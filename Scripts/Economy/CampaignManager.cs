using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Gün içindeki bir kazanç boostu (kuryeye yüzde ek ödeme).</summary>
    public class BoostWindow
    {
        public int startHour;
        public int endHour;      // dahil değil
        public float percent;    // 0.30 = %30
        public string label;

        public string TimeText => $"{startHour:00}:00–{endHour:00}:00";
    }

    /// <summary>
    /// Kurye kampanyaları — hepsi şirketin kârından ödenir:
    ///  • Hafta sonu sabah boostu (sipariş az olduğu için) ve öğle boostu
    ///  • Hafta içi gün içine serpiştirilmiş rastgele boostlar
    ///  • Haftalık hedef ödülü (X paket teslim et → Y Ft)
    /// Boost saatinde kuryeler sipariş reddetmez ve aynı anda +1 paket alır.
    /// </summary>
    public class CampaignManager : MonoBehaviour
    {
        public static CampaignManager I { get; private set; }

        [Header("Hafta sonu boostları")]
        public bool weekendMorningBoost = true;
        public int weekendMorningStart = 8;
        public int weekendMorningEnd = 10;
        public float weekendMorningPercent = 0.30f;
        public bool weekendLunchBoost = true;
        public int weekendLunchStart = 11;
        public int weekendLunchEnd = 14;
        public float weekendLunchPercent = 0.15f;

        [Header("Hafta içi rastgele boostlar")]
        public bool weekdayRandomBoosts = true;
        [Range(0, 4)] public int weekdayBoostCount = 2;
        public float weekdayMaxPercent = 0.30f;

        [Header("Kötü hava boostu (otomatik)")]
        public bool weatherBoost = true;
        public float coldBoost = 0.05f;
        public float rainBoost = 0.10f;
        public float snowBoost = 0.15f;
        public float stormBoost = 0.20f;

        /// <summary>Şu anki havaya göre otomatik boost (0 = yok). Saat boostuyla toplanmaz, yüksek olan geçerli.</summary>
        public float WeatherBoostPercent
        {
            get
            {
                if (!weatherBoost || WeatherSystem.I == null) return 0f;
                switch (WeatherSystem.I.Current)
                {
                    case WeatherKind.Soguk: return coldBoost;
                    case WeatherKind.Yagmurlu: return rainBoost;
                    case WeatherKind.Karli: return snowBoost;
                    case WeatherKind.Firtina: return stormBoost;
                    default: return 0f;
                }
            }
        }

        [Header("Haftalık hedef")]
        public bool weeklyChallenge = true;
        public int weeklyTarget = 200;
        public int weeklyReward = 25000;

        [Header("Aylık hedef")]
        public bool monthlyChallenge = true;
        public int monthlyTarget = 700;
        public int monthlyReward = 50000;

        public long rewardsPaidThisMonth;
        public int rewardsClaimedThisMonth;

        /// <summary>Hafta içi bugün için planlanan rastgele boostlar.</summary>
        public readonly List<BoostWindow> WeekdayBoosts = new List<BoostWindow>();

        public long boostPaidThisWeek;
        public long rewardsPaidThisWeek;
        public int rewardsClaimedThisWeek;

        bool ready;

        static string Ft(long v) => GameData.Ft(v);

        void Awake() { I = this; }

        public void Init()
        {
            GameClock.I.OnNewDay += OnNewDay;
            GameClock.I.OnNewMonth += StartNewMonth;
            PlanDay();
            ready = true;
        }

        void OnNewDay()
        {
            if (GameClock.I.DayOfWeek == 0) StartNewWeek();
            PlanDay();
        }

        void StartNewMonth()
        {
            foreach (var c in CourierManager.I.Couriers)
            {
                c.monthDeliveries = 0;
                c.monthlyRewardClaimed = false;
                c.earnedMonth = 0;
                c.kmMonth = 0f;
            }
            rewardsPaidThisMonth = 0;
            rewardsClaimedThisMonth = 0;
            if (monthlyChallenge)
                EventManager.I.Notify($"{GameClock.I.MonthName} başladı! Aylık hedef: {monthlyTarget} paket → {Ft(monthlyReward)} ödül.");
        }

        void StartNewWeek()
        {
            foreach (var c in CourierManager.I.Couriers)
            {
                c.lastWeekDeliveries = c.weekDeliveries;
                c.offersSeenLastWeek = c.offersSeenWeek;
                c.offersAcceptedLastWeek = c.offersAcceptedWeek;
                c.offersSeenWeek = c.offersAcceptedWeek = 0;
                c.kmWeek = 0f;
                c.weekDeliveries = 0;
                c.weeklyRewardClaimed = false;
            }
            boostPaidThisWeek = 0;
            rewardsPaidThisWeek = 0;
            rewardsClaimedThisWeek = 0;
            if (weeklyChallenge)
                EventManager.I.Notify($"Yeni hafta! Haftalık hedef: {weeklyTarget} paket → {Ft(weeklyReward)} ödül.");
        }

        /// <summary>Hafta içi günler için rastgele boost saatleri belirler ve duyurur.</summary>
        public void PlanDay()
        {
            WeekdayBoosts.Clear();
            var clock = GameClock.I;

            if (!clock.IsWeekend && weekdayRandomBoosts)
            {
                for (int i = 0; i < weekdayBoostCount; i++)
                {
                    for (int attempt = 0; attempt < 15; attempt++)
                    {
                        int start = Random.Range(7, 21);
                        int end = Mathf.Min(23, start + Random.Range(1, 3));
                        if (Overlaps(start, end)) continue;
                        float pct = Mathf.Round(Random.Range(0.10f, Mathf.Max(0.10f, weekdayMaxPercent)) * 20f) / 20f; // %5 adımlar
                        WeekdayBoosts.Add(new BoostWindow { startHour = start, endHour = end, percent = pct, label = "Hafta içi boost" });
                        break;
                    }
                }
                WeekdayBoosts.Sort((a, b) => a.startHour.CompareTo(b.startHour));
            }

            var today = TodayWindows();
            if (today.Count > 0)
            {
                string list = "";
                foreach (var w in today) list += (list.Length > 0 ? ", " : "") + $"{w.TimeText} %{w.percent * 100f:0}";
                EventManager.I.Notify($"{clock.DayName} boostları: {list}");
                if (CourierManager.I != null)
                    foreach (var c in CourierManager.I.Couriers) c.AddInbox($"Bugünün boostları: {list}");
            }
        }

        bool Overlaps(int start, int end)
        {
            foreach (var w in WeekdayBoosts)
                if (start < w.endHour && w.startHour < end) return true;
            return false;
        }

        /// <summary>Bugünün tüm boostları (hafta sonu kuralları + hafta içi plan).</summary>
        public List<BoostWindow> TodayWindows()
        {
            var list = new List<BoostWindow>();
            if (GameClock.I.IsWeekend)
            {
                if (weekendMorningBoost)
                    list.Add(new BoostWindow { startHour = weekendMorningStart, endHour = weekendMorningEnd, percent = weekendMorningPercent, label = "Hafta sonu sabah boostu" });
                if (weekendLunchBoost)
                    list.Add(new BoostWindow { startHour = weekendLunchStart, endHour = weekendLunchEnd, percent = weekendLunchPercent, label = "Hafta sonu öğle boostu" });
            }
            else
            {
                list.AddRange(WeekdayBoosts);
            }
            return list;
        }

        public BoostWindow ActiveWindow
        {
            get
            {
                if (!ready) return null;
                int h = GameClock.I.Hour;
                BoostWindow best = null;
                foreach (var w in TodayWindows())
                    if (h >= w.startHour && h < w.endHour && (best == null || w.percent > best.percent)) best = w;
                return best;
            }
        }

        /// <summary>Şu anki boost oranı (0 = yok).</summary>
        public float CurrentBoost
        {
            get
            {
                if (!ready) return 0f;
                var w = ActiveWindow;
                return Mathf.Max(w != null ? w.percent : 0f, WeatherBoostPercent);
            }
        }

        public bool BoostActive => CurrentBoost > 0f;

        /// <summary>Kurye ücretine eklenecek boost tutarı (şirketin kârından).</summary>
        public int BoostAmount(int courierPay) => Mathf.RoundToInt(courierPay * CurrentBoost);

        public void RegisterBoostPaid(int amount)
        {
            boostPaidThisWeek += amount;
        }

        /// <summary>Teslimat sonrası haftalık hedef kontrolü.</summary>
        public void OnDelivered(Courier c)
        {
            c.weekDeliveries++;
            c.monthDeliveries++;

            if (weeklyChallenge && !c.weeklyRewardClaimed && c.weekDeliveries >= weeklyTarget)
            {
                c.weeklyRewardClaimed = true;
                EconomyManager.I.SpendCampaign(weeklyReward, $"Haftalık hedef ödülü: {c.courierName}");
                c.Earn(weeklyReward);
                c.AddInbox($"Tebrikler! Haftalık hedefe ulaştın ({weeklyTarget} paket), {Ft(weeklyReward)} ödül hesabına yazıldı.");
                c.morale = Mathf.Min(1f, c.morale + 0.15f);
                rewardsPaidThisWeek += weeklyReward;
                rewardsClaimedThisWeek++;
                EventManager.I.Notify($"{c.courierName} haftalık hedefe ulaştı ({weeklyTarget} paket) ve {Ft(weeklyReward)} ödül aldı!");
            }

            if (monthlyChallenge && !c.monthlyRewardClaimed && c.monthDeliveries >= monthlyTarget)
            {
                c.monthlyRewardClaimed = true;
                EconomyManager.I.SpendCampaign(monthlyReward, $"Aylık hedef ödülü: {c.courierName}");
                c.Earn(monthlyReward);
                c.AddInbox($"Ayın kuryesi! {monthlyTarget}+ paket, {Ft(monthlyReward)} ödül hesabına yazıldı.");
                c.morale = Mathf.Min(1f, c.morale + 0.25f);
                rewardsPaidThisMonth += monthlyReward;
                rewardsClaimedThisMonth++;
                EventManager.I.Push(new GameEvent("Ayın kuryesi!",
                    $"{c.courierName} bu ay {monthlyTarget}+ paket teslim etti ve {Ft(monthlyReward)} ekstra kazandı. Morali tavan yaptı!"));
            }
        }
    }
}
