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
        public int cancelledToday;  // kurye bulunamadığı için iptal (toplam)
        public int busyLostToday;   // platform "yoğun" gösterdiği için sipariş vermeyen müşteri (itibar düşmez)
        public int cancelledRejectedToday; // bunlardan: kuryeler gördü ama teklifi reddetti
        public readonly int[] cancelledByHour = new int[24];
        public float reputationAtDayStart = -1f;
        public float cancelReputationPenalty = 0.015f;
        [Tooltip("Dükkanın teslimat yarıçapı (kare, kuş uçuşu değil Manhattan). 1 kare ≈ 0,2 km.")]
        public int deliveryRadius = 16;
        [Tooltip("Siparişlerin yüzde kaçı yarıçaptan bağımsız, şehrin herhangi bir açık yerinden gelir (uzak sipariş).")]
        [Range(0f, 1f)] public float farOrderChance = 0.2f;
        public int rejectedToday;   // kuryelerin reddettiği teklif
        public int heavyLostToday;  // taşıyabilecek kurye olmadığı için alınamayan ağır sipariş
        public int batchedToday;    // toplu teslimata eklenen sipariş
        bool heavyWarnedToday;

        float spawnAcc;
        float assignTimer;
        Transform markerRoot;

        static string Ft(long v) => GameData.Ft(v);

        void Awake() { I = this; }

        public void Init()
        {
            reputationAtDayStart = CompanyProfile.Reputation;
            markerRoot = new GameObject("Müşteriler").transform;
            markerRoot.SetParent(transform, false);
            GameClock.I.OnNewDay += () => { busyLostToday = 0; cancelledRejectedToday = 0; System.Array.Clear(cancelledByHour, 0, 24); reputationAtDayStart = CompanyProfile.Reputation; lostToday = 0; cancelledToday = 0; rejectedToday = 0; heavyLostToday = 0; heavyWarnedToday = false; batchedToday = 0; };
        }

        /// <summary>
        /// Saate göre talep çarpanı, dükkan türüne göre ayrı:
        /// • Yemek (hafta içi): 10–13 ofisten / home office'ten öğle siparişi zirvesi, 16'dan sonra iş çıkışı, akşam yemeği zirvesi.
        /// • Market (hafta içi): 16–20 iş çıkışı, evde yemek yapacaklar için en yoğun saatler.
        /// • Hafta sonu: sabahlar çok sakin; yemek öğle ve akşam, market öğleden önce/sonra alışveriş.
        /// </summary>
        public static float DemandCurve(ShopDef shop, int h, bool weekend)
        {
            if (shop.isMarket)
            {
                if (weekend)
                {
                    if (h < 8) return 0.02f;
                    if (h < 10) return 0.3f;
                    if (h < 14) return 1.2f;   // hafta sonu alışverişi
                    if (h < 18) return 1.0f;
                    if (h < 21) return 0.9f;
                    return 0.2f;
                }
                if (h < 6) return 0.02f;
                if (h < 8) return 0.2f;
                if (h < 10) return 0.5f;
                if (h < 13) return 0.7f;
                if (h < 16) return 0.6f;
                if (h < 20) return 1.8f;       // iş çıkışı: evde yemek yapacaklar
                if (h < 22) return 0.8f;
                return 0.2f;
            }

            if (weekend)
            {
                if (h < 2) return 0.08f;
                if (h < 8) return 0.02f;
                if (h < 11) return 0.12f;
                if (h < 15) return 1.5f;
                if (h < 18) return 0.9f;
                if (h < 22) return 1.6f;
                return 0.4f;
            }
            if (h < 2) return 0.1f;
            if (h < 6) return 0.03f;
            if (h < 8) return 0.15f;
            if (h < 10) return 0.4f;
            if (h < 13) return 1.8f;           // ofis / home office öğle siparişleri
            if (h < 14) return 1.0f;
            if (h < 16) return 0.6f;
            if (h < 18) return 1.1f;           // iş çıkışı başlıyor
            if (h < 21) return 1.7f;           // akşam yemeği
            if (h < 23) return 0.8f;
            return 0.3f;
        }

        /// <summary>Dükkanın şu anki talep ağırlığı (yoğunluk × saat çarpanı).</summary>
        static float ShopDemand(Shop s) =>
            s.def.weight * DemandCurve(s.def, GameClock.I.Hour, GameClock.I.IsWeekend) * RivalShare(s);

        /// <summary>Rakip de bu dükkanla çalışıyorsa siparişler paylaşılır; münhasırsa artar.</summary>
        static float RivalShare(Shop s) => RivalCompany.I != null ? RivalCompany.I.ShareFor(s) : 1f;

        /// <summary>Yakınlaşınca müşteri noktaları binanın çatısına oturur (3D binaların içinde kalmasın).</summary>
        void LateUpdate()
        {
            if (City3D.I == null) return;
            LiftMarkers(Pending);
            LiftMarkers(Active);
        }

        static void LiftMarkers(List<Order> list)
        {
            foreach (var o in list)
            {
                if (o.marker == null) continue;
                var p = o.marker.transform.position;
                p.z = -City3D.I.HeightAt(Vector2Int.RoundToInt(new Vector2(p.x, p.y))) - 0.03f;
                o.marker.transform.position = p;
            }
        }

        void Update()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            float dm = GameClock.I.DeltaMinutes;
            if (dm <= 0f) return;

            // Her sözleşmeli dükkan kendi türüne göre (yemek / market) saatlik talep üretir
            float demand = 0f;
            foreach (var s in CityMap.I.Shops)
                if (s.contracted && CityMap.I.IsUnlocked(s.tile))
                    demand += DemandCurve(s.def, GameClock.I.Hour, GameClock.I.IsWeekend) * RivalShare(s);

            float rate = baseRatePerShop * demand
                         * (0.55f + 0.15f * CompanyProfile.Reputation) * WeatherSystem.I.DemandFactor * UpgradeManager.I.OrderRateMultiplier
                         * Difficulty.OrderRate;
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
                if (s.contracted && CityMap.I.IsUnlocked(s.tile)) total += ShopDemand(s);
            if (total <= 0f) return null;

            float r = Random.value * total;
            foreach (var s in CityMap.I.Shops)
            {
                if (!s.contracted || !CityMap.I.IsUnlocked(s.tile)) continue;
                r -= ShopDemand(s);
                if (r <= 0f) return s;
            }
            return null;
        }

        [Header("Nakit ödeme")]
        [Range(0f, 0.6f)] public float cashChanceFood = 0.12f;
        [Range(0f, 0.6f)] public float cashChanceMarket = 0.25f;

        [Header("Adres sorunları")]
        [Range(0f, 0.2f)] public float wrongAddressChance = 0.025f;
        [Range(0f, 0.2f)] public float notHomeChance = 0.02f;

        AddressIssue RandomAddressIssue()
        {
            float r = Random.value;
            float wrong = wrongAddressChance * UpgradeManager.I.WrongAddressMultiplier;
            float notHome = notHomeChance * UpgradeManager.I.NotHomeMultiplier;
            if (r < wrong) return AddressIssue.YanlisAdres;
            if (r < wrong + notHome) return AddressIssue.EvdeYok;
            return AddressIssue.Yok;
        }

        /// <summary>
        /// Kurye kapıda 10 dk uğraştıktan sonra çağrılır.
        /// Yanlış adres: vardiyadaki destek müşteriyi arayıp doğru adresi bulur (itibar artar);
        /// yoksa kurye kendisi dener. Evde yok: müşteri gelebilir; gelmezse destek arar.
        /// Hepsi başarısızsa sipariş kuryede kalır (çok nadir).
        /// </summary>
        public void ResolveAddressIssue(Order o, Courier c)
        {
            if (o == null) return;
            o.issueResolved = true;
            o.customerFault = true;

            if (o.addressIssue == AddressIssue.YanlisAdres)
            {
                bool solved = SupportManager.I.TryResolveWrongAddress(o);
                if (!solved && Random.value < 0.75f)
                {
                    solved = true;
                    EventManager.I.Notify($"{c.ShortName} müşteriye kendisi ulaştı ve doğru adresi öğrendi (#{o.id}).");
                }
                if (solved)
                {
                    // Doğru adres: yakınlardaki başka bir bina
                    var map = CityMap.I;
                    var realTile = map.RandomCustomerTileNear(o.customerTile, 8);
                    for (int i = 0; i < 10 && CityMap.Manhattan(realTile, o.customerTile) > 8; i++)
                        realTile = map.RandomCustomerTileNear(o.customerTile, 8);
                    var realDoor = map.GetDoor(realTile);
                    int extra = Mathf.Max(1, map.FindPath(o.customerDoor, realDoor).Count - 1);

                    // Ek yol için kuryeye km ücreti (şirket öder)
                    int extraPay = Mathf.RoundToInt(c.V.payPerKm * GameData.Km(extra));
                    o.courierPay += extraPay;
                    o.distance += extra;
                    o.customerTile = realTile;
                    o.SetAddress(realTile, realDoor);
                    if (o.marker != null) o.marker.transform.position = CityMap.ToWorld(realTile);
                    c.RedirectTo(realDoor);
                    return;
                }
            }
            else // Evde yok
            {
                if (Random.value < 0.7f)
                {
                    EventManager.I.Notify($"#{o.id} müşterisi birkaç dakika sonra geldi, sipariş teslim edildi.");
                    c.DeliverHere();
                    return;
                }
                if (SupportManager.I.TryReachAbsentCustomer(o))
                {
                    c.DeliverHere();
                    return;
                }
            }

            // Hiçbir şekilde ulaşılamadı
            c.KeepOrder();
        }

        /// <summary>
        /// Müşteriye ulaşılamadı: müşteri yine ücreti öder (iade yok), kurye ücretini alır
        /// ve yemek kuryeye kalır. Puan ve itibar etkilenmez.
        /// </summary>
        public void CompleteKept(Order o, Courier c)
        {
            o.status = OrderStatus.Teslim;
            o.keptByCourier = true;
            o.deliveredAt = GameClock.I.TotalMinutes;
            o.rating = 0f;
            EconomyManager.I.RegisterDelivery(o);
            c.morale = Mathf.Min(1f, c.morale + 0.03f); // bedava yemek :)
            if (o.marker != null) Destroy(o.marker.gameObject);
            Active.Remove(o);
            History.Insert(0, o);
            if (History.Count > 50) History.RemoveAt(History.Count - 1);
            CampaignManager.I.OnDelivered(c);
            EventManager.I.Notify($"#{o.id} müşterisine hiçbir şekilde ulaşılamadı. Sipariş {c.ShortName}'da kaldı " +
                                  "(müşteri ücreti ödedi, yemek kuryenin oldu).");
        }

        static CustomerType RandomCustomerType()
        {
            float r = Random.value;
            if (r < 0.55f) return CustomerType.Normal;
            if (r < 0.75f) return CustomerType.Aceleci;
            if (r < 0.90f) return CustomerType.Comert;
            return CustomerType.Zor;
        }

        /// <summary>
        /// Wolt gibi yoğunluk modu: bekleyen sipariş boştaki kuryeleri çok aşarsa uygulama müşteriye
        /// "yoğunluk var, teslimat uzun sürebilir" gösterir; bir kısmı hiç sipariş vermez. İtibar düşmez,
        /// ama o sipariş de kaybolur.
        /// </summary>
        public int BusyExcess
        {
            get
            {
                int free = 0;
                foreach (var c in CourierManager.I.Couriers) if (c.Available && c.CanTakeMore) free++;
                return Pending.Count - free - 2;
            }
        }
        public bool IsBusy => BusyExcess > 0;

        void TrySpawn()
        {
            var shop = PickShop();
            if (shop == null) return;

            int excess = BusyExcess;
            if (excess > 0 && Random.value < Mathf.Min(0.75f, excess * 0.12f))
            {
                busyLostToday++;
                OrderHeat.Record(shop.tile, true);   // müşteri henüz belli değil: dükkanın yeri
                return;
            }

            var map = CityMap.I;
            // Çoğu müşteri dükkanın yakınından sipariş verir, ama bazıları şehrin uzak bir yerinden de verebilir
            // (uzak siparişte teslimat ücreti, kurye kazancı ve söz verilen süre mesafeyle birlikte artar)
            var custTile = Random.value < farOrderChance
                ? map.RandomCustomerTile(shop.tile)
                : map.RandomCustomerTileNear(shop.tile, deliveryRadius);
            var custDoor = map.GetDoor(custTile);
            int dist = Mathf.Max(1, map.FindPath(shop.door, custDoor).Count - 1);

            int basket = GameData.Round10(Random.Range(shop.def.basketMin, shop.def.basketMax));
            // Sepet gerçek ürünlerden oluşur (menüden); tutar ürünlerin toplamı
            var items = Menus.Generate(shop.def, basket);
            basket = Mathf.Max(shop.def.basketMin / 2, Menus.Sum(items));

            // Ağırlık: sepet büyüdükçe artar, biraz da rastgelelik
            float t = Mathf.InverseLerp(shop.def.basketMin, shop.def.basketMax, basket);
            float kg = Mathf.Round(Mathf.Lerp(shop.def.kgMin, shop.def.kgMax, t * 0.7f + Random.value * 0.3f) * 10f) / 10f;

            // Ekipte bu ağırlığı taşıyabilecek kurye yoksa sipariş kabul edilmez
            if (!EconomyManager.AnyoneCanCarry(kg))
            {
                heavyLostToday++;
                OrderHeat.Record(custTile, true);
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
            var quote = EconomyManager.I.QuoteFee(commission, dist, kg);
            int fee = quote.Total;

            // Pahalı teslimat ücreti = müşteri vazgeçebilir (kötü hava ücretini çoğu müşteri anlayışla karşılar)
            float accept = Mathf.Clamp(1.35f - (fee - quote.weather * 0.7f) / 2200f, 0.15f, 1f);
            if (RivalCompany.I != null) accept *= RivalCompany.I.PriceSensitivity; // rakip fiyat kırıyorsa
            if (Random.value > accept)
            {
                lostToday++;
                OrderHeat.Record(custTile, true);
                return;
            }

            float now = GameClock.I.TotalMinutes;
            var o = new Order
            {
                shop = shop,
                customerTile = custTile,
                customerDoor = custDoor,
                customerType = RandomCustomerType(),
                addressIssue = RandomAddressIssue(),
                basket = basket,
                distance = dist,
                weightKg = kg,
                commissionIncome = commission,
                deliveryFee = fee,
                feeBase = quote.basePart,
                feeKm = quote.kmPart,
                feeExtra = quote.extra,
                feeWeather = quote.weather,
                createdAt = now,
                items = items,
                customerName = GameData.RandomName(),
                customerNote = Addresses.RandomNote()
            };
            o.SetAddress(custTile, custDoor);
            // Nakit ödeme: marketlerde daha sık. Müşteri yuvarlak banknot verir, para üstü hazırlanır.
            if (Random.value < (shop.def.isMarket ? cashChanceMarket : cashChanceFood))
            {
                o.cashPay = true;
                int amt = o.CashAmount;
                int[] steps = { 1000, 5000, 10000, 20000 };
                int step = steps[Random.Range(0, steps.Length)];
                o.cashGiven = (amt + step - 1) / step * step;
                if (o.cashGiven == amt && Random.value < 0.5f) o.cashGiven += 1000;
            }
            o.readyAt = now + shop.def.prepMinutes * shop.prepMultiplier * Random.Range(0.8f, 1.3f);
            o.deadlineAt = o.readyAt + dist / 0.45f + (o.customerType == CustomerType.Aceleci ? 7f : 15f)
                           + o.DoorMinutes(1f, true); // müşteri katını da hesaba katan tahmini süre
            o.marker = SpriteFactory.Create($"Müşteri #{o.id}", markerRoot, true,
                o.IsHeavy ? new Color(0.75f, 0.35f, 1f) : new Color(1f, 0.6f, 0.2f),
                CityMap.ToWorld(custTile), o.IsHeavy ? 0.6f : 0.45f, 15); // 3D binaların üstünde görünsün

            Pending.Add(o);
            shop.ordersTotal++;
            OrderHeat.Record(custTile, false);
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
                cancelledByHour[GameClock.I.Hour]++;
                OrderHeat.Record(o.customerTile, true);
                MapFx.Cancelled(o);
                if (GoalManager.I != null) GoalManager.I.OnCancelled();
                if (RivalCompany.I != null) RivalCompany.I.OnCancelled(o);
                bool rejected = o.priceDeclines > 0 || o.rejectedBy.Count > 0;
                if (rejected) cancelledRejectedToday++;
                CompanyProfile.AddReputation(-cancelReputationPenalty);
                EventManager.I.Notify(rejected
                    ? $"#{o.id} ({o.shop.Name}) iptal oldu: kuryeler teklifi gördü ama kabul etmedi. İtibar düştü."
                    : $"#{o.id} ({o.shop.Name}) kurye bulunamadığı için iptal oldu. İtibar düştü.");
            }
        }

        float EstimateEta(Courier c, Order o)
        {
            // Toplu siparişe uygun kuryeler öne alınır (aynı/yakın dükkan, yakın müşteri)
            float eta = c.EstimateMinutes(o);
            if (c.CanBatch(o)) eta -= 8f;
            if (c.CanBatch(o) && c.CustomerNearRoute(o)) eta -= 4f;
            return eta;
        }

        void AutoAssign()
        {
            float now = GameClock.I.TotalMinutes;
            for (int i = 0; i < Pending.Count; i++)
            {
                var o = Pending[i];

                // 10 dk'dan uzun bekleyen ve fiyat yüzünden reddedilen sipariş: reddedenlere bir kez daha, daha esnek sorulur
                if (!o.secondRound && o.priceDeclines > 0 && now - o.createdAt > 10f)
                {
                    o.secondRound = true;
                    o.rejectedBy.Clear();
                }

                Courier best = null;
                float bestScore = float.MaxValue;
                foreach (var c in CourierManager.I.Couriers)
                {
                    if (!c.CanTakeMore || !c.CanCarry(o) || o.rejectedBy.Contains(c)) continue;
                    if (o.cashPay && !c.CanTakeCash(o.CashAmount)) continue; // nakit limiti
                    int profit = EconomyManager.I.ProfitIf(o, c);
                    if (profit < EconomyManager.I.RequiredAssignProfit(o)) continue; // zararına / çok az kârlı iş yok
                    float score = EstimateEta(c, o);
                    if (UpgradeManager.I.SmartDispatch)
                    {
                        // Akıllı dispatcher: teklifi beğenmesi muhtemel kuryeyi öne alır
                        int courierGets = EconomyManager.I.CourierPay(c, o.distance) + CampaignManager.I.BoostAmount(EconomyManager.I.CourierPay(c, o.distance));
                        if (!c.LikesOffer(o, courierGets)) score += 30f;
                        if (prioritizeProfit) score -= profit / 40f;
                    }
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
        public bool Assign(Order o, Courier c) => Assign(o, c, false);

        public bool Assign(Order o, Courier c, bool manual)
        {
            if (o.status != OrderStatus.Bekliyor || c == null || !c.CanTakeMore || o.rejectedBy.Contains(c)) return false;
            if (o.cashPay && !c.CanTakeCash(o.CashAmount))
            {
                if (manual) EventManager.I.Notify($"#{o.id} nakit sipariş: {c.ShortName}'ın nakit limiti dolu ({GameData.Ft(c.cashHeld)} / {GameData.Ft(Courier.CashLimit)}).");
                return false;
            }
            if (!c.CanCarry(o))
            {
                EventManager.I.Notify($"#{o.id} ({GameData.KgText(o.weightKg)}) {c.ShortName}'ın aracına fazla ağır.");
                return false;
            }

            int pay = EconomyManager.I.CourierPay(c, o.distance);
            int need = EconomyManager.I.RequiredAssignProfit(o);
            if (o.CompanyIncome - pay < need)
            {
                EventManager.I.Notify(o.CompanyIncome - pay < 0
                    ? $"#{o.id} {c.ShortName}'a atanamadı: zararına iş olur ({Ft(o.CompanyIncome - pay)})."
                    : $"#{o.id} {c.ShortName}'a atanamadı: kâr {Ft(o.CompanyIncome - pay)}, en az {Ft(need)} olmalı.");
                return false;
            }

            // Kurye tahmini kazancı görür; beğenmezse reddeder, sipariş başka kuryeyi bekler
            int boost = CampaignManager.I.BoostAmount(pay);
            if (!c.WillAcceptOffer(o, pay + boost))
            {
                c.NoteOffer(o, pay + boost, false, $"kazanç düşük (saatlik ~{Ft((long)c.OfferHourly(o, pay + boost))})");
                o.rejectedBy.Add(c);
                o.priceDeclines++;
                rejectedToday++;
                if (manual)
                    EventManager.I.Notify($"{c.ShortName} #{o.id} teklifini beğenmedi: tahmini {Ft(pay + boost)} için ~{c.EstimateMinutes(o):0} dk " +
                                          $"(saatlik ~{Ft((long)c.OfferHourly(o, pay + boost))}).");
                return false;
            }

            if (Random.value < c.RejectChance)
            {
                c.NoteOffer(o, pay + boost, false, c.fatigue >= 0.75f ? "çok yorgun" : "morali düşük");
                o.rejectedBy.Add(c);
                rejectedToday++;
                EventManager.I.Notify($"{c.ShortName} #{o.id} siparişini reddetti (moral %{c.morale * 100f:0}, yorgunluk %{c.fatigue * 100f:0}).");
                return false;
            }

            c.NoteOffer(o, pay + boost, true, "");
            o.courierPay = pay;
            o.boostPay = CampaignManager.I.BoostAmount(pay); // müşteriye yansımaz, kârdan ödenir
            o.courier = c;
            o.status = OrderStatus.Atandi;
            Pending.Remove(o);
            Active.Add(o);
            if (o.marker != null) o.marker.color = o.IsHeavy ? new Color(0.9f, 0.65f, 1f) : new Color(1f, 0.85f, 0.3f);
            c.Assign(o);
            if (o.batched) batchedToday++;
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

            if (o.cashPay)
                c.EarnCashTip(o.tip);                       // nakit bahşiş doğrudan kuryenin cebine
            else
            {
                EconomyManager.I.money += o.tip;           // müşteri uygulamadan ödedi (gelir değil, kuryeye aktarılır)
                c.Earn(o.tip);                             // kuryenin hesabına yazılır, kesimde ödenir
            }
            c.tipsTotal += o.tip;
            c.tipsToday += o.tip;
            c.morale = Mathf.Min(1f, c.morale + (o.tip >= 800 ? 0.04f : 0.02f));
            if (o.tip >= 800)
            {
                string why = badWeather ? " (kötü havada geldiği için)" : (o.IsHeavy ? " (ağır paketi kapıya taşıdığı için)" : "");
                EventManager.I.Notify($"{c.ShortName} müşteriden {GameData.Ft(o.tip)} bahşiş aldı{why}!");
            }
        }

        /// <summary>Kurye teslim almayı iptal etti: sipariş tekrar bekleyenlere döner, o kuryeye bir daha teklif edilmez.</summary>
        public void ReturnToPending(Order o, Courier c)
        {
            o.status = OrderStatus.Bekliyor;
            o.courier = null;
            o.courierPay = 0;
            o.boostPay = 0;
            o.rejectedBy.Add(c);
            o.createdAt = GameClock.I.TotalMinutes; // yeni kurye için süre baştan başlar
            o.supportCalled = false;
            Active.Remove(o);
            if (!Pending.Contains(o)) Pending.Insert(0, o);
            if (o.marker != null) o.marker.color = o.IsHeavy ? new Color(0.75f, 0.35f, 1f) : new Color(1f, 0.6f, 0.2f);
        }

        public void CompleteDelivery(Order o, Courier c)
        {
            float now = GameClock.I.TotalMinutes;
            o.status = OrderStatus.Teslim;
            o.deliveredAt = now;

            float late = o.customerFault ? 0f : now - o.deadlineAt; // müşterinin adres hatası gecikmesi sayılmaz
            float rating = 5f;
            if (late > 0f) rating -= Mathf.Min(3f, late / 10f);
            rating += (c.politeness - 0.75f) * 2f;
            rating += (c.morale - 0.6f) * 0.8f; // mutlu kurye güler yüzlüdür
            if (o.shop.def.hotFood)
            {
                // Sıcak yemek: termal çantayla 30 dk, çantasız 15 dk sıcak kalır (toplu siparişte risk)
                float carried = now - o.pickedAt;
                float limit = c.Has(EquipmentKind.TermalCanta) ? 30f : 15f;
                if (carried > limit)
                {
                    rating -= 0.8f;
                    o.deliveredCold = true;
                }
            }
            if (o.customerType == CustomerType.Zor) rating -= 0.7f;
            if (o.customerType == CustomerType.Aceleci && late > 0f) rating -= 1f;
            if (c.hygiene < Courier.HygieneLimit) rating -= (Courier.HygieneLimit - c.hygiene) * 3f; // kirli çanta
            o.rating = Mathf.Clamp(rating, 1f, 5f);

            RollTip(o, c, late);
            if (RivalCompany.I != null) RivalCompany.I.OnDelivered(o, late);

            EconomyManager.I.RegisterDelivery(o);
            MapFx.Delivered(o);   // haritada "+350 Ft" ve teslimat sesi
            CompanyProfile.Reputation = Mathf.Lerp(CompanyProfile.Reputation, o.rating, 0.04f);
            c.morale = Mathf.Clamp01(c.morale + (o.rating >= 4f ? 0.01f : -0.02f));

            if (o.marker != null) Destroy(o.marker.gameObject);
            Active.Remove(o);
            History.Insert(0, o);
            if (History.Count > 50) History.RemoveAt(History.Count - 1);

            CampaignManager.I.OnDelivered(c);
            if (GoalManager.I != null) GoalManager.I.OnDelivered(o);
            EventManager.I.RollHygieneComplaint(o, c);
            EventManager.I.RollDeliveryIncident(o, c);
        }
    }
}
