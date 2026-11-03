using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Yakınlaştırınca yerden yükselen 3D şehir.
    ///  • Şehir paketi kuruluysa (ör. SimplePoly City → Kurye Tycoon > Şehir Paketini Kur) paketin binaları ve ağaçları kullanılır.
    ///  • Kurulu değilse basit kutu binalar çizilir.
    /// Harita XY düzleminde; "yukarı" −Z (kameraya doğru). Binalar yola bakacak şekilde döndürülür.
    /// Paket modelleri repoya konmaz (Asset Store lisansı): Assets/Resources/SehirPaketi altına kopyalanır.
    /// </summary>
    public class City3D : MonoBehaviour
    {
        public static City3D I { get; private set; }

        public const string PackFolder = "SehirPaketi";
        const string FrontKey = "KuryeTycoon.BinaCephesi";
        const string UsePackKey = "KuryeTycoon.SehirPaketiKullan";

        /// <summary>Paketteki binaların ön cephesi farklı yöne bakıyorsa düzeltme (0/90/180/270).</summary>
        public static int FrontOffset
        {
            get { return PlayerPrefs.GetInt(FrontKey, 0); }
            set { PlayerPrefs.SetInt(FrontKey, ((value % 360) + 360) % 360); PlayerPrefs.Save(); }
        }

        public static bool UsePack
        {
            get { return PlayerPrefs.GetInt(UsePackKey, 1) == 1; }
            set { PlayerPrefs.SetInt(UsePackKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        static GameObject[] packBuildings, packTrees;
        public static int PackBuildingCount { get { LoadPack(); return packBuildings.Length; } }
        public static int PackTreeCount { get { LoadPack(); return packTrees.Length; } }

        static void LoadPack()
        {
            if (packBuildings != null) return;
            packBuildings = Resources.LoadAll<GameObject>(PackFolder + "/Binalar");
            packTrees = Resources.LoadAll<GameObject>(PackFolder + "/Agaclar");
        }

        class Piece
        {
            public Transform t;       // tutucu: yerde durur, z ölçeği yükselme oranı
            public MeshFilter mf;     // kutu binalarda; paket modellerinde null
            public float height;      // tam boy (kare biriminde)
            public bool prefab;
            public Color baseColor;
            public int tileX;
            public Shop shop;
            public bool hq;
        }

        readonly List<Piece> pieces = new List<Piece>();
        readonly Dictionary<Vector2Int, float> heightByTile = new Dictionary<Vector2Int, float>();
        Transform root;
        Material mat;
        float lastGrow = -1f;
        int lastUnlock = -1;
        int lastContracts = -1;

        void Awake() { I = this; }

        /// <summary>Karedeki binanın şu anki boyu (yükselme oranı dahil). Etiketler ve müşteri noktaları çatıya oturur.</summary>
        public float HeightAt(Vector2Int tile)
        {
            float g = MapCamera.I != null ? MapCamera.I.Tilt01 : 0f;
            return heightByTile.TryGetValue(tile, out float h) ? h * g : 0f;
        }

        /// <summary>Ayar değişince şehri yeniden kurar.</summary>
        public void Rebuild()
        {
            packBuildings = null;
            packTrees = null;
            if (root != null) Destroy(root.gameObject);
            pieces.Clear();
            heightByTile.Clear();
            lastGrow = -1f;
            Build();
        }

        public void Build()
        {
            var map = CityMap.I;
            if (map == null) return;
            LoadPack();
            bool pack = UsePack && packBuildings.Length > 0;
            bool packTreesOk = UsePack && packTrees.Length > 0;

            root = new GameObject("3D Şehir").transform;
            root.SetParent(transform, false);

            // Kutu binalar için sprite malzemesi: projenin render pipeline'ına uygun, vertex rengini gösterir
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
                    int region = x <= CityMap.UnlockMaxX[1] ? 1 : (x <= CityMap.UnlockMaxX[2] ? 2 : 3);
                    float face = FaceAngle(map, tile);

                    if (tile == map.HQTile)
                    {
                        // Merkez ofis her zaman şirket renginde kutu: haritada hemen tanınsın
                        AddBox(tile, 0.84f, 1.9f, CompanyProfile.Primary, null, true);
                    }
                    else if (shopTiles.TryGetValue(tile, out var shop))
                    {
                        // Dükkan: paket binası biraz küçük (etrafında dükkan renginde zemin görünür) ya da renkli kutu
                        if (pack) AddPrefab(Pick(packBuildings, rng, region, true), tile, 0.66f, face, shop);
                        else AddBox(tile, 0.8f, 0.75f, shop.def.color, shop, false);
                    }
                    else if (!frontage)
                    {
                        int n = 1 + rng.Next(3);
                        for (int i = 0; i < n; i++)
                        {
                            var off = new Vector2((float)rng.NextDouble() * 0.6f - 0.3f, (float)rng.NextDouble() * 0.6f - 0.3f);
                            float h = 0.25f + (float)rng.NextDouble() * 0.25f;
                            if (packTreesOk)
                                AddPrefab(packTrees[rng.Next(packTrees.Length)], tile, 0f, (float)rng.NextDouble() * 360f, null, off, h + 0.15f);
                            else
                            {
                                var c = Color.Lerp(new Color(0.16f, 0.42f, 0.22f), new Color(0.28f, 0.55f, 0.25f), (float)rng.NextDouble());
                                AddBox(tile, 0.22f, h, c, null, false, off, false);
                            }
                        }
                    }
                    else if (pack)
                    {
                        AddPrefab(Pick(packBuildings, rng, region, false), tile, region == 3 ? 0.9f : 0.84f, face, null);
                    }
                    else
                    {
                        float h, size = 0.82f;
                        if (region == 1) h = 0.6f + (float)rng.NextDouble() * 1.2f;
                        else if (region == 2) h = 0.5f + (float)rng.NextDouble() * 0.8f;
                        else { h = 0.4f + (float)rng.NextDouble() * 0.4f; size = 0.88f; }
                        var c = Color.Lerp(new Color(0.42f, 0.45f, 0.52f), new Color(0.58f, 0.55f, 0.50f), (float)rng.NextDouble());
                        AddBox(tile, size, h, c, null, false);
                    }
                }
            }
            Refresh(true);
            foreach (var p in pieces) p.t.gameObject.SetActive(false);
        }

        /// <summary>
        /// Paketten bina seç: merkezde yüksek binalar daha olası, sanayide alçak/geniş; dükkanlarda alçak binalar.
        /// (Prefabların boyu bilinmediği için yükseklik/genişlik oranına göre ağırlık verilir.)
        /// </summary>
        GameObject Pick(GameObject[] list, System.Random rng, int region, bool shop)
        {
            GameObject best = list[rng.Next(list.Length)];
            float bestScore = float.MinValue;
            for (int i = 0; i < 3; i++)
            {
                var cand = list[rng.Next(list.Length)];
                float tall = Tallness(cand);
                float want = shop ? 0.8f : region == 1 ? 2.5f : region == 2 ? 1.4f : 0.7f;
                float score = -Mathf.Abs(Mathf.Log(Mathf.Max(0.1f, tall) / want)) + (float)rng.NextDouble() * 0.6f;
                if (score > bestScore) { bestScore = score; best = cand; }
            }
            return best;
        }

        static readonly Dictionary<GameObject, float> tallCache = new Dictionary<GameObject, float>();
        static float Tallness(GameObject prefab)
        {
            if (tallCache.TryGetValue(prefab, out float t)) return t;
            var b = new Bounds();
            bool any = false;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var s = mf.transform.lossyScale;
                var size = Vector3.Scale(mb.size, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
                var c = mf.transform.position;
                var bb = new Bounds(c, size);
                if (!any) { b = bb; any = true; } else b.Encapsulate(bb);
            }
            t = any ? b.size.y / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z)) : 1f;
            tallCache[prefab] = t;
            return t;
        }

        static bool IsAdjacentToRoad(CityMap map, int x, int y) =>
            (x > 0 && map.IsRoad(x - 1, y)) || (x < CityMap.Width - 1 && map.IsRoad(x + 1, y)) ||
            (y > 0 && map.IsRoad(x, y - 1)) || (y < CityMap.Height - 1 && map.IsRoad(x, y + 1));

        /// <summary>Binanın önü yola baksın (güneydeki yol öncelikli: kamera güneyden baktığı için cephe görünür).</summary>
        static float FaceAngle(CityMap map, Vector2Int t)
        {
            if (t.y > 0 && map.IsRoad(t.x, t.y - 1)) return 180f;
            if (t.x > 0 && map.IsRoad(t.x - 1, t.y)) return 90f;
            if (t.x < CityMap.Width - 1 && map.IsRoad(t.x + 1, t.y)) return 270f;
            return 0f;
        }

        Transform Holder(string name, Vector2Int tile, Vector2 offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(tile.x + offset.x, tile.y + offset.y, 0f);
            return go.transform;
        }

        // ---------- Paket modeli ----------

        /// <summary>Prefabı kareye sığdırır (footprint) ya da verilen boya ölçekler, tabanı yere oturtur, yüzünü döndürür.</summary>
        void AddPrefab(GameObject prefab, Vector2Int tile, float footprint, float faceAngle, Shop shop,
                       Vector2 offset = default, float targetHeight = 0f)
        {
            if (prefab == null) return;
            var holder = Holder(prefab.name, tile, offset);
            var inst = Instantiate(prefab, holder, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localScale = Vector3.one;
            inst.transform.localRotation = Quaternion.Euler(0f, 0f, faceAngle + FrontOffset) * Quaternion.Euler(-90f, 0f, 0f);

            foreach (var col in inst.GetComponentsInChildren<Collider>()) Destroy(col);
            foreach (var rb in inst.GetComponentsInChildren<Rigidbody>()) Destroy(rb);
            foreach (var l in inst.GetComponentsInChildren<Light>()) Destroy(l);
            var renderers = inst.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Destroy(holder.gameObject); return; }
            foreach (var r in renderers)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.sortingOrder = 5;
            }

            var b = CombinedBounds(renderers);
            float horiz = Mathf.Max(b.size.x, b.size.y);
            float s = targetHeight > 0f ? targetHeight / Mathf.Max(0.001f, b.size.z) : footprint / Mathf.Max(0.001f, horiz);
            inst.transform.localScale = Vector3.one * s;
            b = CombinedBounds(renderers);
            inst.transform.position += new Vector3(holder.position.x - b.center.x, holder.position.y - b.center.y, -b.max.z);
            float height = b.size.z;

            pieces.Add(new Piece { t = holder, height = height, prefab = true, tileX = tile.x, shop = shop });
            if (targetHeight <= 0f) heightByTile[tile] = height;
            Tint(renderers, !CityMap.I.IsUnlocked(tile.x));
        }

        static Bounds CombinedBounds(Renderer[] rs)
        {
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>Kilitli bölgedeki paket binalarını karart (malzemeyi bozmadan, property block ile).</summary>
        static void Tint(Renderer[] rs, bool dark)
        {
            foreach (var r in rs)
            {
                if (!dark) { r.SetPropertyBlock(null); continue; } // malzemenin kendi rengi
                var mpb = new MaterialPropertyBlock();
                var c = new Color(0.3f, 0.3f, 0.33f);
                mpb.SetColor("_BaseColor", c);
                mpb.SetColor("_Color", c);
                r.SetPropertyBlock(mpb);
            }
        }

        // ---------- Kutu bina (paket yoksa) ----------

        void AddBox(Vector2Int tile, float size, float height, Color c, Shop shop, bool hq, Vector2 offset = default, bool registerHeight = true)
        {
            var holder = Holder(shop != null ? shop.def.name + " (3D)" : hq ? "Merkez Ofis (3D)" : "Bina", tile, offset);
            holder.localScale = new Vector3(size, size, 0.001f);
            var mf = holder.gameObject.AddComponent<MeshFilter>();
            var mr = holder.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingOrder = 5; // zemin ve dükkan karelerinin üstünde, kurye ve müşterilerin altında
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mf.sharedMesh = BoxMesh();
            pieces.Add(new Piece { t = holder, mf = mf, height = height, baseColor = c, tileX = tile.x, shop = shop, hq = hq });
            if (registerHeight) heightByTile[tile] = height;
        }

        /// <summary>Birim kutu: taban z=0, çatı z=−1. Görünen yüzler (yanlar, güney cephe, çatı); renk vertex renginde.</summary>
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
            // Sıra önemli (sprite malzemesi derinlik yazmaz): önce yanlar, sonra ön cephe, en son çatı
            Quad(new Vector3(-h, -h, 0), new Vector3(-h, h, 0), new Vector3(-h, h, -1), new Vector3(-h, -h, -1));
            Quad(new Vector3(h, -h, 0), new Vector3(h, -h, -1), new Vector3(h, h, -1), new Vector3(h, h, 0));
            Quad(new Vector3(-h, -h, 0), new Vector3(-h, -h, -1), new Vector3(h, -h, -1), new Vector3(h, -h, 0));
            Quad(new Vector3(-h, -h, -1), new Vector3(-h, h, -1), new Vector3(h, h, -1), new Vector3(h, -h, -1));
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
            if (!colors) return;
            foreach (var p in pieces)
            {
                if (p.prefab)
                {
                    Tint(p.t.GetComponentsInChildren<Renderer>(true), !map.IsUnlocked(p.tileX));
                    continue;
                }
                var c = p.hq ? CompanyProfile.Primary : p.baseColor;
                if (p.shop != null && !p.shop.contracted) c = Color.Lerp(c, new Color(0.45f, 0.47f, 0.52f), 0.55f);
                if (!map.IsUnlocked(p.tileX)) c = Color.Lerp(c, Color.black, 0.65f);
                Paint(p.mf.mesh, c);
            }
            lastUnlock = map.UnlockLevel;
            lastContracts = ContractCount();
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
                // Kutu: birim kutu boyu kadar uzatılır. Paket modeli: zaten doğru boyda, sadece yükselme oranı.
                p.t.localScale = new Vector3(s.x, s.y, Mathf.Max(0.001f, (p.prefab ? 1f : p.height) * g));
            }
        }
    }
}
