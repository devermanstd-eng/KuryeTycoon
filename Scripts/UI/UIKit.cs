using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Pencerelerin ortak yazı ve düzen parçaları. Amaç: oyuncu paragraf okumasın, göz gezdirerek anlasın.
    ///  • Section(başlık, yardım)  bölüm başlığı + üstünde ince ayraç; yardım metni varsa sağda "?" (üstüne gelince açılır)
    ///  • Intro(id, kısa, uzun)    tek cümle görünür, uzun açıklama "?" balonunda (Intro.cs)
    ///  • HelpOnLast(metin)        son satırın sağına "?" koyar
    ///  • warn stili               turuncu zeminli uyarı kutusu (bütün uyarılar böyle görünür)
    ///  • ChipRow                  durum etiketleri: Çevrimiçi, Yorgun, Nakit borcu...
    ///  • KV                       "Anahtar ........ değer" satırı
    ///  • DetailButton / CardOpen  kartın ayrıntılarını aç/kapa (kart varsayılan kısa)
    ///  • PageTabs                 uzun pencereyi sayfalara böler (Ayarlar, Finans)
    /// Yazı boyutları (Inter): pencere başlığı 19 · bölüm 16 kalın · gövde 14 · ikincil 12 soluk · etiket 12.
    /// </summary>
    public partial class GameUI
    {
        GUIStyle body, bodyDim, kitSection, kitIntro, kitHelp, kitTip, kitChip, kitKey, kitVal;

        static readonly Color HelpBg = new Color(1f, 1f, 1f, 0.09f);
        static readonly Color HelpBgHover = new Color(1f, 1f, 1f, 0.22f);
        public static readonly Color InfoTone = new Color(0.45f, 0.72f, 1f);
        public static readonly Color NeutralTone = new Color(0.62f, 0.65f, 0.71f);

        /// <summary>InitStyles'ın sonunda çağrılır (şirket rengi değişince stiller yeniden kurulur).</summary>
        void InitKit()
        {
            body = new GUIStyle(label) { fontSize = 14 };
            body.normal.textColor = UITheme.Yazi(new Color(0.86f, 0.88f, 0.92f));
            bodyDim = new GUIStyle(body) { fontSize = 12 };
            bodyDim.normal.textColor = UITheme.Yazi(UITheme.TextDim);

            kitSection = new GUIStyle(header) { fontSize = 16, margin = new RectOffset(4, 4, 16, 6) };
            kitSection.normal.textColor = UITheme.Yazi(UITheme.Text);

            kitIntro = new GUIStyle(body) { padding = new RectOffset(0, 26, 1, 3) };
            kitIntro.normal.textColor = UITheme.Yazi(new Color(0.78f, 0.80f, 0.86f));

            kitHelp = new GUIStyle(label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, padding = new RectOffset(0, 0, 0, 1) };
            kitTip = new GUIStyle(body) { fontSize = 13 };
            kitTip.normal.textColor = UITheme.Yazi(UITheme.Text);

            kitChip = new GUIStyle(label)
            {
                fontSize = 12, fontStyle = FontStyle.Bold, wordWrap = false, stretchWidth = false,
                padding = new RectOffset(9, 9, 3, 4), margin = new RectOffset(0, 5, 2, 3), border = new RectOffset(8, 8, 8, 8),
                alignment = TextAnchor.MiddleCenter
            };
            UITheme.SetBg(kitChip.normal, Color.white, 8);

            kitKey = new GUIStyle(body) { wordWrap = false };
            kitKey.normal.textColor = UITheme.Yazi(UITheme.TextDim);
            kitVal = new GUIStyle(body) { wordWrap = false, alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
            kitVal.normal.textColor = UITheme.Yazi(UITheme.Text);

            // Uyarılar: düz turuncu yazı yerine zeminli kutu (göz hemen bulur)
            warn = new GUIStyle(small)
            {
                fontSize = 13,
                padding = new RectOffset(10, 10, 5, 6),
                margin = new RectOffset(4, 4, 3, 3),
                border = new RectOffset(6, 6, 6, 6)
            };
            warn.normal.textColor = UITheme.Yazi(new Color(1f, 0.82f, 0.58f));
            UITheme.SetBg(warn.normal, new Color(1f, 0.6f, 0.2f, 0.14f), 6);
        }

        /// <summary>Paylaşılan stillerin rengini sıfırlar (haritadaki kartlar ve bildirimler değiştirebiliyor).</summary>
        void ResetTextColors()
        {
            small.normal.textColor = UITheme.Yazi(UITheme.TextDim);
            label.normal.textColor = UITheme.Yazi(UITheme.Text);
            header.normal.textColor = UITheme.Yazi(Color.white);
        }

        // ---------------------------------------------------------------- bölüm başlığı

        /// <summary>Bölüm başlığı: üstünde ince ayraç, yardım metni varsa sağda "?".</summary>
        void Section(string title, string help = null)
        {
            GUILayout.Label(title, kitSection);
            if (Event.current.type != EventType.Repaint) return;
            var lr = GUILayoutUtility.GetLastRect();
            if (lr.y > 12f) Fill(new Rect(lr.x, lr.y - 8f, lr.width, 1f), UITheme.Line);
            if (!string.IsNullOrEmpty(help)) HelpBadgeAt(new Rect(lr.xMax - 18f, lr.y + (lr.height - 17f) / 2f, 17f, 17f), help);
        }

        // ---------------------------------------------------------------- "?" yardım balonu

        string helpTip;
        bool helpTipInWindow;
        bool drawingWindow;   // DrawPanel pencere içeriğini çizerken true

        void HelpBadgeAt(Rect r, string text)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text)) return;
            bool hover = r.Contains(Event.current.mousePosition) && modalFree;
            UITheme.Box(r, hover ? HelpBgHover : HelpBg, r.height / 2f);
            kitHelp.normal.textColor = UITheme.Yazi(hover ? Color.white : UITheme.TextDim);
            GUI.Label(r, "?", kitHelp);
            if (hover)
            {
                helpTip = text;
                helpTipInWindow = drawingWindow;
            }
        }

        /// <summary>Son çizilen satırın sağ ucuna "?" koyar (satır sağda ~24 px boşluk bırakmalı).</summary>
        void HelpOnLast(string text)
        {
            if (Event.current.type != EventType.Repaint) return;
            var lr = GUILayoutUtility.GetLastRect();
            HelpBadgeAt(new Rect(lr.xMax - 18f, lr.y + Mathf.Min(2f, (lr.height - 17f) / 2f), 17f, 17f), text);
        }

        /// <summary>"?" metnini fare yanında gösterir. Kök düzeyde, her şeyin üstünde çizilir.</summary>
        void DrawHelpTip(float sw, float sh)
        {
            if (string.IsNullOrEmpty(helpTip) || Event.current.type != EventType.Repaint) return;
            var m = Event.current.mousePosition;
            if (helpTipInWindow && !(panelOpen && WindowContentRect(sh).Contains(m))) return;   // kaydırılıp gizlenmiş satır
            const float w = 330f;
            var c = new GUIContent(helpTip);
            float h = kitTip.CalcHeight(c, w - 24f) + 18f;
            float x = Mathf.Min(m.x + 16f, sw - w - 8f);
            float y = m.y + 18f;
            if (y + h > sh - 8f) y = Mathf.Max(TopBar + 4f, m.y - h - 10f);
            var r = new Rect(x, y, w, h);
            UITheme.Shadowed(r, new Color(0.06f, 0.065f, 0.08f, 0.98f), 10f, 6f);
            UITheme.Outline(r, UITheme.WithAlpha(Color.white, 0.12f), 1f, 10f);
            GUI.Label(new Rect(r.x + 12f, r.y + 9f, w - 24f, h - 18f), c, kitTip);
        }

        /// <summary>Pencerenin kaydırılan içerik alanı (başlık çubuğunun altı).</summary>
        Rect WindowContentRect(float sh)
        {
            var w = WindowRect(sh);
            float hh = WindowHeaderHeight();
            return new Rect(w.x + 12f, w.y + hh + 8f, w.width - 24f, w.height - hh - 16f);
        }

        // ---------------------------------------------------------------- durum etiketleri

        readonly List<(string text, Color color)> chips = new List<(string, Color)>();

        /// <summary>chips listesindeki etiketleri çizer ve listeyi boşaltır. maxW: satır genişliği (verilmezse pencere kartı).</summary>
        void ChipRow(float maxW = 0f)
        {
            if (chips.Count == 0) return;
            // Pencere kartındaki kullanılabilir genişlik (kenar boşlukları ve kaydırma çubuğu düşülünce); taşarsa alt satıra geçer
            if (maxW <= 0f) maxW = panelWidth - 80f;
            float used = 0f;
            GUILayout.BeginHorizontal();
            var oldBg = GUI.backgroundColor;
            foreach (var (text, color) in chips)
            {
                var content = new GUIContent(text);
                float cw = kitChip.CalcSize(content).x + kitChip.margin.right;
                if (used > 0f && used + cw > maxW)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    used = 0f;
                }
                GUI.backgroundColor = new Color(color.r, color.g, color.b, 0.2f);
                kitChip.normal.textColor = UITheme.Yazi(Color.Lerp(color, Color.white, 0.3f));
                GUILayout.Label(content, kitChip);
                used += cw;
            }
            GUI.backgroundColor = oldBg;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            chips.Clear();
        }

        /// <summary>Etiket ekler (uzun metin kısaltılır: etiket tek satır kalmalı).</summary>
        void Chip(string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (text.Length > 44) text = text.Substring(0, 42).TrimEnd() + "…";
            chips.Add((text, color));
        }

        // ---------------------------------------------------------------- anahtar / değer

        void KV(string key, string value, Color? valueColor = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(key, kitKey);
            GUILayout.FlexibleSpace();
            kitVal.normal.textColor = UITheme.Yazi(valueColor ?? UITheme.Text);
            GUILayout.Label(value, kitVal);
            GUILayout.EndHorizontal();
        }

        static Color MoneyColor(long v) => v > 0 ? UITheme.Good : v < 0 ? UITheme.Bad : UITheme.Text;

        /// <summary>İki ince çubuk yan yana (ör. Moral ve Yorgunluk): iki satır yerine bir satır.</summary>
        void MiniBars(string n1, float v1, Color c1, string n2, float v2, Color c2)
        {
            GUILayout.BeginHorizontal();
            MiniBar(n1, v1, c1);
            GUILayout.Space(12);
            MiniBar(n2, v2, c2);
            GUILayout.EndHorizontal();
        }

        void MiniBar(string name, float v, Color c, float labelWidth = 84f)
        {
            GUILayout.Label(name, kitKey, GUILayout.Width(labelWidth));
            Rect r = GUILayoutUtility.GetRect(30, 20, GUILayout.ExpandWidth(true));
            r.y += 7f;
            r.height = 7f;
            UITheme.Box(r, new Color(0f, 0f, 0f, 0.45f), 3f);
            float fw = r.width * Mathf.Clamp01(v);
            if (fw > 1f) UITheme.Box(new Rect(r.x, r.y, Mathf.Max(6f, fw), r.height), c, 3f);
            GUILayout.Label($"%{v * 100f:0}", kitKey, GUILayout.Width(42));
        }

        /// <summary>"Ebeveyn (çocuklar okuldayken...)" → "Ebeveyn": etiketlerde parantez açıklaması yer kaplamasın.</summary>
        static string ShortLabel(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            int i = s.IndexOf(" (", System.StringComparison.Ordinal);
            return i > 0 ? s.Substring(0, i) : s;
        }

        // ---------------------------------------------------------------- kart ayrıntısı

        readonly HashSet<object> openCards = new HashSet<object>();

        bool CardOpen(object key) => key != null && openCards.Contains(key);

        void DetailButton(object key)
        {
            bool open = CardOpen(key);
            if (GUILayout.Button(open ? "Daha az" : "Detay", smallButton, GUILayout.Width(82)))
            {
                var k = key;
                deferred = () =>
                {
                    if (!openCards.Remove(k))
                    {
                        if (openCards.Count > 200) openCards.Clear();
                        openCards.Add(k);
                    }
                };
            }
        }

        // ---------------------------------------------------------------- sayfalar

        /// <summary>Pencerenin üstünde sayfa seçici. Seçim bir sonraki karede değişir (IMGUI düzeni bozulmasın).</summary>
        void PageTabs(int current, System.Action<int> set, params string[] names)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < names.Length; i++)
            {
                int k = i;
                if (SelectButton(current == i, names[i], smallButton)) deferred = () => { set(k); scroll = Vector2.zero; };
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
        }
    }
}
