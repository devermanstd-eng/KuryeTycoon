using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    public enum CourierState { Bosta, DukkanaGidiyor, Bekliyor, MusteriyeGidiyor, Mola, Izinli }

    /// <summary>
    /// Haritada hareket eden tek bir kurye.
    /// Moral ve yorgunluk; aynı anda kaç paket aldığını ve sipariş reddetme ihtimalini belirler.
    /// Çok yorulunca kendisi mola verir.
    /// </summary>
    public class Courier : MonoBehaviour
    {
        [Header("Kimlik")]
        public string courierName;
        public VehicleKind vehicle;

        [Header("Yetenekler")]
        public float speedSkill;   // 0.85–1.35
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
        public int extraPay;       // paket başı ek ücret (deneyim / zam)
        public int unavailableDays;
        public long totalEarned;
        public string leaveReason = "";
        public int weekDeliveries;          // haftalık hedef için
        public bool weeklyRewardClaimed;

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
        float breakUntil;          // > 0 ise süreli mola (telefon şarjı)
        string breakReason = "";
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
            level = c.level;
            extraPay = c.askExtraPay;
            Position = start;
            transform.position = CityMap.ToWorld(Position);

            ring = SpriteFactory.Create("Halka", transform, true, CompanyProfile.Secondary, Vector3.zero, 0.75f, 10);
            body = SpriteFactory.Create("Gövde", transform, true, CompanyProfile.Primary, Vector3.zero, 0.55f, 11);
            State = CourierState.Bosta;
        }

        public bool Has(EquipmentKind k) => equipment.Contains(k);
        public int OrderCount => queue.Count + (current != null ? 1 : 0);
        public bool Available => State != CourierState.Mola && State != CourierState.Izinli;

        /// <summary>
        /// Aynı anda taşıyabileceği paket: araç kapasitesi,
        /// moral yüksekse +1, moral düşükse −1, yorgunsa −1 (en az 1).
        /// </summary>
        public int EffectiveCapacity
        {
            get
            {
                int cap = V.capacity;
                if (morale >= 0.8f) cap += 1;
                if (morale < 0.35f) cap -= 1;
                if (fatigue >= 0.6f) cap -= 1;
                if (CampaignManager.I != null && CampaignManager.I.BoostActive) cap += 1; // boost saatinde daha çok paket alır
                return Mathf.Max(1, cap);
            }
        }

        public bool CanTakeMore => Available && OrderCount < EffectiveCapacity;

        /// <summary>Düşük moral ve yüksek yorgunlukta sipariş reddetme ihtimali.</summary>
        public float RejectChance =>
            (CampaignManager.I != null && CampaignManager.I.BoostActive) ? 0f // boost saatinde reddetmez
            : Mathf.Clamp01((0.45f - morale) * 1.2f) + (fatigue >= 0.75f ? 0.15f : 0f);

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
                if (fatigue >= 0.9f)
                {
                    StartBreak(0f, "Çok yorgun, mola veriyor");
                    EventManager.I.Notify($"{courierName} çok yoruldu ve mola verdi.");
                }
                else if (State != CourierState.Mola)
                {
                    State = CourierState.Bosta;
                }
                return;
            }
            current = queue.Dequeue();
            State = CourierState.DukkanaGidiyor;
            GoTo(current.shop.door);
        }

        /// <summary>minutes = 0 ise yorgunluk %30'a inene kadar sürer.</summary>
        void StartBreak(float minutes, string reason)
        {
            breakUntil = minutes > 0f ? GameClock.I.TotalMinutes + minutes : 0f;
            breakReason = reason;
            State = CourierState.Mola;
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
                    case CourierState.Mola:
                        fatigue = Mathf.Max(0f, fatigue - dm * 0.004f);
                        bool done = breakUntil > 0f ? clock.TotalMinutes >= breakUntil : fatigue <= 0.3f;
                        if (done)
                        {
                            breakUntil = 0f;
                            State = CourierState.Bosta;
                            if (queue.Count > 0) StartNext();
                        }
                        break;
                    case CourierState.Bosta:
                        fatigue = Mathf.Max(0f, fatigue - dm * 0.0015f);
                        break;
                }
            }

            transform.position = CityMap.ToWorld(Position);
            bool off = State == CourierState.Mola || State == CourierState.Izinli;
            ring.color = CompanyProfile.Secondary;
            body.color = off ? Color.Lerp(CompanyProfile.Primary, Color.gray, 0.6f) : CompanyProfile.Primary;
        }

        void Work(float dm)
        {
            float rate = 0.0011f / Mathf.Max(0.3f, stamina);
            if (V.exposed && WeatherSystem.I.IsHot && !Has(EquipmentKind.YazTisort)) rate *= 1.5f;
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

                // Powerbank yoksa telefon kapanabilir (elinde başka paket yoksa)
                if (!Has(EquipmentKind.Powerbank) && queue.Count == 0 && Random.value < 0.05f)
                {
                    StartBreak(60f, "Telefonu kapandı, şarj ediyor");
                    EventManager.I.Notify($"{courierName}'ın telefonu kapandı, 1 saat çevrimdışı. (Powerbank alınmalı)");
                    return;
                }
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

        public void SetOnLeave(int days, string reason)
        {
            unavailableDays = days;
            leaveReason = reason;
            State = CourierState.Izinli;
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

        /// <summary>Eksik ekipman yüzünden şu an yaşanan kayıplar (arayüzde gösterilir).</summary>
        public List<string> ActivePenalties()
        {
            var list = new List<string>();
            var w = WeatherSystem.I;
            var season = GameClock.I.Season;

            if (V.exposed)
            {
                if (w.Current == WeatherKind.Karli && !Has(EquipmentKind.KisMontu)) list.Add("Kar yağıyor, montu yok: %30 yavaş");
                else if (w.Current == WeatherKind.Soguk && !Has(EquipmentKind.KisMontu)) list.Add("Hava soğuk, montu yok: %10 yavaş");
                if (season == Season.Kis && !Has(EquipmentKind.KisMontu)) list.Add("Kışın montsuz: her gün moral düşüyor, %10 hastalanma riski");
                if (w.IsRainy && !Has(EquipmentKind.Yagmurluk)) list.Add("Yağmur var, yağmurluğu yok: %18 yavaş, paket ıslanabilir");
                if ((season == Season.Ilkbahar || season == Season.Sonbahar) && !Has(EquipmentKind.Yagmurluk)) list.Add("Yağmur sezonu, yağmurluğu yok: %4 hastalanma riski");
                if (w.IsHot && !Has(EquipmentKind.YazTisort)) list.Add("Sıcak, tişörtü yok: %8 yavaş, 1,5 kat hızlı yoruluyor");
                if (season == Season.Yaz && !Has(EquipmentKind.YazTisort)) list.Add("Yazın tişörtsüz: her gün moral düşüyor");
                if (!Has(EquipmentKind.Kask)) list.Add("Kask yok: kaza riski yüksek, kazada 3 gün izin");
            }
            if (!Has(EquipmentKind.TermalCanta)) list.Add("Termal çanta yok: 2 km üstü sıcak yemek soğuk gidiyor");
            if (!Has(EquipmentKind.Powerbank)) list.Add("Powerbank yok: telefonu kapanabilir");
            return list;
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
                    case CourierState.Mola: return "Mola: " + breakReason;
                    case CourierState.Izinli: return $"İzinli ({leaveReason}, {unavailableDays} gün)";
                    default: return "Boşta";
                }
            }
        }
    }
}
