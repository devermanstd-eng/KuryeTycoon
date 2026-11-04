using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Arayüz teması: yuvarlak köşeli kartlar, düğmeler, çipler ve sistem yazı tipi.
    /// IMGUI ile çalışır; dokular çalışma anında üretilir (dosya gerekmez).
    /// </summary>
    public static class UITheme
    {
        // ---------- Renkler ----------
        public static readonly Color Bg = new Color(0.075f, 0.082f, 0.10f, 0.98f);       // sol panel
        public static readonly Color Card = new Color(0.135f, 0.145f, 0.175f, 1f);       // kart
        public static readonly Color CardHi = new Color(0.17f, 0.18f, 0.215f, 1f);       // üstüne gelince
        public static readonly Color Line = new Color(1f, 1f, 1f, 0.07f);                // ince çizgi
        public static readonly Color Text = new Color(0.93f, 0.94f, 0.96f);
        public static readonly Color TextDim = new Color(0.62f, 0.65f, 0.71f);
        public static readonly Color Warn = new Color(1f, 0.66f, 0.28f);
        public static readonly Color Good = new Color(0.35f, 0.82f, 0.52f);
        public static readonly Color Bad = new Color(0.95f, 0.38f, 0.36f);

        public static Color Accent => CompanyProfile.Primary;
        public static Color AccentText => CompanyProfile.Secondary;

        // ---------- Çizim ----------

        /// <summary>Yuvarlak köşeli dolu dikdörtgen (sadece Repaint'te çizer).</summary>
        public static void Box(Rect r, Color c, float radius = 10f)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, radius);
        }

        /// <summary>Yuvarlak köşeli çerçeve.</summary>
        public static void Outline(Rect r, Color c, float width, float radius = 10f)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, width, radius);
        }

        /// <summary>Hafif gölgeli kart.</summary>
        public static void Shadowed(Rect r, Color c, float radius = 12f, float shadow = 6f)
        {
            Box(new Rect(r.x - 1, r.y + 2, r.width + 2, r.height + shadow), new Color(0f, 0f, 0f, 0.28f), radius + 2f);
            Box(r, c, radius);
        }

        // ---------- Dokular (GUIStyle arka planları için 9-dilimli) ----------

        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        /// <summary>Kenarları yumuşatılmış yuvarlak köşeli doku; GUIStyle.border = radius ile kullanılır.</summary>
        public static Texture2D RoundTex(Color fill, int radius = 8, Color? border = null)
        {
            string key = $"{fill}{radius}{border}";
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;
            int size = radius * 2 + 4;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            Color b = border ?? fill;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Köşe merkezine uzaklık
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);          // kenar yumuşatma
                    var c = (border.HasValue && d > radius - 1.2f) ? b : fill;
                    c.a *= a;
                    px[y * size + x] = c;
                }
            t.SetPixels(px);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            cache[key] = t;
            return t;
        }

        public static void SetBg(GUIStyleState s, Color c, int radius) => s.background = RoundTex(c, radius);

        // ---------- Yazı tipi ----------

        static Font font;
        static bool fontTried;

        /// <summary>Sistemin modern yazı tipi (Windows: Segoe UI, Mac: Helvetica Neue / SF); yoksa Unity'nin varsayılanı.</summary>
        public static Font UIFont
        {
            get
            {
                if (fontTried) return font;
                fontTried = true;
                try
                {
                    var installed = new HashSet<string>(Font.GetOSInstalledFontNames());
                    foreach (var n in new[] { "Segoe UI", "SF Pro Text", "Helvetica Neue", "Inter", "Roboto", "Arial" })
                        if (installed.Contains(n)) { font = Font.CreateDynamicFontFromOSFont(n, 14); break; }
                }
                catch { font = null; }
                return font;
            }
        }

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }
}
