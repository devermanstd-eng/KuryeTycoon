using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KuryeTycoon
{
    /// <summary>
    /// Organik şehrin 3D zemini: ışık alan tek mesh ve bölgeye göre desenli doku. Düz renkli 2D kareler yerine:
    ///  • Ev mahalleleri, kampüs, park, kıyı: tonları dalgalanan çimen (bahçelerde çiçek beneği)
    ///  • Eski şehir ve meydan: Arnavut kaldırımı (derzli taşlar)
    ///  • Çarşı ve kule bölgesi: cadde önü kare taş döşeme, arka avlular çimenli
    ///  • Sanayi ve AVM: derzli beton
    ///  • Nehir yatağı: kum ve çamur; kıyılar eğimli iner (Arazi.NehirCukur)
    /// Bölgeler arası geçiş karelerin arasında yumuşak (dört komşu karenin deseni harmanlanır): basamak görünmez.
    /// Kilitli bölge solgun, kar ve ıslaklık dokuya işlenir (CityMap.RefreshColors → Renkle).
    /// </summary>
    public static class OrganikZemin
    {
        const int PikselKare = 24;      // bir kare (10 m) dokuda 24 piksel
        const int MeshAdim = 4;          // mesh: kare başına 4×4 hücre (nehir kıyısı eğimi için)

        enum Desen { Cim, Bahce, Kaldirim, Plaka, Beton, Kum }

        static Texture2D doku;
        static Material malzeme;
        static Color32[] taban;          // desenli, kilit/kar uygulanmamış piksel
        static float[] kilitAgirlik;     // piksel başına kilitli bölge ağırlığı
        static int tw, th;
        static int sonKilit = -1, sonKar = -1, sonIslak = -1;
        static GameObject nesne;

        public static bool Var => nesne != null;

        /// <summary>Zemin mesh'ini ve dokusunu kurar. parent: City3D kökü; ornek: binaların gölgelendiricisi.</summary>
        public static GameObject Kur(Transform parent, Material ornek)
        {
            if (!SehirPlani.Aktif) return null;
            int W = CityMap.Width, H = CityMap.Height;
            DokuUret(W, H);

            var sh = ornek != null ? ornek.shader : Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            if (malzeme == null || malzeme.shader != sh)
            {
                malzeme = new Material(sh) { name = "OrganikZemin" };
                if (malzeme.HasProperty("_BaseColor")) malzeme.SetColor("_BaseColor", Color.white);
                if (malzeme.HasProperty("_Smoothness")) malzeme.SetFloat("_Smoothness", 0.05f);
                if (malzeme.HasProperty("_Metallic")) malzeme.SetFloat("_Metallic", 0f);
                // AnimeToon gibi fırça/sarmaşık özellikleri olan gölgelendiricide hepsi kapalı
                foreach (var ad in new[] { "_FircaGuc", "_TonFarki", "_Sarmasik", "_Cim", "_CatiFirca" })
                    if (malzeme.HasProperty(ad)) malzeme.SetFloat(ad, 0f);
            }
            malzeme.mainTexture = doku;
            if (malzeme.HasProperty("_BaseMap")) malzeme.SetTexture("_BaseMap", doku);

            // Mesh: şehir dikdörtgeni, nehir yatağında çukur
            float x0 = -0.5f, y0 = -0.5f;
            int nx = W * MeshAdim, ny = H * MeshAdim;
            var v = new Vector3[(nx + 1) * (ny + 1)];
            var uv = new Vector2[v.Length];
            for (int j = 0; j <= ny; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float x = x0 + (float)i / MeshAdim, y = y0 + (float)j / MeshAdim;
                    // Zemin 2D karelerin hemen üstünde (−Z yukarı), yolların altında
                    v[j * (nx + 1) + i] = new Vector3(x, y, Arazi.Z(x, y) - 0.0006f);
                    uv[j * (nx + 1) + i] = new Vector2((float)i / nx, (float)j / ny);
                }
            var t = new int[nx * ny * 6];
            int k = 0;
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                    // Yüz −Z (yukarı) baksın
                    t[k++] = a; t[k++] = c; t[k++] = b;
                    t[k++] = b; t[k++] = c; t[k++] = d;
                }
            var mesh = new Mesh { name = "OrganikZemin", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = v;
            mesh.uv = uv;
            mesh.triangles = t;
            mesh.RecalculateNormals();
            var nr = mesh.normals;
            if (nr.Length > 0 && nr[nr.Length / 2].z > 0f)
            {
                // Sarım ters çıktıysa çevir
                for (int q = 0; q < t.Length; q += 3) { int tmp = t[q + 1]; t[q + 1] = t[q + 2]; t[q + 2] = tmp; }
                mesh.triangles = t;
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();

            if (nesne != null) Object.Destroy(nesne);
            nesne = new GameObject("Şehir zemini (3D)");
            nesne.transform.SetParent(parent, false);
            nesne.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = nesne.AddComponent<MeshRenderer>();
            mr.sharedMaterial = malzeme;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            mr.sortingOrder = 0;
            sonKilit = sonKar = sonIslak = -1;
            if (CityMap.I != null) Renkle(CityMap.I);
            return nesne;
        }

        // ------------------------------------------------------------------ doku

        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(s * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFF) / 65535f;
            }
        }

        static Desen KareDeseni(int x, int y)
        {
            if (CityMap.WaterAt(x, y)) return Desen.Kum;
            var z = DuzBolge(x, y);
            bool on = OnCephe(x, y);
            switch (z)
            {
                case Zone.Houses: return Desen.Bahce;
                case Zone.Campus: return on ? Desen.Plaka : Desen.Cim;
                case Zone.Park: case Zone.River: case Zone.Country: return Desen.Cim;
                case Zone.OldTown: case Zone.Plaza: return Desen.Kaldirim;
                case Zone.Mixed: case Zone.HighRise: return on ? Desen.Plaka : Desen.Cim;
                case Zone.Warehouse: case Zone.Mall: return Desen.Beton;
                default: return Desen.Cim;
            }
        }

        /// <summary>Çayır çimi için karenin çim ağırlığı (0–1): çimen ve bahçe 1, sert zemin 0. Bkz. Cim.</summary>
        public static float CimAgirligi(int x, int y)
        {
            switch (KareDeseni(x, y))
            {
                case Desen.Cim: return 1f;
                case Desen.Bahce: return 1f;
                default: return 0f;
            }
        }

        static bool OnCephe(int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if ((dx != 0 || dy != 0) && CityMap.RoadAt(x + dx, y + dy)) return true;
            return CityMap.RoadAt(x, y);
        }

        static bool BinaBolgesi(Zone z) =>
            z == Zone.OldTown || z == Zone.Mixed || z == Zone.HighRise || z == Zone.Houses || z == Zone.Campus || z == Zone.Warehouse || z == Zone.Mall;

        /// <summary>City3D.BinaBolgesi ile aynı düzleme (5×5 çoğunluk): zemin deseni binalarla uyuşsun.</summary>
        static Zone DuzBolge(int x, int y)
        {
            var z = CityMap.ZoneOf(x, y);
            if (!BinaBolgesi(z)) return z;
            var say = new int[16];
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                {
                    var zz = CityMap.ZoneOf(x + dx, y + dy);
                    if (!BinaBolgesi(zz)) continue;
                    say[(int)zz] += (dx == 0 && dy == 0) ? 2 : 1;
                }
            int en = (int)z;
            for (int i = 0; i < say.Length; i++) if (say[i] > say[en]) en = i;
            return (Zone)en;
        }

        /// <summary>"Oyunda pastel zemin" (AtolyeAyari): Bina Atölyesi'nin zemin paleti; iri, yumuşak lekeler, derz yok.</summary>
        static Color PastelRenk(Desen d, float wx, float wy)
        {
            float n1 = Mathf.PerlinNoise(wx * 0.8f + 3.1f, wy * 0.8f + 9.7f);
            float n2 = Mathf.PerlinNoise(wx * 3.1f + 1.7f, wy * 3.1f + 4.4f);
            float leke = n1 > 0.58f ? 1f : n2 > 0.7f ? 0.5f : 0f;        // basamaklı: suluboya lekesi gibi
            switch (d)
            {
                case Desen.Cim:
                case Desen.Bahce:
                {
                    var c = Color.Lerp(new Color(0.749f, 0.878f, 0.659f), new Color(0.682f, 0.827f, 0.588f), leke);
                    if (d == Desen.Bahce)
                    {
                        int cx = Mathf.FloorToInt(wx * 14f), cy = Mathf.FloorToInt(wy * 14f);
                        float h = Hash(cx, cy, 5);
                        if (h > 0.985f) c = Hash(cx, cy, 6) < 0.5f ? new Color(0.91f, 0.38f, 0.48f) : new Color(0.96f, 0.78f, 0.29f);
                    }
                    return c;
                }
                case Desen.Kaldirim: return Color.Lerp(new Color(0.851f, 0.796f, 0.714f), new Color(0.796f, 0.733f, 0.639f), leke);
                case Desen.Plaka: return Color.Lerp(new Color(0.902f, 0.875f, 0.816f), new Color(0.847f, 0.816f, 0.749f), leke);
                case Desen.Beton: return Color.Lerp(new Color(0.84f, 0.83f, 0.80f), new Color(0.78f, 0.77f, 0.75f), leke);
                default: return Color.Lerp(new Color(0.851f, 0.753f, 0.604f), new Color(0.769f, 0.651f, 0.486f), leke);
            }
        }

        static Color Renk(Desen d, float wx, float wy)
        {
            if (AtolyeAyari.PastelZemin) return PastelRenk(d, wx, wy);
            switch (d)
            {
                case Desen.Cim:
                case Desen.Bahce:
                {
                    float n1 = Mathf.PerlinNoise(wx * 0.9f + 3.1f, wy * 0.9f + 9.7f);
                    float n2 = Mathf.PerlinNoise(wx * 5.3f + 1.7f, wy * 5.3f + 4.4f);
                    var a = d == Desen.Bahce ? new Color(0.43f, 0.62f, 0.29f) : new Color(0.47f, 0.66f, 0.32f);
                    var b = d == Desen.Bahce ? new Color(0.55f, 0.71f, 0.35f) : new Color(0.58f, 0.74f, 0.38f);
                    var c = Color.Lerp(a, b, n1) * (0.92f + 0.14f * n2);
                    if (d == Desen.Bahce)
                    {
                        // Bahçe çiçekleri ve çit dibi koyu çimen
                        int cx = Mathf.FloorToInt(wx * 14f), cy = Mathf.FloorToInt(wy * 14f);
                        float h = Hash(cx, cy, 5);
                        if (h > 0.985f) c = Hash(cx, cy, 6) < 0.5f ? new Color(0.93f, 0.45f, 0.48f) : new Color(0.97f, 0.87f, 0.42f);
                    }
                    return c;
                }
                case Desen.Kaldirim:
                {
                    // Arnavut kaldırımı: sıra sıra kaydırılmış küçük taşlar, koyu derz
                    const float S = 0.075f;
                    float v = wy / S;
                    int row = Mathf.FloorToInt(v);
                    float u = wx / S + (row % 2 == 0 ? 0f : 0.5f);
                    int col = Mathf.FloorToInt(u);
                    float fu = u - col, fv = v - row;
                    float kenar = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv));
                    float h = Hash(col, row, 11);
                    var tas = Color.Lerp(new Color(0.72f, 0.66f, 0.58f), new Color(0.82f, 0.77f, 0.68f), h);
                    tas *= 0.94f + 0.1f * Mathf.PerlinNoise(wx * 1.3f, wy * 1.3f);
                    return kenar < 0.12f ? tas * 0.72f : tas;
                }
                case Desen.Plaka:
                {
                    // Büyük kare taş döşeme, ince derz
                    const float S = 0.16f;
                    float u = wx / S, v = wy / S;
                    int cu = Mathf.FloorToInt(u), cv = Mathf.FloorToInt(v);
                    float fu = u - cu, fv = v - cv;
                    float kenar = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv));
                    float h = Hash(cu, cv, 21);
                    var tas = Color.Lerp(new Color(0.78f, 0.76f, 0.71f), new Color(0.86f, 0.84f, 0.79f), h);
                    return kenar < 0.05f ? tas * 0.8f : tas;
                }
                case Desen.Beton:
                {
                    float n = Mathf.PerlinNoise(wx * 2.1f + 7f, wy * 2.1f + 3f);
                    var c = Color.Lerp(new Color(0.60f, 0.60f, 0.58f), new Color(0.70f, 0.70f, 0.67f), n);
                    float fu = Mathf.Repeat(wx, 0.5f), fv = Mathf.Repeat(wy, 0.5f);
                    if (fu < 0.012f || fv < 0.012f) c *= 0.82f;
                    if (Mathf.PerlinNoise(wx * 0.7f + 50f, wy * 0.7f) > 0.72f) c *= 0.9f;   // yağ lekesi
                    return c;
                }
                default:
                {
                    // Nehir yatağı: kum ve çamur
                    float n = Mathf.PerlinNoise(wx * 3f + 11f, wy * 3f + 17f);
                    return Color.Lerp(new Color(0.56f, 0.50f, 0.38f), new Color(0.68f, 0.62f, 0.47f), n);
                }
            }
        }

        static void DokuUret(int W, int H)
        {
            tw = W * PikselKare; th = H * PikselKare;
            var desen = new Desen[W, H];
            var kilit = new float[W, H];
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++) desen[x, y] = KareDeseni(x, y);
            taban = new Color32[tw * th];
            kilitAgirlik = new float[tw * th];
            var agirlik = new float[6];
            for (int j = 0; j < th; j++)
            {
                float wy = -0.5f + (j + 0.5f) / PikselKare;
                for (int i = 0; i < tw; i++)
                {
                    float wx = -0.5f + (i + 0.5f) / PikselKare;
                    // Dört komşu karenin ağırlıkları (kare merkezleri tam sayıda), kenara yakın daha keskin geçiş
                    float gx = Mathf.Clamp(wx, 0f, W - 1.0001f), gy = Mathf.Clamp(wy, 0f, H - 1.0001f);
                    int ix = Mathf.FloorToInt(gx), iy = Mathf.FloorToInt(gy);
                    float fx = Yumusat(gx - ix), fy = Yumusat(gy - iy);
                    for (int q = 0; q < 6; q++) agirlik[q] = 0f;
                    agirlik[(int)desen[ix, iy]] += (1 - fx) * (1 - fy);
                    agirlik[(int)desen[ix + 1, iy]] += fx * (1 - fy);
                    agirlik[(int)desen[ix, iy + 1]] += (1 - fx) * fy;
                    agirlik[(int)desen[ix + 1, iy + 1]] += fx * fy;
                    Color c = Color.black;
                    float top = 0f;
                    for (int q = 0; q < 6; q++)
                    {
                        if (agirlik[q] < 0.02f) continue;
                        c += Renk((Desen)q, wx, wy) * agirlik[q];
                        top += agirlik[q];
                    }
                    c /= Mathf.Max(0.001f, top);
                    // Nehir kıyısında ıslak, koyu toprak bandı
                    float cuk = Arazi.NehirCukur(wx, wy);
                    if (cuk > 0.05f && cuk < 0.95f) c = Color.Lerp(c, new Color(0.42f, 0.40f, 0.30f), Mathf.Sin(cuk * Mathf.PI) * 0.45f);
                    c.a = 1f;
                    taban[j * tw + i] = c;
                }
            }
            if (doku == null || doku.width != tw || doku.height != th)
            {
                if (doku != null) Object.Destroy(doku);
                doku = new Texture2D(tw, th, TextureFormat.RGBA32, true) { name = "OrganikZeminDokusu", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            }
        }

        static float Yumusat(float f)
        {
            // Kare ortasında saf desen, kenarda dar geçiş
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.7f, f));
        }

        // ------------------------------------------------------------------ kilit, kar, ıslaklık

        /// <summary>Kilitli bölgeleri soldurur, kar ve ıslaklığı uygular (değiştiyse). CityMap.RefreshColors çağırır.</summary>
        public static void Renkle(CityMap map)
        {
            if (doku == null || taban == null || map == null) return;
            int kilit = map.UnlockLevel;
            int kar = Mathf.RoundToInt(map.KarMiktari * 8f);
            int islak = Mathf.RoundToInt(map.Islaklik * 4f);
            if (kilit == sonKilit && kar == sonKar && islak == sonIslak) return;
            sonKilit = kilit; sonKar = kar; sonIslak = islak;
            int W = CityMap.Width, H = CityMap.Height;
            var kilitli = new float[W, H];
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++) kilitli[x, y] = map.IsUnlocked(x, y) ? 0f : 1f;
            float karA = kar / 8f * 0.85f, islakA = islak / 4f * 0.3f;
            var px = new Color32[taban.Length];
            var karRenk = new Color(0.93f, 0.95f, 0.98f);
            for (int j = 0; j < th; j++)
            {
                float wy = -0.5f + (j + 0.5f) / PikselKare;
                float gy = Mathf.Clamp(wy, 0f, H - 1.0001f);
                int iy = Mathf.FloorToInt(gy);
                float fy = Yumusat(gy - iy);
                for (int i = 0; i < tw; i++)
                {
                    float wx = -0.5f + (i + 0.5f) / PikselKare;
                    float gx = Mathf.Clamp(wx, 0f, W - 1.0001f);
                    int ix = Mathf.FloorToInt(gx);
                    float fx = Yumusat(gx - ix);
                    float kl = Mathf.Lerp(Mathf.Lerp(kilitli[ix, iy], kilitli[ix + 1, iy], fx), Mathf.Lerp(kilitli[ix, iy + 1], kilitli[ix + 1, iy + 1], fx), fy);
                    Color c = taban[j * tw + i];
                    if (islakA > 0f) c = Color.Lerp(c, new Color(c.r * 0.62f, c.g * 0.66f, c.b * 0.78f + 0.04f), islakA);
                    if (karA > 0f) c = Color.Lerp(c, karRenk, karA);
                    if (kl > 0.001f) c = Color.Lerp(c, CityMap.LockedTint(c), kl);
                    c.a = 1f;
                    px[j * tw + i] = c;
                }
            }
            doku.SetPixels32(px);
            doku.Apply(true);
        }
    }
}
