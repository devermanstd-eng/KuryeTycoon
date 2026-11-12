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

        /// <summary>Kartın sağ sütununda etiketlerin sığacağı genişlik.</summary>
        float CardColumnWidth => panelWidth - 90f - PortraitSize - 10f;

        // ---------------------------------------------------------------- yıldızlar ve beceriler

        static int Stars(float v, float min, float max) => Mathf.Clamp(1 + Mathf.RoundToInt(Mathf.Clamp01((v - min) / (max - min)) * 4f), 1, 5);
        static int SpeedStars(float v) => Stars(v, 0.85f, 1.3f);
        static int CareStars(float v) => Stars(v, 0.55f, 1f);
        static int StaminaStars(float v) => Stars(v, 0.7f, 1.3f);

        static string TidyWord(float tidiness)
        {
            int s = Stars(tidiness, 0.2f, 1f);
            return s <= 2 ? "dağınık" : s == 3 ? "orta titiz" : "titiz";
        }

        static void DrawStars(Rect r, int n, float size)
        {
            if (Event.current.type != EventType.Repaint) return;
            for (int i = 0; i < 5; i++)
                Icons.Draw(new Rect(r.x + i * (size + 2f), r.y + (r.height - size) / 2f, size, size), Icons.Kind.Star, i < n ? StarOn : StarOff);
        }

        /// <summary>Fare bu alanın üstündeyse "?" balonundaki gibi açıklama gösterir.</summary>
        void HoverTip(Rect r, string text)
        {
            if (Event.current.type != EventType.Repaint || !modalFree) return;
            if (!r.Contains(Event.current.mousePosition)) return;
            helpTip = text;
            helpTipInWindow = drawingWindow;
        }

        void SkillCell(string name, int stars, string help, float nameWidth)
        {
            GUILayout.Label(name, kitKey, GUILayout.Width(nameWidth));
            var nameR = GUILayoutUtility.GetLastRect();
            var r = GUILayoutUtility.GetRect(5 * 15f, 20f, GUILayout.Width(5 * 15f), GUILayout.Height(20f));
            DrawStars(r, stars, 13f);
            HoverTip(Rect.MinMaxRect(nameR.xMin, r.yMin, r.xMax, r.yMax), $"<b>{name}</b> {stars}/5: {help}");
        }

        /// <summary>Destek personelinin tek becerisi (0,5–1 arası) yıldızla.</summary>
        void SupportSkillRow(float skill)
        {
            GUILayout.BeginHorizontal();
            SkillCell("Yetenek", Stars(skill, 0.5f, 1f), "şikayetleri daha ucuza kapatır (iade değerinin %30'u ile %60'ı arası).", 80f);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>Dört beceri, ikişerli iki satırda yıldızla.</summary>
        void SkillGrid(float speed, float care, float politeness, float stamina)
        {
            const float nw = 96f;
            GUILayout.BeginHorizontal();
            SkillCell("Hız", SpeedStars(speed), "teslimatları daha çabuk bitirir.", nw);
            GUILayout.Space(14);
            SkillCell("Dikkat", CareStars(care), "siparişte eksik ve hasar az olur, kaskını daha çok takar.", nw);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            SkillCell("Nezaket", CareStars(politeness), "müşteriler daha yüksek puan verir.", nw);
            GUILayout.Space(14);
            SkillCell("Dayanıklılık", StaminaStars(stamina), "geç yorulur, kapıya ve kata daha çabuk çıkar.", nw);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

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

        void EndIdCardHead()
        {
            GUILayout.EndVertical();
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
            HoverTip(r, $"<b>Tecrübe {level}</b>: teslimat yaptıkça artar; her seviye biraz daha hızlı.");
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

        /// <summary>Moral (yüz simgesiyle) ve yorgunluk çubukları tek satırda.</summary>
        void MoodFatigueRow(float morale, float fatigue)
        {
            GUILayout.BeginHorizontal();
            var ir = GUILayoutUtility.GetRect(22f, 20f, GUILayout.Width(22f), GUILayout.Height(20f));
            Icons.Draw(new Rect(ir.x, ir.y + 2f, 16f, 16f), Icons.Mood(morale), Icons.MoodColor(morale));
            MiniBar("Moral", morale, Icons.MoodColor(morale), 50f);
            GUILayout.Space(12);
            MiniBar("Yorgunluk", fatigue, FatigueColor, 76f);
            GUILayout.EndHorizontal();
        }

        /// <summary>Metnin soluna simge koyan düğme (ör. kurye atarken aracın simgesi).</summary>
        bool IconButton(Icons.Kind icon, string text, GUIStyle style, params GUILayoutOption[] options)
        {
            const string pad = "      ";   // simgeye yer
            bool clicked = GUILayout.Button(pad + text, style, options);
            if (Event.current.type == EventType.Repaint)
            {
                var r = GUILayoutUtility.GetLastRect();
                float cw = Mathf.Min(r.width, style.CalcSize(new GUIContent(pad + text)).x) - style.padding.horizontal;
                float s = Mathf.Min(16f, style.fontSize + 2f);
                float x = r.center.x - cw / 2f;
                var col = style.normal.textColor;
                if (!GUI.enabled) col.a *= 0.45f;
                Icons.Draw(new Rect(x, r.center.y - s / 2f, s, s), icon, col);
            }
            return clicked;
        }

        /// <summary>Solunda küçük simge olan yazı satırı.</summary>
        void IconLabel(Icons.Kind icon, string text, GUIStyle style)
        {
            GUILayout.BeginHorizontal();
            var r = GUILayoutUtility.GetRect(20f, 18f, GUILayout.Width(20f), GUILayout.Height(18f));
            Icons.Draw(new Rect(r.x + 1f, r.y + 2f, 15f, 15f), icon, style.normal.textColor);
            GUILayout.Label(text, style);
            GUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- kurye kartı

        /// <summary>Kuryeler penceresindeki kimlik kartı (tabloda satıra tıklayınca da bu açılır).</summary>
        void DrawCourierCard(Courier c, bool forceOpen)
        {
            bool open = forceOpen || CardOpen(c);
            GUILayout.BeginVertical(box);
            BeginIdCardHead(c.courierName, Portraits.MoodOf(c.morale), CompanyProfile.Primary, c.vehicle, c.level);

            // Durum: tek bakışta
            Chip(c.StateName, CourierStateColor(c));
            if (c.fatigue >= 0.6f) Chip("Yorgun", UITheme.Warn);
            if (c.cashHeld >= Courier.CashLimit * 0.7f && !c.cashBlocked) Chip("Nakit borcu yüksek", UITheme.Warn);
            if (c.hygiene < Courier.HygieneLimit) Chip("Çanta kirli", UITheme.Warn);
            if (c.BagWorn) Chip("Çanta yıpranmış", UITheme.Warn);
            if (!c.insured) Chip("Sigortasız", NeutralTone);
            var pens = c.ActivePenalties();
            if (pens.Count > 0) Chip(pens.Count == 1 ? "1 eksik" : $"{pens.Count} eksik", UITheme.Warn);   // ayrıntısı Detay'da
            if (c.RejectChance > 0f) Chip($"Teklif reddedebilir (%{c.RejectChance * 100f:0})", UITheme.Warn);
            ChipRow(CardColumnWidth);

            MoodFatigueRow(c.morale, c.fatigue);
            GUILayout.Label($"Bugün {c.deliveriesToday} teslimat · {Ft(c.earnedToday)} kazandı", bodyDim);
            EndIdCardHead();

            if (open)
            {
                GUILayout.Space(4);
                foreach (var pen in pens) GUILayout.Label(pen, warn);
                DrawCourierDetails(c);
            }

            GUILayout.BeginHorizontal();
            var courier = c;
            if (!forceOpen) DetailButton(c);
            if (GUILayout.Button("Ekipman", smallButton, GUILayout.Width(96)))
                deferred = () => { selectedCourier = courier; tab = Tab.Ekipman; panelOpen = true; scroll = Vector2.zero; };
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        // ---------------------------------------------------------------- başvuru kartı

        void DrawCandidateCard(CourierCandidate cand, float now)
        {
            var cm = CourierManager.I;
            var up = UpgradeManager.I;
            var v = GameData.Vehicle(cand.vehicle);
            bool open = CardOpen(cand);
            float hoursLeft = Mathf.Max(0f, (cand.expiresAt - now) / 60f);

            GUILayout.BeginVertical(box);
            BeginIdCardHead(cand.name, 2, null, cand.vehicle, cand.level);
            if (!string.IsNullOrEmpty(cand.note)) GUILayout.Label($"\"{cand.note}\"", note);
            Chip(ShortLabel(Courier.OccupationLabel(cand.occupation)), NeutralTone);
            Chip(cand.pickiness >= 0.75f ? "Çok seçici" : cand.pickiness >= 0.45f ? "Normal seçici" : "Pek seçici değil",
                 cand.pickiness >= 0.75f ? UITheme.Warn : cand.pickiness >= 0.45f ? NeutralTone : UITheme.Good);
            Chip(cand.insured ? "Sigortalı" : "Sigortasız", cand.insured ? UITheme.Good : NeutralTone);
            Chip($"{hoursLeft:0} saat geçerli", hoursLeft <= 3f ? UITheme.Warn : NeutralTone);
            ChipRow(CardColumnWidth);
            EndIdCardHead();

            GUILayout.Space(2);
            SkillGrid(cand.speedSkill, cand.care, cand.politeness, cand.stamina);

            if (open)
            {
                GUILayout.Space(4);
                GUILayout.Label($"Meslek: {Courier.OccupationLabel(cand.occupation)}", small);
                if (cand.occupation == Occupation.Ogrenci)
                    GUILayout.Label($"Bu dönem ders programı: {Courier.ScheduleText(cand.scheduleSeed, GameClock.I)}", small);
                if (cand.alsoOwnsBike) GUILayout.Label("Yedekte bisikleti de var.", small);
                KV("Hız", $"{v.tilesPerMinute * 60f * GameData.KmPerTile:0} km/saat");
                KV("Aynı anda", $"{v.capacity} paket");
                KV("Kazancı", $"{Ft(v.basePay)}/paket + {Ft(v.payPerKm)}/km");
                KV("Taşır", GameData.CarryText(v));
            }

            GUILayout.BeginHorizontal();
            var c = cand;
            GUI.enabled = modalFree && !up.CourierCapFull;
            if (GUILayout.Button(up.CourierCapFull ? "İşe al (hesaplar dolu)" : "İşe al", accentButton, GUILayout.MinWidth(120)))
                deferred = () => AskConfirm("Kuryeyi işe al",
                    $"{c.name} ({GameData.Vehicle(c.vehicle).name}, {Courier.OccupationLabel(c.occupation)}) işe alınsın mı? " +
                    $"Kurye hesapları {cm.Couriers.Count}/{up.CourierCap} → {cm.Couriers.Count + 1}/{up.CourierCap}. " +
                    $"Başlangıç kiti şirkete {Ft(GameData.StarterKitCost)}.",
                    "Evet, işe al", () => cm.Hire(c));
            GUI.enabled = modalFree;
            if (GUILayout.Button("Reddet", button, GUILayout.Width(96))) deferred = () => cm.Reject(c);
            GUILayout.FlexibleSpace();
            DetailButton(c);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        // ---------------------------------------------------------------- kartlar / tablo

        const string CourierViewKey = "KuryeTycoon.KuryeTablosu";
        static int courierViewCache = -1;

        /// <summary>Kuryeler tablo olarak mı gösterilsin (bu bilgisayarda kalıcı).</summary>
        static bool CourierTableView
        {
            get
            {
                if (courierViewCache < 0) courierViewCache = PlayerPrefs.GetInt(CourierViewKey, 0);
                return courierViewCache == 1;
            }
            set
            {
                courierViewCache = value ? 1 : 0;
                PlayerPrefs.SetInt(CourierViewKey, courierViewCache);
                PlayerPrefs.Save();
            }
        }

        void CourierViewToggle()
        {
            if (SelectButton(!CourierTableView, "Kartlar", smallButton, GUILayout.Width(74))) deferred = () => CourierTableView = false;
            if (SelectButton(CourierTableView, "Tablo", smallButton, GUILayout.Width(74))) deferred = () => CourierTableView = true;
        }

        enum CourierSort { Ad, Arac, Durum, Moral, Yorgunluk, Bugun, Kazanc }
        CourierSort courierSort = CourierSort.Durum;
        bool courierSortDesc;
        readonly List<Courier> tableRows = new List<Courier>();

        static readonly string[] SortNames = { "Ad", "Araç", "Durum", "Moral", "Yorg.", "Paket", "Kazanç" };
        static readonly float[] ColW = { 0f, 34f, 116f, 46f, 58f, 46f, 84f };   // 0 = kalan genişlik (isim)
        const float RowH = 34f;

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

        int CompareCouriers(Courier a, Courier b)
        {
            int r;
            switch (courierSort)
            {
                case CourierSort.Arac: r = a.vehicle.CompareTo(b.vehicle); break;
                case CourierSort.Durum: r = StateRank(a).CompareTo(StateRank(b)); break;
                case CourierSort.Moral: r = a.morale.CompareTo(b.morale); break;
                case CourierSort.Yorgunluk: r = a.fatigue.CompareTo(b.fatigue); break;
                case CourierSort.Bugun: r = a.deliveriesToday.CompareTo(b.deliveriesToday); break;
                case CourierSort.Kazanc: r = a.earnedToday.CompareTo(b.earnedToday); break;
                default: r = 0; break;
            }
            if (courierSortDesc) r = -r;
            if (r == 0) r = string.CompareOrdinal(a.courierName, b.courierName);
            if (r == 0) r = a.courierId.CompareTo(b.courierId);
            return r;
        }

        /// <summary>Sütun başlığına tıklayınca: aynı sütunsa yön değişir, değilse o sütuna göre (sayılar büyükten küçüğe).</summary>
        void SetCourierSort(CourierSort s)
        {
            if (courierSort == s) courierSortDesc = !courierSortDesc;
            else
            {
                courierSort = s;
                courierSortDesc = s == CourierSort.Yorgunluk || s == CourierSort.Bugun || s == CourierSort.Kazanc;
            }
        }

        void DrawCourierTable(List<Courier> couriers)
        {
            EnsureCardStyles();
            tableRows.Clear();
            tableRows.AddRange(couriers);
            tableRows.Sort(CompareCouriers);

            var mouse = Event.current.mousePosition;
            bool repaint = Event.current.type == EventType.Repaint;

            // Başlık
            var hr = GUILayoutUtility.GetRect(100f, 26f, GUILayout.ExpandWidth(true));
            var cols = TableColumns(hr);
            for (int i = 0; i < cols.Length; i++)
            {
                var cr = cols[i];
                var s = (CourierSort)i;
                bool sel = courierSort == s;
                bool hov = cr.Contains(mouse) && modalFree;
                if (repaint)
                {
                    if (hov) UITheme.Box(cr, UITheme.WithAlpha(Color.white, 0.06f), 6f);
                    tableHead.normal.textColor = UITheme.Yazi(sel ? UITheme.Text : UITheme.TextDim);
                    string t = SortNames[i] + (sel ? (courierSortDesc ? " ↓" : " ↑") : "");
                    GUI.Label(new Rect(cr.x + (i == 0 ? 36f : 4f), cr.y, cr.width - 6f, cr.height), t, tableHead);
                }
                if (GUI.Button(cr, GUIContent.none, GUIStyle.none)) { var ss = s; deferred = () => SetCourierSort(ss); }
            }
            if (repaint) Fill(new Rect(hr.x, hr.yMax - 1f, hr.width, 1f), UITheme.Line);

            for (int k = 0; k < tableRows.Count; k++)
            {
                var c = tableRows[k];
                bool open = CardOpen(c);
                var rr = GUILayoutUtility.GetRect(100f, RowH, GUILayout.ExpandWidth(true));
                bool hov = rr.Contains(mouse) && modalFree;
                if (repaint) DrawCourierRow(c, rr, k, hov, open);
                if (GUI.Button(rr, GUIContent.none, GUIStyle.none))
                {
                    var key = c;
                    deferred = () => { if (!openCards.Remove(key)) openCards.Add(key); };
                }
                if (open)
                {
                    DrawCourierCard(c, true);
                    GUILayout.Space(4);
                }
            }
        }

        /// <summary>Başlık ve satırlarda aynı sütun dikdörtgenleri. İlk sütun (isim) kalan genişliği alır.</summary>
        static Rect[] TableColumns(Rect row)
        {
            var cols = new Rect[ColW.Length];
            float fixedW = 0f;
            for (int i = 1; i < ColW.Length; i++) fixedW += ColW[i];
            float x = row.x;
            for (int i = 0; i < ColW.Length; i++)
            {
                float w = i == 0 ? Mathf.Max(90f, row.width - fixedW) : ColW[i];
                cols[i] = new Rect(x, row.y, w, row.height);
                x += w;
            }
            return cols;
        }

        void DrawCourierRow(Courier c, Rect rr, int index, bool hover, bool open)
        {
            if (open) UITheme.Box(rr, UITheme.CardHi, 8f);
            else if (hover) UITheme.Box(rr, UITheme.WithAlpha(Color.white, 0.07f), 8f);
            else if (index % 2 == 1) UITheme.Box(rr, UITheme.WithAlpha(Color.white, 0.025f), 8f);

            var cols = TableColumns(rr);
            float cy = rr.center.y;

            // İsim (profil resmiyle) ve eksik uyarısı
            var nc = cols[0];
            Portraits.Draw(new Rect(nc.x + 3f, cy - 13f, 26f, 26f), c.courierName, Portraits.MoodOf(c.morale), CompanyProfile.Primary);
            tableCell.normal.textColor = UITheme.Yazi(Color.white);
            string nm = c.ShortName;
            float nw = Mathf.Min(tableCell.CalcSize(new GUIContent(nm)).x, nc.width - 58f);
            GUI.Label(new Rect(nc.x + 36f, rr.y, nw + 2f, rr.height), nm, tableCell);
            int pens = c.ActivePenalties().Count;
            if (pens > 0 || c.cashBlocked)
            {
                var wr = new Rect(nc.x + 40f + nw, cy - 7f, 14f, 14f);
                Icons.Draw(wr, Icons.Kind.Warning, c.cashBlocked ? UITheme.Bad : UITheme.Warn);
                HoverTip(wr, c.cashBlocked ? "Hesabı kilitli: nakit borcunu kapatması gerekiyor." : $"{pens} eksik: ayrıntısı satıra tıklayınca.");
            }

            // Araç
            var vc = cols[1];
            Icons.Draw(new Rect(vc.x + 7f, cy - 9f, 18f, 18f), Icons.Of(c.vehicle), UITheme.Text);
            HoverTip(vc, c.V.name);

            // Durum
            var sc = cols[2];
            var stc = CourierStateColor(c);
            UITheme.Box(new Rect(sc.x + 4f, cy - 4f, 8f, 8f), stc, 4f);
            tableCell.normal.textColor = UITheme.Yazi(Color.Lerp(stc, Color.white, 0.35f));
            GUI.Label(new Rect(sc.x + 16f, rr.y, sc.width - 18f, rr.height), ShortState(c), tableCell);
            HoverTip(sc, c.StateName);

            // Moral
            var mc = cols[3];
            Icons.Draw(new Rect(mc.x + 12f, cy - 9f, 18f, 18f), Icons.Mood(c.morale), Icons.MoodColor(c.morale));
            HoverTip(mc, $"Moral %{c.morale * 100f:0}");

            // Yorgunluk
            var fc = cols[4];
            var bar = new Rect(fc.x + 4f, cy - 3f, fc.width - 14f, 6f);
            UITheme.Box(bar, new Color(0f, 0f, 0f, 0.45f), 3f);
            float fw = bar.width * Mathf.Clamp01(c.fatigue);
            if (fw > 1f) UITheme.Box(new Rect(bar.x, bar.y, Mathf.Max(6f, fw), bar.height), FatigueColor, 3f);
            HoverTip(fc, $"Yorgunluk %{c.fatigue * 100f:0}");

            // Bugünkü paket ve kazanç
            tableCell.normal.textColor = UITheme.Yazi(UITheme.Text);
            GUI.Label(new Rect(cols[5].x + 4f, rr.y, cols[5].width - 6f, rr.height), c.deliveriesToday.ToString(), tableCell);
            tableCell.normal.textColor = UITheme.Yazi(c.earnedToday > 0 ? UITheme.Text : UITheme.TextDim);
            GUI.Label(new Rect(cols[6].x + 4f, rr.y, cols[6].width - 6f, rr.height), Ft(c.earnedToday), tableCell);
            HoverTip(new Rect(cols[5].x, rr.y, cols[5].width + cols[6].width, rr.height), "Bugün teslim ettiği paket ve kazandığı para");
        }
    }
}
