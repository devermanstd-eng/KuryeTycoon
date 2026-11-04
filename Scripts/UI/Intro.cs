using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Pencere açıklamaları: kısa cümle hep görünür, uzun hali "Ayrıntı" ile açılır.
    /// Ayarlar > Arayüz'den ayrıntılar hep açık yapılabilir.
    /// Kullanım: Intro("kuryeler", "Kısa açıklama.", "Uzun açıklama...");
    /// </summary>
    public partial class GameUI
    {
        const string DetailsKey = "KuryeTycoon.AyrintilariGoster";
        static int detailsCache = -1;

        static bool ShowAllDetails
        {
            get
            {
                if (detailsCache < 0) detailsCache = PlayerPrefs.GetInt(DetailsKey, 0);
                return detailsCache == 1;
            }
            set
            {
                detailsCache = value ? 1 : 0;
                PlayerPrefs.SetInt(DetailsKey, detailsCache);
                PlayerPrefs.Save();
            }
        }

        readonly HashSet<string> introOpen = new HashSet<string>();
        GUIStyle linkStyle;

        void Intro(string id, string brief, string details)
        {
            GUILayout.Label(brief, small);
            if (string.IsNullOrEmpty(details)) return;
            bool always = ShowAllDetails;
            bool open = always || introOpen.Contains(id);
            if (open) GUILayout.Label(details, small);
            if (always) return;

            if (linkStyle == null)
            {
                linkStyle = new GUIStyle(small) { wordWrap = false, fontStyle = FontStyle.Bold, margin = new RectOffset(4, 4, 0, 4) };
                linkStyle.normal.background = linkStyle.hover.background = linkStyle.active.background = null;
                linkStyle.normal.textColor = SelectedTint;
                linkStyle.hover.textColor = linkStyle.active.textColor = UITheme.Text;
            }
            // Açıp kapamak düzeni değiştirir: bir sonraki karede yapılır
            if (GUILayout.Button(open ? "Ayrıntıyı gizle" : "Ayrıntı", linkStyle, GUILayout.ExpandWidth(false)))
                deferred = () => { if (!introOpen.Remove(id)) introOpen.Add(id); };
        }
    }
}
