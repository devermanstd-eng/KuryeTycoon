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
            yield return new WaitForSecondsRealtime(0.3f);   // arayüz tamamen kalksın
            yield return StartCoroutine(TestCek(yol));
            fotoModu = false;
            panelOpen = eskiPanel;
            popupCourier = eskiTelefon;
        }

        /// <summary>Telefonun bütün ekranları (ana ekran ve menü dahil).</summary>
        static readonly PhoneScreen[] TestTelefonEkranlari =
        {
            PhoneScreen.Home, PhoneScreen.Menu, PhoneScreen.Stats, PhoneScreen.Earn, PhoneScreen.Insights, PhoneScreen.Finance,
            PhoneScreen.Insurance, PhoneScreen.Profile, PhoneScreen.Inbox, PhoneScreen.Support, PhoneScreen.PrevTask
        };

        /// <summary>
        /// Test sonunda: ana ekran, açık olan her pencere (uzunsa aşağı kaydırarak birkaç parça; Finans ve Ayarlar'ın her sayfası),
        /// kurye telefonunun bütün ekranları (mümkünse görevdeki bir kuryenin; teslim alma ve teslimat ekranları ayrıca),
        /// büyük kurye kartı ve bina kartı (dükkan ve müşteri binası). Açılmamış menüler kilitli listesine yazılır.
        /// </summary>
        public IEnumerator TestPencereGoruntuleri(string klasor, List<string> kilitli)
        {
            Directory.CreateDirectory(klasor);
            fotoModu = false;
            popupCourier = null;
            CloseInfo();        // arayüz turundan açık kalmış olabilir
            CloseBigCard();
            turKapat = 0f;
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
                        if (UiSekmeVar(t) && pencereKaydir != null) pencereKaydir.scrollOffset = new Vector2(0f, s * 520f);   // UI Toolkit penceresi
                        yield return null;
                        yield return null;   // UI Toolkit düzeni otursun
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

            // Telefon ve bina kartı haritanın üstünde durur. Ekran görüntüsünde IMGUI (harita etiketleri, güneş parlaması, gece tonu)
            // UI Toolkit'in üstüne çıkıyor ve kartı soluk gösteriyordu (16.11 testi); bu çekimlerde IMGUI çizilmez.
            testKartCekimi = true;

            // Kurye telefonu (sağdan kayarak açılır, önce yerine otursun). Mümkünse görevdeki bir kurye: görev ekranı da görünsün.
            if (cm.Couriers.Count > 0)
            {
                Courier alan = null, getiren = null;
                foreach (var k in cm.Couriers)
                {
                    if (k.CurrentOrder == null) continue;
                    if (InPickupPhase(k)) { if (alan == null) alan = k; }
                    else if (getiren == null) getiren = k;
                }
                var tel = getiren ?? alan ?? cm.Couriers[0];
                TelefonAc(tel);
                yield return new WaitForSecondsRealtime(0.8f);
                foreach (var ps in TestTelefonEkranlari)
                {
                    TelefonSayfa(ps);
                    yield return null;
                    yield return null;   // UI Toolkit düzeni otursun
                    yield return StartCoroutine(TestCek(Path.Combine(klasor, $"telefon_{++no:000}_{ps}.jpg")));
                }
                // Teslimat ekranının Ürünler sekmesi ve teslim alma ekranı
                if (getiren != null)
                {
                    TelefonSayfa(PhoneScreen.Home);
                    phoneTaskTab = 1;
                    yield return null;
                    yield return null;
                    yield return StartCoroutine(TestCek(Path.Combine(klasor, $"telefon_{++no:000}_teslimat_urunler.jpg")));
                    phoneTaskTab = 0;
                }
                if (alan != null && alan != tel)
                {
                    TelefonAc(alan);
                    yield return new WaitForSecondsRealtime(0.4f);
                    yield return StartCoroutine(TestCek(Path.Combine(klasor, $"telefon_{++no:000}_teslim_alma.jpg")));
                }
                phoneScreen = PhoneScreen.Home;
                popupCourier = null;

                // Büyük kurye kartı (ortada, perdeli)
                OpenBigCard(cm.Couriers[0]);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return StartCoroutine(TestCek(Path.Combine(klasor, $"pencere_{++no:000}_buyuk_kurye_karti.jpg")));
                CloseBigCard();
            }

            // Bina kartı: sözleşmeli ilk dükkan ve siparişi olan bir müşteri binası
            Shop dukkan = null;
            if (CityMap.I != null)
                foreach (var s in CityMap.I.Shops)
                    if (s.contracted && !s.own) { dukkan = s; break; }
            if (dukkan != null)
            {
                DukkanKartiAc(dukkan);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return StartCoroutine(TestCek(Path.Combine(klasor, $"pencere_{++no:000}_bina_karti_dukkan.jpg")));
                CloseInfo();
            }
            var musteri = MusteriBinasi(0);
            if (musteri.HasValue)
            {
                BinaKartiAc(musteri.Value);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return StartCoroutine(TestCek(Path.Combine(klasor, $"pencere_{++no:000}_bina_karti_musteri.jpg")));
                CloseInfo();
            }
            testKartCekimi = false;
        }

        /// <summary>Test sonunda telefon ve bina kartı çekilirken IMGUI çizilmez (GameUI.OrtaKutuAcik).</summary>
        bool testKartCekimi;

        /// <summary>Siparişi olan bir müşteri binası (sıra: önce yoldaki siparişler, sonra geçmiş). Rastgele sayı kullanmaz.</summary>
        static Vector2Int? MusteriBinasi(int sira)
        {
            var om = OrderManager.I;
            if (om == null) return null;
            int n = om.Active.Count;
            if (n > 0) return om.Active[sira % n].customerTile;
            foreach (var o in om.History) return o.customerTile;
            return null;
        }

        // ------------------------------------------------------------------ arayüz turu (test oynarken)

        float turSonrakiDakika = -1f, turKapat;
        int turSira, turTelefon, turBina;
        readonly HashSet<CourierState> turDurumlar = new HashSet<CourierState>();

        /// <summary>Test özetine: oynarken kurye telefonu ve bina kartı kaç kez, hangi kurye durumlarında açıldı.</summary>
        public string TestTurOzeti => $"Arayüz turu: telefon {turTelefon} kez ({turDurumlar.Count} farklı kurye durumunda: " +
                                       $"{string.Join(", ", turDurumlar)}), bina kartı {turBina} kez";

        /// <summary>
        /// Otomatik test oynarken (her karede çağrılır): oyun saatiyle 3 saatte bir kurye telefonunu ya da bina kartını 1,5 sn açar.
        /// Telefon önce henüz görülmemiş bir durumdaki kuryeye açılır (dükkana gidiyor, bekliyor, kata çıkıyor, adres sorunu…),
        /// teslimattaysa Bilgi ve Ürünler sekmeleri sırayla. Yeni arayüzün kodu kuryenin farklı hallerinde de çalışsın; hata olursa
        /// "Arayüz hatası (…)" diye test raporuna düşer. Oyunun gidişine etkisi yok: yalnızca gösterim, rastgele sayı kullanmaz.
        /// </summary>
        public void TestArayuzTuru()
        {
            var clk = GameClock.I;
            var cm = CourierManager.I;
            if (clk == null || cm == null || !UiAktif) return;
            if (turKapat > 0f)
            {
                if (Time.unscaledTime < turKapat) return;
                turKapat = 0f;
                popupCourier = null;
                CloseInfo();
                phoneTaskTab = 0;
                return;
            }
            if (turSonrakiDakika < 0f) turSonrakiDakika = clk.TotalMinutes + 180f;
            if (clk.TotalMinutes < turSonrakiDakika || pauseMenuOpen || ConfirmOpen || BigCardOpen) return;
            turSonrakiDakika = clk.TotalMinutes + 180f;
            turSira++;

            if (turSira % 3 == 0)
            {
                // Bina kartı: sırayla bir dükkan, bir müşteri binası
                if ((turSira / 3) % 2 == 1 && CityMap.I != null)
                {
                    int sayi = 0;
                    foreach (var s in CityMap.I.Shops) if (s.contracted) sayi++;
                    int hedef = sayi > 0 ? turSira % sayi : -1;
                    foreach (var s in CityMap.I.Shops)
                        if (s.contracted && hedef-- == 0) { DukkanKartiAc(s); turBina++; break; }
                }
                else
                {
                    var t = MusteriBinasi(turSira);
                    if (t.HasValue) { BinaKartiAc(t.Value); turBina++; }
                }
            }
            else if (cm.Couriers.Count > 0)
            {
                // Telefon: önce henüz görülmemiş durumdaki kurye, yoksa sıradaki
                Courier sec = null;
                foreach (var k in cm.Couriers)
                    if (!turDurumlar.Contains(k.State)) { sec = k; break; }
                if (sec == null) sec = cm.Couriers[turSira % cm.Couriers.Count];
                turDurumlar.Add(sec.State);
                TelefonAc(sec);
                phoneTaskTab = turSira % 2;
                turTelefon++;
            }
            turKapat = Time.unscaledTime + 1.5f;
        }
    }
}
