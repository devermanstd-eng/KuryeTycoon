using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// Bina Atölyesi (yalnız geliştirme aracı; oyuncu görmez): "birkaç blok koy, binaya dönüşsün" + çevre atölyesi.
    /// Scene görünümünde 5 × 5 hücrelik (14 m arsa) ızgara. Her değişiklikte AtolyeKurucu (oyunla ortak kurucu) komşuluğa ve kullanım türüne
    /// göre pastel parçaları seçip modeli anında kurar (tohumlu: aynı tasarım hep aynı model).
    ///  • Blok fırçası: sol tık blok koy (dolu hücrede rengini / tarzını değiştirir) · sağ tık ya da Shift + sol tık sil ·
    ///    PageUp / PageDown kat.
    ///  • Süs fırçası: seçili dekor / ağacı ızgarasız, tıklanan yere koyar · Shift + tık en yakını siler · [ ve ] döndürür.
    ///  • Dağıt fırçası: seçili parçadan yarıçap içine yoğunluk kadar serper (çiçek, ot, çalı).
    ///  • Kullanım türü (dükkân, market, kamu, sanayi, simge, konut): zemin kata türün vitrini, kapısı, tentesi, tabelası
    ///    (yazı + simge) ve ön dekorları gelir; prefab adı türe göre City3D'nin doğru havuzuna girer.
    ///  • İç mekân: zemin katın içi türe göre kendiliğinden döşenir (AtolyeKurucu.IcPlan); "İç mekânı göster" önizlemede
    ///    çatıyı kaldırır. Prefabda parçalar ayrı çocuklardır (_Govde, _Duvar_*, _Cati, _Ic, _Kepenk…) ve köke AtolyePlani yazılır.
    ///  • "Tasarımı kaydet": yalnız tasarım JSON'u yazılır (Resources/Atolye/Tasarimlar/atolye_&lt;ad&gt;.json). Mesh ve prefab
    ///    dosyası YOK: model oyun çalışırken kurulur (World/Atolye/AtolyeUretici; adı &lt;önek&gt;_atolye_&lt;ad&gt;).
    ///  • Çevre modelleri (ağaç, çalı, donatı) da oyun çalışırken parça kütüphanesinden kurulur.
    /// Geri al: Ctrl + Z.
    /// </summary>
    public class BinaAtolyesi : EditorWindow
    {
        /// <summary>Tasarım JSON'larının durduğu klasör (oyun buradan okur): Assets/KuryeTycoon/Resources/Atolye/Tasarimlar.</summary>
        const string TasarimKlasor = "Assets/KuryeTycoon/Resources/Atolye/Tasarimlar";
        static string TasarimKlasorTam => Path.Combine(Application.dataPath, "KuryeTycoon/Resources/Atolye/Tasarimlar");
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
        bool icGoster;       // önizlemede çatı / üst katlar kalkık, iç mekân görünür

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
            var kt = AtolyeKurucu.Kut;
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
            var mesh = AtolyeKurucu.Kur(tasarim, false, out var adlar, icGoster);
            var mf = onizleme.GetComponent<MeshFilter>();
            var mr = onizleme.GetComponent<MeshRenderer>();
            if (onizlemeMesh != null) DestroyImmediate(onizlemeMesh);
            onizlemeMesh = mesh;
            ucgen = 0;
            if (mesh != null)
            {
                mesh.hideFlags = HideFlags.DontSave;
                mf.sharedMesh = mesh;
                mr.sharedMaterials = AtolyeKurucu.Malzemeler(adlar);
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
            float sinir = AtolyeKurucu.Izgara * AtolyeKurucu.Hucre * 0.5f;
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
            float h = AtolyeKurucu.Hucre;
            int n = AtolyeKurucu.Izgara;
            float mx = (x - (n - 1) * 0.5f) * h, my = (y - (n - 1) * 0.5f) * h;
            return new Vector3(-mx, k * AtolyeKurucu.KatBoyu, -my);
        }

        void SahneGui(SceneView sv)
        {
            if (tasarim == null) return;
            float h = AtolyeKurucu.Hucre, kb = AtolyeKurucu.KatBoyu;
            int n = AtolyeKurucu.Izgara;
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
            if (AtolyeKurucu.Kut == null)
            {
                EditorGUILayout.HelpBox("Parça kütüphanesi okunamadı ya da eski: " + AtolyeKurucu.KutuphaneYol +
                                        "\nRepo kökünde: python3 Tools/Blender/pastel_parcalar.py --json", MessageType.Error);
                if (GUILayout.Button("Yeniden oku")) { AtolyeKurucu.Yukle(); ListeleriKur(); Yenile(); }
                EditorGUILayout.EndScrollView();
                return;
            }
            if (turAd == null || susAd == null) ListeleriKur();
            var kt = AtolyeKurucu.Kut;

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
                EditorGUI.BeginChangeCheck();
                icGoster = EditorGUILayout.ToggleLeft(new GUIContent("İç mekânı göster (çatı ve üst katlar kalkık)",
                    "Zemin katın içi kullanım türüne göre kendiliğinden döşenir: mobilyalar öne bakar, kapıdan her hücreye yol açık kalır. " +
                    "Oyunda kamera yaklaşınca çatı kalkar; kepenk, poşet ve insanlar oyunda eklenir."), icGoster);
                if (EditorGUI.EndChangeCheck()) Yenile();
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
                // Sahne modu: zemin (yol, çim, kaldırım, su, yükselti) + birden çok bina aynı sahnede (Editor/SahneAtolyesi.cs)
                if (GUILayout.Button("Sahne Atölyesi'ni aç (zemin, yollar, birden çok bina)")) SahneAtolyesi.Ac();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ---- kullanım türü, tabela, tente
            acKullanim = EditorGUILayout.BeginFoldoutHeaderGroup(acKullanim, "Kullanım (tür, tabela, tente)");
            if (acKullanim)
            {
                var tv = AtolyeKurucu.TurBul(tasarim);
                EditorGUI.BeginChangeCheck();
                int ts = Mathf.Max(0, System.Array.IndexOf(turAd, tv.ad));
                ts = EditorGUILayout.Popup("Tür", ts, turEtiket);
                string tabela = EditorGUILayout.DelayedTextField(new GUIContent("Tabela yazısı", "Boşsa türün varsayılanı. A–Z, 0–9 ve & ' - / . çizilir."), tasarim.tabela);
                int bolge = EditorGUILayout.Popup(new GUIContent("Bölge (havuz)", "Yalnız öneki Bolge olan türlerde prefab adına girer."),
                                                  Mathf.Max(0, System.Array.IndexOf(AtolyeKurucu.Bolgeler, tasarim.bolge)), AtolyeKurucu.Bolgeler);
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
                    else tasarim.bolge = AtolyeKurucu.Bolgeler[bolge];
                    tasarim.tabela = tabela ?? "";
                    tasarim.tente = tente;
                    EditorUtility.SetDirty(tasarim);
                    Yenile();
                }
                tv = AtolyeKurucu.TurBul(tasarim);
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
                    tarz = EditorGUILayout.Popup("Duvar tarzı", tarz, AtolyeKurucu.TarzAd);
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
                var pv = AtolyeKurucu.ParcaBul(SeciliSus);
                EditorGUILayout.LabelField("Oyunda: " + CevreHedefi(pv), EditorStyles.miniLabel);
                if (GUILayout.Button("Tek başına göster"))
                {
                    YeniTasarim();
                    tasarim.ad = SeciliSus;
                    tasarim.susler.Add(new BinaTasarimi.Sus { ad = SeciliSus, olcek = 1f, renk = susRenk });
                    Yenile();
                }
                EditorGUILayout.HelpBox("Ağaç, çalı ve donatılar için dosya üretilmez: parça kütüphanesinde hedefi olan her parça oyun " +
                                        "çalışırken kendiliğinden kurulur (Resources/Gorsel/AtolyeAyari > Çevre modellerini kullan açıksa).", MessageType.None);
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
                    if (GUILayout.Button("Tasarımı kaydet (" + PrefabAdi(tasarim) + ")", GUILayout.Height(30f)))
                    {
                        string yol = Kaydet(tasarim);
                        if (yol != null) EditorUtility.DisplayDialog("Bina Atölyesi", "Kaydedildi:\n" + yol + "\n\nMesh ya da prefab dosyası yok: bina oyun çalışırken bu tasarımdan kurulur. " +
                                                                     "Yeni oyun başlatınca şehirde görünür (Resources/Gorsel/AtolyeAyari açıksa).", "Tamam");
                    }
                }
                EditorGUILayout.HelpBox("Klasördeki her tasarım (" + TasarimKlasor + ") oyunda bir bina olur. Silmek için JSON dosyasını sil.", MessageType.None);
                if (GUILayout.Button("Parça kütüphanesini yeniden oku")) { AtolyeKurucu.Yukle(); ListeleriKur(); Yenile(); }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.EndScrollView();
        }

        static string Temiz(string s) => AtolyeKurucu.TemizAd(s);

        static string PrefabAdi(BinaTasarimi t) => AtolyeKurucu.ModelAdi(t);

        static int RenkSec(string baslik, string[] renkler, int secili)
        {
            EditorGUILayout.LabelField(baslik);
            using (new EditorGUILayout.HorizontalScope())
            {
                var eski = GUI.backgroundColor;
                for (int i = 0; i < renkler.Length; i++)
                {
                    GUI.backgroundColor = AtolyeKurucu.Renk(renkler[i]);
                    if (GUILayout.Button(i == secili ? "●" : "", GUILayout.Width(24f), GUILayout.Height(22f))) secili = i;
                }
                GUI.backgroundColor = eski;
            }
            return Mathf.Clamp(secili, 0, renkler.Length - 1);
        }

        // ---------------------------------------------------------------- içe / dışa aktarma, kaydetme

        static BinaTasarimi JsondanOku(string yol)
        {
            var t = AtolyeKurucu.TasarimOku(File.ReadAllText(yol));
            if (t == null) t = CreateInstance<BinaTasarimi>();
            t.hideFlags = HideFlags.DontSaveInEditor;
            return t;
        }

        /// <summary>Zemin katta önü (sokak tarafı, y-1) boş, cephe alan bir blok var mı? Tür yalnız bu cephelere uygulanır.</summary>
        bool SokagaBakanZeminVar()
        {
            if (tasarim == null || tasarim.bloklar == null) return false;
            foreach (var b in tasarim.bloklar)
            {
                if (b.k != 0 || b.tarz == AtolyeKurucu.Kaide || b.tarz == AtolyeKurucu.Merdiven) continue;
                if (BlokBul(b.x, b.y - 1, 0) < 0) return true;
            }
            return false;
        }

        /// <summary>Ornekler klasöründe türü bu olan ilk tasarımı açar (mevcut tasarımın yerine).</summary>
        void TurOrnegiYukle(string turAdi)
        {
            string klasor = TasarimKlasorTam;
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
                                                 "Saklamak istersen önce 'JSON dışa aktar' ya da 'Tasarımı kaydet'.", "Örneği yükle", "Vazgeç"))
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
            string yol = EditorUtility.OpenFilePanel("Tasarım JSON'u", TasarimKlasorTam, "json");
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

        /// <summary>
        /// Tasarımı oyunun okuduğu klasöre JSON olarak yazar (Resources/Atolye/Tasarimlar/atolye_&lt;ad&gt;.json). Mesh ya da prefab
        /// dosyası üretilmez: model oyun çalışırken bu JSON'dan kurulur (AtolyeUretici). Yazılan dosyanın yolunu döndürür.
        /// </summary>
        static string Kaydet(BinaTasarimi t)
        {
            if (t == null || (t.bloklar.Count == 0 && t.susler.Count == 0)) return null;
            t.ad = Temiz(t.ad);
            Directory.CreateDirectory(TasarimKlasorTam);
            string dosya = "atolye_" + t.ad + ".json";
            File.WriteAllText(Path.Combine(TasarimKlasorTam, dosya), JsonUtility.ToJson(t, true));
            AssetDatabase.Refresh();
            AtolyeUretici.Sifirla();      // oyun açıksa: sonraki şehir kurulumunda yeni tasarım da kurulur
            return TasarimKlasor + "/" + dosya;
        }

        /// <summary>Çevre modelinin oyundaki havuzu (parçanın "hedef" alanından).</summary>
        static string CevreHedefi(AtolyeKurucu.ParcaVeri pv)
        {
            if (pv == null) return "-";
            string hedef = pv.hedef ?? "";
            if (hedef == "agac") return "ağaç havuzu (Agac_atolye_" + pv.ad.Replace("agac_", "") + ")";
            if (hedef == "cali") return "çalı havuzu (Cali_atolye_" + pv.ad.Replace("cali_", "") + ")";
            if (hedef.StartsWith("donati:")) return "sokak donatısı: " + hedef.Substring(7);
            return "yalnız arsa süsü (oyunda bu parçaya bakan havuz yok)";
        }
    }
}
