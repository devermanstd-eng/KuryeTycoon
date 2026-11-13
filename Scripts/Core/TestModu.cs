using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Otomatik test: bir bot yeni bir oyunu yüksek hızda oynar ve olan her şeyi dosyaya yazar.
    /// Kurulum ekranındaki "Geliştirici: otomatik test" bölümünden başlar (yalnızca Editor'de ve geliştirme sürümünde görünür).
    ///
    /// Bot ne yapar (oyuncunun yapabildiklerini, menüler açıldıkça):
    ///   • Karar kartlarını cevaplar (ilk seçenek ya da rastgele; "Yeniden başla" ve "raporları kapat" hiç seçilmez)
    ///   • Aile borcu taksitini vadesinde öder
    ///   • İptal / bekleme artınca kurye alır, kurye yetince yeni dükkanla sözleşme yapar, dükkan kalmayınca bölge açar
    ///   • Kasa izin verince geliştirme ve destek elemanı alır
    ///
    /// Yazdıkları (kayıt klasöründe test_raporlari/test_TARİH, sonunda aynı adla .zip):
    ///   ozet.txt              en önemli sonuçlar, haftalık tablo, kartlar, en sık bildirimler, hatalar, FPS
    ///   gunluk.csv            her gün sonu: teslimat, iptal, gelir, kasa, itibar, kurye, moral, FPS...
    ///   kartlar.txt           her karar kartının metni, seçenekleri ve botun seçimi
    ///   bildirimler.txt       bütün bildirimler (oyun saatiyle)
    ///   bot.txt               botun yaptığı her şey ve nedeni
    ///   hatalar.txt           Unity hataları (yığın izleriyle) ve en sık uyarılar
    ///   hata_ayiklama_raporu.txt  F9 raporunun aynısı (test sonunda)
    ///   ekran/                pencerelerin, kurye telefonunun, ilk kartların ve 10 günde bir haritanın görüntüleri
    ///
    /// Test sırasında kayıt kapalıdır: oyuncunun kayıtlarına dokunulmaz.
    /// </summary>
    public partial class TestModu : MonoBehaviour
    {
        // ------------------------------------------------------------------ ayarlar (kurulum ekranından)
        public static readonly int[] GunSecenekleri = { 7, 30, 60, 120 };
        public static readonly float[] HizSecenekleri = { 16f, 32f, 64f, 128f };
        public static int HedefGun = 30;
        public static float Hiz = 32f;
        /// <summary>Akıllı: kartları okuyup oyuncu gibi seçer ve bütün menüleri kullanır (TestModuAkilli.cs). Diğer ikisi basit bot.</summary>
        public enum BotTuru { Akilli, IlkSecenek, Rastgele }
        public static BotTuru Tur = BotTuru.Akilli;
        public static string TurAdi(BotTuru t) => t == BotTuru.Akilli ? "akıllı oyuncu" : t == BotTuru.Rastgele ? "rastgele seçim" : "hep ilk seçenek";

        public static bool Aktif { get; private set; }
        public static TestModu I { get; private set; }

        /// <summary>Test sırasında ekran görüntüsü alınıyor ya da rapor hazırlanıyor (bot ve üst şerit bekler).</summary>
        public bool Mesgul => mesgul;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static string Ft(long v) => GameData.Ft(v);

        // ------------------------------------------------------------------ başlatma

        /// <summary>Kurulum ekranından çağrılır: botu kurar ve yeni oyunu başlatır.</summary>
        public static void Baslat()
        {
            if (Aktif || GameBootstrap.I == null || GameBootstrap.I.Started) return;
            var go = new GameObject("Otomatik Test");
            go.transform.SetParent(GameBootstrap.I.transform, false);
            var t = go.AddComponent<TestModu>();   // Awake: klasör, dosyalar, kayıt kapalı
            GameBootstrap.I.StartGame();
            t.OyunBasladi();
        }

        string klasor, ekranKlasoru, zipYolu = "";
        StreamWriter bildirimW, kartW, botW, hataW, csvW;
        GameUI ui;
        System.Random rnd;
        int tohum;
        bool eskiArkaPlan;

        void Awake()
        {
            I = this;
            Aktif = true;
            tohum = System.Environment.TickCount & 0x7fffffff;
            rnd = new System.Random(tohum);
            eskiArkaPlan = Application.runInBackground;
            Application.runInBackground = true;   // başka pencereye geçince test durmasın

            string kok = Path.Combine(Application.persistentDataPath, "test_raporlari");
            klasor = Path.Combine(kok, $"test_{System.DateTime.Now:yyyyMMdd_HHmmss}");
            ekranKlasoru = Path.Combine(klasor, "ekran");
            Directory.CreateDirectory(ekranKlasoru);
            var utf8 = new UTF8Encoding(false);
            bildirimW = Ac("bildirimler.txt", utf8);
            kartW = Ac("kartlar.txt", utf8);
            botW = Ac("bot.txt", utf8);
            hataW = Ac("hatalar.txt", utf8);
            csvW = Ac("gunluk.csv", new UTF8Encoding(true));   // BOM: Excel Türkçe karakterleri doğru açsın
            csvW.WriteLine(CsvBaslik);

            EventManager.Notified += OnNotified;
            EventManager.NotifiedQuiet += OnNotifiedQuiet;
            Application.logMessageReceived += OnLog;
        }

        StreamWriter Ac(string ad, Encoding enc) => new StreamWriter(Path.Combine(klasor, ad), false, enc) { AutoFlush = true };

        int baslangicGun;
        long baslangicKasa;
        float baslangicItibar;
        float baslangicGercek, sonIlerlemeGercek, sonOyunDakika, gunBasiGercek;

        void OyunBasladi()
        {
            var clk = GameClock.I;
            if (SaveSystem.I != null) SaveSystem.I.autoSave = false;
            if (Guide.I != null) Guide.I.FinishTutorial();   // rehber balonları ekran görüntülerini kapatmasın
            clk.SpeedMultiplier = Hiz;
            ui = FindAnyObjectByType<GameUI>();
            baslangicGun = clk.Day;
            baslangicKasa = EconomyManager.I.money;
            baslangicItibar = CompanyProfile.Reputation;
            enDusukKasa = baslangicKasa;
            baslangicGercek = sonIlerlemeGercek = gunBasiGercek = Time.realtimeSinceStartup;
            Eylem("Başlangıç", $"Yeni oyun: {CompanyProfile.Name}, zorluk {Difficulty.Level}, hedef {HedefGun} gün, hız {Hiz}×, " +
                               $"bot {TurAdi(Tur)}, harita tohumu {GameBootstrap.I.MapSeed}, bot tohumu {tohum}, " +
                               $"kasa {Ft(baslangicKasa)}, kod {DebugReport.CodeVersion}");
        }

        void OnDestroy()
        {
            EventManager.Notified -= OnNotified;
            EventManager.NotifiedQuiet -= OnNotifiedQuiet;
            Application.logMessageReceived -= OnLog;
            Kapat();
            Application.runInBackground = eskiArkaPlan;
            if (I == this) { I = null; Aktif = false; }
        }

        void Kapat()
        {
            foreach (var w in new[] { bildirimW, kartW, botW, hataW, csvW })
                if (w != null) { try { w.Dispose(); } catch { } }
            bildirimW = kartW = botW = hataW = csvW = null;
        }

        // ------------------------------------------------------------------ her kare

        bool bitti, mesgul, haritaBekliyor;
        string bitisNedeni = "";
        int sonKararSaati = -1, islenenGun, durakSayisi;
        int gunKare; float gunSure, gunEnUzunKare;
        bool takildiYazildi;

        void Update()
        {
            if (bitti || GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            var clk = GameClock.I;

            float dt = Time.unscaledDeltaTime;
            gunKare++; gunSure += dt;
            if (dt > gunEnUzunKare) gunEnUzunKare = dt;

            if (mesgul) return;

            // Gün sonları (StatsHistory her gece bir satır ekler)
            var gunler = StatsHistory.I != null ? StatsHistory.I.Days : null;
            if (gunler != null)
                while (islenenGun < gunler.Count) { var kayit = gunler[islenenGun++]; GunSonu(kayit, islenenGun); }

            // Karar kartı açıksa önce o (oyun kart açıkken durur)
            var ev = EventManager.I.Current;
            if (ev != null) { KartIsle(ev); return; }

            // Oyun bir şey yüzünden durduysa (menü açık değilken) devam ettir
            if (clk.Paused && (ui == null || !ui.TestMenuAcik))
            {
                clk.Paused = false;
                durakSayisi++;
                if (durakSayisi <= 20) Eylem("Uyarı", "Oyun kendiliğinden duraklamıştı, bot devam ettirdi.");
            }

            // Takılma kontrolü: 5 dakika oyun saati ilerlemezse bir kez not düşülür
            if (clk.TotalMinutes != sonOyunDakika) { sonOyunDakika = clk.TotalMinutes; sonIlerlemeGercek = Time.realtimeSinceStartup; takildiYazildi = false; }
            else if (!takildiYazildi && Time.realtimeSinceStartup - sonIlerlemeGercek > 300f)
            {
                takildiYazildi = true;
                Eylem("Uyarı", "Oyun saati 5 dakikadır ilerlemiyor (menü mü açık?).");
            }

            // Öğlen haritanın görüntüsü (gün sonunda istendiyse)
            if (haritaBekliyor && clk.Hour >= 13 && ui != null)
            {
                haritaBekliyor = false;
                StartCoroutine(HaritaGoruntusu());
                return;
            }

            // Bitiş: hedef gün tamamlandıktan sonraki öğlen (ekran görüntüleri gündüz çıksın)
            if (clk.CompanyDay > HedefGun && clk.Hour >= 12) { Bitir($"{HedefGun} gün tamamlandı"); return; }

            // Bot kararları: oyun saatinde saatte bir
            int saat = (int)(clk.TotalMinutes / 60f);
            if (saat != sonKararSaati)
            {
                sonKararSaati = saat;
                try { Kararlar(); }
                catch (System.Exception ex) { Debug.LogException(ex); Eylem("Bot hatası", ex.Message); }
            }
        }

        // ------------------------------------------------------------------ karar kartları

        readonly Dictionary<string, int> kartSayisi = new Dictionary<string, int>();
        readonly Dictionary<string, int> secimSayisi = new Dictionary<string, int>();
        readonly HashSet<string> goruntulenenKart = new HashSet<string>();
        int kartToplam, kartGoruntuSayisi;
        int iflasGunu = -1, kapanisGunu = -1, borcBittiGunu = -1, taksitKacti;
        const int MaxKartGoruntusu = 10;

        void KartIsle(GameEvent ev)
        {
            string anahtar = KartAnahtari(ev.title);
            if (ui != null && kartGoruntuSayisi < MaxKartGoruntusu && goruntulenenKart.Add(anahtar))
            {
                StartCoroutine(KartGoruntusuSonraSec(ev));
                return;
            }
            Sec(ev);
        }

        IEnumerator KartGoruntusuSonraSec(GameEvent ev)
        {
            mesgul = true;
            kartGoruntuSayisi++;
            yield return StartCoroutine(ui.TestCek(Path.Combine(ekranKlasoru, $"kart_{kartGoruntuSayisi:00}_{DosyaAdi(ev.title)}.jpg")));
            mesgul = false;
            if (EventManager.I != null && EventManager.I.Current == ev) Sec(ev);
        }

        /// <summary>Gün sonu raporu gibi her gün değişen başlıkları tek başlıkta toplar (tarih, isim, sayı atılır).</summary>
        static string KartAnahtari(string title)
        {
            if (string.IsNullOrEmpty(title)) return "(başlıksız)";
            int i = title.IndexOf(" — ", System.StringComparison.Ordinal);
            string t = i > 0 ? title.Substring(0, i) : title;
            return Regex.Replace(t, @"\d+", "#");
        }

        void Sec(GameEvent ev)
        {
            string neden;
            int idx = SecimYap(ev, out neden);
            var clk = GameClock.I;
            string anahtar = KartAnahtari(ev.title);
            kartToplam++;
            Say(kartSayisi, anahtar);
            string secilen = idx >= 0 && idx < ev.choices.Count ? ev.choices[idx].label : "(yok)";
            if (ev.choices.Count > 1) Say(secimSayisi, $"{anahtar} → {secilen}");

            if (ev.title == "İflas!" && iflasGunu < 0) iflasGunu = clk.CompanyDay;
            if (ev.title != null && ev.title.StartsWith("Son:") && kapanisGunu < 0) kapanisGunu = clk.CompanyDay;
            if (ev.title == "Borç bitti!" && borcBittiGunu < 0) borcBittiGunu = clk.CompanyDay;
            if (ev.title == "Taksit kaçtı!") taksitKacti++;

            if (kartW != null)
            {
                kartW.WriteLine($"[{Zaman()}] {ev.title}");
                kartW.WriteLine("    " + (ev.body ?? "").Replace("\n", "\n    "));
                for (int i = 0; i < ev.choices.Count; i++)
                    kartW.WriteLine($"    {(i == idx ? "→" : " ")} {i + 1}) {ev.choices[i].label}");
                if (!string.IsNullOrEmpty(neden) && ev.choices.Count > 1) kartW.WriteLine($"    neden: {neden}");
                kartW.WriteLine();
            }

            try { EventManager.I.Choose(idx); }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                Eylem("Kart hatası", $"\"{ev.title}\" / \"{secilen}\" seçilince hata: {ex.Message}");
            }
        }

        int SecimYap(GameEvent ev, out string neden)
        {
            neden = "";
            var izinli = new List<int>();
            for (int i = 0; i < ev.choices.Count; i++)
            {
                string l = ev.choices[i].label ?? "";
                if (l.Contains("Yeniden başla") || l.Contains("raporlarını kapat") || l.Contains("raporları kapat")) continue;
                izinli.Add(i);
            }
            if (izinli.Count == 0) return 0;
            // İflas / kapanış kartı: test sürsün diye "devam" seçilir
            bool son = ev.title == "İflas!" || (ev.title != null && ev.title.StartsWith("Son:"));
            if (son)
                foreach (int i in izinli)
                    if ((ev.choices[i].label ?? "").ToLowerInvariant().Contains("devam")) return i;
            if (izinli.Count == 1) return izinli[0];
            if (Tur == BotTuru.Akilli)
            {
                int a = -1;
                try { a = AkilliKart(ev, izinli, out neden); }
                catch (System.Exception ex) { Debug.LogException(ex); neden = "akıllı seçimde hata, ilk seçenek: " + ex.Message; }
                if (izinli.Contains(a)) return a;
            }
            return Tur == BotTuru.Rastgele ? izinli[rnd.Next(izinli.Count)] : izinli[0];
        }

        // ------------------------------------------------------------------ bot kararları

        int sonIseAlimGun = -100, sonSozlesmeGun = -100, sonBolgeGun = -100, sonGelistirmeGun = -100;
        readonly Dictionary<string, int> eylemSayisi = new Dictionary<string, int>();

        void Kararlar()
        {
            if (Tur == BotTuru.Akilli) { AkilliKararlar(); return; }
            long rezerv = Rezerv();
            BorcOde();
            IseAlim(rezerv);
            Sozlesme(rezerv);
            Bolge(rezerv);
            Gelistirme(rezerv);
            Destek(rezerv);
        }

        /// <summary>Kasada hep bırakılan para: sabit giderler + yaklaşan taksit.</summary>
        long Rezerv()
        {
            long r = 200000 + UpgradeManager.I.TotalDailyCost * 5L;
            var loan = FamilyLoan.I;
            if (loan != null && !loan.Finished && !loan.Failed && loan.NextDue != null && loan.DaysUntil(loan.NextDue) <= 21)
                r += loan.NextDue.Remaining;
            return r;
        }

        DayRecord SonGun()
        {
            var d = StatsHistory.I != null ? StatsHistory.I.Days : null;
            return d != null && d.Count > 0 ? d[d.Count - 1] : null;
        }

        long OrtalamaGelir(int gun)
        {
            var d = StatsHistory.I != null ? StatsHistory.I.Days : null;
            if (d == null || d.Count == 0) return 0;
            int n = Mathf.Min(gun, d.Count);
            long t = 0;
            for (int i = d.Count - n; i < d.Count; i++) t += d[i].income;
            return t / n;
        }

        int SozlesmeliDukkan()
        {
            int n = 0;
            foreach (var s in CityMap.I.Shops) if (s.contracted && !s.own) n++;
            return n;
        }

        void BorcOde()
        {
            var loan = FamilyLoan.I;
            if (loan == null || loan.Finished || loan.Failed) return;
            var due = loan.NextDue;
            if (due == null) return;
            int kalan = loan.DaysUntil(due);
            if (kalan > 1) return;
            long para = EconomyManager.I.money;
            if (para >= due.Remaining + 50000)
            {
                long tutar = due.Remaining;
                if (loan.Pay(tutar)) Eylem("Borç", $"{due.DueText} taksiti ödendi: {Ft(tutar)} (kasa {Ft(para)})");
            }
            else if (kalan <= 0 && para > 150000)
            {
                long tutar = System.Math.Min(due.Remaining, para - 100000);
                if (loan.Pay(tutar)) Eylem("Borç", $"{due.DueText} taksitine para yetmedi, kısmi ödendi: {Ft(tutar)} / {Ft(due.Remaining + tutar)}");
            }
        }

        void IseAlim(long rezerv)
        {
            var cm = CourierManager.I;
            var clk = GameClock.I;
            if (clk.Day == sonIseAlimGun || cm.Applications.Count == 0 || UpgradeManager.I.CourierCapFull) return;
            if (EconomyManager.I.FreeCash < rezerv / 2) return;
            var son = SonGun();
            int kurye = cm.Couriers.Count, dukkan = SozlesmeliDukkan();
            string neden = null;
            if (kurye < 3) neden = "3'ten az kurye";
            else if (kurye < dukkan * 1.5f) neden = $"dükkan başına kurye az ({kurye}/{dukkan})";
            else if (son != null && son.cancelled > Mathf.Max(2, son.deliveries * 0.03f)) neden = $"dün {son.cancelled} iptal";
            else if (son != null && son.avgAssignWait > 6f) neden = $"atama bekleme {son.avgAssignWait:0.0} dk";
            else if (son != null && son.deliveries > 0 && son.lost > son.deliveries * 0.2f) neden = $"dün {son.lost} kaçan sipariş";
            if (neden == null) return;

            CourierCandidate en = null;
            float enPuan = float.MinValue;
            foreach (var a in cm.Applications)
            {
                float p = a.speedSkill + a.care + a.stamina * 0.5f - a.pickiness + a.level * 0.1f;
                if (p > enPuan) { enPuan = p; en = a; }
            }
            if (en == null) return;
            int once = cm.Couriers.Count;
            cm.Hire(en);
            if (cm.Couriers.Count > once)
            {
                sonIseAlimGun = clk.Day;
                Eylem("İşe alım", $"{en.name} ({GameData.Vehicle(en.vehicle).name}, {CourierManager.ShortOccupation(en.occupation)}, seviye {en.level}, " +
                                   $"seçicilik {en.pickiness:0.00}) · neden: {neden} · başvuru {cm.Applications.Count + 1}");
            }
        }

        void Sozlesme(long rezerv)
        {
            var clk = GameClock.I;
            var map = CityMap.I;
            if (clk.Day - sonSozlesmeGun < 2) return;
            var son = SonGun();
            if (son == null) return;   // ilk gün bitmeden sözleşme yok
            if (son.deliveries + son.cancelled > 0 && son.cancelled > (son.deliveries + son.cancelled) * 0.05f) return;   // önce mevcut işi yetiştir
            if (CourierManager.I.Couriers.Count < SozlesmeliDukkan()) return;

            Shop en = null;
            foreach (var s in map.Shops)
            {
                if (s.contracted || s.own || !map.IsUnlocked(s.tile) || clk.Day < s.resignBlockedUntil) continue;
                if (en == null || s.def.contractCost < en.def.contractCost) en = s;
            }
            if (en == null || EconomyManager.I.FreeCash - en.def.contractCost < rezerv) return;
            if (map.SignContract(en))
            {
                sonSozlesmeGun = clk.Day;
                Eylem("Sözleşme", $"{en.Name} ({Ft(en.def.contractCost)}, komisyon %{en.Commission * 100f:0.#}) · dükkan {SozlesmeliDukkan()}");
            }
        }

        void Bolge(long rezerv)
        {
            var clk = GameClock.I;
            var map = CityMap.I;
            if (clk.Day - sonBolgeGun < 7 || map.UnlockLevel >= map.MaxLevel) return;
            foreach (var s in map.Shops)
                if (!s.contracted && !s.own && map.IsUnlocked(s.tile)) return;   // açık bölgede imzalanacak dükkan varken açma
            long bedel = CityMap.UnlockCost[map.UnlockLevel + 1];
            if (EconomyManager.I.FreeCash < bedel + rezerv * 2) return;
            if (map.TryUnlockNext())
            {
                sonBolgeGun = clk.Day;
                Eylem("Bölge", $"{CityMap.DistrictNames[map.UnlockLevel]} açıldı ({Ft(bedel)})");
            }
        }

        void Gelistirme(long rezerv)
        {
            if (Guide.I != null && !Guide.I.IsOpen(Feature.Gelisim)) return;
            var up = UpgradeManager.I;
            var clk = GameClock.I;
            var eco = EconomyManager.I;

            // Kurye hesabı dolduysa ve başvuru bekliyorsa platform paketi
            if (up.CourierCapFull && CourierManager.I.Applications.Count > 0 && !up.IsLocked(UpgradeKind.Platform))
            {
                int c = up.NextCost(UpgradeKind.Platform);
                if (c > 0 && eco.FreeCash - c >= rezerv && up.Buy(UpgradeKind.Platform))
                {
                    Eylem("Geliştirme", $"Platform paketi → seviye {up.Level(UpgradeKind.Platform)} ({Ft(c)}), kurye sınırı {up.CourierCap}");
                    return;
                }
            }

            // Diğerleri: 5 günde bir, en ucuz olan, günlük gideri gelirin %10'unu geçmiyorsa
            if (clk.Day - sonGelistirmeGun < 5) return;
            long gelir = OrtalamaGelir(7);
            if (gelir <= 0) return;
            UpgradeKind? sec = null;
            int secBedel = int.MaxValue;
            foreach (var d in UpgradeManager.Defs)
            {
                if (d.kind == UpgradeKind.Platform || (!Sade.Hijyen && d.kind == UpgradeKind.Hijyen)) continue;
                int bedel = up.NextCost(d.kind);
                if (bedel <= 0 || up.IsLocked(d.kind)) continue;
                int lv = up.Level(d.kind);
                long ekGunluk = d.DailyAt(lv + 1) - d.DailyAt(lv);
                if (ekGunluk > gelir * 0.1f) continue;
                if (eco.FreeCash < rezerv + bedel * 3L) continue;
                if (bedel < secBedel) { secBedel = bedel; sec = d.kind; }
            }
            if (sec == null) return;
            var k = sec.Value;
            if (up.Buy(k))
            {
                sonGelistirmeGun = clk.Day;
                var def = UpgradeManager.Def(k);
                Eylem("Geliştirme", $"{def.name} → {def.levelNames[up.Level(k)]} ({Ft(secBedel)}, günlük gider {Ft(up.DailyCost(k))}) · 7 günlük ort. gelir {Ft(gelir)}");
            }
        }

        void Destek(long rezerv)
        {
            if (Guide.I != null && !Guide.I.IsOpen(Feature.Destek)) return;
            var sm = SupportManager.I;
            if (sm == null || sm.Agents.Count > 0 || sm.Candidates.Count == 0 || CourierManager.I.Couriers.Count < 6) return;
            SupportCandidate en = null;
            foreach (var c in sm.Candidates) if (en == null || c.skill > en.skill) en = c;
            if (en == null || EconomyManager.I.FreeCash < rezerv + en.monthlySalary * 2L) return;
            int once = sm.Agents.Count;
            sm.Hire(en);
            if (sm.Agents.Count > once) Eylem("Destek", $"{en.name} işe alındı (beceri {en.skill:0.00}, aylık {Ft(en.monthlySalary)})");
        }

        void Eylem(string tur, string metin)
        {
            Say(eylemSayisi, tur);
            if (botW != null) botW.WriteLine($"[{Zaman()}] {tur}: {metin}");
        }

        // ------------------------------------------------------------------ gün sonu

        const string CsvBaslik =
            "gun;tarih;teslimat;iptal;kacan;gelir;gider;net;kasa;serbest_kasa;itibar;kurye;dukkan;bolge;gec;gec_15dk;soguk;ort_teslim_dk;" +
            "ort_atama_bekleme_dk;ort_alma_bekleme_dk;dukkan_iptali;musteri_vazgecti;gecikme_iptali;kurye_odemesi;ekipman;maaslar;" +
            "ort_moral;ort_yorgunluk;basvuru;pazar_payi;aile_borcu_kalan;gelistirme_gunluk;fps_ort;fps_en_dusuk;gercek_saniye";

        long enDusukKasa;
        int enDusukKasaGun;
        readonly List<string> fpsSatirlari = new List<string>();
        float fpsToplam; int fpsGun, fpsDusukGun;
        float enKotuFps = float.MaxValue; int enKotuFpsGun;

        /// <summary>gunNo: biten günün sırası (1 = şirketin ilk günü). DayRecord.companyDay ertesi günü gösterir.</summary>
        void GunSonu(DayRecord d, int gunNo)
        {
            var cm = CourierManager.I;
            float moral = 0f, yorgun = 0f;
            foreach (var c in cm.Couriers) { moral += c.morale; yorgun += c.fatigue; }
            int n = Mathf.Max(1, cm.Couriers.Count);
            float fpsOrt = gunSure > 0f ? gunKare / gunSure : 0f;
            float fpsMin = gunEnUzunKare > 0f ? 1f / gunEnUzunKare : 0f;
            float gercek = Time.realtimeSinceStartup - gunBasiGercek;
            gunKare = 0; gunSure = 0f; gunEnUzunKare = 0f; gunBasiGercek = Time.realtimeSinceStartup;
            if (gunNo > 1)   // ilk gün kurulumun yükünü taşır, ortalamaya katılmaz
            {
                fpsToplam += fpsOrt; fpsGun++;
                if (fpsOrt < 30f) fpsDusukGun++;
                if (fpsOrt < enKotuFps) { enKotuFps = fpsOrt; enKotuFpsGun = gunNo; }
            }

            if (d.cash < enDusukKasa) { enDusukKasa = d.cash; enDusukKasaGun = gunNo; }
            float pay = RivalCompany.I != null ? RivalCompany.I.MarketShare : 0f;
            long borc = FamilyLoan.I != null ? FamilyLoan.I.Remaining : 0;

            if (csvW != null)
                csvW.WriteLine(string.Join(";", new[]
                {
                    gunNo.ToString(Inv), d.date, d.deliveries.ToString(Inv), d.cancelled.ToString(Inv), d.lost.ToString(Inv),
                    P(d.income), P(d.expenses), P(d.net), P(d.cash), P(d.freeCash),
                    F(d.reputation), d.couriers.ToString(Inv), d.contractedShops.ToString(Inv), d.district.ToString(Inv),
                    d.late.ToString(Inv), d.late15.ToString(Inv), d.cold.ToString(Inv), F(d.avgMinutes), F(d.avgAssignWait), F(d.avgPickupWait),
                    d.cancelShop.ToString(Inv), d.cancelCustomer.ToString(Inv), d.cancelDelay.ToString(Inv),
                    P(d.courierPay), P(d.equipment), P(d.salaries),
                    F(moral / n), F(yorgun / n), cm.Applications.Count.ToString(Inv), F(pay), P(borc),
                    P(UpgradeManager.I.TotalDailyCost), F(fpsOrt), F(fpsMin), F(gercek)
                }));

            if (gunNo == 1 || gunNo % 10 == 0) haritaBekliyor = true;
        }

        static string F(float v) => v.ToString("0.##", Inv);

        /// <summary>CSV için para: oyunda seçili birimde, binlik ayırıcısız (Euro seçiliyse € olarak, değilse Ft).</summary>
        static string P(long v) => GameData.DisplayCurrency == Currency.Euro ? (v / (double)GameData.FtPerEuro).ToString("0.##", Inv) : v.ToString(Inv);

        IEnumerator HaritaGoruntusu()
        {
            mesgul = true;
            yield return StartCoroutine(ui.TestHaritaGoruntusu(Path.Combine(ekranKlasoru, $"harita_gun{GameClock.I.CompanyDay:000}.jpg")));
            mesgul = false;
        }

        // ------------------------------------------------------------------ bildirimler ve hatalar

        readonly Dictionary<string, int> bildirimKalip = new Dictionary<string, int>();
        readonly Dictionary<string, int> hataSayisi = new Dictionary<string, int>();
        readonly Dictionary<string, int> uyariSayisi = new Dictionary<string, int>();
        int bildirimToplam, hataToplam, uyariToplam;
        static readonly Regex SayiKalibi = new Regex(@"\d[\d .,:]*\d|\d");

        /// <summary>Sessiz bildirimler (oyuncu görmez): dosyaya "(sessiz)" diye yazılır.</summary>
        void OnNotifiedQuiet(string m) => OnNotified("(sessiz) " + m);

        void OnNotified(string m)
        {
            if (bitti || string.IsNullOrEmpty(m)) return;
            bildirimToplam++;
            if (bildirimW != null) bildirimW.WriteLine($"[{Zaman()}] {m}");
            string k = SayiKalibi.Replace(m, "#");
            if (k.Length > 140) k = k.Substring(0, 140) + "…";
            Say(bildirimKalip, k);
            AkilliBildirim(m);
        }

        void OnLog(string mesaj, string yigin, LogType tur)
        {
            if (tur == LogType.Log) return;
            string k = mesaj ?? "";
            if (k.Length > 300) k = k.Substring(0, 300) + "…";
            if (tur == LogType.Warning)
            {
                uyariToplam++;
                Say(uyariSayisi, k);
                return;
            }
            hataToplam++;
            Say(hataSayisi, k);
            if (hataSayisi[k] == 1 && hataW != null)
            {
                hataW.WriteLine($"[{Zaman()}] {tur}: {mesaj}");
                hataW.WriteLine("    " + (yigin ?? "").Trim().Replace("\n", "\n    "));
                hataW.WriteLine();
            }
        }

        // ------------------------------------------------------------------ bitiş

        /// <summary>Testi bitirir: oyun durur, F9 raporu, ekran görüntüleri, özet ve .zip hazırlanır.</summary>
        public void Bitir(string neden)
        {
            if (bitti) return;
            bitti = true;
            bitisNedeni = neden;
            GameClock.I.Paused = true;
            StartCoroutine(BitirRutin());
        }

        IEnumerator BitirRutin()
        {
            mesgul = true;
            Eylem("Bitiş", bitisNedeni);

            // Sırada bekleyen kartlar ekran görüntülerini kapatmasın
            for (int i = 0; i < 100 && EventManager.I != null && !EventManager.I.Idle; i++)
            {
                if (EventManager.I.Current != null) Sec(EventManager.I.Current);
                yield return null;
            }

            string f9 = "";
            try
            {
                if (DebugReport.I != null) f9 = DebugReport.I.Write(false);
                if (!string.IsNullOrEmpty(f9) && File.Exists(f9)) File.Copy(f9, Path.Combine(klasor, "hata_ayiklama_raporu.txt"), true);
            }
            catch (System.Exception ex) { Debug.LogWarning("Test: F9 raporu kopyalanamadı: " + ex.Message); }

            var kilitli = new List<string>();
            if (ui != null) yield return StartCoroutine(ui.TestPencereGoruntuleri(ekranKlasoru, kilitli));

            try { File.WriteAllText(Path.Combine(klasor, "ozet.txt"), Ozet(kilitli), new UTF8Encoding(false)); }
            catch (System.Exception ex) { Debug.LogWarning("Test: özet yazılamadı: " + ex.Message); }

            if (hataW != null)
            {
                hataW.WriteLine("==================== EN SIK UYARILAR ====================");
                foreach (var kv in EnCok(uyariSayisi, 25)) hataW.WriteLine($"  {kv.Value}× {kv.Key}");
            }
            Kapat();

            try
            {
                zipYolu = klasor + ".zip";
                if (File.Exists(zipYolu)) File.Delete(zipYolu);
                if (!Zipla(klasor, zipYolu)) zipYolu = "";
            }
            catch (System.Exception ex)
            {
                zipYolu = "";
                Debug.LogWarning("Test: zip yapılamadı (klasörü kendin sıkıştırıp gönder): " + ex.Message);
            }

            mesgul = false;
            KlasoruAc();
        }

        /// <summary>
        /// Klasörü .zip yapar. ZipFile her Unity ayarında derlenmeyebileceği için yansıma ile çağrılır:
        /// bulunamazsa false döner, klasör yine de durur (Mac'te sağ tık > Sıkıştır).
        /// </summary>
        static bool Zipla(string kaynak, string hedef)
        {
            var t = System.Type.GetType("System.IO.Compression.ZipFile, System.IO.Compression.ZipFile")
                 ?? System.Type.GetType("System.IO.Compression.ZipFile, System.IO.Compression.FileSystem")
                 ?? System.Type.GetType("System.IO.Compression.ZipFile, netstandard");
            var m = t != null ? t.GetMethod("CreateFromDirectory", new[] { typeof(string), typeof(string) }) : null;
            if (m == null)
            {
                Debug.LogWarning("Test: ZipFile bulunamadı, rapor klasör olarak kaldı (kendin sıkıştırıp gönder).");
                return false;
            }
            m.Invoke(null, new object[] { kaynak, hedef });
            return true;
        }

        public void KlasoruAc()
        {
            try { Application.OpenURL(new System.Uri(Path.GetDirectoryName(klasor)).AbsoluteUri); }
            catch (System.Exception ex) { Debug.LogWarning("Test: klasör açılamadı: " + ex.Message); }
        }

        // ------------------------------------------------------------------ özet

        string Ozet(List<string> kilitli)
        {
            var sb = new StringBuilder();
            var clk = GameClock.I;
            var eco = EconomyManager.I;
            var cm = CourierManager.I;
            var gunler = StatsHistory.I != null ? StatsHistory.I.Days : new List<DayRecord>();
            float gercekDk = (Time.realtimeSinceStartup - baslangicGercek) / 60f;

            sb.AppendLine("KURYE TYCOON — OTOMATİK TEST ÖZETİ");
            sb.AppendLine($"Kod sürümü: {DebugReport.CodeVersion}");
            sb.AppendLine($"Tarih (gerçek): {System.DateTime.Now:yyyy-MM-dd HH:mm} · Unity {Application.unityVersion} · {SystemInfo.operatingSystem}");
            sb.AppendLine($"Cihaz: {SystemInfo.deviceModel} · {SystemInfo.processorType} · {SystemInfo.graphicsDeviceName} · ekran {Screen.width}x{Screen.height}");
            sb.AppendLine($"Ayarlar: hedef {HedefGun} gün · hız {Hiz}× · zorluk {Difficulty.Level} · bot {TurAdi(Tur)} · " +
                          $"grafik {GrafikKalitesi.Seviye} · harita tohumu {GameBootstrap.I.MapSeed} · bot tohumu {tohum}");
            sb.AppendLine($"Bitiş: {bitisNedeni} · oynanan {gunler.Count} tam gün · gerçek süre {gercekDk:0.0} dk");

            Baslik(sb, "SONUÇ");
            sb.AppendLine($"Kasa: {Ft(baslangicKasa)} → {Ft(eco.money)} · en düşük {Ft(enDusukKasa)} ({(enDusukKasaGun > 0 ? enDusukKasaGun + ". gün sonu" : "başlangıç")}) · serbest kasa {Ft(eco.FreeCash)}");
            sb.AppendLine($"İtibar: {baslangicItibar:0.00} → {CompanyProfile.Reputation:0.00}");
            sb.AppendLine($"Kurye: {cm.Couriers.Count} (sınır {UpgradeManager.I.CourierCap}) · başvuru bekleyen {cm.Applications.Count} · " +
                          $"ayrılan {Sayac("Kurye ayrıldı")} · sözleşmeli dükkan {SozlesmeliDukkan()}/{CityMap.I.Shops.Count} · bölge {CityMap.I.UnlockLevel}/{CityMap.I.MaxLevel}");
            if (RivalCompany.I != null) sb.AppendLine($"Pazar payı (geçen hafta): %{RivalCompany.I.MarketShare * 100f:0}");
            var loan = FamilyLoan.I;
            if (loan != null)
                sb.AppendLine($"Aile borcu: ödenen {Ft(loan.TotalPaid)} · kalan {Ft(loan.Remaining)} · güven {loan.Trust} · kaçan taksit kartı {taksitKacti}" +
                              (loan.Finished ? " · BİTTİ" : "") + (loan.Failed ? " · BAŞARISIZ (şirket kapandı)" : ""));
            sb.AppendLine(iflasGunu > 0 ? $"!!! İFLAS: {iflasGunu}. gün (bot \"borçla devam\" seçti)" : "İflas yok.");
            if (kapanisGunu > 0) sb.AppendLine($"!!! ŞİRKET KAPANDI kartı: {kapanisGunu}. gün");
            if (borcBittiGunu > 0) sb.AppendLine($"Aile borcu {borcBittiGunu}. günde bitti.");

            if (gunler.Count > 0)
            {
                long tes = 0, ipt = 0, kac = 0, net = 0, gec = 0, gec15 = 0, soguk = 0;
                double dk = 0;
                foreach (var d in gunler)
                {
                    tes += d.deliveries; ipt += d.cancelled; kac += d.lost; net += d.net; gec += d.late; gec15 += d.late15; soguk += d.cold;
                    dk += d.avgMinutes * d.deliveries;
                }
                sb.AppendLine($"Toplam: {tes} teslimat · {ipt} iptal (%{Yuzde(ipt, tes + ipt):0.0}) · {kac} kaçan · net {Ft(net)} (günlük ort. {Ft(net / gunler.Count)})");
                sb.AppendLine($"Kalite: ort. teslim {(tes > 0 ? dk / tes : 0):0.0} dk · geç %{Yuzde(gec, tes):0.0} · 15 dk+ geç %{Yuzde(gec15, tes):0.0} · soğuk %{Yuzde(soguk, tes):0.0}");

                Baslik(sb, "HAFTA HAFTA");
                sb.AppendLine("hafta | teslimat | iptal % | net          | hafta sonu kasa | kurye | dükkan | itibar");
                for (int i = 0; i < gunler.Count; i += 7)
                {
                    long ht = 0, hi = 0, hn = 0;
                    int son = Mathf.Min(gunler.Count, i + 7);
                    for (int j = i; j < son; j++) { ht += gunler[j].deliveries; hi += gunler[j].cancelled; hn += gunler[j].net; }
                    var s = gunler[son - 1];
                    sb.AppendLine($"{i / 7 + 1,5} | {ht,8} | {Yuzde(hi, ht + hi),6:0.0} | {Ft(hn),12} | {Ft(s.cash),15} | {s.couriers,5} | {s.contractedShops,6} | {s.reputation:0.00}");
                }
            }

            Baslik(sb, "BOTUN YAPTIKLARI (ayrıntısı bot.txt)");
            foreach (var kv in EnCok(eylemSayisi, 50)) sb.AppendLine($"  {kv.Key}: {kv.Value}");
            sb.AppendLine(Tur == BotTuru.Akilli
                ? "Akıllı bot: kampanya ayarları ve araç değişimi dışında oyuncunun yapabildiği her şeyi kullanır (nedenleri bot.txt'de)."
                : "Basit bot: kampanyalar, kendi marketin, ekipman teklifi, operasyon sorumlusu ve fiyat ayarlarına dokunmaz.");

            Baslik(sb, $"KARAR KARTLARI ({kartToplam} kart; metinleri kartlar.txt)");
            foreach (var kv in EnCok(kartSayisi, 80)) sb.AppendLine($"  {kv.Value,4}× {kv.Key}");
            sb.AppendLine("Seçimler:");
            foreach (var kv in EnCok(secimSayisi, 80)) sb.AppendLine($"  {kv.Value,4}× {kv.Key}");

            Baslik(sb, $"EN SIK BİLDİRİMLER ({bildirimToplam} bildirim; sayılar # yapıldı; tamamı bildirimler.txt)");
            foreach (var kv in EnCok(bildirimKalip, 40)) sb.AppendLine($"  {kv.Value,5}× {kv.Key}");

            Baslik(sb, $"HATALAR: {hataToplam} hata ({hataSayisi.Count} farklı) · {uyariToplam} uyarı ({uyariSayisi.Count} farklı)");
            if (hataSayisi.Count == 0) sb.AppendLine("Hata yok.");
            foreach (var kv in EnCok(hataSayisi, 30)) sb.AppendLine($"  {kv.Value,5}× {kv.Key}");

            Baslik(sb, "PERFORMANS");
            if (fpsGun > 0)
                sb.AppendLine($"Ortalama FPS {fpsToplam / fpsGun:0} · en kötü gün {enKotuFps:0} FPS ({enKotuFpsGun}. gün) · 30 FPS altı gün: {fpsDusukGun}/{fpsGun}");
            sb.AppendLine($"Kendiliğinden duraklama: {durakSayisi} kez");
            sb.AppendLine("(Hız yüksek olduğu için FPS normal oyundan biraz düşük çıkabilir; günlük değerler gunluk.csv'de.)");

            Baslik(sb, "MENÜLER");
            if (Guide.I != null)
                foreach (Feature f in System.Enum.GetValues(typeof(Feature)))
                    sb.AppendLine($"  {f}: {(Guide.I.IsOpen(f) ? "açık" : "KAPALI")}");
            foreach (var k in kilitli) sb.AppendLine("  " + k);

            Baslik(sb, "DOSYALAR");
            sb.AppendLine("  gunluk.csv · kartlar.txt · bildirimler.txt · bot.txt · hatalar.txt · hata_ayiklama_raporu.txt (F9) · ekran/");
            return sb.ToString();
        }

        static void Baslik(StringBuilder sb, string t)
        {
            sb.AppendLine();
            sb.AppendLine("==================== " + t + " ====================");
        }

        static float Yuzde(long a, long b) => b > 0 ? a * 100f / b : 0f;
        static int Sayac(string k) => Telemetry.Counts.TryGetValue(k, out int v) ? v : 0;

        static void Say(Dictionary<string, int> d, string k)
        {
            d.TryGetValue(k, out int v);
            d[k] = v + 1;
        }

        static List<KeyValuePair<string, int>> EnCok(Dictionary<string, int> d, int n)
        {
            var l = new List<KeyValuePair<string, int>>(d);
            l.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));
            if (l.Count > n) l.RemoveRange(n, l.Count - n);
            return l;
        }

        static string Zaman()
        {
            var c = GameClock.I;
            return c != null ? $"{c.CompanyDay}. gün {c.Hour:00}:{c.Minute:00}" : "-";
        }

        static string DosyaAdi(string s)
        {
            if (string.IsNullOrEmpty(s)) return "kart";
            var sb = new StringBuilder();
            foreach (char ch in s)
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
                if (sb.Length >= 40) break;
            }
            return sb.ToString().Trim('_');
        }

        // ------------------------------------------------------------------ üst şerit

        GUIStyle seritYazi;

        void OnGUI()
        {
            if (mesgul || GameBootstrap.I == null || !GameBootstrap.I.Started) return;   // ekran görüntüsüne girmesin
            GUI.depth = -100;
            float s = Mathf.Clamp(Screen.height / 900f, 0.8f, 2f);
            var eskiMatris = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float sw = Screen.width / s;
            if (seritYazi == null)
                seritYazi = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, normal = { textColor = Color.white } };

            float w = Mathf.Min(620f, sw - 20f);
            var r = new Rect((sw - w) / 2f, 52f, w, bitti ? 92f : 58f);
            var eskiRenk = GUI.color;
            GUI.color = new Color(0.05f, 0.05f, 0.07f, 0.85f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = eskiRenk;

            var clk = GameClock.I;
            if (!bitti)
            {
                GUI.Label(new Rect(r.x + 10, r.y + 6, w - 150, 46),
                    $"OTOMATİK TEST · {Mathf.Min(clk.CompanyDay, HedefGun)}/{HedefGun}. gün · {clk.SpeedMultiplier:0}× · kasa {Ft(EconomyManager.I.money)}\n" +
                    $"kart {kartToplam} · bot eylemi {Toplam(eylemSayisi)} · hata {hataToplam} · kayıt kapalı", seritYazi);
                if (GUI.Button(new Rect(r.xMax - 132, r.y + 14, 122, 30), "Testi şimdi bitir")) Bitir("elle bitirildi");
            }
            else
            {
                GUI.Label(new Rect(r.x + 10, r.y + 6, w - 20, 50),
                    $"Test bitti ({bitisNedeni}). Gönderilecek dosya:\n{(string.IsNullOrEmpty(zipYolu) ? klasor : zipYolu)}", seritYazi);
                if (GUI.Button(new Rect(r.x + 10, r.y + 56, 130, 28), "Klasörü aç")) KlasoruAc();
                if (GUI.Button(new Rect(r.x + 150, r.y + 56, 150, 28), "Ana menüye dön")) GameBootstrap.Restart();
            }
            GUI.matrix = eskiMatris;
        }

        static int Toplam(Dictionary<string, int> d) { int t = 0; foreach (var v in d.Values) t += v; return t; }
    }
}
