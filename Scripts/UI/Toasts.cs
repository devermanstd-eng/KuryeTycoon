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
        GUIStyle toastText;

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

        void DrawToasts(float sw, float sh)
        {
            if (toasts.Count == 0) return;
            if (toastText == null) toastText = new GUIStyle(label) { fontSize = 13, wordWrap = true, alignment = TextAnchor.MiddleLeft };
            float now = Time.unscaledTime;
            float w = 460f;
            float cx = DockW + (sw - DockW) / 2f;
            float y = TopBar + 12f;
            for (int i = 0; i < toasts.Count; i++)
            {
                var t = toasts[i];
                float age = now - t.born;
                if (age > t.Life) { var dead = t; deferred += () => toasts.Remove(dead); continue; }
                float a = Mathf.Min(Mathf.Clamp01(age / 0.25f), Mathf.Clamp01((t.Life - age) / 0.5f));
                float slide = (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.25f))) * -16f;

                Color accent; Icons.Kind icon; string head;
                switch (t.type)
                {
                    case ToastType.Good: accent = UITheme.Good; icon = Icons.Kind.Star; head = "Harika"; break;
                    case ToastType.Boost: accent = new Color(1f, 0.62f, 0.20f); icon = Icons.Kind.Bolt; head = "Boost"; break;
                    case ToastType.Info:
                        accent = new Color(0.40f, 0.70f, 1f);
                        bool city = t.text.StartsWith("Şehir");
                        icon = city ? Icons.Kind.Calendar : Icons.Kind.Clock; head = city ? "Şehir" : "İpucu"; break;
                    case ToastType.Message: accent = new Color(0.55f, 0.85f, 0.65f); icon = Icons.Kind.Phone; head = "Kurye mesajı · tıkla"; break;
                    default: accent = UITheme.Bad; icon = Icons.Kind.Warning; head = "Dikkat"; break;
                }
                string body = t.text + (t.count > 1 ? $"  (+{t.count - 1})" : "");
                float h = Mathf.Max(52f, toastText.CalcHeight(new GUIContent(body), w - 74f) + 26f);
                var r = new Rect(cx - w / 2f, y + slide, w, h);

                var oldColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, a);
                UITheme.Shadowed(r, new Color(0.08f, 0.085f, 0.105f, 0.97f), 12f, 6f);
                UITheme.Box(new Rect(r.x, r.y, 5f, r.height), accent, 3f);
                UITheme.Box(new Rect(r.x + 16f, r.y + h / 2f - 15f, 30f, 30f), UITheme.WithAlpha(accent, 0.2f), 15f);
                Icons.Draw(new Rect(r.x + 23f, r.y + h / 2f - 8f, 16f, 16f), icon, UITheme.WithAlpha(accent, a));
                small.normal.textColor = UITheme.Yazi(accent);
                GUI.Label(new Rect(r.x + 58f, r.y + 5f, w - 70f, 16f), head, small);
                toastText.normal.textColor = UITheme.Yazi(UITheme.Text);
                GUI.Label(new Rect(r.x + 58f, r.y + 18f, w - 74f, h - 22f), body, toastText);
                GUI.color = oldColor;

                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    var dead = t;
                    bool openMsgs = t.type == ToastType.Message;
                    deferred += () => { toasts.Remove(dead); if (openMsgs && !(panelOpen && tab == Tab.Mesajlar)) OpenTab(Tab.Mesajlar); };
                }
                y += h + 8f;
            }
            small.normal.textColor = UITheme.Yazi(UITheme.TextDim);
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

        /// <summary>Sol üstte küçük hedef takip kutusu: oyuncunun her an bir amacı olsun. Tıklayınca Hedefler penceresi açılır.</summary>
        void DrawGoalTracker()
        {
            if (!TrackerVisible) return;
            var gm = GoalManager.I;
            var r = GoalTrackerRect();
            bool hover = r.Contains(Event.current.mousePosition);
            UITheme.Shadowed(r, new Color(0.07f, 0.075f, 0.09f, hover ? 0.95f : 0.85f), 12f, 4f);
            Icons.Draw(new Rect(r.x + 12f, r.y + 9f, 14f, 14f), Icons.Kind.Flag, UITheme.Accent);
            small.normal.textColor = UITheme.Yazi(UITheme.Text);
            GUI.Label(new Rect(r.x + 32f, r.y + 6f, 160f, 20f), "Haftalık hedefler", small);
            small.normal.textColor = UITheme.Yazi(UITheme.TextDim);
            GUI.Label(new Rect(r.xMax - 92f, r.y + 6f, 82f, 20f), $"{gm.DaysLeft} gün kaldı", small);

            var shownGoals = TrackerGoals();
            for (int i = 0; i < shownGoals.Count; i++)
            {
                var g = shownGoals[i];
                float yy = r.y + 30f + i * 34f;
                var dot = new Rect(r.x + 12f, yy + 4f, 14f, 14f);
                if (g.done) { UITheme.Box(dot, UITheme.Good, 7f); }
                else UITheme.Outline(dot, UITheme.TextDim, 1.5f, 7f);
                small.normal.textColor = UITheme.Yazi(g.done ? UITheme.TextDim : UITheme.Text);
                GUI.Label(new Rect(r.x + 32f, yy, r.width - 130f, 20f), g.Title, small);
                small.normal.textColor = UITheme.Yazi(g.done ? UITheme.Good : UITheme.TextDim);
                GUI.Label(new Rect(r.xMax - 92f, yy, 82f, 20f), g.done ? "✓" : g.ProgressText, small);
                var bar = new Rect(r.x + 32f, yy + 21f, r.width - 44f, 4f);
                UITheme.Box(bar, UITheme.WithAlpha(Color.white, 0.08f), 2f);
                UITheme.Box(new Rect(bar.x, bar.y, bar.width * g.Progress01, bar.height), g.done ? UITheme.Good : UITheme.Accent, 2f);
            }
            small.normal.textColor = UITheme.Yazi(UITheme.TextDim);
            if (TrackerExtra > 0)
            {
                int done = 0;
                foreach (var g in gm.Goals) if (g.done) done++;
                GUI.Label(new Rect(r.x + 32f, r.yMax - 22f, r.width - 44f, 18f), $"+{TrackerExtra} hedef daha · {done}/{gm.Goals.Count} bitti · tıkla", small);
            }
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) deferred = () => OpenTab(Tab.Hedefler);
        }

        void DrawGoals()
        {
            var gm = GoalManager.I;
            if (gm == null) return;
            Section($"Bu haftanın hedefleri · {gm.DaysLeft} gün kaldı");
            Intro("hedefler", "Her pazartesi 6 yeni hedef gelir. Tamamladığın hedefin ödülü hemen kasaya girer.",
                  "Hedefler şirketinin büyüklüğüne göre seçilir. Yarım kalan hedef için ceza yok.");
            GUILayout.Space(6);
            foreach (var g in gm.Goals)
            {
                GUILayout.BeginVertical(box);
                GUILayout.Label((g.done ? "✓ " : "") + g.Title, header);
                Bar("İlerleme", g.Progress01, g.done ? UITheme.Good : UITheme.Accent, g.ProgressText);
                GUILayout.Label($"Ödül: {Ft(g.reward)}" + (g.reputation > 0f ? " + itibar" : "") + (g.done ? " · alındı" : ""), g.done ? small : note);
                GUILayout.EndVertical();
            }
            GUILayout.Space(6);
            GUILayout.Label($"Bugüne kadar tamamlanan hedef: {gm.completedTotal}", small);
        }
    }
}
