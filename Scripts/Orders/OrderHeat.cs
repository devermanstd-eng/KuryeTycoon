using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Sipariş yoğunluğu: son 7 günde haritanın hangi bölgesinden sipariş geldiği ve nerede sipariş kaçtığı.
    /// Harita 3×3 karelik hücrelere bölünür; her gün için ayrı sayaç tutulur (7 günlük halka), en eski gün düşer.
    /// Kaçan sipariş: iptal, pahalı bulup vazgeçen, taşıyacak kurye olmadığı için alınamayan (müşterinin yeri)
    /// ve yoğunlukta hiç verilmeyen (dükkanın yeri, müşteri henüz belli değil).
    /// Kayda yazılmaz: yüklemeden sonra yeniden birikir.
    /// </summary>
    public static class OrderHeat
    {
        public const int Days = 7;
        public const int Cell = 3;
        public static readonly int CellsX = (CityMap.Width + Cell - 1) / Cell;
        public static readonly int CellsY = (CityMap.Height + Cell - 1) / Cell;

        static readonly int[,] orders = new int[Days, CellsX * CellsY];
        static readonly int[,] lost = new int[Days, CellsX * CellsY];
        static readonly int[] slotDay = new int[Days];   // halkadaki her yuvanın hangi güne ait olduğu

        /// <summary>Yeni oyun ya da yüklenen oyun başında sıfırlanır.</summary>
        public static void Reset()
        {
            System.Array.Clear(orders, 0, orders.Length);
            System.Array.Clear(lost, 0, lost.Length);
            for (int i = 0; i < Days; i++) slotDay[i] = -1;
        }

        static int Today => GameClock.I != null ? GameClock.I.Day : 0;

        public static void Record(Vector2Int tile, bool isLost)
        {
            int cx = Mathf.Clamp(tile.x / Cell, 0, CellsX - 1);
            int cy = Mathf.Clamp(tile.y / Cell, 0, CellsY - 1);
            int day = Today;
            int slot = ((day % Days) + Days) % Days;
            if (slotDay[slot] != day)
            {
                for (int i = 0; i < CellsX * CellsY; i++) { orders[slot, i] = 0; lost[slot, i] = 0; }
                slotDay[slot] = day;
            }
            if (isLost) lost[slot, cy * CellsX + cx]++;
            else orders[slot, cy * CellsX + cx]++;
        }

        /// <summary>Son 7 günün toplamı, hücre başına (dizi her çağrıda doldurulur). Dönüş: en büyük hücre değeri.</summary>
        public static int Sum(bool isLost, int[] into, out int total)
        {
            System.Array.Clear(into, 0, into.Length);
            int day = Today, max = 0;
            total = 0;
            var src = isLost ? lost : orders;
            for (int s = 0; s < Days; s++)
            {
                if (slotDay[s] < 0 || slotDay[s] > day || slotDay[s] <= day - Days) continue;
                for (int i = 0; i < into.Length; i++) into[i] += src[s, i];
            }
            for (int i = 0; i < into.Length; i++)
            {
                total += into[i];
                if (into[i] > max) max = into[i];
            }
            return max;
        }

        /// <summary>Hücrenin ortasındaki kare.</summary>
        public static Vector2Int CellCenter(int index)
        {
            int cx = index % CellsX, cy = index / CellsX;
            return new Vector2Int(Mathf.Min(CityMap.Width - 1, cx * Cell + Cell / 2), Mathf.Min(CityMap.Height - 1, cy * Cell + Cell / 2));
        }
    }
}
