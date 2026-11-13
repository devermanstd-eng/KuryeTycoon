using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Otomatik testin "akıllı oyuncu" botu (Tur = Akilli). Gerçek bir oyuncu gibi:
    ///   • Karar kartını okur: seçeneklerdeki tutarı, itibar/moral etkisini ve şirketin durumunu tartıp seçer; nedenini yazar.
    ///   • Her sabah 07:00'de dünün raporuna bakar: iptallerin yoğun olduğu saatlere uygun kurye alır, servis yetiyorsa
    ///     en kârlı dükkanla anlaşır, dükkan kalmayınca bölge açar, ihtiyaca göre geliştirme seçer.
    ///   • Telefon kapanması, yağmur, kaza gibi olaylara göre ekipman teklif eder; memnun dükkanla pazarlık yapar.
    ///   • Kaçan sipariş çoksa teslimat ücretini düşürür, kuryeler teklif reddediyorsa bonusu artırır (haftada bir).
    ///   • Borcu vadesinden önce öder, kasa rahatsa borcun tamamını kapatır; sonra kendi marketini açar.
    /// Kampanya (boost) ayarlarına dokunmaz: varsayılanlar zaten açık.
    /// </summary>
    public partial class TestModu
    {
        // ------------------------------------------------------------------ gözlem (bildirimlerden ve gün sonlarından)

        int bugunTelefon, bugunAriza, bugunKaza, bugunAdres;
        int dunTelefon, dunAriza, dunKaza, dunAdres;
        readonly float[] iptalProfili = new float[24];   // son günlerin iptalleri saat saat (üstel ortalama)
        int sonProfilGun = -1, sonSabahGun = -1, sonHaftaGun = -1;
        int iptalRedDun, iptalRedBugun;

        void AkilliBildirim(string m)
        {
            if (m.Contains("telefonu kapandı")) bugunTelefon++;
            else if (m.Contains("arızalandı")) bugunAriza++;
            else if (m.Contains("kaza geçirdi")) bugunKaza++;
            else if (m.Contains("evde değil") || m.Contains("doğru adresi")) bugunAdres++;
        }

        /// <summary>Saatte bir (oyun saati). Sabah 07:00 planlama, 23:00 günün iptal profilini kaydetme; diğer saatlerde acil işler.</summary>
        void AkilliKararlar()
        {
            var clk = GameClock.I;
            if (clk.Hour == 23 && sonProfilGun != clk.Day)
            {
                sonProfilGun = clk.Day;
                var byHour = OrderManager.I.cancelledByHour;
                for (int h = 0; h < 24; h++) iptalProfili[h] = iptalProfili[h] * 0.6f + byHour[h] * 0.4f;
                iptalRedBugun = OrderManager.I.cancelledRejectedToday;
            }
            if (clk.Hour == 0 && sonSabahGun != clk.Day)
            {
                // gün değişti: dünün olay sayıları
                dunTelefon = bugunTelefon; dunAriza = bugunAriza; dunKaza = bugunKaza; dunAdres = bugunAdres;
                bugunTelefon = bugunAriza = bugunKaza = bugunAdres = 0;
                iptalRedDun = iptalRedBugun; iptalRedBugun = 0;
            }

            AkilliBorc();
            // Kurye açığı çok büyükse gün içinde de alır (oyuncu bildirimleri görüp İşe Alım'a bakar)
            if (clk.Hour >= 9 && clk.Hour <= 20) AkilliIseAlim(true);

            if (clk.Hour == 7 && sonSabahGun != clk.Day)
            {
                sonSabahGun = clk.Day;
                AkilliIseAlim(false);
                AkilliSozlesme();
                AkilliBolge();
                AkilliGelistirme();
                AkilliDestek();
                AkilliOperasyon();
                AkilliEkipman();
                AkilliDukkanBakimi();
                AkilliMarket();
                if (clk.DayOfWeek == 0 && sonHaftaGun != clk.Day) { sonHaftaGun = clk.Day; AkilliFiyat(); }
            }
        }

        // ------------------------------------------------------------------ yardımcılar

        /// <summary>Serbest kasadan sabit giderlere ve yaklaşan taksite ayrılan pay düşülünce harcanabilecek para.</summary>
        long Bos => EconomyManager.I.FreeCash - Rezerv();

        /// <summary>Son n günün ortalaması (DayRecord alanından).</summary>
        float Ort(int n, System.Func<DayRecord, float> f)
        {
            var d = StatsHistory.I != null ? StatsHistory.I.Days : null;
            if (d == null || d.Count == 0) return 0f;
            int k = Mathf.Min(n, d.Count);
            float t = 0f;
            for (int i = d.Count - k; i < d.Count; i++) t += f(d[i]);
            return t / k;
        }

        float IptalOrani(int n) => Ort(n, d => d.deliveries + d.cancelled > 0 ? d.cancelled / (float)(d.deliveries + d.cancelled) : 0f);
        long HaftalikKuryeOdemesi => (long)(Ort(7, d => d.courierPay) * 7f);
        long GunlukTeslimatKari => (long)Ort(7, d => d.deliveryProfit);
        float OrtMoral
        {
            get
            {
                var cs = CourierManager.I.Couriers;
                if (cs.Count == 0) return 1f;
                float t = 0f; foreach (var c in cs) t += c.morale;
                return t / cs.Count;
            }
        }

        static readonly CultureInfo Tr = CultureInfo.InvariantCulture;
        static readonly Regex TutarKalibi = new Regex(@"([−\-+])\s?(\d[\d   ]*(?:[.,]\d+)?)\s?(€|Ft)");
        static readonly Regex YuzdeKalibi = new Regex(@"%\s?(\d+)");

        /// <summary>Seçenek metnindeki tutarlar (forint), işaretiyle: "−75 €" → −30000. Gösterilen para birimi ne olursa olsun.</summary>
        static List<long> Tutarlar(string s)
        {
            var l = new List<long>();
            if (string.IsNullOrEmpty(s)) return l;
            foreach (Match m in TutarKalibi.Matches(s))
            {
                string sayi = m.Groups[2].Value.Replace(" ", "").Replace(" ", "").Replace(" ", "");
                double v;
                if (m.Groups[3].Value == "€")
                {
                    if (!double.TryParse(sayi.Replace(',', '.'), NumberStyles.Float, Tr, out v)) continue;
                    v *= GameData.FtPerEuro;
                }
                else if (!double.TryParse(sayi.Replace(".", "").Replace(",", ""), NumberStyles.Float, Tr, out v)) continue;
                l.Add((long)(m.Groups[1].Value == "+" ? v : -v));
            }
            return l;
        }

        /// <summary>Metindeki ilk gideri (pozitif) verir; yoksa 0.</summary>
        static long Gider(string s)
        {
            foreach (var t in Tutarlar(s)) if (t < 0) return -t;
            return 0;
        }

        /// <summary>Metindeki son yüzde ("kabul ihtimali %40" gibi; öndeki komisyon oranı atlanır).</summary>
        static float Yuzde(string s)
        {
            var ms = YuzdeKalibi.Matches(s ?? "");
            return ms.Count > 0 ? float.Parse(ms[ms.Count - 1].Groups[1].Value, Tr) / 100f : -1f;
        }

        static readonly Regex IsaretsizTutar = new Regex(@"(\d[\d\u00A0\u202F ]*(?:[.,]\d+)?)\s?(€|Ft)");

        /// <summary>Metindeki ilk tutar, işaret aranmadan ("Şimdi öde (1 250 €)" → 500000).</summary>
        static long IlkTutar(string s)
        {
            var m = IsaretsizTutar.Match(s ?? "");
            if (!m.Success) return 0;
            var l = Tutarlar("−" + m.Value);
            return l.Count > 0 ? -l[0] : 0;
        }

        static int Bul(GameEvent ev, List<int> izinli, params string[] parcalar)
        {
            foreach (int i in izinli)
            {
                string l = (ev.choices[i].label ?? "").ToLowerInvariant();
                foreach (var p in parcalar) if (l.Contains(p.ToLowerInvariant())) return i;
            }
            return -1;
        }

        static Shop DukkanBul(string metin)
        {
            Shop en = null;
            foreach (var s in CityMap.I.Shops)
                if (!string.IsNullOrEmpty(s.Name) && metin.Contains(s.Name) && (en == null || s.Name.Length > en.Name.Length)) en = s;
            return en;
        }

        static Courier KuryeBul(string metin)
        {
            foreach (var c in CourierManager.I.Couriers) if (metin.Contains(c.courierName)) return c;
            foreach (var c in CourierManager.I.Couriers) if (metin.Contains(c.ShortName)) return c;
            return null;
        }

        /// <summary>Dükkanın son ayda (yoksa bu ay) teslimatlarımızdaki payı.</summary>
        static float DukkanPayi(Shop s)
        {
            long top = 0, bu = 0;
            foreach (var x in CityMap.I.Shops)
            {
                if (!x.contracted) continue;
                int n = Mathf.Max(x.lastMonthOrders, x.monthOrders);
                top += n;
                if (x == s) bu = n;
            }
            return top > 0 ? bu / (float)top : 0f;
        }

        // ------------------------------------------------------------------ karar kartları

        /// <summary>Kartı okuyup seçer. Bilmediği kartta seçenekleri tutar ve itibar/moral etkisine göre puanlar.</summary>
        int AkilliKart(GameEvent ev, List<int> izinli, out string neden)
        {
            string t = ev.title ?? "";
            string metin = t + "\n" + (ev.body ?? "");
            var eco = EconomyManager.I;
            long bos = Bos;
            int i;

            // Raporlar ve bilgi kartları
            if (t.StartsWith("Gün sonu raporu") || t.StartsWith("Haftalık rapor")) { neden = "rapor"; return izinli[0]; }

            if (t == "Yarın taksit günü")
            {
                var loan = FamilyLoan.I;
                long kalan = loan != null && loan.NextDue != null ? loan.NextDue.Remaining : 0;
                if (eco.money >= kalan + 40000) { neden = $"kasada {Ft(eco.money)} var, taksit ({Ft(kalan)}) şimdi ödenir"; return Bul(ev, izinli, "şimdi öde"); }
                neden = "kasa yetmiyor, yarına kadar kazanılacak"; return Bul(ev, izinli, "yarına");
            }

            if (t == "Kasa krizi!")
            {
                i = Bul(ev, izinli, "kredi");
                if (i >= 0) { neden = "kasa eksi: acil kredi alınır"; return i; }
            }

            if (t.Contains("Vergi Zamanı"))
            {
                long vergi = IlkTutar(ev.choices[0].label);
                if (eco.money >= vergi) { neden = "vergi kasadan ödenir (ertelemek ceza ve faiz)"; return Bul(ev, izinli, "şimdi öde"); }
                neden = "kasa yetmiyor, ertelenir"; return Bul(ev, izinli, "ertele");
            }

            if (t == "Yerel işletme vergisi" || t == "Belediye harcı")
            {
                long tutar = Gider(ev.choices[0].label);
                if (bos > tutar) { neden = "harcanabilir para yetiyor, ceza ödememek için hemen ödenir"; return Bul(ev, izinli, "öde"); }
                neden = "para dar, %10 cezayla ertelenir"; return Bul(ev, izinli, "ertele");
            }

            if (t.StartsWith("KDV"))
            {
                long tutar = Gider(ev.choices[0].label);
                // İtiraz: %50 sıfır, %50 1,5 kat → beklenen 0,75 kat
                if (bos > tutar * 3 / 2 || bos < tutar) { neden = "itirazın beklenen maliyeti %75, daha ucuz"; return Bul(ev, izinli, "tiraz"); }
                neden = "1,5 katını kaldıramaz, ödenir"; return Bul(ev, izinli, "öde");
            }

            if (t == "Vergi müfettişi kapıda")
            {
                long uzlas = Gider(ev.choices[0].label);
                var itiraz = Tutarlar(ev.choices.Count > 1 ? ev.choices[1].label : "");
                long avukat = itiraz.Count > 0 ? -itiraz[0] : 0, buyuk = itiraz.Count > 1 ? -itiraz[1] : uzlas * 3 / 2;
                long beklenen = avukat + buyuk / 2 + GunlukTeslimatKari / 4;   // itibar düşüşü de bir maliyet
                if (beklenen < uzlas) { neden = $"itirazın beklenen maliyeti {Ft(beklenen)} < uzlaşma {Ft(uzlas)}"; return Bul(ev, izinli, "tiraz"); }
                neden = $"uzlaşma {Ft(uzlas)} itirazın beklenen maliyetinden ({Ft(beklenen)}) ucuz"; return Bul(ev, izinli, "uzlaş");
            }

            if (t.Contains("dükkanına göz dikti"))
            {
                var s = DukkanBul(t);
                float pay = s != null ? DukkanPayi(s) : 0f;
                int hic = Bul(ev, izinli, "bir şey yapma"), karsi = Bul(ev, izinli, "karşı teklif"), mun = Bul(ev, izinli, "münhasırlık");
                float kal = hic >= 0 ? Yuzde(ev.choices[hic].label) : 0f, munSans = mun >= 0 ? Yuzde(ev.choices[mun].label) : 0f;
                if (kal >= 0.75f) { neden = $"dükkanın kalma ihtimali %{kal * 100f:0}, komisyon feda edilmez"; return hic; }
                if (mun >= 0 && munSans >= 0.6f && pay >= 0.1f) { neden = $"önemli dükkan (siparişlerin %{pay * 100f:0}'i), münhasırlık şansı %{munSans * 100f:0}"; return mun; }
                if (karsi >= 0 && pay >= 0.12f) { neden = $"önemli dükkan (siparişlerin %{pay * 100f:0}'i): karşı teklif garanti tutar"; return karsi; }
                neden = $"dükkan küçük (siparişlerin %{pay * 100f:0}'i), risk alınır"; return hic >= 0 ? hic : izinli[0];
            }

            if (t.Contains("kuryelerini ayartıyor"))
            {
                bool kuryeAz = IptalOrani(3) > 0.05f || CourierManager.I.Couriers.Count <= 6;
                int prim = Bul(ev, izinli, "prim ver"), birak = Bul(ev, izinli, "bırak"), zam = Bul(ev, izinli, "artır");
                long primBedel = prim >= 0 ? Gider(ev.choices[prim].label) : long.MaxValue;
                long zamBedel = HaftalikKuryeOdemesi * 4 / 10;   // 4 hafta %10
                if (!kuryeAz) { neden = "kurye yeterli, gidenlerin yerine yeni başvuru var"; return birak; }
                if (primBedel <= zamBedel && primBedel < bos) { neden = $"kurye açığı var; prim ({Ft(primBedel)}) 4 haftalık zamdan ({Ft(zamBedel)}) ucuz"; return prim; }
                if (zam >= 0 && zamBedel < bos) { neden = $"kurye açığı var; 4 haftalık zam ~{Ft(zamBedel)}"; return zam; }
                neden = "para yok, risk alınır"; return birak;
            }

            if (t.StartsWith("Fiyat savaşı"))
            {
                int karsi = Bul(ev, izinli, "karşı kampanya");
                long bedel = karsi >= 0 ? Gider(ev.choices[karsi].label) : 0;
                long kayip = GunlukTeslimatKari * 7 * 15 / 100 / Mathf.Max(1, CityMap.I.UnlockLevel);   // bölgedeki kâr × (%25 − %10)
                if (karsi >= 0 && bedel < kayip && bedel < bos) { neden = $"kampanya {Ft(bedel)} < tahmini kayıp {Ft(kayip)}"; return karsi; }
                neden = $"kampanya ({Ft(bedel)}) tahmini kayıptan ({Ft(kayip)}) pahalı"; return Bul(ev, izinli, "görmezden");
            }

            if (t.Contains("reklam atağında"))
            {
                int rek = Bul(ev, izinli, "reklam ver");
                long bedel = rek >= 0 ? Gider(ev.choices[rek].label) : 0;
                float pay = RivalCompany.I != null ? RivalCompany.I.MarketShare : 0.5f;
                if (rek >= 0 && pay < 0.55f && bedel < bos / 3) { neden = $"pazar payı %{pay * 100f:0}, reklam karşılanabilir"; return rek; }
                neden = $"pazar payı %{pay * 100f:0} ya da para dar"; return Bul(ev, izinli, "görmezden");
            }

            if (t.EndsWith("indirim istiyor"))
            {
                var s = DukkanBul(t);
                float pay = s != null ? DukkanPayi(s) : 0f;
                if (s != null && (s.satisfaction < 50f || pay >= 0.2f)) { neden = $"memnuniyet {s.satisfaction:0}, pay %{pay * 100f:0}: dükkanı kaybetmemek için"; return Bul(ev, izinli, "kabul"); }
                neden = s != null ? $"memnuniyet {s.satisfaction:0} yeterli, komisyon korunur" : "dükkan bulunamadı"; return Bul(ev, izinli, "reddet");
            }

            if (t == "Sözleşme yenileme: rakip teklifi")
            {
                var s = DukkanBul(ev.body ?? "");
                float pay = s != null ? DukkanPayi(s) : 0f;
                int mun = Bul(ev, izinli, "münhasırlık"), esit = Bul(ev, izinli, "eşitle"), israr = Bul(ev, izinli, "ısrar");
                if (s != null && s.satisfaction >= 85f && israr >= 0) { neden = $"memnuniyet {s.satisfaction:0}, büyük ihtimalle kalır"; return israr; }
                if (mun >= 0) { neden = "münhasırlık: komisyon −3 puan ama siparişler +%15 ve rakip çıkar (toplamda daha kârlı)"; return mun; }
                neden = $"rakibe kaptırmamak için eşitlenir (pay %{pay * 100f:0})"; return esit >= 0 ? esit : izinli[0];
            }

            if (t == "Komisyon teklifi")
            {
                var s = DukkanBul(ev.body ?? "");
                long bedel = Gider(ev.choices[0].label);
                float gunluk = s != null ? Mathf.Max(s.lastMonthOrders / 30f, s.monthOrders / (float)Mathf.Max(1, GameClock.I.DayOfMonth)) : 0f;
                float sepet = s != null ? (s.def.basketMin + s.def.basketMax) / 2f : 4000f;
                long kazanc = (long)(gunluk * sepet * 0.02f * 90f);
                if (kazanc > bedel * 3 / 2 && bedel < bos) { neden = $"3 ayda ~{Ft(kazanc)} ek komisyon > {Ft(bedel)}"; return Bul(ev, izinli, "kabul"); }
                neden = $"3 ayda ~{Ft(kazanc)} ek komisyon, bedele ({Ft(bedel)}) değmez"; return Bul(ev, izinli, "reddet");
            }

            if (t == "Eksik teslimat (kurye)" || t == "Hasarlı sipariş")
            {
                var c = KuryeBul(ev.body ?? "");
                if (c != null && c.morale < 0.5f) { neden = $"{c.ShortName}'ın morali düşük ({c.morale:0.00}), şirket üstlenir"; return Bul(ev, izinli, "şirket karşılasın"); }
                neden = "kural: kurye kaynaklı iade kuryeden düşülür"; return Bul(ev, izinli, "kuryeden düş");
            }

            if (t == "Tekrarlayan hijyen şikayeti")
            {
                i = Bul(ev, izinli, "yeni termal çanta");
                if (i >= 0) { neden = "çanta yıpranmış, temizlik yetmez"; return i; }
                neden = "ilk ciddi uyarı"; return Bul(ev, izinli, "ciddi uyarı");
            }

            if (t == "Kuryeler paket başı ücret artışı istiyor")
            {
                float moral = OrtMoral;
                if (moral < 0.65f || IptalOrani(3) > 0.06f) { neden = $"ort. moral {moral:0.00}, kurye kaybı göze alınamaz"; return Bul(ev, izinli, "kabul"); }
                neden = $"ort. moral {moral:0.00}, −12 kaldırılabilir"; return Bul(ev, izinli, "reddet");
            }

            if (t == "Dükkan komisyon indirimi istiyor")
            {
                var s = DukkanBul(ev.body ?? "");
                float pay = s != null ? DukkanPayi(s) : 0f;
                if (s != null && s.satisfaction - 30f < 40f && pay >= 0.1f) { neden = $"memnuniyet {s.satisfaction:0}: reddedersen dükkan gider (siparişlerin %{pay * 100f:0}'i)"; return Bul(ev, izinli, "kabul"); }
                neden = s != null ? $"memnuniyet {s.satisfaction:0}, pay %{pay * 100f:0}: kalıcı indirim verilmez" : "dükkan bulunamadı"; return Bul(ev, izinli, "reddet");
            }

            if (t == "Belediyeden toplu park cezası")
            {
                long ceza = Gider(ev.choices[0].label);
                if (ceza < bos / 4) { neden = "ceza küçük, kuryelerin morali korunur"; return Bul(ev, izinli, "şirket ödesin"); }
                neden = "ceza büyük, kuryelerle paylaşılır"; return Bul(ev, izinli, "kes");
            }

            if (t == "Ekipman noktasında hırsızlık")
            {
                long kamera = Gider(ev.choices[0].label);
                if (kamera < bos / 3) { neden = "kamera karşılanabilir, moral düşmesin"; return Bul(ev, izinli, "kamera"); }
                neden = "para dar, kontrol yapılır"; return Bul(ev, izinli, "kontrol");
            }

            if (t == "Pahalı sipariş kayboldu")
            {
                long iade = Gider(ev.choices[0].label);
                var c = KuryeBul(ev.body ?? "");
                if (iade < bos / 10 || (c != null && c.morale < 0.55f)) { neden = "iade küçük ya da kuryenin morali düşük, şirket öder"; return Bul(ev, izinli, "şirket ödesin"); }
                neden = "iade büyük, yarısı kuryeden"; return Bul(ev, izinli, "yarı yarıya");
            }

            if (t == "Kuryeler grev hazırlığında")
            {
                long hafta = HaftalikKuryeOdemesi, gun = GunlukTeslimatKari;
                long kabul = hafta * 6 * 15 / 100, uzlas = hafta * 6 * 7 / 100 + gun / 3, red = gun * 2 + gun;   // red: 2 gün iş yok + itibar
                if (kabul <= uzlas && kabul <= red) { neden = $"kabul {Ft(kabul)} en ucuzu (uzlaş {Ft(uzlas)}, grev {Ft(red)})"; return Bul(ev, izinli, "kabul"); }
                if (uzlas <= red) { neden = $"uzlaş {Ft(uzlas)} en ucuzu (kabul {Ft(kabul)}, grev {Ft(red)})"; return Bul(ev, izinli, "uzlaş"); }
                neden = $"grev {Ft(red)} en ucuzu (kabul {Ft(kabul)}, uzlaş {Ft(uzlas)})"; return Bul(ev, izinli, "reddet");
            }

            if (t == "Uygulama çöktü!")
            {
                int sunucu = Bul(ev, izinli, "yedekli sunucu"), kupon = Bul(ev, izinli, "kupon");
                if (sunucu >= 0 && Gider(ev.choices[sunucu].label) < bos / 3) { neden = "yedekli sunucu karşılanabilir, bir daha çökmesin"; return sunucu; }
                if (kupon >= 0 && Gider(ev.choices[kupon].label) < bos / 5) { neden = "kupon ucuz, itibar korunur"; return kupon; }
                neden = "para dar, sadece duyuru"; return Bul(ev, izinli, "duyuru");
            }

            if (t == "Akaryakıt zammı")
            {
                int kart = Bul(ev, izinli, "yakıt kartı");
                long bedel = kart >= 0 ? Gider(ev.choices[kart].label) : long.MaxValue;
                long destek = HaftalikKuryeOdemesi * 3 * 12 / 100 / 2;   // motor/araba payı yaklaşık yarısı
                if (bedel <= destek && bedel < bos) { neden = $"yakıt kartı {Ft(bedel)} ≤ 3 haftalık destek ~{Ft(destek)}"; return kart; }
                neden = $"yakıt desteği ~{Ft(destek)} daha ucuz ya da kasa dar"; return Bul(ev, izinli, "yakıt desteği");
            }

            if (t == "Sıcak dalgası")
            {
                int paket = Bul(ev, izinli, "su ve soğutma");
                if (paket >= 0 && Gider(ev.choices[paket].label) < bos / 4) { neden = "soğutma paketi karşılanabilir, kuryeler daha az yorulur"; return paket; }
                neden = "para dar, sıcak primiyle daha çok kurye çıkar"; return Bul(ev, izinli, "sıcak primi");
            }

            if (t == "Platform Çalışanları Yasası")
            {
                var govde = Tutarlar((ev.body ?? "").Replace("~", "−"));
                long calisanGunluk = 0;
                foreach (var v in govde) if (v < 0) calisanGunluk = -v;   // gövdedeki son tutar: "günde ~X"
                long calisanAylik = calisanGunluk * 30;
                int serbest = Bul(ev, izinli, "serbest çalışan");
                long serbestAylik = serbest >= 0 ? Gider(ev.choices[serbest].label) : long.MaxValue;
                if (calisanAylik > 0 && calisanAylik < serbestAylik) { neden = $"çalışan statüsü ayda ~{Ft(calisanAylik)} < serbest {Ft(serbestAylik)}, üstelik moral ve grev avantajı"; return Bul(ev, izinli, "çalışan statüsüne"); }
                neden = $"serbest kalmak ayda {Ft(serbestAylik)}, çalışan statüsü ~{Ft(calisanAylik)}"; return serbest;
            }

            // Bilinmeyen kart: her seçeneği puanla
            return GenelPuan(ev, izinli, out neden);
        }

        /// <summary>
        /// Bilinmeyen kart: gider (haftalık teslimat kârına oranla), itibar ve moral etkisi, "reddet/görmezden" cezası.
        /// Parası yetmeyen seçenek seçilmez.
        /// </summary>
        int GenelPuan(GameEvent ev, List<int> izinli, out string neden)
        {
            long bos = Bos;
            float olcek = Mathf.Max(100000f, GunlukTeslimatKari * 7f);
            int en = izinli[0];
            float enPuan = float.MinValue;
            var sb = new System.Text.StringBuilder();
            foreach (int i in izinli)
            {
                string l = ev.choices[i].label ?? "";
                string k = l.ToLowerInvariant();
                float p = 0f;
                long gider = Gider(l);
                p -= gider / olcek;
                if (gider > 0 && gider > bos) p -= 10f;
                foreach (var tt in Tutarlar(l)) if (tt > 0) p += tt / olcek;
                var im = Regex.Match(l, @"itibar\s?([−\-+])\s?(\d+[.,]?\d*)");
                if (im.Success)
                {
                    float v = float.Parse(im.Groups[2].Value.Replace(',', '.'), Tr);
                    p += (im.Groups[1].Value == "+" ? v : -v) * 8f;
                }
                else if (k.Contains("itibar düşer")) p -= 0.5f;
                if (k.Contains("moral")) p += k.Contains("artar") || k.Contains("moral +") ? 0.15f : -0.2f;
                if (k.StartsWith("reddet") || k.StartsWith("görmezden") || k.StartsWith("bir şey yapma")) p -= 0.05f;
                sb.Append($"{i + 1}:{p:0.00} ");
                if (p > enPuan) { enPuan = p; en = i; }
            }
            neden = "puanlama (gider, itibar, moral) → " + sb.ToString().Trim();
            return en;
        }

        // ------------------------------------------------------------------ borç

        void AkilliBorc()
        {
            var loan = FamilyLoan.I;
            if (loan == null || loan.Finished || loan.Failed) return;
            var eco = EconomyManager.I;
            var due = loan.NextDue;
            // Kasa çok rahatsa borcun tamamını kapat (aile riskini sıfırla)
            long sabit = 200000 + UpgradeManager.I.TotalDailyCost * 7L;
            if (eco.FreeCash > loan.Remaining + sabit + 400000)
            {
                long kalan = loan.Remaining;
                if (loan.Pay(kalan)) { Eylem("Borç", $"Kasa rahat (serbest {Ft(eco.FreeCash + kalan)}): borcun tamamı kapatıldı ({Ft(kalan)})"); return; }
            }
            if (due == null) return;
            int gunKaldi = loan.DaysUntil(due);
            if (gunKaldi <= 3 && eco.money >= due.Remaining + 50000)
            {
                long tutar = due.Remaining;
                if (loan.Pay(tutar)) Eylem("Borç", $"{due.DueText} taksiti {gunKaldi} gün önceden ödendi: {Ft(tutar)}");
            }
            else if (gunKaldi <= 0 && eco.money > 150000)
            {
                long tutar = System.Math.Min(due.Remaining, eco.money - 100000);
                if (loan.Pay(tutar)) Eylem("Borç", $"{due.DueText} taksitine para yetmedi, kısmi ödendi: {Ft(tutar)}");
            }
        }

        // ------------------------------------------------------------------ işe alım

        /// <summary>Mesleğe göre o saatte çalışma isteği (Courier.Preference'ın sadeleştirilmiş hali; hafta içi).</summary>
        static float SaatUygunlugu(Occupation o, int h)
        {
            switch (o)
            {
                case Occupation.OfisCalisani: return h >= 17 && h < 23 ? 0.85f : 0.05f;
                case Occupation.HaftaSonu: return 0.25f * (h >= 9 && h < 23 ? 0.9f : 0.05f);   // haftanın 2 günü
                case Occupation.Ogrenci: return h >= 10 && h < 23 ? 0.6f : 0.05f;
                case Occupation.Vardiyali: return (h >= 7 && h < 13) || (h >= 15 && h < 23) ? 0.45f : 0.03f;
                case Occupation.Emekli: return h >= 8 && h < 15 ? 0.85f : 0.05f;
                case Occupation.Ebeveyn: return h >= 9 && h < 15 ? 0.8f : 0.03f;
                case Occupation.GeceKuryesi: return h >= 20 || h < 3 ? 0.85f : 0.05f;
                default: return (h >= 10 && h < 15) || (h >= 17 && h < 22) ? 0.9f : 0.2f;
            }
        }

        int bugunAlinan, alimGunu = -1;

        void AkilliIseAlim(bool acilMi)
        {
            var cm = CourierManager.I;
            var clk = GameClock.I;
            if (alimGunu != clk.Day) { alimGunu = clk.Day; bugunAlinan = 0; }
            if (cm.Applications.Count == 0 || UpgradeManager.I.CourierCapFull) return;

            var son = SonGun();
            int kurye = cm.Couriers.Count, dukkan = SozlesmeliDukkan();
            float iptal = son != null && son.deliveries + son.cancelled > 0 ? son.cancelled / (float)(son.deliveries + son.cancelled) : 0f;
            float kuryeBasi = son != null ? son.deliveries / (float)Mathf.Max(1, son.couriers) : 0f;
            // Bugünün durumu (gün içi acil alım): saatlik iptal
            int bugunIptal = OrderManager.I.cancelledToday;

            int hedef;   // bugün en fazla kaç kurye alınır
            string neden;
            if (kurye < Mathf.CeilToInt(dukkan * 2.5f)) { hedef = 2; neden = $"dükkan başına kurye az ({kurye}/{dukkan}, hedef 2,5)"; }
            else if (iptal > 0.10f) { hedef = 2; neden = $"dün iptal %{iptal * 100f:0}"; }
            else if (iptal > 0.03f || (son != null && son.avgAssignWait > 5f)) { hedef = 1; neden = $"dün iptal %{iptal * 100f:0}, atama bekleme {(son != null ? son.avgAssignWait : 0f):0.0} dk"; }
            else { hedef = 0; neden = ""; }
            if (acilMi)
            {
                // Gün içinde: sadece iptaller patlıyorsa (bugün 15+ iptal) ve günde 3'ü geçmeden
                if (bugunIptal < 15 || bugunAlinan >= 3) return;
                hedef = bugunAlinan + 1; neden = $"bugün şimdiden {bugunIptal} iptal";
            }
            if (bugunAlinan >= hedef) return;
            if (!acilMi && kuryeBasi > 0f && kuryeBasi < 10f && iptal < 0.05f) return;   // kuryeler zaten boş, kazançları düşmesin
            if (EconomyManager.I.FreeCash < 100000) return;   // başlangıç kiti bile alınamaz

            // İptal profili yoksa (ilk gün) tipik yoğunluk: öğle ve akşam
            float[] prof = new float[24];
            float profTop = 0f;
            for (int h = 0; h < 24; h++) { prof[h] = iptalProfili[h]; profTop += prof[h]; }
            if (profTop < 1f) for (int h = 0; h < 24; h++) prof[h] = (h >= 11 && h < 14) || (h >= 17 && h < 21) ? 1f : 0.1f;

            CourierCandidate en = null;
            float enPuan = float.MinValue, enKapsama = 0f;
            float profMax = 0f;
            for (int h = 0; h < 24; h++) profMax += prof[h];
            foreach (var a in cm.Applications)
            {
                if (a.pickiness > 0.75f) continue;   // çok seçici: teklifleri reddeder
                float kapsama = 0f;
                for (int h = 0; h < 24; h++) kapsama += prof[h] * SaatUygunlugu(a.occupation, h);
                kapsama /= Mathf.Max(0.01f, profMax);
                float hiz = a.vehicle == VehicleKind.Motosiklet ? 0.25f : a.vehicle == VehicleKind.Araba ? 0.15f : a.vehicle == VehicleKind.EScooter ? 0.1f : 0f;
                float p = kapsama * 3f + a.speedSkill + a.care + a.stamina * 0.5f - a.pickiness * 1.2f + a.level * 0.1f + hiz;
                if (p > enPuan) { enPuan = p; en = a; enKapsama = kapsama; }
            }
            if (en == null) return;
            int once = cm.Couriers.Count;
            cm.Hire(en);
            if (cm.Couriers.Count > once)
            {
                bugunAlinan++;
                int yogun = 0; float yogunDeger = -1f;
                for (int h = 0; h < 24; h++) if (prof[h] > yogunDeger) { yogunDeger = prof[h]; yogun = h; }
                Eylem("İşe alım", $"{en.name} ({GameData.Vehicle(en.vehicle).name}, {CourierManager.ShortOccupation(en.occupation)}, seviye {en.level}, " +
                                   $"seçicilik {en.pickiness:0.00}) · neden: {neden} · iptallerin en yoğun saati {yogun:00}:00, bu kuryenin o saatleri karşılaması %{enKapsama * 100f:0} · " +
                                   $"{cm.Applications.Count + 1} başvuru arasından");
            }
        }

        // ------------------------------------------------------------------ dükkanlar ve bölge

        /// <summary>Dükkanın günlük beklenen katkısı: yoğunluk × sepet × komisyon, merkeze uzaklıkla azalır.</summary>
        static float DukkanDegeri(Shop s)
        {
            float sepet = (s.def.basketMin + s.def.basketMax) / 2f;
            int mesafe = CityMap.Manhattan(s.door, CityMap.I.HQDoor);
            return s.def.weight * sepet * s.Commission / (1f + mesafe / 25f);
        }

        void AkilliSozlesme()
        {
            var clk = GameClock.I;
            var map = CityMap.I;
            var son = SonGun();
            if (son == null) return;   // ilk gün bitmeden yok
            float iptal = IptalOrani(2);
            int kurye = CourierManager.I.Couriers.Count, dukkan = SozlesmeliDukkan();
            float kuryeBasi = son.deliveries / (float)Mathf.Max(1, son.couriers);
            bool servisIyi = iptal < 0.04f && son.avgAssignWait < 5f;
            bool kuryeBos = kuryeBasi < 14f && iptal < 0.06f;   // kuryeler boş kalıyor: iş lazım
            if (!servisIyi && !kuryeBos) return;
            if (kurye < Mathf.CeilToInt((dukkan + 1) * 2f)) return;   // yeni dükkana yetecek kurye yok

            Shop en = null;
            float enDeger = 0f;
            foreach (var s in map.Shops)
            {
                if (s.contracted || s.own || !map.IsUnlocked(s.tile) || clk.Day < s.resignBlockedUntil) continue;
                // 60 günlük beklenen katkı − imza bedeli
                float deger = DukkanDegeri(s) * 60f - s.def.contractCost;
                if (en == null || deger > enDeger) { en = s; enDeger = deger; }
            }
            if (en == null || en.def.contractCost > Bos) return;
            if (map.SignContract(en))
                Eylem("Sözleşme", $"{en.Name} ({Ft(en.def.contractCost)}, komisyon %{en.Commission * 100f:0.#}, yoğunluk {en.def.weight:0.00}, " +
                                   $"merkeze {CityMap.Manhattan(en.door, map.HQDoor)} kare) · neden: {(servisIyi ? $"servis iyi (iptal %{iptal * 100f:0})" : $"kurye başı {kuryeBasi:0} teslimat, iş lazım")} · dükkan {SozlesmeliDukkan()}");
        }

        int sonBolgeAcmaGun = -100;

        void AkilliBolge()
        {
            var clk = GameClock.I;
            var map = CityMap.I;
            if (clk.Day - sonBolgeAcmaGun < 7 || map.UnlockLevel >= map.MaxLevel) return;
            // Açık bölgede değerli dükkan kaldıysa önce onlar
            float enIyiKalan = 0f, ortalama = 0f; int n = 0;
            foreach (var s in map.Shops)
            {
                if (!map.IsUnlocked(s.tile) || s.own) continue;
                float d = DukkanDegeri(s);
                if (s.contracted) { ortalama += d; n++; }
                else if (d > enIyiKalan) enIyiKalan = d;
            }
            ortalama = n > 0 ? ortalama / n : 0f;
            if (enIyiKalan > ortalama * 0.5f) return;
            if (IptalOrani(3) > 0.05f) return;   // servis oturmadan büyüme yok
            long bedel = CityMap.UnlockCost[map.UnlockLevel + 1];
            if (bedel > Bos / 2) return;
            if (map.TryUnlockNext())
            {
                sonBolgeAcmaGun = clk.Day;
                Eylem("Bölge", $"{CityMap.DistrictNames[map.UnlockLevel]} açıldı ({Ft(bedel)}) · açık bölgede değerli dükkan kalmadı, servis iyi");
            }
        }

        int sonPazarlikGun = -100;

        /// <summary>Haftada en fazla bir pazarlık: şansı yüksek dükkanla komisyon artışı ya da münhasırlık.</summary>
        void AkilliDukkanBakimi()
        {
            var rv = RivalCompany.I;
            var clk = GameClock.I;
            if (rv == null || clk.Day - sonPazarlikGun < 7) return;
            Shop enM = null, enP = null;
            foreach (var s in CityMap.I.Shops)
            {
                if (!s.contracted || s.own) continue;
                if (s.rivalContracted && rv.CanOfferExclusive(s) && rv.ExclusiveChance(s) >= 0.65f && DukkanPayi(s) >= 0.1f) enM = s;
                else if (rv.CanNegotiate(s) && rv.NegotiateChance(s) >= 0.5f && (enP == null || DukkanPayi(s) > DukkanPayi(enP))) enP = s;
            }
            if (enM != null)
            {
                sonPazarlikGun = clk.Day;
                Eylem("Pazarlık", $"{enM.Name}'a münhasırlık önerildi (şans %{rv.ExclusiveChance(enM) * 100f:0}, rakip de çalışıyor)");
                rv.OfferExclusive(enM);
            }
            else if (enP != null)
            {
                sonPazarlikGun = clk.Day;
                Eylem("Pazarlık", $"{enP.Name} ile komisyon pazarlığı (şans %{rv.NegotiateChance(enP) * 100f:0}, memnuniyet {enP.satisfaction:0})");
                rv.Negotiate(enP);
            }
        }

        // ------------------------------------------------------------------ geliştirme, personel, ekipman, market, fiyat

        int sonAkilliGelistirmeGun = -100;

        void AkilliGelistirme()
        {
            if (Guide.I != null && !Guide.I.IsOpen(Feature.Gelisim)) return;
            var up = UpgradeManager.I;
            var clk = GameClock.I;
            var cm = CourierManager.I;
            var son = SonGun();
            long bos = Bos;

            // Kurye hesabı doldu ve hâlâ kurye lazım: platform paketi (bekletilmez)
            if (up.CourierCapFull && cm.Applications.Count > 0 && IptalOrani(2) > 0.03f && Al(UpgradeKind.Platform, bos, "kurye hesapları dolu, iptaller sürüyor")) return;

            if (son == null || clk.Day - sonAkilliGelistirmeGun < 3) return;
            long gelir = OrtalamaGelir(7);
            float iptal = IptalOrani(3);
            float kuryeBasi = son.deliveries / (float)Mathf.Max(1, son.couriers);

            // İhtiyaca göre öncelik sırası
            var adaylar = new List<(UpgradeKind k, string neden)>();
            if (son.avgAssignWait > 5f || iptalRedDun >= 3) adaylar.Add((UpgradeKind.Dispatcher, $"atama bekleme {son.avgAssignWait:0.0} dk, dün {iptalRedDun} teklif reddi"));
            if (iptalRedDun >= 3) adaylar.Add((UpgradeKind.Uygulama, $"kuryeler teklif reddediyor ({iptalRedDun}/gün)"));
            if (dunTelefon + dunAriza >= 2) adaylar.Add((UpgradeKind.Bakim, $"dün {dunTelefon} telefon kapanması, {dunAriza} arıza"));
            if (son.avgPickupWait > 12f) adaylar.Add((UpgradeKind.Restoran, $"dükkanda bekleme {son.avgPickupWait:0.0} dk"));
            if (dunAdres >= 3) adaylar.Add((UpgradeKind.Adres, $"dün {dunAdres} adres/evde yok sorunu"));
            if (OrtMoral < 0.6f) adaylar.Add((UpgradeKind.Dinlenme, $"ort. moral {OrtMoral:0.00}"));
            if (iptal < 0.03f && kuryeBasi < 12f && cm.Couriers.Count >= 5) adaylar.Add((UpgradeKind.Reklam, $"servis iyi, kurye başı {kuryeBasi:0} teslimat: talep lazım"));
            if (SupportManager.I != null && SupportManager.I.Agents.Count >= up.SupportCap && cm.Couriers.Count >= 15) adaylar.Add((UpgradeKind.Ofis, "destek ekibi sınırda"));
            if (cm.Couriers.Count >= 15) adaylar.Add((UpgradeKind.Hub, $"{cm.Couriers.Count} kurye: dağıtım merkezi"));

            foreach (var a in adaylar)
            {
                if (up.IsLocked(a.k) || up.NextCost(a.k) <= 0) continue;
                var d = UpgradeManager.Def(a.k);
                int lv = up.Level(a.k);
                long ekGunluk = d.DailyAt(lv + 1) - d.DailyAt(lv);
                if (gelir > 0 && ekGunluk > gelir * 0.08f) continue;   // günlük gideri gelire göre ağır
                if (Al(a.k, bos, a.neden)) { sonAkilliGelistirmeGun = clk.Day; return; }
            }
        }

        bool Al(UpgradeKind k, long bos, string neden)
        {
            var up = UpgradeManager.I;
            int bedel = up.NextCost(k);
            if (bedel <= 0 || up.IsLocked(k) || bedel > bos / 2) return false;
            if (!up.Buy(k)) return false;
            var def = UpgradeManager.Def(k);
            Eylem("Geliştirme", $"{def.name} → {def.levelNames[up.Level(k)]} ({Ft(bedel)}, günlük gider {Ft(up.DailyCost(k))}) · neden: {neden}");
            return true;
        }

        void AkilliDestek()
        {
            if (Guide.I != null && !Guide.I.IsOpen(Feature.Destek)) return;
            var sm = SupportManager.I;
            var cm = CourierManager.I;
            if (sm == null || sm.Candidates.Count == 0) return;
            int istenen = cm.Couriers.Count >= 14 ? 2 : cm.Couriers.Count >= 6 ? 1 : 0;
            if (sm.Agents.Count >= istenen || sm.Agents.Count >= UpgradeManager.I.SupportCap) return;
            SupportCandidate en = null;
            foreach (var c in sm.Candidates) if (en == null || c.skill > en.skill) en = c;
            if (en == null || en.monthlySalary * 2L > Bos) return;
            // İlk eleman akşam yoğunluğunu (14–22), ikincisi sabahı kapsar
            en.chosenShift = sm.Agents.Count == 0 ? SupportShift.Ogle : SupportShift.Sabah;
            int once = sm.Agents.Count;
            sm.Hire(en);
            if (sm.Agents.Count > once)
                Eylem("Destek", $"{en.name} işe alındı ({SupportManager.ShiftLabel(en.chosenShift)}, beceri {en.skill:0.00}, aylık {Ft(en.monthlySalary)}) · {cm.Couriers.Count} kurye");
        }

        void AkilliOperasyon()
        {
            var om = OperationsManager.I;
            if (om == null || om.HasManager || om.Candidates.Count == 0 || CourierManager.I.Couriers.Count < 10) return;
            OpsPerson en = null;
            foreach (var c in om.Candidates) if (en == null || c.skill > en.skill) en = c;
            if (en == null || en.monthlySalary * 2L > Bos) return;
            om.Hire(en);
            if (om.HasManager) Eylem("Operasyon", $"{en.name} operasyon sorumlusu oldu (beceri {en.skill:0.00}, aylık {Ft(en.monthlySalary)}): ekipmanı artık o teklif eder");
        }

        int sonPowerbankGun = -100, sonYagmurlukGun = -100, sonKaskGun = -100;

        void AkilliEkipman()
        {
            var cm = CourierManager.I;
            var clk = GameClock.I;
            if (OperationsManager.I != null && OperationsManager.I.HasManager) return;   // sorumlu varsa o yapar
            if (Bos < 100000) return;
            if (dunTelefon >= 2 && clk.Day - sonPowerbankGun >= 3)
            {
                sonPowerbankGun = clk.Day;
                int n = cm.OfferToAll(EquipmentKind.Powerbank);
                Eylem("Ekipman", $"Dün {dunTelefon} kuryenin telefonu kapandı: herkese powerbank teklif edildi, {n} kabul");
            }
            bool yagmurMevsimi = clk.Season == Season.Ilkbahar || clk.Season == Season.Sonbahar || (WeatherSystem.I != null && WeatherSystem.I.IsRainy);
            if (yagmurMevsimi && clk.Day - sonYagmurlukGun >= 7)
            {
                int eksik = 0;
                foreach (var c in cm.Couriers) if (c.V.exposed && !c.Has(EquipmentKind.Yagmurluk)) eksik++;
                if (eksik >= 2)
                {
                    sonYagmurlukGun = clk.Day;
                    int n = cm.OfferToAll(EquipmentKind.Yagmurluk);
                    Eylem("Ekipman", $"Yağmur mevsimi, {eksik} açık araçlı kuryede yağmurluk yok: teklif edildi, {n} kabul");
                }
            }
            if (dunKaza >= 1 && clk.Day - sonKaskGun >= 7)
            {
                sonKaskGun = clk.Day;
                int n = cm.OfferToAll(EquipmentKind.Kask);
                Eylem("Ekipman", $"Dün kaza oldu: kaskı olmayanlara kask teklif edildi, {n} kabul");
            }
        }

        void AkilliMarket()
        {
            var ds = DarkStore.I;
            var clk = GameClock.I;
            if (ds == null || (Guide.I != null && !Guide.I.IsOpen(Feature.Market))) return;
            if (ds.IsOpen)
            {
                long alinan = ds.RestockAll(0.8f, false);
                if (alinan > 0) Eylem("Market", $"Raflar %80'e tamamlandı ({Ft(alinan)} stok)");
                return;
            }
            var loan = FamilyLoan.I;
            bool borcBitti = loan == null || loan.Finished;
            long bedel = DarkStore.SizeCost[1];
            if (clk.CompanyDay < 21 || !borcBitti || IptalOrani(3) > 0.04f) return;
            if (Bos < bedel * 2) return;   // açılış + ilk stok + birkaç günlük gider
            if (ds.Open())
            {
                long stok = ds.RestockAll(0.8f, false);
                Eylem("Market", $"Kendi marketin açıldı ({Ft(bedel)}) ve {Ft(stok)} stok alındı · borç bitti, servis iyi, kasa rahat");
            }
        }

        int baslangicTaban = -1;
        float baslangicBonus = -1f;
        int kacanPahaliHaftaBasi;

        /// <summary>Haftada bir (pazartesi sabahı): kaçan siparişe göre teslimat ücreti, teklif reddine göre bonus.</summary>
        void AkilliFiyat()
        {
            var eco = EconomyManager.I;
            if (baslangicTaban < 0) { baslangicTaban = eco.baseFee; baslangicBonus = eco.bonusMultiplier; }
            int kacan = Sayac("Kaçan sipariş: teslimat ücreti pahalı") - kacanPahaliHaftaBasi;
            kacanPahaliHaftaBasi = Sayac("Kaçan sipariş: teslimat ücreti pahalı");
            float teslimat = Ort(7, d => d.deliveries) * 7f;
            if (teslimat <= 0f) return;
            float oran = kacan / teslimat;
            if (oran > 0.06f && eco.baseFee > baslangicTaban * 0.7f)
            {
                int eski = eco.baseFee;
                eco.baseFee = Mathf.Max(200, Mathf.RoundToInt(eco.baseFee * 0.9f / 10f) * 10);
                Eylem("Fiyat", $"Geçen hafta {kacan} müşteri ücreti pahalı bulup vazgeçti (teslimatın %{oran * 100f:0}): taban ücret {Ft(eski)} → {Ft(eco.baseFee)}");
            }
            else if (oran < 0.01f && IptalOrani(7) < 0.04f && eco.baseFee < baslangicTaban * 1.2f)
            {
                int eski = eco.baseFee;
                eco.baseFee = Mathf.RoundToInt(eco.baseFee * 1.05f / 10f) * 10;
                Eylem("Fiyat", $"Fiyattan vazgeçen yok denecek kadar az (%{oran * 100f:0.0}), servis iyi: taban ücret {Ft(eski)} → {Ft(eco.baseFee)}");
            }
            if (iptalRedDun >= 4 && eco.bonusMultiplier < baslangicBonus + 0.5f)
            {
                float eski = eco.bonusMultiplier;
                eco.bonusMultiplier = Mathf.Min(2f, eco.bonusMultiplier + 0.1f);
                Eylem("Fiyat", $"Kuryeler teklifleri reddediyor (dün {iptalRedDun}): bonus çarpanı {eski:0.0} → {eco.bonusMultiplier:0.0}");
            }
        }
    }
}
