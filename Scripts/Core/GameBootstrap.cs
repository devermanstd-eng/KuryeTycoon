using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Oyunun giriş noktası. Sahnede yoksa Play'e basınca kendiliğinden oluşur;
    /// istersen sahneye boş bir GameObject'e ekleyip Inspector'dan ayarlarını da değiştirebilirsin.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap I { get; private set; }

        /// <summary>Herhangi bir sahnede (boş sahne dahil) Play'e basınca oyunu otomatik kurar.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            EnsureExists();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => EnsureExists();
        }

        static void EnsureExists()
        {
            if (I != null || FindAnyObjectByType<GameBootstrap>() != null) return;
            new GameObject("Oyun").AddComponent<GameBootstrap>();
        }

        /// <summary>Yeni oyun: tüm sistemleri silip baştan kurar (sahneyi yeniden yüklemeye gerek yok).</summary>
        public static void Restart()
        {
            var old = I;
            I = null;
            if (old != null) Destroy(old.gameObject);
            new GameObject("Oyun").AddComponent<GameBootstrap>();
        }

        [Header("Başlangıç")]
        // Not: alan adı bilerek değiştirildi (eski "startingMoney"). Eski sahnelerde kayıtlı 300 000 değeri
        // artık okunmaz; herkes güncel varsayılanla (1 000 000 Ft) başlar.
        [Tooltip("Ailenden aldığın borç = başlangıç kasası")]
        public long familyLoanAmount = 1000000;
        public VehicleKind[] starterCouriers = { VehicleKind.Bisiklet, VehicleKind.EScooter };

        public bool Started { get; private set; }

        void Awake()
        {
            I = this;
            CompanyProfile.ResetDefaults();
            GameData.LoadCurrency();
            EnsureCamera();

            var clock = Make<GameClock>("Saat");
            clock.Paused = true; // şirket kurulana kadar zaman durur

            Make<EconomyManager>("Ekonomi");
            Make<WeatherSystem>("Hava");
            Make<CityMap>("Şehir Haritası");
            Make<City3D>("3D Şehir");
            Make<MapCamera>("Harita Kamerası");
            Make<CourierManager>("Kurye Yönetimi");
            Make<OrderManager>("Siparişler");
            Make<CampaignManager>("Kampanyalar");
            Make<UpgradeManager>("Geliştirmeler");
            Make<SupportManager>("Destek Ekibi");
            Make<RivalCompany>("Rakip Şirket");
            Make<RivalFleet>("Rakip Kuryeler");
            Make<FamilyLoan>("Hikaye - Aile Borcu");
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

            EconomyManager.I.money = familyLoanAmount;
            EconomyManager.I.InitCashMarkers();
            CityMap.I.Generate();
            City3D.I.Build();
            CourierManager.I.Init();
            foreach (var v in starterCouriers) CourierManager.I.HireStarter(v);
            OrderManager.I.Init();
            CampaignManager.I.Init();
            SupportManager.I.Init();
            RivalCompany.I.Init();
            FamilyLoan.I.principal = familyLoanAmount;
            FamilyLoan.I.Init();

            Started = true;
            GameClock.I.Paused = false;

            EventManager.I.Push(new GameEvent($"Hoş geldin, {CompanyProfile.Name}!",
                "Merkez ofisin haritanın sol alt köşesinde. 2 kuryen ve 2 sözleşmeli dükkanın var.\n" +
                "Aile borcunu ve taksitleri üst barda ve Finans sekmesinde görürsün.\n\n" +
                "• Siparişler otomatik atanır; istersen Siparişler sekmesinden kendin ata.\n" +
                "• Zararına iş yapılmaz: teslimat ücreti her zaman kurye ücretini karşılar.\n" +
                "• Kuryeler paket başı + km başı kazanır. Gelen başvuruları İşe Alım sekmesinden kabul et.\n" +
                "• Kuryeler serbest çalışır: kendi düzenlerine ve yorgunluklarına göre çevrimiçi/çevrimdışı olurlar.\n" +
                "• Moralsiz ve yorgun kuryeler daha az paket alır, sipariş reddedebilir.\n" +
                "• Ekipman eksikse kuryenin kartında ne kaybettiğini görürsün (kışın mont, yazın tişört...).\n" +
                "• Kampanyalar sekmesinde boostlar ve haftalık hedef ödülü var (kârından ödenir).\n" +
                "• Destek sekmesinden maaşlı destek personeli al: vardiyalarında şikayetleri kendileri çözer.\n" +
                $"• Platform paketin {UpgradeManager.I.CourierCap} kurye hesabı kaldırıyor. Geliştirmeler'den paketi yükselt ve kalıcı iyileştirmeler al.\n" +
                "• Dükkanlarla sözleşme yap, yeni bölgeler aç.\n" +
                "• Yıl sonunda vergi ödersin; yıl içinde de habersiz vergi ve harçlar çıkabilir, kasanda yedek tut.\n\n" +
                "Takvim gerçek: şirket 1 Mart Pazartesi kurulur, vergi yılı 31 Aralık'ta biter.\nKısayollar: Boşluk = duraklat, 1–8 = hız (1x … 128x)\n" +
                "Harita: fare tekerleği = yakınlaş (yakında şehir 3D olur), sürükle veya WASD/oklar = kaydır, sağ tuşla sürükle veya Q/E = döndür, 0 = tüm harita"));
        }
    }
}
