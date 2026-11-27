using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Nehrin hayatı (organik şehir): nehir boyunca gidip gelen tekneler (gezi teknesi, kayık, mavna), suyun üstünde
    /// süzülen ördek aileleri, kıyılarda sazlık. Tekneler köprülerin altından geçer (su yüzeyi zeminin altında).
    /// Nehrin orta çizgisi su karelerinden bulunur (en uzak iki su karesi arasındaki yol, yumuşatılmış).
    /// Modeller Bina Atölyesi parçalarıdır (hedefi "nehir:…": gezi teknesi, dar kanal teknesi, kürekli kayık, ördek, kuğu,
    /// sazlık, nilüfer; AtolyeUretici.Nehir): pastel gölgelendirici ve kalem çizgisiyle şehrin geri kalanıyla aynı dilde.
    /// Sazlıklar tek mesh'te birleşir ve rüzgârda eğilir (köşe animasyonu); teknelerin bayrağı dalgalanır. Atölye çevre
    /// modelleri kapalıysa ya da kütüphane yoksa eski, kutulardan kurulan tekneler kullanılır.
    /// </summary>
    public class NehirHayati : MonoBehaviour
    {
        const float SuZ = -0.0015f;        // OrganikSehir3D.ZSu ile aynı (−Z yukarı)
        readonly List<Vector2> yol = new List<Vector2>();
        readonly List<float> topUz = new List<float>();
        float toplam;

        class Gezgin { public Transform t; public float s, hiz; public int yon; public float yan; public float salinim; }
        readonly List<Gezgin> tekneler = new List<Gezgin>();
        readonly List<Gezgin> ordekler = new List<Gezgin>();

        public static NehirHayati Kur(Transform parent, int tohum)
        {
            var go = new GameObject("Nehir hayatı");
            go.transform.SetParent(parent, false);
            var nh = go.AddComponent<NehirHayati>();
            nh.Hazirla(tohum);
            return nh;
        }

        void Hazirla(int tohum)
        {
            var rng = new System.Random(tohum);
            OrtaCizgi();
            if (toplam < 3f) return;
            // Tekneler
            int n = Mathf.Clamp(Mathf.RoundToInt(toplam / 18f), 2, 6);
            for (int i = 0; i < n; i++)
            {
                int tur = i % 3;
                var t = Tekne(tur, rng);
                tekneler.Add(new Gezgin { t = t, s = (float)rng.NextDouble() * toplam, hiz = tur == 1 ? 0.35f : tur == 2 ? 0.18f : 0.26f,
                    yon = rng.Next(2) == 0 ? 1 : -1, yan = ((float)rng.NextDouble() - 0.5f) * 0.6f, salinim = (float)rng.NextDouble() * 6f });
            }
            // Ördek aileleri
            int no = Mathf.Clamp(Mathf.RoundToInt(toplam / 10f), 3, 10);
            for (int i = 0; i < no; i++)
            {
                var t = OrdekAilesi(rng);
                ordekler.Add(new Gezgin { t = t, s = (float)rng.NextDouble() * toplam, hiz = 0.04f + (float)rng.NextDouble() * 0.04f,
                    yon = rng.Next(2) == 0 ? 1 : -1, yan = ((float)rng.NextDouble() - 0.5f) * 1.1f, salinim = (float)rng.NextDouble() * 6f });
            }
            Sazlik(rng);
        }

        /// <summary>En uzak iki su karesi arasındaki yolu bulur, yumuşatır.</summary>
        void OrtaCizgi()
        {
            int W = CityMap.Width, H = CityMap.Height;
            var su = new List<Vector2Int>();
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (CityMap.WaterAt(x, y) || CityMap.BridgeAt(x, y)) su.Add(new Vector2Int(x, y));
            if (su.Count < 4) return;
            var set = new HashSet<Vector2Int>(su);
            Dictionary<Vector2Int, Vector2Int> once;
            var a = Uzak(su[0], set, out _);
            var b = Uzak(a, set, out once);
            var kareler = new List<Vector2Int>();
            for (var p = b; ; p = once[p]) { kareler.Add(p); if (p == a) break; }
            // Yumuşat: kayan ortalama
            for (int i = 0; i < kareler.Count; i++)
            {
                Vector2 t = Vector2.zero; int c = 0;
                for (int k = -3; k <= 3; k++)
                {
                    int j = Mathf.Clamp(i + k, 0, kareler.Count - 1);
                    t += (Vector2)kareler[j]; c++;
                }
                yol.Add(t / c);
            }
            topUz.Add(0f);
            for (int i = 1; i < yol.Count; i++) topUz.Add(topUz[i - 1] + Vector2.Distance(yol[i - 1], yol[i]));
            toplam = topUz[topUz.Count - 1];
        }

        static Vector2Int Uzak(Vector2Int bas, HashSet<Vector2Int> set, out Dictionary<Vector2Int, Vector2Int> once)
        {
            once = new Dictionary<Vector2Int, Vector2Int> { [bas] = bas };
            var q = new Queue<Vector2Int>();
            q.Enqueue(bas);
            var son = bas;
            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down, new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1) };
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                son = p;
                foreach (var d in dirs)
                {
                    var n = p + d;
                    if (!set.Contains(n) || once.ContainsKey(n)) continue;
                    once[n] = p;
                    q.Enqueue(n);
                }
            }
            return son;
        }

        Vector2 Nokta(float s, out Vector2 yon)
        {
            s = Mathf.Clamp(s, 0f, toplam);
            int i = 1;
            while (i < topUz.Count - 1 && topUz[i] < s) i++;
            float u = topUz[i] - topUz[i - 1];
            float t = u > 1e-5f ? (s - topUz[i - 1]) / u : 0f;
            yon = (yol[i] - yol[i - 1]).normalized;
            return Vector2.Lerp(yol[i - 1], yol[i], t);
        }

        void Update()
        {
            if (toplam < 3f) return;
            float dt = Time.deltaTime * (GameClock.I != null ? (GameClock.I.Paused ? 0f : Mathf.Clamp(GameClock.I.EffectiveSpeed, 0f, 4f)) : 1f);
            foreach (var g in tekneler) Ilerle(g, dt, 0.02f);
            foreach (var g in ordekler) Ilerle(g, dt, 0.008f);
        }

        void Ilerle(Gezgin g, float dt, float sallanma)
        {
            if (g.t == null) return;
            g.s += g.hiz * g.yon * dt;
            if (g.s > toplam - 0.5f) { g.s = toplam - 0.5f; g.yon = -1; }
            if (g.s < 0.5f) { g.s = 0.5f; g.yon = 1; }
            var p = Nokta(g.s, out var yon);
            var sol = new Vector2(-yon.y, yon.x);
            float yan = g.yan + Mathf.Sin(Time.time * 0.13f + g.salinim) * 0.12f;
            var q = p + sol * yan * (g.yon > 0 ? 1f : -1f);
            // Su yüzeyinde, hafif inip kalkma
            float z = SuZ - 0.001f + Mathf.Sin(Time.time * 1.7f + g.salinim) * sallanma * 0.1f;
            g.t.position = new Vector3(q.x, q.y, z);
            var ileri = yon * g.yon;
            float aci = Mathf.Atan2(ileri.y, ileri.x) * Mathf.Rad2Deg;
            g.t.rotation = Quaternion.Euler(0f, 0f, aci) * Quaternion.Euler(Mathf.Sin(Time.time * 1.3f + g.salinim) * 3f, 0f, 0f);
        }

        // ------------------------------------------------------------------ modeller (kodla, metre → kare 0,075)

        static readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();

        static Material Mat(Color c)
        {
            if (mats.TryGetValue(c, out var m) && m != null) return m;
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(sh) { name = "Nehir_" + ColorUtility.ToHtmlStringRGB(c) };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
            mats[c] = m;
            return m;
        }

        /// <summary>Kutu parçası: yerel x ileri, y sol, z aşağı (−Z yukarı); boyutlar kare.</summary>
        static void Kutu(Transform p, Vector3 merkez, Vector3 boy, Color c, PrimitiveType tip = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(tip);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(p, false);
            go.transform.localPosition = merkez;
            go.transform.localScale = boy;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Mat(c);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // ---- atölye modelleri
        const float AtolyeOlcek = 0.085f;       // metre → kare (donatılar 0,1; tekneler nehre sığsın diye biraz küçük)
        const float SazOlcek = 0.075f;          // sazlık ve nilüferler (birleşik mesh'in taşıyıcı ölçeği: mesh metre uzayında kalır)
        // atölye mesh'i (−X ileri, Y yukarı) → gezgin ekseni (x ileri, −z yukarı)
        static readonly Quaternion AtolyeDonus = Quaternion.LookRotation(Vector3.down, Vector3.back);

        static bool AtolyeVar => AtolyeAyari.Cevre && AtolyeUretici.Nehir("tekne").Count > 0;

        /// <summary>Atölye şablonunun kopyasını gezginin altına koyar (gölge düşürür; çarpıştırıcı yok).</summary>
        static Transform AtolyeKoy(GameObject sablon, Transform ust, Vector3 yer, float olcek)
        {
            var k = Instantiate(sablon, ust, false);
            k.name = sablon.name;
            k.transform.localPosition = yer;
            k.transform.localRotation = AtolyeDonus;
            k.transform.localScale = Vector3.one * olcek;
            foreach (var r in k.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            return k.transform;
        }

        static GameObject Sec(List<GameObject> l, string icerir, System.Random rng)
        {
            if (l == null || l.Count == 0) return null;
            if (icerir != null)
                foreach (var g in l) if (g != null && g.name.Contains(icerir)) return g;
            return l[rng.Next(l.Count)];
        }

        Transform Tekne(int tur, System.Random rng)
        {
            var go = new GameObject(tur == 0 ? "Gezi teknesi" : tur == 1 ? "Kayık" : "Mavna");
            go.transform.SetParent(transform, false);
            var t = go.transform;
            if (AtolyeVar)
            {
                var sablon = Sec(AtolyeUretici.Nehir("tekne"), tur == 0 ? "tekne_gezi" : tur == 1 ? "tekne_kayik" : "tekne_kanal", rng);
                if (sablon != null)
                {
                    // gezi teknesi ve kanal teknesi uzun (13–14 m): dar nehirde kıyıya taşmasın diye biraz daha küçük
                    float olcek = AtolyeOlcek * (tur == 1 ? 1.15f : 0.8f);
                    // gövde suya gömülür (model su hattı z = 0'da, omurga biraz altında dursun; gezgin ekseninde +z aşağı)
                    AtolyeKoy(sablon, t, new Vector3(0f, 0f, (tur == 1 ? 0.12f : 0.3f) * olcek), olcek);
                    return t;
                }
            }
            const float S = 0.075f;
            Color[] govde = { new Color(0.93f, 0.92f, 0.88f), new Color(0.75f, 0.24f, 0.20f), new Color(0.20f, 0.33f, 0.55f), new Color(0.25f, 0.45f, 0.35f) };
            var g = govde[rng.Next(govde.Length)];
            if (tur == 0)
            {
                // Gezi teknesi: uzun beyaz gövde, camlı kabin, çatı, bayrak
                Kutu(t, new Vector3(0, 0, -0.3f * S), new Vector3(9f * S, 2.6f * S, 1.2f * S), g);
                Kutu(t, new Vector3(4.6f * S, 0, -0.35f * S), new Vector3(1.6f * S, 1.6f * S, 1.0f * S), g);
                Kutu(t, new Vector3(-0.3f * S, 0, -1.6f * S), new Vector3(6.2f * S, 2.2f * S, 1.4f * S), new Color(0.62f, 0.78f, 0.86f));
                Kutu(t, new Vector3(-0.3f * S, 0, -2.4f * S), new Vector3(6.8f * S, 2.6f * S, 0.15f * S), new Color(0.80f, 0.30f, 0.24f));
                Kutu(t, new Vector3(-3.6f * S, 0, -3.0f * S), new Vector3(0.08f * S, 0.08f * S, 1.2f * S), new Color(0.3f, 0.3f, 0.3f));
                Kutu(t, new Vector3(-3.3f * S, 0, -3.4f * S), new Vector3(0.6f * S, 0.04f * S, 0.4f * S), new Color(0.80f, 0.20f, 0.25f));
            }
            else if (tur == 1)
            {
                // Kayık: küçük ahşap tekne, kürekçi
                Kutu(t, new Vector3(0, 0, -0.25f * S), new Vector3(3.6f * S, 1.3f * S, 0.6f * S), new Color(0.55f, 0.38f, 0.24f));
                Kutu(t, new Vector3(0, 0, -0.52f * S), new Vector3(3.2f * S, 1.0f * S, 0.06f * S), new Color(0.70f, 0.52f, 0.34f));
                Kutu(t, new Vector3(0, 0, -1.0f * S), new Vector3(0.5f * S, 0.5f * S, 0.9f * S), g, PrimitiveType.Capsule);
                Kutu(t, new Vector3(0, 0, -1.6f * S), new Vector3(0.38f * S, 0.38f * S, 0.38f * S), new Color(0.95f, 0.80f, 0.65f), PrimitiveType.Sphere);
            }
            else
            {
                // Mavna: yassı yük teknesi, kum yığını, kaptan köşkü
                Kutu(t, new Vector3(0, 0, -0.35f * S), new Vector3(12f * S, 3.2f * S, 1.2f * S), new Color(0.24f, 0.26f, 0.30f));
                Kutu(t, new Vector3(1.2f * S, 0, -1.2f * S), new Vector3(7f * S, 2.6f * S, 0.8f * S), new Color(0.80f, 0.68f, 0.44f));
                Kutu(t, new Vector3(-5f * S, 0, -1.5f * S), new Vector3(1.6f * S, 2.2f * S, 1.8f * S), g);
                Kutu(t, new Vector3(-5f * S, 0, -2.5f * S), new Vector3(1.9f * S, 2.5f * S, 0.15f * S), new Color(0.3f, 0.3f, 0.3f));
            }
            return t;
        }

        Transform OrdekAilesi(System.Random rng)
        {
            var go = new GameObject("Ördekler");
            go.transform.SetParent(transform, false);
            const float S = 0.075f;
            int n = 2 + rng.Next(4);
            if (AtolyeVar && AtolyeUretici.Nehir("ordek").Count > 0)
            {
                // Altıda bir aile kuğu çifti; gerisi anne (ya da yeşil başlı) ördek ve arkasında dizili yavrular
                var kugular = AtolyeUretici.Nehir("kugu");
                var yavrular = AtolyeUretici.Nehir("yavru");
                if (kugular.Count > 0 && rng.Next(6) == 0)
                {
                    AtolyeKoy(kugular[0], go.transform, Vector3.zero, AtolyeOlcek * 0.9f);
                    AtolyeKoy(kugular[0], go.transform, new Vector3(-1.6f * S, 0.9f * S, 0f), AtolyeOlcek * 0.82f);
                    return go.transform;
                }
                var ordekler2 = AtolyeUretici.Nehir("ordek");
                AtolyeKoy(ordekler2[rng.Next(ordekler2.Count)], go.transform, Vector3.zero, AtolyeOlcek * 1.25f);
                for (int i = 1; i < n; i++)
                {
                    var sb = yavrular.Count > 0 ? yavrular[0] : ordekler2[0];
                    AtolyeKoy(sb, go.transform, new Vector3(-i * 0.75f * S, (i % 2 == 0 ? 0.22f : -0.22f) * S, 0f), AtolyeOlcek * (yavrular.Count > 0 ? 1.25f : 0.7f));
                }
                return go.transform;
            }
            for (int i = 0; i < n; i++)
            {
                bool anne = i == 0;
                float b = anne ? 1f : 0.6f;
                var o = new GameObject("Ördek").transform;
                o.SetParent(go.transform, false);
                o.localPosition = new Vector3(-i * 0.7f * S, (i % 2 == 0 ? 0.2f : -0.2f) * S, 0);
                var renk = anne ? (rng.Next(2) == 0 ? new Color(0.45f, 0.35f, 0.25f) : new Color(0.95f, 0.95f, 0.92f)) : new Color(0.85f, 0.75f, 0.40f);
                Kutu(o, new Vector3(0, 0, -0.15f * S * b), new Vector3(0.7f * S * b, 0.45f * S * b, 0.35f * S * b), renk, PrimitiveType.Sphere);
                Kutu(o, new Vector3(0.3f * S * b, 0, -0.42f * S * b), new Vector3(0.26f * S * b, 0.26f * S * b, 0.26f * S * b),
                     anne && renk.r < 0.6f ? new Color(0.15f, 0.45f, 0.30f) : renk, PrimitiveType.Sphere);
                Kutu(o, new Vector3(0.46f * S * b, 0, -0.42f * S * b), new Vector3(0.14f * S * b, 0.08f * S * b, 0.05f * S * b), new Color(0.95f, 0.60f, 0.15f));
            }
            return go.transform;
        }

        /// <summary>
        /// Atölye sazlıkları ve nilüferleri: kıyı şeridine saz demetleri, kıyıya yakın durgun suya nilüfer öbekleri. Malzeme
        /// başına tek mesh'te birleşir (AtolyeBirlestirici); sazlar rüzgârda eğilir, nilüferler suyla hafifçe oynar.
        /// </summary>
        void AtolyeSazlik(System.Random rng)
        {
            var sazlar = AtolyeUretici.Nehir("sazlik");
            var niluferler = AtolyeUretici.Nehir("nilufer");
            var malzeme = new Dictionary<Material, List<AtolyeBirlestirici.Ornek>>();
            void Ekle(GameObject sablon, Vector3 yer, float aci, float olcek)
            {
                var m = Matrix4x4.TRS(yer, Quaternion.Euler(0f, 0f, aci) * Quaternion.Euler(-90f, 0f, 0f), Vector3.one * olcek);
                var kokTers = sablon.transform.worldToLocalMatrix;
                foreach (var mf in sablon.GetComponentsInChildren<MeshFilter>(true))
                {
                    var r = mf.GetComponent<MeshRenderer>();
                    if (mf.sharedMesh == null || r == null) continue;
                    var mats = r.sharedMaterials;
                    var yerel = kokTers * mf.transform.localToWorldMatrix;
                    for (int i = 0; i < mf.sharedMesh.subMeshCount && i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;
                        if (!malzeme.TryGetValue(mats[i], out var l)) malzeme[mats[i]] = l = new List<AtolyeBirlestirici.Ornek>();
                        l.Add(new AtolyeBirlestirici.Ornek { mesh = mf.sharedMesh, sub = i, m = m * yerel, faz = (float)rng.NextDouble() * 6.2832f });
                    }
                }
            }
            int W = CityMap.Width, H = CityMap.Height;
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    if (!CityMap.WaterAt(x, y)) continue;
                    bool kiyi = false;
                    for (int dx = -1; dx <= 1 && !kiyi; dx++)
                        for (int dy = -1; dy <= 1 && !kiyi; dy++)
                            if (!CityMap.WaterAt(x + dx, y + dy) && !CityMap.RoadAt(x + dx, y + dy)) kiyi = true;
                    if (!kiyi || rng.NextDouble() > 0.6) continue;
                    int demet = 3 + rng.Next(5);
                    for (int i = 0; i < demet; i++)
                    {
                        var p = new Vector2(x + (float)rng.NextDouble() - 0.5f, y + (float)rng.NextDouble() - 0.5f);
                        float cuk = Arazi.NehirCukur(p.x, p.y);
                        if (cuk < 0.45f) continue;                       // kuru zemin
                        if (cuk <= 0.9f)
                            Ekle(sazlar[rng.Next(sazlar.Count)], new Vector3(p.x, p.y, Arazi.Z(p) - 0.001f), (float)rng.NextDouble() * 360f,
                                 SazOlcek * (0.75f + (float)rng.NextDouble() * 0.5f));
                        else if (niluferler.Count > 0 && cuk < 0.97f && rng.Next(3) == 0)
                            Ekle(niluferler[rng.Next(niluferler.Count)], new Vector3(p.x, p.y, SuZ - 0.0006f), (float)rng.NextDouble() * 360f,
                                 SazOlcek * (0.8f + (float)rng.NextDouble() * 0.5f));
                    }
                }
            foreach (var kv in malzeme)
                AtolyeBirlestirici.Kur(transform, "Sazlık · " + kv.Key.name, kv.Key, kv.Value, SazOlcek, false);
            AtolyeBirlestirici.Bosalt();
        }

        /// <summary>Kıyıda sazlık: su kenarındaki karelerde ince saz demetleri (tek mesh).</summary>
        void Sazlik(System.Random rng)
        {
            if (AtolyeVar && AtolyeUretici.Nehir("sazlik").Count > 0) { AtolyeSazlik(rng); return; }
            var v = new List<Vector3>();
            var tr = new List<int>();
            int W = CityMap.Width, H = CityMap.Height;
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    if (!CityMap.WaterAt(x, y)) continue;
                    bool kiyi = false;
                    for (int dx = -1; dx <= 1 && !kiyi; dx++)
                        for (int dy = -1; dy <= 1 && !kiyi; dy++)
                            if (!CityMap.WaterAt(x + dx, y + dy) && !CityMap.RoadAt(x + dx, y + dy)) kiyi = true;
                    if (!kiyi || rng.NextDouble() > 0.55) continue;
                    int demet = 3 + rng.Next(5);
                    for (int i = 0; i < demet; i++)
                    {
                        var p = new Vector2(x + (float)rng.NextDouble() - 0.5f, y + (float)rng.NextDouble() - 0.5f);
                        float cuk = Arazi.NehirCukur(p.x, p.y);
                        if (cuk < 0.45f || cuk > 0.9f) continue;     // kıyı şeridi: ne kuru zemin ne nehrin ortası
                        float zb = Arazi.Z(p) - 0.001f;
                        for (int k = 0; k < 4; k++)
                        {
                            var q = p + new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.08f;
                            float boy = 0.09f + (float)rng.NextDouble() * 0.08f;
                            var ust = new Vector3(q.x + ((float)rng.NextDouble() - 0.5f) * 0.03f, q.y + ((float)rng.NextDouble() - 0.5f) * 0.03f, zb - boy);
                            float r = 0.006f;
                            // Ön ve arka yüz AYRI köşelerle: aynı köşeleri iki yönde paylaşmak RecalculateNormals'ta sıfır normal
                            // veriyordu → gölgelendiricide NaN → bloom / alan derinliği onu kıyıda büyük siyah daireye çeviriyordu
                            var a0 = new Vector3(q.x - r, q.y, zb); var a1 = new Vector3(q.x + r, q.y, zb);
                            var c0 = new Vector3(q.x, q.y - r, zb); var c1 = new Vector3(q.x, q.y + r, zb);
                            int b0 = v.Count;
                            v.Add(a0); v.Add(a1); v.Add(ust); v.Add(a1); v.Add(a0); v.Add(ust);
                            v.Add(c0); v.Add(c1); v.Add(ust); v.Add(c1); v.Add(c0); v.Add(ust);
                            for (int j = 0; j < 12; j++) tr.Add(b0 + j);
                        }
                    }
                }
            if (v.Count == 0) return;
            var m = new Mesh { name = "Sazlik", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v);
            m.SetTriangles(tr, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            var go = new GameObject("Sazlık");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mat(new Color(0.45f, 0.58f, 0.27f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
