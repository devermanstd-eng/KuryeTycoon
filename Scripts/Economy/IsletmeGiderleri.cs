using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Geç oyun işletme giderleri: şirket büyüdükçe (kurye sayısı, açık bölge, kurye ödemeleri) kademeli artan sabit giderler.
    /// Amaç: kredi bittikten sonra kasanın bedavaya şişmesini önlemek, kâr marjını daraltmak. 14.11 testinde 60. günde günlük gelir
    /// ~4 000 €, sabit giderler (geliştirmeler) ~90 €'ydu; kasa 60 günde 2 450 €'dan 43 000 €'ya çıktı.
    /// Küçük şirkete (10 kurye ve tek bölgeye kadar) sıfır.
    ///  • Bölge operasyonu: her yeni bölge için saha ekibi, teslim noktası, depo (2. bölge 25 €, 3. 50 €, 4. 85 €, 5. 130 € / gün).
    ///  • Filo sorumluluk sigortası: 11–20. kurye için kişi başı 2 €, 21–35. için 3,5 €, 36. ve sonrası için 5 € / gün.
    ///  • Yönetim ve İK: 15. kuryeden sonra her ek kurye 4 € × (1 + ek/30) / gün (koordinasyon büyüdükçe pahalanır).
    ///  • Kurye sosyal katkısı (platform çalışanları yasası): 21–35 kuryede günün kurye ödemelerinin %3'ü, 36+ kuryede %6'sı.
    /// Ölüm sarmalı önlemi: toplam, son 7 günün ortalama teslimat kârının %35'ini geçemez; fiyat savaşı sürerken %20'sini,
    /// son 7 gün zarardaysa %15'ini. Kesinti kalemlere orantılı dağıtılır.
    /// Her gece EconomyManager.OnNewDay'de kesilir ("İşletme giderleri" kategorisi); Finans > Özet'te kalem kalem görünür.
    /// </summary>
    public static class IsletmeGiderleri
    {
        public struct Kalem
        {
            public string ad, aciklama;
            public long tam, kesilen;
        }

        static readonly float[] BolgeEuro = { 0f, 0f, 25f, 50f, 85f, 130f };
        const float Euro = GameData.FtPerEuro;
        public const float NormalTavan = 0.35f, SavasTavani = 0.2f, ZararTavani = 0.15f;

        public static long BolgeOperasyonu(int bolge)
        {
            float t = 0f;
            for (int d = 2; d <= Mathf.Min(bolge, BolgeEuro.Length - 1); d++) t += BolgeEuro[d];
            return (long)(t * Euro);
        }

        public static long FiloSigortasi(int n)
        {
            float t = Mathf.Clamp(n - 10, 0, 10) * 2f + Mathf.Clamp(n - 20, 0, 15) * 3.5f + Mathf.Max(0, n - 35) * 5f;
            return (long)(t * Euro);
        }

        public static long Yonetim(int n)
        {
            int ek = Mathf.Max(0, n - 15);
            return (long)(ek * 4f * (1f + ek / 30f) * Euro);
        }

        public static float KatkiOrani(int n) => n > 35 ? 0.06f : n > 20 ? 0.03f : 0f;

        /// <summary>Son n günün ortalaması (StatsHistory); kayıt yoksa 0.</summary>
        static long Ortalama(int n, System.Func<DayRecord, long> f, out int sayi)
        {
            sayi = 0;
            var d = StatsHistory.I != null ? StatsHistory.I.Days : null;
            if (d == null || d.Count == 0) return 0;
            int k = Mathf.Min(n, d.Count);
            long t = 0;
            for (int i = d.Count - k; i < d.Count; i++) t += f(d[i]);
            sayi = k;
            return t / k;
        }

        /// <summary>Tavan oranı: normalde %35, fiyat savaşında %20, son 7 gün zarardaysa %15.</summary>
        public static float TavanOrani(out string neden)
        {
            var rv = RivalCompany.I;
            long net = Ortalama(7, r => r.net, out int sayi);
            if (sayi >= 3 && net < 0) { neden = "son 7 gün zararda"; return ZararTavani; }
            if (rv != null && rv.priceWarDays > 0) { neden = "fiyat savaşı sürüyor"; return SavasTavani; }
            neden = "";
            return NormalTavan;
        }

        /// <summary>Kesilebilecek en fazla tutar: son 7 günün ortalama teslimat kârı × tavan oranı (yedek: bugünkü teslimat kârı).</summary>
        public static long Tavan(long bugunkuTeslimatKari, out float oran, out string neden)
        {
            oran = TavanOrani(out neden);
            long ort = Ortalama(7, r => r.deliveryProfit, out int sayi);
            if (sayi < 3) ort = bugunkuTeslimatKari;
            return (long)(System.Math.Max(0L, ort) * oran);
        }

        /// <summary>Kalemleri hesaplar (kuryeOdemesi: o günün kurye ödemeleri; teslimatKari: o günün teslimat kârı, kayıt azsa tavan için).</summary>
        public static List<Kalem> Hesapla(long kuryeOdemesi, long teslimatKari, out long tamToplam, out long kesilenToplam, out long tavan,
                                          out float tavanOrani, out string tavanNedeni)
        {
            int n = CourierManager.I != null ? CourierManager.I.Couriers.Count : 0;
            int bolge = CityMap.I != null ? CityMap.I.UnlockLevel : 1;
            var l = new List<Kalem>();
            void Ekle(string ad, string aciklama, long tutar) { if (tutar > 0) l.Add(new Kalem { ad = ad, aciklama = aciklama, tam = tutar }); }

            Ekle("Bölge operasyonu", $"{bolge} açık bölge: her yeni bölgede saha ekibi, teslim noktası ve depo kirası " +
                                     "(2. bölge 25 €, 3. 50 €, 4. 85 €, 5. 130 € / gün).", BolgeOperasyonu(bolge));
            Ekle("Filo sorumluluk sigortası", $"{n} kurye: 11–20. kurye için kişi başı 2 €, 21–35. için 3,5 €, 36+ için 5 € / gün.", FiloSigortasi(n));
            Ekle("Yönetim ve İK", $"{n} kurye: 15. kuryeden sonra her ek kurye 4 € × (1 + ek/30) / gün; ekip büyüdükçe koordinasyon pahalanır.", Yonetim(n));
            float oran = KatkiOrani(n);
            Ekle("Kurye sosyal katkısı", $"{n} kurye: platform çalışanları yasası gereği kurye ödemelerinin %{oran * 100f:0}'i " +
                                         "(21–35 kuryede %3, 36+ kuryede %6).", (long)(kuryeOdemesi * oran));

            tamToplam = 0;
            foreach (var k in l) tamToplam += k.tam;
            tavan = Tavan(teslimatKari, out tavanOrani, out tavanNedeni);
            kesilenToplam = 0;
            float kesir = tamToplam > tavan && tamToplam > 0 ? tavan / (float)tamToplam : 1f;
            for (int i = 0; i < l.Count; i++)
            {
                var k = l[i];
                k.kesilen = (long)Mathf.Round(k.tam * kesir / 10f) * 10;
                kesilenToplam += k.kesilen;
                l[i] = k;
            }
            return l;
        }

        static int sonKademe = -1;

        /// <summary>Yeni oyun ya da yükleme (Denge.Sifirla'dan): kademe bildirimi yeniden ölçülsün.</summary>
        public static void Sifirla() { sonKademe = -1; }

        /// <summary>Şirketin gider kademesi: hangi kalemlerin başladığı (bildirim için).</summary>
        static int Kademe()
        {
            int n = CourierManager.I != null ? CourierManager.I.Couriers.Count : 0;
            int bolge = CityMap.I != null ? CityMap.I.UnlockLevel : 1;
            return (bolge >= 2 ? 1 : 0) + (n > 10 ? 1 : 0) + (n > 15 ? 1 : 0) + (n > 20 ? 1 : 0) + (n > 35 ? 1 : 0);
        }

        /// <summary>Her gece (EconomyManager.OnNewDay): günün giderini keser, yeni kademede oyuncuya söyler.</summary>
        public static void GunlukKes(EconomyManager eco, PeriodStats bugun)
        {
            var kalemler = Hesapla(bugun.courierPay, bugun.DeliveryProfit, out long tam, out long kesilen, out _, out float oran, out string neden);
            foreach (var k in kalemler)
                if (k.kesilen > 0) eco.Spend(k.kesilen, "İşletme: " + k.ad, ExpenseCategory.Isletme);

            int kademe = Kademe();
            if (sonKademe >= 0 && kademe > sonKademe && kesilen > 0)
                EventManager.I.Notify($"Şirket büyüdü, işletme giderleri arttı: günde {GameData.Ft(kesilen)}" +
                                      (kesilen < tam ? $" (tamamı {GameData.Ft(tam)}; tavan: teslimat kârının %{oran * 100f:0}'i{(neden.Length > 0 ? ", " + neden : "")})" : "") +
                                      ". Kalemler: Finans > Özet.");
            sonKademe = kademe;
        }
    }
}
