using UnityEngine;

namespace KuryeTycoon
{
    public enum Season { Kis, Ilkbahar, Yaz, Sonbahar }

    /// <summary>
    /// Oyun içi takvim: 1 ay = 28 gün (4 hafta), 1 yıl = 12 ay.
    /// Mevsimler aylara göre (Aralık–Şubat kış ...). Oyun Mart ayında, Pazartesi başlar.
    /// </summary>
    public class GameClock : MonoBehaviour
    {
        public static GameClock I { get; private set; }

        static readonly string[] DayNames = { "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar" };
        static readonly string[] MonthNames = { "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };

        [Tooltip("1x hızda gerçek 1 saniyede geçen oyun dakikası")]
        public float minutesPerSecond = 2f;
        [Tooltip("Bir ay kaç gün (7'nin katı olmalı)")]
        public int daysPerMonth = 28;
        public int monthsPerYear = 12;
        [Tooltip("Oyun hangi ayda başlar (1 = Ocak)")]
        public int startMonth = 3;
        public int startHour = 8;

        public float TotalMinutes { get; private set; }
        public float DeltaMinutes { get; private set; }
        public float SpeedMultiplier { get; set; } = 1f;
        public bool Paused { get; set; }
        /// <summary>Bir olay kartı açıkken oyun otomatik durur.</summary>
        public bool EventPause { get; set; }

        public int DaysPerYear => daysPerMonth * monthsPerYear;
        public int Day => (int)(TotalMinutes / 1440f) + 1;
        public int Year => (Day - 1) / DaysPerYear + 1;
        public int DayOfYear => (Day - 1) % DaysPerYear + 1;
        /// <summary>1 = Ocak ... 12 = Aralık</summary>
        public int Month => (DayOfYear - 1) / daysPerMonth + 1;
        public int DayOfMonth => (DayOfYear - 1) % daysPerMonth + 1;
        public int WeekOfMonth => (DayOfMonth - 1) / 7 + 1;
        /// <summary>0 = Pazartesi ... 6 = Pazar</summary>
        public int DayOfWeek => (Day - 1) % 7;
        public bool IsWeekend => DayOfWeek >= 5;
        public string DayName => DayNames[DayOfWeek];
        public string MonthName => MonthNames[(Month - 1) % 12];
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

        int lastDay;
        int lastMonth;
        int lastYear;

        void Awake()
        {
            I = this;
            TotalMinutes = (Mathf.Clamp(startMonth, 1, monthsPerYear) - 1) * daysPerMonth * 1440f + startHour * 60f;
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

        public string TimeText => $"Yıl {Year} · {DayOfMonth} {MonthName} · {DayName} {Hour:00}:{Minute:00}";
    }
}
