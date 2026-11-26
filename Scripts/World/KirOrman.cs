using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KuryeTycoon
{
    /// <summary>
    /// Şehrin çevresindeki kırı canlandıran bitki örtüsü (organik şehir):
    ///  • Ormanlar: geniş lekeler halinde; tepelerde sık ve çam ağırlıklı, ovada yapraklı koru ve ağaçlıklar.
    ///  • Çalı çitleri: bazı tarlaların kenarları boyunca alçak çalı sırası.
    ///  • Meyve bahçeleri: çiftliklerin ve köylerin yanında sıra sıra küçük yuvarlak ağaçlar.
    /// Binlerce ağaç tek tek nesne olmasın diye ağaçlar alçak poligonlu, kendi üretilen geometri; malzeme başına
    /// tek mesh (statik toplu çizim). Ağaçlar tarla, yol, nehir, köy ve banliyö kuşağına girmez; arazinin yüksekliğine oturur.
    /// Grafik kalitesi düşükse daha seyrek.
    /// </summary>
    public static class KirOrman
    {
        // Dünya: 1 kare = 10 m, −Z yukarı. Kır zemini z = +0,012 − Tepe.
        const float ZeminZ = 0.012f;

        static readonly Color[] YaprakRenk =
        {
            new Color(0.27f, 0.47f, 0.22f),   // yapraklı, koyu
            new Color(0.38f, 0.56f, 0.25f),   // yapraklı, açık
            new Color(0.48f, 0.55f, 0.24f),   // sarımsı yeşil (kavak, söğüt)
            new Color(0.17f, 0.34f, 0.22f),   // çam
            new Color(0.44f, 0.60f, 0.30f),   // meyve ağacı
        };
        static readonly Color GovdeRenk = new Color(0.36f, 0.27f, 0.20f);

        class Parca
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<int> t = new List<int>();

            /// <summary>Düz gölgeli üçgen (alçak poligon görünümü); yüz dışarı baksın diye merkezden uzağa çevrilir.</summary>
            public void Ucgen(Vector3 a, Vector3 b, Vector3 c, Vector3 merkez)
            {
                var nr = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(nr, (a + b + c) / 3f - merkez) < 0f) { var tmp = b; b = c; c = tmp; nr = -nr; }
                nr.Normalize();
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c);
                n.Add(nr); n.Add(nr); n.Add(nr);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }
        }

        static readonly Parca[] parcalar = new Parca[6];   // 0–4 yaprak renkleri, 5 gövde
        static System.Random rng;

        // İkosahedron (yuvarlak taç): 12 köşe, 20 yüz
        static readonly Vector3[] ikoV;
        static readonly int[] ikoT =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };

        static KirOrman()
        {
            float f = (1f + Mathf.Sqrt(5f)) / 2f;
            ikoV = new[]
            {
                new Vector3(-1, f, 0), new Vector3(1, f, 0), new Vector3(-1, -f, 0), new Vector3(1, -f, 0),
                new Vector3(0, -1, f), new Vector3(0, 1, f), new Vector3(0, -1, -f), new Vector3(0, 1, -f),
                new Vector3(f, 0, -1), new Vector3(f, 0, 1), new Vector3(-f, 0, -1), new Vector3(-f, 0, 1)
            };
            for (int i = 0; i < ikoV.Length; i++) ikoV[i] = ikoV[i].normalized;
        }

        /// <summary>Kır bitki örtüsünü kurar. ornek: şehrin bir malzemesi (aynı gölgelendirici); dolu: köy/çiftlik noktaları.</summary>
        static bool[,] acikIzgara;
        static int acikW, acikH;

        /// <summary>Kırda bu nokta tarla, yol, çiftlik ya da köy dışında mı (çayır çimi için). Kurulmadıysa false.</summary>
        public static bool AcikAlan(Vector2 p)
        {
            if (acikIzgara == null) return false;
            int i = Mathf.RoundToInt((p.x + 43f) / 0.5f), j = Mathf.RoundToInt((p.y + 43f) / 0.5f);
            if (i < 0 || j < 0 || i >= acikW || j >= acikH) return false;
            return !acikIzgara[i, j];
        }

        public static void Kur(Transform parent, CityMap map, Material ornek, IList<Vector2> dolu, int tohum)
        {
            if (!CityMap.Organik || map == null) return;
            rng = new System.Random(tohum);
            for (int i = 0; i < parcalar.Length; i++) parcalar[i] = new Parca();

            int seviye = GrafikKalitesi.Seviye;
            float yogunluk = seviye == GrafikKalitesi.Yuksek ? 1f : seviye == GrafikKalitesi.Orta ? 0.75f : 0.5f;
            int W = CityMap.Width, H = CityMap.Height;
            const float Pay = 43f;                          // kır zemini KirPay (44) içinde kal
            float B = CityMap.Banliyo + 1.5f;

            // Hızlı doluluk ızgarası (yarım kare): tarlalar, yollar, nehir, köy ve çiftlikler
            const float A = 0.5f;
            int gw = Mathf.CeilToInt((W + 2 * Pay) / A) + 1, gh = Mathf.CeilToInt((H + 2 * Pay) / A) + 1;
            var izgara = new bool[gw, gh];
            acikIzgara = izgara; acikW = gw; acikH = gh;
            void Isaretle(Rect r)
            {
                int i0 = Mathf.Max(0, Mathf.FloorToInt((r.xMin + Pay) / A)), i1 = Mathf.Min(gw - 1, Mathf.CeilToInt((r.xMax + Pay) / A));
                int j0 = Mathf.Max(0, Mathf.FloorToInt((r.yMin + Pay) / A)), j1 = Mathf.Min(gh - 1, Mathf.CeilToInt((r.yMax + Pay) / A));
                for (int i = i0; i <= i1; i++) for (int j = j0; j <= j1; j++) izgara[i, j] = true;
            }
            bool plan = CityMap.KirPlanli;
            foreach (var f in map.Fields)
            {
                if (!f.Cokgen) { Isaretle(new Rect(f.r.xMin - 0.15f, f.r.yMin - 0.15f, f.r.width + 0.3f, f.r.height + 0.3f)); continue; }
                // Çokgen tarla: saran dikdörtgenin içinde, çokgene 0,15 kareden yakın hücreler
                int i0 = Mathf.Max(0, Mathf.FloorToInt((f.r.xMin - 0.15f + Pay) / A)), i1 = Mathf.Min(gw - 1, Mathf.CeilToInt((f.r.xMax + 0.15f + Pay) / A));
                int j0 = Mathf.Max(0, Mathf.FloorToInt((f.r.yMin - 0.15f + Pay) / A)), j1 = Mathf.Min(gh - 1, Mathf.CeilToInt((f.r.yMax + 0.15f + Pay) / A));
                for (int i = i0; i <= i1; i++)
                    for (int j = j0; j <= j1; j++)
                        if (CityMap.TarlaIci(f, new Vector2(i * A - Pay, j * A - Pay)) > -0.3f) izgara[i, j] = true;
            }
            if (plan)
            {
                // Plan kırsalı veriyorsa: eğri yolların çevresi (uzaklık ızgarasından) ve şehir zemininin örttüğü kareler dolu
                for (int i = 0; i < gw; i++)
                    for (int j = 0; j < gh; j++)
                    {
                        var q = new Vector2(i * A - Pay, j * A - Pay);
                        if (CityMap.KirYoluYakin(q, 0.4f)) { izgara[i, j] = true; continue; }
                        int tx = Mathf.RoundToInt(q.x), ty = Mathf.RoundToInt(q.y);
                        if (tx >= 0 && ty >= 0 && tx < W && ty < H && !SehirPlani.SafKir(tx, ty)) izgara[i, j] = true;
                    }
            }
            else
            foreach (var r in CityMap.CountryRoads)
            {
                var mn = Vector2.Min(r.a, r.b); var mx = Vector2.Max(r.a, r.b);
                Isaretle(Rect.MinMaxRect(mn.x - 0.75f, mn.y - 0.75f, mx.x + 0.75f, mx.y + 0.75f));
            }
            foreach (var p in map.Farms) Isaretle(new Rect(p.x - 1.2f, p.y - 1.0f, 3.0f, 2.2f));
            if (dolu != null) foreach (var p in dolu) Isaretle(new Rect(p.x - 0.7f, p.y - 0.7f, 1.4f, 1.4f));
            bool Bos(Vector2 p)
            {
                int i = Mathf.RoundToInt((p.x + Pay) / A), j = Mathf.RoundToInt((p.y + Pay) / A);
                if (i < 0 || j < 0 || i >= gw || j >= gh) return false;
                return !izgara[i, j];
            }
            float Uzak(Vector2 p) => Mathf.Max(Mathf.Max(-p.x, p.x - (W - 1)), Mathf.Max(-p.y, p.y - (H - 1)));
            float Z(Vector2 p) => ZeminZ - Arazi.Tepe(p.x, p.y);

            // 1) Ormanlar: büyük ölçekli gürültü lekesi; tepelere çıktıkça orman artar ve çam ağırlıklı olur
            int ormanAgac = 0;
            float adim = seviye == GrafikKalitesi.Dusuk ? 0.75f : 0.6f;
            for (float x = -Pay; x < W - 1 + Pay; x += adim)
                for (float y = -Pay; y < H - 1 + Pay; y += adim)
                {
                    var p = new Vector2(x + ((float)rng.NextDouble() - 0.5f) * adim * 0.9f, y + ((float)rng.NextDouble() - 0.5f) * adim * 0.9f);
                    // Şehre uzaklık: plan kırsalı veriyorsa şehir lekesine (ağaçlar lekenin hemen dışında başlar), yoksa dikdörtgene
                    float u = plan ? CityMap.KenteUzaklik(p) : Uzak(p);
                    if (u < (plan ? 1.6f : B)) continue;
                    float tepe = Arazi.Tepe(p.x, p.y);
                    float leke = Mathf.PerlinNoise(p.x * 0.055f + 71.3f, p.y * 0.055f + 19.7f) * 0.75f
                               + Mathf.PerlinNoise(p.x * 0.16f + 5.1f, p.y * 0.16f + 88.2f) * 0.25f;
                    // Kameranın gördüğü kuşakta (şehre 30 kareye kadar) tarlaların arasında korular; uzak tepelerde geniş ormanlar
                    float esik = 0.47f - Mathf.Clamp01(tepe / 1.6f) * 0.2f;
                    if (u > 30f && rng.NextDouble() < 0.45) continue;        // uzak: kamera az görür, performans
                    if (!Bos(p) || CityMap.KirNehirYakin(p, 0.5f)) continue;
                    if (leke < esik)
                    {
                        // Lekenin dışı: çayıra serpili tek ağaç (kır boş görünmesin)
                        if (rng.NextDouble() > 0.03 * yogunluk) continue;
                    }
                    else
                    {
                        float kenar = Mathf.Clamp01((leke - esik) / 0.05f);     // lekenin kenarında seyrek
                        if (rng.NextDouble() > kenar * 0.9f * yogunluk + 0.03f) continue;
                    }
                    bool cam = tepe > 0.9f ? rng.NextDouble() < 0.8 : tepe > 0.35f ? rng.NextDouble() < 0.45 : rng.NextDouble() < 0.12;
                    float boy = 0.85f + (float)rng.NextDouble() * 0.5f;
                    if (cam) Cam(new Vector3(p.x, p.y, Z(p)), boy);
                    else Yaprakli(new Vector3(p.x, p.y, Z(p)), boy, rng.NextDouble() < 0.5 ? 0 : 1);
                    ormanAgac++;
                }

            // 2) Çalı çitleri: tarla kenarlarında alçak çalı sıraları (tarlaları birbirinden ayırır)
            int cali = 0;
            float citOlasilik = 0.32f * yogunluk;
            foreach (var f in map.Fields)
            {
                if (!f.Cokgen) continue;
                // Çokgen tarla: kenarların bir kısmında (dışında, 0,1 kare) çalı sırası
                int n = f.kose.Length;
                for (int kenar = 0; kenar < n; kenar++)
                {
                    // çokgenin kenarı dikdörtgeninkinden çok (5–8): çalı sayısı eskisine yakın kalsın diye olasılık yarım
                    if (rng.NextDouble() > citOlasilik * 0.5f) continue;
                    Vector2 a = f.kose[kenar], b = f.kose[(kenar + 1) % n];
                    var e = b - a;
                    float boy = e.magnitude;
                    if (boy < 0.8f) continue;
                    var yon = e / boy;
                    var dis = new Vector2(yon.y, -yon.x);
                    for (float d = 0.1f; d < boy; d += 0.32f + (float)rng.NextDouble() * 0.14f)
                    {
                        var p = a + yon * d + dis * 0.1f;
                        if (CityMap.KirYoluYakin(p, 0.3f) || CityMap.KirNehirYakin(p, 0.3f) || CityMap.KenteUzaklik(p) < 1.2f) continue;
                        Cali(new Vector3(p.x, p.y, Z(p)), 0.8f + (float)rng.NextDouble() * 0.5f);
                        cali++;
                    }
                }
            }
            foreach (var f in map.Fields)
            {
                if (f.Cokgen) continue;
                if (Uzak(f.r.center) < B) continue;
                for (int kenar = 0; kenar < 4; kenar++)
                {
                    if (rng.NextDouble() > citOlasilik) continue;
                    var r = f.r;
                    Vector2 a = kenar == 0 ? new Vector2(r.xMin, r.yMin - 0.1f) : kenar == 1 ? new Vector2(r.xMin, r.yMax + 0.1f)
                              : kenar == 2 ? new Vector2(r.xMin - 0.1f, r.yMin) : new Vector2(r.xMax + 0.1f, r.yMin);
                    Vector2 yon = kenar < 2 ? Vector2.right : Vector2.up;
                    float boy = kenar < 2 ? r.width : r.height;
                    for (float d = 0.1f; d < boy; d += 0.32f + (float)rng.NextDouble() * 0.14f)
                    {
                        var p = a + yon * d;
                        if (CityMap.KirYoluYakin(p, 0.35f) || CityMap.KirNehirYakin(p, 0.3f)) continue;
                        Cali(new Vector3(p.x, p.y, Z(p)), 0.8f + (float)rng.NextDouble() * 0.5f);
                        cali++;
                    }
                }
            }

            // 3) Meyve bahçeleri: çiftliklerin arkasında sıra sıra küçük ağaçlar
            int meyve = 0;
            foreach (var c in map.Farms)
            {
                var bas = c + new Vector2(-1.4f, 1.3f);
                for (int i = 0; i < 6; i++)
                    for (int j = 0; j < 4; j++)
                    {
                        var p = bas + new Vector2(i * 0.5f, j * 0.5f);
                        if (CityMap.KirYoluYakin(p, 0.4f) || CityMap.KirNehirYakin(p, 0.4f)) continue;
                        if (rng.NextDouble() < 0.1) continue;
                        Meyve(new Vector3(p.x, p.y, Z(p)), 0.85f + (float)rng.NextDouble() * 0.3f);
                        meyve++;
                    }
            }

            Material Mat(string ad, Color c)
            {
                var sh = ornek != null ? ornek.shader : Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                var m = new Material(sh) { name = ad };
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                m.color = c;
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.08f);
                return m;
            }
            var kok = new GameObject("Kır bitki örtüsü (orman, çit, bahçe)").transform;
            kok.SetParent(parent, false);
            for (int i = 0; i < parcalar.Length; i++)
            {
                var pr = parcalar[i];
                if (pr.v.Count == 0) continue;
                var mesh = new Mesh { name = "KirOrman_" + i, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(pr.v);
                mesh.SetNormals(pr.n);
                mesh.SetTriangles(pr.t, 0);
                mesh.RecalculateBounds();
                var go = new GameObject(i < 5 ? "Ağaçlar · " + i : "Ağaç gövdeleri");
                go.transform.SetParent(kok, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Mat(i < 5 ? "KirYaprak" + i : "KirGovde", i < 5 ? YaprakRenk[i] : GovdeRenk);
                mr.shadowCastingMode = seviye == GrafikKalitesi.Dusuk ? ShadowCastingMode.Off : ShadowCastingMode.On;
                mr.receiveShadows = true;
                parcalar[i] = null;
            }
            Debug.Log($"Kurye Tycoon: kır bitki örtüsü: {ormanAgac} orman ağacı, {cali} çalı, {meyve} meyve ağacı.");
        }

        // ---------------------------------------------------------------- ağaç geometrileri (−Z yukarı)

        static void Govde(Vector3 taban, float r, float boy)
        {
            var pr = parcalar[5];
            var merkez = taban + new Vector3(0f, 0f, -boy * 0.5f);
            for (int k = 0; k < 4; k++)
            {
                float a0 = k * Mathf.PI * 0.5f + 0.4f, a1 = a0 + Mathf.PI * 0.5f;
                var d0 = new Vector3(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r, 0f);
                var d1 = new Vector3(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r, 0f);
                var ust = new Vector3(0f, 0f, -boy);
                pr.Ucgen(taban + d0, taban + d1, taban + d1 * 0.7f + ust, merkez);
                pr.Ucgen(taban + d0, taban + d1 * 0.7f + ust, taban + d0 * 0.7f + ust, merkez);
            }
        }

        /// <summary>Yuvarlak taç: rastgele bozulmuş ikosahedron (her ağaç biraz farklı).</summary>
        static void Tac(int renk, Vector3 merkez, Vector3 olcek)
        {
            var pr = parcalar[renk];
            var k = new Vector3[12];
            for (int i = 0; i < 12; i++)
            {
                float boz = 0.85f + (float)rng.NextDouble() * 0.3f;
                k[i] = merkez + Vector3.Scale(ikoV[i] * boz, olcek);
            }
            for (int i = 0; i < ikoT.Length; i += 3) pr.Ucgen(k[ikoT[i]], k[ikoT[i + 1]], k[ikoT[i + 2]], merkez);
        }

        static void Yaprakli(Vector3 taban, float boy, int renk)
        {
            float h = 0.95f * boy;                 // ≈ 8–12 m
            Govde(taban, 0.04f, h * 0.45f);
            float r = 0.3f * boy;
            Tac(renk, taban + new Vector3(0f, 0f, -h * 0.62f), new Vector3(r, r, r * 0.9f));
            if (rng.NextDouble() < 0.45)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                Tac(renk, taban + new Vector3(Mathf.Cos(a) * r * 0.55f, Mathf.Sin(a) * r * 0.55f, -h * 0.5f), new Vector3(r * 0.7f, r * 0.7f, r * 0.62f));
            }
        }

        static void Cam(Vector3 taban, float boy)
        {
            var pr = parcalar[3];
            float h = 1.25f * boy;                 // ≈ 11–17 m
            Govde(taban, 0.035f, h * 0.2f);
            // Üst üste iki koni (8 kenar)
            for (int kat = 0; kat < 2; kat++)
            {
                float alt = h * (0.14f + kat * 0.34f), ust = h * (kat == 0 ? 0.72f : 1f);
                float r = 0.27f * boy * (kat == 0 ? 1f : 0.72f);
                var tepe = taban + new Vector3(0f, 0f, -ust);
                var merkez = taban + new Vector3(0f, 0f, -(alt + ust) * 0.5f);
                for (int k = 0; k < 8; k++)
                {
                    float a0 = k * Mathf.PI / 4f, a1 = a0 + Mathf.PI / 4f;
                    var p0 = taban + new Vector3(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r, -alt);
                    var p1 = taban + new Vector3(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r, -alt);
                    pr.Ucgen(p0, p1, tepe, merkez);
                    pr.Ucgen(p0, p1, taban + new Vector3(0f, 0f, -alt), merkez);   // alt kapak
                }
            }
        }

        static void Cali(Vector3 taban, float boy)
        {
            float r = 0.13f * boy;
            Tac(rng.NextDouble() < 0.6 ? 0 : 2, taban + new Vector3(0f, 0f, -r * 0.7f), new Vector3(r * 1.2f, r * 1.2f, r * 0.85f));
        }

        static void Meyve(Vector3 taban, float boy)
        {
            float h = 0.45f * boy;                 // ≈ 4–5 m
            Govde(taban, 0.025f, h * 0.5f);
            float r = 0.16f * boy;
            Tac(4, taban + new Vector3(0f, 0f, -h * 0.75f), new Vector3(r, r, r * 0.85f));
        }
    }
}
