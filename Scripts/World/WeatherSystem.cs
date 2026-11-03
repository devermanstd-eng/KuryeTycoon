using UnityEngine;

namespace KuryeTycoon
{
    public enum WeatherKind { Gunesli, Sicak, Bulutlu, Yagmurlu, Soguk, Karli, Firtina }

    /// <summary>Mevsime göre hava durumu. Hava; bonusu, kurye hızını ve sipariş talebini etkiler.</summary>
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem I { get; private set; }

        public WeatherKind Current { get; private set; } = WeatherKind.Gunesli;
        float nextChange;

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

        void Start() { Roll(); }

        void Update()
        {
            if (GameClock.I != null && GameClock.I.TotalMinutes >= nextChange) Roll();
        }

        void Roll()
        {
            int s = (int)GameClock.I.Season;
            var kinds = SeasonKinds[s];
            var weights = SeasonWeights[s];
            int total = 0;
            foreach (int w in weights) total += w;
            int r = Random.Range(0, total);
            for (int i = 0; i < kinds.Length; i++)
            {
                if (r < weights[i]) { Current = kinds[i]; break; }
                r -= weights[i];
            }
            nextChange = GameClock.I.TotalMinutes + Random.Range(240f, 480f);
            if (EventManager.I != null && GameBootstrap.I != null && GameBootstrap.I.Started)
            {
                string extra = "";
                if (DemandFactor > 1f) extra += $" · siparişler %{(DemandFactor - 1f) * 100f:0} artar";
                if (WeatherBonus > 0) extra += $" · kurye hava bonusu +{GameData.Ft(WeatherBonus)}";
                if (CustomerSurcharge > 0) extra += $" · müşteriye kötü hava ücreti +{GameData.Ft(CustomerSurcharge)}";
                float wb = CampaignManager.I != null ? CampaignManager.I.WeatherBoostPercent : 0f;
                if (wb > 0f) extra += $" · hava boostu %{wb * 100f:0}";
                EventManager.I.Notify($"Hava değişti: {Name}{extra}");
            }
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
