using UnityEngine;

namespace KuryeTycoon
{
    public enum UpgradeKind { Platform, Dispatcher, Dinlenme, Bakim, Hijyen, Reklam, Uygulama }

    public class UpgradeDef
    {
        public UpgradeKind kind;
        public string name;
        public string summary;
        public int[] costs;          // seviye başına fiyat (costs.Length = en yüksek seviye)
        public string[] levelText;   // her seviyenin etkisi (levelText[0] = başlangıç durumu)
        /// <summary>Metinde para tutarı varsa: her gösterimde yeniden yazılır (Ft/€ ayarı anında yansır).</summary>
        public System.Func<int, string> levelTextFn;
        public int requiresDistrict; // gereken bölge seviyesi (0 = yok)

        public int MaxLevel => costs.Length;
        public string LevelText(int lv) => levelTextFn != null ? levelTextFn(lv) : levelText[lv];
    }

    /// <summary>
    /// Kalıcı şirket geliştirmeleri (Taksimetre'deki garaj geliştirmeleri gibi).
    /// En önemlisi platform paketi: kuryeler serbest çalışır ama her biri uygulamada bir hesap ister;
    /// paket kaç aktif kurye hesabı olabileceğini ve günlük sunucu/lisans ücretini belirler.
    /// </summary>
    public class UpgradeManager : MonoBehaviour
    {
        public static UpgradeManager I { get; private set; }

        // Hesap sınırları kabaca açık dükkanların talebine göre ayarlı; başlangıç bilerek biraz dar:
        // 4 = başlangıçtaki 2 dükkan (akşam zirvesinde zorlanır), 8 = 3–4 dükkan, 14 = 1. bölge dolu,
        // 22 = 2. bölge dolu, 32 = tüm şehir. Seviye açıklamaları bu dizilerden otomatik yazılır.
        static readonly int[] AccountCap = { 4, 8, 14, 22, 32 };
        static readonly int[] PlatformFee = { 0, 1500, 3000, 5000, 8000 };
        static readonly string[] PlatformLevelNames = { "Başlangıç paketi", "Büyüme paketi", "Profesyonel paket", "Şehir paketi", "Kurumsal paket" };

        static string PlatformLevelText(int lv) =>
            $"{PlatformLevelNames[lv]}: {AccountCap[lv]} kurye hesabı, " +
            (PlatformFee[lv] == 0 ? "ücretsiz" : $"sunucu {Ft(PlatformFee[lv])}/gün");

        public static readonly UpgradeDef[] Defs =
        {
            new UpgradeDef
            {
                kind = UpgradeKind.Platform, name = "Kurye platformu paketi",
                summary = "Kuryeler kendi işinin patronu ama her biri uygulamanda bir hesapla çalışır. Paket kaç aktif kurye " +
                          "hesabını kaldırabileceğini belirler; büyük paketin sunucu ve lisans ücreti de yüksek.",
                costs = new[] { 80000, 200000, 450000, 900000 },
                levelTextFn = PlatformLevelText
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Dispatcher, name = "Akıllı dispatcher yazılımı",
                summary = "Otomatik atama siparişi önce kabul etmesi muhtemel kuryelere verir; kâr öncelikli atama açılır.",
                costs = new[] { 90000 },
                levelText = new[] { "Basit atama: en yakın kurye", "Akıllı atama: daha az red, kâr öncelikli atama seçeneği" }
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Uygulama, name = "Kurye uygulaması 2.0",
                summary = "Daha iyi rota ve kazanç tahmini: kuryeler %5 hızlı, teklifleri daha kolay kabul eder.",
                costs = new[] { 110000 },
                levelText = new[] { "Eski uygulama", "Kuryeler %5 hızlı, teklif beklentisi %10 düşük" },
                requiresDistrict = 2
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Dinlenme, name = "Kurye dinlenme alanı",
                summary = "Merkezde çay, şarj ve oturma alanı: kuryeler 2 kat hızlı dinlenir, morali her gün artar.",
                costs = new[] { 120000 },
                levelText = new[] { "Dinlenme alanı yok", "2 kat hızlı dinlenme, her gün moral +" },
                requiresDistrict = 2
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Bakim, name = "Bakım ve şarj istasyonu",
                summary = "Araç arızaları yarıya iner, kuryelerin telefonu daha az kapanır.",
                costs = new[] { 90000 },
                levelText = new[] { "İstasyon yok", "Arıza riski −%50, telefon kapanması −%50" }
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Hijyen, name = "Hijyen istasyonu",
                summary = "Çantalar daha yavaş kirlenir ve her Pazartesi merkezde ücretsiz temizlenir.",
                costs = new[] { 60000 },
                levelText = new[] { "İstasyon yok", "Kirlenme −%30, her Pazartesi ücretsiz temizlik" }
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Reklam, name = "Marka bilinirliği kampanyası",
                summary = "Şehirde reklam panoları: daha çok sipariş ve daha sık kurye başvurusu.",
                costs = new[] { 150000 },
                levelText = new[] { "Reklam yok", "Sipariş +%15, kurye başvurusu +%30" },
                requiresDistrict = 2
            },
        };

        readonly int[] levels = new int[System.Enum.GetValues(typeof(UpgradeKind)).Length];

        static string Ft(long v) => GameData.Ft(v);

        void Awake() { I = this; }

        void Start() { GameClock.I.OnNewDay += OnNewDay; }

        /// <summary>
        /// Geliştirmeyi türüne göre bulur. Defs dizisi ekrandaki sıraya göre dizili ve enum sırasıyla aynı değil,
        /// o yüzden Defs[(int)k] yanlış geliştirmeyi (yanlış fiyat ve isimle) döndürürdü.
        /// </summary>
        public static UpgradeDef Def(UpgradeKind k)
        {
            foreach (var d in Defs)
                if (d.kind == k) return d;
            return null;
        }
        public int Level(UpgradeKind k) => levels[(int)k];
        public bool Has(UpgradeKind k) => levels[(int)k] > 0;

        public int CourierCap => AccountCap[Level(UpgradeKind.Platform)];
        public int PlatformFeePerDay => PlatformFee[Level(UpgradeKind.Platform)];
        public string PlatformName => Def(UpgradeKind.Platform).LevelText(Level(UpgradeKind.Platform));
        public bool CourierCapFull => CourierManager.I.Couriers.Count >= CourierCap;

        /// <summary>Gereken bölge henüz açılmadıysa geliştirme satın alınamaz.</summary>
        public bool IsLocked(UpgradeKind k) => CityMap.I.UnlockLevel < Def(k).requiresDistrict;

        public int NextCost(UpgradeKind k)
        {
            var d = Def(k);
            int lv = Level(k);
            return lv >= d.MaxLevel ? -1 : d.costs[lv];
        }

        public bool Buy(UpgradeKind k)
        {
            var d = Def(k);
            int lv = Level(k);
            if (lv >= d.MaxLevel || IsLocked(k)) return false;
            if (!EconomyManager.I.TrySpend(d.costs[lv], $"Geliştirme: {d.name} ({lv + 1}. seviye)", ExpenseCategory.Gelistirme)) return false;
            levels[(int)k] = lv + 1;
            EventManager.I.Notify($"{d.name} tamamlandı: {d.LevelText(lv + 1)}");
            return true;
        }

        void OnNewDay()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;

            if (Has(UpgradeKind.Dinlenme))
                foreach (var c in CourierManager.I.Couriers) c.morale = Mathf.Min(1f, c.morale + 0.01f);

            if (Has(UpgradeKind.Hijyen) && GameClock.I.DayOfWeek == 0)
            {
                foreach (var c in CourierManager.I.Couriers) c.CleanBag(true);
                EventManager.I.Notify("Hijyen istasyonu: tüm kuryelerin çantası ücretsiz temizlendi.");
            }
        }

        // ---------- Diğer sistemlerin kullandığı çarpanlar ----------
        public float RestMultiplier => Has(UpgradeKind.Dinlenme) ? 2f : 1f;
        public float BreakdownMultiplier => Has(UpgradeKind.Bakim) ? 0.5f : 1f;
        public float PhoneDieMultiplier => Has(UpgradeKind.Bakim) ? 0.5f : 1f;
        public float DirtMultiplier => Has(UpgradeKind.Hijyen) ? 0.7f : 1f;
        public float OrderRateMultiplier => Has(UpgradeKind.Reklam) ? 1.15f : 1f;
        public float ApplicationMultiplier => Has(UpgradeKind.Reklam) ? 1.3f : 1f;
        public float SpeedMultiplier => Has(UpgradeKind.Uygulama) ? 1.05f : 1f;
        public float OfferThresholdMultiplier => Has(UpgradeKind.Uygulama) ? 0.9f : 1f;
        public bool SmartDispatch => Has(UpgradeKind.Dispatcher);
    }
}
