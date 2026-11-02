using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>İş başvurusu yapan aday.</summary>
    public class CourierCandidate
    {
        public string name;
        public VehicleKind vehicle;
        public bool alsoOwnsBike;      // motorlu araç sahibinin yedekte bisikleti de var
        public VehicleKind dreamVehicle; // parası olursa almak istediği araç
        public Occupation occupation;
        public float speedSkill, care, politeness, stamina;
        public float tidiness;
        public float pickiness;
        public int level = 1;
        public bool insured;           // kendi parasıyla sigortalı
        public long savings;           // birikimi
        public string note = "";
        public float expiresAt;
    }

    /// <summary>
    /// Başvurular, işe alım / çıkarma (ücretsiz), ekipman,
    /// günlük moral, izin, hastalık, kaza, arıza, araç yükseltme ve sigorta.
    /// Kuryeler serbest çalışır: zam istemez, kendi araçlarını ve sigortalarını kendileri öder.
    /// </summary>
    public class CourierManager : MonoBehaviour
    {
        public static CourierManager I { get; private set; }

        public readonly List<Courier> Couriers = new List<Courier>();
        public readonly List<CourierCandidate> Applications = new List<CourierCandidate>();
        public int maxApplications = 6;

        Transform root;
        float nextApplicationAt;
        bool ready;

        static string Ft(long v) => GameData.Ft(v);

        void Awake() { I = this; }

        public void Init()
        {
            root = new GameObject("Kuryeler").transform;
            root.SetParent(transform, false);
            for (int i = 0; i < 2; i++) Applications.Add(MakeCandidate(RandomVehicle(), true));
            ScheduleNextApplication();
            GameClock.I.OnNewDay += OnNewDay;
            GameClock.I.OnNewMonth += () => { foreach (var c in Couriers) c.pickupCancelsThisMonth = 0; };
            ready = true;
        }

        public int OnlineCount
        {
            get
            {
                int n = 0;
                foreach (var c in Couriers) if (c.IsOnline) n++;
                return n;
            }
        }

        void Update()
        {
            if (!ready || GameClock.I.DeltaMinutes <= 0f) return;
            float now = GameClock.I.TotalMinutes;

            for (int i = Applications.Count - 1; i >= 0; i--)
                if (now >= Applications[i].expiresAt) Applications.RemoveAt(i);

            if (now >= nextApplicationAt)
            {
                if (Applications.Count < maxApplications)
                {
                    var c = MakeCandidate(RandomVehicle(), true);
                    Applications.Add(c);
                    EventManager.I.Notify($"Yeni iş başvurusu: {c.name} ({GameData.Vehicle(c.vehicle).name}, {ShortOccupation(c.occupation)})");
                }
                ScheduleNextApplication();
            }
        }

        public static string ShortOccupation(Occupation o)
        {
            switch (o)
            {
                case Occupation.OfisCalisani: return "ofis çalışanı";
                case Occupation.HaftaSonu: return "hafta sonu";
                case Occupation.Ogrenci: return "öğrenci";
                case Occupation.Vardiyali: return "vardiyalı işçi";
                case Occupation.Emekli: return "emekli";
                default: return "tam zamanlı";
            }
        }

        void ScheduleNextApplication()
        {
            float rep = 0.5f + 0.15f * CompanyProfile.Reputation;
            float bonus = Mathf.Lerp(0.8f, 1.3f, (EconomyManager.I.bonusMultiplier - 0.5f) / 1.5f);
            nextApplicationAt = GameClock.I.TotalMinutes + Random.Range(120f, 360f) / (rep * bonus);
        }

        static Occupation RandomOccupation()
        {
            float r = Random.value;
            if (r < 0.20f) return Occupation.TamZamanli;
            if (r < 0.40f) return Occupation.OfisCalisani;
            if (r < 0.65f) return Occupation.Ogrenci;
            if (r < 0.80f) return Occupation.HaftaSonu;
            if (r < 0.90f) return Occupation.Vardiyali;
            return Occupation.Emekli;
        }

        static float InsureChance(VehicleKind v)
        {
            switch (v)
            {
                case VehicleKind.Bisiklet: return 0.35f;
                case VehicleKind.EScooter: return 0.5f;
                case VehicleKind.Motosiklet: return 0.6f;
                default: return 0.75f;
            }
        }

        public CourierCandidate MakeCandidate(VehicleKind v, bool withExpiry)
        {
            float r = Random.value;
            int level = r < 0.70f ? 1 : (r < 0.92f ? 2 : 3);
            float skill = 0.05f * (level - 1);
            return new CourierCandidate
            {
                name = GameData.RandomName(),
                vehicle = v,
                alsoOwnsBike = v != VehicleKind.Bisiklet && Random.value < 0.4f,
                dreamVehicle = RandomDream(v),
                occupation = RandomOccupation(),
                level = level,
                insured = Random.value < InsureChance(v),
                savings = Mathf.RoundToInt(Random.Range(0f, 60000f) / 1000f) * 1000,
                speedSkill = Random.Range(0.85f, 1.2f) + skill,
                care = Mathf.Min(1f, Random.Range(0.55f, 0.95f) + skill),
                politeness = Mathf.Min(1f, Random.Range(0.55f, 0.95f) + skill),
                stamina = Random.Range(0.7f, 1.2f) + skill,
                tidiness = Random.Range(0.2f, 1f),
                pickiness = Random.Range(0.05f, 0.85f),
                note = GameData.RandomNote(),
                expiresAt = withExpiry ? GameClock.I.TotalMinutes + 1440f : float.MaxValue
            };
        }

        /// <summary>Yarısı aracından memnun; diğerleri parası olursa daha iyi bir araç ister (illa bir üstü değil).</summary>
        static VehicleKind RandomDream(VehicleKind current)
        {
            if (current == VehicleKind.Araba || Random.value < 0.5f) return current;
            return (VehicleKind)Random.Range((int)current + 1, (int)VehicleKind.Araba + 1);
        }

        VehicleKind RandomVehicle()
        {
            float r = Random.value;
            if (r < 0.45f) return VehicleKind.Bisiklet;
            if (r < 0.65f) return VehicleKind.EScooter;
            if (r < 0.83f) return VehicleKind.Motosiklet;
            return VehicleKind.Araba;
        }

        /// <summary>Başlangıç kuryeleri (tam zamanlı, deneyimsiz).</summary>
        public Courier HireStarter(VehicleKind v)
        {
            var c = MakeCandidate(v, false);
            c.level = 1;
            c.occupation = Occupation.TamZamanli;
            c.pickiness = Random.Range(0.1f, 0.35f); // ilk kuryeler pek seçici değil
            return Spawn(c);
        }

        public void Hire(CourierCandidate c)
        {
            if (!Applications.Remove(c)) return;
            Spawn(c);
            EventManager.I.Notify($"{c.name} ({GameData.Vehicle(c.vehicle).name}) ekibe katıldı!");
        }

        public void Reject(CourierCandidate c)
        {
            Applications.Remove(c);
        }

        Courier Spawn(CourierCandidate c)
        {
            var go = new GameObject("Kurye " + c.name);
            go.transform.SetParent(root, false);
            var courier = go.AddComponent<Courier>();
            courier.Init(c, CityMap.I.HQDoor);
            GiveStarterKit(courier);
            Couriers.Add(courier);
            return courier;
        }

        /// <summary>
        /// Giriş seviyesi ekipman. Şirkete maliyeti kit başı 5 000 Ft;
        /// kuryeden de 30 000 Ft kazanınca 20 000 Ft depozito alınır.
        /// </summary>
        void GiveStarterKit(Courier c)
        {
            EconomyManager.I.Spend(GameData.StarterKitCost, $"Başlangıç kiti: {c.courierName}", ExpenseCategory.Ekipman);
            foreach (var k in GameData.StarterKit) c.equipment.Add(k);
            c.hasCompanyKit = true;
        }

        /// <summary>
        /// Ayrılan kurye ekipmanı iade ederse depozitosu geri ödenir ve kit depoya döner.
        /// İade etmezse depozito şirkette kalır.
        /// </summary>
        void HandleLeaving(Courier c)
        {
            if (!c.hasCompanyKit) return;
            bool returns = Random.value < (c.depositPaid ? 0.9f : 0.7f);
            if (returns)
            {
                if (c.depositPaid)
                {
                    EconomyManager.I.RefundDeposit(c);
                    EventManager.I.Notify($"{c.courierName} ekipmanı iade etti, {GameData.Ft(GameData.DepositAmount)} depozitosu geri ödendi.");
                }
                else EventManager.I.Notify($"{c.courierName} ekipmanı iade etti (depozito henüz kesilmemişti).");
            }
            else if (c.depositPaid)
            {
                EconomyManager.I.KeepDeposit(c);
                EventManager.I.Notify($"{c.courierName} ekipmanı iade etmedi, {GameData.Ft(GameData.DepositAmount)} depozito şirkette kaldı.");
            }
            else EventManager.I.Notify($"{c.courierName} ekipmanı iade etmedi (depozito henüz kesilmemişti).");
            c.hasCompanyKit = false;
        }

        public bool Fire(Courier c)
        {
            if (c.OrderCount > 0)
            {
                EventManager.I.Notify($"{c.courierName} elinde paket varken işten çıkarılamaz.");
                return false;
            }
            EventManager.I.Notify($"{c.courierName} ile yollar ayrıldı.");
            HandleLeaving(c);
            Couriers.Remove(c);
            Destroy(c.gameObject);
            return true;
        }

        public bool BuyEquipment(Courier c, EquipmentKind k)
        {
            if (c == null || c.Has(k)) return false;
            var e = GameData.Equip(k);
            if (!EconomyManager.I.TrySpend(e.price, $"{e.name} → {c.courierName}", ExpenseCategory.Ekipman)) return false;
            c.equipment.Add(k);
            c.morale = Mathf.Min(1f, c.morale + 0.03f);
            return true;
        }

        // ---------- Hijyen ----------

        public const int CompanyCleaningCost = 1500;

        /// <summary>Kuryeye temizlik hatırlatması: bir sonraki gün çantasını temizler (moral biraz düşer).</summary>
        public void RequestCleaning(Courier c)
        {
            c.cleanRequested = true;
            EventManager.I.Notify($"{c.ShortName}'a çanta temizliği hatırlatması gönderildi.");
        }

        /// <summary>Şirket hemen temizletir (ücret şirketten).</summary>
        public bool CompanyClean(Courier c)
        {
            if (!EconomyManager.I.TrySpend(CompanyCleaningCost, $"Çanta temizliği: {c.courierName}", ExpenseCategory.Ekipman)) return false;
            c.CleanBag(true);
            return true;
        }

        public int CleanAll()
        {
            int n = 0;
            foreach (var c in Couriers.ToArray())
                if (c.hygiene < c.MaxHygiene - 0.05f && CompanyClean(c)) n++;
            if (n > 0) EventManager.I.Notify($"{n} kuryenin çantası temizletildi.");
            return n;
        }

        /// <summary>Yıpranan çantayı yenisiyle değiştir (termal çanta fiyatı).</summary>
        public bool ReplaceBag(Courier c)
        {
            var e = GameData.Equip(EquipmentKind.TermalCanta);
            if (!EconomyManager.I.TrySpend(e.price, $"Yeni çanta: {c.courierName}", ExpenseCategory.Ekipman)) return false;
            c.ReplaceBag();
            EventManager.I.Notify($"{c.ShortName}'a yeni termal çanta verildi.");
            return true;
        }

        public int BuyForAll(EquipmentKind k)
        {
            int n = 0;
            foreach (var c in Couriers.ToArray())
                if (!c.Has(k) && BuyEquipment(c, k)) n++;
            if (n > 0) EventManager.I.Notify($"{n} kuryeye {GameData.Equip(k).name} gönderildi.");
            return n;
        }

        // ================================================================== gün sonu

        void OnNewDay()
        {
            var clock = GameClock.I;
            var season = clock.Season;
            float bonusM = EconomyManager.I.bonusMultiplier;
            bool monday = clock.DayOfWeek == 0;

            foreach (var c in Couriers.ToArray())
            {
                int worked = c.deliveriesToday;
                bool satIdle = c.onlineMinutesToday > 120f && worked == 0;
                c.NewDay(clock);

                // --- Moral ---
                float d = (0.6f - c.morale) * 0.05f + (bonusM - 1f) * 0.08f + c.equipment.Count * 0.004f;
                if (satIdle) d -= 0.03f;
                else d += Mathf.Min(0.05f, worked * 0.004f);
                if (c.V.exposed && season == Season.Kis && !c.Has(EquipmentKind.KisMontu)) d -= 0.06f;
                if (c.V.exposed && season == Season.Yaz && !c.Has(EquipmentKind.YazTisort)) d -= 0.03f;
                if (c.fatigue > 0.7f) d -= 0.03f;
                c.morale = Mathf.Clamp01(c.morale + d);

                if (c.morale < 0.2f && c.OrderCount == 0 && Random.value < 0.35f)
                {
                    Quit(c);
                    continue;
                }

                // --- Kendi parası: sigorta ve araç ---
                if (monday && c.insured && !c.PayInsurance())
                    EventManager.I.Notify($"{c.courierName} sigorta primini ödeyemedi, sigortası düştü.");
                if (!c.insured && Random.value < 0.02f && c.TryBuyInsurance())
                    EventManager.I.Notify($"{c.courierName} şirket platformu üzerinden kendine sigorta yaptırdı.");
                if (c.OrderCount == 0 && Random.value < 0.25f && c.TryBuyDreamVehicle())
                    VehicleUpgraded(c);

                if (c.State == CourierState.Izinli || c.OrderCount > 0) continue;
                if (RollSickness(c, season)) continue;
                if (RollBreakdown(c)) continue;
                RollAccident(c);
            }
        }

        void VehicleUpgraded(Courier c)
        {
            var v = c.V;
            string extra = v.kind == VehicleKind.Araba ? " Artık ağır siparişleri de alabilir." : "";
            EventManager.I.Push(new GameEvent("Yeni araç!",
                $"{c.courierName} biriktirdiği parayla kendine {v.name} aldı ({Ft(v.ownPrice)}).\n" +
                $"Artık paket başı {Ft(v.basePay)} + {v.payPerKm} Ft/km kazanıyor ve aynı anda {v.capacity} paket taşıyabiliyor.{extra}"));
            c.morale = Mathf.Min(1f, c.morale + 0.1f);
        }

        bool RollBreakdown(Courier c)
        {
            if (Random.value >= c.V.breakdownRisk) return false;
            var broken = c.V;
            int days = Random.Range(1, 4);
            int cost = Mathf.RoundToInt(broken.ownPrice * Random.Range(0.03f, 0.08f) / 1000f) * 1000;
            c.BreakDown(days, cost);

            string after = c.State == CourierState.Izinli
                ? "Başka aracı olmadığı için tamir bitene kadar çalışamayacak."
                : $"Tamir bitene kadar {c.V.name} ile çalışacak (kazancı ve taşıma kapasitesi buna göre değişir).";
            EventManager.I.Notify($"{c.courierName}'ın {broken.name} arızalandı ({days} gün, tamiri {Ft(cost)} kendi cebinden). {after}");
            return true;
        }

        bool RollSickness(Courier c, Season season)
        {
            if (!c.V.exposed) return false;
            float risk = 0f;
            string cause = "";
            if (season == Season.Kis && !c.Has(EquipmentKind.KisMontu)) { risk = 0.10f; cause = "montsuz çalışırken üşüttü"; }
            else if ((season == Season.Ilkbahar || season == Season.Sonbahar) && !c.Has(EquipmentKind.Yagmurluk)) { risk = 0.04f; cause = "yağmurda ıslanıp hastalandı"; }
            if (Random.value >= risk) return false;

            c.SetOnLeave(1, "hasta");
            EventManager.I.Notify($"{c.courierName} {cause}, bugün çalışamıyor.");
            return true;
        }

        void Quit(Courier c)
        {
            EventManager.I.Push(new GameEvent("Kurye ayrıldı",
                $"{c.courierName} moralinin çok düşük olduğunu söyleyerek seninle çalışmayı bıraktı. " +
                "Yeterince sipariş, mevsime uygun ekipman ve bonuslar moral için önemli."));
            HandleLeaving(c);
            Couriers.Remove(c);
            Destroy(c.gameObject);
        }

        /// <summary>
        /// Kaza. Şirket masraf ödemez: sigortalıysa sigorta karşılar,
        /// değilse kurye kendi öder, daha uzun süre çalışamaz ve morali çok düşer.
        /// </summary>
        void RollAccident(Courier c)
        {
            float risk = c.V.accidentRisk;
            var w = WeatherSystem.I;
            if (w.Current == WeatherKind.Karli) risk *= 2f;
            else if (w.IsRainy) risk *= 1.5f;
            bool helmet = c.V.exposed && c.Has(EquipmentKind.Kask);
            if (helmet) risk *= 0.4f;
            if (Random.value >= risk) return;

            int days = helmet ? 1 : 3;
            string helmetText = helmet ? "Kask sayesinde hafif atlattı. " : (c.V.exposed ? "Kaskı olmadığı için yaralanması ağır. " : "");

            if (c.insured)
            {
                c.SetOnLeave(days, "kaza");
                c.morale = Mathf.Max(0f, c.morale - 0.05f);
                EventManager.I.Notify($"{c.courierName} kaza geçirdi ({days} gün çalışamayacak). {helmetText}Masrafları sigortası karşıladı.");
                return;
            }

            days += 2; // sigortasız: tedavi ve tamir daha uzun sürer
            c.SetOnLeave(days, "kaza");
            c.morale = Mathf.Max(0f, c.morale - 0.2f);
            long cost = Mathf.RoundToInt(c.V.ownPrice * 0.06f / 1000f) * 1000 + 10000;
            c.savings = System.Math.Max(0L, c.savings - cost);
            bool buys = Random.value < 0.5f;
            if (buys) c.insured = true; // iyileşince sigorta yaptırmaya karar verdi (prim Pazartesi kesilir)

            EventManager.I.Notify($"{c.courierName} kaza geçirdi, {days} gün çalışamayacak. {helmetText}" +
                $"Sigortası olmadığı için ~{Ft(cost)} masrafı kendi ödedi, morali çok düştü." +
                (buys ? " Sigorta yaptırmaya karar verdi." : ""));
        }
    }
}
