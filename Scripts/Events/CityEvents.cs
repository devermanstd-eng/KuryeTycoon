using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Bir şehir etkinliği: belli saatlerde talebi değiştirir, bazıları siparişleri bir bölgeye toplar.</summary>
    [System.Serializable]
    public class CityEvent
    {
        public string title;
        public string desc;
        public int day;                 // GameClock.Day
        public float fromHour, toHour;  // toHour > 24 olabilir (gece yarısını geçer)
        public float demand = 1f;       // bütün siparişler
        public float food = 1f;         // sadece yemek / kafe
        public float market = 1f;       // sadece market
        public int spotX = -1, spotY = -1, spotRadius;
        public float spotShare;         // siparişlerin ne kadarı etkinlik alanından
        public bool announced, started;

        public bool HasSpot => spotX >= 0;
        public Vector2Int Spot => new Vector2Int(spotX, spotY);

        public bool ActiveAt(float absHour)
        {
            float start = (day - 1) * 24f + fromHour, end = (day - 1) * 24f + toHour;
            return absHour >= start && absHour < end;
        }

        public string TimeText => $"{Hh(fromHour)}–{Hh(toHour)}";
        static string Hh(float h) { h %= 24f; int hh = (int)h, mm = Mathf.RoundToInt((h - hh) * 60f); return $"{hh:00}:{mm:00}"; }
    }

    [System.Serializable]
    public class CityEventsSave
    {
        public List<CityEvent> events = new List<CityEvent>();
        public int plannedWeekStart;
        public int hintDay, lunchHintDay;
    }

    /// <summary>
    /// Şehir hayatı:
    ///  • Takvim: Noel (24–26 Aralık), Yılbaşı gecesi, 1 Ocak, Sevgililer Günü, Black Friday, sınav haftaları (öğrenciler az sipariş verir, gece çok).
    ///  • Her hafta rastgele 1–2 etkinlik: maç akşamı (evde maç izleyenler pizza/burger söyler), Kossuth Meydanı'nda konser,
    ///    Belediye Parkı'nda sokak festivali (siparişlerin bir kısmı etkinlik alanından gelir). Etkinlik sabah duyurulur, haritada işaretlenir.
    ///  • Erken oyun ipucu: akşam yoğunluğundan önce kurye azsa "kurye al" uyarısı.
    /// </summary>
    public class CityEvents : MonoBehaviour
    {
        public static CityEvents I { get; private set; }

        public readonly List<CityEvent> Events = new List<CityEvent>();
        int plannedWeekStart = -100;
        int hintDay = -1, lunchHintDay = -1;
        int lastHourChecked = -1;

        void Awake() { I = this; }

        public void Init()
        {
            GameClock.I.OnNewDay += OnNewDay;
            Plan();
        }

        static float AbsHour => GameClock.I.TotalMinutes / 60f;

        // ---------------------------------------------------------------- talep etkisi (OrderManager)

        public static float DemandFactor
        {
            get
            {
                if (I == null || GameClock.I == null) return 1f;
                float f = 1f, h = AbsHour;
                foreach (var e in I.Events) if (e.ActiveAt(h)) f *= e.demand;
                if (ExamWeeks)
                {
                    int hr = GameClock.I.Hour;
                    f *= hr >= 22 || hr < 2 ? 1.12f : 0.95f;   // öğrenciler gündüz ders çalışıyor, gece aç kalıyor
                }
                return f;
            }
        }

        public static float ShopFactor(Shop s)
        {
            if (I == null || GameClock.I == null || s == null) return 1f;
            float f = 1f, h = AbsHour;
            foreach (var e in I.Events)
                if (e.ActiveAt(h)) f *= s.def.isMarket ? e.market : e.food;
            return f;
        }

        /// <summary>Şu an bir etkinlik alanı varsa, siparişin oradan gelme ihtimali.</summary>
        public static bool TryEventSpot(out Vector2Int spot, out int radius)
        {
            spot = default; radius = 0;
            if (I == null || GameClock.I == null) return false;
            float h = AbsHour;
            foreach (var e in I.Events)
            {
                if (!e.HasSpot || !e.ActiveAt(h)) continue;
                if (!CityMap.I.IsUnlocked(e.Spot)) continue;
                if (Random.value < e.spotShare) { spot = e.Spot; radius = e.spotRadius; return true; }
            }
            return false;
        }

        /// <summary>Sınav dönemleri: 12 Aralık – 24 Ocak ve 18 Mayıs – 21 Haziran.</summary>
        public static bool ExamWeeks
        {
            get
            {
                if (GameClock.I == null) return false;
                int m = GameClock.I.Month, d = GameClock.I.DayOfMonth;
                return (m == 12 && d >= 12) || (m == 1 && d <= 24) || (m == 5 && d >= 18) || (m == 6 && d <= 21);
            }
        }

        public IEnumerable<CityEvent> Today
        {
            get
            {
                int day = GameClock.I.Day;
                foreach (var e in Events) if (e.day == day) yield return e;
            }
        }

        public CityEvent ActiveNow
        {
            get
            {
                float h = AbsHour;
                foreach (var e in Events) if (e.ActiveAt(h)) return e;
                return null;
            }
        }

        // ---------------------------------------------------------------- planlama

        void OnNewDay()
        {
            Events.RemoveAll(e => e.day < GameClock.I.Day - 1);
            Plan();
            int m = GameClock.I.Month, d = GameClock.I.DayOfMonth;
            if ((m == 12 && d == 12) || (m == 5 && d == 18))
                EventManager.I.Notify("Şehir: sınav dönemi başladı. Öğrenciler gündüz daha az sipariş veriyor ama gece geç saatte acıkıyorlar.");
            if ((m == 1 && d == 25) || (m == 6 && d == 22))
                EventManager.I.Notify("Şehir: sınav dönemi bitti, öğrenci siparişleri normale döndü.");
        }

        void Plan()
        {
            var clock = GameClock.I;
            // Bugün ve yarın için takvim etkinlikleri
            for (int k = 0; k <= 1; k++) AddCalendar(clock.Day + k);

            // Haftalık rastgele etkinlikler (pazartesi planlanır)
            int weekStart = clock.Day - clock.DayOfWeek;
            if (weekStart != plannedWeekStart)
            {
                plannedWeekStart = weekStart;
                int n = Random.value < 0.45f ? 2 : 1;
                var used = new HashSet<int>();
                for (int i = 0; i < n; i++)
                {
                    int kind = Random.Range(0, 4);
                    if (!used.Add(kind)) continue;
                    AddRandom(weekStart, kind);
                }
            }
        }

        bool Has(int day, string title) => Events.Exists(e => e.day == day && e.title == title);

        void Add(CityEvent e)
        {
            if (e.day < GameClock.I.Day || Has(e.day, e.title)) return;
            Events.Add(e);
        }

        void AddCalendar(int absDay)
        {
            int dd = GameClock.I.DayOfMonthOf(absDay);
            int dayOfYear = (absDay - 1) % GameClock.DaysPerYear + 1;
            int m = MonthOfDayOfYear(dayOfYear);
            int dow = GameClock.I.DayOfWeekOf(absDay);

            if (m == 12 && dd == 24)
                Add(new CityEvent { title = "Szenteste (Noel arifesi)", day = absDay, fromHour = 9, toHour = 15, market = 1.7f, food = 0.9f,
                    desc = "Herkes son dakika alışverişinde: market siparişleri patlıyor. Akşam aileler evde, yemek siparişi az." });
            if (m == 12 && dd == 24)
                Add(new CityEvent { title = "Noel akşamı", day = absDay, fromHour = 17, toHour = 24, demand = 0.55f,
                    desc = "Aileler sofrada: akşam siparişleri çok az. Kuryelerin bir kısmını erken eve gönderebilirsin." });
            if (m == 12 && (dd == 25 || dd == 26))
                Add(new CityEvent { title = "Noel tatili", day = absDay, fromHour = 11, toHour = 22, demand = 0.85f, food = 1.15f, market = 0.6f,
                    desc = "Marketlerin çoğu kapalı, evde kalanlar yemek söylüyor." });
            if (m == 12 && dd == 31)
                Add(new CityEvent { title = "Yılbaşı gecesi (Szilveszter)", day = absDay, fromHour = 16, toHour = 23.5f, demand = 1.45f, market = 1.3f,
                    desc = "Parti hazırlığı: pizza, atıştırmalık, içecek. Akşam boyunca yoğunluk; gece yarısı herkes sokakta." });
            if (m == 1 && dd == 1)
                Add(new CityEvent { title = "Yeni yıl sabahı", day = absDay, fromHour = 6, toHour = 12, demand = 0.45f,
                    desc = "Şehir uyuyor. Öğleden sonra herkes uyanınca burger ve döner siparişleri artar." });
            if (m == 1 && dd == 1)
                Add(new CityEvent { title = "Yeni yıl öğleden sonrası", day = absDay, fromHour = 12, toHour = 21, food = 1.35f,
                    desc = "Akşamdan kalma şehir: fast food siparişleri çok yüksek." });
            if (m == 2 && dd == 14)
                Add(new CityEvent { title = "Sevgililer Günü", day = absDay, fromHour = 18, toHour = 23, food = 1.3f,
                    desc = "Evde romantik akşam yemeği: sushi ve gurme restoranlar çok sipariş alıyor." });
            if (m == 11 && dow == 4 && dd >= 23)
                Add(new CityEvent { title = "Black Friday", day = absDay, fromHour = 8, toHour = 22, market = 1.3f, demand = 1.05f,
                    desc = "İndirim günü: market siparişleri ve sepetler büyük." });
        }

        static int MonthOfDayOfYear(int d)
        {
            int m = 1;
            while (m < 12 && d > GameClock.MonthLength(m)) { d -= GameClock.MonthLength(m); m++; }
            return m;
        }

        void AddRandom(int weekStart, int kind)
        {
            switch (kind)
            {
                case 0: // Maç akşamı
                {
                    int dow = Random.value < 0.5f ? 2 : (Random.value < 0.5f ? 5 : 6);
                    float kick = Random.value < 0.5f ? 18.5f : 20.75f;
                    Add(new CityEvent { title = "Maç akşamı: Nyíregyháza Spartacus", day = weekStart + dow, fromHour = kick - 1.5f, toHour = kick + 2f,
                        food = 1.4f, market = 1.15f, desc = "Şehrin takımı oynuyor. Maçı evde izleyenler pizza, burger ve bira söylüyor; maç başlamadan hemen önce yoğunluk zirve yapar." });
                    break;
                }
                case 1: // Konser
                {
                    int dow = Random.value < 0.5f ? 4 : 5;
                    Add(new CityEvent { title = "Kossuth Meydanı'nda konser", day = weekStart + dow, fromHour = 19, toHour = 23.5f,
                        demand = 1.12f, spotX = CityMap.PlazaCenter.x, spotY = CityMap.PlazaCenter.y, spotRadius = 5, spotShare = 0.35f,
                        desc = "Meydanda açık hava konseri: çevredeki evlere ve ofislere sipariş yağıyor, meydanın etrafı kalabalık (trafik yavaş)." });
                    break;
                }
                case 2: // Sokak festivali
                {
                    int dow = Random.value < 0.5f ? 5 : 6;
                    Add(new CityEvent { title = "Belediye Parkı sokak festivali", day = weekStart + dow, fromHour = 11, toHour = 20,
                        demand = 1.08f, food = 1.1f, spotX = CityMap.BelediyeCenter.x, spotY = CityMap.BelediyeCenter.y, spotRadius = 6, spotShare = 0.3f,
                        desc = "Gün boyu festival: parkın çevresinden çok sipariş gelir. Dükkanlar da kalabalık." });
                    break;
                }
                default: // Yağmurlu pazar sinema / dizi gecesi gibi: büyük dizi finali
                {
                    int dow = Random.Range(0, 7);
                    Add(new CityEvent { title = "Popüler dizinin finali", day = weekStart + dow, fromHour = 19.5f, toHour = 22.5f, food = 1.25f,
                        desc = "Herkes evde ekran başında: akşam yemek siparişleri artar." });
                    break;
                }
            }
        }

        // ---------------------------------------------------------------- duyurular ve ipuçları

        void Update()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || GameClock.I == null) return;
            int hour = GameClock.I.Hour;
            if (hour == lastHourChecked) return;
            lastHourChecked = hour;
            int day = GameClock.I.Day;

            foreach (var e in Events)
            {
                if (e.day != day) continue;
                // Sabah 8'de (ya da etkinlik daha erkense hemen) duyur
                if (!e.announced && (hour >= 8 || hour >= e.fromHour - 1f))
                {
                    e.announced = true;
                    EventManager.I.Notify($"Şehir etkinliği bugün {e.TimeText}: {e.title}. {e.desc}");
                }
                if (!e.started && hour >= Mathf.FloorToInt(e.fromHour) && hour < e.toHour)
                {
                    e.started = true;
                    EventManager.I.Notify($"Şehir etkinliği başladı: {e.title} ({e.TimeText}). {ImpactText(e)}");
                }
            }

            EarlyGameHints(hour, day);
        }

        static string ImpactText(CityEvent e)
        {
            var parts = new List<string>();
            float all = e.demand;
            if (all > 1.01f) parts.Add($"siparişler ~%{(all - 1f) * 100f:0} fazla");
            else if (all < 0.99f) parts.Add($"siparişler ~%{(1f - all) * 100f:0} az");
            if (e.food > 1.01f) parts.Add($"yemek siparişleri ~%{(e.food - 1f) * 100f:0} fazla");
            else if (e.food < 0.99f) parts.Add($"yemek siparişleri az");
            if (e.market > 1.01f) parts.Add($"market siparişleri ~%{(e.market - 1f) * 100f:0} fazla");
            else if (e.market < 0.99f) parts.Add("market siparişleri az");
            if (e.HasSpot) parts.Add($"siparişlerin ~%{e.spotShare * 100f:0}'i etkinlik alanından");
            return parts.Count > 0 ? char.ToUpper(parts[0][0]) + string.Join(", ", parts).Substring(1) + "." : "";
        }

        /// <summary>
        /// Erken oyun dengesi: akşam yoğunluğu (18–21) ve öğle (12–14) öncesi kurye sayısı azsa ipucu.
        /// İlk 3 hafta her gün; sonrasında sadece dün çok iptal olduysa.
        /// </summary>
        void EarlyGameHints(int hour, int day)
        {
            var cm = CourierManager.I;
            var om = OrderManager.I;
            if (cm == null || om == null || UpgradeManager.I == null) return;
            int couriers = cm.Couriers.Count;
            int contracted = 0;
            foreach (var s in CityMap.I.Shops) if (s.contracted && CityMap.I.IsUnlocked(s.tile)) contracted++;
            // Kabaca: 2 sözleşmeli dükkana 1 kurye + 1 yedek
            int want = Mathf.Max(3, Mathf.CeilToInt(contracted * 0.6f) + 1);
            bool early = GameClock.I.CompanyDay <= 21;
            var hist = StatsHistory.I != null ? StatsHistory.I.Days : null;
            bool lastDayBad = hist != null && hist.Count > 0 && hist[hist.Count - 1].cancelled + hist[hist.Count - 1].lost >= 6;
            if (!early && !lastDayBad) return;
            if (couriers >= want && !lastDayBad) return;
            bool canHire = !UpgradeManager.I.CourierCapFull;
            int apps = KayitSistemi.I != null ? KayitSistemi.I.Adaylar.Count : 0;

            if (hour == 16 && hintDay != day)
            {
                hintDay = day;
                string why = lastDayBad ? $"Dün {hist[hist.Count - 1].cancelled + hist[hist.Count - 1].lost} sipariş iptal oldu ya da kaçtı. " : "";
                string how = !canHire ? "Kurye hesabı sınırı dolu: Gelişim'den platform seviyesini yükselt."
                           : apps > 0 ? $"Kayıt sürecinde {apps} aday var: Kayıtlar'dan yeni kurye bonusu ya da reklamla hızlandır."
                           : "Kayıtlar'dan reklam ver ya da yeni kurye bonusu aç.";
                EventManager.I.Notify($"İpucu: akşam yoğunluğu (18:00–21:00) yaklaşıyor. {why}{couriers} kuryen var, bu kadar dükkan için en az {want} önerilir. {how}");
            }
            else if (hour == 10 && lunchHintDay != day && early && couriers < want - 1)
            {
                lunchHintDay = day;
                EventManager.I.Notify($"İpucu: öğle yoğunluğu (12:00–14:00) yaklaşıyor ve sadece {couriers} kuryen var. Siparişler bekletilirse iptal olur — " +
                                      (canHire ? "Kayıtlar'dan kayıt kampanyası başlat." : "Gelişim'den kurye sınırını artır."));
            }
        }

        // ---------------------------------------------------------------- kayıt

        public CityEventsSave CaptureSave() => new CityEventsSave
        {
            events = new List<CityEvent>(Events),
            plannedWeekStart = plannedWeekStart,
            hintDay = hintDay,
            lunchHintDay = lunchHintDay
        };

        public void RestoreSave(CityEventsSave s)
        {
            if (s == null) return;
            Events.Clear();
            if (s.events != null) Events.AddRange(s.events);
            plannedWeekStart = s.plannedWeekStart;
            hintDay = s.hintDay;
            lunchHintDay = s.lunchHintDay;
            Plan();
        }
    }
}
