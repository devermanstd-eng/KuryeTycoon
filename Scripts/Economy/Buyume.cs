using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Büyüme hissi (incremental): şirket büyüdükçe rakamlar katlanarak artsın.
    ///  • Fiyatlar artık gelire göre şişmiyor (Denge.FiyatCarpani = 1): her geliştirmenin kendi seviye fiyatı var.
    ///  • Komisyon geliri çarpanı: her büyük kilometre taşı kalıcı +%3, marka (prestij) bonusları üstüne çarpılır.
    ///  • Üst barda günlük ortalama kâr ve çarpan görünür.
    /// </summary>
    public static class Buyume
    {
        public const float TasBasi = 0.03f;

        /// <summary>Ulaşılan büyük (kartlı) kilometre taşı sayısı.</summary>
        public static int KartTasSayisi
        {
            get
            {
                var m = Milestones.I;
                if (m == null) return 0;
                int n = 0;
                foreach (var d in m.Defs) if (d.card && m.IsReached(d.id)) n++;
                return n;
            }
        }

        public static float TasCarpani => 1f + TasBasi * KartTasSayisi;

        /// <summary>Siparişin komisyon gelirine uygulanan toplam çarpan.</summary>
        public static float KomisyonCarpani => TasCarpani * Marka.GelirCarpani;

        /// <summary>Son n günün ortalama net kârı (Ft).</summary>
        public static long OrtalamaNet(int n = 7)
        {
            var h = StatsHistory.I != null ? StatsHistory.I.Days : null;
            if (h == null || h.Count == 0) return 0;
            double t = 0; int k = 0;
            for (int i = Mathf.Max(0, h.Count - n); i < h.Count; i++) { t += h[i].net; k++; }
            return (long)(t / k);
        }

        /// <summary>Son 3 günün ortalaması önceki 4 günün ortalamasından ne kadar farklı (oran; veri azsa 0).</summary>
        public static float Egilim
        {
            get
            {
                var h = StatsHistory.I != null ? StatsHistory.I.Days : null;
                if (h == null || h.Count < 5) return 0f;
                double a = 0, b = 0; int na = 0, nb = 0;
                for (int i = h.Count - 1; i >= 0 && i >= h.Count - 7; i--)
                {
                    if (i >= h.Count - 3) { a += h[i].net; na++; } else { b += h[i].net; nb++; }
                }
                if (na == 0 || nb == 0) return 0f;
                double ortB = b / nb, ortA = a / na;
                if (System.Math.Abs(ortB) < 1000) return ortA > ortB ? 1f : ortA < ortB ? -1f : 0f;
                return Mathf.Clamp((float)((ortA - ortB) / System.Math.Abs(ortB)), -1f, 3f);
            }
        }

        public static string CarpanAciklama =>
            $"Komisyon geliri ×{KomisyonCarpani:0.00}: kilometre taşları ×{TasCarpani:0.00} ({KartTasSayisi} taş, her biri +%{TasBasi * 100f:0})" +
            (Marka.GelirCarpani > 1.001f ? $", marka ×{Marka.GelirCarpani:0.00}" : "") + ".";
    }
}
