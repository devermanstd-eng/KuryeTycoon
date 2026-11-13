using System;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Senaryolar, serbest mod ve günlük meydan okuma. Yeni oyun kurarken seçilir (kurulum ekranı), kayda yazılır.
    ///  • Klasik: bildiğimiz oyun (kurulum kredisi, rakipler, büyüme).
    ///  • Borç batağı: kurulum kredisi 15 000 € (+faiz), aynı beş taksit. Hedef: krediyi Temmuz sonuna kadar kapat.
    ///  • Rakiplerin şehri: üç platform da güçlü, Turbo Paket bütün dükkanlarla çalışıyor. Hedef: 90 gün içinde şehrin lideri ol
    ///    (bir haftada her rakipten çok teslimat).
    ///  • Hızlı büyüme: yatırımcıdan 6 000 € ek sermaye. Hedef: 60 gün içinde 25 kurye ve günde ortalama 1 000 € gelir.
    ///  • Serbest mod: 250 000 € kasa, borç yok, hedef yok; şehri kurup denemek için.
    ///  • Günlük meydan okuma: herkes bugün aynı şehirle başlar (tohum = bugünün tarihi). 14 gün sonra puan:
    ///    teslimat × itibar / 5. Bu bilgisayardaki en iyi puan saklanır.
    /// Senaryo bitince (başarılı ya da başarısız) oyun sürer; sonuç kartı çıkar.
    /// </summary>
    public static class Senaryo
    {
        public enum Tur { Klasik, BorcBatagi, RakibinSehri, HizliBuyume, Serbest, GunlukMeydan }

        public static readonly string[] Adlar = { "Klasik", "Borç batağı", "Rakiplerin şehri", "Hızlı büyüme", "Serbest mod", "Günlük meydan okuma" };

        public static readonly string[] Aciklamalar =
        {
            "Kredini öde, rakiplerini geç, şirketi büyüt. Hedef sende.",
            "Kurulum kredisi 15 000 € (+%8 faiz), Mart–Temmuz beş taksit. Hedef: krediyi zamanında kapat. Zor.",
            "Üç platform da güçlü, Turbo Paket şehrin bütün dükkanlarıyla çalışıyor. Hedef: 90 gün içinde şehrin lideri ol (bir haftada her rakipten çok teslimat).",
            "Bir yatırımcı 6 000 € ek sermaye verdi. Hedef: 60 gün içinde 25 kurye ve son 7 günde günde ortalama 1 000 € gelir.",
            "250 000 € kasa, borç yok, hedef yok. Şehri kur, dene, eğlen.",
            "Herkes bugün aynı şehirle başlar. 14 gün sonra puanın: teslimat × itibar / 5. Her gün yeni şehir.",
        };

        public static Tur Secili = Tur.Klasik;
        /// <summary>0 sürüyor, 1 başarılı, 2 başarısız.</summary>
        public static int Durum;
        public static int GunlukTohum;

        public const int RakipGun = 90, BuyumeGun = 60, GunlukGun = 14;
        const long BorcBatagiBorcu = 6000000;     // 15 000 €
        const long BuyumeSermaye = 2400000;       // 6 000 €
        const long SerbestKasa = 100000000;       // 250 000 €

        public static void Sifirla() { Durum = 0; }

        static int Gun => GameClock.I != null ? GameClock.I.CompanyDay : 1;

        /// <summary>Bugünün tohumu (yyyyMMdd). Günlük meydan okumada harita bu tohumla kurulur.</summary>
        public static int BugununTohumu()
        {
            var d = DateTime.Now;
            return d.Year * 10000 + d.Month * 100 + d.Day;
        }

        /// <summary>StartGame'de dünya kurulmadan önce: haritanın tohumu (günlük meydan okumada sabit).</summary>
        public static int HaritaTohumu(int rastgele)
        {
            if (Secili != Tur.GunlukMeydan) return rastgele;
            GunlukTohum = BugununTohumu();
            UnityEngine.Random.InitState(GunlukTohum);
            return GunlukTohum;
        }

        /// <summary>StartGame: kurulum kredisinin tutarı (senaryoya göre; 0 = kredisiz).</summary>
        public static long AileBorcu(long varsayilan) =>
            Secili == Tur.BorcBatagi ? BorcBatagiBorcu : Secili == Tur.Serbest ? 0 : varsayilan;

        /// <summary>StartGame: sistemler kurulduktan sonra senaryonun başlangıç ayarları.</summary>
        public static void Baslat()
        {
            Durum = 0;
            var eco = EconomyManager.I;
            switch (Secili)
            {
                case Tur.HizliBuyume:
                    eco.money += BuyumeSermaye;   // sermaye: gelir sayılmaz (fiyat seviyesini şişirmesin)
                    EventManager.I.Notify($"Yatırımcı sermayesi kasaya girdi: {GameData.Ft(BuyumeSermaye)}.");
                    break;
                case Tur.Serbest:
                    eco.money = SerbestKasa;
                    break;
                case Tur.RakibinSehri:
                    var rv = RivalCompany.I;
                    if (rv != null)
                    {
                        foreach (var s in CityMap.I.Shops) if (!s.own) s.rivalContracted = true;   // en az Turbo Paket her dükkanda
                        foreach (var r in rv.Rakipler)
                        {
                            r.couriers = Mathf.Max(r.couriers, 14);
                            r.reputation = Mathf.Max(r.reputation, 4.0f);
                            r.quality = Mathf.Max(r.quality, 0.35f);
                            r.money += 3000000;
                        }
                    }
                    break;
            }
        }

        public static bool HedefVar => Secili != Tur.Klasik && Secili != Tur.Serbest;

        /// <summary>Hedef kutusunda ve raporda görünen kısa ilerleme.</summary>
        public static string IlerlemeYazisi
        {
            get
            {
                if (!HedefVar) return "";
                string ad = Adlar[(int)Secili];
                if (Durum == 1) return $"{ad}: tamamlandı ✓";
                if (Durum == 2) return $"{ad}: başarısız";
                switch (Secili)
                {
                    case Tur.BorcBatagi:
                        var fl = FamilyLoan.I;
                        return fl == null ? ad : $"{ad} · kalan borç {GameData.Ft(fl.Remaining)}";
                    case Tur.RakibinSehri:
                        var rv = RivalCompany.I;
                        return $"{ad} · {Mathf.Max(0, RakipGun - Gun)} gün · pazar payın %{(rv != null ? rv.MarketShare * 100f : 0f):0}" +
                               (rv != null && rv.Lideriz ? " · şu an liderisin" : " · lider değilsin");
                    case Tur.HizliBuyume:
                        return $"{ad} · {Mathf.Max(0, BuyumeGun - Gun)} gün · {CourierManager.I.Couriers.Count}/25 kurye · günde {GameData.Ft(OrtalamaGelir(7))} / {GameData.Ft(400000)}";
                    case Tur.GunlukMeydan:
                        return $"Günlük meydan okuma · {Mathf.Max(0, GunlukGun - Gun + 1)} gün · puan {Puan()}";
                }
                return ad;
            }
        }

        static long OrtalamaGelir(int gun)
        {
            var d = StatsHistory.I != null ? StatsHistory.I.Days : null;
            if (d == null || d.Count == 0) return 0;
            int n = Mathf.Min(gun, d.Count);
            long t = 0;
            for (int i = d.Count - n; i < d.Count; i++) t += (long)d[i].income;
            return t / n;
        }

        static int Teslimatlar()
        {
            var d = StatsHistory.I != null ? StatsHistory.I.Days : null;
            if (d == null) return 0;
            int t = 0;
            foreach (var r in d) t += r.deliveries;
            return t;
        }

        public static int Puan() => Mathf.RoundToInt(Teslimatlar() * CompanyProfile.Reputation / 5f);

        static string EnIyiKey => "KuryeTycoon.Gunluk." + GunlukTohum;

        /// <summary>Her gece (EventManager.OnNewDay): hedef tuttu mu, süre doldu mu.</summary>
        public static void YeniGun()
        {
            if (!HedefVar || Durum != 0 || GameClock.I == null) return;
            int gun = Gun;
            switch (Secili)
            {
                case Tur.BorcBatagi:
                    if (FamilyLoan.I != null && FamilyLoan.I.Finished && !FamilyLoan.I.Failed) Bitir(true, "Kredinin tamamını ödedin. Şehir artık seni ciddiye alıyor.");
                    else if (FamilyLoan.I != null && FamilyLoan.I.Failed) Bitir(false, "Taksitleri ödeyemedin.");
                    break;
                case Tur.RakibinSehri:
                    var rv = RivalCompany.I;
                    if (rv != null && gun >= 14 && rv.Lideriz) Bitir(true, $"Geçen hafta her rakipten çok teslimat yaptın (pazar payın %{rv.MarketShare * 100f:0}): şehrin lideri artık sensin.");
                    else if (gun > RakipGun) Bitir(false, $"90 gün doldu, liderliği alamadın (pazar payın %{(rv != null ? rv.MarketShare * 100f : 0f):0}).");
                    break;
                case Tur.HizliBuyume:
                    if (CourierManager.I.Couriers.Count >= 25 && OrtalamaGelir(7) >= 400000) Bitir(true, "25 kurye ve günde 1 000 € gelir: yatırımcı yeni tur için kapıda.");
                    else if (gun > BuyumeGun) Bitir(false, "60 gün doldu: yatırımcı ek tur yapmayacak. Şirket senin, oynamaya devam edebilirsin.");
                    break;
                case Tur.GunlukMeydan:
                    if (gun > GunlukGun)
                    {
                        int p = Puan();
                        int enIyi = PlayerPrefs.GetInt(EnIyiKey, 0);
                        bool rekor = p > enIyi;
                        if (rekor) { PlayerPrefs.SetInt(EnIyiKey, p); PlayerPrefs.Save(); }
                        Bitir(true, $"Bugünün şehrinde 14 gün: {Teslimatlar()} teslimat, itibar {CompanyProfile.Reputation:0.0}. Puanın {p}." +
                                    (rekor ? " Bu bilgisayardaki en iyi puan!" : $" Bu bilgisayardaki en iyi: {enIyi}.") +
                                    " Yarın yeni bir şehir gelecek.");
                    }
                    break;
            }
        }

        static void Bitir(bool basarili, string metin)
        {
            Durum = basarili ? 1 : 2;
            var ev = new GameEvent(basarili ? $"Senaryo tamamlandı: {Adlar[(int)Secili]}" : $"Senaryo başarısız: {Adlar[(int)Secili]}",
                metin + "\n\nOyun devam ediyor; istersen şirketi büyütmeyi sürdür.")
                .Ozet("Gün", Gun.ToString())
                .Ozet("Kurye", CourierManager.I.Couriers.Count.ToString())
                .Ozet("İtibar", $"{CompanyProfile.Reputation:0.0}", CompanyProfile.Reputation >= 4f ? 1 : 0)
                .Ozet("Sonuç", basarili ? "Başarılı" : "Başarısız", basarili ? 1 : 2);
            ev.Add("Tamam", null);
            EventManager.I.Push(ev);
        }
    }
}
