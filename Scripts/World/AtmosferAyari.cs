using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace KuryeTycoon
{
    /// <summary>
    /// Şehrin atmosferi: gün boyu ışık anahtarları (güneş rengi ve gücü, üç tonlu ortam ışığı, gölge koyuluğu), gölge ayarı ve
    /// ekran efektleri profili (Volume). Resources/Gorsel/Atmosfer.asset'te durur; Gorsel3D açılışta okur.
    ///  • Dosya yoksa Gorsel3D koddaki eski ayarlarla çalışır (oyun bu dosyaya bağlı değil).
    ///  • "Kurye Tycoon > Atmosferi Güncelle" (Editor/AtmosferKurucu.cs) dosyayı sıcak şehir ayarıyla yazar; sonra Inspector'dan
    ///    elle de ayarlanabilir. Sahneye bir şey yazılmaz: ayar bu dosyada ve profil dosyasında.
    ///  • Oyun açıkken değiştirip "Atmosferi Güncelle"ye basınca ışık hemen yenilenir (Gorsel3D.AtmosferiYenile).
    /// </summary>
    [CreateAssetMenu(menuName = "Kurye Tycoon/Atmosfer Ayarı", fileName = "Atmosfer")]
    public class AtmosferAyari : ScriptableObject
    {
        /// <summary>Resources içindeki yol.</summary>
        public const string Yol = "Gorsel/Atmosfer";

        [Serializable]
        public struct IsikAnahtari
        {
            [Range(0f, 24f)] public float saat;
            [Tooltip("Güneşin (gece ayın) rengi")] public Color gunes;
            [Range(0f, 3f)] public float siddet;
            [Tooltip("Ortam ışığı: gökten gelen")] public Color gok;
            [Tooltip("Ortam ışığı: ufuktan gelen")] public Color ufuk;
            [Tooltip("Ortam ışığı: yerden yansıyan")] public Color zemin;
            [Tooltip("Gölgenin koyuluğu (0 yok, 1 tam)"), Range(0f, 1f)] public float golge;

            public IsikAnahtari(float saat, Color gunes, float siddet, Color gok, Color ufuk, Color zemin, float golge)
            {
                this.saat = saat; this.gunes = gunes; this.siddet = siddet;
                this.gok = gok; this.ufuk = ufuk; this.zemin = zemin; this.golge = golge;
            }
        }

        [Header("Gün boyu ışık (saat sırasıyla; ilk 0, son 24 olmalı)")]
        public IsikAnahtari[] anahtarlar = Array.Empty<IsikAnahtari>();

        [Header("Gölge")]
        [Tooltip("Güneş gölgesinin derinlik sapması (gölge sivilcesi / kopması dengesi)")]
        public float golgeSapmasi = 0.02f;
        [Tooltip("Normal yönünde sapma")]
        public float golgeNormalSapmasi = 0.25f;
        [Tooltip("Bulut ve yağmurda gölgenin ne kadar silikleşeceği (0 hiç, 1 tamamen)"), Range(0f, 1f)]
        public float bulutluGolgeAzalmasi = 0.6f;

        [Header("Gece")]
        [Tooltip("Gece camlardaki gök yansıması (gündüz 1)"), Range(0f, 1f)]
        public float geceYansima = 0.12f;

        [Header("Ekran efektleri (ton eşleme, renk, ışıma, kenar kararması)")]
        public VolumeProfile profil;

        /// <summary>Kullanılabilir mi: en az iki anahtar, saat sırasıyla.</summary>
        public bool Gecerli
        {
            get
            {
                if (anahtarlar == null || anahtarlar.Length < 2) return false;
                for (int i = 1; i < anahtarlar.Length; i++)
                    if (anahtarlar[i].saat < anahtarlar[i - 1].saat) return false;
                return true;
            }
        }

        static AtmosferAyari yuklu;
        static bool arandi;

        /// <summary>Resources'tan bir kez yükler (yoksa null). Yenile: editörden güncellenince yeniden okur.</summary>
        public static AtmosferAyari Yukle(bool yenile = false)
        {
            if (yenile || !arandi)
            {
                arandi = true;
                yuklu = Resources.Load<AtmosferAyari>(Yol);
            }
            return yuklu;
        }
    }
}
