using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    public enum TehditTur { Kurye = 0, Dukkan = 1, FiyatSavasi = 2 }

    /// <summary>Önceden duyurulan bir rakip hamlesi (kayda yazılır).</summary>
    [System.Serializable]
    public class RakipTehdit
    {
        public int tur;                 // TehditTur
        public int rakip;               // Rakipler listesindeki sıra
        public int gun;                 // gerçekleşeceği gün
        public int ilanGunu;
        public int dukkan = -1;         // dükkan tehdidi: CityMap.Shops sırası
        public int bolge;               // fiyat savaşı: bölge (1–5)
        public List<string> kuryeler = new List<string>();   // kurye tehdidi: hedeflenen kuryelerin adları

        public TehditTur Tur => (TehditTur)tur;
    }

    /// <summary>
    /// Rakiplerin pazar payına göre sertleşmesi ve hamlelerinin önceden duyurulması.
    ///
    /// Tehdit seviyesi takvime değil pazar payına bağlı (3 rakipte):
    ///  • 0 sakin (%35 altı): hamleler seyrek.
    ///  • 1 tetikte (%35–50): saldırganlık tabanı yükselir, yatırım ararlar, kurye teklifleri 4 haftada bir.
    ///  • 2 saldırıda (%50+): haftada iki hamle olabilir, yatırım çok daha olası, en değerli (eğitimli) kuryelere %30 fazla teklif
    ///    (3 haftada bir), münhasır dükkanlara bile teklif, fiyat savaşı en çok sipariş aldığın bölgede ve 10 gün.
    /// Dükkan, kurye ve fiyat savaşı hamleleri birkaç gün önceden duyurulur (bildirim + sağ üstte durum kartı); oyuncu o arada
    /// dükkana münhasırlık önerebilir, memnuniyetini, kuryelerin moralini yükseltebilir, para ayırabilir. Vadesinde karar kartı gelir.
    /// </summary>
    public partial class RivalCompany
    {
        public readonly List<RakipTehdit> Tehditler = new List<RakipTehdit>();
        int sonTehditSeviyesi;

        /// <summary>Pazar payının tehdit eşikleri (3 rakipte %35 ve %50).</summary>
        public float TehditEsigi1 => SaldirganlikEsigi;
        public float TehditEsigi2 => SaldirganlikEsigi + 0.15f;

        /// <summary>0 sakin, 1 tetikte, 2 saldırıda (geçen haftanın pazar payına göre; ilk hafta 0).</summary>
        public int TehditSeviyesi
        {
            get
            {
                if (myDeliveriesLastWeek + deliveriesLastWeek == 0 || AktifRakip == 0) return 0;
                float p = MarketShare;
                return p >= TehditEsigi2 ? 2 : p >= TehditEsigi1 ? 1 : 0;
            }
        }

        public static readonly string[] SeviyeAdlari = { "sakin", "tetikte", "saldırıda" };

        /// <summary>Haftalık: seviye değiştiyse oyuncuya söylenir.</summary>
        void TehditSeviyesiniDuyur(int seviye)
        {
            if (seviye == sonTehditSeviyesi) return;
            int onceki = sonTehditSeviyesi;
            sonTehditSeviyesi = seviye;
            string pay = $"%{MarketShare * 100f:0}";
            if (seviye > onceki)
            {
                var ev = seviye >= 2
                    ? new GameEvent("Rakipler saldırıya geçti",
                        $"Pazar payın {pay} oldu, %{TehditEsigi2 * 100f:0}'i geçti. Rakipler artık her hafta hamle yapabilir: yatırım arayacaklar, " +
                        "en değerli (eğitimli, kıdemli) kuryelerine %30 fazla ücret teklif edecekler, münhasır dükkanlarına bile gelecekler, " +
                        "fiyat savaşını en çok sipariş aldığın bölgede açacaklar.\n\n" +
                        "Hamleler birkaç gün önceden sağ üstte görünür: o arada dükkana münhasırlık öner, memnuniyetini yükselt, kuryelerin moralini koru.")
                    : new GameEvent("Rakipler tetikte",
                        $"Pazar payın {pay} oldu, %{TehditEsigi1 * 100f:0}'i geçti. Rakipler seni ciddiye almaya başladı: daha sık hamle yapacak, " +
                        $"yatırımcı arayacak, kuryelerine ve dükkanlarına teklif verecekler. Pay %{TehditEsigi2 * 100f:0}'i geçerse saldırıya geçerler.\n\n" +
                        "Hamleler birkaç gün önceden sağ üstte görünür.");
                ev.Add("Tamam", null);
                EventManager.I.Push(ev);
            }
            else
                EventManager.I.Notify(seviye == 0
                    ? $"Pazar payın {pay}: rakipler sakinleşti."
                    : $"Pazar payın {pay}: rakipler saldırıyı bıraktı ama hâlâ tetikte.");
        }

        bool HedefteMi(Shop s)
        {
            int idx = CityMap.I.Shops.IndexOf(s);
            foreach (var t in Tehditler) if (t.Tur == TehditTur.Dukkan && t.dukkan == idx) return true;
            return false;
        }

        bool HedefteMi(Courier c)
        {
            foreach (var t in Tehditler) if (t.Tur == TehditTur.Kurye && t.kuryeler.Contains(c.courierName)) return true;
            return false;
        }

        /// <summary>Bir hamleyi hazırlar ve duyurur. Hedef yoksa false (başka hamle seçilir).</summary>
        bool TehditHazirla(int ri, TehditTur tur)
        {
            var r = Rakipler[ri];
            foreach (var t in Tehditler) if (t.rakip == ri && t.Tur == tur) return false;   // aynı rakibin aynı hamlesi zaten yolda
            var th = new RakipTehdit { tur = (int)tur, rakip = ri, ilanGunu = Day };
            switch (tur)
            {
                case TehditTur.Dukkan:
                {
                    var s = DukkanHedefi();
                    if (s == null) return false;
                    th.dukkan = CityMap.I.Shops.IndexOf(s);
                    th.gun = Day + 3;
                    EventManager.I.Notify($"{r.ad}, {s.Name} dükkanına teklif hazırlıyor (3 gün içinde" +
                                          (s.exclusive ? ", münhasır olsa da" : "") + $"). Memnuniyet {s.satisfaction:0}/100" +
                                          (s.exclusive ? "." : ": münhasırlık önerirsen rakip geri çekilir."));
                    break;
                }
                case TehditTur.Kurye:
                {
                    if (CrisisManager.Employees) return false;
                    var hedef = KuryeHedefleri(r);
                    if (hedef == null) return false;
                    foreach (var c in hedef) th.kuryeler.Add(c.courierName);
                    r.lastCourierRaidDay = Day;
                    th.gun = Day + 3;
                    EventManager.I.Notify($"{r.ad}, en değerli {hedef.Count} kuryene teklif hazırlıyor (3 gün içinde): " +
                                          $"{string.Join(", ", th.kuryeler)}. Morali yüksek kurye kalır.");
                    break;
                }
                case TehditTur.FiyatSavasi:
                {
                    if (priceWarDays > 0) return false;
                    foreach (var t in Tehditler) if (t.Tur == TehditTur.FiyatSavasi) return false;   // bir seferde bir savaş
                    th.bolge = SavasBolgesi();
                    th.gun = Day + 2;
                    EventManager.I.Notify($"{r.ad}, {CityMap.DistrictNames[th.bolge]} bölgesinde fiyat savaşına hazırlanıyor (2 gün sonra).");
                    break;
                }
            }
            Tehditler.Add(th);
            Telemetry.Add("Rakip: hamle duyuruldu");
            return true;
        }

        /// <summary>Her gece: vadesi gelen hamleler gerçekleşir (karar kartı).</summary>
        void TehditleriIsle()
        {
            for (int i = Tehditler.Count - 1; i >= 0; i--)
            {
                var t = Tehditler[i];
                if (Day < t.gun) continue;
                Tehditler.RemoveAt(i);
                var r = RakipAt(t.rakip);
                if (r == null || r.satinAlindi) continue;   // platformu satın aldın: hamle yok
                switch (t.Tur)
                {
                    case TehditTur.Dukkan:
                        var shops = CityMap.I.Shops;
                        if (t.dukkan >= 0 && t.dukkan < shops.Count) ShopRaidOn(t.rakip, shops[t.dukkan]);
                        break;
                    case TehditTur.Kurye:
                        var hedef = new List<Courier>();
                        foreach (var c in CourierManager.I.Couriers) if (t.kuryeler.Contains(c.courierName)) hedef.Add(c);
                        CourierRaidOn(t.rakip, hedef);
                        break;
                    case TehditTur.FiyatSavasi:
                        if (priceWarDays == 0) PriceWarOn(t.rakip, t.bolge);
                        break;
                }
            }
        }

        /// <summary>Tehdidin hedef dükkanı (dükkan tehdidi değilse null).</summary>
        public Shop TehditDukkani(RakipTehdit t)
        {
            var shops = CityMap.I != null ? CityMap.I.Shops : null;
            return t.Tur == TehditTur.Dukkan && shops != null && t.dukkan >= 0 && t.dukkan < shops.Count ? shops[t.dukkan] : null;
        }

        /// <summary>Saldırıdayken münhasır dükkana teklif: münhasırlığı bozmak için çok daha düşük komisyon.</summary>
        void MunhasirDukkanTeklifi(int ri, Shop shop)
        {
            var r = Rakipler[ri];
            float offer = Mathf.Max(0.05f, shop.Commission - Mathf.Lerp(0.05f, 0.07f, r.fiyatEgilimi));
            // Münhasır dükkan daha sadık: memnuniyet 80'de ~%83 kalır
            float keep = Mathf.Clamp(0.35f + shop.satisfaction / 100f * 0.6f, 0.35f, 0.97f);
            Telemetry.Add("Rakip: münhasır dükkan teklifi");
            var ev = new GameEvent($"{r.ad}, münhasır {shop.Name} dükkanına göz dikti",
                $"{r.ad} ({r.Tanim}), münhasırlığı bozması için {shop.Name} dükkanına {Pct(offer)} komisyon teklif etti " +
                $"(senin oranın {Pct(shop.Commission)}, memnuniyet {shop.satisfaction:0}/100, geçen ay {shop.lastMonthOrders} sipariş).");
            ev.Add($"Karşı teklif ver (→ {Pct(shop.Commission - 0.02f)}, münhasırlık sürer, sözleşme 6 ay uzar, memnuniyet +6)", () =>
            {
                shop.commissionBonus -= 0.02f; Renew(shop); AddSatisfaction(shop, 6f);
            });
            ev.Add($"Bir şey yapma (kalma ihtimali %{keep * 100f:0})", () =>
            {
                if (Random.value < keep) EventManager.I.Notify($"{shop.Name} {TurkceEk.In(r.ad)} teklifini reddetti, münhasırlık sürüyor.");
                else LoseShop(shop, $"münhasırlığı bozup {TurkceEk.In(r.ad)} teklifini kabul etti", ri);
            });
            EventManager.I.Push(ev);
        }
    }
}
