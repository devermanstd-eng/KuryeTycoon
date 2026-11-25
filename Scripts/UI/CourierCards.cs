using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Kimlik kartları ve kurye tablosu: sayı okumak yerine göz gezdirerek anlaşılsın.
    ///  • Kart: solda profil resmi (sağ altında tecrübe rozeti), sağda isim, araç simgesi ve durum etiketleri.
    ///    İşe Alım'daki başvurular ve Kuryeler penceresi aynı kartı kullanır.
    ///  • Beceriler "0.85 / 1.12" gibi sayılar yerine 1–5 yıldız (her beceri kendi aralığına göre). Üstüne gelince ne işe yaradığı yazar.
    ///  • Moral yüz simgesiyle (gülen / düz / üzgün); profil resmindeki yüz ifadesi de moralle değişir.
    ///  • Kuryeler penceresinde "Kartlar / Tablo": tabloda sütun başlığına tıklayınca sıralanır, satıra tıklayınca kartı açılır.
    /// </summary>
    public partial class GameUI
    {
        const float PortraitSize = 60f;
        static readonly Color StarOn = new Color(1f, 0.79f, 0.3f);
        static readonly Color StarOff = new Color(1f, 1f, 1f, 0.14f);
        static readonly Color FatigueColor = new Color(1f, 0.6f, 0.2f);

        GUIStyle cardName, tableHead, tableCell;

        void EnsureCardStyles()
        {
            if (cardName != null) return;
            cardName = UITheme.Semi(new GUIStyle(label) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = false, margin = new RectOffset(0, 4, 0, 2) });
            cardName.normal.textColor = UITheme.Yazi(Color.white);
            tableHead = UITheme.Semi(new GUIStyle(label) { fontSize = 12, fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip });
            tableCell = new GUIStyle(label) { fontSize = 13, wordWrap = false, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        }

        // ---------------------------------------------------------------- yıldızlar ve beceriler

        static int Stars(float v, float min, float max) => Mathf.Clamp(1 + Mathf.RoundToInt(Mathf.Clamp01((v - min) / (max - min)) * 4f), 1, 5);
        static int SpeedStars(float v) => Stars(v, 0.85f, 1.3f);
        static int CareStars(float v) => Stars(v, 0.55f, 1f);
        static int StaminaStars(float v) => Stars(v, 0.7f, 1.3f);

        // ---------------------------------------------------------------- kimlik kartı parçaları

        /// <summary>
        /// Kartın başı: solda profil resmi (tecrübe rozetiyle), sağ sütunda isim ve araç.
        /// Sonra çağıran sağ sütuna etiketleri vb. çizer ve <see cref="EndIdCardHead"/> ile kapatır.
        /// </summary>
        void BeginIdCardHead(string name, int mood, Color? shirt, VehicleKind vehicle, int level)
            => BeginIdCardHead(name, mood, shirt, Icons.Of(vehicle), GameData.Vehicle(vehicle).name, null, level);

        /// <summary>Aynı kart başı, araç yerine başka bir etiketle (ör. destek personeli). level 0 ise tecrübe rozeti yok.</summary>
        void BeginIdCardHead(string name, int mood, Color? shirt, Icons.Kind tagIcon, string tagText, Color? tagTint, int level)
        {
            EnsureCardStyles();
            GUILayout.BeginHorizontal();
            var pr = GUILayoutUtility.GetRect(PortraitSize, PortraitSize, GUILayout.Width(PortraitSize), GUILayout.Height(PortraitSize));
            Portraits.Draw(pr, name, mood, shirt);
            if (level > 0) LevelBadge(pr, level);
            GUILayout.Space(10);
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, cardName);
            GUILayout.FlexibleSpace();
            Tag(tagIcon, tagText, tagTint);
            GUILayout.EndHorizontal();
        }

        /// <summary>Profil resminin sağ alt köşesinde tecrübe rozeti (yıldız + seviye).</summary>
        void LevelBadge(Rect pr, int level)
        {
            if (Event.current.type != EventType.Repaint) return;
            var r = new Rect(pr.xMax - 26f, pr.yMax - 20f, 28f, 20f);
            UITheme.Box(r, new Color(0.07f, 0.075f, 0.09f, 0.96f), 7f);
            UITheme.Outline(r, UITheme.WithAlpha(StarOn, 0.55f), 1f, 7f);
            Icons.Draw(new Rect(r.x + 4f, r.y + 4.5f, 11f, 11f), Icons.Kind.Star, StarOn);
            kitHelp.normal.textColor = UITheme.Yazi(Color.white);
            GUI.Label(new Rect(r.x + 14f, r.y, r.width - 15f, r.height), level.ToString(), kitHelp);
        }

        /// <summary>Simge ve kısa yazı, yuvarlak zeminde (kartın sağ üstünde: araç, "Vardiyada"...). tint verilirse o renkte.</summary>
        void Tag(Icons.Kind icon, string text, Color? tint = null)
        {
            var content = new GUIContent(text);
            var sz = bodyDim.CalcSize(content);
            var r = GUILayoutUtility.GetRect(sz.x + 32f, 24f, GUILayout.Width(sz.x + 32f), GUILayout.Height(24f));
            if (Event.current.type != EventType.Repaint) return;
            var col = tint ?? UITheme.Text;
            UITheme.Box(r, tint.HasValue ? UITheme.WithAlpha(col, 0.16f) : new Color(1f, 1f, 1f, 0.07f), 12f);
            Icons.Draw(new Rect(r.x + 8f, r.y + 4f, 16f, 16f), icon, col);
            var old = bodyDim.normal.textColor;
            if (tint.HasValue) bodyDim.normal.textColor = UITheme.Yazi(Color.Lerp(col, Color.white, 0.3f));
            GUI.Label(new Rect(r.x + 27f, r.y + (r.height - sz.y) / 2f, sz.x + 4f, sz.y), content, bodyDim);
            bodyDim.normal.textColor = UITheme.Yazi(old);
        }

        // ---------------------------------------------------------------- kurye kartı

        // ---------------------------------------------------------------- başvuru kartı

        // ---------------------------------------------------------------- kartlar / tablo

        static readonly float[] ColW = { 0f, 34f, 116f, 46f, 58f, 46f, 84f };   // 0 = kalan genişlik (isim)

        /// <summary>Durum sırası: önce sorunlular, en sonda çevrimdışı.</summary>
        static int StateRank(Courier c)
        {
            if (c.cashBlocked) return 0;
            switch (c.State)
            {
                case CourierState.Izinli: return 1;
                case CourierState.AdresSorunu: return 2;
                case CourierState.Mola: return 3;
                case CourierState.Bosta: return 4;
                case CourierState.DukkanaGidiyor: case CourierState.Bekliyor: return 5;
                case CourierState.MusteriyeGidiyor: case CourierState.KataCikiyor: return 6;
                default: return 7;
            }
        }

        static string ShortState(Courier c)
        {
            if (c.cashBlocked) return "Hesap kilitli";
            switch (c.State)
            {
                case CourierState.Cevrimdisi: return "Çevrimdışı";
                case CourierState.Bosta: return "Boşta";
                case CourierState.DukkanaGidiyor: return "Dükkana";
                case CourierState.Bekliyor: return "Dükkanda";
                case CourierState.MusteriyeGidiyor: return "Müşteriye";
                case CourierState.AdresSorunu: return "Adres sorunu";
                case CourierState.KataCikiyor: return "Teslim ediyor";
                case CourierState.Mola: return "Molada";
                case CourierState.Izinli: return "İzinli";
                default: return c.StateName;
            }
        }
    }
}
