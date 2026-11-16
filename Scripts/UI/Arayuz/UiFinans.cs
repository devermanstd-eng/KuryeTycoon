using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Finans penceresi (UI Toolkit, Yazıcı ile). Üstte (kaymaz) sayfalar: Özet · Ödemeler · Vergi · Hareketler · Diğer.
    /// Sade: Özet sayı kutularıyla başlar (harcanabilir, bugün ve dün net, sabit gider), gelir-gider "bugün | dün" tablosunda
    /// yan yana; kredide sadece sıradaki taksit ve ilerleme görünür, taksit planı açılır. Açıklamalar "?" ipuçlarında.
    /// </summary>
    public partial class GameUI
    {
        class UiFinans : UiSekme
        {
            Yazici y;
            U.Secenekler sayfalar;
            int gosterilenSayfa = -1;
            bool taksitPlani;

            protected override void Kur()
            {
                sayfalar = U.SecenekEkle(Ust(), new[] { "Özet", "Ödemeler", "Vergi", "Hareketler", "Diğer" },
                                         () => ui.financePage, v => { ui.financePage = v; Yenile(); });
                sayfalar.kok.style.marginTop = 2;
                sayfalar.kok.style.marginBottom = 8;
                y = new Yazici(ui, kok);
            }

            public override void Yenile()
            {
                var e = EconomyManager.I;
                var clock = GameClock.I;
                if (e == null || clock == null) return;
                sayfalar.Yenile();
                if (gosterilenSayfa != ui.financePage)
                {
                    gosterilenSayfa = ui.financePage;
                    // Sayfa değişince kaydırma başa dönsün
                    var sv = kok.GetFirstAncestorOfType<UnityEngine.UIElements.ScrollView>();
                    if (sv != null) sv.scrollOffset = Vector2.zero;
                }
                y.Basla();
                switch (ui.financePage)
                {
                    case 0: Ozet(e); break;
                    case 1: Odemeler(e); break;
                    case 2: Vergi(e, clock); break;
                    case 3:
                        y.Bolum("Hesap hareketleri", "Son 25 hareket, en yenisi üstte.");
                        for (int i = 0; i < Mathf.Min(25, e.Ledger.Count); i++) y.Kucuk(e.Ledger[i]);
                        break;
                    default:
                        Sigorta(e);
                        KilometreTaslari();
                        break;
                }
                y.Bitir();
            }

            void Ozet(EconomyManager e)
            {
                var dun = e.Yesterday;
                IsletmeGiderleri.Hesapla((dun ?? e.Today).courierPay, (dun ?? e.Today).DeliveryProfit, out _, out long isletme, out _, out _, out _);
                long sabit = isletme + (UpgradeManager.I != null ? UpgradeManager.I.TotalDailyCost : 0);
                long bugunNet = e.Today.Profit - e.Today.penalties;
                y.KutularBasla();
                y.Kutu("Harcanabilir", Ft(e.FreeCash), $"kasa {Ft(e.money)}", e.FreeCash < 0 ? UITheme.Bad : (Color?)null,
                       "Serbest kasa = ana kasa − kuryelerin hesaplarında bekleyen ve yatacak paralar + kuryelerdeki nakit (şirkete borçları). " +
                       "Geliştirme, ekipman, sözleşme gibi harcamalar serbest kasadan yapılır.");
                y.Kutu("Bugün net", Ft(bugunNet), $"{e.Today.deliveries} teslimat", MoneyColor(bugunNet));
                if (dun != null)
                {
                    long dunNet = dun.Profit - dun.penalties;
                    y.Kutu("Dün net", Ft(dunNet), dun.deliveries > 0 ? $"paket başı {Ft(dun.DeliveryProfit / dun.deliveries)}" : "", MoneyColor(dunNet));
                }
                y.Kutu("Sabit gider", Ft(sabit) + "/gün", "işletme + geliştirme", null,
                       "Her gece kesilen işletme giderleri (aşağıda kalem kalem) ve geliştirmelerin günlük işletme bedeli.");
                y.KutularBitir();
                if (e.bankLoanRemaining > 0)
                    y.Uyari($"Acil banka kredisi: kalan {Ft(e.bankLoanRemaining)} · her gün {Ft(e.bankLoanDaily)} kesilir");
                if (e.taxDebt > 0)
                {
                    y.Uyari($"Vergi borcu: {Ft(e.taxDebt)} — her gün %2 büyür!");
                    y.Dugme($"Vergi borcunu öde ({Ft(e.taxDebt)})", () => e.PayDebt(), true, "vurgu");
                }
                y.SatirBasla();
                if (!string.IsNullOrEmpty(e.LastDailyReport))
                    y.Dugme("Dünün raporu", () => EventManager.I.Push(new GameEvent(e.LastDailyReportTitle, e.LastDailyReport)), true, "kucuk-dg");
                if (!string.IsNullOrEmpty(e.LastWeeklyReport))
                    y.Dugme("Geçen haftanın raporu", () => EventManager.I.Push(new GameEvent(e.LastWeeklyReportTitle, e.LastWeeklyReport)), true, "kucuk-dg");
                y.SatirBitir();
                y.Bolum("Bugün ve dün", "Teslimat kârı = komisyon + teslimat ücreti − kurye ödemeleri − boost. Net kâr = teslimat kârı − diğer giderler + diğer gelirler.");
                DonemTablosu(e.Today, "Bugün", dun, "Dün");
                Kredi(e);
                IsletmeGiderleriBolumu(e);
            }

            void Kredi(EconomyManager e)
            {
                var loan = FamilyLoan.I;
                if (loan == null) return;
                y.KartBasla();
                y.Baslik($"Kurulum kredisi ({FamilyLoan.Banka})");
                y.Yardim("Kredi taksiti gider değildir, vergiden düşülmez (faiz taksitlerin içinde). Erken ödemek taksiti kapatır; ama kasada kötü günler için yedek bırak.");
                if (loan.Finished)
                {
                    y.Yazi($"Kredi faiziyle tamamen ödendi ({Ft(loan.Toplam)}). Şirket artık kimseye borçlu değil.");
                    y.KartBitir();
                    return;
                }
                if (loan.Failed) y.Uyari("Kredi ödenemedi, banka geri çağırdı ve şirket kapandı (test modunda devam ediyorsun).");
                var sonraki = loan.NextDue;
                y.KutularBasla();
                y.Kutu("Kalan", Ft(loan.Remaining), $"toplam {Ft(loan.Toplam)}", null,
                       $"Çekilen {Ft(loan.principal)} + faiz {Ft(loan.Faiz)}. Bugüne kadar ödenen {Ft(loan.TotalPaid)}.");
                if (sonraki != null)
                {
                    int gun = loan.DaysUntil(sonraki);
                    y.Kutu("Sıradaki taksit", Ft(sonraki.Remaining), $"{sonraki.DueText} · {gun} gün", null, null, gun <= 7 ? UITheme.Warn : (Color?)null);
                }
                y.Kutu("Kredi notu", $"{loan.Trust}/{loan.maxTrust}", null, loan.Trust < loan.maxTrust / 2 ? UITheme.Warn : (Color?)null,
                       "Taksitleri vadesinde ödedikçe yükselir, kaçırınca düşer.");
                y.KutularBitir();
                y.Cubuk("Ödenen", loan.TotalPaid / (float)Mathf.Max(1L, loan.Toplam), new Color(0.35f, 0.8f, 0.5f),
                        $"%{loan.TotalPaid * 100f / Mathf.Max(1L, loan.Toplam):0}");
                y.AcilirBaslik("Taksit planı", $"{loan.Schedule.Count} taksit", null, taksitPlani, () => taksitPlani = !taksitPlani);
                if (taksitPlani)
                    foreach (var t in loan.Schedule)
                    {
                        string durum = t.closed ? (t.Remaining == 0 ? "ödendi" : "kaçırıldı") : $"{loan.DaysUntil(t)} gün";
                        y.Tablo(new[] { t.DueText, Ft(t.amount), durum }, false, null,
                                new Color?[] { null, null, t.closed ? (t.Remaining == 0 ? UITheme.Good : UITheme.Bad) : loan.DaysUntil(t) <= 7 ? UITheme.Warn : (Color?)null });
                    }
                if (sonraki != null && !loan.Failed)
                {
                    y.SatirBasla();
                    long taksit = sonraki.Remaining;
                    y.Dugme($"Taksiti öde ({Ft(taksit)})", () => loan.Pay(taksit), e.money >= taksit, "kucuk-dg");
                    y.Dugme($"{Ft(50000)} erken öde", () => ui.ConfirmIfExpensive(50000, "Krediye erken ödeme",
                            $"{FamilyLoan.Banka} kredisine {Ft(50000)} erken ödensin mi? Serbest kasan {Ft(EconomyManager.I.FreeCash)}.",
                            () => loan.Pay(50000)), e.money >= 50000, "kucuk-dg");
                    long kalan = loan.Remaining;
                    y.Dugme($"Tamamını öde ({Ft(kalan)})", () => loan.Pay(kalan), e.money >= kalan, "kucuk-dg");
                    y.SatirBitir();
                }
                y.KartBitir();
            }

            /// <summary>Şirket büyüdükçe artan günlük giderler (IsletmeGiderleri.cs): kalem kalem, tavanla birlikte.</summary>
            void IsletmeGiderleriBolumu(EconomyManager e)
            {
                var dun = e.Yesterday ?? e.Today;
                var kalemler = IsletmeGiderleri.Hesapla(dun.courierPay, dun.DeliveryProfit, out long tam, out long kesilen, out long tavan,
                                                        out float oran, out string neden);
                y.Bolum("İşletme giderleri (günlük)",
                        "Şirket büyüdükçe artan sabit giderler; her gece kesilir. 10 kurye ve tek bölgeye kadar yok. " +
                        "Bölge operasyonu (her yeni bölgede saha ekibi ve depo), filo sorumluluk sigortası (11. kuryeden sonra), " +
                        "yönetim ve İK (15. kuryeden sonra, ekip büyüdükçe pahalanır), kurye sosyal katkısı (21+ kuryede kurye ödemelerinin %3'ü, 36+ kuryede %6'sı).\n\n" +
                        $"Tavan: toplam, son 7 günün ortalama teslimat kârının %{IsletmeGiderleri.NormalTavan * 100f:0}'ini geçmez; fiyat savaşı sürerken " +
                        $"%{IsletmeGiderleri.SavasTavani * 100f:0}'sini, son 7 gün zarardaysa %{IsletmeGiderleri.ZararTavani * 100f:0}'ini. Şirket tek seferde batmasın diye.");
                if (kalemler.Count == 0)
                {
                    y.Kucuk("Şirket henüz küçük: işletme gideri yok (11. kurye ya da 2. bölgeyle başlar).");
                    return;
                }
                foreach (var k in kalemler)
                {
                    y.KV(k.ad, Ft(k.kesilen) + (k.kesilen < k.tam ? $" (tamamı {Ft(k.tam)})" : ""));
                    y.Yardim(k.aciklama);
                }
                y.KV("Toplam (bu gece kesilecek)", Ft(kesilen), UITheme.Warn);
                if (kesilen < tam)
                    y.Not($"Tavan devrede: teslimat kârının %{oran * 100f:0}'i ({Ft(tavan)}){(neden.Length > 0 ? " · " + neden : "")}. Tamamı {Ft(tam)} olurdu.");
            }

            void Odemeler(EconomyManager e)
            {
                y.Bolum("Kurye ödemeleri");
                y.Yazi("Kurye kazançları hesaplarında birikir; ayın 15'i ve son günü kasandan ödenir.");
                y.Yardim("Bahşişler dahildir. Hesap gece kesilir, 1–2 iş günü içinde kasadan ödenir. Kuryeler istedikleri gün hesaplarının %80'ini " +
                         "erken çekebilir (2–3 iş günü). " + (Sade.NakitBorcu
                             ? $"Nakit siparişlerde kurye parayı şirkete borçlanır: üzerindeki nakit {Ft(Courier.CashLimit)}'ı geçemez, " +
                               "ay sonuna kadar keep cash (hesabın en fazla %80'iyle mahsup) ya da offload (kartla ödeme) ile kapatılır."
                             : "Nakit siparişlerin parası doğrudan kasana girer."));
                y.KutularBasla();
                y.Kutu("Hesaplarda", Ft(e.TotalCourierBalances), $"kesim {e.NextCutText}");
                y.Kutu("Yatacak", Ft(e.TotalPendingPayouts), $"{e.Payouts.Count} ödeme");
                if (Sade.NakitBorcu || e.TotalCashHeld > 0) y.Kutu("Kuryelerdeki nakit", Ft(e.TotalCashHeld), "şirkete borçları");
                if (e.depositsHeld > 0) y.Kutu("Depozitolar", Ft(e.depositsHeld), "iade borcu", null, "Gelir değil: ekipmanı iade eden kuryeye geri ödenir.");
                y.KutularBitir();
                if (e.Payouts.Count > 0)
                {
                    y.Tablo(new[] { "Kurye", "Tutar", "Yatacağı gün" }, true);
                    foreach (var p in e.Payouts)
                        y.Tablo(new[] { p.courierName + (p.early ? " (erken)" : ""), Ft(p.amount), GameClock.I.DateOf(p.payDay).Split(',')[0] });
                }
            }

            void Vergi(EconomyManager e, GameClock clock)
            {
                long vergilenen = System.Math.Max(0L, e.Year.Profit);
                long tahmin = EconomyManager.ProgressiveTax(vergilenen);
                y.KutularBasla();
                y.Kutu($"{clock.Year} kârı", Ft(e.Year.Profit), $"{clock.CompanyDay}. gün", MoneyColor(e.Year.Profit),
                       "1 Ocak – 31 Aralık. Yıl sonunda kâr üzerinden kademeli vergi ödenir.");
                y.Kutu("Tahmini vergi", Ft(tahmin), $"ortalama %{EconomyManager.EffectiveTaxRate(vergilenen) * 100f:0.0}", null,
                       EconomyManager.TaxBracketText);
                y.Kutu("Ödenen geçici", Ft(e.GeciciVergiOdenen), "bu yıl");
                y.Kutu("Sıradaki geçici", Ft(e.TahminiGeciciVergi), EconomyManager.SiradakiVergiTarihi(clock.Month), UITheme.Warn,
                       "Kademeli vergi 3 ayda bir peşin istenir (31 Mart, 30 Haziran, 30 Eylül): yılbaşından bu yana kârın vergisi − bu yıl ödenen. " +
                       "Yıl sonunda kesin hesap yapılır: kalan fark ödenir, fazlası iade edilir. Ertelemek %10 ceza ve günlük %2 faiz getirir.");
                y.KutularBitir();
                y.Bolum($"Bu vergi yılı · {clock.Year}");
                DonemTablosu(e.Year, "Bu yıl", null, null);
                if (!string.IsNullOrEmpty(e.LastTaxSummary))
                {
                    y.Bolum("Son vergi dönemi");
                    y.Kucuk(e.LastTaxSummary);
                }
            }

            /// <summary>
            /// Gelir-gider tablosu: bir ya da iki dönem yan yana (bugün | dün). Satırlar: teslimat, gelir, kurye ödemesi, teslimat kârı,
            /// paket başı kâr, diğer giderler, diğer gelir, net kâr. Diğer giderlerin kalemleri satırın ipucunda.
            /// </summary>
            void DonemTablosu(PeriodStats a, string aAd, PeriodStats b, string bAd)
            {
                bool iki = b != null;
                string[] Satir(string ad, System.Func<PeriodStats, string> f) => iki ? new[] { ad, f(a), f(b) } : new[] { ad, f(a) };
                y.Tablo(iki ? new[] { "", aAd, bAd } : new[] { "", aAd }, true);
                y.Tablo(Satir("Teslimat", s => s.deliveries.ToString()));
                y.Tablo(Satir("Gelir (komisyon + ücret)", s => Ft(s.commission + s.fees)));
                y.Tablo(Satir("Kurye ödemesi ve boost", s => "−" + Ft(s.courierPay + s.boosts)));
                y.Tablo(Satir("Teslimat kârı", s => Ft(s.DeliveryProfit)), false, null, Renkler(iki, s => s.DeliveryProfit, a, b));
                y.Tablo(Satir("Paket başı kâr", s => s.deliveries > 0 ? Ft(s.DeliveryProfit / s.deliveries) : "–"));
                y.Tablo(Satir("Diğer giderler", s => s.OtherExpenses + s.rewards > 0 ? "−" + Ft(s.OtherExpenses + s.rewards) : "–"), false, null, null,
                        "Geliştirme, işletme, ekipman, maaş, bölge, sözleşme, iade… · " + aAd + ": " + Kalemler(a) + (iki ? " · " + bAd + ": " + Kalemler(b) : ""));
                if (a.otherIncome > 0 || (iki && b.otherIncome > 0))
                    y.Tablo(Satir("Diğer gelir", s => s.otherIncome > 0 ? "+" + Ft(s.otherIncome) : "–"), false, null, null,
                            "Kilometre taşı ödülleri, sigorta komisyonu, kurye kesintileri.");
                if (a.penalties > 0 || (iki && b.penalties > 0))
                    y.Tablo(Satir("Cezalar", s => s.penalties > 0 ? "−" + Ft(s.penalties) : "–"), false, null, null, "Vergi cezası ve faizi (vergiden düşülmez).");
                y.Tablo(Satir("Net kâr", s => Ft(s.Profit - s.penalties)), false, null, Renkler(iki, s => s.Profit - s.penalties, a, b));
            }

            static Color?[] Renkler(bool iki, System.Func<PeriodStats, long> f, PeriodStats a, PeriodStats b) =>
                iki ? new Color?[] { null, MoneyColor(f(a)), MoneyColor(f(b)) } : new Color?[] { null, MoneyColor(f(a)) };

            static string Kalemler(PeriodStats s)
            {
                string liste = "";
                for (int i = 0; i < s.byCategory.Length; i++)
                    if (s.byCategory[i] > 0) liste += (liste.Length > 0 ? ", " : "") + $"{PeriodStats.CategoryNames[i]} {Ft(s.byCategory[i])}";
                if (s.rewards > 0) liste += (liste.Length > 0 ? ", " : "") + $"hedef ödülleri {Ft(s.rewards)}";
                return liste.Length > 0 ? liste : "yok";
            }

            readonly System.Collections.Generic.List<string> planAdlari = new System.Collections.Generic.List<string>();

            void Sigorta(EconomyManager e)
            {
                y.KartBasla();
                y.Baslik("Sigorta ortaklığı");
                y.Yardim("Kuryeler sigorta yaptırmak zorunda değil. Yaptırmak isteyenler şirketin anlaşmalı sigortacısı üzerinden yaptırır, " +
                         "haftalık primden şirket komisyon alır. Dışarıdan sigortalı kuryeler zamanla anlaşmaya geçebilir.");
                var p = e.Plan;
                int sigortali = 0, anlasmali = 0;
                long haftalik = 0;
                foreach (var c in CourierManager.I.Couriers)
                {
                    if (!c.insured) continue;
                    sigortali++;
                    if (c.insuredViaCompany) { anlasmali++; haftalik += GameData.Round10(c.CurrentPremium * p.commission); }
                }
                y.Yazi($"Şu an: {p.name} · sigortalı kurye {sigortali}/{CourierManager.I.Couriers.Count} (anlaşma üzerinden {anlasmali}) · " +
                       $"haftalık komisyon ~{Ft(haftalik)} · bugüne kadar {Ft(e.insuranceIncomeTotal)}");
                for (int i = 0; i < EconomyManager.InsurancePlans.Length; i++)
                {
                    var pl = EconomyManager.InsurancePlans[i];
                    int idx = i;
                    string yazi = i == 0 ? pl.name : $"{pl.name} · prim {Ft(pl.premium)}/hafta · şirket payı %{pl.commission * 100f:0}";
                    bool secili = e.insurancePlan == i;
                    y.Dugme((secili ? "✓ " : "") + yazi, () => { if (!secili) e.SetInsurancePlan(idx); },
                            secili || e.CanChangeInsurancePlan, secili ? "kucuk-dg vurgu" : "kucuk-dg");
                    y.Kucuk(pl.desc);
                }
                if (!e.CanChangeInsurancePlan)
                    y.Kucuk($"Anlaşma {EconomyManager.InsurancePlanCooldownDays} günde bir değiştirilebilir ({GameClock.I.DateOf(e.insurancePlanChangeDay + EconomyManager.InsurancePlanCooldownDays)}).");
                y.KartBitir();
            }

            void KilometreTaslari()
            {
                var ms = Milestones.I;
                if (ms == null) return;
                y.Bolum($"Kilometre taşları: {ms.ReachedCount}/{ms.Defs.Count}");
                y.SatirBasla();
                y.Dugme(ui.showMilestones ? "Gizle" : "Göster", () => ui.showMilestones = !ui.showMilestones, true, "kucuk-dg");
                y.SatirBitir();
                if (!ui.showMilestones) return;
                foreach (var m in ms.Defs)
                {
                    string odul = m.RewardText;
                    if (ms.IsReached(m.id))
                        y.Yazi($"✓ {m.title} — {ms.ReachedDate(m.id)}" + (odul.Length > 0 ? $" · ödül alındı: {odul}" : ""), null, UITheme.Good);
                    else
                    {
                        string ilerleme = m.progress != null ? m.progress() : "";
                        y.Kucuk($"{m.title}" + (string.IsNullOrEmpty(ilerleme) ? "" : $" ({ilerleme})") + (odul.Length > 0 ? $" · ödül: {odul}" : ""));
                    }
                }
            }
        }
    }
}
