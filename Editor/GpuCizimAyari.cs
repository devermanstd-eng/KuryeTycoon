using UnityEditor;
using UnityEngine;

namespace KuryeTycoon.EditorAraclari
{
    /// <summary>
    /// GPU Resident Drawer (u72; KuryeURP.asset > Instanced Drawing) için proje ayarı: Graphics > Shader Stripping >
    /// BatchRendererGroup Variants = Keep All. Bu ayar olmadan Unity GPU Resident Drawer'ı editörde de çalıştırmıyor ve oyun
    /// derlemesinde DOTS instancing varyantlarını siliyor. Ayar ProjectSettings/GraphicsSettings.asset'te durur (depoda değil):
    /// editör açılınca kendiliğinden yazılır. Yazılamazsa Console'a elle nasıl yapılacağı yazılır.
    /// </summary>
    [InitializeOnLoad]
    static class GpuCizimAyari
    {
        static GpuCizimAyari()
        {
            EditorApplication.delayCall += Uygula;
        }

        static void Uygula()
        {
            try
            {
                var varliklar = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
                if (varliklar == null || varliklar.Length == 0 || varliklar[0] == null) { Uyar(); return; }
                var so = new SerializedObject(varliklar[0]);
                var p = so.FindProperty("m_BrgStripping");
                if (p == null) { Uyar(); return; }
                int hepsi = -1;
                if (p.propertyType == SerializedPropertyType.Enum)
                {
                    var adlar = p.enumNames;
                    for (int i = 0; i < adlar.Length; i++)
                        if (adlar[i].Replace(" ", "").ToLowerInvariant().Contains("keepall")) { hepsi = i; break; }
                    if (hepsi < 0) { Uyar(); return; }
                    if (p.enumValueIndex == hepsi) return;
                    p.enumValueIndex = hepsi;
                }
                else
                {
                    // Eski Unity'lerde düz sayı: 0 Entities Graphics yoksa sil · 1 hepsini sil · 2 hepsini tut
                    if (p.intValue == 2) return;
                    p.intValue = 2;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log("Kurye Tycoon: Graphics > BatchRendererGroup Variants = Keep All yapıldı (GPU Resident Drawer için).");
            }
            catch (System.Exception) { Uyar(); }
        }

        static void Uyar()
        {
            Debug.LogWarning("Kurye Tycoon: BatchRendererGroup ayarı kodla yapılamadı. Elle: Project Settings > Graphics > Shader Stripping > " +
                             "BatchRendererGroup Variants = Keep All (yoksa GPU Resident Drawer çalışmaz, şehir çok daha yavaş çizilir).");
        }
    }
}
