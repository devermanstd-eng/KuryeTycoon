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

        // ---------------------------------------------------------------- bölüm başlığı


        /// <summary>Pencerenin kaydırılan içerik alanı (başlık çubuğunun altı).</summary>
        Rect WindowContentRect(float sh)
        {
            var w = WindowRect(sh);
            float hh = WindowHeaderHeight();
            return new Rect(w.x + 12f, w.y + hh + 8f, w.width - 24f, w.height - hh - 16f);
        }

        // ---------------------------------------------------------------- durum etiketleri

        readonly List<(string text, Color color)> chips = new List<(string, Color)>();

        /// <summary>Etiket ekler (uzun metin kısaltılır: etiket tek satır kalmalı).</summary>
        void Chip(string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            // Uzun durum yazısı ("Çevrimdışı: Geçimini kuryelikten sağlıyor, ...") kısalır: iki noktadan öncesi kalır
            if (text.Length > 30)
            {
                int k = text.IndexOf(": ", System.StringComparison.Ordinal);
                text = k >= 4 && k <= 26 ? text.Substring(0, k) : text.Substring(0, 28).TrimEnd() + "…";
            }
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

        /// <summary>"Ebeveyn (çocuklar okuldayken...)" → "Ebeveyn": etiketlerde parantez açıklaması yer kaplamasın.</summary>
        static string ShortLabel(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            int i = s.IndexOf(" (", System.StringComparison.Ordinal);
            return i > 0 ? s.Substring(0, i) : s;
        }

        // ---------------------------------------------------------------- kart ayrıntısı

        // ---------------------------------------------------------------- sayfalar
    }
}
