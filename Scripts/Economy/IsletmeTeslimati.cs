using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    [System.Serializable]
    public class IsletmeSave
    {
        public List<int> x = new List<int>(), y = new List<int>();
        public List<float> memnuniyet = new List<float>();
        public List<int> kusBitis = new List<int>();
        public int toplamTeslim, toplamKacan, toplamGec;
        public long toplamGelir;
    }

    /// <summary>
    /// İşletmelere teslimat (geliştirme: UpgradeManager.Isletme; Wolt Drive, Uber Direct gibi). Yeni bir iş kolu: restoran olmayan
    /// işletmeler (eczane, çiçekçi, kitapçı, butik...) kendi müşterilerine gidecek paketleri senin kuryelerinle gönderir.
    ///  • İş ortakları haritada açık bölgelerde boş bir dükkan yerine yerleşir (seviyeye göre 3 / 6 / 10 ortak).
    ///  • Her ortağın günde birkaç paketi olur (eczane en çok); 09:00–20:00 arası, öğle (12–14) ve akşam (18–20) yemek
    ///    yoğunluğunda az, 10–11 ve 14–17 arasında çok. Cumartesi %60, pazar %30.
    ///  • Komisyon ve sepet yok: işletme paket başına 1 300 Ft + km başı 280 Ft öder; kurye normal tarifesini alır.
    ///    Paket 90 dk içinde teslim edilmeli. Atamada kargo gibi en sona kalır (yemek siparişinin önüne geçmez), süresine
    ///    25 dk kalınca normal sıraya alınır. Müşteri vazgeçmez, itibar etkilenmez.
    ///  • Her ortağın memnuniyeti var: zamanında teslim +1,5, geç −3, teslim edilemeyen −10. 30'un altına düşerse ortak
    ///    7 gün paket göndermez, sonra 50 ile döner.
    /// Gelir siparişin teslimat ücreti olarak (EconomyManager.RegisterDelivery) yazılır.
    /// </summary>
    public class IsletmeTeslimati : MonoBehaviour
    {
        public static IsletmeTeslimati I { get; private set; }

        public const float TeslimSuresi = 90f, AcilDk = 25f;
        public const float AcilisSaat = 9f, KapanisSaat = 20f;

        public static int OrtakSayisi(int lv) => new[] { 0, 3, 6, 10 }[Mathf.Clamp(lv, 0, 3)];
        public static int Ucret(int mesafeKare) => 1300 + Mathf.RoundToInt(280f * GameData.Km(mesafeKare));

        class OrtakTuru
        {
            public string ad, urun, not;
            public Color renk;
            public float gunluk, kgMin, kgMax;
        }

        static readonly OrtakTuru[] Turler =
        {
            new OrtakTuru { ad = "Patika Eczanesi", urun = "Eczane paketi (reçetesiz ilaç, bakım ürünü)", not = "Eczane: kapıda teslim, kimlik sorulmaz.", renk = new Color(0.3f, 0.75f, 0.5f), gunluk = 9f, kgMin = 0.2f, kgMax = 2f },
            new OrtakTuru { ad = "Rózsa Çiçekçi", urun = "Çiçek buketi", not = "Çiçek: dik taşıyın, ezilmesin. Kart içinde.", renk = new Color(0.95f, 0.45f, 0.6f), gunluk = 6f, kgMin = 0.5f, kgMax = 3f },
            new OrtakTuru { ad = "Lapozó Kitabevi", urun = "Kitap paketi", not = "Kitap: yağmurda ıslanmasın.", renk = new Color(0.55f, 0.45f, 0.8f), gunluk = 4f, kgMin = 0.4f, kgMax = 3f },
            new OrtakTuru { ad = "Tech Sarok", urun = "Elektronik aksesuar", not = "Elektronik: kapıda imza.", renk = new Color(0.35f, 0.55f, 0.85f), gunluk = 5f, kgMin = 0.2f, kgMax = 4f },
            new OrtakTuru { ad = "Divat Butik", urun = "Giyim paketi", not = "Butik: denemek için 10 dk bekleyebilirsiniz.", renk = new Color(0.85f, 0.6f, 0.35f), gunluk = 4f, kgMin = 0.3f, kgMax = 2.5f },
            new OrtakTuru { ad = "Mancs Pet Shop", urun = "Evcil hayvan maması", not = "Pet shop: ağır olabilir.", renk = new Color(0.7f, 0.55f, 0.35f), gunluk = 5f, kgMin = 1f, kgMax = 12f },
            new OrtakTuru { ad = "Tiszta Kuru Temizleme", urun = "Temizlenmiş kıyafet (askıda)", not = "Kuru temizleme: askıyı katlamayın.", renk = new Color(0.5f, 0.75f, 0.85f), gunluk = 4f, kgMin = 0.5f, kgMax = 3f },
            new OrtakTuru { ad = "Optika Plusz", urun = "Gözlük ve lens", not = "Optik: kırılacak eşya.", renk = new Color(0.6f, 0.65f, 0.7f), gunluk = 3f, kgMin = 0.1f, kgMax = 0.6f },
            new OrtakTuru { ad = "Sport Pont", urun = "Spor malzemesi", not = "Spor mağazası: büyük kutu olabilir.", renk = new Color(0.9f, 0.35f, 0.3f), gunluk = 4f, kgMin = 0.5f, kgMax = 9f },
            new OrtakTuru { ad = "Papír Kırtasiye", urun = "Kırtasiye ve ofis malzemesi", not = "Kırtasiye: ofise teslim, resepsiyona bırakılabilir.", renk = new Color(0.8f, 0.75f, 0.35f), gunluk = 5f, kgMin = 0.3f, kgMax = 6f },
        };

        static readonly float[] SaatAgirligi =
        {   // 09 10   11   12   13   14   15   16   17   18   19
            1.0f, 1.4f, 1.2f, 0.6f, 0.6f, 1.2f, 1.4f, 1.3f, 0.9f, 0.6f, 0.5f
        };
        static float AgirlikToplami
        {
            get { float t = 0f; foreach (var w in SaatAgirligi) t += w; return t; }
        }

        public static int GunlukTahmin(int lv)
        {
            float t = 0f;
            for (int i = 0; i < OrtakSayisi(lv) && i < Turler.Length; i++) t += Turler[i].gunluk;
            return Mathf.RoundToInt(t);
        }

        public class Ortak
        {
            public Shop shop;
            public int tur;
            public float memnuniyet = 70f;
            public int kusBitis;     // bu güne kadar paket göndermiyor
            public float birikim;    // sıradaki paketin birikimi (0–1)
            public int bugunTeslim, bugunGec, bugunKacan;
            public string Ad => Turler[tur].ad;
            public bool Kusgun => GameClock.I != null && GameClock.I.Day < kusBitis;
        }

        public readonly List<Ortak> Ortaklar = new List<Ortak>();
        readonly List<Vector2Int> kayitliYerler = new List<Vector2Int>();
        readonly List<float> kayitliMemnuniyet = new List<float>();
        readonly List<int> kayitliKus = new List<int>();
        int sonKurmaDenemesi = -1;
        float sonDakika = -1f;

        public int bugunTeslim, bugunKacan, bugunGec, dunTeslim, dunKacan, dunGec;
        public long bugunGelir, dunGelir;
        public int toplamTeslim, toplamKacan, toplamGec;
        public long toplamGelir;

        public int Seviye => UpgradeManager.I != null ? UpgradeManager.I.Level(UpgradeKind.Isletme) : 0;

        void Awake() { I = this; }

        public void Init()
        {
            Ortaklar.Clear(); kayitliYerler.Clear(); kayitliMemnuniyet.Clear(); kayitliKus.Clear();
            bugunTeslim = bugunKacan = bugunGec = dunTeslim = dunKacan = dunGec = 0;
            bugunGelir = dunGelir = 0; toplamTeslim = toplamKacan = toplamGec = 0; toplamGelir = 0;
            sonKurmaDenemesi = -1; sonDakika = -1f;
            GameClock.I.OnNewDay -= YeniGun;
            GameClock.I.OnNewDay += YeniGun;
        }

        void YeniGun()
        {
            if (Seviye > 0 && (bugunTeslim > 0 || bugunKacan > 0))
            {
                string m = $"İşletme teslimatı dünkü özet: {bugunTeslim} paket teslim edildi" + (bugunGec > 0 ? $" ({bugunGec} geç)" : "") +
                           $", gelir {GameData.Ft(bugunGelir)}" + (bugunKacan > 0 ? $", {bugunKacan} paket teslim edilemedi." : ".");
                if (bugunKacan > 0) EventManager.I.Notify(m); else EventManager.I.NotifyQuiet(m);
            }
            dunTeslim = bugunTeslim; dunKacan = bugunKacan; dunGec = bugunGec; dunGelir = bugunGelir;
            bugunTeslim = bugunKacan = bugunGec = 0; bugunGelir = 0;
            foreach (var o in Ortaklar)
            {
                o.bugunTeslim = o.bugunGec = o.bugunKacan = 0;
                if (!o.Kusgun && o.kusBitis > 0 && GameClock.I.Day == o.kusBitis)
                {
                    o.memnuniyet = 50f;
                    EventManager.I.Notify($"{o.Ad} yeniden paket göndermeye başladı (memnuniyet 50/100).");
                }
            }
        }

        void Update()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || GameClock.I == null || CityMap.I == null || OrderManager.I == null) return;
            var clk = GameClock.I;
            if (Seviye <= 0) { sonDakika = clk.TotalMinutes; return; }
            if (Ortaklar.Count < OrtakSayisi(Seviye) && sonKurmaDenemesi != clk.Day) { sonKurmaDenemesi = clk.Day; OrtaklariKur(); }

            float now = clk.TotalMinutes;
            if (sonDakika < 0f || now < sonDakika) { sonDakika = now; return; }
            float dt = Mathf.Min(now - sonDakika, 30f);   // yükleme ya da uzun kare: birikimi şişirme
            sonDakika = now;
            float saat = (now % 1440f) / 60f;
            if (saat < AcilisSaat || saat >= KapanisSaat || dt <= 0f) return;
            int dow = clk.DayOfWeek;
            float gun = dow == 6 ? 0.3f : dow == 5 ? 0.6f : 1f;
            float agirlik = SaatAgirligi[Mathf.Clamp((int)(saat - AcilisSaat), 0, SaatAgirligi.Length - 1)] / AgirlikToplami;
            int sinir = 3;   // bir karede en fazla birkaç paket
            foreach (var o in Ortaklar)
            {
                if (o.Kusgun || o.shop == null) continue;
                // Günlük paket × saat ağırlığı (saat başına) × dt/60, biraz rastgelelik
                o.birikim += Turler[o.tur].gunluk * gun * agirlik * dt / 60f * Random.Range(0.7f, 1.3f);
                while (o.birikim >= 1f && sinir-- > 0)
                {
                    o.birikim -= 1f;
                    PaketCikar(o);
                }
            }
        }

        void OrtaklariKur()
        {
            var map = CityMap.I;
            while (Ortaklar.Count < OrtakSayisi(Seviye) && Ortaklar.Count < Turler.Length)
            {
                int i = Ortaklar.Count;
                Vector2Int? yer = i < kayitliYerler.Count ? kayitliYerler[i] : map.RastgeleBosDukkanYeri();
                if (!yer.HasValue) return;
                var t = Turler[i];
                var def = new ShopDef
                {
                    name = t.ad,
                    color = t.renk,
                    prepMinutes = 4,
                    basketMin = 0, basketMax = 0,
                    commission = 0f,
                    weight = 0f,              // rastgele yemek siparişi üretmez: paketleri bu sınıf çıkarır
                    contractCost = 0,
                    unlockLevel = 1,
                    hotFood = false,
                    isMarket = false,
                    kgMin = t.kgMin, kgMax = t.kgMax
                };
                var shop = map.AddOwnShop(def, yer);
                if (shop == null) return;
                shop.kargo = true;
                shop.isletme = true;
                var o = new Ortak { shop = shop, tur = i };
                if (i < kayitliMemnuniyet.Count) o.memnuniyet = kayitliMemnuniyet[i];
                if (i < kayitliKus.Count) o.kusBitis = kayitliKus[i];
                Ortaklar.Add(o);
                if (i >= kayitliYerler.Count)
                {
                    kayitliYerler.Add(shop.tile);
                    EventManager.I.Notify($"Yeni iş ortağı: {t.ad} ({CityMap.DistrictOf(shop.tile)}). Günde ~{t.gunluk:0} paketini senin kuryelerin götürecek, " +
                                          $"paket başı {GameData.Ft(Ucret(15))} (3 km), 90 dk içinde.");
                }
            }
        }

        void PaketCikar(Ortak o)
        {
            var map = CityMap.I;
            var t = Turler[o.tur];
            var hedef = Random.value < 0.75f ? map.RandomCustomerTileNear(o.shop.tile, 22) : map.RandomCustomerTile(o.shop.tile);
            float kg = Mathf.Round(Random.Range(t.kgMin, t.kgMax) * 10f) / 10f;
            if (!EconomyManager.AnyoneCanCarry(kg)) kg = Mathf.Min(kg, 5f);
            OrderManager.I.IsletmeSiparisi(o.shop, hedef, kg, t.urun, t.not);
        }

        Ortak OrtakOf(Order o)
        {
            foreach (var x in Ortaklar) if (x.shop == o.shop) return x;
            return null;
        }

        public void Teslim(Order o, float gec)
        {
            bugunTeslim++; toplamTeslim++;
            bugunGelir += o.deliveryFee; toplamGelir += o.deliveryFee;
            var x = OrtakOf(o);
            if (gec > 0f) { bugunGec++; toplamGec++; }
            if (x != null)
            {
                x.bugunTeslim++;
                if (gec > 0f) x.bugunGec++;
                Memnuniyet(x, gec > 0f ? -3f : 1.5f);
            }
            Telemetry.Add(gec > 0f ? "İşletme teslimatı: geç" : "İşletme teslimatı: zamanında");
        }

        public void Kacti(Order o)
        {
            bugunKacan++; toplamKacan++;
            var x = OrtakOf(o);
            if (x != null) { x.bugunKacan++; Memnuniyet(x, -10f); }
            Telemetry.Add("İşletme teslimatı: teslim edilemedi");
        }

        void Memnuniyet(Ortak x, float d)
        {
            x.memnuniyet = Mathf.Clamp(x.memnuniyet + d, 0f, 100f);
            if (x.memnuniyet < 30f && !x.Kusgun)
            {
                x.kusBitis = GameClock.I.Day + 7;
                EventManager.I.Push(new GameEvent($"{x.Ad} küstü",
                    $"{x.Ad} paketlerinin geç ya da hiç gitmemesinden bıktı: 7 gün paket göndermeyecek ({GameClock.I.DateOf(x.kusBitis)} tarihine kadar). " +
                    "Sonra memnuniyet 50 ile yeniden başlar. Sakin saatlerde boşta kurye bırakmak ya da daha çok kurye almak işletme paketlerini hızlandırır."));
            }
        }

        public Ortak OrtakBul(Shop s)
        {
            foreach (var x in Ortaklar) if (x.shop == s) return x;
            return null;
        }

        public IsletmeSave Kaydet()
        {
            var s = new IsletmeSave { toplamTeslim = toplamTeslim, toplamKacan = toplamKacan, toplamGec = toplamGec, toplamGelir = toplamGelir };
            for (int i = 0; i < kayitliYerler.Count; i++)
            {
                s.x.Add(kayitliYerler[i].x); s.y.Add(kayitliYerler[i].y);
                var o = i < Ortaklar.Count ? Ortaklar[i] : null;
                s.memnuniyet.Add(o != null ? o.memnuniyet : (i < kayitliMemnuniyet.Count ? kayitliMemnuniyet[i] : 70f));
                s.kusBitis.Add(o != null ? o.kusBitis : (i < kayitliKus.Count ? kayitliKus[i] : 0));
            }
            return s;
        }

        public void Yukle(IsletmeSave s)
        {
            Ortaklar.Clear(); kayitliYerler.Clear(); kayitliMemnuniyet.Clear(); kayitliKus.Clear();
            if (s == null) return;
            toplamTeslim = s.toplamTeslim; toplamKacan = s.toplamKacan; toplamGec = s.toplamGec; toplamGelir = s.toplamGelir;
            for (int i = 0; i < s.x.Count && i < s.y.Count; i++)
            {
                kayitliYerler.Add(new Vector2Int(s.x[i], s.y[i]));
                kayitliMemnuniyet.Add(i < s.memnuniyet.Count ? s.memnuniyet[i] : 70f);
                kayitliKus.Add(i < s.kusBitis.Count ? s.kusBitis[i] : 0);
            }
        }
    }
}
