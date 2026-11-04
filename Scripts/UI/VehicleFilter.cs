using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Kurye listesi ve başvurular için araç filtresi: Hepsi / Bisiklet / E-scooter / Motosiklet / Araba.</summary>
    public partial class GameUI
    {
        int courierFilter = -1;        // -1 = hepsi, yoksa (int)VehicleKind
        int applicationFilter = -1;

        static readonly VehicleKind[] FilterKinds =
            { VehicleKind.Bisiklet, VehicleKind.EScooter, VehicleKind.Motosiklet, VehicleKind.Araba };

        /// <summary>Filtre butonları; her butonda o araçtaki sayı yazar. Seçim deferred ile değişir (IMGUI düzeni bozulmasın).</summary>
        void VehicleFilterBar(int current, System.Func<VehicleKind, int> countOf, int total, System.Action<int> set)
        {
            GUILayout.BeginHorizontal();
            if (SelectButton(current < 0, $"Hepsi ({total})", smallButton)) deferred = () => set(-1);
            foreach (var v in FilterKinds)
            {
                int n = countOf(v);
                int id = (int)v;
                if (SelectButton(current == id, $"{GameData.Vehicle(v).name} ({n})", smallButton)) deferred = () => set(id);
            }
            GUILayout.EndHorizontal();
        }

        static bool PassesFilter(int filter, VehicleKind v) => filter < 0 || (int)v == filter;

        int CourierCountByVehicle(VehicleKind v)
        {
            int n = 0;
            foreach (var c in CourierManager.I.Couriers) if (c.vehicle == v) n++;
            return n;
        }

        int ApplicationCountByVehicle(VehicleKind v)
        {
            int n = 0;
            foreach (var a in CourierManager.I.Applications) if (a.vehicle == v) n++;
            return n;
        }
    }
}
