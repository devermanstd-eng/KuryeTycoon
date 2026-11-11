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

        // ---------------------------------------------------------------- günün dört bölümüne göre ışık
        // Sabah 06–11 (pembe şafak → serin altın, uzun gölgeler) · Gündüz 11–17 (parlak, nötr) ·
        // Akşam 17–21 (altın → turuncu gün batımı → mor alacakaranlık) · Gece 21–06 (lavanta ay ışığı).
        // Saat anahtarları arasında yumuşak geçiş yapılır.
        struct Anahtar
        {
            public float saat, siddet, golge;
            public Color gunes, gok, ufuk, zemin;
            public Anahtar(float saat, Color gunes, float siddet, Color gok, Color ufuk, Color zemin, float golge)
            { this.saat = saat; this.gunes = gunes; this.siddet = siddet; this.gok = gok; this.ufuk = ufuk; this.zemin = zemin; this.golge = golge; }
        }

        static Color C(float r, float g, float b) => new Color(r, g, b);

        static readonly Color Moon = C(0.62f, 0.60f, 0.90f);   // lavanta ay ışığı (referanstaki mor akşam)
        static readonly Anahtar GeceA = new Anahtar(0f, Moon, 0.35f, C(0.30f, 0.29f, 0.44f), C(0.27f, 0.23f, 0.32f), C(0.14f, 0.13f, 0.17f), 0.45f);

        static readonly Anahtar[] Anahtarlar =
        {
            GeceA,
            new Anahtar(4.9f,  Moon, 0.35f, C(0.30f, 0.29f, 0.44f), C(0.27f, 0.23f, 0.32f), C(0.14f, 0.13f, 0.17f), 0.45f),
            // Sabah: şafakta pembe-turuncu, sonra serin altın; güneş alçak, gölgeler uzun
            new Anahtar(6.0f,  C(1.00f, 0.70f, 0.62f), 0.70f, C(0.50f, 0.50f, 0.64f), C(0.62f, 0.50f, 0.50f), C(0.26f, 0.26f, 0.27f), 0.55f),
            new Anahtar(7.3f,  C(1.00f, 0.86f, 0.71f), 1.05f, C(0.62f, 0.70f, 0.84f), C(0.66f, 0.62f, 0.58f), C(0.36f, 0.40f, 0.33f), 0.66f),
            new Anahtar(10.4f, C(1.00f, 0.93f, 0.84f), 1.20f, C(0.65f, 0.72f, 0.82f), C(0.64f, 0.61f, 0.55f), C(0.40f, 0.44f, 0.34f), 0.70f),
            // Gündüz: en parlak, nötr beyaz ışık, gölgeler kısa
            new Anahtar(11.6f, C(1.00f, 0.96f, 0.90f), 1.30f, C(0.68f, 0.74f, 0.80f), C(0.64f, 0.60f, 0.54f), C(0.40f, 0.44f, 0.34f), 0.72f),
            new Anahtar(16.4f, C(1.00f, 0.95f, 0.86f), 1.25f, C(0.67f, 0.72f, 0.79f), C(0.65f, 0.60f, 0.53f), C(0.40f, 0.43f, 0.33f), 0.72f),
            // Akşam: altın saat → turuncu gün batımı → mor alacakaranlık (pencereler ve lambalar yanar)
            new Anahtar(17.6f, C(1.00f, 0.80f, 0.56f), 1.10f, C(0.66f, 0.64f, 0.70f), C(0.72f, 0.60f, 0.50f), C(0.40f, 0.38f, 0.32f), 0.70f),
            new Anahtar(19.0f, C(1.00f, 0.58f, 0.34f), 0.90f, C(0.60f, 0.50f, 0.62f), C(0.80f, 0.55f, 0.46f), C(0.34f, 0.30f, 0.30f), 0.60f),
            new Anahtar(20.2f, C(0.82f, 0.56f, 0.78f), 0.55f, C(0.42f, 0.36f, 0.56f), C(0.45f, 0.34f, 0.44f), C(0.20f, 0.18f, 0.22f), 0.50f),
            // Gece
            new Anahtar(21.0f, Moon, 0.35f, C(0.30f, 0.29f, 0.44f), C(0.27f, 0.23f, 0.32f), C(0.14f, 0.13f, 0.17f), 0.45f),
            new Anahtar(24.0f, Moon, 0.35f, C(0.30f, 0.29f, 0.44f), C(0.27f, 0.23f, 0.32f), C(0.14f, 0.13f, 0.17f), 0.45f),
        };

        /// <summary>Saate göre ışık anahtarı (iki anahtar arasında yumuşak geçiş).</summary>
        static Anahtar Profil(float h)
        {
            h = ((h % 24f) + 24f) % 24f;
            for (int i = 0; i < Anahtarlar.Length - 1; i++)
            {
                var a = Anahtarlar[i];
                var b = Anahtarlar[i + 1];
                if (h < a.saat || h > b.saat) continue;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a.saat, b.saat, h));
                return new Anahtar(h, Color.Lerp(a.gunes, b.gunes, t), Mathf.Lerp(a.siddet, b.siddet, t), Color.Lerp(a.gok, b.gok, t),
                                   Color.Lerp(a.ufuk, b.ufuk, t), Color.Lerp(a.zemin, b.zemin, t), Mathf.Lerp(a.golge, b.golge, t));
            }
            return GeceA;
        }

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
            foreach (var l in FindObjectsByType<Light>())
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
            float hour = cycle ? DayNight.HourF : 14f;          // döngü kapalıysa hep gündüz
            float night = cycle ? DayNight.Night01 : 0f;
            var p = Profil(hour);

            // Güneşin yolu: sabah 05:30 doğu (alçak) → öğle güney (yüksek) → akşam 20:30 batı (alçak)
            float t = Mathf.Clamp01(Mathf.InverseLerp(5.5f, 20.5f, hour));
            float az = Mathf.PI * t;
            var horiz = new Vector2(Mathf.Cos(az), -Mathf.Sin(az));            // +x doğu, −y güney (kameranın tarafı)
            // Öğlen bile tam tepede değil: gölgeler hep görünsün. Sabah ve akşam güneş alçak, gölgeler uzun.
            float elev = Mathf.Lerp(6f, 52f, Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.85f)) * Mathf.Deg2Rad;

            Vector3 toSun;
            if (night > 0.5f)
                toSun = new Vector3(-0.45f, -0.6f, -0.65f).normalized;        // gece: güneybatıdan alçak ay ışığı
            else
                toSun = new Vector3(horiz.x * Mathf.Cos(elev), horiz.y * Mathf.Cos(elev), -Mathf.Sin(elev)).normalized;   // dünyada "yukarı" −Z
            sun.transform.rotation = Quaternion.LookRotation(-toSun, Vector3.up);

            sun.color = p.gunes;
            sun.intensity = p.siddet;
            sun.shadowStrength = p.golge;

            // Bulutlu ve yağmurlu havada güneş zayıflar, gölgeler silikleşir
            var fx = WeatherFx.I;
            if (fx != null && WeatherFx.Enabled)
            {
                float overcast = Mathf.Clamp01(fx.CloudAmt * 0.45f + fx.RainAmt * 0.35f + fx.StormAmt * 0.25f + fx.SnowAmt * 0.2f);
                sun.intensity *= 1f - overcast * 0.55f;
                sun.shadowStrength *= 1f - overcast * 0.6f;
            }
            // 2D zemin ve kır da geceye uysun (ışıktan etkilenmiyorlar)
            if (CityMap.I != null) CityMap.I.SetNight(night);

            RenderSettings.ambientSkyColor = p.gok;
            RenderSettings.ambientEquatorColor = p.ufuk;
            RenderSettings.ambientGroundColor = p.zemin;
            // Gece camlar gündüz gökyüzünü yansıtıp mavi-beyaz parlamasın
            RenderSettings.reflectionIntensity = Mathf.Lerp(1f, 0.12f, night);
            // Kod kutusu binalar (AVM, otogar, merkez) ışıktan etkilenmiyor: geceye kendileri uysun
            if (City3D.I != null) City3D.I.SetNight(night);
        }
    }
}
