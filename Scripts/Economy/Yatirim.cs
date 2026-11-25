using UnityEngine;

namespace KuryeTycoon
{
    [System.Serializable]
    public class YatirimSave
    {
        public float pay = 1f;
        public int tamamlanan, baski, ret;
        public bool hedefAktif;
        public int hedefBitis, sonTeklifGunu = -999;
        public float kosul = 1f;
        public long toplamYatirim;
        public long toplamTemettu;          // 24.11: yatırımcılara ödenen temettü toplamı (eski kayıtlarda 0)
        public int teklifTur = -1;
        public int hedefIlce, hedefTeslimat, hedefAbone;
    }

    /// <summary>
    /// Yatırım turları (Şirket > Finans): gerçek teslimat girişimleri gibi tohum yatırım, sonra A, B ve C serisi.
    ///  • Koşullar tutunca yatırımcı teklif kartı gelir: X para karşılığında şirketin %Y'si ve süreli büyüme hedefleri
    ///    (açık bölge, günlük teslimat, abone). Kabul edersen para kasaya girer, kurucu payın küçülür.
    ///  • Hedefleri süresinde tutturursan sonraki tur daha iyi koşullarla gelir (para ×1,25, pay daha az).
    ///    Tutturamazsan yönetim kurulu baskısı: masrafları kısmak ya da payından vermek zorunda kalırsın; sonraki tur ucuzlar.
    ///  • Payın şirket satışını (Marka) etkiler: satıştan alınan marka puanı, şirket değerinin sana düşen kısmından hesaplanır.
    /// </summary>
    public class Yatirim : MonoBehaviour
    {
        public static Yatirim I { get; private set; }

        public class Tur
        {
            public string ad, yatirimci;
            public long tutar;     // Ft
            public float pay;      // yatırımcıya verilen oran
            public int sure;       // hedef süresi (gün)
            public int ilce, teslimat, abone;
            public int enErkenGun, enAzTeslimat;   // teklifin gelmesi için
        }

        // 24.11 (u30, 5 şehir × 120 gün): A serisi ~29. günde 20–25 bin € getiriyor, 60 ve 80 hesaplık paketler hemen
        // alınıyordu; 120. günde kasada ~200 bin € birikiyordu. Turlar daha geç ve daha küçük; yatırımcı her gün temettü alır
        // (Temettu). Tohum 10. → 14. gün; A 8 → 5 M, en erken 35. gün; B 32 → 20 M, 60. gün; C 100 → 60 M, 85. gün.
        public static readonly Tur[] Turlar =
        {
            new Tur { ad = "Tohum yatırım", yatirimci = "Swan Angels", tutar = 1600000, pay = 0.12f, sure = 60,
                      ilce = 2, teslimat = 150, abone = 0, enErkenGun = 14, enAzTeslimat = 80 },
            new Tur { ad = "A serisi", yatirimci = "Northwind Ventures", tutar = 5000000, pay = 0.18f, sure = 90,
                      ilce = 3, teslimat = 500, abone = 300, enErkenGun = 35, enAzTeslimat = 300 },
            new Tur { ad = "B serisi", yatirimci = "Summit Capital", tutar = 20000000, pay = 0.20f, sure = 120,
                      ilce = 4, teslimat = 1200, abone = 1500, enErkenGun = 60, enAzTeslimat = 700 },
            new Tur { ad = "C serisi", yatirimci = "Euro Growth Partners", tutar = 60000000, pay = 0.20f, sure = 150,
                      ilce = 5, teslimat = 2500, abone = 5000, enErkenGun = 85, enAzTeslimat = 1100 },
        };

        /// <summary>Yatırımcılar her gün dünkü faaliyet kârından (tek seferlik alımlar hariç) paylarının bu oranını temettü alır.</summary>
        public const float TemettuOrani = 0.5f;
        /// <summary>Bugün yatırımcılara ödenen temettü (Ft).</summary>
        public long BugunTemettu { get; private set; }
        public long ToplamTemettu { get; private set; }

        public float pay = 1f;            // kurucu (oyuncu) payı
        public int tamamlanan;            // kabul edilen tur sayısı
        public bool hedefAktif;
        public int hedefBitis;            // GameClock.Day
        public int baski, ret;
        public int sonTeklifGunu = -999;
        public float kosul = 1f;          // sonraki turun koşulları: 1 normal, 1,25 iyi, 0,7 kötü
        public long toplamYatirim;
        int teklifTur = -1;               // açık (cevap bekleyen) teklif
        // Teklif anında sabitlenen hedefler: turun tabanı ya da şirketin o anki hâlinin üstü (hazır tutan hedef olmasın)
        public int hedefIlce, hedefTeslimat, hedefAbone;

        void Awake() { I = this; }

        public void Init()
        {
            pay = 1f; tamamlanan = 0; hedefAktif = false; hedefBitis = 0; baski = ret = 0;
            sonTeklifGunu = -999; kosul = 1f; toplamYatirim = 0; teklifTur = -1;
            ToplamTemettu = 0; BugunTemettu = 0;
            hedefIlce = hedefTeslimat = hedefAbone = 0;
            GameClock.I.OnNewDay += YeniGun;
        }

        public Tur AktifTur => tamamlanan > 0 && tamamlanan <= Turlar.Length ? Turlar[tamamlanan - 1] : null;
        public Tur SiradakiTur => tamamlanan < Turlar.Length ? Turlar[tamamlanan] : null;

        public long TeklifTutari(Tur t) => (long)(Mathf.Round(t.tutar * kosul / 100000f) * 100000f);
        public float TeklifPayi(Tur t) => Mathf.Clamp(t.pay / Mathf.Sqrt(kosul), 0.05f, 0.35f);

        static int Gun => GameClock.I != null ? GameClock.I.Day : 0;

        static float OrtTeslimat
        {
            get
            {
                var h = StatsHistory.I != null ? StatsHistory.I.Days : null;
                if (h == null || h.Count == 0) return 0f;
                float t = 0f; int n = 0;
                for (int i = Mathf.Max(0, h.Count - 7); i < h.Count; i++) { t += h[i].deliveries; n++; }
                return t / n;
            }
        }

        static int AcikIlce => CityMap.I != null ? CityMap.I.UnlockLevel : 1;
        static int AboneSayisi => Abonelik.I != null ? Abonelik.I.Abone : 0;

        /// <summary>Teklif anındaki hedefler: turun tabanı, şirket zaten oradaysa daha yukarısı (bölge +1, teslimat ×1,6, abone ×2).</summary>
        void HedefleriBelirle(Tur t)
        {
            int maxIlce = CityMap.I != null ? CityMap.I.MaxLevel : 5;
            // Bütün bölgeler zaten açıksa bölge hedefi konmaz (-1): baştan tutmuş bir şart anlamsız
            hedefIlce = AcikIlce >= maxIlce ? -1 : Mathf.Min(maxIlce, Mathf.Max(t.ilce, AcikIlce + 1));
            hedefTeslimat = Mathf.Max(t.teslimat, Mathf.RoundToInt(OrtTeslimat * 1.6f / 10f) * 10);
            hedefAbone = t.abone > 0 ? Mathf.Max(t.abone, Mathf.RoundToInt(AboneSayisi * 2f / 10f) * 10) : 0;
        }

        /// <summary>Bu turda bölge hedefi var mı (bütün bölgeler açıkken teklif geldiyse yok).</summary>
        public bool IlceHedefiVar => hedefIlce >= 0;
        public int HedefIlce => hedefIlce > 0 ? hedefIlce : hedefIlce < 0 ? AcikIlce : (AktifTur != null ? AktifTur.ilce : 1);
        public int HedefTeslimat => hedefTeslimat > 0 ? hedefTeslimat : (AktifTur != null ? AktifTur.teslimat : 0);
        public int HedefAbone => hedefAbone;

        public bool IlceTamam(Tur t) => !IlceHedefiVar || AcikIlce >= HedefIlce;
        public bool TeslimatTamam(Tur t) => OrtTeslimat >= HedefTeslimat;
        public bool AboneTamam(Tur t) => AboneSayisi >= HedefAbone;
        public bool HedeflerTamam(Tur t) => IlceTamam(t) && TeslimatTamam(t) && AboneTamam(t);
        public int KalanGun => hedefAktif ? Mathf.Max(0, hedefBitis - Gun) : 0;

        public string HedefMetni(Tur t) =>
            (IlceHedefiVar ? $"{HedefIlce} açık bölge (şimdi {AcikIlce}) · " : "") +
            $"günde {HedefTeslimat} teslimat (7 gün ort. {OrtTeslimat:0})" +
            (HedefAbone > 0 ? $" · {HedefAbone} abone (şimdi {AboneSayisi})" : "");

        /// <summary>
        /// Temettü: yatırımcı payı (1 − kurucu payı) × TemettuOrani × dünkü faaliyet kârı. Geliştirme, bölge, sözleşme gibi tek
        /// seferlik alımlar kârdan düşülmez (büyümeye yatırım temettüyü azaltmasın). Zarar günü ödeme yok.
        /// Geç oyunda biriken parayı eritir: A'dan sonra kârın ~%14'ü, B'den sonra ~%21'i, C'den sonra ~%27'si.
        /// </summary>
        void Temettu()
        {
            BugunTemettu = 0;
            if (tamamlanan == 0 || pay >= 0.999f) return;
            var em = EconomyManager.I;
            var dun = em != null ? em.Yesterday : null;
            if (dun == null) return;
            long faaliyet = dun.Profit + dun.yatirimHarcama;
            if (faaliyet <= 0) return;
            long t = (long)(faaliyet * (1f - pay) * TemettuOrani);
            if (t < 1000) return;
            em.Spend(t, "Yatırımcı temettüsü", ExpenseCategory.Diger);
            BugunTemettu = t;
            ToplamTemettu += t;
        }

        void YeniGun()
        {
            Temettu();
            var e = EventManager.I;
            if (hedefAktif)
            {
                var t = AktifTur;
                if (t != null && HedeflerTamam(t))
                {
                    hedefAktif = false;
                    kosul = 1.25f;
                    sonTeklifGunu = Gun;
                    CompanyProfile.AddReputation(0.05f);
                    e.Push(new GameEvent($"{t.ad}: hedefler tutturuldu",
                        $"{t.yatirimci} memnun: {HedefMetni(t)}. Basında \"hızlı büyüyen girişim\" haberleri çıktı (itibar +0,05). " +
                        (SiradakiTur != null ? $"Sıradaki tur ({SiradakiTur.ad}) daha iyi koşullarla gelecek." : "Son tur da tamam; artık şirketi satmayı düşünebilirsin."))
                        .Ozet("Payın", $"%{pay * 100f:0}", 0));
                }
                else if (Gun > hedefBitis)
                {
                    hedefAktif = false;
                    baski++;
                    kosul = 0.7f;
                    sonTeklifGunu = Gun;
                    YonetimKurulu(t);
                }
                return;
            }

            if (teklifTur >= 0) return;   // cevap bekleyen teklif var
            var s = SiradakiTur;
            if (s == null) return;
            int bekleme = tamamlanan == 0 && ret == 0 ? 0 : (ret > 0 ? 30 : 14);
            if (Gun - sonTeklifGunu < bekleme) return;
            if (GameClock.I.CompanyDay < s.enErkenGun || OrtTeslimat < s.enAzTeslimat) return;
            Teklif(tamamlanan);
        }

        void Teklif(int no)
        {
            var t = Turlar[no];
            teklifTur = no;
            HedefleriBelirle(t);
            long tutar = TeklifTutari(t);
            float p = TeklifPayi(t);
            long degerleme = (long)(tutar / p);
            string kosulYazi = kosul > 1.01f ? " Önceki hedefleri tutturduğun için koşullar iyi." : kosul < 0.99f ? " Önceki hedefler tutmadığı için koşullar ağır." : "";
            EventManager.I.Push(new GameEvent($"Yatırımcı teklifi: {t.ad}",
                    $"{t.yatirimci}, {Ft(tutar)} karşılığında şirketin %{p * 100f:0} payını istiyor (şirket değerlemesi {Ft(degerleme)}).{kosulYazi}\n\n" +
                    $"Şart: {t.sure} gün içinde {HedefMetni(t)}.\n" +
                    $"Tutturursan sonraki tur daha iyi gelir; tutturamazsan yönetim kurulu baskısı başlar. " +
                    $"Payın %{pay * 100f:0} → %{pay * (1f - p) * 100f:0} olur; şirketi satarken marka puanı sana düşen paydan hesaplanır. " +
                    $"Yatırımcılar her gün faaliyet kârının, paylarının yarısı kadarını temettü olarak alır (kabul edersen kârın ~%{(1f - pay * (1f - p)) * TemettuOrani * 100f:0}'i).")
                .Ozet("Yatırım", Ft(tutar), 1)
                .Ozet("Verilen pay", $"%{p * 100f:0}", 3)
                .Ozet("Süre", $"{t.sure} gün")
                .Add("Kabul et", () => Kabul(no))
                .Add("Reddet (bir ay sonra tekrar sorulur)", () => { teklifTur = -1; ret++; sonTeklifGunu = Gun; }));
        }

        void Kabul(int no)
        {
            var t = Turlar[no];
            long tutar = TeklifTutari(t);
            float p = TeklifPayi(t);
            teklifTur = -1;
            EconomyManager.I.AddSermaye(tutar, $"Yatırım: {t.ad} ({t.yatirimci})");
            toplamYatirim += tutar;
            pay *= 1f - p;
            tamamlanan = no + 1;
            hedefAktif = true;
            hedefBitis = Gun + t.sure;
            kosul = 1f;
            ret = 0;
            EventManager.I.Notify($"{t.ad}: {Ft(tutar)} kasaya girdi. Payın %{pay * 100f:0}. Hedef {t.sure} gün: {HedefMetni(t)}.");
        }

        void YonetimKurulu(Tur t)
        {
            EventManager.I.Push(new GameEvent("Yönetim kurulu baskısı",
                    $"{t?.ad} hedefleri süresinde tutmadı: {(t != null ? HedefMetni(t) : "")}. {t?.yatirimci} yönetim kurulunda hesap soruyor. " +
                    "Sonraki yatırım turu daha ağır koşullarla gelecek. Şimdi bir taviz vermen gerekiyor.")
                .Add("Masrafları kıs: yoğun saat primleri kapanır, kurye morali düşer", () =>
                {
                    var c = CampaignManager.I;
                    if (c != null) { c.aksamPrimi = false; c.oglePrimi = false; }
                    if (CourierManager.I != null) foreach (var k in CourierManager.I.Couriers) k.morale = Mathf.Clamp01(k.morale - 0.06f);
                })
                .Add("Kurucu payından %3 ver", () => pay = Mathf.Max(0.05f, pay - 0.03f)));
        }

        /// <summary>Finans ve ağaçta gösterilen kısa durum.</summary>
        public string Durum
        {
            get
            {
                if (hedefAktif && AktifTur != null)
                    return $"{AktifTur.ad} hedefi · {KalanGun} gün kaldı · {HedefMetni(AktifTur)}";
                if (teklifTur >= 0) return $"{Turlar[teklifTur].ad} teklifi cevap bekliyor";
                var s = SiradakiTur;
                if (s == null) return "Bütün turlar tamam";
                return $"Sıradaki: {s.ad} (günde {s.enAzTeslimat}+ teslimat" + (s.enErkenGun > 0 ? $", {s.enErkenGun}. günden sonra)" : ")");
            }
        }

        static string Ft(long v) => GameData.Ft(v);

        public YatirimSave Kaydet() => new YatirimSave
        {
            pay = pay, tamamlanan = tamamlanan, baski = baski, ret = ret, hedefAktif = hedefAktif, hedefBitis = hedefBitis,
            sonTeklifGunu = sonTeklifGunu, kosul = kosul, toplamYatirim = toplamYatirim, toplamTemettu = ToplamTemettu, teklifTur = teklifTur,
            hedefIlce = hedefIlce, hedefTeslimat = hedefTeslimat, hedefAbone = hedefAbone
        };

        public void Yukle(YatirimSave s)
        {
            if (s == null) return;
            pay = Mathf.Clamp(s.pay, 0.05f, 1f);
            tamamlanan = Mathf.Clamp(s.tamamlanan, 0, Turlar.Length);
            baski = s.baski; ret = s.ret; hedefAktif = s.hedefAktif; hedefBitis = s.hedefBitis;
            hedefIlce = s.hedefIlce; hedefTeslimat = s.hedefTeslimat; hedefAbone = s.hedefAbone;
            sonTeklifGunu = s.sonTeklifGunu; kosul = s.kosul <= 0f ? 1f : s.kosul; toplamYatirim = s.toplamYatirim;
            ToplamTemettu = s.toplamTemettu;
            // Kayıtta açık kalmış teklif kartı kaybolur (kartlar kayda girmez): teklifi yeniden sor
            teklifTur = -1;
        }
    }
}
