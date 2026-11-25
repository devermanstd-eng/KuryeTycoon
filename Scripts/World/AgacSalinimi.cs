using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Ağaçlar rüzgârda çok hafif sallanır (köşe kaydırma, gölgelendiricide): City3D ağaç ve çalı modellerini (Agac_*, Cali_*)
    /// yerleştirirken parçalarına salınan malzeme kopyalarını takar. Asıl .mat dosyaları değişmez; kopyalar paylaşılır
    /// (her malzeme için bir kopya: SRP Batcher toplu çizer).
    ///  • Yaprak ve gövde (KuryeTycoon/AnimeToon): kopyada _Salinim = 1.
    ///  • Siyah çizgi kabuğu (URP/Unlit): KuryeTycoon/CizgiSalinim kopyası, aynı kaymayla (çizgi ağaçtan ayrılmaz).
    ///  • Başka gölgelendirici (ör. Anime Stil kapalıyken URP/Lit): olduğu gibi kalır, sallanmaz.
    /// Rüzgârın gücü ve yönü Diorama'dan (hava durumuna göre: sakin, rüzgârlı, fırtına). Kırdaki orman (KirOrman) birleşik
    /// mesh olduğu için sallanmaz; uzakta zaten görünmez.
    /// </summary>
    public static class AgacSalinimi
    {
        static readonly Dictionary<Material, Material> kopyalar = new Dictionary<Material, Material>();
        static readonly int SalinimId = Shader.PropertyToID("_Salinim");
        static Shader cizgiShader;
        static bool cizgiArandi;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Sifirla()
        {
            kopyalar.Clear();
            cizgiShader = null;
            cizgiArandi = false;
        }

        /// <summary>Rüzgârda sallanacak bir model mi (ağaç ya da çalı)?</summary>
        public static bool AgacMi(GameObject prefab) =>
            prefab != null && (prefab.name.StartsWith("Agac_") || prefab.name.StartsWith("Cali_"));

        /// <summary>Modelin parçalarına salınan malzeme kopyalarını takar (renklendirmeden önce çağrılmalı).</summary>
        public static void Uygula(Renderer[] parcalar)
        {
            if (parcalar == null) return;
            foreach (var r in parcalar)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                bool degisti = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var k = Kopya(mats[i]);
                    if (k != mats[i]) { mats[i] = k; degisti = true; }
                }
                if (degisti) r.sharedMaterials = mats;
            }
        }

        static Material Kopya(Material m)
        {
            if (m == null) return null;
            if (kopyalar.TryGetValue(m, out var k) && k != null) return k;
            if (m.HasProperty(SalinimId))
            {
                k = new Material(m) { name = m.name };
                k.SetFloat(SalinimId, 1f);
            }
            else if (m.shader != null && m.shader.name == "Universal Render Pipeline/Unlit")
            {
                if (!cizgiArandi) { cizgiArandi = true; cizgiShader = Shader.Find("KuryeTycoon/CizgiSalinim"); }
                if (cizgiShader == null) return m;
                k = new Material(cizgiShader) { name = m.name };
                k.SetColor("_BaseColor", m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color);
                if (m.HasProperty("_Cull")) k.SetFloat("_Cull", m.GetFloat("_Cull"));
                k.SetFloat(SalinimId, 1f);
                k.renderQueue = m.renderQueue;
            }
            else return m;
            k.enableInstancing = m.enableInstancing;
            kopyalar[m] = k;
            return k;
        }
    }
}
