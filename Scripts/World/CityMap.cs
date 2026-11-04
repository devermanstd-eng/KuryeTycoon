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
    ///  • Beş bölge: Merkez Mahalle (başlangıç, ortada), Doğu İlçesi, Sanayi &amp; Kampüs (doğuda),
    ///    Batı Liman (batıda: liman, otogar) ve Yeşil Tepe (kuzeyde, bütün şehir boyunca villalar).
    ///  • Şehrin ortasından (y = 13–15) Tisza nehri geçer; karşıya sadece 5 köprüden geçilir.
    ///  • Her blok bir türdedir: eski şehir, kule bloklar, bahçeli evler, çarşı, AVM, kampüs, sanayi, park, meydan.
    ///  • Merkez ofis Merkez Mahalle'nin ortasındaki Kossuth Meydanı'nda.
    ///  • Haritanın dışı kır: tarlalar, ormanlar, çiftlikler; nehir ve bazı yollar şehrin dışına devam eder (süs).
    /// Düzen sabittir (rastgele değil): adresler, kat sayıları ve 3D şehir her oyunda aynıdır.
    /// </summary>
    public class CityMap : MonoBehaviour
    {
        public static CityMap I { get; private set; }

        public const int Width = 49;
        public const int Height = 33;
        public const int Block = 4;

        public static readonly long[] UnlockCost = { 0, 0, 150000, 400000, 750000, 1200000 };
        public static readonly string[] DistrictNames = { "", "Merkez Mahalle", "Doğu İlçesi", "Sanayi & Kampüs", "Batı Liman", "Yeşil Tepe" };

        /// <summary>Karenin bölgesi (1–5). Bölge sınırındaki yol, açılış sırası önce gelen bölgeye aittir.</summary>
        public static int RegionOf(int x, int y)
        {
            if (y >= 25) return 5;      // Yeşil Tepe: kuzey şerit
            if (x < 12) return 4;       // Batı Liman
            if (x <= 24) return 1;      // Merkez Mahalle
            if (x <= 36) return 2;      // Doğu İlçesi
            return 3;                   // Sanayi & Kampüs
        }
        public static int RegionOf(Vector2Int t) => RegionOf(t.x, t.y);
        public static string DistrictOf(Vector2Int t) => DistrictNames[RegionOf(t)];

        // ---------- Şehir düzeni ----------

        public const int RiverBlockY = 3;                       // nehir: y = 13, 14, 15
        public static readonly int[] BridgeX = { 8, 16, 24, 32, 44 };
        public const int RailY = 22;                            // tren yolu (sanayi)
        public const int RailFromX = 37;
        public const float RiverY = RiverBlockY * Block + Block / 2f;   // nehrin ortası (14)

        public static readonly Vector2Int PlazaCenter = new Vector2Int(18, 6);      // Kossuth Meydanı (çeşme)
        public static readonly Vector2Int BelediyeCenter = new Vector2Int(22, 6);   // Belediye Parkı (anıt)
        public static readonly Vector2Int OtogarCenter = new Vector2Int(6, 6);      // Otogar (Batı Liman)

        /// <summary>
        /// Blok türleri: her satır bir blok sırası (by = 0 güney … 7 kuzey), her harf bir blok (bx = 0 batı … 11 doğu).
        /// O eski şehir · H kule bloklar · E bahçeli ev · M çarşı · A AVM/otogar · C kampüs · W sanayi/liman · P park · Z meydan · R nehir
        /// Sütunlar: bx 0–2 Batı Liman · 3–5 Merkez · 6–8 Doğu · 9–11 Sanayi; by 6–7 Yeşil Tepe.
        /// </summary>
        static readonly string[] Layout =
        {
            "EMMOOOEEEACC",   // by 0
            "WAMOZPMEEMAM",   // by 1  (otogar, meydan + HQ, Belediye Parkı, Nyír Plaza AVM)
            "WPMMOMMPMMWW",   // by 2  (liman, nehir kıyısı)
            "RRRRRRRRRRRR",   // by 3  Tisza
            "MHMHHMMHMWWW",   // by 4
            "EMOHHHEMEWWW",   // by 5  (tren yolu sanayide)
            "PEEEMEEPEEME",   // by 6  Yeşil Tepe
            "EEPEEEPEEEEP",   // by 7  Yeşil Tepe
        };

        public static readonly Landmark[] Landmarks =
        {
            new Landmark("Eski Şehir",           new Vector2(14f, 2f),    new Color(1f, 0.86f, 0.70f)),
            new Landmark("Kossuth Meydanı",      new Vector2(18f, 7.15f), new Color(1f, 0.92f, 0.75f)),
            new Landmark("Belediye Parkı",       new Vector2(22f, 6f),    new Color(0.70f, 1f, 0.70f)),
            new Landmark("Tisza",                new Vector2(4f, RiverY), new Color(0.65f, 0.85f, 1f)),
            new Landmark("Tisza",                new Vector2(28f, RiverY), new Color(0.65f, 0.85f, 1f)),
            new Landmark("Tisza",                new Vector2(40f, RiverY), new Color(0.65f, 0.85f, 1f)),
            new Landmark("Kule Bloklar",         new Vector2(18f, 20f),   new Color(1f, 0.80f, 0.85f)),
            new Landmark("Bahçeli Evler",        new Vector2(30f, 2f),    new Color(0.85f, 1f, 0.80f)),
            new Landmark("Nehir Parkı",          new Vector2(30f, 10f),   new Color(0.70f, 1f, 0.70f)),
            new Landmark("Tepe AVM",             new Vector2(38f, 2f),    new Color(1f, 0.85f, 0.60f)),
            new Landmark("Nyír Plaza AVM",       new Vector2(42f, 6f),    new Color(1f, 0.85f, 0.60f)),
            new Landmark("Kampüs",               new Vector2(44f, 2f),    new Color(1f, 0.95f, 0.70f)),
            new Landmark("Sanayi & Lojistik",    new Vector2(42f, 18f),   new Color(0.80f, 0.92f, 0.90f)),
            new Landmark("Tisza Limanı",         new Vector2(2f, 10f),    new Color(0.75f, 0.90f, 1f)),
            new Landmark("Otogar",               new Vector2(6f, 6f),     new Color(1f, 0.92f, 0.60f)),
            new Landmark("Batı Yakası",          new Vector2(6f, 20f),    new Color(1f, 0.90f, 0.80f)),
            new Landmark("Yeşil Tepe Villaları", new Vector2(24f, 28f),   new Color(0.80f, 1f, 0.75f)),
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
            int bx = Mathf.Clamp(x / Block, 0, Layout[0].Length - 1);
            int by = Mathf.Clamp(y / Block, 0, Layout.Length - 1);
            switch (Layout[by][bx])
            {
                case 'O': return Zone.OldTown;
                case 'H': return Zone.HighRise;
                case 'E': return Zone.Houses;
                case 'A': return Zone.Mall;
                case 'C': return Zone.Campus;
                case 'W': return Zone.Warehouse;
                case 'P': return Zone.Park;
                case 'Z': return Zone.Plaza;
                case 'R': return Zone.River;
                default: return Zone.Mixed;
            }
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
        public static readonly Color OtogarColor = new Color(0.64f, 0.66f, 0.70f);
        public static readonly Color CountryColor = new Color(0.36f, 0.55f, 0.30f);   // şehir dışı çayır

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
                case Zone.Mall: return BlockCenterOf(x, y) == OtogarCenter ? OtogarColor : MallColor;
                case Zone.Park: return GrassColor;
                case Zone.Plaza: return PlazaColor;
                case Zone.River: return WaterColor;
                default: p = MixedRoofs; break;
            }
            return p[Hash(x, y) % p.Length];
        }

        /// <summary>Lejantta gösterilen renk.</summary>
        public static Color LegendColor(Zone z) => RoofColor(z, 1, 1);

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

        // ---------- Şehir dışı (kır) ----------

        /// <summary>Şehrin dışına devam eden kır yolları: (başlangıç, bitiş) dünya koordinatı.</summary>
        public static readonly (Vector2 a, Vector2 b)[] CountryRoads =
        {
            (new Vector2(-70f, 4f),  new Vector2(-0.5f, 4f)),
            (new Vector2(-70f, 28f), new Vector2(-0.5f, 28f)),
            (new Vector2(Width - 0.5f, 8f),  new Vector2(Width + 70f, 8f)),
            (new Vector2(Width - 0.5f, 28f), new Vector2(Width + 70f, 28f)),
            (new Vector2(16f, -70f), new Vector2(16f, -0.5f)),
            (new Vector2(40f, -70f), new Vector2(40f, -0.5f)),
            (new Vector2(8f, Height - 0.5f),  new Vector2(8f, Height + 70f)),
            (new Vector2(32f, Height - 0.5f), new Vector2(32f, Height + 70f)),
        };

        public struct Field { public Rect r; public Color c; }
        public readonly List<Field> Fields = new List<Field>();
        public readonly List<Vector3> CountryTrees = new List<Vector3>();   // x, y, boy
        public readonly List<Vector2> Farms = new List<Vector2>();

        static bool NearCountryFeature(Vector2 p, float margin)
        {
            // Şehrin kendisi
            if (p.x > -0.5f - margin && p.x < Width - 0.5f + margin && p.y > -0.5f - margin && p.y < Height - 0.5f + margin) return true;
            // Nehir uzantısı
            if (Mathf.Abs(p.y - RiverY) < 1.5f + margin && (p.x < 0f || p.x > Width - 1f)) return true;
            // Kır yolları
            foreach (var r in CountryRoads)
            {
                if (Mathf.Approximately(r.a.y, r.b.y))
                {
                    if (Mathf.Abs(p.y - r.a.y) < 0.4f + margin && p.x >= Mathf.Min(r.a.x, r.b.x) - margin && p.x <= Mathf.Max(r.a.x, r.b.x) + margin) return true;
                }
                else if (Mathf.Abs(p.x - r.a.x) < 0.4f + margin && p.y >= Mathf.Min(r.a.y, r.b.y) - margin && p.y <= Mathf.Max(r.a.y, r.b.y) + margin) return true;
            }
            return false;
        }

        static bool RectClear(Rect r, float margin)
        {
            for (float x = r.xMin; x <= r.xMax + 0.01f; x += Mathf.Max(0.5f, r.width / 4f))
                for (float y = r.yMin; y <= r.yMax + 0.01f; y += Mathf.Max(0.5f, r.height / 4f))
                    if (NearCountryFeature(new Vector2(x, y), margin)) return false;
            return true;
        }

        /// <summary>Kır düzenini üretir (tohumlu: her oyunda aynı). 2D ve 3D aynı verileri kullanır.</summary>
        void GenerateCountryside()
        {
            var rng = new System.Random(4242);
            var fieldColors = new[]
            {
                new Color(0.78f, 0.70f, 0.40f), new Color(0.44f, 0.64f, 0.30f), new Color(0.54f, 0.42f, 0.28f),
                new Color(0.56f, 0.72f, 0.38f), new Color(0.70f, 0.66f, 0.36f), new Color(0.40f, 0.58f, 0.34f)
            };
            // Tarlalar: şehrin etrafında halka (kamera en çok bu kısmı görür)
            const float ring = 32f;
            for (float x = -ring; x < Width + ring; x += 6f)
                for (float y = -ring; y < Height + ring; y += 5f)
                {
                    if (rng.NextDouble() < 0.25) continue;          // arada çayır kalsın
                    float w = 4f + (float)rng.NextDouble() * 2f, h = 3f + (float)rng.NextDouble() * 2f;
                    var r = new Rect(x + 0.4f, y + 0.4f, w, h);
                    if (!RectClear(r, 0.6f)) continue;
                    Fields.Add(new Field { r = r, c = fieldColors[rng.Next(fieldColors.Length)] });
                }

            // Ormanlar: rastgele kümeler
            for (int k = 0; k < 22; k++)   // performans: orman sayısı sınırlı
            {
                var c = new Vector2((float)rng.NextDouble() * (Width + 2 * ring) - ring, (float)rng.NextDouble() * (Height + 2 * ring) - ring);
                if (NearCountryFeature(c, 2f)) { k--; if (rng.NextDouble() < 0.02) break; continue; }
                float radius = 1.5f + (float)rng.NextDouble() * 3f;
                int n = 7 + rng.Next(10);
                for (int i = 0; i < n; i++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = Mathf.Sqrt((float)rng.NextDouble()) * radius;
                    var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                    if (NearCountryFeature(p, 0.4f)) continue;
                    CountryTrees.Add(new Vector3(p.x, p.y, 0.3f + (float)rng.NextDouble() * 0.3f));
                }
            }
            // Şehrin kenarı boyunca ağaç sırası (şehir kırdan yumuşakça ayrılsın)
            for (float x = -1.5f; x < Width + 1f; x += 1.1f)
            {
                if (rng.NextDouble() < 0.4) TryEdgeTree(new Vector2(x, -1.6f), rng);
                if (rng.NextDouble() < 0.4) TryEdgeTree(new Vector2(x, Height + 0.6f), rng);
            }
            for (float y = -1.5f; y < Height + 1f; y += 1.1f)
            {
                if (rng.NextDouble() < 0.4) TryEdgeTree(new Vector2(-1.6f, y), rng);
                if (rng.NextDouble() < 0.4) TryEdgeTree(new Vector2(Width + 0.6f, y), rng);
            }

            // Çiftlikler: yollara yakın
            foreach (var r in CountryRoads)
            {
                var dir = (r.b - r.a).normalized;
                var side = new Vector2(-dir.y, dir.x);
                for (int i = 0; i < 3; i++)
                {
                    float t = 0.15f + (float)rng.NextDouble() * 0.6f;
                    var p = Vector2.Lerp(r.a, r.b, t) + side * (rng.NextDouble() < 0.5 ? 1.4f : -1.4f);
                    if (NearCountryFeature(p, 0.5f)) continue;
                    if (Vector2.Distance(p, new Vector2(Width / 2f, Height / 2f)) > 60f) continue;
                    Farms.Add(p);
                }
            }
        }

        void TryEdgeTree(Vector2 p, System.Random rng)
        {
            // Kenar ağaçları şehrin hemen dışında: sadece nehir ve kır yollarından uzak dur
            if (Mathf.Abs(p.y - RiverY) < 2f) return;
            foreach (var r in CountryRoads)
            {
                if (Mathf.Approximately(r.a.y, r.b.y) && Mathf.Abs(p.y - r.a.y) < 0.9f) return;
                if (!Mathf.Approximately(r.a.y, r.b.y) && Mathf.Abs(p.x - r.a.x) < 0.9f) return;
            }
            CountryTrees.Add(new Vector3(p.x + (float)rng.NextDouble() * 0.4f, p.y + (float)rng.NextDouble() * 0.4f, 0.3f + (float)rng.NextDouble() * 0.25f));
        }

        // ---------- Durum ----------

        public int UnlockLevel { get; private set; } = 1;
        public int MaxLevel => 5;

        public readonly List<Shop> Shops = new List<Shop>();
        public Vector2Int HQTile { get; private set; }
        public Vector2Int HQDoor { get; private set; }

        static readonly Vector2Int[] Dirs = { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up };

        SpriteRenderer[,] ground;
        Color[,] groundBase;
        readonly List<(SpriteRenderer sr, Color c, Vector2Int t)> decor = new List<(SpriteRenderer, Color, Vector2Int)>();
        readonly List<(SpriteRenderer sr, Color c)> freeDecor = new List<(SpriteRenderer, Color)>();   // kır süsleri (kilit yok)
        float snowCover;                         // kar örtüsü (0–1): WeatherFx kar yağdıkça artırır
        static readonly Color SnowColor = new Color(0.93f, 0.95f, 0.98f);

        /// <summary>Kar örtüsü: zemin, çatılar ve kır beyaza döner (yollar daha az, su hiç). 0 = kar yok.</summary>
        public void SetSnowCover(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (Mathf.Abs(amount - snowCover) < 0.001f) return;
            snowCover = amount;
            RefreshColors();
        }

        float wetness;                           // ıslak zemin (0–1): yağmurda kararır

        /// <summary>Islak zemin: yağmurda yollar ve zemin koyulaşıp maviye kayar (su birikintisi parlaklığı). 0 = kuru.</summary>
        public void SetWetness(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (Mathf.Abs(amount - wetness) < 0.001f) return;
            wetness = amount;
            RefreshColors();
        }

        Color Snowy(Color c, float mul = 1f)
        {
            if (c == WaterColor) return c;
            if (wetness > 0f)
            {
                var wet = new Color(c.r * 0.62f, c.g * 0.66f, c.b * 0.78f + 0.04f, c.a);
                c = Color.Lerp(c, wet, wetness * 0.8f * Mathf.Min(1f, mul + 0.3f));
            }
            if (snowCover <= 0f) return c;
            return Color.Lerp(c, SnowColor, snowCover * mul);
        }
        readonly List<Vector2Int> waterTiles = new List<Vector2Int>();
        SpriteRenderer hq;
        Transform root, roofRoot, countryRoot;
        readonly List<Vector2Int> customerTiles = new List<Vector2Int>();
        readonly HashSet<Vector2Int> customerSet = new HashSet<Vector2Int>();

        /// <summary>Müşterilerin oturduğu (sipariş verebilen) bina karesi mi?</summary>
        public bool IsCustomerBuilding(Vector2Int t) => customerSet.Contains(t);
        readonly List<Vector2Int> shopTiles = new List<Vector2Int>();
        readonly HashSet<Vector2Int> reserved = new HashSet<Vector2Int>();

        // Tekneler ve tren
        class Mover { public Transform t; public float speed; public float x; public float y; public int dir; public float wait; }
        readonly List<Mover> boats = new List<Mover>();
        Mover train;

        void Awake() { I = this; }

        public bool IsRoad(int x, int y) => RoadAt(x, y);
        public bool InBounds(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < Width && p.y < Height;
        public bool IsUnlocked(int x, int y) => RegionOf(x, y) <= UnlockLevel;
        public bool IsUnlocked(Vector2Int t) => IsUnlocked(t.x, t.y);
        public static Vector3 ToWorld(Vector2 t) => new Vector3(t.x, t.y, 0f);
        public static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        public void Generate()
        {
            root = new GameObject("Şehir").transform;
            root.SetParent(transform, false);
            roofRoot = new GameObject("Çatılar (2D)").transform;
            roofRoot.SetParent(root, false);
            countryRoot = new GameObject("Kır").transform;
            countryRoot.SetParent(root, false);
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
                    if (CustomerZone(z)) { customerTiles.Add(t); customerSet.Add(t); }
                    if (ShopZone(z)) shopTiles.Add(t);
                }
            }

            GenerateCountryside();
            BuildCountryside();
            BuildDecor();

            // Merkez ofis: Merkez Mahalle'nin ortasındaki meydanda, güneydeki yola bakar
            HQTile = new Vector2Int(PlazaCenter.x, PlazaCenter.y - 1);
            HQDoor = GetDoor(HQTile);
            for (int x = PlazaCenter.x - 1; x <= PlazaCenter.x + 1; x++)
                for (int y = PlazaCenter.y - 1; y <= PlazaCenter.y + 1; y++) reserved.Add(new Vector2Int(x, y));
            hq = SpriteFactory.Create("Merkez Ofis", root, false, CompanyProfile.Primary, ToWorld(HQTile), 0.9f, 2);

            PlaceShops();
            BuildMovers();
            RefreshColors();
        }

        // ---------- 2D süsler ----------

        SpriteRenderer Deco(string name, Transform parent, bool round, Color c, Vector2 pos, Vector2 scale, int order, bool trackLock = true)
        {
            var sr = SpriteFactory.Create(name, parent, round, c, new Vector3(pos.x, pos.y, 0f), 1f, order);
            sr.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            if (!trackLock) freeDecor.Add((sr, c));
            if (trackLock)
            {
                var t = new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(pos.x), 0, Width - 1), Mathf.Clamp(Mathf.RoundToInt(pos.y), 0, Height - 1));
                decor.Add((sr, c, t));
            }
            return sr;
        }

        /// <summary>Şehir dışı: çayır zemin, tarlalar, nehrin ve yolların devamı (2D; 3D ağaç ve çiftlikleri City3D çizer).</summary>
        void BuildCountryside()
        {
            float pad = 90f;
            Deco("Çayır", countryRoot, false, CountryColor, new Vector2((Width - 1) / 2f, (Height - 1) / 2f),
                 new Vector2(Width + pad * 2f, Height + pad * 2f), -10, false);
            foreach (var f in Fields)
                Deco("Tarla", countryRoot, false, f.c, f.r.center, f.r.size, -9, false);
            // Nehir şehrin iki yanından devam eder
            Deco("Tisza (batı)", countryRoot, false, WaterColor, new Vector2(-0.5f - pad / 2f, RiverY), new Vector2(pad, 3f), -8, false);
            Deco("Tisza (doğu)", countryRoot, false, WaterColor, new Vector2(Width - 0.5f + pad / 2f, RiverY), new Vector2(pad, 3f), -8, false);
            foreach (var r in CountryRoads)
            {
                var mid = (r.a + r.b) / 2f;
                bool horiz = Mathf.Approximately(r.a.y, r.b.y);
                var size = horiz ? new Vector2(Mathf.Abs(r.b.x - r.a.x), 0.7f) : new Vector2(0.7f, Mathf.Abs(r.b.y - r.a.y));
                Deco("Kır yolu", countryRoot, false, RoadColor, mid, size, -7, false);
                var line = horiz ? new Vector2(size.x, 0.05f) : new Vector2(0.05f, size.y);
                Deco("Yol çizgisi", countryRoot, false, new Color(0.85f, 0.82f, 0.65f), mid, line, -7, false);
            }
            // 2D ağaçlar ve çiftlik çatıları (yakınlaşınca 3D'leri çıkar, bunlar gizlenir)
            foreach (var t in CountryTrees)
            {
                var tc = Color.Lerp(new Color(0.14f, 0.36f, 0.18f), new Color(0.24f, 0.46f, 0.20f), Mathf.Repeat(t.x * 0.37f + t.y * 0.71f, 1f));
                Deco("Ağaç", roofRoot, true, tc, new Vector2(t.x, t.y), Vector2.one * (0.45f + t.z * 0.5f), -6, false);
            }
            foreach (var p in Farms)
            {
                Deco("Çiftlik evi", roofRoot, false, new Color(0.70f, 0.38f, 0.30f), p, new Vector2(0.6f, 0.45f), -6, false);
                Deco("Ahır", roofRoot, false, new Color(0.62f, 0.22f, 0.18f), p + new Vector2(0.9f, 0.2f), new Vector2(0.7f, 0.5f), -6, false);
            }
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
                    // Liman: Batı'da nehrin güney kıyısında iskeleler
                    if (WaterAt(x, y) && y == RiverBlockY * Block + 1 && x < 12 && x % 4 == 2)
                    {
                        Deco("İskele", root, false, new Color(0.50f, 0.38f, 0.26f), p + new Vector2(0f, -0.1f), new Vector2(0.3f, 0.9f), 1);
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
                            // Park ortası: Belediye Parkı'nda anıt, diğerlerinde gölet
                            if (new Vector2Int(x, y) == BelediyeCenter) Deco("Anıt", roofRoot, false, new Color(0.85f, 0.83f, 0.78f), p, new Vector2(0.22f, 0.22f), 1);
                            else Deco("Gölet", roofRoot, true, WaterColor, p, new Vector2(0.6f, 0.45f), 1);
                        }
                    }
                    else if (z == Zone.Mall && IsBlockCenter(x, y))
                    {
                        // AVM / otogar: bloğun tamamını kaplayan tek büyük çatı
                        bool otogar = new Vector2Int(x, y) == OtogarCenter;
                        Deco(otogar ? "Otogar" : "AVM", roofRoot, false, otogar ? OtogarColor : MallColor, p, new Vector2(2.6f, 2.6f), 1);
                        if (otogar)
                        {
                            // Peronlarda otobüsler
                            for (int i = 0; i < 4; i++)
                                Deco("Otobüs", roofRoot, false, new Color(0.95f, 0.78f, 0.20f), p + new Vector2(-0.9f + i * 0.6f, -0.3f), new Vector2(0.3f, 0.9f), 1);
                        }
                        else Deco("AVM cam çatı", roofRoot, false, new Color(0.62f, 0.80f, 0.90f), p, new Vector2(1.6f, 0.7f), 1);
                    }
                }
            }

            // Meydanın ortasında çeşme
            var fountain = new Vector2(PlazaCenter.x, PlazaCenter.y + 0.2f);
            Deco("Çeşme", roofRoot, true, new Color(0.70f, 0.68f, 0.64f), fountain, new Vector2(0.5f, 0.5f), 1);
            Deco("Çeşme suyu", roofRoot, true, new Color(0.45f, 0.72f, 0.90f), fountain, new Vector2(0.34f, 0.34f), 1);
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
            // Nehirde dört tekne (iki yönde); şehrin dışında da akar
            var boatDefs = new[]
            {
                (x: 3f, dir: 1, y: RiverY - 0.35f), (x: 26f, dir: 1, y: RiverY - 0.35f),
                (x: 14f, dir: -1, y: RiverY + 0.35f), (x: 40f, dir: -1, y: RiverY + 0.35f)
            };
            foreach (var b in boatDefs)
            {
                var t = new GameObject("Tekne").transform;
                t.SetParent(root, false);
                var hull = SpriteFactory.Create("Gövde", t, false, new Color(0.95f, 0.95f, 0.93f), Vector3.zero, 1f, 1);
                hull.transform.localScale = new Vector3(0.75f, 0.22f, 1f);
                var cabin = SpriteFactory.Create("Kabin", t, false, new Color(0.30f, 0.42f, 0.58f), new Vector3(-0.08f, 0f, 0f), 1f, 1);
                cabin.transform.localScale = new Vector3(0.3f, 0.14f, 1f);
                boats.Add(new Mover { t = t, speed = 0.35f + boats.Count * 0.06f, x = b.x, y = b.y, dir = b.dir });
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
                if (b.x > Width + 12f) b.x = -12f;
                if (b.x < -12f) b.x = Width + 12f;
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
                    if (!IsUnlocked(w)) c = LockedTint(c);
                    ground[w.x, w.y].color = c;
                }
            }
        }

        /// <summary>Kilitli bölgenin rengi: koyu ve gri (bölge yine okunur, ama açık olmadığı belli).</summary>
        public static Color LockedTint(Color c)
        {
            float g = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            return Color.Lerp(Color.Lerp(c, new Color(g, g, g), 0.55f), Color.black, 0.38f);
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
                    if (RegionOf(t) != def.unlockLevel || reserved.Contains(t)) continue;
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
                    if (!WaterAt(x, y)) c = Snowy(c, RoadAt(x, y) ? 0.5f : 1f);
                    if (!IsUnlocked(x, y)) c = LockedTint(c);
                    ground[x, y].color = c;
                }

            foreach (var d in decor)
            {
                if (d.sr == null) continue;
                var dc = Snowy(d.c, 0.8f);
                d.sr.color = IsUnlocked(d.t) ? dc : LockedTint(dc);
            }
            foreach (var f in freeDecor)
                if (f.sr != null) f.sr.color = Snowy(f.c, 0.9f);

            foreach (var s in Shops)
            {
                Color c = s.def.color;
                if (!IsUnlocked(s.tile)) c = Color.Lerp(LockedTint(c), Color.black, 0.3f);
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
            if (s.contracted || !IsUnlocked(s.tile)) return false;
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
            if (GoalManager.I != null) GoalManager.I.OnContract();
            EventManager.I.Notify($"{s.Name} ile sözleşme imzalandı (komisyon %{s.Commission * 100f:0.#}).");
            return true;
        }

        public Vector2Int RandomCustomerTile(Vector2Int avoid)
        {
            for (int i = 0; i < 80; i++)
            {
                var t = customerTiles[Random.Range(0, customerTiles.Count)];
                if (IsUnlocked(t) && t != avoid && !reserved.Contains(t)) return t;
            }
            // Çok nadir: rastgele denemeler tutmadıysa açık bölgedeki ilk uygun bina
            foreach (var t in customerTiles)
                if (IsUnlocked(t) && t != avoid && !reserved.Contains(t)) return t;
            return HQTile;
        }

        /// <summary>
        /// Dükkanın çevresinden müşteri binası: yarıçap içinde, yakın binalar daha olası (Wolt'taki teslimat bölgesi gibi).
        /// Uygun bina bulunamazsa şehirdeki herhangi bir açık bina.
        /// </summary>
        public Vector2Int RandomCustomerTileNear(Vector2Int shopTile, int radius)
        {
            for (int i = 0; i < 120; i++)
            {
                var t = customerTiles[Random.Range(0, customerTiles.Count)];
                if (!IsUnlocked(t) || t == shopTile || reserved.Contains(t)) continue;
                int d = Manhattan(t, shopTile);
                if (d > radius) continue;
                // Uzaklık arttıkça olasılık azalır (en uzakta ~%30)
                if (Random.value > 1f - 0.7f * d / (float)radius) continue;
                return t;
            }
            return RandomCustomerTile(shopTile);
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
                    if (n != to && !(IsRoad(n.x, n.y) && IsUnlocked(n))) continue;
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
