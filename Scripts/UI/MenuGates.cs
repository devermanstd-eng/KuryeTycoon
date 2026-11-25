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
                case Tab.Analitik: return Feature.Grafik;
                case Tab.Mutfak: return Feature.Market;
                case Tab.ArGe: return Feature.Gelisim;
                case Tab.Bolgeler: return Feature.Gelisim;
                case Tab.Mesajlar: return Feature.Mesajlar;
                default: return null;
            }
        }

        static bool TabOpen(Tab t)
        {
            var f = FeatureOf(t);
            return f == null || Guide.I == null || Guide.I.IsOpen(f.Value);
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
            // Yüzen üst çubuk (16.11 akşam): harita üst çubuğun altına da çizilir; üst çubuk ayrı ayrı yüzen kapsüller, araları harita.
            float top = UiAktif ? 0f : Mathf.Clamp01(TopBar * uiScale / Screen.height);
            MapCamera.I.SetViewport(new Rect(mapLeft01, 0f, 1f - mapLeft01, 1f - top));
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

        // Ayarlar penceresi (arayüz boyutu, grafik kalitesi…) UI Toolkit'te: Arayuz/UiAyarlar.cs
    }
}
