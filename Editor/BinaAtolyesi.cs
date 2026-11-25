using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// Bina Atölyesi (yalnız geliştirme aracı; oyuncu görmez): "birkaç blok koy, binaya dönüşsün".
    /// Scene görünümünde 5 × 5 hücrelik (14 m arsa) ızgaraya blok konur / silinir; her değişiklikte BinaAtolyesiKurucu
    /// komşuluğa göre pastel modüler parçaları seçip binayı anında kurar (tohumlu: aynı tasarım hep aynı bina).
    ///  • Sol tık: seçili katta blok koy (dolu hücreye tıklamak rengini / tarzını değiştirir) · Sağ tık ya da Shift + sol tık: sil
    ///  • Kat: penceredeki kaydırıcı ya da Scene'de PageUp / PageDown · Geri al: Ctrl + Z (Undo)
    ///  • "Prefab olarak kaydet": mesh (malzemeye göre alt-mesh) + _Hafif LOD + prefab
    ///    (Resources/SehirPaketi/Binalar/Bolge_&lt;bölge&gt;_atolye_&lt;ad&gt;): City3D bölge havuzuna adından alır. Tasarım da
    ///    (BinaTasarimi + JSON) Modeller/Atolye altına yazılır: sonradan açılıp düzenlenir.
    /// </summary>
    public class BinaAtolyesi : EditorWindow
    {
        const string AtolyeKlasor = "Assets/KuryeTycoon/Modeller/Atolye";
        const string PrefabKlasor = "Assets/KuryeTycoon/Resources/SehirPaketi/Binalar";
        const string OrnekKlasor = "Assets/KuryeTycoon/Editor/BinaAtolyesi/Ornekler";
        const float LodGecis = 0.08f;      // BlenderBinaKurulum ile aynı
        const int KatSayisi = 8;

        BinaTasarimi tasarim;
        int kat, tarz, renk, cati;
        GameObject onizleme;
        Mesh onizlemeMesh;
        int ucgen;
        Vector2Int fare = new Vector2Int(-1, -1);
        Vector2 kaydir;

        [MenuItem("Kurye Tycoon/Bina Atölyesi")]
        static void Ac()
        {
            var w = GetWindow<BinaAtolyesi>("Bina Atölyesi");
            w.minSize = new Vector2(300f, 420f);
            w.Show();
        }

        void OnEnable()
        {
            SceneView.duringSceneGui += SahneGui;
            Undo.undoRedoPerformed += Yenile;
            if (tasarim == null) YeniTasarim();
            Yenile();
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= SahneGui;
            Undo.undoRedoPerformed -= Yenile;
            if (onizleme != null) DestroyImmediate(onizleme);
            if (onizlemeMesh != null) DestroyImmediate(onizlemeMesh);
            onizleme = null;
            onizlemeMesh = null;
        }

        void YeniTasarim()
        {
            tasarim = CreateInstance<BinaTasarimi>();
            tasarim.name = "Yeni bina";
            tasarim.hideFlags = HideFlags.DontSaveInEditor;
        }

        // ---------------------------------------------------------------- önizleme

        void Yenile()
        {
            if (tasarim == null) return;
            if (onizleme == null)
            {
                onizleme = new GameObject("Bina Atölyesi (önizleme)") { hideFlags = HideFlags.DontSave };
                onizleme.AddComponent<MeshFilter>();
                onizleme.AddComponent<MeshRenderer>();
            }
            var mesh = BinaAtolyesiKurucu.Kur(tasarim, false, out var adlar);
            var mf = onizleme.GetComponent<MeshFilter>();
            var mr = onizleme.GetComponent<MeshRenderer>();
            if (onizlemeMesh != null) DestroyImmediate(onizlemeMesh);
            onizlemeMesh = mesh;
            ucgen = 0;
            if (mesh != null)
            {
                mesh.hideFlags = HideFlags.DontSave;
                mf.sharedMesh = mesh;
                mr.sharedMaterials = BinaAtolyesiKurucu.Malzemeler(adlar);
                for (int i = 0; i < mesh.subMeshCount; i++) ucgen += (int)(mesh.GetIndexCount(i) / 3);
            }
            else mf.sharedMesh = null;
            SceneView.RepaintAll();
            Repaint();
        }

        int BlokBul(int x, int y, int k)
        {
            for (int i = 0; i < tasarim.bloklar.Count; i++)
            {
                var b = tasarim.bloklar[i];
                if (b.x == x && b.y == y && b.k == k) return i;
            }
            return -1;
        }

        void Koy(int x, int y, int k)
        {
            Undo.RecordObject(tasarim, "Blok koy");
            var b = new BinaTasarimi.Blok { x = x, y = y, k = k, tarz = tarz, renk = renk, cati = cati };
            int i = BlokBul(x, y, k);
            if (i >= 0) tasarim.bloklar[i] = b; else tasarim.bloklar.Add(b);
            EditorUtility.SetDirty(tasarim);
            Yenile();
        }

        void Sil(int x, int y, int k)
        {
            int i = BlokBul(x, y, k);
            if (i < 0) return;
            Undo.RecordObject(tasarim, "Blok sil");
            tasarim.bloklar.RemoveAt(i);
            EditorUtility.SetDirty(tasarim);
            Yenile();
        }

        // ---------------------------------------------------------------- Scene görünümü: ızgara, fare

        /// <summary>Hücre (x, y) ve kat → Unity uzayında hücrenin alt ortası (önizleme ile aynı eksen: Y yukarı, ön +Z).</summary>
        static Vector3 HucreYeri(int x, int y, int k)
        {
            float h = BinaAtolyesiKurucu.Hucre;
            int n = BinaAtolyesiKurucu.Izgara;
            float mx = (x - (n - 1) * 0.5f) * h, my = (y - (n - 1) * 0.5f) * h;
            return new Vector3(-mx, k * BinaAtolyesiKurucu.KatBoyu, -my);
        }

        void SahneGui(SceneView sv)
        {
            if (tasarim == null) return;
            float h = BinaAtolyesiKurucu.Hucre, kb = BinaAtolyesiKurucu.KatBoyu;
            int n = BinaAtolyesiKurucu.Izgara;
            float yari = n * h * 0.5f, yuk = kat * kb;
            var e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);

            // Fare hangi hücrede (seçili katın taban düzleminde)
            var isin = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var hucre = new Vector2Int(-1, -1);
            if (Mathf.Abs(isin.direction.y) > 1e-5f)
            {
                float tt = (yuk - isin.origin.y) / isin.direction.y;
                if (tt > 0f)
                {
                    Vector3 p = isin.origin + isin.direction * tt;
                    int cx = Mathf.FloorToInt(-p.x / h + n * 0.5f), cy = Mathf.FloorToInt(-p.z / h + n * 0.5f);
                    if (cx >= 0 && cy >= 0 && cx < n && cy < n) hucre = new Vector2Int(cx, cy);
                }
            }

            switch (e.type)
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(id);
                    break;
                case EventType.MouseMove:
                    if (hucre != fare) { fare = hucre; sv.Repaint(); }
                    break;
                case EventType.MouseDown:
                    if (e.alt || hucre.x < 0) break;       // Alt: kamerayı döndürme
                    if (e.button == 0 && !e.shift) { Koy(hucre.x, hucre.y, kat); e.Use(); }
                    else if (e.button == 1 || (e.button == 0 && e.shift)) { Sil(hucre.x, hucre.y, kat); e.Use(); }
                    break;
                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.PageUp) { kat = Mathf.Min(KatSayisi - 1, kat + 1); e.Use(); Repaint(); sv.Repaint(); }
                    else if (e.keyCode == KeyCode.PageDown) { kat = Mathf.Max(0, kat - 1); e.Use(); Repaint(); sv.Repaint(); }
                    break;
                case EventType.Repaint:
                    // Izgara (seçili katın tabanı) ve arsa sınırı; sokak tarafı (ön, +Z) kalın çizgi
                    Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                    Handles.color = new Color(0.25f, 0.2f, 0.3f, 0.55f);
                    for (int i = 0; i <= n; i++)
                    {
                        float a = -yari + i * h;
                        Handles.DrawLine(new Vector3(a, yuk, -yari), new Vector3(a, yuk, yari));
                        Handles.DrawLine(new Vector3(-yari, yuk, a), new Vector3(yari, yuk, a));
                    }
                    Handles.color = new Color(0.85f, 0.35f, 0.3f, 0.9f);
                    Handles.DrawLine(new Vector3(-yari, 0f, yari), new Vector3(yari, 0f, yari), 3f);
                    Handles.Label(new Vector3(0f, 0f, yari + 0.8f), "sokak (ön)");
                    if (fare.x >= 0)
                    {
                        bool dolu = BlokBul(fare.x, fare.y, kat) >= 0;
                        Handles.color = dolu ? new Color(1f, 0.75f, 0.2f, 0.95f) : new Color(0.3f, 0.75f, 0.45f, 0.95f);
                        Handles.DrawWireCube(HucreYeri(fare.x, fare.y, kat) + new Vector3(0f, kb * 0.5f, 0f), new Vector3(h, kb, h));
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- pencere

        void OnGUI()
        {
            kaydir = EditorGUILayout.BeginScrollView(kaydir);
            if (BinaAtolyesiKurucu.Kut == null)
            {
                EditorGUILayout.HelpBox("Parça kütüphanesi okunamadı: " + BinaAtolyesiKurucu.KutuphaneYol +
                                        "\nRepo kökünde: python3 Tools/Blender/pastel_parcalar.py --json", MessageType.Error);
                if (GUILayout.Button("Yeniden oku")) { BinaAtolyesiKurucu.Yukle(); Yenile(); }
                EditorGUILayout.EndScrollView();
                return;
            }
            var kt = BinaAtolyesiKurucu.Kut;

            EditorGUILayout.LabelField("Tasarım", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var yeni = (BinaTasarimi)EditorGUILayout.ObjectField("Dosya", EditorUtility.IsPersistent(tasarim) ? tasarim : null, typeof(BinaTasarimi), false);
            if (EditorGUI.EndChangeCheck() && yeni != null) { tasarim = yeni; Yenile(); }
            EditorGUI.BeginChangeCheck();
            string ad = EditorGUILayout.TextField("Ad", tasarim.ad);
            int bolge = EditorGUILayout.Popup("Bölge (havuz)", Mathf.Max(0, System.Array.IndexOf(BinaAtolyesiKurucu.Bolgeler, tasarim.bolge)), BinaAtolyesiKurucu.Bolgeler);
            int tohum = EditorGUILayout.IntField("Tohum (varyant)", tasarim.tohum);
            bool dukkan = EditorGUILayout.Toggle("Zemin kat dükkân", tasarim.dukkan);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(tasarim, "Bina ayarı");
                tasarim.ad = Temiz(ad);
                tasarim.bolge = BinaAtolyesiKurucu.Bolgeler[bolge];
                tasarim.tohum = tohum;
                tasarim.dukkan = dukkan;
                EditorUtility.SetDirty(tasarim);
                Yenile();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Fırça", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            kat = EditorGUILayout.IntSlider("Kat (PageUp / PageDown)", kat, 0, KatSayisi - 1);
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
            tarz = EditorGUILayout.Popup("Tarz", tarz, BinaAtolyesiKurucu.TarzAd);
            renk = RenkSec("Duvar rengi", kt.duvarRenk, renk);
            cati = RenkSec("Çatı rengi", kt.catiRenk, cati);
            EditorGUILayout.HelpBox("Scene görünümünde: sol tık blok koyar (dolu hücrede rengini / tarzını değiştirir), sağ tık ya da Shift + sol tık siler. " +
                                    "Kırmızı çizgi sokak tarafı. Tek hücrelik yüksek yığın kule tepesi alır; altı boş blok kemerli geçit olur; " +
                                    "taş merdiven bitişiğindeki bloğa doğru çıkar.", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"{tasarim.bloklar.Count} blok · {ucgen} üçgen", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Yeni")) { YeniTasarim(); Yenile(); }
                if (GUILayout.Button("Temizle") && tasarim.bloklar.Count > 0)
                {
                    Undo.RecordObject(tasarim, "Blokları temizle");
                    tasarim.bloklar.Clear();
                    EditorUtility.SetDirty(tasarim);
                    Yenile();
                }
                if (GUILayout.Button("Parçaları yeniden oku")) { BinaAtolyesiKurucu.Yukle(); Yenile(); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("JSON içe aktar")) IceAktar();
                if (GUILayout.Button("JSON dışa aktar")) DisaAktar();
            }
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(tasarim.bloklar.Count == 0))
            {
                if (GUILayout.Button("Prefab olarak kaydet", GUILayout.Height(30f)))
                {
                    string yol = Kaydet(tasarim, true);
                    if (yol != null) EditorUtility.DisplayDialog("Bina Atölyesi", "Kaydedildi:\n" + yol + "\n\nYeni oyun başlatınca '" + tasarim.bolge + "' bölgesinde görünür.", "Tamam");
                }
            }
            if (GUILayout.Button("Örnek binaları üret (Editor/BinaAtolyesi/Ornekler)")) OrnekleriUret();
            EditorGUILayout.EndScrollView();
        }

        static string Temiz(string s)
        {
            if (string.IsNullOrEmpty(s)) return "bina";
            var sb = new System.Text.StringBuilder();
            foreach (char c in s.Trim().ToLowerInvariant())
                sb.Append((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ? c : '_');
            return sb.Length == 0 ? "bina" : sb.ToString();
        }

        static int RenkSec(string baslik, string[] renkler, int secili)
        {
            EditorGUILayout.LabelField(baslik);
            using (new EditorGUILayout.HorizontalScope())
            {
                var eski = GUI.backgroundColor;
                for (int i = 0; i < renkler.Length; i++)
                {
                    GUI.backgroundColor = BinaAtolyesiKurucu.Renk(renkler[i]);
                    if (GUILayout.Button(i == secili ? "●" : "", GUILayout.Width(24f), GUILayout.Height(22f))) secili = i;
                }
                GUI.backgroundColor = eski;
            }
            return Mathf.Clamp(secili, 0, renkler.Length - 1);
        }

        // ---------------------------------------------------------------- içe / dışa aktarma, kaydetme

        void IceAktar()
        {
            string yol = EditorUtility.OpenFilePanel("Tasarım JSON'u", Application.dataPath + "/KuryeTycoon/Editor/BinaAtolyesi/Ornekler", "json");
            if (string.IsNullOrEmpty(yol)) return;
            YeniTasarim();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(yol), tasarim);
            if (tasarim.bloklar == null) tasarim.bloklar = new List<BinaTasarimi.Blok>();
            tasarim.ad = Temiz(tasarim.ad);
            Yenile();
        }

        void DisaAktar()
        {
            string yol = EditorUtility.SaveFilePanel("Tasarım JSON'u", Application.dataPath, "atolye_" + tasarim.ad, "json");
            if (string.IsNullOrEmpty(yol)) return;
            File.WriteAllText(yol, JsonUtility.ToJson(tasarim, true));
            AssetDatabase.Refresh();
        }

        static void MeshYaz(Mesh mesh, string yol)
        {
            var eski = AssetDatabase.LoadAssetAtPath<Mesh>(yol);
            if (eski != null)
            {
                // Var olan dosyanın üstüne yaz: prefabın mesh bağlantısı kopmasın
                EditorUtility.CopySerialized(mesh, eski);
                EditorUtility.SetDirty(eski);
                DestroyImmediate(mesh);
            }
            else AssetDatabase.CreateAsset(mesh, yol);
        }

        /// <summary>Mesh + _Hafif LOD + prefab + tasarım dosyası. Prefabın yolunu döndürür (kurulamazsa null).</summary>
        static string Kaydet(BinaTasarimi t, bool tasarimiYaz)
        {
            var mesh = BinaAtolyesiKurucu.Kur(t, false, out var adlar);
            var hafif = BinaAtolyesiKurucu.Kur(t, true, out var hafifAdlar);
            if (mesh == null) return null;
            string bolge = System.Array.IndexOf(BinaAtolyesiKurucu.Bolgeler, t.bolge) >= 0 ? t.bolge : "eski";
            string ad = "Bolge_" + bolge + "_atolye_" + Temiz(t.ad);
            BinaAtolyesiKurucu.KlasorAc(AtolyeKlasor);
            BinaAtolyesiKurucu.KlasorAc(PrefabKlasor);
            string meshYol = AtolyeKlasor + "/" + ad + ".asset", hafifYol = AtolyeKlasor + "/" + ad + "_Hafif.asset";
            mesh.name = ad;
            MeshYaz(mesh, meshYol);
            if (hafif != null) { hafif.name = ad + "_Hafif"; MeshYaz(hafif, hafifYol); }
            AssetDatabase.SaveAssets();
            var meshA = AssetDatabase.LoadAssetAtPath<Mesh>(meshYol);
            var hafifA = hafif != null ? AssetDatabase.LoadAssetAtPath<Mesh>(hafifYol) : null;

            var kok = new GameObject(ad);
            var l0 = Parca(kok.transform, "LOD0", meshA, BinaAtolyesiKurucu.Malzemeler(adlar));
            if (hafifA != null)
            {
                var l1 = Parca(kok.transform, "LOD1", hafifA, BinaAtolyesiKurucu.Malzemeler(hafifAdlar));
                var grup = kok.AddComponent<LODGroup>();
                grup.SetLODs(new[] { new LOD(LodGecis, new Renderer[] { l0 }), new LOD(0.0005f, new Renderer[] { l1 }) });
                grup.RecalculateBounds();
            }
            string prefabYol = PrefabKlasor + "/" + ad + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(kok, prefabYol);
            DestroyImmediate(kok);

            if (tasarimiYaz)
            {
                // Tasarım: düzenlenebilir dosya (BinaTasarimi) + taşınabilir JSON
                string tasarimYol = AtolyeKlasor + "/" + ad + "_tasarim.asset";
                if (!EditorUtility.IsPersistent(t))
                {
                    var eski = AssetDatabase.LoadAssetAtPath<BinaTasarimi>(tasarimYol);
                    if (eski != null) { EditorUtility.CopySerialized(t, eski); EditorUtility.SetDirty(eski); }
                    else
                    {
                        t.hideFlags = HideFlags.None;
                        AssetDatabase.CreateAsset(t, tasarimYol);
                    }
                }
                File.WriteAllText(Path.Combine(Application.dataPath, "KuryeTycoon/Modeller/Atolye/" + ad + "_tasarim.json"), JsonUtility.ToJson(t, true));
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return prefabYol;
        }

        static MeshRenderer Parca(Transform ust, string ad, Mesh mesh, Material[] malzemeler)
        {
            var go = new GameObject(ad);
            go.transform.SetParent(ust, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = malzemeler;
            return mr;
        }

        /// <summary>Editor/BinaAtolyesi/Ornekler altındaki bütün tasarım JSON'larından prefab üretir.</summary>
        void OrnekleriUret()
        {
            string klasor = Path.Combine(Application.dataPath, "KuryeTycoon/Editor/BinaAtolyesi/Ornekler");
            if (!Directory.Exists(klasor)) { EditorUtility.DisplayDialog("Bina Atölyesi", "Örnek klasörü yok: " + OrnekKlasor, "Tamam"); return; }
            int say = 0;
            foreach (string yol in Directory.GetFiles(klasor, "*.json"))
            {
                var t = CreateInstance<BinaTasarimi>();
                JsonUtility.FromJsonOverwrite(File.ReadAllText(yol), t);
                if (t.bloklar != null && t.bloklar.Count > 0 && Kaydet(t, true) != null) say++;
                if (!EditorUtility.IsPersistent(t)) DestroyImmediate(t);
            }
            EditorUtility.DisplayDialog("Bina Atölyesi", say + " örnek bina prefab olarak kaydedildi (" + PrefabKlasor + ").", "Tamam");
        }
    }
}
