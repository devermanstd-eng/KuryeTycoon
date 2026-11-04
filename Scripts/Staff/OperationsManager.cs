using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Operasyon sorumlusu (aday ya da çalışan).</summary>
    [System.Serializable]
    public class OpsPerson
    {
        public string name;
        public float skill;            // 0.5–1
        public int monthlySalary;      // Ft / ay
        public string note;
        public int daysEmployedThisMonth;
        public float expiresAt;        // aday olarak başvurunun geçerliliğinin bittiği an
    }

    /// <summary>
    /// Maaşlı operasyon sorumlusu (tek kişi). Belirlediğin kurallara göre kendisi çalışır:
    ///  • her sabah 09:00'da eksik ekipmanı kuryelere teklif eder (ödeme şekli Ekipman sekmesindeki seçim),
    ///  • yıpranan çantaları yeniler,
    ///  • gün içinde çantası kirlenen kuryeleri uyarır.
    /// Teklifi kurye yine reddedebilir; reddedene 3 gün sonra tekrar sorar.
    /// Yetenek: ikna (kabul ihtimali +%0–15), işleri erteleme ihtimali ve toplu alım indirimi (%10).
    /// Maaşı ay başında, geçen ay çalıştığı gün kadar ödenir. Ne yaptığı gün sonu raporunda yazar.
    /// </summary>
    public class OperationsManager : MonoBehaviour
    {
        public static OperationsManager I { get; private set; }

        public OpsPerson Manager { get; private set; }
        public bool HasManager => Manager != null;
        public readonly List<OpsPerson> Candidates = new List<OpsPerson>();

        /// <summary>Hangi ekipmanlar teklif edilsin (EquipmentKind sırasıyla).</summary>
        public readonly bool[] offerKinds = { true, true, true, true, true, true };

        [Header("Kurallar")]
        [Tooltip("Bu kadar teslimat yapmamış yeni kuryeye ekipman sorulmaz (kısa süre kalacaklara para harcamamak için)")]
        public int minDeliveries = 10;
        [Tooltip("Sorumlunun bir ayda ekipmana harcayabileceği en fazla şirket parası")]
        public int monthlyBudget = 60000;
        public bool replaceWornBags = true;
        public bool warnDirtyBags = true;
        public const float RetryDays = 3f;

        public long spentThisMonth;
        public string lastPassSummary = "";

        // Gün içi kayıt (gün sonu raporu için)
        int dayOffers, dayAccepted, dayBags, dayWarned;
        long daySpent;
        bool dayPostponed, dayBudgetHit;
        readonly Dictionary<EquipmentKind, int> dayGiven = new Dictionary<EquipmentKind, int>();
        readonly List<string> dayRefused = new List<string>();

        int lastPassDay = -1;
        float lastHygieneAt = -1000f;
        bool ready;

        /// <summary>Önce mevsimlik ve güvenlik ekipmanı.</summary>
        static readonly EquipmentKind[] Priority =
        {
            EquipmentKind.KisMontu, EquipmentKind.Yagmurluk, EquipmentKind.YazTisort,
            EquipmentKind.Kask, EquipmentKind.TermalCanta, EquipmentKind.Powerbank
        };

        static readonly string[] Notes =
        {
            "Bir lojistik firmasında depo ve filo işlerine baktım.",
            "Eskiden kuryeydim, neyin eksik olduğunu yoldan bilirim.",
            "Tedarikçilerle pazarlık etmeyi severim.",
            "Excel'siz yaşayamam, her kuruşu takip ederim.",
            "İlk işim olacak ama düzenli biriyimdir.",
            "Bir yemek zincirinde vardiya müdürlüğü yaptım."
        };

        static string Ft(long v) => GameData.Ft(v);

        void Awake() { I = this; }

        public void Init()
        {
            RefreshCandidates();
            GameClock.I.OnNewDay += OnNewDay;
            GameClock.I.OnNewMonth += OnNewMonth;
            ready = true;
        }

        // ---------- Yetenek ----------

        /// <summary>Aylık maaş: yeteneğe göre 100 / 130 / 160 bin Ft.</summary>
        public static int SalaryFor(float skill) => skill >= 0.85f ? 160000 : (skill >= 0.7f ? 130000 : 100000);

        float SkillNorm => HasManager ? Mathf.InverseLerp(0.5f, 1f, Manager.skill) : 0f;
        /// <summary>İkna bonusu: kuryenin kabul ihtimaline eklenir.</summary>
        public float Persuasion => HasManager ? Mathf.Lerp(0f, 0.15f, SkillNorm) : 0f;
        /// <summary>Yetenekli sorumlu (0,85+) toplu alımda %10 indirim alır.</summary>
        public float Discount => HasManager && Manager.skill >= 0.85f ? 0.1f : 0f;
        /// <summary>Bir sabah işleri ertelemeden yapma ihtimali.</summary>
        public float Reliability => HasManager ? Mathf.Lerp(0.6f, 1f, SkillNorm) : 0f;
        public long BudgetLeft => System.Math.Max(0L, monthlyBudget - spentThisMonth);
        public int MonthlySalary => HasManager ? Manager.monthlySalary : 0;

        // ---------- İşe alım ----------

        void RefreshCandidates()
        {
            Candidates.Clear();
            for (int i = 0; i < 2; i++) AddCandidate();
        }

        void AddCandidate()
        {
            float skill = Random.Range(0.5f, 1f);
            Candidates.Add(new OpsPerson
            {
                name = GameData.RandomName(),
                skill = skill,
                monthlySalary = SalaryFor(skill),
                note = Notes[Random.Range(0, Notes.Length)],
                expiresAt = SupportManager.ExpiryFromNow()
            });
        }

        /// <summary>Başvuruyu reddet; yerine ertesi gün yenisi gelir.</summary>
        public void Reject(OpsPerson c)
        {
            if (Candidates.Remove(c)) EventManager.I.Notify($"{c.name} adlı adayın operasyon sorumluluğu başvurusu reddedildi.");
        }

        public void Hire(OpsPerson c)
        {
            if (HasManager || !Candidates.Remove(c)) return;
            c.daysEmployedThisMonth = 0;
            Manager = c;
            lastPassDay = -1; // saat 09:00'u geçtiyse bugünkü işlere hemen bakar
            EventManager.I.Notify($"{c.name} operasyon sorumlusu olarak işe başladı (maaş {Ft(c.monthlySalary)}/ay). " +
                                  "Her sabah 09:00'da kuryelerin eksiklerine bakacak.");
        }

        /// <summary>İşten çıkarırken o ay çalıştığı günlerin maaşı hemen ödenir.</summary>
        public void Fire()
        {
            if (!HasManager) return;
            var m = Manager;
            Manager = null;
            long due = Prorated(m, GameClock.I.DaysInMonth);
            if (due > 0) EconomyManager.I.Spend(due, $"Son maaş: {m.name} (operasyon)", ExpenseCategory.Maas);
            EventManager.I.Notify($"{m.name} operasyon sorumluluğundan ayrıldı (son maaşı {Ft(due)}).");
        }

        static long Prorated(OpsPerson m, int daysInMonth) =>
            (long)Mathf.Round(m.monthlySalary * Mathf.Min(1f, m.daysEmployedThisMonth / (float)Mathf.Max(1, daysInMonth)) / 1000f) * 1000;

        public long AccruedThisMonth => HasManager ? Prorated(Manager, GameClock.I.DaysInMonth) : 0;

        void OnNewDay()
        {
            if (HasManager) Manager.daysEmployedThisMonth++;
            float now = GameClock.I.TotalMinutes;
            Candidates.RemoveAll(c => c.expiresAt > 0f && now >= c.expiresAt); // süresi dolanlar
            if (GameClock.I.DayOfWeek == 0) RefreshCandidates();
            else if (Candidates.Count < 2) AddCandidate();
        }

        /// <summary>Ay başında: geçen ayın maaşı ödenir (çalışılan gün kadar), aylık ekipman bütçesi sıfırlanır.</summary>
        void OnNewMonth()
        {
            spentThisMonth = 0;
            if (!HasManager) return;
            // OnNewMonth çağrıldığında takvim yeni aya geçmiştir; geçen ayın gün sayısı için önceki ayı hesapla
            int prevMonth = (GameClock.I.Month + 10) % 12; // 0 tabanlı
            int[] md = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
            long pay = Prorated(Manager, md[prevMonth]);
            Manager.daysEmployedThisMonth = 0;
            if (pay <= 0) return;
            EconomyManager.I.Spend(pay, $"Operasyon sorumlusu maaşı ({Manager.name})", ExpenseCategory.Maas);
            EventManager.I.Notify($"Operasyon sorumlusuna aylık maaş ödendi: {Ft(pay)}.");
        }

        // ---------- Günlük iş ----------

        void Update()
        {
            if (!ready || !HasManager || GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            var clock = GameClock.I;
            if (clock == null || clock.DeltaMinutes <= 0f) return;

            int h = clock.Hour;
            if (h >= 9 && lastPassDay != clock.Day)
            {
                lastPassDay = clock.Day;
                MorningPass();
            }
            if (warnDirtyBags && h >= 10 && h < 22 && clock.TotalMinutes - lastHygieneAt >= 120f)
            {
                lastHygieneAt = clock.TotalMinutes;
                HygieneRound();
            }
        }

        string FirstName => Manager.name.Split(' ')[0];

        void Spent(long v)
        {
            spentThisMonth += v;
            daySpent += v;
        }

        /// <summary>Sabah turu: yıpranan çantalar ve eksik ekipman teklifleri.</summary>
        void MorningPass()
        {
            var cm = CourierManager.I;
            if (cm == null) return;

            if (Random.value > Reliability)
            {
                dayPostponed = true;
                lastPassSummary = $"{GameClock.I.DayName}: ekipman işlerini erteledi.";
                return;
            }

            int offers = 0, accepted = 0, bags = 0;
            var pay = cm.equipmentPayment;
            foreach (var c in cm.Couriers.ToArray())
            {
                if (replaceWornBags && c.BagWorn)
                {
                    long bagPrice = CourierManager.PriceOf(EquipmentKind.TermalCanta);
                    if (bagPrice > BudgetLeft) dayBudgetHit = true;
                    else if (cm.ReplaceBag(c)) { Spent(bagPrice); bags++; }
                }

                if (c.deliveries < minDeliveries) continue;
                foreach (var k in Priority)
                {
                    if (!offerKinds[(int)k] || c.Has(k) || cm.OnCooldown(c, k)) continue;
                    if (CourierManager.Usefulness(c, k) < 0.6f) continue; // sadece şimdi ya da yakında gerekeni sorar
                    long companyPart = CourierManager.CompanyCost(k, pay, Discount);
                    if (companyPart > BudgetLeft) { dayBudgetHit = true; continue; }

                    var r = cm.OfferEquipment(c, k, false, Persuasion, RetryDays, Discount);
                    if (r == OfferResult.Yapilamadi) continue;
                    offers++;
                    if (r == OfferResult.Kabul)
                    {
                        accepted++;
                        Spent(companyPart);
                        dayGiven[k] = dayGiven.TryGetValue(k, out int n) ? n + 1 : 1;
                    }
                    else dayRefused.Add($"{c.ShortName}: {GameData.Equip(k).name} ({CourierManager.RefuseReason(c, k, pay)})");
                }
            }

            dayOffers += offers;
            dayAccepted += accepted;
            dayBags += bags;
            lastPassSummary = offers == 0 && bags == 0
                ? $"{GameClock.I.DayName}: yapılacak iş yoktu."
                : $"{GameClock.I.DayName}: {offers} teklif, {accepted} kabul" + (bags > 0 ? $", {bags} çanta yenilendi" : "") + ".";
            if (offers > 0 || bags > 0)
                EventManager.I.Notify($"Operasyon ({FirstName}): {offers} ekipman teklifi, {accepted} kabul" +
                                      (bags > 0 ? $", {bags} çanta yenilendi" : "") + ". Ayrıntı gün sonu raporunda.");
        }

        /// <summary>Gün içinde çantası kirlenen kuryeleri uyarır. Yeteneksiz sorumlu bazen fark etmez.</summary>
        void HygieneRound()
        {
            var cm = CourierManager.I;
            if (cm == null) return;
            int n = 0;
            foreach (var c in cm.Couriers)
            {
                if (c.hygiene >= Courier.HygieneLimit || c.cleanRequested) continue;
                if (Random.value > Mathf.Lerp(0.5f, 1f, SkillNorm)) continue;
                cm.RequestCleaning(c, false);
                n++;
            }
            dayWarned += n;
            if (n > 0) EventManager.I.Notify($"Operasyon ({FirstName}): {n} kuryeyi çanta temizliği için uyardı.");
        }

        // ---------- Kayıt ----------

        public OpsSave CaptureSave() => new OpsSave
        {
            hasManager = HasManager,
            manager = Manager,
            offerKinds = (bool[])offerKinds.Clone(),
            minDeliveries = minDeliveries,
            monthlyBudget = monthlyBudget,
            replaceWornBags = replaceWornBags,
            warnDirtyBags = warnDirtyBags,
            spentThisMonth = spentThisMonth
        };

        public void RestoreSave(OpsSave s)
        {
            Manager = s.hasManager && s.manager != null && !string.IsNullOrEmpty(s.manager.name) ? s.manager : null;
            if (s.offerKinds != null)
                for (int i = 0; i < offerKinds.Length && i < s.offerKinds.Length; i++) offerKinds[i] = s.offerKinds[i];
            minDeliveries = s.minDeliveries;
            monthlyBudget = s.monthlyBudget;
            replaceWornBags = s.replaceWornBags;
            warnDirtyBags = s.warnDirtyBags;
            spentThisMonth = s.spentThisMonth;
            lastPassDay = -1;
        }

        /// <summary>Gün sonu raporu için bugünün özeti. Çağrılınca günlük sayaçlar sıfırlanır.</summary>
        public string TakeDayReport()
        {
            string s = null;
            if (HasManager)
            {
                var parts = new List<string>();
                if (dayPostponed) parts.Add("bugün ekipman işlerini erteledi");
                if (dayOffers > 0)
                {
                    string given = "";
                    if (dayGiven.Count > 0)
                    {
                        var g = new List<string>();
                        foreach (var kv in dayGiven) g.Add($"{GameData.Equip(kv.Key).name} ×{kv.Value}");
                        given = $" ({string.Join(", ", g)})";
                    }
                    parts.Add($"{dayOffers} teklif, {dayAccepted} kabul{given}");
                }
                if (dayRefused.Count > 0)
                {
                    int shown = Mathf.Min(5, dayRefused.Count);
                    string list = string.Join(", ", dayRefused.GetRange(0, shown));
                    if (dayRefused.Count > shown) list += $" ve {dayRefused.Count - shown} ret daha";
                    parts.Add("reddeden: " + list);
                }
                if (dayBags > 0) parts.Add($"{dayBags} yıpranmış çanta yenilendi");
                if (dayWarned > 0) parts.Add($"{dayWarned} kurye çanta temizliği için uyarıldı");
                if (daySpent > 0) parts.Add($"ekipmana {Ft(daySpent)} (bu ay {Ft(spentThisMonth)} / bütçe {Ft(monthlyBudget)})");
                if (dayBudgetHit) parts.Add("aylık ekipman bütçesi doldu, bazı teklifler yapılamadı");
                s = $"Operasyon sorumlusu ({Manager.name}): " + (parts.Count > 0 ? string.Join(" · ", parts) : "bugün yapılacak iş yoktu");
            }

            dayOffers = dayAccepted = dayBags = dayWarned = 0;
            daySpent = 0;
            dayPostponed = dayBudgetHit = false;
            dayGiven.Clear();
            dayRefused.Clear();
            return s;
        }
    }
}
