using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Haritada sipariş yoğunluğu katmanı (bkz. <see cref="OrderHeat"/>). Alttaki çubuktaki "Yoğunluk" butonu ya da H tuşu:
    /// kapalı → siparişler (nereden sipariş geliyor: mavi az, sarı orta, kırmızı çok) → kaçanlar (nerede sipariş kaybediliyor) → kapalı.
    /// Bölge açarken ve dükkan seçerken tahmin yerine veriye bakmak için.
    /// </summary>
    public partial class GameUI
    {
        enum HeatMode { Kapali, Siparis, Kacan }
        HeatMode heatMode;

        int[] heatCells;
        int heatMax, heatTotal;
        float heatRefreshAt;
        static Texture2D heatBlob;

        void CycleHeat() => heatMode = (HeatMode)(((int)heatMode + 1) % 3);

        string HeatButtonText =>
            heatMode == HeatMode.Siparis ? "Yoğunluk: sipariş" : heatMode == HeatMode.Kacan ? "Yoğunluk: kaçan" : "Yoğunluk";

        /// <summary>Alttaki çubukta, buton yanındaki açıklama (katman açıkken).</summary>
        string HeatCaption =>
            heatMode == HeatMode.Siparis
                ? $"Son 7 gün · {heatTotal} sipariş · mavi az → kırmızı çok · H: sonraki"
                : $"Son 7 gün · {heatTotal} kaçan sipariş (iptal, pahalı bulan, yoğunluk) · H: kapat";

        static Texture2D HeatBlob
        {
            get
            {
                if (heatBlob != null) return heatBlob;
                const int size = 64;
                heatBlob = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[size * size];
                float r = size / 2f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                        float a = Mathf.Clamp01(1f - d);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * (3f - 2f * a) * 255f)); // yumuşak kenar
                    }
                heatBlob.SetPixels32(px);
                heatBlob.Apply();
                return heatBlob;
            }
        }

        static Color HeatColor(float t)
        {
            // mavi → sarı → kırmızı
            var low = new Color(0.25f, 0.6f, 1f);
            var mid = new Color(1f, 0.85f, 0.2f);
            var high = new Color(1f, 0.25f, 0.2f);
            return t < 0.5f ? Color.Lerp(low, mid, t * 2f) : Color.Lerp(mid, high, (t - 0.5f) * 2f);
        }

        /// <summary>Dünya etiketlerinin altında çizilir.</summary>
        void DrawHeat()
        {
            if (heatMode == HeatMode.Kapali || Event.current.type != EventType.Repaint) return;
            var cam = Camera.main;
            if (cam == null) return;
            if (heatCells == null) heatCells = new int[OrderHeat.CellsX * OrderHeat.CellsY];
            if (Time.unscaledTime >= heatRefreshAt)
            {
                heatRefreshAt = Time.unscaledTime + 0.5f;
                heatMax = OrderHeat.Sum(heatMode == HeatMode.Kacan, heatCells, out heatTotal);
            }
            if (heatMax <= 0) return;

            float left = MapLeftGui;
            var old = GUI.color;
            for (int i = 0; i < heatCells.Length; i++)
            {
                int n = heatCells[i];
                if (n <= 0) continue;
                var tile = OrderHeat.CellCenter(i);
                Vector3 a = cam.WorldToScreenPoint(CityMap.ToWorld(tile));
                Vector3 b = cam.WorldToScreenPoint(CityMap.ToWorld(tile) + new Vector3(OrderHeat.Cell * 0.5f, 0f, 0f));
                if (a.z < 0f) continue;
                float x = a.x / uiScale, y = (Screen.height - a.y) / uiScale;
                float rad = Mathf.Max(10f, Vector2.Distance(a, b) / uiScale) * 1.5f;
                if (x + rad < left || y + rad < TopBar) continue;
                float t = Mathf.Sqrt(n / (float)heatMax);   // karekök: az siparişli yerler de görünsün
                var col = heatMode == HeatMode.Kacan ? new Color(1f, 0.3f, 0.25f) : HeatColor(t);
                col.a = 0.25f + 0.5f * t;
                float s = rad * (0.75f + 0.5f * t);
                GUI.color = col;
                GUI.DrawTexture(new Rect(x - s, y - s, s * 2f, s * 2f), HeatBlob);
            }
            GUI.color = old;
        }
    }
}
