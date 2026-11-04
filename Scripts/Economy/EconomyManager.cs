using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Teslimat dışı giderlerin türü (Finans'ta ayrı gösterilir).</summary>
    public enum ExpenseCategory { Kira, Ekipman, Sozlesme, Bolge, Iade, Kaza, Reklam, Diger, Vergi, Maas, Gelistirme }

    public class PeriodStats
    {
        public static readonly string[] CategoryNames =
            { "Ofis kirası", "Ekipman", "Sözleşmeler", "Yeni bölge", "İade / tazminat", "Kaza desteği", "Reklam", "Diğer", "Vergi ve harçlar", "Personel maaşları", "Geliştirmeler" };

        public long commission;     // dükkan komisyonları
        public long fees;           // müşteriden alınan teslimat ücretleri
        public long otherIncome;    // kurye kesintileri vb.
        public long courierPay;     // kurye ödemeleri (paket + km + bonus)
        public long boosts;         // boost ödemeleri (kârdan)
        public long rewards;        // haftalık / aylık hedef ödülleri (kârdan)
        public readonly long[] byCategory = new long[11];
        public long penalties;      // vergi cezası/faizi (vergiden düşülmez)
        public long payoutsPaid;    // kuryelerin hesabına yatan (hakediş ödemesi)
        public long cashCollected;  // kuryelerden tahsil edilen nakit (offload)
        public int cashOrders;      // nakit ödemeli sipariş sayısı
        public long loanPaid;       // aile borcuna ödenen (gider değil)
        public long depositNet;     // alınan − iade edilen depozito (gelir değil)
        public int deliveries;

        public long OtherExpenses
        {
            get
            {
                long s = 0;
                foreach (var v in byCategory) s += v;
                return s;
            }
        }

        public long Income => commission + fees + otherIncome;
        public long Expenses => courierPay + boosts + rewards + OtherExpenses;
        /// <summary>Teslimatlardan kalan: komisyon + teslimat ücreti − kurye − boost.</summary>
        public long DeliveryProfit => commission + fees - courierPay - boosts;
        /// <summary>Vergilendirilebilir kâr (giderler düşülmüş, cezalar hariç).</summary>
        public long Profit => Income - Expenses;

        /// <summary>Başka bir dönemin rakamlarını bu döneme ekler (günler → hafta).</summary>
        public void Add(PeriodStats o)
        {
            commission += o.commission; fees += o.fees; otherIncome += o.otherIncome;
            courierPay += o.courierPay; boosts += o.boosts; rewards += o.rewards;
            for (int i = 0; i < byCategory.Length; i++) byCategory[i] += o.byCategory[i];
            penalties += o.penalties; loanPaid += o.loanPaid; depositNet += o.depositNet;
            payoutsPaid += o.payoutsPaid; cashCollected += o.cashCollected; cashOrders += o.cashOrders;
            deliveries += o.deliveries;
        }
    }

    /// <summary>
    /// Para, fiyatlandırma, kurye ödemeleri, yıl içindeki sürpriz vergiler ve yıl sonu vergisi.
    ///
    /// Kurye ödemesi = paket başı ücret + km başı ücret × km + ek ücret + bonus
    /// Şirket geliri = sepet × komisyon + teslimat ücreti
    /// Net kâr       = şirket geliri − kurye ödemesi − boost
    /// Teslimat ücreti, en pahalı kuryeye bile verilse min. kâr kalacak şekilde hesaplanır.
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        public static EconomyManager I { get; private set; }

        [Header("Kasa")]
        public long money = 300000;
        public long taxDebt;
        /// <summary>Kuryelerden alınan ekipman depozitoları (gelir değil, iade borcu).</summary>
        public long depositsHeld;

        [Header("Fiyatlandırma (müşteriden)")]
        public int baseFee = 590;
        public int feePerKm = 150;
        public int minProfit = 300;
        [Tooltip("Atamada sipariş başı en az bu kadar kâr kalmalı; daha azını bırakan kuryeye atanmaz")]
        public int minAssignProfit = 150;
        [Tooltip("Bu kadar dakika bekleyen siparişte atama için kâr şartı 0'a iner (iptal olmasın)")]
        public float relaxAfterMinutes = 15f;

        /// <summary>Bu sipariş için atamada şart koşulan en düşük kâr.</summary>
        public int RequiredAssignProfit(Order o) =>
            GameClock.I.TotalMinutes - o.createdAt >= relaxAfterMinutes ? 0 : minAssignProfit;

        [Header("Kurye bonusları")]
        public float bonusMultiplier = 1f;
        public int nightBonus = 200;
        public int rushBonus = 100;

        [Header("Vergi")]
        [Range(0f, 0.5f)] public float taxRate = 0.15f;
        [Tooltip("Yıl içinde sürpriz vergi/harç kaç günde bir çıkar (en az–en çok)")]
        public int surpriseMinDays = 6;
        public int surpriseMaxDays = 16;

        [Header("Sabit giderler")]
        public int officeRentPerDay = 4000;

        [Header("Gün sonu raporu")]
        public bool showDailyReport = true;
        public bool showWeeklyReport = true;
        public string LastWeeklyReport = "";
        public string LastWeeklyReportTitle = "";

        // Haftalık rapor için biriken veriler
        PeriodStats week = new PeriodStats();
        int weekStartDay = -1;
        int weekBatched, weekCancelled, weekLost, weekHeavyLost, weekCancelledRejected, weekBusyLost;
        readonly int[] weekCancelByHour = new int[24];
        float repAtWeekStart = -1f;
        long cashAtWeekStart = long.MinValue;
        long lastWeekNet = long.MinValue;
        readonly List<(string date, int deliveries, long net)> weekDays = new List<(string, int, long)>();
        public string LastDailyReport = "";
        public string LastDailyReportTitle = "";
        long cashAtLastReport = long.MinValue;

        /// <summary>Oyun başında kasa işaretlerini ayarlar (rapordaki "dünden/hafta başından" farkları için).</summary>
        public void InitCashMarkers()
        {
            cashAtLastReport = money;
            cashAtWeekStart = money;
        }
        public long bankruptcyLimit = -200000;

        [System.NonSerialized] public PeriodStats Year = new PeriodStats();
        [System.NonSerialized] public PeriodStats Today = new PeriodStats();
        [System.NonSerialized] public PeriodStats Yesterday;
        public string LastTaxSummary = "";
        public readonly List<string> Ledger = new List<string>();

        bool bankrupt;

        static string Ft(long v) => GameData.Ft(v);

        void Awake() { I = this; }

        void Start()
        {
            GameClock.I.OnNewDay += OnNewDay;
            GameClock.I.OnNewYear += OnNewYear;
        }

        // ---------- Kurye ödemeleri ----------

        public int TimeBonus()
        {
            int h = GameClock.I.Hour;
            if (h >= 22 || h < 6) return nightBonus;
            if ((h >= 11 && h < 14) || (h >= 18 && h < 21)) return rushBonus;
            return 0;
        }

        public int CurrentBonus => Mathf.RoundToInt((WeatherSystem.I.WeatherBonus + TimeBonus()) * bonusMultiplier);

        public int PayFor(VehicleDef v, int tiles) =>
            v.basePay + Mathf.RoundToInt(v.payPerKm * GameData.Km(tiles)) + CurrentBonus;

        /// <summary>Bu kuryeye bu mesafe için şu an ödenecek tutar (boost hariç).</summary>
        public int CourierPay(Courier c, int tiles) => PayFor(c.V, tiles);

        public int ProfitIf(Order o, Courier c) => o.CompanyIncome - CourierPay(c, o.distance);

        /// <summary>
        /// Bu ağırlığı taşıyabilen kuryeler arasında en pahalısının bu mesafe için ücreti.
        /// Ekipte taşıyabilen kurye yoksa, taşıyabilecek en ucuz araç tipine göre hesaplanır.
        /// </summary>
        public int MaxCourierPay(int tiles, float weightKg)
        {
            int max = 0;
            foreach (var c in CourierManager.I.Couriers)
                if (c.CanCarry(weightKg)) max = Mathf.Max(max, CourierPay(c, tiles));
            if (max == 0)
            {
                foreach (var v in GameData.Vehicles)
                    if (weightKg <= v.maxWeightKg) { max = PayFor(v, tiles); break; }
            }
            return max;
        }

        /// <summary>Ekipte bu ağırlığı taşıyabilecek kurye var mı?</summary>
        public static bool AnyoneCanCarry(float weightKg)
        {
            foreach (var c in CourierManager.I.Couriers) if (c.CanCarry(weightKg)) return true;
            return false;
        }

        /// <summary>
        /// Siparişi büyük ihtimalle götürecek kuryenin ücreti: bu ağırlığı taşıyabilen ve şu an müsait
        /// olan en ucuz kurye. Müsait kimse yoksa ekipte taşıyabilen en ucuz kurye; o da yoksa en ucuz uygun araç tipi.
        /// Böylece ekipteki pahalı (arabalı) kurye sadece ağır siparişlerin fiyatını etkiler.
        /// </summary>
        public int ExpectedCourierPay(int tiles, float weightKg)
        {
            int best = int.MaxValue;
            foreach (var c in CourierManager.I.Couriers)
                if (c.CanCarry(weightKg) && c.CanTakeMore) best = Mathf.Min(best, CourierPay(c, tiles));
            if (best == int.MaxValue)
                foreach (var c in CourierManager.I.Couriers)
                    if (c.CanCarry(weightKg)) best = Mathf.Min(best, CourierPay(c, tiles));
            if (best == int.MaxValue)
                foreach (var v in GameData.Vehicles)
                    if (weightKg <= v.maxWeightKg) best = Mathf.Min(best, PayFor(v, tiles));
            return best == int.MaxValue ? 0 : best;
        }

        /// <summary>Teslimat ücretinin dökümü (sipariş kartında gösterilir).</summary>
        public struct FeeQuote
        {
            public int basePart;   // taban ücret
            public int kmPart;     // km başı × mesafe
            public int weather;    // kötü hava ücreti (yoksa 0)
            public int extra;      // kurye ücretini karşılamak için gereken ek (yoksa 0)
            public int Total => basePart + kmPart + weather + extra;
        }

        /// <summary>
        /// Müşteriden alınacak teslimat ücreti:
        /// taban + km ücreti × km; bu, muhtemel kuryenin ücreti + min. kâr − komisyonu karşılamıyorsa fark "ek" olarak eklenir.
        /// Zararına iş kuralı ayrıca atama anında korunur (kâr bırakmayan kuryeye atanmaz).
        /// </summary>
        public FeeQuote QuoteFee(int commissionIncome, int tiles, float weightKg)
        {
            var q = new FeeQuote
            {
                basePart = baseFee,
                kmPart = Mathf.RoundToInt(feePerKm * GameData.Km(tiles)),
                weather = WeatherSystem.I != null ? WeatherSystem.I.CustomerSurcharge : 0
            };
            int required = ExpectedCourierPay(tiles, weightKg) + minProfit - commissionIncome;
            int normal = q.basePart + q.kmPart + q.weather;
            if (required > normal) q.extra = GameData.CeilTo10(required - normal);
            return q;
        }

        public int QuoteDeliveryFee(int commissionIncome, int tiles, float weightKg) =>
            QuoteFee(commissionIncome, tiles, weightKg).Total;

        public void RegisterDelivery(Order o)
        {
            // Kurye ücreti hemen ödenmez, kuryenin hesabına yazılır (kesim gününde ödenir).
            if (o.cashPay)
            {
                // Nakit: müşteri parayı kuryeye verdi. Şirket dükkana sepeti kendi cebinden öder,
                // kuryeden nakdi sonra (keep cash / offload) tahsil eder.
                money += o.CompanyIncome - o.CashAmount;
                if (!o.keptByCourier)
                {
                    o.courier.cashHeld += o.CashAmount;
                    o.courier.cashOrders++;
                    o.courier.cashTakenTotal += o.CashAmount;
                }
                Today.cashOrders++;
            }
            else money += o.CompanyIncome;
            foreach (var s in new[] { Year, Today })
            {
                s.commission += o.commissionIncome;
                s.fees += o.deliveryFee;
                s.courierPay += o.courierPay;
                s.boosts += o.boostPay;
                s.deliveries++;
            }
            o.courier.Earn(o.courierPay + o.boostPay);
            if (o.boostPay > 0) CampaignManager.I.RegisterBoostPaid(o.boostPay);

            int profit = o.Profit;
            string boost = o.boostPay > 0 ? $", boost {Ft(o.boostPay)}" : "";
            Log($"#{o.id} teslim ({GameData.KmText(o.distance)}): gelir {Ft(o.CompanyIncome)}, kurye {Ft(o.courierPay)}{boost}, kâr {Ft(profit)}");
            CheckBankruptcy();
        }

        // ---------- Para hareketleri ----------

        /// <summary>
        /// Serbest kasa: kasadaki paranın kuryelere ait olan kısmı (hesaplarındaki ve yatacak hakedişler) düşülür,
        /// kuryelerin elindeki şirket nakdi eklenir. Yatırım ve harcamalar bu paradan yapılmalı.
        /// </summary>
        public long FreeCash => money - TotalCourierBalances - TotalPendingPayouts + TotalCashHeld;

        /// <summary>İsteğe bağlı harcamalar (geliştirme, ekipman, sözleşme, bölge, reklam): serbest kasa yetmezse yapılmaz.</summary>
        public bool TrySpend(long amount, string label, ExpenseCategory category = ExpenseCategory.Diger)
        {
            if (FreeCash < amount)
            {
                EventManager.I.Notify($"Yapılamadı: {label} ({Ft(amount)}). Serbest kasa {Ft(FreeCash)} — kasadaki paranın " +
                                      $"{Ft(money - FreeCash)}'ı kuryelerin hakedişi, onlara ödenecek.");
                return false;
            }
            Spend(amount, label, category);
            return true;
        }

        public void Spend(long amount, string label, ExpenseCategory category = ExpenseCategory.Diger)
        {
            money -= amount;
            Year.byCategory[(int)category] += amount;
            Today.byCategory[(int)category] += amount;
            Log($"−{Ft(amount)} {label}");
            CheckBankruptcy();
        }

        /// <summary>Haftalık / aylık hedef ödülü. Kârdan ödenir.</summary>
        /// <summary>Hedef ödülü: gider olarak yazılır, kuryenin hesabına eklenir (kesimde ödenir).</summary>
        public void SpendCampaign(long amount, string label)
        {
            Year.rewards += amount;
            Today.rewards += amount;
            Log($"−{Ft(amount)} {label}");
            CheckBankruptcy();
        }

        // ---------- Ekipman depozitosu (gelir sayılmaz, vergilendirilmez) ----------

        public void CollectDeposit(Courier c)
        {
            if (c.depositPaid) return;
            c.depositPaid = true;
            c.depositDay = GameClock.I.Day;
            c.AddInbox($"Ekipman depozitosu {Ft(GameData.DepositAmount)} hesabından kesildi. Ekipmanı iade edince geri ödenir.");
            c.balance -= GameData.DepositAmount;          // kuryenin hesabından kesilir
            Today.depositNet += GameData.DepositAmount;
            depositsHeld += GameData.DepositAmount;
            Log($"+{Ft(GameData.DepositAmount)} ekipman depozitosu: {c.courierName} (iade borcu)");
            EventManager.I.Notify($"{c.courierName} {Ft(GameData.DepositThreshold)} kazanca ulaştı, {Ft(GameData.DepositAmount)} ekipman depozitosu kesildi.");
        }

        public void RefundDeposit(Courier c)
        {
            money -= GameData.DepositAmount;
            Today.depositNet -= GameData.DepositAmount;
            depositsHeld -= GameData.DepositAmount;
            Log($"−{Ft(GameData.DepositAmount)} depozito iadesi: {c.courierName}");
            CheckBankruptcy();
        }

        /// <summary>Ekipmanı iade etmeyen kuryenin depozitosu şirkete gelir olarak kalır.</summary>
        public void KeepDeposit(Courier c)
        {
            depositsHeld -= GameData.DepositAmount;
            Year.otherIncome += GameData.DepositAmount;
            Today.otherIncome += GameData.DepositAmount;
            Log($"İade edilmeyen ekipman: {c.courierName} depozitosu gelire geçti ({Ft(GameData.DepositAmount)})");
        }

        // ---------- Kurye hesapları: kesim, erken ödeme, nakit ----------

        public class Payout
        {
            public Courier courier;
            public string courierName;
            public long amount;
            public int payDay;
            public bool early;
        }

        public readonly List<Payout> Payouts = new List<Payout>();

        public long TotalCourierBalances
        {
            get { long s = 0; foreach (var c in CourierManager.I.Couriers) s += c.balance; return s; }
        }
        public long TotalPendingPayouts
        {
            get { long s = 0; foreach (var p in Payouts) s += p.amount; return s; }
        }
        public long TotalCashHeld
        {
            get { long s = 0; foreach (var c in CourierManager.I.Couriers) s += c.cashHeld; return s; }
        }

        /// <summary>Sıradaki hesap kesimi (15'i veya ayın son günü).</summary>
        public string NextCutText
        {
            get
            {
                var clock = GameClock.I;
                int d = clock.Day;
                while (clock.DayOfMonthOf(d) != 15 && !clock.IsLastDayOfMonth(d)) d++;
                return clock.DateOf(d);
            }
        }

        void SchedulePayout(Courier c, long amount, bool early)
        {
            var clock = GameClock.I;
            int days = early ? Random.Range(2, 4) : Random.Range(1, 3);
            Payouts.Add(new Payout { courier = c, courierName = c.courierName, amount = amount, early = early,
                                     payDay = clock.AddBusinessDays(clock.Day, days) });
            c.pendingPayout += amount;
        }

        /// <summary>Ayın 15'i ve son günü gece: hesaplar kesilir, 1–2 iş günü içinde yatar.</summary>
        void CutPayPeriod()
        {
            long total = 0;
            int n = 0;
            foreach (var c in CourierManager.I.Couriers)
            {
                if (c.balance <= 0) continue;
                SchedulePayout(c, c.balance, false);
                total += c.balance;
                c.balance = 0;
                n++;
            }
            if (n > 0)
            {
                Log($"Hesap kesimi: {n} kurye, {Ft(total)} (1–2 iş günü içinde ödenecek)");
                EventManager.I.Notify($"Kurye hesapları kesildi: {n} kurye, toplam {Ft(total)}. 1–2 iş günü içinde hesaplarına yatacak.");
            }
        }

        public void RequestEarlyPayout(Courier c)
        {
            long amount = (long)(c.balance * Courier.EarlyPayoutShare) / 10 * 10;
            if (amount <= 0) return;
            c.balance -= amount;
            c.earlyPayouts++;
            c.AddInbox($"Erken ödeme talebin alındı: {Ft(amount)} (2–3 iş günü).");
            c.earlyPayoutTotal += amount;
            SchedulePayout(c, amount, true);
            EventManager.I.Notify($"{c.ShortName} erken ödeme istedi: {Ft(amount)} (hesabının %80'i, 2–3 iş günü içinde).");
        }

        void ProcessPayouts()
        {
            int today = GameClock.I.Day;
            long total = 0;
            int n = 0;
            for (int i = Payouts.Count - 1; i >= 0; i--)
            {
                var p = Payouts[i];
                if (p.payDay > today) continue;
                PayOut(p);
                total += p.amount;
                n++;
                Payouts.RemoveAt(i);
            }
            if (n > 0) EventManager.I.Notify($"Kuryelerin hesabına {Ft(total)} yatırıldı ({n} ödeme).");
        }

        void PayOut(Payout p)
        {
            money -= p.amount;
            Year.payoutsPaid += p.amount;
            Today.payoutsPaid += p.amount;
            if (p.courier != null) p.courier.OnPaid(p.amount);
            Log($"−{Ft(p.amount)} kurye ödemesi{(p.early ? " (erken)" : "")}: {p.courierName}");
            CheckBankruptcy();
        }

        /// <summary>Keep cash: kuryedeki nakit, hesabındaki parayla mahsup edilir (kasa değişmez).</summary>
        public void KeepCash(Courier c, long amount)
        {
            c.balance -= amount;
            c.cashHeld -= amount;
            c.keepCashTotal += amount;
            Log($"Keep cash: {c.courierName} {Ft(amount)} nakdi hesabından mahsup etti");
        }

        /// <summary>Offload: kurye nakdi kendi kartıyla şirkete öder.</summary>
        public void Offload(Courier c, long amount)
        {
            if (amount <= 0) return;
            c.cashHeld -= amount;
            c.offloadTotal += amount;
            money += amount;
            Year.cashCollected += amount;
            Today.cashCollected += amount;
            Log($"+{Ft(amount)} offload: {c.courierName} nakit borcunu ödedi");
        }

        /// <summary>Ayrılan kurye: nakit mahsup/tahsil, bekleyen ve kalan hakedişler hemen ödenir.</summary>
        public void FinalSettlement(Courier c)
        {
            long net = System.Math.Min(c.cashHeld, System.Math.Max(0L, c.balance));
            if (net > 0) KeepCash(c, net);
            if (c.cashHeld > 0) Offload(c, c.cashHeld);
            for (int i = Payouts.Count - 1; i >= 0; i--)
                if (Payouts[i].courier == c) { PayOut(Payouts[i]); Payouts.RemoveAt(i); }
            if (c.balance > 0)
            {
                PayOut(new Payout { courier = c, courierName = c.courierName, amount = c.balance });
                c.balance = 0;
            }
        }

        // ---------- Kayıt ----------
        // Sadece oyunun durumu ve oyuncunun Ayarlar'dan değiştirdikleri kaydedilir; denge sabitleri
        // (kira, kredi tutarı, iflas sınırı...) koddaki güncel değerden gelir.

        public EconomySave CaptureSave(List<Courier> couriers)
        {
            var s = new EconomySave
            {
                money = money, taxDebt = taxDebt, depositsHeld = depositsHeld,
                baseFee = baseFee, feePerKm = feePerKm, minProfit = minProfit, minAssignProfit = minAssignProfit,
                bonusMultiplier = bonusMultiplier, showDailyReport = showDailyReport, showWeeklyReport = showWeeklyReport,
                lastDailyReport = LastDailyReport, lastDailyReportTitle = LastDailyReportTitle,
                lastWeeklyReport = LastWeeklyReport, lastWeeklyReportTitle = LastWeeklyReportTitle, lastTaxSummary = LastTaxSummary,
                year = SaveSystem.ToSave(Year), today = SaveSystem.ToSave(Today), week = SaveSystem.ToSave(week),
                weekStartDay = weekStartDay, weekBatched = weekBatched, weekCancelled = weekCancelled, weekLost = weekLost,
                weekHeavyLost = weekHeavyLost, weekCancelledRejected = weekCancelledRejected, weekBusyLost = weekBusyLost,
                weekCancelByHour = (int[])weekCancelByHour.Clone(), repAtWeekStart = repAtWeekStart,
                cashAtWeekStart = cashAtWeekStart, lastWeekNet = lastWeekNet, cashAtLastReport = cashAtLastReport,
                nextSurpriseDay = nextSurpriseDay, localTaxPaidThisYear = localTaxPaidThisYear,
                bankLoanRemaining = bankLoanRemaining, bankLoanDaily = bankLoanDaily, emergencyLoanUsed = emergencyLoanUsed,
                crisisWarned = crisisWarned, bankrupt = bankrupt, daysUnderLimit = daysUnderLimit,
                insurancePlan = insurancePlan, insurancePlanChangeDay = insurancePlanChangeDay, insuranceIncomeTotal = insuranceIncomeTotal
            };
            foreach (var w in weekDays)
            {
                s.weekDayDates.Add(w.date);
                s.weekDayDeliveries.Add(w.deliveries);
                s.weekDayNets.Add(w.net);
            }
            foreach (var p in Payouts)
                s.payouts.Add(new PayoutSave
                {
                    courierIndex = p.courier != null ? couriers.IndexOf(p.courier) : -1,
                    courierName = p.courierName, amount = p.amount, payDay = p.payDay, early = p.early
                });
            for (int i = Mathf.Max(0, Ledger.Count - 100); i < Ledger.Count; i++) s.ledger.Add(Ledger[i]);
            return s;
        }

        /// <summary>Kuryeler geri konduktan sonra çağrılır (bekleyen ödemeler kuryelere bağlanır).</summary>
        public void RestoreSave(EconomySave s, List<Courier> couriers)
        {
            money = s.money; taxDebt = s.taxDebt; depositsHeld = s.depositsHeld;
            baseFee = s.baseFee; feePerKm = s.feePerKm; minProfit = s.minProfit; minAssignProfit = s.minAssignProfit;
            bonusMultiplier = s.bonusMultiplier; showDailyReport = s.showDailyReport; showWeeklyReport = s.showWeeklyReport;
            LastDailyReport = s.lastDailyReport ?? ""; LastDailyReportTitle = s.lastDailyReportTitle ?? "";
            LastWeeklyReport = s.lastWeeklyReport ?? ""; LastWeeklyReportTitle = s.lastWeeklyReportTitle ?? "";
            LastTaxSummary = s.lastTaxSummary ?? "";
            Year = SaveSystem.FromSave(s.year);
            Today = SaveSystem.FromSave(s.today);
            week = SaveSystem.FromSave(s.week);

            weekStartDay = s.weekStartDay; weekBatched = s.weekBatched; weekCancelled = s.weekCancelled; weekLost = s.weekLost;
            weekHeavyLost = s.weekHeavyLost; weekCancelledRejected = s.weekCancelledRejected; weekBusyLost = s.weekBusyLost;
            if (s.weekCancelByHour != null)
                System.Array.Copy(s.weekCancelByHour, weekCancelByHour, Mathf.Min(weekCancelByHour.Length, s.weekCancelByHour.Length));
            repAtWeekStart = s.repAtWeekStart; cashAtWeekStart = s.cashAtWeekStart;
            lastWeekNet = s.lastWeekNet; cashAtLastReport = s.cashAtLastReport;
            weekDays.Clear();
            int n = Mathf.Min(s.weekDayDates.Count, Mathf.Min(s.weekDayDeliveries.Count, s.weekDayNets.Count));
            for (int i = 0; i < n; i++) weekDays.Add((s.weekDayDates[i], s.weekDayDeliveries[i], s.weekDayNets[i]));

            Payouts.Clear();
            foreach (var p in s.payouts)
            {
                var c = p.courierIndex >= 0 && p.courierIndex < couriers.Count ? couriers[p.courierIndex] : null;
                Payouts.Add(new Payout { courier = c, courierName = p.courierName, amount = p.amount, payDay = p.payDay, early = p.early });
            }

            nextSurpriseDay = s.nextSurpriseDay; localTaxPaidThisYear = s.localTaxPaidThisYear;
            bankLoanRemaining = s.bankLoanRemaining; bankLoanDaily = s.bankLoanDaily; emergencyLoanUsed = s.emergencyLoanUsed;
            crisisWarned = s.crisisWarned; bankrupt = s.bankrupt; daysUnderLimit = s.daysUnderLimit;
            insurancePlan = s.insurancePlan; insurancePlanChangeDay = s.insurancePlanChangeDay; insuranceIncomeTotal = s.insuranceIncomeTotal;
            Ledger.Clear();
            if (s.ledger != null) Ledger.AddRange(s.ledger);
        }

        /// <summary>
        /// Kuryenin hesabından kesinti (ör. ekipman payı). Kasaya para girmez ama kuryeye olan borç azalır,
        /// bu yüzden serbest kasa artar; kurye kesintisi olarak gelire yazılır.
        /// </summary>
        public void ChargeCourier(Courier c, long amount, string label)
        {
            if (amount <= 0) return;
            c.balance -= amount;
            Year.otherIncome += amount;
            Today.otherIncome += amount;
            Log($"+{Ft(amount)} {label} (kurye hesabından kesildi)");
        }

        // ---------- Sigorta ortaklığı ----------
        // Kuryeler sigortayı isterlerse şirketin anlaşmalı olduğu sigortacı üzerinden yaptırır (zorunlu değil).
        // Şirket her haftalık primden komisyon alır. Plan seçimi: düşük prim → daha çok kurye alır ama komisyon az.

        public class InsurancePlan
        {
            public string name;
            public long premium;          // kuryenin haftalık primi (Ft)
            public float commission;      // şirketin primden payı
            public float buyMultiplier;   // kuryelerin sigorta yaptırma isteği
            public int accidentDaysSaved; // kazada daha kısa iyileşme (gün)
            public string desc;
        }

        public static readonly InsurancePlan[] InsurancePlans =
        {
            new InsurancePlan { name = "Anlaşma yok", premium = 2500, commission = 0f, buyMultiplier = 1f,
                desc = "Kuryeler isterse sigortayı kendileri dışarıdan yaptırır. Şirket bundan kazanmaz." },
            new InsurancePlan { name = "Kalkan Sigorta · Ekonomik", premium = 1800, commission = 0.10f, buyMultiplier = 1.5f,
                desc = "Ucuz prim: daha çok kurye yaptırır, komisyon düşük." },
            new InsurancePlan { name = "Kalkan Sigorta · Standart", premium = 2500, commission = 0.18f, buyMultiplier = 1.1f,
                desc = "Dengeli prim ve komisyon." },
            new InsurancePlan { name = "Kalkan Sigorta · Plus", premium = 3400, commission = 0.28f, buyMultiplier = 0.65f, accidentDaysSaved = 1,
                desc = "Pahalı ama kapsamlı: kazada kurye 1 gün erken döner. Daha az kurye yaptırır, komisyon yüksek." },
        };

        public int insurancePlan;
        public int insurancePlanChangeDay = -999;
        public long insuranceIncomeToday, insuranceIncomeTotal;
        public InsurancePlan Plan => InsurancePlans[Mathf.Clamp(insurancePlan, 0, InsurancePlans.Length - 1)];
        public bool HasInsurancePartner => insurancePlan > 0;
        public const int InsurancePlanCooldownDays = 30;

        public bool CanChangeInsurancePlan => GameClock.I.Day - insurancePlanChangeDay >= InsurancePlanCooldownDays || insurancePlan == 0;

        public void SetInsurancePlan(int plan)
        {
            if (plan == insurancePlan || !CanChangeInsurancePlan) return;
            insurancePlan = Mathf.Clamp(plan, 0, InsurancePlans.Length - 1);
            insurancePlanChangeDay = GameClock.I.Day;
            int moved = 0;
            foreach (var c in CourierManager.I.Couriers)
            {
                if (!c.insuredViaCompany) continue;
                if (insurancePlan == 0) c.insuredViaCompany = false; // poliçe kuryenin üstünde kalır, artık dışarıdan
                else moved++;
            }
            EventManager.I.Notify(insurancePlan == 0
                ? "Sigorta anlaşması bitti. Kuryeler sigortalarını kendi sürdürecek."
                : $"Sigorta anlaşması: {Plan.name} (haftalık prim {Ft(Plan.premium)}, şirket payı %{Plan.commission * 100f:0})." +
                  (moved > 0 ? $" {moved} kuryenin poliçesi yeni plana geçti." : ""));
        }

        /// <summary>Kurye haftalık primi şirket üzerinden ödedi: komisyon şirkete gelir yazılır.</summary>
        public void CollectInsuranceCommission(Courier c, long premium)
        {
            long amount = GameData.Round10(premium * Plan.commission);
            if (amount <= 0) return;
            money += amount;
            Year.otherIncome += amount;
            Today.otherIncome += amount;
            insuranceIncomeToday += amount;
            insuranceIncomeTotal += amount;
            Log($"+{Ft(amount)} sigorta komisyonu: {c.courierName}");
        }

        public int InsuredViaCompanyCount
        {
            get { int n = 0; foreach (var c in CourierManager.I.Couriers) if (c.insured && c.insuredViaCompany) n++; return n; }
        }

        public void AddIncome(long amount, string label)
        {
            money += amount;
            Year.otherIncome += amount;
            Today.otherIncome += amount;
            Log($"+{Ft(amount)} {label}");
        }

        /// <summary>Borç anapara ödemesi: gider değildir, vergiden düşülmez.</summary>
        public void PayLoan(long amount, string label)
        {
            money -= amount;
            Year.loanPaid += amount;
            Today.loanPaid += amount;
            Log($"−{Ft(amount)} {label}");
        }

        /// <summary>Ceza (vergiden düşülmez).</summary>
        public void SpendPenalty(long amount, string label)
        {
            money -= amount;
            Year.penalties += amount;
            Today.penalties += amount;
            Log($"−{Ft(amount)} {label} (ceza)");
            CheckBankruptcy();
        }

        void Log(string s)
        {
            var c = GameClock.I;
            Ledger.Insert(0, $"{c.DayOfMonth} {c.MonthName} {c.Hour:00}:{c.Minute:00}  {s}");
            if (Ledger.Count > 60) Ledger.RemoveAt(Ledger.Count - 1);
        }

        // ---------- Gün / yıl ----------

        void OnNewDay()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;

            // Kuryeler kendi araçlarıyla, serbest çalışır: şirketin günlük gideri ofis kirası + platform sunucu ücreti
            DailyCrisisCheck();
            // Ofis kirası, platform ücreti, yazılım abonelikleri, tesis giderleri ve reklam bütçesi (geliştirmelere göre)
            if (UpgradeManager.I != null) UpgradeManager.I.ChargeDaily();
            else Spend(officeRentPerDay, "Ofis kirası", ExpenseCategory.Kira);

            if (taxDebt > 0)
            {
                long interest = (long)Mathf.Ceil(taxDebt * 0.02f);
                taxDebt += interest;
                Year.penalties += interest;
                CompanyProfile.AddReputation(-0.03f);
                EventManager.I.Notify($"Ödenmemiş vergi borcu büyüyor: {Ft(taxDebt)} (itibar düştü)");
            }

            BuildDailyReport(Today);
            insuranceIncomeToday = 0;
            if (StatsHistory.I != null) StatsHistory.I.Record(Today);   // grafikler ve CSV denge kaydı
            AccumulateWeek(Today);
            if (GameClock.I.DayOfWeek == 0) BuildWeeklyReport(); // Pazar bitti, yeni hafta başlıyor
            Yesterday = Today;
            Today = new PeriodStats();

            // Kurye hesapları: dün 15'i ya da ayın son günüyse kesim; vadesi gelen ödemeler yatar
            var clk = GameClock.I;
            if (clk.DayOfMonthOf(clk.Day - 1) == 15 || clk.IsLastDayOfMonth(clk.Day - 1)) CutPayPeriod();
            ProcessPayouts();

            if (nextSurpriseDay == 0) ScheduleSurprise();
            else if (GameClock.I.CompanyDay >= nextSurpriseDay)
            {
                RollSurpriseTax();
                ScheduleSurprise();
            }
        }

        // ---------- Gün sonu raporu ----------

        static string Signed(long v) => (v >= 0 ? "+" : "−") + GameData.Ft(System.Math.Abs(v));

        /// <summary>Biten günün gelir, gider, kâr ve kasa özetini hazırlar ve gösterir.</summary>
        void BuildDailyReport(PeriodStats s)
        {
            var clock = GameClock.I;
            var om = OrderManager.I;
            var sb = new System.Text.StringBuilder();

            // Teslimatlar
            sb.Append($"TESLİMAT: {s.deliveries} paket");
            if (om != null)
            {
                var extra = new List<string>();
                if (om.batchedToday > 0) extra.Add($"{om.batchedToday} toplu");
                if (om.cancelledToday > 0)
                {
                    int none = om.cancelledToday - om.cancelledRejectedToday;
                    var parts = new List<string>();
                    if (none > 0) parts.Add($"{none} kurye yok/hepsi meşgul");
                    if (om.cancelledRejectedToday > 0) parts.Add($"{om.cancelledRejectedToday} kuryeler reddetti");
                    extra.Add($"{om.cancelledToday} iptal ({string.Join(", ", parts)})");
                }
                if (om.lostToday > 0) extra.Add($"{om.lostToday} müşteri ücret pahalı diye vazgeçti");
                if (om.busyLostToday > 0) extra.Add($"{om.busyLostToday} müşteri yoğunluk uyarısını görüp sipariş vermedi");
                if (om.heavyLostToday > 0) extra.Add($"{om.heavyLostToday} taşıyacak kurye yoktu");
                if (extra.Count > 0) sb.Append(" · " + string.Join(" · ", extra));
            }
            sb.Append('\n');
            if (om != null)
            {
                string hint = CancelHint(om.cancelledByHour, clock.DayOfWeekOf(clock.Day - 1) >= 5);
                if (hint != null) sb.Append(hint + "\n");
                if (om.reputationAtDayStart >= 0f)
                {
                    float dr = CompanyProfile.Reputation - om.reputationAtDayStart;
                    sb.Append($"İtibar: {CompanyProfile.Reputation:0.00}/5 (dünden {(dr >= 0 ? "+" : "−")}{Mathf.Abs(dr):0.00}) · itibar düştükçe sipariş azalır\n");
                }
            }

            // Gelir
            sb.Append($"\nGELİR: {Ft(s.Income)}\n");
            sb.Append($"   Dükkan komisyonları: {Ft(s.commission)}\n");
            sb.Append($"   Müşteri teslimat ücretleri: {Ft(s.fees)}\n");
            if (s.otherIncome > 0) sb.Append($"   Diğer (kesinti, sigorta komisyonu, depozito, vergi iadesi): {Ft(s.otherIncome)}\n");
            if (insuranceIncomeToday > 0) sb.Append($"      bunun {Ft(insuranceIncomeToday)}'ı sigorta komisyonu ({InsuredViaCompanyCount} kurye {Plan.name} ile sigortalı)\n");

            // Gider
            sb.Append($"\nGİDER: {Ft(s.Expenses)}\n");
            sb.Append($"   Kurye hakedişleri (hesaplarına yazılan): {Ft(s.courierPay)}\n");
            if (s.boosts > 0) sb.Append($"   Boost ödemeleri: {Ft(s.boosts)}\n");
            if (s.rewards > 0) sb.Append($"   Hedef ödülleri: {Ft(s.rewards)}\n");
            for (int i = 0; i < s.byCategory.Length; i++)
                if (s.byCategory[i] > 0) sb.Append($"   {PeriodStats.CategoryNames[i]}: {Ft(s.byCategory[i])}\n");
            if (s.penalties > 0) sb.Append($"   Cezalar: {Ft(s.penalties)}\n");

            // Kâr
            sb.Append($"\nTeslimat kârı: {Signed(s.DeliveryProfit)}");
            if (s.deliveries > 0) sb.Append($" (paket başı {Ft(s.DeliveryProfit / s.deliveries)})");
            sb.Append($"\nNET KÂR: {Signed(s.Profit - s.penalties)}\n");

            // Kasa hareketi
            if (s.loanPaid > 0) sb.Append($"\nAile borcuna ödenen: {Ft(s.loanPaid)}");
            if (s.payoutsPaid > 0) sb.Append($"\nKuryelerin hesabına yatan: {Ft(s.payoutsPaid)}");
            if (s.cashOrders > 0) sb.Append($"\nNakit siparişler: {s.cashOrders}" + (s.cashCollected > 0 ? $" · kuryelerden tahsil edilen nakit {Ft(s.cashCollected)}" : ""));
            if (s.depositNet != 0) sb.Append($"\nEkipman depozitosu (kuryeden kesilen − iade): {Signed(s.depositNet)}");
            sb.Append($"\nKuryelere borç: hesaplarda {Ft(TotalCourierBalances)} + yatacak {Ft(TotalPendingPayouts)} · kuryelerdeki nakit {Ft(TotalCashHeld)}");
            sb.Append($"\nSerbest kasa (kurye hakedişleri düşülünce): {Ft(FreeCash)}" + (FreeCash < 0 ? " — DİKKAT: kuryelere ödeme yapamayabilirsin!" : ""));
            if (bankLoanRemaining > 0) sb.Append($"\nAcil kredi borcu: {Ft(bankLoanRemaining)} (günde {Ft(bankLoanDaily)})");
            if (cashAtLastReport != long.MinValue)
                sb.Append($"\nKasa: {Ft(money)} (dünden {Signed(money - cashAtLastReport)})");
            else
                sb.Append($"\nKasa: {Ft(money)}");
            cashAtLastReport = money;

            // Günün kuryesi
            var cm = CourierManager.I;
            if (cm != null)
            {
                Courier best = null;
                foreach (var c in cm.Couriers)
                    if (c.deliveriesToday > 0 && (best == null || c.deliveriesToday > best.deliveriesToday)) best = c;
                if (best != null)
                    sb.Append($"\n\nGünün kuryesi: {best.courierName} — {best.deliveriesToday} paket, {Ft(best.earnedToday)} kazandı" +
                              (best.tipsToday > 0 ? $" ({Ft(best.tipsToday)} bahşiş)" : ""));
            }

            // Operasyon sorumlusunun bugün yaptıkları (çağrılınca sayaçları sıfırlanır)
            string ops = OperationsManager.I != null ? OperationsManager.I.TakeDayReport() : null;
            if (ops != null) sb.Append("\n" + ops);

            // Aile borcu hatırlatması
            var loan = FamilyLoan.I;
            if (loan != null && !loan.Finished && !loan.Failed && loan.NextDue != null)
                sb.Append($"\nSıradaki taksit: {Ft(loan.NextDue.Remaining)} → {loan.NextDue.DueText} ({loan.DaysUntil(loan.NextDue)} gün)");

            LastDailyReportTitle = $"Gün sonu raporu — {clock.DateOf(clock.Day - 1)}";
            LastDailyReport = sb.ToString();

            if (showDailyReport)
                EventManager.I.Push(new GameEvent(LastDailyReportTitle, LastDailyReport)
                    .Add("Tamam", null)
                    .Add("Gün sonu raporlarını kapat (Ayarlar'dan açılabilir)", () => showDailyReport = false));
        }

        /// <summary>İptallerin en yoğun olduğu 3 saatlik dilimi ve o saatlere uygun kurye tipini söyler.</summary>
        static string CancelHint(int[] byHour, bool weekend)
        {
            int total = 0; foreach (var v in byHour) total += v;
            if (total < 3) return null;
            int best = 0, bestSum = -1;
            for (int h = 0; h < 24; h++)
            {
                int sum = byHour[h] + byHour[(h + 1) % 24] + byHour[(h + 2) % 24];
                if (sum > bestSum) { bestSum = sum; best = h; }
            }
            int mid = (best + 1) % 24;
            string who;
            if (weekend) who = "hafta sonu kuryesi veya öğrenci";
            else if (mid >= 7 && mid < 14) who = "tam zamanlı kurye, emekli veya sabah vardiyalı işçi";
            else if (mid >= 14 && mid < 17) who = "tam zamanlı kurye veya akşam vardiyalı işçi";
            else if (mid >= 17 && mid < 23) who = "öğrenci veya ofis çalışanı (akşamları çalışırlar)";
            else who = "gece çalışan kurye (gece talebi düşük, ücretsiz bırakılabilir)";
            return $"İptallerin çoğu {best:00}:00–{(best + 3) % 24:00}:00 arası ({bestSum}/{total}) → bu saatler için {who} işe al.";
        }

        // ---------- Haftalık rapor ----------

        void AccumulateWeek(PeriodStats day)
        {
            var clock = GameClock.I;
            var om = OrderManager.I;
            if (weekStartDay < 0) weekStartDay = clock.Day - 1;
            if (cashAtWeekStart == long.MinValue) cashAtWeekStart = money;
            week.Add(day);
            if (om != null)
            {
                weekBatched += om.batchedToday;
                weekCancelled += om.cancelledToday;
                weekCancelledRejected += om.cancelledRejectedToday;
                weekBusyLost += om.busyLostToday;
                for (int h = 0; h < 24; h++) weekCancelByHour[h] += om.cancelledByHour[h];
                if (repAtWeekStart < 0f) repAtWeekStart = om.reputationAtDayStart >= 0f ? om.reputationAtDayStart : CompanyProfile.Reputation;
                weekLost += om.lostToday;
                weekHeavyLost += om.heavyLostToday;
            }
            weekDays.Add((clock.DateOf(clock.Day - 1), day.deliveries, day.Profit - day.penalties));
        }

        /// <summary>Biten haftanın (Pazartesi–Pazar) özeti.</summary>
        void BuildWeeklyReport()
        {
            var clock = GameClock.I;
            var s = week;
            var sb = new System.Text.StringBuilder();
            int days = Mathf.Max(1, weekDays.Count);
            long net = s.Profit - s.penalties;

            sb.Append($"TESLİMAT: {s.deliveries} paket (günde ortalama {s.deliveries / days})");
            var extra = new List<string>();
            if (weekBatched > 0) extra.Add($"{weekBatched} toplu");
            if (weekCancelled > 0)
                extra.Add($"{weekCancelled} iptal" + (weekCancelledRejected > 0 ? $" (bunun {weekCancelledRejected}'i kuryeler reddettiği için)" : ""));
            if (weekLost > 0) extra.Add($"{weekLost} müşteri vazgeçti");
            if (weekBusyLost > 0) extra.Add($"{weekBusyLost} müşteri yoğunluk yüzünden sipariş vermedi");
            if (weekHeavyLost > 0) extra.Add($"{weekHeavyLost} taşıyacak kurye yoktu");
            if (extra.Count > 0) sb.Append("\n   " + string.Join(" · ", extra));
            string whint = CancelHint(weekCancelByHour, false);
            if (whint != null) sb.Append("\n   " + whint.Replace("İptallerin çoğu", "Hafta boyunca iptallerin çoğu"));
            if (repAtWeekStart >= 0f)
            {
                float dr = CompanyProfile.Reputation - repAtWeekStart;
                sb.Append($"\n   İtibar: {CompanyProfile.Reputation:0.00}/5 (hafta başından {(dr >= 0 ? "+" : "−")}{Mathf.Abs(dr):0.00})");
            }

            sb.Append($"\n\nGELİR: {Ft(s.Income)}  (komisyon {Ft(s.commission)}, teslimat ücreti {Ft(s.fees)}" +
                      (s.otherIncome > 0 ? $", diğer {Ft(s.otherIncome)}" : "") + ")");

            sb.Append($"\nGİDER: {Ft(s.Expenses)}\n");
            sb.Append($"   Kurye hakedişleri (hesaplarına yazılan): {Ft(s.courierPay)}\n");
            if (s.boosts > 0) sb.Append($"   Boost ödemeleri: {Ft(s.boosts)}\n");
            if (s.rewards > 0) sb.Append($"   Hedef ödülleri: {Ft(s.rewards)}\n");
            for (int i = 0; i < s.byCategory.Length; i++)
                if (s.byCategory[i] > 0) sb.Append($"   {PeriodStats.CategoryNames[i]}: {Ft(s.byCategory[i])}\n");
            if (s.penalties > 0) sb.Append($"   Cezalar: {Ft(s.penalties)}\n");

            sb.Append($"\nTeslimat kârı: {Signed(s.DeliveryProfit)}");
            if (s.deliveries > 0) sb.Append($" (paket başı {Ft(s.DeliveryProfit / s.deliveries)})");
            sb.Append($"\nNET KÂR: {Signed(net)}");
            if (lastWeekNet != long.MinValue) sb.Append($" (geçen hafta {Signed(lastWeekNet)})");

            // En iyi / en kötü gün
            if (weekDays.Count > 1)
            {
                var best = weekDays[0]; var worst = weekDays[0];
                foreach (var d in weekDays) { if (d.net > best.net) best = d; if (d.net < worst.net) worst = d; }
                sb.Append($"\nEn iyi gün: {best.date} ({Signed(best.net)}, {best.deliveries} paket)");
                sb.Append($"\nEn kötü gün: {worst.date} ({Signed(worst.net)}, {worst.deliveries} paket)");
            }

            if (s.loanPaid > 0) sb.Append($"\n\nAile borcuna ödenen: {Ft(s.loanPaid)}");
            if (s.payoutsPaid > 0) sb.Append($"\nKuryelerin hesabına yatan: {Ft(s.payoutsPaid)}");
            if (s.cashOrders > 0) sb.Append($"\nNakit siparişler: {s.cashOrders} · tahsil edilen nakit {Ft(s.cashCollected)}");
            if (s.depositNet != 0) sb.Append($"\nEkipman depozitosu (kuryeden kesilen − iade): {Signed(s.depositNet)}");
            sb.Append($"\nKasa: {Ft(money)}");
            if (cashAtWeekStart != long.MinValue) sb.Append($" (hafta başından {Signed(money - cashAtWeekStart)})");

            // Haftanın kuryeleri
            var cm = CourierManager.I;
            if (cm != null && cm.Couriers.Count > 0)
            {
                var list = new List<Courier>(cm.Couriers);
                list.Sort((a, b) => b.weekDeliveries.CompareTo(a.weekDeliveries));
                sb.Append("\n\nHaftanın kuryeleri:");
                for (int i = 0; i < Mathf.Min(3, list.Count); i++)
                {
                    var c = list[i];
                    if (c.weekDeliveries == 0) break;
                    sb.Append($"\n   {i + 1}. {c.courierName} — {c.weekDeliveries} paket, {Ft(c.earnedWeek)} kazandı");
                }
                int idle = 0;
                foreach (var c in cm.Couriers) if (c.weekDeliveries == 0) idle++;
                if (idle > 0) sb.Append($"\n   Bu hafta hiç teslimat yapmayan kurye: {idle}");
            }

            var loan = FamilyLoan.I;
            if (RivalCompany.I != null) sb.Append("\n\n" + RivalCompany.I.ReportLine);
            if (loan != null && !loan.Finished && !loan.Failed && loan.NextDue != null)
                sb.Append($"\n\nAile borcu: kalan {Ft(loan.Remaining)} · sıradaki taksit {Ft(loan.NextDue.Remaining)} → {loan.NextDue.DueText} ({loan.DaysUntil(loan.NextDue)} gün)");

            string first = weekDays.Count > 0 ? weekDays[0].date : clock.DateOf(weekStartDay);
            string last = weekDays.Count > 0 ? weekDays[weekDays.Count - 1].date : clock.DateOf(clock.Day - 1);
            LastWeeklyReportTitle = $"Haftalık rapor — {first} / {last}";
            LastWeeklyReport = sb.ToString();

            if (showWeeklyReport)
                EventManager.I.Push(new GameEvent(LastWeeklyReportTitle, LastWeeklyReport)
                    .Add("Tamam", null)
                    .Add("Haftalık raporları kapat (Ayarlar'dan açılabilir)", () => showWeeklyReport = false));

            // Yeni hafta için sıfırla
            lastWeekNet = net;
            week = new PeriodStats();
            weekStartDay = clock.Day;
            weekBatched = weekCancelled = weekLost = weekHeavyLost = weekCancelledRejected = weekBusyLost = 0;
            System.Array.Clear(weekCancelByHour, 0, 24);
            repAtWeekStart = CompanyProfile.Reputation;
            cashAtWeekStart = money;
            weekDays.Clear();
        }

        // ---------- Yıl içindeki sürpriz vergiler ve harçlar ----------

        int nextSurpriseDay;
        long localTaxPaidThisYear;

        void ScheduleSurprise()
        {
            nextSurpriseDay = GameClock.I.CompanyDay + Random.Range(surpriseMinDays, surpriseMaxDays + 1);
        }

        static long Round1000(float v) => (long)(Mathf.Round(v / 1000f) * 1000f);

        /// <summary>Ertelenen vergi: %10 ceza eklenir, borca yazılır (her gün %2 faiz).</summary>
        void Postpone(long amount)
        {
            long withPenalty = Round1000(amount * 1.1f);
            Year.penalties += withPenalty - amount;
            taxDebt += withPenalty;
        }

        void RollSurpriseTax()
        {
            int couriers = Mathf.Max(1, CourierManager.I.Couriers.Count);
            int roll = Random.Range(0, 100);

            if (roll < 22)
            {
                // Yerel işletme vergisi: yıl başından beri gelirin %2'si (daha önce ödenen düşülür)
                long due = Round1000(Year.Income * 0.02f) - localTaxPaidThisYear;
                if (due < 3000) due = 3000;
                EventManager.I.Push(new GameEvent("Yerel işletme vergisi",
                        $"Belediye, yıl başından beri elde ettiğin gelirin %2'si oranında yerel işletme vergisi tahakkuk ettirdi: {Ft(due)}.")
                    .Add($"Öde (−{Ft(due)})", () => { localTaxPaidThisYear += due; PayFee(due, "Yerel işletme vergisi"); })
                    .Add("Ertele (+%10 ceza, borca yazılır)", () => { localTaxPaidThisYear += due; Postpone(due); }));
            }
            else if (roll < 40)
            {
                // KDV/ÁFA farkı: itiraz edilebilir
                long due = Round1000(Mathf.Max(4000f, Year.fees * Random.Range(0.01f, 0.03f)));
                EventManager.I.Push(new GameEvent("KDV (ÁFA) farkı",
                        $"Vergi dairesi teslimat ücretlerindeki KDV hesabında fark buldu ve {Ft(due)} ek ödeme istiyor.")
                    .Add($"Öde (−{Ft(due)})", () => PayFee(due, "KDV farkı"))
                    .Add("İtiraz et (%50 iptal, %50 ise 1,5 katı ödersin)", () =>
                    {
                        if (Random.value < 0.5f)
                            EventManager.I.Push(new GameEvent("İtiraz kabul edildi", "Vergi dairesi hatalı hesap yaptığını kabul etti. Ödeme iptal."));
                        else
                        {
                            PayFee(due, "KDV farkı");
                            SpendPenalty(due / 2, "KDV itirazı reddedildi");
                            EventManager.I.Push(new GameEvent("İtiraz reddedildi", $"İtirazın reddedildi. {Ft(due)} + {Ft(due / 2)} ceza ödendi."));
                        }
                    }));
            }
            else if (roll < 55)
            {
                // Vergi denetimi
                long fine = Round1000(Mathf.Max(10000f, Mathf.Max(0f, Year.Profit) * 0.05f + couriers * 2000f));
                const int accountant = 15000;
                EventManager.I.Push(new GameEvent("Vergi denetimi!",
                        "Vergi müfettişleri kapıda. Kurye ödemelerini, depozitoları ve faturaları inceleyecekler.\n" +
                        $"Eksik belge çıkarsa ceza {Ft(fine)} civarında olabilir.")
                    .Add($"Muhasebeciye danış (−{Ft(accountant)}, ceza ihtimali düşer)", () =>
                    {
                        PayFee(accountant, "Muhasebeci (denetim)");
                        AuditResult(fine, 0.2f);
                    })
                    .Add("Kendin hallet", () => AuditResult(fine, 0.55f)));
            }
            else if (roll < 70)
            {
                long fee = Round1000(Random.Range(12000f, 25000f));
                EventManager.I.Push(new GameEvent("Belediye harcı",
                        $"Merkez ofisin tabelası ve işyeri ruhsatı için yıllık belediye harcı geldi: {Ft(fee)}.")
                    .Add($"Öde (−{Ft(fee)})", () => PayFee(fee, "Ruhsat ve tabela harcı"))
                    .Add("Ertele (+%10 ceza, borca yazılır)", () => Postpone(fee)));
            }
            else if (roll < 85)
            {
                long fee = couriers * 1500L;
                PayFee(fee, "Kurye iş güvenliği fonu");
                EventManager.I.Push(new GameEvent("İş güvenliği fonu katkısı",
                    $"Platform üzerinden çalışan her kurye için {Ft(1500)} iş güvenliği fonu katkısı otomatik tahsil edildi.\n{couriers} kurye → {Ft(fee)}"));
            }
            else
            {
                long refund = Round1000(Random.Range(5000f, 20000f));
                money += refund;
                Year.otherIncome += refund;
                Today.otherIncome += refund;
                Log($"+{Ft(refund)} vergi iadesi");
                EventManager.I.Push(new GameEvent("Vergi iadesi!",
                    $"Geçen dönem fazla vergi ödediğin ortaya çıktı. Hesabına {Ft(refund)} iade yattı."));
            }
        }

        void AuditResult(long fine, float chance)
        {
            if (Random.value < chance)
            {
                SpendPenalty(fine, "Vergi denetimi cezası");
                CompanyProfile.AddReputation(-0.1f);
                EventManager.I.Push(new GameEvent("Denetim sonucu: ceza", $"Eksik belgeler bulundu. {Ft(fine)} ceza ödendi, itibar biraz düştü."));
            }
            else
            {
                EventManager.I.Push(new GameEvent("Denetim sonucu: temiz", "Müfettişler her şeyi düzenli buldu. Ceza yok."));
            }
        }

        void PayFee(long amount, string label) => Spend(amount, label, ExpenseCategory.Vergi);

        void OnNewYear(int endedYear)
        {
            long taxable = System.Math.Max(0L, Year.Profit);
            long tax = (long)Mathf.Round(taxable * taxRate);
            LastTaxSummary = $"{endedYear}. yıl → Gelir {Ft(Year.Income)}, Gider {Ft(Year.Expenses)}, " +
                             $"Vergilendirilebilir kâr {Ft(taxable)}, Vergi (%{taxRate * 100f:0}) {Ft(tax)}";
            Year = new PeriodStats();
            localTaxPaidThisYear = 0;

            if (tax <= 0)
            {
                EventManager.I.Push(new GameEvent($"{endedYear}. Yıl Sonu", "Bu yıl kâr olmadığı için vergi çıkmadı.\n\n" + LastTaxSummary));
                return;
            }

            EventManager.I.Push(new GameEvent($"{endedYear}. Yıl Sonu — Vergi Zamanı",
                    LastTaxSummary + $"\n\nAna kasanda {Ft(money)} var.")
                .Add($"Şimdi öde ({Ft(tax)})", () => PayTax(tax))
                .Add("Ertele (+%10 ceza, her gün +%2 faiz, itibar düşer)", () => Postpone(tax)));
        }

        public void PayTax(long amount)
        {
            money -= amount;
            Log($"Vergi ödendi: {Ft(amount)}");
            EventManager.I.Notify($"Vergi ödendi: {Ft(amount)}");
            CheckBankruptcy();
        }

        public void PayDebt()
        {
            if (taxDebt <= 0) return;
            long amount = taxDebt;
            taxDebt = 0;
            PayTax(amount);
        }

        // ---------- Kasa krizi, acil kredi ve iflas ----------
        [Header("Kasa krizi")]
        public int graceDays = 3;               // limitin altında kaç gün kalınca iflas
        public long emergencyLoanAmount = 400000;
        public float emergencyLoanInterest = 0.15f;
        public long bankLoanRemaining;          // acil kredi borcu (her gün taksit kesilir)
        public long bankLoanDaily;
        public bool emergencyLoanUsed;
        int daysUnderLimit;
        bool crisisWarned;

        /// <summary>Kasa limitin altına inince hemen iflas olmaz: uyarı gelir, acil kredi seçeneği ve birkaç gün süre verilir.</summary>
        void CheckBankruptcy()
        {
            if (bankrupt || money > bankruptcyLimit || crisisWarned) return;
            crisisWarned = true;
            var ev = new GameEvent("Kasa krizi!",
                $"Kasa {Ft(money)} oldu: ödemeleri karşılayamıyorsun. {graceDays} gün içinde kasayı {Ft(bankruptcyLimit)} üstüne çıkaramazsan iflas edersin.\n" +
                $"Kuryelerin hesaplarında {Ft(TotalCourierBalances)}, yatacak {Ft(TotalPendingPayouts)} hakediş var.");
            if (!emergencyLoanUsed)
            {
                long total = (long)(emergencyLoanAmount * (1f + emergencyLoanInterest));
                ev.Add($"Acil banka kredisi al (+{Ft(emergencyLoanAmount)}, 30 günde {Ft(total)} geri ödenir)", TakeEmergencyLoan);
            }
            ev.Add($"Toparlamaya çalış ({graceDays} gün süre)", null);
            EventManager.I.Push(ev);
        }

        void TakeEmergencyLoan()
        {
            emergencyLoanUsed = true;
            money += emergencyLoanAmount;
            bankLoanRemaining = (long)(emergencyLoanAmount * (1f + emergencyLoanInterest));
            bankLoanDaily = GameData.Round10(bankLoanRemaining / 30f);
            Log($"+{Ft(emergencyLoanAmount)} acil banka kredisi");
            if (money > bankruptcyLimit) { crisisWarned = false; daysUnderLimit = 0; }
        }

        /// <summary>Gece: kredi taksiti ve iflas süresi.</summary>
        void DailyCrisisCheck()
        {
            if (bankLoanRemaining > 0)
            {
                long pay = System.Math.Min(bankLoanDaily, bankLoanRemaining);
                bankLoanRemaining -= pay;
                money -= pay;
                Year.byCategory[(int)ExpenseCategory.Diger] += pay;
                Today.byCategory[(int)ExpenseCategory.Diger] += pay;
                Log($"−{Ft(pay)} acil kredi taksiti (kalan {Ft(bankLoanRemaining)})");
            }
            if (bankrupt) return;
            if (money > bankruptcyLimit)
            {
                if (daysUnderLimit > 0) EventManager.I.Notify("Kasa toparlandı, iflas tehlikesi geçti.");
                daysUnderLimit = 0;
                crisisWarned = false;
                return;
            }
            daysUnderLimit++;
            if (!crisisWarned) CheckBankruptcy();
            if (daysUnderLimit < graceDays)
            {
                EventManager.I.Notify($"Kasa krizi: {graceDays - daysUnderLimit} gün içinde toparlamazsan iflas.");
                return;
            }
            bankrupt = true;
            EventManager.I.Push(new GameEvent("İflas!",
                    $"Şirketin borcu {Ft(-money)} oldu ve {graceDays} gün içinde toparlanamadı. İflas ettin.")
                .Add("Yeniden başla", () => GameBootstrap.Restart())
                .Add("Borçla devam et (sadece test için)", () => { bankrupt = false; daysUnderLimit = 0; crisisWarned = false; }));
        }
    }
}
