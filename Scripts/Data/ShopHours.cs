using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Dükkanların açılış ve kapanış saatleri (gerçek hayattaki gibi). Kapalı dükkana sipariş gelmez;
    /// kapanıştan 15 dakika önce yeni sipariş alınmaz (mutfak / kasa kapanıyor). Verilmiş siparişler yine teslim edilir.
    /// Saatler: hafta içi (Pzt–Cum) ve hafta sonu (Cmt–Paz). Gece yarısını geçen kapanış 24'ten büyük yazılır (02:00 = 26).
    /// 24 saat açık: Burger Queen, McDanold's, Non-Stop Gyros ve bakkallar (Market 24, ABC'ler, éjjel-nappali).
    /// </summary>
    public static class ShopHours
    {
        public struct Hours
        {
            public float openWd, closeWd, openWe, closeWe;
            public bool Always => openWd <= 0f && closeWd >= 24f && openWe <= 0f && closeWe >= 24f;
        }

        static Hours H(float o1, float c1, float o2, float c2) => new Hours { openWd = o1, closeWd = c1, openWe = o2, closeWe = c2 };
        static readonly Hours Nonstop = H(0, 24, 0, 24);

        static readonly Dictionary<string, Hours> Table = new Dictionary<string, Hours>
        {
            // Yemek
            { "Burger Queen",   Nonstop },
            { "McDanold's",     Nonstop },
            { "Non-Stop Gyros", Nonstop },
            { "Döner Kral",     H(10, 26, 10, 28) },     // 10:00–02:00, hafta sonu 04:00'e kadar
            { "Starbocks",      H(7, 21, 8, 20) },
            { "Pizza Hat",      H(11, 23, 11, 24) },
            { "KFG",            H(10, 23, 10, 25) },     // hafta sonu 01:00'e kadar
            { "Sushi Samurai",  H(11, 22, 12, 22) },
            { "Fish & Ships",   H(11, 22, 11, 22) },
            { "Otogar Büfé",    H(5, 23, 6, 23) },
            { "Villa Gourmet",  H(12, 23, 12, 24) },
            // Market
            { "Szpar Express",  H(6, 21, 7, 19) },
            { "InterSzpar",     H(7, 21, 7, 20) },
            { "BioSzpar",       H(7, 20, 8, 18) },
            { DarkStore.StoreName, H(7, 24, 8, 24) },     // kendi marketin (dark store)
            // Bakkal / éjjel-nappali: 24 saat
            { "Market 24",      Nonstop },
            { "ABC Non-Stop",   Nonstop },
            { "Sarok Kisbolt",  Nonstop },
            { "Éjjel-Nappali",  Nonstop },
            { "Mini ABC",       Nonstop },
            { "Kikötő Non-Stop", Nonstop },
            { "Falu ABC",       Nonstop },
        };

        public static Hours Of(ShopDef d) => d != null && Table.TryGetValue(d.name, out var h) ? h : Nonstop;
        public static bool Is24h(ShopDef d) => Of(d).Always;

        static bool WeekendDay(int absDay) => GameClock.I.DayOfWeekOf(absDay) >= 5;

        /// <summary>Verilen andaki (GameClock.TotalMinutes) açık mı. Önceki günün gece yarısını geçen saatlerine de bakar.</summary>
        public static bool IsOpenAt(ShopDef d, float totalMinutes, float cutoffHours = 0f)
        {
            var h = Of(d);
            if (h.Always) return true;
            if (GameClock.I == null) return true;
            int day = Mathf.FloorToInt(totalMinutes / 1440f) + 1;
            float hour = (totalMinutes % 1440f) / 60f;
            bool we = WeekendDay(day);
            float o = we ? h.openWe : h.openWd, c = (we ? h.closeWe : h.closeWd) - cutoffHours;
            if (hour >= o && hour < c) return true;
            // Dünün geç kapanışı (ör. 02:00 = 26)
            bool weY = WeekendDay(day - 1);
            float cY = (weY ? h.closeWe : h.closeWd) - cutoffHours;
            return cY > 24f && hour + 24f < cY;
        }

        public static bool IsOpen(ShopDef d) => GameClock.I == null || IsOpenAt(d, GameClock.I.TotalMinutes);

        /// <summary>Yeni sipariş alıyor mu (kapanıştan 15 dk önce durur).</summary>
        public static bool AcceptsOrders(ShopDef d) => GameClock.I == null || IsOpenAt(d, GameClock.I.TotalMinutes, 0.25f);

        /// <summary>24 saat açık yerlerde gece (00–06) de az da olsa sipariş gelir.</summary>
        public static float NightFloor(ShopDef d)
        {
            if (GameClock.I == null || !Is24h(d)) return 0f;
            int hr = GameClock.I.Hour;
            if (hr >= 6) return 0f;
            if (d.isMarket) return 0.12f;
            return GameClock.I.IsWeekend ? 0.3f : 0.2f;
        }

        static string T(float h)
        {
            h %= 24f;
            int hh = Mathf.FloorToInt(h), mm = Mathf.RoundToInt((h - hh) * 60f);
            return $"{hh:00}:{mm:00}";
        }

        static string Range(float o, float c) => c >= 24f && o <= 0f ? "24 saat" : $"{T(o)}–{T(c)}";

        /// <summary>"Hafta içi 06:00–21:00 · Hafta sonu 07:00–19:00" ya da "24 saat açık".</summary>
        public static string HoursText(ShopDef d)
        {
            var h = Of(d);
            if (h.Always) return "24 saat açık";
            string wd = Range(h.openWd, h.closeWd), we = Range(h.openWe, h.closeWe);
            return wd == we ? $"Her gün {wd}" : $"Hafta içi {wd} · Hafta sonu {we}";
        }

        /// <summary>"Açık · 21:00'da kapanır" / "Kapalı · 06:00'da açılır".</summary>
        public static string StatusText(ShopDef d)
        {
            var h = Of(d);
            if (h.Always) return "Açık · 24 saat";
            if (GameClock.I == null) return "";
            float now = GameClock.I.TotalMinutes;
            bool open = IsOpenAt(d, now);
            // Sonraki değişim anını 15 dakikalık adımlarla bul (en fazla 2 gün)
            for (int i = 1; i <= 2 * 96; i++)
            {
                float t = now + i * 15f;
                if (IsOpenAt(d, t) != open)
                {
                    float rounded = Mathf.Floor(t / 15f) * 15f;
                    string when = T((rounded % 1440f) / 60f);
                    if (open) return $"Açık · {when}'da kapanır";
                    bool tomorrow = Mathf.FloorToInt(rounded / 1440f) > Mathf.FloorToInt(now / 1440f);
                    return $"Kapalı · {(tomorrow ? "yarın " : "")}{when}'da açılır";
                }
            }
            return open ? "Açık" : "Kapalı";
        }
    }
}
