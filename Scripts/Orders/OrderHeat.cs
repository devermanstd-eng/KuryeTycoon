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
        // Teslimat süresi katmanı: hücreye teslim edilen sipariş sayısı ve toplam süresi (dk)
        static readonly int[,] delivered = new int[Days, CellsX * CellsY];
        static readonly float[,] minutes = new float[Days, CellsX * CellsY];

        /// <summary>Yeni oyun ya da yüklenen oyun başında sıfırlanır.</summary>
        public static void Reset()
        {
            System.Array.Clear(orders, 0, orders.Length);
            System.Array.Clear(lost, 0, lost.Length);
            System.Array.Clear(delivered, 0, delivered.Length);
            System.Array.Clear(minutes, 0, minutes.Length);
            for (int i = 0; i < Days; i++) slotDay[i] = -1;
        }

        /// <summary>Bugünün yuvası (gün değiştiyse temizlenir).</summary>
        static int Slot()
        {
            int day = Today;
            int slot = ((day % Days) + Days) % Days;
            if (slotDay[slot] != day)
            {
                for (int i = 0; i < CellsX * CellsY; i++) { orders[slot, i] = 0; lost[slot, i] = 0; delivered[slot, i] = 0; minutes[slot, i] = 0f; }
                slotDay[slot] = day;
            }
            return slot;
        }

        static int CellOf(Vector2Int tile) =>
            Mathf.Clamp(tile.y / Cell, 0, CellsY - 1) * CellsX + Mathf.Clamp(tile.x / Cell, 0, CellsX - 1);

        /// <summary>Teslim edilen sipariş: müşterinin yeri ve sipariş verildiğinden teslime kadar geçen süre.</summary>
        public static void RecordDelivery(Vector2Int tile, float dk)
        {
            int slot = Slot(), i = CellOf(tile);
            delivered[slot, i]++;
            minutes[slot, i] += dk;
        }

        /// <summary>Son 7 günde hücre başına ortalama teslimat süresi (dk) ve teslimat sayısı. Dönüş: toplam teslimat.</summary>
        public static int AvgMinutes(float[] avg, int[] count)
        {
            System.Array.Clear(avg, 0, avg.Length);
            System.Array.Clear(count, 0, count.Length);
            int day = Today, total = 0;
            for (int s = 0; s < Days; s++)
            {
                if (slotDay[s] < 0 || slotDay[s] > day || slotDay[s] <= day - Days) continue;
                for (int i = 0; i < avg.Length; i++) { avg[i] += minutes[s, i]; count[i] += delivered[s, i]; }
            }
            for (int i = 0; i < avg.Length; i++)
            {
                if (count[i] > 0) avg[i] /= count[i];
                total += count[i];
            }
            return total;
        }

        static int Today => GameClock.I != null ? GameClock.I.Day : 0;

        public static void Record(Vector2Int tile, bool isLost)
        {
            int slot = Slot(), i = CellOf(tile);
            if (isLost) lost[slot, i]++;
            else orders[slot, i]++;
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

    /// <summary>
    /// Dükkan kârlılığı katmanı: her sözleşmeli dükkanın son 7 gündeki kârı (gelir − kurye ödemesi − boost − stok).
    /// Kayda yazılmaz: yüklemeden sonra yeniden birikir.
    /// </summary>
    public static class DukkanKari
    {
        const int Days = 7;
        static readonly System.Collections.Generic.Dictionary<Shop, long[]> kar = new System.Collections.Generic.Dictionary<Shop, long[]>();
        static readonly System.Collections.Generic.Dictionary<Shop, int[]> adet = new System.Collections.Generic.Dictionary<Shop, int[]>();
        static readonly int[] slotDay = new int[Days];

        public static void Reset()
        {
            kar.Clear();
            adet.Clear();
            for (int i = 0; i < Days; i++) slotDay[i] = -1;
        }

        public static void Record(Order o)
        {
            if (o == null || o.shop == null) return;
            int day = GameClock.I != null ? GameClock.I.Day : 0;
            int slot = ((day % Days) + Days) % Days;
            if (slotDay[slot] != day)
            {
                foreach (var v in kar.Values) v[slot] = 0;
                foreach (var v in adet.Values) v[slot] = 0;
                slotDay[slot] = day;
            }
            if (!kar.TryGetValue(o.shop, out var k)) { k = new long[Days]; kar[o.shop] = k; adet[o.shop] = new int[Days]; }
            k[slot] += o.Profit;
            adet[o.shop][slot]++;
        }

        /// <summary>Son 7 günün günlük ortalama kârı ve toplam teslimatı.</summary>
        public static long GunlukKar(Shop s, out int teslimat)
        {
            teslimat = 0;
            if (s == null || !kar.TryGetValue(s, out var k)) return 0;
            int day = GameClock.I != null ? GameClock.I.Day : 0;
            long t = 0; int gun = 0;
            for (int i = 0; i < Days; i++)
            {
                if (slotDay[i] < 0 || slotDay[i] > day || slotDay[i] <= day - Days) continue;
                t += k[i]; teslimat += adet[s][i]; gun++;
            }
            return gun > 0 ? t / gun : 0;
        }
    }
}
