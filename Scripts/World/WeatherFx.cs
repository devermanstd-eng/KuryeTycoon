using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Havanın görsel simülasyonu (oyuna etkisi WeatherSystem'de; burası sadece görüntü):
    ///  • Yağmur: rüzgârla eğilen damlalar, kararan ve maviye kayan ışık. Fırtınada daha yoğun yağmur, sert rüzgâr, şimşek çakmaları.
    ///  • Kar: sallanarak süzülen kar taneleri; kar yağdıkça şehir beyaza bürünür, kar durunca yavaşça erir.
    ///  • Rüzgâr: rüzgârlı havalarda savrulan yapraklar ve rüzgâr çizgileri; şiddeti ani esintilerle değişir.
    ///  • Güneş: sıcak ışık, köşede güneş parlaması ve hafif ışık hüzmeleri; sıcak havada daha güçlü. Bulutluda gri, soğukta mavi ton.
    /// Geçişler yumuşaktır. Oyun duraklayınca yağış da durur. Ayarlar > Harita > Hava efektleri ile kapatılabilir.
    /// Ekran üstünde çizilir ama arayüzün altında kalır.
    /// </summary>
    public class WeatherFx : MonoBehaviour
    {
        public static WeatherFx I { get; private set; }

        const string PrefKey = "KuryeTycoon.HavaEfektleri";
        public static bool Enabled
        {
            get { return PlayerPrefs.GetInt(PrefKey, 1) == 1; }
            set { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        struct Drop { public Vector2 p; public float speed; public float len; public float phase; public float size; public int kind; }

        readonly List<Drop> rain = new List<Drop>();
        readonly List<Drop> snow = new List<Drop>();
        readonly List<Drop> leaves = new List<Drop>();
        readonly List<Drop> gusts = new List<Drop>();

        // Yumuşak geçen yoğunluklar (0–1)
        float rainAmt, snowAmt, windAmt, sunAmt, cloudAmt, coldAmt, stormAmt, heatAmt;
        float flash, nextLightning = 5f;
        float snowCover;           // şehirdeki kar örtüsü (0–0,55)
        float lastCoverApplied = -1f;

        Texture2D dot, glare, beam;

        void Awake() { I = this; }

        void Start()
        {
            dot = SoftDot(32);
            glare = RadialGlare(256);
            beam = Beam(64, 256);
        }

        // ---------------------------------------------------------------- hedefler

        void Targets(out float r, out float s, out float w, out float sun, out float cloud, out float cold, out float storm, out float heat)
        {
            r = s = w = sun = cloud = cold = storm = heat = 0f;
            var ws = WeatherSystem.I;
            if (ws == null) return;
            switch (ws.Current)
            {
                case WeatherKind.Gunesli: sun = 1f; w = 0.1f; break;
                case WeatherKind.Sicak: sun = 1f; heat = 1f; w = 0.05f; break;
                case WeatherKind.Bulutlu: cloud = 1f; w = 0.35f; break;
                case WeatherKind.Yagmurlu: r = 0.6f; cloud = 1f; w = 0.3f; break;
                case WeatherKind.Soguk: cold = 1f; cloud = 0.4f; w = 0.45f; break;
                case WeatherKind.Karli: s = 1f; cold = 1f; cloud = 0.7f; w = 0.3f; break;
                case WeatherKind.Firtina: r = 1f; cloud = 1f; storm = 1f; w = 1f; break;
            }
        }

        void Update()
        {
            float rdt = Time.unscaledDeltaTime;
            Targets(out float tr, out float ts, out float tw, out float tsun, out float tcloud, out float tcold, out float tstorm, out float theat);
            float k = 1f - Mathf.Exp(-rdt / 2.5f);   // ~2,5 sn'de geçiş
            rainAmt = Mathf.Lerp(rainAmt, tr, k);
            snowAmt = Mathf.Lerp(snowAmt, ts, k);
            sunAmt = Mathf.Lerp(sunAmt, tsun, k);
            cloudAmt = Mathf.Lerp(cloudAmt, tcloud, k);
            coldAmt = Mathf.Lerp(coldAmt, tcold, k);
            stormAmt = Mathf.Lerp(stormAmt, tstorm, k);
            heatAmt = Mathf.Lerp(heatAmt, theat, k);
            // Rüzgâr ani esintilerle dalgalanır
            float gust = Mathf.PerlinNoise(Time.time * 0.25f, 0.37f);
            windAmt = Mathf.Lerp(windAmt, Mathf.Clamp01(tw * (0.6f + gust * 0.8f)), k * 2f);

            bool paused = GameClock.I == null || GameClock.I.Paused;
            float dt = paused ? 0f : rdt;

            // Şimşek (fırtınada 6–16 saniyede bir, çoğu zaman çift çakar)
            flash = Mathf.Max(0f, flash - rdt * 3.5f);
            if (stormAmt > 0.5f && !paused)
            {
                nextLightning -= rdt;
                if (nextLightning <= 0f)
                {
                    flash = Random.Range(0.45f, 0.75f);
                    nextLightning = Random.value < 0.4f ? 0.18f : Random.Range(6f, 16f);
                }
            }

            // Kar örtüsü: kar yağdıkça birikir, yağmayınca erir (sıcakta hızlı)
            float target = snowAmt > 0.3f ? 0.55f : 0f;
            float rate = target > snowCover ? 0.02f : (sunAmt > 0.5f ? 0.03f : 0.01f);
            snowCover = Mathf.MoveTowards(snowCover, target, rate * dt);
            if (CityMap.I != null && Mathf.Abs(snowCover - lastCoverApplied) > 0.015f)
            {
                lastCoverApplied = snowCover;
                CityMap.I.SetSnowCover(Enabled ? snowCover : 0f);
            }

            if (!Enabled) return;
            Simulate(dt);
        }

        // ---------------------------------------------------------------- parçacıklar

        void Ensure(List<Drop> list, int want, System.Func<Drop> make)
        {
            while (list.Count < want) list.Add(make());
            if (list.Count > want) list.RemoveRange(want, list.Count - want);
        }

        Vector2 RandomScreen() => new Vector2(Random.Range(-100f, Screen.width + 100f), Random.Range(-50f, Screen.height));

        void Simulate(float dt)
        {
            float w = windAmt;
            float scale = Screen.height / 900f;

            Ensure(rain, Mathf.RoundToInt(rainAmt * 650f), () => new Drop { p = RandomScreen(), speed = Random.Range(900f, 1400f), len = Random.Range(14f, 26f) });
            Ensure(snow, Mathf.RoundToInt(snowAmt * 420f), () => new Drop { p = RandomScreen(), speed = Random.Range(40f, 90f), size = Random.Range(2f, 5.5f), phase = Random.value * 10f });
            Ensure(leaves, Mathf.RoundToInt(Mathf.Clamp01((w - 0.35f) * 1.6f) * (snowAmt > 0.5f ? 0f : 40f)), () => new Drop { p = RandomScreen(), speed = Random.Range(60f, 140f), size = Random.Range(3f, 6f), phase = Random.value * 10f, kind = Random.Range(0, 3) });
            Ensure(gusts, Mathf.RoundToInt(Mathf.Clamp01((w - 0.4f) * 1.7f) * 22f), () => new Drop { p = RandomScreen(), speed = Random.Range(500f, 900f), len = Random.Range(80f, 200f), phase = Random.value });

            float windX = w * 420f;   // px/sn yatay sürüklenme
            for (int i = 0; i < rain.Count; i++)
            {
                var d = rain[i];
                d.p += new Vector2(windX * 1.2f, d.speed) * dt * scale;
                if (d.p.y > Screen.height + 30f || d.p.x > Screen.width + 120f) { d.p = new Vector2(Random.Range(-200f, Screen.width), Random.Range(-60f, -10f)); }
                rain[i] = d;
            }
            for (int i = 0; i < snow.Count; i++)
            {
                var d = snow[i];
                d.phase += dt;
                d.p += new Vector2(windX * 0.35f + Mathf.Sin(d.phase * 1.3f) * 25f, d.speed) * dt * scale;
                if (d.p.y > Screen.height + 10f || d.p.x > Screen.width + 60f) d.p = new Vector2(Random.Range(-150f, Screen.width), Random.Range(-40f, -5f));
                snow[i] = d;
            }
            for (int i = 0; i < leaves.Count; i++)
            {
                var d = leaves[i];
                d.phase += dt * 3f;
                d.p += new Vector2(windX * 0.9f + d.speed * 0.5f, Mathf.Sin(d.phase) * 60f + 20f) * dt * scale;
                if (d.p.x > Screen.width + 30f || d.p.y > Screen.height + 30f || d.p.y < -40f) d.p = new Vector2(Random.Range(-60f, -10f), Random.Range(0f, Screen.height));
                leaves[i] = d;
            }
            for (int i = 0; i < gusts.Count; i++)
            {
                var d = gusts[i];
                d.p.x += d.speed * dt * scale;
                d.phase += dt * 0.8f;
                if (d.p.x > Screen.width + 220f) { d.p = new Vector2(Random.Range(-400f, -200f), Random.Range(0f, Screen.height)); d.phase = 0f; }
                gusts[i] = d;
            }
        }

        // ---------------------------------------------------------------- çizim

        void OnGUI()
        {
            if (!Enabled || Event.current.type != EventType.Repaint) return;
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            GUI.depth = 10;   // arayüzün (GameUI) altında kalsın
            GUI.matrix = Matrix4x4.identity;
            float sw = Screen.width, sh = Screen.height;
            var full = new Rect(0, 0, sw, sh);
            var old = GUI.color;

            // Işık tonu: güneşte sıcak, bulutluda gri, yağmurda koyu mavi-gri, soğukta mavi
            Tint(full, new Color(1f, 0.85f, 0.55f), sunAmt * (0.05f + heatAmt * 0.05f));
            Tint(full, new Color(0.45f, 0.48f, 0.55f), cloudAmt * 0.10f);
            Tint(full, new Color(0.12f, 0.16f, 0.24f), rainAmt * 0.16f + stormAmt * 0.10f);
            Tint(full, new Color(0.65f, 0.80f, 1f), coldAmt * 0.07f);

            // Güneş: sağ üst köşede parlama ve hafif ışık hüzmeleri
            if (sunAmt > 0.02f)
            {
                float a = sunAmt * (0.35f + heatAmt * 0.2f);
                float size = sh * (0.9f + heatAmt * 0.3f);
                GUI.color = new Color(1f, 0.92f, 0.65f, a);
                GUI.DrawTexture(new Rect(sw - size * 0.55f, -size * 0.45f, size, size), glare);
                // Hüzmeler: köşeden çapraza doğru, yavaşça nefes alır
                float t = Time.unscaledTime;
                for (int i = 0; i < 4; i++)
                {
                    float breathe = 0.5f + 0.5f * Mathf.Sin(t * 0.3f + i * 1.7f);
                    GUI.color = new Color(1f, 0.95f, 0.75f, sunAmt * 0.06f * breathe);
                    var pivot = new Vector2(sw - sh * 0.05f, -sh * 0.05f);
                    var m = GUI.matrix;
                    GUIUtility.RotateAroundPivot(35f + i * 12f, pivot);
                    GUI.DrawTexture(new Rect(pivot.x - sh * 0.08f, pivot.y, sh * (0.16f + i * 0.03f), sh * 1.6f), beam);
                    GUI.matrix = m;
                }
            }

            // Rüzgâr çizgileri
            foreach (var g in gusts)
            {
                float fade = Mathf.Sin(Mathf.Clamp01(g.phase) * Mathf.PI);
                GUI.color = new Color(1f, 1f, 1f, 0.10f * fade);
                GUI.DrawTexture(new Rect(g.p.x, g.p.y, g.len, 1.5f), Texture2D.whiteTexture);
            }

            // Yağmur: rüzgâr yönüne eğik ince çizgiler
            if (rain.Count > 0)
            {
                float angle = Mathf.Atan2(windAmt * 420f * 1.2f, 1100f) * Mathf.Rad2Deg;
                GUI.color = new Color(0.75f, 0.82f, 0.95f, 0.35f + stormAmt * 0.1f);
                var m = GUI.matrix;
                foreach (var d in rain)
                {
                    GUI.matrix = m;
                    GUIUtility.RotateAroundPivot(-angle, d.p);
                    GUI.DrawTexture(new Rect(d.p.x, d.p.y, 1.4f, d.len), Texture2D.whiteTexture);
                }
                GUI.matrix = m;
            }

            // Kar taneleri
            foreach (var d in snow)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.85f);
                GUI.DrawTexture(new Rect(d.p.x - d.size / 2f, d.p.y - d.size / 2f, d.size, d.size), dot);
            }

            // Savrulan yapraklar
            var leafCols = new[] { new Color(0.45f, 0.55f, 0.20f, 0.9f), new Color(0.75f, 0.50f, 0.15f, 0.9f), new Color(0.55f, 0.30f, 0.15f, 0.9f) };
            foreach (var d in leaves)
            {
                GUI.color = leafCols[d.kind];
                var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(d.phase * 60f, d.p);
                GUI.DrawTexture(new Rect(d.p.x - d.size, d.p.y - d.size * 0.5f, d.size * 2f, d.size), dot);
                GUI.matrix = m;
            }

            // Şimşek
            if (flash > 0f) Tint(full, new Color(0.9f, 0.93f, 1f), flash * 0.55f);

            GUI.color = old;
        }

        static void Tint(Rect r, Color c, float a)
        {
            if (a <= 0.001f) return;
            GUI.color = new Color(c.r, c.g, c.b, a);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }

        // ---------------------------------------------------------------- dokular

        static Texture2D SoftDot(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            float r = n / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(1f - d * d) * 255f));
                }
            t.SetPixels32(px); t.Apply();
            return t;
        }

        static Texture2D RadialGlare(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            float r = n / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (0.6f + 0.4f * a);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px); t.Apply();
            return t;
        }

        static Texture2D Beam(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float across = 1f - Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                    float along = (float)y / h;   // köşeden uzaklaştıkça söner (y=0 alt; GUI'de üst)
                    float a = across * across * Mathf.Clamp01(along);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px); t.Apply();
            return t;
        }
    }
}
