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
}
