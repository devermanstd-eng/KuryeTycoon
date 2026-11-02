using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// SAHNEYE EKLENECEK TEK SCRIPT.
    /// Boş bir GameObject'e ekle ve Play'e bas; tüm sistemleri kendisi kurar.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap I { get; private set; }

        [Header("Başlangıç")]
        public long startingMoney = 300000;
        public VehicleKind[] starterCouriers = { VehicleKind.Bisiklet, VehicleKind.EScooter };

        public bool Started { get; private set; }

        void Awake()
        {
            I = this;
            CompanyProfile.ResetDefaults();
            EnsureCamera();

            var clock = Make<GameClock>("Saat");
            clock.Paused = true; // şirket kurulana kadar zaman durur

            Make<EconomyManager>("Ekonomi");
            Make<WeatherSystem>("Hava");
            Make<CityMap>("Şehir Haritası");
            Make<CourierManager>("Kurye Yönetimi");
            Make<OrderManager>("Siparişler");
            Make<CampaignManager>("Kampanyalar");
            Make<SupportManager>("Destek Ekibi");
            Make<EventManager>("Olaylar");
            Make<GameUI>("Arayüz");
        }

        T Make<T>(string objName) where T : Component
        {
            var go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        static void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
            cam.transform.position = new Vector3(16f, 10f, -10f);
            cam.transform.rotation = Quaternion.identity;
        }

        /// <summary>Kurulum ekranındaki "Başla" butonu çağırır.</summary>
        public void StartGame()
        {
            if (Started) return;

            EconomyManager.I.money = startingMoney;
            CityMap.I.Generate();
            CourierManager.I.Init();
            foreach (var v in starterCouriers) CourierManager.I.HireStarter(v);
            OrderManager.I.Init();
            CampaignManager.I.Init();
            SupportManager.I.Init();

            Started = true;
            GameClock.I.Paused = false;

            EventManager.I.Push(new GameEvent($"Hoş geldin, {CompanyProfile.Name}!",
                "Merkez ofisin haritanın sol alt köşesinde. 2 kuryen ve 2 sözleşmeli dükkanın var.\n\n" +
                "• Siparişler otomatik atanır; istersen Siparişler sekmesinden kendin ata.\n" +
                "• Zararına iş yapılmaz: teslimat ücreti her zaman kurye ücretini karşılar.\n" +
                "• Kuryeler paket başı + km başı kazanır. Gelen başvuruları İşe Alım sekmesinden kabul et.\n" +
                "• Kuryeler serbest çalışır: kendi düzenlerine ve yorgunluklarına göre çevrimiçi/çevrimdışı olurlar.\n" +
                "• Moralsiz ve yorgun kuryeler daha az paket alır, sipariş reddedebilir.\n" +
                "• Ekipman eksikse kuryenin kartında ne kaybettiğini görürsün (kışın mont, yazın tişört...).\n" +
                "• Kampanyalar sekmesinde boostlar ve haftalık hedef ödülü var (kârından ödenir).\n" +
                "• Destek sekmesinden maaşlı destek personeli al: vardiyalarında şikayetleri kendileri çözer.\n" +
                "• Dükkanlarla sözleşme yap, yeni bölgeler aç.\n" +
                "• Yıl sonunda vergi ödersin; yıl içinde de habersiz vergi ve harçlar çıkabilir, kasanda yedek tut.\n\n" +
                "Takvim gerçek: şirket 1 Mart Pazartesi kurulur, vergi yılı 31 Aralık'ta biter.\nKısayollar: Boşluk = duraklat, 1–5 = hız (1x, 2x, 4x, 8x, 16x)"));
        }
    }
}
