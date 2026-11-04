using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    [System.Serializable]
    public class DarkStoreSave
    {
        public bool open;
        public int tileX, tileY;
        public int size;
        public long[] stock;
        public int[] assortment;
        public bool autoRestock;
        public float restockTarget;
        public int openedDay;
        public long salesTotal, profitTotal, spoiledTotal;
        public int ordersTotal, lostTotal;
        public long salesToday, profitToday;
        public int ordersToday, lostToday;
    }

    /// <summary>
    /// Kendi marketin (Wolt Market / dark store gibi): şehirde kendi deponu açarsın.
    ///  • Komisyon yok: satışın tamamı senin, ama ürünleri sen alırsın (stok). Brüt marj ~%30.
    ///  • 5 reyon: temel gıda, taze ürün, içecek, atıştırmalık, ev &amp; bakım. Her reyonun stoğu (alış fiyatıyla) ve çeşit seviyesi var.
    ///  • Çeşit arttıkça daha çok sipariş gelir, ama raf/soğutma gideri artar. Taze ürün her gün bir miktar bozulur.
    ///  • Stok biten reyondaki ürünler siparişten eksik çıkar; sepetin çoğu yoksa sipariş kaçar.
    ///  • Depo büyüklüğü: küçük / orta / büyük — kira, toplayıcı personel, stok kapasitesi ve talep buna göre.
    ///  • Otomatik stok: her sabah reyonları hedef seviyeye tamamlar (serbest kasadan).
    /// </summary>
    public class DarkStore : MonoBehaviour
    {
        public static DarkStore I { get; private set; }

        public const string StoreName = "Kurye Market";
        public const float Margin = 0.30f;              // brüt marj: satışın %30'u kâr, %70'i malın alış fiyatı
        public const int RequiredDistrict = 2;          // Doğu Yakası açılınca

        public static readonly string[] AisleNames = { "Temel gıda", "Taze ürün", "İçecek", "Atıştırmalık & tatlı", "Ev & kişisel bakım" };
        static readonly float[] AisleShare = { 0.30f, 0.20f, 0.25f, 0.15f, 0.10f };
        public const int Aisles = 5;

        // Depo büyüklüğü (1–3)
        public static readonly string[] SizeNames = { "", "Küçük depo (80 m²)", "Orta depo (200 m²)", "Büyük depo (450 m²)" };
        public static readonly long[] SizeCost = { 0, 900000, 1400000, 2600000 };   // açılış / büyütme bedeli (raf, soğutucu, tadilat)
        public static readonly long[] SizeRent = { 0, 14000, 26000, 48000 };        // günlük kira + elektrik
        public static readonly int[] SizePickers = { 0, 1, 2, 4 };                 // toplayıcı (günlük ücret aşağıda)
        public const long PickerDaily = 15000;
        public static readonly long[] SizeCapacity = { 0, 260000, 650000, 1500000 };// toplam stok kapasitesi (alış fiyatıyla)
        static readonly float[] SizeDemand = { 0f, 1f, 1.6f, 2.4f };

        // Çeşit seviyesi (1–3): talep çarpanı ve reyon başı günlük raf gideri
        public static readonly string[] AssortNames = { "", "Temel", "Geniş", "Çok geniş" };
        static readonly float[] AssortDemand = { 0f, 0.8f, 1f, 1.2f };
        public static readonly long[] AssortDaily = { 0, 0, 1500, 3500 };

        public bool IsOpen => shop != null;
        [System.NonSerialized] public Shop shop;   // haritadaki dükkan (kayda DarkStoreSave ile yazılır)
        public int size = 1;
        public readonly long[] stock = new long[Aisles];
        public readonly int[] assortment = { 2, 2, 2, 2, 2 };
        public bool autoRestock = true;
        public float restockTarget = 0.8f;              // otomatik stok: kapasitenin bu oranına tamamla
        public int openedDay;
        public long salesTotal, profitTotal, spoiledTotal;
        public int ordersTotal, lostTotal;
        public long salesToday, profitToday;
        public int ordersToday, lostToday;
        bool lowWarnedToday;

        ShopDef def;

        void Awake() { I = this; }

        public void Init()
        {
            GameClock.I.OnNewDay += OnNewDay;
        }

        ShopDef MakeDef()
        {
            if (def != null) return def;
            def = new ShopDef
            {
                name = StoreName,
                color = CompanyProfile.Primary,
                prepMinutes = 6,
                basketMin = 3000, basketMax = 16000,
                commission = 1f,
                weight = 0.7f,
                contractCost = 0,
                unlockLevel = 1,
                hotFood = false,
                isMarket = true,
                kgMin = 1.5f, kgMax = 16f
            };
            Menus.Register(StoreName, "Szpar Express", 0.98f);   // zincir marketten biraz ucuz
            return def;
        }

        // ---------------------------------------------------------------- hesaplar

        public long Capacity => SizeCapacity[size];
        public long AisleCapacity(int a) => (long)(Capacity * AisleShare[a] * (0.8f + 0.2f * assortment[a]) / 1.2f);
        public long TotalStock { get { long t = 0; foreach (var v in stock) t += v; return t; } }
        public float Fill01(int a) => AisleCapacity(a) <= 0 ? 0f : Mathf.Clamp01(stock[a] / (float)AisleCapacity(a));

        float AssortFactor
        {
            get
            {
                float f = 0f;
                for (int a = 0; a < Aisles; a++) f += AisleShare[a] * AssortDemand[assortment[a]];
                return f;
            }
        }

        /// <summary>Talep ağırlığı: depo büyüklüğü × çeşit × (stok doluluğu: boş raflı markete müşteri az gelir).</summary>
        public void UpdateWeight()
        {
            if (shop == null) return;
            float fill = 0f;
            for (int a = 0; a < Aisles; a++) fill += AisleShare[a] * Fill01(a);
            shop.def.weight = 0.7f * SizeDemand[size] * AssortFactor * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(fill * 2.5f));
            shop.def.color = CompanyProfile.Primary;
        }

        public long DailyCost
        {
            get
            {
                long c = SizeRent[size] + SizePickers[size] * PickerDaily;
                for (int a = 0; a < Aisles; a++) c += AssortDaily[assortment[a]];
                return c;
            }
        }

        public string LockReason
        {
            get
            {
                if (CityMap.I == null) return "";
                if (CityMap.I.UnlockLevel < RequiredDistrict) return $"Önce {CityMap.DistrictNames[RequiredDistrict]} bölgesini aç.";
                return "";
            }
        }

        // ---------------------------------------------------------------- işlemler

        public bool Open()
        {
            if (IsOpen || LockReason != "") return false;
            if (!EconomyManager.I.TrySpend(SizeCost[1], $"{StoreName} açılışı (raf, soğutucu, tadilat)", ExpenseCategory.Gelistirme)) return false;
            var s = CityMap.I.AddOwnShop(MakeDef());
            if (s == null)
            {
                EconomyManager.I.AddIncome(SizeCost[1], $"{StoreName} açılışı iade (yer bulunamadı)");
                EventManager.I.Notify("Açık bölgede depo için boş yer bulunamadı.");
                return false;
            }
            shop = s;
            size = 1;
            openedDay = GameClock.I.Day;
            for (int a = 0; a < Aisles; a++) stock[a] = 0;
            UpdateWeight();
            if (City3D.I != null) City3D.I.Rebuild();
            CityMap.I.RefreshColors();
            EventManager.I.Notify($"{StoreName} açıldı! Depo yeri: {CityMap.DistrictNames[CityMap.RegionOf(s.tile)]}. Raflar boş: Market penceresinden stok al.");
            return true;
        }

        public bool Upgrade()
        {
            if (!IsOpen || size >= 3) return false;
            long cost = SizeCost[size + 1];
            if (!EconomyManager.I.TrySpend(cost, $"{StoreName}: {SizeNames[size + 1]}", ExpenseCategory.Gelistirme)) return false;
            size++;
            UpdateWeight();
            EventManager.I.Notify($"{StoreName} büyüdü: {SizeNames[size]}. Daha çok stok, daha çok sipariş, {SizePickers[size]} toplayıcı.");
            return true;
        }

        public void SetAssortment(int a, int level)
        {
            assortment[a] = Mathf.Clamp(level, 1, 3);
            stock[a] = System.Math.Min(stock[a], AisleCapacity(a));
            UpdateWeight();
        }

        /// <summary>Reyonu verilen doluluğa kadar tamamlar. Dönen: harcanan tutar.</summary>
        public long Restock(int a, float toFill, bool notify)
        {
            if (!IsOpen) return 0;
            long want = (long)(AisleCapacity(a) * Mathf.Clamp01(toFill)) - stock[a];
            if (want <= 1000) return 0;
            want = want / 100 * 100;
            long free = EconomyManager.I.FreeCash;
            if (free < want) want = System.Math.Max(0, free / 100 * 100);
            if (want <= 0)
            {
                if (notify) EventManager.I.Notify($"{StoreName}: {AisleNames[a]} için stok alınamadı, serbest kasa yetersiz.");
                return 0;
            }
            EconomyManager.I.Spend(want, $"{StoreName} stok: {AisleNames[a]}", ExpenseCategory.Stok);
            stock[a] += want;
            UpdateWeight();
            return want;
        }

        public long RestockAll(float toFill, bool notify)
        {
            long t = 0;
            for (int a = 0; a < Aisles; a++) t += Restock(a, toFill, false);
            if (notify && t > 0) EventManager.I.Notify($"{StoreName}: {GameData.Ft(t)} stok alındı.");
            return t;
        }

        /// <summary>
        /// Sipariş oluşurken OrderManager çağırır. Sepetin malı reyonlardan düşülür; olmayan ürünler sepetten çıkar.
        /// false: sepetin çoğu stokta yok, sipariş kaçtı.
        /// </summary>
        public bool TakeOrder(List<OrderItem> items, ref int basket, out int cost)
        {
            cost = 0;
            if (!IsOpen) return false;
            // Hangi reyondan ne kadar mal gerekiyor (alış fiyatıyla)
            float need = basket * (1f - Margin);
            float avail = 0f;
            var take = new float[Aisles];
            for (int a = 0; a < Aisles; a++)
            {
                float n = need * AisleShare[a];
                take[a] = Mathf.Min(n, stock[a]);
                avail += take[a];
            }
            float f = need <= 0f ? 1f : avail / need;
            if (f < 0.6f)
            {
                lostToday++; lostTotal++;
                if (!lowWarnedToday)
                {
                    lowWarnedToday = true;
                    EventManager.I.Notify($"{StoreName}: raflar boş, sipariş kaçtı! Market penceresinden stok al (ya da otomatik stoğu aç).");
                }
                return false;
            }
            if (f < 0.999f && items != null && items.Count > 1)
            {
                // Eksik ürünler sepetten çıkar (müşteri kalanını alır)
                int target = Mathf.RoundToInt(basket * f);
                int guard = 0;
                while (items.Count > 1 && Menus.Sum(items) > target && guard++ < 40)
                    items.RemoveAt(Random.Range(0, items.Count));
                basket = Mathf.Max(1, Menus.Sum(items));
                f = Mathf.Min(1f, basket * (1f - Margin) / Mathf.Max(1f, need));
            }
            for (int a = 0; a < Aisles; a++)
            {
                long t = (long)Mathf.Min(stock[a], need * AisleShare[a] * f);
                stock[a] -= t;
                cost += (int)t;
            }
            UpdateWeight();
            return true;
        }

        public void OnDelivered(Order o)
        {
            if (o == null || o.shop == null || !o.shop.own) return;
            ordersToday++; ordersTotal++;
            salesToday += o.basket; salesTotal += o.basket;
            long gross = o.basket - o.stockCost;
            profitToday += gross; profitTotal += gross;
        }

        void OnNewDay()
        {
            if (!IsOpen || GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            // Taze ürün bozulur (günde ~%12)
            long spoil = (long)(stock[1] * 0.12f);
            stock[1] -= spoil;
            spoiledTotal += spoil;
            // Gider: kira, toplayıcılar, raf/soğutma
            long cost = DailyCost;
            EconomyManager.I.Spend(SizeRent[size], $"{StoreName} kira ve elektrik", ExpenseCategory.Kira);
            if (SizePickers[size] > 0) EconomyManager.I.Spend(SizePickers[size] * PickerDaily, $"{StoreName} toplayıcı ({SizePickers[size]} kişi)", ExpenseCategory.Maas);
            long shelves = cost - SizeRent[size] - SizePickers[size] * PickerDaily;
            if (shelves > 0) EconomyManager.I.Spend(shelves, $"{StoreName} geniş çeşit (raf, soğutma)", ExpenseCategory.Diger);

            if (ordersToday > 0 || lostToday > 0)
                EventManager.I.Notify($"{StoreName} dün: {ordersToday} sipariş, satış {GameData.Ft(salesToday)}, brüt kâr {GameData.Ft(profitToday)}, " +
                                      $"gider {GameData.Ft(cost)}" + (spoil > 0 ? $", bozulan taze ürün {GameData.Ft(spoil)}" : "") +
                                      (lostToday > 0 ? $", stok yüzünden kaçan {lostToday}" : "") + ".");
            ordersToday = lostToday = 0;
            salesToday = profitToday = 0;
            lowWarnedToday = false;

            if (autoRestock) RestockAll(restockTarget, true);
            UpdateWeight();
        }

        // ---------------------------------------------------------------- kayıt

        public DarkStoreSave CaptureSave()
        {
            var d = new DarkStoreSave
            {
                open = IsOpen,
                tileX = shop != null ? shop.tile.x : 0,
                tileY = shop != null ? shop.tile.y : 0,
                size = size,
                stock = (long[])stock.Clone(),
                assortment = (int[])assortment.Clone(),
                autoRestock = autoRestock,
                restockTarget = restockTarget,
                openedDay = openedDay,
                salesTotal = salesTotal, profitTotal = profitTotal, spoiledTotal = spoiledTotal,
                ordersTotal = ordersTotal, lostTotal = lostTotal,
                salesToday = salesToday, profitToday = profitToday, ordersToday = ordersToday, lostToday = lostToday
            };
            return d;
        }

        public void RestoreSave(DarkStoreSave d)
        {
            if (d == null || !d.open) return;
            var s = CityMap.I.AddOwnShop(MakeDef(), new Vector2Int(d.tileX, d.tileY));
            if (s == null) return;
            shop = s;
            size = Mathf.Clamp(d.size, 1, 3);
            if (d.stock != null) for (int a = 0; a < Aisles && a < d.stock.Length; a++) stock[a] = d.stock[a];
            if (d.assortment != null) for (int a = 0; a < Aisles && a < d.assortment.Length; a++) assortment[a] = Mathf.Clamp(d.assortment[a], 1, 3);
            autoRestock = d.autoRestock;
            restockTarget = d.restockTarget > 0f ? d.restockTarget : 0.8f;
            openedDay = d.openedDay;
            salesTotal = d.salesTotal; profitTotal = d.profitTotal; spoiledTotal = d.spoiledTotal;
            ordersTotal = d.ordersTotal; lostTotal = d.lostTotal;
            salesToday = d.salesToday; profitToday = d.profitToday; ordersToday = d.ordersToday; lostToday = d.lostToday;
            UpdateWeight();
            if (City3D.I != null) City3D.I.Rebuild();
        }
    }
}
