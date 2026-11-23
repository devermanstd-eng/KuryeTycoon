using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KuryeTycoon
{
    [Serializable]
    public class BasarimVeri
    {
        public List<string> id = new List<string>();
        public List<string> tarih = new List<string>();
    }

    public class BasarimTanimi
    {
        public string id;            // Steam başarım kimliğiyle aynı (ASCII, büyük harf)
        public string ad, aciklama, kategori;
        public Func<bool> kosul;
        public Func<float> ilerleme;  // 0–1 (yoksa null: ya hep ya hiç)
        public Func<string> ilerlemeMetni;
    }

    /// <summary>
    /// Başarımlar (Hedefler > Başarımlar): oyunlar arası kalıcı (marka gibi persistentDataPath/basarimlar.json), şirket satılsa da
    /// silinmez. Saniyede bir kontrol edilir; açılınca bildirim gelir. Incremental oyunlarda oyuncuyu geri getiren uzun vadeli
    /// hedefler: 100 000 teslimat, 1 milyon € şirket değeri, Budapeşte, kusursuz gün...
    ///  • Otomatik testte dosyaya yazılmaz ve Steam'e gitmez (sadece o test için bellekte sayılır, özette görünür).
    ///  • Steam: proje Steamworks.NET ile kurulup Scripting Define Symbols'a KURYE_STEAM eklenince her başarım aynı kimlikle
    ///    SteamUserStats.SetAchievement ile açılır (Steamworks panelinde aynı kimlikler tanımlanmalı; liste KURULUM.md'de).
    /// </summary>
    public class Basarimlar : MonoBehaviour
    {
        public static Basarimlar I { get; private set; }

        public static readonly List<BasarimTanimi> Tanimlar = new List<BasarimTanimi>();
        static BasarimVeri veri;
        static readonly Dictionary<string, string> acik = new Dictionary<string, string>();
        static readonly HashSet<string> testteAcilan = new HashSet<string>();
        float zamanlayici;

        static string Dosya => Path.Combine(Application.persistentDataPath, "basarimlar.json");
        static bool Test => TestModu.Aktif;

        public static int AcikSayisi
        {
            get
            {
                Yukle();
                int n = 0;
                foreach (var t in Tanimlar) if (AcikMi(t.id)) n++;
                return n;
            }
        }

        public static bool AcikMi(string id) => Test ? testteAcilan.Contains(id) : acik.ContainsKey(id);
        public static string Tarih(string id) => !Test && acik.TryGetValue(id, out var t) ? t : "";

        void Awake()
        {
            I = this;
            if (Tanimlar.Count == 0) Tanimla();
            Yukle();
        }

        static void Yukle()
        {
            if (veri != null) return;
            veri = null;
            try { if (File.Exists(Dosya)) veri = JsonUtility.FromJson<BasarimVeri>(File.ReadAllText(Dosya)); }
            catch (Exception e) { Debug.LogWarning("Başarım dosyası okunamadı: " + e.Message); }
            if (veri == null) veri = new BasarimVeri();
            acik.Clear();
            for (int i = 0; i < veri.id.Count; i++) acik[veri.id[i]] = i < veri.tarih.Count ? veri.tarih[i] : "";
        }

        static void Kaydet()
        {
            try
            {
                veri.id.Clear(); veri.tarih.Clear();
                foreach (var kv in acik) { veri.id.Add(kv.Key); veri.tarih.Add(kv.Value); }
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(Dosya, JsonUtility.ToJson(veri, true));
            }
            catch (Exception e) { Debug.LogWarning("Başarım dosyası yazılamadı: " + e.Message); }
        }

        /// <summary>Yeni test başlarken testte açılanlar sıfırlanır.</summary>
        public static void TestiSifirla() => testteAcilan.Clear();

        void Update()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || GameClock.I == null) return;
            zamanlayici -= Time.unscaledDeltaTime;
            if (zamanlayici > 0f) return;
            zamanlayici = 1f;
            foreach (var t in Tanimlar)
            {
                if (AcikMi(t.id)) continue;
                bool ok;
                try { ok = t.kosul(); }
                catch { ok = false; }   // sistem henüz kurulmadıysa
                if (ok) Ac(t);
            }
        }

        static void Ac(BasarimTanimi t)
        {
            if (Test) testteAcilan.Add(t.id);
            else
            {
                acik[t.id] = GameClock.I != null ? $"{GameClock.I.DateText} ({CompanyProfile.Name})" : DateTime.Now.ToString("yyyy-MM-dd");
                Kaydet();
                SteamAc(t.id);
            }
            Telemetry.Add("Başarım: " + t.id);
            if (EventManager.I != null) EventManager.I.Notify($"Başarım açıldı: {t.ad}. {t.aciklama}");
            GameSounds.Play(Sfx.Iyi);
        }

        static void SteamAc(string id)
        {
#if KURYE_STEAM
            try
            {
                Steamworks.SteamUserStats.SetAchievement(id);
                Steamworks.SteamUserStats.StoreStats();
            }
            catch (Exception e) { Debug.LogWarning("Steam başarımı açılamadı: " + e.Message); }
#endif
        }

        // ------------------------------------------------------------------ tanımlar

        static long Teslimat => Milestones.TotalDeliveries;
        static int Kurye => CourierManager.I != null ? CourierManager.I.Couriers.Count : 0;
        static long Kasa => EconomyManager.I != null ? EconomyManager.I.money : 0;
        static DayRecord SonGun => StatsHistory.I != null && StatsHistory.I.Days.Count > 0 ? StatsHistory.I.Days[StatsHistory.I.Days.Count - 1] : null;
        static long EUR(long euro) => euro * 400;

        static int Dukkan(Func<Shop, bool> f)
        {
            int n = 0;
            if (CityMap.I != null) foreach (var s in CityMap.I.Shops) if (s.contracted && !s.own && !s.kargo && f(s)) n++;
            return n;
        }

        static int KucukDugumToplam
        {
            get { int n = 0; foreach (var d in GelisimAgaci.Dugumler) if (!d.buyuk && d.id != GelisimAgaci.Merkez) n++; return n; }
        }

        static void B(string id, string kat, string ad, string aciklama, Func<bool> kosul, Func<float> ilerleme = null, Func<string> metin = null) =>
            Tanimlar.Add(new BasarimTanimi { id = id, kategori = kat, ad = ad, aciklama = aciklama, kosul = kosul, ilerleme = ilerleme, ilerlemeMetni = metin });

        static void Sayi(string id, string kat, string ad, string aciklama, Func<double> deger, double hedef, Func<double, string> bicim = null)
        {
            bicim = bicim ?? (v => v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' '));
            B(id, kat, ad, aciklama, () => deger() >= hedef, () => (float)Math.Min(1.0, deger() / hedef), () => $"{bicim(Math.Min(deger(), hedef))} / {bicim(hedef)}");
        }

        static string Euro(double ft) => GameData.Ft((long)ft);

        static void Tanimla()
        {
            const string T = "Teslimat", E = "Ekip", D = "Dükkanlar", P = "Para ve büyüme", K = "Kalite", Y = "Yeni iş kolları", S = "Şirket ve şehirler";
            Sayi("TESLIMAT_1", T, "İlk paket", "İlk teslimatını yap.", () => Teslimat, 1);
            Sayi("TESLIMAT_1000", T, "Bin kapı", "1 000 teslimat.", () => Teslimat, 1000);
            Sayi("TESLIMAT_10000", T, "Şehrin kuryesi", "10 000 teslimat.", () => Teslimat, 10000);
            Sayi("TESLIMAT_50000", T, "Elli bin", "50 000 teslimat.", () => Teslimat, 50000);
            Sayi("TESLIMAT_100000", T, "Yüz bin kapı", "100 000 teslimat (oyunlar arası değil, tek şirkette).", () => Teslimat, 100000);

            Sayi("KURYE_10", E, "Küçük ekip", "10 kurye.", () => Kurye, 10);
            Sayi("KURYE_50", E, "Filo", "50 kurye.", () => Kurye, 50);
            Sayi("KURYE_100", E, "Yüz kişilik filo", "100 kurye.", () => Kurye, 100);
            B("YASA_KARMA", E, "Adil model", "Platform yasasında karma modeli seç: tam zamanlı kuryeler çalışan olsun.", () => CrisisManager.I != null && CrisisManager.I.karma);
            Sayi("CALISAN_50", E, "Güvenceli iş", "50 kurye çalışan statüsünde.", () => CrisisManager.CalisanSayisi(), 50);

            Sayi("DUKKAN_10", D, "Mahallenin uygulaması", "10 sözleşmeli dükkan.", () => Dukkan(s => true), 10);
            Sayi("DUKKAN_20", D, "Şehrin uygulaması", "20 sözleşmeli dükkan.", () => Dukkan(s => true), 20);
            Sayi("MUNHASIR_5", D, "Sadece biz", "5 münhasır dükkan.", () => Dukkan(s => s.exclusive), 5);
            B("SADAKAT_ORTAK", D, "Gerçek ortaklık", "Bir dükkanın sadakati Ortak seviyesine ulaşsın.", () => Dukkan(s => DukkanSadakati.Seviye(s) >= 3) > 0,
              () => { float m = 0f; if (CityMap.I != null) foreach (var s in CityMap.I.Shops) if (s.contracted && !s.own && !s.kargo) m = Mathf.Max(m, s.sadakat); return m / 80f; },
              () => { float m = 0f; if (CityMap.I != null) foreach (var s in CityMap.I.Shops) if (s.contracted && !s.own && !s.kargo) m = Mathf.Max(m, s.sadakat); return $"en sadık dükkan {Mathf.Min(m, 80f):0} / 80"; });
            Sayi("ORTAK_5", D, "Sadık çevre", "5 dükkan Ortak seviyesinde.", () => Dukkan(s => DukkanSadakati.Seviye(s) >= 3), 5);

            Sayi("KASA_10K", P, "İlk birikim", "Kasada 10 000 €.", () => Kasa, EUR(10000), Euro);
            Sayi("KASA_100K", P, "Sağlam kasa", "Kasada 100 000 €.", () => Kasa, EUR(100000), Euro);
            Sayi("DEGER_1M", P, "Milyonluk girişim", "Şirket değeri 1 000 000 €.", () => Marka.SirketDegeri, EUR(1000000), Euro);
            B("BORC_BITTI", P, "Aileye borç yok", "Aile kredisini tamamen geri öde.", () => FamilyLoan.I != null && FamilyLoan.I.Finished && !FamilyLoan.I.Failed && FamilyLoan.I.Toplam > 0);
            Sayi("YATIRIM_TOHUM", P, "Tohum", "İlk yatırım turunu (Tohum) kabul et.", () => Yatirim.I != null ? Yatirim.I.tamamlanan : 0, 1);
            Sayi("YATIRIM_C", P, "C serisi", "Dört yatırım turunun hepsini al.", () => Yatirim.I != null ? Yatirim.I.tamamlanan : 0, 4);
            Sayi("PAY_50", P, "Pazar lideri", "Pazar payın %50'yi geçsin.", () => RivalCompany.I != null ? RivalCompany.I.MarketShare * 100f : 0f, 50, v => $"%{v:0}");
            Sayi("PAY_70", P, "Rakipsiz", "Pazar payın %70'i geçsin.", () => RivalCompany.I != null ? RivalCompany.I.MarketShare * 100f : 0f, 70, v => $"%{v:0}");

            B("KUSURSUZ_GUN", K, "Kusursuz gün", "En az 200 teslimatlı bir günü hiç iptal ve kaçan sipariş olmadan bitir.",
              () => { var g = SonGun; return g != null && g.deliveries >= 200 && g.cancelled + g.lost == 0; });
            B("HIZLI_GUN", K, "Şimşek", "En az 150 teslimatlı bir günde ortalama teslim süresi 28 dakikanın altında kalsın.",
              () => { var g = SonGun; return g != null && g.deliveries >= 150 && g.avgMinutes > 0f && g.avgMinutes < 28f; });
            B("GEC_YOK", K, "Dakik", "En az 100 teslimatlı bir günü tek bir geç teslimat olmadan bitir.",
              () => { var g = SonGun; return g != null && g.deliveries >= 100 && g.late == 0; });
            Sayi("ITIBAR_45", K, "Sevilen marka", "İtibar 4,5.", () => CompanyProfile.Reputation, 4.5, v => v.ToString("0.00"));
            Sayi("ITIBAR_48", K, "Beş yıldız", "İtibar 4,8.", () => CompanyProfile.Reputation, 4.8, v => v.ToString("0.00"));

            Sayi("ABONE_1000", Y, "Bin üye", "Aboneliğinde 1 000 üye.", () => Abonelik.I != null ? Abonelik.I.Abone : 0, 1000);
            Sayi("KARGO_1000", Y, "Kargo dağıtıcısı", "1 000 kargo paketi teslim et.", () => KargoAnlasmasi.I != null ? KargoAnlasmasi.I.toplamTeslim : 0, 1000);
            Sayi("ISLETME_500", Y, "Şehrin lojistiği", "İşletmelere 500 paket teslim et.", () => IsletmeTeslimati.I != null ? IsletmeTeslimati.I.toplamTeslim : 0, 500);
            Sayi("OTOMASYON_3", Y, "Kendi kendine işleyen", "Operasyon otomasyonunu en üst seviyeye çıkar.", () => Otomasyon.Seviye, 3);
            Sayi("AGAC_TAM", Y, "Bütün dallar", "Gelişim ağacındaki bütün küçük düğümleri al.", () => GelisimAgaci.AlinanSayisi, Math.Max(1, KucukDugumToplam));

            Sayi("YIL_1", S, "Bir yıl", "Şirketini bir yıl ayakta tut.", () => GameClock.I != null ? GameClock.I.CompanyDay : 0, 365);
            Sayi("SATIS_1", S, "Exit", "İlk şirketini sat ve yeni şehre geç.", () => Marka.Veri.satilan, 1);
            Sayi("SATIS_3", S, "Seri girişimci", "Üç şirket sat.", () => Marka.Veri.satilan, 3);
            B("BUDAPESTE", S, "Başkent", "Budapeşte'de şirket kur.", () => Marka.AktifSehir >= Marka.Sehirler.Length - 1);
        }
    }
}
