using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KuryeTycoon
{
    [Serializable]
    public class Istirak
    {
        public string ad = "", sehir = "";
        public long deger;          // holdinge katıldığı andaki şirket değeri
        public long temettu;        // taban günlük temettü (Ft)
        public int seviye;          // yönetime yapılan yatırım (0–10)
        public long yatirilan;
        public long odenen;         // bugüne kadar ödediği temettü
    }

    [Serializable]
    public class HoldingVeri
    {
        public List<Istirak> istirakler = new List<Istirak>();
        public long toplamTemettu;
    }

    /// <summary>
    /// Holding (ikinci prestij katmanı, Şirket > Holding). Büyüyen şirketi satmak yerine holdinge katabilirsin: marka puanının yarısını
    /// alırsın ve yine bir sonraki şehirde baştan başlarsın, ama eski şirket kapanmaz; atadığın yönetimle çalışmaya devam eder ve
    /// sonraki bütün oyunlarına her gün temettü öder (son 7 günün ortalama net kârının %8'i × payın, en az 10 000 Ft).
    ///  • Temettü sermaye olarak girer (AddSermaye): günlük işletme kârına ve şirket değerine sayılmaz, büyüme çarpanını şişirmez.
    ///  • Yönetime yatırım: oyundaki kasandan, iştirakin temettüsünü her seviyede taban değerin %50'si kadar artırır (en fazla 10).
    ///    Bedel = taban temettü × 20 × (seviye+1)^1,5; ilk seviye ~40 günde kendini öder, sonrakiler daha geç.
    ///  • Sinerji: her iştirak sipariş +%2 (en fazla 6 iştirak, +%12): marka birden çok şehirde tanınıyor.
    /// Bilgisayarda bir dosyada durur (holding.json), kayıt yuvalarından bağımsız. Otomatik testte etkisi yoktur.
    /// </summary>
    public class Holding : MonoBehaviour
    {
        public static Holding I { get; private set; }

        public const float TemettuOrani = 0.08f;
        public const long EnAzTemettu = 10000;
        public const int MaksSeviye = 10;
        public const float SatisPuanOrani = 0.5f;

        static HoldingVeri veri;
        static string Dosya => Path.Combine(Application.persistentDataPath, "holding.json");

        /// <summary>Bu oyunda temettünün en son ödendiği gün (kayda girer: yeniden yüklemede iki kez ödenmesin).</summary>
        public int sonGun = -1;

        void Awake() { I = this; }

        public void Init() { sonGun = -1; }

        public static HoldingVeri Veri
        {
            get
            {
                if (veri == null) Yukle();
                return veri;
            }
        }

        static void Yukle()
        {
            veri = null;
            try { if (File.Exists(Dosya)) veri = JsonUtility.FromJson<HoldingVeri>(File.ReadAllText(Dosya)); }
            catch (Exception e) { Debug.LogWarning("Holding dosyası okunamadı: " + e.Message); }
            if (veri == null) veri = new HoldingVeri();
            if (veri.istirakler == null) veri.istirakler = new List<Istirak>();
        }

        static void Kaydet()
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(Dosya, JsonUtility.ToJson(Veri, true));
            }
            catch (Exception e) { Debug.LogWarning("Holding dosyası yazılamadı: " + e.Message); }
        }

        static bool Etkin => !TestModu.Aktif;

        public static int IstirakSayisi => Veri.istirakler.Count;

        public static string Unvan =>
            IstirakSayisi == 0 ? "Holding yok" : IstirakSayisi == 1 ? "Girişim grubu" : IstirakSayisi <= 3 ? "Holding" : IstirakSayisi <= 5 ? "Büyük holding" : "Konglomera";

        // ------------------------------------------------------------------ hesaplar

        public static long Temettu(Istirak i) => i == null ? 0 : i.temettu + i.temettu * i.seviye / 2;

        public static long YatirimBedeli(Istirak i) => i == null ? 0 : (long)(20f * i.temettu * Mathf.Pow(i.seviye + 1, 1.5f));

        public static long GunlukToplam
        {
            get { long t = 0; foreach (var i in Veri.istirakler) t += Temettu(i); return t; }
        }

        public static long ToplamDeger
        {
            get { long t = 0; foreach (var i in Veri.istirakler) t += i.deger; return t; }
        }

        /// <summary>OrderManager sipariş oranı çarpanı (sinerji).</summary>
        public static float SiparisCarpani => Etkin ? 1f + 0.02f * Mathf.Min(6, IstirakSayisi) : 1f;

        /// <summary>Bu şirket şimdi holdinge katılsa günlük taban temettüsü.</summary>
        public static long OlasiTemettu => Math.Max(EnAzTemettu, (long)(Math.Max(0L, Buyume.OrtalamaNet(7)) * Marka.Pay * TemettuOrani));

        public static int OlasiPuan
        {
            get
            {
                int tam = Marka.PuanFor(Marka.PayinDegeri);
                return tam < 1 ? 0 : Mathf.Max(1, Mathf.FloorToInt(tam * SatisPuanOrani));
            }
        }

        // ------------------------------------------------------------------ işlemler

        /// <summary>Şirketi holdinge katar: marka puanının yarısı, iştirak kaydı; oyun biter (Marka.Sat kaydı siler). Dönüş: alınan puan.</summary>
        public static int HoldingeKat()
        {
            if (Marka.SatisEngeli.Length > 0) return 0;
            var yeni = new Istirak
            {
                ad = CompanyProfile.Name,
                sehir = Marka.SehirAdi,
                deger = Marka.PayinDegeri,
                temettu = OlasiTemettu,
            };
            int p = Marka.Sat(SatisPuanOrani);
            if (p <= 0) return 0;
            Veri.istirakler.Add(yeni);
            Kaydet();
            return p;
        }

        public static bool Yatir(Istirak i)
        {
            if (i == null || i.seviye >= MaksSeviye || EconomyManager.I == null) return false;
            long bedel = YatirimBedeli(i);
            if (!EconomyManager.I.TrySpendYatirim(bedel, $"Holding: {i.ad} yönetimine yatırım", ExpenseCategory.Gelistirme)) return false;
            i.seviye++;
            i.yatirilan += bedel;
            Kaydet();
            EventManager.I.Notify($"{i.ad} ({i.sehir}) yönetimine yatırım yaptın: günlük temettü {GameData.Ft(Temettu(i))}.");
            return true;
        }

        // ------------------------------------------------------------------ günlük temettü

        void Update()
        {
            var clk = GameClock.I;
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || clk == null || clk.Paused || !Etkin) return;
            if (clk.Hour < 9 || clk.Day == sonGun) return;
            bool ilk = sonGun < 0;
            sonGun = clk.Day;
            if (IstirakSayisi == 0 || EconomyManager.I == null) return;
            long toplam = 0;
            foreach (var i in Veri.istirakler)
            {
                long t = Temettu(i);
                i.odenen += t;
                toplam += t;
            }
            Veri.toplamTemettu += toplam;
            Kaydet();
            EconomyManager.I.AddSermaye(toplam, $"Holding temettüsü ({IstirakSayisi} iştirak)");
            if (ilk) EventManager.I.Notify($"Holding: {IstirakSayisi} iştirakin her sabah {GameData.Ft(toplam)} temettü gönderiyor (Şirket > Holding).");
        }
    }
}
