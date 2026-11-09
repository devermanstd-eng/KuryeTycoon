using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Biten bir günün özeti (grafikler, CSV denge kaydı ve kayıt dosyası için).</summary>
    [System.Serializable]
    public class DayRecord
    {
        public int companyDay;         // şirketin kaçıncı günü
        public string date;
        public int deliveries;
        public int cancelled;          // kurye bulunamadı / reddedildi
        public int lost;               // ücret pahalı, yoğunluk, ağır sipariş yüzünden gelmeyen
        public long income;
        public long expenses;
        public long deliveryProfit;
        public long net;               // net kâr (cezalar dahil)
        public long courierPay;
        public long equipment;
        public long salaries;
        public long cash;              // gün sonu kasa
        public long freeCash;          // gün sonu serbest kasa
        public float reputation;
        public int couriers;
        public int contractedShops;
        public int district;
        // Teslimat kalitesi (hata ayıklama raporu için; eski kayıtlarda 0)
        public int late;               // söz verilen süreden geç
        public int late15;             // 15 dk'dan fazla geç
        public int cold;               // sıcak yemek soğuk teslim
        public float avgMinutes;       // siparişten teslime ortalama süre
        public float avgAssignWait;    // siparişin kuryeye atanana kadar beklediği ortalama süre
        public float avgPickupWait;    // atanmadan dükkandan alınana kadar ortalama süre
        public int cancelShop;         // dükkanın iptal ettiği
        public int cancelCustomer;     // müşterinin vazgeçtiği (fikir değiştirdi)
        public int cancelDelay;        // müşterinin gecikme yüzünden vazgeçtiği (cancelled içinde)
    }

    /// <summary>
    /// Her gece gün sonu raporuyla birlikte günün özetini saklar.
    /// Finans sekmesindeki grafikler bu veriyi kullanır; istenirse her gün bir CSV dosyasına da yazılır
    /// (denge testi için: Excel / Google Sheets ile açılır, ayraç ';').
    /// </summary>
    public class StatsHistory : MonoBehaviour
    {
        public static StatsHistory I { get; private set; }

        public readonly List<DayRecord> Days = new List<DayRecord>();
        [Tooltip("Her gün sonunda verileri CSV dosyasına yaz")]
        public bool writeCsv = true;

        /// <summary>Bu oturumun CSV dosyası (ilk gün sonunda oluşturulur).</summary>
        public string CsvPath { get; private set; } = "";
        public static string CsvFolder => System.IO.Path.Combine(Application.persistentDataPath, "DengeKaydi");

        void Awake() { I = this; }

        /// <summary>EconomyManager gün sonu raporunu hazırladığı anda çağırır (Today henüz sıfırlanmadan).</summary>
        public void Record(PeriodStats s)
        {
            var clock = GameClock.I;
            var om = OrderManager.I;
            var eco = EconomyManager.I;
            int contracted = 0;
            if (CityMap.I != null)
                foreach (var shop in CityMap.I.Shops) if (shop.contracted) contracted++;

            var d = new DayRecord
            {
                companyDay = clock.CompanyDay,
                date = clock.DateOf(clock.Day - 1),
                deliveries = s.deliveries,
                cancelled = om != null ? om.cancelledToday : 0,
                lost = om != null ? om.lostToday + om.busyLostToday + om.heavyLostToday : 0,
                income = s.Income,
                expenses = s.Expenses,
                deliveryProfit = s.DeliveryProfit,
                net = s.Profit - s.penalties,
                courierPay = s.courierPay,
                equipment = s.byCategory[(int)ExpenseCategory.Ekipman],
                salaries = s.byCategory[(int)ExpenseCategory.Maas],
                cash = eco.money,
                freeCash = eco.FreeCash,
                reputation = CompanyProfile.Reputation,
                couriers = CourierManager.I != null ? CourierManager.I.Couriers.Count : 0,
                contractedShops = contracted,
                district = CityMap.I != null ? CityMap.I.UnlockLevel : 1,
                late = Telemetry.DayLate,
                late15 = Telemetry.DayLate15,
                cold = Telemetry.DayCold,
                avgMinutes = Telemetry.DayDelivered > 0 ? Telemetry.DayMinutes / Telemetry.DayDelivered : 0f,
                avgAssignWait = Telemetry.DayDelivered > 0 ? Telemetry.DayAssignWait / Telemetry.DayDelivered : 0f,
                avgPickupWait = Telemetry.DayDelivered > 0 ? Telemetry.DayPickupWait / Telemetry.DayDelivered : 0f,
                cancelShop = om != null ? om.cancelledShopToday : 0,
                cancelCustomer = om != null ? om.cancelledCustomerToday : 0,
                cancelDelay = om != null ? om.cancelledDelayToday : 0
            };
            Telemetry.ResetDay();
            Days.Add(d);
            if (writeCsv) AppendCsv(d);
        }

        // ---------- CSV ----------

        const string Header =
            "gun;tarih;teslimat;iptal;kacan;gelir;gider;teslimat_kari;net_kar;kurye_odemesi;ekipman;maaslar;kasa;serbest_kasa;itibar;kurye;sozlesmeli_dukkan;bolge";

        static string Row(DayRecord d) =>
            string.Join(";", new[]
            {
                d.companyDay.ToString(), d.date, d.deliveries.ToString(), d.cancelled.ToString(), d.lost.ToString(),
                d.income.ToString(), d.expenses.ToString(), d.deliveryProfit.ToString(), d.net.ToString(),
                d.courierPay.ToString(), d.equipment.ToString(), d.salaries.ToString(), d.cash.ToString(), d.freeCash.ToString(),
                d.reputation.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                d.couriers.ToString(), d.contractedShops.ToString(), d.district.ToString()
            });

        void AppendCsv(DayRecord d)
        {
            try
            {
                if (string.IsNullOrEmpty(CsvPath))
                {
                    // Oturumun ilk kaydı: dosyayı oluştur, kayıttan yüklenen eski günleri de yaz
                    System.IO.Directory.CreateDirectory(CsvFolder);
                    string safeName = MakeSafe(CompanyProfile.Name);
                    CsvPath = System.IO.Path.Combine(CsvFolder, $"denge_{safeName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine(Header);
                    for (int i = 0; i < Days.Count - 1; i++) sb.AppendLine(Row(Days[i]));
                    System.IO.File.WriteAllText(CsvPath, sb.ToString(), new System.Text.UTF8Encoding(true));
                }
                System.IO.File.AppendAllText(CsvPath, Row(d) + "\n");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Denge kaydı yazılamadı: {ex.Message}");
                writeCsv = false;
            }
        }

        static string MakeSafe(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.Length > 0 ? sb.ToString() : "sirket";
        }

        /// <summary>Kayıt dosyasından yüklerken geçmişi geri koyar.</summary>
        public void Restore(List<DayRecord> days)
        {
            Days.Clear();
            if (days != null) Days.AddRange(days);
            CsvPath = ""; // yeni oturumda yeni dosya (eski günler de içine yazılır)
        }
    }
}
