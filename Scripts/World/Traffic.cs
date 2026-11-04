using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Şehri canlandıran trafik (süs, oyuna etkisi yok):
    ///  • Arabalar ve otobüsler yollarda sağdan gider, kavşaklarda rastgele döner (çoğunlukla düz devam eder).
    ///  • Yayalar yolların kenarındaki kaldırımlarda yürür (sadece yakınlaşınca görünür).
    /// Şehir paketi kuruluysa (Kurye Tycoon > Şehir Paketini Kur) paketin araçları ve insanları kullanılır
    /// (Resources/SehirPaketi/Araclar ve Insanlar); yoksa basit renkli kutular.
    /// Oyun duraklatılınca durur, hızlandırılınca biraz hızlanır.
    /// </summary>
    public class Traffic : MonoBehaviour
    {
        public static Traffic I { get; private set; }

        public int carCount = 45;
        public int busCount = 6;
        public int pedestrianCount = 70;

        const string PrefKey = "KuryeTycoon.Trafik";
        public static bool Enabled
        {
            get { return PlayerPrefs.GetInt(PrefKey, 1) == 1; }
            set { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        const string FacingKey = "KuryeTycoon.AracYonu";
        /// <summary>Paketteki araçların burnu farklı yöne bakıyorsa düzeltme (0/90/180/270).</summary>
        public static int FacingOffset
        {
            get { return PlayerPrefs.GetInt(FacingKey, 0); }
            set { PlayerPrefs.SetInt(FacingKey, ((value % 360) + 360) % 360); PlayerPrefs.Save(); }
        }

        static GameObject[] packCars, packBuses, packPeople;
        public static int PackVehicleCount { get { LoadPack(); return packCars.Length + packBuses.Length; } }
        public static int PackPeopleCount { get { LoadPack(); return packPeople.Length; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { packCars = packBuses = packPeople = null; }

        static void LoadPack()
        {
            if (packCars != null) return;
            var vehicles = Resources.LoadAll<GameObject>(City3D.PackFolder + "/Araclar");
            var cars = new List<GameObject>();
            var buses = new List<GameObject>();
            foreach (var v in vehicles)
            {
                string n = v.name.ToLowerInvariant();
                if (n.Contains("bus") || n.Contains("truck") || n.Contains("lorry") || n.Contains("garbage") || n.Contains("fire")) buses.Add(v);
                else cars.Add(v);
            }
            packCars = cars.ToArray();
            packBuses = buses.ToArray();
            packPeople = Resources.LoadAll<GameObject>(City3D.PackFolder + "/Insanlar");
        }

        enum Kind { Car, Bus, Walker }

        class Agent
        {
            public Kind kind;
            public Transform t;
            public bool prefab;         // paket modeli (yüksekliği sabit) ya da kutu (yakınlaştıkça yükselir)
            public Vector2Int from, to;
            public float progress;      // 0–1 iki kare arasında
            public float speed;         // kare / saniye
            public float lane;          // yolun ortasından sağa uzaklık
            public float height;
            public Vector2 visual;      // yumuşatılmış konum
            public float angle;
            public Animator anim;
        }

        readonly List<Agent> agents = new List<Agent>();
        Transform root;
        Material mat;
        System.Random rng;
        float lastTilt = -1f;
        bool lastWalkersVisible = true;

        static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        static readonly Color[] CarColors =
        {
            new Color(0.85f, 0.20f, 0.20f), new Color(0.20f, 0.40f, 0.85f), new Color(0.95f, 0.95f, 0.95f),
            new Color(0.15f, 0.15f, 0.17f), new Color(0.65f, 0.67f, 0.70f), new Color(0.95f, 0.75f, 0.15f),
            new Color(0.20f, 0.60f, 0.35f), new Color(0.55f, 0.25f, 0.20f)
        };
        static readonly Color[] ClothColors =
        {
            new Color(0.85f, 0.30f, 0.30f), new Color(0.25f, 0.45f, 0.80f), new Color(0.95f, 0.85f, 0.30f),
            new Color(0.30f, 0.65f, 0.40f), new Color(0.75f, 0.45f, 0.75f), new Color(0.90f, 0.90f, 0.90f), new Color(0.25f, 0.25f, 0.28f)
        };

        void Awake() { I = this; }

        public void Build()
        {
            if (root != null) Destroy(root.gameObject);
            agents.Clear();
            root = new GameObject("Trafik").transform;
            root.SetParent(transform, false);
            if (!Enabled) return;

            packCars = null;
            LoadPack();
            bool usePack = City3D.UsePack;

            // Kutular için sprite malzemesi (vertex rengini gösterir, projenin render pipeline'ına uygun)
            var probe = SpriteFactory.Create("malzeme", root, false, Color.white, Vector3.zero, 1f, 0);
            mat = probe.sharedMaterial;
            Destroy(probe.gameObject);

            rng = new System.Random(9001);
            var roads = new List<Vector2Int>();
            for (int x = 0; x < CityMap.Width; x++)
                for (int y = 0; y < CityMap.Height; y++)
                    if (CityMap.RoadAt(x, y)) roads.Add(new Vector2Int(x, y));
            if (roads.Count == 0) return;

            for (int i = 0; i < carCount; i++)
            {
                var a = NewAgent(Kind.Car, roads[rng.Next(roads.Count)], 0.55f + (float)rng.NextDouble() * 0.35f, 0.17f);
                if (usePack && packCars.Length > 0) AttachPrefab(a, packCars[rng.Next(packCars.Length)], 0.34f, false);
                else AttachBox(a, new Vector3(0.3f, 0.15f, 0.1f), CarColors[rng.Next(CarColors.Length)]);
            }
            for (int i = 0; i < busCount; i++)
            {
                var a = NewAgent(Kind.Bus, roads[rng.Next(roads.Count)], 0.4f, 0.17f);
                if (usePack && packBuses.Length > 0) AttachPrefab(a, packBuses[rng.Next(packBuses.Length)], 0.58f, false);
                else if (usePack && packCars.Length > 0) AttachPrefab(a, packCars[rng.Next(packCars.Length)], 0.34f, false);
                else AttachBox(a, new Vector3(0.56f, 0.19f, 0.17f), i % 2 == 0 ? new Color(0.95f, 0.78f, 0.20f) : new Color(0.20f, 0.45f, 0.80f));
            }
            for (int i = 0; i < pedestrianCount; i++)
            {
                var a = NewAgent(Kind.Walker, roads[rng.Next(roads.Count)], 0.1f + (float)rng.NextDouble() * 0.06f, rng.NextDouble() < 0.5 ? 0.44f : -0.44f);
                if (usePack && packPeople.Length > 0) AttachPrefab(a, packPeople[rng.Next(packPeople.Length)], CharacterVisuals.TargetHeight * 0.92f, true);
                else AttachBox(a, new Vector3(0.06f, 0.06f, 0.11f), ClothColors[rng.Next(ClothColors.Length)]);
            }
            lastTilt = -1f;
            lastWalkersVisible = true;
        }

        Agent NewAgent(Kind kind, Vector2Int at, float speed, float lane)
        {
            var go = new GameObject(kind == Kind.Car ? "Araba" : kind == Kind.Bus ? "Otobüs" : "Yaya");
            go.transform.SetParent(root, false);
            var a = new Agent { kind = kind, t = go.transform, from = at, speed = speed, lane = lane };
            a.to = NextTile(at, at - Vector2Int.up);
            a.progress = (float)rng.NextDouble();
            a.visual = Target(a);
            a.t.localPosition = new Vector3(a.visual.x, a.visual.y, -0.003f);
            agents.Add(a);
            return a;
        }

        /// <summary>Basit kutu: uzaktan üstten dikdörtgen, yakınlaşınca 3D.</summary>
        void AttachBox(Agent a, Vector3 size, Color c)
        {
            var mf = a.t.gameObject.AddComponent<MeshFilter>();
            var mr = a.t.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingOrder = 4;   // yolların ve köprülerin üstünde, binaların altında
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mf.sharedMesh = City3D.BoxMesh();
            City3D.Paint(mf.sharedMesh, c);
            a.t.localScale = new Vector3(size.x, size.y, 0.001f);
            a.height = size.z;
        }

        /// <summary>
        /// Paket modeli: araçlar boyuna göre (uzunluk), insanlar boylarına göre ölçeklenir. Model Y yukarı, Z ileri kabul edilir
        /// (aracın burnu ters duruyorsa Ayarlar > Harita > Araç yönü).
        /// </summary>
        void AttachPrefab(Agent a, GameObject prefab, float targetSize, bool byHeight)
        {
            var inst = Instantiate(prefab, a.t, false);
            inst.name = prefab.name;
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localScale = Vector3.one;
            inst.transform.localRotation = Quaternion.Euler(0f, 0f, -90f + FacingOffset) * Quaternion.Euler(-90f, 0f, 0f);

            foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                mb.enabled = false;
                Destroy(mb);
            }
            foreach (var col in inst.GetComponentsInChildren<Collider>()) Destroy(col);
            foreach (var rb in inst.GetComponentsInChildren<Rigidbody>()) Destroy(rb);
            foreach (var l in inst.GetComponentsInChildren<Light>()) Destroy(l);
            var renderers = inst.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Destroy(inst); AttachBox(a, new Vector3(0.3f, 0.15f, 0.1f), CarColors[0]); return; }
            foreach (var r in renderers)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.sortingOrder = byHeight ? 19 : 4;   // yayalar kurye karakterleriyle aynı katmanda, araçlar binaların altında
            }

            // Boyut: ara (holder) dönmeden önceki duruşta ölç
            var rot = a.t.rotation;
            a.t.rotation = Quaternion.identity;
            var b = Bounds(renderers);
            float size = byHeight ? b.size.z : Mathf.Max(b.size.x, b.size.y);
            float s = targetSize / Mathf.Max(0.001f, size);
            inst.transform.localScale = Vector3.one * s;
            b = Bounds(renderers);
            inst.transform.position += new Vector3(a.t.position.x - b.center.x, a.t.position.y - b.center.y, -b.max.z);
            a.t.rotation = rot;

            a.prefab = true;
            a.anim = inst.GetComponentInChildren<Animator>();
            if (a.anim != null) a.anim.applyRootMotion = false;
        }

        static Bounds Bounds(Renderer[] rs)
        {
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>Kavşakta sonraki kare: geri dönmez (çıkmaz sokak hariç), çoğunlukla düz devam eder.</summary>
        Vector2Int NextTile(Vector2Int cur, Vector2Int prev)
        {
            var dir = cur - prev;
            var options = new List<Vector2Int>();
            Vector2Int straight = cur + dir;
            bool hasStraight = false;
            foreach (var d in Dirs)
            {
                var n = cur + d;
                if (n == prev || n.x < 0 || n.y < 0 || n.x >= CityMap.Width || n.y >= CityMap.Height || !CityMap.RoadAt(n.x, n.y)) continue;
                options.Add(n);
                if (n == straight) hasStraight = true;
            }
            if (options.Count == 0) return prev;                    // çıkmaz: geri dön
            if (hasStraight && rng.NextDouble() < 0.65) return straight;
            return options[rng.Next(options.Count)];
        }

        Vector2 Target(Agent a)
        {
            Vector2 f = a.from, t = a.to;
            var dir = (t - f);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            dir.Normalize();
            var right = new Vector2(dir.y, -dir.x);
            return Vector2.Lerp(f, t, a.progress) + right * a.lane;
        }

        void Update()
        {
            if (agents.Count == 0 || CityMap.I == null) return;
            var clock = GameClock.I;
            bool paused = clock == null || clock.Paused;
            float gs = clock != null ? Mathf.Clamp(Mathf.Sqrt(clock.SpeedMultiplier), 1f, 3f) : 1f;
            float dt = paused ? 0f : Time.deltaTime * gs;

            float tilt = MapCamera.I != null ? MapCamera.I.Tilt01 : 0f;
            bool walkersVisible = MapCamera.I != null && MapCamera.I.Zoom > 0.4f;
            bool rescale = Mathf.Abs(tilt - lastTilt) > 0.001f;
            lastTilt = tilt;

            foreach (var a in agents)
            {
                if (a.kind == Kind.Walker && walkersVisible != lastWalkersVisible) a.t.gameObject.SetActive(walkersVisible);
                if (a.kind == Kind.Walker && !walkersVisible) continue;

                a.progress += a.speed * dt;
                while (a.progress >= 1f)
                {
                    a.progress -= 1f;
                    var prev = a.from;
                    a.from = a.to;
                    a.to = NextTile(a.from, prev);
                }
                var target = Target(a);
                // Dönüşlerde şerit değişimi yumuşak olsun
                a.visual = Vector2.Lerp(a.visual, target, paused ? 0f : Mathf.Clamp01(Time.deltaTime * 10f));
                a.t.localPosition = new Vector3(a.visual.x, a.visual.y, -0.003f);

                var d = (Vector2)(a.to - a.from);
                if (d.sqrMagnitude > 0.0001f)
                {
                    float want = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                    a.angle = Mathf.LerpAngle(a.angle, want, paused ? 0f : Mathf.Clamp01(Time.deltaTime * 8f));
                    a.t.localRotation = Quaternion.Euler(0f, 0f, a.angle);
                }
                if (a.anim != null) a.anim.speed = paused ? 0f : gs;

                if (rescale && !a.prefab)
                {
                    var s = a.t.localScale;
                    a.t.localScale = new Vector3(s.x, s.y, Mathf.Max(0.001f, a.height * tilt));
                }
            }
            lastWalkersVisible = walkersVisible;
        }
    }
}
