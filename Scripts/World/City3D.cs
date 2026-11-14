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

        const string UseRoadsKey = "KuryeTycoon.PaketYollari";
        public static bool UseRoads
        {
            get { return PlayerPrefs.GetInt(UseRoadsKey, 1) == 1; }
            set { PlayerPrefs.SetInt(UseRoadsKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Yol parçalarının paketteki yönü bilinmediği için tür başına döndürme düzeltmesi (0: düz, 1: köşe, 2: T).</summary>
        public static int RoadOffset(int kind) => PlayerPrefs.GetInt("KuryeTycoon.YolYonu" + kind, 0);
        public static void SetRoadOffset(int kind, int deg) { PlayerPrefs.SetInt("KuryeTycoon.YolYonu" + kind, ((deg % 360) + 360) % 360); PlayerPrefs.Save(); }

        static GameObject[] packBuildings, packTrees, packRoads;
        // Blender scriptiyle üretilen kendi binalarımız (Tools/Blender/kurye_modeller.py → Kurye Tycoon > Blender Binalarını Prefab Yap)
        static GameObject[] ownHouses, ownTown, ownAll;
        static Dictionary<string, List<GameObject>> ownByKey;
        static readonly string[] OwnPrefixes = { "MustakilEv_", "SiraEv_", "OfisKulesi_", "Market_", "FastFood_", "Apartman_", "Dukkan_",
                                                            "Konut_", "Buro_", "Sanayi_", "Kamu_", "Blok_", "Simge_" };
        /// <summary>Dükkan adı → kendi binasının ad parçası (FastFood_burger_queen_1_1 gibi).</summary>
        static readonly Dictionary<string, string> ShopModel = new Dictionary<string, string>
        {
            { "Burger Queen", "FastFood_burger_queen" }, { "McDanold's", "FastFood_mcdanolds" },
            { "Szpar Express", "Market_szpar_express" }, { "Market 24", "Market_market_24" }, { "InterSzpar", "Market_interszpar" },
            // Altı dükkanlı binalar (Blender: MODEL = "dukkan")
            { "Döner Kral", "Dukkan_doner_kral" }, { "Starbocks", "Dukkan_starbocks" }, { "Pizza Hat", "Dukkan_pizza_hat" },
            { "KFG", "Dukkan_kfg" }, { "Sushi Samurai", "Dukkan_sushi_samurai" }, { "Fish & Ships", "Dukkan_fish_ships" },
            { "Otogar Büfé", "Dukkan_otogar_bufe" }, { "Villa Gourmet", "Dukkan_villa_gourmet" }, { "Non-Stop Gyros", "Dukkan_nonstop_gyros" },
            { "BioSzpar", "Dukkan_bioszpar" }, { "ABC Non-Stop", "Dukkan_abc_nonstop" }, { "Sarok Kisbolt", "Dukkan_sarok_kisbolt" },
            { "Éjjel-Nappali", "Dukkan_ejjel_nappali" }, { "Mini ABC", "Dukkan_mini_abc" }, { "Kikötő Non-Stop", "Dukkan_kikoto_nonstop" },
            { "Falu ABC", "Dukkan_falu_abc" },
        };
        public static int OwnBuildingCount { get { LoadPack(); return ownAll.Length; } }
        // Kendi park parçalarımız (Resources/SehirPaketi/Parklar): park kareleri, göl, anıt, meydan çeşmesi
        static GameObject[] parkTiles, courtTiles, countryExtras;
        /// <summary>Kendi binalarımızın dikey çarpanı: taban kareye sığdığı için binalar basık görünüyordu.</summary>
        const float OwnHeightBoost = 1.18f;
        static GameObject[] ownOld, ownHigh, ownMixed;
        static GameObject[] ownIndustry, ownFarm, ownKamu;   // fabrika/depo/garaj · ahır/sera/yel değirmeni · kamu binaları
        List<GameObject> kamuKalan;                         // bu kurulumda henüz yerleşmemiş kamu binaları (her biri bir kez)
        static GameObject parkPond, parkMonument, plazaFountain;
        static GameObject roadStraight, roadCorner, roadT, roadCross, groundBlock, groundGrass;
        static GameObject roadStraightTrees;   // ağaçlı düz yol (Blender: Yol_lane_agacli)
        // Geniş caddeler (Blender: Yol_cadde_*, Yol_kavsak_ca/cc, Yol_t_XY, Yol_kose_XY) — yoksa normal yol parçaları
        static GameObject avenueStraight, avenueStraightTrees;
        static Dictionary<string, GameObject> avenueJunction;
        // Bitişik blok binaları (Blender: MODEL = "blok"): kat sayısı → modeller
        static Dictionary<int, List<GameObject>> blokDuz, blokKose;
        // Referans (CityRT) binaları: tek tek şehir binaları ve dükkânlar; simge yapılar bir bloğun tamamını kaplar
        static GameObject[] ownRef, ownSimge;
        Dictionary<Vector2Int, GameObject> simgeBlok;   // blok merkezi → o bloğa konan simge yapı
        public static int PackBuildingCount { get { LoadPack(); return packBuildings.Length; } }
        public static int PackTreeCount { get { LoadPack(); return packTrees.Length; } }
        public static bool PackRoadsOk { get { LoadPack(); return roadStraight != null && roadCross != null; } }

        static void LoadPack()
        {
            if (packBuildings != null) return;
            var loaded = Resources.LoadAll<GameObject>(PackFolder + "/Binalar");
            packTrees = Resources.LoadAll<GameObject>(PackFolder + "/Agaclar");
            packRoads = Resources.LoadAll<GameObject>(PackFolder + "/Yollar");

            // Kendi binalarımız paketten ayrı tutulur: genişlik süzgecine girmez, yerine göre seçilir
            var all = new List<GameObject>();
            var own = new List<GameObject>();
            var houses = new List<GameObject>();
            var town = new List<GameObject>();
            ownByKey = new Dictionary<string, List<GameObject>>();
            var industry = new List<GameObject>(); var farm = new List<GameObject>(); var kamu = new List<GameObject>();
            var refs = new List<GameObject>(); var simge = new List<GameObject>();
            blokDuz = new Dictionary<int, List<GameObject>>();
            blokKose = new Dictionary<int, List<GameObject>>();
            foreach (var p in loaded)
            {
                if (!IsOwn(p.name)) { all.Add(p); continue; }
                own.Add(p);
                if (p.name.StartsWith("Simge_")) { simge.Add(p); continue; }
                if (p.name.StartsWith("Konut_ref") || p.name.StartsWith("Dukkan_cadde") || p.name.StartsWith("Buro_klinker")) refs.Add(p);
                if (p.name.StartsWith("Blok_"))
                {
                    // Blok_duz_5k_2 → tür "duz", 5 kat
                    var parca = p.name.Split('_');
                    if (parca.Length >= 3 && int.TryParse(parca[2].TrimEnd('k'), out int kat))
                    {
                        var d = parca[1] == "kose" ? blokKose : blokDuz;
                        if (!d.TryGetValue(kat, out var bl)) d[kat] = bl = new List<GameObject>();
                        bl.Add(p);
                    }
                    continue;
                }
                if (p.name.StartsWith("MustakilEv_") || p.name.StartsWith("Konut_villa") || p.name.StartsWith("Konut_konak") ||
                    p.name.StartsWith("Konut_sale")) houses.Add(p);
                else if (p.name.StartsWith("Apartman_") || p.name.StartsWith("SiraEv_") || p.name.StartsWith("OfisKulesi_") ||
                         p.name.StartsWith("Dukkan_genel") || p.name.StartsWith("Dukkan_cadde") || p.name.StartsWith("Konut_") || p.name.StartsWith("Buro_")) town.Add(p);
                else if (p.name.StartsWith("Sanayi_fabrika") || p.name.StartsWith("Sanayi_depo") || p.name.StartsWith("Sanayi_garaj")) industry.Add(p);
                else if (p.name.StartsWith("Sanayi_")) farm.Add(p);
                else if (p.name.StartsWith("Kamu_")) kamu.Add(p);
                foreach (var kv in ShopModel)
                    if (p.name.StartsWith(kv.Value))
                    {
                        if (!ownByKey.TryGetValue(kv.Key, out var l)) ownByKey[kv.Key] = l = new List<GameObject>();
                        l.Add(p);
                    }
            }
            ownAll = own.ToArray();
            ownHouses = houses.ToArray();
            ownTown = town.ToArray();
            ownIndustry = industry.ToArray();
            ownFarm = farm.ToArray();
            ownKamu = kamu.ToArray();
            ownRef = refs.ToArray();
            ownSimge = simge.ToArray();
            // Bölgeye göre bina havuzları: eski şehirde sıra evler, kule bloklarda ofis kuleleri, çarşıda karışık
            var old = new List<GameObject>(); var high = new List<GameObject>(); var mixed = new List<GameObject>();
            foreach (var p in town)
            {
                bool apt = p.name.StartsWith("Apartman_"), sira = p.name.StartsWith("SiraEv_"), ofis = p.name.StartsWith("OfisKulesi_");
                bool dukkan = p.name.StartsWith("Dukkan_genel") || p.name.StartsWith("Dukkan_cadde");
                bool eskiKonut = p.name.StartsWith("Konut_sira_evler") || p.name.StartsWith("Konut_tugla_apt") || p.name.StartsWith("Konut_avlulu");
                bool yeniBlok = p.name.StartsWith("Konut_balkonlu") || p.name.StartsWith("Buro_");
                if (sira || apt || dukkan || eskiKonut || p.name.StartsWith("Buro_saat")) old.Add(p);
                if (ofis || apt || yeniBlok) high.Add(p);
                mixed.Add(p);
            }
            ownOld = old.Count > 0 ? old.ToArray() : ownTown;
            ownHigh = high.Count > 0 ? high.ToArray() : ownTown;
            ownMixed = mixed.ToArray();

            // Kendi yol parçalarımız (Yol_...) paketinkilerden önce bulunsun; kendi ağaçlarımız varsa sadece onlar
            System.Array.Sort(packRoads, (p, q) => (q.name.StartsWith("Yol_") ? 1 : 0) - (p.name.StartsWith("Yol_") ? 1 : 0));
            var ownTrees = new List<GameObject>();
            foreach (var p in packTrees)
                if (p.name.StartsWith("Agac_") || p.name.StartsWith("Cali_")) ownTrees.Add(p);
            if (ownTrees.Count > 0) packTrees = ownTrees.ToArray();

            var parks = Resources.LoadAll<GameObject>(PackFolder + "/Parklar");
            var tiles = new List<GameObject>();
            var courts = new List<GameObject>();
            var extras = new List<GameObject>();
            parkPond = parkMonument = plazaFountain = null;
            foreach (var p in parks)
            {
                if (p.name.StartsWith("Park_cim")) tiles.Add(p);
                else if (p.name.StartsWith("Avlu_")) courts.Add(p);
                else if (p.name.StartsWith("Kir_")) extras.Add(p);
                else if (p.name == "Park_gol") parkPond = p;
                else if (p.name == "Park_anit") parkMonument = p;
                else if (p.name == "Meydan_cesme") plazaFountain = p;
            }
            parkTiles = tiles.ToArray();
            courtTiles = courts.ToArray();
            countryExtras = extras.ToArray();

            // Binalar klasörüne düşmüş direk, lamba, rüzgar gülü (çok ince) ve hazır şehir blokları (çok geniş) elenir
            var widths = new List<float>();
            foreach (var p in all) widths.Add(Width(p));
            widths.Sort();
            float median = widths.Count > 0 ? widths[widths.Count / 2] : 1f;
            var keep = new List<GameObject>();
            foreach (var p in all)
            {
                float w = Width(p);
                if (w >= median * 0.45f && w <= median * 2.6f) keep.Add(p);
            }
            packBuildings = keep.ToArray();

            roadStraight = FindRoad("lane_01", "lane_02", "road lane", "road tile");
            roadStraightTrees = null;
            foreach (var p in packRoads) if (p != null && p.name == "Yol_lane_agacli") roadStraightTrees = p;
            avenueStraight = avenueStraightTrees = null;
            avenueJunction = new Dictionary<string, GameObject>();
            foreach (var p in packRoads)
            {
                if (p == null) continue;
                if (p.name == "Yol_cadde_01") avenueStraight = p;
                else if (p.name == "Yol_cadde_agacli") avenueStraightTrees = p;
                else if (p.name.StartsWith("Yol_kavsak_") || p.name.StartsWith("Yol_t_") && p.name.Length == 8 || p.name.StartsWith("Yol_kose_"))
                    avenueJunction[p.name] = p;
            }
            roadCorner = FindRoad("corner_01", "corner");
            roadT = FindRoad("t_intersection_01", "t_intersection", "t intersection");
            roadCross = FindRoadExcept("intersection", "t_", "t intersection");
            groundBlock = FindRoad("sidewalk", "concrete");
            groundGrass = FindRoad("grass");
        }

        static GameObject FindRoad(params string[] keys)
        {
            foreach (var k in keys)
                foreach (var p in packRoads)
                    if (p.name.ToLowerInvariant().Contains(k)) return p;
            return null;
        }

        static GameObject FindRoadExcept(string key, params string[] not)
        {
            foreach (var p in packRoads)
            {
                string n = p.name.ToLowerInvariant();
                if (!n.Contains(key)) continue;
                bool bad = false;
                foreach (var x in not) if (n.Contains(x)) bad = true;
                if (!bad) return p;
            }
            return null;
        }

        class Piece
        {
            public Transform t;       // tutucu: yerde durur, z ölçeği yükselme oranı
            public MeshFilter mf;     // kutu binalarda; paket modellerinde null
            public float height;      // tam boy (kare biriminde)
            public bool prefab;
            public float heightMul = 1f;  // paket modellerinde yükselme çarpanı (kendi binalarımız biraz daha uzun: referanstaki gibi dolu silüet)
            public bool flat;         // yol / zemin: yükselmez, sadece görünür olur
            public Color baseColor;
            public Vector2Int tile;
            public Shop shop;
            public bool hq;
            public int varSeed = -1;  // kendi binalarımızda renk çeşitliliği tohumu (−1: yok)
        }

        readonly List<Piece> pieces = new List<Piece>();
        readonly Dictionary<Vector2Int, float> heightByTile = new Dictionary<Vector2Int, float>();
        Transform root;
        /// <summary>3D şehrin kökü (GeceIsiklari pencere ve lambaları buradan bulur).</summary>
        public Transform Root => root;
        /// <summary>Renkler (kilit, bina tonu) her yeniden boyandığında artar: üstüne yazılan gece ışıkları yeniden uygulansın.</summary>
        public static int TintVersion { get; private set; }
        /// <summary>Şehir kurulunca çağrılır (gece ışıkları pencere ve lambaları kaydeder).</summary>
        public static event System.Action Built;

        float nightAmt;
        /// <summary>Gece: kod kutusu binalar (AVM, otogar, merkez ofis, depo kutuları) ışıksız malzemeyle çizildiği için
        /// geceleri bembeyaz parlıyordu; renkleri geceye göre koyulaşır (Gorsel3D çağırır).</summary>
        public void SetNight(float n)
        {
            if (Mathf.Abs(n - nightAmt) < 0.03f && !(n == 0f && nightAmt > 0f)) return;
            nightAmt = n;
            var map = CityMap.I;
            if (map == null) return;
            foreach (var p in pieces)
            {
                if (p.prefab || p.mf == null) continue;
                Paint(p.mf.mesh, BoxColor(p, map));
            }
        }

        Color BoxColor(Piece p, CityMap map)
        {
            var c = p.hq ? CompanyProfile.Primary : p.baseColor;
            if (p.shop != null && !p.shop.contracted) c = Color.Lerp(c, new Color(0.45f, 0.47f, 0.52f), 0.55f);
            if (!map.IsUnlocked(p.tile)) c = CityMap.LockedTint(c);
            if (nightAmt > 0f)
            {
                var m = CityMap.NightMul;
                c = Color.Lerp(c, new Color(c.r * m.r, c.g * m.g, c.b * m.b, c.a), nightAmt * 0.85f);
            }
            return c;
        }
        Material mat;
        float lastGrow = -1f;
        int lastUnlock = -1;
        int lastContracts = -1;

        void Awake() { I = this; ClearTintCache(); }

        /// <summary>Karedeki binanın şu anki boyu (yükselme oranı dahil). Etiketler ve müşteri noktaları çatıya oturur.</summary>
        /// <summary>Binaların tam boyları (kare birimi, yükselme oranı hariç). Gece pencere ışıkları kullanır.</summary>
        public Dictionary<Vector2Int, float> BuildingHeights => heightByTile;

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
            packRoads = null;
            if (root != null) Destroy(root.gameObject);
            pieces.Clear();
            heightByTile.Clear();
            ClearTintCache();
            lastGrow = -1f;
            Build();
            if (Traffic.I != null) Traffic.I.Build();   // trafik de paketin araçlarını kullanır
        }

        public void Build()
        {
            var map = CityMap.I;
            if (map == null) return;
            LoadPack();
            bool pack = UsePack && packBuildings.Length > 0;
            bool own = UsePack && ownAll.Length > 0;
            bool ownPark = UsePack && parkTiles.Length > 0;
            bool packTreesOk = UsePack && packTrees.Length > 0;
            kamuKalan = ownKamu != null ? new List<GameObject>(ownKamu) : new List<GameObject>();
            bool roads3D = UsePack && UseRoads && roadStraight != null && roadCross != null;

            root = new GameObject("3D Şehir").transform;
            root.SetParent(transform, false);

            // Kutu binalar için sprite malzemesi: projenin render pipeline'ına uygun, vertex rengini gösterir
            var probe = SpriteFactory.Create("malzeme", root, false, Color.white, Vector3.zero, 1f, 0);
            mat = probe.sharedMaterial;
            Destroy(probe.gameObject);

            var shopTiles = new Dictionary<Vector2Int, Shop>();
            foreach (var s in map.Shops) shopTiles[s.tile] = s;

            var rng = new System.Random(12345);
            SimgeBloklariniSec(map, shopTiles);
            for (int x = 0; x < CityMap.Width; x++)
            {
                for (int y = 0; y < CityMap.Height; y++)
                {
                    var tile = new Vector2Int(x, y);
                    if (map.IsRoad(x, y))
                    {
                        // Köprüler teknelerin üstünde çizilsin (sortingOrder 3)
                        bool bridge = CityMap.BridgeAt(x, y);
                        if (roads3D) AddRoad(map, tile, bridge ? 3 : 1);
                        if (bridge)
                        {
                            var rail = new Color(0.80f, 0.78f, 0.74f);
                            AddBoxR(tile, new Vector2(0.06f, 1.0f), 0.07f, rail, null, false, new Vector2(-0.46f, 0f), false);
                            AddBoxR(tile, new Vector2(0.06f, 1.0f), 0.07f, rail, null, false, new Vector2(0.46f, 0f), false);
                        }
                        continue;
                    }
                    // Nehir ve tren yolu 2D haritadan görünür (su, raylar); üstlerine bir şey konmaz
                    if (CityMap.WaterAt(x, y) || CityMap.RailAt(x, y)) continue;

                    var zone = CityMap.ZoneOf(tile);
                    // Kır kareleri (şehrin girintili kenarı): dokulu kır zemini görünür, ağaçları kır düzeni koyar (orman, çit)
                    if (zone == Zone.Country) continue;
                    bool frontage = IsAdjacentToRoad(map, x, y);
                    float face = FaceAngle(map, tile);
                    int floors = Addresses.Floors(tile);

                    // Paket yolları açıksa zemin: kaldırım ya da çim (bölgeye göre)
                    if (roads3D)
                    {
                        bool grass = zone == Zone.Park || zone == Zone.Houses || zone == Zone.River ||
                                     (!frontage && (zone == Zone.HighRise || zone == Zone.Mixed || zone == Zone.Campus));
                        var gnd = grass && groundGrass != null ? groundGrass : groundBlock;
                        // Kendi park parçalarımız ve meydan çeşmesi kendi zeminiyle gelir (üst üste binmesin)
                        bool ownGround = (zone == Zone.Park && ownPark) || (tile == CityMap.PlazaCenter && UsePack && plazaFountain != null);
                        if (gnd != null && !ownGround) AddFlat(gnd, tile, 0f);
                    }

                    // Nehir kıyısı (kıvrımlı nehrin suyu olmayan tarafı): çimenli set, seyrek ağaç ve çalı
                    if (zone == Zone.River)
                    {
                        AddTrees(tile, rng, 1 + rng.Next(3), packTreesOk);
                        if (UsePack && countryExtras != null && countryExtras.Length > 0 && rng.NextDouble() < 0.35)
                            AddPrefab(countryExtras[rng.Next(countryExtras.Length)], tile, 0f, (float)rng.NextDouble() * 360f, null,
                                      new Vector2((float)rng.NextDouble() * 0.5f - 0.25f, (float)rng.NextDouble() * 0.5f - 0.25f), 0.06f, false);
                        continue;
                    }

                    if (simgeBlok != null && simgeBlok.Count > 0 && !frontageRoadOnly(map, x, y))
                    {
                        var bm = CityMap.BlockCenterOf(x, y);
                        if (simgeBlok.TryGetValue(bm, out var sp))
                        {
                            if (tile == bm)
                            {
                                // Simge yapı bloğun dokuz karesini kaplar (opera, gar, pazar …), önü güneydeki yola bakar
                                AddPrefab(sp, tile, 2.95f, 180f, null, default, 0f, true, true);
                                float hh = heightByTile.TryGetValue(tile, out var h0) ? h0 : 1f;
                                for (int dx = -1; dx <= 1; dx++)
                                    for (int dy = -1; dy <= 1; dy++) heightByTile[new Vector2Int(x + dx, y + dy)] = hh;
                            }
                            continue;
                        }
                    }

                    if (tile == map.HQTile)
                    {
                        // Merkez ofis her zaman şirket renginde kutu: haritada hemen tanınsın
                        AddBox(tile, 0.84f, 1.9f, CompanyProfile.Primary, null, true);
                        continue;
                    }

                    if (shopTiles.TryGetValue(tile, out var shop))
                    {
                        // AVM'deki dükkanlar AVM binasının içinde: ayrı bina yok
                        if (zone == Zone.Mall) continue;
                        // Dükkan: paket binası biraz küçük (etrafında dükkan renginde zemin görünür) ya da renkli kutu
                        var ownShop = own ? OwnForShop(shop.def.name, rng) : null;
                        if (ownShop != null) AddPrefab(ownShop, tile, OwnFootprint(ownShop, 0.92f), face, shop, default, 0f, true, true);
                        else if (pack) AddPrefab(Pick(packBuildings, rng, 2, true), tile, 0.66f, face, shop);
                        else AddBox(tile, 0.8f, 0.75f, shop.def.color, shop, false);
                        continue;
                    }

                    switch (zone)
                    {
                        case Zone.Plaza:
                            if (tile == CityMap.PlazaCenter && UsePack && plazaFountain != null)
                                AddFlat(plazaFountain, tile, 0f, 2);
                            else if (tile == CityMap.PlazaCenter)
                            {
                                // Meydan çeşmesi ve heykel
                                AddBoxR(tile, new Vector2(0.5f, 0.5f), 0.06f, new Color(0.70f, 0.68f, 0.64f), null, false, new Vector2(0f, 0.2f), false);
                                AddBoxR(tile, new Vector2(0.36f, 0.36f), 0.07f, new Color(0.45f, 0.72f, 0.90f), null, false, new Vector2(0f, 0.2f), false);
                                AddBoxR(tile, new Vector2(0.08f, 0.08f), 0.32f, new Color(0.62f, 0.60f, 0.56f), null, false, new Vector2(0f, 0.2f), false);
                            }
                            break;

                        case Zone.Park:
                            if (ownPark)
                            {
                                if (CityMap.IsBlockCenter(x, y))
                                {
                                    var orta = tile == CityMap.BelediyeCenter ? parkMonument : parkPond;
                                    AddFlat(orta != null ? orta : parkTiles[rng.Next(parkTiles.Length)], tile, 90f * rng.Next(4), 2);
                                }
                                else
                                {
                                    // Park karesi (oyun parkı, çiçek bahçesi, gezinti yolu) ve köşelerinde ağaçlar
                                    AddFlat(parkTiles[rng.Next(parkTiles.Length)], tile, 90f * rng.Next(4), 2);
                                    int nt = 1 + rng.Next(3);
                                    for (int i = 0; i < nt; i++)
                                    {
                                        var kose = new Vector2((i % 2 == 0) ? -0.36f : 0.36f, (i < 2) ? -0.36f : 0.36f);
                                        AddTrees(tile, rng, 1, packTreesOk, kose, 0.04f);
                                    }
                                }
                            }
                            else if (CityMap.IsBlockCenter(x, y))
                            {
                                if (tile == CityMap.BelediyeCenter)   // Belediye Parkı: anıt kule
                                {
                                    AddBoxR(tile, new Vector2(0.34f, 0.34f), 0.08f, new Color(0.78f, 0.76f, 0.70f), null, false, default, false);
                                    AddBoxR(tile, new Vector2(0.16f, 0.16f), 0.95f, new Color(0.86f, 0.84f, 0.78f), null, false, default, false);
                                }
                                else AddBoxR(tile, new Vector2(0.62f, 0.46f), 0.01f, new Color(0.30f, 0.58f, 0.80f), null, false, default, false); // gölet
                            }
                            else AddTrees(tile, rng, 3 + rng.Next(2), packTreesOk);
                            break;

                        case Zone.Mall:
                            if (CityMap.IsBlockCenter(x, y))
                            {
                                float top;
                                if (tile == CityMap.OtogarCenter)
                                {
                                    // Otogar: alçak, geniş peron binası ve önünde otobüsler
                                    AddBoxR(tile, new Vector2(2.7f, 1.2f), 0.35f, CityMap.OtogarColor, null, false, new Vector2(0f, 0.7f), false);
                                    for (int i = 0; i < 4; i++)
                                        AddBoxR(tile, new Vector2(0.28f, 0.85f), 0.2f, new Color(0.95f, 0.78f, 0.20f), null, false, new Vector2(-0.9f + i * 0.6f, -0.5f), false);
                                    top = 0.35f;
                                }
                                else
                                {
                                    // Bloğun tamamını kaplayan büyük AVM binası + cam çatılı orta bölüm
                                    AddBoxR(tile, new Vector2(2.7f, 2.7f), 0.7f, CityMap.MallColor, null, false, default, false);
                                    AddBoxR(tile, new Vector2(1.5f, 0.8f), 0.82f, new Color(0.62f, 0.80f, 0.90f), null, false, default, false);
                                    top = 0.82f;
                                }
                                for (int dx = -1; dx <= 1; dx++)
                                    for (int dy = -1; dy <= 1; dy++) heightByTile[new Vector2Int(x + dx, y + dy)] = top;
                            }
                            break;

                        case Zone.Warehouse:
                            // Fabrikalar, depolar, garajlar (kendi modellerimiz varsa); yoksa geniş, alçak kutular
                            if (own && ownIndustry.Length > 0)
                            {
                                var sb = ownIndustry[rng.Next(ownIndustry.Length)];
                                AddPrefab(sb, tile, OwnFootprint(sb, 0.97f), frontage ? face : 90f * rng.Next(4), null, default, 0f, true, true);
                                break;
                            }
                            AddBox(tile, frontage ? 0.9f : 0.82f, 0.28f + (float)rng.NextDouble() * 0.12f, CityMap.RoofColor(zone, x, y), null, false);
                            break;

                        case Zone.Houses:
                            if (frontage)
                            {
                                // Müstakil ev: yola yakın küçük bina, arkasında bahçe ağacı
                                var front = FrontDir(map, tile) * 0.14f;
                                if (own && ownHouses.Length > 0)
                                {
                                    // Kendi evlerimizin bahçesi ve ağaçları modelin içinde
                                    var h = ownHouses[rng.Next(ownHouses.Length)];
                                    AddPrefab(h, tile, OwnFootprint(h, 0.97f), face, null, default, 0f, true, true);
                                    break;
                                }
                                if (pack) AddPrefab(Pick(packBuildings, rng, floors, false), tile, 0.55f, face, null, front);
                                else AddBox(tile, 0.55f, 0.18f + floors * 0.12f, CityMap.RoofColor(zone, x, y), null, false, front);
                                AddTrees(tile, rng, 1, packTreesOk, -front * 2.2f, 0.12f);
                            }
                            else AddTrees(tile, rng, 2 + rng.Next(2), packTreesOk);
                            break;

                        default: // eski şehir, kule bloklar, çarşı, kampüs
                            if (!frontage)
                            {
                                if (UsePack && courtTiles.Length > 0)
                                {
                                    // İç avlu: otopark (park etmiş arabalar), bahçe, oyun alanı, havuz; bir köşesinde ağaç.
                                    // Referanstaki gibi avluların çoğu dolu otopark ya da yarı otopark yarı bahçe.
                                    var avlu = courtTiles[rng.Next(courtTiles.Length)];
                                    if (avlu.name.Contains("havuz") && rng.NextDouble() < 0.6) avlu = courtTiles[rng.Next(courtTiles.Length)];
                                    AddFlat(avlu, tile, 90f * rng.Next(4), 2);
                                    AddTrees(tile, rng, 1, packTreesOk, new Vector2(rng.Next(2) == 0 ? -0.36f : 0.36f, rng.Next(2) == 0 ? -0.36f : 0.36f), 0.04f);
                                }
                                // İç avlu: eski şehirde az ağaç, diğerlerinde yeşil alan
                                else if (zone == Zone.OldTown) AddTrees(tile, rng, 1, packTreesOk);
                                else AddTrees(tile, rng, 1 + rng.Next(3), packTreesOk);
                            }
                            else if (own && kamuKalan.Count > 0 && (zone == Zone.Mixed || zone == Zone.OldTown) && rng.NextDouble() < 0.12)
                            {
                                // Kamu binaları (polis, itfaiye, hastane, okul, kütüphane, belediye, postane): her biri şehirde bir kez
                                int ki = rng.Next(kamuKalan.Count);
                                var kb = kamuKalan[ki];
                                kamuKalan.RemoveAt(ki);
                                AddPrefab(kb, tile, OwnFootprint(kb, 0.97f), face, null, default, 0f, true, true);
                            }
                            else if (own && ownRef != null && ownRef.Length > 0 && (zone == Zone.OldTown || zone == Zone.Mixed || zone == Zone.HighRise) &&
                                     rng.NextDouble() < 0.28)
                            {
                                // Referans binaları (Haussmann, Altbau, pasajlı bina, pub, fırın, benzinlik …)
                                var rb = ownRef[rng.Next(ownRef.Length)];
                                AddPrefab(rb, tile, OwnFootprint(rb, 0.97f), face, null, default, 0f, true, true);
                            }
                            else if (BlokVar && (zone == Zone.OldTown || zone == Zone.Mixed || zone == Zone.HighRise) &&
                                     !(zone == Zone.HighRise && floors >= 10 && rng.NextDouble() < 0.5) && rng.NextDouble() < 0.88)
                            {
                                // Kapalı blok: bitişik binalar kaldırıma yaslanır, köşede L biçimli köşe binası
                                AddBlok(map, tile, floors, rng);
                            }
                            else if (own && ownTown.Length > 0)
                            {
                                var havuz = zone == Zone.HighRise ? ownHigh : zone == Zone.OldTown ? ownOld : ownMixed;
                                var b = Pick(havuz, rng, floors, false);
                                AddPrefab(b, tile, OwnFootprint(b, 0.97f), face, null, default, 0f, true, true);
                            }
                            else if (pack)
                                AddPrefab(Pick(packBuildings, rng, floors, false), tile, zone == Zone.HighRise ? 0.8f : 0.86f, face, null);
                            else
                                AddBox(tile, zone == Zone.HighRise ? 0.74f : 0.84f, 0.25f + floors * 0.16f, CityMap.RoofColor(zone, x, y), null, false);
                            break;
                    }
                }
            }
            BuildCountryside(map, pack, packTreesOk, rng);
            Refresh(true);
            foreach (var p in pieces) p.t.gameObject.SetActive(false);
            Built?.Invoke();
        }

        /// <summary>
        /// Paketten bina seç: merkezde yüksek binalar daha olası, sanayide alçak/geniş; dükkanlarda alçak binalar.
        /// (Prefabların boyu bilinmediği için yükseklik/genişlik oranına göre ağırlık verilir.)
        /// </summary>
        const float OwnLotDepth = 0.08f;   // Blender'daki arsa levhası kalınlığı (metre; FBX 1 m = 1 birim)
        const float OwnAsphalt = 0.10f;    // Blender'da yol parçalarının asfalt yüzeyi (metre)
        // Kendi yol parçalarımızda kaldırım üstünün dünya yüksekliği: (0,22 − 0,10) m × (1,01 / 10 m) yukarıda (−Z yukarı)
        const float OwnSidewalkTop = -(0.22f - 0.10f) * 0.101f - 0.0025f;

        /// <summary>Kendi düz parçalarımız mı (yol, zemin, park, avlu, meydan)?</summary>
        static bool IsOwnFlat(string name) =>
            name.StartsWith("Yol_") || name.StartsWith("Park_") || name.StartsWith("Avlu_") || name.StartsWith("Meydan_");

        static bool IsOwn(string name)
        {
            foreach (var p in OwnPrefixes) if (name.StartsWith(p)) return true;
            return false;
        }

        /// <summary>Dükkanın kendi binası (Burger Queen → Burger Queen binası). Yoksa null: paket binası ya da kutu kullanılır.</summary>
        static GameObject OwnForShop(string shopName, System.Random rng)
        {
            if (ownByKey == null || !ownByKey.TryGetValue(shopName, out var l) || l.Count == 0) return null;
            return l[rng.Next(l.Count)];
        }

        /// <summary>Kendi binalarımız gerçek boylarıyla oranlı: hipermarket evden geniş, ama komşu kareye taşmaz.</summary>
        static float OwnFootprint(GameObject prefab, float baseFoot)
        {
            const float RefWidth = 14f;   // metre: tipik müstakil ev arsası
            float w = Width(prefab);
            return Mathf.Min(0.99f, baseFoot * Mathf.Clamp(w / RefWidth, 0.95f, 1.3f));
        }

        /// <summary>Karenin bloğu bir simge yapıya ayrıldıysa (yollar hariç) true. Yol karesi değilse çağrılır.</summary>
        static bool frontageRoadOnly(CityMap map, int x, int y) => map.IsRoad(x, y);

        /// <summary>
        /// Simge yapılar için bloklar seç: eski şehir, çarşı ve kule bloklarından içinde yol, su, dükkân, merkez ofis ya da
        /// meydan çeşmesi olmayan 3×3 bloklar. Her simge bir kez, bloklar birbirine bitişik değil.
        /// </summary>
        void SimgeBloklariniSec(CityMap map, Dictionary<Vector2Int, Shop> shopTiles)
        {
            simgeBlok = new Dictionary<Vector2Int, GameObject>();
            if (!UsePack || ownSimge == null || ownSimge.Length == 0) return;
            var aday = new List<Vector2Int>();
            for (int bx = 0; bx * CityMap.Block + 3 < CityMap.Width; bx++)
                for (int by = 0; by * CityMap.Block + 3 < CityMap.Height; by++)
                {
                    var c = new Vector2Int(bx * CityMap.Block + 2, by * CityMap.Block + 2);
                    var z = CityMap.ZoneOf(c);
                    if (z != Zone.OldTown && z != Zone.Mixed && z != Zone.HighRise && z != Zone.Campus) continue;
                    bool ok = true;
                    for (int dx = -1; dx <= 1 && ok; dx++)
                        for (int dy = -1; dy <= 1 && ok; dy++)
                        {
                            var t = new Vector2Int(c.x + dx, c.y + dy);
                            if (map.IsRoad(t.x, t.y) || CityMap.WaterAt(t.x, t.y) || CityMap.RailAt(t.x, t.y) || shopTiles.ContainsKey(t) ||
                                t == map.HQTile || t == CityMap.PlazaCenter || CityMap.ZoneOf(t) != z) ok = false;
                        }
                    if (ok) aday.Add(c);
                }
            var rng = new System.Random(4242);
            var simgeler = new List<GameObject>(ownSimge);
            while (simgeler.Count > 0 && aday.Count > 0)
            {
                var c = aday[rng.Next(aday.Count)];
                aday.Remove(c);
                bool komsu = false;
                foreach (var k in simgeBlok.Keys)
                    if (Mathf.Abs(k.x - c.x) <= CityMap.Block && Mathf.Abs(k.y - c.y) <= CityMap.Block) komsu = true;
                if (komsu) continue;
                int i = rng.Next(simgeler.Count);
                simgeBlok[c] = simgeler[i];
                simgeler.RemoveAt(i);
            }
        }

        static bool BlokVar => UsePack && blokDuz != null && blokDuz.Count > 0;

        /// <summary>İstenen kata en yakın blok modeli (aynı kattan rastgele varyant).</summary>
        static GameObject BlokSec(Dictionary<int, List<GameObject>> d, int floors, System.Random rng)
        {
            if (d == null || d.Count == 0) return null;
            int best = -1, bestFark = int.MaxValue;
            foreach (var k in d.Keys)
            {
                int f = Mathf.Abs(k - floors);
                if (f < bestFark || (f == bestFark && rng.Next(2) == 0)) { bestFark = f; best = k; }
            }
            var l = d[best];
            return l[rng.Next(l.Count)];
        }

        /// <summary>
        /// Blok binası: tek yola bakan kare ara bina, iki dik yola bakan kare köşe binası (L).
        /// Kanonik köşe modeli kuzey ve doğu yollarına bakar (yol köşesiyle aynı dönüş kuralı).
        /// </summary>
        void AddBlok(CityMap map, Vector2Int t, int floors, System.Random rng)
        {
            bool n = t.y < CityMap.Height - 1 && map.IsRoad(t.x, t.y + 1);
            bool s = t.y > 0 && map.IsRoad(t.x, t.y - 1);
            bool e = t.x < CityMap.Width - 1 && map.IsRoad(t.x + 1, t.y);
            bool w = t.x > 0 && map.IsRoad(t.x - 1, t.y);
            bool kose = (n || s) && (e || w) && blokKose != null && blokKose.Count > 0;
            GameObject p;
            float aci;
            if (kose)
            {
                // İki dik yol: güneydeki ve doğudaki yol öncelikli (üç yola bakan karede)
                bool kn = !s && n, ke = e;
                aci = (kn && ke) ? 0f : kn ? 90f : !ke ? 180f : 270f;
                p = BlokSec(blokKose, floors, rng);
            }
            else
            {
                aci = FaceAngle(map, t);
                p = BlokSec(blokDuz, floors, rng);
            }
            AddPrefab(p, t, 1.003f, aci, null, default, 0f, true, true);
        }

        GameObject Pick(GameObject[] list, System.Random rng, int floors, bool shop)
        {
            GameObject best = list[rng.Next(list.Length)];
            float bestScore = float.MinValue;
            for (int i = 0; i < 3; i++)
            {
                var cand = list[rng.Next(list.Length)];
                float tall = Tallness(cand);
                // Kat sayısına uygun boy/en oranı: 1–2 kat basık, 10 kat kule
                float want = Mathf.Max(0.4f, floors * 0.32f);
                float score = -Mathf.Abs(Mathf.Log(Mathf.Max(0.1f, tall) / want)) + (float)rng.NextDouble() * 0.6f;
                if (score > bestScore) { bestScore = score; best = cand; }
            }
            return best;
        }

        static float Tallness(GameObject prefab)
        {
            var sz = PrefabSize(prefab);
            return sz.y / Mathf.Max(0.01f, Mathf.Max(sz.x, sz.z));
        }

        static float Width(GameObject prefab)
        {
            var sz = PrefabSize(prefab);
            return Mathf.Max(sz.x, sz.z);
        }

        static readonly Dictionary<GameObject, Vector3> sizeCache = new Dictionary<GameObject, Vector3>();
        /// <summary>Prefabın kendi ölçeğindeki boyutu (Y yukarı), sahneye koymadan.</summary>
        static Vector3 PrefabSize(GameObject prefab)
        {
            if (sizeCache.TryGetValue(prefab, out var cached)) return cached;
            var b = new Bounds();
            bool any = false;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var s = mf.transform.lossyScale;
                var size = Vector3.Scale(mb.size, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
                var c = mf.transform.TransformPoint(mb.center);
                var bb = new Bounds(c, size);
                if (!any) { b = bb; any = true; } else b.Encapsulate(bb);
            }
            var result = any ? b.size : Vector3.one;
            sizeCache[prefab] = result;
            return result;
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

        /// <summary>
        /// Şehir dışı: ormanlar ve çiftlikler 3D (tarla, çayır, nehir ve kır yolları 2D haritadan görünür).
        /// Parçalar merkez ofisin karesine bağlanır (o kare hep açık): kilitli bölge gibi kararmazlar.
        /// </summary>
        /// <summary>Kır yolları şehir yollarıyla aynı 3D parçalarla döşenir (şehirden ne kadar uzağa).</summary>
        const int KirYoluUzunluk = 30;

        void BuildCountryside(CityMap map, bool pack, bool packTreesOk, System.Random rng)
        {
            KirZeminiKur(map);
            var anchor = map.HQTile;
            if (UsePack && UseRoads && roadStraight != null)
            {
                foreach (var r in CityMap.CountryRoads)
                {
                    bool yatay = Mathf.Approximately(r.a.y, r.b.y);
                    int sabit = Mathf.RoundToInt(yatay ? r.a.y : r.a.x);
                    float bas = yatay ? Mathf.Min(r.a.x, r.b.x) : Mathf.Min(r.a.y, r.b.y);
                    float son = yatay ? Mathf.Max(r.a.x, r.b.x) : Mathf.Max(r.a.y, r.b.y);
                    int lim = yatay ? CityMap.Width : CityMap.Height;
                    for (int k = Mathf.CeilToInt(bas); k <= Mathf.FloorToInt(son); k++)
                    {
                        if (k >= 0 && k < lim) continue;                                   // şehrin içi: zaten yol karesi
                        if ((k < 0 ? -k : k - lim + 1) > KirYoluUzunluk) continue;
                        var t = yatay ? new Vector2Int(k, sabit) : new Vector2Int(sabit, k);
                        // Parçalar merkez ofisin karesine bağlanır: kilitli bölge gibi kararmasın
                        AddFlat(roadStraight, anchor, yatay ? 90f : 0f, 1, new Vector2(t.x - anchor.x, t.y - anchor.y));
                    }
                }
            }
            int sira = 0;
            bool extras = UsePack && countryExtras != null && countryExtras.Length > 0;
            foreach (var t in map.CountryTrees)
            {
                var off = new Vector2(t.x - anchor.x, t.y - anchor.y);
                // Her beş ağaçtan birinin yanında çiçek öbeği ya da kaya (çayır canlı görünsün)
                if (extras && (sira++ % 9) == 0)
                    AddPrefab(countryExtras[rng.Next(countryExtras.Length)], anchor, 0f, (float)rng.NextDouble() * 360f, null,
                              off + new Vector2(0.45f, -0.3f), 0.06f, false);
                if (packTreesOk)
                    AddPrefab(packTrees[rng.Next(packTrees.Length)], anchor, 0f, (float)rng.NextDouble() * 360f, null, off, t.z + 0.15f, false);
                else
                {
                    var c = Color.Lerp(new Color(0.14f, 0.38f, 0.20f), new Color(0.26f, 0.50f, 0.22f), (float)rng.NextDouble());
                    AddBox(anchor, 0.3f, t.z, c, null, false, off, false);
                }
            }
            foreach (var f in map.Farms)
            {
                var off = new Vector2(f.x - anchor.x, f.y - anchor.y);
                if (UsePack && ownFarm != null && ownFarm.Length > 0)
                {
                    // Kendi çiftlik yapılarımız: ahır, sera ya da yel değirmeni; yanında ikinci bir yapı
                    var a = ownFarm[rng.Next(ownFarm.Length)];
                    var b = ownFarm[rng.Next(ownFarm.Length)];
                    AddPrefab(a, anchor, OwnFootprint(a, 0.9f), 180f, null, off, 0f, false, true);
                    if (b != a) AddPrefab(b, anchor, OwnFootprint(b, 0.9f), 90f * rng.Next(4), null, off + new Vector2(1f, 0.2f), 0f, false, true);
                    continue;
                }
                if (pack) AddPrefab(Pick(packBuildings, rng, 1, false), anchor, 0.55f, 180f, null, off, 0f, false);
                else AddBoxR(anchor, new Vector2(0.6f, 0.45f), 0.3f, new Color(0.85f, 0.80f, 0.70f), null, false, off, false);
                // Kırmızı ahır
                AddBoxR(anchor, new Vector2(0.7f, 0.5f), 0.35f, new Color(0.62f, 0.22f, 0.18f), null, false, off + new Vector2(0.9f, 0.2f), false);
            }
        }

        // ---------- Dokulu kır zemini ----------
        // Şehir dışı eskiden ışık almayan düz 2D renk kutularıydı (çayır, tarla): parlak ve düz görünüyordu.
        // Artık tek, ışık ve gölge alan bir zemin: hafif dalgalı çimen, sıra sıra ekilmiş tarlalar, tarla kenarlarında koyu çit izi.
        // Nehir, kır yolları ve kar örtüsü 2D olarak bunun üstünde kalır. Doku bir kez üretilir (kır düzeni tohumlu, hep aynı).

        const float KirPay = 44f;
        static Texture2D kirDoku;
        static Material kirMalzeme;

        void KirZeminiKur(CityMap map)
        {
            float x0 = -KirPay - 0.5f, y0 = -KirPay - 0.5f;
            float w = CityMap.Width + 2f * KirPay, h = CityMap.Height + 2f * KirPay;
            if (kirDoku == null) kirDoku = KirDokusu(map, x0, y0, w, h);
            if (kirMalzeme == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                if (sh == null) return;   // 3D ışık yok: 2D kır zemini kalır
                kirMalzeme = new Material(sh) { name = "KirZemini" };
                kirMalzeme.mainTexture = kirDoku;
                if (kirMalzeme.HasProperty("_BaseMap")) kirMalzeme.SetTexture("_BaseMap", kirDoku);
                if (kirMalzeme.HasProperty("_BaseColor")) kirMalzeme.SetColor("_BaseColor", Color.white);
                if (kirMalzeme.HasProperty("_Smoothness")) kirMalzeme.SetFloat("_Smoothness", 0.04f);
                if (kirMalzeme.HasProperty("_Metallic")) kirMalzeme.SetFloat("_Metallic", 0f);
                if (kirMalzeme.HasProperty("_Cull")) kirMalzeme.SetFloat("_Cull", 0f);   // iki yüz de çizilsin
            }

            var go = new GameObject("Kır zemini (3D)");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(0f, 0f, 0.012f);   // 2D nehir, yollar ve kar örtüsünün hemen altında (−Z yukarı)
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var m = new Mesh { name = "KirZemini" };
            m.vertices = new[] { new Vector3(x0, y0, 0f), new Vector3(x0 + w, y0, 0f), new Vector3(x0 + w, y0 + h, 0f), new Vector3(x0, y0 + h, 0f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };   // (0,0,−1): yukarı
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            mf.sharedMesh = m;
            mr.sharedMaterial = kirMalzeme;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
            mr.sortingOrder = -20;
            map.Kir3DZemin();   // 2D çayır ve tarlalar gizlenir (sadece kar yağınca beyaz örtü olarak görünür)
        }

        static Texture2D KirDokusu(CityMap map, float x0, float y0, float w, float h)
        {
            const int TW = 1536;
            int TH = Mathf.Clamp(Mathf.RoundToInt(TW * h / w), 256, 2048);
            var px = new Color32[TW * TH];
            float sx = w / TW, sy = h / TH;

            // Çimen: iki yumuşak yeşil arasında geniş lekeler, üstüne ince doku
            var cA = new Color(0.40f, 0.53f, 0.27f);
            var cB = new Color(0.51f, 0.62f, 0.33f);
            for (int j = 0; j < TH; j++)
            {
                float wy = y0 + (j + 0.5f) * sy;
                for (int i = 0; i < TW; i++)
                {
                    float wx = x0 + (i + 0.5f) * sx;
                    float n1 = Mathf.PerlinNoise(wx * 0.07f + 31.7f, wy * 0.07f + 11.3f);
                    float n2 = Mathf.PerlinNoise(wx * 0.55f + 5.1f, wy * 0.55f + 77.9f);
                    var c = Color.Lerp(cA, cB, n1) * (0.93f + 0.12f * n2);
                    c.a = 1f;
                    px[j * TW + i] = c;
                }
            }

            // Tarlalar: ekin sıraları (yatay ya da dikey), kenarda koyu çit izi, hafif renk dalgası
            int fi = 0;
            foreach (var f in map.Fields)
            {
                fi++;
                var r = f.r;
                bool yatay = ((fi * 7919) % 2) == 0;
                float aralik = 0.38f + 0.08f * (fi % 3);
                int i0 = Mathf.Max(0, Mathf.FloorToInt((r.xMin - x0) / sx)), i1 = Mathf.Min(TW - 1, Mathf.CeilToInt((r.xMax - x0) / sx));
                int j0 = Mathf.Max(0, Mathf.FloorToInt((r.yMin - y0) / sy)), j1 = Mathf.Min(TH - 1, Mathf.CeilToInt((r.yMax - y0) / sy));
                for (int j = j0; j <= j1; j++)
                {
                    float wy = y0 + (j + 0.5f) * sy;
                    for (int i = i0; i <= i1; i++)
                    {
                        float wx = x0 + (i + 0.5f) * sx;
                        float kenar = Mathf.Min(Mathf.Min(wx - r.xMin, r.xMax - wx), Mathf.Min(wy - r.yMin, r.yMax - wy));
                        if (kenar < 0f) continue;
                        int k = j * TW + i;
                        Color c;
                        if (kenar < 0.09f) c = (Color)px[k] * 0.8f;   // tarla kenarı: koyu çimen/çit izi
                        else
                        {
                            float u = yatay ? wy - r.yMin : wx - r.xMin;
                            float serit = Mathf.Repeat(u / aralik, 1f) < 0.5f ? 0.9f : 1.04f;
                            float n = Mathf.PerlinNoise(wx * 0.35f + fi * 3.3f, wy * 0.35f + fi * 1.7f);
                            c = f.c * (serit * (0.94f + 0.1f * n));
                        }
                        c.a = 1f;
                        px[k] = c;
                    }
                }
            }

            var t = new Texture2D(TW, TH, TextureFormat.RGBA32, true)
            {
                name = "KirZemini", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4
            };
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        /// <summary>Binadan yola doğru birim vektör.</summary>
        static Vector2 FrontDir(CityMap map, Vector2Int t)
        {
            if (t.y > 0 && map.IsRoad(t.x, t.y - 1)) return Vector2.down;
            if (t.y < CityMap.Height - 1 && map.IsRoad(t.x, t.y + 1)) return Vector2.up;
            if (t.x > 0 && map.IsRoad(t.x - 1, t.y)) return Vector2.left;
            if (t.x < CityMap.Width - 1 && map.IsRoad(t.x + 1, t.y)) return Vector2.right;
            return Vector2.zero;
        }

        /// <summary>Kareye ağaç koyar (paketin ağaçları ya da yeşil kutular). center verilirse ağaçlar onun etrafında toplanır.</summary>
        void AddTrees(Vector2Int tile, System.Random rng, int n, bool packTreesOk, Vector2 center = default, float spread = 0.3f)
        {
            for (int i = 0; i < n; i++)
            {
                var off = center + new Vector2((float)rng.NextDouble() * spread * 2f - spread, (float)rng.NextDouble() * spread * 2f - spread);
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
                       Vector2 offset = default, float targetHeight = 0f, bool registerHeight = true, bool ownModel = false)
        {
            if (prefab == null) return;
            var holder = Holder(prefab.name, tile, offset);
            var inst = Instantiate(prefab, holder, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localScale = Vector3.one;
            // Kendi binalarımızın önü hep +Z (Blender'da −Y): paket için olan cephe düzeltmesi uygulanmaz
            float duzeltme = ownModel ? 0f : FrontOffset;
            inst.transform.localRotation = Quaternion.Euler(0f, 0f, faceAngle + duzeltme) * Quaternion.Euler(-90f, 0f, 0f);

            foreach (var col in inst.GetComponentsInChildren<Collider>()) Destroy(col);
            foreach (var rb in inst.GetComponentsInChildren<Rigidbody>()) Destroy(rb);
            foreach (var l in inst.GetComponentsInChildren<Light>()) Destroy(l);
            var renderers = inst.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Destroy(holder.gameObject); return; }
            foreach (var r in renderers)
            {
                // Binalar gölge düşürür ve alır (güneş: Gorsel3D). 2D Renderer'da bu ayarların etkisi yok.
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
                r.sortingOrder = 5;
            }

            var b = CombinedBounds(renderers);
            float horiz = Mathf.Max(b.size.x, b.size.y);
            // Blok binaları tam 10 m kare: ölçek sabit (1 m = 0,1 kare), çizgi kabuğu yüzünden küçülüp aralarında boşluk kalmasın
            float s = targetHeight > 0f ? targetHeight / Mathf.Max(0.001f, b.size.z)
                    : prefab.name.StartsWith("Blok_") ? footprint * 0.1f : footprint / Mathf.Max(0.001f, horiz);
            inst.transform.localScale = Vector3.one * s;
            b = CombinedBounds(renderers);
            inst.transform.position += new Vector3(holder.position.x - b.center.x, holder.position.y - b.center.y, -b.max.z);
            float height = b.size.z;
            // Kendi binalarımızın arsa levhasının üstü, yol kaldırımıyla aynı seviyede olsun (bina ne gömük ne havada)
            if (ownModel) inst.transform.position += new Vector3(0f, 0f, OwnLotDepth * s + OwnSidewalkTop);
            // Aynı model her karede biraz farklı renkte (duvar, kapı, panjur, çatı): şehir tekrar eden kopyalar gibi görünmesin
            int seed = ownModel ? ((tile.x + 101) * 73856093 ^ (tile.y + 37) * 19349663 ^ (int)(offset.x * 1000f)) & 0x7fffffff : -1;

            float mul = ownModel && targetHeight <= 0f ? OwnHeightBoost : 1f;
            pieces.Add(new Piece { t = holder, height = height * mul, prefab = true, tile = tile, shop = shop, varSeed = seed, heightMul = mul });
            if (targetHeight <= 0f && registerHeight) heightByTile[tile] = height * mul;
            Tint(renderers, !CityMap.I.IsUnlocked(tile), seed);
        }

        // ---------- Renk: ortak malzeme kopyaları (performans) ----------
        // Eskiden her binanın her malzemesine ayrı "property block" veriliyordu. Property block'lu parçalar SRP Batcher'a
        // giremez: binlerce parça tek tek çiziliyordu (gölge geçişlerinde bir kez daha). Artık aynı renge boyanan bütün
        // parçalar aynı malzeme kopyasını paylaşır; renk çeşitliliği 16 tohumla sınırlı, kopya sayısı az kalır.
        const int VaryCount = 16;
        static readonly Dictionary<(Material, int), Material> tintCache = new Dictionary<(Material, int), Material>();
        static readonly Dictionary<Renderer, Material[]> origMats = new Dictionary<Renderer, Material[]>();

        /// <summary>Parçanın özgün (gündüz, boyanmamış) malzemeleri. Gece ışıkları ve boyama aynı listeden başlar.</summary>
        static Material[] Originals(Renderer r)
        {
            if (!origMats.TryGetValue(r, out var o)) { o = r.sharedMaterials; origMats[r] = o; }
            return o;
        }

        /// <summary>Malzemenin belirli renkteki ortak kopyası (anahtar: özgün malzeme + tohum/karartma).</summary>
        static Material Tinted(Material m, int key, Color c)
        {
            if (tintCache.TryGetValue((m, key), out var t) && t != null) return t;
            t = new Material(m) { name = m.name };   // ad aynı kalır: gece ışıkları ve vurgu ada bakar
            if (t.HasProperty("_BaseColor")) t.SetColor("_BaseColor", c);
            if (t.HasProperty("_Color")) t.SetColor("_Color", c);
            tintCache[(m, key)] = t;
            return t;
        }

        static void ClearTintCache()
        {
            foreach (var t in tintCache.Values) if (t != null) Destroy(t);
            tintCache.Clear();
            origMats.Clear();
        }

        static void TintVaried(Renderer[] rs, bool dark, int seed)
        {
            int vs = seed % VaryCount;
            foreach (var r in rs)
            {
                r.SetPropertyBlock(null);
                var orig = Originals(r);
                var mats = new Material[orig.Length];
                for (int i = 0; i < orig.Length; i++)
                {
                    var m = orig[i];
                    mats[i] = m;
                    if (m == null || m.name.StartsWith("Cizgi")) continue;   // siyah çizgi hep siyah
                    Color c0 = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
                    Color c = Vary(c0, m.name, vs);
                    bool varied = c != c0;
                    if (!varied && !dark) continue;                           // rengi değişmeyen: özgün malzeme
                    if (dark) c = CityMap.LockedTint(c);
                    mats[i] = Tinted(m, (varied ? vs + 1 : 0) + (dark ? 100 : 0), c);
                }
                r.sharedMaterials = mats;
            }
        }

        static float Rand01(int seed, int k) => ((seed * 73856093 ^ k * 19349663 ^ 0x5bd1e995) & 0xffff) / 65535f;

        /// <summary>Malzeme adına göre renk oynatma: duvar/kapı/panjur belirgin, çatı/baca/taban hafif, diğerleri aynı.</summary>
        static Color Vary(Color c, string name, int seed)
        {
            float hue, sat, val;
            int k = name.GetHashCode() & 0xff;
            if (name.StartsWith("Duvar") || name.StartsWith("Kapi") || name.StartsWith("Panjur")) { hue = 0.10f; sat = 0.30f; val = 0.12f; }
            else if (name.StartsWith("Cati") || name.StartsWith("Baca") || name.StartsWith("Taban") || name.StartsWith("Suslu")) { hue = 0.03f; sat = 0.15f; val = 0.08f; }
            else return c;
            Color.RGBToHSV(c, out float h, out float sv, out float v);
            h = Mathf.Repeat(h + (Rand01(seed, k + 1) - 0.5f) * hue, 1f);
            sv = Mathf.Clamp01(sv * (1f + (Rand01(seed, k + 2) - 0.5f) * 2f * sat));
            v = Mathf.Clamp01(v * (1f + (Rand01(seed, k + 3) - 0.5f) * 2f * val));
            var o = Color.HSVToRGB(h, sv, v);
            o.a = c.a;
            return o;
        }

        static Bounds CombinedBounds(Renderer[] rs)
        {
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>
        /// Kilitli bölgedeki paket binalarını karart (malzemeyi bozmadan, property block ile).
        /// seed ≥ 0 ise (kendi binalarımız) her malzemeye ayrı renk verilir: duvar, kapı, panjur, çatı tonu karede kareye değişir.
        /// </summary>
        static void Tint(Renderer[] rs, bool dark, int seed = -1)
        {
            if (seed >= 0) { TintVaried(rs, dark, seed); return; }
            foreach (var r in rs)
            {
                r.SetPropertyBlock(null);
                var orig = Originals(r);
                if (!dark) { r.sharedMaterials = orig; continue; } // malzemenin kendi rengi
                // Kilitli bölge: her malzeme kendi renginin soluk hâli (düz gri değil)
                var mats = new Material[orig.Length];
                for (int i = 0; i < orig.Length; i++)
                {
                    var m = orig[i];
                    mats[i] = m;
                    if (m == null || m.name.StartsWith("Cizgi") || m.name.StartsWith("IsikHavuz")) continue;
                    var c = CityMap.LockedTint(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color);
                    mats[i] = Tinted(m, 100, c);
                }
                r.sharedMaterials = mats;
            }
        }

        /// <summary>Grafik kalitesi: Düşük'te yol ve zemin parçaları gölge düşürmez (gölge geçişindeki çizim sayısı azalır).</summary>
        public void ApplyQuality()
        {
            var mode = GrafikKalitesi.ZeminGolgesi ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (var p in pieces)
            {
                if (!p.flat || p.t == null) continue;
                foreach (var r in p.t.GetComponentsInChildren<Renderer>(true))
                    if (r.shadowCastingMode != mode) r.shadowCastingMode = mode;
            }
        }

        // ---------- Yollar ve zemin ----------

        /// <summary>Ana cadde çizgileri: her ikinci blok yolu (dikeyde x = 8, 16 …; yatayda y = 4, 12 …), harita kenarı hariç.</summary>
        public static bool AvenueColumn(int x) => x % (CityMap.Block * 2) == 0 && x > 0 && x < CityMap.Width - 1;
        public static bool AvenueRow(int y) => y % (CityMap.Block * 2) == CityMap.Block && y > 0 && y < CityMap.Height - 1;
        bool AvenuesOn => avenueStraight != null && avenueJunction != null && avenueJunction.Count >= 8;

        /// <summary>Yön (0 kuzey, 1 batı, 2 güney, 3 doğu) kolunda cadde mi? Kuzey-güney kolu sütuna, doğu-batı satıra bağlı.</summary>
        static bool ArmAvenue(Vector2Int t, int dir) => (dir % 2 == 0) ? AvenueColumn(t.x) : AvenueRow(t.y);

        GameObject Junction(string ad) => avenueJunction != null && avenueJunction.TryGetValue(ad, out var p) ? p : null;

        /// <summary>Komşu yollara göre parça seçer: düz, köşe, T, dörtlü kavşak (ana caddelerde geniş sürümleri).</summary>
        void AddRoad(CityMap map, Vector2Int t, int order = 1)
        {
            // Harita kenarındaki yol karesi: şehir dışına devam eden kır yolu da komşu sayılır (kavşak doğru seçilsin)
            bool n = t.y < CityMap.Height - 1 ? map.IsRoad(t.x, t.y + 1) : CityMap.CountryRoadAt(t.x, t.y + 1);
            bool s = t.y > 0 ? map.IsRoad(t.x, t.y - 1) : CityMap.CountryRoadAt(t.x, t.y - 1);
            bool e = t.x < CityMap.Width - 1 ? map.IsRoad(t.x + 1, t.y) : CityMap.CountryRoadAt(t.x + 1, t.y);
            bool w = t.x > 0 ? map.IsRoad(t.x - 1, t.y) : CityMap.CountryRoadAt(t.x - 1, t.y);
            int count = (n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (w ? 1 : 0);
            bool av = AvenuesOn;
            // Açılar Z ekseninde, saat yönünün tersine: 0 = kuzey, 90 = batı, 180 = güney, 270 = doğu.
            // k adım (90°) döndürünce kanonik yön d dünyada (d + k) % 4 olur.
            if (count >= 4)
            {
                bool cv = ArmAvenue(t, 0), ch = ArmAvenue(t, 1);
                if (av && cv && ch && Junction("Yol_kavsak_cc") != null) { AddFlat(Junction("Yol_kavsak_cc"), t, 0f, order); return; }
                if (av && (cv || ch) && Junction("Yol_kavsak_ca") != null) { AddFlat(Junction("Yol_kavsak_ca"), t, cv ? 0f : 90f, order); return; }
                AddFlat(roadCross, t, 0f, order);
                return;
            }
            if (count == 3 && roadT != null)
            {
                // Kanonik T: batı-kuzey-doğu (güney yok)
                int k = !s ? 0 : !e ? 1 : !n ? 2 : 3;
                if (av)
                {
                    bool bar = ArmAvenue(t, (1 + k) % 4), govde = ArmAvenue(t, k % 4);
                    var jp = (bar || govde) ? Junction("Yol_t_" + (bar ? "c" : "s") + (govde ? "c" : "s")) : null;
                    if (jp != null) { AddFlat(jp, t, k * 90f, order); return; }
                }
                AddFlat(roadT, t, k * 90f + RoadOffset(2), order);
                return;
            }
            if (count == 2 && !(n && s) && !(e && w) && roadCorner != null)
            {
                // Kanonik köşe: kuzey-doğu
                int k = (n && e) ? 0 : (n && w) ? 1 : (s && w) ? 2 : 3;
                if (av)
                {
                    bool ku = ArmAvenue(t, k % 4), do_ = ArmAvenue(t, (3 + k) % 4);
                    var jp = (ku || do_) ? Junction("Yol_kose_" + (ku ? "c" : "s") + (do_ ? "c" : "s")) : null;
                    if (jp != null) { AddFlat(jp, t, k * 90f, order); return; }
                }
                AddFlat(roadCorner, t, k * 90f + RoadOffset(1), order);
                return;
            }
            if (count >= 3) { AddFlat(roadCross, t, 0f, order); return; }
            // Düz: kanonik kuzey-güney
            float st = (e || w) && !(n || s) ? 90f : 0f;
            bool cadde = st == 0f ? AvenueColumn(t.x) : AvenueRow(t.y);
            if (av && cadde)
            {
                // Ana cadde: geniş, çoğu ağaçlı (referanstaki bulvarlar)
                var c = avenueStraightTrees != null && ((t.x * 7 + t.y * 13) % 5) != 0 ? avenueStraightTrees : avenueStraight;
                AddFlat(c, t, st, order);
                return;
            }
            var straight = roadStraight;
            if (roadStraightTrees != null && CityMap.InCity(t.x, t.y))
            {
                // Caddeler yoksa eski davranış (ana yollar ağaçlı); varsa sokakların küçük bir kısmı ağaçlı
                bool avenue = st == 0f ? (t.x / CityMap.Block) % 2 == 0 : (t.y / CityMap.Block) % 2 == 1;
                if (!av && avenue && ((t.x * 7 + t.y * 13) % 5) != 0) straight = roadStraightTrees;
                else if (av && ((t.x * 11 + t.y * 5) % 4) == 0) straight = roadStraightTrees;
            }
            AddFlat(straight, t, st + RoadOffset(0), order);
        }

        /// <summary>Düz parça: kareyi tam kaplar, yüzeyi zeminin hemen üstünde, yükselmez.</summary>
        void AddFlat(GameObject prefab, Vector2Int tile, float angle, int order = 1, Vector2 offset = default)
        {
            if (prefab == null) return;
            var holder = Holder(prefab.name, tile, offset);
            var inst = Instantiate(prefab, holder, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localScale = Vector3.one;
            inst.transform.localRotation = Quaternion.Euler(0f, 0f, angle) * Quaternion.Euler(-90f, 0f, 0f);
            foreach (var col in inst.GetComponentsInChildren<Collider>()) Destroy(col);
            var renderers = inst.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Destroy(holder.gameObject); return; }
            foreach (var r in renderers)
            {
                // Yol ve park parçaları binaların gölgesini alır; üstlerindeki lamba, ağaç, bank da gölge düşürür
                r.shadowCastingMode = GrafikKalitesi.ZeminGolgesi ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = true;
                r.sortingOrder = order; // zemin karelerinin üstünde, binaların altında
            }
            var b = CombinedBounds(renderers);
            float boyut = Mathf.Max(b.size.x, b.size.y);
            // Kendi yol ve park parçalarımız 10 m karedir; kenardan taşan trafik ışığı, lamba ya da ışık halkası
            // parçanın sınırını büyütüp onu küçültmesin (yoksa yollar arasında boşluk kalıyor)
            if (IsOwnFlat(prefab.name) && boyut > 9.5f && boyut < 12f) boyut = 10f;
            float s = 1.01f / Mathf.Max(0.001f, boyut);
            inst.transform.localScale = Vector3.one * s;
            b = CombinedBounds(renderers);
            // Paket parçası: en üst yüzey zeminin hemen üstünde. Kendi parçalarımızda (lamba, trafik ışığı, ağaç var)
            // en üst nokta lambanın tepesi olur; onu hizalamak yolu zeminin altına gömüyordu. Onlarda asfalt yüzeyi (0,10 m) hizalanır.
            float z = IsOwnFlat(prefab.name) ? -b.max.z + OwnAsphalt * s - 0.002f : -0.002f - b.min.z;
            inst.transform.position += new Vector3(holder.position.x - b.center.x, holder.position.y - b.center.y, z);
            pieces.Add(new Piece { t = holder, prefab = true, flat = true, tile = tile });
            Tint(renderers, !CityMap.I.IsUnlocked(tile));
        }

        // ---------- Kutu bina (paket yoksa) ----------

        void AddBox(Vector2Int tile, float size, float height, Color c, Shop shop, bool hq, Vector2 offset = default, bool registerHeight = true)
            => AddBoxR(tile, new Vector2(size, size), height, c, shop, hq, offset, registerHeight);

        /// <summary>Dikdörtgen tabanlı kutu (köprü korkuluğu, AVM, çeşme için).</summary>
        void AddBoxR(Vector2Int tile, Vector2 size, float height, Color c, Shop shop, bool hq, Vector2 offset = default, bool registerHeight = true)
        {
            var holder = Holder(shop != null ? shop.def.name + " (3D)" : hq ? "Merkez Ofis (3D)" : "Bina", tile, offset);
            holder.localScale = new Vector3(size.x, size.y, 0.001f);
            var mf = holder.gameObject.AddComponent<MeshFilter>();
            var mr = holder.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingOrder = 5; // zemin ve dükkan karelerinin üstünde, kurye ve müşterilerin altında
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mf.sharedMesh = BoxMesh();
            pieces.Add(new Piece { t = holder, mf = mf, height = height, baseColor = c, tile = tile, shop = shop, hq = hq });
            if (registerHeight) heightByTile[tile] = height;
        }

        /// <summary>Birim kutu: taban z=0, çatı z=−1. Görünen yüzler (yanlar, güney cephe, çatı); renk vertex renginde.</summary>
        public static Mesh BoxMesh()
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
            // Sıra önemli (sprite malzemesi derinlik yazmaz): önce arka, yanlar, sonra ön cephe, en son çatı
            Quad(new Vector3(-h, h, 0), new Vector3(h, h, 0), new Vector3(h, h, -1), new Vector3(-h, h, -1));     // kuzey (arka)
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

        public static void Paint(Mesh m, Color baseColor)
        {
            var side = Color.Lerp(baseColor, Color.black, 0.45f);
            var front = Color.Lerp(baseColor, Color.black, 0.28f);
            var roof = Color.Lerp(baseColor, Color.white, 0.12f);
            var cols = new Color[20];
            for (int i = 0; i < 4; i++) { cols[i] = side; cols[4 + i] = side; cols[8 + i] = side; cols[12 + i] = front; cols[16 + i] = roof; }
            cols[13] = cols[14] = Color.Lerp(front, roof, 0.3f);
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
                    Tint(p.t.GetComponentsInChildren<Renderer>(true), !map.IsUnlocked(p.tile), p.varSeed);
                    continue;
                }
                Paint(p.mf.mesh, BoxColor(p, map));
            }
            lastUnlock = map.UnlockLevel;
            lastContracts = ContractCount();
            TintVersion++;
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
                if (p.flat) continue;
                p.t.localScale = new Vector3(s.x, s.y, Mathf.Max(0.001f, (p.prefab ? p.heightMul : p.height) * g));
            }
        }
    }
}
