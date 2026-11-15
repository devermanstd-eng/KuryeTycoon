using UnityEditor;
using UnityEngine;

namespace KuryeTycoon.EditorTools
{
    /// <summary>Oyun açıkken (Play) hızlı deneme komutları. Sadece editörde; oyuna girmez.</summary>
    public static class GelistiriciAraclari
    {
        [MenuItem("Kurye Tycoon/Deneme: Bütün kuryeleri arabaya bindir")]
        static void HepsiArabali()
        {
            if (!Application.isPlaying || CourierManager.I == null)
            {
                EditorUtility.DisplayDialog("Kurye Tycoon", "Önce Play'e bas ve bir oyun başlat.", "Tamam");
                return;
            }
            int n = 0;
            foreach (var c in CourierManager.I.Couriers)
            {
                c.ownedVehicles.Add(VehicleKind.Araba);
                c.vehicle = VehicleKind.Araba;
                c.RebuildModel();
                n++;
            }
            Debug.Log($"Kurye Tycoon: {n} kurye arabaya bindirildi (araç modeli: {(CharacterVisuals.CarAvailable ? "var" : "YOK")}).");
        }
    }
}
