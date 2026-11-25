using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Haritada kısa süreli uçan yazılar: teslimatta "+350 Ft" (siparişten kalan kâr), iptalde kırmızı "İptal".
    /// Oyun kodu buraya haber verir (OrderManager), GameUI UI Toolkit'te çizer (Arayuz/UiHaritaEtiketleri.cs). Ayarlar > Arayüz'den
    /// kapatılabilir.
    /// </summary>
    public static class MapFx
    {
        public struct Pop
        {
            public Vector2Int tile;
            public string text;
            public Color color;
            public float born;   // gerçek zaman (oyun hızından bağımsız)
        }

        public const float Life = 1.6f;
        const int MaxPops = 40;
        public static readonly List<Pop> Pops = new List<Pop>();

        public static readonly Color Good = new Color(0.45f, 0.95f, 0.55f);
        public static readonly Color Bad = new Color(1f, 0.42f, 0.38f);
        /// <summary>Büyük kâr, seri, seviye atlama (Kutlama, KuryeKariyer).</summary>
        public static readonly Color Altin = new Color(1f, 0.82f, 0.25f);

        public static void Delivered(Order o, float late = 0f)
        {
            if (o == null) return;
            int p = o.Profit;
            bool buyuk = Kutlama.Teslim(o, late);   // seri sayacı; ortalamanın 3 katı kâr altın renkte uçar
            Add(o.customerTile, p >= 0 ? "+" + GameData.Ft(p) : GameData.Ft(p), p < 0 ? Bad : buyuk ? Altin : Good);
            GameSounds.Play(Sfx.Teslimat);
        }

        public static void Cancelled(Order o)
        {
            if (o == null) return;
            Add(o.customerTile, "İptal", Bad);
            Kutlama.Iptal(o);   // seriyi bozar
        }

        /// <summary>Özel uçan yazı (seri, seviye atlama...).</summary>
        public static void Ozel(Vector2Int tile, string text, Color color) => Add(tile, text, color);

        static void Add(Vector2Int tile, string text, Color color)
        {
            if (Pops.Count >= MaxPops) Pops.RemoveAt(0);
            Pops.Add(new Pop { tile = tile, text = text, color = color, born = Time.unscaledTime });
        }
    }

    public partial class GameUI
    {
        const string PopsKey = "KuryeTycoon.UcanYazilar";
        static int popsCache = -1;

        static bool ShowPopsSetting
        {
            get
            {
                if (popsCache < 0) popsCache = PlayerPrefs.GetInt(PopsKey, 1);
                return popsCache == 1;
            }
            set
            {
                popsCache = value ? 1 : 0;
                PlayerPrefs.SetInt(PopsKey, popsCache);
                PlayerPrefs.Save();
            }
        }

        // Uçan yazıların çizimi: Arayuz/UiHaritaEtiketleri.cs (UcanYazilar)
    }
}
