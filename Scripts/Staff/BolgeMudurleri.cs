using System;
using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    [Serializable]
    public class BolgeMuduru
    {
        public int bolge;              // 1–5 (CityMap.DistrictNames)
        public string ad = "";
        public float yetenek;          // 0,5–1
        public int uzmanlik;           // 0 satış, 1 ilişki
        public int gun;                // görevde geçen gün (deneyim)
        public bool otoSozlesme = true;
        public int sonSozlesmeGunu = -999;
        public int yenilenen, imzalanan;   // tuttuğu ve yeni getirdiği dükkan sayısı
    }

    [Serializable]
    public class BolgeSave
    {
        public List<BolgeMuduru> mudurler = new List<BolgeMuduru>();
        public int sonGun = -1;
        public long odenen;
    }

    /// <summary>
    /// Bölge müdürleri (İşletme > Bölgeler). İkinci bölge açılınca her açık bölgeye bir müdür atanabilir (AdVenture Capitalist'teki
    /// yöneticiler gibi: şirket büyüdükçe ayrıntıyı devredersin). Müdür kendi bölgesindeki dükkanları yönetir:
    ///  • Sipariş: bölgedeki dükkanların talebi artar (deneyim ve yeteneğe göre +%2–10; satış uzmanı ×1,5).
    ///  • İlişki: her gün bölgedeki sözleşmeli dükkanların memnuniyeti biraz artar (ilişki uzmanı ×2).
    ///  • Sözleşme yenileme: rakip teklif verince kart açılmaz; müdür ya mevcut şartlarla tutar ya da teklife eşitler, dükkanı kaptırmaz.
    ///  • Yeni sözleşme (açıksa): kasada bedelin 3 katı varsa bölgedeki en yoğun sözleşmesiz dükkanla anlaşır (satış 2, ilişki 4 günde bir).
    /// Deneyim: görevdeki gün sayısıyla 1–5. seviye (0, 7, 20, 40, 70 gün). Maaş her gün (ayda ~280–420 bin Ft, deneyimle artar, şirket
    /// büyüdükçe Denge.Olcekle); atarken bir aylık maaş kadar işe alım bedeli. Adaylar 3 kişi, 3 günde bir yenilenebilir.
    /// </summary>
    public class BolgeMudurleri : MonoBehaviour
    {
        public static BolgeMudurleri I { get; private set; }

        public static readonly string[] UzmanlikAdlari = { "Satış", "İlişki" };
        public static readonly string[] UzmanlikAciklamalari =
        {
            "Yeni dükkan bulur ve sipariş getirir: talep etkisi ×1,5, iki günde bir yeni sözleşme.",
            "Dükkanlarla arası iyidir: memnuniyet artışı ×2, sözleşme yenilemede dükkanı mevcut şartlarla tutma ihtimali yüksek.",
        };
        static readonly int[] SeviyeGunu = { 0, 7, 20, 40, 70 };
        const long TabanMaas = 350000;

        public readonly List<BolgeMuduru> Mudurler = new List<BolgeMuduru>();
        public readonly List<BolgeMuduru> Adaylar = new List<BolgeMuduru>();
        public int adayYenilemeGunu = -999;
        int sonGun = -1;
        public long odenen;

        void Awake() { I = this; }

        public void Init()
        {
            Mudurler.Clear(); Adaylar.Clear();
            adayYenilemeGunu = -999; sonGun = -1; odenen = 0;
        }

        // ------------------------------------------------------------------ bilgiler

        public static bool Acik => CityMap.I != null && CityMap.I.UnlockLevel >= 2;

        public BolgeMuduru MuduruOf(int bolge)
        {
            foreach (var m in Mudurler) if (m.bolge == bolge) return m;
            return null;
        }

        public static int Seviye(BolgeMuduru m)
        {
            int lv = 1;
            for (int i = 0; i < SeviyeGunu.Length; i++) if (m.gun >= SeviyeGunu[i]) lv = i + 1;
            return lv;
        }

        /// <summary>Sonraki seviyeye kalan gün (son seviyede −1).</summary>
        public static int SonrakiSeviyeyeGun(BolgeMuduru m)
        {
            int lv = Seviye(m);
            return lv < SeviyeGunu.Length ? SeviyeGunu[lv] - m.gun : -1;
        }

        public static long AylikMaas(BolgeMuduru m) =>
            Denge.Olcekle((long)(TabanMaas * (0.8f + 0.4f * m.yetenek) * (1f + 0.05f * (Seviye(m) - 1))));

        public static long GunlukMaas(BolgeMuduru m) => AylikMaas(m) / 30;

        /// <summary>Bölgedeki dükkanların talep eki (0,1 = %10).</summary>
        public static float TalepEki(BolgeMuduru m) =>
            (0.01f + 0.01f * Seviye(m)) * (0.6f + 0.4f * m.yetenek) * (m.uzmanlik == 0 ? 1.5f : 1f);

        public static float MemnuniyetArtisi(BolgeMuduru m) =>
            0.4f * Seviye(m) * (0.6f + 0.4f * m.yetenek) * (m.uzmanlik == 1 ? 2f : 1f);

        public static float TutmaIhtimali(BolgeMuduru m, Shop s) =>
            Mathf.Clamp(0.1f + 0.7f * s.satisfaction / 100f + 0.04f * Seviye(m) + (m.uzmanlik == 1 ? 0.15f : 0f), 0.1f, 0.95f);

        /// <summary>OrderManager.ShopDemand çarpanı.</summary>
        public static float TalepCarpani(Shop s)
        {
            if (I == null || I.Mudurler.Count == 0 || s == null || !s.contracted || s.own || s.kargo) return 1f;
            var m = I.MuduruOf(CityMap.RegionOf(s.tile));
            return m == null ? 1f : 1f + TalepEki(m);
        }

        public long GunlukToplamMaas
        {
            get { long t = 0; foreach (var m in Mudurler) t += GunlukMaas(m); return t; }
        }

        static bool Normal(Shop s) => s != null && !s.own && !s.kargo && !s.isletme && !s.mutfak;

        public int DukkanSayisi(int bolge, bool sozlesmeli)
        {
            int n = 0;
            if (CityMap.I == null) return 0;
            foreach (var s in CityMap.I.Shops)
                if (Normal(s) && s.contracted == sozlesmeli && CityMap.RegionOf(s.tile) == bolge) n++;
            return n;
        }

        // ------------------------------------------------------------------ adaylar ve atama

        public bool AdayYenilenebilir => GameClock.I != null && GameClock.I.Day - adayYenilemeGunu >= 3;

        public void AdaylariYenile()
        {
            if (!AdayYenilenebilir) return;
            adayYenilemeGunu = GameClock.I.Day;
            Adaylar.Clear();
            for (int i = 0; i < 3; i++)
                Adaylar.Add(new BolgeMuduru
                {
                    ad = GameData.RandomName(),
                    yetenek = Mathf.Round(UnityEngine.Random.Range(0.5f, 1f) * 100f) / 100f,
                    uzmanlik = i < 2 ? i : UnityEngine.Random.Range(0, 2),
                    gun = UnityEngine.Random.value < 0.3f ? 7 : 0,   // bazıları başka şirketten tecrübeli gelir
                });
        }

        public static long IseAlimBedeli(BolgeMuduru m) => AylikMaas(m);

        public bool Ata(BolgeMuduru aday, int bolge)
        {
            if (aday == null || !Adaylar.Contains(aday) || MuduruOf(bolge) != null || CityMap.I == null || bolge > CityMap.I.UnlockLevel) return false;
            if (!EconomyManager.I.TrySpend(IseAlimBedeli(aday), $"Bölge müdürü işe alımı: {aday.ad}", ExpenseCategory.Maas)) return false;
            aday.bolge = bolge;
            Adaylar.Remove(aday);
            Mudurler.Add(aday);
            Telemetry.Add("Bölge müdürü atandı");
            EventManager.I.Notify($"{aday.ad} {CityMap.DistrictNames[bolge]} bölge müdürü oldu ({UzmanlikAdlari[aday.uzmanlik]}). " +
                                  $"Bölgedeki dükkanlarla artık o ilgileniyor.");
            return true;
        }

        public void GorevdenAl(BolgeMuduru m)
        {
            if (m == null || !Mudurler.Remove(m)) return;
            EventManager.I.Notify($"{m.ad} {CityMap.DistrictNames[m.bolge]} bölge müdürlüğünden ayrıldı.");
        }

        // ------------------------------------------------------------------ günlük iş

        void Update()
        {
            var clk = GameClock.I;
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || clk == null || clk.Paused) return;
            if (clk.Hour < 8 || clk.Day == sonGun) return;
            sonGun = clk.Day;
            if (Mudurler.Count == 0) return;

            long maas = GunlukToplamMaas;
            if (maas > 0)
            {
                EconomyManager.I.Spend(maas, $"Bölge müdürlerinin maaşı ({Mudurler.Count} kişi)", ExpenseCategory.Maas);
                odenen += maas;
            }
            var yapilan = new List<string>();
            foreach (var m in Mudurler)
            {
                int eskiLv = Seviye(m);
                m.gun++;
                if (Seviye(m) > eskiLv)
                    yapilan.Add($"{m.ad} deneyim kazandı ({Seviye(m)}. seviye, talep +%{TalepEki(m) * 100f:0.#})");
                Ilgilen(m);
                string imza = YeniSozlesme(m, clk.Day);
                if (imza != null) yapilan.Add(imza);
            }
            if (yapilan.Count > 0) EventManager.I.Notify("Bölge müdürleri: " + string.Join(" · ", yapilan) + ".");
        }

        void Ilgilen(BolgeMuduru m)
        {
            float art = MemnuniyetArtisi(m);
            foreach (var s in CityMap.I.Shops)
                if (Normal(s) && s.contracted && CityMap.RegionOf(s.tile) == m.bolge)
                    RivalCompany.AddSatisfaction(s, art);
        }

        string YeniSozlesme(BolgeMuduru m, int gun)
        {
            if (!m.otoSozlesme || gun - m.sonSozlesmeGunu < (m.uzmanlik == 0 ? 2 : 4)) return null;
            Shop en = null;
            foreach (var s in CityMap.I.Shops)
            {
                if (!Normal(s) || s.contracted || CityMap.RegionOf(s.tile) != m.bolge || !CityMap.I.IsUnlocked(s.tile)) continue;
                if (gun < s.resignBlockedUntil) continue;
                if (en == null || s.def.weight > en.def.weight) en = s;
            }
            if (en == null || EconomyManager.I.FreeCash < en.def.contractCost * 3L) return null;
            if (!CityMap.I.SignContract(en)) return null;
            m.sonSozlesmeGunu = gun;
            m.imzalanan++;
            return $"{m.ad} {en.Name} ile sözleşme imzaladı ({GameData.Ft(en.def.contractCost)})";
        }

        /// <summary>
        /// RivalCompany.Renewal çağırır: rakip teklif varken bölgenin müdürü varsa kartsız karar verir. true: müdür halletti.
        /// eslestir: dükkanı teklife eşitleyerek tuttu (komisyon düşer).
        /// </summary>
        public bool YenilemeyiUstlen(Shop s, string rakipAd, out bool eslestir)
        {
            eslestir = false;
            if (s == null) return false;
            var m = MuduruOf(CityMap.RegionOf(s.tile));
            if (m == null) return false;
            eslestir = UnityEngine.Random.value >= TutmaIhtimali(m, s);
            m.yenilenen++;
            EventManager.I.Notify(eslestir
                ? $"Bölge müdürün {m.ad}, {TurkceEk.In(rakipAd)} teklifine eşitleyerek {s.Name} sözleşmesini 6 ay uzattı."
                : $"Bölge müdürün {m.ad}, {rakipAd} teklif vermesine rağmen {s.Name} sözleşmesini mevcut şartlarla 6 ay uzattı.");
            return true;
        }

        // ------------------------------------------------------------------ kayıt

        public BolgeSave Kaydet() => new BolgeSave { mudurler = new List<BolgeMuduru>(Mudurler), sonGun = sonGun, odenen = odenen };

        public void Yukle(BolgeSave s)
        {
            Init();
            if (s == null) return;
            if (s.mudurler != null) Mudurler.AddRange(s.mudurler);
            sonGun = s.sonGun;
            odenen = s.odenen;
        }
    }
}
