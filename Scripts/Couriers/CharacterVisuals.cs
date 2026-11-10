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

        public static float TargetHeight = 0.36f;  // karakterin boyu (1 = bir kare ≈ bina katı); binaların yanında insan ölçeğinde
        static RuntimeAnimatorController walkController;

        /// <summary>Unity'de "domain reload" kapalıysa statik önbellekler Play'ler arasında kalır; her Play'de sıfırla.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            prefabs = null;
            walkController = null;
            sun = null;
        }

        /// <summary>Yürüme animasyonu kurulu mu (Kurye Tycoon > Karakter Animasyonlarını Kur)?</summary>
        public static bool HasWalkAnimation => LoadController() != null;

        /// <summary>Karakter Animasyonlarını Kur'un oluşturduğu yürüme/bekleme controller'ı (yoksa null). Yayalar da kullanır.</summary>
        public static RuntimeAnimatorController WalkController => LoadController();

        /// <summary>
        /// Blender scriptiyle üretilen kuryelerde "SirketRengi" ve "SirketRengi2" adlı malzemeler (mont, çanta, kask)
        /// oyuncunun seçtiği şirket renklerine boyanır. Başka paketlerin modellerinde bu adlar yok, onlara dokunulmaz.
        /// </summary>
        public static void TintCompanyColors(Renderer r)
        {
            var shared = r.sharedMaterials;
            bool any = false;
            foreach (var m in shared)
                if (m != null && m.name.StartsWith("SirketRengi")) { any = true; break; }
            if (!any) return;
            var mats = r.materials;   // kopya: her kurye kendi malzemesini alır
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null || !m.name.StartsWith("SirketRengi")) continue;
                m.color = m.name.StartsWith("SirketRengi2") ? CompanyProfile.Secondary : CompanyProfile.Primary;
            }
            r.materials = mats;
        }

        static RuntimeAnimatorController LoadController()
        {
            if (walkController == null) walkController = Resources.Load<RuntimeAnimatorController>(ResourceFolder + "/KuryeAnimator");
            return walkController;
        }

        /// <summary>Ayakta duran ve verilen açıya bakan duruş (0 = kuzey, 90 = batı, 180 = güney, 270 = doğu).</summary>
        static Quaternion Facing(float angle) => Quaternion.Euler(0f, 0f, angle) * Quaternion.Euler(-90f, 0f, 0f);
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
            // Harita XY düzleminde, "yukarı" −Z (kameraya doğru). Model ayakta durur, yüzü güneye (kameraya) bakar.
            go.transform.localScale = Vector3.one;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Facing(180f);

            // Paketin kendi scriptleri (ör. CityPeople: Start'ta kendi animasyon durumlarını oynatır) bizim
            // yürüme animatörümüzle çakışır, hata verir ve karakteri idle'da tutar: Start çalışmadan kapat ve sil.
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue; // prefabda eksik (missing) script
                mb.enabled = false;
                Object.Destroy(mb);
            }
            foreach (var col in go.GetComponentsInChildren<Collider>()) Object.Destroy(col);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) Object.Destroy(rb);
            var renderers = go.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                // 2D Renderer çizim sırasını sortingOrder'a göre yapar: haritanın ve dairelerin üstünde çizilsin
                r.sortingOrder = 20;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
                TintCompanyColors(r);              // MakeUnlit rengi kopyaladığı için önce boya
                if (Mode == 1) MakeUnlit(r);
            }

            // Paketteki modelin boyu ne olursa olsun (cm/m) haritada aynı boyda görünsün
            var b = CombinedBounds(renderers);
            if (b.size.z > 0.0001f)
            {
                float s = TargetHeight / b.size.z;
                go.transform.localScale = Vector3.one * s;
                b = CombinedBounds(renderers);
                go.transform.position += new Vector3(parent.position.x - b.center.x, parent.position.y - b.center.y, -0.06f - b.max.z);
            }
            // Yürüme animasyonu: Kurye Tycoon > Karakter Animasyonlarını Kur'un oluşturduğu controller
            var ctrl = LoadController();
            var anim = go.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                if (ctrl != null) anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;             // animasyon kuryeyi yoldan kaydırmasın
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            if (Mode == 0) EnsureLight();
            return go;
        }

        static Bounds CombinedBounds(Renderer[] rs)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var r in rs)
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
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
            // Sahnede ana güneş ayarlıysa onu kullan (Find çağrısı yok: Unity sürümleri arasında API değişti)
            if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional) { sun = RenderSettings.sun; return; }
            var go = new GameObject("Güneş (karakterler için)");
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(25f, -25f, 0f); // kameranın arkasından, hafif yandan
        }

        /// <summary>Hareket yönüne döndürür ve varsa animasyon parametrelerini ayarlar.</summary>
        public static void UpdateModel(GameObject model, Vector2 delta, bool walking, bool visible, ref float facingY)
        {
            if (model == null) return;
            if (model.activeSelf != visible) model.SetActive(visible);
            if (!visible) return;

            // Yürüme: kurye yoldaysa (dükkana / müşteriye gidiyor) ve oyun duraklatılmamışsa
            bool moving = walking;
            if (delta.sqrMagnitude > 0.000001f)
            {
                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) facingY = delta.x > 0 ? 270f : 90f; // doğu / batı
                else facingY = delta.y > 0 ? 0f : 180f;                                       // kuzey (sırtı) / güney (yüzü)
            }
            var target = Facing(facingY);
            model.transform.localRotation = Quaternion.Slerp(model.transform.localRotation, target, 0.25f);

            var anim = model.GetComponentInChildren<Animator>();
            if (anim == null) return;
            // Animasyon menüsü oyun açıkken çalıştırıldıysa da controller'ı tak
            var ctrl = LoadController();
            if (ctrl != null && anim.runtimeAnimatorController != ctrl)
            {
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
            }
            if (anim.runtimeAnimatorController == null) return;
            // Oyun hızlandıkça adımlar da biraz hızlanır (aşırı değil)
            float gs = GameClock.I != null ? GameClock.I.SpeedMultiplier : 1f;
            anim.speed = moving ? Mathf.Clamp(Mathf.Sqrt(gs), 1f, 3f) : 1f;
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
            walkController = null;
            if (CourierManager.I == null) return;
            foreach (var c in CourierManager.I.Couriers) c.RebuildModel();
        }
    }
}
