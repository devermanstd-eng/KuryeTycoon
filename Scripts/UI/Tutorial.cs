using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// İlk gün rehberi: haritanın altında küçük bir kart, sırayla ana menüleri gösterir.
    /// Hedef simge sol çubukta yanıp söner; o pencere açılınca kart kendiliğinden sonraki adıma geçer.
    /// "Atla" sadece o adımı, "Rehberi kapat" hepsini geçer. Adım kayda yazılır (bkz. <see cref="Guide"/>).
    /// </summary>
    public partial class GameUI
    {
        struct TutorialStepDef
        {
            public string title, text;
            public Tab? target;                  // vurgulanan simge: bu pencere açılınca adım geçer
            public System.Func<bool> done;       // hedef simge yoksa: adımın geçme şartı (yoksa "Devam" ile geçilir)
        }

        static readonly TutorialStepDef[] TutorialSteps =
        {
            new TutorialStepDef
            {
                title = "Hoş geldin! Kısa bir tur",
                text = "Haritadaki noktalar kuryelerin, simgeler sözleşmeli dükkanların. Siparişler kendiliğinden gelir ve " +
                       "otomatik atanır; senin işin şirketi büyütmek.\nSürükle = kaydır · tekerlek = yakınlaş · Boşluk = duraklat"
            },
            new TutorialStepDef
            {
                title = "Siparişler", target = Tab.Siparisler,
                text = "Bekleyen ve yoldaki siparişler burada. Soldaki yanıp sönen Siparişler simgesine tıkla."
            },
            new TutorialStepDef
            {
                title = "Kuryeler", target = Tab.Kuryeler,
                text = "Ekip menüsünü aç. Kuryeler serbest çalışır, istedikleri saatte çevrimiçi olurlar; durumlarını burada görürsün."
            },
            new TutorialStepDef
            {
                title = "Kayıtlar", target = Tab.IseAlim,
                text = "Ekip menüsünün Kayıtlar sekmesine geç. Kuryeler uygulamaya kendileri kaydolur ve süreci bitirince hesapları açılır; sen kuralları ve kampanyaları belirlersin."
            },
            new TutorialStepDef
            {
                title = "Dükkanlar", target = Tab.Dukkanlar,
                text = "İşletme menüsünü aç. Daha çok dükkanla sözleşme = daha çok sipariş; yeni bölgeleri de buradan açarsın."
            },
            new TutorialStepDef
            {
                title = "Hedefler", target = Tab.Hedefler,
                text = "Her pazartesi 6 hedef gelir. Tamamladığın hedefin ödülü hemen kasana girer."
            },
            new TutorialStepDef
            {
                title = "Hız",
                text = "Zamanı üst bardan ya da 1–8 tuşlarıyla hızlandır. Devam etmek için 2x ya da üstünü seç.",
                done = () => GameClock.I != null && !GameClock.I.Paused && GameClock.I.SpeedMultiplier >= 2f
            },
            new TutorialStepDef
            {
                title = "Hazırsın!",
                text = "Kasanı ve kredi taksitlerini Finans'ta izle; yıl sonunda vergi var, kasanda yedek tut. " +
                       "Soluk menüler ilerleyen günlerde açılacak. Esc = menü (kaydet / çık)."
            },
        };

        static readonly Color TutorialColor = new Color(1f, 0.82f, 0.25f);
        GUIStyle tutTitle, tutText, tutStep;

        bool TutorialVisible =>
            Guide.I != null && !Guide.I.TutorialDone && GameBootstrap.I != null && GameBootstrap.I.Started &&
            EventManager.I != null && EventManager.I.Current == null && !pauseMenuOpen && !ConfirmOpen;

        TutorialStepDef CurrentStep => TutorialSteps[Mathf.Clamp(Guide.I.TutorialStep, 0, TutorialSteps.Length - 1)];

        /// <summary>LateUpdate'te: adımın şartı gerçekleştiyse sonraki adıma geç.</summary>
        void UpdateTutorial()
        {
            if (!TutorialVisible) return;
            var g = Guide.I;
            if (g.TutorialStep >= TutorialSteps.Length) { g.FinishTutorial(); return; }
            var st = TutorialSteps[g.TutorialStep];
            bool met = st.target != null ? panelOpen && tab == st.target.Value : st.done != null && st.done();
            if (met) g.NextStep(TutorialSteps.Length);
        }

        void InitTutorialStyles()
        {
            if (tutTitle != null) return;
            tutTitle = new GUIStyle(header) { wordWrap = true, margin = new RectOffset(0, 0, 0, 0) };
            tutTitle.normal.textColor = UITheme.Text;
            tutText = new GUIStyle(label) { wordWrap = true };
            tutText.normal.textColor = UITheme.Text;
            tutStep = new GUIStyle(small) { alignment = TextAnchor.MiddleRight, wordWrap = false };
            tutStep.normal.textColor = UITheme.TextDim;
        }

        const float TutorialW = 390f;

        /// <summary>Kartın yeri: haritanın alt ortası (pencere, lejant ve haber akışının arasında); yer yoksa akışın üstü.</summary>
        Rect TutorialRect(float sw, float sh)
        {
            InitTutorialStyles();
            float w = TutorialW;
            float h = 16f + 24f + tutText.CalcHeight(new GUIContent(CurrentStep.text), w - 32f) + 12f + 30f + 14f;
            float left = Mathf.Max(MapLeftGui, panelOpen ? WindowRect(sh).xMax : DockW) + 12f;
            if (legendOpen) left = Mathf.Max(left, LegendRect(sh).xMax + 12f);
            float right = sw - 530f - 12f;   // haber akışının solu
            if (right - left >= w)
                return new Rect(left + (right - left - w) / 2f, sh - h - 54f, w, h);
            return new Rect(sw - w - 12f, sh - 22f * 5f - 22f - h - 12f, w, h);
        }

        // Rehber kartı ve hedef simge vurgusu UI Toolkit'te (Arayuz/UiBildirim.cs). Eski IMGUI çizimi 25.11'de silindi.
    }
}
