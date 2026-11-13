using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Teklif edilen ekipmanı kim öder.</summary>
    public enum EquipmentPayment { Sirket, YariYariya, Kurye }

    public enum OfferResult { Kabul, Ret, Yapilamadi }

    /// <summary>İş başvurusu yapan aday.</summary>
    public class CourierCandidate
    {
        public string name;
        public VehicleKind vehicle;
        public bool alsoOwnsBike;      // motorlu araç sahibinin yedekte bisikleti de var
        public VehicleKind dreamVehicle; // parası olursa almak istediği araç
        public Occupation occupation;
        public int scheduleSeed;       // öğrenciyse ders programı
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
            if (!ready || GameClock.I == null || GameClock.I.DeltaMinutes <= 0f) return;
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
                case Occupation.Ebeveyn: return "ebeveyn";
                case Occupation.GeceKuryesi: return "gece kuryesi";
                default: return "tam zamanlı";
            }
        }

        void ScheduleNextApplication()
        {
            float rep = 0.5f + 0.15f * CompanyProfile.Reputation;
            float bonus = Mathf.Lerp(0.8f, 1.3f, (EconomyManager.I.bonusMultiplier - 0.5f) / 1.5f);
            nextApplicationAt = GameClock.I.TotalMinutes + Random.Range(120f, 360f) / (rep * bonus * UpgradeManager.I.ApplicationMultiplier * Difficulty.ApplicationRate);
        }

        static Occupation RandomOccupation()
        {
            float r = Random.value;
            // Gerçekte kuryelerin çoğu geçimini bu işten sağlar; öğle yoğunluğunu da onlar karşılar
            if (r < 0.30f) return Occupation.TamZamanli;    // %30
            if (r < 0.42f) return Occupation.OfisCalisani;  // %12
            if (r < 0.60f) return Occupation.Ogrenci;       // %18
            if (r < 0.70f) return Occupation.HaftaSonu;     // %10
            if (r < 0.80f) return Occupation.Vardiyali;     // %10
            if (r < 0.87f) return Occupation.Emekli;        // %7
            if (r < 0.95f) return Occupation.Ebeveyn;       // %8
            return Occupation.GeceKuryesi;                  // %5
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
            var aday = new CourierCandidate
            {
                name = GameData.RandomName(),
                vehicle = v,
                alsoOwnsBike = v != VehicleKind.Bisiklet && Random.value < 0.4f,
                dreamVehicle = RandomDream(v),
                occupation = RandomOccupation(),
                scheduleSeed = Random.Range(1, 1000000),
                level = level,
                insured = Random.value < InsureChance(v),
                savings = Mathf.RoundToInt(Random.Range(0f, 60000f) / 1000f) * 1000,
                speedSkill = Random.Range(0.85f, 1.2f) + skill,
                care = Mathf.Min(1f, Random.Range(0.55f, 0.95f) + skill),
                politeness = Mathf.Min(1f, Random.Range(0.55f, 0.95f) + skill),
                stamina = Random.Range(0.7f, 1.2f) + skill,
                tidiness = Random.Range(0.2f, 1f),
                pickiness = Random.Range(0.05f, 0.85f),
                expiresAt = withExpiry ? GameClock.I.TotalMinutes + 1440f : float.MaxValue
            };
            aday.note = GameData.ApplicationNote(aday.occupation, aday.level);   // söz mesleğe ve tecrübeye uysun
            return aday;
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
            c.note = GameData.ApplicationNote(c.occupation, c.level);
            return Spawn(c);
        }

        public void Hire(CourierCandidate c)
        {
            if (UpgradeManager.I.CourierCapFull)
            {
                EventManager.I.Notify($"Kurye hesapları dolu ({UpgradeManager.I.CourierCap}). {UpgradeManager.I.CourierCapHint}");
                return;
            }
            if (!Applications.Remove(c)) return;
            Spawn(c);
            EventManager.I.Notify($"{c.name} ({GameData.Vehicle(c.vehicle).name}) ekibe katıldı!");
            if (GoalManager.I != null) GoalManager.I.OnHire();
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
        /// Kayıttan yüklenecek kurye: merkez ofiste oluşturulur, başlangıç kiti verilmez ve para kesilmez.
        /// Alanlarını SaveSystem doldurur, sonra Courier.AfterLoad çağrılır.
        /// </summary>
        public Courier SpawnRestored()
        {
            var go = new GameObject("Kurye");
            go.transform.SetParent(root, false);
            var courier = go.AddComponent<Courier>();
            courier.Init(MakeCandidate(VehicleKind.Bisiklet, false), CityMap.I.HQDoor);
            Couriers.Add(courier);
            return courier;
        }

        /// <summary>
        /// İşe başlayan kurye termal çanta ve tişörtü depozitoyla alır (Wolt'taki gibi): şirket depodan verir ya da satın alır,
        /// bedeli kuryenin ilk kazançlarından depozito olarak kesilir.
        /// </summary>
        void GiveStarterKit(Courier c)
        {
            foreach (var k in GameData.StarterKit) IssueGear(c, k, false);
        }

        // ---------- Depozitolu ekipman ----------
        // Ekipmanı şirket dağıtmaz: kurye ihtiyaç duyunca kendisi ister. Şirket depodan (iade edilenlerden) verir, depoda yoksa
        // satın alır; bedeli depozito olarak kuryenin kazancından kesilir. Kurye ayrılırken ekipmanı iade ederse depozito geri
        // ödenir ve ekipman depoya döner; iade etmezse depozito şirkette kalır.

        /// <summary>İade edilmiş, depoda bekleyen ekipman (tür başına adet).</summary>
        public readonly int[] gearStock = new int[System.Enum.GetValues(typeof(EquipmentKind)).Length];

        public bool IssueGear(Courier c, EquipmentKind k, bool notify = true)
        {
            if (c == null || c.Has(k)) return false;
            var e = GameData.Equip(k);
            long price = PriceOf(k);
            bool fromStock = gearStock[(int)k] > 0;
            if (fromStock) gearStock[(int)k]--;
            else EconomyManager.I.Spend(price, $"Ekipman alımı: {e.name} ({c.courierName} için)", ExpenseCategory.Ekipman);
            c.equipment.Add(k);
            c.NoteGear(k);
            if (c.depositGear == null) c.depositGear = new List<EquipmentKind>();
            c.depositGear.Add(k);
            c.equipRefusedUntil.Remove(k);
            if (!Sade.Depozito)
            {
                // Sade: ekipman şirketin malı, depozito kesilmez; kurye ayrılırken geri getirir (HandleLeaving)
                c.AddInbox($"{e.name} teslim aldın ({(fromStock ? "depodan" : "yeni")}). Şirket ekipmanı: ayrılırken iade etmelisin.");
                if (notify)
                    EventManager.I.Notify($"{c.ShortName} {e.name} istedi ve aldı ({(fromStock ? "depodan" : $"şirket satın aldı, {Ft(price)}")}).");
                return true;
            }
            c.depositOwed += price;
            EconomyManager.I.CollectGearDeposit(c);
            c.AddInbox($"{e.name} teslim aldın ({(fromStock ? "depodan" : "yeni")}). Depozito {Ft(price)}: ayrılırken iade edersen geri ödenir.");
            if (notify)
                EventManager.I.Notify($"{c.ShortName} {e.name} istedi ve aldı ({(fromStock ? "depodan" : "şirket satın aldı")}); {Ft(price)} depozito kazancından kesilecek.");
            return true;
        }

        /// <summary>Kuryeye ekipmanını almasını hatırlat (ücretsiz): 2 gün boyunca isteme ihtimali artar.</summary>
        public void RemindGear(Courier c, EquipmentKind k, bool notify = true)
        {
            if (c.Has(k)) return;
            c.gearNudgeUntil = GameClock.I.TotalMinutes + 2f * 1440f;
            c.AddInbox($"Hatırlatma: {GameData.Equip(k).name} almak ister misin? Depozito karşılığı ofisten alabilirsin.");
            if (notify) EventManager.I.Notify($"{c.ShortName}'a {GameData.Equip(k).name} hatırlatıldı.");
        }

        public int RemindAll(EquipmentKind k)
        {
            int n = 0;
            foreach (var c in Couriers)
                if (!c.Has(k) && Usefulness(c, k) >= 0.5f) { RemindGear(c, k, false); n++; }
            EventManager.I.Notify(n > 0 ? $"{GameData.Equip(k).name}: {n} kuryeye hatırlatıldı." : $"{GameData.Equip(k).name}: hatırlatılacak kurye yok.");
            return n;
        }

        static float BaseNeed(EquipmentKind k)
        {
            switch (k)
            {
                case EquipmentKind.TermalCanta: return 0.5f;
                case EquipmentKind.KisMontu: return 0.35f;
                case EquipmentKind.Yagmurluk: return 0.3f;
                case EquipmentKind.YazTisort: return 0.3f;
                case EquipmentKind.Kask: return 0.12f;
                default: return 0.1f;   // powerbank
            }
        }

        /// <summary>Her sabah: kurye eksik ve şu an işine yarayan bir ekipmanı kendisi isteyebilir (günde en fazla bir tane).</summary>
        void DailyGearRequests()
        {
            float now = GameClock.I.TotalMinutes;
            foreach (var c in Couriers)
            {
                if (c.deliveries < 3) continue;   // önce birkaç teslimat yapıp işi tanısın
                bool nudged = now < c.gearNudgeUntil;
                foreach (EquipmentKind k in System.Enum.GetValues(typeof(EquipmentKind)))
                {
                    if (c.Has(k)) continue;
                    float u = Usefulness(c, k);
                    if (u < 0.5f) continue;
                    float p = BaseNeed(k) * u * Mathf.Lerp(0.6f, 1.2f, c.care) * (nudged ? 2f : 1f);
                    if (k == EquipmentKind.Kask) p *= Mathf.Lerp(0.5f, 1.3f, c.care);
                    if (c.balance + c.savings < PriceOf(k)) p *= 0.3f;     // depozitoyu karşılayamayacaksa çekinir
                    if (Random.value < p) { IssueGear(c, k); break; }
                }
            }
        }

        /// <summary>
        /// Ayrılan kurye depozitolu ekipmanı iade ederse kesilmiş depozitosu geri ödenir ve ekipman depoya döner.
        /// İade etmezse kesilmiş depozito şirkette kalır.
        /// </summary>
        void HandleLeaving(Courier c)
        {
            Telemetry.Add("Kurye ayrıldı");
            Telemetry.Line($"KURYE AYRILDI {c.courierName} · moral {c.morale:0.00} · yorgunluk {c.fatigue:0.00}");
            EconomyManager.I.FinalSettlement(c);   // nakit ve hakedişler kapatılır
            c.MigrateGear();
            if (c.depositGear == null || c.depositGear.Count == 0) return;
            string gear = string.Join(", ", c.depositGear.ConvertAll(k => GameData.Equip(k).name));
            // Depozitosu tamamen kesilmiş kurye ekipmanı daha çok iade eder (parasını geri almak ister)
            if (!Sade.Depozito) c.depositOwed = 0;   // sade: kesilecek depozito yok
            bool returns = Random.value < (c.depositOwed <= 0 ? 0.85f : 0.55f);
            long paid = c.depositPaidTotal;
            if (returns)
            {
                foreach (var k in c.depositGear) gearStock[(int)k]++;
                EconomyManager.I.RefundGearDeposit(c);
                EventManager.I.Notify($"{c.courierName} ekipmanını iade etti ({gear})" +
                                      (paid > 0 ? $"; {GameData.Ft(paid)} depozitosu geri ödendi." : Sade.Depozito ? "; depozitosu henüz kesilmemişti." : ".") + " Ekipman depoya döndü.");
            }
            else
            {
                EconomyManager.I.KeepGearDeposit(c);
                EventManager.I.Notify($"{c.courierName} ekipmanını iade etmedi ({gear}); " +
                                      (paid > 0 ? $"{GameData.Ft(paid)} depozito şirkette kaldı." : Sade.Depozito ? "depozito kesilmemişti, ekipman kayıp." : "ekipman kayıp.") +
                                      (c.depositOwed > 0 ? $" Kesilemeyen {GameData.Ft(c.depositOwed)} kayıp." : ""));
            }
            c.depositGear.Clear();
            c.depositOwed = 0;
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

        // ---------- Ekipman teklifi ----------
        // Kuryeler serbest çalışır: ekipman zorla verilmez, teklif edilir. Kurye kabul ederse şirket ekipmanı alır,
        // ödeme şekline göre kuryenin payı hesabından kesilir. Reddederse bir süre aynı ekipman tekrar sorulamaz.

        [Header("Ekipman teklifi")]
        public EquipmentPayment equipmentPayment = EquipmentPayment.Sirket;
        [Tooltip("Elle yapılan teklif reddedilince aynı ekipmanın tekrar sorulabilmesi için geçmesi gereken gün")]
        public float manualRetryDays = 1f;

        public static float CourierShare(EquipmentPayment p) =>
            p == EquipmentPayment.Sirket ? 0f : (p == EquipmentPayment.YariYariya ? 0.5f : 1f);

        public static string PaymentLabel(EquipmentPayment p) =>
            p == EquipmentPayment.Sirket ? "Şirket öder" : (p == EquipmentPayment.YariYariya ? "Yarı yarıya" : "Kurye öder (hesabından)");

        /// <summary>İndirimli fiyat (operasyon sorumlusu toplu alımda indirim alabilir).</summary>
        public static long PriceOf(EquipmentKind k, float discount = 0f) =>
            GameData.Round10(GameData.Equip(k).price * (1f - discount));

        /// <summary>Kuryenin hesabından kesilecek pay.</summary>
        public static long CourierCost(EquipmentKind k, EquipmentPayment p, float discount = 0f) =>
            GameData.Round10(PriceOf(k, discount) * CourierShare(p));

        /// <summary>Şirketin cebinden çıkacak kısım.</summary>
        public static long CompanyCost(EquipmentKind k, EquipmentPayment p, float discount = 0f) =>
            PriceOf(k, discount) - CourierCost(k, p, discount);

        /// <summary>Bu ekipman şu an bu kuryenin işine ne kadar yarıyor (0,1–1).</summary>
        public static float Usefulness(Courier c, EquipmentKind k)
        {
            var season = GameClock.I.Season;
            var w = WeatherSystem.I;
            bool exposed = c.V.exposed;
            switch (k)
            {
                case EquipmentKind.KisMontu:
                    if (!exposed) return 0.1f;
                    return season == Season.Kis || w.IsCold ? 1f : (season == Season.Sonbahar ? 0.6f : 0.2f);
                case EquipmentKind.Yagmurluk:
                    if (!exposed) return 0.1f;
                    return season == Season.Ilkbahar || season == Season.Sonbahar || w.IsRainy ? 1f : 0.3f;
                case EquipmentKind.YazTisort:
                    if (!exposed) return 0.1f;
                    return season == Season.Yaz || w.IsHot ? 1f : (season == Season.Ilkbahar ? 0.5f : 0.2f);
                case EquipmentKind.Kask:
                    return exposed ? 1f : 0.1f;
                default:
                    return 1f; // termal çanta ve powerbank her zaman işe yarar
            }
        }

        static bool PartTime(Courier c) => c.occupation == Occupation.Ogrenci || c.occupation == Occupation.HaftaSonu;

        /// <summary>
        /// Kuryenin teklifi kabul etme ihtimali (0–1). Ödeme şekli, mevsim, ne kadar kalmayı düşündüğü,
        /// hesabındaki para, morali ve (kask için) dikkati etkiler. persuasion: operasyon sorumlusunun ikna bonusu.
        /// </summary>
        public float AcceptChance(Courier c, EquipmentKind k, EquipmentPayment pay, float persuasion = 0f, float discount = 0f)
        {
            float share = CourierShare(pay);
            long cost = CourierCost(k, pay, discount);
            if (cost > 0 && c.balance < cost) return 0f;           // hesabında yeterli para yok

            float p = pay == EquipmentPayment.Sirket ? 0.95f : (pay == EquipmentPayment.YariYariya ? 0.75f : 0.45f);
            float u = Usefulness(c, k);
            p *= 1f - (1f - u) * (0.2f + 0.5f * share);             // mevsimi değilse: bedavaya çoğu alır, parayla az kişi
            if (share > 0f)
            {
                if (PartTime(c)) p *= 1f - 0.25f * share;            // uzun kalmayı düşünmeyen parasını harcamak istemez
                if (c.deliveries < 20) p *= 1f - 0.15f * share;      // yeni kurye şirkete henüz güvenmiyor
                if (cost > 0) p *= Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(c.balance / (cost * 4f))); // parası az olan daha az ister
            }
            p *= Mathf.Lerp(0.75f, 1.05f, c.morale);                  // moralsiz kurye şirkete güvenmez
            if (k == EquipmentKind.Kask) p *= Mathf.Lerp(0.6f, 1f, c.care); // dikkatsizler kaskı rahatsız bulur
            return Mathf.Clamp01(p + persuasion);
        }

        /// <summary>Reddetme nedeni (bildirim ve rapor için kısa metin).</summary>
        public static string RefuseReason(Courier c, EquipmentKind k, EquipmentPayment pay)
        {
            long cost = CourierCost(k, pay);
            if (cost > 0 && c.balance < cost) return "hesabında yeterli para yok";
            if (Usefulness(c, k) < 0.5f) return c.V.exposed ? "şu an gerek görmüyor" : "arabayla çalıştığı için gerek görmüyor";
            if (k == EquipmentKind.Kask && c.care < 0.65f) return "kaskı rahatsız buluyor";
            if (CourierShare(pay) > 0f && PartTime(c)) return "uzun süre çalışmayı düşünmüyor, para harcamak istemiyor";
            if (c.morale < 0.4f) return "morali bozuk, şirkete güvenmiyor";
            if (CourierShare(pay) > 0f) return "parasını kendine saklamak istiyor";
            return "şimdilik istemiyor";
        }

        public bool OnCooldown(Courier c, EquipmentKind k) =>
            c.equipRefusedUntil.TryGetValue(k, out float until) && GameClock.I.TotalMinutes < until;

        /// <summary>Reddedilen teklifin tekrar sorulabilmesine kalan süre (metin).</summary>
        public string CooldownText(Courier c, EquipmentKind k)
        {
            if (!c.equipRefusedUntil.TryGetValue(k, out float until)) return "";
            float h = (until - GameClock.I.TotalMinutes) / 60f;
            return h >= 24f ? $"{Mathf.CeilToInt(h / 24f)} gün" : $"{Mathf.Max(1, Mathf.CeilToInt(h))} saat";
        }

        /// <summary>
        /// Ekipmanı kuryeye teklif eder. Serbest kasa şirketin payına yetmezse teklif yapılmaz.
        /// Kabulde şirket ekipmanı alır, kuryenin payı hesabından kesilir; retteki kurye retryDays gün tekrar sorulamaz.
        /// </summary>
        public OfferResult OfferEquipment(Courier c, EquipmentKind k, bool notify = true, float persuasion = 0f,
                                          float retryDays = -1f, float discount = 0f)
        {
            if (c == null || c.Has(k) || !Couriers.Contains(c) || OnCooldown(c, k)) return OfferResult.Yapilamadi;
            var e = GameData.Equip(k);
            var eco = EconomyManager.I;
            var pay = equipmentPayment;
            long price = PriceOf(k, discount);
            long courierPart = CourierCost(k, pay, discount);
            long companyPart = price - courierPart;

            if (eco.FreeCash < companyPart)
            {
                if (notify) EventManager.I.Notify($"Yapılamadı: {e.name} → {c.ShortName} (şirket payı {Ft(companyPart)}). Serbest kasa {Ft(eco.FreeCash)}.");
                return OfferResult.Yapilamadi;
            }

            if (Random.value >= AcceptChance(c, k, pay, persuasion, discount))
            {
                c.equipRefusedUntil[k] = GameClock.I.TotalMinutes + (retryDays >= 0f ? retryDays : manualRetryDays) * 1440f;
                if (notify) EventManager.I.Notify($"{c.ShortName} {e.name} teklifini reddetti: {RefuseReason(c, k, pay)}.");
                return OfferResult.Ret;
            }

            eco.Spend(price, $"{e.name} → {c.courierName}", ExpenseCategory.Ekipman);
            if (courierPart > 0) eco.ChargeCourier(c, courierPart, $"{e.name} payı: {c.courierName}");
            c.equipment.Add(k);
            c.NoteGear(k);
            c.AddInbox(courierPart == 0 ? $"{e.name} teslim aldın (şirket ödedi)." : $"{e.name} teslim aldın, {Ft(courierPart)} hesabından kesildi.");
            c.equipRefusedUntil.Remove(k);
            c.morale = Mathf.Min(1f, c.morale + (courierPart == 0 ? 0.03f : 0.01f));
            if (notify)
                EventManager.I.Notify(courierPart == 0
                    ? $"{c.ShortName} {e.name} teklifini kabul etti."
                    : $"{c.ShortName} {e.name} teklifini kabul etti, {Ft(courierPart)} hesabından kesildi.");
            return OfferResult.Kabul;
        }

        // ---------- Hijyen ----------

        /// <summary>Kuryeyi uyar: elindeki paket bitince çantasını kendisi temizler (uyarılmak moralini biraz düşürür).</summary>
        public void RequestCleaning(Courier c, bool notify = true)
        {
            if (c.cleanRequested) return;
            c.cleanRequested = true;
            c.morale = Mathf.Max(0f, c.morale - 0.02f);
            c.AddInbox("Çantan kirli, lütfen temizle. Müşteriler şikayet ediyor.");
            if (notify) EventManager.I.Notify($"{c.ShortName} uyarıldı: paketi bitince çantasını temizleyecek.");
        }

        /// <summary>Hesabı 1 gün dondur: kurye bugün çevrimiçi olamaz (izinli sayılır), morali düşer.</summary>
        public void SuspendForDay(Courier c)
        {
            c.Suspend(1);
            c.morale = Mathf.Max(0f, c.morale - 0.15f);
            c.AddInbox("Hesabın hijyen şikayetleri yüzünden 1 gün donduruldu. Çantanı temizle.");
            EventManager.I.Notify($"{c.ShortName}'ın hesabı 1 gün donduruldu.");
        }

        /// <summary>Çantası kirli tüm kuryeleri uyar (ücretsiz).</summary>
        public int WarnAllDirty()
        {
            int n = 0;
            foreach (var c in Couriers)
                if (c.hygiene < Courier.HygieneLimit && !c.cleanRequested) { RequestCleaning(c); n++; }
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

        /// <summary>
        /// Eksik olan herkese teklif et. İşine yaramayanlara (ör. arabalı kuryeye mont) ve
        /// yakın zamanda reddedenlere sorulmaz. Sonuç tek bildirimde özetlenir.
        /// </summary>
        public int OfferToAll(EquipmentKind k)
        {
            int accepted = 0, skipped = 0;
            bool noCash = false;
            var refused = new List<string>();
            foreach (var c in Couriers.ToArray())
            {
                if (c.Has(k) || OnCooldown(c, k)) continue;
                if (Usefulness(c, k) < 0.15f) { skipped++; continue; }
                var r = OfferEquipment(c, k, false);
                if (r == OfferResult.Kabul) accepted++;
                else if (r == OfferResult.Ret) refused.Add($"{c.ShortName} ({RefuseReason(c, k, equipmentPayment)})");
                else { noCash = true; break; } // serbest kasa yetmedi: devam etme
            }
            string name = GameData.Equip(k).name;
            string cashNote = noCash ? $" Serbest kasa yetmediği için kalanlara sorulamadı ({Ft(EconomyManager.I.FreeCash)})." : "";
            if (accepted == 0 && refused.Count == 0)
                EventManager.I.Notify((skipped > 0 ? $"{name}: sorulacak kurye yok (arabalılara sorulmaz)." : $"{name}: sorulacak kurye yok.") + cashNote);
            else
                EventManager.I.Notify($"{name} teklifi: {accepted} kurye kabul etti" +
                                      (refused.Count > 0 ? $", {refused.Count} reddetti — {string.Join(", ", refused)}" : "") + "." + cashNote);
            return accepted;
        }

        /// <summary>Aracına göre makul saatlik kazanç çarpanı (arabalı yakıt öder, bisikletli daha azına razı).</summary>
        static float AracBeklentisi(VehicleKind v) =>
            v == VehicleKind.Araba ? 1.25f : v == VehicleKind.Motosiklet ? 1.1f : v == VehicleKind.EScooter ? 0.9f : 0.85f;

        /// <summary>Kuryenin moralinin gideceği yer (0–1).</summary>
        static float MoralHedefi(Courier c, float saat, long kazanc, bool satIdle, Season season, float bonusM)
        {
            float hedef;
            if (saat >= 1f)
            {
                float oran = kazanc / saat / (Denge.BeklenenSaatlikKazanc * AracBeklentisi(c.vehicle));
                hedef = 0.3f + 0.5f * Mathf.Clamp01((oran - 0.4f) / 0.9f);   // beklenenin %100'ü → ~0,63; %130'u → 0,8
            }
            else hedef = Mathf.Lerp(c.morale, 0.6f, 0.3f);   // çalışmadı (izin): kazanç etkisi yok
            hedef += (bonusM - 1f) * 0.15f + Mathf.Min(0.06f, c.equipment.Count * 0.01f);
            hedef -= c.burnout * 0.3f;
            if (satIdle) hedef -= 0.1f;   // çevrimiçi bekledi, iş gelmedi
            if (c.V.exposed && season == Season.Kis && !c.Has(EquipmentKind.KisMontu)) hedef -= 0.12f;
            if (c.V.exposed && season == Season.Yaz && !c.Has(EquipmentKind.YazTisort)) hedef -= 0.06f;
            if (c.fatigue > 0.7f) hedef -= 0.05f;
            return Mathf.Clamp01(hedef);
        }

        // ================================================================== gün sonu

        void OnNewDay()
        {
            if (GameBootstrap.I != null && GameBootstrap.I.Started) DailyGearRequests();
            var clock = GameClock.I;
            var season = clock.Season;
            float bonusM = EconomyManager.I.bonusMultiplier;
            bool monday = clock.DayOfWeek == 0;
            bool monthEnd = clock.IsLastDayOfMonth(clock.Day - 1);

            // Nakit kapatma (keep cash / offload) ve erken ödeme istekleri
            foreach (var c in Couriers)
            {
                c.SettleCash(monthEnd);
                c.MaybeEarlyPayout();
            }

            foreach (var c in Couriers.ToArray())
            {
                int worked = c.deliveriesToday;
                bool satIdle = c.onlineMinutesToday > 120f && worked == 0;
                float saat = c.onlineMinutesToday / 60f;
                long kazanc = c.earnedToday;
                c.NewDay(clock);

                // --- Moral: her gece bir hedefe yaklaşır (Denge.MoralYaklasma) ---
                // Hedef: saatlik kazancı aracına göre makul mü, yorgunluk birikmiş mi, ekipmanı ve bonusu var mı.
                // Eskiden her iyi teslimat moral ekliyordu ve moral 60 gün %100'de kalıyordu.
                c.morale = Mathf.Clamp01(c.morale + (MoralHedefi(c, saat, kazanc, satIdle, season, bonusM) - c.morale) * Denge.MoralYaklasma);

                // Çok mutsuz kurye bırakır: moral 0,2'de günde %5, 0,1'de %10
                float birak = (Denge.BirakmaMoralSiniri - c.morale) * Denge.BirakmaOrani;
                if (birak > 0f && c.OrderCount == 0 && Random.value < birak)
                {
                    Quit(c);
                    continue;
                }

                // --- Kendi parası: sigorta ve araç ---
                if (monday && c.insured && !c.PayInsurance())
                    EventManager.I.Notify($"{c.courierName} sigorta primini ödeyemedi, sigortası düştü.");
                var eco = EconomyManager.I;
                if (!c.insured && Random.value < 0.02f * eco.Plan.buyMultiplier && c.TryBuyInsurance())
                    EventManager.I.Notify(c.insuredViaCompany
                        ? $"{c.courierName} anlaşmalı sigortacı üzerinden sigorta yaptırdı ({eco.Plan.name})."
                        : $"{c.courierName} kendine sigorta yaptırdı.");
                // Dışarıdan sigortalı kurye, şirketin anlaşması daha uygunsa Pazartesi geçiş yapabilir
                if (monday && c.insured && !c.insuredViaCompany && eco.HasInsurancePartner &&
                    Random.value < 0.12f * eco.Plan.buyMultiplier * (eco.Plan.premium <= Courier.InsurancePremium ? 1.5f : 0.6f))
                {
                    c.insuredViaCompany = true;
                    c.AddInbox($"Sigortanı {eco.Plan.name}'ye taşıdın (haftalık {Ft(eco.Plan.premium)}).");
                    EventManager.I.Notify($"{c.courierName} sigortasını anlaşmalı sigortacıya taşıdı.");
                }
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
            EventManager.I.Notify($"Yeni araç: {c.courierName} biriktirdiği parayla {v.name} aldı ({Ft(v.ownPrice)}). " +
                                  $"Artık paket başı {Ft(v.basePay)} + {Ft(v.payPerKm)}/km kazanıyor, aynı anda {v.capacity} paket taşıyor.{extra}");
            c.morale = Mathf.Min(1f, c.morale + 0.1f);
        }

        bool RollBreakdown(Courier c)
        {
            if (Random.value >= c.V.breakdownRisk * UpgradeManager.I.BreakdownMultiplier) return false;
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

        /// <summary>Rakip şirket kuryeyi ayarttı.</summary>
        public void PoachToRival(Courier c, string rivalName, bool card = true)
        {
            if (card) EventManager.I.Push(new GameEvent("Kurye rakibe geçti",
                $"{c.courierName} ({ShortOccupation(c.occupation)}) {TurkceEk.In(rivalName)} daha iyi boost teklifini kabul edip oraya geçti. " +
                "Morali düşük kuryeler rakibin tekliflerine daha açık."));
            HandleLeaving(c);
            Couriers.Remove(c);
            Destroy(c.gameObject);
        }

        /// <summary>
        /// Kurye kendi isteğiyle ayrılır (zor karar olayları). Elinde paket varsa önce onları bitirir:
        /// morali sıfırlanır ve gece büyük ihtimalle ayrılır.
        /// </summary>
        public void Resign(Courier c, string reason)
        {
            if (c == null || !Couriers.Contains(c)) return;
            if (c.OrderCount > 0)
            {
                c.morale = 0f;
                EventManager.I.Notify($"{c.courierName} ayrılmaya karar verdi ({reason}); elindeki paketleri bitirince bırakacak.");
                return;
            }
            EventManager.I.Notify($"{c.courierName} ayrıldı: {reason}.");
            HandleLeaving(c);
            Couriers.Remove(c);
            Destroy(c.gameObject);
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
                if (c.insuredViaCompany) days = Mathf.Max(1, days - EconomyManager.I.Plan.accidentDaysSaved);
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
            if (buys)
            {
                c.insured = true; // iyileşince sigorta yaptırmaya karar verdi (prim Pazartesi kesilir)
                c.insuredViaCompany = EconomyManager.I.HasInsurancePartner;
            }

            EventManager.I.Notify($"{c.courierName} kaza geçirdi, {days} gün çalışamayacak. {helmetText}" +
                $"Sigortası olmadığı için ~{Ft(cost)} masrafı kendi ödedi, morali çok düştü." +
                (buys ? " Sigorta yaptırmaya karar verdi." : ""));
        }
    }
}
