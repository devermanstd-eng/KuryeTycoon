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
        // u68 (sonsuz büyüme): otomatik dükkan yatırımı, kart kararları ve hücum yeteneği (eski kayıtlarda varsayılanlar)
        public bool otoYatirim = true;
        public int yatirimYuzde = 20;      // serbest kasanın günde en çok yüzde kaçı bölgenin dükkan seviyelerine
        public int yatirimSayisi;          // aldığı dükkan seviyesi
        public long yatirimToplam;
        public int kartKarari;             // senin yerine karar verdiği kart
        public float hucumBitis = -1f;     // GameClock.TotalMinutes: hücum bu dakikaya kadar sürer
        public int hucumHazirGun = -999;   // bu günden itibaren yeniden kullanılabilir
        public int hucumSayisi;
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
        public const float HucumSuresiDk = 120f;
        public const float HucumCarpanDegeri = 1.5f;
        public const int HucumBeklemeGun = 2;
        public const int KartRaporuIcinMudur = 3;   // bu kadar müdür varken gün sonu raporu kart olarak açılmaz
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

        // ------------------------------------------------------------------ hücum (aktif yetenek, u68)

        public static bool HucumAktif(BolgeMuduru m) => m != null && GameClock.I != null && GameClock.I.TotalMinutes < m.hucumBitis;
        public static bool HucumHazir(BolgeMuduru m) => m != null && GameClock.I != null && !HucumAktif(m) && GameClock.I.Day >= m.hucumHazirGun;

        /// <summary>Müdürün bölgesinde hücum sürüyorsa o bölgedeki dükkanların sipariş çarpanı (OrderManager).</summary>
        public static float HucumCarpani(Shop s)
        {
            if (I == null || I.Mudurler.Count == 0 || s == null || !s.contracted || s.own || s.kargo) return 1f;
            var m = I.MuduruOf(CityMap.RegionOf(s.tile));
            return HucumAktif(m) ? HucumCarpanDegeri : 1f;
        }

        /// <summary>
        /// "Sipariş hücumu": müdür bölgedeki dükkanlarla 2 saatlik bir kampanya yapar (uygulamada bölge vitrini, ücretsiz teslimat
        /// rozeti): siparişler ×1,5. İki günde bir kullanılabilir. Yoğun saatten hemen önce açmak en kârlısı; yeterli kurye yoksa
        /// siparişler kaçar.
        /// </summary>
        public bool HucumBaslat(BolgeMuduru m)
        {
            if (!HucumHazir(m)) return false;
            m.hucumBitis = GameClock.I.TotalMinutes + HucumSuresiDk;
            m.hucumHazirGun = GameClock.I.Day + HucumBeklemeGun;
            m.hucumSayisi++;
            Telemetry.Add("Müdür hücumu");
            EventManager.I.Notify($"{m.ad}: {CityMap.DistrictNames[m.bolge]} sipariş hücumu başladı. 2 saat boyunca bölgedeki dükkanlara sipariş ×{HucumCarpanDegeri:0.#}. " +
                                  "Yeterli kurye çevrimiçi değilse siparişler kaçar.");
            return true;
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
                string yatirim = OtoYatirim(m);
                if (yatirim != null) yapilan.Add(yatirim);
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
        /// Otomatik dükkan yatırımı (u68): serbest kasanın yatirimYuzde'si kadarını bölgedeki en ucuz dükkan seviyelerine yatırır
        /// (en çok 20 seviye). Kasada bir haftalık sabit gider kadar para hep kalır.
        /// </summary>
        string OtoYatirim(BolgeMuduru m)
        {
            if (!m.otoYatirim || m.yatirimYuzde <= 0 || EconomyManager.I == null || UpgradeManager.I == null) return null;
            if (!DukkanSeviyesi.Acik || !DukkanSeviyesi.KuryeYetiyor) return null;   // kurye yetmiyorsa sipariş katlanmaz
            long rezerv = 7L * (UpgradeManager.I.TotalDailyCost + GunlukToplamMaas);
            long serbest = EconomyManager.I.FreeCash - rezerv;
            if (serbest <= 0) return null;
            long butce = serbest * m.yatirimYuzde / 100;
            int alinan = 0, donum = 0;
            long harcanan = 0;
            for (int k = 0; k < 20; k++)
            {
                Shop en = null;
                long enBedel = long.MaxValue;
                foreach (var s in CityMap.I.Shops)
                {
                    if (!DukkanSeviyesi.Uygun(s) || CityMap.RegionOf(s.tile) != m.bolge) continue;
                    long b = DukkanSeviyesi.Bedel(s);
                    if (b < enBedel) { en = s; enBedel = b; }
                }
                if (en == null || harcanan + enBedel > butce) break;
                int eski = DukkanSeviyesi.DonumSayisi(en.seviye);
                if (!DukkanSeviyesi.SeviyeAtla(en, true)) break;
                harcanan += enBedel;
                alinan++;
                if (DukkanSeviyesi.DonumSayisi(en.seviye) > eski) donum++;
            }
            if (alinan == 0) return null;
            m.yatirimSayisi += alinan;
            m.yatirimToplam += harcanan;
            return $"{m.ad} bölgedeki dükkanlara {alinan} ortaklık seviyesi yatırdı ({GameData.Ft(harcanan)})" + (donum > 0 ? $", {donum} dönüm noktası" : "");
        }

        // ------------------------------------------------------------------ kartları devralma (u68)

        static Shop DukkanAdla(string ad)
        {
            if (CityMap.I == null || string.IsNullOrEmpty(ad)) return null;
            foreach (var s in CityMap.I.Shops) if (s.Name == ad) return s;
            return null;
        }

        /// <summary>Metinde adı geçen dükkan (en uzun eşleşen ad: "Burger Queen" ile "Burger Queen Express" karışmasın).</summary>
        static Shop DukkanMetinde(string metin)
        {
            if (CityMap.I == null || string.IsNullOrEmpty(metin)) return null;
            Shop en = null;
            foreach (var s in CityMap.I.Shops)
                if (!string.IsNullOrEmpty(s.Name) && metin.Contains(s.Name) && (en == null || s.Name.Length > en.Name.Length)) en = s;
            return en;
        }

        static int Secenek(GameEvent e, string parca)
        {
            for (int i = 0; i < e.choices.Count; i++)
                if (e.choices[i].label != null && e.choices[i].label.IndexOf(parca, StringComparison.OrdinalIgnoreCase) >= 0) return i;
            return -1;
        }

        /// <summary>Dükkanın son ayda (yoksa bu ay) sözleşmeli dükkanların siparişlerindeki payı.</summary>
        static float Pay(Shop s)
        {
            long top = 0, bu = 0;
            foreach (var x in CityMap.I.Shops)
            {
                if (!x.contracted) continue;
                int n = Mathf.Max(x.lastMonthOrders, x.monthOrders);
                top += n;
                if (x == s) bu = n;
            }
            return top > 0 ? bu / (float)top : 0f;
        }

        /// <summary>
        /// EventManager.Push çağırır: kart müdürün işiyse kart açılmaz, müdür seçer ve sonucu bildirim olarak yazar. true: devraldı.
        /// Devraldıkları: bölgesindeki bir dükkanın komisyon indirimi istemesi, ortak reklam teklifi ve dükkan sahibinin indirim baskısı.
        /// Bütün bölgelerin yarısından fazlasında (en az 3) müdür varsa gün sonu raporu da kart olarak açılmaz (haber akışına düşer).
        /// </summary>
        public bool KartiUstlen(GameEvent e)
        {
            if (Mudurler.Count == 0 || e == null || string.IsNullOrEmpty(e.title) || CityMap.I == null) return false;
            string t = e.title;

            if (t.StartsWith("Gün sonu raporu") && Mudurler.Count >= KartRaporuIcinMudur && Mudurler.Count * 2 > CityMap.I.UnlockLevel)
            {
                Telemetry.Add("Müdür kararı: Gün sonu raporu");
                string ozet = "";
                if (e.ozet != null)
                    foreach (var o in e.ozet) ozet += (ozet.Length > 0 ? " · " : "") + $"{o.baslik} {o.deger}";
                EventManager.I.Notify(t + (ozet.Length > 0 ? ": " + ozet : "") + " (bölge müdürleri raporu okudu, ayrıntı İstatistik'te).");
                return true;
            }

            const string indirimEki = " indirim istiyor";
            Shop s;
            if (t.EndsWith(indirimEki)) s = DukkanAdla(t.Substring(0, t.Length - indirimEki.Length));
            else if (t == "Komisyon teklifi" || t == "Dükkan komisyon indirimi istiyor") s = DukkanMetinde(e.body);
            else return false;
            if (s == null) return false;
            var m = MuduruOf(CityMap.RegionOf(s.tile));
            if (m == null) return false;

            int sec;
            string neden;
            float pay = Pay(s);
            if (t.EndsWith(indirimEki))
            {
                // Hacim indirimi (−2 puan): dükkan memnuniyetsizse ya da siparişlerin büyük kısmı ondan geliyorsa kabul
                bool kabul = s.satisfaction < 50f || pay >= 0.2f;
                sec = Secenek(e, kabul ? "kabul" : "reddet");
                neden = kabul ? $"memnuniyet {s.satisfaction:0}, siparişlerin %{pay * 100f:0}'i: dükkanı kaybetmemek için" : $"memnuniyet {s.satisfaction:0} yeterli, komisyon korunur";
            }
            else if (t == "Komisyon teklifi")
            {
                // Ortak reklam karşılığı +2 puan komisyon: 3 aylık ek komisyon bedelin 1,5 katını geçiyorsa kabul
                float gunluk = Mathf.Max(s.lastMonthOrders / 30f, s.monthOrders / (float)Mathf.Max(1, GameClock.I.DayOfMonth));
                float sepet = (s.def.basketMin + s.def.basketMax) / 2f;
                long kazanc = (long)(gunluk * sepet * 0.02f * 90f);
                bool kabul = kazanc > 45000 && EconomyManager.I.FreeCash > 120000;
                sec = Secenek(e, kabul ? "kabul" : "reddet");
                neden = $"3 ayda ~{GameData.Ft(kazanc)} ek komisyon" + (kabul ? "" : ", değmez");
            }
            else
            {
                // Kalıcı −3 puan: reddetmek memnuniyeti 30 düşürür; dükkan önemliyse ve gidecekse kabul
                bool kabul = s.satisfaction - 30f < 40f && pay >= 0.1f;
                sec = Secenek(e, kabul ? "kabul" : "reddet");
                neden = kabul ? $"memnuniyet {s.satisfaction:0}: reddedilirse dükkan gider (siparişlerin %{pay * 100f:0}'i)" : $"memnuniyet {s.satisfaction:0}, kalıcı indirim verilmez";
            }
            if (sec < 0) return false;

            var secim = e.choices[sec];
            m.kartKarari++;
            Telemetry.Add("Müdür kararı: " + t);
            EventManager.I.Notify($"{m.ad} ({CityMap.DistrictNames[m.bolge]} müdürü) senin yerine karar verdi: {t} → {secim.label} · {neden}.");
            secim.action?.Invoke();
            return true;
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
