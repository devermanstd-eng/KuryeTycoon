using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Oyunun giriş noktası. Sahnede yoksa Play'e basınca kendiliğinden oluşur;
    /// istersen sahneye boş bir GameObject'e ekleyip Inspector'dan ayarlarını da değiştirebilirsin.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        static GameBootstrap instance;
        public static GameBootstrap I
        {
            get { if (instance == null) instance = FindAnyObjectByType<GameBootstrap>(); return instance; }
            private set { instance = value; }
        }

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
            Make<StatsHistory>("İstatistik");
            Make<WeatherSystem>("Hava");
            Make<CityMap>("Şehir Haritası");
            Make<City3D>("3D Şehir");
            Make<Traffic>("Trafik");
            Make<WeatherFx>("Hava Efektleri");
            Make<MapCamera>("Harita Kamerası");
            Make<CourierManager>("Kurye Yönetimi");
            Make<OrderManager>("Siparişler");
            Make<CampaignManager>("Kampanyalar");
            Make<GoalManager>("Haftalık Hedefler");
            Make<UpgradeManager>("Geliştirmeler");
            Make<SupportManager>("Destek Ekibi");
            Make<OperationsManager>("Operasyon Sorumlusu");
            Make<RivalCompany>("Rakip Şirket");
            Make<RivalFleet>("Rakip Kuryeler");
            Make<FamilyLoan>("Hikaye - Aile Borcu");
            Make<Milestones>("Kilometre Taşları");
            Make<SaveSystem>("Kayıt");
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

        /// <summary>Haritanın üretildiği tohum: kayda yazılır, yüklemede aynı harita ve 3D şehir çıkar.</summary>
        public int MapSeed { get; private set; }

        /// <summary>Son yükleme denemesi hata verdiyse nedeni (kurulum ekranında gösterilir).</summary>
        public static string LastLoadError = "";

        /// <summary>Harita ve 3D şehri verilen tohumla üretir; oyunun geri kalan rastgeleliğini etkilemez.</summary>
        void GenerateWorld(int seed)
        {
            MapSeed = seed;
            var old = Random.state;
            Random.InitState(seed);
            CityMap.I.Generate();
            City3D.I.Build();
            Traffic.I.Build();
            Random.state = old;
        }

        /// <summary>
        /// Kurulum ekranındaki "Kayıtlı oyuna devam et" butonu çağırır. Sistemler yeni oyundaki sırayla kurulur,
        /// sonra kayıttaki durum üstüne yazılır. Hata olursa temiz bir kurulum ekranına dönülür.
        /// </summary>
        public bool LoadGame()
        {
            if (Started) return false;
            var d = SaveSystem.ReadSave();
            if (d == null)
            {
                LastLoadError = "Kayıt dosyası okunamadı.";
                return false;
            }
            try
            {
                CompanyProfile.Name = string.IsNullOrEmpty(d.companyName) ? "Hızlı Kurye" : d.companyName;
                CompanyProfile.Primary = d.primary;
                CompanyProfile.Secondary = d.secondary;
                CompanyProfile.Reputation = Mathf.Clamp(d.reputation, 1f, 5f);
                GameClock.I.RestoreTime(d.totalMinutes, d.startDay);

                GenerateWorld(d.mapSeed);
                CourierManager.I.Init();
                OrderManager.I.Init();
                CampaignManager.I.Init();
                SupportManager.I.Init();
                OperationsManager.I.Init();
                RivalCompany.I.Init();
                GoalManager.I.Init();
                FamilyLoan.I.principal = d.loan != null && d.loan.principal > 0 ? d.loan.principal : familyLoanAmount;
                FamilyLoan.I.InitRestored(d.loan);
                SaveSystem.ApplyAfterInit(d);
                SaveSystem.I.Init();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Kayıt yüklenemedi: {ex}");
                LastLoadError = $"Kayıt yüklenemedi: {ex.Message}";
                Restart();
                return false;
            }

            LastLoadError = "";
            Started = true;
            GameClock.I.SpeedMultiplier = 1f;
            GameClock.I.Paused = false;
            EventManager.I.Notify($"Kayıtlı oyun yüklendi ({d.gameDate}). Kayıt anında yolda olan siparişler iptal oldu, kuryeler merkez ofiste.");
            return true;
        }

        /// <summary>Kurulum ekranındaki "Başla" butonu çağırır.</summary>
        public void StartGame()
        {
            if (Started) return;

            EconomyManager.I.money = familyLoanAmount;
            EconomyManager.I.InitCashMarkers();
            GenerateWorld(Random.Range(1, int.MaxValue));
            CourierManager.I.Init();
            foreach (var v in starterCouriers) CourierManager.I.HireStarter(v).StartShift();
            OrderManager.I.Init();
            CampaignManager.I.Init();
            SupportManager.I.Init();
            OperationsManager.I.Init();
            RivalCompany.I.Init();
            GoalManager.I.Init();
            FamilyLoan.I.principal = familyLoanAmount;
            FamilyLoan.I.Init();
            SaveSystem.I.Init();

            Started = true;
            GameClock.I.Paused = false;

            EventManager.I.Push(new GameEvent($"Hoş geldin, {CompanyProfile.Name}!",
                "Merkez ofisin Merkez Mahalle'deki Kossuth Meydanı'nda. 2 kuryen işbaşında ve 2 sözleşmeli dükkanın var.\n" +
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
                "Takvim gerçek: şirket 1 Mart Pazartesi kurulur, vergi yılı 31 Aralık'ta biter.\nKısayollar: Boşluk = duraklat, 1–8 = hız (1x … 128x), Esc = menü (kaydet / çık)\n" +
                "Oyun her gece otomatik kaydedilir; kurulum ekranından kaldığın yerden devam edebilirsin.\n" +
                "Harita: fare tekerleği = yakınlaş (yakında şehir 3D olur), sürükle veya WASD/oklar = kaydır, sağ tuşla sürükle veya Q/E = döndür, 0 = tüm harita"));
        }
    }
}
