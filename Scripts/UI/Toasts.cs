using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Bildirim kartları ve haftalık hedefler:
    ///  • Önemli olaylar (iptal, kaza, boost, yeni bölge, sözleşme, hedef tamamlandı…) haritanın üst ortasında birkaç saniye
    ///    görünen renkli kartlar olarak çıkar. Aynı türden art arda gelenler tek kartta toplanır (ör. "+3").
    ///    Sıradan bilgiler sadece alttaki akışta kalır. Karta tıklayınca kapanır.
    ///  • Haftalık hedefler: sol üstte küçük takip kutusu (pencere kapalıyken) ve Hedefler penceresi.
    /// </summary>
    public partial class GameUI
    {
        enum ToastType { Bad, Boost, Good, Info, Message }

        class Toast
        {
            public ToastType type;
            public string text;
            public float born;
            public int count = 1;
            public float Life => type == ToastType.Bad ? 6f : 5f;
        }

        readonly List<Toast> toasts = new List<Toast>();

        void OnNotified(string msg)
        {
            var type = Classify(msg);
            if (type == null) return;
            if (type.Value == ToastType.Message) msg = msg.Substring(Mathf.Min(msg.Length, 8));   // "Mesaj — " öneki
            if (type.Value == ToastType.Good) GameSounds.Play(Sfx.Iyi);
            else if (type.Value == ToastType.Bad) GameSounds.Play(Sfx.Kotu);
            float now = Time.unscaledTime;
            // Aynı türden yeni bir kart zaten görünüyorsa onu güncelle (iptal yağmurunda ekran dolmasın)
            foreach (var t in toasts)
                if (t.type == type.Value && now - t.born < 3f)
                {
                    t.text = msg;
                    t.count++;
                    t.born = now;
                    return;
                }
            toasts.Insert(0, new Toast { type = type.Value, text = msg, born = now });
            if (toasts.Count > 4) toasts.RemoveAt(toasts.Count - 1);
        }

        static ToastType? Classify(string msg)
        {
            string m = msg.ToLowerInvariant();
            if (msg.StartsWith("Mesaj —")) return ToastType.Message;
            if (msg.StartsWith("İpucu:") || msg.StartsWith("Şehir etkinliği") || msg.StartsWith("Şehir:")) return ToastType.Info;
            if (m.Contains("kötü yorum viral")) return ToastType.Bad;
            if (m.Contains("övgüsü viral") || m.Contains("marketin açıldı") || m.Contains("market açıldı")) return ToastType.Good;
            if (m.Contains("raflar boş")) return ToastType.Bad;
            if (m.Contains("hedef tamamlandı") || m.Contains("hedefler geldi") || m.Contains("açıldı!") || m.Contains("sözleşme imzalandı") ||
                m.Contains("ekibe katıldı") || m.Contains("kilometre taşı") || m.Contains("ödül"))
                return ToastType.Good;
            if (m.Contains("boost")) return ToastType.Boost;
            if (m.Contains("iptal") || m.Contains("kaza") || m.Contains("iflas") || m.Contains("istifa") || m.Contains("ayrıldı") ||
                m.Contains("hastalan") || m.Contains("şikayet") || m.Contains("çalındı") || m.Contains("ceza") || m.Contains("vergi borcu") ||
                m.Contains("arıza") || m.Contains("rakip"))
                return ToastType.Bad;
            return null;
        }

        bool ToastAt(Vector2 m, float sw)
        {
            if (toasts.Count == 0 || UiAktif) return false;   // yeni arayüzde UiUzerinde bakar
            float w = 460f, cx = DockW + (sw - DockW) / 2f;
            return m.x > cx - w / 2f && m.x < cx + w / 2f && m.y > TopBar && m.y < TopBar + 12f + toasts.Count * 80f;
        }

        // ---------------------------------------------------------------- haftalık hedefler

        const int TrackerMax = 3;   // haritanın üstünde en fazla bu kadar hedef (gerisi Hedefler penceresinde)
        readonly List<Goal> trackerGoals = new List<Goal>();

        /// <summary>Takip kutusundaki hedefler: bitmemişler, en çok ilerleyen önce; yer kalırsa bitenler.</summary>
        List<Goal> TrackerGoals()
        {
            trackerGoals.Clear();
            var gm = GoalManager.I;
            if (gm == null) return trackerGoals;
            foreach (var g in gm.Goals) if (!g.done) trackerGoals.Add(g);
            trackerGoals.Sort((a, b) => b.Progress01.CompareTo(a.Progress01));
            foreach (var g in gm.Goals) if (g.done) trackerGoals.Add(g);
            if (trackerGoals.Count > TrackerMax) trackerGoals.RemoveRange(TrackerMax, trackerGoals.Count - TrackerMax);
            return trackerGoals;
        }

        int TrackerExtra => GoalManager.I != null ? Mathf.Max(0, GoalManager.I.Goals.Count - TrackerMax) : 0;

        Rect GoalTrackerRect()
        {
            int n = GoalManager.I != null ? Mathf.Min(TrackerMax, GoalManager.I.Goals.Count) : 0;
            return new Rect(DockW + 10f, TopBar + 10f, 330f, 34f + n * 34f + (TrackerExtra > 0 ? 18f : 0f));
        }

        bool TrackerVisible => !panelOpen && GoalManager.I != null && GoalManager.I.Goals.Count > 0;
    }
}
