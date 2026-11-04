using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Prototip arayüzü (Unity IMGUI). Kurulum ekranı, üst bar, sol panel sekmeleri,
    /// haber akışı ve olay kartları. Hiçbir Canvas / prefab gerektirmez.
    /// </summary>
    public partial class GameUI : MonoBehaviour
    {
        enum Tab { Siparisler, Kuryeler, IseAlim, Destek, Gelistirmeler, Dukkanlar, Ekipman, Kampanyalar, Finans, Ayarlar }
        static readonly string[] BaseTabNames = { "Siparişler", "Kuryeler", "İşe Alım", "Destek", "Geliştirmeler", "Dükkanlar", "Ekipman", "Kampanyalar", "Finans", "Ayarlar" };

        public float panelWidth = 440f;
        const float TopBar = 48f;

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

        void Start()
        {
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
        }

        void LateUpdate()
        {
            // Harita kamerası sol panel ve üst bar dışındaki alana çizer (yakınlaştırma/kaydırma MapCamera'da)
            if (MapCamera.I == null) return;
            float left = Mathf.Clamp01(panelWidth * uiScale / Screen.width);
            float top = Mathf.Clamp01(TopBar * uiScale / Screen.height);
            MapCamera.I.SetViewport(new Rect(left, 0f, 1f - left, 1f - top));
        }

        // =====================================================================
        void OnGUI()
        {
            uiScale = Mathf.Clamp(Screen.height / 900f, 0.8f, 2f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));
            float sw = Screen.width / uiScale;
            float sh = Screen.height / uiScale;
            InitStyles();

            if (GameBootstrap.I == null) return;
            if (!GameBootstrap.I.Started)
            {
                DrawSetup(sw, sh);
                return;
            }

            if (EventManager.I.Current == null) HandleMenuKey();
            modalFree = EventManager.I.Current == null && !pauseMenuOpen;
            if (modalFree) HandleKeys();
            if (!pauseMenuOpen) HandleMapMouse(sw, sh);

            GUI.enabled = modalFree;
            DrawWorldLabels();
            DrawTopBar(sw);
            DrawPanel(sh);
            DrawFeed(sw, sh);
            DrawZoomButtons(sh);
            DrawLegend(sh);
            DrawCourierPopup(sw, sh);
            GUI.enabled = true;
            DrawEvent(sw, sh);
            if (pauseMenuOpen) DrawPauseMenu(sw, sh);
        }

        // ---------------------------------------------------------------- ESC menüsü (kaydet / ana menü / çık)
        bool pauseMenuOpen, menuWasPaused, confirmMainMenu;

        void HandleMenuKey()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode != KeyCode.Escape) return;
            e.Use();
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
            float w = 400f, h = 420f;
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
                if (GUILayout.Button("Kaydet", button, GUILayout.Height(34))) deferred = () => save.Save(false);
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
        SaveData setupSave;
        bool setupSaveChecked;

        void DrawSetup(float sw, float sh)
        {
            if (!setupSaveChecked)
            {
                setupSaveChecked = true;
                setupSave = SaveSystem.ReadSave();
            }
            bool hasError = !string.IsNullOrEmpty(GameBootstrap.LastLoadError);

            Fill(new Rect(0, 0, sw, sh), new Color(0.08f, 0.09f, 0.11f));
            float w = 620f, h = 680f + (setupSave != null ? 120f : 0f) + (hasError ? 40f : 0f);
            var r = new Rect((sw - w) / 2f, Mathf.Max(10f, (sh - h) / 2f), w, h);
            UITheme.Shadowed(r, new Color(0.115f, 0.125f, 0.155f), 18f, 10f);

            GUILayout.BeginArea(new Rect(r.x + 24, r.y + 20, w - 48, h - 40));
            if (hasError) GUILayout.Label(GameBootstrap.LastLoadError, warn);
            if (setupSave != null)
            {
                GUILayout.BeginVertical(box);
                GUILayout.Label("Kayıtlı oyun", header);
                GUILayout.Label(SaveSystem.Summary(setupSave), small);
                if (GUILayout.Button("Kayıtlı oyuna devam et", bigButton, GUILayout.Height(40)))
                    deferred = () => { if (!GameBootstrap.I.LoadGame()) setupSaveChecked = false; };
                GUILayout.EndVertical();
                GUILayout.Label("Ya da yeni bir şirket kur (yeni oyun ilk gece kaydedilince eski kaydın yerini alır):", small);
                GUILayout.Space(6);
            }
            GUILayout.Label("Kurye Şirketini Kur", title);
            GUILayout.Label($"Yıllarca kuryelik yaptın. Şimdi annenle babanın emeklilik birikimi olan {Ft(GameBootstrap.I.familyLoanAmount)} ile kendi kurye " +
                            "şirketini kuruyorsun. Bu parayı Temmuz sonuna kadar taksitlerle geri ödemen gerekiyor.", label);
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

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Şirketi Kur ve Başla", bigButton, GUILayout.Height(50)))
            {
                CompanyProfile.Name = string.IsNullOrEmpty(companyName.Trim()) ? "Hızlı Kurye" : companyName.Trim();
                CompanyProfile.Primary = primary;
                CompanyProfile.Secondary = secondary;
                deferred = () => GameBootstrap.I.StartGame();
            }
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- klavye
        void HandleKeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;
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
                case KeyCode.Alpha0: case KeyCode.Keypad0: case KeyCode.Home: mc.ResetView(); e.Use(); break;
            }
        }

        static void SetSpeed(float s)
        {
            GameClock.I.SpeedMultiplier = s;
            GameClock.I.Paused = false;
        }

        // ---------------------------------------------------------------- harita etiketleri
        void DrawWorldLabels()
        {
            var cam = Camera.main;
            if (cam == null || CityMap.I == null) return;

            // Yer adları (park, nehir, AVM, bölge): kilitli bölgedekiler soluk
            if (ShowLandmarks)
                foreach (var lm in CityMap.Landmarks)
                {
                    bool open = CityMap.I.IsUnlocked(Mathf.RoundToInt(lm.pos.x));
                    var c = open ? lm.color : UITheme.WithAlpha(lm.color, 0.45f);
                    LandmarkLabel(cam, CityMap.ToWorld(lm.pos) + MapCamera.LabelOffset(0f, 0.05f), lm.name, c);
                }

            foreach (var s in CityMap.I.Shops)
            {
                if (!CityMap.I.IsUnlocked(s.tile.x)) continue;
                WorldLabel(cam, CityMap.ToWorld(s.tile) + MapCamera.LabelOffset(0.7f, BuildingTop(s.tile) + 0.2f),
                    s.contracted ? s.Name : s.Name + " (sözleşmesiz)",
                    s.contracted ? Color.white : new Color(0.7f, 0.7f, 0.7f));
            }

            float hqTop = BuildingTop(CityMap.I.HQTile);
            WorldLabel(cam, CityMap.ToWorld(CityMap.I.HQTile) + MapCamera.LabelOffset(0f, hqTop + 0.05f), CompanyProfile.Initial, CompanyProfile.Secondary);
            WorldLabel(cam, CityMap.ToWorld(CityMap.I.HQTile) + MapCamera.LabelOffset(-0.7f, hqTop + 0.35f), "Merkez", Color.white);

            // Rakip kuryeler: sadece yakındayken, soluk mor etiket
            if (RivalFleet.I != null && MapCamera.I != null && MapCamera.I.ShowModels)
                foreach (var a in RivalFleet.I.Agents)
                    if (a != null && a.Visible)
                        WorldLabel(cam, CityMap.ToWorld(a.Position) + MapCamera.LabelOffset(0.5f, 0.55f), a.label,
                                   Color.Lerp(RivalFleet.RivalColor, Color.white, 0.4f));

            // Aynı karedeki kuryelerin isimleri üst üste binmesin
            var stack = new Dictionary<Vector2Int, int>();
            foreach (var c in CourierManager.I.Couriers)
            {
                var key = Vector2Int.RoundToInt(c.Position);
                stack.TryGetValue(key, out int n);
                stack[key] = n + 1;
                bool hi = c == popupCourier || c == hoverCourier;
                WorldLabel(cam, CityMap.ToWorld(c.Position) + CourierLabelOffset(n),
                    $"{c.ShortName} ({c.V.letter})", hi ? new Color(1f, 0.85f, 0.2f) : Color.white);
            }
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
                landmarkStyle = new GUIStyle(worldLabel) { fontStyle = FontStyle.BoldAndItalic, fontSize = 13, alignment = TextAnchor.MiddleCenter };
            float x = sp.x / uiScale;
            float y = (Screen.height - sp.y) / uiScale;
            var r = new Rect(x - 110f, y - 11f, 220f, 22f);
            landmarkStyle.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.75f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, landmarkStyle);
            landmarkStyle.normal.textColor = color;
            GUI.Label(r, text, landmarkStyle);
        }

        bool legendOpen;

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
            float rowH = 19f, w = 250f, h = items.Count * rowH + 34f;
            var r = new Rect(panelWidth + 10f, sh - 52f - h, w, h);
            UITheme.Shadowed(r, new Color(0.07f, 0.075f, 0.09f, 0.92f), 12f, 4f);
            small.normal.textColor = UITheme.Text;
            GUI.Label(new Rect(r.x + 12, r.y + 6, w - 24, 20), "Lejant", small);
            for (int i = 0; i < items.Count; i++)
            {
                float yy = r.y + 28f + i * rowH;
                UITheme.Box(new Rect(r.x + 12, yy + 3, 14, 13), items[i].c, 3f);
                small.normal.textColor = UITheme.TextDim;
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
            bool overMap = m.x > panelWidth && m.y > TopBar &&
                           !(popupCourier != null && PopupRect(sw, sh).Contains(m)) &&
                           !new Rect(panelWidth + 10f, sh - 44f, 205f, 30f).Contains(m);
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
                        e.Use();
                    }
                }
            }
        }

        void DrawZoomButtons(float sh)
        {
            var mc = MapCamera.I;
            if (mc == null) return;
            float x = panelWidth + 10f, y = sh - 44f;
            if (GUI.Button(new Rect(x, y, 34, 30), "+", button)) mc.ZoomStep(1.5f);
            if (GUI.Button(new Rect(x + 38, y, 34, 30), "−", button)) mc.ZoomStep(-1.5f);
            if (GUI.Button(new Rect(x + 76, y, 34, 30), "◄", button)) mc.Rotate(-45f);
            if (GUI.Button(new Rect(x + 114, y, 34, 30), "►", button)) mc.Rotate(45f);
            if (GUI.Button(new Rect(x + 152, y, 60, 30), "Tümü", smallButton)) mc.ResetView();
            if (GUI.Button(new Rect(x + 216, y, 66, 30), legendOpen ? "Lejant ✓" : "Lejant", smallButton)) legendOpen = !legendOpen;
            if (GUI.Button(new Rect(x + 286, y, 88, 30), mc.FlatMode ? "3D görünüm" : "Düz harita", smallButton)) mc.ToggleFlatMode();
            var r = new Rect(x + 380, y + 3, 470, 24);
            UITheme.Box(r, new Color(0.05f, 0.055f, 0.07f, 0.75f), 12f);
            GUI.Label(new Rect(r.x + 6, r.y + 2, r.width - 8, r.height),
                "Tekerlek: yakınlaş · sol sürükle: kaydır · sağ sürükle / Q-E: döndür · N: kuzey · M: düz harita · 0: tüm harita", small);
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
            worldLabel.normal.textColor = color;
            GUI.Label(r, text, worldLabel);
        }

        // ---------------------------------------------------------------- üst bar
        void DrawTopBar(float sw)
        {
            var clock = GameClock.I;
            var eco = EconomyManager.I;
            var weather = WeatherSystem.I;
            var prim = CompanyProfile.Primary;
            var sec = CompanyProfile.Secondary;

            // Zemin: şirket rengi, altında ince gölge
            Fill(new Rect(0, 0, sw, TopBar), prim);
            Fill(new Rect(0, TopBar - 3, sw, 3), Color.Lerp(prim, Color.black, 0.25f));
            Fill(new Rect(0, TopBar, sw, 4), new Color(0f, 0f, 0f, 0.18f));

            // Logo: yuvarlak kare içinde baş harf + şirket adı
            var logo = new Rect(12, 8, 32, 32);
            UITheme.Box(logo, sec, 9f);
            barText.normal.textColor = prim;
            barText.fontSize = 17;
            GUI.Label(logo, CompanyProfile.Initial, barText);
            barText.fontSize = 12;
            barTitle.normal.textColor = sec;
            GUI.Label(new Rect(52, 0, 220, TopBar), CompanyProfile.Name, barTitle);

            // Bilgi çipleri
            var chips = new List<(string text, Color bg)>();
            Color chip = UITheme.WithAlpha(Color.black, 0.16f);
            chips.Add(($"Kasa  {Ft(eco.money)}", chip));
            chips.Add(($"Serbest  {Ft(eco.FreeCash)}", eco.FreeCash < 0 ? UITheme.WithAlpha(UITheme.Bad, 0.85f) : chip));
            chips.Add(($"★ {CompanyProfile.Reputation:0.0}", chip));
            chips.Add(($"● {CourierManager.I.OnlineCount}/{CourierManager.I.Couriers.Count} çevrimiçi", chip));
            chips.Add((clock.TimeText, chip));
            chips.Add(($"{clock.SeasonName} · {weather.Name}", chip));
            var boost = CampaignManager.I.ActiveWindow;
            float wBoost = CampaignManager.I.WeatherBoostPercent;
            if (boost != null && boost.percent >= wBoost) chips.Add(($"BOOST %{boost.percent * 100f:0} · {boost.TimeText}", UITheme.WithAlpha(UITheme.Warn, 0.9f)));
            else if (wBoost > 0f) chips.Add(($"HAVA BOOSTU %{wBoost * 100f:0}", UITheme.WithAlpha(UITheme.Warn, 0.9f)));
            if (eco.taxDebt > 0) chips.Add(($"VERGİ BORCU {Ft(eco.taxDebt)}", UITheme.WithAlpha(UITheme.Bad, 0.9f)));
            if (FamilyLoan.I != null && !FamilyLoan.I.Finished) chips.Add((FamilyLoan.I.TopBarText, chip));

            float speedW = 9 * 44f + 16f;
            float x = 280f, maxX = sw - speedW - 16f;
            barText.normal.textColor = sec;
            foreach (var (t, bg) in chips)
            {
                float w = barText.CalcSize(new GUIContent(t)).x + 22f;
                if (x + w > maxX) break;
                var r = new Rect(x, 11, w, 26);
                UITheme.Box(r, bg, 13f);
                GUI.Label(r, t, barText);
                x += w + 6f;
            }

            // Hız seçici: tek hap içinde, seçili olan açık renkte
            float bx = sw - speedW - 8f;
            var group = new Rect(bx, 7, speedW, 34);
            UITheme.Box(group, UITheme.WithAlpha(Color.black, 0.2f), 17f);
            float[] speeds = { 0f, 1f, 2f, 4f, 8f, 16f, 32f, 64f, 128f };
            for (int i = 0; i < speeds.Length; i++)
            {
                float s = speeds[i];
                var r = new Rect(bx + 8 + i * 44f, 11, 40, 26);
                bool on = s == 0f ? clock.Paused : IsSpeed(clock, s);
                if (on) UITheme.Box(r, sec, 13f);
                barText.normal.textColor = on ? prim : sec;
                GUI.Label(r, s == 0f ? "II" : s + "x", barText);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    if (s == 0f) clock.Paused = !clock.Paused;
                    else SetSpeed(s);
                }
            }
        }

        static bool IsSpeed(GameClock c, float s) => !c.Paused && Mathf.Approximately(c.SpeedMultiplier, s);

        // ---------------------------------------------------------------- sol panel
        void DrawPanel(float sh)
        {
            var r = new Rect(0, TopBar, panelWidth, sh - TopBar);
            Fill(r, UITheme.Bg);
            Fill(new Rect(r.xMax - 1, r.y, 1, r.height), UITheme.Line);

            var names = (string[])BaseTabNames.Clone();
            int apps = CourierManager.I.Applications.Count;
            if (apps > 0) names[(int)Tab.IseAlim] = $"İşe Alım ({apps})";

            GUILayout.BeginArea(new Rect(r.x + 8, r.y + 8, r.width - 16, r.height - 16));
            int newTab = GUILayout.SelectionGrid((int)tab, names, 5, smallButton, GUILayout.Height(58));
            if (newTab != (int)tab) { var t = (Tab)newTab; deferred = () => { tab = t; scroll = Vector2.zero; }; }
            GUILayout.Space(6);
            scroll = GUILayout.BeginScrollView(scroll);

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
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawOrders()
        {
            var om = OrderManager.I;
            var eco = EconomyManager.I;
            float now = GameClock.I.TotalMinutes;

            om.autoAssign = GUILayout.Toggle(om.autoAssign, " Otomatik atama (dispatcher)", toggle);
            if (UpgradeManager.I.SmartDispatch)
                om.prioritizeProfit = GUILayout.Toggle(om.prioritizeProfit, " Atamada kârı öncele (kapalıysa en hızlı kurye)", toggle);
            else
                GUILayout.Label("Basit dispatcher: en yakın kuryeye teklif eder. Akıllı atama için Geliştirmeler'e bak.", small);
            if (om.IsBusy)
                GUILayout.Label($"YOĞUNLUK MODU: bekleyen sipariş boştaki kuryeleri aşıyor. Uygulama müşterilere uzun teslimat uyarısı gösteriyor, " +
                                "bir kısmı sipariş vermiyor (itibar düşmez ama sipariş kaybolur). Çözüm: daha çok kurye veya boost.", warn);
            GUILayout.Label($"Bugün: yoğunluk yüzünden sipariş vermeyen {om.busyLostToday} · vazgeçen müşteri (ücret pahalı) {om.lostToday} · iptal {om.cancelledToday} (kuryeler reddetti {om.cancelledRejectedToday}) · " +
                            $"kuryelerin reddettiği {om.rejectedToday} · taşıyacak kurye olmadığı için alınamayan {om.heavyLostToday} · toplu teslimat {om.batchedToday}", small);
            GUILayout.Label($"Ağırlık: 20 kg'a kadar bisiklet, scooter, motor ve araba; {GameData.HeavyKg:0} kg ve üstü sadece araba.", small);

            GUILayout.Space(6);
            GUILayout.Label($"Bekleyen siparişler ({om.Pending.Count})", header);
            int shown = 0;
            foreach (var o in om.Pending)
            {
                if (shown++ >= 12) break;
                GUILayout.BeginVertical(box);
                GUILayout.Label($"#{o.id} {o.shop.Name} · {GameData.KmText(o.distance)} · {GameData.KgText(o.weightKg)} · {o.CustomerTypeName}", label);
                GUILayout.Label($"{o.customerName} — {o.AddressText}", small);
                GUILayout.Label($"{o.BuildingText}", o.UsesStairs && o.floor >= 4 ? warn : small);
                GUILayout.Label($"Sipariş ({o.ItemCount} ürün): {o.ItemsText}", small);
                if (!string.IsNullOrEmpty(o.customerNote)) GUILayout.Label($"Not: \"{o.customerNote}\"", note);
                if (o.IsHeavy) GUILayout.Label("AĞIR SİPARİŞ — sadece arabalı kuryeler alabilir", warn);
                if (o.priceDeclines > 0)
                    GUILayout.Label($"{o.priceDeclines} kurye kazancı beğenmeyip reddetti. Bonus çarpanı veya boost kabul edilmesini kolaylaştırır.", warn);
                if (o.shopQueue) GUILayout.Label("Önceki kurye dükkandaki sıra yüzünden bıraktı.", small);
                if (o.cashPay)
                    GUILayout.Label($"NAKİT: müşteri {Ft(o.cashGiven)} verecek, tutar {Ft(o.CashAmount)}, para üstü {Ft(o.CashChange)} hazırlanmalı. " +
                                    "Kurye nakdi şirkete borçlanır.", warn);
                GUILayout.Label($"Sepet {Ft(o.basket)} · Komisyon {Ft(o.commissionIncome)} + Teslimat ücreti {Ft(o.deliveryFee)} " +
                                $"= Gelir {Ft(o.CompanyIncome)} · İptale {Mathf.Max(0f, o.createdAt + om.pendingTimeout - now):0} dk", small);
                GUILayout.Label($"Teslimat ücreti: taban {Ft(o.feeBase)} + {GameData.KmText(o.distance)} × {Ft(eco.feePerKm)} = {Ft(o.feeKm)}" +
                                (o.feeWeather > 0 ? $" + kötü hava {Ft(o.feeWeather)}" : "") +
                                (o.feeExtra > 0 ? $" + ek {Ft(o.feeExtra)} (kurye ücretini karşılamak için)" : ""),
                                o.feeExtra > 0 ? warn : small);

                GUILayout.BeginHorizontal();
                int count = 0;
                foreach (var c in CourierManager.I.Couriers)
                {
                    if (!c.CanTakeMore || !c.CanCarry(o)) continue;
                    if (o.cashPay && !c.CanTakeCash(o.CashAmount)) continue;
                    bool rejected = o.rejectedBy.Contains(c);
                    int p = eco.ProfitIf(o, c);
                    int need = eco.RequiredAssignProfit(o);
                    GUI.enabled = modalFree && p >= need && !rejected;
                    var order = o;
                    var courier = c;
                    int boostCost = CampaignManager.I.BoostAmount(eco.CourierPay(c, o.distance));
                    string boostTxt = boostCost > 0 ? $" (boost −{Ft(boostCost)})" : "";
                    int courierGets = eco.CourierPay(c, o.distance) + boostCost;
                    string mood = c.LikesOffer(o, courierGets) ? "" : " · beğenmeyebilir";
                    string txt = rejected ? $"{c.ShortName}: reddetti"
                               : p >= need ? $"{c.ShortName} ({c.V.letter}) kurye ~{Ft(courierGets)} · kâr +{Ft(p)}{boostTxt}{mood}"
                               : p >= 0 ? $"{c.ShortName}: kâr az ({Ft(p)} < {Ft(need)})"
                               : $"{c.ShortName}: zarar {Ft(p)}";
                    if (GUILayout.Button(txt, smallButton)) deferred = () => om.Assign(order, courier, true);
                    GUI.enabled = modalFree;
                    if (++count % 2 == 0)
                    {
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                    }
                }
                if (count == 0) GUILayout.Label(o.IsHeavy ? "Müsait arabalı kurye yok." : "Bu ağırlığı taşıyabilecek müsait kurye yok.", small);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            GUILayout.Space(6);
            GUILayout.Label($"Yolda ({om.Active.Count})", header);
            foreach (var c in CourierManager.I.Couriers)
            {
                if (c.OrderCount == 0) continue;
                string ids = c.CurrentOrder != null ? "#" + c.CurrentOrder.id : "";
                foreach (var q in c.QueuedOrders) ids += " #" + q.id;
                GUILayout.Label($"{c.ShortName} ({c.V.letter}) {c.OrderCount}/{c.EffectiveCapacity} paket (çantada {c.CarryingCount}): {c.StateName} — {ids}", small);
            }

            GUILayout.Space(6);
            GUILayout.Label("Son teslimatlar", header);
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
                GUILayout.Label($"#{o.id} {o.shop.Name} → {o.courier.ShortName} · {GameData.KmText(o.distance)} · " +
                                $"{stars} · kurye {Ft(o.courierPay)} · kâr {Ft(o.Profit)}{tip}{issue}",
                                o.keptByCourier ? warn : small);
            }
        }

        void DrawCouriers()
        {
            var cm = CourierManager.I;
            GUILayout.Label($"Kuryeler ({cm.Couriers.Count}/{UpgradeManager.I.CourierCap} hesap) · Şu an çevrimiçi: {cm.OnlineCount}", header);
            GUILayout.Label("Kuryeler serbest çalışır: asıl mesleklerine (öğrenci, ofis çalışanı, vardiyalı işçi...), yorgunluklarına ve " +
                            "günlük hayatlarına göre istedikleri zaman çevrimiçi/çevrimdışı olurlar. Onları zorlayamazsın; boostlar ve iyi moral " +
                            "çevrimiçi olma isteğini artırır. Kendi araçlarını ve sigortalarını kendi kazançlarından öderler; " +
                            "para biriktirince daha iyi araç alırlar.", small);

            foreach (var c in cm.Couriers)
            {
                GUILayout.BeginVertical(box);
                GUILayout.Label($"{c.courierName} — {c.V.name} · Tecrübe {c.level}", header);
                GUILayout.Label($"{c.StateName} · Teslimat {c.deliveries} (bugün {c.deliveriesToday}) · Kazancı {Ft(c.totalEarned)}", small);
                if (c.tipsTotal > 0)
                    GUILayout.Label($"Bahşişler: toplam {Ft(c.tipsTotal)}" + (c.tipsToday > 0 ? $" (bugün {Ft(c.tipsToday)})" : "") + " — tamamı kuryenin", small);
                GUILayout.Label($"Meslek: {c.OccupationName}", small);
                if (c.occupation == Occupation.Ogrenci)
                    GUILayout.Label($"Bu dönem ders programı: {Courier.ScheduleText(c.scheduleSeed, GameClock.I)}", small);
                GUILayout.Label($"Bugün: {c.OccupationStatus}" + (c.dayOff ? " · İzin günü yapıyor" : ""), small);
                Bar("Moral", c.morale, Color.Lerp(new Color(0.9f, 0.3f, 0.3f), new Color(0.3f, 0.85f, 0.4f), c.morale));
                Bar("Yorgunluk", c.fatigue, new Color(1f, 0.6f, 0.2f));

                string capNote = "";
                if (c.morale >= 0.8f) capNote += ", moral yüksek +1";
                if (c.morale < 0.35f) capNote += ", moral düşük −1";
                if (c.fatigue >= 0.6f) capNote += ", yorgun −1";
                GUILayout.Label($"Aynı anda taşıyabileceği: {c.EffectiveCapacity} paket (araç {c.V.capacity}{capNote})", label);
                if (c.RejectChance > 0f)
                    GUILayout.Label($"Siparişlerin yaklaşık %{c.RejectChance * 100f:0}'ini reddedebilir.", warn);

                GUILayout.Label($"Hız {c.speedSkill:0.00} · Dikkat {c.care:0.00} · Nezaket {c.politeness:0.00} · Dayanıklılık {c.stamina:0.00}", small);
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
                if (c.hasCompanyKit)
                    GUILayout.Label($"Şirket ekipmanı: {GameData.StarterKitText} · " +
                                    (c.depositPaid ? $"depozito alındı ({Ft(GameData.DepositAmount)}, ayrılırken iade edilir)"
                                                   : $"depozito {Ft(GameData.DepositThreshold)} kazanınca kesilecek (şu an {Ft(c.totalEarned)})"), small);
                GUILayout.Label(c.insured ? $"Sigorta: var ({(c.insuredViaCompany ? EconomyManager.I.Plan.name : "dışarıdan")}, kendi öder, haftalık {Ft(c.CurrentPremium)}; bugüne kadar {Ft(c.insurancePaid)})"
                                          : "Sigorta: yok (kazada masrafları kendi öder, daha uzun süre çalışamaz)",
                                c.insured ? small : warn);
                var camp = CampaignManager.I;
                if (camp.weeklyChallenge)
                {
                    if (c.weeklyRewardClaimed)
                        GUILayout.Label($"Haftalık hedef tamamlandı! ({Ft(camp.weeklyReward)} ödendi)", label);
                    else
                        Bar("Haftalık", c.weekDeliveries / (float)Mathf.Max(1, camp.weeklyTarget), new Color(0.4f, 0.7f, 1f),
                            $"{c.weekDeliveries}/{camp.weeklyTarget}");
                }
                if (camp.monthlyChallenge)
                {
                    if (c.monthlyRewardClaimed)
                        GUILayout.Label($"Aylık hedef tamamlandı! ({Ft(camp.monthlyReward)} ödendi)", label);
                    else
                        Bar("Aylık", c.monthDeliveries / (float)Mathf.Max(1, camp.monthlyTarget), new Color(0.75f, 0.55f, 1f),
                            $"{c.monthDeliveries}/{camp.monthlyTarget}");
                }
                GUILayout.Label($"Ekipman: {EquipmentText(c)}", small);

                // Çanta hijyeni
                Color hc = c.hygiene >= 0.8f ? new Color(0.3f, 0.85f, 0.4f)
                         : c.hygiene >= Courier.HygieneLimit ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.95f, 0.35f, 0.3f);
                Bar("Hijyen", c.hygiene, hc);
                GUILayout.Label($"Teklif seçiciliği: {c.PickinessText} · Dükkanda sıra yüzünden iptal: bu ay {c.pickupCancelsThisMonth}/{Courier.MaxPickupCancels}",
                                c.pickupCancelsThisMonth >= Courier.MaxPickupCancels ? warn : small);
                GUILayout.Label($"Çanta: {c.HygieneText} · Titizlik {c.tidiness:0.00} · Çanta kullanımı {c.bagUses}/{Courier.BagWornUses}" +
                                (c.cleanRequested ? " · Uyarıldı, paketi bitince temizleyecek" : ""),
                                c.hygiene < Courier.HygieneLimit ? warn : small);
                GUILayout.BeginHorizontal();
                var hcour = c;
                GUI.enabled = modalFree && !c.cleanRequested && c.hygiene < c.MaxHygiene - 0.05f;
                if (GUILayout.Button("Temizlik için uyar", smallButton)) deferred = () => cm.RequestCleaning(hcour);
                GUI.enabled = modalFree && c.BagWorn;
                if (GUILayout.Button($"Yeni çanta (−{Ft(GameData.Equip(EquipmentKind.TermalCanta).price)})", smallButton)) deferred = () => cm.ReplaceBag(hcour);
                GUI.enabled = modalFree;
                GUILayout.EndHorizontal();
                foreach (var p in c.ActivePenalties()) GUILayout.Label("• " + p, warn);

                GUILayout.BeginHorizontal();
                var courier = c;
                if (GUILayout.Button("Ekipman teklif et", smallButton)) deferred = () => { selectedCourier = courier; tab = Tab.Ekipman; scroll = Vector2.zero; };
                if (GUILayout.Button("Nakit / ödeme detayı", smallButton)) deferred = () => { popupCourier = courier; popupScroll = Vector2.zero; };
                GUI.enabled = modalFree && c.OrderCount == 0;
                if (GUILayout.Button(c.OrderCount == 0 ? "İşten çıkar" : "İşten çıkar (paketi var)", smallButton)) deferred = () => cm.Fire(courier);
                GUI.enabled = modalFree;
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
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
            GUILayout.Label($"Kurye hesapları: {cm.Couriers.Count}/{up.CourierCap} ({up.PlatformName})",
                up.CourierCapFull ? warn : label);
            if (up.CourierCapFull)
                GUILayout.Label("Platform paketindeki kurye hesapları dolu. Yeni kurye için Geliştirmeler'den paketi yükselt ya da çalışmayan bir kuryeyle yollarını ayır.", warn);
            GUILayout.Label($"Gelen başvurular ({cm.Applications.Count}/{cm.maxApplications})", header);
            GUILayout.Label("Başvurular zamanla gelir; itibarın ve bonus çarpanın yüksekse daha sık gelir. Her başvuru 1 gün geçerlidir. " +
                            "Kuryeler paket ve km başı kazandığı için işe alım ve çıkarma ücretsizdir; her yeni kuryeye başlangıç kiti verilir. " +
                            "Ama çok kurye = kişi başı az paket = düşük moral.", small);

            if (cm.Applications.Count == 0)
                GUILayout.Label("Şu an bekleyen başvuru yok. Yeni başvurular haber akışında duyurulur.", label);

            foreach (var cand in cm.Applications)
            {
                var v = GameData.Vehicle(cand.vehicle);
                GUILayout.BeginVertical(box);
                GUILayout.Label($"{cand.name} — {v.name} · Tecrübe {cand.level}", header);
                GUILayout.Label($"\"{cand.note}\"", note);
                GUILayout.Label($"Meslek: {Courier.OccupationLabel(cand.occupation)}", small);
                if (cand.occupation == Occupation.Ogrenci)
                    GUILayout.Label($"Bu dönem ders programı: {Courier.ScheduleText(cand.scheduleSeed, GameClock.I)}", small);
                GUILayout.Label(cand.pickiness >= 0.75f ? "Teklif konusunda çok seçici" : cand.pickiness >= 0.45f ? "Teklif konusunda normal" : "Teklif konusunda pek seçici değil", small);
                GUILayout.Label((cand.alsoOwnsBike ? "Yedekte bisikleti de var · " : "") +
                                (cand.insured ? "Kendi sigortası var" : "Sigortası yok"), small);
                GUILayout.Label($"Hız {cand.speedSkill:0.00} · Dikkat {cand.care:0.00} · Nezaket {cand.politeness:0.00} · Dayanıklılık {cand.stamina:0.00}", small);
                GUILayout.Label($"Araç: {v.tilesPerMinute * 60f * GameData.KmPerTile:0} km/saat · {v.capacity} paket · " +
                                $"Kazanç {Ft(v.basePay)}/paket + {Ft(v.payPerKm)}/km · Taşır: {GameData.CarryText(v)}", small);
                GUILayout.Label($"Başvuru {Mathf.Max(0f, (cand.expiresAt - now) / 60f):0} saat sonra geçersiz olacak.", small);

                GUILayout.BeginHorizontal();
                var c = cand;
                GUI.enabled = modalFree && !up.CourierCapFull;
                if (GUILayout.Button(up.CourierCapFull ? "İşe al (hesaplar dolu)" : "İşe al", button)) deferred = () => cm.Hire(c);
                GUI.enabled = modalFree;
                if (GUILayout.Button("Reddet", button)) deferred = () => cm.Reject(c);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            // Destek personeli de buradan işe alınabilir
            GUILayout.Space(10);
            DrawSupportCandidates();
        }

        void DrawShops()
        {
            var map = CityMap.I;
            GUILayout.Label($"Açık bölge: {map.UnlockLevel}/{map.MaxLevel} — {CityMap.DistrictNames[map.UnlockLevel]}", header);
            if (map.UnlockLevel < map.MaxLevel)
            {
                int next = map.UnlockLevel + 1;
                if (GUILayout.Button($"Yeni bölge aç: {CityMap.DistrictNames[next]} — {Ft(CityMap.UnlockCost[next])}", button))
                    deferred = () => map.TryUnlockNext();
            }
            GUILayout.Space(6);

            // Rakip platform
            var rv = RivalCompany.I;
            if (rv != null)
            {
                GUILayout.BeginVertical(box);
                GUILayout.Label($"Rakip: {rv.companyName} (yapay zekâ)", header);
                GUILayout.Label($"İtibar {rv.reputation:0.0}/5 · {rv.couriers} kurye · {rv.RivalShopCount} dükkanla çalışıyor · " +
                                $"geçen hafta ~{rv.deliveriesLastWeek} teslimat · pazar payın ~%{rv.MarketShare * 100f:0}", small);
                if (rv.priceWarDays > 0) GUILayout.Label($"Fiyat savaşı: {rv.companyName} {rv.priceWarDays} gün daha ücret kırıyor, müşteriler fiyata duyarlı.", warn);
                GUILayout.Label("Rakip de çalıştığı dükkanlarda siparişler itibar ve kurye sayısına göre paylaşılır. Münhasır dükkanlarda rakip yok. " +
                                "Sözleşmeler 6 ayda bir yenilenir; memnuniyet düşükse rakip araya girer. Moralsiz kuryelerini de ayartabilir.", small);
                GUILayout.EndVertical();
            }

            foreach (var s in map.Shops)
            {
                GUILayout.BeginVertical(box);
                GUILayout.Label(s.Name + (s.exclusive ? "  [Münhasır]" : s.contracted ? "  [Sözleşmeli]" : "") +
                                (s.rivalContracted ? $"  · {(rv != null ? rv.companyName : "rakip")} ile de çalışıyor" : ""), header);
                GUILayout.Label($"Komisyon %{s.Commission * 100f:0.#} · Hazırlık ~{s.def.prepMinutes:0} dk · " +
                                $"Sepet {Ft(s.def.basketMin)}–{Ft(s.def.basketMax)} · Ağırlık {GameData.KgText(s.def.kgMin)}–{GameData.KgText(s.def.kgMax)} · Toplam sipariş {s.ordersTotal}", small);
                if (s.def.kgMax >= GameData.HeavyKg)
                    GUILayout.Label($"{GameData.HeavyKg:0} kg üstü ağır siparişler gelir: bunlar için arabalı kurye gerekir.", warn);
                if (!map.IsUnlocked(s.tile.x))
                {
                    GUILayout.Label($"Kilitli: önce \"{CityMap.DistrictNames[s.def.unlockLevel]}\" bölgesini aç.", small);
                }
                else if (!s.contracted)
                {
                    var shop = s;
                    if (GameClock.I.Day < s.resignBlockedUntil)
                        GUILayout.Label($"Seninle çalışmayı bıraktı; {GameClock.I.DateOf(s.resignBlockedUntil)} tarihinden sonra yeniden teklif edebilirsin.", warn);
                    else if (GUILayout.Button($"Sözleşme imzala — {Ft(s.def.contractCost)}", button))
                        deferred = () => map.SignContract(shop);
                }
                else if (rv != null)
                {
                    var shop = s;
                    Color sc = Color.Lerp(new Color(0.9f, 0.3f, 0.3f), new Color(0.3f, 0.85f, 0.4f), s.satisfaction / 100f);
                    Bar("Memnuniyet", s.satisfaction / 100f, sc, $"{s.satisfaction:0}/100");
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
                        deferred = () => rv.OfferExclusive(shop);
                    GUI.enabled = modalFree;
                    GUILayout.EndHorizontal();
                    if (GameClock.I.Day < s.negotiateCooldownUntil)
                        GUILayout.Label($"Yeni teklif için {GameClock.I.DateOf(s.negotiateCooldownUntil)} tarihini bekle.", small);
                }
                GUILayout.EndVertical();
            }
        }

        void DrawEquipment()
        {
            var cm = CourierManager.I;
            switch (GameClock.I.Season)
            {
                case Season.Kis: GUILayout.Label("KIŞ: Montsuz kuryeler yavaşlar, hastalanır ve morali her gün düşer.", header); break;
                case Season.Yaz: GUILayout.Label("YAZ: Tişörtsüz kuryeler sıcakta yavaşlar, hızlı yorulur, morali düşer.", header); break;
                case Season.Sonbahar: GUILayout.Label("SONBAHAR: Yağmur sezonu ve kış yaklaşıyor. Yağmurluk ve montları şimdiden al.", header); break;
                default: GUILayout.Label("İLKBAHAR: Yağmur sezonu, yağmurluksuz kuryeler ıslanır ve hastalanabilir.", header); break;
            }
            GUILayout.Label("Ekipman kısa vadede kârı düşürür ama gider olarak vergiden düşülür; hız, moral, puan ve " +
                            "daha az hastalık/kaza olarak geri döner. Kuryeler sekmesinde her kuryenin eksikleri yazar.", small);

            // Teklif ve ödeme şekli
            GUILayout.Space(4);
            GUILayout.Label("Kuryeler serbest çalışır: ekipmanı zorla veremezsin, teklif edersin. Kabul edip etmemesi ödeme şekline, " +
                            "mevsime, ne kadar kalmayı düşündüğüne, hesabındaki paraya ve moraline bağlı. Reddeden kuryeye aynı ekipman " +
                            $"{cm.manualRetryDays:0} gün tekrar sorulamaz.", small);
            GUILayout.Label("Ekipmanı kim öder?", label);
            GUILayout.BeginHorizontal();
            for (int pi = 0; pi < 3; pi++)
            {
                var pay = (EquipmentPayment)pi;
                if (SelectButton(cm.equipmentPayment == pay, CourierManager.PaymentLabel(pay), smallButton)) deferred = () => cm.equipmentPayment = pay;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(cm.equipmentPayment == EquipmentPayment.Sirket ? "Şirket öder: kuryelerin çoğu kabul eder, maliyet tamamen şirketin." :
                            cm.equipmentPayment == EquipmentPayment.YariYariya ? "Yarı yarıya: kuryenin payı hesabından kesilir; kabul edenler azalır." :
                            "Kurye öder: şirkete maliyeti yok ama çoğu reddeder, özellikle mevsimi değilse ve uzun kalmayacaksa.", small);
            GUILayout.Label($"Başlangıç kiti ({GameData.StarterKitText}) her yeni kuryeye verilir; şirkete maliyeti kit başı {Ft(GameData.StarterKitCost)}. " +
                            $"Kuryeden de {Ft(GameData.DepositThreshold)} kazanınca {Ft(GameData.DepositAmount)} depozito kesilir, " +
                            "ayrılırken ekipmanı iade ederse geri ödenir.", label);

            // Hijyen özeti
            int dirty = 0, worn = 0;
            foreach (var hc in cm.Couriers) { if (hc.hygiene < Courier.HygieneLimit) dirty++; if (hc.BagWorn) worn++; }
            GUILayout.Space(4);
            GUILayout.Label($"Hijyen: {dirty} kuryenin çantası kirli (%{Courier.HygieneLimit * 100f:0} altı), {worn} çanta yıpranmış.",
                dirty > 0 ? warn : label);
            GUILayout.Label("Çanta temizliği kuryenin kendi sorumluluğu: her gece zorunlu olarak temizler, titiz olanlar gün içinde de temizler. " +
                            "Kirli çanta müşteri şikayeti ve düşük puan getirir; gün içinde kirlenenleri uyarabilirsin (ücretsiz, moral biraz düşer). " +
                            $"{Courier.BagWornUses} teslimattan sonra çanta yıpranır, yenisi gerekir.", small);
            if (GUILayout.Button("Çantası kirli olanların hepsini uyar", smallButton))
                deferred = () => cm.WarnAllDirty();

            if (selectedCourier == null || !cm.Couriers.Contains(selectedCourier))
                selectedCourier = cm.Couriers.Count > 0 ? cm.Couriers[0] : null;

            GUILayout.Space(4);
            GUILayout.Label("Kurye seç:", label);
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
                long courierPart = CourierManager.CourierCost(kind, cm.equipmentPayment);
                GUILayout.BeginVertical(box);
                GUILayout.Label($"{e.name} — {Ft(e.price)}" +
                                (courierPart > 0 ? $" (şirket {Ft(e.price - courierPart)}, kurye {Ft(courierPart)})" : ""), header);
                GUILayout.Label(e.effect, small);
                GUILayout.BeginHorizontal();
                if (selectedCourier != null)
                {
                    var sc = selectedCourier;
                    if (sc.Has(kind)) GUILayout.Label($"{sc.ShortName}: var", small);
                    else if (cm.OnCooldown(sc, kind)) GUILayout.Label($"{sc.ShortName} reddetti, {cm.CooldownText(sc, kind)} sonra tekrar sorulabilir", small);
                    else
                    {
                        float chance = cm.AcceptChance(sc, kind, cm.equipmentPayment);
                        if (GUILayout.Button($"{sc.ShortName}'a teklif et (kabul ~%{chance * 100f:0})", smallButton))
                            deferred = () => cm.OfferEquipment(sc, kind);
                    }
                }
                if (GUILayout.Button("Eksik olan herkese teklif et", smallButton)) deferred = () => cm.OfferToAll(kind);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
        }

        void DrawSupport()
        {
            var sm = SupportManager.I;
            var clock = GameClock.I;

            GUILayout.Label("Destek ekibi maaşlıdır ve sabit vardiyada, haftada 5 gün çalışır. Vardiyadaki destek eksik ürün/hasarlı " +
                            "sipariş şikayetlerini kendisi pazarlıkla çözer (sana kart gelmez), kurye bulunamayan siparişlerde müşteriyi " +
                            "arayıp ek süre alır ve kuryelere destek olur. Vardiyada kimse yoksa kararlar sana kalır.", small);

            // Bugünün 24 saatlik kapsama çizelgesi
            GUILayout.Space(6);
            int covered = sm.CoveredHours(clock.DayOfWeek);
            var onDuty = sm.OnDutyAgent();
            GUILayout.Label($"Bugün ({clock.DayName}) destek kapsaması: {covered}/24 saat" +
                            (onDuty != null ? $" · Şu an vardiyada: {onDuty.name}" : " · Şu an vardiyada kimse yok"), header);
            DrawCoverageChart(sm, clock);

            GUILayout.Space(6);
            GUILayout.Label($"Aylık maaş toplamı: {Ft(sm.MonthlyPayroll)} · Bu ay şu ana kadar birikmiş maaş: {Ft(sm.AccruedThisMonth)} " +
                            $"(ay sonunda ödenir) · Bu ay çözülen: {sm.handledThisMonth}", label);
            if (sm.AccruedThisMonth > 0 && sm.AccruedThisMonth > EconomyManager.I.FreeCash)
                GUILayout.Label($"DİKKAT: ay sonunda ödenecek maaş ({Ft(sm.AccruedThisMonth)}) serbest kasandan ({Ft(EconomyManager.I.FreeCash)}) fazla. " +
                                "Aynı günlerde kurye ödemeleri de yatacak; iflas riski!", warn);

            GUILayout.Space(6);
            GUILayout.Label($"Destek ekibi ({sm.Agents.Count})", header);
            if (sm.Agents.Count == 0) GUILayout.Label("Henüz destek personelin yok.", small);
            foreach (var a in sm.Agents)
            {
                GUILayout.BeginVertical(box);
                bool duty = a.IsOnDuty(clock);
                GUILayout.Label($"{a.name}{(duty ? "  — VARDİYADA" : "")}", header);
                GUILayout.Label($"{a.ShiftText} · {a.DaysText} · Maaş {Ft(a.monthlySalary)}/ay", small);
                Bar("Yetenek", a.skill, new Color(0.4f, 0.75f, 1f));
                GUILayout.Label($"Çözdüğü: {a.handledTotal} · Pazarlıkla kazandırdığı: {Ft(a.savedTotal)}", small);
                var agent = a;
                if (GUILayout.Button("İşten çıkar (bu ayın maaşı ödenir)", smallButton)) deferred = () => sm.Fire(agent);
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

            GUILayout.Space(10);
            GUILayout.Label("Operasyon sorumlusu", header);
            GUILayout.Label("Maaşlı tek kişi. Her sabah 09:00'da aşağıdaki kurallara göre eksik ekipmanı kuryelere teklif eder " +
                            $"(ödeme şekli Ekipman sekmesindeki seçim: {CourierManager.PaymentLabel(cm.equipmentPayment)}), " +
                            "yıpranan çantaları yeniler ve gün içinde çantası kirlenen kuryeleri uyarır. Kurye teklifi yine reddedebilir; " +
                            $"reddedene {OperationsManager.RetryDays:0} gün sonra tekrar sorar. Yetenekli sorumlu daha ikna edicidir, işleri ertelemez " +
                            "ve toplu alımda %10 indirim alır. Az kuryen varken maaşına değmeyebilir, büyüdükçe seni bu işlerden kurtarır. " +
                            "Ne yaptığı gün sonu raporunda yazar.", small);

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
                if (GUILayout.Button("İşten çıkar (bu ayın maaşı ödenir)", smallButton)) deferred = () => ops.Fire();
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
                    var c = cand;
                    if (GUILayout.Button("İşe al", button)) deferred = () => ops.Hire(c);
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
            GUILayout.Label("Giyecekleri sadece mevsimi gelince ya da yaklaşınca sorar (ör. montu sonbahar ve kışın); arabalı kuryelere giyecek ve kask sormaz.", small);
            ops.replaceWornBags = GUILayout.Toggle(ops.replaceWornBags,
                $" Yıpranan çantayı yenile ({Ft(GameData.Equip(EquipmentKind.TermalCanta).price)}, şirket öder)", toggle);
            ops.warnDirtyBags = GUILayout.Toggle(ops.warnDirtyBags, " Çantası kirlenen kuryeyi uyar (ücretsiz, moral biraz düşer)", toggle);

            GUILayout.Label("Yeni kuryeye ekipman ne zaman sorulsun:", small);
            GUILayout.BeginHorizontal();
            foreach (int md in new[] { 0, 10, 30, 60 })
            {
                int v = md;
                if (SelectButton(ops.minDeliveries == v, v == 0 ? "Hemen" : $"{v} teslimattan sonra", smallButton)) deferred = () => ops.minDeliveries = v;
            }
            GUILayout.EndHorizontal();
            ops.monthlyBudget = MoneySlider("Aylık bütçe", ops.monthlyBudget, 0, 300000, 5000);
            GUILayout.Label($"Bu ay sorumlunun ekipmana harcadığı (şirket payı): {Ft(ops.spentThisMonth)} / {Ft(ops.monthlyBudget)}",
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
            GUILayout.Label("Kalıcı geliştirmeler şirketini büyütür. En önemlisi platform paketi: kuryeler serbest çalışır ama " +
                            "her biri uygulamanda bir hesap ister; paket kaç kurye hesabı olabileceğini belirler ve büyük paketin " +
                            "günlük sunucu ücreti de yüksektir. Sadece gerçekten ihtiyacın olduğunda yükselt. " +
                            "Geliştirmeler gider olarak vergiden düşülür. Aile borcunu unutma!", small);
            GUILayout.Label($"Kasa: {Ft(e.money)} · Kurye: {CourierManager.I.Couriers.Count}/{up.CourierCap} · Günlük platform ücreti: {Ft(up.PlatformFeePerDay)}", label);

            foreach (var d in UpgradeManager.Defs)
            {
                int lv = up.Level(d.kind);
                GUILayout.BeginVertical(box);
                string lvText = d.MaxLevel > 1 ? $" · Seviye {lv}/{d.MaxLevel}" : (lv > 0 ? " · ALINDI" : "");
                GUILayout.Label(d.name + lvText, header);
                GUILayout.Label(d.summary, small);
                GUILayout.Label("Şu an: " + d.LevelText(lv), label);
                int cost = up.NextCost(d.kind);
                if (cost >= 0)
                {
                    GUILayout.Label("Sonraki: " + d.LevelText(lv + 1), small);
                    if (up.IsLocked(d.kind))
                    {
                        GUILayout.Label($"Kilitli: önce \"{CityMap.DistrictNames[d.requiresDistrict]}\" bölgesini aç. ({Ft(cost)})", small);
                    }
                    else
                    {
                        var kind = d.kind;
                        GUI.enabled = modalFree && e.money >= cost;
                        if (GUILayout.Button($"Satın al — {Ft(cost)}", button)) deferred = () => up.Buy(kind);
                        GUI.enabled = modalFree;
                    }
                }
                GUILayout.EndVertical();
            }
        }

        void DrawSupportCandidates()
        {
            var sm = SupportManager.I;
            GUILayout.Space(6);
            GUILayout.Label($"Destek personeli başvuruları ({SupportManager.I.Candidates.Count}) — maaşlı, sabit vardiya", header);
            if (SupportManager.I.Candidates.Count == 0) GUILayout.Label("Şu an destek başvurusu yok; ertesi gün yenisi gelir.", small);
            foreach (var cand in sm.Candidates)
            {
                GUILayout.BeginVertical(box);
                GUILayout.Label($"{cand.name} — Maaş beklentisi {Ft(cand.monthlySalary)}/ay", header);
                long payrollAfter = TotalPayroll + cand.monthlySalary;
                GUILayout.Label($"İşe alırsan aylık toplam maaş: {Ft(payrollAfter)} · serbest kasan: {Ft(EconomyManager.I.FreeCash)}",
                                payrollAfter > EconomyManager.I.FreeCash ? warn : small);
                GUILayout.Label($"\"{cand.note}\"", note);
                Bar("Yetenek", cand.skill, new Color(0.4f, 0.75f, 1f));
                GUILayout.Label("Yetenekli personel şikayetleri daha ucuza kapatır (iade değerinin %30'u ile %60'ı arası).", small);

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
                if (GUILayout.Button($"İşe al ({SupportManager.ShiftLabel(c.chosenShift)}, {(c.chosenDays == WorkDays.PztCum ? "Pzt–Cum" : "Çar–Paz")})", button))
                    deferred = () => sm.Hire(c);
                GUILayout.EndVertical();
            }
        }

        void DrawCampaigns()
        {
            var camp = CampaignManager.I;
            var clock = GameClock.I;
            var eco = EconomyManager.I;

            GUILayout.Label("Kampanyalar şirketin kârından ödenir; müşterinin teslimat ücretine yansımaz. " +
                            "Boost saatinde çevrimdışı kuryeler çevrimiçi olmaya daha istekli olur, kuryeler sipariş reddetmez ve aynı anda +1 paket alır.", small);

            GUILayout.Space(6);
            GUILayout.Label($"Bugün: {clock.DayName}{(clock.IsWeekend ? " (hafta sonu)" : "")}", header);
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
            GUILayout.Label("Kötü hava boostu (otomatik)", header);
            camp.weatherBoost = GUILayout.Toggle(camp.weatherBoost, " Kötü havada otomatik boost", toggle);
            camp.coldBoost = PercentSlider("Soğuk", camp.coldBoost, 0f, 0.60f);
            camp.rainBoost = PercentSlider("Yağmur", camp.rainBoost, 0f, 0.60f);
            camp.snowBoost = PercentSlider("Kar", camp.snowBoost, 0f, 0.60f);
            camp.stormBoost = PercentSlider("Fırtına", camp.stormBoost, 0f, 0.60f);
            var wx = WeatherSystem.I;
            GUILayout.Label($"Şu an: {wx.Name} · sipariş ×{wx.DemandFactor:0.00} · kurye hava bonusu {Ft(wx.WeatherBonus)} · " +
                            $"müşteriye kötü hava ücreti {Ft(wx.CustomerSurcharge)} · hava boostu %{camp.WeatherBoostPercent * 100f:0}", label);
            GUILayout.Label("Kötü havada siparişler artar ama bisiklet, scooter ve motor kuryeleri dışarı çıkmaya daha az isteklidir " +
                            "(yağmurluk ve kış montu bu isteksizliği azaltır, arabalılar etkilenmez). Hava boostu onları çevrimiçi çeker. " +
                            "Saat boostuyla toplanmaz, hangisi yüksekse o geçerli olur. Müşteriden alınan kötü hava ücreti de bu masrafı kısmen karşılar.", small);

            GUILayout.Space(8);
            GUILayout.Label("Hafta sonu boostları", header);
            camp.weekendMorningBoost = GUILayout.Toggle(camp.weekendMorningBoost,
                $" Sabah boostu {camp.weekendMorningStart:00}:00–{camp.weekendMorningEnd:00}:00 (sipariş az olduğu için)", toggle);
            camp.weekendMorningPercent = PercentSlider("Sabah yüzdesi", camp.weekendMorningPercent, 0.05f, 0.60f);
            camp.weekendLunchBoost = GUILayout.Toggle(camp.weekendLunchBoost,
                $" Öğle boostu {camp.weekendLunchStart:00}:00–{camp.weekendLunchEnd:00}:00", toggle);
            camp.weekendLunchPercent = PercentSlider("Öğle yüzdesi", camp.weekendLunchPercent, 0.05f, 0.60f);

            GUILayout.Space(8);
            GUILayout.Label("Hafta içi rastgele boostlar", header);
            camp.weekdayRandomBoosts = GUILayout.Toggle(camp.weekdayRandomBoosts, " Gün içine serpiştirilmiş boostlar", toggle);
            camp.weekdayBoostCount = IntSlider("Günde kaç tane", camp.weekdayBoostCount, 0, 4, 1);
            camp.weekdayMaxPercent = PercentSlider("En yüksek yüzde", camp.weekdayMaxPercent, 0.10f, 0.60f);
            GUILayout.Label("Hafta içi boost saatleri her gün yeniden belirlenir; değişiklikler ertesi gün geçerli olur.", small);

            GUILayout.Space(8);
            GUILayout.Label("Haftalık hedef ödülü", header);
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
            GUILayout.Label("Aylık hedef ödülü", header);
            camp.monthlyChallenge = GUILayout.Toggle(camp.monthlyChallenge, " Aylık hedef açık (her ayın başında sıfırlanır)", toggle);
            camp.monthlyTarget = IntSlider("Hedef paket", camp.monthlyTarget, 50, 1000, 10);
            camp.monthlyReward = MoneySlider("Ödül", camp.monthlyReward, 0, 200000, 1000);
            RewardCheck(avg, camp.monthlyTarget, camp.monthlyReward);

            GUILayout.Space(8);
            GUILayout.Label($"Bu hafta ({clock.MonthName}, {clock.WeekOfMonth}. hafta)", header);
            GUILayout.Label($"Boost ödemeleri: {Ft(camp.boostPaidThisWeek)}", label);
            GUILayout.Label($"Haftalık hedef ödülleri: {Ft(camp.rewardsPaidThisWeek)} ({camp.rewardsClaimedThisWeek} kurye)", label);
            GUILayout.Label($"Bu ay aylık hedef ödülleri: {Ft(camp.rewardsPaidThisMonth)} ({camp.rewardsClaimedThisMonth} kurye)", label);
            GUILayout.Label($"Bugünkü kampanya gideri: {Ft(eco.Today.boosts + eco.Today.rewards)}", small);
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
                if (GUILayout.Button($"{Ft(50000)} erken öde", smallButton)) deferred = () => loan.Pay(50000);
                GUI.enabled = modalFree && e.money >= loan.Remaining;
                if (GUILayout.Button($"Tamamını öde ({Ft(loan.Remaining)})", smallButton)) deferred = () => loan.Pay(loan.Remaining);
                GUI.enabled = modalFree;
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("Borç ödemesi gider değildir, vergiden düşülmez. Erken ödemek taksiti kapatır; ama kasada sürpriz vergiler için yedek bırak.", small);
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
                if (ms.IsReached(m.id))
                    GUILayout.Label($"• {m.title} — {ms.ReachedDate(m.id)}", label);
                else
                {
                    string p = m.progress != null ? m.progress() : "";
                    GUILayout.Label($"   {m.title}" + (string.IsNullOrEmpty(p) ? "" : $" ({p})"), small);
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
            GUILayout.Label("Kuryeler sigorta yaptırmak zorunda değil. Yaptırmak isteyenler şirketin anlaşmalı sigortacısı üzerinden yaptırır, " +
                            "haftalık primden şirket komisyon alır. Dışarıdan sigortalı kuryeler zamanla anlaşmaya geçebilir.", small);
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

        void DrawFinance()
        {
            DrawFamilyLoan();
            var e = EconomyManager.I;
            if (!string.IsNullOrEmpty(e.LastDailyReport) &&
                GUILayout.Button("Dünün gün sonu raporunu göster", smallButton))
                deferred = () => EventManager.I.Push(new GameEvent(e.LastDailyReportTitle, e.LastDailyReport));
            if (!string.IsNullOrEmpty(e.LastWeeklyReport) &&
                GUILayout.Button("Geçen haftanın raporunu göster", smallButton))
                deferred = () => EventManager.I.Push(new GameEvent(e.LastWeeklyReportTitle, e.LastWeeklyReport));
            var clock = GameClock.I;

            DrawHistoryCharts();
            DrawMilestones();

            DrawInsurancePartner(e);

            GUILayout.Label("Kasa", header);
            GUILayout.Label($"Ana kasa: {Ft(e.money)}", label);

            // Kurye hesapları (Wolt tipi)
            GUILayout.Label("Kurye ödemeleri", header);
            GUILayout.Label("Kurye kazançları (bahşişler dahil) hesaplarında birikir. Ayın 15'i ve son günü gece hesap kesilir, " +
                            "1–2 iş günü içinde kasadan ödenir. Kuryeler istedikleri gün hesaplarının %80'ini erken çekebilir (2–3 iş günü). " +
                            $"Nakit siparişlerde kurye parayı şirkete borçlanır: üzerindeki nakit {Ft(Courier.CashLimit)}'ı geçemez, ay sonuna kadar " +
                            "keep cash (hesabın en fazla %80'iyle mahsup) ya da offload (kartla ödeme) ile kapatılır.", small);
            GUILayout.Label($"Kuryelerin hesaplarında: {Ft(e.TotalCourierBalances)} · Sıradaki kesim: {e.NextCutText}", label);
            GUILayout.Label($"Yatmayı bekleyen ödemeler: {Ft(e.TotalPendingPayouts)}", label);
            foreach (var p in e.Payouts)
                GUILayout.Label($"   {p.courierName}: {Ft(p.amount)}{(p.early ? " (erken)" : "")} → {GameClock.I.DateOf(p.payDay)}", small);
            GUILayout.Label($"Kuryelerdeki nakit (şirkete borçları): {Ft(e.TotalCashHeld)}", label);
            long realCash = e.FreeCash;
            GUILayout.Label($"Kurye borçları düşülünce serbest kasa: {Ft(realCash)} (geliştirme, ekipman, sözleşme gibi harcamalar bu paradan yapılır)",
                            realCash < 0 ? warn : small);
            if (e.bankLoanRemaining > 0)
                GUILayout.Label($"Acil banka kredisi: kalan {Ft(e.bankLoanRemaining)} · her gün {Ft(e.bankLoanDaily)} kesilir", warn);
            GUILayout.Space(6);
            if (e.depositsHeld > 0)
                GUILayout.Label($"Tutulan ekipman depozitoları: {Ft(e.depositsHeld)} (gelir değil; ekipmanı iade eden kuryeye geri ödenir)", small);
            if (e.taxDebt > 0)
            {
                GUILayout.Label($"Vergi borcu: {Ft(e.taxDebt)} — her gün %2 büyür!", warn);
                if (GUILayout.Button($"Vergi borcunu öde ({Ft(e.taxDebt)})", button)) deferred = () => e.PayDebt();
            }

            GUILayout.Space(6);
            GUILayout.Label($"Bu vergi yılı (Yıl {clock.Year}: 1 Ocak – 31 Aralık · şirketin {clock.CompanyDay}. günü)", header);
            Stats(e.Year);
            long est = (long)Mathf.Round(Mathf.Max(0f, e.Year.Profit) * e.taxRate);
            GUILayout.Label($"Tahmini yıl sonu vergisi (%{e.taxRate * 100f:0}): {Ft(est)}", label);

            GUILayout.Space(6);
            GUILayout.Label("Bugün", header);
            Stats(e.Today);
            if (e.Yesterday != null)
            {
                GUILayout.Label("Dün", header);
                Stats(e.Yesterday);
            }
            if (!string.IsNullOrEmpty(e.LastTaxSummary))
            {
                GUILayout.Label("Son vergi dönemi", header);
                GUILayout.Label(e.LastTaxSummary, small);
            }

            GUILayout.Space(6);
            GUILayout.Label("Hesap hareketleri", header);
            for (int i = 0; i < Mathf.Min(25, e.Ledger.Count); i++) GUILayout.Label(e.Ledger[i], small);
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
                if (s.otherIncome > 0) GUILayout.Label($"  + Diğer gelir (kurye kesintileri) {Ft(s.otherIncome)}", small);
            }

            // 3) Sonuç
            if (s.penalties > 0) GUILayout.Label($"Cezalar (vergiden düşülmez): −{Ft(s.penalties)}", warn);
            GUILayout.Label($"Net kâr: {Ft(s.Profit - s.penalties)}", header);
        }

        void DrawSettings()
        {
            var e = EconomyManager.I;

            GUILayout.Label("Gün sonu raporu", header);
            e.showDailyReport = GUILayout.Toggle(e.showDailyReport, " Her gece gün sonu raporunu göster (oyun duraklar)", toggle);
            e.showWeeklyReport = GUILayout.Toggle(e.showWeeklyReport, " Her Pazar gecesi haftalık raporu göster", toggle);
            GUILayout.Space(8);

            GUILayout.Label("Harita", header);
            bool lm = GUILayout.Toggle(ShowLandmarks, " Yer adlarını göster (park, nehir, AVM, bölgeler)", toggle);
            if (lm != ShowLandmarks) ShowLandmarks = lm;
            GUILayout.Space(8);

            GUILayout.Label("3D şehir (yakınlaştırınca)", header);
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

            GUILayout.Label("Kurye karakterleri (3D)", header);
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

            var stats = StatsHistory.I;
            if (stats != null)
            {
                GUILayout.Label("Denge kaydı (CSV)", header);
                stats.writeCsv = GUILayout.Toggle(stats.writeCsv, " Her gün sonunda verileri CSV dosyasına yaz (test ve denge için)", toggle);
                GUILayout.Label("Teslimat, iptal, gelir, gider, kâr, kasa, itibar, kurye ve dükkan sayısı her gece bir satır olarak yazılır. " +
                                "Excel ya da Google Sheets ile açılır (ayraç ';'). Her oyun oturumu yeni bir dosya açar, kayıttan devam edilen " +
                                "oyunun eski günleri de içine yazılır.", small);
                GUILayout.Label(string.IsNullOrEmpty(stats.CsvPath) ? $"Klasör: {StatsHistory.CsvFolder}" : $"Dosya: {stats.CsvPath}", small);
                if (GUILayout.Button("Klasörü aç", smallButton))
                    deferred = () =>
                    {
                        System.IO.Directory.CreateDirectory(StatsHistory.CsvFolder);
                        Application.OpenURL(new System.Uri(StatsHistory.CsvFolder).AbsoluteUri);
                    };
                GUILayout.Space(8);
            }

            GUILayout.Label("Para birimi", header);
            GUILayout.BeginHorizontal();
            if (SelectButton(GameData.DisplayCurrency == Currency.Forint, "Forint (Ft)", smallButton))
                deferred = () => GameData.SetCurrency(Currency.Forint);
            if (SelectButton(GameData.DisplayCurrency == Currency.Euro, "Euro (€)", smallButton))
                deferred = () => GameData.SetCurrency(Currency.Euro);
            GUILayout.EndHorizontal();
            GUILayout.Label($"Sadece gösterim değişir, oyun dengesi aynı kalır (1 € = {GameData.FtPerEuro:0} Ft). " +
                            "Seçim kaydedilir. Daha önce yazılmış bildirim ve hesap kayıtları yazıldıkları birimde kalır.", small);
            GUILayout.Space(8);

            GUILayout.Label("Fiyatlandırma (müşteriden alınan teslimat ücreti)", header);
            e.baseFee = MoneySlider("Taban ücret", e.baseFee, 200, 1500, 10);
            e.feePerKm = MoneySlider("Km başı", e.feePerKm, 0, 400, 10);
            e.minProfit = MoneySlider("Min. kâr/sipariş", e.minProfit, 0, 1000, 50);
            e.minAssignProfit = MoneySlider("Atamada min. kâr", e.minAssignProfit, 0, 600, 10);
            GUILayout.Label($"Atamada min. kâr: siparişi götürecek kuryeye ödeme sonrası şirkete en az bu kadar kalmalı; daha azını bırakan kuryeye " +
                            $"atanmaz. {e.relaxAfterMinutes:0} dakikadan uzun bekleyen siparişte şart 0'a iner, böylece sipariş iptal olmaz.", small);
            GUILayout.Label("Kural: Teslimat ücreti = taban + km ücreti × km. Bu, siparişi büyük ihtimalle götürecek kuryenin " +
                            "(o ağırlığı taşıyabilen ve müsait en ucuz kurye) ücreti + min. kâr − komisyonu karşılamıyorsa fark 'ek' olarak eklenir. " +
                            "Zararına iş yine yapılmaz: kâr bırakmayan kuryeye sipariş atanmaz, uygun kurye beklenir. " +
                            "Ücret yükseldikçe vazgeçen müşteri artar.", small);

            GUILayout.Space(8);
            GUILayout.Label("Kurye ücretleri (araca göre)", header);
            foreach (var v in GameData.Vehicles)
                GUILayout.Label($"{v.name}: {Ft(v.basePay)}/paket + {Ft(v.payPerKm)}/km · {GameData.CarryText(v)}", small);

            GUILayout.Space(8);
            GUILayout.Label("Kurye bonusları", header);
            e.bonusMultiplier = FloatSlider("Bonus çarpanı", e.bonusMultiplier, 0.5f, 2f);
            GUILayout.Label($"Şu anki bonus: {Ft(e.CurrentBonus)} (hava {WeatherSystem.I.Name}: {Ft(WeatherSystem.I.WeatherBonus)}, saat: {Ft(e.TimeBonus())}) × çarpan. " +
                            "Yüksek bonus moral ve daha çok başvuru getirir ama kârı düşürür.", small);

            GUILayout.Space(8);
            GUILayout.Label("Vergi", header);
            GUILayout.Label($"Yıl sonu vergisi: %{e.taxRate * 100f:0} — 31 Aralık'ta (gelir − giderler) üzerinden ödenir. Giderler vergiden düşülür.", small);
            GUILayout.Label("Yıl içinde de habersiz vergi ve harçlar çıkar: yerel işletme vergisi, KDV farkı, vergi denetimi, " +
                            "belediye harcı, iş güvenliği fonu. Bazen de vergi iadesi gelir. Kasanda her zaman biraz yedek para tut.", small);

            GUILayout.Space(8);
            GUILayout.Label("Örnek hesap (şu anki ayarlarla)", header);
            const int basket = 5000;
            const int tiles = 15;
            int commission = Mathf.RoundToInt(basket * 0.22f);
            int fee = e.QuoteDeliveryFee(commission, tiles, 2f);
            int pay = e.ExpectedCourierPay(tiles, 2f);
            GUILayout.Label($"Sepet {Ft(basket)}, komisyon %22 = {Ft(commission)}", small);
            GUILayout.Label($"Mesafe {GameData.KmText(tiles)} → teslimat ücreti {Ft(fee)}", small);
            GUILayout.Label($"Şirket geliri = {Ft(commission + fee)}", small);
            GUILayout.Label($"Muhtemel kuryeye ödeme = {Ft(pay)} → kâr {Ft(commission + fee - pay)}", label);
        }

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
                small.normal.textColor = i == 0 ? UITheme.Text : UITheme.TextDim;
                GUI.Label(new Rect(row.x + 12, row.y, row.width - 12, row.height), feed[i], small);
            }
            small.normal.textColor = UITheme.TextDim;
        }

        // ---------------------------------------------------------------- olay kartı
        void DrawEvent(float sw, float sh)
        {
            var ev = EventManager.I.Current;
            if (ev == null) return;

            Fill(new Rect(0, 0, sw, sh), new Color(0.02f, 0.025f, 0.035f, 0.62f));
            float w = 580f;
            float bodyH = label.CalcHeight(new GUIContent(ev.body), w - 56f);
            float h = Mathf.Min(sh - 40f, 118f + bodyH + ev.choices.Count * 46f);
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
            GUILayout.Label(ev.body, label);
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

        // ---------------------------------------------------------------- yardımcılar
        static void Fill(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = c;
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
            if (stylesReady && themeColor == CompanyProfile.Primary) return;
            stylesReady = true;
            themeColor = CompanyProfile.Primary;

            var font = UITheme.UIFont;
            var text = UITheme.Text;

            label = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, richText = true };
            if (font != null) label.font = font;
            label.normal.textColor = text;

            small = new GUIStyle(label) { fontSize = 11 };
            small.normal.textColor = UITheme.TextDim;

            warn = new GUIStyle(small);
            warn.normal.textColor = UITheme.Warn;

            note = new GUIStyle(small) { fontStyle = FontStyle.Italic };
            note.normal.textColor = new Color(0.78f, 0.84f, 0.96f);

            header = new GUIStyle(label) { fontSize = 15, fontStyle = FontStyle.Bold, margin = new RectOffset(4, 4, 6, 3) };
            header.normal.textColor = Color.white;

            title = new GUIStyle(header) { fontSize = 23 };

            // Kart: yuvarlak köşeli, hafif açık zemin
            box = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(12, 12, 9, 9),
                margin = new RectOffset(0, 0, 5, 5),
                border = new RectOffset(10, 10, 10, 10)
            };
            UITheme.SetBg(box.normal, UITheme.Card, 10);

            // Düğmeler: düz, yuvarlak köşeli; seçili (on) hali şirket renginde
            button = MakeButton(13, 9);
            smallButton = MakeButton(11, 8);
            bigButton = MakeButton(18, 12);
            bigButton.fontStyle = FontStyle.Bold;

            toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 13 };
            if (font != null) toggle.font = font;
            toggle.normal.textColor = text;
            toggle.onNormal.textColor = text;
            toggle.hover.textColor = Color.white;
            toggle.onHover.textColor = Color.white;

            textField = new GUIStyle(GUI.skin.textField) { fontSize = 18, padding = new RectOffset(10, 10, 6, 6), border = new RectOffset(8, 8, 8, 8) };
            if (font != null) textField.font = font;
            UITheme.SetBg(textField.normal, new Color(0.06f, 0.065f, 0.08f), 8);
            UITheme.SetBg(textField.focused, new Color(0.05f, 0.055f, 0.07f), 8);
            textField.normal.textColor = text;
            textField.focused.textColor = Color.white;

            worldLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
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
            barText = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
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
            phoneSection = new GUIStyle(label) { fontSize = 10, fontStyle = FontStyle.Bold, margin = new RectOffset(6, 4, 10, 2) };
            phoneSection.normal.textColor = UITheme.TextDim;
            phoneBig = new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Bold };
            phoneBig.normal.textColor = Color.white;
            phoneCard = new GUIStyle(box) { padding = new RectOffset(12, 12, 10, 10), margin = new RectOffset(2, 2, 4, 4), border = new RectOffset(14, 14, 14, 14) };
            UITheme.SetBg(phoneCard.normal, new Color(0.11f, 0.12f, 0.15f), 14);
            accentButton = MakeButton(13, 9);
            accentButton.normal = accentButton.onNormal;
            accentButton.hover = accentButton.onHover;
            accentButton.active = accentButton.onActive;
            accentButton.fontStyle = FontStyle.Bold;
            phoneButton = MakeButton(12, 14);
            phoneButton.fixedHeight = 34;
            phoneButton.fontStyle = FontStyle.Bold;
        }

        GUIStyle phoneTitle, phoneSection, phoneBig, phoneCard, phoneButton, accentButton;

        /// <summary>Yuvarlak köşeli düz düğme stili. Seçili (on) hali şirket renginde.</summary>
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
            st.normal.textColor = st.hover.textColor = st.active.textColor = st.focused.textColor = UITheme.Text;
            st.onNormal.textColor = st.onHover.textColor = st.onActive.textColor = st.onFocused.textColor = UITheme.AccentText;
            return st;
        }

        /// <summary>Şirket rengi değişince (kurulum ekranında) düğme stillerini yeniden üret.</summary>
        public void RefreshTheme() { stylesReady = false; }
    }
}
