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
        [Tooltip("Atölye ağaçları, çalıları ve sokak donatıları kullanılsın. Açıkken atölye ağacı varsa yalnız onlar dikilir; " +
                 "bir donatı türünün (bank, lamba ...) atölye modeli varsa o türde yalnız o kullanılır.")]
        public bool cevreModelleriniKullan = false;
        [Tooltip("Oyunda da modellerin kendi kalem çizgileri (Pastel_Cizgi) çizilsin. Kapalıyken konturu yalnız ekran geçişi " +
                 "(Mürekkep çizgisi) çizer: çift çizgi olmaz. Ekran geçişi kapalıysa bunu açın.")]
        public bool oyundaKalemCizgisi = false;

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
        public static bool OyundaKalemCizgisi => Al() != null && Al().oyundaKalemCizgisi;
        public static bool AtolyeMi(string ad) => ad != null && ad.Contains(Isaret);
    }
}
