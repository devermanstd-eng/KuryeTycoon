using UnityEngine;

namespace KuryeTycoon
{
    public enum Season { Kis, Ilkbahar, Yaz, Sonbahar }

    /// <summary>
    /// Oyun içi zaman: dakika, gün, haftanın günü, yıl, mevsim. Hız ve duraklatma burada.
    /// Varsayılan takvim: 1 yıl = 28 gün = 4 hafta; her mevsim 1 hafta. Gün 1 = Pazartesi.
    /// </summary>
    public class GameClock : MonoBehaviour
    {
        public static GameClock I { get; private set; }

        static readonly string[] DayNames = { "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar" };

        [Tooltip("1x hızda gerçek 1 saniyede geçen oyun dakikası")]
        public float minutesPerSecond = 2f;
        [Tooltip("Bir oyun yılı kaç gün (vergi her yıl sonunda ödenir). 7'nin katı olması önerilir.")]
        public int daysPerYear = 28;
        public int startHour = 8;

        public float TotalMinutes { get; private set; }
        public float DeltaMinutes { get; private set; }
        public float SpeedMultiplier { get; set; } = 1f;
        public bool Paused { get; set; }
        /// <summary>Bir olay kartı açıkken oyun otomatik durur.</summary>
        public bool EventPause { get; set; }

        public int Day => (int)(TotalMinutes / 1440f) + 1;
        public int Year => (Day - 1) / daysPerYear + 1;
        public int DayOfYear => (Day - 1) % daysPerYear + 1;
        public int Week => (Day - 1) / 7 + 1;
        /// <summary>0 = Pazartesi ... 6 = Pazar</summary>
        public int DayOfWeek => (Day - 1) % 7;
        public bool IsWeekend => DayOfWeek >= 5;
        public string DayName => DayNames[DayOfWeek];
        public int Hour => (int)(TotalMinutes % 1440f) / 60;
        public int Minute => (int)(TotalMinutes % 60f);
        public Season Season => (Season)Mathf.Min(3, (DayOfYear - 1) * 4 / daysPerYear);

        public event System.Action OnNewDay;
        public event System.Action<int> OnNewYear; // parametre: biten yıl

        int lastDay = 1;
        int lastYear = 1;

        void Awake()
        {
            I = this;
            TotalMinutes = startHour * 60f;
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

        public string TimeText => $"Yıl {Year} · Gün {DayOfYear}/{daysPerYear} · {DayName} {Hour:00}:{Minute:00}";
    }
}
