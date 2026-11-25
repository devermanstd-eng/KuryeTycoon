using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KuryeTycoon
{
    /// <summary>
    /// Minyatür maket (diorama) görünümü: Gorsel3D'nin ekran efektleri profilinin oyun içi kopyasını her karede ayarlar.
    ///  • Tilt-shift: alan derinliği odağı her karede ekranın ortasındaki zemine konur; ekranın üstü (uzak taraf) hafif bulanık,
    ///    ortadaki kurye ve binalar net. Hızlı Gaussian; iki kenarlı Bokeh yalnız DioramaAyari.bokehTiltShift ile (M2 Air'de kare
    ///    hızını yarıya indirdi). Düşük kalitede ve düz haritada kapalı.
    ///  • Saate göre renk: gündüz bal rengi, altın saatte sıcak turuncu, gece derin lacivert gölge + sıcak parlaklar.
    ///    Işıma eşiği gündüz yüksek (güneşli çatı parlamaz), gece düşük (yalnız pencere, vitrin ve lambalar parlar).
    ///  • Ekrana yarı saydam ton bindirmez: eskiden 2D gece/bulut/güneş perdesi haritayı grileştiriyor, güneş parlaması
    ///    ekranın üstüne biniyordu. Bu açıkken DayNight ve WeatherFx tam ekran tonları çizmez (<see cref="RenkAktif"/>).
    ///  • Ağaç salınımı ve far huzmeleri için ortak gölgelendirici değerleri (_KuryeSalinim, _KuryeRuzgarYon), ıslak yol.
    /// Ayarlar DioramaAyari'nda (Resources/Gorsel/Diorama.asset; yoksa koddaki varsayılanlar).
    /// </summary>
    public static class Diorama
    {
        static VolumeProfile profil;
        static DepthOfField dof;
        static ColorAdjustments renk;
        static WhiteBalance beyaz;
        static SplitToning bolunmus;
        static Bloom isima;

        static float sonIslak = -1f, islakZamani;

        static readonly int SalinimId = Shader.PropertyToID("_KuryeSalinim");
        static readonly int RuzgarYonId = Shader.PropertyToID("_KuryeRuzgarYon");
        static readonly int RuzgarId = Shader.PropertyToID("_KuryeRuzgar");

        /// <summary>
        /// Renk derecelendirmesi çalışıyor mu? Açıkken DayNight ve WeatherFx ekrana tam ekran ton (gece perdesi, bulut grisi,
        /// güneş sıcaklığı, güneş parlaması) çizmez: aynı iş ekran efektlerinde, kontrastı ve doygunluğu bozmadan yapılır.
        /// </summary>
        public static bool RenkAktif => profil != null && Gorsel3D.Active && DioramaAyari.Gecerli.renkDerecelendirme;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Sifirla()
        {
            profil = null;
            dof = null; renk = null; beyaz = null; bolunmus = null; isima = null;
            sonIslak = -1f;
        }

        /// <summary>Gorsel3D profili her kurduğunda (açılışta, "Atmosferi Güncelle"de): eksik efektleri kopyaya ekler.</summary>
        public static void ProfilHazirla(VolumeProfile p)
        {
            profil = p;
            if (p == null) return;
            dof = Al<DepthOfField>(p);
            renk = Al<ColorAdjustments>(p);
            beyaz = Al<WhiteBalance>(p);
            bolunmus = Al<SplitToning>(p);
            p.TryGet(out isima);          // ışımayı Gorsel3D kurar (kalite ayarı açıp kapatır)
            dof.mode.Override(DepthOfFieldMode.Off);
        }

        static T Al<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (!p.TryGet(out T c)) c = p.Add<T>(true);
            c.active = true;
            return c;
        }

        /// <summary>Gorsel3D.UpdateLighting'den, her karede.</summary>
        public static void Guncelle(Camera cam, float saat, float gece, float bulut)
        {
            var a = DioramaAyari.Gecerli;
            KuresellDegerler(a);
            IslakYol(a);
            if (profil == null) return;
            TiltShift(cam, a);
            if (a.renkDerecelendirme) Renk(a, saat, gece, bulut);
        }

        // ---------------------------------------------------------------- tilt-shift

        /// <summary>Kenardaki derinliğin odak uzaklığına oranı (kamera 38–50° eğik, 30° görüş): üst kenar ~1,3 kat uzak.</summary>
        const float KenarOrani = 0.26f;

        static void TiltShift(Camera cam, DioramaAyari a)
        {
            int mod = GrafikKalitesi.TiltShiftModu;    // 0 kapalı, 1 hızlı (Gaussian), 2 Bokeh (yalnız istenirse)
            var mc = MapCamera.I;
            if (mod == 0 || a.tiltShiftPiksel <= 0.01f || cam == null || cam.orthographic || mc == null || mc.FlatMode ||
                !OdakUzakligi(cam, out float P))
            {
                if (dof.mode.value != DepthOfFieldMode.Off) dof.mode.Override(DepthOfFieldMode.Off);
                return;
            }
            if (mod == 2)
            {
                // URP Bokeh: CoC = (1 − P/derinlik) · maxCoC, maxCoC = (A·F)/(P − F), A = odak/diyafram, F = odak/1000 (m).
                // Bulanıklık yarıçapı = CoC · 14 px. Kenarda istenen piksele göre diyafram hesaplanır (odak en uzun: 300 mm).
                const float odak = 300f, F = odak / 1000f;
                float M = a.tiltShiftPiksel / 14f / KenarOrani;
                float diyafram = Mathf.Clamp(odak * odak / 1000f / (M * Mathf.Max(0.5f, P - F)), 1f, 32f);
                dof.mode.Override(DepthOfFieldMode.Bokeh);
                dof.focusDistance.Override(P);
                dof.focalLength.Override(odak);
                dof.aperture.Override(diyafram);
                dof.bladeCount.Override(6);
                dof.bladeCurvature.Override(1f);
            }
            else
            {
                // Hızlı: yalnız uzak taraf (ekranın üstü) bulanıklaşır
                dof.mode.Override(DepthOfFieldMode.Gaussian);
                dof.gaussianStart.Override(P * 1.07f);
                dof.gaussianEnd.Override(P * 1.5f);
                dof.gaussianMaxRadius.Override(a.hizliBulaniklik);
                dof.highQualitySampling.Override(false);
            }
        }

        /// <summary>Kameradan ekranın ortasındaki zemin noktasına uzaklık (görüş doğrultusunda).</summary>
        static bool OdakUzakligi(Camera cam, out float P)
        {
            P = 10f;
            var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (ray.direction.z < 1e-4f) return false;            // kamera +Z'ye (yere) bakmıyor
            float t = -ray.origin.z / ray.direction.z;
            if (t <= 0.1f) return false;
            P = t * Vector3.Dot(ray.direction, cam.transform.forward);
            return P > 0.2f;
        }

        // ---------------------------------------------------------------- renk ve ışıma

        static void Renk(DioramaAyari a, float saat, float gece, float bulut)
        {
            float g1 = (saat - a.sabahAltin) / Mathf.Max(0.1f, a.altinGenislik);
            float g2 = (saat - a.aksamAltin) / Mathf.Max(0.1f, a.altinGenislik);
            float altin = Mathf.Max(Mathf.Exp(-g1 * g1), Mathf.Exp(-g2 * g2)) * (1f - gece) * (1f - bulut * 0.6f);
            var t = DioramaAyari.Ton.Lerp(a.gunduz, a.altinSaat, altin);
            t = DioramaAyari.Ton.Lerp(t, a.gece, gece);
            t.sicaklik -= bulut * a.bulutluSerinlik;
            t.isimaGucu *= 1f - 0.4f * bulut * (1f - gece);
            // Haritayı grileştiren ayarlara izin yok: doygunluk eksiye, pozlama −0,25'in altına inmez
            t.doygunluk = Mathf.Max(0f, t.doygunluk);
            t.pozlama = Mathf.Max(-0.25f, t.pozlama);

            renk.postExposure.Override(t.pozlama);
            renk.contrast.Override(t.kontrast);
            renk.saturation.Override(t.doygunluk);
            renk.colorFilter.Override(Color.white);       // sıcaklık beyaz ayarından: filtre her şeyi (pencereleri de) boyuyordu
            beyaz.temperature.Override(t.sicaklik);
            beyaz.tint.Override(t.renkKaymasi);
            bolunmus.shadows.Override(t.golgeTonu);
            bolunmus.highlights.Override(t.parlakTonu);
            bolunmus.balance.Override(t.tonDengesi);
            if (isima != null)
            {
                isima.threshold.Override(t.isimaEsigi);
                isima.intensity.Override(t.isimaGucu);
                isima.tint.Override(t.isimaRengi);
                isima.scatter.Override(0.62f);
                isima.clamp.Override(8f);                  // tek parlak piksel (güneş parıltısı) ekranı yakmasın
            }
        }

        // ---------------------------------------------------------------- gölgelendirici değerleri

        static void KuresellDegerler(DioramaAyari a)
        {
            var fx = WeatherFx.I;
            float ruzgar = fx != null && WeatherFx.Enabled ? Mathf.Clamp01(fx.WindAmt + fx.StormAmt * 0.6f) : 0.15f;
            Shader.SetGlobalFloat(SalinimId, Mathf.Lerp(a.salinimSakin, a.salinimFirtina, ruzgar));
            Shader.SetGlobalFloat(RuzgarId, ruzgar);
            Shader.SetGlobalVector(RuzgarYonId, new Vector4(0.94f, 0.34f, 0f, 0f));   // ekranda soldan sağa (yağmur da öyle eğik)
        }

        /// <summary>Islak asfalt: koyulaşır ve parlar (güneş, lamba ve gökyüzü yansır). Saniyede iki kez, değiştiyse.</summary>
        static void IslakYol(DioramaAyari a)
        {
            if (Time.unscaledTime < islakZamani) return;
            islakZamani = Time.unscaledTime + 0.5f;
            float islak = CityMap.I != null ? CityMap.I.Islaklik : 0f;
            if (Mathf.Abs(islak - sonIslak) < 0.01f) return;
            sonIslak = islak;
            OrganikSehir3D.Islat(islak, a.islakParlaklik, a.islakKoyuluk);
        }
    }
}
