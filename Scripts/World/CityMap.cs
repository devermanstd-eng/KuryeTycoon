using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Izgara şehir: her 4 karede bir yol. Bölgeler para ile açılır.
    /// Bir kare = yaklaşık 200 metre.
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

        public int UnlockLevel { get; private set; } = 1;
        public int MaxLevel => 3;

        public readonly List<Shop> Shops = new List<Shop>();
        public Vector2Int HQTile { get; private set; }
        public Vector2Int HQDoor { get; private set; }

        static readonly Color RoadColor = new Color(0.20f, 0.21f, 0.25f);
        static readonly Color BuildingColor = new Color(0.36f, 0.39f, 0.45f);
        static readonly Color ParkColor = new Color(0.25f, 0.45f, 0.30f);
        static readonly Vector2Int[] Dirs = { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up };

        SpriteRenderer[,] tiles;
        SpriteRenderer hq;
        Transform root;
        readonly List<Vector2Int> buildingTiles = new List<Vector2Int>();
        readonly HashSet<Vector2Int> reserved = new HashSet<Vector2Int>();

        void Awake() { I = this; }

        public bool IsRoad(int x, int y) => x % Block == 0 || y % Block == 0;
        public bool InBounds(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < Width && p.y < Height;
        public bool IsUnlocked(int x) => x <= UnlockMaxX[UnlockLevel];
        public static Vector3 ToWorld(Vector2 t) => new Vector3(t.x, t.y, 0f);
        public static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        int RegionOf(int x) => x <= UnlockMaxX[1] ? 1 : (x <= UnlockMaxX[2] ? 2 : 3);

        public void Generate()
        {
            root = new GameObject("Şehir").transform;
            root.SetParent(transform, false);
            tiles = new SpriteRenderer[Width, Height];

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    bool road = IsRoad(x, y);
                    tiles[x, y] = SpriteFactory.Create($"Kare {x},{y}", root, false, Color.white,
                        ToWorld(new Vector2(x, y)), road ? 1f : 0.9f, 0);
                    if (!road && IsAdjacentToRoad(x, y)) buildingTiles.Add(new Vector2Int(x, y));
                }
            }

            // Merkez ofis: sol alt köşe
            HQTile = new Vector2Int(1, 1);
            HQDoor = GetDoor(HQTile);
            reserved.Add(HQTile);
            hq = SpriteFactory.Create("Merkez Ofis", root, false, CompanyProfile.Primary, ToWorld(HQTile), 0.9f, 2);

            PlaceShops();
            RefreshColors();
        }

        bool IsAdjacentToRoad(int x, int y)
        {
            foreach (var d in Dirs)
            {
                var n = new Vector2Int(x + d.x, y + d.y);
                if (InBounds(n) && IsRoad(n.x, n.y)) return true;
            }
            return false;
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
                var options = new List<Vector2Int>();
                foreach (var t in buildingTiles)
                    if (RegionOf(t.x) == def.unlockLevel && !reserved.Contains(t)) options.Add(t);
                if (options.Count == 0) continue;

                var tile = options[Random.Range(0, options.Count)];
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
            if (tiles == null) return;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Color c;
                    if (IsRoad(x, y)) c = RoadColor;
                    else if (!IsAdjacentToRoad(x, y)) c = ParkColor;
                    else c = BuildingColor;
                    if (!IsUnlocked(x)) c = Color.Lerp(c, Color.black, 0.65f);
                    tiles[x, y].color = c;
                }
            }

            foreach (var s in Shops)
            {
                Color c = s.def.color;
                if (!IsUnlocked(s.tile.x)) c = Color.Lerp(c, Color.black, 0.75f);
                else if (!s.contracted) c = Color.Lerp(c, BuildingColor, 0.6f);
                s.sr.color = c;
            }

            if (hq != null) hq.color = CompanyProfile.Primary;
        }

        public bool TryUnlockNext()
        {
            if (UnlockLevel >= MaxLevel) return false;
            long cost = UnlockCost[UnlockLevel + 1];
            if (!EconomyManager.I.TrySpend(cost, $"Yeni bölge: {DistrictNames[UnlockLevel + 1]}")) return false;
            UnlockLevel++;
            RefreshColors();
            EventManager.I.Notify($"{DistrictNames[UnlockLevel]} açıldı! Yeni dükkanlarla sözleşme yapabilirsin.");
            return true;
        }

        public bool SignContract(Shop s)
        {
            if (s.contracted || !IsUnlocked(s.tile.x)) return false;
            if (!EconomyManager.I.TrySpend(s.def.contractCost, $"Sözleşme: {s.Name}")) return false;
            s.contracted = true;
            RefreshColors();
            EventManager.I.Notify($"{s.Name} ile sözleşme imzalandı (komisyon %{s.Commission * 100f:0.#}).");
            return true;
        }

        public Vector2Int RandomCustomerTile(Vector2Int avoid)
        {
            for (int i = 0; i < 50; i++)
            {
                var t = buildingTiles[Random.Range(0, buildingTiles.Count)];
                if (IsUnlocked(t.x) && t != avoid && !reserved.Contains(t)) return t;
            }
            return new Vector2Int(2, 3);
        }

        /// <summary>Yollar üzerinde en kısa yol (BFS). Başlangıç ve bitiş dahil.</summary>
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
