using UnityEngine;

namespace KuryeTycoon
{
    // Sıra kayıt uyumluluğu için sabit: yeni türler sona eklenir
    // Dinlenme = kurye avantaj programı, Bakim = anlaşmalı servisler, Hijyen = çanta kontrolü ve ekipman partneri (eski adlar kayıt uyumu için)
    // Yeni türler hep sona eklenir (kayıttaki seviye dizisi sıraya göre okunur)
    public enum UpgradeKind { Platform, Dispatcher, Dinlenme, Bakim, Hijyen, Reklam, Uygulama, Ofis, Restoran, Adres, Hub, EFilo, KargoBisikleti, PaketDolabi, Robot, Kargo, Akademi, Otomasyon, Isletme }

    public class UpgradeDef
    {
        public UpgradeKind kind;
        public string name;
        public string summary;
        public int[] costs;           // seviyeye çıkma bedeli: costs[i] = i+1. seviyenin kurulum bedeli (costs.Length = en yüksek seviye)
        public int[] daily;           // seviyenin günlük işletme gideri: daily[i] = i. seviyede (daily.Length = MaxLevel + 1)
        public string[] levelNames;   // levelNames[0] = başlangıç durumu
        public System.Func<int, string> effect;   // seviyenin etkisi (metin)
        public int[] reqDistrict;     // i+1. seviye için gereken bölge (boşsa yok)
        public int[] reqOffice;       // i+1. seviye için gereken merkez ofis seviyesi (boşsa yok)
        public System.Func<int, string> extraLock;   // hedef seviye için ek koşul (boş metin = yok)
        public ExpenseCategory category = ExpenseCategory.Gelistirme;
        public bool canDowngrade = true;

        public int MaxLevel => costs.Length;
        public string LevelText(int lv) => $"{levelNames[lv]}: {effect(lv)}";
        /// <summary>Seviyenin günlük gideri; şirket büyüdükçe yavaşça artar (Denge.OlcekleGunluk).</summary>
        public int DailyAt(int lv) => daily != null && lv < daily.Length ? Denge.OlcekleGunluk(daily[lv]) : 0;
        public int ReqDistrict(int targetLevel) => reqDistrict != null && targetLevel - 1 < reqDistrict.Length ? reqDistrict[targetLevel - 1] : 0;
        public int ReqOffice(int targetLevel) => reqOffice != null && targetLevel - 1 < reqOffice.Length ? reqOffice[targetLevel - 1] : 0;
    }

    /// <summary>
    /// Kalıcı şirket geliştirmeleri. Her birinin seviyeleri var; her seviyenin bir kurulum bedeli ve günlük işletme gideri olur
    /// (yazılım aboneliği, kira, temizlik malzemesi, reklam bütçesi…). Gerçekçi kurallar:
    ///  • Platform paketi kaç aktif kurye hesabı olabileceğini belirler. 22 ve 32 kurye yeni bölge ister, 45 ve 60 yalnız fiyatla,
    ///    80 ve üstü bütün şehri (Green Hill) ister (26.11).
    ///  • Kuryeler serbest çalışır: dinlenme, yemek, araç bakımı ve çanta temizliği kendi işleri. Platform ancak avantaj programı,
    ///    anlaşmalı servisler ve uygulama özellikleriyle kolaylaştırır. Ofis destek ekibinin çalıştığı yerdir.
    ///  • Büyük ofis daha çok kira demek; reklam her gün bütçe harcar. İşletme gideri olan geliştirmeler küçültülebilir
    ///    (kurulum bedeli geri gelmez).
    /// Tüm giderler vergiden düşülür.
    /// </summary>
    public class UpgradeManager : MonoBehaviour
    {
        public static UpgradeManager I { get; private set; }

        // Hesap sınırları açık bölgelerin talebine göre: 4 = başlangıç, 14 = Merkez dolu, 22 = Doğu, 32 = Sanayi, 45 = West Harbour, 60–80 = bütün şehir;
        // 100–180 = büyük şehir operasyonu (kurye merkezleri, yapay zekâ dispatch ve bölge ofisleri gerekir)
        static readonly int[] AccountCap = { 4, 8, 14, 22, 32, 45, 60, 80, 100, 125, 150, 180 };

        /// <summary>Başka bir geliştirmenin seviyesi yetmiyorsa kilit metni.</summary>
        static string Need(UpgradeKind k, int lv)
        {
            if (I == null || I.Level(k) >= lv) return "";
            var d = Def(k);
            return $"önce {d.name}: {d.levelNames[lv]}";
        }

        static string FirstLock(params string[] reasons)
        {
            foreach (var r in reasons) if (!string.IsNullOrEmpty(r)) return r;
            return "";
        }

        static string Ft(long v) => GameData.Ft(v);
        static string Pct(float m) => $"%{Mathf.Abs(m - 1f) * 100f:0}";

        public static readonly UpgradeDef[] Defs =
        {
            new UpgradeDef
            {
                kind = UpgradeKind.Platform, name = "Kurye platformu paketi",
                summary = "Kuryeler kendi işinin patronu ama her biri uygulamanda bir hesapla çalışır. Paket kaç aktif kurye hesabını " +
                          "kaldırabileceğini belirler; büyük paketin sunucu ve lisans ücreti de yüksek. İlk büyük paketler yeni bölge ister " +
                          "(22 kurye East Side, 32 kurye Industry & Campus); 80 kurye ve üstü bütün şehri (Green Hill).",
                // Metropol (32 → 45) 1,6 M → 1,1 M → 0,9 M, Kurumsal (45 → 60) 2,8 M → 2,4 M → 2,0 M → 1,3 M. 15.11'de 32'de 30 gün,
                // 16.11 sabah 32'de 17, 45'te 18; 16.11 akşam 22'de 11, 32'de 11, 45'te yine 18 gün beklendi (hedef 10–13): Green Hill
                // 5 810 € + Kurumsal 9 745 € ≈ 15 500 €, günde ~1 000 € birikirken. Green Hill de 1,2 M → 0,9 M (CityMap.UnlockCost).
                // 20.11 120 günlük test (2 şehir): 32, 45 ve 60 hesapta paket 21–28 gün dolu kaldı (hedef ≤13), 60'ta 44–46 gün.
                // Şehir 900 → 675 bin, Metropol 900 → 675 bin, Kurumsal+ 4,5 → 3 M (bölgeler de ucuzladı: CityMap.UnlockCost).
                // 23.11 48 günlük test: 80 hesap 14 gün dolu kaldı (Büyük şehir 50 000 € birikirken); 20 M → 14 M.
                // 24.11 (u30, 5 şehir × 120 gün): fiyat ölçeklemesi kapanınca (Denge.FiyatOlcekleme) 22–60 arası paketler 1–8 günde
                // alınıyordu, 125 hesap 60–66. günde doluyordu. Hedef 9–12 gün: paket + önündeki bölge ≈ o aşamadaki 10 günlük
                // faaliyet kârının yarısı (22 kurye ~1,2 bin €/gün, 32 ~2 bin, 45 ~2,8 bin, 60 ~4 bin). 22: 450 bin → 800 bin,
                // 32: 675 bin → 1,4 M, 45: 675 bin → 2,2 M, 60: 1,3 → 3 M, 80: 3 → 8 M, 125: 40 → 36 M.
                // 25.11 ikinci tur (u34, 2 şehir × 120 gün): bekleme inişli çıkışlıydı, 22: 4–6, 32: 17–25, 45: 1–2, 60: 14–21,
                // 80: 9–11 gün; 100 → 125 geçişi 38–53 gün. Yük 32 ve 60'tan 22 ve 45'e kaydı: 22: 800 bin → 1 M, 32: 1,4 → 1 M,
                // 45: 2,2 → 3 M, 60: 3 → 2,4 M, 125: 36 → 24 M.
                // 26.11 yapısal düzeltme (u35, 2 şehir × 120 gün: 22: 1–3, 32: 20–23, 45: 1–10, 60: 19–36, 80: 5, 100: 33–40 gün):
                // paketin ne kadar beklediğini fiyat değil önündeki bölge kilidi belirliyordu (45 West Harbour'u, 60 Green Hill'i
                // bekliyordu; Green Hill açılınca 45 bir günde bitiyordu) ve fiyat oynatınca duvar bir paketten öbürüne kayıyordu.
                // 45 ve 60'ın bölge kilidi kalktı (32 gibi Industry & Campus yeter); 80 ve üstü bütün şehri ister. Fiyatlar her
                // adımda ~×1,4–1,75 büyüyen düz bir eğri: 32: 1 → 1,4 M, 45: 3 → 2,2 M, 60: 2,4 → 3,2 M, 80: 8 → 5,6 M,
                // 100: 14 → 9 M, 125: 24 → 14 M, 150: 70 → 24 M, 180: 120 → 40 M (125'ten sonra 70 M'lik duvar 40 günlük ölü dönem
                // yapardı; B ve C yatırım turları 60. ve 85. günden sonra gelir, 100 ve 150'yi hızlandırır).
                costs = new[] { 80000, 200000, 1000000, 1400000, 2200000, 3200000, 5600000, 9000000, 14000000, 24000000, 40000000 },
                daily = new[] { 0, 1500, 3000, 5000, 8000, 12000, 17000, 24000, 34000, 46000, 62000, 85000 },
                levelNames = new[] { "Başlangıç", "Büyüme", "Profesyonel", "Bölgesel", "Şehir", "Metropol", "Kurumsal", "Kurumsal+",
                                     "Büyük şehir", "Büyük şehir+", "Bölge lideri", "Pazar lideri" },
                effect = lv => $"{AccountCap[lv]} kurye hesabı",
                reqDistrict = new[] { 0, 0, 2, 3, 3, 3, 5, 5, 5, 5, 5 },   // 22: East Side, 32–60: Industry & Campus, 80+: Green Hill
                extraLock = lv => lv == 8 ? Need(UpgradeKind.Ofis, 2)
                                : lv == 9 ? FirstLock(Need(UpgradeKind.Ofis, 3), Need(UpgradeKind.Hub, 1))
                                : lv == 10 ? FirstLock(Need(UpgradeKind.Hub, 2), Need(UpgradeKind.Dispatcher, 3))
                                : lv == 11 ? FirstLock(Need(UpgradeKind.Hub, 3), Need(UpgradeKind.Ofis, 4)) : "",
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Ofis, name = "Merkez ofis",
                summary = "Operasyon ve destek ekibinin çalıştığı yer; yeni kuryelerin kaydının yapıldığı ve ekipman aldığı nokta. " +
                          "Kuryeler ofise gelmez, sahada kendi başına çalışır. Büyük ofis daha çok destek personeli alır ve daha görünürdür (daha çok başvuru).",
                costs = new[] { 250000, 700000, 1800000, 12000000 },
                daily = new[] { 4000, 7000, 12000, 20000, 32000 },
                levelNames = new[] { "Kiralık küçük ofis", "Sokak katında ofis", "Kat ofisi", "Şirket binası", "Şirket binası + bölge ofisleri" },
                effect = lv => $"en fazla {SupportCapAt(lv)} destek personeli" + (lv > 0 ? $", kurye kaydı +{Pct(OfficeAppsAt(lv))}" : ""),
                reqDistrict = new[] { 0, 2, 3, 5 },
                category = ExpenseCategory.Kira,
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Dispatcher, name = "Dispatcher yazılımı",
                summary = "Siparişlerin kuryelere nasıl dağıtılacağı. Akıllı atama siparişi önce kabul etmesi muhtemel kuryelere verir; " +
                          "talep tahmini yoğun saatten önce kuryelere haber verir.",
                costs = new[] { 90000, 350000, 15000000 },
                daily = new[] { 0, 800, 2000, 7000 },
                levelNames = new[] { "Basit atama", "Akıllı atama", "Akıllı atama + talep tahmini", "Yapay zekâ dispatch + hava tahmini" },
                effect = lv => lv == 0 ? "en yakın kuryeye teklif" : lv == 1 ? "daha az red, kâr öncelikli atama seçeneği"
                             : lv == 2 ? "daha az red, yoğun saatlerde +%15 çevrimiçi kurye"
                             : "daha az red, yoğun saatlerde +%20, yağmur ve karda +%20 çevrimiçi kurye (kuryelere önceden haber verilir)",
                reqDistrict = new[] { 0, 2, 5 },
                extraLock = lv => lv == 3 ? Need(UpgradeKind.Ofis, 3) : "",
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Uygulama, name = "Kurye uygulaması",
                summary = "Rota, navigasyon ve kazanç tahmini. İyi uygulamada kuryeler daha hızlı gider ve teklifleri daha kolay kabul eder.",
                costs = new[] { 110000, 400000, 12000000 },
                daily = new[] { 0, 600, 1500, 5000 },
                levelNames = new[] { "Eski uygulama", "Uygulama 2.0", "Uygulama 3.0 (canlı trafik)", "Uygulama 4.0 (çok duraklı rota)" },
                effect = lv => lv == 0 ? "standart" : lv == 1 ? "kuryeler %5 hızlı, teklif beklentisi %10 düşük"
                             : lv == 2 ? "kuryeler %8 hızlı, teklif beklentisi %15 düşük"
                             : "kuryeler %12 hızlı, teklif beklentisi %20 düşük",
                reqDistrict = new[] { 0, 3, 5 },
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Dinlenme, name = "Kurye avantaj programı",
                summary = "Kuryeler serbest çalışır; molasını, yemeğini kendisi ayarlar. Platform ise anlaşmalı firmalarla kuryelere indirim " +
                          "sağlayabilir. Kurye kendini değerli hisseder: morali yükselir, rakibe kaçmaz.",
                costs = new[] { 30000, 120000, 300000, 6000000 },
                daily = new[] { 0, 400, 1200, 2500, 8000 },
                levelNames = new[] { "Yok", "Akaryakıt ve telefon hattı indirimi", "+ Anlaşmalı restoranlarda yemek indirimi", "+ Sadakat programı (kıdem primi)",
                                     "+ Kaza ve sağlık sigortası katkısı" },
                effect = lv => lv == 0 ? "kurye her şeyi kendi karşılar" : $"her gün moral +{MoraleAt(lv) * 100f:0.#}" + (lv >= 3 ? ", kıdemli kuryeler rakibe kaçmaz" : "")
                             + (lv >= 4 ? ", grev ihtimali yarıya iner" : ""),
                reqDistrict = new[] { 0, 2, 3, 5 },
                category = ExpenseCategory.Diger,
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Bakim, name = "Anlaşmalı servisler",
                summary = "Araç bakımı ve telefon kuryenin kendi işi. Platform anlaşmalı servislerle indirim ve öncelik sağlarsa kuryeler " +
                          "bakımı ertelemez: daha az arıza, daha az kapanan telefon.",
                costs = new[] { 40000, 150000, 350000, 9000000 },
                daily = new[] { 0, 300, 900, 1800, 7000 },
                levelNames = new[] { "Yok", "İndirimli powerbank ve telefon tamiri", "+ Anlaşmalı bisiklet, scooter ve motor servisi", "+ Öncelikli servis ve yedek araç kiralama",
                                     "+ Kendi servis atölyen" },
                effect = lv => lv == 0 ? "standart arıza ve telefon riski"
                             : $"telefon kapanması −{Pct(PhoneAt(lv))}" + (lv >= 2 ? $", araç arızası −{Pct(BreakAt(lv))}" : ""),
                reqDistrict = new[] { 0, 0, 3, 5 },
                category = ExpenseCategory.Diger,
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Hijyen, name = "Çanta kontrolü ve ekipman partneri",
                summary = "Çanta temizliği kuryenin sorumluluğu. Uygulama ara sıra çanta fotoğrafı ister (Wolt'taki gibi); ekipman partneri " +
                          "kuryelere indirimli termal çanta ve temizlik kiti satar.",
                costs = new[] { 50000, 150000 },
                daily = new[] { 0, 200, 600 },
                levelNames = new[] { "Yok", "Uygulamada çanta fotoğrafı kontrolü", "+ Ekipman partneri (indirimli çanta ve temizlik kiti)" },
                effect = lv => lv == 0 ? "kontrol yok" : lv == 1 ? "kuryeler çantayı daha sık temizler"
                             : $"daha sık temizlik, kirlenme −{Pct(DirtAt(lv))}",
                category = ExpenseCategory.Diger,
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Restoran, name = "Restoran entegrasyonu",
                summary = "Dükkanlara sipariş tableti ve kasa (POS) entegrasyonu: sipariş hazır olunca doğru zamanda haber verilir, " +
                          "kurye dükkanda daha az bekler; paketleme kontrol listesiyle eksik ürün azalır.",
                costs = new[] { 120000, 450000, 10000000 },
                daily = new[] { 0, 1000, 2500, 6000 },
                levelNames = new[] { "Telefonla bildirim", "Dükkan tableti", "Kasa (POS) entegrasyonu + paketleme listesi",
                                     "+ Yapay zekâ hazırlık süresi tahmini" },
                effect = lv => lv == 0 ? "dükkanda sıra ve bekleme sık"
                             : $"dükkanda sıra −{Pct(QueueAt(lv))}" + (lv >= 2 ? $", dükkandaki eksik ürün −{Pct(MissingAt(lv))}" : ""),
                reqDistrict = new[] { 0, 2, 5 },
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Adres, name = "Müşteri uygulaması ve adres doğrulama",
                summary = "Müşteri adresini haritada işaretler, bina girişinin fotoğrafını ve notunu ekler; kurye yaklaşınca müşteriye bildirim gider.",
                costs = new[] { 80000, 250000, 7000000 },
                daily = new[] { 0, 500, 1200, 4000 },
                levelNames = new[] { "Sadece yazılı adres", "Harita pini + giriş fotoğrafı", "+ \"Kurye yaklaştı\" bildirimi",
                                     "+ Canlı konum paylaşımı ve görüntülü bina tarifi" },   // paket dolapları ayrı geliştirme (PaketDolabi)
                effect = lv => lv == 0 ? "yanlış adres ve evde olmayan müşteri sık"
                             : $"yanlış adres −{Pct(WrongAddrAt(lv))}" + (lv >= 2 ? $", müşteri evde yok −{Pct(NotHomeAt(lv))}" : ""),
                reqDistrict = new[] { 0, 2, 5 },
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Reklam, name = "Reklam ve marka bilinirliği",
                summary = "Reklam kalıcı değildir: her gün bütçe harcar. Daha çok sipariş ve kurye kaydı getirir. İstediğin zaman küçültebilirsin.",
                costs = new[] { 50000, 150000, 300000, 8000000 },
                daily = new[] { 0, 2000, 6000, 12000, 30000 },
                levelNames = new[] { "Reklam yok", "Sosyal medya", "Şehir panoları", "Radyo + influencer", "TV + spor kulübü sponsorluğu" },
                effect = lv => lv == 0 ? "ağızdan ağıza" : $"sipariş +{Pct(OrdersAt(lv))}, kurye kaydı +{Pct(AdAppsAt(lv))}",
                reqDistrict = new[] { 0, 2, 3, 5 },
                category = ExpenseCategory.Reklam,
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Hub, name = "Kurye merkezleri (mikro hub)",
                summary = "Şehrin yoğun noktalarında kuryelerin mola verdiği, telefonunu ve aracını şarj ettiği, tuvalete ve sıcak/soğuk " +
                          "havadan kaçabildiği küçük noktalar. Kuryeler daha az yorulur, molayı bitirip daha çabuk yeniden çevrimiçi olur. " +
                          "Büyük platform paketleri için gerekir.",
                costs = new[] { 6000000, 16000000, 36000000 },
                daily = new[] { 0, 6000, 14000, 26000 },
                levelNames = new[] { "Yok", "Merkez'de bir hub", "Üç bölgede hub", "Her bölgede hub + şarj istasyonları" },
                effect = lv => lv == 0 ? "kuryeler molayı sokakta ya da evde verir"
                             : $"yorulma −{Pct(HubFatigueAt(lv))}, çevrimiçi kurye +{Pct(HubOnlineAt(lv))}",
                reqDistrict = new[] { 4, 5, 5 },
                extraLock = lv => lv == 1 ? Need(UpgradeKind.Ofis, 2) : lv == 3 ? Need(UpgradeKind.Ofis, 3) : "",
                category = ExpenseCategory.Kira,
            },
            new UpgradeDef
            {
                kind = UpgradeKind.EFilo, name = "E-bisiklet ve scooter filosu",
                summary = "Şirket elektrikli bisiklet ve scooter kiralar, kuryelere düşük ücretle verir (Wolt ve Foodora'nın kiralık araç " +
                          "anlaşmaları gibi). Bisikletli kuryeler hızlanır; aracı olmayanlar da kaydolabildiği için kayıt artar.",
                costs = new[] { 8000000, 24000000 },
                daily = new[] { 0, 8000, 20000 },
                levelNames = new[] { "Yok", "E-bisiklet kiralama", "+ E-scooter ve batarya değişim istasyonları" },
                effect = lv => lv == 0 ? "her kurye kendi aracıyla"
                             : $"bisiklet kuryeleri +{Pct(EBikeAt(lv))} hızlı" + (lv >= 2 ? $", e-scooter kuryeleri +{Pct(EScooterAt(lv))} hızlı" : "")
                               + $", kurye kaydı +{Pct(FleetAppsAt(lv))}",
                reqDistrict = new[] { 3, 5 },
                extraLock = lv => lv == 2 ? Need(UpgradeKind.Hub, 1) : "",
                category = ExpenseCategory.Ekipman,
            },
            // ---------- Geç oyun ----------
            new UpgradeDef
            {
                kind = UpgradeKind.KargoBisikleti, name = "Kargo bisikletleri",
                summary = "Önünde büyük kasası olan elektrikli kargo bisikletleri (Avrupa şehirlerinde market ve kargo teslimatında yaygın). " +
                          "Şirket alır, bisikletli kuryelere verir: aynı anda 2 paket fazla taşırlar. Çok kuryeli şirkette kurye başına daha çok iş, daha az iptal.",
                costs = new[] { 1600000, 4000000, 9000000 },
                daily = new[] { 0, 2000, 4500, 9000 },
                levelNames = new[] { "Yok", "4 kargo bisikleti", "10 kargo bisikleti", "20 kargo bisikleti" },
                effect = lv => lv == 0 ? "bisikletli kurye kendi bisikletiyle" : $"{KargoBisikletiAt(lv)} bisikletli kurye +2 paket taşır",
                reqDistrict = new[] { 2, 3, 4 },
                category = ExpenseCategory.Ekipman,
            },
            new UpgradeDef
            {
                kind = UpgradeKind.PaketDolabi, name = "Paket dolapları",
                summary = "Mahallelere akıllı paket dolapları kurulur (Foxpost, Packeta gibi). Bazı müşteriler siparişini dolaptan almayı seçer: " +
                          "kurye kata çıkmaz, kapı kodu ve \"evde yok\" derdi olmaz, teslimat hızlanır; müşteri de memnun kalır.",
                costs = new[] { 1200000, 3600000, 8000000 },
                daily = new[] { 0, 1500, 4000, 8000 },
                levelNames = new[] { "Yok", "Merkez'de 5 dolap", "Üç bölgede 15 dolap", "Her bölgede 40 dolap" },
                effect = lv => lv == 0 ? "her sipariş kapıya" : $"kartla ödenen siparişlerin %{DolapAt(lv) * 100f:0}'i dolaba (kat yok, evde yok derdi yok, puan +0,2)",
                reqDistrict = new[] { 2, 3, 5 },
                category = ExpenseCategory.Diger,
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Robot, name = "Teslimat robotları",
                summary = "Kaldırımda giden altı tekerlekli teslimat robotları (Starship gibi). Merkeze yakın dükkanlardan 2 km'ye kadar, kartla ödenen, " +
                          "hafif siparişleri götürür. Kurye ücreti yoktur ama yavaştır (saatte ~5 km) ve fırtınada, karda çalışmaz.",
                costs = new[] { 5000000, 12000000, 24000000 },
                daily = new[] { 0, 4000, 9000, 16000 },
                levelNames = new[] { "Yok", "3 robot", "8 robot", "16 robot" },
                effect = lv => lv == 0 ? "bütün teslimatları kuryeler yapar" : $"{RobotAt(lv)} robot kısa mesafe siparişleri götürür",
                reqDistrict = new[] { 3, 4, 5 },
                extraLock = lv => lv == 1 ? Need(UpgradeKind.Ofis, 2) : "",
                category = ExpenseCategory.Ekipman,
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Kargo, name = "Kargo teslimatı",
                summary = "Yeni iş kolu: bir kargo şirketinin şehir içi paket dağıtımını üstlenirsin. Depona her sabah paketler gelir, " +
                          "09:00–16:00 arasında dağıtıma çıkar, akşam 19:00'a kadar teslim edilmeli. Sıcak yemek gibi acelesi yok: kuryeler " +
                          "boşta kaldıkları sakin saatlerde götürür (yemek siparişlerinin önüne geçmez). Kargo şirketi paket başına mesafeye göre öder; " +
                          "teslim edilemeyen paket için ceza keser. Ağır paketler için arabalı kurye gerekir.",
                costs = new[] { 1200000, 3500000, 9000000 },
                daily = new[] { 0, 8000, 16000, 28000 },
                levelNames = new[] { "Yok", "Kargo anlaşması (küçük depo)", "Bölge kargo deposu", "Şehir kargo merkezi" },
                effect = lv => lv == 0 ? "sadece yemek ve market" : $"günde ~{KargoAnlasmasi.GunlukPaket(lv)} paket",
                reqDistrict = new[] { 2, 3, 4 },
                extraLock = lv => lv == 3 ? Need(UpgradeKind.Ofis, 2) : "",
                category = ExpenseCategory.Gelistirme,
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Isletme, name = "İşletmelere teslimat",
                summary = "Yeni iş kolu (Wolt Drive, Uber Direct gibi): eczane, çiçekçi, kitapçı, butik gibi işletmeler kendi müşterilerine " +
                          "gidecek paketleri senin kuryelerinle gönderir. Komisyon yok: işletme teslimat başına sabit ücret öder " +
                          $"({GameData.Ft(IsletmeTeslimati.Ucret(15))}, 3 km). Siparişler daha çok öğle ve akşam yoğunluğunun dışında gelir, " +
                          "1,5 saat içinde teslim edilmeli; yemek siparişlerinin önüne geçmez, süresi daralınca öne alınır. " +
                          "Geç kalırsan işletmenin memnuniyeti düşer, çok düşerse bir hafta sipariş vermez.",
                costs = new[] { 800000, 2500000, 7000000 },
                daily = new[] { 0, 4000, 9000, 16000 },
                levelNames = new[] { "Yok", "3 iş ortağı", "6 iş ortağı", "10 iş ortağı" },
                effect = lv => lv == 0 ? "sadece yemek, market ve kargo" : $"{IsletmeTeslimati.OrtakSayisi(lv)} iş ortağı · günde ~{IsletmeTeslimati.GunlukTahmin(lv)} teslimat",
                reqDistrict = new[] { 2, 3, 4 },
                category = ExpenseCategory.Gelistirme,
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Akademi, name = "Kurye akademisi",
                summary = "Kuryelere ücretsiz eğitim, mentorluk ve kariyer yolu. Kendini değerli hisseden kurye daha mutlu çalışır: moral hedefi " +
                          "yükselir, teklifleri daha kolay kabul eder, işi bırakması ve rakibe gitmesi zorlaşır. Yeni kuryeler daha yüksek moralle başlar.",
                costs = new[] { 600000, 2500000, 8000000 },
                daily = new[] { 0, 3000, 8000, 18000 },
                levelNames = new[] { "Yok", "Eğitim odası", "Kurye akademisi", "Akademi kampüsü" },
                effect = lv => lv == 0 ? "kurye kendi başına öğrenir"
                             : $"moral +{AkademiMoralAt(lv) * 100f:0} puan, teklif beklentisi −%{(1f - AkademiTeklifAt(lv)) * 100f:0}, bırakma/ayartılma −%{(1f - AkademiAyartmaAt(lv)) * 100f:0}",
                reqDistrict = new[] { 0, 3, 4 },
                reqOffice = new[] { 0, 2, 3 },
                category = ExpenseCategory.Gelistirme,
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Otomasyon, name = "Operasyon otomasyonu",
                summary = "Her gün elle yaptığın ayarları bir yazılım ve küçük bir ekip devralır: yoğun saat primleri, planlı teslimat, kurye " +
                          "kayıtları, mutfak anlaşmaları. Ne yaptığını her sabah bildirir. Sen oyunda değilken de şirket çalışmaya devam eder: " +
                          "oyuna dönünce \"sen yokken\" kazancı gelir (seviye arttıkça daha uzun ve daha verimli).",
                costs = new[] { 150000, 600000, 2000000 },
                daily = new[] { 0, 500, 1500, 4000 },
                levelNames = new[] { "Yok", "Vardiya asistanı", "+ Kayıt asistanı", "+ Operasyon müdürü" },
                effect = lv => (lv == 0 ? "her ayar elle"
                             : lv == 1 ? "yoğun saat primlerini ve planlı teslimatı kendisi açar/kapatır"
                             : lv == 2 ? "primler ve planlı teslimat + kurye kayıtlarını ihtiyaca göre açar/kapatır"
                             : "primler, planlı teslimat, kayıtlar + dükkanda bekleme uzarsa öncelikli hazırlık anlaşması")
                             + $" · oyun kapalıyken kazanç %{Otomasyon.Verim(lv) * 100f:0}, en fazla {Otomasyon.SaatSiniri(lv):0} saat",
                reqDistrict = new[] { 0, 2, 3 },
                category = ExpenseCategory.Gelistirme,
            },
        };

        static float AkademiMoralAt(int lv) => new[] { 0f, 0.04f, 0.08f, 0.12f }[Mathf.Clamp(lv, 0, 3)];
        static float AkademiTeklifAt(int lv) => new[] { 1f, 0.97f, 0.94f, 0.9f }[Mathf.Clamp(lv, 0, 3)];
        static float AkademiAyartmaAt(int lv) => new[] { 1f, 0.8f, 0.6f, 0.45f }[Mathf.Clamp(lv, 0, 3)];
        /// <summary>Kurye akademisi: moral hedefine eklenen.</summary>
        public float AkademiMoralEki => AkademiMoralAt(Level(UpgradeKind.Akademi));
        /// <summary>Kurye akademisi: teklif beklentisi çarpanı.</summary>
        public float AkademiTeklifCarpani => AkademiTeklifAt(Level(UpgradeKind.Akademi));
        /// <summary>Kurye akademisi: işi bırakma ve rakibe gitme ihtimali çarpanı.</summary>
        public float AkademiAyartmaCarpani => AkademiAyartmaAt(Level(UpgradeKind.Akademi));

        public static int KargoBisikletiAt(int lv) => new[] { 0, 4, 10, 20 }[Mathf.Clamp(lv, 0, 3)];
        public static float DolapAt(int lv) => new[] { 0f, 0.1f, 0.2f, 0.3f }[Mathf.Clamp(lv, 0, 3)];
        public static int RobotAt(int lv) => new[] { 0, 3, 8, 16 }[Mathf.Clamp(lv, 0, 3)];

        /// <summary>Kartla ödenen bir siparişin paket dolabına gitme ihtimali.</summary>
        public float DolapOrani => DolapAt(Level(UpgradeKind.PaketDolabi));
        public int RobotSayisi => RobotAt(Level(UpgradeKind.Robot));

        readonly System.Collections.Generic.HashSet<Courier> kargoBisikletliler = new System.Collections.Generic.HashSet<Courier>();
        int kargoKaresi = -1;

        /// <summary>
        /// Bu kurye şirketin kargo bisikletlerinden birini kullanıyor mu? (İlk N bisikletli kurye, işe alınma sırasıyla.)
        /// Atama döngüsünde çok çağrılır: liste karede bir hesaplanır.
        /// </summary>
        public bool KargoBisikletiVar(Courier c)
        {
            int n = KargoBisikletiAt(Level(UpgradeKind.KargoBisikleti));
            if (n <= 0 || c == null || c.vehicle != VehicleKind.Bisiklet) return false;
            if (kargoKaresi != Time.frameCount)
            {
                kargoKaresi = Time.frameCount;
                kargoBisikletliler.Clear();
                foreach (var x in CourierManager.I.Couriers)
                {
                    if (kargoBisikletliler.Count >= n) break;
                    if (x.vehicle == VehicleKind.Bisiklet) kargoBisikletliler.Add(x);
                }
            }
            return kargoBisikletliler.Contains(c);
        }

        // ---------- Seviye etkileri ----------
        static float MoraleAt(int lv) => new[] { 0f, 0.005f, 0.01f, 0.015f, 0.02f }[lv];
        static float PhoneAt(int lv) => new[] { 1f, 0.6f, 0.5f, 0.4f, 0.3f }[lv];
        static float BreakAt(int lv) => new[] { 1f, 1f, 0.7f, 0.55f, 0.4f }[lv];
        static float DirtAt(int lv) => new[] { 1f, 1f, 0.8f }[lv];
        static float CleanAt(int lv) => new[] { 0f, 0.25f, 0.3f }[lv];
        static float QueueAt(int lv) => new[] { 1f, 0.6f, 0.4f, 0.25f }[lv];
        static float MissingAt(int lv) => new[] { 1f, 1f, 0.5f, 0.35f }[lv];
        static float WrongAddrAt(int lv) => new[] { 1f, 0.5f, 0.4f, 0.3f }[lv];
        static float NotHomeAt(int lv) => new[] { 1f, 1f, 0.5f, 0.3f }[lv];
        static int SupportCapAt(int lv) => new[] { 2, 4, 7, 12, 18 }[lv];
        static float OrdersAt(int lv) => new[] { 1f, 1.05f, 1.10f, 1.15f, 1.22f }[lv];
        static float AdAppsAt(int lv) => new[] { 1f, 1.15f, 1.3f, 1.45f, 1.6f }[lv];
        static float OfficeAppsAt(int lv) => new[] { 1f, 1.05f, 1.10f, 1.15f, 1.25f }[lv];
        static float HubFatigueAt(int lv) => new[] { 1f, 0.9f, 0.8f, 0.7f }[lv];
        static float HubOnlineAt(int lv) => new[] { 1f, 1.05f, 1.10f, 1.15f }[lv];
        static float EBikeAt(int lv) => new[] { 1f, 1.15f, 1.25f }[lv];
        static float EScooterAt(int lv) => new[] { 1f, 1f, 1.1f }[lv];
        static float FleetAppsAt(int lv) => new[] { 1f, 1.15f, 1.3f }[lv];

        readonly int[] levels = new int[System.Enum.GetValues(typeof(UpgradeKind)).Length];

        void Awake() { I = this; }

        void Start() { GameClock.I.OnNewDay += OnNewDay; }

        public static UpgradeDef Def(UpgradeKind k)
        {
            foreach (var d in Defs)
                if (d.kind == k) return d;
            return null;
        }
        public int Level(UpgradeKind k) => levels[(int)k];
        public bool Has(UpgradeKind k) => levels[(int)k] > 0;

        /// <summary>Doğrudan seviye ver (yeni oyunda marka bonusu: hazır sistemler). Bedel alınmaz.</summary>
        public void SetLevel(UpgradeKind k, int lv)
        {
            var d = Def(k);
            levels[(int)k] = Mathf.Clamp(lv, 0, d != null ? d.MaxLevel : 0);
        }

        // ---------- Kayıt ----------
        public int[] CaptureLevels() => (int[])levels.Clone();

        public void RestoreLevels(int[] saved)
        {
            if (saved == null) return;
            for (int i = 0; i < levels.Length && i < saved.Length; i++)
            {
                var d = Def((UpgradeKind)i);
                levels[i] = Mathf.Clamp(saved[i], 0, d != null ? d.MaxLevel : 0);
            }
        }

        // ---------- Platform ----------
        /// <summary>Kurye hesabı sınırı: platform paketi + filo merkezleri (u68, sınırsız).</summary>
        public int CourierCap => AccountCap[Level(UpgradeKind.Platform)] + FiloMerkezi.EkHesap;
        /// <summary>Sıradaki platform paketinin eklediği hesap (son pakette 0).</summary>
        public int PlatformEkHesap
        {
            get { int lv = Level(UpgradeKind.Platform); return lv + 1 < AccountCap.Length ? AccountCap[lv + 1] - AccountCap[lv] : 0; }
        }
        public int PlatformFeePerDay => Def(UpgradeKind.Platform).DailyAt(Level(UpgradeKind.Platform));
        public string PlatformName => Def(UpgradeKind.Platform).LevelText(Level(UpgradeKind.Platform));
        public bool CourierCapFull => CourierManager.I.Couriers.Count >= CourierCap;

        /// <summary>
        /// Kurye hesapları dolunca oyuncuya ne yapması gerektiğini söyler. Paket bir bölge istiyorsa önce onu söyler
        /// (testte bot ve oyuncu "Gelişim'den yükselt" deyip kilitli pakete takılıyordu).
        /// </summary>
        public string CourierCapHint
        {
            get
            {
                var d = Def(UpgradeKind.Platform);
                int next = Level(UpgradeKind.Platform) + 1;
                if (next > d.MaxLevel)
                    return $"En büyük platform paketindesin: bir bölgede filo merkezi kur ya da büyüt (İşletme > Bölgeler, {GameData.Ft(FiloMerkezi.Bedel)}, +{FiloMerkezi.HesapPerSeviye} hesap).";
                int dist = d.ReqDistrict(next);
                if (dist > 0 && CityMap.I.UnlockLevel < dist)
                    return $"Önce \"{CityMap.DistrictNames[dist]}\" bölgesini aç (İşletme > Dükkanlar, {GameData.Ft(CityMap.BolgeBedeli(dist))}), sonra Gelişim'den kurye paketini yükselt.";
                string kilit = LockReason(UpgradeKind.Platform);
                if (kilit.Length > 0)
                    return FiloMerkezi.Acik
                        ? $"Kurye paketini yükseltmek için {kilit}. Ya da bir bölgede filo merkezi kur (İşletme > Bölgeler, {GameData.Ft(FiloMerkezi.Bedel)}, +{FiloMerkezi.HesapPerSeviye} hesap)."
                        : $"Kurye paketini yükseltmek için {kilit}.";
                return $"Gelişim'den kurye paketini yükselt ({GameData.Ft(NextCost(UpgradeKind.Platform))}) ya da bir kuryeyle yollarını ayır.";
            }
        }

        // ---------- Satın alma / küçültme ----------

        /// <summary>Sonraki seviye neden alınamıyor (boşsa alınabilir).</summary>
        public string LockReason(UpgradeKind k)
        {
            var d = Def(k);
            int next = Level(k) + 1;
            if (next > d.MaxLevel) return "";
            int dist = d.ReqDistrict(next);
            if (dist > 0 && CityMap.I.UnlockLevel < dist) return $"önce \"{CityMap.DistrictNames[dist]}\" bölgesini aç";
            int off = d.ReqOffice(next);
            if (off > 0 && Level(UpgradeKind.Ofis) < off) return $"önce merkez ofisi büyüt ({Def(UpgradeKind.Ofis).levelNames[off]})";
            string agac = GelisimAgaci.UstKilidi(k, next);   // gelişim ağacı: ilk seviye ancak üst düğüm alınınca
            if (agac.Length > 0) return agac;
            if (d.extraLock != null) return d.extraLock(next) ?? "";
            return "";
        }

        public bool IsLocked(UpgradeKind k) => LockReason(k).Length > 0;

        public int NextCost(UpgradeKind k)
        {
            var d = Def(k);
            int lv = Level(k);
            // Şirket büyüdükçe kurulum bedeli artar. Kurye platformu paketi çarpanın yarısını alır: 15.11 testinde (işletme giderleri
            // ve geçici vergiyle) kurye sayısı 30 gün 32'de kaldı, 45 hesaplık paket 9 000–9 900 €'ya çıkmıştı; kayıplar ve geç teslim arttı.
            // Metropol tabanı da 1,6 M'dan 1,1 M'ya indi (çarpan 2,75'te ~5 100 €).
            if (lv >= d.MaxLevel) return -1;
            long bedel = Denge.Olcekle(d.costs[lv], k == UpgradeKind.Platform ? 0.5f : 1f);
            return (int)(Mathf.Round(bedel * Marka.GelistirmeCarpani / 1000f) * 1000f);   // marka: deneyimli yönetim indirimi
        }

        public bool Buy(UpgradeKind k)
        {
            var d = Def(k);
            int lv = Level(k);
            if (lv >= d.MaxLevel || IsLocked(k)) return false;
            if (!EconomyManager.I.TrySpendYatirim(NextCost(k), $"Geliştirme: {d.name} → {d.levelNames[lv + 1]}", ExpenseCategory.Gelistirme)) return false;
            levels[(int)k] = lv + 1;
            int dailyNow = d.DailyAt(lv + 1);
            EventManager.I.Notify($"{d.name} tamamlandı: {d.LevelText(lv + 1)}" + (dailyNow > 0 ? $" (günlük gider {Ft(dailyNow)})." : "."));
            return true;
        }

        /// <summary>Küçültme neden yapılamıyor (boşsa yapılabilir).</summary>
        public string DowngradeBlock(UpgradeKind k)
        {
            var d = Def(k);
            int lv = Level(k);
            if (lv <= 0 || !d.canDowngrade) return "küçültülemez";
            if (k == UpgradeKind.Platform && CourierManager.I.Couriers.Count > AccountCap[lv - 1] + FiloMerkezi.EkHesap)
                return $"önce kurye sayısını {AccountCap[lv - 1] + FiloMerkezi.EkHesap}'e indir";
            if (k == UpgradeKind.Ofis && SupportManager.I != null && SupportManager.I.Agents.Count > SupportCapAt(lv - 1))
                return $"önce destek ekibini {SupportCapAt(lv - 1)} kişiye indir";
            if (k == UpgradeKind.Ofis)
                foreach (var o in Defs)
                    if (o.kind != UpgradeKind.Ofis && Level(o.kind) > 0 && o.ReqOffice(Level(o.kind)) >= lv)
                        return $"{o.name} bu ofiste duruyor";
            // Başka bir geliştirmenin mevcut seviyesi buna dayanıyorsa küçültülemez (ör. büyük platform paketi hub'a dayanır)
            levels[(int)k] = lv - 1;
            string dep = "";
            foreach (var o in Defs)
                if (o.kind != k && o.extraLock != null && Level(o.kind) > 0 && !string.IsNullOrEmpty(o.extraLock(Level(o.kind))))
                { dep = $"{o.name} ({o.levelNames[Level(o.kind)]}) buna dayanıyor"; break; }
            levels[(int)k] = lv;
            if (dep.Length > 0) return dep;
            return "";
        }

        public bool Downgrade(UpgradeKind k)
        {
            if (DowngradeBlock(k).Length > 0) return false;
            var d = Def(k);
            levels[(int)k]--;
            EventManager.I.Notify($"{d.name} küçültüldü: {d.LevelText(Level(k))}. Günlük gider artık {Ft(d.DailyAt(Level(k)))}.");
            return true;
        }

        // ---------- Günlük giderler ----------

        /// <summary>Bütün geliştirmelerin bugünkü günlük gideri (ofis kirası dahil).</summary>
        public int TotalDailyCost
        {
            get
            {
                int sum = 0;
                foreach (var d in Defs) sum += DailyCost(d.kind);
                return sum + (int)FiloMerkezi.GunlukGider;
            }
        }

        public int DailyCost(UpgradeKind k)
        {
            var d = Def(k);
            int lv = Level(k);
            // Ofisin başlangıç kirası ekonomi ayarından gelir
            if (k == UpgradeKind.Ofis && lv == 0 && EconomyManager.I != null) return EconomyManager.I.officeRentPerDay;
            return d.DailyAt(lv);
        }

        /// <summary>Gece yarısı ekonomi çağırır: kira, abonelikler, işletme ve reklam giderleri ayrı satırlarda ödenir.</summary>
        public void ChargeDaily()
        {
            foreach (var d in Defs)
            {
                int c = DailyCost(d.kind);
                if (c <= 0) continue;
                string label = d.kind == UpgradeKind.Ofis ? "Ofis kirası" : d.kind == UpgradeKind.Platform ? "Platform sunucu ve lisans ücreti"
                             : d.kind == UpgradeKind.Reklam ? $"Reklam bütçesi ({d.levelNames[Level(d.kind)]})" : $"{d.name} işletme gideri";
                EconomyManager.I.Spend(c, label, d.category);
            }
            if (FiloMerkezi.GunlukGider > 0)
                EconomyManager.I.Spend(FiloMerkezi.GunlukGider, $"Filo merkezlerinin kirası ({FiloMerkezi.Toplam} seviye)", ExpenseCategory.Kira);
        }

        void OnNewDay()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            float m = MoraleAt(Level(UpgradeKind.Dinlenme));
            if (m > 0f)
                foreach (var c in CourierManager.I.Couriers) c.morale = Mathf.Min(1f, c.morale + m);
        }

        // ---------- Diğer sistemlerin kullandığı çarpanlar ----------
        public float RestMultiplier => 1f;   // kuryeler kendi başına dinlenir (platformun dinlenme alanı yok)
        public bool LoyaltyProgram => Level(UpgradeKind.Dinlenme) >= 3;
        public float ShopQueueMultiplier => QueueAt(Level(UpgradeKind.Restoran));
        public float ShopMissingMultiplier => MissingAt(Level(UpgradeKind.Restoran));
        public float WrongAddressMultiplier => WrongAddrAt(Level(UpgradeKind.Adres));
        public float NotHomeMultiplier => NotHomeAt(Level(UpgradeKind.Adres));
        public int SupportCap => SupportCapAt(Level(UpgradeKind.Ofis));
        public float BreakdownMultiplier => BreakAt(Level(UpgradeKind.Bakim));
        public float PhoneDieMultiplier => PhoneAt(Level(UpgradeKind.Bakim));
        public float DirtMultiplier => DirtAt(Level(UpgradeKind.Hijyen));
        public float HygieneCleanBonus => CleanAt(Level(UpgradeKind.Hijyen));
        public float OrderRateMultiplier => OrdersAt(Level(UpgradeKind.Reklam));
        public float ApplicationMultiplier => AdAppsAt(Level(UpgradeKind.Reklam)) * OfficeAppsAt(Level(UpgradeKind.Ofis)) * FleetAppsAt(Level(UpgradeKind.EFilo))
                                             * (1f + GelisimAgaci.Bonus(BonusTip.Basvuru));
        public float SpeedMultiplier => new[] { 1f, 1.05f, 1.08f, 1.12f }[Level(UpgradeKind.Uygulama)] * (1f + GelisimAgaci.Bonus(BonusTip.Hiz));
        public float OfferThresholdMultiplier => new[] { 1f, 0.9f, 0.85f, 0.8f }[Level(UpgradeKind.Uygulama)];
        public bool SmartDispatch => Level(UpgradeKind.Dispatcher) >= 1;
        public float ApplicationFleetMultiplier => FleetAppsAt(Level(UpgradeKind.EFilo));
        public float HubFatigueMultiplier => HubFatigueAt(Level(UpgradeKind.Hub));
        public float HubOnlineMultiplier => HubOnlineAt(Level(UpgradeKind.Hub));
        public bool InsuranceProgram => Level(UpgradeKind.Dinlenme) >= 4;

        /// <summary>E-filo: bisiklet (ve 2. seviyede e-scooter) kuryeleri hızlanır.</summary>
        public float VehicleSpeedMultiplier(VehicleKind v)
        {
            int lv = Level(UpgradeKind.EFilo);
            if (v == VehicleKind.Bisiklet) return EBikeAt(lv);
            if (v == VehicleKind.EScooter) return EScooterAt(lv);
            return 1f;
        }

        /// <summary>
        /// Talep tahmini (dispatcher 2): yoğun saatlerde (11–14, 17–21) kuryeler daha çok çevrimiçi olur.
        /// Dispatcher 3: yoğun saatlerde +%20, ayrıca yağmur/kar/fırtınada her saatte +%20.
        /// </summary>
        public float PeakAvailabilityMultiplier(int hour)
        {
            int lv = Level(UpgradeKind.Dispatcher);
            if (lv < 2) return 1f;
            bool peak = (hour >= 11 && hour < 14) || (hour >= 17 && hour < 21);
            float m = peak ? (lv >= 3 ? 1.2f : 1.15f) : 1f;
            if (lv >= 3 && WeatherSystem.I != null && (WeatherSystem.I.IsRainy || WeatherSystem.I.Current == WeatherKind.Karli)) m *= 1.2f;
            return m;
        }
    }
}
