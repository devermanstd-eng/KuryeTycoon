using UnityEngine;

namespace KuryeTycoon
{
    public enum VehicleKind { Yuruyerek, Bisiklet, EScooter, Motosiklet, Araba }
    public enum EquipmentKind { KisMontu, Yagmurluk, YazTisort, TermalCanta, Kask, Powerbank }

    public class VehicleDef
    {
        public VehicleKind kind;
        public string name;
        public string letter;
        public float tilesPerMinute;   // oyun dakikasında kaç kare gider
        public int capacity;           // aynı anda kaç sipariş taşır
        public int basePay;            // kuryeye teslimat başı sabit ödeme (Ft)
        public int payPerTile;         // kuryeye kare başı ödeme (Ft)
        public int hireCost;           // işe alım primi (Ft)
        public int dailyCost;          // şirketin günlük gideri: yakıt, sigorta, telefon (Ft)
        public bool exposed;           // hava şartlarına açık mı (araba hariç hepsi)
        public float accidentRisk;     // günlük kaza ihtimali
    }

    public class ShopDef
    {
        public string name;
        public Color color;
        public float prepMinutes;
        public int basketMin, basketMax;
        public float commission;       // sözleşmedeki komisyon oranı
        public float weight;           // sipariş yoğunluğu
        public int contractCost;       // sözleşme imza bedeli (0 = başlangıçta sözleşmeli)
        public int unlockLevel;        // hangi bölgede
        public bool hotFood;
    }

    public class EquipmentDef
    {
        public EquipmentKind kind;
        public string name;
        public int price;
        public string effect;
    }

    public static class GameData
    {
        public static readonly VehicleDef[] Vehicles =
        {
            new VehicleDef { kind = VehicleKind.Yuruyerek,  name = "Yürüyerek",  letter = "Y", tilesPerMinute = 0.25f, capacity = 1, basePay = 500,  payPerTile = 20, hireCost = 5000,  dailyCost = 300,  exposed = true,  accidentRisk = 0.002f },
            new VehicleDef { kind = VehicleKind.Bisiklet,   name = "Bisiklet",   letter = "B", tilesPerMinute = 0.50f, capacity = 2, basePay = 700,  payPerTile = 25, hireCost = 10000, dailyCost = 500,  exposed = true,  accidentRisk = 0.010f },
            new VehicleDef { kind = VehicleKind.EScooter,   name = "E-Scooter",  letter = "S", tilesPerMinute = 0.65f, capacity = 2, basePay = 800,  payPerTile = 30, hireCost = 20000, dailyCost = 900,  exposed = true,  accidentRisk = 0.015f },
            new VehicleDef { kind = VehicleKind.Motosiklet, name = "Motosiklet", letter = "M", tilesPerMinute = 0.95f, capacity = 3, basePay = 950,  payPerTile = 35, hireCost = 35000, dailyCost = 1800, exposed = true,  accidentRisk = 0.020f },
            new VehicleDef { kind = VehicleKind.Araba,      name = "Araba",      letter = "A", tilesPerMinute = 0.85f, capacity = 4, basePay = 1100, payPerTile = 45, hireCost = 50000, dailyCost = 3000, exposed = false, accidentRisk = 0.008f },
        };

        public static VehicleDef Vehicle(VehicleKind k) => Vehicles[(int)k];

        public static readonly ShopDef[] Shops =
        {
            // Bölge 1 — Merkez Mahalle
            new ShopDef { name = "Burger Queen",  color = new Color(0.95f, 0.45f, 0.15f), prepMinutes = 8,  basketMin = 2500, basketMax = 6000,  commission = 0.10f, weight = 1.2f, contractCost = 0,      unlockLevel = 1, hotFood = true },
            new ShopDef { name = "Döner Kral",    color = new Color(0.80f, 0.20f, 0.20f), prepMinutes = 6,  basketMin = 1800, basketMax = 4500,  commission = 0.12f, weight = 1.0f, contractCost = 0,      unlockLevel = 1, hotFood = true },
            new ShopDef { name = "Starbocks",     color = new Color(0.10f, 0.55f, 0.35f), prepMinutes = 5,  basketMin = 1500, basketMax = 4000,  commission = 0.12f, weight = 0.8f, contractCost = 20000,  unlockLevel = 1, hotFood = false },
            new ShopDef { name = "Market 24",     color = new Color(0.25f, 0.45f, 0.90f), prepMinutes = 5,  basketMin = 3000, basketMax = 12000, commission = 0.08f, weight = 0.6f, contractCost = 25000,  unlockLevel = 1, hotFood = false },
            // Bölge 2 — Doğu İlçesi
            new ShopDef { name = "McDanold's",    color = new Color(0.95f, 0.80f, 0.10f), prepMinutes = 7,  basketMin = 2500, basketMax = 6500,  commission = 0.10f, weight = 1.4f, contractCost = 60000,  unlockLevel = 2, hotFood = true },
            new ShopDef { name = "Pizza Hat",     color = new Color(0.85f, 0.10f, 0.35f), prepMinutes = 18, basketMin = 3500, basketMax = 9000,  commission = 0.11f, weight = 1.0f, contractCost = 50000,  unlockLevel = 2, hotFood = true },
            // Bölge 3 — Sanayi & Kampüs
            new ShopDef { name = "KFG",           color = new Color(0.70f, 0.15f, 0.10f), prepMinutes = 10, basketMin = 3000, basketMax = 7500,  commission = 0.11f, weight = 1.1f, contractCost = 80000,  unlockLevel = 3, hotFood = true },
            new ShopDef { name = "Sushi Samurai", color = new Color(0.95f, 0.95f, 0.95f), prepMinutes = 20, basketMin = 5000, basketMax = 15000, commission = 0.13f, weight = 0.7f, contractCost = 100000, unlockLevel = 3, hotFood = false },
        };

        public static readonly EquipmentDef[] Equipment =
        {
            new EquipmentDef { kind = EquipmentKind.KisMontu,    name = "Kış Montu",    price = 9000, effect = "Kar/soğukta yavaşlama azalır, kışın moral düşmez." },
            new EquipmentDef { kind = EquipmentKind.Yagmurluk,   name = "Yağmurluk",    price = 5000, effect = "Yağmurda yavaşlama neredeyse yok." },
            new EquipmentDef { kind = EquipmentKind.YazTisort,   name = "Yaz Tişörtü",  price = 3000, effect = "Sıcakta yavaşlamaz, yazın moral düşmez." },
            new EquipmentDef { kind = EquipmentKind.TermalCanta, name = "Termal Çanta", price = 7000, effect = "Hasar riski −%40, sıcak yemek soğuk gitmez." },
            new EquipmentDef { kind = EquipmentKind.Kask,        name = "Kask",         price = 8000, effect = "Kaza riski −%60." },
            new EquipmentDef { kind = EquipmentKind.Powerbank,   name = "Powerbank",    price = 4000, effect = "Kurye %20 daha geç yorulur." },
        };

        public static EquipmentDef Equip(EquipmentKind k) => Equipment[(int)k];

        static readonly string[] FirstNames =
        {
            "Ahmet", "Mehmet", "Ayşe", "Zeynep", "Can", "Elif", "Emre", "Selin",
            "Bence", "Dávid", "Anna", "Réka", "Márk", "Lili", "Tamás", "Eszter"
        };

        static readonly string[] LastNames =
        {
            "Yılmaz", "Kaya", "Demir", "Şahin", "Çelik", "Nagy", "Kovács", "Tóth", "Szabó", "Horváth"
        };

        public static string RandomName() =>
            FirstNames[Random.Range(0, FirstNames.Length)] + " " + LastNames[Random.Range(0, LastNames.Length)];

        /// <summary>5000 → "5 000 Ft"</summary>
        public static string Ft(long v) =>
            v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' ') + " Ft";

        public static int Round10(float v) => Mathf.RoundToInt(v / 10f) * 10;
        public static int CeilTo10(float v) => Mathf.CeilToInt(v / 10f) * 10;
    }
}
