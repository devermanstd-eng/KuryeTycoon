using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KuryeTycoon
{
    /// <summary>Teslimat dışı giderlerin türü (Finans'ta ayrı gösterilir).</summary>
    public enum ExpenseCategory { Kira, Ekipman, Sozlesme, Bolge, Iade, Kaza, Reklam, Diger, Vergi, Maas }

    public class PeriodStats
    {
        public static readonly string[] CategoryNames =
            { "Ofis kirası", "Ekipman", "Sözleşmeler", "Yeni bölge", "İade / tazminat", "Kaza desteği", "Reklam", "Diğer", "Vergi ve harçlar", "Destek maaşları" };

        public long commission;     // dükkan komisyonları
        public long fees;           // müşteriden alınan teslimat ücretleri
        public long otherIncome;    // kurye kesintileri vb.
        public long courierPay;     // kurye ödemeleri (paket + km + bonus)
        public long boosts;         // boost ödemeleri (kârdan)
        public long rewards;        // haftalık / aylık hedef ödülleri (kârdan)
        public readonly long[] byCategory = new long[10];
        public long penalties;      // vergi cezası/faizi (vergiden düşülmez)
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
        public int minProfit = 200;

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
        /// Müşteriden alınacak teslimat ücreti:
        /// en büyüğü( taban + km ücreti × km , en pahalı kurye ücreti + min. kâr − komisyon )
        /// </summary>
        public int QuoteDeliveryFee(int commissionIncome, int tiles, float weightKg)
        {
            int normal = baseFee + Mathf.RoundToInt(feePerKm * GameData.Km(tiles));
            int required = MaxCourierPay(tiles, weightKg) + minProfit - commissionIncome;
            return GameData.CeilTo10(Mathf.Max(normal, required, 0));
        }

        public void RegisterDelivery(Order o)
        {
            money += o.CompanyIncome - o.courierPay - o.boostPay;
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

        public bool TrySpend(long amount, string label, ExpenseCategory category = ExpenseCategory.Diger)
        {
            if (money < amount)
            {
                EventManager.I.Notify($"Yetersiz bakiye: {label} ({Ft(amount)})");
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
        public void SpendCampaign(long amount, string label)
        {
            money -= amount;
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
            c.savings = System.Math.Max(0L, c.savings - GameData.DepositAmount);
            money += GameData.DepositAmount;
            depositsHeld += GameData.DepositAmount;
            Log($"+{Ft(GameData.DepositAmount)} ekipman depozitosu: {c.courierName} (iade borcu)");
            EventManager.I.Notify($"{c.courierName} {Ft(GameData.DepositThreshold)} kazanca ulaştı, {Ft(GameData.DepositAmount)} ekipman depozitosu kesildi.");
        }

        public void RefundDeposit(Courier c)
        {
            money -= GameData.DepositAmount;
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

            // Kuryeler kendi araçlarıyla, serbest çalışır: şirketin günlük gideri sadece ofis
            Spend(officeRentPerDay, "Ofis kirası", ExpenseCategory.Kira);

            if (taxDebt > 0)
            {
                long interest = (long)Mathf.Ceil(taxDebt * 0.02f);
                taxDebt += interest;
                Year.penalties += interest;
                CompanyProfile.AddReputation(-0.03f);
                EventManager.I.Notify($"Ödenmemiş vergi borcu büyüyor: {Ft(taxDebt)} (itibar düştü)");
            }

            Yesterday = Today;
            Today = new PeriodStats();

            if (nextSurpriseDay == 0) ScheduleSurprise();
            else if (GameClock.I.CompanyDay >= nextSurpriseDay)
            {
                RollSurpriseTax();
                ScheduleSurprise();
            }
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
                    $"Platform üzerinden çalışan her kurye için 1 500 Ft iş güvenliği fonu katkısı otomatik tahsil edildi.\n{couriers} kurye → {Ft(fee)}"));
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

        void CheckBankruptcy()
        {
            if (bankrupt || money > bankruptcyLimit) return;
            bankrupt = true;
            EventManager.I.Push(new GameEvent("İflas!",
                    $"Şirketin borcu {Ft(-money)} oldu ve iflas ettin.")
                .Add("Yeniden başla", () => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex))
                .Add("Borçla devam et (sadece test için)", () => bankrupt = false));
        }
    }
}
