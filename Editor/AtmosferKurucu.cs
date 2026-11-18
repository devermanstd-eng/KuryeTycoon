using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// "Kurye Tycoon > Atmosferi Güncelle": şehrin ışığını sıcak, sakin şehir (cozy) ayarına getirir.
    ///  • Resources/Gorsel/Atmosfer.asset (AtmosferAyari): gün boyu ışık anahtarları (şafak şeftali, gündüz bal rengi,
    ///    uzun altın saat, mercan gün batımı, lavanta alacakaranlık, yumuşak mavi-mor gece), daha açık gölgeler.
    ///  • Resources/Gorsel/AtmosferProfili.asset (VolumeProfile): ton eşleme, sıcak renk filtresi ve beyaz ayarı, bölünmüş
    ///    tonlama (gölgeler serin mor, parlaklar şeftali), sıcak ışıma, kahverengi kenar kararması.
    ///  • KuryeURP: yumuşak gölge açık ve yüksek kalitede mi (değilse açar).
    /// Sahneye ve prefablara hiçbir şey yazmaz: Gorsel3D oyun açılırken bu iki dosyayı okur (yoksa koddaki eski ayar).
    /// Oyun açıkken çalıştırılırsa ışık hemen yenilenir. Dosyalar depoya girer: arkadaşlar menüyü çalıştırmadan aynı görünümü alır.
    /// </summary>
    public static class AtmosferKurucu
    {
        const string Klasor = "Assets/KuryeTycoon/Resources/Gorsel";
        const string AyarYol = Klasor + "/Atmosfer.asset";
        const string ProfilYol = Klasor + "/AtmosferProfili.asset";
        const string PipelineYol = Klasor + "/KuryeURP.asset";

        [MenuItem("Kurye Tycoon/Atmosferi Güncelle")]
        public static void Guncelle()
        {
            if (AssetDatabase.LoadAssetAtPath<AtmosferAyari>(AyarYol) != null &&
                !EditorUtility.DisplayDialog("Atmosferi Güncelle",
                    "Atmosfer ayarı zaten var:\n" + AyarYol + "\n\nInspector'da elle değiştirdiğin değerler sıcak şehir ayarına döner. Devam edilsin mi?",
                    "Güncelle", "Vazgeç"))
                return;

            KlasorHazirla();
            var profil = ProfilYaz();
            var ayar = AyarYaz(profil);
            var notlar = new List<string>();
            GolgeAyari(notlar);
            AssetDatabase.SaveAssets();

            if (Application.isPlaying && Gorsel3D.I != null) Gorsel3D.I.AtmosferiYenile();
            Selection.activeObject = ayar;
            EditorGUIUtility.PingObject(ayar);

            string ek = notlar.Count > 0 ? "\n\n" + string.Join("\n", notlar) : "";
            Debug.Log("Kurye Tycoon: atmosfer güncellendi (" + AyarYol + ", " + ProfilYol + ")." + ek);
            EditorUtility.DisplayDialog("Atmosferi Güncelle",
                "Sıcak şehir atmosferi yazıldı:\n" + AyarYol + "\n" + ProfilYol +
                "\n\nPlay'e basınca (oyun açıksa hemen) yeni ışık görünür. Saatleri, renkleri ve efektleri bu iki dosyanın " +
                "Inspector'ından ayarlayabilirsin. İkisini de GitHub'a gönder." + ek, "Tamam");
        }

        [MenuItem("Kurye Tycoon/Atmosferi Kaldır (koddaki eski ayar)")]
        static void Kaldir()
        {
            if (!EditorUtility.DisplayDialog("Atmosferi Kaldır",
                    "Atmosfer.asset ve AtmosferProfili.asset silinecek; oyun koddaki eski ışık ayarına döner (karşılaştırmak için). " +
                    "Geri getirmek için Atmosferi Güncelle'yi yeniden çalıştırman yeter.", "Sil", "Vazgeç"))
                return;
            AssetDatabase.DeleteAsset(AyarYol);
            AssetDatabase.DeleteAsset(ProfilYol);
            AssetDatabase.SaveAssets();
            if (Application.isPlaying && Gorsel3D.I != null) Gorsel3D.I.AtmosferiYenile();
            Debug.Log("Kurye Tycoon: atmosfer dosyaları silindi; koddaki eski ışık ayarı kullanılıyor.");
        }

        static void KlasorHazirla()
        {
            if (!AssetDatabase.IsValidFolder("Assets/KuryeTycoon/Resources"))
                AssetDatabase.CreateFolder("Assets/KuryeTycoon", "Resources");
            if (!AssetDatabase.IsValidFolder(Klasor))
                AssetDatabase.CreateFolder("Assets/KuryeTycoon/Resources", "Gorsel");
        }

        // ---------------------------------------------------------------- gün boyu ışık

        static Color C(float r, float g, float b) => new Color(r, g, b);

        static AtmosferAyari.IsikAnahtari A(float saat, Color gunes, float siddet, Color gok, Color ufuk, Color zemin, float golge)
            => new AtmosferAyari.IsikAnahtari(saat, gunes, siddet, gok, ufuk, zemin, golge);

        /// <summary>
        /// Sıcak şehir: koddaki eski ayardan daha sıcak tonlar, daha açık gölgeler (0,40–0,62; eskisi 0,45–0,72), daha uzun altın saat
        /// ve gece biraz daha aydınlık (oyun okunur kalsın). Saatler eskisiyle aynı: DayNight'ın gece/gündüz geçişiyle uyumlu.
        /// </summary>
        static AtmosferAyari.IsikAnahtari[] SicakAnahtarlar()
        {
            var ay = C(0.60f, 0.62f, 0.92f);
            var geceGok = C(0.31f, 0.31f, 0.47f);
            var geceUfuk = C(0.31f, 0.26f, 0.35f);
            var geceZemin = C(0.16f, 0.14f, 0.17f);
            return new[]
            {
                A(0f,    ay, 0.32f, geceGok, geceUfuk, geceZemin, 0.40f),
                A(4.9f,  ay, 0.32f, geceGok, geceUfuk, geceZemin, 0.40f),
                // Şafak: şeftali güneş, pembe ufuk
                A(6.0f,  C(1.00f, 0.72f, 0.60f), 0.70f, C(0.52f, 0.50f, 0.64f), C(0.72f, 0.55f, 0.50f), C(0.30f, 0.27f, 0.26f), 0.48f),
                A(7.3f,  C(1.00f, 0.86f, 0.70f), 1.00f, C(0.64f, 0.68f, 0.80f), C(0.73f, 0.64f, 0.56f), C(0.40f, 0.40f, 0.32f), 0.55f),
                // Gündüz: bal rengi güneş, sıcak ufuk, yumuşak gölge
                A(10.4f, C(1.00f, 0.92f, 0.80f), 1.15f, C(0.68f, 0.72f, 0.80f), C(0.73f, 0.64f, 0.55f), C(0.44f, 0.44f, 0.34f), 0.60f),
                A(11.6f, C(1.00f, 0.94f, 0.84f), 1.22f, C(0.70f, 0.74f, 0.80f), C(0.72f, 0.64f, 0.54f), C(0.44f, 0.45f, 0.34f), 0.62f),
                A(16.4f, C(1.00f, 0.92f, 0.80f), 1.18f, C(0.70f, 0.72f, 0.78f), C(0.74f, 0.64f, 0.53f), C(0.44f, 0.43f, 0.33f), 0.62f),
                // Akşam: uzun altın saat → mercan gün batımı → lavanta alacakaranlık
                A(17.4f, C(1.00f, 0.80f, 0.56f), 1.08f, C(0.70f, 0.66f, 0.70f), C(0.80f, 0.64f, 0.50f), C(0.44f, 0.40f, 0.32f), 0.60f),
                A(19.0f, C(1.00f, 0.62f, 0.38f), 0.90f, C(0.64f, 0.52f, 0.62f), C(0.88f, 0.58f, 0.46f), C(0.38f, 0.32f, 0.30f), 0.52f),
                A(20.2f, C(0.86f, 0.58f, 0.76f), 0.55f, C(0.46f, 0.40f, 0.58f), C(0.52f, 0.38f, 0.46f), C(0.24f, 0.21f, 0.24f), 0.45f),
                // Gece: yumuşak mavi-mor ay ışığı
                A(21.0f, ay, 0.32f, geceGok, geceUfuk, geceZemin, 0.40f),
                A(24.0f, ay, 0.32f, geceGok, geceUfuk, geceZemin, 0.40f),
            };
        }

        static AtmosferAyari AyarYaz(VolumeProfile profil)
        {
            var a = AssetDatabase.LoadAssetAtPath<AtmosferAyari>(AyarYol);
            if (a == null)
            {
                a = ScriptableObject.CreateInstance<AtmosferAyari>();
                AssetDatabase.CreateAsset(a, AyarYol);
            }
            a.anahtarlar = SicakAnahtarlar();
            a.golgeSapmasi = 0.02f;
            a.golgeNormalSapmasi = 0.25f;
            a.bulutluGolgeAzalmasi = 0.55f;
            a.geceYansima = 0.15f;
            a.profil = profil;
            EditorUtility.SetDirty(a);
            return a;
        }

        // ---------------------------------------------------------------- ekran efektleri

        static VolumeProfile ProfilYaz()
        {
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilYol);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(p, ProfilYol);
            }
            else Temizle(p);

            var tone = Ekle<Tonemapping>(p);
            tone.mode.Override(TonemappingMode.Neutral);

            var ca = Ekle<ColorAdjustments>(p);
            ca.postExposure.Override(0.1f);
            ca.contrast.Override(8f);
            ca.saturation.Override(10f);
            ca.colorFilter.Override(new Color(1f, 0.975f, 0.94f));        // hafif sıcak filtre

            var wb = Ekle<WhiteBalance>(p);
            wb.temperature.Override(12f);                                    // öğleden sonra güneşi (eskisi 7)
            wb.tint.Override(2f);

            var st = Ekle<SplitToning>(p);                                   // gölgeler serin mor, parlaklar şeftali (nötr: gri)
            st.shadows.Override(new Color(0.44f, 0.44f, 0.56f));
            st.highlights.Override(new Color(0.62f, 0.54f, 0.44f));
            st.balance.Override(-10f);

            var bl = Ekle<Bloom>(p);                                          // lambalar ve pencereler sıcak parlasın
            bl.intensity.Override(0.32f);
            bl.threshold.Override(1.0f);
            bl.scatter.Override(0.65f);
            bl.tint.Override(new Color(1f, 0.92f, 0.82f));

            var vg = Ekle<Vignette>(p);
            vg.intensity.Override(0.2f);
            vg.smoothness.Override(0.55f);
            vg.color.Override(new Color(0.16f, 0.10f, 0.07f));               // siyah değil, sıcak kahve

            EditorUtility.SetDirty(p);
            return p;
        }

        /// <summary>Profil bileşenleri profil dosyasının içinde alt varlık olarak durmalı (yoksa kaydedilmez).</summary>
        static T Ekle<T>(VolumeProfile p) where T : VolumeComponent
        {
            var c = p.Add<T>(true);
            c.name = typeof(T).Name;
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        static void Temizle(VolumeProfile p)
        {
            foreach (var c in p.components.ToArray())
            {
                if (c == null) continue;
                AssetDatabase.RemoveObjectFromAsset(c);
                Object.DestroyImmediate(c, true);
            }
            p.components.Clear();
            p.isDirty = true;
        }

        // ---------------------------------------------------------------- gölge

        /// <summary>Yumuşak gölge açık ve yüksek kalitede mi; değilse açar. Gölge mesafesine ve kademelere dokunmaz (kalite ayarı yönetir).</summary>
        static void GolgeAyari(List<string> notlar)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineYol);
            if (asset == null)
            {
                notlar.Add("Not: 3D ışık ayarı (KuryeURP) yok; önce Kurye Tycoon > 3D Işık ve Gölge Kurulumu'nu çalıştır.");
                return;
            }
            var so = new SerializedObject(asset);
            bool degisti = false;
            var yumusak = so.FindProperty("m_SoftShadowsSupported");
            if (yumusak != null && !yumusak.boolValue) { yumusak.boolValue = true; degisti = true; }
            var kalite = so.FindProperty("m_SoftShadowQuality");
            if (kalite != null && kalite.intValue != 3) { kalite.intValue = 3; degisti = true; }   // yüksek
            if (degisti)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                notlar.Add("KuryeURP: yumuşak gölge açıldı (yüksek kalite).");
            }
        }
    }
}
