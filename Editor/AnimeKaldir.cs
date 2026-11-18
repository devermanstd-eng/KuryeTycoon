using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// Anime stil (AnimeToon shader + Murekkep Cizgi ekran geçişi) projeden tamamen kaldırıldı. Bu araç kalıntıları temizler:
    ///  • Modeller/Malzemeler'de AnimeToon'da kalan ya da shader'ı kayıp (pembe) malzemeler URP/Lit'e döner;
    ///    renk malzeme adından okunur ("Duvar_f1e4c8" → #f1e4c8), pencere/vitrin/lamba malzemelerinde gece ışığı açık kalır.
    ///  • Renderer'daki "Murekkep Cizgi" geçişi silinir.
    ///  • AnimeToon.shader, MurekkepCizgi.shader/.mat ve Editor/AnimeStil.cs varsa silinir.
    /// Editör her açıldığında kendiliğinden bakar (iş yoksa hiçbir şey yapmaz); elle: Kurye Tycoon > Anime Stil Kalıntılarını Temizle.
    /// </summary>
    [InitializeOnLoad]
    public static class AnimeKaldir
    {
        const string MalzemeKlasor = "Assets/KuryeTycoon/Modeller/Malzemeler";
        const string GorselKlasor = "Assets/KuryeTycoon/Resources/Gorsel";
        const string RendererYol = GorselKlasor + "/KuryeURP_Renderer.asset";
        static readonly string[] Silinecek =
        {
            GorselKlasor + "/AnimeToon.shader", GorselKlasor + "/MurekkepCizgi.shader", GorselKlasor + "/MurekkepCizgi.mat",
            "Assets/KuryeTycoon/Editor/AnimeStil.cs",
        };

        static AnimeKaldir()
        {
            EditorApplication.delayCall += () => Temizle(false);
        }

        [MenuItem("Kurye Tycoon/Anime Stil Kalıntılarını Temizle")]
        static void Menu() => Temizle(true);

        static void Temizle(bool rapor)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return;
            int malzeme = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { MalzemeKlasor }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (m == null) continue;
                string sn = m.shader != null ? m.shader.name : "";
                string ad = m.name;
                bool bozuk = m.shader == null || sn == "Hidden/InternalErrorShader" || sn == "KuryeTycoon/AnimeToon";
                // Çizgi kabukları anime stilde mürekkep kahvesine boyanmıştı: adındaki renge (siyah) dönsün
                if (!bozuk && ad.StartsWith("Cizgi"))
                {
                    var c0 = AddanRenk(ad, out bool b0);
                    var simdi = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
                    bozuk = b0 && ((Vector4)(simdi - c0)).sqrMagnitude > 0.0004f;
                }
                if (!bozuk) continue;
                bool cizgi = ad.StartsWith("Cizgi");
                var renk = AddanRenk(ad, out bool bulundu);
                if (!bulundu) renk = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.gray;
                if (cizgi)
                {
                    var unlit = Shader.Find("Universal Render Pipeline/Unlit");
                    m.shader = unlit != null ? unlit : lit;
                }
                else m.shader = lit;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", renk);
                m.color = renk;
                if (!cizgi && m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", ad.StartsWith("Cam") ? 0.6f : 0.1f);
                bool isikVeren = ad.StartsWith("Vitrin_") || ad.StartsWith("Yazi_") || ad.StartsWith("Cam_") || ad.StartsWith("Cam2_") || ad.StartsWith("Cam3_") ||
                                 ad.StartsWith("CamKoyu") || ad.StartsWith("OfisCam") || ad.StartsWith("YIsik") || ad.StartsWith("PIsik") ||
                                 ad.StartsWith("Isik_") || ad.StartsWith("TSari_isik");
                if (isikVeren && m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", Color.black);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                EditorUtility.SetDirty(m);
                malzeme++;
            }

            bool gecis = GecisiSil();
            int dosya = 0;
            foreach (var y in Silinecek)
                if (AssetDatabase.LoadAssetAtPath<Object>(y) != null && AssetDatabase.DeleteAsset(y)) dosya++;

            if (malzeme > 0 || gecis || dosya > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"Kurye Tycoon: anime stil kaldırıldı — {malzeme} malzeme URP/Lit'e döndü, " +
                          (gecis ? "mürekkep çizgisi geçişi silindi, " : "") + $"{dosya} dosya silindi.");
            }
            if (rapor)
                EditorUtility.DisplayDialog("Anime stil", malzeme + dosya == 0 && !gecis ? "Temizlenecek bir şey kalmadı." :
                    $"{malzeme} malzeme URP/Lit'e döndü\n{(gecis ? "Mürekkep çizgisi geçişi silindi\n" : "")}{dosya} dosya silindi", "Tamam");
        }

        /// <summary>Renderer'dan "Murekkep Cizgi" ekran geçişini (ve kayıp script'li geçişleri) siler.</summary>
        static bool GecisiSil()
        {
            var r = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererYol);
            if (r == null) return false;
            var so = new SerializedObject(r);
            var liste = so.FindProperty("m_RendererFeatures");
            var harita = so.FindProperty("m_RendererFeatureMap");
            bool silindi = false;
            for (int i = liste.arraySize - 1; i >= 0; i--)
            {
                var el = liste.GetArrayElementAtIndex(i);
                var f = el.objectReferenceValue as ScriptableRendererFeature;
                if (f == null || f.name != "Murekkep Cizgi") continue;
                el.objectReferenceValue = null;
                liste.DeleteArrayElementAtIndex(i);
                if (harita != null && i < harita.arraySize) harita.DeleteArrayElementAtIndex(i);
                AssetDatabase.RemoveObjectFromAsset(f);
                Object.DestroyImmediate(f, true);
                silindi = true;
            }
            if (!silindi) return false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(r);
            return true;
        }

        static Color AddanRenk(string ad, out bool bulundu)
        {
            bulundu = false;
            int i = ad.LastIndexOf('_');
            if (i < 0 || ad.Length - i - 1 < 6) return Color.white;
            if (!int.TryParse(ad.Substring(i + 1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) return Color.white;
            bulundu = true;
            return new Color32((byte)(v >> 16), (byte)((v >> 8) & 255), (byte)(v & 255), 255);
        }
    }
}
