using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    public enum GoalKind { Teslimat, YuksekPuan, IptalsizGun, SicakYemek, YeniSozlesme, BoostTeslimat, YeniKurye, ZamanindaTeslim,
                           UzakTeslimat, MarketTeslimat, GeceTeslimat, YagmurTeslimat, Bahsis, Kar, TopluTeslimat }

    [System.Serializable]
    public class Goal
    {
        public GoalKind kind;
        public int target;
        public int progress;
        public long reward;          // para ödülü (kuruş)
        public float reputation;     // ek itibar ödülü
        public bool done;

        public float Progress01 => target <= 0 ? 1f : Mathf.Clamp01(progress / (float)target);

        public string Title
        {
            get
            {
                switch (kind)
                {
                    case GoalKind.Teslimat: return $"Bu hafta {target} teslimat yap";
                    case GoalKind.YuksekPuan: return $"{target} teslimatı 4,5★ ve üstü puanla tamamla";
                    case GoalKind.IptalsizGun: return $"{target} günü hiç iptal olmadan bitir";
                    case GoalKind.SicakYemek: return $"{target} sıcak yemeği soğumadan teslim et";
                    case GoalKind.YeniSozlesme: return target > 1 ? $"{target} yeni dükkanla sözleşme imzala" : "Yeni bir dükkanla sözleşme imzala";
                    case GoalKind.BoostTeslimat: return $"Boost saatlerinde {target} teslimat yap";
                    case GoalKind.YeniKurye: return target > 1 ? $"{target} yeni kurye işe al" : "Yeni bir kurye işe al";
                    case GoalKind.UzakTeslimat: return $"3 km'den uzak {target} sipariş teslim et";
                    case GoalKind.MarketTeslimat: return $"{target} market siparişi teslim et";
                    case GoalKind.GeceTeslimat: return $"Gece (22:00–03:00) {target} teslimat yap";
                    case GoalKind.YagmurTeslimat: return $"Yağmur ya da karda {target} teslimat yap";
                    case GoalKind.Bahsis: return $"Kuryelerin bu hafta {GameData.Ft(target)} bahşiş toplasın";
                    case GoalKind.Kar: return $"Teslimatlardan {GameData.Ft(target)} kâr et";
                    case GoalKind.TopluTeslimat: return $"{target} siparişi toplu teslimatla götür";
                    default: return $"{target} siparişi geç kalmadan teslim et";
                }
            }
        }

        public bool IsMoney => kind == GoalKind.Bahsis || kind == GoalKind.Kar;
        public string ProgressText => IsMoney ? $"{GameData.Ft(progress)} / {GameData.Ft(target)}" : $"{progress}/{target}";
    }

    [System.Serializable]
    public class GoalSave
    {
        public List<Goal> goals = new List<Goal>();
        public int weekStartDay;
        public int completedTotal;
        public int lastWeekDeliveries;
    }

    /// <summary>
    /// Haftalık hedefler: her pazartesi şirketin büyüklüğüne göre 6 hedef gelir (teslimat sayısı, yüksek puan,
    /// iptalsiz gün, sıcak yemek, yeni sözleşme, boost saatleri, yeni kurye, zamanında teslim).
    /// Tamamlanan hedefin ödülü hemen kasaya girer. Biten hafta yarım kalan hedefler için ceza yoktur.
    /// </summary>
    public class GoalManager : MonoBehaviour
    {
        public static GoalManager I { get; private set; }

        public readonly List<Goal> Goals = new List<Goal>();
        public const int GoalsPerWeek = 6;
        public int weekStartDay;
        public int completedTotal;
        int weekDeliveries, lastWeekDeliveries;
        int cancelsToday;

        void Awake() { I = this; }

        public void Init()
        {
            GameClock.I.OnNewDay += OnNewDay;
            if (Goals.Count == 0) NewWeek();
        }

        public int DaysLeft => Mathf.Max(0, weekStartDay + 7 - GameClock.I.Day);

        void OnNewDay()
        {
            // Dün hiç iptal olmadıysa (ve en az bir teslimat yapıldıysa) iptalsiz gün sayılır
            if (cancelsToday == 0 && dayDeliveries > 0) Advance(GoalKind.IptalsizGun, 1);
            cancelsToday = 0;
            dayDeliveries = 0;
            if (GameClock.I.Day >= weekStartDay + 7) NewWeek();
        }

        int dayDeliveries;

        void NewWeek()
        {
            int unfinished = 0;
            foreach (var g in Goals) if (!g.done) unfinished++;
            if (Goals.Count > 0 && unfinished > 0)
                EventManager.I.Notify($"Hafta bitti: {Goals.Count - unfinished}/{Goals.Count} hedef tamamlandı. Yeni haftalık hedefler geldi.");
            else if (Goals.Count > 0)
                EventManager.I.Notify("Haftanın bütün hedefleri tamamlandı! Yeni haftalık hedefler geldi.");

            lastWeekDeliveries = Goals.Count > 0 ? weekDeliveries : 0;
            weekDeliveries = 0;
            weekStartDay = GameClock.I.Day;
            Goals.Clear();

            // Hedef büyüklüğü şirketin büyüklüğüne göre: geçen haftanın teslimatı, kurye sayısı
            int couriers = Mathf.Max(1, CourierManager.I.Couriers.Count);
            int baseDeliveries = Mathf.Max(25, Mathf.Max(couriers * 18, Mathf.RoundToInt(lastWeekDeliveries * 1.15f)));
            baseDeliveries = Mathf.RoundToInt(baseDeliveries / 5f) * 5;

            int bd = baseDeliveries;
            var pool = new List<Goal>
            {
                new Goal { kind = GoalKind.Teslimat, target = bd, reward = bd * 250L },
                new Goal { kind = GoalKind.YuksekPuan, target = Round5(bd * 0.5f), reward = Round5(bd * 0.5f) * 320L, reputation = 0.03f },
                new Goal { kind = GoalKind.IptalsizGun, target = 3, reward = 9000, reputation = 0.05f },
                new Goal { kind = GoalKind.SicakYemek, target = Round5(bd * 0.35f), reward = Round5(bd * 0.35f) * 280L },
                new Goal { kind = GoalKind.ZamanindaTeslim, target = Round5(bd * 0.6f), reward = Round5(bd * 0.6f) * 280L },
                new Goal { kind = GoalKind.BoostTeslimat, target = Round5(Mathf.Max(10, bd * 0.25f)), reward = 9000 },
                new Goal { kind = GoalKind.UzakTeslimat, target = Round5(Mathf.Max(5, bd * 0.15f)), reward = Round5(Mathf.Max(5, bd * 0.15f)) * 500L },
                new Goal { kind = GoalKind.MarketTeslimat, target = Round5(Mathf.Max(5, bd * 0.2f)), reward = Round5(Mathf.Max(5, bd * 0.2f)) * 400L },
                new Goal { kind = GoalKind.GeceTeslimat, target = Round5(Mathf.Max(5, bd * 0.1f)), reward = Round5(Mathf.Max(5, bd * 0.1f)) * 600L },
                new Goal { kind = GoalKind.Bahsis, target = Mathf.RoundToInt(Mathf.Max(5000, bd * 180) / 1000f) * 1000, reward = 8000 },
                new Goal { kind = GoalKind.Kar, target = Mathf.RoundToInt(bd * 900 / 1000f) * 1000, reward = Mathf.RoundToInt(bd * 120 / 100f) * 100 },
                new Goal { kind = GoalKind.TopluTeslimat, target = Round5(Mathf.Max(5, bd * 0.15f)), reward = Round5(Mathf.Max(5, bd * 0.15f)) * 450L },
            };
            if (WeatherSystem.I != null && (GameClock.I.Season == Season.Sonbahar || GameClock.I.Season == Season.Kis || GameClock.I.Season == Season.Ilkbahar))
                pool.Add(new Goal { kind = GoalKind.YagmurTeslimat, target = Round5(Mathf.Max(5, bd * 0.2f)), reward = Round5(Mathf.Max(5, bd * 0.2f)) * 450L });
            // Açık bölgede sözleşmesiz dükkan varsa
            foreach (var s in CityMap.I.Shops)
                if (!s.contracted && CityMap.I.IsUnlocked(s.tile))
                {
                    pool.Add(new Goal { kind = GoalKind.YeniSozlesme, target = 1, reward = Mathf.Max(5000, s.def.contractCost / 4) });
                    break;
                }
            if (!UpgradeManager.I.CourierCapFull)
                pool.Add(new Goal { kind = GoalKind.YeniKurye, target = 1, reward = 2500 });

            // Teslimat hedefi her hafta var; diğer ikisi rastgele
            Goals.Add(pool[0]);
            pool.RemoveAt(0);
            for (int i = 0; i < GoalsPerWeek - 1 && pool.Count > 0; i++)
            {
                int k = Random.Range(0, pool.Count);
                Goals.Add(pool[k]);
                pool.RemoveAt(k);
            }
        }

        static int Round5(float v) => Mathf.Max(5, Mathf.RoundToInt(v / 5f) * 5);

        void Advance(GoalKind kind, int amount)
        {
            foreach (var g in Goals)
            {
                if (g.kind != kind || g.done) continue;
                g.progress = Mathf.Min(g.target, g.progress + amount);
                if (g.progress >= g.target) Complete(g);
            }
        }

        void Complete(Goal g)
        {
            g.done = true;
            completedTotal++;
            EconomyManager.I.AddIncome(g.reward, $"Hedef ödülü: {g.Title}");
            if (g.reputation > 0f) CompanyProfile.AddReputation(g.reputation);
            EventManager.I.Notify($"Hedef tamamlandı: {g.Title}. Ödül {GameData.Ft(g.reward)}" + (g.reputation > 0f ? " ve itibar." : "."));
        }

        // ---------- Olaylar ----------

        public void OnDelivered(Order o)
        {
            weekDeliveries++;
            dayDeliveries++;
            Advance(GoalKind.Teslimat, 1);
            if (o.rating >= 4.5f) Advance(GoalKind.YuksekPuan, 1);
            if (o.shop.def.hotFood && !o.deliveredCold) Advance(GoalKind.SicakYemek, 1);
            if (o.deliveredAt <= o.deadlineAt || o.customerFault) Advance(GoalKind.ZamanindaTeslim, 1);
            if (CampaignManager.I != null && CampaignManager.I.BoostActive) Advance(GoalKind.BoostTeslimat, 1);
            if (o.DistanceKm >= 3f) Advance(GoalKind.UzakTeslimat, 1);
            if (o.shop.def.isMarket) Advance(GoalKind.MarketTeslimat, 1);
            int h = GameClock.I.Hour;
            if (h >= 22 || h < 3) Advance(GoalKind.GeceTeslimat, 1);
            if (WeatherSystem.I != null && (WeatherSystem.I.IsRainy || WeatherSystem.I.Current == WeatherKind.Karli)) Advance(GoalKind.YagmurTeslimat, 1);
            if (o.tip > 0) Advance(GoalKind.Bahsis, o.tip);
            if (o.Profit > 0) Advance(GoalKind.Kar, o.Profit);
            if (o.batched) Advance(GoalKind.TopluTeslimat, 1);
        }

        public void OnCancelled() => cancelsToday++;
        public void OnContract() => Advance(GoalKind.YeniSozlesme, 1);
        public void OnHire() => Advance(GoalKind.YeniKurye, 1);

        // ---------- Kayıt ----------

        public GoalSave CaptureSave() => new GoalSave
        {
            goals = new List<Goal>(Goals),
            weekStartDay = weekStartDay,
            completedTotal = completedTotal,
            lastWeekDeliveries = lastWeekDeliveries
        };

        public void RestoreSave(GoalSave s)
        {
            if (s == null) return;
            Goals.Clear();
            if (s.goals != null) Goals.AddRange(s.goals);
            weekStartDay = s.weekStartDay;
            completedTotal = s.completedTotal;
            lastWeekDeliveries = s.lastWeekDeliveries;
            if (Goals.Count == 0) NewWeek();
        }
    }
}
