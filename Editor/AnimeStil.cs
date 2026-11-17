using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// Anime (Ghibli tarzı boyalı) görünüm: tek tıkla aç / kapat.
    ///  • Modeller/Malzemeler'deki URP/Lit malzemeler KuryeTycoon/AnimeToon'a geçer (renk, doku ve ışıma korunur).
    ///  • "Cizgi" kabuk malzemeleri sıcak mürekkep rengine boyanır.
    ///  • Renderer'a "Murekkep Cizgi" tam ekran geçişi eklenir (derinlik + normal kenarları, renk ayarı).
    /// Geri al: malzemeler URP/Lit'e döner, tam ekran geçiş kapanır.
    /// </summary>
    public static class AnimeStil
    {
        const string MalzemeKlasor = "Assets/KuryeTycoon/Modeller/Malzemeler";
        const string GorselKlasor = "Assets/KuryeTycoon/Resources/Gorsel";
        const string RendererYol = GorselKlasor + "/KuryeURP_Renderer.asset";
        const string CizgiMatYol = GorselKlasor + "/MurekkepCizgi.mat";
        const string OzellikAdi = "Murekkep Cizgi";
        public const string ToonShader = "KuryeTycoon/AnimeToon";
        public static readonly Color Murekkep = new Color(0.17f, 0.13f, 0.12f, 1f);   // #2b2120 sıcak koyu kahve

        /// <summary>Anime stil açık mı? (Renderer'daki mürekkep geçişi açıksa.) BlenderBinaKurulum yeni malzemelerde bunu kullanır.</summary>
        public static bool Aktif
        {
            get
            {
                var r = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererYol);
                if (r == null) return false;
                return r.rendererFeatures.Any(f => f != null && f.name == OzellikAdi && f.isActive);
            }
        }

        [MenuItem("Kurye Tycoon/Anime Stil/Uygula")]
        public static void Uygula()
        {
            var toon = Shader.Find(ToonShader);
            if (toon == null)
            {
                EditorUtility.DisplayDialog("Anime Stil", "AnimeToon shader bulunamadı (Resources/Gorsel/AnimeToon.shader). Konsolda derleme hatası var mı?", "Tamam");
                return;
            }
            int sayi = 0, cizgi = 0;
            foreach (var m in Malzemeler())
            {
                if (m.name.StartsWith("Cizgi"))
                {
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Murekkep);
                    m.color = Murekkep;
                    EditorUtility.SetDirty(m);
                    cizgi++;
                    continue;
                }
                if (m.shader == null || (m.shader.name != "Universal Render Pipeline/Lit" && m.shader.name != ToonShader)) continue;
                ToonYap(m, toon);
                sayi++;
            }

            var r = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererYol);
            string not = "";
            if (r == null) not = "\n\nUyarı: renderer bulunamadı; önce 'Kurye Tycoon/3D Işık ve Gölge Kurulumu' çalıştırın.";
            else if (!CizgiGecisi(r, true)) not = "\n\nUyarı: Full Screen Pass Renderer Feature bulunamadı, mürekkep çizgisi eklenemedi.";

            AssetDatabase.SaveAssets();
            Debug.Log($"Kurye Tycoon: anime stil uygulandı — {sayi} malzeme toon, {cizgi} çizgi mürekkep rengi.{not}");
            EditorUtility.DisplayDialog("Anime Stil", $"{sayi} malzeme anime toon'a geçti, {cizgi} çizgi malzemesi mürekkep rengine boyandı, mürekkep çizgisi açıldı.{not}", "Tamam");
        }

        [MenuItem("Kurye Tycoon/Anime Stil/Geri al")]
        public static void GeriAl()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            int sayi = 0;
            foreach (var m in Malzemeler())
            {
                if (m.name.StartsWith("Cizgi"))
                {
                    var c = HexRenk(m.name.Substring(m.name.LastIndexOf('_') + 1), new Color(0.08f, 0.08f, 0.08f));
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                    m.color = c;
                    EditorUtility.SetDirty(m);
                    continue;
                }
                if (m.shader == null || m.shader.name != ToonShader) continue;
                var renk = m.GetColor("_BaseColor");
                var isima = m.GetColor("_EmissionColor");
                m.shader = lit;
                m.SetColor("_BaseColor", renk);
                m.SetColor("_EmissionColor", isima);
                if (isima.maxColorComponent > 0.001f) m.EnableKeyword("_EMISSION");
                m.SetFloat("_Smoothness", m.name.StartsWith("Cam") ? 0.6f : 0.1f);
                EditorUtility.SetDirty(m);
                sayi++;
            }
            var r = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererYol);
            if (r != null) CizgiGecisi(r, false);
            AssetDatabase.SaveAssets();
            Debug.Log($"Kurye Tycoon: anime stil geri alındı — {sayi} malzeme URP/Lit.");
            EditorUtility.DisplayDialog("Anime Stil", $"{sayi} malzeme eski haline (URP/Lit) döndü, mürekkep çizgisi kapatıldı.", "Tamam");
        }

        /// <summary>Bir malzemeyi AnimeToon'a geçirir, tür adına göre ufak ayar yapar (cam parlak kenarlı, yeşillik daha yumuşak).</summary>
        public static void ToonYap(Material m, Shader toon)
        {
            var renk = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
            var isima = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            bool isikAcik = m.IsKeywordEnabled("_EMISSION") || (m.shader != null && m.shader.name == ToonShader);
            m.shader = toon;
            m.SetColor("_BaseColor", renk);
            m.SetColor("_EmissionColor", isikAcik ? isima : Color.black);
            string ad = m.name;
            bool cam = ad.StartsWith("Cam") || ad.StartsWith("OfisCam") || ad.StartsWith("Vitrin");
            bool yesil = ad.StartsWith("Agac") || ad.StartsWith("Yaprak") || ad.StartsWith("Cali") || ad.StartsWith("Cim") || ad.StartsWith("Sarmasik");
            m.SetFloat("_Rim", cam ? 0.45f : 0.22f);
            m.SetFloat("_FircaGuc", yesil ? 0.32f : 0.22f);
            m.SetFloat("_TonFarki", yesil ? 0.14f : 0.09f);
            m.SetFloat("_FircaOlcek", yesil ? 22f : 16f);

            // Boyalı dokular (ada göre): duvar sarmaşık, zemin çim, çatı fırça
            bool sarmasik = Bas(ad, "Duvar", "Siva", "Tas_", "Kulube") && !Bas(ad, "TasAvlu");
            bool cim = Bas(ad, "Cim", "YCim", "KirCim", "KirOt", "Saha", "PCim");
            bool cati = Bas(ad, "Cati", "CatiKoyu", "DuzCati", "Kursun", "Cinko", "Arduvaz", "Kiremit");
            m.SetFloat("_Sarmasik", sarmasik ? 1f : 0f);
            m.SetFloat("_Cim", cim ? 1f : 0f);
            m.SetFloat("_CatiFirca", cati ? 1f : 0f);
            m.SetFloat("_SarmasikBoy", 0.5f);
            EditorUtility.SetDirty(m);
        }

        static bool Bas(string ad, params string[] onekler)
        {
            foreach (var o in onekler)
                if (ad.StartsWith(o, StringComparison.Ordinal)) return true;
            return false;
        }

        static Material[] Malzemeler()
        {
            return AssetDatabase.FindAssets("t:Material", new[] { MalzemeKlasor })
                .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(m => m != null).ToArray();
        }

        /// <summary>Mürekkep tam ekran geçişini ekler (yoksa) ve açar/kapatır.</summary>
        static bool CizgiGecisi(UniversalRendererData renderer, bool ac)
        {
            var mevcut = renderer.rendererFeatures.FirstOrDefault(f => f != null && f.name == OzellikAdi);
            if (mevcut == null)
            {
                if (!ac) return true;
                var tip = TypeCache.GetTypesDerivedFrom<ScriptableRendererFeature>().FirstOrDefault(t => t.Name == "FullScreenPassRendererFeature");
                if (tip == null) return false;
                mevcut = ScriptableObject.CreateInstance(tip) as ScriptableRendererFeature;
                if (mevcut == null) return false;
                mevcut.name = OzellikAdi;
                AssetDatabase.AddObjectToAsset(mevcut, renderer);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mevcut, out string _, out long localId);
                var so = new SerializedObject(renderer);
                var liste = so.FindProperty("m_RendererFeatures");
                var harita = so.FindProperty("m_RendererFeatureMap");
                liste.arraySize++;
                liste.GetArrayElementAtIndex(liste.arraySize - 1).objectReferenceValue = mevcut;
                if (harita != null)
                {
                    harita.arraySize++;
                    harita.GetArrayElementAtIndex(harita.arraySize - 1).longValue = localId;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (ac)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(CizgiMatYol);
                var sh = Shader.Find("KuryeTycoon/MurekkepCizgi");
                if (mat == null && sh != null)
                {
                    mat = new Material(sh) { name = "MurekkepCizgi" };
                    AssetDatabase.CreateAsset(mat, CizgiMatYol);
                }
                if (mat != null) mat.SetColor("_Murekkep", Murekkep);
                var fs = new SerializedObject(mevcut);
                Ayarla(fs, "passMaterial", mat);
                Ayarla(fs, "injectionPoint", 550);          // BeforeRenderingPostProcessing: bloom ve ton eşleme çizgiden sonra
                Ayarla(fs, "requirements", 3);              // Depth | Normal
                Ayarla(fs, "fetchColorBuffer", true);
                Ayarla(fs, "passIndex", 0);
                fs.ApplyModifiedPropertiesWithoutUndo();
            }
            mevcut.SetActive(ac);
            EditorUtility.SetDirty(mevcut);
            EditorUtility.SetDirty(renderer);
            return true;
        }

        static void Ayarla(SerializedObject so, string yol, object deger)
        {
            var p = so.FindProperty(yol);
            if (p == null) { Debug.LogWarning("Kurye Tycoon: ayar bulunamadı: " + yol); return; }
            switch (deger)
            {
                case bool b: p.boolValue = b; break;
                case int i: if (p.propertyType == SerializedPropertyType.Enum) p.intValue = i; else p.intValue = i; break;
                case float f: p.floatValue = f; break;
                case UnityEngine.Object o: p.objectReferenceValue = o; break;
            }
        }

        static Color HexRenk(string hex, Color varsayilan)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : varsayilan;
        }
    }
}
