using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    public class CourierCandidate
    {
        public string name;
        public VehicleKind vehicle;
        public float speedSkill, care, politeness, stamina;
        public int hireCost;
    }

    /// <summary>İşe alım, işten çıkarma, ekipman, günlük moral / kaza / zam olayları.</summary>
    public class CourierManager : MonoBehaviour
    {
        public static CourierManager I { get; private set; }

        public readonly List<Courier> Couriers = new List<Courier>();
        public readonly List<CourierCandidate> Candidates = new List<CourierCandidate>();
        public int candidatesPerDay = 3;
        public int accidentTreatmentCost = 15000;

        Transform root;

        static string Ft(long v) => GameData.Ft(v);

        void Awake() { I = this; }

        public void Init()
        {
            root = new GameObject("Kuryeler").transform;
            root.SetParent(transform, false);
            RefreshCandidates();
            GameClock.I.OnNewDay += OnNewDay;
        }

        public CourierCandidate MakeCandidate(VehicleKind v)
        {
            var def = GameData.Vehicle(v);
            return new CourierCandidate
            {
                name = GameData.RandomName(),
                vehicle = v,
                speedSkill = Random.Range(0.85f, 1.2f),
                care = Random.Range(0.55f, 0.98f),
                politeness = Random.Range(0.55f, 0.98f),
                stamina = Random.Range(0.7f, 1.2f),
                hireCost = Mathf.RoundToInt(def.hireCost * Random.Range(0.8f, 1.3f) / 1000f) * 1000
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

        public void RefreshCandidates()
        {
            Candidates.Clear();
            for (int i = 0; i < candidatesPerDay; i++) Candidates.Add(MakeCandidate(RandomVehicle()));
        }

        /// <summary>Başlangıç kuryeleri (ücretsiz).</summary>
        public Courier HireFree(VehicleKind v) => Spawn(MakeCandidate(v));

        public bool Hire(CourierCandidate c)
        {
            if (!EconomyManager.I.TrySpend(c.hireCost, $"İşe alım primi: {c.name}")) return false;
            Candidates.Remove(c);
            Spawn(c);
            EventManager.I.Notify($"{c.name} ({GameData.Vehicle(c.vehicle).name}) ekibe katıldı!");
            return true;
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

        public int SeveranceFor(Courier c) => c.V.hireCost / 5;

        public bool Fire(Courier c)
        {
            if (c.OrderCount > 0)
            {
                EventManager.I.Notify($"{c.courierName} elinde sipariş varken işten çıkarılamaz.");
                return false;
            }
            EconomyManager.I.Spend(SeveranceFor(c), $"Tazminat: {c.courierName}");
            Couriers.Remove(c);
            Destroy(c.gameObject);
            return true;
        }

        public bool BuyEquipment(Courier c, EquipmentKind k)
        {
            if (c == null || c.Has(k)) return false;
            var e = GameData.Equip(k);
            if (!EconomyManager.I.TrySpend(e.price, $"{e.name} → {c.courierName}")) return false;
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
            RefreshCandidates();
            var season = GameClock.I.Season;
            float bonusM = EconomyManager.I.bonusMultiplier;

            foreach (var c in Couriers.ToArray())
            {
                c.NewDay();

                float d = (0.6f - c.morale) * 0.05f + (bonusM - 1f) * 0.08f + c.equipment.Count * 0.004f;
                if (season == Season.Kis && c.V.exposed && !c.Has(EquipmentKind.KisMontu)) d -= 0.06f;
                if (season == Season.Yaz && !c.Has(EquipmentKind.YazTisort)) d -= 0.03f;
                if (c.fatigue > 0.7f) d -= 0.03f;
                c.morale = Mathf.Clamp01(c.morale + d);

                if (c.morale < 0.2f && c.OrderCount == 0 && Random.value < 0.35f)
                {
                    Quit(c);
                    continue;
                }
                RollAccident(c);
            }
            RollRaiseRequest();
        }

        void Quit(Courier c)
        {
            EventManager.I.Push(new GameEvent("İstifa",
                $"{c.courierName} moralinin çok düşük olduğunu söyleyerek istifa etti. " +
                "Mevsime uygun ekipman, bonuslar ve dinlenme moral için önemli."));
            Couriers.Remove(c);
            Destroy(c.gameObject);
        }

        void RollAccident(Courier c)
        {
            if (c.State == CourierState.Izinli || c.OrderCount > 0) return;

            float risk = c.V.accidentRisk;
            var w = WeatherSystem.I;
            if (w.Current == WeatherKind.Karli) risk *= 2f;
            else if (w.IsRainy) risk *= 1.5f;
            if (c.V.exposed && c.Has(EquipmentKind.Kask)) risk *= 0.4f;
            if (Random.value >= risk) return;

            c.SetOnLeave(2);
            int cost = accidentTreatmentCost;
            string helmet = c.Has(EquipmentKind.Kask) ? " Kask sayesinde yaralanma hafif." : "";
            EventManager.I.Push(new GameEvent("Kaza!",
                    $"{c.courierName} ({c.V.name}) küçük bir kaza geçirdi ve 2 gün çalışamayacak.{helmet}")
                .Add($"Tedavi ve onarımı karşıla (−{Ft(cost)}, moral +)", () =>
                {
                    EconomyManager.I.Spend(cost, $"Kaza masrafı: {c.courierName}");
                    c.morale = Mathf.Min(1f, c.morale + 0.15f);
                })
                .Add("Karşılama (moral −)", () => c.morale = Mathf.Max(0f, c.morale - 0.25f)));
        }

        void RollRaiseRequest()
        {
            if (Random.value > 0.2f) return;
            var eligible = new List<Courier>();
            foreach (var c in Couriers) if (c.deliveries >= 15) eligible.Add(c);
            if (eligible.Count == 0) return;

            var k = eligible[Random.Range(0, eligible.Count)];
            EventManager.I.Push(new GameEvent("Zam talebi",
                    $"{k.courierName} toplam {k.deliveries} teslimat yaptı ve teslimat başına +100 Ft zam istiyor.")
                .Add("Zam ver (+100 Ft/teslimat, moral +)", () =>
                {
                    k.extraPay += 100;
                    k.morale = Mathf.Min(1f, k.morale + 0.2f);
                })
                .Add("Reddet (moral −)", () => k.morale = Mathf.Max(0f, k.morale - 0.15f)));
        }
    }
}
