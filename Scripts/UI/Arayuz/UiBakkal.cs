using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Canlı bakkalın (World/BakkalCanli) dükkân kartındaki bölümü (UI Toolkit; bina kartının içinde, UiBinaKarti.BinaDukkan çağırır):
    /// bugünkü siparişler ve kazanç (gerçek sipariş verisi), raf doluluğu ve içerideki müşteri (görsel), canlı pencere
    /// (ikinci kamera), "İçeri gir" kamerası, tıklanan nesnenin bilgisi ve kozmetik süsler.
    /// Süsler yalnız görseldir: para harcamaz, sipariş süresini değiştirmez, kayda girmez (PlayerPrefs).
    /// </summary>
    public partial class GameUI
    {
        void BakkalKarti(Yazici y, Shop s)
        {
            var b = BakkalCanli.Bul(s);
            if (b == null) return;
            var z = b.OzetAl();
            bool iceride = BakkalCanli.Iceride == b;

            y.Bolum("Dükkânın içi");
            // Canlı pencere: içeriden küçük görüntü (içerideyken ana kamera zaten orada)
            var doku = iceride ? null : b.PencereDoku();
            var resim = y.Ozel("bakkal-pencere", () =>
            {
                var r = new Image { scaleMode = ScaleMode.ScaleAndCrop };
                r.style.height = 150f;
                r.style.marginTop = 4f;
                r.style.marginBottom = 6f;
                r.pickingMode = PickingMode.Ignore;
                return r;
            });
            if (resim.image != doku) resim.image = doku;
            U.Goster(resim, doku != null);

            y.KutularBasla();
            y.Kutu("Bugün", $"{z.bugun} sipariş", $"{z.teslim} teslim · {z.yolda} yolda");
            y.Kutu("Bugünkü kazanç", Ft(z.kazanc), "teslim edilenlerden");
            y.Kutu("Tezgâh", z.hazir > 0 ? $"{z.hazir} poşet hazır" : "boş", z.hazirlaniyor > 0 ? $"{z.hazirlaniyor} hazırlanıyor" : "hazırlanan yok",
                   z.hazir > 0 ? U.Iyi : (Color?)null);
            y.Kutu("İçeride", z.simule ? $"{z.musteri} müşteri" : "—", z.simule ? (z.acik ? "şu an" : "kapalı") : "yaklaşınca görünür");
            y.KutularBitir();
            Color stokRenk = z.stok > 0.5f ? U.Iyi : z.stok > 0.25f ? U.Hardal : U.Uyari;
            y.Cubuk("Raf doluluğu", z.stok, stokRenk, $"%{z.stok * 100f:0}");
            y.Kucuk(z.tedarik ? "Tedarik kamyoneti arka kapıda: koliler iniyor, raflar doluyor."
                              : !z.acik ? "Dükkân kapalı: kepenkler inik." : "Raflar gün içinde boşalır; her sabah 06:30'da tedarik gelir.");

            if (iceride)
            {
                y.Kucuk("İçeridesin: fareyle sürükle ya da ok tuşlarıyla etrafa bak, Tab ile yer değiştir, Esc ile çık.");
                for (int i = 0; i < BakkalCanli.BakisAd.Length; i++)
                {
                    int no = i;
                    y.Dugme("Bakış: " + BakkalCanli.BakisAd[i], () => b.BakisSec(no), b.Bakis != i, "kucuk-dg");
                }
                y.Dugme("Dışarı çık", () => b.Cik(), true, "vurgu");
                return;
            }
            y.Dugme("İçeri gir", () => b.IceriGir(), true, "vurgu");

            // Tıklanan nesne ve süsler
            var n = b.SeciliNesne;
            if (n == BakkalCanli.Nesne.Yok)
            {
                y.Kucuk("İçerideki raflara, kasaya, soğutuculara ya da vitrine tıklayabilirsin.");
                return;
            }
            y.Bolum(n == BakkalCanli.Nesne.Raf ? "Raflar" : n == BakkalCanli.Nesne.Kasa ? "Kasa" : n == BakkalCanli.Nesne.Sogutucu ? "Soğutucular" : "Vitrin");
            switch (n)
            {
                case BakkalCanli.Nesne.Raf:
                    y.Kucuk($"Üç orta raf ve duvar rafı. Doluluk %{z.stok * 100f:0}: satış ve siparişlerle boşalır, sabah tedarikle dolar.");
                    break;
                case BakkalCanli.Nesne.Kasa:
                    y.Kucuk(z.hazir > 0 ? $"Tezgâhta {z.hazir} sipariş poşeti kuryesini bekliyor." :
                            z.hazirlaniyor > 0 ? "Kasiyer siparişi raflardan topluyor." : "Kasiyer müşterilere bakıyor.");
                    BakkalSus(y, BakkalCanli.SusKasa2, "İkinci kasa");
                    break;
                case BakkalCanli.Nesne.Sogutucu:
                    y.Kucuk("Üç içecek soğutucusu ve dondurma dolabı.");
                    BakkalSus(y, BakkalCanli.SusSogutucu, "Yeni soğutucu dolap");
                    break;
                default:
                    y.Kucuk("Sokağa bakan iki vitrin: ekmek ve meyve teşhiri.");
                    BakkalSus(y, BakkalCanli.SusVitrin, "Vitrin yenileme");
                    break;
            }
        }

        /// <summary>Kozmetik süs düğmesi (ücretsiz, yalnız görsel).</summary>
        void BakkalSus(Yazici y, int sus, string ad)
        {
            bool var_ = BakkalCanli.Sus(sus);
            y.Dugme(var_ ? ad + ": kaldır" : ad + ": ekle (görsel)", () => BakkalCanli.SusDegistir(sus), true, "kucuk-dg");
        }
    }
}
