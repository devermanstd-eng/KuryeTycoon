using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Olay anlatıcısı (RimWorld'deki Cassandra / Phoebe / Randy gibi): kötü olayların (zor kararlar, krizler, dükkan sorunları)
    /// ne sıklıkta geldiğini ve büyük bir olaydan sonra nefes alma süresini ayarlar. Yeni oyunda seçilir, Ayarlar'dan değişir.
    ///  • Sakin: olaylar seyrek, büyük olaydan sonra 7 gün mola; zor günlerden sonra bazen küçük bir yardım gelir.
    ///  • Klasik: bugünkü denge (kriz 10–22 günde bir), büyük olaydan sonra 3 gün mola.
    ///  • Rastgele: aralık 4–30 gün, mola yok; art arda iki kriz de gelebilir.
    /// Kayda yazılır.
    /// </summary>
    public static class Anlatici
    {
        public enum Tur { Sakin, Klasik, Rastgele }

        public static readonly string[] Adlar = { "Sakin", "Klasik", "Rastgele" };
        public static readonly string[] Aciklamalar =
        {
            "Kötü olaylar seyrek; büyük bir olaydan sonra bir hafta nefes aldırır, zor günlerden sonra bazen küçük bir yardım gelir. Şehri kurmaya odaklanmak için.",
            "Dengeli: krizler 10–22 günde bir, büyük olaydan sonra 3 gün mola. Önerilen.",
            "Ne zaman ne geleceği belli değil: aralık 4–30 gün, mola yok, art arda iki kriz de gelebilir.",
        };

        public static Tur Secili = Tur.Klasik;
        static int sonBuyukOlayGunu = -999;

        public static void Sifirla() { sonBuyukOlayGunu = -999; }

        static int Gun => GameClock.I != null ? GameClock.I.Day : 0;

        /// <summary>Büyük olaydan (kriz, zor karar) sonra mola: o günlerde yeni kötü olay gelmez.</summary>
        public static bool Mola
        {
            get
            {
                int m = Secili == Tur.Sakin ? 7 : Secili == Tur.Klasik ? 3 : 0;
                return Gun - sonBuyukOlayGunu < m;
            }
        }

        /// <summary>Kriz ya da zor karar çıkınca çağrılır.</summary>
        public static void BuyukOlay() => sonBuyukOlayGunu = Gun;

        /// <summary>Günlük küçük olayların (dükkan sorunu, viral yorum, teklif) çıkma ihtimali çarpanı.</summary>
        public static float OlayCarpani => Secili == Tur.Sakin ? 0.6f : Secili == Tur.Rastgele ? 1.15f : 1f;

        /// <summary>Zor karar kartının her gece çıkma ihtimali çarpanı.</summary>
        public static float ZorKararCarpani => Secili == Tur.Sakin ? 0.5f : Secili == Tur.Rastgele ? 1.3f : 1f;

        /// <summary>Bir sonraki krize kadar gün.</summary>
        public static int KrizAraligi()
        {
            switch (Secili)
            {
                case Tur.Sakin: return Random.Range(18, 31);
                case Tur.Rastgele: return Random.Range(4, 31);
                default: return Random.Range(10, 22);
            }
        }

        /// <summary>Her gece: Sakin anlatıcı zor bir olaydan sonra bazen küçük bir yardım gönderir.</summary>
        public static void YeniGun()
        {
            if (Secili != Tur.Sakin || GameClock.I == null || EventManager.I == null) return;
            int gecen = Gun - sonBuyukOlayGunu;
            if (gecen < 1 || gecen > 5 || Random.value > 0.25f) return;
            switch (Random.Range(0, 3))
            {
                case 0:
                {
                    long hibe = Denge.Olcekle(60000);
                    EconomyManager.I.AddIncome(hibe, "Esnaf derneği desteği");
                    EventManager.I.Notify($"Mahalle esnaf derneği zor günlerde yanında: {GameData.Ft(hibe)} destek gönderdi.");
                    break;
                }
                case 1:
                    Itibar.Olay(0.05f);
                    EventManager.I.Notify("Yerel bir gazete kuryelerinin hikâyesini yazdı: itibar biraz arttı.");
                    break;
                default:
                    if (CourierManager.I != null)
                        foreach (var c in CourierManager.I.Couriers) c.morale = Mathf.Clamp01(c.morale + 0.05f);
                    EventManager.I.Notify("Bir müşteri kuryelere teşekkür kartı ve tatlı gönderdi: kuryelerin morali biraz yükseldi.");
                    break;
            }
        }
    }
}
