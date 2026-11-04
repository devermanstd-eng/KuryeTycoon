using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    public enum CustomerType { Normal, Aceleci, Comert, Zor }
    public enum OrderStatus { Bekliyor, Atandi, Alindi, Teslim, Iptal }
    public enum AddressIssue { Yok, YanlisAdres, EvdeYok }

    /// <summary>Haritadaki bir dükkan ve onunla yapılan sözleşme.</summary>
    public class Shop
    {
        public ShopDef def;
        public Vector2Int tile;
        public Vector2Int door;
        public bool contracted;
        public float commissionBonus;      // olaylarla kazanılan ek komisyon
        public float prepMultiplier = 1f;  // "dükkan geç hazırlıyor" olayı
        public int ordersTotal;
        public SpriteRenderer sr;

        // ---------- Sözleşme ilişkisi ----------
        public float satisfaction = 65f;   // 0–100: geç/soğuk teslimat, iptal ve şikayetler düşürür
        public int contractStartDay;       // sözleşmenin başladığı gün (GameClock.Day)
        public int contractEndDay;         // yenileme günü (6 ayda bir)
        public bool exclusive;             // sadece seninle çalışıyor (rakip yok, daha çok sipariş, daha düşük oran)
        public bool rivalContracted;       // rakip platformla da çalışıyor (siparişler paylaşılır)
        public int monthOrders;            // bu ay teslim edilen sipariş
        public int lastMonthOrders;
        public int negotiateCooldownUntil; // bu güne kadar yeniden pazarlık yapılamaz
        public int resignBlockedUntil;     // ayrıldıktan sonra yeniden sözleşme için bekleme
        public bool lowSatisfactionWarned;
        public float todayLoss;            // (eski) günlük kayıp sınırı
        // Son günlerin hizmet karnesi (her gün %20 unutulur: ~5 günlük hafıza). Memnuniyet buna göre yavaşça değişir.
        public float perfOk, perfLate, perfCold, perfCancel, perfIncident;
        public bool own;                   // kendi marketin (dark store): komisyon yok, satışın tamamı senin, stok senin

        public string Name => def.name;
        public float Commission => own ? 1f : Mathf.Max(0.05f, def.commission + commissionBonus);
    }

    public class Order
    {
        static int nextId = 1;

        /// <summary>Sipariş numarası 0–999 arası döner (999'dan sonra 0'dan devam eder).</summary>
        static int NextId()
        {
            int v = nextId;
            nextId = (nextId + 1) % 1000;
            return v;
        }

        public int id = NextId();
        public Shop shop;
        public Vector2Int customerTile;
        public Vector2Int customerDoor;
        public CustomerType customerType;
        public OrderStatus status = OrderStatus.Bekliyor;

        public int basket;            // müşterinin sepet tutarı (dükkana gider)
        public int distance;          // kare cinsinden yol uzunluğu (1 kare = 0,2 km)
        public float weightKg;        // sipariş ağırlığı
        public int commissionIncome;  // sepet × komisyon oranı (bize kalır)
        public int deliveryFee;       // müşteriden alınan teslimat ücreti (bize kalır)
        public int feeBase, feeKm, feeExtra, feeWeather; // teslimat ücretinin dökümü
        public int courierPay;        // atama anında sabitlenen kurye ödemesi
        public int boostPay;          // boost saatindeki ek ödeme (şirketin kârından)
        public int tip;               // bahşiş (kuryeye gider)
        public float rating;

        public float createdAt, readyAt, deadlineAt, deliveredAt;
        public Courier courier;
        public SpriteRenderer marker;

        /// <summary>Bu siparişi reddeden kuryeler (tekrar teklif edilmez).</summary>
        public readonly HashSet<Courier> rejectedBy = new HashSet<Courier>();
        /// <summary>Destek müşteriyi arayıp ek süre aldı mı.</summary>
        public bool supportCalled;

        /// <summary>Adres sorunu: yanlış işaretlenmiş adres ya da müşteri evde yok.</summary>
        public AddressIssue addressIssue;
        public bool issueResolved;
        public bool customerFault;     // gecikme müşterinin hatası: puan düşmez
        public bool keptByCourier;     // müşteriye ulaşılamadı, sipariş kuryede kaldı
        public int priceDeclines;      // fiyatı beğenmeyip reddeden kurye sayısı
        public bool shopQueue;         // dükkanda sıra vardı
        public bool secondRound;       // uzun bekledi, reddedenlere tekrar (daha esnek) sorulur
        public bool batched;           // başka siparişlerle aynı turda götürüldü (toplu)
        public float pickedAt;         // dükkandan alındığı an (sıcak yemek soğuma süresi)
        public bool deliveredCold;     // sıcak yemek soğuk gitti

        /// <summary>Nakit ödeme: müşteri kuryeye nakit öder, kurye parayı şirkete borçlanır.</summary>
        public bool cashPay;
        public int cashGiven;          // müşterinin vereceği banknot (ör. 20 000)
        public int CashAmount => basket + deliveryFee;
        public int CashChange => System.Math.Max(0, cashGiven - CashAmount);

        // ---------- Sipariş içeriği ve adres ----------
        public List<OrderItem> items = new List<OrderItem>();
        public string customerName = "";
        public string customerNote = "";
        public string street = "";
        public int number;           // kapı numarası
        public int floor;            // 0 = zemin kat
        public int floors;           // binanın kat sayısı
        public char block = 'A';
        public int blocks = 1;
        public int flat;             // daire no
        public bool elevator;
        public bool elevatorBroken;

        public string ItemsText => Menus.Text(items);
        public int ItemCount { get { int n = 0; foreach (var i in items) n += i.qty; return n; } }

        public string AddressText =>
            $"{street} No: {number}" + (blocks > 1 ? $", {block} Blok" : "") + $", {Addresses.FloorText(floor)}" +
            (flat > 0 ? $", Daire {flat}" : "");

        public string BuildingText =>
            $"{floors} katlı bina · " + (floor == 0 ? "zemin kat, kapıda teslim" :
            elevator ? (elevatorBroken ? "asansör ARIZALI, merdivenle" : "asansörlü") : "asansör yok, merdivenle");

        /// <summary>Adresi binanın kalıcı bilgilerinden kurar (aynı bina her zaman aynı sokak, numara ve kat sayısı).</summary>
        public void SetAddress(Vector2Int tile, Vector2Int door)
        {
            street = Addresses.StreetOf(door);
            number = Addresses.Number(tile, door);
            floors = Addresses.Floors(tile);
            blocks = Addresses.Blocks(tile);
            elevator = Addresses.HasElevator(tile);
            // Zemin kat da olabilir; çok katlı binada yukarı katlar daha olası değil, eşit dağılım
            floor = Random.Range(0, floors);
            block = (char)('A' + Random.Range(0, blocks));
            int perFloor = Addresses.FlatsPerFloor(tile);
            flat = floors > 1 ? floor * perFloor + Random.Range(1, perFloor + 1) : 0;
            elevatorBroken = elevator && Random.value < 0.08f;
        }

        /// <summary>
        /// Kapıda / katta geçen süre (oyun dakikası): bina girişi, kat çıkışı (asansör ya da merdiven), teslim ve iniş.
        /// Merdiven kat başına ~0,9 dk (çıkış + iniş), dayanıklılık ve ağır paket etkiler; asansör beklemesi 1–2 dk.
        /// </summary>
        public float DoorMinutes(float stamina, bool estimate = false)
        {
            float t = 1f;                                   // kapıda teslim, zil, para üstü
            if (cashPay) t += 0.5f;
            if (blocks > 1) t += 0.5f;                      // doğru bloğu bulma
            if (floor == 0) return t;
            if (elevator && !elevatorBroken)
                return t + (estimate ? 1.5f : Random.Range(1f, 2f)) + floor * 0.25f;
            float perFloor = 0.9f / Mathf.Clamp(stamina, 0.6f, 1.3f);
            if (weightKg > 10f) perFloor *= 1.3f;
            return t + floor * perFloor;
        }

        public bool UsesStairs => floor > 0 && (!elevator || elevatorBroken);

        public int CompanyIncome => commissionIncome + deliveryFee;
        /// <summary>Kendi marketinde satılan malın alış maliyeti (stoktan düşer; kârdan çıkarılır).</summary>
        public int stockCost;
        public int Profit => CompanyIncome - courierPay - boostPay - stockCost;
        public float DistanceKm => GameData.Km(distance);
        /// <summary>20 kg ve üstü: sadece arabalı kuryeler alabilir.</summary>
        public bool IsHeavy => weightKg >= GameData.HeavyKg;

        public string CustomerTypeName
        {
            get
            {
                switch (customerType)
                {
                    case CustomerType.Aceleci: return "Aceleci müşteri";
                    case CustomerType.Comert: return "Cömert müşteri";
                    case CustomerType.Zor: return "Zor müşteri";
                    default: return "Normal müşteri";
                }
            }
        }
    }
}
