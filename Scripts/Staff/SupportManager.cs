using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    public enum SupportShift { Sabah, Ogle, Gece }
    public enum WorkDays { PztCum, CarPaz }

    /// <summary>Maaşlı destek personeli: sabit vardiya, haftada 5 gün.</summary>
    [System.Serializable]
    public class SupportAgent
    {
        public string name;
        public float skill;            // 0.5–1: şikayetleri ne kadar ucuza kapatır
        public int monthlySalary;      // Ft / ay
        public SupportShift shift;
        public WorkDays days;
        public int daysEmployedThisMonth;
        public int handledTotal;
        public long savedTotal;        // pazarlıkla şirkete kazandırdığı

        public string ShiftText => SupportManager.ShiftLabel(shift);
        public string DaysText => days == WorkDays.PztCum ? "Pazartesi–Cuma" : "Çarşamba–Pazar";

        bool WorksOn(int dayOfWeek) =>
            days == WorkDays.PztCum ? dayOfWeek <= 4 : dayOfWeek >= 2;

        public bool IsOnDuty(GameClock c)
        {
            int h = c.Hour;
            switch (shift)
            {
                case SupportShift.Sabah: return WorksOn(c.DayOfWeek) && h >= 6 && h < 14;
                case SupportShift.Ogle: return WorksOn(c.DayOfWeek) && h >= 14 && h < 22;
                default:
                    // Gece vardiyası 22:00'de başlar, ertesi gün 06:00'da biter
                    if (h >= 22) return WorksOn(c.DayOfWeek);
                    if (h < 6) return WorksOn((c.DayOfWeek + 6) % 7);
                    return false;
            }
        }

        public bool WorksShiftAt(int dayOfWeek, int hour)
        {
            switch (shift)
            {
                case SupportShift.Sabah: return WorksOn(dayOfWeek) && hour >= 6 && hour < 14;
                case SupportShift.Ogle: return WorksOn(dayOfWeek) && hour >= 14 && hour < 22;
                default:
                    if (hour >= 22) return WorksOn(dayOfWeek);
                    if (hour < 6) return WorksOn((dayOfWeek + 6) % 7);
                    return false;
            }
        }
    }

    /// <summary>Destek personeli başvurusu.</summary>
    public class SupportCandidate
    {
        public string name;
        public float skill;
        public int monthlySalary;
        public string note;
        public SupportShift chosenShift = SupportShift.Ogle;
        public WorkDays chosenDays = WorkDays.PztCum;
        public float expiresAt;    // başvurunun geçerliliğinin bittiği an (oyun dakikası, gece yarısı)
    }

    /// <summary>
    /// Destek ekibi: vardiyadaki personel
    ///  • eksik ürün / hasarlı sipariş şikayetlerini kendisi, pazarlıkla daha ucuza çözer,
    ///  • kurye bulunamayan siparişlerde müşteriyi arayıp bekletir (iptal gecikir, itibar kaybı azalır),
    ///  • kuryelere destek olur (günlük moral artışı).
    /// Maaşlar her ayın sonunda, o ay çalıştığı gün kadar ödenir.
    /// </summary>
    public class SupportManager : MonoBehaviour
    {
        public static SupportManager I { get; private set; }

        public readonly List<SupportAgent> Agents = new List<SupportAgent>();
        public readonly List<SupportCandidate> Candidates = new List<SupportCandidate>();
        public int candidatesPerWeek = 3;
        [Tooltip("Vardiyada destek varken kurye bulunamayan siparişe eklenen bekleme süresi (dk)")]
        public float extraWaitMinutes = 20f;

        public int handledThisMonth;
        bool ready;

        static string Ft(long v) => GameData.Ft(v);

        static readonly string[] Notes =
        {
            "Daha önce bir çağrı merkezinde çalıştım.",
            "Üç dil konuşuyorum: Türkçe, Macarca, İngilizce.",
            "Sabırlıyımdır, zor müşterilerle iyi anlaşırım.",
            "İlk işim olacak ama çok hızlı öğrenirim.",
            "Eskiden kuryeydim, kuryelerin dilinden anlarım.",
            "E-ticaret firmasında müşteri hizmetleri yöneticisiydim."
        };

        void Awake() { I = this; }

        public void Init()
        {
            RefreshCandidates();
            GameClock.I.OnNewDay += OnNewDay;
            GameClock.I.OnNewMonth += PaySalaries;
            ready = true;
        }

        public static string ShiftLabel(SupportShift s)
        {
            switch (s)
            {
                case SupportShift.Sabah: return "Sabah 06:00–14:00";
                case SupportShift.Ogle: return "Öğle 14:00–22:00";
                default: return "Gece 22:00–06:00";
            }
        }

        /// <summary>Prototip maaşları (aylık): yeteneğe göre 120 / 150 / 180 bin Ft.</summary>
        public static int SalaryFor(float skill) => skill >= 0.85f ? 180000 : (skill >= 0.7f ? 150000 : 120000);

        void RefreshCandidates()
        {
            Candidates.Clear();
            for (int i = 0; i < candidatesPerWeek; i++) AddCandidate();
        }

        void AddCandidate()
        {
            float skill = Random.Range(0.5f, 1f);
            Candidates.Add(new SupportCandidate
            {
                name = GameData.RandomName(),
                skill = skill,
                monthlySalary = SalaryFor(skill),
                note = Notes[Random.Range(0, Notes.Length)],
                chosenShift = (SupportShift)Random.Range(0, 3),
                chosenDays = Random.value < 0.5f ? WorkDays.PztCum : WorkDays.CarPaz,
                expiresAt = ExpiryFromNow()
            });
        }

        /// <summary>Başvurular yarın gece yarısına kadar geçerli (en az 1, en çok 2 gün).</summary>
        public const int CandidateValidDays = 2;

        public static float ExpiryFromNow() =>
            (Mathf.Floor(GameClock.I.TotalMinutes / 1440f) + CandidateValidDays) * 1440f;

        /// <summary>Başvuruyu reddet; yerine ertesi gün yenisi gelir.</summary>
        public void Reject(SupportCandidate c)
        {
            if (Candidates.Remove(c)) EventManager.I.Notify($"{c.name} adlı adayın destek başvurusu reddedildi.");
        }

        /// <summary>Süresi dolan başvuruları kaldırır.</summary>
        void PruneExpired()
        {
            float now = GameClock.I.TotalMinutes;
            Candidates.RemoveAll(c => c.expiresAt > 0f && now >= c.expiresAt);
        }

        public void Hire(SupportCandidate c)
        {
            if (UpgradeManager.I != null && Agents.Count >= UpgradeManager.I.SupportCap)
            {
                EventManager.I.Notify($"Ofiste yer yok: en fazla {UpgradeManager.I.SupportCap} destek personeli çalışabilir. Gelişim > Merkez ofis'i büyüt.");
                return;
            }
            if (!Candidates.Remove(c)) return;
            Agents.Add(new SupportAgent
            {
                name = c.name,
                skill = c.skill,
                monthlySalary = c.monthlySalary,
                shift = c.chosenShift,
                days = c.chosenDays
            });
            EventManager.I.Notify($"{c.name} destek ekibine katıldı ({ShiftLabel(c.chosenShift)}, maaş {Ft(c.monthlySalary)}/ay).");
        }

        /// <summary>İşten çıkarırken o ay çalıştığı günlerin maaşı hemen ödenir.</summary>
        public void Fire(SupportAgent a)
        {
            if (!Agents.Remove(a)) return;
            long due = ProratedSalary(a);
            if (due > 0) EconomyManager.I.Spend(due, $"Son maaş: {a.name}", ExpenseCategory.Maas);
            EventManager.I.Notify($"{a.name} destek ekibinden ayrıldı (son maaşı {Ft(due)}).");
        }

        public static long ProratedSalary(SupportAgent a)
        {
            int days = Mathf.Max(1, GameClock.I.DaysInMonth);
            return (long)Mathf.Round(a.monthlySalary * Mathf.Min(1f, a.daysEmployedThisMonth / (float)days) / 1000f) * 1000;
        }

        public long MonthlyPayroll
        {
            get
            {
                long s = 0;
                foreach (var a in Agents) s += a.monthlySalary;
                return s;
            }
        }

        public long AccruedThisMonth
        {
            get
            {
                long s = 0;
                foreach (var a in Agents) s += ProratedSalary(a);
                return s;
            }
        }

        void OnNewDay()
        {
            foreach (var a in Agents) a.daysEmployedThisMonth++;
            PruneExpired();
            if (GameClock.I.DayOfWeek == 0) RefreshCandidates();
            else if (Candidates.Count < 2) AddCandidate(); // işe alınan, reddedilen ya da süresi dolan adayın yerine yenisi gelir

            // Gün boyunca en az 8 saat destek varsa kuryelerin morali biraz artar
            if (CoveredHours(GameClock.I.DayOfWeek) >= 8)
                foreach (var c in CourierManager.I.Couriers) c.morale = Mathf.Min(1f, c.morale + 0.01f);
        }

        /// <summary>Ay başında geçen ayın maaşları ödenir (çalışılan gün kadar).</summary>
        void PaySalaries()
        {
            if (Agents.Count == 0) { handledThisMonth = 0; return; }
            // OnNewMonth çağrıldığında takvim yeni aya geçmiştir; geçen ayın gün sayısı için önceki ayı hesapla
            int prevMonth = (GameClock.I.Month + 10) % 12; // 0 tabanlı
            int[] md = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
            long total = 0;
            foreach (var a in Agents)
            {
                long pay = (long)Mathf.Round(a.monthlySalary * Mathf.Min(1f, a.daysEmployedThisMonth / (float)md[prevMonth]) / 1000f) * 1000;
                total += pay;
                a.daysEmployedThisMonth = 0;
            }
            EconomyManager.I.Spend(total, $"Destek ekibi maaşları ({Agents.Count} kişi)", ExpenseCategory.Maas);
            EventManager.I.Notify($"Destek ekibine aylık maaş ödendi: {Ft(total)}. Geçen ay {handledThisMonth} şikayet çözdüler.");
            handledThisMonth = 0;
        }

        public SupportAgent OnDutyAgent()
        {
            if (!ready) return null;
            SupportAgent best = null;
            foreach (var a in Agents)
                if (a.IsOnDuty(GameClock.I) && (best == null || a.skill > best.skill)) best = a;
            return best;
        }

        public int CoveredHours(int dayOfWeek)
        {
            int n = 0;
            for (int h = 0; h < 24; h++)
            {
                foreach (var a in Agents)
                    if (a.WorksShiftAt(dayOfWeek, h)) { n++; break; }
            }
            return n;
        }

        public bool CoveredAt(int dayOfWeek, int hour)
        {
            foreach (var a in Agents) if (a.WorksShiftAt(dayOfWeek, hour)) return true;
            return false;
        }

        /// <summary>
        /// Vardiyadaki destek şikayeti kendisi çözer: müşteriyle pazarlık edip değerin bir kısmını
        /// kupon olarak verir, itibar kaybı olmaz, kuryenin morali bozulmaz. Kimse yoksa false döner.
        /// </summary>
        public bool TryHandleIncident(Order o, Courier c, int value, string problem)
        {
            var a = OnDutyAgent();
            if (a == null) return false;

            float share = Mathf.Lerp(0.6f, 0.3f, Mathf.InverseLerp(0.5f, 1f, a.skill));
            long cost = GameData.Round10(value * share);
            EconomyManager.I.Spend(cost, $"Destek çözümü #{o.id} ({problem})", ExpenseCategory.Iade);
            if (a.skill >= 0.85f) CompanyProfile.AddReputation(0.02f);

            a.handledTotal++;
            a.savedTotal += value - cost;
            handledThisMonth++;
            EventManager.I.Notify($"Destek ({a.name.Split(' ')[0]}) #{o.id} {problem} şikayetini çözdü: %{share * 100f:0} kupon (−{Ft(cost)}, tam iade {Ft(value)} olacaktı).");
            return true;
        }

        /// <summary>
        /// Yanlış işaretlenmiş adres: vardiyadaki destek müşteriyi arayıp doğru adresi bulur.
        /// Başarı yeteneğe bağlı (%70–95). Başarırsa müşterinin şirkete güveni (itibar) artar.
        /// </summary>
        public bool TryResolveWrongAddress(Order o)
        {
            var a = OnDutyAgent();
            if (a == null) return false;
            a.handledTotal++;
            handledThisMonth++;
            if (Random.value > Mathf.Lerp(0.7f, 0.95f, Mathf.InverseLerp(0.5f, 1f, a.skill)))
            {
                EventManager.I.Notify($"Destek ({a.name.Split(' ')[0]}) #{o.id} müşterisine ulaşamadı.");
                return false;
            }
            CompanyProfile.AddReputation(0.05f);
            EventManager.I.Notify($"Destek ({a.name.Split(' ')[0]}) #{o.id} müşterisini arayıp doğru adresi buldu, kurye yönlendirildi. Müşterinin güveni arttı (itibar +0.05).");
            return true;
        }

        /// <summary>Müşteri evde yok: destek arar, %80 müşteri döner (itibar biraz artar).</summary>
        public bool TryReachAbsentCustomer(Order o)
        {
            var a = OnDutyAgent();
            if (a == null) return false;
            a.handledTotal++;
            handledThisMonth++;
            if (Random.value > 0.8f) return false;
            CompanyProfile.AddReputation(0.03f);
            EventManager.I.Notify($"Destek ({a.name.Split(' ')[0]}) #{o.id} müşterisini aradı, müşteri eve döndü ve siparişini aldı (itibar +0.03).");
            return true;
        }

        /// <summary>Kurye bulunamayan sipariş: destek müşteriyi arayıp bekletir.</summary>
        public bool TryCallCustomer(Order o)
        {
            if (o.supportCalled) return false;
            var a = OnDutyAgent();
            if (a == null) return false;
            o.supportCalled = true;
            a.handledTotal++;
            handledThisMonth++;
            EventManager.I.Notify($"Destek ({a.name.Split(' ')[0]}) #{o.id} müşterisini arayıp gecikme için özür diledi, {extraWaitMinutes:0} dk ek süre aldı.");
            return true;
        }
    }
}
