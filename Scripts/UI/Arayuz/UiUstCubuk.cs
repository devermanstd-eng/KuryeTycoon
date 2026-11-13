using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Üst çubuk (UI Toolkit): logo ve şirket adı · KASA, HARCANABİLİR, İTİBAR, ÇEVRİMİÇİ, tarih/saat/hava kartları ·
    /// uyarı hapları (çökme, vergi borcu, kriz, fiyat savaşı, boost, aile borcu, gece hızı, pazar payı) · oynat/duraklat ve hız.
    /// Sığmayan uyarı hapları daralır, yazıları "…" ile kesilir (en önemlisi solda; tamamı üstüne gelince).
    /// </summary>
    public partial class GameUI
    {
        VisualElement ustKok, ustLogo, ustUyarilar, ustOynat, ustOynatSimge;
        Label ustLogoHarf, ustAd, hizEk;
        readonly List<(Label l, float hiz)> hizDilimleri = new List<(Label, float)>();
        string ustUyariImza = "";

        class StatKart
        {
            public VisualElement kok, simgeKutu, simge;
            public Label baslik, deger;
        }

        StatKart skKasa, skHarca, skItibar, skCevrim, skZaman;

        static readonly Color KasaRengi = new Color(0.40f, 0.85f, 0.55f);
        static readonly Color HarcaRengi = new Color(0.45f, 0.75f, 1f);
        static readonly Color ItibarRengi = new Color(1f, 0.80f, 0.25f);
        static readonly Color Turuncu = new Color(1f, 0.62f, 0.20f);

        void UiUstKur()
        {
            ustKok = U.El(katHud, "ust");
            ustLogo = U.El(ustKok, "logo");
            ustLogoHarf = U.Kalin(ustLogo, "", "logo-harf");
            ustAd = U.Kalin(ustKok, "", "sirket-adi", "tek-satir");

            var kartlar = U.Gecirgen(ustKok, "ust-kartlar");
            skKasa = StatKartEkle(kartlar, Icons.Kind.Wallet, KasaRengi, "KASA",
                () => "Şirketin bütün parası. Kuryelerin hesaplarındaki hakedişler ayın 15'inde ve son günü bu paradan ödenir.");
            skHarca = StatKartEkle(kartlar, Icons.Kind.Coins, HarcaRengi, "HARCANABİLİR",
                () => "Kuryelere ödenecek hakedişler düşülünce kalan para. Harcamalarını buna göre yap.");
            skItibar = StatKartEkle(kartlar, Icons.Kind.Star, ItibarRengi, "İTİBAR", () =>
            {
                float f = ItibarFarki();
                return $"Son 14 günün müşteri puanı ortalaması (iptal 1 yıldız sayılır). Bugün {(f >= 0 ? "+" : "−")}{Mathf.Abs(f):0.00}. " +
                       "İtibar yükseldikçe sipariş artar.";
            });
            skCevrim = StatKartEkle(kartlar, Icons.Kind.Couriers, UITheme.Accent, "ÇEVRİMİÇİ", () => "Şu an çevrimiçi kurye / toplam kurye.");
            skZaman = StatKartEkle(kartlar, Icons.Kind.Sun, Color.white, "", () =>
            {
                var c = GameClock.I;
                return c == null ? "" : $"{DayNight.PhaseName} · {c.SeasonName} · Yıl {c.Year} · şirketin {c.CompanyDay}. günü";
            });

            ustUyarilar = U.Gecirgen(ustKok, "ust-uyarilar");

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
        }

        StatKart StatKartEkle(VisualElement ust, Icons.Kind simge, Color renk, string baslik, Func<string> ipucu)
        {
            var k = new StatKart();
            k.kok = U.El(ust, "stat");
            k.simgeKutu = U.El(k.kok, "stat-simge-kutu");
            k.simgeKutu.pickingMode = PickingMode.Ignore;
            k.simge = U.Simge(k.simgeKutu, simge, renk, 0f, "stat-simge");
            U.Zemin(k.simgeKutu, U.A(renk, 0.18f));
            var yazi = U.Gecirgen(k.kok, "stat-yazi");
            k.baslik = U.Kalin(yazi, baslik, "stat-baslik");
            k.deger = U.Kalin(yazi, "", "stat-deger");
            Ipucu(k.kok, ipucu);
            return k;
        }

        void StatAyarla(StatKart k, string deger, Color simgeRengi, Color? degerRengi = null, Color? zemin = null, Icons.Kind? simge = null, string baslik = null)
        {
            U.Metin(k.deger, deger);
            if (baslik != null) U.Metin(k.baslik, baslik);
            U.Renk(k.deger, degerRengi ?? UITheme.Text);
            if (zemin.HasValue) U.Zemin(k.kok, zemin.Value); else ZeminSil(k.kok);
            if (simge.HasValue) U.SimgeAyarla(k.simge, simge.Value, simgeRengi);
            else U.SimgeAyarla(k.simge, (Icons.Kind)k.simge.userData, simgeRengi);
            U.Zemin(k.simgeKutu, U.A(simgeRengi, 0.18f));
        }

        float ItibarFarki()
        {
            var omr = OrderManager.I;
            return omr != null && omr.reputationAtDayStart >= 0f ? CompanyProfile.Reputation - omr.reputationAtDayStart : 0f;
        }

        void UiUstYenile(bool agir)
        {
            var clock = GameClock.I;
            if (clock == null) return;
            var prim = CompanyProfile.Primary;
            var sec = CompanyProfile.Secondary;

            // Hız düğmeleri her karede (tıklama hemen görünsün)
            bool paused = clock.Paused;
            if (paused) U.Zemin(ustOynat, UITheme.Warn); else ZeminSil(ustOynat);
            U.SimgeAyarla(ustOynatSimge, paused ? Icons.Kind.Play : Icons.Kind.Pause, paused ? new Color(0.12f, 0.08f, 0.02f) : UITheme.Text);
            foreach (var (l, sp) in hizDilimleri)
            {
                bool on = IsSpeed(clock, sp);
                if (on) U.Zemin(l, prim); else ZeminSil(l);
                U.Renk(l, on ? sec : (paused ? U.A(UITheme.TextDim, 0.6f) : UITheme.TextDim));
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
            if (eco == null || weather == null || CourierManager.I == null) return;

            if (ustKok.style.borderBottomColor != prim) ustKok.style.borderBottomColor = prim;
            U.Zemin(ustLogo, prim);
            U.Renk(ustLogoHarf, sec);
            U.Metin(ustLogoHarf, CompanyProfile.Initial);
            U.Metin(ustAd, CompanyProfile.Name);

            StatAyarla(skKasa, Ft(eco.money), KasaRengi);
            bool freeNeg = eco.FreeCash < 0;
            StatAyarla(skHarca, Ft(eco.FreeCash), freeNeg ? UITheme.Bad : HarcaRengi, freeNeg ? UITheme.Bad : (Color?)null,
                       freeNeg ? U.A(UITheme.Bad, 0.18f) : (Color?)null);
            float repFark = ItibarFarki();
            StatAyarla(skItibar, $"{CompanyProfile.Reputation:0.0} / 5" + (repFark > 0.01f ? " ▲" : repFark < -0.01f ? " ▼" : ""), ItibarRengi);
            StatAyarla(skCevrim, $"{CourierManager.I.OnlineCount} / {CourierManager.I.Couriers.Count}", prim);
            StatAyarla(skZaman, $"{clock.Hour:00}:{clock.Minute:00} · {weather.Name}", WeatherTint(weather.Current), null, null,
                       WeatherIcon(weather.Current), $"{clock.DayName.ToUpperInvariant()} · {clock.DayOfMonth} {clock.MonthName.ToUpperInvariant()}");

            UstUyarilariYenile(eco);
        }

        struct UstHap { public Icons.Kind simge; public string yazi; public Color zemin, renk; }
        readonly List<UstHap> ustHaplar = new List<UstHap>();

        void UstUyarilariYenile(EconomyManager eco)
        {
            ustHaplar.Clear();
            void Ekle(Icons.Kind k, string t, Color bg, Color fg) => ustHaplar.Add(new UstHap { simge = k, yazi = t, zemin = bg, renk = fg });

            // Önce acil olanlar (çökme, vergi borcu, kriz, fiyat savaşı), sonra boost, aile borcu, gece, pazar payı
            if (CrisisManager.OrdersBlocked)
                Ekle(Icons.Kind.Warning, $"Uygulama çöktü · {CrisisManager.OutageMinutesLeft:0} dk", UITheme.Bad, Color.white);
            if (eco.taxDebt > 0)
                Ekle(Icons.Kind.Warning, $"Vergi borcu {Ft(eco.taxDebt)}", UITheme.Bad, Color.white);
            if (CrisisManager.I != null)
                foreach (var ce in CrisisManager.I.Effects)
                    Ekle(Icons.Kind.Warning, $"{ce.title} · {CrisisManager.I.DaysLeft(ce)} g", U.A(Turuncu, 0.35f), UITheme.Text);
            if (RivalCompany.I != null && RivalCompany.I.priceWarDays > 0 && RivalCompany.I.warDistrict > 0)
            {
                var wo = RivalCompany.I.RakipAt(RivalCompany.I.warOwner);
                Ekle(Icons.Kind.Flag, $"Fiyat savaşı{(wo != null ? " (" + wo.kisa + ")" : "")}: {CityMap.DistrictNames[RivalCompany.I.warDistrict]} · {RivalCompany.I.priceWarDays} g",
                     U.A(UITheme.Bad, 0.6f), Color.white);
            }
            var boost = CampaignManager.I != null ? CampaignManager.I.ActiveWindow : null;
            float wBoost = CampaignManager.I != null ? CampaignManager.I.WeatherBoostPercent : 0f;
            var koyu = new Color(0.12f, 0.08f, 0.02f);
            if (boost != null && boost.percent >= wBoost)
                Ekle(Icons.Kind.Bolt, $"Boost %{boost.percent * 100f:0} · {boost.TimeText}", Turuncu, koyu);
            else if (wBoost > 0f)
                Ekle(Icons.Kind.Bolt, $"Hava boostu %{wBoost * 100f:0}", Turuncu, koyu);
            if (FamilyLoan.I != null && !FamilyLoan.I.Finished)
                Ekle(Icons.Kind.Wallet, FamilyLoan.I.TopBarText, U.A(Color.white, 0.08f), UITheme.Text);
            if (GameClock.I.NightFastActive && !GameClock.I.Paused)
                Ekle(Icons.Kind.Moon, $"Gece hızlı · {GameClock.I.EffectiveSpeed:0}×", new Color(0.22f, 0.26f, 0.45f), Color.white);
            if (RivalCompany.I != null)
            {
                var rvc = RivalCompany.I;
                bool noData = rvc.myDeliveriesLastWeek + rvc.deliveriesLastWeek == 0;
                float sh = rvc.MarketShare, tr = rvc.ShareTrend;
                string arrow = tr > 0.005f ? " ▲" : tr < -0.005f ? " ▼" : "";
                // En büyük rakip (geçen haftanın payına göre)
                Rakip enb = null;
                foreach (var r in rvc.Rakipler) if (enb == null || rvc.PayiOf(r) > rvc.PayiOf(enb)) enb = r;
                float enbPay = enb != null ? rvc.PayiOf(enb) : 0f;
                string t = noData ? $"Pazar payı: ilk hafta sonunda · {rvc.Rakipler.Count} rakip"
                                  : $"Pazar payı %{sh * 100f:0}{arrow} · {(enb != null ? enb.kisa : "rakip")} %{enbPay * 100f:0}";
                // Renk: en büyük rakibi geçtiysen yeşil, yakınsan turuncu, gerideysen kırmızı
                Color bg = sh >= enbPay * 1.15f ? U.A(KasaRengi, 0.22f) : sh >= enbPay * 0.8f ? U.A(Turuncu, 0.30f) : UITheme.Bad;
                Ekle(Icons.Kind.Chart, t, bg, UITheme.Text);
            }

            // Değişmediyse ağaç yeniden kurulmaz
            var sb = new System.Text.StringBuilder();
            foreach (var h in ustHaplar) sb.Append(h.simge).Append(h.yazi).Append(h.zemin).Append('|');
            string imza = sb.ToString();
            if (imza == ustUyariImza) return;
            ustUyariImza = imza;
            ustUyarilar.Clear();
            foreach (var h in ustHaplar)
            {
                var hap = U.Gecirgen(ustUyarilar, "hap");
                U.Zemin(hap, h.zemin);
                U.Simge(hap, h.simge, h.renk, 0f, "hap-simge");
                var l = U.Kalin(hap, h.yazi, "hap-yazi");
                U.Renk(l, h.renk);
                Ipucu(hap, h.yazi);   // sığmayınca yazı "…" ile kesilir; tamamı burada
            }
        }
    }
}
