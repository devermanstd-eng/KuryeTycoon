using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Akıllı botun yeni oynanış özelliklerini kullanması (her sabah 07:00):
    ///  • Yoğun saat primi: son günlerde akşam (18–21) ya da öğle (11–14) iptalleri birikiyorsa o saatin primini açar,
    ///    iptal kalmayınca ve moral iyiyse kapatır.
    ///  • Planlı teslimat: yoğunluktan kaçan sipariş varsa açar.
    ///  • Ekspres: itibar 3,9+ ve dünkü geç teslim %25'in altındaysa açar; ekspreslerin dörtte biri geç kalıyorsa kapatır.
    ///  • Öncelikli hazırlık: yemek dükkanlarında bekleme uzunsa en çok sipariş gelen, hazırlığı yavaş dükkanla anlaşır.
    ///  • Rakip bölge kaybı: kasa rahatsa en çok kaybedilen bölgede geri kazanma kampanyası.
    /// Kargo ve akademi geliştirmeleri AkilliGelistirme'nin aday listesinde.
    /// </summary>
    public partial class TestModu
    {
        void AkilliHizmet()
        {
            var clk = GameClock.I;
            var camp = CampaignManager.I;
            var hz = HizmetSeviyeleri.I;
            var son = SonGun();
            if (camp != null && clk.CompanyDay >= 3 && (Guide.I == null || Guide.I.IsOpen(Feature.Kampanya)))
            {
                float aksam = iptalProfili[18] + iptalProfili[19] + iptalProfili[20];
                float ogle = iptalProfili[11] + iptalProfili[12] + iptalProfili[13];
                if (!camp.aksamPrimi && aksam >= 2.5f)
                {
                    camp.aksamPrimi = true;
                    camp.aksamPrimiYuzde = 0.2f;
                    Eylem("Kampanya", $"Akşam primi açıldı (%20, 18–21) · neden: son günlerde akşam iptali ortalaması {aksam:0.0}/gün");
                }
                else if (camp.aksamPrimi && aksam < 0.4f && OrtMoral > 0.62f)
                {
                    camp.aksamPrimi = false;
                    Eylem("Kampanya", $"Akşam primi kapatıldı · neden: akşam iptali kalmadı ({aksam:0.0}/gün), moral iyi ({OrtMoral:0.00})");
                }
                if (!camp.oglePrimi && ogle >= 2.5f)
                {
                    camp.oglePrimi = true;
                    camp.oglePrimiYuzde = 0.15f;
                    Eylem("Kampanya", $"Öğle primi açıldı (%15, 11–14) · neden: son günlerde öğle iptali ortalaması {ogle:0.0}/gün");
                }
                else if (camp.oglePrimi && ogle < 0.4f && OrtMoral > 0.62f)
                {
                    camp.oglePrimi = false;
                    Eylem("Kampanya", $"Öğle primi kapatıldı · neden: öğle iptali kalmadı ({ogle:0.0}/gün)");
                }
            }

            if (hz != null)
            {
                if (!hz.planliAcik && YogunKayipOrani(3) > 0.02f)
                {
                    hz.planliAcik = true;
                    Eylem("Hizmet", $"Planlı teslimat açıldı · neden: son 3 günde siparişlerin %{YogunKayipOrani(3) * 100f:0}'i yoğunluktan kaçtı");
                }
                float gec = son != null && son.deliveries > 0 ? son.late / (float)son.deliveries : 1f;
                if (!hz.ekspresAcik && clk.CompanyDay >= 7 && CompanyProfile.Reputation >= 3.9f && gec < 0.25f)
                {
                    hz.ekspresAcik = true;
                    Eylem("Hizmet", $"Ekspres teslimat açıldı ({Ft(hz.ekspresUcret)}) · neden: itibar {CompanyProfile.Reputation:0.00}, dün geç %{gec * 100f:0}");
                }
                else if (hz.ekspresAcik && hz.ekspresDun >= 6 && hz.ekspresGecDun > hz.ekspresDun * 0.25f)
                {
                    hz.ekspresAcik = false;
                    Eylem("Hizmet", $"Ekspres kapatıldı · neden: dün {hz.ekspresDun} ekspresin {hz.ekspresGecDun}'i geç kaldı (iade)");
                }
            }

            // Öncelikli hazırlık: dükkanda bekleme uzunsa hazırlığı yavaş ve çok sipariş gelen yemek dükkanıyla anlaş
            if (son != null && son.avgPickupWait > 14f && CityMap.I != null)
            {
                Shop en = null;
                foreach (var s in CityMap.I.Shops)
                {
                    if (!s.contracted || s.own || s.oncelikli || s.def.isMarket || s.def.prepMinutes < 8f || !Mutfak.DegistirilebilirMi(s)) continue;
                    if (en == null || s.monthOrders + s.lastMonthOrders > en.monthOrders + en.lastMonthOrders) en = s;
                }
                if (en != null && en.monthOrders + en.lastMonthOrders >= 40)
                {
                    Mutfak.Degistir(en, true);
                    Eylem("Mutfak", $"{en.Name} ile öncelikli hazırlık anlaşması · neden: dükkanda bekleme {son.avgPickupWait:0.0} dk");
                }
            }

            // Gelişim ağacının küçük düğümleri: kasa rahatsa en ucuz açık düğüm (günde en fazla bir)
            if (!Biriktir)
            {
                AgacDugum en = null;
                foreach (var d in GelisimAgaci.Dugumler)
                    if (!d.buyuk && GelisimAgaci.KucukEngeli(d).Length == 0 && (en == null || d.bedel < en.bedel)) en = d;
                if (en != null && Bos > en.bedel * 5 && GelisimAgaci.KucukAl(en))
                    Eylem("Gelişim", $"ağaçta {en.ad} alındı ({Ft(en.bedel)}) · neden: kasa rahat, kalıcı bonus");
            }

            // Görmezden gelinen fiyat savaşının bölge kaybı: kasa rahatsa geri kazan
            var rv = RivalCompany.I;
            if (rv != null && !Biriktir)
            {
                int enKotu = -1;
                for (int b = 1; b < rv.bolgeSadakat.Length; b++)
                    if (rv.GeriKazanabilirMi(b) && rv.bolgeSadakat[b] < 0.93f && (enKotu < 0 || rv.bolgeSadakat[b] < rv.bolgeSadakat[enKotu])) enKotu = b;
                if (enKotu > 0 && Bos > rv.GeriKazanmaBedeli * 3)
                {
                    float once = rv.bolgeSadakat[enKotu];
                    rv.GeriKazan(enKotu);
                    Eylem("Rakip", $"{CityMap.DistrictNames[enKotu]} geri kazanma kampanyası ({Ft(rv.GeriKazanmaBedeli)}) · sadakat %{once * 100f:0} → %{rv.bolgeSadakat[enKotu] * 100f:0}");
                }
            }
        }
    }
}
