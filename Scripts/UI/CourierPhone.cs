using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Kurye telefonu: haritada kuryeye tıklayınca sağdan açılan, gerçek kurye uygulamalarına benzeyen ekran.
    /// Ekranlar: Ana ekran (harita + alt panel: teklif / aktif görev / yoğunluk), Menü, İstatistikler,
    /// Ekstra kazan, Analizler, Finans, Sigorta, Profil, Gelen kutusu, Destek.
    /// Tamamen bilgi amaçlı: kurye yine kendi kararlarını verir.
    /// </summary>
    public partial class GameUI
    {
        enum PhoneScreen { Home, Menu, Stats, Earn, Insights, Finance, Insurance, Profile, Inbox, Support, PrevTask }

        PhoneScreen phoneScreen = PhoneScreen.Home;
        int statsTab;               // 0 bugün, 1 hafta, 2 ay
        int supportOpen = -1;       // açık destek konusu
        float phoneAnim;            // 0 → 1: telefon sağdan kayarak gelir
        Courier phoneAnimFor;

        // ---------- Renkler (koyu lacivert, şirket rengi vurgulu) ----------
        static readonly Color PBg = new Color(0.045f, 0.05f, 0.075f);
        static readonly Color PSheet = new Color(0.085f, 0.09f, 0.135f);
        static readonly Color PCard = new Color(0.12f, 0.13f, 0.185f);
        static readonly Color PLine = new Color(1f, 1f, 1f, 0.08f);
        static readonly Color PDim = new Color(0.58f, 0.61f, 0.72f);
        static Color PAccent => Color.Lerp(CompanyProfile.Primary, Color.white, 0.32f);
        static readonly Color PGreen = new Color(0.45f, 0.85f, 0.45f);
        static readonly Color PRed = new Color(0.98f, 0.45f, 0.40f);

        GUIStyle pHuge, pTitle, pBody, pDimS, pSmall, pAcc, pRowT, pCenter, pBtnText;
        Color pStylesFor;

        void EnsurePhoneStyles()
        {
            if (pHuge != null && pStylesFor == CompanyProfile.Primary) return;
            pStylesFor = CompanyProfile.Primary;
            var f = UITheme.UIFont;
            GUIStyle Mk(int size, FontStyle fs, Color c, bool wrap = true)
            {
                var st = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs, wordWrap = wrap, richText = true, padding = new RectOffset(0, 0, 1, 1) };
                if (f != null) st.font = f;
                st.normal.textColor = c;
                return st;
            }
            pHuge = Mk(26, FontStyle.Bold, new Color(0.93f, 0.92f, 0.90f));
            pTitle = Mk(18, FontStyle.Bold, Color.white);
            pBody = Mk(13, FontStyle.Normal, new Color(0.92f, 0.93f, 0.96f));
            pDimS = Mk(12, FontStyle.Normal, PDim);
            pSmall = Mk(10, FontStyle.Bold, PDim, false);
            pAcc = Mk(14, FontStyle.Bold, PAccent);
            pRowT = Mk(15, FontStyle.Normal, new Color(0.93f, 0.94f, 0.97f), false);
            pCenter = Mk(12, FontStyle.Bold, Color.white, false);
            pCenter.alignment = TextAnchor.MiddleCenter;
            pBtnText = Mk(15, FontStyle.Bold, new Color(0.04f, 0.05f, 0.08f), false);
            pBtnText.alignment = TextAnchor.MiddleCenter;
        }

        Rect PhoneRect(float sw, float sh)
        {
            float w = 340f;
            float h = Mathf.Clamp(sh - TopBar - 70f, 460f, 720f);
            float slide = (1f - Mathf.SmoothStep(0f, 1f, phoneAnim)) * (w + 40f);
            return new Rect(sw - w - 18f + slide, TopBar + 14f, w, h);
        }

        void GoPhone(PhoneScreen s)
        {
            deferred = () => { phoneScreen = s; popupScroll = Vector2.zero; supportOpen = -1; };
        }

        // ====================================================================== çerçeve

        void DrawCourierPopup(float sw, float sh)
        {
            var c = popupCourier;
            if (c == null) { phoneAnim = 0f; return; }
            if (!CourierManager.I.Couriers.Contains(c)) { popupCourier = null; return; }
            EnsurePhoneStyles();
            if (phoneAnimFor != c)
            {
                phoneAnimFor = c;
                phoneScreen = PhoneScreen.Home;
                popupScroll = Vector2.zero;
                phoneTaskTab = 0;
                phoneOfflineEdit = false;
                phoneConfirmCancel = -1;
                if (phoneAnim > 0.5f) phoneAnim = 0.85f;
            }
            if (Event.current.type == EventType.Repaint) phoneAnim = Mathf.MoveTowards(phoneAnim, 1f, Time.unscaledDeltaTime * 4f);

            var r = PhoneRect(sw, sh);
            var metal = new Color(0.36f, 0.38f, 0.42f);

            // Gölge, yan tuşlar, gövde ve ekran zemini
            UITheme.Box(new Rect(r.x - 2, r.y + 8, r.width + 4, r.height + 8), new Color(0f, 0f, 0f, 0.35f), 48f);
            UITheme.Box(new Rect(r.x - 3, r.y + 120, 4, 46), metal, 2f);
            UITheme.Box(new Rect(r.x - 3, r.y + 176, 4, 46), metal, 2f);
            UITheme.Box(new Rect(r.xMax - 1, r.y + 150, 4, 70), metal, 2f);
            UITheme.Box(r, new Color(0.03f, 0.032f, 0.04f), 46f);
            var scr = new Rect(r.x + 10, r.y + 10, r.width - 20, r.height - 20);
            UITheme.Box(scr, PBg, 37f);

            switch (phoneScreen)
            {
                case PhoneScreen.Home: PhoneHome(c, scr); break;
                case PhoneScreen.Menu: PhoneMenu(c, scr); break;
                default: PhoneSubPage(c, scr); break;
            }

            // Çerçeve en son: köşelere taşan içeriği örter
            UITheme.Outline(r, new Color(0.03f, 0.032f, 0.04f), 11f, 46f);
            UITheme.Outline(r, metal, 2f, 46f);
            PhoneStatusBar(c, scr);
            UITheme.Box(new Rect(scr.center.x - 60, scr.yMax - 11, 120, 5), UITheme.WithAlpha(Color.white, 0.6f), 2.5f);
        }

        void PhoneStatusBar(Courier c, Rect scr)
        {
            var clock = GameClock.I;
            var st = pCenter;
            var oldA = st.alignment;
            st.alignment = TextAnchor.MiddleLeft;
            st.fontSize = 13;
            GUI.Label(new Rect(scr.x + 28, scr.y + 10, 70, 22), $"{clock.Hour:00}:{clock.Minute:00}", st);
            st.fontSize = 12;
            st.alignment = oldA;
            UITheme.Box(new Rect(scr.center.x - 48, scr.y + 9, 96, 26), Color.black, 13f);          // dinamik ada
            float sx = scr.xMax - 76;
            for (int i = 0; i < 4; i++)
                UITheme.Box(new Rect(sx + i * 5, scr.y + 25 - 3 - i * 2.5f, 3, 3 + i * 2.5f), i < 3 ? Color.white : UITheme.WithAlpha(Color.white, 0.35f), 1f);
            float battery = c.Has(EquipmentKind.Powerbank) ? 0.95f : Mathf.Clamp(1f - c.onlineMinutesToday / 720f, 0.08f, 1f);
            var bat = new Rect(scr.xMax - 48, scr.y + 15, 24, 12);
            UITheme.Box(bat, UITheme.WithAlpha(Color.white, 0.3f), 3.5f);
            UITheme.Box(new Rect(bat.x, bat.y, bat.width * battery, bat.height), battery < 0.2f ? PRed : Color.white, 3.5f);
            UITheme.Box(new Rect(bat.xMax + 1, bat.y + 4, 2, 4), UITheme.WithAlpha(Color.white, 0.5f), 1f);
        }

        /// <summary>Yuvarlak simge düğmesi (koyu daire).</summary>
        bool RoundIcon(Rect r, System.Action<Rect> drawIcon)
        {
            UITheme.Box(r, new Color(0.06f, 0.065f, 0.09f, 0.95f), r.width / 2f);
            drawIcon(r);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        static void IconMenu(Rect r)
        {
            for (int i = 0; i < 3; i++) UITheme.Box(new Rect(r.center.x - 8, r.center.y - 6 + i * 5.5f, 16, 2), Color.white, 1f);
        }

        static void IconBell(Rect r)
        {
            UITheme.Box(new Rect(r.center.x - 6, r.center.y - 7, 12, 11), Color.white, 5f);
            UITheme.Box(new Rect(r.center.x - 8, r.center.y + 2, 16, 3), Color.white, 1.5f);
            UITheme.Box(new Rect(r.center.x - 2, r.center.y + 6, 4, 3), Color.white, 1.5f);
        }

        static void IconClose(Rect r)
        {
            // Çarpı: iki küçük kare dizisi
            for (int i = -4; i <= 4; i++)
            {
                UITheme.Box(new Rect(r.center.x + i - 1, r.center.y + i - 1, 2.5f, 2.5f), Color.white, 1f);
                UITheme.Box(new Rect(r.center.x + i - 1, r.center.y - i - 1, 2.5f, 2.5f), Color.white, 1f);
            }
        }

        static void IconBack(Rect r)
        {
            UITheme.Box(new Rect(r.center.x - 7, r.center.y - 1, 15, 2.2f), Color.white, 1f);
            for (int i = 0; i <= 5; i++)
            {
                UITheme.Box(new Rect(r.center.x - 7 + i - 1, r.center.y - i - 1, 2.5f, 2.5f), Color.white, 1f);
                UITheme.Box(new Rect(r.center.x - 7 + i - 1, r.center.y + i - 1, 2.5f, 2.5f), Color.white, 1f);
            }
        }

        // ====================================================================== ana ekran

        void PhoneHome(Courier c, Rect scr)
        {
            float mapH = scr.height * 0.46f;
            var map = new Rect(scr.x, scr.y, scr.width, mapH + 30f);
            PhoneMap(c, map);

            // Harita üstü düğmeler
            if (RoundIcon(new Rect(scr.x + 16, scr.y + 44, 40, 40), IconMenu)) GoPhone(PhoneScreen.Menu);
            if (RoundIcon(new Rect(scr.xMax - 104, scr.y + 44, 40, 40), IconBell)) GoPhone(PhoneScreen.Inbox);
            if (c.inboxText.Count > 0) UITheme.Box(new Rect(scr.xMax - 74, scr.y + 46, 9, 9), PRed, 4.5f);
            if (RoundIcon(new Rect(scr.xMax - 56, scr.y + 44, 40, 40), IconClose)) deferred = () => popupCourier = null;

            // Alt panel
            var sheet = new Rect(scr.x, scr.y + mapH, scr.width, scr.height - mapH);
            UITheme.Box(sheet, PSheet, 24f);
            UITheme.Box(new Rect(sheet.center.x - 24, sheet.y + 8, 48, 4), UITheme.WithAlpha(Color.white, 0.22f), 2f);

            bool showOffer = c.lastOffer != null && Time.unscaledTime - c.lastOfferRealTime < 4f;
            bool task = !showOffer && c.CarryingList.Count + c.ToPickList.Count > 0;
            var area = new Rect(sheet.x + 18, sheet.y + 20, sheet.width - 36, sheet.height - 40 - (task ? 64f : 0f));
            phoneContentWidth = area.width;
            GUILayout.BeginArea(area);
            popupScroll = GUILayout.BeginScrollView(popupScroll, GUIStyle.none, GUIStyle.none);

            if (showOffer) HomeOffer(c);
            else if (task) HomeTaskApp(c);
            else HomeIdle(c);

            GUILayout.Space(8);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            // Gerçek uygulamadaki gibi altta sabit büyük düğme (kurye durumu)
            if (task) TaskStatusPill(c, new Rect(sheet.x + 14, sheet.yMax - 22 - 54, sheet.width - 28, 54));
        }

        // ---------- Harita ----------

        void PhoneMap(Courier c, Rect map)
        {
            var cm = CityMap.I;
            Vector2 me = c.Position;
            Order cur = c.CurrentOrder;
            Vector2 target = me;
            bool toShop = c.State == CourierState.DukkanaGidiyor || c.State == CourierState.Bekliyor;
            if (cur != null) target = toShop ? (Vector2)cur.shop.tile : (Vector2)cur.customerTile;

            Vector2 center = cur != null ? (me + target) / 2f : me;
            float span = cur != null ? Mathf.Max(Mathf.Abs(target.x - me.x), Mathf.Abs(target.y - me.y)) + 3f : 6f;
            float s = Mathf.Clamp(Mathf.Min(map.width, map.height - 40f) / (span * 2f), 9f, 24f);

            GUI.BeginGroup(map);
            var local = new Rect(0, 0, map.width, map.height);
            Fill(local, new Color(0.13f, 0.15f, 0.20f));
            Vector2 P(Vector2 t) => new Vector2((t.x - center.x) * s + map.width / 2f, (center.y - t.y) * s + map.height / 2f - 10f);

            int x0 = Mathf.Max(0, Mathf.FloorToInt(center.x - map.width / 2f / s) - 1);
            int x1 = Mathf.Min(CityMap.Width - 1, Mathf.CeilToInt(center.x + map.width / 2f / s) + 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(center.y - map.height / 2f / s) - 2);
            int y1 = Mathf.Min(CityMap.Height - 1, Mathf.CeilToInt(center.y + map.height / 2f / s) + 2);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    var p = P(new Vector2(x, y));
                    var tr = new Rect(p.x - s / 2f, p.y - s / 2f, s + 0.5f, s + 0.5f);
                    Color col;
                    if (cm.IsRoad(x, y)) col = CityMap.BridgeAt(x, y) ? new Color(0.36f, 0.36f, 0.42f) : new Color(0.27f, 0.29f, 0.38f);
                    else if (CityMap.WaterAt(x, y)) col = new Color(0.12f, 0.28f, 0.45f);
                    else col = Color.Lerp(cm.MiniColor(x, y), new Color(0.10f, 0.12f, 0.17f), 0.62f);   // uygulamadaki koyu harita teması
                    if (!cm.IsUnlocked(x, y)) col = Color.Lerp(col, Color.black, 0.5f);
                    Fill(tr, col);
                    if (!cm.IsRoad(x, y) && !CityMap.WaterAt(x, y)) Fill(new Rect(tr.x + 1.5f, tr.y + 1.5f, tr.width - 3f, tr.height - 3f), Color.Lerp(col, Color.white, 0.03f));
                }

            // Yakındaki dükkanlar (POI)
            var poi = new GUIStyle(pSmall) { fontSize = 9 };
            foreach (var shop in cm.Shops)
            {
                if (!shop.contracted) continue;
                var p = P(shop.tile);
                if (p.x < -20 || p.y < -20 || p.x > map.width + 20 || p.y > map.height + 20) continue;
                UITheme.Box(new Rect(p.x - 5, p.y - 5, 10, 10), shop.def.color, 5f);
                poi.normal.textColor = new Color(0.98f, 0.85f, 0.45f);
                GUI.Label(new Rect(p.x + 7, p.y - 7, 100, 14), shop.Name, poi);
            }

            // Rota
            var path = c.PathList;
            if (cur != null && path != null && path.Count > 0)
            {
                Vector2 prev = me;
                for (int i = c.PathIndex; i < path.Count; i++)
                {
                    DrawRouteSeg(P(prev), P(path[i]), 4.5f);
                    prev = path[i];
                }
            }

            // Hedefler
            if (cur != null)
            {
                var sp = P(cur.shop.tile);
                Ring(sp, 9f, PAccent);
                var cp = P(cur.customerTile);
                Ring(cp, 9f, PGreen);

                // Balon: "Teslim alma X dk" / "Teslimat X dk"
                float eta = EtaMinutes(c);
                string label = c.State == CourierState.KataCikiyor ? $"Binada {c.DoorMinutesLeft:0} dk"
                             : c.State == CourierState.Bekliyor ? "Dükkanda bekliyor"
                             : toShop ? $"Teslim alma {eta:0} dk" : $"Teslimat {eta:0} dk";
                var tp = toShop ? sp : cp;
                float bw = pCenter.CalcSize(new GUIContent(label)).x + 46f;
                var bubble = new Rect(Mathf.Clamp(tp.x - bw / 2f, 6, map.width - bw - 6), Mathf.Max(48f, tp.y - 48f), bw, 30);
                UITheme.Box(bubble, new Color(0.05f, 0.055f, 0.08f, 0.96f), 15f);
                UITheme.Box(new Rect(bubble.x + 3, bubble.y + 3, 24, 24), toShop ? PAccent : PGreen, 12f);
                GUI.Label(new Rect(bubble.x + 30, bubble.y, bw - 34, 30), label, pCenter);
            }

            // Kurye
            var mp = P(me);
            UITheme.Box(new Rect(mp.x - 12, mp.y - 12, 24, 24), Color.white, 12f);
            var vl = new GUIStyle(pCenter) { fontSize = 11 };
            vl.normal.textColor = Color.black;
            GUI.Label(new Rect(mp.x - 12, mp.y - 12, 24, 24), c.V.letter, vl);

            GUI.EndGroup();
        }

        static void DrawRouteSeg(Vector2 a, Vector2 b, float w)
        {
            // Izgarada yollar düz: önce yatay, sonra dikey parça
            var corner = new Vector2(b.x, a.y);
            UITheme.Box(new Rect(Mathf.Min(a.x, corner.x) - w / 2f, a.y - w / 2f, Mathf.Abs(corner.x - a.x) + w, w), Color.white, w / 2f);
            UITheme.Box(new Rect(b.x - w / 2f, Mathf.Min(corner.y, b.y) - w / 2f, w, Mathf.Abs(b.y - corner.y) + w), Color.white, w / 2f);
        }

        static void Ring(Vector2 p, float rad, Color c)
        {
            UITheme.Box(new Rect(p.x - rad - 3, p.y - rad - 3, (rad + 3) * 2, (rad + 3) * 2), c, rad + 3);
            UITheme.Box(new Rect(p.x - rad, p.y - rad, rad * 2, rad * 2), new Color(0.06f, 0.07f, 0.10f), rad);
        }

        static float EtaMinutes(Courier c)
        {
            var path = c.PathList;
            if (path == null || path.Count == 0) return 0f;
            float tiles = 0f;
            Vector2 prev = c.Position;
            for (int i = c.PathIndex; i < path.Count; i++) { tiles += Vector2.Distance(prev, path[i]); prev = path[i]; }
            return tiles / Mathf.Max(0.05f, c.Speed);
        }

        // ---------- Alt panel içerikleri ----------

        void HomeOffer(Courier c)
        {
            var o = c.lastOffer;
            GUILayout.Label(Ft(c.lastOfferPay), pHuge);
            GUILayout.Label("Teslimatın tamamı için tahmini kazanç", pDimS);
            PLineSpace();
            GUILayout.Label("<b>Teslim alınacak yer</b>", pBody);
            GUILayout.Label($"{o.shop.Name} | {Addresses.StreetOf(o.shop.door)}", pAcc);
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            KeyVal("Rota mesafesi", GameData.KmText(o.distance));
            float est = c.EstimateMinutes(o);
            KeyVal("Tahmini", $"{Mathf.Max(1f, est * 0.8f):0} – {est * 1.25f + 2f:0} dk");
            GUILayout.EndHorizontal();
            if (o.cashPay)
            {
                PLineSpace();
                KeyVal("Nakit sipariş", o.CashChange > 0 ? $"{Ft(o.CashChange)} para üstü" : "Para üstü gerekmiyor");
            }
            if (o.weightKg >= 8f)
            {
                PLineSpace();
                KeyVal("Ağır sipariş", GameData.KgText(o.weightKg));
            }
            GUILayout.Space(10);
            BigPill(c.lastOfferAccepted ? "Kabul etti" : "Reddetti", c.lastOfferAccepted ? PAccent : PRed, false);
            if (!c.lastOfferAccepted && !string.IsNullOrEmpty(c.lastOfferReason))
                GUILayout.Label($"Neden: {c.lastOfferReason}", pDimS);
        }

        void HomeTask(Courier c)
        {
            var cur = c.CurrentOrder;
            float eta = EtaMinutes(c);
            string head = c.State == CourierState.KataCikiyor ? $"Binada · {c.DoorMinutesLeft:0} dk"
                        : c.State == CourierState.Bekliyor ? "Dükkanda bekliyor"
                        : c.State == CourierState.DukkanaGidiyor ? $"Teslim alma · {eta:0} dk"
                        : c.State == CourierState.AdresSorunu ? "Adreste sorun var"
                        : $"Teslimat · {eta:0} dk";
            GUILayout.Label(head, pHuge);
            if (cur != null)
                GUILayout.Label(c.State == CourierState.DukkanaGidiyor || c.State == CourierState.Bekliyor
                    ? $"{cur.shop.Name} | {Addresses.StreetOf(cur.shop.door)}" : cur.AddressText, pAcc);
            GUILayout.Label(c.StateName, pDimS);
            GUILayout.Space(4);
            float now = GameClock.I.TotalMinutes;
            foreach (var o in c.CarryingList) OrderCard(o, c, true, now);
            foreach (var o in c.ToPickList) OrderCard(o, c, false, now);
        }

        void HomeIdle(Courier c)
        {
            if (c.IsOnline)
            {
                int level = DemandLevel();
                string[] titles = { "Normalden sakin", "Normal yoğunluk", "Çok yoğun" };
                string[] names = { "Düşük", "Normal", "Yüksek" };
                GUILayout.Label(titles[level], pHuge);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Sipariş yoğunluğu", pDimS, GUILayout.ExpandWidth(false));
                var r = GUILayoutUtility.GetRect(26, 18, GUILayout.Width(26));
                for (int i = 0; i < 3; i++)
                {
                    Color bc = i <= level ? (level == 0 ? PRed : level == 1 ? new Color(1f, 0.75f, 0.3f) : PGreen) : UITheme.WithAlpha(PDim, 0.35f);
                    UITheme.Box(new Rect(r.x + 4 + i * 6, r.y + 13 - 4 - i * 3, 4, 5 + i * 3), bc, 1f);
                }
                GUILayout.Label(names[level], pDimS);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else
            {
                string district = CityMap.DistrictOf(Vector2Int.RoundToInt(c.Position));
                GUILayout.Label(district, pHuge);
                GUILayout.Label(c.StateName, pDimS);
            }
            GUILayout.Space(8);
            BoostCard(true);
            GUILayout.Space(10);
            BigPill(c.IsOnline ? "Çevrimiçi · sipariş bekliyor" : "Çevrimdışı", c.IsOnline ? PAccent : new Color(0.30f, 0.32f, 0.40f), true);
            GUILayout.Label(c.OccupationStatus, pDimS);
        }

        static int DemandLevel()
        {
            var om = OrderManager.I;
            if (om != null && om.IsBusy) return 2;
            float sum = 0f; int n = 0;
            var clock = GameClock.I;
            foreach (var s in CityMap.I.Shops)
                if (s.contracted) { sum += OrderManager.DemandCurve(s.def, clock.Hour, clock.IsWeekend); n++; }
            float avg = n > 0 ? sum / n : 0f;
            return avg < 0.7f ? 0 : avg < 1.3f ? 1 : 2;
        }

        /// <summary>Aktif ya da sıradaki boost kartı (sola resim yerine renkli blok).</summary>
        void BoostCard(bool compact)
        {
            var camp = CampaignManager.I;
            var active = camp.ActiveWindow;
            float wb = camp.WeatherBoostPercent;
            string text;
            if (active != null && active.percent >= wb) text = $"+%{active.percent * 100f:0} · {active.TimeText}";
            else if (wb > 0f) text = $"+%{wb * 100f:0} kötü hava boostu · şimdi";
            else
            {
                BoostWindow next = null;
                foreach (var w in camp.TodayWindows())
                    if (w.startHour > GameClock.I.Hour && (next == null || w.startHour < next.startHour)) next = w;
                text = next != null ? $"+%{next.percent * 100f:0} · {next.startHour:00}:00'dan itibaren" : "Bugün başka boost yok";
            }
            var r = GUILayoutUtility.GetRect(100, 44, GUILayout.ExpandWidth(true));
            UITheme.Box(r, new Color(0.08f, 0.24f, 0.30f), 12f);
            UITheme.Box(new Rect(r.x, r.y, 52, r.height), Color.Lerp(CompanyProfile.Primary, Color.white, 0.1f), 12f);
            GUI.Label(new Rect(r.x, r.y, 52, r.height), "%", pCenter);
            var t = new GUIStyle(pBody) { fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleLeft };
            GUI.Label(new Rect(r.x + 64, r.y, r.width - 90, r.height), text, t);
            GUI.Label(new Rect(r.xMax - 26, r.y, 20, r.height), ">", t);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) GoPhone(PhoneScreen.Earn);
        }

        /// <summary>Büyük hap: solda koyu daire içinde ok, ortada yazı.</summary>
        void BigPill(string text, Color c, bool arrow)
        {
            var r = GUILayoutUtility.GetRect(100, 56, GUILayout.ExpandWidth(true));
            UITheme.Box(r, c, 28f);
            if (arrow)
            {
                var d = new Rect(r.x + 5, r.y + 5, 46, 46);
                UITheme.Box(d, new Color(0.03f, 0.035f, 0.05f), 23f);
                for (int i = 0; i <= 6; i++)
                {
                    UITheme.Box(new Rect(d.center.x - 3 + i - 1, d.center.y - 6 + i - 1, 3, 3), c, 1.5f);
                    UITheme.Box(new Rect(d.center.x - 3 + i - 1, d.center.y + 6 - i - 1, 3, 3), c, 1.5f);
                }
            }
            GUI.Label(new Rect(r.x + (arrow ? 40 : 0), r.y, r.width - (arrow ? 40 : 0), r.height), text, pBtnText);
        }

        void KeyVal(string k, string v)
        {
            GUILayout.BeginVertical();
            GUILayout.Label(k, pDimS);
            GUILayout.Label(v, new GUIStyle(pBody) { fontSize = 16, wordWrap = false });
            GUILayout.EndVertical();
        }

        void PLineSpace()
        {
            var r = GUILayoutUtility.GetRect(10, 13, GUILayout.ExpandWidth(true));
            Fill(new Rect(r.x - 18, r.y + 6, r.width + 36, 1), PLine);
        }

        /// <summary>Sipariş kartı: durum etiketi, ürünler, adres, kat, nakit, kalan süre.</summary>
        void OrderCard(Order o, Courier c, bool inBag, float now)
        {
            var start = GUILayoutUtility.GetRect(1, 2, GUILayout.ExpandWidth(true));
            GUILayout.BeginVertical();
            bool currentOne = c.CurrentOrder == o;
            string where = inBag ? (currentOne ? "TESLİM EDİLİYOR" : "ÇANTADA") : (currentOne ? "DÜKKANA GİDİYOR" : "ALINACAK");
            Color tagC = inBag ? (currentOne ? PAccent : PGreen) : (currentOne ? new Color(1f, 0.75f, 0.3f) : PDim);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>#{o.id} {o.shop.Name}</b>", pBody);
            GUILayout.FlexibleSpace();
            PTag(where, tagC);
            GUILayout.EndHorizontal();
            GUILayout.Label($"{o.ItemCount} ürün · {GameData.KgText(o.weightKg)} — {o.ItemsText}", pDimS);
            GUILayout.Label($"{o.customerName} · {o.AddressText}", pDimS);
            GUILayout.Label(o.BuildingText, o.UsesStairs && o.floor >= 4 ? new GUIStyle(pDimS) { normal = { textColor = new Color(1f, 0.7f, 0.35f) } } : pDimS);
            if (o.cashPay) GUILayout.Label($"Nakit: {Ft(o.CashAmount)} · müşteri {Ft(o.cashGiven)} verecek · para üstü {Ft(o.CashChange)}", new GUIStyle(pDimS) { normal = { textColor = new Color(1f, 0.7f, 0.35f) } });
            if (!string.IsNullOrEmpty(o.customerNote)) GUILayout.Label($"\"{o.customerNote}\"", new GUIStyle(pDimS) { fontStyle = FontStyle.Italic });
            float left = o.deadlineAt - now;
            GUILayout.BeginHorizontal();
            GUILayout.Label(left >= 0 ? $"{left:0} dk kaldı" : $"{-left:0} dk gecikmede", left >= 0 ? pBody : new GUIStyle(pBody) { normal = { textColor = PRed } });
            GUILayout.FlexibleSpace();
            GUILayout.Label($"<b>{Ft(o.courierPay + o.boostPay)}</b>", pBody);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            var end = GUILayoutUtility.GetRect(1, 8, GUILayout.ExpandWidth(true));
            Fill(new Rect(end.x - 18, end.y + 4, end.width + 36, 1), PLine);
        }

        void PTag(string text, Color c)
        {
            var size = pSmall.CalcSize(new GUIContent(text));
            var r = GUILayoutUtility.GetRect(size.x + 14, 20, GUILayout.Width(size.x + 14), GUILayout.Height(20));
            UITheme.Box(r, UITheme.WithAlpha(c, 0.2f), 10f);
            var st = new GUIStyle(pSmall) { alignment = TextAnchor.MiddleCenter };
            st.normal.textColor = c;
            GUI.Label(r, text, st);
        }

        // ====================================================================== menü

        void PhoneMenu(Courier c, Rect scr)
        {
            var area = new Rect(scr.x + 16, scr.y + 48, scr.width - 32, scr.height - 70);
            GUILayout.BeginArea(area);
            GUILayout.BeginHorizontal();
            var back = GUILayoutUtility.GetRect(36, 36, GUILayout.Width(36));
            if (RoundIcon(back, IconBack)) GoPhone(PhoneScreen.Home);
            GUILayout.FlexibleSpace();
            var cl = GUILayoutUtility.GetRect(36, 36, GUILayout.Width(36));
            if (RoundIcon(cl, IconClose)) deferred = () => popupCourier = null;
            GUILayout.EndHorizontal();
            popupScroll = GUILayout.BeginScrollView(popupScroll, GUIStyle.none, GUIStyle.none);

            // Profil satırı
            var pr = GUILayoutUtility.GetRect(100, 62, GUILayout.ExpandWidth(true));
            var av = new Rect(pr.x, pr.y + 10, 42, 42);
            UITheme.Box(av, CompanyProfile.Secondary, 21f);
            var avs = new GUIStyle(pCenter) { fontSize = 18 };
            avs.normal.textColor = CompanyProfile.Primary;
            GUI.Label(av, c.courierName.Substring(0, 1), avs);
            GUI.Label(new Rect(av.xMax + 12, pr.y + 10, pr.width - 60, 22), c.courierName, pRowT);
            var stc = new GUIStyle(pDimS) { wordWrap = false };
            stc.normal.textColor = c.IsOnline ? PGreen : PDim;
            GUI.Label(new Rect(av.xMax + 12, pr.y + 32, pr.width - 60, 18), c.IsOnline ? "Çevrimiçi" : "Çevrimdışı", stc);
            if (GUI.Button(pr, GUIContent.none, GUIStyle.none)) GoPhone(PhoneScreen.Profile);
            Fill(new Rect(pr.x + 54, pr.yMax - 1, pr.width - 54, 1), PLine);

            MenuRow("T", "Teslimat ayarları", c.V.name + " · " + c.VehiclesText, PhoneScreen.Profile);
            MenuRow("A", "Analizler", $"Bu hafta {c.weekDeliveries} teslimat", PhoneScreen.Insights);
            MenuRow("F", "Finans", $"Hesabında {Ft(c.balance)}", PhoneScreen.Finance);
            MenuRow("İ", "İstatistikler", $"Bugün {c.deliveriesToday} teslimat", PhoneScreen.Stats);
            MenuRow("E", "Ekstra kazan", "Boostlar ve hedefler", PhoneScreen.Earn);
            MenuRow("S", "Sigorta ve haklar", c.insured ? "Sigortalı" : "Sigortası yok", PhoneScreen.Insurance);
            MenuRow("G", "Gelen kutusu", $"{c.inboxText.Count} mesaj", PhoneScreen.Inbox);
            MenuRow("D", "Destek", "Sık sorulanlar", PhoneScreen.Support);
            GUILayout.Space(6);
            GUILayout.Label($"Kurye uygulaması · {CompanyProfile.Name}", pDimS);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void MenuRow(string icon, string title, string sub, PhoneScreen target)
        {
            var r = GUILayoutUtility.GetRect(100, 54, GUILayout.ExpandWidth(true));
            var ic = new Rect(r.x + 4, r.y + 13, 28, 28);
            UITheme.Outline(ic, UITheme.WithAlpha(Color.white, 0.75f), 1.5f, 8f);
            GUI.Label(ic, icon, pCenter);
            GUI.Label(new Rect(r.x + 54, r.y + 8, r.width - 60, 22), title, pRowT);
            GUI.Label(new Rect(r.x + 54, r.y + 29, r.width - 60, 18), sub, new GUIStyle(pDimS) { wordWrap = false, clipping = TextClipping.Clip });
            Fill(new Rect(r.x + 54, r.yMax - 1, r.width - 54, 1), PLine);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) GoPhone(target);
        }

        // ====================================================================== alt sayfalar

        void PhoneSubPage(Courier c, Rect scr)
        {
            var area = new Rect(scr.x + 18, scr.y + 48, scr.width - 36, scr.height - 70);
            GUILayout.BeginArea(area);
            GUILayout.BeginHorizontal();
            var back = GUILayoutUtility.GetRect(36, 36, GUILayout.Width(36));
            if (RoundIcon(back, IconBack)) GoPhone(phoneScreen == PhoneScreen.PrevTask ? PhoneScreen.Home : PhoneScreen.Menu);
            GUILayout.FlexibleSpace();
            var cl = GUILayoutUtility.GetRect(36, 36, GUILayout.Width(36));
            if (RoundIcon(cl, IconClose)) deferred = () => popupCourier = null;
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            string title;
            switch (phoneScreen)
            {
                case PhoneScreen.Stats: title = "İstatistiklerin"; break;
                case PhoneScreen.Earn: title = "Ekstra kazan"; break;
                case PhoneScreen.Insights: title = "Analizler"; break;
                case PhoneScreen.Finance: title = "Finans"; break;
                case PhoneScreen.Insurance: title = "Sigorta ve haklar"; break;
                case PhoneScreen.Profile: title = "Profil"; break;
                case PhoneScreen.Inbox: title = "Gelen kutusu"; break;
                case PhoneScreen.PrevTask: title = "Önceki görev"; break;
                default: title = "Nasıl yardımcı olabiliriz?"; break;
            }
            GUILayout.Label(title, pHuge);
            GUILayout.Space(6);
            popupScroll = GUILayout.BeginScrollView(popupScroll, GUIStyle.none, GUIStyle.none);
            switch (phoneScreen)
            {
                case PhoneScreen.Stats: PageStats(c); break;
                case PhoneScreen.Earn: PageEarn(c); break;
                case PhoneScreen.Insights: PageInsights(c); break;
                case PhoneScreen.Finance: PageFinance(c); break;
                case PhoneScreen.Insurance: PageInsurance(c); break;
                case PhoneScreen.Profile: PageProfile(c); break;
                case PhoneScreen.Inbox: PageInbox(c); break;
                case PhoneScreen.PrevTask: PagePrevTask(c); break;
                default: PageSupport(c); break;
            }
            GUILayout.Space(10);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>Liste satırı: solda ad, sağda değer, altında ince çizgi.</summary>
        void PRow(string k, string v, Color? vc = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(k, pRowT);
            GUILayout.FlexibleSpace();
            var st = new GUIStyle(pRowT) { alignment = TextAnchor.MiddleRight };
            st.normal.textColor = vc ?? PDim;
            GUILayout.Label(v, st);
            GUILayout.EndHorizontal();
            var r = GUILayoutUtility.GetRect(10, 13, GUILayout.ExpandWidth(true));
            Fill(new Rect(r.x, r.y + 6, r.width, 1), PLine);
        }

        void PHeader(string t)
        {
            GUILayout.Space(8);
            GUILayout.Label(t, pTitle);
        }

        // ---------- İstatistikler ----------
        void PageStats(Courier c)
        {
            var r = GUILayoutUtility.GetRect(100, 36, GUILayout.ExpandWidth(true));
            UITheme.Box(r, PCard, 10f);
            string[] tabs = { "Bugün", "Hafta", "Ay" };
            float tw = r.width / 3f;
            for (int i = 0; i < 3; i++)
            {
                var tr = new Rect(r.x + i * tw + 3, r.y + 3, tw - 6, r.height - 6);
                if (statsTab == i) UITheme.Box(tr, new Color(0.36f, 0.38f, 0.46f), 8f);
                GUI.Label(tr, tabs[i], pCenter);
                int idx = i;
                if (GUI.Button(tr, GUIContent.none, GUIStyle.none)) deferred = () => statsTab = idx;
            }
            GUILayout.Space(10);
            var clock = GameClock.I;
            string when = statsTab == 0 ? clock.DateOf(clock.Day) : statsTab == 1 ? "Bu hafta (Pazartesi'den beri)" : clock.MonthName;
            GUILayout.Label($"<b>{when}</b>", pBody);
            GUILayout.Space(4);
            int del = statsTab == 0 ? c.deliveriesToday : statsTab == 1 ? c.weekDeliveries : c.monthDeliveries;
            float km = statsTab == 0 ? c.kmToday : statsTab == 1 ? c.kmWeek : c.kmMonth;
            long earn = statsTab == 0 ? c.earnedToday : statsTab == 1 ? c.earnedWeek : c.earnedMonth;
            PRow("Tamamlanan teslimat", del.ToString());
            PRow("Görevde ve dışında mesafe", $"{km:0.#} km");
            PRow("Kazanç (tahmini)", earn > 0 ? Ft(earn) : "—");
            if (statsTab == 0) PRow("Çevrimiçi süre", $"{c.onlineMinutesToday / 60f:0.0} saat");
            PRow("Toplam teslimat", c.deliveries.ToString());
        }

        // ---------- Ekstra kazan ----------
        void PageEarn(Courier c)
        {
            var camp = CampaignManager.I;
            var clock = GameClock.I;
            GUILayout.Label("<b>Aktif</b>", pBody);
            var active = camp.ActiveWindow;
            float wb = camp.WeatherBoostPercent;
            var r = GUILayoutUtility.GetRect(100, 76, GUILayout.ExpandWidth(true));
            bool any = active != null || wb > 0f;
            UITheme.Box(r, any ? new Color(0.07f, 0.26f, 0.33f) : PCard, 12f);
            UITheme.Box(new Rect(r.x, r.y, 70, r.height), any ? Color.Lerp(CompanyProfile.Primary, Color.white, 0.1f) : new Color(0.2f, 0.21f, 0.27f), 12f);
            var big = new GUIStyle(pCenter) { fontSize = 22 };
            GUI.Label(new Rect(r.x, r.y, 70, r.height), "%", big);
            var t1 = new GUIStyle(pDimS) { wordWrap = false };
            var t2 = new GUIStyle(pTitle) { fontSize = 20, wordWrap = false };
            if (active != null && active.percent >= wb)
            {
                GUI.Label(new Rect(r.x + 84, r.y + 8, r.width - 90, 18), "Şimdi · Boost", t1);
                GUI.Label(new Rect(r.x + 84, r.y + 26, r.width - 90, 26), $"+%{active.percent * 100f:0}", t2);
                GUI.Label(new Rect(r.x + 84, r.y + 52, r.width - 90, 18), active.TimeText, t1);
            }
            else if (wb > 0f)
            {
                GUI.Label(new Rect(r.x + 84, r.y + 8, r.width - 90, 18), $"Şimdi · {WeatherSystem.I.Name}", t1);
                GUI.Label(new Rect(r.x + 84, r.y + 26, r.width - 90, 26), $"+%{wb * 100f:0}", t2);
                GUI.Label(new Rect(r.x + 84, r.y + 52, r.width - 90, 18), "Kötü hava boostu", t1);
            }
            else GUI.Label(new Rect(r.x + 84, r.y, r.width - 90, r.height), "Şu an aktif boost yok", new GUIStyle(pBody) { alignment = TextAnchor.MiddleLeft });

            PHeader("Daha sonra");
            int shown = 0;
            foreach (var w in camp.TodayWindows())
            {
                if (w.startHour <= clock.Hour) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Bugün", pDimS, GUILayout.Width(56));
                GUILayout.BeginVertical();
                GUILayout.Label($"<b>+%{w.percent * 100f:0}</b>", new GUIStyle(pBody) { fontSize = 16 });
                GUILayout.Label(w.TimeText, pDimS);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                var lr = GUILayoutUtility.GetRect(10, 9, GUILayout.ExpandWidth(true));
                Fill(new Rect(lr.x, lr.y + 4, lr.width, 1), PLine);
                shown++;
            }
            if (shown == 0) GUILayout.Label("Bugün başka boost yok.", pDimS);
            GUILayout.Label("Yarının boostları gece yarısı açıklanır.", pDimS);

            if (camp.weeklyChallenge || camp.monthlyChallenge) PHeader("Hedefler");
            if (camp.weeklyChallenge) GoalBar($"Haftalık · {Ft(camp.weeklyReward)}", c.weekDeliveries, camp.weeklyTarget, c.weeklyRewardClaimed);
            if (camp.monthlyChallenge) GoalBar($"Aylık · {Ft(camp.monthlyReward)}", c.monthDeliveries, camp.monthlyTarget, c.monthlyRewardClaimed);
        }

        void GoalBar(string name, int have, int target, bool done)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, pBody);
            GUILayout.FlexibleSpace();
            GUILayout.Label(done ? "Tamamlandı" : $"{have}/{target}", done ? new GUIStyle(pDimS) { normal = { textColor = PGreen } } : pDimS);
            GUILayout.EndHorizontal();
            var r = GUILayoutUtility.GetRect(100, 12, GUILayout.ExpandWidth(true));
            UITheme.Box(new Rect(r.x, r.y + 2, r.width, 8), PCard, 4f);
            float v = done ? 1f : Mathf.Clamp01(have / (float)Mathf.Max(1, target));
            if (v > 0f) UITheme.Box(new Rect(r.x, r.y + 2, Mathf.Max(8f, r.width * v), 8), done ? PGreen : PAccent, 4f);
            GUILayout.Space(6);
        }

        // ---------- Analizler ----------
        void PageInsights(Courier c)
        {
            GUILayout.Label($"Merhaba {c.courierName.Split(' ')[0]}! Bu haftan böyle gidiyor. Müşterileri mutlu ettiğin için teşekkürler!", pBody);
            GUILayout.Label("Veriler anlık güncellenir", pDimS);
            PHeader("Tamamlanan teslimat");
            var r = GUILayoutUtility.GetRect(100, 90, GUILayout.ExpandWidth(true));
            UITheme.Box(r, PAccent, 14f);
            var num = new GUIStyle(pCenter) { fontSize = 34 };
            num.normal.textColor = new Color(0.03f, 0.04f, 0.06f);
            GUI.Label(new Rect(r.x, r.y + 8, r.width * 0.62f, 48), c.weekDeliveries.ToString(), num);
            var lbl = new GUIStyle(pCenter) { fontSize = 13 };
            lbl.normal.textColor = new Color(0.03f, 0.04f, 0.06f);
            GUI.Label(new Rect(r.x, r.y + 56, r.width * 0.62f, 22), "Tamamlanan teslimat", lbl);
            UITheme.Box(new Rect(r.xMax - 92, r.y + 14, 70, 64), new Color(1f, 1f, 1f, 0.55f), 10f);   // çanta
            UITheme.Box(new Rect(r.xMax - 80, r.y + 6, 46, 14), new Color(1f, 1f, 1f, 0.35f), 7f);
            int need = c.lastWeekDeliveries - c.weekDeliveries + 1;
            GUILayout.Space(4);
            GUILayout.Label(need > 0 ? $"Geçen haftayı geçmek için {need} teslimat daha!" : "Geçen haftanın teslimat sayısını geçtin!", pBody);

            PHeader("Kabul edilen görevler");
            float acc = c.offersSeenWeek > 0 ? c.offersAcceptedWeek / (float)c.offersSeenWeek : 0f;
            Gauge(acc, $"{c.offersAcceptedWeek}/{c.offersSeenWeek} teklif");
            if (c.offersSeenWeek > 0 && acc < 0.5f) GUILayout.Label("Dikkat: birçok kazanç fırsatını kaçırıyor gibi görünüyorsun!", new GUIStyle(pBody) { normal = { textColor = new Color(1f, 0.7f, 0.4f) } });

            PHeader("Görev tamamlama oranı");
            int started = c.monthDeliveries + c.pickupCancelsThisMonth;
            float comp = started > 0 ? c.monthDeliveries / (float)started : 1f;
            Gauge(comp, $"bu ay {c.pickupCancelsThisMonth}/{Courier.MaxPickupCancels} iptal");
        }

        /// <summary>Halka gösterge: ortada yüzde, altında ilerleme çizgisi.</summary>
        void Gauge(float v, string sub)
        {
            var r = GUILayoutUtility.GetRect(100, 130, GUILayout.ExpandWidth(true));
            var ring = new Rect(r.center.x - 55, r.y + 4, 110, 110);
            UITheme.Outline(ring, new Color(0.22f, 0.24f, 0.32f), 6f, 55f);
            // Yüzde kadar dolu yay yerine: halkanın üstüne doluluk noktaları
            int dots = 36, on = Mathf.RoundToInt(dots * Mathf.Clamp01(v));
            for (int i = 0; i < on; i++)
            {
                float a = (i / (float)dots) * Mathf.PI * 2f - Mathf.PI / 2f;
                var p = new Vector2(ring.center.x + Mathf.Cos(a) * 52f, ring.center.y + Mathf.Sin(a) * 52f);
                UITheme.Box(new Rect(p.x - 3.5f, p.y - 3.5f, 7, 7), PAccent, 3.5f);
            }
            var big = new GUIStyle(pCenter) { fontSize = 26 };
            GUI.Label(new Rect(ring.x, ring.y + 30, ring.width, 34), $"%{v * 100f:0}", big);
            GUI.Label(new Rect(r.x, ring.yMax - 2, r.width, 18), sub, new GUIStyle(pDimS) { alignment = TextAnchor.MiddleCenter, wordWrap = false });
        }

        // ---------- Finans ----------
        void PageFinance(Courier c)
        {
            GUILayout.Label(Ft(c.balance), pHuge);
            GUILayout.Label($"Hesabında · sıradaki kesim {EconomyManager.I.NextCutText}", pDimS);
            PHeader("Ödemeler");
            bool anyP = false;
            foreach (var p in EconomyManager.I.Payouts)
                if (p.courier == c) { PRow(p.early ? "Yatacak (erken)" : "Yatacak", $"{Ft(p.amount)} · {GameClock.I.DateOf(p.payDay)}"); anyP = true; }
            if (!anyP) PRow("Yatacak", "—");
            PRow("Bugüne kadar yatan", Ft(c.receivedTotal));
            PRow("Erken ödeme", c.earlyPayouts > 0 ? $"{c.earlyPayouts} kez · {Ft(c.earlyPayoutTotal)}" : "—");
            PHeader("Nakit");
            PRow("Üzerindeki nakit", $"{Ft(c.cashHeld)} / {Ft(Courier.CashLimit)}", c.cashHeld >= Courier.CashLimit * 0.7f ? PRed : (Color?)null);
            if (c.cashBlocked) GUILayout.Label("Hesabın kilitli: nakit borcunu kapatana kadar sipariş alamazsın.", new GUIStyle(pBody) { normal = { textColor = PRed } });
            PRow("Keep cash ile kapatılabilir", Ft(System.Math.Min(c.cashHeld, c.KeepCashCapacity)));
            PRow("Nakit teslimat", $"{c.cashOrders} paket");
            PRow("Müşterilerden alınan", Ft(c.cashTakenTotal));
            PRow("Keep cash ile mahsup", Ft(c.keepCashTotal));
            PRow("Offload ile ödenen", Ft(c.offloadTotal));
            PHeader("Toplam");
            PRow("Toplam kazanç", Ft(c.totalEarned));
            PRow("Bahşişler", Ft(c.tipsTotal));
            PRow("Araç birikimi", Ft(c.savings));
        }

        // ---------- Sigorta ----------
        void PageInsurance(Courier c)
        {
            var r = GUILayoutUtility.GetRect(100, 130, GUILayout.ExpandWidth(true));
            UITheme.Box(r, new Color(0.02f, 0.025f, 0.035f), 10f);
            var clip = new Rect(r.center.x - 60, r.y + 18, 76, 96);
            UITheme.Box(clip, new Color(0.95f, 0.5f, 0.2f), 8f);
            UITheme.Box(new Rect(clip.x + 6, clip.y + 6, clip.width - 12, clip.height - 12), Color.white, 5f);
            for (int i = 0; i < 4; i++) UITheme.Box(new Rect(clip.x + 14, clip.y + 44 + i * 10, 46, 4), new Color(0.85f, 0.86f, 0.9f), 2f);
            UITheme.Box(new Rect(clip.x + 14, clip.y + 16, 20, 20), new Color(0.3f, 0.7f, 0.95f), 10f);
            var shield = new Rect(r.center.x + 2, r.y + 50, 54, 62);
            UITheme.Box(shield, new Color(1f, 0.82f, 0.3f), 22f);
            UITheme.Box(new Rect(shield.center.x - 14, shield.center.y - 14, 28, 28), Color.white, 14f);
            GUI.Label(new Rect(shield.center.x - 14, shield.center.y - 14, 28, 28), c.insured ? "OK" : "-", new GUIStyle(pCenter) { fontSize = 16, normal = { textColor = new Color(0.3f, 0.7f, 0.95f) } });
            GUILayout.Space(6);
            GUILayout.Label(c.insured ? "Kaza sigortan aktif" : "Kaza sigortan yok", new GUIStyle(pTitle) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Label(c.insured
                ? (c.insuredViaCompany
                    ? $"Sigortan {CompanyProfile.Name}'ın anlaşmalı sigortacısı {EconomyManager.I.Plan.name} üzerinden. Primini sen ödüyorsun (haftalık {Ft(c.CurrentPremium)}). Kazada masrafları sigorta karşılar ve daha kısa sürede işe dönersin."
                    : $"Sigortanı dışarıdan kendin yaptırdın (haftalık {Ft(c.CurrentPremium)}). Kazada masrafları sigorta karşılar ve daha kısa sürede işe dönersin.")
                : (EconomyManager.I.HasInsurancePartner
                    ? $"Serbest kuryesin, sigorta zorunlu değil. İstersen {EconomyManager.I.Plan.name} ile haftalık {Ft(EconomyManager.I.Plan.premium)} karşılığında sigortalanabilirsin. Sigortasız kazada masrafları kendin ödersin."
                    : "Serbest kurye olarak sigortanı kendin yaptırırsın. Sigortasız kazada masrafları kendin ödersin ve daha uzun süre çalışamazsın."),
                new GUIStyle(pBody) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(8);
            PRow("Sigortacı", c.insured ? (c.insuredViaCompany ? EconomyManager.I.Plan.name : "Dışarıdan") : "—");
            PRow("Haftalık prim", Ft(c.insured ? c.CurrentPremium : (EconomyManager.I.HasInsurancePartner ? EconomyManager.I.Plan.premium : Courier.InsurancePremium)));
            PRow("Bugüne kadar ödenen", Ft(c.insurancePaid));
            PRow("Kask", c.Has(EquipmentKind.Kask) ? "Var" : "Yok", c.Has(EquipmentKind.Kask) ? (Color?)null : PRed);
        }

        // ---------- Profil ----------
        void PageProfile(Courier c)
        {
            PHeader("Kişisel bilgiler");
            ProfField("Ad soyad", c.courierName);
            ProfField("Kurye ID", c.courierId > 0 ? c.courierId.ToString() : "—");
            ProfField("Meslek", c.OccupationName);
            ProfField("Tecrübe", $"Seviye {c.level} · {c.deliveries} teslimat");

            PHeader("Araçlar");
            ProfField("Kullandığı", c.V.name);
            ProfField("Sahip olduğu", c.VehiclesText);
            if (c.dreamVehicle != c.vehicle) ProfField("Hayalindeki araç", $"{GameData.Vehicle(c.dreamVehicle).name} · birikim {Ft(c.savings)}");

            PHeader("Ekipman");
            var clock = GameClock.I;
            if (c.depositPaidTotal > 0 || c.depositOwed > 0)
                ProfField("Depozito", $"{Ft(c.depositPaidTotal)} kesildi" + (c.depositOwed > 0 ? $", {Ft(c.depositOwed)} kesilecek" : "") +
                                      (c.depositDay > 0 ? $" (ilk kesinti {clock.DateOf(c.depositDay)})" : ""));
            foreach (var k in c.equipment)
            {
                string n = GameData.Equip(k).name;
                int i = c.gearNames.IndexOf(n);
                ProfField(n, i >= 0 ? $"{clock.DateOf(c.gearDays[i])} tarihinde teslim alındı" : "Teslim alındı");
            }
            if (c.equipment.Count == 0) GUILayout.Label("Ekipmanı yok.", pDimS);
        }

        void ProfField(string k, string v)
        {
            GUILayout.Label(v, pRowT);
            GUILayout.Label(k, pDimS);
            GUILayout.Space(6);
        }

        // ---------- Gelen kutusu ----------
        void PageInbox(Courier c)
        {
            if (c.inboxText.Count == 0) { GUILayout.Label("Henüz mesaj yok.", pDimS); return; }
            for (int i = 0; i < c.inboxText.Count; i++)
            {
                GUILayout.Label($"<b>{c.inboxText[i]}</b>", pBody);
                GUILayout.Label(i < c.inboxWhen.Count ? c.inboxWhen[i] : "", pDimS);
                var r = GUILayoutUtility.GetRect(10, 13, GUILayout.ExpandWidth(true));
                Fill(new Rect(r.x - 18, r.y + 6, r.width + 36, 1), PLine);
            }
        }

        // ---------- Destek ----------
        void PageSupport(Courier c)
        {
            GUILayout.Label("Merhaba! Konunu seç.", pDimS);
            PHeader("Konular");
            string why;
            if (c.cashBlocked) why = "Hesabın nakit borcu yüzünden kilitli. Keep cash ya da offload ile kapatınca tekrar sipariş alırsın.";
            else if (!c.IsOnline) why = $"Şu an çevrimdışısın ({c.StateName}).";
            else if (c.RejectChance > 0.2f) why = $"Moralin düşük ya da yorgunsun; tekliflerin yaklaşık %{c.RejectChance * 100f:0}'ini reddediyorsun.";
            else if (DemandLevel() == 0) why = "Şu an sipariş yoğunluğu düşük. Boost saatlerinde ve öğle/akşam yoğunluğunda daha çok teklif gelir.";
            else why = "Yakındaki kuryeler teklifleri senden önce alıyor olabilir. Dükkanlara yakın beklemek işe yarar.";
            SupportTopic(0, "Bir süredir sipariş gelmiyor, neden?", why);
            SupportTopic(1, "Nakit siparişler", $"Müşteriden aldığın nakit şirkete borcundur. Üzerinde en fazla {Ft(Courier.CashLimit)} olabilir. Ay sonuna kadar keep cash (hesabının en fazla %80'iyle mahsup) ya da offload (kartla ödeme) ile kapatmalısın, yoksa hesabın kilitlenir.");
            SupportTopic(2, "Ödemeler", "Kazancın hesabında birikir. Ayın 15'i ve son günü kesilir, 1–2 iş günü içinde yatar. İstersen hesabının %80'ini erken çekebilirsin (2–3 iş günü).");
            SupportTopic(3, "Ekipman", "İhtiyacın olan ekipmanı (termal çanta, mont, yağmurluk, kask…) ofisten depozitoyla alırsın; bedeli kazancından kesilir. Ayrılırken ekipmanı iade edersen depozitonun tamamı geri ödenir, etmezsen şirkette kalır.");
            SupportTopic(4, "Sözleşme ve çalışma", "Serbest kuryesin: ne zaman çevrimiçi olacağına sen karar verirsin. Aracını ve sigortanı kendin karşılarsın. Teklifleri reddedebilirsin; dükkanda sıra varsa ayda en fazla 10 kez teslim almayı iptal edebilirsin.");
            SupportTopic(5, "Kat ve teslimat", "Adrese varınca binaya girip kata çıkarsın. Asansörlü binada bekleme + kat başı kısa süre, asansörsüz binada her kat merdivenle; ağır paketlerde daha uzun sürer ve yorar.");
        }

        void SupportTopic(int id, string title, string body)
        {
            var r = GUILayoutUtility.GetRect(100, 50, GUILayout.ExpandWidth(true));
            GUI.Label(new Rect(r.x, r.y + 14, r.width - 30, 24), title, pRowT);
            GUI.Label(new Rect(r.xMax - 20, r.y + 12, 20, 24), supportOpen == id ? "v" : ">", new GUIStyle(pRowT) { normal = { textColor = PDim } });
            Fill(new Rect(r.x, r.yMax - 1, r.width, 1), PLine);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { int v = supportOpen == id ? -1 : id; deferred = () => supportOpen = v; }
            if (supportOpen == id)
            {
                GUILayout.Space(4);
                GUILayout.Label(body, pBody);
                GUILayout.Space(8);
            }
        }
    }
}
