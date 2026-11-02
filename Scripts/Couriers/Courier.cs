using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    public enum CourierState { Cevrimdisi, Bosta, DukkanaGidiyor, Bekliyor, MusteriyeGidiyor, AdresSorunu, Mola, Izinli }

    /// <summary>Kuryenin asıl hayatı: ne zaman kuryelik yapabileceğini belirler.</summary>
    public enum Occupation { TamZamanli, OfisCalisani, HaftaSonu, Ogrenci, Vardiyali, Emekli }

    /// <summary>
    /// Serbest çalışan kurye.
    /// • Mesleğine, yorgunluğuna, moraline ve günlük hayatına göre kendisi çevrimiçi/çevrimdışı olur.
    /// • Kazancının bir kısmını biriktirir; parası yetince kendi cebinden daha iyi araç alır.
    /// • İsterse şirketin platformu üzerinden, kendi parasıyla sigorta yaptırır.
    /// </summary>
    public class Courier : MonoBehaviour
    {
        /// <summary>Kazancın ne kadarını biriktirir (araç almak için).</summary>
        public const float SavingRate = 0.3f;
        /// <summary>Sigorta primi (haftalık, kuryenin kendi cebinden).</summary>
        public const int InsurancePremium = 2500;

        [Header("Kimlik")]
        public string courierName;
        public VehicleKind vehicle;            // şu an kullandığı araç
        public Occupation occupation;

        [Header("Yetenekler")]
        public float speedSkill;
        public float care;
        public float politeness;
        public float stamina;
        public float tidiness;     // titizlik: çantasını ne sıklıkla kendisi temizler
        public float pickiness;    // seçicilik: az kazandıran / uzak teklifleri reddetme eğilimi (0–1)

        [Header("Durum")]
        public float fatigue;
        public float morale = 0.7f;
        public int deliveries;
        public int deliveriesToday;
        public int level = 1;
        public float xp;
        public int unavailableDays;
        public long totalEarned;
        public long tipsTotal;             // müşterilerden aldığı bahşişler (tamamen kendisinin)
        public long tipsToday;

        [Header("Hijyen")]
        public float hygiene = 1f;             // çanta temizliği (0–1); %60 altı müşteri şikayeti
        public int bagUses;                    // mevcut çantayla yapılan teslimat (yıpranma)
        public bool cleanRequested;            // şirket temizlik hatırlatması gönderdi
        public string leaveReason = "";
        public bool dayOff;
        public float onlineMinutesToday;
        public int weekDeliveries;
        public bool weeklyRewardClaimed;
        public int monthDeliveries;
        public bool monthlyRewardClaimed;
        public int pickupCancelsThisMonth;      // dükkanda sıra yüzünden teslim almayı iptal (ayda en fazla 10)
        public const int MaxPickupCancels = 10;

        [Header("Kendi parası")]
        public long savings;                   // araç almak için birikim
        public bool insured;                   // kendi parasıyla sigortalı mı
        public long insurancePaid;             // bugüne kadar ödediği prim
        public int repairDays;                 // aracı tamirde (gün)
        public VehicleKind repairingVehicle;

        public VehicleKind dreamVehicle;       // parası olursa almak istediği araç (kendisininkiyle aynıysa hiç yükseltmez)

        [Header("Şirket ekipmanı")]
        public bool hasCompanyKit;             // başlangıç kitini taşıyor
        public bool depositPaid;               // 20 000 Ft depozito kesildi mi

        public readonly HashSet<VehicleKind> ownedVehicles = new HashSet<VehicleKind>();
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
        float breakUntil;
        string breakReason = "";
        string offlineReason = "";
        bool wantsOffline;
        string wantsOfflineReason = "";
        bool vehicleSwitchPending;
        int lastDecisionHour = -1;
        SpriteRenderer body;
        SpriteRenderer ring;

        public void Init(CourierCandidate c, Vector2Int start)
        {
            courierName = c.name;
            vehicle = c.vehicle;
            occupation = c.occupation;
            speedSkill = c.speedSkill;
            care = c.care;
            politeness = c.politeness;
            stamina = c.stamina;
            tidiness = c.tidiness;
            pickiness = c.pickiness;
            level = c.level;
            insured = c.insured;
            savings = c.savings;
            ownedVehicles.Add(c.vehicle);
            dreamVehicle = c.dreamVehicle;
            if (c.alsoOwnsBike) ownedVehicles.Add(VehicleKind.Bisiklet);
            Position = start;
            transform.position = CityMap.ToWorld(Position);

            ring = SpriteFactory.Create("Halka", transform, true, CompanyProfile.Secondary, Vector3.zero, 0.75f, 10);
            body = SpriteFactory.Create("Gövde", transform, true, CompanyProfile.Primary, Vector3.zero, 0.55f, 11);

            bool online = Preference() >= 0.5f;
            State = online ? CourierState.Bosta : CourierState.Cevrimdisi;
            offlineReason = online ? "" : OccupationStatus;
            lastDecisionHour = GameClock.I.Hour;
        }

        // ================================================================== meslek / günlük hayat

        public static string OccupationLabel(Occupation o)
        {
            switch (o)
            {
                case Occupation.OfisCalisani: return "Ofis çalışanı (hafta içi 17:00'den sonra)";
                case Occupation.HaftaSonu: return "Sadece hafta sonu çalışır";
                case Occupation.Ogrenci: return "Öğrenci (derslere ve sınavlara göre)";
                case Occupation.Vardiyali: return "Vardiyalı fabrika işçisi (vardiyası haftalık değişir)";
                case Occupation.Emekli: return "Emekli (sabahları)";
                default: return "Tam zamanlı kurye (her gün çalışabilir)";
            }
        }

        public string OccupationName => OccupationLabel(occupation);

        public static bool IsExamPeriod(GameClock c)
        {
            int m = c.Month, d = c.DayOfMonth;
            return (m == 1 && d <= 20) || (m == 4 && d <= 10) || (m == 6 && d <= 20) || (m == 11 && d >= 10 && d <= 20);
        }

        public static bool IsSchoolHoliday(GameClock c)
        {
            int m = c.Month, d = c.DayOfMonth;
            return m == 7 || m == 8 || (m == 1 && d > 20) || (m == 2 && d <= 10) || (m == 12 && d >= 24);
        }

        /// <summary>Vardiyalı: çift haftalarda fabrikada sabah vardiyası (kuryelik akşam), tek haftalarda tersi.</summary>
        static bool FactoryMorningWeek(GameClock c) => ((c.CompanyDay - 1) / 7) % 2 == 0;

        /// <summary>Mesleğinin bugünkü durumu (arayüzde gösterilir).</summary>
        public string OccupationStatus
        {
            get
            {
                var c = GameClock.I;
                switch (occupation)
                {
                    case Occupation.Ogrenci:
                        if (IsExamPeriod(c)) return "Sınav haftası, ders çalışıyor";
                        if (IsSchoolHoliday(c)) return "Okul tatilde, her gün çalışabilir";
                        return c.IsWeekend ? "Hafta sonu, okul yok" : "Ders dönemi, akşamları çalışır";
                    case Occupation.OfisCalisani:
                        return c.IsWeekend ? "Hafta sonu, ofis kapalı" : (c.Hour < 17 ? "Ofiste, mesai 17:00'de bitiyor" : "Mesai bitti");
                    case Occupation.HaftaSonu:
                        return c.IsWeekend ? "Hafta sonu, çalışma günü" : "Hafta içi, kendi işinde";
                    case Occupation.Vardiyali:
                        return FactoryMorningWeek(c) ? "Bu hafta fabrikada sabah vardiyası (15:00 sonrası kuryelik)"
                                                     : "Bu hafta fabrikada akşam vardiyası (sabahları kuryelik)";
                    case Occupation.Emekli:
                        return "Emekli, sabahları çalışmayı sever";
                    default:
                        return "Geçimini kuryelikten sağlıyor, her gün çalışabilir";
                }
            }
        }

        /// <summary>Şu an kuryelik yapmayı ne kadar ister (0–1).</summary>
        public float Preference()
        {
            var c = GameClock.I;
            int h = c.Hour;
            bool we = c.IsWeekend;
            switch (occupation)
            {
                case Occupation.OfisCalisani:
                    if (we) return (h >= 11 && h < 20) ? 0.5f : 0.05f;
                    return (h >= 17 && h < 23) ? 0.85f : 0.03f;

                case Occupation.HaftaSonu:
                    return we ? ((h >= 9 && h < 23) ? 0.9f : 0.05f) : 0.02f;

                case Occupation.Ogrenci:
                    if (IsExamPeriod(c)) return (h >= 20 && h < 23) ? 0.2f : 0.03f;
                    if (IsSchoolHoliday(c) || we) return (h >= 10 && h < 23) ? 0.85f : 0.05f;
                    if (h >= 16 && h < 24) return 0.8f;
                    return (h >= 12 && h < 14) ? 0.3f : 0.03f; // dersler arasında

                case Occupation.Vardiyali:
                    if (we) return (h >= 10 && h < 20) ? 0.6f : 0.05f;
                    if (FactoryMorningWeek(c)) return (h >= 15 && h < 23) ? 0.85f : 0.03f;
                    return (h >= 7 && h < 13) ? 0.85f : 0.03f;

                case Occupation.Emekli:
                    if (we) return (h >= 9 && h < 13) ? 0.3f : 0.03f;
                    return (h >= 8 && h < 14) ? 0.85f : ((h >= 14 && h < 17) ? 0.35f : 0.03f);

                default:
                    return (h >= 9 && h < 21) ? 0.9f : ((h >= 7 && h < 23) ? 0.35f : 0.05f);
            }
        }

        /// <summary>Her oyun saatinde kurye çalışıp çalışmayacağına kendisi karar verir.</summary>
        void DecideAvailability()
        {
            if (State == CourierState.Izinli || State == CourierState.Mola) return;
            bool boost = CampaignManager.I != null && CampaignManager.I.BoostActive;

            if (dayOff)
            {
                if (IsOnline) RequestOffline("Bugün izin günü");
                return;
            }

            float pref = Preference();

            if (IsOnline)
            {
                float pOff = (1f - pref) * 0.7f
                             + (fatigue >= 0.8f ? 0.6f : 0f)
                             + (morale < 0.3f ? 0.15f : 0f)
                             - (boost ? 0.35f : 0f);
                if (Random.value < pOff)
                    RequestOffline(fatigue >= 0.8f ? "Yoruldu, eve gitti" : OccupationStatus);
            }
            else
            {
                if (fatigue > 0.5f) return;
                // Boost, sadece zaten müsait olabilecek kuryeleri çeker (sınavdaki öğrenciyi değil)
                float pOn = pref * (0.4f + morale * 0.6f) + (boost && pref >= 0.2f ? 0.35f : 0f);
                if (Random.value < pOn)
                {
                    GoOnline();
                    if (boost && pref < 0.5f) EventManager.I.Notify($"{ShortName} boost için çevrimiçi oldu.");
                }
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

        void GoOnline()
        {
            offlineReason = "";
            State = CourierState.Bosta;
        }

        // ================================================================== para / araç / sigorta

        /// <summary>Kurye kazanır; bir kısmını biriktirir.</summary>
        public void Earn(long amount)
        {
            totalEarned += amount;
            savings += (long)(amount * SavingRate);

            // Toplam kazancı eşiğe ulaşınca ekipman depozitosu kesilir
            if (hasCompanyKit && !depositPaid && totalEarned >= GameData.DepositThreshold)
                EconomyManager.I.CollectDeposit(this);
        }

        /// <summary>Tamirde olmayan en iyi aracı.</summary>
        VehicleKind BestUsableVehicle(out bool any)
        {
            any = false;
            VehicleKind best = VehicleKind.Bisiklet;
            foreach (var v in ownedVehicles)
            {
                if (repairDays > 0 && v == repairingVehicle) continue;
                if (!any || (int)v > (int)best) best = v;
                any = true;
            }
            return best;
        }

        /// <summary>Kullandığı aracı duruma göre günceller (elinde paket varken bekler).</summary>
        public void RefreshVehicle()
        {
            if (OrderCount > 0)
            {
                vehicleSwitchPending = true;
                return;
            }
            vehicleSwitchPending = false;
            var best = BestUsableVehicle(out bool any);
            if (!any)
            {
                if (State != CourierState.Izinli) SetOnLeave(Mathf.Max(1, repairDays), "aracı tamirde");
                return;
            }
            vehicle = best;
        }

        /// <summary>
        /// Araç yükseltmek zorunlu değil: kurye sadece istediği bir araç varsa
        /// ve parası yetiyorsa onu (ara basamakları atlayarak) kendi cebinden alır.
        /// </summary>
        public bool TryBuyDreamVehicle()
        {
            if (ownedVehicles.Contains(dreamVehicle)) return false;
            var def = GameData.Vehicle(dreamVehicle);
            if (savings < def.ownPrice) return false;
            savings -= def.ownPrice;
            ownedVehicles.Add(dreamVehicle);
            RefreshVehicle();
            return true;
        }

        /// <summary>Kullandığı araç arızalanır; tamir parası kuryenin cebinden.</summary>
        public void BreakDown(int days, int repairCost)
        {
            repairingVehicle = vehicle;
            repairDays = days;
            savings = System.Math.Max(0L, savings - repairCost);
            RefreshVehicle();
        }

        /// <summary>Haftalık sigorta primi (kendi cebinden).</summary>
        public bool PayInsurance()
        {
            if (!insured) return false;
            if (savings < InsurancePremium)
            {
                insured = false; // ödeyemedi, sigortası düştü
                return false;
            }
            savings -= InsurancePremium;
            insurancePaid += InsurancePremium;
            return true;
        }

        public bool TryBuyInsurance()
        {
            if (insured || savings < InsurancePremium) return false;
            insured = true;
            savings -= InsurancePremium;
            insurancePaid += InsurancePremium;
            return true;
        }

        // ================================================================== kapasite / reddetme

        public bool Has(EquipmentKind k) => equipment.Contains(k);
        public int OrderCount => queue.Count + (current != null ? 1 : 0);
        public bool Available => IsOnline && State != CourierState.Mola && !wantsOffline;

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
        public bool CanCarry(Order o) => o.weightKg <= V.maxWeightKg;
        public bool CanCarry(float kg) => kg <= V.maxWeightKg;

        public float RejectChance =>
            (CampaignManager.I != null && CampaignManager.I.BoostActive) ? 0f
            : Mathf.Clamp01((0.45f - morale) * 1.2f) + (fatigue >= 0.75f ? 0.15f : 0f);

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

        public float Speed =>
            V.tilesPerMinute * speedSkill * (1f + 0.04f * (level - 1)) *
            WeatherSystem.I.SpeedFactor(this) * (1f - 0.3f * fatigue) * TrafficFactor;

        // ================================================================== teslimat akışı

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
                if (vehicleSwitchPending) RefreshVehicle();
                if (State == CourierState.Izinli) return;
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
                    case CourierState.AdresSorunu:
                        Work(dm);
                        if (clock.TotalMinutes >= issueWaitUntil) OrderManager.I.ResolveAddressIssue(current, this);
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
                float now = GameClock.I.TotalMinutes;

                // Dükkanda bazen sıra olur
                if (Random.value < 0.25f)
                {
                    current.readyAt = Mathf.Max(current.readyAt, now) + Random.Range(5f, 20f);
                    current.shopQueue = true;
                }

                if (now >= current.readyAt) { PickUp(); return; }

                // Uzun bekleme: kurye teslim almayı iptal edebilir (ayda en fazla 10 kez)
                float wait = current.readyAt - now;
                if (wait > 8f && pickupCancelsThisMonth < MaxPickupCancels &&
                    Random.value < Mathf.Clamp01((wait - 8f) / 20f) * Mathf.Lerp(0.3f, 1f, pickiness))
                {
                    CancelPickup(wait);
                    return;
                }
                State = CourierState.Bekliyor;
            }
            else if (State == CourierState.MusteriyeGidiyor)
            {
                // Adreste sorun var mı? (yanlış işaretlenmiş adres / müşteri evde yok)
                if (current.addressIssue != AddressIssue.Yok && !current.issueResolved)
                {
                    State = CourierState.AdresSorunu;
                    issueWaitUntil = GameClock.I.TotalMinutes + 10f; // kapıda bekler, arar
                    EventManager.I.Notify(current.addressIssue == AddressIssue.YanlisAdres
                        ? $"{ShortName} #{current.id} adresine vardı ama adres yanlış işaretlenmiş, müşteriye ulaşmaya çalışıyor."
                        : $"{ShortName} #{current.id} adresine vardı ama müşteri evde değil, bekliyor.");
                    return;
                }
                DeliverCurrent(false);
            }
        }

        float issueWaitUntil;

        void CancelPickup(float wait)
        {
            var o = current;
            current = null;
            pickupCancelsThisMonth++;
            OrderManager.I.ReturnToPending(o, this);
            EventManager.I.Notify($"{ShortName} {o.shop.Name}'da {wait:0} dk sıra olduğu için #{o.id} teslim almayı iptal etti " +
                                  $"(bu ay {pickupCancelsThisMonth}/{MaxPickupCancels}). Sipariş yeni kurye bekliyor.");
            StartNext();
        }

        // ================================================================== teklif değerlendirme

        /// <summary>Teklifin tahmini süresi (dk): dükkana gidiş + hazırlık beklemesi + müşteriye gidiş.</summary>
        public float EstimateMinutes(Order o)
        {
            float speed = Mathf.Max(0.05f, V.tilesPerMinute * speedSkill);
            float toShop = CityMap.Manhattan(FreeTile, o.shop.door) / speed + OrderCount * 12f;
            float wait = Mathf.Max(0f, o.readyAt - (GameClock.I.TotalMinutes + toShop));
            return toShop + wait + o.distance / speed;
        }

        /// <summary>Teklifin tahmini saatlik kazancı.</summary>
        public float OfferHourly(Order o, int pay) => pay / Mathf.Max(5f, EstimateMinutes(o)) * 60f;

        /// <summary>Bu aracın "normal" bir işten saatlik kazancı (8 kare gidiş, 12 kare teslimat, bonus yok).</summary>
        public float ReferenceHourly
        {
            get
            {
                float speed = Mathf.Max(0.05f, V.tilesPerMinute * speedSkill);
                float pay = V.basePay + V.payPerKm * GameData.Km(12);
                return pay / (20f / speed) * 60f;
            }
        }

        /// <summary>Kurye tahmini kazancı görür; seçiciliğine göre beğenmezse reddeder.</summary>
        public bool LikesOffer(Order o, int pay)
        {
            float threshold = Mathf.Lerp(0.55f, 1.0f, pickiness);
            return OfferHourly(o, pay) >= ReferenceHourly * threshold;
        }

        public string PickinessText =>
            pickiness >= 0.75f ? "Çok seçici (az kazandıran/uzak işleri sık reddeder)"
            : pickiness >= 0.45f ? "Normal" : "Pek seçmez (çoğu işi kabul eder)";

        /// <summary>Adres sorunu çözüldü: kurye doğru adrese yönlenir.</summary>
        public void RedirectTo(Vector2Int door)
        {
            if (current == null) return;
            current.customerDoor = door;
            State = CourierState.MusteriyeGidiyor;
            GoTo(door);
        }

        /// <summary>Adreste teslim (sorun çözüldüyse de buradan geçer).</summary>
        public void DeliverHere()
        {
            if (current == null) return;
            DeliverCurrent(false);
        }

        /// <summary>Müşteriye hiç ulaşılamadı: sipariş kuryede kalır (çok nadir).</summary>
        public void KeepOrder()
        {
            if (current == null) return;
            DeliverCurrent(true);
        }

        void DeliverCurrent(bool kept)
        {
            {
                var done = current;
                current = null;
                deliveries++;
                deliveriesToday++;
                AddXp();
                DirtyBag(done);
                if (kept) OrderManager.I.CompleteKept(done, this);
                else OrderManager.I.CompleteDelivery(done, this);

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
                EventManager.I.Notify($"{courierName} tecrübe kazandı (tecrübe {level}): artık daha hızlı.");
            }
        }

        public void SetOnLeave(int days, string reason)
        {
            unavailableDays = Mathf.Max(unavailableDays, days);
            leaveReason = reason;
            State = CourierState.Izinli;
        }

        /// <summary>Gece yarısı çağrılır.</summary>
        public void NewDay(GameClock clock)
        {
            deliveriesToday = 0;
            tipsToday = 0;

            // Çanta temizliği: hatırlatma gönderildiyse kesin, değilse titizliğine göre
            if (cleanRequested || Random.value < tidiness * 0.3f) CleanBag(false);
            onlineMinutesToday = 0f;

            if (repairDays > 0)
            {
                repairDays--;
                if (repairDays == 0)
                {
                    EventManager.I.Notify($"{courierName}'ın {GameData.Vehicle(repairingVehicle).name} tamirden çıktı.");
                    RefreshVehicle();
                }
            }

            if (unavailableDays > 0)
            {
                unavailableDays--;
                if (unavailableDays == 0 && State == CourierState.Izinli) GoOffline("İzinden döndü, dinleniyor");
            }

            // İzin günü: zorunlu değil, kurye canı isterse ara sıra kendine izin verir
            float chance = clock.IsWeekend ? 0.12f : 0.06f;
            if (occupation == Occupation.TamZamanli) chance *= 0.5f; // geçimini kuryelikten sağlıyor
            if (morale < 0.4f) chance *= 1.5f;
            dayOff = Random.value < chance;
        }

        // ================================================================== arayüz

        // ================================================================== hijyen

        public const float HygieneLimit = 0.6f;
        public const int BagWornUses = 400;

        /// <summary>Çok kullanılan çanta yıpranır: temizlense de en fazla %75'e çıkar.</summary>
        public bool BagWorn => bagUses >= BagWornUses;
        public float MaxHygiene => BagWorn ? 0.75f : 1f;

        void DirtyBag(Order o)
        {
            bagUses++;
            float d = 0.012f;
            if (o.shop.def.hotFood) d *= 1.5f;              // yağ, sos
            if (o.IsHeavy || o.weightKg > 10f) d *= 1.3f;
            if (WeatherSystem.I.IsRainy && V.exposed) d *= 1.5f;
            d *= Mathf.Lerp(1.4f, 0.8f, care);              // dikkatsiz kurye daha çok döker
            hygiene = Mathf.Max(0f, hygiene - d);
        }

        /// <summary>Çantayı temizler (kurye kendisi ya da şirket temizletir).</summary>
        public void CleanBag(bool byCompany)
        {
            hygiene = MaxHygiene;
            if (cleanRequested && !byCompany) morale = Mathf.Max(0f, morale - 0.02f); // uyarılmak hoşuna gitmez
            cleanRequested = false;
        }

        /// <summary>Yeni termal çanta: yıpranma sıfırlanır, hijyen tam.</summary>
        public void ReplaceBag()
        {
            bagUses = 0;
            hygiene = 1f;
            cleanRequested = false;
            equipment.Add(EquipmentKind.TermalCanta);
        }

        public string HygieneText =>
            hygiene >= 0.8f ? "Temiz" : hygiene >= HygieneLimit ? "Biraz kirli" : hygiene >= 0.35f ? "Kirli — müşteriler şikayet ediyor" : "Çok kirli — sık şikayet";

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
                if (!Has(EquipmentKind.Kask)) list.Add("Kask yok: kaza riski yüksek, kazada daha uzun süre çalışamaz");
            }
            if (!Has(EquipmentKind.TermalCanta)) list.Add("Termal çanta yok: 2 km üstü sıcak yemek soğuk gidiyor");
            if (!Has(EquipmentKind.Powerbank)) list.Add("Powerbank yok: telefonu kapanabilir");
            if (hygiene < HygieneLimit) list.Add($"Çanta hijyeni %{hygiene * 100f:0}: müşteri şikayeti ve düşük puan riski");
            if (BagWorn) list.Add("Çantası yıprandı: temizlense de tam temiz olmuyor, yeni çanta gerekli");
            return list;
        }

        public string VehiclesText
        {
            get
            {
                string s = "";
                for (int i = (int)VehicleKind.Araba; i >= 0; i--)
                {
                    var k = (VehicleKind)i;
                    if (!ownedVehicles.Contains(k)) continue;
                    string tag = k == vehicle ? " (kullanıyor)" : (repairDays > 0 && k == repairingVehicle ? $" (tamirde, {repairDays} gün)" : "");
                    s += (s.Length > 0 ? ", " : "") + GameData.Vehicle(k).name + tag;
                }
                return s;
            }
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
                    case CourierState.AdresSorunu:
                        return current != null && current.addressIssue == AddressIssue.YanlisAdres
                            ? "Adres yanlış, müşteriye ulaşmaya çalışıyor" : "Müşteri evde yok, kapıda bekliyor";
                    case CourierState.Mola: return "Mola: " + breakReason;
                    case CourierState.Izinli: return $"Çalışamıyor ({leaveReason}, {unavailableDays} gün)";
                    default: return "Çevrimiçi, sipariş bekliyor";
                }
            }
        }
    }
}
