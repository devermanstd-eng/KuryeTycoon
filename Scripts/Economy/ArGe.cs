using System;
using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    [Serializable]
    public class ArGeSave
    {
        public List<string> tamamlanan = new List<string>();
        public List<string> sira = new List<string>();
        public string aktif = "";
        public float ilerleme;   // oyun dakikası
        public long harcanan;
    }

    /// <summary>
    /// Ar-Ge (Şirket > Gelişim ağacının alt dalı; u71'e kadar ayrı "Ar-Ge" sekmesiydi): zamanla ilerleyen araştırma sırası. Bir proje başlatılınca bedeli peşin ödenir, oyun günleriyle
    /// ilerler; bitince kalıcı etkisi açılır. Aynı anda bir proje araştırılır, iki proje sıraya konabilir (bedeli sırası gelince).
    /// Oyun kapalıyken de ilerler: kayıt yüklenince gerçek saatin her saati 0,5 oyun günü sayılır (en fazla sıradaki projenin sonuna).
    /// Bazı projeler öncekileri ister (ağaç). Etkiler gelişim ağacının küçük bonuslarına eklenir (GelisimAgaci.Bonus) ya da özeldir:
    /// yeni nesil termal çanta (sıcak kalma +8 dk), yedekli bulut (uygulama çökmesi seyrek), drone pilotu (robot filosuna 4 drone).
    /// </summary>
    public class ArGe : MonoBehaviour
    {
        public static ArGe I { get; private set; }

        public class Proje
        {
            public string id, ad, aciklama, etki;
            public long bedel;
            public float gun;
            public string[] onKosul = new string[0];
            public BonusTip? tip; public float miktar;
            public BonusTip? tip2; public float miktar2;
        }

        public static readonly List<Proje> Projeler = new List<Proje>
        {
            new Proje { id = "rota", ad = "Rota optimizasyonu", bedel = 400000, gun = 4, tip = BonusTip.Hiz, miktar = 0.03f,
                aciklama = "Trafik ve tek yön verisiyle kuryeye en kısa yolu gösteren algoritma.", etki = "kurye hızı +%3" },
            new Proje { id = "termal", ad = "Yeni nesil termal çanta", bedel = 350000, gun = 3,
                aciklama = "Vakum yalıtımlı, ısıtma pedli çanta tasarımı.", etki = "sıcak yemek 8 dk daha uzun sıcak kalır" },
            new Proje { id = "batarya", ad = "Uzun menzilli e-araç bataryası", bedel = 700000, gun = 5, tip = BonusTip.Hiz, miktar = 0.02f, tip2 = BonusTip.Moral, miktar2 = 0.02f,
                aciklama = "Elektrikli scooter ve bisikletler için hızlı değişen, daha uzun giden batarya.", etki = "kurye hızı +%2, moral +2 puan" },
            new Proje { id = "talep", ad = "Yapay zekâlı talep tahmini", bedel = 900000, gun = 6, onKosul = new[] { "rota" }, tip = BonusTip.Siparis, miktar = 0.04f,
                aciklama = "Hava, maç, gün ve saat verisiyle siparişleri önceden tahmin edip kuryeleri doğru yere ve saate yönlendirir.", etki = "sipariş +%4" },
            new Proje { id = "uygulama2", ad = "Kurye uygulaması 2.0", bedel = 600000, gun = 4, onKosul = new[] { "rota" }, tip = BonusTip.Basvuru, miktar = 0.08f, tip2 = BonusTip.Moral, miktar2 = 0.02f,
                aciklama = "Kazanç tahmini, şeffaf iş dağıtımı ve kolay mola: kuryeler uygulamayı sever.", etki = "kurye başvurusu +%8, moral +2 puan" },
            new Proje { id = "fiyat", ad = "Dinamik fiyatlandırma", bedel = 1400000, gun = 7, onKosul = new[] { "talep" }, tip = BonusTip.Komisyon, miktar = 0.03f,
                aciklama = "Restoranlarla talep ve saate göre değişen komisyon anlaşmaları.", etki = "komisyon geliri +%3" },
            new Proje { id = "bulut", ad = "Yedekli bulut altyapısı", bedel = 1200000, gun = 6, onKosul = new[] { "talep" },
                aciklama = "İki bölgeli bulut, otomatik yedek: uygulama çökmesi çok seyrekleşir.", etki = "uygulama çökme krizi seyrek (yedekli sunucu)" },
            new Proje { id = "uzaktan", ad = "Uzaktan yönetim paneli", bedel = 800000, gun = 5, onKosul = new[] { "uygulama2" }, tip = BonusTip.SenYokken, miktar = 2f,
                aciklama = "Telefondan şirketi izle: oyun kapalıyken operasyon daha uzun sürer.", etki = "sen yokken kazanç süresi +2 saat" },
            new Proje { id = "drone", ad = "Drone teslimatı pilotu", bedel = 4000000, gun = 12, onKosul = new[] { "batarya", "talep" },
                aciklama = "Havacılık otoritesi izniyle şehirde drone pilotu: 2 kg'a kadar hafif ve yakın siparişleri robot filosuyla birlikte drone'lar götürür (~3 kat hızlı).",
                etki = "robot filosuna 4 drone (hafif, yakın siparişler; kurye ücreti yok)" },
            new Proje { id = "drone2", ad = "Drone filosu", bedel = 9000000, gun = 15, onKosul = new[] { "drone", "bulut" },
                aciklama = "Pilot başarılı: izin genişler, şehrin her yerinde daha çok drone uçar (fırtınada ve karda yine yere iner).", etki = "drone sayısı 4 → 12" },
        };

        readonly HashSet<string> tamamlanan = new HashSet<string>();
        public readonly List<string> Sira = new List<string>();
        public string aktif = "";
        public float ilerleme;     // oyun dakikası
        public long harcanan;
        float sonDakika = -1f;
        float bekleyenCevrimdisi;  // yüklemede eklenecek dakika (Update'te uygulanır)

        public const float CevrimdisiGunSaatBasi = 0.5f;
        public const int SiraSiniri = 2;

        void Awake() { I = this; }

        public void Init()
        {
            tamamlanan.Clear(); Sira.Clear();
            aktif = ""; ilerleme = 0f; harcanan = 0; sonDakika = -1f; bekleyenCevrimdisi = 0f;
        }

        public static Proje Bul(string id) => Projeler.Find(p => p.id == id);
        public bool Tamam(string id) => tamamlanan.Contains(id);
        public int TamamSayisi => tamamlanan.Count;
        public Proje Aktif => Bul(aktif);
        public float Oran => Aktif != null ? Mathf.Clamp01(ilerleme / (Aktif.gun * 1440f)) : 0f;
        public float KalanGun => Aktif != null ? Mathf.Max(0f, Aktif.gun - ilerleme / 1440f) : 0f;

        // ------------------------------------------------------------------ etkiler

        public static float Bonus(BonusTip t)
        {
            if (I == null) return 0f;
            float s = 0f;
            foreach (var id in I.tamamlanan)
            {
                var p = Bul(id);
                if (p == null) continue;
                if (p.tip == t) s += p.miktar;
                if (p.tip2 == t) s += p.miktar2;
            }
            return s;
        }

        public static float TermalEk => I != null && I.Tamam("termal") ? 8f : 0f;
        public static int DroneSayisi => I == null ? 0 : I.Tamam("drone2") ? 12 : I.Tamam("drone") ? 4 : 0;

        // ------------------------------------------------------------------ kurallar

        /// <summary>Başlatma engeli (boşsa başlatılabilir; para hariç).</summary>
        public string Engel(Proje p)
        {
            if (p == null) return "yok";
            if (Tamam(p.id)) return "tamamlandı";
            if (aktif == p.id) return "araştırılıyor";
            if (Sira.Contains(p.id)) return "sırada";
            foreach (var k in p.onKosul)
                if (!Tamam(k) && aktif != k && !Sira.Contains(k)) return $"önce \"{Bul(k).ad}\"";
            if (!string.IsNullOrEmpty(aktif) && Sira.Count >= SiraSiniri) return "sıra dolu";
            return "";
        }

        /// <summary>Başlat ya da sıraya koy. Aktif proje yoksa bedel hemen ödenir; sıradakinin bedeli sırası gelince.</summary>
        public bool Baslat(Proje p)
        {
            if (Engel(p).Length > 0) return false;
            if (string.IsNullOrEmpty(aktif))
            {
                foreach (var k in p.onKosul) if (!Tamam(k)) return false;   // ön koşulu bitmemişse ancak sıraya girer
                if (!EconomyManager.I.TrySpendYatirim(p.bedel, $"Ar-Ge: {p.ad}", ExpenseCategory.Gelistirme)) return false;
                harcanan += p.bedel;
                aktif = p.id; ilerleme = 0f;
                Telemetry.Add("Ar-Ge: başladı");
                EventManager.I.Notify($"Ar-Ge başladı: {p.ad} ({p.gun:0} gün). Bitince: {p.etki}.");
                return true;
            }
            Sira.Add(p.id);
            EventManager.I.Notify($"Ar-Ge sırasına eklendi: {p.ad}. Bedeli ({GameData.Ft(p.bedel)}) sırası gelince ödenir.");
            return true;
        }

        public void SiradanCikar(string id) => Sira.Remove(id);

        // ------------------------------------------------------------------ akış

        void Update()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || GameClock.I == null) return;
            float now = GameClock.I.TotalMinutes;
            if (sonDakika < 0f || now < sonDakika) { sonDakika = now; }
            float dt = Mathf.Min(now - sonDakika, 60f);
            sonDakika = now;
            if (bekleyenCevrimdisi > 0f && Aktif != null)
            {
                float kalan = Aktif.gun * 1440f - ilerleme;
                float ek = Mathf.Min(bekleyenCevrimdisi, kalan);
                ilerleme += ek;
                if (ek > 1f) EventManager.I.Notify($"Sen yokken Ar-Ge ilerledi: {Aktif.ad} %{Oran * 100f:0} (+{ek / 1440f:0.#} gün).");
                bekleyenCevrimdisi = 0f;
            }
            if (Aktif == null) { SiradakiniBaslat(); return; }
            ilerleme += dt;
            if (ilerleme >= Aktif.gun * 1440f) Bitir();
        }

        void Bitir()
        {
            var p = Aktif;
            tamamlanan.Add(p.id);
            aktif = ""; ilerleme = 0f;
            if (p.id == "bulut" && CrisisManager.I != null) CrisisManager.I.serverUpgraded = true;
            Telemetry.Add("Ar-Ge: tamamlandı");
            EventManager.I.Push(new GameEvent($"Ar-Ge tamamlandı: {p.ad}", $"{p.aciklama}\n\nKalıcı etki: {p.etki}."));
            SiradakiniBaslat();
        }

        void SiradakiniBaslat()
        {
            while (Sira.Count > 0 && string.IsNullOrEmpty(aktif))
            {
                var p = Bul(Sira[0]);
                bool hazir = p != null;
                if (hazir) foreach (var k in p.onKosul) if (!Tamam(k)) { hazir = false; break; }
                if (!hazir)
                {
                    // Ön koşulu sırada daha geride: öne alınabilecek ilk projeyi bul
                    int i = Sira.FindIndex(x => { var q = Bul(x); if (q == null) return false; foreach (var k in q.onKosul) if (!Tamam(k)) return false; return true; });
                    if (i < 0) return;
                    var tmp = Sira[i]; Sira.RemoveAt(i); Sira.Insert(0, tmp);
                    continue;
                }
                if (EconomyManager.I.FreeCash < p.bedel) return;   // parası gelince başlar
                Sira.RemoveAt(0);
                Baslat(p);
            }
        }

        // ------------------------------------------------------------------ kayıt

        public ArGeSave Kaydet()
        {
            var s = new ArGeSave { aktif = aktif, ilerleme = ilerleme, harcanan = harcanan };
            s.tamamlanan.AddRange(tamamlanan);
            s.sira.AddRange(Sira);
            return s;
        }

        /// <param name="kayitUtcTicks">Kaydın gerçek zamanı: oyun kapalıyken geçen süre araştırmaya eklenir.</param>
        public void Yukle(ArGeSave s, long kayitUtcTicks)
        {
            if (s == null) return;
            tamamlanan.Clear();
            foreach (var t in s.tamamlanan) if (Bul(t) != null) tamamlanan.Add(t);
            Sira.Clear(); Sira.AddRange(s.sira);
            aktif = Bul(s.aktif) != null ? s.aktif : "";
            ilerleme = s.ilerleme; harcanan = s.harcanan;
            if (kayitUtcTicks > 0 && !TestModu.Aktif)
            {
                double saat = (DateTime.UtcNow.Ticks - kayitUtcTicks) / (double)TimeSpan.TicksPerHour;
                if (saat > 0.05) bekleyenCevrimdisi = (float)Math.Min(saat * CevrimdisiGunSaatBasi * 1440.0, 30.0 * 1440.0);
            }
            if (tamamlanan.Contains("bulut") && CrisisManager.I != null) CrisisManager.I.serverUpgraded = true;
        }
    }
}
