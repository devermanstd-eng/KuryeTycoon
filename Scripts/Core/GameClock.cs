using UnityEngine;

namespace KuryeTycoon
{
    public enum Season { Kis, Ilkbahar, Yaz, Sonbahar }

    /// <summary>
    /// Gerçek takvim: 365 gün, gerçek ay uzunlukları.
    /// Şirket 1 Mart'ta, Pazartesi kurulur. Vergi yılı 1 Ocak–31 Aralık.
    /// </summary>
    public class GameClock : MonoBehaviour
    {
        public static GameClock I { get; private set; }

        static readonly string[] DayNames = { "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar" };
        static readonly string[] MonthNames = { "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
        static readonly int[] MonthDays = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

        public const int DaysPerYear = 365;

        [Tooltip("1x hızda gerçek 1 saniyede geçen oyun dakikası")]
        public float minutesPerSecond = 2f;
        [Tooltip("Şirketin kurulduğu ay (1 = Ocak)")]
        public int startMonth = 3;
        public int startHour = 8;

        public float TotalMinutes { get; private set; }
        public float DeltaMinutes { get; private set; }
        public float SpeedMultiplier { get; set; } = 1f;
        public bool Paused { get; set; }
        /// <summary>Bir olay kartı açıkken oyun otomatik durur.</summary>
        public bool EventPause { get; set; }

        /// <summary>Takvimdeki mutlak gün (1. yılın 1 Ocak'ı = 1).</summary>
        public int Day => (int)(TotalMinutes / 1440f) + 1;
        /// <summary>Şirketin kaçıncı günü (kuruluş günü = 1).</summary>
        public int CompanyDay => Day - startDay + 1;
        public int Year => (Day - 1) / DaysPerYear + 1;
        public int DayOfYear => (Day - 1) % DaysPerYear + 1;

        /// <summary>1 = Ocak ... 12 = Aralık</summary>
        public int Month
        {
            get
            {
                int d = DayOfYear;
                for (int m = 0; m < 12; m++)
                {
                    if (d <= MonthDays[m]) return m + 1;
                    d -= MonthDays[m];
                }
                return 12;
            }
        }

        public int DayOfMonth
        {
            get
            {
                int d = DayOfYear;
                for (int m = 0; m < 12; m++)
                {
                    if (d <= MonthDays[m]) return d;
                    d -= MonthDays[m];
                }
                return d;
            }
        }

        public int DaysInMonth => MonthDays[Month - 1];

        public static int MonthLength(int month) => MonthDays[Mathf.Clamp(month, 1, 12) - 1];
        public static string MonthNameOf(int month) => MonthNames[Mathf.Clamp(month, 1, 12) - 1];

        /// <summary>Verilen yıl/ay/günün mutlak gün numarası (Day ile karşılaştırılabilir).</summary>
        public static int AbsoluteDay(int year, int month, int day)
        {
            int d = (year - 1) * DaysPerYear;
            for (int m = 1; m < Mathf.Clamp(month, 1, 12); m++) d += MonthDays[m - 1];
            return d + day;
        }
        public int WeekOfMonth => (DayOfMonth - 1) / 7 + 1;
        /// <summary>0 = Pazartesi ... 6 = Pazar (kuruluş günü Pazartesi)</summary>
        public int DayOfWeek => ((Day - startDay) % 7 + 7) % 7;
        public bool IsWeekend => DayOfWeek >= 5;
        public string DayName => DayNames[DayOfWeek];
        public string MonthName => MonthNames[Month - 1];
        public int Hour => (int)(TotalMinutes % 1440f) / 60;
        public int Minute => (int)(TotalMinutes % 60f);

        public Season Season
        {
            get
            {
                int m = Month;
                if (m == 12 || m <= 2) return Season.Kis;
                if (m <= 5) return Season.Ilkbahar;
                if (m <= 8) return Season.Yaz;
                return Season.Sonbahar;
            }
        }

        public event System.Action OnNewDay;
        public event System.Action OnNewMonth;
        public event System.Action<int> OnNewYear; // parametre: biten yıl

        int startDay;
        int lastDay;
        int lastMonth;
        int lastYear;

        void Awake()
        {
            I = this;
            int firstDayOfMonth = 1;
            for (int m = 0; m < Mathf.Clamp(startMonth, 1, 12) - 1; m++) firstDayOfMonth += MonthDays[m];
            TotalMinutes = (firstDayOfMonth - 1) * 1440f + startHour * 60f;
            startDay = Day;
            lastDay = Day;
            lastMonth = Month;
            lastYear = Year;
        }

        void Update()
        {
            DeltaMinutes = (Paused || EventPause) ? 0f : Time.deltaTime * minutesPerSecond * SpeedMultiplier;
            TotalMinutes += DeltaMinutes;

            if (Day != lastDay)
            {
                lastDay = Day;
                OnNewDay?.Invoke();
            }
            if (Month != lastMonth)
            {
                lastMonth = Month;
                OnNewMonth?.Invoke();
            }
            if (Year != lastYear)
            {
                int ended = lastYear;
                lastYear = Year;
                OnNewYear?.Invoke(ended);
            }
        }

        public string SeasonName
        {
            get
            {
                switch (Season)
                {
                    case Season.Kis: return "Kış";
                    case Season.Ilkbahar: return "İlkbahar";
                    case Season.Yaz: return "Yaz";
                    default: return "Sonbahar";
                }
            }
        }

        public string DateText => $"{DayOfMonth} {MonthName}, Yıl {Year}";
        public string TimeText => $"{DateText} · {DayName} {Hour:00}:{Minute:00} · {CompanyDay}. gün";
    }
}
