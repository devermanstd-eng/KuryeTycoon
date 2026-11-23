using System;
using System.IO;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Marka puanıyla alınan kalıcı geliştirmeler (sıra dosya uyumu için sabit: yenisi sona).</summary>
    public enum MarkaPerk { Sermaye, Taninirlik, KuryeAgi, Pazarlik, Yonetim, HazirSistemler }

    [Serializable]
    public class MarkaVeri
    {
        public int puan;                 // harcanmamış marka puanı
        public int toplamPuan;           // bugüne kadar kazanılan
        public int satilan;              // satılan şirket sayısı (= kaçıncı şehirde olduğun)
        public long enBuyukSatis;        // en değerli satış (Ft)
        public int[] seviye = new int[6];
        public string sonSirket = "";
    }

    /// <summary>
    /// Prestij (yeniden başlama döngüsü). Şirket büyüyünce satılır; satıştan marka puanı gelir ve oyuncu bir sonraki,
    /// daha büyük şehirde sıfırdan başlar. Marka puanı kalıcı geliştirmelere harcanır; her satılan şirket ayrıca kalıcı
    /// "tecrübe" verir (komisyon geliri +%5). Kayıt yuvalarından bağımsız, bilgisayarda bir dosyada durur (marka.json).
    /// Otomatik testte marka bonusları kapalıdır (sonuçlar karşılaştırılabilir kalsın).
    /// </summary>
    public static class Marka
    {
        public static readonly string[] Sehirler = { "Nyíregyháza", "Debrecen", "Miskolc", "Szeged", "Pécs", "Győr", "Budapest" };
        public static readonly string[] PostaKodlari = { "4400", "4024", "3525", "6720", "7621", "9021", "1051" };
        public static readonly string[] Takimlar = { "Nyíregyháza Spartacus", "Debreceni VSC", "Diósgyőri VTK", "Szeged-Csanád", "PMFC", "ETO FC Győr", "Ferencváros" };

        public static readonly string[] PerkAdlari = { "Sermaye", "Tanınmış marka", "Kurye ağı", "Pazarlık gücü", "Deneyimli yönetim", "Hazır sistemler" };
        public static readonly int[] PerkMaks = { 5, 5, 5, 5, 5, 3 };

        public const long PuanBirimi = 1600000;          // 4 000 €: değer bunun n² katına ulaşınca n puan
        public const int EnErkenSatisGunu = 30;

        static MarkaVeri veri;
        static string Dosya => Path.Combine(Application.persistentDataPath, "marka.json");

        public static MarkaVeri Veri
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
            try { if (File.Exists(Dosya)) veri = JsonUtility.FromJson<MarkaVeri>(File.ReadAllText(Dosya)); }
            catch (Exception e) { Debug.LogWarning("Marka dosyası okunamadı: " + e.Message); }
            if (veri == null) veri = new MarkaVeri();
            if (veri.seviye == null || veri.seviye.Length < PerkMaks.Length)
            {
                var s = new int[PerkMaks.Length];
                if (veri.seviye != null) Array.Copy(veri.seviye, s, Mathf.Min(veri.seviye.Length, s.Length));
                veri.seviye = s;
            }
        }

        static void Kaydet()
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(Dosya, JsonUtility.ToJson(Veri, true));
            }
            catch (Exception e) { Debug.LogWarning("Marka dosyası yazılamadı: " + e.Message); }
        }

        /// <summary>Bonuslar etkin mi (otomatik testte kapalı).</summary>
        static bool Etkin => !TestModu.Aktif;

        public static int Seviye(MarkaPerk p) => Etkin ? Veri.seviye[(int)p] : 0;

        // ------------------------------------------------------------------ şehir

        /// <summary>Oynanan oyunun şehri (yeni oyunda satılan şirket sayısından, yüklemede kayıttan gelir).</summary>
        public static int AktifSehir;

        public static string SehirAdi => Sehirler[Mathf.Clamp(AktifSehir, 0, Sehirler.Length - 1)];
        public static string PostaKodu => PostaKodlari[Mathf.Clamp(AktifSehir, 0, PostaKodlari.Length - 1)];
        public static string Takim => Takimlar[Mathf.Clamp(AktifSehir, 0, Takimlar.Length - 1)];
        public static int SiradakiSehir => Etkin ? Mathf.Min(Veri.satilan, Sehirler.Length - 1) : 0;

        /// <summary>Büyük şehir: daha çok sipariş (+%8/şehir) ama rakipler daha güçlü başlar.</summary>
        public static float SehirSiparisCarpani => 1f + 0.08f * AktifSehir;

        // ------------------------------------------------------------------ bonuslar

        public static int TecrubeSayisi => Etkin ? Mathf.Min(10, Veri.satilan) : 0;
        /// <summary>Komisyon gelirine kalıcı çarpan: pazarlık gücü ve tecrübe (satılan şirket).</summary>
        public static float GelirCarpani => (1f + 0.06f * Seviye(MarkaPerk.Pazarlik)) * (1f + 0.05f * TecrubeSayisi);
        public static float SiparisCarpani => 1f + 0.05f * Seviye(MarkaPerk.Taninirlik);
        public static float BasvuruCarpani => 1f + 0.12f * Seviye(MarkaPerk.KuryeAgi);
        public static float GelistirmeCarpani => 1f - 0.06f * Seviye(MarkaPerk.Yonetim);
        public static long BaslangicEki => 160000L * Seviye(MarkaPerk.Sermaye);
        public static int HazirOtomasyon => Seviye(MarkaPerk.HazirSistemler);

        public static string PerkEtkisi(MarkaPerk p, int lv)
        {
            switch (p)
            {
                case MarkaPerk.Sermaye: return $"başlangıç kasası +{GameData.Ft(160000L * lv)}";
                case MarkaPerk.Taninirlik: return $"sipariş +%{5 * lv}";
                case MarkaPerk.KuryeAgi: return $"kurye başvurusu +%{12 * lv}";
                case MarkaPerk.Pazarlik: return $"komisyon geliri +%{6 * lv}";
                case MarkaPerk.Yonetim: return $"geliştirme kurulum bedeli −%{6 * lv}";
                default: return lv == 0 ? "otomasyon yok" : $"Otomasyon geliştirmesi {lv}. seviyede başlar";
            }
        }

        public static int PerkBedeli(MarkaPerk p) => Veri.seviye[(int)p] + 1;

        public static bool PerkAl(MarkaPerk p)
        {
            var v = Veri;
            int lv = v.seviye[(int)p];
            if (lv >= PerkMaks[(int)p] || v.puan < lv + 1) return false;
            v.puan -= lv + 1;
            v.seviye[(int)p] = lv + 1;
            Kaydet();
            return true;
        }

        // ------------------------------------------------------------------ satış

        /// <summary>
        /// Şirketin değeri: son 7 günün ortalama net kârının 90 katı + serbest kasa + sözleşmeli dükkanlar + açık bölgeler,
        /// eksi kalan kredi (alıcı borcu da devralır).
        /// </summary>
        public static long SirketDegeri
        {
            get
            {
                var e = EconomyManager.I;
                if (e == null) return 0;
                double net = 0; int n = 0;
                var h = StatsHistory.I != null ? StatsHistory.I.Days : null;
                if (h != null) for (int i = Mathf.Max(0, h.Count - 7); i < h.Count; i++) { net += h[i].IsletmeNet; n++; }
                long ort = n > 0 ? (long)(net / n) : 0;
                long d = Math.Max(0L, ort) * 90 + Math.Max(0L, e.FreeCash);
                if (CityMap.I != null)
                {
                    foreach (var s in CityMap.I.Shops) if (s.contracted && !s.own) d += 100000;
                    d += (CityMap.I.UnlockLevel - 1) * 400000L;
                }
                if (FamilyLoan.I != null) d -= FamilyLoan.I.Remaining;
                return Math.Max(0L, d);
            }
        }

        /// <summary>Kurucu payı (yatırım turlarında küçülür). Satış puanı şirket değerinin sana düşen kısmından.</summary>
        public static float Pay => Yatirim.I != null ? Yatirim.I.pay : 1f;
        public static long PayinDegeri => (long)(SirketDegeri * Pay);

        public static int PuanFor(long deger) => deger <= 0 ? 0 : Mathf.FloorToInt(Mathf.Sqrt(deger / (float)PuanBirimi));

        /// <summary>Bir sonraki puan için gereken değer.</summary>
        public static long SonrakiPuanDegeri(int puan) => (long)(puan + 1) * (puan + 1) * PuanBirimi;

        /// <summary>Satış neden yapılamıyor (boşsa yapılabilir).</summary>
        public static string SatisEngeli
        {
            get
            {
                if (TestModu.Aktif) return "otomatik testte kapalı";
                if (Senaryo.Secili != Senaryo.Tur.Klasik) return "sadece Klasik oyunda";
                var c = GameClock.I;
                if (c != null && c.CompanyDay < EnErkenSatisGunu) return $"şirket en az {EnErkenSatisGunu} günlük olmalı ({c.CompanyDay}. gün)";
                if (PuanFor(PayinDegeri) < 1) return Pay < 0.999f ? $"payının değeri en az {GameData.Ft(PuanBirimi)} olmalı" : $"şirketin değeri en az {GameData.Ft(PuanBirimi)} olmalı";
                return "";
            }
        }

        /// <summary>Şirketi satar: puan kalıcı dosyaya yazılır, bu kayıt yuvası silinir (aynı şirket ikinci kez satılamasın).</summary>
        public static int Sat()
        {
            if (SatisEngeli.Length > 0) return 0;
            long deger = SirketDegeri;
            int p = PuanFor((long)(deger * Pay));
            var v = Veri;
            v.puan += p;
            v.toplamPuan += p;
            v.satilan++;
            v.enBuyukSatis = Math.Max(v.enBuyukSatis, deger);
            v.sonSirket = $"{CompanyProfile.Name} ({SehirAdi}, {GameData.Ft(deger)})";
            Kaydet();
            SaveSystem.DeleteSlot(SaveSystem.Slot);
            return p;
        }

        public static string Ozet =>
            $"{Veri.puan} marka puanı · {Veri.satilan} şirket sattın · komisyon +%{(GelirCarpani - 1f) * 100f:0} kalıcı";
    }
}
