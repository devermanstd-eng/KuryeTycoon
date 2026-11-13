using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Kurye adayının kayıt sürecindeki aşaması (sonunda hesap açılır). Egitim artık kullanılmıyor (eski kayıtlar için duruyor).</summary>
    public enum KayitAsamasi { Kayit, Belge, Kontrol, Egitim, Ekipman }

    /// <summary>Adayın hangi yoldan geldiği.</summary>
    public enum KayitTuru { Bireysel, OkulKooperatifi, CalismaIzinli, Tavsiye, EskiKurye }

    /// <summary>Kayıt sürecindeki bir aday.</summary>
    [System.Serializable]
    public class KayitAdayi
    {
        public CourierCandidate aday;
        public KayitTuru tur;
        public KayitAsamasi asama;
        public float asamaBitis;       // bu aşamanın biteceği oyun dakikası
        public float kayitZamani;
        public int tavsiyeEden;        // tavsiye eden kuryenin courierId'si (0: yok)
        public string tavsiyeEdenAd = "";
        public bool hesapBekliyor;     // her şey tamam, kurye hesabı sınırı dolu olduğu için bekliyor
        public float beklemeBasi;
    }

    [System.Serializable]
    public class KayitSave
    {
        public List<KayitAdayi> adaylar = new List<KayitAdayi>();
        public bool[] acik;
        public int[] bekleme;
        public int kontrol = 1, yeniBonus, tavsiyePrim, puanKurali, iptalKurali;
        public int reklamBitis, ilanBitis;
        public float sonrakiKayit, sonBeklemeAkisi;
        public int[] haftaBirakan, gecenHaftaBirakan;
        public int haftaKayit, haftaAktif, haftaElenen, haftaIlkHafta, haftaAski;
        public int gecenHaftaKayit, gecenHaftaAktif, gecenHaftaElenen, gecenHaftaIlkHafta, gecenHaftaAski;
    }

    /// <summary>
    /// Kurye kayıtları (eski "İşe Alım"ın yerine). Gerçekteki gibi kimse "işe alınmaz": insanlar uygulamaya kendileri
    /// kaydolur ve bir süreçten geçer; her aşamada bir kısmı vazgeçer. Oyuncu kişileri tek tek seçmez, kuralları belirler.
    ///
    ///  Aşamalar: Kayıt formu → Belge yükleme → Kontrol → Ekipman alma → hesap açılır → ilk hafta.
    ///  Aday türleri: bireysel (egyéni vállalkozó), okul kooperatifi (öğrenci, belgesi hızlı), çalışma izinli (çok çalışır,
    ///   kontrolü uzun), tavsiyeyle gelen (az vazgeçer, tavsiye edene prim), geri dönen eski kurye (kontrolü kısa, tecrübeli).
    ///  Oyuncunun düğmeleri:
    ///   • Kayıt araç türüne göre açık/kapalı: kapalıyken gelenler bekleme listesine yazılır, açınca saatte bir sürece girer.
    ///   • Kontrol sıkılığı: hızlı (çabuk ama özensiz adaylar da geçer) · normal · sıkı (yavaş, özensizler elenir).
    ///   • Kampanyalar: ilk 30 teslimata paket başı bonus, tavsiye primi, sosyal medya reklamı, üniversite ilanı.
    ///   • Hesap kuralları: puanı düşen ya da çok teslim alma iptali yapan kuryenin hesabı 7 gün askıya alınır;
    ///     60 gün içinde 3. askıda hesap kapanır. Kuryeler ayrıca elle askıya alınabilir ya da hesapları kapatılabilir.
    ///  Hesap açılması kurye hesabı sınırına (Gelişim > platform paketi) bağlı: sınır doluysa hazır adaylar bekler.
    /// </summary>
    public class KayitSistemi : MonoBehaviour
    {
        public static KayitSistemi I { get; private set; }

        public readonly List<KayitAdayi> Adaylar = new List<KayitAdayi>();

        [Header("Kayıt politikası")]
        public bool[] acik = { true, true, true, true };      // VehicleKind sırasıyla
        public int[] bekleme = new int[4];                      // kayıt kapalıyken bekleme listesine yazılan
        public int kontrol = 1;                                 // 0 hızlı · 1 normal · 2 sıkı
        public int yeniBonus;                                   // 0 yok · 1 · 2 (ilk 30 teslimata paket başı bonus)
        public int tavsiyePrim;                                 // 0 yok · 1 · 2
        public int puanKurali;                                  // 0 kapalı · 1 · 2 · 3
        public int iptalKurali;                                 // 0 kapalı · 1 · 2
        public int reklamBitis = -1, ilanBitis = -1;            // kampanyanın sürdüğü son gün

        public static readonly string[] KontrolAdlari = { "Hızlı", "Normal", "Sıkı" };
        public static readonly long[] BonusTutar = { 0, 150, 300 };
        public static readonly long[] PrimTutar = { 0, 6000, 12000 };
        public static readonly float[] PuanSinir = { 0f, 4.0f, 4.3f, 4.5f };
        public static readonly int[] IptalSinir = { 0, 8, 5 };
        public const int BonusAdet = 30;          // yeni kurye bonusu ve tavsiye primi bu kadar teslimata kadar
        public const int AskiGun = 7;
        public const int BeklemeSiniri = 60;

        // Haftalık sayılar (Pazartesi sıfırlanır; geçen hafta pencerede gösterilir)
        public int[] haftaBirakan = new int[5], gecenHaftaBirakan = new int[5];
        public int haftaKayit, haftaAktif, haftaElenen, haftaIlkHafta, haftaAski;
        public int gecenHaftaKayit, gecenHaftaAktif, gecenHaftaElenen, gecenHaftaIlkHafta, gecenHaftaAski;

        float sonrakiKayit, sonBeklemeAkisi;
        bool ready;

        static string Ft(long v) => GameData.Ft(v);
        static int Day => GameClock.I.Day;

        void Awake() { I = this; }

        public static string AsamaAdi(KayitAsamasi a)
        {
            switch (a)
            {
                case KayitAsamasi.Kayit: return "Kayıt formu";
                case KayitAsamasi.Belge: return "Belge yükleme";
                case KayitAsamasi.Kontrol: return "Kontrol";
                case KayitAsamasi.Egitim: return "Eğitim";
                default: return "Ekipman alma";
            }
        }

        public static string TurAdi(KayitTuru t)
        {
            switch (t)
            {
                case KayitTuru.OkulKooperatifi: return "Okul kooperatifi";
                case KayitTuru.CalismaIzinli: return "Çalışma izinli";
                case KayitTuru.Tavsiye: return "Tavsiyeyle";
                case KayitTuru.EskiKurye: return "Geri dönen kurye";
                default: return "Bireysel (egyéni vállalkozó)";
            }
        }

        public static string TurAciklama(KayitTuru t)
        {
            switch (t)
            {
                case KayitTuru.OkulKooperatifi: return "Öğrenci, okul kooperatifi üzerinden çalışır: belgeleri hızlı, sınav haftalarında az çalışır.";
                case KayitTuru.CalismaIzinli: return "AB dışından, çalışma izniyle: çok çalışır ve az seçicidir, ama izin kontrolü uzun sürer.";
                case KayitTuru.Tavsiye: return "Bir kuryenin arkadaşı: süreçte daha az vazgeçer. 30 teslimata ulaşınca tavsiye edene prim ödenir.";
                case KayitTuru.EskiKurye: return "Daha önce kuryelik yapmış: belgesi sistemde, kontrolü yarı sürede biter, tecrübeli başlar.";
                default: return "Kendi vergi numarasıyla çalışan serbest kurye (egyéni vállalkozó).";
            }
        }

        // ================================================================ kurulum ve kayıt

        public void Init()
        {
            Adaylar.Clear();
            for (int i = 0; i < 4; i++) { acik[i] = true; bekleme[i] = 0; }
            // Oyunun başı: Turbo Paket'ten eski vardiya arkadaşların seninle gelmek istiyor. Belgeleri hazır, ilk gün
            // hesapları açılır (şirket boş kurye kadrosuyla başlayıp ilk günleri iptallerle geçirmesin). Birkaçı da süreçte.
            for (int i = 0; i < 7; i++)
            {
                var a = YeniAday();
                if (a == null) continue;
                float now = GameClock.I.TotalMinutes;
                if (i < 4)
                {
                    a.tur = KayitTuru.EskiKurye;
                    a.aday.level = Mathf.Max(a.aday.level, 2);
                    if (a.aday.occupation == Occupation.Ogrenci) a.aday.occupation = Occupation.TamZamanli;
                    a.aday.note = GameData.ApplicationNote(a.aday.occupation, a.aday.level);
                    a.asama = KayitAsamasi.Ekipman;
                    a.asamaBitis = now + Random.Range(60f, 300f) + 120f * i;   // ilk gün öğlene kadar
                }
                else
                {
                    a.asama = i == 4 ? KayitAsamasi.Ekipman : KayitAsamasi.Kontrol;
                    a.asamaBitis = now + Random.Range(600f, 1800f);
                }
                Adaylar.Add(a);
            }
            Planla();
            sonBeklemeAkisi = GameClock.I.TotalMinutes;
            if (!ready) { GameClock.I.OnNewDay += OnNewDay; ready = true; }
        }

        public KayitSave CaptureSave() => new KayitSave
        {
            adaylar = new List<KayitAdayi>(Adaylar), acik = (bool[])acik.Clone(), bekleme = (int[])bekleme.Clone(),
            kontrol = kontrol, yeniBonus = yeniBonus, tavsiyePrim = tavsiyePrim, puanKurali = puanKurali, iptalKurali = iptalKurali,
            reklamBitis = reklamBitis, ilanBitis = ilanBitis, sonrakiKayit = sonrakiKayit, sonBeklemeAkisi = sonBeklemeAkisi,
            haftaBirakan = (int[])haftaBirakan.Clone(), gecenHaftaBirakan = (int[])gecenHaftaBirakan.Clone(),
            haftaKayit = haftaKayit, haftaAktif = haftaAktif, haftaElenen = haftaElenen, haftaIlkHafta = haftaIlkHafta, haftaAski = haftaAski,
            gecenHaftaKayit = gecenHaftaKayit, gecenHaftaAktif = gecenHaftaAktif, gecenHaftaElenen = gecenHaftaElenen,
            gecenHaftaIlkHafta = gecenHaftaIlkHafta, gecenHaftaAski = gecenHaftaAski
        };

        /// <summary>Init'ten sonra çağrılır. Eski kayıtlarda (null) Init'teki başlangıç adayları kalır.</summary>
        public void RestoreSave(KayitSave s)
        {
            if (s == null) return;
            Adaylar.Clear();
            if (s.adaylar != null)
                foreach (var a in s.adaylar)
                {
                    if (a == null || a.aday == null) continue;
                    if (a.asama == KayitAsamasi.Egitim) a.asama = KayitAsamasi.Ekipman;   // eğitim aşaması kaldırıldı
                    Adaylar.Add(a);
                }
            if (s.acik != null) for (int i = 0; i < 4 && i < s.acik.Length; i++) acik[i] = s.acik[i];
            if (s.bekleme != null) for (int i = 0; i < 4 && i < s.bekleme.Length; i++) bekleme[i] = s.bekleme[i];
            kontrol = Mathf.Clamp(s.kontrol, 0, 2);
            yeniBonus = Mathf.Clamp(s.yeniBonus, 0, 2);
            tavsiyePrim = Mathf.Clamp(s.tavsiyePrim, 0, 2);
            puanKurali = Mathf.Clamp(s.puanKurali, 0, 3);
            iptalKurali = Mathf.Clamp(s.iptalKurali, 0, 2);
            reklamBitis = s.reklamBitis; ilanBitis = s.ilanBitis;
            sonrakiKayit = s.sonrakiKayit > 0f ? s.sonrakiKayit : GameClock.I.TotalMinutes + 60f;
            sonBeklemeAkisi = s.sonBeklemeAkisi;
            if (s.haftaBirakan != null && s.haftaBirakan.Length == 5) haftaBirakan = s.haftaBirakan;
            if (s.gecenHaftaBirakan != null && s.gecenHaftaBirakan.Length == 5) gecenHaftaBirakan = s.gecenHaftaBirakan;
            haftaKayit = s.haftaKayit; haftaAktif = s.haftaAktif; haftaElenen = s.haftaElenen; haftaIlkHafta = s.haftaIlkHafta; haftaAski = s.haftaAski;
            gecenHaftaKayit = s.gecenHaftaKayit; gecenHaftaAktif = s.gecenHaftaAktif; gecenHaftaElenen = s.gecenHaftaElenen;
            gecenHaftaIlkHafta = s.gecenHaftaIlkHafta; gecenHaftaAski = s.gecenHaftaAski;
        }

        public bool ReklamAktif => reklamBitis >= Day;
        public bool IlanAktif => ilanBitis >= Day;
        public long YeniKuryeBonusu => BonusTutar[Mathf.Clamp(yeniBonus, 0, 2)];
        public long TavsiyePrimi => PrimTutar[Mathf.Clamp(tavsiyePrim, 0, 2)];
        public float MinPuan => PuanSinir[Mathf.Clamp(puanKurali, 0, 3)];
        public int MaxIptal => IptalSinir[Mathf.Clamp(iptalKurali, 0, 2)];

        /// <summary>Her şeyi tamam, kurye hesabı sınırı dolu olduğu için bekleyen aday sayısı.</summary>
        public int HesapBekleyen { get { int n = 0; foreach (var a in Adaylar) if (a.hesapBekliyor) n++; return n; } }
        public int BeklemeListesi { get { int n = 0; foreach (var b in bekleme) n += b; return n; } }

        /// <summary>Hesap sınırı dolu ve hesap bekleyen yeterince kişi var: yeni kayıtlar kendiliğinden bekleme listesine yazılır.</summary>
        public bool Dolu => UpgradeManager.I != null && UpgradeManager.I.CourierCapFull && HesapBekleyen >= DoluEsigi;
        public const int DoluEsigi = 4;

        public int AsamadakiSayi(KayitAsamasi s)
        {
            int n = 0;
            foreach (var a in Adaylar) if (a.asama == s) n++;
            return n;
        }

        /// <summary>Kayıt hızı çarpanı (1 ≈ günde 7 kayıt).</summary>
        public float KayitCarpani
        {
            get
            {
                float rep = 0.5f + 0.15f * CompanyProfile.Reputation;
                float bonus = Mathf.Lerp(0.8f, 1.3f, (EconomyManager.I.bonusMultiplier - 0.5f) / 1.5f);
                float kampanya = (ReklamAktif ? 1.6f : 1f) * (1f + 0.15f * yeniBonus) * (1f + 0.08f * tavsiyePrim) * (IlanAktif ? 1.25f : 1f);
                return rep * bonus * kampanya * UpgradeManager.I.ApplicationMultiplier * Difficulty.ApplicationRate;
            }
        }

        void Planla()
        {
            // Ortalama ~3,5 saatte bir kayıt (günde ~7); itibar, bonus ve kampanyalar artırır
            sonrakiKayit = GameClock.I.TotalMinutes + Random.Range(120f, 300f) / Mathf.Max(0.2f, KayitCarpani);
        }

        void Update()
        {
            if (!ready || GameClock.I == null || GameClock.I.DeltaMinutes <= 0f) return;
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            float now = GameClock.I.TotalMinutes;

            if (now >= sonrakiKayit)
            {
                YeniKayit();
                Planla();
            }

            // Bekleme listesi: kayıt açılan araç türünde saatte bir kişi sürece girer
            if (now - sonBeklemeAkisi >= 60f)
            {
                sonBeklemeAkisi = now;
                for (int v = 0; v < 4; v++)
                    if (acik[v] && bekleme[v] > 0 && !Dolu)
                    {
                        bekleme[v]--;
                        var a = YeniAday((VehicleKind)v);
                        if (a != null) { a.asamaBitis = now + Sure(a); Adaylar.Add(a); haftaKayit++; }
                    }
            }

            for (int i = Adaylar.Count - 1; i >= 0; i--)
                if (i < Adaylar.Count && now >= Adaylar[i].asamaBitis) Ilerle(Adaylar[i]);
        }

        KayitTuru RastgeleTur()
        {
            bool tavsiyeEdenVar = false;
            foreach (var c in CourierManager.I.Couriers) if (c.morale >= 0.5f) { tavsiyeEdenVar = true; break; }
            float wB = 0.45f, wO = IlanAktif ? 0.45f : 0.22f, wI = 0.15f, wT = tavsiyeEdenVar ? 0.05f + 0.1f * tavsiyePrim : 0f, wE = 0.06f;
            float x = Random.value * (wB + wO + wI + wT + wE);
            if (x < wB) return KayitTuru.Bireysel; x -= wB;
            if (x < wO) return KayitTuru.OkulKooperatifi; x -= wO;
            if (x < wI) return KayitTuru.CalismaIzinli; x -= wI;
            if (x < wT) return KayitTuru.Tavsiye;
            return KayitTuru.EskiKurye;
        }

        /// <summary>Yeni bir aday oluşturur (sürece eklemez). Araç verilmezse rastgele.</summary>
        KayitAdayi YeniAday(VehicleKind? arac = null)
        {
            var cm = CourierManager.I;
            var tur = RastgeleTur();
            var v = arac ?? cm.RandomVehicle();
            if (arac == null && (tur == KayitTuru.OkulKooperatifi || tur == KayitTuru.CalismaIzinli) && v == VehicleKind.Araba && Random.value < 0.6f)
                v = Random.value < 0.5f ? VehicleKind.Bisiklet : VehicleKind.EScooter;
            var c = cm.MakeCandidate(v, false);
            var a = new KayitAdayi { aday = c, tur = tur, asama = KayitAsamasi.Kayit, kayitZamani = GameClock.I.TotalMinutes };

            switch (tur)
            {
                case KayitTuru.OkulKooperatifi:
                    c.occupation = Occupation.Ogrenci;
                    break;
                case KayitTuru.CalismaIzinli:
                    c.occupation = Random.value < 0.7f ? Occupation.TamZamanli : Occupation.GeceKuryesi;
                    c.stamina += 0.1f;
                    c.pickiness *= 0.6f;
                    break;
                case KayitTuru.Tavsiye:
                {
                    var adaylar = new List<Courier>();
                    foreach (var k in cm.Couriers) if (k.morale >= 0.5f) adaylar.Add(k);
                    if (adaylar.Count > 0)
                    {
                        var r = adaylar[Random.Range(0, adaylar.Count)];
                        a.tavsiyeEden = r.courierId;
                        a.tavsiyeEdenAd = r.courierName;
                    }
                    else a.tur = KayitTuru.Bireysel;
                    c.care = Mathf.Min(1f, c.care + 0.05f);
                    break;
                }
                case KayitTuru.EskiKurye:
                    c.level = Mathf.Max(c.level, Random.value < 0.6f ? 2 : 3);
                    c.speedSkill += 0.05f; c.care = Mathf.Min(1f, c.care + 0.05f);
                    break;
                default:
                    if (c.occupation == Occupation.Ogrenci) a.tur = KayitTuru.OkulKooperatifi;   // öğrencilerin çoğu kooperatif üzerinden
                    break;
            }
            c.note = GameData.ApplicationNote(c.occupation, c.level);
            return a;
        }

        void YeniKayit()
        {
            var a = YeniAday();
            if (a == null) return;
            int v = (int)a.aday.vehicle;
            if (!acik[v] || Dolu)
            {
                if (bekleme[v] < BeklemeSiniri) bekleme[v]++;
                return;
            }
            a.asamaBitis = GameClock.I.TotalMinutes + Sure(a);
            Adaylar.Add(a);
            haftaKayit++;
            EventManager.I.NotifyQuiet($"Yeni kurye kaydı: {a.aday.name} ({GameData.Vehicle(a.aday.vehicle).name}, {TurAdi(a.tur)})");
        }

        /// <summary>Adayın bulunduğu aşamanın süresi (oyun dakikası).</summary>
        float Sure(KayitAdayi a)
        {
            var v = a.aday.vehicle;
            switch (a.asama)
            {
                case KayitAsamasi.Kayit: return Random.Range(60f, 360f);
                case KayitAsamasi.Belge:
                    if (a.tur == KayitTuru.OkulKooperatifi) return Random.Range(120f, 480f);
                    if (a.tur == KayitTuru.CalismaIzinli) return Random.Range(1440f, 2880f);
                    return Random.Range(240f, 1440f);
                case KayitAsamasi.Kontrol:
                {
                    float t = kontrol == 0 ? Random.Range(120f, 480f) : kontrol == 1 ? Random.Range(720f, 2160f) : Random.Range(2160f, 4320f);
                    if (v == VehicleKind.Motosiklet || v == VehicleKind.Araba) t += Random.Range(720f, 1440f) * (kontrol == 0 ? 0.4f : 1f);   // ehliyet, sigorta, araç
                    if (a.tur == KayitTuru.CalismaIzinli) t += Random.Range(1440f, 4320f) * (kontrol == 0 ? 0.5f : 1f);                       // çalışma izni
                    if (a.tur == KayitTuru.EskiKurye) t *= 0.5f;
                    return t;
                }
                case KayitAsamasi.Egitim: return Random.Range(120f, 360f);
                default: return Random.Range(240f, 1440f);
            }
        }

        /// <summary>Aşama bittiğinde vazgeçme ihtimali.</summary>
        float BirakmaIhtimali(KayitAdayi a)
        {
            float p;
            switch (a.asama)
            {
                case KayitAsamasi.Kayit: p = 0.12f; break;
                case KayitAsamasi.Belge: p = a.tur == KayitTuru.CalismaIzinli ? 0.24f : 0.18f; break;
                case KayitAsamasi.Kontrol: p = 0.03f + 0.03f * kontrol; break;   // uzun kontrolde vazgeçen olur
                case KayitAsamasi.Egitim: p = 0.08f; break;
                default: p = 0.04f; break;
            }
            if (a.tur == KayitTuru.Tavsiye || a.tur == KayitTuru.EskiKurye) p *= 0.5f;
            p *= 1f - 0.15f * yeniBonus;
            return p;
        }

        static readonly string[] BirakmaNedeni =
            { "kayıt formunu yarıda bıraktı", "belgelerini yüklemedi", "kontrol uzun sürünce vazgeçti", "eğitime gelmedi", "ekipmanı almaya gelmedi" };

        void Ilerle(KayitAdayi a)
        {
            float now = GameClock.I.TotalMinutes;

            // Son aşama: hesap açılır (sınır doluysa bekler, uzun beklerse vazgeçer)
            if (a.asama == KayitAsamasi.Ekipman)
            {
                if (UpgradeManager.I.CourierCapFull)
                {
                    if (!a.hesapBekliyor) { a.hesapBekliyor = true; a.beklemeBasi = now; }
                    a.asamaBitis = now + 60f;
                    if (now - a.beklemeBasi > 2880f && Random.value < 0.02f) Birak(a, "hesabı açılmayınca başka platforma gitti", 4);
                    return;
                }
                if (Random.value < BirakmaIhtimali(a)) { Birak(a, BirakmaNedeni[4], 4); return; }
                Aktiflestir(a);
                return;
            }

            if (Random.value < BirakmaIhtimali(a)) { Birak(a, BirakmaNedeni[(int)a.asama], (int)a.asama); return; }

            // Kontrol: sıkılığa göre özensiz adaylar elenir
            if (a.asama == KayitAsamasi.Kontrol)
            {
                var c = a.aday;
                bool elendi = kontrol == 2 ? (c.care < 0.62f || c.politeness < 0.6f) : kontrol == 1 && c.care < 0.5f;
                if (elendi)
                {
                    Adaylar.Remove(a);
                    haftaElenen++;
                    EventManager.I.NotifyQuiet($"{c.name} kontrolde elendi ({KontrolAdlari[kontrol]} kontrol).");
                    return;
                }
            }

            var next = a.asama + 1;
            if (next == KayitAsamasi.Egitim) next = KayitAsamasi.Ekipman;   // eğitim aşaması yok: kontrolden sonra ekipman
            a.asama = next;
            a.asamaBitis = now + Sure(a);
        }

        void Birak(KayitAdayi a, string neden, int asama)
        {
            Adaylar.Remove(a);
            haftaBirakan[Mathf.Clamp(asama, 0, 4)]++;
            EventManager.I.NotifyQuiet($"{a.aday.name} kayıttan vazgeçti: {neden}.");
        }

        void Aktiflestir(KayitAdayi a)
        {
            Adaylar.Remove(a);
            var c = CourierManager.I.ActivateRegistrant(a.aday);
            if (c == null) return;
            c.kayitTuru = (int)a.tur;
            c.aktifGun = Day;
            c.tavsiyeEden = a.tavsiyeEden;
            haftaAktif++;
            string kim = a.tur == KayitTuru.EskiKurye ? "geri döndü" : "kayıt sürecini tamamladı";
            EventManager.I.Notify($"{c.courierName} ({GameData.Vehicle(c.vehicle).name}, {TurAdi(a.tur)}) {kim}: kurye hesabı açıldı." +
                                  (a.tur == KayitTuru.Tavsiye && a.tavsiyeEdenAd.Length > 0 ? $" ({a.tavsiyeEdenAd} tavsiye etti)" : ""));
        }

        // ================================================================ kampanyalar

        public long ReklamBedeli => Round1000(Mathf.Max(60000f, CrisisManager.AvgDailyIncome() * 0.3f));
        public long IlanBedeli => Round1000(Mathf.Max(25000f, CrisisManager.AvgDailyIncome() * 0.1f));
        static long Round1000(float v) => (long)Mathf.Round(v / 1000f) * 1000;

        public void ReklamVer()
        {
            if (ReklamAktif) return;
            if (!EconomyManager.I.TrySpend(ReklamBedeli, "Kurye kaydı: sosyal medya reklamı", ExpenseCategory.Reklam)) return;
            reklamBitis = Day + 6;
            EventManager.I.Notify("Sosyal medyada kurye reklamı başladı: 7 gün boyunca kayıtlar %60 artar.");
        }

        public void IlanVer()
        {
            if (IlanAktif) return;
            if (!EconomyManager.I.TrySpend(IlanBedeli, "Kurye kaydı: üniversite ilanı", ExpenseCategory.Reklam)) return;
            ilanBitis = Day + 6;
            EventManager.I.Notify("Üniversiteye kurye ilanı asıldı: 7 gün boyunca öğrenci kayıtları iki katına çıkar.");
        }

        /// <summary>Teslimattan sonra: kuryenin puanı, yeni kurye bonusu ve tavsiye primi.</summary>
        public void OnDelivered(Courier c, float rating)
        {
            if (c == null) return;
            c.puanAdet++;
            c.puanOrt = c.puanAdet == 1 ? rating : Mathf.Lerp(c.puanOrt, rating, 1f / Mathf.Min(30, c.puanAdet));

            if (c.aktifGun > 0 && c.deliveries <= BonusAdet && YeniKuryeBonusu > 0)
            {
                EconomyManager.I.SpendCampaign(YeniKuryeBonusu, $"Yeni kurye bonusu: {c.courierName}");
                c.Earn(YeniKuryeBonusu);
            }

            if (c.tavsiyeEden != 0 && !c.tavsiyePrimOdendi && c.deliveries >= BonusAdet)
            {
                c.tavsiyePrimOdendi = true;
                if (TavsiyePrimi <= 0) return;
                Courier r = null;
                foreach (var k in CourierManager.I.Couriers) if (k.courierId == c.tavsiyeEden) { r = k; break; }
                if (r == null) return;
                EconomyManager.I.SpendCampaign(TavsiyePrimi, $"Tavsiye primi: {r.courierName}");
                r.Earn(TavsiyePrimi);
                r.morale = Mathf.Min(1f, r.morale + 0.05f);
                r.AddInbox($"Tavsiye ettiğin {c.courierName} {BonusAdet} teslimata ulaştı: {Ft(TavsiyePrimi)} tavsiye primi hesabına yazıldı.");
                EventManager.I.Notify($"{r.courierName}, tavsiye ettiği {c.courierName} {BonusAdet} teslimata ulaşınca {Ft(TavsiyePrimi)} prim aldı.");
            }
        }

        // ================================================================ hesap kuralları

        public bool Askida(Courier c) => c.askiBitis > Day;

        /// <summary>Hesabı askıya alır (7 gün). 60 gün içinde 3. askıda hesap kapanır.</summary>
        public bool AskiyaAl(Courier c, string neden, bool elle)
        {
            if (c == null || Askida(c)) return false;
            if (c.OrderCount > 0)
            {
                if (elle) EventManager.I.Notify($"{c.courierName} elinde paket varken hesabı askıya alınamaz.");
                return false;
            }
            c.askiSayisi = Day - c.sonAskiGun > 60 ? 1 : c.askiSayisi + 1;
            c.sonAskiGun = Day;
            haftaAski++;
            if (c.askiSayisi >= 3)
            {
                EventManager.I.Notify($"{c.courierName} hesabı kapatıldı: 60 gün içinde 3. askı ({neden}).");
                CourierManager.I.Fire(c, false);
                return true;
            }
            c.askiBitis = Day + AskiGun;
            c.SetOnLeave(AskiGun, "hesabı askıda");
            c.morale = Mathf.Clamp01(c.morale - 0.15f);
            c.pickupCancelsThisMonth = 0;
            // Döndüğünde aynı puanla hemen yeniden askıya alınmasın: puan geçmişi yarıya iner
            c.puanAdet = Mathf.Min(c.puanAdet, 10);
            c.AddInbox($"Hesabın {AskiGun} gün askıya alındı: {neden}. {(c.askiSayisi == 2 ? "Bir askı daha olursa hesabın kapanacak." : "")}");
            EventManager.I.Notify($"{c.courierName} hesabı {AskiGun} gün askıya alındı: {neden} (60 günde {c.askiSayisi}. askı).");
            return true;
        }

        void OnNewDay()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            var cm = CourierManager.I;

            // Haftalık sayılar
            if (GameClock.I.DayOfWeek == 0)
            {
                gecenHaftaBirakan = haftaBirakan; haftaBirakan = new int[5];
                gecenHaftaKayit = haftaKayit; gecenHaftaAktif = haftaAktif; gecenHaftaElenen = haftaElenen;
                gecenHaftaIlkHafta = haftaIlkHafta; gecenHaftaAski = haftaAski;
                haftaKayit = haftaAktif = haftaElenen = haftaIlkHafta = haftaAski = 0;
            }

            foreach (var c in cm.Couriers.ToArray())
            {
                // İlk hafta: beklediği kadar iş bulamayan yeni kurye bırakır
                if (c.aktifGun > 0 && !c.ilkHaftaTamam)
                {
                    int gun = Day - c.aktifGun;
                    if (c.deliveries >= 10 || gun > 7) c.ilkHaftaTamam = true;
                    else if (gun >= 3 && c.OrderCount == 0)
                    {
                        float p = 0.1f * (1f - 0.2f * yeniBonus) * (c.kayitTuru == (int)KayitTuru.Tavsiye ? 0.6f : 1f);
                        if (Random.value < p)
                        {
                            haftaIlkHafta++;
                            cm.Resign(c, $"ilk haftasında {c.deliveries} teslimat yaptı, beklediği kazancı bulamadı");
                            continue;
                        }
                    }
                }

                // Hesap kuralları
                if (Askida(c)) continue;
                if (MinPuan > 0f && c.puanAdet >= 20 && c.puanOrt < MinPuan)
                    AskiyaAl(c, $"müşteri puanı {c.puanOrt:0.0} (kural: en az {MinPuan:0.0})", false);
                else if (MaxIptal > 0 && c.pickupCancelsThisMonth >= MaxIptal)
                    AskiyaAl(c, $"bu ay {c.pickupCancelsThisMonth} kez teslim almayı iptal etti (kural: en fazla {MaxIptal - 1})", false);
            }
        }
    }
}
