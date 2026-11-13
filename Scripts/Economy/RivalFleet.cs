using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Rakip platformların (Turbo Paket, Panda Futár, Villám Kaja) kuryelerini haritada gösterir. Sadece görseldir: oyun ekonomisi
    /// RivalCompany'de hesaplanır. Kurye sayısı rakiplerin toplam kurye sayısını ve saati (gece az, gündüz çok) izler;
    /// her rakip kurye sayısı oranında görünür ve kendi renginde çizilir.
    /// Her kurye kendi platformunun çalıştığı dükkanlardan birine gider, bekler, şehirde bir binaya teslim eder, tekrarlar.
    /// </summary>
    public class RivalFleet : MonoBehaviour
    {
        public static RivalFleet I { get; private set; }

        public static readonly Color RivalColor = new Color(0.62f, 0.32f, 0.85f);   // eski: tek rakip rengi (Turbo Paket)
        public int maxVisible = 30;

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
            // Hangi platformdan: haritadaki payı kurye sayısının payından en geride olan rakip
            var rc = RivalCompany.I;
            int ri = 0; float worst = float.MaxValue; int total = Mathf.Max(1, rc.couriers);
            int visible = 0;
            foreach (var x in Agents) if (x != null && !x.leaving) visible++;
            for (int i = 0; i < rc.Rakipler.Count; i++)
            {
                int n = 0;
                foreach (var x in Agents) if (x != null && !x.leaving && x.rakip == i) n++;
                float want = rc.Rakipler[i].couriers / (float)total * (visible + 1);
                float d = n - want;
                if (d < worst) { worst = d; ri = i; }
            }
            var go = new GameObject("Rakip kurye");
            go.transform.SetParent(transform, false);
            var a = go.AddComponent<RivalCourierAgent>();
            a.Init(map.GetDoor(tile), Agents.Count, ri);
            Agents.Add(a);
        }
    }
}
