using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Kuryenin paketleri haritada:
    ///  • Kurye dükkanda beklerken (sipariş hazırlanıyor) kapının önünde dükkan çalışanı açık bir keseye ürünleri tek tek
    ///    koyar; hazırlık ilerledikçe kese dolar. Sipariş hazır olunca çalışan ve kese kalkar, kese kuryeye geçer.
    ///  • Yaya / bisiklet / scooter / motor kuryesi taşıdığı paketleri kapanmış kese olarak elinde taşır
    ///    (sağ elde ilki, iki ve daha fazla pakette sol elde de bir kese). Arabalı kuryede kese bagajdadır.
    /// Modeller: Resources/Keseler (Blender: calistir_kese.py → Kurye Tycoon > Blender Binalarını Prefab Yap).
    /// </summary>
    public class KuryePaketi : MonoBehaviour
    {
        Courier kurye;
        /// <summary>Karakterlerin ayağı haritanın biraz üstünde (CharacterVisuals.Spawn: −0,06).</summary>
        const float Zemin = 0.06f;

        // ---- elde taşınan keseler
        readonly GameObject[] eldeki = new GameObject[2];
        readonly string[] eldekiTur = new string[2];
        GameObject kemikModeli;                 // kemiklerin bulunduğu model (değişince yeniden aranır)
        Transform sagEl, sagDirsek, solEl, solDirsek;

        static readonly Dictionary<string, float> Boy = new Dictionary<string, float>
        {
            { "ufak", 0.23f }, { "orta", 0.31f }, { "uzun", 0.43f }, { "genis", 0.35f }, { "ince", 0.28f }, { "buyuk", 0.41f }, { "acik", 0.30f }
        };

        // ---- dükkanda hazırlık
        GameObject istasyon, calisan, acikKese;
        readonly List<GameObject> urunler = new List<GameObject>();
        Order hazirlanan;
        float basladi;
        float calisanYon;
        Vector3 keseYeri, uruneBaslangic;

        void Awake() => kurye = GetComponent<Courier>();

        void OnDisable() => Temizle();
        void OnDestroy() => Temizle();

        void Temizle()
        {
            for (int i = 0; i < 2; i++) { if (eldeki[i] != null) Destroy(eldeki[i]); eldeki[i] = null; eldekiTur[i] = null; }
            IstasyonuKaldir();
        }

        void LateUpdate()
        {
            if (kurye == null) return;
            bool acik = KeseGorsel.Var && MapCamera.I != null && MapCamera.I.ShowModels && kurye.IsOnline;
            Hazirlik(acik);
            Eldekiler(acik);
        }

        // ================================================================ dükkanda hazırlık

        void Hazirlik(bool goster)
        {
            var o = kurye.AktifSiparis;
            bool bekliyor = kurye.State == CourierState.Bekliyor && o != null && o.shop != null;
            if (!bekliyor || o != hazirlanan) IstasyonuKaldir();
            if (!bekliyor || !goster) { if (istasyon != null) istasyon.SetActive(false); return; }
            if (istasyon == null) IstasyonKur(o);
            istasyon.SetActive(true);

            float now = GameClock.I != null ? GameClock.I.TotalMinutes : 0f;
            float sure = Mathf.Max(0.5f, o.readyAt - basladi);
            float ilerleme = Mathf.Clamp01((now - basladi) / sure);
            int n = urunler.Count;
            for (int i = 0; i < n; i++)
            {
                // her ürün kendi dilimde: elden keseye iner, sonra kesenin içinde kalır (üstü ağızdan görünür)
                float a = (i + 0.15f) / (n + 0.5f), b = (i + 0.85f) / (n + 0.5f);
                float t = Mathf.InverseLerp(a, b, ilerleme);
                var u = urunler[i];
                u.SetActive(ilerleme >= a);
                if (!u.activeSelf) continue;
                Vector3 ic = keseYeri + Vector3.back * (KeseGorsel.Olcek * (0.12f + 0.035f * i))
                             + new Vector3(Mathf.Sin(i * 2.1f), Mathf.Cos(i * 1.7f), 0f) * KeseGorsel.Olcek * 0.03f;
                float e = t * t * (3f - 2f * t);
                u.transform.position = Vector3.Lerp(uruneBaslangic, ic, e) + Vector3.back * Mathf.Sin(e * Mathf.PI) * KeseGorsel.Olcek * 0.08f;
                u.transform.rotation = Quaternion.Euler(0f, 0f, i * 37f) * Quaternion.Euler(-90f, 0f, 0f);
            }
            if (calisan != null)
            {
                float yuz = calisanYon;
                CharacterVisuals.UpdateModel(calisan, Vector2.zero, false, true, ref yuz);
            }
        }

        void IstasyonKur(Order o)
        {
            hazirlanan = o;
            basladi = GameClock.I != null ? GameClock.I.TotalMinutes : 0f;
            var shop = o.shop;
            Vector2 kapi = shop.door;
            Vector2 ice = (Vector2)(shop.tile - shop.door);
            if (ice.sqrMagnitude < 0.01f) ice = Vector2.up;
            ice.Normalize();
            Vector2 yan = new Vector2(-ice.y, ice.x);
            // aynı dükkanda bekleyen kuryelerin tezgâhları üst üste binmesin
            float kay = ((Mathf.Abs(kurye.courierName.GetHashCode()) % 5) - 2) * 0.09f;
            Vector2 kese2 = kapi + ice * 0.20f + yan * kay;
            Vector2 calisan2 = kapi + ice * 0.33f + yan * kay;

            istasyon = new GameObject("Paket hazırlığı (" + kurye.courierName + ")");
            keseYeri = CityMap.ToWorld(kese2) + Vector3.back * Zemin;              // karakterlerin ayağıyla aynı hizada
            acikKese = KeseGorsel.Yap("acik", istasyon.transform);
            if (acikKese != null)
                KeseGorsel.Yerlestir(acikKese, keseYeri, Vector3.back, new Vector3(-ice.x, -ice.y, 0f));

            // dükkan çalışanı: kesenin arkasında, kapıya (kuryeye) dönük; şirket kıyafeti değil kendi kıyafeti
            var cGo = new GameObject("Çalışan");
            cGo.transform.SetParent(istasyon.transform, false);
            cGo.transform.position = CityMap.ToWorld(calisan2);
            calisan = CharacterVisuals.Spawn(cGo.transform, shop.Name + "#" + kurye.courierName, false);
            Vector2 d = -ice;                          // dışarı bakar
            calisanYon = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (d.x > 0 ? 270f : 90f) : (d.y > 0 ? 0f : 180f);
            uruneBaslangic = CityMap.ToWorld(Vector2.Lerp(calisan2, kese2, 0.55f)) + Vector3.back * CharacterVisuals.TargetHeight * 0.55f;

            var rnd = new System.Random(o.id * 7919 + 17);
            int n = Mathf.Clamp(2 + o.basket / 4000, 3, 6);
            Color dr = shop.def != null ? shop.def.color : Color.white;
            for (int i = 0; i < n; i++)
            {
                var u = KeseGorsel.Urun(rnd, dr);
                u.transform.SetParent(istasyon.transform, true);
                u.SetActive(false);
                urunler.Add(u);
            }
        }

        void IstasyonuKaldir()
        {
            if (istasyon != null) Destroy(istasyon);
            istasyon = null; calisan = null; acikKese = null; hazirlanan = null;
            urunler.Clear();
        }

        // ================================================================ elde taşınan keseler

        void Eldekiler(bool goster)
        {
            var model = kurye.Model;
            var tasinan = kurye.CarryingList;
            int adet = (!goster || kurye.ArabaModeli || model == null || !model.activeInHierarchy) ? 0 : Mathf.Min(2, tasinan.Count);
            if (model != kemikModeli) KemikleriBul(model);
            for (int i = 0; i < 2; i++)
            {
                if (i >= adet)
                {
                    if (eldeki[i] != null) eldeki[i].SetActive(false);
                    continue;
                }
                string tur = KeseGorsel.Tur(tasinan[i]);
                if (eldeki[i] == null || eldekiTur[i] != tur)
                {
                    if (eldeki[i] != null) Destroy(eldeki[i]);
                    eldeki[i] = KeseGorsel.Yap(tur);
                    eldekiTur[i] = tur;
                    if (eldeki[i] == null) continue;
                }
                eldeki[i].SetActive(true);
                Tasi(eldeki[i], tur, model.transform, i == 0 ? sagEl : solEl, i == 0 ? sagDirsek : solDirsek, i == 0 ? 1f : -1f);
            }
        }

        void Tasi(GameObject kese, string tur, Transform govde, Transform el, Transform dirsek, float taraf)
        {
            Vector3 yukari = Vector3.back;                       // haritada yukarısı −Z
            Vector3 elP;
            if (el != null)
            {
                elP = el.position;
                if (dirsek != null && el.childCount == 0 && el.name.ToLowerInvariant().Contains("forearm"))
                    elP = el.position + (el.position - dirsek.position) * 0.9f;     // ön kol: bileğe uzat
            }
            else
            {
                // kemik yoksa: gövdenin yanında, kalça hizasında
                elP = govde.position + govde.right * taraf * CharacterVisuals.TargetHeight * 0.17f
                      + yukari * CharacterVisuals.TargetHeight * 0.42f;
            }
            Vector3 merkez = govde.position;
            Vector3 dis = Vector3.ProjectOnPlane(elP - merkez, yukari);
            if (dis.sqrMagnitude < 1e-8f) dis = govde.right * taraf;
            dis.Normalize();
            float boy = (Boy.TryGetValue(tur, out var b) ? b : 0.3f) * KeseGorsel.Olcek;
            // kese elden sarkar: ağzı elde, tabanı aşağıda; geniş yüzü bacağa paralel, biraz dışarıda
            Vector3 taban = elP - yukari * boy + dis * KeseGorsel.Olcek * 0.05f;
            float zemin = Zemin;
            if (Vector3.Dot(taban, yukari) < zemin) taban -= yukari * (Vector3.Dot(taban, yukari) - zemin);
            KeseGorsel.Yerlestir(kese, taban, yukari, dis);
        }

        void KemikleriBul(GameObject model)
        {
            kemikModeli = model;
            sagEl = sagDirsek = solEl = solDirsek = null;
            if (model == null) return;
            Transform sagOn = null, solOn = null, sagUst = null, solUst = null;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant().Replace("_", "").Replace(".", "").Replace(" ", "");
                bool sag = n.Contains("right") || n.EndsWith("r") && (n.Contains("hand") || n.Contains("arm"));
                bool sol = n.Contains("left") || n.EndsWith("l") && (n.Contains("hand") || n.Contains("arm"));
                if (n.Contains("hand") && !n.Contains("index") && !n.Contains("thumb") && !n.Contains("middle") && !n.Contains("ring") && !n.Contains("pinky"))
                {
                    if (sag && sagEl == null) sagEl = t;
                    else if (sol && solEl == null) solEl = t;
                }
                else if (n.Contains("forearm"))
                {
                    if (sag && sagOn == null) sagOn = t;
                    else if (sol && solOn == null) solOn = t;
                }
                else if (n.Contains("arm") && !n.Contains("forearm"))
                {
                    if (sag && sagUst == null) sagUst = t;
                    else if (sol && solUst == null) solUst = t;
                }
            }
            if (sagEl != null) sagDirsek = sagOn; else { sagEl = sagOn; sagDirsek = sagUst; }
            if (solEl != null) solDirsek = solOn; else { solEl = solOn; solDirsek = solUst; }
        }
    }
}
