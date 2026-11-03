using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Kuryeler için isteğe bağlı 3D karakter modelleri (ör. Asset Store'daki "City People FREE Samples").
    ///
    /// Modeller repoya KONMAZ (Asset Store lisansı herkese açık repoda paylaşmaya izin vermez).
    /// Her bilgisayarda bir kez: paketi içe aktar → menüden "Kurye Tycoon > Karakterleri Kur".
    /// Bu, karakter prefablarını Assets/Resources/KuryeKarakterleri klasörüne kopyalar; oyun oradan yükler.
    /// Klasör boşsa ya da yoksa kuryeler eskisi gibi daire olarak çizilir.
    /// </summary>
    public static class CharacterVisuals
    {
        public const string ResourceFolder = "KuryeKarakterleri";
        const string PrefKey = "KuryeTycoon.KarakterModu";

        /// <summary>0 = 3D karakter (ışıklı), 1 = 3D karakter (düz renk), 2 = kapalı (daireler).</summary>
        public static int Mode
        {
            get { return PlayerPrefs.GetInt(PrefKey, 0); }
            set { PlayerPrefs.SetInt(PrefKey, value); PlayerPrefs.Save(); }
        }

        public static float Scale = 0.45f;   // modelin harita karesine göre boyu
        static GameObject[] prefabs;
        static Light sun;

        public static int Count
        {
            get
            {
                if (prefabs == null) prefabs = Resources.LoadAll<GameObject>(ResourceFolder);
                return prefabs.Length;
            }
        }

        public static bool Available => Mode != 2 && Count > 0;

        /// <summary>Kuryeye model takar; model yoksa null döner (daire gösterilmeye devam eder).</summary>
        public static GameObject Spawn(Transform parent, string seedName)
        {
            if (!Available) return null;
            int idx = Mathf.Abs(seedName.GetHashCode()) % prefabs.Length;
            var go = Object.Instantiate(prefabs[idx], parent, false);
            go.name = "Karakter";
            go.transform.localScale = Vector3.one * Scale;
            go.transform.localPosition = new Vector3(0f, -0.35f, -1f); // ayaklar karenin altında, sprite'ların önünde
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // yüzü kameraya dönük

            foreach (var col in go.GetComponentsInChildren<Collider>()) Object.Destroy(col);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) Object.Destroy(rb);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (Mode == 1) MakeUnlit(r);
            }
            if (Mode == 0) EnsureLight();
            return go;
        }

        /// <summary>2D renderer'da ışıklı modeller karanlık görünürse: dokuyu ve rengi koruyan düz (unlit) malzeme.</summary>
        static void MakeUnlit(Renderer r)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader == null) return;
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                var src = mats[i];
                var m = new Material(shader);
                if (src.mainTexture != null) m.mainTexture = src.mainTexture;
                if (src.HasProperty("_BaseColor") || src.HasProperty("_Color")) m.color = src.color;
                mats[i] = m;
            }
            r.materials = mats;
        }

        static void EnsureLight()
        {
            if (sun != null) return;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; return; }
            var go = new GameObject("Güneş (karakterler için)");
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(25f, -25f, 0f); // kameranın arkasından, hafif yandan
        }

        /// <summary>Hareket yönüne döndürür ve varsa animasyon parametrelerini ayarlar.</summary>
        public static void UpdateModel(GameObject model, Vector2 delta, bool online, ref float facingY)
        {
            if (model == null) return;
            if (model.activeSelf != online) model.SetActive(online);
            if (!online) return;

            bool moving = delta.sqrMagnitude > 0.000001f;
            if (moving)
            {
                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) facingY = delta.x > 0 ? 90f : 270f; // sağa / sola
                else facingY = delta.y > 0 ? 0f : 180f;                                       // yukarı (sırtı) / aşağı (yüzü)
            }
            var target = Quaternion.Euler(0f, facingY, 0f);
            model.transform.localRotation = Quaternion.Slerp(model.transform.localRotation, target, 0.25f);

            var anim = model.GetComponentInChildren<Animator>();
            if (anim == null || anim.runtimeAnimatorController == null) return;
            foreach (var p in anim.parameters)
            {
                string n = p.name.ToLowerInvariant();
                if (p.type == AnimatorControllerParameterType.Float && (n == "speed" || n == "movespeed" || n == "forward"))
                    anim.SetFloat(p.nameHash, moving ? 1f : 0f);
                else if (p.type == AnimatorControllerParameterType.Bool && (n == "walk" || n == "walking" || n == "iswalking" || n == "move" || n == "moving"))
                    anim.SetBool(p.nameHash, moving);
            }
        }

        /// <summary>Ayar değişince kuryelerin modellerini yeniler.</summary>
        public static void Refresh()
        {
            prefabs = null;
            if (CourierManager.I == null) return;
            foreach (var c in CourierManager.I.Couriers) c.RebuildModel();
        }
    }
}
