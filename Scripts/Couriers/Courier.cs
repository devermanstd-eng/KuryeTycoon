using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    public enum CourierState { Cevrimdisi, Bosta, DukkanaGidiyor, Bekliyor, MusteriyeGidiyor, Mola, Izinli }

    /// <summary>Kuryenin günlük hayatı: genelde hangi saatlerde çalışmak ister.</summary>
    public enum WorkRoutine { TamGun, Sabahci, Ogrenci, OgleAksam, GeceKusu }

    /// <summary>
    /// Serbest çalışan kurye: istediği zaman çevrimiçi / çevrimdışı olur.
    /// Kararını çalışma düzeni, yorgunluk, moral, izin günleri ve boostlar belirler.
    /// Moral ve yorgunluk aynı anda kaç paket aldığını ve reddetme ihtimalini de etkiler.
    /// </summary>
    public class Courier : MonoBehaviour
    {
        [Header("Kimlik")]
        public string courierName;
        public VehicleKind vehicle;
        public WorkRoutine routine;

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
        public int extraPay;       // paket başı ek ücret (deneyim)
        public int unavailableDays;
        public long totalEarned;
        public string leaveReason = "";
        public bool dayOff;                 // bugün hiç çalışmayacak
        public float onlineMinutesToday;
        public int weekDeliveries;          // haftalık hedef için
        public bool weeklyRewardClaimed;
        public int monthDeliveries;         // aylık hedef için
        public bool monthlyRewardClaimed;

        public readonly HashSet<EquipmentKind> equipment = new HashSet<EquipmentKind>();

        public CourierState State { get; private set; }
        public Vector2 Position { get; private set; }
        public Order CurrentOrder => current;
        public IEnumerable<Order> QueuedOrders => queue;
        public VehicleDef V => GameData.Vehicle(vehicle);
        public string ShortName => courierName.Split(' ')[0];
        public bool IsOnline => State != CourierState.Cevrimdisi && State != CourierState.Izinli;

        readonly Queue<Order> queue = new Queue<Order>();
        Order current;
        List<Vector2Int> path;
        int pathIndex;
        float breakUntil;          // telefon şarjı gibi süreli mola
        string breakReason = "";
        string offlineReason = "";
        bool wantsOffline;         // elindeki paketler bitince çevrimdışı olacak
        string wantsOfflineReason = "";
        int lastDecisionHour = -1;
        SpriteRenderer body;
        SpriteRenderer ring;

        public void Init(CourierCandidate c, Vector2Int start)
        {
            courierName = c.name;
            vehicle = c.vehicle;
            routine = c.routine;
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

            // İşe başladığı saatte düzenine uygunsa hemen çevrimiçi olur
            bool online = RoutinePreference(GameClock.I.Hour, GameClock.I.IsWeekend) >= 0.5f;
            State = online ? CourierState.Bosta : CourierState.Cevrimdisi;
            offlineReason = online ? "" : "Çalışma saati değil";
            lastDecisionHour = GameClock.I.Hour;
        }

        // ------------------------------------------------------------------ çalışma düzeni

        public string RoutineName => RoutineLabel(routine);

        public static string RoutineLabel(WorkRoutine r)
        {
            {
                switch (r)
                {
                    case WorkRoutine.Sabahci: return "Sabahçı (genelde 06–15)";
                    case WorkRoutine.Ogrenci: return "Öğrenci (hafta içi 17–01, hafta sonu gündüz)";
                    case WorkRoutine.OgleAksam: return "Öğle + akşam yoğunluğu (11–14, 17–22)";
                    case WorkRoutine.GeceKusu: return "Gece kuşu (genelde 20–04)";
                    default: return "Tam gün (genelde 09–21)";
                }
            }
        }

        /// <summary>Bu saatte çalışmayı ne kadar ister (0–1).</summary>
        public float RoutinePreference(int h, bool weekend)
        {
            switch (routine)
            {
                case WorkRoutine.Sabahci:
                    return (h >= 6 && h < 15) ? 0.9f : (h >= 15 && h < 17 ? 0.35f : 0.05f);
                case WorkRoutine.Ogrenci:
                    if (weekend) return (h >= 11 && h < 23) ? 0.85f : 0.08f;
                    return (h >= 17 || h < 1) ? 0.9f : (h >= 12 && h < 14 ? 0.35f : 0.05f);
                case WorkRoutine.OgleAksam:
                    return ((h >= 11 && h < 14) || (h >= 17 && h < 22)) ? 0.9f : 0.08f;
                case WorkRoutine.GeceKusu:
                    return (h >= 20 || h < 4) ? 0.9f : (h >= 16 && h < 20 ? 0.35f : 0.05f);
                default:
                    return (h >= 9 && h < 21) ? 0.9f : ((h >= 7 && h < 23) ? 0.35f : 0.05f);
            }
        }

        /// <summary>Her oyun saatinde kurye çalışıp çalışmayacağına kendisi karar verir.</summary>
        void DecideAvailability()
        {
            if (State == CourierState.Izinli || State == CourierState.Mola) return;
            var clock = GameClock.I;
            bool boost = CampaignManager.I != null && CampaignManager.I.BoostActive;

            if (dayOff)
            {
                if (IsOnline) RequestOffline("Bugün izin günü");
                return;
            }

            float pref = RoutinePreference(clock.Hour, clock.IsWeekend);

            if (IsOnline)
            {
                float pOff = (1f - pref) * 0.7f
                             + (fatigue >= 0.8f ? 0.6f : 0f)
                             + (morale < 0.3f ? 0.15f : 0f)
                             - (boost ? 0.35f : 0f);
                if (Random.value < pOff)
                    RequestOffline(fatigue >= 0.8f ? "Yoruldu, eve gitti" : "Günlük hayatına döndü");
            }
            else
            {
                if (fatigue > 0.5f) return; // önce dinlenmesi lazım
                float pOn = pref * (0.4f + morale * 0.6f) + (boost ? 0.35f : 0f);
                if (Random.value < pOn) GoOnline(boost);
            }
        }

        void RequestOffline(string reason)
        {
            if (OrderCount > 0)
            {
                wantsOffline = true;
                wantsOfflineReason = reason;
                return;
            }
            GoOffline(reason);
        }

        void GoOffline(string reason)
        {
            wantsOffline = false;
            offlineReason = reason;
            path = null;
            State = CourierState.Cevrimdisi;
        }

        void GoOnline(bool boost)
        {
            offlineReason = "";
            State = CourierState.Bosta;
            if (boost) EventManager.I.Notify($"{ShortName} boost için çevrimiçi oldu.");
        }

        // ------------------------------------------------------------------ kapasite / reddetme

        public bool Has(EquipmentKind k) => equipment.Contains(k);
        public int OrderCount => queue.Count + (current != null ? 1 : 0);
        public bool Available => IsOnline && State != CourierState.Mola && !wantsOffline;

        /// <summary>
        /// Aynı anda taşıyabileceği paket: araç kapasitesi,
        /// moral yüksekse +1, moral düşükse −1, yorgunsa −1, boost saatinde +1 (en az 1).
        /// </summary>
        public int EffectiveCapacity
        {
            get
            {
                int cap = V.capacity;
                if (morale >= 0.8f) cap += 1;
                if (morale < 0.35f) cap -= 1;
                if (fatigue >= 0.6f) cap -= 1;
                if (CampaignManager.I != null && CampaignManager.I.BoostActive) cap += 1;
                return Mathf.Max(1, cap);
            }
        }

        public bool CanTakeMore => Available && OrderCount < EffectiveCapacity;

        /// <summary>Aracı bu siparişin ağırlığını taşıyabilir mi?</summary>
        public bool CanCarry(Order o) => o.weightKg <= V.maxWeightKg;
        public bool CanCarry(float kg) => kg <= V.maxWeightKg;

        /// <summary>Düşük moral ve yüksek yorgunlukta sipariş reddetme ihtimali. Boost saatinde reddetmez.</summary>
        public float RejectChance =>
            (CampaignManager.I != null && CampaignManager.I.BoostActive) ? 0f
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

        // ------------------------------------------------------------------ teslimat akışı

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
                if (wantsOffline) GoOffline(wantsOfflineReason);
                else if (fatigue >= 0.95f) GoOffline("Çok yoruldu, eve gitti");
                else if (State != CourierState.Mola) State = CourierState.Bosta;
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
                if (clock.Hour != lastDecisionHour)
                {
                    lastDecisionHour = clock.Hour;
                    DecideAvailability();
                }
                if (IsOnline) onlineMinutesToday += dm;

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
                        fatigue = Mathf.Max(0f, fatigue - dm * 0.002f);
                        if (clock.TotalMinutes >= breakUntil)
                        {
                            State = CourierState.Bosta;
                            if (queue.Count > 0) StartNext();
                        }
                        break;
                    case CourierState.Bosta:
                        fatigue = Mathf.Max(0f, fatigue - dm * 0.0015f);
                        break;
                    case CourierState.Cevrimdisi:
                    case CourierState.Izinli:
                        fatigue = Mathf.Max(0f, fatigue - dm * 0.006f);
                        break;
                }
            }

            transform.position = CityMap.ToWorld(Position);
            ring.color = IsOnline ? CompanyProfile.Secondary : new Color(0.5f, 0.5f, 0.5f, 0.35f);
            var p = CompanyProfile.Primary;
            body.color = IsOnline ? (State == CourierState.Mola ? Color.Lerp(p, Color.gray, 0.5f) : p)
                                  : new Color(p.r * 0.5f, p.g * 0.5f, p.b * 0.5f, 0.35f);
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
                if (!Has(EquipmentKind.Powerbank) && queue.Count == 0 && !wantsOffline && Random.value < 0.05f)
                {
                    breakUntil = GameClock.I.TotalMinutes + 60f;
                    breakReason = "Telefonu kapandı, şarj ediyor";
                    State = CourierState.Mola;
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

        /// <summary>Gece yarısı çağrılır. Bugün izin günü mü, kurye kendisi karar verir.</summary>
        public void NewDay(bool weekend)
        {
            deliveriesToday = 0;
            onlineMinutesToday = 0f;

            if (unavailableDays > 0)
            {
                unavailableDays--;
                if (unavailableDays == 0 && State == CourierState.Izinli) GoOffline("İyileşti, dinleniyor");
            }

            float chance = weekend ? 0.2f : 0.1f;
            if (morale < 0.4f) chance *= 1.5f;
            dayOff = Random.value < chance;
        }

        // ------------------------------------------------------------------ arayüz

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
                    case CourierState.Cevrimdisi: return "Çevrimdışı: " + offlineReason;
                    case CourierState.DukkanaGidiyor: return "Dükkana gidiyor";
                    case CourierState.Bekliyor: return "Dükkanda bekliyor";
                    case CourierState.MusteriyeGidiyor: return wantsOffline ? "Son paketini götürüyor, sonra çıkacak" : "Müşteriye gidiyor";
                    case CourierState.Mola: return "Mola: " + breakReason;
                    case CourierState.Izinli: return $"İzinli ({leaveReason}, {unavailableDays} gün)";
                    default: return "Çevrimiçi, sipariş bekliyor";
                }
            }
        }
    }
}
