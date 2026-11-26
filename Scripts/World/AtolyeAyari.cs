using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Bina Atölyesi'nin (Editor/BinaAtolyesi.cs) ürettiği pastel modellerin oyunda kullanımı: Resources/Gorsel/AtolyeAyari.asset.
    /// Atölye prefablarının adında "_atolye_" geçer; mevcut modeller silinmez, bu ayarlar yalnızca hangisinin seçileceğini belirler.
    /// Dosya yoksa varsayılanlar geçerli.
    /// </summary>
    public class AtolyeAyari : ScriptableObject
    {
        public const string Isaret = "_atolye_";
        const string Yol = "Gorsel/AtolyeAyari";

        [Tooltip("Atölye binaları şehirde kullanılsın. Kapalıyken hiçbiri yerleşmez. Açıkken bölge havuzlarına aday olarak girer; " +
                 "bir dükkânın (ör. Burger Queen) atölye binası varsa o dükkân için yalnız atölye binası seçilir.")]
        public bool binalariKullan = true;
        [Tooltip("Şehir yalnız atölye binalarıyla kurulsun: bölge, ev, kamu, sanayi, kule ve blok havuzlarındaki eski modeller " +
                 "(paket binaları, Londra blokları, referans binaları) kullanılmaz; atölye binası olmayan havuz (ör. çiftlik) eskisi gibi. " +
                 "Bölge havuzlarını Tools/Blender/sehir_tasarimlari.py doldurur (Resources/Atolye/Tasarimlar/sehir_paketi.json).")]
        public bool yalnizAtolye = false;
        [Tooltip("Atölye ağaçları, çalıları ve sokak donatıları kullanılsın. Açıkken atölye ağacı varsa yalnız onlar dikilir; " +
                 "bir donatı türünün (bank, lamba ...) atölye modeli varsa o türde yalnız o kullanılır.")]
        public bool cevreModelleriniKullan = false;
        [Tooltip("Oyunda da modellerin kendi kalem çizgileri (Pastel_Cizgi) çizilsin. Kapalıyken konturu yalnız ekran geçişi " +
                 "(Mürekkep çizgisi) çizer: çift çizgi olmaz. Ekran geçişi kapalıysa bunu açın.")]
        public bool oyundaKalemCizgisi = true;
        [Tooltip("Şehrin zemini pastel görünsün: yol, kaldırım, bordür, şerit, çim ve meydan malzemeleri Bina Atölyesi'nin pastel " +
                 "paletine (Pastel gölgelendiricisi) geçer; organik zeminin dokusu ve çim tutamları da pastel tonlara döner. " +
                 "Yol ağı ve geometri aynı kalır. Kapalıyken eski görünüm.")]
        public bool oyundaPastelZemin = false;

        static AtolyeAyari ayar;
        static bool yuklendi;

        static AtolyeAyari Al()
        {
            if (!yuklendi)
            {
                ayar = Resources.Load<AtolyeAyari>(Yol);
                yuklendi = true;
            }
            return ayar;
        }

        /// <summary>Ayar dosyasını yeniden okur (şehir kurulurken).</summary>
        public static void Yenile() { yuklendi = false; }

        public static bool Binalar => Al() == null || Al().binalariKullan;
        public static bool Cevre => Al() != null && Al().cevreModelleriniKullan;
        public static bool YalnizAtolye => Binalar && Al() != null && Al().yalnizAtolye;
        public static bool OyundaKalemCizgisi => Al() != null && Al().oyundaKalemCizgisi;
        public static bool PastelZemin => Al() != null && Al().oyundaPastelZemin;

        // Zemin malzemesi adının başı → pastel karşılığı (uzun önek önce). Yol parçalarının ve parkların malzemeleri adla eşlenir.
        static readonly string[,] PastelEsleme =
        {
            { "YKaldirim2", "Pastel_Kaldirim_d8d0bf" }, { "YKaldirim", "Pastel_Kaldirim_e6dfd0" }, { "Kaldirim", "Pastel_Kaldirim_e6dfd0" },
            { "YAsfalt", "Pastel_Asfalt_958f9c" }, { "TAsfalt", "Pastel_Asfalt_958f9c" }, { "YolKoyu", "Pastel_Asfalt_878190" },
            { "YolTasi", "Pastel_Meydan_d9cbb6" }, { "TasYol", "Pastel_Meydan_d9cbb6" }, { "TasAvlu", "Pastel_Meydan_cbbba3" }, { "TuglaZemin", "Pastel_Meydan_cbbba3" },
            { "YBordur", "Pastel_Tas_dcd6ca" }, { "YSerit", "Pastel_Silme_fbf7ee" }, { "YBeyaz", "Pastel_Silme_fbf7ee" }, { "Serit", "Pastel_Silme_fbf7ee" },
            { "YCim_koyu", "Pastel_Cim_aed396" }, { "YCim", "Pastel_Cim_bfe0a8" }, { "Cim", "Pastel_Cim_bfe0a8" }, { "YToprak", "Pastel_Toprak_d9c09a" },
        };

        /// <summary>
        /// "Oyunda pastel zemin" açıkken zemin malzemelerini pastel karşılıklarıyla değiştirir (yeni dizi; eşleşme yoksa aynı dizi).
        /// City3D parçanın özgün malzemelerini ilk okurken çağırır: kilit solgunluğu ve gece ışıkları bunun üstüne çalışır.
        /// </summary>
        public static Material[] PastelZeminCevir(Material[] mats)
        {
            if (mats == null || !PastelZemin) return mats;
            Material[] yeni = null;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                string n = m.name;
                if (n.StartsWith("Pastel_")) continue;
                for (int k = 0; k < PastelEsleme.GetLength(0); k++)
                {
                    string onek = PastelEsleme[k, 0];
                    if (!n.StartsWith(onek)) continue;
                    // önekten sonra harf geliyorsa başka malzemedir (ör. "Cimento"): yalnız ad bitişi, '_' ya da boşluk
                    if (n.Length > onek.Length && char.IsLetterOrDigit(n[onek.Length])) continue;
                    if (yeni == null) yeni = (Material[])mats.Clone();
                    yeni[i] = AtolyeKurucu.Malzeme(PastelEsleme[k, 1]);
                    break;
                }
            }
            return yeni ?? mats;
        }
        public static bool AtolyeMi(string ad) => ad != null && ad.Contains(Isaret);
    }
}
