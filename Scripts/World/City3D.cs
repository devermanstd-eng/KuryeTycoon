using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Yakınlaştırınca yerden yükselen basit 3D şehir: binalar, dükkanlar, merkez ofis ve parktaki ağaçlar.
    /// Harita XY düzleminde; binalar −Z yönünde (kameraya doğru) yükselir.
    /// Her bina tek mesh: çatı + kameraya bakan ön cephe (vertex rengiyle gölgeli). Sprite malzemesini kullanır,
    /// böylece hem URP 2D Renderer'da hem normal URP'de ışık gerektirmeden aynı görünür.
    /// </summary>
    public class City3D : MonoBehaviour
    {
        public static City3D I { get; private set; }

        class Piece
        {
            public Transform t;
            public MeshFilter mf;
            public float height;
            public Color baseColor;
            public int tileX;
            public Shop shop;
            public bool hq;
            public bool tree;
        }

        readonly List<Piece> pieces = new List<Piece>();
        readonly Dictionary<Vector2Int, float> heightByTile = new Dictionary<Vector2Int, float>();
        Material mat;
        float lastGrow = -1f;
        int lastUnlock = -1;
        int lastContracts = -1;

        void Awake() { I = this; }

        /// <summary>Etiketlerin çatının üstünde durması için karedeki binanın tam boyu (yükselme oranı dahil).</summary>
        public float HeightAt(Vector2Int tile)
        {
            float g = MapCamera.I != null ? MapCamera.I.Tilt01 : 0f;
            return heightByTile.TryGetValue(tile, out float h) ? h * g : 0f;
        }

        public void Build()
        {
            var map = CityMap.I;
            var root = new GameObject("3D Şehir").transform;
            root.SetParent(transform, false);

            // Sprite'ların malzemesi: projenin render pipeline'ına uygun, vertex rengini gösterir
            var probe = SpriteFactory.Create("malzeme", root, false, Color.white, Vector3.zero, 1f, 0);
            mat = probe.sharedMaterial;
            Destroy(probe.gameObject);

            var shopTiles = new Dictionary<Vector2Int, Shop>();
            foreach (var s in map.Shops) shopTiles[s.tile] = s;

            var rng = new System.Random(12345);
            for (int x = 0; x < CityMap.Width; x++)
            {
                for (int y = 0; y < CityMap.Height; y++)
                {
                    if (map.IsRoad(x, y)) continue;
                    var tile = new Vector2Int(x, y);
                    bool frontage = IsAdjacentToRoad(map, x, y);

                    if (tile == map.HQTile)
                    {
                        Add(root, tile, 0.84f, 1.9f, CompanyProfile.Primary, null, true, false);
                    }
                    else if (shopTiles.TryGetValue(tile, out var shop))
                    {
                        Add(root, tile, 0.8f, 0.75f, shop.def.color, shop, false, false);
                    }
                    else if (!frontage)
                    {
                        // Park: birkaç ağaç
                        int n = 1 + rng.Next(3);
                        for (int i = 0; i < n; i++)
                        {
                            var off = new Vector2((float)rng.NextDouble() * 0.6f - 0.3f, (float)rng.NextDouble() * 0.6f - 0.3f);
                            float h = 0.25f + (float)rng.NextDouble() * 0.25f;
                            var c = Color.Lerp(new Color(0.16f, 0.42f, 0.22f), new Color(0.28f, 0.55f, 0.25f), (float)rng.NextDouble());
                            Add(root, tile, 0.22f, h, c, null, false, true, off);
                        }
                    }
                    else
                    {
                        // Bölgeye göre bina karakteri: merkez yüksek, doğu orta, sanayi alçak ve geniş
                        float h, size = 0.82f;
                        int region = x <= CityMap.UnlockMaxX[1] ? 1 : (x <= CityMap.UnlockMaxX[2] ? 2 : 3);
                        if (region == 1) h = 0.6f + (float)rng.NextDouble() * 1.2f;
                        else if (region == 2) h = 0.5f + (float)rng.NextDouble() * 0.8f;
                        else { h = 0.4f + (float)rng.NextDouble() * 0.4f; size = 0.88f; }
                        float shade = (float)rng.NextDouble();
                        var c = Color.Lerp(new Color(0.42f, 0.45f, 0.52f), new Color(0.58f, 0.55f, 0.50f), shade);
                        Add(root, tile, size, h, c, null, false, false);
                    }
                }
            }
            Refresh(true);
        }

        static bool IsAdjacentToRoad(CityMap map, int x, int y) =>
            (x > 0 && map.IsRoad(x - 1, y)) || (x < CityMap.Width - 1 && map.IsRoad(x + 1, y)) ||
            (y > 0 && map.IsRoad(x, y - 1)) || (y < CityMap.Height - 1 && map.IsRoad(x, y + 1));

        void Add(Transform root, Vector2Int tile, float size, float height, Color c, Shop shop, bool hq, bool tree, Vector2 offset = default)
        {
            var go = new GameObject(shop != null ? shop.def.name + " (3D)" : hq ? "Merkez Ofis (3D)" : tree ? "Ağaç" : "Bina");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(tile.x + offset.x, tile.y + offset.y, 0f);
            go.transform.localScale = new Vector3(size, size, 0.001f);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingOrder = 5; // zemin ve dükkan karelerinin üstünde, kurye ve müşterilerin altında
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mf.sharedMesh = BoxMesh();
            var p = new Piece { t = go.transform, mf = mf, height = height, baseColor = c, tileX = tile.x, shop = shop, hq = hq, tree = tree };
            pieces.Add(p);
            if (!tree) heightByTile[tile] = height;
            go.SetActive(false);
        }

        /// <summary>
        /// Birim kutu: taban z=0, çatı z=−1. Sadece kameradan görünen yüzler (çatı, güney cephe, doğu/batı yanlar).
        /// Renkler vertex rengiyle verilir; çatı açık, cepheler koyu.
        /// </summary>
        static Mesh BoxMesh()
        {
            var m = new Mesh();
            var v = new List<Vector3>();
            var tris = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }
            const float h = 0.5f;
            // Sıra önemli (malzeme derinlik yazmaz): önce yanlar, sonra ön cephe, en son çatı
            Quad(new Vector3(-h, -h, 0), new Vector3(-h, h, 0), new Vector3(-h, h, -1), new Vector3(-h, -h, -1)); // batı
            Quad(new Vector3(h, -h, 0), new Vector3(h, -h, -1), new Vector3(h, h, -1), new Vector3(h, h, 0));     // doğu
            Quad(new Vector3(-h, -h, 0), new Vector3(-h, -h, -1), new Vector3(h, -h, -1), new Vector3(h, -h, 0)); // güney (ön)
            Quad(new Vector3(-h, -h, -1), new Vector3(-h, h, -1), new Vector3(h, h, -1), new Vector3(h, -h, -1)); // çatı
            m.SetVertices(v);
            m.SetTriangles(tris, 0);
            m.SetColors(new Color[v.Count]);
            m.RecalculateBounds();
            return m;
        }

        static void Paint(Mesh m, Color baseColor)
        {
            var side = Color.Lerp(baseColor, Color.black, 0.45f);
            var front = Color.Lerp(baseColor, Color.black, 0.28f);
            var roof = Color.Lerp(baseColor, Color.white, 0.12f);
            var cols = new Color[16];
            for (int i = 0; i < 4; i++) { cols[i] = side; cols[4 + i] = side; cols[8 + i] = front; cols[12 + i] = roof; }
            // Ön cephenin üst kenarı biraz açık: pencere/ışık hissi
            cols[9] = cols[10] = Color.Lerp(front, roof, 0.3f);
            m.colors = cols;
        }

        int ContractCount()
        {
            int n = 0;
            foreach (var s in CityMap.I.Shops) if (s.contracted) n++;
            return n;
        }

        void Refresh(bool colors)
        {
            var map = CityMap.I;
            if (colors)
            {
                foreach (var p in pieces)
                {
                    var c = p.hq ? CompanyProfile.Primary : p.baseColor;
                    if (p.shop != null && !p.shop.contracted) c = Color.Lerp(c, new Color(0.45f, 0.47f, 0.52f), 0.55f);
                    if (!map.IsUnlocked(p.tileX)) c = Color.Lerp(c, Color.black, 0.65f);
                    Paint(p.mf.mesh, c);
                }
                lastUnlock = map.UnlockLevel;
                lastContracts = ContractCount();
            }
        }

        void LateUpdate()
        {
            if (CityMap.I == null || MapCamera.I == null || pieces.Count == 0) return;
            if (CityMap.I.UnlockLevel != lastUnlock || ContractCount() != lastContracts) Refresh(true);

            float g = MapCamera.I.Tilt01;
            if (Mathf.Abs(g - lastGrow) < 0.001f) return;
            lastGrow = g;
            bool show = g > 0.01f;
            foreach (var p in pieces)
            {
                if (p.t.gameObject.activeSelf != show) p.t.gameObject.SetActive(show);
                if (!show) continue;
                var s = p.t.localScale;
                p.t.localScale = new Vector3(s.x, s.y, Mathf.Max(0.001f, p.height * g));
            }
        }
    }
}
