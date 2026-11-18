using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Organik şehrin 3D yol ağı ve nehri: plan çizgilerinden (SehirPlani) tek parça mesh.
    ///  • Yollar: kaldırım şeridi (geniş, açık taş) + asfalt (dar, koyu) + caddelerde kesik orta çizgi. Köşelerde yuvarlak birleşim.
    ///  • Nehir: su konturları (kulak kesme üçgenleme), yumuşak kıyı.
    /// Dünyada yukarı −Z; zemin z = 0. Izgara şehirde kullanılmaz.
    /// </summary>
    public static class OrganikSehir3D
    {
        // Genişlikler (kare = 10 m)
        const float CaddeAsfalt = 0.62f, SokakAsfalt = 0.42f;
        const float Kaldirim = 0.16f;                  // asfaltın iki yanında
        const float ZKaldirim = -0.003f, ZAsfalt = -0.006f, ZCizgi = -0.0075f, ZSu = -0.0015f;

        static readonly Color KaldirimRenk = new Color(0.74f, 0.71f, 0.65f);
        static readonly Color AsfaltRenk = new Color(0.27f, 0.28f, 0.31f);
        static readonly Color CizgiRenk = new Color(0.92f, 0.90f, 0.82f);
        static readonly Color SuRenk = new Color(0.38f, 0.63f, 0.84f);

        /// <summary>Yol ağını ve nehri kurar. ornek: şehrin bir malzemesi (aynı gölgelendirici kullanılsın: anime toon ya da URP Lit).</summary>
        public static List<GameObject> Kur(Transform parent, Material ornek)
        {
            ornekShader = ornek != null ? ornek.shader : null;
            var sonuc = new List<GameObject>();
            if (!SehirPlani.Aktif) return sonuc;

            var kal = new MeshKurucu();
            var asf = new MeshKurucu();
            var cizgi = new MeshKurucu();
            foreach (var c in SehirPlani.Yollar)
            {
                if (c.Sayi < 2) continue;
                float g = c.genis ? CaddeAsfalt : SokakAsfalt;
                Serit(kal, c, g * 0.5f + Kaldirim, ZKaldirim);
                Serit(asf, c, g * 0.5f, ZAsfalt);
                if (c.genis) KesikCizgi(cizgi, c, 0.035f, ZCizgi);
            }
            sonuc.Add(Nesne("Kaldırımlar (organik)", parent, kal, Malzeme("OrganikKaldirim", KaldirimRenk)));
            sonuc.Add(Nesne("Yollar (organik)", parent, asf, Malzeme("OrganikAsfalt", AsfaltRenk)));
            if (cizgi.v.Count > 0) sonuc.Add(Nesne("Yol çizgileri (organik)", parent, cizgi, Malzeme("OrganikCizgi", CizgiRenk)));

            var su = new MeshKurucu();
            foreach (var c in SehirPlani.Su)
            {
                if (c.Sayi < 3) continue;
                var pts = new List<Vector2>(c.Sayi);
                for (int i = 0; i < c.Sayi; i++) pts.Add(c[i]);
                if ((pts[0] - pts[pts.Count - 1]).sqrMagnitude < 1e-4f) pts.RemoveAt(pts.Count - 1);
                Poligon(su, pts, ZSu);
            }
            if (su.v.Count > 0) sonuc.Add(Nesne("Tisza (organik)", parent, su, Malzeme("OrganikSu", SuRenk), golgeAl: false));
            return sonuc;
        }

        // ------------------------------------------------------------------ mesh yardımcıları

        class MeshKurucu
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<int> t = new List<int>();

            public void Ucgen(Vector3 a, Vector3 b, Vector3 c)
            {
                // Yüz yukarı (−Z) baksın: normal (b−a)×(c−a) −Z yönünde olmalı
                if (Vector3.Cross(b - a, c - a).z > 0f) { var tmp = b; b = c; c = tmp; }
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }

            public void Dortgen(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Ucgen(a, b, c); Ucgen(a, c, d); }
        }

        /// <summary>Çizgi boyunca yarı genişliği r olan şerit; köşe ve uçlarda yuvarlak disk (boşluk ve sivri uç kalmasın).</summary>
        static void Serit(MeshKurucu m, SehirPlani.Cizgi c, float r, float z)
        {
            for (int k = 0; k + 1 < c.Sayi; k++)
            {
                Vector2 a = c[k], b = c[k + 1];
                var d = b - a;
                if (d.sqrMagnitude < 1e-6f) continue;
                var n = new Vector2(-d.y, d.x).normalized * r;
                m.Dortgen(V(a + n, z), V(b + n, z), V(b - n, z), V(a - n, z));
            }
            for (int k = 0; k < c.Sayi; k++) Disk(m, c[k], r, z, 12);
        }

        static void Disk(MeshKurucu m, Vector2 o, float r, float z, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                m.Ucgen(V(o, z), V(o + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, z), V(o + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r, z));
            }
        }

        static void KesikCizgi(MeshKurucu m, SehirPlani.Cizgi c, float yari, float z)
        {
            const float Dolu = 0.22f, Bos = 0.18f;
            float faz = 0f;
            for (int k = 0; k + 1 < c.Sayi; k++)
            {
                Vector2 a = c[k], b = c[k + 1];
                float L = Vector2.Distance(a, b);
                if (L < 1e-4f) continue;
                var yon = (b - a) / L;
                var n = new Vector2(-yon.y, yon.x) * yari;
                float s = 0f;
                while (s < L)
                {
                    float periyot = Dolu + Bos;
                    float icinde = Mathf.Repeat(faz + s, periyot);
                    if (icinde < Dolu)
                    {
                        float e = Mathf.Min(L, s + (Dolu - icinde));
                        var p0 = a + yon * s;
                        var p1 = a + yon * e;
                        m.Dortgen(V(p0 + n, z), V(p1 + n, z), V(p1 - n, z), V(p0 - n, z));
                        s = e;
                    }
                    else s += periyot - icinde;
                }
                faz += L;
            }
        }

        /// <summary>Basit çokgen (içbükey olabilir) — kulak kesme ile üçgenler.</summary>
        static void Poligon(MeshKurucu m, List<Vector2> p, float z)
        {
            int n = p.Count;
            if (n < 3) return;
            float alan = 0f;
            for (int i = 0; i < n; i++) { var a = p[i]; var b = p[(i + 1) % n]; alan += a.x * b.y - b.x * a.y; }
            var idx = new List<int>(n);
            for (int i = 0; i < n; i++) idx.Add(alan > 0f ? i : n - 1 - i);     // saat yönünün tersi
            int guvenlik = n * n;
            while (idx.Count > 3 && guvenlik-- > 0)
            {
                bool kesildi = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = p[ia], b = p[ib], c = p[ic];
                    if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) <= 1e-7f) continue;   // içbükey köşe
                    bool icerde = false;
                    for (int j = 0; j < idx.Count && !icerde; j++)
                    {
                        int k = idx[j];
                        if (k == ia || k == ib || k == ic) continue;
                        if (UcgenIcinde(p[k], a, b, c)) icerde = true;
                    }
                    if (icerde) continue;
                    m.Ucgen(V(a, z), V(b, z), V(c, z));
                    idx.RemoveAt(i);
                    kesildi = true;
                    break;
                }
                if (!kesildi) break;     // bozuk çokgen: kalan kısmı bırak
            }
            if (idx.Count == 3) m.Ucgen(V(p[idx[0]], z), V(p[idx[1]], z), V(p[idx[2]], z));
        }

        static bool UcgenIcinde(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        static Vector3 V(Vector2 p, float z) => new Vector3(p.x, p.y, z);

        static GameObject Nesne(string ad, Transform parent, MeshKurucu k, Material mat, bool golgeAl = true)
        {
            var go = new GameObject(ad);
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = ad };
            if (k.v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(k.v);
            mesh.SetTriangles(k.t, 0);
            var nr = new Vector3[k.v.Count];
            for (int i = 0; i < nr.Length; i++) nr[i] = Vector3.back;      // yukarı (−Z)
            mesh.normals = nr;
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = golgeAl;
            mr.sortingOrder = 1;
            return go;
        }

        static readonly Dictionary<string, Material> malzemeler = new Dictionary<string, Material>();
        static Shader ornekShader;

        static Material Malzeme(string ad, Color c)
        {
            var sh = ornekShader;
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (malzemeler.TryGetValue(ad, out var m) && m != null && m.shader == sh) return m;
            m = new Material(sh) { name = ad };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", ad == "OrganikSu" ? 0.7f : 0.05f);
            malzemeler[ad] = m;
            return m;
        }
    }
}
