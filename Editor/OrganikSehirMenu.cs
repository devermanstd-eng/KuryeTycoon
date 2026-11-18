using UnityEditor;

namespace KuryeTycoon.EditorTools
{
    /// <summary>Organik şehri (gerçek harita planı: eğri ve çapraz sokaklar, kıvrımlı nehir) aç / kapat. Play'e basınca geçerli olur.</summary>
    public static class OrganikSehirMenu
    {
        const string Yol = "Kurye Tycoon/Organik Şehir (harita planı)";

        [MenuItem(Yol)]
        static void Degistir()
        {
            bool acik = !SehirPlani.Aktif;
            SehirPlani.Ayarla(acik);
            EditorUtility.DisplayDialog("Organik Şehir",
                acik ? "Organik şehir AÇIK. Play'e basınca şehir harita planından kurulur.\nKayıtlar ayrı tutulur (organik_kayit*.json): yeni oyun başlat."
                     : "Organik şehir KAPALI. Eski ızgara şehir kullanılır.", "Tamam");
        }

        [MenuItem(Yol, true)]
        static bool Dogrula()
        {
            Menu.SetChecked(Yol, SehirPlani.Aktif);
            return true;
        }
    }
}
