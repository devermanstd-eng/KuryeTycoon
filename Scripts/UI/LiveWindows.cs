using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Yorumlar, Kendi Marketin ve İstatistikler pencerelerinin ortak durumu (filtre, dönem) ve İstatistik gruplaması (BuildStats);
    /// pencerelerin kendisi UI Toolkit'te (Arayuz/UiMesajlar, UiMarket, UiIstatistik). Burada ayrıca eski arayüzün (F8)
    /// Dükkanlar listesindeki kendi market özeti ve haritada şehir etkinliği işareti var (bina kartı: Arayuz/UiBinaKarti.cs).
    /// </summary>
    public partial class GameUI
    {
        // ================================================================ Mesajlar

        int msgFilter;   // 0 tümü, 1 kurye, 2 yorum

        // ================================================================ Kendi marketin

        /// <summary>Eski arayüzde (F8) Dükkanlar listesinde kendi marketinin kısa özeti (dükkan kartı yerine).</summary>
        void DrawOwnStoreSummary(bool withTitle)
        {
            var ds = DarkStore.I;
            if (ds == null || !ds.IsOpen) return;
            if (withTitle) GUILayout.Label($"{DarkStore.StoreName}  [Senin]", header);
            GUILayout.Label($"Kendi deponun marketi · {DarkStore.SizeNames[ds.size]} · komisyon yok, brüt marj ~%{DarkStore.Margin * 100f:0}", small);
            GUILayout.Label($"{ShopHours.StatusText(ds.shop.def)} · {ShopHours.HoursText(ds.shop.def)}", small);
            GUILayout.Label($"Stok {Ft(ds.TotalStock)} / {Ft(ds.Capacity)} · bugün {ds.ordersToday} sipariş, brüt kâr {Ft(ds.profitToday)}", small);
            if (GUILayout.Button("Market penceresini aç", smallButton)) deferred += () => { CloseInfo(); OpenTab(Tab.Market); };
        }

        // ================================================================ İstatistikler

        int statPeriod;   // 0 günlük, 1 haftalık, 2 aylık

        class StatRow
        {
            public string label;
            public int days, deliveries, cancelled, lost;
            public long net, income, expenses, cash;
            public float reputation;
            public int couriers, shops;
        }

        List<StatRow> BuildStats(List<DayRecord> days, int mode)
        {
            var rows = new List<StatRow>();
            StatRow cur = null;
            string curKey = null;
            foreach (var d in days)
            {
                string key;
                string lbl;
                if (mode == 0) { key = d.companyDay.ToString(); lbl = ShortDate(d.date); }
                else if (mode == 1) { int w = (d.companyDay - 1) / 7 + 1; key = w.ToString(); lbl = $"{w}. hafta"; }
                else { key = MonthOf(d.date); lbl = key; }
                if (cur == null || key != curKey)
                {
                    cur = new StatRow { label = lbl };
                    rows.Add(cur);
                    curKey = key;
                }
                cur.days++;
                cur.deliveries += d.deliveries;
                cur.cancelled += d.cancelled;
                cur.lost += d.lost;
                cur.net += d.net;
                cur.income += d.income;
                cur.expenses += d.expenses;
                cur.cash = d.cash;
                cur.reputation = d.reputation;
                cur.couriers = d.couriers;
                cur.shops = d.contractedShops;
            }
            int max = mode == 0 ? 30 : mode == 1 ? 16 : 12;
            if (rows.Count > max) rows.RemoveRange(0, rows.Count - max);
            return rows;
        }

        static string ShortDate(string date)
        {
            if (string.IsNullOrEmpty(date)) return "";
            int comma = date.IndexOf(',');
            return comma > 0 ? date.Substring(0, comma) : date;
        }

        static string MonthOf(string date)
        {
            string s = ShortDate(date);
            int sp = s.IndexOf(' ');
            return sp >= 0 ? s.Substring(sp + 1) : s;
        }

        // ================================================================ Haritada şehir etkinliği

        GUIStyle eventPill;

        void DrawCityEventMarker(float sw)
        {
            var ce = CityEvents.I;
            if (ce == null || Event.current.type != EventType.Repaint) return;
            var e = ce.ActiveNow;
            if (e == null) return;
            var cam = Camera.main;
            if (eventPill == null) eventPill = UITheme.Semi(new GUIStyle(small) { alignment = TextAnchor.MiddleLeft, wordWrap = false, fontStyle = FontStyle.Bold });
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            var col = new Color(0.40f, 0.70f, 1f);

            if (e.HasSpot && cam != null && CityMap.I.IsUnlocked(e.Spot))
            {
                var c = ToGui(cam, CityMap.ToWorld(e.Spot));
                var edge = ToGui(cam, CityMap.ToWorld(e.Spot) + new Vector3(e.spotRadius, 0f, 0f));
                float rad = Mathf.Max(18f, Vector2.Distance(c, edge));
                UITheme.Outline(new Rect(c.x - rad, c.y - rad, rad * 2f, rad * 2f), UITheme.WithAlpha(col, 0.35f + pulse * 0.3f), 2f, rad);
                float d = 12f + pulse * 4f;
                UITheme.Box(new Rect(c.x - d / 2f, c.y - d / 2f, d, d), UITheme.WithAlpha(col, 0.85f), d / 2f);
                Icons.Draw(new Rect(c.x - 7f, c.y - 7f, 14f, 14f), Icons.Kind.Calendar, Color.white);
                eventPill.normal.textColor = UITheme.Yazi(Color.white);
                var sz = eventPill.CalcSize(new GUIContent(e.title));
                var pr = new Rect(c.x - sz.x / 2f - 8f, c.y - d / 2f - 26f, sz.x + 16f, 20f);
                UITheme.Box(pr, new Color(0.05f, 0.08f, 0.14f, 0.85f), 10f);
                GUI.Label(new Rect(pr.x + 8f, pr.y, sz.x + 4f, 20f), e.title, eventPill);
            }

            // Sağ üstte etkinlik rozeti (alanı olmayan etkinlikler de görünsün). Yeni arayüzde sağ üstteki durum sütununda.
            if (UiAktif) return;
            string txt = $"{e.title} · {e.TimeText}";
            eventPill.normal.textColor = UITheme.Yazi(UITheme.Text);
            var s2 = eventPill.CalcSize(new GUIContent(txt));
            var br = new Rect(sw - s2.x - 60f, TopBar + 10f, s2.x + 44f, 26f);
            UITheme.Shadowed(br, new Color(0.07f, 0.09f, 0.13f, 0.92f), 13f, 3f);
            Icons.Draw(new Rect(br.x + 10f, br.y + 5f, 16f, 16f), Icons.Kind.Calendar, col);
            GUI.Label(new Rect(br.x + 32f, br.y, s2.x + 6f, 26f), txt, eventPill);
        }
    }
}
