using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Gelişim (geliştirmeler) ve Hedefler pencereleri (UI Toolkit).
    /// Geliştirme kartı: ad ve seviye noktaları · şu anki etkisi · sonraki seviye ve ek günlük gider · tek "Yükselt — bedel" düğmesi.
    /// Kilitliyse nedeni turuncu tek satır. Uzun açıklama "?" yerine kartın başlığına gelince.
    /// </summary>
    public partial class GameUI
    {
        class UiGelisim : UiSekme
        {
            Label kasa, fiyat, hesaplar, gider;
            readonly List<Kart> kartlar = new List<Kart>();

            class Kart
            {
                public UpgradeDef d;
                public VisualElement kok, seviyeler;
                public Label ad, seviyeAd, simdi, sonraki, kilit;
                public Button yukselt, kucult;
            }

            protected override void Kur()
            {
                var bilgi = U.El(kok, "bilgi-kutusu");
                var s1 = U.El(bilgi, "satir");
                kasa = U.Kalin(U.El(s1, "kolon", "esnek"), "", "buyuk");
                U.Yazi(kasa.parent, "Harcanabilir para", "minik", "soluk");
                gider = U.Kalin(U.El(s1, "kolon", "esnek"), "", "buyuk");
                U.Yazi(gider.parent, "Günlük toplam gider", "minik", "soluk");
                hesaplar = U.Kalin(U.El(s1, "kolon", "esnek"), "", "buyuk");
                U.Yazi(hesaplar.parent, "Kurye hesapları", "minik", "soluk");
                fiyat = U.Yazi(bilgi, "", "minik", "soluk");
                fiyat.style.marginTop = 6;
                ui.Ipucu(bilgi, "Her geliştirmenin kurulum bedeli ve günlük gideri var. Sadece gerçekten gerektiğinde yükselt. " +
                                "Büyük platform paketleri şehir büyüdükçe, tesisler ise merkez ofis büyüdükçe açılır. İşletme gideri olanları küçültebilirsin " +
                                "(kurulum bedeli geri gelmez). Fiyatlar şirketin büyüklüğüne göre değişir: son iki haftanın günlük geliri arttıkça kurulum " +
                                "bedelleri artar, günlük giderler daha yavaş artar. Erken yatırım ucuzdur.");

                foreach (var d in UpgradeManager.Defs)
                {
                    var k = new Kart { d = d };
                    var kind = d.kind;
                    k.kok = U.El(kok, "kart");
                    var ust = U.El(k.kok, "satir");
                    k.ad = U.Kalin(ust, d.name, "buyuk", "esnek", "tek-satir");
                    ui.Ipucu(k.ad, () => d.summary);
                    k.ad.pickingMode = PickingMode.Position;
                    k.seviyeler = U.Seviyeler(ust, 0, d.MaxLevel, UITheme.Accent);
                    k.seviyeAd = U.Yazi(k.kok, "", "kucuk", "soluk");
                    k.seviyeAd.style.marginTop = 2;
                    k.simdi = U.Yazi(k.kok, "", "kucuk");
                    k.simdi.style.marginTop = 4;
                    k.sonraki = U.Yazi(k.kok, "", "minik", "soluk");
                    k.sonraki.style.marginTop = 3;
                    k.kilit = U.Yazi(k.kok, "", "kilit-yazi");
                    var dug = U.El(k.kok, "satir");
                    dug.style.marginTop = 8;
                    k.yukselt = U.Dugme(dug, "", () =>
                    {
                        var up = UpgradeManager.I;
                        int lv = up.Level(kind);
                        int cost = up.NextCost(kind);
                        if (cost < 0) return;
                        string what = $"{d.name}: {d.LevelText(lv + 1)}";
                        int daily = d.DailyAt(lv + 1);
                        ui.ConfirmIfExpensive(cost, "Geliştirme",
                            $"{what}. Kurulum {Ft(cost)}, sonra günlük {Ft(daily)} gider. Serbest kasan {Ft(EconomyManager.I.FreeCash)}. Kurulum bedeli geri gelmez.",
                            () => up.Buy(kind));
                    }, "ilk");
                    U.Gecirgen(dug, "bosluk");
                    k.kucult = U.Dugme(dug, "", () =>
                    {
                        var up = UpgradeManager.I;
                        int lv = up.Level(kind);
                        if (lv <= 0) return;
                        string what = $"{d.name}: {d.levelNames[lv]} → {d.levelNames[lv - 1]}";
                        ui.AskConfirm("Geliştirmeyi küçült",
                            $"{what}. Günlük gider azalır ama kurulum bedeli geri gelmez; tekrar yükseltmek için yeniden ödersin.",
                            "Evet, küçült", () => up.Downgrade(kind));
                    }, "kucuk-dg");
                    kartlar.Add(k);
                }
            }

            public override void Yenile()
            {
                var up = UpgradeManager.I;
                var e = EconomyManager.I;
                if (up == null || e == null) return;
                U.Metin(kasa, Ft(e.FreeCash));
                U.Metin(gider, Ft(up.TotalDailyCost));
                U.Metin(hesaplar, $"{CourierManager.I.Couriers.Count}/{up.CourierCap}");
                bool carpan = Denge.FiyatCarpani > 1f;
                U.Goster(fiyat, carpan);
                if (carpan) U.Metin(fiyat, $"Fiyat seviyesi ×{Denge.FiyatCarpani:0.00}: son 14 günün ortalama gelirine göre (şirket büyüdükçe tedarikçiler de fiyat artırır).");

                foreach (var k in kartlar)
                {
                    var d = k.d;
                    var kind = d.kind;
                    int lv = up.Level(kind);
                    bool gizli = !Sade.Hijyen && kind == UpgradeKind.Hijyen && lv == 0;
                    U.Goster(k.kok, !gizli);
                    if (gizli) continue;
                    U.SeviyeAyarla(k.seviyeler, lv, d.MaxLevel, UITheme.Accent);
                    U.Metin(k.seviyeAd, $"{d.levelNames[lv]} ({lv}/{d.MaxLevel})");
                    int dailyNow = up.DailyCost(kind);
                    U.Metin(k.simdi, d.effect(lv) + (dailyNow > 0 ? $" · günlük {Ft(dailyNow)}" : ""));

                    int cost = up.NextCost(kind);
                    bool maks = cost < 0;
                    U.Goster(k.sonraki, !maks);
                    string kilit = maks ? "" : up.LockReason(kind);
                    U.Goster(k.kilit, kilit.Length > 0);
                    if (!maks)
                    {
                        int dailyNext = d.DailyAt(lv + 1);
                        U.Metin(k.sonraki, $"Sonraki: {d.LevelText(lv + 1)} · günlük {Ft(dailyNext)}" +
                                           (dailyNext > dailyNow ? $" (+{Ft(dailyNext - dailyNow)}/gün)" : ""));
                        if (kilit.Length > 0) U.Metin(k.kilit, $"Kilitli: {kilit}. (Kurulum {Ft(cost)})");
                    }
                    bool alinir = !maks && !up.IsLocked(kind);
                    U.Goster(k.yukselt, alinir);
                    if (alinir)
                    {
                        U.Metin(k.yukselt, $"Yükselt — {Ft(cost)}");
                        k.yukselt.SetEnabled(e.FreeCash >= cost);
                        U.Zemin(k.yukselt, e.FreeCash >= cost ? UITheme.Accent : new Color(1f, 1f, 1f, 0.08f));
                        U.Renk(k.yukselt, e.FreeCash >= cost ? UITheme.AccentText : UITheme.Text);
                    }
                    bool kucultur = lv > 0 && d.canDowngrade && d.DailyAt(lv) > d.DailyAt(lv - 1);
                    U.Goster(k.kucult, kucultur);
                    if (kucultur)
                    {
                        string block = up.DowngradeBlock(kind);
                        k.kucult.SetEnabled(block.Length == 0);
                        U.Metin(k.kucult, block.Length == 0 ? $"Küçült (−{Ft(d.DailyAt(lv) - d.DailyAt(lv - 1))}/gün)" : $"Küçültülemez: {block}");
                    }
                }
            }
        }

        class UiHedefler : UiSekme
        {
            Label baslik, toplam;
            VisualElement kap;
            readonly List<(VisualElement kok, Label ad, Label odul, U.Cubuk cubuk, Label sayi)> satirlar =
                new List<(VisualElement, Label, Label, U.Cubuk, Label)>();

            protected override void Kur()
            {
                baslik = U.Bolum(kok, "");
                var aciklama = U.Yazi(kok, "Her pazartesi 6 yeni hedef gelir. Tamamladığın hedefin ödülü hemen kasaya girer. " +
                                           "Hedefler şirketinin büyüklüğüne göre seçilir; yarım kalan hedef için ceza yok.", "kucuk", "soluk");
                aciklama.style.marginBottom = 8;
                kap = U.El(kok, "kolon");
                toplam = U.Yazi(kok, "", "kucuk", "soluk");
                toplam.style.marginTop = 6;
            }

            public override void Yenile()
            {
                var gm = GoalManager.I;
                if (gm == null) return;
                U.Metin(baslik, $"BU HAFTANIN HEDEFLERİ · {gm.DaysLeft} GÜN KALDI");
                while (satirlar.Count < gm.Goals.Count)
                {
                    var kok = U.El(kap, "kart");
                    var ust = U.El(kok, "satir");
                    var ad = U.Kalin(ust, "", "esnek");
                    var sayi = U.Yazi(ust, "", "kucuk", "soluk");
                    var cb = U.CubukEkle(kok, 0f, UITheme.Accent);
                    cb.kok.style.marginTop = 6;
                    var odul = U.Yazi(kok, "", "minik", "soluk");
                    odul.style.marginTop = 5;
                    satirlar.Add((kok, ad, odul, cb, sayi));
                }
                for (int i = 0; i < satirlar.Count; i++)
                {
                    var (kok, ad, odul, cb, sayi) = satirlar[i];
                    bool vr = i < gm.Goals.Count;
                    U.Goster(kok, vr);
                    if (!vr) continue;
                    var g = gm.Goals[i];
                    U.Metin(ad, (g.done ? "✓ " : "") + g.Title);
                    U.Renk(ad, g.done ? UITheme.Good : UITheme.Text);
                    U.Metin(sayi, g.ProgressText);
                    cb.Ayarla(g.Progress01, g.done ? UITheme.Good : UITheme.Accent);
                    U.Metin(odul, $"Ödül: {Ft(g.reward)}" + (g.reputation > 0f ? " + itibar" : "") + (g.done ? " · alındı" : ""));
                }
                U.Metin(toplam, $"Bugüne kadar tamamlanan hedef: {gm.completedTotal}");
            }
        }
    }
}
