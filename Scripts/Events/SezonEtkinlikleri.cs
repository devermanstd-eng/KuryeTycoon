using System;
using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    [Serializable]
    public class SezonSave
    {
        public string aktifId = "";
        public int aktifBas, aktifBit, hedefTeslimat;
        public long basTeslimat;
        public bool hedefli, kampanya, prim, duyuruId;
        public string duyurulanId = "";
        public int duyurulanBas;
        public int tamamlanan, kacirilan;
        public List<string> bitenler = new List<string>();   // "id@başlangıç günü"
    }

    /// <summary>
    /// Sezonluk etkinlikler (oyun takvimiyle): Ramazan, Paskalya, Anneler Günü, kupa finali, yaz turnuvası, Szent István günü,
    /// okula dönüş, Black Friday haftası, Noel alışverişi (advent), yılbaşı. Teslimat platformlarının gerçekte en yoğun günleri.
    ///  • Talep: her gün belli saatlerde CityEvents'e etkinlik yazar (yemek / market / bütün siparişler çarpanı, bazen meydanda alan).
    ///    İşletme teslimatı ortakları da etkilenir (Anneler Günü çiçekçi ×4, Black Friday elektronik ×3...).
    ///  • Hedef: etkinlik boyunca son 7 günün ortalamasının biraz üstünde teslimat ve en fazla %8 iptal + kaçan. Tutarsan ödül:
    ///    para (7 gün ort. işletme kârı × gün × 0,4, en az 400 €), itibar +0,05 ve kalıcı komisyon +%1 (en fazla +%10).
    ///    Oyunun ilk haftasında başlayan etkinliğin hedefi yok (sadece talep etkisi).
    ///  • Hazırlık: 3 gün önce kart: etkinlik kampanyası (talep +%12, bedel 0,5 günlük gelir), kurye primi (ilk 7 gün ücret +%8,
    ///    çevrimiçi +%15), ikisi ya da hiçbiri.
    ///  • Gerçek takvim: oyuncu bilgisayarındaki tarih de aynı etkinliğin içindeyse ödül ×1,5 (oyuncuyu o günlerde geri çeker).
    /// </summary>
    public class SezonEtkinlikleri : MonoBehaviour
    {
        public static SezonEtkinlikleri I { get; private set; }

        public class Sezon
        {
            public string id, ad, aciklama;
            public Func<int, int> bas;            // oyun yılı → başlangıcın mutlak günü
            public int gun;
            public float hedefCarpani = 1.15f;
            public Action<int, int> gunluk;        // (mutlak gün, etkinliğin kaçıncı günü 0..)
            public Dictionary<int, float> isletme; // işletme teslimatı ortak türü → paket çarpanı
            public Func<DateTime, bool> gercek;    // gerçek takvimde de şu an mı
        }

        public static readonly List<Sezon> Sezonlar = new List<Sezon>();

        // Durum
        public Sezon Aktif { get; private set; }
        public int aktifBas, aktifBit, hedefTeslimat;
        public long basTeslimat;
        public bool hedefli, kampanya, prim;
        Sezon duyurulan;
        int duyurulanBas;
        public int tamamlanan, kacirilan;
        readonly HashSet<string> bitenler = new HashSet<string>();

        public const float KampanyaTalep = 1.12f, EnFazlaKayip = 0.08f;

        void Awake()
        {
            I = this;
            if (Sezonlar.Count == 0) Tanimla();
        }

        public void Init()
        {
            Aktif = null; duyurulan = null;
            hedefli = kampanya = prim = false;
            tamamlanan = kacirilan = 0;
            bitenler.Clear();
            GameClock.I.OnNewDay -= YeniGun;
            GameClock.I.OnNewDay += YeniGun;
            kuruldu = false;   // ilk karede (kayıt yüklendiyse Yukle'den sonra) bugünün etkinlikleri kurulur
        }

        bool kuruldu;

        // ------------------------------------------------------------------ dışarıya açık etkiler

        /// <summary>OrderManager: etkinlik kampanyası talebi.</summary>
        public static float TalepCarpani => I != null && I.Aktif != null && I.kampanya ? KampanyaTalep : 1f;

        /// <summary>Buyume.KomisyonCarpani: tutturulan her etkinlik +%1 (en fazla +%10).</summary>
        public static float KaliciCarpani => I != null ? 1f + 0.01f * Mathf.Min(10, I.tamamlanan) : 1f;

        /// <summary>IsletmeTeslimati: ortak türünün (0 eczane, 1 çiçekçi ...) etkinlikteki paket çarpanı.</summary>
        public static float IsletmeCarpani(int tur)
        {
            if (I == null || I.Aktif == null || I.Aktif.isletme == null) return 1f;
            return I.Aktif.isletme.TryGetValue(tur, out var c) ? c : 1f;
        }

        public bool GercekTakvimde(Sezon s)
        {
            try { return s != null && s.gercek != null && s.gercek(DateTime.Now); }
            catch { return false; }
        }

        public long BugunkuTeslimat => Math.Max(0, Milestones.TotalDeliveries - basTeslimat);
        public int KalanGun => Aktif != null ? Mathf.Max(0, aktifBit - GameClock.I.Day + 1) : 0;

        /// <summary>Hedefler sekmesi için: aktif ya da 7 gün içinde başlayacak etkinlik.</summary>
        public Sezon Yaklasan(out int basGun)
        {
            basGun = 0;
            if (GameClock.I == null) return null;
            int bugun = GameClock.I.Day;
            Sezon en = null;
            foreach (var s in Sezonlar)
                for (int y = GameClock.I.Year; y <= GameClock.I.Year + 1; y++)
                {
                    int b = s.bas(y);
                    if (b > bugun && b - bugun <= 7 && (en == null || b < basGun)) { en = s; basGun = b; }
                }
            return en;
        }

        // ------------------------------------------------------------------ günlük akış

        void YeniGun()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || !kuruldu) return;
            GunuKur();
        }

        void Update()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || GameClock.I == null) return;
            if (!kuruldu) { kuruldu = true; GunuKur(); }
            if (Aktif == null || StatsHistory.I == null) return;
            if (GameClock.I.Day <= aktifBit) return;
            // Bitti: son gününün gün sonu kaydı yazılınca değerlendir
            int fark = GameClock.I.Day - GameClock.I.CompanyDay;
            var gunler = StatsHistory.I.Days;
            if (gunler.Count == 0 || gunler[gunler.Count - 1].companyDay + fark < aktifBit) return;
            Bitir(fark);
        }

        void GunuKur()
        {
            var clk = GameClock.I;
            int bugun = clk.Day;
            // Bugün ve yarın için talep etkinlikleri; başlayan etkinliği aç
            foreach (var s in Sezonlar)
                for (int y = clk.Year - 1; y <= clk.Year + 1; y++)
                {
                    int b = s.bas(y);
                    if (b <= 0) continue;
                    for (int k = 0; k <= 1; k++)
                    {
                        int g = bugun + k;
                        if (g >= b && g < b + s.gun) s.gunluk?.Invoke(g, g - b);
                    }
                    if (b == bugun && Aktif == null && !bitenler.Contains(s.id + "@" + b)) Baslat(s, b);
                    if (b - bugun == 3 && duyurulan == null && Aktif == null && clk.CompanyDay + 3 >= 7) Duyur(s, b);
                }
        }

        static long Round1000(float v) => (long)Mathf.Round(v / 1000f) * 1000;

        static float Ort7Teslimat()
        {
            var h = StatsHistory.I != null ? StatsHistory.I.Days : null;
            if (h == null || h.Count == 0) return 0f;
            int n = 0; float t = 0f;
            for (int i = Mathf.Max(0, h.Count - 7); i < h.Count; i++) { t += h[i].deliveries; n++; }
            return n > 0 ? t / n : 0f;
        }

        public static long KampanyaBedeli => Round1000(Mathf.Clamp(CrisisManager.AvgDailyIncome() * 0.5f, 100000f, 4000000f));

        void Duyur(Sezon s, int b)
        {
            duyurulan = s; duyurulanBas = b;
            Telemetry.Add("Sezon: duyuru");
            long bedel = KampanyaBedeli;
            bool gercek = GercekTakvimde(s);
            var ev = new GameEvent($"Yaklaşan etkinlik: {s.ad}",
                $"{s.ad} 3 gün sonra başlıyor ({GameClock.I.DateOf(b)}, {s.gun} gün). {s.aciklama}\n\n" +
                "Etkinlik boyunca hedef: son haftanın ortalamasının biraz üstünde teslimat ve en fazla %8 iptal + kaçan sipariş. " +
                "Tutarsan para ödülü, itibar ve kalıcı +%1 komisyon." +
                (gercek ? "\n\nGerçek takvimde de şu an bu dönemdesin: ödül ×1,5." : ""));
            ev.Add($"Etkinlik kampanyası yap (−{GameData.Ft(bedel)}, talep +%12)", () => Hazirlik(true, false, bedel));
            ev.Add("Kuryelere etkinlik primi (ilk 7 gün ücret +%8, daha çok kurye çevrimiçi)", () => Hazirlik(false, true, 0));
            ev.Add($"İkisi de (−{GameData.Ft(bedel)} + kurye primi)", () => Hazirlik(true, true, bedel));
            ev.Add("Hazırlık yapma", () => Hazirlik(false, false, 0));
            EventManager.I.Push(ev);
        }

        void Hazirlik(bool kamp, bool pr, long bedel)
        {
            if (kamp && !EconomyManager.I.TrySpend(bedel, $"Etkinlik kampanyası: {duyurulan?.ad}", ExpenseCategory.Reklam)) kamp = false;
            kampanya = kamp; prim = pr;
        }

        void Baslat(Sezon s, int b)
        {
            Aktif = s;
            aktifBas = b;
            aktifBit = b + s.gun - 1;
            basTeslimat = Milestones.TotalDeliveries;
            hedefli = GameClock.I.CompanyDay >= 7;
            if (duyurulan != s) { kampanya = false; prim = false; }   // duyurusu yapılmadan başladı (yükleme, ilk hafta)
            duyurulan = null;
            float ort = Ort7Teslimat();
            int gunEtkin = s.gun;
            hedefTeslimat = Mathf.Max(30 * gunEtkin, Mathf.RoundToInt(ort * gunEtkin * s.hedefCarpani));
            if (prim && CrisisManager.I != null)
                CrisisManager.I.AddEffect(new CrisisEffect { id = "sezon_prim", title = $"{s.ad}: kurye primi", pay = 1.08f, online = 1.15f }, Mathf.Min(7, s.gun));
            Telemetry.Add("Sezon: başladı");
            EventManager.I.Push(new GameEvent($"Etkinlik başladı: {s.ad}",
                $"{s.aciklama}\n\n" + (hedefli
                    ? $"Hedef: {s.gun} günde {hedefTeslimat} teslimat ve en fazla %{EnFazlaKayip * 100f:0} iptal + kaçan. Hedefler > Haftalık'ta ilerlemesi görünür." +
                      (kampanya ? " Etkinlik kampanyası yayında (talep +%12)." : "") + (prim ? " Kurye primi verildi." : "")
                    : "Şirket yeni kurulduğu için bu etkinliğin hedefi yok; sadece talep değişir.")));
        }

        void Bitir(int fark)
        {
            var s = Aktif;
            bitenler.Add(s.id + "@" + aktifBas);
            long teslim = 0; int kayip = 0;
            foreach (var d in StatsHistory.I.Days)
            {
                int abs = d.companyDay + fark;
                if (abs < aktifBas || abs > aktifBit) continue;
                teslim += d.deliveries;
                kayip += d.cancelled + d.lost;
            }
            float oran = teslim + kayip > 0 ? kayip / (float)(teslim + kayip) : 0f;
            Aktif = null;
            bool kamp = kampanya;
            kampanya = prim = false;
            if (!hedefli) return;
            bool tuttu = teslim >= hedefTeslimat && oran <= EnFazlaKayip;
            if (tuttu)
            {
                tamamlanan++;
                float carpan = GercekTakvimde(s) ? 1.5f : 1f;
                long odul = Round1000(Mathf.Max(160000f, Buyume.OrtalamaNet(7) * s.gun * 0.4f) * carpan);
                EconomyManager.I.AddIncome(odul, $"Etkinlik ödülü: {s.ad}");
                CompanyProfile.AddReputation(0.05f);
                Telemetry.Add("Sezon: hedef tuttu");
                EventManager.I.Push(new GameEvent($"{s.ad}: hedef tuttu!",
                    $"{teslim} teslimat (hedef {hedefTeslimat}), iptal + kaçan %{oran * 100f:0.#}. Ödül {GameData.Ft(odul)}" +
                    (carpan > 1f ? " (gerçek takvim ×1,5)" : "") + $", itibar +0,05 ve kalıcı komisyon +%1 (toplam +%{Mathf.Min(10, tamamlanan)})."));
            }
            else
            {
                kacirilan++;
                Telemetry.Add("Sezon: hedef tutmadı");
                EventManager.I.Notify($"{s.ad} bitti: hedef tutmadı ({teslim}/{hedefTeslimat} teslimat, iptal + kaçan %{oran * 100f:0.#}, sınır %{EnFazlaKayip * 100f:0}).");
            }
        }

        // ------------------------------------------------------------------ kayıt

        public SezonSave Kaydet()
        {
            var s = new SezonSave
            {
                aktifId = Aktif != null ? Aktif.id : "", aktifBas = aktifBas, aktifBit = aktifBit, hedefTeslimat = hedefTeslimat,
                basTeslimat = basTeslimat, hedefli = hedefli, kampanya = kampanya, prim = prim,
                duyurulanId = duyurulan != null ? duyurulan.id : "", duyurulanBas = duyurulanBas,
                tamamlanan = tamamlanan, kacirilan = kacirilan
            };
            s.bitenler.AddRange(bitenler);
            return s;
        }

        public void Yukle(SezonSave s)
        {
            if (s == null) return;
            Aktif = Sezonlar.Find(x => x.id == s.aktifId);
            aktifBas = s.aktifBas; aktifBit = s.aktifBit; hedefTeslimat = s.hedefTeslimat; basTeslimat = s.basTeslimat;
            hedefli = s.hedefli; kampanya = s.kampanya; prim = s.prim;
            duyurulan = Sezonlar.Find(x => x.id == s.duyurulanId); duyurulanBas = s.duyurulanBas;
            tamamlanan = s.tamamlanan; kacirilan = s.kacirilan;
            bitenler.Clear();
            if (s.bitenler != null) foreach (var b in s.bitenler) bitenler.Add(b);
        }

        // ------------------------------------------------------------------ tanımlar

        static void Etk(int gun, string ad, float bas, float bit, float yemek = 1f, float market = 1f, float hepsi = 1f, string aciklama = "", bool meydan = false)
        {
            if (CityEvents.I == null) return;
            var e = new CityEvent { title = ad, day = gun, fromHour = bas, toHour = bit, food = yemek, market = market, demand = hepsi, desc = aciklama };
            if (meydan) { e.spotX = CityMap.PlazaCenter.x; e.spotY = CityMap.PlazaCenter.y; e.spotRadius = 6; e.spotShare = 0.3f; }
            CityEvents.I.SezonEkle(e);
        }

        static int Abs(int y, int m, int d) => GameClock.AbsoluteDay(y, m, d);

        /// <summary>Ayın belli bir haftanın günü (0 Pzt … 6 Paz) aralığındaki ilk günü.</summary>
        static int IlkGun(int y, int m, int gun1, int gun2, int haftaGunu)
        {
            for (int d = gun1; d <= gun2; d++)
            {
                int a = Abs(y, m, d);
                if (GameClock.I != null && GameClock.I.DayOfWeekOf(a) == haftaGunu) return a;
            }
            return Abs(y, m, gun1);
        }

        static bool Arada(DateTime t, int m1, int d1, int m2, int d2)
        {
            var a = new DateTime(t.Year, m1, d1); var b = new DateTime(t.Year, m2, d2);
            return t.Date >= a && t.Date <= b;
        }

        static void Tanimla()
        {
            Sezonlar.Add(new Sezon
            {
                id = "ramazan", ad = "Ramazan", gun = 30, hedefCarpani = 1.03f,
                aciklama = "Şehirdeki Müslüman aileler ve üniversitenin uluslararası öğrencileri oruç tutuyor: iftar saatinde döner, pide ve tatlı siparişleri, gece sahur siparişleri artar.",
                bas = y => { int doy = 49 - 11 * (y - 1); while (doy <= 0) doy += GameClock.DaysPerYear; return (y - 1) * GameClock.DaysPerYear + doy; },
                gunluk = (g, i) =>
                {
                    Etk(g, "İftar saati", 18f, 20.5f, yemek: 1.15f, aciklama: "Oruç açılıyor: iftar sofrası için yemek siparişleri.");
                    Etk(g, "Sahur", 26.5f, 28.5f, yemek: 1.2f, aciklama: "Gece sahur siparişleri.");
                },
                gercek = t => { var bas = new DateTime(2026, 2, 18).AddDays(-10.9 * (t.Year - 2026)); return t.Date >= bas.Date && t.Date < bas.Date.AddDays(30); }
            });
            Sezonlar.Add(new Sezon
            {
                id = "paskalya", ad = "Paskalya (Húsvét)", gun = 3, hedefCarpani = 1.1f,
                aciklama = "Macaristan'da büyük bayram: cumartesi jambon, yumurta ve tatlı alışverişi, pazar aile sofrası, pazartesi (Húsvéthétfő) tatil ve dışarıdan yemek.",
                bas = y => Abs(y, 4, 4),
                gunluk = (g, i) =>
                {
                    if (i == 0) Etk(g, "Paskalya alışverişi", 8f, 18f, market: 1.35f, aciklama: "Jambon, yumurta, kalács: market siparişleri çok yüksek.");
                    if (i == 1) Etk(g, "Paskalya sofrası", 11f, 16f, hepsi: 0.8f, aciklama: "Aileler sofrada: öğlen sipariş az.");
                    if (i == 2) Etk(g, "Paskalya pazartesisi", 12f, 21f, yemek: 1.25f, aciklama: "Resmî tatil, kimse yemek yapmak istemiyor.");
                },
                isletme = new Dictionary<int, float> { { 1, 2.5f } },
                gercek = t => Arada(t, 3, 28, 4, 22) && t.DayOfWeek >= DayOfWeek.Friday || Arada(t, 3, 29, 4, 23) && t.DayOfWeek == DayOfWeek.Monday
            });
            Sezonlar.Add(new Sezon
            {
                id = "anneler", ad = "Anneler Günü", gun = 1, hedefCarpani = 1.2f,
                aciklama = "Mayısın ilk pazarı: kahvaltı ve brunch siparişleri, çiçek ve hediye paketleri (işletme teslimatında çiçekçi ×4).",
                bas = y => IlkGun(y, 5, 1, 7, 6),
                gunluk = (g, i) => Etk(g, "Anneler Günü brunch'ı", 9f, 14f, yemek: 1.3f, aciklama: "Annelere kahvaltı ve tatlı."),
                isletme = new Dictionary<int, float> { { 1, 4f }, { 4, 1.5f } },
                gercek = t => t.Month == 5 && t.Day <= 7 && t.DayOfWeek == DayOfWeek.Sunday
            });
            Sezonlar.Add(new Sezon
            {
                id = "kupafinali", ad = "Kupa finali", gun = 1, hedefCarpani = 1.25f,
                aciklama = "Mayıs sonu büyük final akşamı: bütün şehir ekran başında, pizza, burger ve atıştırmalık.",
                bas = y => IlkGun(y, 5, 25, 31, 5),
                gunluk = (g, i) => Etk(g, "Kupa finali", 19.5f, 23.5f, yemek: 1.6f, market: 1.2f, aciklama: "Final maçı: başlama düdüğünden önce sipariş yağar."),
                gercek = t => t.Month == 5 && t.Day >= 25 && t.DayOfWeek == DayOfWeek.Saturday
            });
            Sezonlar.Add(new Sezon
            {
                id = "turnuva", ad = "Yaz futbol turnuvası", gun = 32, hedefCarpani = 1.05f,
                aciklama = "Dünya Kupası / Avrupa Şampiyonası ayı: iki günde bir akşam maçı, Macaristan oynadığı akşamlar çok yoğun.",
                bas = y => Abs(y, 6, 11),
                gunluk = (g, i) =>
                {
                    if (i % 6 == 2) Etk(g, "Macaristan maçı", 19f, 23.5f, yemek: 1.7f, market: 1.25f, aciklama: "Milli maç: şehir evde ya da sokakta, sipariş patlıyor.", meydan: true);
                    else if (i % 2 == 0) Etk(g, "Turnuva maçı", 17.5f, 23f, yemek: 1.35f, aciklama: "Akşam maçı: pizza ve burger.");
                },
                gercek = t => Arada(t, 6, 11, 7, 12)
            });
            Sezonlar.Add(new Sezon
            {
                id = "szentistvan", ad = "Szent István günü", gun = 1, hedefCarpani = 1.2f,
                aciklama = "20 Ağustos, millî bayram: akşam havai fişek, meydanda kalabalık, sokak yemekleri.",
                bas = y => Abs(y, 8, 20),
                gunluk = (g, i) => Etk(g, "Havai fişek akşamı", 17f, 23.5f, hepsi: 1.25f, aciklama: "Meydanda kutlama: çevresinden çok sipariş.", meydan: true),
                gercek = t => t.Month == 8 && t.Day == 20
            });
            Sezonlar.Add(new Sezon
            {
                id = "okul", ad = "Okula dönüş", gun = 7, hedefCarpani = 1.08f,
                aciklama = "Eylülün ilk haftası: okul ve üniversite açılıyor; sabah ve akşam market, kırtasiye ve kitap paketleri.",
                bas = y => Abs(y, 9, 1),
                gunluk = (g, i) =>
                {
                    Etk(g, "Okul alışverişi (sabah)", 7f, 10f, market: 1.2f, aciklama: "Okul öncesi kahvaltılık ve atıştırmalık.");
                    Etk(g, "Okul alışverişi (akşam)", 15f, 19f, market: 1.2f, aciklama: "Okul dönüşü market.");
                },
                isletme = new Dictionary<int, float> { { 9, 3f }, { 2, 2f } },
                gercek = t => Arada(t, 9, 1, 9, 7)
            });
            Sezonlar.Add(new Sezon
            {
                id = "blackfriday", ad = "Black Friday haftası", gun = 5, hedefCarpani = 1.15f,
                aciklama = "Perşembeden Cyber Monday'e indirim haftası: market sepetleri büyük, elektronik ve giyim paketleri (işletme teslimatında ×2–3).",
                bas = y => IlkGun(y, 11, 23, 29, 4) - 1,
                gunluk = (g, i) =>
                {
                    if (i != 1) Etk(g, "Black Friday indirimleri", 8f, 22f, market: 1.3f, hepsi: 1.05f, aciklama: "İndirim haftası: market siparişleri yüksek.");
                },
                isletme = new Dictionary<int, float> { { 3, 3f }, { 4, 2.5f }, { 8, 2f }, { 2, 1.5f } },
                gercek = t => t.Month == 11 && t.Day >= 22 || t.Month == 12 && t.Day <= 2
            });
            Sezonlar.Add(new Sezon
            {
                id = "advent", ad = "Noel alışverişi (advent)", gun = 23, hedefCarpani = 1.05f,
                aciklama = "Aralığın ilk üç haftası: akşamları hediye ve market alışverişi, Noel pazarı. İşletme teslimatında hediye paketleri ×1,6.",
                bas = y => Abs(y, 12, 1),
                gunluk = (g, i) => Etk(g, "Noel alışverişi", 16f, 21f, market: 1.1f, yemek: 1.05f, aciklama: "Hediye ve Noel hazırlığı."),
                isletme = new Dictionary<int, float> { { 1, 1.6f }, { 2, 1.6f }, { 3, 1.6f }, { 4, 1.6f }, { 8, 1.6f } },
                gercek = t => Arada(t, 12, 1, 12, 23)
            });
            Sezonlar.Add(new Sezon
            {
                id = "yilbasi", ad = "Yılbaşı (Szilveszter)", gun = 2, hedefCarpani = 1.2f,
                aciklama = "Yılbaşı gecesi parti hazırlığı ve 1 Ocak öğleden sonrası akşamdan kalma şehir: yılın en yoğun iki günü.",
                bas = y => Abs(y, 12, 31),
                gunluk = null,   // talep etkisi zaten şehir takviminde (CityEvents)
                gercek = t => t.Month == 12 && t.Day == 31 || t.Month == 1 && t.Day == 1
            });
        }
    }
}
