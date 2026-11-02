using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KuryeTycoon
{
    /// <summary>Teslimat dışı giderlerin türü (Finans'ta ayrı gösterilir).</summary>
    public enum ExpenseCategory { Kira, Ekipman, Sozlesme, Bolge, Iade, Kaza, Reklam, Diger }

    public class PeriodStats
    {
        public static readonly string[] CategoryNames =
            { "Ofis kirası", "Ekipman", "Sözleşmeler", "Yeni bölge", "İade / tazminat", "Kaza desteği", "Reklam", "Diğer" };

        public long commission;     // dükkan komisyonları
        public long fees;           // müşteriden alınan teslimat ücretleri
        public long otherIncome;    // kurye kesintileri vb.
        public long courierPay;     // kurye ödemeleri (paket + km + bonus)
        public long boosts;         // boost ödemeleri (kârdan)
        public long rewards;        // haftalık / aylık hedef ödülleri (kârdan)
        public readonly long[] byCategory = new long[8];
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
    /// Para, fiyatlandırma, kurye ödemeleri, vergi kasası ve yıl sonu vergisi.
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
        public long taxVault;
        public long taxDebt;

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
        [Range(0f, 0.5f)] public float vaultPercent = 0.2f;

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
            if (profit > 0 && vaultPercent > 0f)
            {
                long put = Mathf.RoundToInt(profit * vaultPercent);
                money -= put;
                taxVault += put;
            }
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

        public void AddIncome(long amount, string label)
        {
            money += amount;
            Year.otherIncome += amount;
            Today.otherIncome += amount;
            Log($"+{Ft(amount)} {label}");
        }

        public void WithdrawVault()
        {
            if (taxVault <= 0) return;
            money += taxVault;
            Log($"Vergi kasasından ana kasaya {Ft(taxVault)} aktarıldı");
            taxVault = 0;
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
        }

        void OnNewYear(int endedYear)
        {
            long taxable = System.Math.Max(0L, Year.Profit);
            long tax = (long)Mathf.Round(taxable * taxRate);
            LastTaxSummary = $"{endedYear}. yıl → Gelir {Ft(Year.Income)}, Gider {Ft(Year.Expenses)}, " +
                             $"Vergilendirilebilir kâr {Ft(taxable)}, Vergi (%{taxRate * 100f:0}) {Ft(tax)}";
            Year = new PeriodStats();

            if (tax <= 0)
            {
                EventManager.I.Push(new GameEvent($"{endedYear}. Yıl Sonu", "Bu yıl kâr olmadığı için vergi çıkmadı.\n\n" + LastTaxSummary));
                return;
            }

            EventManager.I.Push(new GameEvent($"{endedYear}. Yıl Sonu — Vergi Zamanı",
                    LastTaxSummary + $"\n\nVergi kasanda {Ft(taxVault)} var. Ödeme önce vergi kasasından yapılır.")
                .Add($"Şimdi öde ({Ft(tax)})", () => PayTax(tax))
                .Add("Ertele (+%10 ceza, her gün +%2 faiz, itibar düşer)", () =>
                {
                    long withPenalty = (long)Mathf.Round(tax * 1.1f);
                    Year.penalties += withPenalty - tax;
                    taxDebt += withPenalty;
                }));
        }

        public void PayTax(long amount)
        {
            long fromVault = System.Math.Min(taxVault, amount);
            taxVault -= fromVault;
            long rest = amount - fromVault;
            money -= rest;
            Log($"Vergi ödendi: {Ft(amount)} (kasadan {Ft(fromVault)}, ana kasadan {Ft(rest)})");
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
            if (bankrupt || money + taxVault > bankruptcyLimit) return;
            bankrupt = true;
            EventManager.I.Push(new GameEvent("İflas!",
                    $"Şirketin borcu {Ft(-(money + taxVault))} oldu ve iflas ettin.")
                .Add("Yeniden başla", () => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex))
                .Add("Borçla devam et (sadece test için)", () => bankrupt = false));
        }
    }
}
