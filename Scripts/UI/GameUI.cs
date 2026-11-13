using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Prototip arayüzü (Unity IMGUI). Kurulum ekranı, üst bar, sol panel sekmeleri,
    /// haber akışı ve olay kartları. Hiçbir Canvas / prefab gerektirmez.
    /// GEÇİŞ: arayüz UI Toolkit'e taşınıyor (Scripts/UI/Arayuz, Resources/UI/Arayuz). Üst çubuk, sol menü, pencere çerçevesi,
    /// harita araçları, olaylar, hedef kutusu ve Siparişler/Kuryeler/Gelişim/Hedefler pencereleri orada. Yeni arayüz OnGUI'ye
    /// yazılmaz. Buradaki eski çizimler F8 ile (eski arayüz) ve henüz taşınmayan pencereler için duruyor.
    /// </summary>
    public partial class GameUI : MonoBehaviour
    {
        enum Tab { Siparisler, Kuryeler, IseAlim, Destek, Gelistirmeler, Dukkanlar, Ekipman, Kampanyalar, Finans, Ayarlar, Hedefler, Mesajlar, Market, Istatistik }
        static readonly string[] BaseTabNames = { "Siparişler", "Kuryeler", "İşe Alım", "Destek", "Geliştirmeler", "Dükkanlar", "Ekipman", "Kampanyalar", "Finans", "Ayarlar", "Haftalık Hedefler",
                                                  "Müşteri Yorumları", "Kendi Marketin", "İstatistikler" };

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

        static readonly Color SelectedTint = new Color(0.35f, 0.85f, 1f);

        Tab tab;
        Vector2 scroll;
        float uiScale = 1f;
        bool modalFree = true;
        System.Action deferred; // listeleri değiştiren işlemler bir sonraki Update'te yapılır

        // Kurulum ekranı
        string companyName = "Hızlı Kurye";
        Color primary, secondary;
        int editingColor;

        Courier selectedCourier;
        Courier popupCourier;   // haritada tıklanan kurye: sağda detay paneli
        Courier hoverCourier;   // fare üstündeki kurye (ismi sarı yanar)
        Vector2 popupScroll;

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
            UiGuncelle();   // UI Toolkit arayüzü (Arayuz/UiKok.cs)
        }

        void LateUpdate()
        {
            UpdateMenuGroups();   // yeni açılan sekmeler, son bakılan sekme
            UpdateTutorial();
            // Harita kamerası sol panel ve üst bar dışındaki alana çizer (yakınlaştırma/kaydırma MapCamera'da).
            // Pencere açıkken harita pencerenin sağına kayar (Ayarlar'dan kapatılırsa pencere haritanın üstünde yüzer).
            if (MapCamera.I == null) return;
            UpdateMapViewport();
        }

        // =====================================================================
        void OnGUI()
        {
            // Ekran yüksekliğine göre ölçek × oyuncunun seçtiği arayüz boyutu (Ayarlar > Arayüz)
            uiScale = UiOlcek();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));
            float sw = Screen.width / uiScale;
            float sh = Screen.height / uiScale;
            InitStyles();
            if (Event.current.type == EventType.Repaint) helpTip = null;   // "?" balonu her karede yeniden bulunur

            if (GameBootstrap.I == null) return;
            if (!GameBootstrap.I.Started)
            {
                DrawSetup(sw, sh);
                return;
            }
            if (HandlePhotoMode(sw, sh)) return;   // F10: arayüz gizli, yalnızca şehir

            if (ConfirmOpen) HandleConfirmKey();
            else if (BigCardOpen) HandleBigCardKey();   // büyük kurye kartı: Esc kapatır (CourierBigCard.cs)
            else if (EventManager.I.Current == null) HandleMenuKey();
            modalFree = EventManager.I.Current == null && !pauseMenuOpen && !ConfirmOpen && !BigCardOpen;
            if (modalFree) HandleKeys();
            if (!pauseMenuOpen && !ConfirmOpen && !BigCardOpen) HandleMapMouse(sw, sh);

            GUI.enabled = modalFree;
            // Yeni arayüz (UI Toolkit) açıksa üst çubuk, sol menü, pencere başlığı, harita araçları, olaylar ve hedef kutusu orada çizilir.
            // Haritanın üstüne çizilen IMGUI parçaları (katman, etiketler, uçan yazılar) harita alanına kırpılır: UI Toolkit'in altına taşmasın.
            bool eskiArayuz = !UiAktif;
            var haritaAlani = new Rect(MapLeftGui, TopBar, Mathf.Max(0f, sw - MapLeftGui), Mathf.Max(0f, sh - TopBar));
            if (!eskiArayuz) GUI.BeginClip(haritaAlani, -haritaAlani.position, Vector2.zero, false);
            DrawHeat();          // harita katmanı (etiketlerin altında)
            DrawWorldLabels();
            DrawMapPops();       // uçan "+350 Ft" yazıları
            if (!eskiArayuz) GUI.EndClip();
            if (eskiArayuz) DrawTopBar(sw);
            DrawPanel(sh);
            if (eskiArayuz) DrawFeed(sw, sh);
            if (eskiArayuz) DrawZoomButtons(sh);
            DrawLegend(sh);
            if (eskiArayuz) DrawHeatMenu(sh);    // harita katmanı menüsü (HeatMap.cs)
            if (eskiArayuz) DrawGoalTracker();
            if (eskiArayuz) DrawUyarilar(sw);    // "Dikkat" listesi (UyariListesi.cs)
            DrawCityEventMarker(sw);
            UITheme.Klasik = true;      // kurye telefonu: koyu ekran olarak kalır
            DrawCourierPopup(sw, sh);
            UITheme.Klasik = false;
            DrawInfoMarker();
            DrawInfoPopup(sw, sh);
            DrawToasts(sw, sh);
            DrawGuideOverlays(sw, sh);
            GUI.enabled = true;
            DrawEvent(sw, sh);
            if (BigCardOpen && EventManager.I.Current == null && !pauseMenuOpen) DrawBigCard(sw, sh);
            if (pauseMenuOpen) DrawPauseMenu(sw, sh);
            if (ConfirmOpen) DrawConfirm(sw, sh);
        }

        // ---------------------------------------------------------------- ESC menüsü (kaydet / ana menü / çık)
        bool pauseMenuOpen, menuWasPaused, confirmMainMenu;

        void HandleMenuKey()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode != KeyCode.Escape) return;
            e.Use();
            // Önce açık pencere kapanır, sonra Esc menüsü açılır
            if (InfoOpen && !pauseMenuOpen) { CloseInfo(); return; }
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

        void DrawPauseMenu(float sw, float sh)
        {
            Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, 0.55f));
            float w = 440f, h = 450f;
            var r = new Rect((sw - w) / 2f, (sh - h) / 2f, w, h);
            UITheme.Shadowed(r, new Color(0.115f, 0.125f, 0.155f), 18f, 10f);
            GUILayout.BeginArea(new Rect(r.x + 20, r.y + 16, w - 40, h - 32));
            GUILayout.Label("Menü", title);
            GUILayout.Label("Oyun duraklatıldı. Esc ile kapanır.", small);
            GUILayout.Space(8);
            if (GUILayout.Button("Devam et", button, GUILayout.Height(34))) deferred = () => SetPauseMenu(false);

            var save = SaveSystem.I;
            if (save != null)
            {
                GUI.enabled = SaveSystem.CanSaveNow;
                if (GUILayout.Button($"Kaydet ({SaveSystem.Slot + 1}. yuva)", button, GUILayout.Height(34))) deferred = () => save.Save(false);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Başka yuvaya kaydet:", small, GUILayout.Width(140));
                for (int i = 0; i < SaveSystem.SlotCount; i++)
                {
                    if (i == SaveSystem.Slot) continue;
                    int slot = i;
                    bool full = SaveSystem.SlotHasSave(i);
                    if (GUILayout.Button($"{i + 1}" + (full ? " (üstüne yaz)" : ""), smallButton))
                        deferred = () => { SaveSystem.Slot = slot; save.Save(false); setupSaveChecked = false; };
                }
                GUILayout.EndHorizontal();
                GUI.enabled = true;
                GUILayout.Label(string.IsNullOrEmpty(save.LastSaveText) ? "Bu oturumda henüz kaydedilmedi." : $"Son kayıt: {save.LastSaveText}", small);
                save.autoSave = GUILayout.Toggle(save.autoSave, " Her gece otomatik kaydet", toggle);
            }

            GUILayout.Space(10);
            if (!confirmMainMenu)
            {
                if (GUILayout.Button("Ana menüye dön", button, GUILayout.Height(30))) deferred = () => confirmMainMenu = true;
            }
            else
            {
                GUILayout.Label("Son kayıttan sonraki ilerleme kaybolur. Emin misin?", warn);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Evet, ana menüye dön", smallButton)) deferred = () => { SetPauseMenu(false); GameBootstrap.Restart(); };
                if (GUILayout.Button("Vazgeç", smallButton)) deferred = () => confirmMainMenu = false;
                GUILayout.EndHorizontal();
            }

            if (save != null)
            {
                GUI.enabled = SaveSystem.CanSaveNow;
                if (GUILayout.Button("Kaydet ve çık", button, GUILayout.Height(30)))
                    deferred = () => { if (save.Save(false)) QuitGame(); };
                GUI.enabled = true;
            }
            if (GUILayout.Button("Kaydetmeden çık", button, GUILayout.Height(30))) deferred = QuitGame;
            GUILayout.EndArea();
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
        Vector2 setupScroll;

        void DrawSetup(float sw, float sh)
        {
            if (!setupSaveChecked)
            {
                setupSaveChecked = true;
                setupSave = null;
                for (int i = 0; i < SaveSystem.SlotCount; i++)
                {
                    slotSaves[i] = SaveSystem.ReadSave(i);
                    if (slotSaves[i] != null && setupSave == null) setupSave = slotSaves[i];
                }
                if (newGameSlot < 0) { int e = SaveSystem.FirstEmptySlot(); newGameSlot = e >= 0 ? e : SaveSystem.Slot; }
            }
            bool hasError = !string.IsNullOrEmpty(GameBootstrap.LastLoadError);

            Fill(new Rect(0, 0, sw, sh), new Color(0.08f, 0.09f, 0.11f));
            float w = 620f, h = 1250f + (Application.isEditor || Debug.isDebugBuild ? 60f : 0f) + (setupSave != null ? 70f + SaveSystem.SlotCount * 44f : 50f) + (hasError ? 40f : 0f);
            h = Mathf.Min(h, sh - 20f);
            var r = new Rect((sw - w) / 2f, Mathf.Max(10f, (sh - h) / 2f), w, h);
            UITheme.Shadowed(r, new Color(0.115f, 0.125f, 0.155f), 18f, 10f);

            GUILayout.BeginArea(new Rect(r.x + 24, r.y + 20, w - 48, h - 40));
            setupScroll = GUILayout.BeginScrollView(setupScroll);
            if (hasError) GUILayout.Label(GameBootstrap.LastLoadError, warn);
            if (setupSave != null)
            {
                GUILayout.BeginVertical(box);
                GUILayout.Label("Kayıtlı oyunlar", header);
                for (int i = 0; i < SaveSystem.SlotCount; i++)
                {
                    int slot = i;
                    var sd = slotSaves[i];
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{i + 1}. yuva", label, GUILayout.Width(58));
                    if (sd == null)
                    {
                        GUILayout.Label("boş", small);
                        GUILayout.FlexibleSpace();
                    }
                    else
                    {
                        GUILayout.Label((SaveSystem.IsOld(sd) ? "ESKİ SÜRÜM · " : "") + SaveSystem.Summary(sd), SaveSystem.IsOld(sd) ? warn : small, GUILayout.Width(w - 48 - 58 - 190));
                        GUILayout.FlexibleSpace();
                        if (confirmDeleteSlot == i)
                        {
                            if (GUILayout.Button("Evet, sil", smallButton, GUILayout.Width(70)))
                                deferred = () => { SaveSystem.DeleteSlot(slot); confirmDeleteSlot = -1; newGameSlot = -1; setupSaveChecked = false; };
                            if (GUILayout.Button("Vazgeç", smallButton, GUILayout.Width(60))) deferred = () => confirmDeleteSlot = -1;
                        }
                        else
                        {
                            bool askOld = SaveSystem.IsOld(sd) && confirmOldSlot != i;
                            if (GUILayout.Button(askOld || !SaveSystem.IsOld(sd) ? "Devam et" : "Yine de aç", smallButton, GUILayout.Width(80)))
                            {
                                if (askOld) deferred = () => confirmOldSlot = slot;
                                else deferred = () => { confirmOldSlot = -1; SaveSystem.Slot = slot; if (!GameBootstrap.I.LoadGame()) setupSaveChecked = false; };
                            }
                            if (GUILayout.Button("Sil", smallButton, GUILayout.Width(44))) deferred = () => confirmDeleteSlot = slot;
                        }
                    }
                    GUILayout.EndHorizontal();
                    if (sd != null && confirmOldSlot == i)
                        GUILayout.Label("Bu kayıt oyunun eski bir sürümünden: o zamandan beri şehir planı ve bazı kurallar değişti. Açılır ama dükkanlar ve kuryeler " +
                                        "yanlış yerde çıkabilir, denge tutmayabilir. Yeni oyun önerilir; yine de açmak için \"Yine de aç\"a bas.", warn);
                }
                GUILayout.EndVertical();
                GUILayout.Space(6);
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Yeni oyunun kayıt yuvası:", small, GUILayout.Width(170));
            for (int i = 0; i < SaveSystem.SlotCount; i++)
            {
                int slot = i;
                if (SelectButton(newGameSlot == i, $"{i + 1}" + (slotSaves[i] != null ? " (dolu)" : ""), smallButton)) deferred = () => newGameSlot = slot;
            }
            GUILayout.EndHorizontal();
            if (newGameSlot >= 0 && slotSaves[newGameSlot] != null)
                GUILayout.Label($"{newGameSlot + 1}. yuvadaki \"{slotSaves[newGameSlot].companyName}\" kaydı, yeni oyun ilk kez kaydedilince silinir.", warn);
            GUILayout.Space(6);
            GUILayout.Label("Kurye Şirketini Kur", title);
            var gb = GameBootstrap.I;
            long borc = System.Math.Max(gb.familyLoanTotal, gb.familyLoanAmount);
            GUILayout.Label($"Yıllarca kuryelik yaptın. Şimdi annenle babanın emeklilik birikimi olan {Ft(borc)} ile kendi kurye şirketini kuruyorsun. " +
                            (borc > gb.familyLoanAmount ? $"Kuruluş masraflarından sonra kasada {Ft(gb.familyLoanAmount)} kalıyor. " : "") +
                            $"Borcu Temmuz sonuna kadar ayda {Ft(borc / 5)} taksitle geri ödemen gerekiyor.", label);
            GUILayout.Space(6);
            GUILayout.BeginVertical(box);
            GUILayout.Label("Bu oyunda ne yapacaksın?", header);
            GUILayout.Label("•  Dükkanlarla anlaş, kurye al, siparişleri zamanında teslim ettir. Hızlı teslimat puanını ve pazar payını büyütür.", small);
            GUILayout.Label("•  Gelirin: dükkan komisyonu ve teslimat ücreti. Giderin: kurye hakedişi, ekipman, geliştirmeler, personel.", small);
            GUILayout.Label("•  Kuryeler serbest çalışır: saatte iyi kazanırsa mutlu olur, uzun günler biriktikçe yorulur. Çok kurye alırsan iş başına düşen kazanç azalır.", small);
            GUILayout.Label("•  İtibar son iki haftanın müşteri puanıdır; iptal edilen sipariş 1 yıldız sayılır. Şirket büyüdükçe yatırımlar da pahalanır.", small);
            GUILayout.Label("•  Şehir yaşıyor: gündüz ve gece, hava, trafik ve şehir olayları siparişleri ve kuryeleri etkiler.", small);
            GUILayout.Label("•  İlk hedef: aile borcunu zamanında ödemek. Sonrası: şehrin en büyük kurye şirketi olmak.", small);
            GUILayout.Label("Boşluk: duraklat · 1–8: hız · H: sipariş yoğunluğu · F10: fotoğraf modu · Esc: menü. Oyun her gece kendini kaydeder.", bodyDim);
            GUILayout.EndVertical();
            GUILayout.Space(6);
            GUILayout.Label("Önce şirketine bir isim ve renk ver.", small);
            GUILayout.Space(12);

            GUILayout.Label("Şirket adı", header);
            companyName = GUILayout.TextField(companyName, 24, textField);
            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            if (SelectButton(editingColor == 0, "Ana renk", button, GUILayout.Height(28))) editingColor = 0;
            if (SelectButton(editingColor == 1, "İkincil renk", button, GUILayout.Height(28))) editingColor = 1;
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            Color c = editingColor == 0 ? primary : secondary;
            GUILayout.BeginHorizontal();
            foreach (var p in Presets)
            {
                Rect sr = GUILayoutUtility.GetRect(42, 42, GUILayout.Width(42), GUILayout.Height(42));
                bool selected = Mathf.Abs(p.r - c.r) + Mathf.Abs(p.g - c.g) + Mathf.Abs(p.b - c.b) < 0.02f;
                if (selected) Fill(sr, Color.white);
                Fill(new Rect(sr.x + 3, sr.y + 3, sr.width - 6, sr.height - 6), p);
                if (GUI.Button(sr, GUIContent.none, GUIStyle.none)) c = p;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            c.r = SliderRow("Kırmızı", c.r, 0f, 1f, Mathf.RoundToInt(c.r * 255f).ToString(), 80);
            c.g = SliderRow("Yeşil", c.g, 0f, 1f, Mathf.RoundToInt(c.g * 255f).ToString(), 80);
            c.b = SliderRow("Mavi", c.b, 0f, 1f, Mathf.RoundToInt(c.b * 255f).ToString(), 80);
            c.a = 1f;
            if (editingColor == 0) primary = c; else secondary = c;

            GUILayout.Space(12);
            GUILayout.Label("Önizleme", header);
            Rect pr = GUILayoutUtility.GetRect(100, 130, GUILayout.ExpandWidth(true));
            Fill(pr, primary);
            var logo = new Rect(pr.x + 18, pr.y + 18, 94, 94);
            Fill(logo, secondary);
            Fill(new Rect(logo.x + 7, logo.y + 7, logo.width - 14, logo.height - 14), primary);
            logoLetter.normal.textColor = secondary;
            string initial = string.IsNullOrEmpty(companyName.Trim()) ? "K" : companyName.Trim().Substring(0, 1).ToUpper();
            GUI.Label(logo, initial, logoLetter);
            barTitle.normal.textColor = secondary;
            GUI.Label(new Rect(pr.x + 130, pr.y + 30, pr.width - 260, 40), companyName, barTitle);
            barText.normal.textColor = secondary;
            GUI.Label(new Rect(pr.x + 130, pr.y + 66, pr.width - 260, 30), "Kurye montu, çanta ve araçlar bu renklerde olacak", barText);
            var courier = new Rect(pr.xMax - 100, pr.y + 35, 60, 60);
            Fill(courier, secondary);
            Fill(new Rect(courier.x + 10, courier.y + 10, 40, 40), primary);

            GUILayout.Space(12);
            DrawDifficultyPicker();
            GUILayout.Space(12);
            DrawSenaryoSecici();   // oyun türü ve olay anlatıcısı (SenaryoSecimi.cs)

            GUILayout.Space(14);
            if (GUILayout.Button("Şirketi Kur ve Başla", bigButton, GUILayout.Height(50)))
            {
                Difficulty.Level = setupDifficulty;
                Difficulty.LastChoice = setupDifficulty;
                Senaryo.Secili = (Senaryo.Tur)setupSenaryo;
                Anlatici.Secili = (Anlatici.Tur)setupAnlatici;
                CompanyProfile.Name = string.IsNullOrEmpty(companyName.Trim()) ? "Hızlı Kurye" : companyName.Trim();
                CompanyProfile.Primary = primary;
                CompanyProfile.Secondary = secondary;
                if (newGameSlot >= 0) SaveSystem.Slot = newGameSlot;
                deferred = () => GameBootstrap.I.StartGame();
            }
            DrawTestSetup();   // geliştirici: otomatik test (TestModuUI.cs)
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- fotoğraf modu (F10)
        bool fotoModu;
        float fotoIpucuBitis;

        /// <summary>
        /// F10 arayüzü gizler (ekran görüntüsü, fragman): harita kısayolları ve fare çalışmaya devam eder.
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
            if (Time.unscaledTime < fotoIpucuBitis)
            {
                var hr = new Rect(sw / 2f - 190f, sh - 54f, 380f, 30f);
                Fill(hr, new Color(0f, 0f, 0f, 0.55f));
                GUI.Label(hr, "Fotoğraf modu · F10 ya da Esc: arayüzü geri getir", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } });
            }
            return true;
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
                case KeyCode.F8: YeniArayuz = !YeniArayuz; e.Use(); return;   // geçiş döneminde eski arayüze dönüş
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

        // ---------------------------------------------------------------- harita etiketleri
        struct LabelReq { public Vector2 p; public string text; public Color color; public bool landmark; public Rect r; public bool skip; public bool hasIcon; public Icons.Kind icon; public bool onemli; }
        readonly List<LabelReq> labelReqs = new List<LabelReq>();
        readonly List<Rect> labelPlaced = new List<Rect>();

        /// <summary>Etiketi sıraya koyar; icon verilirse yazının soluna simge çizilir (ör. kuryenin aracı).</summary>
        void QueueLabel(Camera cam, Vector3 world, string text, Color color, bool landmark = false, Icons.Kind? icon = null, bool onemli = false)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) return;
            labelReqs.Add(new LabelReq
            {
                p = new Vector2(sp.x / uiScale, (Screen.height - sp.y) / uiScale), text = text, color = color, landmark = landmark,
                hasIcon = icon.HasValue, icon = icon ?? Icons.Kind.Close, onemli = onemli
            });
        }

        /// <summary>
        /// Harita etiketleri. Çakışmasınlar diye önem sırasına göre yerleştirilir: kuryeler, merkez ofis, dükkanlar, rakipler, yer adları.
        /// Çakışan etiket biraz yukarı/aşağı kaydırılır; yer adı çakışırsa hiç gösterilmez. Önemli olan en üstte çizilir.
        /// </summary>
        void DrawWorldLabels()
        {
            var cam = Camera.main;
            if (cam == null || CityMap.I == null) return;
            labelReqs.Clear();
            float zoom = MapCamera.I != null ? MapCamera.I.Zoom : 1f;

            // 1) Kuryeler (aynı karedekiler üst üste dizilir). Kalabalık ekipte bütün şehre bakarken isim yazılmaz
            //    (sadece fare üstündeki / seçili kurye): etiketler birbirine girmesin
            var stack = new Dictionary<Vector2Int, int>();
            bool kalabalik = CourierManager.I.Couriers.Count > 8 && zoom < 0.18f;
            foreach (var c in CourierManager.I.Couriers)
            {
                var key = Vector2Int.RoundToInt(c.Position);
                stack.TryGetValue(key, out int n);
                stack[key] = n + 1;
                bool hi = c == popupCourier || c == hoverCourier;
                if (kalabalik && !hi) continue;
                QueueLabel(cam, CityMap.ToWorld(c.Position) + CourierLabelOffset(n),
                    c.ShortName, hi ? new Color(1f, 0.85f, 0.2f) : Color.white, false, Icons.Of(c.vehicle), hi);
            }

            // 2) Merkez ofis
            float hqTop = BuildingTop(CityMap.I.HQTile);
            QueueLabel(cam, CityMap.ToWorld(CityMap.I.HQTile) + MapCamera.LabelOffset(0f, hqTop + 0.05f), CompanyProfile.Initial, CompanyProfile.Secondary, false, null, true);
            QueueLabel(cam, CityMap.ToWorld(CityMap.I.HQTile) + MapCamera.LabelOffset(-0.7f, hqTop + 0.35f), "Merkez", Color.white, false, null, true);

            // 3) Dükkanlar (uzaktayken sözleşmesizler gizlenir: harita kalabalık olmasın)
            foreach (var s in CityMap.I.Shops)
            {
                if (!CityMap.I.IsUnlocked(s.tile)) continue;
                if (!s.contracted && zoom < 0.3f && infoShop != s) continue;
                if (heatMode == HeatMode.DukkanKari && !s.contracted) continue;   // kârlılık katmanında yalnızca sözleşmeli dükkanlar
                if (ShopProfitLabel(s, out string karYazi, out Color karRenk))
                {
                    QueueLabel(cam, CityMap.ToWorld(s.tile) + MapCamera.LabelOffset(0.7f, BuildingTop(s.tile) + 0.2f), karYazi, karRenk);
                    continue;
                }
                bool shopOpen = ShopHours.IsOpen(s.def);
                QueueLabel(cam, CityMap.ToWorld(s.tile) + MapCamera.LabelOffset(0.7f, BuildingTop(s.tile) + 0.2f),
                    (s.contracted ? s.Name : s.Name + " (sözleşmesiz)") + (shopOpen ? "" : " · Kapalı"),
                    !shopOpen ? new Color(0.55f, 0.55f, 0.6f) : s.contracted ? Color.white : new Color(0.7f, 0.7f, 0.7f));
            }

            // 4) Rakip kuryeler: sadece yakındayken
            if (RivalFleet.I != null && MapCamera.I != null && MapCamera.I.ShowModels)
                foreach (var a in RivalFleet.I.Agents)
                    if (a != null && a.Visible)
                        QueueLabel(cam, CityMap.ToWorld(a.Position) + MapCamera.LabelOffset(0.5f, 0.55f), a.label,
                                   Color.Lerp(RivalFleet.RivalColor, Color.white, 0.4f));

            // 4b) Teslimat robotları: sadece yakındayken
            if (RobotFilo.I != null && MapCamera.I != null && MapCamera.I.ShowModels)
                foreach (var rb in RobotFilo.I.Robotlar)
                    if (rb.durum != RobotFilo.Durum.Bosta)
                        QueueLabel(cam, CityMap.ToWorld(rb.pos) + MapCamera.LabelOffset(0.5f, 0.35f),
                                   rb.order != null ? $"{rb.Ad} · #{rb.order.id}" : rb.Ad, RobotFilo.RobotHalka);

            // 5) Yer adları: kilitli bölgedekiler soluk
            if (ShowLandmarks)
                foreach (var lm in CityMap.Landmarks)
                {
                    bool open = CityMap.I.IsUnlocked(Vector2Int.RoundToInt(lm.pos));
                    var c = open ? lm.color : UITheme.WithAlpha(lm.color, 0.45f);
                    QueueLabel(cam, CityMap.ToWorld(lm.pos) + MapCamera.LabelOffset(0f, 0.05f), lm.name, c, true);
                }

            // Yerleştir
            if (landmarkStyle == null)
                landmarkStyle = UITheme.Semi(new GUIStyle(worldLabel) { fontStyle = FontStyle.BoldAndItalic, fontSize = 13, alignment = TextAnchor.MiddleCenter });
            labelPlaced.Clear();
            // Haritanın üstündeki UI Toolkit kutuları (olaylar, hedefler, araç çubuğu) dolu sayılır: etiket altlarına girmesin
            if (UiAktif) UiHaritaUstuDolu(labelPlaced);
            float[] shifts = { 0f, -19f, 19f, -38f, 38f };
            for (int i = 0; i < labelReqs.Count; i++)
            {
                var q = labelReqs[i];
                var style = q.landmark ? landmarkStyle : worldLabel;
                float tw = style.CalcSize(new GUIContent(q.text)).x + 14f + (q.hasIcon ? 18f : 0f);
                var baseR = new Rect(q.p.x - tw / 2f, q.p.y - 9f, tw, 18f);
                q.r = baseR;
                q.skip = false;
                bool ok = false;
                foreach (float dy in q.landmark ? new[] { 0f } : shifts)
                {
                    var r = new Rect(baseR.x, baseR.y + dy, baseR.width, baseR.height);
                    bool hit = false;
                    foreach (var o in labelPlaced) if (o.Overlaps(r)) { hit = true; break; }
                    if (!hit) { q.r = r; ok = true; break; }
                }
                if (!ok && !q.onemli) q.skip = true;   // yer yoksa çizilmez (üst üste binen etiket okunmuyor)
                if (!q.skip) labelPlaced.Add(q.r);
                labelReqs[i] = q;
            }

            // Önemsizden önemliye çiz: kuryeler en üstte kalsın
            for (int i = labelReqs.Count - 1; i >= 0; i--)
            {
                var q = labelReqs[i];
                if (q.skip) continue;
                if (q.landmark) DrawLandmarkAt(q.r, q.text, q.color);
                else if (q.hasIcon) DrawPillAt(q.r, q.text, q.color, q.icon);
                else DrawPillAt(q.r, q.text, q.color);
            }
        }

        void DrawPillAt(Rect r, string text, Color color)
        {
            UITheme.Box(r, new Color(0.05f, 0.055f, 0.07f, 0.72f), 9f);
            worldLabel.normal.textColor = UITheme.Yazi(color);
            GUI.Label(new Rect(r.center.x - 120f, r.y - 1f, 240f, 20f), text, worldLabel);
        }

        /// <summary>Solunda simge olan etiket (kurye: aracın simgesi + adı).</summary>
        void DrawPillAt(Rect r, string text, Color color, Icons.Kind icon)
        {
            UITheme.Box(r, new Color(0.05f, 0.055f, 0.07f, 0.72f), 9f);
            Icons.Draw(new Rect(r.x + 7f, r.center.y - 7f, 14f, 14f), icon, color);
            worldLabel.normal.textColor = UITheme.Yazi(color);
            float cx = r.center.x + 9f;   // yazı simgenin sağındaki alanın ortasında
            GUI.Label(new Rect(cx - 120f, r.y - 1f, 240f, 20f), text, worldLabel);
        }

        void DrawLandmarkAt(Rect r, string text, Color color)
        {
            var lr = new Rect(r.center.x - 120f, r.center.y - 11f, 240f, 22f);
            landmarkStyle.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.75f);
            GUI.Label(new Rect(lr.x + 1, lr.y + 1, lr.width, lr.height), text, landmarkStyle);
            landmarkStyle.normal.textColor = color;
            GUI.Label(lr, text, landmarkStyle);
        }

        const string LandmarkKey = "KuryeTycoon.HaritaEtiketleri";
        static bool ShowLandmarks
        {
            get { return PlayerPrefs.GetInt(LandmarkKey, 1) == 1; }
            set { PlayerPrefs.SetInt(LandmarkKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        GUIStyle landmarkStyle;

        /// <summary>Yer adı: zeminsiz, gölgeli, italik yazı (dükkan ve kurye etiketlerinden ayırt edilsin).</summary>
        void LandmarkLabel(Camera cam, Vector3 world, string text, Color color)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) return;
            if (landmarkStyle == null)
                landmarkStyle = UITheme.Semi(new GUIStyle(worldLabel) { fontStyle = FontStyle.BoldAndItalic, fontSize = 13, alignment = TextAnchor.MiddleCenter });
            float x = sp.x / uiScale;
            float y = (Screen.height - sp.y) / uiScale;
            var r = new Rect(x - 110f, y - 11f, 220f, 22f);
            landmarkStyle.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.75f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, landmarkStyle);
            landmarkStyle.normal.textColor = color;
            GUI.Label(r, text, landmarkStyle);
        }

        bool legendOpen;
        const int LegendItems = 14;

        Rect LegendRect(float sh)
        {
            float w = 250f, h = LegendItems * 19f + 34f;
            float x = panelOpen ? WindowRect(sh).xMax + 10f : DockW + 10f;
            return new Rect(x, sh - 52f - h, w, h);
        }

        /// <summary>Harita lejantı: renklerin ne anlama geldiği.</summary>
        void DrawLegend(float sh)
        {
            if (!legendOpen) return;
            var items = new List<(Color c, string t)>
            {
                (CityMap.RoadColor, "Yol"),
                (CityMap.BridgeColor, "Köprü (nehri sadece buradan geç)"),
                (CityMap.WaterColor, "Nehir"),
                (CityMap.GrassColor, "Park"),
                (CityMap.PlazaColor, "Meydan"),
                (CityMap.LegendColor(Zone.OldTown), "Eski şehir (3–5 kat)"),
                (CityMap.LegendColor(Zone.HighRise), "Kule bloklar (7–12 kat, asansör)"),
                (CityMap.LegendColor(Zone.Houses), "Bahçeli evler (1–2 kat)"),
                (CityMap.LegendColor(Zone.Mixed), "Çarşı (3–6 kat)"),
                (CityMap.MallColor, "AVM"),
                (CityMap.LegendColor(Zone.Campus), "Kampüs (2–4 kat)"),
                (CityMap.LegendColor(Zone.Warehouse), "Sanayi (1–2 kat)"),
                (CityMap.RailBedColor, "Tren yolu"),
                (CompanyProfile.Primary, "Merkez ofis"),
            };
            var r = LegendRect(sh);
            float rowH = 19f, w = r.width;
            UITheme.Shadowed(r, new Color(0.07f, 0.075f, 0.09f, 0.92f), 12f, 4f);
            small.normal.textColor = UITheme.Yazi(UITheme.Text);
            GUI.Label(new Rect(r.x + 12, r.y + 6, w - 24, 20), "Lejant", small);
            for (int i = 0; i < items.Count; i++)
            {
                float yy = r.y + 28f + i * rowH;
                UITheme.Box(new Rect(r.x + 12, yy + 3, 14, 13), items[i].c, 3f);
                small.normal.textColor = UITheme.Yazi(UITheme.TextDim);
                GUI.Label(new Rect(r.x + 34, yy, w - 44, rowH), items[i].t, small);
            }
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

        Rect PopupRect(float sw, float sh) => PhoneRect(sw, sh);

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
                           !(popupCourier != null && PopupRect(sw, sh).Contains(m)) &&
                           !(InfoOpen && InfoRect(sw, sh).Contains(m)) &&
                           !(TrackerVisible && GoalTrackerRect().Contains(m)) &&
                           !ToastAt(m, sw) &&
                           !(heatMenuOpen && HeatMenuRect(sh).Contains(m)) &&
                           !(!UiAktif && UyariGorunur && UyariRect(sw).Contains(m)) &&
                           !new Rect(DockW + 10f, sh - 44f, 560f, 30f).Contains(m) &&
                           !UiUzerinde(m);   // UI Toolkit elemanı (üst çubuk, menü, pencere, olaylar...)
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
                        popupCourier = c;
                        popupScroll = Vector2.zero;
                        CloseInfo();
                        e.Use();
                    }
                    else if (HandleInfoClick(m)) e.Use();
                }
            }
        }

        void DrawZoomButtons(float sh)
        {
            var mc = MapCamera.I;
            if (mc == null) return;
            float x = DockW + 10f, y = sh - 44f;
            if (GUI.Button(new Rect(x, y, 34, 30), "+", button)) mc.ZoomStep(1.5f);
            if (GUI.Button(new Rect(x + 38, y, 34, 30), "−", button)) mc.ZoomStep(-1.5f);
            if (GUI.Button(new Rect(x + 76, y, 34, 30), "◄", button)) mc.Rotate(-45f);
            if (GUI.Button(new Rect(x + 114, y, 34, 30), "►", button)) mc.Rotate(45f);
            if (GUI.Button(new Rect(x + 152, y, 60, 30), "Tümü", smallButton)) mc.ResetView();
            if (GUI.Button(new Rect(x + 216, y, 66, 30), legendOpen ? "Lejant ✓" : "Lejant", smallButton)) legendOpen = !legendOpen;
            if (GUI.Button(new Rect(x + 286, y, 88, 30), mc.FlatMode ? "3D görünüm" : "Düz harita", smallButton)) mc.ToggleFlatMode();
            if (GUI.Button(new Rect(x + 378, y, 150, 30), HeatButtonText, smallButton)) heatMenuOpen = !heatMenuOpen;
            // Kısayollar ekranda yazı olarak durmasın: "?" üstüne gelince görünür. Katman açıkken onun açıklaması yazar.
            HelpBadgeAt(new Rect(x + 536, y + 5, 20, 20),
                "Harita kısayolları\nTekerlek: yakınlaş · sol sürükle: kaydır · sağ sürükle ya da Q/E: döndür\n" +
                "N: kuzey · M: düz harita · 0: tüm harita · H: harita katmanları (sipariş, kaçan, teslimat süresi, kapsama, dükkan kârı)\nBoşluk: duraklat · 1–8: hız · Esc: menü · F10: fotoğraf modu (arayüzü gizler)");
            if (heatMode != HeatMode.Kapali)
            {
                string hint = HeatCaption;
                float hw = small.CalcSize(new GUIContent(hint)).x + 16f;
                var r = new Rect(x + 564, y + 3, hw, 24);
                UITheme.Box(r, new Color(0.05f, 0.055f, 0.07f, 0.75f), 12f);
                GUI.Label(new Rect(r.x + 6, r.y + 2, r.width - 8, r.height), hint, small);
            }
        }

        /// <summary>Kurye panelindeki sipariş kutusu: içerik, adres, kat, nakit, kalan süre.</summary>
        void WorldLabel(Camera cam, Vector3 world, string text, Color color)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) return;
            float x = sp.x / uiScale;
            float y = (Screen.height - sp.y) / uiScale;
            var r = new Rect(x - 90f, y - 10f, 180f, 20f);
            // Okunurluk için yarı saydam hap zemin
            float tw = worldLabel.CalcSize(new GUIContent(text)).x + 14f;
            UITheme.Box(new Rect(x - tw / 2f, y - 9f, tw, 18f), new Color(0.05f, 0.055f, 0.07f, 0.72f), 9f);
            worldLabel.normal.textColor = UITheme.Yazi(color);
            GUI.Label(r, text, worldLabel);
        }

        // ---------------------------------------------------------------- üst bar
        GUIStyle topCaption, topValue, topName;

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

        /// <summary>Üst bardaki bilgi kartı: renkli simge dairesi, küçük başlık, kalın değer. Genişliğini döndürür.</summary>
        float StatCard(float x, Icons.Kind icon, Color tint, string caption, string value, Color? bg = null, Color? valueColor = null, string tip = null)
        {
            float textW = Mathf.Max(topCaption.CalcSize(new GUIContent(caption)).x, topValue.CalcSize(new GUIContent(value)).x);
            float w = 44f + textW + 14f;
            var r = new Rect(x, 9f, w, TopBar - 18f);
            UITheme.Box(r, bg ?? UITheme.WithAlpha(Color.white, 0.055f), 12f);
            var ic = new Rect(r.x + 7f, r.center.y - 14f, 28f, 28f);
            UITheme.Box(ic, UITheme.WithAlpha(tint, 0.18f), 14f);
            Icons.Draw(new Rect(ic.x + 6f, ic.y + 6f, 16f, 16f), icon, tint);
            topCaption.normal.textColor = UITheme.Yazi(UITheme.TextDim);
            GUI.Label(new Rect(r.x + 42f, r.y + 3f, textW + 10f, 14f), caption, topCaption);
            topValue.normal.textColor = UITheme.Yazi(valueColor ?? UITheme.Text);
            GUI.Label(new Rect(r.x + 42f, r.y + 16f, textW + 10f, 20f), value, topValue);
            if (tip != null) HoverTip(r, tip);
            return w;
        }

        /// <summary>Uyarı hapı (boost, vergi borcu, aile borcu): dolu renkli, simgeli.</summary>
        float AlertPill(float x, Icons.Kind icon, string text, Color bg, Color fg)
        {
            float w = topValue.CalcSize(new GUIContent(text)).x + 44f;
            var r = new Rect(x, 13f, w, TopBar - 26f);
            UITheme.Box(r, bg, 17f);
            Icons.Draw(new Rect(r.x + 11f, r.center.y - 8f, 16f, 16f), icon, fg);
            topValue.normal.textColor = UITheme.Yazi(fg);
            GUI.Label(new Rect(r.x + 32f, r.y, w - 36f, r.height), text, topValue);
            return w;
        }

        void DrawTopBar(float sw)
        {
            var clock = GameClock.I;
            var eco = EconomyManager.I;
            var weather = WeatherSystem.I;
            var prim = CompanyProfile.Primary;
            var sec = CompanyProfile.Secondary;

            if (topCaption == null)
            {
                topCaption = UITheme.Semi(new GUIStyle(small) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, wordWrap = false });
                topValue = UITheme.Semi(new GUIStyle(small) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false });
                topName = UITheme.Semi(new GUIStyle(small) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false });
            }

            // Zemin: koyu, altında şirket renginde ince şerit ve yumuşak gölge
            Fill(new Rect(0, 0, sw, TopBar), new Color(0.06f, 0.065f, 0.08f, 0.97f));
            Fill(new Rect(0, TopBar - 2, sw, 2), prim);
            for (int i = 0; i < 6; i++) Fill(new Rect(0, TopBar + i, sw, 1), new Color(0f, 0f, 0f, 0.22f * (1f - i / 6f)));

            // Logo + şirket adı (simge çubuğunun hizasında)
            var logo = new Rect(17, 12, 36, 36);
            UITheme.Box(logo, prim, 11f);
            barText.normal.textColor = sec;
            barText.fontSize = 19;
            GUI.Label(logo, CompanyProfile.Initial, barText);
            barText.fontSize = 12;
            topName.normal.textColor = UITheme.Yazi(UITheme.Text);
            float nameW = Mathf.Min(220f, topName.CalcSize(new GUIContent(CompanyProfile.Name)).x);
            GUI.Label(new Rect(66, 0, nameW + 4, TopBar), CompanyProfile.Name, topName);

            // Hız seçici genişliği (sağda). 64× ve 128× klavyeden (7, 8); seçiliyse ayrı bir kutuda görünür.
            float[] speeds = { 1f, 2f, 4f, 8f, 16f, 32f };
            bool ekHiz = !clock.Paused && clock.SpeedMultiplier > 32.5f;
            float segW = 40f, speedW = 46f + 8f + (speeds.Length + (ekHiz ? 1 : 0)) * segW + 8f;
            float maxX = sw - speedW - 20f;

            // Bilgi kartları
            float x = Mathf.Max(DockW + 14f, 66f + nameW + 22f);
            void Add(System.Func<float, float> draw, float estimate)
            {
                if (x + estimate > maxX) return;
                x += draw(x) + 8f;
            }
            float Est(string cap, string val) => 58f + Mathf.Max(topCaption.CalcSize(new GUIContent(cap)).x, topValue.CalcSize(new GUIContent(val)).x);

            // Ana bilgiler: kasa, harcanabilir para, itibar (eğilimiyle), çevrimiçi kurye, tarih+saat+hava (tek kutu)
            string money = Ft(eco.money), free = Ft(eco.FreeCash);
            Add(px => StatCard(px, Icons.Kind.Wallet, new Color(0.40f, 0.85f, 0.55f), "KASA", money, null, null,
                               "Şirketin bütün parası. Kuryelerin hesaplarındaki hakedişler ayın 15'inde ve son günü bu paradan ödenir."), Est("KASA", money));
            bool freeNeg = eco.FreeCash < 0;
            Add(px => StatCard(px, Icons.Kind.Coins, freeNeg ? UITheme.Bad : new Color(0.45f, 0.75f, 1f), "HARCANABİLİR", free,
                               freeNeg ? UITheme.WithAlpha(UITheme.Bad, 0.18f) : (Color?)null, freeNeg ? UITheme.Bad : (Color?)null,
                               "Kuryelere ödenecek hakedişler düşülünce kalan para. Harcamalarını buna göre yap."), Est("HARCANABİLİR", free));
            var omr = OrderManager.I;
            float repFark = omr != null && omr.reputationAtDayStart >= 0f ? CompanyProfile.Reputation - omr.reputationAtDayStart : 0f;
            string rep = $"{CompanyProfile.Reputation:0.0} / 5" + (repFark > 0.01f ? " ▲" : repFark < -0.01f ? " ▼" : "");
            Add(px => StatCard(px, Icons.Kind.Star, new Color(1f, 0.80f, 0.25f), "İTİBAR", rep, null, null,
                               $"Son 14 günün müşteri puanı ortalaması (iptal 1 yıldız sayılır). Bugün {(repFark >= 0 ? "+" : "−")}{Mathf.Abs(repFark):0.00}. " +
                               "İtibar yükseldikçe sipariş artar."), Est("İTİBAR", rep));
            string cour = $"{CourierManager.I.OnlineCount} / {CourierManager.I.Couriers.Count}";
            Add(px => StatCard(px, Icons.Kind.Couriers, prim, "ÇEVRİMİÇİ", cour, null, null, "Şu an çevrimiçi kurye / toplam kurye."), Est("ÇEVRİMİÇİ", cour));
            string time = $"{clock.Hour:00}:{clock.Minute:00} · {weather.Name}";
            string date = $"{clock.DayName.ToUpperInvariant()} · {clock.DayOfMonth} {clock.MonthName.ToUpperInvariant()}";
            Add(px => StatCard(px, WeatherIcon(weather.Current), WeatherTint(weather.Current), date, time, null, null,
                               $"{DayNight.PhaseName} · {clock.SeasonName} · Yıl {clock.Year} · şirketin {clock.CompanyDay}. günü"), Est(date, time));

            // Uyarılar: önce acil olanlar (çökme, vergi borcu, kriz, fiyat savaşı), sonra boost, aile borcu, pazar payı
            if (CrisisManager.OrdersBlocked)
            {
                string t = $"Uygulama çöktü · {CrisisManager.OutageMinutesLeft:0} dk";
                Add(px => AlertPill(px, Icons.Kind.Warning, t, UITheme.Bad, Color.white), topValue.CalcSize(new GUIContent(t)).x + 44f);
            }
            if (eco.taxDebt > 0)
            {
                string t = $"Vergi borcu {Ft(eco.taxDebt)}";
                Add(px => AlertPill(px, Icons.Kind.Warning, t, UITheme.Bad, Color.white), topValue.CalcSize(new GUIContent(t)).x + 44f);
            }
            if (CrisisManager.I != null)
                foreach (var ce in CrisisManager.I.Effects)
                {
                    string t = $"{ce.title} · {CrisisManager.I.DaysLeft(ce)} g";
                    Add(px => AlertPill(px, Icons.Kind.Warning, t, UITheme.WithAlpha(new Color(1f, 0.62f, 0.20f), 0.35f), UITheme.Text), topValue.CalcSize(new GUIContent(t)).x + 44f);
                }
            if (RivalCompany.I != null && RivalCompany.I.priceWarDays > 0 && RivalCompany.I.warDistrict > 0)
            {
                string t = $"Fiyat savaşı: {CityMap.DistrictNames[RivalCompany.I.warDistrict]} · {RivalCompany.I.priceWarDays} g";
                Add(px => AlertPill(px, Icons.Kind.Flag, t, UITheme.WithAlpha(UITheme.Bad, 0.6f), Color.white), topValue.CalcSize(new GUIContent(t)).x + 44f);
            }
            var boost = CampaignManager.I.ActiveWindow;
            float wBoost = CampaignManager.I.WeatherBoostPercent;
            var orange = new Color(1f, 0.62f, 0.20f);
            if (boost != null && boost.percent >= wBoost)
            {
                string t = $"Boost %{boost.percent * 100f:0} · {boost.TimeText}";
                Add(px => AlertPill(px, Icons.Kind.Bolt, t, orange, new Color(0.12f, 0.08f, 0.02f)), topValue.CalcSize(new GUIContent(t)).x + 44f);
            }
            else if (wBoost > 0f)
            {
                string t = $"Hava boostu %{wBoost * 100f:0}";
                Add(px => AlertPill(px, Icons.Kind.Bolt, t, orange, new Color(0.12f, 0.08f, 0.02f)), topValue.CalcSize(new GUIContent(t)).x + 44f);
            }
            if (FamilyLoan.I != null && !FamilyLoan.I.Finished)
            {
                string t = FamilyLoan.I.TopBarText;
                Add(px => AlertPill(px, Icons.Kind.Heart, t, UITheme.WithAlpha(Color.white, 0.08f), UITheme.Text), topValue.CalcSize(new GUIContent(t)).x + 44f);
            }
            if (GameClock.I.NightFastActive && !GameClock.I.Paused)
            {
                string t = $"Gece hızlı · {GameClock.I.EffectiveSpeed:0}×";
                Add(px => AlertPill(px, Icons.Kind.Moon, t, new Color(0.22f, 0.26f, 0.45f), Color.white), topValue.CalcSize(new GUIContent(t)).x + 44f);
            }
            // Pazar payı (rakibe karşı)
            if (RivalCompany.I != null)
            {
                var rvc = RivalCompany.I;
                bool noData = rvc.myDeliveriesLastWeek + rvc.deliveriesLastWeek == 0;   // ilk hafta: henüz ölçülmedi
                float sh = rvc.MarketShare, tr = rvc.ShareTrend;
                string arrow = tr > 0.005f ? " ▲" : tr < -0.005f ? " ▼" : "";
                string t = noData ? $"Pazar payı: ilk hafta sonunda · rakip {rvc.companyName}"
                                  : $"Pazar payı %{sh * 100f:0}{arrow} · {rvc.companyName} %{(1f - sh) * 100f:0}";
                Color bg = sh >= 0.55f ? UITheme.WithAlpha(new Color(0.40f, 0.85f, 0.55f), 0.22f)
                         : sh >= 0.42f ? UITheme.WithAlpha(orange, 0.30f) : UITheme.Bad;
                Add(px => AlertPill(px, Icons.Kind.Chart, t, bg, UITheme.Text), topValue.CalcSize(new GUIContent(t)).x + 44f);
            }

            // Hız seçici: oynat/duraklat düğmesi + hız segmentleri
            float bx = sw - speedW - 12f;
            var group = new Rect(bx, 9f, speedW, TopBar - 18f);
            UITheme.Box(group, UITheme.WithAlpha(Color.white, 0.055f), 12f);
            var pp = new Rect(bx + 4f, group.y + 4f, 46f, group.height - 8f);
            bool paused = clock.Paused;
            bool ppHover = pp.Contains(Event.current.mousePosition);
            UITheme.Box(pp, paused ? UITheme.Warn : (ppHover ? UITheme.CardHi : UITheme.WithAlpha(Color.white, 0.08f)), 9f);
            Icons.Draw(new Rect(pp.center.x - 8f, pp.center.y - 8f, 16f, 16f), paused ? Icons.Kind.Play : Icons.Kind.Pause,
                       paused ? new Color(0.12f, 0.08f, 0.02f) : UITheme.Text);
            if (GUI.Button(pp, GUIContent.none, GUIStyle.none)) clock.Paused = !clock.Paused;

            float sx = pp.xMax + 8f;
            for (int i = 0; i < speeds.Length; i++)
            {
                float sp = speeds[i];
                var r = new Rect(sx + i * segW, group.y + 4f, segW - 2f, group.height - 8f);
                bool on = IsSpeed(clock, sp);
                bool hover = r.Contains(Event.current.mousePosition);
                if (on) UITheme.Box(r, prim, 9f);
                else if (hover) UITheme.Box(r, UITheme.WithAlpha(Color.white, 0.07f), 9f);
                barText.normal.textColor = on ? sec : UITheme.Yazi((paused ? UITheme.WithAlpha(UITheme.TextDim, 0.6f) : UITheme.TextDim));
                GUI.Label(r, sp + "×", barText);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) SetSpeed(sp);
            }
            if (ekHiz)
            {
                var r = new Rect(sx + speeds.Length * segW, group.y + 4f, segW - 2f, group.height - 8f);
                UITheme.Box(r, prim, 9f);
                barText.normal.textColor = sec;
                GUI.Label(r, clock.SpeedMultiplier.ToString("0") + "×", barText);
                HoverTip(r, "Klavyeden seçilen hız (6–8). Normal hızlar için soldakilerden birine bas.");
            }
        }

        static bool IsSpeed(GameClock c, float s) => !c.Paused && Mathf.Approximately(c.SpeedMultiplier, s);

        // ---------------------------------------------------------------- simge çubuğu ve pencere

        static readonly (Tab tab, Icons.Kind icon, string label)[] DockItems =
        {
            // Gruplar (aralarında ince çizgi): günlük takip · ekip · satış · şirket
            (Tab.Siparisler, Icons.Kind.Orders, "Siparişler"),
            (Tab.Mesajlar, Icons.Kind.Star, "Yorumlar"),
            (Tab.Hedefler, Icons.Kind.Flag, "Hedefler"),
            (Tab.Kuryeler, Icons.Kind.Couriers, "Kuryeler"),
            (Tab.IseAlim, Icons.Kind.Hiring, "İşe Alım"),
            (Tab.Ekipman, Icons.Kind.Equipment, "Ekipman"),
            (Tab.Destek, Icons.Kind.Support, "Destek"),
            (Tab.Dukkanlar, Icons.Kind.Shops, "Dükkanlar"),
            (Tab.Market, Icons.Kind.Basket, "Market"),
            (Tab.Kampanyalar, Icons.Kind.Campaigns, "Kampanya"),
            (Tab.Gelistirmeler, Icons.Kind.Upgrades, "Gelişim"),
            (Tab.Finans, Icons.Kind.Finance, "Finans"),
            (Tab.Istatistik, Icons.Kind.Chart, "Grafik"),
            (Tab.Ayarlar, Icons.Kind.Settings, "Ayarlar"),
        };

        /// <summary>Simge çubuğunda yeni grubun ilk simgeleri (üstlerine ince çizgi çekilir).</summary>
        static bool StartsDockGroup(Tab t) => t == Tab.Kuryeler || t == Tab.Dukkanlar || t == Tab.Gelistirmeler;

        GUIStyle dockLabel, windowTitle;

        static Icons.Kind IconOf(Tab t)
        {
            foreach (var d in DockItems) if (d.tab == t) return d.icon;
            return Icons.Kind.Orders;
        }

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
                case Tab.IseAlim: return CourierManager.I != null ? CourierManager.I.Applications.Count : 0;
                case Tab.Mesajlar: return CourierMessages.I != null ? CourierMessages.I.Unread : 0;
                default: return 0;
            }
        }

        /// <summary>Soldaki simge çubuğu ve tıklanınca açılan pencere.</summary>
        void DrawPanel(float sh)
        {
            if (dockLabel == null)
            {
                dockLabel = new GUIStyle(small) { alignment = TextAnchor.UpperCenter, fontSize = 11, wordWrap = false, clipping = TextClipping.Overflow };
                windowTitle = new GUIStyle(header) { fontSize = 19, alignment = TextAnchor.MiddleLeft };
            }

            bool eski = !UiAktif;
            dockTip = null;
            if (eski)
            {
                // Çubuk
                var bar = new Rect(0, TopBar, DockW, sh - TopBar);
                Fill(bar, UITheme.Bg);
                Fill(new Rect(bar.xMax - 1, bar.y, 1, bar.height), UITheme.Line);
                DrawMapGap(sh);   // harita pencerenin sağına kaydıysa aradaki şerit

                // Sol çubuk: 6 ana menü + Ayarlar (bkz. MenuGroups.cs)
                dockTip = null;
                DrawGroupDock(sh);
            }

            if (!panelOpen) return;

            // Pencere
            var w = WindowRect(sh);
            float headH = WindowHeaderHeight();
            if (eski)
            {
                UITheme.Shadowed(w, UITheme.Bg, 14f, 8f);
                UITheme.Outline(w, UITheme.Line, 1f, 14f);
                // Başlık: menü adı, kapat ve sekme şeridi (bkz. MenuGroups.cs)
                DrawWindowHeader(w);
            }
            else
            {
                // Yeni arayüz: başlık ve sekmeler UI Toolkit'te. Sekme taşındıysa içerik de orada; taşınmadıysa
                // gövdenin zemini ve içeriği burada (IMGUI) çizilir, başlıkla üst üste binmez.
                if (UiSekmeVar(tab)) return;
                var govde = new Rect(w.x, w.y + headH + 1f, w.width, w.height - headH - 1f);
                Fill(new Rect(govde.x, govde.y, govde.width, Mathf.Max(0f, govde.height - 14f)), UITheme.Bg);
                UITheme.Box(new Rect(govde.x, govde.yMax - 28f, govde.width, 28f), UITheme.Bg, 14f);
            }

            GUILayout.BeginArea(new Rect(w.x + 12f, w.y + headH + 8f, w.width - 24f, w.height - headH - 16f));
            scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);   // yatay çubuk yok
            ResetTextColors();
            drawingWindow = true;

            switch (tab)
            {
                case Tab.Siparisler: DrawOrders(); break;
                case Tab.Kuryeler: DrawCouriers(); break;
                case Tab.IseAlim: DrawHiring(); break;
                case Tab.Dukkanlar: DrawShops(); break;
                case Tab.Ekipman: DrawEquipment(); break;
                case Tab.Kampanyalar: DrawCampaigns(); break;
                case Tab.Destek: DrawSupport(); break;
                case Tab.Gelistirmeler: DrawUpgrades(); break;
                case Tab.Finans: DrawFinance(); break;
                case Tab.Ayarlar: DrawSettings(); break;
                case Tab.Hedefler: DrawGoals(); break;
                case Tab.Mesajlar: DrawMessages(); break;
                case Tab.Market: DrawMarket(); break;
                case Tab.Istatistik: DrawStatistics(); break;
            }

            drawingWindow = false;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        GUIStyle orderIncomeStyle;
        readonly HashSet<int> assignListOpen = new HashSet<int>();

        void DrawOrders()
        {
            var om = OrderManager.I;
            var eco = EconomyManager.I;
            float now = GameClock.I.TotalMinutes;

            om.autoAssign = GUILayout.Toggle(om.autoAssign, " Otomatik atama (dispatcher)", toggle);
            if (UpgradeManager.I.SmartDispatch)
                om.prioritizeProfit = GUILayout.Toggle(om.prioritizeProfit, " Atamada kârı öncele (kapalıysa en hızlı kurye)", toggle);
            else
                GUILayout.Label("Basit atama: en yakın kuryeye teklif eder. Akıllı atama Gelişim menüsünde.", small);
            if (om.IsBusy)
                GUILayout.Label("YOĞUNLUK: bekleyen sipariş boştaki kuryeden fazla, bazı müşteriler sipariş vermiyor. Çözüm: daha çok kurye veya boost.", warn);
            int lostTotal = om.busyLostToday + om.lostToday + om.cancelledToday + om.heavyLostToday;
            Intro("siparisler", $"Bugün kaybedilen sipariş: {lostTotal} · toplu teslimat: {om.batchedToday}",
                  $"Yoğunluk yüzünden sipariş vermeyen {om.busyLostToday} · vazgeçen müşteri (ücret pahalı) {om.lostToday} · " +
                  $"iptal {om.cancelledToday} (kuryeler reddetti {om.cancelledRejectedToday}, müşteri gecikmeden vazgeçti {om.cancelledDelayToday}) · " +
                  $"dükkan iptal etti {om.cancelledShopToday} · müşteri fikrini değiştirdi {om.cancelledCustomerToday} · taşıyacak kurye olmadığı için alınamayan {om.heavyLostToday} · " +
                  $"kuryelerin reddettiği teklif {om.rejectedToday}.\n" +
                  "Yoğunlukta uygulama müşterilere uzun teslimat uyarısı gösterir; itibar düşmez ama sipariş kaybolur.\n" +
                  $"Ağırlık: 20 kg'a kadar bisiklet, scooter, motor ve araba; {GameData.HeavyKg:0} kg ve üstü sadece araba.");

            GUILayout.Space(6);
            Section($"Bekleyen siparişler ({om.Pending.Count})");
            int shown = 0;
            if (orderIncomeStyle == null) orderIncomeStyle = UITheme.Semi(new GUIStyle(label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight, wordWrap = false });
            orderIncomeStyle.normal.textColor = UITheme.Yazi(UITheme.Good);
            foreach (var o in om.Pending)
            {
                if (shown++ >= 12) break;
                GUILayout.BeginVertical(box);
                bool open = CardOpen(o);
                float minsLeft = Mathf.Max(0f, o.createdAt + om.pendingTimeout - now);

                // 1. satır: sipariş ve gelir · 2. satır: kısa bilgi
                GUILayout.BeginHorizontal();
                GUILayout.Label($"#{o.id} {o.shop.Name}", header);
                GUILayout.FlexibleSpace();
                GUILayout.Label(Ft(o.CompanyIncome), orderIncomeStyle);
                GUILayout.EndHorizontal();
                GUILayout.Label($"{GameData.KmText(o.distance)} · {GameData.KgText(o.weightKg)} · {o.CustomerTypeName}", small);
                Chip($"İptale {minsLeft:0} dk", minsLeft <= 5f ? UITheme.Bad : minsLeft <= 10f ? UITheme.Warn : NeutralTone);
                if (o.IsHeavy) Chip("Ağır: sadece araba", UITheme.Warn);
                if (o.cashPay) Chip("Nakit", UITheme.Warn);
                if (o.UsesStairs && o.floor >= 4) Chip($"{o.floor}. kat, merdiven", UITheme.Warn);
                if (o.priceDeclines > 0) Chip($"{o.priceDeclines} kurye reddetti", UITheme.Warn);
                ChipRow();

                // Atanabilecek kuryeler: en kârlısı tek düğme, diğerleri açılır listede
                Courier best = null;
                int bestP = int.MinValue, eligible = 0;
                int need = eco.RequiredAssignProfit(o);
                foreach (var c in CourierManager.I.Couriers)
                {
                    if (!c.CanTakeMore || !c.CanCarry(o)) continue;
                    if (o.cashPay && !c.CanTakeCash(o.CashAmount)) continue;
                    eligible++;
                    if (o.rejectedBy.Contains(c)) continue;
                    int p = eco.ProfitIf(o, c);
                    if (p >= need && p > bestP) { bestP = p; best = c; }
                }
                bool listOpen = assignListOpen.Contains(o.id);
                GUILayout.BeginHorizontal();
                if (best != null)
                {
                    var order = o; var courier = best;
                    GUI.enabled = modalFree;
                    if (IconButton(Icons.Of(best.vehicle), $"Ata: {best.ShortName} · kâr +{Ft(bestP)}", button)) deferred = () => om.Assign(order, courier, true);
                }
                else GUILayout.Label(eligible == 0 ? (o.IsHeavy ? "Müsait arabalı kurye yok." : "Müsait kurye yok.") : "Kâr eden kurye yok (detay için listeye bak).", small);
                GUI.enabled = modalFree;
                if (eligible > 1 || (eligible == 1 && best == null))
                    if (GUILayout.Button(listOpen ? "Listeyi kapat" : $"Diğer kuryeler ({eligible})", smallButton, GUILayout.Width(120)))
                    {
                        int id = o.id;
                        deferred = () => { if (!assignListOpen.Remove(id)) assignListOpen.Add(id); };
                    }
                DetailButton(o);
                GUILayout.EndHorizontal();

                if (listOpen)
                {
                    foreach (var c in CourierManager.I.Couriers)
                    {
                        if (!c.CanTakeMore || !c.CanCarry(o)) continue;
                        if (o.cashPay && !c.CanTakeCash(o.CashAmount)) continue;
                        bool rejected = o.rejectedBy.Contains(c);
                        int p = eco.ProfitIf(o, c);
                        int boostCost = CampaignManager.I.BoostAmount(eco.CourierPay(c, o.distance));
                        int courierGets = eco.CourierPay(c, o.distance) + boostCost;
                        string mood = c.LikesOffer(o, courierGets) ? "" : " · beğenmeyebilir";
                        string txt = rejected ? $"{c.ShortName} · reddetti"
                                   : p >= need ? $"{c.ShortName} · kurye {Ft(courierGets)} · kâr +{Ft(p)}{mood}"
                                   : p >= 0 ? $"{c.ShortName} · kâr az ({Ft(p)})"
                                   : $"{c.ShortName} · zarar {Ft(p)}";
                        GUI.enabled = modalFree && p >= need && !rejected;
                        var order = o; var courier = c;
                        if (IconButton(Icons.Of(c.vehicle), txt, smallButton)) deferred = () => om.Assign(order, courier, true);
                        GUI.enabled = modalFree;
                    }
                }

                if (open)
                {
                    GUILayout.Space(4);
                    GUILayout.Label($"{o.customerName} — {o.AddressText}", small);
                    GUILayout.Label($"{o.BuildingText}", small);
                    GUILayout.Label($"Sipariş ({o.ItemCount} ürün): {o.ItemsText}", small);
                    if (!string.IsNullOrEmpty(o.customerNote)) GUILayout.Label($"Not: \"{o.customerNote}\"", note);
                    if (o.priceDeclines > 0)
                        GUILayout.Label("Kuryeler kazancı beğenmedi: bonus çarpanı ya da boost kabul edilmesini kolaylaştırır.", small);
                    if (o.shopQueue) GUILayout.Label("Önceki kurye dükkandaki sıra yüzünden bıraktı.", small);
                    if (o.cashPay)
                        GUILayout.Label($"Nakit: müşteri {Ft(o.cashGiven)} verecek, tutar {Ft(o.CashAmount)}, para üstü {Ft(o.CashChange)}. " +
                                        "Kurye nakdi şirkete borçlanır.", small);
                    GUILayout.Label($"Sepet {Ft(o.basket)} · Komisyon {Ft(o.commissionIncome)} + Teslimat ücreti {Ft(o.deliveryFee)} = Gelir {Ft(o.CompanyIncome)}", small);
                    GUILayout.Label($"Teslimat ücreti: taban {Ft(o.feeBase)} + {GameData.KmText(o.distance)} × {Ft(eco.feePerKm)} = {Ft(o.feeKm)}" +
                                    (o.feeWeather > 0 ? $" + kötü hava {Ft(o.feeWeather)}" : "") +
                                    (o.feeExtra > 0 ? $" + ek {Ft(o.feeExtra)} (kurye ücretini karşılamak için)" : ""), small);
                }
                GUILayout.EndVertical();
            }

            GUILayout.Space(6);
            int robotYolda = RobotFilo.I != null ? RobotFilo.I.Yolda : 0;
            Section(robotYolda > 0 ? $"Yolda ({om.Active.Count} + {robotYolda} robotta)" : $"Yolda ({om.Active.Count})");
            if (RobotFilo.I != null && RobotFilo.I.Robotlar.Count > 0) GUILayout.Label(RobotFilo.I.DurumMetni(), small);
            foreach (var c in CourierManager.I.Couriers)
            {
                if (c.OrderCount == 0) continue;
                string ids = c.CurrentOrder != null ? "#" + c.CurrentOrder.id : "";
                foreach (var q in c.QueuedOrders) ids += " #" + q.id;
                IconLabel(Icons.Of(c.vehicle), $"{c.ShortName} {c.OrderCount}/{c.EffectiveCapacity} paket (çantada {c.CarryingCount}): {c.StateName} — {ids}", small);
            }

            GUILayout.Space(6);
            Section("Son teslimatlar");
            for (int i = 0; i < Mathf.Min(8, om.History.Count); i++)
            {
                var o = om.History[i];
                string tip = o.tip > 0 ? $" · bahşiş {Ft(o.tip)}" : "";
                string issue = o.keptByCourier ? " · müşteriye ulaşılamadı, kuryede kaldı"
                             : o.addressIssue == AddressIssue.YanlisAdres ? " · adres yanlıştı, çözüldü"
                             : o.addressIssue == AddressIssue.EvdeYok ? " · müşteri evde yoktu, çözüldü" : "";
                if (o.batched) issue += " · toplu";
                if (o.deliveredCold) issue += " · soğuk gitti";
                string stars = o.keptByCourier ? "puan yok" : $"{o.rating:0.0} yıldız";
                string kim = o.courier != null ? o.courier.ShortName : o.robot ? "robot" : "-";
                if (o.dolap) issue += " · paket dolabına";
                GUILayout.Label($"#{o.id} {o.shop.Name} → {kim} · {GameData.KmText(o.distance)} · " +
                                $"{stars} · kurye {Ft(o.courierPay)} · kâr {Ft(o.Profit)}{tip}{issue}",
                                o.keptByCourier ? warn : small);
            }
        }

        void DrawCouriers()
        {
            var cm = CourierManager.I;
            Chip($"{cm.Couriers.Count}/{UpgradeManager.I.CourierCap} hesap", UpgradeManager.I.CourierCapFull ? UITheme.Warn : NeutralTone);
            Chip($"{cm.OnlineCount} çevrimiçi", cm.OnlineCount > 0 ? UITheme.Good : NeutralTone);
            if (cm.Couriers.Count > 0)
            {
                // Ekibin hali tek bakışta: ortalama moral, yorgunluğu birikmiş ve bırakmayı düşünen kurye
                float om = 0f; int yorgun = 0, birakacak = 0;
                foreach (var c in cm.Couriers)
                {
                    om += c.morale;
                    if (c.burnout >= 0.5f) yorgun++;
                    if (c.morale < Denge.BirakmaMoralSiniri) birakacak++;
                }
                om /= cm.Couriers.Count;
                Chip($"ort. moral %{om * 100f:0}", om >= 0.6f ? UITheme.Good : om >= 0.4f ? UITheme.Warn : UITheme.Bad);
                if (yorgun > 0) Chip($"{yorgun} kurye yorgun", UITheme.Warn);
                if (birakacak > 0) Chip($"{birakacak} kurye bırakabilir", UITheme.Bad);
            }
            ChipRow();
            Intro("kuryeler", "Kuryeler serbest çalışır, istedikleri saatte çevrimiçi olurlar. Saatte iyi kazanınca mutlu olurlar; uzun günler yorgunluk biriktirir.",
                  "Asıl mesleklerine (öğrenci, ofis çalışanı, vardiyalı işçi...), yorgunluklarına ve günlük hayatlarına göre " +
                  "çevrimiçi/çevrimdışı olurlar; onları zorlayamazsın. Kendi araçlarını ve sigortalarını kendi kazançlarından öderler; " +
                  "para biriktirince daha iyi araç alırlar. Kartlar kısa görünür: her şeyi görmek için \"Detay\".");

            // Araç filtresi sadece kalabalık listede; sağında Kartlar / Tablo seçimi
            bool viewToggle = cm.Couriers.Count >= 3;
            if (cm.Couriers.Count >= 5) VehicleFilterBar(courierFilter, CourierCountByVehicle, cm.Couriers.Count, f => courierFilter = f,
                                                         viewToggle ? new System.Action(CourierViewToggle) : null);
            else
            {
                courierFilter = -1;
                if (viewToggle)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    CourierViewToggle();
                    GUILayout.EndHorizontal();
                }
            }
            if (courierFilter >= 0 && CourierCountByVehicle((VehicleKind)courierFilter) == 0)
                GUILayout.Label($"{GameData.Vehicle((VehicleKind)courierFilter).name} ile çalışan kurye yok.", bodyDim);

            if (viewToggle && CourierTableView)
            {
                var list = new List<Courier>();
                foreach (var c in cm.Couriers) if (PassesFilter(courierFilter, c.vehicle)) list.Add(c);
                DrawCourierTable(list);
                return;
            }
            foreach (var c in cm.Couriers)
                if (PassesFilter(courierFilter, c.vehicle)) DrawCourierCard(c, false);
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

        /// <summary>Kurye kartının "Detay" kısmı: meslek, beceriler, hesap, sigorta, hijyen, hedefler.</summary>
        void DrawCourierDetails(Courier c)
        {
            var cm = CourierManager.I;
            GUILayout.Space(4);
            if (c.tipsTotal > 0)
                GUILayout.Label($"Bahşişler: toplam {Ft(c.tipsTotal)}" + (c.tipsToday > 0 ? $" (bugün {Ft(c.tipsToday)})" : "") + " — tamamı kuryenin", small);
            GUILayout.Label($"Meslek: {c.OccupationName} · Bugün: {c.OccupationStatus}" + (c.dayOff ? " · izin günü" : ""), small);
            if (c.occupation == Occupation.Ogrenci)
                GUILayout.Label($"Bu dönem ders programı: {Courier.ScheduleText(c.scheduleSeed, GameClock.I)}", small);

            string capNote = "";
            if (c.morale >= 0.8f) capNote += ", moral yüksek +1";
            if (c.morale < 0.35f) capNote += ", moral düşük −1";
            if (c.fatigue >= 0.6f) capNote += ", yorgun −1";
            GUILayout.Label($"Aynı anda taşıyabileceği: {c.EffectiveCapacity} paket (araç {c.V.capacity}{capNote})", small);
            SkillGrid(c.speedSkill, c.care, c.politeness, c.stamina);
            GUILayout.Label($"Kazanç: {Ft(c.V.basePay)}/paket + {Ft(c.V.payPerKm)}/km + bonus · Taşır: {GameData.CarryText(c.V)}", small);
            GUILayout.Label($"Araçları: {c.VehiclesText}", small);
            GUILayout.Label($"Hesabı: {Ft(c.balance)} (kesim {EconomyManager.I.NextCutText})" +
                            (c.pendingPayout > 0 ? $" · yatacak {Ft(c.pendingPayout)}" : "") +
                            $" · bugüne kadar yatan {Ft(c.receivedTotal)}" +
                            (c.earlyPayouts > 0 ? $" · erken ödeme {c.earlyPayouts} kez" : ""), small);
            if (c.cashHeld > 0 || c.cashBlocked)
                GUILayout.Label($"Üzerindeki nakit: {Ft(c.cashHeld)} / {Ft(Courier.CashLimit)}" +
                                (c.cashBlocked ? " · HESABI KİLİTLİ (nakit borcunu kapatana kadar sipariş alamaz)" : " · ay sonuna kadar kapatmalı"),
                                c.cashBlocked || c.cashHeld >= Courier.CashLimit * 0.7f ? warn : small);
            if (c.depositGear != null && c.depositGear.Count > 0 && (Sade.Depozito || c.depositPaidTotal > 0))
                GUILayout.Label($"Depozitolu ekipman: {string.Join(", ", c.depositGear.ConvertAll(k => GameData.Equip(k).name))} · " +
                                $"kesilen depozito {Ft(c.depositPaidTotal)}" + (c.depositOwed > 0 ? $", kesilecek {Ft(c.depositOwed)}" : "") +
                                " (ayrılırken iade ederse geri ödenir)", small);
            GUILayout.Label(c.insured ? $"Sigorta: var ({(c.insuredViaCompany ? EconomyManager.I.Plan.name : "dışarıdan")}, kendi öder, haftalık {Ft(c.CurrentPremium)}; bugüne kadar {Ft(c.insurancePaid)})"
                                      : "Sigorta: yok (kazada masrafları kendi öder, daha uzun süre çalışamaz)", small);
            var camp = CampaignManager.I;
            if (camp.weeklyChallenge)
            {
                if (c.weeklyRewardClaimed)
                    GUILayout.Label($"Haftalık hedef tamamlandı! ({Ft(camp.weeklyReward)} ödendi)", small);
                else
                    Bar("Haftalık", c.weekDeliveries / (float)Mathf.Max(1, camp.weeklyTarget), new Color(0.4f, 0.7f, 1f),
                        $"{c.weekDeliveries}/{camp.weeklyTarget}");
            }
            if (camp.monthlyChallenge)
            {
                if (c.monthlyRewardClaimed)
                    GUILayout.Label($"Aylık hedef tamamlandı! ({Ft(camp.monthlyReward)} ödendi)", small);
                else
                    Bar("Aylık", c.monthDeliveries / (float)Mathf.Max(1, camp.monthlyTarget), new Color(0.75f, 0.55f, 1f),
                        $"{c.monthDeliveries}/{camp.monthlyTarget}");
            }
            GUILayout.Label($"Ekipman: {EquipmentText(c)}", small);

            // Çanta hijyeni
            Color hc = c.hygiene >= 0.8f ? new Color(0.3f, 0.85f, 0.4f)
                     : c.hygiene >= Courier.HygieneLimit ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.95f, 0.35f, 0.3f);
            if (Sade.Hijyen) Bar("Hijyen", c.hygiene, hc);
            GUILayout.Label($"Teklif seçiciliği: {c.PickinessText} · Dükkanda sıra yüzünden iptal: bu ay {c.pickupCancelsThisMonth}/{Courier.MaxPickupCancels}",
                            c.pickupCancelsThisMonth >= Courier.MaxPickupCancels ? warn : small);
            if (Sade.Hijyen)
                GUILayout.Label($"Çanta: {c.HygieneText} · kendisi {TidyWord(c.tidiness)} · çanta kullanımı {c.bagUses}/{Courier.BagWornUses}" +
                                (c.cleanRequested ? " · Uyarıldı, paketi bitince temizleyecek" : ""), small);
            else
                GUILayout.Label($"Çanta kullanımı: {c.bagUses}/{Courier.BagWornUses}" + (c.BagWorn ? " · yıprandı, yenisi gerekiyor" : ""), c.BagWorn ? warn : small);
            GUILayout.BeginHorizontal();
            var hcour = c;
            GUI.enabled = modalFree && !c.cleanRequested && c.hygiene < c.MaxHygiene - 0.05f;
            if (Sade.Hijyen && GUILayout.Button("Temizlik için uyar", smallButton)) deferred = () => cm.RequestCleaning(hcour);
            GUI.enabled = modalFree && c.BagWorn;
            if (GUILayout.Button($"Yeni çanta (−{Ft(GameData.Equip(EquipmentKind.TermalCanta).price)})", smallButton)) deferred = () => cm.ReplaceBag(hcour);
            GUI.enabled = modalFree;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            var courier = c;
            if (GUILayout.Button("Nakit / ödeme", smallButton)) deferred = () => { popupCourier = courier; popupScroll = Vector2.zero; CloseInfo(); };
            GUI.enabled = modalFree && c.OrderCount == 0;
            if (GUILayout.Button(c.OrderCount == 0 ? "İşten çıkar" : "İşten çıkar (paketi var)", smallButton))
                deferred = () => AskConfirm("Kuryeyle yolları ayır",
                    $"{courier.courierName} ({courier.V.name}, {courier.deliveries} teslimat) işten çıkarılsın mı? " +
                    $"Hesabında bekleyen {Ft(courier.balance)} ve varsa depozitosu ödenir. Bu geri alınamaz.",
                    "Evet, işten çıkar", () => cm.Fire(courier));
            GUI.enabled = modalFree;
            GUILayout.EndHorizontal();
        }

        static string EquipmentText(Courier c)
        {
            if (c.equipment.Count == 0) return "yok";
            string s = "";
            foreach (var k in c.equipment) s += (s.Length > 0 ? ", " : "") + GameData.Equip(k).name;
            return s;
        }

        void DrawHiring()
        {
            var cm = CourierManager.I;
            float now = GameClock.I.TotalMinutes;
            var up = UpgradeManager.I;
            if (up.CourierCapFull)
                GUILayout.Label($"Kurye hesapları dolu ({cm.Couriers.Count}/{up.CourierCap}). {up.CourierCapHint}", warn);
            else
                KV("Kurye hesapları", $"{cm.Couriers.Count}/{up.CourierCap}");
            Section($"Gelen başvurular ({cm.Applications.Count}/{cm.maxApplications})");
            Intro("isealim", "Başvurular zamanla gelir ve 1 gün geçerlidir. İşe almak ve çıkarmak ücretsizdir.",
                  "İtibarın ve bonus çarpanın yüksekse başvuru daha sık gelir. Kuryeler paket ve km başı kazandığı için maaş yoktur; " +
                  "her yeni kuryeye başlangıç kiti verilir. Ama çok kurye = kişi başı az paket = düşük moral.");

            if (cm.Applications.Count == 0)
                GUILayout.Label("Şu an bekleyen başvuru yok. Yeni başvurular haber akışında duyurulur.", body);
            else
            {
                if (cm.Applications.Count >= 5) VehicleFilterBar(applicationFilter, ApplicationCountByVehicle, cm.Applications.Count, f => applicationFilter = f);
                else applicationFilter = -1;
                if (applicationFilter >= 0 && ApplicationCountByVehicle((VehicleKind)applicationFilter) == 0)
                    GUILayout.Label($"{GameData.Vehicle((VehicleKind)applicationFilter).name} ile başvuran yok.", small);
                WorkPartFilterBar(cm.Applications);   // saat filtresi (WorkHoursView.cs)
            }

            foreach (var cand in cm.Applications)
                if (PassesFilter(applicationFilter, cand.vehicle) && PassesWorkFilter(cand)) DrawCandidateCard(cand, now);

            // Destek personeli de buradan işe alınabilir (Destek menüsü açıldıktan sonra)
            if (!TabOpen(Tab.Destek)) return;
            GUILayout.Space(10);
            DrawSupportCandidates();
        }

        void DrawShops()
        {
            var map = CityMap.I;
            Section($"Açık bölge: {map.UnlockLevel}/{map.MaxLevel} — {CityMap.DistrictNames[map.UnlockLevel]}");
            if (map.UnlockLevel < map.MaxLevel)
            {
                int next = map.UnlockLevel + 1;
                if (GUILayout.Button($"Yeni bölge aç: {CityMap.DistrictNames[next]} — {Ft(CityMap.BolgeBedeli(next))}", button))
                    deferred = () => AskConfirm("Yeni bölge aç",
                        $"{CityMap.DistrictNames[next]} {Ft(CityMap.BolgeBedeli(next))} karşılığında açılsın mı? " +
                        $"Serbest kasan: {Ft(EconomyManager.I.FreeCash)}. Bölge açmak geri alınamaz.",
                        $"Evet, aç ({Ft(CityMap.BolgeBedeli(next))})", () => map.TryUnlockNext());
            }
            GUILayout.Space(6);

            // Rakip platform
            var rv = RivalCompany.I;
            if (rv != null)
            {
                GUILayout.BeginVertical(box);
                GUILayout.Label($"Rakip: {rv.companyName} (yapay zekâ)", header);
                HelpOnLast("Rakip de çalıştığı dükkanlarda siparişler itibar ve kurye sayısına göre paylaşılır. Münhasır dükkanlarda rakip yok. " +
                           "Sözleşmeler 6 ayda bir yenilenir; memnuniyet düşükse rakip araya girer. Moralsiz kuryelerini de ayartabilir.");
                GUILayout.Label($"İtibar {rv.reputation:0.0}/5{(rv.marketingDays > 0 ? $" (reklamla {rv.EffectiveReputation:0.0})" : "")} · " +
                                $"{rv.couriers} kurye · {rv.RivalShopCount} dükkanla çalışıyor · geçen hafta ~{rv.deliveriesLastWeek} teslimat", small);
                GUILayout.Label($"Pazar payın: %{rv.MarketShare * 100f:0} (geçen hafta {(rv.ShareTrend >= 0f ? "+" : "−")}{Mathf.Abs(rv.ShareTrend) * 100f:0.0} puan) · " +
                                $"rakibin tavrı: {rv.MoodText} · yatırım turu: {rv.fundingRounds}", small);
                GUILayout.Label($"Hizmet (son 7 gün): sen ort. {rv.myAvgMinutes:0} dk, %{rv.myLateRate * 100f:0} geç → müşteri gözünde ×{rv.MyService:0.00} · " +
                                $"{rv.companyName} ~{rv.rivalAvgMinutes:0} dk, %{rv.rivalLateRate * 100f:0} geç → ×{rv.RivalService:0.00} · " +
                                $"toplam talep ×{rv.ServiceDemandFactor:0.00}", small);
                HelpOnLast("Daha hızlı ve dakik teslimat yaptıkça paylaşılan dükkanlarda müşteriler rakipten sana geçer ve şehirde sipariş " +
                           "vermek isteyen insan sayısı artar. 40 dk ortalama nötr; her dakika hızlı ≈ +%2, geç kalan siparişler düşürür. " +
                           "Kurye fazlan varken siparişler toplu taşınmaz, boştaki kuryeye gider.");
                HelpOnLast("Pazar payın %45'in üstüne çıktıkça rakip saldırganlaşır: yatırım bulur, kurye alır, hizmetini iyileştirir; " +
                           "en iyi dükkanlarına ve kuryelerine teklif yapar, bir bölgede fiyat savaşı ya da reklam atağı başlatır. " +
                           "İtibarını yüksek tut, iyi dükkanlarla münhasırlık yap, kuryelerinin moralini koru.");
                if (rv.shareHistory.Count > 1)
                {
                    var hs = new System.Text.StringBuilder("Son haftalar: ");
                    foreach (var v in rv.shareHistory) hs.Append($"%{v * 100f:0} ");
                    GUILayout.Label(hs.ToString(), small);
                }
                if (rv.priceWarDays > 0 && rv.warDistrict > 0)
                    GUILayout.Label($"Fiyat savaşı: {rv.companyName} {CityMap.DistrictNames[rv.warDistrict]} bölgesinde {rv.priceWarDays} gün daha ücret kırıyor " +
                                    $"(oradaki siparişlerin −%{(1f - rv.warFactor) * 100f:0}).", warn);
                if (rv.marketingDays > 0) GUILayout.Label($"Reklam atağı: {rv.marketingDays} gün daha müşterilere daha iyi görünüyor.", warn);
                GUILayout.EndVertical();
            }

            // Krizler ve kalıcı kararlar
            if (CrisisManager.I != null)
            {
                GUILayout.BeginVertical(box);
                GUILayout.Label("Krizler ve kalıcı kararlar", header);
                HelpOnLast("Ara sıra kriz çıkar: kurye grevi, uygulama çökmesi, akaryakıt zammı, yazın sıcak dalgası, platform çalışanları yasası. " +
                           "Her birinde bir seçim yaparsın; sonucu günlerce ya da haftalarca sürer.");
                GUILayout.Label(CrisisManager.I.StatusText, small);
                GUILayout.EndVertical();
            }

            foreach (var s in map.Shops)
            {
                if (s.own) continue;   // kendi marketin: Market penceresinde
                GUILayout.BeginVertical(box);
                DrawShopCard(s, rv, true);
                GUILayout.EndVertical();
            }
        }

        /// <summary>Dükkan bilgisi ve sözleşme işlemleri (Dükkanlar penceresi ve haritada dükkana tıklayınca açılan kart).</summary>
        void DrawShopCard(Shop s, RivalCompany rv, bool withTitle)
        {
            if (s.own) { DrawOwnStoreSummary(withTitle); return; }
            var map = CityMap.I;
            if (withTitle)
            {
                GUILayout.Label(s.Name + (s.exclusive ? "  [Münhasır]" : s.contracted ? "  [Sözleşmeli]" : "") +
                                (s.rivalContracted ? $"  · {(rv != null ? rv.companyName : "rakip")} ile de çalışıyor" : ""), header);
            }
            bool openNow = ShopHours.IsOpen(s.def);
            GUILayout.Label($"{ShopHours.StatusText(s.def)} · {ShopHours.HoursText(s.def)}", openNow ? note : warn);
            GUILayout.Label($"Komisyon %{s.Commission * 100f:0.#} · Hazırlık ~{s.def.prepMinutes:0} dk · " +
                            $"Sepet {Ft(s.def.basketMin)}–{Ft(s.def.basketMax)} · Ağırlık {GameData.KgText(s.def.kgMin)}–{GameData.KgText(s.def.kgMax)} · Toplam sipariş {s.ordersTotal}", small);
            if (s.def.kgMax >= GameData.HeavyKg)
                GUILayout.Label($"{GameData.HeavyKg:0} kg üstü ağır siparişler gelir: bunlar için arabalı kurye gerekir.", warn);
            if (!map.IsUnlocked(s.tile))
            {
                GUILayout.Label($"Kilitli: önce \"{CityMap.DistrictNames[s.def.unlockLevel]}\" bölgesini aç.", small);
            }
            else if (!s.contracted)
            {
                var shop = s;
                if (GameClock.I.Day < s.resignBlockedUntil)
                    GUILayout.Label($"Seninle çalışmayı bıraktı; {GameClock.I.DateOf(s.resignBlockedUntil)} tarihinden sonra yeniden teklif edebilirsin.", warn);
                else if (GUILayout.Button($"Sözleşme imzala — {Ft(s.def.contractCost)}", button))
                    deferred = () => ConfirmIfExpensive(shop.def.contractCost, "Sözleşme imzala",
                        $"{shop.def.name} ile {Ft(shop.def.contractCost)} karşılığında sözleşme imzalansın mı? " +
                        $"Serbest kasan: {Ft(EconomyManager.I.FreeCash)}.",
                        () => map.SignContract(shop));
            }
            else if (rv != null)
            {
                var shop = s;
                Color sc = Color.Lerp(new Color(0.9f, 0.3f, 0.3f), new Color(0.3f, 0.85f, 0.4f), s.satisfaction / 100f);
                Bar("Memnuniyet", s.satisfaction / 100f, sc, $"{s.satisfaction:0}/100");
                GUILayout.Label(RivalCompany.PerformanceText(s), small);
                float share = rv.ShareFor(s);
                GUILayout.Label($"Sözleşme bitişi: {GameClock.I.DateOf(s.contractEndDay)} · bu ay {s.monthOrders} sipariş (geçen ay {s.lastMonthOrders}) · " +
                                (s.exclusive ? "münhasır: siparişler +%15" : s.rivalContracted ? $"rakiple paylaşılıyor: payın ~%{share / 1.25f * 100f:0}" : "rakip yok"),
                                small);
                if (s.commissionBonus != 0f)
                    GUILayout.Label($"Pazarlık/anlaşma farkı: {(s.commissionBonus > 0 ? "+" : "−")}{Mathf.Abs(s.commissionBonus) * 100f:0.#} puan (temel %{s.def.commission * 100f:0.#})", small);
                GUILayout.BeginHorizontal();
                GUI.enabled = modalFree && rv.CanNegotiate(s);
                if (GUILayout.Button($"Pazarlık: komisyon +2 puan (şans %{rv.NegotiateChance(s) * 100f:0})", smallButton))
                    deferred = () => rv.Negotiate(shop);
                GUI.enabled = modalFree && rv.CanOfferExclusive(s);
                if (GUILayout.Button(s.exclusive ? "Münhasır" : $"Münhasırlık öner: −3 puan (şans %{rv.ExclusiveChance(s) * 100f:0})", smallButton))
                    deferred = () => AskConfirm("Münhasırlık öner",
                        $"{shop.def.name} sadece seninle çalışsın diye teklif edilsin mi? Kabul ederse rakip çıkar, siparişler %15 artar, " +
                        "komisyonun 3 puan düşer ve sözleşme 6 ay uzar. Reddederse memnuniyeti biraz düşer ve 30 gün tekrar teklif edemezsin. " +
                        $"Kabul ihtimali yaklaşık %{rv.ExclusiveChance(shop) * 100f:0}.",
                        "Evet, teklif et", () => rv.OfferExclusive(shop));
                GUI.enabled = modalFree;
                GUILayout.EndHorizontal();
                if (GameClock.I.Day < s.negotiateCooldownUntil)
                    GUILayout.Label($"Yeni teklif için {GameClock.I.DateOf(s.negotiateCooldownUntil)} tarihini bekle.", small);
            }
        }

        void DrawEquipment()
        {
            var cm = CourierManager.I;
            switch (GameClock.I.Season)
            {
                case Season.Kis: GUILayout.Label("Kış: montsuz kuryeler yavaşlar, hastalanır, morali her gün düşer.", warn); break;
                case Season.Yaz: GUILayout.Label("Yaz: tişörtsüz kuryeler sıcakta yavaşlar, çabuk yorulur.", warn); break;
                case Season.Sonbahar: GUILayout.Label("Sonbahar: yağmur sezonu, kış yaklaşıyor. Yağmurluk ve mont zamanı.", warn); break;
                default: GUILayout.Label("İlkbahar: yağmur sezonu, yağmurluksuz kuryeler ıslanıp hastalanabilir.", warn); break;
            }
            // Depozito sistemi (açıklama kısa, ayrıntısı "Ayrıntı"da)
            var eco = EconomyManager.I;
            if (Sade.Depozito)
                Intro("ekipman", "Kuryeler ekipmanı ihtiyaç duyunca kendileri ister; bedeli depozito olarak kazançlarından kesilir. Eksikleri Kuryeler penceresinde yazar.",
                      "Ekipman kısa vadede kârı düşürür ama gider olarak vergiden düşülür; hız, moral, puan ve daha az hastalık/kaza olarak geri döner. " +
                      "Kuryeler mevsime, yağmura ve dikkatlerine göre ister. Şirket depodan verir ya da depoda yoksa satın alır. " +
                      "Kurye ayrılırken ekipmanı iade ederse depozitosu geri ödenir ve ekipman depoya döner; iade etmezse depozito sende kalır. " +
                      $"Yeni kuryeler işe başlarken {GameData.StarterKitText} depozitoyla alır.");
            else
                Intro("ekipman", "Ekipmanı şirket verir: kuryeler ihtiyaç duyunca ister, depoda varsa oradan çıkar, yoksa satın alınır. Eksikleri Kuryeler penceresinde yazar.",
                      "Ekipman kısa vadede kârı düşürür ama gider olarak vergiden düşülür; hız, moral, puan ve daha az hastalık/kaza olarak geri döner. " +
                      "Kuryeler mevsime, yağmura ve dikkatlerine göre ister. Ekipman şirketin malıdır: kurye ayrılırken çoğu zaman iade eder ve ekipman depoya döner, " +
                      "sıradaki kuryeye bedavaya verilir; iade etmezse o ekipman kayıptır. " +
                      $"Yeni kuryeler işe başlarken {GameData.StarterKitText} alır.");
            long owed = 0;
            foreach (var oc in cm.Couriers) owed += oc.depositOwed;
            var stockParts = new List<string>();
            foreach (var se in GameData.Equipment) if (cm.gearStock[(int)se.kind] > 0) stockParts.Add($"{cm.gearStock[(int)se.kind]}× {se.name}");
            if (Sade.Depozito || eco.depositsHeld > 0) KV("Elindeki depozito (iade borcu)", Ft(eco.depositsHeld));
            if (Sade.Depozito) KV("Kuryelerden kesilecek", Ft(owed));
            KV("Depo", stockParts.Count > 0 ? string.Join(", ", stockParts) : "boş");
            GUILayout.Label("\"Hatırlat\" ücretsizdir: kuryenin 2 gün boyunca o ekipmanı isteme ihtimali artar.", bodyDim);

            // Hijyen özeti
            int dirty = 0, worn = 0;
            foreach (var hc in cm.Couriers) { if (hc.hygiene < Courier.HygieneLimit) dirty++; if (hc.BagWorn) worn++; }
            if (Sade.Hijyen)
            {
                Section("Çanta hijyeni",
                    "Çanta temizliği kuryenin kendi sorumluluğu: her gece temizler, titiz olanlar gün içinde de temizler. " +
                    "Kirli çanta müşteri şikayeti ve düşük puan getirir; gün içinde kirlenenleri uyarabilirsin (ücretsiz, moral biraz düşer). " +
                    $"{Courier.BagWornUses} teslimattan sonra çanta yıpranır, yenisi gerekir.");
                GUILayout.Label($"{dirty} kuryenin çantası kirli · {worn} çanta yıpranmış", dirty > 0 ? warn : body);
                if (GUILayout.Button("Çantası kirli olanların hepsini uyar", smallButton))
                    deferred = () => cm.WarnAllDirty();
            }
            else
            {
                Section("Çanta yıpranması", $"{Courier.BagWornUses} teslimattan sonra termal çanta yıpranır; yenisi Kuryeler penceresinden alınır.");
                GUILayout.Label(worn > 0 ? $"{worn} çanta yıpranmış" : "Yıpranmış çanta yok", worn > 0 ? warn : body);
            }

            if (selectedCourier == null || !cm.Couriers.Contains(selectedCourier))
                selectedCourier = cm.Couriers.Count > 0 ? cm.Couriers[0] : null;

            Section("Kurye bazında");
            GUILayout.BeginHorizontal();
            int n = 0;
            foreach (var c in cm.Couriers)
            {
                var courier = c;
                if (SelectButton(c == selectedCourier, c.ShortName, smallButton)) deferred = () => selectedCourier = courier;
                if (++n % 4 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }
            GUILayout.EndHorizontal();

            if (selectedCourier != null)
            {
                var pens = selectedCourier.ActivePenalties();
                if (pens.Count == 0) GUILayout.Label($"{selectedCourier.ShortName}: eksik yok.", label);
                foreach (var p in pens) GUILayout.Label("• " + p, warn);
            }

            foreach (var e in GameData.Equipment)
            {
                var kind = e.kind;
                int have = 0, need = 0;
                foreach (var ec in cm.Couriers) { if (ec.Has(kind)) have++; else if (CourierManager.Usefulness(ec, kind) >= 0.5f) need++; }
                GUILayout.BeginVertical(box);
                GUILayout.Label($"{e.name} — {(Sade.Depozito ? "depozito" : "fiyatı")} {Ft(e.price)}", header);
                GUILayout.Label(e.effect, small);
                GUILayout.Label($"{have} kuryede var · şu an işine yarayıp eksik olan: {need} · depoda {cm.gearStock[(int)kind]}", need > 0 ? warn : small);
                GUILayout.BeginHorizontal();
                if (selectedCourier != null)
                {
                    var sc = selectedCourier;
                    if (sc.Has(kind)) GUILayout.Label($"{sc.ShortName}: var" + (Sade.Depozito && sc.depositGear != null && sc.depositGear.Contains(kind) ? " (depozitolu)" : ""), small);
                    else if (GameClock.I.TotalMinutes < sc.gearNudgeUntil) GUILayout.Label($"{sc.ShortName}'a hatırlatıldı", small);
                    else if (GUILayout.Button($"{sc.ShortName}'a hatırlat", smallButton)) deferred = () => cm.RemindGear(sc, kind);
                }
                if (GUILayout.Button("Eksik olanlara hatırlat", smallButton)) deferred = () => cm.RemindAll(kind);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
        }

        void DrawSupport()
        {
            var sm = SupportManager.I;
            var clock = GameClock.I;

            Intro("destek", "Maaşlı destek ekibi vardiyasında şikayetleri senin yerine çözer. Vardiyada kimse yoksa kararlar sana kalır.",
                  "Sabit vardiyada, haftada 5 gün çalışırlar. Eksik ürün/hasarlı sipariş şikayetlerini pazarlıkla çözerler (sana kart gelmez), " +
                  "kurye bulunamayan siparişlerde müşteriyi arayıp ek süre alırlar ve kuryelere destek olurlar.");

            // Bugünün 24 saatlik kapsama çizelgesi
            GUILayout.Space(6);
            int covered = sm.CoveredHours(clock.DayOfWeek);
            var onDuty = sm.OnDutyAgent();
            Section($"Bugünün kapsaması: {covered}/24 saat");
            Chip(onDuty != null ? $"Şu an vardiyada: {onDuty.name}" : "Şu an vardiyada kimse yok", onDuty != null ? UITheme.Good : UITheme.Warn);
            ChipRow();
            DrawCoverageChart(sm, clock);

            GUILayout.Space(6);
            KV("Aylık maaş toplamı", Ft(sm.MonthlyPayroll));
            KV("Bu ay birikmiş (ay sonunda ödenir)", Ft(sm.AccruedThisMonth));
            KV("Bu ay çözülen şikayet", sm.handledThisMonth.ToString());
            if (sm.AccruedThisMonth > 0 && sm.AccruedThisMonth > EconomyManager.I.FreeCash)
                GUILayout.Label($"Ay sonu maaşı ({Ft(sm.AccruedThisMonth)}) serbest kasandan ({Ft(EconomyManager.I.FreeCash)}) fazla, aynı günlerde kurye ödemeleri de var: iflas riski!", warn);

            GUILayout.Space(6);
            Section($"Destek ekibi ({sm.Agents.Count})");
            if (sm.Agents.Count == 0) GUILayout.Label("Henüz destek personelin yok.", small);
            foreach (var a in sm.Agents)
            {
                GUILayout.BeginVertical(box);
                bool duty = a.IsOnDuty(clock);
                BeginIdCardHead(a.name, 2, CompanyProfile.Primary, Icons.Kind.Support, duty ? "Vardiyada" : "Vardiya dışı",
                                duty ? UITheme.Good : (Color?)null, 0);
                GUILayout.Label($"{a.ShiftText} · {a.DaysText} · Maaş {Ft(a.monthlySalary)}/ay", bodyDim);
                SupportSkillRow(a.skill);
                GUILayout.Label($"Çözdüğü: {a.handledTotal} · Pazarlıkla kazandırdığı: {Ft(a.savedTotal)}", bodyDim);
                EndIdCardHead();
                var agent = a;
                if (GUILayout.Button("İşten çıkar (bu ayın maaşı ödenir)", smallButton))
                    deferred = () => AskConfirm("Destek personelini çıkar",
                        $"{agent.name} işten çıkarılsın mı? Bu ay çalıştığı günlerin maaşı ({Ft(SupportManager.ProratedSalary(agent))}) hemen ödenir. " +
                        "Vardiyası boş kalır.",
                        "Evet, işten çıkar", () => sm.Fire(agent));
                GUILayout.EndVertical();
            }

            DrawSupportCandidates();
            DrawOperations();
        }

        /// <summary>Destek ekibi + operasyon sorumlusunun aylık maaş toplamı.</summary>
        long TotalPayroll => SupportManager.I.MonthlyPayroll + (OperationsManager.I != null ? OperationsManager.I.MonthlySalary : 0);

        void DrawOperations()
        {
            var ops = OperationsManager.I;
            var cm = CourierManager.I;
            if (ops == null) return;

            Section("Operasyon sorumlusu",
                "Maaşlı tek kişi. Her sabah 09:00'da eksik ve işine yarayacak ekipmanı kuryelere hatırlatır (kuryeler depozitoyla alır), " +
                "yıpranan çantaları yeniler (bütçeden) ve gün içinde çantası kirlenen kuryeleri uyarır. Yetenekli sorumlu işleri ertelemez. " +
                "Ne yaptığı gün sonu raporunda yazar.");
            GUILayout.Label("Ekipman ve çanta işlerini senin yerine yapar. Az kuryen varken maaşına değmeyebilir.", body);

            if (ops.HasManager)
            {
                var m = ops.Manager;
                GUILayout.BeginVertical(box);
                GUILayout.Label(m.name, header);
                GUILayout.Label($"Maaş {Ft(m.monthlySalary)}/ay · bu ay birikmiş {Ft(ops.AccruedThisMonth)} (ay başında ödenir)", small);
                Bar("Yetenek", m.skill, new Color(0.4f, 0.75f, 1f));
                GUILayout.Label($"İkna: kabul ihtimali +%{ops.Persuasion * 100f:0} · İşleri ertelemeden yapma: %{ops.Reliability * 100f:0}" +
                                (ops.Discount > 0f ? " · Toplu alım indirimi %10" : ""), small);
                if (!string.IsNullOrEmpty(ops.lastPassSummary)) GUILayout.Label("Son sabah turu: " + ops.lastPassSummary, small);
                if (GUILayout.Button("İşten çıkar (bu ayın maaşı ödenir)", smallButton))
                    deferred = () => AskConfirm("Operasyon sorumlusunu çıkar",
                        $"{m.name} işten çıkarılsın mı? Bu ay çalıştığı günlerin maaşı ({Ft(ops.AccruedThisMonth)}) hemen ödenir; " +
                        "eksik ekipman teklifleri ve çanta işleri yine sana kalır.",
                        "Evet, işten çıkar", () => ops.Fire());
                GUILayout.EndVertical();
            }
            else
            {
                GUILayout.Label($"Başvurular ({ops.Candidates.Count})", label);
                if (ops.Candidates.Count == 0) GUILayout.Label("Şu an başvuru yok; yenisi gelecek.", small);
                foreach (var cand in ops.Candidates)
                {
                    GUILayout.BeginVertical(box);
                    GUILayout.Label($"{cand.name} — Maaş beklentisi {Ft(cand.monthlySalary)}/ay", header);
                    GUILayout.Label($"\"{cand.note}\"", note);
                    Bar("Yetenek", cand.skill, new Color(0.4f, 0.75f, 1f));
                    long payrollAfter = TotalPayroll + cand.monthlySalary;
                    GUILayout.Label($"İşe alırsan aylık toplam maaş: {Ft(payrollAfter)} · serbest kasan: {Ft(EconomyManager.I.FreeCash)}",
                                    payrollAfter > EconomyManager.I.FreeCash ? warn : small);
                    GUILayout.Label(ExpiryText(cand.expiresAt), small);
                    var c = cand;
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("İşe al", button))
                        deferred = () => AskConfirm("Operasyon sorumlusunu işe al",
                            $"{c.name} {Ft(c.monthlySalary)}/ay maaşla işe alınsın mı? Maaş ay başında, çalıştığı gün kadar ödenir. " +
                            $"Aylık toplam maaşın {Ft(TotalPayroll + c.monthlySalary)} olur.",
                            "Evet, işe al", () => ops.Hire(c));
                    if (GUILayout.Button("Reddet", button)) deferred = () => ops.Reject(c);
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                }
            }

            // Kurallar (işe almadan önce de ayarlanabilir)
            GUILayout.Space(4);
            GUILayout.Label("Kurallar — hangi ekipmanları teklif etsin:", label);
            GUILayout.BeginHorizontal();
            int col = 0;
            foreach (var e in GameData.Equipment)
            {
                int ki = (int)e.kind;
                ops.offerKinds[ki] = GUILayout.Toggle(ops.offerKinds[ki], " " + e.name, toggle);
                if (++col % 3 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }
            GUILayout.EndHorizontal();
            HelpOnLast("Giyecekleri sadece mevsimi gelince ya da yaklaşınca sorar (ör. montu sonbahar ve kışın); arabalı kuryelere giyecek ve kask sormaz.");
            ops.replaceWornBags = GUILayout.Toggle(ops.replaceWornBags,
                $" Yıpranan çantayı yenile ({Ft(GameData.Equip(EquipmentKind.TermalCanta).price)}, şirket öder)", toggle);
            if (Sade.Hijyen) ops.warnDirtyBags = GUILayout.Toggle(ops.warnDirtyBags, " Çantası kirlenen kuryeyi uyar (ücretsiz, moral biraz düşer)", toggle);

            GUILayout.Label("Yeni kuryeye ekipman ne zaman sorulsun:", small);
            GUILayout.BeginHorizontal();
            foreach (int md in new[] { 0, 10, 30, 60 })
            {
                int v = md;
                if (SelectButton(ops.minDeliveries == v, v == 0 ? "Hemen" : $"{v} teslimattan sonra", smallButton)) deferred = () => ops.minDeliveries = v;
            }
            GUILayout.EndHorizontal();
            ops.monthlyBudget = MoneySlider("Aylık bütçe", ops.monthlyBudget, 0, 300000, 5000);
            GUILayout.Label($"Bu ay sorumlunun çanta yenilemeye harcadığı: {Ft(ops.spentThisMonth)} / {Ft(ops.monthlyBudget)}",
                            ops.BudgetLeft == 0 ? warn : small);
        }

        /// <summary>24 saatlik destek kapsama çizelgesi: saat etiketleri hücrelerle tam hizalı.</summary>
        void DrawCoverageChart(SupportManager sm, GameClock clock)
        {
            Rect r = GUILayoutUtility.GetRect(100, 40, GUILayout.ExpandWidth(true));
            const float labelW = 28f;
            float left = r.x + labelW / 2f;               // etiketler kenardan taşmasın
            float width = r.width - labelW;
            float cw = width / 24f;

            for (int h = 0; h < 24; h++)
            {
                var cell = new Rect(left + h * cw + 1f, r.y, cw - 2f, 16f);
                Fill(cell, sm.CoveredAt(clock.DayOfWeek, h) ? new Color(0.3f, 0.8f, 0.45f) : new Color(1f, 1f, 1f, 0.12f));
                if (h == clock.Hour) Fill(new Rect(cell.x, cell.y + 18f, cell.width, 3f), Color.white);
            }

            // Saat çizgileri ve etiketleri (00, 06, 12, 18, 24) tam hücre sınırlarında
            var old = small.alignment;
            small.alignment = TextAnchor.UpperCenter;
            for (int h = 0; h <= 24; h += 6)
            {
                float x = left + h * cw;
                Fill(new Rect(x - 0.5f, r.y - 2f, 1f, 20f), new Color(1f, 1f, 1f, 0.35f));
                GUI.Label(new Rect(x - labelW / 2f, r.y + 22f, labelW, 16f), h.ToString("00"), small);
            }
            small.alignment = old;
        }

        void DrawUpgrades()
        {
            var up = UpgradeManager.I;
            var e = EconomyManager.I;
            Intro("gelisim", "Her geliştirmenin kurulum bedeli ve günlük gideri var. Sadece gerçekten gerektiğinde yükselt.",
                  "Günlük gider: abonelik, kira, malzeme, reklam bütçesi. Büyük platform paketleri şehir büyüdükçe, tesisler ise merkez ofis " +
                  "büyüdükçe açılır. İşletme gideri olanları küçültebilirsin (kurulum bedeli geri gelmez). Bütün giderler vergiden düşülür. " +
                  "Fiyatlar şirketin büyüklüğüne göre değişir: son iki haftanın günlük geliri arttıkça kurulum bedelleri ve bölge açma bedeli artar, " +
                  "günlük giderler daha yavaş artar. Erken yatırım ucuzdur.");
            KV("Serbest kasa", Ft(e.FreeCash));
            if (Denge.FiyatCarpani > 1f)
            {
                KV("Fiyat seviyesi", $"×{Denge.FiyatCarpani:0.00}");
                HelpOnLast("Son 14 günün ortalama gelirine göre. Şirket büyüdükçe tedarikçiler de fiyat artırır.");
            }
            KV("Kurye hesapları", $"{CourierManager.I.Couriers.Count}/{up.CourierCap}");
            KV("Günlük toplam gider", $"{Ft(up.TotalDailyCost)} (ayda ~{Ft(up.TotalDailyCost * 30L)})");

            foreach (var d in UpgradeManager.Defs)
            {
                var kind = d.kind;
                int lv = up.Level(kind);
                if (!Sade.Hijyen && kind == UpgradeKind.Hijyen && lv == 0) continue;   // hijyen sistemi kapalı: satın alınmamışsa gösterilmez
                GUILayout.BeginVertical(box);
                GUILayout.Label($"{d.name} · {d.levelNames[lv]} ({lv}/{d.MaxLevel})", header);
                HelpOnLast(d.summary);   // ne işe yaradığı "?" balonunda
                int dailyNow = up.DailyCost(kind);
                GUILayout.Label("Şu an: " + d.effect(lv) + (dailyNow > 0 ? $" · günlük {Ft(dailyNow)}" : ""), label);

                int cost = up.NextCost(kind);
                if (cost >= 0)
                {
                    int dailyNext = d.DailyAt(lv + 1);
                    GUILayout.Label($"Sonraki: {d.LevelText(lv + 1)} · günlük {Ft(dailyNext)}" +
                                    (dailyNext > dailyNow ? $" (+{Ft(dailyNext - dailyNow)}/gün)" : ""), small);
                    string lockReason = up.LockReason(kind);
                    if (lockReason.Length > 0) GUILayout.Label($"Kilitli: {lockReason}. (Kurulum {Ft(cost)})", warn);
                }
                else GUILayout.Label("En yüksek seviyede.", small);

                GUILayout.BeginHorizontal();
                if (cost >= 0 && !up.IsLocked(kind))
                {
                    GUI.enabled = modalFree && e.FreeCash >= cost;
                    if (GUILayout.Button($"Yükselt — {Ft(cost)}", button))
                    {
                        string what = $"{d.name}: {d.LevelText(lv + 1)}";
                        int daily = d.DailyAt(lv + 1);
                        deferred = () => ConfirmIfExpensive(cost, "Geliştirme",
                            $"{what}. Kurulum {Ft(cost)}, sonra günlük {Ft(daily)} gider. Serbest kasan {Ft(EconomyManager.I.FreeCash)}. " +
                            "Kurulum bedeli geri gelmez.",
                            () => up.Buy(kind));
                    }
                    GUI.enabled = modalFree;
                }
                if (lv > 0 && d.canDowngrade && d.DailyAt(lv) > d.DailyAt(lv - 1))
                {
                    string block = up.DowngradeBlock(kind);
                    GUI.enabled = modalFree && block.Length == 0;
                    if (GUILayout.Button(block.Length == 0 ? $"Küçült (−{Ft(d.DailyAt(lv) - d.DailyAt(lv - 1))}/gün)" : $"Küçültülemez: {block}", smallButton, GUILayout.Width(block.Length == 0 ? 170 : 260)))
                    {
                        string what = $"{d.name}: {d.levelNames[lv]} → {d.levelNames[lv - 1]}";
                        deferred = () => AskConfirm("Geliştirmeyi küçült",
                            $"{what}. Günlük gider azalır ama kurulum bedeli geri gelmez; tekrar yükseltmek için yeniden ödersin.",
                            "Evet, küçült", () => up.Downgrade(kind));
                    }
                    GUI.enabled = modalFree;
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
        }

        void DrawSupportCandidates()
        {
            var sm = SupportManager.I;
            GUILayout.Space(6);
            Section($"Destek personeli başvuruları ({SupportManager.I.Candidates.Count})",
                $"Destek personeli maaşlıdır ve sabit vardiyada çalışır. Her başvuru {SupportManager.CandidateValidDays} gün geçerli; " +
                "reddedilen ya da süresi dolanın yerine ertesi gün yenisi gelir. Yetenekli personel şikayetleri daha ucuza kapatır " +
                "(iade değerinin %30'u ile %60'ı arası).");
            if (SupportManager.I.Candidates.Count == 0) GUILayout.Label("Şu an destek başvurusu yok; ertesi gün yenisi gelir.", bodyDim);
            foreach (var cand in sm.Candidates)
            {
                GUILayout.BeginVertical(box);
                BeginIdCardHead(cand.name, 2, null, Icons.Kind.Wallet, $"{Ft(cand.monthlySalary)}/ay", null, 0);
                if (!string.IsNullOrEmpty(cand.note)) GUILayout.Label($"\"{cand.note}\"", note);
                SupportSkillRow(cand.skill);
                EndIdCardHead();
                long payrollAfter = TotalPayroll + cand.monthlySalary;
                GUILayout.Label($"İşe alırsan aylık toplam maaş: {Ft(payrollAfter)} · serbest kasan: {Ft(EconomyManager.I.FreeCash)}",
                                payrollAfter > EconomyManager.I.FreeCash ? warn : small);

                GUILayout.Label("Vardiya:", small);
                GUILayout.BeginHorizontal();
                var c = cand;
                for (int s = 0; s < 3; s++)
                {
                    var shift = (SupportShift)s;
                    if (SelectButton(c.chosenShift == shift, SupportManager.ShiftLabel(shift), smallButton)) deferred = () => c.chosenShift = shift;
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (SelectButton(c.chosenDays == WorkDays.PztCum, "Pazartesi–Cuma", smallButton)) deferred = () => c.chosenDays = WorkDays.PztCum;
                if (SelectButton(c.chosenDays == WorkDays.CarPaz, "Çarşamba–Pazar", smallButton)) deferred = () => c.chosenDays = WorkDays.CarPaz;
                GUILayout.EndHorizontal();
                GUILayout.Label(ExpiryText(cand.expiresAt), small);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button($"İşe al ({SupportManager.ShiftLabel(c.chosenShift)}, {(c.chosenDays == WorkDays.PztCum ? "Pzt–Cum" : "Çar–Paz")})", accentButton))
                    deferred = () => AskConfirm("Destek personelini işe al",
                        $"{c.name} {Ft(c.monthlySalary)}/ay maaşla, {SupportManager.ShiftLabel(c.chosenShift)} vardiyasında " +
                        $"{(c.chosenDays == WorkDays.PztCum ? "Pazartesi–Cuma" : "Çarşamba–Pazar")} çalışmak üzere işe alınsın mı? " +
                        $"Aylık toplam maaşın {Ft(TotalPayroll + c.monthlySalary)} olur.",
                        "Evet, işe al", () => sm.Hire(c));
                if (GUILayout.Button("Reddet", button, GUILayout.Width(90))) deferred = () => sm.Reject(c);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
        }

        /// <summary>Başvurunun kalan süresi (destek ve operasyon adayları gece yarısı sona erer).</summary>
        static string ExpiryText(float expiresAt)
        {
            if (expiresAt <= 0f || GameClock.I == null) return "";
            float h = (expiresAt - GameClock.I.TotalMinutes) / 60f;
            if (h <= 24f) return $"Başvuru bu gece sona erer ({Mathf.Max(0f, h):0} saat kaldı).";
            return $"Başvuru yarın gece sona erer ({h:0} saat kaldı).";
        }

        void DrawCampaigns()
        {
            var camp = CampaignManager.I;
            var clock = GameClock.I;
            var eco = EconomyManager.I;

            Intro("kampanya", "Boost saatinde kuryeler daha çok çevrimiçi olur ve sipariş reddetmez. Bedeli kârından ödenir.",
                  "Kampanyalar şirketin kârından ödenir; müşterinin teslimat ücretine yansımaz. Boost saatinde çevrimdışı kuryeler " +
                  "çevrimiçi olmaya daha istekli olur, kuryeler sipariş reddetmez ve aynı anda +1 paket alır.");

            GUILayout.Space(6);
            Section($"Bugünün boostları · {clock.DayName}{(clock.IsWeekend ? " (hafta sonu)" : "")}");
            var today = camp.TodayWindows();
            var active = camp.ActiveWindow;
            if (today.Count == 0) GUILayout.Label("Bugün boost yok.", label);
            foreach (var w in today)
            {
                bool on = active != null && w.startHour == active.startHour && w.percent == active.percent;
                bool past = clock.Hour >= w.endHour;
                string state = on ? "  ŞU AN AKTİF" : (past ? "  (bitti)" : "");
                GUILayout.Label($"{w.TimeText}  %{w.percent * 100f:0}  — {w.label}{state}", on ? header : (past ? small : label));
            }

            GUILayout.Space(8);
            Section("Kötü hava boostu (otomatik)",
                "Kötü havada siparişler artar ama bisiklet, scooter ve motor kuryeleri dışarı çıkmaya daha az isteklidir " +
                "(yağmurluk ve kış montu bu isteksizliği azaltır, arabalılar etkilenmez). Hava boostu onları çevrimiçi çeker. " +
                "Saat boostuyla toplanmaz, hangisi yüksekse o geçerli olur. Müşteriden alınan kötü hava ücreti de bu masrafı kısmen karşılar.");
            camp.weatherBoost = GUILayout.Toggle(camp.weatherBoost, " Kötü havada otomatik boost", toggle);
            camp.coldBoost = PercentSlider("Soğuk", camp.coldBoost, 0f, 0.60f);
            camp.rainBoost = PercentSlider("Yağmur", camp.rainBoost, 0f, 0.60f);
            camp.snowBoost = PercentSlider("Kar", camp.snowBoost, 0f, 0.60f);
            camp.stormBoost = PercentSlider("Fırtına", camp.stormBoost, 0f, 0.60f);
            var wx = WeatherSystem.I;
            KV($"Şu an: {wx.Name}", $"sipariş ×{wx.DemandFactor:0.00} · hava boostu %{camp.WeatherBoostPercent * 100f:0}");
            KV("Kurye hava bonusu / müşteriye hava ücreti", $"{Ft(wx.WeatherBonus)} / {Ft(wx.CustomerSurcharge)}");

            GUILayout.Space(8);
            Section("Hafta sonu boostları");
            camp.weekendMorningBoost = GUILayout.Toggle(camp.weekendMorningBoost,
                $" Sabah boostu {camp.weekendMorningStart:00}:00–{camp.weekendMorningEnd:00}:00 (sipariş az olduğu için)", toggle);
            camp.weekendMorningPercent = PercentSlider("Sabah yüzdesi", camp.weekendMorningPercent, 0.05f, 0.60f);
            camp.weekendLunchBoost = GUILayout.Toggle(camp.weekendLunchBoost,
                $" Öğle boostu {camp.weekendLunchStart:00}:00–{camp.weekendLunchEnd:00}:00", toggle);
            camp.weekendLunchPercent = PercentSlider("Öğle yüzdesi", camp.weekendLunchPercent, 0.05f, 0.60f);

            GUILayout.Space(8);
            Section("Hafta içi rastgele boostlar", "Hafta içi boost saatleri her gün yeniden belirlenir; değişiklikler ertesi gün geçerli olur.");
            camp.weekdayRandomBoosts = GUILayout.Toggle(camp.weekdayRandomBoosts, " Gün içine serpiştirilmiş boostlar", toggle);
            camp.weekdayBoostCount = IntSlider("Günde kaç tane", camp.weekdayBoostCount, 0, 4, 1);
            camp.weekdayMaxPercent = PercentSlider("En yüksek yüzde", camp.weekdayMaxPercent, 0.10f, 0.60f);

            GUILayout.Space(8);
            Section("Haftalık hedef ödülü");
            camp.weeklyChallenge = GUILayout.Toggle(camp.weeklyChallenge, " Haftalık hedef açık (her Pazartesi sıfırlanır)", toggle);
            camp.weeklyTarget = IntSlider("Hedef paket", camp.weeklyTarget, 20, 400, 10);
            camp.weeklyReward = MoneySlider("Ödül", camp.weeklyReward, 0, 100000, 1000);

            // Ödülün kârla karşılaştırması
            int samples = 0;
            long sum = 0;
            foreach (var o in OrderManager.I.History) { sum += o.Profit; samples++; }
            float avg = samples > 0 ? sum / (float)samples : 300f;
            RewardCheck(avg, camp.weeklyTarget, camp.weeklyReward);

            GUILayout.Space(8);
            Section("Aylık hedef ödülü");
            camp.monthlyChallenge = GUILayout.Toggle(camp.monthlyChallenge, " Aylık hedef açık (her ayın başında sıfırlanır)", toggle);
            camp.monthlyTarget = IntSlider("Hedef paket", camp.monthlyTarget, 50, 1000, 10);
            camp.monthlyReward = MoneySlider("Ödül", camp.monthlyReward, 0, 200000, 1000);
            RewardCheck(avg, camp.monthlyTarget, camp.monthlyReward);

            GUILayout.Space(8);
            Section($"Bu hafta ({clock.MonthName}, {clock.WeekOfMonth}. hafta)");
            KV("Boost ödemeleri", Ft(camp.boostPaidThisWeek));
            KV($"Haftalık hedef ödülleri ({camp.rewardsClaimedThisWeek} kurye)", Ft(camp.rewardsPaidThisWeek));
            KV($"Bu ay aylık hedef ödülleri ({camp.rewardsClaimedThisMonth} kurye)", Ft(camp.rewardsPaidThisMonth));
            KV("Bugünkü kampanya gideri", Ft(eco.Today.boosts + eco.Today.rewards));
        }

        void RewardCheck(float avgProfit, int target, int reward)
        {
            long targetProfit = (long)(avgProfit * target);
            GUILayout.Label($"Ortalama sipariş kârı ~{Ft((long)avgProfit)} → {target} paket ≈ {Ft(targetProfit)} kâr, ödül {Ft(reward)}.", small);
            if (reward > targetProfit)
                GUILayout.Label("Bu ödül, o paketlerden kazandığından fazla: hedefe ulaşan her kurye zarar ettirir.", warn);
        }

        float PercentSlider(string name, float v, float min, float max)
        {
            float f = SliderRow(name, v, min, max, $"%{v * 100f:0}", 140);
            return Mathf.Round(f * 20f) / 20f; // %5 adımlar
        }

        void DrawFamilyLoan()
        {
            var loan = FamilyLoan.I;
            var e = EconomyManager.I;
            GUILayout.BeginVertical(box);
            GUILayout.Label("Aile borcu", header);
            HelpOnLast("Borç ödemesi gider değildir, vergiden düşülmez. Erken ödemek taksiti kapatır; ama kasada kötü günler için yedek bırak.");
            if (loan.Finished)
            {
                GUILayout.Label($"Borç tamamen ödendi ({Ft(loan.principal)}). Ailen seninle gurur duyuyor.", label);
                GUILayout.EndVertical();
                return;
            }
            if (loan.Failed) GUILayout.Label("Borç ödenemedi, şirket kapandı (test modunda devam ediyorsun).", warn);

            GUILayout.Label($"Kalan: {Ft(loan.Remaining)} / {Ft(loan.principal)} · Aile güveni: {loan.Trust}/{loan.maxTrust}", label);
            Bar("Ödenen", loan.TotalPaid / (float)loan.principal, new Color(0.35f, 0.8f, 0.5f),
                $"%{loan.TotalPaid * 100f / loan.principal:0}");
            foreach (var t in loan.Schedule)
            {
                string state = t.closed ? (t.Remaining == 0 ? "ödendi" : "kaçırıldı") : $"{loan.DaysUntil(t)} gün kaldı";
                GUILayout.Label($"• {t.DueText}: {Ft(t.amount)} — ödenen {Ft(t.paid)} ({state})",
                    !t.closed && loan.DaysUntil(t) <= 7 ? warn : small);
            }
            var next = loan.NextDue;
            if (next != null && !loan.Failed)
            {
                GUILayout.BeginHorizontal();
                GUI.enabled = modalFree && e.money >= next.Remaining;
                if (GUILayout.Button($"Taksiti öde ({Ft(next.Remaining)})", smallButton)) deferred = () => loan.Pay(next.Remaining);
                GUI.enabled = modalFree && e.money >= 50000;
                if (GUILayout.Button($"{Ft(50000)} erken öde", smallButton))
                    deferred = () => ConfirmIfExpensive(50000, "Aile borcuna erken ödeme",
                        $"Annenle babana {Ft(50000)} erken ödensin mi? Serbest kasan {Ft(EconomyManager.I.FreeCash)}.",
                        () => loan.Pay(50000));
                GUI.enabled = modalFree && e.money >= loan.Remaining;
                if (GUILayout.Button($"Tamamını öde ({Ft(loan.Remaining)})", smallButton)) deferred = () => loan.Pay(loan.Remaining);
                GUI.enabled = modalFree;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        // ---------------------------------------------------------------- grafikler (son 30 gün)

        void DrawHistoryCharts()
        {
            var h = StatsHistory.I;
            if (h == null) return;
            if (h.Days.Count == 0)
            {
                GUILayout.Label("Grafikler ilk gün sonundan sonra görünür.", small);
                return;
            }
            int n = Mathf.Min(30, h.Days.Count);
            var days = h.Days.GetRange(h.Days.Count - n, n);
            var last = days[n - 1];
            GUILayout.Label($"Son {n} gün", header);
            MiniBarChart($"Net kâr — dün {Ft(last.net)}", days.ConvertAll(d => (float)d.net),
                         new Color(0.35f, 0.8f, 0.5f), new Color(0.95f, 0.4f, 0.35f));
            MiniBarChart($"Teslimat — dün {last.deliveries} paket" + (last.cancelled > 0 ? $", {last.cancelled} iptal" : ""),
                         days.ConvertAll(d => (float)d.deliveries), new Color(0.4f, 0.75f, 1f), new Color(0.4f, 0.75f, 1f));
            MiniLineChart($"İtibar — dün {last.reputation:0.00}/5", days.ConvertAll(d => d.reputation), 0f, 5f, new Color(1f, 0.8f, 0.3f));
            GUILayout.Space(6);
        }

        bool showMilestones;

        void DrawMilestones()
        {
            var ms = Milestones.I;
            if (ms == null) return;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Kilometre taşları: {ms.ReachedCount}/{ms.Defs.Count}", header);
            if (GUILayout.Button(showMilestones ? "Gizle" : "Göster", smallButton, GUILayout.Width(70))) deferred = () => showMilestones = !showMilestones;
            GUILayout.EndHorizontal();
            if (!showMilestones) return;
            foreach (var m in ms.Defs)
            {
                string rw = m.RewardText;
                if (ms.IsReached(m.id))
                    GUILayout.Label($"• {m.title} — {ms.ReachedDate(m.id)}" + (rw.Length > 0 ? $" · ödül alındı: {rw}" : ""), label);
                else
                {
                    string p = m.progress != null ? m.progress() : "";
                    GUILayout.Label($"   {m.title}" + (string.IsNullOrEmpty(p) ? "" : $" ({p})") + (rw.Length > 0 ? $" · ödül: {rw}" : ""), small);
                }
            }
            GUILayout.Space(6);
        }

        /// <summary>Basit çubuk grafik; negatif değer varsa sıfır çizgisi ortadadır.</summary>
        void MiniBarChart(string title, List<float> values, Color positive, Color negative)
        {
            GUILayout.Label(title, small);
            Rect r = GUILayoutUtility.GetRect(100, 46, GUILayout.ExpandWidth(true));
            Fill(r, new Color(0f, 0f, 0f, 0.25f));
            float max = 0f;
            bool hasNeg = false;
            foreach (var v in values) { max = Mathf.Max(max, Mathf.Abs(v)); if (v < 0f) hasNeg = true; }
            if (max <= 0f || values.Count == 0) return;
            float zeroY = hasNeg ? r.y + r.height / 2f : r.yMax;
            float scale = (hasNeg ? r.height / 2f : r.height) / max;
            float bw = r.width / values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                float hgt = Mathf.Max(1f, Mathf.Abs(values[i]) * scale);
                var bar = values[i] >= 0f
                    ? new Rect(r.x + i * bw + 1f, zeroY - hgt, Mathf.Max(1f, bw - 2f), hgt)
                    : new Rect(r.x + i * bw + 1f, zeroY, Mathf.Max(1f, bw - 2f), hgt);
                Fill(bar, values[i] >= 0f ? positive : negative);
            }
            if (hasNeg) Fill(new Rect(r.x, zeroY - 0.5f, r.width, 1f), new Color(1f, 1f, 1f, 0.35f));
        }

        /// <summary>Basit çizgi grafik (noktalar ve aralarında dikey bağlantılar).</summary>
        void MiniLineChart(string title, List<float> values, float min, float max, Color color)
        {
            GUILayout.Label(title, small);
            Rect r = GUILayoutUtility.GetRect(100, 40, GUILayout.ExpandWidth(true));
            Fill(r, new Color(0f, 0f, 0f, 0.25f));
            if (values.Count == 0 || max <= min) return;
            float bw = r.width / values.Count;
            float prevY = 0f;
            for (int i = 0; i < values.Count; i++)
            {
                float t = Mathf.InverseLerp(min, max, values[i]);
                float x = r.x + i * bw + bw / 2f;
                float y = r.yMax - 3f - t * (r.height - 6f);
                if (i > 0) Fill(new Rect(x - bw / 2f - 1f, Mathf.Min(prevY, y), 2f, Mathf.Abs(prevY - y) + 1f), color);
                Fill(new Rect(x - 2f, y - 2f, 4f, 4f), color);
                prevY = y;
            }
        }

        /// <summary>Finans: sigorta ortaklığı (kuryeler isterse anlaşmalı sigortacıdan yaptırır, şirket komisyon alır).</summary>
        void DrawInsurancePartner(EconomyManager e)
        {
            GUILayout.BeginVertical(box);
            GUILayout.Label("Sigorta ortaklığı", header);
            HelpOnLast("Kuryeler sigorta yaptırmak zorunda değil. Yaptırmak isteyenler şirketin anlaşmalı sigortacısı üzerinden yaptırır, " +
                       "haftalık primden şirket komisyon alır. Dışarıdan sigortalı kuryeler zamanla anlaşmaya geçebilir.");
            var p = e.Plan;
            int insured = 0, via = 0;
            long weekly = 0;
            foreach (var c in CourierManager.I.Couriers)
            {
                if (!c.insured) continue;
                insured++;
                if (c.insuredViaCompany) { via++; weekly += GameData.Round10(c.CurrentPremium * p.commission); }
            }
            GUILayout.Label($"Şu an: {p.name} · sigortalı kurye {insured}/{CourierManager.I.Couriers.Count} (anlaşma üzerinden {via}) · " +
                            $"haftalık komisyon ~{Ft(weekly)} · bugüne kadar {Ft(e.insuranceIncomeTotal)}", label);
            for (int i = 0; i < EconomyManager.InsurancePlans.Length; i++)
            {
                var pl = EconomyManager.InsurancePlans[i];
                int idx = i;
                string txt = i == 0 ? $"{pl.name}" : $"{pl.name} · prim {Ft(pl.premium)}/hafta · şirket payı %{pl.commission * 100f:0}";
                GUI.enabled = modalFree && (e.CanChangeInsurancePlan || i == e.insurancePlan);
                if (SelectButton(e.insurancePlan == i, txt, smallButton)) deferred = () => e.SetInsurancePlan(idx);
                GUI.enabled = modalFree;
                GUILayout.Label(pl.desc, small);
            }
            if (!e.CanChangeInsurancePlan)
                GUILayout.Label($"Anlaşma {EconomyManager.InsurancePlanCooldownDays} günde bir değiştirilebilir ({GameClock.I.DateOf(e.insurancePlanChangeDay + EconomyManager.InsurancePlanCooldownDays)}).", small);
            GUILayout.EndVertical();
        }

        int financePage;   // 0 özet, 1 ödemeler, 2 vergi, 3 hareketler, 4 diğer

        void DrawFinance()
        {
            var e = EconomyManager.I;
            var clock = GameClock.I;
            // Finans sayfalara bölündü; grafikler Grafik penceresinde
            PageTabs(financePage, v => financePage = v, "Özet", "Ödemeler", "Vergi", "Hareketler", "Diğer");

            switch (financePage)
            {
                case 0:
                {
                    Section("Kasa", "Serbest kasa = ana kasa − kuryelerin hesaplarında bekleyen ve yatacak paralar + kuryelerdeki nakit (şirkete borçları). " +
                                    "Geliştirme, ekipman, sözleşme gibi harcamalar serbest kasadan yapılır.");
                    KV("Ana kasa", Ft(e.money));
                    KV("Serbest kasa (harcanabilir)", Ft(e.FreeCash), e.FreeCash < 0 ? UITheme.Bad : (Color?)null);
                    if (e.bankLoanRemaining > 0)
                        GUILayout.Label($"Acil banka kredisi: kalan {Ft(e.bankLoanRemaining)} · her gün {Ft(e.bankLoanDaily)} kesilir", warn);
                    if (e.taxDebt > 0)
                    {
                        GUILayout.Label($"Vergi borcu: {Ft(e.taxDebt)} — her gün %2 büyür!", warn);
                        if (GUILayout.Button($"Vergi borcunu öde ({Ft(e.taxDebt)})", button)) deferred = () => e.PayDebt();
                    }
                    GUILayout.BeginHorizontal();
                    // Genişlik yazıya göre verilir: satır kaydırmalı düğme dar kalınca yazı yarım görünüyordu
                    if (!string.IsNullOrEmpty(e.LastDailyReport) && GUILayout.Button("Dünün raporu", smallButton, TekSatir("Dünün raporu")))
                        deferred = () => EventManager.I.Push(new GameEvent(e.LastDailyReportTitle, e.LastDailyReport));
                    if (!string.IsNullOrEmpty(e.LastWeeklyReport) && GUILayout.Button("Geçen haftanın raporu", smallButton, TekSatir("Geçen haftanın raporu")))
                        deferred = () => EventManager.I.Push(new GameEvent(e.LastWeeklyReportTitle, e.LastWeeklyReport));
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(4);
                    DrawFamilyLoan();
                    Section("Bugün");
                    Stats(e.Today);
                    break;
                }
                case 1:
                {
                    Section("Kurye ödemeleri");
                    Intro("finans.kurye", "Kurye kazançları hesaplarında birikir; ayın 15'i ve son günü kasandan ödenir.",
                          "Bahşişler dahildir. Hesap gece kesilir, 1–2 iş günü içinde kasadan ödenir. Kuryeler istedikleri gün hesaplarının %80'ini " +
                          "erken çekebilir (2–3 iş günü). " + (Sade.NakitBorcu
                              ? $"Nakit siparişlerde kurye parayı şirkete borçlanır: üzerindeki nakit {Ft(Courier.CashLimit)}'ı geçemez, " +
                                "ay sonuna kadar keep cash (hesabın en fazla %80'iyle mahsup) ya da offload (kartla ödeme) ile kapatılır."
                              : "Nakit siparişlerin parası doğrudan kasana girer."));
                    KV("Kuryelerin hesaplarında", Ft(e.TotalCourierBalances));
                    KV("Sıradaki kesim", e.NextCutText);
                    KV("Yatmayı bekleyen ödemeler", Ft(e.TotalPendingPayouts));
                    foreach (var p in e.Payouts)
                        GUILayout.Label($"   {p.courierName}: {Ft(p.amount)}{(p.early ? " (erken)" : "")} → {GameClock.I.DateOf(p.payDay)}", small);
                    if (Sade.NakitBorcu || e.TotalCashHeld > 0) KV("Kuryelerdeki nakit (şirkete borçları)", Ft(e.TotalCashHeld));
                    if (e.depositsHeld > 0)
                    {
                        KV("Tutulan ekipman depozitoları", Ft(e.depositsHeld));
                        HelpOnLast("Gelir değil: ekipmanı iade eden kuryeye geri ödenir.");
                    }
                    break;
                }
                case 2:
                {
                    Section($"Bu vergi yılı · {clock.Year}", $"1 Ocak – 31 Aralık. Şirketin {clock.CompanyDay}. günü. Yıl sonunda kâr üzerinden vergi ödenir.");
                    Stats(e.Year);
                    long taxable = System.Math.Max(0L, e.Year.Profit);
                    long est = EconomyManager.ProgressiveTax(taxable);
                    KV($"Tahmini yıl sonu vergisi (kademeli, ortalama %{EconomyManager.EffectiveTaxRate(taxable) * 100f:0.0})", Ft(est));
                    if (e.Yesterday != null)
                    {
                        Section("Dün");
                        Stats(e.Yesterday);
                    }
                    if (!string.IsNullOrEmpty(e.LastTaxSummary))
                    {
                        Section("Son vergi dönemi");
                        GUILayout.Label(e.LastTaxSummary, small);
                    }
                    break;
                }
                case 3:
                {
                    Section("Hesap hareketleri", "Son 25 hareket, en yenisi üstte.");
                    for (int i = 0; i < Mathf.Min(25, e.Ledger.Count); i++) GUILayout.Label(e.Ledger[i], small);
                    break;
                }
                default:
                {
                    DrawInsurancePartner(e);
                    GUILayout.Space(4);
                    DrawMilestones();
                    break;
                }
            }
        }

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

        void DrawSettings()
        {
            var e = EconomyManager.I;
            // Uzun ayarlar sayfalara bölündü: aradığını bul, hepsini okumak zorunda kalma
            PageTabs(settingsPage, v => settingsPage = v, "Oyun", "Ekonomi", "Harita", "Gelişmiş");
            switch (settingsPage)
            {
                case 0:
                    DrawInterfaceSettings();
                    Section("Gece");
                    bool nf = GUILayout.Toggle(GameClock.NightFast, $" Gece 00:00–05:00 arası zaman kendiliğinden {GameClock.NightSpeed:0} kat hızlı geçsin (en fazla 64×)", toggle);
                    if (nf != GameClock.NightFast) GameClock.NightFast = nf;
                    GUILayout.Space(8);
                    Section("Gün sonu raporu");
                    e.showDailyReport = GUILayout.Toggle(e.showDailyReport, " Her gece gün sonu raporunu göster (oyun duraklar)", toggle);
                    e.showWeeklyReport = GUILayout.Toggle(e.showWeeklyReport, " Her Pazar gecesi haftalık raporu göster", toggle);
                    GUILayout.Space(8);

                    Section("Para birimi", $"Sadece gösterim değişir, oyun dengesi aynı kalır (1 € = {GameData.FtPerEuro:0} Ft). " +
                        "Seçim kaydedilir. Daha önce yazılmış bildirim ve hesap kayıtları yazıldıkları birimde kalır.");
                    GUILayout.BeginHorizontal();
                    if (SelectButton(GameData.DisplayCurrency == Currency.Forint, "Forint (Ft)", smallButton))
                        deferred = () => GameData.SetCurrency(Currency.Forint);
                    if (SelectButton(GameData.DisplayCurrency == Currency.Euro, "Euro (€)", smallButton))
                        deferred = () => GameData.SetCurrency(Currency.Euro);
                    GUILayout.EndHorizontal();
                    GUILayout.Space(8);

                    break;
                case 1:
                    Section("Fiyatlandırma (müşteriden alınan teslimat ücreti)",
                        "Teslimat ücreti = taban + km ücreti × km. Bu, siparişi büyük ihtimalle götürecek kuryenin " +
                        "(o ağırlığı taşıyabilen ve müsait en ucuz kurye) ücreti + min. kâr − komisyonu karşılamıyorsa fark 'ek' olarak eklenir. " +
                        "Zararına iş yine yapılmaz: kâr bırakmayan kuryeye sipariş atanmaz, uygun kurye beklenir. Ücret yükseldikçe vazgeçen müşteri artar.\n\n" +
                        $"Atamada min. kâr: kuryeye ödeme sonrası şirkete en az bu kadar kalmalı. {e.relaxAfterMinutes:0} dakikadan uzun bekleyen " +
                        "siparişte şart 0'a iner, böylece sipariş iptal olmaz.");
                    e.baseFee = MoneySlider("Taban ücret", e.baseFee, 200, 1500, 10);
                    e.feePerKm = MoneySlider("Km başı", e.feePerKm, 0, 400, 10);
                    e.minProfit = MoneySlider("Min. kâr/sipariş", e.minProfit, 0, 1000, 50);
                    e.minAssignProfit = MoneySlider("Atamada min. kâr", e.minAssignProfit, 0, 600, 10);

                    GUILayout.Space(8);
                    Section("Kurye ücretleri (araca göre)");
                    foreach (var v in GameData.Vehicles)
                        GUILayout.Label($"{v.name}: {Ft(v.basePay)}/paket + {Ft(v.payPerKm)}/km · {GameData.CarryText(v)}", small);

                    GUILayout.Space(8);
                    Section("Kurye bonusları", "Yüksek bonus moral ve daha çok başvuru getirir ama kârı düşürür. Bonus = hava bonusu + saat bonusu, çarpanla çarpılır.");
                    e.bonusMultiplier = FloatSlider("Bonus çarpanı", e.bonusMultiplier, 0.5f, 2f);
                    KV("Şu anki bonus", $"{Ft(e.CurrentBonus)} (hava {Ft(WeatherSystem.I.WeatherBonus)} + saat {Ft(e.TimeBonus())})");

                    GUILayout.Space(8);
                    Section("Vergi", Sade.SurprizVergi
                        ? "31 Aralık'ta (gelir − giderler) üzerinden ödenir; giderler vergiden düşülür. Yıl içinde de habersiz vergi ve harçlar çıkar: " +
                          "yerel işletme vergisi, KDV farkı, vergi denetimi, belediye harcı, iş güvenliği fonu. Bazen de vergi iadesi gelir. Kasanda her zaman biraz yedek para tut."
                        : "Tek vergi var: 31 Aralık'ta yılın kârı (gelir − giderler) üzerinden ödenir; giderler vergiden düşülür. " +
                          "Yıl içinde başka vergi ya da harç çıkmaz; yıl sonu için kasanda para ayır.");
                    KV("Yıl sonu vergisi (kâr üzerinden)", "kademeli");
                    GUILayout.Label(EconomyManager.TaxBracketText, small);
                    HelpOnLast("Kârın her dilimi kendi oranıyla vergilenir: şirket büyüdükçe kârın üst kısmından daha yüksek oranda vergi ödenir.");

                    GUILayout.Space(8);
                    Section("Örnek hesap (şu anki ayarlarla)");
                    const int basket = 5000;
                    const int tiles = 15;
                    int commission = Mathf.RoundToInt(basket * 0.22f);
                    int fee = e.QuoteDeliveryFee(commission, tiles, 2f);
                    int pay = e.ExpectedCourierPay(tiles, 2f);
                    KV($"Sepet {Ft(basket)}, komisyon %22", Ft(commission));
                    KV($"Mesafe {GameData.KmText(tiles)}, teslimat ücreti", Ft(fee));
                    KV("Şirket geliri", Ft(commission + fee));
                    KV("Muhtemel kuryeye ödeme", "−" + Ft(pay));
                    KV("Kâr", Ft(commission + fee - pay), MoneyColor(commission + fee - pay));
                    break;
                case 2:
                    DrawGraphicsSettings();
                    Section("Harita");
                    bool lm = GUILayout.Toggle(ShowLandmarks, " Yer adlarını göster (park, nehir, AVM, bölgeler)", toggle);
                    if (lm != ShowLandmarks) ShowLandmarks = lm;
                    bool dn = GUILayout.Toggle(DayNight.Enabled, " Gece ve gündüz: akşam turuncu ışık, gece karanlık, pencere ışıkları ve sokak lambaları", toggle);
                    if (dn != DayNight.Enabled) DayNight.Enabled = dn;
                    bool wfx = GUILayout.Toggle(WeatherFx.Enabled, " Hava efektleri: yağmur, kar, rüzgâr, güneş, şimşek, kar örtüsü", toggle);
                    if (wfx != WeatherFx.Enabled) WeatherFx.Enabled = wfx;
                    if (WeatherFx.Enabled && WeatherSystem.I != null)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Havayı dene:", small, GUILayout.Width(90));
                        foreach (WeatherKind wk in System.Enum.GetValues(typeof(WeatherKind)))
                        {
                            var kind = wk;
                            if (SelectButton(WeatherSystem.I.Current == wk, WeatherName(wk), smallButton))
                                deferred = () => WeatherSystem.I.Restore(kind);
                        }
                        GUILayout.EndHorizontal();
                    }
                    bool traffic = GUILayout.Toggle(Traffic.Enabled, " Trafik: arabalar, otobüsler, yayalar (kapatmak performansı artırır)", toggle);
                    if (traffic != Traffic.Enabled) deferred = () => { Traffic.Enabled = traffic; if (Traffic.I != null) Traffic.I.Build(); };
                    if (Traffic.Enabled)
                    {
                        int nv = Traffic.PackVehicleCount, np = Traffic.PackPeopleCount;
                        GUILayout.Label(nv + np > 0
                            ? $"Şehir paketinden {nv} araç ve {np} insan modeli kullanılıyor."
                            : "Şehir paketinin araç ve insanları kurulu değil: basit kutular kullanılıyor. (Kurye Tycoon > Şehir Paketini Kur'u tekrar çalıştır.)", small);
                        if (nv > 0)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Araç yönü düzeltme (burnu ters gidiyorsa):", small, GUILayout.Width(250));
                            foreach (int ang in new[] { 0, 90, 180, 270 })
                            {
                                int a2 = ang;
                                if (SelectButton(Traffic.FacingOffset == ang, $"{ang}°", smallButton))
                                    deferred = () => { Traffic.FacingOffset = a2; if (Traffic.I != null) Traffic.I.Build(); };
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                    bool persp = GUILayout.Toggle(MapCamera.PerspectiveSetting, " Perspektif kamera (derinlik hissi; kapalıysa düz/ortografik)", toggle);
                    if (persp != MapCamera.PerspectiveSetting) MapCamera.PerspectiveSetting = persp;
                    GUILayout.Space(8);

                    break;
                default:
                    Section("3D şehir paketi", "Şehir paketi kurulumu ve bina/yol yönleri. Geliştirme sırasında paket kurmak için.");
                    int nb = City3D.PackBuildingCount, nt = City3D.PackTreeCount;
                    if (nb == 0)
                    {
                        GUILayout.Label("Şehir paketi kurulu değil: yakınlaştırınca basit kutu binalar görünür. Kurmak için Asset Store'dan şehir paketini " +
                                        "(ör. SimplePoly City) içe aktar, sonra Unity menüsünden Kurye Tycoon > Şehir Paketini Kur. (Ayrıntı KURULUM.md'de.)", small);
                    }
                    else
                    {
                        GUILayout.Label($"Şehir paketi: {nb} bina, {nt} ağaç.", small);
                        bool use = GUILayout.Toggle(City3D.UsePack, " Paketin binalarını kullan (kapalıysa kutu binalar)", toggle);
                        if (use != City3D.UsePack) deferred = () => { City3D.UsePack = use; if (City3D.I != null) City3D.I.Rebuild(); };
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Bina cephesi düzeltme:", small, GUILayout.Width(150));
                        foreach (int a in new[] { 0, 90, 180, 270 })
                        {
                            int ang = a;
                            if (SelectButton(City3D.FrontOffset == ang, $"{ang}°", smallButton))
                                deferred = () => { City3D.FrontOffset = ang; if (City3D.I != null) City3D.I.Rebuild(); };
                        }
                        GUILayout.EndHorizontal();
                        GUILayout.Label("Binaların arkası yola bakıyorsa 180°'yi, yanı bakıyorsa 90° ya da 270°'yi dene.", small);

                        if (City3D.PackRoadsOk)
                        {
                            bool roads = GUILayout.Toggle(City3D.UseRoads, " Paketin yollarını ve kaldırımlarını kullan", toggle);
                            if (roads != City3D.UseRoads) deferred = () => { City3D.UseRoads = roads; if (City3D.I != null) City3D.I.Rebuild(); };
                            if (City3D.UseRoads)
                            {
                                string[] kinds = { "Düz yol", "Köşe", "T kavşak" };
                                for (int k = 0; k < 3; k++)
                                {
                                    int kind = k;
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Label($"{kinds[k]} yönü:", small, GUILayout.Width(150));
                                    foreach (int a2 in new[] { 0, 90, 180, 270 })
                                    {
                                        int ang2 = a2;
                                        if (SelectButton(City3D.RoadOffset(kind) == ang2, $"{ang2}°", smallButton))
                                            deferred = () => { City3D.SetRoadOffset(kind, ang2); if (City3D.I != null) City3D.I.Rebuild(); };
                                    }
                                    GUILayout.EndHorizontal();
                                }
                                GUILayout.Label("Yol çizgileri yola dik duruyorsa ya da köşeler/kavşaklar yanlış yöne açılıyorsa ilgili türün yönünü değiştir.", small);
                            }
                        }
                    }
                    GUILayout.Space(8);

                    Section("Kurye karakterleri (3D)");
                    int cnt = CharacterVisuals.Count;
                    if (cnt == 0)
                    {
                        GUILayout.Label("Karakter modeli bulunamadı; kuryeler daire olarak çiziliyor. Kurmak için: Asset Store'dan karakter paketini içe aktar, " +
                                        "sonra Unity menüsünden Kurye Tycoon > Karakterleri Kur. (Ayrıntı KURULUM.md'de.)", small);
                    }
                    else
                    {
                        GUILayout.Label($"{cnt} karakter modeli yüklü · yürüme animasyonu: " +
                                        (CharacterVisuals.HasWalkAnimation ? "kurulu" : "KURULU DEĞİL (Unity menüsü: Kurye Tycoon > Karakter Animasyonlarını Kur)"),
                                        CharacterVisuals.HasWalkAnimation ? small : warn);
                        GUILayout.BeginHorizontal();
                        int mode = CharacterVisuals.Mode;
                        if (SelectButton(mode == 0, "Işıklı", smallButton)) deferred = () => { CharacterVisuals.Mode = 0; CharacterVisuals.Refresh(); };
                        if (SelectButton(mode == 1, "Düz renk", smallButton)) deferred = () => { CharacterVisuals.Mode = 1; CharacterVisuals.Refresh(); };
                        if (SelectButton(mode == 2, "Kapalı (daire)", smallButton)) deferred = () => { CharacterVisuals.Mode = 2; CharacterVisuals.Refresh(); };
                        GUILayout.EndHorizontal();
                        GUILayout.Label("Karakterler siyah/karanlık görünürse \"Düz renk\"i seç. Pembe görünürse malzemeler projenin render pipeline'ına çevrilmeli (KURULUM.md).", small);
                    }
                    GUILayout.Space(8);

                    Section("Hata ayıklama raporu",
                        "Oyunun o anki bütün durumunu (kasa, gider dökümü, günlük geçmiş, kuryeler, dükkanlar, siparişler, geliştirmeler, " +
                        "iptal ve kaçan sipariş sebepleri, son bildirimler ve kaydın tamamı) tek bir metin dosyasına yazar. " +
                        "Sorunları bulmak için bu dosyayı gönder. Kısayol: F9.");
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Hata ayıklama raporu oluştur", button)) deferred = () => { if (DebugReport.I != null) DebugReport.I.Write(true); };
                    if (GUILayout.Button("Klasörü aç", smallButton, GUILayout.Width(100))) deferred = DebugReport.OpenFolder;
                    GUILayout.EndHorizontal();
                    GUILayout.Label(DebugReport.I != null && DebugReport.I.lastPath != "" ? $"Son rapor: {DebugReport.I.lastPath}" : $"Klasör: {DebugReport.Folder}", small);
                    GUILayout.Space(8);

                    var stats = StatsHistory.I;
                    if (stats != null)
                    {
                        Section("Denge kaydı (CSV)",
                            "Teslimat, iptal, gelir, gider, kâr, kasa, itibar, kurye ve dükkan sayısı her gece bir satır olarak yazılır. " +
                            "Excel ya da Google Sheets ile açılır (ayraç ';'). Her oyun oturumu yeni bir dosya açar, kayıttan devam edilen " +
                            "oyunun eski günleri de içine yazılır.");
                        stats.writeCsv = GUILayout.Toggle(stats.writeCsv, " Her gün sonunda verileri CSV dosyasına yaz (test ve denge için)", toggle);
                        GUILayout.Label(string.IsNullOrEmpty(stats.CsvPath) ? $"Klasör: {StatsHistory.CsvFolder}" : $"Dosya: {stats.CsvPath}", small);
                        if (GUILayout.Button("Klasörü aç", smallButton))
                            deferred = () =>
                            {
                                System.IO.Directory.CreateDirectory(StatsHistory.CsvFolder);
                                Application.OpenURL(new System.Uri(StatsHistory.CsvFolder).AbsoluteUri);
                            };
                        GUILayout.Space(8);
                    }

                    break;
            }
        }

        int settingsPage;   // 0 oyun, 1 ekonomi, 2 harita, 3 gelişmiş

        // ---------------------------------------------------------------- kontroller
        /// <summary>Seçili olan şirket renginde dolu görünen düğme.</summary>
        bool SelectButton(bool selected, string text, GUIStyle style, params GUILayoutOption[] options)
        {
            bool now = GUILayout.Toggle(selected, text, style, options);
            return now != selected;
        }

        bool SelectButtonAt(Rect r, bool selected, string text)
        {
            bool now = GUI.Toggle(r, selected, text, button);
            return now != selected;
        }

        /// <summary>Etiket, kaydırıcı ve değer aynı hizada.</summary>
        float SliderRow(string name, float v, float min, float max, string valueText, float labelWidth, float valueWidth = 50f)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, label, GUILayout.Width(labelWidth), GUILayout.Height(24));
            GUILayout.BeginVertical();
            GUILayout.Space(9);
            v = GUILayout.HorizontalSlider(v, min, max);
            GUILayout.EndVertical();
            GUILayout.Label(valueText, label, GUILayout.Width(valueWidth), GUILayout.Height(24));
            GUILayout.EndHorizontal();
            return v;
        }

        int IntSlider(string name, int v, int min, int max, int step)
        {
            float f = SliderRow(name, v, min, max, v.ToString(), 140);
            return Mathf.RoundToInt(f / step) * step;
        }

        /// <summary>Para tutarı kaydırıcısı: değer içeride Ft, ekranda seçili para biriminde yazılır.</summary>
        int MoneySlider(string name, int v, int min, int max, int step)
        {
            float f = SliderRow(name, v, min, max, Ft(v), 140, 80);
            return Mathf.RoundToInt(f / step) * step;
        }

        float FloatSlider(string name, float v, float min, float max)
        {
            float f = SliderRow(name, v, min, max, v.ToString("0.00"), 140);
            return Mathf.Round(f * 100f) / 100f;
        }

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

        // ---------------------------------------------------------------- haber akışı
        void DrawFeed(float sw, float sh)
        {
            var feed = EventManager.I.Feed;
            if (feed.Count == 0) return;
            int lines = Mathf.Min(5, feed.Count);
            var r = new Rect(sw - 530f, sh - 22f * lines - 22f, 518f, 22f * lines + 12f);
            UITheme.Shadowed(r, new Color(0.07f, 0.075f, 0.09f, 0.88f), 12f, 4f);
            for (int i = 0; i < lines; i++)
            {
                var row = new Rect(r.x + 10, r.y + 6 + i * 22f, r.width - 20, 22f);
                UITheme.Box(new Rect(row.x, row.y + 8, 5, 5), i == 0 ? UITheme.Accent : UITheme.WithAlpha(UITheme.TextDim, 0.5f), 2.5f);
                small.normal.textColor = UITheme.Yazi(i == 0 ? UITheme.Text : UITheme.TextDim);
                GUI.Label(new Rect(row.x + 12, row.y, row.width - 12, row.height), feed[i], small);
            }
            small.normal.textColor = UITheme.Yazi(UITheme.TextDim);
        }

        // ---------------------------------------------------------------- olay kartı
        Vector2 eventScroll;
        GameEvent eventScrollFor;

        void DrawEvent(float sw, float sh)
        {
            var ev = EventManager.I.Current;
            if (ev == null) return;

            Fill(new Rect(0, 0, sw, sh), new Color(0.02f, 0.025f, 0.035f, 0.62f));
            bool ozetli = ev.ozet != null && ev.ozet.Count > 0;
            float w = ozetli ? 640f : 580f;
            var govdeStil = ozetli ? small : label;   // özetli kartta metin ayrıntıdır: küçük yazılır
            if (olayAyrintiFor != ev) { olayAyrintiFor = ev; olayAyrintiAcik = false; }
            // Rapor kartı: üstte özet kutuları ve ilk paragraf; dökümün geri kalanı "Ayrıntılar" ile açılır (metin duvarı olmasın)
            string ilk = ev.body ?? "";
            int kesim = ozetli ? ilk.IndexOf("\n\n", System.StringComparison.Ordinal) : -1;
            bool katlanir = kesim > 0 && kesim < ilk.Length - 2;
            string govdeMetni = katlanir && !olayAyrintiAcik ? ilk.Substring(0, kesim) : ilk;
            float bodyH = govdeStil.CalcHeight(new GUIContent(govdeMetni), w - 56f) + (katlanir ? 32f : 0f);
            float ozetH = ozetli ? 74f : 0f;
            float h = Mathf.Min(sh - 40f, 118f + ozetH + bodyH + ev.choices.Count * 46f);
            var r = new Rect((sw - w) / 2f, (sh - h) / 2f, w, h);
            UITheme.Shadowed(r, new Color(0.115f, 0.125f, 0.155f), 18f, 10f);
            UITheme.Box(new Rect(r.x, r.y, r.width, 54f), UITheme.WithAlpha(UITheme.Accent, 0.16f), 18f);
            UITheme.Box(new Rect(r.x + 26, r.y + 18, 6, 22), UITheme.Accent, 3f);

            GUILayout.BeginArea(new Rect(r.x + 28, r.y + 12, w - 56, h - 24));
            GUILayout.BeginHorizontal();
            GUILayout.Space(14);
            GUILayout.Label(ev.title, title);
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            if (ozetli) KartOzetSatiri(ev.ozet, w - 56f);
            // Uzun kart (ör. haftalık rapor) ekrana sığmazsa metin kayar; seçenek düğmeleri her zaman görünür kalır
            float bodyRoom = h - 118f - ozetH - ev.choices.Count * 46f;
            if (bodyH > bodyRoom + 1f)
            {
                if (eventScrollFor != ev) { eventScrollFor = ev; eventScroll = Vector2.zero; }
                eventScroll = GUILayout.BeginScrollView(eventScroll, GUILayout.Height(Mathf.Max(60f, bodyRoom)));
                GUILayout.Label(govdeMetni, govdeStil);
                GUILayout.EndScrollView();
            }
            else GUILayout.Label(govdeMetni, govdeStil);
            if (katlanir && GUILayout.Button(olayAyrintiAcik ? "Ayrıntıları gizle ▲" : "Ayrıntılar (gelir, gider, kasa) ▼", smallButton, GUILayout.Height(26)))
                deferred = () => olayAyrintiAcik = !olayAyrintiAcik;
            GUILayout.Space(12);
            for (int i = 0; i < ev.choices.Count; i++)
            {
                int idx = i;
                // İlk seçenek vurgulu (çoğunlukla önerilen / "Tamam")
                if (GUILayout.Button(ev.choices[i].label, i == 0 ? accentButton : button, GUILayout.Height(38)))
                    deferred = () => EventManager.I.Choose(idx);
            }
            GUILayout.EndArea();
        }

        GUIStyle ozetBaslik, ozetDeger;
        GameEvent olayAyrintiFor;
        bool olayAyrintiAcik;

        /// <summary>Rapor kartının üstündeki büyük sayılar: yan yana kutular (iyi yeşil, kötü kırmızı, dikkat turuncu).</summary>
        void KartOzetSatiri(List<KartOzeti> ozet, float genislik)
        {
            if (ozetBaslik == null)
            {
                ozetBaslik = UITheme.Semi(new GUIStyle(small) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, wordWrap = false });
                ozetDeger = UITheme.Semi(new GUIStyle(label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false });
            }
            var satir = GUILayoutUtility.GetRect(genislik, 62f, GUILayout.Height(62f));
            int n = ozet.Count;
            float gap = 8f, kw = (satir.width - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                var o = ozet[i];
                var r = new Rect(satir.x + i * (kw + gap), satir.y, kw, satir.height);
                Color ton = o.ton == 1 ? UITheme.Good : o.ton == 2 ? UITheme.Bad : o.ton == 3 ? UITheme.Warn : UITheme.Text;
                UITheme.Box(r, o.ton == 0 ? UITheme.WithAlpha(Color.white, 0.05f) : UITheme.WithAlpha(ton, 0.13f), 10f);
                ozetBaslik.normal.textColor = UITheme.Yazi(UITheme.TextDim);
                GUI.Label(new Rect(r.x, r.y + 8f, r.width, 16f), o.baslik.ToUpperInvariant(), ozetBaslik);
                ozetDeger.normal.textColor = UITheme.Yazi(ton);
                GUI.Label(new Rect(r.x + 4f, r.y + 24f, r.width - 8f, 30f), o.deger, ozetDeger);
            }
            GUILayout.Space(10);
        }

        // ---------------------------------------------------------------- yardımcılar
        static void Fill(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = UITheme.Zemin(c);     // kâğıt tema: koyu zeminler kreme döner
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

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

        /// <summary>Yuvarlak köşeli düz düğme stili. Seçili (on) hali şirket renginde.</summary>
        /// <summary>Küçük düğmenin yazısı tek satıra sığsın diye genişlik ve yükseklik.</summary>
        GUILayoutOption[] TekSatir(string yazi)
        {
            var sz = smallButton.CalcSize(new GUIContent(yazi));
            return new[] { GUILayout.Width(sz.x + 6f), GUILayout.Height(Mathf.Max(26f, sz.y)) };
        }

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

        /// <summary>Şirket rengi değişince (kurulum ekranında) düğme stillerini yeniden üret.</summary>
        public void RefreshTheme() { stylesReady = false; }
    }
}
