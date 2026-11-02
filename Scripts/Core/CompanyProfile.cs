using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Oyuncunun şirketi: adı, renk paleti ve itibarı.</summary>
    public static class CompanyProfile
    {
        public static string Name = "Hızlı Kurye";
        public static Color Primary = new Color(0.0f, 0.62f, 0.87f);
        public static Color Secondary = Color.white;
        /// <summary>1–5 arası müşteri puanı ortalaması. Sipariş sayısını etkiler.</summary>
        public static float Reputation = 3.5f;

        public static void ResetDefaults()
        {
            Name = "Hızlı Kurye";
            Primary = new Color(0.0f, 0.62f, 0.87f);
            Secondary = Color.white;
            Reputation = 3.5f;
        }

        public static void AddReputation(float delta)
        {
            Reputation = Mathf.Clamp(Reputation + delta, 1f, 5f);
        }

        public static string Initial =>
            string.IsNullOrEmpty(Name) ? "K" : Name.Trim().Substring(0, 1).ToUpper();
    }
}
