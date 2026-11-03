using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Rakip şirketin (Turbo Paket) kuryelerini haritada gösterir. Sadece görseldir: oyun ekonomisi
    /// RivalCompany'de hesaplanır. Kurye sayısı rakibin kurye sayısını ve saatini (gece az, gündüz çok) izler.
    /// Her kurye rakibin çalıştığı dükkanlardan birine gider, bekler, şehirde bir binaya teslim eder, tekrarlar.
    /// </summary>
    public class RivalFleet : MonoBehaviour
    {
        public static RivalFleet I { get; private set; }

        public static readonly Color RivalColor = new Color(0.62f, 0.32f, 0.85f);
        public int maxVisible = 24;

        public readonly List<RivalCourierAgent> Agents = new List<RivalCourierAgent>();

        void Awake() { I = this; }

        /// <summary>Saate göre rakip kuryelerinin ne kadarı çalışıyor.</summary>
        static float OnlineShare(int hour)
        {
            if (hour < 7) return 0.15f;
            if (hour < 10) return 0.45f;
            if (hour < 15) return 0.8f;
            if (hour < 17) return 0.55f;
            if (hour < 22) return 0.85f;
            return 0.35f;
        }

        void Update()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || RivalCompany.I == null || CityMap.I == null) return;
            int want = Mathf.Min(maxVisible, Mathf.RoundToInt(RivalCompany.I.couriers * OnlineShare(GameClock.I.Hour)));

            // Fazlası işini bitirince (paketsizken) ayrılır, eksikse merkezden değil rastgele bir yoldan katılır
            int active = 0;
            foreach (var a in Agents) if (!a.leaving) active++;
            if (active < want) Spawn();
            else if (active > want)
                foreach (var a in Agents) if (!a.leaving && !a.carrying) { a.leaving = true; break; }

            for (int i = Agents.Count - 1; i >= 0; i--)
                if (Agents[i] == null) Agents.RemoveAt(i);
        }

        void Spawn()
        {
            var map = CityMap.I;
            var tile = map.RandomCustomerTile(new Vector2Int(-10, -10));
            var go = new GameObject("Rakip kurye");
            go.transform.SetParent(transform, false);
            var a = go.AddComponent<RivalCourierAgent>();
            a.Init(map.GetDoor(tile), Agents.Count);
            Agents.Add(a);
        }
    }
}
