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

        /// <summary>
        /// Filtre düğmeleri: "Hepsi" ve her araç için simge + sayı (üstüne gelince aracın adı). Seçim deferred ile değişir
        /// (IMGUI düzeni bozulmasın). trailing verilirse aynı satırın sağına çizilir (ör. Kartlar / Tablo).
        /// </summary>
        void VehicleFilterBar(int current, System.Func<VehicleKind, int> countOf, int total, System.Action<int> set, System.Action trailing = null)
        {
            GUILayout.BeginHorizontal();
            if (SelectButton(current < 0, $"Hepsi ({total})", smallButton, GUILayout.Width(88))) deferred = () => set(-1);
            foreach (var v in FilterKinds)
            {
                int n = countOf(v);
                int id = (int)v;
                if (SelectButton(current == id, "      " + n, smallButton, GUILayout.Width(56))) deferred = () => set(id);
                if (Event.current.type == EventType.Repaint)
                {
                    var r = GUILayoutUtility.GetLastRect();
                    var col = current == id ? smallButton.onNormal.textColor : smallButton.normal.textColor;
                    float cw = smallButton.CalcSize(new GUIContent("      " + n)).x - smallButton.padding.horizontal;
                    Icons.Draw(new Rect(r.center.x - cw / 2f, r.center.y - 8f, 16f, 16f), Icons.Of(v), col);
                    HoverTip(r, $"{GameData.Vehicle(v).name}: {n}");
                }
            }
            if (trailing != null)
            {
                GUILayout.FlexibleSpace();
                trailing();
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
