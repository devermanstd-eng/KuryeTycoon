using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Ekipman penceresi (UI Toolkit, Yazıcı ile). İçerik IMGUI'deki DrawEquipment ile aynı: mevsim uyarısı, depo ve depozito,
    /// çanta hijyeni, kurye seçimi ve eksikleri, her ekipman için kart (kimde var, kime yarar, hatırlat).
    /// </summary>
    public partial class GameUI
    {
        class UiEkipman : UiSekme
        {
            Yazici y;
            readonly List<string> kuryeAdlari = new List<string>();
            readonly List<string> depo = new List<string>();

            protected override void Kur() { y = new Yazici(ui, kok); }

            public override void Yenile()
            {
                var cm = CourierManager.I;
                var eco = EconomyManager.I;
                var clock = GameClock.I;
                if (cm == null || eco == null || clock == null) return;
                y.Basla();

                switch (clock.Season)
                {
                    case Season.Kis: y.Uyari("Kış: montsuz kuryeler yavaşlar, hastalanır, morali her gün düşer."); break;
                    case Season.Yaz: y.Uyari("Yaz: tişörtsüz kuryeler sıcakta yavaşlar, çabuk yorulur."); break;
                    case Season.Sonbahar: y.Uyari("Sonbahar: yağmur sezonu, kış yaklaşıyor. Yağmurluk ve mont zamanı."); break;
                    default: y.Uyari("İlkbahar: yağmur sezonu, yağmurluksuz kuryeler ıslanıp hastalanabilir."); break;
                }
                if (Sade.Depozito)
                {
                    y.Yazi("Kuryeler ekipmanı ihtiyaç duyunca kendileri ister; bedeli depozito olarak kazançlarından kesilir. Eksikleri Kuryeler penceresinde yazar.");
                    y.Yardim("Ekipman kısa vadede kârı düşürür ama gider olarak vergiden düşülür; hız, moral, puan ve daha az hastalık/kaza olarak geri döner. " +
                             "Kuryeler mevsime, yağmura ve dikkatlerine göre ister. Şirket depodan verir ya da depoda yoksa satın alır. " +
                             "Kurye ayrılırken ekipmanı iade ederse depozitosu geri ödenir ve ekipman depoya döner; iade etmezse depozito sende kalır. " +
                             $"Yeni kuryeler işe başlarken {GameData.StarterKitText} depozitoyla alır.");
                }
                else
                {
                    y.Yazi("Ekipmanı şirket verir: kuryeler ihtiyaç duyunca ister, depoda varsa oradan çıkar, yoksa satın alınır. Eksikleri Kuryeler penceresinde yazar.");
                    y.Yardim("Ekipman kısa vadede kârı düşürür ama gider olarak vergiden düşülür; hız, moral, puan ve daha az hastalık/kaza olarak geri döner. " +
                             "Kuryeler mevsime, yağmura ve dikkatlerine göre ister. Ekipman şirketin malıdır: kurye ayrılırken çoğu zaman iade eder ve ekipman depoya döner, " +
                             "sıradaki kuryeye bedavaya verilir; iade etmezse o ekipman kayıptır. " +
                             $"Yeni kuryeler işe başlarken {GameData.StarterKitText} alır.");
                }

                long borc = 0;
                foreach (var oc in cm.Couriers) borc += oc.depositOwed;
                depo.Clear();
                foreach (var se in GameData.Equipment) if (cm.gearStock[(int)se.kind] > 0) depo.Add($"{cm.gearStock[(int)se.kind]}× {se.name}");
                y.Bosluk(4);
                if (Sade.Depozito || eco.depositsHeld > 0) y.KV("Elindeki depozito (iade borcu)", Ft(eco.depositsHeld));
                if (Sade.Depozito) y.KV("Kuryelerden kesilecek", Ft(borc));
                y.KV("Depo", depo.Count > 0 ? string.Join(", ", depo) : "boş");
                y.Kucuk("\"Hatırlat\" ücretsizdir: kuryenin 2 gün boyunca o ekipmanı isteme ihtimali artar.");

                // Çanta
                int kirli = 0, yipranmis = 0;
                foreach (var hc in cm.Couriers) { if (hc.hygiene < Courier.HygieneLimit) kirli++; if (hc.BagWorn) yipranmis++; }
                if (Sade.Hijyen)
                {
                    y.Bolum("Çanta hijyeni",
                        "Çanta temizliği kuryenin kendi sorumluluğu: her gece temizler, titiz olanlar gün içinde de temizler. " +
                        "Kirli çanta müşteri şikayeti ve düşük puan getirir; gün içinde kirlenenleri uyarabilirsin (ücretsiz, moral biraz düşer). " +
                        $"{Courier.BagWornUses} teslimattan sonra çanta yıpranır, yenisi gerekir.");
                    y.Yazi($"{kirli} kuryenin çantası kirli · {yipranmis} çanta yıpranmış", kirli > 0 ? "uyari-renk" : null);
                    y.SatirBasla();
                    y.Dugme("Çantası kirli olanların hepsini uyar", () => cm.WarnAllDirty(), kirli > 0, "kucuk-dg");
                    y.SatirBitir();
                }
                else
                {
                    y.Bolum("Çanta yıpranması", $"{Courier.BagWornUses} teslimattan sonra termal çanta yıpranır; yenisi Kuryeler penceresinden alınır.");
                    y.Yazi(yipranmis > 0 ? $"{yipranmis} çanta yıpranmış" : "Yıpranmış çanta yok", yipranmis > 0 ? "uyari-renk" : null);
                }

                // Kurye seçimi
                if (ui.selectedCourier == null || !cm.Couriers.Contains(ui.selectedCourier))
                    ui.selectedCourier = cm.Couriers.Count > 0 ? cm.Couriers[0] : null;
                var sc = ui.selectedCourier;
                y.Bolum("Kurye bazında");
                kuryeAdlari.Clear();
                foreach (var c in cm.Couriers) kuryeAdlari.Add(c.ShortName);
                y.Secim(kuryeAdlari, sc != null ? cm.Couriers.IndexOf(sc) : -1,
                        v => { if (v >= 0 && v < cm.Couriers.Count) ui.selectedCourier = cm.Couriers[v]; }, true);
                if (sc != null)
                {
                    var cezalar = sc.ActivePenalties();
                    if (cezalar.Count == 0) y.Yazi($"{sc.ShortName}: eksik yok.");
                    foreach (var p in cezalar) y.Uyari("• " + p);
                }

                // Ekipmanlar
                foreach (var e in GameData.Equipment)
                {
                    var tur = e.kind;
                    int var_ = 0, lazim = 0;
                    foreach (var ec in cm.Couriers) { if (ec.Has(tur)) var_++; else if (CourierManager.Usefulness(ec, tur) >= 0.5f) lazim++; }
                    y.KartBasla();
                    y.Baslik($"{e.name} — {(Sade.Depozito ? "depozito" : "fiyatı")} {Ft(e.price)}");
                    y.Kucuk(e.effect);
                    y.Yazi($"{var_} kuryede var · şu an işine yarayıp eksik olan: {lazim} · depoda {cm.gearStock[(int)tur]}",
                           lazim > 0 ? "kucuk uyari-renk" : "kucuk soluk");
                    y.SatirBasla();
                    if (sc != null)
                    {
                        if (sc.Has(tur))
                            y.Kucuk($"{sc.ShortName}: var" + (Sade.Depozito && sc.depositGear != null && sc.depositGear.Contains(tur) ? " (depozitolu)" : ""));
                        else if (clock.TotalMinutes < sc.gearNudgeUntil) y.Kucuk($"{sc.ShortName}'a hatırlatıldı");
                        else
                        {
                            var kurye = sc;
                            y.Dugme($"{sc.ShortName}'a hatırlat", () => cm.RemindGear(kurye, tur), true, "kucuk-dg");
                        }
                    }
                    y.Dugme("Eksik olanlara hatırlat", () => cm.RemindAll(tur), true, "kucuk-dg");
                    y.SatirBitir();
                    y.KartBitir();
                }
                y.Bitir();
            }
        }
    }
}
