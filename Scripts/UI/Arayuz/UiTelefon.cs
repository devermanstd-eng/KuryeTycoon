using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Kurye telefonu (UI Toolkit; eski IMGUI CourierPhone.cs yerine): haritada kuryeye tıklayınca sağdan kayarak açılan,
    /// gerçek kurye uygulamalarına benzeyen ekran. Pencere katmanında (katPencere), açık pencerenin üstünde durur.
    /// Ekranlar: Ana ekran (harita + alt panel: teklif / aktif görev / yoğunluk; görevdeyken altta sabit durum hapı), Menü,
    /// İstatistikler, Ekstra kazan, Analizler, Finans, Sigorta, Profil, Gelen kutusu, Destek, Önceki görev.
    /// Görev ekranı ve düğmeleri (gecikme bildir, destek, iptal, çevrimdışı saati) UiTelefonGorev.cs'te; parçalar UiTelefonParcalar.cs'te.
    /// İçerik 8 Hz'de (ve tıklayınca hemen) yenilenir; kayma ve harita her karede.
    /// </summary>
    public partial class GameUI
    {
        enum PhoneScreen { Home, Menu, Stats, Earn, Insights, Finance, Insurance, Profile, Inbox, Support, PrevTask }

        PhoneScreen phoneScreen = PhoneScreen.Home;
        int statsTab;               // 0 bugün, 1 hafta, 2 ay
        int supportOpen = -1;       // açık destek konusu
        float phoneAnim;            // 0 → 1: telefon sağdan kayarak gelir
        Courier phoneAnimFor;

        // ---------- Renkler (koyu lacivert, şirket rengi vurgulu) ----------
        static readonly Color PBg = new Color(0.045f, 0.05f, 0.075f);
        static readonly Color PSheet = new Color(0.085f, 0.09f, 0.135f);
        static readonly Color PCard = new Color(0.12f, 0.13f, 0.185f);
        static readonly Color PLine = new Color(1f, 1f, 1f, 0.08f);
        static readonly Color PDim = new Color(0.58f, 0.61f, 0.72f);
        static Color PAccent => Color.Lerp(CompanyProfile.Primary, Color.white, 0.32f);
        static readonly Color PGreen = new Color(0.45f, 0.85f, 0.45f);
        static readonly Color PRed = new Color(0.98f, 0.45f, 0.40f);
        static readonly Color PKoyu = new Color(0.04f, 0.05f, 0.08f);   // açık zemin üstündeki yazı

        // ---------- Haritanın sağındaki kartlar (telefon, bina kartı) ----------
        const float TelGenislik = 340f, BinaGenislik = 370f, SagKartKenar = 18f;

        /// <summary>
        /// Sağdaki kart (kurye telefonu ya da bina kartı) açıksa ekranın sağından kapladığı genişlik (kenar ve boşlukla); yoksa 0.
        /// Sağ üstteki durum kartları ve bildirimler buna göre sola kayar.
        /// </summary>
        float SagKartGenisligi => popupCourier != null ? SagKartKenar + TelGenislik + 12f : InfoOpen ? SagKartKenar + BinaGenislik + 12f : 0f;

        /// <summary>Kurye telefonunu açar (haritada tıklama, Kuryeler penceresi, uyarılar, büyük kart).</summary>
        void TelefonAc(Courier c)
        {
            popupCourier = c;
            CloseInfo();
            telKirli = true;
        }

        void TelefonSayfa(PhoneScreen s)
        {
            phoneScreen = s;
            supportOpen = -1;
            telKaydirSifirla = true;
            telKirli = true;
        }

        // ====================================================================== kurulum

        VisualElement telKok, telEkran, telAna, telMenu, telAlt, telSheet, telZilNokta, telPil, telPilDolu;
        TelHarita telHarita;
        TelHap telDurumHap;
        ScrollView telAnaKaydir, telMenuKaydir, telAltKaydir;
        Yazici telAnaY, telMenuY, telAltY;
        Label telSaat, telAltBaslik;
        bool telKirli = true, telKaydirSifirla;
        float telSonYukseklik = -1f;

        void UiTelefonKur()
        {
            telKok = U.El(katPencere, "tel");
            telKok.style.width = TelGenislik;
            telKok.style.top = TopBar + 14f;
            // Bir şeye tıklanınca içerik hemen yenilensin (8 Hz'i beklemeden)
            telKok.RegisterCallback<PointerUpEvent>(_ => telKirli = true, TrickleDown.TrickleDown);

            U.Gecirgen(telKok, "tel-golge");
            TelTus(true, 120f, 46f);
            TelTus(true, 176f, 46f);
            TelTus(false, 150f, 70f);
            var govde = U.El(telKok, "tel-govde");
            telEkran = U.El(govde, "tel-ekran");

            // Ana ekran: harita, üstünde menü / zil / kapat, altta kaydırılan panel ve sabit durum hapı
            telAna = U.Gecirgen(telEkran, "tel-sayfa");
            telHarita = new TelHarita();
            telAna.Add(telHarita);
            TelYuvarlak(telAna, TelSimgeTur.Menu, () => TelefonSayfa(PhoneScreen.Menu), "tel-harita-dg").style.left = 16f;
            TelYuvarlak(telAna, TelSimgeTur.Zil, () => TelefonSayfa(PhoneScreen.Inbox), "tel-harita-dg").style.right = 64f;
            telZilNokta = U.Gecirgen(telAna, "tel-zil-nokta");
            TelYuvarlak(telAna, TelSimgeTur.Kapat, TelefonKapat, "tel-harita-dg").style.right = 16f;
            telSheet = U.El(telAna, "tel-sheet");
            U.Gecirgen(telSheet, "tel-tutamak");
            telAnaKaydir = TelKaydirici(telSheet);
            telAnaY = new Yazici(this, telAnaKaydir.contentContainer);
            telDurumHap = new TelHap();
            telDurumHap.AddToClassList("tel-durum-hap");
            telSheet.Add(telDurumHap);

            // Menü
            telMenu = U.Gecirgen(telEkran, "tel-sayfa", "tel-liste-sayfa");
            TelUstSatir(telMenu, () => TelefonSayfa(PhoneScreen.Home));
            telMenuKaydir = TelKaydirici(telMenu);
            telMenuY = new Yazici(this, telMenuKaydir.contentContainer);

            // Alt sayfalar
            telAlt = U.Gecirgen(telEkran, "tel-sayfa", "tel-liste-sayfa", "tel-alt-sayfa");
            TelUstSatir(telAlt, () => TelefonSayfa(phoneScreen == PhoneScreen.PrevTask ? PhoneScreen.Home : PhoneScreen.Menu));
            telAltBaslik = U.Kalin(telAlt, "", "tel-sayfa-baslik");
            telAltKaydir = TelKaydirici(telAlt);
            telAltY = new Yazici(this, telAltKaydir.contentContainer);

            // Durum çubuğu (saat, ada, sinyal, pil) ve alttaki çizgi her ekranın üstünde
            var dc = U.Gecirgen(telEkran, "tel-durum-cubugu");
            telSaat = U.Kalin(dc, "", "tel-saat");
            U.Gecirgen(dc, "tel-ada");
            var sinyal = U.Gecirgen(dc, "tel-sinyal");
            for (int i = 0; i < 4; i++)
            {
                var b = U.Gecirgen(sinyal, "tel-sinyal-cubuk");
                b.style.height = 3f + i * 2.5f;
                if (i == 3) b.style.opacity = 0.35f;
            }
            telPil = U.Gecirgen(dc, "tel-pil");
            telPilDolu = U.Gecirgen(telPil, "tel-pil-dolu");
            U.Gecirgen(dc, "tel-pil-uc");
            U.Gecirgen(telEkran, "tel-ev-cizgi");
            U.Goster(telKok, false);
        }

        void TelTus(bool sol, float ust, float boy)
        {
            var t = U.Gecirgen(telKok, "tel-tus");
            if (sol) t.style.left = -3f;
            else t.style.right = -3f;
            t.style.top = ust;
            t.style.height = boy;
        }

        static VisualElement TelYuvarlak(VisualElement ust, TelSimgeTur tur, Action tik, string sinif)
        {
            var d = U.Tikla(U.El(ust, "tel-yuvarlak", sinif), tik);
            d.Add(new TelSimge(tur, 20f));
            return d;
        }

        void TelUstSatir(VisualElement ust, Action geri)
        {
            var s = U.Gecirgen(ust, "satir", "tel-ust-satir");
            TelYuvarlak(s, TelSimgeTur.Geri, geri, "kucuk");
            U.Gecirgen(s, "bosluk");
            TelYuvarlak(s, TelSimgeTur.Kapat, TelefonKapat, "kucuk");
        }

        void TelefonKapat() => popupCourier = null;

        static ScrollView TelKaydirici(VisualElement ust)
        {
            var k = new ScrollView(ScrollViewMode.Vertical);
            k.AddToClassList("kaydir");
            k.AddToClassList("tel-kaydir");
            k.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            k.verticalScrollerVisibility = ScrollerVisibility.Hidden;   // gerçek telefondaki gibi çubuk yok; tekerlekle kayar
            ust.Add(k);
            return k;
        }

        // ====================================================================== yenileme

        /// <summary>Her karede: kayma, harita ve durum çubuğu. İçerik 8 Hz'de ya da tıklanınca.</summary>
        void UiTelefonYenile(bool agir)
        {
            var c = popupCourier;
            if (c != null && (CourierManager.I == null || !CourierManager.I.Couriers.Contains(c))) { popupCourier = null; c = null; }
            if (c == null)
            {
                phoneAnim = 0f;
                phoneAnimFor = null;   // yeniden açınca ana ekrandan başlar
                U.Goster(telKok, false);
                return;
            }
            if (phoneAnimFor != c)
            {
                phoneAnimFor = c;
                phoneScreen = PhoneScreen.Home;
                phoneTaskTab = 0;
                phoneOfflineEdit = false;
                phoneConfirmCancel = -1;
                supportOpen = -1;
                telKaydirSifirla = true;
                telKirli = true;
                if (phoneAnim > 0.5f) phoneAnim = 0.85f;
            }
            U.Goster(telKok, true);

            // Sağdan kayarak gelir
            phoneAnim = Mathf.MoveTowards(phoneAnim, 1f, Time.unscaledDeltaTime * 4f);
            float kayma = (1f - Mathf.SmoothStep(0f, 1f, phoneAnim)) * (TelGenislik + 40f);
            telKok.style.right = SagKartKenar - kayma;
            float sh = UiEkranYuksekligi;
            float h = Mathf.Clamp(sh - TopBar - 70f, 460f, 720f);
            if (Mathf.Abs(h - telSonYukseklik) > 0.5f)
            {
                telSonYukseklik = h;
                telKok.style.height = h;
                // Harita ekranın ~%46'sı, panel altında (ekran = telefon − 20 px çerçeve)
                float mapH = (h - 20f) * 0.46f;
                telHarita.style.height = mapH + 30f;
                telSheet.style.top = mapH;
            }

            // Durum çubuğu
            var clock = GameClock.I;
            if (clock != null) U.Metin(telSaat, $"{clock.Hour:00}:{clock.Minute:00}");
            float pil = c.Has(EquipmentKind.Powerbank) ? 0.95f : Mathf.Clamp(1f - c.onlineMinutesToday / 720f, 0.08f, 1f);
            telPilDolu.style.width = Length.Percent(pil * 100f);
            U.Zemin(telPilDolu, pil < 0.2f ? PRed : Color.white);

            bool ana = phoneScreen == PhoneScreen.Home;
            bool menu = phoneScreen == PhoneScreen.Menu;
            U.Goster(telAna, ana);
            U.Goster(telMenu, menu);
            U.Goster(telAlt, !ana && !menu);
            if (ana)
            {
                telHarita.c = c;
                telHarita.Guncelle();
            }

            if (!agir && !telKirli) return;
            telKirli = false;
            if (telKaydirSifirla)
            {
                telKaydirSifirla = false;
                telAnaKaydir.scrollOffset = Vector2.zero;
                telMenuKaydir.scrollOffset = Vector2.zero;
                telAltKaydir.scrollOffset = Vector2.zero;
            }
            if (ana) TelAnaEkran(c);
            else if (menu) TelMenuEkran(c);
            else TelAltSayfa(c);
        }

        // ====================================================================== ana ekran

        void TelAnaEkran(Courier c)
        {
            U.Goster(telZilNokta, c.inboxText.Count > 0);
            bool teklif = c.lastOffer != null && Time.unscaledTime - c.lastOfferRealTime < 4f;
            bool gorev = !teklif && c.CarryingList.Count + c.ToPickList.Count > 0;
            var y = telAnaY;
            y.Basla();
            if (teklif) HomeOffer(y, c);
            else if (gorev) HomeTaskApp(y, c);
            else HomeIdle(y, c);
            y.Bosluk(8);
            y.Bitir();
            // Gerçek uygulamadaki gibi altta sabit büyük hap (kurye durumu)
            U.Goster(telDurumHap, gorev);
            if (gorev) telDurumHap.Ayarla(TaskStatusText(c), PAccent, true);
        }

        void HomeOffer(Yazici y, Courier c)
        {
            var o = c.lastOffer;
            y.Yazi(Ft(c.lastOfferPay), "tel-dev");
            y.Yazi("Teslimatın tamamı için tahmini kazanç", "tel-soluk");
            TelCizgi(y);
            y.Yazi("<b>Teslim alınacak yer</b>");
            y.Yazi($"{o.shop.Name} | {Addresses.StreetOf(o.shop.door)}", "tel-vurgu", PAccent);
            y.KutularBasla();
            y.Kutu("Rota mesafesi", GameData.KmText(o.distance));
            float est = c.EstimateMinutes(o);
            y.Kutu("Tahmini", $"{Mathf.Max(1f, est * 0.8f):0} – {est * 1.25f + 2f:0} dk");
            y.KutularBitir();
            if (o.cashPay)
            {
                TelCizgi(y);
                y.KutularBasla();
                y.Kutu("Nakit sipariş", o.CashChange > 0 ? $"{Ft(o.CashChange)} para üstü" : "Para üstü gerekmiyor");
                y.KutularBitir();
            }
            if (o.weightKg >= 8f)
            {
                TelCizgi(y);
                y.KutularBasla();
                y.Kutu("Ağır sipariş", GameData.KgText(o.weightKg));
                y.KutularBitir();
            }
            y.Bosluk(10);
            TelHapYaz(y, c.lastOfferAccepted ? "Kabul etti" : "Reddetti", c.lastOfferAccepted ? PAccent : PRed, false);
            if (!c.lastOfferAccepted && !string.IsNullOrEmpty(c.lastOfferReason))
                y.Yazi($"Neden: {c.lastOfferReason}", "tel-soluk");
        }

        static readonly string[] YogunlukBaslik = { "Normalden sakin", "Normal yoğunluk", "Çok yoğun" };
        static readonly string[] YogunlukAd = { "Düşük", "Normal", "Yüksek" };

        void HomeIdle(Yazici y, Courier c)
        {
            if (c.IsOnline)
            {
                int level = DemandLevel();
                y.Yazi(YogunlukBaslik[level], "tel-dev");
                y.Ozel("yogunluk", () => new TelYogunluk()).Ayarla(level, YogunlukAd[level]);
            }
            else
            {
                y.Yazi(CityMap.DistrictOf(Vector2Int.RoundToInt(c.Position)), "tel-dev");
                y.Yazi(c.StateName, "tel-soluk");
            }
            y.Bosluk(8);
            TelBoostYaz(y);
            y.Bosluk(10);
            TelHapYaz(y, c.IsOnline ? "Çevrimiçi · sipariş bekliyor" : "Çevrimdışı", c.IsOnline ? PAccent : new Color(0.30f, 0.32f, 0.40f), true);
            y.Yazi(c.OccupationStatus, "tel-soluk");
        }

        static int DemandLevel()
        {
            var om = OrderManager.I;
            if (om != null && om.IsBusy) return 2;
            float sum = 0f; int n = 0;
            var clock = GameClock.I;
            foreach (var s in CityMap.I.Shops)
                if (s.contracted) { sum += OrderManager.DemandCurve(s.def, clock.Hour, clock.IsWeekend); n++; }
            float avg = n > 0 ? sum / n : 0f;
            return avg < 0.7f ? 0 : avg < 1.3f ? 1 : 2;
        }

        static float EtaMinutes(Courier c)
        {
            var path = c.PathList;
            if (path == null || path.Count == 0) return 0f;
            float tiles = 0f;
            Vector2 prev = c.Position;
            for (int i = c.PathIndex; i < path.Count; i++) { tiles += Vector2.Distance(prev, path[i]); prev = path[i]; }
            return tiles / Mathf.Max(0.05f, c.Speed);
        }

        /// <summary>Aktif ya da sıradaki boost kartı; tıklayınca Ekstra kazan.</summary>
        void TelBoostYaz(Yazici y)
        {
            var camp = CampaignManager.I;
            var active = camp.ActiveWindow;
            float wb = camp.WeatherBoostPercent;
            string text;
            if (active != null && active.percent >= wb) text = $"+%{active.percent * 100f:0} · {active.TimeText}";
            else if (wb > 0f) text = $"+%{wb * 100f:0} kötü hava boostu · şimdi";
            else
            {
                BoostWindow next = null;
                foreach (var w in camp.TodayWindows())
                    if (w.startHour > GameClock.I.Hour && (next == null || w.startHour < next.startHour)) next = w;
                text = next != null ? $"+%{next.percent * 100f:0} · {next.startHour:00}:00'dan itibaren" : "Bugün başka boost yok";
            }
            var b = y.Ozel("boost", () => new TelBoost());
            b.tik = () => TelefonSayfa(PhoneScreen.Earn);
            b.Ayarla(text);
        }

        // ---------- ortak küçük parçalar ----------

        static void TelCizgi(Yazici y) => y.Ozel("tel-cizgi", () => U.Gecirgen(null, "tel-cizgi"));

        static void TelBaslik(Yazici y, string t)
        {
            y.Bosluk(8);
            y.Yazi(t, "tel-baslik");
        }

        static void TelHapYaz(Yazici y, string metin, Color zemin, bool ok) => y.Ozel("hap", () => new TelHap()).Ayarla(metin, zemin, ok);

        /// <summary>Liste satırı: solda ad, sağda değer, altında ince çizgi.</summary>
        static void PRow(Yazici y, string k, string v, Color? vc = null) => y.KV(k, v, vc);

        /// <summary>Uygulama düğmesi (yuvarlak köşeli, renkli).</summary>
        static void TelDugme(Yazici y, string metin, Action tik, Color zemin, Color yazi, bool etkin = true, string ek = null)
            => y.Dugme(metin, tik, etkin, "tel-dg" + (ek != null ? " " + ek : ""), zemin, yazi);

        /// <summary>Şirket renginde yazı bağlantısı ("Destekle iletişime geç", "Çevir").</summary>
        static void TelBaglanti(Yazici y, string metin, Action tik, string ek = null)
            => y.Dugme(metin, tik, true, "tel-baglanti" + (ek != null ? " " + ek : ""), new Color(0, 0, 0, 0), PAccent);

        /// <summary>Sipariş kartı: durum etiketi, ürünler, adres, kat, nakit, kalan süre.</summary>
        void OrderCard(Yazici y, Order o, Courier c, bool inBag, float now)
        {
            bool currentOne = c.CurrentOrder == o;
            string where = inBag ? (currentOne ? "TESLİM EDİLİYOR" : "ÇANTADA") : (currentOne ? "DÜKKANA GİDİYOR" : "ALINACAK");
            Color tagC = inBag ? (currentOne ? PAccent : PGreen) : (currentOne ? new Color(1f, 0.75f, 0.3f) : PDim);
            y.Ozel("siparis-bas", () => new TelBaslikEtiket()).Ayarla($"#{o.id} {o.shop.Name}", where, tagC);
            y.Yazi($"{o.ItemCount} ürün · {GameData.KgText(o.weightKg)} — {o.ItemsText}", "tel-soluk");
            y.Yazi($"{o.customerName} · {o.AddressText}", "tel-soluk");
            y.Yazi(o.BuildingText, "tel-soluk", o.UsesStairs && o.floor >= 4 ? new Color(1f, 0.7f, 0.35f) : (Color?)null);
            if (o.cashPay) y.Yazi($"Nakit: {Ft(o.CashAmount)} · müşteri {Ft(o.cashGiven)} verecek · para üstü {Ft(o.CashChange)}", "tel-soluk", new Color(1f, 0.7f, 0.35f));
            if (!string.IsNullOrEmpty(o.customerNote)) y.Yazi($"\"{o.customerNote}\"", "tel-soluk tel-italik");
            float left = o.deadlineAt - now;
            y.Ozel("siparis-alt", () => new TelIkili()).Ayarla(left >= 0 ? $"{left:0} dk kaldı" : $"{-left:0} dk gecikmede",
                                                               left >= 0 ? (Color?)null : PRed, Ft(o.courierPay + o.boostPay));
            TelCizgi(y);
        }

        // ====================================================================== menü

        void TelMenuEkran(Courier c)
        {
            var y = telMenuY;
            y.Basla();
            var p = y.Ozel("profil", () => new TelProfil());
            p.tik = () => TelefonSayfa(PhoneScreen.Profile);
            p.Ayarla(c.courierName, c.IsOnline);
            MenuRow(y, "T", "Teslimat ayarları", c.V.name + " · " + c.VehiclesText, PhoneScreen.Profile);
            MenuRow(y, "A", "Analizler", $"Bu hafta {c.weekDeliveries} teslimat", PhoneScreen.Insights);
            MenuRow(y, "F", "Finans", $"Hesabında {Ft(c.balance)}", PhoneScreen.Finance);
            MenuRow(y, "İ", "İstatistikler", $"Bugün {c.deliveriesToday} teslimat", PhoneScreen.Stats);
            MenuRow(y, "E", "Ekstra kazan", "Boostlar ve hedefler", PhoneScreen.Earn);
            MenuRow(y, "S", "Sigorta ve haklar", c.insured ? "Sigortalı" : "Sigortası yok", PhoneScreen.Insurance);
            MenuRow(y, "G", "Gelen kutusu", $"{c.inboxText.Count} mesaj", PhoneScreen.Inbox);
            MenuRow(y, "D", "Destek", "Sık sorulanlar", PhoneScreen.Support);
            y.Bosluk(6);
            y.Yazi($"Kurye uygulaması · {CompanyProfile.Name}", "tel-soluk");
            y.Bitir();
        }

        void MenuRow(Yazici y, string icon, string title, string sub, PhoneScreen target)
        {
            var r = y.Ozel("menu-satir", () => new TelMenuSatir());
            r.tik = () => TelefonSayfa(target);
            r.Ayarla(icon, title, sub);
        }

        // ====================================================================== alt sayfalar

        void TelAltSayfa(Courier c)
        {
            string title;
            switch (phoneScreen)
            {
                case PhoneScreen.Stats: title = "İstatistiklerin"; break;
                case PhoneScreen.Earn: title = "Ekstra kazan"; break;
                case PhoneScreen.Insights: title = "Analizler"; break;
                case PhoneScreen.Finance: title = "Finans"; break;
                case PhoneScreen.Insurance: title = "Sigorta ve haklar"; break;
                case PhoneScreen.Profile: title = "Profil"; break;
                case PhoneScreen.Inbox: title = "Gelen kutusu"; break;
                case PhoneScreen.PrevTask: title = "Önceki görev"; break;
                default: title = "Nasıl yardımcı olabiliriz?"; break;
            }
            U.Metin(telAltBaslik, title);
            var y = telAltY;
            y.Basla();
            switch (phoneScreen)
            {
                case PhoneScreen.Stats: PageStats(y, c); break;
                case PhoneScreen.Earn: PageEarn(y, c); break;
                case PhoneScreen.Insights: PageInsights(y, c); break;
                case PhoneScreen.Finance: PageFinance(y, c); break;
                case PhoneScreen.Insurance: PageInsurance(y, c); break;
                case PhoneScreen.Profile: PageProfile(y, c); break;
                case PhoneScreen.Inbox: PageInbox(y, c); break;
                case PhoneScreen.PrevTask: PagePrevTask(y, c); break;
                default: PageSupport(y, c); break;
            }
            y.Bosluk(10);
            y.Bitir();
        }

        // ---------- İstatistikler ----------
        static readonly string[] IstatistikSekmeleri = { "Bugün", "Hafta", "Ay" };

        void PageStats(Yazici y, Courier c)
        {
            var seg = y.Ozel("segment", () => new TelSegment());
            seg.sec = i => { statsTab = i; telKirli = true; };
            seg.Ayarla(IstatistikSekmeleri, statsTab);
            y.Bosluk(10);
            var clock = GameClock.I;
            string when = statsTab == 0 ? clock.DateOf(clock.Day) : statsTab == 1 ? "Bu hafta (Pazartesi'den beri)" : clock.MonthName;
            y.Yazi($"<b>{when}</b>");
            y.Bosluk(4);
            int del = statsTab == 0 ? c.deliveriesToday : statsTab == 1 ? c.weekDeliveries : c.monthDeliveries;
            float km = statsTab == 0 ? c.kmToday : statsTab == 1 ? c.kmWeek : c.kmMonth;
            long earn = statsTab == 0 ? c.earnedToday : statsTab == 1 ? c.earnedWeek : c.earnedMonth;
            PRow(y, "Tamamlanan teslimat", del.ToString());
            PRow(y, "Görevde ve dışında mesafe", $"{km:0.#} km");
            PRow(y, "Kazanç (tahmini)", earn > 0 ? Ft(earn) : "—");
            if (statsTab == 0) PRow(y, "Çevrimiçi süre", $"{c.onlineMinutesToday / 60f:0.0} saat");
            PRow(y, "Toplam teslimat", c.deliveries.ToString());
        }

        // ---------- Ekstra kazan ----------
        void PageEarn(Yazici y, Courier c)
        {
            var camp = CampaignManager.I;
            var clock = GameClock.I;
            y.Yazi("<b>Aktif</b>");
            var active = camp.ActiveWindow;
            float wb = camp.WeatherBoostPercent;
            var kart = y.Ozel("boost-buyuk", () => new TelBoostBuyuk());
            if (active != null && active.percent >= wb) kart.Ayarla(true, "Şimdi · Boost", $"+%{active.percent * 100f:0}", active.TimeText);
            else if (wb > 0f) kart.Ayarla(true, $"Şimdi · {WeatherSystem.I.Name}", $"+%{wb * 100f:0}", "Kötü hava boostu");
            else kart.Ayarla(false, null, null, null);

            TelBaslik(y, "Daha sonra");
            int shown = 0;
            foreach (var w in camp.TodayWindows())
            {
                if (w.startHour <= clock.Hour) continue;
                y.Yazi($"<b>Bugün · +%{w.percent * 100f:0}</b>", "tel-deger");
                y.Yazi(w.TimeText, "tel-soluk");
                TelCizgi(y);
                shown++;
            }
            if (shown == 0) y.Yazi("Bugün başka boost yok.", "tel-soluk");
            y.Yazi("Yarının boostları gece yarısı açıklanır.", "tel-soluk");

            if (camp.weeklyChallenge || camp.monthlyChallenge) TelBaslik(y, "Hedefler");
            if (camp.weeklyChallenge) y.Ozel("hedef", () => new TelHedef()).Ayarla($"Haftalık · {Ft(camp.weeklyReward)}", c.weekDeliveries, camp.weeklyTarget, c.weeklyRewardClaimed);
            if (camp.monthlyChallenge) y.Ozel("hedef", () => new TelHedef()).Ayarla($"Aylık · {Ft(camp.monthlyReward)}", c.monthDeliveries, camp.monthlyTarget, c.monthlyRewardClaimed);
        }

        // ---------- Analizler ----------
        void PageInsights(Yazici y, Courier c)
        {
            y.Yazi($"Merhaba {c.courierName.Split(' ')[0]}! Bu haftan böyle gidiyor. Müşterileri mutlu ettiğin için teşekkürler!");
            y.Yazi("Veriler anlık güncellenir", "tel-soluk");
            TelBaslik(y, "Tamamlanan teslimat");
            y.Ozel("buyuk-sayi", () => new TelBuyukSayi()).Ayarla(c.weekDeliveries);
            int need = c.lastWeekDeliveries - c.weekDeliveries + 1;
            y.Bosluk(4);
            y.Yazi(need > 0 ? $"Geçen haftayı geçmek için {need} teslimat daha!" : "Geçen haftanın teslimat sayısını geçtin!");

            TelBaslik(y, "Kabul edilen görevler");
            float acc = c.offersSeenWeek > 0 ? c.offersAcceptedWeek / (float)c.offersSeenWeek : 0f;
            y.Ozel("gosterge", () => new TelGosterge()).Ayarla(acc, $"{c.offersAcceptedWeek}/{c.offersSeenWeek} teklif", PAccent);
            if (c.offersSeenWeek > 0 && acc < 0.5f) y.Yazi("Dikkat: birçok kazanç fırsatını kaçırıyor gibi görünüyorsun!", null, new Color(1f, 0.7f, 0.4f));

            TelBaslik(y, "Görev tamamlama oranı");
            int started = c.monthDeliveries + c.pickupCancelsThisMonth;
            float comp = started > 0 ? c.monthDeliveries / (float)started : 1f;
            y.Ozel("gosterge", () => new TelGosterge()).Ayarla(comp, $"bu ay {c.pickupCancelsThisMonth}/{Courier.MaxPickupCancels} iptal", PAccent);
        }

        // ---------- Finans ----------
        void PageFinance(Yazici y, Courier c)
        {
            y.Yazi(Ft(c.balance), "tel-dev");
            y.Yazi($"Hesabında · sıradaki kesim {EconomyManager.I.NextCutText}", "tel-soluk");
            TelBaslik(y, "Ödemeler");
            bool anyP = false;
            foreach (var p in EconomyManager.I.Payouts)
                if (p.courier == c) { PRow(y, p.early ? "Yatacak (erken)" : "Yatacak", $"{Ft(p.amount)} · {GameClock.I.DateOf(p.payDay)}"); anyP = true; }
            if (!anyP) PRow(y, "Yatacak", "—");
            PRow(y, "Bugüne kadar yatan", Ft(c.receivedTotal));
            PRow(y, "Erken ödeme", c.earlyPayouts > 0 ? $"{c.earlyPayouts} kez · {Ft(c.earlyPayoutTotal)}" : "—");
            TelBaslik(y, "Nakit");
            if (Sade.NakitBorcu)
            {
                PRow(y, "Üzerindeki nakit", $"{Ft(c.cashHeld)} / {Ft(Courier.CashLimit)}", c.cashHeld >= Courier.CashLimit * 0.7f ? PRed : (Color?)null);
                if (c.cashBlocked) y.Yazi("Hesabın kilitli: nakit borcunu kapatana kadar sipariş alamazsın.", null, PRed);
                PRow(y, "Keep cash ile kapatılabilir", Ft(Math.Min(c.cashHeld, c.KeepCashCapacity)));
            }
            PRow(y, "Nakit teslimat", $"{c.cashOrders} paket");
            PRow(y, "Müşterilerden alınan", Ft(c.cashTakenTotal));
            PRow(y, "Keep cash ile mahsup", Ft(c.keepCashTotal));
            PRow(y, "Offload ile ödenen", Ft(c.offloadTotal));
            TelBaslik(y, "Toplam");
            PRow(y, "Toplam kazanç", Ft(c.totalEarned));
            PRow(y, "Bahşişler", Ft(c.tipsTotal));
            PRow(y, "Araç birikimi", Ft(c.savings));
        }

        // ---------- Sigorta ----------
        void PageInsurance(Yazici y, Courier c)
        {
            y.Ozel("sigorta", () => new TelSigortaResim()).Ayarla(c.insured);
            y.Bosluk(6);
            y.Yazi(c.insured ? "Kaza sigortan aktif" : "Kaza sigortan yok", "tel-baslik tel-orta");
            var eco = EconomyManager.I;
            y.Yazi(c.insured
                ? (c.insuredViaCompany
                    ? $"Sigortan {CompanyProfile.Name}'ın anlaşmalı sigortacısı {eco.Plan.name} üzerinden. Primini sen ödüyorsun (haftalık {Ft(c.CurrentPremium)}). Kazada masrafları sigorta karşılar ve daha kısa sürede işe dönersin."
                    : $"Sigortanı dışarıdan kendin yaptırdın (haftalık {Ft(c.CurrentPremium)}). Kazada masrafları sigorta karşılar ve daha kısa sürede işe dönersin.")
                : (eco.HasInsurancePartner
                    ? $"Serbest kuryesin, sigorta zorunlu değil. İstersen {eco.Plan.name} ile haftalık {Ft(eco.Plan.premium)} karşılığında sigortalanabilirsin. Sigortasız kazada masrafları kendin ödersin."
                    : "Serbest kurye olarak sigortanı kendin yaptırırsın. Sigortasız kazada masrafları kendin ödersin ve daha uzun süre çalışamazsın."),
                "tel-orta");
            y.Bosluk(8);
            PRow(y, "Sigortacı", c.insured ? (c.insuredViaCompany ? eco.Plan.name : "Dışarıdan") : "—");
            PRow(y, "Haftalık prim", Ft(c.insured ? c.CurrentPremium : (eco.HasInsurancePartner ? eco.Plan.premium : Courier.InsurancePremium)));
            PRow(y, "Bugüne kadar ödenen", Ft(c.insurancePaid));
            PRow(y, "Kask", c.Has(EquipmentKind.Kask) ? "Var" : "Yok", c.Has(EquipmentKind.Kask) ? (Color?)null : PRed);
        }

        // ---------- Profil ----------
        void PageProfile(Yazici y, Courier c)
        {
            TelBaslik(y, "Kişisel bilgiler");
            ProfField(y, "Ad soyad", c.courierName);
            ProfField(y, "Kurye ID", c.courierId > 0 ? c.courierId.ToString() : "—");
            ProfField(y, "Meslek", c.OccupationName);
            ProfField(y, "Tecrübe", $"Seviye {c.level} · {c.deliveries} teslimat");

            TelBaslik(y, "Araçlar");
            ProfField(y, "Kullandığı", c.V.name);
            ProfField(y, "Sahip olduğu", c.VehiclesText);
            if (c.dreamVehicle != c.vehicle) ProfField(y, "Hayalindeki araç", $"{GameData.Vehicle(c.dreamVehicle).name} · birikim {Ft(c.savings)}");

            TelBaslik(y, "Ekipman");
            var clock = GameClock.I;
            if (c.depositPaidTotal > 0 || c.depositOwed > 0)
                ProfField(y, "Depozito", $"{Ft(c.depositPaidTotal)} kesildi" + (c.depositOwed > 0 ? $", {Ft(c.depositOwed)} kesilecek" : "") +
                                         (c.depositDay > 0 ? $" (ilk kesinti {clock.DateOf(c.depositDay)})" : ""));
            foreach (var k in c.equipment)
            {
                string n = GameData.Equip(k).name;
                int i = c.gearNames.IndexOf(n);
                ProfField(y, n, i >= 0 ? $"{clock.DateOf(c.gearDays[i])} tarihinde teslim alındı" : "Teslim alındı");
            }
            if (c.equipment.Count == 0) y.Yazi("Ekipmanı yok.", "tel-soluk");
        }

        static void ProfField(Yazici y, string k, string v)
        {
            y.Yazi(v, "tel-satir");
            y.Yazi(k, "tel-soluk");
            y.Bosluk(6);
        }

        // ---------- Gelen kutusu ----------
        void PageInbox(Yazici y, Courier c)
        {
            if (c.inboxText.Count == 0) { y.Yazi("Henüz mesaj yok.", "tel-soluk"); return; }
            for (int i = 0; i < c.inboxText.Count; i++)
            {
                y.Yazi($"<b>{c.inboxText[i]}</b>");
                y.Yazi(i < c.inboxWhen.Count ? c.inboxWhen[i] : "", "tel-soluk");
                TelCizgi(y);
            }
        }

        // ---------- Destek ----------
        void PageSupport(Yazici y, Courier c)
        {
            y.Yazi("Merhaba! Konunu seç.", "tel-soluk");
            TelBaslik(y, "Konular");
            string why;
            if (c.cashBlocked) why = "Hesabın nakit borcu yüzünden kilitli. Keep cash ya da offload ile kapatınca tekrar sipariş alırsın.";
            else if (!c.IsOnline) why = $"Şu an çevrimdışısın ({c.StateName}).";
            else if (c.RejectChance > 0.2f) why = $"Moralin düşük ya da yorgunsun; tekliflerin yaklaşık %{c.RejectChance * 100f:0}'ini reddediyorsun.";
            else if (DemandLevel() == 0) why = "Şu an sipariş yoğunluğu düşük. Boost saatlerinde ve öğle/akşam yoğunluğunda daha çok teklif gelir.";
            else why = "Yakındaki kuryeler teklifleri senden önce alıyor olabilir. Dükkanlara yakın beklemek işe yarar.";
            SupportTopic(y, 0, "Bir süredir sipariş gelmiyor, neden?", why);
            SupportTopic(y, 1, "Nakit siparişler", Sade.NakitBorcu
                ? $"Müşteriden aldığın nakit şirkete borcundur. Üzerinde en fazla {Ft(Courier.CashLimit)} olabilir. Ay sonuna kadar keep cash (hesabının en fazla %80'iyle mahsup) ya da offload (kartla ödeme) ile kapatmalısın, yoksa hesabın kilitlenir."
                : "Müşteriden aldığın nakit şirketindir; gün sonunda uygulama üzerinden şirkete aktarılır. Para üstünü doğru vermeye dikkat et.");
            SupportTopic(y, 2, "Ödemeler", "Kazancın hesabında birikir. Ayın 15'i ve son günü kesilir, 1–2 iş günü içinde yatar. İstersen hesabının %80'ini erken çekebilirsin (2–3 iş günü).");
            SupportTopic(y, 3, "Ekipman", Sade.Depozito
                ? "İhtiyacın olan ekipmanı (termal çanta, mont, yağmurluk, kask…) ofisten depozitoyla alırsın; bedeli kazancından kesilir. Ayrılırken ekipmanı iade edersen depozitonun tamamı geri ödenir, etmezsen şirkette kalır."
                : "İhtiyacın olan ekipmanı (termal çanta, mont, yağmurluk, kask…) ofisten ücretsiz alırsın. Ekipman şirketindir: ayrılırken iade etmelisin.");
            SupportTopic(y, 4, "Sözleşme ve çalışma", "Serbest kuryesin: ne zaman çevrimiçi olacağına sen karar verirsin. Aracını ve sigortanı kendin karşılarsın. Teklifleri reddedebilirsin; dükkanda sıra varsa ayda en fazla 10 kez teslim almayı iptal edebilirsin.");
            SupportTopic(y, 5, "Kat ve teslimat", "Adrese varınca binaya girip kata çıkarsın. Asansörlü binada bekleme + kat başı kısa süre, asansörsüz binada her kat merdivenle; ağır paketlerde daha uzun sürer ve yorar.");
        }

        void SupportTopic(Yazici y, int id, string title, string body)
        {
            var k = y.Ozel("konu", () => new TelKonu());
            k.tik = () => { supportOpen = supportOpen == id ? -1 : id; telKirli = true; };
            k.Ayarla(title, supportOpen == id);
            if (supportOpen != id) return;
            y.Bosluk(4);
            y.Yazi(body);
            y.Bosluk(8);
        }
    }
}
