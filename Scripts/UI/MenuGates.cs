using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Sadeleştirme için arayüz tarafı:
    ///  • Kilitli menüler: soldaki çubukta soluk görünür, "Kilitli" yazar; üstüne gelince ne zaman açılacağı yazar (bkz. <see cref="Guide"/>).
    ///  • Pencere haritayı kapatmasın: pencere açıkken harita pencerenin sağına kayar (Ayarlar'dan kapatılabilir).
    ///  • Ayarlar'daki "Arayüz" bölümü.
    /// </summary>
    public partial class GameUI
    {
        // ---------------------------------------------------------------- kilitli menüler

        /// <summary>Sekmenin kilidi; null = her zaman açık.</summary>
        static Feature? FeatureOf(Tab t)
        {
            switch (t)
            {
                case Tab.Gelistirmeler: return Feature.Gelisim;
                case Tab.Kampanyalar: return Feature.Kampanya;
                case Tab.Destek: return Feature.Destek;
                case Tab.Market: return Feature.Market;
                case Tab.Istatistik: return Feature.Grafik;
                case Tab.Mesajlar: return Feature.Mesajlar;
                default: return null;
            }
        }

        static bool TabOpen(Tab t)
        {
            var f = FeatureOf(t);
            return f == null || Guide.I == null || Guide.I.IsOpen(f.Value);
        }

        /// <summary>Simge çubuğundan açma: kilitliyse açılmaz, nedeni haber akışına yazılır.</summary>
        void TryOpenTab(Tab t)
        {
            if (!TabOpen(t))
            {
                EventManager.I.Notify($"{BaseTabNames[(int)t]} henüz kilitli: {Guide.LockReason(FeatureOf(t).Value)}");
                return;
            }
            OpenTab(t);
        }

        Tab? dockTip;       // fare üstündeki kilitli simge (ipucu kutusu için)
        Rect dockTipRect;

        static readonly Color LockedIcon = new Color(0.62f, 0.65f, 0.71f, 0.32f);

        /// <summary>Kilitli simgenin yanında küçük açıklama kutusu.</summary>
        void DrawDockTooltip()
        {
            if (dockTip == null || !modalFree) return;
            var f = FeatureOf(dockTip.Value);
            if (f == null) return;
            string text = $"<b>{BaseTabNames[(int)dockTip.Value]}</b> kilitli\n{Guide.LockReason(f.Value)}";
            float w = 250f;
            var content = new GUIContent(text);
            small.normal.textColor = UITheme.Yazi(UITheme.Text);
            float h = small.CalcHeight(content, w - 20f) + 14f;
            var r = new Rect(DockW + 6f, dockTipRect.center.y - h / 2f, w, h);
            UITheme.Shadowed(r, new Color(0.07f, 0.075f, 0.09f, 0.97f), 10f, 4f);
            GUI.Label(new Rect(r.x + 10f, r.y + 7f, w - 20f, h - 14f), content, small);
            small.normal.textColor = UITheme.Yazi(UITheme.TextDim);   // paylaşılan stil: rengi geri al
        }

        /// <summary>Sekmenin bulunduğu menünün sol çubuktaki kutusu (rehber vurgusu için).</summary>
        Rect DockRect(Tab t, float sh) => GroupDockRect(GroupIndexOf(t), sh);

        // ---------------------------------------------------------------- pencere haritayı kapatmasın

        const string PushMapKey = "KuryeTycoon.PencereHaritayiItsin";
        static int pushMapCache = -1;

        /// <summary>Pencere açılınca harita sağa kaysın mı (varsayılan açık). Kapalıysa pencere haritanın üstünde yüzer.</summary>
        static bool PushMapSetting
        {
            get
            {
                if (pushMapCache < 0) pushMapCache = PlayerPrefs.GetInt(PushMapKey, 1);
                return pushMapCache == 1;
            }
            set
            {
                pushMapCache = value ? 1 : 0;
                PlayerPrefs.SetInt(PushMapKey, pushMapCache);
                PlayerPrefs.Save();
            }
        }

        float mapLeft01 = -1f;   // haritanın ekrandaki sol kenarı (0–1); pencere açılıp kapanınca yumuşakça kayar

        /// <summary>Haritanın başladığı x (GUI birimi): simge çubuğunun ya da açık pencerenin sağı.</summary>
        float MapLeftGui => Mathf.Max(DockW, mapLeft01 * Screen.width / Mathf.Max(0.01f, uiScale));

        /// <summary>LateUpdate'te çağrılır: kamera sadece haritaya ayrılan alana çizer.</summary>
        void UpdateMapViewport()
        {
            float dock = DockW * uiScale / Screen.width;
            float target = dock;
            if (panelOpen && PushMapSetting)
                target = (DockW + 10f + panelWidth + 10f) * uiScale / Screen.width;
            target = Mathf.Clamp(target, 0f, 0.6f);   // çok dar ekranda harita fazla küçülmesin
            mapLeft01 = mapLeft01 < 0f ? target : Mathf.MoveTowards(mapLeft01, target, Time.unscaledDeltaTime * 2.2f);
            float top = Mathf.Clamp01(TopBar * uiScale / Screen.height);
            MapCamera.I.SetViewport(new Rect(mapLeft01, 0f, 1f - mapLeft01, 1f - top));
        }

        /// <summary>Harita kaydırılınca simge çubuğu ile harita arasında kalan şerit (pencerenin arkası).</summary>
        void DrawMapGap(float sh)
        {
            float ml = MapLeftGui;
            if (ml <= DockW + 0.5f) return;
            Fill(new Rect(DockW, TopBar, ml - DockW, sh - TopBar), new Color(0.06f, 0.066f, 0.08f, 1f));
            Fill(new Rect(ml - 1f, TopBar, 1f, sh - TopBar), UITheme.Line);
        }

        // ---------------------------------------------------------------- arayüz boyutu

        const string UiSizeKey = "KuryeTycoon.ArayuzBoyutu";
        static readonly string[] UiSizeNames = { "Küçük", "Normal", "Büyük", "Çok büyük" };
        static readonly float[] UiSizeFactors = { 0.9f, 1f, 1.15f, 1.3f };
        static int uiSizeCache = -1;

        /// <summary>Oyuncunun seçtiği arayüz boyutu (0–3, varsayılan Normal). Bu bilgisayarda kalıcı.</summary>
        static int UiSizeSetting
        {
            get
            {
                if (uiSizeCache < 0) uiSizeCache = Mathf.Clamp(PlayerPrefs.GetInt(UiSizeKey, 1), 0, UiSizeFactors.Length - 1);
                return uiSizeCache;
            }
            set
            {
                uiSizeCache = Mathf.Clamp(value, 0, UiSizeFactors.Length - 1);
                PlayerPrefs.SetInt(UiSizeKey, uiSizeCache);
                PlayerPrefs.Save();
            }
        }

        static float UiSizeFactor => UiSizeFactors[UiSizeSetting];

        // ---------------------------------------------------------------- Ayarlar > Harita > Grafik

        void DrawGraphicsSettings()
        {
            Section("Grafik", "Oyun kasıyorsa kaliteyi düşür. Otomatik: oyun kare hızına bakıp kendisi ayarlar. " +
                              "Yüksek: yumuşak gölgeler, ortam gölgesi, ışıma. Düşük: sert gölgeler, daha düşük çözünürlük, gece daha az lamba ışığı.");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Kalite", body, GUILayout.Width(70));
            if (SelectButton(GrafikKalitesi.Ayar == GrafikKalitesi.Otomatik, "Otomatik", smallButton))
                deferred = () => GrafikKalitesi.Ayar = GrafikKalitesi.Otomatik;
            for (int i = 0; i < GrafikKalitesi.Adlar.Length; i++)
            {
                int k = i;
                if (SelectButton(GrafikKalitesi.Ayar == i, GrafikKalitesi.Adlar[i], smallButton)) deferred = () => GrafikKalitesi.Ayar = k;
            }
            GUILayout.EndHorizontal();
            string fps = GrafikOlcer.I != null && GrafikOlcer.I.Fps > 0f ? $" · şu an ~{GrafikOlcer.I.Fps:0} FPS" : "";
            string oto = GrafikKalitesi.Ayar == GrafikKalitesi.Otomatik ? $"Otomatik → {GrafikKalitesi.Adlar[GrafikKalitesi.Seviye]}" : GrafikKalitesi.Adlar[GrafikKalitesi.Seviye];
            GUILayout.Label(Gorsel3D.Active ? $"Geçerli: {oto}{fps}" : $"3D ışık kapalı (eski düz görünüm){fps}", small);
            bool isik = GUILayout.Toggle(Gorsel3D.Enabled, " Gerçek ışık ve gölge (3D). Kapatınca eski düz görünüm; oyunu yeniden başlatınca uygulanır", toggle);
            if (isik != Gorsel3D.Enabled) Gorsel3D.Enabled = isik;
            GUILayout.Space(8);
        }

        // ---------------------------------------------------------------- Ayarlar > Arayüz

        void DrawInterfaceSettings()
        {
            Section("Arayüz",
                $"Kilitli menüler: Gelişim {Guide.GelisimDay}. günde, Kampanya {Guide.KampanyaDay}., Grafik {Guide.GrafikDay}., Destek {Guide.DestekDay}., " +
                $"Market {Guide.MarketDay}. günde (ya da şartı gelince), Yorumlar ilk müşteri yorumuyla açılır. \"Bütün menüleri baştan aç\" hepsini hemen açar.");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Arayüz boyutu", body, GUILayout.Width(130));
            for (int i = 0; i < UiSizeNames.Length; i++)
            {
                int k = i;
                if (SelectButton(UiSizeSetting == i, UiSizeNames[i], smallButton)) deferred = () => UiSizeSetting = k;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
            bool push = GUILayout.Toggle(PushMapSetting, " Pencere açılınca haritayı sağa kaydır (pencere haritayı kapatmaz)", toggle);
            if (push != PushMapSetting) PushMapSetting = push;
            DrawFeedbackSettings();
            bool det = GUILayout.Toggle(ShowAllDetails, " Açıklamaları \"?\" balonu yerine hep açık göster", toggle);
            if (det != ShowAllDetails) ShowAllDetails = det;
            bool all = GUILayout.Toggle(Guide.AllMenusSetting, " Bütün menüleri baştan aç (deneyimli oyuncular için)", toggle);
            if (all != Guide.AllMenusSetting) deferred = () => Guide.AllMenusSetting = all;
            var g = Guide.I;
            if (g != null)
            {
                string txt = g.TutorialDone ? "Başlangıç rehberini yeniden göster" : "Başlangıç rehberini kapat";
                if (GUILayout.Button(txt, smallButton, GUILayout.ExpandWidth(false)))
                    deferred = () => { if (g.TutorialDone) g.RestartTutorial(); else g.FinishTutorial(); };
            }
            GUILayout.Label($"Zorluk: {Difficulty.Name(Difficulty.Level)} (yeni oyun kurarken seçilir)", small);
            GUILayout.Space(8);
        }
    }
}
