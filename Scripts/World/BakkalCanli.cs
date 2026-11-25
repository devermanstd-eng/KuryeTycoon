using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Mahalle bakkalının canlı hâli (model: Tools/Blender/bakkal.py). Yalnızca görseldir: sipariş, para ve denge
    /// verisine dokunmaz, hiçbir şeyi kayda yazmaz (kozmetik süsler PlayerPrefs'te).
    ///  • İnsanlar (BakkalInsanlar.cs): kasiyer, müşteriler, kurye ve tedarik şoförü; basit yol noktası ağında yürürler.
    ///  • Sipariş: bu dükkânın gerçek siparişlerine bakar (OrderManager.Pending / Active). Hazırlanan sipariş varsa
    ///    (şimdi &lt; readyAt) kasiyer raflardan toplar; hazır olunca (şimdi ≥ readyAt, durum Bekliyor / Atandi) tezgâhta
    ///    poşet durur; sipariş Alindi olunca poşet kalkar. Kurye figürü, gerçek kurye dükkâna yaklaşınca
    ///    (DukkanaGidiyor / Bekliyor, hedefi bu dükkân) içeri girer.
    ///  • Saat: ShopHours kapalıysa kepenk iner, ampuller söner, müşteri gelmez. Müşteri sayısı OrderManager.DemandCurve'e
    ///    göre. Raflar gün içinde boşalır (talep eğrisi + bugünkü sipariş ürünleri), 06:30'da tedarik kamyoneti gelir,
    ///    06:36–07:05 arası koliler iner ve raflar dolar. Hepsi saatten hesaplanır: kayıt gerekmez.
    ///  • Seçim: dükkân kartı açıkken (SeciliDukkan) çatı yakınlık aranmadan kalkar; içerideki nesnelere tıklanır.
    ///  • Görüş: çatı açıkken kameraya bakan duvarların bel üstü iner (Duvar_* objeleri); "İçeri gir" kamerayı göz
    ///    hizasına indirir (harita kamerası o sırada kapalı); kartta küçük canlı pencere (ikinci kamera → RenderTexture).
    /// Performans: bina ekranda yeterince büyük değilken (ya da görünmüyorken) hiçbir şey simüle edilmez; durum 0,25 sn'de
    /// bir taranır; figürler yalnız o sırada vardır. Model → Unity dönüşümü dört parçanın kökünden çıkarılır (eksen çevirisi ve
    /// ölçekten bağımsız); parçalar eksikse bileşen kendini kapatır, bina sabit hâliyle kalır.
    /// </summary>
    public partial class BakkalCanli : MonoBehaviour
    {
        // ---- bakkal.py --tablo çıktısı (elle değiştirme) ----
        static readonly (string ad, Vector3 p)[] Noktalar =
        {
            ("A0a", new Vector3(-3.8f, -0.8f, 0.26f)),
            ("A0b", new Vector3(-3.8f, 0.4f, 0.26f)),
            ("A0c", new Vector3(-3.8f, 1.6f, 0.26f)),
            ("A1a", new Vector3(-1.92f, -0.8f, 0.26f)),
            ("A1b", new Vector3(-1.92f, 0.4f, 0.26f)),
            ("A1c", new Vector3(-1.92f, 1.6f, 0.26f)),
            ("A2a", new Vector3(0.03f, -0.8f, 0.26f)),
            ("A2b", new Vector3(0.03f, 0.4f, 0.26f)),
            ("A2c", new Vector3(0.03f, 1.6f, 0.26f)),
            ("AD", new Vector3(3f, 5.45f, 0.08f)),
            ("AI", new Vector3(3.05f, 3.8f, 0.26f)),
            ("B0", new Vector3(-3.8f, 2.95f, 0.26f)),
            ("B1", new Vector3(-1.92f, 2.95f, 0.26f)),
            ("B2", new Vector3(0.03f, 2.95f, 0.26f)),
            ("B3", new Vector3(2.2f, 2.95f, 0.26f)),
            ("ESIK", new Vector3(3.75f, -3.65f, 0.26f)),
            ("GIR", new Vector3(3.25f, -2.3f, 0.26f)),
            ("K0", new Vector3(-3.8f, -2.25f, 0.26f)),
            ("K1", new Vector3(-1.92f, -2.25f, 0.26f)),
            ("K2", new Vector3(0.03f, -2.25f, 0.26f)),
            ("K3", new Vector3(2.2f, -2.25f, 0.26f)),
            ("KAPI", new Vector3(3.85f, -2.65f, 0.26f)),
            ("KAS", new Vector3(3.95f, 1.75f, 0.26f)),
            ("KC", new Vector3(3.75f, 3f, 0.26f)),
            ("KUR", new Vector3(2.2f, 2.4f, 0.26f)),
            ("KV", new Vector3(0.55f, 6.3f, 0.08f)),
            ("ON", new Vector3(3.3f, -5.3f, 0.08f)),
            ("Q0", new Vector3(2.2f, 1.7f, 0.26f)),
            ("Q1", new Vector3(2.2f, 0.8f, 0.26f)),
            ("Q2", new Vector3(2.2f, -0.1f, 0.26f)),
            ("Q3", new Vector3(2.2f, -1f, 0.26f)),
            ("S1", new Vector3(5.6f, -6.5f, 0.08f)),
            ("S2", new Vector3(-1.5f, -6.6f, 0.08f)),
        };
        static readonly string[] Kenarlar =
        {
            "S1-ON", "S2-ON", "ON-ESIK", "ESIK-KAPI", "KAPI-GIR", "GIR-K3", "K3-Q3", "Q3-Q2", "Q2-Q1", "Q1-Q0", "Q0-KUR", "KUR-B3", "B3-KC", "KC-KAS", "B3-AI", "AI-AD", "AD-KV", "K0-A0a", "A0a-A0b", "A0b-A0c", "A0c-B0", "K0-K1", "B0-B1", "K1-A1a", "A1a-A1b", "A1b-A1c", "A1c-B1", "K1-K2", "B1-B2", "K2-A2a", "A2a-A2b", "A2b-A2c", "A2c-B2", "K2-K3", "B2-B3"
        };
        static readonly (string ek, Vector3 p)[] Pivotlar =
        {
            ("_Kapi", new Vector3(4.145f, -2.2f, 0.26f)),
            ("_Kepenk1", new Vector3(-3f, -3.703f, 2.55f)),
            ("_Kamyonet", new Vector3(-2.6f, 6.15f, 0.08f)),
            ("_Poset", new Vector3(2.98f, 1.6f, 1.21f)),
            ("_Kepenk2", new Vector3(0.6f, -3.703f, 2.55f)),
            ("_Kepenk3", new Vector3(3.92f, -2.725f, 2.46f)),
            ("_Koliler", new Vector3(-0.2f, 6.4f, 0.08f)),
        };
        // ---- tablo sonu ----

        const float S2 = 0.70710678f;
        const float ZeminZ = 0.26f, TavanZ = 3.36f;
        static readonly Vector3 KapiKapaliYon = new Vector3(-S2, -S2, 0f), KapiIceriYon = new Vector3(-S2, S2, 0f);
        const float KapiAcikAci = 82f;
        static readonly string[] RafAd = { "G1", "G2", "G3", "DR", "SG" };
        static readonly float[] RafEsik = { 0.25f, 0.5f, 0.75f };    // Urun_<raf>_1 … _3: stok bu oranın altına inince gizlenir
        static readonly string[] GizliParcalar = { "_Kepenk1", "_Kepenk2", "_Kepenk3", "_Poset", "_Kamyonet", "_Koliler", "_Kasa2", "_Sogutucu4", "_Vitrin2" };
        static readonly string[] IcParcalar = { "_Ampul", "_Poset", "_Kasa2", "_Sogutucu4", "_Vitrin2" };
        static readonly string[] DuvarEk = { "_Duvar_On", "_Duvar_Pah", "_Duvar_Sag", "_Duvar_Arka", "_Duvar_Sol" };
        static readonly Vector3[] DuvarNormal =
        {
            new Vector3(0f, -1f, 0f), new Vector3(S2, -S2, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(-1f, 0f, 0f),
        };
        const int DuvarOn = 0, DuvarPah = 1;

        // Tedarik (oyun dakikası, gün içinde): kamyonet gelir, koliler iner, kamyonet gider
        const float TedarikGelis = 390f, TedarikBasla = 396f, TedarikBitis = 425f, TedarikGidis = 432f;

        /// <summary>Bina kartı açık olan dükkân (GameUI her karede yazar). Bu dükkânın çatısı uzaktan da açılır.</summary>
        public static Shop SeciliDukkan;
        static readonly List<BakkalCanli> hepsi = new List<BakkalCanli>();

        /// <summary>Dükkânın canlı bakkalı (yoksa null: eski bina ya da model parçaları eksik).</summary>
        public static BakkalCanli Bul(Shop s)
        {
            if (s == null) return null;
            for (int i = 0; i < hepsi.Count; i++) if (hepsi[i] != null && hepsi[i].shop == s) return hepsi[i];
            return null;
        }

        Shop shop;
        CatiKaldir cati;
        Transform kok, icKok, canliKok;
        Vector3 refB, refU;                    // dönüşümün dayanağı: modelde ve Unity yerelinde aynı nokta
        Matrix4x4 donusum;                     // model (metre) → kökün yerel uzayı (yalnız 3×3 kısmı)
        Vector3 yukari;                        // model +Z'nin kök yerelindeki yönü (birim)
        float metre;                           // modelde 1 m'nin kök yerelindeki boyu

        Transform kapi, poset, kamyonet, koliler, ampul, kasa2, sogutucu4, vitrin2;
        Quaternion kapiDonus0;
        readonly Transform[] kepenk = new Transform[3];
        readonly Vector3[] kepenkOlcek0 = new Vector3[3];
        readonly Transform[] duvar = new Transform[5];
        readonly Vector3[] duvarOlcek0 = new Vector3[5];
        readonly float[] duvarInik = new float[5], duvarHedef = new float[5];
        readonly Transform[,] urun = new Transform[5, 3];
        Vector3 koliOlcek0, kamyonetOlcek0;

        bool etkin;
        float sonrakiKontrol, sonrakiAgir;
        float kapiAcik, kepenkInik = -1f;
        bool dukkanAcik = true;
        float stok = 1f;
        int siparisUrun;                       // tedarikten beri bu dükkândan çıkan sipariş ürünleri
        int hazirlanan, hazir;
        bool kuryeYakin, posetGorunur;
        float hazirBekleme;
        int alinanOlay;                        // kurye figürünün götüreceği teslim almalar
        readonly HashSet<Order> izlenen = new HashSet<Order>(), izlenenYeni = new HashSet<Order>();
        readonly HashSet<int> toplanan = new HashSet<int>();
        Order toplanacak;

        // ---------------------------------------------------------------- kurulum

        /// <summary>
        /// Çatısı kalkan modele (CatiKaldir) canlı bakkalı takar. Model parçaları tam değilse ya da dükkân yoksa yalnızca
        /// varsayılan olarak gizli durması gereken parçaları (kepenk, poşet, kamyonet, süsler) gizler.
        /// </summary>
        public static BakkalCanli Tak(GameObject inst, Shop shop, CatiKaldir ck)
        {
            if (inst == null || ck == null || ck.Kok == null) return null;
            var kok = ck.Kok;
            for (int i = 0; i < kok.childCount; i++)
            {
                var c = kok.GetChild(i);
                for (int k = 0; k < GizliParcalar.Length; k++)
                    if (c.name.EndsWith(GizliParcalar[k])) { c.gameObject.SetActive(false); break; }
            }
            if (shop == null) return null;
            var b = inst.AddComponent<BakkalCanli>();
            if (!b.Kur(shop, ck))
            {
                Destroy(b);
                return null;
            }
            return b;
        }

        static Transform Parca(Transform kok, string ek)
        {
            for (int i = 0; i < kok.childCount; i++)
            {
                var c = kok.GetChild(i);
                if (c.name.EndsWith(ek)) return c;
            }
            return null;
        }

        static Matrix4x4 Sutunlar(Vector3 a, Vector3 b, Vector3 c)
        {
            var m = Matrix4x4.identity;
            m.SetColumn(0, new Vector4(a.x, a.y, a.z, 0f));
            m.SetColumn(1, new Vector4(b.x, b.y, b.z, 0f));
            m.SetColumn(2, new Vector4(c.x, c.y, c.z, 0f));
            return m;
        }

        bool Kur(Shop s, CatiKaldir ck)
        {
            shop = s;
            cati = ck;
            kok = ck.Kok;
            icKok = ck.Ic;
            // Dönüşüm: ilk dört pivotlu parçanın modeldeki ve Unity'deki yerinden
            var u = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                var t = Parca(kok, Pivotlar[i].ek);
                if (t == null)
                {
                    Debug.LogWarning("Kurye Tycoon · bakkal: '" + Pivotlar[i].ek + "' parçası yok, canlı bakkal kapalı (model eski mi? Kurye Tycoon > Blender > Bakkalı üret).");
                    return false;
                }
                u[i] = t.localPosition;
            }
            Vector3 b0 = Pivotlar[0].p;
            var mb = Sutunlar(Pivotlar[1].p - b0, Pivotlar[2].p - b0, Pivotlar[3].p - b0);
            if (Mathf.Abs(mb.determinant) < 1e-5f) return false;
            donusum = Sutunlar(u[1] - u[0], u[2] - u[0], u[3] - u[0]) * mb.inverse;
            refB = b0;
            refU = u[0];
            Vector3 z = donusum.MultiplyVector(Vector3.forward);
            metre = z.magnitude;
            if (metre < 1e-4f || metre > 1e4f || float.IsNaN(metre))
            {
                Debug.LogWarning("Kurye Tycoon · bakkal: parça konumlarından dönüşüm çıkarılamadı, canlı bakkal kapalı.");
                return false;
            }
            yukari = z / metre;

            kapi = Parca(kok, "_Kapi");
            kapiDonus0 = kapi.localRotation;
            poset = Parca(kok, "_Poset");
            kamyonet = Parca(kok, "_Kamyonet");
            kamyonetOlcek0 = kamyonet.localScale;
            koliler = Parca(kok, "_Koliler");
            if (koliler != null) koliOlcek0 = koliler.localScale;
            ampul = Parca(kok, "_Ampul");
            kasa2 = Parca(kok, "_Kasa2");
            sogutucu4 = Parca(kok, "_Sogutucu4");
            vitrin2 = Parca(kok, "_Vitrin2");
            for (int i = 0; i < 3; i++)
            {
                kepenk[i] = Parca(kok, "_Kepenk" + (i + 1));
                if (kepenk[i] != null) kepenkOlcek0[i] = kepenk[i].localScale;
            }
            for (int i = 0; i < 5; i++)
            {
                duvar[i] = Parca(kok, DuvarEk[i]);
                if (duvar[i] != null) duvarOlcek0[i] = duvar[i].localScale;
            }
            for (int r = 0; r < 5; r++)
                for (int k = 0; k < 3; k++)
                    urun[r, k] = Parca(kok, "_Urun_" + RafAd[r] + "_" + (k + 1));

            // İç mekâna ait parçalar iç objenin altına: çatı kapalıyken onunla birlikte gizlenir
            if (icKok != null)
            {
                for (int i = kok.childCount - 1; i >= 0; i--)
                {
                    var c = kok.GetChild(i);
                    bool icParca = c.name.Contains("_Urun_");
                    for (int k = 0; k < IcParcalar.Length && !icParca; k++) icParca = c.name.EndsWith(IcParcalar[k]);
                    if (icParca) c.SetParent(icKok, true);
                }
            }

            canliKok = new GameObject(CatiKaldir.CanliAd).transform;
            canliKok.SetParent(kok, false);
            canliKok.gameObject.SetActive(false);
            AgKur();
            SusUygula();
            hepsi.Add(this);
            sonrakiKontrol = Time.unscaledTime + Random.value * 0.25f;
            return true;
        }

        void OnDestroy()
        {
            hepsi.Remove(this);
            if (Iceride == this) Cik();
            PencereBirak();
        }

        void OnDisable()
        {
            // Bina gizlendi (düz harita görünümü): figürler dursun; tekrar görününce Kontrol yeniden başlatır
            if (Iceride == this) Cik();
            etkin = false;
            uyutBekliyor = true;
        }
        bool uyutBekliyor;

        // ---------------------------------------------------------------- dönüşüm

        /// <summary>Model noktası (metre) → kökün yerel uzayı.</summary>
        Vector3 Yerel(Vector3 b) => refU + donusum.MultiplyVector(b - refB);
        /// <summary>Model yönü → kökün yerel uzayı (boyu ölçekli).</summary>
        Vector3 YerelYon(Vector3 b) => donusum.MultiplyVector(b);
        Vector3 Dunya(Vector3 b) => kok.TransformPoint(Yerel(b));

        /// <summary>Parçayı model yukarı ekseninde (kendi kökü sabit) s oranına ezer: kepenk sarılır, duvar iner.</summary>
        void DikeyOlcek(Transform t, Vector3 olcek0, float s)
        {
            Vector3 e = Quaternion.Inverse(t.localRotation) * yukari;
            e = new Vector3(Mathf.Abs(e.x), Mathf.Abs(e.y), Mathf.Abs(e.z));
            Vector3 k = Vector3.one - e * (1f - s);
            t.localScale = Vector3.Scale(olcek0, k);
        }

        // ---------------------------------------------------------------- döngü

        void Update()
        {
            if (shop == null || kok == null) return;
            float simdi = Time.unscaledTime;
            if (uyutBekliyor) { uyutBekliyor = false; Uyut(); }
            if (simdi >= sonrakiKontrol)
            {
                sonrakiKontrol = simdi + 0.25f;
                Kontrol(simdi);
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

        /// <summary>Durum taraması (0,25 sn'de bir): görünürlük, seçim, siparişler, saat.</summary>
        void Kontrol(float simdi)
        {
            bool secili = SeciliDukkan == shop;
            bool iceride = Iceride == this;
            cati.zorlaAcik = secili && !iceride;
            if (!secili && !iceride) { seciliNesne = Nesne.Yok; PencereBirak(); }

            // Görünür mü: çatı açık / açılıyor, kamera içeride ya da bina ekranda yeterince büyük ve ekranın içinde
            bool yakin = cati.SonBoy >= 0.09f && cati.SonMerkez < 0.95f;
            bool yeni = iceride || cati.Acik || cati.IcGorunur || yakin;
            if (MapCamera.I != null && MapCamera.I.Tilt01 < 0.9f && !iceride) yeni = false;
            if (yeni != etkin)
            {
                etkin = yeni;
                if (etkin) Uyandir(); else Uyut();
            }
            if (!etkin) return;

            var clk = GameClock.I;
            if (clk == null) return;
            float now = clk.TotalMinutes;
            SiparisTara(now);
            dukkanAcik = ShopHours.IsOpen(shop.def);
            if (simdi >= sonrakiAgir)
            {
                sonrakiAgir = simdi + 1f;
                siparisUrun = SiparisUrunSay(now);
                stok = StokHesapla(now);
                RaflariUygula();
                SusUygula();
            }
            DuvarHedefleri(iceride);
        }

        void Uyandir()
        {
            canliKok.gameObject.SetActive(true);
            sonrakiAgir = 0f;
            kepenkInik = -1f;       // ilk karede doğrudan yerine otursun
            InsanlariBaslat();
        }

        void Uyut()
        {
            if (canliKok != null) canliKok.gameObject.SetActive(false);
            InsanlariTopla();
            izlenen.Clear();
            alinanOlay = 0;
            posetGorunur = false;
            if (poset != null) poset.gameObject.SetActive(false);
            if (kamyonet != null) kamyonet.gameObject.SetActive(false);
            if (koliler != null) koliler.gameObject.SetActive(false);
            // Kapı kapalı, duvarlar yerinde
            kapiAcik = 0f;
            if (kapi != null) { kapi.localRotation = kapiDonus0; kapi.gameObject.SetActive(true); }
            for (int i = 0; i < 5; i++)
            {
                duvarInik[i] = duvarHedef[i] = 0f;
                if (duvar[i] != null) { duvar[i].localScale = duvarOlcek0[i]; duvar[i].gameObject.SetActive(true); }
            }
        }

        // ---------------------------------------------------------------- siparişler (gerçek durum)

        void SiparisTara(float now)
        {
            hazirlanan = hazir = 0;
            toplanacak = null;
            izlenenYeni.Clear();
            var om = OrderManager.I;
            if (om != null)
            {
                Tara(om.Pending, now);
                Tara(om.Active, now);
            }
            // Az önce bekleyen sipariş teslim alındıysa: kurye figürü poşeti götürür
            foreach (var o in izlenen)
                if (!izlenenYeni.Contains(o) && o.status == OrderStatus.Alindi) alinanOlay++;
            izlenen.Clear();
            foreach (var o in izlenenYeni) izlenen.Add(o);
            if (alinanOlay > 3) alinanOlay = 3;

            kuryeYakin = false;
            var cm = CourierManager.I;
            if (cm != null)
            {
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
        }

        void Tara(List<Order> liste, float now)
        {
            for (int i = 0; i < liste.Count; i++)
            {
                var o = liste[i];
                if (o == null || o.shop != shop) continue;
                if (o.status != OrderStatus.Bekliyor && o.status != OrderStatus.Atandi) continue;
                izlenenYeni.Add(o);
                if (now < o.readyAt)
                {
                    hazirlanan++;
                    if (toplanacak == null && !toplanan.Contains(o.id)) toplanacak = o;
                }
                else hazir++;
            }
        }

        /// <summary>Son tedarikten (07:05) beri bu dükkândan çıkan sipariş ürünleri.</summary>
        int SiparisUrunSay(float now)
        {
            var om = OrderManager.I;
            if (om == null) return 0;
            float bas = DonguBasi(now);
            int n = 0;
            foreach (var o in om.Pending) if (o.shop == shop && o.createdAt >= bas) n += o.ItemCount;
            foreach (var o in om.Active) if (o.shop == shop && o.createdAt >= bas) n += o.ItemCount;
            int bakilan = 0;
            foreach (var o in om.History)
            {
                if (++bakilan > 600) break;
                if (o.shop == shop && o.createdAt >= bas && o.status == OrderStatus.Teslim) n += o.ItemCount;
            }
            return n;
        }

        static float DonguBasi(float now)
        {
            float gun = now % 1440f;
            float bas = now - gun + TedarikBitis;
            if (gun < TedarikBitis) bas -= 1440f;
            return bas;
        }

        /// <summary>Raf doluluğu (0–1): tedarikten beri geçen talep (DemandCurve) ve çıkan sipariş ürünleriyle azalır.</summary>
        float StokHesapla(float now)
        {
            var clk = GameClock.I;
            float bas = DonguBasi(now);
            bool hs = clk != null && clk.IsWeekend;
            float toplam = 0f, gecen = 0f;
            for (int i = 0; i < 24; i++)
            {
                float t0 = bas + i * 60f;
                int h = (int)(((t0 % 1440f) + 1440f) % 1440f / 60f) % 24;
                float w = OrderManager.DemandCurve(shop.def, h, hs);
                toplam += w;
                if (t0 + 60f <= now) gecen += w;
                else if (t0 < now) gecen += w * (now - t0) / 60f;
            }
            float s = 1f - 0.78f * (toplam > 0f ? gecen / toplam : 0f) - 0.012f * siparisUrun;
            s = Mathf.Clamp(s, 0.05f, 1f);
            float gun = now % 1440f;
            if (gun >= TedarikBasla && gun < TedarikBitis) s = Mathf.Lerp(s, 1f, (gun - TedarikBasla) / (TedarikBitis - TedarikBasla));
            return s;
        }

        void RaflariUygula()
        {
            for (int r = 0; r < 5; r++)
            {
                float s = stok + ((r * 37) % 10 - 5) * 0.01f;      // raflar aynı anda boşalmasın
                for (int k = 0; k < 3; k++)
                {
                    var t = urun[r, k];
                    if (t == null) continue;
                    bool gor = s > RafEsik[k];
                    if (t.gameObject.activeSelf != gor) t.gameObject.SetActive(gor);
                }
            }
        }

        // ---------------------------------------------------------------- kapı, kepenk, poşet, kamyonet, duvarlar

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
            for (int i = 0; i < 5; i++)
            {
                float hedef = 0f;
                if (kameraya != Vector2.zero)
                {
                    Vector3 n = kok.TransformDirection(YerelYon(DuvarNormal[i]));
                    var n2 = new Vector2(n.x, n.y);
                    if (n2.sqrMagnitude > 1e-10f && Vector2.Dot(n2.normalized, kameraya) > 0.22f) hedef = 1f;
                }
                duvarHedef[i] = hedef;
            }
        }

        void ParcalariGuncelle(float gercek)
        {
            // Duvarlar: kameraya bakanların bel üstü iner
            for (int i = 0; i < 5; i++)
            {
                if (duvar[i] == null) continue;
                if (!Mathf.Approximately(duvarInik[i], duvarHedef[i]))
                {
                    duvarInik[i] = Mathf.MoveTowards(duvarInik[i], duvarHedef[i], gercek * 3f);
                    float s = 1f - Mathf.SmoothStep(0f, 1f, duvarInik[i]);
                    bool gor = s > 0.02f;
                    if (duvar[i].gameObject.activeSelf != gor) duvar[i].gameObject.SetActive(gor);
                    if (gor) DikeyOlcek(duvar[i], duvarOlcek0[i], Mathf.Max(0.02f, s));
                }
            }
            bool pahInik = duvarInik[DuvarPah] > 0.5f, onInik = duvarInik[DuvarOn] > 0.5f;

            // Kapı: yakınında biri varsa açılır
            if (kapi != null)
            {
                bool gor = !pahInik;
                if (kapi.gameObject.activeSelf != gor) kapi.gameObject.SetActive(gor);
                float hedef = KapiYakini() ? 1f : 0f;
                if (!Mathf.Approximately(kapiAcik, hedef))
                {
                    kapiAcik = Mathf.MoveTowards(kapiAcik, hedef, gercek * 3.5f);
                    float a = Mathf.SmoothStep(0f, 1f, kapiAcik) * KapiAcikAci * Mathf.Deg2Rad;
                    Vector3 d0 = YerelYon(KapiKapaliYon).normalized;
                    Vector3 d1 = (YerelYon(KapiKapaliYon) * Mathf.Cos(a) + YerelYon(KapiIceriYon) * Mathf.Sin(a)).normalized;
                    kapi.localRotation = Quaternion.FromToRotation(d0, d1) * kapiDonus0;
                }
            }

            // Kepenkler: dükkân kapalıyken iner (ilk karede animasyonsuz yerine oturur)
            float kh = dukkanAcik ? 0f : 1f;
            kepenkInik = kepenkInik < 0f ? kh : Mathf.MoveTowards(kepenkInik, kh, gercek * 0.7f);
            for (int i = 0; i < 3; i++)
            {
                if (kepenk[i] == null) continue;
                bool gizli = kepenkInik < 0.03f || (i < 2 ? onInik : pahInik);
                if (kepenk[i].gameObject.activeSelf == gizli) kepenk[i].gameObject.SetActive(!gizli);
                if (!gizli) DikeyOlcek(kepenk[i], kepenkOlcek0[i], Mathf.Max(0.03f, kepenkInik));
            }
            if (ampul != null && ampul.gameObject.activeSelf != dukkanAcik) ampul.gameObject.SetActive(dukkanAcik);

            // Poşet: hazır sipariş varken tezgâhta (kasiyer toplayıp dönünce ya da birkaç saniye sonra)
            if (hazir > 0)
            {
                hazirBekleme += gercek;
                if (!posetGorunur && (KasiyerKasada || hazirBekleme > 5f)) posetGorunur = true;
            }
            else { hazirBekleme = 0f; posetGorunur = false; }
            if (poset != null && poset.gameObject.activeSelf != posetGorunur) poset.gameObject.SetActive(posetGorunur);

            // Tedarik kamyoneti: saatten
            var clk = GameClock.I;
            if (clk == null || kamyonet == null) return;
            float gun = clk.TotalMinutes % 1440f;
            bool var_ = gun >= TedarikGelis && gun < TedarikGidis;
            if (kamyonet.gameObject.activeSelf != var_) kamyonet.gameObject.SetActive(var_);
            bool indirme = gun >= TedarikBasla && gun < TedarikBitis;
            if (var_)
            {
                float uzak = gun < TedarikBasla ? 1f - (gun - TedarikGelis) / (TedarikBasla - TedarikGelis)
                           : gun >= TedarikBitis ? (gun - TedarikBitis) / (TedarikGidis - TedarikBitis) : 0f;
                uzak = Mathf.SmoothStep(0f, 1f, uzak);
                kamyonet.localPosition = Yerel(Pivotlar[2].p + new Vector3(-6.5f * uzak, 0f, 0f));
                kamyonet.localScale = kamyonetOlcek0 * Mathf.Lerp(1f, 0.25f, Mathf.Clamp01((uzak - 0.55f) / 0.45f));
            }
            if (koliler != null)
            {
                if (koliler.gameObject.activeSelf != indirme) koliler.gameObject.SetActive(indirme);
                if (indirme) koliler.localScale = koliOlcek0 * Mathf.Lerp(1f, 0.4f, (gun - TedarikBasla) / (TedarikBitis - TedarikBasla));
            }
            tedarikSuruyor = indirme;
        }
        bool tedarikSuruyor;

        // ---------------------------------------------------------------- kart için özet

        public struct Ozet
        {
            public int bugun, hazirlaniyor, hazir, yolda, teslim, musteri;
            public long kazanc;
            public float stok;
            public bool acik, simule, tedarik;
        }

        /// <summary>Dükkân kartı için bugünün özeti (gerçek sipariş verisinden; müşteri sayısı ve stok görseldir).</summary>
        public Ozet OzetAl()
        {
            var z = new Ozet { stok = stok, simule = etkin, musteri = MusteriSayisi, tedarik = tedarikSuruyor };
            var clk = GameClock.I;
            var om = OrderManager.I;
            if (clk == null || om == null) return z;
            float now = clk.TotalMinutes;
            float gunBasi = now - now % 1440f;
            z.acik = ShopHours.IsOpen(shop.def);
            if (!etkin) { siparisUrun = SiparisUrunSay(now); z.stok = stok = StokHesapla(now); }
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

        // ---------------------------------------------------------------- nesneye tıklama ve kozmetik süsler

        public enum Nesne { Yok, Raf, Kasa, Sogutucu, Vitrin }
        Nesne seciliNesne;
        public Nesne SeciliNesne => seciliNesne;

        static readonly Nesne[] SicakTur = { Nesne.Raf, Nesne.Raf, Nesne.Raf, Nesne.Raf, Nesne.Kasa, Nesne.Sogutucu, Nesne.Sogutucu, Nesne.Vitrin, Nesne.Vitrin };
        static readonly Vector3[] SicakYer =
        {
            new Vector3(-2.9f, 0.45f, 1.4f), new Vector3(-0.95f, 0.45f, 1.4f), new Vector3(1f, 0.45f, 1.4f), new Vector3(-4.7f, -1f, 1.6f),
            new Vector3(3.05f, 1.6f, 1.3f), new Vector3(0.03f, 4f, 1.8f), new Vector3(-3.1f, 4f, 1f), new Vector3(-3f, -3.1f, 1f), new Vector3(0.6f, -3.1f, 1f),
        };

        /// <summary>Çatı açıkken içerideki nesnelere tıklama (ekran pikseli). Bir nesneye denk geldiyse true.</summary>
        public bool NesneTikla(Camera cam, Vector2 ekranPx)
        {
            if (cam == null || !cati.IcGorunur || Iceride == this) return false;
            float sinir = Mathf.Max(18f, 0.12f * cati.SonBoy * Screen.height);
            float en = sinir;
            Nesne bulunan = Nesne.Yok;
            for (int i = 0; i < SicakYer.Length; i++)
            {
                Vector3 e = cam.WorldToScreenPoint(Dunya(SicakYer[i]));
                if (e.z <= 0f) continue;
                float d = Vector2.Distance(new Vector2(e.x, e.y), ekranPx);
                if (d < en) { en = d; bulunan = SicakTur[i]; }
            }
            if (bulunan == Nesne.Yok) return false;
            seciliNesne = bulunan;
            return true;
        }

        public const int SusKasa2 = 0, SusSogutucu = 1, SusVitrin = 2;
        const string SusKey = "KuryeTycoon.BakkalSus";
        static int susCache = -1;
        /// <summary>Kozmetik süsler (ikinci kasa, dördüncü soğutucu, yeni vitrin): yalnız görsel, ücretsiz, PlayerPrefs'te.</summary>
        public static bool Sus(int i)
        {
            if (susCache < 0) susCache = PlayerPrefs.GetInt(SusKey, 0);
            return (susCache & (1 << i)) != 0;
        }

        public static void SusDegistir(int i)
        {
            if (susCache < 0) susCache = PlayerPrefs.GetInt(SusKey, 0);
            susCache ^= 1 << i;
            PlayerPrefs.SetInt(SusKey, susCache);
            PlayerPrefs.Save();
            for (int k = 0; k < hepsi.Count; k++) if (hepsi[k] != null) hepsi[k].SusUygula();
        }

        void SusUygula()
        {
            if (kasa2 != null && kasa2.gameObject.activeSelf != Sus(SusKasa2)) kasa2.gameObject.SetActive(Sus(SusKasa2));
            if (sogutucu4 != null && sogutucu4.gameObject.activeSelf != Sus(SusSogutucu)) sogutucu4.gameObject.SetActive(Sus(SusSogutucu));
            if (vitrin2 != null && vitrin2.gameObject.activeSelf != Sus(SusVitrin)) vitrin2.gameObject.SetActive(Sus(SusVitrin));
        }

        // ---------------------------------------------------------------- "İçeri gir" kamerası

        /// <summary>Kamerası içinde olan bakkal (yoksa null). GameUI bu sırada harita girdilerini buraya yollar.</summary>
        public static BakkalCanli Iceride { get; private set; }
        static readonly Vector3[] BakisYer =
        {
            new Vector3(3f, -1.9f, ZeminZ + 1.6f), new Vector3(2.2f, 1f, ZeminZ + 1.6f), new Vector3(-3.8f, 2.9f, ZeminZ + 1.6f), new Vector3(0.03f, -2.2f, ZeminZ + 1.6f),
        };
        static readonly float[] BakisYaw = { 150f, 20f, -40f, 90f };
        public static readonly string[] BakisAd = { "Giriş", "Kasa önü", "Arka köşe", "Ön koridor" };
        int bakis;
        float bakisYaw, bakisPitch;
        Light icIsik;
        public int Bakis => bakis;

        public void IceriGir()
        {
            if (Iceride == this) return;
            if (Iceride != null) Iceride.Cik();
            var mc = MapCamera.I;
            if (mc == null || Camera.main == null) return;
            Iceride = this;
            mc.enabled = false;                 // harita kamerası kamerayı yönetmesin; çıkınca kendi durumuna döner
            cati.iceriModu = true;
            cati.zorlaAcik = false;
            PencereBirak();
            BakisSec(0);
            if (icIsik == null)
            {
                var go = new GameObject("Bakkal iç ışık");
                go.transform.SetParent(kok, false);
                go.transform.localPosition = Yerel(new Vector3(-0.2f, 0.5f, TavanZ - 0.5f));
                icIsik = go.AddComponent<Light>();
                icIsik.type = LightType.Point;
                icIsik.color = new Color(1f, 0.93f, 0.8f);
                icIsik.shadows = LightShadows.None;
            }
            float birim = kok.TransformVector(YerelYon(Vector3.right)).magnitude;   // 1 m'nin dünyadaki boyu
            icIsik.range = 11f * birim;
            icIsik.intensity = 1.4f;
            icIsik.enabled = true;
            sonrakiKontrol = 0f;
        }

        public void Cik()
        {
            if (Iceride != this) return;
            Iceride = null;
            if (cati != null) cati.iceriModu = false;
            if (icIsik != null) icIsik.enabled = false;
            if (MapCamera.I != null) MapCamera.I.enabled = true;
            sonrakiKontrol = 0f;
        }

        public void BakisSec(int i)
        {
            bakis = ((i % BakisYer.Length) + BakisYer.Length) % BakisYer.Length;
            bakisYaw = BakisYaw[bakis];
            bakisPitch = -6f;
        }

        /// <summary>İçerideyken fare ve klavye (GameUI'nin IMGUI olayından): sürükle / ok tuşları bakış, Tab sonraki nokta.</summary>
        public void Girdi(Event e)
        {
            if (e == null) return;
            if (e.type == EventType.MouseDrag)
            {
                bakisYaw -= e.delta.x * 0.25f;
                bakisPitch = Mathf.Clamp(bakisPitch - e.delta.y * 0.2f, -55f, 55f);
                e.Use();
            }
            else if (e.type == EventType.KeyDown)
            {
                switch (e.keyCode)
                {
                    case KeyCode.LeftArrow: case KeyCode.A: bakisYaw += 8f; break;
                    case KeyCode.RightArrow: case KeyCode.D: bakisYaw -= 8f; break;
                    case KeyCode.UpArrow: case KeyCode.W: bakisPitch = Mathf.Min(55f, bakisPitch + 5f); break;
                    case KeyCode.DownArrow: case KeyCode.S: bakisPitch = Mathf.Max(-55f, bakisPitch - 5f); break;
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
            Vector3 f = kok.TransformDirection(YerelYon(yon));
            Vector3 up = kok.TransformDirection(yukari);
            if (f.sqrMagnitude < 1e-12f) return;
            float birim = kok.TransformVector(YerelYon(Vector3.right)).magnitude;
            cam.orthographic = false;
            cam.fieldOfView = 68f;
            cam.nearClipPlane = Mathf.Max(0.0005f, 0.12f * birim);
            cam.farClipPlane = 400f;
            cam.transform.SetPositionAndRotation(Dunya(BakisYer[bakis]), Quaternion.LookRotation(f.normalized, up.normalized));
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
                pencereRT = new RenderTexture(320, 200, 16) { name = "BakkalPencere" };
                var go = new GameObject("Bakkal pencere kamerası");
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
            // İç köşeden (sol ön, tavana yakın) kasaya ve raflara bakan geniş açı
            Vector3 goz = new Vector3(-4.55f, -2.95f, ZeminZ + 2.45f), hedef = new Vector3(1.2f, 1.6f, ZeminZ + 0.7f);
            Vector3 f = kok.TransformDirection(YerelYon(hedef - goz));
            if (f.sqrMagnitude < 1e-12f) return;
            float birim = kok.TransformVector(YerelYon(Vector3.right)).magnitude;
            pencereKam.nearClipPlane = Mathf.Max(0.0005f, 0.1f * birim);
            pencereKam.farClipPlane = 60f * birim + 5f;
            pencereKam.transform.SetPositionAndRotation(Dunya(goz), Quaternion.LookRotation(f.normalized, kok.TransformDirection(yukari).normalized));
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
}
