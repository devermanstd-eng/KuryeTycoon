using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KuryeTycoon
{
    public class PeriodStats
    {
        public long commission;     // dükkan komisyonları
        public long fees;           // müşteriden alınan teslimat ücretleri
        public long otherIncome;    // kurye kesintileri vb.
        public long courierPay;     // kurye ödemeleri
        public long otherExpenses;  // kira, ekipman, sözleşme, iade, tazminat...
        public long penalties;      // vergi cezası/faizi (vergiden düşülmez)
        public int deliveries;

        public long Income => commission + fees + otherIncome;
        public long Expenses => courierPay + otherExpenses;
        /// <summary>Vergilendirilebilir kâr (giderler düşülmüş, cezalar hariç).</summary>
        public long Profit => Income - Expenses;
    }

    /// <summary>
    /// Para, fiyatlandırma, kurye ödemeleri, vergi kasası ve yıl sonu vergisi.
    ///
    /// Sipariş başı:
    ///   Şirket geliri = sepet × komisyon + teslimat ücreti
    ///   Net kâr       = şirket geliri − kurye ödemesi
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
        public int feePerTile = 30;
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

        public PeriodStats Year = new PeriodStats();
        public PeriodStats Today = new PeriodStats();
        public PeriodStats Yesterday;
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

        // ---------- Ödemeler ----------

        public int TimeBonus()
        {
            int h = GameClock.I.Hour;
            if (h >= 22 || h < 6) return nightBonus;
            if ((h >= 11 && h < 14) || (h >= 18 && h < 21)) return rushBonus;
            return 0;
        }

        public int CurrentBonus => Mathf.RoundToInt((WeatherSystem.I.WeatherBonus + TimeBonus()) * bonusMultiplier);

        /// <summary>Bu kuryeye bu mesafe için şu an ödenecek tutar.</summary>
        public int CourierPay(Courier c, int distance) =>
            c.V.basePay + c.V.payPerTile * distance + c.extraPay + CurrentBonus;

        public int ProfitIf(Order o, Courier c) => o.CompanyIncome - CourierPay(c, o.distance);

        /// <summary>Ekipteki en pahalı kuryenin bu mesafe için ücreti.</summary>
        public int MaxCourierPay(int distance)
        {
            int max = 0;
            foreach (var c in CourierManager.I.Couriers) max = Mathf.Max(max, CourierPay(c, distance));
            if (max == 0)
            {
                var v = GameData.Vehicle(VehicleKind.Bisiklet);
                max = v.basePay + v.payPerTile * distance + CurrentBonus;
            }
            return max;
        }

        /// <summary>
        /// Müşteriden alınacak teslimat ücreti:
        /// max(taban + kare × mesafe, en pahalı kurye ücreti + min. kâr − komisyon)
        /// </summary>
        public int QuoteDeliveryFee(int commissionIncome, int distance)
        {
            int normal = baseFee + feePerTile * distance;
            int required = MaxCourierPay(distance) + minProfit - commissionIncome;
            return GameData.CeilTo10(Mathf.Max(normal, required, 0));
        }

        public void RegisterDelivery(Order o)
        {
            money += o.CompanyIncome - o.courierPay;
            foreach (var s in new[] { Year, Today })
            {
                s.commission += o.commissionIncome;
                s.fees += o.deliveryFee;
                s.courierPay += o.courierPay;
                s.deliveries++;
            }
            o.courier.totalEarned += o.courierPay;

            int profit = o.Profit;
            if (profit > 0 && vaultPercent > 0f)
            {
                long put = Mathf.RoundToInt(profit * vaultPercent);
                money -= put;
                taxVault += put;
            }
            Log($"#{o.id} teslim: gelir {Ft(o.CompanyIncome)}, kurye {Ft(o.courierPay)}, kâr {Ft(profit)}");
            CheckBankruptcy();
        }

        // ---------- Para hareketleri ----------

        public bool TrySpend(long amount, string label)
        {
            if (money < amount)
            {
                EventManager.I.Notify($"Yetersiz bakiye: {label} ({Ft(amount)})");
                return false;
            }
            Spend(amount, label);
            return true;
        }

        public void Spend(long amount, string label, bool penalty = false)
        {
            money -= amount;
            if (penalty)
            {
                Year.penalties += amount;
                Today.penalties += amount;
            }
            else
            {
                Year.otherExpenses += amount;
                Today.otherExpenses += amount;
            }
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
            Ledger.Insert(0, $"G{c.Day} {c.Hour:00}:{c.Minute:00}  {s}");
            if (Ledger.Count > 60) Ledger.RemoveAt(Ledger.Count - 1);
        }

        // ---------- Gün / yıl ----------

        void OnNewDay()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;

            long courierCosts = 0;
            foreach (var c in CourierManager.I.Couriers) courierCosts += c.V.dailyCost;
            Spend(officeRentPerDay, "Ofis kirası");
            if (courierCosts > 0) Spend(courierCosts, "Kurye günlük giderleri (yakıt, sigorta, telefon)");

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
