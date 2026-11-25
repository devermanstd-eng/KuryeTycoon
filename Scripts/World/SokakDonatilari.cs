using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KuryeTycoon
{
    /// <summary>
    /// Organik şehrin sokak donatıları (Blender: Tools/Blender/bolge_modelleri.py → Donati_*, Resources/SehirPaketi/Donatilar).
    ///  • Yol boyunca lambalar: eski şehirde, meydanda ve parkta döküm klasik lamba; diğer yerlerde modern lamba (caddede iki yanda).
    ///  • Lambaların arasında bölgeye göre: bank, çöp kutusu, ilan sütunu, saksı, posta kutusu, yangın musluğu, bisiklet parkı,
    ///    şarj istasyonu, trafo, reklam panosu, büfe ...
    ///  • Caddelerde aralıklı otobüs durakları; sokak uçlarında (kavşakta) DUR / yol ver levhası, cadde kavşaklarında trafik lambası.
    ///  • Kır yollarında ahşap elektrik direkleri, tarlalarda saman balyaları.
    /// Bütün donatılar malzeme başına tek mesh'te birleşir (binlerce parça tek tek çizilmesin). Lamba başları (YIsik_)
    /// GeceIsiklari tarafından bulunur ve geceleri yanar.
    /// Dünyada yukarı −Z; kare = 10 m. Donatılar metre ölçüsünde, Olcek ile küçültülür.
    /// </summary>
    public static class SokakDonatilari
    {
        const string Klasor = "SehirPaketi/Donatilar";
        const float Olcek = 0.1f;                    // 1 m → kare (biraz abartılı: üstten bakınca seçilsin)
        const float CaddeAsfalt = OrganikSehir3D.CaddeAsfalt, SokakAsfalt = OrganikSehir3D.SokakAsfalt, Kaldirim = OrganikSehir3D.Kaldirim;
        const float ZKaldirim = -0.003f;

        class Tur
        {
            public readonly List<(Mesh mesh, int sub, Material mat, Matrix4x4 m)> parcalar = new List<(Mesh, int, Material, Matrix4x4)>();
            public float taban;      // döndürülmüş modelin en alt noktası (dünya z, ölçek 1)
        }

        static Dictionary<string, List<Tur>> turler;

        static void Yukle()
        {
            if (turler != null) return;
            turler = new Dictionary<string, List<Tur>>();
            var atolyeTur = new HashSet<string>();
            var duz = Quaternion.Euler(-90f, 0f, 0f);
            // Bina Atölyesi donatıları (Donati_bank_atolye_1) dosya değil: oyun çalışırken kurulan şablonlar (AtolyeUretici).
            // Klasörde eski kaydedilmiş atölye prefabı kaldıysa kullanılmaz.
            var adaylar = new List<GameObject>();
            foreach (var eski in Resources.LoadAll<GameObject>(Klasor))
                if (eski != null && !AtolyeAyari.AtolyeMi(eski.name)) adaylar.Add(eski);
            if (AtolyeAyari.Cevre) adaylar.AddRange(AtolyeUretici.Donatilar());
            foreach (var p in adaylar)
            {
                if (p == null || !p.name.StartsWith("Donati_")) continue;
                bool atolye = AtolyeAyari.AtolyeMi(p.name);
                // Donati_bank_2 → "bank"; Donati_bank_atolye_1 → "bank"
                string ad = p.name.Substring(7);
                int son = ad.LastIndexOf('_');
                if (son > 0 && int.TryParse(ad.Substring(son + 1), out _)) ad = ad.Substring(0, son);
                if (ad.EndsWith("_atolye")) ad = ad.Substring(0, ad.Length - 7);
                var t = new Tur();
                float enAlt = float.NegativeInfinity;
                var kok = p.transform.worldToLocalMatrix;
                foreach (var mf in p.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    if (mf.transform.parent != null && mf.transform.parent.name == "LOD1") continue;
                    if (mf.name == "LOD1") continue;
                    var r = mf.GetComponent<MeshRenderer>();
                    if (r == null) continue;
                    var m = kok * mf.transform.localToWorldMatrix;
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mf.sharedMesh.subMeshCount && i < mats.Length; i++)
                        if (mats[i] != null) t.parcalar.Add((mf.sharedMesh, i, mats[i], m));
                    if (mf.sharedMesh.isReadable)
                    {
                        var dm = Matrix4x4.Rotate(duz) * m;
                        foreach (var v in mf.sharedMesh.vertices) enAlt = Mathf.Max(enAlt, dm.MultiplyPoint3x4(v).z);
                    }
                    else enAlt = Mathf.Max(enAlt, (Matrix4x4.Rotate(duz) * m).MultiplyPoint3x4(mf.sharedMesh.bounds.min).z);
                }
                if (t.parcalar.Count == 0) continue;
                t.taban = float.IsInfinity(enAlt) ? 0f : enAlt;
                if (!turler.TryGetValue(ad, out var l)) turler[ad] = l = new List<Tur>();
                // bir türün atölye modeli varsa o türde yalnız atölye modelleri kalır
                if (atolye && !atolyeTur.Contains(ad)) { l.Clear(); atolyeTur.Add(ad); }
                if (!atolye && atolyeTur.Contains(ad)) continue;
                l.Add(t);
            }
        }

        public static bool Var { get { Yukle(); return turler.Count > 0; } }

        // ------------------------------------------------------------------ yerleşim

        class Toplayici
        {
            public readonly Dictionary<Material, List<CombineInstance>> malzeme = new Dictionary<Material, List<CombineInstance>>();
            public readonly Dictionary<Vector2Int, List<Vector3>> dolu = new Dictionary<Vector2Int, List<Vector3>>();   // (x, y, boşluk)
            public int adet;
        }

        static float R01(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFF) / 65535f;
            }
        }

        /// <summary>Donatıyı koyar: p dünya noktası (x, y), bak: önünün baktığı yön (yol tarafı).</summary>
        static bool Koy(Toplayici top, string ad, Vector2 p, Vector2 bak, System.Random rng, float bosluk = 0.06f)
        {
            if (!turler.TryGetValue(ad, out var l) || l.Count == 0) return false;
            // Başka donatının dibine konmasın
            int hx = Mathf.FloorToInt(p.x * 2f), hy = Mathf.FloorToInt(p.y * 2f);
            for (int ix = hx - 1; ix <= hx + 1; ix++)
                for (int iy = hy - 1; iy <= hy + 1; iy++)
                    if (top.dolu.TryGetValue(new Vector2Int(ix, iy), out var ql))
                        foreach (var q in ql)
                            if ((new Vector2(q.x, q.y) - p).sqrMagnitude < (bosluk + q.z) * (bosluk + q.z)) return false;
            var t = l[rng.Next(l.Count)];
            float aci = Mathf.Atan2(-bak.x, bak.y) * Mathf.Rad2Deg;
            var rot = Quaternion.Euler(0f, 0f, aci) * Quaternion.Euler(-90f, 0f, 0f);
            var poz = new Vector3(p.x, p.y, ZKaldirim - t.taban * Olcek + Arazi.Z(p));
            var dunya = Matrix4x4.TRS(poz, rot, Vector3.one * Olcek);
            foreach (var (mesh, sub, mat, m) in t.parcalar)
            {
                if (!top.malzeme.TryGetValue(mat, out var ci)) top.malzeme[mat] = ci = new List<CombineInstance>();
                ci.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = dunya * m });
            }
            var hk = new Vector2Int(hx, hy);
            if (!top.dolu.TryGetValue(hk, out var hl)) top.dolu[hk] = hl = new List<Vector3>();
            hl.Add(new Vector3(p.x, p.y, bosluk));
            top.adet++;
            return true;
        }

        // Yol çizgilerinin ızgara dizini: bir noktanın asfalt üstünde olup olmadığını hızlı bulmak için
        static Dictionary<Vector2Int, List<(Vector2 a, Vector2 b, float r)>> izgara;

        static void IzgaraKur()
        {
            izgara = new Dictionary<Vector2Int, List<(Vector2, Vector2, float)>>();
            foreach (var c in SehirPlani.Yollar)
            {
                float r = (c.genis ? CaddeAsfalt : SokakAsfalt) * 0.5f;
                for (int k = 0; k + 1 < c.Sayi; k++)
                {
                    Vector2 a = c[k], b = c[k + 1];
                    int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - 1), x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + 1);
                    int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - 1), y1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + 1);
                    for (int x = x0; x <= x1; x++)
                        for (int y = y0; y <= y1; y++)
                        {
                            var key = new Vector2Int(x, y);
                            if (!izgara.TryGetValue(key, out var l)) izgara[key] = l = new List<(Vector2, Vector2, float)>();
                            l.Add((a, b, r));
                        }
                }
            }
        }

        /// <summary>Nokta herhangi bir yolun asfaltında (ya da paydan yakın) mı?</summary>
        static bool Asfaltta(Vector2 p, float pay)
        {
            if (!izgara.TryGetValue(new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)), out var l)) return false;
            foreach (var (a, b, r) in l)
            {
                var d = b - a;
                float t = d.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude) : 0f;
                if ((a + d * t - p).sqrMagnitude < (r + pay) * (r + pay)) return true;
            }
            return false;
        }

        static Zone BolgeAt(Vector2 p) => CityMap.ZoneOf(new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y)));

        /// <summary>Kaldırımdaki nokta donatı alır mı: suda/köprüde değil, başka yolun asfaltında değil, bina arsasında değil.</summary>
        static bool Uygun(Vector2 p)
        {
            var t = new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
            if (CityMap.WaterAt(t.x, t.y) || CityMap.BridgeAt(t.x, t.y)) return false;
            if (Asfaltta(p, 0.025f)) return false;
            var z = CityMap.ZoneOf(t);
            // Bina karesine taşmasın: yol karesi, park, meydan, nehir kıyısı ya da kır
            return CityMap.RoadAt(t.x, t.y) || z == Zone.Park || z == Zone.Plaza || z == Zone.River || z == Zone.Country;
        }

        static string Lamba(Zone z, bool genis)
        {
            if (z == Zone.OldTown || z == Zone.Plaza || z == Zone.Park) return "lamba_klasik";
            return "lamba_modern";
        }

        /// <summary>Bölgeye göre lambalar arasına konacak donatı (null: boş).</summary>
        static string AraDonati(Zone z, float r)
        {
            (string, float)[] tablo;
            switch (z)
            {
                case Zone.OldTown:
                    tablo = new[] { ("bank", .13f), ("cop_kutusu", .14f), ("saksi", .12f), ("ilan_sutunu", .05f), ("posta_kutusu", .04f),
                                    ("bisiklet_park", .04f), ("yangin_musluk", .04f), ("bufe", .02f), ("cesme", .02f) }; break;
                case Zone.Mixed:
                    tablo = new[] { ("cop_kutusu", .17f), ("bank", .09f), ("bisiklet_park", .07f), ("posta_kutusu", .05f), ("yangin_musluk", .05f),
                                    ("saksi", .08f), ("sarj", .03f), ("ilan_sutunu", .03f), ("reklam_panosu", .02f), ("trafo", .03f), ("geri_donusum", .03f), ("bufe", .02f) }; break;
                case Zone.HighRise:
                    tablo = new[] { ("cop_kutusu", .15f), ("bank", .08f), ("bisiklet_park", .08f), ("sarj", .05f), ("saksi", .1f),
                                    ("reklam_panosu", .03f), ("trafo", .03f), ("geri_donusum", .03f) }; break;
                case Zone.Houses:
                    tablo = new[] { ("cop_kutusu", .05f), ("yangin_musluk", .05f), ("posta_kutusu", .03f), ("trafo", .03f), ("geri_donusum", .03f) }; break;
                case Zone.Campus:
                    tablo = new[] { ("bisiklet_park", .2f), ("bank", .14f), ("cop_kutusu", .12f), ("ilan_sutunu", .04f) }; break;
                case Zone.Warehouse:
                    tablo = new[] { ("trafo", .08f), ("yangin_musluk", .06f), ("reklam_panosu", .05f), ("geri_donusum", .03f) }; break;
                case Zone.Mall:
                    tablo = new[] { ("cop_kutusu", .1f), ("sarj", .1f), ("reklam_panosu", .06f), ("bisiklet_park", .05f), ("saksi", .05f) }; break;
                case Zone.Park:
                case Zone.River:
                    tablo = new[] { ("bank", .25f), ("cop_kutusu", .12f), ("cesme", .04f), ("bufe", .02f) }; break;
                case Zone.Plaza:
                    tablo = new[] { ("bank", .2f), ("saksi", .2f), ("baba", .12f), ("ilan_sutunu", .08f), ("bufe", .05f) }; break;
                default:
                    return null;
            }
            float t = 0f;
            foreach (var (ad, o) in tablo)
            {
                t += o;
                if (r < t) return ad;
            }
            return null;
        }

        /// <summary>Organik şehir donatılarını kurar; parent: City3D kökü. Kurulan nesneleri döndürür.</summary>
        public static List<GameObject> Kur(Transform parent, int tohum)
        {
            var sonuc = new List<GameObject>();
            Yukle();
            if (turler.Count == 0 || !SehirPlani.Aktif) return sonuc;
            IzgaraKur();
            var rng = new System.Random(tohum * 31 + 7);
            var top = new Toplayici();

            int ci = 0;
            foreach (var c in SehirPlani.Yollar)
            {
                ci++;
                if (c.Sayi < 2) continue;
                float yari = (c.genis ? CaddeAsfalt : SokakAsfalt) * 0.5f;
                float ofs = yari + Kaldirim * 0.55f;
                float lambaAra = c.genis ? 1.9f : 2.4f;
                float durakSonra = 4f + (float)rng.NextDouble() * 6f;
                float yol = 0f, sonrakiLamba = 0.8f + (float)rng.NextDouble(), sonrakiAra = 0.4f + (float)rng.NextDouble();
                int lambaNo = 0;
                for (int k = 0; k + 1 < c.Sayi; k++)
                {
                    Vector2 a = c[k], b = c[k + 1];
                    float L = Vector2.Distance(a, b);
                    if (L < 1e-4f) continue;
                    var yon = (b - a) / L;
                    var sol = new Vector2(-yon.y, yon.x);
                    for (float d = 0f; d < L; d += 0.1f)
                    {
                        float s = yol + d;
                        var m = a + yon * d;
                        if (s >= sonrakiLamba)
                        {
                            sonrakiLamba = s + lambaAra;
                            lambaNo++;
                            // Sokakta lambalar bir sağda bir solda; caddede iki yanda karşılıklı
                            for (int taraf = -1; taraf <= 1; taraf += 2)
                            {
                                if (!c.genis && (lambaNo % 2 == 0) != (taraf > 0)) continue;
                                var p = m + sol * (ofs * taraf);
                                if (!Uygun(p)) continue;
                                Koy(top, Lamba(BolgeAt(p), c.genis), p, -sol * taraf, rng, 0.08f);
                            }
                        }
                        if (s >= sonrakiAra)
                        {
                            sonrakiAra = s + 0.38f + (float)rng.NextDouble() * 0.35f;
                            int taraf = rng.Next(2) == 0 ? -1 : 1;
                            var p = m + sol * (ofs * taraf);
                            if (!Uygun(p)) continue;
                            if (c.genis && s >= durakSonra)
                            {
                                if (Koy(top, "durak", p, -sol * taraf, rng, 0.16f)) durakSonra = s + 9f + (float)rng.NextDouble() * 6f;
                                continue;
                            }
                            var ad = AraDonati(BolgeAt(p), (float)rng.NextDouble() * 0.7f);
                            if (ad != null) Koy(top, ad, p, -sol * taraf, rng, ad == "bufe" || ad == "reklam_panosu" ? 0.14f : 0.07f);
                        }
                    }
                    yol += L;
                }

                // Sokak uçları: kavşağa yaklaşırken sağda levha; caddede trafik lambası
                for (int uc = 0; uc < 2; uc++)
                {
                    Vector2 p0 = uc == 0 ? c[0] : c[c.Sayi - 1];
                    Vector2 p1 = uc == 0 ? c[1] : c[c.Sayi - 2];
                    var d = p1 - p0;
                    if (d.sqrMagnitude < 1e-6f) continue;
                    d.Normalize();
                    // uca doğru giden araç için sağ taraf
                    var gelis = -d;
                    var sag = new Vector2(gelis.y, -gelis.x);
                    var p = p0 + d * (yari + 0.35f) + sag * ofs;
                    if (!Uygun(p)) continue;
                    float r = (float)rng.NextDouble();
                    string ad = c.genis ? (r < 0.35f ? "trafik_lambasi" : r < 0.55f ? "isaret_yaya" : null)
                                        : (r < 0.3f ? "isaret_yol_ver" : r < 0.45f ? "isaret_dur" : r < 0.52f ? "isaret_park" : null);
                    if (ad != null) Koy(top, ad, p, gelis, rng, 0.06f);
                }
            }

            // Kır yolları: elektrik direkleri (bir yanda), tarlalarda saman balyaları
            int w = CityMap.Width, h = CityMap.Height;
            foreach (var r in CityMap.CountryRoads)
            {
                var d = r.b - r.a;
                float L = d.magnitude;
                if (L < 0.5f) continue;
                var yon = d / L;
                var yan = new Vector2(-yon.y, yon.x);
                for (float s = 0.7f; s < L; s += 2.2f)
                {
                    var m = r.a + yon * s;
                    bool icerde = m.x > -0.5f && m.y > -0.5f && m.x < w - 0.5f && m.y < h - 0.5f;
                    if (icerde) continue;
                    var p = m + yan * 0.42f;
                    if (Asfaltta(p, 0.05f)) continue;
                    Koy(top, "elektrik_diregi", p, -yan, rng, 0.1f);
                    if (rng.NextDouble() < 0.12)
                    {
                        var q = m - yan * (0.9f + (float)rng.NextDouble() * 1.6f) + yon * (float)(rng.NextDouble() - 0.5);
                        if (!CityMap.KirYoluYakin(q, 0.5f)) Koy(top, "saman_balyasi", q, new Vector2((float)rng.NextDouble() - 0.5f, 1f).normalized, rng, 0.25f);
                    }
                }
            }

            // Birleştir: malzeme başına tek mesh (65 bin köşeyi aşarsa parçalara böl)
            foreach (var kv in top.malzeme)
            {
                var liste = kv.Value;
                int bas = 0;
                while (bas < liste.Count)
                {
                    int kose = 0, son = bas;
                    while (son < liste.Count && kose < 400000)
                    {
                        kose += liste[son].mesh.vertexCount;
                        son++;
                    }
                    var parca = liste.GetRange(bas, son - bas).ToArray();
                    var mesh = new Mesh { name = "Donatilar_" + kv.Key.name, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(parca, true, true);
                    mesh.RecalculateBounds();
                    var go = new GameObject("Donatılar · " + kv.Key.name);
                    go.transform.SetParent(parent, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = kv.Key;
                    mr.shadowCastingMode = ShadowCastingMode.On;
                    mr.receiveShadows = true;
                    sonuc.Add(go);
                    bas = son;
                }
            }
            Debug.Log($"Kurye Tycoon · sokak donatıları: {top.adet} parça, {sonuc.Count} birleşik mesh");
            return sonuc;
        }
    }
}
