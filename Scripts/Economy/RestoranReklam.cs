using UnityEngine;

namespace KuryeTycoon
{
    [System.Serializable]
    public class RestoranReklamSave
    {
        public int seviye;
        public long toplamGelir;
    }

    /// <summary>
    /// Restoranlardan reklam geliri (İşletme > Dükkanlar): sözleşmeli dükkanlar uygulamada öne çıkmak (sponsorlu listeleme)
    /// için para öder. Gerçekte platformların önemli bir gelir kalemi.
    ///  • Seviye: Yok / Az / Orta / Yoğun. Seviye arttıkça daha çok dükkan reklam alır ve her biri daha çok öder
    ///    (günlük 3,75 € × seviye çarpanı × dükkanın büyüklüğü). Her gece yeniden dağıtılır, gelir o gece yazılır.
    ///  • Reklam alan dükkan daha çok sipariş alır (seviye başına +%12 talep).
    ///  • Bedeli müşteri memnuniyeti: uygulama reklamla dolunca her teslimatın puanı biraz düşer
    ///    (Az −0,03 · Orta −0,07 · Yoğun −0,12 yıldız). Fazla reklam satarsan itibar düşer, az satarsan para masada kalır.
    /// </summary>
    public class RestoranReklam : MonoBehaviour
    {
        public static RestoranReklam I { get; private set; }

        public static readonly string[] SeviyeAdlari = { "Yok", "Az", "Orta", "Yoğun" };
        static readonly float[] GelirCarpani = { 0f, 1f, 2.2f, 3.6f };
        static readonly float[] Ceza = { 0f, 0.03f, 0.07f, 0.12f };
        static readonly float[] Oran = { 0f, 0.3f, 0.5f, 0.7f };
        public const long TabanUcret = 1500;   // dükkan başı günlük (Ft), seviye ve büyüklükle çarpılır

        public int seviye;
        public long toplamGelir, dunGelir;
        public int dunSponsor;

        void Awake() { I = this; }

        public void Init()
        {
            seviye = 0;
            toplamGelir = dunGelir = 0;
            dunSponsor = 0;
            GameClock.I.OnNewDay += YeniGun;
        }

        /// <summary>Her teslimatın puanından düşülen (uygulama reklamla dolu).</summary>
        public static float PuanCezasi => I != null ? Ceza[Mathf.Clamp(I.seviye, 0, 3)] : 0f;

        /// <summary>Reklam alan dükkanın talep çarpanı.</summary>
        public static float TalepCarpani(Shop s) => s != null && s.sponsorlu && I != null ? 1f + 0.12f * I.seviye : 1f;

        public static float CezaOf(int lv) => Ceza[Mathf.Clamp(lv, 0, 3)];

        static float Buyukluk(Shop s) => 1f + Mathf.Min(2f, (s.monthOrders + s.lastMonthOrders) / 300f);

        /// <summary>Bu seviyede yaklaşık günlük gelir (tahmin: dükkanların reklam alma oranıyla).</summary>
        public long TahminiGelir(int lv)
        {
            if (CityMap.I == null || lv <= 0) return 0;
            float t = 0f;
            foreach (var s in CityMap.I.Shops)
                if (s.contracted && !s.own && !s.kargo) t += TabanUcret * GelirCarpani[lv] * Buyukluk(s) * Oran[lv];
            return (long)t;
        }

        public void SeviyeAyarla(int lv)
        {
            lv = Mathf.Clamp(lv, 0, 3);
            if (lv == seviye) return;
            seviye = lv;
            Dagit();
            EventManager.I.Notify(lv == 0 ? "Restoran reklamları kapatıldı: uygulamada sponsorlu dükkan yok."
                                          : $"Restoran reklamları: {SeviyeAdlari[lv]}. Yeni reklamlar bu gece başlar.");
        }

        /// <summary>Bu gece hangi dükkanların reklam aldığını belirler (seviye 0'da hepsi kalkar).</summary>
        int Dagit()
        {
            int n = 0;
            if (CityMap.I == null) return 0;
            foreach (var s in CityMap.I.Shops)
            {
                bool al = seviye > 0 && s.contracted && !s.own && !s.kargo && Random.value < Oran[seviye] * Mathf.Min(1.3f, Buyukluk(s) * 0.8f);
                s.sponsorlu = al;
                if (al) n++;
            }
            return n;
        }

        void YeniGun()
        {
            dunGelir = 0;
            dunSponsor = 0;
            int n = Dagit();
            if (seviye <= 0 || n == 0) return;
            long gelir = 0;
            foreach (var s in CityMap.I.Shops)
                if (s.sponsorlu) gelir += (long)(TabanUcret * GelirCarpani[seviye] * Buyukluk(s));
            gelir = (gelir / 100) * 100;
            if (gelir <= 0) return;
            EconomyManager.I.AddIncome(gelir, $"Restoran reklamları ({n} dükkan)");
            dunGelir = gelir;
            dunSponsor = n;
            toplamGelir += gelir;
        }

        public RestoranReklamSave Kaydet() => new RestoranReklamSave { seviye = seviye, toplamGelir = toplamGelir };

        public void Yukle(RestoranReklamSave s)
        {
            if (s == null) return;
            seviye = Mathf.Clamp(s.seviye, 0, 3);
            toplamGelir = s.toplamGelir;
            Dagit();
        }
    }
}
