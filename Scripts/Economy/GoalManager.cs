using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    public enum GoalKind { Teslimat, YuksekPuan, IptalsizGun, SicakYemek, YeniSozlesme, BoostTeslimat, YeniKurye, ZamanindaTeslim }

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
                    default: return $"{target} siparişi geç kalmadan teslim et";
                }
            }
        }
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
    /// Haftalık hedefler: her pazartesi şirketin büyüklüğüne göre 3 hedef gelir (teslimat sayısı, yüksek puan,
    /// iptalsiz gün, sıcak yemek, yeni sözleşme, boost saatleri, yeni kurye, zamanında teslim).
    /// Tamamlanan hedefin ödülü hemen kasaya girer. Biten hafta yarım kalan hedefler için ceza yoktur.
    /// </summary>
    public class GoalManager : MonoBehaviour
    {
        public static GoalManager I { get; private set; }

        public readonly List<Goal> Goals = new List<Goal>();
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

            var pool = new List<Goal>
            {
                new Goal { kind = GoalKind.Teslimat, target = baseDeliveries, reward = baseDeliveries * 80L },
                new Goal { kind = GoalKind.YuksekPuan, target = Round5(baseDeliveries * 0.5f), reward = Round5(baseDeliveries * 0.5f) * 110L, reputation = 0.03f },
                new Goal { kind = GoalKind.IptalsizGun, target = 3, reward = 3500, reputation = 0.05f },
                new Goal { kind = GoalKind.SicakYemek, target = Round5(baseDeliveries * 0.35f), reward = Round5(baseDeliveries * 0.35f) * 90L },
                new Goal { kind = GoalKind.ZamanindaTeslim, target = Round5(baseDeliveries * 0.6f), reward = Round5(baseDeliveries * 0.6f) * 90L },
                new Goal { kind = GoalKind.BoostTeslimat, target = Round5(Mathf.Max(10, baseDeliveries * 0.25f)), reward = 3000 },
            };
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
            for (int i = 0; i < 2 && pool.Count > 0; i++)
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
