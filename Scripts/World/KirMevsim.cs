using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Kırın mevsim durumu: taze (ilkbahar), yaz, kurak (yaz sonu), sonbahar, kış. Takvime göre yumuşakça geçer;
    /// sıcak dalgasında kuraklık öne çıkar. City3D kır zemininin dokusunu ve parsellerin renklerini buna göre seçer.
    /// </summary>
    public static class KirDurum
    {
        public const int Taze = 0, Yaz = 1, Kurak = 2, Sonbahar = 3, Kis = 4;
        public static readonly string[] Adlar = { "taze", "yaz", "kurak", "sonbahar", "kış" };

        // (ay, 0 = 1 Ocak … 12 = yıl sonu; durum)
        static readonly (float ay, int durum)[] Anahtar =
        {
            (0.5f, Kis), (2.0f, Kis), (3.4f, Taze), (5.0f, Taze), (6.0f, Yaz), (7.3f, Kurak), (8.5f, Kurak),
            (9.5f, Sonbahar), (10.8f, Sonbahar), (11.6f, Kis), (12.5f, Kis)
        };

        public static float[] Agirliklar()
        {
            var w = new float[5];
            var c = GameClock.I;
            if (c == null) { w[Yaz] = 1f; return w; }
            float ay = c.Month - 1 + (c.DayOfMonth - 1) / 30f;
            if (ay < Anahtar[0].ay) ay += 12f;
            for (int i = 0; i < Anahtar.Length - 1; i++)
            {
                var a = Anahtar[i];
                var b = Anahtar[i + 1];
                if (ay < a.ay || ay > b.ay) continue;
                float t = Mathf.InverseLerp(a.ay, b.ay, ay);
                w[a.durum] += 1f - t;
                w[b.durum] += t;
                break;
            }
            float top = w[0] + w[1] + w[2] + w[3] + w[4];
            if (top < 0.001f) { w[Yaz] = 1f; top = 1f; }
            for (int i = 0; i < 5; i++) w[i] /= top;

            // Sıcak dalgası: yeşilin bir kısmı kurak tona kayar
            if (WeatherSystem.I != null && WeatherSystem.I.Current == WeatherKind.Sicak)
            {
                float kay = 0.35f * (w[Taze] + w[Yaz]);
                w[Taze] *= 0.65f; w[Yaz] *= 0.65f; w[Kurak] += kay;
            }
            return w;
        }

        public static int EnYakin()
        {
            var w = Agirliklar();
            int en = 0;
            for (int i = 1; i < 5; i++) if (w[i] > w[en]) en = i;
            return en;
        }
    }

    /// <summary>
    /// Kır parsellerinin (Blender: Kir_alan_...) malzeme renkleri, mevsime göre. Malzeme adının "_" öncesi (KirCim, KirBugday...)
    /// anahtardır. Tabloda olmayanlar (ahşap, inek, sera) kendi rengini korur; kar onları da biraz örter.
    /// </summary>
    public static class KirPalet
    {
        // taze, yaz, kurak, sonbahar, kış
        static readonly Dictionary<string, string[]> Tablo = new Dictionary<string, string[]>
        {
            { "KirCim",        new[] { "#6fa047", "#6f9a4a", "#a59f58", "#7f8a4a", "#7d7a5e" } },
            { "KirCimKoyu",    new[] { "#5a8a3c", "#5a8540", "#8c8a4c", "#66723e", "#6a6650" } },
            { "KirToprak",     new[] { "#7e5f42", "#7e5f42", "#8a6c4c", "#6f5238", "#75624e" } },
            { "KirToprakKoyu", new[] { "#6a4e36", "#6a4e36", "#775c40", "#5c432e", "#665444" } },
            { "KirEkin",       new[] { "#7fb04a", "#8fb24e", "#c9b45a", "#9c8a52", "#8a7d5c" } },
            { "KirEkinUc",     new[] { "#9cc95a", "#a9c45e", "#dcc56a", "#b39e5e", "#9a8c66" } },
            { "KirBugday",     new[] { "#86b04e", "#cfae55", "#dcb95e", "#b49a5c", "#9a8c66" } },
            { "KirBugdayUc",   new[] { "#9ec65c", "#e0c46c", "#ead07a", "#c6ad6a", "#a89a72" } },
            { "KirKolza",      new[] { "#e6cf3e", "#9caa4a", "#b9a65a", "#8f8a52", "#857c5e" } },
            { "KirKolzaUc",    new[] { "#f2de5a", "#b4bf5a", "#c9b468", "#9a945a", "#908766" } },
            { "KirSaman",      new[] { "#9fae64", "#c9b07a", "#d3b87e", "#a99470", "#9a8b72" } },
            { "KirBalya",      new[] { "#d8bf72", "#d8bf72", "#dcc47a", "#c4aa66", "#b8a278" } },
            { "KirAsma",       new[] { "#6f9a3e", "#5f8a3e", "#7f8a3e", "#b07a34", "#7a6046" } },
            { "KirYaprak",     new[] { "#7cb04e", "#6b9a45", "#84944a", "#c9852e", "#7a6a52" } },
            { "KirYaprakKoyu", new[] { "#649a42", "#557f3a", "#6f7e40", "#a85e2a", "#6a5c48" } },
            { "KirCali",       new[] { "#64994a", "#5f8f4a", "#7a8a48", "#8a7a3e", "#6c6650" } },
            { "KirKamis",      new[] { "#8fb05a", "#8fa25a", "#b0a460", "#a8925a", "#9a8a66" } },
            { "KirCam",        new[] { "#4f7f4a", "#4f7f4a", "#55784a", "#4b7448", "#47704a" } },
            { "KirMeyve",      new[] { "#f4f0ea", "#d9473a", "#d9473a", "#e08a3a", "#7a6a52" } },
            { "KirSu",         new[] { "#5f9fc8", "#5f9fc8", "#6a9fb8", "#557f9a", "#b8cfdc" } },
        };

        static readonly Dictionary<string, Color[]> renk = new Dictionary<string, Color[]>();

        static Color[] Renkler(string etiket)
        {
            if (renk.TryGetValue(etiket, out var r)) return r;
            if (!Tablo.TryGetValue(etiket, out var hex)) { renk[etiket] = null; return null; }
            r = new Color[5];
            for (int i = 0; i < 5; i++) ColorUtility.TryParseHtmlString(hex[i], out r[i]);
            renk[etiket] = r;
            return r;
        }

        /// <summary>Mevsim ağırlıklarına göre karışık renk.</summary>
        public static Color Renk(string etiket, Color asil, float[] w)
        {
            var r = Renkler(etiket);
            if (r == null)
            {
                // Çiçekler kışın solar, sonbaharda kahveye döner; diğerleri kendi rengi
                if (etiket == "KirCicek") return Color.Lerp(asil, new Color(0.54f, 0.48f, 0.37f), w[KirDurum.Kis] * 0.75f + w[KirDurum.Sonbahar] * 0.35f);
                return asil;
            }
            Color c = Color.black;
            for (int i = 0; i < 5; i++) c += r[i] * w[i];
            return c;
        }

        /// <summary>Zemin malzemesi mi (ıslakken daha çok koyulaşır).</summary>
        public static float Zemin(string etiket)
        {
            switch (etiket)
            {
                case "KirCim": case "KirCimKoyu": case "KirToprak": case "KirToprakKoyu": case "KirEkin": case "KirEkinUc":
                case "KirBugday": case "KirBugdayUc": case "KirKolza": case "KirKolzaUc": case "KirSaman": case "KirBalya":
                    return 1f;
                default: return 0f;
            }
        }

        /// <summary>Kar örtüsünün bu malzemeyi ne kadar beyazlattığı.</summary>
        public static float KarTutar(string etiket)
        {
            if (Zemin(etiket) > 0.5f) return etiket == "KirBalya" ? 0.8f : 1f;
            switch (etiket)
            {
                case "KirYaprak": case "KirYaprakKoyu": case "KirCali": case "KirAsma": case "KirCam": case "KirKamis": case "KirMeyve":
                    return 0.55f;
                case "KirCicek": return 0.9f;
                case "KirSu": return 0.75f;
                case "KirTas": return 0.6f;
                case "KirSera": case "KirSeraKenar": return 0.4f;
                case "KirAhsap": return 0.35f;
                default: return 0.1f;
            }
        }
    }
}
