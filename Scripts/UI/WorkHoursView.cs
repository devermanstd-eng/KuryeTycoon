using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// İşe Alım: adayın çalışma saatleri ve saate göre filtre.
    ///  • Kartta "Çalışma saatleri": Hafta içi 17:00–23:00 · Hafta sonu 11:00–20:00 gibi düz yazı.
    ///  • Başvuruların üstünde saat filtresi: Sabah / Öğlen / Akşam / Gece. Birden fazla seçilebilir; seçilenlerin hepsinde
    ///    çalışan adaylar listelenir (ör. Sabah + Akşam). "Her saat" seçimi temizler.
    /// Saatler elle yazılmaz; kuryenin oyunda çevrimiçi olma kararını verdiği Courier.PreferenceAt'ten hesaplanır.
    /// Aynı saatleri olan günler tek satırda birleşir (Her gün / Hafta içi / Hafta sonu / Pzt–Çar ...);
    /// vardiyalı işçide bu hafta ve gelecek hafta ayrı yazılır.
    /// </summary>
    public partial class GameUI
    {
        const float WorkSure = 0.5f;     // bu ve üstü: o saatte kesin çalışır
        const float WorkMaybe = 0.2f;    // bu ve üstü: bazen çalışır
        const int WorkPartMinHours = 2;  // filtre: o bölümde en az bu kadar saat kesin çalışan aday

        static readonly string[] WorkDayNames = { "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz" };
        static readonly string[] WorkPartNames = { "Sabah", "Öğlen", "Akşam", "Gece" };
        static readonly int[] WorkPartFrom = { 6, 12, 17, 22 };
        static readonly int[] WorkPartTo = { 12, 17, 22, 30 };   // 30 = ertesi gün 06:00

        class WorkInfo
        {
            public readonly List<string> lines = new List<string>();
            public readonly bool[] parts = new bool[4];   // Sabah, Öğlen, Akşam, Gece
        }

        readonly Dictionary<string, WorkInfo> workCache = new Dictionary<string, WorkInfo>();
        int workCacheDay = -1;
        readonly bool[] workParts = new bool[4];   // seçili gün bölümleri; hiçbiri seçili değilse filtre yok

        bool AnyWorkPart => workParts[0] || workParts[1] || workParts[2] || workParts[3];

        // ---------------------------------------------------------------- kart

        void WorkHoursSection(Occupation occ, int seed)
        {
            var clock = GameClock.I;
            if (clock == null) return;
            var info = WorkInfoFor(occ, seed, clock);
            GUILayout.Space(2);
            GUILayout.Label("Çalışma saatleri: " + string.Join("  ·  ", info.lines), small);
        }

        // ---------------------------------------------------------------- filtre

        void WorkPartFilterBar(List<CourierCandidate> apps)
        {
            var clock = GameClock.I;
            if (clock == null || apps.Count < 2) { System.Array.Clear(workParts, 0, 4); return; }
            GUILayout.BeginHorizontal();
            if (SelectButton(!AnyWorkPart, $"Her saat ({apps.Count})", smallButton, GUILayout.Width(100)))
                deferred = () => System.Array.Clear(workParts, 0, 4);
            for (int i = 0; i < 4; i++)
            {
                int k = i, n = 0;
                foreach (var a in apps) if (WorkInfoFor(a.occupation, a.scheduleSeed, clock).parts[i]) n++;
                string range = $"{WorkPartFrom[i]:00}:00–{WorkPartTo[i] % 24:00}:00";
                if (SelectButton(workParts[i], $"{WorkPartNames[i]} ({n})", smallButton)) deferred = () => workParts[k] = !workParts[k];
                if (Event.current.type == EventType.Repaint)
                    HoverTip(GUILayoutUtility.GetLastRect(), $"{WorkPartNames[i]} {range}: bu saatlerde çalışan {n} aday. Birden fazla bölüm seçebilirsin.");
            }
            GUILayout.EndHorizontal();
            if (AnyWorkPart)
            {
                int n = 0;
                foreach (var a in apps) if (PassesWorkFilter(a)) n++;
                var sel = new List<string>();
                for (int i = 0; i < 4; i++) if (workParts[i]) sel.Add(WorkPartNames[i]);
                string what = string.Join(" + ", sel);
                GUILayout.Label(n == 0 ? $"{what} saatlerinin hepsinde çalışan başvuru yok."
                                       : $"{what}: bu bölümlerin hepsinde çalışan {n} aday gösteriliyor.", small);
            }
        }

        bool PassesWorkFilter(CourierCandidate c)
        {
            if (!AnyWorkPart || GameClock.I == null) return true;
            var parts = WorkInfoFor(c.occupation, c.scheduleSeed, GameClock.I).parts;
            for (int i = 0; i < 4; i++) if (workParts[i] && !parts[i]) return false;   // seçilenlerin hepsinde çalışmalı
            return true;
        }

        // ---------------------------------------------------------------- hesap

        WorkInfo WorkInfoFor(Occupation occ, int seed, GameClock clock)
        {
            if (workCacheDay != clock.Day) { workCache.Clear(); workCacheDay = clock.Day; }
            string key = (int)occ + ":" + seed;
            if (workCache.TryGetValue(key, out var cached)) return cached;

            var info = new WorkInfo();
            var week = new byte[7][];
            for (int d = 0; d < 7; d++) week[d] = WorkLevels(occ, seed, clock, d, 0);

            if (AllSame(week, 0, 6))
                info.lines.Add("Her gün " + HoursText(week[0]));
            else
            {
                var nextMon = WorkLevels(occ, seed, clock, 0, 1);
                if (AllSame(week, 0, 4) && !Same(nextMon, week[0]))
                {
                    // Vardiyalı işçi: vardiyası haftalık değişir
                    info.lines.Add("Bu hafta içi " + HoursText(week[0]));
                    info.lines.Add("Gelecek hafta içi " + HoursText(nextMon));
                    AddPart(info, nextMon);
                }
                else AddDayRuns(info, week, 0, 4, "Hafta içi");
                AddDayRuns(info, week, 5, 6, "Hafta sonu");
            }
            for (int d = 0; d < 7; d++) AddPart(info, week[d]);

            workCache[key] = info;
            return info;
        }

        /// <summary>Gün bölümünde (Sabah/Öğlen/Akşam/Gece) en az WorkPartMinHours saat kesin çalışıyorsa işaretle.</summary>
        static void AddPart(WorkInfo info, byte[] lv)
        {
            for (int i = 0; i < 4; i++)
            {
                int n = 0;
                for (int h = WorkPartFrom[i]; h < WorkPartTo[i]; h++) if (lv[h % 24] == 2) n++;
                if (n >= WorkPartMinHours) info.parts[i] = true;
            }
        }

        static void AddDayRuns(WorkInfo info, byte[][] week, int from, int to, string allLabel)
        {
            if (AllSame(week, from, to))
            {
                info.lines.Add($"{allLabel} {HoursText(week[from])}");
                return;
            }
            int d = from;
            while (d <= to)
            {
                int e = d;
                while (e + 1 <= to && Same(week[e + 1], week[d])) e++;
                string label = e == d ? WorkDayNames[d] : $"{WorkDayNames[d]}–{WorkDayNames[e]}";
                info.lines.Add($"{label} {HoursText(week[d])}");
                d = e + 1;
            }
        }

        static string HoursText(byte[] lv)
        {
            string sure = HourRanges(lv, 2);
            if (sure.Length > 0) return sure;
            string maybe = HourRanges(lv, 1);
            return maybe.Length > 0 ? $"ara sıra ({maybe})" : "çalışmaz";
        }

        static byte[] WorkLevels(Occupation occ, int seed, GameClock clock, int dow, int weekOffset)
        {
            var lv = new byte[24];
            for (int h = 0; h < 24; h++)
            {
                float p = Courier.PreferenceAt(occ, seed, clock, dow, h, weekOffset);
                lv[h] = (byte)(p >= WorkSure ? 2 : p >= WorkMaybe ? 1 : 0);
            }
            return lv;
        }

        static bool Same(byte[] a, byte[] b)
        {
            for (int i = 0; i < 24; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static bool AllSame(byte[][] week, int from, int to)
        {
            for (int d = from + 1; d <= to; d++) if (!Same(week[d], week[from])) return false;
            return true;
        }

        /// <summary>Seviyesi want olan saatleri "10:00–15:00, 17:00–22:00" gibi yazar; gece yarısını geçen aralık bölünmez (20:00–03:00).</summary>
        static string HourRanges(byte[] lv, byte want)
        {
            int n = 0;
            for (int h = 0; h < 24; h++) if (lv[h] == want) n++;
            if (n == 0) return "";
            if (n == 24) return "bütün gün";
            int start = 0;
            while (lv[start] == want) start++;
            var parts = new List<string>();
            int i = 0;
            while (i < 24)
            {
                int h = (start + i) % 24;
                if (lv[h] != want) { i++; continue; }
                int len = 0;
                while (i < 24 && lv[(start + i) % 24] == want) { i++; len++; }
                int end = h + len;
                parts.Add($"{h:00}:00–{(end == 24 ? 24 : end % 24):00}:00");
            }
            return string.Join(", ", parts);
        }
    }
}
