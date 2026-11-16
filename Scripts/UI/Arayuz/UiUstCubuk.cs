using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Üst çubuk ve sağ üstteki durum sütunu (UI Toolkit).
    ///
    /// Üst çubuk (Anno / Two Point Hospital gibi): logo ve şirket adı · kısa göstergeler (simge, büyük değer, altında küçük açıklama):
    /// kasa (altında harcanabilir), itibar, çevrimiçi kurye, pazar payı · sağda tarih/saat/hava ve hız düğmeleri bir arada.
    /// Hiçbir yazı kesilmez: yer yetmezse önce şirket adı, sonra pazar payı gizlenir.
    ///
    /// Durum sütunu (RimWorld'ün sağ üstteki uyarıları gibi): süren durumlar tek tek kart olarak, tam metinle: uygulama çöktü,
    /// vergi borcu, krizler, fiyat savaşı, boost, kredi taksiti, şehir etkinliği, gece hızı. Tıklayınca ilgili pencere açılır.
    /// Eskiden bunlar üst çubukta hap olarak duruyordu ve sığmayınca yarım görünüyordu.
    /// </summary>
    public partial class GameUI
    {
        VisualElement ustKok, ustLogo, ustOynat, ustOynatSimge;
        Label ustLogoHarf, ustAd, hizEk;
        readonly List<(Label l, float hiz)> hizDilimleri = new List<(Label, float)>();

        class Gosterge
        {
            public VisualElement kok, simgeKutu, simge;
            public Label deger, alt;
        }

        Gosterge gKasa, gItibar, gKurye, gPazar, gZaman;

        static readonly Color KasaRengi = new Color(0.40f, 0.85f, 0.55f);
        static readonly Color HarcaRengi = new Color(0.45f, 0.75f, 1f);
        static readonly Color ItibarRengi = new Color(1f, 0.80f, 0.25f);
        static readonly Color Turuncu = new Color(1f, 0.62f, 0.20f);
        static readonly Color BilgiRengi = new Color(0.40f, 0.70f, 1f);

        void UiUstKur()
        {
            ustKok = U.El(katHud, "ust");
            var marka = U.Gecirgen(ustKok, "marka");   // logo + şirket adı tek kapsülde (kâğıt tema)
            ustLogo = U.El(marka, "logo");
            ustLogoHarf = U.Kalin(ustLogo, "", "logo-harf");
            ustAd = U.Kalin(marka, "", "sirket-adi");

            var sol = U.Gecirgen(ustKok, "ust-gostergeler");
            gKasa = GostergeEkle(sol, Icons.Kind.Wallet, KasaRengi, () =>
            {
                var e = EconomyManager.I;
                if (e == null) return "";
                return $"Kasa: {Ft(e.money)}. Şirketin bütün parası.\n" +
                       $"Harcanabilir: {Ft(e.FreeCash)}. Kuryelere ödenecek hakedişler düşülünce kalan; harcamalarını buna göre yap.\n" +
                       "Kurye hakedişleri ayın 15'inde ve son günü kasadan ödenir.";
            });
            gItibar = GostergeEkle(sol, Icons.Kind.Star, ItibarRengi, () =>
            {
                float f = ItibarFarki();
                return $"İtibar {CompanyProfile.Reputation:0.00} / 5. Son 14 günün müşteri puanı ortalaması (iptal 1 yıldız sayılır; geç teslim " +
                       "en az yarım yıldız götürür; yoğunluk yüzünden vazgeçen müşterilerin dörtte biri 2 yıldız bırakır). " +
                       $"Bugün {(f >= 0 ? "+" : "−")}{Mathf.Abs(f):0.00}. İtibar yükseldikçe sipariş artar.";
            });
            gKurye = GostergeEkle(sol, Icons.Kind.Couriers, UITheme.Accent, () =>
            {
                var cm = CourierManager.I;
                var up = UpgradeManager.I;
                if (cm == null) return "";
                return $"Şu an çevrimiçi {cm.OnlineCount} kurye, toplam {cm.Couriers.Count}." +
                       (up != null ? $" Kurye hesabı sınırı {up.CourierCap} (Gelişim > platform paketi)." : "");
            });
            gPazar = GostergeEkle(sol, Icons.Kind.Chart, KasaRengi, PazarIpucu);
            U.Tikla(gPazar.kok, () => SekmeyeGit(Tab.Dukkanlar));

            U.Gecirgen(ustKok, "bosluk");

            gZaman = GostergeEkle(ustKok, Icons.Kind.Sun, Color.white, () =>
            {
                var c = GameClock.I;
                return c == null ? "" : $"{c.DayName}, {c.DayOfMonth} {c.MonthName} · {DayNight.PhaseName} · {c.SeasonName} · Yıl {c.Year} · şirketin {c.CompanyDay}. günü";
            });
            gZaman.kok.AddToClassList("zaman");

            var hiz = U.El(ustKok, "hiz");
            ustOynat = U.Tikla(U.El(hiz, "hiz-oynat"), () => { if (GameClock.I != null) GameClock.I.Paused = !GameClock.I.Paused; });
            ustOynatSimge = U.Simge(ustOynat, Icons.Kind.Pause, Color.white, 16f);
            Ipucu(ustOynat, "Duraklat / devam (Boşluk)");
            foreach (float sp in new[] { 1f, 2f, 4f, 8f, 16f, 32f })
            {
                float s = sp;
                var l = U.Kalin(hiz, sp + "×", "hiz-dilim");
                U.Tikla(l, () => SetSpeed(s));
                hizDilimleri.Add((l, sp));
            }
            hizEk = U.Kalin(hiz, "", "hiz-dilim");
            Ipucu(hizEk, "Klavyeden seçilen hız (6–8). Normal hızlar için soldakilerden birine bas.");

            DurumKur();
        }

        Gosterge GostergeEkle(VisualElement ust, Icons.Kind simge, Color renk, Func<string> ipucu)
        {
            var g = new Gosterge();
            g.kok = U.El(ust, "gosterge");
            g.simgeKutu = U.Gecirgen(g.kok, "gosterge-simge-kutu");
            g.simge = U.Simge(g.simgeKutu, simge, renk, 0f, "gosterge-simge");
            U.Zemin(g.simgeKutu, U.A(renk, 0.18f));
            var yazi = U.Gecirgen(g.kok, "gosterge-yazi");
            g.deger = U.Kalin(yazi, "", "gosterge-deger");
            g.alt = U.Yazi(yazi, "", "gosterge-alt");
            Ipucu(g.kok, ipucu);
            return g;
        }

        static void GostergeAyarla(Gosterge g, string deger, string alt, Color simgeRengi, Color? degerRengi = null, Color? altRengi = null, Icons.Kind? simge = null)
        {
            U.Metin(g.deger, deger);
            U.Metin(g.alt, alt);
            U.Renk(g.deger, U.Okunur(degerRengi ?? U.Murekkep));   // kâğıt üstünde okunur
            U.Renk(g.alt, U.Okunur(altRengi ?? U.Soluk));
            U.SimgeAyarla(g.simge, simge ?? (Icons.Kind)g.simge.userData, simgeRengi);
            U.Zemin(g.simgeKutu, U.A(simgeRengi, 0.18f));
        }

        float ItibarFarki()
        {
            var omr = OrderManager.I;
            return omr != null && omr.reputationAtDayStart >= 0f ? CompanyProfile.Reputation - omr.reputationAtDayStart : 0f;
        }

        /// <summary>Pencereyi açar (açıksa kapatmaz).</summary>
        void SekmeyeGit(Tab t)
        {
            if (panelOpen && tab == t) return;
            OpenTab(t);
        }

        string PazarIpucu()
        {
            var rv = RivalCompany.I;
            if (rv == null) return "";
            var sb = new System.Text.StringBuilder();
            sb.Append($"Pazar payın: geçen hafta şehirdeki teslimatların %{rv.MarketShare * 100f:0}'i sende");
            sb.Append(rv.ShareTrend >= 0f ? $" (+{rv.ShareTrend * 100f:0.0} puan)." : $" (−{-rv.ShareTrend * 100f:0.0} puan).");
            foreach (var r in rv.Rakipler)
                if (!r.satinAlindi) sb.Append($"\n{r.ad}: %{rv.PayiOf(r) * 100f:0} · {r.couriers} kurye · itibar {r.reputation:0.0}");
            sb.Append("\nTıkla: Dükkanlar > Rakipler.");
            return sb.ToString();
        }

        void UiUstYenile(bool agir)
        {
            var clock = GameClock.I;
            if (clock == null) return;
            var prim = CompanyProfile.Primary;
            var sec = CompanyProfile.Secondary;

            // Hız düğmeleri her karede (tıklama hemen görünsün)
            bool paused = clock.Paused;
            if (paused) U.Zemin(ustOynat, U.Uyari); else ZeminSil(ustOynat);
            U.SimgeAyarla(ustOynatSimge, paused ? Icons.Kind.Play : Icons.Kind.Pause, paused ? new Color(0.12f, 0.08f, 0.02f) : U.Murekkep);
            foreach (var (l, sp) in hizDilimleri)
            {
                bool on = IsSpeed(clock, sp);
                if (on) U.Zemin(l, prim); else ZeminSil(l);
                U.Renk(l, on ? sec : (paused ? U.A(U.Soluk, 0.6f) : U.Soluk));
            }
            bool ek = !paused && clock.SpeedMultiplier > 32.5f;
            U.Goster(hizEk, ek);
            if (ek)
            {
                U.Metin(hizEk, clock.SpeedMultiplier.ToString("0") + "×");
                U.Zemin(hizEk, prim);
                U.Renk(hizEk, sec);
            }

            if (!agir) return;
            var eco = EconomyManager.I;
            var weather = WeatherSystem.I;
            var cm = CourierManager.I;
            if (eco == null || weather == null || cm == null) return;

            if (ustKok.style.borderBottomColor != prim) ustKok.style.borderBottomColor = prim;
            U.Zemin(ustLogo, prim);
            U.Renk(ustLogoHarf, sec);
            U.Metin(ustLogoHarf, CompanyProfile.Initial);
            U.Metin(ustAd, CompanyProfile.Name);

            bool eksi = eco.FreeCash < 0;
            GostergeAyarla(gKasa, Ft(eco.money), "harcanabilir " + Ft(eco.FreeCash), KasaRengi, null, eksi ? U.Kotu : (Color?)null);
            float fark = ItibarFarki();
            GostergeAyarla(gItibar, $"{CompanyProfile.Reputation:0.0}" + (fark > 0.01f ? " ▲" : fark < -0.01f ? " ▼" : ""), "itibar", ItibarRengi,
                           fark < -0.01f ? new Color(1f, 0.6f, 0.5f) : (Color?)null);
            var up = UpgradeManager.I;
            bool dolu = up != null && up.CourierCapFull;
            GostergeAyarla(gKurye, $"{cm.OnlineCount} / {cm.Couriers.Count}", dolu ? "çevrimiçi · hesaplar dolu" : "çevrimiçi", prim, null,
                           dolu ? Turuncu : (Color?)null);
            PazarYenile();
            GostergeAyarla(gZaman, $"{clock.Hour:00}:{clock.Minute:00}", $"{KisaGun(clock.DayName)} {clock.DayOfMonth} {KisaAy(clock.MonthName)} · {weather.Name}",
                           WeatherTint(weather.Current), null, null, WeatherIcon(weather.Current));

            // Yer yetmezse yazı kesilmez, gösterge gizlenir: önce şirket adı, sonra pazar payı
            float sw = UiEkranGenisligi;
            U.Goster(ustAd, sw >= 1320f);
            U.Goster(gPazar.kok, sw >= 1160f && RivalCompany.I != null);

            DurumYenile(eco);
        }

        void PazarYenile()
        {
            var rv = RivalCompany.I;
            if (rv == null) return;
            bool veriYok = rv.myDeliveriesLastWeek + rv.deliveriesLastWeek == 0;
            if (veriYok)
            {
                GostergeAyarla(gPazar, "—", $"pazar payı · {rv.AktifRakip} rakip", U.Soluk);
                return;
            }
            float pay = rv.MarketShare, tr = rv.ShareTrend;
            Rakip enb = null;
            foreach (var r in rv.Rakipler) if (!r.satinAlindi && (enb == null || rv.PayiOf(r) > rv.PayiOf(enb))) enb = r;
            float enbPay = enb != null ? rv.PayiOf(enb) : 0f;
            // En büyük rakibi açık ara geçtiysen yeşil, başa başsa turuncu, gerideysen kırmızı
            Color renk = pay >= enbPay * 1.15f ? KasaRengi : pay >= enbPay * 0.8f ? Turuncu : U.Kotu;
            GostergeAyarla(gPazar, $"%{pay * 100f:0}" + (tr > 0.005f ? " ▲" : tr < -0.005f ? " ▼" : ""),
                           enb != null ? $"pazar payı · {enb.kisa} %{enbPay * 100f:0}" : "pazar payı", renk);
        }

        static string KisaGun(string gun)
        {
            switch (gun)
            {
                case "Pazartesi": return "Pzt";
                case "Salı": return "Sal";
                case "Çarşamba": return "Çar";
                case "Perşembe": return "Per";
                case "Cuma": return "Cum";
                case "Cumartesi": return "Cmt";
                case "Pazar": return "Paz";
                default: return string.IsNullOrEmpty(gun) || gun.Length <= 3 ? gun : gun.Substring(0, 3);
            }
        }
        static string KisaAy(string ay) => string.IsNullOrEmpty(ay) || ay.Length <= 4 ? ay : ay.Substring(0, 3);

        // ------------------------------------------------------------------ durum sütunu (sağ üst)

        VisualElement durumKok;
        U.Liste<string, DurumOge, DurumKart> durumlar;
        readonly List<DurumOge> durumListe = new List<DurumOge>();

        class DurumOge
        {
            public string anahtar, baslik, alt;
            public Icons.Kind simge;
            public Color renk;
            public Action tik;
        }

        class DurumKart
        {
            public VisualElement kok, serit, simgeKutu, simge;
            public Label baslik, alt;
            public Action tik;
        }

        void DurumKur()
        {
            durumKok = U.Gecirgen(katHud, "durum");
            durumlar = new U.Liste<string, DurumOge, DurumKart>(durumKok, o => o.anahtar, DurumKartKur, k => k.kok, DurumKartYenile);
        }

        DurumKart DurumKartKur(DurumOge o)
        {
            var k = new DurumKart();
            k.kok = U.El(null, "durum-kart");
            U.Tikla(k.kok, () => k.tik?.Invoke());
            k.serit = U.Gecirgen(k.kok, "durum-serit");
            k.simgeKutu = U.Gecirgen(k.kok, "durum-simge-kutu");
            k.simge = U.Simge(k.simgeKutu, o.simge, o.renk, 15f);
            var yazi = U.Gecirgen(k.kok, "durum-yazi");
            k.baslik = U.Kalin(yazi, "", "durum-baslik");
            k.alt = U.Yazi(yazi, "", "durum-alt");
            return k;
        }

        void DurumKartYenile(DurumKart k, DurumOge o)
        {
            k.tik = o.tik;
            U.Zemin(k.serit, o.renk);
            U.Zemin(k.simgeKutu, U.A(o.renk, 0.2f));
            U.SimgeAyarla(k.simge, o.simge, o.renk);
            U.Metin(k.baslik, o.baslik);
            U.Metin(k.alt, o.alt ?? "");
            U.Goster(k.alt, !string.IsNullOrEmpty(o.alt));
        }

        int durumKullanilan;

        DurumOge DurumEkle(string anahtar, Icons.Kind simge, Color renk, string baslik, string alt, Action tik = null)
        {
            DurumOge o;
            if (durumKullanilan < durumListe.Count) o = durumListe[durumKullanilan];
            else { o = new DurumOge(); durumListe.Add(o); }
            durumKullanilan++;
            o.anahtar = anahtar; o.simge = simge; o.renk = renk; o.baslik = baslik; o.alt = alt; o.tik = tik;
            return o;
        }

        readonly List<DurumOge> durumGoster = new List<DurumOge>();

        void DurumYenile(EconomyManager eco)
        {
            durumKullanilan = 0;

            // Acil olanlar üstte
            if (CrisisManager.OrdersBlocked)
                DurumEkle("cokme", Icons.Kind.Warning, U.Kotu, "Uygulama çöktü", $"{CrisisManager.OutageMinutesLeft:0} dk daha sipariş gelmiyor");
            if (eco.taxDebt > 0)
                DurumEkle("vergi", Icons.Kind.Warning, U.Kotu, $"Vergi borcu {Ft(eco.taxDebt)}", "Her gün %2 büyür · tıkla: Finans",
                          () => { financePage = 0; SekmeyeGit(Tab.Finans); });
            if (CrisisManager.I != null)
            {
                int i = 0;
                foreach (var ce in CrisisManager.I.Effects)
                    DurumEkle("kriz" + i++, Icons.Kind.Warning, Turuncu, ce.title, $"{CrisisManager.I.DaysLeft(ce)} gün daha", () => SekmeyeGit(Tab.Dukkanlar));
            }
            var rv = RivalCompany.I;
            if (rv != null && rv.priceWarDays > 0 && rv.warDistrict > 0)
            {
                var wo = rv.RakipAt(rv.warOwner);
                DurumEkle("savas", Icons.Kind.Flag, U.Kotu, $"Fiyat savaşı: {(wo != null ? wo.ad : "rakip")}",
                          $"{CityMap.DistrictNames[rv.warDistrict]} · {rv.priceWarDays} gün · oradaki siparişler −%{(1f - rv.warFactor) * 100f:0}",
                          () => SekmeyeGit(Tab.Dukkanlar));
            }
            if (rv != null) RakipDurumlari(rv);
            KapasiteDurumlari(eco);

            // Para
            var loan = FamilyLoan.I;
            if (loan != null && !loan.Finished && !loan.Failed && loan.NextDue != null)
            {
                var n = loan.NextDue;
                int gun = loan.DaysUntil(n);
                Color r = gun <= 3 && eco.money < n.Remaining ? U.Kotu : gun <= 7 ? Turuncu : BilgiRengi;
                DurumEkle("kredi", Icons.Kind.Wallet, r, $"Taksit {Ft(n.Remaining)} · {n.DueText}",
                          $"{(gun <= 0 ? "bugün" : gun + " gün kaldı")} · kalan kredi {Ft(loan.Remaining)} · not {loan.Trust}/{loan.maxTrust}",
                          () => { financePage = 0; SekmeyeGit(Tab.Finans); });
            }

            // Fırsatlar ve bilgi
            var boost = CampaignManager.I != null ? CampaignManager.I.ActiveWindow : null;
            float wBoost = CampaignManager.I != null ? CampaignManager.I.WeatherBoostPercent : 0f;
            if (boost != null && boost.percent >= wBoost)
                DurumEkle("boost", Icons.Kind.Bolt, Turuncu, $"Boost %{boost.percent * 100f:0}", $"{boost.TimeText} · kuryeler daha çok çevrimiçi olur",
                          () => SekmeyeGit(Tab.Kampanyalar));
            else if (wBoost > 0f)
                DurumEkle("boost", Icons.Kind.Bolt, Turuncu, $"Hava boostu %{wBoost * 100f:0}", "Kötü havada kuryelere ek ücret",
                          () => SekmeyeGit(Tab.Kampanyalar));
            var ce2 = CityEvents.I != null ? CityEvents.I.ActiveNow : null;
            if (ce2 != null)
            {
                var etk = ce2;
                DurumEkle("sehir", Icons.Kind.Calendar, BilgiRengi, etk.title, etk.TimeText + (etk.HasSpot ? " · tıkla: haritada göster" : ""),
                          etk.HasSpot ? (Action)(() => { if (MapCamera.I != null) MapCamera.I.FocusOn(etk.Spot, 0.7f); }) : null);
            }
            if (GameClock.I.NightFastActive && !GameClock.I.Paused)
                DurumEkle("gece", Icons.Kind.Moon, new Color(0.55f, 0.6f, 1f), $"Gece hızlı · {GameClock.I.EffectiveSpeed:0}×", "Gece sipariş azken zaman hızlı akar");

            durumGoster.Clear();
            for (int i = 0; i < durumKullanilan; i++) durumGoster.Add(durumListe[i]);

            // Konum: sağ üst. Kurye telefonu ya da bina kartı (sağda) açıksa onların soluna kayar (genişlikleri farklı:
            // telefon 340, bina kartı 370); yer yoksa gizlenir. Büyük kurye kartı açıksa perdesinin altında kalır.
            float sw = UiEkranGenisligi, sh = UiEkranYuksekligi;
            float sagKart = SagKartGenisligi;
            float sag = sagKart > 0f ? sagKart : 12f;
            bool sigar = sw - sag - DurumGenislik > MapLeftGui + 12f;
            bool gor = durumGoster.Count > 0 && sigar;
            U.Goster(durumKok, gor);
            if (!gor) { durumlar.Esitle(durumGoster); return; }
            durumKok.style.right = sag;
            // Alttaki olaylar kartına binmesin
            float alt = olayKok != null && U.Gorunur(olayKok) && olayKok.layout.height > 1f ? olayKok.layout.height + 24f : 60f;
            durumKok.style.maxHeight = Mathf.Max(80f, sh - TopBar - 10f - alt);
            durumlar.Esitle(durumGoster);
        }

        const float DurumGenislik = 300f;

        /// <summary>
        /// Kuryeler yetişmiyor (yoğunluk: müşteri vazgeçiyor, itibar düşüyor, teslimat alanı daraldı) ve kurye hesapları dolu
        /// (kaç kişi bekliyor, sıradaki paket ve onun istediği bölge ne kadar). 15.11 testinde 32 kuryede 30 gün beklenirken
        /// oyuncunun ne yapacağını gösteren bir şey yoktu; binlerce sipariş sessizce kaçıyordu.
        /// </summary>
        void KapasiteDurumlari(EconomyManager eco)
        {
            var om = OrderManager.I;
            if (om != null && (om.IsBusy || om.yogunKacanToday >= 15))
            {
                int toplam = om.yogunKacanToday + EconomyManager.I.Today.deliveries;
                DurumEkle("yogun", Icons.Kind.Warning, om.IsBusy ? U.Kotu : Turuncu, "Yoğunluk: kuryeler yetişmiyor",
                          $"Bugün {om.yogunKacanToday} müşteri vazgeçti (%{(toplam > 0 ? om.yogunKacanToday * 100f / toplam : 0f):0}) · itibar düşüyor · " +
                          "teslimat alanı daraldı · çare: kurye, boost ya da biraz daha yüksek ücret",
                          () => SekmeyeGit(Tab.Siparisler));
            }

            var up = UpgradeManager.I;
            var ks = KayitSistemi.I;
            if (up == null || ks == null || CityMap.I == null || !up.CourierCapFull || ks.HesapBekleyen == 0) return;
            long paket = up.NextCost(UpgradeKind.Platform);
            if (paket <= 0) return;
            var pd = UpgradeManager.Def(UpgradeKind.Platform);
            int sonraki = up.Level(UpgradeKind.Platform) + 1;
            int bolge = pd.ReqDistrict(sonraki);
            var map = CityMap.I;
            string alt;
            bool bolgeLazim = bolge > 0 && map.UnlockLevel < bolge;
            if (bolgeLazim)
            {
                long bb = 0;
                for (int b = map.UnlockLevel + 1; b <= bolge; b++) bb += CityMap.BolgeBedeli(b);
                alt = $"Önce {CityMap.DistrictNames[bolge]} bölgesi ({Ft(bb)}), sonra {pd.levelNames[sonraki]} paketi ({Ft(paket)}) · harcanabilir {Ft(eco.FreeCash)}";
            }
            else
            {
                string kilit = up.LockReason(UpgradeKind.Platform);
                alt = kilit.Length > 0 ? $"{pd.levelNames[sonraki]} paketi ({Ft(paket)}): {kilit}"
                                       : $"{pd.levelNames[sonraki]} paketi {Ft(paket)} ({pd.effect(sonraki)}) · harcanabilir {Ft(eco.FreeCash)}";
            }
            DurumEkle("hesap", Icons.Kind.Couriers, Turuncu, $"Kurye hesapları dolu · {ks.HesapBekleyen} kişi bekliyor", alt,
                      () => SekmeyeGit(bolgeLazim ? Tab.Dukkanlar : Tab.Gelistirmeler));
        }

        /// <summary>
        /// Rakiplerin tehdit seviyesi (pazar payına göre), önceden duyurulan hamleleri (kaç gün kaldığıyla) ve yeni yatırım alanlar.
        /// Hamleler RivalCompany.Tehditler'de (RakipTehditleri.cs).
        /// </summary>
        void RakipDurumlari(RivalCompany rv)
        {
            int sev = rv.TehditSeviyesi;
            if (sev >= 1)
                DurumEkle("tehdit", Icons.Kind.Flag, sev >= 2 ? U.Kotu : Turuncu,
                          sev >= 2 ? "Rakipler saldırıda" : "Rakipler tetikte",
                          $"Pazar payın %{rv.MarketShare * 100f:0} (eşik %{(sev >= 2 ? rv.TehditEsigi2 : rv.TehditEsigi1) * 100f:0}) · " +
                          (sev >= 2 ? "sık hamle, yatırım, kurye ve münhasır dükkan teklifleri" : "hamleler sıklaşır, yatırım ararlar"),
                          () => SekmeyeGit(Tab.Dukkanlar));
            int bugun = GameClock.I.Day;
            for (int i = 0; i < rv.Tehditler.Count; i++)
            {
                var t = rv.Tehditler[i];
                var r = rv.RakipAt(t.rakip);
                if (r == null || r.satinAlindi) continue;
                int kalan = t.gun - bugun;
                string ne = kalan <= 0 ? "bu gece" : kalan == 1 ? "yarın" : $"{kalan} gün içinde";
                switch (t.Tur)
                {
                    case TehditTur.Dukkan:
                        var s = rv.TehditDukkani(t);
                        if (s == null) break;
                        DurumEkle("th" + i, Icons.Kind.Shops, Turuncu, $"{r.kisa}: {s.Name} dükkanına teklif",
                                  $"{ne} · memnuniyet {s.satisfaction:0}/100 · " +
                                  (s.exclusive ? "münhasır ama rakipler saldırıda, bu da hedef" : "münhasırlık önerirsen geri çekilir"),
                                  () => SekmeyeGit(Tab.Dukkanlar));
                        break;
                    case TehditTur.Kurye:
                        string adlar = t.kuryeler.Count <= 3 ? string.Join(", ", t.kuryeler)
                                     : string.Join(", ", t.kuryeler.GetRange(0, 3)) + $" +{t.kuryeler.Count - 3}";
                        DurumEkle("th" + i, Icons.Kind.Couriers, Turuncu, $"{r.kisa}: {t.kuryeler.Count} kuryene teklif",
                                  $"{ne} · {adlar} · morali yüksek olan kalır", () => SekmeyeGit(Tab.Kuryeler));
                        break;
                    case TehditTur.FiyatSavasi:
                        DurumEkle("th" + i, Icons.Kind.Flag, U.Kotu, $"{r.kisa}: fiyat savaşı hazırlığı",
                                  $"{CityMap.DistrictNames[Mathf.Clamp(t.bolge, 0, CityMap.DistrictNames.Length - 1)]} · {ne} başlıyor",
                                  () => SekmeyeGit(Tab.Dukkanlar));
                        break;
                }
            }
            for (int i = 0; i < rv.Rakipler.Count; i++)
            {
                var r = rv.Rakipler[i];
                int gecen = bugun - r.yatirimGunu;
                if (r.satinAlindi || gecen < 0 || gecen >= 21) continue;
                DurumEkle("yatirim" + i, Icons.Kind.Coins, Turuncu, $"{r.ad} yatırım aldı",
                          $"{21 - gecen} gün daha hızla büyüyor: kurye alıyor, hizmetini iyileştiriyor", () => SekmeyeGit(Tab.Dukkanlar));
            }
        }
    }
}
