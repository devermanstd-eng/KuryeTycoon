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

        static bool PassesFilter(int filter, VehicleKind v) => filter < 0 || (int)v == filter;

        int CourierCountByVehicle(VehicleKind v)
        {
            int n = 0;
            foreach (var c in CourierManager.I.Couriers) if (c.vehicle == v) n++;
            return n;
        }
    }
}
