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
            small.normal.textColor = UITheme.Text;
            float h = small.CalcHeight(content, w - 20f) + 14f;
            var r = new Rect(DockW + 6f, dockTipRect.center.y - h / 2f, w, h);
            UITheme.Shadowed(r, new Color(0.07f, 0.075f, 0.09f, 0.97f), 10f, 4f);
            GUI.Label(new Rect(r.x + 10f, r.y + 7f, w - 20f, h - 14f), content, small);
            small.normal.textColor = UITheme.TextDim;   // paylaşılan stil: rengi geri al
        }

        /// <summary>Soldaki çubukta bir sekmenin simge kutusu (DrawPanel'deki dizilişle aynı).</summary>
        Rect DockRect(Tab t, float sh)
        {
            if (t == Tab.Ayarlar) return new Rect(7f, sh - 74f, DockW - 14f, 62f);
            float y = TopBar + 10f, step = DockStep(sh);
            foreach (var d in DockItems)
            {
                if (d.tab == t) return new Rect(7f, y, DockW - 14f, step - 4f);
                if (d.tab != Tab.Ayarlar) y += step;
            }
            return Rect.zero;
        }

        /// <summary>Simge çubuğunda iki simge arası (ekran kısaysa simgeler sıkışır; Ayarlar en altta ayrı).</summary>
        static float DockStep(float sh)
        {
            int n = 0;
            foreach (var d in DockItems) if (d.tab != Tab.Ayarlar) n++;
            float avail = sh - TopBar - 10f - 84f;
            return Mathf.Clamp(avail / Mathf.Max(1, n), 44f, 66f);
        }

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

        // ---------------------------------------------------------------- Ayarlar > Arayüz

        void DrawInterfaceSettings()
        {
            Section("Arayüz",
                $"Kilitli menüler: Gelişim {Guide.GelisimDay}. günde, Kampanya {Guide.KampanyaDay}., Grafik {Guide.GrafikDay}., Destek {Guide.DestekDay}., " +
                $"Market {Guide.MarketDay}. günde (ya da şartı gelince), Mesajlar ilk mesajla açılır. \"Bütün menüleri baştan aç\" hepsini hemen açar.");
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
