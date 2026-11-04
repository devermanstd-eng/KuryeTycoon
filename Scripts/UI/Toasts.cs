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
        enum ToastType { Bad, Boost, Good }

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
                small.normal.textColor = accent;
                GUI.Label(new Rect(r.x + 58f, r.y + 5f, w - 70f, 16f), head, small);
                toastText.normal.textColor = UITheme.Text;
                GUI.Label(new Rect(r.x + 58f, r.y + 18f, w - 74f, h - 22f), body, toastText);
                GUI.color = oldColor;

                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { var dead = t; deferred += () => toasts.Remove(dead); }
                y += h + 8f;
            }
            small.normal.textColor = UITheme.TextDim;
        }

        bool ToastAt(Vector2 m, float sw)
        {
            if (toasts.Count == 0) return false;
            float w = 460f, cx = DockW + (sw - DockW) / 2f;
            return m.x > cx - w / 2f && m.x < cx + w / 2f && m.y > TopBar && m.y < TopBar + 12f + toasts.Count * 80f;
        }

        // ---------------------------------------------------------------- haftalık hedefler

        Rect GoalTrackerRect()
        {
            int n = GoalManager.I != null ? GoalManager.I.Goals.Count : 0;
            return new Rect(DockW + 10f, TopBar + 10f, 290f, 34f + n * 34f);
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
            small.normal.textColor = UITheme.Text;
            GUI.Label(new Rect(r.x + 32f, r.y + 6f, 160f, 20f), "Haftalık hedefler", small);
            small.normal.textColor = UITheme.TextDim;
            GUI.Label(new Rect(r.xMax - 92f, r.y + 6f, 82f, 20f), $"{gm.DaysLeft} gün kaldı", small);

            for (int i = 0; i < gm.Goals.Count; i++)
            {
                var g = gm.Goals[i];
                float yy = r.y + 30f + i * 34f;
                var dot = new Rect(r.x + 12f, yy + 4f, 14f, 14f);
                if (g.done) { UITheme.Box(dot, UITheme.Good, 7f); }
                else UITheme.Outline(dot, UITheme.TextDim, 1.5f, 7f);
                small.normal.textColor = g.done ? UITheme.TextDim : UITheme.Text;
                GUI.Label(new Rect(r.x + 32f, yy, r.width - 100f, 20f), g.Title, small);
                small.normal.textColor = g.done ? UITheme.Good : UITheme.TextDim;
                GUI.Label(new Rect(r.xMax - 62f, yy, 52f, 20f), g.done ? "✓" : $"{g.progress}/{g.target}", small);
                var bar = new Rect(r.x + 32f, yy + 21f, r.width - 44f, 4f);
                UITheme.Box(bar, UITheme.WithAlpha(Color.white, 0.08f), 2f);
                UITheme.Box(new Rect(bar.x, bar.y, bar.width * g.Progress01, bar.height), g.done ? UITheme.Good : UITheme.Accent, 2f);
            }
            small.normal.textColor = UITheme.TextDim;
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) deferred = () => OpenTab(Tab.Hedefler);
        }

        void DrawGoals()
        {
            var gm = GoalManager.I;
            if (gm == null) return;
            GUILayout.Label($"Bu haftanın hedefleri · {gm.DaysLeft} gün kaldı", header);
            Intro("hedefler", "Her pazartesi 3 yeni hedef gelir. Tamamladığın hedefin ödülü hemen kasaya girer.",
                  "Hedefler şirketinin büyüklüğüne göre seçilir. Yarım kalan hedef için ceza yok.");
            GUILayout.Space(6);
            foreach (var g in gm.Goals)
            {
                GUILayout.BeginVertical(box);
                GUILayout.Label((g.done ? "✓ " : "") + g.Title, header);
                Bar("İlerleme", g.Progress01, g.done ? UITheme.Good : UITheme.Accent, $"{g.progress}/{g.target}");
                GUILayout.Label($"Ödül: {Ft(g.reward)}" + (g.reputation > 0f ? " + itibar" : "") + (g.done ? " · alındı" : ""), g.done ? small : note);
                GUILayout.EndVertical();
            }
            GUILayout.Space(6);
            GUILayout.Label($"Bugüne kadar tamamlanan hedef: {gm.completedTotal}", small);
        }
    }
}
