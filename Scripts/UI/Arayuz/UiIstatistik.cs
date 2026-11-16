using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// İstatistikler (Grafik) penceresi (UI Toolkit; eski IMGUI DrawStatistics yerine). Üstte (kaymaz) Günlük · Haftalık · Aylık.
    /// Dönemin özeti sayı kutularında (net kâr ve günlük ortalaması, teslimat, iptal + kaçan oranı, en iyi dönem);
    /// altında sekiz grafik (Painter2D): net kâr, gelir, gider, teslimat, iptal + kaçan (çubuk), itibar, kurye, kasa (çizgi).
    /// Üstüne gelince o dönemin değeri grafiğin başlığında yazar. Veriler StatsHistory'den, gruplama BuildStats ile.
    /// </summary>
    public partial class GameUI
    {
        class UiIstatistik : UiSekme
        {
            Yazici y;
            U.Secenekler donem;
            int sonGunSayisi = -1, sonDonem = -1;

            protected override void Kur()
            {
                donem = U.SecenekEkle(Ust(), new[] { "Günlük", "Haftalık", "Aylık" }, () => ui.statPeriod, v => { ui.statPeriod = v; Yenile(); });
                donem.kok.style.marginTop = 2;
                donem.kok.style.marginBottom = 8;
                y = new Yazici(ui, kok);
            }

            public override void Acildi() { sonGunSayisi = -1; }

            public override void Yenile()
            {
                var h = StatsHistory.I;
                if (h == null) return;
                donem.Yenile();
                // Veriler gece bir kez değişir: gün sayısı ya da dönem değişmediyse yeniden çizmeye gerek yok
                if (h.Days.Count == sonGunSayisi && ui.statPeriod == sonDonem) return;
                sonGunSayisi = h.Days.Count;
                sonDonem = ui.statPeriod;
                y.Basla();
                y.Yazi("Şirketinin gidişatı: kâr, teslimat, iptal, itibar, kurye ve kasa.");
                y.Yardim("Veriler her gece gün sonunda kaydedilir. Haftalık ve aylık görünümde değerler toplanır (itibar, kurye ve kasa dönem sonundaki değerdir). " +
                         "Grafiğin üstüne gelince o dönemin değeri başlıkta yazar.");
                if (h.Days.Count == 0)
                {
                    y.Kucuk("Grafikler ilk gün sonundan sonra görünür.");
                    y.Bitir();
                    return;
                }

                var satirlar = ui.BuildStats(h.Days, ui.statPeriod);
                string birim = ui.statPeriod == 0 ? "gün" : ui.statPeriod == 1 ? "hafta" : "ay";
                long net = 0, gelir = 0, gider = 0;
                int teslimat = 0, kayip = 0, gunler = 0;
                StatRow enIyi = null;
                foreach (var r in satirlar)
                {
                    net += r.net; gelir += r.income; gider += r.expenses;
                    teslimat += r.deliveries; kayip += r.cancelled + r.lost; gunler += r.days;
                    if (enIyi == null || r.net > enIyi.net) enIyi = r;
                }
                float kayipOrani = teslimat + kayip > 0 ? kayip / (float)(teslimat + kayip) : 0f;
                y.KutularBasla();
                y.Kutu($"Net kâr · son {satirlar.Count} {birim}", Ft(net), $"günde {Ft(gunler > 0 ? net / gunler : 0)}", MoneyColor(net),
                       $"Gelir {Ft(gelir)} · gider {Ft(gider)} ({gunler} gün)");
                y.Kutu("Teslimat", teslimat.ToString(), $"günde ~{(gunler > 0 ? teslimat / (float)gunler : 0f):0.#}");
                y.Kutu("İptal + kaçan", $"%{kayipOrani * 100f:0.#}", $"{kayip} sipariş", kayipOrani > 0.1f ? U.Kotu : kayipOrani > 0.05f ? U.Uyari : (Color?)null);
                if (enIyi != null) y.Kutu($"En iyi {birim}", Ft(enIyi.net), enIyi.label, MoneyColor(enIyi.net));
                y.KutularBitir();

                var etiket = satirlar.ConvertAll(r => r.label);
                System.Func<float, string> para = v => Ft((long)v);
                System.Func<float, string> sayi = v => v.ToString("0");
                Cubuk("net", "Net kâr", satirlar.ConvertAll(r => (float)r.net), etiket, U.Iyi, para);
                Cubuk("gelir", "Gelir", satirlar.ConvertAll(r => (float)r.income), etiket, new Color(0.29f, 0.53f, 0.74f), para);   // kâğıt tema: suluboya tonları
                Cubuk("gider", "Gider", satirlar.ConvertAll(r => (float)r.expenses), etiket, new Color(0.80f, 0.42f, 0.24f), para);
                Cubuk("teslimat", "Teslimat", satirlar.ConvertAll(r => (float)r.deliveries), etiket, new Color(0.29f, 0.53f, 0.74f), sayi);
                Cubuk("kayip", "İptal + kaçan sipariş", satirlar.ConvertAll(r => (float)(r.cancelled + r.lost)), etiket, U.Kotu, sayi);
                Cizgi("itibar", "İtibar (dönem sonu)", satirlar.ConvertAll(r => r.reputation), etiket, new Color(0.80f, 0.60f, 0.12f), v => v.ToString("0.00"), 1f, 5f);
                Cizgi("kurye", "Kurye sayısı", satirlar.ConvertAll(r => (float)r.couriers), etiket, new Color(0.50f, 0.40f, 0.75f), sayi);
                Cizgi("kasa", "Kasa (dönem sonu)", satirlar.ConvertAll(r => (float)r.cash), etiket, new Color(0.24f, 0.58f, 0.50f), para);
                y.Bitir();
            }

            void Cubuk(string anahtar, string ad, List<float> degerler, List<string> etiketler, Color renk, System.Func<float, string> bicim) =>
                y.Ozel(anahtar, () => new GrafikKutu()).Ayarla(ad, degerler, etiketler, true, renk, U.Kotu, bicim);

            void Cizgi(string anahtar, string ad, List<float> degerler, List<string> etiketler, Color renk, System.Func<float, string> bicim,
                       float min = 0f, float max = -1f) =>
                y.Ozel(anahtar, () => new GrafikKutu()).Ayarla(ad, degerler, etiketler, false, renk, renk, bicim, min, max);
        }
    }
}
