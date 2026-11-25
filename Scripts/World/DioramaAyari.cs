using System;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Minyatür maket (diorama) görünümünün ayarları: tilt-shift bulanıklığı, saate göre renk derecelendirmesi ve ışıma,
    /// gece far huzmeleri, ağaç salınımı, ıslak yol, seçim halkası. Resources/Gorsel/Diorama.asset'te durur; Diorama okur.
    ///  • Dosya yoksa aşağıdaki varsayılanlar kullanılır (oyun bu dosyaya bağlı değil).
    ///  • "Kurye Tycoon > Diorama Atmosferi Kur" (Editor/DioramaAtmosferKurucu.cs) dosyayı bu varsayılanlarla yazar; sonra
    ///    Inspector'dan ayarlanabilir. Oyun açıkken değiştirince hemen uygulanır (Diorama her karede okur).
    /// Sahneye bir şey yazılmaz.
    /// </summary>
    [CreateAssetMenu(menuName = "Kurye Tycoon/Diorama Ayarı", fileName = "Diorama")]
    public class DioramaAyari : ScriptableObject
    {
        public const string Yol = "Gorsel/Diorama";

        /// <summary>Günün bir anının renk ve ışıma ayarı. Diorama saate göre üçü (gündüz, altın saat, gece) arasında geçiş yapar.</summary>
        [Serializable]
        public struct Ton
        {
            [Tooltip("Beyaz ayarı: artı sıcak (sarı), eksi serin (mavi)"), Range(-60f, 60f)] public float sicaklik;
            [Tooltip("Artı mor, eksi yeşil"), Range(-30f, 30f)] public float renkKaymasi;
            [Tooltip("Doygunluk. Eksi değer haritayı grileştirir: kullanma"), Range(0f, 40f)] public float doygunluk;
            [Range(-20f, 40f)] public float kontrast;
            [Tooltip("Pozlama (EV). Gece bile −0,25'ten koyu yapma: harita okunmaz"), Range(-0.4f, 0.4f)] public float pozlama;
            [Tooltip("Gölgelerin tonu (bölünmüş tonlama; nötr gri 0,5)")] public Color golgeTonu;
            [Tooltip("Parlak yerlerin tonu (nötr gri 0,5)")] public Color parlakTonu;
            [Range(-100f, 100f)] public float tonDengesi;
            [Tooltip("Işıma eşiği (gama). Gündüz yüksek: güneşli çatılar parlamasın; gece düşük: yalnız pencere ve lambalar"), Range(0.5f, 2f)] public float isimaEsigi;
            [Range(0f, 1.5f)] public float isimaGucu;
            public Color isimaRengi;

            public static Ton Lerp(in Ton a, in Ton b, float t)
            {
                return new Ton
                {
                    sicaklik = Mathf.Lerp(a.sicaklik, b.sicaklik, t),
                    renkKaymasi = Mathf.Lerp(a.renkKaymasi, b.renkKaymasi, t),
                    doygunluk = Mathf.Lerp(a.doygunluk, b.doygunluk, t),
                    kontrast = Mathf.Lerp(a.kontrast, b.kontrast, t),
                    pozlama = Mathf.Lerp(a.pozlama, b.pozlama, t),
                    golgeTonu = Color.Lerp(a.golgeTonu, b.golgeTonu, t),
                    parlakTonu = Color.Lerp(a.parlakTonu, b.parlakTonu, t),
                    tonDengesi = Mathf.Lerp(a.tonDengesi, b.tonDengesi, t),
                    isimaEsigi = Mathf.Lerp(a.isimaEsigi, b.isimaEsigi, t),
                    isimaGucu = Mathf.Lerp(a.isimaGucu, b.isimaGucu, t),
                    isimaRengi = Color.Lerp(a.isimaRengi, b.isimaRengi, t),
                };
            }
        }

        [Header("Tilt-shift (minyatür maket bulanıklığı)")]
        [Tooltip("Ekranın üst ve alt kenarındaki en fazla bulanıklık (1080p'de piksel). Ortası hep net. 0 kapalı.")]
        [Range(0f, 12f)] public float tiltShiftPiksel = 4.5f;
        [Tooltip("Hızlı bulanıklığın (Gaussian, yalnız uzak taraf: ekranın üstü) en büyük yarıçapı")]
        [Range(0.5f, 1.5f)] public float hizliBulaniklik = 1.0f;
        [Tooltip("Güçlü ekran kartı için: Yüksek kalitede iki kenarlı Bokeh bulanıklığı. 25.11'de M2 Air'de kare hızını yarıya " +
                 "indirdi (24 → 12 FPS); kapalıyken her kalitede hızlı Gaussian.")]
        public bool bokehTiltShift = false;

        [Header("Ortam gölgesi (SSAO): binaların, ağaçların ve araçların yere bastığı yer")]
        [Range(0f, 4f)] public float ssaoGucu = 2.1f;
        [Tooltip("Dünya biriminde (1 kare = 10 m). Küçük: yalnız temas gölgesi")]
        [Range(0.02f, 0.4f)] public float ssaoYaricap = 0.09f;
        [Tooltip("Kameradan bu uzaklığa kadar uygulanır (kamera en uzakta ~120 birim)")]
        public float ssaoUzaklik = 220f;
        [Range(0f, 1f)] public float ssaoDogrudanIsik = 0.22f;

        [Header("Renk derecelendirmesi (saate göre)")]
        [Tooltip("Kapalıysa AtmosferProfili'ndeki sabit renk ayarı kullanılır")]
        public bool renkDerecelendirme = true;
        public Ton gunduz = VarsayilanGunduz;
        public Ton altinSaat = VarsayilanAltin;
        public Ton gece = VarsayilanGece;
        [Tooltip("Sabah altın saatinin ortası (saat)")] public float sabahAltin = 7.0f;
        [Tooltip("Akşam altın saatinin ortası (saat)")] public float aksamAltin = 18.4f;
        [Tooltip("Altın saatin yarı genişliği (saat)")] public float altinGenislik = 1.3f;
        [Tooltip("Bulut ve yağmurda ne kadar serinler (beyaz ayarı)"), Range(0f, 30f)] public float bulutluSerinlik = 10f;

        [Header("Gece far huzmeleri")]
        [Range(0f, 3f)] public float farGucu = 1.1f;
        public Color farRengi = new Color(1f, 0.86f, 0.62f);

        [Header("Ağaç salınımı")]
        [Tooltip("Sakin havada ağaç tepesinin salınımı (dünya birimi; 1 kare = 10 m)"), Range(0f, 0.05f)] public float salinimSakin = 0.01f;
        [Tooltip("Fırtınada ağaç tepesinin salınımı"), Range(0f, 0.12f)] public float salinimFirtina = 0.04f;

        [Header("Islak yol")]
        [Tooltip("Islak asfaltın parlaklığı (smoothness)"), Range(0f, 1f)] public float islakParlaklik = 0.72f;
        [Tooltip("Islak asfalt ne kadar koyulaşır (çarpan)"), Range(0.4f, 1f)] public float islakKoyuluk = 0.7f;

        [Header("Seçim halkası")]
        public Color halkaRengi = new Color(1f, 0.78f, 0.36f);
        public Color halkaMurekkep = new Color(0.10f, 0.07f, 0.06f);

        // ---------------------------------------------------------------- varsayılan tonlar

        /// <summary>Gündüz: bal rengi, canlı ama yumuşak; ışıma yalnız çok parlak yerde (ıslak yolda güneş parıltısı).</summary>
        public static readonly Ton VarsayilanGunduz = new Ton
        {
            sicaklik = 10f, renkKaymasi = 2f, doygunluk = 12f, kontrast = 10f, pozlama = 0.05f,
            golgeTonu = new Color(0.47f, 0.47f, 0.54f), parlakTonu = new Color(0.58f, 0.53f, 0.46f), tonDengesi = -10f,
            isimaEsigi = 1.3f, isimaGucu = 0.18f, isimaRengi = new Color(1f, 0.93f, 0.82f),
        };

        /// <summary>Altın saat: sıcak turuncu-altın, gölgeler hafif mor.</summary>
        public static readonly Ton VarsayilanAltin = new Ton
        {
            sicaklik = 24f, renkKaymasi = 4f, doygunluk = 16f, kontrast = 12f, pozlama = 0.04f,
            golgeTonu = new Color(0.45f, 0.43f, 0.56f), parlakTonu = new Color(0.66f, 0.54f, 0.40f), tonDengesi = -12f,
            isimaEsigi = 1.15f, isimaGucu = 0.26f, isimaRengi = new Color(1f, 0.86f, 0.66f),
        };

        /// <summary>
        /// Gece: derin lacivert gölgeler, parlaklar sıcak kalır (pencere ve lamba altın sarısı), düşük eşikli yumuşak ışıma.
        /// Ekrana ton bindirilmez (eski 2D gece perdesi haritayı grileştiriyordu): koyuluk ışıktan ve bölünmüş tonlamadan gelir.
        /// </summary>
        public static readonly Ton VarsayilanGece = new Ton
        {
            sicaklik = -12f, renkKaymasi = 4f, doygunluk = 8f, kontrast = 16f, pozlama = -0.08f,
            golgeTonu = new Color(0.34f, 0.39f, 0.62f), parlakTonu = new Color(0.66f, 0.57f, 0.44f), tonDengesi = -35f,
            isimaEsigi = 0.95f, isimaGucu = 0.55f, isimaRengi = new Color(1f, 0.86f, 0.64f),
        };

        static DioramaAyari yuklu;
        static bool arandi;

        /// <summary>Resources'tan bir kez yükler (yoksa null). Yenile: editörden güncellenince yeniden okur.</summary>
        public static DioramaAyari Yukle(bool yenile = false)
        {
            if (yenile || !arandi)
            {
                arandi = true;
                yuklu = Resources.Load<DioramaAyari>(Yol);
            }
            return yuklu;
        }

        static DioramaAyari varsayilan;

        /// <summary>Dosya varsa o, yoksa varsayılan değerli bellek kopyası (hiç null dönmez).</summary>
        public static DioramaAyari Gecerli
        {
            get
            {
                var a = Yukle();
                if (a != null) return a;
                if (varsayilan == null)
                {
                    varsayilan = CreateInstance<DioramaAyari>();
                    varsayilan.hideFlags = HideFlags.HideAndDontSave;
                }
                return varsayilan;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Sifirla()
        {
            yuklu = null;
            arandi = false;
        }
    }
}
