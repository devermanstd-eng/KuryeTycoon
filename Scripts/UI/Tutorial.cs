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

        /// <summary>Rehber kartı, hedef simgenin vurgusu ve kilitli simge ipucu. OnGUI'de bildirim kartlarından sonra çizilir.</summary>
        void DrawGuideOverlays(float sw, float sh)
        {
            DrawDockTooltip();
            DrawHelpTip(sw, sh);   // pencerelerdeki "?" balonu
            if (!TutorialVisible || UiAktif) return;   // yeni arayüzde rehber kartı ve vurgu UI Toolkit'te (Arayuz/UiBildirim.cs)
            InitTutorialStyles();
            var g = Guide.I;
            int index = Mathf.Clamp(g.TutorialStep, 0, TutorialSteps.Length - 1);
            var st = TutorialSteps[index];
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f);

            // Hedef yanıp söner: doğru menü açıksa pencere içindeki sekme, değilse sol çubuktaki menü
            if (st.target != null && !(panelOpen && tab == st.target.Value))
            {
                bool inWindow = panelOpen && GroupIndexOf(tab) == GroupIndexOf(st.target.Value) && subTabRects.ContainsKey(st.target.Value);
                var dr = inWindow ? subTabRects[st.target.Value] : DockRect(st.target.Value, sh);
                UITheme.Outline(new Rect(dr.x - 2f, dr.y - 2f, dr.width + 4f, dr.height + 4f), UITheme.WithAlpha(TutorialColor, pulse), 2.5f, 15f);
                if (!panelOpen && !inWindow)
                {
                    var pill = new Rect(dr.xMax + 10f, dr.center.y - 13f, 64f, 26f);
                    UITheme.Box(pill, UITheme.WithAlpha(TutorialColor, 0.85f + 0.15f * pulse), 13f);
                    tutStep.normal.textColor = new Color(0.1f, 0.1f, 0.12f);
                    tutStep.alignment = TextAnchor.MiddleCenter;
                    GUI.Label(pill, "← Tıkla", tutStep);
                }
            }

            // Kart
            var r = TutorialRect(sw, sh);
            UITheme.Shadowed(r, new Color(0.115f, 0.125f, 0.155f, 0.98f), 14f, 8f);
            UITheme.Outline(r, UITheme.WithAlpha(TutorialColor, 0.6f), 1.5f, 14f);
            float x = r.x + 16f, w = r.width - 32f, y = r.y + 14f;

            tutStep.alignment = TextAnchor.MiddleRight;
            tutStep.normal.textColor = UITheme.TextDim;
            GUI.Label(new Rect(x, y, w, 22f), $"Rehber {index + 1}/{TutorialSteps.Length}", tutStep);
            GUI.Label(new Rect(x, y, w - 90f, 24f), st.title, tutTitle);
            y += 26f;
            float th = tutText.CalcHeight(new GUIContent(st.text), w);
            GUI.Label(new Rect(x, y, w, th), st.text, tutText);
            y += th + 12f;

            bool last = index == TutorialSteps.Length - 1;
            bool waits = st.target != null || st.done != null;   // kendiliğinden geçen adım
            string next = last ? "Bitir" : (waits ? "Atla" : "Devam");
            var nextR = new Rect(x, y, 110f, 30f);
            if (GUI.Button(nextR, next, waits ? smallButton : button)) deferred = () => g.NextStep(TutorialSteps.Length);
            if (!last && GUI.Button(new Rect(x + w - 130f, y, 130f, 30f), "Rehberi kapat", smallButton))
                deferred = () => g.FinishTutorial();
        }
    }
}
