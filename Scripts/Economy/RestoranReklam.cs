using System.Collections.Generic;
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
    ///  • Seviye: Yok / Az / Orta / Yoğun. Seviye arttıkça daha çok dükkan reklam alır (%30 / %50 / %70) ve satışının daha
    ///    büyük kısmını öder (%2,5 / %4 / %6; gerçekte de reklamlar satışa göre faturalanır). Reklamlar her gece dağıtılır,
    ///    reklamlı dükkanın o günkü satışı üzerinden ertesi gece gelir yazılır.
    ///  • Reklam alan dükkan daha çok sipariş alır (seviye başına +%12 talep).
    ///  • Bedeli müşteri memnuniyeti: uygulama reklamla dolunca her teslimatın puanı biraz düşer
    ///    (Az −0,03 · Orta −0,07 · Yoğun −0,12 yıldız). Fazla reklam satarsan itibar düşer, az satarsan para masada kalır.
    /// </summary>
    public class RestoranReklam : MonoBehaviour
    {
        public static RestoranReklam I { get; private set; }

        public static readonly string[] SeviyeAdlari = { "Yok", "Az", "Orta", "Yoğun" };
        static readonly float[] SatisPayi = { 0f, 0.025f, 0.04f, 0.06f };
        static readonly float[] Ceza = { 0f, 0.03f, 0.07f, 0.12f };
        static readonly float[] Oran = { 0f, 0.3f, 0.5f, 0.7f };
        readonly Dictionary<Shop, long> satis = new Dictionary<Shop, long>();
        readonly Dictionary<Shop, long> dunSatis = new Dictionary<Shop, long>();

        public int seviye;
        public long toplamGelir, dunGelir;
        public int dunSponsor;

        void Awake() { I = this; }

        public void Init()
        {
            seviye = 0;
            toplamGelir = dunGelir = 0;
            dunSponsor = 0;
            satis.Clear(); dunSatis.Clear();
            GameClock.I.OnNewDay += YeniGun;
        }

        /// <summary>Her teslimatın puanından düşülen (uygulama reklamla dolu).</summary>
        public static float PuanCezasi => I != null ? Ceza[Mathf.Clamp(I.seviye, 0, 3)] : 0f;

        /// <summary>Reklam alan dükkanın talep çarpanı.</summary>
        public static float TalepCarpani(Shop s) => s != null && s.sponsorlu && I != null ? 1f + 0.12f * I.seviye : 1f;

        public static float CezaOf(int lv) => Ceza[Mathf.Clamp(lv, 0, 3)];

        static float Buyukluk(Shop s) => 1f + Mathf.Min(2f, (s.monthOrders + s.lastMonthOrders) / 300f);

        /// <summary>Teslim edilen siparişin sepeti dükkanın bugünkü satışına yazılır (reklam bedeli bunun yüzdesi).</summary>
        public static void SatisKaydet(Order o)
        {
            if (I == null || o == null || o.shop == null || o.shop.own || o.shop.kargo) return;
            I.satis.TryGetValue(o.shop, out long v);
            I.satis[o.shop] = v + o.basket;
        }

        /// <summary>Bu seviyede yaklaşık günlük gelir (dünkü satışlar × reklam alma oranı × satış payı).</summary>
        public long TahminiGelir(int lv)
        {
            if (CityMap.I == null || lv <= 0) return 0;
            float t = 0f;
            foreach (var s in CityMap.I.Shops)
            {
                if (!s.contracted || s.own || s.kargo) continue;
                dunSatis.TryGetValue(s, out long v);
                if (v == 0) satis.TryGetValue(s, out v);
                t += v * SatisPayi[lv] * Oran[lv];
            }
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
            // Önce bugün reklamı olan dükkanlardan bugünkü satışlarının payı alınır, sonra yarının reklamları dağıtılır
            long gelir = 0; int n = 0;
            if (seviye > 0 && CityMap.I != null)
                foreach (var s in CityMap.I.Shops)
                {
                    if (!s.sponsorlu) continue;
                    satis.TryGetValue(s, out long v);
                    gelir += (long)(v * SatisPayi[seviye]);
                    n++;
                }
            dunSatis.Clear();
            foreach (var kv in satis) dunSatis[kv.Key] = kv.Value;
            satis.Clear();
            Dagit();
            gelir = (gelir / 100) * 100;
            if (gelir <= 0) return;
            EconomyManager.I.AddIncome(gelir, $"Restoran reklamları ({n} dükkan)");
            dunGelir = gelir;
            dunSponsor = n;
            toplamGelir += gelir;
        }

        public static float SatisPayiOf(int lv) => SatisPayi[Mathf.Clamp(lv, 0, 3)];

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
