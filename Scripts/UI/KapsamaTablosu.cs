using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Haftalık kurye kapsaması: haftanın her günü ve gün bölümü (Sabah/Öğlen/Akşam/Gece) için kaç kuryenin çalıştığı ve
    /// o saatlerde ne kadar sipariş beklendiği. Kayıtlar ekranının başında tablo olarak, eksik saatler sağ üstteki
    /// Dikkat listesinde de görünür.
    ///  • Kurye sayısı: kuryenin oyunda çevrimiçi olma kararını verdiği Courier.PreferenceAt'ten (çalışma saatleriyle aynı);
    ///    o bölümde en az WorkPartMinHours saat kesin çalışan kurye sayılır. Gece bölümü ertesi günün 06:00'sına kadar.
    ///  • Talep: bölümün en yoğun saatinde anlaşmalı dükkanların OrderManager.DemandCurve ortalaması (yoksa açık bölgedeki
    ///    bütün dükkanlar). Ortalama değil en yoğun saat: gece bölümü 8 saat, 22–23 arası siparişler ortalamada kayboluyordu.
    ///  • Gerekli kurye: yoğun saatte filonun %40'ı (en az 2), orta saatte %20'si (en az 1), sakin saatte 0.
    ///    Hiç kurye yoksa "eksik" (kırmızı), gerekenden azsa "az" (sarı).
    /// Hesap günde bir ve kurye sayısı değişince yenilenir.
    /// </summary>
    public partial class GameUI
    {
        const float KapsamaYogun = 1.0f;   // bu ve üstü talep: yoğun
        const float KapsamaOrta = 0.4f;    // bu ve üstü: orta, altı sakin

        public enum KapsamaDurum { Sakin, Tamam, Az, Eksik }

        class KapsamaVerisi
        {
            public readonly int[,] kurye = new int[7, 4];
            public readonly float[,] talep = new float[7, 4];
            public readonly int[,] gerekli = new int[7, 4];
            public readonly KapsamaDurum[,] durum = new KapsamaDurum[7, 4];
        }

        KapsamaVerisi kapsamaVeri;
        int kapsamaGun = -1, kapsamaKurye = -1;

        KapsamaVerisi Kapsama()
        {
            var clock = GameClock.I;
            var cm = CourierManager.I;
            if (clock == null || cm == null || CityMap.I == null) return null;
            if (kapsamaVeri != null && kapsamaGun == clock.Day && kapsamaKurye == cm.Couriers.Count) return kapsamaVeri;
            kapsamaGun = clock.Day;
            kapsamaKurye = cm.Couriers.Count;

            var k = new KapsamaVerisi();
            var dukkanlar = new List<ShopDef>();
            foreach (var s in CityMap.I.Shops) if (s.contracted && !s.own) dukkanlar.Add(s.def);
            if (dukkanlar.Count == 0)
                foreach (var s in CityMap.I.Shops) if (CityMap.I.IsUnlocked(s.tile)) dukkanlar.Add(s.def);

            int filo = cm.Couriers.Count;
            for (int d = 0; d < 7; d++)
            {
                for (int p = 0; p < 4; p++)
                {
                    // Talep: bölümün en yoğun saati
                    float talep = 0f;
                    for (int h = WorkPartFrom[p]; h < WorkPartTo[p]; h++)
                    {
                        int gun = (d + h / 24) % 7, hh = h % 24;
                        float t = 0f;
                        foreach (var def in dukkanlar) t += OrderManager.DemandCurve(def, hh, gun >= 5);
                        if (dukkanlar.Count > 0) talep = Mathf.Max(talep, t / dukkanlar.Count);
                    }

                    // Kurye: bu bölümde en az WorkPartMinHours saat kesin çalışanlar
                    int n = 0;
                    foreach (var c in cm.Couriers)
                    {
                        int kesin = 0;
                        for (int h = WorkPartFrom[p]; h < WorkPartTo[p]; h++)
                            if (Courier.PreferenceAt(c.occupation, c.scheduleSeed, clock, (d + h / 24) % 7, h % 24) >= WorkSure) kesin++;
                        if (kesin >= WorkPartMinHours) n++;
                    }

                    int gerekli = talep >= KapsamaYogun ? Mathf.Max(2, Mathf.RoundToInt(filo * 0.4f))
                                : talep >= KapsamaOrta ? Mathf.Max(1, Mathf.RoundToInt(filo * 0.2f)) : 0;
                    k.kurye[d, p] = n;
                    k.talep[d, p] = talep;
                    k.gerekli[d, p] = gerekli;
                    k.durum[d, p] = gerekli == 0 ? KapsamaDurum.Sakin
                                  : n == 0 ? KapsamaDurum.Eksik
                                  : n < gerekli ? KapsamaDurum.Az : KapsamaDurum.Tamam;
                }
            }
            kapsamaVeri = k;
            return k;
        }

        static string TalepAdi(float t) => t >= KapsamaYogun ? "yoğun" : t >= KapsamaOrta ? "orta" : "sakin";

        /// <summary>Bu gün bölümünde eksik ya da az olan günler ("Pzt, Sal, Cum"); hiç yoksa boş.</summary>
        string KapsamaEksikGunler(KapsamaVerisi k, int p, bool sadeceEksik)
        {
            var gunler = new List<string>();
            for (int d = 0; d < 7; d++)
            {
                var du = k.durum[d, p];
                if (du == KapsamaDurum.Eksik || (!sadeceEksik && du == KapsamaDurum.Az)) gunler.Add(WorkDayNames[d]);
            }
            if (gunler.Count == 7) return "her gün";
            if (gunler.Count == 5 && gunler[0] == "Pzt" && gunler[4] == "Cum") return "hafta içi";
            return string.Join(", ", gunler);
        }

        static string KapsamaIpucu(int p)
        {
            switch (p)
            {
                case 2: return "Üniversite ilanı öğrenci getirir; öğrenciler çoğunlukla akşam çalışır.";
                case 3: return "Gece çalışan kurye seyrek kaydolur; yeni kurye bonusu kayıtları hızlandırır.";
                default: return "Kurye reklamı ve yeni kurye bonusu kayıtları hızlandırır.";
            }
        }

        /// <summary>Dikkat listesi için: bu hafta hiç kurye olmayan ama sipariş beklenen bölümler.</summary>
        void KapsamaUyarilari()
        {
            var k = Kapsama();
            if (k == null || CourierManager.I.Couriers.Count == 0) return;
            for (int p = 0; p < 4; p++)
            {
                string gunler = KapsamaEksikGunler(k, p, true);
                if (gunler.Length == 0) continue;
                uyarilar.Add(new Uyari
                {
                    metin = $"{WorkPartNames[p]} kurye yok: {gunler}",
                    renk = UITheme.Warn, yer = CityMap.I != null ? (Vector2)CityMap.I.HQTile : Vector2.zero, kayitlar = true, kapsamaBolum = p,
                    oncelik = 1.4f,
                });
            }
        }
    }
}
