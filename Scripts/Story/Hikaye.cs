using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KuryeTycoon
{
    [Serializable]
    public class HikayeVeri
    {
        public bool[] tamam = new bool[7];
        public bool final;
    }

    [Serializable]
    public class HikayeSave
    {
        public bool giris, orta, liderlik, bitti;
    }

    /// <summary>
    /// Hikâye kampanyası (Hedefler > Hikâye). Her şehir bir bölüm (Marka.Sehirler sırasıyla, 7 bölüm): üç hedef, bir rakip.
    /// Karşında Kovács Viktor var: her şehirde lider platformun (RivalCompany.Lider) arkasındaki Turbo Grup'un patronu.
    /// Bölüm açılınca Viktor'un mesajı (kart), 20. günde bir laf sokması, pazarda onu geçince öfkeli bir mesaj, üç hedef tamamlanınca
    /// bölüm sonu kartı ve kalıcı ödül (+2 marka puanı, her bölüm bir kez). Budapeşte bölümü finaldir: bitince oyunun sonu kartı
    /// (oynamaya devam edilebilir). Şirketi bölümü bitirmeden satarsan o bölüm yarım kalır.
    /// Bölüm ilerlemesi bilgisayarda (hikaye.json), bölüm içindeki mesajlar kayıtta. Sadece Klasik oyunda; otomatik testte kapalı.
    /// </summary>
    public class Hikaye : MonoBehaviour
    {
        public static Hikaye I { get; private set; }

        public class Hedef
        {
            public Func<string> adF;            // para birimi ayarı değişebilir: ad her seferinde yazılır
            public string ad => adF();
            public Func<bool> tamam;
            public Func<string> durum;
        }

        public class Bolum
        {
            public string baslik, giris, orta, liderlik, bitis;
            public Hedef[] hedefler;
        }

        public const string Patron = "Kovács Viktor";
        public const int BolumOdulu = 2;

        static HikayeVeri veri;
        static string Dosya => Path.Combine(Application.persistentDataPath, "hikaye.json");

        public bool giris, orta, liderlik, bitti;
        float zamanlayici;

        void Awake() { I = this; }

        public void Init() { giris = orta = liderlik = bitti = false; }

        // ------------------------------------------------------------------ kalıcı ilerleme

        public static HikayeVeri Veri
        {
            get
            {
                if (veri == null)
                {
                    try { if (File.Exists(Dosya)) veri = JsonUtility.FromJson<HikayeVeri>(File.ReadAllText(Dosya)); }
                    catch (Exception e) { Debug.LogWarning("Hikâye dosyası okunamadı: " + e.Message); }
                    if (veri == null) veri = new HikayeVeri();
                    if (veri.tamam == null || veri.tamam.Length < 7)
                    {
                        var t = new bool[7];
                        if (veri.tamam != null) Array.Copy(veri.tamam, t, Mathf.Min(7, veri.tamam.Length));
                        veri.tamam = t;
                    }
                }
                return veri;
            }
        }

        static void VeriKaydet()
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(Dosya, JsonUtility.ToJson(Veri, true));
            }
            catch (Exception e) { Debug.LogWarning("Hikâye dosyası yazılamadı: " + e.Message); }
        }

        public static int TamamSayisi { get { int n = 0; foreach (bool b in Veri.tamam) if (b) n++; return n; } }

        public static bool Etkin => !TestModu.Aktif && Senaryo.Secili == Senaryo.Tur.Klasik;

        public static int BolumNo => Mathf.Clamp(Marka.AktifSehir, 0, 6);
        public static Bolum Aktif => Bolumler[BolumNo];

        static string R => RivalCompany.I != null && RivalCompany.I.Lider != null ? RivalCompany.I.Lider.ad : "Turbo Paket";

        /// <summary>Metindeki {R}'yi şehrin lider rakibinin adıyla değiştirir.</summary>
        public static string Yaz(string metin) => metin.Replace("{R}", R);

        // ------------------------------------------------------------------ yardımcılar (hedefler)

        static long EUR(long euro) => euro * 400;
        static float Pay => RivalCompany.I != null ? RivalCompany.I.MarketShare : 0f;
        static int Kurye => CourierManager.I != null ? CourierManager.I.Couriers.Count : 0;

        static int Dukkan(Func<Shop, bool> f)
        {
            int n = 0;
            if (CityMap.I != null) foreach (var s in CityMap.I.Shops) if (s.contracted && !s.own && !s.kargo && f(s)) n++;
            return n;
        }

        static string Sayi(long v, long hedef) => $"{Math.Min(v, hedef):#,0} / {hedef:#,0}".Replace(',', ' ');
        static string Yuzde(float v, float hedef) => $"%{Mathf.Min(v, hedef) * 100f:0} / %{hedef * 100f:0}";

        static Hedef H(string ad, Func<bool> tamam, Func<string> durum) => new Hedef { adF = () => ad, tamam = tamam, durum = durum };
        static Hedef H(Func<string> ad, Func<bool> tamam, Func<string> durum) => new Hedef { adF = ad, tamam = tamam, durum = durum };

        static Hedef PayHedefi(float p) => H($"Pazar payın %{p * 100f:0}'e ulaşsın", () => Pay >= p, () => Yuzde(Pay, p));
        static Hedef KuryeHedefi(int n) => H($"{n} kurye", () => Kurye >= n, () => Sayi(Kurye, n));
        static Hedef DegerHedefi(long euro) => H(() => $"Şirket değeri {GameData.Ft(EUR(euro))}", () => Marka.SirketDegeri >= EUR(euro),
                                                  () => $"{GameData.Ft(Math.Min(Marka.SirketDegeri, EUR(euro)))} / {GameData.Ft(EUR(euro))}");

        // ------------------------------------------------------------------ bölümler

        static Bolum[] bolumler;
        /// <summary>Bölümler (ilk kullanımda kurulur: Unity'nin yükleme sırasında statik kurucu çalışmasın).</summary>
        public static Bolum[] Bolumler => bolumler ?? (bolumler = Kur());

        static Bolum[] Kur() => new[]
        {
            new Bolum
            {
                baslik = "1. Bölüm · Nyíregyháza: Aile borcu",
                giris = "\"Bir bisiklet ve bir telefonla kurye şirketi mi? Şehirde {R} varken mi? Sana bir ay veriyorum.\" — " + Patron + ", Turbo Grup",
                orta = "\"Hâlâ ayakta mısın? Annenin parası bitince görüşürüz.\" — " + Patron,
                liderlik = "\"Bu şehir küçük, alabilirsin. Debrecen'de görüşürüz.\" — " + Patron,
                bitis = "Ailenin borcu kapandı, şehirde artık sen de varsın. Viktor ilk kez telefonunu açıp seni aradı: \"Satarsan iyi para veririm.\"",
                hedefler = new[]
                {
                    H("Aile kredisini geri öde", () => FamilyLoan.I != null && (FamilyLoan.I.Toplam <= 0 || (FamilyLoan.I.Finished && !FamilyLoan.I.Failed)),
                      () => FamilyLoan.I == null || FamilyLoan.I.Toplam <= 0 ? "borç yok" : $"kalan {GameData.Ft(FamilyLoan.I.Remaining)}"),
                    H("8 sözleşmeli dükkan", () => Dukkan(s => true) >= 8, () => Sayi(Dukkan(s => true), 8)),
                    PayHedefi(0.40f),
                }
            },
            new Bolum
            {
                baslik = "2. Bölüm · Debrecen: Kampüs savaşı",
                giris = "\"Üniversite şehri öğrencilerle dolu, hepsi {R} uygulamasını kullanıyor. Burada taşra numaraların işlemez.\" — " + Patron,
                orta = "\"Öğrencilere kampanya mı yaptın? Ben de yaptım. İki katını.\" — " + Patron,
                liderlik = "\"Öğrenciler vefasızdır. Gelecek dönem seni unuturlar.\" — " + Patron,
                bitis = "Kampüste herkesin telefonunda artık senin uygulaman var. Viktor'un yönetim kurulu toplantısında adın geçti.",
                hedefler = new[]
                {
                    H("5 000 teslimat", () => Milestones.TotalDeliveries >= 5000, () => Sayi(Milestones.TotalDeliveries, 5000)),
                    KuryeHedefi(25),
                    PayHedefi(0.45f),
                }
            },
            new Bolum
            {
                baslik = "3. Bölüm · Miskolc: Fabrikalar şehri",
                giris = "\"Miskolc'te para restoranlarda değil, fabrikalarda. Onlar da benimle çalışıyor.\" — " + Patron,
                orta = "\"İşletmelere teslimat mı? Kargocular seni yer bitirir.\" — " + Patron,
                liderlik = "\"Rakamlarına baktım. Fena değil. Hâlâ küçüksün.\" — " + Patron,
                bitis = "Fabrikalar, ofisler ve restoranlar seninle çalışıyor. Viktor bir gazeteye \"yeni nesil rakiplerimiz\" diye senden bahsetti.",
                hedefler = new[]
                {
                    H("3 bölge açık", () => CityMap.I != null && CityMap.I.UnlockLevel >= 3, () => Sayi(CityMap.I != null ? CityMap.I.UnlockLevel : 1, 3)),
                    H("3 işletme teslimatı ortağı", () => IsletmeTeslimati.I != null && IsletmeTeslimati.I.Ortaklar.Count >= 3,
                      () => Sayi(IsletmeTeslimati.I != null ? IsletmeTeslimati.I.Ortaklar.Count : 0, 3)),
                    H(() => $"Günlük kâr (7 gün ort.) {GameData.Ft(EUR(1500))}", () => Buyume.OrtalamaNet(7) >= EUR(1500),
                      () => $"{GameData.Ft(Math.Max(0L, Math.Min(Buyume.OrtalamaNet(7), EUR(1500))))} / {GameData.Ft(EUR(1500))}"),
                }
            },
            new Bolum
            {
                baslik = "4. Bölüm · Szeged: Güneş ve yıldızlar",
                giris = "\"Szeged'de müşteriler seçicidir. Bir yıldız düşersen kimse sana sipariş vermez.\" — " + Patron,
                orta = "\"Puanlarına bakıyorum. Benim kuryelerim daha güler yüzlü.\" — " + Patron,
                liderlik = "\"Müşterilerim neden sana geçiyor? ... Bunu araştıracağım.\" — " + Patron,
                bitis = "Şehrin en sevilen teslimat uygulaması seninki. Viktor, {R} için yeni bir pazarlama müdürü aramaya başladı.",
                hedefler = new[]
                {
                    H("İtibar 4,5", () => CompanyProfile.Reputation >= 4.5f, () => $"{Mathf.Min(CompanyProfile.Reputation, 4.5f):0.00} / 4,50"),
                    H("Bir sezon etkinliğinin hedefini tuttur", () => SezonEtkinlikleri.I != null && SezonEtkinlikleri.I.tamamlanan >= 1,
                      () => Sayi(SezonEtkinlikleri.I != null ? SezonEtkinlikleri.I.tamamlanan : 0, 1)),
                    PayHedefi(0.50f),
                }
            },
            new Bolum
            {
                baslik = "5. Bölüm · Pécs: Lezzet yolu",
                giris = "\"Pécs'in en iyi restoranları benimle münhasır çalışıyor. Sana kırıntılar kalır.\" — " + Patron,
                orta = "\"Kendi mutfağını mı açtın? Kurye şirketi yemek mi yapar artık?\" — " + Patron,
                liderlik = "\"Restoranlarım tek tek sana geçiyor. Bu kişisel bir mesele oldu.\" — " + Patron,
                bitis = "Şehrin en iyi mutfakları ve kendi markaların seninle. Viktor ilk kez bir toplantıda sesini yükseltti.",
                hedefler = new[]
                {
                    H("Sanal mutfakta 2 marka", () => SanalMutfak.I != null && SanalMutfak.I.Acik.Count >= 2, () => Sayi(SanalMutfak.I != null ? SanalMutfak.I.Acik.Count : 0, 2)),
                    H("5 münhasır dükkan", () => Dukkan(s => s.exclusive) >= 5, () => Sayi(Dukkan(s => s.exclusive), 5)),
                    KuryeHedefi(50),
                }
            },
            new Bolum
            {
                baslik = "6. Bölüm · Győr: Teknoloji hamlesi",
                giris = "\"Győr'de fabrikalar robotla çalışır. Senin bisikletli kuryelerin burada müzelik.\" — " + Patron,
                orta = "\"Drone mu? Benim mühendislerim ondan iyisini yapıyor. Yakında görürsün.\" — " + Patron,
                liderlik = "\"Yatırımcılarım bana seni soruyor. Bu hoşuma gitmiyor.\" — " + Patron,
                bitis = "Laboratuvarın, bölge müdürlerin ve değerinle artık gerçek bir teknoloji şirketisin. Viktor sana Budapeşte'de buluşma teklif etti.",
                hedefler = new[]
                {
                    H("4 Ar-Ge projesi", () => ArGe.I != null && ArGe.I.TamamSayisi >= 4, () => Sayi(ArGe.I != null ? ArGe.I.TamamSayisi : 0, 4)),
                    H("2 bölge müdürü", () => BolgeMudurleri.I != null && BolgeMudurleri.I.Mudurler.Count >= 2,
                      () => Sayi(BolgeMudurleri.I != null ? BolgeMudurleri.I.Mudurler.Count : 0, 2)),
                    DegerHedefi(500000),
                }
            },
            new Bolum
            {
                baslik = "7. Bölüm · Budapeşte: Başkent (final)",
                giris = "\"Başkente hoş geldin. Burası benim evim; {R} bu şehirde doğdu. Burada kaybedersen her şeyi kaybedersin.\" — " + Patron,
                orta = "\"Bütün şehirlerde seni izledim. Ama Budapeşte başka. Burada kimse beni geçemedi.\" — " + Patron,
                liderlik = "\"... Tebrikler. Ama bu daha bitmedi.\" — " + Patron,
                bitis = "Budapeşte'nin lider teslimat şirketi sensin. Viktor son kez aradı: \"Bir bisiklet ve bir telefonla başladığını duydum. İnanmamıştım.\"",
                hedefler = new[]
                {
                    H("Pazar lideri ol (%55 pay)", () => Pay >= 0.55f && RivalCompany.I != null && RivalCompany.I.Lideriz, () => Yuzde(Pay, 0.55f)),
                    H("Bir rakip platformu satın al", () => RakipAlindi, () => RakipAlindi ? "1 / 1" : "0 / 1"),
                    DegerHedefi(2000000),
                }
            },
        };

        static bool RakipAlindi
        {
            get
            {
                if (RivalCompany.I == null) return false;
                foreach (var r in RivalCompany.I.Rakipler) if (r.satinAlindi) return true;
                return false;
            }
        }

        public static int TamamHedef(Bolum b)
        {
            int n = 0;
            foreach (var h in b.hedefler) if (h.tamam()) n++;
            return n;
        }

        // ------------------------------------------------------------------ akış

        void Update()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || GameClock.I == null || EventManager.I == null || !Etkin) return;
            zamanlayici -= Time.unscaledDeltaTime;
            if (zamanlayici > 0f) return;
            zamanlayici = 1f;

            var b = Aktif;
            int gun = GameClock.I.CompanyDay;
            if (!giris)
            {
                giris = true;
                EventManager.I.Push(new GameEvent(b.baslik,
                    Yaz(b.giris) + "\n\nBölümün hedefleri (Hedefler > Hikâye):\n• " + string.Join("\n• ", HedefAdlari(b)) +
                    $"\n\nHepsini tamamlarsan bölüm biter: +{BolumOdulu} marka puanı (kalıcı)."));
                return;
            }
            if (bitti) return;

            if (TamamHedef(b) == b.hedefler.Length)
            {
                bitti = true;
                bool ilkKez = !Veri.tamam[BolumNo];
                Veri.tamam[BolumNo] = true;
                bool finalMi = BolumNo == 6;
                if (finalMi) Veri.final = true;
                VeriKaydet();
                if (ilkKez) Marka.PuanEkle(BolumOdulu);
                CompanyProfile.AddReputation(0.05f);
                var kart = new GameEvent($"Bölüm tamamlandı: {b.baslik}",
                    Yaz(b.bitis) + (ilkKez ? $"\n\nÖdül: +{BolumOdulu} marka puanı (kalıcı)." : "\n\nBu bölümü daha önce de bitirmiştin.") +
                    (finalMi ? "" : $"\n\nHazır olunca şirketi sat ya da holdinge kat (Şirket > Gelişim / Holding): sonraki bölüm {Marka.Sehirler[Mathf.Min(BolumNo + 1, 6)]}'de."));
                kart.Ozet("Bölüm", $"{BolumNo + 1} / 7", 1);
                kart.Ozet("Biten bölümler", $"{TamamSayisi} / 7", 1);
                EventManager.I.Push(kart);
                GameSounds.Play(Sfx.Buyuk);
                if (finalMi)
                    EventManager.I.Push(new GameEvent("Son",
                        "Nyíregyháza'da bir bisiklet, bir telefon ve ailenin birikimiyle başladın. Bugün Budapeşte'nin lider teslimat şirketi senin.\n\n" +
                        $"Kovács Viktor ve Turbo Grup artık senin rakibin değil, geçmişin. Holdinginde {Holding.IstirakSayisi} iştirak var, " +
                        $"{Marka.Veri.satilan} şirket sattın.\n\nOynamaya devam edebilirsin: şirketini büyütmeye, holdingini genişletmeye, efsane kuryeleri toplamaya. " +
                        "Teşekkürler!").Add("Devam", null));
                return;
            }

            if (!orta && gun >= 20)
            {
                orta = true;
                EventManager.I.NotifyQuiet(Yaz(b.orta));
                EventManager.I.Notify($"Hikâye: {TamamHedef(b)}/{b.hedefler.Length} hedef tamam (Hedefler > Hikâye).");
            }
            if (!liderlik && gun >= 7 && RivalCompany.I != null && RivalCompany.I.Lideriz)
            {
                liderlik = true;
                EventManager.I.Push(new GameEvent($"{Patron} aradı", Yaz(b.liderlik) +
                    $"\n\nBu hafta şehirde en çok teslimatı sen yaptın; {R} artık ikinci."));
            }
        }

        static List<string> HedefAdlari(Bolum b)
        {
            var l = new List<string>();
            foreach (var h in b.hedefler) l.Add(h.ad);
            return l;
        }

        // ------------------------------------------------------------------ kayıt

        public HikayeSave Kaydet() => new HikayeSave { giris = giris, orta = orta, liderlik = liderlik, bitti = bitti };

        public void Yukle(HikayeSave s)
        {
            Init();
            if (s == null)
            {
                giris = true;   // eski kayıt: giriş kartı açılmasın, hedefler yine sayılır
                return;
            }
            giris = s.giris; orta = s.orta; liderlik = s.liderlik; bitti = s.bitti;
        }
    }
}
