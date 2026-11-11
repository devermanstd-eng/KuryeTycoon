using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// Blender scriptinin (Tools/Blender/kurye_modeller.py) ürettiği bina FBX'lerini oyuna prefab olarak ekler.
    ///  • FBX'ler: Assets/KuryeTycoon/Modeller/Binalar (Resources dışında, oyun FBX'i ayrıca yüklemesin)
    ///  • Malzemeler: Assets/KuryeTycoon/Modeller/Malzemeler (URP Lit; "Cizgi" URP Unlit siyah)
    ///  • Prefablar: Assets/KuryeTycoon/Resources/SehirPaketi/Binalar (oyun buradan yükler)
    /// Malzeme rengi Blender'daki malzeme adından okunur (ör. "Duvar_f1e4c8" → #f1e4c8), aynı renk tek malzeme olur.
    /// </summary>
    public static class BlenderBinaKurulum
    {
        const string ModelKlasor = "Assets/KuryeTycoon/Modeller/Binalar";
        const string MalzemeKlasor = "Assets/KuryeTycoon/Modeller/Malzemeler";
        const string PrefabKlasor = "Assets/KuryeTycoon/Resources/SehirPaketi/Binalar";
        const string EskiKlasor = "Assets/Resources/SehirPaketi/Binalar";
        static readonly string[] Onekler = { "MustakilEv_", "SiraEv_", "OfisKulesi_", "Market_", "FastFood_", "Apartman_", "Yol_", "Agac_", "Cali_", "Park_", "Meydan_", "Avlu_", "Kir_", "Dukkan_", "Konut_", "Buro_", "Sanayi_", "Kamu_", "Hava_" };
        const string ParkKlasor = "Assets/KuryeTycoon/Resources/SehirPaketi/Parklar";
        const string YolKlasor = "Assets/KuryeTycoon/Resources/SehirPaketi/Yollar";
        const string AgacKlasor = "Assets/KuryeTycoon/Resources/SehirPaketi/Agaclar";
        const string HavaKlasor = "Assets/KuryeTycoon/Resources/SehirPaketi/Hava";   // bulut, damla, kar, yaprak, şimşek (Hava3D)

        /// <summary>Prefabın gideceği klasör: yollar ve zemin Yollar'a, ağaç ve çalılar Agaclar'a, park parçaları Parklar'a, gerisi Binalar'a.</summary>
        static string Hedef(string ad) =>
            ad.StartsWith("Hava_") ? HavaKlasor :
            ad.StartsWith("Yol_") ? YolKlasor : (ad.StartsWith("Park_") || ad.StartsWith("Meydan_") || ad.StartsWith("Avlu_") || ad.StartsWith("Kir_")) ? ParkKlasor : (ad.StartsWith("Agac_") || ad.StartsWith("Cali_")) ? AgacKlasor : PrefabKlasor;

        [MenuItem("Kurye Tycoon/Blender Binalarını Prefab Yap")]
        static void Kur()
        {
            Klasor(ModelKlasor);
            Klasor(MalzemeKlasor);
            Klasor(PrefabKlasor);
            Klasor(YolKlasor);
            Klasor(AgacKlasor);
            Klasor(ParkKlasor);
            Klasor(HavaKlasor);

            // 1) Eski yere (Resources) kaydedilmiş Blender FBX'lerini model klasörüne taşı
            int tasinan = 0;
            if (AssetDatabase.IsValidFolder(EskiKlasor))
                foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { EskiKlasor }))
                {
                    string yol = AssetDatabase.GUIDToAssetPath(g);
                    if (!BlenderBinasi(yol)) continue;
                    string hedef = ModelKlasor + "/" + Path.GetFileName(yol);
                    if (AssetDatabase.LoadAssetAtPath<Object>(hedef) != null) AssetDatabase.DeleteAsset(hedef);
                    if (string.IsNullOrEmpty(AssetDatabase.MoveAsset(yol, hedef))) tasinan++;
                }

            // Eski adlı hafif sürümler (_LOD1): Unity bu eki kendi LOD sistemine ait sanıp uyarı veriyordu, artık _Hafif
            foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { ModelKlasor }))
            {
                string yol = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(yol).EndsWith("_LOD1")) AssetDatabase.DeleteAsset(yol);
            }

            // 2) Her FBX'ten prefab
            var yollar = new List<string>();
            foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { ModelKlasor }))
            {
                string yol = AssetDatabase.GUIDToAssetPath(g);
                if (BlenderBinasi(yol)) yollar.Add(yol);
            }
            yollar.Sort();

            var malzemeler = new Dictionary<string, Material>();
            int yapilan = 0, lodlu = 0;
            foreach (var yol in yollar)
            {
                IceAktarmaAyari(yol);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(yol);
                if (model == null) continue;
                // Hafif sürüm (ADI_Hafif.fbx) varsa: LOD Group, yakında tam detay, uzakta hafif model
                string lodYol = yol.Substring(0, yol.Length - 4) + "_Hafif.fbx";
                var lod = AssetDatabase.LoadAssetAtPath<GameObject>(lodYol);
                GameObject kopya;
                if (lod != null)
                {
                    IceAktarmaAyari(lodYol);
                    kopya = new GameObject(model.name);
                    var l0 = Object.Instantiate(model, kopya.transform); l0.name = "LOD0";
                    var l1 = Object.Instantiate(lod, kopya.transform); l1.name = "LOD1";
                    MalzemeAta(l0, malzemeler);
                    MalzemeAta(l1, malzemeler);
                    var grup = kopya.AddComponent<LODGroup>();
                    grup.SetLODs(new[]
                    {
                        new LOD(LodGecis, l0.GetComponentsInChildren<Renderer>(true)),
                        new LOD(0.0005f, l1.GetComponentsInChildren<Renderer>(true)),
                    });
                    grup.RecalculateBounds();
                    lodlu++;
                }
                else
                {
                    kopya = Object.Instantiate(model);
                    kopya.name = model.name;
                    MalzemeAta(kopya, malzemeler);
                }
                string prefabYol = Hedef(model.name) + "/" + model.name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(kopya, prefabYol);
                Object.DestroyImmediate(kopya);
                yapilan++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string mesaj = yollar.Count == 0
                ? "Blender binası bulunamadı.\n\nBlender'da kurye_modeller.py'yi çalıştır; FBX'ler " + ModelKlasor + " klasörüne kaydedilir."
                : $"{yapilan} prefab hazır (binalar, yollar ve ağaçlar: Assets/KuryeTycoon/Resources/SehirPaketi).\n• {malzemeler.Count} malzeme\n" +
                  (lodlu > 0 ? $"• {lodlu} prefab LOD'lu (uzakta hafif model)\n" : "") +
                  (tasinan > 0 ? $"• {tasinan} FBX Resources'tan Modeller klasörüne taşındı\n" : "") +
                  "\nYeni oyun başlatınca şehirde görünürler.";
            EditorUtility.DisplayDialog("Kurye Tycoon", mesaj, "Tamam");
        }

        /// <summary>LOD0'dan LOD1'e geçiş: model ekranın bu oranından küçük görününce hafif sürüm.</summary>
        const float LodGecis = 0.08f;

        static void MalzemeAta(GameObject go, Dictionary<string, Material> malzemeler)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null) mats[i] = Malzeme(mats[i], malzemeler);
                r.sharedMaterials = mats;
            }
        }

        static bool BlenderBinasi(string yol)
        {
            string ad = Path.GetFileName(yol);
            if (!ad.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) return false;
            string kok = Path.GetFileNameWithoutExtension(ad);
            if (kok.EndsWith("_Hafif") || kok.EndsWith("_LOD1")) return false;   // hafif sürüm ayrı prefab olmaz
            foreach (var o in Onekler) if (ad.StartsWith(o)) return true;
            return false;
        }

        static void IceAktarmaAyari(string yol)
        {
            var mi = AssetImporter.GetAtPath(yol) as ModelImporter;
            if (mi == null) return;
            bool degisti = false;
            if (mi.importCameras) { mi.importCameras = false; degisti = true; }
            if (mi.importLights) { mi.importLights = false; degisti = true; }
            if (mi.importAnimation) { mi.importAnimation = false; degisti = true; }
            if (mi.animationType != ModelImporterAnimationType.None) { mi.animationType = ModelImporterAnimationType.None; degisti = true; }
            if (degisti) mi.SaveAndReimport();
        }

        /// <summary>FBX'in içindeki malzemenin yerine, adındaki renkle URP malzemesi (bir kez oluşturulur).</summary>
        static Material Malzeme(Material kaynak, Dictionary<string, Material> onbellek)
        {
            string ad = kaynak.name.Replace(" (Instance)", "");
            if (onbellek.TryGetValue(ad, out var m)) return m;

            // Çizgi siyah ve ışıksız; şimşek de ışıksız (gölgede kalmasın, hep parlak görünsün)
            bool cizgi = ad.StartsWith("Cizgi") || ad.StartsWith("Simsek");
            Color renk = AddanRenk(ad, out bool bulundu);
            if (!bulundu) renk = kaynak.HasProperty("_BaseColor") ? kaynak.GetColor("_BaseColor") : kaynak.color;

            string yol = MalzemeKlasor + "/" + ad + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(yol);
            var shader = Shader.Find(cizgi ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find(cizgi ? "Unlit/Color" : "Standard");
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, yol);
            }
            else m.shader = shader;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", renk);
            m.color = renk;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", ad.StartsWith("Cam") ? 0.6f : 0.1f);
            EditorUtility.SetDirty(m);
            onbellek[ad] = m;
            return m;
        }

        static Color AddanRenk(string ad, out bool bulundu)
        {
            bulundu = false;
            int i = ad.LastIndexOf('_');
            if (i < 0 || ad.Length - i - 1 < 6) return Color.white;
            string hex = ad.Substring(i + 1, 6);
            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) return Color.white;
            bulundu = true;
            return new Color32((byte)(v >> 16), (byte)((v >> 8) & 255), (byte)(v & 255), 255);
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
