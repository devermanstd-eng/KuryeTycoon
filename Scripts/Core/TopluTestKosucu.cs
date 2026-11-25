using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Toplu simülasyon: akıllı test botunu (TestModu + TestModuAkilli) arka arkaya birçok koşuda çalıştırır,
    /// sonuçları tek bir toplu_ozet.txt'ye ve koşu başına gunluk_*.csv'ye yazar.
    ///
    /// Başlatma: Editor/TopluTest.cs → -executeMethod KuryeTycoon.EditorTools.TopluTest.Calistir (Tools/toplu_test.sh).
    /// Play moduna girerken domain yeniden yüklendiği için editördeki statik bilgi kaybolur; bu yüzden koşucu
    /// komut satırına bakarak kendini kurar (o argüman yoksa hiçbir şey yapmaz, normal oyuna dokunmaz).
    ///
    /// Koşular:
    ///   • 5 sabit şehir tohumu, Klasik, her biri 60 gün (-topluGun ile değişir; Serbest de bu süreyle koşar)
    ///   • Senaryolar (Senaryo.Tur): Borç batağı, Rakiplerin şehri, Hızlı büyüme, Serbest, Günlük meydan okuma —
    ///     her biri kendi süresiyle; hedef tutar ya da kaybedilirse koşu o gün biter.
    ///     Klasik senaryo ayrıca koşulmaz: 1. tohum koşusunun birebir aynısı olurdu (aynı şehir, aynı bot tohumu, aynı süre).
    ///
    /// Komut satırı (hepsi isteğe bağlı):
    ///   -topluCikti &lt;klasör&gt;   rapor klasörü (yoksa persistentDataPath/test_raporlari/toplu_TARİH)
    ///   -topluHiz &lt;16|32|64|128&gt; oyun hızı (varsayılan 64; denge onayı için 32 önerilir)
    ///   -topluSec &lt;hepsi|tohum|senaryo&gt;
    ///   -topluGun &lt;10–365&gt;       Klasik tohum koşularının ve Serbest'in süresi (varsayılan 60; uzun vade dengesi için 120)
    /// </summary>
    public class TopluTestKosucu : MonoBehaviour
    {
        public const string MetotAdi = "TopluTest.Calistir";

        /// <summary>Bütün koşular bitti, rapor yazıldı. Editör tarafı (TopluTestIzleyici) bunu görünce Unity'yi kapatır.</summary>
        public static bool Bitti { get; private set; }
        /// <summary>0: hepsi tamam ve hata yok · 1: hata var ya da koşu tamamlanamadı.</summary>
        public static int CikisKodu { get; private set; } = 1;
        public static string RaporKlasoru { get; private set; } = "";

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // Denge hedefleri (CLAUDE.md: Test ve Denge Doğrulama)
        const float PayAlt = 0.45f, PayUst = 0.55f;
        const int PaketBeklemeUst = 13;
        /// <summary>22–60 hesaplık paketler bundan kısa sürede alınırsa oyun fazla hızlı (24.11: 1–8 gün; hedef 9–12).</summary>
        const int PaketBeklemeAlt = 9;

        public static readonly int[] SehirTohumlari = { TestModu.SabitHaritaTohumu, 123456789, 246813579, 975318642, 555000111 };
        const int VarsayilanKlasikGun = 60;
        /// <summary>Pazar payı seyrinde ara noktalar (her 30 günde bir).</summary>
        const int SeyirAraligi = 30;
        /// <summary>Borç batağı: son taksit Temmuz sonunda (1 Mart + 153 gün); sonucu ertesi gece yazılır.</summary>
        const int BorcBatagiGun = 155;

        class Kosu
        {
            public string ad;
            public Senaryo.Tur senaryo;
            public int haritaTohumu, hedefGun;

            // sonuçlar
            public bool tamamlandi;
            public string bitis = "-", sonuc = "-", klasor = "";
            public int gun, hata, uyari, kurye, kuryeSiniri, dukkan, iflasGunu = -1, kapanisGunu = -1, teslimat, iptal, kacan;
            public long kasaBaslangic, kasaBitis, enDusukKasa, krediKalan;
            public float pay = -1f, itibar, atamaBekleme, almaBekleme, gercekDk;
            public string paketBekleme = "-";
            public bool payBandda = true, paketBandda = true;
            public List<KeyValuePair<string, int>> hatalar = new List<KeyValuePair<string, int>>();
            /// <summary>Gün sonlarında okunan pazar payı (gün, pay). Günlük kayıtta pay yok; koşu sırasında toplanır.</summary>
            public readonly List<KeyValuePair<int, float>> paySeyri = new List<KeyValuePair<int, float>>();
            public string paySeyriMetni = "-", bolgeGecisleri = "-";
            /// <summary>En uzun kesintisiz "ölü" dönem: hiçbir geliştirme, bölge ya da kurye paketi alınmayan gün sayısı ve başladığı gün.</summary>
            public int oluEnUzun, oluBas, oluSeri, oluSeriBas;
            public long oluImza = -1;
        }

        // ------------------------------------------------------------------ kurulum

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Kendiliginden()
        {
            if (!Istendi()) return;
            var go = new GameObject("Toplu Test");
            DontDestroyOnLoad(go);
            go.AddComponent<TopluTestKosucu>();
        }

        static bool Istendi()
        {
            foreach (var a in System.Environment.GetCommandLineArgs())
                if (a != null && a.EndsWith(MetotAdi, System.StringComparison.Ordinal)) return true;
            return false;
        }

        static string Arg(string ad, string varsayilan)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (string.Equals(a[i], ad, System.StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return varsayilan;
        }

        readonly List<Kosu> kosular = new List<Kosu>();
        float hiz;
        int klasikGun = VarsayilanKlasikGun;
        int toplamHata, kosuIciHata;
        float baslangicGercek;

        void Awake()
        {
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
        }

        void OnDestroy() { Application.logMessageReceived -= OnLog; }

        void OnLog(string m, string y, LogType t)
        {
            if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) toplamHata++;
        }

        void Start()
        {
            hiz = float.TryParse(Arg("-topluHiz", "64"), NumberStyles.Float, Inv, out float h) ? Mathf.Clamp(h, 1f, 128f) : 64f;
            string sec = Arg("-topluSec", "hepsi").ToLowerInvariant();
            klasikGun = int.TryParse(Arg("-topluGun", VarsayilanKlasikGun.ToString(Inv)), NumberStyles.Integer, Inv, out int g)
                ? Mathf.Clamp(g, 10, 365) : VarsayilanKlasikGun;
            RaporKlasoru = Arg("-topluCikti", Path.Combine(Application.persistentDataPath, "test_raporlari", $"toplu_{System.DateTime.Now:yyyyMMdd_HHmmss}"));
            Directory.CreateDirectory(RaporKlasoru);

            if (sec != "senaryo")
                for (int i = 0; i < SehirTohumlari.Length; i++)
                    kosular.Add(new Kosu { ad = $"tohum{i + 1}", senaryo = Senaryo.Tur.Klasik, haritaTohumu = SehirTohumlari[i], hedefGun = klasikGun });
            if (sec != "tohum")
            {
                Ekle(Senaryo.Tur.BorcBatagi, BorcBatagiGun);
                Ekle(Senaryo.Tur.RakibinSehri, Senaryo.RakipGun + 1);
                Ekle(Senaryo.Tur.HizliBuyume, Senaryo.BuyumeGun + 1);
                Ekle(Senaryo.Tur.Serbest, klasikGun);
                Ekle(Senaryo.Tur.GunlukMeydan, Senaryo.GunlukGun + 1);
            }

            Log($"{kosular.Count} koşu, hız {hiz}×, Klasik {klasikGun} gün, rapor: {RaporKlasoru}");
            StartCoroutine(Calis());
        }

        void Ekle(Senaryo.Tur t, int gun) =>
            kosular.Add(new Kosu { ad = "senaryo_" + DosyaAdi(Senaryo.Adlar[(int)t]), senaryo = t, haritaTohumu = TestModu.SabitHaritaTohumu, hedefGun = gun });

        static void Log(string m) => Debug.Log("[TopluTest] " + m);

        // ------------------------------------------------------------------ koşular

        IEnumerator Calis()
        {
            baslangicGercek = Time.realtimeSinceStartup;
            for (int i = 0; i < kosular.Count; i++)
            {
                var k = kosular[i];
                Log($"{i + 1}/{kosular.Count} başlıyor: {k.ad} ({Senaryo.Adlar[(int)k.senaryo]}, tohum {k.haritaTohumu}, en fazla {k.hedefGun} gün)");
                yield return StartCoroutine(Kos(k));
                Log($"{i + 1}/{kosular.Count} bitti: {k.ad} · {k.sonuc} · hata {k.hata} · kasa {GameData.Ft(k.kasaBitis)} · " +
                    $"pay {(k.pay >= 0f ? $"%{k.pay * 100f:0}" : "-")} · itibar {k.itibar:0.00} · {k.gercekDk:0.0} dk");
                try { File.WriteAllText(Path.Combine(RaporKlasoru, "toplu_ozet.txt"), Ozet(i + 1), new UTF8Encoding(false)); }   // ara kayıt: yarıda kalırsa da okunur
                catch (System.Exception ex) { Debug.LogWarning("[TopluTest] ara özet yazılamadı: " + ex.Message); }

                GameBootstrap.Restart();
                for (int f = 0; f < 5; f++) yield return null;
            }

            TestModu.OzelHaritaTohumu = TestModu.OzelBotTohumu = null;
            TestModu.Ekransiz = false;
            Senaryo.Secili = Senaryo.Tur.Klasik;

            bool hepsiTamam = kosular.TrueForAll(k => k.tamamlandi);
            int disHata = Mathf.Max(0, toplamHata - kosuIciHata);
            CikisKodu = hepsiTamam && toplamHata == 0 ? 0 : 1;
            try { File.WriteAllText(Path.Combine(RaporKlasoru, "toplu_ozet.txt"), Ozet(kosular.Count), new UTF8Encoding(false)); }
            catch (System.Exception ex) { Debug.LogError("[TopluTest] toplu_ozet.txt yazılamadı: " + ex.Message); CikisKodu = 1; }
            Log($"BİTTİ · çıkış kodu {CikisKodu} · koşularda {kosuIciHata} hata, koşu dışında {disHata} hata · rapor: {RaporKlasoru}");
            Bitti = true;
        }

        IEnumerator Kos(Kosu k)
        {
            float t0 = Time.realtimeSinceStartup;

            // Kurulum ekranı hazır olsun (Restart'tan sonra yeni GameBootstrap)
            float bekle = 0f;
            while ((GameBootstrap.I == null || GameBootstrap.I.Started) && bekle < 30f) { bekle += Time.unscaledDeltaTime; yield return null; }
            yield return null; yield return null;
            if (GameBootstrap.I == null || GameBootstrap.I.Started) { k.bitis = "oyun kurulamadı"; k.sonuc = "HATA"; yield break; }

            Senaryo.Secili = k.senaryo;
            TestModu.Ekransiz = true;
            TestModu.Tur = TestModu.BotTuru.Akilli;
            TestModu.SabitTohum = true;
            TestModu.HedefGun = k.hedefGun;
            TestModu.Hiz = hiz;
            TestModu.OzelHaritaTohumu = k.haritaTohumu;
            TestModu.OzelBotTohumu = TestModu.SabitBotTohumu;

            try { TestModu.Baslat(); }
            catch (System.Exception ex) { Debug.LogException(ex); }
            var tm = TestModu.I;
            if (tm == null || !GameBootstrap.I.Started) { k.bitis = "test başlatılamadı"; k.sonuc = "HATA"; yield break; }
            k.klasor = tm.Klasor;
            k.kasaBaslangic = tm.BaslangicKasa;

            // Gerçek zaman sınırı: beklenen sürenin 3 katı + 2 dk (oyun saati takılırsa koşu sonsuza kadar sürmesin)
            float dakikaSn = Mathf.Max(0.01f, GameClock.I.minutesPerSecond * hiz);
            float sinir = k.hedefGun * 1440f / dakikaSn * 3f + 120f;

            int sonGun = 0;
            while (TestModu.I == tm && !tm.RaporHazir)
            {
                // Gün sonu kaydı eklenince o günün pazar payını al (gunluk.csv'de de var; özet için burada tutulur)
                var sh = StatsHistory.I;
                if (sh != null && sh.Days.Count > sonGun)
                {
                    sonGun = sh.Days.Count;
                    if (RivalCompany.I != null) k.paySeyri.Add(new KeyValuePair<int, float>(sonGun, RivalCompany.I.MarketShare));
                    OluGun(k, sonGun);
                }
                if (Senaryo.HedefVar && Senaryo.Durum != 0 && !tm.RaporHazir)
                    tm.Bitir(Senaryo.Durum == 1 ? "senaryo kazanıldı" : "senaryo kaybedildi");
                else if (Time.realtimeSinceStartup - t0 > sinir)
                {
                    Debug.LogError($"[TopluTest] {k.ad}: zaman aşımı ({sinir / 60f:0} dk), test kesildi.");
                    tm.Bitir("zaman aşımı");
                }
                yield return null;
            }
            if (TestModu.I != tm || tm == null) { k.bitis = "test yarıda kayboldu"; k.sonuc = "HATA"; yield break; }

            Topla(k, tm);
            k.gercekDk = (Time.realtimeSinceStartup - t0) / 60f;
            kosuIciHata += k.hata;
            Kopyala(k, "gunluk.csv", $"gunluk_{k.ad}.csv");
            Kopyala(k, "ozet.txt", $"ozet_{k.ad}.txt");
        }

        void Kopyala(Kosu k, string kaynak, string hedef)
        {
            try
            {
                string yol = Path.Combine(k.klasor, kaynak);
                if (File.Exists(yol)) File.Copy(yol, Path.Combine(RaporKlasoru, hedef), true);
            }
            catch (System.Exception ex) { Debug.LogWarning($"[TopluTest] {kaynak} kopyalanamadı: {ex.Message}"); }
        }

        // ------------------------------------------------------------------ sonuçları toplama

        static void Topla(Kosu k, TestModu tm)
        {
            k.tamamlandi = tm.BitisNedeni != "zaman aşımı";
            k.bitis = tm.BitisNedeni;
            k.hata = tm.HataToplam;
            k.uyari = tm.UyariToplam;
            k.hatalar = tm.EnSikHatalar(5);
            k.iflasGunu = tm.IflasGunu;
            k.kapanisGunu = tm.KapanisGunu;
            k.enDusukKasa = tm.EnDusukKasa;
            k.kasaBitis = EconomyManager.I != null ? EconomyManager.I.money : 0;
            k.itibar = CompanyProfile.Reputation;
            k.pay = RivalCompany.I != null ? RivalCompany.I.MarketShare : -1f;
            k.kurye = CourierManager.I != null ? CourierManager.I.Couriers.Count : 0;
            k.kuryeSiniri = UpgradeManager.I != null ? UpgradeManager.I.CourierCap : 0;
            k.krediKalan = FamilyLoan.I != null ? FamilyLoan.I.Remaining : 0;
            // Sözleşmeli dükkan (kendi marketlerimiz hariç); eskiden yalnızca kendi marketleri sayılıyordu ve hep 0 çıkıyordu
            if (CityMap.I != null) foreach (var s in CityMap.I.Shops) if (s.contracted && !s.own) k.dukkan++;

            var gunler = StatsHistory.I != null ? StatsHistory.I.Days : null;
            k.gun = gunler != null ? gunler.Count : 0;
            if (gunler != null && gunler.Count > 0)
            {
                double atama = 0, alma = 0;
                foreach (var d in gunler)
                {
                    k.teslimat += d.deliveries; k.iptal += d.cancelled; k.kacan += d.lost;
                    atama += d.avgAssignWait * d.deliveries; alma += d.avgPickupWait * d.deliveries;
                }
                k.atamaBekleme = k.teslimat > 0 ? (float)(atama / k.teslimat) : 0f;
                k.almaBekleme = k.teslimat > 0 ? (float)(alma / k.teslimat) : 0f;
                PaketBekleme(k, gunler);
                BolgeGecisleri(k, gunler);
            }
            PaySeyri(k);

            // Pazar payı bandı yalnızca rakiplerin normal olduğu koşullarda anlamlı (Rakiplerin şehri ve Serbest bilerek dışında)
            k.payBandda = k.pay < 0f || k.senaryo == Senaryo.Tur.RakibinSehri || k.senaryo == Senaryo.Tur.Serbest || (k.pay >= PayAlt && k.pay <= PayUst);

            k.sonuc = Sonuc(k);
        }

        /// <summary>
        /// Kurye hesabı paketinin (tavanın) dolu kaldığı gün sayısı, TestModu.KapasiteOzeti ile aynı tanım.
        /// Sonradan geçilen paketlerde 13 günü aşan bekleme bant dışıdır; son paket test bitince hâlâ sürebilir.
        /// </summary>
        /// <summary>
        /// İlerleme imzası: bütün geliştirme seviyeleri (kurye paketi dahil) + açık bölge. Gün sonunda değişmediyse o gün
        /// "ölü" sayılır; en uzun kesintisiz seri tutulur (120 günlük tempo: oyuncunun alacak bir şey bulamadığı dönemler).
        /// </summary>
        static void OluGun(Kosu k, int gun)
        {
            long imza = CityMap.I != null ? CityMap.I.UnlockLevel * 100000L : 0;
            if (UpgradeManager.I != null)
                foreach (UpgradeKind u in System.Enum.GetValues(typeof(UpgradeKind))) imza += UpgradeManager.I.Level(u);
            imza += GelisimAgaci.AlinanSayisi * 1000L;
            if (k.oluImza >= 0 && imza == k.oluImza)
            {
                if (k.oluSeri == 0) k.oluSeriBas = gun;
                k.oluSeri++;
                if (k.oluSeri > k.oluEnUzun) { k.oluEnUzun = k.oluSeri; k.oluBas = k.oluSeriBas; }
            }
            else k.oluSeri = 0;
            k.oluImza = imza;
        }

        static void PaketBekleme(Kosu k, List<DayRecord> gunler)
        {
            var sira = new List<int>();
            var dolu = new Dictionary<int, int>();
            foreach (var d in gunler)
            {
                int t = d.hesapTavani;
                if (t <= 0 || d.couriers < t) continue;
                if (!dolu.ContainsKey(t)) { sira.Add(t); dolu[t] = 0; }
                dolu[t]++;
            }
            if (sira.Count == 0) { k.paketBekleme = "hiç dolmadı"; return; }
            int enBuyuk = gunler[gunler.Count - 1].hesapTavani;
            var parca = new List<string>();
            foreach (int t in sira)
            {
                bool suruyor = t == enBuyuk;
                bool asti = !suruyor && dolu[t] > PaketBeklemeUst;
                bool hizli = !suruyor && t >= 22 && t <= 60 && dolu[t] < PaketBeklemeAlt;   // orta oyun fazla hızlı
                if (asti || hizli) k.paketBandda = false;
                parca.Add($"{t}:{dolu[t]}g{(suruyor ? "…" : "")}{(asti ? "!" : hizli ? "↓" : "")}");
            }
            k.paketBekleme = string.Join(" ", parca);
        }

        /// <summary>
        /// Pazar payı her 30 günde ve son günde, en yüksek değer ve bandın üstüne (%55) ilk çıktığı gün.
        /// Oyun başında pay doğal olarak düşük; asıl soru geç oyunda rakiplerin ezilip ezilmediği.
        /// </summary>
        static void PaySeyri(Kosu k)
        {
            // Son gün kaydı test biterken eklenir; döngü onu görmeden çıkmış olabilir
            if (k.pay >= 0f && k.gun > 0 && (k.paySeyri.Count == 0 || k.paySeyri[k.paySeyri.Count - 1].Key < k.gun))
                k.paySeyri.Add(new KeyValuePair<int, float>(k.gun, k.pay));
            if (k.paySeyri.Count == 0) return;
            var parca = new List<string>();
            int enYuksekGun = 0, ilkUstGun = -1;
            float enYuksek = -1f;
            for (int i = 0; i < k.paySeyri.Count; i++)
            {
                var p = k.paySeyri[i];
                bool son = i == k.paySeyri.Count - 1;
                if (p.Key % SeyirAraligi == 0 || son) parca.Add($"{p.Key}. gün %{p.Value * 100f:0}");
                if (p.Value > enYuksek) { enYuksek = p.Value; enYuksekGun = p.Key; }
                if (ilkUstGun < 0 && p.Value > PayUst) ilkUstGun = p.Key;
            }
            k.paySeyriMetni = string.Join(" · ", parca) + $" · en yüksek %{enYuksek * 100f:0} ({enYuksekGun}. gün) · " +
                              (ilkUstGun > 0 ? $"%{PayUst * 100f:0} üstüne ilk {ilkUstGun}. gün" : $"%{PayUst * 100f:0} üstüne hiç çıkmadı");
        }

        /// <summary>Yeni bölge açıldığı günler (gün sonu kaydındaki bölge seviyesi).</summary>
        static void BolgeGecisleri(Kosu k, List<DayRecord> gunler)
        {
            var parca = new List<string>();
            int onceki = gunler[0].district;
            for (int i = 1; i < gunler.Count; i++)
            {
                int b = gunler[i].district;
                if (b <= onceki) continue;
                string ad = b >= 0 && b < CityMap.DistrictNames.Length ? CityMap.DistrictNames[b] : b.ToString(Inv);
                parca.Add($"{ad} {i + 1}. gün");
                onceki = b;
            }
            k.bolgeGecisleri = parca.Count > 0 ? string.Join(" · ", parca) : "yeni bölge açılmadı";
        }

        static string Sonuc(Kosu k)
        {
            if (k.bitis == "zaman aşımı") return "ZAMAN AŞIMI";
            string ek = k.iflasGunu > 0 ? $" · iflas {k.iflasGunu}. gün" : k.kapanisGunu > 0 ? $" · kapandı {k.kapanisGunu}. gün" : "";
            if (k.senaryo == Senaryo.Tur.Klasik) return $"{k.gun} gün tamam{ek}";
            if (!Senaryo.HedefVar) return $"hedef yok, {k.gun} gün{ek}";
            if (k.senaryo == Senaryo.Tur.GunlukMeydan) return (Senaryo.Durum == 1 ? $"BİTTİ · puan {Senaryo.Puan()}" : "SONUÇSUZ") + ek;
            return (Senaryo.Durum == 1 ? $"KAZANDI ({k.gun}. gün)" : Senaryo.Durum == 2 ? $"KAYBETTİ ({k.gun}. gün)" : "SONUÇSUZ (süre doldu)") + ek;
        }

        // ------------------------------------------------------------------ rapor

        string Ozet(int biten)
        {
            var sb = new StringBuilder();
            float dk = (Time.realtimeSinceStartup - baslangicGercek) / 60f;
            sb.AppendLine("KURYE TYCOON — TOPLU SİMÜLASYON ÖZETİ");
            sb.AppendLine($"Kod sürümü: {DebugReport.CodeVersion}");
            sb.AppendLine($"Tarih: {System.DateTime.Now:yyyy-MM-dd HH:mm} · Unity {Application.unityVersion} · {SystemInfo.operatingSystem} · " +
                          $"batch {(Application.isBatchMode ? "evet" : "hayır")} · grafik {SystemInfo.graphicsDeviceType}");
            sb.AppendLine($"Ayarlar: bot akıllı · hız {hiz}× · bot tohumu {TestModu.SabitBotTohumu} · ekran görüntüsü yok");
            sb.AppendLine($"Durum: {biten}/{kosular.Count} koşu bitti · gerçek süre {dk:0.0} dk · koşularda {kosuIciHata} hata, toplam {toplamHata} hata");
            sb.AppendLine($"Denge bandı: pazar payı %{PayAlt * 100f:0}–{PayUst * 100f:0} (Rakiplerin şehri ve Serbest hariç) · kurye paketi beklemesi {PaketBeklemeAlt}–{PaketBeklemeUst} gün (22–60 hesap; üstü ≤ {PaketBeklemeUst})");
            sb.AppendLine($"Not: Klasik senaryo ayrıca koşulmadı; tohum1 koşusu odur (aynı şehir, aynı bot tohumu, {klasikGun} gün).");
            sb.AppendLine();

            string Satir(params string[] h) => string.Join(" | ", h);
            sb.AppendLine(Satir("Koşu".PadRight(26), "Tohum".PadLeft(10), "Gün".PadLeft(3), "Hata".PadLeft(4),
                "Kasa (başl. → bitiş)".PadRight(30), "Pay".PadLeft(4), "İtibar", "Atama/alma dk".PadRight(13), "Paket bekleme".PadRight(18), "Sonuç"));
            sb.AppendLine(new string('-', 150));
            for (int i = 0; i < biten && i < kosular.Count; i++)
            {
                var k = kosular[i];
                string pay = k.pay >= 0f ? $"%{k.pay * 100f:0}" + (k.payBandda ? "" : "!") : "-";
                sb.AppendLine(Satir(k.ad.PadRight(26), k.haritaTohumu.ToString(Inv).PadLeft(10), k.gun.ToString(Inv).PadLeft(3), k.hata.ToString(Inv).PadLeft(4),
                    $"{GameData.Ft(k.kasaBaslangic)} → {GameData.Ft(k.kasaBitis)}".PadRight(30), pay.PadLeft(4), $"{k.itibar:0.00}".PadLeft(6),
                    $"{k.atamaBekleme:0.0} / {k.almaBekleme:0.0}".PadRight(13), k.paketBekleme.PadRight(18), k.sonuc));
            }
            sb.AppendLine($"(Pay sonunda '!' = bant dışı · paket bekleme 'hesap:gün', '…' = test bitince sürüyordu, '!' = {PaketBeklemeUst} günü aştı, " +
                          $"'↓' = 22–60 hesapta {PaketBeklemeAlt} günden kısa: fazla hızlı)");
            var oluListe = new List<string>();
            foreach (var ko in kosular)
                if (ko.gun > 0) oluListe.Add($"{ko.ad} {ko.oluEnUzun} g" + (ko.oluEnUzun > 0 ? $" ({ko.oluBas}.–{ko.oluBas + ko.oluEnUzun - 1}.)" : ""));
            if (oluListe.Count > 0) sb.AppendLine("En uzun ölü dönem (hiçbir geliştirme, bölge, paket, ağaç düğümü alınmayan gün): " + string.Join(" · ", oluListe));

            var bantDisi = new List<string>();
            var hatali = new List<string>();
            for (int i = 0; i < biten && i < kosular.Count; i++)
            {
                var k = kosular[i];
                if (!k.payBandda) bantDisi.Add($"{k.ad}: pazar payı %{k.pay * 100f:0}");
                if (!k.paketBandda) bantDisi.Add($"{k.ad}: kurye paketi beklemesi {k.paketBekleme}");
                if (k.hata > 0 || !k.tamamlandi) hatali.Add(k.ad);
            }
            sb.AppendLine();
            sb.AppendLine("Denge: " + (bantDisi.Count == 0 ? "bütün koşular bantta." : "BANT DIŞI → " + string.Join(" · ", bantDisi)));
            sb.AppendLine("Hatalar: " + (hatali.Count == 0 ? "hata yok." : "HATALI KOŞU → " + string.Join(", ", hatali)));

            sb.AppendLine();
            sb.AppendLine("==================== AYRINTI ====================");
            for (int i = 0; i < biten && i < kosular.Count; i++)
            {
                var k = kosular[i];
                sb.AppendLine();
                sb.AppendLine($"--- {k.ad} · {Senaryo.Adlar[(int)k.senaryo]} · tohum {k.haritaTohumu} · en fazla {k.hedefGun} gün");
                sb.AppendLine($"Bitiş: {k.bitis} · sonuç: {k.sonuc} · {k.gun} tam gün · gerçek {k.gercekDk:0.0} dk");
                sb.AppendLine($"Kasa: {GameData.Ft(k.kasaBaslangic)} → {GameData.Ft(k.kasaBitis)} (en düşük {GameData.Ft(k.enDusukKasa)}) · kredi kalan {GameData.Ft(k.krediKalan)}");
                sb.AppendLine($"Pazar payı: {(k.pay >= 0f ? $"%{k.pay * 100f:0.0}" : "-")} · itibar {k.itibar:0.00} · kurye {k.kurye}/{k.kuryeSiniri} · sözleşmeli dükkan {k.dukkan}");
                sb.AppendLine($"Teslimat {k.teslimat} · iptal {k.iptal} · kaçan {k.kacan} · ort. atama bekleme {k.atamaBekleme:0.0} dk · ort. alma bekleme {k.almaBekleme:0.0} dk");
                sb.AppendLine($"Pazar payı seyri: {k.paySeyriMetni}");
                sb.AppendLine(k.oluEnUzun > 0
                    ? $"En uzun ölü dönem: {k.oluEnUzun} gün ({k.oluBas}–{k.oluBas + k.oluEnUzun - 1}. gün): geliştirme, bölge ya da kurye paketi alınmadı"
                    : "En uzun ölü dönem: yok (her gün bir şey alındı)");
                sb.AppendLine($"Bölge açılışları: {k.bolgeGecisleri}");
                sb.AppendLine($"Kurye paketi beklemesi: {k.paketBekleme}");
                sb.AppendLine($"Hata {k.hata} · uyarı {k.uyari}");
                foreach (var h in k.hatalar) sb.AppendLine($"    {h.Value,4}× {h.Key}");
                sb.AppendLine($"Dosyalar: gunluk_{k.ad}.csv · ozet_{k.ad}.txt · tam rapor {k.klasor}");
            }
            return sb.ToString();
        }

        static string DosyaAdi(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in s.ToLowerInvariant())
            {
                char c = ch switch { 'ç' => 'c', 'ğ' => 'g', 'ı' => 'i', 'ö' => 'o', 'ş' => 's', 'ü' => 'u', _ => ch };
                if (c >= 'a' && c <= 'z' || c >= '0' && c <= '9') sb.Append(c);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
            }
            return sb.ToString().Trim('_');
        }
    }
}
