using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Moralin somut etkileri tek yerde (kurye kartında açıklama olarak da gösterilir):
    ///  • Teklif kabulü: morali yüksek kurye daha az kazandıran / uzak işleri de kabul eder (beklentisi %15'e kadar düşer),
    ///    moralsiz kurye daha seçici olur (+%10). Moral %45'in altında teklifleri doğrudan da reddedebilir (Courier.RejectChance).
    ///  • Müşteri puanı: güler yüzlü kurye daha yüksek puan alır (moral %85'te +0,2, %30'da −0,24).
    ///  • Çevrimiçi olma: morali yüksek kurye daha sık çevrimiçi olur, moralsiz olan erken bırakır.
    ///  • Sadakat: moral %30'un altında kurye işi bırakmayı düşünür; rakipler moralsiz kuryeleri ayartır
    ///    (rakipler tetikteyken %42, saldırıdayken %48'in altındakileri). Rakip teklifinde morali yüksek kurye daha çok kalır.
    /// </summary>
    public static class MoralEtkisi
    {
        /// <summary>Teklif beklentisinin çarpanı: moral %35 → ×1,10, %60 → ×0,98, %85 → ×0,85.</summary>
        public static float TeklifCarpani(float moral) => Mathf.Lerp(1.1f, 0.85f, Mathf.InverseLerp(0.35f, 0.85f, moral));

        /// <summary>Kurye kartı için: bu kuryenin morali şu an ne kazandırıyor / kaybettiriyor.</summary>
        public static string Aciklama(Courier c)
        {
            float m = c.morale;
            float teklif = (TeklifCarpani(m) - 1f) * 100f;
            float puan = (m - 0.6f) * 0.8f;
            string t = teklif <= -1f ? $"teklif beklentisi %{-teklif:0} düşük (daha çok işi kabul eder)"
                     : teklif >= 1f ? $"teklif beklentisi %{teklif:0} yüksek (daha seçici)" : "teklif beklentisi normal";
            string p = Mathf.Abs(puan) < 0.02f ? "müşteri puanına etkisi yok" : $"müşteri puanı {(puan > 0 ? "+" : "−")}{Mathf.Abs(puan):0.00}";
            string sad;
            if (m < Denge.BirakmaMoralSiniri) sad = "işi bırakmayı düşünüyor, rakiplerin hedefinde";
            else if (m < 0.45f) sad = "teklifleri reddedebilir, rakipler ayartabilir";
            else if (m >= 0.75f) sad = "sadık: rakip teklifine kolay gitmez, sık çevrimiçi olur";
            else sad = "sadakati normal";
            return $"Moral %{m * 100f:0}: {t} · {p} · {sad}.";
        }
    }
}
