using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KuryeTycoon
{
    // =====================================================================
    // Kayıt dosyasının veri yapıları (JsonUtility: [Serializable] + public alanlar)
    // =====================================================================

    [System.Serializable]
    public class PeriodStatsSave
    {
        public long commission, fees, otherIncome, courierPay, boosts, rewards, penalties;
        public long payoutsPaid, cashCollected, loanPaid, depositNet;
        public int cashOrders, deliveries;
        public long[] byCategory;
    }

    [System.Serializable]
    public class PayoutSave
    {
        public int courierIndex;       // Couriers listesindeki sıra (−1: ayrılmış kurye)
        public string courierName;
        public long amount;
        public int payDay;
        public bool early;
    }

    [System.Serializable]
    public class EconomySave
    {
        public long money, taxDebt, depositsHeld;
        // Oyuncunun Ayarlar'dan değiştirdikleri
        public int baseFee, feePerKm, minProfit, minAssignProfit;
        public float bonusMultiplier;
        public bool showDailyReport, showWeeklyReport;
        // Raporlar
        public string lastDailyReport, lastDailyReportTitle, lastWeeklyReport, lastWeeklyReportTitle, lastTaxSummary;
        public PeriodStatsSave year, today, week;
        // Haftalık rapor sayaçları
        public int weekStartDay;
        public int weekBatched, weekCancelled, weekLost, weekHeavyLost, weekCancelledRejected, weekBusyLost;
        public int[] weekCancelByHour;
        public float repAtWeekStart;
        public int insurancePlan, insurancePlanChangeDay;
        public long insuranceIncomeTotal;
        public long cashAtWeekStart, lastWeekNet, cashAtLastReport;
        public List<string> weekDayDates = new List<string>();
        public List<int> weekDayDeliveries = new List<int>();
        public List<long> weekDayNets = new List<long>();
        // Kurye ödemeleri, vergi, acil kredi
        public List<PayoutSave> payouts = new List<PayoutSave>();
        public int nextSurpriseDay;
        public long localTaxPaidThisYear;
        public long bankLoanRemaining, bankLoanDaily;
        public bool emergencyLoanUsed, crisisWarned, bankrupt;
        public int daysUnderLimit;
        public List<string> ledger = new List<string>();
    }

    [System.Serializable]
    public class ShopSave
    {
        public string name;
        public bool contracted, exclusive, rivalContracted, lowSatisfactionWarned;
        public float commissionBonus, prepMultiplier, satisfaction;
        public int ordersTotal, contractStartDay, contractEndDay, monthOrders, lastMonthOrders;
        public int negotiateCooldownUntil, resignBlockedUntil;
    }

    [System.Serializable]
    public class CourierSave
    {
        public string json;                    // Courier'in bütün açık alanları (JsonUtility)
        public List<int> ownedVehicles = new List<int>();
        public List<int> equipment = new List<int>();
        public List<int> refusedKinds = new List<int>();
        public List<float> refusedUntil = new List<float>();
    }

    [System.Serializable]
    public class RivalSave
    {
        public float reputation;
        public int couriers, deliveriesLastWeek, priceWarDays;
        public int deliveriesThisWeek, myDeliveriesThisWeek, myDeliveriesLastWeek;
        public long money;
    }

    [System.Serializable]
    public class LoanSave
    {
        public long principal, totalPaid;
        public int trust, deliveriesMilestone;
        public bool finished, failed;
        public List<Installment> schedule = new List<Installment>();
    }

    [System.Serializable]
    public class OpsSave
    {
        public bool hasManager;
        public OpsPerson manager;
        public bool[] offerKinds;
        public int minDeliveries, monthlyBudget;
        public bool replaceWornBags, warnDirtyBags;
        public long spentThisMonth;
    }

    [System.Serializable]
    public class SaveData
    {
        public int version;
        public string savedAt;                 // gerçek tarih/saat
        public string gameDate;                // oyun içi tarih metni
        public int companyDay;

        // Şirket
        public string companyName;
        public Color primary, secondary;
        public float reputation;

        // Zaman ve harita
        public float totalMinutes;
        public int startDay;
        public int mapSeed;
        public int unlockLevel;
        public List<ShopSave> shops = new List<ShopSave>();
        public int weather;

        // Sistemler
        public EconomySave economy;
        public List<CourierSave> couriers = new List<CourierSave>();
        public int equipmentPayment;
        public bool autoAssign, prioritizeProfit;
        public string campaignJson;
        public int[] upgradeLevels;
        public List<SupportAgent> support = new List<SupportAgent>();
        public int supportHandledThisMonth;
        public OpsSave ops;
        public RivalSave rival;
        public LoanSave loan;
        public List<DayRecord> history = new List<DayRecord>();
        public List<string> milestoneIds = new List<string>();
        public List<string> milestoneDates = new List<string>();
        public List<string> feed = new List<string>();
    }

    // =====================================================================

    /// <summary>
    /// Kayıt sistemi (tek kayıt yuvası, JSON). Oyun her gece gün sonundan sonra, açık ya da sırada bekleyen
    /// olay kartı kalmayınca otomatik kaydeder (bekleyen bir karar kayıtla kaybolmasın diye). ESC menüsünden elle de kaydedilir.
    ///
    /// Bilerek kaydedilmeyenler: yoldaki/bekleyen siparişler (yüklemede iptal olur; gece kaydedildiği için genelde 0–2),
    /// kuryelerin yoldaki konumu (hepsi merkez ofiste başlar), iş başvuruları ve aday listeleri (yeniden gelir),
    /// rakibin haritadaki kuryeleri (yeniden oluşur), hava (yeniden belirlenir). Denge sabitleri (kira, sipariş oranı vb.)
    /// kayda yazılmaz: koddaki güncel değerler kullanılır, böylece denge değişiklikleri eski kayıtlara da yansır.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        public static SaveSystem I { get; private set; }

        public const int Version = 1;
        [Tooltip("Her gece gün sonundan sonra otomatik kaydet")]
        public bool autoSave = true;

        public string LastSaveText { get; private set; } = "";
        bool autosavePending;

        public static string SavePath => Path.Combine(Application.persistentDataPath, "kayit.json");
        static string BackupPath => Path.Combine(Application.persistentDataPath, "kayit_onceki.json");

        static string Ft(long v) => GameData.Ft(v);

        void Awake() { I = this; }

        /// <summary>Oyun başlarken (yeni ya da yüklenmiş) çağrılır.</summary>
        public void Init()
        {
            GameClock.I.OnNewDay += () => autosavePending = true;
        }

        void Update()
        {
            if (!autosavePending || !autoSave || GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            if (!CanSaveNow) return;
            autosavePending = false;
            Save(true);
        }

        /// <summary>Açık ya da sırada bekleyen olay kartı yoksa kaydedilebilir.</summary>
        public static bool CanSaveNow => EventManager.I != null && EventManager.I.Idle;

        public static bool HasSave => File.Exists(SavePath);

        public static SaveData ReadSave()
        {
            try
            {
                if (!HasSave) return null;
                var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                return d != null && d.economy != null ? d : null;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Kayıt okunamadı: {ex.Message}");
                return null;
            }
        }

        public static string Summary(SaveData d) =>
            d == null ? "" : $"{d.companyName} · {d.companyDay}. gün ({d.gameDate}) · kasa {Ft(d.economy.money)} · kaydedildi: {d.savedAt}";

        public bool Save(bool auto)
        {
            if (!CanSaveNow)
            {
                EventManager.I.Notify("Kaydetmek için önce açık kartı kapat.");
                return false;
            }
            try
            {
                string json = JsonUtility.ToJson(Capture(), true);
                Directory.CreateDirectory(Application.persistentDataPath);
                string tmp = SavePath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(SavePath)) File.Copy(SavePath, BackupPath, true);
                File.Copy(tmp, SavePath, true);
                File.Delete(tmp);
                LastSaveText = $"{GameClock.I.DateOf(GameClock.I.Day)} {GameClock.I.Hour:00}:{GameClock.I.Minute:00}";
                if (!auto) EventManager.I.Notify("Oyun kaydedildi.");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Kayıt başarısız: {ex}");
                EventManager.I.Notify($"Kayıt başarısız: {ex.Message}");
                return false;
            }
        }

        // ================================================================== kaydet

        public static PeriodStatsSave ToSave(PeriodStats s)
        {
            if (s == null) return null;
            return new PeriodStatsSave
            {
                commission = s.commission, fees = s.fees, otherIncome = s.otherIncome, courierPay = s.courierPay,
                boosts = s.boosts, rewards = s.rewards, penalties = s.penalties, payoutsPaid = s.payoutsPaid,
                cashCollected = s.cashCollected, loanPaid = s.loanPaid, depositNet = s.depositNet,
                cashOrders = s.cashOrders, deliveries = s.deliveries,
                byCategory = (long[])s.byCategory.Clone()
            };
        }

        public static PeriodStats FromSave(PeriodStatsSave d)
        {
            var s = new PeriodStats();
            if (d == null) return s;
            s.commission = d.commission; s.fees = d.fees; s.otherIncome = d.otherIncome; s.courierPay = d.courierPay;
            s.boosts = d.boosts; s.rewards = d.rewards; s.penalties = d.penalties; s.payoutsPaid = d.payoutsPaid;
            s.cashCollected = d.cashCollected; s.loanPaid = d.loanPaid; s.depositNet = d.depositNet;
            s.cashOrders = d.cashOrders; s.deliveries = d.deliveries;
            if (d.byCategory != null)
                System.Array.Copy(d.byCategory, s.byCategory, Mathf.Min(d.byCategory.Length, s.byCategory.Length));
            return s;
        }

        SaveData Capture()
        {
            var clock = GameClock.I;
            var cm = CourierManager.I;
            var d = new SaveData
            {
                version = Version,
                savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                gameDate = clock.DateText,
                companyDay = clock.CompanyDay,
                companyName = CompanyProfile.Name,
                primary = CompanyProfile.Primary,
                secondary = CompanyProfile.Secondary,
                reputation = CompanyProfile.Reputation,
                totalMinutes = clock.TotalMinutes,
                startDay = clock.StartDay,
                mapSeed = GameBootstrap.I.MapSeed,
                unlockLevel = CityMap.I.UnlockLevel,
                weather = (int)WeatherSystem.I.Current,
                economy = EconomyManager.I.CaptureSave(cm.Couriers),
                equipmentPayment = (int)cm.equipmentPayment,
                autoAssign = OrderManager.I.autoAssign,
                prioritizeProfit = OrderManager.I.prioritizeProfit,
                campaignJson = JsonUtility.ToJson(CampaignManager.I),
                upgradeLevels = UpgradeManager.I.CaptureLevels(),
                supportHandledThisMonth = SupportManager.I.handledThisMonth,
                ops = OperationsManager.I.CaptureSave(),
                rival = RivalCompany.I.CaptureSave(),
                loan = FamilyLoan.I.CaptureSave()
            };

            foreach (var s in CityMap.I.Shops)
                d.shops.Add(new ShopSave
                {
                    name = s.def.name, contracted = s.contracted, exclusive = s.exclusive, rivalContracted = s.rivalContracted,
                    lowSatisfactionWarned = s.lowSatisfactionWarned, commissionBonus = s.commissionBonus,
                    prepMultiplier = s.prepMultiplier, satisfaction = s.satisfaction, ordersTotal = s.ordersTotal,
                    contractStartDay = s.contractStartDay, contractEndDay = s.contractEndDay, monthOrders = s.monthOrders,
                    lastMonthOrders = s.lastMonthOrders, negotiateCooldownUntil = s.negotiateCooldownUntil,
                    resignBlockedUntil = s.resignBlockedUntil
                });

            foreach (var c in cm.Couriers)
            {
                var cs = new CourierSave { json = JsonUtility.ToJson(c) };
                foreach (var v in c.ownedVehicles) cs.ownedVehicles.Add((int)v);
                foreach (var k in c.equipment) cs.equipment.Add((int)k);
                foreach (var kv in c.equipRefusedUntil) { cs.refusedKinds.Add((int)kv.Key); cs.refusedUntil.Add(kv.Value); }
                d.couriers.Add(cs);
            }

            d.support.AddRange(SupportManager.I.Agents);
            if (StatsHistory.I != null) d.history.AddRange(StatsHistory.I.Days);
            if (Milestones.I != null)
            {
                d.milestoneIds = Milestones.I.SaveIds();
                d.milestoneDates = Milestones.I.SaveDates();
            }
            var feed = EventManager.I.Feed;
            for (int i = Mathf.Max(0, feed.Count - 30); i < feed.Count; i++) d.feed.Add(feed[i]);
            return d;
        }

        // ================================================================== yükle

        /// <summary>
        /// Harita ve 3D şehir kurulduktan, sistemler Init edildikten sonra GameBootstrap.LoadGame çağırır.
        /// Sıra önemli: önce dükkanlar ve kuryeler, sonra onlara bağlı ekonomi (kurye ödemeleri).
        /// </summary>
        public static void ApplyAfterInit(SaveData d)
        {
            var cm = CourierManager.I;

            // Harita: bölge ve dükkan sözleşmeleri
            CityMap.I.RestoreUnlockLevel(d.unlockLevel);
            foreach (var ss in d.shops)
            {
                var s = CityMap.I.Shops.Find(x => x.def.name == ss.name);
                if (s == null) continue;
                s.contracted = ss.contracted; s.exclusive = ss.exclusive; s.rivalContracted = ss.rivalContracted;
                s.lowSatisfactionWarned = ss.lowSatisfactionWarned; s.commissionBonus = ss.commissionBonus;
                s.prepMultiplier = ss.prepMultiplier; s.satisfaction = ss.satisfaction; s.ordersTotal = ss.ordersTotal;
                s.contractStartDay = ss.contractStartDay; s.contractEndDay = ss.contractEndDay; s.monthOrders = ss.monthOrders;
                s.lastMonthOrders = ss.lastMonthOrders; s.negotiateCooldownUntil = ss.negotiateCooldownUntil;
                s.resignBlockedUntil = ss.resignBlockedUntil;
            }
            CityMap.I.RefreshColors();

            // Kuryeler (merkez ofiste başlarlar)
            foreach (var cs in d.couriers)
            {
                var c = cm.SpawnRestored();
                JsonUtility.FromJsonOverwrite(cs.json, c);
                c.ownedVehicles.Clear();
                foreach (int v in cs.ownedVehicles) c.ownedVehicles.Add((VehicleKind)v);
                if (c.ownedVehicles.Count == 0) c.ownedVehicles.Add(c.vehicle);
                c.equipment.Clear();
                foreach (int k in cs.equipment) c.equipment.Add((EquipmentKind)k);
                c.equipRefusedUntil.Clear();
                for (int i = 0; i < cs.refusedKinds.Count && i < cs.refusedUntil.Count; i++)
                    c.equipRefusedUntil[(EquipmentKind)cs.refusedKinds[i]] = cs.refusedUntil[i];
                c.AfterLoad();
            }
            cm.equipmentPayment = (EquipmentPayment)Mathf.Clamp(d.equipmentPayment, 0, 2);

            // Ekonomi (kurye ödemeleri kuryelere bağlanır)
            EconomyManager.I.RestoreSave(d.economy, cm.Couriers);

            // Diğer sistemler
            OrderManager.I.autoAssign = d.autoAssign;
            OrderManager.I.prioritizeProfit = d.prioritizeProfit;
            if (!string.IsNullOrEmpty(d.campaignJson)) JsonUtility.FromJsonOverwrite(d.campaignJson, CampaignManager.I);
            UpgradeManager.I.RestoreLevels(d.upgradeLevels);
            SupportManager.I.Agents.Clear();
            if (d.support != null) SupportManager.I.Agents.AddRange(d.support);
            SupportManager.I.handledThisMonth = d.supportHandledThisMonth;
            if (d.ops != null) OperationsManager.I.RestoreSave(d.ops);
            if (d.rival != null) RivalCompany.I.RestoreSave(d.rival);
            WeatherSystem.I.Restore((WeatherKind)d.weather);
            if (StatsHistory.I != null) StatsHistory.I.Restore(d.history);
            if (Milestones.I != null) Milestones.I.Restore(d.milestoneIds, d.milestoneDates);
            if (d.feed != null) EventManager.I.Feed.AddRange(d.feed);
        }
    }
}
