using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Normal = 0: eski kayıtlarda alan yok, Normal sayılır.</summary>
    public enum DifficultyLevel { Normal = 0, Kolay = 1, Zor = 2 }

    /// <summary>
    /// Zorluk seviyesi: kurulum ekranında seçilir, kayda yazılır, oyun içinde değişmez.
    /// Birkaç denge çarpanını değiştirir (Normal'de hepsi 1, yani oyun eskisi gibi):
    ///  • Sipariş sıklığı (OrderManager)          Kolay ×1,15  Zor ×0,88
    ///  • Kurye seçiciliği / teklif beklentisi    Kolay ×0,85  Zor ×1,12   (Courier.OfferThreshold)
    ///  • Kurye başvurusu sıklığı                  Kolay ×1,3   Zor ×0,8    (CourierManager)
    ///  • Sürpriz vergi/harç arası (gün)           Kolay ×1,5   Zor ×0,75   (EconomyManager)
    /// </summary>
    public static class Difficulty
    {
        public static DifficultyLevel Level = DifficultyLevel.Normal;

        public static readonly DifficultyLevel[] Levels = { DifficultyLevel.Kolay, DifficultyLevel.Normal, DifficultyLevel.Zor };

        static bool Easy => Level == DifficultyLevel.Kolay;
        static bool Hard => Level == DifficultyLevel.Zor;

        public static float OrderRate => Easy ? 1.15f : Hard ? 0.88f : 1f;
        public static float OfferThreshold => Easy ? 0.85f : Hard ? 1.12f : 1f;
        public static float ApplicationRate => Easy ? 1.3f : Hard ? 0.8f : 1f;
        public static float SurpriseInterval => Easy ? 1.5f : Hard ? 0.75f : 1f;

        public static string Name(DifficultyLevel d) =>
            d == DifficultyLevel.Kolay ? "Kolay" : d == DifficultyLevel.Zor ? "Zor" : "Normal";

        public static string Description(DifficultyLevel d)
        {
            switch (d)
            {
                case DifficultyLevel.Kolay:
                    return "Oyunu öğrenmek için: daha çok sipariş (+%15), kuryeler daha az seçici, başvurular daha sık, sürpriz vergi ve harçlar daha seyrek.";
                case DifficultyLevel.Zor:
                    return "Deneyimliler için: daha az sipariş (−%12), kuryeler daha seçici, başvurular daha seyrek, sürpriz giderler daha sık.";
                default:
                    return "Oyunun dengelendiği seviye.";
            }
        }

        // Kurulum ekranında son seçilen (bu bilgisayarda hatırlanır)
        const string PrefKey = "KuryeTycoon.Zorluk";

        public static DifficultyLevel LastChoice
        {
            get => (DifficultyLevel)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, 2);
            set { PlayerPrefs.SetInt(PrefKey, (int)value); PlayerPrefs.Save(); }
        }
    }

    /// <summary>Kurulum ekranındaki zorluk seçimi.</summary>
    public partial class GameUI
    {
        DifficultyLevel setupDifficulty;
        bool setupDifficultyLoaded;   // PlayerPrefs alan başlatıcısında okunamaz (Unity izin vermez), ilk çizimde okunur

        void DrawDifficultyPicker()
        {
            if (!setupDifficultyLoaded)
            {
                setupDifficultyLoaded = true;
                setupDifficulty = Difficulty.LastChoice;
            }
            GUILayout.Label("Zorluk", header);
            GUILayout.BeginHorizontal();
            foreach (var d in Difficulty.Levels)
            {
                var dd = d;
                if (SelectButton(setupDifficulty == d, Difficulty.Name(d), button, GUILayout.Height(30))) setupDifficulty = dd;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(Difficulty.Description(setupDifficulty), small);
        }
    }
}
