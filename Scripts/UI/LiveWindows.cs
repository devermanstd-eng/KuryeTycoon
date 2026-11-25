using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Yorumlar, Kendi Marketin ve İstatistikler pencerelerinin ortak durumu (filtre, dönem) ve İstatistik gruplaması (BuildStats);
    /// pencerelerin kendisi UI Toolkit'te (Arayuz/UiMesajlar, UiMarket, UiIstatistik). Burada ayrıca haritada şehir etkinliği
    /// işareti var (bina kartı: Arayuz/UiBinaKarti.cs).
    /// </summary>
    public partial class GameUI
    {
        // ================================================================ Mesajlar

        int msgFilter;   // 0 tümü, 1 kurye, 2 yorum

        // ================================================================ Kendi marketin

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

        // Haritadaki şehir etkinliği işareti: Arayuz/UiHaritaEtiketleri.cs (EtkinlikIsareti)
    }
}
