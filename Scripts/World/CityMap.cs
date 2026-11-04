using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Bir yapı adasının (iki yol arasındaki 3x3 karelik blok) türü.</summary>
    public enum Zone { OldTown, HighRise, Houses, Mixed, Mall, Campus, Warehouse, Park, Plaza, River }

    /// <summary>Haritada adı yazan yer (park, AVM, nehir, bölge).</summary>
    public struct Landmark
    {
        public string name;
        public Vector2 pos;
        public Color color;
        public Landmark(string name, Vector2 pos, Color color) { this.name = name; this.pos = pos; this.color = color; }
    }

    /// <summary>
    /// Izgara şehir: her 4 karede bir yol. Bölgeler para ile açılır. Bir kare = yaklaşık 200 metre.
    ///  • Şehrin ortasından (y = 9–11) nehir geçer; karşıya yalnızca 4 köprüden (x = 4, 12, 20, 28) geçilir.
    ///  • Her blok bir türdedir: eski şehir, kule bloklar, bahçeli evler, çarşı, AVM, kampüs, sanayi, park, meydan.
    ///  • Merkez ofis Merkez Mahalle'nin ortasındaki meydanda.
    ///  • Sanayi bölgesinden tren yolu geçer; nehirde tekneler, rayda tren dolaşır (süs).
    /// Düzen sabittir (rastgele değil): adresler, kat sayıları ve 3D şehir her oyunda aynıdır.
    /// </summary>
    public class CityMap : MonoBehaviour
    {
        public static CityMap I { get; private set; }

        public const int Width = 33;
        public const int Height = 21;
        public const int Block = 4;

        // seviye → açık olan en büyük x
        public static readonly int[] UnlockMaxX = { 0, 12, 24, 32 };
        public static readonly long[] UnlockCost = { 0, 0, 150000, 400000 };
        public static readonly string[] DistrictNames = { "", "Merkez Mahalle", "Doğu İlçesi", "Sanayi & Kampüs" };

        // ---------- Şehir düzeni ----------

        public const int RiverBlockY = 2;                       // nehir: y = 9, 10, 11
        public static readonly int[] BridgeX = { 4, 12, 20, 28 };
        public const int RailY = 18;                            // tren yolu (sanayi)
        public const int RailFromX = 25;
        public const float RiverY = RiverBlockY * Block + Block / 2f;   // nehrin ortası (10)

        /// <summary>Blok türleri [bx, by]: bx = x / 4 (0–7, soldan sağa), by = y / 4 (0–4, aşağıdan yukarı).</summary>
        static readonly Zone[,] BlockZones =
        {
            // by:   0 (güney)       1               2 (nehir)     3                 4 (kuzey)
            { Zone.OldTown,  Zone.OldTown,  Zone.River, Zone.HighRise,  Zone.HighRise },  // bx 0  Merkez
            { Zone.OldTown,  Zone.Plaza,    Zone.River, Zone.HighRise,  Zone.HighRise },  // bx 1  Merkez (meydan + HQ)
            { Zone.OldTown,  Zone.Park,     Zone.River, Zone.HighRise,  Zone.Mixed },     // bx 2  Merkez (Belediye Parkı)
            { Zone.Houses,   Zone.Mixed,    Zone.River, Zone.Mixed,     Zone.HighRise },  // bx 3  Doğu
            { Zone.Houses,   Zone.Houses,   Zone.River, Zone.Park,      Zone.Mixed },     // bx 4  Doğu (Nehir Parkı)
            { Zone.Houses,   Zone.Houses,   Zone.River, Zone.Mixed,     Zone.Mixed },     // bx 5  Doğu
            { Zone.Mall,     Zone.Mixed,    Zone.River, Zone.Warehouse, Zone.Warehouse }, // bx 6  Sanayi (Tepe AVM)
            { Zone.Campus,   Zone.Mall,     Zone.River, Zone.Warehouse, Zone.Warehouse }, // bx 7  Sanayi (Nyír Plaza AVM)
        };

        public static readonly Landmark[] Landmarks =
        {
            new Landmark("Eski Şehir",         new Vector2(2f, 2f),   new Color(1f, 0.86f, 0.70f)),
            new Landmark("Kossuth Meydanı",    new Vector2(6f, 7.15f), new Color(1f, 0.92f, 0.75f)),
            new Landmark("Belediye Parkı",     new Vector2(10f, 6f),  new Color(0.70f, 1f, 0.70f)),
            new Landmark("Tisza",              new Vector2(8f, RiverY), new Color(0.65f, 0.85f, 1f)),
            new Landmark("Tisza",              new Vector2(24f, RiverY), new Color(0.65f, 0.85f, 1f)),
            new Landmark("Kule Bloklar",       new Vector2(6f, 18f),  new Color(1f, 0.80f, 0.85f)),
            new Landmark("Bahçeli Evler",      new Vector2(18f, 2f),  new Color(0.85f, 1f, 0.80f)),
            new Landmark("Nehir Parkı",        new Vector2(18f, 14f), new Color(0.70f, 1f, 0.70f)),
            new Landmark("Tepe AVM",           new Vector2(26f, 2f),  new Color(1f, 0.85f, 0.60f)),
            new Landmark("Nyír Plaza AVM",     new Vector2(30f, 6f),  new Color(1f, 0.85f, 0.60f)),
            new Landmark("Kampüs",             new Vector2(30f, 2f),  new Color(1f, 0.95f, 0.70f)),
            new Landmark("Sanayi & Lojistik",  new Vector2(28f, 16f), new Color(0.80f, 0.92f, 0.90f)),
        };

        static bool InRiverRows(int y) => y > RiverBlockY * Block && y < (RiverBlockY + 1) * Block;
        static bool IsBridgeX(int x) => System.Array.IndexOf(BridgeX, x) >= 0;

        /// <summary>Yol mu? (Nehrin içinde sadece köprüler yoldur.)</summary>
        public static bool RoadAt(int x, int y)
        {
            if (x % Block != 0 && y % Block != 0) return false;
            if (InRiverRows(y)) return IsBridgeX(x);
            return true;
        }

        public static bool WaterAt(int x, int y) => InRiverRows(y) && !RoadAt(x, y);
        public static bool BridgeAt(int x, int y) => InRiverRows(y) && RoadAt(x, y);
        public static bool RailAt(int x, int y) => y == RailY && x >= RailFromX && x < Width && !RoadAt(x, y);
        /// <summary>Tren yolunun yolu kestiği hemzemin geçit.</summary>
        public static bool RailCrossingAt(int x, int y) => y == RailY && x >= RailFromX && RoadAt(x, y);

        public static Zone ZoneOf(Vector2Int t) => ZoneOf(t.x, t.y);
        public static Zone ZoneOf(int x, int y)
        {
            if (InRiverRows(y)) return Zone.River;
            int bx = Mathf.Clamp(x / Block, 0, BlockZones.GetLength(0) - 1);
            int by = Mathf.Clamp(y / Block, 0, BlockZones.GetLength(1) - 1);
            return BlockZones[bx, by];
        }

        /// <summary>Kare, bloğunun ortasındaki kare mi (yola değmeyen iç kare)?</summary>
        public static bool IsBlockCenter(int x, int y) => x % Block == 2 && y % Block == 2;
        public static Vector2Int BlockCenterOf(int x, int y) => new Vector2Int(x / Block * Block + 2, y / Block * Block + 2);

        static bool CustomerZone(Zone z) =>
            z == Zone.OldTown || z == Zone.HighRise || z == Zone.Houses || z == Zone.Mixed || z == Zone.Campus || z == Zone.Warehouse;
        static bool PreferredShopZone(Zone z) => z == Zone.Mixed || z == Zone.Mall || z == Zone.OldTown;
        static bool ShopZone(Zone z) => PreferredShopZone(z) || z == Zone.HighRise || z == Zone.Campus || z == Zone.Houses;

        public static string ZoneName(Zone z)
        {
            switch (z)
            {
                case Zone.OldTown: return "Eski şehir";
                case Zone.HighRise: return "Kule bloklar";
                case Zone.Houses: return "Bahçeli evler";
                case Zone.Mixed: return "Çarşı";
                case Zone.Mall: return "AVM";
                case Zone.Campus: return "Kampüs";
                case Zone.Warehouse: return "Sanayi";
                case Zone.Park: return "Park";
                case Zone.Plaza: return "Meydan";
                default: return "Nehir";
            }
        }

        // ---------- Renkler (2D harita) ----------

        public static readonly Color RoadColor = new Color(0.25f, 0.26f, 0.30f);
        public static readonly Color BridgeColor = new Color(0.42f, 0.40f, 0.38f);
        public static readonly Color WaterColor = new Color(0.22f, 0.50f, 0.72f);
        public static readonly Color GrassColor = new Color(0.33f, 0.58f, 0.32f);
        public static readonly Color PlazaColor = new Color(0.80f, 0.74f, 0.63f);
        public static readonly Color SidewalkColor = new Color(0.60f, 0.60f, 0.58f);
        public static readonly Color YardColor = new Color(0.47f, 0.47f, 0.48f);
        public static readonly Color RailBedColor = new Color(0.42f, 0.36f, 0.32f);
        public static readonly Color MallColor = new Color(0.88f, 0.76f, 0.72f);

        static readonly Color[] OldTownRoofs = { new Color(0.66f, 0.34f, 0.26f), new Color(0.56f, 0.40f, 0.30f), new Color(0.36f, 0.50f, 0.42f), new Color(0.70f, 0.46f, 0.32f) };
        static readonly Color[] HighRiseRoofs = { new Color(0.80f, 0.60f, 0.62f), new Color(0.72f, 0.56f, 0.64f), new Color(0.84f, 0.68f, 0.62f) };
        static readonly Color[] HouseRoofs = { new Color(0.68f, 0.36f, 0.36f), new Color(0.56f, 0.32f, 0.30f), new Color(0.44f, 0.50f, 0.64f), new Color(0.42f, 0.56f, 0.50f) };
        static readonly Color[] MixedRoofs = { new Color(0.56f, 0.58f, 0.66f), new Color(0.64f, 0.57f, 0.50f), new Color(0.50f, 0.56f, 0.58f) };
        static readonly Color[] CampusRoofs = { new Color(0.82f, 0.75f, 0.60f), new Color(0.76f, 0.70f, 0.58f) };
        static readonly Color[] WarehouseRoofs = { new Color(0.48f, 0.64f, 0.60f), new Color(0.58f, 0.62f, 0.66f), new Color(0.52f, 0.58f, 0.54f) };

        static int Hash(int x, int y) => (x * 73856093 ^ y * 19349663 ^ 0x2f6b) & 0x7fffffff;

        /// <summary>Bölgenin çatı rengi (2D harita, 3D kutu binalar ve lejant aynı paleti kullanır).</summary>
        public static Color RoofColor(Zone z, int x, int y)
        {
            Color[] p;
            switch (z)
            {
                case Zone.OldTown: p = OldTownRoofs; break;
                case Zone.HighRise: p = HighRiseRoofs; break;
                case Zone.Houses: p = HouseRoofs; break;
                case Zone.Campus: p = CampusRoofs; break;
                case Zone.Warehouse: p = WarehouseRoofs; break;
                case Zone.Mall: return MallColor;
                case Zone.Park: return GrassColor;
                case Zone.Plaza: return PlazaColor;
                case Zone.River: return WaterColor;
                default: p = MixedRoofs; break;
            }
            return p[Hash(x, y) % p.Length];
        }

        /// <summary>Lejantta gösterilen renk.</summary>
        public static Color LegendColor(Zone z) => RoofColor(z, 0, 0);

        /// <summary>Karenin zemin rengi (çatının etrafında görünen kaldırım / bahçe / su).</summary>
        public static Color GroundColor(int x, int y)
        {
            if (BridgeAt(x, y)) return BridgeColor;
            if (RoadAt(x, y)) return RoadColor;
            if (WaterAt(x, y)) return WaterColor;
            if (RailAt(x, y)) return RailBedColor;
            switch (ZoneOf(x, y))
            {
                case Zone.Park:
                case Zone.Houses: return GrassColor;
                case Zone.Plaza: return PlazaColor;
                case Zone.Warehouse: return YardColor;
                case Zone.Mall: return SidewalkColor;
                case Zone.OldTown: return IsFrontage(x, y) ? SidewalkColor : Color.Lerp(PlazaColor, SidewalkColor, 0.5f);
                default: return IsFrontage(x, y) ? SidewalkColor : GrassColor;
            }
        }

        /// <summary>Kuşbakışı karenin baskın rengi (telefon haritası için).</summary>
        public Color MiniColor(int x, int y)
        {
            if (RoadAt(x, y) || WaterAt(x, y) || RailAt(x, y)) return GroundColor(x, y);
            var z = ZoneOf(x, y);
            if (z == Zone.Park || z == Zone.Plaza || z == Zone.Mall) return RoofColor(z, x, y);
            return HasRoof(x, y) ? RoofColor(z, x, y) : GroundColor(x, y);
        }

        static bool IsFrontage(int x, int y)
        {
            return (x > 0 && RoadAt(x - 1, y)) || (x < Width - 1 && RoadAt(x + 1, y)) ||
                   (y > 0 && RoadAt(x, y - 1)) || (y < Height - 1 && RoadAt(x, y + 1));
        }

        /// <summary>Bu karede çatısı görünen bir bina var mı (2D harita)?</summary>
        static bool HasRoof(int x, int y)
        {
            if (RoadAt(x, y) || WaterAt(x, y) || RailAt(x, y)) return false;
            var z = ZoneOf(x, y);
            if (z == Zone.Park || z == Zone.Plaza || z == Zone.Mall) return false;
            if (z == Zone.Warehouse) return true;          // sanayide iç kareler de depo
            return IsFrontage(x, y);
        }

        // ---------- Durum ----------

        public int UnlockLevel { get; private set; } = 1;
        public int MaxLevel => 3;

        public readonly List<Shop> Shops = new List<Shop>();
        public Vector2Int HQTile { get; private set; }
        public Vector2Int HQDoor { get; private set; }

        static readonly Vector2Int[] Dirs = { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up };

        SpriteRenderer[,] ground;
        Color[,] groundBase;
        readonly List<(SpriteRenderer sr, Color c, int x)> decor = new List<(SpriteRenderer, Color, int)>();
        readonly List<Vector2Int> waterTiles = new List<Vector2Int>();
        SpriteRenderer hq;
        Transform root, roofRoot;
        readonly List<Vector2Int> customerTiles = new List<Vector2Int>();
        readonly List<Vector2Int> shopTiles = new List<Vector2Int>();
        readonly HashSet<Vector2Int> reserved = new HashSet<Vector2Int>();

        // Tekneler ve tren
        class Mover { public Transform t; public float speed; public float x; public float y; public int dir; public float wait; }
        readonly List<Mover> boats = new List<Mover>();
        Mover train;

        void Awake() { I = this; }

        public bool IsRoad(int x, int y) => RoadAt(x, y);
        public bool InBounds(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < Width && p.y < Height;
        public bool IsUnlocked(int x) => x <= UnlockMaxX[UnlockLevel];
        public static Vector3 ToWorld(Vector2 t) => new Vector3(t.x, t.y, 0f);
        public static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        static int RegionOf(int x) => x <= UnlockMaxX[1] ? 1 : (x <= UnlockMaxX[2] ? 2 : 3);

        public void Generate()
        {
            root = new GameObject("Şehir").transform;
            root.SetParent(transform, false);
            roofRoot = new GameObject("Çatılar (2D)").transform;
            roofRoot.SetParent(root, false);
            ground = new SpriteRenderer[Width, Height];
            groundBase = new Color[Width, Height];

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var c = GroundColor(x, y);
                    groundBase[x, y] = c;
                    // Köprüler teknelerin üstünde çizilsin (tekne altından geçer)
                    int order = BridgeAt(x, y) ? 2 : 0;
                    ground[x, y] = SpriteFactory.Create($"Kare {x},{y}", root, false, c, ToWorld(new Vector2(x, y)), 1.01f, order);
                    if (WaterAt(x, y)) waterTiles.Add(new Vector2Int(x, y));

                    var t = new Vector2Int(x, y);
                    if (RoadAt(x, y) || WaterAt(x, y) || RailAt(x, y)) continue;
                    var z = ZoneOf(x, y);
                    if (!IsFrontage(x, y)) continue;
                    if (CustomerZone(z)) customerTiles.Add(t);
                    if (ShopZone(z)) shopTiles.Add(t);
                }
            }

            BuildDecor();

            // Merkez ofis: Merkez Mahalle'nin ortasındaki meydanda, güneydeki yola bakar
            HQTile = new Vector2Int(6, 5);
            HQDoor = GetDoor(HQTile);
            for (int x = 5; x <= 7; x++)
                for (int y = 5; y <= 7; y++) reserved.Add(new Vector2Int(x, y));
            hq = SpriteFactory.Create("Merkez Ofis", root, false, CompanyProfile.Primary, ToWorld(HQTile), 0.9f, 2);

            PlaceShops();
            BuildMovers();
            RefreshColors();
        }

        // ---------- 2D süsler: çatılar, ağaçlar, çeşme, raylar, köprü korkulukları ----------

        SpriteRenderer Deco(string name, Transform parent, bool round, Color c, Vector2 pos, Vector2 scale, int order)
        {
            var sr = SpriteFactory.Create(name, parent, round, c, new Vector3(pos.x, pos.y, 0f), 1f, order);
            sr.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            decor.Add((sr, c, Mathf.RoundToInt(pos.x)));
            return sr;
        }

        void BuildDecor()
        {
            var rng = new System.Random(777);
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var p = new Vector2(x, y);
                    var z = ZoneOf(x, y);

                    if (BridgeAt(x, y))
                    {
                        var rail = new Color(0.82f, 0.80f, 0.76f);
                        Deco("Korkuluk", root, false, rail, p + new Vector2(-0.46f, 0f), new Vector2(0.06f, 1.01f), 3);
                        Deco("Korkuluk", root, false, rail, p + new Vector2(0.46f, 0f), new Vector2(0.06f, 1.01f), 3);
                        continue;
                    }
                    if (RailAt(x, y) || RailCrossingAt(x, y))
                    {
                        var steel = new Color(0.20f, 0.20f, 0.22f);
                        for (int i = 0; i < 4; i++)
                            Deco("Travers", root, false, new Color(0.30f, 0.24f, 0.20f), p + new Vector2(-0.375f + i * 0.25f, 0f), new Vector2(0.08f, 0.42f), 1);
                        Deco("Ray", root, false, steel, p + new Vector2(0f, 0.12f), new Vector2(1.01f, 0.04f), 1);
                        Deco("Ray", root, false, steel, p + new Vector2(0f, -0.12f), new Vector2(1.01f, 0.04f), 1);
                        continue;
                    }
                    if (RoadAt(x, y) || WaterAt(x, y)) continue;

                    if (HasRoof(x, y))
                    {
                        var roof = RoofColor(z, x, y);
                        float s = z == Zone.Houses ? 0.56f : z == Zone.Warehouse ? 0.9f : 0.8f;
                        Vector2 off = z == Zone.Houses ? FrontOffset(x, y) * 0.14f : Vector2.zero;
                        Deco("Çatı", roofRoot, false, roof, p + off, new Vector2(s, s), 1);
                        // Çatı sırtı: bir ton açık şerit, binalar düz kareden ayırt edilsin
                        Deco("Çatı sırtı", roofRoot, false, Color.Lerp(roof, Color.white, 0.18f), p + off, new Vector2(s * 0.92f, s * 0.18f), 1);
                    }
                    else if (z == Zone.Park || z == Zone.Houses || (!IsFrontage(x, y) && z != Zone.Plaza && z != Zone.Mall && z != Zone.OldTown))
                    {
                        // Ağaçlar (park daha sık)
                        int n = z == Zone.Park ? 3 + rng.Next(2) : 1 + rng.Next(2);
                        if (z == Zone.Park && IsBlockCenter(x, y)) n = 0;
                        for (int i = 0; i < n; i++)
                        {
                            var off = new Vector2((float)rng.NextDouble() * 0.7f - 0.35f, (float)rng.NextDouble() * 0.7f - 0.35f);
                            float s = 0.22f + (float)rng.NextDouble() * 0.12f;
                            var tc = Color.Lerp(new Color(0.16f, 0.40f, 0.20f), new Color(0.26f, 0.50f, 0.22f), (float)rng.NextDouble());
                            Deco("Ağaç", roofRoot, true, tc, p + off, new Vector2(s, s), 1);
                        }
                        if (z == Zone.Park && IsBlockCenter(x, y))
                        {
                            // Park ortası: gölet ya da anıt
                            bool belediye = x / Block == 2;
                            if (belediye) Deco("Anıt", roofRoot, false, new Color(0.85f, 0.83f, 0.78f), p, new Vector2(0.22f, 0.22f), 1);
                            else Deco("Gölet", roofRoot, true, WaterColor, p, new Vector2(0.6f, 0.45f), 1);
                        }
                    }
                }
            }

            // Meydanın ortasında çeşme
            Deco("Çeşme", roofRoot, true, new Color(0.70f, 0.68f, 0.64f), new Vector2(6f, 6.2f), new Vector2(0.5f, 0.5f), 1);
            Deco("Çeşme suyu", roofRoot, true, new Color(0.45f, 0.72f, 0.90f), new Vector2(6f, 6.2f), new Vector2(0.34f, 0.34f), 1);

            // AVM'ler: bloğun tamamını kaplayan tek büyük çatı + cam çatı şeridi
            foreach (var c in new[] { new Vector2Int(26, 2), new Vector2Int(30, 6) })
            {
                Deco("AVM", roofRoot, false, MallColor, c, new Vector2(2.6f, 2.6f), 1);
                Deco("AVM cam çatı", roofRoot, false, new Color(0.62f, 0.80f, 0.90f), c, new Vector2(1.6f, 0.7f), 1);
            }
        }

        /// <summary>Bina karesinden yola doğru birim vektör (bahçeli evler yola yakın durur, arkası bahçe).</summary>
        static Vector2 FrontOffset(int x, int y)
        {
            if (y > 0 && RoadAt(x, y - 1)) return Vector2.down;
            if (y < Height - 1 && RoadAt(x, y + 1)) return Vector2.up;
            if (x > 0 && RoadAt(x - 1, y)) return Vector2.left;
            if (x < Width - 1 && RoadAt(x + 1, y)) return Vector2.right;
            return Vector2.zero;
        }

        void BuildMovers()
        {
            // Nehirde üç tekne (iki yönde)
            var boatDefs = new[] { (x: 3f, dir: 1, y: RiverY - 0.35f), (x: 20f, dir: 1, y: RiverY - 0.35f), (x: 12f, dir: -1, y: RiverY + 0.35f) };
            foreach (var b in boatDefs)
            {
                var t = new GameObject("Tekne").transform;
                t.SetParent(root, false);
                var hull = SpriteFactory.Create("Gövde", t, false, new Color(0.95f, 0.95f, 0.93f), Vector3.zero, 1f, 1);
                hull.transform.localScale = new Vector3(0.75f, 0.22f, 1f);
                var cabin = SpriteFactory.Create("Kabin", t, false, new Color(0.30f, 0.42f, 0.58f), new Vector3(-0.08f, 0f, 0f), 1f, 1);
                cabin.transform.localScale = new Vector3(0.3f, 0.14f, 1f);
                boats.Add(new Mover { t = t, speed = 0.35f + boats.Count * 0.07f, x = b.x, y = b.y, dir = b.dir });
            }

            // Sanayi tren yolunda yük treni
            var tr = new GameObject("Tren").transform;
            tr.SetParent(root, false);
            var colors = new[] { new Color(0.78f, 0.22f, 0.18f), new Color(0.45f, 0.48f, 0.52f), new Color(0.55f, 0.40f, 0.28f), new Color(0.45f, 0.48f, 0.52f) };
            for (int i = 0; i < colors.Length; i++)
            {
                var car = SpriteFactory.Create("Vagon", tr, false, colors[i], new Vector3(-i * 0.62f, 0f, 0f), 1f, 1);
                car.transform.localScale = new Vector3(0.56f, 0.28f, 1f);
            }
            train = new Mover { t = tr, speed = 0.9f, x = RailFromX - 1f, y = RailY, dir = 1 };
        }

        float shimmerTimer;

        void Update()
        {
            bool paused = GameClock.I == null || GameClock.I.Paused;
            float dt = paused ? 0f : Time.deltaTime;

            foreach (var b in boats)
            {
                b.x += b.dir * b.speed * dt;
                if (b.x > Width + 1.5f) b.x = -1.5f;
                if (b.x < -1.5f) b.x = Width + 1.5f;
                b.t.localPosition = new Vector3(b.x, b.y, 0f);
                b.t.localScale = new Vector3(b.dir, 1f, 1f);
            }

            if (train != null)
            {
                if (train.wait > 0f) train.wait -= dt;
                else
                {
                    train.x += train.speed * dt;
                    if (train.x > Width + 3f) { train.x = RailFromX - 1f; train.wait = 12f; }
                }
                // Raylar Sanayi'de başlar: istasyondan çıkana kadar görünmesin
                train.t.gameObject.SetActive(train.wait <= 0f);
                train.t.localPosition = new Vector3(train.x, train.y, 0f);
            }

            // Su yüzeyi hafifçe dalgalansın
            shimmerTimer += Time.deltaTime;
            if (shimmerTimer > 0.12f && ground != null)
            {
                shimmerTimer = 0f;
                float tm = Time.time;
                foreach (var w in waterTiles)
                {
                    float k = 0.5f + 0.5f * Mathf.Sin(tm * 0.9f + w.x * 0.7f + w.y * 1.3f);
                    var c = Color.Lerp(WaterColor, Color.Lerp(WaterColor, Color.white, 0.12f), k * 0.6f);
                    if (!IsUnlocked(w.x)) c = Color.Lerp(c, Color.black, 0.55f);
                    ground[w.x, w.y].color = c;
                }
            }
        }

        void LateUpdate()
        {
            // Yakınlaşınca 3D binalar çıkar: 2D çatılar ve ağaçlar onların altında kalıp titremesin
            if (roofRoot == null) return;
            bool flat = MapCamera.I == null || MapCamera.I.Tilt01 < 0.01f || City3D.I == null;
            if (roofRoot.gameObject.activeSelf != flat) roofRoot.gameObject.SetActive(flat);
        }

        public Vector2Int GetDoor(Vector2Int building)
        {
            foreach (var d in Dirs)
            {
                var n = building + d;
                if (InBounds(n) && IsRoad(n.x, n.y)) return n;
            }
            return building;
        }

        void PlaceShops()
        {
            foreach (var def in GameData.Shops)
            {
                var preferred = new List<Vector2Int>();
                var options = new List<Vector2Int>();
                foreach (var t in shopTiles)
                {
                    if (RegionOf(t.x) != def.unlockLevel || reserved.Contains(t)) continue;
                    options.Add(t);
                    if (PreferredShopZone(ZoneOf(t))) preferred.Add(t);
                }
                var list = preferred.Count > 0 ? preferred : options;
                if (list.Count == 0) continue;

                var tile = list[Random.Range(0, list.Count)];
                reserved.Add(tile);
                var shop = new Shop
                {
                    def = def,
                    tile = tile,
                    door = GetDoor(tile),
                    contracted = def.contractCost == 0
                };
                shop.sr = SpriteFactory.Create(def.name, root, false, def.color, ToWorld(tile), 0.8f, 2);
                Shops.Add(shop);
            }
        }

        public void RefreshColors()
        {
            if (ground == null) return;
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    var c = groundBase[x, y];
                    if (!IsUnlocked(x)) c = Color.Lerp(c, Color.black, 0.6f);
                    ground[x, y].color = c;
                }

            foreach (var d in decor)
            {
                if (d.sr == null) continue;
                d.sr.color = IsUnlocked(d.x) ? d.c : Color.Lerp(d.c, Color.black, 0.6f);
            }

            foreach (var s in Shops)
            {
                Color c = s.def.color;
                if (!IsUnlocked(s.tile.x)) c = Color.Lerp(c, Color.black, 0.75f);
                else if (!s.contracted) c = Color.Lerp(c, new Color(0.55f, 0.56f, 0.60f), 0.6f);
                s.sr.color = c;
            }

            if (hq != null) hq.color = CompanyProfile.Primary;
        }

        /// <summary>Kayıttan yüklerken açık bölge seviyesini geri koyar.</summary>
        public void RestoreUnlockLevel(int level)
        {
            UnlockLevel = Mathf.Clamp(level, 1, MaxLevel);
            RefreshColors();
        }

        public bool TryUnlockNext()
        {
            if (UnlockLevel >= MaxLevel) return false;
            long cost = UnlockCost[UnlockLevel + 1];
            if (!EconomyManager.I.TrySpend(cost, $"Yeni bölge: {DistrictNames[UnlockLevel + 1]}", ExpenseCategory.Bolge)) return false;
            UnlockLevel++;
            RefreshColors();
            EventManager.I.Notify($"{DistrictNames[UnlockLevel]} açıldı! Yeni dükkanlarla sözleşme yapabilirsin.");
            return true;
        }

        public bool SignContract(Shop s)
        {
            if (s.contracted || !IsUnlocked(s.tile.x)) return false;
            if (GameClock.I.Day < s.resignBlockedUntil)
            {
                EventManager.I.Notify($"{s.Name} şimdilik yeni sözleşme istemiyor ({GameClock.I.DateOf(s.resignBlockedUntil)} tarihinden sonra dene).");
                return false;
            }
            if (!EconomyManager.I.TrySpend(s.def.contractCost, $"Sözleşme: {s.Name}", ExpenseCategory.Sozlesme)) return false;
            s.contracted = true;
            s.contractStartDay = GameClock.I.Day;
            s.contractEndDay = GameClock.I.Day + (RivalCompany.I != null ? RivalCompany.I.contractDays : 182);
            s.satisfaction = Mathf.Max(s.satisfaction, 60f);
            RefreshColors();
            EventManager.I.Notify($"{s.Name} ile sözleşme imzalandı (komisyon %{s.Commission * 100f:0.#}).");
            return true;
        }

        public Vector2Int RandomCustomerTile(Vector2Int avoid)
        {
            for (int i = 0; i < 50; i++)
            {
                var t = customerTiles[Random.Range(0, customerTiles.Count)];
                if (IsUnlocked(t.x) && t != avoid && !reserved.Contains(t)) return t;
            }
            return new Vector2Int(2, 3);
        }

        /// <summary>Yollar üzerinde en kısa yol (BFS). Başlangıç ve bitiş dahil. Nehri sadece köprülerden geçer.</summary>
        public List<Vector2Int> FindPath(Vector2Int from, Vector2Int to)
        {
            var result = new List<Vector2Int>();
            if (from == to)
            {
                result.Add(to);
                return result;
            }

            var prev = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            prev[from] = from;

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (cur == to) break;
                foreach (var d in Dirs)
                {
                    var n = cur + d;
                    if (!InBounds(n) || prev.ContainsKey(n)) continue;
                    if (n != to && !(IsRoad(n.x, n.y) && IsUnlocked(n.x))) continue;
                    prev[n] = cur;
                    queue.Enqueue(n);
                }
            }

            if (!prev.ContainsKey(to))
            {
                result.Add(from);
                result.Add(to);
                return result;
            }

            for (var p = to; p != from; p = prev[p]) result.Add(p);
            result.Add(from);
            result.Reverse();
            return result;
        }
    }
}
