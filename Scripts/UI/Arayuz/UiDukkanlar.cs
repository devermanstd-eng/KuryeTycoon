using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Dükkanlar penceresi (UI Toolkit, Yazıcı ile). Üstte (kaymaz) dükkan filtresi; altta bölge açma, rakipler, krizler ve
    /// dükkan kartları. Her dükkan tek satır başlık (ad, durum, memnuniyet); tıklayınca ayrıntı ve sözleşme işlemleri açılır.
    /// İçerik IMGUI'deki DrawShops / DrawShopCard ile aynı.
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
                if (!map.IsUnlocked(s.tile)) { durum = "Kilitli"; renk = U.Soluk; }
                else if (s.exclusive) { durum = $"Münhasır · {s.satisfaction:0}/100"; renk = UITheme.Good; }
                else if (s.contracted) { durum = $"Sözleşmeli · {s.satisfaction:0}/100"; renk = s.satisfaction < 40f ? UITheme.Warn : UITheme.Good; }
                else if (GameClock.I.Day < s.resignBlockedUntil) { durum = "Ayrıldı"; renk = UITheme.Bad; }
                else { durum = $"Sözleşme yok · {Ft(s.def.contractCost)}"; renk = null; }
                y.AcilirBaslik(s.Name, durum, renk, ac, tik);
                string ek = s.rivalContracted ? $" · {(rv != null ? rv.DukkanRakipleri(s) : "rakip")} ile de çalışıyor" : "";
                y.Kucuk($"{ShopHours.StatusText(s.def)} · komisyon %{s.Commission * 100f:0.#} · toplam {s.ordersTotal} sipariş{ek}");
            }

            void Rakipler(RivalCompany rv)
            {
                y.KartBasla();
                y.Baslik($"Rakipler: {rv.Rakipler.Count} platform (yapay zekâ)");
                y.Yardim("Bir dükkanla birden çok platform çalışabilir: siparişler itibar, kurye sayısı ve teslimat hızına göre paylaşılır. " +
                         "Münhasır dükkanlarda hiçbir rakip yok. Sözleşmeler 6 ayda bir yenilenir; memnuniyet düşükse bir rakip araya girer. " +
                         "Moralsiz kuryelerini de ayartabilirler.");
                y.Kucuk($"Pazar payın: %{rv.MarketShare * 100f:0} (geçen hafta {(rv.ShareTrend >= 0f ? "+" : "−")}{Mathf.Abs(rv.ShareTrend) * 100f:0.0} puan) · " +
                        $"sen ort. {rv.myAvgMinutes:0} dk, %{rv.myLateRate * 100f:0} geç → müşteri gözünde ×{rv.MyService:0.00} · toplam talep ×{rv.ServiceDemandFactor:0.00}");
                y.Yardim("Daha hızlı ve dakik teslimat yaptıkça paylaşılan dükkanlarda müşteriler rakipten sana geçer ve şehirde sipariş " +
                         "vermek isteyen insan sayısı artar. 40 dk ortalama nötr; her dakika hızlı ≈ +%2, geç kalan siparişler düşürür. " +
                         "Kurye fazlan varken siparişler toplu taşınmaz, boştaki kuryeye gider.\n\n" +
                         "Pazar payın büyüdükçe (3 rakipte %35'ten sonra) rakipler saldırganlaşır: yatırım bulur, kurye alır, hizmetini iyileştirir; " +
                         "en iyi dükkanlarına ve kuryelerine teklif yapar, bir bölgede fiyat savaşı ya da reklam atağı başlatır. " +
                         "İtibarını yüksek tut, iyi dükkanlarla münhasırlık yap, kuryelerinin moralini koru.");
                for (int ri = 0; ri < rv.Rakipler.Count; ri++)
                {
                    var r = rv.Rakipler[ri];
                    y.Noktali($"{r.ad} · {r.Tanim}", r.renk);
                    y.Kucuk($"pay %{rv.PayiOf(r) * 100f:0} · itibar {r.reputation:0.0}/5{(r.marketingDays > 0 ? $" (reklamla {r.EffectiveReputation:0.0})" : "")} · " +
                            $"{r.couriers} kurye · {rv.ShopCountOf(ri)} dükkan · ~{r.deliveriesLastWeek} teslimat · ~{r.avgMinutes:0} dk, %{r.lateRate * 100f:0} geç (×{r.Service:0.00}) · " +
                            $"tavrı: {r.MoodText}{(r.fundingRounds > 0 ? $" · yatırım turu {r.fundingRounds}" : "")}");
                    if (r.marketingDays > 0) y.Uyari($"Reklam atağı: {r.marketingDays} gün daha müşterilere daha iyi görünüyor.");
                }
                if (rv.shareHistory.Count > 1)
                {
                    var hs = new System.Text.StringBuilder("Son haftalar: ");
                    foreach (var v in rv.shareHistory) hs.Append($"%{v * 100f:0} ");
                    y.Kucuk(hs.ToString());
                }
                if (rv.priceWarDays > 0 && rv.warDistrict > 0)
                    y.Uyari($"Fiyat savaşı: {(rv.RakipAt(rv.warOwner) != null ? rv.RakipAt(rv.warOwner).ad : "rakip")} {CityMap.DistrictNames[rv.warDistrict]} bölgesinde {rv.priceWarDays} gün daha ücret kırıyor " +
                            $"(oradaki siparişlerin −%{(1f - rv.warFactor) * 100f:0}).");
                y.KartBitir();
            }
        }

        /// <summary>
        /// Dükkanın ayrıntısı ve sözleşme işlemleri (Yazıcı ile; IMGUI'deki DrawShopCard'ın başlıksız hali).
        /// Haritadaki bina kartı UI Toolkit'e taşınınca o da bunu kullanır.
        /// </summary>
        void UiDukkanAyrinti(Yazici y, Shop s, RivalCompany rv)
        {
            var map = CityMap.I;
            var clock = GameClock.I;
            bool acikMi = ShopHours.IsOpen(s.def);
            y.Yazi($"{ShopHours.StatusText(s.def)} · {ShopHours.HoursText(s.def)}", acikMi ? "kucuk not-renk" : "kucuk uyari-renk");
            y.Kucuk($"Komisyon %{s.Commission * 100f:0.#} · Hazırlık ~{s.def.prepMinutes:0} dk · " +
                    $"Sepet {Ft(s.def.basketMin)}–{Ft(s.def.basketMax)} · Ağırlık {GameData.KgText(s.def.kgMin)}–{GameData.KgText(s.def.kgMax)} · Toplam sipariş {s.ordersTotal}");
            if (s.def.kgMax >= GameData.HeavyKg)
                y.Uyari($"{GameData.HeavyKg:0} kg üstü ağır siparişler gelir: bunlar için arabalı kurye gerekir.");

            if (!map.IsUnlocked(s.tile))
            {
                y.Kucuk($"Kilitli: önce \"{CityMap.DistrictNames[s.def.unlockLevel]}\" bölgesini aç.");
                return;
            }
            if (!s.contracted)
            {
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
            Color sc = Color.Lerp(new Color(0.9f, 0.3f, 0.3f), new Color(0.3f, 0.85f, 0.4f), s.satisfaction / 100f);
            y.Cubuk("Memnuniyet", s.satisfaction / 100f, sc, $"{s.satisfaction:0}/100");
            y.Kucuk(RivalCompany.PerformanceText(s));
            y.Kucuk($"Sözleşme bitişi: {clock.DateOf(s.contractEndDay)} · bu ay {s.monthOrders} sipariş (geçen ay {s.lastMonthOrders}) · " +
                    (s.exclusive ? "münhasır: siparişler +%15" : s.rivalContracted ? $"{rv.DukkanRakipleri(s)} ile paylaşılıyor: müşterilerin ~%{rv.MyFraction(s) * 100f:0}'i seni seçiyor" : "rakip yok"));
            if (s.commissionBonus != 0f)
                y.Kucuk($"Pazarlık/anlaşma farkı: {(s.commissionBonus > 0 ? "+" : "−")}{Mathf.Abs(s.commissionBonus) * 100f:0.#} puan (temel %{s.def.commission * 100f:0.#})");
            y.SatirBasla();
            y.Dugme($"Pazarlık: komisyon +2 puan (şans %{rv.NegotiateChance(s) * 100f:0})", () => rv.Negotiate(shop), rv.CanNegotiate(s), "kucuk-dg");
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
