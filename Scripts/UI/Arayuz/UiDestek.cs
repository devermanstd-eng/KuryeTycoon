using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Destek penceresi (UI Toolkit, Yazıcı ile; eski IMGUI DrawSupport + DrawOperations yerine).
    /// Üstte sayı kutuları (bugünkü kapsama, vardiyadaki kişi, aylık maaş, bu ay çözülen), 24 saatlik kapsama şeridi;
    /// destek ekibi, başvurular (vardiya ve gün seçimiyle), operasyon sorumlusu ve onun kuralları.
    /// </summary>
    public partial class GameUI
    {
        /// <summary>Yetenek yıldızları (zengin metin): dolu sarı, boş soluk.</summary>
        static string YildizYazi(int n)
        {
            n = Mathf.Clamp(n, 0, 5);
            return $"<color=#FFC94D>{new string('★', n)}</color><color=#FFFFFF33>{new string('★', 5 - n)}</color>";
        }

        class UiDestek : UiSekme
        {
            Yazici y;
            readonly List<string> ekipmanAdlari = new List<string>();
            static readonly string[] VardiyaAdlari = { SupportManager.ShiftLabel((SupportShift)0), SupportManager.ShiftLabel((SupportShift)1), SupportManager.ShiftLabel((SupportShift)2) };
            static readonly string[] GunAdlari = { "Pazartesi–Cuma", "Çarşamba–Pazar" };
            static readonly string[] SormaAdlari = { "Hemen", "10 teslimattan sonra", "30 teslimattan sonra", "60 teslimattan sonra" };
            static readonly int[] SormaDegerleri = { 0, 10, 30, 60 };

            protected override void Kur() { y = new Yazici(ui, kok); }

            public override void Yenile()
            {
                var sm = SupportManager.I;
                var clock = GameClock.I;
                var eco = EconomyManager.I;
                if (sm == null || clock == null || eco == null) return;
                y.Basla();

                y.Yazi("Maaşlı destek ekibi vardiyasında şikayetleri senin yerine çözer. Vardiyada kimse yoksa kararlar sana kalır.");
                y.Yardim("Sabit vardiyada, haftada 5 gün çalışırlar. Eksik ürün/hasarlı sipariş şikayetlerini pazarlıkla çözerler (sana kart gelmez), " +
                         "kurye bulunamayan siparişlerde müşteriyi arayıp ek süre alırlar, yanlış adreste müşteriyi arayıp doğru adresi bulurlar.");

                int kapsama = sm.CoveredHours(clock.DayOfWeek);
                var nobetci = sm.OnDutyAgent();
                y.KutularBasla();
                y.Kutu("Bugün kapsama", $"{kapsama}/24 saat", null, kapsama >= 16 ? UITheme.Good : kapsama >= 8 ? (Color?)null : UITheme.Warn);
                y.Kutu("Şu an", nobetci != null ? nobetci.name.Split(' ')[0] : "kimse yok", nobetci != null ? "vardiyada" : "kararlar sana",
                       nobetci != null ? UITheme.Good : UITheme.Warn);
                y.Kutu("Aylık maaş", Ft(sm.MonthlyPayroll), $"bu ay birikmiş {Ft(sm.AccruedThisMonth)}", null, "Ay sonunda ödenir.");
                y.Kutu("Bu ay çözülen", sm.handledThisMonth.ToString(), "şikayet");
                y.KutularBitir();
                y.Ozel("kapsama", () => new SaatCizelgesi()).Ayarla(h => sm.CoveredAt(clock.DayOfWeek, h), clock.Hour);
                if (sm.AccruedThisMonth > 0 && sm.AccruedThisMonth > eco.FreeCash)
                    y.Uyari($"Ay sonu maaşı ({Ft(sm.AccruedThisMonth)}) serbest kasandan ({Ft(eco.FreeCash)}) fazla, aynı günlerde kurye ödemeleri de var: iflas riski!");

                // Ekip
                y.Bolum($"Destek ekibi ({sm.Agents.Count})");
                if (sm.Agents.Count == 0) y.Kucuk("Henüz destek personelin yok. Aşağıdaki başvurulardan birini işe al.");
                foreach (var a in sm.Agents)
                {
                    bool nobette = a.IsOnDuty(clock);
                    y.KartBasla();
                    y.Tablo(new[] { $"<b>{a.name}</b>", nobette ? "vardiyada" : "dışarıda" }, false, nobette ? UITheme.Good : U.Soluk,
                            new Color?[] { null, nobette ? UITheme.Good : U.Soluk });
                    y.Kucuk($"{a.ShiftText} · {a.DaysText} · {Ft(a.monthlySalary)}/ay");
                    y.Yazi($"Yetenek {YildizYazi(Stars(a.skill, 0.5f, 1f))} · çözdüğü {a.handledTotal} · kazandırdığı {Ft(a.savedTotal)}", "kucuk");
                    y.Yardim("Yetenekli personel şikayetleri daha ucuza kapatır (iade değerinin %30'u ile %60'ı arası).");
                    var agent = a;
                    y.Dugme("İşten çıkar", () => ui.AskConfirm("Destek personelini çıkar",
                        $"{agent.name} işten çıkarılsın mı? Bu ay çalıştığı günlerin maaşı ({Ft(SupportManager.ProratedSalary(agent))}) hemen ödenir. " +
                        "Vardiyası boş kalır.", "Evet, işten çıkar", () => sm.Fire(agent)), true, "kucuk-dg tehlike");
                    y.KartBitir();
                }

                Basvurular(sm, eco);
                Operasyon(eco);
                y.Bitir();
            }

            void Basvurular(SupportManager sm, EconomyManager eco)
            {
                y.Bolum($"Destek başvuruları ({sm.Candidates.Count})",
                        $"Destek personeli maaşlıdır ve sabit vardiyada çalışır. Her başvuru {SupportManager.CandidateValidDays} gün geçerli; " +
                        "reddedilen ya da süresi dolanın yerine ertesi gün yenisi gelir.");
                if (sm.Candidates.Count == 0) y.Kucuk("Şu an destek başvurusu yok; ertesi gün yenisi gelir.");
                foreach (var cand in sm.Candidates)
                {
                    var c = cand;
                    long sonra = ui.TotalPayroll + c.monthlySalary;
                    y.KartBasla();
                    y.Tablo(new[] { $"<b>{c.name}</b>", $"{Ft(c.monthlySalary)}/ay" });
                    if (!string.IsNullOrEmpty(c.note)) y.Not($"\"{c.note}\"");
                    y.Yazi($"Yetenek {YildizYazi(Stars(c.skill, 0.5f, 1f))} · işe alırsan aylık maaşlar {Ft(sonra)}",
                           sonra > eco.FreeCash ? "kucuk uyari-renk" : "kucuk");
                    y.Secim(VardiyaAdlari, (int)c.chosenShift, v => c.chosenShift = (SupportShift)v);
                    y.Secim(GunAdlari, c.chosenDays == WorkDays.PztCum ? 0 : 1, v => c.chosenDays = v == 0 ? WorkDays.PztCum : WorkDays.CarPaz);
                    y.Kucuk(ExpiryText(c.expiresAt));
                    y.SatirBasla();
                    y.Dugme($"İşe al ({SupportManager.ShiftLabel(c.chosenShift)}, {(c.chosenDays == WorkDays.PztCum ? "Pzt–Cum" : "Çar–Paz")})", () => ui.AskConfirm(
                        "Destek personelini işe al",
                        $"{c.name} {Ft(c.monthlySalary)}/ay maaşla, {SupportManager.ShiftLabel(c.chosenShift)} vardiyasında " +
                        $"{(c.chosenDays == WorkDays.PztCum ? "Pazartesi–Cuma" : "Çarşamba–Pazar")} çalışmak üzere işe alınsın mı? " +
                        $"Aylık toplam maaşın {Ft(ui.TotalPayroll + c.monthlySalary)} olur.",
                        "Evet, işe al", () => sm.Hire(c)), true, "vurgu");
                    y.Dugme("Reddet", () => sm.Reject(c), true, "kucuk-dg");
                    y.SatirBitir();
                    y.KartBitir();
                }
            }

            void Operasyon(EconomyManager eco)
            {
                var ops = OperationsManager.I;
                if (ops == null) return;
                y.Bolum("Operasyon sorumlusu",
                        "Maaşlı tek kişi. Her sabah 09:00'da eksik ve işine yarayacak ekipmanı kuryelere hatırlatır (kuryeler depozitoyla alır), " +
                        "yıpranan çantaları yeniler (bütçeden) ve gün içinde çantası kirlenen kuryeleri uyarır. Yetenekli sorumlu işleri ertelemez. " +
                        "Ne yaptığı gün sonu raporunda yazar. Az kuryen varken maaşına değmeyebilir.");
                if (ops.HasManager)
                {
                    var m = ops.Manager;
                    y.KartBasla();
                    y.Tablo(new[] { $"<b>{m.name}</b>", $"{Ft(m.monthlySalary)}/ay" });
                    y.KutularBasla();
                    y.Kutu("Yetenek", $"%{m.skill * 100f:0}");
                    y.Kutu("İkna", $"+%{ops.Persuasion * 100f:0}", "kabul ihtimali");
                    y.Kutu("Ertelemez", $"%{ops.Reliability * 100f:0}", ops.Discount > 0f ? "toplu alım −%10" : null);
                    y.KutularBitir();
                    y.Kucuk($"Bu ay birikmiş maaş {Ft(ops.AccruedThisMonth)} (ay başında ödenir)");
                    if (!string.IsNullOrEmpty(ops.lastPassSummary)) y.Kucuk("Son sabah turu: " + ops.lastPassSummary);
                    y.Dugme("İşten çıkar", () => ui.AskConfirm("Operasyon sorumlusunu çıkar",
                        $"{m.name} işten çıkarılsın mı? Bu ay çalıştığı günlerin maaşı ({Ft(ops.AccruedThisMonth)}) hemen ödenir; " +
                        "eksik ekipman teklifleri ve çanta işleri yine sana kalır.",
                        "Evet, işten çıkar", () => ops.Fire()), true, "kucuk-dg tehlike");
                    y.KartBitir();
                }
                else
                {
                    if (ops.Candidates.Count == 0) y.Kucuk("Şu an başvuru yok; yenisi gelecek.");
                    foreach (var cand in ops.Candidates)
                    {
                        var c = cand;
                        long sonra = ui.TotalPayroll + c.monthlySalary;
                        y.KartBasla();
                        y.Tablo(new[] { $"<b>{c.name}</b>", $"{Ft(c.monthlySalary)}/ay" });
                        if (!string.IsNullOrEmpty(c.note)) y.Not($"\"{c.note}\"");
                        y.Cubuk("Yetenek", c.skill, new Color(0.4f, 0.75f, 1f), $"%{c.skill * 100f:0}");
                        y.Yazi($"İşe alırsan aylık maaşlar {Ft(sonra)} · serbest kasan {Ft(eco.FreeCash)}", sonra > eco.FreeCash ? "kucuk uyari-renk" : "kucuk soluk");
                        y.Kucuk(ExpiryText(c.expiresAt));
                        y.SatirBasla();
                        y.Dugme("İşe al", () => ui.AskConfirm("Operasyon sorumlusunu işe al",
                            $"{c.name} {Ft(c.monthlySalary)}/ay maaşla işe alınsın mı? Maaş ay başında, çalıştığı gün kadar ödenir. " +
                            $"Aylık toplam maaşın {Ft(ui.TotalPayroll + c.monthlySalary)} olur.",
                            "Evet, işe al", () => ops.Hire(c)), true, "vurgu");
                        y.Dugme("Reddet", () => ops.Reject(c), true, "kucuk-dg");
                        y.SatirBitir();
                        y.KartBitir();
                    }
                }

                // Kurallar (işe almadan önce de ayarlanabilir)
                y.Bolum("Kurallar", "Giyecekleri sadece mevsimi gelince ya da yaklaşınca sorar (ör. montu sonbahar ve kışın); arabalı kuryelere giyecek ve kask sormaz.");
                y.Kucuk("Hangi ekipmanları teklif etsin:");
                ekipmanAdlari.Clear();
                foreach (var e in GameData.Equipment) ekipmanAdlari.Add(e.name);
                y.CokluSecim(ekipmanAdlari, i => ops.offerKinds[(int)GameData.Equipment[i].kind],
                             i => { int ki = (int)GameData.Equipment[i].kind; ops.offerKinds[ki] = !ops.offerKinds[ki]; });
                y.Onay($"Yıpranan çantayı yenile ({Ft(GameData.Equip(EquipmentKind.TermalCanta).price)}, şirket öder)", ops.replaceWornBags, v => ops.replaceWornBags = v);
                if (Sade.Hijyen) y.Onay("Çantası kirlenen kuryeyi uyar (ücretsiz, moral biraz düşer)", ops.warnDirtyBags, v => ops.warnDirtyBags = v);
                y.Kucuk("Yeni kuryeye ekipman ne zaman sorulsun:");
                y.Secim(SormaAdlari, System.Array.IndexOf(SormaDegerleri, ops.minDeliveries), v => ops.minDeliveries = SormaDegerleri[v], true);
                y.Kaydirici("Aylık bütçe", ops.monthlyBudget, 0, 300000, Ft(ops.monthlyBudget), v => ops.monthlyBudget = Mathf.RoundToInt(v / 5000f) * 5000);
                y.Yazi($"Bu ay çanta yenilemeye harcanan: {Ft(ops.spentThisMonth)} / {Ft(ops.monthlyBudget)}", ops.BudgetLeft == 0 ? "kucuk uyari-renk" : "kucuk soluk");
            }
        }
    }
}
