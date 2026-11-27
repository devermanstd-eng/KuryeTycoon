using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Bir yapı adasının (iki yol arasındaki 3x3 karelik blok) türü.</summary>
    public enum Zone { OldTown, HighRise, Houses, Mixed, Mall, Campus, Warehouse, Park, Plaza, River, Country }

    /// <summary>Haritada adı yazan yer (park, AVM, nehir, bölge).</summary>
    public struct Landmark
    {
        public string name;
        public Vector2 pos;
        public Color color;
        public Landmark(string name, Vector2 pos, Color color) { this.name = name; this.pos = pos; this.color = color; }
    }

    /// <summary>
    /// Izgara şehir: her 4 karede bir yol (bazı blokların içinden ara sokak geçer, köşelerdeki bloklar kır). Bölgeler para ile açılır. Bir kare = yaklaşık 200 metre.
    ///  • Beş bölge: Downtown (başlangıç, ortada), East Side, Sanayi &amp; Kampüs (doğuda),
    ///    West Harbour (batıda: liman, otogar) ve Green Hill (kuzeyde, bütün şehir boyunca villalar).
    ///  • Şehrin ortasından (y = 13–15) Swan River geçer; karşıya sadece 5 köprüden geçilir.
    ///  • Her blok bir türdedir: eski şehir, kule bloklar, bahçeli evler, çarşı, AVM, kampüs, sanayi, park, meydan.
    ///  • Merkez ofis Downtown'un ortasındaki Clock Tower Square'de.
    ///  • Haritanın dışı kır: tarlalar, ormanlar, çiftlikler; nehir ve bazı yollar şehrin dışına devam eder (süs).
    /// Düzen sabittir (rastgele değil): adresler, kat sayıları ve 3D şehir her oyunda aynıdır.
    /// </summary>
    public class CityMap : MonoBehaviour
    {
        public static CityMap I { get; private set; }

        // Izgara şehir 49×33; organik şehir (SehirPlani) açıksa boyut plandan gelir.
        const int IzgaraW = 49, IzgaraH = 33;
        public static int Width => Organik ? SehirPlani.W : IzgaraW;
        public static int Height => Organik ? SehirPlani.H : IzgaraH;
        public const int Block = 4;
        /// <summary>Organik şehir (gerçek harita planından, eğri ve çapraz sokaklar) açık mı?</summary>
        public static bool Organik => SehirPlani.Aktif;

        // Green Hill 1,2 M → 0,9 M (16.11 akşam). 21.11: West Harbour 750 → 600 bin, Green Hill 900 → 720 bin: 120 günlük testte
        // 39–44. ve 63–64. günde açılıyordu; 32 ve 45 hesaplık paketler bu bölgeleri beklerken 10–12 gün dolu kalıyordu.
        // 24.11 (u30): fiyatlar sabitlenince bütün bölgeler 26–27. günde açılıyordu; hedef son bölge ~45–55. gün.
        // East Side 150 → 600 bin, Industry & Campus 400 bin → 1 M, West Harbour 600 bin → 1,8 M, Green Hill 720 bin → 3,2 M.
        // 25.11 ikinci tur: 32 hesap paketi Industry & Campus'u beklerken 17–25 gün duvar oluyordu; Green Hill 39–49. günde
        // açılıyor (hedef 45–55), 60 hesap paketiyle birlikte hafifledi. Industry & Campus 1 → 0,8 M, Green Hill 3,2 → 2,8 M.
        public static readonly long[] UnlockCost = { 0, 0, 600000, 800000, 1800000, 2800000 };
        public static readonly string[] DistrictNames = { "", "Downtown", "East Side", "Industry & Campus", "West Harbour", "Green Hill" };

        /// <summary>Karenin bölgesi (1–5). Bölge sınırındaki yol, açılış sırası önce gelen bölgeye aittir.</summary>
        public static int RegionOf(int x, int y)
        {
            if (Organik) return SehirPlani.Bolge(x, y);
            if (!InCity(x, y)) return 1;   // kır: kilit yok (karartılmaz, üstünde bina da yok)
            if (y >= 25) return 5;      // Green Hill: kuzey şerit
            if (x < 12) return 4;       // West Harbour
            if (x <= 24) return 1;      // Downtown
            if (x <= 36) return 2;      // East Side
            return 3;                   // Industry & Campus
        }
        public static int RegionOf(Vector2Int t) => RegionOf(t.x, t.y);
        public static string DistrictOf(Vector2Int t) => DistrictNames[RegionOf(t)];

        // ---------- Şehir düzeni ----------

        public const int RiverBlockY = 3;                       // nehir: y = 13, 14, 15
        public static readonly int[] BridgeX = { 8, 16, 24, 32, 44 };
        public const int RailY = 22;                            // tren yolu (sanayi)
        public const int RailFromX = 37;
        public const float RiverY = RiverBlockY * Block + Block / 2f;   // nehrin ortası (14)

        public static Vector2Int PlazaCenter => Organik ? SehirPlani.Meydan : new Vector2Int(18, 6);      // Clock Tower Square (çeşme)
        public static Vector2Int BelediyeCenter => Organik ? SehirPlani.Belediye : new Vector2Int(22, 6);   // City Park (anıt)
        public static Vector2Int OtogarCenter => Organik ? SehirPlani.Otogar : new Vector2Int(6, 6);      // Otogar (West Harbour)

        /// <summary>
        /// Blok türleri: her satır bir blok sırası (by = 0 güney … 7 kuzey), her harf bir blok (bx = 0 batı … 11 doğu).
        /// O eski şehir · H kule bloklar · E bahçeli ev · M çarşı · A AVM/otogar · C kampüs · W sanayi/liman · P park · Z meydan · R nehir
        /// . kır (şehir dışı: yol yok, orman ve çayır; şehrin sınırı girintili çıkıntılı olsun diye)
        /// Sütunlar: bx 0–2 West Harbour · 3–5 Merkez · 6–8 Doğu · 9–11 Sanayi; by 6–7 Green Hill.
        /// </summary>
        // Büyük nehir şehri gibi: merkez nehrin iki yakasında bitişik nizam, avlulu yoğun bloklar ve kuleler;
        // dışa doğru çarşı (karışık), en dışta müstakil ev mahalleleri; sanayi ve liman doğuda.
        static readonly string[] Layout =
        {
            ".EEOOMMEEACC",   // by 0  dış güney: bahçeli evler, eski şehrin ucu, Hilltop Mall, kampüs
            "WAMOZPOMMEAE",   // by 1  (otogar, meydan + HQ, City Park, Northgate Mall)
            "WMOOHOOPMMEE",   // by 2  güney yakası: yoğun merkez, Riverside Park
            "RRRRRRRRRRRR",   // by 3  Swan River
            "MMOOHHOOMMWW",   // by 4  kuzey yakası: yoğun merkez, doğuda sanayi
            ".EMOHHMMEWWW",   // by 5  kule bloklar, dışa doğru çarşı ve evler (tren yolu sanayide)
            ".EEEEPEEEEE.",   // by 6  Green Hill: müstakil evler
            "..EEEPEEEE..",   // by 7  Green Hill
        };

        /// <summary>
        /// Blokların içinden geçen ara sokaklar: - doğu-batı (bloğun ortasından), | kuzey-güney, . yok.
        /// Merkezdeki bloklar bölünmez (avlulu kapalı bloklar); müstakil ev mahalleleri küçük parsellere bölünür (çok sokaklı).
        /// </summary>
        static readonly string[] Alleys =
        {
            ".|-..-.|-...",   // by 0
            ".........-.|",   // by 1
            "..........-|",   // by 2
            "............",   // by 3  nehir
            "............",   // by 4
            ".|......-...",   // by 5
            ".-|-|.-|-|-.",   // by 6
            "..|-|.-|-|..",   // by 7
        };

        /// <summary>Kaldırılan yol parçaları (iki blok birleşip büyük ada olur): (x, y başı, y sonu) dikey parçalar.</summary>
        static readonly (int x, int y0, int y1)[] MergedRoads =
        {
            (44, 1, 3),     // Kampüs: iki blok tek yerleşke
            (44, 17, 19),   // Sanayi: büyük lojistik alanı
        };

        static bool BlockInCity(int bx, int by) =>
            bx >= 0 && by >= 0 && bx < Layout[0].Length && by < Layout.Length && Layout[by][bx] != '.';

        static bool AlleyAt(int x, int y)
        {
            int bx = x / Block, by = y / Block;
            if (!BlockInCity(bx, by) || by >= Alleys.Length || bx >= Alleys[by].Length) return false;
            char c = Alleys[by][bx];
            if (c == '-') return y % Block == 2;
            if (c == '|') return x % Block == 2;
            return false;
        }

        static bool MergedAt(int x, int y)
        {
            foreach (var m in MergedRoads)
                if (x == m.x && y >= m.y0 && y <= m.y1) return true;
            return false;
        }

        /// <summary>Kare şehrin içinde mi (kır değil)? Yollar da şehre dahil.</summary>
        public static bool InCity(int x, int y) =>
            Organik ? x >= 0 && y >= 0 && x < Width && y < Height && (!SehirPlani.KirPlanli || !SehirPlani.KirKaresi(x, y) || SehirPlani.Yol(x, y))
                    : RoadAt(x, y) || ZoneOf(x, y) != Zone.Country;

        /// <summary>Kırsal plandan mı geliyor (plan sürüm 2: tarla çokgenleri, kır yolları, köyler)? Değilse kır burada üretilir.</summary>
        public static bool KirPlanli => Organik && SehirPlani.KirPlanli;

        /// <summary>Noktanın şehre (en yakın kentsel kareye) uzaklığı (kare). Plan kırsalı vermiyorsa şehir dikdörtgenine uzaklık.</summary>
        public static float KenteUzaklik(Vector2 p) => KirPlanli ? SehirPlani.KentUzakligi(p.x, p.y) : DisariUzaklik(p);

        /// <summary>Yeni Play'de (alan yeniden yüklenmese de) plana bağlı önbellekler sıfırlanır.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OyunBasiSifirla()
        {
            organikLm = null; organikKir = null; planKir = null;
            kirNehir = null; kirNehirUzak = null; kirYolUzak = null;
        }

        /// <summary>Haritada adı yazan yerler (organik şehirde plandan: meydan, park, nehir, semtler).</summary>
        public static Landmark[] Landmarks => Organik ? OrganikLandmarks() : IzgaraLandmarks;

        static Landmark[] organikLm;
        static Landmark[] OrganikLandmarks()
        {
            if (organikLm != null) return organikLm;
            var l = new List<Landmark>
            {
                new Landmark("Clock Tower Square", (Vector2)PlazaCenter + new Vector2(0f, 1.15f), new Color(1f, 0.92f, 0.75f)),
                new Landmark("City Park", BelediyeCenter, new Color(0.70f, 1f, 0.70f)),
                new Landmark("Bus Station", OtogarCenter, new Color(1f, 0.92f, 0.60f)),
            };
            // Nehir adı: suyun üç ayrı yerinde
            var su = new List<Vector2Int>();
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (SehirPlani.SuVar(x, y) && SehirPlani.SuVar(x + 1, y) && SehirPlani.SuVar(x - 1, y)) su.Add(new Vector2Int(x, y));
            if (su.Count > 0)
                for (int i = 0; i < 3; i++)
                    l.Add(new Landmark("Swan River", su[(su.Count - 1) * (i * 2 + 1) / 6], new Color(0.65f, 0.85f, 1f)));
            // Semt adları: her semtin yapı karelerinin ortası
            var top = new Vector2[6];
            var say = new int[6];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    if (RoadAt(x, y) || WaterAt(x, y) || ZoneOf(x, y) == Zone.Country) continue;
                    int b = RegionOf(x, y);
                    top[b] += new Vector2(x, y);
                    say[b]++;
                }
            var renk = new[] { Color.white, new Color(1f, 0.86f, 0.70f), new Color(1f, 0.90f, 0.80f), new Color(0.80f, 0.92f, 0.90f), new Color(0.75f, 0.90f, 1f), new Color(0.80f, 1f, 0.75f) };
            for (int b = 1; b <= 5; b++)
                if (say[b] > 0) l.Add(new Landmark(DistrictNames[b], top[b] / say[b], renk[b]));
            // u65: plandaki adlı yerler (mahalle merkezleri, istasyonlar, mezarlıklar, parklar, üniversite; okullar yazılmaz: kalabalık)
            foreach (var yer in SehirPlani.Yerler)
            {
                if (yer == null || string.IsNullOrEmpty(yer.ad)) continue;
                if (yer.tur == SehirPlani.YerOkul && !yer.ad.Contains("University")) continue;
                Color yr = yer.tur == SehirPlani.YerMahalle ? new Color(1f, 0.90f, 0.78f)
                         : yer.tur == SehirPlani.YerIstasyon ? new Color(0.88f, 0.90f, 1f)
                         : yer.tur == SehirPlani.YerMezarlik ? new Color(0.84f, 0.90f, 0.84f)
                         : yer.tur == SehirPlani.YerPark ? new Color(0.70f, 1f, 0.70f)
                         : new Color(1f, 0.95f, 0.70f);
                l.Add(new Landmark(yer.ad, yer.Konum, yr));
            }
            organikLm = l.ToArray();
            return organikLm;
        }

        static readonly Landmark[] IzgaraLandmarks =
        {
            new Landmark("Old Town",           new Vector2(14f, 2f),    new Color(1f, 0.86f, 0.70f)),
            new Landmark("Clock Tower Square",      new Vector2(18f, 7.15f), new Color(1f, 0.92f, 0.75f)),
            new Landmark("City Park",       new Vector2(22f, 6f),    new Color(0.70f, 1f, 0.70f)),
            new Landmark("Swan River",                new Vector2(4f, RiverY), new Color(0.65f, 0.85f, 1f)),
            new Landmark("Swan River",                new Vector2(28f, RiverY), new Color(0.65f, 0.85f, 1f)),
            new Landmark("Swan River",                new Vector2(40f, RiverY), new Color(0.65f, 0.85f, 1f)),
            new Landmark("Tower Blocks",         new Vector2(18f, 20f),   new Color(1f, 0.80f, 0.85f)),
            new Landmark("Garden Homes",        new Vector2(30f, 2f),    new Color(0.85f, 1f, 0.80f)),
            new Landmark("Riverside Park",          new Vector2(30f, 10f),   new Color(0.70f, 1f, 0.70f)),
            new Landmark("Hilltop Mall",             new Vector2(38f, 2f),    new Color(1f, 0.85f, 0.60f)),
            new Landmark("Northgate Mall",       new Vector2(42f, 6f),    new Color(1f, 0.85f, 0.60f)),
            new Landmark("Campus",               new Vector2(44f, 2f),    new Color(1f, 0.95f, 0.70f)),
            new Landmark("Industry & Logistics",    new Vector2(42f, 18f),   new Color(0.80f, 0.92f, 0.90f)),
            new Landmark("Swan Harbour",         new Vector2(2f, 10f),    new Color(0.75f, 0.90f, 1f)),
            new Landmark("Bus Station",               new Vector2(6f, 6f),     new Color(1f, 0.92f, 0.60f)),
            new Landmark("West Bank",          new Vector2(6f, 20f),    new Color(1f, 0.90f, 0.80f)),
            new Landmark("Green Hill Villas", new Vector2(24f, 28f),   new Color(0.80f, 1f, 0.75f)),
        };

        static bool InRiverRows(int y) => y > RiverBlockY * Block && y < (RiverBlockY + 1) * Block;
        static bool IsBridgeX(int x) => System.Array.IndexOf(BridgeX, x) >= 0;

        /// <summary>
        /// Yol mu? Izgara çizgileri (her 4 karede bir) şehirdeki bir bloğa değiyorsa yoldur; kırdaki bloklarda yol yok.
        /// Nehrin içinde sadece köprüler yoldur. Ayrıca blok içi ara sokaklar ve birleşik bloklarda kaldırılan parçalar.
        /// </summary>
        public static bool RoadAt(int x, int y)
        {
            if (Organik) return SehirPlani.Yol(x, y);
            if (x < 0 || y < 0 || x >= Width || y >= Height) return false;
            bool vx = x % Block == 0, hy = y % Block == 0;
            if (!vx && !hy) return AlleyAt(x, y);
            if (InRiverRows(y)) return IsBridgeX(x);
            if (MergedAt(x, y)) return false;
            int bx0 = vx ? x / Block - 1 : x / Block, by0 = hy ? y / Block - 1 : y / Block;
            for (int bx = bx0; bx <= x / Block; bx++)
                for (int by = by0; by <= y / Block; by++)
                    if (BlockInCity(bx, by)) return true;
            return false;
        }

        public static bool WaterAt(int x, int y) => Organik ? SehirPlani.SuVar(x, y) : InRiverRows(y) && !RoadAt(x, y) && RiverWaterAt(x, y);
        public static bool BridgeAt(int x, int y) => Organik ? SehirPlani.Kopru(x, y) : InRiverRows(y) && RoadAt(x, y);
        /// <summary>Nehir şeridinde suyun olmadığı kıyı karesi (çimen ve ağaç: nehir dümdüz kanal gibi görünmesin).</summary>
        public static bool RiverBankAt(int x, int y) => !Organik && InRiverRows(y) && !RoadAt(x, y) && !RiverWaterAt(x, y);

        /// <summary>
        /// Nehir şeridinin (3 kare) hangi karelerinde su var: orta sıra hep su (tekneler ve köprüler için),
        /// kenar sıralar kıvrımlı: nehir bir güneye bir kuzeye yaslanır, karşı kıyıda çimenli set kalır.
        /// Batıdaki liman ve haritanın kenarları hep geniş (şehir dışındaki nehre bağlansın).
        /// </summary>
        static bool RiverWaterAt(int x, int y)
        {
            int mid = RiverBlockY * Block + Block / 2;
            if (y == mid) return true;
            if (x < 12 || x > Width - 3) return true;
            float s = Mathf.Sin(x * 0.36f + 0.8f) + 0.35f * Mathf.Sin(x * 0.9f + 2.1f);
            return y < mid ? s > -0.45f : s < 0.45f;
        }

        /// <summary>
        /// Şehrin dışında nehrin orta çizgisi: şehir kenarından uzaklaştıkça kıvrılır (referanstaki gibi doğal akış).
        /// Şehrin içinde düz (RiverY).
        /// </summary>
        public static float RiverCurveY(float x)
        {
            if (Organik) return -9999f;            // organik şehirde nehir şehir dışına bu eğriyle uzanmaz
            float d = x < -0.5f ? -0.5f - x : x > Width - 0.5f ? x - (Width - 0.5f) : 0f;
            if (d <= 0f) return RiverY;
            float amp = Mathf.Min(d * 0.45f, 6f);
            float side = x < 0f ? 1f : -1f;
            return RiverY + side * amp * Mathf.Sin(d * 0.16f);
        }
        public static bool RailAt(int x, int y) => !Organik && y == RailY && x >= RailFromX && x < Width && !RoadAt(x, y);
        /// <summary>Tren yolunun yolu kestiği hemzemin geçit.</summary>
        public static bool RailCrossingAt(int x, int y) => !Organik && y == RailY && x >= RailFromX && RoadAt(x, y);

        public static Zone ZoneOf(Vector2Int t) => ZoneOf(t.x, t.y);
        public static Zone ZoneOf(int x, int y)
        {
            if (Organik)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) return Zone.Country;
                switch (SehirPlani.ZoneHarf(x, y))
                {
                    case 'O': return Zone.OldTown;
                    case 'H': return Zone.HighRise;
                    case 'E': return Zone.Houses;
                    case 'B': return Zone.Houses;       // banliyö evi: oyun mantığında bahçeli ev; City3D küçük ev havuzundan seçer
                    case 'C': return Zone.Campus;
                    case 'W': return Zone.Warehouse;
                    case 'P': return Zone.Park;
                    case 'Z': return Zone.Plaza;
                    case 'A': return Zone.Mall;
                    case 'R': return Zone.River;
                    // u65: mezarlık ('G') ve demiryolu şeridi ('D') oyun mantığında nehir kıyısı gibi yapısız yeşil şerittir (bina, dükkân,
                    // müşteri yok); mezar taşlarını, balastı ve rayları OrganikSehir3D çizer
                    case 'G': return Zone.River;
                    case 'D': return Zone.River;
                    case '.': return Zone.Country;      // plan dikdörtgeninin içindeki kır (şehir lekesinin dışı)
                    default: return Zone.Mixed;
                }
            }
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
                case '.': return Zone.Country;
                default: return Zone.Mixed;
            }
        }

        /// <summary>Kare, bloğunun ortasındaki kare mi (yola değmeyen iç kare)?</summary>
        public static bool IsBlockCenter(int x, int y) =>
            Organik ? new Vector2Int(x, y) == BelediyeCenter : x % Block == 2 && y % Block == 2;
        public static Vector2Int BlockCenterOf(int x, int y) => Organik ? new Vector2Int(x, y) : new Vector2Int(x / Block * Block + 2, y / Block * Block + 2);

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
                case Zone.Country: return "Kır";
                default: return "Nehir";
            }
        }

        // ---------- Renkler (2D harita) ----------

        public static readonly Color RoadColor = new Color(0.25f, 0.26f, 0.30f);
        public static readonly Color BridgeColor = new Color(0.42f, 0.40f, 0.38f);
        public static readonly Color WaterColor = new Color(0.36f, 0.62f, 0.84f);   // açık, canlı nehir mavisi
        public static readonly Color GrassColor = new Color(0.40f, 0.62f, 0.33f);
        public static readonly Color RiverBankColor = new Color(0.44f, 0.64f, 0.35f);
        public static readonly Color PlazaColor = new Color(0.82f, 0.76f, 0.64f);
        // Kaldırım ve avlu: soğuk gri yerine sıcak, açık taş rengi (referanstaki gibi şehir griye boğulmasın)
        public static readonly Color SidewalkColor = new Color(0.74f, 0.71f, 0.65f);
        public static readonly Color YardColor = new Color(0.60f, 0.58f, 0.53f);
        public static readonly Color RailBedColor = new Color(0.42f, 0.36f, 0.32f);
        public static readonly Color MallColor = new Color(0.88f, 0.76f, 0.72f);
        public static readonly Color OtogarColor = new Color(0.64f, 0.66f, 0.70f);
        public static readonly Color CountryColor = new Color(0.42f, 0.62f, 0.33f);   // şehir dışı çayır

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
                case Zone.Country: return CountryColor;
                default: p = MixedRoofs; break;
            }
            return p[Hash(x, y) % p.Length];
        }

        /// <summary>Lejantta gösterilen renk.</summary>
        public static Color LegendColor(Zone z) => RoofColor(z, 1, 1);

        /// <summary>Karenin zemin rengi (çatının etrafında görünen kaldırım / bahçe / su).</summary>
        public static Color GroundColor(int x, int y)
        {
            if (Organik && x >= 0 && y >= 0 && x < Width && y < Height)
            {
                // Organik şehir: zemin rengi yumuşak geçişli (çim ↔ taş), yol/su/park kenarlarını çizgi mesh'leri çizer.
                // Su ve yol kareleri de zemin renginde: kare kare mavi ve gri basamaklar görünmesin.
                if (ZoneOf(x, y) == Zone.Park) return GrassColor;
                var tas = Color.Lerp(PlazaColor, SidewalkColor, 0.6f);
                return Color.Lerp(GrassColor, tas, SehirPlani.Yumusak(x, y));
            }
            if (BridgeAt(x, y)) return BridgeColor;
            if (RoadAt(x, y)) return RoadColor;
            if (WaterAt(x, y)) return WaterColor;
            if (RiverBankAt(x, y)) return RiverBankColor;
            if (RailAt(x, y)) return RailBedColor;
            switch (ZoneOf(x, y))
            {
                case Zone.Country: return CountryColor;
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
            if (Organik && RoadAt(x, y)) return BridgeAt(x, y) ? BridgeColor : RoadColor;
            if (Organik && WaterAt(x, y)) return WaterColor;
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
            if (z == Zone.Park || z == Zone.Plaza || z == Zone.Mall || z == Zone.Country || z == Zone.River) return false;
            if (z == Zone.Warehouse) return true;          // sanayide iç kareler de depo
            return IsFrontage(x, y);
        }

        // ---------- Şehir dışı (kır) ----------

        /// <summary>Şehrin dışına devam eden kır yolları: (başlangıç, bitiş) dünya koordinatı.</summary>
        public static (Vector2 a, Vector2 b)[] CountryRoads => Organik ? (SehirPlani.KirPlanli ? PlanKirYollari() : OrganikKirYollari()) : IzgaraKirYollari;

        static (Vector2 a, Vector2 b)[] planKir;
        /// <summary>Plandaki şehir dışı yollar, kısa doğru parçaları olarak (eğri ve çapraz: eksene paralel değiller).</summary>
        static (Vector2 a, Vector2 b)[] PlanKirYollari()
        {
            if (planKir != null) return planKir;
            var l = new List<(Vector2, Vector2)>();
            foreach (var c in SehirPlani.KirYollari)
            {
                if (c == null) continue;
                for (int k = 0; k + 1 < c.Sayi; k++)
                    if ((c[k + 1] - c[k]).sqrMagnitude > 1e-8f) l.Add((c[k], c[k + 1]));
            }
            planKir = l.ToArray();
            return planKir;
        }

        // Plan kır yollarına uzaklık ızgarası (yarım kare çözünürlük): KirYoluYakin tepeler, orman, tarla, çim için çok sık sorulur
        static float[,] kirYolUzak;
        static int kyW, kyH;

        static void KirYoluIzgarasi()
        {
            kyW = Mathf.CeilToInt((Width + 2f * KnPay) / KnAdim) + 1;
            kyH = Mathf.CeilToInt((Height + 2f * KnPay) / KnAdim) + 1;
            var g = new float[kyW, kyH];
            for (int i = 0; i < kyW; i++) for (int j = 0; j < kyH; j++) g[i, j] = 99f;
            const float Menzil = 4.5f;
            // Demiryolu da ızgaraya işlenir (yol listesine değil): orman, çalı, çayır, tepeler hattın üstüne gelmesin
            var parcalar = new List<(Vector2 a, Vector2 b)>(PlanKirYollari());
            foreach (var c in SehirPlani.Demiryolu)
            {
                if (c == null) continue;
                for (int k = 0; k + 1 < c.Sayi; k++)
                    if ((c[k + 1] - c[k]).sqrMagnitude > 1e-8f) parcalar.Add((c[k], c[k + 1]));
            }
            foreach (var r in parcalar)
            {
                Vector2 a = r.a, b = r.b;
                int i0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - Menzil + KnPay) / KnAdim));
                int i1 = Mathf.Min(kyW - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + Menzil + KnPay) / KnAdim));
                int j0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - Menzil + KnPay) / KnAdim));
                int j1 = Mathf.Min(kyH - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + Menzil + KnPay) / KnAdim));
                var ab = b - a;
                float L2 = Mathf.Max(ab.sqrMagnitude, 1e-8f);
                for (int i = i0; i <= i1; i++)
                    for (int j = j0; j <= j1; j++)
                    {
                        var q = new Vector2(i * KnAdim - KnPay, j * KnAdim - KnPay);
                        float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / L2);
                        float d = Vector2.Distance(q, a + ab * t);
                        if (d < g[i, j]) g[i, j] = d;
                    }
            }
            kirYolUzak = g;
        }

        /// <summary>Noktanın plandaki en yakın şehir dışı yolun eksenine uzaklığı (kare; uzaktaysa 99). Yalnız plan kırsalı veriyorsa.</summary>
        public static float KirYoluUzak(Vector2 p)
        {
            if (!KirPlanli) return 99f;
            if (kirYolUzak == null) KirYoluIzgarasi();
            float fx = (p.x + KnPay) / KnAdim, fy = (p.y + KnPay) / KnAdim;
            if (fx < 0f || fy < 0f || fx > kyW - 1.001f || fy > kyH - 1.001f) return 99f;
            int ix = (int)fx, iy = (int)fy;
            float tx = fx - ix, ty = fy - iy;
            float a = Mathf.Lerp(kirYolUzak[ix, iy], kirYolUzak[ix + 1, iy], tx);
            float b = Mathf.Lerp(kirYolUzak[ix, iy + 1], kirYolUzak[ix + 1, iy + 1], tx);
            return Mathf.Lerp(a, b, ty);
        }

        static (Vector2 a, Vector2 b)[] organikKir;
        /// <summary>Organik şehirde kır yolları: harita kenarına değen caddeler dümdüz dışarı uzar.</summary>
        static (Vector2 a, Vector2 b)[] OrganikKirYollari()
        {
            if (organikKir != null) return organikKir;
            var l = new List<(Vector2, Vector2)>();
            int w = Width, h = Height;
            for (int x = 1; x < w - 1; x++)
            {
                if (RoadAt(x, 0) && !RoadAt(x - 1, 0)) l.Add((new Vector2(x, -Boy(x, 0)), new Vector2(x, -0.5f)));
                if (RoadAt(x, h - 1) && !RoadAt(x - 1, h - 1)) l.Add((new Vector2(x, h - 0.5f), new Vector2(x, h - 1 + Boy(x, h - 1))));
            }
            for (int y = 1; y < h - 1; y++)
            {
                if (RoadAt(0, y) && !RoadAt(0, y - 1)) l.Add((new Vector2(-Boy(0, y), y), new Vector2(-0.5f, y)));
                if (RoadAt(w - 1, y) && !RoadAt(w - 1, y - 1)) l.Add((new Vector2(w - 0.5f, y), new Vector2(w - 1 + Boy(w - 1, y), y)));
            }
            // Kenarda birbirine çok yakın sokaklar dışarıda tarak gibi paralel uzamasın: caddeler kalır, sokaklar arası en az 4 kare
            var secilen = new List<(Vector2, Vector2)>();
            foreach (var r in l)
            {
                var uc = r.Item1.x < -0.5f || r.Item1.y < -0.5f ? r.Item2 : r.Item1;     // şehir kenarındaki uç
                var ucT = new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(uc.x), 0, w - 1), Mathf.Clamp(Mathf.RoundToInt(uc.y), 0, h - 1));
                bool genis = SehirPlani.Genis(SehirPlani.YolNo(ucT.x, ucT.y));
                bool yakin = false;
                foreach (var q in secilen)
                {
                    var uq = q.Item1.x < -0.5f || q.Item1.y < -0.5f ? q.Item2 : q.Item1;
                    if (Vector2.Distance(uq, uc) < 4f) { yakin = true; break; }
                }
                if (!yakin || genis) secilen.Add(r);
            }
            l = secilen;
            // Çevre yolu: şehri banliyö kuşağının dışından saran dikdörtgen; sokaklar buna bağlanır (tarlada yarım kalmaz)
            float c = CevreYolu;
            l.Add((new Vector2(-c, -c), new Vector2(w - 1 + c, -c)));
            l.Add((new Vector2(-c, h - 1 + c), new Vector2(w - 1 + c, h - 1 + c)));
            l.Add((new Vector2(-c, -c), new Vector2(-c, h - 1 + c)));
            l.Add((new Vector2(w - 1 + c, -c), new Vector2(w - 1 + c, h - 1 + c)));
            organikKir = l.ToArray();
            // Caddeler kırda uzağa gider; sokaklar banliyö kuşağından biraz öteye kadar
            float Boy(int x, int y) => SehirPlani.Genis(SehirPlani.YolNo(x, y)) ? 70f : CevreYolu;
            return organikKir;
        }

        // ---------- Organik şehirde nehrin şehir dışındaki devamı ----------

        /// <summary>Nehrin şehir dışına akan bir kolu: orta çizgi noktaları ve her noktadaki su genişliği (kare).</summary>
        public class KirNehirKolu
        {
            public readonly List<Vector2> P = new List<Vector2>();
            public readonly List<float> G = new List<float>();
        }

        static List<KirNehirKolu> kirNehir;
        static float[,] kirNehirUzak;                 // kıyıya (su kenarına) uzaklık; su içinde eksi
        const float KnPay = 56f, KnAdim = 0.5f;       // ızgara: şehrin KnPay kare ötesine kadar, yarım kare çözünürlük
        static int knW, knH;

        /// <summary>Organik şehirde nehrin şehir kenarından çıkıp harita sonuna kadar kıvrılarak akan kolları.</summary>
        public static IReadOnlyList<KirNehirKolu> KirNehir
        {
            get
            {
                if (kirNehir == null) KirNehirUret();
                return kirNehir;
            }
        }

        /// <summary>
        /// Şehir kenarındaki su karelerini bulur (her kenarda bitişik su dizisi = bir çıkış) ve her çıkıştan dışarı,
        /// yumuşak kıvrımlarla (Perlin) harita sonuna kadar uzanan bir kol üretir. Tohumlu: her oyunda aynı.
        /// </summary>
        static void KirNehirUret()
        {
            kirNehir = new List<KirNehirKolu>();
            if (!Organik) return;
            // Plan nehrin şehir dışındaki kollarını veriyorsa (sürüm 2) onlar kullanılır: tarlalar ve yollar o kollara göre üretildi
            var planKollari = SehirPlani.KirNehir;
            if (planKollari.Count > 0)
            {
                foreach (var c in planKollari)
                {
                    if (c == null || c.Sayi < 2) continue;
                    var kol = new KirNehirKolu();
                    for (int i = 0; i < c.Sayi; i++)
                    {
                        kol.P.Add(c[i]);
                        kol.G.Add(c.g != null && i < c.g.Length ? Mathf.Max(1f, c.g[i]) : 3.5f);
                    }
                    kirNehir.Add(kol);
                }
                KirNehirIzgarasi();
                return;
            }
            int w = Width, h = Height;
            var cikislar = new List<(Vector2 merkez, Vector2 yon, float gen)>();
            void Tara(int n, System.Func<int, Vector2Int> kare, Vector2 yon)
            {
                int bas = -1;
                for (int i = 0; i <= n; i++)
                {
                    bool su = false;
                    if (i < n) { var t = kare(i); su = SehirPlani.SuVar(t.x, t.y); }
                    if (su && bas < 0) bas = i;
                    if (!su && bas >= 0)
                    {
                        var a = kare(bas); var b = kare(i - 1);
                        var m = (new Vector2(a.x, a.y) + new Vector2(b.x, b.y)) * 0.5f;
                        cikislar.Add((m, yon, i - bas + 0.4f));
                        bas = -1;
                    }
                }
            }
            Tara(w, i => new Vector2Int(i, 0), Vector2.down);
            Tara(w, i => new Vector2Int(i, h - 1), Vector2.up);
            Tara(h, i => new Vector2Int(0, i), Vector2.left);
            Tara(h, i => new Vector2Int(w - 1, i), Vector2.right);

            int no = 0;
            foreach (var c in cikislar)
            {
                no++;
                var kol = new KirNehirKolu();
                float g0 = Mathf.Clamp(c.gen, 2.2f, 4.5f);
                float aci0 = Mathf.Atan2(c.yon.y, c.yon.x);
                var p = c.merkez - c.yon * 1.6f;               // şehrin içinden başla: su çokgeniyle bindirmeli, ek yeri görünmesin
                float sapma = 0f, s = 0f;
                for (int adim = 0; adim < 400; adim++)
                {
                    kol.P.Add(p);
                    float dis = DisariUzaklik(p);
                    kol.G.Add(Mathf.Lerp(g0, g0 + 0.9f, Mathf.Clamp01(dis / 30f)));
                    if (dis > KnPay - 4f) break;
                    // Kıvrım: düşük frekanslı gürültü; şehirden çıkarken düz başlar, sonra serbestçe kıvrılır
                    float serbest = Mathf.Clamp01((s - 2.5f) / 8f);
                    float hedef = (Mathf.PerlinNoise(no * 17.3f + 3.1f, s * 0.045f) * 2f - 1f) * 1.25f
                                + (Mathf.PerlinNoise(no * 5.7f + 41.2f, s * 0.13f) * 2f - 1f) * 0.35f;
                    sapma = Mathf.Lerp(sapma, hedef * serbest, 0.25f);
                    float aci = aci0 + Mathf.Clamp(sapma, -1.15f, 1.15f);     // hep dışarı doğru (en çok ~66° yana)
                    p += new Vector2(Mathf.Cos(aci), Mathf.Sin(aci)) * KnAdim;
                    s += KnAdim;
                }
                kirNehir.Add(kol);
            }
            KirNehirIzgarasi();
            if (cikislar.Count > 0) Debug.Log($"Kurye Tycoon: nehir şehir dışında {cikislar.Count} kol halinde devam ediyor.");
        }

        /// <summary>Şehir dikdörtgeninin dışına uzaklık (kare; içerde 0).</summary>
        static float DisariUzaklik(Vector2 p)
        {
            float dx = Mathf.Max(0f, Mathf.Max(-0.5f - p.x, p.x - (Width - 0.5f)));
            float dy = Mathf.Max(0f, Mathf.Max(-0.5f - p.y, p.y - (Height - 0.5f)));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Kıyıya uzaklık ızgarası: her parçayı çevresindeki hücrelere işler (sorgu hızlı olsun).</summary>
        static void KirNehirIzgarasi()
        {
            knW = Mathf.CeilToInt((Width + 2f * KnPay) / KnAdim) + 1;
            knH = Mathf.CeilToInt((Height + 2f * KnPay) / KnAdim) + 1;
            kirNehirUzak = new float[knW, knH];
            for (int i = 0; i < knW; i++) for (int j = 0; j < knH; j++) kirNehirUzak[i, j] = 99f;
            const float Menzil = 12f;
            foreach (var kol in kirNehir)
                for (int k = 0; k + 1 < kol.P.Count; k++)
                {
                    Vector2 a = kol.P[k], b = kol.P[k + 1];
                    float ga = kol.G[k] * 0.5f, gb = kol.G[k + 1] * 0.5f;
                    int i0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - Menzil + KnPay) / KnAdim));
                    int i1 = Mathf.Min(knW - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + Menzil + KnPay) / KnAdim));
                    int j0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - Menzil + KnPay) / KnAdim));
                    int j1 = Mathf.Min(knH - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + Menzil + KnPay) / KnAdim));
                    var ab = b - a;
                    float L2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
                    for (int i = i0; i <= i1; i++)
                        for (int j = j0; j <= j1; j++)
                        {
                            var q = new Vector2(i * KnAdim - KnPay, j * KnAdim - KnPay);
                            float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / L2);
                            float d = Vector2.Distance(q, a + ab * t) - Mathf.Lerp(ga, gb, t);
                            if (d < kirNehirUzak[i, j]) kirNehirUzak[i, j] = d;
                        }
                }
        }

        /// <summary>Noktanın şehir dışındaki nehrin kıyısına uzaklığı (kare; su içinde eksi, nehir yoksa 99).</summary>
        public static float KirNehirUzak(Vector2 p)
        {
            if (!Organik) return 99f;
            if (kirNehir == null) KirNehirUret();
            if (kirNehirUzak == null) return 99f;
            float fx = (p.x + KnPay) / KnAdim, fy = (p.y + KnPay) / KnAdim;
            if (fx < 0f || fy < 0f || fx > knW - 1.001f || fy > knH - 1.001f) return 99f;
            int ix = (int)fx, iy = (int)fy;
            float tx = fx - ix, ty = fy - iy;
            float a = Mathf.Lerp(kirNehirUzak[ix, iy], kirNehirUzak[ix + 1, iy], tx);
            float b = Mathf.Lerp(kirNehirUzak[ix, iy + 1], kirNehirUzak[ix + 1, iy + 1], tx);
            return Mathf.Lerp(a, b, ty);
        }

        /// <summary>Nokta şehir dışındaki nehre (kıyı payıyla) yakın mı? Şehrin içi hep false (orayı plan çizer).</summary>
        public static bool KirNehirYakin(Vector2 p, float pay) =>
            Organik && DisariUzaklik(p) > 0f && KirNehirUzak(p) < pay;

        /// <summary>Kır özelliklerinin (tarla, orman, çiftlik) nehirden kaçınması: organikte gerçek kol, ızgarada eski eğri.</summary>
        static bool NehirUzantisiYakin(Vector2 p, float pay) =>
            Organik ? KirNehirYakin(p, pay) : (p.x < 0f || p.x > Width - 1f) && Mathf.Abs(p.y - RiverCurveY(p.x)) < 1.7f + pay;

        static readonly (Vector2 a, Vector2 b)[] IzgaraKirYollari =
        {
            (new Vector2(-70f, 4f),  new Vector2(-0.5f, 4f)),
            (new Vector2(-70f, 28f), new Vector2(-0.5f, 28f)),
            (new Vector2(IzgaraW - 0.5f, 8f),  new Vector2(IzgaraW + 70f, 8f)),
            (new Vector2(IzgaraW - 0.5f, 28f), new Vector2(IzgaraW + 70f, 28f)),
            (new Vector2(16f, -70f), new Vector2(16f, -0.5f)),
            (new Vector2(40f, -70f), new Vector2(40f, -0.5f)),
            (new Vector2(8f, IzgaraH - 0.5f),  new Vector2(8f, IzgaraH + 70f)),
            (new Vector2(32f, IzgaraH - 0.5f), new Vector2(32f, IzgaraH + 70f)),
        };

        /// <summary>
        /// Tarla. Izgara şehirde ve eski organik planda eksene paralel dikdörtgen (r). Plan kırsalı veriyorsa (sürüm 2) dışbükey
        /// çokgen (kose, saat yönünün tersi; r onu saran dikdörtgen) ve içine sığan eğik dikdörtgen (merkez, boyut, aci °):
        /// 3D parsel modeli ve ekin sıraları o dikdörtgene göre.
        /// </summary>
        public struct Field
        {
            public Rect r; public Color c;
            public Vector2[] kose; public Vector2 merkez, boyut; public float aci;
            public bool Cokgen => kose != null && kose.Length >= 3;
            /// <summary>Parsel dikdörtgeninin merkezi ve boyutu (çokgen değilse r).</summary>
            public Vector2 ParselMerkez => Cokgen ? merkez : r.center;
            public Vector2 ParselBoyut => Cokgen ? boyut : r.size;
            public float ParselAci => Cokgen ? aci : 0f;
        }

        /// <summary>Noktanın tarlanın kenarlarına içeriden en küçük uzaklığı (kare; artı = içeride, eksi = dışarıda).</summary>
        public static float TarlaIci(Field f, Vector2 p)
        {
            if (!f.Cokgen)
                return Mathf.Min(Mathf.Min(p.x - f.r.xMin, f.r.xMax - p.x), Mathf.Min(p.y - f.r.yMin, f.r.yMax - p.y));
            float en = float.MaxValue;
            int n = f.kose.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = f.kose[i], b = f.kose[(i + 1) % n];
                var e = b - a;
                float L = e.magnitude;
                if (L < 1e-5f) continue;
                float d = (e.x * (p.y - a.y) - e.y * (p.x - a.x)) / L;      // kenarın solu (içerisi) artı
                if (d < en) en = d;
            }
            return en;
        }

        /// <summary>
        /// Çokgen tarlanın zemin rengi: üstüne oturacak 3D parselin türüne göre (City3D.KirTuru; sıra numarası 1'den başlar).
        /// Ekin: buğday / genç ekin / kolza; sürülmüş: toprak; saman: anız; çayır, bağ, bahçe, gölet, koru, sera: çimen.
        /// </summary>
        static Color TarlaRengi(int fi, int tur)
        {
            string t = City3D.KirTuru(fi);
            if (t.EndsWith("ekin")) { int k = Mathf.Abs(tur) % 3; return TarlaRenkleri[k == 0 ? 0 : k == 1 ? 1 : 3]; }
            if (t.EndsWith("surulmus")) return TarlaRenkleri[2];
            if (t.EndsWith("saman")) return TarlaRenkleri[5];
            return TarlaRenkleri[4];
        }

        static readonly Color[] TarlaRenkleri =
        {
            // Tarla renkleri (yumuşak): olgun buğday, genç ekin, sürülmüş toprak, kolza, çayır, anız
            new Color(0.80f, 0.71f, 0.42f), new Color(0.55f, 0.66f, 0.33f), new Color(0.55f, 0.43f, 0.30f),
            new Color(0.84f, 0.78f, 0.36f), new Color(0.48f, 0.60f, 0.32f), new Color(0.73f, 0.65f, 0.45f)
        };
        public readonly List<Field> Fields = new List<Field>();
        public readonly List<Vector3> CountryTrees = new List<Vector3>();   // x, y, boy
        public readonly List<Vector2> Farms = new List<Vector2>();

        /// <summary>Organik şehrin çevresinde banliyö kuşağı (kare): tarla ve orman burada başlamaz, evler seyrelerek kıra karışır.</summary>
        public const float Banliyo = 7f;
        /// <summary>Organik şehrin çevre yolu: şehir kenarından bu kadar kare uzakta.</summary>
        public const float CevreYolu = 9f;

        /// <summary>Nokta organik şehrin banliyö kuşağında mı (şehrin dışında, kenara Banliyo kareden yakın)?</summary>
        public static bool BanliyoKusagi(Vector2 p) =>
            Organik && !SehirPlani.KirPlanli && p.x > -Banliyo && p.y > -Banliyo && p.x < Width - 1 + Banliyo && p.y < Height - 1 + Banliyo;

        /// <summary>Nokta bir kır yoluna yakın mı (City3D banliyö evleri yola taşmasın)?</summary>
        public static bool KirYoluYakin(Vector2 p, float pay) => NearCountryRoad(p, pay);

        static bool NearCountryFeature(Vector2 p, float margin)
        {
            if (BanliyoKusagi(p)) return true;
            // Şehrin kendisi (girintili sınır: kırdaki bloklar ormana ve tarlaya açık)
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    int cx = Mathf.RoundToInt(p.x + i * margin), cy = Mathf.RoundToInt(p.y + j * margin);
                    if (cx < 0 || cy < 0 || cx >= Width || cy >= Height) continue;
                    if (InCity(cx, cy)) return true;
                }
            // Nehir uzantısı
            if (NehirUzantisiYakin(p, margin + (Organik ? 0.9f : 0f))) return true;
            // Kır yolları
            return NearCountryRoad(p, margin);
        }

        /// <summary>Harita dışındaki (x, y) karesi bir kır yolunun üstünde mi?</summary>
        public static bool CountryRoadAt(int x, int y)
        {
            if (x >= 0 && y >= 0 && x < Width && y < Height) return false;
            if (KirPlanli) return KirYoluUzak(new Vector2(x, y)) < 0.5f;
            foreach (var r in CountryRoads)
            {
                if (Mathf.Approximately(r.a.y, r.b.y))
                {
                    if (Mathf.RoundToInt(r.a.y) == y && x >= Mathf.Min(r.a.x, r.b.x) - 0.5f && x <= Mathf.Max(r.a.x, r.b.x) + 0.5f) return true;
                }
                else if (Mathf.RoundToInt(r.a.x) == x && y >= Mathf.Min(r.a.y, r.b.y) - 0.5f && y <= Mathf.Max(r.a.y, r.b.y) + 0.5f) return true;
            }
            return false;
        }

        static bool NearCountryRoad(Vector2 p, float margin)
        {
            if (KirPlanli) return KirYoluUzak(p) < 0.4f + margin;       // eğri yollar: uzaklık ızgarasından
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

        /// <summary>Kırda nokta dolu mu: tarla, nehir, kır yolu ya da ağaca çok yakın (çayır parçaları buraya konmaz).</summary>
        public bool KirdaDolu(Vector2 p, float pay)
        {
            if (InField(p, pay * 0.5f)) return true;
            if (NehirUzantisiYakin(p, pay + (Organik ? 1.1f : 0.5f))) return true;
            if (!Organik && p.x >= -1f && p.x <= Width && Mathf.Abs(p.y - RiverY) < 2.5f) return true;
            if (NearCountryRoad(p, pay * 0.6f)) return true;
            foreach (var f in Farms) if ((f - p).sqrMagnitude < 2.5f) return true;
            return false;
        }

        /// <summary>Nokta bir tarlanın içinde mi (kenar payıyla)?</summary>
        bool InField(Vector2 p, float margin)
        {
            foreach (var f in Fields)
            {
                if (!(p.x > f.r.xMin - margin && p.x < f.r.xMax + margin && p.y > f.r.yMin - margin && p.y < f.r.yMax + margin)) continue;
                if (!f.Cokgen || TarlaIci(f, p) > -margin) return true;
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
            var fieldColors = TarlaRenkleri;
            const float ring = 32f;
            // Plan kırsalı veriyorsa (sürüm 2): tarlalar plandaki düzensiz çokgenler, çiftlikler plandaki yerlerinde
            bool plan = KirPlanli;
            if (plan)
            {
                foreach (var t in SehirPlani.Tarlalar)
                {
                    if (t == null || t.Sayi < 3) continue;
                    var k = new Vector2[t.Sayi];
                    float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
                    for (int i = 0; i < k.Length; i++)
                    {
                        k[i] = new Vector2(t.x[i], t.y[i]);
                        x0 = Mathf.Min(x0, k[i].x); x1 = Mathf.Max(x1, k[i].x);
                        y0 = Mathf.Min(y0, k[i].y); y1 = Mathf.Max(y1, k[i].y);
                    }
                    if (t.gen < 0.3f || t.boy < 0.3f) continue;
                    Fields.Add(new Field
                    {
                        r = Rect.MinMaxRect(x0, y0, x1, y1), c = TarlaRengi(Fields.Count + 1, t.tur),
                        kose = k, merkez = new Vector2(t.mx, t.my), boyut = new Vector2(t.gen, t.boy), aci = t.aci
                    });
                }
                var cf = SehirPlani.Ciftlikler;
                if (cf != null)
                    for (int i = 0; i < cf.Sayi; i++) Farms.Add(new Vector2(cf.x[i], cf.y[i]));
            }
            // Tarlalar: şehrin etrafında halka (kamera en çok bu kısmı görür)
            for (float x = -ring; x < Width + ring && !plan; x += 6f)
                for (float y = -ring; y < Height + ring; y += 5f)
                {
                    // Tarlalar yan yana dizilir (yama gibi), aralarında ince çit/çimen şeridi kalır; arada çayır boşlukları
                    if (rng.NextDouble() < 0.3) continue;
                    float w = 4.6f + (float)rng.NextDouble() * 1.1f, h = 3.8f + (float)rng.NextDouble() * 0.9f;
                    var r = new Rect(x + 0.15f, y + 0.15f, w, h);
                    if (!RectClear(r, 0.6f)) continue;
                    Fields.Add(new Field { r = r, c = fieldColors[rng.Next(fieldColors.Length)] });
                }

            // Boşlukları doldur: şehrin girintili kenarı ve büyük tarlaların arasında kalan yerlere küçük tarlalar
            // (yoksa şehirle tarlalar arasında geniş boş çimen şeritleri kalıyordu)
            for (float x = -ring; x < Width + ring && !plan; x += 3f)
                for (float y = -ring; y < Height + ring; y += 2.6f)
                {
                    if (rng.NextDouble() < 0.25) continue;
                    float w = 2.4f + (float)rng.NextDouble() * 0.5f, h = 2.0f + (float)rng.NextDouble() * 0.4f;
                    var r = new Rect(x + 0.15f, y + 0.15f, w, h);
                    if (InField(r.center, Mathf.Max(w, h) * 0.5f + 0.2f)) continue;
                    if (!RectClear(r, 0.5f)) continue;
                    Fields.Add(new Field { r = r, c = fieldColors[rng.Next(fieldColors.Length)] });
                }

            // Ormanlar: rastgele kümeler
            for (int k = 0; k < 28; k++)   // performans: orman sayısı sınırlı
            {
                var c = new Vector2((float)rng.NextDouble() * (Width + 2 * ring) - ring, (float)rng.NextDouble() * (Height + 2 * ring) - ring);
                if (NearCountryFeature(c, 2f)) { k--; if (rng.NextDouble() < 0.02) break; continue; }
                float radius = 2f + (float)rng.NextDouble() * 4f;
                int n = (int)(radius * radius * 1.6f) + rng.Next(8);   // sık orman: alan büyüdükçe ağaç artar
                for (int i = 0; i < n; i++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = Mathf.Sqrt((float)rng.NextDouble()) * radius;
                    var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                    if (NearCountryFeature(p, 0.4f) || InField(p, 0.1f)) continue;
                    CountryTrees.Add(new Vector3(p.x, p.y, 0.3f + (float)rng.NextDouble() * 0.3f));
                }
            }
            // Çit ağaçları: bazı tarlaların bir kenarı boyunca sık ağaç sırası (tarlalar arasındaki çitler gibi).
            // Eskiden çayıra rastgele 160 tek ağaç serpiliyordu; alakasız yerlerde duruyorlardı.
            foreach (var f in Fields)
            {
                if (!f.Cokgen) continue;
                // Çokgen tarla: bir kenarı boyunca (dışına) ağaç sırası
                if (rng.NextDouble() > 0.4) continue;
                int n = f.kose.Length, i0 = rng.Next(n);
                Vector2 a = f.kose[i0], b = f.kose[(i0 + 1) % n];
                var e = b - a;
                float L = e.magnitude;
                if (L < 1.2f) continue;
                var yon = e / L;
                var dis = new Vector2(yon.y, -yon.x);            // çokgen saat yönünün tersine: dışarısı kenarın sağı
                for (float d = 0.3f; d < L - 0.2f; d += 0.65f + (float)rng.NextDouble() * 0.3f)
                {
                    var p = a + yon * d + dis * 0.1f;
                    if (NearCountryRoad(p, 0.45f) || KirNehirYakin(p, 0.5f) || KenteUzaklik(p) < 1.6f) continue;
                    CountryTrees.Add(new Vector3(p.x, p.y, 0.24f + (float)rng.NextDouble() * 0.18f));
                }
            }
            foreach (var f in Fields)
            {
                if (f.Cokgen) continue;
                if (rng.NextDouble() > 0.35) continue;
                int kenar = rng.Next(4);
                var r = f.r;
                Vector2 a = kenar == 0 ? new Vector2(r.xMin, r.yMin - 0.12f) : kenar == 1 ? new Vector2(r.xMin, r.yMax + 0.12f)
                          : kenar == 2 ? new Vector2(r.xMin - 0.12f, r.yMin) : new Vector2(r.xMax + 0.12f, r.yMin);
                Vector2 yon = kenar < 2 ? Vector2.right : Vector2.up;
                float boy = kenar < 2 ? r.width : r.height;
                for (float d = 0.3f; d < boy - 0.2f; d += 0.65f + (float)rng.NextDouble() * 0.3f)
                {
                    var p = a + yon * d;
                    if (NearCountryFeature(p, 0.5f)) continue;
                    CountryTrees.Add(new Vector3(p.x, p.y, 0.24f + (float)rng.NextDouble() * 0.18f));
                }
            }
            // Nehir kıyısında ağaç sıraları (kıvrımı belli etsin)
            for (int side = -1; side <= 1 && !Organik; side += 2)
                for (float d = 1.5f; d < ring; d += 0.9f)
                {
                    float x = side < 0 ? -0.5f - d : Width - 0.5f + d;
                    float cy = RiverCurveY(x);
                    for (int bank = -1; bank <= 1; bank += 2)
                    {
                        if (rng.NextDouble() > 0.6) continue;
                        var p = new Vector2(x + (float)rng.NextDouble() * 0.5f, cy + bank * (2.1f + (float)rng.NextDouble() * 1.2f));
                        if (NearCountryRoad(p, 0.5f)) continue;
                        CountryTrees.Add(new Vector3(p.x, p.y, 0.3f + (float)rng.NextDouble() * 0.3f));
                    }
                }

            // Organik: şehir dışındaki nehir kollarının kıyısında seyrek söğüt/kavak sırası (kıvrımı belli etsin)
            if (Organik)
                foreach (var kol in KirNehir)
                    for (int k = 4; k + 1 < kol.P.Count; k += 2)
                    {
                        var a = kol.P[k]; var b = kol.P[k + 1];
                        if (DisariUzaklik(a) < (plan ? 0.5f : Banliyo + 0.5f)) continue;
                        var yon = (b - a).normalized; var yan = new Vector2(-yon.y, yon.x);
                        for (int kiyi = -1; kiyi <= 1; kiyi += 2)
                        {
                            if (rng.NextDouble() > 0.42) continue;
                            var q = a + yan * kiyi * (kol.G[k] * 0.5f + 0.45f + (float)rng.NextDouble() * 0.9f);
                            if (NearCountryRoad(q, 0.6f) || InField(q, 0.15f)) continue;
                            CountryTrees.Add(new Vector3(q.x, q.y, 0.32f + (float)rng.NextDouble() * 0.3f));
                        }
                    }

            // Şehrin kenarı boyunca ağaç sırası (şehir kırdan yumuşakça ayrılsın). Plan kırsalı veriyorsa şehrin kenarı dikdörtgen
            // değil: kenar ağaçlarını orman ve çitler verir
            for (float x = -1.5f; x < Width + 1f && !plan; x += 1.1f)
            {
                if (rng.NextDouble() < 0.22) TryEdgeTree(new Vector2(x, -1.6f), rng);
                if (rng.NextDouble() < 0.22) TryEdgeTree(new Vector2(x, Height + 0.6f), rng);
            }
            for (float y = -1.5f; y < Height + 1f && !plan; y += 1.1f)
            {
                if (rng.NextDouble() < 0.22) TryEdgeTree(new Vector2(-1.6f, y), rng);
                if (rng.NextDouble() < 0.22) TryEdgeTree(new Vector2(Width + 0.6f, y), rng);
            }

            // Çiftlikler: yollara yakın (plan kırsalı veriyorsa plandaki yerlerinde: yukarıda eklendi)
            if (!plan)
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
            if (Organik ? KirNehirYakin(p, 0.6f) : Mathf.Abs(p.y - RiverCurveY(p.x)) < 2f) return;
            if (InField(p, 0.2f)) return;
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

        /// <summary>Kar örtüsü ve ıslaklık (0–1): 3D kır zemini ve parselleri de bunlara göre renk değiştirir.</summary>
        public float KarOrtusu => snowCover;
        public float Islaklik => wetness;
        float nightAmt;                          // gece (0–1): 2D zemin, kır ve süsler de kararır (3D ışıkla uyumlu)

        /// <summary>
        /// Gece kararması: 3D şehir güneşle kararır ama 2D çizilen zemin, tarlalar, nehir ve kır ışıktan etkilenmez.
        /// Gorsel3D her an gecenin oranını verir; renkler lacivert gece tonuna kayar.
        /// </summary>
        public void SetNight(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (Mathf.Abs(amount - nightAmt) < 0.02f && !(amount == 0f && nightAmt > 0f)) return;
            nightAmt = amount;
            RefreshColors();
        }

        public static readonly Color NightMul = new Color(0.36f, 0.34f, 0.50f);   // mor-lavanta gece (3D ortam ışığıyla aynı ton)
        Color Night(Color c)
        {
            if (nightAmt <= 0f) return c;
            var n = new Color(c.r * NightMul.r, c.g * NightMul.g, c.b * NightMul.b, c.a);
            return Color.Lerp(c, n, nightAmt * 0.85f);
        }

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
            // Kar örtüsü en fazla ~0,55: yoğun karda zemin ve kır tamamen beyaz olsun (yollar mul ile yarı)
            return Color.Lerp(c, SnowColor, Mathf.Clamp01(snowCover / 0.5f) * mul);
        }
        readonly List<Vector2Int> waterTiles = new List<Vector2Int>();
        SpriteRenderer hq;
        Transform root, roofRoot, countryRoot;
        readonly List<Vector2Int> customerTiles = new List<Vector2Int>();
        readonly HashSet<Vector2Int> customerSet = new HashSet<Vector2Int>();

        /// <summary>Müşterilerin oturduğu (sipariş verebilen) bina karesi mi?</summary>
        public bool IsCustomerBuilding(Vector2Int t) => customerSet.Contains(t);
        readonly List<Vector2Int> shopTiles = new List<Vector2Int>();
        readonly HashSet<Vector2Int> shopSet = new HashSet<Vector2Int>();
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
        /// <summary>Kare → dünya. Organik şehirde z arazinin yüksekliği (−Z yukarı): kuryeler, işaretler, etiketler tepeye oturur.</summary>
        public static Vector3 ToWorld(Vector2 t) => new Vector3(t.x, t.y, Organik ? Arazi.Z(t.x, t.y) : 0f);
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
                    // Organik şehirde AVM karelerinin hepsine mağaza binası kurulur: dükkân oraya konmaz (binasız kalırdı)
                    if (ShopZone(z) && !(Organik && z == Zone.Mall)) { shopTiles.Add(t); shopSet.Add(t); }
                }
            }

            GenerateCountryside();
            BuildCountryside();
            BuildDecor();

            // Merkez ofis: Downtown'un ortasındaki meydanda, güneydeki yola bakar
            HQTile = Organik ? OrganikMerkezOfis() : new Vector2Int(PlazaCenter.x, PlazaCenter.y - 1);
            HQDoor = GetDoor(HQTile);
            for (int x = PlazaCenter.x - 1; x <= PlazaCenter.x + 1; x++)
                for (int y = PlazaCenter.y - 1; y <= PlazaCenter.y + 1; y++) reserved.Add(new Vector2Int(x, y));
            hq = SpriteFactory.Create("Merkez Ofis", root, false, CompanyProfile.Primary, ToWorld(HQTile), 0.9f, 2);

            PlaceShops();
            BuildMovers();
            RefreshColors();
        }

        /// <summary>Organik şehirde merkez ofis: meydana en yakın, yola bakan yapı arsası.</summary>
        Vector2Int OrganikMerkezOfis()
        {
            var m = PlazaCenter;
            var best = m;
            float bd = float.MaxValue;
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    if (RoadAt(x, y) || WaterAt(x, y) || ZoneOf(x, y) == Zone.Park || !IsFrontage(x, y)) continue;
                    if (new Vector2Int(x, y) == m) continue;
                    float d = (new Vector2(x, y) - (Vector2)m).sqrMagnitude;
                    if (d < bd) { bd = d; best = new Vector2Int(x, y); }
                }
            return best;
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
            kirSprite.Add(Deco("Çayır", countryRoot, false, CountryColor, new Vector2((Width - 1) / 2f, (Height - 1) / 2f),
                 new Vector2(Width + pad * 2f, Height + pad * 2f), -10, false));
            foreach (var f in Fields)
            {
                var ts = Deco("Tarla", countryRoot, false, f.c, f.ParselMerkez, f.ParselBoyut, -9, false);
                if (f.Cokgen) ts.transform.rotation = Quaternion.Euler(0f, 0f, f.aci);
                kirSprite.Add(ts);
            }
            // Nehir şehrin iki yanından kıvrılarak devam eder: eğriyi kısa, döndürülmüş parçalarla çiz
            // (önce biraz geniş çimen kıyı, üstüne su)
            const float step = 0.7f;
            for (int side = -1; side <= 1 && !Organik; side += 2)
                for (float d = 0f; d < pad; d += step)
                {
                    float xa = side < 0 ? -0.5f - d : Width - 0.5f + d;
                    float xb = xa + side * step;
                    var pa = new Vector2(xa, RiverCurveY(xa));
                    var pb = new Vector2(xb, RiverCurveY(xb));
                    var mid = (pa + pb) / 2f;
                    float len = Vector2.Distance(pa, pb) + 0.25f;
                    float ang = Mathf.Atan2(pb.y - pa.y, pb.x - pa.x) * Mathf.Rad2Deg;
                    var bank = Deco("Swan River kıyısı", countryRoot, false, RiverBankColor, mid, new Vector2(len, 3.9f), -9, false);
                    bank.transform.rotation = Quaternion.Euler(0f, 0f, ang);
                    var water = Deco("Swan River", countryRoot, false, WaterColor, mid, new Vector2(len, 3f), -8, false);
                    water.transform.rotation = Quaternion.Euler(0f, 0f, ang);
                }
            // Organik: nehrin şehir dışındaki kolları (2D düz harita görünümü)
            if (Organik)
                foreach (var kol in KirNehir)
                    for (int k = 0; k + 1 < kol.P.Count; k++)
                    {
                        Vector2 pa = kol.P[k], pb = kol.P[k + 1];
                        var mid = (pa + pb) / 2f;
                        float len = Vector2.Distance(pa, pb) + 0.3f;
                        float ang = Mathf.Atan2(pb.y - pa.y, pb.x - pa.x) * Mathf.Rad2Deg;
                        var bank = Deco("Swan River kıyısı", countryRoot, false, RiverBankColor, mid, new Vector2(len, kol.G[k] + 0.9f), -9, false);
                        bank.transform.rotation = Quaternion.Euler(0f, 0f, ang);
                        var water = Deco("Swan River", countryRoot, false, WaterColor, mid, new Vector2(len, kol.G[k]), -8, false);
                        water.transform.rotation = Quaternion.Euler(0f, 0f, ang);
                    }
            // Plan kırsalı veriyorsa şehir dışı yolları OrganikSehir3D çizer (eğri şerit, araziye oturur): 2D kutu yok
            if (!KirPlanli)
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

                    // Plan dikdörtgeninin içindeki kır kareleri: 2D süs yok (ağaçları orman ve çitler verir)
                    if (KirPlanli && z == Zone.Country) continue;
                    if (BridgeAt(x, y))
                    {
                        // Plan sürüm 2'de köprüler her yönde olabilir: korkuluğu OrganikSehir3D yol boyunca kurar
                        if (!(Organik && SehirPlani.Surum >= 2))
                        {
                            var rail = new Color(0.82f, 0.80f, 0.76f);
                            Deco("Korkuluk", root, false, rail, p + new Vector2(-0.46f, 0f), new Vector2(0.06f, 1.01f), 3);
                            Deco("Korkuluk", root, false, rail, p + new Vector2(0.46f, 0f), new Vector2(0.06f, 1.01f), 3);
                        }
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
                    if (!Organik && WaterAt(x, y) && y == RiverBlockY * Block + 1 && x < 12 && x % 4 == 2)
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
                    else if (z == Zone.Park || z == Zone.Houses || z == Zone.Country || z == Zone.River || (!IsFrontage(x, y) && z != Zone.Plaza && z != Zone.Mall && z != Zone.OldTown))
                    {
                        // Ağaçlar (park ve kır daha sık)
                        int n = z == Zone.Park || z == Zone.Country ? 3 + rng.Next(2) : 1 + rng.Next(2);
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
                            // Park ortası: City Park'ta anıt, diğerlerinde gölet
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
            if (Organik) return;    // nehir kıvrımlı ve tren yolu yok: tekne ve tren şimdilik sadece ızgara şehirde
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
                // Şehir dışında nehir kıvrılır: tekne eğriyi izler ve akış yönüne döner
                float cy = RiverCurveY(b.x);
                float slope = (RiverCurveY(b.x + 0.2f) - RiverCurveY(b.x - 0.2f)) / 0.4f;
                b.t.localPosition = new Vector3(b.x, cy + (b.y - RiverY), 0f);
                b.t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan(slope) * Mathf.Rad2Deg);
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
            if (shimmerTimer > 0.12f && ground != null && !zemin3D)      // 3D zemin varken 2D su kareleri kapalı
            {
                shimmerTimer = 0f;
                float tm = Time.time;
                foreach (var w in waterTiles)
                {
                    float k = 0.5f + 0.5f * Mathf.Sin(tm * 0.9f + w.x * 0.7f + w.y * 1.3f);
                    var c = Color.Lerp(WaterColor, Color.Lerp(WaterColor, Color.white, 0.12f), k * 0.6f);
                    if (!IsUnlocked(w)) c = LockedTint(c);
                    ground[w.x, w.y].color = Night(c);
                }
            }
        }

        /// <summary>Kilitli bölgenin rengi: koyu ve gri (bölge yine okunur, ama açık olmadığı belli).</summary>
        public static Color LockedTint(Color c)
        {
            float g = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            // Kilitli bölge: düz koyu gri değil, rengi solmuş ama seçilebilen şehir (referanstaki gibi canlı görünsün)
            return Color.Lerp(Color.Lerp(c, new Color(g, g, g), 0.5f), Color.black, 0.16f);
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
                    // Plan sürüm 2: dükkânlar çarşı caddelerine (high street); teras mahalleleri (eski şehir bölgesi) konut ağırlıklı
                    if (Organik && SehirPlani.Surum >= 2 ? ZoneOf(t) == Zone.Mixed : PreferredShopZone(ZoneOf(t))) preferred.Add(t);
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

        /// <summary>Kendi marketin (dark store) için açık bölgede boş bir dükkan karesi bulur; tile verilirse (kayıttan) orayı kullanır.</summary>
        /// <summary>Açık bölgelerde boş bir dükkan yeri (rastgele; yoksa null). İşletme teslimatı ortakları için.</summary>
        public Vector2Int? RastgeleBosDukkanYeri()
        {
            var bos = new List<Vector2Int>();
            foreach (var t in shopTiles) if (!reserved.Contains(t) && IsUnlocked(t)) bos.Add(t);
            if (bos.Count == 0) return null;
            return bos[Random.Range(0, bos.Count)];
        }

        public Shop AddOwnShop(ShopDef def, Vector2Int? at = null)
        {
            Vector2Int tile;
            // Kayıttan gelen yer bu haritada geçerli değilse (şehir planı değişmiş: yol, su, kır, park ya da bir dükkânın arsası) en
            // yakın boş dükkân yerine taşınır; hiç yer yoksa aşağıdaki seçim devralır. Kendi dükkânlarımız aynı arsayı paylaşabilir
            // (sanal mutfağın markaları tek mutfakta): dolu saymak için arsada sözleşmeli bir dükkân olmalı.
            if (at.HasValue && (!shopSet.Contains(at.Value) || Shops.Exists(q => !q.own && q.tile == at.Value)))
            {
                var yakin = new Vector2Int(-1, -1);
                float enYakin = float.MaxValue;
                foreach (var t in shopTiles)
                {
                    if (reserved.Contains(t)) continue;
                    float d = Vector2Int.Distance(t, at.Value) + (IsUnlocked(t) ? 0f : 1000f);
                    if (d < enYakin) { enYakin = d; yakin = t; }
                }
                Debug.LogWarning($"Kurye Tycoon: kayıttaki dükkân yeri {at.Value} bu şehir planında geçersiz; {(yakin.x >= 0 ? yakin.ToString() : "yeni bir yer")} kullanılıyor.");
                at = yakin.x >= 0 ? yakin : (Vector2Int?)null;
            }
            if (at.HasValue) tile = at.Value;
            else
            {
                var best = new Vector2Int(-1, -1);
                float bestScore = float.MaxValue;
                foreach (var t in shopTiles)
                {
                    if (reserved.Contains(t) || !IsUnlocked(t)) continue;
                    var z = ZoneOf(t);
                    // Depo: ana caddede değil, merkeze yakın ve ucuz bir yer (depo / karma bölge tercih)
                    float score = Vector2Int.Distance(t, HQTile) + (z == Zone.Warehouse || z == Zone.Mixed ? 0f : 6f);
                    if (score < bestScore) { bestScore = score; best = t; }
                }
                if (best.x < 0) return null;
                tile = best;
            }
            reserved.Add(tile);
            var shop = new Shop
            {
                def = def,
                tile = tile,
                door = GetDoor(tile),
                contracted = true,
                exclusive = true,
                own = true,
                satisfaction = 100f,
                contractEndDay = int.MaxValue / 2
            };
            shop.sr = SpriteFactory.Create(def.name, root, false, def.color, ToWorld(tile), 0.8f, 2);
            Shops.Add(shop);
            return shop;
        }

        // 3D'de kır zemini (çayır, tarlalar, şehir kenarındaki kır kareleri) City3D'nin ışık alan dokulu zemininden gelir.
        // Bu 2D parçalar gizlenir; sadece kar yağınca zeminin üstünü beyaz örtmek için kar oranında görünür olurlar.
        readonly List<SpriteRenderer> kirSprite = new List<SpriteRenderer>();
        bool kir3D;

        /// <summary>City3D dokulu kır zeminini kurunca çağırır.</summary>
        /// <summary>Kar örtüsü (0–1) ve ıslaklık (0–1): 3D şehir zemini (OrganikZemin) dokusuna işler.</summary>
        public float KarMiktari => snowCover;
        bool zemin3D;

        /// <summary>Organik şehrin 3D zemini kuruldu: düz 2D kareler gizlenir (nehir yatağı çukurunu örtmesinler).</summary>
        public void Zemin3D()
        {
            zemin3D = true;
            if (ground != null)
                foreach (var sr in ground) if (sr != null) sr.enabled = false;
            RefreshColors();
        }

        public void Kir3DZemin()
        {
            kir3D = true;
            RefreshColors();
        }

        void Kir2DGizle()
        {
            float a = Mathf.Clamp01(snowCover * 2f);   // kar örtüsü en çok ~0,55: yoğun karda kır tamamen beyaz
            void Ayarla(SpriteRenderer sr)
            {
                if (sr == null) return;
                var c = sr.color; c.a = a; sr.color = c;
                sr.enabled = a > 0.01f;
            }
            foreach (var sr in kirSprite) Ayarla(sr);
            // Organik şehrin 3D zemini varken kareler kapalı kalır (kar zemin dokusuna ve çayır örtüsüne işlenir)
            for (int x = 0; x < Width && !zemin3D; x++)
                for (int y = 0; y < Height; y++)
                    if (ZoneOf(x, y) == Zone.Country && !RoadAt(x, y) && !WaterAt(x, y)) Ayarla(ground[x, y]);
        }

        public void RefreshColors()
        {
            if (ground == null) return;
            // Organik şehrin 3D zemini varken 2D kareler kapalıdır (Zemin3D): 19 296 karenin rengini boşuna hesaplama
            for (int x = 0; x < Width && !zemin3D; x++)
                for (int y = 0; y < Height; y++)
                {
                    var c = groundBase[x, y];
                    if (!WaterAt(x, y)) c = Snowy(c, RoadAt(x, y) ? 0.5f : 1f);
                    if (!IsUnlocked(x, y)) c = LockedTint(c);
                    ground[x, y].color = Night(c);
                }
            if (zemin3D) OrganikZemin.Renkle(this);

            foreach (var d in decor)
            {
                if (d.sr == null) continue;
                var dc = Snowy(d.c, 0.8f);
                d.sr.color = Night(IsUnlocked(d.t) ? dc : LockedTint(dc));
            }
            foreach (var f in freeDecor)
                if (f.sr != null) f.sr.color = Night(Snowy(f.c, 0.9f));

            foreach (var s in Shops)
            {
                Color c = s.def.color;
                if (!IsUnlocked(s.tile)) c = Color.Lerp(LockedTint(c), Color.black, 0.3f);
                else if (!s.contracted) c = Color.Lerp(c, new Color(0.55f, 0.56f, 0.60f), 0.6f);
                s.sr.color = c;
            }

            if (hq != null) hq.color = CompanyProfile.Primary;
            if (kir3D) Kir2DGizle();
        }

        /// <summary>Kayıttan yüklerken açık bölge seviyesini geri koyar.</summary>
        public void RestoreUnlockLevel(int level)
        {
            UnlockLevel = Mathf.Clamp(level, 1, MaxLevel);
            RefreshColors();
        }

        /// <summary>
        /// Bölgenin açılış bedeli: şirket büyüdükçe artar (Denge.Olcekle), ama platform paketi gibi çarpanın yarısıyla: bölge,
        /// sıradaki kurye paketinin ön koşulu. 16.11 testinde tam çarpanla West Harbour 4 220 €, Green Hill 8 620 € tuttu; paketle
        /// birlikte 32 ve 45 kuryede 17–18 gün beklendi (hedef 10–13). Yarım çarpanla 32'de 11 güne indi, 45'te 18'de kaldı:
        /// Green Hill 0,9 M'ye indi (16.11 akşam).
        /// </summary>
        public static long BolgeBedeli(int seviye) => Denge.Olcekle(UnlockCost[Mathf.Clamp(seviye, 0, UnlockCost.Length - 1)], 0.5f);

        public bool TryUnlockNext()
        {
            if (UnlockLevel >= MaxLevel) return false;
            long cost = BolgeBedeli(UnlockLevel + 1);
            if (!EconomyManager.I.TrySpendYatirim(cost, $"Yeni bölge: {DistrictNames[UnlockLevel + 1]}", ExpenseCategory.Bolge)) return false;
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
            if (!EconomyManager.I.TrySpendYatirim(s.def.contractCost, $"Sözleşme: {s.Name}", ExpenseCategory.Sozlesme)) return false;
            s.contracted = true;
            s.contractStartDay = GameClock.I.Day;
            s.contractEndDay = GameClock.I.Day + (RivalCompany.I != null ? RivalCompany.I.contractDays : 182);
            s.satisfaction = Mathf.Max(s.satisfaction, 60f);
            RefreshColors();
            if (GoalManager.I != null) GoalManager.I.OnContract();
            EventManager.I.Notify($"{s.Name} ile sözleşme imzalandı (komisyon %{s.Commission * 100f:0.#}).");
            return true;
        }

        // u65: şehir büyüdü (müşteri binası 1 419 → 3 106, 90 × 88 → 144 × 134). Sipariş sayısı bina sayısına bağlı değil (dükkân
        // başına üretilir), ama müşteri seçimi bütün binalardan rastgele deneyerek yapılıyor: deneme sayısı sabit kalsa küçük
        // yarıçaplı / tek semtli seçimler daha sık tutmaz, "şehirde herhangi bir bina"ya düşerdi. Deneme sayısı bina sayısıyla
        // ölçeklenir (u64 şehri = 1 420 bina referans), böylece tutma olasılığı eski şehirdeki gibi kalır.
        const int ReferansBina = 1420;
        int Deneme(int taban) => Mathf.Max(taban, (int)((long)taban * customerTiles.Count / ReferansBina));

        /// <summary>
        /// Uzak sipariş menzili (Manhattan, kare): büyük planda "şehrin herhangi bir yerinden" gelen siparişler (OrderManager
        /// farOrderChance, kargo, işletme teslimatı) dükkâna en çok bu kadar uzak binadan gelir. Sınırsızken büyük şehirde uzak
        /// siparişin ortalama yolu 46,5 → 68 kareye çıkıyordu (plan_olc.py, seviye 5); 54 ile 43,7 (seviye 3: 37,2 → 41,7).
        /// u64 ve daha küçük planlarda (W × H ≤ 12 000) sınır yok. Tools/SehirPlani/plan_olc.py aynı değeri kullanır.
        /// </summary>
        public const int UzakSiparisMenzili = 54;
        const int UzakMenzilEsigi = 12000;

        public Vector2Int RandomCustomerTile(Vector2Int avoid)
        {
            bool sinirli = Organik && Width * Height > UzakMenzilEsigi && InBounds(avoid);
            int deneme = Deneme(80);
            for (int i = 0; i < deneme; i++)
            {
                var t = customerTiles[Random.Range(0, customerTiles.Count)];
                if (!IsUnlocked(t) || t == avoid || reserved.Contains(t)) continue;
                // son çeyrekte sınır kalkar: menzilde hiç açık bina yoksa (ör. şehrin ucundaki depo) sipariş yine bir binaya gitsin
                if (sinirli && i < deneme * 3 / 4 && Manhattan(t, avoid) > UzakSiparisMenzili) continue;
                return t;
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
            int deneme = Deneme(120);
            for (int i = 0; i < deneme; i++)
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

        /// <summary>
        /// Organik şehirde kare kare (merdiven gibi) yolu düzleştirir: aradaki köşeler, düz çizgi hep yol karelerinin üstünden
        /// geçiyorsa atlanır. Kuryeler çapraz ve eğri sokaklarda zikzak çizmeden, yol boyunca yürür. Izgara şehirde değişmez.
        /// </summary>
        public static List<Vector2Int> Yumusat(List<Vector2Int> p)
        {
            if (!Organik || p == null || p.Count < 3) return p;
            var r = new List<Vector2Int>(p.Count) { p[0] };
            int a = 0;
            while (a < p.Count - 1)
            {
                int j = a + 1;
                // En uzak görünen noktayı bul (en çok 12 adım ileri: uzun düz yolda bile hesap ucuz kalsın)
                for (int k = Mathf.Min(p.Count - 1, a + 12); k > a + 1; k--)
                    if (DuzGecer(p[a], p[k], k == p.Count - 1)) { j = k; break; }
                r.Add(p[j]);
                a = j;
            }
            return r;
        }

        static bool DuzGecer(Vector2Int a, Vector2Int b, bool sonNokta)
        {
            Vector2 pa = a, pb = b;
            float L = Vector2.Distance(pa, pb);
            int n = Mathf.CeilToInt(L / 0.2f);
            for (int i = 1; i < n; i++)
            {
                var q = Vector2.Lerp(pa, pb, i / (float)n);
                int x = Mathf.RoundToInt(q.x), y = Mathf.RoundToInt(q.y);
                if (sonNokta && x == b.x && y == b.y) continue;      // hedef bina karesi yol değil
                if (!RoadAt(x, y)) return false;
            }
            return true;
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

            // u65: yol karesi 1 995 → 3 981. Sözlük ve kuyruk yerine kare numaralı diziler (damgalı: her aramada temizlenmez):
            // aynı komşu sırası, aynı sonuç; arama başına bellek ayrılmaz.
            int w = Width, h = Height, n = w * h;
            if (!InBounds(from) || !InBounds(to))
            {
                result.Add(from);
                result.Add(to);
                return result;
            }
            if (yolOnceki == null || yolOnceki.Length != n)
            {
                yolOnceki = new int[n];
                yolDamga = new int[n];
                yolKuyruk = new int[n];
                yolDamgaNo = 0;
            }
            if (yolDamgaNo == int.MaxValue) { System.Array.Clear(yolDamga, 0, n); yolDamgaNo = 0; }
            yolDamgaNo++;
            int bas = from.y * w + from.x, hedef = to.y * w + to.x;
            int kb = 0, ks = 0;
            yolKuyruk[ks++] = bas;
            yolDamga[bas] = yolDamgaNo;
            yolOnceki[bas] = bas;
            while (kb < ks)
            {
                int cur = yolKuyruk[kb++];
                if (cur == hedef) break;
                int cx = cur % w, cy = cur / w;
                for (int di = 0; di < Dirs.Length; di++)
                {
                    int nx = cx + Dirs[di].x, ny = cy + Dirs[di].y;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int ni = ny * w + nx;
                    if (yolDamga[ni] == yolDamgaNo) continue;
                    if (ni != hedef && !(IsRoad(nx, ny) && IsUnlocked(nx, ny))) continue;
                    yolDamga[ni] = yolDamgaNo;
                    yolOnceki[ni] = cur;
                    yolKuyruk[ks++] = ni;
                }
            }

            if (yolDamga[hedef] != yolDamgaNo)
            {
                result.Add(from);
                result.Add(to);
                return result;
            }

            for (int p = hedef; p != bas; p = yolOnceki[p]) result.Add(new Vector2Int(p % w, p / w));
            result.Add(from);
            result.Reverse();
            return result;
        }

        // FindPath çalışma dizileri (kare numarası = y × Width + x)
        int[] yolOnceki, yolDamga, yolKuyruk;
        int yolDamgaNo;
    }
}
