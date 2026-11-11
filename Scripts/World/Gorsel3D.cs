using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KuryeTycoon
{
    /// <summary>
    /// 3D ışık, gölge ve renk (referans: sıcak güneş, yumuşak gölgeler, canlı ama yumuşak renkler).
    ///  • Proje 2D Renderer ile açıldı; 2D Renderer gerçek güneş ışığı ve gölge çizmez. Oyun açılırken
    ///    Resources/Gorsel/KuryeURP (Universal Renderer, gölgeli) render ayarına geçilir.
    ///    Bu dosya yoksa: Unity'de "Kurye Tycoon > 3D Işık ve Gölge Kurulumu" menüsü bir kez çalıştırılır.
    ///  • Güneş saate göre doğudan doğar, güneyden geçer, batıdan batar (gölgeler gün boyunca döner);
    ///    şafakta pembe, akşamüstü turuncu, gece ay ışığı.
    ///  • Ortam ışığı üç tonlu (gök mavisi, ufuk sıcak, yer yeşil) ve ekran efektleri (ton eşleme, hafif ışıma, sıcak beyaz ayarı).
    ///  • Ayarlar: PlayerPrefs "KuryeTycoon.Isik3D" (1 açık, 0 kapalı; kapalıysa eski düz görünüm).
    /// </summary>
    public class Gorsel3D : MonoBehaviour
    {
        public static Gorsel3D I { get; private set; }

        const string PrefKey = "KuryeTycoon.Isik3D";
        public const string PipelinePath = "Gorsel/KuryeURP";

        /// <summary>Gerçek güneş ışığı ve gölge çalışıyor mu (DayNight ekran tonunu buna göre hafifletir).</summary>
        public static bool Active => I != null && I.pipelineOk;

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(PrefKey, 1) == 1;
            set { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        Light sun;
        Volume volume;
        bool pipelineOk;
        Camera lastCam;

        // Günün renkleri
        static readonly Color SunDay = new Color(1.00f, 0.95f, 0.86f);
        static readonly Color SunDusk = new Color(1.00f, 0.62f, 0.36f);
        static readonly Color SunDawn = new Color(1.00f, 0.72f, 0.70f);
        static readonly Color Moon = new Color(0.55f, 0.65f, 0.95f);

        static readonly Color SkyDay = new Color(0.66f, 0.72f, 0.80f);
        static readonly Color EquatorDay = new Color(0.64f, 0.60f, 0.54f);
        static readonly Color GroundDay = new Color(0.40f, 0.44f, 0.34f);
        static readonly Color SkyNight = new Color(0.12f, 0.15f, 0.26f);
        static readonly Color EquatorNight = new Color(0.10f, 0.11f, 0.18f);
        static readonly Color GroundNight = new Color(0.06f, 0.07f, 0.10f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null || !Enabled) return;
            var go = new GameObject("Görsel (güneş, gölge, renk)");
            DontDestroyOnLoad(go);
            go.AddComponent<Gorsel3D>();
        }

        void Awake()
        {
            I = this;
            pipelineOk = ApplyPipeline();
            if (!pipelineOk)
            {
                Debug.LogWarning("Kurye Tycoon: 3D ışık ayarı bulunamadı (Resources/" + PipelinePath + "). " +
                                 "Unity'de \"Kurye Tycoon > 3D Işık ve Gölge Kurulumu\" menüsünü bir kez çalıştırın.");
                return;
            }
            CreateSun();
            CreateVolume();
            UpdateLighting();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        /// <summary>Gölgeli render ayarına geçer. Zaten o ayardaysa bir şey yapmaz.</summary>
        static bool ApplyPipeline()
        {
            var asset = Resources.Load<RenderPipelineAsset>(PipelinePath);
            if (asset == null) return false;
            if (QualitySettings.renderPipeline != asset) QualitySettings.renderPipeline = asset;
            if (GraphicsSettings.defaultRenderPipeline != asset) GraphicsSettings.defaultRenderPipeline = asset;
            return true;
        }

        void CreateSun()
        {
            // Sahnedeki diğer yönlü ışıkları kapat (ör. karakterler için eklenen yedek güneş): iki güneş, iki gölge olmasın
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.enabled = false;

            var go = new GameObject("Güneş");
            go.transform.SetParent(transform, false);
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;          // gölge tam kara değil: referanstaki gibi yumuşak
            sun.shadowBias = 0.02f;
            sun.shadowNormalBias = 0.25f;
            sun.renderMode = LightRenderMode.ForcePixel;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
        }

        void CreateVolume()
        {
            var go = new GameObject("Renk ayarı");
            go.transform.SetParent(transform, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();

            var tone = p.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.12f);
            ca.contrast.Override(10f);
            ca.saturation.Override(12f);

            var wb = p.Add<WhiteBalance>(true);
            wb.temperature.Override(7f);          // hafif sıcak (öğleden sonra güneşi)

            var bloom = p.Add<Bloom>(true);
            bloom.intensity.Override(0.25f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.6f);

            var vig = p.Add<Vignette>(true);
            vig.intensity.Override(0.16f);
            vig.smoothness.Override(0.5f);

            volume.sharedProfile = p;
        }

        void LateUpdate()
        {
            if (!pipelineOk) return;
            var cam = Camera.main;
            if (cam != null && cam != lastCam)
            {
                lastCam = cam;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.renderPostProcessing = true;
                    data.renderShadows = true;
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                }
            }
            UpdateLighting();
        }

        /// <summary>Güneşin yönü, rengi ve gücü ile ortam ışığı saate göre.</summary>
        void UpdateLighting()
        {
            if (sun == null) return;
            bool cycle = DayNight.Enabled && GameClock.I != null;
            float hour = cycle ? DayNight.HourF : 15.5f;          // döngü kapalıysa hep öğleden sonra
            float rise = DayNight.Sunrise, set = DayNight.Sunset;
            float night = cycle ? DayNight.Night01 : 0f;
            float dusk = cycle ? DayNight.Dusk01 : 0f;
            float dawn = cycle ? DayNight.Dawn01 : 0f;

            // Gün içindeki konum: 0 doğuş (doğu), 0,5 öğle (güney), 1 batış (batı)
            float t = Mathf.Clamp01(Mathf.InverseLerp(rise - 0.4f, set + 0.4f, hour));
            float az = Mathf.PI * t;
            var horiz = new Vector2(Mathf.Cos(az), -Mathf.Sin(az));            // +x doğu, −y güney (kameranın tarafı)
            // Öğlen bile tam tepede değil: gölgeler hep görünsün
            float elev = Mathf.Lerp(14f, 52f, Mathf.Sin(Mathf.PI * t)) * Mathf.Deg2Rad;

            Vector3 toSun;
            if (night > 0.5f)
            {
                // Gece: güneybatıdan alçak ay ışığı
                toSun = new Vector3(-0.45f, -0.6f, -0.65f).normalized;
            }
            else
            {
                // Dünyada "yukarı" −Z
                toSun = new Vector3(horiz.x * Mathf.Cos(elev), horiz.y * Mathf.Cos(elev), -Mathf.Sin(elev)).normalized;
            }
            sun.transform.rotation = Quaternion.LookRotation(-toSun, Vector3.up);

            var day = SunDay;
            if (dusk > 0f) day = Color.Lerp(day, SunDusk, dusk);
            if (dawn > 0f) day = Color.Lerp(day, SunDawn, dawn * 0.8f);
            sun.color = Color.Lerp(day, Moon, Mathf.SmoothStep(0f, 1f, night));
            float dayI = Mathf.Lerp(1.25f, 0.95f, Mathf.Max(dusk, dawn));
            sun.intensity = Mathf.Lerp(dayI, 0.28f, night);
            sun.shadowStrength = Mathf.Lerp(0.72f, 0.45f, night);

            RenderSettings.ambientSkyColor = Color.Lerp(SkyDay, SkyNight, night);
            RenderSettings.ambientEquatorColor = Color.Lerp(Color.Lerp(EquatorDay, new Color(0.75f, 0.55f, 0.45f), dusk * 0.6f), EquatorNight, night);
            RenderSettings.ambientGroundColor = Color.Lerp(GroundDay, GroundNight, night);
        }
    }
}
