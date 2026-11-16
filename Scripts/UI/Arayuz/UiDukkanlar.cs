using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Dükkanlar penceresi (UI Toolkit, Yazıcı ile). Üstte (kaymaz) dükkan filtresi; altta bölge açma, rakipler, krizler ve
    /// dükkan kartları. Sade: rakipler sayı kutuları ve "sen + rakipler" tablosuyla (pay, itibar, kurye, dükkan yan yana);
    /// her dükkan tek satır başlık + kararın sayıları (bu ay sipariş ve farkı, komisyon, payın); tıklayınca ayrıntı açılır.
    /// Uzun açıklamalar "?" ve satır ipuçlarında.
    /// </summary>
    public partial class GameUI
    {
        class UiDukkanlar : UiSekme
        {
            Yazici y;
            U.Secenekler filtre;
            int filtreNo;   // 0 hepsi, 1 sözleşmeli, 2 sözleşme yok, 3 kilitli
            readonly HashSet<Shop> acik = new HashSet<Shop>();
            readonly List<Shop> liste = new List<Shop>();

            protected override void Kur()
            {
                var ust = Ust();
                filtre = U.SecenekEkle(ust, new[] { "Hepsi", "Sözleşmeli", "Sözleşme yok", "Kilitli" }, () => filtreNo, v => filtreNo = v);
                filtre.kok.style.marginTop = 2;
                filtre.kok.style.marginBottom = 8;
                y = new Yazici(ui, kok);
            }

            public override void Yenile()
            {
                var map = CityMap.I;
                var rv = RivalCompany.I;
                if (map == null || GameClock.I == null || EconomyManager.I == null) return;
                filtre.Yenile();
                y.Basla();

                y.Bolum($"Açık bölge: {map.UnlockLevel}/{map.MaxLevel} — {CityMap.DistrictNames[map.UnlockLevel]}");
                if (map.UnlockLevel < map.MaxLevel)
                {
                    int sonraki = map.UnlockLevel + 1;
                    long bedel = CityMap.BolgeBedeli(sonraki);
                    y.Dugme($"Yeni bölge aç: {CityMap.DistrictNames[sonraki]} — {Ft(bedel)}", () => ui.AskConfirm("Yeni bölge aç",
                        $"{CityMap.DistrictNames[sonraki]} {Ft(bedel)} karşılığında açılsın mı? " +
                        $"Serbest kasan: {Ft(EconomyManager.I.FreeCash)}. Bölge açmak geri alınamaz.",
                        $"Evet, aç ({Ft(bedel)})", () => map.TryUnlockNext()));
                }

                if (rv != null) Rakipler(rv);

                if (CrisisManager.I != null)
                {
                    y.KartBasla();
                    y.Baslik("Krizler ve kalıcı kararlar");
                    y.Yardim("Ara sıra kriz çıkar: kurye grevi, uygulama çökmesi, akaryakıt zammı, yazın sıcak dalgası, platform çalışanları yasası. " +
                             "Her birinde bir seçim yaparsın; sonucu günlerce ya da haftalarca sürer.");
                    y.Kucuk(CrisisManager.I.StatusText);
                    y.KartBitir();
                }

                // Dükkanlar: filtreye göre (kendi marketin Market penceresinde)
                liste.Clear();
                foreach (var s in map.Shops)
                {
                    if (s.own) continue;
                    bool kilitli = !map.IsUnlocked(s.tile);
                    bool uy = filtreNo == 0 || (filtreNo == 1 && s.contracted) || (filtreNo == 2 && !s.contracted && !kilitli) || (filtreNo == 3 && kilitli);
                    if (uy) liste.Add(s);
                }
                y.Bolum($"Dükkanlar ({liste.Count})");
                if (liste.Count == 0) y.Kucuk("Bu filtrede dükkan yok.");
                foreach (var s in liste)
                {
                    y.KartBasla();
                    bool ac = acik.Contains(s);
                    var dukkan = s;
                    DukkanBasligi(s, rv, ac, () => { if (!acik.Remove(dukkan)) acik.Add(dukkan); });
                    if (ac) ui.UiDukkanAyrinti(y, s, rv);
                    y.KartBitir();
                }
                y.Bitir();
            }

            void DukkanBasligi(Shop s, RivalCompany rv, bool ac, System.Action tik)
            {
                var map = CityMap.I;
                string durum;
                Color? renk;
                bool kilitli = !map.IsUnlocked(s.tile);
                if (kilitli) { durum = "Kilitli"; renk = U.Soluk; }
                else if (s.exclusive) { durum = $"Münhasır · {s.satisfaction:0}/100"; renk = U.Iyi; }
                else if (s.contracted) { durum = $"Sözleşmeli · {s.satisfaction:0}/100"; renk = s.satisfaction < 40f ? U.Uyari : U.Iyi; }
                else if (GameClock.I.Day < s.resignBlockedUntil) { durum = "Ayrıldı"; renk = U.Kotu; }
                else { durum = $"Sözleşme yok · {Ft(s.def.contractCost)}"; renk = null; }
                y.AcilirBaslik(s.Name, durum, renk, ac, tik);
                if (kilitli)
                    y.Kucuk($"{CityMap.DistrictNames[s.def.unlockLevel]} bölgesinde · komisyon %{s.def.commission * 100f:0.#}");
                else if (!s.contracted)
                    y.Kucuk($"Komisyon %{s.Commission * 100f:0.#} · sepet {Ft(s.def.basketMin)}–{Ft(s.def.basketMax)} · hazırlık ~{s.def.prepMinutes:0} dk");
                else
                {
                    int fark = s.monthOrders - s.lastMonthOrders;
                    string pay = s.exclusive ? "münhasır" : s.rivalContracted && rv != null ? $"payın %{rv.MyFraction(s) * 100f:0}" : "rakip yok";
                    y.Kucuk($"Bu ay {s.monthOrders} sipariş" + (s.lastMonthOrders > 0 ? $" ({(fark >= 0 ? "+" : "−")}{Mathf.Abs(fark)} geçen aya göre)" : "") +
                            $" · komisyon %{s.Commission * 100f:0.#} · {pay}");
                }
            }

            void Rakipler(RivalCompany rv)
            {
                int sev = rv.TehditSeviyesi;
                y.KartBasla();
                y.Yazi($"Rakipler · {RivalCompany.SeviyeAdlari[sev]}", "yz-baslik",
                       sev >= 2 ? U.Kotu : sev == 1 ? (Color?)new Color(1f, 0.62f, 0.2f) : null);
                y.Yardim("Bir dükkanla birden çok platform çalışabilir: siparişler itibar, kurye sayısı ve teslimat hızına göre paylaşılır. " +
                         "Münhasır dükkanlarda rakip yok. Sözleşmeler 6 ayda bir yenilenir; memnuniyet düşükse bir rakip araya girer. " +
                         "Moralsiz kuryelerini de ayartabilirler.\n\n" +
                         $"Rakiplerin sertliği pazar payına bağlı: %{rv.TehditEsigi1 * 100f:0}'ten sonra tetikte (hamleler sıklaşır, yatırım ararlar), " +
                         $"%{rv.TehditEsigi2 * 100f:0}'den sonra saldırıda (her hafta hamle, eğitimli kuryelerine %30 fazla teklif, münhasır dükkanlarına bile " +
                         "teklif, fiyat savaşı en çok sipariş aldığın bölgede). Hamleler birkaç gün önceden sağ üstte görünür.");

                y.KutularBasla();
                float trend = rv.ShareTrend * 100f;
                y.Kutu("Pazar payın", $"%{rv.MarketShare * 100f:0}", $"{(trend >= 0f ? "+" : "−")}{Mathf.Abs(trend):0.0} puan (hafta)",
                       sev >= 2 ? U.Kotu : (Color?)null,
                       $"Eşikler: %{rv.TehditEsigi1 * 100f:0} tetikte, %{rv.TehditEsigi2 * 100f:0} saldırıda.",
                       trend > 0.05f ? U.Iyi : trend < -0.05f ? U.Kotu : (Color?)null);
                y.Kutu("Hizmetin", $"×{rv.MyService:0.00}", $"{rv.myAvgMinutes:0} dk · %{rv.myLateRate * 100f:0} geç", null,
                       "Müşteri gözündeki hızın: 40 dk ortalama nötr, her dakika hızlı ≈ +%2, geç kalanlar düşürür. " +
                       "Paylaşılan dükkanlarda müşteriler daha hızlı olana geçer.");
                y.Kutu("Toplam talep", $"×{rv.ServiceDemandFactor:0.00}", "hizmete göre", null,
                       "Hızlı ve dakik teslimat yaptıkça şehirde sipariş vermek isteyen insan sayısı artar.");
                y.KutularBitir();

                // Sen ve rakipler yan yana: farklar tek bakışta
                y.Tablo(new[] { "Platform", "Pay", "İtibar", "Kurye", "Dükkan" }, true);
                int benimDukkan = 0;
                foreach (var d in CityMap.I.Shops) if (d.contracted && !d.own) benimDukkan++;
                y.Tablo(new[] { "Sen", $"%{rv.MarketShare * 100f:0}", $"{CompanyProfile.Reputation:0.0}",
                                CourierManager.I.Couriers.Count.ToString(), benimDukkan.ToString() }, false, UITheme.Accent);
                for (int ri = 0; ri < rv.Rakipler.Count; ri++)
                {
                    var r = rv.Rakipler[ri];
                    string itibar = r.marketingDays > 0 ? $"{r.EffectiveReputation:0.0}*" : $"{r.reputation:0.0}";
                    y.Tablo(new[] { r.ad, $"%{rv.PayiOf(r) * 100f:0}", itibar, r.couriers.ToString(), rv.ShopCountOf(ri).ToString() }, false, r.renk,
                            new Color?[] { null, null, r.marketingDays > 0 ? U.Uyari : (Color?)null, null, null },
                            $"{r.ad} · {r.Tanim} · tavrı: {r.MoodText}{(r.fundingRounds > 0 ? $" · yatırım turu {r.fundingRounds}" : "")}\n" +
                            $"~{r.deliveriesLastWeek} teslimat/hafta · ~{r.avgMinutes:0} dk, %{r.lateRate * 100f:0} geç (×{r.Service:0.00})" +
                            (r.marketingDays > 0 ? $"\nReklam atağı: {r.marketingDays} gün daha müşterilere daha iyi görünüyor (*)." : ""));
                }
                if (rv.shareHistory.Count > 1)
                {
                    var hs = new System.Text.StringBuilder("Payın son haftalar: ");
                    for (int i = 0; i < rv.shareHistory.Count; i++) hs.Append(i > 0 ? " → " : "").Append($"%{rv.shareHistory[i] * 100f:0}");
                    y.Bosluk(4);
                    y.Kucuk(hs.ToString());
                }
                if (rv.priceWarDays > 0 && rv.warDistrict > 0)
                    y.Uyari($"Fiyat savaşı: {(rv.RakipAt(rv.warOwner) != null ? rv.RakipAt(rv.warOwner).ad : "rakip")} {CityMap.DistrictNames[rv.warDistrict]} bölgesinde " +
                            $"{rv.priceWarDays} gün daha ücret kırıyor (oradaki siparişler −%{(1f - rv.warFactor) * 100f:0}).");
                y.KartBitir();
            }
        }

        /// <summary>
        /// Dükkanın ayrıntısı ve sözleşme işlemleri (Yazıcı ile; IMGUI'deki DrawShopCard'ın başlıksız hali).
        /// Önce kararın sayıları kutularda (sipariş ve farkı, komisyon, payın, sözleşmenin kalanı), sonra memnuniyet ve işlemler.
        /// Haritadaki bina kartı UI Toolkit'e taşınınca o da bunu kullanır.
        /// </summary>
        void UiDukkanAyrinti(Yazici y, Shop s, RivalCompany rv)
        {
            var map = CityMap.I;
            var clock = GameClock.I;
            bool acikMi = ShopHours.IsOpen(s.def);
            y.Yazi($"{ShopHours.StatusText(s.def)} · {ShopHours.HoursText(s.def)}", acikMi ? "kucuk not-renk" : "kucuk uyari-renk");

            if (!map.IsUnlocked(s.tile))
            {
                y.Kucuk($"Kilitli: önce \"{CityMap.DistrictNames[s.def.unlockLevel]}\" bölgesini aç.");
                return;
            }
            string agir = s.def.kgMax >= GameData.HeavyKg ? $"{GameData.HeavyKg:0} kg üstü ağır siparişler gelir: arabalı kurye gerekir." : null;
            if (!s.contracted)
            {
                y.KutularBasla();
                y.Kutu("Komisyon", $"%{s.Commission * 100f:0.#}", "sepetten");
                y.Kutu("Sepet", $"{Ft(s.def.basketMin)}–{Ft(s.def.basketMax)}", $"{GameData.KgText(s.def.kgMin)}–{GameData.KgText(s.def.kgMax)}");
                y.Kutu("Hazırlık", $"~{s.def.prepMinutes:0} dk", null);
                y.Kutu("Sözleşme", Ft(s.def.contractCost), "bir kez");
                y.KutularBitir();
                if (agir != null) y.Uyari(agir);
                var dukkan = s;
                if (clock.Day < s.resignBlockedUntil)
                    y.Uyari($"Seninle çalışmayı bıraktı; {clock.DateOf(s.resignBlockedUntil)} tarihinden sonra yeniden teklif edebilirsin.");
                else
                    y.Dugme($"Sözleşme imzala — {Ft(s.def.contractCost)}", () => ConfirmIfExpensive(dukkan.def.contractCost, "Sözleşme imzala",
                        $"{dukkan.def.name} ile {Ft(dukkan.def.contractCost)} karşılığında sözleşme imzalansın mı? " +
                        $"Serbest kasan: {Ft(EconomyManager.I.FreeCash)}.",
                        () => map.SignContract(dukkan)), true, "vurgu");
                return;
            }
            if (rv == null) return;

            var shop = s;
            int fark = s.monthOrders - s.lastMonthOrders;
            int kalan = s.contractEndDay - clock.Day;
            float bonus = s.commissionBonus * 100f;
            y.KutularBasla();
            y.Kutu("Bu ay", $"{s.monthOrders} sipariş", s.lastMonthOrders > 0 ? $"{(fark >= 0 ? "+" : "−")}{Mathf.Abs(fark)} (geçen ay {s.lastMonthOrders})" : $"toplam {s.ordersTotal}",
                   null, null, s.lastMonthOrders > 0 ? (fark >= 0 ? U.Iyi : U.Kotu) : (Color?)null);
            y.Kutu("Komisyon", $"%{s.Commission * 100f:0.#}", Mathf.Abs(bonus) > 0.01f ? $"temel %{s.def.commission * 100f:0.#} ({(bonus > 0 ? "+" : "−")}{Mathf.Abs(bonus):0.#})" : "temel oran",
                   null, "Pazarlıkla +2 puan, münhasırlıkla −3 puan değişir.", bonus > 0.01f ? U.Iyi : bonus < -0.01f ? U.Uyari : (Color?)null);
            y.Kutu("Senin payın", s.exclusive || !s.rivalContracted ? "%100" : $"%{rv.MyFraction(s) * 100f:0}",
                   s.exclusive ? "münhasır · +%15 sipariş" : s.rivalContracted ? rv.DukkanRakipleri(s) : "rakip yok", null,
                   s.rivalContracted && !s.exclusive ? "Dükkanın müşterilerinin ne kadarı seni seçiyor: itibar, kurye sayısı ve hıza göre paylaşılır. Münhasırlık bütün rakipleri çıkarır." : null);
            y.Kutu("Sözleşme", $"{Mathf.Max(0, kalan)} gün", clock.DateOf(s.contractEndDay), null, null, kalan <= 14 ? U.Uyari : (Color?)null);
            y.KutularBitir();

            Color sc = Color.Lerp(new Color(0.9f, 0.3f, 0.3f), new Color(0.3f, 0.85f, 0.4f), s.satisfaction / 100f);
            y.Cubuk("Memnuniyet", s.satisfaction / 100f, sc, $"{s.satisfaction:0}/100");
            y.Kucuk(RivalCompany.PerformanceText(s));
            y.Kucuk($"Hazırlık ~{s.def.prepMinutes:0} dk · sepet {Ft(s.def.basketMin)}–{Ft(s.def.basketMax)} · " +
                    $"{GameData.KgText(s.def.kgMin)}–{GameData.KgText(s.def.kgMax)} · toplam {s.ordersTotal} sipariş");
            if (agir != null) y.Uyari(agir);
            y.SatirBasla();
            y.Dugme($"Pazarlık: +2 puan (şans %{rv.NegotiateChance(s) * 100f:0})", () => rv.Negotiate(shop), rv.CanNegotiate(s), "kucuk-dg");
            y.Dugme(s.exclusive ? "Münhasır" : $"Münhasırlık öner: −3 puan (şans %{rv.ExclusiveChance(s) * 100f:0})", () => AskConfirm("Münhasırlık öner",
                    $"{shop.def.name} sadece seninle çalışsın diye teklif edilsin mi? Kabul ederse bütün rakipler çıkar, siparişler %15 artar, " +
                    "komisyonun 3 puan düşer ve sözleşme 6 ay uzar. Reddederse memnuniyeti biraz düşer ve 30 gün tekrar teklif edemezsin. " +
                    $"Kabul ihtimali yaklaşık %{rv.ExclusiveChance(shop) * 100f:0}.",
                    "Evet, teklif et", () => rv.OfferExclusive(shop)), rv.CanOfferExclusive(s), "kucuk-dg");
            y.SatirBitir();
            if (clock.Day < s.negotiateCooldownUntil)
                y.Kucuk($"Yeni teklif için {clock.DateOf(s.negotiateCooldownUntil)} tarihini bekle.");
        }
    }
}
