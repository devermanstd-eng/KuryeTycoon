using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Canlı atölye binasının (World/DukkanCanli) bina kartındaki bölümü (UI Toolkit; UiBinaKarti çağırır): canlı pencere
    /// (ikinci kamera), açık / kapalı durumu, içerideki insanlar, dükkânsa bugünkü siparişler (gerçek veri), "İçeri gir"
    /// kamerası ve bakış noktaları. Ayrıca kamera bir binanın içindeyken ekranın üstünde duran "Dışarı çık" düğmesi
    /// (mahalle bakkalı için de geçerli).
    /// </summary>
    public partial class GameUI
    {
        Button iceriCikDugme;

        /// <summary>
        /// Kamera içerideyken ekranın üst ortasında "Dışarı çık" düğmesi. Karttaki (havuzlanan) düğmeden bağımsızdır ve
        /// basıldığı anda (PointerDown) çalışır: kart o sırada yeniden çizilse de tıklama kaybolmaz.
        /// </summary>
        void UiIceriCikYenile()
        {
            bool gor = IceriBakis.Acik;
            if (iceriCikDugme == null)
            {
                if (!gor || katUst == null) return;
                iceriCikDugme = U.Dugme(katUst, "Dışarı çık (Esc)", null, "dg-iceri-cik");
                iceriCikDugme.style.position = Position.Absolute;
                iceriCikDugme.style.top = TopBar + 14f;
                iceriCikDugme.style.left = new Length(50f, LengthUnit.Percent);
                iceriCikDugme.style.marginLeft = -70f;
                iceriCikDugme.style.width = 140f;
                iceriCikDugme.style.height = 34f;
                U.Zemin(iceriCikDugme, UITheme.Accent);
                U.Renk(iceriCikDugme, UITheme.AccentText);
                iceriCikDugme.RegisterCallback<PointerDownEvent>(e =>
                {
                    IceriBakis.Cik();
                    binaKirli = true;
                    e.StopPropagation();
                }, TrickleDown.TrickleDown);
            }
            U.Goster(iceriCikDugme, gor);
        }

        /// <summary>Bina kartına canlı bina bölümünü yazar. Kamera bu binanın içindeyse true (kartın kalanı yazılmaz).</summary>
        bool DukkanKarti(Yazici y, DukkanCanli b)
        {
            if (b == null || b.Plan == null) return false;
            var z = b.OzetAl();
            bool iceride = DukkanCanli.Iceride == b;
            var p = b.Plan;

            y.Bolum("Binanın içi" + (string.IsNullOrEmpty(p.etiket) ? "" : " · " + p.etiket));
            var doku = iceride ? null : b.PencereDoku();
            var resim = y.Ozel("dukkan-pencere", () =>
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

            bool konut = p.turGrup == "Konut";
            y.KutularBasla();
            if (z.siparis)
            {
                y.Kutu("Bugün", $"{z.bugun} sipariş", $"{z.teslim} teslim · {z.yolda} yolda");
                y.Kutu("Bugünkü kazanç", Ft(z.kazanc), "teslim edilenlerden");
                y.Kutu("Tezgâh", z.hazir > 0 ? $"{z.hazir} poşet hazır" : "boş", z.hazirlaniyor > 0 ? $"{z.hazirlaniyor} hazırlanıyor" : "hazırlanan yok",
                       z.hazir > 0 ? U.Iyi : (Color?)null);
            }
            else y.Kutu("Durum", konut ? "Ev" : z.acik ? "Açık" : "Kapalı", konut ? "ışıklar akşam yanar" : z.acik ? "ışıklar yanıyor" : "ışıklar sönük");
            y.Kutu("İçeride", z.simule ? $"{z.musteri} {(konut ? "kişi" : "ziyaretçi")}" : "—",
                   z.simule ? (z.calisan > 0 ? $"{z.calisan} çalışan" : z.acik ? "şu an" : "kapalı") : "yaklaşınca görünür");
            y.KutularBitir();
            if (!z.acik && !konut) y.Kucuk(p.siparis ? "Kapalı: kepenkler inik, vitrin karanlık." : "Kapalı: içeride kimse yok, ışıklar sönük.");

            if (iceride)
            {
                y.Kucuk("İçeridesin: fareyle sürükle ya da ok tuşlarıyla etrafa bak, Tab ile yer değiştir, Esc ile çık.");
                for (int i = 0; i < b.BakisSayisi; i++)
                {
                    int no = i;
                    y.Dugme("Bakış: " + b.BakisAdi(i), () => b.BakisSec(no), b.Bakis != i, "kucuk-dg");
                }
                y.Dugme("Dışarı çık", () => IceriBakis.Cik(), true, "vurgu");
                return true;
            }
            y.Dugme("İçeri gir", () => b.IceriGir(), true, "vurgu");
            return false;
        }
    }
}
