using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Sanal mutfak sekmesi (İşletme > Mutfak; UI Toolkit). Kapalıyken: ne olduğu, kilit ve "Mutfağı kur". Açıkken: bugünün sayıları,
    /// marka marka popülerlik, menü seviyesi ve sipariş; menü geliştirme ve yeni marka düğmeleri. Veri SanalMutfak'tan.
    /// </summary>
    public partial class GameUI
    {
        class UiMutfak : UiSekme
        {
            Yazici y;

            protected override void Kur() { y = new Yazici(ui, kok); }

            public override void Yenile()
            {
                var sm = SanalMutfak.I;
                if (sm == null || GameClock.I == null) return;
                y.Basla();
                y.Yazi("Sadece uygulamada satılan kendi yemek markaların, tek bir mutfakta. Komisyon yok, sepetin tamamı senin.");
                y.Yardim($"Malzeme sepetin %{SanalMutfak.GidaOrani * 100f:0}'si, her teslimatta gider yazılır. Mutfağın kira ve enerjisi günde {Ft(SanalMutfak.GunlukKira)}, " +
                         $"her marka için bir aşçı günde {Ft(SanalMutfak.AsciGunluk)}. Yeni marka 0,6 popülerlikle başlar; her gün biraz artar, iyi puan yükseltir, " +
                         "kötü puan düşürür. Menü geliştirme talebi ve müşteri puanını artırır. Siparişler normal yemek siparişi gibi kuryelere gider.");

                if (!sm.IsOpen)
                {
                    string kilit = sm.KilitNedeni;
                    y.KutularBasla();
                    y.Kutu("Kurulum", Ft(SanalMutfak.KurulumBedeli), "ilk marka dahil");
                    y.Kutu("Günlük gider", Ft(SanalMutfak.GunlukKira + SanalMutfak.AsciGunluk), "kira + 1 aşçı");
                    y.Kutu("Markalar", SanalMutfak.Markalar.Length.ToString(), $"yenisi {Ft(SanalMutfak.MarkaBedeli)}");
                    y.KutularBitir();
                    foreach (var t in SanalMutfak.Markalar) y.KV(t.ad, $"{t.tur} · sepet {Ft(t.sepetMin)}–{Ft(t.sepetMax)}");
                    if (kilit.Length > 0) y.Uyari($"Kilitli: {kilit}.");
                    y.Dugme($"Mutfağı kur ({Ft(SanalMutfak.KurulumBedeli)})", () => ui.AskConfirm("Sanal mutfak kur",
                            $"Şehirde bir mutfak kiralanır, ekipman alınır ve ilk markan {SanalMutfak.Markalar[0].ad} uygulamada açılır. " +
                            $"Bedel {Ft(SanalMutfak.KurulumBedeli)}, günlük gider {Ft(SanalMutfak.GunlukKira + SanalMutfak.AsciGunluk)}.",
                            "Evet, kur", () => sm.Kur()),
                        kilit.Length == 0 && EconomyManager.I.FreeCash >= SanalMutfak.KurulumBedeli);
                    y.Bitir();
                    return;
                }

                long dunNet = sm.dunSatis - sm.dunMalzeme - sm.dunGider;
                y.KutularBasla();
                y.Kutu("Bugün", $"{sm.bugunSiparis} sipariş", Ft(sm.bugunSatis));
                y.Kutu("Dün net", Ft(dunNet), $"satış {Ft(sm.dunSatis)} − malzeme {Ft(sm.dunMalzeme)} − gider {Ft(sm.dunGider)}", MoneyColor(dunNet));
                y.Kutu("Günlük gider", Ft(sm.GunlukGider), $"{sm.Acik.Count} aşçı");
                y.Kutu("Toplam", $"{sm.toplamSiparis} sipariş", Ft(sm.toplamSatis - sm.toplamMalzeme - sm.toplamGider) + " net");
                y.KutularBitir();
                y.Kucuk($"Mutfak: {CityMap.DistrictOf(sm.Acik[0].shop.tile)} · {GameClock.I.Day - sm.acilisGunu + 1}. gün · açık 10:00–23:00 (hafta sonu 24:00)");

                foreach (var m in sm.Acik)
                {
                    y.KartBasla();
                    y.Baslik($"{m.T.ad} · {m.T.tur}");
                    y.Cubuk("Popülerlik", Mathf.Clamp01(m.populerlik / 1.5f), m.populerlik >= 1f ? U.Iyi : m.populerlik >= 0.6f ? UITheme.Accent : U.Uyari, $"{m.populerlik:0.00}");
                    y.Kucuk($"Menü {m.menu}/3 · bugün {m.bugunSiparis} sipariş ({Ft(m.bugunSatis)}) · toplam {m.siparis} · puan +{0.1f * (m.menu - 1):0.0}");
                    var mm = m;
                    if (m.menu < 3)
                        y.Dugme($"Menüyü geliştir: {m.menu + 1}. seviye ({Ft(sm.MenuBedel(m))})", () => sm.MenuGelistir(mm),
                                EconomyManager.I.FreeCash >= sm.MenuBedel(m), "kucuk-dg");
                    y.KartBitir();
                }
                int sira = sm.SiradakiMarka;
                if (sira >= 0)
                {
                    var t = SanalMutfak.Markalar[sira];
                    y.Dugme($"Yeni marka aç: {t.ad} ({t.tur}) · {Ft(SanalMutfak.MarkaBedeli)}", () => ui.AskConfirm("Yeni sanal marka",
                            $"{t.ad} aynı mutfakta açılır: sepet {Ft(t.sepetMin)}–{Ft(t.sepetMax)}, bir aşçı daha (günde {Ft(SanalMutfak.AsciGunluk)}).",
                            "Evet, aç", () => sm.MarkaAc()),
                        EconomyManager.I.FreeCash >= SanalMutfak.MarkaBedeli);
                }
                y.Bitir();
            }
        }
    }
}
