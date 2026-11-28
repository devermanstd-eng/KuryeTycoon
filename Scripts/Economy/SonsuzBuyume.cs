using System;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Dükkan ortaklık seviyesi (28.11, u68: "tek şehirde sonsuz büyüme"). Sözleşmeli her dükkana şirket para yatırır: uygulamada öne
    /// çıkarma, profesyonel menü fotoğrafları, paketleme malzemesi, dükkana ikinci bir sipariş tableti ve hazırlık hattı. Her seviye o
    /// dükkanın siparişini +%3 artırır; 10, 25, 50 ve 100. seviyelerde (100'den sonra her 50 seviyede) sipariş ×1,5 olur (dönüm noktası).
    /// Bedel her seviyede ×1,13 büyür (AdVenture Capitalist'teki 1,07–1,15 aralığı), tabanı dükkanın sepet büyüklüğüne bağlı.
    ///
    /// Neden: 120 günlük testlerde bot 48. günde bütün bölgeleri, ~86. günde 100 kurye hesabını doldurunca büyüyecek bir şey kalmıyordu.
    /// Seviye sınırı yok: şirket her zaman bir sonraki dükkan seviyesine para yatırabilir; artan sipariş de daha çok kurye ister
    /// (FiloMerkezi). Seviye dükkanda kalır: sözleşme biterse etkisi durur, yeniden imzalanınca geri gelir.
    ///
    /// Sipariş etkisi OrderManager'da iki yerde: toplam sipariş hızı (Update'teki talep toplamı) ve siparişin hangi dükkandan
    /// geleceği (ShopDemand). Bölge müdürünün hücumu (BolgeMudurleri.HucumCarpani) da aynı yerlere girer.
    /// </summary>
    public static class DukkanSeviyesi
    {
        public static readonly int[] Donumler = { 10, 25, 50, 100 };
        public const float SeviyeBasiTalep = 0.03f;
        public const float DonumCarpani = 1.5f;
        public const float BedelBuyume = 1.13f;
        const int DonumSonrasiAralik = 50;   // 100. seviyeden sonra her 50 seviyede bir dönüm noktası

        public const int GerekenBolge = 3;   // Industry & Campus: ilk haftalar bugünkü gibi kalsın (kurye, sözleşme, bölge)

        public static void Init() { }

        /// <summary>Ortaklık seviyeleri açık mı (üçüncü bölge açılınca, botta ~17. gün).</summary>
        public static bool Acik => CityMap.I != null && CityMap.I.UnlockLevel >= GerekenBolge;

        /// <summary>Seviye yatırılabilecek dükkan: sözleşmeli, açık bölgede, sıradan bir dükkan (kendi marketin, mutfak, kargo değil).</summary>
        public static bool Uygun(Shop s) =>
            s != null && s.contracted && !s.own && !s.kargo && !s.isletme && !s.mutfak && CityMap.I != null && CityMap.I.IsUnlocked(s.tile);

        public static int DonumSayisi(int lv)
        {
            int n = 0;
            foreach (int d in Donumler) if (lv >= d) n++;
            int son = Donumler[Donumler.Length - 1];
            if (lv >= son + DonumSonrasiAralik) n += (lv - son) / DonumSonrasiAralik;
            return n;
        }

        /// <summary>Bu seviyeden sonraki dönüm noktası.</summary>
        public static int SonrakiDonum(int lv)
        {
            foreach (int d in Donumler) if (lv < d) return d;
            int son = Donumler[Donumler.Length - 1];
            return son + ((lv - son) / DonumSonrasiAralik + 1) * DonumSonrasiAralik;
        }

        /// <summary>Seviyenin sipariş çarpanı: (1 + 0,03 × seviye) × 1,5^dönüm.</summary>
        public static float Carpan(int lv) =>
            lv <= 0 ? 1f : (1f + SeviyeBasiTalep * lv) * Mathf.Pow(DonumCarpani, DonumSayisi(lv));

        /// <summary>OrderManager çarpanı (sözleşme yoksa ya da dükkan sıradan değilse 1).</summary>
        public static float TalepCarpani(Shop s) =>
            s == null || s.seviye <= 0 || !s.contracted || s.own || s.kargo || s.isletme || s.mutfak ? 1f : Carpan(s.seviye);

        static long Taban(Shop s) => Math.Max(40000L, (long)((s.def.basketMin + s.def.basketMax) * 0.5f * 12f));

        /// <summary>Bir sonraki seviyenin bedeli.</summary>
        public static long Bedel(Shop s) => Bedel(s, s.seviye);

        public static long Bedel(Shop s, int lv)
        {
            double b = Taban(s) * Math.Pow(BedelBuyume, lv) * Marka.GelistirmeCarpani;
            return (long)(Math.Round(b / 1000.0) * 1000.0);
        }

        /// <summary>Seviyeyi bir artırır. sessiz: dönüm noktası dışında bildirim yazmaz (müdürün otomatik yatırımı).</summary>
        public static bool SeviyeAtla(Shop s, bool sessiz = false)
        {
            if (!Acik || !Uygun(s) || EconomyManager.I == null) return false;
            long b = Bedel(s);
            if (EconomyManager.I.FreeCash < b)
            {
                if (!sessiz) EventManager.I.Notify($"{s.Name} ortaklık seviyesi için serbest kasa yetmiyor ({GameData.Ft(b)}).");
                return false;
            }
            if (!EconomyManager.I.TrySpendYatirim(b, $"Dükkan ortaklığı: {s.Name} → {s.seviye + 1}. seviye", ExpenseCategory.Gelistirme)) return false;
            int eskiDonum = DonumSayisi(s.seviye);
            s.seviye++;
            Telemetry.Add("Dükkan seviyesi");
            if (DonumSayisi(s.seviye) > eskiDonum)
            {
                Telemetry.Add("Dükkan dönüm noktası");
                EventManager.I.Notify($"{s.Name} {s.seviye}. ortaklık seviyesine ulaştı: siparişleri ×{DonumCarpani:0.#} (toplam ×{Carpan(s.seviye):0.0}). " +
                                      $"Sonraki dönüm noktası {SonrakiDonum(s.seviye)}. seviye.");
            }
            else if (!sessiz)
                EventManager.I.NotifyQuiet($"{s.Name} ortaklık seviyesi {s.seviye} (sipariş ×{Carpan(s.seviye):0.00}).");
            return true;
        }

        /// <summary>
        /// Kurye yetiyor mu: dün iptal + kaçan siparişler toplamın %8'inden azsa. Müdürün otomatik yatırımı ve bot buna bakar
        /// (kurye yetmezken sipariş katlamak iptal ve itibar kaybı demek; önce filo merkezi ya da platform paketi).
        /// </summary>
        public static bool KuryeYetiyor
        {
            get
            {
                var h = StatsHistory.I;
                if (h == null || h.Days.Count == 0) return true;
                var d = h.Days[h.Days.Count - 1];
                int top = d.deliveries + d.cancelled + d.lost;
                return top <= 0 || (d.cancelled + d.lost) / (float)top < 0.08f;
            }
        }

        /// <summary>Kaç seviye birden alınabilir (en çok n), toplam bedeliyle.</summary>
        public static int AlinabilirSeviye(Shop s, int n, long butce, out long toplam)
        {
            toplam = 0;
            int k = 0;
            for (; k < n; k++)
            {
                long b = Bedel(s, s.seviye + k);
                if (toplam + b > butce) break;
                toplam += b;
            }
            return k;
        }

        public static string Aciklama =>
            "Dükkana ortak yatırım: uygulamada öne çıkarma, profesyonel menü fotoğrafları, paketleme malzemesi, ikinci sipariş tableti ve " +
            $"hazırlık hattı. Her seviye siparişi +%{SeviyeBasiTalep * 100f:0} artırır; {string.Join(", ", Donumler)}. seviyelerde " +
            $"(sonra her {DonumSonrasiAralik} seviyede) siparişler ×{DonumCarpani:0.#} olur. Bedel her seviyede ×{BedelBuyume:0.00} büyür. " +
            "Seviye sınırı yok; artan sipariş daha çok kurye ister.";
    }

    /// <summary>
    /// Filo merkezleri (28.11, u68). En büyük platform paketlerinin üstünde sınırsız kurye kapasitesi: 100 hesaplık paketten (Büyük şehir)
    /// sonra her açık bölgede bir filo merkezi kurulup büyütülebilir. Her seviye +6 kurye hesabı (merkezin kayıt, ekipman ve vardiya
    /// kapasitesi), bedeli bütün merkezlerin toplam seviyesiyle ×1,07 büyür (u70; ilk sürümde ×1,15); her seviyenin günlük kirası 16 000 Ft (40 €).
    /// Platform paketleri (Denizhan'ın 26.11 eğrisi) aynen kalır: paketler kurye başına daha ucuzdur, filo merkezi onların üstündeki
    /// sınırsız kuyruk ve ofis/hub kilitlerine takılınca alternatif yol.
    /// </summary>
    public static class FiloMerkezi
    {
        public const int HesapPerSeviye = 6;
        public const long TabanBedel = 4000000;          // 10 000 €
        public const float BedelBuyume = 1.07f;          // u70: 1,15 → 1,07 (10 M € testinde 32 seviye ~5,8 M € tuttu)
        public const long SeviyeGunlukGider = 16000;     // 40 €
        public const int GerekenPlatform = 8;            // Büyük şehir (100 hesap)

        static readonly int[] seviye = new int[6];       // 1–5 bölge

        public static void Init() { Array.Clear(seviye, 0, seviye.Length); }

        public static int Seviye(int bolge) => bolge >= 1 && bolge < seviye.Length ? seviye[bolge] : 0;

        public static int Toplam
        {
            get { int t = 0; for (int i = 1; i < seviye.Length; i++) t += seviye[i]; return t; }
        }

        public static int EkHesap => Toplam * HesapPerSeviye;

        public static long GunlukGider => SeviyeGunlukGider * Toplam;

        public static bool Acik => UpgradeManager.I != null && UpgradeManager.I.Level(UpgradeKind.Platform) >= GerekenPlatform;

        /// <summary>Bu bölgede merkez neden kurulamıyor (boşsa kurulabilir).</summary>
        public static string Kilit(int bolge)
        {
            if (!Acik)
                return $"önce kurye platformu paketi: {UpgradeManager.Def(UpgradeKind.Platform).levelNames[GerekenPlatform]} (100 kurye hesabı)";
            if (CityMap.I == null || bolge < 1 || bolge > CityMap.I.UnlockLevel) return "önce bölgeyi aç";
            return "";
        }

        /// <summary>Bir sonraki seviyenin bedeli (hangi bölgede olursa olsun).</summary>
        public static long Bedel
        {
            get
            {
                double b = TabanBedel * Math.Pow(BedelBuyume, Toplam) * Marka.GelistirmeCarpani;
                return (long)(Math.Round(b / 1000.0) * 1000.0);
            }
        }

        public static bool Kur(int bolge, bool sessiz = false)
        {
            if (Kilit(bolge).Length > 0 || EconomyManager.I == null) return false;
            long b = Bedel;
            string ad = CityMap.DistrictNames[bolge];
            if (!EconomyManager.I.TrySpendYatirim(b, $"Filo merkezi: {ad} → {seviye[bolge] + 1}. seviye", ExpenseCategory.Gelistirme)) return false;
            seviye[bolge]++;
            Telemetry.Add("Filo merkezi");
            if (!sessiz)
                EventManager.I.Notify($"{ad} filo merkezi {seviye[bolge]}. seviyede: +{HesapPerSeviye} kurye hesabı (toplam kurye sınırı " +
                                      $"{UpgradeManager.I.CourierCap}). Günlük kira {GameData.Ft(GunlukGider)}.");
            return true;
        }

        public static int[] Kaydet() => (int[])seviye.Clone();

        public static void Yukle(int[] s)
        {
            Init();
            if (s == null) return;
            for (int i = 1; i < seviye.Length && i < s.Length; i++) seviye[i] = Mathf.Max(0, s[i]);
        }

        public static string Aciklama =>
            "Büyük şehir paketinden (100 kurye hesabı) sonra her bölgede bir filo merkezi kurabilirsin: kuryelerin kaydının yapıldığı, " +
            $"ekipman aldığı, vardiya planlanan bir bina. Her seviye +{HesapPerSeviye} kurye hesabı, günlük kirası {GameData.Ft(SeviyeGunlukGider)}. " +
            $"Bedel bütün merkezlerin toplam seviyesiyle ×{BedelBuyume:0.00} büyür. Sınır yok.";
    }
}
