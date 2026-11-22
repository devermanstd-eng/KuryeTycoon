using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Dükkan mutfağı: siparişin ne kadar sürede hazırlanacağı.
    ///  • Her dükkanın kendi hızı var (ShopDef.prepMinutes: büfe 4–6 dk, burger 8, pizza 18, gurme 25).
    ///  • Yoğun saatte mutfak yavaşlar: yemekte öğle (11–14) ve akşam (18–21) ×1,25, markette iş çıkışı (16–20) ×1,15.
    ///  • Aynı anda hazırlanan siparişin artıkça sıra uzar: hazırlanmakta olan her sipariş (ilk ikisinden sonra) +%8, en fazla ×1,8.
    ///    Bu yüzden bir dükkana çok sipariş yağınca kurye kapıda bekler.
    ///  • Öncelikli hazırlık anlaşması: dükkan senin siparişlerini sıraya sokmadan hemen hazırlar (temel süre ×0,7, yoğunluk
    ///    etkisinin sadece %30'u). Karşılığında dükkan komisyonundan 3 puan daha fazla pay alır (senin komisyonun düşer).
    ///    Dükkan bu anlaşmadan memnun olur: memnuniyet +4, rakip teklifinde kalma ihtimali +%10.
    /// </summary>
    public static class Mutfak
    {
        /// <summary>Öncelikli hazırlık anlaşmasında dükkana bırakılan komisyon payı (senin komisyonun bu kadar düşer).</summary>
        public const float OncelikKomisyon = 0.03f;
        /// <summary>Öncelikli hazırlıkta temel süre çarpanı.</summary>
        public const float OncelikHiz = 0.7f;
        /// <summary>Anlaşma en sık bu kadar günde bir değiştirilebilir.</summary>
        public const int DegisimBekleme = 7;
        /// <summary>Anlaşmalı dükkan rakip teklifinde bu kadar daha sadık.</summary>
        public const float OncelikSadakat = 0.1f;

        /// <summary>Saate göre mutfak yoğunluğu (1 = normal).</summary>
        public static float SaatCarpani(Shop s, int h, bool hafsonu)
        {
            if (s == null || s.def == null || s.own || s.kargo) return 1f;
            if (s.def.isMarket) return h >= 16 && h < 20 ? 1.15f : 1f;
            if (hafsonu) return (h >= 12 && h < 15) || (h >= 18 && h < 22) ? 1.2f : 1f;
            return (h >= 11 && h < 14) || (h >= 18 && h < 21) ? 1.25f : 1f;
        }

        /// <summary>Dükkanda şu an hazırlanmakta olan (henüz hazır olmayan, alınmamış) senin siparişlerin.</summary>
        public static int Kuyruk(Shop s)
        {
            var om = OrderManager.I;
            if (om == null || s == null || GameClock.I == null) return 0;
            float now = GameClock.I.TotalMinutes;
            int n = 0;
            foreach (var o in om.Pending) if (o.shop == s && o.readyAt > now && !o.planli) n++;
            foreach (var o in om.Active) if (o.shop == s && o.status == OrderStatus.Atandi && o.readyAt > now) n++;
            return n;
        }

        /// <summary>Sıra etkisi: ilk iki siparişten sonra her biri +%8, en fazla ×1,8.</summary>
        public static float KuyrukCarpani(int kuyruk) => Mathf.Min(1.8f, 1f + 0.08f * Mathf.Max(0, kuyruk - 2));

        /// <summary>Bu dükkanda şimdi verilen bir siparişin hazırlık süresi çarpanı (prepMultiplier hariç).</summary>
        public static float Carpan(Shop s)
        {
            if (s == null || s.def == null || s.own || s.kargo) return 1f;
            var clk = GameClock.I;
            float yogun = SaatCarpani(s, clk != null ? clk.Hour : 12, clk != null && clk.IsWeekend) * KuyrukCarpani(Kuyruk(s));
            if (s.oncelikli) return OncelikHiz * (1f + (yogun - 1f) * 0.3f);
            return yogun;
        }

        /// <summary>Şu an verilen bir siparişin tahmini hazırlık süresi (dk).</summary>
        public static float TahminiDk(Shop s) => s.def.prepMinutes * s.prepMultiplier * Carpan(s);

        /// <summary>Yoğun saatte (sıra yokken) tahmini hazırlık süresi (dk): anlaşma kararını vermek için.</summary>
        public static float YogunDk(Shop s, bool oncelikli)
        {
            float yogun = s.def.isMarket ? 1.15f : 1.25f;
            yogun *= KuyrukCarpani(5);   // yoğun saatte tipik sıra
            float m = oncelikli ? OncelikHiz * (1f + (yogun - 1f) * 0.3f) : yogun;
            return s.def.prepMinutes * s.prepMultiplier * m;
        }

        public static bool DegistirilebilirMi(Shop s) =>
            GameClock.I == null || GameClock.I.Day - s.oncelikDegisimGunu >= DegisimBekleme;

        /// <summary>Öncelikli hazırlık anlaşmasını aç/kapat.</summary>
        public static void Degistir(Shop s, bool ac)
        {
            if (s == null || s.oncelikli == ac || !DegistirilebilirMi(s)) return;
            s.oncelikli = ac;
            s.oncelikDegisimGunu = GameClock.I != null ? GameClock.I.Day : 0;
            if (ac)
            {
                RivalCompany.AddSatisfaction(s, 4f);
                EventManager.I.Notify($"{s.Name} ile öncelikli hazırlık anlaşması yapıldı: siparişlerin sıraya girmeden hazırlanacak " +
                                      $"(yoğun saatte ~{YogunDk(s, false):0} dk yerine ~{YogunDk(s, true):0} dk). Komisyonun %{s.Commission * 100f:0.#} oldu.");
                Telemetry.Add("Mutfak: öncelikli hazırlık anlaşması");
            }
            else
            {
                RivalCompany.AddSatisfaction(s, -3f);
                EventManager.I.Notify($"{s.Name} ile öncelikli hazırlık anlaşması bitti: komisyonun %{s.Commission * 100f:0.#}'e döndü.");
            }
        }

        /// <summary>Arayüz için kısa açıklama.</summary>
        public static string Ozet(Shop s)
        {
            int k = Kuyruk(s);
            string sira = k > 0 ? $" · şu an mutfakta {k} siparişin" : "";
            return $"Hazırlık şu an ~{TahminiDk(s):0} dk (normal ~{s.def.prepMinutes * s.prepMultiplier:0} dk, yoğun saatte ~{YogunDk(s, s.oncelikli):0} dk){sira}";
        }
    }
}
