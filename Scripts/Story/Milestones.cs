using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    public class MilestoneDef
    {
        public string id;
        public string title;
        public string text;
        public bool card;                     // ulaşınca kart mı (büyük hedef), bildirim mi
        public System.Func<bool> reached;
        public System.Func<string> progress;  // ilerleme metni (yoksa boş)
        public long reward;                   // ulaşınca kasaya giren ödül (basın ilgisi, sponsorluk...)
        public float reputation;              // ek itibar

        public string RewardText
        {
            get
            {
                if (reward <= 0 && reputation <= 0f) return "";
                if (reward <= 0) return "itibar";
                return GameData.Ft(reward) + (reputation > 0f ? " + itibar" : "");
            }
        }
    }

    /// <summary>
    /// Kilometre taşları (GDD: 100 teslimat, 10 kurye, şehir lideri...). Oyun sırasında saniyede bir kontrol edilir;
    /// ulaşılınca kart ya da bildirim çıkar ve Finans sekmesindeki listede tarihiyle görünür.
    /// Her taşın bir ödülü var (para ve/veya itibar): ulaşıldığı an bir kez ödenir, yüklenen kayıtta tekrar ödenmez.
    /// İleride Steam başarımlarına bağlanabilir.
    /// </summary>
    public class Milestones : MonoBehaviour
    {
        public static Milestones I { get; private set; }

        public readonly List<MilestoneDef> Defs = new List<MilestoneDef>();
        readonly Dictionary<string, string> reachedOn = new Dictionary<string, string>(); // id → tarih
        float timer;

        static string Ft(long v) => GameData.Ft(v);
        /// <summary>1000 → "1 000" (cihazın dil ayarından bağımsız).</summary>
        static string N(long v) => v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' ');

        void Awake()
        {
            I = this;
            BuildDefs();
        }

        /// <summary>Şirketin bugüne kadarki toplam teslimatı (geçmiş günler + bugün).</summary>
        public static long TotalDeliveries
        {
            get
            {
                long n = EconomyManager.I != null ? EconomyManager.I.Today.deliveries : 0;
                if (StatsHistory.I != null) foreach (var d in StatsHistory.I.Days) n += d.deliveries;
                return n;
            }
        }

        static int CourierCount => CourierManager.I != null ? CourierManager.I.Couriers.Count : 0;

        void Deliveries(string id, long target, bool card, long reward, float reputation = 0f)
        {
            Defs.Add(new MilestoneDef
            {
                id = id, card = card, reward = reward, reputation = reputation,
                title = target == 1 ? "İlk teslimat" : $"{N(target)} teslimat",
                text = target == 1
                    ? "Şirketin ilk siparişini teslim etti. Her büyük şirket bir paketle başlar."
                    : $"Şirketin toplam {N(target)} siparişi teslim etti.",
                reached = () => TotalDeliveries >= target,
                progress = () => $"{N(System.Math.Min(TotalDeliveries, target))} / {N(target)}"
            });
        }

        void Couriers(string id, int target, long reward)
        {
            Defs.Add(new MilestoneDef
            {
                id = id, card = true, reward = reward,
                title = $"{target} kurye",
                text = $"Ekibinde aynı anda {target} kurye çalışıyor.",
                reached = () => CourierCount >= target,
                progress = () => $"{Mathf.Min(CourierCount, target)} / {target}"
            });
        }

        void BuildDefs()
        {
            Defs.Clear();
            // Ödüller: erken taşlar küçük bir itme, büyükler bir sonraki büyüme adımına katkı (bölge açmak 150 000 – 1 200 000 Ft)
            Deliveries("teslimat_1", 1, false, 2000);
            Deliveries("teslimat_100", 100, true, 10000);
            Deliveries("teslimat_1000", 1000, true, 50000, 0.05f);
            Deliveries("teslimat_10000", 10000, true, 250000, 0.1f);
            Couriers("kurye_5", 5, 10000);
            Couriers("kurye_10", 10, 30000);
            Couriers("kurye_20", 20, 80000);
            Defs.Add(new MilestoneDef
            {
                id = "bolge_2", card = true, title = "Yeni ilçe", reward = 40000,
                text = $"{CityMap.DistrictNames[2]} açıldı. Şirket mahalleden çıkıp ilçeye yayıldı.",
                reached = () => CityMap.I != null && CityMap.I.UnlockLevel >= 2
            });
            Defs.Add(new MilestoneDef
            {
                id = "bolge_3", card = true, title = "Bütün şehir", reward = 200000, reputation = 0.05f,
                text = "Haritanın tamamında teslimat yapıyorsun.",
                reached = () => CityMap.I != null && CityMap.I.UnlockLevel >= CityMap.I.MaxLevel
            });
            Defs.Add(new MilestoneDef
            {
                id = "itibar_4", card = true, title = "İtibar 4,0", reward = 25000,
                text = "Müşteriler şirketini seviyor: itibarın 4,0'a ulaştı.",
                reached = () => CompanyProfile.Reputation >= 4f,
                progress = () => $"{CompanyProfile.Reputation:0.00} / 4,00"
            });
            Defs.Add(new MilestoneDef
            {
                id = "itibar_45", card = true, title = "İtibar 4,5", reward = 75000,
                text = "Şehrin en sevilen kurye şirketlerinden birisin: itibarın 4,5'e ulaştı.",
                reached = () => CompanyProfile.Reputation >= 4.5f,
                progress = () => $"{CompanyProfile.Reputation:0.00} / 4,50"
            });
            Defs.Add(new MilestoneDef
            {
                id = "gunluk_kar", card = true, title = "Büyük gün", reward = 30000,
                text = $"Bir günde {Ft(100000)} net kâr ettin.",
                reached = () =>
                {
                    var h = StatsHistory.I;
                    if (h == null) return false;
                    foreach (var d in h.Days) if (d.net >= 100000) return true;
                    return false;
                }
            });
            Defs.Add(new MilestoneDef
            {
                id = "sehir_lideri", card = true, title = "Şehrin lideri", reward = 100000, reputation = 0.05f,
                text = "Geçen hafta bütün rakiplerinden daha çok teslimat yaptın: pazarın lideri sensin.",
                reached = () => RivalCompany.I != null && RivalCompany.I.myDeliveriesLastWeek > 0 && RivalCompany.I.Lideriz,
                progress = () => RivalCompany.I != null ? $"pazar payı %{RivalCompany.I.MarketShare * 100f:0}" : ""
            });
            Defs.Add(new MilestoneDef
            {
                id = "kurumsal", card = true, title = "Kurumsal", reward = 150000,
                text = "Kurye platformunun Kurumsal paketine geçtin.",
                reached = () => UpgradeManager.I != null && UpgradeManager.I.Level(UpgradeKind.Platform) >= 7
            });
            Defs.Add(new MilestoneDef
            {
                id = "pazar_lideri", card = true, title = "Pazar lideri", reward = 400000,
                text = "Kurye platformunun en büyük paketine geçtin: 180 kurye hesabı.",
                reached = () => UpgradeManager.I != null &&
                                UpgradeManager.I.Level(UpgradeKind.Platform) >= UpgradeManager.Def(UpgradeKind.Platform).MaxLevel
            });
            Defs.Add(new MilestoneDef
            {
                id = "aile_borcu", card = true, title = "Söz tutuldu", reputation = 0.1f,
                text = "Kurulum kredisini faiziyle kuruşu kuruşuna geri ödedin.",
                reached = () => FamilyLoan.I != null && FamilyLoan.I.Finished
            });
        }

        public bool IsReached(string id) => reachedOn.ContainsKey(id);
        public string ReachedDate(string id) => reachedOn.TryGetValue(id, out string d) ? d : "";
        public int ReachedCount => reachedOn.Count;

        void Update()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || GameClock.I == null || EventManager.I == null) return;
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = 1f;

            foreach (var m in Defs)
            {
                if (reachedOn.ContainsKey(m.id) || !m.reached()) continue;
                reachedOn[m.id] = GameClock.I.DateOf(GameClock.I.Day);
                string reward = PayReward(m);
                if (m.card)
                {
                    EventManager.I.Push(new GameEvent($"Kilometre taşı: {m.title}", m.text + reward));
                    GameSounds.Play(Sfx.Buyuk);
                }
                else EventManager.I.Notify($"Kilometre taşı: {m.title}. {m.text}{reward}");
            }
        }

        /// <summary>Ödülü öder; kart/bildirim için açıklama döner.</summary>
        static string PayReward(MilestoneDef m)
        {
            if (m.reward > 0 && EconomyManager.I != null) EconomyManager.I.AddIncome(m.reward, $"Kilometre taşı ödülü: {m.title}");
            if (m.reputation > 0f) CompanyProfile.AddReputation(m.reputation);
            if (m.reward <= 0 && m.reputation <= 0f) return "";
            if (m.reward <= 0) return m.card ? "\n\nÖdül: itibarın arttı." : " Ödül: itibar.";
            string rep = m.reputation > 0f ? " ve itibar" : "";
            return m.card
                ? $"\n\nÖdül: {Ft(m.reward)}{rep} (basın ilgisi ve sponsorluk). Kasana eklendi."
                : $" Ödül: {Ft(m.reward)}{rep}.";
        }

        // ---------- Kayıt ----------

        public List<string> SaveIds() => new List<string>(reachedOn.Keys);

        public List<string> SaveDates()
        {
            var list = new List<string>();
            foreach (var id in reachedOn.Keys) list.Add(reachedOn[id]);
            return list;
        }

        public void Restore(List<string> ids, List<string> dates)
        {
            reachedOn.Clear();
            if (ids == null) return;
            for (int i = 0; i < ids.Count; i++)
                reachedOn[ids[i]] = dates != null && i < dates.Count ? dates[i] : "";
        }
    }
}
