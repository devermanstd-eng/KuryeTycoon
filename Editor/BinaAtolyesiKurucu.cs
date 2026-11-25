using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// Bina Atölyesi'nin birleştiricisi: tasarımdan (BinaTasarimi: bloklar + arsa süsleri) komşuluk kurallarıyla modüler pastel
    /// parçaları seçer ve tek mesh'e birleştirir (malzemeye göre alt-mesh + kalem çizgileri alt-mesh'i).
    ///  • Parçalar ve kullanım türü tablosu FBX değil: Tools/Blender/pastel_parcalar.py'nin yazdığı
    ///    Editor/BinaAtolyesi/pastel_parcalar.json'dan okunur (Blender gerekmez). Parça koordinatları model ekseninde
    ///    (metre, Z yukarı, ön −Y); burada Unity'ye çevrilir (x, y, z) → (−x, z, −y).
    ///  • Kurallar pastel_parcalar.py'deki yerlesim() / _cati() / kenarlar() ile AYNI (karma fonksiyonu dahil): birini
    ///    değiştirince öbürünü de değiştir.
    ///  • Kalem çizgileri: birleşmiş mesh'in sert (iki yüz arası &gt; 25°) ve açık kenarları ince şeritler olarak
    ///    "Pastel_Cizgi" alt-mesh'ine yazılır (KuryeTycoon/PastelCizgi ekranda sabit kalınlığa açar). Aynı düzlemde yan yana
    ///    gelen parçaların ek yerleri çizilmez. Ağaç taçlarının "Pastel_Kabuk" yüzleri de bu alt-mesh'e gider.
    ///  • Malzemeler adla kurulur: "Pastel_&lt;etiket&gt;_&lt;rrggbb&gt;" → KuryeTycoon/Pastel, renk addan.
    /// </summary>
    public static class BinaAtolyesiKurucu
    {
        public const string KutuphaneYol = "Assets/KuryeTycoon/Editor/BinaAtolyesi/pastel_parcalar.json";
        const string MalzemeKlasor = "Assets/KuryeTycoon/Modeller/Malzemeler";
        public const string CizgiMalzemesi = "Pastel_Cizgi_3a2f3a";
        public const int Apartman = 0, Fachwerk = 1, Tas = 2, Kaide = 3, Merdiven = 4;
        const int CtOto = 0, CtKirma = 1, CtBesik = 2, CtMansart = 3, CtTeras = 4, CtBasamak = 5, CtTestere = 6;
        public static readonly string[] TarzAd = { "Pastel apartman", "Yarı ahşap (fachwerk) ev", "Taş kule", "Taş kaide / teras", "Taş merdiven" };
        public static readonly string[] Bolgeler = { "eski", "karma", "kule", "ev", "kampus", "sanayi", "avm" };

        // ---------------------------------------------------------------- kütüphane (JSON)

        [Serializable] public class AltVeri { public string malzeme; public int[] uc; }
        [Serializable] public class ParcaVeri { public string ad, grup, aciklama, hedef; public bool kucuk; public float[] v; public AltVeri[] alt; }
        [Serializable]
        public class TurVeri
        {
            public string ad, etiket, grup, onek, tabela, simge, on, kapi, duvar, cephe, tepe, vurgu, bolge;
            public int tente;
            public string[] aks;
        }
        [Serializable]
        public class Kutuphane
        {
            public int surum;
            public float hucre = 2.8f, kat = 3f;
            public int izgara = 5;
            public string[] duvarRenk, catiRenk, vurguRenk, tenteRenk, yaprakRenk, catiTarz;
            public TurVeri[] turler;
            public ParcaVeri[] parcalar, hafifler;
        }

        static Kutuphane kut;
        static Dictionary<string, ParcaVeri> parca, hafif;
        static Dictionary<string, TurVeri> tur;
        static readonly TurVeri BosTur = new TurVeri { ad = "konut", etiket = "Konut", grup = "Konut", onek = "Bolge", bolge = "eski" };

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

        /// <summary>Parça kütüphanesini (yeniden) okur. Dosya yoksa, bozuksa ya da eski sürümse false.</summary>
        public static bool Yukle()
        {
            kut = null;
            parca = new Dictionary<string, ParcaVeri>();
            hafif = new Dictionary<string, ParcaVeri>();
            tur = new Dictionary<string, TurVeri>();
            string metin = null;
            // Önce diskten oku: içe aktarma henüz bitmemişken TextAsset eski içeriği verebilir.
            string tam = Path.Combine(Application.dataPath, "KuryeTycoon/Editor/BinaAtolyesi/pastel_parcalar.json");
            if (File.Exists(tam)) metin = File.ReadAllText(tam);
            if (string.IsNullOrEmpty(metin))
            {
                var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(KutuphaneYol);
                if (ta != null) metin = ta.text;
            }
            if (string.IsNullOrEmpty(metin))
            {
                Debug.LogError("Bina Atölyesi: parça kütüphanesi yok: " + KutuphaneYol + " (python3 Tools/Blender/pastel_parcalar.py --json)");
                return false;
            }
            Kutuphane k;
            try { k = JsonUtility.FromJson<Kutuphane>(metin); }
            catch (Exception ex) { Debug.LogError("Bina Atölyesi: parça kütüphanesi okunamadı: " + ex.Message); return false; }
            if (k == null || k.parcalar == null || k.parcalar.Length == 0 || k.surum < 2 || k.turler == null || k.yaprakRenk == null || k.tenteRenk == null)
            {
                Debug.LogError("Bina Atölyesi: parça kütüphanesi eski ya da eksik (sürüm 2 gerekli). python3 Tools/Blender/pastel_parcalar.py --json");
                return false;
            }
            foreach (var p in k.parcalar) parca[p.ad] = p;
            if (k.hafifler != null) foreach (var p in k.hafifler) hafif[p.ad] = p;
            foreach (var t in k.turler) tur[t.ad] = t;
            kut = k;
            return true;
        }

        public static Color Renk(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;

        public static TurVeri TurBul(BinaTasarimi t)
        {
            if (Kut == null) return BosTur;
            string ad = !string.IsNullOrEmpty(t.tur) ? t.tur : t.dukkan ? "dukkan" : "konut";
            return tur.TryGetValue(ad, out var v) ? v : tur.TryGetValue("konut", out v) ? v : BosTur;
        }

        public static ParcaVeri ParcaBul(string ad) => Kut != null && ad != null && parca.TryGetValue(ad, out var p) ? p : null;

        /// <summary>Prefab adının başı: türün öneki ("Bolge" ise Bolge_&lt;bölge&gt;).</summary>
        public static string Onek(BinaTasarimi t)
        {
            var tv = TurBul(t);
            if (Bos(tv.onek) || tv.onek == "Bolge")
                return "Bolge_" + (Array.IndexOf(Bolgeler, t.bolge) >= 0 ? t.bolge : "eski");
            return tv.onek;
        }

        static bool Bos(string s) => string.IsNullOrEmpty(s);

        // ---------------------------------------------------------------- kurallar (pastel_parcalar.py: yerlesim)

        class Renkler { public string D, d, C, c, K, T, Y, y, k; }

        struct Yer
        {
            public string ad;
            public float cx, cy, z, zs, olcek, aci;
            public int don;
            public Vector2 kay;
            public Renkler r;
        }

        struct Cokgen { public Vector3[] p; public string mat; public Vector3 n; }
        struct KutuVeri { public float x0, x1, y0, y1, z0, z1; public string mat; }

        static readonly Vector2Int[] Yon = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) };
        static readonly string[] AptUst = { "duvar_pencere", "duvar_pencere", "duvar_cift", "duvar_kemerli", "duvar_saksi", "duvar_panjur", "duvar_dar", "duvar_balkon", "duvar_duz", "duvar_yuvarlak" };
        static readonly string[] FwUst = { "fachwerk_pencere", "fachwerk_capraz", "fachwerk_saksi", "fachwerk_x", "fachwerk_pencere" };
        static readonly string[] TasUst = { "tas_duvar", "tas_mazgal", "tas_kemerli", "tas_duvar" };
        static readonly HashSet<string> SaksiAlir = new HashSet<string> { "duvar_pencere", "duvar_cift", "duvar_panjur" };
        static readonly HashSet<string> CatiKarolari = new HashSet<string> { "cati_duz", "cati_egim", "cati_kose", "cati_vadi", "cati_semer" };
        static readonly float[] HKirma = { 0f, 0.85f, 1.7f, 2.55f, 3.4f }, HBesik = { 0f, 1.25f, 2.5f, 3.75f, 5f }, HMansart = { 0f, 2f, 2.45f, 2.9f, 3.35f };
        const string Silme = "Pastel_Silme_fbf7ee";

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
        static int Mod(int v, int n) => ((v % n) + n) % n;
        /// <summary>Tabana yuvarlayan ikiye bölme (Python'daki // 2).</summary>
        static int Yari(int v) => v >= 0 ? v / 2 : -((1 - v) / 2);

        static string Koyu(string hex, float f)
        {
            Color32 c = Renk(hex);
            return "#" + ((int)(c.r * f)).ToString("x2") + ((int)(c.g * f)).ToString("x2") + ((int)(c.b * f)).ToString("x2");
        }

        /// <summary>Kalem çizgisi rengi: yüzey renginin koyusu ile sıcak koyu mor-kahvenin ortası (saf siyah değil).</summary>
        static Color32 CizgiRengi(Color32 c)
        {
            return new Color32((byte)(c.r * 0.225f + 0.135f * 255f), (byte)(c.g * 0.225f + 0.1f * 255f), (byte)(c.b * 0.225f + 0.135f * 255f), 255);
        }

        static string CizgiRengiHex(string hex)
        {
            Color32 c = CizgiRengi(Renk(hex));
            return "#" + c.r.ToString("x2") + c.g.ToString("x2") + c.b.ToString("x2");
        }

        /// <summary>Karakterin harf parçası (boşluk ve bilinmeyenler için null). pastel_parcalar.py: harf_adi.</summary>
        static string HarfAdi(char ch)
        {
            ch = char.ToUpperInvariant(ch);
            switch (ch)
            {
                case '&': return "harf_ve";
                case '\'': return "harf_kesme";
                case '-': return "harf_tire";
                case '/': return "harf_bolu";
                case '.': return "harf_nokta";
            }
            if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) return "harf_" + char.ToLowerInvariant(ch);
            return null;
        }

        class Durum
        {
            public BinaTasarimi t;
            public TurVeri T;
            public Dictionary<Vector3Int, BinaTasarimi.Blok> blok = new Dictionary<Vector3Int, BinaTasarimi.Blok>();
            public List<Yer> yerler = new List<Yer>();
            public List<Cokgen> cokgenler = new List<Cokgen>();
            public List<KutuVeri> kutular = new List<KutuVeri>();

            public bool Var(int x, int y, int k) => blok.ContainsKey(new Vector3Int(x, y, k));
            public bool Dolu(int x, int y, int k) => blok.TryGetValue(new Vector3Int(x, y, k), out var b) && b.tarz != Merdiven;

            public bool OnAcik(int x, int y)
            {
                return blok.TryGetValue(new Vector3Int(x, y, 0), out var b) && (b.tarz == Apartman || b.tarz == Fachwerk || b.tarz == Tas) && !Dolu(x, y - 1, 0);
            }

            public Renkler RenkleriAl(int renk, int cati, int yaprak)
            {
                var kt = Kut;
                string dv = kt.duvarRenk[Mod(renk, kt.duvarRenk.Length)];
                string ct = kt.catiRenk[Mod(cati, kt.catiRenk.Length)];
                string vr = !Bos(T.vurgu) ? T.vurgu : kt.vurguRenk[Sec(Karma(t.tohum, renk, cati, 7), kt.vurguRenk.Length)];
                string yp = kt.yaprakRenk[Mod(yaprak, kt.yaprakRenk.Length)];
                return new Renkler
                {
                    D = dv, d = Koyu(dv, 0.9f), C = ct, c = Koyu(ct, 0.86f), K = vr, T = kt.tenteRenk[Mod(t.tente, kt.tenteRenk.Length)],
                    Y = yp, y = Koyu(yp, 0.88f), k = CizgiRengiHex(yp),
                };
            }

            public Renkler RenkleriAl(BinaTasarimi.Blok b) => RenkleriAl(b.renk, b.cati, 0);

            public void Koy(string ad, int x, int y, float z, int don, Renkler r, float zs = 1f, float kx = 0f, float ky = 0f, float olcek = 1f)
            {
                float cx = (x - (Izgara - 1) * 0.5f) * Hucre, cy = (y - (Izgara - 1) * 0.5f) * Hucre;
                yerler.Add(new Yer { ad = ad, cx = cx, cy = cy, z = z, don = Mod(don, 4), zs = zs, kay = new Vector2(kx, ky), r = r, olcek = olcek, aci = 0f });
            }
        }

        static Durum Yerlesim(BinaTasarimi t)
        {
            float A = Hucre * 0.5f, KAT = KatBoyu, HUCRE = Hucre;
            var T = TurBul(t);
            var s = new Durum { t = t, T = T };
            if (t.bloklar != null) foreach (var b in t.bloklar) s.blok[new Vector3Int(b.x, b.y, b.k)] = b;
            int tohum = t.tohum;
            string metin = (!Bos(t.tabela) ? t.tabela : T.tabela ?? "").ToUpperInvariant();

            // kapı hücresi: sokağa en yakın, zemindeki açık ön yüzlerden biri
            var onYuzler = new List<Vector2Int>();   // (y, x)
            foreach (var b in s.blok.Values)
                if (b.k == 0 && s.OnAcik(b.x, b.y)) onYuzler.Add(new Vector2Int(b.y, b.x));
            onYuzler.Sort((p, q) => p.x != q.x ? p.x.CompareTo(q.x) : p.y.CompareTo(q.y));
            bool kapiVar = false;
            int kapiX = 0, kapiY = 0, kapiUst = 0;
            if (onYuzler.Count > 0)
            {
                int yMin = onYuzler[0].x;
                var adaylar = onYuzler.FindAll(p => p.x == yMin);
                var sec = adaylar[Sec(Karma(tohum, adaylar.Count, 3, 1), adaylar.Count)];
                kapiVar = true;
                kapiX = sec.y; kapiY = sec.x;
                while (s.Dolu(kapiX, kapiY, kapiUst + 1)) kapiUst++;
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
                bool kapiHucresi = kapiVar && kapiX == x && kapiY == y;
                bool sade = !Bos(T.duvar) && tarz == Apartman;
                for (int i = 0; i < 4; i++)
                {
                    if (s.Dolu(x + Yon[i].x, y + Yon[i].y, k)) continue;
                    uint h = Karma(x * 4 + i, y, tohum, 11);                 // sütun boyunca aynı: pencereler üst üste hizalı
                    uint hk = Karma(x * 4 + i, y, tohum + k * 31, 17);
                    bool kapiYuzu = k == 0 && i == 0 && kapiHucresi;
                    bool onZemin = k == 0 && i == 0 && tarz != Kaide;
                    bool cepheYuzu = !Bos(T.cephe) && kapiHucresi && i == 0 && k == kapiUst && k > 0;
                    string ad;
                    if (tarz == Kaide) ad = "tas_duvar";
                    else if (kapiYuzu) ad = !Bos(T.kapi) ? T.kapi : tarz == Tas ? "kapi_kemerli" : "kapi_ahsap";
                    else if (onZemin && !Bos(T.on)) ad = T.on;
                    else if (cepheYuzu) ad = "duvar_duz";
                    else if (sade) ad = T.duvar;
                    else if (tarz == Tas) ad = TasUst[Sec(h + (hk % 3 == 0 ? 1u : 0u), TasUst.Length)];
                    else if (tarz == Fachwerk) ad = k == 0 ? (h % 3 != 0 ? "duvar_pencere" : "duvar_duz") : FwUst[Sec(h, FwUst.Length)];
                    else if (k == 0) ad = h % 4 != 0 ? "zemin_pencere" : "duvar_duz";
                    else
                    {
                        ad = AptUst[Sec(h, AptUst.Length)];
                        if (ad == "duvar_balkon" && hk % 2 != 0) ad = "duvar_pencere";
                    }
                    s.Koy(ad, x, y, z, i, r);
                    if (ad == "duvar_duz" && cepheYuzu) s.Koy(T.cephe, x, y, z, i, r);
                    if (SaksiAlir.Contains(ad) && hk % 5 == 0) s.Koy("cicek_kutusu_p", x, y, z, i, r);
                    if (onZemin && T.tente != 0 && (kapiYuzu || !Bos(T.on))) s.Koy("tente", x, y, z, i, r);
                    if (k == 0 && tarz != Kaide && !sade) s.Koy("kaide", x, y, z, i, r);
                    if (tarz == Apartman && Bos(T.duvar)) s.Koy("silme", x, y, z + KAT, i, r);
                    else if (tarz == Fachwerk && k > 0) s.Koy("silme_ahsap", x, y, z, i, r);
                    if (k == 0 && i != 0 && (tarz == Apartman || tarz == Fachwerk) && Bos(T.duvar))
                    {
                        uint a = Karma(x, y, i, tohum + 5) % 14;
                        if (a == 0) s.Koy("fici", x, y, z, i, r, 1f, 0.7f, -A - 0.45f);
                        else if (a == 1) s.Koy("saksi", x, y, z, i, r, 1f, -0.8f, -A - 0.38f);
                        else if (a == 2 && ad == "duvar_duz") s.Koy("sarmasik", x, y, z, i, r);
                    }
                    if (kapiYuzu) s.Koy("paspas", x, y, z, i, r, 1f, 0f, -A - 0.75f);
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
                    {
                        if (sade) continue;
                        s.Koy(tarz == Tas || tarz == Kaide ? "kose_tas" : (tarz == Fachwerk && k > 0) ? "kose_ahsap" : "kose_dis", x, y, z, i, r);
                    }
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

            // kapı sırası: tabela bandı + yazı + simge, ön dekorlar, çatı parçası
            if (kapiVar)
            {
                var r = s.RenkleriAl(s.blok[new Vector3Int(kapiX, kapiY, 0)]);
                var sira = new List<int> { kapiX };
                for (int x = kapiX - 1; s.OnAcik(x, kapiY); x--) sira.Insert(0, x);
                for (int x = kapiX + 1; s.OnAcik(x, kapiY); x++) sira.Add(x);
                if (metin.Length > 0)
                {
                    foreach (int x in sira) s.Koy("tabela_bant", x, kapiY, 0f, 0, r);
                    float W = sira.Count * HUCRE;
                    float sol = !Bos(T.simge) ? 0.75f : 0.2f;
                    int n = metin.Length;
                    float px = Mathf.Min(0.075f, (W - sol - 0.2f) / (n * 6 - 1));
                    float x0 = -A + sol + (W - sol - 0.2f - (n * 6 - 1) * px) / 2f;
                    for (int j = 0; j < n; j++)
                    {
                        string ha = HarfAdi(metin[j]);
                        if (ha != null) s.Koy(ha, sira[0], kapiY, 2.54f - 3.5f * px, 0, r, 1f, x0 + j * 6 * px, -A - 0.13f, px);
                    }
                    if (!Bos(T.simge)) s.Koy(T.simge, sira[0], kapiY, 0f, 0, r, 1f, -A + 0.4f, 0f);
                }
                if (T.aks != null && T.aks.Length > 0)
                {
                    int j = 0;
                    foreach (int x in sira)
                    {
                        if (x == kapiX) continue;
                        string ad = T.aks[j % T.aks.Length];
                        j++;
                        if (!Bos(ad)) s.Koy(ad, x, kapiY, 0f, 0, r, 1f, 0f, -A - 1.05f);
                    }
                }
                if (!Bos(T.tepe)) s.Koy(T.tepe, kapiX, kapiY, (kapiUst + 1) * KAT, 0, r, 1f, 0f, 0.3f);
            }

            // çatılar: her katta üstü açık hücreler; aynı tarz ve çatı tarzından bağlı bölgeler
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
                    var b0 = ust[bas];
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
                            if (ust.TryGetValue(q, out var qb) && qb.tarz == b0.tarz && qb.ct == b0.ct && gorulen.Add(q)) yigin.Push(q);
                        }
                    }
                    Cati(s, bolge, b0, k, !Bos(T.duvar));
                }
            }

            // arsa süsleri (serbest yerleşim)
            if (t.susler != null)
                foreach (var su in t.susler)
                {
                    if (Bos(su.ad) || !parca.ContainsKey(su.ad)) continue;
                    var r = s.RenkleriAl(0, su.renk, su.renk);
                    if (Bos(T.vurgu)) r.K = Kut.vurguRenk[Mod(su.renk, Kut.vurguRenk.Length)];
                    s.yerler.Add(new Yer
                    {
                        ad = su.ad, cx = su.x, cy = su.y, z = 0f, don = 0, zs = 1f, kay = Vector2.zero, r = r,
                        olcek = su.olcek > 0f ? su.olcek : 1f, aci = su.aci * Mathf.Deg2Rad,
                    });
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

        static void Cati(Durum s, List<Vector2Int> bolge, BinaTasarimi.Blok b0, int k, bool sade)
        {
            float A = Hucre * 0.5f, KAT = KatBoyu;
            float z = (k + 1) * KAT;
            int tohum = s.t.tohum, tarz = b0.tarz, ct = b0.ct;
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
                foreach (var p in bolge)
                {
                    s.Koy("duz_cati", p.x, p.y, z, 0, r);
                    for (int i = 0; i < 4; i++)
                    {
                        var q = p + Yon[i];
                        if (!kume.Contains(q) && !s.Var(q.x, q.y, k) && !s.Dolu(q.x, q.y, k + 1)) s.Koy("korkuluk_demir", p.x, p.y, z, i, r);
                    }
                }
                return;
            }
            if (ct == CtOto)
            {
                if (bolge.Count == 1 && k >= 2)
                {
                    var p = bolge[0];                                  // ince yüksek yığın: kule tepesi
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
                ct = tarz == Fachwerk ? CtBesik : CtKirma;
            }
            if (ct == CtTeras)
            {
                foreach (var p in bolge) s.Koy("duz_cati", p.x, p.y, z, 0, r);
                KenarParcasi("korkuluk", null);
                foreach (var p in bolge)
                    if (Karma(p.x, p.y, k, tohum + 21) % 6 == 0) s.Koy("baca_ince", p.x, p.y, z + 0.9f, 0, r, 1f, 0.5f, 0.4f);
                return;
            }
            if (ct == CtTestere)
            {
                foreach (var p in bolge) s.Koy("cati_testere", p.x, p.y, z, 0, r);
                KenarParcasi("sacak", null);
                return;
            }

            // eğimli çatı: yarım hücrelik kafeste dışarıya uzaklık (kırma / mansart: 8 komşu; beşik: mahyaya dik eksen)
            bool besik = ct == CtBesik || ct == CtBasamak;
            int eksen = gen >= der ? 0 : 1;
            float[] H = ct == CtMansart ? HMansart : besik ? HBesik : HKirma;
            var d = new Dictionary<Vector2Int, int>();
            const int Bilinmiyor = -1;
            foreach (var p in bolge)
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                    {
                        var q = new Vector2Int(2 * p.x + i, 2 * p.y + j);
                        if (d.ContainsKey(q)) continue;
                        int cx0 = Yari(q.x - 1), cx1 = Yari(q.x), cy0 = Yari(q.y - 1), cy1 = Yari(q.y);
                        bool k00 = kume.Contains(new Vector2Int(cx0, cy0)), k10 = kume.Contains(new Vector2Int(cx1, cy0));
                        bool k01 = kume.Contains(new Vector2Int(cx0, cy1)), k11 = kume.Contains(new Vector2Int(cx1, cy1));
                        bool sinir;
                        if (besik)
                        {
                            bool once = eksen == 0 ? (k00 || k10) : (k00 || k01);
                            bool sonra = eksen == 0 ? (k01 || k11) : (k10 || k11);
                            sinir = !(once && sonra);
                        }
                        else sinir = !(k00 && k10 && k01 && k11);
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
                            var p0 = new Vector3(k0.x, k0.y, z + H[h00]); var p1 = new Vector3(k1.x, k1.y, z + H[h10]);
                            var p2 = new Vector3(k2.x, k2.y, z + H[h11]); var p3 = new Vector3(k3.x, k3.y, z + H[h01]);
                            s.cokgenler.Add(new Cokgen { p = new[] { p0, p1, p2 }, mat = catiMat, n = new Vector3(0f, 0f, 1f) });
                            s.cokgenler.Add(new Cokgen { p = new[] { p0, p2, p3 }, mat = catiMat, n = new Vector3(0f, 0f, 1f) });
                            continue;
                        }
                        var karo = CatiDesen[a + 2 * b + 4 * c + 8 * e];
                        float zs = h0 < 4 ? H[h0 + 1] - H[h0] : 1f;
                        s.Koy(karo.ad, p.x, p.y, z + H[h0], karo.don, r, zs, -A + i * A, -A + j * A);
                    }
            KenarParcasi("sacak", "sacak_kose");
            // alınlıklar (basamaklı tarzda çatıyı aşan kalın basamaklar)
            string alinMat = tarz == Fachwerk ? "Pastel_Siva_f6f0e2" : "Pastel_Duvar_" + r.D.TrimStart('#');
            foreach (var p in bolge)
                for (int i = 0; i < 4; i++)
                {
                    if (kume.Contains(p + Yon[i])) continue;
                    int dx = Yon[i].x, dy = Yon[i].y;
                    for (int q = 0; q < 2; q++)
                    {
                        Vector2Int u, v;
                        if (i == 0) { u = new Vector2Int(2 * p.x + q, 2 * p.y); v = new Vector2Int(2 * p.x + q + 1, 2 * p.y); }
                        else if (i == 1) { u = new Vector2Int(2 * p.x + 2, 2 * p.y + q); v = new Vector2Int(2 * p.x + 2, 2 * p.y + q + 1); }
                        else if (i == 2) { u = new Vector2Int(2 * p.x + 2 - q, 2 * p.y + 2); v = new Vector2Int(2 * p.x + 1 - q, 2 * p.y + 2); }
                        else { u = new Vector2Int(2 * p.x, 2 * p.y + 2 - q); v = new Vector2Int(2 * p.x, 2 * p.y + 1 - q); }
                        float hu = H[d[u]], hv = H[d[v]];
                        if (hu <= 0f && hv <= 0f) continue;
                        Vector2 pa = Kafes(u.x, u.y), pb = Kafes(v.x, v.y);
                        if (ct == CtBasamak)
                        {
                            float ust = Mathf.Max(hu, hv) + 0.45f;
                            float x0 = Mathf.Min(pa.x, pb.x), x1 = Mathf.Max(pa.x, pb.x), y0 = Mathf.Min(pa.y, pb.y), y1 = Mathf.Max(pa.y, pb.y);
                            if (dx != 0) { if (dx > 0) { x0 = pa.x - 0.28f; x1 = pa.x + 0.02f; } else { x0 = pa.x - 0.02f; x1 = pa.x + 0.28f; } }
                            else { if (dy > 0) { y0 = pa.y - 0.28f; y1 = pa.y + 0.02f; } else { y0 = pa.y - 0.02f; y1 = pa.y + 0.28f; } }
                            s.kutular.Add(new KutuVeri { x0 = x0, x1 = x1, y0 = y0, y1 = y1, z0 = z, z1 = z + ust, mat = alinMat });
                            s.kutular.Add(new KutuVeri { x0 = x0 - 0.05f, x1 = x1 + 0.05f, y0 = y0 - 0.05f, y1 = y1 + 0.05f, z0 = z + ust, z1 = z + ust + 0.09f, mat = Silme });
                        }
                        else
                        {
                            s.cokgenler.Add(new Cokgen
                            {
                                p = new[] { new Vector3(pa.x, pa.y, z), new Vector3(pb.x, pb.y, z), new Vector3(pb.x, pb.y, z + hv), new Vector3(pa.x, pa.y, z + hu) },
                                mat = alinMat, n = new Vector3(dx, dy, 0f),
                            });
                        }
                    }
                }
            // baca ve çatı penceresi
            foreach (var p in bolge)
            {
                uint hh = Karma(p.x, p.y, k, tohum + 21) % 7;
                int orta = d[new Vector2Int(2 * p.x + 1, 2 * p.y + 1)];
                if (hh == 0) s.Koy("baca", p.x, p.y, z + H[orta], 0, r, 1f, 0.35f, 0.2f);
                else if (hh == 1 && (ct == CtKirma || ct == CtMansart) && !kume.Contains(new Vector2Int(p.x, p.y - 1)) && orta >= 1 && !sade)
                    s.Koy("cati_penceresi", p.x, p.y, z, 0, r, 1f, 0f, -A);
            }
        }

        // ---------------------------------------------------------------- mesh

        static string Renkli(string ad, Renkler r)
        {
            int i = ad.IndexOf("_@", StringComparison.Ordinal);
            if (i < 0) return ad;
            string hex;
            switch (ad.Substring(i + 2))
            {
                case "D": hex = r.D; break;
                case "d": hex = r.d; break;
                case "C": hex = r.C; break;
                case "c": hex = r.c; break;
                case "T": hex = r.T; break;
                case "Y": hex = r.Y; break;
                case "y": hex = r.y; break;
                case "k": hex = r.k; break;
                default: hex = r.K; break;
            }
            return ad.Substring(0, i + 1) + hex.TrimStart('#');
        }

        static Color32 AddanRenk(string ad)
        {
            int i = ad.LastIndexOf('_');
            if (i >= 0 && ad.Length - i - 1 >= 6 &&
                int.TryParse(ad.Substring(i + 1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v))
                return new Color32((byte)(v >> 16), (byte)((v >> 8) & 255), (byte)(v & 255), 255);
            return new Color32(255, 255, 255, 255);
        }

        /// <summary>Model ekseninden (metre, Z yukarı, ön −Y) Unity model uzayına (Y yukarı, ön +Z).</summary>
        static Vector3 Unity(float mx, float my, float mz) => new Vector3(-mx, mz, -my);

        class Kenar { public Vector3 a, b; public List<(Vector3 n, Vector3 ic, Color32 renk)> yuzler = new List<(Vector3, Vector3, Color32)>(4); }

        static long Nicem(Vector3 p)
        {
            long x = Mathf.RoundToInt(p.x * 200f) + (1 << 20), y = Mathf.RoundToInt(p.y * 200f) + (1 << 20), z = Mathf.RoundToInt(p.z * 200f) + (1 << 20);
            return (x << 42) | (y << 21) | z;
        }

        /// <summary>
        /// Tasarımı tek mesh'e kurar. malzemeAdlari: alt-mesh sırasıyla malzeme adları (sonuncusu kalem çizgileri).
        /// hafifMi: uzak görünüm (ayrıntısız parçalar, yalnız uzun çizgiler). Kütüphane yoksa ya da tasarım boşsa null.
        /// </summary>
        public static Mesh Kur(BinaTasarimi t, bool hafifMi, out List<string> malzemeAdlari)
        {
            malzemeAdlari = new List<string>();
            if (t == null || Kut == null) return null;
            if ((t.bloklar == null || t.bloklar.Count == 0) && (t.susler == null || t.susler.Count == 0)) return null;
            var s = Yerlesim(t);
            float A = Hucre * 0.5f;
            var kose = new List<Vector3>();
            var alt = new Dictionary<string, List<int>>();
            // kalem çizgileri alt-mesh'i: köşe, öbür uç (uv1), yan / uç (uv0), renk
            var cKose = new List<Vector3>(); var cDiger = new List<Vector3>(); var cUv = new List<Vector2>(); var cRenk = new List<Color32>();
            var cUc = new List<int>();

            List<int> Liste(string mat)
            {
                if (!alt.TryGetValue(mat, out var l)) { alt[mat] = l = new List<int>(); }
                return l;
            }

            var gecici = new List<Vector3>();
            foreach (var y in s.yerler)
            {
                if (!parca.TryGetValue(y.ad, out var asil)) continue;
                ParcaVeri pv = asil;
                if (hafifMi)
                {
                    bool atla = asil.grup == "ayrinti" || asil.grup == "harf" || asil.grup == "simge" || (asil.grup == "cevre" && asil.kucuk);
                    if (atla && y.ad != "tas_merdiven" && y.ad != "baca") continue;
                    if (hafif.TryGetValue(y.ad, out var hv) && hv.v != null && hv.v.Length > 0) pv = hv;
                }
                bool karo = CatiKarolari.Contains(y.ad);
                float ca = Mathf.Cos(y.aci), sa = Mathf.Sin(y.aci);
                gecici.Clear();
                for (int i = 0; i + 2 < pv.v.Length; i += 3)
                {
                    float px = pv.v[i] * y.olcek, py = pv.v[i + 1] * y.olcek, pz = pv.v[i + 2] * y.olcek;
                    if (y.aci != 0f) { float tx = px * ca - py * sa; py = px * sa + py * ca; px = tx; }
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
                    gecici.Add(Unity(y.cx + px, y.cy + py, y.z + pz * y.zs));
                }
                int bas = kose.Count;
                kose.AddRange(gecici);
                foreach (var a in pv.alt)
                {
                    string mat = Renkli(a.malzeme, y.r);
                    if (mat.StartsWith("Pastel_Kabuk_"))
                    {
                        // ters kabuk: çizgi alt-mesh'ine, açılmayan (uv.x = 0) üçgenler olarak
                        if (hafifMi) continue;
                        Color32 kr = AddanRenk(mat);
                        for (int i = 0; i + 2 < a.uc.Length; i += 3)
                        {
                            int cb = cKose.Count;
                            for (int q = 0; q < 3; q++)
                            {
                                Vector3 p = gecici[a.uc[i + q]];
                                cKose.Add(p); cDiger.Add(p); cUv.Add(Vector2.zero); cRenk.Add(kr);
                            }
                            cUc.Add(cb); cUc.Add(cb + 2); cUc.Add(cb + 1);
                        }
                        continue;
                    }
                    var l = Liste(mat);
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
            foreach (var k in s.kutular) KutuEkle(kose, Liste(k.mat), k);
            if (kose.Count == 0 && cKose.Count == 0) return null;

            var adlar = new List<string>(alt.Keys);
            adlar.Sort(string.CompareOrdinal);

            // ---- kalem çizgileri: sert ve açık kenarlar (pastel_parcalar.py: kenarlar)
            var kenarlar = new Dictionary<(long, long), Kenar>();
            foreach (string mat in adlar)
            {
                if (mat.Contains("_Yazi_")) continue;
                Color32 renk = CizgiRengi(AddanRenk(mat));
                var l = alt[mat];
                for (int i = 0; i + 2 < l.Count; i += 3)
                {
                    Vector3 p0 = kose[l[i]], p1 = kose[l[i + 1]], p2 = kose[l[i + 2]];
                    Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
                    if (n.sqrMagnitude < 1e-12f) continue;
                    n.Normalize();
                    KenarEkle(kenarlar, p0, p1, p2, n, renk);
                    KenarEkle(kenarlar, p1, p2, p0, n, renk);
                    KenarEkle(kenarlar, p2, p0, p1, n, renk);
                }
            }
            float enKisa = hafifMi ? 0.9f : 0.07f;
            foreach (var ke in kenarlar.Values)
            {
                if ((ke.a - ke.b).sqrMagnitude < enKisa * enKisa) continue;
                var L = ke.yuzler;
                bool ciz = L.Count == 1;
                if (!ciz)
                {
                    bool dikis = false, keskin = false;
                    for (int i = 0; i < L.Count; i++)
                        for (int j = i + 1; j < L.Count; j++)
                        {
                            float dn = Vector3.Dot(L[i].n, L[j].n);
                            if (dn > 0.985f && Vector3.Dot(L[i].ic, L[j].ic) < -0.5f) dikis = true;
                            if (dn < 0.906f) keskin = true;
                        }
                    ciz = keskin && !dikis;
                }
                if (!ciz) continue;
                int cb = cKose.Count;
                Color32 renk = L[0].renk;
                cKose.Add(ke.a); cDiger.Add(ke.b); cUv.Add(new Vector2(-1f, 1f)); cRenk.Add(renk);
                cKose.Add(ke.a); cDiger.Add(ke.b); cUv.Add(new Vector2(1f, 1f)); cRenk.Add(renk);
                cKose.Add(ke.b); cDiger.Add(ke.a); cUv.Add(new Vector2(-1f, -1f)); cRenk.Add(renk);
                cKose.Add(ke.b); cDiger.Add(ke.a); cUv.Add(new Vector2(1f, -1f)); cRenk.Add(renk);
                cUc.Add(cb); cUc.Add(cb + 1); cUc.Add(cb + 2); cUc.Add(cb + 2); cUc.Add(cb + 1); cUc.Add(cb + 3);
            }

            // ---- mesh: önce yüzey köşeleri (beyaz köşe rengi), sonra çizgi köşeleri
            int yuzeySay = kose.Count;
            var renkler = new List<Color32>(yuzeySay + cKose.Count);
            var uv0 = new List<Vector2>(yuzeySay + cKose.Count);
            var uv1 = new List<Vector3>(yuzeySay + cKose.Count);
            var beyaz = new Color32(255, 255, 255, 255);
            for (int i = 0; i < yuzeySay; i++) { renkler.Add(beyaz); uv0.Add(Vector2.zero); uv1.Add(kose[i]); }
            kose.AddRange(cKose); renkler.AddRange(cRenk); uv0.AddRange(cUv); uv1.AddRange(cDiger);
            for (int i = 0; i < cUc.Count; i++) cUc[i] += yuzeySay;

            var mesh = new Mesh { name = t.ad + (hafifMi ? "_Hafif" : "") };
            if (kose.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(kose);
            mesh.SetColors(renkler);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            bool cizgiVar = cUc.Count > 0;
            mesh.subMeshCount = adlar.Count + (cizgiVar ? 1 : 0);
            for (int i = 0; i < adlar.Count; i++) mesh.SetTriangles(alt[adlar[i]], i);
            if (cizgiVar)
            {
                mesh.SetTriangles(cUc, adlar.Count);
                adlar.Add(CizgiMalzemesi);
            }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            malzemeAdlari = adlar;
            return mesh;
        }

        static void KenarEkle(Dictionary<(long, long), Kenar> kenarlar, Vector3 a, Vector3 b, Vector3 ucuncu, Vector3 n, Color32 renk)
        {
            long ka = Nicem(a), kb = Nicem(b);
            if (ka == kb) return;
            var anahtar = ka < kb ? (ka, kb) : (kb, ka);
            if (!kenarlar.TryGetValue(anahtar, out var ke)) kenarlar[anahtar] = ke = new Kenar { a = a, b = b };
            // yüzün içine doğru yön: üçüncü köşenin kenara dik bileşeni
            Vector3 e = (b - a).normalized;
            Vector3 ic = ucuncu - a;
            ic -= e * Vector3.Dot(ic, e);
            ke.yuzler.Add((n, ic.normalized, renk));
        }

        /// <summary>Eksenlere paralel kutu (model ekseninde verilir): altı yüz, dışa bakan.</summary>
        static void KutuEkle(List<Vector3> kose, List<int> uc, KutuVeri k)
        {
            Vector3 P(int i) => Unity((i & 1) != 0 ? k.x1 : k.x0, (i & 2) != 0 ? k.y1 : k.y0, (i & 4) != 0 ? k.z1 : k.z0);
            // her yüz: dört köşe (model ekseninde dışarıdan bakınca saat yönünün tersine)
            int[][] yuz = { new[] { 0, 2, 6, 4 }, new[] { 1, 5, 7, 3 }, new[] { 0, 4, 5, 1 }, new[] { 2, 3, 7, 6 }, new[] { 0, 1, 3, 2 }, new[] { 4, 6, 7, 5 } };
            Vector3[] dis = { Vector3.left, Vector3.right, Vector3.back, Vector3.forward, Vector3.down, Vector3.up };   // model ekseninde: −x, +x, −y, +y, −z, +z
            for (int f = 0; f < 6; f++)
            {
                int bas = kose.Count;
                Vector3 p0 = P(yuz[f][0]), p1 = P(yuz[f][1]), p2 = P(yuz[f][2]), p3 = P(yuz[f][3]);
                kose.Add(p0); kose.Add(p1); kose.Add(p2); kose.Add(p3);
                // Unity'de dışa bakan yön: model yönünün çevrilmişi; üçgen sırası buna göre seçilir (saat yönü = ön yüz)
                Vector3 md = dis[f];
                Vector3 ud = f < 2 ? new Vector3(-md.x, 0f, 0f) : f < 4 ? new Vector3(0f, 0f, -md.z) : new Vector3(0f, md.y, 0f);
                bool duz = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), ud) > 0f;
                if (duz) { uc.Add(bas); uc.Add(bas + 1); uc.Add(bas + 2); uc.Add(bas); uc.Add(bas + 2); uc.Add(bas + 3); }
                else { uc.Add(bas); uc.Add(bas + 2); uc.Add(bas + 1); uc.Add(bas); uc.Add(bas + 3); uc.Add(bas + 2); }
            }
        }

        // ---------------------------------------------------------------- malzemeler

        static readonly Dictionary<string, Material> malzemeOnbellek = new Dictionary<string, Material>();

        /// <summary>
        /// "Pastel_&lt;etiket&gt;_&lt;rrggbb&gt;" adlı malzeme (yoksa Modeller/Malzemeler altında kurulur): Pastel gölgelendiricisi, renk addan.
        /// "Pastel_Cizgi" → kalem çizgisi gölgelendiricisi. Görünüm ayarları her seferinde yazılır (eski dosyalar da güncellenir).
        /// </summary>
        public static Material Malzeme(string ad)
        {
            if (malzemeOnbellek.TryGetValue(ad, out var m) && m != null) return m;
            bool cizgi = ad.StartsWith("Pastel_Cizgi");
            string yol = MalzemeKlasor + "/" + ad + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(yol);
            var shader = Shader.Find(cizgi ? "KuryeTycoon/PastelCizgi" : "KuryeTycoon/Pastel");
            if (shader == null) shader = Shader.Find(cizgi ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (m == null)
            {
                KlasorAc(MalzemeKlasor);
                m = new Material(shader) { name = ad };
                AssetDatabase.CreateAsset(m, yol);
            }
            else if (m.shader != shader) m.shader = shader;
            m.enableInstancing = true;
            if (cizgi)
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(m);
                malzemeOnbellek[ad] = m;
                return m;
            }
            Color renk = AddanRenk(ad);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", renk);
            m.color = renk;
            // Görünüm (Pastel.shader varsayılanları): eski kaydedilmiş malzemeler de yeni değerleri alsın
            Ayar(m, "_GolgeRengi", new Color(0.80f, 0.76f, 0.86f, 1f));
            Ayar(m, "_KagitOlcek", 26f); Ayar(m, "_LekeOlcek", 0.9f); Ayar(m, "_TaramaSiklik", 14f);
            Ayar(m, "_TaramaGuc", 0.16f); Ayar(m, "_TaramaKalinlik", 0.16f); Ayar(m, "_IsiktaTarama", 0.035f);
            bool cam = ad.StartsWith("Pastel_Cam");
            Ayar(m, "_Cam", cam ? 1f : 0f);
            // Gece ışık verenler (GeceIsiklari "Pastel_Cam_", "Pastel_Isik_", "Pastel_Yazi_" adlarını tanır): gündüz ışıma siyah
            if ((cam || ad.StartsWith("Pastel_Isik") || ad.StartsWith("Pastel_Yazi")) && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(m);
            malzemeOnbellek[ad] = m;
            return m;
        }

        static void Ayar(Material m, string ad, float v) { if (m.HasProperty(ad)) m.SetFloat(ad, v); }
        static void Ayar(Material m, string ad, Color v) { if (m.HasProperty(ad)) m.SetColor(ad, v); }

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
