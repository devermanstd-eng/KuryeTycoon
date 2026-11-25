using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// Sahne Atölyesi (yalnız geliştirme aracı): Bina Atölyesi'nin "sahne" modu. 16 × 16 hücrelik (44,8 m) zeminde yollar, çim,
    /// kaldırım, meydan, patika, su, tarh ve çit boyanır; hücreler 0–2 kademe yükseltilir; kaydedilmiş bina tasarımları
    /// yerleştirilir, süs / ağaç konur. Zemin komşuluğa göre kendiliğinden döşenir (AtolyeKurucu.ZeminYerlesim): yola şerit,
    /// yaya geçidi, kaldırım + bordür; suya taş rıhtım; kademe kenarına yamaç / istinat duvarı.
    ///  • Zemin fırçası: sol tık / sürükle boyar · sağ tık ya da Shift + sol tık çime (kademe 0) döndürür.
    ///  • Bina fırçası: sol tık seçili tasarımı hücreye koyar (5 × 5 arsanın ortası) · R döndürür · Shift + tık en yakını siler.
    ///  • Süs fırçası: sol tık koyar · Shift + tık en yakını siler · [ ve ] döndürür.
    ///  • Kaydet: yalnız JSON (Resources/Atolye/Sahneler/sahne_&lt;ad&gt;.json); model dosyası yok, önizleme çalışırken kurulur.
    /// Geri al: penceredeki "Geri al" düğmesi (sahne bir Unity nesnesi olmadığı için Ctrl + Z çalışmaz).
    /// </summary>
    public class SahneAtolyesi : EditorWindow
    {
        const string SahneKlasor = "Assets/KuryeTycoon/Resources/Atolye/Sahneler";
        static string SahneKlasorTam => Path.Combine(Application.dataPath, "KuryeTycoon/Resources/Atolye/Sahneler");
        static string TasarimKlasorTam => Path.Combine(Application.dataPath, "KuryeTycoon/Resources/Atolye/Tasarimlar");
        const int ModZemin = 0, ModBina = 1, ModSus = 2;
        static readonly string[] ModAd = { "Zemin", "Bina", "Süs / ağaç" };
        static readonly string[] KademeAd = { "0 (düz)", "1 (+1,2 m)", "2 (+2,4 m)" };

        SahneTasarimi sahne;
        int mod, zeminSira = 1, kademe, binaSira, binaDon, susSira, susRenk, sahneSira;
        float susAci, susOlcek = 1f;
        bool yalnizKademe;
        string[] zeminAd, zeminEtiket, binaAd, susAd, susEtiket, sahneAd;
        readonly List<string> geri = new List<string>();

        GameObject onizleme, zeminGo, isik;
        Mesh zeminMesh;
        readonly List<GameObject> binaGo = new List<GameObject>();
        string binaImza = "";
        Vector2Int fare = new Vector2Int(-1, -1);
        Vector3 fareYer;
        bool fareIcinde;
        Vector2 kaydir;
        int ucgen;

        [MenuItem("Kurye Tycoon/Sahne Atölyesi (zemin, yollar, binalar)")]
        public static void Ac()
        {
            var w = GetWindow<SahneAtolyesi>("Sahne Atölyesi");
            w.minSize = new Vector2(320f, 420f);
            w.Show();
        }

        void OnEnable()
        {
            SceneView.duringSceneGui += SahneGui;
            if (!Application.isPlaying) Shader.SetGlobalFloat("_PastelCizgiGizle", 0f);
            if (sahne == null) sahne = new SahneTasarimi();
            ListeleriKur();
            HepsiniYenile();
            EditorApplication.delayCall += KamerayiOdakla;
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= SahneGui;
            Temizle();
            if (isik != null) DestroyImmediate(isik);
            isik = null;
        }

        void Temizle()
        {
            if (onizleme != null) DestroyImmediate(onizleme);
            if (zeminMesh != null) DestroyImmediate(zeminMesh);
            onizleme = null; zeminGo = null; zeminMesh = null;
            binaGo.Clear();
            binaImza = "";
        }

        void ListeleriKur()
        {
            var kt = AtolyeKurucu.Kut;
            zeminAd = new string[0]; zeminEtiket = new string[0]; susAd = new string[0]; susEtiket = new string[0];
            if (kt != null)
            {
                if (kt.zeminTur != null)
                {
                    zeminAd = new string[kt.zeminTur.Length]; zeminEtiket = new string[kt.zeminTur.Length];
                    for (int i = 0; i < kt.zeminTur.Length; i++) { zeminAd[i] = kt.zeminTur[i].ad; zeminEtiket[i] = kt.zeminTur[i].etiket.Replace("/", "-"); }
                }
                var a = new List<string>(); var e = new List<string>();
                foreach (var p in kt.parcalar)
                {
                    if (p.grup != "agac" && p.grup != "cevre") continue;
                    a.Add(p.ad);
                    e.Add((p.grup == "agac" ? "Ağaç ve çalı/" : "Dekor/") + p.aciklama);
                }
                susAd = a.ToArray(); susEtiket = e.ToArray();
            }
            binaAd = DosyaAdlari(TasarimKlasorTam, "atolye_");
            sahneAd = DosyaAdlari(SahneKlasorTam, "sahne_");
            zeminSira = Mathf.Clamp(zeminSira, 0, Mathf.Max(0, zeminAd.Length - 1));
            binaSira = Mathf.Clamp(binaSira, 0, Mathf.Max(0, binaAd.Length - 1));
            susSira = Mathf.Clamp(susSira, 0, Mathf.Max(0, susAd.Length - 1));
            sahneSira = Mathf.Clamp(sahneSira, 0, Mathf.Max(0, sahneAd.Length - 1));
        }

        static string[] DosyaAdlari(string klasor, string onek)
        {
            var l = new List<string>();
            if (Directory.Exists(klasor))
                foreach (string d in Directory.GetFiles(klasor, onek + "*.json"))
                    l.Add(Path.GetFileNameWithoutExtension(d).Substring(onek.Length));
            l.Sort(string.CompareOrdinal);
            return l.ToArray();
        }

        void KamerayiOdakla()
        {
            if (this == null) return;
            if (isik == null)
            {
                bool var_ = false;
                foreach (var l in Object.FindObjectsByType<Light>())
                    if (l.type == LightType.Directional && l.isActiveAndEnabled) var_ = true;
                if (!var_)
                {
                    isik = new GameObject("Sahne Atölyesi (ışık)") { hideFlags = HideFlags.DontSave };
                    var li = isik.AddComponent<Light>();
                    li.type = LightType.Directional;
                    li.intensity = 1.3f;
                    li.color = new Color(1f, 0.96f, 0.88f);
                    li.shadows = LightShadows.Soft;
                    isik.transform.rotation = Quaternion.Euler(45f, 160f, 0f);
                }
            }
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) return;
            sv.in2DMode = false;
            sv.orthographic = false;
            sv.LookAt(new Vector3(0f, 2f, 0f), Quaternion.Euler(38f, 210f, 0f), 38f);
            sv.Repaint();
        }

        // ---------------------------------------------------------------- önizleme

        void KokHazirla()
        {
            if (onizleme != null) return;
            onizleme = new GameObject("Sahne Atölyesi (önizleme)") { hideFlags = HideFlags.DontSave };
            zeminGo = new GameObject("Zemin") { hideFlags = HideFlags.DontSave };
            zeminGo.transform.SetParent(onizleme.transform, false);
            zeminGo.AddComponent<MeshFilter>();
            zeminGo.AddComponent<MeshRenderer>();
            binaGo.Clear();
            binaImza = "";
        }

        /// <summary>Zemin + süs mesh'ini yeniden kurar (zemin ya da süs değişince).</summary>
        void ZeminiYenile()
        {
            if (sahne == null) return;
            KokHazirla();
            var mesh = AtolyeKurucu.ZeminKur(sahne, out var adlar);
            if (zeminMesh != null) DestroyImmediate(zeminMesh);
            zeminMesh = mesh;
            ucgen = 0;
            var mf = zeminGo.GetComponent<MeshFilter>();
            if (mesh != null)
            {
                mesh.hideFlags = HideFlags.DontSave;
                mf.sharedMesh = mesh;
                zeminGo.GetComponent<MeshRenderer>().sharedMaterials = AtolyeKurucu.Malzemeler(adlar);
                for (int i = 0; i < mesh.subMeshCount; i++) ucgen += (int)(mesh.GetIndexCount(i) / 3);
            }
            else mf.sharedMesh = null;
            SceneView.RepaintAll();
            Repaint();
        }

        /// <summary>Binaları yeniden kurar (bina listesi ya da altlarındaki kademe değişince; imza aynıysa dokunmaz).</summary>
        void BinalariYenile()
        {
            if (sahne == null) return;
            KokHazirla();
            var sb = new System.Text.StringBuilder();
            foreach (var b in sahne.binalar)
                sb.Append(b.tasarim).Append(':').Append(b.x).Append(',').Append(b.y).Append(',').Append(b.don).Append(',').Append(b.kapali ? 1 : 0)
                  .Append(',').Append(sahne.Kademe(b.x, b.y)).Append(';');
            string imza = sb.ToString();
            if (imza == binaImza) return;
            binaImza = imza;
            foreach (var go in binaGo) if (go != null) DestroyImmediate(go);
            binaGo.Clear();
            foreach (var b in sahne.binalar)
            {
                var go = AtolyeUretici.BinaKur(sahne, b, onizleme.transform);
                if (go == null) continue;
                foreach (var tr in go.GetComponentsInChildren<Transform>(true)) tr.gameObject.hideFlags = HideFlags.DontSave;
                binaGo.Add(go);
            }
            SceneView.RepaintAll();
        }

        void HepsiniYenile()
        {
            Temizle();
            ZeminiYenile();
            BinalariYenile();
        }

        // ---------------------------------------------------------------- düzenleme

        void GeriKaydet()
        {
            geri.Add(JsonUtility.ToJson(sahne));
            if (geri.Count > 40) geri.RemoveAt(0);
        }

        void GeriAl()
        {
            if (geri.Count == 0) return;
            string j = geri[geri.Count - 1];
            geri.RemoveAt(geri.Count - 1);
            sahne = JsonUtility.FromJson<SahneTasarimi>(j);
            Duzelt();
            ZeminiYenile();
            BinalariYenile();
        }

        void Duzelt()
        {
            if (sahne == null) sahne = new SahneTasarimi();
            if (sahne.zemin == null) sahne.zemin = new List<SahneTasarimi.Zemin>();
            if (sahne.binalar == null) sahne.binalar = new List<SahneTasarimi.Bina>();
            if (sahne.susler == null) sahne.susler = new List<BinaTasarimi.Sus>();
            if (sahne.n < 4) sahne.n = 16;
            sahne.ad = AtolyeKurucu.TemizAd(sahne.ad);
        }

        /// <summary>Hücreyi boyar. Değiştiyse true.</summary>
        bool Boya(int x, int y, bool sil)
        {
            if (zeminAd.Length == 0) return false;
            int i = sahne.ZeminBul(x, y);
            string eskiT = i >= 0 ? sahne.zemin[i].t : "cim";
            int eskiK = i >= 0 ? sahne.zemin[i].k : 0;
            string t = sil ? "cim" : yalnizKademe ? eskiT : zeminAd[zeminSira];
            int k = sil ? 0 : kademe;
            if (t == eskiT && k == eskiK) return false;
            if (t == "cim" && k == 0) { if (i >= 0) sahne.zemin.RemoveAt(i); }
            else
            {
                var h = new SahneTasarimi.Zemin { x = x, y = y, t = t, k = k };
                if (i >= 0) sahne.zemin[i] = h; else sahne.zemin.Add(h);
            }
            return true;
        }

        void BinaKoy(int x, int y)
        {
            if (binaAd.Length == 0) return;
            GeriKaydet();
            sahne.binalar.Add(new SahneTasarimi.Bina { tasarim = binaAd[binaSira], x = x, y = y, don = binaDon });
            BinalariYenile();
        }

        void BinaSil(int x, int y)
        {
            int en = -1, enUzak = 99;
            for (int i = 0; i < sahne.binalar.Count; i++)
            {
                int d = Mathf.Abs(sahne.binalar[i].x - x) + Mathf.Abs(sahne.binalar[i].y - y);
                if (d < enUzak) { enUzak = d; en = i; }
            }
            if (en < 0 || enUzak > 3) return;
            GeriKaydet();
            sahne.binalar.RemoveAt(en);
            BinalariYenile();
        }

        void SusKoy(Vector2 yer)
        {
            if (susAd.Length == 0) return;
            GeriKaydet();
            sahne.susler.Add(new BinaTasarimi.Sus { ad = susAd[susSira], x = yer.x, y = yer.y, aci = susAci, olcek = Mathf.Max(0.1f, susOlcek), renk = susRenk });
            ZeminiYenile();
        }

        void SusSil(Vector2 yer)
        {
            int en = -1;
            float enUzak = 1.6f * 1.6f;
            for (int i = 0; i < sahne.susler.Count; i++)
            {
                float d = (new Vector2(sahne.susler[i].x, sahne.susler[i].y) - yer).sqrMagnitude;
                if (d < enUzak) { enUzak = d; en = i; }
            }
            if (en < 0) return;
            GeriKaydet();
            sahne.susler.RemoveAt(en);
            ZeminiYenile();
        }

        // ---------------------------------------------------------------- Scene görünümü

        void SahneGui(SceneView sv)
        {
            if (sahne == null) return;
            float h = AtolyeKurucu.Hucre;
            int n = sahne.n;
            float yari = n * h * 0.5f;
            var e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);

            var isin = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var hucre = new Vector2Int(-1, -1);
            bool icinde = false;
            Vector3 yer = Vector3.zero;
            if (Mathf.Abs(isin.direction.y) > 1e-5f)
            {
                float tt = -isin.origin.y / isin.direction.y;
                if (tt > 0f)
                {
                    yer = isin.origin + isin.direction * tt;
                    icinde = Mathf.Abs(yer.x) <= yari && Mathf.Abs(yer.z) <= yari;
                    int cx = Mathf.FloorToInt(-yer.x / h + n * 0.5f), cy = Mathf.FloorToInt(-yer.z / h + n * 0.5f);
                    if (cx >= 0 && cy >= 0 && cx < n && cy < n) hucre = new Vector2Int(cx, cy);
                }
            }
            bool silme = e.button == 1 || e.shift;

            switch (e.type)
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(id);
                    break;
                case EventType.MouseMove:
                    fare = hucre; fareYer = yer; fareIcinde = icinde;
                    sv.Repaint();
                    break;
                case EventType.MouseDown:
                    if (e.alt || (e.button != 0 && e.button != 1)) break;
                    if (mod == ModZemin)
                    {
                        if (hucre.x < 0) break;
                        GeriKaydet();
                        if (Boya(hucre.x, hucre.y, silme)) { ZeminiYenile(); BinalariYenile(); }
                        else geri.RemoveAt(geri.Count - 1);
                        e.Use();
                    }
                    else if (mod == ModBina)
                    {
                        if (hucre.x < 0) break;
                        if (silme) BinaSil(hucre.x, hucre.y); else BinaKoy(hucre.x, hucre.y);
                        e.Use();
                    }
                    else if (icinde)
                    {
                        var p = new Vector2(-yer.x, -yer.z);
                        if (silme) SusSil(p); else SusKoy(p);
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (e.alt || mod != ModZemin || hucre.x < 0 || (e.button != 0 && e.button != 1)) break;
                    fare = hucre;
                    if (Boya(hucre.x, hucre.y, silme)) { ZeminiYenile(); BinalariYenile(); }
                    e.Use();
                    break;
                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.R && mod == ModBina) { binaDon = (binaDon + 1) % 4; e.Use(); Repaint(); sv.Repaint(); }
                    else if (e.keyCode == KeyCode.LeftBracket) { susAci = Mathf.Repeat(susAci - 15f, 360f); e.Use(); Repaint(); sv.Repaint(); }
                    else if (e.keyCode == KeyCode.RightBracket) { susAci = Mathf.Repeat(susAci + 15f, 360f); e.Use(); Repaint(); sv.Repaint(); }
                    break;
                case EventType.Repaint:
                    Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                    Handles.color = new Color(0.25f, 0.2f, 0.3f, mod == ModSus ? 0.12f : 0.3f);
                    for (int i = 0; i <= n; i++)
                    {
                        float a = -yari + i * h;
                        Handles.DrawLine(new Vector3(a, 0.03f, -yari), new Vector3(a, 0.03f, yari));
                        Handles.DrawLine(new Vector3(-yari, 0.03f, a), new Vector3(yari, 0.03f, a));
                    }
                    Handles.color = new Color(0.85f, 0.35f, 0.3f, 0.9f);
                    Handles.DrawLine(new Vector3(-yari, 0f, yari), new Vector3(yari, 0f, yari), 3f);
                    Handles.Label(new Vector3(0f, 0f, yari + 1.2f), "ön (y = 0)");
                    if (mod != ModSus && fare.x >= 0)
                    {
                        Vector2 c = AtolyeKurucu.SahneMerkezi(n, fare.x, fare.y);
                        float yk = sahne.Kademe(fare.x, fare.y) * AtolyeKurucu.Kademe;
                        var m = new Vector3(-c.x, yk + 0.05f, -c.y);
                        Handles.color = new Color(0.3f, 0.75f, 0.45f, 0.95f);
                        if (mod == ModZemin) Handles.DrawWireCube(m, new Vector3(h, 0.1f, h));
                        else
                        {
                            // binanın 5 × 5 arsası ve ön yüzünün baktığı yön
                            Handles.DrawWireCube(m, new Vector3(h * 5f, 0.1f, h * 5f));
                            Vector2 on = new Vector2(0f, -1f);
                            for (int q = 0; q < binaDon; q++) on = new Vector2(-on.y, on.x);
                            Handles.DrawLine(m, m + new Vector3(-on.x, 0f, -on.y) * h * 3f, 3f);
                        }
                    }
                    else if (mod == ModSus && fareIcinde)
                    {
                        Handles.color = new Color(0.3f, 0.75f, 0.45f, 0.95f);
                        Handles.DrawWireDisc(new Vector3(fareYer.x, 0.05f, fareYer.z), Vector3.up, 0.5f);
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- pencere

        void OnGUI()
        {
            kaydir = EditorGUILayout.BeginScrollView(kaydir);
            if (AtolyeKurucu.Kut == null || zeminAd == null || zeminAd.Length == 0)
            {
                EditorGUILayout.HelpBox("Parça kütüphanesi okunamadı ya da zemin türleri yok: " + AtolyeKurucu.KutuphaneYol +
                                        "\nRepo kökünde: python3 Tools/Blender/pastel_parcalar.py --json", MessageType.Error);
                if (GUILayout.Button("Yeniden oku")) { AtolyeKurucu.Yukle(); ListeleriKur(); HepsiniYenile(); }
                EditorGUILayout.EndScrollView();
                return;
            }
            Duzelt();

            EditorGUILayout.LabelField("Sahne", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            string ad = EditorGUILayout.DelayedTextField("Ad", sahne.ad);
            int tohum = EditorGUILayout.IntField("Tohum (varyant)", sahne.tohum);
            if (EditorGUI.EndChangeCheck()) { sahne.ad = AtolyeKurucu.TemizAd(ad); sahne.tohum = tohum; ZeminiYenile(); }
            EditorGUILayout.LabelField($"{sahne.n} × {sahne.n} hücre · {sahne.zemin.Count} boyalı hücre · {sahne.binalar.Count} bina · {sahne.susler.Count} süs · zemin {ucgen} üçgen", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Yeni")) { GeriKaydet(); sahne = new SahneTasarimi(); HepsiniYenile(); }
                using (new EditorGUI.DisabledScope(geri.Count == 0))
                    if (GUILayout.Button("Geri al (" + geri.Count + ")")) GeriAl();
                if (GUILayout.Button("Kamerayı ortala")) KamerayiOdakla();
            }
            if (sahneAd.Length > 0)
                using (new EditorGUILayout.HorizontalScope())
                {
                    sahneSira = EditorGUILayout.Popup(sahneSira, sahneAd);
                    if (GUILayout.Button("Aç", GUILayout.Width(60f))) SahneAc(sahneAd[sahneSira]);
                }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Fırça", EditorStyles.boldLabel);
            mod = GUILayout.Toolbar(mod, ModAd);
            if (mod == ModZemin)
            {
                zeminSira = GUILayout.SelectionGrid(zeminSira, zeminEtiket, 2);
                kademe = EditorGUILayout.IntPopup("Yükselti kademesi", kademe, KademeAd, new[] { 0, 1, 2 });
                yalnizKademe = EditorGUILayout.ToggleLeft("Yalnız kademeyi değiştir (zemin türü kalsın)", yalnizKademe);
                EditorGUILayout.HelpBox("Sol tık / sürükle: boya · sağ tık ya da Shift: çime döndür.\nYola şerit, yaya geçidi, kaldırım ve bordür; suya taş rıhtım; " +
                                        "kademe kenarına yamaç (çim, patika) ya da istinat duvarı (yol, kaldırım, meydan) kendiliğinden gelir.", MessageType.None);
            }
            else if (mod == ModBina)
            {
                if (binaAd.Length == 0) EditorGUILayout.HelpBox("Kayıtlı tasarım yok. Önce Bina Atölyesi'nde bir bina kaydet.", MessageType.Warning);
                else
                {
                    binaSira = EditorGUILayout.Popup("Tasarım", binaSira, binaAd);
                    binaDon = EditorGUILayout.IntPopup("Ön yüz", binaDon, new[] { "öne (y = 0)", "sağa", "arkaya", "sola" }, new[] { 0, 1, 2, 3 });
                    EditorGUILayout.HelpBox("Sol tık: binayı koy (yeşil kare 5 × 5 arsası, çizgi ön yüzü) · R: döndür · Shift + tık: en yakın binayı sil.\n" +
                                            "Bina, orta hücresinin kademesine oturur. Yeni bina türü / tabelası için Bina Atölyesi'nde tasarla ve kaydet.", MessageType.None);
                }
                if (GUILayout.Button("Bina Atölyesi'ni aç")) EditorApplication.ExecuteMenuItem("Kurye Tycoon/Bina Atölyesi");
                if (GUILayout.Button("Tasarım listesini yenile")) ListeleriKur();
            }
            else
            {
                if (susAd.Length > 0)
                {
                    susSira = EditorGUILayout.Popup("Model", susSira, susEtiket);
                    susRenk = EditorGUILayout.IntSlider("Renk sırası", susRenk, 0, 5);
                    susAci = EditorGUILayout.Slider("Açı", susAci, 0f, 360f);
                    susOlcek = EditorGUILayout.Slider("Ölçek", susOlcek, 0.4f, 2.5f);
                }
                EditorGUILayout.HelpBox("Sol tık: koy · Shift + tık: en yakını sil · [ ve ]: döndür. Süsler zemin kotunda (kademe 0) durur.", MessageType.None);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Kaydet", EditorStyles.boldLabel);
            if (GUILayout.Button("Sahneyi kaydet (sahne_" + sahne.ad + ".json)", GUILayout.Height(28f)))
            {
                Directory.CreateDirectory(SahneKlasorTam);
                File.WriteAllText(Path.Combine(SahneKlasorTam, "sahne_" + sahne.ad + ".json"), JsonUtility.ToJson(sahne));
                AssetDatabase.Refresh();
                ListeleriKur();
                EditorUtility.DisplayDialog("Sahne Atölyesi", "Kaydedildi:\n" + SahneKlasor + "/sahne_" + sahne.ad + ".json", "Tamam");
            }
            EditorGUILayout.HelpBox("Sahne yalnız JSON'dur (zemin hücreleri, bina yerleri, süsler): model dosyası üretilmez.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        void SahneAc(string ad)
        {
            string yol = Path.Combine(SahneKlasorTam, "sahne_" + ad + ".json");
            if (!File.Exists(yol)) return;
            GeriKaydet();
            try { sahne = JsonUtility.FromJson<SahneTasarimi>(File.ReadAllText(yol)); }
            catch (System.Exception ex) { Debug.LogError("Sahne Atölyesi: okunamadı: " + ex.Message); return; }
            Duzelt();
            HepsiniYenile();
            KamerayiOdakla();
        }
    }
}
