using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Onay penceresi: işe alma / çıkarma, sözleşme iptali, bölge açma gibi önemli ya da geri alınamayan işlemlerden
    /// ve <see cref="ConfirmThreshold"/> üstündeki harcamalardan önce sorar. Açıkken oyun durur, Esc = Vazgeç.
    /// Kullanım: deferred = () => AskConfirm("Başlık", "Açıklama", "Evet, yap", () => işlem());
    /// </summary>
    public partial class GameUI
    {
        /// <summary>Bu tutar ve üstündeki harcamalar onay ister.</summary>
        public const long ConfirmThreshold = 50000;

        string confirmTitle = "", confirmText = "", confirmYes = "";
        System.Action confirmAction;
        bool confirmWasPaused;

        bool ConfirmOpen => confirmAction != null;

        /// <summary>Onay penceresi açar; "Evet" denirse onYes çalışır.</summary>
        void AskConfirm(string title, string text, string yesLabel, System.Action onYes)
        {
            if (onYes == null) return;
            if (!ConfirmOpen && GameClock.I != null)
            {
                confirmWasPaused = GameClock.I.Paused;
                GameClock.I.Paused = true;
            }
            confirmTitle = title;
            confirmText = text;
            confirmYes = yesLabel;
            confirmAction = onYes;
        }

        /// <summary>Tutar eşiğin üstündeyse onay sorar, değilse hemen yapar.</summary>
        void ConfirmIfExpensive(long cost, string title, string text, System.Action onYes)
        {
            if (cost >= ConfirmThreshold) AskConfirm(title, text, $"Evet, öde ({Ft(cost)})", onYes);
            else onYes();
        }

        void CloseConfirm()
        {
            if (!ConfirmOpen) return;
            confirmAction = null;
            if (GameClock.I != null) GameClock.I.Paused = confirmWasPaused;
        }

        /// <summary>Onay açıkken Esc = Vazgeç, Enter = Evet.</summary>
        void HandleConfirmKey()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;
            if (e.keyCode == KeyCode.Escape)
            {
                e.Use();
                deferred = CloseConfirm;
            }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                e.Use();
                AcceptConfirm();
            }
        }

        void AcceptConfirm()
        {
            var a = confirmAction;
            deferred = () =>
            {
                CloseConfirm();
                a?.Invoke();
            };
        }

        void DrawConfirm(float sw, float sh)
        {
            Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, 0.5f));
            float w = 440f;
            float h = 114f + body.CalcHeight(new GUIContent(confirmText), w - 40f);   // metne göre: boşluk kalmasın
            var r = new Rect((sw - w) / 2f, (sh - h) / 2f, w, h);
            Fill(r, new Color(0.14f, 0.15f, 0.18f));
            GUILayout.BeginArea(new Rect(r.x + 20f, r.y + 16f, w - 40f, h - 32f));
            GUILayout.Label(confirmTitle, header);
            GUILayout.Space(4);
            GUILayout.Label(confirmText, body);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(confirmYes, button, GUILayout.Height(34))) AcceptConfirm();
            if (GUILayout.Button("Vazgeç", button, GUILayout.Height(34))) deferred = CloseConfirm;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
