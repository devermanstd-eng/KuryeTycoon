using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// "Kurye Tycoon > 3D Işık ve Gölge Kurulumu": gerçek güneş ışığı ve gölge için Universal Renderer ayarı oluşturur.
    ///  • Proje 2D Renderer ile açılmıştı; o renderer 3D modellere güneş ışığı ve gölge vermez.
    ///  • Oluşan dosyalar (Resources/Gorsel) depoya girer: arkadaşlar menüyü çalıştırmadan da aynı görünümü alır.
    ///    Oyun açılırken Gorsel3D bu ayara geçer.
    ///  • Ayar: yumuşak gölge, 4 kademe, 4096 gölge haritası, HDR, MSAA 4x, ekran ortam gölgesi (SSAO).
    /// </summary>
    public static class GorselKurulum
    {
        const string Klasor = "Assets/KuryeTycoon/Resources/Gorsel";
        const string RendererYol = Klasor + "/KuryeURP_Renderer.asset";
        const string PipelineYol = Klasor + "/KuryeURP.asset";

        [MenuItem("Kurye Tycoon/3D Işık ve Gölge Kurulumu")]
        public static void Kur()
        {
            if (!AssetDatabase.IsValidFolder("Assets/KuryeTycoon/Resources"))
                AssetDatabase.CreateFolder("Assets/KuryeTycoon", "Resources");
            if (!AssetDatabase.IsValidFolder(Klasor))
                AssetDatabase.CreateFolder("Assets/KuryeTycoon/Resources", "Gorsel");

            // ---- Renderer (Universal, forward)
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererYol);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererYol);
            }
            EksikKaynaklariDoldur(renderer);
            SsaoEkle(renderer);
            EditorUtility.SetDirty(renderer);

            // ---- Render pipeline ayarı
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineYol);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, PipelineYol);
            }
            var so = new SerializedObject(asset);
            Ayarla(so, "m_RendererDataList.Array.data[0]", renderer);
            Ayarla(so, "m_DefaultRendererIndex", 0);
            Ayarla(so, "m_SupportsHDR", true);
            Ayarla(so, "m_MSAA", 4);
            Ayarla(so, "m_RenderScale", 1f);
            Ayarla(so, "m_RequireDepthTexture", true);
            Ayarla(so, "m_MainLightRenderingMode", 1);              // piksel başına
            Ayarla(so, "m_MainLightShadowsSupported", true);
            Ayarla(so, "m_MainLightShadowmapResolution", 4096);
            Ayarla(so, "m_AdditionalLightsRenderingMode", 1);
            Ayarla(so, "m_ShadowDistance", 140f);                    // harita ~50 kare; uzaktan bakınca da gölge kalsın
            Ayarla(so, "m_ShadowCascadeCount", 4);
            Ayarla(so, "m_Cascade4Split", new Vector3(0.08f, 0.22f, 0.5f));
            Ayarla(so, "m_ShadowDepthBias", 1.0f);
            Ayarla(so, "m_ShadowNormalBias", 0.6f);
            Ayarla(so, "m_SoftShadowsSupported", true);
            Ayarla(so, "m_SoftShadowQuality", 3);                   // yüksek
            Ayarla(so, "m_ConservativeEnclosingSphere", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            // ---- Projede de bu ayar kullanılsın (editörde de gölgeler görünür)
            GraphicsSettings.defaultRenderPipeline = asset;
            int eski = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(eski, false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Kurye Tycoon: 3D ışık ve gölge ayarı hazır (" + PipelineYol + "). Play'e basınca güneş ve gölgeler görünür.");
            EditorUtility.DisplayDialog("3D Işık ve Gölge",
                "Gölgeli render ayarı oluşturuldu ve projeye atandı.\n\n" + PipelineYol +
                "\n\nPlay'e basınca güneş ışığı, yumuşak gölgeler ve renk ayarı görünür.\n" +
                "Bu dosyaları GitHub'a göndermeyi unutma: arkadaşların da aynı görünümü alır.", "Tamam");
        }

        /// <summary>Kodla oluşturulan renderer'da shader ve efekt kaynakları boş kalabilir: URP'nin kendi yükleyicisiyle,
        /// olmazsa projedeki 2D Renderer'dan kopyalayarak doldurur.</summary>
        static void EksikKaynaklariDoldur(UniversalRendererData renderer)
        {
            try
            {
                var m = renderer.GetType().GetMethod("ReloadAllNullProperties",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                m?.Invoke(renderer, null);
            }
            catch (Exception e) { Debug.LogWarning("Kurye Tycoon: renderer kaynakları yüklenemedi: " + e.Message); }

            // Alan adı URP sürümüne göre değişmesin diye SerializedObject ile (kod derlemesi alan adına bağlı kalmaz)
            var so = new SerializedObject(renderer);
            var alan = so.FindProperty("postProcessData");
            if (alan == null || alan.objectReferenceValue != null) return;
            PostProcessData bulunan = null;
            // Projedeki 2D Renderer'ın efekt verisi (ton eşleme, ışıma) aynı paketten gelir
            foreach (var guid in AssetDatabase.FindAssets("t:Renderer2DData"))
            {
                var r2 = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (r2 == null) continue;
                var p = new SerializedObject(r2).FindProperty("m_PostProcessData");
                if (p != null && p.objectReferenceValue is PostProcessData ppd) { bulunan = ppd; break; }
            }
            if (bulunan == null)
            {
                // Son çare: paketin varsayılan efekt verisi
                foreach (var guid in AssetDatabase.FindAssets("t:PostProcessData"))
                {
                    bulunan = AssetDatabase.LoadAssetAtPath<PostProcessData>(AssetDatabase.GUIDToAssetPath(guid));
                    if (bulunan != null) break;
                }
            }
            if (bulunan == null)
            {
                Debug.LogWarning("Kurye Tycoon: PostProcessData bulunamadı; gölgeler çalışır ama renk efektleri (ton eşleme, ışıma) görünmeyebilir.");
                return;
            }
            alan.objectReferenceValue = bulunan;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Ekran ortam gölgesi (SSAO): binaların dibi ve köşeler hafifçe koyulaşır (referanstaki yumuşak derinlik).</summary>
        static void SsaoEkle(UniversalRendererData renderer)
        {
            foreach (var f in renderer.rendererFeatures)
                if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") return;

            var tip = Type.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion, Unity.RenderPipelines.Universal.Runtime");
            if (tip == null) { Debug.LogWarning("Kurye Tycoon: SSAO bulunamadı, ortam gölgesi eklenmedi."); return; }
            var ozellik = ScriptableObject.CreateInstance(tip) as ScriptableRendererFeature;
            if (ozellik == null) return;
            ozellik.name = "Ortam Gölgesi (SSAO)";
            AssetDatabase.AddObjectToAsset(ozellik, renderer);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ozellik, out string _, out long localId);

            var so = new SerializedObject(renderer);
            var liste = so.FindProperty("m_RendererFeatures");
            var harita = so.FindProperty("m_RendererFeatureMap");
            if (liste != null)
            {
                liste.arraySize++;
                liste.GetArrayElementAtIndex(liste.arraySize - 1).objectReferenceValue = ozellik;
            }
            if (harita != null)
            {
                harita.arraySize++;
                harita.GetArrayElementAtIndex(harita.arraySize - 1).longValue = localId;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // Bizim ölçek: bir kare 1 birim (10 m). Yarıçap ve güç buna göre
            var fs = new SerializedObject(ozellik);
            Ayarla(fs, "m_Settings.Intensity", 1.6f);
            Ayarla(fs, "m_Settings.DirectLightingStrength", 0.3f);
            Ayarla(fs, "m_Settings.Radius", 0.06f);
            Ayarla(fs, "m_Settings.Falloff", 25f);
            fs.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ozellik);
        }

        /// <summary>Alan bu URP sürümünde varsa ayarlar; yoksa sessizce geçer.</summary>
        static void Ayarla(SerializedObject so, string yol, object deger)
        {
            var p = so.FindProperty(yol);
            if (p == null) return;
            switch (deger)
            {
                case bool b: if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = b; break;
                case int i:
                    if (p.propertyType == SerializedPropertyType.Integer || p.propertyType == SerializedPropertyType.Enum) p.intValue = i;
                    break;
                case float f: if (p.propertyType == SerializedPropertyType.Float) p.floatValue = f; break;
                case Vector3 v: if (p.propertyType == SerializedPropertyType.Vector3) p.vector3Value = v; break;
                case UnityEngine.Object o: if (p.propertyType == SerializedPropertyType.ObjectReference) p.objectReferenceValue = o; break;
            }
        }
    }
}
