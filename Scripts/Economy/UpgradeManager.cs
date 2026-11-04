using UnityEngine;

namespace KuryeTycoon
{
    // Sıra kayıt uyumluluğu için sabit: yeni türler sona eklenir
    public enum UpgradeKind { Platform, Dispatcher, Dinlenme, Bakim, Hijyen, Reklam, Uygulama, Ofis }

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
        public ExpenseCategory category = ExpenseCategory.Gelistirme;
        public bool canDowngrade = true;

        public int MaxLevel => costs.Length;
        public string LevelText(int lv) => $"{levelNames[lv]}: {effect(lv)}";
        public int DailyAt(int lv) => daily != null && lv < daily.Length ? daily[lv] : 0;
        public int ReqDistrict(int targetLevel) => reqDistrict != null && targetLevel - 1 < reqDistrict.Length ? reqDistrict[targetLevel - 1] : 0;
        public int ReqOffice(int targetLevel) => reqOffice != null && targetLevel - 1 < reqOffice.Length ? reqOffice[targetLevel - 1] : 0;
    }

    /// <summary>
    /// Kalıcı şirket geliştirmeleri. Her birinin seviyeleri var; her seviyenin bir kurulum bedeli ve günlük işletme gideri olur
    /// (yazılım aboneliği, kira, temizlik malzemesi, reklam bütçesi…). Gerçekçi kurallar:
    ///  • Platform paketi kaç aktif kurye hesabı olabileceğini belirler; büyük paket ancak şehir büyüyünce (bölge açılınca) alınabilir.
    ///  • Dinlenme alanı, hijyen istasyonu ve bakım atölyesi fiziksel yer ister: önce merkez ofisi büyütmek gerekir.
    ///  • Büyük ofis daha çok kira demek; reklam her gün bütçe harcar. İşletme gideri olan geliştirmeler küçültülebilir
    ///    (kurulum bedeli geri gelmez).
    /// Tüm giderler vergiden düşülür.
    /// </summary>
    public class UpgradeManager : MonoBehaviour
    {
        public static UpgradeManager I { get; private set; }

        // Hesap sınırları açık bölgelerin talebine göre: 4 = başlangıç, 14 = Merkez dolu, 22 = Doğu, 32 = Sanayi, 45 = Batı Liman, 60–80 = bütün şehir
        static readonly int[] AccountCap = { 4, 8, 14, 22, 32, 45, 60, 80 };

        static string Ft(long v) => GameData.Ft(v);
        static string Pct(float m) => $"%{Mathf.Abs(m - 1f) * 100f:0}";

        public static readonly UpgradeDef[] Defs =
        {
            new UpgradeDef
            {
                kind = UpgradeKind.Platform, name = "Kurye platformu paketi",
                summary = "Kuryeler kendi işinin patronu ama her biri uygulamanda bir hesapla çalışır. Paket kaç aktif kurye hesabını " +
                          "kaldırabileceğini belirler; büyük paketin sunucu ve lisans ücreti de yüksek. Büyük paketler şehir büyüdükçe açılır.",
                costs = new[] { 80000, 200000, 450000, 900000, 1600000, 2800000, 4500000 },
                daily = new[] { 0, 1500, 3000, 5000, 8000, 12000, 17000, 24000 },
                levelNames = new[] { "Başlangıç", "Büyüme", "Profesyonel", "Bölgesel", "Şehir", "Metropol", "Kurumsal", "Kurumsal+" },
                effect = lv => $"{AccountCap[lv]} kurye hesabı",
                reqDistrict = new[] { 0, 0, 2, 3, 4, 5, 5 },
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Ofis, name = "Merkez ofis",
                summary = "Şirketin fiziksel yeri. Dinlenme alanı, hijyen istasyonu ve bakım atölyesi için yer gerekir. " +
                          "Büyük ofis daha görünür (daha çok kurye başvurusu) ama kirası da yüksek.",
                costs = new[] { 250000, 700000, 1800000 },
                daily = new[] { 4000, 7000, 12000, 20000 },
                levelNames = new[] { "Kiralık küçük ofis", "Sokak katında ofis", "Kat ofisi", "Şirket binası" },
                effect = lv => lv == 0 ? "sadece masa ve depo" : lv == 1 ? "dinlenme ve temizlik köşesine yer var, başvuru +%5"
                             : lv == 2 ? "atölye ve salon için yer, başvuru +%10" : "her şeye yer, başvuru +%15",
                reqDistrict = new[] { 0, 2, 3 },
                category = ExpenseCategory.Kira,
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Dispatcher, name = "Dispatcher yazılımı",
                summary = "Siparişlerin kuryelere nasıl dağıtılacağı. Akıllı atama siparişi önce kabul etmesi muhtemel kuryelere verir; " +
                          "talep tahmini yoğun saatten önce kuryelere haber verir.",
                costs = new[] { 90000, 350000 },
                daily = new[] { 0, 800, 2000 },
                levelNames = new[] { "Basit atama", "Akıllı atama", "Akıllı atama + talep tahmini" },
                effect = lv => lv == 0 ? "en yakın kuryeye teklif" : lv == 1 ? "daha az red, kâr öncelikli atama seçeneği"
                             : "daha az red, yoğun saatlerde +%15 çevrimiçi kurye",
                reqDistrict = new[] { 0, 2 },
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Uygulama, name = "Kurye uygulaması",
                summary = "Rota, navigasyon ve kazanç tahmini. İyi uygulamada kuryeler daha hızlı gider ve teklifleri daha kolay kabul eder.",
                costs = new[] { 110000, 400000 },
                daily = new[] { 0, 600, 1500 },
                levelNames = new[] { "Eski uygulama", "Uygulama 2.0", "Uygulama 3.0 (canlı trafik)" },
                effect = lv => lv == 0 ? "standart" : lv == 1 ? "kuryeler %5 hızlı, teklif beklentisi %10 düşük"
                             : "kuryeler %8 hızlı, teklif beklentisi %15 düşük",
                reqDistrict = new[] { 0, 3 },
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Dinlenme, name = "Kurye dinlenme alanı",
                summary = "Merkezde kuryelerin mola verdiği yer: daha hızlı dinlenirler, morali her gün biraz artar.",
                costs = new[] { 60000, 150000, 400000 },
                daily = new[] { 0, 300, 800, 1800 },
                levelNames = new[] { "Yok", "Çay köşesi", "Dinlenme salonu", "Kurye lounge (duş, mutfak)" },
                effect = lv => lv == 0 ? "kuryeler evde / arabada dinlenir"
                             : $"{RestAt(lv):0.#} kat hızlı dinlenme, her gün moral +{MoraleAt(lv) * 100f:0.#}",
                reqOffice = new[] { 1, 1, 2 },
                reqDistrict = new[] { 0, 2, 0 },
                category = ExpenseCategory.Diger,
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Bakim, name = "Bakım ve şarj",
                summary = "Telefon ve araç sorunları: şarj noktası telefon kapanmasını, atölye ve anlaşmalı servis araç arızalarını azaltır.",
                costs = new[] { 40000, 250000, 500000 },
                daily = new[] { 0, 200, 1000, 2000 },
                levelNames = new[] { "Yok", "Şarj noktası", "Bakım atölyesi", "Atölye + anlaşmalı servis" },
                effect = lv => lv == 0 ? "standart arıza ve telefon riski"
                             : $"telefon kapanması −{Pct(PhoneAt(lv))}" + (lv >= 2 ? $", araç arızası −{Pct(BreakAt(lv))}" : ""),
                reqOffice = new[] { 0, 2, 2 },
                reqDistrict = new[] { 0, 0, 3 },
                category = ExpenseCategory.Diger,
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Hijyen, name = "Hijyen istasyonu",
                summary = "Çanta temizliği kuryenin sorumluluğu; istasyon bunu kolaylaştırır: çantalar daha yavaş kirlenir, kuryeler daha sık temizler.",
                costs = new[] { 40000, 150000 },
                daily = new[] { 0, 200, 500 },
                levelNames = new[] { "Yok", "Temizlik köşesi", "Çanta yıkama istasyonu" },
                effect = lv => lv == 0 ? "kuryeler evde temizler" : $"kirlenme −{Pct(DirtAt(lv))}, gün içinde daha sık temizlik",
                reqOffice = new[] { 1, 1 },
                category = ExpenseCategory.Diger,
            },
            new UpgradeDef
            {
                kind = UpgradeKind.Reklam, name = "Reklam ve marka bilinirliği",
                summary = "Reklam kalıcı değildir: her gün bütçe harcar. Daha çok sipariş ve kurye başvurusu getirir. İstediğin zaman küçültebilirsin.",
                costs = new[] { 50000, 150000, 300000 },
                daily = new[] { 0, 2000, 6000, 12000 },
                levelNames = new[] { "Reklam yok", "Sosyal medya", "Şehir panoları", "Radyo + influencer" },
                effect = lv => lv == 0 ? "ağızdan ağıza" : $"sipariş +{Pct(OrdersAt(lv))}, kurye başvurusu +{Pct(AdAppsAt(lv))}",
                reqDistrict = new[] { 0, 2, 3 },
                category = ExpenseCategory.Reklam,
            },
        };

        // ---------- Seviye etkileri ----------
        static float RestAt(int lv) => new[] { 1f, 1.5f, 2f, 2.5f }[lv];
        static float MoraleAt(int lv) => new[] { 0f, 0.005f, 0.01f, 0.015f }[lv];
        static float PhoneAt(int lv) => new[] { 1f, 0.5f, 0.4f, 0.3f }[lv];
        static float BreakAt(int lv) => new[] { 1f, 1f, 0.6f, 0.4f }[lv];
        static float DirtAt(int lv) => new[] { 1f, 0.8f, 0.65f }[lv];
        static float CleanAt(int lv) => new[] { 0f, 0.2f, 0.35f }[lv];
        static float OrdersAt(int lv) => new[] { 1f, 1.05f, 1.10f, 1.15f }[lv];
        static float AdAppsAt(int lv) => new[] { 1f, 1.15f, 1.3f, 1.45f }[lv];
        static float OfficeAppsAt(int lv) => new[] { 1f, 1.05f, 1.10f, 1.15f }[lv];

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
        public int CourierCap => AccountCap[Level(UpgradeKind.Platform)];
        public int PlatformFeePerDay => Def(UpgradeKind.Platform).DailyAt(Level(UpgradeKind.Platform));
        public string PlatformName => Def(UpgradeKind.Platform).LevelText(Level(UpgradeKind.Platform));
        public bool CourierCapFull => CourierManager.I.Couriers.Count >= CourierCap;

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
            return "";
        }

        public bool IsLocked(UpgradeKind k) => LockReason(k).Length > 0;

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
            if (!EconomyManager.I.TrySpend(d.costs[lv], $"Geliştirme: {d.name} → {d.levelNames[lv + 1]}", ExpenseCategory.Gelistirme)) return false;
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
            if (k == UpgradeKind.Platform && CourierManager.I.Couriers.Count > AccountCap[lv - 1])
                return $"önce kurye sayısını {AccountCap[lv - 1]}'e indir";
            if (k == UpgradeKind.Ofis)
                foreach (var o in Defs)
                    if (o.kind != UpgradeKind.Ofis && Level(o.kind) > 0 && o.ReqOffice(Level(o.kind)) >= lv)
                        return $"{o.name} bu ofiste duruyor";
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
                return sum;
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
        }

        void OnNewDay()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            float m = MoraleAt(Level(UpgradeKind.Dinlenme));
            if (m > 0f)
                foreach (var c in CourierManager.I.Couriers) c.morale = Mathf.Min(1f, c.morale + m);
        }

        // ---------- Diğer sistemlerin kullandığı çarpanlar ----------
        public float RestMultiplier => RestAt(Level(UpgradeKind.Dinlenme));
        public float BreakdownMultiplier => BreakAt(Level(UpgradeKind.Bakim));
        public float PhoneDieMultiplier => PhoneAt(Level(UpgradeKind.Bakim));
        public float DirtMultiplier => DirtAt(Level(UpgradeKind.Hijyen));
        public float HygieneCleanBonus => CleanAt(Level(UpgradeKind.Hijyen));
        public float OrderRateMultiplier => OrdersAt(Level(UpgradeKind.Reklam));
        public float ApplicationMultiplier => AdAppsAt(Level(UpgradeKind.Reklam)) * OfficeAppsAt(Level(UpgradeKind.Ofis));
        public float SpeedMultiplier => new[] { 1f, 1.05f, 1.08f }[Level(UpgradeKind.Uygulama)];
        public float OfferThresholdMultiplier => new[] { 1f, 0.9f, 0.85f }[Level(UpgradeKind.Uygulama)];
        public bool SmartDispatch => Level(UpgradeKind.Dispatcher) >= 1;

        /// <summary>Talep tahmini (dispatcher 2): yoğun saatlerde (11–14, 17–21) kuryeler daha çok çevrimiçi olur.</summary>
        public float PeakAvailabilityMultiplier(int hour)
        {
            if (Level(UpgradeKind.Dispatcher) < 2) return 1f;
            return (hour >= 11 && hour < 14) || (hour >= 17 && hour < 21) ? 1.15f : 1f;
        }
    }
}
