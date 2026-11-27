using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Organik şehir planı (Resources/Sehir/plan.json). Sürüm 2: Tools/SehirPlani/sehir_uret.py tohumlu, prosedürel bir
    /// "Londra tipi" şehir üretir (sürüm 1: plan_uret.py bir harita görüntüsünden çıkarırdı; o planlar da okunur).
    ///  • Yollar gerçek çizgiler (eğri, çapraz) olarak gelir; oyun mantığı için kare ızgaraya da işlenmiştir (4 bağlı: kare kare yürünür).
    ///    Sürüm 2'de her çizginin sınıfı (1 ana yol, 2 cadde, 3 sokak, 4 ara sokak) ve adı var: genişlik ve çizgiler sınıfa göre.
    ///  • Her kare: yol, köprü, su, park ya da yapı arsası; bölge türü (eski şehir, kule, ev, çarşı, kampüs, park …); semt (1–5).
    ///    Sürüm 2'de plan dikdörtgeninin içinde kır kareleri de var ('.' bölgesi): şehir lekesi düzensiz, kenarı kıra karışır.
    ///  • Sürüm 2 kırsalı da verir (KirPlanli): şehir dışı yollar, nehrin kolları, tarla çokgenleri, çiftlikler, köy ve yol boyu evler.
    ///  • u65 (büyük şehir, 144 × 134): demiryolu hattı (Demiryolu: çizgi; karelerde 'D' bölgesi, yol değil), mezarlık çokgenleri
    ///    (Mezarlik; karelerde 'G'), adı yazan yerler (Yerler: mahalle merkezleri, istasyonlar, parklar, okullar). Hepsi isteğe bağlı:
    ///    eski planlarda yoktur, boş liste döner.
    ///  • Açık/kapalı: PlayerPrefs "KuryeTycoon.OrganikSehir" (editörde Kurye Tycoon > Organik Şehir menüsü). Kapalıyken eski ızgara şehir.
    /// Koordinat: 1 kare = 1 dünya birimi; x doğuya, y kuzeye artar.
    /// </summary>
    public static class SehirPlani
    {
        public const string Anahtar = "KuryeTycoon.OrganikSehir";

        /// <summary>Yol sınıfları (plan sürüm 2). Eski planda yalnız `genis` bayrağı var: geniş = ana yol, değilse sokak.</summary>
        public const int Ana = 1, Cadde = 2, Sokak = 3, AraSokak = 4;

        [System.Serializable]
        public class Cizgi
        {
            public bool genis;
            public int sinif;             // 1 ana yol, 2 cadde, 3 sokak, 4 ara sokak (0: eski plan, `genis`e bakılır)
            public string ad;             // sokağın adı (boşsa numaradan türetilir)
            public float[] x;
            public float[] y;
            public float[] g;             // yalnız nehir kollarında: her noktadaki su genişliği (kare)
            public int Sayi => x != null && y != null ? Mathf.Min(x.Length, y.Length) : 0;
            public Vector2 this[int i] => new Vector2(x[i], y[i]);
            /// <summary>Geçerli sınıf (1–4).</summary>
            public int Sinif => sinif >= Ana && sinif <= AraSokak ? sinif : (genis ? Ana : Sokak);
        }

        /// <summary>Kır tarlası: dışbükey çokgen (saat yönünün tersi) ve içine sığan eğik dikdörtgen (3D parsel modeli buraya oturur).</summary>
        [System.Serializable]
        public class Tarla
        {
            public float[] x;
            public float[] y;
            public float mx, my;          // dikdörtgenin merkezi
            public float gen, boy;        // dikdörtgenin boyutu (kare): gen sıralar boyunca, boy sıralara dik
            public float aci;             // dikdörtgenin dönüşü (derece, +x'ten saat yönünün tersine)
            public int tur;               // renk / tür sırası (0–5)
            public int Sayi => x != null && y != null ? Mathf.Min(x.Length, y.Length) : 0;
        }

        [System.Serializable]
        public class Noktalar
        {
            public float[] x;
            public float[] y;
            public float[] aci;           // yalnız kır evlerinde: evin baktığı yön (City3D açı kuralı)
            public int[] tur;             // yalnız kır evlerinde: 0 yol boyu ev, 1 köy evi, 2 köy kilisesi
            public int Sayi => x != null && y != null ? Mathf.Min(x.Length, y.Length) : 0;
        }

        /// <summary>Adı yazan yer: tur 0 mahalle merkezi, 1 istasyon (aci: hattın yönü, derece, +x'ten saat yönünün tersine),
        /// 2 mezarlık, 3 park, 4 okul / kampüs.</summary>
        [System.Serializable]
        public class Yer
        {
            public string ad;
            public float x, y;
            public int tur;
            public float aci;
            public Vector2 Konum => new Vector2(x, y);
        }
        public const int YerMahalle = 0, YerIstasyon = 1, YerMezarlik = 2, YerPark = 3, YerOkul = 4;

        [System.Serializable]
        class Veri
        {
            public int surum;
            public int tohum;
            public int W;
            public int H;
            public int[] merkez;
            public int[] meydan;
            public int[] belediye;
            public int[] otogar;
            public Cizgi[] yollar;
            public Cizgi[] su;
            public Cizgi[] park;
            public string[] kare;
            public string[] zone;
            public string[] bolge;
            public string[] yogunluk;
            // Sürüm 2: kırsal (yoksa null / boş: kır eski usulle oyunda üretilir)
            public Cizgi[] kirYollari;
            public Cizgi[] kirNehir;
            public Tarla[] tarlalar;
            public Noktalar ciftlikler;
            public Noktalar kirEvleri;
            // u65: demiryolu, mezarlıklar, adı yazan yerler (yoksa null)
            public Cizgi[] demiryolu;
            public Cizgi[] mezarlik;
            public Yer[] yerler;
        }

        /// <summary>Yeni Play'de (alan yeniden yüklenmese de) plan yeniden okunur: plan.json yeniden üretilmiş olabilir.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OyunBasiSifirla() { yuklendi = false; v = null; aktifOkundu = false; }

        static bool yuklendi;
        static Veri v;
        static char[,] kare, zone;
        static byte[,] bolge, yogun;
        static int[,] yolNo;              // her yol karesini çizen çizginin numarası (sokak adı için)
        static Vector2[,] yolYonu;        // her karede en yakın yolun yönü (binalar yola dönük dursun)
        static float[,] yolUzak;          // en yakın yol çizgisine uzaklık
        static float[,] yumusak;          // yumuşatılmış yapı yoğunluğu (0 çim … 1 taş): zemin rengi kare kare değişmesin
        static float[,] kentUzak;         // her karenin en yakın kentsel (kır olmayan) kareye uzaklığı (kare)
        static bool[,] safKir;            // kare ve sekiz komşusu kır (yol ve su yok): şehir zemini burayı örtmez
        static readonly Cizgi[] BosCizgi = new Cizgi[0];
        static readonly Tarla[] BosTarla = new Tarla[0];
        static readonly Yer[] BosYer = new Yer[0];

        /// <summary>Organik şehir açık mı? (Plan dosyası varsa ve ayar açıksa.)</summary>
        public static bool Aktif
        {
            get
            {
                if (!aktifOkundu)
                {
                    aktifOkundu = true;
                    aktif = PlayerPrefs.GetInt(Anahtar, 0) == 1 && Yukle();
                }
                return aktif;
            }
        }
        static bool aktif, aktifOkundu;

        /// <summary>Ayarı değiştir (yeni oyun / yeniden yükleme sonrası geçerli olur).</summary>
        public static void Ayarla(bool acik)
        {
            PlayerPrefs.SetInt(Anahtar, acik ? 1 : 0);
            PlayerPrefs.Save();
            aktifOkundu = false;
        }

        public static int W => v != null ? v.W : 0;
        public static int H => v != null ? v.H : 0;
        public static Vector2Int Merkez => V2(v?.merkez);
        public static Vector2Int Meydan => V2(v?.meydan);
        public static Vector2Int Belediye => V2(v?.belediye);
        public static Vector2Int Otogar => V2(v?.otogar);
        public static IReadOnlyList<Cizgi> Yollar => v?.yollar ?? BosCizgi;
        public static IReadOnlyList<Cizgi> Su => v?.su ?? BosCizgi;
        public static IReadOnlyList<Cizgi> Park => v?.park ?? BosCizgi;

        /// <summary>Plan sürümü (1: görüntüden, 2: prosedürel) ve tohumu.</summary>
        public static int Surum => v != null ? v.surum : 0;
        public static int Tohum => v != null ? v.tohum : 0;
        /// <summary>Planın kimliği ("sürüm:tohum:G×Y"): kayıt dosyasına yazılır; farklı planla kaydedilmiş oyun güvenli açılır.</summary>
        public static string Kimlik => v != null ? $"{v.surum}:{v.tohum}:{v.W}x{v.H}" : "";

        /// <summary>Plan kırsalı da veriyor mu (tarlalar, kır yolları)? Öyleyse kır plandan kurulur, oyunda üretilmez.</summary>
        public static bool KirPlanli => v != null && ((v.tarlalar != null && v.tarlalar.Length > 0) || (v.kirYollari != null && v.kirYollari.Length > 0));
        public static IReadOnlyList<Cizgi> KirYollari => v?.kirYollari ?? BosCizgi;
        public static IReadOnlyList<Cizgi> KirNehir => v?.kirNehir ?? BosCizgi;
        public static IReadOnlyList<Tarla> Tarlalar => v?.tarlalar ?? BosTarla;
        public static Noktalar Ciftlikler => v?.ciftlikler;
        public static Noktalar KirEvleri => v?.kirEvleri;
        /// <summary>Demiryolu hatları (terminalden kıra uzanan çizgi). Yol değildir: kurye ve trafik gitmez; yalnız görsel ve yapısız şerit.</summary>
        public static IReadOnlyList<Cizgi> Demiryolu => v?.demiryolu ?? BosCizgi;
        /// <summary>Mezarlık çokgenleri (karelerde 'G' bölgesi).</summary>
        public static IReadOnlyList<Cizgi> Mezarlik => v?.mezarlik ?? BosCizgi;
        /// <summary>Adı yazan yerler: mahalle merkezleri, istasyonlar, mezarlıklar, parklar, okullar.</summary>
        public static IReadOnlyList<Yer> Yerler => v?.yerler ?? BosYer;
        /// <summary>Kare demiryolu şeridinde mi ('D': balast ve raylar; yapı ve çim yok)?</summary>
        public static bool RayKaresi(int x, int y) => Icinde(x, y) && zone[x, y] == 'D';
        /// <summary>Kare mezarlıkta mı ('G')?</summary>
        public static bool MezarlikKaresi(int x, int y) => Icinde(x, y) && zone[x, y] == 'G';

        static Vector2Int V2(int[] a) => a != null && a.Length >= 2 ? new Vector2Int(a[0], a[1]) : Vector2Int.zero;

        static bool Icinde(int x, int y) => v != null && x >= 0 && y >= 0 && x < v.W && y < v.H;

        public static char Kare(int x, int y) => Icinde(x, y) ? kare[x, y] : ' ';
        public static bool Yol(int x, int y) { char c = Kare(x, y); return c == 'Y' || c == 'K'; }
        public static bool Kopru(int x, int y) => Kare(x, y) == 'K';
        public static bool SuVar(int x, int y) => Kare(x, y) == 'S';
        public static bool ParkVar(int x, int y) => Kare(x, y) == 'P';
        public static char ZoneHarf(int x, int y) => Icinde(x, y) ? zone[x, y] : '.';
        /// <summary>Kır karesi mi (plan dikdörtgeninin içinde, şehir lekesinin dışında)? Dikdörtgenin dışı da kır.</summary>
        public static bool KirKaresi(int x, int y) => !Icinde(x, y) || zone[x, y] == '.';
        /// <summary>Kare ve sekiz komşusu yolsuz, susuz kır mı? Şehir zemini (OrganikZemin) burayı örtmez: kır zemini ve tarlalar görünür.</summary>
        public static bool SafKir(int x, int y) => !Icinde(x, y) || (safKir != null && safKir[x, y]);

        /// <summary>Noktanın en yakın kentsel kareye uzaklığı (kare; şehrin içinde 0). Dikdörtgenin dışında kenara uzaklık eklenir.</summary>
        public static float KentUzakligi(float x, float y)
        {
            if (v == null || kentUzak == null) return 0f;
            float cx = Mathf.Clamp(x, 0f, v.W - 1f), cy = Mathf.Clamp(y, 0f, v.H - 1f);
            float dis = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return kentUzak[Mathf.RoundToInt(cx), Mathf.RoundToInt(cy)] + dis;
        }
        public static int Bolge(int x, int y) => Icinde(x, y) ? bolge[x, y] : 1;
        public static int Yogunluk(int x, int y) => Icinde(x, y) ? yogun[x, y] : 0;
        public static int YolNo(int x, int y) => Icinde(x, y) ? yolNo[x, y] : -1;
        public static Vector2 YolYonu(int x, int y) => Icinde(x, y) ? yolYonu[x, y] : Vector2.right;
        public static float YolUzakligi(int x, int y) => Icinde(x, y) ? yolUzak[x, y] : 99f;
        /// <summary>Yumuşatılmış yapı yoğunluğu, 0 (bahçeli, yeşil) … 1 (sık, taş zemin).</summary>
        public static float Yumusak(int x, int y) => Icinde(x, y) ? yumusak[x, y] : 0f;

        /// <summary>Plan dosyasını okur (bir kez). Dosya yoksa false.</summary>
        public static bool Yukle()
        {
            if (yuklendi) return v != null;
            yuklendi = true;
            var ta = Resources.Load<TextAsset>("Sehir/plan");
            if (ta == null) { Debug.LogWarning("Kurye Tycoon: Resources/Sehir/plan.json yok; organik şehir kapalı."); return false; }
            v = JsonUtility.FromJson<Veri>(ta.text);
            if (v == null || v.W <= 0 || v.H <= 0 || v.kare == null || v.kare.Length != v.H) { v = null; return false; }
            int w = v.W, h = v.H;
            kare = new char[w, h];
            zone = new char[w, h];
            bolge = new byte[w, h];
            yogun = new byte[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    kare[x, y] = Harf(v.kare, x, y, '.');
                    zone[x, y] = Harf(v.zone, x, y, 'M');
                    bolge[x, y] = (byte)Mathf.Clamp(Harf(v.bolge, x, y, '1') - '0', 1, 5);
                    yogun[x, y] = (byte)Mathf.Clamp(Harf(v.yogunluk, x, y, '0') - '0', 0, 9);
                }
            if (v.yollar != null) foreach (var c in v.yollar) Sik(c, 0.35f);   // yollar tepeli arazide kırılmadan uzansın
            if (v.kirYollari != null) foreach (var c in v.kirYollari) Sik(c, 0.5f);
            if (v.demiryolu != null) foreach (var c in v.demiryolu) Sik(c, 0.35f);
            YolBilgisiKur();
            YumusakKur();
            KirBilgisiKur();
            Debug.Log($"Kurye Tycoon: organik şehir planı yüklendi (sürüm {v.surum}, tohum {v.tohum}, {w}×{h}, {v.yollar?.Length ?? 0} yol çizgisi" +
                      (KirPlanli ? $", kır: {v.kirYollari?.Length ?? 0} yol, {v.tarlalar?.Length ?? 0} tarla)." : ")."));
            return true;
        }

        /// <summary>Kır kareleri: saf kır maskesi ve kentsel karelere uzaklık (iki geçişli pah uzaklığı).</summary>
        static void KirBilgisiKur()
        {
            int w = v.W, h = v.H;
            safKir = new bool[w, h];
            kentUzak = new float[w, h];
            bool Kent(int x, int y) { char k = kare[x, y]; return zone[x, y] != '.' || k == 'Y' || k == 'K' || k == 'S'; }
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    kentUzak[x, y] = Kent(x, y) ? 0f : 1e6f;
                    bool saf = true;
                    for (int dx = -1; dx <= 1 && saf; dx++)
                        for (int dy = -1; dy <= 1 && saf; dy++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                            if (Kent(xx, yy)) saf = false;
                        }
                    safKir[x, y] = saf;
                }
            const float D = 1f, K = 1.4142f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = kentUzak[x, y];
                    if (x > 0) d = Mathf.Min(d, kentUzak[x - 1, y] + D);
                    if (y > 0) d = Mathf.Min(d, kentUzak[x, y - 1] + D);
                    if (x > 0 && y > 0) d = Mathf.Min(d, kentUzak[x - 1, y - 1] + K);
                    if (x < w - 1 && y > 0) d = Mathf.Min(d, kentUzak[x + 1, y - 1] + K);
                    kentUzak[x, y] = d;
                }
            for (int y = h - 1; y >= 0; y--)
                for (int x = w - 1; x >= 0; x--)
                {
                    float d = kentUzak[x, y];
                    if (x < w - 1) d = Mathf.Min(d, kentUzak[x + 1, y] + D);
                    if (y < h - 1) d = Mathf.Min(d, kentUzak[x, y + 1] + D);
                    if (x < w - 1 && y < h - 1) d = Mathf.Min(d, kentUzak[x + 1, y + 1] + K);
                    if (x > 0 && y < h - 1) d = Mathf.Min(d, kentUzak[x - 1, y + 1] + K);
                    kentUzak[x, y] = d;
                }
        }

        /// <summary>Çizginin noktalarını sıklaştırır (iki nokta arası en çok `adim` kare): yol şeridi arazinin yüksekliğini izler.</summary>
        static void Sik(Cizgi c, float adim)
        {
            if (c == null || c.Sayi < 2) return;
            var xs = new List<float>(c.Sayi * 2);
            var ys = new List<float>(c.Sayi * 2);
            for (int i = 0; i + 1 < c.Sayi; i++)
            {
                Vector2 a = c[i], b = c[i + 1];
                int n = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / adim));
                for (int k = 0; k < n; k++) { var p = Vector2.Lerp(a, b, k / (float)n); xs.Add(p.x); ys.Add(p.y); }
            }
            xs.Add(c.x[c.Sayi - 1]); ys.Add(c.y[c.Sayi - 1]);
            c.x = xs.ToArray();
            c.y = ys.ToArray();
        }

        static char Harf(string[] satirlar, int x, int y, char varsayilan)
        {
            if (satirlar == null || y >= satirlar.Length || satirlar[y] == null || x >= satirlar[y].Length) return varsayilan;
            return satirlar[y][x];
        }

        static void YumusakKur()
        {
            int w = v.W, h = v.H;
            yumusak = new float[w, h];
            const int R = 3;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    float top = 0f, ag = 0f;
                    for (int dx = -R; dx <= R; dx++)
                        for (int dy = -R; dy <= R; dy++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                            float a = 1f / (1f + dx * dx + dy * dy);
                            char z = zone[xx, yy];
                            float d = yogun[xx, yy] / 9f;
                            if (z == 'P' || z == 'E' || z == 'C') d *= 0.4f;      // bahçeli bölgeler yeşile çeker
                            top += d * a; ag += a;
                        }
                    yumusak[x, y] = Mathf.Clamp01(top / Mathf.Max(ag, 1e-3f) * 2.4f);
                }
        }

        /// <summary>Her kare için en yakın yol çizgisi: numarası, yönü ve uzaklığı.</summary>
        static void YolBilgisiKur()
        {
            int w = v.W, h = v.H;
            yolNo = new int[w, h];
            yolYonu = new Vector2[w, h];
            yolUzak = new float[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++) { yolNo[x, y] = -1; yolUzak[x, y] = 99f; yolYonu[x, y] = Vector2.right; }
            if (v.yollar == null) return;
            const float Menzil = 2.5f;
            for (int i = 0; i < v.yollar.Length; i++)
            {
                var c = v.yollar[i];
                if (c == null) continue;
                for (int k = 0; k + 1 < c.Sayi; k++)
                {
                    Vector2 a = c[k], b = c[k + 1];
                    var ab = b - a;
                    float L2 = ab.sqrMagnitude;
                    if (L2 < 1e-6f) continue;
                    var yon = ab.normalized;
                    int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - Menzil)), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + Menzil));
                    int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - Menzil)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + Menzil));
                    for (int x = x0; x <= x1; x++)
                        for (int y = y0; y <= y1; y++)
                        {
                            var p = new Vector2(x, y);
                            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / L2);
                            float d = Vector2.Distance(p, a + ab * t);
                            if (d >= yolUzak[x, y]) continue;
                            yolUzak[x, y] = d;
                            yolYonu[x, y] = yon;
                            yolNo[x, y] = i;
                        }
                }
            }
        }

        /// <summary>Yol çizgisi geniş (ana yol ya da cadde) mi?</summary>
        public static bool Genis(int no) => v?.yollar != null && no >= 0 && no < v.yollar.Length && v.yollar[no] != null && v.yollar[no].genis;

        /// <summary>Yol çizgisinin sınıfı (1 ana, 2 cadde, 3 sokak, 4 ara sokak); çizgi yoksa sokak.</summary>
        public static int Sinif(int no) => v?.yollar != null && no >= 0 && no < v.yollar.Length && v.yollar[no] != null ? v.yollar[no].Sinif : Sokak;

        /// <summary>Yol çizgisinin plandaki adı (yoksa null).</summary>
        public static string YolAdi(int no) =>
            v?.yollar != null && no >= 0 && no < v.yollar.Length && v.yollar[no] != null && !string.IsNullOrEmpty(v.yollar[no].ad) ? v.yollar[no].ad : null;

        /// <summary>
        /// Arsadaki binanın yola dönük açısı (derece, City3D'nin dönüş kuralıyla: 180 = güneye bakar).
        /// En yakın yol çizgisine dik, yola doğru.
        /// </summary>
        public static float YolaBakanAci(int x, int y)
        {
            if (!Icinde(x, y)) return 180f;
            var yon = yolYonu[x, y];
            int no = yolNo[x, y];
            Vector2 p = new Vector2(x, y), en = p;
            if (no >= 0)
            {
                // En yakın noktayı yeniden bul (yön vektörünün hangi yanına bakacağımızı bilmek için)
                var c = v.yollar[no];
                float best = float.MaxValue;
                for (int k = 0; k + 1 < c.Sayi; k++)
                {
                    Vector2 a = c[k], b = c[k + 1];
                    var ab = b - a;
                    float L2 = ab.sqrMagnitude;
                    if (L2 < 1e-6f) continue;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / L2);
                    var q = a + ab * t;
                    float d = (q - p).sqrMagnitude;
                    if (d < best) { best = d; en = q; }
                }
            }
            var bak = en - p;
            if (bak.sqrMagnitude < 1e-4f) bak = new Vector2(-yon.y, yon.x);
            // City3D: 0° kuzeye (+y), 90° batıya, 180° güneye, 270° doğuya bakar
            float aci = (Mathf.Atan2(-bak.x, bak.y) * Mathf.Rad2Deg + 360f) % 360f;
            // Eksene yakın açılar 90'ın katına yuvarlanır: döndürülmüş bina küçülmesin, komşularıyla hizalı dursun.
            // Gerçekten çapraz yollarda (45° civarı) bina yola dönük kalır.
            float kat = Mathf.Round(aci / 90f) * 90f;
            if (Mathf.Abs(Mathf.DeltaAngle(aci, kat)) < 24f) aci = kat % 360f;
            return aci;
        }
    }
}
