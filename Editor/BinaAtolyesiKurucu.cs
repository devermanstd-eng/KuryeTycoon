using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// Bina Atölyesi'nin birleştiricisi: blok tasarımından (BinaTasarimi) komşuluk kurallarıyla modüler pastel parçaları seçer
    /// ve tek mesh'e birleştirir (malzemeye göre alt-mesh).
    ///  • Parçalar FBX değil: Tools/Blender/pastel_parcalar.py'nin yazdığı Editor/BinaAtolyesi/pastel_parcalar.json'dan okunur
    ///    (Blender gerekmez). Parça koordinatları model ekseninde (metre, Z yukarı, ön −Y); burada Unity'ye çevrilir
    ///    (x, y, z) → (−x, z, −y): öteki kendi modellerimiz gibi Y yukarı, ön +Z (City3D prefabı −90° X ile yatırır).
    ///  • Kurallar pastel_parcalar.py'deki yerlesim() ile AYNI (karma fonksiyonu dahil): birini değiştirince öbürünü de değiştir.
    ///  • Malzemeler adla kurulur: "Pastel_&lt;etiket&gt;_&lt;rrggbb&gt;" → KuryeTycoon/Pastel gölgelendiricisi, renk addan.
    /// </summary>
    public static class BinaAtolyesiKurucu
    {
        public const string KutuphaneYol = "Assets/KuryeTycoon/Editor/BinaAtolyesi/pastel_parcalar.json";
        const string MalzemeKlasor = "Assets/KuryeTycoon/Modeller/Malzemeler";
        public const int Apartman = 0, Fachwerk = 1, Tas = 2, Kaide = 3, Merdiven = 4;
        public static readonly string[] TarzAd = { "Pastel apartman", "Yarı ahşap (fachwerk) ev", "Taş kule", "Taş kaide / teras", "Taş merdiven" };
        public static readonly string[] Bolgeler = { "eski", "karma", "kule", "ev", "kampus", "sanayi", "avm" };

        // ---------------------------------------------------------------- kütüphane (JSON)

        [Serializable] public class AltVeri { public string malzeme; public int[] uc; }
        [Serializable] public class ParcaVeri { public string ad, grup, aciklama; public float[] v; public AltVeri[] alt; }
        [Serializable]
        public class Kutuphane
        {
            public int surum;
            public float hucre = 2.8f, kat = 3f;
            public int izgara = 5;
            public string[] duvarRenk, catiRenk, vurguRenk;
            public ParcaVeri[] parcalar, hafifler;
        }

        static Kutuphane kut;
        static Dictionary<string, ParcaVeri> parca, hafif;

        public static Kutuphane Kut
        {
            get
            {
                if (kut == null) Yukle();
                return kut;
            }
        }

        public static float Hucre => Kut != null ? Kut.hucre : 2.8f;
        public static float KatBoyu => Kut != null ? Kut.kat : 3f;
        public static int Izgara => Kut != null ? Kut.izgara : 5;

        /// <summary>Parça kütüphanesini (yeniden) okur. Dosya yoksa ya da bozuksa false.</summary>
        public static bool Yukle()
        {
            kut = null;
            parca = new Dictionary<string, ParcaVeri>();
            hafif = new Dictionary<string, ParcaVeri>();
            string metin = null;
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(KutuphaneYol);
            if (ta != null) metin = ta.text;
            else
            {
                string tam = Path.Combine(Application.dataPath, "KuryeTycoon/Editor/BinaAtolyesi/pastel_parcalar.json");
                if (File.Exists(tam)) metin = File.ReadAllText(tam);
            }
            if (string.IsNullOrEmpty(metin))
            {
                Debug.LogError("Bina Atölyesi: parça kütüphanesi yok: " + KutuphaneYol + " (python3 Tools/Blender/pastel_parcalar.py --json)");
                return false;
            }
            try { kut = JsonUtility.FromJson<Kutuphane>(metin); }
            catch (Exception ex) { Debug.LogError("Bina Atölyesi: parça kütüphanesi okunamadı: " + ex.Message); return false; }
            if (kut == null || kut.parcalar == null || kut.parcalar.Length == 0) { kut = null; return false; }
            foreach (var p in kut.parcalar) parca[p.ad] = p;
            if (kut.hafifler != null) foreach (var p in kut.hafifler) hafif[p.ad] = p;
            return true;
        }

        public static Color Renk(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
        }

        // ---------------------------------------------------------------- kurallar (pastel_parcalar.py: yerlesim)

        struct Renkler { public string D, d, C, c, K; }

        struct Yer
        {
            public string ad;
            public int x, y, don;
            public float z, zs;
            public Vector2 kay;
            public Renkler r;
        }

        struct Cokgen { public Vector3[] p; public string mat; public Vector3 n; }

        static readonly Vector2Int[] Yon = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) };
        static readonly string[] AptUst = { "duvar_pencere", "duvar_pencere", "duvar_cift", "duvar_kemerli", "duvar_saksi", "duvar_panjur", "duvar_dar", "duvar_balkon", "duvar_duz", "duvar_yuvarlak" };
        static readonly string[] FwUst = { "fachwerk_pencere", "fachwerk_capraz", "fachwerk_saksi", "fachwerk_x", "fachwerk_pencere" };
        static readonly string[] TasUst = { "tas_duvar", "tas_mazgal", "tas_kemerli", "tas_duvar" };
        static readonly HashSet<string> CatiKarolari = new HashSet<string> { "cati_duz", "cati_egim", "cati_kose", "cati_vadi", "cati_semer" };

        static uint Karma(int a, int b, int c, int d)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791) ^ ((uint)d * 2654435761u);
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return h;
            }
        }

        static int Sec(uint h, int n) => (int)(h % (uint)n);
        /// <summary>Tabana yuvarlayan ikiye bölme (Python'daki // 2).</summary>
        static int Yari(int v) => v >= 0 ? v / 2 : -((1 - v) / 2);

        static string Koyu(string hex, float f)
        {
            Color32 c = Renk(hex);
            return "#" + ((int)(c.r * f)).ToString("x2") + ((int)(c.g * f)).ToString("x2") + ((int)(c.b * f)).ToString("x2");
        }

        class Durum
        {
            public BinaTasarimi t;
            public Dictionary<Vector3Int, BinaTasarimi.Blok> blok = new Dictionary<Vector3Int, BinaTasarimi.Blok>();
            public List<Yer> yerler = new List<Yer>();
            public List<Cokgen> cokgenler = new List<Cokgen>();
            public bool kapiVar;
            public Vector2Int kapi;      // (x, y)

            public bool Var(int x, int y, int k) => blok.ContainsKey(new Vector3Int(x, y, k));
            public bool Dolu(int x, int y, int k) => blok.TryGetValue(new Vector3Int(x, y, k), out var b) && b.tarz != Merdiven;

            public Renkler RenkleriAl(BinaTasarimi.Blok b)
            {
                var kt = Kut;
                string dv = kt.duvarRenk[((b.renk % kt.duvarRenk.Length) + kt.duvarRenk.Length) % kt.duvarRenk.Length];
                string ct = kt.catiRenk[((b.cati % kt.catiRenk.Length) + kt.catiRenk.Length) % kt.catiRenk.Length];
                string vr = kt.vurguRenk[Sec(Karma(t.tohum, b.renk, b.cati, 7), kt.vurguRenk.Length)];
                return new Renkler { D = dv, d = Koyu(dv, 0.9f), C = ct, c = Koyu(ct, 0.86f), K = vr };
            }

            public void Koy(string ad, int x, int y, float z, int don, Renkler r, float zs = 1f, float kx = 0f, float ky = 0f)
            {
                yerler.Add(new Yer { ad = ad, x = x, y = y, z = z, don = ((don % 4) + 4) % 4, zs = zs, kay = new Vector2(kx, ky), r = r });
            }
        }

        static Durum Yerlesim(BinaTasarimi t)
        {
            float A = Hucre * 0.5f, KAT = KatBoyu;
            var s = new Durum { t = t };
            foreach (var b in t.bloklar) s.blok[new Vector3Int(b.x, b.y, b.k)] = b;
            int tohum = t.tohum;

            // kapı hücresi: sokağa en yakın, zemindeki açık ön yüzlerden biri
            var onYuzler = new List<Vector2Int>();   // (y, x)
            foreach (var b in s.blok.Values)
                if (b.k == 0 && (b.tarz == Apartman || b.tarz == Fachwerk || b.tarz == Tas) && !s.Dolu(b.x, b.y - 1, 0))
                    onYuzler.Add(new Vector2Int(b.y, b.x));
            onYuzler.Sort((p, q) => p.x != q.x ? p.x.CompareTo(q.x) : p.y.CompareTo(q.y));
            if (onYuzler.Count > 0)
            {
                int yMin = onYuzler[0].x;
                var adaylar = onYuzler.FindAll(p => p.x == yMin);
                var sec = adaylar[Sec(Karma(tohum, adaylar.Count, 3, 1), adaylar.Count)];
                s.kapiVar = true;
                s.kapi = new Vector2Int(sec.y, sec.x);
            }

            var sirali = new List<BinaTasarimi.Blok>(s.blok.Values);
            sirali.Sort((p, q) => p.x != q.x ? p.x.CompareTo(q.x) : p.y != q.y ? p.y.CompareTo(q.y) : p.k.CompareTo(q.k));
            foreach (var b in sirali)
            {
                int x = b.x, y = b.y, k = b.k, tarz = b.tarz;
                var r = s.RenkleriAl(b);
                float z = k * KAT;
                if (tarz == Merdiven)
                {
                    int don = 0;
                    for (int i = 0; i < 4; i++)
                        if (s.Dolu(x + Yon[i].x, y + Yon[i].y, k)) { don = (i + 2) % 4; break; }
                    s.Koy("tas_merdiven", x, y, z, don, r);
                    continue;
                }
                bool altBos = k > 0 && !s.Dolu(x, y, k - 1);
                if (altBos) s.Koy("taban", x, y, z, 0, r);
                bool kapiHucresi = s.kapiVar && s.kapi.x == x && s.kapi.y == y;
                for (int i = 0; i < 4; i++)
                {
                    if (s.Dolu(x + Yon[i].x, y + Yon[i].y, k)) continue;
                    uint h = Karma(x * 4 + i, y, tohum, 11);                 // sütun boyunca aynı: pencereler üst üste hizalı
                    uint hk = Karma(x * 4 + i, y, tohum + k * 31, 17);
                    string ad;
                    bool kapiYuzu = k == 0 && i == 0 && kapiHucresi;
                    if (tarz == Kaide) ad = "tas_duvar";
                    else if (tarz == Tas) ad = kapiYuzu ? "kapi_kemerli" : TasUst[Sec(h + (hk % 3 == 0 ? 1u : 0u), TasUst.Length)];
                    else if (tarz == Fachwerk)
                    {
                        if (k == 0) ad = kapiYuzu ? "kapi_ahsap" : (h % 3 != 0 ? "duvar_pencere" : "duvar_duz");
                        else ad = FwUst[Sec(h, FwUst.Length)];
                    }
                    else
                    {
                        if (k == 0)
                        {
                            if (kapiYuzu) ad = t.dukkan ? "kapi_vitrin" : "kapi_ahsap";
                            else if (i == 0 && t.dukkan) ad = "vitrin";
                            else ad = h % 4 != 0 ? "zemin_pencere" : "duvar_duz";
                        }
                        else
                        {
                            ad = AptUst[Sec(h, AptUst.Length)];
                            if (ad == "duvar_balkon" && hk % 2 != 0) ad = "duvar_pencere";
                        }
                    }
                    s.Koy(ad, x, y, z, i, r);
                    if (k == 0 && tarz != Kaide) s.Koy("kaide", x, y, z, i, r);
                    if (tarz == Apartman) s.Koy("silme", x, y, z + KAT, i, r);
                    else if (tarz == Fachwerk && k > 0) s.Koy("silme_ahsap", x, y, z, i, r);
                    if (k == 0 && (tarz == Apartman || tarz == Fachwerk) && !kapiYuzu)
                    {
                        uint a = Karma(x, y, i, tohum + 5) % 14;
                        if (a == 0) s.Koy("fici", x, y, z, i, r, 1f, 0.7f, -A - 0.45f);
                        else if (a == 1) s.Koy("saksi", x, y, z, i, r, 1f, -0.8f, -A - 0.38f);
                        else if (a == 2 && ad == "duvar_duz") s.Koy("sarmasik", x, y, z, i, r);
                        else if (a == 3) s.Koy("bank", x, y, z, i, r, 1f, 0f, -A - 0.4f);
                    }
                    if (kapiYuzu)
                    {
                        s.Koy("paspas", x, y, z, i, r, 1f, 0f, -A - 0.75f);
                        if (tarz == Apartman && t.dukkan) s.Koy("tabela", x, y, z, i, r);
                    }
                    if (k == 0 && tarz == Apartman && ad == "zemin_pencere" && Karma(x, y, i, tohum + 9) % 5 == 0)
                        s.Koy("fener", x, y, z, i, r, 1f, 1.15f, 0f);
                }
                // köşeler
                for (int i = 0; i < 4; i++)
                {
                    var a = Yon[(i + 3) % 4];
                    var c = Yon[i];
                    bool aBos = !s.Dolu(x + a.x, y + a.y, k), cBos = !s.Dolu(x + c.x, y + c.y, k);
                    if (aBos && cBos)
                        s.Koy(tarz == Tas || tarz == Kaide ? "kose_tas" : (tarz == Fachwerk && k > 0) ? "kose_ahsap" : "kose_dis", x, y, z, i, r);
                    else if (!aBos && !cBos && !s.Dolu(x + a.x + c.x, y + a.y + c.y, k))
                        s.Koy("kose_ic", x, y, z, i, r);
                }
                // altı boş: kemerli geçit
                if (altBos && !s.Var(x, y, k - 1))
                {
                    for (int i = 0; i < 4; i++)
                        if (!s.Dolu(x + Yon[i].x, y + Yon[i].y, k - 1)) s.Koy("kemer", x, y, z - KAT, i, r);
                    for (int i = 0; i < 4; i++)
                    {
                        var a = Yon[(i + 3) % 4];
                        var c = Yon[i];
                        if (!s.Dolu(x + a.x, y + a.y, k - 1) && !s.Dolu(x + c.x, y + c.y, k - 1)) s.Koy("kemer_ayak", x, y, z - KAT, i, r);
                    }
                }
            }

            // çatılar: her katta üstü açık hücreler, aynı tarzdan bağlı bölgeler
            var katlar = new SortedSet<int>();
            foreach (var b in s.blok.Values) katlar.Add(b.k);
            foreach (int k in katlar)
            {
                var ust = new Dictionary<Vector2Int, BinaTasarimi.Blok>();
                foreach (var b in s.blok.Values)
                    if (b.k == k && b.tarz != Merdiven && !s.Dolu(b.x, b.y, k + 1)) ust[new Vector2Int(b.x, b.y)] = b;
                var baslar = new List<Vector2Int>(ust.Keys);
                baslar.Sort((p, q) => p.x != q.x ? p.x.CompareTo(q.x) : p.y.CompareTo(q.y));
                var gorulen = new HashSet<Vector2Int>();
                foreach (var bas in baslar)
                {
                    if (!gorulen.Add(bas)) continue;
                    int tarz = ust[bas].tarz;
                    var bolge = new List<Vector2Int>();
                    var yigin = new Stack<Vector2Int>();
                    yigin.Push(bas);
                    while (yigin.Count > 0)
                    {
                        var p = yigin.Pop();
                        bolge.Add(p);
                        foreach (var d in Yon)
                        {
                            var q = p + d;
                            if (ust.TryGetValue(q, out var qb) && qb.tarz == tarz && gorulen.Add(q)) yigin.Push(q);
                        }
                    }
                    Cati(s, bolge, ust[bas], k, tarz);
                }
            }
            return s;
        }

        static readonly Dictionary<int, (string ad, int don)> CatiDesen = new Dictionary<int, (string, int)>
        {
            // anahtar: h00 + 2·h10 + 4·h11 + 8·h01
            { 0, ("cati_duz", 0) },
            { 4 + 8, ("cati_egim", 0) }, { 1 + 8, ("cati_egim", 1) }, { 1 + 2, ("cati_egim", 2) }, { 2 + 4, ("cati_egim", 3) },
            { 4, ("cati_kose", 0) }, { 8, ("cati_kose", 1) }, { 1, ("cati_kose", 2) }, { 2, ("cati_kose", 3) },
            { 2 + 4 + 8, ("cati_vadi", 0) }, { 1 + 4 + 8, ("cati_vadi", 1) }, { 1 + 2 + 8, ("cati_vadi", 2) }, { 1 + 2 + 4, ("cati_vadi", 3) },
            { 1 + 4, ("cati_semer", 0) }, { 2 + 8, ("cati_semer", 1) },
        };

        static Vector2 Kafes(int px, int py)
        {
            float A = Hucre * 0.5f;
            return new Vector2((px - Izgara) * A, (py - Izgara) * A);
        }

        static void Cati(Durum s, List<Vector2Int> bolge, BinaTasarimi.Blok b0, int k, int tarz)
        {
            float A = Hucre * 0.5f, KAT = KatBoyu;
            float z = (k + 1) * KAT;
            int tohum = s.t.tohum;
            var kume = new HashSet<Vector2Int>(bolge);
            int xMin = int.MaxValue, xMax = int.MinValue, yMin = int.MaxValue, yMax = int.MinValue;
            foreach (var p in bolge)
            {
                xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x);
                yMin = Mathf.Min(yMin, p.y); yMax = Mathf.Max(yMax, p.y);
            }
            int gen = xMax - xMin + 1, der = yMax - yMin + 1;
            var r = s.RenkleriAl(b0);

            void KenarParcasi(string ad, string koseAd)
            {
                foreach (var p in bolge)
                {
                    for (int i = 0; i < 4; i++)
                        if (!kume.Contains(p + Yon[i])) s.Koy(ad, p.x, p.y, z, i, r);
                    if (koseAd == null) continue;
                    for (int i = 0; i < 4; i++)
                        if (!kume.Contains(p + Yon[(i + 3) % 4]) && !kume.Contains(p + Yon[i])) s.Koy(koseAd, p.x, p.y, z, i, r);
                }
            }

            if (tarz == Kaide)
            {
                foreach (var p in bolge) s.Koy("duz_cati", p.x, p.y, z, 0, r);
                foreach (var p in bolge)
                    for (int i = 0; i < 4; i++)
                    {
                        var q = p + Yon[i];
                        if (!kume.Contains(q) && !s.Var(q.x, q.y, k) && !s.Dolu(q.x, q.y, k + 1)) s.Koy("korkuluk_demir", p.x, p.y, z, i, r);
                    }
                return;
            }
            if (bolge.Count == 1 && k >= 2)
            {
                var p = bolge[0];
                if (tarz == Tas)
                {
                    s.Koy("duz_cati", p.x, p.y, z, 0, r);
                    for (int i = 0; i < 4; i++) s.Koy("dama", p.x, p.y, z, i, r);
                }
                else s.Koy(Karma(p.x, p.y, k, tohum) % 4 == 0 ? "kule_kubbe" : "kule_sivri", p.x, p.y, z, 0, r);
                return;
            }
            if (tarz == Tas)
            {
                foreach (var p in bolge) s.Koy("duz_cati", p.x, p.y, z, 0, r);
                KenarParcasi("dama", null);
                return;
            }

            // eğimli çatı: yarım hücrelik kafeste dışarıya uzaklık
            bool besik = tarz == Fachwerk;
            int eksen = gen >= der ? 0 : 1;
            float egim = besik ? 1.25f : 0.85f;
            var d = new Dictionary<Vector2Int, int>();
            const int Bilinmiyor = -1;
            foreach (var p in bolge)
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                    {
                        var q = new Vector2Int(2 * p.x + i, 2 * p.y + j);
                        if (d.ContainsKey(q)) continue;
                        bool sinir;
                        int cx0 = Yari(q.x - 1), cx1 = Yari(q.x), cy0 = Yari(q.y - 1), cy1 = Yari(q.y);
                        if (besik)
                        {
                            bool once, sonra;
                            if (eksen == 0)
                            {
                                once = kume.Contains(new Vector2Int(cx0, cy0)) || kume.Contains(new Vector2Int(cx1, cy0));
                                sonra = kume.Contains(new Vector2Int(cx0, cy1)) || kume.Contains(new Vector2Int(cx1, cy1));
                            }
                            else
                            {
                                once = kume.Contains(new Vector2Int(cx0, cy0)) || kume.Contains(new Vector2Int(cx0, cy1));
                                sonra = kume.Contains(new Vector2Int(cx1, cy0)) || kume.Contains(new Vector2Int(cx1, cy1));
                            }
                            sinir = !(once && sonra);
                        }
                        else
                        {
                            sinir = !kume.Contains(new Vector2Int(cx0, cy0)) || !kume.Contains(new Vector2Int(cx1, cy0)) ||
                                    !kume.Contains(new Vector2Int(cx0, cy1)) || !kume.Contains(new Vector2Int(cx1, cy1));
                        }
                        d[q] = sinir ? 0 : Bilinmiyor;
                    }
            Vector2Int[] komsular = besik
                ? (eksen == 0 ? new[] { new Vector2Int(0, 1), new Vector2Int(0, -1) } : new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0) })
                : new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
                          new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1) };
            var kuyruk = new List<Vector2Int>();
            foreach (var kv in d) if (kv.Value == 0) kuyruk.Add(kv.Key);
            int adim = 0;
            while (kuyruk.Count > 0)
            {
                adim++;
                var yeni = new List<Vector2Int>();
                foreach (var p in kuyruk)
                    foreach (var kk in komsular)
                    {
                        var q = p + kk;
                        if (d.TryGetValue(q, out int dv) && dv == Bilinmiyor) { d[q] = adim; yeni.Add(q); }
                    }
                kuyruk = yeni;
            }
            var anahtarlar = new List<Vector2Int>(d.Keys);
            foreach (var p in anahtarlar) d[p] = Mathf.Clamp(d[p], 0, 4);

            string catiMat = "Pastel_Cati_" + r.C.TrimStart('#');
            foreach (var p in bolge)
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < 2; j++)
                    {
                        int px = 2 * p.x + i, py = 2 * p.y + j;
                        int h00 = d[new Vector2Int(px, py)], h10 = d[new Vector2Int(px + 1, py)], h11 = d[new Vector2Int(px + 1, py + 1)], h01 = d[new Vector2Int(px, py + 1)];
                        int h0 = Mathf.Min(Mathf.Min(h00, h10), Mathf.Min(h11, h01));
                        int a = h00 - h0, b = h10 - h0, c = h11 - h0, e = h01 - h0;
                        if (a > 1 || b > 1 || c > 1 || e > 1)
                        {
                            // tabloda olmayan (iki basamak birden yükselen) karo: düz iki üçgen
                            Vector2 k0 = Kafes(px, py), k1 = Kafes(px + 1, py), k2 = Kafes(px + 1, py + 1), k3 = Kafes(px, py + 1);
                            var p0 = new Vector3(k0.x, k0.y, z + h00 * egim); var p1 = new Vector3(k1.x, k1.y, z + h10 * egim);
                            var p2 = new Vector3(k2.x, k2.y, z + h11 * egim); var p3 = new Vector3(k3.x, k3.y, z + h01 * egim);
                            s.cokgenler.Add(new Cokgen { p = new[] { p0, p1, p2 }, mat = catiMat, n = new Vector3(0f, 0f, 1f) });
                            s.cokgenler.Add(new Cokgen { p = new[] { p0, p2, p3 }, mat = catiMat, n = new Vector3(0f, 0f, 1f) });
                            continue;
                        }
                        var karo = CatiDesen[a + 2 * b + 4 * c + 8 * e];
                        s.Koy(karo.ad, p.x, p.y, z + h0 * egim, karo.don, r, egim, -A + i * A, -A + j * A);
                    }
            KenarParcasi("sacak", "sacak_kose");
            // alınlıklar
            string alinMat = besik ? "Pastel_Siva_f6f0e2" : "Pastel_Duvar_" + r.D.TrimStart('#');
            foreach (var p in bolge)
                for (int i = 0; i < 4; i++)
                {
                    if (kume.Contains(p + Yon[i])) continue;
                    for (int q = 0; q < 2; q++)
                    {
                        Vector2Int u, v;
                        if (i == 0) { u = new Vector2Int(2 * p.x + q, 2 * p.y); v = new Vector2Int(2 * p.x + q + 1, 2 * p.y); }
                        else if (i == 1) { u = new Vector2Int(2 * p.x + 2, 2 * p.y + q); v = new Vector2Int(2 * p.x + 2, 2 * p.y + q + 1); }
                        else if (i == 2) { u = new Vector2Int(2 * p.x + 2 - q, 2 * p.y + 2); v = new Vector2Int(2 * p.x + 1 - q, 2 * p.y + 2); }
                        else { u = new Vector2Int(2 * p.x, 2 * p.y + 2 - q); v = new Vector2Int(2 * p.x, 2 * p.y + 1 - q); }
                        float hu = d[u] * egim, hv = d[v] * egim;
                        if (hu <= 0f && hv <= 0f) continue;
                        Vector2 ku = Kafes(u.x, u.y), kv2 = Kafes(v.x, v.y);
                        s.cokgenler.Add(new Cokgen
                        {
                            p = new[] { new Vector3(ku.x, ku.y, z), new Vector3(kv2.x, kv2.y, z), new Vector3(kv2.x, kv2.y, z + hv), new Vector3(ku.x, ku.y, z + hu) },
                            mat = alinMat, n = new Vector3(Yon[i].x, Yon[i].y, 0f),
                        });
                    }
                }
            // baca ve çatı penceresi
            foreach (var p in bolge)
            {
                uint hh = Karma(p.x, p.y, k, tohum + 21) % 7;
                int orta = d[new Vector2Int(2 * p.x + 1, 2 * p.y + 1)];
                if (hh == 0) s.Koy(besik ? "baca" : "baca_ince", p.x, p.y, z + orta * egim, 0, r, 1f, 0.35f, 0.2f);
                else if (hh == 1 && !besik && !kume.Contains(new Vector2Int(p.x, p.y - 1)) && orta >= 1)
                    s.Koy("cati_penceresi", p.x, p.y, z, 0, r, 1f, 0f, -A);
            }
        }

        // ---------------------------------------------------------------- mesh

        static string Renkli(string ad, Renkler r)
        {
            int i = ad.IndexOf("_@", StringComparison.Ordinal);
            if (i < 0) return ad;
            string rol = ad.Substring(i + 2);
            string hex = rol == "D" ? r.D : rol == "d" ? r.d : rol == "C" ? r.C : rol == "c" ? r.c : r.K;
            return ad.Substring(0, i + 1) + hex.TrimStart('#');
        }

        /// <summary>Model ekseninden (metre, Z yukarı, ön −Y) Unity model uzayına (Y yukarı, ön +Z).</summary>
        static Vector3 Unity(float mx, float my, float mz) => new Vector3(-mx, mz, -my);

        /// <summary>
        /// Tasarımı tek mesh'e kurar. malzemeAdlari: alt-mesh sırasıyla malzeme adları. hafifMi: uzak görünüm (ayrıntısız parçalar).
        /// Kütüphane yoksa ya da tasarım boşsa null.
        /// </summary>
        public static Mesh Kur(BinaTasarimi t, bool hafifMi, out List<string> malzemeAdlari)
        {
            malzemeAdlari = new List<string>();
            if (t == null || t.bloklar == null || t.bloklar.Count == 0 || Kut == null) return null;
            var s = Yerlesim(t);
            float A = Hucre * 0.5f;
            var kose = new List<Vector3>();
            var alt = new Dictionary<string, List<int>>();

            List<int> Liste(string mat)
            {
                if (!alt.TryGetValue(mat, out var l)) { alt[mat] = l = new List<int>(); }
                return l;
            }

            foreach (var y in s.yerler)
            {
                ParcaVeri pv = null;
                if (hafifMi)
                {
                    if (parca.TryGetValue(y.ad, out var asil) && asil.grup == "ayrinti" && y.ad != "tas_merdiven" && y.ad != "baca") continue;
                    hafif.TryGetValue(y.ad, out pv);
                }
                if (pv == null && !parca.TryGetValue(y.ad, out pv)) continue;
                float cx = (y.x - (Izgara - 1) * 0.5f) * Hucre, cy = (y.y - (Izgara - 1) * 0.5f) * Hucre;
                bool karo = CatiKarolari.Contains(y.ad);
                int bas = kose.Count;
                for (int i = 0; i + 2 < pv.v.Length; i += 3)
                {
                    float px = pv.v[i], py = pv.v[i + 1], pz = pv.v[i + 2];
                    if (karo)
                    {
                        px -= A * 0.5f; py -= A * 0.5f;
                        for (int q = 0; q < y.don; q++) { float tx = -py; py = px; px = tx; }
                        px += A * 0.5f + y.kay.x; py += A * 0.5f + y.kay.y;
                    }
                    else
                    {
                        px += y.kay.x; py += y.kay.y;
                        for (int q = 0; q < y.don; q++) { float tx = -py; py = px; px = tx; }
                    }
                    kose.Add(Unity(cx + px, cy + py, y.z + pz * y.zs));
                }
                foreach (var a in pv.alt)
                {
                    var l = Liste(Renkli(a.malzeme, y.r));
                    // eksen çevirisi yönü ters çevirir: üçgen sırası da ters
                    for (int i = 0; i + 2 < a.uc.Length; i += 3) { l.Add(bas + a.uc[i]); l.Add(bas + a.uc[i + 2]); l.Add(bas + a.uc[i + 1]); }
                }
            }
            foreach (var c in s.cokgenler)
            {
                var p = c.p;
                Vector3 nn = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
                bool ters = Vector3.Dot(nn, c.n) < 0f;
                int bas = kose.Count;
                for (int i = 0; i < p.Length; i++)
                {
                    var q = ters ? p[p.Length - 1 - i] : p[i];
                    kose.Add(Unity(q.x, q.y, q.z));
                }
                var l = Liste(c.mat);
                for (int i = 1; i + 1 < p.Length; i++) { l.Add(bas); l.Add(bas + i + 1); l.Add(bas + i); }
            }
            if (kose.Count == 0) return null;

            var mesh = new Mesh { name = t.ad + (hafifMi ? "_Hafif" : "") };
            if (kose.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(kose);
            var adlar = new List<string>(alt.Keys);
            adlar.Sort(string.CompareOrdinal);
            mesh.subMeshCount = adlar.Count;
            for (int i = 0; i < adlar.Count; i++) mesh.SetTriangles(alt[adlar[i]], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            malzemeAdlari = adlar;
            return mesh;
        }

        // ---------------------------------------------------------------- malzemeler

        static readonly Dictionary<string, Material> malzemeOnbellek = new Dictionary<string, Material>();

        /// <summary>"Pastel_&lt;etiket&gt;_&lt;rrggbb&gt;" adlı malzeme (yoksa Modeller/Malzemeler altında kurulur): Pastel gölgelendiricisi, renk addan.</summary>
        public static Material Malzeme(string ad)
        {
            if (malzemeOnbellek.TryGetValue(ad, out var m) && m != null) return m;
            string yol = MalzemeKlasor + "/" + ad + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(yol);
            var shader = Shader.Find("KuryeTycoon/Pastel");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (m == null)
            {
                KlasorAc(MalzemeKlasor);
                m = new Material(shader) { name = ad };
                AssetDatabase.CreateAsset(m, yol);
            }
            else if (m.shader != shader) m.shader = shader;
            Color renk = Color.white;
            int i = ad.LastIndexOf('_');
            if (i >= 0 && ad.Length - i - 1 >= 6 &&
                int.TryParse(ad.Substring(i + 1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v))
                renk = new Color32((byte)(v >> 16), (byte)((v >> 8) & 255), (byte)(v & 255), 255);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", renk);
            m.color = renk;
            // Gece ışık verenler (GeceIsiklari "Pastel_Cam" ve "Pastel_Isik" adlarını tanır): gündüz ışıma siyah
            if ((ad.StartsWith("Pastel_Cam") || ad.StartsWith("Pastel_Isik")) && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(m);
            malzemeOnbellek[ad] = m;
            return m;
        }

        public static Material[] Malzemeler(List<string> adlar)
        {
            var dizi = new Material[adlar.Count];
            for (int i = 0; i < adlar.Count; i++) dizi[i] = Malzeme(adlar[i]);
            return dizi;
        }

        public static void KlasorAc(string yol)
        {
            if (AssetDatabase.IsValidFolder(yol)) return;
            string ust = Path.GetDirectoryName(yol).Replace('\\', '/');
            KlasorAc(ust);
            AssetDatabase.CreateFolder(ust, Path.GetFileName(yol));
        }
    }
}
