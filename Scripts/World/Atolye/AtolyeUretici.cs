using System.Collections.Generic;
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
    /// </summary>
    public static class AtolyeUretici
    {
        const float LodGecis = 0.08f;      // BlenderBinaKurulum ile aynı
        // Şablondaki çocuk sırası (adlar "<ad>_<grup>"): CatiKaldir "_Cati" ve "_Ic" ile biten çocukları arar
        static readonly string[] GrupSirasi = { "Govde", "Duvar_On", "Duvar_Sag", "Duvar_Arka", "Duvar_Sol", "Cati", "Ic", "Ampul", "Poset", "Kepenk" };

        static Transform kok;
        static List<GameObject> binalar, agaclar, donatilar;
        static readonly Dictionary<GameObject, BinaTasarimi> bekleyen = new Dictionary<GameObject, BinaTasarimi>();
        static double toplamMs;
        static int kurulan, ucgen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OyunBasi()
        {
            // Alan yeniden yüklenmese de (Enter Play Mode ayarı) eski oyunun yok edilmiş şablonları kullanılmasın
            kok = null; binalar = null; agaclar = null; donatilar = null;
            bekleyen.Clear();
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
            foreach (var d in dosyalar)
            {
                var t = AtolyeKurucu.TasarimOku(d.text);
                if (t == null) continue;
                if (t.bloklar.Count == 0 && t.susler.Count == 0) { Yok(t); continue; }
                string ad = AtolyeKurucu.ModelAdi(t);
                if (!adlar.Add(ad))
                {
                    Debug.LogWarning("Kurye Tycoon · atölye: aynı adlı iki tasarım (" + ad + "), ikincisi atlandı: " + d.name);
                    Yok(t);
                    continue;
                }
                var go = Sablon(ad);
                bekleyen[go] = t;
                binalar.Add(go);
            }
            return binalar;
        }

        /// <summary>Şablonun mesh'leri kurulmadıysa kurar (atölye şablonu değilse hiçbir şey yapmaz). City3D kullanmadan önce çağırır.</summary>
        public static void Hazirla(GameObject sablon)
        {
            if (sablon == null || bekleyen.Count == 0) return;
            if (!bekleyen.TryGetValue(sablon, out var t)) return;
            bekleyen.Remove(sablon);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try { Doldur(sablon, t, true); }
            catch (System.Exception ex) { Debug.LogError("Kurye Tycoon · atölye: '" + sablon.name + "' kurulamadı: " + ex); }
            sw.Stop();
            toplamMs += sw.Elapsed.TotalMilliseconds;
            kurulan++;
            Yok(t);
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

        static void CevreKur()
        {
            if (agaclar != null && donatilar != null && (agaclar.Count == 0 || agaclar[0] != null) && (donatilar.Count == 0 || donatilar[0] != null)) return;
            agaclar = new List<GameObject>();
            donatilar = new List<GameObject>();
            var kt = AtolyeKurucu.Kut;
            if (kt == null) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var sayac = new Dictionary<string, int>();
            int n = 0;
            foreach (var p in kt.parcalar)
            {
                if ((p.grup != "agac" && p.grup != "cevre") || string.IsNullOrEmpty(p.hedef)) continue;
                string ad;
                bool lod, agac = false;
                if (p.hedef == "agac") { ad = "Agac_atolye_" + p.ad.Replace("agac_", ""); lod = true; agac = true; }
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
                var go = Sablon(ad);
                try { Doldur(go, t, lod); }
                catch (System.Exception ex) { Debug.LogError("Kurye Tycoon · atölye: '" + ad + "' kurulamadı: " + ex); }
                Yok(t);
                (agac ? agaclar : donatilar).Add(go);
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
            string ad = kokGo.name;
            if (lod)
            {
                var kur = AtolyeKurucu.KurGruplu(t);
                if (kur != null && kur.plan != null && kur.plan.hucreler.Count > 0 && kur.mesh.ContainsKey("Cati") && kur.mesh.ContainsKey("Ic"))
                {
                    GrupluDoldur(kokGo, t, kur);
                    return;
                }
                if (kur != null)
                    foreach (var m in kur.mesh.Values) Yok(m);
            }
            var mesh = AtolyeKurucu.Kur(t, false, out var adlar);
            if (mesh == null) return;
            mesh.name = ad;
            Say(mesh);
            List<string> hafifAdlar = null;
            var hafif = lod ? AtolyeKurucu.Kur(t, true, out hafifAdlar) : null;
            if (hafif != null)
            {
                hafif.name = ad + "_Hafif";
                var l0 = Parca(kokGo.transform, "LOD0", mesh, AtolyeKurucu.Malzemeler(adlar));
                var l1 = Parca(kokGo.transform, "LOD1", hafif, AtolyeKurucu.Malzemeler(hafifAdlar));
                var grup = kokGo.AddComponent<LODGroup>();
                grup.SetLODs(new[] { new LOD(LodGecis, new Renderer[] { l0 }), new LOD(0.0005f, new Renderer[] { l1 }) });
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
        static void GrupluDoldur(GameObject kokGo, BinaTasarimi t, AtolyeKurucu.Kurulum kur)
        {
            string ad = kokGo.name;
            var hafif = AtolyeKurucu.Kur(t, true, out var hafifAdlar);
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
