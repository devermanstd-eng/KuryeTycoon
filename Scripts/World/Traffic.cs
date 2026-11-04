using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Şehri canlandıran trafik (süs, oyuna etkisi yok):
    ///  • Arabalar ve otobüsler yollarda sağdan gider, kavşaklarda rastgele döner (çoğunlukla düz devam eder).
    ///  • Yayalar yolların kenarındaki kaldırımlarda yürür (sadece yakınlaşınca görünür).
    /// Hepsi tek kutu modeldir: uzaktan bakınca üstten renkli dikdörtgen, yakınlaşınca 3D görünür.
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

        enum Kind { Car, Bus, Walker }

        class Agent
        {
            public Kind kind;
            public Transform t;
            public Vector2Int from, to;
            public float progress;      // 0–1 iki kare arasında
            public float speed;         // kare / saniye
            public float lane;          // yolun ortasından sağa uzaklık
            public float height;
            public Vector2 visual;      // ekrandaki yumuşatılmış konum
            public float angle;
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
                Spawn(Kind.Car, roads[rng.Next(roads.Count)], new Vector3(0.3f, 0.15f, 0.1f), CarColors[rng.Next(CarColors.Length)],
                      0.55f + (float)rng.NextDouble() * 0.35f, 0.17f);
            for (int i = 0; i < busCount; i++)
                Spawn(Kind.Bus, roads[rng.Next(roads.Count)], new Vector3(0.56f, 0.19f, 0.17f),
                      i % 2 == 0 ? new Color(0.95f, 0.78f, 0.20f) : new Color(0.20f, 0.45f, 0.80f), 0.4f, 0.17f);
            for (int i = 0; i < pedestrianCount; i++)
                Spawn(Kind.Walker, roads[rng.Next(roads.Count)], new Vector3(0.06f, 0.06f, 0.11f), ClothColors[rng.Next(ClothColors.Length)],
                      0.1f + (float)rng.NextDouble() * 0.06f, rng.NextDouble() < 0.5 ? 0.44f : -0.44f);
            lastTilt = -1f;
        }

        void Spawn(Kind kind, Vector2Int at, Vector3 size, Color c, float speed, float lane)
        {
            var go = new GameObject(kind == Kind.Car ? "Araba" : kind == Kind.Bus ? "Otobüs" : "Yaya");
            go.transform.SetParent(root, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            // Yolların ve köprülerin üstünde, binaların altında (önündeki bina aracı örter)
            mr.sortingOrder = 4;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mf.sharedMesh = City3D.BoxMesh();
            City3D.Paint(mf.sharedMesh, c);
            go.transform.localScale = new Vector3(size.x, size.y, 0.001f);

            var a = new Agent { kind = kind, t = go.transform, from = at, speed = speed, lane = lane, height = size.z };
            a.to = NextTile(at, at - Vector2Int.up);
            a.progress = (float)rng.NextDouble();
            a.visual = Target(a);
            agents.Add(a);
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
                if (n == prev || !CityMap.RoadAt(n.x, n.y) || n.x < 0 || n.y < 0 || n.x >= CityMap.Width || n.y >= CityMap.Height) continue;
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

                if (rescale)
                {
                    var s = a.t.localScale;
                    a.t.localScale = new Vector3(s.x, s.y, Mathf.Max(0.001f, a.height * tilt));
                }
            }
            lastWalkersVisible = walkersVisible;
        }
    }
}
