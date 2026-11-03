using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Siparişteki bir ürün.</summary>
    public struct OrderItem
    {
        public string name;
        public int qty;
        public int unitPrice;
        public int Total => qty * unitPrice;
    }

    /// <summary>
    /// Şehrin adres sistemi. Tutarlıdır: aynı bina her zaman aynı sokakta, aynı numarada, aynı kat sayısındadır.
    ///  • Dikey yollar (x = 0, 4, 8 …) "Cadde", yatay yollar (y = 0, 4, 8 …) "Sokak".
    ///  • Bina numarası, kapısının açıldığı yol boyunca konumuna göre (bir taraf tek, karşı taraf çift).
    ///  • Kat sayısı bölgeye göre: Merkez apartmanlar (4–10), Doğu (2–6), Sanayi & Kampüs müstakil/alçak (1–3).
    ///  • 5+ katlı binalarda asansör var; 7+ katlılar iki bloklu (A, B).
    /// </summary>
    public static class Addresses
    {
        static readonly string[] AvenueNames =
            { "Kossuth", "Petőfi", "Rákóczi", "Széchenyi", "Dózsa György", "Arany János", "Bethlen", "Jókai", "Vörösmarty" };
        static readonly string[] StreetNames =
            { "Kölcsey", "Ady Endre", "Hunyadi", "Madách", "Bocskai", "Szent István" };

        public static bool IsAvenue(Vector2Int road) => road.x % CityMap.Block == 0 && road.y % CityMap.Block != 0;

        public static string StreetOf(Vector2Int road)
        {
            if (IsAvenue(road)) return AvenueNames[(road.x / CityMap.Block) % AvenueNames.Length] + " Caddesi";
            return StreetNames[(road.y / CityMap.Block) % StreetNames.Length] + " Sokak";
        }

        /// <summary>Kapı numarası: yol boyunca sıra, yolun bir tarafı tek, diğer tarafı çift.</summary>
        public static int Number(Vector2Int building, Vector2Int door)
        {
            if (IsAvenue(door)) return building.y * 2 + (building.x > door.x ? 2 : 1);
            return building.x * 2 + (building.y > door.y ? 2 : 1);
        }

        static System.Random TileRng(Vector2Int t) => new System.Random(t.x * 73856093 ^ t.y * 19349663 ^ 0x5bd1e995);

        public static int Floors(Vector2Int t)
        {
            var r = TileRng(t);
            if (t.x <= CityMap.UnlockMaxX[1]) return 4 + r.Next(7);   // 4–10
            if (t.x <= CityMap.UnlockMaxX[2]) return 2 + r.Next(5);   // 2–6
            return 1 + r.Next(3);                                      // 1–3
        }

        public static bool HasElevator(Vector2Int t) => Floors(t) >= 5;
        public static int Blocks(Vector2Int t) => Floors(t) >= 7 ? 2 : 1;
        public static int FlatsPerFloor(Vector2Int t) => Floors(t) >= 4 ? 4 : 2;

        public static string FloorText(int floor) => floor == 0 ? "Zemin kat" : $"{floor}. kat";

        static readonly string[] CustomerNotes =
        {
            "", "", "", "", "", "",
            "Zili çalmayın, bebek uyuyor.", "Kapıya bırakın, lütfen.", "Kapıda ödeme yok, kartla ödendi.",
            "Arka girişten gelin.", "Köpek var, korkmayın.", "Gelince arayın, zil bozuk.",
            "Interkomda soyadıma basın.", "Asansör küçük, dikkat.", "Teşekkürler, iyi çalışmalar!"
        };

        public static string RandomNote() => CustomerNotes[Random.Range(0, CustomerNotes.Length)];
    }

    /// <summary>Dükkan menüleri: sipariş içeriği bunlardan oluşur, sepet tutarı ürünlerin toplamıdır.</summary>
    public static class Menus
    {
        struct M { public string n; public int p; public M(string n, int p) { this.n = n; this.p = p; } }

        static readonly Dictionary<string, M[]> menus = new Dictionary<string, M[]>
        {
            { "Burger Queen", new[] { new M("Whooper menü", 2890), new M("Cheeseburger", 990), new M("Tavuk burger", 1490), new M("Büyük patates", 890),
                                      new M("Soğan halkası", 790), new M("Kola 0,5 L", 590), new M("Milkshake", 990) } },
            { "Döner Kral",   new[] { new M("Dürüm döner", 2490), new M("Porsiyon döner", 3290), new M("İskender", 3990), new M("Lahmacun", 1490),
                                      new M("Ayran", 490), new M("Baklava (4 dilim)", 1290), new M("Mercimek çorbası", 990) } },
            { "Starbocks",    new[] { new M("Latte", 1490), new M("Cappuccino", 1390), new M("Frappuccino", 1890), new M("Kruvasan", 990),
                                      new M("Cheesecake", 1490), new M("Muffin", 890), new M("Sandviç", 1690) } },
            { "Szpar Express",new[] { new M("Ekmek", 590), new M("Süt 1 L", 450), new M("Yumurta (10'lu)", 990), new M("Peynir", 1290),
                                      new M("Su 6×1,5 L", 1290), new M("Elma 1 kg", 690), new M("Makarna", 490), new M("Kahve 250 g", 1990),
                                      new M("Deterjan", 2490), new M("Tuvalet kağıdı (8'li)", 1890) } },
            { "Market 24",    new[] { new M("Ekmek", 620), new M("Süt 1 L", 490), new M("Bira (4'lü)", 1590), new M("Cips", 690),
                                      new M("Kola 1,5 L", 790), new M("Dondurma", 1290), new M("Sigara kağıdı", 290), new M("Çikolata", 450),
                                      new M("Yumurta (10'lu)", 1090), new M("Peynir", 1390) } },
            { "McDanold's",   new[] { new M("Big Mec menü", 2790), new M("McNugget (9'lu)", 1890), new M("Patates", 790), new M("McFlurry", 890),
                                      new M("Kola 0,5 L", 590), new M("Çizburger", 890), new M("Elmalı turta", 490) } },
            { "Pizza Hat",    new[] { new M("Margarita pizza", 3490), new M("Karışık pizza", 4290), new M("Pepperoni pizza", 3990),
                                      new M("Sarımsaklı ekmek", 990), new M("Tavuk kanat (6'lı)", 1990), new M("Kola 1 L", 890) } },
            { "InterSzpar",   new[] { new M("Su 6×1,5 L", 1190), new M("Süt 1 L", 420), new M("Ekmek", 550), new M("Tavuk göğsü 1 kg", 2990),
                                      new M("Kıyma 500 g", 2190), new M("Patates 2 kg", 990), new M("Pirinç 1 kg", 890), new M("Zeytinyağı", 3490),
                                      new M("Deterjan 3 L", 4490), new M("Bebek bezi", 5990), new M("Meyve suyu 1 L", 690), new M("Domates 1 kg", 1190) } },
            { "KFG",          new[] { new M("Kova (8 parça)", 5490), new M("Zinger burger", 1990), new M("Strips (5'li)", 1890), new M("Patates", 790),
                                      new M("Coleslaw", 690), new M("Kola 0,5 L", 590) } },
            { "Sushi Samurai",new[] { new M("California roll (8'li)", 2990), new M("Somon nigiri (4'lü)", 2490), new M("Sushi tabağı (24 parça)", 8990),
                                      new M("Miso çorbası", 990), new M("Gyoza", 1690), new M("Edamame", 990), new M("Yeşil çay", 590) } },
        };

        /// <summary>Hedef tutara yakın, gerçekçi bir sepet oluşturur. Market siparişleri çok ürünlü ve adetlidir.</summary>
        public static List<OrderItem> Generate(ShopDef shop, int target)
        {
            var list = new List<OrderItem>();
            if (!menus.TryGetValue(shop.name, out var menu))
            {
                list.Add(new OrderItem { name = "Sipariş", qty = 1, unitPrice = target });
                return list;
            }
            int total = 0;
            int guard = 0;
            while (total < target * 0.92f && guard++ < 40)
            {
                var m = menu[Random.Range(0, menu.Length)];
                int maxQty = shop.isMarket ? 4 : 2;
                int q = Random.Range(1, maxQty + 1);
                if (total + m.p * q > target * 1.15f) q = 1;
                if (total > 0 && total + m.p * q > target * 1.25f) break;
                int idx = list.FindIndex(x => x.name == m.n);
                if (idx >= 0) { var it = list[idx]; it.qty += q; list[idx] = it; }
                else list.Add(new OrderItem { name = m.n, qty = q, unitPrice = m.p });
                total += m.p * q;
            }
            return list;
        }

        public static int Sum(List<OrderItem> items)
        {
            int s = 0;
            foreach (var i in items) s += i.Total;
            return s;
        }

        public static string Text(List<OrderItem> items, int maxItems = 6)
        {
            if (items == null || items.Count == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < items.Count && i < maxItems; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(items[i].qty > 1 ? $"{items[i].qty}× {items[i].name}" : items[i].name);
            }
            if (items.Count > maxItems) sb.Append($" +{items.Count - maxItems} ürün");
            return sb.ToString();
        }
    }
}
