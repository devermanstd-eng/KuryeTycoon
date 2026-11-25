using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Bina Atölyesi prefabının iç yerleşim planı (Editor/BinaAtolyesi.cs prefabı kaydederken yazar; DukkanCanli okur).
    /// Konumlar MODEL ekseninde (metre, Z yukarı, ön −Y, arsa ortalı); <see cref="Yerel"/> prefabın parça köküne (LOD0) çevirir.
    ///  • Mobilyaların hepsi öne (−Y) bakar ve hücrenin arka yarısında durur: her hücrenin ön yarısı yürüme koridorudur
    ///    (<see cref="Koridor"/>). "Geçit" hücreler boştur: arkadaki sıraya yalnız geçitlerden geçilir.
    ///  • Yalnızca görsel: kayda girmez, dengeyi etkilemez.
    /// </summary>
    public class AtolyePlani : MonoBehaviour
    {
        public string tur, turGrup, etiket;
        /// <summary>Dükkân mı (kepenk, sipariş poşeti, kurye).</summary>
        public bool siparis;
        public int calisan;
        public float hucre = 2.8f, kat = 3f;
        /// <summary>Parçaların ebeveyni (LOD0) ve ayrı duran parçalar (olmayan null).</summary>
        public Transform kok, cati, ic, kepenk, poset, ampul;
        /// <summary>Zemin kat duvarları: ön (−Y), sağ (+X), arka (+Y), sol (−X).</summary>
        public Transform[] duvar = new Transform[4];
        /// <summary>Zemin hücrelerinin ortaları (model x, y), geçit mi, mobilyası (yoksa boş).</summary>
        public Vector2[] hucreOrta = new Vector2[0];
        public Vector2Int[] hucreNo = new Vector2Int[0];
        public bool[] gecit = new bool[0];
        public string[] mobilya = new string[0];
        public int kapiHucre = -1, kasaHucre = -1;

        public const float KoridorY = -0.7f, CalisanY = 1.05f, KepenkKok = 2.3f;

        /// <summary>Model noktası (metre) → parça kökünün (LOD0) yerel uzayı. Editor/BinaAtolyesiKurucu.Unity ile aynı.</summary>
        public static Vector3 Yerel(Vector3 m) => new Vector3(-m.x, m.z, -m.y);

        public Vector3 Koridor(int i) => new Vector3(hucreOrta[i].x, hucreOrta[i].y + KoridorY, 0f);
        /// <summary>Kapının dışı (kaldırım) ve eşiği.</summary>
        public Vector3 Giris => kapiHucre < 0 ? Vector3.zero : new Vector3(hucreOrta[kapiHucre].x, hucreOrta[kapiHucre].y - hucre * 0.5f - 1.5f, 0f);
        public Vector3 Esik => kapiHucre < 0 ? Vector3.zero : new Vector3(hucreOrta[kapiHucre].x, hucreOrta[kapiHucre].y - hucre * 0.5f, 0f);
        /// <summary>Kasanın arkası (çalışanın yeri).</summary>
        public Vector3 KasaArkasi => kasaHucre < 0 ? Vector3.zero : new Vector3(hucreOrta[kasaHucre].x, hucreOrta[kasaHucre].y + CalisanY, 0f);

        public int HucreBul(Vector2Int no)
        {
            for (int i = 0; i < hucreNo.Length; i++) if (hucreNo[i] == no) return i;
            return -1;
        }
    }
}
