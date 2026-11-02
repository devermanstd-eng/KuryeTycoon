using UnityEngine;

namespace KuryeTycoon
{
    public enum UpgradeKind { Ofis, Dispatcher, Dinlenme, Bakim, Hijyen, Reklam, Uygulama }

    public class UpgradeDef
    {
        public UpgradeKind kind;
        public string name;
        public string summary;
        public int[] costs;          // seviye başına fiyat (costs.Length = en yüksek seviye)
        public string[] levelText;   // her seviyenin etkisi (levelText[0] = başlangıç durumu)
        public int requiresDistrict; // gereken bölge seviyesi (0 = yok)

        public int MaxLevel => costs.Length;
    }

    /// <summary>
    /// Kalıcı şirket geliştirmeleri (Taksimetre'deki garaj geliştirmeleri gibi).
    /// En önemlisi ofis: kaç kurye çalıştırabileceğini ve günlük kirayı belirler.
    /// </summary>
    public class UpgradeManager : MonoBehaviour
    {
        public static UpgradeManager I { get; private set; }

        static readonly int[] OfficeCap = { 4, 8, 14, 22, 32 };
        static readonly int[] OfficeRent = { 4000, 6000, 9000, 13000, 18000 };

        public static readonly UpgradeDef[] Defs =
        {
            new UpgradeDef
            {
                kind = UpgradeKind.Ofis, name = "Ofis büyütme",
                summary = "Kaç kurye çalıştırabileceğini belirler. Büyük ofisin kirası da yüksek.",
                costs = new[] { 80000, 200000, 450000, 900000 },
                levelText = new[]
                {
                    "Küçük oda: 4 kurye, kira 4 000 Ft/gün",
                    "Dükkan ofis: 8 kurye, kira 6 000 Ft/gün",
                    "İki katlı ofis: 14 kurye, kira 9 000 Ft/gün",
                    "Kurye merkezi: 22 kurye, kira 13 000 Ft/gün",
                    "Bölge merkezi: 32 kurye, kira 18 000 Ft/gün"
                }
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
                levelText = new[] { "Eski uygulama", "Kuryeler %5 hızlı, teklif beklentisi %10 düşük" }
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Dinlenme, name = "Kurye dinlenme alanı",
                summary = "Merkezde çay, şarj ve oturma alanı: kuryeler 2 kat hızlı dinlenir, morali her gün artar.",
                costs = new[] { 120000 },
                levelText = new[] { "Dinlenme alanı yok", "2 kat hızlı dinlenme, her gün moral +" }
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
                levelText = new[] { "Reklam yok", "Sipariş +%15, kurye başvurusu +%30" }
            },
        };

        readonly int[] levels = new int[System.Enum.GetValues(typeof(UpgradeKind)).Length];

        static string Ft(long v) => GameData.Ft(v);

        void Awake() { I = this; }

        void Start() { GameClock.I.OnNewDay += OnNewDay; }

        public static UpgradeDef Def(UpgradeKind k) => Defs[(int)k];
        public int Level(UpgradeKind k) => levels[(int)k];
        public bool Has(UpgradeKind k) => levels[(int)k] > 0;

        public int CourierCap => OfficeCap[Level(UpgradeKind.Ofis)];
        public int RentPerDay => OfficeRent[Level(UpgradeKind.Ofis)];
        public bool CourierCapFull => CourierManager.I.Couriers.Count >= CourierCap;

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
            if (lv >= d.MaxLevel) return false;
            if (!EconomyManager.I.TrySpend(d.costs[lv], $"Geliştirme: {d.name} ({lv + 1}. seviye)", ExpenseCategory.Gelistirme)) return false;
            levels[(int)k] = lv + 1;
            EventManager.I.Notify($"{d.name} tamamlandı: {d.levelText[lv + 1]}");
            if (k == UpgradeKind.Ofis) CityMap.I.RefreshColors();
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
