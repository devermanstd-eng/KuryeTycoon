using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Kurumsal (catering) siparişler: ofisler, okullar, hastaneler sabahtan öğle yemeği siparişi verir.
    ///  • Hafta içi sabah 8–10 arası teklif gelir (şirket büyüdükçe daha sık, en fazla 2). Teklif 10:30'a kadar geçerli.
    ///  • Kabul edilirse teslim saatinden ~50 dk önce sipariş açılır; sadece "Kurumsal ve catering teslimatı" eğitimi almış,
    ///    motorlu ya da arabalı kurye götürebilir (büyük ve ağır).
    ///  • Zamanında teslim: yüksek teslimat ücreti + prim, itibar ve dükkan memnuniyeti. Geç: prim yok. Kaçarsa: itibar ve memnuniyet düşer.
    /// Kayda yazılmaz: yüklemede açık teklifler düşer.
    /// </summary>
    public class KurumsalSiparisler : MonoBehaviour
    {
        public static KurumsalSiparisler I { get; private set; }

        public class Teklif
        {
            public int no;
            public Shop dukkan;
            public Vector2Int ofis;
            public string ofisAdi;
            public int kisi, sepet, ucret, odul;
            public float kg;
            public float teslimAt, sonKabul;      // oyun dakikası
            public bool kabul, siparisVerildi, bitti, basarili;
            public Order siparis;
        }

        public readonly List<Teklif> Teklifler = new List<Teklif>();
        public int ToplamTeslim { get; private set; }
        public int ToplamKacan { get; private set; }
        int sayac, sonUretimGunu = -1;

        static readonly string[] OfisAdlari =
        {
            "Bir yazılım şirketi", "Belediye binası", "Banka şubesi", "Bir okulun öğretmenler odası", "Hastane personeli",
            "Avukatlık bürosu", "Reklam ajansı", "Üniversite bölümü", "Lojistik firması", "Mimarlık ofisi",
        };

        void Awake() { I = this; }

        public void Init()
        {
            Teklifler.Clear();
            sonUretimGunu = -1;
            GameClock.I.OnNewDay += () => Teklifler.RemoveAll(t => t.bitti || (!t.kabul && !t.siparisVerildi));
        }

        static string Ft(long v) => GameData.Ft(v);

        public int AcikTeklifSayisi
        {
            get { int n = 0; foreach (var t in Teklifler) if (!t.bitti && !t.kabul) n++; return n; }
        }

        void Update()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || GameClock.I == null || OrderManager.I == null) return;
            var clk = GameClock.I;
            float now = clk.TotalMinutes;
            if (clk.Hour >= 8 && clk.Hour < 10 && !clk.IsWeekend && sonUretimGunu != clk.Day)
            {
                sonUretimGunu = clk.Day;
                Uret();
            }
            foreach (var t in Teklifler)
            {
                if (t.bitti) continue;
                if (!t.kabul)
                {
                    if (now > t.sonKabul) t.bitti = true;
                    continue;
                }
                if (!t.siparisVerildi && now >= t.teslimAt - 50f)
                {
                    t.siparisVerildi = true;
                    t.siparis = OrderManager.I.OzelSiparis(t.dukkan, t.ofis, t.sepet, t.kg, t.ucret, t.teslimAt - 20f, t.teslimAt + 5f,
                        $"{t.ofisAdi}: {t.kisi} kişilik öğle yemeği. Resepsiyona, saatinde.");
                    EventManager.I.Notify($"Kurumsal sipariş hazırlanıyor: {t.ofisAdi}, {t.kisi} kişi, {t.dukkan.Name} · teslim {Saat(t.teslimAt)}. " +
                                          (KuryeEgitim.Sayi(KuryeEgitim.Tur.Catering, true) == 0 ? "Dikkat: çevrimiçi catering eğitimli kurye yok!" : ""));
                }
            }
        }

        static string Saat(float dk)
        {
            float g = dk % 1440f;
            return $"{(int)(g / 60f):00}:{(int)(g % 60f):00}";
        }

        void Uret()
        {
            var clk = GameClock.I;
            if (clk.CompanyDay < 8 || AcikTeklifSayisi >= 2) return;
            var map = CityMap.I;
            var yemek = new List<Shop>();
            foreach (var s in map.Shops)
            {
                if (!s.contracted || s.own || s.def.isMarket || !map.IsUnlocked(s.tile)) continue;
                float ogle = (clk.Day - 1) * 1440f + 12f * 60f;
                if (!ShopHours.IsOpenAt(s.def, ogle)) continue;
                yemek.Add(s);
            }
            if (yemek.Count == 0) return;
            float olasilik = Mathf.Clamp(0.2f + 0.02f * CourierManager.I.Couriers.Count, 0.2f, 0.75f);
            if (Random.value > olasilik) return;
            int n = Random.value < 0.3f ? 2 : 1;
            float now = clk.TotalMinutes;
            float gunBasi = (clk.Day - 1) * 1440f;
            for (int i = 0; i < n && AcikTeklifSayisi < 2; i++)
            {
                var dukkan = yemek[Random.Range(0, yemek.Count)];
                int kisi = Random.Range(8, 36);
                float teslim = gunBasi + (Random.value < 0.6f ? 12f : 13f) * 60f + (Random.value < 0.5f ? 0f : 30f);
                int sepet = kisi * Random.Range(2800, 4200);
                int ucret = (int)Denge.Olcekle(4000 + kisi * 180);
                var t = new Teklif
                {
                    no = ++sayac,
                    dukkan = dukkan,
                    ofis = map.RandomCustomerTileNear(dukkan.tile, 14),
                    ofisAdi = OfisAdlari[Random.Range(0, OfisAdlari.Length)],
                    kisi = kisi,
                    sepet = sepet,
                    kg = Mathf.Round(Mathf.Clamp(kisi * 0.45f, 4f, 18f) * 10f) / 10f,
                    ucret = ucret,
                    odul = ucret / 3,
                    teslimAt = teslim,
                    sonKabul = Mathf.Min(teslim - 90f, gunBasi + 10.5f * 60f),
                };
                if (t.sonKabul <= now) continue;
                Teklifler.Add(t);
                EventManager.I.Notify($"Kurumsal sipariş teklifi: {t.ofisAdi}, {kisi} kişilik öğle yemeği ({dukkan.Name}), teslim {Saat(teslim)}. " +
                                      $"Teslimat ücreti {Ft(ucret)} + zamanında prim {Ft(t.odul)}. Siparişler penceresinden kabul et.");
            }
        }

        public void KabulEt(Teklif t)
        {
            if (t == null || t.bitti || t.kabul) return;
            t.kabul = true;
            int egitimli = KuryeEgitim.Sayi(KuryeEgitim.Tur.Catering);
            EventManager.I.Notify($"Kurumsal sipariş kabul edildi: {t.ofisAdi}, teslim {Saat(t.teslimAt)}." +
                                  (egitimli == 0 ? " Catering eğitimli (motorlu/arabalı) kuryen yok: Kuryeler > kart > Eğitim." : ""));
        }

        public void Reddet(Teklif t)
        {
            if (t == null || t.bitti) return;
            t.bitti = true;
        }

        Teklif Bul(Order o)
        {
            foreach (var t in Teklifler) if (t.siparis == o) return t;
            return null;
        }

        /// <summary>OrderManager: kurumsal sipariş teslim edildi.</summary>
        public void Teslim(Order o, float gecikme)
        {
            var t = Bul(o);
            if (t == null) return;
            t.bitti = true;
            ToplamTeslim++;
            if (gecikme <= 0f)
            {
                t.basarili = true;
                EconomyManager.I.AddIncome(t.odul, $"Kurumsal sipariş primi ({t.ofisAdi})");
                Itibar.Olay(0.03f);
                RivalCompany.AddSatisfaction(t.dukkan, 6f);
                EventManager.I.Notify($"Kurumsal sipariş zamanında teslim edildi: {t.ofisAdi} memnun. Prim {Ft(t.odul)}.");
            }
            else EventManager.I.Notify($"Kurumsal sipariş {gecikme:0} dk geç teslim edildi: {t.ofisAdi} primi ödemedi.");
        }

        /// <summary>OrderManager: kurumsal sipariş teslim saatini geçti, kaçtı.</summary>
        public void Kacti(Order o)
        {
            var t = Bul(o);
            if (t == null) return;
            t.bitti = true;
            ToplamKacan++;
            Itibar.Olay(-0.08f);
            RivalCompany.AddSatisfaction(t.dukkan, -12f);
            EventManager.I.Notify($"Kurumsal sipariş teslim edilemedi: {t.ofisAdi} {t.kisi} kişiyle aç kaldı. İtibar ve {t.dukkan.Name} memnuniyeti düştü.");
        }

        public static string SaatYazi(float dk) => Saat(dk);
    }
}
