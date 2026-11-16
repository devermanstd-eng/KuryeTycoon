using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Yorumlar (Mesajlar) penceresi (UI Toolkit, Yazıcı ile; eski IMGUI DrawMessages yerine).
    /// Üstte (kaymaz) filtre (kurye mesajları açıksa); yorum sayısı, ortalama yıldız ve yıldız dağılımı kutularda;
    /// her yorum kısa bir kart: kim · dükkan · yıldızlar · saat, altında yorum. Cevap bekleyen kurye kararları en üstte.
    /// Pencere açıkken okunmamışlar okundu sayılır.
    /// </summary>
    public partial class GameUI
    {
        class UiMesajlar : UiSekme
        {
            Yazici y;
            U.Secenekler filtre;
            readonly int[] dagilim = new int[5];

            protected override void Kur()
            {
                if (CourierMessages.ShowCourierDecisions || CourierMessages.ShowCourierChatter)
                {
                    filtre = U.SecenekEkle(Ust(), new[] { "Tümü", "Kurye kararları", "Yorumlar" }, () => ui.msgFilter, v => { ui.msgFilter = v; Yenile(); });
                    filtre.kok.style.marginTop = 2;
                    filtre.kok.style.marginBottom = 8;
                }
                y = new Yazici(ui, kok);
            }

            public override void Yenile()
            {
                var cmsg = CourierMessages.I;
                if (cmsg == null || GameClock.I == null) return;
                bool kuryeMesaji = CourierMessages.ShowCourierDecisions || CourierMessages.ShowCourierChatter;
                if (!kuryeMesaji) ui.msgFilter = 2;   // sadece müşteri yorumları
                filtre?.Yenile();
                y.Basla();

                y.Yazi("Teslimattan sonra müşterilerin bıraktığı yorumlar ve yıldızlar.");
                y.Yardim("Kötü yorumların küçük bir kısmı sosyal medyada yayılabilir: itibar düşer, 2 gün siparişler azalır. " +
                         "Çok iyi yorumlar da yayılabilir. Yorumları iyileştirmenin yolu: zamanında, sıcak ve temiz çantayla teslimat.");
                if (cmsg.ViralActive)
                {
                    bool iyi = cmsg.ViralFactor > 1f;
                    if (iyi) y.Not("Viral övgü etkisi sürüyor: siparişler ~%10 fazla.");
                    else y.Uyari("Viral kötü yorum etkisi sürüyor: siparişler ~%10 az.");
                }

                if (cmsg.reviewsTotal > 0)
                {
                    System.Array.Clear(dagilim, 0, 5);
                    int listede = 0;
                    foreach (var m in cmsg.Messages)
                        if (m.kind == PhoneKind.Yorum && m.stars >= 1 && m.stars <= 5) { dagilim[m.stars - 1]++; listede++; }
                    float ort = cmsg.AverageStars;
                    y.KutularBasla();
                    y.Kutu("Ortalama", $"{ort:0.0}★", $"{cmsg.reviewsTotal} yorum", ort >= 4.5f ? U.Iyi : ort >= 3.8f ? (Color?)null : U.Uyari);
                    y.Kutu("5★ ve 4★", listede > 0 ? $"%{(dagilim[4] + dagilim[3]) * 100f / listede:0}" : "–", "son yorumlarda", null, null, U.Iyi);
                    y.Kutu("1★ ve 2★", listede > 0 ? $"%{(dagilim[0] + dagilim[1]) * 100f / listede:0}" : "–", "son yorumlarda",
                           dagilim[0] + dagilim[1] > 0 ? U.Kotu : (Color?)null);
                    if (cmsg.PendingCount > 0) y.Kutu("Cevap bekleyen", cmsg.PendingCount.ToString(), "kurye kararı", U.Uyari);
                    y.KutularBitir();
                }

                if (cmsg.Messages.Count == 0)
                {
                    y.Kucuk("Henüz yorum yok. Müşteriler teslimattan sonra yorum bırakır.");
                    y.Bitir();
                    return;
                }

                // Cevap bekleyenler en üstte
                foreach (var m in cmsg.Messages) if (m.Pending && ui.msgFilter != 2) Kart(m);
                foreach (var m in cmsg.Messages)
                {
                    if (m.Pending) continue;
                    if (m.kind == PhoneKind.Bilgi && !CourierMessages.ShowCourierChatter) continue;
                    if (ui.msgFilter == 1 && m.kind == PhoneKind.Yorum) continue;
                    if (ui.msgFilter == 2 && m.kind != PhoneKind.Yorum) continue;
                    Kart(m);
                }
                y.Bitir();
                if (cmsg.Unread > 0) cmsg.MarkAllRead();
            }

            void Kart(PhoneMessage m)
            {
                y.KartBasla();
                if (m.kind == PhoneKind.Yorum)
                {
                    string ust = $"<b>{m.from}</b>" + (string.IsNullOrEmpty(m.shopName) ? "" : $" · {m.shopName}") +
                                 $"   {YildizYazi(m.stars)}   <color=#9EA6B5>{m.timeText}</color>" + (m.viral ? "   <color=#FFA847>VİRAL</color>" : "");
                    y.Yazi(ust, "kucuk");
                    y.Yazi($"\"{m.text}\"");
                }
                else
                {
                    y.Yazi((m.Pending ? "<color=#FFA847>●</color> " : "") + $"<b>{m.from}</b>   <color=#9EA6B5>{m.timeText}</color>", "kucuk");
                    y.Yazi(m.text);
                }
                if (m.Pending)
                {
                    y.SatirBasla();
                    for (int i = 0; i < m.optionTexts.Count; i++)
                    {
                        int k = i;
                        var msg = m;
                        y.Dugme(m.optionTexts[i], () => CourierMessages.I.Answer(msg, k), true, "kucuk-dg");
                    }
                    y.SatirBitir();
                    float kalan = Mathf.Max(0f, m.expiresAt - GameClock.I.TotalMinutes);
                    y.Kucuk($"{kalan:0} dk içinde cevap vermezsen kurye kendi karar verir.");
                }
                else if (!string.IsNullOrEmpty(m.answer)) y.Kucuk(m.answer);
                y.KartBitir();
            }
        }
    }
}
