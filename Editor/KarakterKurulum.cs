using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// Asset Store'dan içe aktarılan karakter paketindeki (ör. City People FREE Samples) prefabları
    /// oyunun yükleyebileceği Assets/Resources/KuryeKarakterleri klasörüne kopyalar.
    /// Bu klasör repoda değildir; Asset Store lisansı modellerin herkese açık repoda paylaşılmasına izin vermez.
    /// </summary>
    public static class KarakterKurulum
    {
        const string Target = "Assets/Resources/KuryeKarakterleri";

        [MenuItem("Kurye Tycoon/Karakterleri Kur (City People)")]
        static void KurCityPeople()
        {
            var found = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string p = path.ToLowerInvariant().Replace(" ", "");
                if (!p.Contains("citypeople")) continue;
                if (path.StartsWith(Target)) continue;
                if (IsCharacter(path)) found.Add(path);
            }
            Copy(found, "City People paketinde karakter prefabı bulunamadı.\n\n" +
                        "Paketi Window > Package Manager > My Assets'ten indirip Import ettiğinden emin ol. " +
                        "Farklı bir klasördeyse, karakter prefablarının olduğu klasörü Project penceresinde seçip " +
                        "Kurye Tycoon > Seçili Klasördeki Karakterleri Kur'u kullan.");
        }

        [MenuItem("Kurye Tycoon/Seçili Klasördeki Karakterleri Kur")]
        static void KurSecili()
        {
            var folders = new List<string>();
            foreach (var o in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(o);
                if (AssetDatabase.IsValidFolder(path)) folders.Add(path);
            }
            if (folders.Count == 0)
            {
                EditorUtility.DisplayDialog("Kurye Tycoon", "Önce Project penceresinde karakter prefablarının olduğu klasörü seç.", "Tamam");
                return;
            }
            var found = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", folders.ToArray()))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(Target) && IsCharacter(path)) found.Add(path);
            }
            Copy(found, "Seçili klasörde karakter (SkinnedMeshRenderer içeren) prefab bulunamadı.");
        }

        [MenuItem("Kurye Tycoon/Karakterleri Kaldır")]
        static void Kaldir()
        {
            if (!AssetDatabase.IsValidFolder(Target)) return;
            if (!EditorUtility.DisplayDialog("Kurye Tycoon", "Resources/KuryeKarakterleri klasörü silinsin mi? (Orijinal paket silinmez.)", "Sil", "Vazgeç")) return;
            AssetDatabase.DeleteAsset(Target);
        }

        static bool IsCharacter(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return go != null && go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
        }

        static void Copy(List<string> paths, string emptyMessage)
        {
            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("Kurye Tycoon", emptyMessage, "Tamam");
                return;
            }
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(Target)) AssetDatabase.CreateFolder("Assets/Resources", "KuryeKarakterleri");

            int n = 0;
            foreach (var src in paths)
            {
                string dst = Target + "/" + Path.GetFileName(src);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(dst) != null) continue;
                if (AssetDatabase.CopyAsset(src, dst)) n++;
            }
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Kurye Tycoon",
                $"{n} karakter eklendi ({paths.Count} bulundu).\nPlay'e bas: kuryeler artık bu karakterlerle görünecek.\n\n" +
                "Karakterler pembe görünürse: Window > Rendering > Render Pipeline Converter > Material Upgrade.\n" +
                "Siyah görünürse: oyunda Ayarlar > Kurye karakterleri > Düz renk.", "Tamam");
        }
    }

    /// <summary>
    /// Şehir paketi (ör. SimplePoly City): bina ve ağaç prefablarını isimlerine göre ayırıp
    /// Assets/Resources/SehirPaketi/Binalar ve /Agaclar klasörlerine kopyalar. Yol ve araçlar şimdilik kullanılmaz
    /// (Yollar klasörüne ayrılır). Yanlış ayrılanı Project penceresinde klasörler arasında sürükleyebilirsin.
    /// </summary>
    public static class SehirKurulum
    {
        const string Root = "Assets/Resources/SehirPaketi";

        static readonly string[] Skip = { "car", "truck", "bus", "taxi", "police_car", "policecar", "ambulance", "vehicle", "van", "bike",
                                          "people", "person", "character", "human", "man_", "woman", "camera", "light", "lamp", "hydrant",
                                          "bench", "trash", "bin", "cone", "barrier", "fence", "sign", "traffic", "pole", "wire", "stadium" };
        static readonly string[] Roads = { "road", "street", "cross", "intersection", "crossing", "sidewalk", "pavement", "curb",
                                           "ground", "floor", "terrain", "plane", "tile", "parking", "bridge", "rail" };
        static readonly string[] Trees = { "tree", "bush", "plant", "palm", "pine", "flower", "hedge", "shrub" };
        static readonly string[] Buildings = { "build", "house", "shop", "store", "office", "apartment", "flat", "tower", "skyscraper",
                                               "hotel", "hospital", "station", "restaurant", "cafe", "bank", "school", "home", "market",
                                               "mall", "factory", "warehouse", "garage", "church", "cinema", "gym", "library", "pharmacy",
                                               "residential", "commercial", "industrial", "block", "bld", "bldg" };

        [MenuItem("Kurye Tycoon/Şehir Paketini Kur (SimplePoly City)")]
        static void KurSimplePoly() => Kur(null, "simplepoly");

        [MenuItem("Kurye Tycoon/Seçili Klasördeki Şehir Paketini Kur")]
        static void KurSecili()
        {
            var folders = new List<string>();
            foreach (var o in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(o);
                if (AssetDatabase.IsValidFolder(path)) folders.Add(path);
            }
            if (folders.Count == 0)
            {
                EditorUtility.DisplayDialog("Kurye Tycoon", "Önce Project penceresinde şehir paketinin prefab klasörünü seç.", "Tamam");
                return;
            }
            Kur(folders.ToArray(), null);
        }

        [MenuItem("Kurye Tycoon/Şehir Paketini Kaldır")]
        static void Kaldir()
        {
            if (!AssetDatabase.IsValidFolder(Root)) return;
            if (!EditorUtility.DisplayDialog("Kurye Tycoon", "Resources/SehirPaketi silinsin mi? (Orijinal paket silinmez.)", "Sil", "Vazgeç")) return;
            AssetDatabase.DeleteAsset(Root);
        }

        static bool Has(string name, string[] keys)
        {
            foreach (var k in keys) if (name.Contains(k)) return true;
            return false;
        }

        static void Ensure(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }

        static void Kur(string[] folders, string pathKey)
        {
            var guids = folders != null ? AssetDatabase.FindAssets("t:Prefab", folders) : AssetDatabase.FindAssets("t:Prefab");
            var buildings = new List<string>();
            var trees = new List<string>();
            var roads = new List<string>();
            var unknown = new List<string>();

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(Root) || path.StartsWith("Assets/Resources/KuryeKarakterleri")) continue;
                if (pathKey != null && !path.ToLowerInvariant().Replace(" ", "").Contains(pathKey)) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || go.GetComponentInChildren<Renderer>(true) == null) continue;

                string n = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (Has(n, Trees)) trees.Add(path);
                else if (Has(n, Skip)) continue;
                else if (Has(n, Roads)) roads.Add(path);
                else if (Has(n, Buildings)) buildings.Add(path);
                else unknown.Add(path);
            }

            if (buildings.Count + trees.Count + roads.Count + unknown.Count == 0)
            {
                EditorUtility.DisplayDialog("Kurye Tycoon",
                    "Şehir paketinde prefab bulunamadı.\n\nPaketi Window > Package Manager > My Assets'ten indirip Import ettiğinden emin ol. " +
                    "Farklı isimli bir paketse, prefab klasörünü Project penceresinde seçip Kurye Tycoon > Seçili Klasördeki Şehir Paketini Kur'u kullan.", "Tamam");
                return;
            }

            // İsimden anlaşılamayanlar: bina gibi duruyorsa (yüksekliği tabanına yakın ya da fazla) binalara ekle
            foreach (var u in unknown)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(u);
                var r = go.GetComponentInChildren<MeshFilter>(true);
                if (r != null && r.sharedMesh != null)
                {
                    var size = r.sharedMesh.bounds.size;
                    if (size.y >= 0.5f * Mathf.Max(size.x, size.z)) buildings.Add(u);
                }
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            Ensure("Assets/Resources", "SehirPaketi");
            Ensure(Root, "Binalar");
            Ensure(Root, "Agaclar");
            Ensure(Root, "Yollar");

            int nb = CopyAll(buildings, Root + "/Binalar");
            int nt = CopyAll(trees, Root + "/Agaclar");
            int nr = CopyAll(roads, Root + "/Yollar");
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Kurye Tycoon",
                $"Şehir paketi kuruldu:\n• {nb} bina\n• {nt} ağaç\n• {nr} yol parçası (şimdilik kullanılmıyor)\n\n" +
                "Yanlış klasöre düşen olursa Assets/Resources/SehirPaketi altında sürükleyerek taşı (araba, insan vb. Binalar'dan silinebilir).\n\n" +
                "Binalar pembe görünürse: Window > Rendering > Render Pipeline Converter > Material Upgrade.\n" +
                "Yeni oyun başlatınca yakınlaştırdığında paketin binaları görünür.", "Tamam");
        }

        static int CopyAll(List<string> paths, string dstFolder)
        {
            int n = 0;
            foreach (var src in paths)
            {
                string dst = dstFolder + "/" + Path.GetFileName(src);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(dst) != null) continue;
                if (AssetDatabase.CopyAsset(src, dst)) n++;
            }
            return n;
        }
    }
}
