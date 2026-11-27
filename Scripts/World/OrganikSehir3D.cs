using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Organik şehrin 3D yol ağı ve nehri: plan çizgilerinden (SehirPlani) tek parça mesh.
    ///  • Yollar: kaldırım şeridi (geniş, açık taş) + asfalt (dar, koyu). Köşelerde yuvarlak birleşim. Genişlik ve çizgiler yolun
    ///    sınıfına göre (SehirPlani.Cizgi.Sinif): ana yol 8 m, dört şerit, çift sarı orta çizgi; cadde 6,2 m, kesik orta çizgi ve
    ///    kenar çizgisi; sokak 4,6 m, çizgisiz; ara sokak (mews, geçit) 3 m, arnavut kaldırımı, dar kaldırım, çizgisiz.
    ///  • Köprülerde iki yanda korkuluk duvarı. Plan kırsalı veriyorsa şehir dışı yollar da buradan (asfalt ana yol, çakıl köy yolu).
    ///  • Nehir: su konturları (kulak kesme üçgenleme), yumuşak kıyı.
    ///  • u65: demiryolu (çift hat: balast, traversler, raylar; yolu hemzemin geçer), terminal ve banliyö durağı (peronlar, gar
    ///    binası, peron çatısı), mezarlıklar (duvar, mezar taşı sıraları). Hepsi plan çizgilerinden basit mesh; model kullanılmaz.
    /// Dünyada yukarı −Z; zemin z = 0. Izgara şehirde kullanılmaz.
    /// </summary>
    public static class OrganikSehir3D
    {
        // Asfalt genişlikleri (kare = 10 m), sınıfa göre: ana yol iki yönde ikişer şerit (8 m), cadde iki geniş şerit (6,2 m),
        // sokak iki dar şerit (4,6 m), ara sokak tek şerit (3 m)
        public const float AnaAsfalt = 0.80f, CaddeAsfalt = 0.62f, SokakAsfalt = 0.46f, AraAsfalt = 0.30f;
        public const float Kaldirim = 0.10f, AraKaldirim = 0.05f;           // asfaltın iki yanında
        // Şehir dışı yollar: ana yol, ikincil yol, köy yolu (çakıl)
        public const float KirAnaAsfalt = 0.62f, KirCaddeAsfalt = 0.48f, KirYolu = 0.34f;

        /// <summary>Sınıfın asfalt genişliği (kare).</summary>
        public static float Asfalt(int sinif) =>
            sinif == SehirPlani.Ana ? AnaAsfalt : sinif == SehirPlani.Cadde ? CaddeAsfalt : sinif == SehirPlani.AraSokak ? AraAsfalt : SokakAsfalt;
        /// <summary>Sınıfın kaldırım genişliği (kare; asfaltın her iki yanında).</summary>
        public static float KaldirimGenisligi(int sinif) => sinif == SehirPlani.AraSokak ? AraKaldirim : Kaldirim;
        /// <summary>Şehir dışı yolun genişliği (kare).</summary>
        public static float KirAsfalt(int sinif) => sinif == SehirPlani.Ana ? KirAnaAsfalt : sinif == SehirPlani.Cadde ? KirCaddeAsfalt : KirYolu;
        // Su yüzeyi zeminin altında: nehir yatağı OrganikZemin'de çukurlaşır, kıyılar eğimli iner (köprüler suyun üstünde kalır)
        const float ZKaldirim = -0.003f, ZAsfalt = -0.006f, ZCizgi = -0.0075f, ZSu = -0.0015f, ZPark = -0.001f;

        static readonly Color KaldirimRenk = new Color(0.74f, 0.71f, 0.65f);
        static readonly Color AsfaltRenk = new Color(0.27f, 0.28f, 0.31f);
        static readonly Color CizgiRenk = new Color(0.92f, 0.90f, 0.82f);
        static readonly Color SariRenk = new Color(0.93f, 0.76f, 0.25f);
        static readonly Color BordurRenk = new Color(0.62f, 0.60f, 0.57f);
        static readonly Color SuRenk = new Color(0.38f, 0.63f, 0.84f);
        static readonly Color ParkRenk = new Color(0.47f, 0.66f, 0.36f);
        static readonly Color ArnavutRenk = new Color(0.43f, 0.41f, 0.39f);     // ara sokak: koyu arnavut kaldırımı
        static readonly Color CakilRenk = new Color(0.66f, 0.60f, 0.48f);       // köy yolu: çakıl / sıkışmış toprak
        static readonly Color YolKenariRenk = new Color(0.55f, 0.56f, 0.42f);   // kır yolunun banketi: kuru ot

        /// <summary>Yol ağını ve nehri kurar. ornek: şehrin bir malzemesi (aynı gölgelendirici kullanılsın: anime toon ya da URP Lit).</summary>
        public static List<GameObject> Kur(Transform parent, Material ornek)
        {
            ornekShader = ornek != null ? ornek.shader : null;
            var sonuc = new List<GameObject>();
            if (!SehirPlani.Aktif) return sonuc;

            var kal = new MeshKurucu();
            var bordur = new MeshKurucu();
            var asf = new MeshKurucu();
            var cizgi = new MeshKurucu();
            var sari = new MeshKurucu();
            var ara = new MeshKurucu();
            var korkuluk = new MeshKurucu();
            foreach (var c in SehirPlani.Yollar)
            {
                if (c == null || c.Sayi < 2) continue;
                int sinif = c.Sinif;
                float g = Asfalt(sinif), kd = KaldirimGenisligi(sinif);
                Serit(kal, c, g * 0.5f + kd, ZKaldirim);
                Serit(bordur, c, g * 0.5f + 0.012f, ZKaldirim - 0.0008f);
                // Ara sokağın taş döşemesi asfaltın biraz altında: kavşakta sokağın asfaltı üstte kalır (aynı düzlemde titreşmez)
                if (sinif == SehirPlani.AraSokak) Serit(ara, c, g * 0.5f, ZAsfalt + 0.0005f);
                else Serit(asf, c, g * 0.5f, ZAsfalt);
                KopruKorkulugu(korkuluk, c, g * 0.5f + kd);
            }
            // Çizgiler asfaltın üstünde, ayrı geçişte: kavşakta başka yolun asfaltı çizgiyi örtmesin
            foreach (var c in SehirPlani.Yollar)
            {
                if (c == null || c.Sayi < 2) continue;
                int sinif = c.Sinif;
                float g = Asfalt(sinif);
                if (sinif == SehirPlani.Ana)
                {
                    // Ana yol: ortada çift sarı çizgi, iki yanda şerit kesikleri, kenarda beyaz kenar çizgisi, uçlarda yaya geçidi
                    YanCizgi(sari, c, 0.016f, 0.006f, ZCizgi, false);
                    YanCizgi(sari, c, -0.016f, 0.006f, ZCizgi, false);
                    YanCizgi(cizgi, c, g * 0.25f, 0.007f, ZCizgi, true);
                    YanCizgi(cizgi, c, -g * 0.25f, 0.007f, ZCizgi, true);
                    YanCizgi(cizgi, c, g * 0.5f - 0.03f, 0.006f, ZCizgi, false);
                    YanCizgi(cizgi, c, -(g * 0.5f - 0.03f), 0.006f, ZCizgi, false);
                    YayaGecidi(cizgi, c, g, ZCizgi - 0.0002f);
                }
                else if (sinif == SehirPlani.Cadde)
                {
                    // Cadde: ortada kesik beyaz çizgi (çift yönlü iki şerit), ince kenar çizgisi, uçlarda yaya geçidi
                    YanCizgi(cizgi, c, 0f, 0.0075f, ZCizgi, true);
                    YanCizgi(cizgi, c, g * 0.5f - 0.025f, 0.004f, ZCizgi, false);
                    YanCizgi(cizgi, c, -(g * 0.5f - 0.025f), 0.004f, ZCizgi, false);
                    if (Uzunluk(c) > 2.2f) YayaGecidi(cizgi, c, g, ZCizgi - 0.0002f);
                }
                else if (sinif == SehirPlani.Sokak)
                {
                    // Sokak: çizgisiz (dar mahalle sokağı); yalnız uzun sokakların ucunda yaya geçidi
                    if (Uzunluk(c) > 4f) YayaGecidi(cizgi, c, g, ZCizgi - 0.0002f);
                }
                // Ara sokak: çizgi yok
            }
            // Şehir dışı yollar (plan kırsalı veriyorsa): banket + asfalt (ana / ikincil) ya da çakıl (köy yolu)
            var banket = new MeshKurucu();
            var cakil = new MeshKurucu();
            foreach (var c in SehirPlani.KirYollari)
            {
                if (c == null || c.Sayi < 2) continue;
                int sinif = c.Sinif;
                float g = KirAsfalt(sinif);
                Serit(banket, c, g * 0.5f + 0.06f, ZKaldirim);
                if (sinif == SehirPlani.Ana || sinif == SehirPlani.Cadde) Serit(asf, c, g * 0.5f, ZAsfalt);
                else Serit(cakil, c, g * 0.5f, ZAsfalt + 0.0005f);
            }
            foreach (var c in SehirPlani.KirYollari)
            {
                if (c == null || c.Sayi < 2) continue;
                if (c.Sinif == SehirPlani.Ana) YanCizgi(cizgi, c, 0f, 0.007f, ZCizgi, true);      // ana yol: kesik orta çizgi
            }
            sonuc.Add(Nesne("Kaldırımlar (organik)", parent, kal, Malzeme("OrganikKaldirim", KaldirimRenk)));
            sonuc.Add(Nesne("Bordürler (organik)", parent, bordur, Malzeme("OrganikBordur", BordurRenk)));
            sonuc.Add(Nesne("Yollar (organik)", parent, asf, Malzeme("OrganikAsfalt", AsfaltRenk)));
            if (cizgi.v.Count > 0) sonuc.Add(Nesne("Yol çizgileri (organik)", parent, cizgi, Malzeme("OrganikCizgi", CizgiRenk)));
            if (sari.v.Count > 0) sonuc.Add(Nesne("Yol çizgileri sarı (organik)", parent, sari, Malzeme("OrganikSariCizgi", SariRenk)));
            if (ara.v.Count > 0) sonuc.Add(Nesne("Ara sokaklar (organik)", parent, ara, Malzeme("OrganikArnavut", ArnavutRenk)));
            if (korkuluk.v.Count > 0) sonuc.Add(Nesne("Köprü korkulukları (organik)", parent, korkuluk, Malzeme("OrganikKopru", KopruRenk)));
            if (banket.v.Count > 0) sonuc.Add(Nesne("Kır yolu banketi (organik)", parent, banket, Malzeme("OrganikYolKenari", YolKenariRenk)));
            if (cakil.v.Count > 0) sonuc.Add(Nesne("Köy yolları (organik)", parent, cakil, Malzeme("OrganikCakil", CakilRenk)));

            // Parklar: plan konturlarından yumuşak kenarlı çim (kare park zemini kenarda basamak yapmasın)
            var park = new MeshKurucu();
            foreach (var c in SehirPlani.Park)
            {
                if (c.Sayi < 3) continue;
                var pts = new List<Vector2>(c.Sayi);
                for (int i = 0; i < c.Sayi; i++) pts.Add(c[i]);
                if ((pts[0] - pts[pts.Count - 1]).sqrMagnitude < 1e-4f) pts.RemoveAt(pts.Count - 1);
                Poligon(park, pts, ZPark);
            }
            if (park.v.Count > 0) sonuc.Add(Nesne("Parklar (organik)", parent, park, Malzeme("OrganikPark", ParkRenk)));

            DemiryoluKur(parent, sonuc);
            MezarlikKur(parent, sonuc);

            var su = new MeshKurucu();
            duz = true;                // su yüzeyi düz
            foreach (var c in SehirPlani.Su)
            {
                if (c.Sayi < 3) continue;
                var pts = new List<Vector2>(c.Sayi);
                for (int i = 0; i < c.Sayi; i++) pts.Add(c[i]);
                if ((pts[0] - pts[pts.Count - 1]).sqrMagnitude < 1e-4f) pts.RemoveAt(pts.Count - 1);
                Poligon(su, pts, ZSu);
            }
            if (su.v.Count > 0)
            {
                var suMat = SuMalzemesi();
                sonuc.Add(Nesne("Swan River (organik)", parent, su, suMat != null ? suMat : Malzeme("OrganikSu", SuRenk), golgeAl: false));
            }
            KirNehirKur(parent, sonuc);
            duz = false;
            IslakUygula();          // şehir yeniden kurulunca yağmurdaki ıslaklık kaybolmasın
            return sonuc;
        }

        // ------------------------------------------------------------------ demiryolu, istasyonlar, mezarlık (u65)

        static readonly Color BalastRenk = new Color(0.52f, 0.48f, 0.43f);
        static readonly Color RayRenk = new Color(0.27f, 0.27f, 0.30f);
        static readonly Color TraversRenk = new Color(0.36f, 0.27f, 0.20f);
        static readonly Color PeronRenk = new Color(0.74f, 0.72f, 0.68f);
        static readonly Color GarRenk = new Color(0.62f, 0.36f, 0.28f);          // tuğla
        static readonly Color GarCatiRenk = new Color(0.36f, 0.40f, 0.44f);      // arduvaz / cam çatı
        static readonly Color MezarTasiRenk = new Color(0.80f, 0.80f, 0.77f);
        // Balast kaldırımın ve bordürün üstünde, asfaltın altında: hemzemin geçitte asfalt balastı ve traversleri örter, raylar
        // asfaltın ve yol çizgilerinin üstünde kalır
        const float ZBalast = -0.0045f, ZTravers = -0.0052f, ZRay = -0.009f;
        const float HatAraligi = 0.16f;          // çift hattın eksenden uzaklığı (kare)
        const float RayAcikligi = 0.07f;         // hat ekseninden raya (kare; 1,4 m)

        /// <summary>
        /// Demiryolu: plan çizgisi boyunca çift hat (balast şeridi, ahşap traversler, çelik raylar) ve istasyonlar (plan Yerler,
        /// tur 1): hattın ilk noktasındaki istasyon terminaldir (gar binası, saat kulesi, peron çatısı, iki yan + orta peron),
        /// ötekiler banliyö durağı (iki yan peron, küçük sundurma). Yol değil: kurye ve trafik kullanmaz.
        /// </summary>
        static void DemiryoluKur(Transform parent, List<GameObject> sonuc)
        {
            var hatlar = SehirPlani.Demiryolu;
            if (hatlar == null || hatlar.Count == 0) return;
            var balast = new MeshKurucu();
            var travers = new MeshKurucu();
            var ray = new MeshKurucu();
            foreach (var c in hatlar)
            {
                if (c == null || c.Sayi < 2) continue;
                Serit(balast, c, HatAraligi + 0.2f, ZBalast);
                for (int h = -1; h <= 1; h += 2)
                {
                    float eksen = h * HatAraligi;
                    Traversler(travers, c, eksen, 0.11f, 0.022f, 0.15f, ZTravers);
                    YanCizgi(ray, c, eksen + RayAcikligi, 0.011f, ZRay, false);
                    YanCizgi(ray, c, eksen - RayAcikligi, 0.011f, ZRay, false);
                }
            }
            if (balast.v.Count == 0) return;
            sonuc.Add(Nesne("Demiryolu balastı (organik)", parent, balast, Malzeme("OrganikBalast", BalastRenk)));
            if (travers.v.Count > 0) sonuc.Add(Nesne("Demiryolu traversleri (organik)", parent, travers, Malzeme("OrganikTravers", TraversRenk)));
            if (ray.v.Count > 0) sonuc.Add(Nesne("Raylar (organik)", parent, ray, Malzeme("OrganikRay", RayRenk)));

            var peron = new MeshKurucu();
            var gar = new MeshKurucu();
            var cati = new MeshKurucu();
            foreach (var yer in SehirPlani.Yerler)
            {
                if (yer == null || yer.tur != SehirPlani.YerIstasyon) continue;
                var p = yer.Konum;
                var yon = new Vector2(Mathf.Cos(yer.aci * Mathf.Deg2Rad), Mathf.Sin(yer.aci * Mathf.Deg2Rad));
                var yan = new Vector2(-yon.y, yon.x);
                bool terminal = false;
                foreach (var c in hatlar)
                    if (c != null && c.Sayi > 0 && (c[0] - p).sqrMagnitude < 0.36f) terminal = true;
                if (terminal)
                {
                    // peronlar: iki yanda ve iki hattın arasında dar orta peron (hattın ilk 4,6 karesi)
                    var orta = p + yon * 2.45f;
                    KutuMesh(peron, peron, orta + yan * 0.62f, yon, 2.25f, 0.2f, 0f, 0.022f);
                    KutuMesh(peron, peron, orta - yan * 0.62f, yon, 2.25f, 0.2f, 0f, 0.022f);
                    // peron çatısı (tren hangarı): hattın başını örten yüksek çatı, altı açık; dört köşede direk
                    var hangar = p + yon * 1.45f;
                    KutuMesh(cati, cati, hangar, yon, 1.25f, 0.86f, 0.17f, 0.2f);
                    for (int a = -1; a <= 1; a += 2)
                        for (int b = -1; b <= 1; b += 2)
                            KutuMesh(gar, gar, hangar + yon * (1.2f * a) + yan * (0.82f * b), yon, 0.025f, 0.025f, 0f, 0.17f);
                    // gar binası: hattın başında, hatta dik uzun tuğla bina; ortasında saat kulesi
                    var bina = p - yon * 0.42f;
                    KutuMesh(cati, gar, bina, yon, 0.36f, 0.92f, 0f, 0.26f);
                    KutuMesh(cati, gar, bina, yon, 0.11f, 0.11f, 0f, 0.46f);
                    // tampon: hatların ucunda
                    KutuMesh(gar, gar, p + yon * 0.12f, yon, 0.03f, 0.32f, 0f, 0.03f);
                }
                else
                {
                    KutuMesh(peron, peron, p + yan * 0.6f, yon, 1.9f, 0.18f, 0f, 0.022f);
                    KutuMesh(peron, peron, p - yan * 0.6f, yon, 1.9f, 0.18f, 0f, 0.022f);
                    KutuMesh(cati, gar, p + yan * 0.66f, yon, 0.45f, 0.1f, 0.022f, 0.11f);       // sundurma
                    KutuMesh(cati, gar, p - yan * 0.66f, yon, 0.3f, 0.1f, 0.022f, 0.11f);
                }
            }
            if (peron.v.Count > 0) sonuc.Add(Nesne("Peronlar (organik)", parent, peron, Malzeme("OrganikPeron", PeronRenk)));
            if (gar.v.Count > 0) sonuc.Add(Nesne("İstasyon binaları (organik)", parent, gar, Malzeme("OrganikGar", GarRenk)));
            if (cati.v.Count > 0) sonuc.Add(Nesne("İstasyon çatıları (organik)", parent, cati, Malzeme("OrganikGarCati", GarCatiRenk)));
        }

        /// <summary>Çizginin `ofs` kadar yanında, `periyot` aralıklı, çizgiye dik kısa dörtgenler (traversler).</summary>
        static void Traversler(MeshKurucu m, SehirPlani.Cizgi c, float ofs, float yariBoy, float yariEn, float periyot, float z)
        {
            double s = 0.0;
            for (int k = 0; k + 1 < c.Sayi; k++)
            {
                Vector2 a = c[k], b = c[k + 1];
                float L = Vector2.Distance(a, b);
                if (L < 1e-5f) continue;
                var yon = (b - a) / L;
                var n = new Vector2(-yon.y, yon.x);
                long j0 = (long)System.Math.Ceiling(s / periyot);
                long j1 = (long)System.Math.Floor((s + L) / periyot);
                for (long j = j0; j <= j1; j++)
                {
                    float d = (float)(j * (double)periyot - s);
                    if (d < 0f || d >= L) continue;
                    var o = a + yon * d + n * ofs;
                    m.Dortgen(V(o + n * yariBoy - yon * yariEn, z), V(o + n * yariBoy + yon * yariEn, z),
                              V(o - n * yariBoy + yon * yariEn, z), V(o - n * yariBoy - yon * yariEn, z));
                }
                s += L;
            }
        }

        /// <summary>
        /// Zemine oturan (ya da `alt` kadar yukarıda duran) kutu: merkez, uzun eksenin yönü, yarı boy (yön boyunca), yarı en,
        /// altının ve üstünün zeminden yüksekliği (kare). Üst yüz `ust` mesh'ine, dört duvar (iki yüzlü) `duvar` mesh'ine eklenir.
        /// Taban merkezin arazi yüksekliğine göre düzdür; alt = 0 ise duvarlar yamaçta açık kalmasın diye biraz zemine gömülür.
        /// </summary>
        static void KutuMesh(MeshKurucu ust, MeshKurucu duvar, Vector2 merkez, Vector2 yon, float yariBoy, float yariEn, float alt, float ustY)
        {
            var n = new Vector2(-yon.y, yon.x);
            Vector2 p0 = merkez - yon * yariBoy - n * yariEn, p1 = merkez + yon * yariBoy - n * yariEn;
            Vector2 p2 = merkez + yon * yariBoy + n * yariEn, p3 = merkez - yon * yariBoy + n * yariEn;
            float zc = Arazi.Z(merkez.x, merkez.y);
            float zu = zc - ustY;                                  // −Z yukarı
            float za = alt > 0f ? zc - alt : zc + 0.02f;
            ust.Dortgen(new Vector3(p0.x, p0.y, zu), new Vector3(p1.x, p1.y, zu), new Vector3(p2.x, p2.y, zu), new Vector3(p3.x, p3.y, zu));
            if (alt > 0f)       // havada duran levhanın altı da görünsün
                ust.IkiYuz(new Vector3(p0.x, p0.y, za), new Vector3(p1.x, p1.y, za), new Vector3(p2.x, p2.y, za), new Vector3(p3.x, p3.y, za));
            var k = new[] { p0, p1, p2, p3 };
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = k[i], b = k[(i + 1) % 4];
                duvar.IkiYuz(new Vector3(a.x, a.y, za), new Vector3(b.x, b.y, za), new Vector3(b.x, b.y, zu), new Vector3(a.x, a.y, zu));
            }
        }

        /// <summary>
        /// Mezarlıklar: plan çokgeninin çevresinde alçak taş duvar; içinde en uzun kenara paralel sıralar hâlinde mezar taşları
        /// (yerde taş levha + dik baş taşı). Ortada haç biçiminde boş yürüyüş yolu kalır. Zemin çimen (OrganikZemin).
        /// </summary>
        static void MezarlikKur(Transform parent, List<GameObject> sonuc)
        {
            var alanlar = SehirPlani.Mezarlik;
            if (alanlar == null || alanlar.Count == 0) return;
            var tas = new MeshKurucu();
            foreach (var c in alanlar)
            {
                if (c == null || c.Sayi < 3) continue;
                var p = new List<Vector2>(c.Sayi);
                for (int i = 0; i < c.Sayi; i++) p.Add(c[i]);
                if ((p[0] - p[p.Count - 1]).sqrMagnitude < 1e-4f) p.RemoveAt(p.Count - 1);
                int n = p.Count;
                if (n < 3) continue;
                // duvar ve en uzun kenar
                Vector2 uzun = Vector2.right, agirlik = Vector2.zero;
                float enUzun = 0f;
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = p[i], b = p[(i + 1) % n];
                    agirlik += a;
                    float L = (b - a).magnitude;
                    if (L > enUzun) { enUzun = L; uzun = (b - a) / L; }
                    if (L < 1e-4f) continue;
                    tas.IkiYuz(V(a, 0.004f), V(b, 0.004f), V(b, -0.028f), V(a, -0.028f));
                    var kn = new Vector2(-(b - a).y, (b - a).x) / L * 0.012f;
                    tas.Dortgen(V(a + kn, -0.028f), V(b + kn, -0.028f), V(b - kn, -0.028f), V(a - kn, -0.028f));
                }
                agirlik /= n;
                var dik = new Vector2(-uzun.y, uzun.x);
                float yari = 0f;
                for (int i = 0; i < n; i++) yari = Mathf.Max(yari, (p[i] - agirlik).magnitude);
                // sıralar: uzun kenar boyunca 0,26, sıralar arası 0,42 kare
                for (float v = -yari; v <= yari; v += 0.42f)
                    for (float u = -yari; u <= yari; u += 0.26f)
                    {
                        if (Mathf.Abs(v) < 0.2f || Mathf.Abs(u) < 0.16f) continue;       // ortadaki haç biçimli yol
                        var o = agirlik + uzun * u + dik * v;
                        if (!CokgenIci(p, o, 0.3f)) continue;
                        // tek düze dizilmesin: taşların bir kısmı eksik, boyları farklı
                        int hx = Mathf.RoundToInt(o.x * 31f), hy = Mathf.RoundToInt(o.y * 17f);
                        uint hsh = unchecked((uint)(hx * 73856093) ^ (uint)(hy * 19349663));
                        if (hsh % 7u == 0u) continue;
                        float boy = 0.032f + (hsh % 5u) * 0.005f;
                        // yerdeki levha
                        tas.Dortgen(V(o - uzun * 0.035f - dik * 0.08f, -0.0022f), V(o + uzun * 0.035f - dik * 0.08f, -0.0022f),
                                    V(o + uzun * 0.035f + dik * 0.08f, -0.0022f), V(o - uzun * 0.035f + dik * 0.08f, -0.0022f));
                        // baş taşı: küçük dik kutu (üstü ve yanları görünsün)
                        KutuMesh(tas, tas, o + dik * 0.085f, uzun, 0.036f, 0.012f, 0f, boy);
                    }
            }
            if (tas.v.Count > 0) sonuc.Add(Nesne("Mezarlıklar (organik)", parent, tas, Malzeme("OrganikMezarTasi", MezarTasiRenk)));
        }

        /// <summary>Nokta çokgenin içinde ve kenarlarına en az `pay` uzak mı (ışın sayma; çokgen içbükey olabilir)?</summary>
        static bool CokgenIci(List<Vector2> p, Vector2 q, float pay)
        {
            bool ic = false;
            int n = p.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 a = p[i], b = p[j];
                if ((a.y > q.y) != (b.y > q.y) && q.x < (b.x - a.x) * (q.y - a.y) / (b.y - a.y) + a.x) ic = !ic;
                var ab = b - a;
                float L2 = ab.sqrMagnitude;
                float t = L2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / L2) : 0f;
                if ((a + ab * t - q).sqrMagnitude < pay * pay) return false;
            }
            return ic;
        }

        // ------------------------------------------------------------------ ıslak yol (Diorama)

        static float islak, islakParlak = 0.72f, islakKoyu = 0.7f;

        /// <summary>
        /// Yağmurda asfalt koyulaşır ve parlar (gökyüzü, güneş ve gece sokak lambaları yansır); kaldırım ve çizgiler daha az.
        /// Diorama ıslaklık değişince çağırır (CityMap.Islaklik). Malzemeler bu sınıfın kopyaları: asıl dosyalar değişmez.
        /// </summary>
        public static void Islat(float deger, float parlaklik, float koyuluk)
        {
            islak = Mathf.Clamp01(deger);
            islakParlak = parlaklik;
            islakKoyu = koyuluk;
            IslakUygula();
        }

        static void IslakUygula()
        {
            IslakAyarla("OrganikAsfalt", AsfaltRenk, 1f, 1f);
            IslakAyarla("OrganikKaldirim", KaldirimRenk, 0.55f, 0.55f);     // taş: daha az koyulaşır, daha az parlar
            IslakAyarla("OrganikBordur", BordurRenk, 0.55f, 0.55f);
            IslakAyarla("OrganikCizgi", CizgiRenk, 0.3f, 0.7f);
            IslakAyarla("OrganikSariCizgi", SariRenk, 0.3f, 0.7f);
            IslakAyarla("OrganikArnavut", ArnavutRenk, 0.8f, 0.8f);          // ıslak taş: asfalta yakın koyulaşır ve parlar
            IslakAyarla("OrganikCakil", CakilRenk, 0.7f, 0.25f);             // ıslak çakıl koyulaşır, az parlar
        }

        static void IslakAyarla(string ad, Color asil, float koyuPay, float parlakPay)
        {
            if (!malzemeler.TryGetValue(ad, out var m) || m == null) return;
            var c = asil * Mathf.Lerp(1f, Mathf.Lerp(1f, islakKoyu, koyuPay), islak);
            c.a = 1f;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", Mathf.Lerp(0.05f, islakParlak * parlakPay, islak));
        }

        // ------------------------------------------------------------------ nehrin şehir dışındaki kolları

        static readonly Color KumRenk = new Color(0.72f, 0.66f, 0.50f);
        static readonly Color KopruRenk = new Color(0.66f, 0.64f, 0.60f);

        /// <summary>
        /// Nehir şehrin kenarında bitmesin: CityMap.KirNehir kolları aynı canlı suyla harita sonuna kadar akar.
        /// Kıyıda ince kum/çakıl şeridi; kır yollarının nehri geçtiği yerde korkuluklu beton köprü.
        /// </summary>
        static void KirNehirKur(Transform parent, List<GameObject> sonuc)
        {
            var kollar = CityMap.KirNehir;
            if (kollar == null || kollar.Count == 0) return;
            int W = CityMap.Width, H = CityMap.Height;
            bool Disarida(Vector2 p) => p.x < -0.5f || p.y < -0.5f || p.x > W - 0.5f || p.y > H - 0.5f;

            var su = new MeshKurucu();
            var kum = new MeshKurucu();
            foreach (var kol in kollar)
            {
                int n = kol.P.Count;
                if (n < 2) continue;
                Vector2 Yan(int i)
                {
                    var d = (kol.P[Mathf.Min(n - 1, i + 1)] - kol.P[Mathf.Max(0, i - 1)]).normalized;
                    return new Vector2(-d.y, d.x);
                }
                for (int i = 0; i + 1 < n; i++)
                {
                    Vector2 a = kol.P[i], b = kol.P[i + 1], na = Yan(i), nb = Yan(i + 1);
                    float ga = kol.G[i] * 0.5f, gb = kol.G[i + 1] * 0.5f;
                    su.Dortgen(V(a + na * ga, ZSu), V(b + nb * gb, ZSu), V(b - nb * gb, ZSu), V(a - na * ga, ZSu));
                    if (!Disarida(a) && !Disarida(b)) continue;          // şehrin içinde kıyıyı plan çizer
                    for (int k = -1; k <= 1; k += 2)
                    {
                        float ia = ga - 0.05f, ib = gb - 0.05f, da = ga + 0.3f, db = gb + 0.3f;
                        kum.Dortgen(V(a + na * k * ia, ZSu + 0.0007f), V(b + nb * k * ib, ZSu + 0.0007f),
                                    V(b + nb * k * db, ZSu + 0.0007f), V(a + na * k * da, ZSu + 0.0007f));
                    }
                }
            }
            if (su.v.Count == 0) return;
            var suMat = SuMalzemesi();
            sonuc.Add(Nesne("Swan River · şehir dışı", parent, su, suMat != null ? suMat : Malzeme("OrganikSu", SuRenk), golgeAl: false));
            if (kum.v.Count > 0) sonuc.Add(Nesne("Swan River kıyısı · şehir dışı", parent, kum, Malzeme("OrganikKum", KumRenk)));

            // Köprüler: kır yolu × nehir kolu kesişimleri
            var kopruler = new GameObject("Kır köprüleri").transform;
            kopruler.SetParent(parent, false);
            sonuc.Add(kopruler.gameObject);
            var beton = Malzeme("OrganikKopru", KopruRenk);
            var asfalt = Malzeme("OrganikAsfalt", AsfaltRenk);
            foreach (var r in CityMap.CountryRoads)
                foreach (var kol in kollar)
                    for (int i = 0; i + 1 < kol.P.Count; i++)
                    {
                        if (!Kesisim(r.a, r.b, kol.P[i], kol.P[i + 1], out var x)) continue;
                        if (!Disarida(x)) continue;
                        var yol = (r.b - r.a).normalized;
                        var nehir = (kol.P[i + 1] - kol.P[i]).normalized;
                        float sin = Mathf.Abs(yol.x * nehir.y - yol.y * nehir.x);
                        float boy = kol.G[i] / Mathf.Max(0.4f, sin) + 1.1f;
                        Kopru(kopruler, x, yol, boy, beton, asfalt);
                    }
        }

        static bool Kesisim(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2, out Vector2 x)
        {
            x = default;
            var r = p2 - p; var s = q2 - q;
            float den = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(den) < 1e-6f) return false;
            var qp = q - p;
            float t = (qp.x * s.y - qp.y * s.x) / den;
            float u = (qp.x * r.y - qp.y * r.x) / den;
            if (t < 0f || t > 1f || u < 0f || u > 1f) return false;
            x = p + r * t;
            return true;
        }

        /// <summary>Basit beton köprü: tabliye, üstünde asfalt, iki yanda korkuluk, ortada iki ayak (−Z yukarı).</summary>
        static void Kopru(Transform parent, Vector2 merkez, Vector2 yon, float boy, Material beton, Material asfalt)
        {
            float aci = Mathf.Atan2(yon.y, yon.x) * Mathf.Rad2Deg;
            var kok = new GameObject("Köprü").transform;
            kok.SetParent(parent, false);
            kok.localPosition = new Vector3(merkez.x, merkez.y, 0f);
            kok.localRotation = Quaternion.Euler(0f, 0f, aci);
            void Kutu(Vector3 orta, Vector3 boyut, Material m)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
                go.transform.SetParent(kok, false);
                go.transform.localPosition = orta;
                go.transform.localScale = boyut;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = m;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            Kutu(new Vector3(0f, 0f, 0.004f), new Vector3(boy, 1.0f, 0.04f), beton);                 // tabliye (üstü −0,016)
            Kutu(new Vector3(0f, 0f, -0.017f), new Vector3(boy + 0.02f, 0.74f, 0.003f), asfalt);      // asfalt
            for (int k = -1; k <= 1; k += 2)
            {
                Kutu(new Vector3(0f, k * 0.475f, -0.045f), new Vector3(boy, 0.05f, 0.058f), beton);   // korkuluk duvarı
                Kutu(new Vector3(boy * 0.22f * k, 0f, 0.03f), new Vector3(0.12f, 0.7f, 0.06f), beton); // ayak
            }
        }

        // ------------------------------------------------------------------ mesh yardımcıları

        class MeshKurucu
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<int> t = new List<int>();

            public void Ucgen(Vector3 a, Vector3 b, Vector3 c)
            {
                // Yüz yukarı (−Z) baksın: normal (b−a)×(c−a) −Z yönünde olmalı
                if (Vector3.Cross(b - a, c - a).z > 0f) { var tmp = b; b = c; c = tmp; }
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }

            public void Dortgen(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Ucgen(a, b, c); Ucgen(a, c, d); }

            /// <summary>Dik duran dörtgen (duvar): iki sarımla da eklenir, arka yüzü ayıklayan gölgelendiricide de iki yandan görünür.</summary>
            public void IkiYuz(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
                t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2);
            }
        }

        /// <summary>
        /// Çizginin köprü karelerinden geçen parçalarına iki yanda korkuluk duvarı (yarım metre yüksek, iki yüzlü) ve üst şeridi.
        /// ofs: yol ekseninden duvara uzaklık (asfaltın yarısı + kaldırım).
        /// </summary>
        static void KopruKorkulugu(MeshKurucu m, SehirPlani.Cizgi c, float ofs)
        {
            const float Yukseklik = 0.05f, Kalinlik = 0.012f;
            for (int k = 0; k + 1 < c.Sayi; k++)
            {
                Vector2 a = c[k], b = c[k + 1];
                var orta = (a + b) * 0.5f;
                if (!CityMap.BridgeAt(Mathf.RoundToInt(orta.x), Mathf.RoundToInt(orta.y))) continue;
                var d = b - a;
                if (d.sqrMagnitude < 1e-6f) continue;
                var n = new Vector2(-d.y, d.x).normalized;
                for (int yan = -1; yan <= 1; yan += 2)
                {
                    Vector2 a0 = a + n * (ofs * yan), b0 = b + n * (ofs * yan);
                    // duvar: tabandan (kaldırım) yukarı; iki sarımla da eklenir (hangi yandan bakılırsa bakılsın görünsün)
                    Vector3 alt0 = V(a0, ZKaldirim), alt1 = V(b0, ZKaldirim), ust0 = V(a0, ZKaldirim - Yukseklik), ust1 = V(b0, ZKaldirim - Yukseklik);
                    m.IkiYuz(alt0, alt1, ust1, ust0);
                    // üst şerit (yukarı bakar)
                    var kn = n * Kalinlik;
                    m.Dortgen(V(a0 + kn, ZKaldirim - Yukseklik), V(b0 + kn, ZKaldirim - Yukseklik), V(b0 - kn, ZKaldirim - Yukseklik), V(a0 - kn, ZKaldirim - Yukseklik));
                }
            }
        }

        /// <summary>Çizgi boyunca yarı genişliği r olan şerit; köşe ve uçlarda yuvarlak disk (boşluk ve sivri uç kalmasın).</summary>
        static void Serit(MeshKurucu m, SehirPlani.Cizgi c, float r, float z)
        {
            for (int k = 0; k + 1 < c.Sayi; k++)
            {
                Vector2 a = c[k], b = c[k + 1];
                var d = b - a;
                if (d.sqrMagnitude < 1e-6f) continue;
                var n = new Vector2(-d.y, d.x).normalized * r;
                m.Dortgen(V(a + n, z), V(b + n, z), V(b - n, z), V(a - n, z));
            }
            // disk yalnız uçlarda ve yön değiştiren köşelerde (sıklaştırılmış düz parçalarda gereksiz)
            for (int k = 0; k < c.Sayi; k++)
            {
                if (k > 0 && k < c.Sayi - 1)
                {
                    Vector2 d0 = (c[k] - c[k - 1]).normalized, d1 = (c[k + 1] - c[k]).normalized;
                    if (Vector2.Dot(d0, d1) > 0.997f) continue;
                }
                Disk(m, c[k], r, z, 12);
            }
        }

        static void Disk(MeshKurucu m, Vector2 o, float r, float z, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                m.Ucgen(V(o, z), V(o + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, z), V(o + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r, z));
            }
        }

        static void KesikCizgi(MeshKurucu m, SehirPlani.Cizgi c, float yari, float z)
        {
            const float Dolu = 0.22f, Periyot = 0.40f;
            // Çizgi boyunca birikimli uzunluk; her kesik, başlangıç ve bitiş noktası arasında düz bir parça
            var top = new float[c.Sayi];
            for (int k = 1; k < c.Sayi; k++) top[k] = top[k - 1] + Vector2.Distance(c[k - 1], c[k]);
            float L = top[c.Sayi - 1];
            int seg = 0;
            Vector2 Nokta(float d)
            {
                while (seg < c.Sayi - 2 && top[seg + 1] < d) seg++;
                float u = top[seg + 1] - top[seg];
                float t = u > 1e-5f ? (d - top[seg]) / u : 0f;
                return Vector2.Lerp(c[seg], c[seg + 1], Mathf.Clamp01(t));
            }
            int adet = Mathf.FloorToInt(L / Periyot);
            for (int i = 0; i <= adet; i++)
            {
                float d0 = i * Periyot + 0.09f, d1 = Mathf.Min(L, d0 + Dolu);
                if (d1 - d0 < 0.05f) break;
                var p0 = Nokta(d0);
                var p1 = Nokta(d1);
                var yon = p1 - p0;
                if (yon.sqrMagnitude < 1e-6f) continue;
                var n = new Vector2(-yon.y, yon.x).normalized * yari;
                m.Dortgen(V(p0 + n, z), V(p1 + n, z), V(p1 - n, z), V(p0 - n, z));
            }
        }

        static float Uzunluk(SehirPlani.Cizgi c)
        {
            float L = 0f;
            for (int k = 1; k < c.Sayi; k++) L += Vector2.Distance(c[k - 1], c[k]);
            return L;
        }

        /// <summary>Çizgiye paralel (ofs kadar yana kaymış) düz ya da kesik şerit çizgisi.</summary>
        static void YanCizgi(MeshKurucu m, SehirPlani.Cizgi c, float ofs, float yari, float z, bool kesik)
        {
            const float Dolu = 0.2f, Periyot = 0.38f;
            double s = 0.0;
            for (int k = 0; k + 1 < c.Sayi; k++)
            {
                Vector2 a = c[k], b = c[k + 1];
                float L = Vector2.Distance(a, b);
                if (L < 1e-5f) continue;
                var yon = (b - a) / L;
                var n = new Vector2(-yon.y, yon.x);
                if (!kesik)
                {
                    var p0 = a + n * ofs; var p1 = b + n * ofs;
                    m.Dortgen(V(p0 + n * yari, z), V(p1 + n * yari, z), V(p1 - n * yari, z), V(p0 - n * yari, z));
                }
                else
                {
                    // Parçalar arasında kesik düzeni sürsün (birikimli uzunluk).
                    // Kesikler sabit aralıklı: j. kesik [j·Periyot, j·Periyot + Dolu]. Tamsayı sayaçlı, her zaman biter.
                    long j0 = (long)System.Math.Floor(s / Periyot);
                    long j1 = (long)System.Math.Floor((s + L) / Periyot);
                    for (long j = j0; j <= j1; j++)
                    {
                        float d0 = Mathf.Max(0f, (float)(j * (double)Periyot - s));
                        float d1 = Mathf.Min(L, (float)(j * (double)Periyot + Dolu - s));
                        if (d1 - d0 < 1e-4f) continue;
                        var p0 = a + yon * d0 + n * ofs; var p1 = a + yon * d1 + n * ofs;
                        m.Dortgen(V(p0 + n * yari, z), V(p1 + n * yari, z), V(p1 - n * yari, z), V(p0 - n * yari, z));
                    }
                }
                s += L;
            }
        }

        /// <summary>Yolun iki ucunda (kavşağa varmadan) zebra yaya geçidi.</summary>
        static void YayaGecidi(MeshKurucu m, SehirPlani.Cizgi c, float g, float z)
        {
            for (int uc = 0; uc < 2; uc++)
            {
                Vector2 p0 = uc == 0 ? c[0] : c[c.Sayi - 1];
                Vector2 p1 = uc == 0 ? c[1] : c[c.Sayi - 2];
                var d = p1 - p0;
                float L = d.magnitude;
                if (L < g + 0.3f) continue;
                d /= L;
                var n = new Vector2(-d.y, d.x);
                var mrk = p0 + d * (g * 0.5f + 0.2f);
                int seritSayi = Mathf.Max(4, Mathf.RoundToInt(g / 0.07f));
                float bas = -g * 0.5f + 0.04f, adim = (g - 0.08f) / seritSayi;
                for (int i = 0; i < seritSayi; i++)
                {
                    if (i % 2 == 1) continue;
                    float u0 = bas + i * adim, u1 = u0 + adim;
                    var a = mrk + n * u0 - d * 0.09f; var b = mrk + n * u1 - d * 0.09f;
                    var e = mrk + n * u1 + d * 0.09f; var f = mrk + n * u0 + d * 0.09f;
                    m.Dortgen(V(a, z), V(b, z), V(e, z), V(f, z));
                }
            }
        }

        /// <summary>Canlı su: KuryeTycoon/Su (dalga, parıltı, akıntı). Bulunamazsa null (düz renk malzeme kullanılır).</summary>
        static Material SuMalzemesi()
        {
            if (malzemeler.TryGetValue("OrganikSuCanli", out var m) && m != null) return m;
            var sh = Shader.Find("KuryeTycoon/Su");
            if (sh == null) return null;
            m = new Material(sh) { name = "OrganikSuCanli" };
            malzemeler["OrganikSuCanli"] = m;
            return m;
        }

        /// <summary>Basit çokgen (içbükey olabilir) — kulak kesme ile üçgenler.</summary>
        static void Poligon(MeshKurucu m, List<Vector2> p, float z)
        {
            int n = p.Count;
            if (n < 3) return;
            float alan = 0f;
            for (int i = 0; i < n; i++) { var a = p[i]; var b = p[(i + 1) % n]; alan += a.x * b.y - b.x * a.y; }
            var idx = new List<int>(n);
            for (int i = 0; i < n; i++) idx.Add(alan > 0f ? i : n - 1 - i);     // saat yönünün tersi
            int guvenlik = n * n;
            while (idx.Count > 3 && guvenlik-- > 0)
            {
                bool kesildi = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = p[ia], b = p[ib], c = p[ic];
                    if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) <= 1e-7f) continue;   // içbükey köşe
                    bool icerde = false;
                    for (int j = 0; j < idx.Count && !icerde; j++)
                    {
                        int k = idx[j];
                        if (k == ia || k == ib || k == ic) continue;
                        if (UcgenIcinde(p[k], a, b, c)) icerde = true;
                    }
                    if (icerde) continue;
                    UcgenBol(m, a, b, c, z);
                    idx.RemoveAt(i);
                    kesildi = true;
                    break;
                }
                if (!kesildi) break;     // bozuk çokgen: kalan kısmı bırak
            }
            if (idx.Count == 3) UcgenBol(m, p[idx[0]], p[idx[1]], p[idx[2]], z);
        }

        /// <summary>Büyük üçgeni (park, su) kenarı ~0,8 kareden kısa parçalara böler: tepeli arazide düz üçgen zemine gömülmesin.</summary>
        static void UcgenBol(MeshKurucu m, Vector2 a, Vector2 b, Vector2 c, float z, int derinlik = 0)
        {
            float enUzun = Mathf.Max((a - b).sqrMagnitude, Mathf.Max((b - c).sqrMagnitude, (c - a).sqrMagnitude));
            if (duz || enUzun < 0.64f || derinlik > 6) { m.Ucgen(V(a, z), V(b, z), V(c, z)); return; }
            Vector2 ab = (a + b) * 0.5f, bc = (b + c) * 0.5f, ca = (c + a) * 0.5f;
            UcgenBol(m, a, ab, ca, z, derinlik + 1);
            UcgenBol(m, ab, b, bc, z, derinlik + 1);
            UcgenBol(m, ca, bc, c, z, derinlik + 1);
            UcgenBol(m, ab, bc, ca, z, derinlik + 1);
        }

        static bool UcgenIcinde(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        // Kara üstündeki her köşe arazinin yüksekliğine oturur (yollar tepeye çıkar, vadiye iner); su yüzeyi düz kalır (duz = true)
        static bool duz;
        static Vector3 V(Vector2 p, float z) => new Vector3(p.x, p.y, z + (duz ? 0f : Arazi.Z(p.x, p.y)));

        static GameObject Nesne(string ad, Transform parent, MeshKurucu k, Material mat, bool golgeAl = true)
        {
            var go = new GameObject(ad);
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = ad };
            if (k.v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(k.v);
            mesh.SetTriangles(k.t, 0);
            var nr = new Vector3[k.v.Count];
            for (int i = 0; i < nr.Length; i++) nr[i] = Vector3.back;      // yukarı (−Z)
            mesh.normals = nr;
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = golgeAl;
            mr.sortingOrder = 1;
            return go;
        }

        static readonly Dictionary<string, Material> malzemeler = new Dictionary<string, Material>();
        static Shader ornekShader;

        static Material Malzeme(string ad, Color c)
        {
            var sh = ornekShader;
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (malzemeler.TryGetValue(ad, out var m) && m != null && m.shader == sh) return m;
            m = new Material(sh) { name = ad };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", ad == "OrganikSu" ? 0.7f : 0.05f);
            malzemeler[ad] = m;
            return m;
        }
    }
}
