using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Finans penceresi (UI Toolkit, Yazıcı ile). Üstte (kaymaz) sayfalar: Özet · Ödemeler · Vergi · Hareketler · Diğer.
    /// İçerik IMGUI'deki DrawFinance, DrawFamilyLoan, Stats, DrawInsurancePartner ve DrawMilestones ile aynı.
    /// </summary>
    public partial class GameUI
    {
        class UiFinans : UiSekme
        {
            Yazici y;
            U.Secenekler sayfalar;
            int gosterilenSayfa = -1;

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
                y.Bolum("Kasa", "Serbest kasa = ana kasa − kuryelerin hesaplarında bekleyen ve yatacak paralar + kuryelerdeki nakit (şirkete borçları). " +
                                "Geliştirme, ekipman, sözleşme gibi harcamalar serbest kasadan yapılır.");
                y.KV("Ana kasa", Ft(e.money));
                y.KV("Serbest kasa (harcanabilir)", Ft(e.FreeCash), e.FreeCash < 0 ? UITheme.Bad : (Color?)null);
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
                Kredi(e);
                y.Bolum("Bugün");
                Donem(e.Today);
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
                y.Yazi($"Kalan: {Ft(loan.Remaining)} / {Ft(loan.Toplam)} (çekilen {Ft(loan.principal)} + faiz {Ft(loan.Faiz)}) · Kredi notu: {loan.Trust}/{loan.maxTrust}");
                y.Cubuk("Ödenen", loan.TotalPaid / (float)Mathf.Max(1L, loan.Toplam), new Color(0.35f, 0.8f, 0.5f),
                        $"%{loan.TotalPaid * 100f / Mathf.Max(1L, loan.Toplam):0}");
                foreach (var t in loan.Schedule)
                {
                    string durum = t.closed ? (t.Remaining == 0 ? "ödendi" : "kaçırıldı") : $"{loan.DaysUntil(t)} gün kaldı";
                    y.Yazi($"• {t.DueText}: {Ft(t.amount)} — ödenen {Ft(t.paid)} ({durum})",
                           !t.closed && loan.DaysUntil(t) <= 7 ? "kucuk uyari-renk" : "kucuk soluk");
                }
                var sonraki = loan.NextDue;
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

            void Odemeler(EconomyManager e)
            {
                y.Bolum("Kurye ödemeleri");
                y.Yazi("Kurye kazançları hesaplarında birikir; ayın 15'i ve son günü kasandan ödenir.");
                y.Yardim("Bahşişler dahildir. Hesap gece kesilir, 1–2 iş günü içinde kasadan ödenir. Kuryeler istedikleri gün hesaplarının %80'ini " +
                         "erken çekebilir (2–3 iş günü). " + (Sade.NakitBorcu
                             ? $"Nakit siparişlerde kurye parayı şirkete borçlanır: üzerindeki nakit {Ft(Courier.CashLimit)}'ı geçemez, " +
                               "ay sonuna kadar keep cash (hesabın en fazla %80'iyle mahsup) ya da offload (kartla ödeme) ile kapatılır."
                             : "Nakit siparişlerin parası doğrudan kasana girer."));
                y.KV("Kuryelerin hesaplarında", Ft(e.TotalCourierBalances));
                y.KV("Sıradaki kesim", e.NextCutText);
                y.KV("Yatmayı bekleyen ödemeler", Ft(e.TotalPendingPayouts));
                foreach (var p in e.Payouts)
                    y.Kucuk($"{p.courierName}: {Ft(p.amount)}{(p.early ? " (erken)" : "")} → {GameClock.I.DateOf(p.payDay)}");
                if (Sade.NakitBorcu || e.TotalCashHeld > 0) y.KV("Kuryelerdeki nakit (şirkete borçları)", Ft(e.TotalCashHeld));
                if (e.depositsHeld > 0)
                {
                    y.KV("Tutulan ekipman depozitoları", Ft(e.depositsHeld));
                    y.Yardim("Gelir değil: ekipmanı iade eden kuryeye geri ödenir.");
                }
            }

            void Vergi(EconomyManager e, GameClock clock)
            {
                y.Bolum($"Bu vergi yılı · {clock.Year}", $"1 Ocak – 31 Aralık. Şirketin {clock.CompanyDay}. günü. Yıl sonunda kâr üzerinden vergi ödenir.");
                Donem(e.Year);
                long vergilenen = System.Math.Max(0L, e.Year.Profit);
                long tahmin = EconomyManager.ProgressiveTax(vergilenen);
                y.KV($"Tahmini yıl sonu vergisi (kademeli, ortalama %{EconomyManager.EffectiveTaxRate(vergilenen) * 100f:0.0})", Ft(tahmin));
                if (e.Yesterday != null)
                {
                    y.Bolum("Dün");
                    Donem(e.Yesterday);
                }
                if (!string.IsNullOrEmpty(e.LastTaxSummary))
                {
                    y.Bolum("Son vergi dönemi");
                    y.Kucuk(e.LastTaxSummary);
                }
            }

            /// <summary>Bir dönemin gelir-gider dökümü (IMGUI'deki Stats).</summary>
            void Donem(PeriodStats s)
            {
                y.Kucuk($"Teslimat: {s.deliveries} paket");
                y.Kucuk($"  + Komisyon {Ft(s.commission)}  + Teslimat ücreti {Ft(s.fees)}");
                y.Kucuk($"  − Kurye ödemeleri {Ft(s.courierPay)}" + (s.boosts > 0 ? $"  − Boost {Ft(s.boosts)}" : ""));
                y.KV("Teslimat kârı" + (s.deliveries > 0 ? $" (paket başı {Ft(s.DeliveryProfit / s.deliveries)})" : ""), Ft(s.DeliveryProfit));

                long digerleri = s.OtherExpenses + s.rewards;
                if (digerleri > 0 || s.otherIncome > 0)
                {
                    string liste = "";
                    for (int i = 0; i < s.byCategory.Length; i++)
                        if (s.byCategory[i] > 0) liste += (liste.Length > 0 ? ", " : "") + $"{PeriodStats.CategoryNames[i]} {Ft(s.byCategory[i])}";
                    if (s.rewards > 0) liste += (liste.Length > 0 ? ", " : "") + $"Hedef ödülleri {Ft(s.rewards)}";
                    y.KV("Yatırım ve diğer giderler", "−" + Ft(digerleri));
                    if (liste.Length > 0) y.Kucuk("  " + liste);
                    if (s.otherIncome > 0) y.Kucuk($"  + Diğer gelir (ödüller, sigorta komisyonu, kesintiler) {Ft(s.otherIncome)}");
                }
                if (s.penalties > 0) y.Uyari($"Cezalar (vergiden düşülmez): −{Ft(s.penalties)}");
                y.KV("Net kâr", Ft(s.Profit - s.penalties), MoneyColor(s.Profit - s.penalties));
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
