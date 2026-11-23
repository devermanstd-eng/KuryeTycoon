using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Dükkan sadakati (0–100): sözleşmeli dükkanla birlikte geçen zaman ve iyi hizmetle yavaş yavaş artar.
    /// Dört seviye: Yeni (0) · Düzenli (25) · Sadık (50) · Ortak (80). Gerçekte de platformlar uzun süre birlikte çalıştıkları
    /// restoranlarla daha sağlam ilişki kurar; restoran seni uygulamada öne çıkarır, rakibin teklifini kolay kabul etmez.
    ///  • Günlük artış: 0,9 + (memnuniyet − 60) × 0,03; münhasır dükkanda +0,5, rakiple de çalışan dükkanda −0,3.
    ///    Memnuniyet 70'te ~3 haftada Düzenli, ~6 haftada Sadık; münhasır ve memnun dükkan ~6 haftada Ortak olur.
    ///    Sözleşme bitince günde 2 puan düşer; dükkan rakibe giderse sadakatin %70'i kaybolur.
    ///  • Etkiler (seviye başına): rakip teklifinde kalma ihtimali +4 puan, münhasırlık kabulü +6 puan, sipariş +%3.
    ///    Sadık dükkan rakibin anlaşma teklifini %35, Ortak dükkan %70 ihtimalle geri çevirir.
    ///  • Primli münhasırlık: dükkana münhasırlık için tek seferlik prim ödersen (geçen ay + bu ayki sipariş × 300 Ft,
    ///    375–5 000 €) kabul ihtimali 30 puan artar. Prim sadece kabul ederse ödenir (gerçekte platformlar münhasır
    ///    restoranlara imza primi ya da pazarlama bütçesi verir).
    /// Değer Shop.sadakat'ta, kayda ShopSave ile girer.
    /// </summary>
    public static class DukkanSadakati
    {
        public static readonly string[] Adlar = { "Yeni", "Düzenli", "Sadık", "Ortak" };
        public static readonly float[] Esikler = { 0f, 25f, 50f, 80f };
        public const float PrimEki = 0.3f;

        public static int Seviye(Shop s)
        {
            if (s == null) return 0;
            float v = s.sadakat;
            return v >= Esikler[3] ? 3 : v >= Esikler[2] ? 2 : v >= Esikler[1] ? 1 : 0;
        }

        public static string Ad(Shop s) => Adlar[Seviye(s)];

        /// <summary>Rakip teklifinde dükkanın kalma ihtimaline eklenen.</summary>
        public static float TutmaEki(Shop s) => 0.04f * Seviye(s);

        /// <summary>Münhasırlık kabul ihtimaline eklenen.</summary>
        public static float MunhasirlikEki(Shop s) => 0.06f * Seviye(s);

        /// <summary>Rakip bu dükkanla anlaşmaya kalkınca dükkanın geri çevirme ihtimali.</summary>
        public static float RakipRedOrani(Shop s)
        {
            if (s == null || !s.contracted) return 0f;
            int lv = Seviye(s);
            return lv >= 3 ? 0.7f : lv == 2 ? 0.35f : 0f;
        }

        /// <summary>Sipariş talebi çarpanı (dükkan seni öne çıkarır).</summary>
        public static float SiparisCarpani(Shop s) =>
            s == null || !s.contracted || s.own || s.kargo ? 1f : 1f + 0.03f * Seviye(s);

        public static float GunlukArtis(Shop s)
        {
            if (s == null || !s.contracted) return -2f;
            float d = 0.9f + (s.satisfaction - 60f) * 0.03f;
            if (s.exclusive) d += 0.5f;
            else if (s.rivalContracted) d -= 0.3f;
            return d;
        }

        /// <summary>Bir sonraki seviyeye bu hızla kaç gün (artmıyorsa -1).</summary>
        public static int SonrakineGun(Shop s)
        {
            int lv = Seviye(s);
            if (lv >= 3) return 0;
            float d = GunlukArtis(s);
            if (d <= 0.01f) return -1;
            return Mathf.CeilToInt((Esikler[lv + 1] - s.sadakat) / d);
        }

        public static string EtkiMetni(int lv)
        {
            if (lv <= 0) return "henüz ek etkisi yok";
            string t = $"sipariş +%{3 * lv}, rakip teklifinde kalma +{4 * lv} puan, münhasırlık kabulü +{6 * lv} puan";
            if (lv == 2) t += ", rakibin anlaşma teklifini %35 ihtimalle geri çevirir";
            if (lv >= 3) t += ", rakibin anlaşma teklifini %70 ihtimalle geri çevirir";
            return t;
        }

        /// <summary>Münhasırlık primi (tek seferlik, sadece kabul ederse ödenir).</summary>
        public static long Prim(Shop s)
        {
            if (s == null) return 0;
            float v = Mathf.Clamp((s.lastMonthOrders + s.monthOrders) * 300f, 150000f, 2000000f);
            return (long)Mathf.Round(v / 1000f) * 1000;
        }

        /// <summary>Eski kayıt: sadakat yoksa sözleşme yaşından tahmin (en fazla 40).</summary>
        public static float EskiKayitTahmini(Shop s, int bugun) =>
            s == null || !s.contracted ? 0f : Mathf.Clamp((bugun - s.contractStartDay) * 0.8f, 0f, 40f);

        public static void Init()
        {
            GameClock.I.OnNewDay -= YeniGun;
            GameClock.I.OnNewDay += YeniGun;
        }

        static void YeniGun()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || CityMap.I == null) return;
            foreach (var s in CityMap.I.Shops)
            {
                if (s.own || s.kargo) continue;
                int once = Seviye(s);
                s.sadakat = Mathf.Clamp(s.sadakat + GunlukArtis(s), 0f, 100f);
                int sonra = Seviye(s);
                if (sonra > once && s.contracted)
                {
                    Telemetry.Add($"Dükkan sadakati: {Adlar[sonra]}");
                    string m = $"{s.Name} artık {Adlar[sonra].ToLowerInvariant()} iş ortağın (sadakat {s.sadakat:0}/100): {EtkiMetni(sonra)}.";
                    if (sonra >= 2) EventManager.I.Notify(m); else EventManager.I.NotifyQuiet(m);
                }
            }
        }
    }
}
