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
        readonly List<Drop> clouds = new List<Drop>();    // yerde kayan bulut gölgeleri
        readonly List<Drop> splashes = new List<Drop>();  // yağmur damlası sıçramaları
        float wetness;             // yerin ıslaklığı (0–1)
        float lastWetApplied = -1f;

        // Yumuşak geçen yoğunluklar (0–1)
        float rainAmt, snowAmt, windAmt, sunAmt, cloudAmt, coldAmt, stormAmt, heatAmt;
        float flash, nextLightning = 5f;
        float snowCover;           // şehirdeki kar örtüsü (0–0,55)
        float lastCoverApplied = -1f;

        Texture2D dot, glare, beam, puff;

        // Hava3D (Blender'dan gelen 3D bulut, yağmur, kar, yaprak) bu yoğunlukları okur
        public float RainAmt => rainAmt;
        public float SnowAmt => snowAmt;
        public float WindAmt => windAmt;
        public float SunAmt => sunAmt;
        public float CloudAmt => cloudAmt;
        public float StormAmt => stormAmt;
        public float Flash => flash;

        void Awake() { I = this; }

        void Start()
        {
            dot = SoftDot(32);
            glare = RadialGlare(256);
            beam = Beam(64, 256);
            puff = CloudPuff(256, 128);
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
            // Geçiş süresi oyun hızına göre: hızlandırınca hava da çabuk değişir, efekt geride kalmasın
            float spd = GameClock.I != null && !GameClock.I.Paused ? Mathf.Max(1f, GameClock.I.SpeedMultiplier) : 1f;
            float tt = Mathf.Clamp(2.5f / Mathf.Sqrt(spd), 0.25f, 2.5f);
            float k = 1f - Mathf.Exp(-rdt / tt);
            float kOut = 1f - Mathf.Exp(-rdt / (tt * 0.5f));   // yağış dururken daha hızlı kesilsin
            rainAmt = Mathf.Lerp(rainAmt, tr, tr < rainAmt ? kOut : k);
            snowAmt = Mathf.Lerp(snowAmt, ts, ts < snowAmt ? kOut : k);
            if (tr <= 0f && rainAmt < 0.03f) rainAmt = 0f;
            if (ts <= 0f && snowAmt < 0.03f) snowAmt = 0f;
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

            // Kar örtüsü ve ıslaklık oyun zamanıyla değişir (her hızda tutarlı): kar ~2 saatte birikir, güneşte ~3, bulutta ~10 saatte erir
            float gm = paused || GameClock.I == null ? 0f : GameClock.I.DeltaMinutes;
            float target = snowAmt > 0.3f ? 0.55f : 0f;
            float rate = target > snowCover ? 0.55f / 120f : (sunAmt > 0.5f ? 0.55f / 180f : 0.55f / 600f);
            snowCover = Mathf.MoveTowards(snowCover, target, rate * gm);
            if (CityMap.I != null && Mathf.Abs(snowCover - lastCoverApplied) > 0.015f)
            {
                lastCoverApplied = snowCover;
                CityMap.I.SetSnowCover(Enabled ? snowCover : 0f);
            }

            // Islak zemin: yağmurda kararır, yağmur durunca yavaşça kurur (güneşte hızlı)
            float wetTarget = Mathf.Clamp01(rainAmt * 1.2f);
            // Islanma ~20 oyun dakikasında, kuruma güneşte ~1,5 saatte, bulutta ~4 saatte
            float wetRate = wetTarget > wetness ? 1f / 20f : (sunAmt > 0.5f ? 1f / 90f : 1f / 240f);
            wetness = Mathf.MoveTowards(wetness, wetTarget, wetRate * gm);
            if (CityMap.I != null && Mathf.Abs(wetness - lastWetApplied) > 0.02f)
            {
                lastWetApplied = wetness;
                CityMap.I.SetWetness(Enabled ? wetness : 0f);
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

            Ensure(rain, Mathf.RoundToInt(rainAmt * 700f), () => new Drop { p = RandomScreen(), speed = Random.Range(900f, 1400f), len = Random.Range(18f, 34f) });
            Ensure(splashes, Mathf.RoundToInt(rainAmt * 60f), () => new Drop { p = RandomScreen(), phase = Random.value, size = Random.Range(6f, 14f) });
            // Bulutlar: güneşte birkaç küçük beyaz bulut, bulutluda çok ve büyük, yağmurda/fırtınada koyu ve kalabalık
            int cloudCount = Mathf.RoundToInt(sunAmt * 3f + cloudAmt * 8f + rainAmt * 3f + stormAmt * 3f + snowAmt * 2f);
            Ensure(clouds, cloudCount, () => new Drop
            {
                p = new Vector2(Random.Range(-Screen.width * 0.2f, Screen.width), Random.Range(Screen.height * 0.08f, Screen.height * 0.9f)),
                size = Random.Range(0.28f, 0.55f) * Screen.height * (sunAmt > 0.5f ? 0.7f : 1f),
                speed = Random.Range(14f, 30f),
                phase = Random.value
            });
            Ensure(snow, Mathf.RoundToInt(snowAmt * 520f), () => new Drop { p = RandomScreen(), speed = Random.Range(40f, 90f), size = Random.Range(2f, 5.5f), phase = Random.value * 10f });
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
            for (int i = 0; i < splashes.Count; i++)
            {
                var d = splashes[i];
                d.phase += dt * 2.5f;
                if (d.phase >= 1f) { d.phase = 0f; d.p = RandomScreen(); d.size = Random.Range(6f, 14f); }
                splashes[i] = d;
            }
            for (int i = 0; i < clouds.Count; i++)
            {
                var d = clouds[i];
                d.p.x += (d.speed + windX * 0.15f) * dt * scale;
                if (d.p.x - d.size > Screen.width) d.p = new Vector2(-d.size * 1.2f, Random.Range(0f, Screen.height));
                clouds[i] = d;
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
            GUI.depth = 10;
            if (Event.current.type != EventType.Repaint) return;
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            // GUI.depth her olayda (Start'ta da) ayarlanır: arayüzün (GameUI) altında kalsın
            GUI.matrix = Matrix4x4.identity;
            float sw = Screen.width, sh = Screen.height;
            var full = new Rect(0, 0, sw, sh);
            var old = GUI.color;

            // Gece ve gündüz: akşam turuncu, gece karanlık; pencere ışıkları ve sokak lambaları (hava efektleri kapalıyken de)
            DayNight.Draw(sw, sh);
            if (!Enabled) { GUI.color = old; return; }
            float sunV = sunAmt * DayNight.Daylight01;

            // Işık tonu: güneşte sıcak, bulutluda gri, yağmurda koyu mavi-gri, soğukta mavi
            Tint(full, new Color(1f, 0.82f, 0.50f), sunV * (0.08f + heatAmt * 0.08f));
            Tint(full, new Color(0.42f, 0.45f, 0.52f), cloudAmt * 0.16f);
            Tint(full, new Color(0.10f, 0.14f, 0.22f), rainAmt * 0.22f + stormAmt * 0.16f);
            Tint(full, new Color(0.62f, 0.78f, 1f), coldAmt * 0.10f);

            // 3D hava açıkken (yakın görünüm) bulut, yağmur, kar ve yapraklar 3D modellerle çizilir: ekrandakiler atlanır
            bool hava3D = Hava3D.Showing;

            // Bulut gölgeleri: haritanın üstünden rüzgârla kayan koyu lekeler
            if (!hava3D)
            foreach (var c in clouds)
            {
                GUI.color = new Color(0.05f, 0.06f, 0.09f, 0.18f * Mathf.Clamp01(cloudAmt + rainAmt + sunV * 0.5f));
                GUI.DrawTexture(new Rect(c.p.x - c.size, c.p.y - c.size * 0.4f + c.size * 0.25f, c.size * 2f, c.size * 0.8f), puff);
            }

            // Güneş: sağ üst köşede parlama ve hafif ışık hüzmeleri
            if (sunV > 0.02f)
            {
                float a = sunV * (0.55f + heatAmt * 0.25f);
                float size = sh * (1.0f + heatAmt * 0.3f);
                // Güneş üst barın hemen altında, sağ üstte
                var sunC = new Vector2(sw * 0.88f, sh * 0.12f);
                GUI.color = new Color(1f, 0.90f, 0.60f, a);
                GUI.DrawTexture(new Rect(sunC.x - size / 2f, sunC.y - size / 2f, size, size), glare);
                GUI.color = new Color(1f, 0.98f, 0.85f, sunV * 0.8f);
                GUI.DrawTexture(new Rect(sunC.x - sh * 0.04f, sunC.y - sh * 0.04f, sh * 0.08f, sh * 0.08f), dot);
                // Hüzmeler: köşeden çapraza doğru, yavaşça nefes alır
                float t = Time.unscaledTime;
                for (int i = 0; i < 4; i++)
                {
                    float breathe = 0.5f + 0.5f * Mathf.Sin(t * 0.3f + i * 1.7f);
                    GUI.color = new Color(1f, 0.95f, 0.75f, sunV * 0.10f * breathe);
                    var pivot = new Vector2(sw * 0.88f, sh * 0.12f);
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
            if (rain.Count > 0 && !hava3D)
            {
                float angle = Mathf.Atan2(windAmt * 420f * 1.2f, 1100f) * Mathf.Rad2Deg;
                GUI.color = new Color(0.80f, 0.86f, 0.98f, 0.55f + stormAmt * 0.15f);
                var m = GUI.matrix;
                foreach (var d in rain)
                {
                    GUI.matrix = m;
                    GUIUtility.RotateAroundPivot(-angle, d.p);
                    GUI.DrawTexture(new Rect(d.p.x, d.p.y, 2f, d.len), Texture2D.whiteTexture);
                }
                GUI.matrix = m;
                // Yerde sıçrayan damlalar: büyüyüp sönen halkalar
                foreach (var sp in splashes)
                {
                    float r = sp.size * (0.4f + sp.phase);
                    GUI.color = new Color(0.85f, 0.9f, 1f, 0.35f * (1f - sp.phase));
                    GUI.DrawTexture(new Rect(sp.p.x - r, sp.p.y - r * 0.4f, r * 2f, r * 0.8f), dot);
                }
            }

            // Kar taneleri
            if (!hava3D)
            foreach (var d in snow)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.95f);
                float sz = d.size * 1.3f;
                GUI.DrawTexture(new Rect(d.p.x - sz / 2f, d.p.y - sz / 2f, sz, sz), dot);
            }

            // Savrulan yapraklar
            var leafCols = new[] { new Color(0.45f, 0.55f, 0.20f, 0.9f), new Color(0.75f, 0.50f, 0.15f, 0.9f), new Color(0.55f, 0.30f, 0.15f, 0.9f) };
            if (!hava3D)
            foreach (var d in leaves)
            {
                GUI.color = leafCols[d.kind];
                var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(d.phase * 60f, d.p);
                GUI.DrawTexture(new Rect(d.p.x - d.size, d.p.y - d.size * 0.5f, d.size * 2f, d.size), dot);
                GUI.matrix = m;
            }

            // Bulutlar (gölgeleri yukarıda yere düştü): yağmurda koyulaşır
            float darkness = Mathf.Clamp01(rainAmt * 0.55f + stormAmt * 0.35f + snowAmt * 0.15f);
            var cloudCol = Color.Lerp(new Color(1f, 1f, 1f), new Color(0.38f, 0.40f, 0.45f), darkness);
            float cloudAlpha = Mathf.Clamp01(0.35f + cloudAmt * 0.25f + rainAmt * 0.15f) * (sunAmt > 0.5f ? 0.75f : 1f);
            if (!hava3D)
            foreach (var c in clouds)
            {
                float w2 = c.size * 2f, h2 = c.size;
                GUI.color = new Color(cloudCol.r, cloudCol.g, cloudCol.b, cloudAlpha);
                GUI.DrawTexture(new Rect(c.p.x - w2 / 2f, c.p.y - h2 / 2f - c.size * 0.35f, w2, h2), puff);
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

        /// <summary>Kabarık bulut: üst üste binen yumuşak yuvarlaklar (her oyunda aynı şekil).</summary>
        static Texture2D CloudPuff(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[w * h];
            var rng = new System.Random(31);
            var blobs = new List<Vector3>();   // x, y, yarıçap (0–1 arası, w'ye göre)
            for (int i = 0; i < 9; i++)
                blobs.Add(new Vector3(0.18f + (float)rng.NextDouble() * 0.64f, 0.35f + (float)rng.NextDouble() * 0.25f, 0.12f + (float)rng.NextDouble() * 0.12f));
            blobs.Add(new Vector3(0.5f, 0.45f, 0.26f));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h * 0.5f;   // en-boy oranı 2:1
                    float a = 0f;
                    foreach (var b in blobs)
                    {
                        float d = Vector2.Distance(new Vector2(u, v), new Vector2(b.x, b.y * 0.5f)) / b.z;
                        a = Mathf.Max(a, Mathf.Clamp01(1f - d * d));
                    }
                    a = Mathf.SmoothStep(0f, 1f, a);
                    // Alt kısım biraz koyu (hacim hissi)
                    byte shade = (byte)(Mathf.Lerp(215f, 255f, Mathf.Clamp01(v * 2.6f)));
                    px[y * w + x] = new Color32(shade, shade, (byte)Mathf.Min(255, shade + 8), (byte)(a * 255f));
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
