using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Arazinin yüksekliği (dünyada yukarı −Z; dönen değer z ofseti, eksi = yukarıda).
    ///  • Şehrin içi de yumuşak tepeli (u58): nehir vadide akar, kıyıdan uzaklaştıkça zemin yamaçlarla yükselir; iki ölçekli
    ///    gürültüyle tepeler ve sırtlar (en çok ~0,9 kare ≈ 9 m). Yollar, kaldırımlar, binalar, kuryeler, trafik ve etiketler
    ///    bu yüksekliğe oturur (OrganikSehir3D, City3D, CityMap.ToWorld, Traffic, MapCamera.GroundPoint).
    ///    AtolyeAyari > Şehir yükseltisi kapalıysa şehir eskisi gibi düz.
    ///  • Nehir yatağı yalnızca suyun altında çukur (su yüzeyi kara seviyesinde, parkların üstünde).
    ///  • Şehrin dışında, kır parsellerinin ötesinde (≈26 kare) tepeler başlar, haritanın kenarına doğru yükselir.
    ///    Kır yollarının çevresi düzlenir: yollar vadilerden geçer, gömülmez.
    /// Binalar, ağaçlar ve donatılar bu yüksekliğe oturur (City3D.AddPrefab, SokakDonatilari).
    /// </summary>
    public static class Arazi
    {
        public const float NehirDerinlik = 0.09f;    // nehir yatağının en derin yeri (kare)
        const float TepeBas = 24f, TepeTam = 40f;     // şehir kenarından uzaklık (kare): tepeler burada başlar / tam boyuna ulaşır
        const float TepeBoy = 2.6f;                    // en yüksek tepe (kare ≈ 26 m)

        static float[,] suUzak;                         // her karenin en yakın su karesine uzaklığı (kare)
        static float[,] kirUzak;                        // en yakın kır karesine ya da şehir kenarına uzaklık (kare): şehir tepeleri orada söner
        static int sw, sh;

        /// <summary>Şehir kurulurken bir kez: su uzaklık haritası.</summary>
        public static void Hazirla()
        {
            sw = CityMap.Width; sh = CityMap.Height;
            // kır kareleri ve şehir kenarı: şehir tepeleri burada söner (kır parselleri, tarlalar ve dış tepeler düz zemine göre)
            kirUzak = new float[sw, sh];
            for (int x = 0; x < sw; x++)
                for (int y = 0; y < sh; y++)
                    kirUzak[x, y] = (x == 0 || y == 0 || x == sw - 1 || y == sh - 1 || CityMap.ZoneOf(x, y) == Zone.Country) ? 0f : 1e6f;
            Uzaklik(kirUzak);
            suUzak = new float[sw, sh];
            const float Sonsuz = 1e6f;
            for (int x = 0; x < sw; x++)
                for (int y = 0; y < sh; y++)
                    suUzak[x, y] = CityMap.WaterAt(x, y) ? 0f : Sonsuz;
            // İki geçişli pah uzaklığı (8 komşu): yeterince yuvarlak
            const float D = 1f, K = 1.4142f;
            for (int y = 0; y < sh; y++)
                for (int x = 0; x < sw; x++)
                {
                    float v = suUzak[x, y];
                    if (x > 0) v = Mathf.Min(v, suUzak[x - 1, y] + D);
                    if (y > 0) v = Mathf.Min(v, suUzak[x, y - 1] + D);
                    if (x > 0 && y > 0) v = Mathf.Min(v, suUzak[x - 1, y - 1] + K);
                    if (x < sw - 1 && y > 0) v = Mathf.Min(v, suUzak[x + 1, y - 1] + K);
                    suUzak[x, y] = v;
                }
            for (int y = sh - 1; y >= 0; y--)
                for (int x = sw - 1; x >= 0; x--)
                {
                    float v = suUzak[x, y];
                    if (x < sw - 1) v = Mathf.Min(v, suUzak[x + 1, y] + D);
                    if (y < sh - 1) v = Mathf.Min(v, suUzak[x, y + 1] + D);
                    if (x < sw - 1 && y < sh - 1) v = Mathf.Min(v, suUzak[x + 1, y + 1] + K);
                    if (x > 0 && y < sh - 1) v = Mathf.Min(v, suUzak[x - 1, y + 1] + K);
                    suUzak[x, y] = v;
                }
        }

        /// <summary>İki geçişli pah uzaklığı (8 komşu): 0 olan karelerden uzaklık.</summary>
        static void Uzaklik(float[,] d)
        {
            const float D = 1f, K = 1.4142f;
            for (int y = 0; y < sh; y++)
                for (int x = 0; x < sw; x++)
                {
                    float v = d[x, y];
                    if (x > 0) v = Mathf.Min(v, d[x - 1, y] + D);
                    if (y > 0) v = Mathf.Min(v, d[x, y - 1] + D);
                    if (x > 0 && y > 0) v = Mathf.Min(v, d[x - 1, y - 1] + K);
                    if (x < sw - 1 && y > 0) v = Mathf.Min(v, d[x + 1, y - 1] + K);
                    d[x, y] = v;
                }
            for (int y = sh - 1; y >= 0; y--)
                for (int x = sw - 1; x >= 0; x--)
                {
                    float v = d[x, y];
                    if (x < sw - 1) v = Mathf.Min(v, d[x + 1, y] + D);
                    if (y < sh - 1) v = Mathf.Min(v, d[x, y + 1] + D);
                    if (x < sw - 1 && y < sh - 1) v = Mathf.Min(v, d[x + 1, y + 1] + K);
                    if (x > 0 && y < sh - 1) v = Mathf.Min(v, d[x - 1, y + 1] + K);
                    d[x, y] = v;
                }
        }

        static float Ornekle(float[,] d, float x, float y)
        {
            x = Mathf.Clamp(x, 0f, sw - 1.001f); y = Mathf.Clamp(y, 0f, sh - 1.001f);
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            return Mathf.Lerp(Mathf.Lerp(d[ix, iy], d[ix + 1, iy], fx), Mathf.Lerp(d[ix, iy + 1], d[ix + 1, iy + 1], fx), fy);
        }

        static float SuUzakligi(float x, float y)
        {
            if (suUzak == null) return 99f;
            x = Mathf.Clamp(x, 0f, sw - 1.001f); y = Mathf.Clamp(y, 0f, sh - 1.001f);
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            float a = Mathf.Lerp(suUzak[ix, iy], suUzak[ix + 1, iy], fx);
            float b = Mathf.Lerp(suUzak[ix, iy + 1], suUzak[ix + 1, iy + 1], fx);
            return Mathf.Lerp(a, b, fy);
        }

        /// <summary>Nehir yatağının derinliği (0 kıyıdan uzakta, 1 su içinde).</summary>
        public static float NehirCukur(float x, float y)
        {
            if (suUzak == null || x < -0.5f || y < -0.5f || x > sw - 0.5f || y > sh - 0.5f) return 0f;
            float d = SuUzakligi(x, y);
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.35f, d));
        }

        /// <summary>Şehir dikdörtgeninin dışına uzaklık (kare; içerde 0).</summary>
        static float DisUzaklik(float x, float y)
        {
            float dx = Mathf.Max(0f, Mathf.Max(-0.5f - x, x - (CityMap.Width - 0.5f)));
            float dy = Mathf.Max(0f, Mathf.Max(-0.5f - y, y - (CityMap.Height - 0.5f)));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Tepelerin yüksekliği (kare, artı = yukarı). Kır yollarının çevresi düzlenir.</summary>
        public static float Tepe(float x, float y)
        {
            float u = DisUzaklik(x, y);
            if (u <= TepeBas) return 0f;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TepeBas, TepeTam, u));
            // Yumuşak tepeler: iki ölçekte gürültü, sırtlar
            float n = Mathf.PerlinNoise(x * 0.045f + 13.1f, y * 0.045f + 7.7f) * 0.7f + Mathf.PerlinNoise(x * 0.11f + 3.3f, y * 0.11f + 41.9f) * 0.3f;
            float h = TepeBoy * k * (0.35f + 0.65f * n);
            if (h <= 0.001f) return 0f;
            // Yollar vadide
            var p = new Vector2(x, y);
            float yol = CityMap.KirYoluYakin(p, 0.8f) ? 0f : CityMap.KirYoluYakin(p, 1.8f) ? 0.25f : CityMap.KirYoluYakin(p, 3.2f) ? 0.6f : 1f;
            // Nehir vadide akar: kıyıdan ~2 kare düz taban, sonra yamaçlar yavaşça yükselir
            float vadi = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.5f, 10f, CityMap.KirNehirUzak(p)));
            return h * yol * vadi;
        }

        /// <summary>
        /// Zeminin suyun ALTINDA kalan kısmı (0 kıyıda ve karada, 1 su ortasında).
        /// Kıyı kenarında (su karesinin yarısı kadar dışarıda) sıfırlanır: su yüzeyi karayla aynı seviyede,
        /// çukur sadece suyun altında kalır; kıyıda hendek ya da suyu örten çim oluşmaz.
        /// Kıyı boyama ve sazlık için daha geniş NehirCukur kullanılır.
        /// </summary>
        public static float NehirYatak(float x, float y)
        {
            if (suUzak == null || x < -0.5f || y < -0.5f || x > sw - 0.5f || y > sh - 0.5f) return 0f;
            float d = SuUzakligi(x, y);
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.45f, d));
        }

        const float SehirTepeBoy = 0.9f;                // şehrin içindeki en yüksek tepe (kare)

        /// <summary>
        /// Şehrin içindeki yükselti (kare, artı = yukarı): nehirden uzaklaştıkça yükselen yamaçlar ve iki ölçekte tepeler.
        /// Kıyıdan ~1,5 kareye kadar düz (köprüler, rıhtım), ~12 karede tam boy. Şehrin dışına doğru Tepe() ile birleşir.
        /// </summary>
        public static float SehirTepe(float x, float y)
        {
            if (suUzak == null || kirUzak == null || !AtolyeAyari.SehirYukseltisi) return 0f;
            if (x < -0.5f || y < -0.5f || x > sw - 0.5f || y > sh - 0.5f) return 0f;
            float d = SuUzakligi(x, y);
            float vadi = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.5f, 12f, d));
            // kıra ve şehir kenarına doğru söner (kır parselleri düz zemine göre çizilir)
            vadi *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 5f, Ornekle(kirUzak, x, y)));
            if (vadi <= 0f) return 0f;
            float n = Mathf.PerlinNoise(x * 0.055f + 71.3f, y * 0.055f + 19.9f) * 0.65f + Mathf.PerlinNoise(x * 0.13f + 5.1f, y * 0.13f + 88.2f) * 0.35f;
            return SehirTepeBoy * vadi * (0.18f + 0.82f * Mathf.SmoothStep(0f, 1f, n));
        }

        /// <summary>Dünya z ofseti (−Z yukarı): tepelerde eksi, nehir yatağında (su altında) artı.</summary>
        public static float Z(float x, float y) => NehirYatak(x, y) * NehirDerinlik - Tepe(x, y) - SehirTepe(x, y);
        public static float Z(Vector2 p) => Z(p.x, p.y);
    }
}
