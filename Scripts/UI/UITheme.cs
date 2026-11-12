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
        // ---------- Kâğıt teması (CityRT tarzı): krem paneller, koyu kahve mürekkep çerçeve ve yazı ----------
        // Oyunun tek teması koyu tema (iki temayı birden bakımlı tutmak yerine biri seçildi). Kâğıt temasının kodu duruyor:
        // burayı true yapınca krem paneller ve elle çizilmiş simgeler geri gelir.
        public static bool Kagit => false;
        /// <summary>Bu kapsamda çizilenler eski koyu renklerinde kalır (kurye telefonu ekranı).</summary>
        public static bool Klasik;
        static bool KagitAcik => Kagit && !Klasik;

        public static readonly Color Ink = new Color(0.227f, 0.173f, 0.145f);            // mürekkep (çerçeve, yazı)
        public static readonly Color Paper = new Color(0.984f, 0.953f, 0.886f);          // krem panel
        public static readonly Color PaperCard = new Color(1f, 0.979f, 0.94f);           // açık kart
        public static readonly Color PaperHi = new Color(1f, 0.94f, 0.82f);              // üstüne gelince

        static readonly Color DarkBg = new Color(0.075f, 0.082f, 0.10f, 0.98f);
        static readonly Color DarkCard = new Color(0.135f, 0.145f, 0.175f, 1f);
        static readonly Color DarkCardHi = new Color(0.17f, 0.18f, 0.215f, 1f);

        // ---------- Renkler ----------
        public static Color Bg => KagitAcik ? Paper : DarkBg;                             // sol panel
        public static Color Card => KagitAcik ? PaperCard : DarkCard;                     // kart
        public static Color CardHi => KagitAcik ? PaperHi : DarkCardHi;                   // üstüne gelince
        public static Color Line => KagitAcik ? new Color(Ink.r, Ink.g, Ink.b, 0.22f) : new Color(1f, 1f, 1f, 0.07f);   // ince çizgi
        public static Color Text => KagitAcik ? Ink : new Color(0.93f, 0.94f, 0.96f);
        public static Color TextDim => KagitAcik ? new Color(0.47f, 0.39f, 0.33f) : new Color(0.62f, 0.65f, 0.71f);
        public static Color Warn => KagitAcik ? new Color(0.86f, 0.45f, 0.10f) : new Color(1f, 0.66f, 0.28f);
        public static Color Good => KagitAcik ? new Color(0.22f, 0.58f, 0.30f) : new Color(0.35f, 0.82f, 0.52f);
        public static Color Bad => KagitAcik ? new Color(0.84f, 0.26f, 0.22f) : new Color(0.95f, 0.38f, 0.36f);
        /// <summary>Vurgulu yazı (eskiden beyaz): kâğıt temada mürekkep.</summary>
        public static Color Strong => KagitAcik ? Ink : Color.white;

        static float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        static float Sat(Color c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b));

        /// <summary>Koyu, renksiz bir zemin rengi mi (eski panel / kart / düğme)?</summary>
        static bool KoyuZemin(Color c) => c.a > 0.5f && Lum(c) < 0.33f && Sat(c) < 0.13f;

        /// <summary>Eski koyu zemin rengini kâğıt tonuna çevirir: en koyu → krem panel, orta → açık kart, açığa yakın → sıcak vurgu.</summary>
        public static Color Zemin(Color c)
        {
            if (!KagitAcik || !KoyuZemin(c)) return c;
            float l = Lum(c);
            Color k = l < 0.12f ? Color.Lerp(Paper, PaperCard, Mathf.InverseLerp(0.05f, 0.12f, l))
                                : Color.Lerp(PaperCard, PaperHi, Mathf.InverseLerp(0.14f, 0.27f, l));
            k.a = c.a;
            return k;
        }

        /// <summary>
        /// Yazı rengi: kâğıt temada açık renksiz yazılar mürekkebe, açık renkli yazılar okunur koyu tona döner
        /// (krem zemin üstünde beyaz yazı kaybolmasın). Renkli zemindeki (şirket rengi düğme) yazılar için kullanılmaz.
        /// </summary>
        public static Color Yazi(Color c)
        {
            if (!KagitAcik) return c;
            float l = Lum(c), sat = Sat(c);
            Color r;
            if (sat < 0.2f)
            {
                if (l < 0.45f) return c;                                              // zaten koyu
                r = Color.Lerp(TextDim, Ink, Mathf.InverseLerp(0.6f, 0.9f, l));
            }
            else
            {
                if (l < 0.5f) return c;
                r = Color.Lerp(c, new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f), Mathf.InverseLerp(0.5f, 0.9f, l));
            }
            r.a = c.a;
            return r;
        }

        /// <summary>Stilin bütün durumlarındaki yazı rengini kâğıt temaya çevirir (arka planı koyu olanlar hariç).</summary>
        public static void KagitYazi(GUIStyle s)
        {
            if (s == null || !Kagit) return;
            foreach (var st in new[] { s.normal, s.hover, s.active, s.focused })
                if (st.background == null || !st.background.name.StartsWith("renkli")) st.textColor = Yazi(st.textColor);
        }

        public static Color Accent => CompanyProfile.Primary;
        public static Color AccentText => CompanyProfile.Secondary;

        // ---------- Çizim ----------

        /// <summary>Yuvarlak köşeli dolu dikdörtgen (sadece Repaint'te çizer).</summary>
        public static void Box(Rect r, Color c, float radius = 10f)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (KagitAcik && KoyuZemin(c) && r.width >= 14f && r.height >= 14f)
            {
                // Kâğıt panel: Grease Pencil ile çizilmiş krem çerçeve (Blender: Tools/Blender/arayuz_gp.py) ya da yedek doku
                KagitPanel(r, Zemin(c), radius);
                return;
            }
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, Zemin(c), 0f, radius);
        }

        static GUIStyle panelStyle, kartStyle;
        static bool panelTried;

        static GUIStyle PanelStyle(string ad)
        {
            var t = Resources.Load<Texture2D>("UI/Panel/" + ad);
            if (t == null) return null;
            int b = Mathf.RoundToInt(t.width * 0.27f);
            return new GUIStyle { normal = { background = t }, border = new RectOffset(b, b, b, b) };
        }

        /// <summary>Krem kâğıt panel + mürekkep çerçeve. Köşe boyu kutuya göre küçülür (küçük çiplerde de düzgün).</summary>
        static void KagitPanel(Rect r, Color fill, float radius)
        {
            if (!panelTried) { panelTried = true; panelStyle = PanelStyle("panel"); kartStyle = PanelStyle("kart"); }
            var st = Lum(fill) < 0.96f ? panelStyle : kartStyle ?? panelStyle;
            float min = Mathf.Min(r.width, r.height);
            if (st != null && min >= 28f && radius >= 6f)
            {
                var old = GUI.color;
                // Doku kendi krem rengini taşır; istenen ton (hover, kart) çarpımla yaklaştırılır
                var tex = Lum(fill) < 0.96f ? new Color(0.984f, 0.953f, 0.886f) : new Color(1f, 0.979f, 0.94f);
                GUI.color = new Color(old.r * Mathf.Clamp01(fill.r / tex.r), old.g * Mathf.Clamp01(fill.g / tex.g), old.b * Mathf.Clamp01(fill.b / tex.b), old.a * fill.a);
                st.Draw(r, false, false, false, false);
                GUI.color = old;
                return;
            }
            float rad = Mathf.Min(radius, min * 0.5f);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, fill, 0f, rad);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(Ink.r, Ink.g, Ink.b, fill.a), 1.6f, rad);
        }

        /// <summary>Yuvarlak köşeli çerçeve.</summary>
        public static void Outline(Rect r, Color c, float width, float radius = 10f)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, KagitAcik && Lum(c) > 0.6f && Sat(c) < 0.2f ? new Color(Ink.r, Ink.g, Ink.b, Mathf.Max(0.35f, c.a)) : c, width, radius);
        }

        /// <summary>Hafif gölgeli kart.</summary>
        public static void Shadowed(Rect r, Color c, float radius = 12f, float shadow = 6f)
        {
            Box(new Rect(r.x - 1, r.y + 2, r.width + 2, r.height + shadow), new Color(0.23f, 0.17f, 0.12f, KagitAcik ? 0.18f : 0.28f), radius + 2f);
            Box(r, c, radius);
        }

        // ---------- Dokular (GUIStyle arka planları için 9-dilimli) ----------

        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        /// <summary>Kenarları yumuşatılmış yuvarlak köşeli doku; GUIStyle.border = radius ile kullanılır.</summary>
        public static Texture2D RoundTex(Color fill, int radius = 8, Color? border = null)
        {
            // Kâğıt tema: koyu zeminler krem olur ve mürekkep çerçeve alır; renkli (şirket rengi) dokular "renkli" adını taşır
            bool kagit = KagitAcik && KoyuZemin(fill);
            if (kagit) { fill = Zemin(fill); border = border ?? Ink; }
            string key = $"{fill}{radius}{border}{kagit}";
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
                    var c = (border.HasValue && d > radius - (kagit ? 1.8f : 1.2f)) ? b : fill;
                    c.a *= a;
                    px[y * size + x] = c;
                }
            t.SetPixels(px);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            t.name = kagit || Lum(fill) > 0.8f ? "kagit" : "renkli";
            cache[key] = t;
            return t;
        }

        public static void SetBg(GUIStyleState s, Color c, int radius) => s.background = RoundTex(c, radius);

        // ---------- Yazı tipi ----------

        static Font font;
        static bool fontTried;

        /// <summary>
        /// Arayüz yazı tipi: oyunla gelen Inter (Resources/Fonts, SIL OFL lisanslı; Türkçe ve Macarca harflerin hepsi var),
        /// böylece Mac'te ve Windows'ta aynı görünür. Bulunamazsa sistemin modern yazı tipi (Segoe UI, Helvetica Neue / SF).
        /// </summary>
        public static Font UIFont
        {
            get
            {
                if (fontTried) return font;
                fontTried = true;
                font = Resources.Load<Font>("Fonts/Inter-Regular");
                if (font != null) return font;
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

        static Font boldFont;
        static bool boldTried;

        /// <summary>Kalın yazılar için Inter SemiBold (yoksa null: kalınlığı Unity taklit eder).</summary>
        public static Font UIFontBold
        {
            get
            {
                if (boldTried) return boldFont;
                boldTried = true;
                boldFont = Resources.Load<Font>("Fonts/Inter-SemiBold");
                return boldFont;
            }
        }

        /// <summary>
        /// Kalın stili gerçek kalın harfe (Inter SemiBold) çevirir; Unity'nin taklit kalınlığı küçük yazıda bulanık görünür.
        /// Aynı stile iki kez uygulamak zararsız.
        /// </summary>
        public static GUIStyle Semi(GUIStyle s)
        {
            var b = UIFontBold;
            if (s == null || b == null) return s;
            if (s.fontStyle == FontStyle.Bold) { s.font = b; s.fontStyle = FontStyle.Normal; }
            else if (s.fontStyle == FontStyle.BoldAndItalic) { s.font = b; s.fontStyle = FontStyle.Italic; }
            return s;
        }

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }
}
