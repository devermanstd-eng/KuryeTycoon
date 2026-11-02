using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Sipariş üretme, kuryeye atama (manuel / otomatik), teslimat ve müşteri puanı.</summary>
    public class OrderManager : MonoBehaviour
    {
        public static OrderManager I { get; private set; }

        public readonly List<Order> Pending = new List<Order>();
        public readonly List<Order> Active = new List<Order>();
        public readonly List<Order> History = new List<Order>();

        [Header("Atama")]
        public bool autoAssign = true;
        public bool prioritizeProfit = false;

        [Header("Talep")]
        [Tooltip("Sözleşmeli dükkan başına, oyun dakikasında ortalama sipariş")]
        public float baseRatePerShop = 0.05f;
        [Tooltip("Kurye bulunamazsa sipariş kaç dakika sonra iptal olur")]
        public float pendingTimeout = 30f;

        public int lostToday;       // ücret pahalı geldiği için vazgeçen müşteri
        public int cancelledToday;  // kurye bulunamadığı için iptal
        public int rejectedToday;   // kuryelerin reddettiği teklif
        public int heavyLostToday;  // taşıyabilecek kurye olmadığı için alınamayan ağır sipariş
        bool heavyWarnedToday;

        float spawnAcc;
        float assignTimer;
        Transform markerRoot;

        static string Ft(long v) => GameData.Ft(v);

        void Awake() { I = this; }

        public void Init()
        {
            markerRoot = new GameObject("Müşteriler").transform;
            markerRoot.SetParent(transform, false);
            GameClock.I.OnNewDay += () => { lostToday = 0; cancelledToday = 0; rejectedToday = 0; heavyLostToday = 0; heavyWarnedToday = false; };
        }

        static float HourCurve(int h)
        {
            if (GameClock.I.IsWeekend)
            {
                // Hafta sonu: gece ve sabah çok sakin (insanlar uyuyor), öğle ve akşam yoğun
                if (h < 2) return 0.08f;
                if (h < 8) return 0.02f;
                if (h < 11) return 0.12f;
                if (h < 15) return 1.5f;
                if (h < 18) return 0.9f;
                if (h < 22) return 1.6f;
                return 0.4f;
            }
            // Hafta içi: gece yarısından sonra ve sabah erken çok az sipariş
            if (h < 2) return 0.1f;
            if (h < 6) return 0.03f;
            if (h < 8) return 0.15f;
            if (h < 10) return 0.4f;
            if (h < 11) return 0.7f;
            if (h < 14) return 1.4f;
            if (h < 17) return 0.7f;
            if (h < 21) return 1.5f;
            if (h < 23) return 0.7f;
            return 0.3f;
        }

        void Update()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            float dm = GameClock.I.DeltaMinutes;
            if (dm <= 0f) return;

            int contracted = 0;
            foreach (var s in CityMap.I.Shops)
                if (s.contracted && CityMap.I.IsUnlocked(s.tile.x)) contracted++;

            float rate = baseRatePerShop * contracted * HourCurve(GameClock.I.Hour)
                         * (0.55f + 0.15f * CompanyProfile.Reputation) * WeatherSystem.I.DemandFactor;
            spawnAcc += rate * dm;
            while (spawnAcc >= 1f)
            {
                spawnAcc -= 1f;
                TrySpawn();
            }

            ExpirePending();

            if (autoAssign)
            {
                assignTimer -= Time.deltaTime;
                if (assignTimer <= 0f)
                {
                    assignTimer = 0.25f;
                    AutoAssign();
                }
            }
        }

        Shop PickShop()
        {
            float total = 0f;
            foreach (var s in CityMap.I.Shops)
                if (s.contracted && CityMap.I.IsUnlocked(s.tile.x)) total += s.def.weight;
            if (total <= 0f) return null;

            float r = Random.value * total;
            foreach (var s in CityMap.I.Shops)
            {
                if (!s.contracted || !CityMap.I.IsUnlocked(s.tile.x)) continue;
                r -= s.def.weight;
                if (r <= 0f) return s;
            }
            return null;
        }

        static CustomerType RandomCustomerType()
        {
            float r = Random.value;
            if (r < 0.55f) return CustomerType.Normal;
            if (r < 0.75f) return CustomerType.Aceleci;
            if (r < 0.90f) return CustomerType.Comert;
            return CustomerType.Zor;
        }

        void TrySpawn()
        {
            var shop = PickShop();
            if (shop == null) return;

            var map = CityMap.I;
            var custTile = map.RandomCustomerTile(shop.tile);
            var custDoor = map.GetDoor(custTile);
            int dist = Mathf.Max(1, map.FindPath(shop.door, custDoor).Count - 1);

            int basket = GameData.Round10(Random.Range(shop.def.basketMin, shop.def.basketMax));

            // Ağırlık: sepet büyüdükçe artar, biraz da rastgelelik
            float t = Mathf.InverseLerp(shop.def.basketMin, shop.def.basketMax, basket);
            float kg = Mathf.Round(Mathf.Lerp(shop.def.kgMin, shop.def.kgMax, t * 0.7f + Random.value * 0.3f) * 10f) / 10f;

            // Ekipte bu ağırlığı taşıyabilecek kurye yoksa sipariş kabul edilmez
            if (!EconomyManager.AnyoneCanCarry(kg))
            {
                heavyLostToday++;
                if (!heavyWarnedToday)
                {
                    heavyWarnedToday = true;
                    EventManager.I.Notify(kg >= GameData.HeavyKg
                        ? $"{shop.Name}'dan {GameData.KgText(kg)} ağır sipariş geldi ama arabalı kuryen yok, alınamadı."
                        : $"{shop.Name}'dan {GameData.KgText(kg)} sipariş geldi ama taşıyabilecek kurye yok, alınamadı.");
                }
                return;
            }

            int commission = Mathf.RoundToInt(basket * shop.Commission);
            int fee = EconomyManager.I.QuoteDeliveryFee(commission, dist, kg);

            // Pahalı teslimat ücreti = müşteri vazgeçebilir
            float accept = Mathf.Clamp(1.35f - fee / 2200f, 0.15f, 1f);
            if (Random.value > accept)
            {
                lostToday++;
                return;
            }

            float now = GameClock.I.TotalMinutes;
            var o = new Order
            {
                shop = shop,
                customerTile = custTile,
                customerDoor = custDoor,
                customerType = RandomCustomerType(),
                basket = basket,
                distance = dist,
                weightKg = kg,
                commissionIncome = commission,
                deliveryFee = fee,
                createdAt = now
            };
            o.readyAt = now + shop.def.prepMinutes * shop.prepMultiplier * Random.Range(0.8f, 1.3f);
            o.deadlineAt = o.readyAt + dist / 0.45f + (o.customerType == CustomerType.Aceleci ? 7f : 15f);
            o.marker = SpriteFactory.Create($"Müşteri #{o.id}", markerRoot, true,
                o.IsHeavy ? new Color(0.75f, 0.35f, 1f) : new Color(1f, 0.6f, 0.2f),
                CityMap.ToWorld(custTile), o.IsHeavy ? 0.6f : 0.45f, 3);

            Pending.Add(o);
            shop.ordersTotal++;
        }

        void ExpirePending()
        {
            float now = GameClock.I.TotalMinutes;
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                var o = Pending[i];
                float limit = pendingTimeout + (o.supportCalled ? SupportManager.I.extraWaitMinutes : 0f);
                if (now - o.createdAt < limit) continue;
                if (SupportManager.I.TryCallCustomer(o)) continue; // destek müşteriyi arayıp oyalar
                o.status = OrderStatus.Iptal;
                if (o.marker != null) Destroy(o.marker.gameObject);
                Pending.RemoveAt(i);
                cancelledToday++;
                CompanyProfile.AddReputation(-0.04f);
                EventManager.I.Notify($"#{o.id} ({o.shop.Name}) kurye bulunamadığı için iptal oldu. İtibar düştü.");
            }
        }

        float EstimateEta(Courier c, Order o)
        {
            float speed = Mathf.Max(0.05f, c.V.tilesPerMinute * c.speedSkill);
            return CityMap.Manhattan(c.FreeTile, o.shop.door) / speed + c.OrderCount * 12f;
        }

        void AutoAssign()
        {
            for (int i = 0; i < Pending.Count; i++)
            {
                var o = Pending[i];
                Courier best = null;
                float bestScore = float.MaxValue;
                foreach (var c in CourierManager.I.Couriers)
                {
                    if (!c.CanTakeMore || !c.CanCarry(o) || o.rejectedBy.Contains(c)) continue;
                    int profit = EconomyManager.I.ProfitIf(o, c);
                    if (profit < 0) continue; // asla zararına iş yok
                    float score = EstimateEta(c, o);
                    if (prioritizeProfit) score -= profit / 40f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = c;
                    }
                }
                if (best != null && Assign(o, best)) i--;
            }
        }

        /// <summary>
        /// Siparişi kuryeye teklif eder. Zararına ise reddedilir.
        /// Moralsiz / yorgun kurye teklifi geri çevirebilir.
        /// </summary>
        public bool Assign(Order o, Courier c)
        {
            if (o.status != OrderStatus.Bekliyor || c == null || !c.CanTakeMore || o.rejectedBy.Contains(c)) return false;
            if (!c.CanCarry(o))
            {
                EventManager.I.Notify($"#{o.id} ({GameData.KgText(o.weightKg)}) {c.ShortName}'ın aracına fazla ağır.");
                return false;
            }

            int pay = EconomyManager.I.CourierPay(c, o.distance);
            if (o.CompanyIncome - pay < 0)
            {
                EventManager.I.Notify($"#{o.id} {c.ShortName}'a atanamadı: zararına iş olur ({Ft(o.CompanyIncome - pay)}).");
                return false;
            }

            if (Random.value < c.RejectChance)
            {
                o.rejectedBy.Add(c);
                rejectedToday++;
                EventManager.I.Notify($"{c.ShortName} #{o.id} siparişini reddetti (moral %{c.morale * 100f:0}, yorgunluk %{c.fatigue * 100f:0}).");
                return false;
            }

            o.courierPay = pay;
            o.boostPay = CampaignManager.I.BoostAmount(pay); // müşteriye yansımaz, kârdan ödenir
            o.courier = c;
            o.status = OrderStatus.Atandi;
            Pending.Remove(o);
            Active.Add(o);
            if (o.marker != null) o.marker.color = o.IsHeavy ? new Color(0.9f, 0.65f, 1f) : new Color(1f, 0.85f, 0.3f);
            c.Assign(o);
            return true;
        }

        /// <summary>
        /// Müşteri bazen bahşiş verir. Bahşiş tamamen kuryenin; şirket kasasına girmez.
        /// İhtimal: müşteri tipi, puan, hava, gece ve ağır paket etkiler.
        /// </summary>
        void RollTip(Order o, Courier c, float late)
        {
            float chance;
            switch (o.customerType)
            {
                case CustomerType.Comert: chance = 0.70f; break;
                case CustomerType.Aceleci: chance = late > 0f ? 0f : 0.15f; break;
                case CustomerType.Zor: chance = 0.03f; break;
                default: chance = 0.15f; break;
            }
            if (o.rating < 3f) chance = 0f;
            else if (o.rating >= 4.5f) chance *= 1.5f;

            var w = WeatherSystem.I;
            bool badWeather = w.IsRainy || w.Current == WeatherKind.Karli || w.Current == WeatherKind.Soguk;
            int h = GameClock.I.Hour;
            if (badWeather) chance *= 1.5f;           // "bu havada geldin, sağ ol"
            if (h >= 22 || h < 6) chance *= 1.3f;     // gece teslimatı
            if (o.IsHeavy) chance *= 1.5f;            // ağır paketi kapıya kadar taşıdı

            if (Random.value >= Mathf.Min(0.95f, chance)) return;

            float max = o.customerType == CustomerType.Comert ? 1500f : 800f;
            if (o.basket > 15000) max += 500f;
            o.tip = Mathf.RoundToInt(Random.Range(200f, max) / 50f) * 50;

            c.Earn(o.tip);
            c.tipsTotal += o.tip;
            c.tipsToday += o.tip;
            c.morale = Mathf.Min(1f, c.morale + (o.tip >= 800 ? 0.04f : 0.02f));
            if (o.tip >= 800)
            {
                string why = badWeather ? " (kötü havada geldiği için)" : (o.IsHeavy ? " (ağır paketi kapıya taşıdığı için)" : "");
                EventManager.I.Notify($"{c.ShortName} müşteriden {GameData.Ft(o.tip)} bahşiş aldı{why}!");
            }
        }

        public void CompleteDelivery(Order o, Courier c)
        {
            float now = GameClock.I.TotalMinutes;
            o.status = OrderStatus.Teslim;
            o.deliveredAt = now;

            float late = now - o.deadlineAt;
            float rating = 5f;
            if (late > 0f) rating -= Mathf.Min(3f, late / 10f);
            rating += (c.politeness - 0.75f) * 2f;
            rating += (c.morale - 0.6f) * 0.8f; // mutlu kurye güler yüzlüdür
            if (o.shop.def.hotFood && !c.Has(EquipmentKind.TermalCanta) && o.DistanceKm > 2f) rating -= 0.8f;
            if (o.customerType == CustomerType.Zor) rating -= 0.7f;
            if (o.customerType == CustomerType.Aceleci && late > 0f) rating -= 1f;
            o.rating = Mathf.Clamp(rating, 1f, 5f);

            RollTip(o, c, late);

            EconomyManager.I.RegisterDelivery(o);
            CompanyProfile.Reputation = Mathf.Lerp(CompanyProfile.Reputation, o.rating, 0.04f);
            c.morale = Mathf.Clamp01(c.morale + (o.rating >= 4f ? 0.01f : -0.02f));

            if (o.marker != null) Destroy(o.marker.gameObject);
            Active.Remove(o);
            History.Insert(0, o);
            if (History.Count > 50) History.RemoveAt(History.Count - 1);

            CampaignManager.I.OnDelivered(c);
            EventManager.I.RollDeliveryIncident(o, c);
        }
    }
}
