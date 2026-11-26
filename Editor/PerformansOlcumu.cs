using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// "Kurye Tycoon > Performans Ölçümü (oyun açıkken)": kare hızını neyin yediğini tek seferde ölçer. Oyun açıkken (Play)
    /// çalışır, ~75 saniye sürer; kamerayı oynatmadan bekle.
    ///  • Önce sayım: atölye binalarının üçgen sayısı (yakın / uzak görünüm, kalem çizgisi payı), mesh belleği, ağaç, donatı.
    ///  • Sonra aşamalar: her aşamada TEK bir şey kapatılır (gölge, ortam gölgesi, ekran efektleri, çözünürlük, atölye binaları,
    ///    ağaçlar, orman, donatılar, gece ışıkları, yaya ve trafik), 1,5 sn beklenir, 4 sn ölçülür, geri açılır. Tabana göre
    ///    kazanılan milisaniye neyin pahalı olduğunu gösterir. Sonda hepsi birden kapalı ve taban yeniden (kayma var mı).
    ///  • Ölçülen: kare süresi (ortalama, en kötü %5), ana iş parçacığı / çizim iş parçacığı / ekran kartı süresi (destekleniyorsa),
    ///    çizim çağrısı, SetPass, üçgen, gölge düşüren sayısı.
    /// Hiçbir dosyaya ve sahneye yazmaz (yalnız rapor): değiştirdiği her şeyi bitince ya da oyun durunca geri alır.
    /// Rapor: &lt;proje&gt;/Logs/KuryePerformans_*.txt ve Console.
    /// </summary>
    public static class PerformansOlcumu
    {
        const string MenuYolu = "Kurye Tycoon/Performans Ölçümü (oyun açıkken)";
        const double Oturma = 1.5, Olcme = 4.0;

        class Asama
        {
            public string ad;
            public System.Action ac, kapat;
            public System.Action her;      // her karede (kalite yeniden uygulanınca Gorsel3D kamera ayarını sıfırlar)
        }

        class Sonuc
        {
            public string ad;
            public int kare, zamanKare, seviye;
            public double ms, ms95, ana, render, gpu, anaRec;
            public double batch, setpass, cizim, ucgen, golge, gc;
        }

        static bool calisiyor;
        static List<Asama> asamalar;
        static int sira;
        static double asamaBas;
        static int sonKare;
        static readonly List<float> sureler = new List<float>();
        static Sonuc anlik;
        static readonly List<Sonuc> sonuclar = new List<Sonuc>();
        static StringBuilder rapor;
        static ProfilerRecorder rBatch, rSetPass, rCizim, rUcgen, rGolge, rAna, rGc;
        static readonly FrameTiming[] zaman = new FrameTiming[1];

        // Kapatılan şeylerin eski hâli (geri almak için)
        static readonly List<Renderer> binaR = new List<Renderer>(), cevreR = new List<Renderer>(), ormanR = new List<Renderer>(),
                                       donatiR = new List<Renderer>(), canliR = new List<Renderer>();
        static readonly List<Renderer> gizlenen = new List<Renderer>();
        static readonly List<Light> sonenIsik = new List<Light>();
        static int eskiLod = -1, eskiSsao = int.MinValue;
        static float eskiOlcek = -1f;
        static bool geceKapali;

        [MenuItem(MenuYolu, true)]
        static bool Gecerli() => EditorApplication.isPlaying && !calisiyor;

        [MenuItem(MenuYolu)]
        static void Baslat()
        {
            var city = City3D.I;
            if (city == null || city.Root == null)
            {
                EditorUtility.DisplayDialog("Performans Ölçümü", "Şehir henüz kurulmadı. Oyun açılıp şehir görününce tekrar dene.", "Tamam");
                return;
            }
            if (city.BekleyenModel > 0)
            {
                EditorUtility.DisplayDialog("Performans Ölçümü",
                    $"Şehir binaları hâlâ arka planda kuruluyor ({city.BekleyenModel} tasarım kaldı). Kurulum sırasında kare hızı " +
                    "zaten düşük olur; Console'da \"arka planda kuruldu\" yazısını görünce tekrar dene.", "Tamam");
                return;
            }
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;

            rapor = new StringBuilder();
            sonuclar.Clear();
            Sayim(city);
            AsamalariKur();

            rBatch = Kaydedici(ProfilerCategory.Render, "Batches Count");
            rSetPass = Kaydedici(ProfilerCategory.Render, "SetPass Calls Count");
            rCizim = Kaydedici(ProfilerCategory.Render, "Draw Calls Count");
            rUcgen = Kaydedici(ProfilerCategory.Render, "Triangles Count");
            rGolge = Kaydedici(ProfilerCategory.Render, "Shadow Casters Count");
            rAna = Kaydedici(ProfilerCategory.Internal, "Main Thread");
            rGc = Kaydedici(ProfilerCategory.Memory, "GC Allocated In Frame");

            calisiyor = true;
            sira = -1;
            EditorApplication.update += Adim;
            EditorApplication.playModeStateChanged += OyunDurumu;
            SonrakiAsama();
            Debug.Log($"Kurye Tycoon · performans ölçümü başladı: {asamalar.Count} aşama, ~{asamalar.Count * (Oturma + Olcme):0} sn. Kamerayı oynatma.");
        }

        static void Birak(ref ProfilerRecorder r)
        {
            if (r.Valid) r.Dispose();
            r = default;
        }

        static ProfilerRecorder Kaydedici(ProfilerCategory k, string ad)
        {
            try { return ProfilerRecorder.StartNew(k, ad); }
            catch { return default; }
        }

        // ---------------------------------------------------------------- aşamalar

        static void AsamalariKur()
        {
            asamalar = new List<Asama>
            {
                new Asama { ad = "Taban (şu anki ayar)" },
                new Asama { ad = "Güneş gölgesi kapalı", her = () => KameraVerisi(d => d.renderShadows = false), kapat = () => KameraVerisi(d => d.renderShadows = true) },
                new Asama { ad = "Ortam gölgesi (SSAO) kapalı", ac = SsaoKapat, kapat = SsaoAc },
                new Asama { ad = "Ekran efektleri kapalı (renk, ışıma, tilt-shift)", her = () => KameraVerisi(d => d.renderPostProcessing = false), kapat = () => KameraVerisi(d => d.renderPostProcessing = true) },
                new Asama { ad = "Çözünürlük yarıya (ekran kartı dolgu payı)", ac = () => Olcek(0.5f), kapat = OlcekGeri },
                new Asama { ad = "Atölye ve ağaçlar hep uzak görünümde (LOD1)", ac = () => { eskiLod = QualitySettings.maximumLODLevel; QualitySettings.maximumLODLevel = 1; }, kapat = LodGeri },
                new Asama { ad = $"Atölye binaları gizli ({binaR.Count} parça)", ac = () => Gizle(binaR), kapat = GizleGeri },
                new Asama { ad = $"Şehir ağaçları ve çalıları gizli ({cevreR.Count} parça)", ac = () => Gizle(cevreR), kapat = GizleGeri },
                new Asama { ad = $"Kır ormanı gizli ({ormanR.Count} parça)", ac = () => Gizle(ormanR), kapat = GizleGeri },
                new Asama { ad = $"Sokak donatıları gizli ({donatiR.Count} parça)", ac = () => Gizle(donatiR), kapat = GizleGeri },
                new Asama { ad = "Gece ışıkları betiği ve lamba ışıkları kapalı", ac = GeceKapat, kapat = GeceAc },
                new Asama { ad = $"Yayalar, kuryeler ve trafik gizli ({canliR.Count} parça)", ac = () => { CanlilariTopla(); Gizle(canliR); }, kapat = GizleGeri },
                new Asama
                {
                    ad = "Hepsi birden kapalı (yalnız zemin ve yollar)",
                    ac = () =>
                    {
                        SsaoKapat();
                        GeceKapat();
                        CanlilariTopla();
                        Gizle(binaR); Gizle(cevreR); Gizle(ormanR); Gizle(donatiR); Gizle(canliR);
                    },
                    her = () => KameraVerisi(d => { d.renderShadows = false; d.renderPostProcessing = false; }),
                    kapat = () =>
                    {
                        GizleGeri();
                        GeceAc();
                        SsaoAc();
                        KameraVerisi(d => { d.renderShadows = true; d.renderPostProcessing = true; });
                    }
                },
                new Asama { ad = "Taban (yeniden: kayma var mı)" },
            };
        }

        static void SonrakiAsama()
        {
            sira++;
            if (sira >= asamalar.Count) { Bitir(true); return; }
            var a = asamalar[sira];
            try { a.ac?.Invoke(); }
            catch (System.Exception ex) { Debug.LogWarning("Kurye Tycoon · performans ölçümü: '" + a.ad + "' açılamadı: " + ex.Message); }
            anlik = new Sonuc { ad = a.ad, seviye = GrafikKalitesi.Seviye };
            sureler.Clear();
            asamaBas = EditorApplication.timeSinceStartup;
        }

        static void Adim()
        {
            if (!calisiyor) return;
            if (!EditorApplication.isPlaying) { Bitir(false); return; }
            if (EditorApplication.isPaused) return;
            int kare = Time.frameCount;
            if (kare == sonKare) return;
            sonKare = kare;
            FrameTimingManager.CaptureFrameTimings();
            var a = asamalar[sira];
            if (a.her != null)
            {
                try { a.her(); }
                catch (System.Exception ex) { Debug.LogWarning("Kurye Tycoon · performans ölçümü: '" + a.ad + "': " + ex.Message); a.her = null; }
            }
            double gecen = EditorApplication.timeSinceStartup - asamaBas;
            if (gecen < Oturma) return;
            if (gecen < Oturma + Olcme) { Ornekle(); return; }
            AsamaBitir();
        }

        static void Ornekle()
        {
            var s = anlik;
            sureler.Add(Time.unscaledDeltaTime * 1000f);
            s.kare++;
            if (rBatch.Valid) s.batch += rBatch.LastValue;
            if (rSetPass.Valid) s.setpass += rSetPass.LastValue;
            if (rCizim.Valid) s.cizim += rCizim.LastValue;
            if (rUcgen.Valid) s.ucgen += rUcgen.LastValue;
            if (rGolge.Valid) s.golge += rGolge.LastValue;
            if (rGc.Valid) s.gc += rGc.LastValue;
            if (rAna.Valid) s.anaRec += rAna.LastValue / 1e6;     // ns → ms
            if (FrameTimingManager.GetLatestTimings(1, zaman) > 0 && zaman[0].cpuFrameTime > 0.0)
            {
                s.zamanKare++;
                s.ana += zaman[0].cpuMainThreadFrameTime;
                s.render += zaman[0].cpuRenderThreadFrameTime;
                s.gpu += zaman[0].gpuFrameTime;
            }
        }

        static void AsamaBitir()
        {
            var s = anlik;
            int n = Mathf.Max(1, s.kare);
            double toplam = 0;
            foreach (var d in sureler) toplam += d;
            s.ms = toplam / n;
            if (sureler.Count > 0)
            {
                sureler.Sort();
                s.ms95 = sureler[Mathf.Clamp(Mathf.CeilToInt(sureler.Count * 0.95f) - 1, 0, sureler.Count - 1)];
            }
            s.batch /= n; s.setpass /= n; s.cizim /= n; s.ucgen /= n; s.golge /= n; s.gc /= n; s.anaRec /= n;
            if (s.zamanKare > 0) { s.ana /= s.zamanKare; s.render /= s.zamanKare; s.gpu /= s.zamanKare; }
            sonuclar.Add(s);
            var a = asamalar[sira];
            try { a.kapat?.Invoke(); }
            catch (System.Exception ex) { Debug.LogWarning("Kurye Tycoon · performans ölçümü: '" + a.ad + "' geri alınamadı: " + ex.Message); }
            SonrakiAsama();
        }

        static void OyunDurumu(PlayModeStateChange d)
        {
            if (d == PlayModeStateChange.ExitingPlayMode && calisiyor) Bitir(false);
        }

        static void Bitir(bool tamam)
        {
            if (!calisiyor) return;
            calisiyor = false;
            EditorApplication.update -= Adim;
            EditorApplication.playModeStateChanged -= OyunDurumu;
            // Ne açık kaldıysa geri al
            try
            {
                GizleGeri();
                GeceAc();
                LodGeri();
                OlcekGeri();
                SsaoAc();
                if (EditorApplication.isPlaying) KameraVerisi(d => { d.renderShadows = true; d.renderPostProcessing = true; });
            }
            catch (System.Exception ex) { Debug.LogWarning("Kurye Tycoon · performans ölçümü: geri alma: " + ex.Message); }
            Birak(ref rBatch); Birak(ref rSetPass); Birak(ref rCizim); Birak(ref rUcgen); Birak(ref rGolge); Birak(ref rAna); Birak(ref rGc);
            if (!tamam)
            {
                Debug.LogWarning("Kurye Tycoon · performans ölçümü yarıda kaldı (oyun durdu); değiştirilen ayarlar geri alındı.");
                return;
            }
            RaporYaz();
        }

        // ---------------------------------------------------------------- aç / kapat

        static void KameraVerisi(System.Action<UniversalAdditionalCameraData> f)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var d = cam.GetUniversalAdditionalCameraData();
            if (d != null) f(d);
        }

        static void SsaoKapat()
        {
            if (eskiSsao == int.MinValue) eskiSsao = GrafikKalitesi.OrtamGolgesiSecimi;
            GrafikKalitesi.OrtamGolgesiSecimi = GrafikKalitesi.SecimKapali;
        }

        static void SsaoAc()
        {
            if (eskiSsao == int.MinValue) return;
            GrafikKalitesi.OrtamGolgesiSecimi = eskiSsao;     // kaliteyi yeniden uygular (gölge ve kamera ayarı da yerine gelir)
            eskiSsao = int.MinValue;
        }

        static UniversalRenderPipelineAsset Urp => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        static void Olcek(float carpan)
        {
            var a = Urp;
            if (a == null) return;
            if (eskiOlcek < 0f) eskiOlcek = a.renderScale;
            a.renderScale = Mathf.Max(0.1f, eskiOlcek * carpan);
        }

        static void OlcekGeri()
        {
            var a = Urp;
            if (a != null && eskiOlcek > 0f) a.renderScale = eskiOlcek;
            eskiOlcek = -1f;
        }

        static void LodGeri()
        {
            if (eskiLod >= 0) QualitySettings.maximumLODLevel = eskiLod;
            eskiLod = -1;
        }

        static void Gizle(List<Renderer> l)
        {
            foreach (var r in l)
                if (r != null && !r.forceRenderingOff) { r.forceRenderingOff = true; gizlenen.Add(r); }
        }

        static void GizleGeri()
        {
            foreach (var r in gizlenen) if (r != null) r.forceRenderingOff = false;
            gizlenen.Clear();
        }

        static void GeceKapat()
        {
            var g = GeceIsiklari.I;
            if (g != null && g.enabled) { g.enabled = false; geceKapali = true; }
            if (g != null)
                foreach (var l in g.GetComponentsInChildren<Light>())
                    if (l.enabled) { l.enabled = false; sonenIsik.Add(l); }
        }

        static void GeceAc()
        {
            foreach (var l in sonenIsik) if (l != null) l.enabled = true;
            sonenIsik.Clear();
            if (geceKapali && GeceIsiklari.I != null) GeceIsiklari.I.enabled = true;
            geceKapali = false;
        }

        /// <summary>Yayalar, kuryeler (iskeletli karakterler) ve trafik: sayıları oyun içinde değişir, aşamanın başında toplanır.</summary>
        static void CanlilariTopla()
        {
            canliR.Clear();
            foreach (var r in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None)) canliR.Add(r);
            var tr = Traffic.I;
            if (tr != null && tr.Kok != null) canliR.AddRange(tr.Kok.GetComponentsInChildren<Renderer>());
        }

        // ---------------------------------------------------------------- sayım

        static void Sayim(City3D city)
        {
            binaR.Clear(); cevreR.Clear(); ormanR.Clear(); donatiR.Clear(); cizgiPayi.Clear();
            var r = rapor;
            r.AppendLine("Kurye Tycoon · performans ölçümü · " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            r.AppendLine($"Kod: {DebugReport.CodeVersion}");
            r.AppendLine($"Unity {Application.unityVersion} · {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}) · bellek {SystemInfo.systemMemorySize} MB");
            var urp = Urp;
            r.AppendLine($"Ekran {Screen.width}×{Screen.height}, çözünürlük ölçeği {(urp != null ? urp.renderScale : 1f):0.00}, MSAA {(urp != null ? urp.msaaSampleCount : 0)}, " +
                         $"gölge uzaklığı {(urp != null ? urp.shadowDistance : 0f):0}, LOD çarpanı {QualitySettings.lodBias:0.00}");
            string[] kAd = { "Düşük", "Orta", "Yüksek" };
            r.AppendLine($"Grafik kalitesi: {(GrafikKalitesi.Ayar == GrafikKalitesi.Otomatik ? "Otomatik → " : "")}{kAd[Mathf.Clamp(GrafikKalitesi.Seviye, 0, 2)]}, " +
                         $"tilt-shift {GrafikKalitesi.TiltShiftModu}, ortam gölgesi {(GrafikKalitesi.OrtamGolgesi ? "açık" : "kapalı")}");
            int sahne = SceneView.sceneViews != null ? SceneView.sceneViews.Count : 0;
            r.AppendLine($"Açık Scene penceresi: {sahne}" + (sahne > 0 ? "  (görünürse şehri ikinci kez çizer: ölçümde kapalı olsun)" : ""));
            var cam = Camera.main;
            if (cam != null)
            {
                var p = cam.transform.position;
                float yer = Mathf.Abs(cam.transform.forward.z) > 1e-3f ? -p.z / cam.transform.forward.z : 0f;
                r.AppendLine($"Kamera: ({p.x:0.0}, {p.y:0.0}, {p.z:0.0}), görüş {cam.fieldOfView:0}°, yere uzaklık {yer:0.0}");
            }
            r.AppendLine($"Gün saati: {DayNight.HourF:0.0}, gece {DayNight.Night01:0.00}");
            r.AppendLine();

            // Atölye modelleri: kökte LODGroup, adı "<önek>_atolye_<ad>(Clone)". Üç düzeyli olanlar ağaç / çalı.
            var tekil0 = new HashSet<Mesh>();
            var tekil1 = new HashSet<Mesh>();
            long binaKopya = 0, cevreKopya = 0, kopyaUcgen0 = 0, kopyaUcgen1 = 0, cevreUcgen0 = 0;
            // Sahnenin tamamı (yalnız açık nesneler: kapalı "Atölye şablonları" kökü sayılmaz)
            foreach (var lg in Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
            {
                if (!lg.gameObject.name.Contains("_atolye_")) continue;
                var lods = lg.GetLODs();
                bool cevre = lg.lodCount >= 3;
                if (cevre) cevreKopya++; else binaKopya++;
                for (int i = 0; i < lods.Length; i++)
                    foreach (var rr in lods[i].renderers)
                    {
                        if (rr == null) continue;
                        (cevre ? cevreR : binaR).Add(rr);
                        var m = MeshAl(rr);
                        if (m == null) continue;
                        long u = Ucgen(m);
                        if (cevre) { if (i == 0) cevreUcgen0 += u; continue; }
                        if (i == 0)
                        {
                            kopyaUcgen0 += u;
                            if (tekil0.Add(m)) cizgiPayi[m] = CizgiHesapla(rr, m);
                        }
                        else if (i == 1) { kopyaUcgen1 += u; tekil1.Add(m); }
                    }
            }
            long ucgen0 = 0, cizgi0 = 0, ucgen1 = 0, bellek = 0, altMesh = 0;
            int okunur = 0;
            foreach (var m in tekil0)
            {
                ucgen0 += Ucgen(m);
                cizgi0 += cizgiPayi.TryGetValue(m, out long cu) ? cu : 0;
                altMesh += m.subMeshCount;
                bellek += Profiler.GetRuntimeMemorySizeLong(m);
                if (m.isReadable) okunur++;
            }
            foreach (var m in tekil1)
            {
                ucgen1 += Ucgen(m);
                bellek += Profiler.GetRuntimeMemorySizeLong(m);
                if (m.isReadable) okunur++;
            }
            r.AppendLine($"Atölye binaları: {binaKopya} kopya, {tekil0.Count} farklı yakın mesh (LOD0), {tekil1.Count} farklı uzak mesh (LOD1)");
            r.AppendLine($"  yakın görünüm: {ucgen0:N0} üçgen (tekil), ortalama {(tekil0.Count > 0 ? ucgen0 / tekil0.Count : 0):N0}/mesh, " +
                         $"kalem çizgisi payı {cizgi0:N0} (%{(ucgen0 > 0 ? 100.0 * cizgi0 / ucgen0 : 0):0}); " +
                         $"ortalama {(tekil0.Count > 0 ? (float)altMesh / tekil0.Count : 0):0.0} alt mesh (malzeme) / mesh");
            r.AppendLine($"  uzak görünüm: {ucgen1:N0} üçgen (tekil), ortalama {(tekil1.Count > 0 ? ucgen1 / tekil1.Count : 0):N0}/mesh");
            r.AppendLine($"  şehirdeki bütün kopyalar: yakın {kopyaUcgen0:N0}, uzak {kopyaUcgen1:N0} üçgen");
            r.AppendLine($"  mesh belleği {bellek / 1048576.0:0} MB, okunabilir (işlemci kopyası duran) mesh {okunur}");
            r.AppendLine($"Şehir ağaçları ve çalıları (atölye): {cevreKopya} kopya, yakın görünümde {cevreUcgen0:N0} üçgen");

            var tumR = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var dr in tumR)
            {
                var ust = dr.transform.parent;
                if (ust != null && ust.name.StartsWith("Kır bitki örtüsü")) ormanR.Add(dr);
                else if (dr.name.StartsWith("Donatılar · ")) donatiR.Add(dr);
            }
            r.AppendLine($"Kır ormanı: {ormanR.Count} birleşik mesh, {Toplam(ormanR):N0} üçgen");
            r.AppendLine($"Sokak donatıları: {donatiR.Count} birleşik mesh, {Toplam(donatiR):N0} üçgen");
            CanlilariTopla();
            r.AppendLine($"Yayalar, kuryeler ve trafik: {canliR.Count} parça");

            long tumBellek = 0;
            var tumMesh = Resources.FindObjectsOfTypeAll<Mesh>();
            foreach (var m in tumMesh) tumBellek += Profiler.GetRuntimeMemorySizeLong(m);
            int etkin = 0;
            foreach (var rr in tumR) if (rr.enabled) etkin++;
            r.AppendLine($"Sahnede {etkin} açık Renderer; bellekte {tumMesh.Length} mesh, {tumBellek / 1048576.0:0} MB");
            r.AppendLine($"Unity belleği: ayrılan {Profiler.GetTotalReservedMemoryLong() / 1048576.0:0} MB, kullanılan {Profiler.GetTotalAllocatedMemoryLong() / 1048576.0:0} MB, " +
                         $"ekran kartı (tahmini) {Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576.0:0} MB");
            r.AppendLine();
        }

        static Mesh MeshAl(Renderer r)
        {
            var mf = r.GetComponent<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        static long Ucgen(Mesh m)
        {
            long u = 0;
            for (int i = 0; i < m.subMeshCount; i++) u += m.GetIndexCount(i) / 3;
            return u;
        }

        static readonly Dictionary<Mesh, long> cizgiPayi = new Dictionary<Mesh, long>();

        /// <summary>
        /// Kalem çizgisi alt mesh'lerinin üçgeni: oyunda gizli olsa da (PastelCizgi gölgelendiricisi köşeleri ekran dışına atar)
        /// kurulur, bellekte durur ve çizim çağrısı alır. Malzeme adı ya da gölgelendirici adından anlaşılır.
        /// </summary>
        static long CizgiHesapla(Renderer r, Mesh m)
        {
            var mats = r.sharedMaterials;
            long u = 0;
            for (int j = 0; j < mats.Length && j < m.subMeshCount; j++)
            {
                var mt = mats[j];
                if (mt == null) continue;
                if (mt.name.StartsWith("Pastel_Cizgi") || (mt.shader != null && mt.shader.name == "KuryeTycoon/PastelCizgi"))
                    u += m.GetIndexCount(j) / 3;
            }
            return u;
        }

        static long Toplam(List<Renderer> l)
        {
            long u = 0;
            foreach (var r in l) { var m = MeshAl(r); if (m != null) u += Ucgen(m); }
            return u;
        }

        // ---------------------------------------------------------------- rapor

        static void RaporYaz()
        {
            var r = rapor;
            var taban = sonuclar.Count > 0 ? sonuclar[0] : null;
            bool zamanVar = false;
            foreach (var s in sonuclar) if (s.zamanKare > 0) zamanVar = true;
            r.AppendLine("Aşamalar (her biri tek değişiklik; Δ = tabana göre kare süresi farkı, eksi = kazanç):");
            r.AppendLine(zamanVar
                ? "  ms ort | en kötü %5 | Δ ms | FPS | ana / çizim / GPU ms | çizim çağrısı | SetPass | üçgen | gölge düşüren | GC KB | kalite"
                : "  ms ort | en kötü %5 | Δ ms | FPS | ana ms | çizim çağrısı | SetPass | üçgen | gölge düşüren | GC KB | kalite");
            foreach (var s in sonuclar)
            {
                double fark = taban != null ? s.ms - taban.ms : 0;
                string isParcacigi = zamanVar ? $"{s.ana:0.0} / {s.render:0.0} / {s.gpu:0.0}" : $"{s.anaRec:0.0}";
                r.AppendLine($"- {s.ad}");
                r.AppendLine($"  {s.ms,6:0.0} | {s.ms95,6:0.0} | {fark,6:+0.0;-0.0;0.0} | {(s.ms > 0 ? 1000.0 / s.ms : 0),5:0} | {isParcacigi} | " +
                             $"{s.cizim:0} | {s.setpass:0} | {s.ucgen:N0} | {s.golge:0} | {s.gc / 1024.0:0} | {s.seviye}");
            }
            if (sonuclar.Count >= 2)
            {
                var son = sonuclar[sonuclar.Count - 1];
                if (taban != null && Mathf.Abs((float)(son.ms - taban.ms)) > taban.ms * 0.15f)
                    r.AppendLine("UYARI: baştaki ve sondaki taban %15'ten fazla farklı (kamera oynadı, kalite değişti ya da arka planda iş vardı): sonuçlar kaba.");
            }
            string klasor = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            Directory.CreateDirectory(klasor);
            string yol = Path.Combine(klasor, "KuryePerformans_" + System.DateTime.Now.ToString("yyyyMMdd_HHmm") + ".txt");
            File.WriteAllText(yol, r.ToString());
            Debug.Log("Kurye Tycoon · performans ölçümü bitti → " + yol + "\n" + r);
            EditorUtility.DisplayDialog("Performans Ölçümü", "Ölçüm bitti. Rapor:\n" + yol, "Tamam");
        }
    }
}
