using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Harita katmanları (bkz. <see cref="OrderHeat"/>, <see cref="DukkanKari"/>). Alttaki çubuktaki "Katman" butonu menüyü açar,
    /// H tuşu sırayla geçer:
    ///  • Sipariş yoğunluğu: nereden sipariş geliyor (mavi az → kırmızı çok)
    ///  • Kaçan siparişler: nerede sipariş kaybediliyor
    ///  • Teslimat süresi: hangi mahalleye geç gidiliyor (yeşil hızlı → kırmızı yavaş)
    ///  • Kurye kapsaması: şu an hangi mahalle müsait kuryeden uzak
    ///  • Dükkan kârlılığı: hangi dükkan para kazandırıyor, hangisi zararda (etiketlerde günlük kâr)
    /// Bölge açarken, kurye yerleştirirken ve sözleşme yenilerken tahmin yerine veriye bakmak için.
    /// </summary>
    public partial class GameUI
    {
        enum HeatMode { Kapali, Siparis, Kacan, TeslimSuresi, Kapsama, DukkanKari }
        const int HeatModeCount = 6;
        HeatMode heatMode;
        bool heatMenuOpen;

        int[] heatCells;
        float[] heatAvg;
        int heatMax, heatTotal;
        float heatGenelOrt;
        int heatOnline, heatZararda;
        float heatRefreshAt;
        static Texture2D heatBlob;

        void CycleHeat()
        {
            heatMode = (HeatMode)(((int)heatMode + 1) % HeatModeCount);
            heatRefreshAt = 0f;
        }

        void SetHeat(HeatMode m)
        {
            heatMode = m;
            heatRefreshAt = 0f;
            heatMenuOpen = false;
        }

        static string HeatName(HeatMode m)
        {
            switch (m)
            {
                case HeatMode.Siparis: return "Sipariş yoğunluğu";
                case HeatMode.Kacan: return "Kaçan siparişler";
                case HeatMode.TeslimSuresi: return "Teslimat süresi";
                case HeatMode.Kapsama: return "Kurye kapsaması";
                case HeatMode.DukkanKari: return "Dükkan kârlılığı";
                default: return "Kapalı";
            }
        }

        string HeatButtonText => heatMode == HeatMode.Kapali ? "Harita katmanı ▲" : HeatName(heatMode) + " ▲";

        /// <summary>Alttaki çubukta, buton yanındaki açıklama (katman açıkken).</summary>
        string HeatCaption
        {
            get
            {
                switch (heatMode)
                {
                    case HeatMode.Siparis:
                        return $"Son 7 gün · {heatTotal} sipariş · mavi az → kırmızı çok · H: sonraki";
                    case HeatMode.Kacan:
                        return $"Son 7 gün · {heatTotal} kaçan sipariş (iptal, pahalı bulan, yoğunluk) · H: sonraki";
                    case HeatMode.TeslimSuresi:
                        return heatTotal == 0 ? "Son 7 günde teslimat yok"
                            : $"Son 7 gün · {heatTotal} teslimat, ortalama {heatGenelOrt:0} dk · yeşil ≤ 25 dk, kırmızı ≥ 45 dk · H: sonraki";
                    case HeatMode.Kapsama:
                        return heatOnline == 0 ? "Şu an müsait kurye yok: bütün harita kırmızı"
                            : $"Şu an · en yakın müsait kuryeye uzaklık ({heatOnline} kurye) · yeşil ≤ 1 km, kırmızı ≥ 2,5 km · H: sonraki";
                    case HeatMode.DukkanKari:
                        return $"Son 7 gün · dükkan başına günlük ortalama kâr (kurye ve stok düşülmüş)" +
                               (heatZararda > 0 ? $" · {heatZararda} dükkan zararda" : "") + " · H: kapat";
                    default: return "";
                }
            }
        }

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

        /// <summary>İyi → kötü: yeşil → sarı → kırmızı (teslimat süresi, kapsama).</summary>
        static Color GoodBadColor(float t)
        {
            var good = new Color(0.3f, 0.85f, 0.45f);
            var mid = new Color(1f, 0.85f, 0.2f);
            var bad = new Color(1f, 0.28f, 0.22f);
            return t < 0.5f ? Color.Lerp(good, mid, t * 2f) : Color.Lerp(mid, bad, (t - 0.5f) * 2f);
        }

        /// <summary>Katman verisini yeniler (yarım saniyede bir).</summary>
        void RefreshHeat()
        {
            int n = OrderHeat.CellsX * OrderHeat.CellsY;
            if (heatCells == null) heatCells = new int[n];
            if (heatAvg == null) heatAvg = new float[n];
            heatMax = 0;
            heatTotal = 0;
            switch (heatMode)
            {
                case HeatMode.Siparis:
                case HeatMode.Kacan:
                    heatMax = OrderHeat.Sum(heatMode == HeatMode.Kacan, heatCells, out heatTotal);
                    break;

                case HeatMode.TeslimSuresi:
                {
                    heatTotal = OrderHeat.AvgMinutes(heatAvg, heatCells);
                    double t = 0;
                    for (int i = 0; i < n; i++) { heatMax = Mathf.Max(heatMax, heatCells[i]); t += heatAvg[i] * heatCells[i]; }
                    heatGenelOrt = heatTotal > 0 ? (float)(t / heatTotal) : 0f;
                    break;
                }

                case HeatMode.Kapsama:
                {
                    // Her açık hücre için en yakın müsait (çevrimiçi, molada değil) kuryeye uzaklık (kare). heatCells: -1 kapalı bölge.
                    var map = CityMap.I;
                    heatOnline = 0;
                    var konumlar = new System.Collections.Generic.List<Vector2Int>();
                    foreach (var c in CourierManager.I.Couriers)
                        if (c.IsOnline && c.State != CourierState.Mola) konumlar.Add(Vector2Int.RoundToInt(c.Position));
                    heatOnline = konumlar.Count;
                    for (int i = 0; i < n; i++)
                    {
                        var tile = OrderHeat.CellCenter(i);
                        if (map == null || !map.IsUnlocked(tile)) { heatCells[i] = -1; continue; }
                        int best = 99;
                        foreach (var p in konumlar) best = Mathf.Min(best, CityMap.Manhattan(tile, p));
                        heatCells[i] = best;
                        heatTotal++;
                    }
                    heatMax = 1;
                    break;
                }

                case HeatMode.DukkanKari:
                    heatZararda = 0;
                    int maxTeslim = 0;
                    foreach (var s in CityMap.I.Shops)
                    {
                        if (!s.contracted) continue;
                        long k = DukkanKari.GunlukKar(s, out int adet);
                        if (adet > 0 && k < 0) heatZararda++;
                        maxTeslim = Mathf.Max(maxTeslim, adet);
                    }
                    heatMax = maxTeslim;
                    break;
            }
        }

        /// <summary>Dünya etiketlerinin altında çizilir.</summary>
        void DrawHeat()
        {
            if (heatMode == HeatMode.Kapali || Event.current.type != EventType.Repaint) return;
            var cam = Camera.main;
            if (cam == null || CityMap.I == null) return;
            if (Time.unscaledTime >= heatRefreshAt)
            {
                heatRefreshAt = Time.unscaledTime + 0.5f;
                RefreshHeat();
            }

            float left = MapLeftGui;
            var old = GUI.color;
            if (heatMode == HeatMode.DukkanKari)
            {
                // Dükkanın yerinde, teslimat sayısıyla büyüyen yeşil (kâr) ya da kırmızı (zarar) leke
                if (heatMax > 0)
                    foreach (var s in CityMap.I.Shops)
                    {
                        if (!s.contracted || !CityMap.I.IsUnlocked(s.tile)) continue;
                        long k = DukkanKari.GunlukKar(s, out int adet);
                        if (adet <= 0) continue;
                        float t = Mathf.Sqrt(adet / (float)heatMax);
                        var col = k >= 0 ? new Color(0.3f, 0.85f, 0.45f) : new Color(1f, 0.28f, 0.22f);
                        col.a = 0.3f + 0.4f * t;
                        if (!HeatScreen(cam, s.tile, 1.6f, left, out float x, out float y, out float rad)) continue;
                        float sz = rad * (0.6f + 0.8f * t);
                        GUI.color = col;
                        GUI.DrawTexture(new Rect(x - sz, y - sz, sz * 2f, sz * 2f), HeatBlob);
                    }
                GUI.color = old;
                return;
            }

            if (heatMax <= 0) return;
            for (int i = 0; i < heatCells.Length; i++)
            {
                int n = heatCells[i];
                Color col;
                float t;
                switch (heatMode)
                {
                    case HeatMode.TeslimSuresi:
                        if (n <= 0) continue;
                        t = Mathf.InverseLerp(22f, 48f, heatAvg[i]);
                        col = GoodBadColor(t);
                        col.a = 0.25f + 0.45f * Mathf.Sqrt(n / (float)heatMax);
                        t = 0.6f;
                        break;
                    case HeatMode.Kapsama:
                        if (n < 0) continue;
                        t = Mathf.InverseLerp(4f, 13f, n);
                        col = GoodBadColor(t);
                        col.a = 0.22f + 0.2f * t;
                        t = 0.5f;
                        break;
                    default:
                        if (n <= 0) continue;
                        t = Mathf.Sqrt(n / (float)heatMax);   // karekök: az siparişli yerler de görünsün
                        col = heatMode == HeatMode.Kacan ? new Color(1f, 0.3f, 0.25f) : HeatColor(t);
                        col.a = 0.25f + 0.5f * t;
                        break;
                }
                if (!HeatScreen(cam, OrderHeat.CellCenter(i), 1.5f, left, out float x, out float y, out float rad)) continue;
                float s = rad * (0.75f + 0.5f * t);
                GUI.color = col;
                GUI.DrawTexture(new Rect(x - s, y - s, s * 2f, s * 2f), HeatBlob);
            }
            GUI.color = old;
        }

        /// <summary>Karenin ekrandaki yeri ve bir hücrelik yarıçap. Ekran dışındaysa false.</summary>
        bool HeatScreen(Camera cam, Vector2Int tile, float k, float left, out float x, out float y, out float rad)
        {
            Vector3 a = cam.WorldToScreenPoint(CityMap.ToWorld(tile));
            Vector3 b = cam.WorldToScreenPoint(CityMap.ToWorld(tile) + new Vector3(OrderHeat.Cell * 0.5f, 0f, 0f));
            x = a.x / uiScale;
            y = (Screen.height - a.y) / uiScale;
            rad = Mathf.Max(10f, Vector2.Distance(a, b) / uiScale) * k;
            if (a.z < 0f) return false;
            return !(x + rad < left || y + rad < TopBar);
        }

        /// <summary>Dükkan kârlılığı katmanında dükkan etiketi: "Ad · +1 240 Ft/gün" (yeşil kâr, kırmızı zarar).</summary>
        bool ShopProfitLabel(Shop s, out string text, out Color color)
        {
            text = null;
            color = Color.white;
            if (heatMode != HeatMode.DukkanKari || !s.contracted) return false;
            long k = DukkanKari.GunlukKar(s, out int adet);
            if (adet == 0) { text = s.Name + " · teslimat yok"; color = new Color(0.6f, 0.6f, 0.65f); return true; }
            text = $"{s.Name} · {(k >= 0 ? "+" : "")}{GameData.Ft(k)}/gün";
            color = k >= 0 ? new Color(0.45f, 0.92f, 0.55f) : new Color(1f, 0.45f, 0.4f);
            return true;
        }

        // ---------------------------------------------------------------- katman menüsü

        Rect HeatMenuRect(float sh)
        {
            float h = HeatModeCount * 28f + 10f;
            return new Rect(DockW + 10f + 378f, sh - 44f - 6f - h, 200f, h);
        }

        void DrawHeatMenu(float sh)
        {
            if (!heatMenuOpen) return;
            var r = HeatMenuRect(sh);
            UITheme.Shadowed(r, new Color(0.07f, 0.075f, 0.09f, 0.94f), 10f, 4f);
            for (int i = 0; i < HeatModeCount; i++)
            {
                var m = (HeatMode)i;
                var row = new Rect(r.x + 5f, r.y + 5f + i * 28f, r.width - 10f, 26f);
                bool sec = m == heatMode;
                if (sec) UITheme.Box(row, UITheme.WithAlpha(UITheme.Accent, 0.3f), 6f);
                else if (row.Contains(Event.current.mousePosition)) UITheme.Box(row, new Color(1f, 1f, 1f, 0.06f), 6f);
                small.normal.textColor = UITheme.Yazi(sec ? Color.white : new Color(0.85f, 0.87f, 0.9f));
                GUI.Label(new Rect(row.x + 10f, row.y + 3f, row.width - 14f, 20f), HeatName(m), small);
                if (GUI.Button(row, GUIContent.none, GUIStyle.none)) { var mm = m; deferred += () => SetHeat(mm); }
            }
            small.normal.textColor = UITheme.Yazi(UITheme.TextDim);
            // Menü dışına tıklayınca kapanır
            var e = Event.current;
            if (e.type == EventType.MouseDown && !r.Contains(e.mousePosition)
                && !new Rect(DockW + 10f + 378f, sh - 44f, 150f, 30f).Contains(e.mousePosition))
                deferred += () => heatMenuOpen = false;
        }
    }
}
