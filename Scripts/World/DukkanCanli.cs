using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Bina Atölyesi binalarının canlı hâli (mahalle bakkalındaki BakkalCanli'nin genel biçimi). Prefabın kökündeki
    /// <see cref="AtolyePlani"/>'nı okur; City3D.AddPrefab çatısı kalkan (CatiKaldir) atölye binasına takar.
    /// Yalnızca görseldir: sipariş, para ve denge verisine dokunmaz, hiçbir şeyi kayda yazmaz.
    ///  • Saat: dükkânın gerçek saatleri (ShopHours) ya da türün varsayılan saatleri. Kapalıyken kepenk iner, tavan lambaları
    ///    söner, vitrin gece yanmaz (GeceIsiklari <see cref="IsikAcik"/>'a bakar); konut pencereleri GeceIsiklari'nin üç
    ///    grubuyla kendi saatlerinde yanıp söner.
    ///  • İnsanlar: kasada çalışan, türe göre müşteri / ziyaretçi (kapıdan girer, koridorlardan bir mobilyanın önüne gider,
    ///    kasaya uğrar, çıkar). Dükkânı olan binada gerçek siparişe bakılır: hazırlanırken çalışan uğraşır, hazır olunca
    ///    tezgâhta poşet durur, gerçek kurye yaklaşınca kurye figürü girip poşeti alır.
    ///  • Seçim: bina kartı açıkken çatı yakınlık aranmadan kalkar; çatı açıkken kameraya bakan zemin kat duvarları alçalır.
    ///  • "İçeri gir": kamera göz hizasına iner (harita kamerası o sırada kapalı); kartta küçük canlı pencere.
    /// Performans: bina ekranda yeterince büyük değilken insanlar simüle edilmez; durum 0,3 sn'de bir taranır.
    /// </summary>
    public class DukkanCanli : MonoBehaviour
    {
        /// <summary>Bina kartı açık olan dükkân / kare (GameUI ayarlar): o binanın çatısı açık tutulur.</summary>
        public static Shop SeciliDukkan;
        public static bool SeciliKareVar;
        public static Vector2Int SeciliKare;
        /// <summary>Kamerası içinde olan bina (yoksa null).</summary>
        public static DukkanCanli Iceride { get; private set; }
        static readonly List<DukkanCanli> hepsi = new List<DukkanCanli>();

        public static DukkanCanli Bul(Shop s)
        {
            if (s == null) return null;
            for (int i = 0; i < hepsi.Count; i++) if (hepsi[i] != null && hepsi[i].shop == s) return hepsi[i];
            return null;
        }

        public static DukkanCanli Bul(Vector2Int kare)
        {
            for (int i = 0; i < hepsi.Count; i++) if (hepsi[i] != null && hepsi[i].kare == kare) return hepsi[i];
            return null;
        }

        AtolyePlani plan;
        CatiKaldir cati;
        Shop shop;
        Vector2Int kare;
        Transform kok, canliKok;
        bool etkin, uyutBekliyor;
        float sonrakiKontrol;
        bool acik = true;
        float kepenkInik = -1f;
        readonly float[] duvarInik = new float[4], duvarHedef = new float[4];
        int[] onceki;                          // kapıdan her hücreye en kısa yol ağacı (hücre → bir önceki hücre; kapı −1; ulaşılmaz −2)
        readonly List<int> hedefler = new List<int>();   // mobilyalı hücreler (kasa hariç)
        Vector3 enAz, enCok;                   // zemin katın sınırları (model metresi)
        int hazirlanan, hazir;
        bool kuryeYakin;
        float hazirBekleme;
        Light icIsik;

        public AtolyePlani Plan => plan;
        public Shop Dukkan => shop;
        public bool Acik => acik;
        /// <summary>İçeride ışık yanıyor mu (GeceIsiklari vitrin camı için sorar).</summary>
        public bool IsikAcik => acik;

        // ---------------------------------------------------------------- kurulum

        public static DukkanCanli Tak(GameObject inst, Shop shop, Vector2Int kare, CatiKaldir ck)
        {
            if (inst == null || ck == null) return null;
            var p = inst.GetComponent<AtolyePlani>();
            if (p == null || p.kok == null || p.hucreOrta == null || p.hucreOrta.Length == 0 || p.kapiHucre < 0) return null;
            var d = inst.AddComponent<DukkanCanli>();
            d.Kur(p, shop, kare, ck);
            return d;
        }

        void Kur(AtolyePlani p, Shop s, Vector2Int k, CatiKaldir ck)
        {
            plan = p; shop = s; kare = k; cati = ck; kok = p.kok;
            if (plan.kepenk != null) plan.kepenk.gameObject.SetActive(false);
            if (plan.poset != null) plan.poset.gameObject.SetActive(false);
            canliKok = new GameObject(CatiKaldir.CanliAd).transform;
            canliKok.SetParent(kok, false);
            canliKok.gameObject.SetActive(false);

            int n = plan.hucreOrta.Length;
            float a = plan.hucre * 0.5f;
            enAz = new Vector3(float.MaxValue, float.MaxValue, 0f);
            enCok = new Vector3(float.MinValue, float.MinValue, plan.kat);
            for (int i = 0; i < n; i++)
            {
                Vector2 o = plan.hucreOrta[i];
                enAz.x = Mathf.Min(enAz.x, o.x - a); enAz.y = Mathf.Min(enAz.y, o.y - a);
                enCok.x = Mathf.Max(enCok.x, o.x + a); enCok.y = Mathf.Max(enCok.y, o.y + a);
                if (i != plan.kasaHucre && i < plan.mobilya.Length && !string.IsNullOrEmpty(plan.mobilya[i])) hedefler.Add(i);
            }
            // Yol ağacı: yanlara hep; arkaya (y + 1) yalnız öndeki hücre geçitse (mobilyalar hücrenin arka yarısında)
            onceki = new int[n];
            for (int i = 0; i < n; i++) onceki[i] = -2;
            var kuyruk = new Queue<int>();
            onceki[plan.kapiHucre] = -1;
            kuyruk.Enqueue(plan.kapiHucre);
            while (kuyruk.Count > 0)
            {
                int c = kuyruk.Dequeue();
                Vector2Int no = plan.hucreNo[c];
                for (int yon = 0; yon < 4; yon++)
                {
                    Vector2Int q = yon == 0 ? new Vector2Int(no.x - 1, no.y) : yon == 1 ? new Vector2Int(no.x + 1, no.y)
                                 : yon == 2 ? new Vector2Int(no.x, no.y + 1) : new Vector2Int(no.x, no.y - 1);
                    int qi = plan.HucreBul(q);
                    if (qi < 0 || onceki[qi] != -2) continue;
                    bool gec = yon < 2 || (yon == 2 ? plan.gecit[c] : plan.gecit[qi]);
                    if (!gec) continue;
                    onceki[qi] = c;
                    kuyruk.Enqueue(qi);
                }
            }
            hedefler.RemoveAll(h => onceki[h] == -2);
            hepsi.Add(this);
            sonrakiKontrol = Time.unscaledTime + Random.value * 0.3f;
        }

        void OnDestroy()
        {
            hepsi.Remove(this);
            if (Iceride == this) Cik();
            PencereBirak();
        }

        void OnDisable()
        {
            if (Iceride == this) Cik();
            etkin = false;
            uyutBekliyor = true;
        }

        // ---------------------------------------------------------------- dönüşüm

        Vector3 Yerel(Vector3 m) => AtolyePlani.Yerel(m);
        Vector3 Dunya(Vector3 m) => kok.TransformPoint(AtolyePlani.Yerel(m));
        /// <summary>1 metrenin dünyadaki boyu.</summary>
        float Birim => kok.TransformVector(Vector3.right).magnitude;

        // ---------------------------------------------------------------- saat

        /// <summary>Dükkânı olmayan binalar için türün varsayılan saatleri (yalnız görsel).</summary>
        static bool TurAcik(string tur, string grup, float saat, bool haftaSonu)
        {
            switch (tur)
            {
                case "hastane": case "polis": case "itfaiye": case "otel": case "gar": case "bakkal24": case "market24": case "gyros": case "burger": case "mcdanolds":
                    return true;
                case "pub": return saat >= 12f || saat < 2f;
                case "bistro": case "pizza": case "kfg": case "sushi": case "balik": case "doner": return saat >= 11f && saat < 23f;
                case "kafe": return saat >= 7f && saat < 21f;
                case "firin": return saat >= 6f && saat < 19f;
                case "bufe": return saat >= 5f && saat < 23f;
                case "avm": return saat >= 10f && saat < 22f;
                case "pazar": return saat >= 6f && saat < 15f;
                case "opera": return saat >= 17f && saat < 23f;
                case "kutuphane": return saat >= 9f && saat < 20f;
            }
            switch (grup)
            {
                case "Konut": return true;
                case "Market": return saat >= 7f && saat < 21f;
                case "Kamu": return !haftaSonu && saat >= 8f && saat < 17f;
                case "Sanayi": return !haftaSonu && saat >= 7f && saat < 18f;
                default: return saat >= 9f && saat < 20f;
            }
        }

        bool AcikMi()
        {
            if (shop != null) return ShopHours.IsOpen(shop.def);
            var clk = GameClock.I;
            if (clk == null) return true;
            return TurAcik(plan.tur, plan.turGrup, DayNight.HourF, clk.IsWeekend);
        }

        // ---------------------------------------------------------------- döngü

        void Update()
        {
            if (plan == null || kok == null) return;
            float simdi = Time.unscaledTime;
            if (uyutBekliyor) { uyutBekliyor = false; Uyut(); }
            if (simdi >= sonrakiKontrol)
            {
                sonrakiKontrol = simdi + 0.3f;
                Kontrol();
            }
            PencereGuncelle(simdi);
            if (!etkin) return;
            var clk = GameClock.I;
            bool durdu = clk == null || clk.Paused || clk.EventPause;
            float gercek = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float dt = durdu ? 0f : gercek * Mathf.Clamp(clk.EffectiveSpeed, 1f, 2.5f);
            InsanlariGuncelle(dt);
            ParcalariGuncelle(gercek);
        }

        void LateUpdate()
        {
            if (Iceride == this) IceriKamera();
        }

        /// <summary>Durum taraması (0,3 sn'de bir): saat, kepenk, görünürlük, seçim, siparişler.</summary>
        void Kontrol()
        {
            bool secili = (shop != null && SeciliDukkan == shop) || (SeciliKareVar && SeciliDukkan == null && SeciliKare == kare);
            bool iceride = Iceride == this;
            cati.zorlaAcik = secili && !iceride;
            if (!secili && !iceride) PencereBirak();
            acik = AcikMi();

            bool yakin = cati.SonBoy >= 0.09f && cati.SonMerkez < 0.95f;
            bool yeni = iceride || cati.Acik || cati.IcGorunur || yakin;
            if (MapCamera.I != null && MapCamera.I.Tilt01 < 0.9f && !iceride) yeni = false;
            if (yeni != etkin)
            {
                etkin = yeni;
                if (etkin) Uyandir(); else Uyut();
            }
            if (!etkin)
            {
                // Uzakta: kepenk canlandırmasız yerine oturur (LOD0 görünüyorsa doğru dursun)
                KepenkUygula(acik ? 0f : 1f, false);
                if (plan.ampul != null && plan.ampul.gameObject.activeSelf != acik) plan.ampul.gameObject.SetActive(acik);
                return;
            }
            SiparisTara();
            DuvarHedefleri(iceride);
        }

        void Uyandir()
        {
            canliKok.gameObject.SetActive(true);
            kepenkInik = -1f;
            InsanlariBaslat();
        }

        void Uyut()
        {
            if (canliKok != null) canliKok.gameObject.SetActive(false);
            InsanlariTopla();
            hazir = hazirlanan = 0;
            if (plan == null) return;
            if (plan.poset != null) plan.poset.gameObject.SetActive(false);
            for (int i = 0; i < 4; i++)
            {
                duvarInik[i] = duvarHedef[i] = 0f;
                var d = plan.duvar != null && i < plan.duvar.Length ? plan.duvar[i] : null;
                if (d != null) d.localScale = Vector3.one;
            }
            if (icIsik != null) icIsik.enabled = false;
        }

        // ---------------------------------------------------------------- siparişler (gerçek durum)

        void SiparisTara()
        {
            hazirlanan = hazir = 0;
            kuryeYakin = false;
            if (shop == null || !plan.siparis) return;
            var clk = GameClock.I;
            var om = OrderManager.I;
            if (clk == null || om == null) return;
            float now = clk.TotalMinutes;
            Tara(om.Pending, now);
            Tara(om.Active, now);
            var cm = CourierManager.I;
            if (cm == null) return;
            Vector2 kapiYeri = shop.door;
            for (int i = 0; i < cm.Couriers.Count; i++)
            {
                var c = cm.Couriers[i];
                if (c == null) continue;
                if (c.State != CourierState.DukkanaGidiyor && c.State != CourierState.Bekliyor) continue;
                var co = c.CurrentOrder;
                if (co == null || co.shop != shop) continue;
                if ((c.Position - kapiYeri).sqrMagnitude < 2.25f) { kuryeYakin = true; break; }
            }
        }

        void Tara(List<Order> liste, float now)
        {
            for (int i = 0; i < liste.Count; i++)
            {
                var o = liste[i];
                if (o == null || o.shop != shop) continue;
                if (o.status != OrderStatus.Bekliyor && o.status != OrderStatus.Atandi) continue;
                if (now < o.readyAt) hazirlanan++; else hazir++;
            }
        }

        // ---------------------------------------------------------------- parçalar: duvar, kepenk, ışık, poşet

        static readonly Vector3[] DuvarNormal = { new Vector3(0f, -1f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(-1f, 0f, 0f) };
        const float DuvarAlcak = 0.3f;       // inen duvarın kalan boyu (oran): diz hizasında bir parapet

        void DuvarHedefleri(bool iceride)
        {
            var cam = Camera.main;
            Vector2 kameraya = Vector2.zero;
            if (cam != null && !iceride && cati.IcGorunur)
            {
                Vector3 f = cam.transform.forward;
                kameraya = new Vector2(-f.x, -f.y);
                kameraya = kameraya.magnitude < 0.08f ? Vector2.zero : kameraya.normalized;   // tam tepeden bakışta duvar inmez
            }
            for (int i = 0; i < 4; i++)
            {
                float hedef = 0f;
                if (kameraya != Vector2.zero)
                {
                    Vector3 nrm = kok.TransformDirection(Yerel(DuvarNormal[i]));
                    var n2 = new Vector2(nrm.x, nrm.y);
                    if (n2.sqrMagnitude > 1e-10f && Vector2.Dot(n2.normalized, kameraya) > 0.22f) hedef = 1f;
                }
                duvarHedef[i] = hedef;
            }
        }

        void KepenkUygula(float inik, bool onDuvarInik)
        {
            if (plan.kepenk == null) return;
            bool gizli = inik < 0.03f || onDuvarInik;
            if (plan.kepenk.gameObject.activeSelf == gizli) plan.kepenk.gameObject.SetActive(!gizli);
            if (!gizli) plan.kepenk.localScale = new Vector3(1f, Mathf.Max(0.03f, inik), 1f);   // kökü üstte: yukarı sarılır
        }

        void ParcalariGuncelle(float gercek)
        {
            for (int i = 0; i < 4; i++)
            {
                var d = plan.duvar != null && i < plan.duvar.Length ? plan.duvar[i] : null;
                if (d == null) continue;
                if (Mathf.Approximately(duvarInik[i], duvarHedef[i])) continue;
                duvarInik[i] = Mathf.MoveTowards(duvarInik[i], duvarHedef[i], gercek * 3f);
                float s = Mathf.Lerp(1f, DuvarAlcak, Mathf.SmoothStep(0f, 1f, duvarInik[i]));
                d.localScale = new Vector3(1f, s, 1f);
            }

            float kh = acik ? 0f : 1f;
            kepenkInik = kepenkInik < 0f ? kh : Mathf.MoveTowards(kepenkInik, kh, gercek * 0.7f);
            KepenkUygula(kepenkInik, duvarInik[0] > 0.5f);
            if (plan.ampul != null && plan.ampul.gameObject.activeSelf != acik) plan.ampul.gameObject.SetActive(acik);

            // Poşet: hazır sipariş varken tezgâhta (birkaç saniye sonra: çalışan hazırlamış gibi)
            bool poset = false;
            if (hazir > 0) { hazirBekleme += gercek; poset = hazirBekleme > 1.5f && !kuryeAldi; }
            else { hazirBekleme = 0f; kuryeAldi = false; }
            if (plan.poset != null && plan.poset.gameObject.activeSelf != poset) plan.poset.gameObject.SetActive(poset);

            // İç ışık: içi görünürken ve hava karanlıkken (ya da kamera içerideyken) yumuşak bir tavan ışığı
            bool isik = acik && (Iceride == this || (cati.IcGorunur && DayNight.Night01 > 0.25f));
            if (isik && icIsik == null)
            {
                var go = new GameObject("İç ışık");
                go.transform.SetParent(kok, false);
                go.transform.localPosition = Yerel(new Vector3((enAz.x + enCok.x) * 0.5f, (enAz.y + enCok.y) * 0.5f, plan.kat - 0.5f));
                icIsik = go.AddComponent<Light>();
                icIsik.type = LightType.Point;
                icIsik.color = new Color(1f, 0.93f, 0.8f);
                icIsik.shadows = LightShadows.None;
            }
            if (icIsik != null)
            {
                if (isik)
                {
                    // Dünya çok küçük (1 m ≈ 0,06 birim): şiddet metre karesine göre ölçeklenmezse her şey beyaza patlar
                    float birim = Birim;
                    float yari = Mathf.Max(enCok.x - enAz.x, enCok.y - enAz.y) * 0.5f;
                    icIsik.range = (6f + yari * 1.5f) * birim;
                    icIsik.intensity = (2f + yari * 0.35f) * birim * birim;
                }
                if (icIsik.enabled != isik) icIsik.enabled = isik;
            }
        }
        bool kuryeAldi;

        // ---------------------------------------------------------------- insanlar

        enum Rol { Musteri, Calisan, Kurye }

        class Kisi
        {
            public Rol rol;
            public Transform kok, kolSol, kolSag, bacakSol, bacakSag;
            public GameObject esya;
            public Vector3 yer;                 // model uzayı (metre)
            public Vector2 bakis = new Vector2(0f, 1f);
            public readonly List<Vector3> yol = new List<Vector3>();
            public int asama, hucre = -1, kalanDurak;
            public float bekle, faz;
            public bool yuruyor, uzaniyor;
        }

        readonly List<Kisi> kisiler = new List<Kisi>();
        float musteriSayaci, calisanSayaci;
        Kisi kurye;
        const float YuruHizi = 1.4f;

        public int MusteriSayisi
        {
            get
            {
                int n = 0;
                for (int i = 0; i < kisiler.Count; i++) if (kisiler[i].rol == Rol.Musteri) n++;
                return n;
            }
        }

        static Mesh kupMesh, kureMesh;
        static readonly Dictionary<int, Material> insanMalzeme = new Dictionary<int, Material>();
        static readonly Color[] UstRenk =
        {
            new Color32(0xd9, 0x4f, 0x3d, 255), new Color32(0xe8, 0xa2, 0x3a, 255), new Color32(0x5a, 0x9a, 0x4a, 255), new Color32(0x2f, 0x7d, 0x8c, 255),
            new Color32(0x8a, 0x4a, 0x6b, 255), new Color32(0x7a, 0xb8, 0xd6, 255), new Color32(0xf2, 0xd0, 0x4a, 255), new Color32(0xf4, 0xf0, 0xe6, 255),
        };
        static readonly Color[] AltRenk = { new Color32(0x3d, 0x5a, 0x8a, 255), new Color32(0x4a, 0x4d, 0x52, 255), new Color32(0x6b, 0x45, 0x2a, 255), new Color32(0x2b, 0x2d, 0x31, 255) };
        static readonly Color[] SacRenk = { new Color32(0x2b, 0x2d, 0x31, 255), new Color32(0x4a, 0x2f, 0x20, 255), new Color32(0xc9, 0xa2, 0x4a, 255), new Color32(0x8a, 0x5a, 0x3a, 255), new Color32(0xb8, 0xb8, 0xb4, 255) };
        static readonly Color[] TenRenk = { new Color32(0xe8, 0xb8, 0x90, 255), new Color32(0xf0, 0xc8, 0xa0, 255), new Color32(0xc6, 0x8e, 0x62, 255), new Color32(0x8d, 0x5a, 0x3a, 255) };

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

        static Material Boya(Color c)
        {
            Color32 c32 = c;
            int anahtar = (c32.r << 16) | (c32.g << 8) | c32.b;
            if (insanMalzeme.TryGetValue(anahtar, out var m) && m != null) return m;
            var sh = Shader.Find("KuryeTycoon/Pastel");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh) { name = "Insan_" + anahtar.ToString("x6") };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            m.enableInstancing = true;
            insanMalzeme[anahtar] = m;
            return m;
        }

        static GameObject Parcacik(Transform ust, string ad, Mesh mesh, Vector3 yer, Vector3 olcek, Color renk)
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

        static Transform Uzuv(Transform ust, string ad, Vector3 mafsal, Vector3 olcek, Color renk, Color uc)
        {
            var m = new GameObject(ad).transform;
            m.SetParent(ust, false);
            m.localPosition = mafsal;
            Parcacik(m, "parça", kupMesh, new Vector3(0f, -olcek.y * 0.5f, 0f), olcek, renk);
            Parcacik(m, "uç", kupMesh, new Vector3(0f, -olcek.y - 0.03f, 0.02f), new Vector3(olcek.x * 1.05f, 0.08f, olcek.z * 1.25f), uc);
            return m;
        }

        /// <summary>Figür: kökü ayak tabanında, kendi uzayında +Z ileri, +Y yukarı, ölçü metre.</summary>
        Kisi KisiKur(Rol rol)
        {
            MeshleriHazirla();
            var k = new Kisi { rol = rol };
            var kk = new GameObject(rol.ToString()).transform;
            kk.SetParent(canliKok, false);
            k.kok = kk;
            Color ust = UstRenk[Random.Range(0, UstRenk.Length)], alt = AltRenk[Random.Range(0, AltRenk.Length)];
            Color sac = SacRenk[Random.Range(0, SacRenk.Length)], ten = TenRenk[Random.Range(0, TenRenk.Length)];
            Color ayakkabi = new Color32(0x3a, 0x2a, 0x22, 255);
            if (rol == Rol.Calisan) { ust = new Color32(0xf4, 0xf0, 0xe6, 255); alt = AltRenk[0]; }
            else if (rol == Rol.Kurye) { ust = CompanyProfile.Primary; alt = AltRenk[3]; }
            k.bacakSol = Uzuv(kk, "bacak sol", new Vector3(-0.09f, 0.82f, 0f), new Vector3(0.14f, 0.76f, 0.17f), alt, ayakkabi);
            k.bacakSag = Uzuv(kk, "bacak sağ", new Vector3(0.09f, 0.82f, 0f), new Vector3(0.14f, 0.76f, 0.17f), alt, ayakkabi);
            Parcacik(kk, "gövde", kupMesh, new Vector3(0f, 1.11f, 0f), new Vector3(0.42f, 0.62f, 0.24f), ust);
            k.kolSol = Uzuv(kk, "kol sol", new Vector3(-0.27f, 1.38f, 0f), new Vector3(0.1f, 0.5f, 0.1f), ust, ten);
            k.kolSag = Uzuv(kk, "kol sağ", new Vector3(0.27f, 1.38f, 0f), new Vector3(0.1f, 0.5f, 0.1f), ust, ten);
            Parcacik(kk, "boyun", kupMesh, new Vector3(0f, 1.45f, 0f), new Vector3(0.12f, 0.08f, 0.12f), ten);
            Parcacik(kk, "baş", kureMesh, new Vector3(0f, 1.62f, 0f), new Vector3(0.28f, 0.3f, 0.28f), ten);
            Parcacik(kk, "saç", kureMesh, new Vector3(0f, 1.69f, -0.03f), new Vector3(0.3f, 0.22f, 0.3f), sac);
            if (rol == Rol.Calisan) Parcacik(kk, "önlük", kupMesh, new Vector3(0f, 0.95f, 0.125f), new Vector3(0.34f, 0.72f, 0.02f), Vurgu());
            if (rol == Rol.Kurye) Parcacik(kk, "çanta", kupMesh, new Vector3(0f, 1.15f, -0.27f), new Vector3(0.4f, 0.48f, 0.28f), CompanyProfile.Primary);
            kisiler.Add(k);
            return k;
        }

        /// <summary>Çalışan önlüğü: dükkânın rengi (yoksa koyu yeşil).</summary>
        Color Vurgu() => shop != null && shop.def != null ? shop.def.color : (Color)new Color32(0x2f, 0x6b, 0x57, 255);

        void PosetVer(Kisi k)
        {
            if (k.esya != null) return;
            k.esya = Parcacik(k.kolSag, "poşet", kupMesh, new Vector3(0f, -0.72f, 0.04f), new Vector3(0.17f, 0.32f, 0.26f), new Color32(0xc4, 0x9a, 0x6c, 255));
        }

        void KisiSil(Kisi k)
        {
            kisiler.Remove(k);
            if (k.kok != null) Destroy(k.kok.gameObject);
            k.kok = null;
            if (kurye == k) kurye = null;
        }

        void Ciz(Kisi k, float dt)
        {
            if (k.kok == null) return;
            k.kok.localPosition = Yerel(k.yer);
            Vector3 ileri = Yerel(new Vector3(k.bakis.x, k.bakis.y, 0f));
            if (ileri.sqrMagnitude > 1e-12f) k.kok.localRotation = Quaternion.LookRotation(ileri.normalized, Vector3.up);
            float sal = 0f;
            if (k.yuruyor) { k.faz += dt * 7.5f; sal = Mathf.Sin(k.faz) * 28f; }
            else if (!k.uzaniyor) k.faz = 0f;
            k.bacakSol.localRotation = Quaternion.Euler(sal, 0f, 0f);
            k.bacakSag.localRotation = Quaternion.Euler(-sal, 0f, 0f);
            float solKol = -sal * 0.8f, sagKol = sal * 0.8f;
            if (k.uzaniyor && !k.yuruyor)
            {
                k.faz += dt * 5f;
                sagKol = -70f - Mathf.Sin(k.faz) * 18f;
            }
            else if (k.esya != null) sagKol = k.yuruyor ? sal * 0.25f : 0f;
            k.kolSol.localRotation = Quaternion.Euler(solKol, 0f, 0f);
            k.kolSag.localRotation = Quaternion.Euler(sagKol, 0f, 0f);
        }

        /// <summary>Yol boyunca yürütür. Yol bittiyse true.</summary>
        bool Yuru(Kisi k, float dt)
        {
            k.yuruyor = false;
            float kalan = YuruHizi * dt;
            while (k.yol.Count > 0 && kalan > 0f)
            {
                Vector3 fark = k.yol[0] - k.yer;
                float d = fark.magnitude;
                if (d > 1e-4f)
                {
                    var yatay = new Vector2(fark.x, fark.y);
                    if (yatay.sqrMagnitude > 1e-6f) k.bakis = yatay.normalized;
                    k.yuruyor = true;
                }
                if (d <= kalan) { k.yer = k.yol[0]; kalan -= d; k.yol.RemoveAt(0); }
                else { k.yer += fark / d * kalan; kalan = 0f; }
            }
            return k.yol.Count == 0;
        }

        readonly List<int> zincir = new List<int>();

        /// <summary>Kapıdan hücreye giden hücre zinciri (kapı dahil, sırayla).</summary>
        void Zincir(int hucre)
        {
            zincir.Clear();
            for (int c = hucre, guard = 0; c >= 0 && guard < 64; c = onceki[c], guard++) zincir.Insert(0, c);
        }

        /// <summary>k'nin bulunduğu hücreden (−1: dışarıda) hedef hücrenin koridoruna yol yazar.</summary>
        void YolVer(Kisi k, int hedef, Vector2 kayma)
        {
            k.yol.Clear();
            if (k.hucre >= 0)
            {
                // ortak ataya kadar geri, sonra hedefe ileri
                Zincir(k.hucre);
                var geri = new List<int>(zincir);
                Zincir(hedef);
                int ortak = 0;
                while (ortak < geri.Count && ortak < zincir.Count && geri[ortak] == zincir[ortak]) ortak++;
                for (int i = geri.Count - 2; i >= ortak - 1 && i >= 0; i--) k.yol.Add(plan.Koridor(geri[i]));
                for (int i = ortak; i < zincir.Count; i++) k.yol.Add(plan.Koridor(zincir[i]));
            }
            else
            {
                Zincir(hedef);
                k.yol.Add(plan.Esik);
                for (int i = 0; i < zincir.Count; i++) k.yol.Add(plan.Koridor(zincir[i]));
            }
            if (k.yol.Count > 0) k.yol[k.yol.Count - 1] += new Vector3(kayma.x, kayma.y, 0f);
            else k.yol.Add(plan.Koridor(hedef) + new Vector3(kayma.x, kayma.y, 0f));
            k.hucre = hedef;
        }

        void CikisYolu(Kisi k)
        {
            k.yol.Clear();
            if (k.hucre >= 0)
            {
                Zincir(k.hucre);
                for (int i = zincir.Count - 2; i >= 0; i--) k.yol.Add(plan.Koridor(zincir[i]));
            }
            k.yol.Add(plan.Esik);
            k.yol.Add(plan.Giris + new Vector3(Random.Range(-1.2f, 1.2f), -0.6f, 0f));
            k.hucre = -1;
        }

        void InsanlariBaslat()
        {
            InsanlariTopla();
            musteriSayaci = 0.5f;
            if (!acik) return;
            CalisanlariKur();
            // Ekrana gelindiğinde içerisi boş görünmesin: birkaç müşteri hemen bir mobilyanın önünde
            int n = Mathf.Min(MusteriHedefi(), 2);
            for (int i = 0; i < n && hedefler.Count > 0; i++)
            {
                var m = KisiKur(Rol.Musteri);
                int h = hedefler[Random.Range(0, hedefler.Count)];
                m.hucre = h;
                m.yer = plan.Koridor(h) + new Vector3(Random.Range(-0.7f, 0.7f), Random.Range(-0.15f, 0.15f), 0f);
                m.bakis = new Vector2(0f, 1f);
                m.asama = 1;
                m.bekle = Random.Range(2f, 7f);
                m.kalanDurak = Random.Range(0, 2);
                m.uzaniyor = Random.value < 0.5f;
            }
        }

        void CalisanlariKur()
        {
            int n = Mathf.Clamp(plan.calisan, 0, 3);
            for (int i = 0; i < n; i++)
            {
                var c = KisiKur(Rol.Calisan);
                if (i == 0 && plan.kasaHucre >= 0)
                {
                    c.yer = plan.KasaArkasi;
                    c.bakis = new Vector2(0f, -1f);
                    c.hucre = plan.kasaHucre;
                    c.asama = 0;                       // kasada
                }
                else if (hedefler.Count > 0)
                {
                    int h = hedefler[(i * 7 + 1) % hedefler.Count];
                    c.hucre = h;
                    c.yer = plan.Koridor(h) + new Vector3(0.5f - i * 0.5f, 0.2f, 0f);
                    c.bakis = new Vector2(0f, 1f);
                    c.asama = 1;                       // gezici: mobilyaların önünde çalışır
                    c.bekle = Random.Range(3f, 8f);
                    c.uzaniyor = true;
                }
                else { KisiSil(c); break; }
            }
        }

        void InsanlariTopla()
        {
            for (int i = kisiler.Count - 1; i >= 0; i--)
                if (kisiler[i].kok != null) Destroy(kisiler[i].kok.gameObject);
            kisiler.Clear();
            kurye = null;
        }

        /// <summary>Türe ve saate göre içeride olması beklenen müşteri / ziyaretçi sayısı.</summary>
        int MusteriHedefi()
        {
            var clk = GameClock.I;
            if (clk == null || !acik || hedefler.Count == 0) return 0;
            if (plan.turGrup == "Sanayi") return 0;
            int ust = Mathf.Min(5, hedefler.Count + 1);
            if (plan.turGrup == "Konut")
            {
                // ev halkı: sabah ve akşam evde
                int h = clk.Hour;
                return (h >= 18 || h < 9) ? Mathf.Min(2, ust) : (clk.IsWeekend ? 1 : 0);
            }
            if (shop != null)
            {
                float w = OrderManager.DemandCurve(shop.def, clk.Hour, clk.IsWeekend);
                return Mathf.Clamp(Mathf.RoundToInt(w * 2.4f + (clk.Hour >= 7 && clk.Hour < 22 ? 0.6f : 0f)), 0, ust);
            }
            int saat = clk.Hour;
            float yogun = (saat >= 11 && saat < 14) || (saat >= 17 && saat < 20) ? 1f : (saat >= 8 && saat < 22 ? 0.6f : 0.25f);
            return Mathf.Clamp(Mathf.RoundToInt(yogun * Mathf.Min(4, hedefler.Count)), 0, ust);
        }

        void InsanlariGuncelle(float dt)
        {
            bool calisanVar = false;
            for (int i = 0; i < kisiler.Count; i++) if (kisiler[i].rol == Rol.Calisan) { calisanVar = true; break; }
            if (acik && !calisanVar && plan.calisan > 0)
            {
                calisanSayaci -= dt;
                if (calisanSayaci <= 0f) { calisanSayaci = 2f; CalisanlariKur(); }
            }

            // Yeni müşteri: kapıdan girer
            musteriSayaci -= dt;
            if (musteriSayaci <= 0f)
            {
                musteriSayaci = Random.Range(3f, 7f);
                if (MusteriSayisi < MusteriHedefi())
                {
                    var m = KisiKur(Rol.Musteri);
                    m.yer = plan.Giris + new Vector3(Random.Range(-1.5f, 1.5f), -0.8f, 0f);
                    m.kalanDurak = Random.Range(1, 3);
                    m.asama = 0;
                    YolVer(m, hedefler[Random.Range(0, hedefler.Count)], new Vector2(Random.Range(-0.6f, 0.6f), 0f));
                }
            }

            // Kurye figürü: gerçek kurye kapıya yaklaşınca ve hazır poşet varken girer
            if (kurye == null && kuryeYakin && hazir > 0 && plan.kasaHucre >= 0 && acik && !kuryeAldi)
            {
                kurye = KisiKur(Rol.Kurye);
                kurye.yer = plan.Giris + new Vector3(0.6f, -0.9f, 0f);
                kurye.asama = 0;
                YolVer(kurye, plan.kasaHucre, new Vector2(-0.4f, 0f));
            }

            for (int i = kisiler.Count - 1; i >= 0; i--)
            {
                var k = kisiler[i];
                switch (k.rol)
                {
                    case Rol.Musteri: Musteri(k, dt); break;
                    case Rol.Calisan: Calisan(k, dt); break;
                    default: Kurye(k, dt); break;
                }
                if (k.kok != null) Ciz(k, dt);
            }
        }

        // aşamalar: 0 yürüyor (hedefe) · 1 mobilyanın önünde · 2 kasaya yürüyor · 3 kasada · 4 çıkıyor
        void Musteri(Kisi m, float dt)
        {
            if (!acik && m.asama < 4) { m.asama = 4; m.uzaniyor = false; CikisYolu(m); }
            switch (m.asama)
            {
                case 0:
                    if (Yuru(m, dt)) { m.asama = 1; m.bekle = Random.Range(3f, 8f); m.bakis = new Vector2(0f, 1f); m.uzaniyor = Random.value < 0.6f; }
                    break;
                case 1:
                    m.yuruyor = false;
                    m.bekle -= dt;
                    if (m.bekle > 0f) break;
                    m.uzaniyor = false;
                    if (m.kalanDurak > 0 && hedefler.Count > 1)
                    {
                        m.kalanDurak--;
                        m.asama = 0;
                        YolVer(m, hedefler[Random.Range(0, hedefler.Count)], new Vector2(Random.Range(-0.6f, 0.6f), 0f));
                    }
                    else if (plan.kasaHucre >= 0 && plan.turGrup != "Konut")
                    {
                        m.asama = 2;
                        YolVer(m, plan.kasaHucre, new Vector2(Random.Range(0.2f, 0.7f), Random.Range(-0.3f, 0f)));
                    }
                    else if (plan.turGrup == "Konut") { m.kalanDurak = 2; m.bekle = Random.Range(4f, 10f); }   // ev halkı çıkmaz, dolaşır
                    else { m.asama = 4; CikisYolu(m); }
                    break;
                case 2:
                    if (Yuru(m, dt)) { m.asama = 3; m.bekle = Random.Range(2.5f, 5f); m.bakis = new Vector2(0f, 1f); m.uzaniyor = true; }
                    break;
                case 3:
                    m.yuruyor = false;
                    m.bekle -= dt;
                    if (m.bekle > 0f) break;
                    m.uzaniyor = false;
                    if (plan.siparis) PosetVer(m);
                    m.asama = 4;
                    CikisYolu(m);
                    break;
                default:
                    if (Yuru(m, dt)) KisiSil(m);
                    break;
            }
        }

        void Calisan(Kisi c, float dt)
        {
            if (!acik) { KisiSil(c); return; }
            if (c.asama == 0)
            {
                // kasada: hazırlanan sipariş ya da kasada müşteri varken uğraşır
                c.yuruyor = false;
                bool mesgul = hazirlanan > 0;
                for (int i = 0; i < kisiler.Count && !mesgul; i++) mesgul = kisiler[i].rol != Rol.Calisan && kisiler[i].asama == 3;
                c.uzaniyor = mesgul;
                c.bakis = new Vector2(0f, -1f);
                return;
            }
            // gezici çalışan: mobilyadan mobilyaya
            if (c.yol.Count > 0)
            {
                c.uzaniyor = false;
                if (Yuru(c, dt)) { c.bekle = Random.Range(4f, 9f); c.bakis = new Vector2(0f, 1f); c.uzaniyor = true; }
                return;
            }
            c.yuruyor = false;
            c.bekle -= dt;
            if (c.bekle <= 0f && hedefler.Count > 1) YolVer(c, hedefler[Random.Range(0, hedefler.Count)], new Vector2(Random.Range(-0.5f, 0.5f), 0.15f));
            else if (c.bekle <= 0f) c.bekle = Random.Range(4f, 9f);
        }

        // kurye: 0 kasaya yürüyor · 1 bekliyor · 2 çıkıyor
        void Kurye(Kisi k, float dt)
        {
            switch (k.asama)
            {
                case 0:
                    if (Yuru(k, dt)) { k.asama = 1; k.bekle = 1.6f; k.bakis = new Vector2(0f, 1f); k.uzaniyor = true; }
                    break;
                case 1:
                    k.yuruyor = false;
                    k.bekle -= dt;
                    if (k.bekle > 0f) break;
                    k.uzaniyor = false;
                    PosetVer(k);
                    kuryeAldi = true;              // poşet tezgâhtan kalkar (gerçek sipariş Alindi olana kadar yeniden konmaz)
                    k.asama = 2;
                    CikisYolu(k);
                    break;
                default:
                    if (Yuru(k, dt)) KisiSil(k);
                    break;
            }
        }

        // ---------------------------------------------------------------- kart için özet

        public struct Ozet
        {
            public int bugun, hazirlaniyor, hazir, yolda, teslim, musteri, calisan;
            public long kazanc;
            public bool acik, simule, siparis;
        }

        public Ozet OzetAl()
        {
            var z = new Ozet { acik = AcikMi(), simule = etkin, musteri = MusteriSayisi, siparis = shop != null && plan.siparis };
            for (int i = 0; i < kisiler.Count; i++) if (kisiler[i].rol == Rol.Calisan) z.calisan++;
            var clk = GameClock.I;
            var om = OrderManager.I;
            if (!z.siparis || clk == null || om == null) return z;
            float now = clk.TotalMinutes;
            float gunBasi = now - now % 1440f;
            foreach (var o in om.Pending)
            {
                if (o.shop != shop) continue;
                if (o.createdAt >= gunBasi) z.bugun++;
                if (now < o.readyAt) z.hazirlaniyor++; else z.hazir++;
            }
            foreach (var o in om.Active)
            {
                if (o.shop != shop) continue;
                if (o.createdAt >= gunBasi) z.bugun++;
                if (o.status == OrderStatus.Alindi) z.yolda++;
                else if (now < o.readyAt) z.hazirlaniyor++;
                else z.hazir++;
            }
            int bakilan = 0;
            foreach (var o in om.History)
            {
                if (++bakilan > 800) break;
                if (o.shop != shop || o.createdAt < gunBasi) continue;
                z.bugun++;
                if (o.status == OrderStatus.Teslim) { z.teslim++; z.kazanc += o.CompanyIncome; }
            }
            return z;
        }

        // ---------------------------------------------------------------- "İçeri gir" kamerası

        public static readonly string[] BakisAd = { "Giriş", "Kasa", "Arka", "Tepeden" };
        int bakis;
        float bakisYaw, bakisPitch;
        Vector3 bakisGoz;
        public int Bakis => bakis;
        public int BakisSayisi => plan != null && plan.kasaHucre >= 0 ? 4 : 3;
        /// <summary>i. bakışın adı (kasası olmayan binada "Kasa" atlanır).</summary>
        public string BakisAdi(int i) => BakisAd[BakisNo(i)];
        int BakisNo(int i) => plan.kasaHucre >= 0 ? i : (i >= 1 ? i + 1 : i);

        public void IceriGir()
        {
            if (Iceride == this) return;
            IceriBakis.Cik();
            var mc = MapCamera.I;
            if (mc == null || Camera.main == null) return;
            Iceride = this;
            mc.enabled = false;                 // harita kamerası kamerayı yönetmesin; çıkınca kendi durumuna döner
            cati.iceriModu = true;
            cati.zorlaAcik = false;
            PencereBirak();
            BakisSec(0);
            sonrakiKontrol = 0f;
        }

        public void Cik()
        {
            if (Iceride != this) return;
            Iceride = null;
            if (cati != null) cati.iceriModu = false;
            if (MapCamera.I != null) MapCamera.I.enabled = true;
            sonrakiKontrol = 0f;
        }

        public void BakisSec(int i)
        {
            int n = BakisSayisi;
            bakis = ((i % n) + n) % n;
            Vector3 goz, hedef;
            Vector3 kapi = plan.Koridor(plan.kapiHucre);
            Vector3 orta = new Vector3((enAz.x + enCok.x) * 0.5f, (enAz.y + enCok.y) * 0.5f, 1.1f);
            switch (BakisNo(bakis))
            {
                case 1:
                {
                    // kasanın önünden, biraz yandan: tezgâh ve arkasındaki çalışan
                    Vector2 k = plan.hucreOrta[plan.kasaHucre];
                    float yan = k.x >= kapi.x ? -1f : 1f;
                    goz = new Vector3(k.x + yan * 1.0f, k.y - 1.15f, 1.55f);
                    hedef = new Vector3(k.x, k.y + 0.7f, 1.15f);
                    break;
                }
                case 2:
                {
                    // kapıya en uzak hücrenin koridorundan geriye, içeriye doğru
                    int uzak = plan.kapiHucre;
                    float en = -1f;
                    for (int h = 0; h < plan.hucreOrta.Length; h++)
                    {
                        if (onceki[h] == -2) continue;
                        float d = (plan.hucreOrta[h] - plan.hucreOrta[plan.kapiHucre]).sqrMagnitude;
                        if (d > en) { en = d; uzak = h; }
                    }
                    goz = plan.Koridor(uzak) + new Vector3(0f, -0.3f, 1.6f);
                    hedef = uzak == plan.kapiHucre ? orta + new Vector3(0f, 1f, 0f) : new Vector3(kapi.x, kapi.y, 1.2f);
                    break;
                }
                case 3:
                    // ön sol köşeden, tavana yakın: bütün zemin kat
                    goz = new Vector3(enAz.x + 0.45f, enAz.y + 0.45f, plan.kat - 0.45f);
                    hedef = new Vector3(orta.x, orta.y, 0.6f);
                    break;
                default:
                    // kapının hemen içi: içeriye doğru
                    goz = plan.Esik + new Vector3(0f, 0.45f, 1.6f);
                    hedef = new Vector3(Mathf.Lerp(kapi.x, orta.x, 0.5f), Mathf.Max(kapi.y + 2.5f, orta.y), 1.3f);
                    break;
            }
            bakisGoz = goz;
            Vector3 yon = hedef - goz;
            if (yon.sqrMagnitude < 1e-6f) yon = new Vector3(0f, 1f, 0f);
            bakisYaw = Mathf.Atan2(yon.y, yon.x) * Mathf.Rad2Deg;
            bakisPitch = Mathf.Asin(Mathf.Clamp(yon.normalized.z, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>İçerideyken fare ve klavye (GameUI'nin IMGUI olayından): sürükle / ok tuşları bakış, Tab sonraki nokta.</summary>
        public void Girdi(Event e)
        {
            if (e == null) return;
            if (e.type == EventType.MouseDrag)
            {
                bakisYaw -= e.delta.x * 0.25f;
                bakisPitch = Mathf.Clamp(bakisPitch - e.delta.y * 0.2f, -60f, 55f);
                e.Use();
            }
            else if (e.type == EventType.KeyDown)
            {
                switch (e.keyCode)
                {
                    case KeyCode.LeftArrow: case KeyCode.A: bakisYaw += 8f; break;
                    case KeyCode.RightArrow: case KeyCode.D: bakisYaw -= 8f; break;
                    case KeyCode.UpArrow: case KeyCode.W: bakisPitch = Mathf.Min(55f, bakisPitch + 5f); break;
                    case KeyCode.DownArrow: case KeyCode.S: bakisPitch = Mathf.Max(-60f, bakisPitch - 5f); break;
                    case KeyCode.Tab: BakisSec(bakis + 1); break;
                    default: return;
                }
                e.Use();
            }
        }

        void IceriKamera()
        {
            var cam = Camera.main;
            if (cam == null || kok == null) { Cik(); return; }
            float y = bakisYaw * Mathf.Deg2Rad, p = bakisPitch * Mathf.Deg2Rad;
            var yon = new Vector3(Mathf.Cos(y) * Mathf.Cos(p), Mathf.Sin(y) * Mathf.Cos(p), Mathf.Sin(p));
            Vector3 f = kok.TransformDirection(Yerel(yon));
            Vector3 up = kok.TransformDirection(Vector3.up);
            if (f.sqrMagnitude < 1e-12f) return;
            cam.orthographic = false;
            cam.fieldOfView = 70f;
            cam.nearClipPlane = Mathf.Max(0.0005f, 0.12f * Birim);
            cam.farClipPlane = 400f;
            cam.transform.SetPositionAndRotation(Dunya(bakisGoz), Quaternion.LookRotation(f.normalized, up.normalized));
        }

        // ---------------------------------------------------------------- kartta canlı pencere

        Camera pencereKam;
        RenderTexture pencereRT;
        float pencereSonraki;

        /// <summary>Kartta gösterilecek canlı görüntü (ilk çağrıda kamera ve doku kurulur). İçerideyken null.</summary>
        public Texture PencereDoku()
        {
            if (Iceride == this || kok == null) return null;
            if (pencereKam == null)
            {
                var ana = Camera.main;
                if (ana == null) return null;
                pencereRT = new RenderTexture(320, 200, 16) { name = "DukkanPencere" };
                var go = new GameObject("Dükkân pencere kamerası");
                go.transform.SetParent(kok, false);
                pencereKam = go.AddComponent<Camera>();
                pencereKam.clearFlags = ana.clearFlags;
                pencereKam.backgroundColor = ana.backgroundColor;
                pencereKam.cullingMask = ana.cullingMask;
                pencereKam.orthographic = false;
                pencereKam.fieldOfView = 72f;
                pencereKam.depth = ana.depth - 2f;
                pencereKam.targetTexture = pencereRT;
                pencereKam.enabled = false;
                pencereSonraki = 0f;
            }
            return pencereRT;
        }

        /// <summary>Pencere kamerası saniyede ~6 kare çizer: yalnız o karelerde etkin.</summary>
        void PencereGuncelle(float simdi)
        {
            if (pencereKam == null) return;
            if (simdi < pencereSonraki) { if (pencereKam.enabled) pencereKam.enabled = false; return; }
            pencereSonraki = simdi + 0.16f;
            Vector3 goz = new Vector3(enAz.x + 0.4f, enAz.y + 0.4f, plan.kat - 0.5f);
            Vector3 hedef = new Vector3((enAz.x + enCok.x) * 0.5f, (enAz.y + enCok.y) * 0.5f + 0.4f, 0.7f);
            Vector3 f = kok.TransformDirection(Yerel(hedef - goz));
            if (f.sqrMagnitude < 1e-12f) return;
            float birim = Birim;
            pencereKam.nearClipPlane = Mathf.Max(0.0005f, 0.1f * birim);
            pencereKam.farClipPlane = 60f * birim + 5f;
            pencereKam.transform.SetPositionAndRotation(Dunya(goz), Quaternion.LookRotation(f.normalized, kok.TransformDirection(Vector3.up).normalized));
            pencereKam.enabled = true;
        }

        void PencereBirak()
        {
            if (pencereKam != null)
            {
                pencereKam.targetTexture = null;
                Destroy(pencereKam.gameObject);
                pencereKam = null;
            }
            if (pencereRT != null)
            {
                pencereRT.Release();
                Destroy(pencereRT);
                pencereRT = null;
            }
        }
    }

    /// <summary>
    /// "İçeri gir" kamerasının ortak kapısı: kamera mahalle bakkalının (BakkalCanli) ya da bir atölye binasının (DukkanCanli)
    /// içinde olabilir. GameUI girdiyi ve çıkışı buradan yönetir.
    /// </summary>
    public static class IceriBakis
    {
        public static bool Acik => BakkalCanli.Iceride != null || DukkanCanli.Iceride != null;

        public static void Cik()
        {
            if (BakkalCanli.Iceride != null) BakkalCanli.Iceride.Cik();
            if (DukkanCanli.Iceride != null) DukkanCanli.Iceride.Cik();
        }

        public static void Girdi(Event e)
        {
            if (BakkalCanli.Iceride != null) BakkalCanli.Iceride.Girdi(e);
            else if (DukkanCanli.Iceride != null) DukkanCanli.Iceride.Girdi(e);
        }
    }
}
