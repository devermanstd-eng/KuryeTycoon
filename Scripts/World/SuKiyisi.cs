using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Nehir suyunun kıyısı: nehir yatağının derinliğini (Arazi.NehirYatak: 0 kıyıda, 1 ortada) küçük bir dokuya yazar ve su
    /// gölgelendiricisine (Resources/Gorsel/Su.shader) ortak değer olarak verir. Su kıyıda açık turkuaz olur ve ince beyaz köpük
    /// çıkar; ortası koyu kalır. Derinlik dokusu yerine kamera derinliği kullanılsaydı köpük kalite ayarına (SSAO ön geçişi,
    /// derinlik kopyalama zamanı) bağlı kalırdı: bu doku her ayarda aynı çalışır.
    ///  • Şehir kurulurken bir kez (City3D, Arazi.Hazirla'dan sonra). Kare başına 6 piksel, R8.
    ///  • Organik şehir değilse (ızgara şehirde nehir yatağı yok) kapalı: köpük çizilmez.
    /// </summary>
    public static class SuKiyisi
    {
        const int KareBasinaPiksel = 6;
        const int EnBuyuk = 2048;

        static Texture2D doku;
        static readonly int DokuId = Shader.PropertyToID("_KuryeKiyi");
        static readonly int OlcekId = Shader.PropertyToID("_KuryeKiyiOlcek");
        static readonly int VarId = Shader.PropertyToID("_KuryeKiyiVar");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Sifirla()
        {
            doku = null;
            Shader.SetGlobalFloat(VarId, 0f);
        }

        public static void Hazirla()
        {
            int W = CityMap.Width, H = CityMap.Height;
            if (!CityMap.Organik || W <= 0 || H <= 0) { Shader.SetGlobalFloat(VarId, 0f); return; }
            int w = Mathf.Min(W * KareBasinaPiksel, EnBuyuk), h = Mathf.Min(H * KareBasinaPiksel, EnBuyuk);
            if (doku == null || doku.width != w || doku.height != h)
            {
                if (doku != null) Object.Destroy(doku);
                doku = new Texture2D(w, h, TextureFormat.R8, false, true)
                {
                    name = "Nehir kıyısı",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }
            var px = new byte[w * h];
            bool su = false;
            // Doku haritanın tamamını kaplar: dünya x ∈ [−0,5; W − 0,5], y ∈ [−0,5; H − 0,5] (kare merkezleri tamsayı)
            float ox = (float)W / w, oy = (float)H / h;
            for (int y = 0; y < h; y++)
            {
                float wy = -0.5f + (y + 0.5f) * oy;
                for (int x = 0; x < w; x++)
                {
                    float v = Arazi.NehirYatak(-0.5f + (x + 0.5f) * ox, wy);
                    if (v > 0.001f) su = true;
                    px[y * w + x] = (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
                }
            }
            doku.SetPixelData(px, 0);
            doku.Apply(false, false);
            Shader.SetGlobalTexture(DokuId, doku);
            // uv = dünya · (1/W, 1/H) + (0,5/W, 0,5/H)
            Shader.SetGlobalVector(OlcekId, new Vector4(1f / W, 1f / H, 0.5f / W, 0.5f / H));
            Shader.SetGlobalFloat(VarId, su ? 1f : 0f);
        }
    }
}
