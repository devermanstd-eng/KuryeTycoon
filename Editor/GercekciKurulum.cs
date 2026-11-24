using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// Gerçekçi yapılar (Tools/Blender/gercekci_yapilar.py) → oyun.
    ///  • Dokular: Modeller/Gercekci/Dokular (Tools/gercekci_doku.py). *_N.png normal haritası olarak içe aktarılır.
    ///  • Malzemeler: Modeller/Gercekci/Malzemeler/&lt;ad&gt;.mat (URP Lit; renk + normal dokusu, pürüzsüzlük, metal, ışıma).
    ///    Adlar Blender betiğindeki MALZ tablosuyla aynı; tablo burada da tutulur.
    ///  • Prefablar: Resources/SehirPaketi/Gercekci/Gercekci_&lt;ad&gt;.prefab (City3D yükler). SirketRengi_* malzemeleri oyunda
    ///    oyuncunun şirket rengine boyanır.
    /// Menü: Kurye Tycoon > Gerçekçi Yapıları Kur (Blender önizlemesi bitince kendiliğinden de çalışır).
    /// </summary>
    public static class GercekciKurulum
    {
        public const string MenuYolu = "Kurye Tycoon/Gerçekçi Yapıları Kur";
        const string Kok = "Assets/KuryeTycoon/Modeller/Gercekci";
        const string DokuKlasor = Kok + "/Dokular";
        const string MalzemeKlasor = Kok + "/Malzemeler";
        const string PrefabKlasor = "Assets/KuryeTycoon/Resources/SehirPaketi/Gercekci";

        // ad → (doku, renk, pürüzlülük, metal, ışıma gücü, normal gücü). Blender: gercekci_yapilar.py > MALZ
        static readonly Dictionary<string, (string doku, Color renk, float pur, float metal, float isima, float normal)> Tablo =
            new Dictionary<string, (string, Color, float, float, float, float)>
        {
            { "Tugla_Kirmizi", ("Tugla_Kirmizi", Color.white, 0.85f, 0f, 0f, 1f) },
            { "Tugla_Kahve", ("Tugla_Kahve", Color.white, 0.85f, 0f, 0f, 1f) },
            { "Tas_Kirec", ("Tas_Kirec", Color.white, 0.75f, 0f, 0f, 0.8f) },
            { "Tas_Koyu", ("Tas_Koyu", Color.white, 0.6f, 0f, 0f, 0.8f) },
            { "Siva_Beyaz", ("Siva_Beyaz", Color.white, 0.9f, 0f, 0f, 0.5f) },
            { "Beton", ("Beton", Color.white, 0.85f, 0f, 0f, 0.7f) },
            { "Metal_Antrasit", ("Metal_Antrasit", Color.white, 0.45f, 0.6f, 0f, 0.5f) },
            { "Metal_Kepenk", ("Metal_Antrasit", new Color(1.25f, 1.25f, 1.25f), 0.5f, 0.5f, 0f, 1f) },
            { "Cati_Membran", ("Cati_Membran", Color.white, 0.9f, 0f, 0f, 0.6f) },
            { "Kaldirim", ("Kaldirim", Color.white, 0.85f, 0f, 0f, 0.8f) },
            { "Cam", (null, new Color(0.015f, 0.02f, 0.026f), 0.06f, 0f, 0f, 0f) },
            { "Ic_Mekan", (null, new Color(0.55f, 0.45f, 0.33f), 0.9f, 0f, 0.6f, 0f) },
            { "SirketRengi_Tente", (null, new Color(0.02f, 0.30f, 0.62f), 0.6f, 0f, 0f, 0f) },
            { "SirketRengi_Tabela", (null, new Color(0.02f, 0.30f, 0.62f), 0.35f, 0f, 0.4f, 0f) },
            { "Tabela_Yazi", (null, new Color(0.97f, 0.97f, 0.95f), 0.3f, 0f, 3f, 0f) },
            { "Asfalt", ("Beton", new Color(0.17f, 0.17f, 0.18f), 0.9f, 0f, 0f, 0.5f) },
            { "Bitki", (null, new Color(0.05f, 0.13f, 0.03f), 0.75f, 0f, 0f, 0f) },
            { "Toprak", (null, new Color(0.16f, 0.11f, 0.07f), 0.95f, 0f, 0f, 0f) },
            { "Lamba", (null, new Color(1f, 0.85f, 0.6f), 0.3f, 0f, 2f, 0f) },
        };

        [MenuItem(MenuYolu)]
        public static void Kur()
        {
            Klasor(MalzemeKlasor);
            Klasor(PrefabKlasor);
            DokulariAyarla();
            var malzemeler = new Dictionary<string, Material>();
            foreach (var kv in Tablo) malzemeler[kv.Key] = MalzemeYap(kv.Key, kv.Value);
            int sayi = 0;
            foreach (var guid in AssetDatabase.FindAssets("Gercekci_ t:Model", new[] { Kok }))
            {
                string yol = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(yol).Replace('\\', '/') != Kok) continue;
                if (PrefabYap(yol, malzemeler)) sayi++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Kurye Tycoon: gerçekçi yapılar kuruldu — {sayi} prefab, {malzemeler.Count} malzeme ({PrefabKlasor}).");
        }

        static void DokulariAyarla()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { DokuKlasor }))
            {
                string yol = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(yol) as TextureImporter;
                if (ti == null) continue;
                bool normal = Path.GetFileNameWithoutExtension(yol).EndsWith("_N");
                bool degisti = false;
                var tur = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (ti.textureType != tur) { ti.textureType = tur; degisti = true; }
                if (ti.wrapMode != TextureWrapMode.Repeat) { ti.wrapMode = TextureWrapMode.Repeat; degisti = true; }
                if (ti.anisoLevel != 4) { ti.anisoLevel = 4; degisti = true; }
                if (ti.maxTextureSize != 1024) { ti.maxTextureSize = 1024; degisti = true; }
                if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; degisti = true; }
                if (degisti) ti.SaveAndReimport();
            }
        }

        static Material MalzemeYap(string ad, (string doku, Color renk, float pur, float metal, float isima, float normal) t)
        {
            string yol = MalzemeKlasor + "/" + ad + ".mat";
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(yol);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, yol); }
            m.shader = sh;
            m.name = ad;
            var renk = t.renk; renk.a = 1f;
            m.SetColor("_BaseColor", renk);
            m.SetFloat("_Smoothness", 1f - t.pur);
            m.SetFloat("_Metallic", t.metal);
            if (t.doku != null)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DokuKlasor + "/" + t.doku + ".png");
                m.SetTexture("_BaseMap", tex);
                var nor = AssetDatabase.LoadAssetAtPath<Texture2D>(DokuKlasor + "/" + t.doku + "_N.png");
                if (nor != null && t.normal > 0f)
                {
                    m.SetTexture("_BumpMap", nor);
                    m.SetFloat("_BumpScale", t.normal);
                    m.EnableKeyword("_NORMALMAP");
                }
            }
            if (t.isima > 0f)
            {
                m.SetColor("_EmissionColor", renk * t.isima);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else m.DisableKeyword("_EMISSION");
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static bool PrefabYap(string modelYolu, Dictionary<string, Material> malzemeler)
        {
            var mi = AssetImporter.GetAtPath(modelYolu) as ModelImporter;
            if (mi != null)
            {
                bool d = false;
                if (mi.importCameras) { mi.importCameras = false; d = true; }
                if (mi.importLights) { mi.importLights = false; d = true; }
                if (mi.importAnimation) { mi.importAnimation = false; d = true; }
                if (mi.animationType != ModelImporterAnimationType.None) { mi.animationType = ModelImporterAnimationType.None; d = true; }
                if (!mi.isReadable) { mi.isReadable = true; d = true; }          // kar katmanı mesh'i okur
                if (d) mi.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelYolu);
            if (model == null) return false;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go = PrefabUtility.IsPartOfPrefabInstance(go) ? UnpackOrSelf(go) : go;
            int eksik = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string ad = mats[i].name;
                    int nokta = ad.IndexOf('.');
                    if (nokta > 0) ad = ad.Substring(0, nokta);                    // Blender "Tugla_Kirmizi.001"
                    if (malzemeler.TryGetValue(ad, out var m)) mats[i] = m; else eksik++;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            if (eksik > 0) Debug.LogWarning($"Kurye Tycoon: {Path.GetFileName(modelYolu)} — tabloda olmayan {eksik} malzeme (Blender MALZ ile GercekciKurulum.Tablo aynı olmalı).");
            string hedef = PrefabKlasor + "/" + Path.GetFileNameWithoutExtension(modelYolu) + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, hedef);
            Object.DestroyImmediate(go);
            return true;
        }

        static GameObject UnpackOrSelf(GameObject go)
        {
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return go;
        }

        static void Klasor(string yol)
        {
            if (AssetDatabase.IsValidFolder(yol)) return;
            string ust = Path.GetDirectoryName(yol).Replace('\\', '/');
            Klasor(ust);
            AssetDatabase.CreateFolder(ust, Path.GetFileName(yol));
        }
    }
}
