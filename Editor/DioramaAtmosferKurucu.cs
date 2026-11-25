using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// "Kurye Tycoon > Diorama Atmosferi Kur": şehri minyatür maket (diorama) görünümüne getirir. Tek tık; sahneye ve
    /// prefablara hiçbir şey yazmaz, yalnız Resources/Gorsel'deki dosyaları:
    ///  • Diorama.asset (DioramaAyari): tilt-shift gücü, saate göre renk (gündüz bal rengi, altın saat, lacivert gece),
    ///    gündüz yüksek / gece düşük eşikli ışıma, far huzmeleri, ağaç salınımı, ıslak yol, seçim halkası. Inspector'dan ayarlanır.
    ///  • KuryeURP_Renderer: ortam gölgesi (SSAO) temas gölgesine ayarlanır (küçük yarıçap, uzaktan da görünür); yoksa eklenir.
    ///  • Atmosfer.asset: altın saat daha altın, gece derin lacivert (Atmosferi Güncelle ile aynı anahtarlar).
    ///  • AtmosferProfili: ışıma eşiği ve parlak piksel sınırı (gündüz güneşli çatılar parlamasın), alan derinliği bileşeni.
    /// Oyun açıkken çalıştırılırsa hemen uygulanır. Dosyalar depoya girer: arkadaşlar menüyü çalıştırmadan aynı görünümü alır.
    /// Tilt-shift ve ortam gölgesi Ayarlar > Harita'dan ayrıca kapatılabilir (Steam Deck / eski dizüstü).
    /// </summary>
    public static class DioramaAtmosferKurucu
    {
        const string Klasor = "Assets/KuryeTycoon/Resources/Gorsel";
        const string AyarYol = Klasor + "/Diorama.asset";
        const string RendererYol = Klasor + "/KuryeURP_Renderer.asset";
        const string AtmosferYol = Klasor + "/Atmosfer.asset";
        const string ProfilYol = Klasor + "/AtmosferProfili.asset";

        [MenuItem("Kurye Tycoon/Diorama Atmosferi Kur")]
        public static void Kur()
        {
            if (AssetDatabase.LoadAssetAtPath<DioramaAyari>(AyarYol) != null &&
                !EditorUtility.DisplayDialog("Diorama Atmosferi Kur",
                    "Diorama ayarı zaten var:\n" + AyarYol + "\n\nInspector'da elle değiştirdiğin değerler varsayılana döner. Devam edilsin mi?",
                    "Kur", "Vazgeç"))
                return;

            if (!AssetDatabase.IsValidFolder("Assets/KuryeTycoon/Resources"))
                AssetDatabase.CreateFolder("Assets/KuryeTycoon", "Resources");
            if (!AssetDatabase.IsValidFolder(Klasor))
                AssetDatabase.CreateFolder("Assets/KuryeTycoon/Resources", "Gorsel");

            var notlar = new List<string>();
            var ayar = AyarYaz();
            notlar.Add("Diorama.asset: tilt-shift, saate göre renk ve ışıma, far, ağaç salınımı, ıslak yol, seçim halkası");
            Ssao(ayar, notlar);
            Atmosfer(notlar);
            Profil(notlar);
            AssetDatabase.SaveAssets();

            if (Application.isPlaying)
            {
                DioramaAyari.Yukle(true);
                if (Gorsel3D.I != null) Gorsel3D.I.AtmosferiYenile();
                GrafikKalitesi.Uygula();
            }
            Selection.activeObject = ayar;
            EditorGUIUtility.PingObject(ayar);

            string liste = "• " + string.Join("\n• ", notlar);
            Debug.Log("Kurye Tycoon: diorama atmosferi kuruldu.\n" + liste);
            EditorUtility.DisplayDialog("Diorama Atmosferi Kur",
                "Minyatür maket görünümü kuruldu:\n\n" + liste +
                "\n\nPlay'e basınca (oyun açıksa hemen) görünür. Değişen dosyaları GitHub'a gönder (Resources/Gorsel).\n" +
                "Tilt-shift ve ortam gölgesi: Ayarlar > Harita.", "Tamam");
        }

        static DioramaAyari AyarYaz()
        {
            var yeni = ScriptableObject.CreateInstance<DioramaAyari>();      // varsayılan değerler
            var a = AssetDatabase.LoadAssetAtPath<DioramaAyari>(AyarYol);
            if (a == null)
            {
                yeni.name = "Diorama";
                AssetDatabase.CreateAsset(yeni, AyarYol);
                return yeni;
            }
            EditorUtility.CopySerialized(yeni, a);
            a.name = "Diorama";
            Object.DestroyImmediate(yeni);
            EditorUtility.SetDirty(a);
            return a;
        }

        /// <summary>SSAO'yu temas gölgesine ayarlar. Uzaklık 25'ti: kamera 25 birimden uzaktayken ortam gölgesi hiç görünmüyordu.</summary>
        static void Ssao(DioramaAyari a, List<string> notlar)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererYol);
            if (renderer == null)
            {
                notlar.Add("UYARI: KuryeURP_Renderer yok: önce Kurye Tycoon > 3D Işık ve Gölge Kurulumu");
                return;
            }
            ScriptableRendererFeature ssao = null;
            foreach (var f in renderer.rendererFeatures)
                if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") ssao = f;
            if (ssao == null)
            {
                GorselKurulum.SsaoEkle(renderer);
                foreach (var f in renderer.rendererFeatures)
                    if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") ssao = f;
            }
            if (ssao == null)
            {
                notlar.Add("UYARI: ortam gölgesi (SSAO) eklenemedi");
                return;
            }
            var so = new SerializedObject(ssao);
            Ayarla(so, "m_Settings.Intensity", a.ssaoGucu);
            Ayarla(so, "m_Settings.Radius", a.ssaoYaricap);
            Ayarla(so, "m_Settings.Falloff", a.ssaoUzaklik);
            Ayarla(so, "m_Settings.DirectLightingStrength", a.ssaoDogrudanIsik);
            Ayarla(so, "m_Settings.Source", 1);              // derinlik + normal: kenarlar daha temiz
            Ayarla(so, "m_Settings.NormalSamples", 1);       // orta
            Ayarla(so, "m_Settings.Samples", 1);             // orta (8 örnek)
            Ayarla(so, "m_Settings.BlurQuality", 0);         // yüksek (iki yönlü): temas gölgesi binaya taşmasın
            Ayarla(so, "m_Settings.Downsample", false);
            Ayarla(so, "m_Settings.AfterOpaque", false);
            so.ApplyModifiedPropertiesWithoutUndo();
            ssao.SetActive(true);
            EditorUtility.SetDirty(ssao);
            EditorUtility.SetDirty(renderer);
            notlar.Add($"KuryeURP_Renderer: ortam gölgesi güç {a.ssaoGucu:0.0}, yarıçap {a.ssaoYaricap:0.00}, uzaklık {a.ssaoUzaklik:0}");
        }

        static void Atmosfer(List<string> notlar)
        {
            var at = AssetDatabase.LoadAssetAtPath<AtmosferAyari>(AtmosferYol);
            if (at == null)
            {
                notlar.Add("Atmosfer.asset yok (koddaki ışık kullanılıyor): istersen Kurye Tycoon > Atmosferi Güncelle");
                return;
            }
            at.anahtarlar = AtmosferKurucu.SicakAnahtarlar();
            EditorUtility.SetDirty(at);
            notlar.Add("Atmosfer.asset: altın saat ve lacivert gece ışığı");
        }

        static void Profil(List<string> notlar)
        {
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilYol);
            if (p == null)
            {
                notlar.Add("AtmosferProfili yok: renk ve ışıma koddaki profilden");
                return;
            }
            if (p.TryGet(out Bloom bl))
            {
                bl.threshold.Override(1.25f);
                bl.intensity.Override(0.24f);
                bl.clamp.Override(8f);
                EditorUtility.SetDirty(bl);
            }
            if (!p.TryGet(out DepthOfField dof))
            {
                dof = p.Add<DepthOfField>(true);
                dof.name = nameof(DepthOfField);
                dof.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(dof, p);
            }
            dof.mode.Override(DepthOfFieldMode.Off);     // oyunda Diorama açar (kalite ayarına ve kamera odağına göre)
            EditorUtility.SetDirty(dof);
            EditorUtility.SetDirty(p);
            notlar.Add("AtmosferProfili: ışıma eşiği 1,25 ve parlak piksel sınırı 8, alan derinliği bileşeni");
        }

        /// <summary>Alan bu URP sürümünde varsa ayarlar; yoksa sessizce geçer.</summary>
        static void Ayarla(SerializedObject so, string yol, object deger)
        {
            var p = so.FindProperty(yol);
            if (p == null) return;
            switch (deger)
            {
                case bool b: if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = b; break;
                case int i:
                    if (p.propertyType == SerializedPropertyType.Integer || p.propertyType == SerializedPropertyType.Enum) p.intValue = i;
                    break;
                case float f: if (p.propertyType == SerializedPropertyType.Float) p.floatValue = f; break;
            }
        }
    }
}
