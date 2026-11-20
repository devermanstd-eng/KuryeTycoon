using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Kurulum ekranı (oyun başlamadan önce): kayıtlı oyunlar, yeni oyunun kayıt yuvası, hikâye, şirket adı ve renkleri
    /// (önizlemeyle), zorluk, oyun türü, olay anlatıcısı ve geliştirici testi. Eskiden IMGUI'de (DrawSetup) çiziliyordu.
    /// Ayrı bir UIDocument: oyunun asıl arayüzü (UiKok.UiKur) oyun başlayınca kurulur, bu ekran o zaman kaldırılır;
    /// Esc menüsünden ana menüye dönülünce yeniden kurulur. Görünüm Kurye.uss'te ("kurulum-" sınıfları).
    /// </summary>
    public partial class GameUI
    {
        UIDocument kurulumDoc;
        PanelSettings kurulumPanel;
        VisualElement kurulumKok;
        Yazici ky;
        readonly VisualElement[] kurulumRenkler = new VisualElement[Presets.Length];
        VisualElement onizZemin, onizLogoDis, onizLogoIc, onizKurye, onizKuryeIc;
        Label onizHarf, onizAd, onizAlt;
        static readonly string[] RenkSekmeleri = { "Ana renk", "İkincil renk" };
        int setupSenaryo;            // oyun türü (Senaryo.Tur)
        int setupAnlatici = 1;       // olay anlatıcısı (Anlatici.Tur), varsayılan Klasik

        /// <summary>Update'ten: oyun başlamadıysa kurulum ekranını kurar ve yeniler, başladıysa kaldırır.</summary>
        void KurulumGuncelle()
        {
            bool goster = GameBootstrap.I != null && !GameBootstrap.I.Started;
            if (!goster) { KurulumKaldir(); return; }
            if (kurulumDoc == null)
            {
                kurulumPanel = ScriptableObject.CreateInstance<PanelSettings>();
                kurulumPanel.name = "Kurye Kurulum";
                kurulumPanel.scaleMode = PanelScaleMode.ConstantPixelSize;
                kurulumPanel.scale = UiOlcek();
                kurulumPanel.sortingOrder = 200;
                kurulumPanel.clearColor = false;
                var tema = Resources.Load<ThemeStyleSheet>("UI/Arayuz/Tema");
                if (tema != null) kurulumPanel.themeStyleSheet = tema;
                var go = new GameObject("Kurulum Ekranı (UI Toolkit)");
                go.SetActive(false);
                go.transform.SetParent(transform, false);
                kurulumDoc = go.AddComponent<UIDocument>();
                kurulumDoc.panelSettings = kurulumPanel;
                go.SetActive(true);
            }
            if (kurulumKok == null && !KurulumKokKur()) return;   // panel bir sonraki karede hazır olur
            float s = UiOlcek();
            if (Mathf.Abs(kurulumPanel.scale - s) > 0.0005f) kurulumPanel.scale = s;
            KurulumYaz();
        }

        bool KurulumKokKur()
        {
            var root = kurulumDoc.rootVisualElement;
            if (root == null) return false;
            var stil = Resources.Load<StyleSheet>("UI/Arayuz/Kurye");
            if (stil != null) root.styleSheets.Add(stil);
            else Debug.LogError("Arayüz stili bulunamadı: Resources/UI/Arayuz/Kurye.uss");
            var font = UITheme.UIFont;
            if (font != null) root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(font));

            kurulumKok = U.El(root, "kurulum-zemin");
            var kart = U.El(kurulumKok, "kurulum-kart");
            var kaydir = new ScrollView(ScrollViewMode.Vertical);
            kaydir.AddToClassList("kurulum-kaydir");
            kaydir.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            kart.Add(kaydir);
            ky = new Yazici(this, kaydir.contentContainer);
            return true;
        }

        void KurulumKaldir()
        {
            if (kurulumDoc == null) return;
            Destroy(kurulumDoc.gameObject);
            if (kurulumPanel != null) Destroy(kurulumPanel);
            kurulumDoc = null;
            kurulumPanel = null;
            kurulumKok = null;
            ky = null;
            onizZemin = null;
            Array.Clear(kurulumRenkler, 0, kurulumRenkler.Length);
            setupSaveChecked = false;   // ana menüye dönülürse kayıtlar yeniden okunur
        }

        // ------------------------------------------------------------------ içerik

        void KurulumYaz()
        {
            if (!setupSaveChecked)
            {
                setupSaveChecked = true;
                setupSave = null;
                for (int i = 0; i < SaveSystem.SlotCount; i++)
                {
                    slotSaves[i] = SaveSystem.ReadSave(i);
                    if (slotSaves[i] != null && setupSave == null) setupSave = slotSaves[i];
                }
                if (newGameSlot < 0) { int bos = SaveSystem.FirstEmptySlot(); newGameSlot = bos >= 0 ? bos : SaveSystem.Slot; }
            }
            if (!setupDifficultyLoaded)
            {
                setupDifficultyLoaded = true;
                setupDifficulty = Difficulty.LastChoice;
            }

            var y = ky;
            y.Basla();
            if (!string.IsNullOrEmpty(GameBootstrap.LastLoadError)) y.Uyari(GameBootstrap.LastLoadError);
            if (setupSave != null) KayitliOyunlar(y);

            y.Bolum("Yeni oyunun kayıt yuvası");
            var yuvalar = new string[SaveSystem.SlotCount];
            for (int i = 0; i < yuvalar.Length; i++) yuvalar[i] = $"{i + 1}. yuva" + (slotSaves[i] != null ? " (dolu)" : "");
            y.Secim(yuvalar, newGameSlot, i => newGameSlot = i);
            if (newGameSlot >= 0 && slotSaves[newGameSlot] != null)
                y.Uyari($"{newGameSlot + 1}. yuvadaki \"{slotSaves[newGameSlot].companyName}\" kaydı, yeni oyun ilk kez kaydedilince silinir.");

            y.Bosluk(6);
            y.Yazi("Kurye Şirketini Kur", "kurulum-baslik");
            var gb = GameBootstrap.I;
            long borc = Math.Max(gb.familyLoanTotal, gb.familyLoanAmount);
            long toplamBorc = (long)Mathf.Round(borc * (1f + (FamilyLoan.I != null ? FamilyLoan.I.faizOrani : 0.08f)) / 1000f) * 1000;
            y.Yazi($"Yıllarca {FamilyLoan.EskiSirket}'te kuryelik yaptın. Şimdi {FamilyLoan.Banka}'tan aldığın {Ft(borc)} girişimci kredisiyle kendi kurye " +
                   "şirketini kuruyorsun. " +
                   (borc > gb.familyLoanAmount ? $"Kuruluş masraflarından sonra kasada {Ft(gb.familyLoanAmount)} kalıyor. " : "") +
                   $"Krediyi faiziyle ({Ft(toplamBorc)}) Temmuz sonuna kadar ayda ~{Ft(toplamBorc / 5)} taksitle geri ödemen gerekiyor; " +
                   "eski patronun artık rakiplerinden biri.");

            y.KartBasla();
            y.Baslik("Bu oyunda ne yapacaksın?");
            y.Kucuk("•  Dükkanlarla anlaş, kurye al, siparişleri zamanında teslim ettir. Hızlı teslimat puanını ve pazar payını büyütür.");
            y.Kucuk("•  Gelirin: dükkan komisyonu ve teslimat ücreti. Giderin: kurye hakedişi, ekipman, geliştirmeler, personel.");
            y.Kucuk("•  Kuryeler serbest çalışır: saatte iyi kazanırsa mutlu olur, uzun günler biriktikçe yorulur. Çok kurye alırsan iş başına düşen kazanç azalır.");
            y.Kucuk("•  İtibar son iki haftanın müşteri puanıdır; iptal edilen sipariş 1 yıldız sayılır. Şirket büyüdükçe yatırımlar da pahalanır.");
            y.Kucuk("•  Şehir yaşıyor: gündüz ve gece, hava, trafik ve şehir olayları siparişleri ve kuryeleri etkiler.");
            y.Kucuk("•  İlk hedef: banka kredisini zamanında ödemek. Sonrası: şehrin en büyük kurye şirketi olmak.");
            y.Not("Boşluk: duraklat · 1–8: hız · H: sipariş yoğunluğu · F10: fotoğraf modu · Esc: menü. Oyun her gece kendini kaydeder.");
            y.KartBitir();

            y.Bolum("Şirket adı");
            y.Ozel("kurulum-ad", () =>
            {
                var t = new TextField { maxLength = 24, value = companyName };
                t.AddToClassList("kurulum-ad");
                t.RegisterValueChangedCallback(e => companyName = e.newValue);
                return t;
            });

            y.Bolum("Renkler");
            y.Secim(RenkSekmeleri, editingColor, i => editingColor = i);
            Color c = editingColor == 0 ? primary : secondary;
            y.Ozel("kurulum-renkler", RenkPaletiKur);
            for (int i = 0; i < kurulumRenkler.Length; i++)
                if (kurulumRenkler[i] != null) U.Sinif(kurulumRenkler[i], "secili", Benzer(Presets[i], c));
            y.Kaydirici("Kırmızı", c.r, 0f, 1f, Mathf.RoundToInt(c.r * 255f).ToString(), v => RenkKanali(0, v));
            y.Kaydirici("Yeşil", c.g, 0f, 1f, Mathf.RoundToInt(c.g * 255f).ToString(), v => RenkKanali(1, v));
            y.Kaydirici("Mavi", c.b, 0f, 1f, Mathf.RoundToInt(c.b * 255f).ToString(), v => RenkKanali(2, v));

            y.Bolum("Önizleme");
            y.Ozel("kurulum-onizleme", OnizlemeKur);
            OnizlemeYenile();

            y.Bolum("Zorluk");
            int zi = Array.IndexOf(Difficulty.Levels, setupDifficulty);
            var zorluklar = new string[Difficulty.Levels.Length];
            for (int i = 0; i < zorluklar.Length; i++) zorluklar[i] = Difficulty.Name(Difficulty.Levels[i]);
            y.Secim(zorluklar, zi, i => setupDifficulty = Difficulty.Levels[i]);
            y.Kucuk(Difficulty.Description(setupDifficulty));

            y.Bolum("Oyun türü");
            y.Secim(Senaryo.Adlar, setupSenaryo, i => setupSenaryo = i, true);
            string ek = setupSenaryo == (int)Senaryo.Tur.GunlukMeydan ? $" Bugünün şehri: #{Senaryo.BugununTohumu()}." : "";
            y.Kucuk(Senaryo.Aciklamalar[setupSenaryo] + ek);
            y.Bolum("Olay anlatıcısı");
            y.Secim(Anlatici.Adlar, setupAnlatici, i => setupAnlatici = i);
            y.Kucuk(Anlatici.Aciklamalar[setupAnlatici] + " Oyun sırasında Ayarlar'dan değiştirilebilir.");

            y.Bosluk(10);
            y.Dugme("Şirketi Kur ve Başla", KurulumBasla, true, "vurgu kurulum-basla");
            if (Application.isEditor || Debug.isDebugBuild) TestBolumu(y);
            y.Bitir();
        }

        void KayitliOyunlar(Yazici y)
        {
            y.KartBasla();
            y.Baslik("Kayıtlı oyunlar");
            for (int i = 0; i < SaveSystem.SlotCount; i++)
            {
                int slot = i;
                var sd = slotSaves[i];
                if (sd == null) { y.KV($"{i + 1}. yuva", "boş"); continue; }
                bool eski = SaveSystem.IsOld(sd);
                y.KV($"{i + 1}. yuva", (eski ? "ESKİ SÜRÜM · " : "") + SaveSystem.Summary(sd), eski ? U.Uyari : (Color?)null);
                y.SatirBasla();
                if (confirmDeleteSlot == i)
                {
                    y.Dugme("Evet, sil", () => deferred = () =>
                    {
                        SaveSystem.DeleteSlot(slot);
                        confirmDeleteSlot = -1;
                        newGameSlot = -1;
                        setupSaveChecked = false;
                    }, true, "kucuk-dg");
                    y.Dugme("Vazgeç", () => confirmDeleteSlot = -1, true, "kucuk-dg");
                }
                else
                {
                    bool sor = eski && confirmOldSlot != slot;
                    y.Dugme(sor || !eski ? "Devam et" : "Yine de aç", () =>
                    {
                        if (sor) { confirmOldSlot = slot; return; }
                        deferred = () =>
                        {
                            confirmOldSlot = -1;
                            SaveSystem.Slot = slot;
                            if (!GameBootstrap.I.LoadGame()) setupSaveChecked = false;
                        };
                    }, true, "vurgu kucuk-dg");
                    y.Dugme("Sil", () => confirmDeleteSlot = slot, true, "kucuk-dg");
                }
                y.SatirBitir();
                if (confirmOldSlot == i)
                    y.Uyari("Bu kayıt oyunun eski bir sürümünden: o zamandan beri şehir planı ve bazı kurallar değişti. Açılır ama dükkanlar ve kuryeler " +
                            "yanlış yerde çıkabilir, denge tutmayabilir. Yeni oyun önerilir; yine de açmak için \"Yine de aç\"a bas.");
            }
            y.KartBitir();
        }

        void KurulumBasla()
        {
            Difficulty.Level = setupDifficulty;
            Difficulty.LastChoice = setupDifficulty;
            Senaryo.Secili = (Senaryo.Tur)setupSenaryo;
            Anlatici.Secili = (Anlatici.Tur)setupAnlatici;
            CompanyProfile.Name = string.IsNullOrEmpty(companyName.Trim()) ? "Hızlı Kurye" : companyName.Trim();
            CompanyProfile.Primary = primary;
            CompanyProfile.Secondary = secondary;
            if (newGameSlot >= 0) SaveSystem.Slot = newGameSlot;
            deferred = () => GameBootstrap.I.StartGame();
        }

        // ------------------------------------------------------------------ renkler ve önizleme

        static bool Benzer(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.02f;

        void RenkSec(Color p)
        {
            p.a = 1f;
            if (editingColor == 0) primary = p; else secondary = p;
        }

        void RenkKanali(int kanal, float v)
        {
            Color c = editingColor == 0 ? primary : secondary;
            if (kanal == 0) c.r = v; else if (kanal == 1) c.g = v; else c.b = v;
            RenkSec(c);
        }

        VisualElement RenkPaletiKur()
        {
            var sira = U.El(null, "kurulum-renkler");
            for (int i = 0; i < Presets.Length; i++)
            {
                var renk = Presets[i];
                var kutu = U.El(sira, "kurulum-renk");
                U.Zemin(kutu, renk);
                U.Tikla(kutu, () => RenkSec(renk));
                kurulumRenkler[i] = kutu;
            }
            return sira;
        }

        VisualElement OnizlemeKur()
        {
            onizZemin = U.El(null, "kurulum-onizleme");
            onizLogoDis = U.El(onizZemin, "kurulum-logo");
            onizLogoIc = U.El(onizLogoDis, "kurulum-logo-ic");
            onizHarf = U.Kalin(onizLogoIc, "K", "kurulum-harf");
            var metin = U.El(onizZemin, "kurulum-oniz-metin");
            onizAd = U.Kalin(metin, "", "kurulum-oniz-ad");
            onizAlt = U.Yazi(metin, "Kurye montu, çanta ve araçlar bu renklerde olacak", "kurulum-oniz-alt");
            onizKurye = U.El(onizZemin, "kurulum-kurye");
            onizKuryeIc = U.El(onizKurye, "kurulum-kurye-ic");
            return onizZemin;
        }

        void OnizlemeYenile()
        {
            if (onizZemin == null) return;
            U.Zemin(onizZemin, primary);
            U.Zemin(onizLogoDis, secondary);
            U.Zemin(onizLogoIc, primary);
            U.Zemin(onizKurye, secondary);
            U.Zemin(onizKuryeIc, primary);
            string ad = companyName.Trim();
            U.Metin(onizHarf, string.IsNullOrEmpty(ad) ? "K" : ad.Substring(0, 1).ToUpper());
            U.Metin(onizAd, ad);
            U.Renk(onizHarf, secondary);
            U.Renk(onizAd, secondary);
            U.Renk(onizAlt, secondary);
        }

        // ------------------------------------------------------------------ geliştirici: otomatik test

        void TestBolumu(Yazici y)
        {
            y.Bosluk(10);
            y.KartBasla();
            y.Onay("Geliştirici: otomatik test (bot oynar, rapor yazar)", testBolumuAcik, v => testBolumuAcik = v);
            if (testBolumuAcik)
            {
                y.Kucuk("Bot yeni bir oyunu yüksek hızda oynar: kartları cevaplar, kurye alır, dükkanla anlaşır, borcu öder. " +
                        "Olan her şeyi dosyaya yazar, sonunda pencerelerin ekran görüntülerini alır ve rapor klasörünü açar. " +
                        "Kayıtlarına dokunmaz (test boyunca kayıt kapalı). Bitince oradaki .zip dosyasını gönder.");
                y.Bolum("Süre");
                var gunler = new string[TestModu.GunSecenekleri.Length];
                for (int i = 0; i < gunler.Length; i++) gunler[i] = $"{TestModu.GunSecenekleri[i]} gün";
                y.Secim(gunler, Array.IndexOf(TestModu.GunSecenekleri, TestModu.HedefGun), i => TestModu.HedefGun = TestModu.GunSecenekleri[i]);
                y.Bolum("Hız");
                var hizlar = new string[TestModu.HizSecenekleri.Length];
                int hi = -1;
                for (int i = 0; i < hizlar.Length; i++)
                {
                    hizlar[i] = $"{TestModu.HizSecenekleri[i]:0}×";
                    if (Mathf.Approximately(TestModu.Hiz, TestModu.HizSecenekleri[i])) hi = i;
                }
                y.Secim(hizlar, hi, i => TestModu.Hiz = TestModu.HizSecenekleri[i]);
                y.Bolum("Bot");
                var turler = (TestModu.BotTuru[])Enum.GetValues(typeof(TestModu.BotTuru));
                var turAdlari = new string[turler.Length];
                for (int i = 0; i < turler.Length; i++) turAdlari[i] = TestModu.TurAdi(turler[i]);
                y.Secim(turAdlari, Array.IndexOf(turler, TestModu.Tur), i => TestModu.Tur = turler[i]);
                if (TestModu.Tur == TestModu.BotTuru.Akilli)
                    y.Kucuk("Akıllı bot kartları okuyup maliyetine ve duruma göre seçer; iptal saatlerine uygun kurye, kârlı dükkan, " +
                            "ihtiyaca göre geliştirme, ekipman teklifi, pazarlık, fiyat ve kendi marketi kullanır. Her kararın nedeni dosyaya yazılır.");
                // Gece saatleri hızlı geçtiği için gün biraz daha kısa sürer
                float dkGun = GameClock.I != null ? GameClock.I.minutesPerSecond : 2f;
                float tahminDk = 1440f / (dkGun * TestModu.Hiz) * 0.85f * TestModu.HedefGun / 60f;
                y.Kucuk($"Zorluk: yukarıdaki seçim ({Difficulty.Name(setupDifficulty)}). Tahmini süre: ~{Mathf.Max(1f, tahminDk):0} dakika " +
                        "(bilgisayar yavaşsa daha uzun). Test sürerken Unity açık kalsın, bilgisayar uyumasın.");
                y.Dugme("Otomatik testi başlat", () => deferred = () =>
                {
                    Difficulty.Level = setupDifficulty;
                    CompanyProfile.Name = "Test Kurye";
                    CompanyProfile.Primary = primary;
                    CompanyProfile.Secondary = secondary;
                    TestModu.Baslat();
                }, true, "vurgu");
            }
            y.KartBitir();
        }
    }
}
