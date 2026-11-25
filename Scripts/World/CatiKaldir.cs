using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// İçi modelli binaların çatısını kaldırır: kamera binaya yeterince yaklaşınca çatı yukarı süzülüp kaybolur, dükkânın
    /// içi görünür; uzaklaşınca ya da bina ekranın kenarına kayınca geri konur.
    ///  • Model (Tools/Blender/bakkal.py): aynı FBX'te üç obje. Adı "_Cati" ile biten çocuk kalkan parçadır (tavan, saçak,
    ///    çatı, baca, saçak tabelası); adı "_Ic" ile biten çocuk iç mekândır ve yalnızca çatı açıkken etkindir (camlar saydam
    ///    olmadığı için kapalıyken zaten görünmez: boşuna çizilmez, gölgeye girmez, üstüne kar katmanı kurulmaz).
    ///  • Koşul: binanın karesi ekran yüksekliğinin en az %19'unu kaplıyor (49 × 33'lük şehirde MapCamera.Zoom ≈ 0,78 ve
    ///    üstü), bina ekranın ortasına yakın ve binalar ayakta (Tilt01 &gt; 0,9). Kapanma eşikleri daha gevşek (%15,5; ortadan
    ///    daha uzak): sınırda gidip gelirken çatı titremez.
    ///  • Maliyet: koşula saniyede ~8 kez bakılır (bina başına tek WorldToViewportPoint çifti); çatı yalnızca hareket
    ///    ederken her karede güncellenir.
    /// City3D.AddPrefab kendi modellerimizi yerleştirirken <see cref="Tak"/> ile ekler; "_Cati" çocuğu olmayan modelde hiçbir şey yapmaz.
    /// Renderer.enabled'a dokunmaz (LODGroup onu yönetir): parçalar GameObject olarak açılıp kapanır. Renk, gece ışığı ve
    /// mevsim sistemleri kapalı parçaları da gördüğü için (GetComponentsInChildren(true)) etkilenmez.
    /// </summary>
    public class CatiKaldir : MonoBehaviour
    {
        public const string CatiEki = "_Cati";
        public const string IcEki = "_Ic";

        /// <summary>Bir karenin (10 m) ekran yüksekliğine oranı bu değeri geçince çatı açılır / altına inince kapanır.</summary>
        const float AcBoy = 0.19f, KapatBoy = 0.155f;
        /// <summary>Binanın ekran ortasına uzaklığı (ekran yüksekliği biriminde): bundan yakınsa açılır / uzaksa kapanır.</summary>
        const float AcMerkez = 0.36f, KapatMerkez = 0.48f;
        const float Sure = 0.5f;               // açılma / kapanma süresi (saniye, oyun hızından bağımsız)
        const float KontrolAraligi = 0.12f;    // koşula bakma aralığı (saniye)
        const float KalkisKare = 0.45f;        // çatının yükseldiği mesafe (kare; 1 kare = 10 m)

        Transform cati, ic;
        Vector3 yer0, olcek0, merkez0;         // çatının ilk yerel konumu, ölçeği ve kendi ortası (ebeveyn uzayında)
        float t;                               // 0 kapalı … 1 açık
        bool hedefAcik;
        bool sifirla;                          // gizlenip yeniden görünen binada çatı ilk karede yerine konur
        float sonrakiKontrol;

        /// <summary>Çatı açık ya da açılmakta mı (hedef durum).</summary>
        public bool Acik => hedefAcik;
        /// <summary>Çatı şu an tam kapalı değil mi (iç mekân görünüyor mu).</summary>
        public bool IcGorunur => t > 0f;
        /// <summary>Dükkân seçiliyken (bina kartı açık) çatı yakınlık şartı aranmadan açık tutulur (BakkalCanli ayarlar).</summary>
        [System.NonSerialized] public bool zorlaAcik;
        /// <summary>"İçeri gir" kamerası bu binanın içinde: çatı kapalı tutulur ama iç mekân etkin kalır (BakkalCanli ayarlar).</summary>
        [System.NonSerialized] public bool iceriModu;
        /// <summary>Modelin kökü (çatı, gövde ve iç mekân objelerinin ebeveyni) ve iç mekân objesi.</summary>
        public Transform Kok => cati != null ? cati.parent : null;
        public Transform Ic => ic;
        /// <summary>Son kontrolde bir karenin ekran yüksekliğine oranı ve binanın ekran ortasına uzaklığı (BakkalCanli de kullanır).</summary>
        public float SonBoy { get; private set; }
        public float SonMerkez { get; private set; } = 9f;
        public const string CanliAd = "Canli";

        /// <summary>Kar katmanı kurulmayacak parça mı: iç mekân objesi, onun çocukları ya da canlı figürler.</summary>
        public static bool KarYok(Transform tr)
        {
            for (int i = 0; i < 5 && tr != null; i++, tr = tr.parent)
                if (tr.name.EndsWith(IcEki) || tr.name == CanliAd) return true;
            return false;
        }

        /// <summary>Modelde "_Cati" ile biten bir çocuk varsa bileşeni ekler. Yoksa null döner (çoğu bina).</summary>
        public static CatiKaldir Tak(GameObject inst)
        {
            if (inst == null) return null;
            Transform c = null, i = null;
            foreach (var tr in inst.GetComponentsInChildren<Transform>(true))
            {
                if (tr == inst.transform) continue;
                if (c == null && tr.name.EndsWith(CatiEki)) c = tr;
                else if (i == null && tr.name.EndsWith(IcEki)) i = tr;
            }
            if (c == null) return null;
            var k = inst.AddComponent<CatiKaldir>();
            k.Kur(c, i);
            return k;
        }

        void Kur(Transform catiParca, Transform icParca)
        {
            cati = catiParca;
            ic = icParca;
            yer0 = cati.localPosition;
            olcek0 = cati.localScale;
            // Çatı küçülürken kendi ortasına doğru küçülsün (modelin kökü yerde, binanın ortasında)
            var r = cati.GetComponentInChildren<Renderer>(true);
            Vector3 dunyaOrta = r != null ? r.bounds.center : cati.position;
            merkez0 = cati.parent != null ? cati.parent.InverseTransformPoint(dunyaOrta) : dunyaOrta;
            t = 0f;
            hedefAcik = false;
            // Aynı karede kurulan binalar aynı karede kontrol etmesin
            sonrakiKontrol = Time.unscaledTime + Random.value * KontrolAraligi;
            if (ic != null) ic.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            // Bina gizlenince (düz harita görünümü, şehir yeniden kurulurken) çatı kapalı sayılır. Parçaların açık / kapalı
            // durumu burada değiştirilmez (ebeveyn kapanırken çocukları açıp kapamak Unity'de hata verir): tekrar
            // görününce ilk Update'te yerine oturur.
            hedefAcik = false;
            sifirla = true;
        }

        void Update()
        {
            if (cati == null) return;
            if (sifirla) { sifirla = false; Uygula(0f); }
            if (ic != null)
            {
                bool icGoster = t > 0f || iceriModu;
                if (ic.gameObject.activeSelf != icGoster) ic.gameObject.SetActive(icGoster);
            }
            float simdi = Time.unscaledTime;
            if (simdi >= sonrakiKontrol)
            {
                sonrakiKontrol = simdi + KontrolAraligi;
                hedefAcik = AcikOlmali(hedefAcik);
            }
            float hedef = hedefAcik ? 1f : 0f;
            if (Mathf.Approximately(t, hedef)) return;
            Uygula(Mathf.MoveTowards(t, hedef, Time.unscaledDeltaTime / Sure));
        }

        /// <summary>Kamera yeterince yakın ve bina ekranın ortasına yakın mı? (acikti: gevşek kapanma eşikleri kullanılır.)</summary>
        bool AcikOlmali(bool acikti)
        {
            var mc = MapCamera.I;
            var cam = Camera.main;
            SonBoy = 0f; SonMerkez = 9f;
            if (iceriModu) return false;                              // kamera içeride: çatı yerinde
            if (mc == null || cam == null) return false;
            if (mc.Tilt01 < 0.9f) return false;                       // binalar yatık / yükseliyor
            Vector3 p = transform.position;
            Vector3 v = cam.WorldToViewportPoint(p);
            if (v.z <= 0f) return false;                              // kameranın arkasında
            // Bir karenin ekrandaki boyu: kameranın sağına doğru 1 birim ötedeki noktayla ekran farkı
            Vector3 v2 = cam.WorldToViewportPoint(p + cam.transform.right);
            float boy = Mathf.Abs(v2.x - v.x) * cam.aspect;
            float dx = (v.x - 0.5f) * cam.aspect, dy = v.y - 0.5f;
            float merkez = Mathf.Sqrt(dx * dx + dy * dy);
            SonBoy = boy; SonMerkez = merkez;
            if (zorlaAcik) return true;                               // seçili dükkân: uzaktan da açık
            return acikti ? boy >= KapatBoy && merkez <= KapatMerkez
                          : boy >= AcBoy && merkez <= AcMerkez;
        }

        /// <summary>Çatıyı t oranına getirir: ilk yarıda yükselir, ikinci yarıda kendi ortasına doğru küçülüp kaybolur.</summary>
        void Uygula(float yeni)
        {
            t = Mathf.Clamp01(yeni);
            bool kapali = t <= 0f, tamAcik = t >= 1f;
            bool icAcik = !kapali || iceriModu;
            if (ic != null && ic.gameObject.activeSelf != icAcik) ic.gameObject.SetActive(icAcik);
            if (cati.gameObject.activeSelf == tamAcik) cati.gameObject.SetActive(!tamAcik);
            if (kapali)
            {
                cati.localPosition = yer0;
                cati.localScale = olcek0;
                return;
            }
            if (tamAcik) return;
            float yuksel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.7f));
            float kucul = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.35f) / 0.65f));
            kucul = Mathf.Max(0.001f, kucul);
            // Dünya yukarısı −Z; ebeveynin ölçeği (bina yükselme oranı, model ölçeği) hesaba katılır
            Vector3 yukari = cati.parent != null ? cati.parent.InverseTransformVector(Vector3.back) : Vector3.back;
            cati.localScale = olcek0 * kucul;
            cati.localPosition = merkez0 + (yer0 - merkez0) * kucul + yukari * (KalkisKare * yuksel);
        }
    }
}
