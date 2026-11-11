using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Havanın 3D görüntüsü: Blender'da yapılan modellerle (Resources/SehirPaketi/Hava, adları "Hava_...").
    ///  • Bulutlar: şehrin üstünde rüzgârla kayar, güneş ışığında gerçek gölge düşürür (gölgeler binaların üstünden geçer).
    ///    Yağmurda ve fırtınada koyu fırtına bulutları; yakınlaşınca bulut görünmez, sadece gölgesi kalır (manzarayı kapatmasın).
    ///  • Yağmur: rüzgârla akan 3D damlalar ve yerde sıçrama halkaları. Kar: sallanarak süzülen kar taneleri.
    ///  • Rüzgârlı havada savrulan yapraklar; fırtınada şimşek çakar.
    /// Yoğunlukları WeatherFx verir (oyuna etkisi WeatherSystem'de). 3D ışık (Gorsel3D) kapalıysa ya da harita
    /// uzaktan düz görünümdeyse çalışmaz; o zaman WeatherFx'in ekran efektleri çizilir.
    /// </summary>
    public class Hava3D : MonoBehaviour
    {
        public static Hava3D I { get; private set; }
        const string Folder = "SehirPaketi/Hava";

        /// <summary>3D hava şu an çiziliyor mu (WeatherFx aynı şeyleri ekrana ikinci kez çizmesin).</summary>
        public static bool Showing => I != null && I.showing;

        GameObject[] whiteClouds, stormClouds;
        GameObject boltPrefab;
        Mesh dropMesh, splashMesh, snowMesh;
        Material dropMat, splashMat, snowMat;
        readonly List<(Mesh mesh, Material mat)> leafKinds = new List<(Mesh, Material)>();
        bool loaded, showing;

        Transform root;
        ParticleSystem rainPs, splashPs, snowPs;
        readonly List<ParticleSystem> leafPs = new List<ParticleSystem>();

        class Cloud { public Transform t; public Renderer[] rs; public bool storm; public float speed; public float grow; public bool leaving; public float baseScale; }
        readonly List<Cloud> clouds = new List<Cloud>();
        readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();

        Transform bolt;
        float boltTimer, lastFlash;

        // Ölçüler (1 kare = 1 birim ≈ 10 m)
        const float CloudHeight = 9f;      // en uzun kulelerin (≈6) üstünde
        const float CloudWidth = 5.5f;     // büyük bulutun genişliği (kare)
        const float RainTop = 6f;          // damlaların doğduğu yükseklik
        const float RainSpeed = 18f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("Hava (3D)");
            DontDestroyOnLoad(go);
            go.AddComponent<Hava3D>();
        }

        void Awake()
        {
            I = this;
            TryInit();
        }

        bool initDone;
        float retryTimer;
        string lastReason;

        /// <summary>Modeller yüklenince parçacık sistemlerini kurar. Modeller oyun açıldıktan sonra eklenirse (prefab aracı) tekrar dener.</summary>
        void TryInit()
        {
            if (initDone) return;
            Load();
            if (!loaded) return;
            initDone = true;
            root = new GameObject("Hava parçaları").transform;
            root.SetParent(transform, false);
            if (dropMesh != null)
            {
                rainPs = MakeSystem("Yağmur", dropMesh, dropMat, 5000);
                if (splashMesh != null) splashPs = MakeSystem("Sıçrama", splashMesh, splashMat, 800);
            }
            if (snowMesh != null) snowPs = MakeSystem("Kar", snowMesh, snowMat, 4000);
            foreach (var lk in leafKinds) leafPs.Add(MakeSystem("Yaprak", lk.mesh, lk.mat, 200));
            ConfigureRain();
            ConfigureSnow();
            ConfigureLeaves();
            if (boltPrefab != null)
            {
                bolt = Spawn(boltPrefab, root).transform;
                foreach (var r in bolt.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bolt.gameObject.SetActive(false);
            }
            root.gameObject.SetActive(false);
        }

        void OnDestroy() { if (I == this) I = null; }

        // ---------------------------------------------------------------- yükleme

        void Load()
        {
            var all = Resources.LoadAll<GameObject>(Folder);
            var white = new List<GameObject>();
            var storm = new List<GameObject>();
            foreach (var p in all)
            {
                string n = p.name;
                if (n.StartsWith("Hava_bulut")) white.Add(p);
                else if (n.StartsWith("Hava_firtina")) storm.Add(p);
                else if (n == "Hava_damla") Part(p, out dropMesh, out dropMat);
                else if (n == "Hava_sicrama") Part(p, out splashMesh, out splashMat);
                else if (n == "Hava_kar") Part(p, out snowMesh, out snowMat);
                else if (n.StartsWith("Hava_yaprak")) { Part(p, out var m, out var mat); if (m != null) leafKinds.Add((m, mat)); }
                else if (n == "Hava_simsek") boltPrefab = p;
            }
            whiteClouds = white.ToArray();
            stormClouds = storm.Count > 0 ? storm.ToArray() : whiteClouds;
            loaded = whiteClouds.Length > 0 || dropMesh != null || snowMesh != null;
        }

        static void Part(GameObject prefab, out Mesh mesh, out Material mat)
        {
            var mf = prefab.GetComponentInChildren<MeshFilter>(true);
            var mr = prefab.GetComponentInChildren<Renderer>(true);
            mesh = mf != null ? mf.sharedMesh : null;
            mat = mr != null ? mr.sharedMaterial : null;
        }

        static GameObject Spawn(GameObject prefab, Transform parent)
        {
            var go = Instantiate(prefab, parent, false);
            // Blender modelleri Y yukarı gelir; haritada "yukarı" −Z (binalarla aynı dönüş)
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var lg in go.GetComponentsInChildren<LODGroup>()) lg.enabled = false;
            return go;
        }

        ParticleSystem MakeSystem(string name, Mesh mesh, Material mat, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.startRotation3D = true;
            // Model Y yukarı; −90° X dönüşüyle haritanın yukarısına (−Z) bakar
            main.startRotationX = -90f * Mathf.Deg2Rad;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = mesh;
            r.alignment = ParticleSystemRenderSpace.World;
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingOrder = 18;
            ps.Play();
            return ps;
        }

        void ConfigureRain()
        {
            if (rainPs != null)
            {
                var main = rainPs.main;
                main.startLifetime = RainTop / RainSpeed;
                main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.42f);
                var v = rainPs.velocityOverLifetime;
                v.enabled = true;
                v.space = ParticleSystemSimulationSpace.World;
                v.x = 0f; v.y = 0f; v.z = RainSpeed;
            }
            if (splashPs != null)
            {
                var main = splashPs.main;
                main.startLifetime = 0.35f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
                var sol = splashPs.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.3f, 1f, 1f));
            }
        }

        void ConfigureSnow()
        {
            if (snowPs == null) return;
            var main = snowPs.main;
            main.startLifetime = RainTop / 1.4f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var v = snowPs.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.World;
            v.x = 0f; v.y = 0f; v.z = 1.4f;
            var noise = snowPs.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.35f;
            var rot = snowPs.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-1f, 1f);
            rot.y = new ParticleSystem.MinMaxCurve(-1f, 1f);
            rot.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
        }

        void ConfigureLeaves()
        {
            foreach (var ps in leafPs)
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.13f);
                main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                var v = ps.velocityOverLifetime;
                v.enabled = true;
                v.space = ParticleSystemSimulationSpace.World;
                v.x = 3f; v.y = 0f; v.z = 0.15f;
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 1.2f;
                noise.frequency = 0.5f;
                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.separateAxes = true;
                rot.x = new ParticleSystem.MinMaxCurve(-4f, 4f);
                rot.y = new ParticleSystem.MinMaxCurve(-4f, 4f);
                rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            }
        }

        // ---------------------------------------------------------------- her kare

        void LateUpdate()
        {
            var fx = WeatherFx.I;
            var mc = MapCamera.I;
            var cam = Camera.main;
            if (!initDone)
            {
                retryTimer -= Time.unscaledDeltaTime;
                if (retryTimer <= 0f) { retryTimer = 3f; TryInit(); }
            }
            // Neden çizilmiyor: Console'a bir kez yazılır (sorun ararken işe yarar)
            string reason = !initDone ? "Resources/" + Folder + " içinde Hava_ prefabı yok (prefab aracını çalıştırın)"
                : !Gorsel3D.Active ? "3D ışık kapalı (Kurye Tycoon > 3D Işık ve Gölge Kurulumu)"
                : !WeatherFx.Enabled ? "hava efektleri ayarlardan kapalı"
                : fx == null || mc == null || cam == null || CityMap.I == null ? "oyun henüz hazır değil"
                : GameBootstrap.I == null || !GameBootstrap.I.Started ? "oyun başlamadı"
                : !mc.ShowModels ? "harita uzak/düz görünümde (yakınlaşınca 3D hava başlar)"
                : null;
            if (reason != lastReason)
            {
                lastReason = reason;
                Debug.Log("Kurye Tycoon · 3D hava: " + (reason == null ? "açık (bulut, yağmur, kar 3D çiziliyor)" : "kapalı, " + reason));
            }
            bool on = reason == null;
            if (on != showing)
            {
                showing = on;
                if (root != null) root.gameObject.SetActive(on);
                // Kapanıp açılınca parçacık sistemleri kendiliğinden başlamaz
                if (on)
                {
                    if (rainPs != null) rainPs.Play();
                    if (splashPs != null) splashPs.Play();
                    if (snowPs != null) snowPs.Play();
                    foreach (var ps in leafPs) ps.Play();
                }
            }
            if (!on) return;

            bool paused = GameClock.I == null || GameClock.I.Paused;
            float dt = paused ? 0f : Time.unscaledDeltaTime;
            float speed = GameClock.I != null && !paused ? Mathf.Clamp(Mathf.Sqrt(Mathf.Max(1f, GameClock.I.SpeedMultiplier)), 1f, 3f) : 0f;

            // Ekranın ortasının ve köşesinin zemindeki karşılığı: yağış ekranın gördüğü alana düşsün
            var center = new Vector2(CityMap.Width / 2f, CityMap.Height / 2f);
            float radius = 15f;
            if (mc.GroundPoint(cam, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out var c0))
            {
                center = c0;
                if (mc.GroundPoint(cam, new Vector2(Screen.width, Screen.height), out var c1))
                    radius = Mathf.Clamp(Vector2.Distance(c0, c1) * 1.15f, 5f, 40f);
            }

            float wind = fx.WindAmt;
            float windX = 0.6f + wind * 4.5f;   // kare/sn (yağmur ve yapraklar için)

            UpdateClouds(fx, mc, dt * speed, wind);
            UpdatePrecip(fx, center, radius, windX, paused);
            UpdateBolt(fx, center, radius);
        }

        void UpdatePrecip(WeatherFx fx, Vector2 center, float radius, float windX, bool paused)
        {
            float area = (radius * radius) / (15f * 15f);   // görülen alan büyüdükçe daha çok damla (yoğunluk aynı kalsın)
            float sim = paused ? 0f : 1f;

            if (rainPs != null)
            {
                rainPs.transform.position = new Vector3(center.x - windX * 0.15f, center.y, -RainTop);
                var shape = rainPs.shape; shape.scale = new Vector3(radius * 2f, radius * 2f, 0.1f);
                var em = rainPs.emission; em.rateOverTime = Mathf.Min(fx.RainAmt * 2600f * area, 9000f);
                var v = rainPs.velocityOverLifetime; v.x = windX * 0.8f; v.y = 0f; v.z = RainSpeed;
                var main = rainPs.main; main.simulationSpeed = sim;
            }
            if (splashPs != null)
            {
                splashPs.transform.position = new Vector3(center.x, center.y, -0.03f);
                var shape = splashPs.shape; shape.scale = new Vector3(radius * 1.6f, radius * 1.6f, 0.01f);
                var em = splashPs.emission; em.rateOverTime = Mathf.Min(fx.RainAmt * 420f * area, 2000f);
                var main = splashPs.main; main.simulationSpeed = sim;
            }
            if (snowPs != null)
            {
                snowPs.transform.position = new Vector3(center.x - windX * 0.6f, center.y, -RainTop);
                var shape = snowPs.shape; shape.scale = new Vector3(radius * 2f, radius * 2f, 0.1f);
                var em = snowPs.emission; em.rateOverTime = Mathf.Min(fx.SnowAmt * 700f * area, 3000f);
                var v = snowPs.velocityOverLifetime; v.x = windX * 0.3f; v.y = 0f; v.z = 1.4f;
                var main = snowPs.main; main.simulationSpeed = sim;
            }
            float leafAmt = Mathf.Clamp01((fx.WindAmt - 0.35f) * 1.6f) * (fx.SnowAmt > 0.5f ? 0f : 1f);
            foreach (var ps in leafPs)
            {
                ps.transform.position = new Vector3(center.x - radius * 0.8f, center.y, -1.2f);
                var shape = ps.shape; shape.scale = new Vector3(radius * 0.6f, radius * 2f, 1.5f);
                var em = ps.emission; em.rateOverTime = leafAmt * 10f * area;
                var v = ps.velocityOverLifetime; v.x = windX * 1.1f; v.y = 0f; v.z = 0.1f;
                var main = ps.main; main.simulationSpeed = sim;
            }
        }

        void UpdateClouds(WeatherFx fx, MapCamera mc, float dt, float wind)
        {
            if (whiteClouds.Length == 0) return;
            float rainy = Mathf.Clamp01(fx.RainAmt + fx.StormAmt * 0.5f);
            int want = Mathf.Clamp(Mathf.RoundToInt(fx.SunAmt * 3f + fx.CloudAmt * 8f + fx.RainAmt * 4f + fx.StormAmt * 4f + fx.SnowAmt * 3f), 0, 22);
            int active = 0;
            foreach (var c in clouds) if (!c.leaving) active++;
            // Fazla bulutlar küçülerek kaybolur, eksikler büyüyerek belirir (yağmura geçince fırtına bulutları gelir)
            for (int i = 0; i < clouds.Count && active > want; i++)
                if (!clouds[i].leaving) { clouds[i].leaving = true; active--; }
            for (int i = 0; i < clouds.Count && rainy > 0.5f; i++)
                if (!clouds[i].storm && !clouds[i].leaving && Random.value < 0.004f) { clouds[i].leaving = true; active--; }
            while (active < want) { AddCloud(rainy > 0.5f); active++; }

            // Yakınlaşınca bulutların kendisi manzarayı kapatır: sadece gölgesi kalsın
            bool shadowOnly = mc.Zoom > 0.62f;
            var mode = shadowOnly ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
            float gray = Mathf.Clamp01(fx.RainAmt * 0.45f + fx.StormAmt * 0.3f + fx.SnowAmt * 0.15f);
            float minX = -14f, maxX = CityMap.Width + 14f;

            for (int i = clouds.Count - 1; i >= 0; i--)
            {
                var c = clouds[i];
                c.grow = Mathf.MoveTowards(c.grow, c.leaving ? 0f : 1f, Time.unscaledDeltaTime * 0.35f);
                if (c.leaving && c.grow <= 0f) { Destroy(c.t.gameObject); clouds.RemoveAt(i); continue; }
                var p = c.t.position;
                p.x += (c.speed + wind * 0.9f) * dt;
                if (p.x > maxX) { p.x = minX; p.y = Random.Range(-6f, CityMap.Height + 6f); }
                c.t.position = p;
                c.t.localScale = Vector3.one * c.baseScale * Mathf.SmoothStep(0f, 1f, c.grow);
                foreach (var r in c.rs)
                {
                    if (r.shadowCastingMode != mode) r.shadowCastingMode = mode;
                    if (!c.storm) Darken(r, gray);
                }
            }
        }

        void AddCloud(bool storm)
        {
            var arr = storm ? stormClouds : whiteClouds;
            var go = Spawn(arr[Random.Range(0, arr.Length)], root);
            go.name = storm ? "Fırtına bulutu" : "Bulut";
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = new Bounds();
            bool any = false;
            foreach (var r in rs)
            {
                r.receiveShadows = false;
                r.sortingOrder = 17;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            float w = Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.y));
            float target = CloudWidth * Random.Range(0.6f, 1.15f) * (storm ? 1.25f : 1f);
            var c = new Cloud
            {
                t = go.transform, rs = rs, storm = storm, speed = Random.Range(0.25f, 0.55f),
                baseScale = target / w, grow = 0f
            };
            go.transform.position = new Vector3(Random.Range(-14f, CityMap.Width + 14f), Random.Range(-6f, CityMap.Height + 6f),
                                                -CloudHeight - Random.Range(0f, 2.5f));
            go.transform.localScale = Vector3.zero;
            clouds.Add(c);
        }

        /// <summary>Beyaz bulutlar yağmur ve karda griye döner (çizgi siyah kalır).</summary>
        void Darken(Renderer r, float gray)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null || m.name.StartsWith("Cizgi")) continue;
                Color baseC = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
                var c = Color.Lerp(baseC, new Color(0.55f, 0.58f, 0.63f), gray);
                mpb.Clear();
                mpb.SetColor("_BaseColor", c);
                mpb.SetColor("_Color", c);
                r.SetPropertyBlock(mpb, i);
            }
        }

        void UpdateBolt(WeatherFx fx, Vector2 center, float radius)
        {
            if (bolt == null) return;
            float f = fx.Flash;
            // Ekran parlaması başladığı anda görünen alanın içinde bir yere şimşek iner
            if (f > 0.35f && lastFlash <= 0.35f)
            {
                var p = center + Random.insideUnitCircle * radius * 0.7f;
                bolt.position = new Vector3(p.x, p.y, 0f);
                bolt.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)) * Quaternion.Euler(-90f, 0f, 0f);
                bolt.localScale = Vector3.one * Random.Range(0.75f, 1f);   // model 10 m: sahnede ≈ 8–10 kare boy
                bolt.gameObject.SetActive(true);
                boltTimer = 0.22f;
            }
            lastFlash = f;
            if (boltTimer > 0f)
            {
                boltTimer -= Time.unscaledDeltaTime;
                if (boltTimer <= 0f) bolt.gameObject.SetActive(false);
            }
        }
    }
}
