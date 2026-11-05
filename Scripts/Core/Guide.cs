using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Başta kilitli olan, sonradan açılan menüler.</summary>
    /// <summary>Sıra kayıttaki bit sırasıdır: sadece sona eklenir.</summary>
    public enum Feature { Gelisim, Kampanya, Destek, Market, Grafik, Mesajlar }

    [System.Serializable]
    public class GuideSave
    {
        public bool saved;          // eski kayıtlarda yok (false): rehber bitmiş sayılır
        public int tutorialStep;
        public bool tutorialDone;
        public int unlockedMask;
    }

    /// <summary>
    /// Yeni oyuncu için sadeleştirme:
    ///  • İlk gün kısa bir rehber (adım kartları) sol çubuktaki ana menüleri tek tek gösterir. Atla / kapat ile geçilir,
    ///    Ayarlar'dan yeniden başlatılır. Kartları GameUI çizer (Tutorial.cs); burada sadece hangi adımda olunduğu tutulur.
    ///  • Gelişim, Kampanya, Destek, Market, Grafik ve Mesajlar menüleri başta kilitli. Günü ya da şartı gelince kalıcı olarak
    ///    açılır ve bildirim gelir.
    ///    Ayarlar'daki "Bütün menüleri baştan aç" hepsini açar (bu bilgisayarda kalıcı).
    /// Kilit sadece menüyü gizler; arkadaki sistemler (boost saatleri, platform ücreti...) her zamanki gibi çalışır.
    /// Rehber adımı ve açılan menüler kayda yazılır.
    /// </summary>
    public class Guide : MonoBehaviour
    {
        public static Guide I { get; private set; }

        // Açılma şartları (gün = şirketin kaçıncı günü)
        public const int GelisimDay = 2;
        public const int KampanyaDay = 3;
        public const int DestekDay = 5;
        public const int DestekCouriers = 6;
        public const int GrafikDay = 3;
        public const int MarketDay = 7;

        const int FeatureCount = 6;
        public static readonly string[] FeatureNames = { "Gelişim", "Kampanya", "Destek", "Market", "Grafik", "Yorumlar" };

        public int TutorialStep { get; private set; }
        /// <summary>Kurulum ekranında ve eski kayıtlarda rehber yok; yeni oyunda InitNew açar.</summary>
        public bool TutorialDone { get; private set; } = true;

        int unlockedMask;
        float nextCheck;

        // ---------------------------------------------------------------- ayar
        const string AllMenusKey = "KuryeTycoon.ButunMenuler";
        static int allMenusCache = -1;

        /// <summary>Bütün menüler baştan açık olsun mu (deneyimli oyuncular için). Bu bilgisayarda kalıcı.</summary>
        public static bool AllMenusSetting
        {
            get
            {
                if (allMenusCache < 0) allMenusCache = PlayerPrefs.GetInt(AllMenusKey, 0);
                return allMenusCache == 1;
            }
            set
            {
                allMenusCache = value ? 1 : 0;
                PlayerPrefs.SetInt(AllMenusKey, allMenusCache);
                PlayerPrefs.Save();
            }
        }

        void Awake() { I = this; }

        // ---------------------------------------------------------------- oyun başı / kayıt

        /// <summary>Yeni oyun: rehber baştan, kilitli menüler kapalı.</summary>
        public void InitNew()
        {
            TutorialStep = 0;
            TutorialDone = false;
            unlockedMask = 0;
            Evaluate(false);
        }

        public GuideSave CaptureSave() => new GuideSave
        {
            saved = true,
            tutorialStep = TutorialStep,
            tutorialDone = TutorialDone,
            unlockedMask = unlockedMask
        };

        public void RestoreSave(GuideSave s)
        {
            if (s == null || !s.saved)
            {
                // Rehberden önceki bir kayıt: rehberi gösterme, menüleri şartlara göre sessizce aç
                TutorialStep = 0;
                TutorialDone = true;
                unlockedMask = 0;
            }
            else
            {
                TutorialStep = Mathf.Max(0, s.tutorialStep);
                TutorialDone = s.tutorialDone;
                unlockedMask = s.unlockedMask;
            }
            Evaluate(false);
        }

        // ---------------------------------------------------------------- rehber

        public void NextStep(int stepCount)
        {
            TutorialStep++;
            if (TutorialStep >= stepCount) TutorialDone = true;
        }

        public void FinishTutorial() { TutorialDone = true; }

        public void RestartTutorial()
        {
            TutorialStep = 0;
            TutorialDone = false;
        }

        // ---------------------------------------------------------------- kilitli menüler

        public bool IsOpen(Feature f) => AllMenusSetting || (unlockedMask & (1 << (int)f)) != 0;

        public static string LockReason(Feature f)
        {
            switch (f)
            {
                case Feature.Gelisim: return $"{GelisimDay}. günde ya da kurye hesapların dolunca açılır.";
                case Feature.Kampanya: return $"{KampanyaDay}. günde açılır.";
                case Feature.Destek: return $"{DestekDay}. günde ya da {DestekCouriers} kuryen olunca açılır.";
                case Feature.Market: return $"{MarketDay}. günde ya da {CityMap.DistrictNames[DarkStore.RequiredDistrict]} bölgesini açınca açılır.";
                case Feature.Grafik: return $"{GrafikDay}. günde açılır (önce biraz veri birikmeli).";
                case Feature.Mesajlar: return "İlk müşteri yorumu gelince açılır.";
            }
            return "";
        }

        static string UnlockText(Feature f)
        {
            switch (f)
            {
                case Feature.Gelisim: return "platform paketini yükselt, kalıcı iyileştirmeler al.";
                case Feature.Kampanya: return "boost saatleri koy, kuryeler daha çok çevrimiçi olsun.";
                case Feature.Destek: return "maaşlı destek ekibi müşteri sorunlarını senin yerine çözer.";
                case Feature.Market: return "kendi deponu aç, komisyon ödemeden sat.";
                case Feature.Grafik: return "günlük, haftalık ve aylık grafikler.";
                case Feature.Mesajlar: return "müşterilerin yorumları ve yıldızları burada.";
            }
            return "";
        }

        bool Condition(Feature f)
        {
            int day = GameClock.I != null ? GameClock.I.CompanyDay : 1;
            switch (f)
            {
                case Feature.Gelisim:
                    return day >= GelisimDay || (UpgradeManager.I != null && UpgradeManager.I.CourierCapFull);
                case Feature.Kampanya:
                    return day >= KampanyaDay || HasOpenGoal(GoalKind.BoostTeslimat);
                case Feature.Destek:
                    return day >= DestekDay ||
                           (CourierManager.I != null && CourierManager.I.Couriers.Count >= DestekCouriers) ||
                           (SupportManager.I != null && SupportManager.I.Agents.Count > 0);
                case Feature.Market:
                    return day >= MarketDay || (CityMap.I != null && CityMap.I.UnlockLevel >= DarkStore.RequiredDistrict) ||
                           (DarkStore.I != null && DarkStore.I.IsOpen);
                case Feature.Grafik:
                    return day >= GrafikDay;
                case Feature.Mesajlar:
                    return CourierMessages.I != null && CourierMessages.I.Messages.Count > 0;
            }
            return true;
        }

        /// <summary>Bu haftanın hedeflerinde bu tür bitmemiş bir hedef var mı (ör. boost hedefi gelirse Kampanya erken açılır).</summary>
        static bool HasOpenGoal(GoalKind kind)
        {
            if (GoalManager.I == null) return false;
            foreach (var g in GoalManager.I.Goals)
                if (g.kind == kind && !g.done) return true;
            return false;
        }

        void Update()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.5f;
            Evaluate(true);
        }

        /// <summary>Şartı gelen menüleri kalıcı olarak açar; istenirse bildirim gönderir.</summary>
        void Evaluate(bool notify)
        {
            for (int i = 0; i < FeatureCount; i++)
            {
                int bit = 1 << i;
                if ((unlockedMask & bit) != 0) continue;
                if (!Condition((Feature)i)) continue;
                unlockedMask |= bit;
                if (notify && !AllMenusSetting && EventManager.I != null)
                    EventManager.I.Notify($"Yeni menü açıldı! {FeatureNames[i]}: {UnlockText((Feature)i)}");
            }
        }
    }
}
