using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KuryeTycoon
{
    /// <summary>
    /// Grafik kalitesi (Ayarlar > Harita): Otomatik · Düşük · Orta · Yüksek.
    ///  • Yüksek: 3D ışık ayarı olduğu gibi (4096 gölge haritası, 4 kademe, yumuşak gölge, MSAA 4x, SSAO, ışıma, gece 72 lamba ışığı).
    ///  • Orta: 2048 gölge, 2 kademe, SSAO ve MSAA yok (FXAA), Retina ekranda %85 çözünürlük, gece 32 lamba ışığı, yağmur %60.
    ///  • Düşük: 1024 sert gölge, tek kademe, kısa gölge mesafesi, ışıma ve HDR yok, %70 çözünürlük, gece 12 lamba ışığı, yağmur %30.
    ///  • Otomatik (varsayılan): Orta'dan başlar; oyun başladıktan sonra kare hızı düşükse bir kademe iner, iyiyse çıkar.
    ///    Sonuç kaydedilir, bir sonraki açılışta oradan başlar.
    /// Render ayarı dosyaları (Resources/Gorsel) diskte değişmez: değerler oyun sırasında bellekte değişir, oyundan
    /// çıkınca (editörde Play durunca) eski hâline döner. Böylece git'te değişmiş dosya görünmez.
    /// </summary>
    public static class GrafikKalitesi
    {
        public const int Dusuk = 0, Orta = 1, Yuksek = 2, Otomatik = -1;
        public static readonly string[] Adlar = { "Düşük", "Orta", "Yüksek" };

        const string AyarKey = "KuryeTycoon.GrafikKalitesi";
        const string OtoKey = "KuryeTycoon.GrafikOtomatikSonuc";
        static int ayarCache = -2, otoCache = -2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ayarCache = -2;
            otoCache = -2;
            orijinalAlindi = false;
            uygulandi = false;
        }

        /// <summary>Oyuncunun seçimi: −1 otomatik, 0–2 sabit.</summary>
        public static int Ayar
        {
            get
            {
                if (ayarCache == -2) ayarCache = Mathf.Clamp(PlayerPrefs.GetInt(AyarKey, Otomatik), Otomatik, Yuksek);
                return ayarCache;
            }
            set
            {
                ayarCache = Mathf.Clamp(value, Otomatik, Yuksek);
                PlayerPrefs.SetInt(AyarKey, ayarCache);
                PlayerPrefs.Save();
                Uygula();
            }
        }

        /// <summary>Otomatik modun bulduğu seviye (kayıtlı).</summary>
        public static int OtoSonuc
        {
            get
            {
                if (otoCache == -2) otoCache = Mathf.Clamp(PlayerPrefs.GetInt(OtoKey, Orta), Dusuk, Yuksek);
                return otoCache;
            }
            set
            {
                otoCache = Mathf.Clamp(value, Dusuk, Yuksek);
                PlayerPrefs.SetInt(OtoKey, otoCache);
                PlayerPrefs.Save();
                if (Ayar == Otomatik) Uygula();
            }
        }

        /// <summary>Şu an geçerli seviye.</summary>
        public static int Seviye => Ayar == Otomatik ? OtoSonuc : Ayar;

        // ---------------------------------------------------------------- diğer sistemlerin okuduğu değerler

        /// <summary>Gece aynı anda gerçek ışık alan sokak lambası sayısı.</summary>
        public static int LambaIsigi => Seviye == Yuksek ? 72 : Seviye == Orta ? 32 : 12;

        /// <summary>Yağmur, kar ve sıçrama yoğunluğu çarpanı.</summary>
        public static float HavaCarpani => Seviye == Yuksek ? 1f : Seviye == Orta ? 0.6f : 0.3f;

        public static AntialiasingMode KenarYumusatma =>
            Seviye == Yuksek ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
          : Seviye == Orta ? AntialiasingMode.FastApproximateAntialiasing
          : AntialiasingMode.None;

        public static bool YumusakGolge => Seviye >= Orta;
        public static bool Isima => Seviye >= Orta;

        // ---------------------------------------------------------------- render ayarı

        const string RendererPath = "Gorsel/KuryeURP_Renderer";

        static bool orijinalAlindi, uygulandi;
        static float oShadowDistance, oRenderScale;
        static int oCascades, oShadowRes, oMsaa;
        static bool oHdr, oSsao;

        static UniversalRenderPipelineAsset Asset =>
            (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;

        static ScriptableRendererFeature Ssao()
        {
            var data = Resources.Load<ScriptableRendererData>(RendererPath);
            if (data == null) return null;
            foreach (var f in data.rendererFeatures)
                if (f != null && f.GetType().Name.Contains("AmbientOcclusion")) return f;
            return null;
        }

        /// <summary>Seçili seviyeyi uygular (render ayarı, güneş gölgesi, ışıma, kamera). Gorsel3D açılışta ve ayar değişince çağırır.</summary>
        public static void Uygula()
        {
            var a = Asset;
            if (a != null && Gorsel3D.Active)
            {
                if (!orijinalAlindi)
                {
                    orijinalAlindi = true;
                    oShadowDistance = a.shadowDistance;
                    oRenderScale = a.renderScale;
                    oCascades = a.shadowCascadeCount;
                    oMsaa = a.msaaSampleCount;
                    oHdr = a.supportsHDR;
                    oShadowRes = Oku(a, "mainLightShadowmapResolution", 4096);
                    var s0 = Ssao();
                    oSsao = s0 != null && s0.isActive;
                    Application.quitting += GeriAl;
                }
                int sv = Seviye;
                bool retina = Screen.dpi > 150f || Screen.height > 1500;
                if (sv == Yuksek)
                {
                    a.shadowDistance = oShadowDistance;
                    a.shadowCascadeCount = oCascades;
                    Yaz(a, "mainLightShadowmapResolution", "m_MainLightShadowmapResolution", oShadowRes);
                    a.msaaSampleCount = oMsaa;
                    a.renderScale = oRenderScale;
                    a.supportsHDR = oHdr;
                }
                else if (sv == Orta)
                {
                    a.shadowDistance = Mathf.Min(oShadowDistance, 110f);
                    a.shadowCascadeCount = Mathf.Min(oCascades, 2);
                    Yaz(a, "mainLightShadowmapResolution", "m_MainLightShadowmapResolution", Mathf.Min(oShadowRes, 2048));
                    a.msaaSampleCount = 1;
                    a.renderScale = retina ? 0.85f : 1f;
                    a.supportsHDR = oHdr;
                }
                else
                {
                    a.shadowDistance = Mathf.Min(oShadowDistance, 70f);
                    a.shadowCascadeCount = 1;
                    Yaz(a, "mainLightShadowmapResolution", "m_MainLightShadowmapResolution", Mathf.Min(oShadowRes, 1024));
                    a.msaaSampleCount = 1;
                    a.renderScale = 0.7f;
                    a.supportsHDR = false;
                }
                var ssao = Ssao();
                if (ssao != null) ssao.SetActive(sv == Yuksek && oSsao);
                uygulandi = true;
            }
            if (Gorsel3D.I != null) Gorsel3D.I.KaliteyeUy();
        }

        /// <summary>Oyundan çıkarken render ayarını diskteki hâline döndür (editörde dosya değişmiş görünmesin).</summary>
        static void GeriAl()
        {
            if (!uygulandi) return;
            var a = Asset;
            if (a != null)
            {
                a.shadowDistance = oShadowDistance;
                a.shadowCascadeCount = oCascades;
                Yaz(a, "mainLightShadowmapResolution", "m_MainLightShadowmapResolution", oShadowRes);
                a.msaaSampleCount = oMsaa;
                a.renderScale = oRenderScale;
                a.supportsHDR = oHdr;
            }
            var ssao = Ssao();
            if (ssao != null) ssao.SetActive(oSsao);
            uygulandi = false;
        }

        // URP sürümüne göre özellik ya da alan adı değişebiliyor: ikisini de dener, bulamazsa sessizce geçer
        static int Oku(object o, string prop, int varsayilan)
        {
            try
            {
                var p = o.GetType().GetProperty(prop, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null) return (int)p.GetValue(o);
            }
            catch { }
            return varsayilan;
        }

        static void Yaz(object o, string prop, string field, int deger)
        {
            try
            {
                var p = o.GetType().GetProperty(prop, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.CanWrite && p.PropertyType == typeof(int)) { p.SetValue(o, deger); return; }
                var f = o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null) f.SetValue(o, f.FieldType.IsEnum ? System.Enum.ToObject(f.FieldType, deger) : (object)deger);
            }
            catch { }
        }
    }

    /// <summary>
    /// Kare hızını ölçer (Ayarlar'da gösterilir) ve Otomatik modda kaliteyi ayarlar: oyun başladıktan sonra
    /// ortalama 30 FPS'in altındaysa bir kademe iner, Orta'da 55'in üstündeyse Yüksek'i dener. En fazla birkaç deneme.
    /// </summary>
    public class GrafikOlcer : MonoBehaviour
    {
        public static GrafikOlcer I { get; private set; }

        /// <summary>Son birkaç saniyenin ortalama kare hızı.</summary>
        public float Fps { get; private set; }

        float toplam, bekle = 6f;
        int kare, deneme;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("Grafik ölçer");
            DontDestroyOnLoad(go);
            I = go.AddComponent<GrafikOlcer>();
        }

        void OnDestroy() { if (I == this) I = null; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            toplam += dt;
            kare++;
            if (toplam < 4f) return;
            Fps = kare / toplam;
            toplam = 0f;
            kare = 0;
            Karar();
        }

        void Karar()
        {
            if (GrafikKalitesi.Ayar != GrafikKalitesi.Otomatik || deneme >= 3) return;
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || !Gorsel3D.Active) return;
            if (bekle > 0f) { bekle -= 4f; return; }      // şehir kurulurken yaşanan takılmalar sayılmasın
            int sv = GrafikKalitesi.OtoSonuc;
            if (Fps < 30f && sv > GrafikKalitesi.Dusuk)
            {
                deneme++;
                GrafikKalitesi.OtoSonuc = sv - 1;
                Bildir($"Oyun kasıyordu (~{Fps:0} FPS): grafik kalitesi {GrafikKalitesi.Adlar[sv - 1]} yapıldı. Ayarlar > Harita'dan değiştirebilirsin.");
                bekle = 4f;
            }
            else if (Fps > 55f && sv == GrafikKalitesi.Orta && deneme == 0)
            {
                deneme++;
                GrafikKalitesi.OtoSonuc = GrafikKalitesi.Yuksek;   // kaldırırsa kalır, kaldırmazsa sonraki ölçümde geri iner
                bekle = 4f;
            }
            else deneme = 3;   // karar verildi
        }

        static void Bildir(string s)
        {
            if (EventManager.I != null) EventManager.I.Notify(s);
        }
    }
}
