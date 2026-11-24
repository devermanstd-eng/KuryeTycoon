using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KuryeTycoon
{
    /// <summary>
    /// Çayır çimi (organik şehir): Manor Lords'taki gibi canlı, rüzgârla dalgalanan çimen örtüsü. Kendi kodumuzla üretilir.
    ///  • Öbekler: koddan üretilen 6 mesh. İki çim öbeği (kıvrık, sivri uçlu yapraklar), tohum başlı uzun ot, üç kır çiçeği
    ///    öbeği (papatya, düğün çiçeği, peygamber çiçeği, gelincik, yonca). Gölgelendirici KuryeTycoon/Cim (Resources/Gorsel/Cim.shader).
    ///  • Yerleşim: kare başına (10 m) 12 × 12 sarsılmış ızgarada 1 m'lik lekeler (üst üste biner, zemin görünmez), karenin kendi tohumuyla. Şehirde çimen ve bahçe desenli karelerde
    ///    (OrganikZemin.CimAgirligi); yol, su, bina, meydan ve kaldırımda yok. Kırda tarla, yol, çiftlik, köy ve banliyö evlerinin
    ///    dışında (KirOrman.AcikAlan); nehir kıyısında yok. Çiçek ve tohumlu ot kırda daha çok.
    ///  • Çizim: yalnızca kamera yakınken (MapCamera.Zoom ≥ ~0,45; uzaklaşırken boy kısalarak kaybolur), ekranın gördüğü karelerde,
    ///    Graphics.DrawMeshInstanced ile. Kare başına öbek listesi ilk görüldüğünde üretilir ve saklanır. Toplam öbek sınırı grafik
    ///    kalitesine göre (düşük 16 000, orta 38 000, yüksek 70 000). Kameranın baktığı yere kadar tam yoğunluk, ötesi uzaklıkla incelir.
    ///  • Mevsim (KirDurum): ilkbaharda taze yeşil ve bol çiçek, yazın koyu yeşil, kurakta sarı yamalar, sonbaharda altın-kahve, kışın
    ///    solgun ve karla örtülü (kar CityMap.KarMiktari). Rüzgâr havaya göre (fırtınada çok güçlü, sıcakta durgun).
    ///  • Ayarlar > Grafik'ten kapatılabilir (PlayerPrefs "KuryeTycoon.Cim").
    /// </summary>
    public class Cim : MonoBehaviour
    {
        public static Cim I { get; private set; }

        const string AyarKey = "KuryeTycoon.Cim";
        static int ayarCache = -1;
        public static bool Acik
        {
            get { if (ayarCache < 0) ayarCache = PlayerPrefs.GetInt(AyarKey, 1); return ayarCache == 1; }
            set { ayarCache = value ? 1 : 0; PlayerPrefs.SetInt(AyarKey, ayarCache); PlayerPrefs.Save(); }
        }

        // Dünya: 1 kare = 10 m, −Z yukarı. Öbek mesh'i yarıçap 1, boy 1 (yerel −Z); örnek ölçeği aşağıda.
        const float OlcekXY = 0.1f;            // leke yarıçapı ~1 m (bir lekede 28–36 yaprak); lekeler üst üste biner, boşluk kalmaz
        const float BoyMin = 0.03f, BoyMax = 0.06f;     // 30–60 cm (çayır otu)
        const int Izgara = 12;                 // kare başına 12 × 12 leke (~83 cm aralık, sarsılmış ızgara)
        const float KirPay = 43f;              // kır zemini KirOrman ile aynı kuşak
        const float KirZeminZ = 0.012f;

        const int Cim0 = 0, Cim1 = 1, Tohum = 2, Cicek0 = 3, Cicek1 = 4, Cicek2 = 5, TurSayisi = 6;

        Mesh[] meshler;
        Material malzeme;
        CityMap map;
        float[,] sehirAgirlik;                 // şehir karesinin çim ağırlığı (0–1), bir kez hesaplanır
        readonly HashSet<Vector2Int> kirDolu = new HashSet<Vector2Int>();   // banliyö evleri

        class KareVeri { public Matrix4x4[][] m = new Matrix4x4[TurSayisi][]; public int toplam; }
        readonly Dictionary<long, KareVeri> onbellek = new Dictionary<long, KareVeri>();
        readonly Matrix4x4[] tampon = new Matrix4x4[1023];
        readonly List<KareVeri> gorunen = new List<KareVeri>();
        readonly List<float> payKatsayi = new List<float>();

        /// <summary>Kurulum (City3D, kır kurulduktan sonra). banliyoEvleri: kırdaki ev noktaları (çim girmesin).</summary>
        public static void Kur(Transform parent, CityMap map, IList<Vector2> banliyoEvleri)
        {
            if (!CityMap.Organik || map == null) return;
            var sh = Shader.Find("KuryeTycoon/Cim");
            if (sh == null) { Debug.LogWarning("Kurye Tycoon: KuryeTycoon/Cim gölgelendiricisi bulunamadı, çayır çimi kapalı."); return; }
            var go = new GameObject("Çayır çimi");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Cim>();
            c.map = map;
            c.malzeme = new Material(sh) { name = "Cim", enableInstancing = true };
            c.meshler = new Mesh[TurSayisi];
            c.meshler[Cim0] = OtOrbegi(11, 36, false);
            c.meshler[Cim1] = OtOrbegi(23, 30, false);
            c.meshler[Tohum] = OtOrbegi(37, 24, true);
            c.meshler[Cicek0] = CicekObegi(41);
            c.meshler[Cicek1] = CicekObegi(53);
            c.meshler[Cicek2] = CicekObegi(67);
            if (banliyoEvleri != null)
                foreach (var p in banliyoEvleri) c.kirDolu.Add(new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y)));
        }

        void Awake() { I = this; }
        void OnDestroy()
        {
            if (I == this) I = null;
            if (meshler != null) foreach (var m in meshler) if (m != null) Destroy(m);
            if (malzeme != null) Destroy(malzeme);
        }

        // ------------------------------------------------------------------ yerleşim

        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(s * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFF) / 65535f;
            }
        }

        float SehirAgirligi(int x, int y)
        {
            int W = CityMap.Width, H = CityMap.Height;
            if (sehirAgirlik == null)
            {
                sehirAgirlik = new float[W, H];
                var binalar = City3D.I != null ? City3D.I.BuildingHeights : null;
                for (int i = 0; i < W; i++)
                    for (int j = 0; j < H; j++)
                    {
                        float a = OrganikZemin.CimAgirligi(i, j);
                        if (a > 0f && (CityMap.RoadAt(i, j) || CityMap.WaterAt(i, j))) a = 0f;
                        if (a > 0f && binalar != null && binalar.ContainsKey(new Vector2Int(i, j))) a = 0f;
                        sehirAgirlik[i, j] = a;
                    }
            }
            return x >= 0 && y >= 0 && x < W && y < H ? sehirAgirlik[x, y] : 0f;
        }

        /// <summary>Noktadaki çim yoğunluğu (0–1) ve zemin yüksekliği. kir: kırda mı (çiçek daha çok).</summary>
        bool Uygun(Vector2 p, out float z, out float yogun, out bool kir)
        {
            z = 0f; yogun = 0f;
            int W = CityMap.Width, H = CityMap.Height;
            kir = p.x < -0.5f || p.y < -0.5f || p.x > W - 0.5f || p.y > H - 0.5f;
            if (!kir)
            {
                int tx = Mathf.RoundToInt(p.x), ty = Mathf.RoundToInt(p.y);
                yogun = SehirAgirligi(tx, ty);
                if (yogun <= 0f) return false;
                if (Arazi.NehirCukur(p.x, p.y) > 0.12f) return false;
                z = Arazi.Z(p.x, p.y) - 0.0006f;
                return true;
            }
            float u = Mathf.Max(Mathf.Max(-p.x, p.x - (W - 1)), Mathf.Max(-p.y, p.y - (H - 1)));
            if (u > KirPay - 1f) return false;
            if (!KirOrman.AcikAlan(p) || CityMap.KirNehirYakin(p, 0.3f)) return false;
            if (kirDolu.Contains(new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y)))) return false;
            // Çayır lekeleri: kimi yer gür, kimi yer seyrek (Manor Lords'taki gibi düzensiz)
            float leke = Mathf.PerlinNoise(p.x * 0.11f + 13.7f, p.y * 0.11f + 41.2f) * 0.7f + Mathf.PerlinNoise(p.x * 0.45f + 2.2f, p.y * 0.45f + 7.9f) * 0.3f;
            yogun = Mathf.Lerp(0.9f, 1f, Mathf.SmoothStep(0.25f, 0.7f, leke));
            z = KirZeminZ - Arazi.Tepe(p.x, p.y);
            return true;
        }

        static long Anahtar(int x, int y) => ((long)(x + 100000) << 32) | (uint)(y + 100000);

        int buKareUretilen;
        const int KareBasiUretim = 120;   // bir karede en çok bu kadar yeni kare hazırlanır (ilk bakışta takılma olmasın)

        KareVeri KareGetir(int x, int y)
        {
            long k = Anahtar(x, y);
            if (onbellek.TryGetValue(k, out var v)) return v;
            if (buKareUretilen >= KareBasiUretim) return null;
            buKareUretilen++;
            v = new KareVeri();
            var listeler = new List<Matrix4x4>[TurSayisi];
            for (int i = 0; i < TurSayisi; i++) listeler[i] = new List<Matrix4x4>();
            // Sırayı karıştır: uzakta inceltilirken (dizinin başı alınır) kalanlar karenin her yerine dağılsın
            var sira = new int[Izgara * Izgara];
            for (int i = 0; i < sira.Length; i++) sira[i] = i;
            for (int i = sira.Length - 1; i > 0; i--) { int j = (int)(Hash(x, y, 900 + i) * (i + 0.999f)); (sira[i], sira[j]) = (sira[j], sira[i]); }
            foreach (int n in sira)
            {
                float gx = (n % Izgara + 0.15f + 0.7f * Hash(x, y, n * 3 + 1)) / Izgara, gy = (n / Izgara + 0.15f + 0.7f * Hash(x, y, n * 3 + 2)) / Izgara;
                var p = new Vector2(x - 0.5f + gx, y - 0.5f + gy);
                if (!Uygun(p, out float z, out float yogun, out bool kir)) continue;
                float h = Hash(x, y, n * 3 + 3);
                if (h > yogun) continue;
                float tur = Hash(x, y, n * 7 + 101);
                float cicekOran = kir ? 0.08f : 0.04f;
                int t = tur < cicekOran ? Cicek0 + (int)(Hash(x, y, n * 7 + 103) * 2.999f)
                      : tur < cicekOran + (kir ? 0.12f : 0.04f) ? Tohum
                      : (Hash(x, y, n * 7 + 107) < 0.5f ? Cim0 : Cim1);
                float boy = Mathf.Lerp(BoyMin, BoyMax, Hash(x, y, n * 7 + 109)) * Mathf.Lerp(0.8f, 1.1f, yogun);
                if (t >= Cicek0) boy *= 0.9f;
                if (!kir) boy *= 0.75f;  // şehirde biçilmiş çimen, kırda diz boyu çayır
                float s = OlcekXY * Mathf.Lerp(0.8f, 1.25f, Hash(x, y, n * 7 + 113));
                var rot = Quaternion.Euler(0f, 0f, Hash(x, y, n * 7 + 127) * 360f);
                listeler[t].Add(Matrix4x4.TRS(new Vector3(p.x, p.y, z), rot, new Vector3(s, s, boy)));
            }
            for (int i = 0; i < TurSayisi; i++) { v.m[i] = listeler[i].ToArray(); v.toplam += v.m[i].Length; }
            if (onbellek.Count > 3000) onbellek.Clear();   // çok uzun oyunda bellek şişmesin
            onbellek[k] = v;
            return v;
        }

        // ------------------------------------------------------------------ çizim

        static int Butce => GrafikKalitesi.Seviye == GrafikKalitesi.Yuksek ? 70000 : GrafikKalitesi.Seviye == GrafikKalitesi.Orta ? 38000 : 16000;

        void LateUpdate()
        {
            if (!Acik || malzeme == null || MapCamera.I == null || !MapCamera.I.ShowModels) return;
            var cam = Camera.main;
            if (cam == null) return;
            float zoom = MapCamera.I.Zoom;
            float gorunum = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.6f, zoom));
            if (gorunum <= 0.01f) return;

            // Ekranın köşelerinin zemindeki karşılıkları → görünen kare dikdörtgeni
            var px = cam.pixelRect;
            float xmin = float.MaxValue, ymin = float.MaxValue, xmax = float.MinValue, ymax = float.MinValue;
            int bulunan = 0;
            for (int i = 0; i < 4; i++)
            {
                var sp = new Vector2(i % 2 == 0 ? px.xMin : px.xMax, i < 2 ? px.yMin : px.yMax);
                var ray = cam.ScreenPointToRay(sp);
                if (ray.direction.z <= 1e-4f) continue;   // ufka bakan köşe (−Z yukarı: zemine inen ışın +Z gider)
                float tt = -ray.origin.z / ray.direction.z;
                if (tt <= 0f) continue;
                var w = ray.origin + ray.direction * tt;
                xmin = Mathf.Min(xmin, w.x); xmax = Mathf.Max(xmax, w.x);
                ymin = Mathf.Min(ymin, w.y); ymax = Mathf.Max(ymax, w.y);
                bulunan++;
            }
            if (bulunan < 2) return;
            int W = CityMap.Width, H = CityMap.Height;
            int x0 = Mathf.Max(Mathf.FloorToInt(xmin) - 1, -(int)KirPay), x1 = Mathf.Min(Mathf.CeilToInt(xmax) + 1, W - 1 + (int)KirPay);
            int y0 = Mathf.Max(Mathf.FloorToInt(ymin) - 1, -(int)KirPay), y1 = Mathf.Min(Mathf.CeilToInt(ymax) + 1, H - 1 + (int)KirPay);
            if ((long)(x1 - x0 + 1) * (y1 - y0 + 1) > 2500) return;   // çok geniş görüş: çim zaten görünmez

            buKareUretilen = 0;
            // Yakına öncelik: kameranın baktığı noktaya kadar tam yoğunluk, ötesinde uzaklıkla incelir
            var cp = cam.transform.position;
            var orta = cam.ScreenPointToRay(new Vector2(px.center.x, px.center.y));
            float yakin = orta.direction.z > 1e-4f ? (-orta.origin.z / orta.direction.z) : 30f;
            gorunen.Clear(); payKatsayi.Clear();
            float toplam = 0f;
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    var k = KareGetir(x, y);
                    if (k == null || k.toplam == 0) continue;
                    float d = Vector3.Distance(cp, new Vector3(x, y, 0f));
                    float f = Mathf.Lerp(1f, 0.15f, Mathf.InverseLerp(yakin * 1.15f, yakin * 2.2f, d));
                    gorunen.Add(k); payKatsayi.Add(f);
                    toplam += k.toplam * f;
                }
            if (toplam <= 0f) return;
            float oran = Mathf.Min(1f, Butce / toplam);

            MalzemeyiAyarla(gorunum);
            bool cicekVar = malzeme.GetFloat("_Kar") < 0.3f && CicekMevsimi > 0.05f;
            for (int tur = 0; tur < TurSayisi; tur++)
            {
                if (tur >= Cicek0 && !cicekVar) continue;
                float turOran = tur >= Cicek0 ? oran * CicekMevsimi : oran;
                int dolu = 0;
                for (int g = 0; g < gorunen.Count; g++)
                {
                    var arr = gorunen[g].m[tur];
                    float o = turOran * payKatsayi[g];
                    int n = o >= 0.999f ? arr.Length : Mathf.CeilToInt(arr.Length * o);
                    for (int j = 0; j < n; j++)
                    {
                        tampon[dolu++] = arr[j];
                        if (dolu == tampon.Length) { Ciz(tur, dolu); dolu = 0; }
                    }
                }
                if (dolu > 0) Ciz(tur, dolu);
            }
        }

        void Ciz(int tur, int n) =>
            Graphics.DrawMeshInstanced(meshler[tur], 0, malzeme, tampon, n, null, ShadowCastingMode.Off, true, gameObject.layer);

        // ------------------------------------------------------------------ mevsim ve rüzgâr

        // KirDurum sırası: taze, yaz, kurak, sonbahar, kış
        static readonly Color[] UcRenk =
        {
            new Color(0.56f, 0.78f, 0.28f), new Color(0.46f, 0.68f, 0.24f), new Color(0.72f, 0.70f, 0.32f),
            new Color(0.70f, 0.60f, 0.28f), new Color(0.50f, 0.58f, 0.32f),
        };
        static readonly Color[] TabanRenk =
        {
            new Color(0.26f, 0.42f, 0.15f), new Color(0.22f, 0.37f, 0.13f), new Color(0.34f, 0.36f, 0.16f),
            new Color(0.34f, 0.29f, 0.14f), new Color(0.28f, 0.34f, 0.17f),
        };
        static readonly float[] KuruOran = { 0.1f, 0.22f, 0.6f, 0.55f, 0.4f };
        static readonly float[] Cicekler = { 1f, 0.75f, 0.35f, 0.15f, 0f };

        float CicekMevsimi { get; set; } = 1f;
        float sonMevsim = -999f;

        void MalzemeyiAyarla(float gorunum)
        {
            if (Time.unscaledTime - sonMevsim > 2f)
            {
                sonMevsim = Time.unscaledTime;
                var w = KirDurum.Agirliklar();
                Color uc = Color.black, taban = Color.black;
                float kuru = 0f, cicek = 0f;
                for (int i = 0; i < 5; i++)
                {
                    uc += UcRenk[i] * w[i]; taban += TabanRenk[i] * w[i];
                    kuru += KuruOran[i] * w[i]; cicek += Cicekler[i] * w[i];
                }
                malzeme.SetColor("_Uc", uc);
                malzeme.SetColor("_Taban", taban);
                malzeme.SetFloat("_KuruOran", kuru);
                CicekMevsimi = cicek;
                malzeme.SetFloat("_Kar", map != null ? Mathf.Clamp01(map.KarMiktari) : 0f);

                float guc = 0.4f;
                var hava = WeatherSystem.I != null ? WeatherSystem.I.Current : WeatherKind.Gunesli;
                switch (hava)
                {
                    case WeatherKind.Firtina: guc = 1.25f; break;
                    case WeatherKind.Yagmurlu: guc = 0.75f; break;
                    case WeatherKind.Bulutlu: guc = 0.55f; break;
                    case WeatherKind.Soguk: guc = 0.6f; break;
                    case WeatherKind.Karli: guc = 0.5f; break;
                    case WeatherKind.Sicak: guc = 0.2f; break;
                }
                malzeme.SetFloat("_RuzgarGuc", guc);
                malzeme.SetFloat("_RuzgarHiz", 0.25f + guc * 0.35f);
                // Rüzgâr yönü günden güne yavaşça döner
                float aci = (GameClock.I != null ? GameClock.I.Day * 0.7f : 0f) + 0.6f;
                malzeme.SetVector("_RuzgarYon", new Vector4(Mathf.Cos(aci), Mathf.Sin(aci), 0f, 0f));
            }
            malzeme.SetFloat("_Gorunum", gorunum);
        }

        // ------------------------------------------------------------------ mesh üretimi

        static readonly Vector3 Yukari = new Vector3(0f, 0f, -1f);

        class Yapici
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Color> c = new List<Color>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();

            public int Kose(Vector3 p, Vector3 nr, Color renk, float yukseklik, float tohum, float bas)
            {
                v.Add(p); n.Add(nr); renk.a = yukseklik; c.Add(renk); uv.Add(new Vector2(tohum, bas));
                return v.Count - 1;
            }

            public void Ucgen(int a, int b, int d) { t.Add(a); t.Add(b); t.Add(d); }

            public Mesh Mesh(string ad)
            {
                var m = new Mesh { name = ad };
                m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetUVs(0, uv); m.SetTriangles(t, 0);
                m.RecalculateBounds();
                // Rüzgârla eğilince kenarda kesilmesin
                var b = m.bounds; b.Expand(new Vector3(1.5f, 1.5f, 0.5f)); m.bounds = b;
                return m;
            }

            /// <summary>Bir yaprak: kökten uca daralan, dışa kıvrılan şerit (4 parça + sivri uç). tohumBasi: uçta başak.</summary>
            public void Yaprak(System.Random r, Vector2 kok, float boy, float en, Vector2 yon, float kivrim, Color basRenk, bool tohumBasi)
            {
                float tohum = (float)r.NextDouble();
                var yon3 = new Vector3(yon.x, yon.y, 0f);
                var yan = new Vector3(-yon.y, yon.x, 0f) * (1f + ((float)r.NextDouble() - 0.5f) * 0.4f);
                var nr = (yon3 * 0.8f + Yukari * 0.2f).normalized;
                const int P = 2;
                int onceki0 = -1, onceki1 = -1;
                for (int i = 0; i <= P; i++)
                {
                    float s = i / (float)P;
                    var merkez = new Vector3(kok.x, kok.y, 0f) + yon3 * (kivrim * s * s * boy) + Yukari * (boy * s * (1f - 0.18f * kivrim * s));
                    float w = en * Mathf.Pow(1f - s * 0.92f, 0.8f) * 0.5f;
                    int a = Kose(merkez - yan * w, nr, Color.white, s, tohum, 0f);
                    int b = Kose(merkez + yan * w, nr, Color.white, s, tohum, 0f);
                    if (onceki0 >= 0) { Ucgen(onceki0, onceki1, b); Ucgen(onceki0, b, a); }
                    onceki0 = a; onceki1 = b;
                }
                var uc = new Vector3(kok.x, kok.y, 0f) + yon3 * (kivrim * 1.12f * boy) + Yukari * (boy * 1.08f * (1f - 0.18f * kivrim));
                int u = Kose(uc, nr, Color.white, 1f, tohum, 0f);
                Ucgen(onceki0, onceki1, u);
                if (tohumBasi)
                {
                    // Başak: ucun hemen altında, yaprağa dik iki ince elmas (renk köşe renginden)
                    var bm = uc - Yukari * (boy * 0.12f);
                    for (int k = 0; k < 2; k++)
                    {
                        var ek = k == 0 ? yan.normalized : yon3;
                        float bw = en * 0.9f, bh = boy * 0.16f;
                        int a = Kose(bm - Yukari * bh, nr, basRenk, 1f, tohum, 1f);
                        int b = Kose(bm + ek * bw, nr, basRenk, 1f, tohum, 1f);
                        int d = Kose(bm + Yukari * bh * 0.8f, nr, basRenk, 1f, tohum, 1f);
                        int e = Kose(bm - ek * bw, nr, basRenk, 1f, tohum, 1f);
                        Ucgen(a, b, d); Ucgen(a, d, e);
                    }
                }
            }

            /// <summary>Çiçek başı: tepeden görünen yıldız (taç yapraklar) ve ortada göbek; yandan da görünsün diye hafif çanak.</summary>
            public void CicekBasi(System.Random r, Vector3 merkez, float yaricap, int yaprak, Color tac, Color gobek)
            {
                float tohum = (float)r.NextDouble();
                float donus = (float)r.NextDouble() * Mathf.PI * 2f;
                int o = Kose(merkez + Yukari * yaricap * 0.15f, Yukari, gobek, 1f, tohum, 1f);
                for (int i = 0; i < yaprak; i++)
                {
                    float a0 = donus + (i - 0.32f) / yaprak * Mathf.PI * 2f, a1 = donus + (i + 0.32f) / yaprak * Mathf.PI * 2f, am = donus + i / (float)yaprak * Mathf.PI * 2f;
                    var p0 = merkez + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * yaricap * 0.45f;
                    var p1 = merkez + new Vector3(Mathf.Cos(am), Mathf.Sin(am), 0f) * yaricap - Yukari * yaricap * 0.2f;
                    var p2 = merkez + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * yaricap * 0.45f;
                    int a = Kose(p0, Yukari, tac, 1f, tohum, 1f), b = Kose(p1, Yukari, tac, 1f, tohum, 1f), d = Kose(p2, Yukari, tac, 1f, tohum, 1f);
                    Ucgen(o, a, b); Ucgen(o, b, d);
                }
                // Göbek: küçük üçgen kümesi
                for (int i = 0; i < 3; i++)
                {
                    float a0 = donus + i * 2.094f;
                    int a = Kose(merkez + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * yaricap * 0.3f + Yukari * yaricap * 0.1f, Yukari, gobek, 1f, tohum, 1f);
                    int b = Kose(merkez + new Vector3(Mathf.Cos(a0 + 2.094f), Mathf.Sin(a0 + 2.094f), 0f) * yaricap * 0.3f + Yukari * yaricap * 0.1f, Yukari, gobek, 1f, tohum, 1f);
                    Ucgen(o, a, b);
                }
            }
        }

        /// <summary>Çim öbeği: yaprak sayısı kadar kıvrık yaprak, ortada uzun dışta kısa. tohumlu: birkaç yaprağın ucunda başak.</summary>
        static Mesh OtOrbegi(int tohum, int yaprakSayisi, bool tohumlu)
        {
            var r = new System.Random(tohum);
            var y = new Yapici();
            var basRenk = new Color(0.80f, 0.71f, 0.46f);
            for (int i = 0; i < yaprakSayisi; i++)
            {
                float a = (float)(i / (double)yaprakSayisi * Mathf.PI * 2f + r.NextDouble() * 0.6);
                float uz = Mathf.Sqrt((float)r.NextDouble());
                var kok = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * uz;
                var yon = (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) + new Vector2((float)r.NextDouble() - 0.5f, (float)r.NextDouble() - 0.5f) * 0.6f).normalized;
                float boy = Mathf.Lerp(0.55f, 1f, (float)r.NextDouble()) * (1f - uz * 0.25f);
                bool bas = tohumlu && i % 3 == 0;
                if (bas) boy = Mathf.Min(1.25f, boy * 1.35f);
                float en = Mathf.Lerp(0.08f, 0.12f, (float)r.NextDouble()) * (bas ? 0.7f : 1f);
                float kivrim = Mathf.Lerp(0.1f, 0.45f, (float)r.NextDouble()) * (bas ? 0.4f : 1f);
                y.Yaprak(r, kok, boy, en, yon, kivrim, basRenk, bas);
            }
            return y.Mesh(tohumlu ? "Cim_tohumlu" : "Cim_obek");
        }

        static readonly Color[][] CicekPalet =
        {
            new[] { new Color(0.96f, 0.95f, 0.90f), new Color(0.98f, 0.82f, 0.22f) },   // papatya
            new[] { new Color(0.98f, 0.86f, 0.18f), new Color(0.90f, 0.70f, 0.12f) },   // düğün çiçeği
            new[] { new Color(0.42f, 0.48f, 0.90f), new Color(0.30f, 0.32f, 0.72f) },   // peygamber çiçeği
            new[] { new Color(0.86f, 0.20f, 0.16f), new Color(0.12f, 0.10f, 0.10f) },   // gelincik
            new[] { new Color(0.78f, 0.52f, 0.76f), new Color(0.62f, 0.38f, 0.62f) },   // yonca
        };

        /// <summary>Kır çiçeği öbeği: birkaç ot yaprağı ve 3–5 saplı çiçek (karışık renk).</summary>
        static Mesh CicekObegi(int tohum)
        {
            var r = new System.Random(tohum);
            var y = new Yapici();
            for (int i = 0; i < 14; i++)
            {
                float a = (float)(r.NextDouble() * Mathf.PI * 2f);
                var kok = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Sqrt((float)r.NextDouble()) * 0.9f;
                y.Yaprak(r, kok, Mathf.Lerp(0.4f, 0.75f, (float)r.NextDouble()), 0.1f, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 0.6f, Color.white, false);
            }
            int adet = 4 + r.Next(4);
            int anaTur = r.Next(CicekPalet.Length);
            for (int i = 0; i < adet; i++)
            {
                float a = (float)(r.NextDouble() * Mathf.PI * 2f);
                var kok = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Sqrt((float)r.NextDouble()) * 0.8f;
                float boy = Mathf.Lerp(0.75f, 1.15f, (float)r.NextDouble());
                var yon = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                // Sap: ince, az kıvrık yaprak
                y.Yaprak(r, kok, boy, 0.035f, yon, 0.15f, Color.white, false);
                var uc = new Vector3(kok.x, kok.y, 0f) + new Vector3(yon.x, yon.y, 0f) * (0.15f * 1.12f * boy) + Yukari * (boy * 1.08f * (1f - 0.18f * 0.15f));
                var p = r.NextDouble() < 0.6 ? CicekPalet[anaTur] : CicekPalet[r.Next(CicekPalet.Length)];
                y.CicekBasi(r, uc, Mathf.Lerp(0.08f, 0.12f, (float)r.NextDouble()), 5 + r.Next(3), p[0], p[1]);
            }
            return y.Mesh("Cim_cicek");
        }
    }
}
