using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Analitik sekmesi (Şirket > Analitik; UI Toolkit). Son 7 günün saat ve bölge bazında verisi (SaatAnalizi):
    ///  • Özet kutuları: kaçan oranı, en çok kaçıran saat dilimi ve bölge, en büyük kurye açığı saati.
    ///  • Bölge × saat dilimi tablosu: kaçan sipariş oranı (renkli) ve hacim; ortalama teslim süresi.
    ///  • Saatlik grafikler: gelen sipariş, kaçan sipariş, kurye açığı (bekleyen sipariş − boşta kurye).
    ///  • Öneriler: veriye bakıp mikro hub, planlı teslimat, kurye alımı, kargo / işletme teslimatı gibi somut adımlar.
    /// Harita katmanlarıyla (H tuşu) aynı veriden; tablo "ne zaman", katman "nerede" sorusunu cevaplar.
    /// Veri saat başı değişir: sekme oyun saati değişmediyse yeniden çizilmez.
    /// </summary>
    public partial class GameUI
    {
        class UiAnalitik : UiSekme
        {
            Yazici y;
            int sonSaat = -1;

            static readonly int[] DilimBas = { 8, 11, 14, 17, 20, 23 };
            static readonly int[] DilimBit = { 11, 14, 17, 20, 23, 32 };   // 23–08 (ertesi sabah)
            static readonly string[] DilimAd = { "08–11", "11–14", "14–17", "17–20", "20–23", "Gece" };

            protected override void Kur() { y = new Yazici(ui, kok); }

            public override void Acildi() { sonSaat = -1; }

            public override void Yenile()
            {
                if (GameClock.I == null || CityMap.I == null) return;
                int anahtar = GameClock.I.Day * 24 + GameClock.I.Hour;
                if (anahtar == sonSaat) return;
                sonSaat = anahtar;
                var o = SaatAnalizi.Topla();
                y.Basla();
                y.Yazi("Ne zaman ve nerede sipariş kaçırıyorsun, hangi saatte kurye eksik: son 7 günün verisi.");
                y.Yardim("Her sipariş geldiği saat ve müşterinin bölgesiyle kaydedilir. Kaçan sipariş: kurye bulunamadığı, ücret pahalı geldiği, " +
                         "yoğunluk ya da ağırlık yüzünden gelmeyen ya da iptal olan sipariş. Kurye açığı 10 dakikada bir ölçülür: kurye bekleyen " +
                         "sipariş sayısından boşta bekleyen kurye sayısı çıkarılır (artı: kurye eksik, eksi: kurye boşta). Haritada yerini görmek için " +
                         "Harita katmanı > Kaçan siparişler.");
                if (o.gunSayisi == 0 || o.toplamSiparis + o.toplamKacan == 0)
                {
                    y.Kucuk("Veri birkaç saatlik oyundan sonra görünür.");
                    y.Bitir();
                    return;
                }

                // ---------------------------------------------------------------- özet
                int acik = Mathf.Clamp(CityMap.I.UnlockLevel, 1, SaatAnalizi.Bolge - 1);
                float genel = o.toplamKacan / (float)Mathf.Max(1, o.toplamSiparis + o.toplamKacan);
                int enKotuD = -1, enKotuB = -1; float enKotuO = 0f;
                for (int b = 1; b <= acik; b++)
                    for (int d = 0; d < DilimAd.Length; d++)
                    {
                        if (o.Hacim(b, DilimBas[d], DilimBit[d]) < 15) continue;   // az veri: yanıltmasın
                        float r = o.KacanOrani(b, DilimBas[d], DilimBit[d]);
                        if (r > enKotuO) { enKotuO = r; enKotuD = d; enKotuB = b; }
                    }
                int acikSaat = -1; float enAcik = 0f;
                for (int h = 0; h < SaatAnalizi.Saat; h++)
                {
                    float a = o.ortBekleyen[h] - o.ortBosta[h];
                    if (a > enAcik) { enAcik = a; acikSaat = h; }
                }
                int bostaSaat = -1; float enBosta = 0f;
                for (int h = 9; h < 21; h++)
                {
                    float a = o.ortBosta[h] - o.ortBekleyen[h];
                    if (a > enBosta) { enBosta = a; bostaSaat = h; }
                }

                y.KutularBasla();
                y.Kutu("Kaçan sipariş", $"%{genel * 100f:0.#}", $"{o.toplamKacan} / {o.toplamSiparis + o.toplamKacan} · {o.gunSayisi} gün",
                       genel > 0.1f ? U.Kotu : genel > 0.05f ? U.Uyari : U.Iyi);
                y.Kutu("En çok kaçıran", enKotuD >= 0 ? $"%{enKotuO * 100f:0}" : "—",
                       enKotuD >= 0 ? $"{CityMap.DistrictNames[enKotuB]} · {DilimAd[enKotuD]}" : "yeterli veri yok", enKotuO > 0.15f ? U.Kotu : (Color?)null);
                y.Kutu("En büyük kurye açığı", acikSaat >= 0 ? $"{enAcik:0.#} sipariş" : "yok",
                       acikSaat >= 0 ? $"{acikSaat:00}:00–{(acikSaat + 1) % 24:00}:00 arası ortalama" : "bekleyen sipariş hep karşılanıyor", enAcik > 3f ? U.Uyari : (Color?)null);
                y.Kutu("En boş saat", bostaSaat >= 0 ? $"{enBosta:0.#} kurye" : "yok",
                       bostaSaat >= 0 ? $"{bostaSaat:00}:00 civarı boşta bekliyor" : "gündüz boşta kurye yok");
                y.KutularBitir();

                // ---------------------------------------------------------------- bölge × saat dilimi
                y.Bolum("Bölge ve saate göre kaçan sipariş", "Hücrede kaçan sipariş oranı ve parantez içinde o dilimde gelen + kaçan sipariş sayısı (7 gün). " +
                        "Kırmızı %15+, sarı %7+. Son satır bütün şehrin ortalama teslim süresi.");
                var bas = new List<string> { "Bölge" };
                bas.AddRange(DilimAd);
                y.Tablo(bas, true);
                for (int b = 1; b <= acik; b++)
                {
                    var h = new List<string> { CityMap.DistrictNames[b] };
                    var r = new List<Color?> { null };
                    for (int d = 0; d < DilimAd.Length; d++)
                    {
                        int n = o.Hacim(b, DilimBas[d], DilimBit[d]);
                        float k = o.KacanOrani(b, DilimBas[d], DilimBit[d]);
                        h.Add(n == 0 ? "—" : $"%{k * 100f:0} ({n})");
                        r.Add(n < 8 ? U.Soluk : k >= 0.15f ? U.Kotu : k >= 0.07f ? U.Uyari : U.Iyi);
                    }
                    y.Tablo(h, false, null, r);
                }
                var sure = new List<string> { "Teslim süresi" };
                var sr = new List<Color?> { null };
                for (int d = 0; d < DilimAd.Length; d++)
                {
                    int n = 0; float t = 0f;
                    for (int b = 1; b <= acik; b++)
                        for (int hh = DilimBas[d]; hh < DilimBit[d]; hh++) { n += o.teslimBS[b, hh % 24]; t += o.dakikaBS[b, hh % 24]; }
                    float ort = n > 0 ? t / n : 0f;
                    sure.Add(n > 0 ? $"{ort:0} dk" : "—");
                    sr.Add(n == 0 ? U.Soluk : ort > 45f ? U.Kotu : ort > 35f ? U.Uyari : (Color?)null);
                }
                y.Tablo(sure, false, null, sr);

                // ---------------------------------------------------------------- saatlik grafikler
                var etiket = new List<string>();
                for (int h = 0; h < 24; h++) etiket.Add($"{h:00}:00");
                var gelen = new List<float>(); var kacan = new List<float>(); var acikL = new List<float>();
                float gun = Mathf.Max(1, o.gunSayisi);
                for (int h = 0; h < 24; h++)
                {
                    gelen.Add(o.siparisSaat[h] / gun);
                    kacan.Add(o.kacanSaat[h] / gun);
                    acikL.Add(o.ortBekleyen[h] - o.ortBosta[h]);
                }
                System.Func<float, string> sayi = v => v.ToString("0.#");
                y.Ozel("an_gelen", () => new GrafikKutu()).Ayarla("Saatlik gelen sipariş (günlük ortalama)", gelen, etiket, true, new Color(0.4f, 0.75f, 1f), U.Kotu, sayi);
                y.Ozel("an_kacan", () => new GrafikKutu()).Ayarla("Saatlik kaçan sipariş (günlük ortalama)", kacan, etiket, true, U.Kotu, U.Kotu, sayi);
                y.Ozel("an_acik", () => new GrafikKutu()).Ayarla("Kurye açığı: bekleyen sipariş − boşta kurye (artı: kurye eksik)", acikL, etiket, true,
                                                                   new Color(1f, 0.65f, 0.3f), new Color(0.45f, 0.85f, 0.55f), sayi);

                // ---------------------------------------------------------------- öneriler
                y.Bolum("Öneriler", "Verinin işaret ettiği en somut adımlar (en fazla dört).");
                var oneri = Oneriler(o, acik, enKotuB, enKotuD, enKotuO, acikSaat, enAcik, bostaSaat, enBosta);
                if (oneri.Count == 0) y.Kucuk("Şu an belirgin bir darboğaz görünmüyor: kaçan oranı düşük, kurye sayısı saatlere yetiyor.");
                foreach (var m in oneri) y.Noktali(m, U.Uyari, "kucuk");
                y.Bosluk(4);
                y.Dugme("Haritada kaçan siparişleri göster", () => { ui.SetHeat(HeatMode.Kacan); ui.panelOpen = false; }, true, "kucuk-dg");
                y.Bitir();
            }

            static List<string> Oneriler(SaatAnalizi.Ozet o, int acik, int kotuB, int kotuD, float kotuO, int acikSaat, float enAcik, int bostaSaat, float enBosta)
            {
                var l = new List<string>();
                var up = UpgradeManager.I;
                if (kotuD >= 0 && kotuO >= 0.12f)
                {
                    float sure = o.OrtDakika(kotuB, DilimBas[kotuD], DilimBit[kotuD]);
                    bool uzak = kotuB >= 3;
                    l.Add($"{CityMap.DistrictNames[kotuB]} {DilimAd[kotuD]} arası siparişlerin %{kotuO * 100f:0}'i kaçıyor" +
                          (sure > 0f ? $" (ortalama teslim {sure:0} dk)" : "") + ". " +
                          (uzak && up != null && up.Level(UpgradeKind.Hub) == 0 ? "Merkeze uzak: mikro hub kuryeleri oraya yakın bekletir." :
                           "Bu saatte o bölgeye kurye yönlendir ya da yoğunluk primiyle daha çok kuryeyi çevrimiçi yap."));
                }
                if (acikSaat >= 0 && enAcik >= 2f)
                    l.Add($"{acikSaat:00}:00 civarı ortalama {enAcik:0.#} sipariş kurye bekliyor: o saatte çalışan kurye sayısını artır " +
                          "(yeni kurye, vardiya, yoğunluk primi) ya da müşteriye planlı teslimat sun; planlı sipariş yoğunluğu sakin saate kaydırır.");
                if (bostaSaat >= 0 && enBosta >= 3f)
                {
                    bool kargo = up != null && up.Level(UpgradeKind.Kargo) > 0, isl = up != null && up.Level(UpgradeKind.Isletme) > 0;
                    l.Add($"{bostaSaat:00}:00 civarı ortalama {enBosta:0.#} kurye boşta bekliyor. " +
                          (!isl ? "İşletmelere teslimat (Gelişim > Yeni iş kolları) bu saatlere paket getirir." :
                           !kargo ? "Kargo teslimatı sakin saatleri doldurur." : "Bu saatlerde kampanya ya da yeni dükkan anlaşması talep getirir."));
                }
                float gece = 0f; int geceN = 0;
                for (int b = 1; b <= acik; b++) { gece += o.KacanOrani(b, 23, 32) * o.Hacim(b, 23, 32); geceN += o.Hacim(b, 23, 32); }
                if (geceN >= 20 && gece / geceN >= 0.2f && l.Count < 4)
                    l.Add($"Gece (23–08) siparişlerin %{gece / geceN * 100f:0}'i kaçıyor: gece kuşu kuryeler ya da gece primi bu saatleri kurtarır.");
                if (l.Count > 4) l.RemoveRange(4, l.Count - 4);
                return l;
            }
        }
    }
}
