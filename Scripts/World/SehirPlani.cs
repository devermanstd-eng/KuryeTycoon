using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Organik şehir planı (Resources/Sehir/plan.json; Tools/SehirPlani/plan_uret.py bir harita görüntüsünden üretir).
    ///  • Yollar gerçek çizgiler (eğri, çapraz) olarak gelir; oyun mantığı için kare ızgaraya da işlenmiştir (4 bağlı: kare kare yürünür).
    ///  • Her kare: yol, köprü, su, park ya da yapı arsası; bölge türü (eski şehir, kule, ev, çarşı, kampüs, park …); semt (1–5).
    ///  • Açık/kapalı: PlayerPrefs "KuryeTycoon.OrganikSehir" (editörde Kurye Tycoon > Organik Şehir menüsü). Kapalıyken eski ızgara şehir.
    /// Koordinat: 1 kare = 1 dünya birimi; x doğuya, y kuzeye artar.
    /// </summary>
    public static class SehirPlani
    {
        public const string Anahtar = "KuryeTycoon.OrganikSehir";

        [System.Serializable]
        public class Cizgi
        {
            public bool genis;
            public float[] x;
            public float[] y;
            public int Sayi => x != null ? x.Length : 0;
            public Vector2 this[int i] => new Vector2(x[i], y[i]);
        }

        [System.Serializable]
        class Veri
        {
            public int surum;
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
        }

        static bool yuklendi;
        static Veri v;
        static char[,] kare, zone;
        static byte[,] bolge, yogun;
        static int[,] yolNo;              // her yol karesini çizen çizginin numarası (sokak adı için)
        static Vector2[,] yolYonu;        // her karede en yakın yolun yönü (binalar yola dönük dursun)
        static float[,] yolUzak;          // en yakın yol çizgisine uzaklık
        static float[,] yumusak;          // yumuşatılmış yapı yoğunluğu (0 çim … 1 taş): zemin rengi kare kare değişmesin

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
        public static IReadOnlyList<Cizgi> Yollar => v?.yollar ?? new Cizgi[0];
        public static IReadOnlyList<Cizgi> Su => v?.su ?? new Cizgi[0];
        public static IReadOnlyList<Cizgi> Park => v?.park ?? new Cizgi[0];

        static Vector2Int V2(int[] a) => a != null && a.Length >= 2 ? new Vector2Int(a[0], a[1]) : Vector2Int.zero;

        static bool Icinde(int x, int y) => v != null && x >= 0 && y >= 0 && x < v.W && y < v.H;

        public static char Kare(int x, int y) => Icinde(x, y) ? kare[x, y] : ' ';
        public static bool Yol(int x, int y) { char c = Kare(x, y); return c == 'Y' || c == 'K'; }
        public static bool Kopru(int x, int y) => Kare(x, y) == 'K';
        public static bool SuVar(int x, int y) => Kare(x, y) == 'S';
        public static bool ParkVar(int x, int y) => Kare(x, y) == 'P';
        public static char ZoneHarf(int x, int y) => Icinde(x, y) ? zone[x, y] : '.';
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
            YolBilgisiKur();
            YumusakKur();
            Debug.Log($"Kurye Tycoon: organik şehir planı yüklendi ({w}×{h}, {v.yollar?.Length ?? 0} yol çizgisi).");
            return true;
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

        /// <summary>Yol çizgisi geniş (cadde) mi?</summary>
        public static bool Genis(int no) => v?.yollar != null && no >= 0 && no < v.yollar.Length && v.yollar[no].genis;

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
            float en = Mathf.Round(aci / 90f) * 90f;
            if (Mathf.Abs(Mathf.DeltaAngle(aci, en)) < 24f) aci = en % 360f;
            return aci;
        }
    }
}
