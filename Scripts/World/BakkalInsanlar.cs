using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Bakkalın insanları (BakkalCanli'nin parçası): kasiyer, müşteriler, kurye ve tedarik şoförü.
    /// Figürler koddan kurulur (küp ve küre parçalar: gövde, baş, saç, kollar, bacaklar; kol ve bacaklar omuz / kalçadan
    /// döner), malzemeleri modelin bir malzemesinden kopyalanır (oyunun o anki gölgelendiricisiyle aynı görünür).
    /// Yürüyüş: bakkal.py'deki yol noktası ağı (Noktalar / Kenarlar) üstünde en kısa yol; NavMesh yok.
    /// </summary>
    public partial class BakkalCanli
    {
        enum Rol { Musteri, Kasiyer, Kurye, Sofor, Kasiyer2 }

        class Kisi
        {
            public Rol rol;
            public Transform kok, kolSol, kolSag, bacakSol, bacakSag;
            public GameObject esya;            // elinde taşıdığı (sepet, poşet, koli)
            public Vector3 yer;                // model uzayı (metre)
            public Vector2 bakis = new Vector2(0f, 1f);
            public readonly List<int> yol = new List<int>();
            public int dugum;                  // en son bulunduğu yol noktası
            public int asama;
            public float bekle;
            public int kalanRaf;
            public int kuyruk = -1;
            public float faz;
            public bool yuruyor, uzaniyor;
            public Vector2 rafYonu;
            public string cikis;
        }

        // ---- yol ağı
        Vector3[] dugumYer;
        List<int>[] komsu;
        readonly Dictionary<string, int> dugumNo = new Dictionary<string, int>();
        static readonly string[] RafDugum = { "A0a", "A0b", "A0c", "A1a", "A1b", "A1c", "A2a", "A2b", "A2c", "B0", "B1", "B2" };
        static readonly string[] KuyrukDugum = { "Q0", "Q1", "Q2", "Q3" };
        readonly Kisi[] kuyrukSahibi = new Kisi[4];

        readonly List<Kisi> kisiler = new List<Kisi>();
        Kisi kasiyer, kasiyer2, kurye, sofor;
        float musteriSayaci;
        int kasiyerAsama;                      // 0 kasada, 1 raflara gidiyor, 2 dönüyor
        int kasiyerKalan;
        float odemeSayaci;

        const float YuruHizi = 1.5f;           // m/sn (oyun hızıyla 2,5 kata kadar artar)

        bool KasiyerKasada => kasiyer != null && kasiyerAsama == 0 && kasiyer.yol.Count == 0;
        int MusteriSayisi
        {
            get
            {
                int n = 0;
                for (int i = 0; i < kisiler.Count; i++) if (kisiler[i].rol == Rol.Musteri) n++;
                return n;
            }
        }

        void AgKur()
        {
            dugumYer = new Vector3[Noktalar.Length];
            komsu = new List<int>[Noktalar.Length];
            for (int i = 0; i < Noktalar.Length; i++)
            {
                dugumNo[Noktalar[i].ad] = i;
                dugumYer[i] = Noktalar[i].p;
                komsu[i] = new List<int>();
            }
            foreach (var k in Kenarlar)
            {
                int tire = k.IndexOf('-');
                if (tire <= 0) continue;
                if (!dugumNo.TryGetValue(k.Substring(0, tire), out int a) || !dugumNo.TryGetValue(k.Substring(tire + 1), out int b)) continue;
                komsu[a].Add(b);
                komsu[b].Add(a);
            }
        }

        int D(string ad) => dugumNo.TryGetValue(ad, out int i) ? i : 0;

        /// <summary>k'nin bulunduğu noktadan hedefe en kısa yol (Dijkstra; ağ küçük).</summary>
        void YolVer(Kisi k, int hedef)
        {
            k.yol.Clear();
            int n = dugumYer.Length;
            if (hedef < 0 || hedef >= n || hedef == k.dugum) return;
            var uzak = new float[n];
            var once = new int[n];
            var bitti = new bool[n];
            for (int i = 0; i < n; i++) { uzak[i] = float.MaxValue; once[i] = -1; }
            uzak[k.dugum] = 0f;
            for (int adim = 0; adim < n; adim++)
            {
                int en = -1;
                for (int i = 0; i < n; i++) if (!bitti[i] && (en < 0 || uzak[i] < uzak[en])) en = i;
                if (en < 0 || uzak[en] == float.MaxValue) break;
                bitti[en] = true;
                if (en == hedef) break;
                foreach (int j in komsu[en])
                {
                    float d = uzak[en] + Vector3.Distance(dugumYer[en], dugumYer[j]);
                    if (d < uzak[j]) { uzak[j] = d; once[j] = en; }
                }
            }
            if (once[hedef] < 0) return;
            for (int i = hedef; i != k.dugum && i >= 0; i = once[i]) k.yol.Insert(0, i);
        }

        /// <summary>Yol boyunca yürütür. Yol bittiyse true.</summary>
        bool Yuru(Kisi k, float dt)
        {
            k.yuruyor = false;
            float kalan = YuruHizi * dt;
            while (k.yol.Count > 0 && kalan > 0f)
            {
                Vector3 h = dugumYer[k.yol[0]];
                Vector3 fark = h - k.yer;
                float d = fark.magnitude;
                if (d > 1e-4f)
                {
                    var yatay = new Vector2(fark.x, fark.y);
                    if (yatay.sqrMagnitude > 1e-6f) k.bakis = yatay.normalized;
                    k.yuruyor = true;
                }
                if (d <= kalan)
                {
                    k.yer = h;
                    kalan -= d;
                    k.dugum = k.yol[0];
                    k.yol.RemoveAt(0);
                }
                else
                {
                    k.yer += fark / d * kalan;
                    kalan = 0f;
                }
            }
            return k.yol.Count == 0;
        }

        // ---------------------------------------------------------------- figür

        static Mesh kupMesh, kureMesh;
        static readonly Dictionary<int, Material> insanMalzeme = new Dictionary<int, Material>();
        Material ornekMalzeme;

        static void MeshleriHazirla()
        {
            if (kupMesh != null && kureMesh != null) return;
            var k = GameObject.CreatePrimitive(PrimitiveType.Cube);
            kupMesh = k.GetComponent<MeshFilter>().sharedMesh;
            Destroy(k);
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            kureMesh = s.GetComponent<MeshFilter>().sharedMesh;
            Destroy(s);
        }

        Material Boya(Color c)
        {
            Color32 c32 = c;
            int anahtar = (c32.r << 16) | (c32.g << 8) | c32.b;
            if (insanMalzeme.TryGetValue(anahtar, out var m) && m != null) return m;
            if (ornekMalzeme == null)
            {
                foreach (var r in kok.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var sm in r.sharedMaterials)
                        if (sm != null && sm.name.StartsWith("Boya")) { ornekMalzeme = sm; break; }
                    if (ornekMalzeme != null) break;
                }
            }
            if (ornekMalzeme != null) m = new Material(ornekMalzeme);
            else
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                m = new Material(sh);
            }
            m.name = "Insan_" + anahtar.ToString("x6");
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            insanMalzeme[anahtar] = m;
            return m;
        }

        GameObject Parcacik(Transform ust, string ad, Mesh mesh, Vector3 yer, Vector3 olcek, Color renk)
        {
            var go = new GameObject(ad);
            go.transform.SetParent(ust, false);
            go.transform.localPosition = yer;
            go.transform.localScale = olcek;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Boya(renk);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            return go;
        }

        Transform Uzuv(Transform ust, string ad, Vector3 mafsal, Vector3 olcek, Color renk, Color uc)
        {
            var m = new GameObject(ad).transform;
            m.SetParent(ust, false);
            m.localPosition = mafsal;
            Parcacik(m, "parça", kupMesh, new Vector3(0f, -olcek.y * 0.5f, 0f), olcek, renk);
            Parcacik(m, "uç", kupMesh, new Vector3(0f, -olcek.y - 0.03f, 0.02f), new Vector3(olcek.x * 1.05f, 0.08f, olcek.z * 1.25f), uc);
            return m;
        }

        static readonly Color[] UstRenk =
        {
            new Color32(0xd9, 0x4f, 0x3d, 255), new Color32(0xe8, 0xa2, 0x3a, 255), new Color32(0x5a, 0x9a, 0x4a, 255), new Color32(0x2f, 0x7d, 0x8c, 255),
            new Color32(0x8a, 0x4a, 0x6b, 255), new Color32(0x7a, 0xb8, 0xd6, 255), new Color32(0xf2, 0xd0, 0x4a, 255), new Color32(0xf4, 0xf0, 0xe6, 255),
        };
        static readonly Color[] AltRenk = { new Color32(0x3d, 0x5a, 0x8a, 255), new Color32(0x4a, 0x4d, 0x52, 255), new Color32(0x6b, 0x45, 0x2a, 255), new Color32(0x2b, 0x2d, 0x31, 255) };
        static readonly Color[] SacRenk = { new Color32(0x2b, 0x2d, 0x31, 255), new Color32(0x4a, 0x2f, 0x20, 255), new Color32(0xc9, 0xa2, 0x4a, 255), new Color32(0x8a, 0x5a, 0x3a, 255), new Color32(0xb8, 0xb8, 0xb4, 255) };
        static readonly Color[] TenRenk = { new Color32(0xe8, 0xb8, 0x90, 255), new Color32(0xf0, 0xc8, 0xa0, 255), new Color32(0xc6, 0x8e, 0x62, 255), new Color32(0x8d, 0x5a, 0x3a, 255) };

        /// <summary>Figür: kökü ayak tabanında, kendi uzayında +Z ileri, +Y yukarı, ölçü metre (kök modelin metresine ölçeklenir).</summary>
        Kisi KisiKur(Rol rol)
        {
            MeshleriHazirla();
            var k = new Kisi { rol = rol };
            var kok_ = new GameObject(rol.ToString()).transform;
            kok_.SetParent(canliKok, false);
            kok_.localScale = Vector3.one * metre;
            k.kok = kok_;
            Color ust = UstRenk[Random.Range(0, UstRenk.Length)], alt = AltRenk[Random.Range(0, AltRenk.Length)];
            Color sac = SacRenk[Random.Range(0, SacRenk.Length)], ten = TenRenk[Random.Range(0, TenRenk.Length)];
            Color ayakkabi = new Color32(0x3a, 0x2a, 0x22, 255);
            if (rol == Rol.Kasiyer || rol == Rol.Kasiyer2) { ust = new Color32(0xf4, 0xf0, 0xe6, 255); alt = AltRenk[0]; }
            else if (rol == Rol.Kurye) { ust = CompanyProfile.Primary; alt = AltRenk[3]; }
            else if (rol == Rol.Sofor) { ust = new Color32(0x3f, 0x8a, 0x4a, 255); alt = AltRenk[1]; }

            k.bacakSol = Uzuv(kok_, "bacak sol", new Vector3(-0.09f, 0.82f, 0f), new Vector3(0.14f, 0.76f, 0.17f), alt, ayakkabi);
            k.bacakSag = Uzuv(kok_, "bacak sağ", new Vector3(0.09f, 0.82f, 0f), new Vector3(0.14f, 0.76f, 0.17f), alt, ayakkabi);
            Parcacik(kok_, "gövde", kupMesh, new Vector3(0f, 1.11f, 0f), new Vector3(0.42f, 0.62f, 0.24f), ust);
            k.kolSol = Uzuv(kok_, "kol sol", new Vector3(-0.27f, 1.38f, 0f), new Vector3(0.1f, 0.5f, 0.1f), ust, ten);
            k.kolSag = Uzuv(kok_, "kol sağ", new Vector3(0.27f, 1.38f, 0f), new Vector3(0.1f, 0.5f, 0.1f), ust, ten);
            Parcacik(kok_, "boyun", kupMesh, new Vector3(0f, 1.45f, 0f), new Vector3(0.12f, 0.08f, 0.12f), ten);
            Parcacik(kok_, "baş", kureMesh, new Vector3(0f, 1.62f, 0f), new Vector3(0.28f, 0.3f, 0.28f), ten);
            Parcacik(kok_, "saç", kureMesh, new Vector3(0f, 1.69f, -0.03f), new Vector3(0.3f, 0.22f, 0.3f), sac);
            if (rol == Rol.Kasiyer || rol == Rol.Kasiyer2)
                Parcacik(kok_, "önlük", kupMesh, new Vector3(0f, 0.95f, 0.125f), new Vector3(0.34f, 0.72f, 0.02f), new Color32(0x2f, 0x6b, 0x57, 255));
            if (rol == Rol.Kurye)
                Parcacik(kok_, "çanta", kupMesh, new Vector3(0f, 1.15f, -0.27f), new Vector3(0.4f, 0.48f, 0.28f), CompanyProfile.Primary);
            kisiler.Add(k);
            return k;
        }

        /// <summary>Elindeki eşya: 0 yok, 1 sepet, 2 poşet, 3 koli.</summary>
        void EsyaVer(Kisi k, int tur)
        {
            if (k.esya != null) { Destroy(k.esya); k.esya = null; }
            if (tur == 1) k.esya = Parcacik(k.kolSag, "sepet", kupMesh, new Vector3(0f, -0.66f, 0.06f), new Vector3(0.3f, 0.2f, 0.36f), new Color32(0xc8, 0x34, 0x2f, 255));
            else if (tur == 2) k.esya = Parcacik(k.kolSag, "poşet", kupMesh, new Vector3(0f, -0.72f, 0.04f), new Vector3(0.17f, 0.32f, 0.26f), new Color32(0xc4, 0x9a, 0x6c, 255));
            else if (tur == 3) k.esya = Parcacik(k.kok, "koli", kupMesh, new Vector3(0f, 1.05f, 0.32f), new Vector3(0.42f, 0.32f, 0.36f), new Color32(0xc4, 0x9a, 0x6c, 255));
        }

        void KisiSil(Kisi k)
        {
            kisiler.Remove(k);
            for (int i = 0; i < kuyrukSahibi.Length; i++) if (kuyrukSahibi[i] == k) kuyrukSahibi[i] = null;
            if (k.kok != null) Destroy(k.kok.gameObject);
            k.kok = null;
            if (kasiyer == k) kasiyer = null;
            if (kasiyer2 == k) kasiyer2 = null;
            if (kurye == k) kurye = null;
            if (sofor == k) sofor = null;
        }

        void Yerlestir(Kisi k, string dugum)
        {
            k.dugum = D(dugum);
            k.yer = dugumYer[k.dugum];
            k.yol.Clear();
        }

        /// <summary>Figürü model konumuna ve bakışına oturtur, yürüyüş / uzanma hareketini uygular.</summary>
        void Ciz(Kisi k, float dt)
        {
            if (k.kok == null) return;
            k.kok.localPosition = Yerel(k.yer);
            Vector3 ileri = YerelYon(new Vector3(k.bakis.x, k.bakis.y, 0f));
            if (ileri.sqrMagnitude > 1e-12f) k.kok.localRotation = Quaternion.LookRotation(ileri.normalized, yukari);
            float sal = 0f;
            if (k.yuruyor) { k.faz += dt * 7.5f; sal = Mathf.Sin(k.faz) * 28f; }
            else k.faz = 0f;
            k.bacakSol.localRotation = Quaternion.Euler(sal, 0f, 0f);
            k.bacakSag.localRotation = Quaternion.Euler(-sal, 0f, 0f);
            float solKol = -sal * 0.8f, sagKol = sal * 0.8f;
            if (k.uzaniyor)
            {
                // rafa / kasaya uzanma: sağ kol öne kalkıp iner
                k.faz += dt * 5f;
                sagKol = -70f - Mathf.Sin(k.faz) * 18f;
            }
            else if (k.esya != null && k.rol != Rol.Sofor) sagKol = k.yuruyor ? sal * 0.25f : 0f;
            if (k.rol == Rol.Sofor && k.esya != null) { solKol = -55f; sagKol = -55f; }
            k.kolSol.localRotation = Quaternion.Euler(solKol, 0f, 0f);
            k.kolSag.localRotation = Quaternion.Euler(sagKol, 0f, 0f);
        }

        // ---------------------------------------------------------------- başlat / topla

        void InsanlariBaslat()
        {
            InsanlariTopla();
            if (!ShopHours.IsOpen(shop.def)) return;
            kasiyer = KisiKur(Rol.Kasiyer);
            Yerlestir(kasiyer, "KAS");
            kasiyer.bakis = new Vector2(-1f, 0f);
            kasiyerAsama = 0;
            // Dükkân zaten çalışıyordu: müşterilerin bir kısmı raf başında
            int hedef = MusteriHedefi();
            for (int i = 0; i < hedef; i++)
            {
                var m = KisiKur(Rol.Musteri);
                Yerlestir(m, RafDugum[Random.Range(0, RafDugum.Length)]);
                m.asama = 1;
                m.bekle = Random.Range(0.5f, 3f);
                m.kalanRaf = Random.Range(0, 3);
                m.cikis = Random.value < 0.5f ? "S1" : "S2";
                RafaDon(m);
                if (Random.value < 0.6f) EsyaVer(m, 1);
            }
            musteriSayaci = Random.Range(1f, 3f);
        }

        void InsanlariTopla()
        {
            for (int i = kisiler.Count - 1; i >= 0; i--)
                if (kisiler[i].kok != null) Destroy(kisiler[i].kok.gameObject);
            kisiler.Clear();
            kasiyer = kasiyer2 = kurye = sofor = null;
            for (int i = 0; i < kuyrukSahibi.Length; i++) kuyrukSahibi[i] = null;
            kasiyerAsama = 0;
        }

        int MusteriHedefi()
        {
            var clk = GameClock.I;
            if (clk == null || !dukkanAcik) return 0;
            float w = OrderManager.DemandCurve(shop.def, clk.Hour, clk.IsWeekend);
            return Mathf.Clamp(Mathf.RoundToInt(w * 2.4f + (clk.Hour >= 7 && clk.Hour < 22 ? 0.6f : 0f)), 0, 5);
        }

        void RafaDon(Kisi m)
        {
            string ad = Noktalar[m.dugum].ad;
            // Arka koridor (B*): soğutucular ve dondurma dolabı arkada (+Y); ara koridor (A*): sağdaki ya da soldaki raf
            m.rafYonu = ad.StartsWith("B") ? new Vector2(0f, 1f) : new Vector2(Random.value < 0.5f ? -1f : 1f, 0f);
            m.bakis = m.rafYonu;
        }

        bool KapiYakini()
        {
            Vector3 kp = dugumYer[D("KAPI")];
            for (int i = 0; i < kisiler.Count; i++)
            {
                Vector3 f = kisiler[i].yer - kp;
                if (f.x * f.x + f.y * f.y < 1.5f * 1.5f) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- simülasyon

        void InsanlariGuncelle(float dt)
        {
            if (dugumYer == null) return;
            // Dükkân kapandı: kasiyer ve müşteriler çıkar
            if (!dukkanAcik)
            {
                for (int i = kisiler.Count - 1; i >= 0; i--)
                {
                    var k = kisiler[i];
                    if (k.rol == Rol.Sofor) continue;
                    if (k.asama != 9) { k.asama = 9; KuyruktanCik(k); YolVer(k, D("S1")); }
                }
            }
            else
            {
                if (kasiyer == null)
                {
                    kasiyer = KisiKur(Rol.Kasiyer);
                    Yerlestir(kasiyer, "KAS");
                    kasiyer.bakis = new Vector2(-1f, 0f);
                    kasiyerAsama = 0;
                }
                // Müşteri gelişi: yoğun saatte daha sık
                musteriSayaci -= dt;
                if (musteriSayaci <= 0f)
                {
                    musteriSayaci = Random.Range(2.5f, 6f);
                    if (MusteriSayisi < MusteriHedefi())
                    {
                        var m = KisiKur(Rol.Musteri);
                        m.cikis = Random.value < 0.5f ? "S1" : "S2";
                        Yerlestir(m, m.cikis);
                        m.asama = 0;
                        m.kalanRaf = Random.Range(1, 4);
                        YolVer(m, D(RafDugum[Random.Range(0, RafDugum.Length)]));
                    }
                }
            }

            // Kozmetik ikinci kasa: başında duran ikinci kasiyer
            bool k2 = dukkanAcik && Sus(SusKasa2);
            if (k2 && kasiyer2 == null)
            {
                kasiyer2 = KisiKur(Rol.Kasiyer2);
                kasiyer2.yer = new Vector3(3.8f, -0.1f, ZeminZ);
                kasiyer2.bakis = new Vector2(-1f, 0f);
            }
            else if (!k2 && kasiyer2 != null) KisiSil(kasiyer2);

            // Kurye: gerçek kurye dükkâna yaklaşınca içeri girer
            if (kurye == null && dukkanAcik && (kuryeYakin || alinanOlay > 0) && (hazir > 0 || hazirlanan > 0 || alinanOlay > 0))
            {
                kurye = KisiKur(Rol.Kurye);
                Yerlestir(kurye, "S1");
                kurye.asama = 0;
                YolVer(kurye, D("KUR"));
            }
            // Tedarik şoförü: indirme sürerken kamyonetle arka kapı arasında koli taşır
            if (tedarikSuruyor && sofor == null)
            {
                sofor = KisiKur(Rol.Sofor);
                Yerlestir(sofor, "KV");
                sofor.asama = 0;
                EsyaVer(sofor, 3);
                YolVer(sofor, D("AI"));
            }

            for (int i = kisiler.Count - 1; i >= 0; i--)
            {
                var k = kisiler[i];
                k.uzaniyor = false;
                switch (k.rol)
                {
                    case Rol.Musteri: Musteri(k, dt); break;
                    case Rol.Kasiyer: Kasiyer(k, dt); break;
                    case Rol.Kurye: Kurye(k, dt); break;
                    case Rol.Sofor: Sofor(k, dt); break;
                    default: k.yuruyor = false; break;
                }
                if (k.kok != null) Ciz(k, dt);
            }
        }

        void KuyruktanCik(Kisi k)
        {
            if (k.kuyruk >= 0 && k.kuyruk < kuyrukSahibi.Length && kuyrukSahibi[k.kuyruk] == k) kuyrukSahibi[k.kuyruk] = null;
            k.kuyruk = -1;
        }

        /// <summary>Müşteri: 0 rafa gidiyor · 1 rafta bakıyor · 2 kuyruğa gidiyor / sırada · 3 ödüyor · 9 çıkıyor.</summary>
        void Musteri(Kisi m, float dt)
        {
            switch (m.asama)
            {
                case 0:
                    if (Yuru(m, dt))
                    {
                        m.asama = 1;
                        m.bekle = Random.Range(1.6f, 3.6f);
                        RafaDon(m);
                    }
                    break;
                case 1:
                    m.bakis = m.rafYonu;
                    m.bekle -= dt;
                    m.uzaniyor = m.bekle < 1.2f;
                    if (m.bekle <= 0f)
                    {
                        if (m.esya == null) EsyaVer(m, 1);
                        if (--m.kalanRaf > 0)
                        {
                            m.asama = 0;
                            YolVer(m, D(RafDugum[Random.Range(0, RafDugum.Length)]));
                        }
                        else m.asama = 2;
                    }
                    break;
                case 2:
                    {
                        // Sıradaki ilk boş yer; önü boşaldıkça ilerler
                        int hedef = m.kuyruk;
                        if (hedef < 0)
                        {
                            for (int i = 0; i < kuyrukSahibi.Length; i++) if (kuyrukSahibi[i] == null) { hedef = i; break; }
                            if (hedef < 0) { m.asama = 0; m.kalanRaf = 1; YolVer(m, D(RafDugum[Random.Range(0, RafDugum.Length)])); break; }
                            kuyrukSahibi[hedef] = m;
                            m.kuyruk = hedef;
                            YolVer(m, D(KuyrukDugum[hedef]));
                        }
                        else if (hedef > 0 && kuyrukSahibi[hedef - 1] == null)
                        {
                            kuyrukSahibi[hedef] = null;
                            kuyrukSahibi[hedef - 1] = m;
                            m.kuyruk = hedef - 1;
                            YolVer(m, D(KuyrukDugum[hedef - 1]));
                        }
                        bool vardi = Yuru(m, dt);
                        if (vardi)
                        {
                            m.bakis = m.kuyruk == 0 ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
                            if (m.kuyruk == 0 && KasiyerKasada) { m.asama = 3; m.bekle = 2.6f; odemeSayaci = 2.6f; }
                        }
                    }
                    break;
                case 3:
                    m.bakis = new Vector2(1f, 0f);
                    m.uzaniyor = true;
                    m.bekle -= dt;
                    if (m.bekle <= 0f)
                    {
                        KuyruktanCik(m);
                        EsyaVer(m, 2);
                        m.asama = 9;
                        YolVer(m, D(m.cikis ?? "S1"));
                    }
                    break;
                default:
                    if (Yuru(m, dt)) KisiSil(m);
                    break;
            }
        }

        /// <summary>Kasiyer: kasada müşteriye bakar; hazırlanan sipariş varsa raflardan toplar ve döner (sonra poşet tezgâhta).</summary>
        void Kasiyer(Kisi k, float dt)
        {
            if (k.asama == 9) { if (Yuru(k, dt)) KisiSil(k); return; }
            switch (kasiyerAsama)
            {
                case 0:
                    k.yuruyor = false;
                    k.bakis = new Vector2(-1f, 0f);
                    if (odemeSayaci > 0f) { odemeSayaci -= dt; k.uzaniyor = true; }     // ürünleri okutuyor
                    else if (hazirlanan > 0 && toplanacak != null)
                    {
                        // Gerçek sipariş hazırlanıyor: ürün sayısına göre iki ya da üç rafa uğrar
                        toplanan.Add(toplanacak.id);
                        if (toplanan.Count > 64) toplanan.Clear();
                        kasiyerKalan = toplanacak.ItemCount > 3 ? 3 : 2;
                        toplanacak = null;
                        kasiyerAsama = 1;
                        YolVer(k, D(RafDugum[Random.Range(0, RafDugum.Length)]));
                    }
                    else if (hazir > 0 && !posetGorunur) k.uzaniyor = true;             // poşetliyor
                    break;
                case 1:
                    if (k.bekle > 0f)
                    {
                        k.bekle -= dt;
                        k.bakis = k.rafYonu;
                        k.uzaniyor = true;
                        if (k.bekle <= 0f)
                        {
                            if (k.esya == null) EsyaVer(k, 1);
                            if (--kasiyerKalan > 0) YolVer(k, D(RafDugum[Random.Range(0, RafDugum.Length)]));
                            else { kasiyerAsama = 2; YolVer(k, D("KAS")); }
                        }
                    }
                    else if (Yuru(k, dt)) { k.bekle = 1.1f; RafaDon(k); }
                    break;
                default:
                    if (Yuru(k, dt))
                    {
                        EsyaVer(k, 0);
                        kasiyerAsama = 0;
                        odemeSayaci = 1.2f;     // kısa bir poşetleme hareketi
                    }
                    break;
            }
        }

        /// <summary>Kurye: 0 tezgâha yürür · 1 poşeti bekler · 2 poşetle çıkar.</summary>
        void Kurye(Kisi k, float dt)
        {
            switch (k.asama)
            {
                case 0:
                    if (Yuru(k, dt)) { k.asama = 1; k.bekle = 14f; k.bakis = new Vector2(1f, 0f); }
                    break;
                case 1:
                    k.bakis = new Vector2(1f, 0f);
                    k.bekle -= dt;
                    if (alinanOlay > 0)
                    {
                        alinanOlay = 0;
                        EsyaVer(k, 2);
                        k.asama = 2;
                        YolVer(k, D("S1"));
                    }
                    else if (k.bekle <= 0f && !kuryeYakin)
                    {
                        // Gerçek kurye gitti (iptal, başka sipariş): eli boş çıkar
                        k.asama = 2;
                        YolVer(k, D("S1"));
                    }
                    break;
                default:
                    if (Yuru(k, dt)) KisiSil(k);
                    break;
            }
        }

        /// <summary>Şoför: 0 koliyle arka kapıdan içeri · 1 bırakır · 2 kamyonete döner; indirme bitince kaybolur.</summary>
        void Sofor(Kisi k, float dt)
        {
            switch (k.asama)
            {
                case 0:
                    if (Yuru(k, dt)) { k.asama = 1; k.bekle = 0.8f; }
                    break;
                case 1:
                    k.bekle -= dt;
                    if (k.bekle <= 0f) { EsyaVer(k, 0); k.asama = 2; YolVer(k, D("KV")); }
                    break;
                default:
                    if (Yuru(k, dt))
                    {
                        if (!tedarikSuruyor) { KisiSil(k); return; }
                        EsyaVer(k, 3);
                        k.asama = 0;
                        YolVer(k, D("AI"));
                    }
                    break;
            }
        }
    }
}
