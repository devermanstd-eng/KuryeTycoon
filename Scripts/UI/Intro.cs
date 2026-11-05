using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Pencere açıklamaları: kısa cümle hep görünür, uzun açıklama sağdaki "?" balonunda (üstüne gelince açılır).
    /// Ayarlar > Arayüz'de "ayrıntıları hep göster" açıksa uzun açıklama da satırın altına yazılır.
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

        /// <param name="id">eski sürümden kalma, artık kullanılmıyor (çağrılar değişmesin diye duruyor)</param>
        void Intro(string id, string brief, string details)
        {
            bool hasDetails = !string.IsNullOrEmpty(details);
            if (!hasDetails || ShowAllDetails)
            {
                GUILayout.Label(brief, body);
                if (hasDetails) GUILayout.Label(details, bodyDim);
                return;
            }
            GUILayout.Label(brief, kitIntro);
            HelpOnLast(details);
        }
    }
}
