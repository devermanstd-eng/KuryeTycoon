using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    public enum CourierState { Bosta, DukkanaGidiyor, Bekliyor, MusteriyeGidiyor, Dinleniyor, Izinli }

    /// <summary>Haritada hareket eden tek bir kurye.</summary>
    public class Courier : MonoBehaviour
    {
        [Header("Kimlik")]
        public string courierName;
        public VehicleKind vehicle;

        [Header("Yetenekler")]
        public float speedSkill;   // 0.85–1.2
        public float care;         // dikkat: eksik/hasar riski
        public float politeness;   // nezaket: müşteri puanı
        public float stamina;      // dayanıklılık: yorulma hızı

        [Header("Durum")]
        public float fatigue;
        public float morale = 0.7f;
        public int deliveries;
        public int deliveriesToday;
        public int level = 1;
        public float xp;
        public int extraPay;       // zamlar (teslimat başı)
        public int unavailableDays;
        public long totalEarned;

        public readonly HashSet<EquipmentKind> equipment = new HashSet<EquipmentKind>();

        public CourierState State { get; private set; }
        public Vector2 Position { get; private set; }
        public Order CurrentOrder => current;
        public IEnumerable<Order> QueuedOrders => queue;
        public VehicleDef V => GameData.Vehicle(vehicle);
        public string ShortName => courierName.Split(' ')[0];

        readonly Queue<Order> queue = new Queue<Order>();
        Order current;
        List<Vector2Int> path;
        int pathIndex;
        SpriteRenderer body;
        SpriteRenderer ring;

        public void Init(CourierCandidate c, Vector2Int start)
        {
            courierName = c.name;
            vehicle = c.vehicle;
            speedSkill = c.speedSkill;
            care = c.care;
            politeness = c.politeness;
            stamina = c.stamina;
            Position = start;
            transform.position = CityMap.ToWorld(Position);

            ring = SpriteFactory.Create("Halka", transform, true, CompanyProfile.Secondary, Vector3.zero, 0.75f, 10);
            body = SpriteFactory.Create("Gövde", transform, true, CompanyProfile.Primary, Vector3.zero, 0.55f, 11);
            State = CourierState.Bosta;
        }

        public bool Has(EquipmentKind k) => equipment.Contains(k);
        public int OrderCount => queue.Count + (current != null ? 1 : 0);
        public bool Available => State != CourierState.Dinleniyor && State != CourierState.Izinli && fatigue < 0.85f;
        public bool CanTakeMore => Available && OrderCount < V.capacity;

        /// <summary>Elindeki işler bitince nerede olacak (atama tahmini için).</summary>
        public Vector2Int FreeTile
        {
            get
            {
                if (OrderCount == 0) return Vector2Int.RoundToInt(Position);
                Order last = current;
                foreach (var o in queue) last = o;
                return last.customerDoor;
            }
        }

        float TrafficFactor
        {
            get
            {
                if (vehicle != VehicleKind.Araba) return 1f;
                int h = GameClock.I.Hour;
                bool rush = (h >= 7 && h < 9) || (h >= 16 && h < 19);
                return rush ? 0.7f : 1f;
            }
        }

        /// <summary>Kare / oyun dakikası.</summary>
        public float Speed =>
            V.tilesPerMinute * speedSkill * (1f + 0.04f * (level - 1)) *
            WeatherSystem.I.SpeedFactor(this) * (1f - 0.3f * fatigue) * TrafficFactor;

        public void Assign(Order o)
        {
            queue.Enqueue(o);
            if (current == null) StartNext();
        }

        void StartNext()
        {
            if (queue.Count == 0)
            {
                current = null;
                path = null;
                State = fatigue >= 0.85f ? CourierState.Dinleniyor : CourierState.Bosta;
                return;
            }
            current = queue.Dequeue();
            State = CourierState.DukkanaGidiyor;
            GoTo(current.shop.door);
        }

        void GoTo(Vector2Int target)
        {
            path = CityMap.I.FindPath(Vector2Int.RoundToInt(Position), target);
            pathIndex = 0;
        }

        void Update()
        {
            var clock = GameClock.I;
            float dm = clock != null ? clock.DeltaMinutes : 0f;

            if (dm > 0f)
            {
                switch (State)
                {
                    case CourierState.DukkanaGidiyor:
                    case CourierState.MusteriyeGidiyor:
                        Work(dm);
                        if (MoveAlongPath(dm)) Arrived();
                        break;
                    case CourierState.Bekliyor:
                        Work(dm);
                        if (clock.TotalMinutes >= current.readyAt) PickUp();
                        break;
                    case CourierState.Dinleniyor:
                        fatigue = Mathf.Max(0f, fatigue - dm * 0.004f);
                        if (fatigue <= 0.1f) State = CourierState.Bosta;
                        break;
                    case CourierState.Bosta:
                        fatigue = Mathf.Max(0f, fatigue - dm * 0.0015f);
                        break;
                }
            }

            transform.position = CityMap.ToWorld(Position);
            bool off = State == CourierState.Dinleniyor || State == CourierState.Izinli;
            ring.color = CompanyProfile.Secondary;
            body.color = off ? Color.Lerp(CompanyProfile.Primary, Color.gray, 0.6f) : CompanyProfile.Primary;
        }

        void Work(float dm)
        {
            float rate = 0.0011f / Mathf.Max(0.3f, stamina);
            if (Has(EquipmentKind.Powerbank)) rate *= 0.8f;
            fatigue = Mathf.Min(1f, fatigue + rate * dm);
        }

        bool MoveAlongPath(float dm)
        {
            if (path == null || path.Count == 0) return true;
            float remaining = Speed * dm;
            while (remaining > 0f && pathIndex < path.Count)
            {
                Vector2 target = path[pathIndex];
                Vector2 delta = target - Position;
                float d = delta.magnitude;
                if (d <= remaining)
                {
                    Position = target;
                    remaining -= d;
                    pathIndex++;
                }
                else
                {
                    Position += delta / d * remaining;
                    remaining = 0f;
                }
            }
            return pathIndex >= path.Count;
        }

        void Arrived()
        {
            if (State == CourierState.DukkanaGidiyor)
            {
                if (GameClock.I.TotalMinutes >= current.readyAt) PickUp();
                else State = CourierState.Bekliyor;
            }
            else if (State == CourierState.MusteriyeGidiyor)
            {
                var done = current;
                current = null;
                deliveries++;
                deliveriesToday++;
                AddXp();
                OrderManager.I.CompleteDelivery(done, this);
                StartNext();
            }
        }

        void PickUp()
        {
            current.status = OrderStatus.Alindi;
            State = CourierState.MusteriyeGidiyor;
            GoTo(current.customerDoor);
        }

        void AddXp()
        {
            xp += 1f;
            if (xp >= level * 10f)
            {
                xp = 0f;
                level++;
                EventManager.I.Notify($"{courierName} seviye {level} oldu! (daha hızlı)");
            }
        }

        public void SetOnLeave(int days)
        {
            unavailableDays = days;
            State = CourierState.Izinli;
        }

        public void ForceRest()
        {
            if (OrderCount == 0 && State == CourierState.Bosta) State = CourierState.Dinleniyor;
        }

        public void NewDay()
        {
            deliveriesToday = 0;
            if (unavailableDays > 0)
            {
                unavailableDays--;
                if (unavailableDays == 0 && State == CourierState.Izinli) State = CourierState.Bosta;
            }
        }

        public string StateName
        {
            get
            {
                switch (State)
                {
                    case CourierState.DukkanaGidiyor: return "Dükkana gidiyor";
                    case CourierState.Bekliyor: return "Dükkanda bekliyor";
                    case CourierState.MusteriyeGidiyor: return "Müşteriye gidiyor";
                    case CourierState.Dinleniyor: return "Dinleniyor";
                    case CourierState.Izinli: return $"İzinli ({unavailableDays} gün)";
                    default: return "Boşta";
                }
            }
        }
    }
}
