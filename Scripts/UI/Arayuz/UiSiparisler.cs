using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Siparişler penceresi (UI Toolkit). Her bekleyen sipariş tek satır: kalan süre · dükkan ve kısa bilgi · gelir · tek "Ata" düğmesi.
    /// Satıra tıklayınca ayrıntı açılır (adres, ürünler, ücret dökümü, bütün uygun kuryeler).
    /// Altında yoldaki kuryeler ve son teslimatlar, yine tek satır.
    /// </summary>
    public partial class GameUI
    {
        class UiSiparisler : UiSekme
        {
            U.Onay otoAta, karOncele;
            Label basitNot, yogunluk, kayipOzet, bekleyenBaslik, bekleyenBos, yoldaBaslik, robotNot, sonBaslik;
            VisualElement bekleyenKap, yoldaKap, sonKap;
            U.Liste<int, Order, Satir> bekleyen;
            U.Liste<Courier, Courier, YoldaSatir> yolda;
            U.Liste<int, Order, SonSatir> son;
            readonly HashSet<int> acik = new HashSet<int>();
            const int GosterMax = 20;

            class Satir
            {
                public VisualElement kok, satir, ayrinti, kuryeKap;
                public Label sure, ad, alt, tutar, ataYok, adres, bina, urunler, notu, ucret, nakit, reddedenler;
                public Button ata;
                public Order o;
                public Courier enIyi;
                public U.Liste<Courier, Courier, KuryeSecim> kuryeler;
            }

            class KuryeSecim
            {
                public VisualElement kok, simge;
                public Label yazi;
                public Button ata;
                public Courier c;
            }

            class YoldaSatir { public VisualElement kok, simge; public Label yazi; }
            class SonSatir { public VisualElement kok; public Label yazi; }

            protected override void Kur()
            {
                var om = OrderManager.I;
                otoAta = U.OnayEkle(kok, "Otomatik atama (dispatcher)", () => OrderManager.I.autoAssign, v => OrderManager.I.autoAssign = v);
                karOncele = U.OnayEkle(kok, "Atamada kârı öncele (kapalıysa en hızlı kurye)", () => OrderManager.I.prioritizeProfit,
                                       v => OrderManager.I.prioritizeProfit = v);
                basitNot = U.Yazi(kok, "Basit atama: en yakın kuryeye teklif eder. Akıllı atama Gelişim menüsünde.", "kucuk", "soluk");
                yogunluk = U.Yazi(kok, "YOĞUNLUK: bekleyen sipariş boştaki kuryeden fazla, bazı müşteriler sipariş vermiyor. Çözüm: daha çok kurye veya boost.",
                                  "kucuk", "uyari-renk");
                yogunluk.style.marginTop = 4;
                kayipOzet = U.Yazi(kok, "", "kucuk", "soluk");
                kayipOzet.style.marginTop = 4;
                ui.Ipucu(kayipOzet, () =>
                {
                    var o = OrderManager.I;
                    return $"Yoğunluk yüzünden sipariş vermeyen {o.busyLostToday} · vazgeçen müşteri (ücret pahalı) {o.lostToday} · " +
                           $"iptal {o.cancelledToday} (kuryeler reddetti {o.cancelledRejectedToday}, müşteri gecikmeden vazgeçti {o.cancelledDelayToday}) · " +
                           $"dükkan iptal etti {o.cancelledShopToday} · müşteri fikrini değiştirdi {o.cancelledCustomerToday} · " +
                           $"taşıyacak kurye olmadığı için alınamayan {o.heavyLostToday} · kuryelerin reddettiği teklif {o.rejectedToday}.\n" +
                           "Yoğunlukta uygulama müşterilere uzun teslimat uyarısı gösterir; itibar düşmez ama sipariş kaybolur.\n" +
                           $"Ağırlık: 20 kg'a kadar bisiklet, scooter, motor ve araba; {GameData.HeavyKg:0} kg ve üstü sadece araba.";
                });

                bekleyenBaslik = U.Bolum(kok, "BEKLEYEN");
                bekleyenBos = U.Yazi(kok, "Bekleyen sipariş yok.", "kucuk", "soluk");
                bekleyenKap = U.El(kok, "kolon");
                bekleyen = new U.Liste<int, Order, Satir>(bekleyenKap, o => o.id, SatirKur, s => s.kok, SatirYenile);

                yoldaBaslik = U.Bolum(kok, "YOLDA");
                robotNot = U.Yazi(kok, "", "kucuk", "soluk");
                yoldaKap = U.El(kok, "kolon");
                yolda = new U.Liste<Courier, Courier, YoldaSatir>(yoldaKap, c => c, c =>
                {
                    var y = new YoldaSatir();
                    y.kok = U.Gecirgen(null, "satir");
                    y.kok.style.minHeight = 22;
                    y.simge = U.Simge(y.kok, Icons.Of(c.vehicle), UITheme.TextDim, 14f, "mini-simge");
                    y.yazi = U.Yazi(y.kok, "", "kucuk", "tek-satir", "esnek");
                    return y;
                }, y => y.kok, (y, c) =>
                {
                    U.SimgeAyarla(y.simge, Icons.Of(c.vehicle), UITheme.TextDim);
                    string ids = c.CurrentOrder != null ? "#" + c.CurrentOrder.id : "";
                    foreach (var q in c.QueuedOrders) ids += " #" + q.id;
                    U.Metin(y.yazi, $"{c.ShortName} · {c.OrderCount}/{c.EffectiveCapacity} paket · {ShortState(c)} · {ids}");
                });

                sonBaslik = U.Bolum(kok, "SON TESLİMATLAR");
                sonKap = U.El(kok, "kolon");
                son = new U.Liste<int, Order, SonSatir>(sonKap, o => o.id, o =>
                {
                    var s = new SonSatir();
                    s.kok = U.Gecirgen(null, "satir");
                    s.kok.style.minHeight = 20;
                    s.yazi = U.Yazi(s.kok, "", "kucuk", "soluk", "tek-satir", "esnek");
                    return s;
                }, s => s.kok, (s, o) =>
                {
                    string kim = o.courier != null ? o.courier.ShortName : o.robot ? "robot" : "-";
                    string ek = o.keptByCourier ? " · müşteriye ulaşılamadı" : o.deliveredCold ? " · soğuk" : o.dolap ? " · dolaba" : o.batched ? " · toplu" : "";
                    string yildiz = o.keptByCourier ? "puan yok" : $"★{o.rating:0.0}";
                    U.Metin(s.yazi, $"#{o.id} {o.shop.Name} → {kim} · {GameData.KmText(o.distance)} · {yildiz} · kâr {Ft(o.Profit)}{ek}");
                    U.Renk(s.yazi, o.keptByCourier ? UITheme.Warn : UITheme.TextDim);
                });
            }

            Satir SatirKur(Order o)
            {
                var s = new Satir { o = o };
                s.kok = U.El(null, "kolon");
                s.satir = U.Tikla(U.El(s.kok, "satir-kart"), () => { if (!acik.Remove(s.o.id)) acik.Add(s.o.id); SatirYenile(s, s.o); });
                s.sure = U.Kalin(s.satir, "", "sure");
                var ana = U.Gecirgen(s.satir, "siparis-ana");
                s.ad = U.Kalin(ana, "", "siparis-ad");
                s.alt = U.Yazi(ana, "", "siparis-alt");
                s.tutar = U.Kalin(s.satir, "", "tutar");
                s.ata = U.Dugme(s.satir, "Ata", () =>
                {
                    if (s.enIyi != null) { OrderManager.I.Assign(s.o, s.enIyi, true); ui.UiSekmeyiYenile(); }
                }, "kucuk-dg");
                s.ataYok = U.Yazi(s.satir, "", "minik", "soluk");
                s.ataYok.style.marginLeft = 8;
                s.ataYok.style.maxWidth = 120;

                s.ayrinti = U.El(s.kok, "ayrinti");
                s.adres = U.Yazi(s.ayrinti, "", "kucuk");
                s.bina = U.Yazi(s.ayrinti, "", "kucuk", "soluk");
                s.urunler = U.Yazi(s.ayrinti, "", "kucuk", "soluk");
                s.notu = U.Yazi(s.ayrinti, "", "kucuk");
                s.notu.style.unityFontStyleAndWeight = FontStyle.Italic;
                s.nakit = U.Yazi(s.ayrinti, "", "kucuk", "uyari-renk");
                s.reddedenler = U.Yazi(s.ayrinti, "", "kucuk", "uyari-renk");
                s.ucret = U.Yazi(s.ayrinti, "", "minik", "soluk");
                s.ucret.style.marginTop = 4;
                U.Bolum(s.ayrinti, "UYGUN KURYELER");
                s.kuryeKap = U.El(s.ayrinti, "kolon");
                s.kuryeler = new U.Liste<Courier, Courier, KuryeSecim>(s.kuryeKap, c => c, c => KuryeSecimKur(s, c), k => k.kok,
                    (k, c) => KuryeSecimYenile(s, k));
                return s;
            }

            KuryeSecim KuryeSecimKur(Satir s, Courier c)
            {
                var k = new KuryeSecim { c = c };
                k.kok = U.Gecirgen(null, "satir");
                k.kok.style.minHeight = 28;
                k.simge = U.Simge(k.kok, Icons.Of(c.vehicle), UITheme.Text, 14f, "mini-simge");
                k.yazi = U.Yazi(k.kok, "", "kucuk", "tek-satir", "esnek");
                k.ata = U.Dugme(k.kok, "Ata", () => { OrderManager.I.Assign(s.o, k.c, true); ui.UiSekmeyiYenile(); }, "kucuk-dg");
                return k;
            }

            void KuryeSecimYenile(Satir s, KuryeSecim k)
            {
                var eco = EconomyManager.I;
                var o = s.o;
                var c = k.c;
                int need = eco.RequiredAssignProfit(o);
                bool rejected = o.rejectedBy.Contains(c);
                int p = eco.ProfitIf(o, c);
                int pay = eco.CourierPay(c, o.distance);
                int courierGets = pay + CampaignManager.I.BoostAmount(pay);
                string mood = c.LikesOffer(o, courierGets) ? "" : " · beğenmeyebilir";
                string txt = rejected ? $"{c.ShortName} · reddetti"
                           : p >= need ? $"{c.ShortName} · kurye {Ft(courierGets)} · kâr +{Ft(p)}{mood}"
                           : p >= 0 ? $"{c.ShortName} · kâr az ({Ft(p)})"
                           : $"{c.ShortName} · zarar {Ft(p)}";
                U.Metin(k.yazi, txt);
                U.Renk(k.yazi, p >= need && !rejected ? UITheme.Text : UITheme.TextDim);
                k.ata.SetEnabled(p >= need && !rejected);
            }

            static bool Uygun(Order o, Courier c) =>
                c.CanTakeMore && c.CanCarry(o) && (!o.cashPay || c.CanTakeCash(o.CashAmount));

            void SatirYenile(Satir s, Order o)
            {
                s.o = o;
                var om = OrderManager.I;
                var eco = EconomyManager.I;
                float now = GameClock.I.TotalMinutes;
                float kalan = Mathf.Max(0f, o.createdAt + om.pendingTimeout - now);
                U.Metin(s.sure, $"{kalan:0} dk");
                U.CipRengi(s.sure, kalan <= 5f ? UITheme.Bad : kalan <= 10f ? UITheme.Warn : U.Notr);
                U.Metin(s.ad, $"#{o.id} {o.shop.Name}");
                var bilgi = new List<string> { GameData.KmText(o.distance), GameData.KgText(o.weightKg), o.CustomerTypeName };
                if (o.IsHeavy) bilgi.Add("ağır: araba");
                if (o.cashPay) bilgi.Add("nakit");
                if (o.dolap) bilgi.Add("dolaba");
                if (o.UsesStairs && o.floor >= 4) bilgi.Add($"{o.floor}. kat merdiven");
                if (o.priceDeclines > 0) bilgi.Add($"{o.priceDeclines} red");
                U.Metin(s.alt, string.Join(" · ", bilgi));
                U.Metin(s.tutar, Ft(o.CompanyIncome));

                // En kârlı uygun kurye tek düğmede
                Courier best = null;
                int bestP = int.MinValue, eligible = 0;
                int need = eco.RequiredAssignProfit(o);
                foreach (var c in CourierManager.I.Couriers)
                {
                    if (!Uygun(o, c)) continue;
                    eligible++;
                    if (o.rejectedBy.Contains(c)) continue;
                    int p = eco.ProfitIf(o, c);
                    if (p >= need && p > bestP) { bestP = p; best = c; }
                }
                s.enIyi = best;
                U.Goster(s.ata, best != null);
                U.Goster(s.ataYok, best == null);
                if (best != null) U.Metin(s.ata, $"Ata: {best.ShortName}");
                else U.Metin(s.ataYok, eligible == 0 ? (o.IsHeavy ? "Arabalı kurye yok" : "Müsait kurye yok") : "Kâr eden yok");

                bool ac = acik.Contains(o.id);
                U.Sinif(s.satir, "acik", ac);
                U.Goster(s.ayrinti, ac);
                if (!ac) return;
                U.Metin(s.adres, $"{o.customerName} — {o.AddressText}");
                U.Metin(s.bina, o.BuildingText);
                U.Metin(s.urunler, $"Sipariş ({o.ItemCount} ürün): {o.ItemsText}");
                U.Goster(s.notu, !string.IsNullOrEmpty(o.customerNote));
                U.Metin(s.notu, $"Not: \"{o.customerNote}\"");
                U.Goster(s.nakit, o.cashPay);
                if (o.cashPay) U.Metin(s.nakit, $"Nakit: müşteri {Ft(o.cashGiven)} verecek, tutar {Ft(o.CashAmount)}, para üstü {Ft(o.CashChange)}.");
                bool red = o.priceDeclines > 0 || o.shopQueue;
                U.Goster(s.reddedenler, red);
                if (red) U.Metin(s.reddedenler, o.shopQueue ? "Önceki kurye dükkandaki sıra yüzünden bıraktı."
                                                           : "Kuryeler kazancı beğenmedi: bonus çarpanı ya da boost kabul edilmesini kolaylaştırır.");
                U.Metin(s.ucret, $"Sepet {Ft(o.basket)} · komisyon {Ft(o.commissionIncome)} + teslimat ücreti {Ft(o.deliveryFee)} = gelir {Ft(o.CompanyIncome)}\n" +
                                 $"Teslimat ücreti: taban {Ft(o.feeBase)} + {GameData.KmText(o.distance)} × {Ft(eco.feePerKm)} = {Ft(o.feeKm)}" +
                                 (o.feeWeather > 0 ? $" + kötü hava {Ft(o.feeWeather)}" : "") +
                                 (o.feeExtra > 0 ? $" + ek {Ft(o.feeExtra)} (kurye ücretini karşılamak için)" : ""));
                var uygunlar = new List<Courier>();
                foreach (var c in CourierManager.I.Couriers) if (Uygun(o, c)) uygunlar.Add(c);
                uygunlar.Sort((a, b) => eco.ProfitIf(o, b).CompareTo(eco.ProfitIf(o, a)));
                s.kuryeler.Esitle(uygunlar);
            }

            public override void Yenile()
            {
                var om = OrderManager.I;
                if (om == null) return;
                otoAta.Yenile();
                bool akilli = UpgradeManager.I.SmartDispatch;
                U.Goster(karOncele.kok, akilli);
                if (akilli) karOncele.Yenile();
                U.Goster(basitNot, !akilli);
                U.Goster(yogunluk, om.IsBusy);
                int lostTotal = om.busyLostToday + om.lostToday + om.cancelledToday + om.heavyLostToday;
                U.Metin(kayipOzet, $"Bugün kaybedilen sipariş: {lostTotal} · toplu teslimat: {om.batchedToday}  (ayrıntı için üstüne gel)");

                U.Metin(bekleyenBaslik, $"BEKLEYEN ({om.Pending.Count})");
                U.Goster(bekleyenBos, om.Pending.Count == 0);
                var gosterilen = om.Pending.Count > GosterMax ? om.Pending.GetRange(0, GosterMax) : om.Pending;
                bekleyen.Esitle(gosterilen);
                acik.RemoveWhere(id => !om.Pending.Exists(p => p.id == id));

                int robotYolda = RobotFilo.I != null ? RobotFilo.I.Yolda : 0;
                U.Metin(yoldaBaslik, robotYolda > 0 ? $"YOLDA ({om.Active.Count} + {robotYolda} ROBOTTA)" : $"YOLDA ({om.Active.Count})");
                bool robot = RobotFilo.I != null && RobotFilo.I.Robotlar.Count > 0;
                U.Goster(robotNot, robot);
                if (robot) U.Metin(robotNot, RobotFilo.I.DurumMetni());
                var yoldakiler = new List<Courier>();
                foreach (var c in CourierManager.I.Couriers) if (c.OrderCount > 0) yoldakiler.Add(c);
                yolda.Esitle(yoldakiler);

                var sonlar = om.History.Count > 8 ? om.History.GetRange(0, 8) : om.History;
                U.Goster(sonBaslik.parent, sonlar.Count > 0);
                son.Esitle(sonlar);
            }
        }

        /// <summary>Bir işlemden sonra açık sekmeyi hemen yeniler (tıklama sonucu beklemeden görünsün).</summary>
        void UiSekmeyiYenile()
        {
            if (gosterilenSekme.HasValue && uiSekmeler.TryGetValue(gosterilenSekme.Value, out var s)) s.Yenile();
        }
    }
}
