using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Bölge sadakati: fiyat savaşının kalıcı izi.
    ///  • Fiyat savaşını görmezden gelirsen savaş bitince o bölgenin müşterilerinin bir kısmı rakibe alışmış olur:
    ///    o bölgedeki siparişlerin kalıcı olarak %12 (saldırıdayken %16) düşer. Karşı kampanya yaparsan kayıp olmaz.
    ///  • Kayıp kendiliğinden ancak iyi hizmetle yavaşça geri gelir: itibar 4,2+ iken günde 0,6 puan, 3,8+ iken 0,3 puan,
    ///    daha düşükte neredeyse hiç.
    ///  • "Geri kazanma kampanyası" (Dükkanlar > Rakipler): para karşılığında kaybın yarısını hemen geri alırsın (7 günde bir).
    /// </summary>
    public partial class RivalCompany
    {
        /// <summary>Bölge başına senin müşteri sadakatin (1 = tam). İndeks: bölge (1–5).</summary>
        public float[] bolgeSadakat = { 1f, 1f, 1f, 1f, 1f, 1f };
        /// <summary>Şu anki fiyat savaşı görmezden gelindi mi (savaş bitince kalıcı kayıp).</summary>
        public bool savasGormezden;
        public int sonGeriKazanmaGunu = -999;
        public const int GeriKazanmaBekleme = 7;

        public float BolgeSadakati(int bolge) => bolge >= 0 && bolge < bolgeSadakat.Length ? bolgeSadakat[bolge] : 1f;

        /// <summary>Fiyat savaşı bitti: görmezden gelindiyse bölgede kalıcı kayıp.</summary>
        void SavasBittiSadakat(int bolge, bool saldiri)
        {
            if (!savasGormezden || bolge < 1 || bolge >= bolgeSadakat.Length) { savasGormezden = false; return; }
            savasGormezden = false;
            float kayip = saldiri ? 0.16f : 0.12f;
            float once = bolgeSadakat[bolge];
            bolgeSadakat[bolge] = Mathf.Max(0.6f, once - kayip);
            Telemetry.Add("Rakip: fiyat savaşı kalıcı bölge kaybı");
            EventManager.I.Push(new GameEvent($"{CityMap.DistrictNames[bolge]}: müşteriler rakibe alıştı",
                $"Fiyat savaşına karşılık vermediğin için {CityMap.DistrictNames[bolge]} bölgesindeki müşterilerin bir kısmı rakibin uygulamasına alıştı. " +
                $"Bu bölgedeki siparişlerin kalıcı olarak %{(once - bolgeSadakat[bolge]) * 100f:0} daha az (bölge sadakati %{bolgeSadakat[bolge] * 100f:0}).\n\n" +
                "İyi hizmetle (itibar 4,2 ve üstü) müşteriler yavaşça geri döner. Dükkanlar > Rakipler'deki geri kazanma kampanyası kaybın yarısını hemen geri alır.")
                .Add("Tamam", null));
        }

        /// <summary>Her gece: iyi hizmet kaybı yavaşça geri getirir.</summary>
        void SadakatGunluk()
        {
            float itibar = CompanyProfile.Reputation;
            float hiz = itibar >= 4.2f ? 0.006f : itibar >= 3.8f ? 0.003f : 0.0005f;
            for (int b = 1; b < bolgeSadakat.Length; b++)
                if (bolgeSadakat[b] < 1f) bolgeSadakat[b] = Mathf.Min(1f, bolgeSadakat[b] + hiz);
        }

        /// <summary>Geri kazanma kampanyasının bedeli (son günlerin gelirine göre).</summary>
        public long GeriKazanmaBedeli => Round1000(Mathf.Max(300000f, CrisisManager.AvgDailyIncome() * 0.6f));

        public bool GeriKazanabilirMi(int bolge) =>
            bolge >= 1 && bolge < bolgeSadakat.Length && bolgeSadakat[bolge] < 0.995f && Day - sonGeriKazanmaGunu >= GeriKazanmaBekleme;

        /// <summary>Kampanya: kaybın yarısı hemen geri gelir.</summary>
        public void GeriKazan(int bolge)
        {
            if (!GeriKazanabilirMi(bolge)) return;
            long bedel = GeriKazanmaBedeli;
            if (!EconomyManager.I.TrySpend(bedel, $"Geri kazanma kampanyası ({CityMap.DistrictNames[bolge]})", ExpenseCategory.Reklam)) return;
            float once = bolgeSadakat[bolge];
            bolgeSadakat[bolge] = Mathf.Min(1f, once + (1f - once) * 0.5f);
            sonGeriKazanmaGunu = Day;
            EventManager.I.Notify($"{CityMap.DistrictNames[bolge]} bölgesinde geri kazanma kampanyası: indirim kuponları ve reklam. " +
                                  $"Bölge sadakati %{once * 100f:0} → %{bolgeSadakat[bolge] * 100f:0}.");
            Telemetry.Add("Rakip: geri kazanma kampanyası");
        }

        /// <summary>Dükkan bir rakibe gidebilir mi: öncelikli hazırlık anlaşması olan dükkan daha sadık.</summary>
        static float SadakatEki(Shop s) => (s != null && s.oncelikli ? Mutfak.OncelikSadakat : 0f) + DukkanSadakati.TutmaEki(s);

        void SadakatKaydet(RivalSave s)
        {
            s.bolgeSadakat = (float[])bolgeSadakat.Clone();
            s.savasGormezden = savasGormezden;
            s.sonGeriKazanmaGunu = sonGeriKazanmaGunu;
        }

        void SadakatYukle(RivalSave s)
        {
            for (int i = 0; i < bolgeSadakat.Length; i++) bolgeSadakat[i] = 1f;
            if (s.bolgeSadakat != null)
                for (int i = 0; i < bolgeSadakat.Length && i < s.bolgeSadakat.Length; i++)
                    bolgeSadakat[i] = s.bolgeSadakat[i] > 0f ? Mathf.Clamp(s.bolgeSadakat[i], 0.5f, 1f) : 1f;
            savasGormezden = s.savasGormezden;
            sonGeriKazanmaGunu = s.sonGeriKazanmaGunu;
        }
    }
}
