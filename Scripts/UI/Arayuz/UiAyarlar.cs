using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Ayarlar penceresi (UI Toolkit, Yazıcı ile; eski IMGUI DrawSettings, DrawInterfaceSettings, DrawGraphicsSettings,
    /// DrawFeedbackSettings ve DrawAnlaticiAyari yerine). Üstte (kaymaz) sayfalar: Oyun · Ekonomi · Harita · Gelişmiş
    /// (sayfa GameUI.settingsPage'de; test modu her sayfanın görüntüsünü alır).
    /// Açık/kapalı ayarlar onay kutusu, seçimler şerit, sayılar kaydırıcı. Ekonomi sayfasında örnek siparişin hesabı sayı kutularında.
    /// </summary>
    public partial class GameUI
    {
        class UiAyarlar : UiSekme
        {
            Yazici y;
            U.Secenekler sayfalar;
            int gosterilenSayfa = -1;
            readonly List<string> kaliteAdlari = new List<string>();
            readonly List<string> havaAdlari = new List<string>();
            readonly List<WeatherKind> havalar = new List<WeatherKind>();
            static readonly string[] Acilar = { "0°", "90°", "180°", "270°" };
            static readonly string[] KarakterModlari = { "Işıklı", "Düz renk", "Kapalı (daire)" };
            static readonly string[] AracRenkModlari = { "Karışık (her kurye kendi rengi)", "Şirket rengi", "Tek fabrika rengi" };

            protected override void Kur()
            {
                sayfalar = U.SecenekEkle(Ust(), new[] { "Oyun", "Ekonomi", "Harita", "Gelişmiş" }, () => ui.settingsPage, v => { ui.settingsPage = v; Yenile(); });
                sayfalar.kok.style.marginTop = 2;
                sayfalar.kok.style.marginBottom = 8;
                y = new Yazici(ui, kok);
                kaliteAdlari.Add("Otomatik");
                kaliteAdlari.AddRange(GrafikKalitesi.Adlar);
                foreach (WeatherKind wk in System.Enum.GetValues(typeof(WeatherKind))) { havalar.Add(wk); havaAdlari.Add(WeatherName(wk)); }
            }

            public override void Yenile()
            {
                var e = EconomyManager.I;
                if (e == null) return;
                sayfalar.Yenile();
                if (gosterilenSayfa != ui.settingsPage)
                {
                    gosterilenSayfa = ui.settingsPage;
                    var sv = kok.GetFirstAncestorOfType<UnityEngine.UIElements.ScrollView>();
                    if (sv != null) sv.scrollOffset = Vector2.zero;
                }
                y.Basla();
                switch (ui.settingsPage)
                {
                    case 0: Oyun(e); break;
                    case 1: Ekonomi(e); break;
                    case 2: Harita(); break;
                    default: Gelismis(); break;
                }
                y.Bitir();
            }

            static int AciIndeksi(int aci) => Mathf.Clamp(aci / 90, 0, 3);

            // ------------------------------------------------------------------ Oyun

            void Oyun(EconomyManager e)
            {
                y.Bolum("Arayüz",
                        $"Kilitli menüler: Gelişim {Guide.GelisimDay}. günde, Kampanya {Guide.KampanyaDay}., Grafik {Guide.GrafikDay}., Destek {Guide.DestekDay}., " +
                        $"Market {Guide.MarketDay}. günde (ya da şartı gelince), Yorumlar ilk müşteri yorumuyla açılır. \"Bütün menüleri baştan aç\" hepsini hemen açar.");
                y.Kucuk("Arayüz boyutu");
                y.Secim(UiSizeNames, UiSizeSetting, v => UiSizeSetting = v);
                y.Onay("Pencere açılınca haritayı sağa kaydır (pencere haritayı kapatmaz)", PushMapSetting, v => PushMapSetting = v);
                y.Onay("Haritada teslimat kârını göster (uçan \"+0,88 €\" yazıları)", ShowPopsSetting, v => ShowPopsSetting = v);
                y.Onay("Sesler (teslimat, bildirim, kilometre taşı)", GameSounds.Enabled, v => GameSounds.Enabled = v);
                if (GameSounds.Enabled)
                {
                    y.Kaydirici("Ses seviyesi", GameSounds.Volume, 0f, 1f, $"%{GameSounds.Volume * 100f:0}", v => GameSounds.Volume = v);
                    y.Dugme("Sesi dene", () => GameSounds.Play(Sfx.Iyi), true, "kucuk-dg");
                }
                y.Onay("Açıklamaları \"?\" balonu yerine hep açık göster (Kayıtlar)", ShowAllDetails, v => ShowAllDetails = v);
                y.Onay("Bütün menüleri baştan aç (deneyimli oyuncular için)", Guide.AllMenusSetting, v => Guide.AllMenusSetting = v);
                var g = Guide.I;
                if (g != null)
                    y.Dugme(g.TutorialDone ? "Başlangıç rehberini yeniden göster" : "Başlangıç rehberini kapat",
                            () => { if (g.TutorialDone) g.RestartTutorial(); else g.FinishTutorial(); }, true, "kucuk-dg");
                y.Kucuk($"Zorluk: {Difficulty.Name(Difficulty.Level)} (yeni oyun kurarken seçilir)");

                y.Bolum("Olay anlatıcısı");
                y.Secim(Anlatici.Adlar, (int)Anlatici.Secili, v => Anlatici.Secili = (Anlatici.Tur)v);
                y.Kucuk(Anlatici.Aciklamalar[(int)Anlatici.Secili]);
                if (Senaryo.Secili != Senaryo.Tur.Klasik)
                    y.Kucuk($"Oyun türü: {Senaryo.Adlar[(int)Senaryo.Secili]}" + (Senaryo.HedefVar ? $" · {Senaryo.IlerlemeYazisi}" : ""));

                y.Bolum("Zaman ve raporlar");
                y.Onay($"Gece 00:00–05:00 arası zaman kendiliğinden {GameClock.NightSpeed:0} kat hızlı geçsin (en fazla 64×)", GameClock.NightFast, v => GameClock.NightFast = v);
                y.Onay("Her gece gün sonu raporunu göster (oyun duraklar)", e.showDailyReport, v => e.showDailyReport = v);
                y.Onay("Her Pazar gecesi haftalık raporu göster", e.showWeeklyReport, v => e.showWeeklyReport = v);

            }

            // ------------------------------------------------------------------ Ekonomi

            void Ekonomi(EconomyManager e)
            {
                y.Bolum("Fiyatlandırma (müşteriden alınan teslimat ücreti)",
                        "Teslimat ücreti = taban + km ücreti × km. Bu, siparişi büyük ihtimalle götürecek kuryenin " +
                        "(o ağırlığı taşıyabilen ve müsait en ucuz kurye) ücreti + min. kâr − komisyonu karşılamıyorsa fark 'ek' olarak eklenir. " +
                        "Zararına iş yine yapılmaz: kâr bırakmayan kuryeye sipariş atanmaz, uygun kurye beklenir. Ücret yükseldikçe vazgeçen müşteri artar.\n\n" +
                        $"Atamada min. kâr: kuryeye ödeme sonrası şirkete en az bu kadar kalmalı. {e.relaxAfterMinutes:0} dakikadan uzun bekleyen " +
                        "siparişte şart 0'a iner, böylece sipariş iptal olmaz.");
                y.Kaydirici("Taban ücret", e.baseFee, 200, 1500, Ft(e.baseFee), v => e.baseFee = Mathf.RoundToInt(v / 10f) * 10);
                y.Kaydirici("Km başı", e.feePerKm, 0, 400, Ft(e.feePerKm), v => e.feePerKm = Mathf.RoundToInt(v / 10f) * 10);
                y.Kaydirici("Min. kâr / sipariş", e.minProfit, 0, 1000, Ft(e.minProfit), v => e.minProfit = Mathf.RoundToInt(v / 50f) * 50);
                y.Kaydirici("Atamada min. kâr", e.minAssignProfit, 0, 600, Ft(e.minAssignProfit), v => e.minAssignProfit = Mathf.RoundToInt(v / 10f) * 10);

                // Örnek sipariş: ayarların kâra etkisi tek bakışta
                const int sepet = 5000;
                const int kare = 15;
                int komisyon = Mathf.RoundToInt(sepet * 0.22f);
                int ucret = e.QuoteDeliveryFee(komisyon, kare, 2f);
                int odeme = e.ExpectedCourierPay(kare, 2f);
                int kar = komisyon + ucret - odeme;
                y.Kucuk($"Örnek sipariş (şu anki ayarlarla): sepet {Ft(sepet)}, komisyon %22, mesafe {GameData.KmText(kare)}");
                y.KutularBasla();
                y.Kutu("Komisyon", Ft(komisyon));
                y.Kutu("Teslimat ücreti", Ft(ucret));
                y.Kutu("Kuryeye", "−" + Ft(odeme), "muhtemel kurye");
                y.Kutu("Kâr", Ft(kar), null, MoneyColor(kar));
                y.KutularBitir();

                // Hizmet seviyeleri: ekspres ve planlı teslimat
                var hz = HizmetSeviyeleri.I;
                if (hz != null)
                {
                    y.Bolum("Hizmet seviyeleri",
                            "Standart teslimatın yanında müşteriye iki seçenek daha sunabilirsin.\n\n" +
                            $"Ekspres: müşteri ek ücret öder, sipariş {HizmetSeviyeleri.EkspresSoz:0} dakikada teslim sözüyle gelir ve atamada öne geçer. " +
                            "Sadece 30 dakikada yetişebilecek siparişlerde (hazırlık ve yol kısa) sunulur. Aceleci müşteriler daha çok seçer, ücret arttıkça seçen azalır. " +
                            "Geç kalırsan ekspres ücreti müşteriye iade edilir ve puan daha çok düşer; zamanında gelirse müşteri çok memnun kalır.\n\n" +
                            "Planlı teslimat: müşteri 1–2 saat sonraki bir saat aralığını seçer, küçük bir indirim alır. Yoğun saatte \"kurye yok\" görüp " +
                            "vazgeçecek müşterilerin bir kısmı planlıya döner (sipariş kaçmaz); yoğun saatte bazıları da kendiliğinden sonraki saati seçer. " +
                            "Planlı sipariş saati gelene kadar kuryeye atanmaz: talep yoğun saatten sakin saate kayar.");
                    y.Onay($"Ekspres teslimat ({HizmetSeviyeleri.EkspresSoz:0} dk garanti)", hz.ekspresAcik, v => hz.ekspresAcik = v);
                    if (hz.ekspresAcik)
                    {
                        y.Kaydirici("Ekspres ücreti", hz.ekspresUcret, 200, 2000, Ft(hz.ekspresUcret), v => hz.ekspresUcret = Mathf.RoundToInt(v / 50f) * 50);
                        int ekBugun = hz.ekspresBugun, ekDun = hz.ekspresDun;
                        y.KV("Bugün / dün", $"{ekBugun} ekspres ({hz.ekspresGecBugun} geç) · dün {ekDun} ({hz.ekspresGecDun} geç)");
                        y.KV("Ekspres geliri (dün)", $"{Ft(hz.ekspresGelirDun)} − iade {Ft(hz.ekspresIadeDun)}");
                        if (ekDun >= 5 && hz.ekspresGecDun > ekDun * 0.25f)
                            y.Uyari("Dün ekspres siparişlerin dörtte birinden fazlası geç kaldı: iadeler geliri yiyor ve puanı düşürüyor. Kurye sayısını artır ya da ücreti yükselt.");
                    }
                    y.Onay("Planlı teslimat (müşteri sonraki bir saati seçebilir)", hz.planliAcik, v => hz.planliAcik = v);
                    if (hz.planliAcik)
                    {
                        y.Kaydirici("Planlı indirimi", hz.planliIndirim, 0, 800, Ft(hz.planliIndirim), v => hz.planliIndirim = Mathf.RoundToInt(v / 50f) * 50);
                        y.KV("Bugün / dün", $"{hz.planliBugun} planlı ({hz.planliKurtarilanBugun} yoğunluktan kurtarıldı) · dün {hz.planliDun} ({hz.planliKurtarilanDun})");
                    }
                }

                y.Bolum("Kurye ücretleri (araca göre)");
                y.Tablo(new[] { "Araç", "Paket başı", "Km başı" }, true);
                foreach (var v in GameData.Vehicles)
                    y.Tablo(new[] { v.name, Ft(v.basePay), Ft(v.payPerKm) }, false, null, null, $"{v.name} taşır: {GameData.CarryText(v)}");

                y.Bolum("Kurye bonusları", "Yüksek bonus moral ve daha çok kurye kaydı getirir ama kârı düşürür. Bonus = hava bonusu + saat bonusu, çarpanla çarpılır.");
                y.Kaydirici("Bonus çarpanı", e.bonusMultiplier, 0.5f, 2f, $"×{e.bonusMultiplier:0.00}", v => e.bonusMultiplier = Mathf.Round(v * 100f) / 100f);
                y.KV("Şu anki bonus", $"{Ft(e.CurrentBonus)} (hava {Ft(WeatherSystem.I != null ? WeatherSystem.I.WeatherBonus : 0)} + saat {Ft(e.TimeBonus())})");

                y.Bolum("Vergi", Sade.SurprizVergi
                    ? "31 Aralık'ta (gelir − giderler) üzerinden ödenir; giderler vergiden düşülür. Yıl içinde de habersiz vergi ve harçlar çıkar: " +
                      "yerel işletme vergisi, KDV farkı, vergi denetimi, belediye harcı, iş güvenliği fonu. Bazen de vergi iadesi gelir. Kasanda her zaman biraz yedek para tut."
                    : "Yıl sonunda yılın kârı (gelir − giderler) üzerinden kademeli vergi ödenir; giderler vergiden düşülür. " +
                      "3 ayda bir geçici vergi peşin istenir (Finans > Vergi).");
                y.Kucuk(EconomyManager.TaxBracketText);
                y.Yardim("Kârın her dilimi kendi oranıyla vergilenir: şirket büyüdükçe kârın üst kısmından daha yüksek oranda vergi ödenir.");
            }

            // ------------------------------------------------------------------ Harita

            void Harita()
            {
                y.Bolum("Grafik", "Oyun kasıyorsa kaliteyi düşür. Otomatik: oyun kare hızına bakıp kendisi ayarlar. " +
                                  "Yüksek: yumuşak gölgeler, ortam gölgesi, ışıma. Düşük: sert gölgeler, daha düşük çözünürlük, gece daha az lamba ışığı.");
                y.Secim(kaliteAdlari, GrafikKalitesi.Ayar == GrafikKalitesi.Otomatik ? 0 : GrafikKalitesi.Ayar + 1,
                        v => GrafikKalitesi.Ayar = v == 0 ? GrafikKalitesi.Otomatik : v - 1);
                string fps = GrafikOlcer.I != null && GrafikOlcer.I.Fps > 0f ? $" · şu an ~{GrafikOlcer.I.Fps:0} FPS" : "";
                string oto = GrafikKalitesi.Ayar == GrafikKalitesi.Otomatik ? $"Otomatik → {GrafikKalitesi.Adlar[GrafikKalitesi.Seviye]}" : GrafikKalitesi.Adlar[GrafikKalitesi.Seviye];
                y.Kucuk(Gorsel3D.Active ? $"Geçerli: {oto}{fps}" : $"3D ışık kapalı (eski düz görünüm){fps}");
                y.Onay("Çayır çimi: yakından bakınca rüzgârla dalgalanan çimen ve kır çiçekleri (kapatmak performansı artırır)", Cim.Acik, v => Cim.Acik = v);
                if (Cim.Acik)
                    y.Onay("Gerçekçi çim (deneme): yaprak geometrisi yerine dokulu çim kartları; hemen uygulanır", Cim.Gercekci, v => Cim.Gercekci = v);
                y.Onay("Gerçek ışık ve gölge (3D). Kapatınca eski düz görünüm; oyunu yeniden başlatınca uygulanır", Gorsel3D.Enabled, v => Gorsel3D.Enabled = v);

                y.Bolum("Harita");
                y.Onay("Yer adlarını göster (park, nehir, AVM, bölgeler)", ShowLandmarks, v => ShowLandmarks = v);
                y.Onay("Gece ve gündüz: akşam turuncu ışık, gece karanlık, pencere ışıkları ve sokak lambaları", DayNight.Enabled, v => DayNight.Enabled = v);
                y.Onay("Hava efektleri: yağmur, kar, rüzgâr, güneş, şimşek, kar örtüsü", WeatherFx.Enabled, v => WeatherFx.Enabled = v);
                if (WeatherFx.Enabled && WeatherSystem.I != null)
                {
                    y.Kucuk("Havayı dene:");
                    y.Secim(havaAdlari, havalar.IndexOf(WeatherSystem.I.Current), v => WeatherSystem.I.Restore(havalar[v]), true);
                }
                y.Onay("Trafik: arabalar, otobüsler, yayalar (kapatmak performansı artırır)", Traffic.Enabled,
                       v => { Traffic.Enabled = v; if (Traffic.I != null) Traffic.I.Build(); });
                if (Traffic.Enabled)
                {
                    int nv = Traffic.PackVehicleCount, np = Traffic.PackPeopleCount;
                    y.Kucuk(nv + np > 0
                        ? $"Şehir paketinden {nv} araç ve {np} insan modeli kullanılıyor."
                        : "Şehir paketinin araç ve insanları kurulu değil: basit kutular kullanılıyor. (Kurye Tycoon > Şehir Paketini Kur'u tekrar çalıştır.)");
                    if (nv > 0)
                    {
                        y.Kucuk("Araç yönü düzeltme (burnu ters gidiyorsa):");
                        y.Secim(Acilar, AciIndeksi(Traffic.FacingOffset), v => { Traffic.FacingOffset = v * 90; if (Traffic.I != null) Traffic.I.Build(); });
                    }
                }
                y.Onay("Perspektif kamera (derinlik hissi; kapalıysa düz/ortografik)", MapCamera.PerspectiveSetting, v => MapCamera.PerspectiveSetting = v);
            }

            // ------------------------------------------------------------------ Gelişmiş

            void Gelismis()
            {
                y.Bolum("3D şehir paketi", "Şehir paketi kurulumu ve bina/yol yönleri. Geliştirme sırasında paket kurmak için.");
                int nb = City3D.PackBuildingCount, nt = City3D.PackTreeCount;
                if (nb == 0)
                    y.Kucuk("Şehir paketi kurulu değil: yakınlaştırınca basit kutu binalar görünür. Kurmak için Asset Store'dan şehir paketini " +
                            "(ör. SimplePoly City) içe aktar, sonra Unity menüsünden Kurye Tycoon > Şehir Paketini Kur. (Ayrıntı KURULUM.md'de.)");
                else
                {
                    y.Kucuk($"Şehir paketi: {nb} bina, {nt} ağaç.");
                    y.Onay("Paketin binalarını kullan (kapalıysa kutu binalar)", City3D.UsePack, v => { City3D.UsePack = v; if (City3D.I != null) City3D.I.Rebuild(); });
                    y.Kucuk("Bina cephesi düzeltme (arkası yola bakıyorsa 180°, yanı bakıyorsa 90° ya da 270°):");
                    y.Secim(Acilar, AciIndeksi(City3D.FrontOffset), v => { City3D.FrontOffset = v * 90; if (City3D.I != null) City3D.I.Rebuild(); });
                    if (City3D.PackRoadsOk)
                    {
                        y.Onay("Paketin yollarını ve kaldırımlarını kullan", City3D.UseRoads, v => { City3D.UseRoads = v; if (City3D.I != null) City3D.I.Rebuild(); });
                        if (City3D.UseRoads)
                        {
                            string[] turler = { "Düz yol", "Köşe", "T kavşak" };
                            for (int k = 0; k < 3; k++)
                            {
                                int tur = k;
                                y.Kucuk($"{turler[k]} yönü:");
                                y.Secim(Acilar, AciIndeksi(City3D.RoadOffset(tur)), v => { City3D.SetRoadOffset(tur, v * 90); if (City3D.I != null) City3D.I.Rebuild(); });
                            }
                            y.Kucuk("Yol çizgileri yola dik duruyorsa ya da köşeler/kavşaklar yanlış yöne açılıyorsa ilgili türün yönünü değiştir.");
                        }
                    }
                }

                y.Bolum("Kurye karakterleri (3D)");
                if (CharacterVisuals.StilizeVar)
                {
                    y.Onay("Stilize kurye (oyunun kendi modeli; bisikletliler bisiklette)", CharacterVisuals.Stilize,
                           v => { CharacterVisuals.Stilize = v; CharacterVisuals.Refresh(); });
                    y.Kucuk("Kapalıyken kuryeler eski karakter paketinden (City People) karışık görünür. Rakip kuryeler ve dükkan çalışanları hep o paketten.");
                }
                int adet = CharacterVisuals.Count;
                if (adet == 0)
                {
                    if (!CharacterVisuals.StilizeVar)
                        y.Kucuk("Karakter modeli bulunamadı; kuryeler daire olarak çiziliyor. Kurmak için: Asset Store'dan karakter paketini içe aktar, " +
                            "sonra Unity menüsünden Kurye Tycoon > Karakterleri Kur. (Ayrıntı KURULUM.md'de.)");
                }
                else
                {
                    if (CharacterVisuals.HasWalkAnimation) y.Kucuk($"{adet} karakter modeli yüklü · yürüme animasyonu kurulu");
                    else y.Uyari($"{adet} karakter modeli yüklü · yürüme animasyonu KURULU DEĞİL (Unity menüsü: Kurye Tycoon > Karakter Animasyonlarını Kur)");
                }
                if (adet > 0 || CharacterVisuals.StilizeVar)
                {
                    y.Secim(KarakterModlari, CharacterVisuals.Mode, v => { CharacterVisuals.Mode = v; CharacterVisuals.Refresh(); });
                    y.Kucuk("Karakterler siyah/karanlık görünürse \"Düz renk\"i seç. Pembe görünürse malzemeler projenin render pipeline'ına çevrilmeli (KURULUM.md).");
                }

                y.Bolum("Kurye arabaları (VW Polo 6N2)");
                if (!CharacterVisuals.CarAvailable)
                    y.Kucuk("Araç modeli bulunamadı (Unity menüsü: Kurye Tycoon > Blender Binalarını Prefab Yap).");
                else
                {
                    y.Kucuk("Arabalı kuryeler haritada Polo ile gezer: tekerler döner, durunca kapı ve bagaj açılır, yağışta silecekler çalışır, " +
                            $"karanlıkta farlar yanar. Renk: Polo 6N2'nin {AracRenkleri.Polo6N2.Length} fabrika renginden.");
                    y.Secim(AracRenkModlari, AracRenkleri.Mod, v => { AracRenkleri.Mod = v; CharacterVisuals.Refresh(); }, true);
                    if (AracRenkleri.Mod == 2)
                    {
                        int i = AracRenkleri.Secili;
                        var rk = AracRenkleri.Polo6N2[i];
                        y.SatirBasla();
                        y.Dugme("◀", () => { AracRenkleri.Secili--; CharacterVisuals.Refresh(); }, true, "kucuk-dg");
                        y.Yazi($"<color=#{ColorUtility.ToHtmlStringRGB(AracRenkleri.Renk(i))}>■■■</color>  {rk.ad} ({rk.kod}) · {i + 1}/{AracRenkleri.Polo6N2.Length}", "kucuk");
                        y.Dugme("▶", () => { AracRenkleri.Secili++; CharacterVisuals.Refresh(); }, true, "kucuk-dg");
                        y.SatirBitir();
                    }
                }

                y.Bolum("Hata ayıklama raporu",
                        "Oyunun o anki bütün durumunu (kasa, gider dökümü, günlük geçmiş, kuryeler, dükkanlar, siparişler, geliştirmeler, " +
                        "iptal ve kaçan sipariş sebepleri, son bildirimler ve kaydın tamamı) tek bir metin dosyasına yazar. " +
                        "Sorunları bulmak için bu dosyayı gönder. Kısayol: F9.");
                y.SatirBasla();
                y.Dugme("Hata ayıklama raporu oluştur", () => { if (DebugReport.I != null) DebugReport.I.Write(true); }, true, "vurgu");
                y.Dugme("Klasörü aç", DebugReport.OpenFolder, true, "kucuk-dg");
                y.SatirBitir();
                y.Kucuk(DebugReport.I != null && DebugReport.I.lastPath != "" ? $"Son rapor: {DebugReport.I.lastPath}" : $"Klasör: {DebugReport.Folder}");

                var stats = StatsHistory.I;
                if (stats != null)
                {
                    y.Bolum("Denge kaydı (CSV)",
                            "Teslimat, iptal, gelir, gider, kâr, kasa, itibar, kurye ve dükkan sayısı her gece bir satır olarak yazılır. " +
                            "Excel ya da Google Sheets ile açılır (ayraç ';'). Her oyun oturumu yeni bir dosya açar, kayıttan devam edilen " +
                            "oyunun eski günleri de içine yazılır.");
                    y.Onay("Her gün sonunda verileri CSV dosyasına yaz (test ve denge için)", stats.writeCsv, v => stats.writeCsv = v);
                    y.Kucuk(string.IsNullOrEmpty(stats.CsvPath) ? $"Klasör: {StatsHistory.CsvFolder}" : $"Dosya: {stats.CsvPath}");
                    y.Dugme("Klasörü aç", () =>
                    {
                        System.IO.Directory.CreateDirectory(StatsHistory.CsvFolder);
                        Application.OpenURL(new System.Uri(StatsHistory.CsvFolder).AbsoluteUri);
                    }, true, "kucuk-dg");
                }
            }
        }
    }
}
