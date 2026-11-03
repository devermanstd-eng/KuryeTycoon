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
    }

    /// <summary>
    /// Kilometre taşları (GDD: 100 teslimat, 10 kurye, şehir lideri...). Oyun sırasında saniyede bir kontrol edilir;
    /// ulaşılınca kart ya da bildirim çıkar ve Finans sekmesindeki listede tarihiyle görünür.
    /// Şimdilik sadece tanınma amaçlı, oyuna etkisi yok; ileride Steam başarımlarına bağlanabilir.
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

        void Deliveries(string id, long target, bool card)
        {
            Defs.Add(new MilestoneDef
            {
                id = id, card = card,
                title = target == 1 ? "İlk teslimat" : $"{N(target)} teslimat",
                text = target == 1
                    ? "Şirketin ilk siparişini teslim etti. Her büyük şirket bir paketle başlar."
                    : $"Şirketin toplam {N(target)} siparişi teslim etti.",
                reached = () => TotalDeliveries >= target,
                progress = () => $"{N(System.Math.Min(TotalDeliveries, target))} / {N(target)}"
            });
        }

        void Couriers(string id, int target)
        {
            Defs.Add(new MilestoneDef
            {
                id = id, card = true,
                title = $"{target} kurye",
                text = $"Ekibinde aynı anda {target} kurye çalışıyor.",
                reached = () => CourierCount >= target,
                progress = () => $"{Mathf.Min(CourierCount, target)} / {target}"
            });
        }

        void BuildDefs()
        {
            Defs.Clear();
            Deliveries("teslimat_1", 1, false);
            Deliveries("teslimat_100", 100, true);
            Deliveries("teslimat_1000", 1000, true);
            Deliveries("teslimat_10000", 10000, true);
            Couriers("kurye_5", 5);
            Couriers("kurye_10", 10);
            Couriers("kurye_20", 20);
            Defs.Add(new MilestoneDef
            {
                id = "bolge_2", card = true, title = "Yeni ilçe",
                text = $"{CityMap.DistrictNames[2]} açıldı. Şirket mahalleden çıkıp ilçeye yayıldı.",
                reached = () => CityMap.I != null && CityMap.I.UnlockLevel >= 2
            });
            Defs.Add(new MilestoneDef
            {
                id = "bolge_3", card = true, title = "Bütün şehir",
                text = "Haritanın tamamında teslimat yapıyorsun.",
                reached = () => CityMap.I != null && CityMap.I.UnlockLevel >= CityMap.I.MaxLevel
            });
            Defs.Add(new MilestoneDef
            {
                id = "itibar_4", card = true, title = "İtibar 4,0",
                text = "Müşteriler şirketini seviyor: itibarın 4,0'a ulaştı.",
                reached = () => CompanyProfile.Reputation >= 4f,
                progress = () => $"{CompanyProfile.Reputation:0.00} / 4,00"
            });
            Defs.Add(new MilestoneDef
            {
                id = "itibar_45", card = true, title = "İtibar 4,5",
                text = "Şehrin en sevilen kurye şirketlerinden birisin: itibarın 4,5'e ulaştı.",
                reached = () => CompanyProfile.Reputation >= 4.5f,
                progress = () => $"{CompanyProfile.Reputation:0.00} / 4,50"
            });
            Defs.Add(new MilestoneDef
            {
                id = "gunluk_kar", card = true, title = "Büyük gün",
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
                id = "sehir_lideri", card = true, title = "Şehrin lideri",
                text = "Geçen hafta rakibinden daha çok teslimat yaptın: pazarın lideri sensin.",
                reached = () => RivalCompany.I != null && RivalCompany.I.myDeliveriesLastWeek > 0 && RivalCompany.I.MarketShare > 0.5f,
                progress = () => RivalCompany.I != null ? $"pazar payı %{RivalCompany.I.MarketShare * 100f:0}" : ""
            });
            Defs.Add(new MilestoneDef
            {
                id = "kurumsal", card = true, title = "Kurumsal",
                text = "Kurye platformunun en büyük paketine geçtin.",
                reached = () => UpgradeManager.I != null &&
                                UpgradeManager.I.Level(UpgradeKind.Platform) >= UpgradeManager.Def(UpgradeKind.Platform).MaxLevel
            });
            Defs.Add(new MilestoneDef
            {
                id = "aile_borcu", card = true, title = "Söz tutuldu",
                text = "Annenle babanın birikimini kuruşu kuruşuna geri ödedin.",
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
                if (m.card) EventManager.I.Push(new GameEvent($"Kilometre taşı: {m.title}", m.text));
                else EventManager.I.Notify($"Kilometre taşı: {m.title}. {m.text}");
            }
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
