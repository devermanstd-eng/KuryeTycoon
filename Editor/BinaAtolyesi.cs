using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// Bina Atölyesi (yalnız geliştirme aracı; oyuncu görmez): "birkaç blok koy, binaya dönüşsün" + çevre atölyesi.
    /// Scene görünümünde 5 × 5 hücrelik (14 m arsa) ızgara. Her değişiklikte BinaAtolyesiKurucu komşuluğa ve kullanım türüne
    /// göre pastel parçaları seçip modeli anında kurar (tohumlu: aynı tasarım hep aynı model).
    ///  • Blok fırçası: sol tık blok koy (dolu hücrede rengini / tarzını değiştirir) · sağ tık ya da Shift + sol tık sil ·
    ///    PageUp / PageDown kat.
    ///  • Süs fırçası: seçili dekor / ağacı ızgarasız, tıklanan yere koyar · Shift + tık en yakını siler · [ ve ] döndürür.
    ///  • Dağıt fırçası: seçili parçadan yarıçap içine yoğunluk kadar serper (çiçek, ot, çalı).
    ///  • Kullanım türü (dükkân, market, kamu, sanayi, simge, konut): zemin kata türün vitrini, kapısı, tentesi, tabelası
    ///    (yazı + simge) ve ön dekorları gelir; prefab adı türe göre City3D'nin doğru havuzuna girer.
    ///  • "Prefab olarak kaydet": mesh (malzemeye göre alt-mesh + kalem çizgileri) + _Hafif LOD + prefab
    ///    (Resources/SehirPaketi/Binalar/&lt;önek&gt;_atolye_&lt;ad&gt;). Tasarım (BinaTasarimi + JSON) Modeller/Atolye altına yazılır.
    ///  • Çevre modeli: seçili ağaç / dekor tek başına prefab olur (Agaclar ya da Donatilar; City3D ve SokakDonatilari okur).
    /// Geri al: Ctrl + Z.
    /// </summary>
    public class BinaAtolyesi : EditorWindow
    {
        const string AtolyeKlasor = "Assets/KuryeTycoon/Modeller/Atolye";
        const string PrefabKlasor = "Assets/KuryeTycoon/Resources/SehirPaketi/Binalar";
        const string AgacKlasor = "Assets/KuryeTycoon/Resources/SehirPaketi/Agaclar";
        const string DonatiKlasor = "Assets/KuryeTycoon/Resources/SehirPaketi/Donatilar";
        const string OrnekKlasor = "Assets/KuryeTycoon/Editor/BinaAtolyesi/Ornekler";
        const float LodGecis = 0.08f;      // BlenderBinaKurulum ile aynı
        const int KatSayisi = 8;
        const int ModBlok = 0, ModSus = 1, ModDagit = 2;
        static readonly string[] ModAd = { "Blok", "Süs (tek tek)", "Dağıt (serp)" };

        BinaTasarimi tasarim;
        int mod, kat, tarz, renk, cati, catiTarzi;
        // süs fırçası
        int susSira, susRenk;
        float susAci, susOlcek = 1f, susOynama = 0.15f, dagitYaricap = 1.5f;
        int dagitAdet = 6;
        string[] susAd, susEtiket;
        string[] turAd, turEtiket;

        GameObject onizleme, onizlemeIsigi;
        Mesh onizlemeMesh;
        int ucgen;
        Vector2Int fare = new Vector2Int(-1, -1);
        Vector3 fareYer;
        bool fareArsada;
        Vector2 kaydir;
        bool acTasarim = true, acKullanim = true, acFirca = true, acCevre, acKaydet = true;

        [MenuItem("Kurye Tycoon/Bina Atölyesi")]
        static void Ac()
        {
            var w = GetWindow<BinaAtolyesi>("Bina Atölyesi");
            w.minSize = new Vector2(320f, 460f);
            w.Show();
        }

        void OnEnable()
        {
            SceneView.duringSceneGui += SahneGui;
            Undo.undoRedoPerformed += Yenile;
            // Oyun, ekran geçişi konturu çizerken kalem çizgilerini gizleyebilir: atölyede hep görünsün
            if (!Application.isPlaying) Shader.SetGlobalFloat("_PastelCizgiGizle", 0f);
            if (tasarim == null) YeniTasarim();
            ListeleriKur();
            Yenile();
            EditorApplication.delayCall += KamerayiOdakla;
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= SahneGui;
            Undo.undoRedoPerformed -= Yenile;
            if (onizleme != null) DestroyImmediate(onizleme);
            if (onizlemeMesh != null) DestroyImmediate(onizlemeMesh);
            if (onizlemeIsigi != null) DestroyImmediate(onizlemeIsigi);
            onizleme = null;
            onizlemeMesh = null;
            onizlemeIsigi = null;
        }

        void YeniTasarim()
        {
            tasarim = CreateInstance<BinaTasarimi>();
            tasarim.name = "Yeni bina";
            tasarim.hideFlags = HideFlags.DontSaveInEditor;
        }

        /// <summary>Tür ve süs açılır listeleri (kütüphaneden; "Grup/ad" biçiminde: açılır menü gruplar).</summary>
        void ListeleriKur()
        {
            var kt = BinaAtolyesiKurucu.Kut;
            if (kt == null) { susAd = susEtiket = turAd = turEtiket = new string[0]; return; }
            var a = new List<string>(); var e = new List<string>();
            foreach (var p in kt.parcalar)
            {
                if (p.grup != "agac" && p.grup != "cevre") continue;
                a.Add(p.ad);
                e.Add((p.grup == "agac" ? "Ağaç ve çalı/" : "Dekor/") + p.aciklama);
            }
            susAd = a.ToArray(); susEtiket = e.ToArray();
            turAd = new string[kt.turler.Length]; turEtiket = new string[kt.turler.Length];
            for (int i = 0; i < kt.turler.Length; i++)
            {
                turAd[i] = kt.turler[i].ad;
                turEtiket[i] = kt.turler[i].grup + "/" + kt.turler[i].etiket.Replace("/", "-");
            }
            susSira = Mathf.Clamp(susSira, 0, Mathf.Max(0, susAd.Length - 1));
        }

        /// <summary>Sahne kamerasını arsaya, sokak (ön, +Z) tarafından hafif yukarıdan bakacak şekilde getirir.</summary>
        void KamerayiOdakla()
        {
            if (this == null) return;
            IsikHazirla();
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) return;
            sv.in2DMode = false;
            sv.orthographic = false;
            sv.LookAt(new Vector3(0f, 3f, 0f), Quaternion.Euler(32f, 205f, 0f), 11f);
            sv.Repaint();
        }

        /// <summary>Boş sahnede Pastel malzeme güneşsiz kararır; sahnede yönlü ışık yoksa geçici bir güneş koyar.</summary>
        void IsikHazirla()
        {
            if (onizlemeIsigi != null) return;
            foreach (var l in Object.FindObjectsByType<Light>())
                if (l.type == LightType.Directional && l.isActiveAndEnabled) return;
            onizlemeIsigi = new GameObject("Bina Atölyesi (ışık)") { hideFlags = HideFlags.DontSave };
            var isik = onizlemeIsigi.AddComponent<Light>();
            isik.type = LightType.Directional;
            isik.intensity = 1.3f;
            isik.color = new Color(1f, 0.96f, 0.88f);
            isik.shadows = LightShadows.Soft;
            onizlemeIsigi.transform.rotation = Quaternion.Euler(45f, 160f, 0f);   // sokak (ön, +Z) cephesi aydınlık
        }

        // ---------------------------------------------------------------- önizleme

        void Yenile()
        {
            if (tasarim == null) return;
            if (!Application.isPlaying) Shader.SetGlobalFloat("_PastelCizgiGizle", 0f);
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
            var b = new BinaTasarimi.Blok { x = x, y = y, k = k, tarz = tarz, renk = renk, cati = cati, ct = catiTarzi };
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

        // ---------------------------------------------------------------- süsler

        string SeciliSus => susAd != null && susAd.Length > 0 ? susAd[Mathf.Clamp(susSira, 0, susAd.Length - 1)] : null;

        /// <summary>Unity'deki zemin noktası → tasarımın metre koordinatı (arsa ortası 0, sokak −y).</summary>
        static Vector2 ArsaNoktasi(Vector3 p) => new Vector2(-p.x, -p.z);

        void SusKoy(Vector2 yer, bool dagit)
        {
            string ad = SeciliSus;
            if (ad == null) return;
            Undo.RecordObject(tasarim, dagit ? "Süs dağıt" : "Süs koy");
            int adet = dagit ? Mathf.Max(1, dagitAdet) : 1;
            float sinir = BinaAtolyesiKurucu.Izgara * BinaAtolyesiKurucu.Hucre * 0.5f;
            for (int i = 0; i < adet; i++)
            {
                Vector2 p = yer;
                float aci = susAci;
                if (dagit)
                {
                    p += Random.insideUnitCircle * dagitYaricap;
                    aci = Random.Range(0f, 360f);
                }
                if (Mathf.Abs(p.x) > sinir || Mathf.Abs(p.y) > sinir) continue;
                float o = susOlcek * (1f + Random.Range(-susOynama, susOynama));
                tasarim.susler.Add(new BinaTasarimi.Sus { ad = ad, x = p.x, y = p.y, aci = aci, olcek = Mathf.Max(0.1f, o), renk = susRenk });
            }
            EditorUtility.SetDirty(tasarim);
            Yenile();
        }

        void SusSil(Vector2 yer, float yaricap)
        {
            int en = -1;
            float enUzak = yaricap * yaricap;
            for (int i = 0; i < tasarim.susler.Count; i++)
            {
                float d = (new Vector2(tasarim.susler[i].x, tasarim.susler[i].y) - yer).sqrMagnitude;
                if (d < enUzak) { enUzak = d; en = i; }
            }
            if (en < 0) return;
            Undo.RecordObject(tasarim, "Süs sil");
            tasarim.susler.RemoveAt(en);
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
            bool blokModu = mod == ModBlok;
            float yari = n * h * 0.5f, yuk = blokModu ? kat * kb : 0f;
            var e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);

            // Fare nerede: blok modunda seçili katın taban düzlemi, süs modlarında zemin
            var isin = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var hucre = new Vector2Int(-1, -1);
            bool arsada = false;
            Vector3 yer = Vector3.zero;
            if (Mathf.Abs(isin.direction.y) > 1e-5f)
            {
                float tt = (yuk - isin.origin.y) / isin.direction.y;
                if (tt > 0f)
                {
                    yer = isin.origin + isin.direction * tt;
                    arsada = Mathf.Abs(yer.x) <= yari && Mathf.Abs(yer.z) <= yari;
                    int cx = Mathf.FloorToInt(-yer.x / h + n * 0.5f), cy = Mathf.FloorToInt(-yer.z / h + n * 0.5f);
                    if (cx >= 0 && cy >= 0 && cx < n && cy < n) hucre = new Vector2Int(cx, cy);
                }
            }

            switch (e.type)
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(id);
                    break;
                case EventType.MouseMove:
                    if (hucre != fare || !blokModu) { fare = hucre; fareYer = yer; fareArsada = arsada; sv.Repaint(); }
                    break;
                case EventType.MouseDown:
                    if (e.alt) break;                      // Alt: kamerayı döndürme
                    if (blokModu)
                    {
                        if (hucre.x < 0) break;
                        if (e.button == 0 && !e.shift) { Koy(hucre.x, hucre.y, kat); e.Use(); }
                        else if (e.button == 1 || (e.button == 0 && e.shift)) { Sil(hucre.x, hucre.y, kat); e.Use(); }
                    }
                    else if (arsada)
                    {
                        if (e.button == 0 && !e.shift) { SusKoy(ArsaNoktasi(yer), mod == ModDagit); e.Use(); }
                        else if (e.button == 1 || (e.button == 0 && e.shift)) { SusSil(ArsaNoktasi(yer), mod == ModDagit ? dagitYaricap : 1.2f); e.Use(); }
                    }
                    break;
                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.PageUp) { kat = Mathf.Min(KatSayisi - 1, kat + 1); e.Use(); Repaint(); sv.Repaint(); }
                    else if (e.keyCode == KeyCode.PageDown) { kat = Mathf.Max(0, kat - 1); e.Use(); Repaint(); sv.Repaint(); }
                    else if (e.keyCode == KeyCode.LeftBracket) { susAci = Mathf.Repeat(susAci - 15f, 360f); e.Use(); Repaint(); sv.Repaint(); }
                    else if (e.keyCode == KeyCode.RightBracket) { susAci = Mathf.Repeat(susAci + 15f, 360f); e.Use(); Repaint(); sv.Repaint(); }
                    break;
                case EventType.Repaint:
                    // Izgara (seçili katın tabanı) ve arsa sınırı; sokak tarafı (ön, +Z) kalın çizgi
                    Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                    Handles.color = new Color(0.9f, 0.85f, 1f, blokModu ? 0.45f : 0.2f);
                    for (int i = 0; i <= n; i++)
                    {
                        float a = -yari + i * h;
                        Handles.DrawLine(new Vector3(a, yuk, -yari), new Vector3(a, yuk, yari));
                        Handles.DrawLine(new Vector3(-yari, yuk, a), new Vector3(yari, yuk, a));
                    }
                    Handles.color = new Color(0.85f, 0.35f, 0.3f, 0.9f);
                    Handles.DrawLine(new Vector3(-yari, 0f, yari), new Vector3(yari, 0f, yari), 3f);
                    Handles.Label(new Vector3(0f, 0f, yari + 0.8f), "sokak (ön)");
                    if (blokModu && fare.x >= 0)
                    {
                        bool dolu = BlokBul(fare.x, fare.y, kat) >= 0;
                        Handles.color = dolu ? new Color(1f, 0.75f, 0.2f, 0.95f) : new Color(0.3f, 0.75f, 0.45f, 0.95f);
                        Handles.DrawWireCube(HucreYeri(fare.x, fare.y, kat) + new Vector3(0f, kb * 0.5f, 0f), new Vector3(h, kb, h));
                    }
                    else if (!blokModu && fareArsada)
                    {
                        Handles.color = new Color(0.3f, 0.75f, 0.45f, 0.95f);
                        Vector3 m = new Vector3(fareYer.x, 0.02f, fareYer.z);
                        Handles.DrawWireDisc(m, Vector3.up, mod == ModDagit ? dagitYaricap : 0.5f);
                        // yön oku (model ön yüzü −y → Unity +Z; açı model ekseninde saat yönünün tersine)
                        float a = susAci * Mathf.Deg2Rad;
                        Vector2 yon = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));          // model ekseninde döndürülmüş (0, −1)
                        if (mod == ModSus) Handles.DrawLine(m, m + new Vector3(-yon.x, 0f, -yon.y) * 0.9f, 2f);
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
                EditorGUILayout.HelpBox("Parça kütüphanesi okunamadı ya da eski: " + BinaAtolyesiKurucu.KutuphaneYol +
                                        "\nRepo kökünde: python3 Tools/Blender/pastel_parcalar.py --json", MessageType.Error);
                if (GUILayout.Button("Yeniden oku")) { BinaAtolyesiKurucu.Yukle(); ListeleriKur(); Yenile(); }
                EditorGUILayout.EndScrollView();
                return;
            }
            if (turAd == null || susAd == null) ListeleriKur();
            var kt = BinaAtolyesiKurucu.Kut;

            // ---- tasarım
            acTasarim = EditorGUILayout.BeginFoldoutHeaderGroup(acTasarim, "Tasarım");
            if (acTasarim)
            {
                EditorGUI.BeginChangeCheck();
                var yeni = (BinaTasarimi)EditorGUILayout.ObjectField("Dosya", EditorUtility.IsPersistent(tasarim) ? tasarim : null, typeof(BinaTasarimi), false);
                if (EditorGUI.EndChangeCheck() && yeni != null) { tasarim = yeni; if (tasarim.susler == null) tasarim.susler = new List<BinaTasarimi.Sus>(); Yenile(); }
                EditorGUI.BeginChangeCheck();
                string ad = EditorGUILayout.DelayedTextField("Ad", tasarim.ad);
                int tohum = EditorGUILayout.IntField("Tohum (varyant)", tasarim.tohum);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(tasarim, "Bina ayarı");
                    tasarim.ad = Temiz(ad);
                    tasarim.tohum = tohum;
                    EditorUtility.SetDirty(tasarim);
                    Yenile();
                }
                EditorGUILayout.LabelField($"{tasarim.bloklar.Count} blok · {tasarim.susler.Count} süs · {ucgen} üçgen (çizgiler dahil)", EditorStyles.miniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Yeni")) { YeniTasarim(); Yenile(); }
                    if (GUILayout.Button("Blokları sil") && tasarim.bloklar.Count > 0) { Undo.RecordObject(tasarim, "Blokları temizle"); tasarim.bloklar.Clear(); EditorUtility.SetDirty(tasarim); Yenile(); }
                    if (GUILayout.Button("Süsleri sil") && tasarim.susler.Count > 0) { Undo.RecordObject(tasarim, "Süsleri temizle"); tasarim.susler.Clear(); EditorUtility.SetDirty(tasarim); Yenile(); }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("JSON içe aktar")) IceAktar();
                    if (GUILayout.Button("JSON dışa aktar")) DisaAktar();
                    if (GUILayout.Button("Kamerayı getir")) KamerayiOdakla();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ---- kullanım türü, tabela, tente
            acKullanim = EditorGUILayout.BeginFoldoutHeaderGroup(acKullanim, "Kullanım (tür, tabela, tente)");
            if (acKullanim)
            {
                var tv = BinaAtolyesiKurucu.TurBul(tasarim);
                EditorGUI.BeginChangeCheck();
                int ts = Mathf.Max(0, System.Array.IndexOf(turAd, tv.ad));
                ts = EditorGUILayout.Popup("Tür", ts, turEtiket);
                string tabela = EditorGUILayout.DelayedTextField(new GUIContent("Tabela yazısı", "Boşsa türün varsayılanı. A–Z, 0–9 ve & ' - / . çizilir."), tasarim.tabela);
                int bolge = EditorGUILayout.Popup(new GUIContent("Bölge (havuz)", "Yalnız öneki Bolge olan türlerde prefab adına girer."),
                                                  Mathf.Max(0, System.Array.IndexOf(BinaAtolyesiKurucu.Bolgeler, tasarim.bolge)), BinaAtolyesiKurucu.Bolgeler);
                int tente = RenkSec("Tente rengi", kt.tenteRenk, tasarim.tente);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(tasarim, "Kullanım");
                    if (turAd[ts] != tv.ad)
                    {
                        tasarim.tur = turAd[ts];
                        string tb = kt.turler[ts].bolge;
                        if (!string.IsNullOrEmpty(tb)) tasarim.bolge = tb;
                    }
                    else tasarim.bolge = BinaAtolyesiKurucu.Bolgeler[bolge];
                    tasarim.tabela = tabela ?? "";
                    tasarim.tente = tente;
                    EditorUtility.SetDirty(tasarim);
                    Yenile();
                }
                tv = BinaAtolyesiKurucu.TurBul(tasarim);
                EditorGUILayout.HelpBox("Tür, binanın ZEMİN KATINDA sokağa (kırmızı çizgiye) bakan cepheyi değiştirir: vitrin, kapı, tente, tabela, " +
                                        "ön dekor. Üst katlar ve çatı fırçadaki tarz/renkle kurulur. Türün tam hâlini görmek için aşağıdaki " +
                                        "düğmeyle hazır örneğini yükleyip üzerinde oynayabilirsin.", MessageType.Info);
                if (!SokagaBakanZeminVar())
                    EditorGUILayout.HelpBox("Zemin katta sokağa bakan açık cephe yok: tür hiçbir şey göstermez. Kat 0'da, kırmızı çizgiye " +
                                            "en yakın sıraya (önünde blok olmayan) apartman / ahşap / taş bloğu koy.", MessageType.Warning);
                if (GUILayout.Button("Bu türün hazır örneğini yükle (" + tv.etiket + ")")) TurOrnegiYukle(tv.ad);
                EditorGUILayout.LabelField("Varsayılan tabela: " + (string.IsNullOrEmpty(tv.tabela) ? "(yok)" : tv.tabela) + " · prefab: " + PrefabAdi(tasarim), EditorStyles.miniLabel);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ---- fırça
            acFirca = EditorGUILayout.BeginFoldoutHeaderGroup(acFirca, "Fırça");
            if (acFirca)
            {
                EditorGUI.BeginChangeCheck();
                mod = GUILayout.Toolbar(mod, ModAd);
                if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
                if (mod == ModBlok)
                {
                    EditorGUI.BeginChangeCheck();
                    kat = EditorGUILayout.IntSlider("Kat (PageUp / PageDown)", kat, 0, KatSayisi - 1);
                    if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
                    tarz = EditorGUILayout.Popup("Duvar tarzı", tarz, BinaAtolyesiKurucu.TarzAd);
                    catiTarzi = EditorGUILayout.Popup("Çatı tarzı", Mathf.Clamp(catiTarzi, 0, kt.catiTarz.Length - 1), kt.catiTarz);
                    renk = RenkSec("Duvar rengi", kt.duvarRenk, renk);
                    cati = RenkSec("Çatı rengi", kt.catiRenk, cati);
                    EditorGUILayout.HelpBox("Sol tık blok koyar (dolu hücrede rengini / tarzını değiştirir), sağ tık ya da Shift + sol tık siler. Kırmızı çizgi sokak. " +
                                            "Tek hücrelik yüksek yığın kule tepesi alır (çatı tarzı 'Tarza göre' iken); altı boş blok kemerli geçit olur; " +
                                            "taş merdiven bitişiğindeki bloğa doğru çıkar. Aynı tarz ve çatı tarzındaki bitişik bloklar tek çatı altında birleşir.", MessageType.None);
                }
                else if (susAd.Length > 0)
                {
                    susSira = EditorGUILayout.Popup("Parça", susSira, susEtiket);
                    susRenk = RenkSec("Yaprak / vurgu rengi (mevsim)", kt.yaprakRenk, susRenk);
                    susOlcek = EditorGUILayout.Slider("Ölçek", susOlcek, 0.3f, 2.5f);
                    susOynama = EditorGUILayout.Slider("Ölçek oynaması", susOynama, 0f, 0.5f);
                    if (mod == ModSus) susAci = EditorGUILayout.Slider("Açı ([ ve ])", susAci, 0f, 360f);
                    else
                    {
                        dagitYaricap = EditorGUILayout.Slider("Yarıçap (m)", dagitYaricap, 0.3f, 5f);
                        dagitAdet = EditorGUILayout.IntSlider("Yoğunluk (tık başına)", dagitAdet, 1, 30);
                    }
                    EditorGUILayout.HelpBox("Sol tık koyar (ızgarasız), Shift + sol tık ya da sağ tık en yakını siler. Küçük dekorlar uzak görünümde (LOD1) atlanır.", MessageType.None);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ---- çevre modeli (tek başına ağaç / dekor prefabı)
            acCevre = EditorGUILayout.BeginFoldoutHeaderGroup(acCevre, "Çevre modeli (ağaç, çalı, dekor)");
            if (acCevre && susAd.Length > 0)
            {
                susSira = EditorGUILayout.Popup("Model", susSira, susEtiket);
                susRenk = RenkSec("Yaprak / vurgu rengi", kt.yaprakRenk, susRenk);
                var pv = BinaAtolyesiKurucu.ParcaBul(SeciliSus);
                EditorGUILayout.LabelField("Hedef: " + CevreHedefi(pv, susRenk), EditorStyles.miniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Tek başına göster"))
                    {
                        YeniTasarim();
                        tasarim.ad = SeciliSus;
                        tasarim.susler.Add(new BinaTasarimi.Sus { ad = SeciliSus, olcek = 1f, renk = susRenk });
                        Yenile();
                    }
                    if (GUILayout.Button("Prefab olarak kaydet"))
                    {
                        string yol = CevreKaydet(SeciliSus, susRenk);
                        if (yol != null) EditorUtility.DisplayDialog("Bina Atölyesi", "Kaydedildi:\n" + yol, "Tamam");
                    }
                }
                if (GUILayout.Button("Bütün ağaç, çalı ve donatıları üret"))
                {
                    int say = 0;
                    foreach (var p in kt.parcalar)
                        if ((p.grup == "agac" || p.grup == "cevre") && !string.IsNullOrEmpty(p.hedef) && CevreKaydet(p.ad, 0) != null) say++;
                    AssetDatabase.SaveAssets();
                    EditorUtility.DisplayDialog("Bina Atölyesi", say + " çevre modeli kaydedildi (Agaclar / Donatilar).\nOyunda kullanmak için: Resources/Gorsel/AtolyeAyari > Çevre modellerini kullan.", "Tamam");
                }
                EditorGUILayout.HelpBox("Ağaçların yaprak malzemesi 'Pastel_Yaprak_…': oyunda mevsimle renk değiştirir ve kar tutar. " +
                                        "Buradaki renk seçimi başlangıç rengidir (sonbahar / kış renkleri mevsimsiz sahneler için).", MessageType.None);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ---- kaydet
            acKaydet = EditorGUILayout.BeginFoldoutHeaderGroup(acKaydet, "Kaydet");
            if (acKaydet)
            {
                using (new EditorGUI.DisabledScope(tasarim.bloklar.Count == 0 && tasarim.susler.Count == 0))
                {
                    if (GUILayout.Button("Prefab olarak kaydet (" + PrefabAdi(tasarim) + ")", GUILayout.Height(30f)))
                    {
                        string yol = Kaydet(tasarim, true);
                        if (yol != null) EditorUtility.DisplayDialog("Bina Atölyesi", "Kaydedildi:\n" + yol + "\n\nYeni oyun başlatınca şehirde görünür (Resources/Gorsel/AtolyeAyari açıksa).", "Tamam");
                    }
                }
                if (GUILayout.Button("Örnek binaları üret (Editor/BinaAtolyesi/Ornekler)")) OrnekleriUret();
                if (GUILayout.Button("Parça kütüphanesini yeniden oku")) { BinaAtolyesiKurucu.Yukle(); ListeleriKur(); Yenile(); }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
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

        static string PrefabAdi(BinaTasarimi t) => BinaAtolyesiKurucu.Onek(t) + "_atolye_" + Temiz(t.ad);

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

        static BinaTasarimi JsondanOku(string yol)
        {
            var t = CreateInstance<BinaTasarimi>();
            t.hideFlags = HideFlags.DontSaveInEditor;
            JsonUtility.FromJsonOverwrite(File.ReadAllText(yol), t);
            if (t.bloklar == null) t.bloklar = new List<BinaTasarimi.Blok>();
            if (t.susler == null) t.susler = new List<BinaTasarimi.Sus>();
            if (t.tur == null) t.tur = "";
            if (t.tabela == null) t.tabela = "";
            t.ad = Temiz(t.ad);
            return t;
        }

        /// <summary>Zemin katta önü (sokak tarafı, y-1) boş, cephe alan bir blok var mı? Tür yalnız bu cephelere uygulanır.</summary>
        bool SokagaBakanZeminVar()
        {
            if (tasarim == null || tasarim.bloklar == null) return false;
            foreach (var b in tasarim.bloklar)
            {
                if (b.k != 0 || b.tarz == BinaAtolyesiKurucu.Kaide || b.tarz == BinaAtolyesiKurucu.Merdiven) continue;
                if (BlokBul(b.x, b.y - 1, 0) < 0) return true;
            }
            return false;
        }

        /// <summary>Ornekler klasöründe türü bu olan ilk tasarımı açar (mevcut tasarımın yerine).</summary>
        void TurOrnegiYukle(string turAdi)
        {
            string klasor = Path.Combine(Application.dataPath, "KuryeTycoon/Editor/BinaAtolyesi/Ornekler");
            if (!Directory.Exists(klasor)) return;
            var dosyalar = Directory.GetFiles(klasor, "*.json");
            System.Array.Sort(dosyalar);
            foreach (var d in dosyalar)
            {
                var t = JsondanOku(d);
                string ta = string.IsNullOrEmpty(t.tur) ? "konut" : t.tur;
                if (ta != turAdi) { DestroyImmediate(t); continue; }
                if (tasarim != null && tasarim.bloklar.Count > 0 &&
                    !EditorUtility.DisplayDialog("Bina Atölyesi", "Açık tasarım (" + tasarim.bloklar.Count + " blok) örnekle değişecek. " +
                                                 "Saklamak istersen önce 'JSON dışa aktar' ya da 'Prefab olarak kaydet'.", "Örneği yükle", "Vazgeç"))
                { DestroyImmediate(t); return; }
                tasarim = t;
                Yenile();
                KamerayiOdakla();
                return;
            }
            EditorUtility.DisplayDialog("Bina Atölyesi", "Bu tür için hazır örnek yok.", "Tamam");
        }

        void IceAktar()
        {
            string yol = EditorUtility.OpenFilePanel("Tasarım JSON'u", Application.dataPath + "/KuryeTycoon/Editor/BinaAtolyesi/Ornekler", "json");
            if (string.IsNullOrEmpty(yol)) return;
            tasarim = JsondanOku(yol);
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

        /// <summary>Mesh + _Hafif LOD + prefab (+ tasarım dosyası). Prefabın yolunu döndürür (kurulamazsa null).</summary>
        static string PrefabYaz(BinaTasarimi t, string ad, string klasor, bool lod)
        {
            var mesh = BinaAtolyesiKurucu.Kur(t, false, out var adlar);
            if (mesh == null) return null;
            List<string> hafifAdlar = null;
            var hafif = lod ? BinaAtolyesiKurucu.Kur(t, true, out hafifAdlar) : null;
            BinaAtolyesiKurucu.KlasorAc(AtolyeKlasor);
            BinaAtolyesiKurucu.KlasorAc(klasor);
            string meshYol = AtolyeKlasor + "/" + ad + ".asset", hafifYol = AtolyeKlasor + "/" + ad + "_Hafif.asset";
            mesh.name = ad;
            MeshYaz(mesh, meshYol);
            if (hafif != null) { hafif.name = ad + "_Hafif"; MeshYaz(hafif, hafifYol); }
            AssetDatabase.SaveAssets();
            var meshA = AssetDatabase.LoadAssetAtPath<Mesh>(meshYol);
            var hafifA = hafif != null ? AssetDatabase.LoadAssetAtPath<Mesh>(hafifYol) : null;

            GameObject kok;
            if (hafifA != null)
            {
                kok = new GameObject(ad);
                var l0 = Parca(kok.transform, "LOD0", meshA, BinaAtolyesiKurucu.Malzemeler(adlar));
                var l1 = Parca(kok.transform, "LOD1", hafifA, BinaAtolyesiKurucu.Malzemeler(hafifAdlar));
                var grup = kok.AddComponent<LODGroup>();
                grup.SetLODs(new[] { new LOD(LodGecis, new Renderer[] { l0 }), new LOD(0.0005f, new Renderer[] { l1 }) });
                grup.RecalculateBounds();
            }
            else
            {
                kok = new GameObject(ad);
                kok.AddComponent<MeshFilter>().sharedMesh = meshA;
                kok.AddComponent<MeshRenderer>().sharedMaterials = BinaAtolyesiKurucu.Malzemeler(adlar);
            }
            string prefabYol = klasor + "/" + ad + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(kok, prefabYol);
            DestroyImmediate(kok);
            return prefabYol;
        }

        static string Kaydet(BinaTasarimi t, bool tasarimiYaz)
        {
            string ad = PrefabAdi(t);
            string prefabYol = PrefabYaz(t, ad, PrefabKlasor, true);
            if (prefabYol == null) return null;
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

        /// <summary>Çevre modelinin gideceği klasör ve prefab adı (parçanın "hedef" alanından).</summary>
        static void CevreAdi(BinaAtolyesiKurucu.ParcaVeri pv, int renk, out string klasor, out string ad, out bool lod)
        {
            string hedef = pv.hedef ?? "";
            string ek = renk > 0 ? "_r" + renk : "";
            if (hedef == "agac") { klasor = AgacKlasor; ad = "Agac_atolye_" + pv.ad.Replace("agac_", "") + ek; lod = true; }
            else if (hedef == "cali") { klasor = AgacKlasor; ad = "Cali_atolye_" + pv.ad.Replace("cali_", "") + ek; lod = true; }
            else if (hedef.StartsWith("donati:")) { klasor = DonatiKlasor; ad = "Donati_" + hedef.Substring(7) + "_atolye_" + (renk + 1); lod = false; }
            else { klasor = DonatiKlasor; ad = "Donati_" + pv.ad + "_atolye_" + (renk + 1); lod = false; }
        }

        static string CevreHedefi(BinaAtolyesiKurucu.ParcaVeri pv, int renk)
        {
            if (pv == null) return "-";
            CevreAdi(pv, renk, out string klasor, out string ad, out _);
            return klasor.Substring(klasor.LastIndexOf('/') + 1) + "/" + ad + (string.IsNullOrEmpty(pv.hedef) ? "  (oyunda bu ada bakan havuz yok)" : "");
        }

        /// <summary>Tek bir ağaç / dekoru prefab olarak kaydeder (kök modelin tabanının ortasında, ön +Z).</summary>
        static string CevreKaydet(string parcaAdi, int renk)
        {
            var pv = BinaAtolyesiKurucu.ParcaBul(parcaAdi);
            if (pv == null) return null;
            var t = CreateInstance<BinaTasarimi>();
            t.hideFlags = HideFlags.DontSaveInEditor;
            t.ad = parcaAdi;
            t.susler.Add(new BinaTasarimi.Sus { ad = parcaAdi, olcek = 1f, renk = renk });
            CevreAdi(pv, renk, out string klasor, out string ad, out bool lod);
            string yol = PrefabYaz(t, ad, klasor, lod);
            DestroyImmediate(t);
            return yol;
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
            var dosyalar = Directory.GetFiles(klasor, "*.json");
            try
            {
                for (int i = 0; i < dosyalar.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Bina Atölyesi", Path.GetFileName(dosyalar[i]), (float)i / dosyalar.Length);
                    var t = JsondanOku(dosyalar[i]);
                    if ((t.bloklar.Count > 0 || t.susler.Count > 0) && Kaydet(t, true) != null) say++;
                    if (!EditorUtility.IsPersistent(t)) DestroyImmediate(t);
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            EditorUtility.DisplayDialog("Bina Atölyesi", say + " örnek prefab olarak kaydedildi (" + PrefabKlasor + ").", "Tamam");
        }
    }
}
