using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Kraft kâğıt keseler (Blender: MODEL = "kese", Resources/Keseler). Dükkan çalışanı siparişi açık keseye koyar,
    /// kurye kapanmış keseyi elinde taşır. Sipariş türüne göre kese: market → geniş / büyük (ağırsa), sıcak yemek →
    /// ufak / orta, eczane → ince, diğerleri → orta / uzun.
    /// </summary>
    public static class KeseGorsel
    {
        public const string Klasor = "Keseler";
        /// <summary>Kesenin haritadaki ölçeği: insanlar ~1,8 m → CharacterVisuals.TargetHeight; kese biraz büyütülür ki görünsün.</summary>
        public static float Olcek => CharacterVisuals.TargetHeight / 1.8f * 1.45f;

        static Dictionary<string, GameObject> prefablar;

        static Transform eldekiKlasoru;
        /// <summary>Kuryelerin elindeki keseler sahnenin köküne dağılmasın diye tek bir boş nesnenin altında durur.</summary>
        public static Transform EldekiKlasoru
        {
            get
            {
                if (eldekiKlasoru == null) eldekiKlasoru = new GameObject("Kuryelerin keseleri").transform;
                return eldekiKlasoru;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Sifirla() { prefablar = null; eldekiKlasoru = null; urunMalzemesi.Clear(); }

        static void Yukle()
        {
            if (prefablar != null) return;
            prefablar = new Dictionary<string, GameObject>();
            foreach (var p in Resources.LoadAll<GameObject>(Klasor))
                prefablar[p.name.Replace("Kese_", "")] = p;
        }

        public static bool Var { get { Yukle(); return prefablar.Count > 0 && CharacterVisuals.Mode != 2; } }

        /// <summary>Siparişe göre kesenin türü (aynı sipariş hep aynı kese).</summary>
        public static string Tur(Order o)
        {
            if (o == null) return "orta";
            var d = o.shop != null ? o.shop.def : null;
            bool cift = (o.id & 1) == 0;
            if (d != null && d.eczane) return "ince";
            if (d != null && d.isMarket) return o.IsHeavy ? "buyuk" : (cift ? "genis" : "buyuk");
            if (d != null && d.hotFood) return cift ? "ufak" : "orta";
            return cift ? "orta" : "uzun";
        }

        /// <summary>Kese örneği (ölçekli, gölgesiz, haritanın üstünde çizilen). tur: "acik", "orta" …</summary>
        public static GameObject Yap(string tur, Transform ebeveyn = null)
        {
            Yukle();
            if (!prefablar.TryGetValue(tur, out var p) && !prefablar.TryGetValue("orta", out p)) return null;
            var go = Object.Instantiate(p, ebeveyn, false);
            go.name = "Kese_" + tur;
            go.transform.localScale = Vector3.one * Olcek;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            foreach (var lg in go.GetComponentsInChildren<LODGroup>()) Object.Destroy(lg);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.sortingOrder = 21;
            }
            return go;
        }

        /// <summary>Keseyi dünyada konumlar: tabanı p'de, ağzı "yukari" yönünde, ön yüzü "on" yönüne bakar.</summary>
        public static void Yerlestir(GameObject kese, Vector3 p, Vector3 yukari, Vector3 on)
        {
            on = Vector3.ProjectOnPlane(on, yukari);
            if (on.sqrMagnitude < 1e-6f) on = Vector3.Cross(yukari, Vector3.right);
            // Blender: önü −Y, yukarısı +Z → Unity modelinde önü +Z, yukarısı +Y
            kese.transform.SetPositionAndRotation(p, Quaternion.LookRotation(on.normalized, yukari.normalized));
        }

        // ---- dükkanda keseye konan ürünler (küçük kutu, şişe, kap)
        static readonly Dictionary<Color, Material> urunMalzemesi = new Dictionary<Color, Material>();
        static readonly Color[] UrunRenkleri =
        {
            new Color(0.92f, 0.33f, 0.25f), new Color(0.98f, 0.82f, 0.30f), new Color(0.35f, 0.62f, 0.90f),
            new Color(0.45f, 0.75f, 0.42f), new Color(0.95f, 0.95f, 0.92f), new Color(0.62f, 0.42f, 0.28f),
            new Color(0.80f, 0.45f, 0.75f)
        };

        public static GameObject Urun(System.Random rnd, Color dukkanRengi)
        {
            int tip = rnd.Next(3);
            var go = GameObject.CreatePrimitive(tip == 1 ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            go.name = "Urun";
            Object.Destroy(go.GetComponent<Collider>());
            float o = Olcek;
            // metre cinsinden boy: kutu 7–10 cm, şişe 4×12 cm, kap 9×5 cm
            // kesenin içine sığsın: derinliği ~10 cm olan keseye göre
            go.transform.localScale = tip == 1 ? new Vector3(0.035f, 0.05f, 0.035f) * o
                                    : tip == 2 ? new Vector3(0.065f, 0.04f, 0.055f) * o
                                    : new Vector3(0.05f + (float)rnd.NextDouble() * 0.02f, 0.065f, 0.045f) * o;
            Color c = rnd.Next(4) == 0 ? dukkanRengi : UrunRenkleri[rnd.Next(UrunRenkleri.Length)];
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Malzeme(c);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingOrder = 21;
            return go;
        }

        static Material Malzeme(Color c)
        {
            if (urunMalzemesi.TryGetValue(c, out var m) && m != null) return m;
            var sh = Shader.Find(CharacterVisuals.Mode == 1 ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
            urunMalzemesi[c] = m;
            return m;
        }
    }
}
