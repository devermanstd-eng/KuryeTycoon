using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    public enum WeatherKind { Gunesli, Sicak, Bulutlu, Yagmurlu, Soguk, Karli, Firtina }

    /// <summary>Mevsime göre hava durumu. Hava; bonusu, kurye hızını ve sipariş talebini etkiler.</summary>
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem I { get; private set; }

        public WeatherKind Current { get; private set; } = WeatherKind.Gunesli;

        /// <summary>
        /// Hava önceden planlanır (en az 36 saat ileri): her değişimin saati ve türü bellidir. Böylece yarının hava tahmini
        /// verilebilir (GunPlani akşam 20:00'de duyurur) ve oyuncu yoğun saat primini, kurye sayısını ona göre ayarlar.
        /// Tahmin gerçekle aynıdır (oyun içi meteoroloji yanılmaz).
        /// </summary>
        public struct Degisim { public float at; public WeatherKind kind; }
        readonly List<Degisim> plan = new List<Degisim>();
        const float PlanUfku = 36f * 60f;

        static readonly WeatherKind[][] SeasonKinds =
        {
            new[] { WeatherKind.Soguk, WeatherKind.Karli, WeatherKind.Bulutlu, WeatherKind.Gunesli, WeatherKind.Firtina },   // Kış
            new[] { WeatherKind.Gunesli, WeatherKind.Bulutlu, WeatherKind.Yagmurlu, WeatherKind.Firtina },                   // İlkbahar
            new[] { WeatherKind.Gunesli, WeatherKind.Sicak, WeatherKind.Bulutlu, WeatherKind.Firtina },                      // Yaz
            new[] { WeatherKind.Bulutlu, WeatherKind.Yagmurlu, WeatherKind.Gunesli, WeatherKind.Soguk, WeatherKind.Firtina } // Sonbahar
        };

        static readonly int[][] SeasonWeights =
        {
            new[] { 35, 30, 20, 10, 5 },
            new[] { 35, 30, 30, 5 },
            new[] { 40, 40, 10, 10 },
            new[] { 35, 35, 15, 10, 5 }
        };

        void Awake() { I = this; }

        void Start()
        {
            Current = Ciz();
            PlaniUzat();
        }

        void Update()
        {
            if (GameClock.I == null) return;
            float now = GameClock.I.TotalMinutes;
            if (plan.Count == 0) PlaniUzat();
            while (plan.Count > 0 && now >= plan[0].at)
            {
                var d = plan[0];
                plan.RemoveAt(0);
                Uygula(d.kind);
                PlaniUzat();
            }
        }

        /// <summary>Kayıttan yüklerken havayı geri koyar (plan yeniden yapılır).</summary>
        public void Restore(WeatherKind k)
        {
            Current = k;
            plan.Clear();
            PlaniUzat();
        }

        static WeatherKind Ciz()
        {
            int s = GameClock.I != null ? (int)GameClock.I.Season : 0;
            var kinds = SeasonKinds[s];
            var weights = SeasonWeights[s];
            int total = 0;
            foreach (int w in weights) total += w;
            int r = Random.Range(0, total);
            for (int i = 0; i < kinds.Length; i++)
            {
                if (r < weights[i]) return kinds[i];
                r -= weights[i];
            }
            return kinds[0];
        }

        void PlaniUzat()
        {
            if (GameClock.I == null) return;
            float now = GameClock.I.TotalMinutes;
            float son = plan.Count > 0 ? plan[plan.Count - 1].at : now;
            while (son < now + PlanUfku)
            {
                son += Random.Range(240f, 480f);
                plan.Add(new Degisim { at = son, kind = Ciz() });
            }
        }

        void Uygula(WeatherKind k)
        {
            Current = k;
            if (EventManager.I != null && GameBootstrap.I != null && GameBootstrap.I.Started)
            {
                string extra = "";
                if (DemandFactor > 1f) extra += $" · siparişler %{(DemandFactor - 1f) * 100f:0} artar";
                if (WeatherBonus > 0) extra += $" · kurye hava bonusu +{GameData.Ft(WeatherBonus)}";
                if (CustomerSurcharge > 0) extra += $" · müşteriye kötü hava ücreti +{GameData.Ft(CustomerSurcharge)}";
                float wb = CampaignManager.I != null ? CampaignManager.I.WeatherBoostPercent : 0f;
                if (wb > 0f) extra += $" · hava boostu %{wb * 100f:0}";
                // Güneş/bulut değişimi oyunu etkilemez: sessiz. Yağmur, kar gibi etkisi olanlar bildirilir.
                if (extra.Length > 0) EventManager.I.Notify($"Hava değişti: {Name}{extra}");
                else EventManager.I.NotifyQuiet($"Hava değişti: {Name}");
            }
        }

        // ------------------------------------------------------------------ tahmin

        /// <summary>Verilen andaki (planlanmış) hava.</summary>
        public WeatherKind At(float t)
        {
            var k = Current;
            foreach (var d in plan)
            {
                if (d.at > t) break;
                k = d.kind;
            }
            return k;
        }

        public static bool Kotu(WeatherKind k) =>
            k == WeatherKind.Yagmurlu || k == WeatherKind.Firtina || k == WeatherKind.Karli || k == WeatherKind.Soguk;

        public static string Ad(WeatherKind k)
        {
            switch (k)
            {
                case WeatherKind.Sicak: return "sıcak";
                case WeatherKind.Bulutlu: return "bulutlu";
                case WeatherKind.Yagmurlu: return "yağmurlu";
                case WeatherKind.Soguk: return "soğuk";
                case WeatherKind.Karli: return "karlı";
                case WeatherKind.Firtina: return "fırtına";
                default: return "güneşli";
            }
        }

        public static float TalepCarpani(WeatherKind k)
        {
            switch (k)
            {
                case WeatherKind.Karli: return 1.5f;
                case WeatherKind.Firtina: return 1.45f;
                case WeatherKind.Yagmurlu: return 1.35f;
                case WeatherKind.Soguk: return 1.2f;
                case WeatherKind.Sicak: return 1.1f;
                default: return 1f;
            }
        }

        /// <summary>
        /// Bir günün (mutlak gün, 06:00–24:00) hava tahmini: saat aralıklarıyla. Kötü hava aralıkları ve en kötüsü döner.
        /// Örnek: "sabah güneşli, 13:00'ten itibaren yağmurlu (siparişler +%35)".
        /// </summary>
        public string Tahmin(int gun, out WeatherKind enKotu, out float enKotuBas)
        {
            float bas = (gun - 1) * 24f * 60f + 6f * 60f, bit = gun * 24f * 60f;
            var parcalar = new List<string>();
            var kotu = WeatherKind.Gunesli;
            float kotuBas = bas;
            float enKotuCarpan = 0f;
            var k = At(bas);
            float parcaBas = bas;
            void Ekle(WeatherKind kind, float a, float b)
            {
                if (b - a < 30f) return;
                int ha = Mathf.FloorToInt((a % 1440f) / 60f), hb = Mathf.Clamp(Mathf.CeilToInt((b % 1440f == 0f ? 1440f : b % 1440f) / 60f), 0, 24);
                string ad = Ad(kind);
                if (parcalar.Count > 0 && parcalar[parcalar.Count - 1].EndsWith(ad)) return;
                parcalar.Add(a <= bas + 1f ? (b >= bit - 1f ? $"gün boyu {ad}" : $"{hb:00}:00'e kadar {ad}") : $"{ha:00}:00'ten itibaren {ad}");
                float c = TalepCarpani(kind) + (Kotu(kind) ? 0.01f : 0f);
                if (c > enKotuCarpan) { enKotuCarpan = c; kotu = kind; kotuBas = a; }
            }
            foreach (var d in plan)
            {
                if (d.at <= bas) continue;
                if (d.at >= bit) break;
                Ekle(k, parcaBas, d.at);
                k = d.kind; parcaBas = d.at;
            }
            Ekle(k, parcaBas, bit);
            enKotu = kotu;
            enKotuBas = kotuBas;
            return string.Join(", ", parcalar);
        }

        public bool IsRainy => Current == WeatherKind.Yagmurlu || Current == WeatherKind.Firtina;
        public bool IsCold => Current == WeatherKind.Soguk || Current == WeatherKind.Karli;
        public bool IsHot => Current == WeatherKind.Sicak;

        /// <summary>Kuryeye ödenen hava bonusu (Ft, bonus çarpanından önce).</summary>
        public int WeatherBonus
        {
            get
            {
                switch (Current)
                {
                    case WeatherKind.Sicak: return 150;
                    case WeatherKind.Soguk: return 150;
                    case WeatherKind.Yagmurlu: return 200;
                    case WeatherKind.Karli: return 300;
                    case WeatherKind.Firtina: return 350;
                    default: return 0;
                }
            }
        }

        /// <summary>Kötü havada insanlar dışarı çıkmak istemez, daha çok sipariş verir.</summary>
        public float DemandFactor
        {
            get
            {
                switch (Current)
                {
                    case WeatherKind.Karli: return 1.5f;
                    case WeatherKind.Firtina: return 1.45f;
                    case WeatherKind.Yagmurlu: return 1.35f;
                    case WeatherKind.Soguk: return 1.2f;
                    case WeatherKind.Sicak: return 1.1f;
                    default: return 1f;
                }
            }
        }

        /// <summary>Wolt gibi: kötü havada müşterinin teslimat ücretine eklenen "kötü hava ücreti" (Ft).</summary>
        public int CustomerSurcharge
        {
            get
            {
                switch (Current)
                {
                    case WeatherKind.Soguk: return 100;
                    case WeatherKind.Yagmurlu: return 150;
                    case WeatherKind.Karli: return 250;
                    case WeatherKind.Firtina: return 300;
                    default: return 0;
                }
            }
        }

        public float SpeedFactor(Courier c)
        {
            float f = 1f;
            if (c.V.exposed)
            {
                if (IsRainy) f *= c.Has(EquipmentKind.Yagmurluk) ? 1f : 0.82f;
                if (Current == WeatherKind.Karli) f *= c.Has(EquipmentKind.KisMontu) ? 0.90f : 0.70f;
                if (Current == WeatherKind.Soguk && !c.Has(EquipmentKind.KisMontu)) f *= 0.90f;
                if (Current == WeatherKind.Sicak && !c.Has(EquipmentKind.YazTisort)) f *= 0.92f;
            }
            else if (Current == WeatherKind.Karli || Current == WeatherKind.Firtina)
            {
                f *= 0.85f; // araba da kötü havada yavaşlar
            }
            return f;
        }

        public string Name
        {
            get
            {
                switch (Current)
                {
                    case WeatherKind.Sicak: return "Sıcak";
                    case WeatherKind.Bulutlu: return "Bulutlu";
                    case WeatherKind.Yagmurlu: return "Yağmurlu";
                    case WeatherKind.Soguk: return "Soğuk";
                    case WeatherKind.Karli: return "Karlı";
                    case WeatherKind.Firtina: return "Fırtına";
                    default: return "Güneşli";
                }
            }
        }
    }
}
