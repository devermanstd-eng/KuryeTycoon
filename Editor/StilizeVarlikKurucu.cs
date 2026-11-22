using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// Tools/Blender/varlik_uretici.py'nin Art/Stilize'ye ürettiği FBX'lerden malzemeleri ve temiz prefabları kurar.
    /// Sahneye dokunmaz; yalnızca dosya üretir (ekipte sahne çakışması olmasın):
    ///  • İçe aktarma: kamera/ışık yok, mesh okunur (kar katmanı ve gece ışıkları mesh'i okur); kurye Generic iskelet,
    ///    "basic walk", "idle" ve "pedal" döngülü; diğer modellerde animasyon yok.
    ///  • Malzemeler (Art/Stilize/Malzemeler): StilizePalet tek dokulu (mipmap'siz: 32 px renk kareleri komşuya taşmasın);
    ///    düz renkler adındaki renkten (ör. "Cam_86a9ad" → #86a9ad). Anime stil açıksa AnimeToon, değilse URP/Lit.
    ///    "Cizgi" URP/Unlit (ters kabuk dış çizgi); "Isik_", "Cam_", "Vitrin_" gündüz söner, gece GeceIsiklari yakar;
    ///    "SirketRengi" / "SirketRengi2" adları korunur (CharacterVisuals şirket rengine boyar).
    ///  • Prefablar (Resources/Stilize): mevcut şehir ve araç prefablarıyla aynı yapı: kökte model, parçalar ayrı çocuk
    ///    (Teker_On, Teker_Arka, Pedal, Isik), collider yok (oyun tıklamayı haritanın karelerinden bulur, araç prefablarındaki
    ///    collider'ları da siler). Kuryede kökte Animator ve StilizeKuryeAnimator ("walking" ve "pedal" parametreleri;
    ///    "walking" oyunun KuryeAnimator'üyle aynı).
    /// Oyunda: kurye ve bisiklet (CharacterVisuals, Ayarlar'da "Stilize kurye"); diğerleri sonraki adımlarda.
    /// </summary>
    public static class StilizeVarlikKurucu
    {
        public const string MenuYolu = "Kurye Tycoon/Stilize Prefabları Kur";
        const string ModelKlasor = "Assets/KuryeTycoon/Art/Stilize";
        const string MalzemeKlasor = ModelKlasor + "/Malzemeler";
        // Resources altında: oyun kurye ve bisikleti buradan yükler (CharacterVisuals.StilizeFolder)
        const string PrefabKlasor = "Assets/KuryeTycoon/Resources/Stilize";
        const string ControllerYol = PrefabKlasor + "/StilizeKuryeAnimator.controller";
        const string PaletAdi = "StilizePalet";
        const string KarakterAdi = "Kurye";
        static readonly string[] DonguKlipleri = { "walk", "idle", "pedal" };

        [MenuItem(MenuYolu)]
        public static void Kur()
        {
            var yollar = new List<string>();
            if (AssetDatabase.IsValidFolder(ModelKlasor))
                foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { ModelKlasor }))
                {
                    string yol = AssetDatabase.GUIDToAssetPath(g);
                    if (yol.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) &&
                        Path.GetDirectoryName(yol).Replace('\\', '/') == ModelKlasor)
                        yollar.Add(yol);
                }
            if (yollar.Count == 0)
            {
                EditorUtility.DisplayDialog("Stilize Prefabları Kur",
                    "Art/Stilize'de FBX yok.\n\nÖnce modelleri üret: Mac terminalde Tools/varlik_uret.sh ya da " +
                    "Kurye Tycoon > Blender > Stilize varlıkları üret.", "Tamam");
                return;
            }
            yollar.Sort();
            Klasor(MalzemeKlasor);
            Klasor(PrefabKlasor);

            var onbellek = new Dictionary<string, Material>();
            var palet = PaletMalzemesi();
            if (palet != null) onbellek[PaletAdi] = palet;
            var notlar = new List<string>();
            int yapilan = 0;
            foreach (var yol in yollar)
            {
                string ad = Path.GetFileNameWithoutExtension(yol);
                bool karakter = ad == KarakterAdi;
                IceAktarmaAyari(yol, karakter);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(yol);
                if (model == null) { notlar.Add(ad + ": yüklenemedi"); continue; }

                var kopya = Object.Instantiate(model);
                kopya.name = ad;
                MalzemeAta(kopya, onbellek, notlar);
                if (karakter) KarakterKur(kopya, yol, notlar);
                PrefabUtility.SaveAsPrefabAsset(kopya, PrefabKlasor + "/" + ad + ".prefab");
                Object.DestroyImmediate(kopya);
                yapilan++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string mesaj = $"{yapilan} prefab hazır: {PrefabKlasor}\n• {onbellek.Count} malzeme: {MalzemeKlasor}\n" +
                           $"• Gölgelendirici: {(AnimeStil.Aktif ? "AnimeToon (anime stil açık)" : "URP/Lit")}\n" +
                           (notlar.Count > 0 ? "\n" + string.Join("\n", notlar) + "\n" : "") +
                           "\nKurye ve bisiklet oyunda kullanılıyor (Ayarlar > Stilize kurye); diğerleri sonraki adımlarda. " +
                           "Resources/Stilize ve Art/Stilize/Malzemeler'i .meta'larıyla commit'le.";
            Debug.Log("Kurye Tycoon: " + mesaj.Replace("\n", " "));
            EditorUtility.DisplayDialog("Stilize Prefabları Kur", mesaj, "Tamam");
        }

        // ------------------------------------------------------------------ içe aktarma

        static void IceAktarmaAyari(string yol, bool karakter)
        {
            var mi = AssetImporter.GetAtPath(yol) as ModelImporter;
            if (mi == null) return;
            bool degisti = false;
            if (mi.importCameras) { mi.importCameras = false; degisti = true; }
            if (mi.importLights) { mi.importLights = false; degisti = true; }
            if (!mi.isReadable) { mi.isReadable = true; degisti = true; }     // kar katmanı ve gece ışıkları mesh'i okur
            if (karakter)
            {
                if (mi.animationType != ModelImporterAnimationType.Generic) { mi.animationType = ModelImporterAnimationType.Generic; degisti = true; }
                if (mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; degisti = true; }
                if (!mi.importAnimation) { mi.importAnimation = true; degisti = true; }
                if (degisti) { mi.SaveAndReimport(); degisti = false; }

                // Yürüme, bekleme ve pedal döngülü
                var klipler = mi.clipAnimations;
                if (klipler == null || klipler.Length == 0) klipler = mi.defaultClipAnimations;
                bool klipDegisti = false;
                foreach (var k in klipler)
                    if (Dongulu(k.name) && (!k.loopTime || !k.loopPose)) { k.loopTime = true; k.loopPose = true; klipDegisti = true; }
                if (klipDegisti) { mi.clipAnimations = klipler; degisti = true; }
            }
            else
            {
                if (mi.animationType != ModelImporterAnimationType.None) { mi.animationType = ModelImporterAnimationType.None; degisti = true; }
                if (mi.importAnimation) { mi.importAnimation = false; degisti = true; }
            }
            if (degisti) mi.SaveAndReimport();
        }

        static bool Dongulu(string klip)
        {
            string k = klip.ToLowerInvariant();
            return DonguKlipleri.Any(d => k.Contains(d));
        }

        // ------------------------------------------------------------------ malzemeler

        static Shader Golgelendirici(bool isiksiz)
        {
            var s = Shader.Find(isiksiz ? "Universal Render Pipeline/Unlit" : AnimeStil.Aktif ? AnimeStil.ToonShader : "Universal Render Pipeline/Lit");
            return s != null ? s : Shader.Find(isiksiz ? "Unlit/Color" : "Standard");
        }

        static Material MalzemeAl(string yol, Shader shader)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(yol);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, yol);
            }
            else m.shader = shader;
            return m;
        }

        /// <summary>Bütün modellerin ortak renk dokusu: tek malzeme, tek çizim çağrısı.</summary>
        static Material PaletMalzemesi()
        {
            string dokuYol = ModelKlasor + "/" + PaletAdi + ".png";
            if (AssetImporter.GetAtPath(dokuYol) is TextureImporter ti)
            {
                bool d = false;
                if (ti.mipmapEnabled) { ti.mipmapEnabled = false; d = true; }       // 32 px kareler: mipmap komşu renge taşırdı
                if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; d = true; }
                if (ti.filterMode != FilterMode.Bilinear) { ti.filterMode = FilterMode.Bilinear; d = true; }
                if (ti.textureCompression != TextureImporterCompression.Uncompressed) { ti.textureCompression = TextureImporterCompression.Uncompressed; d = true; }
                if (!ti.sRGBTexture) { ti.sRGBTexture = true; d = true; }
                if (d) ti.SaveAndReimport();
            }
            var doku = AssetDatabase.LoadAssetAtPath<Texture2D>(dokuYol);
            if (doku == null) return null;
            var shader = Golgelendirici(false);
            var m = MalzemeAl(MalzemeKlasor + "/" + PaletAdi + ".mat", shader);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", doku);
            m.mainTexture = doku;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            m.color = Color.white;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            if (AnimeStil.Aktif && shader.name == AnimeStil.ToonShader) AnimeStil.ToonYap(m, shader);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void MalzemeAta(GameObject go, Dictionary<string, Material> onbellek, List<string> notlar)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null) mats[i] = Malzeme(mats[i], onbellek, notlar);
                r.sharedMaterials = mats;
            }
        }

        /// <summary>FBX'teki malzemenin yerine adına göre kurulan malzeme (her ad bir kez oluşturulur).</summary>
        static Material Malzeme(Material kaynak, Dictionary<string, Material> onbellek, List<string> notlar)
        {
            string ad = kaynak.name.Replace(" (Instance)", "");
            if (ad.StartsWith(PaletAdi))
            {
                if (onbellek.TryGetValue(PaletAdi, out var p)) return p;
                notlar.Add(PaletAdi + ".png bulunamadı: modeller dokusuz (FBX'in kendi malzemesiyle) kaldı.");
                onbellek[PaletAdi] = kaynak;
                return kaynak;
            }
            if (onbellek.TryGetValue(ad, out var m)) return m;

            bool cizgi = ad.StartsWith("Cizgi");
            bool isikVeren = ad.StartsWith("Isik_") || ad.StartsWith("Cam_") || ad.StartsWith("Vitrin_");
            Color renk = AddanRenk(ad, out bool bulundu);
            if (!bulundu) renk = kaynak.HasProperty("_BaseColor") ? kaynak.GetColor("_BaseColor") : kaynak.color;
            if (cizgi && AnimeStil.Aktif) renk = AnimeStil.Murekkep;

            var shader = Golgelendirici(cizgi);
            m = MalzemeAl(MalzemeKlasor + "/" + ad + ".mat", shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", renk);
            m.color = renk;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", ad.StartsWith("Cam_") || ad.StartsWith("Vitrin_") ? 0.6f : 0.1f);
            if (isikVeren && m.HasProperty("_EmissionColor"))
            {
                // Gündüz ışık yok; gece GeceIsiklari property block ile yakar (şehir binalarındaki camlarla aynı)
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            if (!cizgi && AnimeStil.Aktif && shader.name == AnimeStil.ToonShader) AnimeStil.ToonYap(m, shader);
            EditorUtility.SetDirty(m);
            onbellek[ad] = m;
            return m;
        }

        static Color AddanRenk(string ad, out bool bulundu)
        {
            bulundu = false;
            int i = ad.LastIndexOf('_');
            if (i < 0 || ad.Length - i - 1 < 6) return Color.white;
            if (!int.TryParse(ad.Substring(i + 1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) return Color.white;
            bulundu = true;
            return new Color32((byte)(v >> 16), (byte)((v >> 8) & 255), (byte)(v & 255), 255);
        }

        // ------------------------------------------------------------------ kurye: Animator ve controller

        static void KarakterKur(GameObject kopya, string yol, List<string> notlar)
        {
            var varliklar = AssetDatabase.LoadAllAssetsAtPath(yol);
            var klipler = varliklar.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
            AnimationClip Bul(string ad) => klipler.FirstOrDefault(c => c.name.ToLowerInvariant().Contains(ad));
            var yuru = Bul("walk");
            var bekle = Bul("idle");
            var pedal = Bul("pedal");
            if (yuru == null) notlar.Add("Kurye: yürüme animasyonu bulunamadı (FBX'te \"basic walk\" yok mu?)");

            var anim = kopya.GetComponent<Animator>();
            if (anim == null) anim = kopya.AddComponent<Animator>();
            var avatar = varliklar.OfType<Avatar>().FirstOrDefault();
            if (avatar != null) anim.avatar = avatar;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            anim.runtimeAnimatorController = Controller(yuru, bekle, pedal);
        }

        /// <summary>Bekleme (varsayılan) ↔ yürüme ("walking"), her durumdan pedal ("pedal"). Her kurulumda baştan yazılır.</summary>
        static AnimatorController Controller(AnimationClip yuru, AnimationClip bekle, AnimationClip pedal)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerYol) != null) AssetDatabase.DeleteAsset(ControllerYol);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerYol);
            ctrl.AddParameter("walking", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("pedal", AnimatorControllerParameterType.Bool);
            var sm = ctrl.layers[0].stateMachine;
            var sBekle = sm.AddState("Idle");
            sBekle.motion = bekle != null ? bekle : yuru;
            if (bekle == null) sBekle.speed = 0f;                 // bekleme yoksa yürüme pozunda durur
            var sYuru = sm.AddState("Walk");
            sYuru.motion = yuru;
            var sPedal = sm.AddState("Pedal");
            sPedal.motion = pedal != null ? pedal : sBekle.motion;
            sm.defaultState = sBekle;

            Gecis(sBekle.AddTransition(sYuru), "walking", true);
            Gecis(sYuru.AddTransition(sBekle), "walking", false);
            var herDurum = sm.AddAnyStateTransition(sPedal);
            herDurum.canTransitionToSelf = false;
            Gecis(herDurum, "pedal", true);
            Gecis(sPedal.AddTransition(sBekle), "pedal", false);
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static void Gecis(AnimatorStateTransition t, string parametre, bool deger)
        {
            t.hasExitTime = false;
            t.duration = 0.12f;
            t.AddCondition(deger ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parametre);
        }

        static void Klasor(string yol)
        {
            if (AssetDatabase.IsValidFolder(yol)) return;
            string ust = Path.GetDirectoryName(yol).Replace('\\', '/');
            Klasor(ust);
            AssetDatabase.CreateFolder(ust, Path.GetFileName(yol));
        }
    }
}
