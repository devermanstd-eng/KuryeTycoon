using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Kurye telefonunda aktif görev ekranı (gerçek kurye uygulaması gibi):
    ///  • Teslim alma: "Teslim alınacak yer", dükkan adı ve adresi; "Siparişi al" kartları (#numara, müşterinin kısa adı,
    ///    sipariş içeriği). Kartın okuna basınca "Teslimatta sorun mu var?": Gecikme bildir · Destek.
    ///    Altta "Bu teslimatı iptal et" ve "Destekle iletişime geç".
    ///  • Teslimat: "Teslim edilecek kişi", müşteri ve adres; Bilgi sekmesi (kapı kodu, zil/interkom, kat, daire, notlar ve
    ///    Çevir, nakit, temassız teslimat uyarısı), Ürünler sekmesi (ürünler, teslimat zaman çizelgesi, çevrimdışı saatini
    ///    ayarla, önceki görev).
    ///  • Altta sabit durum düğmesi (Dükkana gidiyor / Siparişleri al / Sipariş teslim edildi!).
    /// Düğmeler gerçekten çalışır: gecikme bildirmek söz verilen süreyi uzatır, destek dükkanı hızlandırır ya da adres
    /// sorununu hemen çözmeye çalışır, iptal siparişi başka kuryeye verir, çevrimdışı saati kuryeyi o saatte eve gönderir.
    /// </summary>
    public partial class GameUI
    {
        int phoneTaskTab;                                   // 0 Bilgi, 1 Ürünler
        readonly HashSet<int> phoneExpanded = new HashSet<int>();
        readonly HashSet<int> phoneTranslated = new HashSet<int>();
        bool phoneOfflineEdit;
        int phoneConfirmCancel = -1;

        static readonly Color PWarnBg = new Color(0.22f, 0.10f, 0.03f);
        static readonly Color PWarnText = new Color(1f, 0.65f, 0.35f);
        static readonly Color PCancelBg = new Color(0.32f, 0.04f, 0.04f);
        static readonly Color PLate = new Color(1f, 0.74f, 0.48f);

        static string Hm(float totalMinutes)
        {
            int m = Mathf.Max(0, Mathf.RoundToInt(totalMinutes));
            return $"{(m / 60) % 24:00}:{m % 60:00}";
        }

        static string ShopAddress(Shop s) => $"{Addresses.StreetOf(s.door)} {Addresses.Number(s.tile, s.door)}, 4400 Nyíregyháza";
        static string CustomerAddress(Order o) => $"{o.street} {o.number}" + (o.blocks > 1 ? $" {o.block} Blok" : "") + ", 4400 Nyíregyháza";

        bool InPickupPhase(Courier c) =>
            c.State == CourierState.DukkanaGidiyor || c.State == CourierState.Bekliyor;

        // ====================================================================== görev ekranı

        void HomeTaskApp(Courier c)
        {
            var cur = c.CurrentOrder;
            if (cur == null)
            {
                // Sipariş arası (bir sonrakine geçiyor)
                GUILayout.Label("Sıradaki görev hazırlanıyor", pTitle);
                foreach (var o in c.CarryingList) OrderCard(o, c, true, GameClock.I.TotalMinutes);
                foreach (var o in c.ToPickList) OrderCard(o, c, false, GameClock.I.TotalMinutes);
                return;
            }
            if (InPickupPhase(c)) PickupView(c, cur);
            else DropoffView(c, cur);
        }

        // ---------------------------------------------------------------------- teslim alma

        void PickupView(Courier c, Order cur)
        {
            GUILayout.Label("Teslim alınacak yer", new GUIStyle(pBody) { fontSize = 14, fontStyle = FontStyle.Bold });
            var big = new GUIStyle(pHuge) { fontSize = 22 };
            big.normal.textColor = PAccent;
            GUILayout.Label(cur.shop.Name, big);
            GUILayout.Label(ShopAddress(cur.shop), pBody);
            PLineSpace();

            GUILayout.Label("Siparişi al", pTitle);
            GUILayout.Space(4);
            // Bu dükkandan alınacak bütün siparişler (toplu siparişte birden fazla)
            foreach (var o in c.ToPickList)
                if (o.shop == cur.shop) CollectCard(c, o);

            GUILayout.Space(6);
            PLineSpace();
            GUILayout.Label("Teslimatta sorun mu var?", pTitle);
            GUILayout.Space(6);
            if (phoneConfirmCancel == cur.id)
            {
                GUILayout.Label($"#{cur.id} iptal edilsin mi? Sipariş başka kuryeye verilir; müşteri biraz daha bekler.", pDimS);
                GUILayout.BeginHorizontal();
                if (PhoneButton("Evet, iptal et", PCancelBg, PRed, 44f)) { var o = cur; deferred += () => { c.CancelTask(o); phoneConfirmCancel = -1; }; }
                if (PhoneButton("Vazgeç", PCard, Color.white, 44f)) deferred += () => phoneConfirmCancel = -1;
                GUILayout.EndHorizontal();
            }
            else if (PhoneButton("Bu teslimatı iptal et", PCancelBg, PRed, 48f)) { int id = cur.id; deferred += () => phoneConfirmCancel = id; }
            GUILayout.Space(4);
            if (PhoneLink("Destekle iletişime geç")) { var o = cur; deferred += () => SupportForPickup(c, o); }
        }

        void CollectCard(Courier c, Order o)
        {
            var start = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            GUILayout.BeginVertical();
            GUILayout.Space(8);
            GUILayout.Label($"#{o.id}", new GUIStyle(pHuge) { fontSize = 24 });
            GUILayout.Label(o.ShortCustomerName, pBody);
            PLineSpace();
            GUILayout.Label("SİPARİŞ DETAYI", pSmall);
            foreach (var it in o.items) GUILayout.Label($"{it.qty} x {it.name}", pDimS);
            if (o.markedLate) GUILayout.Label("Gecikme bildirildi: müşteriye haber verildi (+10 dk).", new GUIStyle(pDimS) { normal = { textColor = PLate } });
            if (o.shopQueue && c.State == CourierState.Bekliyor && c.CurrentOrder == o)
                GUILayout.Label($"Dükkanda sıra var · hazır olmasına ~{Mathf.Max(1f, o.readyAt - GameClock.I.TotalMinutes):0} dk", new GUIStyle(pDimS) { normal = { textColor = PLate } });

            bool open = phoneExpanded.Contains(o.id);
            if (open)
            {
                PLineSpace();
                GUILayout.Label("Teslimatta sorun mu var?", new GUIStyle(pBody) { fontStyle = FontStyle.Bold, fontSize = 14 });
                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                bool ge = GUI.enabled;
                GUI.enabled = ge && !o.markedLate;
                if (PhoneButton(o.markedLate ? "Bildirildi" : "Gecikme bildir", PLate, new Color(0.08f, 0.06f, 0.04f), 40f)) { var ord = o; deferred += () => MarkLate(c, ord); }
                GUI.enabled = ge;
                if (PhoneLinkInline("Destek")) { var ord = o; deferred += () => SupportForPickup(c, ord); }
                GUILayout.EndHorizontal();
            }
            PLineSpace();
            GUILayout.BeginHorizontal();
            string status = c.CurrentOrder == o && c.State == CourierState.Bekliyor ? "Hazırlanıyor" : "Teslim alınacak";
            PhoneButton(status, PAccent, new Color(0.04f, 0.05f, 0.08f), 44f, true);
            var chev = GUILayoutUtility.GetRect(44, 44, GUILayout.Width(44));
            UITheme.Box(chev, new Color(0.04f, 0.17f, 0.21f), 22f);
            Chevron(chev.center, open, PAccent);
            if (GUI.Button(chev, GUIContent.none, GUIStyle.none)) { int id = o.id; deferred += () => { if (!phoneExpanded.Remove(id)) phoneExpanded.Add(id); }; }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            GUILayout.EndVertical();
            var end = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                // Kartın arka planı (içerik üstüne yarı saydam çizilir: yazılar okunur kalır)
                var card = new Rect(start.x - 8, start.y, start.width + 16, end.y - start.y);
                UITheme.Outline(card, PLine, 1f, 12f);
            }
            GUILayout.Space(6);
        }

        // ---------------------------------------------------------------------- teslimat

        void DropoffView(Courier c, Order cur)
        {
            GUILayout.Label("Teslim edilecek kişi", new GUIStyle(pBody) { fontSize = 14, fontStyle = FontStyle.Bold });
            var big = new GUIStyle(pHuge) { fontSize = 22 };
            big.normal.textColor = PGreen;
            GUILayout.Label(cur.ShortCustomerName, big);
            GUILayout.Label(CustomerAddress(cur), pBody);
            GUILayout.Space(8);

            // Bilgi | Ürünler
            var seg = GUILayoutUtility.GetRect(100, 34, GUILayout.ExpandWidth(true));
            UITheme.Box(seg, new Color(0.16f, 0.17f, 0.22f), 10f);
            var half = new Rect(seg.x + 3, seg.y + 3, seg.width / 2f - 3, seg.height - 6);
            var half2 = new Rect(seg.center.x, seg.y + 3, seg.width / 2f - 3, seg.height - 6);
            UITheme.Box(phoneTaskTab == 0 ? half : half2, new Color(0.38f, 0.40f, 0.47f), 8f);
            GUI.Label(half, "Bilgi", pCenter);
            GUI.Label(half2, "Ürünler", pCenter);
            if (GUI.Button(half, GUIContent.none, GUIStyle.none)) deferred += () => phoneTaskTab = 0;
            if (GUI.Button(half2, GUIContent.none, GUIStyle.none)) deferred += () => phoneTaskTab = 1;
            GUILayout.Space(8);

            if (phoneTaskTab == 0) DropoffInfo(c, cur);
            else DropoffItems(c, cur);
        }

        void DropoffInfo(Courier c, Order o)
        {
            // Adres sorunu: hemen destek
            if (c.State == CourierState.AdresSorunu)
            {
                WarnBox(o.addressIssue == AddressIssue.YanlisAdres ? "ADRES YANLIŞ İŞARETLENMİŞ" : "MÜŞTERİ KAPIYI AÇMIYOR",
                        o.addressIssue == AddressIssue.YanlisAdres ? "Kurye binayı bulamıyor. Destek müşteriyi arayıp doğru adresi öğrenebilir."
                                                                   : "Kurye kapıda bekliyor. Destek müşteriyi arayabilir.");
                if (PhoneButton("Desteği hemen ara", PLate, new Color(0.08f, 0.06f, 0.04f), 42f)) deferred += () =>
                {
                    if (c.ContactSupportNow()) EventManager.I.Notify($"{c.ShortName} #{o.id} için desteği aradı.");
                };
                GUILayout.Space(6);
            }

            GUILayout.Label("Adres detayları", pTitle);
            GUILayout.Space(2);
            if (o.doorCode != null)
                DetailRow("Bina kapı kodu", o.codeMissing ? "Müşteri yazmamış · kapıda arayacak (+1,5 dk)" : o.doorCode, o.codeMissing ? PLate : (Color?)null);
            else DetailRow("Bina kapı kodu", "Yok (müstakil / küçük bina)");
            DetailRow("Zil / İnterkom", o.intercom);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(); DetailRow("Kat", Addresses.FloorText(o.floor) + (o.blocks > 1 ? $" · {o.block} Blok" : ""), null, false); GUILayout.EndVertical();
            GUILayout.BeginVertical(); DetailRow("Daire", o.flat > 0 ? o.flat.ToString() : "—", null, false); GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            PLineSpace();
            GUILayout.Label(o.BuildingText, o.UsesStairs && o.floor >= 4 ? new GUIStyle(pDimS) { normal = { textColor = PLate } } : pDimS);

            if (!string.IsNullOrEmpty(o.customerNote))
            {
                PLineSpace();
                GUILayout.Label("Notlar", pTitle);
                bool tr = phoneTranslated.Contains(o.id);
                GUILayout.Label(tr && o.noteTranslation != "" ? o.noteTranslation : o.customerNote, pBody);
                if (o.noteTranslation != "")
                {
                    if (PhoneLinkInline(tr ? "Orijinali göster" : "Çevir")) { int id = o.id; deferred += () => { if (!phoneTranslated.Remove(id)) phoneTranslated.Add(id); }; }
                }
            }

            if (o.cashPay)
            {
                PLineSpace();
                GUILayout.Label("Ödeme", pTitle);
                DetailRow("Nakit tahsil edilecek", Ft(o.CashAmount), PLate);
                DetailRow("Müşteri verecek / para üstü", $"{Ft(o.cashGiven)} / {Ft(o.CashChange)}");
            }

            GUILayout.Space(6);
            GUILayout.Label("Sipariş detayı", pTitle);
            if (o.noContact) WarnBox("TEMASSIZ TESLİMAT", "Lütfen siparişi müşterinin kapısına bırak. Müşteri evde olmasa da teslim edilir.");
            float left = o.deadlineAt - GameClock.I.TotalMinutes;
            DetailRow("Söz verilen süre", left >= 0 ? $"{left:0} dk kaldı · {Hm(o.deadlineAt)}" : $"{-left:0} dk gecikmede", left >= 0 ? (Color?)null : PRed);
            DetailRow("Kazanç", Ft(o.courierPay + o.boostPay));
            if (!o.markedLate && left < 8f)
            {
                if (PhoneButton("Gecikme bildir (+10 dk)", PLate, new Color(0.08f, 0.06f, 0.04f), 40f)) { var ord = o; deferred += () => MarkLate(c, ord); }
            }

            // Çantadaki diğer siparişler
            int others = 0;
            foreach (var x in c.CarryingList) if (x != o) others++;
            foreach (var x in c.ToPickList) others++;
            if (others > 0)
            {
                PLineSpace();
                GUILayout.Label("Sıradaki siparişler", pTitle);
                float now = GameClock.I.TotalMinutes;
                foreach (var x in c.CarryingList) if (x != o) OrderCard(x, c, true, now);
                foreach (var x in c.ToPickList) OrderCard(x, c, false, now);
            }
        }

        void DropoffItems(Courier c, Order o)
        {
            GUILayout.Label("Ürünler", pTitle);
            GUILayout.Space(2);
            var list = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            foreach (var it in o.items)
            {
                GUILayout.Label($"{it.qty} ×  {it.name}", new GUIStyle(pBody) { fontSize = 14, fontStyle = FontStyle.Bold });
                GUILayout.Space(2);
            }
            GUILayout.Label($"{o.ItemCount} ürün · {GameData.KgText(o.weightKg)} · sepet {Ft(o.basket)}", pDimS);
            PLineSpace();

            // Zaman çizelgesi
            GUILayout.Label("Teslimat zaman çizelgesi", pTitle);
            GUILayout.Space(4);
            float now = GameClock.I.TotalMinutes;
            bool picked = o.pickedAt > 0f;
            TimelineRow(picked ? Hm(o.pickedAt) : Hm(Mathf.Max(now, o.readyAt)), picked, PAccent, $"{o.shop.Name}'dan teslim alma");
            float eta = c.CurrentOrder == o ? now + EtaMinutes(c) + (c.State == CourierState.KataCikiyor ? c.DoorMinutesLeft : o.DoorMinutes(c.stamina, true)) : o.deadlineAt;
            TimelineRow(Hm(eta), false, PGreen, $"{o.ShortCustomerName}'ya teslimat");
            GUILayout.Space(8);

            // Çevrimdışı saati
            if (c.plannedOfflineAt > 0f)
                GUILayout.Label($"Planlanan çevrimdışı saati: {Hm(c.plannedOfflineAt)}", new GUIStyle(pDimS) { normal = { textColor = PLate } });
            if (!phoneOfflineEdit)
            {
                if (PhoneButton("Çevrimdışı saatini ayarla", PCancelBg, PRed, 46f)) deferred += () => phoneOfflineEdit = true;
            }
            else
            {
                GUILayout.Label("Ne zaman çevrimdışı olsun? Elindeki siparişleri her zaman bitirir.", pDimS);
                GUILayout.BeginHorizontal();
                if (PhoneButton("Bu görevden sonra", PCard, Color.white, 38f)) deferred += () =>
                {
                    c.plannedOfflineAt = -1f; c.EndShiftEarly("Uygulamadan: bu görevden sonra çevrimdışı"); phoneOfflineEdit = false;
                    EventManager.I.Notify($"{c.ShortName} bu görevden sonra çevrimdışı olacak.");
                };
                if (PhoneButton("1 saat sonra", PCard, Color.white, 38f)) deferred += () =>
                {
                    c.plannedOfflineAt = GameClock.I.TotalMinutes + 60f; phoneOfflineEdit = false;
                    EventManager.I.Notify($"{c.ShortName} {Hm(c.plannedOfflineAt)}'da çevrimdışı olacak.");
                };
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (PhoneButton("2 saat sonra", PCard, Color.white, 38f)) deferred += () =>
                {
                    c.plannedOfflineAt = GameClock.I.TotalMinutes + 120f; phoneOfflineEdit = false;
                    EventManager.I.Notify($"{c.ShortName} {Hm(c.plannedOfflineAt)}'da çevrimdışı olacak.");
                };
                if (PhoneButton(c.plannedOfflineAt > 0f ? "Planı kaldır" : "Vazgeç", PCard, PDim, 38f)) deferred += () => { c.plannedOfflineAt = -1f; phoneOfflineEdit = false; };
                GUILayout.EndHorizontal();
            }
            PLineSpace();

            GUILayout.Label("Hızlı bağlantılar", pTitle);
            var row = GUILayoutUtility.GetRect(100, 44, GUILayout.ExpandWidth(true));
            Icons.Draw(new Rect(row.x + 2, row.y + 12, 20, 20), Icons.Kind.Clock, Color.white);
            GUI.Label(new Rect(row.x + 34, row.y, row.width - 60, row.height), "Önceki görev", new GUIStyle(pRowT) { alignment = TextAnchor.MiddleLeft });
            GUI.Label(new Rect(row.xMax - 20, row.y, 20, row.height), ">", new GUIStyle(pRowT) { alignment = TextAnchor.MiddleCenter });
            Fill(new Rect(row.x, row.yMax - 1, row.width, 1), PLine);
            if (GUI.Button(row, GUIContent.none, GUIStyle.none)) GoPhone(PhoneScreen.PrevTask);
        }

        /// <summary>Ok başı (aşağı / yukarı) küçük karelerle.</summary>
        static void Chevron(Vector2 c, bool up, Color col)
        {
            for (int i = -4; i <= 4; i++)
            {
                float y = (up ? 1f : -1f) * (Mathf.Abs(i) - 2f);
                UITheme.Box(new Rect(c.x + i * 1.2f - 1.2f, c.y + y - 1.2f, 2.6f, 2.6f), col, 1.2f);
            }
        }

        void TimelineRow(string time, bool done, Color c, string text)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(time, new GUIStyle(pBody) { fontSize = 14, wordWrap = false }, GUILayout.Width(46));
            var dot = GUILayoutUtility.GetRect(22, 22, GUILayout.Width(22));
            if (done) { UITheme.Box(new Rect(dot.x + 3, dot.y + 3, 16, 16), c, 8f); GUI.Label(new Rect(dot.x + 3, dot.y + 3, 16, 16), "✓", new GUIStyle(pCenter) { fontSize = 10, normal = { textColor = Color.black } }); }
            else UITheme.Outline(new Rect(dot.x + 3, dot.y + 3, 16, 16), c, 2f, 8f);
            GUILayout.Label(done ? text : $"<b>{text}</b>", new GUIStyle(pBody) { fontSize = 14 });
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        void DetailRow(string k, string v, Color? vc = null, bool line = true)
        {
            GUILayout.Label(k, pDimS);
            var st = new GUIStyle(pBody) { fontSize = 16 };
            if (vc.HasValue) st.normal.textColor = vc.Value;
            GUILayout.Label(v, st);
            if (line) PLineSpace();
        }

        void WarnBox(string head, string text)
        {
            var st = new GUIStyle(pBody) { fontSize = 12 };
            float w = Mathf.Max(100f, phoneContentWidth - 52f);
            float h = 22f + st.CalcHeight(new GUIContent(text), w) + 14f;
            var r = GUILayoutUtility.GetRect(100, h, GUILayout.ExpandWidth(true));
            UITheme.Box(r, PWarnBg, 10f);
            Icons.Draw(new Rect(r.x + 12, r.y + 12, 18, 18), Icons.Kind.Warning, PWarnText);
            var hs = new GUIStyle(pSmall) { fontSize = 11 };
            hs.normal.textColor = Color.white;
            GUI.Label(new Rect(r.x + 40, r.y + 10, r.width - 48, 18), head, hs);
            GUI.Label(new Rect(r.x + 40, r.y + 28, r.width - 48, h - 30), text, st);
            GUILayout.Space(6);
        }

        float phoneContentWidth = 280f;

        /// <summary>Uygulama düğmesi (yuvarlak köşeli). inert = sadece durum gösterir.</summary>
        bool PhoneButton(string text, Color bg, Color fg, float h, bool inert = false)
        {
            var r = GUILayoutUtility.GetRect(60, h, GUILayout.ExpandWidth(true), GUILayout.Height(h));
            var col = GUI.enabled ? bg : UITheme.WithAlpha(bg, 0.45f);
            UITheme.Box(new Rect(r.x + 2, r.y, r.width - 4, r.height), col, h / 2f);
            var st = new GUIStyle(pBtnText) { fontSize = h >= 44f ? 15 : 13 };
            st.normal.textColor = fg;
            GUI.Label(r, text, st);
            if (inert) return false;
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        bool PhoneLink(string text)
        {
            var r = GUILayoutUtility.GetRect(100, 36, GUILayout.ExpandWidth(true));
            var st = new GUIStyle(pAcc) { alignment = TextAnchor.MiddleCenter, fontSize = 15, wordWrap = false };
            GUI.Label(r, text, st);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        bool PhoneLinkInline(string text)
        {
            var st = new GUIStyle(pAcc) { alignment = TextAnchor.MiddleCenter, fontSize = 14, wordWrap = false };
            var size = st.CalcSize(new GUIContent(text));
            var r = GUILayoutUtility.GetRect(size.x + 20, 36, GUILayout.Width(size.x + 20));
            GUI.Label(r, text, st);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// <summary>Alt sheet'in en altında sabit durum düğmesi (kaydırınca kaybolmaz).</summary>
        void TaskStatusPill(Courier c, Rect r)
        {
            var cur = c.CurrentOrder;
            string text;
            if (cur == null) text = "Sıradaki görev";
            else if (c.State == CourierState.DukkanaGidiyor) text = $"Dükkana gidiyor · {EtaMinutes(c):0} dk";
            else if (c.State == CourierState.Bekliyor) text = "Siparişleri al · hazırlanıyor";
            else if (c.State == CourierState.KataCikiyor) text = $"Sipariş teslim ediliyor · {c.DoorMinutesLeft:0} dk";
            else if (c.State == CourierState.AdresSorunu) text = "Adreste sorun var";
            else text = $"Teslimata {EtaMinutes(c):0} dk";
            UITheme.Box(r, PAccent, r.height / 2f);
            var d = new Rect(r.x + 5, r.y + 5, r.height - 10, r.height - 10);
            UITheme.Box(d, new Color(0.03f, 0.035f, 0.05f), d.width / 2f);
            for (int i = 0; i <= 6; i++)
            {
                UITheme.Box(new Rect(d.center.x - 3 + i - 1, d.center.y - 6 + i - 1, 3, 3), PAccent, 1.5f);
                UITheme.Box(new Rect(d.center.x - 3 + i - 1, d.center.y + 6 - i - 1, 3, 3), PAccent, 1.5f);
            }
            GUI.Label(new Rect(r.x + 40, r.y, r.width - 40, r.height), text, pBtnText);
        }

        // ====================================================================== işlemler

        void MarkLate(Courier c, Order o)
        {
            if (o == null || o.markedLate) return;
            o.markedLate = true;
            o.deadlineAt += 10f;
            EventManager.I.Notify($"{c.ShortName} #{o.id} için gecikme bildirdi: müşteriye haber verildi, söz verilen süre +10 dk.");
        }

        /// <summary>Teslim almada destek: dükkanda bekliyorsa destek dükkanı arar (hazırlık hızlanır).</summary>
        void SupportForPickup(Courier c, Order o)
        {
            if (o == null) return;
            float now = GameClock.I.TotalMinutes;
            float wait = o.readyAt - now;
            if (wait <= 0.5f)
            {
                EventManager.I.Notify($"Destek: #{o.id} dükkanda hazır görünüyor, kurye vardığında teslim alabilir.");
                return;
            }
            if (o.shopPinged)
            {
                EventManager.I.Notify($"Destek #{o.id} için dükkanı zaten aradı. Hazır olmasına ~{wait:0} dk.");
                return;
            }
            o.shopPinged = true;
            var agent = SupportManager.I != null ? SupportManager.I.OnDutyAgent() : null;
            if (agent != null)
            {
                o.readyAt = now + wait * 0.5f;
                EventManager.I.Notify($"Destek ({agent.name.Split(' ')[0]}) {o.shop.Name}'ı aradı: #{o.id} öne alındı, ~{wait * 0.5f:0} dk sonra hazır.");
            }
            else if (Random.value < 0.35f)
            {
                o.readyAt = now + wait * 0.75f;
                EventManager.I.Notify($"Vardiyada destek yok; {c.ShortName} dükkanla kendisi konuştu, #{o.id} biraz hızlandı (~{wait * 0.75f:0} dk).");
            }
            else EventManager.I.Notify($"Vardiyada destek personeli yok. {c.ShortName} beklemeye devam ediyor (~{wait:0} dk). Destek'ten personel alabilirsin.");
        }

        // ====================================================================== önceki görev

        void PagePrevTask(Courier c)
        {
            var o = c.lastDelivered;
            if (o == null)
            {
                GUILayout.Label("Bugün henüz tamamlanan görev yok.", pDimS);
                return;
            }
            GUILayout.Label($"#{o.id} · {o.shop.Name}", pTitle);
            GUILayout.Label($"{o.ShortCustomerName} · {CustomerAddress(o)}", pDimS);
            PLineSpace();
            foreach (var it in o.items) GUILayout.Label($"{it.qty} × {it.name}", pBody);
            PLineSpace();
            TimelineRow(o.pickedAt > 0f ? Hm(o.pickedAt) : "—", true, PAccent, $"{o.shop.Name}'dan teslim alındı");
            TimelineRow(Hm(o.deliveredAt), true, PGreen, o.keptByCourier ? "Müşteriye ulaşılamadı" : $"{o.ShortCustomerName}'ya teslim edildi");
            PLineSpace();
            PRow("Kazanç", Ft(o.courierPay + o.boostPay));
            if (o.tip > 0) PRow("Bahşiş", Ft(o.tip), PGreen);
            PRow("Mesafe", GameData.KmText(o.distance));
            PRow("Müşteri puanı", o.rating > 0f ? $"{o.rating:0.0} ★" : "—");
            if (o.deliveredCold) GUILayout.Label("Yemek soğuk teslim edildi.", new GUIStyle(pDimS) { normal = { textColor = PRed } });
            if (o.markedLate) GUILayout.Label("Gecikme bildirilmişti.", new GUIStyle(pDimS) { normal = { textColor = PLate } });
        }
    }
}
