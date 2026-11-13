using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Otomatik testin arayüz tarafı (bkz. Core/TestModu.cs): kurulum ekranındaki bölüm ve ekran görüntüleri.
    /// Pencereler doğrudan açılıp kapatıldığı için GameUI'nin içinde duruyor.
    /// </summary>
    public partial class GameUI
    {
        bool testBolumuAcik;

        /// <summary>Esc menüsü ya da onay penceresi açık mı? (Açıkken bot oyunu devam ettirmez.)</summary>
        public bool TestMenuAcik => pauseMenuOpen || ConfirmOpen;

        /// <summary>Kurulum ekranının altında: yalnızca Editor'de ve geliştirme sürümünde görünür.</summary>
        void DrawTestSetup()
        {
            if (!(Application.isEditor || Debug.isDebugBuild)) return;
            GUILayout.Space(14);
            GUILayout.BeginVertical(box);
            testBolumuAcik = GUILayout.Toggle(testBolumuAcik, " Geliştirici: otomatik test (bot oynar, rapor yazar)", toggle);
            if (testBolumuAcik)
            {
                GUILayout.Label("Bot yeni bir oyunu yüksek hızda oynar: kartları cevaplar, kurye alır, dükkanla anlaşır, borcu öder. " +
                                "Olan her şeyi dosyaya yazar, sonunda pencerelerin ekran görüntülerini alır ve rapor klasörünü açar. " +
                                "Kayıtlarına dokunmaz (test boyunca kayıt kapalı). Bitince oradaki .zip dosyasını gönder.", small);
                GUILayout.Space(4);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Süre", body, GUILayout.Width(60));
                foreach (int g in TestModu.GunSecenekleri)
                {
                    int gun = g;
                    if (SelectButton(TestModu.HedefGun == gun, $"{gun} gün", smallButton)) deferred = () => TestModu.HedefGun = gun;
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Hız", body, GUILayout.Width(60));
                foreach (float h in TestModu.HizSecenekleri)
                {
                    float hiz = h;
                    if (SelectButton(Mathf.Approximately(TestModu.Hiz, hiz), $"{hiz:0}×", smallButton)) deferred = () => TestModu.Hiz = hiz;
                }
                GUILayout.EndHorizontal();

                bool rastgele = GUILayout.Toggle(TestModu.RastgeleSecim, " Kartlarda rastgele seç (kapalıyken hep ilk seçenek)", toggle);
                if (rastgele != TestModu.RastgeleSecim) deferred = () => TestModu.RastgeleSecim = rastgele;

                // Gece saatleri hızlı geçtiği için gün biraz daha kısa sürer
                float dkGun = GameClock.I != null ? GameClock.I.minutesPerSecond : 2f;
                float tahminDk = 1440f / (dkGun * TestModu.Hiz) * 0.85f * TestModu.HedefGun / 60f;
                GUILayout.Label($"Zorluk: yukarıdaki seçim ({Difficulty.Name(setupDifficulty)}). Tahmini süre: ~{Mathf.Max(1f, tahminDk):0} dakika " +
                                "(bilgisayar yavaşsa daha uzun). Test sürerken Unity açık kalsın, bilgisayar uyumasın.", small);
                GUILayout.Space(4);
                if (GUILayout.Button("Otomatik testi başlat", button, GUILayout.Height(34)))
                {
                    deferred = () =>
                    {
                        Difficulty.Level = setupDifficulty;
                        CompanyProfile.Name = "Test Kurye";
                        CompanyProfile.Primary = primary;
                        CompanyProfile.Secondary = secondary;
                        TestModu.Baslat();
                    };
                }
            }
            GUILayout.EndVertical();
        }

        // ---------------------------------------------------------------- ekran görüntüleri

        /// <summary>Son TestCek'in pencere bölgesinin özeti (aynıysa kaydırma bir şey değiştirmemiştir).</summary>
        ulong testSonImza;

        /// <summary>
        /// Karenin sonunda ekranı JPG olarak kaydeder (arayüz dahil). bolge verilirse (arayüz birimiyle) o bölgenin özeti
        /// testSonImza'ya yazılır; atlaImza ile aynıysa dosya yazılmaz (aynı görüntü iki kez çıkmasın).
        /// </summary>
        public IEnumerator TestCek(string yol, Rect? bolge = null, ulong atlaImza = 0)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = null;
            try
            {
                tex = ScreenCapture.CaptureScreenshotAsTexture();
                testSonImza = bolge.HasValue ? BolgeImzasi(tex, bolge.Value) : 0;
                if (atlaImza == 0 || testSonImza != atlaImza)
                    File.WriteAllBytes(yol, tex.EncodeToJPG(88));
            }
            catch (System.Exception ex) { Debug.LogWarning("Test ekran görüntüsü alınamadı: " + ex.Message); }
            if (tex != null) Destroy(tex);
        }

        /// <summary>Arayüz birimindeki bölgenin piksellerinden basit bir özet (FNV); her 3. pikselin her 3. satırı.</summary>
        ulong BolgeImzasi(Texture2D tex, Rect r)
        {
            int x0 = Mathf.Clamp(Mathf.RoundToInt(r.x * uiScale), 0, tex.width - 1);
            int x1 = Mathf.Clamp(Mathf.RoundToInt(r.xMax * uiScale), x0 + 1, tex.width);
            // GUI'de y yukarıdan, dokuda aşağıdan sayılır
            int y0 = Mathf.Clamp(tex.height - Mathf.RoundToInt(r.yMax * uiScale), 0, tex.height - 1);
            int y1 = Mathf.Clamp(tex.height - Mathf.RoundToInt(r.y * uiScale), y0 + 1, tex.height);
            var px = tex.GetPixels32();
            ulong h = 1469598103934665603UL;
            for (int y = y0; y < y1; y += 3)
                for (int x = x0; x < x1; x += 3)
                {
                    var c = px[y * tex.width + x];
                    h = (h ^ c.r) * 1099511628211UL;
                    h = (h ^ c.g) * 1099511628211UL;
                    h = (h ^ c.b) * 1099511628211UL;
                }
            return h == 0 ? 1 : h;
        }

        /// <summary>Arayüz gizli, bütün şehir: haritanın o günkü hali.</summary>
        public IEnumerator TestHaritaGoruntusu(string yol)
        {
            bool eskiPanel = panelOpen;
            var eskiTelefon = popupCourier;
            panelOpen = false;
            popupCourier = null;
            if (MapCamera.I != null) MapCamera.I.ResetView();
            fotoModu = true;
            fotoIpucuBitis = 0f;
            yield return null;
            yield return StartCoroutine(TestCek(yol));
            fotoModu = false;
            panelOpen = eskiPanel;
            popupCourier = eskiTelefon;
        }

        static readonly PhoneScreen[] TestTelefonEkranlari =
            { PhoneScreen.Home, PhoneScreen.Stats, PhoneScreen.Earn, PhoneScreen.Finance, PhoneScreen.Profile, PhoneScreen.Inbox, PhoneScreen.Support };

        /// <summary>
        /// Test sonunda: ana ekran, açık olan her pencere (uzunsa aşağı kaydırarak birkaç parça; Finans ve Ayarlar'ın her sayfası)
        /// ve ilk kuryenin telefonu. Açılmamış menüler kilitli listesine yazılır.
        /// </summary>
        public IEnumerator TestPencereGoruntuleri(string klasor, List<string> kilitli)
        {
            Directory.CreateDirectory(klasor);
            fotoModu = false;
            popupCourier = null;
            panelOpen = false;
            int no = 0;
            yield return null;
            yield return StartCoroutine(TestCek(Path.Combine(klasor, $"pencere_{++no:000}_ana_ekran.jpg")));

            var cm = CourierManager.I;
            float shUi = Screen.height / uiScale;
            foreach (Tab t in System.Enum.GetValues(typeof(Tab)))
            {
                if (!TabOpen(t)) { kilitli?.Add($"{t}: pencere açılmamış (ekran görüntüsü yok)"); continue; }
                int sayfa = t == Tab.Finans ? 5 : t == Tab.Ayarlar ? 4 : 1;
                for (int p = 0; p < sayfa; p++)
                {
                    if (t == Tab.Finans) financePage = p;
                    if (t == Tab.Ayarlar) settingsPage = p;
                    if (t == Tab.Kuryeler && (selectedCourier == null || !cm.Couriers.Contains(selectedCourier)) && cm.Couriers.Count > 0)
                        selectedCourier = cm.Couriers[0];
                    tab = t;
                    panelOpen = true;
                    scroll = Vector2.zero;
                    yield return new WaitForSecondsRealtime(0.4f);   // pencere ve harita kayması otursun, kurye resimleri çizilsin
                    // Pencerenin içi (sağ kenardaki bildirim kartı hariç): aşağı kaydırınca değişmiyorsa sayfa bitmiştir
                    var w = WindowRect(shUi);
                    var ic = new Rect(w.x + 4f, w.y + 4f, w.width - 60f, w.height - 8f);
                    ulong onceki = 0;
                    for (int s = 0; s < 8; s++)
                    {
                        scroll = new Vector2(0f, s * 520f);
                        yield return null;
                        string ad = $"pencere_{++no:000}_{t}" + (sayfa > 1 ? $"_s{p + 1}" : "") + (s > 0 ? $"_asagi{s}" : "") + ".jpg";
                        yield return StartCoroutine(TestCek(Path.Combine(klasor, ad), ic, onceki));
                        if (s > 0 && testSonImza == onceki) { no--; break; }   // aynı görüntü: kaydedilmedi, sayfa bitti
                        onceki = testSonImza;
                    }
                }
            }
            panelOpen = false;
            scroll = Vector2.zero;
            financePage = 0;
            settingsPage = 0;

            // Kurye telefonu (sağdan kayarak açılır, önce yerine otursun)
            if (cm.Couriers.Count > 0)
            {
                popupCourier = cm.Couriers[0];
                yield return new WaitForSecondsRealtime(0.8f);
                foreach (var ps in TestTelefonEkranlari)
                {
                    phoneScreen = ps;
                    popupScroll = Vector2.zero;
                    yield return null;
                    yield return StartCoroutine(TestCek(Path.Combine(klasor, $"telefon_{++no:000}_{ps}.jpg")));
                }
                phoneScreen = PhoneScreen.Home;
                popupCourier = null;
            }
        }
    }
}
