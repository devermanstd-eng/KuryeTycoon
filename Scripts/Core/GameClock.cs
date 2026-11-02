using UnityEngine;

namespace KuryeTycoon
{
    public enum Season { Kis, Ilkbahar, Yaz, Sonbahar }

    /// <summary>Oyun içi zaman: dakika, gün, yıl, mevsim. Hız ve duraklatma burada.</summary>
    public class GameClock : MonoBehaviour
    {
        public static GameClock I { get; private set; }

        [Tooltip("1x hızda gerçek 1 saniyede geçen oyun dakikası")]
        public float minutesPerSecond = 2f;
        [Tooltip("Bir oyun yılı kaç gün (vergi her yıl sonunda ödenir)")]
        public int daysPerYear = 12;
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

        public string SeasonName => Season switch
        {
            Season.Kis => "Kış",
            Season.Ilkbahar => "İlkbahar",
            Season.Yaz => "Yaz",
            _ => "Sonbahar"
        };

        public string TimeText => $"Yıl {Year} · Gün {DayOfYear}/{daysPerYear} · {Hour:00}:{Minute:00}";
    }
}
