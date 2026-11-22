using UnityEngine;

namespace KuryeTycoon
{
    [System.Serializable]
    public class KargoSave
    {
        public int depoX = -1, depoY = -1;
        public int toplamTeslim, toplamKacan;
        public long toplamGelir, toplamCeza;
    }

    /// <summary>
    /// Kargo teslimatı (geliştirme: UpgradeManager.Kargo). Yeni bir iş kolu: bir kargo şirketinin şehir içi paket dağıtımı.
    ///  • Depo şehirde (merkeze yakın, depo/karma bölgede) açılır. Her iş günü sabah paketler gelir: 09:00–16:00 arasında
    ///    eşit aralıklarla dağıtıma çıkar, akşam 19:00'a kadar teslim edilmeli. Cumartesi yarısı, pazar yok.
    ///  • Paketlerin acelesi yok: atamada en sona kalır (boşta kurye varken ya da bekleyen yemek/market siparişi yokken atanır),
    ///    müşteri vazgeçmez. Böylece kuryeler sakin saatlerde boş beklemek yerine para kazanır.
    ///  • Kargo şirketi paket başına öder (1 100 Ft + km başı 330 Ft); kurye normal tarifesini alır. 19:00'a kadar teslim edilemeyen
    ///    her paket için 600 Ft ceza.
    ///  • Paketlerin %15'i ağırdır (8–18 kg): arabalı kurye gerekir (yoksa hafif paket gelir).
    /// </summary>
    public class KargoAnlasmasi : MonoBehaviour
    {
        public static KargoAnlasmasi I { get; private set; }

        public const string DepoAdi = "Kargo deposu";
        public const float DagitimBas = 9f * 60f, DagitimBit = 16f * 60f, SonTeslim = 19f * 60f;
        public const int Ceza = 600;

        public static int GunlukPaket(int lv) => new[] { 0, 30, 70, 140 }[Mathf.Clamp(lv, 0, 3)];
        public static int Ucret(int mesafeKare) => 1100 + Mathf.RoundToInt(330f * GameData.Km(mesafeKare));

        [System.NonSerialized] public Shop depo;   // haritadaki depo (kayda KargoSave ile yazılır)
        ShopDef def;
        Vector2Int? kayitliKonum;

        int planGunu = -1, bugunPlan, bugunCikan, sonDepoDenemesi = -1;
        public int bugunTeslim, bugunKacan, dunTeslim, dunKacan, dunPlan;
        public long bugunGelir, bugunCeza, dunGelir, dunCeza;
        public int toplamTeslim, toplamKacan;
        public long toplamGelir, toplamCeza;

        public int Seviye => UpgradeManager.I != null ? UpgradeManager.I.Level(UpgradeKind.Kargo) : 0;
        public int BugunPlan => bugunPlan;
        public int BugunCikan => bugunCikan;

        void Awake() { I = this; }

        public void Init()
        {
            GameClock.I.OnNewDay += YeniGun;
        }

        void YeniGun()
        {
            if (Seviye > 0 && (dunPlan > 0 || bugunPlan > 0))
            {
                string m = $"Kargo dünkü özet: {bugunTeslim}/{bugunCikan} paket teslim edildi, gelir {GameData.Ft(bugunGelir)}" +
                           (bugunKacan > 0 ? $", {bugunKacan} paket dağıtılamadı (ceza {GameData.Ft(bugunCeza)})." : ".");
                if (bugunKacan > 0) EventManager.I.Notify(m); else EventManager.I.NotifyQuiet(m);
            }
            dunTeslim = bugunTeslim; dunKacan = bugunKacan; dunGelir = bugunGelir; dunCeza = bugunCeza; dunPlan = bugunPlan;
            bugunTeslim = bugunKacan = 0; bugunGelir = bugunCeza = 0;
            planGunu = -1;
        }

        void PlanYap()
        {
            var clk = GameClock.I;
            planGunu = clk.Day;
            bugunCikan = 0;
            int dow = clk.DayOfWeek;
            float gun = dow == 6 ? 0f : dow == 5 ? 0.5f : 1f;
            bugunPlan = Mathf.RoundToInt(GunlukPaket(Seviye) * gun * Random.Range(0.85f, 1.15f));
            // Gün ortasında açıldıysa (ya da kayıt yüklendiyse) geçen saatlerin paketleri gelmez
            float m = clk.TotalMinutes % 1440f;
            if (m > DagitimBas) bugunCikan = Mathf.RoundToInt(bugunPlan * Mathf.Clamp01((m - DagitimBas) / (DagitimBit - DagitimBas)));
        }

        void Update()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || GameClock.I == null || CityMap.I == null) return;
            if (Seviye <= 0) return;
            if (depo == null && sonDepoDenemesi != GameClock.I.Day) { sonDepoDenemesi = GameClock.I.Day; DepoKur(); }
            if (depo == null) return;
            var clk = GameClock.I;
            if (planGunu != clk.Day) PlanYap();
            float m = clk.TotalMinutes % 1440f;
            if (m < DagitimBas || bugunPlan <= 0) return;
            int hedef = Mathf.RoundToInt(bugunPlan * Mathf.Clamp01((m - DagitimBas) / (DagitimBit - DagitimBas)));
            int sinir = 4;   // bir karede en fazla birkaç paket (yüksek hızda yığılmasın)
            while (bugunCikan < hedef && sinir-- > 0) Cikar();
        }

        void DepoKur()
        {
            if (def == null)
                def = new ShopDef
                {
                    name = DepoAdi,
                    color = new Color(0.62f, 0.48f, 0.3f),
                    prepMinutes = 3,
                    basketMin = 0, basketMax = 0,
                    commission = 0f,
                    weight = 0f,              // rastgele sipariş üretmez: paketleri bu sınıf çıkarır
                    contractCost = 0,
                    unlockLevel = 1,
                    hotFood = false,
                    isMarket = false,
                    kgMin = 0.3f, kgMax = 18f
                };
            depo = CityMap.I.AddOwnShop(def, kayitliKonum);
            if (depo == null) return;
            depo.kargo = true;
            kayitliKonum = depo.tile;
            EventManager.I.Notify($"Kargo deposu açıldı ({CityMap.DistrictNames[Mathf.Clamp(CityMap.RegionOf(depo.tile), 0, 5)]}). " +
                                  $"İş günlerinde 09:00–16:00 arası günde ~{GunlukPaket(Seviye)} paket dağıtıma çıkar, 19:00'a kadar teslim edilmeli.");
        }

        void Cikar()
        {
            bugunCikan++;
            var map = CityMap.I;
            var hedef = Random.value < 0.7f ? map.RandomCustomerTileNear(depo.tile, 20) : map.RandomCustomerTile(depo.tile);
            float kg = Random.value < 0.15f ? Random.Range(8f, 18f) : Random.Range(0.3f, 6f);
            if (!EconomyManager.AnyoneCanCarry(kg)) kg = Random.Range(0.3f, 5f);
            kg = Mathf.Round(kg * 10f) / 10f;
            float sonAn = (GameClock.I.Day - 1) * 1440f + SonTeslim;
            if (OrderManager.I != null) OrderManager.I.KargoSiparisi(depo, hedef, kg, sonAn);
        }

        public void Teslim(Order o, float gec)
        {
            bugunTeslim++; toplamTeslim++;
            bugunGelir += o.deliveryFee; toplamGelir += o.deliveryFee;
            Telemetry.Add("Kargo: teslim");
        }

        public void Kacti(Order o)
        {
            bugunKacan++; toplamKacan++;
            bugunCeza += Ceza; toplamCeza += Ceza;
            EconomyManager.I.SpendPenalty(Ceza, $"Kargo: #{o.id} dağıtılamadı");
            Telemetry.Add("Kargo: dağıtılamadı (ceza)");
        }

        public KargoSave Kaydet() => new KargoSave
        {
            depoX = depo != null ? depo.tile.x : -1, depoY = depo != null ? depo.tile.y : -1,
            toplamTeslim = toplamTeslim, toplamKacan = toplamKacan, toplamGelir = toplamGelir, toplamCeza = toplamCeza
        };

        public void Yukle(KargoSave s)
        {
            if (s == null) return;
            kayitliKonum = s.depoX >= 0 ? new Vector2Int(s.depoX, s.depoY) : (Vector2Int?)null;
            toplamTeslim = s.toplamTeslim; toplamKacan = s.toplamKacan; toplamGelir = s.toplamGelir; toplamCeza = s.toplamCeza;
        }
    }
}
