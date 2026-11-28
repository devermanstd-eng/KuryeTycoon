using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Bina Atölyesi modellerini OYUN ÇALIŞIRKEN kurar: depoda mesh ya da prefab dosyası yoktur, yalnızca tasarım JSON'ları
    /// (Resources/Atolye/Tasarimlar) ve parça kütüphanesi (Resources/Atolye/pastel_parcalar.json) vardır.
    ///  • Her tasarım ve her çevre parçası için bir "şablon" GameObject tutulur (kapalı bir kökün altında, sahnede görünmez).
    ///    City3D ve SokakDonatilari şablonları prefab gibi kullanır (ada bakıp havuza koyar, Instantiate eder): kopyalar aynı
    ///    mesh'leri paylaşır, yani her tasarım bir kez kurulur.
    ///  • Bina şablonları başta boştur (yalnız ad); mesh'ler ilk kullanılacağı an kurulur (<see cref="Hazirla"/>): şehirde
    ///    yer almayan tasarımın maliyeti olmaz. Çevre modelleri (ağaç, çalı, donatı) küçüktür, istenince hemen kurulur.
    ///  • Şablonun yapısı eski prefabla aynıdır: kök [LODGroup + AtolyePlani] → LOD0 (çocukları &lt;ad&gt;_Govde, _Duvar_*, _Cati,
    ///    _Ic [altında _Ampul, _Poset], _Kepenk) + LOD1; içi olmayan modelde LOD0 / LOD1 tek mesh. CatiKaldir, DukkanCanli ve
    ///    GeceIsiklari değişmeden çalışır.
    ///  • Süre ölçülür: <see cref="SureyiYaz"/> şehir kurulunca toplamı Console'a yazar.
    ///  • u72: bina mesh'lerinin hesabı (yerleşim, yüzeyler, kalem çizgileri) <see cref="ArkaPlandaHazirla"/> ile arka plan iş
    ///    parçacığında yapılır; ana iş parçacığında (<see cref="Hazirla"/>) yalnız Mesh ve GameObject kurulur. Eskiden bir model
    ///    30–80 ms ana iş parçacığını tutuyordu, şehir kurulurken oyun 10–15 FPS'e düşüyordu.
    /// </summary>
    public static class AtolyeUretici
    {
        const float LodGecis = 0.08f;      // BlenderBinaKurulum ile aynı
        const float UzakGecis = 0.025f;    // ağaç / çalı: bu ekran payının altında en uzak görünüm (LOD2)
        // Şablondaki çocuk sırası (adlar "<ad>_<grup>"): CatiKaldir "_Cati" ve "_Ic" ile biten çocukları arar
        static readonly string[] GrupSirasi = { "Govde", "Duvar_On", "Duvar_Sag", "Duvar_Arka", "Duvar_Sol", "Cati", "Ic", "Ampul", "Poset", "Kepenk" };

        static Transform kok;
        static List<GameObject> binalar, agaclar, donatilar;
        static Dictionary<string, List<GameObject>> nehir;      // tür ("tekne", "ordek", "kugu", "sazlik" ...) → şablonlar
        static readonly Dictionary<GameObject, BinaTasarimi> bekleyen = new Dictionary<GameObject, BinaTasarimi>();
        // arka planda hesaplanan ham mesh verileri (yalnız ana iş parçacığı okur / yazar; görevin içi bu sözlüğe dokunmaz)
        static readonly Dictionary<GameObject, Task<AtolyeKurucu.HamBina>> hazirlik = new Dictionary<GameObject, Task<AtolyeKurucu.HamBina>>();
        /// <summary>Arka plan iş parçacığı kullanılabilir mi (WebGL'de yok).</summary>
        static bool IsParcacigiVar => Application.platform != RuntimePlatform.WebGLPlayer && SystemInfo.processorCount > 1;
        static double toplamMs;
        static int kurulan, ucgen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OyunBasi()
        {
            // Alan yeniden yüklenmese de (Enter Play Mode ayarı) eski oyunun yok edilmiş şablonları kullanılmasın
            kok = null; binalar = null; agaclar = null; donatilar = null; nehir = null;
            bekleyen.Clear();
            hazirlik.Clear();     // süren görevler biter, sonuçları kullanılmaz
            toplamMs = 0; kurulan = 0; ucgen = 0;
        }

        /// <summary>Şablonları atar: tasarım ya da parça kütüphanesi değiştiyse sonraki istekte yeniden kurulur (editör).</summary>
        public static void Sifirla()
        {
            if (kok != null)
            {
                if (Application.isPlaying) Object.Destroy(kok.gameObject);
                else Object.DestroyImmediate(kok.gameObject);
            }
            OyunBasi();
        }

        static Transform Kok()
        {
            if (kok != null) return kok;
            var go = new GameObject("Atölye şablonları");
            go.SetActive(false);                       // çocuklar kendi başına açık ama sahnede görünmez; kopyaları açık doğar
            if (Application.isPlaying) Object.DontDestroyOnLoad(go);
            else go.hideFlags = HideFlags.HideAndDontSave;
            kok = go.transform;
            return kok;
        }

        static GameObject Sablon(string ad)
        {
            var go = new GameObject(ad);
            go.transform.SetParent(Kok(), false);
            return go;
        }

        // ---------------------------------------------------------------- binalar

        /// <summary>
        /// Bütün tasarımların şablonları (adları &lt;önek&gt;_atolye_&lt;ad&gt;). Mesh'leri henüz kurulmamış olabilir: kullanmadan önce
        /// <see cref="Hazirla"/>. Kütüphane ya da tasarım yoksa boş liste.
        /// </summary>
        public static List<GameObject> Binalar()
        {
            if (binalar != null && (binalar.Count == 0 || binalar[0] != null)) return binalar;
            binalar = new List<GameObject>();
            if (AtolyeKurucu.Kut == null) return binalar;
            var dosyalar = Resources.LoadAll<TextAsset>(AtolyeKurucu.TasarimKaynak);
            System.Array.Sort(dosyalar, (p, q) => string.CompareOrdinal(p.name, q.name));
            var adlar = new HashSet<string>();
            // Bir dosyada tek tasarım ya da paket ({"tasarimlar": ["<tasarım JSON'u>", ...]}: Tools/Blender/sehir_tasarimlari.py)
            var metinler = new List<(string metin, string kaynak)>();
            foreach (var d in dosyalar)
            {
                if (d.text.StartsWith("{\"tasarimlar\""))
                {
                    var paket = JsonUtility.FromJson<TasarimPaketi>(d.text);
                    if (paket?.tasarimlar != null) foreach (var m in paket.tasarimlar) metinler.Add((m, d.name));
                }
                else metinler.Add((d.text, d.name));
            }
            foreach (var (metin, kaynak) in metinler)
            {
                var t = AtolyeKurucu.TasarimOku(metin);
                if (t == null) continue;
                if (t.bloklar.Count == 0 && t.susler.Count == 0) { Yok(t); continue; }
                string ad = AtolyeKurucu.ModelAdi(t);
                if (!adlar.Add(ad))
                {
                    Debug.LogWarning("Kurye Tycoon · atölye: aynı adlı iki tasarım (" + ad + "), ikincisi atlandı: " + kaynak);
                    Yok(t);
                    continue;
                }
                var go = Sablon(ad);
                bekleyen[go] = t;
                binalar.Add(go);
            }
            return binalar;
        }

        [System.Serializable] class TasarimPaketi { public string[] tasarimlar; }

        /// <summary>Şablonun mesh'leri kurulmadıysa kurar (atölye şablonu değilse hiçbir şey yapmaz). City3D kullanmadan önce çağırır.</summary>
        public static void Hazirla(GameObject sablon)
        {
            if (sablon == null || bekleyen.Count == 0) return;
            if (!bekleyen.TryGetValue(sablon, out var t)) return;
            bekleyen.Remove(sablon);
            AtolyeKurucu.HamBina ham = null;
            if (hazirlik.TryGetValue(sablon, out var gorev))
            {
                hazirlik.Remove(sablon);
                // bitmediyse burada beklenir (City3D yalnız bitenleri ister); hata verdiyse aşağıda aynı iş ana iş parçacığında denenir
                try { ham = gorev.Result; }
                catch (System.Exception ex) { Debug.LogWarning("Kurye Tycoon · atölye: '" + sablon.name + "' arka planda kurulamadı, yeniden deneniyor: " + ex.GetBaseException().Message); }
            }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (ham != null) DoldurHam(sablon, ham, true);
                else Doldur(sablon, t, true);
            }
            catch (System.Exception ex) { Debug.LogError("Kurye Tycoon · atölye: '" + sablon.name + "' kurulamadı: " + ex); }
            sw.Stop();
            toplamMs += sw.Elapsed.TotalMilliseconds;
            kurulan++;
            Yok(t);
        }

        /// <summary>Verilen tasarımdan hemen bir şablon kurar (ör. oyuncunun merkez ofisi: adı ve rengi oyuna göre). Kurulamazsa null.</summary>
        public static GameObject TasarimdanKur(BinaTasarimi t)
        {
            if (t == null || AtolyeKurucu.Kut == null) return null;
            var go = Sablon(AtolyeKurucu.ModelAdi(t));
            try { Doldur(go, t, true); }
            catch (System.Exception ex) { Debug.LogError("Kurye Tycoon · atölye: '" + go.name + "' kurulamadı: " + ex); Yok(go); return null; }
            return go;
        }

        /// <summary>
        /// Şablonun ham mesh verisini arka plan iş parçacığında hesaplamaya başlar (başlamışsa bir şey yapmaz). true: şablon
        /// <see cref="Hazirla"/> için hazır (hesap bitti, zaten kurulu ya da atölye şablonu değil); false: hesap sürüyor.
        /// </summary>
        public static bool ArkaPlandaHazirla(GameObject sablon)
        {
            if (sablon == null || !bekleyen.TryGetValue(sablon, out var t)) return true;
            if (hazirlik.TryGetValue(sablon, out var gorev)) return gorev.IsCompleted;
            if (!IsParcacigiVar || AtolyeKurucu.Kut == null) return true;   // Hazirla eski yoldan kurar
            hazirlik[sablon] = Task.Run(() => AtolyeKurucu.HamKur(t, true));
            return false;
        }

        /// <summary>Şablon <see cref="Hazirla"/> için hazır mı (yeni hesap başlatmaz).</summary>
        public static bool HazirMi(GameObject sablon)
        {
            if (sablon == null || !bekleyen.ContainsKey(sablon)) return true;
            return hazirlik.TryGetValue(sablon, out var gorev) ? gorev.IsCompleted : !IsParcacigiVar;
        }

        /// <summary>Arka planda hesabı başlamış, henüz bitmemiş şablon sayısı.</summary>
        public static int SurenHazirlik
        {
            get { int n = 0; foreach (var g in hazirlik.Values) if (!g.IsCompleted) n++; return n; }
        }

        /// <summary>Şablonun mesh'leri henüz kurulmadı mı (City3D arka planda kurar, o sırada yerinde kutu durur).</summary>
        public static bool Kurulmadi(GameObject sablon) => sablon != null && bekleyen.ContainsKey(sablon);

        /// <summary>Kurulmamış şablonun tasarımındaki kat sayısı (yer tutucu kutunun boyu için; bilinmiyorsa 2).</summary>
        public static int KatSayisi(GameObject sablon)
        {
            if (sablon == null || !bekleyen.TryGetValue(sablon, out var t) || t.bloklar == null || t.bloklar.Count == 0) return 2;
            int k = 0;
            foreach (var b in t.bloklar) k = Mathf.Max(k, b.k + 1);
            return k;
        }

        /// <summary>Şehir kurulunca: kaç model kuruldu, ne kadar sürdü (Console; hata raporunda da görünür).</summary>
        public static void SureyiYaz()
        {
            if (kurulan == 0) return;
            Debug.Log($"Kurye Tycoon · atölye: {kurulan} model oyun çalışırken kuruldu, toplam {toplamMs:0} ms " +
                      $"(model başına ortalama {toplamMs / kurulan:0.0} ms, {ucgen} üçgen; {bekleyen.Count} tasarım kullanılmadı)");
            toplamMs = 0; kurulan = 0; ucgen = 0;
        }

        // ---------------------------------------------------------------- çevre modelleri (ağaç, çalı, donatı)

        /// <summary>Ağaç ve çalı şablonları (Agac_atolye_*, Cali_atolye_*): parça kütüphanesinde hedefi "agac" / "cali" olanlar.</summary>
        public static List<GameObject> Agaclar()
        {
            CevreKur();
            return agaclar;
        }

        /// <summary>Sokak donatısı şablonları (Donati_&lt;tür&gt;_atolye_&lt;n&gt;): hedefi "donati:&lt;tür&gt;" olan parçalar.</summary>
        public static List<GameObject> Donatilar()
        {
            CevreKur();
            return donatilar;
        }

        /// <summary>
        /// Nehir öğesi şablonları (Nehir_&lt;tür&gt;_atolye_&lt;ad&gt;): hedefi "nehir:&lt;tür&gt;" olan parçalar (tekne, ordek, yavru, kugu,
        /// sazlik, nilufer). Model ekseni: tekneler +x'e bakar, su hattı z = 0 (Unity mesh'inde −X ileri, Y yukarı).
        /// Tür yoksa boş liste.
        /// </summary>
        public static List<GameObject> Nehir(string tur)
        {
            CevreKur();
            return nehir != null && nehir.TryGetValue(tur, out var l) ? l : new List<GameObject>();
        }

        static void CevreKur()
        {
            if (agaclar != null && donatilar != null && (agaclar.Count == 0 || agaclar[0] != null) && (donatilar.Count == 0 || donatilar[0] != null)) return;
            agaclar = new List<GameObject>();
            donatilar = new List<GameObject>();
            nehir = new Dictionary<string, List<GameObject>>();
            var kt = AtolyeKurucu.Kut;
            if (kt == null) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var sayac = new Dictionary<string, int>();
            int n = 0;
            // u72: önce bütün parçaların tasarımı (ana iş parçacığı), sonra mesh hesapları bütün çekirdeklerde paralel,
            // en son Mesh / GameObject kurulumu yine ana iş parçacığında
            var isler = new List<(string ad, bool lod, bool agac, string nehirTur, BinaTasarimi t)>();
            foreach (var p in kt.parcalar)
            {
                if ((p.grup != "agac" && p.grup != "cevre") || string.IsNullOrEmpty(p.hedef)) continue;
                string ad;
                bool lod, agac = false;
                string nehirTur = null;
                if (p.hedef.StartsWith("nehir:")) { nehirTur = p.hedef.Substring(6); ad = "Nehir_" + nehirTur + "_atolye_" + p.ad; lod = false; }
                else if (p.hedef == "agac") { ad = "Agac_atolye_" + p.ad.Replace("agac_", ""); lod = true; agac = true; }
                else if (p.hedef == "cali") { ad = "Cali_atolye_" + p.ad.Replace("cali_", ""); lod = true; agac = true; }
                else
                {
                    string tur = p.hedef.StartsWith("donati:") ? p.hedef.Substring(7) : p.ad;
                    sayac.TryGetValue(tur, out int sira);
                    sayac[tur] = ++sira;
                    ad = "Donati_" + tur + "_atolye_" + sira;
                    lod = false;
                }
                var t = ScriptableObject.CreateInstance<BinaTasarimi>();
                t.hideFlags = HideFlags.HideAndDontSave;
                t.ad = p.ad;
                t.susler.Add(new BinaTasarimi.Sus { ad = p.ad, olcek = 1f, renk = 0 });
                isler.Add((ad, lod, agac, nehirTur, t));
            }
            var hamlar = new AtolyeKurucu.HamBina[isler.Count];
            if (IsParcacigiVar)
            {
                try { Parallel.For(0, isler.Count, i => { hamlar[i] = AtolyeKurucu.HamKur(isler[i].t, isler[i].lod); }); }
                catch (System.Exception ex) { Debug.LogWarning("Kurye Tycoon · atölye: çevre modelleri paralel kurulamadı, sırayla kuruluyor: " + ex.GetBaseException().Message); }
            }
            for (int i = 0; i < isler.Count; i++)
            {
                var (ad, lod, agac, nehirTur, t) = isler[i];
                var go = Sablon(ad);
                try
                {
                    if (hamlar[i] != null) DoldurHam(go, hamlar[i], lod);
                    else Doldur(go, t, lod);
                }
                catch (System.Exception ex) { Debug.LogError("Kurye Tycoon · atölye: '" + ad + "' kurulamadı: " + ex); }
                Yok(t);
                if (nehirTur != null)
                {
                    if (!nehir.TryGetValue(nehirTur, out var nl)) nehir[nehirTur] = nl = new List<GameObject>();
                    nl.Add(go);
                }
                else (agac ? agaclar : donatilar).Add(go);
                n++;
            }
            sw.Stop();
            toplamMs += sw.Elapsed.TotalMilliseconds;
            kurulan += n;
        }

        // ---------------------------------------------------------------- sahne (zemin + binalar)

        /// <summary>Tasarım adı → JSON metni (Resources/Atolye/Tasarimlar/atolye_&lt;ad&gt;). Bulunamazsa null.</summary>
        public static string TasarimJson(string ad)
        {
            if (string.IsNullOrEmpty(ad)) return null;
            var ta = Resources.Load<TextAsset>(AtolyeKurucu.TasarimKaynak + "/atolye_" + ad);
            return ta != null ? ta.text : null;
        }

        /// <summary>
        /// Sahneyi kurar: "Zemin" çocuğu (zemin + süsler, tek mesh) ve her bina için bir çocuk (şablonla aynı yapı; hücresine
        /// taşınmış, 90°'lik dönüşüyle). Atölye penceresi önizleme için çağırır; dosya üretmez. Kurulamazsa null.
        /// </summary>
        public static GameObject SahneKur(SahneTasarimi sh, Transform ust = null)
        {
            if (sh == null || AtolyeKurucu.Kut == null) return null;
            var kokGo = new GameObject("Sahne_" + AtolyeKurucu.TemizAd(sh.ad));
            if (ust != null) kokGo.transform.SetParent(ust, false);
            var zemin = AtolyeKurucu.ZeminKur(sh, out var adlar);
            if (zemin != null) Parca(kokGo.transform, "Zemin", zemin, AtolyeKurucu.Malzemeler(adlar));
            if (sh.binalar != null)
                foreach (var b in sh.binalar) BinaKur(sh, b, kokGo.transform);
            return kokGo;
        }

        /// <summary>Sahnedeki tek bir binayı kurup hücresine yerleştirir (tasarım bulunamazsa null).</summary>
        public static GameObject BinaKur(SahneTasarimi sh, SahneTasarimi.Bina b, Transform ust)
        {
            var t = AtolyeKurucu.TasarimOku(TasarimJson(b.tasarim));
            if (t == null) { Debug.LogWarning("Kurye Tycoon · sahne: tasarım bulunamadı: " + b.tasarim); return null; }
            var go = new GameObject(AtolyeKurucu.ModelAdi(t));
            go.transform.SetParent(ust, false);
            try { Doldur(go, t, true); }
            catch (System.Exception ex) { Debug.LogError("Kurye Tycoon · sahne: '" + b.tasarim + "' kurulamadı: " + ex); }
            Yok(t);
            Vector2 c = AtolyeKurucu.SahneMerkezi(sh.n, b.x, b.y);
            float z = sh.Kademe(b.x, b.y) * AtolyeKurucu.Kademe + AtolyeKurucu.KaldirimZ;
            go.transform.localPosition = AtolyePlani.Yerel(new Vector3(c.x, c.y, z));
            // model ekseninde saat yönünün tersine 90° = Unity'de Y çevresinde −90° (eksen çevirisi x'i ters çevirir)
            go.transform.localRotation = Quaternion.Euler(0f, -90f * (((b.don % 4) + 4) % 4), 0f);
            // önizleme: kapalı dükkânın kepengi inik
            var plan = go.GetComponent<AtolyePlani>();
            if (b.kapali && plan != null && plan.kepenk != null) plan.kepenk.gameObject.SetActive(true);
            return go;
        }

        // ---------------------------------------------------------------- şablonu doldurma

        static MeshRenderer Parca(Transform ust, string ad, Mesh mesh, Material[] malzemeler)
        {
            var go = new GameObject(ad);
            go.transform.SetParent(ust, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = malzemeler;
            return mr;
        }

        static void Yok(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        /// <summary>LODGroup'un sınırlarını mesh sınırlarından verir (RecalculateBounds kapalı nesnede yanlış ölçebilir).</summary>
        static void Sinir(LODGroup g, Bounds b)
        {
            g.localReferencePoint = b.center;
            g.size = Mathf.Max(0.5f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
        }

        static void Say(Mesh m)
        {
            if (m == null) return;
            for (int i = 0; i < m.subMeshCount; i++) ucgen += (int)(m.GetIndexCount(i) / 3);
        }

        /// <summary>
        /// Boş kökü tasarımın modeliyle doldurur. lod: uzak görünüm (LOD1) de kurulur; içi olan binalar parçalı kurulur
        /// (çatı kalkar, duvarlar iner, kepenk sarılır). Atölye penceresi de (oyun dışı) bunu çağırabilir.
        /// </summary>
        public static void Doldur(GameObject kokGo, BinaTasarimi t, bool lod)
        {
            DoldurHam(kokGo, AtolyeKurucu.HamKur(t, lod), lod);
        }

        /// <summary>Hazır ham veriden (<see cref="AtolyeKurucu.HamKur"/>) şablonun mesh'lerini ve LOD'larını kurar (ana iş parçacığı).</summary>
        static void DoldurHam(GameObject kokGo, AtolyeKurucu.HamBina h, bool lod)
        {
            string ad = kokGo.name;
            if (h.gruplu)
            {
                var kur = new AtolyeKurucu.Kurulum { plan = h.plan, tur = h.tur };
                foreach (var kv in h.grup)
                {
                    var m = AtolyeKurucu.MeshOlustur(kv.Value, out var gAdlar);
                    if (m == null) continue;
                    kur.mesh[kv.Key] = m;
                    kur.malzeme[kv.Key] = gAdlar;
                }
                var gHafif = AtolyeKurucu.MeshOlustur(h.hafif, out var gHafifAdlar);
                GrupluDoldur(kokGo, kur, gHafif, gHafifAdlar);
                return;
            }
            var mesh = AtolyeKurucu.MeshOlustur(h.tek, out var adlar);
            if (mesh == null) return;
            mesh.name = ad;
            Say(mesh);
            List<string> hafifAdlar = null;
            var hafif = lod ? AtolyeKurucu.MeshOlustur(h.hafif, out hafifAdlar) : null;
            if (hafif != null)
            {
                hafif.name = ad + "_Hafif";
                var l0 = Parca(kokGo.transform, "LOD0", mesh, AtolyeKurucu.Malzemeler(adlar));
                var l1 = Parca(kokGo.transform, "LOD1", hafif, AtolyeKurucu.Malzemeler(hafifAdlar));
                var grup = kokGo.AddComponent<LODGroup>();
                // ağaç ve çalılarda üçüncü düzey (LOD2: gövde + bir iki küme, en çok ~40 üçgen): şehirde çok sayıda ağaç var
                var uzakMesh = AtolyeKurucu.MeshOlustur(h.uzak, out var uzakAdlar);
                if (uzakMesh != null)
                {
                    uzakMesh.name = ad + "_Uzak";
                    var l2 = Parca(kokGo.transform, "LOD2", uzakMesh, AtolyeKurucu.Malzemeler(uzakAdlar));
                    grup.SetLODs(new[] { new LOD(LodGecis, new Renderer[] { l0 }), new LOD(UzakGecis, new Renderer[] { l1 }), new LOD(0.0005f, new Renderer[] { l2 }) });
                }
                else grup.SetLODs(new[] { new LOD(LodGecis, new Renderer[] { l0 }), new LOD(0.0005f, new Renderer[] { l1 }) });
                Sinir(grup, mesh.bounds);
            }
            else
            {
                kokGo.AddComponent<MeshFilter>().sharedMesh = mesh;
                kokGo.AddComponent<MeshRenderer>().sharedMaterials = AtolyeKurucu.Malzemeler(adlar);
            }
        }

        /// <summary>
        /// İçi olan bina: kök [LODGroup + AtolyePlani] → LOD0 (boş; çocukları &lt;ad&gt;_Govde, _Duvar_*, _Cati, _Ic [altında _Ampul,
        /// _Poset], _Kepenk) + LOD1 (tek mesh, içsiz). _Ic, _Kepenk ve _Poset kapalı durur: oyunda CatiKaldir ve DukkanCanli açar.
        /// </summary>
        static void GrupluDoldur(GameObject kokGo, AtolyeKurucu.Kurulum kur, Mesh hafif, List<string> hafifAdlar)
        {
            string ad = kokGo.name;
            var l0 = new GameObject("LOD0").transform;
            l0.SetParent(kokGo.transform, false);
            var plan = kokGo.AddComponent<AtolyePlani>();
            var tv = kur.tur;
            plan.tur = tv.ad; plan.turGrup = tv.grup; plan.etiket = tv.etiket;
            plan.siparis = tv.kepenk != 0;
            plan.calisan = tv.calisan;
            plan.hucre = AtolyeKurucu.Hucre;
            plan.kat = AtolyeKurucu.KatBoyu;
            plan.kok = l0;
            var cizilen = new List<Renderer>();
            var cocuk = new Dictionary<string, Transform>();
            foreach (string g in GrupSirasi)
            {
                if (!kur.mesh.TryGetValue(g, out var m) || m == null) continue;
                m.name = ad + "_" + g;
                Say(m);
                Transform ust = (g == "Ampul" || g == "Poset") && cocuk.ContainsKey("Ic") ? cocuk["Ic"] : l0;
                var mr = Parca(ust, ad + "_" + g, m, AtolyeKurucu.Malzemeler(kur.malzeme[g]));
                cocuk[g] = mr.transform;
                cizilen.Add(mr);
            }
            Transform Al(string g) => cocuk.TryGetValue(g, out var tr) ? tr : null;
            plan.cati = Al("Cati"); plan.ic = Al("Ic"); plan.kepenk = Al("Kepenk"); plan.poset = Al("Poset"); plan.ampul = Al("Ampul");
            plan.duvar = new Transform[4];
            for (int i = 0; i < 4; i++) plan.duvar[i] = Al(AtolyeKurucu.DuvarGrup[i]);
            if (plan.kepenk != null) plan.kepenk.localPosition = new Vector3(0f, AtolyeKurucu.KepenkKok, 0f);

            // iç yerleşim: hücre ortaları, geçitler, mobilya
            var ip = kur.plan;
            int n = ip.hucreler.Count;
            plan.hucreOrta = new Vector2[n]; plan.hucreNo = new Vector2Int[n]; plan.gecit = new bool[n]; plan.mobilya = new string[n];
            float orta = (AtolyeKurucu.Izgara - 1) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                var h = ip.hucreler[i];
                plan.hucreNo[i] = h;
                plan.hucreOrta[i] = new Vector2((h.x - orta) * plan.hucre, (h.y - orta) * plan.hucre);
                plan.gecit[i] = ip.gecit.Contains(h);
                plan.mobilya[i] = ip.mobilya.TryGetValue(h, out string mob) ? mob : "";
            }
            plan.kapiHucre = plan.HucreBul(ip.kapi);
            plan.kasaHucre = ip.kasaVar ? plan.HucreBul(ip.kasa) : -1;

            MeshRenderer l1 = null;
            if (hafif != null)
            {
                hafif.name = ad + "_Hafif";
                l1 = Parca(kokGo.transform, "LOD1", hafif, AtolyeKurucu.Malzemeler(hafifAdlar));
            }
            var lg = kokGo.AddComponent<LODGroup>();
            if (l1 != null) lg.SetLODs(new[] { new LOD(LodGecis, cizilen.ToArray()), new LOD(0.0005f, new Renderer[] { l1 }) });
            else lg.SetLODs(new[] { new LOD(0.0005f, cizilen.ToArray()) });
            // sınırlar mesh'lerden (şablon kapalı bir kökün altında: Renderer.bounds güvenilmez)
            var b = new Bounds();
            bool ilk = true;
            foreach (var kv in cocuk)
            {
                if (kv.Key == "Kepenk" || !kur.mesh.TryGetValue(kv.Key, out var gm) || gm == null) continue;
                if (ilk) { b = gm.bounds; ilk = false; } else b.Encapsulate(gm.bounds);
            }
            Sinir(lg, b);
            // oyunda açılanlar kapalı durur (bileşenler takılmazsa bina sıradan görünür)
            if (plan.ic != null) plan.ic.gameObject.SetActive(false);
            if (plan.kepenk != null) plan.kepenk.gameObject.SetActive(false);
            if (plan.poset != null) plan.poset.gameObject.SetActive(false);
        }
    }
}
