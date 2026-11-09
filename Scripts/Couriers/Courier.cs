using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    public enum CourierState { Cevrimdisi, Bosta, DukkanaGidiyor, Bekliyor, MusteriyeGidiyor, AdresSorunu, Mola, Izinli, KataCikiyor }

    /// <summary>Kuryenin asıl hayatı: ne zaman kuryelik yapabileceğini belirler.</summary>
    public enum Occupation { TamZamanli, OfisCalisani, HaftaSonu, Ogrenci, Vardiyali, Emekli, Ebeveyn, GeceKuryesi }

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
        public int scheduleSeed;               // öğrencinin ders programı (her dönem değişir)

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
        public long earnedToday;           // bugün kazandığı (ücret + boost + bahşiş + ödül)
        public long earnedWeek;            // bu hafta kazandığı (haftalık rapor)

        [Header("Hijyen")]
        public float hygiene = 1f;             // çanta temizliği (0–1); %60 altı müşteri şikayeti
        public int bagUses;                    // mevcut çantayla yapılan teslimat (yıpranma)
        public bool cleanRequested;            // şirket uyardı: elindeki paket bitince çantasını temizler
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
        public bool hasCompanyKit;             // (eski kayıt) başlangıç kitini taşıyor
        public bool depositPaid;               // (eski kayıt) 20 000 Ft depozito kesildi mi

        // Depozitolu şirket ekipmanı: kurye ister, şirket verir (depodan ya da satın alır), bedeli depozito olarak kuryenin
        // kazancından kesilir. Ayrılırken ekipmanı iade ederse depozito geri ödenir, etmezse şirkette kalır.
        public List<EquipmentKind> depositGear = new List<EquipmentKind>();
        public long depositOwed;               // henüz kesilmemiş depozito (kazandıkça kesilir)
        public long depositPaidTotal;          // kesilmiş, şirkette duran depozito
        public float gearNudgeUntil;           // şirket "ekipmanını al" diye hatırlattı (bu zamana kadar daha istekli)

        /// <summary>Eski kayıttaki başlangıç kiti ve sabit depozitoyu yeni depozito sistemine taşır (tekrar çağrılabilir).</summary>
        public void MigrateGear()
        {
            if (!hasCompanyKit) return;
            long owed = 0;
            foreach (var k in GameData.StarterKit)
                if (equipment.Contains(k) && !depositGear.Contains(k)) { depositGear.Add(k); owed += GameData.Equip(k).price; }
            if (depositPaid) depositPaidTotal += GameData.DepositAmount;
            else depositOwed += owed;
            hasCompanyKit = false;
            depositPaid = false;
        }

        public readonly HashSet<VehicleKind> ownedVehicles = new HashSet<VehicleKind>();
        public readonly HashSet<EquipmentKind> equipment = new HashSet<EquipmentKind>();
        /// <summary>Reddettiği ekipman teklifleri → tekrar sorulabileceği zaman (oyun dakikası).</summary>
        public readonly Dictionary<EquipmentKind, float> equipRefusedUntil = new Dictionary<EquipmentKind, float>();

        public CourierState State { get; private set; }
        public Vector2 Position { get; private set; }
        /// <summary>Şu an gittiği durağın siparişi (dükkan ya da müşteri).</summary>
        public Order CurrentOrder => current;
        /// <summary>Hedefteki dışındaki tüm siparişleri (toplanacak + çantadaki).</summary>
        public IEnumerable<Order> QueuedOrders
        {
            get
            {
                foreach (var o in toPick) if (o != current) yield return o;
                foreach (var o in carrying) if (o != current) yield return o;
            }
        }
        /// <summary>Çantasında taşıdığı (teslim alınmış) paket sayısı.</summary>
        public int CarryingCount => carrying.Count;
        public VehicleDef V => GameData.Vehicle(vehicle);
        /// <summary>Kısa isim; aynı adlı başka kurye varsa soyadının baş harfi eklenir (Elif Y. / Elif K.).</summary>
        public string ShortName
        {
            get
            {
                var parts = courierName.Split(' ');
                string first = parts[0];
                var cm = CourierManager.I;
                if (cm != null && parts.Length > 1)
                    foreach (var o in cm.Couriers)
                        if (o != this && o.courierName.Split(' ')[0] == first)
                            return $"{first} {parts[1][0]}.";
                return first;
            }
        }
        public bool IsOnline => State != CourierState.Cevrimdisi && State != CourierState.Izinli;

        /// <summary>Toplu sipariş: bu yarıçaptaki (kare) dükkanlar aynı turda toplanır.</summary>
        public const int BatchRadius = 4;

        readonly List<Order> toPick = new List<Order>();    // atanmış, henüz dükkandan alınmamış
        readonly List<Order> carrying = new List<Order>();  // çantada, teslim edilecek
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
        GameObject model;       // isteğe bağlı 3D karakter (CharacterVisuals)
        float modelFacing = 180f;
        Vector2 lastModelPos;

        // ================================================================== telefon uygulaması verileri
        public int courierId;
        public float kmToday, kmWeek, kmMonth;
        public long earnedMonth;
        public int lastWeekDeliveries;
        public int offersSeenWeek, offersAcceptedWeek, offersSeenLastWeek, offersAcceptedLastWeek;
        public int depositDay = -1;
        public List<string> gearNames = new List<string>();
        public List<int> gearDays = new List<int>();
        public List<string> inboxText = new List<string>();
        public List<string> inboxWhen = new List<string>();

        // Son değerlendirdiği teklif (telefonda teklif ekranı olarak görünür)
        [System.NonSerialized] public Order lastOffer;
        [System.NonSerialized] public int lastOfferPay;
        [System.NonSerialized] public bool lastOfferAccepted;
        [System.NonSerialized] public string lastOfferReason = "";
        [System.NonSerialized] public float lastOfferRealTime = -999f;

        public void NoteOffer(Order o, int pay, bool accepted, string reason)
        {
            lastOffer = o;
            lastOfferPay = pay;
            lastOfferAccepted = accepted;
            lastOfferReason = reason ?? "";
            lastOfferRealTime = Time.unscaledTime;
            offersSeenWeek++;
            if (accepted) offersAcceptedWeek++;
        }

        /// <summary>Ekipmanı teslim aldığı gün (profilde görünür).</summary>
        public void NoteGear(EquipmentKind k)
        {
            string n = GameData.Equip(k).name;
            int i = gearNames.IndexOf(n);
            int day = GameClock.I != null ? GameClock.I.Day : 1;
            if (i >= 0) gearDays[i] = day;
            else { gearNames.Add(n); gearDays.Add(day); }
        }

        /// <summary>Kuryenin telefonundaki gelen kutusuna mesaj.</summary>
        public void AddInbox(string text)
        {
            var c = GameClock.I;
            inboxText.Insert(0, text);
            inboxWhen.Insert(0, c != null ? $"{c.DateOf(c.Day)} {c.Hour:00}:{c.Minute:00}" : "");
            if (inboxText.Count > 30) { inboxText.RemoveAt(30); inboxWhen.RemoveAt(30); }
        }

        public IReadOnlyList<Vector2Int> PathList => path;
        public int PathIndex => pathIndex;

        public void Init(CourierCandidate c, Vector2Int start)
        {
            courierId = Random.Range(1000000, 9999999);
            courierName = c.name;
            vehicle = c.vehicle;
            occupation = c.occupation;
            scheduleSeed = c.scheduleSeed;
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

            // Biraz kameraya doğru (−Z): 3D yol parçalarının üstünde kalsın
            ring = SpriteFactory.Create("Halka", transform, true, CompanyProfile.Secondary, new Vector3(0f, 0f, -0.04f), 0.75f, 10);
            body = SpriteFactory.Create("Gövde", transform, true, CompanyProfile.Primary, new Vector3(0f, 0f, -0.05f), 0.55f, 11);
            RebuildModel();

            bool online = Preference() >= 0.5f;
            State = online ? CourierState.Bosta : CourierState.Cevrimdisi;
            offlineReason = online ? "" : OccupationStatus;
            lastDecisionHour = GameClock.I.Hour;
        }

        /// <summary>
        /// Kayıttan yüklendikten sonra (alanlar JsonUtility ile doldurulmuşken) çağrılır:
        /// izin / tamir durumunu ve çevrimiçi kararını yeniden kurar, 3D modeli kuryenin adıyla yeniler.
        /// </summary>
        public void AfterLoad()
        {
            gameObject.name = "Kurye " + courierName;
            if (unavailableDays > 0)
                SetOnLeave(unavailableDays, string.IsNullOrEmpty(leaveReason) ? "izinli" : leaveReason);
            else
            {
                bool online = Preference() >= 0.5f;
                State = online ? CourierState.Bosta : CourierState.Cevrimdisi;
                offlineReason = online ? "" : OccupationStatus;
                if (repairDays > 0) RefreshVehicle();
            }
            lastDecisionHour = GameClock.I.Hour;
            RebuildModel();
        }

        /// <summary>3D karakter modelini (yeniden) oluşturur; model yoksa daire gösterilir.</summary>
        public void RebuildModel()
        {
            if (model != null) Destroy(model);
            model = CharacterVisuals.Spawn(transform, courierName);
            if (body != null) body.enabled = true;
            lastModelPos = Position;
        }

        // ================================================================== meslek / günlük hayat

        public static string OccupationLabel(Occupation o)
        {
            switch (o)
            {
                case Occupation.OfisCalisani: return "Ofis çalışanı (hafta içi 17:00'den sonra)";
                case Occupation.HaftaSonu: return "Sadece hafta sonu çalışır";
                case Occupation.Ogrenci: return "Üniversite öğrencisi (kendi ders programına ve sınavlara göre)";
                case Occupation.Vardiyali: return "Vardiyalı fabrika işçisi (vardiyası haftalık değişir)";
                case Occupation.Emekli: return "Emekli (08:00–15:00)";
                case Occupation.Ebeveyn: return "Ebeveyn (çocuklar okuldayken, hafta içi 09:00–15:00)";
                case Occupation.GeceKuryesi: return "Gece kuryesi (20:00–03:00)";
                default: return "Tam zamanlı kurye (her gün 10–15 ve 17–22, arada mola)";
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

        // ---------- Öğrenci ders programı ----------
        // Her öğrencinin kendine ait, her dönem değişen programı var: kimi sabah, kimi öğlen, kimi akşam derste.
        static readonly int[,] ClassBlocks = { { 8, 12 }, { 10, 14 }, { 12, 16 }, { 14, 18 }, { 17, 21 } };
        static readonly string[] DayShort = { "Pzt", "Sal", "Çar", "Per", "Cum" };

        /// <summary>Güz dönemi Eylül–Ocak, bahar dönemi Şubat–Haziran.</summary>
        public static int SemesterIndex(GameClock c)
        {
            if (c.Month >= 9) return c.Year * 2 + 1;
            if (c.Month == 1) return (c.Year - 1) * 2 + 1;
            return c.Year * 2;
        }

        /// <summary>Bu dönem bu hafta içi gününde (0=Pzt) ders saatleri; yoksa -1.</summary>
        public static void ClassesFor(int seed, int semester, int dow, out int s1, out int e1, out int s2, out int e2)
        {
            s1 = e1 = s2 = e2 = -1;
            if (dow < 0 || dow > 4) return;
            var r = new System.Random(seed * 31 + semester * 977 + dow * 131);
            if (r.NextDouble() < 0.8) { int b = r.Next(5); s1 = ClassBlocks[b, 0]; e1 = ClassBlocks[b, 1]; }
            if (r.NextDouble() < 0.25)
            {
                int b = r.Next(5);
                if (s1 < 0 || ClassBlocks[b, 0] >= e1 || ClassBlocks[b, 1] <= s1) { s2 = ClassBlocks[b, 0]; e2 = ClassBlocks[b, 1]; }
            }
        }

        /// <summary>Haftalık ders programı metni (kuryeler ve başvurular ekranında).</summary>
        public static string ScheduleText(int seed, GameClock c)
        {
            int sem = SemesterIndex(c);
            var parts = new List<string>();
            for (int d = 0; d < 5; d++)
            {
                ClassesFor(seed, sem, d, out int s1, out int e1, out int s2, out int e2);
                string t = s1 < 0 && s2 < 0 ? "ders yok" : "";
                if (s1 >= 0) t += $"{s1:00}–{e1:00}";
                if (s2 >= 0) t += (t.Length > 0 ? ", " : "") + $"{s2:00}–{e2:00}";
                parts.Add($"{DayShort[d]} {t}");
            }
            return string.Join(" · ", parts);
        }

        bool InClass(GameClock c, int hourOffset)
        {
            int h = c.Hour + hourOffset;
            ClassesFor(scheduleSeed, SemesterIndex(c), c.DayOfWeek, out int s1, out int e1, out int s2, out int e2);
            return (s1 >= 0 && h >= s1 && h < e1) || (s2 >= 0 && h >= s2 && h < e2);
        }

        string TodayClassesText(GameClock c)
        {
            ClassesFor(scheduleSeed, SemesterIndex(c), c.DayOfWeek, out int s1, out int e1, out int s2, out int e2);
            if (s1 < 0 && s2 < 0) return "Bugün dersi yok, istediği saatte çalışır";
            string t = "";
            if (s1 >= 0) t += $"{s1:00}:00–{e1:00}:00";
            if (s2 >= 0) t += (t.Length > 0 ? " ve " : "") + $"{s2:00}:00–{e2:00}:00";
            return $"Bugün dersi {t}, kalan saatlerde çalışabilir";
        }

        static bool IsSchoolBreakForKids(GameClock c) =>
            c.Month == 7 || c.Month == 8 || (c.Month == 12 && c.DayOfMonth >= 22) || (c.Month == 1 && c.DayOfMonth <= 2);

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
                        return c.IsWeekend ? "Hafta sonu, okul yok" : TodayClassesText(c);
                    case Occupation.OfisCalisani:
                        return c.IsWeekend ? "Hafta sonu, ofis kapalı" : (c.Hour < 17 ? "Ofiste, mesai 17:00'de bitiyor" : "Mesai bitti");
                    case Occupation.HaftaSonu:
                        return c.IsWeekend ? "Hafta sonu, çalışma günü" : "Hafta içi, kendi işinde";
                    case Occupation.Vardiyali:
                        return FactoryMorningWeek(c) ? "Bu hafta fabrikada sabah vardiyası (15:00 sonrası kuryelik)"
                                                     : "Bu hafta fabrikada akşam vardiyası (sabahları kuryelik)";
                    case Occupation.Emekli:
                        return "Emekli, sabahtan öğleden sonraya kadar çalışmayı sever";
                    case Occupation.Ebeveyn:
                        if (c.IsWeekend) return "Hafta sonu, çocuklarla evde";
                        return IsSchoolBreakForKids(c) ? "Okullar tatil, çocuklar evde (az çalışır)"
                                                       : "Çocuklar okulda: 09:00–15:00 arası çalışabilir";
                    case Occupation.GeceKuryesi:
                        return "Gece çalışır (20:00–03:00), gündüz uyur";
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
                    // Kendi ders programı: derste ve derse gitmeden önceki saatte çalışmaz
                    if (InClass(c, 0)) return 0.03f;
                    if (InClass(c, 1)) return 0.1f;
                    if (h >= 10 && h < 23) return 0.8f;
                    return (h >= 8 && h < 10) ? 0.4f : 0.05f;

                case Occupation.Vardiyali:
                    if (we) return (h >= 10 && h < 20) ? 0.6f : 0.05f;
                    if (FactoryMorningWeek(c)) return (h >= 15 && h < 23) ? 0.85f : 0.03f;
                    return (h >= 7 && h < 13) ? 0.85f : 0.03f;

                case Occupation.Emekli:
                    if (we) return (h >= 9 && h < 13) ? 0.3f : 0.03f;
                    return (h >= 8 && h < 15) ? 0.85f : ((h >= 15 && h < 17) ? 0.35f : 0.03f);

                case Occupation.Ebeveyn:
                    if (we) return (h >= 10 && h < 13) ? 0.2f : 0.03f;
                    if (IsSchoolBreakForKids(c)) return (h >= 10 && h < 14) ? 0.3f : 0.03f;
                    return (h >= 9 && h < 15) ? 0.85f : ((h >= 15 && h < 17) ? 0.1f : 0.03f);

                case Occupation.GeceKuryesi:
                    if (h >= 20 || h < 3) return 0.85f;
                    if (h >= 18) return 0.35f;
                    return h < 5 ? 0.2f : 0.03f;

                default:
                    // Tam zamanlı: öğle ve akşam yoğunluğunda çalışır, aradaki ölü saatte mola verir
                    if ((h >= 10 && h < 15) || (h >= 17 && h < 22)) return 0.9f;
                    if (h >= 15 && h < 17) return 0.4f;
                    return ((h >= 8 && h < 10) || (h >= 22 && h < 23)) ? 0.35f : 0.05f;
            }
        }

        /// <summary>Her oyun saatinde kurye çalışıp çalışmayacağına kendisi karar verir.</summary>
        void DecideAvailability()
        {
            HourlyHygiene();
            if (State == CourierState.Izinli || State == CourierState.Mola) return;
            bool boost = CampaignManager.I != null && CampaignManager.I.BoostActive;

            if (dayOff)
            {
                if (IsOnline) RequestOffline("Bugün izin günü");
                return;
            }

            float pref = Preference();
            float crisis = CrisisManager.OnlineFactor(this);   // grev, akaryakıt zammı, sıcak primi

            if (IsOnline)
            {
                if (crisis < 1f && OrderCount == 0 && Random.value < (1f - crisis) * 0.6f)
                {
                    RequestOffline(CrisisManager.OfflineReason(this));
                    return;
                }
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
                float pOn = pref * (0.4f + morale * 0.6f) * WeatherWillingness() + (boost && pref >= 0.2f ? 0.35f : 0f);
                if (UpgradeManager.I != null) pOn *= UpgradeManager.I.PeakAvailabilityMultiplier(GameClock.I.Hour);
                pOn *= crisis;
                if (Random.value < pOn)
                {
                    GoOnline();
                    if (boost && pref < 0.5f) EventManager.I.Notify($"{ShortName} boost için çevrimiçi oldu.");
                }
            }
        }

        /// <summary>Açıkta giden kurye (bisiklet, scooter, motor) kötü havada dışarı çıkmaya daha az isteklidir; ekipman ve araba yardımcı olur.</summary>
        public float WeatherWillingness()
        {
            var w = WeatherSystem.I;
            if (w == null || !V.exposed) return 1f;
            switch (w.Current)
            {
                case WeatherKind.Karli: return Has(EquipmentKind.KisMontu) ? 0.8f : 0.5f;
                case WeatherKind.Firtina: return Has(EquipmentKind.Yagmurluk) ? 0.7f : 0.5f;
                case WeatherKind.Yagmurlu: return Has(EquipmentKind.Yagmurluk) ? 0.95f : 0.7f;
                case WeatherKind.Soguk: return Has(EquipmentKind.KisMontu) ? 1f : 0.85f;
                default: return 1f;
            }
        }

        /// <summary>Kurye mesajlarından: elinde paket yoksa kısa mola verir (yorgunluk azalır).</summary>
        public bool TakeBreak(float minutes, string reason)
        {
            if (!IsOnline || OrderCount > 0 || State == CourierState.Mola) return false;
            breakUntil = GameClock.I.TotalMinutes + minutes;
            breakReason = reason;
            State = CourierState.Mola;
            return true;
        }

        // ---------- Kurye uygulamasından yapılanlar ----------

        /// <summary>Son teslim ettiği sipariş (uygulamada "Önceki görev").</summary>
        [System.NonSerialized] public Order lastDelivered;
        /// <summary>Uygulamadan ayarlanan çevrimdışı olma saati (GameClock.TotalMinutes; yoksa −1).</summary>
        public float plannedOfflineAt = -1f;

        /// <summary>"Bu teslimatı iptal et": henüz dükkandan alınmamış siparişi bırakır, sipariş başka kuryeye gider.</summary>
        public bool CancelTask(Order o)
        {
            if (o == null || !toPick.Contains(o)) return false;
            bool wasCurrent = current == o;
            toPick.Remove(o);
            if (wasCurrent) current = null;
            OrderManager.I.ReturnToPending(o, this);
            EventManager.I.Notify($"{ShortName} #{o.id} teslimatını iptal etti, sipariş başka kuryeye gidecek.");
            if (wasCurrent) PlanNext();
            return true;
        }

        /// <summary>
        /// Sipariş dükkan ya da müşteri tarafından iptal edildi: henüz alınmamış paketi bırakır (başka kuryeye gitmez).
        /// Dönen: kurye dükkana varmış mıydı (boşa gidiş ücreti için).
        /// </summary>
        public bool AbortTask(Order o)
        {
            if (o == null || !toPick.Contains(o)) return false;
            bool wasCurrent = current == o;
            bool atShop = wasCurrent && State == CourierState.Bekliyor;
            toPick.Remove(o);
            if (wasCurrent) { current = null; PlanNext(); }
            return atShop;
        }

        /// <summary>Adres sorununda "Destek": 10 dakika beklemeden destek / müşteri hemen aranır.</summary>
        public bool ContactSupportNow()
        {
            if (State != CourierState.AdresSorunu) return false;
            issueWaitUntil = GameClock.I.TotalMinutes;
            return true;
        }

        /// <summary>Kurye mesajlarından: bugünlük işi bırakır (elindeki paketleri bitirince).</summary>
        public void EndShiftEarly(string reason) => RequestOffline(reason);

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

        /// <summary>Yeni oyunun ilk sabahı: başlangıç kuryeleri işbaşında olsun (oyuncu daha başlamadan iptal cezası yemesin).</summary>
        public void StartShift()
        {
            if (State == CourierState.Cevrimdisi && !dayOff) GoOnline();
        }

        // ================================================================== para / araç / sigorta

        /// <summary>Kurye kazanır; bir kısmını biriktirir.</summary>
        // ================================================================== Wolt tipi hesap ve nakit

        [Header("Hesap ve nakit")]
        public long balance;            // hesabında biriken, henüz kesilmemiş kazanç
        public long pendingPayout;      // kesilmiş / erken istenmiş, hesabına yatmayı bekleyen
        public long receivedTotal;      // bugüne kadar hesabına yatan
        public long cashHeld;           // müşterilerden aldığı, şirkete borçlu olduğu nakit
        public bool cashBlocked;        // nakit limiti aşıldı / ay sonu kapatılmadı: hesap kilitli
        public int earlyPayouts;
        public long earlyPayoutTotal;   // erken ödeme olarak istediği toplam tutar
        public int cashOrders;          // nakit ödemeli teslimat sayısı
        public long cashTakenTotal;     // müşterilerden aldığı toplam nakit
        public long keepCashTotal;      // keep cash ile hesabından mahsup edilen toplam
        public long offloadTotal;       // offload ile kartından şirkete ödediği toplam

        public const int CashLimit = 100000;
        public const float KeepCashShare = 0.8f;     // keep cash: hesabın en fazla %80'iyle mahsup
        public const float EarlyPayoutShare = 0.8f;  // erken ödeme: hesabın %80'i

        /// <summary>Kazanç (ücret, boost, uygulama bahşişi, ödül) hesaba yazılır; kesim gününde ödenir.</summary>
        public void Earn(long amount)
        {
            earnedMonth += amount;
            totalEarned += amount;
            earnedToday += amount;
            earnedWeek += amount;
            balance += amount;

            // Kesilmemiş ekipman depozitosu varsa kazancından kesilir
            if (depositOwed > 0 && balance > 0) EconomyManager.I.CollectGearDeposit(this);
        }

        /// <summary>Nakit bahşiş: doğrudan kuryenin cebine gider.</summary>
        public void EarnCashTip(long amount)
        {
            earnedMonth += amount;
            totalEarned += amount;
            earnedToday += amount;
            earnedWeek += amount;
            savings += (long)(amount * SavingRate);
        }

        /// <summary>Hesabına para yattı.</summary>
        public void OnPaid(long amount)
        {
            receivedTotal += amount;
            pendingPayout = System.Math.Max(0L, pendingPayout - amount);
            savings += (long)(amount * SavingRate);
            AddInbox($"Hesabına {GameData.Ft(amount)} yattı.");
        }

        public bool CanTakeCash(long amount) => !cashBlocked && cashHeld + amount <= CashLimit;
        public long KeepCashCapacity => (long)(balance * KeepCashShare) / 10 * 10;

        /// <summary>
        /// Gün sonunda nakdini kapatır: önce keep cash (hesabın %80'ine kadar), kalanı gerekirse offload.
        /// Ay sonunda tüm nakit kapatılmalı; limit dolarsa ya da ay sonu kapatmazsa hesabı kilitlenir.
        /// </summary>
        public void SettleCash(bool monthEnd)
        {
            if (cashHeld <= 0) { cashHeld = 0; cashBlocked = false; return; }

            long keep = System.Math.Min(cashHeld, KeepCashCapacity);
            if (keep > 0) EconomyManager.I.KeepCash(this, keep);
            if (cashHeld <= 0) { cashBlocked = false; return; }

            bool mustPay = monthEnd || cashBlocked || cashHeld >= CashLimit * 0.7f;
            bool forgets = Random.value < (1f - tidiness) * 0.25f;   // dağınık kurye unutabilir
            if ((mustPay && !forgets) || Random.value < 0.2f)
            {
                EconomyManager.I.Offload(this, cashHeld);
                cashBlocked = false;
            }
            else if (monthEnd || cashHeld >= CashLimit)
            {
                if (!cashBlocked)
                    EventManager.I.Notify($"{courierName} nakit borcunu ({GameData.Ft(cashHeld)}) kapatmadı, hesabı kilitlendi. Ödeyene kadar sipariş alamaz.");
                cashBlocked = true;
                AddInbox("Nakit borcunu kapatmadığın için hesabın kilitlendi. Kapatana kadar sipariş alamazsın.");
                morale = Mathf.Max(0f, morale - 0.05f);
            }
        }

        /// <summary>Parası azsa ya da canı isterse erken ödeme ister (%80).</summary>
        public void MaybeEarlyPayout()
        {
            if (balance < 15000) return;
            float chance = savings < 20000 ? 0.25f : 0.06f;
            if (Random.value < chance) EconomyManager.I.RequestEarlyPayout(this);
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
        /// <summary>Sigortayı şirketin anlaşmalı sigortacısı üzerinden mi yaptırdı (şirket komisyon alır).</summary>
        public bool insuredViaCompany;

        public long CurrentPremium => insuredViaCompany && EconomyManager.I != null ? EconomyManager.I.Plan.premium : InsurancePremium;

        public bool PayInsurance()
        {
            if (!insured) return false;
            long premium = CurrentPremium;
            if (savings < premium)
            {
                insured = false; // ödeyemedi, sigortası düştü
                insuredViaCompany = false;
                AddInbox("Sigorta primini ödeyemedin, sigortan düştü.");
                return false;
            }
            savings -= premium;
            insurancePaid += premium;
            if (insuredViaCompany) EconomyManager.I.CollectInsuranceCommission(this, premium);
            return true;
        }

        /// <summary>Sigorta yaptırır: şirketin anlaşması varsa onun üzerinden, yoksa dışarıdan.</summary>
        public bool TryBuyInsurance()
        {
            var eco = EconomyManager.I;
            bool via = eco != null && eco.HasInsurancePartner;
            long premium = via ? eco.Plan.premium : InsurancePremium;
            if (insured || savings < premium) return false;
            insured = true;
            insuredViaCompany = via;
            savings -= premium;
            insurancePaid += premium;
            if (via) eco.CollectInsuranceCommission(this, premium);
            AddInbox(via ? $"Sigortan aktif: {eco.Plan.name}, haftalık {GameData.Ft(premium)}." : $"Sigortan aktif, haftalık {GameData.Ft(premium)}.");
            return true;
        }

        // ================================================================== kapasite / reddetme

        public bool Has(EquipmentKind k) => equipment.Contains(k);
        public int OrderCount => toPick.Count + carrying.Count;
        public bool Available => IsOnline && State != CourierState.Mola && !wantsOffline && !cashBlocked;

        public int EffectiveCapacity
        {
            get
            {
                // Araç kapasitesi üst sınır (gerçekte kurye aynı anda en fazla 2–4 paket taşır); moral/boost sınırı aşmaz
                int cap = V.capacity;
                if (morale < 0.35f) cap -= 1;
                if (fatigue >= 0.6f) cap -= 1;
                return Mathf.Max(1, cap);
            }
        }

        /// <summary>
        /// Otomatik atamada çantasına yeni paket eklenebilir mi: boşsa evet; doluysa sadece elindeki siparişler
        /// yeniyse (12 dk'dan eski sipariş yoksa). Böylece eski paketler yeni eklenenler yüzünden saatlerce beklemez.
        /// </summary>
        public bool BagYoung
        {
            get
            {
                if (OrderCount == 0) return true;
                float now = GameClock.I.TotalMinutes;
                foreach (var o in carrying) if (now - o.createdAt > 12f) return false;
                foreach (var o in toPick) if (now - o.createdAt > 12f) return false;
                return true;
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
                if (toPick.Count > 0) return toPick[toPick.Count - 1].customerDoor;
                return carrying[carrying.Count - 1].customerDoor;
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
            WeatherSystem.I.SpeedFactor(this) * (1f - 0.3f * fatigue) * TrafficFactor * UpgradeManager.I.SpeedMultiplier;

        // ================================================================== teslimat akışı

        public void Assign(Order o)
        {
            lastBusyAt = GameClock.I.TotalMinutes;
            if (OrderCount > 0 && !o.batched)
            {
                // Toplu sipariş: müşteriye gösterilen tahmini süre biraz uzar
                o.batched = true;
                o.deadlineAt += 5f;
            }
            toPick.Add(o);

            // Boştaysa hemen yola çıkar; yoldaysa rotasını yeniden planlar (yakın dükkana uğrayabilir)
            if (State == CourierState.Bosta || State == CourierState.DukkanaGidiyor || State == CourierState.MusteriyeGidiyor)
                PlanNext();
        }

        /// <summary>
        /// Sıradaki durağı seçer:
        /// 1) Yakındaki (BatchRadius) dükkanlardan toplanacak sipariş varsa önce onu alır (toplu alım).
        ///    Çantada paket varken uzun beklenecek dükkana uğramaz (yemek soğumasın).
        /// 2) Çantadaki paketleri en kısa rotayla dağıtır.
        /// 3) Kalan siparişler için en yakın dükkana gider.
        /// </summary>
        void PlanNext()
        {
            var here = Vector2Int.RoundToInt(Position);
            float now = GameClock.I.TotalMinutes;

            // Yol üstünde başka dükkana uğramak sadece çantadaki paketler tazeyse: en fazla 1 paket, 8 dk'dan kısa süredir çantada
            bool bagFresh = carrying.Count == 0;
            if (carrying.Count == 1 && carrying[0].pickedAt > 0f && now - carrying[0].pickedAt < 8f) bagFresh = true;

            Order pick = null;
            int best = int.MaxValue;
            foreach (var o in toPick)
            {
                if (!bagFresh) break;
                int d = CityMap.Manhattan(here, o.shop.door);
                if (d > BatchRadius) continue;
                if (carrying.Count > 0 && o.readyAt - now > 6f) continue;
                if (d < best) { best = d; pick = o; }
            }
            if (pick != null) { GoToShop(pick); return; }

            if (carrying.Count > 0) { GoToCustomer(BestDropoff(here)); return; }

            if (toPick.Count > 0)
            {
                pick = toPick[0];
                best = int.MaxValue;
                foreach (var o in toPick)
                {
                    int d = CityMap.Manhattan(here, o.shop.door);
                    if (d < best) { best = d; pick = o; }
                }
                GoToShop(pick);
                return;
            }

            // Elinde iş kalmadı
            current = null;
            path = null;
            if (vehicleSwitchPending) RefreshVehicle();
            if (State == CourierState.Izinli) return;
            if (suspendAfterOrders) { suspendAfterOrders = false; wantsOffline = false; SetOnLeave(1, "hesabı donduruldu"); }
            else if (wantsOffline) GoOffline(wantsOfflineReason);
            else if (fatigue >= 0.95f) GoOffline("Çok yoruldu, eve gitti");
            else if (State != CourierState.Mola) State = CourierState.Bosta;
        }

        void GoToShop(Order o)
        {
            current = o;
            State = CourierState.DukkanaGidiyor;
            GoTo(o.shop.door);
        }

        void GoToCustomer(Order o)
        {
            current = o;
            State = CourierState.MusteriyeGidiyor;
            GoTo(o.customerDoor);
        }

        /// <summary>Çantadaki paketler için en kısa teslim sırası (tüm sıralamalar denenir); ilk durağı döner.</summary>
        Order BestDropoff(Vector2Int from)
        {
            if (carrying.Count == 1) return carrying[0];
            if (carrying.Count > 6)
            {
                // Çok paket: en yakın müşteri
                Order nearest = carrying[0];
                int bd = int.MaxValue;
                foreach (var o in carrying)
                {
                    int d = CityMap.Manhattan(from, o.customerDoor);
                    if (d < bd) { bd = d; nearest = o; }
                }
                return nearest;
            }

            var items = carrying.ToArray();
            var used = new bool[items.Length];
            int bestCost = int.MaxValue;
            Order bestFirst = items[0];

            void Search(Vector2Int pos, int depth, int cost, Order first)
            {
                if (cost >= bestCost) return;
                if (depth == items.Length)
                {
                    bestCost = cost;
                    bestFirst = first;
                    return;
                }
                for (int i = 0; i < items.Length; i++)
                {
                    if (used[i]) continue;
                    used[i] = true;
                    var door = items[i].customerDoor;
                    Search(door, depth + 1, cost + CityMap.Manhattan(pos, door), first ?? items[i]);
                    used[i] = false;
                }
            }

            Search(from, 0, 0, null);
            return bestFirst;
        }

        /// <summary>Bu sipariş kuryenin mevcut turuna eklenebilir mi (yakın dükkan)?</summary>
        public bool CanBatch(Order o)
        {
            if (OrderCount == 0) return false;
            foreach (var p in toPick)
                if (CityMap.Manhattan(p.shop.door, o.shop.door) <= BatchRadius) return true;
            return carrying.Count > 0 && CityMap.Manhattan(Vector2Int.RoundToInt(Position), o.shop.door) <= BatchRadius;
        }

        /// <summary>Sipariş, kuryenin taşıdığı/alacağı paketlerden birinin müşterisine yakın mı?</summary>
        public bool CustomerNearRoute(Order o)
        {
            foreach (var p in toPick) if (CityMap.Manhattan(p.customerDoor, o.customerDoor) <= 6) return true;
            foreach (var p in carrying) if (CityMap.Manhattan(p.customerDoor, o.customerDoor) <= 6) return true;
            return false;
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
                // Uygulamadan ayarlanan çevrimdışı saati
                if (plannedOfflineAt > 0f && clock.TotalMinutes >= plannedOfflineAt)
                {
                    plannedOfflineAt = -1f;
                    if (IsOnline) RequestOffline("Planladığı çevrimdışı saati geldi");
                }
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
                        if (clock.TotalMinutes >= current.readyAt) PickUpHere();
                        break;
                    case CourierState.KataCikiyor:
                        Work(dm);
                        if (current != null && current.UsesStairs) fatigue = Mathf.Min(1f, fatigue + dm * 0.0015f); // merdiven yorar
                        if (current == null || clock.TotalMinutes >= doorUntil) { if (current != null) DeliverCurrent(false); else PlanNext(); }
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
                            if (OrderCount > 0) PlanNext();
                        }
                        break;
                    case CourierState.Bosta:
                        fatigue = Mathf.Max(0f, fatigue - dm * 0.0015f * UpgradeManager.I.RestMultiplier);
                        break;
                    case CourierState.Cevrimdisi:
                    case CourierState.Izinli:
                        fatigue = Mathf.Max(0f, fatigue - dm * 0.006f * UpgradeManager.I.RestMultiplier);
                        break;
                }
            }

            transform.position = CityMap.ToWorld(Position);
            ring.color = IsOnline ? CompanyProfile.Secondary : new Color(0.5f, 0.5f, 0.5f, 0.35f);
            var p = CompanyProfile.Primary;
            body.color = IsOnline ? (State == CourierState.Mola ? Color.Lerp(p, Color.gray, 0.5f) : p)
                                  : new Color(p.r * 0.5f, p.g * 0.5f, p.b * 0.5f, 0.35f);

            // 3D karakter: çevrimiçiyken görünür ve yürüdüğü yöne döner; çevrimdışıyken soluk daire kalır
            if (model != null)
            {
                // Uzaktayken daire (ikon), yakınlaşınca 3D karakter
                // Binanın içindeyken (kata çıkıyor) karakter görünmez
                bool show = IsOnline && MapCamera.I != null && MapCamera.I.ShowModels && State != CourierState.KataCikiyor;
                bool walking = (State == CourierState.DukkanaGidiyor || State == CourierState.MusteriyeGidiyor)
                               && GameClock.I != null && GameClock.I.DeltaMinutes > 0f;
                CharacterVisuals.UpdateModel(model, Position - lastModelPos, walking, show, ref modelFacing);
                body.enabled = !show;
                ring.transform.localScale = Vector3.one * (show ? 0.3f : 0.75f); // yakında karakterin ayağında küçük halka
            }
            lastModelPos = Position;
        }

        void Work(float dm)
        {
            float rate = 0.0011f / Mathf.Max(0.3f, stamina);
            if (V.exposed && WeatherSystem.I.IsHot && !Has(EquipmentKind.YazTisort)) rate *= 1.5f;
            if (Has(EquipmentKind.Powerbank)) rate *= 0.8f;
            rate *= CrisisManager.FatigueFactor;   // sıcak dalgası vb.
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
                float moved = Mathf.Min(d, remaining);
                float km = moved * GameData.KmPerTile;
                kmToday += km; kmWeek += km; kmMonth += km;
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
                if (!current.shopQueue && Random.value < 0.25f * UpgradeManager.I.ShopQueueMultiplier)
                {
                    current.readyAt = Mathf.Max(current.readyAt, now) + Random.Range(5f, 20f);
                    current.shopQueue = true;
                }

                if (now >= current.readyAt) { PickUpHere(); return; }

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
                // Temassız teslimatta müşteri evde olmasa da paket kapıya bırakılır
                if (current.noContact && current.addressIssue == AddressIssue.EvdeYok) current.issueResolved = true;
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
                StartDoorstep();
            }
        }

        float issueWaitUntil;
        float doorUntil;

        /// <summary>Binaya girer, katına çıkar (asansör/merdiven), teslim eder, iner. Süre kat ve asansöre bağlı.</summary>
        void StartDoorstep()
        {
            var o = current;
            float mins = o.DoorMinutes(stamina);
            if (mins <= 1.2f) { DeliverCurrent(false); return; }
            doorUntil = GameClock.I.TotalMinutes + mins;
            State = CourierState.KataCikiyor;
            if (o.elevatorBroken)
                EventManager.I.Notify($"{ShortName} #{o.id}: asansör arızalı, {o.floor}. kata merdivenle çıkıyor.");
        }

        // ---------- Arayüz için: kuryenin üstündeki siparişler ----------
        public IReadOnlyList<Order> ToPickList => toPick;
        public IReadOnlyList<Order> CarryingList => carrying;
        public float DoorMinutesLeft => State == CourierState.KataCikiyor ? Mathf.Max(0f, doorUntil - GameClock.I.TotalMinutes) : 0f;

        void CancelPickup(float wait)
        {
            var o = current;
            current = null;
            toPick.Remove(o);
            pickupCancelsThisMonth++;
            OrderManager.I.ReturnToPending(o, this);
            EventManager.I.Notify($"{ShortName} {o.shop.Name}'da {wait:0} dk sıra olduğu için #{o.id} teslim almayı iptal etti " +
                                  $"(bu ay {pickupCancelsThisMonth}/{MaxPickupCancels}). Sipariş yeni kurye bekliyor.");
            PlanNext();
        }

        // ================================================================== teklif değerlendirme

        /// <summary>Teklifin tahmini süresi (dk): dükkana gidiş + hazırlık beklemesi + müşteriye gidiş.</summary>
        public float EstimateMinutes(Order o)
        {
            float speed = Mathf.Max(0.05f, V.tilesPerMinute * speedSkill);
            bool batch = CanBatch(o);
            // Toplu sipariş: yakındaki dükkandan aynı turda alır, diğer paketlerin teslimi araya girer
            Vector2Int from = batch ? Vector2Int.RoundToInt(Position) : FreeTile;
            float toShop = CityMap.Manhattan(from, o.shop.door) / speed + OrderCount * (batch ? 4f : 6f);
            float wait = Mathf.Max(0f, o.readyAt - (GameClock.I.TotalMinutes + toShop));
            return toShop + wait + o.distance / speed + o.DoorMinutes(stamina, true);
        }

        /// <summary>Teklifin tahmini saatlik kazancı.</summary>
        public float OfferHourly(Order o, int pay) => pay / Mathf.Max(5f, EstimateMinutes(o)) * 60f;

        /// <summary>Bu aracın "normal" bir işten saatlik kazancı (10 kare gidiş, 5 dk bekleme, 12 kare teslimat, bonus yok).</summary>
        public float ReferenceHourly
        {
            get
            {
                float speed = Mathf.Max(0.05f, V.tilesPerMinute * speedSkill);
                float pay = V.basePay + V.payPerKm * GameData.Km(12);
                return pay / (22f / speed + 5f) * 60f;
            }
        }

        /// <summary>Boşta ne kadar süredir iş bekliyor (dk). Uzun bekleyen kurye daha az seçici olur.</summary>
        public float IdleMinutes => State == CourierState.Bosta && OrderCount == 0 ? GameClock.I.TotalMinutes - lastBusyAt : 0f;
        float lastBusyAt;

        /// <summary>Teklifin kuryenin beklentisine oranı (1 = normal bir iş kadar iyi).</summary>
        public float OfferRatio(Order o, int pay) => OfferHourly(o, pay) / Mathf.Max(1f, ReferenceHourly);

        /// <summary>Kuryenin bu teklif için beklediği en düşük oran.</summary>
        public float OfferThreshold(Order o)
        {
            float t = Mathf.Lerp(0.35f, 0.8f, pickiness);
            if (IdleMinutes > 20f) t *= 0.8f;      // uzun süredir iş yok: daha az seçici
            if (o.secondRound) t *= 0.75f;         // sipariş uzun beklemiş: pazarlık payı
            t *= UpgradeManager.I.OfferThresholdMultiplier; // kurye uygulaması 2.0
            t *= Difficulty.OfferThreshold;                  // zorluk seviyesi
            return t;
        }

        /// <summary>Arayüz için: teklif kuryenin beklentisinin altında mı (kesin değil, reddedebilir).</summary>
        public bool LikesOffer(Order o, int pay) => OfferRatio(o, pay) >= OfferThreshold(o);

        /// <summary>
        /// Kurye tahmini kazancı görür ve karar verir. Beklentisinin üstündeyse kabul eder;
        /// altındaysa yine de bir ihtimalle kabul eder (az seçici olanlar daha sık).
        /// </summary>
        public bool WillAcceptOffer(Order o, int pay)
        {
            float ratio = OfferRatio(o, pay);
            float thr = OfferThreshold(o);
            if (ratio >= thr) return true;
            float chance = (ratio / thr) * (1f - pickiness * 0.7f);
            return Random.value < chance;
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
            lastBusyAt = GameClock.I.TotalMinutes;
            {
                var done = current;
                lastDelivered = done;
                current = null;
                carrying.Remove(done);
                deliveries++;
                deliveriesToday++;
                AddXp();
                DirtyBag(done);
                if (kept) OrderManager.I.CompleteKept(done, this);
                else OrderManager.I.CompleteDelivery(done, this);

                if (!Has(EquipmentKind.Powerbank) && OrderCount == 0 && !wantsOffline && Random.value < 0.05f * UpgradeManager.I.PhoneDieMultiplier)
                {
                    breakUntil = GameClock.I.TotalMinutes + 60f;
                    breakReason = "Telefonu kapandı, şarj ediyor";
                    State = CourierState.Mola;
                    EventManager.I.Notify($"{courierName}'ın telefonu kapandı, 1 saat çevrimdışı. (Powerbank alınmalı)");
                    return;
                }
                PlanNext();
            }
        }

        /// <summary>Dükkanda: bu dükkandaki hazır tüm siparişleri birlikte alır, sonra rotasını planlar.</summary>
        void PickUpHere()
        {
            var shop = current.shop;
            float now = GameClock.I.TotalMinutes;
            int picked = 0;
            for (int i = toPick.Count - 1; i >= 0; i--)
            {
                var o = toPick[i];
                if (o.shop != shop || (o != current && now < o.readyAt)) continue;
                toPick.RemoveAt(i);
                o.status = OrderStatus.Alindi;
                o.pickedAt = now;
                carrying.Add(o);
                picked++;
            }
            if (picked > 1)
                EventManager.I.Notify($"{ShortName} {shop.Name}'dan {picked} siparişi birlikte aldı (toplu teslimat).");
            else if (carrying.Count > 1)
                EventManager.I.Notify($"{ShortName} yol üstünde {shop.Name}'a uğradı, çantasında {carrying.Count} paket var.");
            current = null;
            PlanNext();
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

        bool suspendAfterOrders;

        /// <summary>Hesabı dondur: elindeki paketler bitince o gün çalışamaz (gece yarısı açılır).</summary>
        public void Suspend(int days)
        {
            if (OrderCount > 0)
            {
                suspendAfterOrders = true;
                wantsOffline = true;
                wantsOfflineReason = "Hesabı donduruldu";
                return;
            }
            SetOnLeave(days, "hesabı donduruldu");
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
            earnedToday = 0;
            kmToday = 0f;
            if (clock.DayOfWeek == 0) { earnedWeek = 0; hygieneComplaintsWeek = 0; } // yeni hafta (rapor bu sıfırlamadan önce hazırlanır)

            // Çanta temizliği kuryenin kendi sorumluluğu: her gece zorunlu olarak temizler (şirkete maliyeti yok)
            if (hygiene < MaxHygiene - 0.01f || cleanRequested) CleanBag();
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

        public const float HygieneLimit = 0.45f;   // bunun altında koku fark edilir: düşük puan ve nadiren şikayet
        public int hygieneComplaintsWeek;          // bu haftaki hijyen şikayeti (3'te şirkete bildirilir)
        public const int BagWornUses = 400;

        /// <summary>Çok kullanılan çanta yıpranır: temizlense de en fazla %75'e çıkar.</summary>
        public bool BagWorn => bagUses >= BagWornUses;
        public float MaxHygiene => BagWorn ? 0.75f : 1f;

        void DirtyBag(Order o)
        {
            bagUses++;
            float d = 0.008f;
            if (o.shop.def.hotFood) d *= 1.3f;              // yağ, sos
            if (o.IsHeavy || o.weightKg > 10f) d *= 1.3f;
            if (WeatherSystem.I.IsRainy && V.exposed) d *= 1.5f;
            d *= Mathf.Lerp(1.4f, 0.8f, care);              // dikkatsiz kurye daha çok döker
            d *= UpgradeManager.I.DirtMultiplier;
            hygiene = Mathf.Max(0f, hygiene - d);
        }

        /// <summary>Çantayı kurye kendisi temizler (kendi sorumluluğu, şirkete maliyeti yok).</summary>
        public void CleanBag()
        {
            hygiene = MaxHygiene;
            cleanRequested = false;
        }

        /// <summary>
        /// Gün içinde: uyarıldıysa paket bitince hemen temizler; kirlenmişse titizliğine göre arada temizler.
        /// Uygulamada çanta fotoğrafı kontrolü varsa daha sık temizler.
        /// </summary>
        void HourlyHygiene()
        {
            if (OrderCount > 0 || hygiene >= MaxHygiene - 0.01f) return;
            if (cleanRequested) { CleanBag(); return; }
            // Herkesin kendi eşiği var: titiz kurye çanta biraz kirlenince, dağınık kurye ancak iyice kirlenince temizler
            float personal = HygieneLimit + 0.1f + tidiness * 0.25f;
            if (hygiene >= personal) return;
            float chance = 0.15f + tidiness * 0.45f;
            if (UpgradeManager.I != null) chance += UpgradeManager.I.HygieneCleanBonus;
            if (Random.value < chance) CleanBag();
        }

        /// <summary>Yeni termal çanta: yıpranma sıfırlanır, hijyen tam.</summary>
        public void ReplaceBag()
        {
            bagUses = 0;
            hygiene = 1f;
            cleanRequested = false;
            equipment.Add(EquipmentKind.TermalCanta);
            NoteGear(EquipmentKind.TermalCanta);
        }

        public string HygieneText =>
            hygiene >= 0.75f ? "Temiz" : hygiene >= HygieneLimit ? "Biraz kirli" : hygiene >= 0.25f ? "Kirli — düşük puan, şikayet riski" : "Çok kirli — şikayet riski yüksek";

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
            if (!Has(EquipmentKind.TermalCanta)) list.Add("Termal çanta yok: sıcak yemek 15 dk'da soğuyor (toplu siparişte risk)");
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
                    case CourierState.Bosta when cashBlocked: return "Hesabı kilitli: nakit borcunu kapatması gerekiyor";
                    case CourierState.DukkanaGidiyor: return "Dükkana gidiyor";
                    case CourierState.Bekliyor: return "Dükkanda bekliyor";
                    case CourierState.MusteriyeGidiyor: return wantsOffline ? "Son paketini götürüyor, sonra çıkacak" : "Müşteriye gidiyor";
                    case CourierState.AdresSorunu:
                        return current != null && current.addressIssue == AddressIssue.YanlisAdres
                            ? "Adres yanlış, müşteriye ulaşmaya çalışıyor" : "Müşteri evde yok, kapıda bekliyor";
                    case CourierState.KataCikiyor:
                        if (current == null) return "Teslim ediyor";
                        return current.floor == 0 ? $"#{current.id} kapıda teslim ediyor"
                             : current.UsesStairs ? $"#{current.id}: {current.floor}. kata merdivenle çıkıyor"
                             : $"#{current.id}: asansörle {current.floor}. kata çıkıyor";
                    case CourierState.Mola: return "Mola: " + breakReason;
                    case CourierState.Izinli: return $"Çalışamıyor ({leaveReason}, {unavailableDays} gün)";
                    default: return "Çevrimiçi, sipariş bekliyor";
                }
            }
        }
    }
}
