using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KuryeTycoon
{
    /// <summary>
    /// Çayır çimi (organik şehir): Manor Lords'taki gibi canlı, rüzgârla dalgalanan çimen örtüsü. Kendi kodumuzla üretilir.
    ///  • Öbekler: koddan üretilen 7 mesh. Kısa çim, uçları sarkan uzun ot, pembe-bej tohum başlı ot, eğrelti ve üç
    ///    çiçekli çim (kısa çimin içinde serpiştirilmiş mor, mavi, beyaz, sarı, kırmızı çiçekler). Gölgelendirici KuryeTycoon/Cim (Resources/Gorsel/Cim.shader).
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

        const string GercekciKey = "KuryeTycoon.CimGercekci";
        static int gercekciCache = -1;
        /// <summary>Deneme: stilize yaprak geometrisi yerine dokulu çim kartları (Resources/Gorsel/CimKart.png + KuryeTycoon/CimKart).</summary>
        public static bool Gercekci
        {
            get { if (gercekciCache < 0) gercekciCache = PlayerPrefs.GetInt(GercekciKey, 0); return gercekciCache == 1; }
            set { gercekciCache = value ? 1 : 0; PlayerPrefs.SetInt(GercekciKey, gercekciCache); PlayerPrefs.Save(); }
        }

        // Dünya: 1 kare = 10 m, −Z yukarı. Öbek mesh'i yarıçap 1, boy 1 (yerel −Z); örnek ölçeği aşağıda.
        const float OlcekXY = 0.1f;            // leke yarıçapı ~1 m (bir lekede 28–36 yaprak); lekeler üst üste biner, boşluk kalmaz
        const float BoyMin = 0.045f, BoyMax = 0.075f;   // 45–75 cm; uzun ot alanında ×1,6'ya kadar (bel boyu), şehirde ×0,6
        const int Izgara = 12;                 // kare başına 12 × 12 leke (~83 cm aralık, sarsılmış ızgara)
        const float KirPay = 43f;              // kır zemini KirOrman ile aynı kuşak
        const float KirZeminZ = 0.012f;

        const int Kisa = 0, Uzun = 1, Tohum = 2, Egr = 3, Cicek0 = 4, TurSayisi = 7;   // 4–6: üç çiçek karışımı

        Mesh[] meshler, kartMeshler;
        Material malzeme, kartMalzeme;
        Mesh[] Meshler => Gercekci && kartMalzeme != null ? kartMeshler : meshler;
        Material Malzeme => Gercekci && kartMalzeme != null ? kartMalzeme : malzeme;
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
            c.meshler[Kisa] = KisaCim(11);
            c.meshler[Uzun] = UzunOt(23);
            c.meshler[Tohum] = TohumluOt(37);
            c.meshler[Egr] = Egrelti(47);
            c.meshler[Cicek0] = CicekliCim(41, new[] { new[] { Mor, Sari }, new[] { Mavi, Beyaz }, new[] { Beyaz, Sari } });       // mor, peygamber çiçeği, papatya
            c.meshler[Cicek0 + 1] = CicekliCim(53, new[] { new[] { Kirmizi, Koyu }, new[] { Beyaz, Sari }, new[] { Mor, Sari } }); // gelincik
            c.meshler[Cicek0 + 2] = CicekliCim(67, new[] { new[] { Sari, Sari }, new[] { Pembe, Beyaz }, new[] { Beyaz, Sari } }); // düğün çiçeği, yonca
            // Gerçekçi kartlar (deneme): atlas hücreleri 0 uzun ot · 1 kısa çim · 2 tohumlu · 3 kuru ot · 4 eğrelti · 5–7 çiçek
            var ksh = Shader.Find("KuryeTycoon/CimKart");
            var atlas = Resources.Load<Texture2D>("Gorsel/CimKart");
            if (ksh != null && atlas != null)
            {
                atlas.wrapMode = TextureWrapMode.Clamp;
                atlas.mipMapBias = -0.6f;   // uzakta yapraklar incelip kaybolmasın
                c.kartMalzeme = new Material(ksh) { name = "CimKart", enableInstancing = true, mainTexture = atlas };
                c.kartMeshler = new Mesh[TurSayisi];
                c.kartMeshler[Kisa] = KartObegi(111, new[] { 1, 1, 3, 1 }, 0.85f);
                c.kartMeshler[Uzun] = KartObegi(123, new[] { 0, 0, 3, 0 }, 1f);
                c.kartMeshler[Tohum] = KartObegi(137, new[] { 2, 0, 2, 3 }, 1f);
                c.kartMeshler[Egr] = KartObegi(147, new[] { 4, 4, 4, 4 }, 0.9f);
                c.kartMeshler[Cicek0] = KartObegi(141, new[] { 5, 1, 5, 1 }, 0.85f);
                c.kartMeshler[Cicek0 + 1] = KartObegi(153, new[] { 6, 1, 6, 1 }, 0.85f);
                c.kartMeshler[Cicek0 + 2] = KartObegi(167, new[] { 7, 1, 7, 1 }, 0.85f);
            }
            else Debug.LogWarning("Kurye Tycoon: gerçekçi çim (CimKart) gölgelendiricisi ya da dokusu yok; stilize çim kullanılır.");
            if (banliyoEvleri != null)
                foreach (var p in banliyoEvleri) c.kirDolu.Add(new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y)));
        }

        void Awake() { I = this; }
        void OnDestroy()
        {
            if (I == this) I = null;
            if (meshler != null) foreach (var m in meshler) if (m != null) Destroy(m);
            if (kartMeshler != null) foreach (var m in kartMeshler) if (m != null) Destroy(m);
            if (malzeme != null) Destroy(malzeme);
            if (kartMalzeme != null) Destroy(kartMalzeme);
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
                // Çayır yamaları (Manor Lords'taki gibi): uzun ot alanı, kısa çim alanı, eğrelti kümeleri, toprak açıklıkları
                float uzunAlan = Mathf.PerlinNoise(p.x * 0.16f + 31.7f, p.y * 0.16f + 5.3f) * 0.75f + Mathf.PerlinNoise(p.x * 0.6f + 8.1f, p.y * 0.6f + 77.2f) * 0.25f;
                float egrAlan = Mathf.PerlinNoise(p.x * 0.22f + 101.3f, p.y * 0.22f + 12.9f);
                if (kir && Mathf.PerlinNoise(p.x * 0.5f + 55.5f, p.y * 0.5f + 3.3f) < 0.17f && Hash(x, y, n * 7 + 99) < 0.85f) continue;   // toprak açıklığı
                float tur = Hash(x, y, n * 7 + 101);
                float uzunOran = kir ? Mathf.SmoothStep(0.25f, 0.75f, uzunAlan) : 0f;
                int t;
                if (tur < (kir ? 0.07f : 0.035f)) t = Cicek0 + (int)(Hash(x, y, n * 7 + 103) * 2.999f);
                else if (tur < (kir ? 0.07f + 0.16f * uzunOran : 0.06f)) t = Tohum;
                else if (kir && egrAlan > 0.66f && Hash(x, y, n * 7 + 105) < 0.45f) t = Egr;
                else t = Hash(x, y, n * 7 + 107) < uzunOran ? Uzun : Kisa;
                float boy = Mathf.Lerp(BoyMin, BoyMax, Hash(x, y, n * 7 + 109));
                if (kir) boy *= Mathf.Lerp(0.85f, 1.6f, uzunOran);   // uzun ot alanında bel boyu
                else boy *= 0.6f;                                    // şehirde biçilmiş çimen
                if (t == Egr) boy *= 1.1f;
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
            Graphics.DrawMeshInstanced(Meshler[tur], 0, Malzeme, tampon, n, null, ShadowCastingMode.Off, true, gameObject.layer);

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

        static readonly string[] RenkAdlari = { "_Uc", "_Taban" };
        static readonly string[] SayiAdlari = { "_KuruOran", "_Kar", "_RuzgarGuc", "_RuzgarHiz", "_Gorunum" };

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
            if (kartMalzeme != null && Gercekci)
            {
                // Kart gölgelendiricisi aynı mevsim / rüzgâr / kar ayarlarını kullanır
                foreach (var ad in RenkAdlari) kartMalzeme.SetColor(ad, malzeme.GetColor(ad));
                foreach (var ad in SayiAdlari) kartMalzeme.SetFloat(ad, malzeme.GetFloat(ad));
                kartMalzeme.SetVector("_RuzgarYon", malzeme.GetVector("_RuzgarYon"));
            }
        }

        // ------------------------------------------------------------------ mesh üretimi

        static readonly Vector3 Yukari = new Vector3(0f, 0f, -1f);
        static float R(System.Random r) => (float)r.NextDouble();

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

            /// <summary>Yaprağın orta çizgisi: kökten dışa açılır, uçta aşağı sarkar (Manor Lords'taki uzun ot gibi).</summary>
            public static Vector3 Cizgi(Vector2 kok, Vector2 yon, float boy, float acilma, float sarkma, float s)
            {
                float disari = acilma * boy * Mathf.Pow(s, 1.5f);
                float yukari = boy * (s - sarkma * s * s * s);
                return new Vector3(kok.x + yon.x * disari, kok.y + yon.y * disari, 0f) + Yukari * yukari;
            }

            /// <summary>Bir yaprak: kökten uca daralan şerit, P parça. sarkma: uç ne kadar aşağı düşer (0 dik, 0,6 çok sarkık).</summary>
            public void Yaprak(Vector2 kok, Vector2 yon, float boy, float en, float acilma, float sarkma, int P, float tohum)
            {
                var yan = new Vector3(-yon.y, yon.x, 0f);
                var nr = (new Vector3(yon.x, yon.y, 0f) * 0.7f + Yukari * 0.3f).normalized;
                int o0 = -1, o1 = -1;
                for (int i = 0; i <= P; i++)
                {
                    float s = i / (float)P;
                    var m = Cizgi(kok, yon, boy, acilma, sarkma, s);
                    float w = en * Mathf.Pow(1f - s, 0.75f) * 0.5f + (i == P ? 0f : en * 0.04f);
                    int a = Kose(m - yan * w, nr, Color.white, s, tohum, 0f);
                    int b = Kose(m + yan * w, nr, Color.white, s, tohum, 0f);
                    if (o0 >= 0) { Ucgen(o0, o1, b); Ucgen(o0, b, a); }
                    o0 = a; o1 = b;
                }
            }

            /// <summary>Küçük elmas (tohum başakçığı, eğrelti yaprakçığı, çiçek tacı). Renk köşe renginden gelir (bas = 1).</summary>
            public void Elmas(Vector3 merkez, Vector3 boyYon, Vector3 enYon, float uzun, float gen, Color renk, float t, float tohum, float bas)
            {
                var nr = Yukari;
                int a = Kose(merkez - boyYon * uzun * 0.5f, nr, renk, t, tohum, bas);
                int b = Kose(merkez + enYon * gen, nr, renk, t, tohum, bas);
                int d = Kose(merkez + boyYon * uzun * 0.5f, nr, renk, t, tohum, bas);
                int e = Kose(merkez - enYon * gen, nr, renk, t, tohum, bas);
                Ucgen(a, b, d); Ucgen(a, d, e);
            }

            /// <summary>Çiçek başı: tepeden görünen küçük yıldız (taç) ve ortada göbek.</summary>
            public void CicekBasi(System.Random r, Vector3 merkez, float yaricap, int yaprak, Color tac, Color gobek)
            {
                float tohum = R(r), donus = R(r) * Mathf.PI * 2f;
                int o = Kose(merkez + Yukari * yaricap * 0.15f, Yukari, gobek, 1f, tohum, 1f);
                for (int i = 0; i < yaprak; i++)
                {
                    float a0 = donus + (i - 0.33f) / yaprak * Mathf.PI * 2f, a1 = donus + (i + 0.33f) / yaprak * Mathf.PI * 2f, am = donus + i / (float)yaprak * Mathf.PI * 2f;
                    var p0 = merkez + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * yaricap * 0.4f;
                    var p1 = merkez + new Vector3(Mathf.Cos(am), Mathf.Sin(am), 0f) * yaricap - Yukari * yaricap * 0.15f;
                    var p2 = merkez + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * yaricap * 0.4f;
                    int a = Kose(p0, Yukari, tac, 1f, tohum, 1f), b = Kose(p1, Yukari, tac, 1f, tohum, 1f), d = Kose(p2, Yukari, tac, 1f, tohum, 1f);
                    Ucgen(o, a, b); Ucgen(o, b, d);
                }
            }
        }

        static Vector2 RastgeleKok(System.Random r, float yaricap, out Vector2 yon)
        {
            float a = R(r) * Mathf.PI * 2f, u = Mathf.Sqrt(R(r)) * yaricap;
            var kok = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * u;
            yon = (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (0.4f + u) + new Vector2(R(r) - 0.5f, R(r) - 0.5f)).normalized;
            return kok;
        }

        /// <summary>Kısa çim: sık, dik, ince yapraklar (biçilmiş çimen ve çayırın alt katı).</summary>
        static Mesh KisaCim(int tohum)
        {
            var r = new System.Random(tohum); var y = new Yapici();
            for (int i = 0; i < 44; i++)
            {
                var kok = RastgeleKok(r, 1f, out var yon);
                y.Yaprak(kok, yon, Mathf.Lerp(0.45f, 1f, R(r)), Mathf.Lerp(0.05f, 0.08f, R(r)), Mathf.Lerp(0.1f, 0.35f, R(r)), Mathf.Lerp(0f, 0.2f, R(r)), 2, R(r));
            }
            return y.Mesh("Cim_kisa");
        }

        /// <summary>Uzun çayır otu: ortadan yükselip dışa açılan, uçları aşağı sarkan ince uzun yapraklar.</summary>
        static Mesh UzunOt(int tohum)
        {
            var r = new System.Random(tohum); var y = new Yapici();
            for (int i = 0; i < 32; i++)
            {
                var kok = RastgeleKok(r, 0.7f, out var yon);
                float boy = Mathf.Lerp(0.6f, 1.15f, R(r)) * (1f - kok.magnitude * 0.3f);
                y.Yaprak(kok, yon, boy, Mathf.Lerp(0.045f, 0.07f, R(r)), Mathf.Lerp(0.35f, 0.9f, R(r)), Mathf.Lerp(0.25f, 0.65f, R(r)), 3, R(r));
            }
            return y.Mesh("Cim_uzun");
        }

        /// <summary>Tohum başlı ot: çevrede yapraklar, ortada ince saplar ve sapın üst kısmına dizilen pembe-bej başakçıklar.</summary>
        static Mesh TohumluOt(int tohum)
        {
            var r = new System.Random(tohum); var y = new Yapici();
            for (int i = 0; i < 18; i++)
            {
                var kok = RastgeleKok(r, 0.8f, out var yon);
                y.Yaprak(kok, yon, Mathf.Lerp(0.4f, 0.75f, R(r)), 0.055f, Mathf.Lerp(0.3f, 0.8f, R(r)), Mathf.Lerp(0.3f, 0.6f, R(r)), 2, R(r));
            }
            var pembe = new Color(0.80f, 0.62f, 0.56f); var bej = new Color(0.86f, 0.78f, 0.58f); var mor = new Color(0.66f, 0.52f, 0.58f);
            for (int i = 0; i < 9; i++)
            {
                var kok = RastgeleKok(r, 0.5f, out var yon);
                float boy = Mathf.Lerp(0.95f, 1.35f, R(r)), acilma = Mathf.Lerp(0.1f, 0.4f, R(r)), sarkma = Mathf.Lerp(0.05f, 0.25f, R(r));
                float th = R(r);
                y.Yaprak(kok, yon, boy, 0.022f, acilma, sarkma, 3, th);
                var renk = R(r) < 0.45f ? pembe : R(r) < 0.6f ? bej : mor;
                var yan = new Vector3(-yon.y, yon.x, 0f);
                for (int k = 0; k < 7; k++)
                {
                    float s = Mathf.Lerp(0.62f, 1f, k / 6f);
                    var m = Yapici.Cizgi(kok, yon, boy, acilma, sarkma, s);
                    var ileri = (Yapici.Cizgi(kok, yon, boy, acilma, sarkma, Mathf.Min(1f, s + 0.05f)) - m).normalized;
                    float taraf = (k % 2 == 0 ? 1f : -1f) * Mathf.Lerp(0.05f, 0.02f, k / 6f);
                    y.Elmas(m + yan * taraf, ileri, yan, 0.1f, 0.022f, renk * Mathf.Lerp(0.9f, 1.08f, R(r)), 1f, th, 1f);
                }
            }
            return y.Mesh("Cim_tohumlu");
        }

        /// <summary>Eğrelti: dışa kıvrılan 6–8 yaprak sapı, iki yanında daralan yaprakçıklar (orman kıyısı ve gölge yerler).</summary>
        static Mesh Egrelti(int tohum)
        {
            var r = new System.Random(tohum); var y = new Yapici();
            int adet = 6 + r.Next(3);
            for (int i = 0; i < adet; i++)
            {
                float a = (i + R(r) * 0.5f) / adet * Mathf.PI * 2f;
                var yon = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var kok = yon * 0.08f;
                float boy = Mathf.Lerp(0.7f, 1f, R(r)), acilma = Mathf.Lerp(0.9f, 1.3f, R(r)), sarkma = Mathf.Lerp(0.45f, 0.6f, R(r));
                float th = R(r);
                y.Yaprak(kok, yon, boy, 0.03f, acilma, sarkma, 3, th);
                var yan = new Vector3(-yon.y, yon.x, 0f);
                for (int k = 1; k <= 9; k++)
                {
                    float s = k / 10f;
                    var m = Yapici.Cizgi(kok, yon, boy, acilma, sarkma, s);
                    var ileri = (Yapici.Cizgi(kok, yon, boy, acilma, sarkma, s + 0.05f) - m).normalized;
                    float uz = 0.32f * Mathf.Sin(s * Mathf.PI) * (1.1f - s * 0.5f);
                    for (int sg = -1; sg <= 1; sg += 2)
                    {
                        var dis = (yan * sg + ileri * 0.35f).normalized;
                        // yaprakçık: sapın yanından dışa uzanan ince şerit (renk normal yaprak gibi mevsimden)
                        var p0 = m; var p1 = m + dis * uz;
                        int q0 = y.Kose(p0 - ileri * 0.03f, Yukari, Color.white, s, th, 0f);
                        int q1 = y.Kose(p0 + ileri * 0.03f, Yukari, Color.white, s, th, 0f);
                        int q2 = y.Kose(p1 - Yukari * 0.02f, Yukari, Color.white, Mathf.Min(1f, s + 0.15f), th, 0f);
                        y.Ucgen(q0, q1, q2);
                    }
                }
            }
            return y.Mesh("Cim_egrelti");
        }

        static readonly Color Beyaz = new Color(0.96f, 0.95f, 0.90f), Sari = new Color(0.98f, 0.84f, 0.20f),
            Mor = new Color(0.55f, 0.40f, 0.85f), Mavi = new Color(0.40f, 0.48f, 0.92f), Kirmizi = new Color(0.88f, 0.20f, 0.15f),
            Pembe = new Color(0.86f, 0.52f, 0.72f), Koyu = new Color(0.14f, 0.10f, 0.10f);

        /// <summary>Serpiştirilmiş kır çiçekleri: kısa çimin içinde lekeye dağılmış 4–6 ince saplı küçük çiçek (öbek değil).</summary>
        static Mesh CicekliCim(int tohum, Color[][] palet)
        {
            var r = new System.Random(tohum); var y = new Yapici();
            for (int i = 0; i < 26; i++)
            {
                var kok = RastgeleKok(r, 1f, out var yon);
                y.Yaprak(kok, yon, Mathf.Lerp(0.4f, 0.8f, R(r)), 0.06f, Mathf.Lerp(0.2f, 0.6f, R(r)), Mathf.Lerp(0.1f, 0.4f, R(r)), 2, R(r));
            }
            int adet = 4 + r.Next(3);
            for (int i = 0; i < adet; i++)
            {
                var kok = RastgeleKok(r, 0.9f, out var yon);
                float boy = Mathf.Lerp(0.8f, 1.2f, R(r));
                y.Yaprak(kok, yon, boy, 0.025f, 0.15f, 0.05f, 2, R(r));
                var uc = Yapici.Cizgi(kok, yon, boy, 0.15f, 0.05f, 1f);
                var p = palet[r.Next(palet.Length)];
                y.CicekBasi(r, uc, Mathf.Lerp(0.05f, 0.08f, R(r)), 5 + r.Next(2), p[0], p[1]);
            }
            return y.Mesh("Cim_cicekli");
        }

        // ------------------------------------------------------------------ gerçekçi kartlar (deneme)

        /// <summary>Dokulu çim öbeği: 3 dikey kart yıldız gibi kesişir (60°), 4. kart dışa eğik. hucreler: her kartın atlas hücresi
        /// (0–7; 4 × 2 ızgara). en: kartın yarı genişliği (yerel birim; ~0,9 → öbek ~1,8 m genişliğinde). Yerel ölçü: yarıçap 1, boy 1 (yukarı −Z), Cim örnek ölçeğiyle büyür.</summary>
        static Mesh KartObegi(int tohum, int[] hucreler, float en)
        {
            var r = new System.Random(tohum);
            var y = new Yapici();
            float donus = R(r) * Mathf.PI;
            for (int k = 0; k < 4; k++)
            {
                int h = hucreler[k];
                float u0 = (h % 4) / 4f, u1 = u0 + 0.25f, v0 = h < 4 ? 0.5f : 0f, v1 = v0 + 0.5f;   // PNG üst satırı = v 0,5–1
                float a = donus + (k < 3 ? k * Mathf.PI / 3f : R(r) * Mathf.PI * 2f);
                var yan = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var ileri = new Vector3(-yan.y, yan.x, 0f);
                float egik = k < 3 ? (R(r) - 0.5f) * 0.25f : 0.55f;         // 4. kart dışa yatık (tepeden bakınca da dolu görünsün)
                var kayma = new Vector3(R(r) - 0.5f, R(r) - 0.5f, 0f) * 0.25f;
                float w = en * (0.85f + R(r) * 0.3f), boy = 1.35f + R(r) * 0.3f;
                var ust = Yukari * boy + ileri * (egik * boy);
                var nr = (ileri * 0.4f + Yukari * 0.6f).normalized;
                int q0 = y.Kose(kayma - yan * w, nr, Color.white, 0f, 0f, 0f);
                int q1 = y.Kose(kayma + yan * w, nr, Color.white, 0f, 0f, 0f);
                int q2 = y.Kose(kayma + yan * w + ust, nr, Color.white, 1f, 0f, 0f);
                int q3 = y.Kose(kayma - yan * w + ust, nr, Color.white, 1f, 0f, 0f);
                y.uv[q0] = new Vector2(u0, v0); y.uv[q1] = new Vector2(u1, v0); y.uv[q2] = new Vector2(u1, v1); y.uv[q3] = new Vector2(u0, v1);
                y.Ucgen(q0, q1, q2); y.Ucgen(q0, q2, q3);
            }
            return y.Mesh("CimKart");
        }
    }
}
