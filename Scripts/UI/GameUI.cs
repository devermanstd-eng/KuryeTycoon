using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Prototip arayüzü (Unity IMGUI). Kurulum ekranı, üst bar, sol panel sekmeleri,
    /// haber akışı ve olay kartları. Hiçbir Canvas / prefab gerektirmez.
    /// Arayüzün tamamı UI Toolkit'te (Scripts/UI/Arayuz, Resources/UI/Arayuz), kurulum ekranı dahil (Arayuz/UiKurulum.cs).
    /// OnGUI'de yalnızca haritanın üstüne çizilenler (etiketler, uçan yazılar, seçili bina işareti, lejant, şehir olayı, "?" balonu)
    /// ve klavye/fare girişi kaldı; eski IMGUI arayüzü (F8) 20.11'de silindi. Yeni çizim OnGUI'ye yazılmaz.
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
            // Ekran yüksekliğine göre ölçek × oyuncunun seçtiği arayüz boyutu (Ayarlar > Arayüz)
            uiScale = UiOlcek();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));
            float sw = Screen.width / uiScale;
            float sh = Screen.height / uiScale;
            InitStyles();
            if (Event.current.type == EventType.Repaint) helpTip = null;   // "?" balonu her karede yeniden bulunur

            if (GameBootstrap.I == null) return;
            if (!GameBootstrap.I.Started) return;   // kurulum ekranı UI Toolkit'te (Arayuz/UiKurulum.cs)
            if (!UiAktif)
            {
                // Eski IMGUI arayüzü (F8) kaldırıldı: arayüz kurulamadıysa en azından nedeni görünsün
                if (uiKurulamadi) GUI.Label(new Rect(20f, 20f, Mathf.Max(100f, sw - 40f), 60f), "Arayüz kurulamadı. Ayrıntı Unity Console'da ya da hata raporunda (F9).", warn);
                return;
            }
            if (HandlePhotoMode(sw, sh)) return;   // F10: arayüz gizli, yalnızca şehir

            if (ConfirmOpen) HandleConfirmKey();
            else if (BigCardOpen) HandleBigCardKey();   // büyük kurye kartı: Esc kapatır (Arayuz/UiBuyukKart.cs)
            else if (EventManager.I.Current == null) HandleMenuKey();
            modalFree = EventManager.I.Current == null && !pauseMenuOpen && !ConfirmOpen && !BigCardOpen;
            if (modalFree) HandleKeys();
            // Gelişim ağacı tam ekran: harita fareyi almaz, IMGUI (etiketler, uçan yazılar) ağacın üstüne çizilmez
            if (AgacGorunur) return;
            if (!pauseMenuOpen && !ConfirmOpen && !BigCardOpen) HandleMapMouse(sw, sh);
            // Kamera bakkalın içinde: harita etiketleri (yer, dükkân ve kurye adları), uçan yazılar, işaret ve lejant çizilmez
            if (BakkalCanli.Iceride != null) return;

            // Ortada bir kutu (olay kartı, Esc menüsü, onay, büyük kurye kartı) açıksa IMGUI hiçbir şey çizmez. Otomatik testin ekran
            // görüntülerinde IMGUI, UI Toolkit'in üstünde çıktı (uçan "+€" yazıları ve gece ışıkları olay kartına biniyordu).
            if (OrtaKutuAcik) return;

            // IMGUI'de yalnızca haritanın üstündekiler kaldı (etiketler, uçan yazılar, seçili bina işareti, lejant, şehir olayı,
            // "?" balonu); bütün arayüz UI Toolkit'te. Harita üstü çizimler harita alanına kırpılır: UI Toolkit'in altına taşmasın.
            GUI.enabled = modalFree;
            // Pencere açıkken harita sağa kaymasa da (Ayarlar > pencere haritayı kapatmasın: kapalı) etiketler pencerenin üstüne çıkmasın
            float haritaSol = Mathf.Max(MapLeftGui, panelOpen ? WindowRect(sh).xMax : 0f);
            var haritaAlani = new Rect(haritaSol, TopBar, Mathf.Max(0f, sw - haritaSol), Mathf.Max(0f, sh - TopBar));
            GUI.BeginClip(haritaAlani, -haritaAlani.position, Vector2.zero, false);
            if (!Ciz2D.UitkCiziyor && Event.current.type == EventType.Repaint) Ciz2D.Isi.Oynat();   // harita katmanı: normalde UI Toolkit'te, arayüzün altında
            DrawWorldLabels();
            DrawMapPops();       // uçan "+350 Ft" yazıları
            DrawInfoMarker();    // seçili binanın üstündeki işaret (bina kartı UI Toolkit'te: Arayuz/UiBinaKarti.cs)
            GUI.EndClip();
            DrawLegend(sh);
            DrawCityEventMarker(sw);
            DrawGuideOverlays(sw, sh);
            GUI.enabled = true;
        }

        /// <summary>Yeni arayüzde ortada bir kutu açık: IMGUI (GameUI ve WeatherFx) o sırada çizmez.</summary>
        public static bool OrtaKutuAcik { get; private set; }

        // ---------------------------------------------------------------- ESC menüsü (kaydet / ana menü / çık)
        bool pauseMenuOpen, menuWasPaused, confirmMainMenu;

        void HandleMenuKey()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode != KeyCode.Escape) return;
            e.Use();
            // Önce bina kartı, kurye telefonu ve açık pencere kapanır, sonra Esc menüsü açılır
            if (BakkalCanli.Iceride != null) { BakkalCanli.Iceride.Cik(); return; }   // bakkalın içinden haritaya dön
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
            // Kamera bakkalın içinde: tuşlar bakışı çevirir, oyun kısayolları çalışmaz (Esc: HandleMenuKey)
            if (BakkalCanli.Iceride != null) { BakkalCanli.Iceride.Girdi(e); if (e.type == EventType.KeyDown) e.Use(); return; }
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
                                   Color.Lerp(a.renk, Color.white, 0.4f));

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
                // Önemli etiket (seçili kurye, merkez) de UI Toolkit kutusunun altına girmez: IMGUI kutunun üstüne çizilirdi
                if (!ok && q.onemli && UiAktif && UiHaritaUstuDoluMu(q.r)) q.skip = true;
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
            // Kamera bakkalın içinde: harita kaydırma / tıklama yok, sürükleme etrafa baktırır
            if (BakkalCanli.Iceride != null)
            {
                if (e.type == EventType.MouseDrag && !UiUzerinde(m)) BakkalCanli.Iceride.Girdi(e);
                return;
            }
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
        static void Fill(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = UITheme.Zemin(c);     // kâğıt tema: koyu zeminler kreme döner
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
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
