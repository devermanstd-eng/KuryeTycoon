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
            MalzemeCevirici.CevirPrefablar(paths);
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
            string animInfo = KarakterAnimasyon.Kur(false);
            EditorUtility.DisplayDialog("Kurye Tycoon",
                animInfo + "\n\n" +
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

        static readonly string[] Vehicles = { "car", "truck", "bus", "taxi", "police", "ambulance", "vehicle", "van", "pickup", "suv",
                                              "sedan", "jeep", "firetruck", "fire_truck", "garbage", "tractor", "lorry" };
        static readonly string[] People = { "people", "person", "character", "human", "man_", "woman", "citizen", "pedestrian",
                                            "male", "female", "girl", "boy", "worker", "npc" };
        static readonly string[] Skip = { "bike", "camera", "light", "lamp", "hydrant", "carpet", "card", "cart",
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

        [MenuItem("Kurye Tycoon/Seçili Klasörü Araç Olarak Ekle")]
        static void AracEkle() => ElleEkle("Araclar", "araç");

        [MenuItem("Kurye Tycoon/Seçili Klasörü İnsan (Yaya) Olarak Ekle")]
        static void InsanEkle() => ElleEkle("Insanlar", "insan");

        /// <summary>Project penceresinde seçili klasör(ler)deki ya da seçili prefablardaki modelleri trafiğe ekler.</summary>
        static void ElleEkle(string sub, string what)
        {
            var paths = new List<string>();
            foreach (var o in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(o);
                if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { path })) paths.Add(AssetDatabase.GUIDToAssetPath(g));
                    foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { path })) paths.Add(AssetDatabase.GUIDToAssetPath(g));
                }
                else if (path.EndsWith(".prefab") || path.EndsWith(".fbx") || path.EndsWith(".FBX") || path.EndsWith(".obj")) paths.Add(path);
            }
            paths.RemoveAll(p => p.StartsWith(Root));
            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("Kurye Tycoon", $"Önce Project penceresinde {what} modellerinin olduğu klasörü (ya da prefabları) seç.", "Tamam");
                return;
            }
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            Ensure("Assets/Resources", "SehirPaketi");
            Ensure(Root, sub);
            var prefabs = paths.FindAll(p => p.EndsWith(".prefab"));
            MalzemeCevirici.CevirPrefablar(prefabs);
            int n = CopyAll(paths, Root + "/" + sub);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Kurye Tycoon", $"{n} {what} modeli eklendi (Assets/Resources/SehirPaketi/{sub}).\nYeni oyun başlatınca trafikte görünür.", "Tamam");
        }

        [MenuItem("Kurye Tycoon/Şehir Paketini Kaldır")]
        static void Kaldir()
        {
            if (!AssetDatabase.IsValidFolder(Root)) return;
            if (!EditorUtility.DisplayDialog("Kurye Tycoon", "Resources/SehirPaketi silinsin mi? (Orijinal paket silinmez.)", "Sil", "Vazgeç")) return;
            AssetDatabase.DeleteAsset(Root);
        }

        static readonly string[] VehicleFolders = { "vehicle", "vehicles", "cars", "car", "transport", "traffic" };
        static readonly string[] PeopleFolders = { "people", "characters", "character", "humans", "human", "pedestrians", "citizens", "npc" };

        /// <summary>Yoldaki klasörlerden biri tam olarak bu adlardan biri mi (ör. ".../Prefabs/Vehicles/Car_01.prefab")?</summary>
        static bool FolderHas(string dir, string[] names)
        {
            foreach (var part in dir.Split('/'))
                foreach (var nm in names)
                    if (part == nm || part.Replace(" ", "").Replace("_", "") == nm) return true;
            return false;
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
            var vehicles = new List<string>();
            var people = new List<string>();
            var unknown = new List<string>();
            var skipped = new List<string>();

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(Root) || path.StartsWith("Assets/Resources/KuryeKarakterleri")) continue;
                if (pathKey != null && !path.ToLowerInvariant().Replace(" ", "").Contains(pathKey)) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || go.GetComponentInChildren<Renderer>(true) == null) continue;

                string n = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                // Paketlerin çoğu modelleri klasörlere ayırır (ör. Prefabs/Vehicles, Prefabs/Characters): klasör adına da bak
                string dir = (Path.GetDirectoryName(path) ?? "").Replace('\\', '/').ToLowerInvariant();
                bool inVehicleFolder = FolderHas(dir, VehicleFolders);
                bool inPeopleFolder = FolderHas(dir, PeopleFolders);
                if (inPeopleFolder) people.Add(path);
                else if (inVehicleFolder) vehicles.Add(path);
                else if (Has(n, Trees)) trees.Add(path);
                else if (Has(n, Skip)) { skipped.Add(n); continue; }
                // "Bus_Station", "Police_Station", "Car_Parking" gibi binalar / yollar araç sayılmasın
                else if (Has(n, People) && !Has(n, Buildings)) people.Add(path);
                else if (Has(n, Vehicles) && !Has(n, Buildings) && !Has(n, Roads) && !n.Contains("wash")) vehicles.Add(path);
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
            Ensure(Root, "Araclar");
            Ensure(Root, "Insanlar");

            var all = new List<string>(buildings); all.AddRange(trees); all.AddRange(roads); all.AddRange(vehicles); all.AddRange(people);
            int conv = MalzemeCevirici.CevirPrefablar(all);
            int nb = CopyAll(buildings, Root + "/Binalar");
            int nt = CopyAll(trees, Root + "/Agaclar");
            int nr = CopyAll(roads, Root + "/Yollar");
            // Konsola rapor: hangi model nereye gitti (araç / insan bulunamazsa adlardan anlaşılır)
            var sb = new System.Text.StringBuilder("Kurye Tycoon şehir paketi raporu\n");
            sb.AppendLine($"ARAÇLAR ({vehicles.Count}): " + string.Join(", ", vehicles.ConvertAll(Path.GetFileNameWithoutExtension)));
            sb.AppendLine($"İNSANLAR ({people.Count}): " + string.Join(", ", people.ConvertAll(Path.GetFileNameWithoutExtension)));
            sb.AppendLine($"ATLANANLAR ({skipped.Count}): " + string.Join(", ", skipped));
            var unk = new List<string>();
            foreach (var u in unknown) if (!buildings.Contains(u)) unk.Add(Path.GetFileNameWithoutExtension(u));
            sb.AppendLine($"TANINMAYANLAR ({unk.Count}): " + string.Join(", ", unk));
            Debug.Log(sb.ToString());
            int nv = CopyAll(vehicles, Root + "/Araclar");
            int np = CopyAll(people, Root + "/Insanlar");
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Kurye Tycoon",
                $"Şehir paketi kuruldu:\n• {conv} malzeme URP'ye çevrildi\n• {nb} bina\n• {nt} ağaç\n• {nr} yol parçası\n• {nv} araç (trafik)\n• {np} insan (yayalar)\n\n" +
                (nv + np == 0 ? "Araç / insan bulunamadı: Console'daki 'şehir paketi raporu'na bak ya da paketin araç klasörünü seçip " +
                                "Kurye Tycoon > Seçili Klasörü Araç Olarak Ekle'yi kullan.\n\n" : "") +
                "Yanlış klasöre düşen olursa Assets/Resources/SehirPaketi altında sürükleyerek taşı (araba, insan vb. Binalar'dan silinebilir).\n\n" +
                "Hâlâ pembe görünen olursa: paketin klasörünü seç > Kurye Tycoon > Seçili Klasördeki Malzemeleri URP'ye Çevir.\n" +
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

    /// <summary>
    /// Eski (Built-in, Unity 5) paketlerin malzemelerini URP Lit'e çevirir. Render Pipeline Converter
    /// çalışmazsa diye: doku ve rengi malzemenin kayıtlı özelliklerinden okur (shader pembe olsa bile).
    /// </summary>
    public static class MalzemeCevirici
    {
        [MenuItem("Kurye Tycoon/Seçili Klasördeki Malzemeleri URP'ye Çevir")]
        static void CevirSecili()
        {
            var folders = new List<string>();
            foreach (var o in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(o);
                if (AssetDatabase.IsValidFolder(path)) folders.Add(path);
            }
            if (folders.Count == 0)
            {
                EditorUtility.DisplayDialog("Kurye Tycoon", "Önce Project penceresinde paketin klasörünü seç (ör. SimplePoly City).", "Tamam");
                return;
            }
            var mats = new List<string>();
            foreach (var g in AssetDatabase.FindAssets("t:Material", folders.ToArray())) mats.Add(AssetDatabase.GUIDToAssetPath(g));
            int n = Cevir(mats);
            EditorUtility.DisplayDialog("Kurye Tycoon", $"{n} malzeme URP'ye çevrildi ({mats.Count} malzeme bulundu).", "Tamam");
        }

        /// <summary>Prefabların kullandığı malzemeleri çevirir.</summary>
        public static int CevirPrefablar(IEnumerable<string> prefabPaths)
        {
            var mats = new List<string>();
            foreach (var dep in AssetDatabase.GetDependencies(new List<string>(prefabPaths).ToArray(), true))
                if (dep.EndsWith(".mat") && dep.StartsWith("Assets/")) mats.Add(dep);
            return Cevir(mats);
        }

        static int Cevir(List<string> matPaths)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return 0;
            int n = 0;
            foreach (var path in matPaths)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.shader == null) continue;
                string sh = m.shader.name;
                if (sh.StartsWith("Universal Render Pipeline") || sh.StartsWith("Shader Graphs") || sh.StartsWith("Sprites")) continue;

                // Kayıtlı özelliklerden doku ve renk (shader artık bu özellikleri tanımasa da dosyada durur)
                Texture tex = null;
                Color col = Color.white;
                bool hasCol = false;
                var so = new SerializedObject(m);
                var texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
                if (texEnvs != null)
                    for (int i = 0; i < texEnvs.arraySize; i++)
                    {
                        var e = texEnvs.GetArrayElementAtIndex(i);
                        string name = e.FindPropertyRelative("first").stringValue;
                        if (name == "_MainTex" || name == "_BaseMap")
                        {
                            var t = e.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                            if (t != null) { tex = t; break; }
                        }
                    }
                var colors = so.FindProperty("m_SavedProperties.m_Colors");
                if (colors != null)
                    for (int i = 0; i < colors.arraySize; i++)
                    {
                        var e = colors.GetArrayElementAtIndex(i);
                        string name = e.FindPropertyRelative("first").stringValue;
                        if (name == "_Color" || name == "_BaseColor") { col = e.FindPropertyRelative("second").colorValue; hasCol = true; break; }
                    }

                m.shader = lit;
                if (tex != null) m.SetTexture("_BaseMap", tex);
                if (hasCol) m.SetColor("_BaseColor", col);
                m.SetFloat("_Smoothness", 0.2f);
                EditorUtility.SetDirty(m);
                n++;
            }
            AssetDatabase.SaveAssets();
            return n;
        }
    }

    /// <summary>
    /// Karakter paketindeki yürüme (tercihen "basic walk") ve bekleme (idle) animasyonlarını bulur,
    /// döngüye alır ve Resources/KuryeKarakterleri/KuryeAnimator adlı bir Animator Controller kurar.
    /// Oyun bu controller'ı kuryelere takar: yürürken walk, dururken idle.
    /// </summary>
    public static class KarakterAnimasyon
    {
        const string ControllerPath = "Assets/Resources/KuryeKarakterleri/KuryeAnimator.controller";

        [MenuItem("Kurye Tycoon/Karakter Animasyonlarını Kur (yürüme)")]
        static void KurMenu()
        {
            EditorUtility.DisplayDialog("Kurye Tycoon", Kur(true), "Tamam");
        }

        struct ClipRef { public AnimationClip clip; public string path; }

        public static string Kur(bool fromMenu)
        {
            var clips = new List<ClipRef>();
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.ToLowerInvariant().Replace(" ", "").Contains("citypeople")) continue;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is AnimationClip c && !c.name.StartsWith("__preview__")) clips.Add(new ClipRef { clip = c, path = path });
            }
            // Seçili klasör de taranır (paket farklı isimdeyse)
            foreach (var sel in Selection.objects)
            {
                string sp = AssetDatabase.GetAssetPath(sel);
                if (!AssetDatabase.IsValidFolder(sp)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { sp }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                        if (o is AnimationClip c && !c.name.StartsWith("__preview__")) clips.Add(new ClipRef { clip = c, path = path });
                }
            }

            var walk = Best(clips, new[] { "basic walk", "basic_walk", "basicwalk", "walk_basic", "walking", "walk" }, new[] { "run", "back", "left", "right", "strafe", "jog" });
            var idle = Best(clips, new[] { "idle", "stand", "breath" }, new[] { "walk", "run" });
            if (walk.clip == null)
                return "Karakter animasyonları: paket içinde yürüme (walk) animasyonu bulunamadı. Karakterler yürürken kayarak hareket eder.\n" +
                       "Animasyonlar farklı bir klasördeyse o klasörü seçip Kurye Tycoon > Karakter Animasyonlarını Kur'u kullan.";

            MakeLoop(walk);
            if (idle.clip != null) MakeLoop(idle);
            // Döngü ayarı yeniden içe aktarma yaptığı için klipleri tekrar yükle
            walk.clip = Reload(walk);
            if (idle.clip != null) idle.clip = Reload(idle);

            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/KuryeKarakterleri")) AssetDatabase.CreateFolder("Assets/Resources", "KuryeKarakterleri");
            if (AssetDatabase.LoadAssetAtPath<Object>(ControllerPath) != null) AssetDatabase.DeleteAsset(ControllerPath);

            var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.AddParameter("walking", AnimatorControllerParameterType.Bool);
            var sm = ctrl.layers[0].stateMachine;
            var idleState = sm.AddState("Idle");
            idleState.motion = idle.clip != null ? idle.clip : walk.clip;
            if (idle.clip == null) idleState.speed = 0f; // idle yoksa yürüme pozunda durur
            var walkState = sm.AddState("Walk");
            walkState.motion = walk.clip;
            sm.defaultState = idleState;
            var t1 = idleState.AddTransition(walkState);
            t1.hasExitTime = false; t1.duration = 0.1f;
            t1.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "walking");
            var t2 = walkState.AddTransition(idleState);
            t2.hasExitTime = false; t2.duration = 0.15f;
            t2.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0f, "walking");
            AssetDatabase.SaveAssets();

            return $"Karakter animasyonları kuruldu: yürüme = \"{walk.clip.name}\"" +
                   (idle.clip != null ? $", bekleme = \"{idle.clip.name}\"" : " (bekleme animasyonu yok)") + ".";
        }

        static ClipRef Best(List<ClipRef> clips, string[] want, string[] avoid)
        {
            foreach (var w in want)
                foreach (var c in clips)
                {
                    string n = c.clip.name.ToLowerInvariant();
                    if (!n.Contains(w)) continue;
                    bool bad = false;
                    foreach (var a in avoid) if (n.Contains(a)) bad = true;
                    if (!bad) return c;
                }
            return default;
        }

        static void MakeLoop(ClipRef r)
        {
            var mi = AssetImporter.GetAtPath(r.path) as ModelImporter;
            if (mi != null)
            {
                var list = mi.clipAnimations;
                if (list == null || list.Length == 0) list = mi.defaultClipAnimations;
                bool changed = false;
                for (int i = 0; i < list.Length; i++)
                    if (list[i].name == r.clip.name && !list[i].loopTime) { list[i].loopTime = true; list[i].loopPose = true; changed = true; }
                if (changed) { mi.clipAnimations = list; mi.SaveAndReimport(); }
                return;
            }
            var settings = AnimationUtility.GetAnimationClipSettings(r.clip);
            if (!settings.loopTime)
            {
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(r.clip, settings);
                EditorUtility.SetDirty(r.clip);
            }
        }

        static AnimationClip Reload(ClipRef r)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(r.path))
                if (o is AnimationClip c && c.name == r.clip.name) return c;
            return r.clip;
        }
    }
}
