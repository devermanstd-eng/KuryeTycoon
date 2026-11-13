using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Oyun dengesinin ayar düğmeleri tek yerde. Otomatik testlerin (60 gün, akıllı bot) sonucuna göre ayarlandı:
    /// eski ayarlarda aile borcu 9. günde bitiyor, kasa 60 günde 75 000 €'ya çıkıyor, moral hep %100, itibar hep ~4,9 kalıyordu.
    /// Hedef: ne öldüren zorluk ne de çok basit. Değiştirince otomatik testle tekrar bakılmalı.
    /// </summary>
    public static class Denge
    {
        // ------------------------------------------------------------------ fiyatlar şirketle büyür

        /// <summary>Fiyat çarpanının 1 olduğu günlük gelir (Ft). İlk haftanın tipik geliri ~400 €.</summary>
        public const float RefGunlukGelir = 160000f;
        /// <summary>Gelir 2 katına çıkınca fiyatlar 2^Us katına çıkar (1 = tam orantılı).</summary>
        public const float FiyatUs = 0.6f;
        public const float FiyatTavan = 5f;
        /// <summary>Bu bedelin altındaki kurulumlar çarpanın tamamını alır; çok pahalı son seviyeler (zaten büyük hedefler) çok az.</summary>
        const float TamOlcekSiniri = 1000000f, AzOlcekSiniri = 10000000f;

        static int carpanGunu = -1;
        static float carpan = 1f;

        /// <summary>Yeni oyun ya da yükleme: önceki oyunun önbelleği kalmasın.</summary>
        public static void Sifirla() { carpanGunu = -1; carpan = 1f; }

        /// <summary>
        /// Son 14 günün ortalama gelirine göre fiyat çarpanı (1–5). Günde bir hesaplanır ve 0,25'lik adımlarla yuvarlanır,
        /// böylece fiyatlar her an oynamaz.
        /// </summary>
        public static float FiyatCarpani
        {
            get
            {
                var clk = GameClock.I;
                if (clk == null) return 1f;
                if (clk.Day != carpanGunu) { carpanGunu = clk.Day; carpan = Hesapla(); }
                return carpan;
            }
        }

        static float Hesapla()
        {
            var d = StatsHistory.I != null ? StatsHistory.I.Days : null;
            if (d == null || d.Count == 0) return 1f;
            int n = Mathf.Min(14, d.Count);
            double t = 0;
            for (int i = d.Count - n; i < d.Count; i++) t += d[i].income;
            float oran = (float)(t / n / RefGunlukGelir);
            if (oran <= 1f) return 1f;
            float m = Mathf.Min(FiyatTavan, Mathf.Pow(oran, FiyatUs));
            return Mathf.Round(m * 4f) / 4f;
        }

        /// <summary>Kurulum bedelini şirketin büyüklüğüne göre ölçekler (1 000 Ft'a yuvarlı).</summary>
        public static long Olcekle(long taban)
        {
            float m = FiyatCarpani;
            if (m <= 1f || taban <= 0) return taban;
            // Pahalı son seviyeler zaten uzun vadeli hedef: çarpanın küçük bir kısmını alır
            float w = taban <= TamOlcekSiniri ? 1f
                    : taban >= AzOlcekSiniri ? 0.2f
                    : Mathf.Lerp(1f, 0.2f, Mathf.InverseLerp(Mathf.Log(TamOlcekSiniri), Mathf.Log(AzOlcekSiniri), Mathf.Log(taban)));
            float etkin = 1f + (m - 1f) * w;
            return (long)(Mathf.Round(taban * etkin / 1000f) * 1000f);
        }

        /// <summary>Günlük işletme giderleri daha yavaş büyür (çarpanın karekökü).</summary>
        public static int OlcekleGunluk(int taban)
        {
            float m = FiyatCarpani;
            if (m <= 1f || taban <= 0) return taban;
            return Mathf.RoundToInt(taban * Mathf.Sqrt(m) / 10f) * 10;
        }

        // ------------------------------------------------------------------ kurulum kredisi

        /// <summary>Kurulum kredisinin tamamı (Ft): 6 000 €, faiziyle 5 taksit (Mart–Temmuz sonu, ayda ~1 300 €). Başlangıç kasası ayrı (2 500 €).</summary>
        public const long AileBorcu = 2400000;

        // ------------------------------------------------------------------ yorgunluk

        /// <summary>Çalışırken dakika başına yorgunluk (eski 0,0011).</summary>
        public const float CalismaYorgunlugu = 0.0013f;
        /// <summary>Her teslimatta (paketi taşıma, kapı, merdiven) eklenen yorgunluk.</summary>
        public const float TeslimatYorgunlugu = 0.004f;
        /// <summary>Günde bundan fazla çevrimiçi kalan kuryede yorgunluk birikir (saat).</summary>
        public const float BirikmeSaatEsigi = 6f;
        /// <summary>Eşiğin üstündeki her saat için birikme.</summary>
        public const float BirikmeSaatBasi = 0.035f;
        /// <summary>Hiç çalışmadığı gün birikmiş yorgunluk bu kadar azalır.</summary>
        public const float IzinGunuDinlenme = 0.35f;
        /// <summary>Her gece kendiliğinden azalma.</summary>
        public const float GeceDinlenme = 0.04f;
        /// <summary>Birikmiş yorgunluk bunu geçince kurye kendine izin günü yapar.</summary>
        public const float ZorunluIzinSiniri = 0.75f;

        // ------------------------------------------------------------------ moral

        /// <summary>Kuryenin makul bulduğu saatlik kazanç (Ft, ~10 €). Altında kalınca moral düşer.</summary>
        public const float BeklenenSaatlikKazanc = 4000f;
        /// <summary>Moral her gece hedefine bu oranda yaklaşır (0,25 = farkın dörtte biri).</summary>
        public const float MoralYaklasma = 0.25f;
        /// <summary>Moral bunun altındayken her gün (sinir − moral) × oran ihtimalle bırakır.</summary>
        public const float BirakmaMoralSiniri = 0.3f;
        public const float BirakmaOrani = 0.5f;

        // ------------------------------------------------------------------ kartlar

        /// <summary>Grev kartı ancak ortalama moral bunun altındayken gelir.</summary>
        public const float GrevMoralSiniri = 0.5f;
        /// <summary>Paket başı zam talebi ancak ortalama moral bunun altındayken gelir.</summary>
        public const float ZamMoralSiniri = 0.6f;
    }
}
