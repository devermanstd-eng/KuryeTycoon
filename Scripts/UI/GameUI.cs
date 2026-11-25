using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Prototip arayüzü (Unity IMGUI). Kurulum ekranı, üst bar, sol panel sekmeleri,
    /// haber akışı ve olay kartları. Hiçbir Canvas / prefab gerektirmez.
    /// Arayüzün tamamı UI Toolkit'te (Scripts/UI/Arayuz, Resources/UI/Arayuz), kurulum ekranı dahil (Arayuz/UiKurulum.cs).
    /// Haritanın üstündeki yazılar (etiketler, uçan yazılar, şehir etkinliği, lejant) 25.11'de UI Toolkit'e taşındı
    /// (Arayuz/UiHaritaEtiketleri.cs), seçili bina işareti 3D seçim halkası oldu. OnGUI'de yalnız klavye/fare girişi kaldı;
    /// çizim yok. Yeni çizim OnGUI'ye yazılmaz.
    /// </summary>
    public partial class GameUI : MonoBehaviour
    {
        enum Tab { Siparisler, Kuryeler, IseAlim, Destek, Gelistirmeler, Dukkanlar, Ekipman, Kampanyalar, Finans, Ayarlar, Hedefler, Mesajlar, Market, Istatistik, Analitik, Basarimlar, Mutfak, ArGe, Bolgeler, Efsaneler }
        static readonly string[] BaseTabNames = { "Siparişler", "Kuryeler", "Kayıtlar", "Destek", "Geliştirmeler", "Dükkanlar", "Ekipman", "Kampanyalar", "Finans", "Ayarlar", "Haftalık Hedefler",
                                                  "Müşteri Yorumları", "Kendi Marketin", "İstatistikler", "Analitik", "Başarımlar", "Sanal Mutfak", "Ar-Ge",
                                                  "Bölge Müdürleri", "Efsane Kuryeler" };

        public float panelWidth = 560f;     // açılan pencerenin genişliği (kimlik kartları ve kurye tablosu sığsın)
        const float TopBar = 60f;
        const float DockW = 76f;            // soldaki simge çubuğu
        bool panelOpen;                     // simgeye tıklanınca açılan pencere

        static readonly Color[] Presets =
        {
            new Color(0.00f, 0.62f, 0.87f), new Color(0.89f, 0.20f, 0.20f), new Color(1.00f, 0.50f, 0.10f),
            new Color(1.00f, 0.80f, 0.10f), new Color(0.20f, 0.72f, 0.35f), new Color(0.10f, 0.60f, 0.60f),
            new Color(0.55f, 0.30f, 0.85f), new Color(0.95f, 0.35f, 0.60f), new Color(0.10f, 0.10f, 0.12f),
            new Color(0.97f, 0.97f, 0.97f)
        };

        Tab tab;
        Vector2 scroll;
        float uiScale = 1f;
        bool modalFree = true;
        System.Action deferred; // listeleri değiştiren işlemler bir sonraki Update'te yapılır

        // Kurulum ekranı
        string companyName = "Swift Courier";
        Color primary, secondary;
        int editingColor;

        Courier selectedCourier;
        Courier popupCourier;   // haritada tıklanan kurye: sağda detay paneli
        Courier hoverCourier;   // fare üstündeki kurye (ismi sarı yanar)

        GUIStyle title, header, label, small, warn, note, box, button, smallButton, bigButton, toggle, textField,
                 worldLabel, barTitle, barText, logoLetter;
        bool stylesReady;

        static string Ft(long v) => GameData.Ft(v);

        void OnDestroy() { EventManager.Notified -= OnNotified; }

        void Start()
        {
            EventManager.Notified -= OnNotified;
            EventManager.Notified += OnNotified;
            primary = CompanyProfile.Primary;
            secondary = CompanyProfile.Secondary;
        }

        void Update()
        {
            if (deferred != null)
            {
                var a = deferred;
                deferred = null;
                a();
            }
            KurulumGuncelle();   // oyun başlamadan: kurulum ekranı (Arayuz/UiKurulum.cs)
            UiGuncelle();   // UI Toolkit arayüzü (Arayuz/UiKok.cs)
            // Büyük kurye kartı da ortada, perdeli bir kutu (UI Toolkit). Test modunda telefon ve bina kartı çekilirken de IMGUI çizmez.
            OrtaKutuAcik = UiAktif && (UiAcikModal != UiModalTur.Yok || BigCardOpen || testKartCekimi);
        }

        void LateUpdate()
        {
            UpdateMenuGroups();   // yeni açılan sekmeler, son bakılan sekme
            UpdateTutorial();
            // Harita kamerası sol panel ve üst bar dışındaki alana çizer (yakınlaştırma/kaydırma MapCamera'da).
            // Pencere açıkken harita pencerenin sağına kayar (Ayarlar'dan kapatılırsa pencere haritanın üstünde yüzer).
            if (MapCamera.I == null) return;
            UpdateMapViewport();
            UiHaritaKatmaniYenile();   // hava, gece/gündüz ve harita katmanı: haritanın üstünde, arayüzün altında (Arayuz/UiHaritaKatmani.cs)
        }

        // =====================================================================
        void OnGUI()
        {
            // Yalnız giriş: klavye kısayolları, Esc, haritada sürükleme / yakınlaştırma / tıklama. Hiçbir şey çizilmez: arayüz ve
            // haritanın üstündeki yazılar UI Toolkit'te. (Girişin Input System'e taşınması ayrı iş: Unity'de denenerek yapılmalı.)
            // Ekran yüksekliğine göre ölçek × oyuncunun seçtiği arayüz boyutu (Ayarlar > Arayüz): fare konumu GUI biriminde gelir
            uiScale = UiOlcek();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));
            float sw = Screen.width / uiScale;
            float sh = Screen.height / uiScale;
            InitStyles();   // rehber kartının yeri (TutorialRect) IMGUI stiliyle ölçülüyor

            if (GameBootstrap.I == null) return;
            if (!GameBootstrap.I.Started) return;   // kurulum ekranı UI Toolkit'te (Arayuz/UiKurulum.cs)
            if (!UiAktif) return;                   // arayüz kurulamadıysa nedeni Unity Console'da ve hata raporunda (F9)
            if (HandlePhotoMode(sw, sh)) return;    // F10: arayüz gizli, yalnızca şehir

            if (ConfirmOpen) HandleConfirmKey();
            else if (BigCardOpen) HandleBigCardKey();   // büyük kurye kartı: Esc kapatır (Arayuz/UiBuyukKart.cs)
            else if (EventManager.I.Current == null) HandleMenuKey();
            modalFree = EventManager.I.Current == null && !pauseMenuOpen && !ConfirmOpen && !BigCardOpen;
            if (modalFree) HandleKeys();
            // Gelişim ağacı tam ekran: harita fareyi almaz
            if (AgacGorunur) return;
            if (!pauseMenuOpen && !ConfirmOpen && !BigCardOpen) HandleMapMouse(sw, sh);
        }

        /// <summary>Ortada perdeli bir kutu açık (olay kartı, Esc menüsü, onay, büyük kurye kartı) ya da test kart çekimi.</summary>
        public static bool OrtaKutuAcik { get; private set; }

        // ---------------------------------------------------------------- ESC menüsü (kaydet / ana menü / çık)
        bool pauseMenuOpen, menuWasPaused, confirmMainMenu;

        void HandleMenuKey()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode != KeyCode.Escape) return;
            e.Use();
            // Önce bina kartı, kurye telefonu ve açık pencere kapanır, sonra Esc menüsü açılır
            if (InfoOpen && !pauseMenuOpen) { CloseInfo(); return; }
            if (popupCourier != null && !pauseMenuOpen) { popupCourier = null; return; }
            if (panelOpen && !pauseMenuOpen) { panelOpen = false; return; }
            SetPauseMenu(!pauseMenuOpen);
        }

        void SetPauseMenu(bool open)
        {
            if (open == pauseMenuOpen) return;
            pauseMenuOpen = open;
            confirmMainMenu = false;
            var clk = GameClock.I;
            if (clk == null) return;
            if (open) { menuWasPaused = clk.Paused; clk.Paused = true; }
            else clk.Paused = menuWasPaused;
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------------------------------------------------------- kurulum
        SaveData setupSave;                 // herhangi bir yuvada kayıt var mı (pencere boyu için)
        readonly SaveData[] slotSaves = new SaveData[SaveSystem.SlotCount];
        bool setupSaveChecked;
        int newGameSlot = -1, confirmDeleteSlot = -1, confirmOldSlot = -1;

        // ---------------------------------------------------------------- fotoğraf modu (F10)
        bool fotoModu;
        float fotoIpucuBitis;

        /// <summary>
        /// F10 arayüzü gizler (ekran görüntüsü, fragman): harita kısayolları ve fare çalışmaya devam eder. İlk 3 saniye altta ipucu.
        /// F10 ya da Esc geri getirir; karar kartı çıkarsa kendiliğinden kapanır. true dönerse arayüz çizilmez.
        /// </summary>
        bool HandlePhotoMode(float sw, float sh)
        {
            var e = Event.current;
            bool busy = pauseMenuOpen || ConfirmOpen || EventManager.I.Current != null;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.F10 || (fotoModu && e.keyCode == KeyCode.Escape)) && (fotoModu || !busy))
            {
                fotoModu = !fotoModu;
                fotoIpucuBitis = Time.unscaledTime + 3f;
                e.Use();
            }
            if (fotoModu && busy) fotoModu = false;
            if (!fotoModu) return false;
            modalFree = true;
            HandleKeys();
            HandleMapMouse(sw, sh);
            return true;   // ipucu yazısı UI Toolkit'te (UiHaritaEtiketleri: fotoIpucu)
        }

        // ---------------------------------------------------------------- klavye
        void HandleKeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;
            if (UiYaziOdakta) return;   // UI Toolkit yazı kutusuna yazılıyor
            var clk = GameClock.I;
            switch (e.keyCode)
            {
                case KeyCode.Space: clk.Paused = !clk.Paused; e.Use(); break;
                case KeyCode.Alpha1: SetSpeed(1f); e.Use(); break;
                case KeyCode.Alpha2: SetSpeed(2f); e.Use(); break;
                case KeyCode.Alpha3: SetSpeed(4f); e.Use(); break;
                case KeyCode.Alpha4: SetSpeed(8f); e.Use(); break;
                case KeyCode.Alpha5: SetSpeed(16f); e.Use(); break;
                case KeyCode.Alpha6: SetSpeed(32f); e.Use(); break;
                case KeyCode.Alpha7: SetSpeed(64f); e.Use(); break;
                case KeyCode.Alpha8: SetSpeed(128f); e.Use(); break;
            }
            var mc = MapCamera.I;
            if (mc == null) return;
            switch (e.keyCode)
            {
                case KeyCode.LeftArrow: case KeyCode.A: mc.Pan(Vector2.left); e.Use(); break;
                case KeyCode.RightArrow: case KeyCode.D: mc.Pan(Vector2.right); e.Use(); break;
                case KeyCode.UpArrow: case KeyCode.W: mc.Pan(Vector2.up); e.Use(); break;
                case KeyCode.DownArrow: case KeyCode.S: mc.Pan(Vector2.down); e.Use(); break;
                case KeyCode.Equals: case KeyCode.Plus: case KeyCode.KeypadPlus: mc.ZoomStep(1.5f); e.Use(); break;
                case KeyCode.Minus: case KeyCode.KeypadMinus: mc.ZoomStep(-1.5f); e.Use(); break;
                case KeyCode.Q: mc.Rotate(-45f); e.Use(); break;
                case KeyCode.E: mc.Rotate(45f); e.Use(); break;
                case KeyCode.N: mc.ResetRotation(); e.Use(); break;
                case KeyCode.M: mc.ToggleFlatMode(); e.Use(); break;
                case KeyCode.H: CycleHeat(); e.Use(); break;
                case KeyCode.Alpha0: case KeyCode.Keypad0: case KeyCode.Home: mc.ResetView(); e.Use(); break;
            }
        }

        static void SetSpeed(float s)
        {
            GameClock.I.SpeedMultiplier = s;
            GameClock.I.Paused = false;
        }

        // ---------------------------------------------------------------- harita etiketleri (çizim: Arayuz/UiHaritaEtiketleri.cs)

        const string LandmarkKey = "KuryeTycoon.HaritaEtiketleri";
        static bool ShowLandmarks
        {
            get { return PlayerPrefs.GetInt(LandmarkKey, 1) == 1; }
            set { PlayerPrefs.SetInt(LandmarkKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        bool legendOpen;      // harita araç çubuğundaki "Lejant"; kutu UI Toolkit'te (UiHaritaEtiketleri.LejantKur)
        const int LegendItems = 14;

        Rect LegendRect(float sh)
        {
            float w = 250f, h = LegendItems * 19f + 34f;
            float x = panelOpen ? WindowRect(sh).xMax + 10f : DockW + 10f;
            return new Rect(x, sh - 52f - h, w, h);
        }

        static float BuildingTop(Vector2Int tile) => City3D.I != null ? City3D.I.HeightAt(tile) : 0f;

        static Vector3 CourierLabelOffset(int stackIndex) =>
            MapCamera.LabelOffset(0.55f + 0.4f * stackIndex, 0.6f + 0.3f * stackIndex);

        Vector2 GuiToScreenPx(Vector2 m) => new Vector2(m.x * uiScale, Screen.height - m.y * uiScale);

        bool dragging, dragMoved;
        int dragButton;
        Vector2 dragStart, dragLast;

        // ---------------------------------------------------------------- haritada kurye seçme
        Vector2 ToGui(Camera cam, Vector3 world)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            return new Vector2(sp.x / uiScale, (Screen.height - sp.y) / uiScale);
        }

        /// <summary>Fare konumundaki kurye (simgesi ya da isim etiketi).</summary>
        Courier CourierAt(Vector2 m)
        {
            var cam = Camera.main;
            if (cam == null || CourierManager.I == null) return null;
            Courier best = null;
            float bestD = float.MaxValue;
            var stack = new Dictionary<Vector2Int, int>();
            foreach (var c in CourierManager.I.Couriers)
            {
                var key = Vector2Int.RoundToInt(c.Position);
                stack.TryGetValue(key, out int n);
                stack[key] = n + 1;
                Vector2 sp = ToGui(cam, CityMap.ToWorld(c.Position));
                Vector2 lp = ToGui(cam, CityMap.ToWorld(c.Position) + CourierLabelOffset(n));
                Vector2 mp = ToGui(cam, CityMap.ToWorld(c.Position) + MapCamera.LabelOffset(0f, 0.18f)); // karakterin gövdesi
                float d = float.MaxValue;
                float ds = Mathf.Min(Vector2.Distance(m, sp), Vector2.Distance(m, mp));
                if (ds < 20f) d = ds;
                if (Mathf.Abs(m.x - lp.x) < 55f && Mathf.Abs(m.y - lp.y) < 10f) d = Mathf.Min(d, Mathf.Abs(m.y - lp.y));
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        void HandleMapMouse(float sw, float sh)
        {
            var e = Event.current;
            Vector2 m = e.mousePosition;
            bool overMap = m.x > MapLeftGui && m.y > TopBar &&
                           !(panelOpen && WindowRect(sh).Contains(m)) &&
                           !(TutorialVisible && TutorialRect(sw, sh).Contains(m)) &&
                           !(legendOpen && LegendRect(sh).Contains(m)) &&
                           !(TrackerVisible && GoalTrackerRect().Contains(m)) &&
                           !ToastAt(m, sw) &&
                           !(heatMenuOpen && HeatMenuRect(sh).Contains(m)) &&
                           !(!UiAktif && UyariGorunur && UyariRect(sw).Contains(m)) &&
                           !new Rect(DockW + 10f, sh - 44f, 560f, 30f).Contains(m) &&
                           !UiUzerinde(m);   // UI Toolkit elemanı (üst çubuk, menü, pencere, olaylar, telefon, bina kartı...)
            // Haritaya tıklayınca açık katman menüsü kapanır (UI Toolkit menüsü dışarı tıklamayı göremez)
            if (UiAktif && heatMenuOpen && e.type == EventType.MouseDown && overMap) heatMenuOpen = false;
            if (e.type == EventType.Repaint || e.type == EventType.MouseMove || e.type == EventType.MouseDown)
                hoverCourier = overMap && modalFree ? CourierAt(m) : null;

            // Yakınlaştırma: fare tekerleği, imlecin olduğu yere
            if (e.type == EventType.ScrollWheel && overMap && modalFree && MapCamera.I != null)
            {
                float steps = Mathf.Clamp(-e.delta.y / 3f, -2f, 2f);
                if (Mathf.Abs(steps) < 0.01f) steps = -Mathf.Sign(e.delta.y);
                MapCamera.I.ZoomAt(steps, GuiToScreenPx(m));
                e.Use();
                return;
            }

            // Kaydırma: herhangi bir tuşla sürükle (sol tuşla birkaç piksel oynatınca başlar, tıklamayı bozmaz)
            if (e.type == EventType.MouseDown && overMap && modalFree)
            {
                dragging = true;
                dragMoved = false;
                dragButton = e.button;
                dragStart = dragLast = m;
            }
            else if (e.type == EventType.MouseDrag && dragging && MapCamera.I != null)
            {
                if (!dragMoved && (dragButton != 0 || Vector2.Distance(m, dragStart) > 4f)) dragMoved = true;
                if (dragMoved)
                {
                    // Sağ tuş: döndür. Sol / orta tuş: kaydır.
                    if (dragButton == 1) MapCamera.I.RotateDrag((m.x - dragLast.x) * uiScale);
                    else MapCamera.I.DragPixels(GuiToScreenPx(dragLast), GuiToScreenPx(m));
                    dragLast = m;
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp && dragging)
            {
                bool wasClick = !dragMoved && dragButton == 0;
                dragging = false;
                if (wasClick && overMap && modalFree)
                {
                    var c = CourierAt(m);
                    if (c != null)
                    {
                        TelefonAc(c);
                        e.Use();
                    }
                    else if (HandleInfoClick(m)) e.Use();
                }
            }
        }

        static string WeatherName(WeatherKind k)
        {
            switch (k)
            {
                case WeatherKind.Sicak: return "Sıcak";
                case WeatherKind.Bulutlu: return "Bulutlu";
                case WeatherKind.Yagmurlu: return "Yağmur";
                case WeatherKind.Soguk: return "Soğuk";
                case WeatherKind.Karli: return "Kar";
                case WeatherKind.Firtina: return "Fırtına";
                default: return "Güneş";
            }
        }

        static Icons.Kind WeatherIcon(WeatherKind k)
        {
            switch (k)
            {
                case WeatherKind.Gunesli: case WeatherKind.Sicak: return Icons.Kind.Sun;
                case WeatherKind.Bulutlu: return Icons.Kind.Cloud;
                case WeatherKind.Yagmurlu: return Icons.Kind.Rain;
                case WeatherKind.Soguk: case WeatherKind.Karli: return Icons.Kind.Snow;
                default: return Icons.Kind.Storm;
            }
        }

        static Color WeatherTint(WeatherKind k)
        {
            switch (k)
            {
                case WeatherKind.Gunesli: return new Color(1f, 0.80f, 0.25f);
                case WeatherKind.Sicak: return new Color(1f, 0.55f, 0.25f);
                case WeatherKind.Bulutlu: return new Color(0.75f, 0.80f, 0.88f);
                case WeatherKind.Yagmurlu: return new Color(0.45f, 0.70f, 1f);
                case WeatherKind.Soguk: case WeatherKind.Karli: return new Color(0.70f, 0.88f, 1f);
                default: return new Color(0.70f, 0.60f, 1f);
            }
        }

        static bool IsSpeed(GameClock c, float s) => !c.Paused && Mathf.Approximately(c.SpeedMultiplier, s);

        // ---------------------------------------------------------------- simge çubuğu ve pencere

        Rect WindowRect(float sh) => new Rect(DockW + 10f, TopBar + 10f, panelWidth, sh - TopBar - 66f);

        void OpenTab(Tab t)
        {
            if (panelOpen && tab == t) { panelOpen = false; return; }   // aynı simge: kapat
            tab = t;
            panelOpen = true;
            scroll = Vector2.zero;
        }

        int BadgeOf(Tab t)
        {
            switch (t)
            {
                case Tab.Siparisler: return OrderManager.I != null ? OrderManager.I.Pending.Count : 0;
                case Tab.IseAlim: return KayitSistemi.I != null ? KayitSistemi.I.HesapBekleyen : 0;   // hesap sınırı yüzünden bekleyenler
                case Tab.Mesajlar: return CourierMessages.I != null ? CourierMessages.I.Unread : 0;
                default: return 0;
            }
        }

        static Color CourierStateColor(Courier c)
        {
            switch (c.State)
            {
                case CourierState.Cevrimdisi: return NeutralTone;
                case CourierState.Izinli: return UITheme.Bad;
                case CourierState.Mola: return UITheme.Warn;
                case CourierState.Bosta: return c.cashBlocked ? UITheme.Bad : UITheme.Good;
                default: return InfoTone;   // dükkana / müşteriye gidiyor, teslim ediyor
            }
        }

        static string EquipmentText(Courier c)
        {
            if (c.equipment.Count == 0) return "yok";
            string s = "";
            foreach (var k in c.equipment) s += (s.Length > 0 ? ", " : "") + GameData.Equip(k).name;
            return s;
        }

        /// <summary>Destek ekibi + operasyon sorumlusunun aylık maaş toplamı.</summary>
        long TotalPayroll => SupportManager.I.MonthlyPayroll + (OperationsManager.I != null ? OperationsManager.I.MonthlySalary : 0);

        /// <summary>Başvurunun kalan süresi (destek ve operasyon adayları gece yarısı sona erer).</summary>
        static string ExpiryText(float expiresAt)
        {
            if (expiresAt <= 0f || GameClock.I == null) return "";
            float h = (expiresAt - GameClock.I.TotalMinutes) / 60f;
            if (h <= 24f) return $"Başvuru bu gece sona erer ({Mathf.Max(0f, h):0} saat kaldı).";
            return $"Başvuru yarın gece sona erer ({h:0} saat kaldı).";
        }

        // ---------------------------------------------------------------- grafikler (son 30 gün)

        bool showMilestones;

        int financePage;   // 0 özet, 1 ödemeler, 2 vergi, 3 hareketler, 4 diğer

        void Stats(PeriodStats s)
        {
            // 1) Teslimatlardan kalan
            GUILayout.Label($"Teslimat: {s.deliveries} paket", small);
            GUILayout.Label($"  + Komisyon {Ft(s.commission)}  + Teslimat ücreti {Ft(s.fees)}", small);
            GUILayout.Label($"  − Kurye ödemeleri {Ft(s.courierPay)}" + (s.boosts > 0 ? $"  − Boost {Ft(s.boosts)}" : ""), small);
            GUILayout.Label($"Teslimat kârı: {Ft(s.DeliveryProfit)}" +
                            (s.deliveries > 0 ? $" (paket başı {Ft(s.DeliveryProfit / s.deliveries)})" : ""), label);

            // 2) Yatırımlar ve sabit giderler
            long others = s.OtherExpenses + s.rewards;
            if (others > 0 || s.otherIncome > 0)
            {
                string list = "";
                for (int i = 0; i < s.byCategory.Length; i++)
                    if (s.byCategory[i] > 0) list += (list.Length > 0 ? ", " : "") + $"{PeriodStats.CategoryNames[i]} {Ft(s.byCategory[i])}";
                if (s.rewards > 0) list += (list.Length > 0 ? ", " : "") + $"Hedef ödülleri {Ft(s.rewards)}";
                GUILayout.Label($"Yatırım ve diğer giderler: −{Ft(others)}", label);
                if (list.Length > 0) GUILayout.Label("  " + list, small);
                if (s.otherIncome > 0) GUILayout.Label($"  + Diğer gelir (ödüller, sigorta komisyonu, kesintiler) {Ft(s.otherIncome)}", small);
            }

            // 3) Sonuç
            if (s.penalties > 0) GUILayout.Label($"Cezalar (vergiden düşülmez): −{Ft(s.penalties)}", warn);
            KV("Net kâr", Ft(s.Profit - s.penalties), MoneyColor(s.Profit - s.penalties));
        }

        int settingsPage;   // 0 oyun, 1 ekonomi, 2 harita, 3 gelişmiş (Ayarlar penceresi: Arayuz/UiAyarlar.cs)

        void Bar(string name, float v, Color c) => Bar(name, v, c, $"%{v * 100f:0}");

        void Bar(string name, float v, Color c, string valueText)
        {
            GUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(name)) GUILayout.Label(name, small, GUILayout.Width(70));
            Rect r = GUILayoutUtility.GetRect(100, 16, GUILayout.ExpandWidth(true));
            r.y += 4;
            r.height = 8;
            UITheme.Box(r, new Color(0f, 0f, 0f, 0.45f), 4f);
            float fw = r.width * Mathf.Clamp01(v);
            if (fw > 1f) UITheme.Box(new Rect(r.x, r.y, Mathf.Max(8f, fw), r.height), c, 4f);
            GUILayout.Label(valueText, small, GUILayout.Width(52));
            GUILayout.EndHorizontal();
        }

        GameEvent olayAyrintiFor;
        bool olayAyrintiAcik;

        // ---------------------------------------------------------------- yardımcılar
        Color themeColor;

        void InitStyles()
        {
            // Şirket rengi değişince (kurulum, kayıt yükleme) düğmeler yeniden boyanır
            if (stylesReady && themeColor == CompanyProfile.Primary && kagitTema == UITheme.Kagit) return;
            stylesReady = true;
            kagitTema = UITheme.Kagit;
            themeColor = CompanyProfile.Primary;

            var font = UITheme.UIFont;
            var text = UITheme.Text;

            label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, richText = true };
            if (font != null) label.font = font;
            label.normal.textColor = UITheme.Yazi(text);

            small = new GUIStyle(label) { fontSize = 12 };
            small.normal.textColor = UITheme.Yazi(UITheme.TextDim);

            warn = new GUIStyle(small);
            warn.normal.textColor = UITheme.Yazi(UITheme.Warn);

            note = new GUIStyle(small) { fontStyle = FontStyle.Italic };
            note.normal.textColor = UITheme.Yazi(new Color(0.78f, 0.84f, 0.96f));

            header = new GUIStyle(label) { fontSize = 16, fontStyle = FontStyle.Bold, margin = new RectOffset(4, 4, 6, 3) };
            header.normal.textColor = UITheme.Yazi(Color.white);

            title = new GUIStyle(header) { fontSize = 24 };

            // Kart: yuvarlak köşeli, hafif açık zemin
            box = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(12, 12, 9, 9),
                margin = new RectOffset(0, 0, 5, 5),
                border = new RectOffset(10, 10, 10, 10)
            };
            UITheme.SetBg(box.normal, UITheme.Card, 10);

            // Düğmeler: düz, yuvarlak köşeli; seçili (on) hali şirket renginde
            button = MakeButton(14, 9);
            smallButton = MakeButton(12, 8);
            bigButton = MakeButton(18, 12);
            bigButton.fontStyle = FontStyle.Bold;

            toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 14, wordWrap = true };
            if (font != null) toggle.font = font;
            // Belirgin onay kutusu: dolu = şirket rengi + tik, boş = açık çerçeve (UITheme.KutuTex)
            var acc = CompanyProfile.Primary;
            toggle.normal.background = UITheme.KutuTex(false, false, acc);
            toggle.hover.background = UITheme.KutuTex(false, true, acc);
            toggle.active.background = UITheme.KutuTex(false, true, acc);
            toggle.focused.background = UITheme.KutuTex(false, false, acc);
            toggle.onNormal.background = UITheme.KutuTex(true, false, acc);
            toggle.onHover.background = UITheme.KutuTex(true, true, acc);
            toggle.onActive.background = UITheme.KutuTex(true, true, acc);
            toggle.onFocused.background = UITheme.KutuTex(true, false, acc);
            // Üst kenar dokunun tamamı: kutu hiç uzamaz, iki satırlı seçenekte de solda üstte kalır
            toggle.border = new RectOffset(UITheme.KutuW, 0, UITheme.KutuH, 0);
            toggle.overflow = new RectOffset(0, 0, 0, 0);
            toggle.padding = new RectOffset(UITheme.KutuW + 4, 4, 4, 4);
            toggle.margin = new RectOffset(2, 2, 3, 3);
            toggle.fixedHeight = 0f;
            toggle.alignment = TextAnchor.MiddleLeft;
            toggle.normal.textColor = UITheme.Yazi(text);
            toggle.onNormal.textColor = UITheme.Yazi(text);
            toggle.hover.textColor = UITheme.Yazi(Color.white);
            toggle.onHover.textColor = UITheme.Yazi(Color.white);

            textField = new GUIStyle(GUI.skin.textField) { fontSize = 18, padding = new RectOffset(10, 10, 6, 6), border = new RectOffset(8, 8, 8, 8) };
            if (font != null) textField.font = font;
            UITheme.SetBg(textField.normal, new Color(0.06f, 0.065f, 0.08f), 8);
            UITheme.SetBg(textField.focused, new Color(0.05f, 0.055f, 0.07f), 8);
            textField.normal.textColor = UITheme.Yazi(text);
            textField.focused.textColor = UITheme.Yazi(Color.white);

            worldLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            if (font != null) worldLabel.font = font;

            barTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            if (font != null) barTitle.font = font;
            barText = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            if (font != null) barText.font = font;
            logoLetter = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            if (font != null) logoLetter.font = font;

            phoneTitle = new GUIStyle(label) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = false };
            phoneTitle.normal.textColor = Color.white;
            phoneSection = new GUIStyle(label) { fontSize = 11, fontStyle = FontStyle.Bold, margin = new RectOffset(6, 4, 10, 2) };
            phoneSection.normal.textColor = new Color(0.62f, 0.65f, 0.71f);
            phoneBig = new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Bold };
            phoneBig.normal.textColor = Color.white;
            phoneCard = new GUIStyle(box) { padding = new RectOffset(12, 12, 10, 10), margin = new RectOffset(2, 2, 4, 4), border = new RectOffset(14, 14, 14, 14) };
            UITheme.SetBg(phoneCard.normal, new Color(0.11f, 0.12f, 0.15f), 14);
            accentButton = MakeButton(14, 9);
            accentButton.normal = accentButton.onNormal;
            accentButton.hover = accentButton.onHover;
            accentButton.active = accentButton.onActive;
            accentButton.fontStyle = FontStyle.Bold;
            phoneButton = MakeButton(12, 14);
            phoneButton.fixedHeight = 34;
            phoneButton.fontStyle = FontStyle.Bold;
            InitKit();   // ortak pencere stilleri (UIKit.cs)

            // Kalın stiller: Unity'nin taklit kalınlığı yerine Inter SemiBold (sonradan kurulan stiller kendi yerinde çevrilir)
            foreach (var f in typeof(GameUI).GetFields(System.Reflection.BindingFlags.Instance |
                                                       System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public))
                if (f.FieldType == typeof(GUIStyle))
                {
                    var gs = f.GetValue(this) as GUIStyle;
                    UITheme.Semi(gs);
                    // Kâğıt tema: açık renk yazılar krem zeminde okunur koyu tona (kurye telefonu stilleri hariç: o ekran koyu kalır)
                    bool telefon = f.Name.StartsWith("phone") || (f.Name.Length > 1 && f.Name[0] == 'p' && char.IsUpper(f.Name[1]));
                    if (!telefon) UITheme.KagitYazi(gs);
                }
        }

        GUIStyle phoneTitle, phoneSection, phoneBig, phoneCard, phoneButton, accentButton;
        bool kagitTema;

        GUIStyle MakeButton(int fontSize, int radius)
        {
            var st = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                wordWrap = true,
                border = new RectOffset(radius, radius, radius, radius),
                padding = new RectOffset(10, 10, 6, 6),
                margin = new RectOffset(3, 3, 3, 3)
            };
            if (UITheme.UIFont != null) st.font = UITheme.UIFont;
            var baseC = new Color(0.19f, 0.205f, 0.245f);
            UITheme.SetBg(st.normal, baseC, radius);
            UITheme.SetBg(st.hover, new Color(0.25f, 0.27f, 0.32f), radius);
            UITheme.SetBg(st.active, new Color(0.14f, 0.15f, 0.18f), radius);
            UITheme.SetBg(st.focused, baseC, radius);
            var acc = UITheme.Accent;
            UITheme.SetBg(st.onNormal, acc, radius);
            UITheme.SetBg(st.onHover, Color.Lerp(acc, Color.white, 0.15f), radius);
            UITheme.SetBg(st.onActive, Color.Lerp(acc, Color.black, 0.2f), radius);
            UITheme.SetBg(st.onFocused, acc, radius);
            st.normal.textColor = UITheme.Yazi(st.hover.textColor = st.active.textColor = st.focused.textColor = UITheme.Text);
            st.onNormal.textColor = st.onHover.textColor = st.onActive.textColor = st.onFocused.textColor = UITheme.AccentText;
            return st;
        }
    }
}
