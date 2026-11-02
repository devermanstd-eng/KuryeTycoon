using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    public enum CustomerType { Normal, Aceleci, Comert, Zor }
    public enum OrderStatus { Bekliyor, Atandi, Alindi, Teslim, Iptal }

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

        public string Name => def.name;
        public float Commission => def.commission + commissionBonus;
    }

    public class Order
    {
        static int nextId = 1;

        public int id = nextId++;
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
        public int courierPay;        // atama anında sabitlenen kurye ödemesi
        public int boostPay;          // boost saatindeki ek ödeme (şirketin kârından)
        public int tip;               // bahşiş (kuryeye gider)
        public float rating;

        public float createdAt, readyAt, deadlineAt, deliveredAt;
        public Courier courier;
        public SpriteRenderer marker;

        /// <summary>Bu siparişi reddeden kuryeler (tekrar teklif edilmez).</summary>
        public readonly HashSet<Courier> rejectedBy = new HashSet<Courier>();

        public int CompanyIncome => commissionIncome + deliveryFee;
        public int Profit => CompanyIncome - courierPay - boostPay;
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
