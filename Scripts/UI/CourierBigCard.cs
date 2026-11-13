using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Büyük kurye kartı: Kuryeler penceresinde kartın "Büyüt" düğmesiyle ekranın ortasında açılır.
    /// Üstte kimlik (büyük portre, araç, meslek, çalışma saatleri, durum, moral); altında 2×2 kutu:
    /// Beceriler · Para / Performans · Ekipman. İçerik sığmazsa kart içinde kaydırılır, düğmeler hep altta görünür.
    /// Açıkken arkadaki arayüz kilitli; Esc, "Kapat" ya da kartın dışına tıklamak kapatır. Oyun durmaz.
    /// </summary>
    public partial class GameUI
    {
        Courier bigCardCourier;
        Vector2 bigCardScroll;

        bool BigCardOpen => bigCardCourier != null;

        void OpenBigCard(Courier c) { bigCardCourier = c; bigCardScroll = Vector2.zero; }

        void CloseBigCard() => bigCardCourier = null;

        void HandleBigCardKey()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                e.Use();
                deferred = CloseBigCard;
            }
        }

        void DrawBigCard(float sw, float sh)
        {
            var c = bigCardCourier;
            if (c == null || CourierManager.I == null || !CourierManager.I.Couriers.Contains(c)) { bigCardCourier = null; return; }
            EnsureCardStyles();
            GUI.enabled = true;

            float w = Mathf.Min(760f, sw - 40f);
            float h = Mathf.Min(680f, sh - 40f);
            var r = new Rect((sw - w) / 2f, (sh - h) / 2f, w, h);

            // Arka plan karartma; kartın dışına tıklamak kapatır
            Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, 0.55f));
            var e = Event.current;
            if (e.type == EventType.MouseDown && !r.Contains(e.mousePosition)) { e.Use(); deferred = CloseBigCard; }
            UITheme.Shadowed(r, UITheme.Bg, 14f, 6f);

            float innerW = w - 40f;
            GUILayout.BeginArea(new Rect(r.x + 20f, r.y + 16f, innerW, h - 32f));
            bigCardScroll = GUILayout.BeginScrollView(bigCardScroll, false, false);
            float contentW = innerW - 18f;   // kaydırma çubuğuna yer
            BigCardHead(c);
            GUILayout.Space(10);

            float colW = (contentW - 12f) / 2f;
            GUILayout.BeginHorizontal(GUILayout.Width(contentW));
            GUILayout.BeginVertical(box, GUILayout.Width(colW));
            BigCardSkills(c);
            GUILayout.EndVertical();
            GUILayout.Space(12);
            GUILayout.BeginVertical(box, GUILayout.Width(colW));
            BigCardMoney(c);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.BeginHorizontal(GUILayout.Width(contentW));
            GUILayout.BeginVertical(box, GUILayout.Width(colW));
            BigCardPerformance(c);
            GUILayout.EndVertical();
            GUILayout.Space(12);
            GUILayout.BeginVertical(box, GUILayout.Width(colW));
            BigCardEquipment(c);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            var courier = c;
            if (GUILayout.Button("Telefonunu aç", button, GUILayout.Height(30)))
                deferred = () => { CloseBigCard(); popupCourier = courier; popupScroll = Vector2.zero; CloseInfo(); };
            if (GUILayout.Button("Ekipman", button, GUILayout.Height(30)))
                deferred = () => { CloseBigCard(); selectedCourier = courier; tab = Tab.Ekipman; panelOpen = true; scroll = Vector2.zero; };
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Kapat", button, GUILayout.Width(110), GUILayout.Height(30))) deferred = CloseBigCard;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- parçalar

        void BigCardHead(Courier c)
        {
            const float big = 96f;
            GUILayout.BeginHorizontal();
            var pr = GUILayoutUtility.GetRect(big, big, GUILayout.Width(big), GUILayout.Height(big));
            Portraits.Draw(pr, c.courierName, Portraits.MoodOf(c.morale), CompanyProfile.Primary);
            if (c.level > 0) LevelBadge(pr, c.level);
            GUILayout.Space(14);
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label(c.courierName, cardName);
            GUILayout.FlexibleSpace();
            Tag(Icons.Of(c.vehicle), c.V.name);
            GUILayout.EndHorizontal();
            GUILayout.Label(c.OccupationName, small);
            if (GameClock.I != null)
                GUILayout.Label("Çalışma saatleri: " + string.Join("  ·  ", WorkInfoFor(c.occupation, c.scheduleSeed, GameClock.I).lines), small);
            Chip(c.StateName, CourierStateColor(c));
            if (c.fatigue >= 0.6f) Chip("Yorgun", UITheme.Warn);
            if (!c.insured) Chip("Sigortasız", NeutralTone);
            ChipRow(400f);
            MoodFatigueRow(c.morale, c.fatigue);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        void BigCardSkills(Courier c)
        {
            BigCardTitle("BECERİLER");
            BigSkillRow("Hız", SpeedStars(c.speedSkill), "teslimatları daha çabuk bitirir.");
            BigSkillRow("Dikkat", CareStars(c.care), "siparişte eksik ve hasar az olur, kaskını daha çok takar.");
            BigSkillRow("Nezaket", CareStars(c.politeness), "müşteriler daha yüksek puan verir.");
            BigSkillRow("Dayanıklılık", StaminaStars(c.stamina), "geç yorulur, kapıya ve kata daha çabuk çıkar.");
            GUILayout.Space(4);
            KV("Aynı anda", $"{c.EffectiveCapacity} paket");
            GUILayout.Label($"Seçicilik: {c.PickinessText}", small);
        }

        /// <summary>Beceri adı solda, yıldızlar aynı satırda sağda.</summary>
        void BigSkillRow(string name, int stars, string help)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, kitKey);
            var nameR = GUILayoutUtility.GetLastRect();
            GUILayout.FlexibleSpace();
            var sr = GUILayoutUtility.GetRect(5 * 15f, 20f, GUILayout.Width(5 * 15f), GUILayout.Height(20f));
            DrawStars(sr, stars, 13f);
            GUILayout.EndHorizontal();
            if (Event.current.type == EventType.Repaint && Rect.MinMaxRect(nameR.xMin, sr.yMin, sr.xMax, sr.yMax).Contains(Event.current.mousePosition))
                helpTip = $"<b>{name}</b> {stars}/5: {help}";
        }

        void BigCardPerformance(Courier c)
        {
            BigCardTitle("PERFORMANS");
            KV("Bugün", $"{c.deliveriesToday} teslimat · {Ft(c.earnedToday)}");
            KV("Bu hafta", $"{c.weekDeliveries} teslimat · {Ft(c.earnedWeek)}");
            KV("Toplam", $"{c.deliveries} teslimat");
            KV("Km", $"bugün {c.kmToday:0} · hafta {c.kmWeek:0}");
            var camp = CampaignManager.I;
            if (camp != null && camp.weeklyChallenge && !c.weeklyRewardClaimed)
                Bar("Haftalık", c.weekDeliveries / (float)Mathf.Max(1, camp.weeklyTarget), new Color(0.4f, 0.7f, 1f),
                    $"{c.weekDeliveries}/{camp.weeklyTarget}");
        }

        void BigCardMoney(Courier c)
        {
            BigCardTitle("PARA");
            KV("Hesabı", Ft(c.balance));
            KV("Kesim", EconomyManager.I.NextCutText);
            if (c.pendingPayout > 0) KV("Yatacak", Ft(c.pendingPayout));
            KV("Bugüne kadar yatan", Ft(c.receivedTotal));
            KV("Bahşiş (toplam)", Ft(c.tipsTotal));
            KV("Paket başı", Ft(c.V.basePay));
            KV("Km başı", $"{Ft(c.V.payPerKm)} + bonus");
        }

        void BigCardEquipment(Courier c)
        {
            BigCardTitle("EKİPMAN");
            GUILayout.Label(EquipmentText(c), small);
            GUILayout.Space(4);
            KV("Çanta kullanımı", $"{c.bagUses}/{Courier.BagWornUses}", c.BagWorn ? UITheme.Warn : (Color?)null);
            if (c.BagWorn) GUILayout.Label("Çanta yıprandı, yenisi gerekiyor.", warn);
            KV("Sigorta", c.insured ? $"var · haftalık {Ft(c.CurrentPremium)}" : "yok");
            KV("Araçları", c.VehiclesText);
        }

        void BigCardTitle(string text)
        {
            GUILayout.Label(text, bodyDim);
            GUILayout.Space(2);
        }
    }
}
