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
        public char cat;      // menü kategorisi (M menü, A ana yemek, Y yan ürün, I içecek, K kahve, C çorba, T tatlı, E ekstra; marketlerde L sıvı, F kırılgan, N diğer)
        public int Total => qty * unitPrice;
    }

    /// <summary>
    /// Şehrin adres sistemi. Tutarlıdır: aynı bina her zaman aynı sokakta, aynı numarada, aynı kat sayısındadır.
    ///  • Dikey yollar (x = 0, 4, 8 …) "Cadde", yatay yollar (y = 0, 4, 8 …) "Sokak".
    ///  • Bina numarası, kapısının açıldığı yol boyunca konumuna göre (bir taraf tek, karşı taraf çift).
    ///  • Kat sayısı blok türüne göre: kule bloklar 7–12, eski şehir 3–5, çarşı 3–6, kampüs 2–4, bahçeli ev ve sanayi 1–2.
    ///  • 5+ katlı binalarda asansör var; 7+ katlılar iki bloklu (A, B).
    /// </summary>
    public static class Addresses
    {
        static readonly string[] AvenueNames =
            { "Kossuth", "Petőfi", "Rákóczi", "Széchenyi", "Dózsa György", "Arany János", "Bethlen", "Jókai", "Vörösmarty",
              "Andrássy", "Deák Ferenc", "Batthyány", "Mátyás Király" };
        static readonly string[] StreetNames =
            { "Kölcsey", "Ady Endre", "Hunyadi", "Madách", "Bocskai", "Szent István", "Kazinczy", "Bartók Béla", "Erkel Ferenc" };

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
            switch (CityMap.ZoneOf(t))
            {
                case Zone.HighRise: return 7 + r.Next(6);    // 7–12 kule bloklar
                case Zone.OldTown: return 3 + r.Next(3);     // 3–5 eski şehir
                case Zone.Houses: return 1 + r.Next(2);      // 1–2 bahçeli ev
                case Zone.Mall: return 2;
                case Zone.Campus: return 2 + r.Next(3);      // 2–4 yurt / fakülte
                case Zone.Warehouse: return 1 + r.Next(2);   // 1–2 depo / atölye
                default: return 3 + r.Next(4);               // 3–6 çarşı
            }
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

}
