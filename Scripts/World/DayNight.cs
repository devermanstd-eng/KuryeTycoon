using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Gece / gündüz döngüsü (sadece görüntü):
    ///  • Gün doğumu ve batımı mevsime göre değişir (Nyíregyháza: Aralık'ta ~07:30–15:50, Haziran'da ~04:45–20:45).
    ///  • Şafakta pembe, akşamüstü turuncu ışık; gece şehir kararır (lacivert ton).
    ///  • Akşam binaların pencerelerinde ışıklar yanar (gece ilerledikçe azalır), kavşaklarda ve yollarda sokak lambaları yanar.
    /// WeatherFx'in OnGUI'si içinden çizilir: arayüzün altında, yağmur/karın altında kalır.
    /// Ayarlar > Harita > "Gece ve gündüz" ile kapatılabilir.
    /// </summary>
    public static class DayNight
    {
        const string PrefKey = "KuryeTycoon.GeceGunduz";
        static int enabledCache = -1;
        public static bool Enabled
        {
            get
            {
                if (enabledCache < 0) enabledCache = PlayerPrefs.GetInt(PrefKey, 1);
                return enabledCache == 1;
            }
            set
            {
                enabledCache = value ? 1 : 0;
                PlayerPrefs.SetInt(PrefKey, enabledCache);
                PlayerPrefs.Save();
            }
        }

        // ---------------------------------------------------------------- saat ve mevsim

        public static float HourF => GameClock.I == null ? 12f : (GameClock.I.TotalMinutes % 1440f) / 60f;

        static float SeasonCos
        {
            get
            {
                if (GameClock.I == null) return 0f;
                // Yılın günü → ay (kesirli). En uzun gün ~21 Haziran (6,7)
                float m = GameClock.I.Month + (GameClock.I.DayOfMonth - 1) / 30f;
                return Mathf.Cos(2f * Mathf.PI * (m - 6.7f) / 12f);
            }
        }

        public static float Sunrise => 6.1f - 1.4f * SeasonCos;
        public static float Sunset => 18.3f + 2.45f * SeasonCos;

        /// <summary>0 = gündüz, 1 = tam gece.</summary>
        public static float Night01
        {
            get
            {
                float h = HourF;
                if (h >= 12f) return Smooth(Sunset - 0.3f, Sunset + 1.4f, h);
                return 1f - Smooth(Sunrise - 0.8f, Sunrise + 0.6f, h);
            }
        }

        /// <summary>Güneşin görünürlüğü (hava efektlerindeki güneş parlaması bununla çarpılır).</summary>
        public static float Daylight01 => Enabled ? 1f - Night01 : 1f;

        public static float Dusk01
        {
            get
            {
                float h = HourF;
                if (h < 12f) return 0f;
                return Mathf.Clamp01(1f - Mathf.Abs(h - (Sunset - 0.1f)) / 1.3f);
            }
        }

        public static float Dawn01
        {
            get
            {
                float h = HourF;
                if (h >= 12f) return 0f;
                return Mathf.Clamp01(1f - Mathf.Abs(h - (Sunrise + 0.2f)) / 1.1f);
            }
        }

        /// <summary>Penceresi yanan binaların oranı: akşam çok, gece yarısından sonra azalır, sabaha karşı biraz artar.</summary>
        static float LitFraction
        {
            get
            {
                float h = HourF;
                if (h >= 12f) return 0.62f;
                if (h < 5f) return Mathf.Lerp(0.5f, 0.14f, h / 5f);
                return 0.32f;
            }
        }

        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        public static string PhaseName
        {
            get
            {
                float n = Night01;
                if (Dusk01 > 0.4f) return "Gün batımı";
                if (Dawn01 > 0.4f) return "Şafak";
                return n > 0.6f ? "Gece" : "Gündüz";
            }
        }

        // ---------------------------------------------------------------- çizim

        static Texture2D glow, dotTex;

        static Texture2D Glow()
        {
            if (glow != null) return glow;
            int n = 64;
            glow = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    px[y * n + x] = new Color(1f, 1f, 1f, a * a);
                }
            glow.SetPixels(px);
            glow.Apply();
            return glow;
        }

        static Texture2D Dot()
        {
            if (dotTex != null) return dotTex;
            int n = 8;
            dotTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool edge = x == 0 || y == 0 || x == n - 1 || y == n - 1;
                    px[y * n + x] = new Color(1f, 1f, 1f, edge ? 0.35f : 1f);
                }
            dotTex.SetPixels(px);
            dotTex.Apply();
            return dotTex;
        }

        static void Tint(Rect r, Color c, float a)
        {
            if (a <= 0.002f) return;
            GUI.color = new Color(c.r, c.g, c.b, a);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }

        static uint Hash(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return h;
            }
        }

        static float Rand01(int x, int y, int salt) => (Hash(x, y, salt) & 0xFFFF) / 65535f;

        /// <summary>Ekran pikseli cinsinden (GUI.matrix birim) çizer. WeatherFx.OnGUI çağırır.</summary>
        public static void Draw(float sw, float sh)
        {
            if (!Enabled || GameClock.I == null) return;
            var full = new Rect(0, 0, sw, sh);
            float night = Night01, dusk = Dusk01, dawn = Dawn01;

            // Işık tonu. Gerçek güneş ışığı açıksa (Gorsel3D) binalar zaten renklenip kararıyor: ekran tonu hafif kalır,
            // sadece ışıktan etkilenmeyen 2D zemini akşama ve geceye uydurur.
            bool lit3D = Gorsel3D.Active;
            Tint(full, new Color(1f, 0.48f, 0.18f), dusk * (lit3D ? 0.08f : 0.20f));
            Tint(full, new Color(1f, 0.62f, 0.72f), dawn * (lit3D ? 0.05f : 0.13f));
            // 3D ışıkta zemin ve kır kendisi kararır (CityMap.SetNight): ekran tonu sadece hafif bir gece havası katar
            Tint(full, new Color(0.02f, 0.035f, 0.11f), night * (lit3D ? 0.16f : 0.56f));

            if (night < 0.15f) return;
            var cam = Camera.main;
            if (cam == null || City3D.I == null || CityMap.I == null) return;
            float lights = Mathf.Clamp01((night - 0.15f) / 0.5f);

            // Bir karenin ekrandaki boyu (lamba ve pencere ölçüsü için)
            var center = new Vector3(CityMap.Width / 2f, CityMap.Height / 2f, 0f);
            Vector3 a0 = cam.WorldToScreenPoint(center), a1 = cam.WorldToScreenPoint(center + Vector3.right);
            float tilePx = Mathf.Max(2f, Vector2.Distance(a0, a1));
            var vp = cam.pixelRect;

            // 3D görünümde lambalar ve pencereler modellerin kendisinde yanar (GeceIsiklari): ekrana ayrıca çizilmez
            if (GeceIsiklari.Showing) return;
            DrawStreetLamps(cam, vp, tilePx, lights);
            DrawWindows(cam, vp, tilePx, lights);
        }

        static bool OnScreen(Rect vp, Vector3 sp, float margin) =>
            sp.z > 0f && sp.x > vp.xMin - margin && sp.x < vp.xMax + margin && sp.y > vp.yMin - margin && sp.y < vp.yMax + margin;

        static void DrawStreetLamps(Camera cam, Rect vp, float tilePx, float lights)
        {
            var g = Glow();
            float size = tilePx * 1.5f;
            float sh = Screen.height;
            int b = CityMap.Block;
            for (int y = 0; y < CityMap.Height; y += 2)
                for (int x = 0; x < CityMap.Width; x += 2)
                {
                    if (!CityMap.RoadAt(x, y)) continue;
                    bool cross = x % b == 0 && y % b == 0;
                    // Kavşaklarda her zaman, ara yollarda yarım blokta bir lamba
                    if (!cross && !((x % b == 2 && y % b == 0) || (y % b == 2 && x % b == 0))) continue;
                    if (CityMap.WaterAt(x, y)) continue;
                    var sp = cam.WorldToScreenPoint(new Vector3(x, y, -0.05f));
                    if (!OnScreen(vp, sp, size)) continue;
                    // Yer yer yanıp sönen, sıcak sarı lambalar
                    float flicker = Rand01(x, y, 7) < 0.04f ? (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f + x)) : 1f;
                    float s = size * (cross ? 1.15f : 0.85f);
                    GUI.color = new Color(1f, 0.80f, 0.45f, 0.42f * lights * flicker);
                    GUI.DrawTexture(new Rect(sp.x - s / 2f, sh - sp.y - s / 2f, s, s), g);
                    float c = Mathf.Max(2f, tilePx * 0.12f);
                    GUI.color = new Color(1f, 0.95f, 0.80f, 0.9f * lights * flicker);
                    GUI.DrawTexture(new Rect(sp.x - c / 2f, sh - sp.y - c / 2f, c, c), g);
                }
        }

        static void DrawWindows(Camera cam, Rect vp, float tilePx, float lights)
        {
            var heights = City3D.I.BuildingHeights;
            if (heights == null || heights.Count == 0) return;
            var d = Dot();
            var g = Glow();
            float grow = MapCamera.I != null ? MapCamera.I.Tilt01 : 0f;
            float lit = LitFraction;
            float sh = Screen.height;
            float win = Mathf.Clamp(tilePx * 0.07f, 1.5f, 6f);
            // Gece kararınca her gün farklı binalar yansın diye günün tohumu
            int daySalt = GameClock.I.Day;
            bool detail = tilePx > 14f;

            foreach (var kv in heights)
            {
                var t = kv.Key;
                if (Rand01(t.x, t.y, daySalt) > lit) continue;
                if (!CityMap.I.IsUnlocked(t) && Rand01(t.x, t.y, 3) > 0.5f) continue;   // kilitli bölge daha sönük
                float h = kv.Value * grow;
                var basePos = new Vector3(t.x, t.y, 0f);
                var top = cam.WorldToScreenPoint(basePos + new Vector3(0f, 0f, -h));
                if (!OnScreen(vp, top, tilePx)) continue;
                var bottom = cam.WorldToScreenPoint(basePos);
                bool warm = Rand01(t.x, t.y, 11) < 0.75f;
                var col = warm ? new Color(1f, 0.83f, 0.45f) : new Color(0.75f, 0.88f, 1f);

                // Hafif hale (uzaktan bakınca bina ışıl ışıl görünsün)
                float hs = tilePx * 0.9f;
                GUI.color = new Color(col.r, col.g, col.b, 0.16f * lights);
                GUI.DrawTexture(new Rect(top.x - hs / 2f, sh - top.y - hs / 2f, hs, hs), g);
                if (!detail) continue;

                // Pencereler: zeminden çatıya kat kat, cephe boyunca birkaç sütun
                float px = bottom.y - top.y;                         // binanın ekrandaki boyu (piksel)
                int floors = Mathf.Clamp(Mathf.RoundToInt(px / (win * 3.2f)), 1, 8);
                int cols = tilePx > 34f ? 3 : 2;
                for (int f = 0; f < floors; f++)
                {
                    float k = (f + 0.6f) / (floors + 0.4f);
                    float wy = Mathf.Lerp(bottom.y, top.y, k);
                    float wx = Mathf.Lerp(bottom.x, top.x, k);
                    for (int c = 0; c < cols; c++)
                    {
                        if (Rand01(t.x * 7 + c, t.y * 13 + f, daySalt + 5) > 0.55f) continue;
                        float off = (c - (cols - 1) / 2f) * tilePx * 0.2f;
                        GUI.color = new Color(col.r, col.g, col.b, 0.9f * lights);
                        GUI.DrawTexture(new Rect(wx + off - win / 2f, sh - wy - win * 0.6f, win, win * 1.2f), d);
                    }
                }
            }
        }
    }
}
