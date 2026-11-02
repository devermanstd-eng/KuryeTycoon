using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Prototip arayüzü (Unity IMGUI). Kurulum ekranı, üst bar, sol panel sekmeleri,
    /// haber akışı ve olay kartları. Hiçbir Canvas / prefab gerektirmez.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        enum Tab { Siparisler, Kuryeler, IseAlim, Dukkanlar, Ekipman, Kampanyalar, Finans, Ayarlar }
        static readonly string[] BaseTabNames = { "Siparişler", "Kuryeler", "İşe Alım", "Dükkanlar", "Ekipman", "Kampanyalar", "Finans", "Ayarlar" };

        public float panelWidth = 440f;
        const float TopBar = 48f;

        static readonly Color[] Presets =
        {
            new Color(0.00f, 0.62f, 0.87f), new Color(0.89f, 0.20f, 0.20f), new Color(1.00f, 0.50f, 0.10f),
            new Color(1.00f, 0.80f, 0.10f), new Color(0.20f, 0.72f, 0.35f), new Color(0.10f, 0.60f, 0.60f),
            new Color(0.55f, 0.30f, 0.85f), new Color(0.95f, 0.35f, 0.60f), new Color(0.10f, 0.10f, 0.12f),
            new Color(0.97f, 0.97f, 0.97f)
        };

        static readonly Color SelectedTint = new Color(0.35f, 0.85f, 1f);

        Tab tab;
        Vector2 scroll;
        float uiScale = 1f;
        bool modalFree = true;
        System.Action deferred; // listeleri değiştiren işlemler bir sonraki Update'te yapılır

        // Kurulum ekranı
        string companyName = "Hızlı Kurye";
        Color primary, secondary;
        int editingColor;

        Courier selectedCourier;

        GUIStyle title, header, label, small, warn, note, box, button, smallButton, bigButton, toggle, textField,
                 worldLabel, barTitle, barText, logoLetter;
        bool stylesReady;

        static string Ft(long v) => GameData.Ft(v);

        void Start()
        {
            primary = CompanyProfile.Primary;
            secondary = CompanyProfile.Secondary;
        }

        void Update()
        {
            if (deferred != null)
            {
                var a = deferred;
                deferred = null;
                a();
            }
        }

        void LateUpdate() { FitCamera(); }

        void FitCamera()
        {
            var cam = Camera.main;
            if (cam == null || GameBootstrap.I == null) return;
            if (!GameBootstrap.I.Started)
            {
                cam.rect = new Rect(0, 0, 1, 1);
                return;
            }
            float left = Mathf.Clamp01(panelWidth * uiScale / Screen.width);
            float top = Mathf.Clamp01(TopBar * uiScale / Screen.height);
            cam.rect = new Rect(left, 0f, 1f - left, 1f - top);
            float w = CityMap.Width + 1f, h = CityMap.Height + 1f;
            cam.orthographicSize = Mathf.Max(h / 2f, w / 2f / Mathf.Max(0.1f, cam.aspect));
            cam.transform.position = new Vector3((CityMap.Width - 1) / 2f, (CityMap.Height - 1) / 2f, -10f);
        }

        // =====================================================================
        void OnGUI()
        {
            uiScale = Mathf.Clamp(Screen.height / 900f, 0.8f, 2f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));
            float sw = Screen.width / uiScale;
            float sh = Screen.height / uiScale;
            InitStyles();

            if (GameBootstrap.I == null) return;
            if (!GameBootstrap.I.Started)
            {
                DrawSetup(sw, sh);
                return;
            }

            modalFree = EventManager.I.Current == null;
            if (modalFree) HandleKeys();

            GUI.enabled = modalFree;
            DrawWorldLabels();
            DrawTopBar(sw);
            DrawPanel(sh);
            DrawFeed(sw, sh);
            GUI.enabled = true;
            DrawEvent(sw, sh);
        }

        // ---------------------------------------------------------------- kurulum
        void DrawSetup(float sw, float sh)
        {
            Fill(new Rect(0, 0, sw, sh), new Color(0.08f, 0.09f, 0.11f));
            float w = 620f, h = 680f;
            var r = new Rect((sw - w) / 2f, Mathf.Max(10f, (sh - h) / 2f), w, h);
            Fill(r, new Color(0.14f, 0.15f, 0.18f));

            GUILayout.BeginArea(new Rect(r.x + 24, r.y + 20, w - 48, h - 40));
            GUILayout.Label("Kurye Şirketini Kur", title);
            GUILayout.Label("Küçük bir sermaye ve 2 kuryeyle başlıyorsun. Önce şirketine bir isim ve renk ver.", label);
            GUILayout.Space(12);

            GUILayout.Label("Şirket adı", header);
            companyName = GUILayout.TextField(companyName, 24, textField);
            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            if (SelectButton(editingColor == 0, "Ana renk", button, GUILayout.Height(28))) editingColor = 0;
            if (SelectButton(editingColor == 1, "İkincil renk", button, GUILayout.Height(28))) editingColor = 1;
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            Color c = editingColor == 0 ? primary : secondary;
            GUILayout.BeginHorizontal();
            foreach (var p in Presets)
            {
                Rect sr = GUILayoutUtility.GetRect(42, 42, GUILayout.Width(42), GUILayout.Height(42));
                bool selected = Mathf.Abs(p.r - c.r) + Mathf.Abs(p.g - c.g) + Mathf.Abs(p.b - c.b) < 0.02f;
                if (selected) Fill(sr, Color.white);
                Fill(new Rect(sr.x + 3, sr.y + 3, sr.width - 6, sr.height - 6), p);
                if (GUI.Button(sr, GUIContent.none, GUIStyle.none)) c = p;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            c.r = SliderRow("Kırmızı", c.r, 0f, 1f, Mathf.RoundToInt(c.r * 255f).ToString(), 80);
            c.g = SliderRow("Yeşil", c.g, 0f, 1f, Mathf.RoundToInt(c.g * 255f).ToString(), 80);
            c.b = SliderRow("Mavi", c.b, 0f, 1f, Mathf.RoundToInt(c.b * 255f).ToString(), 80);
            c.a = 1f;
            if (editingColor == 0) primary = c; else secondary = c;

            GUILayout.Space(12);
            GUILayout.Label("Önizleme", header);
            Rect pr = GUILayoutUtility.GetRect(100, 130, GUILayout.ExpandWidth(true));
            Fill(pr, primary);
            var logo = new Rect(pr.x + 18, pr.y + 18, 94, 94);
            Fill(logo, secondary);
            Fill(new Rect(logo.x + 7, logo.y + 7, logo.width - 14, logo.height - 14), primary);
            logoLetter.normal.textColor = secondary;
            string initial = string.IsNullOrEmpty(companyName.Trim()) ? "K" : companyName.Trim().Substring(0, 1).ToUpper();
            GUI.Label(logo, initial, logoLetter);
            barTitle.normal.textColor = secondary;
            GUI.Label(new Rect(pr.x + 130, pr.y + 30, pr.width - 260, 40), companyName, barTitle);
            barText.normal.textColor = secondary;
            GUI.Label(new Rect(pr.x + 130, pr.y + 66, pr.width - 260, 30), "Kurye montu, çanta ve araçlar bu renklerde olacak", barText);
            var courier = new Rect(pr.xMax - 100, pr.y + 35, 60, 60);
            Fill(courier, secondary);
            Fill(new Rect(courier.x + 10, courier.y + 10, 40, 40), primary);

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Şirketi Kur ve Başla", bigButton, GUILayout.Height(50)))
            {
                CompanyProfile.Name = string.IsNullOrEmpty(companyName.Trim()) ? "Hızlı Kurye" : companyName.Trim();
                CompanyProfile.Primary = primary;
                CompanyProfile.Secondary = secondary;
                deferred = () => GameBootstrap.I.StartGame();
            }
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- klavye
        void HandleKeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;
            var clk = GameClock.I;
            switch (e.keyCode)
            {
                case KeyCode.Space: clk.Paused = !clk.Paused; e.Use(); break;
                case KeyCode.Alpha1: SetSpeed(1f); e.Use(); break;
                case KeyCode.Alpha2: SetSpeed(2f); e.Use(); break;
                case KeyCode.Alpha3: SetSpeed(4f); e.Use(); break;
                case KeyCode.Alpha4: SetSpeed(8f); e.Use(); break;
                case KeyCode.Alpha5: SetSpeed(16f); e.Use(); break;
            }
        }

        static void SetSpeed(float s)
        {
            GameClock.I.SpeedMultiplier = s;
            GameClock.I.Paused = false;
        }

        // ---------------------------------------------------------------- harita etiketleri
        void DrawWorldLabels()
        {
            var cam = Camera.main;
            if (cam == null || CityMap.I == null) return;

            foreach (var s in CityMap.I.Shops)
            {
                if (!CityMap.I.IsUnlocked(s.tile.x)) continue;
                WorldLabel(cam, CityMap.ToWorld(s.tile) + Vector3.up * 0.7f,
                    s.contracted ? s.Name : s.Name + " (sözleşmesiz)",
                    s.contracted ? Color.white : new Color(0.7f, 0.7f, 0.7f));
            }

            WorldLabel(cam, CityMap.ToWorld(CityMap.I.HQTile), CompanyProfile.Initial, CompanyProfile.Secondary);
            WorldLabel(cam, CityMap.ToWorld(CityMap.I.HQTile) + Vector3.down * 0.7f, "Merkez", Color.white);

            // Aynı karedeki kuryelerin isimleri üst üste binmesin
            var stack = new Dictionary<Vector2Int, int>();
            foreach (var c in CourierManager.I.Couriers)
            {
                var key = Vector2Int.RoundToInt(c.Position);
                stack.TryGetValue(key, out int n);
                stack[key] = n + 1;
                WorldLabel(cam, CityMap.ToWorld(c.Position) + Vector3.up * (0.55f + 0.4f * n),
                    $"{c.ShortName} ({c.V.letter})", Color.white);
            }
        }

        void WorldLabel(Camera cam, Vector3 world, string text, Color color)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) return;
            float x = sp.x / uiScale;
            float y = (Screen.height - sp.y) / uiScale;
            var r = new Rect(x - 90f, y - 10f, 180f, 20f);
            worldLabel.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, worldLabel);
            worldLabel.normal.textColor = color;
            GUI.Label(r, text, worldLabel);
        }

        // ---------------------------------------------------------------- üst bar
        void DrawTopBar(float sw)
        {
            var clock = GameClock.I;
            var eco = EconomyManager.I;
            var weather = WeatherSystem.I;

            Fill(new Rect(0, 0, sw, TopBar), CompanyProfile.Primary);
            barTitle.normal.textColor = CompanyProfile.Secondary;
            barText.normal.textColor = CompanyProfile.Secondary;

            GUI.Label(new Rect(14, 0, 260, TopBar), CompanyProfile.Name, barTitle);

            string info = $"Kasa: {Ft(eco.money)}    Vergi kasası: {Ft(eco.taxVault)}    " +
                          $"İtibar: {CompanyProfile.Reputation:0.0}/5    Çevrimiçi: {CourierManager.I.OnlineCount}/{CourierManager.I.Couriers.Count}    {clock.TimeText}    " +
                          $"{clock.SeasonName} · {weather.Name}";
            var boost = CampaignManager.I.ActiveWindow;
            if (boost != null) info += $"    BOOST %{boost.percent * 100f:0} ({boost.TimeText})";
            if (eco.taxDebt > 0) info += $"    VERGİ BORCU: {Ft(eco.taxDebt)}";
            GUI.Label(new Rect(280, 0, sw - 280 - 290, TopBar), info, barText);

            float bx = sw - 282f;
            if (SelectButtonAt(new Rect(bx, 9, 42, 30), clock.Paused, "II")) clock.Paused = !clock.Paused;
            float[] speeds = { 1f, 2f, 4f, 8f, 16f };
            for (int i = 0; i < speeds.Length; i++)
            {
                float s = speeds[i];
                if (SelectButtonAt(new Rect(bx + 46 + i * 46, 9, 42, 30), IsSpeed(clock, s), s + "x")) SetSpeed(s);
            }
        }

        static bool IsSpeed(GameClock c, float s) => !c.Paused && Mathf.Approximately(c.SpeedMultiplier, s);

        // ---------------------------------------------------------------- sol panel
        void DrawPanel(float sh)
        {
            var r = new Rect(0, TopBar, panelWidth, sh - TopBar);
            Fill(r, new Color(0.12f, 0.13f, 0.16f, 0.98f));

            var names = (string[])BaseTabNames.Clone();
            int apps = CourierManager.I.Applications.Count;
            if (apps > 0) names[(int)Tab.IseAlim] = $"İşe Alım ({apps})";

            GUILayout.BeginArea(new Rect(r.x + 8, r.y + 8, r.width - 16, r.height - 16));
            int newTab = GUILayout.SelectionGrid((int)tab, names, 4, smallButton, GUILayout.Height(48));
            if (newTab != (int)tab) { var t = (Tab)newTab; deferred = () => { tab = t; scroll = Vector2.zero; }; }
            GUILayout.Space(6);
            scroll = GUILayout.BeginScrollView(scroll);

            switch (tab)
            {
                case Tab.Siparisler: DrawOrders(); break;
                case Tab.Kuryeler: DrawCouriers(); break;
                case Tab.IseAlim: DrawHiring(); break;
                case Tab.Dukkanlar: DrawShops(); break;
                case Tab.Ekipman: DrawEquipment(); break;
                case Tab.Kampanyalar: DrawCampaigns(); break;
                case Tab.Finans: DrawFinance(); break;
                case Tab.Ayarlar: DrawSettings(); break;
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawOrders()
        {
            var om = OrderManager.I;
            var eco = EconomyManager.I;
            float now = GameClock.I.TotalMinutes;

            om.autoAssign = GUILayout.Toggle(om.autoAssign, " Otomatik atama (dispatcher)", toggle);
            om.prioritizeProfit = GUILayout.Toggle(om.prioritizeProfit, " Atamada kârı öncele (kapalıysa en hızlı kurye)", toggle);
            GUILayout.Label($"Bugün: vazgeçen müşteri (ücret pahalı) {om.lostToday} · iptal (kurye yok) {om.cancelledToday} · " +
                            $"kuryelerin reddettiği {om.rejectedToday} · taşıyacak kurye olmadığı için alınamayan {om.heavyLostToday}", small);
            GUILayout.Label($"Ağırlık: 20 kg'a kadar bisiklet, scooter, motor ve araba; yürüyerek 3 kg'a kadar; {GameData.HeavyKg:0} kg ve üstü sadece araba.", small);

            GUILayout.Space(6);
            GUILayout.Label($"Bekleyen siparişler ({om.Pending.Count})", header);
            int shown = 0;
            foreach (var o in om.Pending)
            {
                if (shown++ >= 12) break;
                GUILayout.BeginVertical(box);
                GUILayout.Label($"#{o.id} {o.shop.Name} · {GameData.KmText(o.distance)} · {GameData.KgText(o.weightKg)} · {o.CustomerTypeName}", label);
                if (o.IsHeavy) GUILayout.Label("AĞIR SİPARİŞ — sadece arabalı kuryeler alabilir", warn);
                GUILayout.Label($"Sepet {Ft(o.basket)} · Komisyon {Ft(o.commissionIncome)} + Teslimat ücreti {Ft(o.deliveryFee)} " +
                                $"= Gelir {Ft(o.CompanyIncome)} · İptale {Mathf.Max(0f, o.createdAt + om.pendingTimeout - now):0} dk", small);

                GUILayout.BeginHorizontal();
                int count = 0;
                foreach (var c in CourierManager.I.Couriers)
                {
                    if (!c.CanTakeMore || !c.CanCarry(o)) continue;
                    bool rejected = o.rejectedBy.Contains(c);
                    int p = eco.ProfitIf(o, c);
                    GUI.enabled = modalFree && p >= 0 && !rejected;
                    var order = o;
                    var courier = c;
                    int boostCost = CampaignManager.I.BoostAmount(eco.CourierPay(c, o.distance));
                    string boostTxt = boostCost > 0 ? $" (boost −{Ft(boostCost)})" : "";
                    string txt = rejected ? $"{c.ShortName}: reddetti"
                               : p >= 0 ? $"{c.ShortName} ({c.V.letter}) kâr +{Ft(p)}{boostTxt}"
                               : $"{c.ShortName}: zarar {Ft(p)}";
                    if (GUILayout.Button(txt, smallButton)) deferred = () => om.Assign(order, courier);
                    GUI.enabled = modalFree;
                    if (++count % 2 == 0)
                    {
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                    }
                }
                if (count == 0) GUILayout.Label(o.IsHeavy ? "Müsait arabalı kurye yok." : "Bu ağırlığı taşıyabilecek müsait kurye yok.", small);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            GUILayout.Space(6);
            GUILayout.Label($"Yolda ({om.Active.Count})", header);
            foreach (var c in CourierManager.I.Couriers)
            {
                if (c.OrderCount == 0) continue;
                string ids = c.CurrentOrder != null ? "#" + c.CurrentOrder.id : "";
                foreach (var q in c.QueuedOrders) ids += " #" + q.id;
                GUILayout.Label($"{c.ShortName} ({c.V.letter}) {c.OrderCount}/{c.EffectiveCapacity} paket: {c.StateName} — {ids}", small);
            }

            GUILayout.Space(6);
            GUILayout.Label("Son teslimatlar", header);
            for (int i = 0; i < Mathf.Min(8, om.History.Count); i++)
            {
                var o = om.History[i];
                string tip = o.tip > 0 ? $" · bahşiş {Ft(o.tip)}" : "";
                GUILayout.Label($"#{o.id} {o.shop.Name} → {o.courier.ShortName} · {GameData.KmText(o.distance)} · " +
                                $"{o.rating:0.0} yıldız · kurye {Ft(o.courierPay)} · kâr {Ft(o.Profit)}{tip}", small);
            }
        }

        void DrawCouriers()
        {
            var cm = CourierManager.I;
            GUILayout.Label($"Kuryeler ({cm.Couriers.Count}) · Şu an çevrimiçi: {cm.OnlineCount}", header);
            GUILayout.Label("Kuryeler serbest çalışır: kendi düzenlerine, yorgunluklarına ve günlük hayatlarına göre istedikleri zaman " +
                            "çevrimiçi/çevrimdışı olurlar, bazı günler hiç çalışmazlar. Onları zorlayamazsın; boostlar ve iyi moral " +
                            "çevrimiçi olma isteğini artırır. Moral yüksekse aynı anda daha çok paket alırlar, düşükse sipariş reddedebilirler.", small);

            foreach (var c in cm.Couriers)
            {
                GUILayout.BeginVertical(box);
                GUILayout.Label($"{c.courierName} — {c.V.name} · Seviye {c.level}", header);
                GUILayout.Label($"{c.StateName} · Teslimat {c.deliveries} (bugün {c.deliveriesToday}) · Kazancı {Ft(c.totalEarned)}", small);
                GUILayout.Label($"Çalışma düzeni: {c.RoutineName}" + (c.dayOff ? " · Bugün izin günü" : ""), small);
                Bar("Moral", c.morale, Color.Lerp(new Color(0.9f, 0.3f, 0.3f), new Color(0.3f, 0.85f, 0.4f), c.morale));
                Bar("Yorgunluk", c.fatigue, new Color(1f, 0.6f, 0.2f));

                string capNote = "";
                if (c.morale >= 0.8f) capNote += ", moral yüksek +1";
                if (c.morale < 0.35f) capNote += ", moral düşük −1";
                if (c.fatigue >= 0.6f) capNote += ", yorgun −1";
                GUILayout.Label($"Aynı anda taşıyabileceği: {c.EffectiveCapacity} paket (araç {c.V.capacity}{capNote})", label);
                if (c.RejectChance > 0f)
                    GUILayout.Label($"Siparişlerin yaklaşık %{c.RejectChance * 100f:0}'ini reddedebilir.", warn);

                GUILayout.Label($"Hız {c.speedSkill:0.00} · Dikkat {c.care:0.00} · Nezaket {c.politeness:0.00} · Dayanıklılık {c.stamina:0.00}", small);
                GUILayout.Label($"Kazanç: {Ft(c.V.basePay + c.extraPay)}/paket + {c.V.payPerKm} Ft/km + bonus · Taşır: {GameData.CarryText(c.V)}", small);
                var camp = CampaignManager.I;
                if (camp.weeklyChallenge)
                {
                    if (c.weeklyRewardClaimed)
                        GUILayout.Label($"Haftalık hedef tamamlandı! ({Ft(camp.weeklyReward)} ödendi)", label);
                    else
                        Bar("Haftalık", c.weekDeliveries / (float)Mathf.Max(1, camp.weeklyTarget), new Color(0.4f, 0.7f, 1f),
                            $"{c.weekDeliveries}/{camp.weeklyTarget}");
                }
                if (camp.monthlyChallenge)
                {
                    if (c.monthlyRewardClaimed)
                        GUILayout.Label($"Aylık hedef tamamlandı! ({Ft(camp.monthlyReward)} ödendi)", label);
                    else
                        Bar("Aylık", c.monthDeliveries / (float)Mathf.Max(1, camp.monthlyTarget), new Color(0.75f, 0.55f, 1f),
                            $"{c.monthDeliveries}/{camp.monthlyTarget}");
                }
                GUILayout.Label($"Ekipman: {EquipmentText(c)}", small);
                foreach (var p in c.ActivePenalties()) GUILayout.Label("• " + p, warn);

                GUILayout.BeginHorizontal();
                var courier = c;
                if (GUILayout.Button("Ekipman al", smallButton)) deferred = () => { selectedCourier = courier; tab = Tab.Ekipman; scroll = Vector2.zero; };
                GUI.enabled = modalFree && c.OrderCount == 0;
                if (GUILayout.Button(c.OrderCount == 0 ? "İşten çıkar" : "İşten çıkar (paketi var)", smallButton)) deferred = () => cm.Fire(courier);
                GUI.enabled = modalFree;
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
        }

        static string EquipmentText(Courier c)
        {
            if (c.equipment.Count == 0) return "yok";
            string s = "";
            foreach (var k in c.equipment) s += (s.Length > 0 ? ", " : "") + GameData.Equip(k).name;
            return s;
        }

        void DrawHiring()
        {
            var cm = CourierManager.I;
            float now = GameClock.I.TotalMinutes;
            GUILayout.Label($"Gelen başvurular ({cm.Applications.Count}/{cm.maxApplications})", header);
            GUILayout.Label("Başvurular zamanla gelir; itibarın ve bonus çarpanın yüksekse daha sık gelir. Her başvuru 1 gün geçerlidir. " +
                            "Kuryeler paket ve km başı kazandığı için işe alım ve çıkarma ücretsizdir. " +
                            "Ama çok kurye = kişi başı az paket = düşük moral.", small);

            if (cm.Applications.Count == 0)
                GUILayout.Label("Şu an bekleyen başvuru yok. Yeni başvurular haber akışında duyurulur.", label);

            foreach (var cand in cm.Applications)
            {
                var v = GameData.Vehicle(cand.vehicle);
                GUILayout.BeginVertical(box);
                GUILayout.Label($"{cand.name} — {v.name} · Seviye {cand.level}", header);
                GUILayout.Label($"\"{cand.note}\"", note);
                GUILayout.Label($"Çalışma düzeni: {Courier.RoutineLabel(cand.routine)}", small);
                GUILayout.Label($"Hız {cand.speedSkill:0.00} · Dikkat {cand.care:0.00} · Nezaket {cand.politeness:0.00} · Dayanıklılık {cand.stamina:0.00}", small);
                GUILayout.Label($"Araç: {v.tilesPerMinute * 60f * GameData.KmPerTile:0} km/saat · {v.capacity} paket · " +
                                $"Kazanç {Ft(v.basePay)}/paket + {v.payPerKm} Ft/km · Taşır: {GameData.CarryText(v)}", small);
                if (cand.askExtraPay > 0)
                    GUILayout.Label($"Deneyimli: paket başı +{Ft(cand.askExtraPay)} ek ücret istiyor.", warn);
                GUILayout.Label($"Başvuru {Mathf.Max(0f, (cand.expiresAt - now) / 60f):0} saat sonra geçersiz olacak.", small);

                GUILayout.BeginHorizontal();
                var c = cand;
                if (GUILayout.Button("İşe al", button)) deferred = () => cm.Hire(c);
                if (GUILayout.Button("Reddet", button)) deferred = () => cm.Reject(c);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
        }

        void DrawShops()
        {
            var map = CityMap.I;
            GUILayout.Label($"Açık bölge: {map.UnlockLevel}/{map.MaxLevel} — {CityMap.DistrictNames[map.UnlockLevel]}", header);
            if (map.UnlockLevel < map.MaxLevel)
            {
                int next = map.UnlockLevel + 1;
                if (GUILayout.Button($"Yeni bölge aç: {CityMap.DistrictNames[next]} — {Ft(CityMap.UnlockCost[next])}", button))
                    deferred = () => map.TryUnlockNext();
            }
            GUILayout.Space(6);

            foreach (var s in map.Shops)
            {
                GUILayout.BeginVertical(box);
                GUILayout.Label(s.Name + (s.contracted ? "  [Sözleşmeli]" : ""), header);
                GUILayout.Label($"Komisyon %{s.Commission * 100f:0.#} · Hazırlık ~{s.def.prepMinutes:0} dk · " +
                                $"Sepet {Ft(s.def.basketMin)}–{Ft(s.def.basketMax)} · Ağırlık {GameData.KgText(s.def.kgMin)}–{GameData.KgText(s.def.kgMax)} · Toplam sipariş {s.ordersTotal}", small);
                if (s.def.kgMax >= GameData.HeavyKg)
                    GUILayout.Label($"{GameData.HeavyKg:0} kg üstü ağır siparişler gelir: bunlar için arabalı kurye gerekir.", warn);
                if (!map.IsUnlocked(s.tile.x))
                {
                    GUILayout.Label($"Kilitli: önce \"{CityMap.DistrictNames[s.def.unlockLevel]}\" bölgesini aç.", small);
                }
                else if (!s.contracted)
                {
                    var shop = s;
                    if (GUILayout.Button($"Sözleşme imzala — {Ft(s.def.contractCost)}", button))
                        deferred = () => map.SignContract(shop);
                }
                GUILayout.EndVertical();
            }
        }

        void DrawEquipment()
        {
            var cm = CourierManager.I;
            switch (GameClock.I.Season)
            {
                case Season.Kis: GUILayout.Label("KIŞ: Montsuz kuryeler yavaşlar, hastalanır ve morali her gün düşer.", header); break;
                case Season.Yaz: GUILayout.Label("YAZ: Tişörtsüz kuryeler sıcakta yavaşlar, hızlı yorulur, morali düşer.", header); break;
                case Season.Sonbahar: GUILayout.Label("SONBAHAR: Yağmur sezonu ve kış yaklaşıyor. Yağmurluk ve montları şimdiden al.", header); break;
                default: GUILayout.Label("İLKBAHAR: Yağmur sezonu, yağmurluksuz kuryeler ıslanır ve hastalanabilir.", header); break;
            }
            GUILayout.Label("Ekipman kısa vadede kârı düşürür ama gider olarak vergiden düşülür; hız, moral, puan ve " +
                            "daha az hastalık/kaza olarak geri döner. Kuryeler sekmesinde her kuryenin eksikleri yazar.", small);

            if (selectedCourier == null || !cm.Couriers.Contains(selectedCourier))
                selectedCourier = cm.Couriers.Count > 0 ? cm.Couriers[0] : null;

            GUILayout.Space(4);
            GUILayout.Label("Kurye seç:", label);
            GUILayout.BeginHorizontal();
            int n = 0;
            foreach (var c in cm.Couriers)
            {
                var courier = c;
                if (SelectButton(c == selectedCourier, c.ShortName, smallButton)) deferred = () => selectedCourier = courier;
                if (++n % 4 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }
            GUILayout.EndHorizontal();

            if (selectedCourier != null)
            {
                var pens = selectedCourier.ActivePenalties();
                if (pens.Count == 0) GUILayout.Label($"{selectedCourier.ShortName}: eksik yok.", label);
                foreach (var p in pens) GUILayout.Label("• " + p, warn);
            }

            foreach (var e in GameData.Equipment)
            {
                GUILayout.BeginVertical(box);
                GUILayout.Label($"{e.name} — {Ft(e.price)}", header);
                GUILayout.Label(e.effect, small);
                GUILayout.BeginHorizontal();
                var kind = e.kind;
                if (selectedCourier != null)
                {
                    var sc = selectedCourier;
                    if (sc.Has(kind)) GUILayout.Label($"{sc.ShortName}: var", small);
                    else if (GUILayout.Button($"{sc.ShortName}'a al", smallButton)) deferred = () => cm.BuyEquipment(sc, kind);
                }
                if (GUILayout.Button("Eksik olan herkese al", smallButton)) deferred = () => cm.BuyForAll(kind);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
        }

        void DrawCampaigns()
        {
            var camp = CampaignManager.I;
            var clock = GameClock.I;
            var eco = EconomyManager.I;

            GUILayout.Label("Kampanyalar şirketin kârından ödenir; müşterinin teslimat ücretine yansımaz. " +
                            "Boost saatinde çevrimdışı kuryeler çevrimiçi olmaya daha istekli olur, kuryeler sipariş reddetmez ve aynı anda +1 paket alır.", small);

            GUILayout.Space(6);
            GUILayout.Label($"Bugün: {clock.DayName}{(clock.IsWeekend ? " (hafta sonu)" : "")}", header);
            var today = camp.TodayWindows();
            var active = camp.ActiveWindow;
            if (today.Count == 0) GUILayout.Label("Bugün boost yok.", label);
            foreach (var w in today)
            {
                bool on = active != null && w.startHour == active.startHour && w.percent == active.percent;
                bool past = clock.Hour >= w.endHour;
                string state = on ? "  ŞU AN AKTİF" : (past ? "  (bitti)" : "");
                GUILayout.Label($"{w.TimeText}  %{w.percent * 100f:0}  — {w.label}{state}", on ? header : (past ? small : label));
            }

            GUILayout.Space(8);
            GUILayout.Label("Hafta sonu boostları", header);
            camp.weekendMorningBoost = GUILayout.Toggle(camp.weekendMorningBoost,
                $" Sabah boostu {camp.weekendMorningStart:00}:00–{camp.weekendMorningEnd:00}:00 (sipariş az olduğu için)", toggle);
            camp.weekendMorningPercent = PercentSlider("Sabah yüzdesi", camp.weekendMorningPercent, 0.05f, 0.60f);
            camp.weekendLunchBoost = GUILayout.Toggle(camp.weekendLunchBoost,
                $" Öğle boostu {camp.weekendLunchStart:00}:00–{camp.weekendLunchEnd:00}:00", toggle);
            camp.weekendLunchPercent = PercentSlider("Öğle yüzdesi", camp.weekendLunchPercent, 0.05f, 0.60f);

            GUILayout.Space(8);
            GUILayout.Label("Hafta içi rastgele boostlar", header);
            camp.weekdayRandomBoosts = GUILayout.Toggle(camp.weekdayRandomBoosts, " Gün içine serpiştirilmiş boostlar", toggle);
            camp.weekdayBoostCount = IntSlider("Günde kaç tane", camp.weekdayBoostCount, 0, 4, 1);
            camp.weekdayMaxPercent = PercentSlider("En yüksek yüzde", camp.weekdayMaxPercent, 0.10f, 0.60f);
            GUILayout.Label("Hafta içi boost saatleri her gün yeniden belirlenir; değişiklikler ertesi gün geçerli olur.", small);

            GUILayout.Space(8);
            GUILayout.Label("Haftalık hedef ödülü", header);
            camp.weeklyChallenge = GUILayout.Toggle(camp.weeklyChallenge, " Haftalık hedef açık (her Pazartesi sıfırlanır)", toggle);
            camp.weeklyTarget = IntSlider("Hedef paket", camp.weeklyTarget, 10, 200, 5);
            camp.weeklyReward = IntSlider("Ödül (Ft)", camp.weeklyReward, 0, 100000, 1000);

            // Ödülün kârla karşılaştırması
            int samples = 0;
            long sum = 0;
            foreach (var o in OrderManager.I.History) { sum += o.Profit; samples++; }
            float avg = samples > 0 ? sum / (float)samples : 300f;
            RewardCheck(avg, camp.weeklyTarget, camp.weeklyReward);

            GUILayout.Space(8);
            GUILayout.Label("Aylık hedef ödülü", header);
            camp.monthlyChallenge = GUILayout.Toggle(camp.monthlyChallenge, " Aylık hedef açık (her ayın başında sıfırlanır)", toggle);
            camp.monthlyTarget = IntSlider("Hedef paket", camp.monthlyTarget, 50, 1000, 10);
            camp.monthlyReward = IntSlider("Ödül (Ft)", camp.monthlyReward, 0, 200000, 1000);
            RewardCheck(avg, camp.monthlyTarget, camp.monthlyReward);

            GUILayout.Space(8);
            GUILayout.Label($"Bu hafta ({clock.MonthName}, {clock.WeekOfMonth}. hafta)", header);
            GUILayout.Label($"Boost ödemeleri: {Ft(camp.boostPaidThisWeek)}", label);
            GUILayout.Label($"Haftalık hedef ödülleri: {Ft(camp.rewardsPaidThisWeek)} ({camp.rewardsClaimedThisWeek} kurye)", label);
            GUILayout.Label($"Bu ay aylık hedef ödülleri: {Ft(camp.rewardsPaidThisMonth)} ({camp.rewardsClaimedThisMonth} kurye)", label);
            GUILayout.Label($"Bugünkü kampanya gideri: {Ft(eco.Today.boosts + eco.Today.rewards)}", small);
        }

        void RewardCheck(float avgProfit, int target, int reward)
        {
            long targetProfit = (long)(avgProfit * target);
            GUILayout.Label($"Ortalama sipariş kârı ~{Ft((long)avgProfit)} → {target} paket ≈ {Ft(targetProfit)} kâr, ödül {Ft(reward)}.", small);
            if (reward > targetProfit)
                GUILayout.Label("Bu ödül, o paketlerden kazandığından fazla: hedefe ulaşan her kurye zarar ettirir.", warn);
        }

        float PercentSlider(string name, float v, float min, float max)
        {
            float f = SliderRow(name, v, min, max, $"%{v * 100f:0}", 140);
            return Mathf.Round(f * 20f) / 20f; // %5 adımlar
        }

        void DrawFinance()
        {
            var e = EconomyManager.I;
            var clock = GameClock.I;

            GUILayout.Label("Kasa", header);
            GUILayout.Label($"Ana kasa: {Ft(e.money)}", label);
            GUILayout.Label($"Vergi kasası: {Ft(e.taxVault)} (her kârın %{e.vaultPercent * 100f:0}'i buraya ayrılır)", label);
            if (e.taxDebt > 0)
            {
                GUILayout.Label($"Vergi borcu: {Ft(e.taxDebt)} — her gün %2 büyür!", warn);
                if (GUILayout.Button($"Vergi borcunu öde ({Ft(e.taxDebt)})", button)) deferred = () => e.PayDebt();
            }
            if (e.taxVault > 0 && GUILayout.Button("Vergi kasasını ana kasaya aktar", smallButton)) deferred = () => e.WithdrawVault();

            GUILayout.Space(6);
            GUILayout.Label($"Bu vergi yılı (Yıl {clock.Year}: 1 Ocak – 31 Aralık · şirketin {clock.CompanyDay}. günü)", header);
            Stats(e.Year);
            long est = (long)Mathf.Round(Mathf.Max(0f, e.Year.Profit) * e.taxRate);
            GUILayout.Label($"Tahmini yıl sonu vergisi (%{e.taxRate * 100f:0}): {Ft(est)}", label);

            GUILayout.Space(6);
            GUILayout.Label("Bugün", header);
            Stats(e.Today);
            if (e.Yesterday != null)
            {
                GUILayout.Label("Dün", header);
                Stats(e.Yesterday);
            }
            if (!string.IsNullOrEmpty(e.LastTaxSummary))
            {
                GUILayout.Label("Son vergi dönemi", header);
                GUILayout.Label(e.LastTaxSummary, small);
            }

            GUILayout.Space(6);
            GUILayout.Label("Hesap hareketleri", header);
            for (int i = 0; i < Mathf.Min(25, e.Ledger.Count); i++) GUILayout.Label(e.Ledger[i], small);
        }

        void Stats(PeriodStats s)
        {
            // 1) Teslimatlardan kalan
            GUILayout.Label($"Teslimat: {s.deliveries} paket", small);
            GUILayout.Label($"  + Komisyon {Ft(s.commission)}  + Teslimat ücreti {Ft(s.fees)}", small);
            GUILayout.Label($"  − Kurye ödemeleri {Ft(s.courierPay)}" + (s.boosts > 0 ? $"  − Boost {Ft(s.boosts)}" : ""), small);
            GUILayout.Label($"Teslimat kârı: {Ft(s.DeliveryProfit)}" +
                            (s.deliveries > 0 ? $" (paket başı {Ft(s.DeliveryProfit / s.deliveries)})" : ""), label);

            // 2) Yatırımlar ve sabit giderler
            long others = s.OtherExpenses + s.rewards;
            if (others > 0 || s.otherIncome > 0)
            {
                string list = "";
                for (int i = 0; i < s.byCategory.Length; i++)
                    if (s.byCategory[i] > 0) list += (list.Length > 0 ? ", " : "") + $"{PeriodStats.CategoryNames[i]} {Ft(s.byCategory[i])}";
                if (s.rewards > 0) list += (list.Length > 0 ? ", " : "") + $"Hedef ödülleri {Ft(s.rewards)}";
                GUILayout.Label($"Yatırım ve diğer giderler: −{Ft(others)}", label);
                if (list.Length > 0) GUILayout.Label("  " + list, small);
                if (s.otherIncome > 0) GUILayout.Label($"  + Diğer gelir (kurye kesintileri) {Ft(s.otherIncome)}", small);
            }

            // 3) Sonuç
            GUILayout.Label($"Net kâr: {Ft(s.Profit)}" + (s.penalties > 0 ? $" · Cezalar: {Ft(s.penalties)}" : ""), header);
        }

        void DrawSettings()
        {
            var e = EconomyManager.I;

            GUILayout.Label("Fiyatlandırma (müşteriden alınan teslimat ücreti)", header);
            e.baseFee = IntSlider("Taban ücret", e.baseFee, 200, 1500, 10);
            e.feePerKm = IntSlider("Km başı", e.feePerKm, 0, 400, 10);
            e.minProfit = IntSlider("Min. kâr/sipariş", e.minProfit, 0, 1000, 50);
            GUILayout.Label("Kural: Teslimat ücreti = en büyüğü( taban + km ücreti × km , en pahalı kuryenin ücreti + min. kâr − komisyon ). " +
                            "Böylece hiçbir siparişte zarar edilmez. Ücret yükseldikçe vazgeçen müşteri artar.", small);

            GUILayout.Space(8);
            GUILayout.Label("Kurye ücretleri (araca göre)", header);
            foreach (var v in GameData.Vehicles)
                GUILayout.Label($"{v.name}: {Ft(v.basePay)}/paket + {v.payPerKm} Ft/km · {GameData.CarryText(v)}", small);

            GUILayout.Space(8);
            GUILayout.Label("Kurye bonusları", header);
            e.bonusMultiplier = FloatSlider("Bonus çarpanı", e.bonusMultiplier, 0.5f, 2f);
            GUILayout.Label($"Şu anki bonus: {Ft(e.CurrentBonus)} (hava {WeatherSystem.I.Name}: {WeatherSystem.I.WeatherBonus} Ft, saat: {e.TimeBonus()} Ft) × çarpan. " +
                            "Yüksek bonus moral ve daha çok başvuru getirir ama kârı düşürür.", small);

            GUILayout.Space(8);
            GUILayout.Label("Vergi", header);
            e.vaultPercent = FloatSlider("Kârdan vergi kasasına", e.vaultPercent, 0f, 0.5f);
            GUILayout.Label($"Vergi oranı: %{e.taxRate * 100f:0} — yıl sonunda (gelir − giderler) üzerinden ödenir. Giderler vergiden düşülür.", small);

            GUILayout.Space(8);
            GUILayout.Label("Örnek hesap (şu anki ayarlarla)", header);
            const int basket = 5000;
            const int tiles = 15;
            int commission = Mathf.RoundToInt(basket * 0.10f);
            int fee = e.QuoteDeliveryFee(commission, tiles, 2f);
            int pay = e.MaxCourierPay(tiles, 2f);
            GUILayout.Label($"Sepet {Ft(basket)}, komisyon %10 = {Ft(commission)}", small);
            GUILayout.Label($"Mesafe {GameData.KmText(tiles)} → teslimat ücreti {Ft(fee)}", small);
            GUILayout.Label($"Şirket geliri = {Ft(commission + fee)}", small);
            GUILayout.Label($"En pahalı kuryeye ödeme = {Ft(pay)} → kâr {Ft(commission + fee - pay)}", label);
        }

        // ---------------------------------------------------------------- kontroller
        bool SelectButton(bool selected, string text, GUIStyle style, params GUILayoutOption[] options)
        {
            var old = GUI.backgroundColor;
            if (selected) GUI.backgroundColor = SelectedTint;
            bool clicked = GUILayout.Button(selected ? "» " + text + " «" : text, style, options);
            GUI.backgroundColor = old;
            return clicked;
        }

        bool SelectButtonAt(Rect r, bool selected, string text)
        {
            var old = GUI.backgroundColor;
            if (selected) GUI.backgroundColor = SelectedTint;
            bool clicked = GUI.Button(r, text, button);
            GUI.backgroundColor = old;
            return clicked;
        }

        /// <summary>Etiket, kaydırıcı ve değer aynı hizada.</summary>
        float SliderRow(string name, float v, float min, float max, string valueText, float labelWidth)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, label, GUILayout.Width(labelWidth), GUILayout.Height(24));
            GUILayout.BeginVertical();
            GUILayout.Space(9);
            v = GUILayout.HorizontalSlider(v, min, max);
            GUILayout.EndVertical();
            GUILayout.Label(valueText, label, GUILayout.Width(50), GUILayout.Height(24));
            GUILayout.EndHorizontal();
            return v;
        }

        int IntSlider(string name, int v, int min, int max, int step)
        {
            float f = SliderRow(name, v, min, max, v.ToString(), 140);
            return Mathf.RoundToInt(f / step) * step;
        }

        float FloatSlider(string name, float v, float min, float max)
        {
            float f = SliderRow(name, v, min, max, v.ToString("0.00"), 140);
            return Mathf.Round(f * 100f) / 100f;
        }

        void Bar(string name, float v, Color c) => Bar(name, v, c, $"%{v * 100f:0}");

        void Bar(string name, float v, Color c, string valueText)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, small, GUILayout.Width(70));
            Rect r = GUILayoutUtility.GetRect(100, 16, GUILayout.ExpandWidth(true));
            r.y += 3;
            r.height = 10;
            Fill(r, new Color(0f, 0f, 0f, 0.4f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(v), r.height), c);
            GUILayout.Label(valueText, small, GUILayout.Width(52));
            GUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- haber akışı
        void DrawFeed(float sw, float sh)
        {
            var feed = EventManager.I.Feed;
            if (feed.Count == 0) return;
            int lines = Mathf.Min(5, feed.Count);
            var r = new Rect(sw - 520f, sh - 22f * lines - 16f, 510f, 22f * lines + 8f);
            Fill(r, new Color(0f, 0f, 0f, 0.6f));
            for (int i = 0; i < lines; i++)
                GUI.Label(new Rect(r.x + 8, r.y + 4 + i * 22f, r.width - 16, 22f), feed[i], small);
        }

        // ---------------------------------------------------------------- olay kartı
        void DrawEvent(float sw, float sh)
        {
            var ev = EventManager.I.Current;
            if (ev == null) return;

            Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, 0.55f));
            float w = 560f;
            float bodyH = label.CalcHeight(new GUIContent(ev.body), w - 48f);
            float h = 100f + bodyH + ev.choices.Count * 44f;
            var r = new Rect((sw - w) / 2f, (sh - h) / 2f, w, h);
            Fill(r, new Color(0.15f, 0.16f, 0.20f));
            Fill(new Rect(r.x, r.y, r.width, 6f), CompanyProfile.Primary);

            GUILayout.BeginArea(new Rect(r.x + 24, r.y + 20, w - 48, h - 30));
            GUILayout.Label(ev.title, title);
            GUILayout.Label(ev.body, label);
            GUILayout.Space(10);
            for (int i = 0; i < ev.choices.Count; i++)
            {
                int idx = i;
                if (GUILayout.Button(ev.choices[i].label, button, GUILayout.Height(36)))
                    deferred = () => EventManager.I.Choose(idx);
            }
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- yardımcılar
        static void Fill(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;

            var text = new Color(0.92f, 0.93f, 0.95f);

            label = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            label.normal.textColor = text;

            small = new GUIStyle(label) { fontSize = 11 };
            small.normal.textColor = new Color(0.72f, 0.74f, 0.79f);

            warn = new GUIStyle(small);
            warn.normal.textColor = new Color(1f, 0.68f, 0.3f);

            note = new GUIStyle(small) { fontStyle = FontStyle.Italic };
            note.normal.textColor = new Color(0.8f, 0.85f, 0.95f);

            header = new GUIStyle(label) { fontSize = 15, fontStyle = FontStyle.Bold };
            header.normal.textColor = Color.white;

            title = new GUIStyle(header) { fontSize = 24 };

            box = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(0, 0, 4, 4)
            };
            box.normal.background = MakeTex(new Color(1f, 1f, 1f, 0.06f));

            button = new GUIStyle(GUI.skin.button) { fontSize = 13, wordWrap = true };
            smallButton = new GUIStyle(GUI.skin.button) { fontSize = 11, wordWrap = true };
            bigButton = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };

            toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 13 };
            toggle.normal.textColor = text;
            toggle.onNormal.textColor = text;
            toggle.hover.textColor = Color.white;
            toggle.onHover.textColor = Color.white;

            textField = new GUIStyle(GUI.skin.textField) { fontSize = 18 };

            worldLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            barTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            barText = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
            logoLetter = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}
