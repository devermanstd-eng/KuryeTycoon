using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>İş başvurusu yapan aday.</summary>
    public class CourierCandidate
    {
        public string name;
        public VehicleKind vehicle;
        public WorkRoutine routine;
        public float speedSkill, care, politeness, stamina;
        public int level = 1;
        public int askExtraPay;    // deneyimliler paket başı ek ücret ister
        public string note = "";
        public float expiresAt;    // başvuru bu oyun dakikasında geçersiz olur
    }

    /// <summary>
    /// Başvurular, işe alım / çıkarma (ücretsiz), ekipman,
    /// günlük moral, izin günü, hastalık ve kaza olayları.
    /// Kuryeler serbest çalışır: zam istemezler, paket başı kazanırlar.
    /// </summary>
    public class CourierManager : MonoBehaviour
    {
        public static CourierManager I { get; private set; }

        public readonly List<Courier> Couriers = new List<Courier>();
        public readonly List<CourierCandidate> Applications = new List<CourierCandidate>();
        public int maxApplications = 6;
        public int accidentTreatmentCost = 15000;

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
                    EventManager.I.Notify($"Yeni iş başvurusu: {c.name} ({GameData.Vehicle(c.vehicle).name}, seviye {c.level})");
                }
                ScheduleNextApplication();
            }
        }

        /// <summary>İtibar ve bonus çarpanı yüksekse başvurular daha sık gelir.</summary>
        void ScheduleNextApplication()
        {
            float rep = 0.5f + 0.15f * CompanyProfile.Reputation;
            float bonus = Mathf.Lerp(0.8f, 1.3f, (EconomyManager.I.bonusMultiplier - 0.5f) / 1.5f);
            nextApplicationAt = GameClock.I.TotalMinutes + Random.Range(120f, 360f) / (rep * bonus);
        }

        static WorkRoutine RandomRoutine()
        {
            float r = Random.value;
            if (r < 0.30f) return WorkRoutine.TamGun;
            if (r < 0.50f) return WorkRoutine.Ogrenci;
            if (r < 0.70f) return WorkRoutine.OgleAksam;
            if (r < 0.85f) return WorkRoutine.Sabahci;
            return WorkRoutine.GeceKusu;
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
                routine = RandomRoutine(),
                level = level,
                askExtraPay = (level - 1) * 75,
                speedSkill = Random.Range(0.85f, 1.2f) + skill,
                care = Mathf.Min(1f, Random.Range(0.55f, 0.95f) + skill),
                politeness = Mathf.Min(1f, Random.Range(0.55f, 0.95f) + skill),
                stamina = Random.Range(0.7f, 1.2f) + skill,
                note = GameData.RandomNote(),
                expiresAt = withExpiry ? GameClock.I.TotalMinutes + 1440f : float.MaxValue
            };
        }

        VehicleKind RandomVehicle()
        {
            float r = Random.value;
            if (r < 0.10f) return VehicleKind.Yuruyerek;
            if (r < 0.45f) return VehicleKind.Bisiklet;
            if (r < 0.65f) return VehicleKind.EScooter;
            if (r < 0.83f) return VehicleKind.Motosiklet;
            return VehicleKind.Araba;
        }

        /// <summary>Başlangıç kuryeleri (tam gün çalışan, deneyimsiz).</summary>
        public Courier HireStarter(VehicleKind v)
        {
            var c = MakeCandidate(v, false);
            c.level = 1;
            c.askExtraPay = 0;
            c.routine = WorkRoutine.TamGun;
            return Spawn(c);
        }

        /// <summary>Başvuruyu kabul et. Ücretsiz: kurye paket başı kazanır.</summary>
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
            Couriers.Add(courier);
            return courier;
        }

        /// <summary>İşten çıkar. Ücretsiz, ama elinde paket varken olmaz.</summary>
        public bool Fire(Courier c)
        {
            if (c.OrderCount > 0)
            {
                EventManager.I.Notify($"{c.courierName} elinde paket varken işten çıkarılamaz.");
                return false;
            }
            EventManager.I.Notify($"{c.courierName} ile yollar ayrıldı.");
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

        public int BuyForAll(EquipmentKind k)
        {
            int n = 0;
            foreach (var c in Couriers.ToArray())
                if (!c.Has(k) && BuyEquipment(c, k)) n++;
            if (n > 0) EventManager.I.Notify($"{n} kuryeye {GameData.Equip(k).name} gönderildi.");
            return n;
        }

        void OnNewDay()
        {
            var season = GameClock.I.Season;
            bool weekend = GameClock.I.IsWeekend;
            float bonusM = EconomyManager.I.bonusMultiplier;
            int daysOff = 0;

            foreach (var c in Couriers.ToArray())
            {
                int worked = c.deliveriesToday;
                bool satIdle = c.onlineMinutesToday > 120f && worked == 0; // çevrimiçi bekledi ama iş gelmedi
                c.NewDay(weekend);
                if (c.dayOff) daysOff++;

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

                if (c.State == CourierState.Izinli || c.OrderCount > 0) continue;
                if (RollSickness(c, season)) continue;
                RollAccident(c);
            }

            if (daysOff > 0) EventManager.I.Notify($"Bugün {daysOff} kurye kendine izin günü yaptı.");
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
            Couriers.Remove(c);
            Destroy(c.gameObject);
        }

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
            c.SetOnLeave(days, "kaza");
            int cost = accidentTreatmentCost;
            string helmetText = helmet ? " Kask sayesinde hafif atlattı." : (c.V.exposed ? " Kaskı olmadığı için yaralanması ağır." : "");
            EventManager.I.Push(new GameEvent("Kaza!",
                    $"{c.courierName} ({c.V.name}) kaza geçirdi ve {days} gün çalışamayacak.{helmetText}")
                .Add($"Tedavi ve onarıma destek ol (−{Ft(cost)}, moral +)", () =>
                {
                    EconomyManager.I.Spend(cost, $"Kaza desteği: {c.courierName}", ExpenseCategory.Kaza);
                    c.morale = Mathf.Min(1f, c.morale + 0.15f);
                })
                .Add("Destek olma (moral −)", () => c.morale = Mathf.Max(0f, c.morale - 0.25f)));
        }
    }
}
