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
        [Tooltip("Başlangıç kasası: kurulum kredisinden kuruluş masrafları düşülünce kalan para")]
        public long familyLoanAmount = 1000000;
        /// <summary>
        /// Bankadan çekilen kurulum kredisinin tamamı (6 000 €, faizi FamilyLoan.faizOrani). Kuruluş masrafları (ofis depozitosu,
        /// ilk ekipman, uygulama) düşülünce kasada familyLoanAmount kalır. Eskiden kredi = kasa = 2 500 €'ydu ve iyi oyuncu 10. günde kapatıyordu.
        /// </summary>
        public long familyLoanTotal = Denge.AileBorcu;
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
            Make<KayitSistemi>("Kurye Kayıtları");
            Make<RobotFilo>("Robotlar");
            Make<KurumsalSiparisler>("Kurumsal Siparişler");
            Make<OrderManager>("Siparişler");
            Make<CampaignManager>("Kampanyalar");
            Make<GoalManager>("Haftalık Hedefler");
            Make<DarkStore>("Kendi Marketin");
            Make<CityEvents>("Şehir Etkinlikleri");
            Make<CrisisManager>("Krizler");
            Make<CourierMessages>("Kurye Mesajları");
            Make<HizmetSeviyeleri>("Hizmet Seviyeleri");
            Make<KargoAnlasmasi>("Kargo");
            Make<GunPlani>("Gün Planı");
            Make<Abonelik>("Abonelik");
            Make<RestoranReklam>("Restoran Reklamları");
            Make<Yatirim>("Yatırım");
            Make<IsletmeTeslimati>("İşletme Teslimatı");
            Make<Basarimlar>("Başarımlar");
            Make<SezonEtkinlikleri>("Sezon Etkinlikleri");
            Make<SanalMutfak>("Sanal Mutfak");
            Make<ArGe>("Ar-Ge");
            Make<Otomasyon>("Otomasyon");
            Make<DebugReport>("Hata Ayıklama");
            Make<UpgradeManager>("Geliştirmeler");
            Make<SupportManager>("Destek Ekibi");
            Make<OperationsManager>("Operasyon Sorumlusu");
            Make<RivalCompany>("Rakip Şirket");
            Make<RivalFleet>("Rakip Kuryeler");
            Make<FamilyLoan>("Hikaye - Kurulum Kredisi");
            Make<Milestones>("Kilometre Taşları");
            Make<Guide>("Rehber");
            Make<GameSounds>("Sesler");
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
                Itibar.Yukle(d.itibarToplam, d.itibarAdet, d.itibarGun, d.itibarOlay, Mathf.Clamp(d.reputation, 1f, 5f));
                Denge.Sifirla();
                GameClock.I.RestoreTime(d.totalMinutes, d.startDay);
                Difficulty.Level = (DifficultyLevel)Mathf.Clamp(d.difficulty, 0, 2);   // sistemler kurulmadan önce
                OrderHeat.Reset(); DukkanKari.Reset();
                Senaryo.Secili = (Senaryo.Tur)Mathf.Clamp(d.senaryo, 0, System.Enum.GetValues(typeof(Senaryo.Tur)).Length - 1);
                Senaryo.Durum = d.senaryoDurum;
                Senaryo.GunlukTohum = d.gunlukTohum;
                Anlatici.Secili = (Anlatici.Tur)Mathf.Clamp(d.anlatici, 0, 2);
                Anlatici.Sifirla();
                Marka.AktifSehir = Mathf.Clamp(d.sehirNo, 0, Marka.Sehirler.Length - 1);

                GenerateWorld(d.mapSeed);
                CourierManager.I.Init();
                OrderManager.I.Init();
                RobotFilo.I.Init();
                KurumsalSiparisler.I.Init();
                CampaignManager.I.Init();
                SupportManager.I.Init();
                OperationsManager.I.Init();
                RivalCompany.I.Init();
                GoalManager.I.Init();
                DarkStore.I.Init();
                CityEvents.I.Init();
                CrisisManager.I.Init();
                CourierMessages.I.Init();
                HizmetSeviyeleri.I.Init();
                KargoAnlasmasi.I.Init();
                GunPlani.I.Init();
                Otomasyon.I.Init();
                Abonelik.I.Init();
                RestoranReklam.I.Init();
                Yatirim.I.Init();
                DukkanSadakati.Init();
                IsletmeTeslimati.I.Init();
                SezonEtkinlikleri.I.Init();
                SanalMutfak.I.Init();
                ArGe.I.Init();
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
            Otomasyon.SenYokken(d.kayitUtc);   // oyun kapalıyken kazanç (incremental)
            return true;
        }

        /// <summary>Yeni oyunun başlangıç kasası: zorluğa göre (Difficulty.StartCash) + marka sermayesi.</summary>
        public long BaslangicKasasi(DifficultyLevel d) =>
            (long)(Mathf.Round(familyLoanAmount * Difficulty.StartCashFor(d) / 1000f) * 1000f) + Marka.BaslangicEki;

        /// <summary>Marka (prestij): hazır sistemler ve büyük şehirde güçlü rakipler.</summary>
        static void MarkaBaslangici()
        {
            int oto = Marka.HazirOtomasyon;
            if (oto > 0 && UpgradeManager.I != null) UpgradeManager.I.SetLevel(UpgradeKind.Otomasyon, oto);
            int sehir = Marka.AktifSehir;
            if (sehir > 0 && RivalCompany.I != null)
                foreach (var r in RivalCompany.I.Rakipler)
                {
                    r.couriers += 2 * sehir;
                    r.reputation = Mathf.Min(4.4f, r.reputation + 0.08f * sehir);
                    r.money += 600000L * sehir;
                }
        }

        /// <summary>Kurulum ekranındaki "Başla" butonu çağırır.</summary>
        public void StartGame()
        {
            if (Started) return;

            Denge.Sifirla();
            Anlatici.Sifirla();
            Senaryo.Sifirla();
            GelisimAgaci.Sifirla();
            // Marka: satılan şirket sayısı kadar büyük şehre geçilir (sadece Klasik oyunda)
            Marka.AktifSehir = Senaryo.Secili == Senaryo.Tur.Klasik ? Marka.SiradakiSehir : 0;
            EconomyManager.I.money = BaslangicKasasi(Difficulty.Level);
            EconomyManager.I.InitCashMarkers();
            OrderHeat.Reset(); DukkanKari.Reset();
            Telemetry.Reset();
            GenerateWorld(Senaryo.HaritaTohumu(TestModu.TestHaritaTohumu ?? Random.Range(1, int.MaxValue)));   // günlük meydan okumada bugünün şehri; testte sabit tohum
            CourierManager.I.Init();
            foreach (var v in starterCouriers) CourierManager.I.HireStarter(v).StartShift();
            OrderManager.I.Init();
            RobotFilo.I.Init();
            KurumsalSiparisler.I.Init();
            CampaignManager.I.Init();
            SupportManager.I.Init();
            OperationsManager.I.Init();
            RivalCompany.I.Init();
            GoalManager.I.Init();
            DarkStore.I.Init();
            CityEvents.I.Init();
            CrisisManager.I.Init();
            CourierMessages.I.Init();
            HizmetSeviyeleri.I.Init();
            KargoAnlasmasi.I.Init();
            GunPlani.I.Init();
            Otomasyon.I.Init();
            Abonelik.I.Init();
            RestoranReklam.I.Init();
            Yatirim.I.Init();
            DukkanSadakati.Init();
            IsletmeTeslimati.I.Init();
            SezonEtkinlikleri.I.Init();
            SanalMutfak.I.Init();
            ArGe.I.Init();
            long borc = Senaryo.AileBorcu(System.Math.Max(familyLoanTotal, familyLoanAmount));
            if (borc <= 0) FamilyLoan.I.BorcsuzBasla();   // serbest mod
            else
            {
                FamilyLoan.I.principal = borc;
                FamilyLoan.I.Init();
            }
            Senaryo.Baslat();   // senaryonun başlangıç ayarları (sermaye, güçlü rakip...)
            MarkaBaslangici();
            Guide.I.InitNew();
            SaveSystem.I.Init();

            Started = true;
            GameClock.I.Paused = false;

            // Kısa tutuldu: menüleri ilk gün rehberi (Tutorial.cs) tek tek gösterir, ayrıntılar pencerelerdeki "Ayrıntı"da.
            EventManager.I.Push(new GameEvent($"Hoş geldin, {CompanyProfile.Name}!",
                (Marka.AktifSehir > 0 ? $"Yeni şehir: {Marka.SehirAdi}. Önceki şirketlerinin tecrübesi seninle: {Marka.Ozet}. " +
                                        "Bu şehir daha büyük (daha çok sipariş) ama rakipler de daha güçlü.\n\n" : "") +
                "2 kuryen ve 2 sözleşmeli dükkanın var; siparişler otomatik atanır.\n" +
                (Senaryo.Secili == Senaryo.Tur.Klasik ? "Amacın: kâr et, kredi taksitlerini öde, rakiplerini geç.\n\n"
                                                       : $"Senaryo: {Senaryo.Adlar[(int)Senaryo.Secili]}. {Senaryo.Aciklamalar[(int)Senaryo.Secili]}\n\n") +
                "Kartı kapatınca kısa bir rehber başlayacak.\n\n" +
                "Boşluk = duraklat · 1–8 = hız · Esc = menü. Oyun her gece kendini kaydeder."));
        }
    }
}
