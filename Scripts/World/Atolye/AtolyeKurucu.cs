using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Bina Atölyesi'nin birleştiricisi (oyun derlemesinde de çalışır; UnityEditor'a bağlı değildir): tasarımdan
    /// (BinaTasarimi: bloklar + arsa süsleri) komşuluk kurallarıyla modüler pastel parçaları seçer ve mesh kurar
    /// (malzemeye göre alt-mesh + kalem çizgileri alt-mesh'i). Oyunda AtolyeUretici, editörde Bina Atölyesi penceresi çağırır.
    ///  • Parçalar ve kullanım türü tablosu: Tools/Blender/pastel_parcalar.py'nin yazdığı Resources/Atolye/pastel_parcalar.json
    ///    (Blender gerekmez). Parça koordinatları model ekseninde (metre, Z yukarı, ön −Y); burada Unity'ye çevrilir
    ///    (x, y, z) → (−x, z, −y).
    ///  • Kurallar pastel_parcalar.py'deki yerlesim() / ic_plan() / _cati() / kenarlar() ile AYNI (karma fonksiyonu dahil):
    ///    birini değiştirince öbürünü de değiştir.
    ///  • Kalem çizgileri: birleşmiş mesh'in sert (iki yüz arası &gt; 25°) ve açık kenarları ince şeritler olarak
    ///    "Pastel_Cizgi" alt-mesh'ine yazılır (KuryeTycoon/PastelCizgi ekranda sabit kalınlığa açar). Aynı düzlemde yan yana
    ///    gelen parçaların ek yerleri çizilmez. Ağaç taçlarının "Pastel_Kabuk" yüzleri de bu alt-mesh'e gider.
    ///  • Malzemeler dosya değildir: "Pastel_&lt;etiket&gt;_&lt;rrggbb&gt;" adıyla çalışırken kurulur ve önbelleğe alınır
    ///    (renk addan; gece ışıkları, mevsim ve kar sistemleri malzemeyi adından tanır).
    /// </summary>
    public static class AtolyeKurucu
    {
        public const string KutuphaneYol = "Assets/KuryeTycoon/Resources/Atolye/pastel_parcalar.json";
        public const string KutuphaneKaynak = "Atolye/pastel_parcalar";       // Resources yolu (TextAsset)
        public const string TasarimKaynak = "Atolye/Tasarimlar";              // Resources klasörü (tasarım JSON'ları)
        public const string CizgiMalzemesi = "Pastel_Cizgi_3a2f3a";
        public const int Apartman = 0, Fachwerk = 1, Tas = 2, Kaide = 3, Merdiven = 4;
        const int CtOto = 0, CtKirma = 1, CtBesik = 2, CtMansart = 3, CtTeras = 4, CtBasamak = 5, CtTestere = 6, CtParapet = 7;
        static readonly string[] TemelTarzAd = { "Boyalı sıva teras (pastel)", "Yarı ahşap (Tudor) ev", "Taş kule", "Taş kaide / teras", "Taş merdiven" };
        /// <summary>Duvar tarzı adları: beş temel tarz + kütüphanedeki Londra üslupları (pastel_parcalar.py: TARZ_AD).</summary>
        public static string[] TarzAd => Kut != null && Kut.tarzAd != null && Kut.tarzAd.Length > 0 ? Kut.tarzAd : TemelTarzAd;
        public static readonly string[] Bolgeler = { "eski", "karma", "kule", "ev", "kampus", "sanayi", "avm" };

        // ---------------------------------------------------------------- kütüphane (JSON)

        [Serializable] public class AltVeri { public string malzeme; public int[] uc; }
        [Serializable] public class ParcaVeri { public string ad, grup, aciklama, hedef; public bool kucuk; public int sinir; public float[] v; public AltVeri[] alt; }   // sinir: bir iç mekânda en fazla kaç tane (0: varsayılan)
        [Serializable]
        public class TurVeri
        {
            public string ad, etiket, grup, onek, tabela, simge, on, kapi, duvar, cephe, tepe, vurgu, bolge;
            public int tente;
            public string[] aks;
            // iç mekân (zemin kat): kasa hücresinin parçası, öbür hücrelerin parçaları, kapı sütununun dibindeki parça, zemin
            public string icKasa, icArka, zemin;
            public string[] ic;
            public int calisan, kepenk;      // kepenk = 1: dükkân (kepenk, sipariş poşeti, kurye)
        }
        [Serializable] public class ZeminTurVeri { public string ad, etiket; }
        /// <summary>
        /// Londra duvar üslubu (pastel_parcalar.py: USLUPLAR; bloğun tarz alanı uslupTaban + sıra). kapi: kapı yüzü ve konutta
        /// her kapiAralik hücrede bir ek kapı; onZemin / zemin: zemin katın ön / öbür yüzleri; onKat1 / kat1: birinci kat;
        /// ust: ikinci kat ve üstü; tavan: sütunun en üst katı; duz: cephe süsünün arkasındaki düz duvar; koseZemin / kose,
        /// kaide, silmeZemin / silme: boşsa konmaz; cati: çatı tarzı "Tarza göre" iken kullanılan tarz.
        /// </summary>
        [Serializable]
        public class UslupVeri
        {
            public string ad, etiket, kapi, duz, kose, koseZemin, kaide, silme, silmeZemin;
            public string[] zemin, onZemin, kat1, onKat1, ust, tavan;
            public int kapiAralik, cati;
        }
        [Serializable]
        public class Kutuphane
        {
            public int surum;
            public string[] tarzAd;          // duvar tarzı adları (temel beş + üsluplar)
            public int uslupTaban = 5;       // bu tarz ve üstü: usluplar[tarz − uslupTaban]
            public UslupVeri[] usluplar;
            public float hucre = 2.8f, kat = 3f;
            public int izgara = 5;
            public int icSinir = 6;          // bir iç mekânda aynı mobilyadan en fazla kaç tane (parçada sinir yazmıyorsa)
            public string[] icYedek;         // türün listesi tükenince kullanılan dolgu mobilyaları
            // sahne zemini (Sahne Atölyesi): kademe boyu, dış etek boyu, kaldırım üst kotu, zemin türleri, "sert" türler (istinat duvarı)
            public float kademe = 1.2f, etek = 0.6f, kaldirimZ = 0.12f;
            public ZeminTurVeri[] zeminTur;
            public string[] sertZemin;
            public string[] duvarRenk, catiRenk, vurguRenk, tenteRenk, yaprakRenk, catiTarz;
            public TurVeri[] turler;
            public ParcaVeri[] parcalar, hafifler;
            public ParcaVeri[] uzaklar;      // en uzak görünüm (LOD2): yalnız ağaç ve çalılar
            public int kisRengi = 7;         // bu yaprak rengi önizlemede kışı gösterir (yalnız bilgi; oyunda _PastelKis)
        }

        static Kutuphane kut;
        static Dictionary<string, ParcaVeri> parca, hafif, uzak;
        static Dictionary<string, TurVeri> tur;
        static readonly TurVeri BosTur = new TurVeri { ad = "konut", etiket = "Konut", grup = "Konut", onek = "Bolge", bolge = "eski", zemin = "ic_zemin_ahsap" };

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
            uzak = new Dictionary<string, ParcaVeri>();
            tur = new Dictionary<string, TurVeri>();
            string metin = null;
#if UNITY_EDITOR
            // Editörde önce diskten oku: içe aktarma henüz bitmemişken TextAsset eski içeriği verebilir
            try
            {
                string tam = Path.Combine(Application.dataPath, "KuryeTycoon/Resources/Atolye/pastel_parcalar.json");
                if (File.Exists(tam)) metin = File.ReadAllText(tam);
            }
            catch (Exception) { metin = null; }
#endif
            if (string.IsNullOrEmpty(metin))
            {
                var ta = Resources.Load<TextAsset>(KutuphaneKaynak);
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
            if (k == null || k.parcalar == null || k.parcalar.Length == 0 || k.surum < 5 || k.turler == null || k.yaprakRenk == null || k.tenteRenk == null)
            {
                Debug.LogError("Bina Atölyesi: parça kütüphanesi eski ya da eksik (sürüm 5 gerekli). python3 Tools/Blender/pastel_parcalar.py --json");
                return false;
            }
            foreach (var p in k.parcalar) parca[p.ad] = p;
            if (k.hafifler != null) foreach (var p in k.hafifler) hafif[p.ad] = p;
            if (k.uzaklar != null) foreach (var p in k.uzaklar) uzak[p.ad] = p;
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
        static bool Bos(string[] l) => l == null || l.Length == 0;

        /// <summary>Tarzın Londra üslubu (tabloda yoksa null: eski tarzlar). pastel_parcalar.py: uslup.</summary>
        static UslupVeri Uslup(int tarz)
        {
            var kt = Kut;
            if (kt == null || kt.usluplar == null) return null;
            int i = tarz - kt.uslupTaban;
            return i >= 0 && i < kt.usluplar.Length ? kt.usluplar[i] : null;
        }

        /// <summary>İçi olan, kapı alabilen gövde tarzı mı (kaide ve merdiven değil). pastel_parcalar.py: govdeli.</summary>
        static bool Govdeli(int tarz) => tarz == Apartman || tarz == Fachwerk || tarz == Tas || Uslup(tarz) != null;

        /// <summary>Tasarım JSON'undan (pastel_parcalar.py --ornekler ya da atölye penceresi) tasarım nesnesi. Bozuksa null.</summary>
        public static BinaTasarimi TasarimOku(string json)
        {
            if (Bos(json)) return null;
            var t = ScriptableObject.CreateInstance<BinaTasarimi>();
            t.hideFlags = HideFlags.HideAndDontSave;
            try { JsonUtility.FromJsonOverwrite(json, t); }
            catch (Exception ex)
            {
                Debug.LogWarning("Bina Atölyesi: tasarım okunamadı: " + ex.Message);
                UnityEngine.Object.DestroyImmediate(t);
                return null;
            }
            if (t.bloklar == null) t.bloklar = new List<BinaTasarimi.Blok>();
            if (t.susler == null) t.susler = new List<BinaTasarimi.Sus>();
            if (t.tur == null) t.tur = "";
            if (t.tabela == null) t.tabela = "";
            t.ad = TemizAd(t.ad);
            return t;
        }

        /// <summary>Dosya ve model adı için: küçük harf, rakam ve alt çizgi.</summary>
        public static string TemizAd(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in (s ?? "").Trim().ToLowerInvariant())
                sb.Append((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ? c : '_');
            return sb.Length == 0 ? "bina" : sb.ToString();
        }

        /// <summary>Oyundaki model adı: &lt;önek&gt;_atolye_&lt;ad&gt; (City3D havuzları ada bakar).</summary>
        public static string ModelAdi(BinaTasarimi t) => Onek(t) + "_atolye_" + TemizAd(t.ad);

        // ---------------------------------------------------------------- kurallar (pastel_parcalar.py: yerlesim)

        // Y, y, a, m: yaprağın dört tonu (orta, koyu, açık sarımsı, gölge yanı); k: taç dış hattı
        class Renkler { public string D, d, C, c, K, T, Y, y, a, m, k; }

        struct Yer
        {
            public string ad;
            public float cx, cy, z, zs, olcek, aci;
            public int don;
            public Vector2 kay;
            public Renkler r;
            public string grup;      // prefabdaki ayrı çocuk: Govde, Cati, Duvar_On/Sag/Arka/Sol, Ic, Ampul, Poset, Kepenk
            public int cam;          // pencere ışık grubu (0–2): Pastel_Cam_, Pastel_Cam2_, Pastel_Cam3_
        }

        struct Cokgen { public Vector3[] p; public string mat; public Vector3 n; public string grup; }
        struct KutuVeri { public float x0, x1, y0, y1, z0, z1; public string mat; public string grup; }

        public static readonly string[] DuvarGrup = { "Duvar_On", "Duvar_Sag", "Duvar_Arka", "Duvar_Sol" };
        static bool GizliGrup(string g) => g == "Ic" || g == "Ampul" || g == "Poset" || g == "Kepenk";

        /// <summary>Zemin katın iç yerleşimi (pastel_parcalar.py: ic_plan). Hücreler (x, y) ızgara koordinatında.</summary>
        public class IcPlani
        {
            public List<Vector2Int> hucreler = new List<Vector2Int>();
            public HashSet<Vector2Int> gecit = new HashSet<Vector2Int>();
            public Dictionary<Vector2Int, string> mobilya = new Dictionary<Vector2Int, string>();
            public bool kasaVar;
            public Vector2Int kasa, kapi;
        }

        static readonly Vector2Int[] Yon = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) };
        static readonly string[] AptUst = { "duvar_pencere", "duvar_pencere", "duvar_cift", "duvar_kemerli", "duvar_saksi", "duvar_panjur", "duvar_dar", "duvar_balkon", "duvar_duz", "duvar_pencere",
                                            "duvar_alinlik", "duvar_panjur_kapali", "duvar_stor" };
        static readonly string[] DuzVaryantlar = { "duvar_duz_tugla", "duvar_duz_kutu" };
        /// <summary>
        /// Kalem çizgisi (pastel_parcalar.py: EN_KISA, KALIN_BOY, KALIN, INCE): kısa kenar çizilmez; 1,35 m ve uzun kenarlar
        /// (bina köşesi, saçak, mahya, kat çizgisi) × 1,0, ayrıntı kenarları × 0,45 (v6: inceldi). "Pastel_Iz_" malzemeli
        /// yüzeyler (tuğla / arduvaz sırası izi, cam kayıtları, korkuluk çubukları) hiç kontur almaz.
        /// </summary>
        const float EnKisa = 0.13f, KalinBoy = 1.35f, Kalin = 1f, Ince = 0.45f;

        /// <summary>Düz apartman duvarının varyantı (altıda bir tuğla yamalı, altıda bir klimalı / kutulu). pastel_parcalar.py: duz_varyant.</summary>
        static string DuzVaryant(int x, int y, int i, int k, int tohum)
        {
            int v = Sec(Karma(x * 4 + i, y, k, tohum + 61), 6);
            string ad = v < DuzVaryantlar.Length ? DuzVaryantlar[v] : "duvar_duz";
            return parca != null && parca.ContainsKey(ad) ? ad : "duvar_duz";
        }
        static readonly string[] FwUst = { "fachwerk_pencere", "fachwerk_capraz", "fachwerk_saksi", "fachwerk_x", "fachwerk_pencere" };
        static readonly string[] TasUst = { "tas_duvar", "tas_mazgal", "tas_kemerli", "tas_duvar" };
        static readonly HashSet<string> SaksiAlir = new HashSet<string> { "duvar_pencere", "duvar_cift", "duvar_panjur", "duvar_panjur_kapali" };
        static readonly HashSet<string> CatiKarolari = new HashSet<string> { "cati_duz", "cati_egim", "cati_kose", "cati_vadi", "cati_semer" };
        static readonly float[] HKirma = { 0f, 0.85f, 1.7f, 2.55f, 3.4f }, HBesik = { 0f, 1.25f, 2.5f, 3.75f, 5f }, HMansart = { 0f, 2f, 2.45f, 2.9f, 3.35f },
                                  HParapet = { 0f, 1.3f, 1.6f, 1.9f, 2.2f };
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

        /// <summary>İki rengin karışımı (t = 0 → h1, 1 → h2). pastel_parcalar.py: karis.</summary>
        static string Karis(string h1, string h2, float t)
        {
            Color32 p = Renk(h1), q = Renk(h2);
            return "#" + ((int)(p.r * (1f - t) + q.r * t)).ToString("x2") + ((int)(p.g * (1f - t) + q.g * t)).ToString("x2") + ((int)(p.b * (1f - t) + q.b * t)).ToString("x2");
        }

        /// <summary>
        /// Yaprak renginden açık (ışık) ve gölge yanı tonları (pastel_parcalar.py: yaprak_tonlari): yeşilde gölge mavimsi,
        /// sonbahar gibi sıcak renkte kızıl (aynı ağaçta sarı-turuncu-kızıl karışır).
        /// </summary>
        static void YaprakTonlari(string yp, out string acik, out string golge)
        {
            Color32 c = Renk(yp);
            acik = Karis(yp, "#f6ecaa", 0.32f);
            golge = c.r > c.g ? Karis(Koyu(yp, 0.92f), "#c0503a", 0.42f) : Karis(Koyu(yp, 0.9f), "#5f8fa8", 0.24f);
        }

        /// <summary>Kalem çizgisi rengi: yüzey renginin koyusu, sıcak mor-kahveye kaydırılmış; v6'da daha açık (siyaha yakın değil).</summary>
        static Color32 CizgiRengi(Color32 c)
        {
            return new Color32((byte)(c.r * 0.33f + 0.17f * 255f), (byte)(c.g * 0.33f + 0.14f * 255f), (byte)(c.b * 0.33f + 0.18f * 255f), 255);
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
            public IcPlani plan = new IcPlani();
            public string zorlaGrup;
            public float kat = 3f;

            public bool Var(int x, int y, int k) => blok.ContainsKey(new Vector3Int(x, y, k));
            public bool Dolu(int x, int y, int k) => blok.TryGetValue(new Vector3Int(x, y, k), out var b) && b.tarz != Merdiven;

            public bool OnAcik(int x, int y)
            {
                return blok.TryGetValue(new Vector3Int(x, y, 0), out var b) && Govdeli(b.tarz) && !Dolu(x, y - 1, 0);
            }

            public Renkler RenkleriAl(int renk, int cati, int yaprak)
            {
                var kt = Kut;
                string dv = kt.duvarRenk[Mod(renk, kt.duvarRenk.Length)];
                string ct = kt.catiRenk[Mod(cati, kt.catiRenk.Length)];
                string vr = !Bos(T.vurgu) ? T.vurgu : kt.vurguRenk[Sec(Karma(t.tohum, renk, cati, 7), kt.vurguRenk.Length)];
                string yp = kt.yaprakRenk[Mod(yaprak, kt.yaprakRenk.Length)];
                YaprakTonlari(yp, out string acik, out string golge);
                return new Renkler
                {
                    D = dv, d = Koyu(dv, 0.9f), C = ct, c = Koyu(ct, 0.86f), K = vr, T = kt.tenteRenk[Mod(t.tente, kt.tenteRenk.Length)],
                    Y = yp, y = Koyu(yp, 0.88f), a = acik, m = golge, k = CizgiRengiHex(yp),
                };
            }

            public Renkler RenkleriAl(BinaTasarimi.Blok b) => RenkleriAl(b.renk, b.cati, 0);

            /// <summary>grup verilmezse birinci kat ve üstü "Cati" (kamera yaklaşınca kalkan parça), zemin "Govde".</summary>
            public void Koy(string ad, int x, int y, float z, int don, Renkler r, float zs = 1f, float kx = 0f, float ky = 0f, float olcek = 1f, string grup = null)
            {
                float cx = (x - (Izgara - 1) * 0.5f) * Hucre, cy = (y - (Izgara - 1) * 0.5f) * Hucre;
                if (grup == null) grup = zorlaGrup ?? (z >= kat - 0.01f ? "Cati" : "Govde");
                int cam = Sec(Karma(x * 4 + Mod(don, 4), y, Mathf.RoundToInt(z / kat), t.tohum + 41), 3);
                yerler.Add(new Yer
                {
                    ad = ad, cx = cx, cy = cy, z = z, don = Mod(don, 4), zs = zs, kay = new Vector2(kx, ky), r = r, olcek = olcek, aci = 0f,
                    grup = grup, cam = cam,
                });
            }
        }

        /// <summary>
        /// İç yerleşim (pastel_parcalar.py: ic_plan ile AYNI). Mobilyaların hepsi öne (−Y) bakar ve hücrenin arka yarısında
        /// durur: her sıranın ön yarısı yürüme koridorudur. Kapı sütunu boş "geçit"tir; koridorlardan ulaşılamayan hücre kalırsa
        /// önündeki hücre de geçit olur (blok şekli ne olursa olsun kapıdan her hücreye yol açık).
        /// </summary>
        static IcPlani IcPlan(Durum s, bool kapiVar, int kx, int ky)
        {
            var plan = new IcPlani();
            var Z = new List<Vector2Int>();
            foreach (var b in s.blok.Values)
                if (b.k == 0 && Govdeli(b.tarz)) Z.Add(new Vector2Int(b.x, b.y));
            if (!kapiVar || Z.Count == 0) return plan;
            Z.Sort((p, q) => p.x != q.x ? p.x.CompareTo(q.x) : p.y.CompareTo(q.y));
            var zs = new HashSet<Vector2Int>(Z);
            plan.hucreler = Z;
            plan.kapi = new Vector2Int(kx, ky);
            var kolon = new List<Vector2Int>();
            for (int y = ky; zs.Contains(new Vector2Int(kx, y)); y++) kolon.Add(new Vector2Int(kx, y));
            var E = plan.gecit;
            for (int i = 0; i + 1 < kolon.Count; i++) E.Add(kolon[i]);
            while (true)
            {
                var R = Erisim(zs, E, plan.kapi);
                if (R.Count == zs.Count) break;
                bool bulundu = false;
                var ek = new Vector2Int();
                foreach (var u in Z)
                {
                    if (R.Contains(u)) continue;
                    var on = new Vector2Int(u.x, u.y - 1);
                    if (R.Contains(on)) { ek = on; bulundu = true; break; }                       // öndeki hücre geçit olur
                    if (R.Contains(new Vector2Int(u.x, u.y + 1))) { ek = u; bulundu = true; break; }
                }
                if (!bulundu) break;                                                             // yalnız çaprazdan bağlı parça
                E.Add(ek);
            }
            var T = s.T;
            var son = kolon[kolon.Count - 1];
            // Aynı mobilyadan en fazla "sinir" tane: dolunca listedeki sıradakine, o da dolunca yedeklere geçilir
            var say = new Dictionary<string, int>();
            bool Al(string ad)
            {
                int sinir = parca.TryGetValue(ad, out var pv) && pv.sinir > 0 ? pv.sinir : Mathf.Max(1, Kut.icSinir);
                say.TryGetValue(ad, out int n);
                if (n >= sinir) return false;
                say[ad] = n + 1;
                return true;
            }
            if (!Bos(T.icKasa))
            {
                var adaylar = new[] { new Vector2Int(kx + 1, ky), new Vector2Int(kx - 1, ky), son };
                foreach (var c in adaylar)
                    if (zs.Contains(c) && !E.Contains(c))
                    {
                        plan.kasaVar = true;
                        plan.kasa = c;
                        plan.mobilya[c] = T.icKasa;
                        Al(T.icKasa);
                        break;
                    }
            }
            if (!E.Contains(son) && !plan.mobilya.ContainsKey(son) && !Bos(T.icArka) && Al(T.icArka)) plan.mobilya[son] = T.icArka;
            int j = 0;
            if (T.ic != null && T.ic.Length > 0)
            {
                // ön sıradan (sokak tarafı) arkaya, soldan sağa
                var sirali = new List<Vector2Int>(Z);
                sirali.Sort((p, q) => p.y != q.y ? p.y.CompareTo(q.y) : p.x.CompareTo(q.x));
                foreach (var c in sirali)
                {
                    if (E.Contains(c) || plan.mobilya.ContainsKey(c)) continue;
                    string sec = null;
                    for (int d = 0; d < T.ic.Length; d++)
                    {
                        string ad = T.ic[(j + d) % T.ic.Length];
                        if (Al(ad)) { sec = ad; j += d + 1; break; }
                    }
                    if (sec == null && Kut.icYedek != null)
                        foreach (string ad in Kut.icYedek)
                            if (Al(ad)) { sec = ad; break; }
                    if (sec != null) plan.mobilya[c] = sec;
                }
            }
            return plan;
        }

        /// <summary>Kapı hücresinden koridorlarla ulaşılan hücreler: yanlara hep; arkaya yalnız öndeki hücre geçitse.</summary>
        static HashSet<Vector2Int> Erisim(HashSet<Vector2Int> zs, HashSet<Vector2Int> E, Vector2Int bas)
        {
            var R = new HashSet<Vector2Int> { bas };
            var yigin = new Stack<Vector2Int>();
            yigin.Push(bas);
            while (yigin.Count > 0)
            {
                var p = yigin.Pop();
                for (int i = 0; i < 4; i++)
                {
                    Vector2Int q;
                    bool gec;
                    if (i == 0) { q = new Vector2Int(p.x - 1, p.y); gec = true; }
                    else if (i == 1) { q = new Vector2Int(p.x + 1, p.y); gec = true; }
                    else if (i == 2) { q = new Vector2Int(p.x, p.y + 1); gec = E.Contains(p); }
                    else { q = new Vector2Int(p.x, p.y - 1); gec = E.Contains(q); }
                    if (gec && zs.Contains(q) && R.Add(q)) yigin.Push(q);
                }
            }
            return R;
        }

        static Durum Yerlesim(BinaTasarimi t)
        {
            float A = Hucre * 0.5f, KAT = KatBoyu, HUCRE = Hucre;
            var T = TurBul(t);
            var s = new Durum { t = t, T = T, kat = KAT };
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
                var U = Uslup(tarz);
                // üst katlar kalkınca kaidenin üstü açık kalmasın
                if (tarz == Kaide && k == 0 && s.Dolu(x, y, 1)) s.Koy("taban", x, y, KAT, 0, r, 1f, 0f, 0f, 0.97f, "Govde");
                for (int i = 0; i < 4; i++)
                {
                    if (s.Dolu(x + Yon[i].x, y + Yon[i].y, k)) continue;
                    string dg = (k == 0 && tarz != Kaide) ? DuvarGrup[i] : null;      // zemin kat duvarı: kameraya bakınca iner
                    uint h = Karma(x * 4 + i, y, tohum, 11);                 // sütun boyunca aynı: pencereler üst üste hizalı
                    uint hk = Karma(x * 4 + i, y, tohum + k * 31, 17);
                    bool kapiYuzu = k == 0 && i == 0 && kapiHucresi;
                    bool onZemin = k == 0 && i == 0 && tarz != Kaide;
                    bool cepheYuzu = !Bos(T.cephe) && kapiHucresi && i == 0 && k == kapiUst && k > 0;
                    bool ekKapi = false;
                    string ad;
                    if (tarz == Kaide) ad = "tas_duvar";
                    else if (kapiYuzu) ad = !Bos(T.kapi) ? T.kapi : U != null ? U.kapi : tarz == Tas ? "kapi_kemerli" : "kapi_ahsap";
                    else if (onZemin && !Bos(T.on)) ad = T.on;
                    else if (cepheYuzu) ad = U != null ? U.duz : "duvar_duz";
                    else if (sade) ad = T.duvar;
                    else if (U != null)
                    {
                        // Londra üslubu: kat ve yöne göre liste; konutta kapı sırasında her kapiAralik hücrede bir ek kapı
                        string[] liste;
                        if (k == 0)
                        {
                            ekKapi = i == 0 && T.grup == "Konut" && U.kapiAralik > 0 && kapiVar && y == kapiY && Mod(x - kapiX, U.kapiAralik) == 0;
                            liste = (i == 0 && !Bos(U.onZemin)) ? U.onZemin : U.zemin;
                        }
                        else if (k == 1)
                        {
                            liste = (i == 0 && !Bos(U.onKat1)) ? U.onKat1 : U.kat1;
                            if (Bos(liste)) liste = U.ust;
                        }
                        else if (!s.Dolu(x, y, k + 1)) liste = !Bos(U.tavan) ? U.tavan : U.ust;
                        else liste = U.ust;
                        ad = ekKapi ? U.kapi : Bos(liste) ? U.duz : liste[Sec(h, liste.Length)];
                    }
                    else if (tarz == Tas) ad = TasUst[Sec(h + (hk % 3 == 0 ? 1u : 0u), TasUst.Length)];
                    else if (tarz == Fachwerk) ad = k == 0 ? (h % 3 != 0 ? "duvar_pencere" : "duvar_duz") : FwUst[Sec(h, FwUst.Length)];
                    else if (k == 0) ad = h % 4 != 0 ? "zemin_pencere" : "duvar_duz";
                    else
                    {
                        ad = AptUst[Sec(h, AptUst.Length)];
                        if (ad == "duvar_balkon" && hk % 2 != 0) ad = "duvar_pencere";
                    }
                    string cizilen = ad;
                    if (ad == "duvar_duz" && tarz == Apartman && Bos(T.duvar) && !cepheYuzu) cizilen = DuzVaryant(x, y, i, k, tohum);
                    s.Koy(cizilen, x, y, z, i, r, 1f, 0f, 0f, 1f, dg);
                    if (cepheYuzu && tarz != Kaide) s.Koy(T.cephe, x, y, z, i, r);
                    if (SaksiAlir.Contains(ad) && hk % 5 == 0) s.Koy("cicek_kutusu_p", x, y, z, i, r, 1f, 0f, 0f, 1f, dg);
                    if (onZemin && T.tente != 0 && (kapiYuzu || !Bos(T.on))) s.Koy("tente", x, y, z, i, r, 1f, 0f, 0f, 1f, dg);
                    if (T.kepenk != 0 && onZemin && (kapiYuzu || !Bos(T.on))) s.Koy("kepenk", x, y, z, i, r, 1f, 0f, 0f, 1f, "Kepenk");
                    // Kaide: kapı, dükkân cephesi ve ek kapı yüzlerinde yok (o parçaların kendi eteği var)
                    if (k == 0 && tarz != Kaide && !sade && !kapiYuzu && !(onZemin && !Bos(T.on)) && !ekKapi)
                    {
                        string kd = U != null ? U.kaide : "kaide";
                        if (!Bos(kd)) s.Koy(kd, x, y, z, i, r, 1f, 0f, 0f, 1f, dg);
                    }
                    if (U != null)
                    {
                        string sm = k == 0 ? U.silmeZemin : U.silme;
                        if (!Bos(sm)) s.Koy(sm, x, y, z + KAT, i, r);
                    }
                    else if (tarz == Apartman && Bos(T.duvar)) s.Koy("silme", x, y, z + KAT, i, r);
                    else if (tarz == Fachwerk && k > 0) s.Koy("silme_ahsap", x, y, z, i, r);
                    if (k == 0 && i != 0 && (tarz == Apartman || tarz == Fachwerk) && Bos(T.duvar))
                    {
                        uint a = Karma(x, y, i, tohum + 5) % 14;
                        if (a == 0) s.Koy("fici", x, y, z, i, r, 1f, 0.7f, -A - 0.45f);
                        else if (a == 1) s.Koy("saksi", x, y, z, i, r, 1f, -0.8f, -A - 0.38f);
                        else if (a == 2 && ad == "duvar_duz") s.Koy("sarmasik", x, y, z, i, r, 1f, 0f, 0f, 1f, dg);
                    }
                    if (kapiYuzu && U == null) s.Koy("paspas", x, y, z, i, r, 1f, 0f, -A - 0.75f);
                    if (k == 0 && tarz == Apartman && ad == "zemin_pencere" && Karma(x, y, i, tohum + 9) % 5 == 0)
                        s.Koy("fener", x, y, z, i, r, 1f, 1.15f, 0f, 1f, dg);
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
                        string kose;
                        if (U != null) kose = k == 0 ? U.koseZemin : U.kose;
                        else
                        {
                            kose = tarz == Tas || tarz == Kaide ? "kose_tas" : (tarz == Fachwerk && k > 0) ? "kose_ahsap" : "kose_dis";
                            // sütun boyunca aynı: yağmur iniş borusu çatıdan yere iner
                            if (kose == "kose_dis" && Karma(x, y, i, tohum + 71) % 4 == 0 && parca.ContainsKey("kose_boru")) kose = "kose_boru";
                        }
                        if (!Bos(kose)) s.Koy(kose, x, y, z, i, r);
                    }
                    else if (U == null && !aBos && !cBos && !s.Dolu(x + a.x + c.x, y + a.y + c.y, k))
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
                    foreach (int x in sira) s.Koy("tabela_bant", x, kapiY, 0f, 0, r, 1f, 0f, 0f, 1f, "Duvar_On");
                    float W = sira.Count * HUCRE;
                    float sol = !Bos(T.simge) ? 0.75f : 0.2f;
                    int n = metin.Length;
                    float px = Mathf.Min(0.075f, (W - sol - 0.2f) / (n * 6 - 1));
                    float x0 = -A + sol + (W - sol - 0.2f - (n * 6 - 1) * px) / 2f;
                    for (int j = 0; j < n; j++)
                    {
                        string ha = HarfAdi(metin[j]);
                        if (ha != null) s.Koy(ha, sira[0], kapiY, 2.54f - 3.5f * px, 0, r, 1f, x0 + j * 6 * px, -A - 0.13f, px, "Duvar_On");
                    }
                    if (!Bos(T.simge)) s.Koy(T.simge, sira[0], kapiY, 0f, 0, r, 1f, -A + 0.4f, 0f, 1f, "Duvar_On");
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

            // iç mekân (zemin kat): zemin kaplaması, tavan lambaları, türe göre mobilya
            s.plan = IcPlan(s, kapiVar, kapiX, kapiY);
            string zeminAd = !Bos(T.zemin) ? T.zemin : "ic_zemin_ahsap";
            foreach (var h in s.plan.hucreler)
            {
                var r = s.RenkleriAl(s.blok[new Vector3Int(h.x, h.y, 0)]);
                s.Koy(zeminAd, h.x, h.y, 0f, 0, r, 1f, 0f, 0f, 1f, "Ic");
                if (Mod(h.x + h.y, 2) == Mod(s.plan.kapi.x + s.plan.kapi.y, 2)) s.Koy("ic_lamba", h.x, h.y, 0f, 0, r, 1f, 0f, 0f, 1f, "Ampul");
                if (s.plan.mobilya.TryGetValue(h, out string mob)) s.Koy(mob, h.x, h.y, 0f, 0, r, 1f, 0f, 0f, 1f, "Ic");
            }
            if (s.plan.kasaVar && T.kepenk != 0)
                s.Koy("ic_poset", s.plan.kasa.x, s.plan.kasa.y, 1.05f, 0, s.RenkleriAl(s.blok[new Vector3Int(s.plan.kasa.x, s.plan.kasa.y, 0)]), 1f, 0.1f, 0.45f, 1f, "Poset");

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
                    // kaide / terasın üstü yerinde kalır; öbür çatılar (ve üst katlar) "Cati" grubunda kalkar
                    string cg = b0.tarz == Kaide ? "Govde" : "Cati";
                    s.zorlaGrup = cg;
                    int n0 = s.cokgenler.Count, n1 = s.kutular.Count;
                    Cati(s, bolge, b0, k, !Bos(T.duvar));
                    s.zorlaGrup = null;
                    for (int i = n0; i < s.cokgenler.Count; i++) { var c = s.cokgenler[i]; c.grup = cg; s.cokgenler[i] = c; }
                    for (int i = n1; i < s.kutular.Count; i++) { var c = s.kutular[i]; c.grup = cg; s.kutular[i] = c; }
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
                        olcek = su.olcek > 0f ? su.olcek : 1f, aci = su.aci * Mathf.Deg2Rad, grup = "Govde", cam = 0,
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
                // kenar parçası hücrenin kendi rengini alır (farklı renkli sıra evlerde parapet / saçak eve göre)
                foreach (var p in bolge)
                {
                    var rr = s.blok.TryGetValue(new Vector3Int(p.x, p.y, k), out var hb) ? s.RenkleriAl(hb) : r;
                    for (int i = 0; i < 4; i++)
                        if (!kume.Contains(p + Yon[i])) s.Koy(ad, p.x, p.y, z, i, rr);
                    if (koseAd == null) continue;
                    for (int i = 0; i < 4; i++)
                        if (!kume.Contains(p + Yon[(i + 3) % 4]) && !kume.Contains(p + Yon[i])) s.Koy(koseAd, p.x, p.y, z, i, rr);
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
                var U = Uslup(tarz);
                ct = U != null ? U.cati : tarz == Fachwerk ? CtBesik : CtKirma;
                if (ct < CtKirma || ct > CtParapet) ct = CtKirma;       // bozuk tablo: kırma
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
            float[] H = ct == CtMansart ? HMansart : ct == CtParapet ? HParapet : besik ? HBesik : HKirma;
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
            // parapet arkası: iki kademeden sonra düz kurşun tepe (çapraz kırık çizgileri olmasın)
            int ustSinir = ct == CtParapet ? 2 : 4;
            foreach (var p in anahtarlar) d[p] = Mathf.Clamp(d[p], 0, ustSinir);

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
            if (ct == CtParapet)
            {
                // Londra: saçak yerine parapet; ortak duvar hizalarında sıra baca; ön kenarda mansart pencereleri
                KenarParcasi("parapet", "parapet_kose");
                int a0 = eksen == 0 ? xMin : yMin, a1 = eksen == 0 ? xMax : yMax;       // mahya ekseni boyunca
                int c0 = eksen == 0 ? yMin : xMin, c1 = eksen == 0 ? yMax : xMax;       // mahyaya dik
                int cb = Yari(c0 + c1);
                float kayma = ((c0 + c1) * 0.5f - cb) * Hucre;                          // bölge çift sıra derinse baca mahya çizgisinde
                float ky = eksen == 0 ? kayma : -kayma;
                for (int a = a0; a <= a1; a++)
                {
                    int hx = eksen == 0 ? a : cb, hy = eksen == 0 ? cb : a;
                    if (!kume.Contains(new Vector2Int(hx, hy))) continue;
                    if ((a - a0) % 2 == 0) s.Koy("baca_sira", hx, hy, z, eksen, r, 1f, -A + 0.3f, ky);
                    if (a == a1) s.Koy("baca_sira", hx, hy, z, eksen, r, 1f, A - 0.3f, ky);
                }
                foreach (var p in bolge)
                    if (Karma(p.x, p.y, k, tohum + 21) % 2 == 0 && !kume.Contains(new Vector2Int(p.x, p.y - 1)) && d[new Vector2Int(2 * p.x + 1, 2 * p.y + 1)] >= 1 && !sade)
                        s.Koy("mansart_penceresi", p.x, p.y, z, 0, r, 1f, 0f, -A);
                return;
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

        // ---------------------------------------------------------------- sahne zemini (pastel_parcalar.py: zemin_yerlesim)

        static readonly Renkler SahneRenk = new Renkler
        {
            D = "#f6f0e2", d = "#e3dccb", C = "#c8553f", c = "#ad4835", K = "#3f7d74", T = "#c8553f", Y = "#8fbf7a", y = "#7da86b", a = "#afcd89", m = "#78a47b", k = "#42443d",
        };

        public static float Kademe => Kut != null ? Kut.kademe : 1.2f;
        public static float KaldirimZ => Kut != null ? Kut.kaldirimZ : 0.12f;

        /// <summary>Sahne hücresinin ortası (model ekseni, metre).</summary>
        public static Vector2 SahneMerkezi(int n, int x, int y) => new Vector2((x - (n - 1) * 0.5f) * Hucre, (y - (n - 1) * 0.5f) * Hucre);

        /// <summary>
        /// Sahnenin zemin parçaları (pastel_parcalar.py: zemin_yerlesim ile AYNI, karma dahil). Boyanmamış hücre çim, kademe 0.
        /// Yol: komşuya göre şerit / yaya geçidi / rögar; yol olmayan kenarına kaldırım şeridi + bordür. Su: taş rıhtım. Çit: komşu
        /// çite kol. Kademe farkı olan kenara toprak yamaç ya da taş istinat duvarı; sahnenin dış kenarına toprak etek.
        /// </summary>
        static void ZeminYerlesim(SahneTasarimi sh, List<Yer> yerler)
        {
            int n = Mathf.Clamp(sh.n, 1, 64);
            int tohum = sh.tohum;
            var zt = new string[n, n];
            var kad = new int[n, n];
            for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) zt[x, y] = "cim";
            if (sh.zemin != null)
                foreach (var h in sh.zemin)
                {
                    if (h.x < 0 || h.y < 0 || h.x >= n || h.y >= n) continue;
                    zt[h.x, h.y] = Bos(h.t) ? "cim" : h.t;
                    kad[h.x, h.y] = Mathf.Clamp(h.k, 0, 2);
                }
            var sert = new HashSet<string>(Kut.sertZemin ?? new[] { "yol", "kaldirim", "meydan", "su" });
            float KADEME = Kut.kademe, ETEK = Kut.etek;

            string Tur(int x, int y) => x < 0 || y < 0 || x >= n || y >= n ? null : zt[x, y];
            void Koy(string ad, int x, int y, float z, int don = 0, float zs = 1f)
            {
                if (!parca.ContainsKey(ad)) return;
                Vector2 orta = SahneMerkezi(n, x, y);
                yerler.Add(new Yer { ad = ad, cx = orta.x, cy = orta.y, z = z, don = Mod(don, 4), zs = zs, kay = Vector2.zero, r = SahneRenk, olcek = 1f, aci = 0f, grup = "Govde", cam = 0 });
            }

            var yol = new bool[4];
            var su = new bool[4];
            for (int x = 0; x < n; x++)
                for (int y = 0; y < n; y++)
                {
                    string t = zt[x, y];
                    int k = kad[x, y];
                    float z = k * KADEME;
                    if (t == "yol")
                    {
                        Koy("z_yol", x, y, z);
                        int c = 0;
                        for (int i = 0; i < 4; i++) { yol[i] = Tur(x + Yon[i].x, y + Yon[i].y) == "yol"; if (yol[i]) c++; }
                        bool yatay = yol[1] && yol[3], dikey = yol[0] && yol[2];
                        int eksen = -1;                                  // 0: y ekseni boyunca (ön–arka), 1: x ekseni boyunca
                        if (c == 1) eksen = (yol[0] || yol[2]) ? 0 : 1;
                        else if (yatay != dikey) eksen = yatay ? 1 : 0;
                        if (eksen >= 0)
                        {
                            bool kavsak = false;                         // yol boyunca komşu hücre kavşak içi mi
                            for (int j = 0; j < 2; j++)
                            {
                                int i = eksen == 1 ? (j == 0 ? 1 : 3) : (j == 0 ? 0 : 2);
                                if (!yol[i]) continue;
                                int qx = x + Yon[i].x, qy = y + Yon[i].y;
                                if (Tur(qx + Yon[0].x, qy + Yon[0].y) == "yol" && Tur(qx + Yon[2].x, qy + Yon[2].y) == "yol" &&
                                    Tur(qx + Yon[1].x, qy + Yon[1].y) == "yol" && Tur(qx + Yon[3].x, qy + Yon[3].y) == "yol") kavsak = true;
                            }
                            bool yan0 = eksen == 1 ? yol[0] : yol[3], yan1 = eksen == 1 ? yol[2] : yol[1];
                            if (kavsak) Koy("z_gecit", x, y, z, eksen);
                            else if (!yan0 && !yan1) Koy("z_serit", x, y, z, eksen);                 // tek şeritli yol
                            else if (yan1 && !yan0) Koy("z_serit_kenar", x, y, z, eksen == 1 ? 0 : 3);   // iki şerit arası çizgi (bir kez)
                            if (!kavsak && Karma(x, y, 3, tohum) % 9 == 0) Koy("z_rogar", x, y, z, eksen);
                        }
                        for (int i = 0; i < 4; i++) if (!yol[i]) Koy("z_bordur", x, y, z, i);
                        for (int i = 0; i < 4; i++)
                        {
                            var a = Yon[(i + 3) % 4];
                            var b = Yon[i];
                            bool ya = yol[(i + 3) % 4], yb = yol[i];
                            if (!ya && !yb) Koy("z_bordur_kose", x, y, z, i);
                            else if (ya && !yb) Koy("z_bordur_uc", x, y, z, i);
                            else if (yb && !ya) Koy("z_bordur_son", x, y, z, (i + 3) % 4);
                            else if (Tur(x + a.x + b.x, y + a.y + b.y) != "yol") Koy("z_bordur_kose", x, y, z, i);
                        }
                    }
                    else if (t == "kaldirim") Koy("z_kaldirim", x, y, z);
                    else if (t == "meydan") Koy("z_meydan_" + Sec(Karma(x, y, 5, tohum), 2), x, y, z);
                    else if (t == "patika") Koy("z_patika", x, y, z, Sec(Karma(x, y, 6, tohum), 4));
                    else if (t == "su")
                    {
                        Koy("z_su", x, y, z, Sec(Karma(x, y, 7, tohum), 4));
                        for (int i = 0; i < 4; i++) su[i] = Tur(x + Yon[i].x, y + Yon[i].y) == "su";
                        for (int i = 0; i < 4; i++) if (!su[i]) Koy("z_su_kenar", x, y, z, i);
                        for (int i = 0; i < 4; i++)
                        {
                            var a = Yon[(i + 3) % 4];
                            var b = Yon[i];
                            if (!(su[(i + 3) % 4] && su[i] && Tur(x + a.x + b.x, y + a.y + b.y) == "su")) Koy("z_su_kose", x, y, z, i);
                        }
                    }
                    else if (t == "tarh") Koy("z_tarh_" + Sec(Karma(x, y, 8, tohum), 2), x, y, z);
                    else if (t == "cit")
                    {
                        Koy("z_cim_0", x, y, z);
                        Koy("z_cit_orta", x, y, z);
                        for (int i = 0; i < 4; i++) if (Tur(x + Yon[i].x, y + Yon[i].y) == "cit") Koy("z_cit_kol", x, y, z, i);
                    }
                    else Koy("z_cim_" + Sec(Karma(x, y, 1, tohum), 3), x, y, z, Sec(Karma(x, y, 2, tohum), 4));
                    // yükselti kenarları ve sahnenin dış eteği
                    for (int i = 0; i < 4; i++)
                    {
                        int qx = x + Yon[i].x, qy = y + Yon[i].y;
                        if (qx < 0 || qy < 0 || qx >= n || qy >= n) Koy("z_etek", x, y, z, i, z + ETEK);
                        else if (kad[qx, qy] < k) Koy(sert.Contains(t) ? "z_istinat" : "z_yamac", x, y, z, i, (k - kad[qx, qy]) * KADEME);
                    }
                }
        }

        /// <summary>Sahnenin zemini + serbest süsleri tek mesh (binalar ayrı kurulur: AtolyeUretici.SahneKur). Kütüphane yoksa null.</summary>
        public static Mesh ZeminKur(SahneTasarimi sh, out List<string> malzemeAdlari)
        {
            malzemeAdlari = new List<string>();
            if (sh == null || Kut == null) return null;
            var gecici = ScriptableObject.CreateInstance<BinaTasarimi>();
            gecici.hideFlags = HideFlags.HideAndDontSave;
            gecici.ad = "sahne";
            gecici.tohum = sh.tohum;
            gecici.tur = "konut";
            if (sh.susler != null) gecici.susler.AddRange(sh.susler);
            var s = Yerlesim(gecici);
            UnityEngine.Object.DestroyImmediate(gecici);
            ZeminYerlesim(sh, s.yerler);
            var gruplar = Topla(s, false, g => "");
            if (!gruplar.TryGetValue("", out var y)) return null;
            return MeshYap(y, TemizAd(sh.ad) + "_zemin", 0f, out malzemeAdlari);
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
                case "a": hex = r.a ?? r.Y; break;
                case "m": hex = r.m ?? r.y; break;
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

        class Kenar
        {
            public Vector3 a, b;
            public Yuzey sahip;      // çizginin yazılacağı grup: kenarı ilk gören yüzün grubu
            public List<(Vector3 n, Vector3 ic, Color32 renk)> yuzler = new List<(Vector3, Vector3, Color32)>(4);
        }

        static long Nicem(Vector3 p)
        {
            long x = Mathf.RoundToInt(p.x * 200f) + (1 << 20), y = Mathf.RoundToInt(p.y * 200f) + (1 << 20), z = Mathf.RoundToInt(p.z * 200f) + (1 << 20);
            return (x << 42) | (y << 21) | z;
        }

        /// <summary>Bir grubun (prefabdaki bir çocuğun) birikmiş geometrisi: yüzeyler malzemeye göre, kalem çizgileri ayrı.</summary>
        class Yuzey
        {
            public List<Vector3> kose = new List<Vector3>();
            public Dictionary<string, List<int>> alt = new Dictionary<string, List<int>>();
            // kalem çizgileri alt-mesh'i: köşe, öbür uç (uv1), yan / uç (uv0), renk
            public List<Vector3> cKose = new List<Vector3>(), cDiger = new List<Vector3>();
            public List<Vector2> cUv = new List<Vector2>();
            public List<Color32> cRenk = new List<Color32>();
            public List<int> cUc = new List<int>();

            public List<int> Liste(string mat)
            {
                if (!alt.TryGetValue(mat, out var l)) { alt[mat] = l = new List<int>(); }
                return l;
            }
        }

        /// <summary>Gruplu kurulumun sonucu: grup adı → mesh ve alt-mesh malzeme adları; iç yerleşim planı.</summary>
        public class Kurulum
        {
            public Dictionary<string, Mesh> mesh = new Dictionary<string, Mesh>();
            public Dictionary<string, List<string>> malzeme = new Dictionary<string, List<string>>();
            public IcPlani plan;
            public TurVeri tur;
        }

        /// <summary>Kepenk mesh'inin kökü bu yükseklikte (model metresi): yukarı doğru sarılır.</summary>
        public const float KepenkKok = 2.3f;

        /// <summary>
        /// Tasarımı tek mesh'e kurar. malzemeAdlari: alt-mesh sırasıyla malzeme adları (sonuncusu kalem çizgileri).
        /// hafifMi: uzak görünüm (ayrıntısız parçalar, yalnız uzun çizgiler; iç mekân yok). Kütüphane yoksa ya da tasarım boşsa null.
        /// icGoster: atölye önizlemesi için çatı / üst katlar ve kepenk atılır, iç mekân eklenir.
        /// </summary>
        public static Mesh Kur(BinaTasarimi t, bool hafifMi, out List<string> malzemeAdlari, bool icGoster = false, bool uzakMi = false)
        {
            malzemeAdlari = new List<string>();
            if (t == null || Kut == null) return null;
            if ((t.bloklar == null || t.bloklar.Count == 0) && (t.susler == null || t.susler.Count == 0)) return null;
            var s = Yerlesim(t);
            Func<string, string> sec;
            if (icGoster && !hafifMi) sec = g => (g == "Cati" || g == "Kepenk") ? null : "";
            else sec = g => GizliGrup(g) ? null : "";
            // uzakMi: en uzak görünüm (LOD2; hafifMi ile birlikte verilir): kütüphanede "uzak" hâli olan parçalar onunla kurulur
            var gruplar = Topla(s, hafifMi, sec, uzakMi);
            if (!gruplar.TryGetValue("", out var y)) return null;
            return MeshYap(y, t.ad + (uzakMi ? "_Uzak" : hafifMi ? "_Hafif" : ""), 0f, out malzemeAdlari);
        }

        /// <summary>
        /// Tasarımı prefabın çocuklarına göre ayrı mesh'lere kurar (yakın görünüm): Govde, Cati, Duvar_On/Sag/Arka/Sol, Ic,
        /// Ampul, Poset, Kepenk. Kalem çizgileri bütün bina üzerinden hesaplanır (gruplar arası ek yerleri çizilmez).
        /// </summary>
        public static Kurulum KurGruplu(BinaTasarimi t)
        {
            if (t == null || Kut == null) return null;
            if ((t.bloklar == null || t.bloklar.Count == 0) && (t.susler == null || t.susler.Count == 0)) return null;
            var s = Yerlesim(t);
            var gruplar = Topla(s, false, g => g);
            var k = new Kurulum { plan = s.plan, tur = s.T };
            foreach (var kv in gruplar)
            {
                var m = MeshYap(kv.Value, t.ad + "_" + kv.Key, kv.Key == "Kepenk" ? KepenkKok : 0f, out var adlar);
                if (m == null) continue;
                k.mesh[kv.Key] = m;
                k.malzeme[kv.Key] = adlar;
            }
            return k;
        }

        /// <summary>Yerleşimi gruplara toplar. sec: yerleşim grubu → çıktı grubu (null: atla).</summary>
        /// <summary>Tasarım tek bir ağaç / çalı süsü mü ve kütüphanede en uzak görünümü (LOD2) var mı?</summary>
        public static bool UzakVar(BinaTasarimi t)
        {
            if (t == null || Kut == null || uzak == null || t.susler == null || t.susler.Count != 1) return false;
            if (t.bloklar != null && t.bloklar.Count > 0) return false;
            return !Bos(t.susler[0].ad) && uzak.TryGetValue(t.susler[0].ad, out var u) && u.v != null && u.v.Length > 0;
        }

        static Dictionary<string, Yuzey> Topla(Durum s, bool hafifMi, Func<string, string> sec, bool uzakMi = false)
        {
            float A = Hucre * 0.5f;
            var gruplar = new Dictionary<string, Yuzey>();
            Yuzey Grup(string ad)
            {
                if (!gruplar.TryGetValue(ad, out var g)) gruplar[ad] = g = new Yuzey();
                return g;
            }

            var gecici = new List<Vector3>();
            foreach (var y in s.yerler)
            {
                if (!parca.TryGetValue(y.ad, out var asil)) continue;
                string cikis = sec(y.grup ?? "Govde");
                if (cikis == null) continue;
                ParcaVeri pv = asil;
                if (hafifMi)
                {
                    bool atla = asil.grup == "ayrinti" || asil.grup == "harf" || asil.grup == "simge" || (asil.grup == "cevre" && asil.kucuk);
                    if (atla && y.ad != "tas_merdiven" && y.ad != "baca") continue;
                    if (hafif.TryGetValue(y.ad, out var hv) && hv.v != null && hv.v.Length > 0) pv = hv;
                    if (uzakMi && uzak.TryGetValue(y.ad, out var uv) && uv.v != null && uv.v.Length > 0) pv = uv;
                }
                var yz = Grup(cikis);
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
                int bas = yz.kose.Count;
                yz.kose.AddRange(gecici);
                foreach (var a in pv.alt)
                {
                    string mat = Renkli(a.malzeme, y.r);
                    // pencere camları üç ışık grubuna dağılır (GeceIsiklari her grubu ayrı saatte yakar / söndürür)
                    if (y.cam != 0 && mat.StartsWith("Pastel_Cam_")) mat = "Pastel_Cam" + (y.cam + 1) + "_" + mat.Substring(11);
                    if (mat.StartsWith("Pastel_Kabuk_"))
                    {
                        // ters kabuk: çizgi alt-mesh'ine, açılmayan (uv.x = 0) üçgenler olarak
                        if (hafifMi) continue;
                        Color32 kr = AddanRenk(mat);
                        // yaprak döken tacın kabuğu: uv.y = 2 işaretiyle yazılır, kışın PastelCizgi gizler (_PastelKis)
                        var kUv = new Vector2(0f, mat.StartsWith("Pastel_Kabuk_Dok_") ? 2f : 0f);
                        for (int i = 0; i + 2 < a.uc.Length; i += 3)
                        {
                            int cb = yz.cKose.Count;
                            for (int q = 0; q < 3; q++)
                            {
                                Vector3 p = gecici[a.uc[i + q]];
                                yz.cKose.Add(p); yz.cDiger.Add(p); yz.cUv.Add(kUv); yz.cRenk.Add(kr);
                            }
                            yz.cUc.Add(cb); yz.cUc.Add(cb + 2); yz.cUc.Add(cb + 1);
                        }
                        continue;
                    }
                    var l = yz.Liste(mat);
                    // eksen çevirisi yönü ters çevirir: üçgen sırası da ters
                    for (int i = 0; i + 2 < a.uc.Length; i += 3) { l.Add(bas + a.uc[i]); l.Add(bas + a.uc[i + 2]); l.Add(bas + a.uc[i + 1]); }
                }
            }
            foreach (var c in s.cokgenler)
            {
                string cikis = sec(c.grup ?? "Cati");
                if (cikis == null) continue;
                var yz = Grup(cikis);
                var p = c.p;
                Vector3 nn = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
                bool ters = Vector3.Dot(nn, c.n) < 0f;
                int bas = yz.kose.Count;
                for (int i = 0; i < p.Length; i++)
                {
                    var q = ters ? p[p.Length - 1 - i] : p[i];
                    yz.kose.Add(Unity(q.x, q.y, q.z));
                }
                var l = yz.Liste(c.mat);
                for (int i = 1; i + 1 < p.Length; i++) { l.Add(bas); l.Add(bas + i + 1); l.Add(bas + i); }
            }
            foreach (var k in s.kutular)
            {
                string cikis = sec(k.grup ?? "Cati");
                if (cikis == null) continue;
                var yz = Grup(cikis);
                KutuEkle(yz.kose, yz.Liste(k.mat), k);
            }

            // ---- kalem çizgileri: sert ve açık kenarlar (pastel_parcalar.py: kenarlar), bütün gruplar birlikte
            var grupAdlari = new List<string>(gruplar.Keys);
            grupAdlari.Sort(string.CompareOrdinal);
            var kenarlar = new Dictionary<(long, long), Kenar>();
            foreach (string ga in grupAdlari)
            {
                var yz = gruplar[ga];
                var adlar = new List<string>(yz.alt.Keys);
                adlar.Sort(string.CompareOrdinal);
                foreach (string mat in adlar)
                {
                    if (mat.Contains("_Yazi_") || mat.Contains("_Iz_")) continue;       // yazı ve "iz" yüzeyleri kontur almaz
                    Color32 renk = CizgiRengi(AddanRenk(mat));
                    var l = yz.alt[mat];
                    for (int i = 0; i + 2 < l.Count; i += 3)
                    {
                        Vector3 p0 = yz.kose[l[i]], p1 = yz.kose[l[i + 1]], p2 = yz.kose[l[i + 2]];
                        Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
                        if (n.sqrMagnitude < 1e-12f) continue;
                        n.Normalize();
                        KenarEkle(kenarlar, yz, p0, p1, p2, n, renk);
                        KenarEkle(kenarlar, yz, p1, p2, p0, n, renk);
                        KenarEkle(kenarlar, yz, p2, p0, p1, n, renk);
                    }
                }
            }
            float enKisa = hafifMi ? 0.9f : EnKisa;
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
                var yz = ke.sahip;
                int cb = yz.cKose.Count;
                Color32 renk = L[0].renk;
                // kademeli kalınlık: uv.x'in büyüklüğü şeridin yarı genişliğini ölçekler (PastelCizgi: yana açma = uv.x × kalınlık)
                float kal = hafifMi ? 1f : (ke.a - ke.b).sqrMagnitude >= KalinBoy * KalinBoy ? Kalin : Ince;
                yz.cKose.Add(ke.a); yz.cDiger.Add(ke.b); yz.cUv.Add(new Vector2(-kal, 1f)); yz.cRenk.Add(renk);
                yz.cKose.Add(ke.a); yz.cDiger.Add(ke.b); yz.cUv.Add(new Vector2(kal, 1f)); yz.cRenk.Add(renk);
                yz.cKose.Add(ke.b); yz.cDiger.Add(ke.a); yz.cUv.Add(new Vector2(-kal, -1f)); yz.cRenk.Add(renk);
                yz.cKose.Add(ke.b); yz.cDiger.Add(ke.a); yz.cUv.Add(new Vector2(kal, -1f)); yz.cRenk.Add(renk);
                yz.cUc.Add(cb); yz.cUc.Add(cb + 1); yz.cUc.Add(cb + 2); yz.cUc.Add(cb + 2); yz.cUc.Add(cb + 1); yz.cUc.Add(cb + 3);
            }
            return gruplar;
        }

        /// <summary>Grubun mesh'i: önce yüzey köşeleri (beyaz köşe rengi), sonra çizgi köşeleri. yKaydir: kök bu yüksekliğe taşınır.</summary>
        static Mesh MeshYap(Yuzey yz, string ad, float yKaydir, out List<string> malzemeAdlari)
        {
            malzemeAdlari = new List<string>();
            if (yz.kose.Count == 0 && yz.cKose.Count == 0) return null;
            var adlar = new List<string>(yz.alt.Keys);
            adlar.Sort(string.CompareOrdinal);
            var kaydir = new Vector3(0f, yKaydir, 0f);
            int yuzeySay = yz.kose.Count;
            int toplam = yuzeySay + yz.cKose.Count;
            var kose = new List<Vector3>(toplam);
            var renkler = new List<Color32>(toplam);
            var uv0 = new List<Vector2>(toplam);
            var uv1 = new List<Vector3>(toplam);
            var beyaz = new Color32(255, 255, 255, 255);
            for (int i = 0; i < yuzeySay; i++) { Vector3 p = yz.kose[i] - kaydir; kose.Add(p); renkler.Add(beyaz); uv0.Add(Vector2.zero); uv1.Add(p); }
            for (int i = 0; i < yz.cKose.Count; i++) { kose.Add(yz.cKose[i] - kaydir); renkler.Add(yz.cRenk[i]); uv0.Add(yz.cUv[i]); uv1.Add(yz.cDiger[i] - kaydir); }
            var cUc = new List<int>(yz.cUc.Count);
            for (int i = 0; i < yz.cUc.Count; i++) cUc.Add(yz.cUc[i] + yuzeySay);

            var mesh = new Mesh { name = ad };
            if (kose.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(kose);
            mesh.SetColors(renkler);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            bool cizgiVar = cUc.Count > 0;
            mesh.subMeshCount = adlar.Count + (cizgiVar ? 1 : 0);
            for (int i = 0; i < adlar.Count; i++) mesh.SetTriangles(yz.alt[adlar[i]], i);
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

        static void KenarEkle(Dictionary<(long, long), Kenar> kenarlar, Yuzey sahip, Vector3 a, Vector3 b, Vector3 ucuncu, Vector3 n, Color32 renk)
        {
            long ka = Nicem(a), kb = Nicem(b);
            if (ka == kb) return;
            var anahtar = ka < kb ? (ka, kb) : (kb, ka);
            if (!kenarlar.TryGetValue(anahtar, out var ke)) kenarlar[anahtar] = ke = new Kenar { a = a, b = b, sahip = sahip };
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

        /// <summary>Oyun başlarken (alan yeniden yüklenmese de) önbellekler boşalır: yok edilmiş malzemeler kullanılmasın.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Sifirla()
        {
            malzemeOnbellek.Clear();
            Shader.SetGlobalFloat("_PastelKis", 0f);       // kış önizlemesi / önceki oyundan kalan kış: yapraklar geri gelir
        }

        /// <summary>
        /// "Pastel_&lt;etiket&gt;_&lt;rrggbb&gt;" adlı malzeme: ilk istekte kurulur, sonra önbellekten gelir (dosyası yoktur).
        /// Pastel gölgelendiricisi, renk addan; "Pastel_Cizgi" → kalem çizgisi gölgelendiricisi.
        /// </summary>
        public static Material Malzeme(string ad)
        {
            if (malzemeOnbellek.TryGetValue(ad, out var m) && m != null) return m;
            bool cizgi = ad.StartsWith("Pastel_Cizgi");
            var shader = Shader.Find(cizgi ? "KuryeTycoon/PastelCizgi" : "KuryeTycoon/Pastel");
            if (shader == null) shader = Shader.Find(cizgi ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader) { name = ad };
            // Editörde oyun çalışmıyorken (atölye önizlemesi) sahneye kaydedilmesin
            if (!Application.isPlaying) m.hideFlags = HideFlags.HideAndDontSave;
            m.enableInstancing = true;
            if (cizgi)
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                malzemeOnbellek[ad] = m;
                return m;
            }
            Color renk = AddanRenk(ad);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", renk);
            if (m.HasProperty("_Color")) m.SetColor("_Color", renk);
            // Görünüm (Pastel.shader varsayılanları)
            Ayar(m, "_GolgeRengi", new Color(0.80f, 0.76f, 0.86f, 1f));
            Ayar(m, "_KagitOlcek", 26f); Ayar(m, "_LekeOlcek", 0.9f); Ayar(m, "_TaramaSiklik", 14f);
            Ayar(m, "_TaramaGuc", 0.16f); Ayar(m, "_TaramaKalinlik", 0.16f); Ayar(m, "_IsiktaTarama", 0.035f);
            bool cam = ad.StartsWith("Pastel_Cam") || ad.StartsWith("Pastel_Vitrin");
            Ayar(m, "_Cam", cam ? 1f : 0f);
            // Mevsim kırpması (Pastel.shader: _Dokum): yaprak döken ağaç yaprakları kışın çizilmez, dal karı yalnız kışın çizilir
            Ayar(m, "_Dokum", ad.StartsWith("Pastel_Yaprak_Dok_") ? 1f : ad.StartsWith("Pastel_DalKar_") ? 2f : 0f);
            // Gece ışık verenler (GeceIsiklari "Pastel_Cam…", "Pastel_Vitrin_", "Pastel_Isik_", "Pastel_Yazi_" adlarını tanır): gündüz ışıma siyah
            if ((cam || ad.StartsWith("Pastel_Isik") || ad.StartsWith("Pastel_Yazi")) && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
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
    }
}
