using System;
using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    [Serializable]
    public class SanalMutfakSave
    {
        public bool acik;
        public int x = -1, y = -1, acilisGunu;
        public List<bool> markaAcik = new List<bool>();
        public List<int> menu = new List<int>();
        public List<float> populerlik = new List<float>();
        public List<int> siparis = new List<int>();
        public long toplamSatis, toplamMalzeme, toplamGider;
        public int toplamSiparis;
    }

    /// <summary>
    /// Sanal mutfak (dark kitchen, Wolt'un ve Deliveroo Editions'ın kendi mutfakları gibi): sadece uygulamada satılan kendi yemek
    /// markaların, tek bir mutfakta pişer. Dark store'un yemek versiyonu.
    ///  • Kurulum 1 600 000 Ft (Doğu İlçesi açıkken, şirketin 10. gününden sonra); ilk marka (Kurye Burger) dahil.
    ///    Diğer markalar 500 000 Ft: Bowl &amp; Co (sağlıklı kase), Nonna Pizza, Tatlı Kutusu.
    ///  • Komisyon yok: sepetin tamamı senin. Malzeme sepetin %32'si (teslimde gider). Kira + enerji günde 12 000 Ft,
    ///    marka başı aşçı günde 16 000 Ft.
    ///  • Menü geliştirme (2. seviye 300 000, 3. seviye 900 000 Ft): talep ×1,25 / ×1,5, müşteri puanı +0,1 / +0,2.
    ///  • Popülerlik (0,3–1,5): yeni marka 0,6 ile başlar; her gün ağızdan ağıza biraz artar (1,0'a kadar), iyi puan (4,3 üstü)
    ///    yükseltir, kötü puan düşürür. Talep = 0,55 × menü × popülerlik.
    ///  • Açık 10:00–23:00 (hafta sonu 24:00). Haritada mutfağın kartında bütün markalar görünür.
    /// </summary>
    public class SanalMutfak : MonoBehaviour
    {
        public static SanalMutfak I { get; private set; }

        public const long KurulumBedeli = 1600000, MarkaBedeli = 500000;
        public static readonly long[] MenuBedeli = { 0, 0, 300000, 900000 };   // seviye 2 ve 3'e çıkmak
        public const long GunlukKira = 12000, AsciGunluk = 16000;
        public const float GidaOrani = 0.32f;
        public const int GerekliBolge = 2, EnErkenGun = 10;
        static readonly float[] MenuTalep = { 0f, 1f, 1.25f, 1.5f };

        public class MarkaTanim
        {
            public string ad, tur;
            public Color renk;
            public bool sicak;
            public int sepetMin, sepetMax;
            public string[] satirlar;      // kendi menüsü (yoksa kaynak dükkandan)
            public string kaynak; public float fiyat = 1f;
        }

        public static readonly MarkaTanim[] Markalar =
        {
            new MarkaTanim { ad = "Kurye Burger", tur = "burger", renk = new Color(0.95f, 0.55f, 0.2f), sicak = true, sepetMin = 3000, sepetMax = 7500, kaynak = "Burger Queen", fiyat = 0.97f },
            new MarkaTanim { ad = "Bowl & Co", tur = "sağlıklı kase", renk = new Color(0.45f, 0.75f, 0.35f), sicak = true, sepetMin = 3200, sepetMax = 7000, satirlar = new[]
            {
                "Tavuklu kinoa kase|3290|A", "Somon poke kase|3990|A", "Falafel humus kase|2990|A", "Teriyaki tofu kase|3190|A", "Biftekli pirinç kase|3790|A",
                "Akdeniz salata kase|2790|A", "Ton balıklı poke|3890|A", "Vegan buddha kase|3090|A",
                "Edamame|990|Y", "Humus ve pide|1190|Y", "Tatlı patates kızartması|1090|Y", "Miso çorba|890|Y",
                "Limonata (ev yapımı)|790|I", "Yeşil smoothie|1290|I", "Kombucha|990|I", "Maden suyu|490|I",
                "Chia puding|990|T", "Meyve kasesi|1190|T"
            }},
            new MarkaTanim { ad = "Nonna Pizza", tur = "pizza", renk = new Color(0.85f, 0.25f, 0.25f), sicak = true, sepetMin = 3500, sepetMax = 9000, kaynak = "Pizza Hat", fiyat = 0.95f },
            new MarkaTanim { ad = "Tatlı Kutusu", tur = "tatlı ve kahve", renk = new Color(0.9f, 0.55f, 0.75f), sicak = false, sepetMin = 2200, sepetMax = 5500, satirlar = new[]
            {
                "Latte|890|K", "Cappuccino|850|K", "Flat white|990|K", "Buzlu karamel latte|1190|K", "Sıcak çikolata|990|K", "Ev yapımı limonata|790|K",
                "Kürtőskalács (tarçınlı)|1290|T", "Dobos torta dilimi|1390|T", "Somlói galuska|1490|T", "San Sebastian cheesecake|1590|T",
                "Tiramisu|1390|T", "Brownie|890|T", "Lotus cheesecake|1490|T", "Baklava (4 dilim)|1690|T",
                "Kurabiye (3)|690|Y", "Kruvasan|590|Y", "Çikolatalı muffin|690|Y", "Makaron (6)|1490|Y"
            }},
        };

        public class MarkaDurum
        {
            public int no;
            public Shop shop;
            public int menu = 1;
            public float populerlik = 0.6f;
            public int siparis, bugunSiparis;
            public long bugunSatis;
            public MarkaTanim T => Markalar[no];
        }

        public readonly List<MarkaDurum> Acik = new List<MarkaDurum>();
        public bool IsOpen => Acik.Count > 0;
        Vector2Int? yer;
        public int acilisGunu;
        public long bugunSatis, bugunMalzeme, dunSatis, dunMalzeme, dunGider;
        public int bugunSiparis, dunSiparis;
        public long toplamSatis, toplamMalzeme, toplamGider;
        public int toplamSiparis;
        // kayıttan gelen ve henüz kurulmamış markalar
        SanalMutfakSave bekleyen;

        void Awake() { I = this; }

        public void Init()
        {
            Acik.Clear();
            yer = null; bekleyen = null;
            bugunSatis = bugunMalzeme = dunSatis = dunMalzeme = dunGider = 0;
            bugunSiparis = dunSiparis = 0;
            toplamSatis = toplamMalzeme = toplamGider = 0; toplamSiparis = 0;
            GameClock.I.OnNewDay -= YeniGun;
            GameClock.I.OnNewDay += YeniGun;
        }

        // ------------------------------------------------------------------ kurallar

        public long GunlukGider => IsOpen ? GunlukKira + AsciGunluk * Acik.Count : 0;

        public string KilitNedeni
        {
            get
            {
                if (CityMap.I == null || GameClock.I == null) return "";
                if (CityMap.I.UnlockLevel < GerekliBolge) return $"önce \"{CityMap.DistrictNames[GerekliBolge]}\" bölgesini aç";
                if (GameClock.I.CompanyDay < EnErkenGun) return $"şirketin {EnErkenGun}. gününden sonra";
                return "";
            }
        }

        public MarkaDurum MarkaOf(Shop s)
        {
            foreach (var m in Acik) if (m.shop == s) return m;
            return null;
        }

        /// <summary>Teslimde müşteri puanına eklenen (geliştirilmiş menü).</summary>
        public static float PuanEki(Shop s)
        {
            var m = I != null ? I.MarkaOf(s) : null;
            return m != null ? 0.1f * (m.menu - 1) : 0f;
        }

        public int SiradakiMarka => Acik.Count < Markalar.Length ? Acik.Count : -1;

        // ------------------------------------------------------------------ eylemler

        public bool Kur()
        {
            if (IsOpen || KilitNedeni.Length > 0) return false;
            if (!EconomyManager.I.TrySpendYatirim(KurulumBedeli, "Sanal mutfak kurulumu (mutfak, ekipman, ilk marka)", ExpenseCategory.Gelistirme)) return false;
            acilisGunu = GameClock.I.Day;
            if (!MarkaKur(0, null, 1, 0.6f, 0)) return false;
            Telemetry.Add("Sanal mutfak: kuruldu");
            EventManager.I.Notify($"Sanal mutfak açıldı ({CityMap.DistrictOf(Acik[0].shop.tile)}): ilk markan {Markalar[0].ad} uygulamada. " +
                                  "Komisyon yok, malzeme sepetin %32'si; yeni marka ve menü geliştirme İşletme > Mutfak'ta.");
            return true;
        }

        public bool MarkaAc()
        {
            int no = SiradakiMarka;
            if (!IsOpen || no < 0) return false;
            if (!EconomyManager.I.TrySpendYatirim(MarkaBedeli, $"Sanal mutfak: yeni marka ({Markalar[no].ad})", ExpenseCategory.Gelistirme)) return false;
            if (!MarkaKur(no, Acik[0].shop.tile, 1, 0.6f, 0)) return false;
            Telemetry.Add("Sanal mutfak: yeni marka");
            EventManager.I.Notify($"Yeni sanal marka: {Markalar[no].ad} ({Markalar[no].tur}). Aynı mutfakta pişer; günde {GameData.Ft(AsciGunluk)} aşçı gideri eklendi.");
            return true;
        }

        public long MenuBedel(MarkaDurum m) => m.menu >= 3 ? 0 : MenuBedeli[m.menu + 1];

        public bool MenuGelistir(MarkaDurum m)
        {
            if (m == null || m.menu >= 3) return false;
            if (!EconomyManager.I.TrySpendYatirim(MenuBedel(m), $"Sanal mutfak: {m.T.ad} menü geliştirme", ExpenseCategory.Gelistirme)) return false;
            m.menu++;
            AgirlikGuncelle(m);
            Telemetry.Add("Sanal mutfak: menü geliştirildi");
            EventManager.I.Notify($"{m.T.ad} menüsü {m.menu}. seviyede: talep ×{MenuTalep[m.menu]:0.00}, müşteri puanı +{0.1f * (m.menu - 1):0.0}.");
            return true;
        }

        bool MarkaKur(int no, Vector2Int? konum, int menu, float pop, int siparis)
        {
            var t = Markalar[no];
            if (t.satirlar != null) Menus.RegisterRows(t.ad, t.satirlar, t.fiyat);
            else Menus.Register(t.ad, t.kaynak, t.fiyat);
            ShopHours.Register(t.ad, 10f, 23f, 10f, 24f);
            var def = new ShopDef
            {
                name = t.ad, color = t.renk, prepMinutes = 12, basketMin = t.sepetMin, basketMax = t.sepetMax,
                commission = 0f, weight = 0f, contractCost = 0, unlockLevel = 1, hotFood = t.sicak, isMarket = false,
                kgMin = 0.4f, kgMax = 2.5f
            };
            var s = CityMap.I.AddOwnShop(def, konum ?? yer);
            if (s == null) return false;
            s.mutfak = true;
            yer = s.tile;
            var m = new MarkaDurum { no = no, shop = s, menu = menu, populerlik = pop, siparis = siparis };
            Acik.Add(m);
            AgirlikGuncelle(m);
            return true;
        }

        void AgirlikGuncelle(MarkaDurum m) => m.shop.def.weight = 0.55f * MenuTalep[Mathf.Clamp(m.menu, 1, 3)] * m.populerlik;

        // ------------------------------------------------------------------ akış

        void Update()
        {
            if (bekleyen == null || GameBootstrap.I == null || !GameBootstrap.I.Started || CityMap.I == null) return;
            // Kayıttan: markaları aynı yere yeniden kur
            var b = bekleyen;
            bekleyen = null;
            if (!b.acik || b.x < 0) return;
            yer = new Vector2Int(b.x, b.y);
            for (int i = 0; i < Markalar.Length && i < b.markaAcik.Count; i++)
            {
                if (!b.markaAcik[i]) continue;
                MarkaKur(i, yer, i < b.menu.Count ? b.menu[i] : 1, i < b.populerlik.Count ? b.populerlik[i] : 0.6f, i < b.siparis.Count ? b.siparis[i] : 0);
            }
        }

        void YeniGun()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || !IsOpen) return;
            long gider = GunlukGider;
            EconomyManager.I.Spend(gider, $"Sanal mutfak: kira, enerji ve {Acik.Count} aşçı", ExpenseCategory.Kira);
            toplamGider += gider;
            if (bugunSiparis > 0)
                EventManager.I.NotifyQuiet($"Sanal mutfak dünkü özet: {bugunSiparis} sipariş, satış {GameData.Ft(bugunSatis)}, malzeme {GameData.Ft(bugunMalzeme)}, " +
                                           $"gider {GameData.Ft(gider)} → {GameData.Ft(bugunSatis - bugunMalzeme - gider)}.");
            dunSatis = bugunSatis; dunMalzeme = bugunMalzeme; dunSiparis = bugunSiparis; dunGider = gider;
            bugunSatis = bugunMalzeme = 0; bugunSiparis = 0;
            foreach (var m in Acik)
            {
                if (m.populerlik < 1f) m.populerlik = Mathf.Min(1f, m.populerlik + 0.015f);   // ağızdan ağıza
                m.bugunSiparis = 0; m.bugunSatis = 0;
                AgirlikGuncelle(m);
            }
        }

        public void Teslim(Order o)
        {
            var m = MarkaOf(o.shop);
            long malzeme = o.stockCost;
            if (malzeme > 0) EconomyManager.I.Spend(malzeme, $"Sanal mutfak: malzeme ({o.shop.Name})", ExpenseCategory.Stok);
            bugunSatis += o.basket; bugunMalzeme += malzeme; bugunSiparis++;
            toplamSatis += o.basket; toplamMalzeme += malzeme; toplamSiparis++;
            if (m == null) return;
            m.siparis++; m.bugunSiparis++; m.bugunSatis += o.basket;
            m.populerlik = Mathf.Clamp(m.populerlik + (o.rating - 4.3f) * 0.01f, 0.3f, 1.5f);
            AgirlikGuncelle(m);
        }

        // ------------------------------------------------------------------ kayıt

        public SanalMutfakSave Kaydet()
        {
            var s = new SanalMutfakSave
            {
                acik = IsOpen, x = yer.HasValue ? yer.Value.x : -1, y = yer.HasValue ? yer.Value.y : -1, acilisGunu = acilisGunu,
                toplamSatis = toplamSatis, toplamMalzeme = toplamMalzeme, toplamGider = toplamGider, toplamSiparis = toplamSiparis
            };
            if (bekleyen != null) return bekleyen;   // yüklendi ama henüz kurulmadı
            for (int i = 0; i < Markalar.Length; i++)
            {
                var m = Acik.Find(x => x.no == i);
                s.markaAcik.Add(m != null);
                s.menu.Add(m != null ? m.menu : 1);
                s.populerlik.Add(m != null ? m.populerlik : 0.6f);
                s.siparis.Add(m != null ? m.siparis : 0);
            }
            return s;
        }

        public void Yukle(SanalMutfakSave s)
        {
            if (s == null) return;
            acilisGunu = s.acilisGunu;
            toplamSatis = s.toplamSatis; toplamMalzeme = s.toplamMalzeme; toplamGider = s.toplamGider; toplamSiparis = s.toplamSiparis;
            bekleyen = s;
        }
    }
}
