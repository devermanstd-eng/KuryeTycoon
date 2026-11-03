using UnityEngine;

namespace KuryeTycoon
{
    public enum VehicleKind { Bisiklet, EScooter, Motosiklet, Araba }
    public enum EquipmentKind { KisMontu, Yagmurluk, YazTisort, TermalCanta, Kask, Powerbank }
    public enum Currency { Forint, Euro }

    public class VehicleDef
    {
        public VehicleKind kind;
        public string name;
        public string letter;
        public float tilesPerMinute;   // oyun dakikasında kaç kare gider (1 kare = 0,2 km)
        public int capacity;           // normal moralde aynı anda kaç paket taşır
        public int basePay;            // kuryeye paket başı sabit ödeme (Ft)
        public int payPerKm;           // kuryeye km başı ödeme (Ft) — 175–286 arası
        public bool exposed;           // hava şartlarına açık mı (araba hariç hepsi)
        public float accidentRisk;     // günlük kaza ihtimali
        public float maxWeightKg;      // taşıyabileceği en ağır sipariş (kg)
        public int ownPrice;           // kuryenin kendi cebinden ödediği araç fiyatı (Ft)
        public float breakdownRisk;    // günlük arıza ihtimali
    }

    public class ShopDef
    {
        public string name;
        public Color color;
        public float prepMinutes;
        public int basketMin, basketMax;
        public float commission;       // sözleşmedeki komisyon oranı (yemek %20–25, market %12–15)
        public float weight;           // sipariş yoğunluğu
        public int contractCost;       // sözleşme imza bedeli (0 = başlangıçta sözleşmeli)
        public int unlockLevel;        // hangi bölgede
        public bool hotFood;
        public float kgMin, kgMax;     // sipariş ağırlığı aralığı (sepet büyüdükçe artar)
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
        /// <summary>Haritada bir kare kaç km.</summary>
        public const float KmPerTile = 0.2f;

        public static readonly VehicleDef[] Vehicles =
        {
            new VehicleDef { kind = VehicleKind.Bisiklet,   name = "Bisiklet",   letter = "B", tilesPerMinute = 0.50f, capacity = 2, basePay = 500, payPerKm = 175, exposed = true,  accidentRisk = 0.010f, maxWeightKg = 20f, ownPrice = 60000, breakdownRisk = 0.002f },
            new VehicleDef { kind = VehicleKind.EScooter,   name = "E-Scooter",  letter = "S", tilesPerMinute = 0.65f, capacity = 2, basePay = 550, payPerKm = 210, exposed = true,  accidentRisk = 0.015f, maxWeightKg = 20f, ownPrice = 180000, breakdownRisk = 0.005f },
            new VehicleDef { kind = VehicleKind.Motosiklet, name = "Motosiklet", letter = "M", tilesPerMinute = 0.95f, capacity = 3, basePay = 600, payPerKm = 245, exposed = true,  accidentRisk = 0.020f, maxWeightKg = 20f, ownPrice = 450000, breakdownRisk = 0.008f },
            new VehicleDef { kind = VehicleKind.Araba,      name = "Araba",      letter = "A", tilesPerMinute = 0.85f, capacity = 4, basePay = 650, payPerKm = 286, exposed = false, accidentRisk = 0.008f, maxWeightKg = 999f, ownPrice = 1200000, breakdownRisk = 0.007f },
        };

        public static VehicleDef Vehicle(VehicleKind k) => Vehicles[(int)k];

        public static readonly ShopDef[] Shops =
        {
            // Bölge 1 — Merkez Mahalle
            new ShopDef { name = "Burger Queen",  color = new Color(0.95f, 0.45f, 0.15f), prepMinutes = 8,  basketMin = 2500, basketMax = 6000,  commission = 0.22f, weight = 1.2f, contractCost = 0,      unlockLevel = 1, hotFood = true, kgMin = 0.5f, kgMax = 2.5f },
            new ShopDef { name = "Döner Kral",    color = new Color(0.80f, 0.20f, 0.20f), prepMinutes = 6,  basketMin = 1800, basketMax = 4500,  commission = 0.25f, weight = 1.0f, contractCost = 0,      unlockLevel = 1, hotFood = true, kgMin = 0.4f, kgMax = 2f },
            new ShopDef { name = "Starbocks",     color = new Color(0.10f, 0.55f, 0.35f), prepMinutes = 5,  basketMin = 1500, basketMax = 4000,  commission = 0.20f, weight = 0.8f, contractCost = 20000,  unlockLevel = 1, hotFood = false, kgMin = 0.3f, kgMax = 1.5f },
            new ShopDef { name = "Szpar Express", color = new Color(0.15f, 0.62f, 0.30f), prepMinutes = 10, basketMin = 3000, basketMax = 15000, commission = 0.15f, weight = 0.7f, contractCost = 30000,  unlockLevel = 1, hotFood = false, kgMin = 2f, kgMax = 18f },
            new ShopDef { name = "Market 24",     color = new Color(0.25f, 0.45f, 0.90f), prepMinutes = 5,  basketMin = 3000, basketMax = 12000, commission = 0.15f, weight = 0.6f, contractCost = 25000,  unlockLevel = 1, hotFood = false, kgMin = 1f, kgMax = 12f },
            // Bölge 2 — Doğu İlçesi
            new ShopDef { name = "McDanold's",    color = new Color(0.95f, 0.80f, 0.10f), prepMinutes = 7,  basketMin = 2500, basketMax = 6500,  commission = 0.20f, weight = 1.4f, contractCost = 60000,  unlockLevel = 2, hotFood = true, kgMin = 0.5f, kgMax = 2.5f },
            new ShopDef { name = "Pizza Hat",     color = new Color(0.85f, 0.10f, 0.35f), prepMinutes = 18, basketMin = 3500, basketMax = 9000,  commission = 0.22f, weight = 1.0f, contractCost = 50000,  unlockLevel = 2, hotFood = true, kgMin = 1f, kgMax = 4f },
            new ShopDef { name = "InterSzpar",    color = new Color(0.85f, 0.12f, 0.18f), prepMinutes = 20, basketMin = 8000, basketMax = 45000, commission = 0.12f, weight = 0.8f, contractCost = 70000,  unlockLevel = 2, hotFood = false, kgMin = 6f, kgMax = 40f },
            // Bölge 3 — Sanayi & Kampüs
            new ShopDef { name = "KFG",           color = new Color(0.70f, 0.15f, 0.10f), prepMinutes = 10, basketMin = 3000, basketMax = 7500,  commission = 0.22f, weight = 1.1f, contractCost = 80000,  unlockLevel = 3, hotFood = true, kgMin = 0.7f, kgMax = 3f },
            new ShopDef { name = "Sushi Samurai", color = new Color(0.95f, 0.95f, 0.95f), prepMinutes = 20, basketMin = 5000, basketMax = 15000, commission = 0.25f, weight = 0.7f, contractCost = 100000, unlockLevel = 3, hotFood = false, kgMin = 0.5f, kgMax = 2.5f },
        };

        public static readonly EquipmentDef[] Equipment =
        {
            new EquipmentDef { kind = EquipmentKind.KisMontu,    name = "Kış Montu",    price = 9000, effect = "Soğukta yavaşlamaz, karda %10 yavaş (montsuz %30). Kışın hastalanmaz (montsuz günde %10) ve morali düşmez. Araba hariç." },
            new EquipmentDef { kind = EquipmentKind.Yagmurluk,   name = "Yağmurluk",    price = 5000, effect = "Yağmurda yavaşlamaz (yoksa %18), paket ıslanmaz (yoksa hasar riski 2 kat), yağmur sezonunda hastalanmaz. Araba hariç." },
            new EquipmentDef { kind = EquipmentKind.YazTisort,   name = "Yaz Tişörtü",  price = 3000, effect = "Sıcakta yavaşlamaz ve 1,5 kat hızlı yorulmaz. Yazın morali düşmez. Araba hariç." },
            new EquipmentDef { kind = EquipmentKind.TermalCanta, name = "Termal Çanta", price = 7000, effect = "Sıcak yemek 30 dk sıcak kalır (çantasız 15 dk, sonra −0,8 yıldız) — toplu siparişte önemli. Hasar riski −%40." },
            new EquipmentDef { kind = EquipmentKind.Kask,        name = "Kask",         price = 8000, effect = "Kaza riski −%60. Kazada 3 gün yerine 1 gün izin." },
            new EquipmentDef { kind = EquipmentKind.Powerbank,   name = "Powerbank",    price = 4000, effect = "Telefon kapanmaz (yoksa teslimatların %5'inde 1 saat çevrimdışı). %20 daha geç yorulur." },
        };

        public static EquipmentDef Equip(EquipmentKind k) => Equipment[(int)k];

        /// <summary>İşe giren her kuryeye verilen giriş seviyesi şirket ekipmanı.</summary>
        public static readonly EquipmentKind[] StarterKit = { EquipmentKind.TermalCanta, EquipmentKind.YazTisort };

        /// <summary>Kurye toplam bu kadar kazanınca depozito kesilir.</summary>
        public const int DepositThreshold = 30000;
        /// <summary>Ekipman depozitosu (ayrılırken ekipmanı iade ederse geri ödenir).</summary>
        public const int DepositAmount = 20000;

        /// <summary>Her başlangıç kitinin şirkete maliyeti (Ft).</summary>
        public const int StarterKitCost = 5000;

        public static string StarterKitText
        {
            get
            {
                string s = "";
                foreach (var k in StarterKit) s += (s.Length > 0 ? " + " : "") + Equip(k).name;
                return s;
            }
        }

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

        static readonly string[] ApplicationNotes =
        {
            "Öğrenciyim, akşamları ve hafta sonları çalışabilirim.",
            "Daha önce başka bir kurye şirketinde çalıştım.",
            "Şehri avucumun içi gibi bilirim.",
            "İlk kuryelik deneyimim olacak ama çok hevesliyim.",
            "Yağmurda karda fark etmez, her gün çalışırım.",
            "Ek gelir için yarı zamanlı çalışmak istiyorum."
        };

        public static string RandomNote() => ApplicationNotes[Random.Range(0, ApplicationNotes.Length)];

        public static float Km(int tiles) => tiles * KmPerTile;

        /// <summary>Bu ağırlık ve üstü "ağır sipariş": sadece arabalı kuryeler alır.</summary>
        public const float HeavyKg = 20f;

        /// <summary>12.5 → "12,5 kg"</summary>
        public static string KgText(float kg) =>
            kg.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + " kg";

        public static string CarryText(VehicleDef v) =>
            v.maxWeightKg >= 900f ? "ağır siparişler dahil her ağırlık" : $"{v.maxWeightKg:0} kg'a kadar";

        /// <summary>15 kare → "3,0 km"</summary>
        public static string KmText(int tiles) =>
            Km(tiles).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + " km";

        // ---------- Para birimi ----------
        // Oyun içeride HER ZAMAN Forint ile hesaplar; para birimi sadece ekranda gösterimi değiştirir,
        // yani oyun dengesi iki seçenekte de aynıdır. Seçim Ayarlar sekmesinden yapılır ve PlayerPrefs'te saklanır.

        /// <summary>Euro gösteriminde kullanılan sabit kur.</summary>
        public const float FtPerEuro = 400f;
        public static Currency DisplayCurrency = Currency.Forint;
        const string CurrencyPrefKey = "KuryeTycoon.ParaBirimi";

        /// <summary>Kayıtlı para birimi tercihini yükler (oyun açılırken GameBootstrap çağırır).</summary>
        public static void LoadCurrency() =>
            DisplayCurrency = PlayerPrefs.GetInt(CurrencyPrefKey, 0) == 1 ? Currency.Euro : Currency.Forint;

        public static void SetCurrency(Currency c)
        {
            DisplayCurrency = c;
            PlayerPrefs.SetInt(CurrencyPrefKey, c == Currency.Euro ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Forint tutarını seçili para biriminde yazar. Adı tarihsel olarak Ft kaldı.
        /// Forint: 5000 → "5 000 Ft". Euro: 500 → "1,25 €", 80000 → "200 €", 1000000 → "2 500 €".
        /// </summary>
        public static string Ft(long v)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (DisplayCurrency == Currency.Euro)
            {
                double eur = System.Math.Round(v / (double)FtPerEuro, 2);
                // Kuruşu olmayan ya da 1 000 €'yu geçen tutarlarda ondalık gösterme
                bool whole = System.Math.Abs(eur) >= 1000 || System.Math.Abs(eur - System.Math.Round(eur)) < 0.005;
                return eur.ToString(whole ? "#,0" : "#,0.00", inv).Replace(',', ' ').Replace('.', ',') + " €";
            }
            return v.ToString("#,0", inv).Replace(',', ' ') + " Ft";
        }

        public static int Round10(float v) => Mathf.RoundToInt(v / 10f) * 10;
        public static int CeilTo10(float v) => Mathf.CeilToInt(v / 10f) * 10;
    }
}
