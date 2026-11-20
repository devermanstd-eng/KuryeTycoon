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
        // Genişlikler (kare = 10 m). Cadde: iki yönde ikişer şerit (8 m), sokak: çift yönlü iki şerit (5,6 m)
        public const float CaddeAsfalt = 0.80f, SokakAsfalt = 0.56f;
        public const float Kaldirim = 0.10f;           // asfaltın iki yanında
        // Su yüzeyi zeminin altında: nehir yatağı OrganikZemin'de çukurlaşır, kıyılar eğimli iner (köprüler suyun üstünde kalır)
        const float ZKaldirim = -0.003f, ZAsfalt = -0.006f, ZCizgi = -0.0075f, ZSu = -0.0015f, ZPark = -0.001f;

        static readonly Color KaldirimRenk = new Color(0.74f, 0.71f, 0.65f);
        static readonly Color AsfaltRenk = new Color(0.27f, 0.28f, 0.31f);
        static readonly Color CizgiRenk = new Color(0.92f, 0.90f, 0.82f);
        static readonly Color SariRenk = new Color(0.93f, 0.76f, 0.25f);
        static readonly Color BordurRenk = new Color(0.62f, 0.60f, 0.57f);
        static readonly Color SuRenk = new Color(0.38f, 0.63f, 0.84f);
        static readonly Color ParkRenk = new Color(0.47f, 0.66f, 0.36f);

        /// <summary>Yol ağını ve nehri kurar. ornek: şehrin bir malzemesi (aynı gölgelendirici kullanılsın: anime toon ya da URP Lit).</summary>
        public static List<GameObject> Kur(Transform parent, Material ornek)
        {
            ornekShader = ornek != null ? ornek.shader : null;
            var sonuc = new List<GameObject>();
            if (!SehirPlani.Aktif) return sonuc;

            var kal = new MeshKurucu();
            var bordur = new MeshKurucu();
            var asf = new MeshKurucu();
            var cizgi = new MeshKurucu();
            var sari = new MeshKurucu();
            foreach (var c in SehirPlani.Yollar)
            {
                if (c.Sayi < 2) continue;
                float g = c.genis ? CaddeAsfalt : SokakAsfalt;
                Serit(kal, c, g * 0.5f + Kaldirim, ZKaldirim);
                Serit(bordur, c, g * 0.5f + 0.012f, ZKaldirim - 0.0008f);
                Serit(asf, c, g * 0.5f, ZAsfalt);
            }
            // Çizgiler asfaltın üstünde, ayrı geçişte: kavşakta başka yolun asfaltı çizgiyi örtmesin
            foreach (var c in SehirPlani.Yollar)
            {
                if (c.Sayi < 2) continue;
                float g = c.genis ? CaddeAsfalt : SokakAsfalt;
                if (c.genis)
                {
                    // Cadde: ortada çift sarı çizgi, iki yanda şerit kesikleri, kenarda beyaz kenar çizgisi, uçlarda yaya geçidi
                    YanCizgi(sari, c, 0.016f, 0.006f, ZCizgi, false);
                    YanCizgi(sari, c, -0.016f, 0.006f, ZCizgi, false);
                    YanCizgi(cizgi, c, g * 0.25f, 0.007f, ZCizgi, true);
                    YanCizgi(cizgi, c, -g * 0.25f, 0.007f, ZCizgi, true);
                    YanCizgi(cizgi, c, g * 0.5f - 0.03f, 0.006f, ZCizgi, false);
                    YanCizgi(cizgi, c, -(g * 0.5f - 0.03f), 0.006f, ZCizgi, false);
                    YayaGecidi(cizgi, c, g, ZCizgi - 0.0002f);
                }
                else
                {
                    // Sokak: ortada kesik beyaz çizgi (çift yönlü iki şerit)
                    YanCizgi(cizgi, c, 0f, 0.0075f, ZCizgi, true);
                    if (Uzunluk(c) > 2.2f) YayaGecidi(cizgi, c, g, ZCizgi - 0.0002f);
                }
            }
            sonuc.Add(Nesne("Kaldırımlar (organik)", parent, kal, Malzeme("OrganikKaldirim", KaldirimRenk)));
            sonuc.Add(Nesne("Bordürler (organik)", parent, bordur, Malzeme("OrganikBordur", BordurRenk)));
            sonuc.Add(Nesne("Yollar (organik)", parent, asf, Malzeme("OrganikAsfalt", AsfaltRenk)));
            if (cizgi.v.Count > 0) sonuc.Add(Nesne("Yol çizgileri (organik)", parent, cizgi, Malzeme("OrganikCizgi", CizgiRenk)));
            if (sari.v.Count > 0) sonuc.Add(Nesne("Yol çizgileri sarı (organik)", parent, sari, Malzeme("OrganikSariCizgi", SariRenk)));

            // Parklar: plan konturlarından yumuşak kenarlı çim (kare park zemini kenarda basamak yapmasın)
            var park = new MeshKurucu();
            foreach (var c in SehirPlani.Park)
            {
                if (c.Sayi < 3) continue;
                var pts = new List<Vector2>(c.Sayi);
                for (int i = 0; i < c.Sayi; i++) pts.Add(c[i]);
                if ((pts[0] - pts[pts.Count - 1]).sqrMagnitude < 1e-4f) pts.RemoveAt(pts.Count - 1);
                Poligon(park, pts, ZPark);
            }
            if (park.v.Count > 0) sonuc.Add(Nesne("Parklar (organik)", parent, park, Malzeme("OrganikPark", ParkRenk)));

            var su = new MeshKurucu();
            foreach (var c in SehirPlani.Su)
            {
                if (c.Sayi < 3) continue;
                var pts = new List<Vector2>(c.Sayi);
                for (int i = 0; i < c.Sayi; i++) pts.Add(c[i]);
                if ((pts[0] - pts[pts.Count - 1]).sqrMagnitude < 1e-4f) pts.RemoveAt(pts.Count - 1);
                Poligon(su, pts, ZSu);
            }
            if (su.v.Count > 0)
            {
                var suMat = SuMalzemesi();
                sonuc.Add(Nesne("Tisza (organik)", parent, su, suMat != null ? suMat : Malzeme("OrganikSu", SuRenk), golgeAl: false));
            }
            KirNehirKur(parent, sonuc);
            return sonuc;
        }

        // ------------------------------------------------------------------ nehrin şehir dışındaki kolları

        static readonly Color KumRenk = new Color(0.72f, 0.66f, 0.50f);
        static readonly Color KopruRenk = new Color(0.66f, 0.64f, 0.60f);

        /// <summary>
        /// Nehir şehrin kenarında bitmesin: CityMap.KirNehir kolları aynı canlı suyla harita sonuna kadar akar.
        /// Kıyıda ince kum/çakıl şeridi; kır yollarının nehri geçtiği yerde korkuluklu beton köprü.
        /// </summary>
        static void KirNehirKur(Transform parent, List<GameObject> sonuc)
        {
            var kollar = CityMap.KirNehir;
            if (kollar == null || kollar.Count == 0) return;
            int W = CityMap.Width, H = CityMap.Height;
            bool Disarida(Vector2 p) => p.x < -0.5f || p.y < -0.5f || p.x > W - 0.5f || p.y > H - 0.5f;

            var su = new MeshKurucu();
            var kum = new MeshKurucu();
            foreach (var kol in kollar)
            {
                int n = kol.P.Count;
                if (n < 2) continue;
                Vector2 Yan(int i)
                {
                    var d = (kol.P[Mathf.Min(n - 1, i + 1)] - kol.P[Mathf.Max(0, i - 1)]).normalized;
                    return new Vector2(-d.y, d.x);
                }
                for (int i = 0; i + 1 < n; i++)
                {
                    Vector2 a = kol.P[i], b = kol.P[i + 1], na = Yan(i), nb = Yan(i + 1);
                    float ga = kol.G[i] * 0.5f, gb = kol.G[i + 1] * 0.5f;
                    su.Dortgen(V(a + na * ga, ZSu), V(b + nb * gb, ZSu), V(b - nb * gb, ZSu), V(a - na * ga, ZSu));
                    if (!Disarida(a) && !Disarida(b)) continue;          // şehrin içinde kıyıyı plan çizer
                    for (int k = -1; k <= 1; k += 2)
                    {
                        float ia = ga - 0.05f, ib = gb - 0.05f, da = ga + 0.3f, db = gb + 0.3f;
                        kum.Dortgen(V(a + na * k * ia, ZSu + 0.0007f), V(b + nb * k * ib, ZSu + 0.0007f),
                                    V(b + nb * k * db, ZSu + 0.0007f), V(a + na * k * da, ZSu + 0.0007f));
                    }
                }
            }
            if (su.v.Count == 0) return;
            var suMat = SuMalzemesi();
            sonuc.Add(Nesne("Tisza · şehir dışı", parent, su, suMat != null ? suMat : Malzeme("OrganikSu", SuRenk), golgeAl: false));
            if (kum.v.Count > 0) sonuc.Add(Nesne("Tisza kıyısı · şehir dışı", parent, kum, Malzeme("OrganikKum", KumRenk)));

            // Köprüler: kır yolu × nehir kolu kesişimleri
            var kopruler = new GameObject("Kır köprüleri").transform;
            kopruler.SetParent(parent, false);
            sonuc.Add(kopruler.gameObject);
            var beton = Malzeme("OrganikKopru", KopruRenk);
            var asfalt = Malzeme("OrganikAsfalt", AsfaltRenk);
            foreach (var r in CityMap.CountryRoads)
                foreach (var kol in kollar)
                    for (int i = 0; i + 1 < kol.P.Count; i++)
                    {
                        if (!Kesisim(r.a, r.b, kol.P[i], kol.P[i + 1], out var x)) continue;
                        if (!Disarida(x)) continue;
                        var yol = (r.b - r.a).normalized;
                        var nehir = (kol.P[i + 1] - kol.P[i]).normalized;
                        float sin = Mathf.Abs(yol.x * nehir.y - yol.y * nehir.x);
                        float boy = kol.G[i] / Mathf.Max(0.4f, sin) + 1.1f;
                        Kopru(kopruler, x, yol, boy, beton, asfalt);
                    }
        }

        static bool Kesisim(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2, out Vector2 x)
        {
            x = default;
            var r = p2 - p; var s = q2 - q;
            float den = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(den) < 1e-6f) return false;
            var qp = q - p;
            float t = (qp.x * s.y - qp.y * s.x) / den;
            float u = (qp.x * r.y - qp.y * r.x) / den;
            if (t < 0f || t > 1f || u < 0f || u > 1f) return false;
            x = p + r * t;
            return true;
        }

        /// <summary>Basit beton köprü: tabliye, üstünde asfalt, iki yanda korkuluk, ortada iki ayak (−Z yukarı).</summary>
        static void Kopru(Transform parent, Vector2 merkez, Vector2 yon, float boy, Material beton, Material asfalt)
        {
            float aci = Mathf.Atan2(yon.y, yon.x) * Mathf.Rad2Deg;
            var kok = new GameObject("Köprü").transform;
            kok.SetParent(parent, false);
            kok.localPosition = new Vector3(merkez.x, merkez.y, 0f);
            kok.localRotation = Quaternion.Euler(0f, 0f, aci);
            void Kutu(Vector3 orta, Vector3 boyut, Material m)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
                go.transform.SetParent(kok, false);
                go.transform.localPosition = orta;
                go.transform.localScale = boyut;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = m;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            Kutu(new Vector3(0f, 0f, 0.004f), new Vector3(boy, 1.0f, 0.04f), beton);                 // tabliye (üstü −0,016)
            Kutu(new Vector3(0f, 0f, -0.017f), new Vector3(boy + 0.02f, 0.74f, 0.003f), asfalt);      // asfalt
            for (int k = -1; k <= 1; k += 2)
            {
                Kutu(new Vector3(0f, k * 0.475f, -0.045f), new Vector3(boy, 0.05f, 0.058f), beton);   // korkuluk duvarı
                Kutu(new Vector3(boy * 0.22f * k, 0f, 0.03f), new Vector3(0.12f, 0.7f, 0.06f), beton); // ayak
            }
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
            const float Dolu = 0.22f, Periyot = 0.40f;
            // Çizgi boyunca birikimli uzunluk; her kesik, başlangıç ve bitiş noktası arasında düz bir parça
            var top = new float[c.Sayi];
            for (int k = 1; k < c.Sayi; k++) top[k] = top[k - 1] + Vector2.Distance(c[k - 1], c[k]);
            float L = top[c.Sayi - 1];
            int seg = 0;
            Vector2 Nokta(float d)
            {
                while (seg < c.Sayi - 2 && top[seg + 1] < d) seg++;
                float u = top[seg + 1] - top[seg];
                float t = u > 1e-5f ? (d - top[seg]) / u : 0f;
                return Vector2.Lerp(c[seg], c[seg + 1], Mathf.Clamp01(t));
            }
            int adet = Mathf.FloorToInt(L / Periyot);
            for (int i = 0; i <= adet; i++)
            {
                float d0 = i * Periyot + 0.09f, d1 = Mathf.Min(L, d0 + Dolu);
                if (d1 - d0 < 0.05f) break;
                var p0 = Nokta(d0);
                var p1 = Nokta(d1);
                var yon = p1 - p0;
                if (yon.sqrMagnitude < 1e-6f) continue;
                var n = new Vector2(-yon.y, yon.x).normalized * yari;
                m.Dortgen(V(p0 + n, z), V(p1 + n, z), V(p1 - n, z), V(p0 - n, z));
            }
        }

        static float Uzunluk(SehirPlani.Cizgi c)
        {
            float L = 0f;
            for (int k = 1; k < c.Sayi; k++) L += Vector2.Distance(c[k - 1], c[k]);
            return L;
        }

        /// <summary>Çizgiye paralel (ofs kadar yana kaymış) düz ya da kesik şerit çizgisi.</summary>
        static void YanCizgi(MeshKurucu m, SehirPlani.Cizgi c, float ofs, float yari, float z, bool kesik)
        {
            const float Dolu = 0.2f, Periyot = 0.38f;
            double s = 0.0;
            for (int k = 0; k + 1 < c.Sayi; k++)
            {
                Vector2 a = c[k], b = c[k + 1];
                float L = Vector2.Distance(a, b);
                if (L < 1e-5f) continue;
                var yon = (b - a) / L;
                var n = new Vector2(-yon.y, yon.x);
                if (!kesik)
                {
                    var p0 = a + n * ofs; var p1 = b + n * ofs;
                    m.Dortgen(V(p0 + n * yari, z), V(p1 + n * yari, z), V(p1 - n * yari, z), V(p0 - n * yari, z));
                }
                else
                {
                    // Parçalar arasında kesik düzeni sürsün (birikimli uzunluk).
                    // Kesikler sabit aralıklı: j. kesik [j·Periyot, j·Periyot + Dolu]. Tamsayı sayaçlı, her zaman biter.
                    long j0 = (long)System.Math.Floor(s / Periyot);
                    long j1 = (long)System.Math.Floor((s + L) / Periyot);
                    for (long j = j0; j <= j1; j++)
                    {
                        float d0 = Mathf.Max(0f, (float)(j * (double)Periyot - s));
                        float d1 = Mathf.Min(L, (float)(j * (double)Periyot + Dolu - s));
                        if (d1 - d0 < 1e-4f) continue;
                        var p0 = a + yon * d0 + n * ofs; var p1 = a + yon * d1 + n * ofs;
                        m.Dortgen(V(p0 + n * yari, z), V(p1 + n * yari, z), V(p1 - n * yari, z), V(p0 - n * yari, z));
                    }
                }
                s += L;
            }
        }

        /// <summary>Yolun iki ucunda (kavşağa varmadan) zebra yaya geçidi.</summary>
        static void YayaGecidi(MeshKurucu m, SehirPlani.Cizgi c, float g, float z)
        {
            for (int uc = 0; uc < 2; uc++)
            {
                Vector2 p0 = uc == 0 ? c[0] : c[c.Sayi - 1];
                Vector2 p1 = uc == 0 ? c[1] : c[c.Sayi - 2];
                var d = p1 - p0;
                float L = d.magnitude;
                if (L < g + 0.3f) continue;
                d /= L;
                var n = new Vector2(-d.y, d.x);
                var mrk = p0 + d * (g * 0.5f + 0.2f);
                int seritSayi = Mathf.Max(4, Mathf.RoundToInt(g / 0.07f));
                float bas = -g * 0.5f + 0.04f, adim = (g - 0.08f) / seritSayi;
                for (int i = 0; i < seritSayi; i++)
                {
                    if (i % 2 == 1) continue;
                    float u0 = bas + i * adim, u1 = u0 + adim;
                    var a = mrk + n * u0 - d * 0.09f; var b = mrk + n * u1 - d * 0.09f;
                    var e = mrk + n * u1 + d * 0.09f; var f = mrk + n * u0 + d * 0.09f;
                    m.Dortgen(V(a, z), V(b, z), V(e, z), V(f, z));
                }
            }
        }

        /// <summary>Canlı su: KuryeTycoon/Su (dalga, parıltı, akıntı). Bulunamazsa null (düz renk malzeme kullanılır).</summary>
        static Material SuMalzemesi()
        {
            if (malzemeler.TryGetValue("OrganikSuCanli", out var m) && m != null) return m;
            var sh = Shader.Find("KuryeTycoon/Su");
            if (sh == null) return null;
            m = new Material(sh) { name = "OrganikSuCanli" };
            malzemeler["OrganikSuCanli"] = m;
            return m;
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
