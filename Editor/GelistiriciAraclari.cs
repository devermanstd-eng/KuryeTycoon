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

        [MenuItem("Kurye Tycoon/Deneme: Bütün kuryeleri bisiklete bindir")]
        static void HepsiBisikletli()
        {
            if (!Application.isPlaying || CourierManager.I == null)
            {
                EditorUtility.DisplayDialog("Kurye Tycoon", "Önce Play'e bas ve bir oyun başlat.", "Tamam");
                return;
            }
            int n = 0;
            foreach (var c in CourierManager.I.Couriers)
            {
                c.ownedVehicles.Add(VehicleKind.Bisiklet);
                c.vehicle = VehicleKind.Bisiklet;
                c.RebuildModel();
                n++;
            }
            Debug.Log($"Kurye Tycoon: {n} kurye bisiklete bindirildi (kese modeli: {(KeseGorsel.Var ? "var" : "YOK")}).");
        }

        [MenuItem("Kurye Tycoon/Deneme: Keseleri say (konsola yazar)")]
        static void KeseleriSay()
        {
            if (!Application.isPlaying || CourierManager.I == null) return;
            int tasiyan = 0, bekleyen = 0, kese = 0, hazirlik = 0;
            foreach (var c in CourierManager.I.Couriers)
            {
                if (c.CarryingList.Count > 0 && !c.ArabaModeli) tasiyan++;
                if (c.State == CourierState.Bekliyor) bekleyen++;
            }
#if UNITY_6000_4_OR_NEWER
            var hepsi = Object.FindObjectsByType<Transform>();
#else
            var hepsi = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
#endif
            foreach (var go in hepsi)
            {
                if (go.name.StartsWith("Kese_") && go.gameObject.activeInHierarchy) kese++;
                if (go.name.StartsWith("Paket hazırlığı") && go.gameObject.activeInHierarchy) hazirlik++;
            }
            Debug.Log($"Kurye Tycoon: elinde paket olan (arabasız) {tasiyan} kurye, dükkanda bekleyen {bekleyen}, " +
                      $"görünen kese {kese}, dükkan önünde hazırlık {hazirlik} (yakınlaşınca görünür).");
        }

        [MenuItem("Kurye Tycoon/Deneme: Araç kapılarını açık tut (aç-kapa)")]
        static void KapilariAcikTut()
        {
            AracAnimasyon.AcikTut = !AracAnimasyon.AcikTut;
            Debug.Log("Kurye Tycoon: araç kapıları " + (AracAnimasyon.AcikTut ? "açık tutuluyor" : "normal"));
        }

        [MenuItem("Kurye Tycoon/Deneme: Kasaya 1 milyon € ekle (P tuşu)")]
        static void ParaEkle()
        {
            if (!Application.isPlaying || EconomyManager.I == null || GameBootstrap.I == null || !GameBootstrap.I.Started)
            {
                EditorUtility.DisplayDialog("Kurye Tycoon", "Önce Play'e bas ve bir oyun başlat.", "Tamam");
                return;
            }
            EconomyManager.I.AddSermaye(DebugReport.DebugPara, "Debug: menü");
            Telemetry.Add("Debug: P ile para");
            EventManager.I.Notify($"Debug: kasaya {GameData.Ft(DebugReport.DebugPara)} eklendi (menü).");
            Debug.Log($"Kurye Tycoon: kasaya {GameData.Ft(DebugReport.DebugPara)} eklendi.");
        }
    }
}
