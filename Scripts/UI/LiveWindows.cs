using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Yeni pencereler:
    ///  • Mesajlar ve Yorumlar: kurye mesajları (bazıları karar ister) ve müşteri yorumları.
    ///  • Kendi Marketin: depo, reyon stokları, çeşit, otomatik stok, büyütme.
    ///  • İstatistikler: günlük / haftalık / aylık kâr, teslimat, iptal, itibar, kurye ve kasa grafikleri.
    ///  • Haritada şehir etkinliği işareti.
    /// </summary>
    public partial class GameUI
    {
        // ================================================================ Mesajlar

        int msgFilter;   // 0 tümü, 1 kurye, 2 yorum

        void DrawMessages()
        {
            var cmsg = CourierMessages.I;
            if (cmsg == null) return;
            Intro("mesajlar", "Kuryelerin çalışırken mesaj atar; bazıları senden karar ister (30 dakika cevap vermezsen kendi karar verirler). " +
                              "Müşteri yorumları da buraya düşer.",
                  "Kötü yorumların küçük bir kısmı sosyal medyada yayılabilir: itibar düşer, 2 gün siparişler azalır. " +
                  "Çok iyi yorumlar da yayılabilir. Yorumları iyileştirmenin yolu: zamanında, sıcak ve temiz çantayla teslimat.");

            if (cmsg.ViralActive)
            {
                bool good = cmsg.ViralFactor > 1f;
                GUILayout.Label(good ? "Viral övgü etkisi sürüyor: siparişler ~%10 fazla." : "Viral kötü yorum etkisi sürüyor: siparişler ~%10 az.", good ? note : warn);
            }
            if (cmsg.reviewsTotal > 0)
                GUILayout.Label($"Müşteri yorumları: {cmsg.reviewsTotal} yorum · ortalama {cmsg.AverageStars:0.0}★", header);

            GUILayout.BeginHorizontal();
            if (SelectButton(msgFilter == 0, "Tümü", smallButton)) msgFilter = 0;
            if (SelectButton(msgFilter == 1, "Kurye mesajları", smallButton)) msgFilter = 1;
            if (SelectButton(msgFilter == 2, "Yorumlar", smallButton)) msgFilter = 2;
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            if (cmsg.Messages.Count == 0)
            {
                GUILayout.Label("Henüz mesaj yok. Kuryeler çalıştıkça mesaj atar.", small);
                return;
            }

            // Cevap bekleyenler en üstte
            foreach (var m in cmsg.Messages) if (m.Pending && msgFilter != 2) DrawMessageCard(m);
            foreach (var m in cmsg.Messages)
            {
                if (m.Pending) continue;
                if (msgFilter == 1 && m.kind == PhoneKind.Yorum) continue;
                if (msgFilter == 2 && m.kind != PhoneKind.Yorum) continue;
                DrawMessageCard(m);
            }
            if (Event.current.type == EventType.Repaint && cmsg.Unread > 0) deferred += cmsg.MarkAllRead;
        }

        void DrawMessageCard(PhoneMessage m)
        {
            GUILayout.BeginVertical(box);
            GUILayout.BeginHorizontal();
            if (m.kind == PhoneKind.Yorum)
            {
                GUILayout.Label(m.from + (string.IsNullOrEmpty(m.shopName) ? "" : $" · {m.shopName}") + (m.viral ? "  · VİRAL" : ""), header);
                GUILayout.FlexibleSpace();
                Rect sr = GUILayoutUtility.GetRect(90, 18, GUILayout.Width(90));
                for (int i = 0; i < 5; i++)
                    Icons.Draw(new Rect(sr.x + i * 18f, sr.y + 1f, 16f, 16f), Icons.Kind.Star,
                               i < m.stars ? new Color(1f, 0.78f, 0.25f) : UITheme.WithAlpha(Color.white, 0.15f));
            }
            else
            {
                GUILayout.Label((m.Pending ? "● " : "") + m.from, header);
                GUILayout.FlexibleSpace();
            }
            GUILayout.Label(m.timeText, small, GUILayout.Width(44));
            GUILayout.EndHorizontal();

            GUILayout.Label(m.kind == PhoneKind.Yorum ? $"\"{m.text}\"" : m.text, label);
            if (m.Pending)
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < m.optionTexts.Count; i++)
                {
                    int k = i;
                    var msg = m;
                    if (GUILayout.Button(m.optionTexts[i], smallButton)) deferred += () => CourierMessages.I.Answer(msg, k);
                }
                GUILayout.EndHorizontal();
                float left = Mathf.Max(0f, m.expiresAt - GameClock.I.TotalMinutes);
                GUILayout.Label($"{left:0} dk içinde cevap vermezsen kurye kendi karar verir.", small);
            }
            else if (!string.IsNullOrEmpty(m.answer)) GUILayout.Label(m.answer, small);
            GUILayout.EndVertical();
        }

        // ================================================================ Kendi marketin

        void DrawMarket()
        {
            var ds = DarkStore.I;
            if (ds == null) return;
            Intro("market", "Kendi deponu açıp market siparişlerini kendin sat (Wolt Market gibi). Komisyon yok: satışın tamamı senin, " +
                            $"ama ürünleri sen alırsın. Brüt marj ~%{DarkStore.Margin * 100f:0}.",
                  "Stok alış fiyatıyla tutulur. Stokta olmayan ürünler siparişten çıkar; sepetin çoğu yoksa sipariş kaçar. " +
                  "Boş raflı markete müşteri az gelir. Taze ürün her gün ~%12 bozulur. Çeşit arttıkça daha çok sipariş gelir ama raf ve soğutma gideri artar. " +
                  "Depo büyüdükçe kira ve toplayıcı sayısı artar, stok kapasitesi ve talep de artar.");

            if (!ds.IsOpen)
            {
                GUILayout.BeginVertical(box);
                GUILayout.Label($"{DarkStore.StoreName} — {DarkStore.SizeNames[1]}", header);
                GUILayout.Label($"Açılış: {Ft(DarkStore.SizeCost[1])} (raf, soğutucu, tadilat) · günlük gider ~{Ft(DarkStore.SizeRent[1] + DarkStore.SizePickers[1] * DarkStore.PickerDaily + 5 * DarkStore.AssortDaily[2])} " +
                                $"(kira, {DarkStore.SizePickers[1]} toplayıcı, raflar) · ilk stok için ~{Ft(DarkStore.SizeCapacity[1] * 8 / 10)} daha gerekir.", small);
                GUILayout.Label("Depo merkeze yakın, açık bir bölgede kurulur. Kuryelerin siparişi depodan alır, hazırlık ~6 dk.", small);
                string lockReason = ds.LockReason;
                if (lockReason != "") GUILayout.Label(lockReason, warn);
                GUI.enabled = lockReason == "" && modalFree;
                if (GUILayout.Button($"Marketi aç — {Ft(DarkStore.SizeCost[1])}", bigButton, GUILayout.Height(38)))
                    deferred += () => AskConfirm($"{DarkStore.StoreName} açılsın mı?",
                        $"{Ft(DarkStore.SizeCost[1])} açılış bedeli serbest kasadan ödenir. Her gün kira, toplayıcı ve raf gideri çıkar. " +
                        "Açtıktan sonra raflar boş: stok almayı unutma (otomatik stok varsayılan açık).", "Aç", () => ds.Open());
                GUI.enabled = modalFree;
                GUILayout.EndVertical();
                return;
            }

            // Özet
            GUILayout.BeginVertical(box);
            GUILayout.Label($"{DarkStore.StoreName} · {DarkStore.SizeNames[ds.size]}", header);
            GUILayout.Label($"Yer: {CityMap.DistrictNames[CityMap.RegionOf(ds.shop.tile)]} · {GameClock.I.Day - ds.openedDay + 1}. gün · " +
                            $"{DarkStore.SizePickers[ds.size]} toplayıcı · günlük gider {Ft(ds.DailyCost)}", small);
            GUILayout.Label($"Bugün: {ds.ordersToday} sipariş · satış {Ft(ds.salesToday)} · brüt kâr {Ft(ds.profitToday)}" +
                            (ds.lostToday > 0 ? $" · stok yüzünden kaçan {ds.lostToday}" : ""), label);
            GUILayout.Label($"Toplam: {ds.ordersTotal} sipariş · satış {Ft(ds.salesTotal)} · brüt kâr {Ft(ds.profitTotal)} · bozulan {Ft(ds.spoiledTotal)}", small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Haritada göster", smallButton) && MapCamera.I != null) { var t = ds.shop.tile; deferred += () => MapCamera.I.FocusOn(t, 0.75f); }
            if (ds.size < 3)
            {
                long uc = DarkStore.SizeCost[ds.size + 1];
                if (GUILayout.Button($"Büyüt: {DarkStore.SizeNames[ds.size + 1]} — {Ft(uc)}", smallButton))
                    deferred += () => AskConfirm("Depo büyütülsün mü?",
                        $"{DarkStore.SizeNames[ds.size + 1]}: stok kapasitesi {Ft(DarkStore.SizeCapacity[ds.size + 1])}, {DarkStore.SizePickers[ds.size + 1]} toplayıcı, " +
                        $"kira {Ft(DarkStore.SizeRent[ds.size + 1])}/gün. Daha çok sipariş gelir.", "Büyüt", () => ds.Upgrade());
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // Stok
            GUILayout.Label($"Stok: {Ft(ds.TotalStock)} / {Ft(ds.Capacity)} (alış fiyatıyla)", header);
            for (int a = 0; a < DarkStore.Aisles; a++)
            {
                int aisle = a;
                GUILayout.BeginVertical(box);
                float fill = ds.Fill01(a);
                Bar(DarkStore.AisleNames[a], fill, fill < 0.25f ? UITheme.Bad : fill < 0.5f ? UITheme.Warn : UITheme.Good,
                    $"{Ft(ds.stock[a])} / {Ft(ds.AisleCapacity(a))}");
                GUILayout.BeginHorizontal();
                GUILayout.Label("Çeşit:", small, GUILayout.Width(44));
                for (int lv = 1; lv <= 3; lv++)
                {
                    int l = lv;
                    string txt = DarkStore.AssortNames[lv] + (DarkStore.AssortDaily[lv] > 0 ? $" (+{Ft(DarkStore.AssortDaily[lv])}/gün)" : "");
                    if (SelectButton(ds.assortment[a] == lv, txt, smallButton)) deferred += () => ds.SetAssortment(aisle, l);
                }
                GUILayout.FlexibleSpace();
                long need = System.Math.Max(0, ds.AisleCapacity(a) - ds.stock[a]);
                GUI.enabled = modalFree && need > 1000;
                if (GUILayout.Button($"Doldur ({Ft(need / 100 * 100)})", smallButton)) deferred += () => ds.Restock(aisle, 1f, true);
                GUI.enabled = modalFree;
                GUILayout.EndHorizontal();
                if (a == 1) GUILayout.Label("Taze ürün her gece ~%12 bozulur: fazla stok tutma.", small);
                GUILayout.EndVertical();
            }

            GUILayout.BeginHorizontal();
            long all = 0;
            for (int a = 0; a < DarkStore.Aisles; a++) all += System.Math.Max(0, ds.AisleCapacity(a) - ds.stock[a]);
            GUI.enabled = modalFree && all > 1000;
            if (GUILayout.Button($"Bütün reyonları doldur ({Ft(all / 100 * 100)})", button)) deferred += () => ds.RestockAll(1f, true);
            GUI.enabled = modalFree;
            GUILayout.EndHorizontal();

            ds.autoRestock = GUILayout.Toggle(ds.autoRestock, $" Otomatik stok: her sabah reyonları %{ds.restockTarget * 100f:0}'e tamamla (serbest kasadan)", toggle);
            if (ds.autoRestock)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Hedef doluluk", small, GUILayout.Width(100));
                ds.restockTarget = Mathf.Round(GUILayout.HorizontalSlider(ds.restockTarget, 0.4f, 1f) * 20f) / 20f;
                GUILayout.Label($"%{ds.restockTarget * 100f:0}", small, GUILayout.Width(40));
                GUILayout.EndHorizontal();
            }
        }

        /// <summary>Haritada kendi marketine tıklayınca (ve dükkan kartı yerine) kısa özet.</summary>
        void DrawOwnStoreSummary(bool withTitle)
        {
            var ds = DarkStore.I;
            if (ds == null || !ds.IsOpen) return;
            if (withTitle) GUILayout.Label($"{DarkStore.StoreName}  [Senin]", header);
            GUILayout.Label($"Kendi deponun marketi · {DarkStore.SizeNames[ds.size]} · komisyon yok, brüt marj ~%{DarkStore.Margin * 100f:0}", small);
            GUILayout.Label($"Stok {Ft(ds.TotalStock)} / {Ft(ds.Capacity)} · bugün {ds.ordersToday} sipariş, brüt kâr {Ft(ds.profitToday)}", small);
            if (GUILayout.Button("Market penceresini aç", smallButton)) deferred += () => { CloseInfo(); OpenTab(Tab.Market); };
        }

        // ================================================================ İstatistikler

        int statPeriod;   // 0 günlük, 1 haftalık, 2 aylık

        class StatRow
        {
            public string label;
            public int days, deliveries, cancelled, lost;
            public long net, income, expenses, cash;
            public float reputation;
            public int couriers, shops;
        }

        List<StatRow> BuildStats(List<DayRecord> days, int mode)
        {
            var rows = new List<StatRow>();
            StatRow cur = null;
            string curKey = null;
            foreach (var d in days)
            {
                string key;
                string lbl;
                if (mode == 0) { key = d.companyDay.ToString(); lbl = ShortDate(d.date); }
                else if (mode == 1) { int w = (d.companyDay - 1) / 7 + 1; key = w.ToString(); lbl = $"{w}. hafta"; }
                else { key = MonthOf(d.date); lbl = key; }
                if (cur == null || key != curKey)
                {
                    cur = new StatRow { label = lbl };
                    rows.Add(cur);
                    curKey = key;
                }
                cur.days++;
                cur.deliveries += d.deliveries;
                cur.cancelled += d.cancelled;
                cur.lost += d.lost;
                cur.net += d.net;
                cur.income += d.income;
                cur.expenses += d.expenses;
                cur.cash = d.cash;
                cur.reputation = d.reputation;
                cur.couriers = d.couriers;
                cur.shops = d.contractedShops;
            }
            int max = mode == 0 ? 30 : mode == 1 ? 16 : 12;
            if (rows.Count > max) rows.RemoveRange(0, rows.Count - max);
            return rows;
        }

        static string ShortDate(string date)
        {
            if (string.IsNullOrEmpty(date)) return "";
            int comma = date.IndexOf(',');
            return comma > 0 ? date.Substring(0, comma) : date;
        }

        static string MonthOf(string date)
        {
            string s = ShortDate(date);
            int sp = s.IndexOf(' ');
            return sp >= 0 ? s.Substring(sp + 1) : s;
        }

        void DrawStatistics()
        {
            var h = StatsHistory.I;
            if (h == null) return;
            Intro("istatistik", "Şirketinin gidişatı: kâr, teslimat, iptal, itibar, kurye ve kasa. Günlük, haftalık ya da aylık bak.",
                  "Veriler her gece gün sonunda kaydedilir. Haftalık ve aylık görünümde değerler toplanır (itibar, kurye ve kasa dönem sonundaki değerdir). " +
                  "Çubukların üstüne gelince değeri görürsün.");
            GUILayout.BeginHorizontal();
            if (SelectButton(statPeriod == 0, "Günlük", button)) statPeriod = 0;
            if (SelectButton(statPeriod == 1, "Haftalık", button)) statPeriod = 1;
            if (SelectButton(statPeriod == 2, "Aylık", button)) statPeriod = 2;
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            if (h.Days.Count == 0)
            {
                GUILayout.Label("Grafikler ilk gün sonundan sonra görünür.", small);
                return;
            }
            var rows = BuildStats(h.Days, statPeriod);
            string unit = statPeriod == 0 ? "gün" : statPeriod == 1 ? "hafta" : "ay";

            // Özet
            long totalNet = 0, totalIncome = 0, totalExp = 0;
            int totalDel = 0, totalCancel = 0, totalDays = 0;
            StatRow best = null;
            foreach (var r in rows)
            {
                totalNet += r.net; totalIncome += r.income; totalExp += r.expenses;
                totalDel += r.deliveries; totalCancel += r.cancelled + r.lost; totalDays += r.days;
                if (best == null || r.net > best.net) best = r;
            }
            GUILayout.BeginVertical(box);
            GUILayout.Label($"Son {rows.Count} {unit} ({totalDays} gün)", header);
            GUILayout.Label($"Net kâr {Ft(totalNet)} · günlük ortalama {Ft(totalDays > 0 ? totalNet / totalDays : 0)} · gelir {Ft(totalIncome)} · gider {Ft(totalExp)}", label);
            float cancelRate = totalDel + totalCancel > 0 ? totalCancel / (float)(totalDel + totalCancel) : 0f;
            GUILayout.Label($"{totalDel} teslimat (günde ~{(totalDays > 0 ? totalDel / (float)totalDays : 0f):0.#}) · iptal/kaçan {totalCancel} (%{cancelRate * 100f:0.#})" +
                            (best != null ? $" · en iyi {unit}: {best.label} ({Ft(best.net)})" : ""), small);
            GUILayout.EndVertical();
            GUILayout.Space(4);

            var labels = rows.ConvertAll(r => r.label);
            StatChart("Net kâr", rows.ConvertAll(r => (float)r.net), labels, UITheme.Good, UITheme.Bad, true);
            StatChart("Gelir", rows.ConvertAll(r => (float)r.income), labels, new Color(0.45f, 0.75f, 1f), UITheme.Bad, true);
            StatChart("Gider", rows.ConvertAll(r => (float)r.expenses), labels, new Color(1f, 0.6f, 0.35f), UITheme.Bad, true);
            StatChart("Teslimat", rows.ConvertAll(r => (float)r.deliveries), labels, new Color(0.4f, 0.75f, 1f), UITheme.Bad, false);
            StatChart("İptal + kaçan sipariş", rows.ConvertAll(r => (float)(r.cancelled + r.lost)), labels, UITheme.Bad, UITheme.Bad, false);
            StatLine("İtibar (dönem sonu)", rows.ConvertAll(r => r.reputation), labels, 1f, 5f, new Color(1f, 0.8f, 0.3f), "0.00");
            StatLine("Kurye sayısı", rows.ConvertAll(r => (float)r.couriers), labels, 0f, -1f, new Color(0.7f, 0.6f, 1f), "0");
            StatLine("Kasa (dönem sonu)", rows.ConvertAll(r => (float)r.cash), labels, 0f, -1f, new Color(0.35f, 0.85f, 0.75f), "money");
        }

        GUIStyle chartTip;

        /// <summary>Etiketli çubuk grafik: fareyle üstüne gelince değer görünür.</summary>
        void StatChart(string title, List<float> values, List<string> labels, Color pos, Color neg, bool money)
        {
            float maxAbs = 0f; bool hasNeg = false;
            foreach (var v in values) { maxAbs = Mathf.Max(maxAbs, Mathf.Abs(v)); if (v < 0f) hasNeg = true; }
            float last = values.Count > 0 ? values[values.Count - 1] : 0f;
            GUILayout.Label($"{title} — son: {(money ? Ft((long)last) : last.ToString("0"))}", small);
            Rect r = GUILayoutUtility.GetRect(100, 70, GUILayout.ExpandWidth(true));
            Rect lr = GUILayoutUtility.GetRect(100, 14, GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint) return;
            UITheme.Box(r, new Color(0f, 0f, 0f, 0.25f), 6f);
            if (values.Count == 0 || maxAbs <= 0f) return;
            float zeroY = hasNeg ? r.y + r.height / 2f : r.yMax - 2f;
            float scale = (hasNeg ? r.height / 2f - 3f : r.height - 5f) / maxAbs;
            float bw = r.width / values.Count;
            int hover = -1;
            var mouse = Event.current.mousePosition;
            for (int i = 0; i < values.Count; i++)
            {
                var col = new Rect(r.x + i * bw, r.y, bw, r.height);
                if (col.Contains(mouse)) hover = i;
                float hgt = Mathf.Max(1f, Mathf.Abs(values[i]) * scale);
                var bar = values[i] >= 0f
                    ? new Rect(r.x + i * bw + 1f, zeroY - hgt, Mathf.Max(1f, bw - 2f), hgt)
                    : new Rect(r.x + i * bw + 1f, zeroY, Mathf.Max(1f, bw - 2f), hgt);
                var c = values[i] >= 0f ? pos : neg;
                if (hover >= 0 && hover != i) c = UITheme.WithAlpha(c, 0.55f);
                Fill(bar, c);
            }
            if (hasNeg) Fill(new Rect(r.x, zeroY - 0.5f, r.width, 1f), new Color(1f, 1f, 1f, 0.35f));
            ChartAxisLabels(lr, labels);
            if (hover >= 0) ChartTip(r, hover, bw, $"{labels[hover]}: {(money ? Ft((long)values[hover]) : values[hover].ToString("0"))}");
        }

        /// <summary>Etiketli çizgi grafik. max &lt; min ise ölçek veriden alınır.</summary>
        void StatLine(string title, List<float> values, List<string> labels, float min, float max, Color color, string fmt)
        {
            if (max < min)
            {
                min = float.MaxValue; max = float.MinValue;
                foreach (var v in values) { min = Mathf.Min(min, v); max = Mathf.Max(max, v); }
                if (values.Count == 0) { min = 0f; max = 1f; }
                float pad = Mathf.Max(1f, (max - min) * 0.15f);
                min -= pad; max += pad;
            }
            float last = values.Count > 0 ? values[values.Count - 1] : 0f;
            System.Func<float, string> f = v => fmt == "money" ? Ft((long)v) : v.ToString(fmt);
            GUILayout.Label($"{title} — son: {f(last)}", small);
            Rect r = GUILayoutUtility.GetRect(100, 56, GUILayout.ExpandWidth(true));
            Rect lr = GUILayoutUtility.GetRect(100, 14, GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint) return;
            UITheme.Box(r, new Color(0f, 0f, 0f, 0.25f), 6f);
            if (values.Count == 0 || max <= min) return;
            float bw = r.width / values.Count;
            Vector2 prev = Vector2.zero;
            int hover = -1;
            var mouse = Event.current.mousePosition;
            for (int i = 0; i < values.Count; i++)
            {
                if (new Rect(r.x + i * bw, r.y, bw, r.height).Contains(mouse)) hover = i;
                float t = Mathf.InverseLerp(min, max, values[i]);
                var p = new Vector2(r.x + i * bw + bw / 2f, r.yMax - 4f - t * (r.height - 8f));
                if (i > 0) DrawSegment(prev, p, color, 2f);
                prev = p;
            }
            for (int i = 0; i < values.Count; i++)
            {
                float t = Mathf.InverseLerp(min, max, values[i]);
                var p = new Vector2(r.x + i * bw + bw / 2f, r.yMax - 4f - t * (r.height - 8f));
                float s = i == hover ? 7f : 4f;
                Fill(new Rect(p.x - s / 2f, p.y - s / 2f, s, s), color);
            }
            ChartAxisLabels(lr, labels);
            if (hover >= 0) ChartTip(r, hover, bw, $"{labels[hover]}: {f(values[hover])}");
        }

        static void DrawSegment(Vector2 a, Vector2 b, Color c, float width)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 0.5f) return;
            var m = GUI.matrix;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(ang, a);
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2f, len, width), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.matrix = m;
        }

        void ChartAxisLabels(Rect lr, List<string> labels)
        {
            if (labels.Count == 0) return;
            small.normal.textColor = UITheme.TextDim;
            var oldAlign = small.alignment;
            small.alignment = TextAnchor.UpperLeft;
            GUI.Label(new Rect(lr.x, lr.y - 2f, lr.width / 2f, 16f), labels[0], small);
            small.alignment = TextAnchor.UpperRight;
            GUI.Label(new Rect(lr.x + lr.width / 2f, lr.y - 2f, lr.width / 2f, 16f), labels[labels.Count - 1], small);
            small.alignment = oldAlign;
        }

        void ChartTip(Rect r, int i, float bw, string text)
        {
            if (chartTip == null) chartTip = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            chartTip.normal.textColor = UITheme.Text;
            var size = chartTip.CalcSize(new GUIContent(text));
            float x = Mathf.Clamp(r.x + i * bw + bw / 2f - size.x / 2f - 6f, r.x, r.xMax - size.x - 12f);
            var tr = new Rect(x, r.y + 2f, size.x + 12f, 18f);
            UITheme.Box(tr, new Color(0.05f, 0.055f, 0.07f, 0.95f), 5f);
            GUI.Label(tr, text, chartTip);
        }

        // ================================================================ Haritada şehir etkinliği

        GUIStyle eventPill;

        void DrawCityEventMarker(float sw)
        {
            var ce = CityEvents.I;
            if (ce == null || Event.current.type != EventType.Repaint) return;
            var e = ce.ActiveNow;
            if (e == null) return;
            var cam = Camera.main;
            if (eventPill == null) eventPill = new GUIStyle(small) { alignment = TextAnchor.MiddleLeft, wordWrap = false, fontStyle = FontStyle.Bold };
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            var col = new Color(0.40f, 0.70f, 1f);

            if (e.HasSpot && cam != null && CityMap.I.IsUnlocked(e.Spot))
            {
                var c = ToGui(cam, CityMap.ToWorld(e.Spot));
                var edge = ToGui(cam, CityMap.ToWorld(e.Spot) + new Vector3(e.spotRadius, 0f, 0f));
                float rad = Mathf.Max(18f, Vector2.Distance(c, edge));
                UITheme.Outline(new Rect(c.x - rad, c.y - rad, rad * 2f, rad * 2f), UITheme.WithAlpha(col, 0.35f + pulse * 0.3f), 2f, rad);
                float d = 12f + pulse * 4f;
                UITheme.Box(new Rect(c.x - d / 2f, c.y - d / 2f, d, d), UITheme.WithAlpha(col, 0.85f), d / 2f);
                Icons.Draw(new Rect(c.x - 7f, c.y - 7f, 14f, 14f), Icons.Kind.Calendar, Color.white);
                eventPill.normal.textColor = Color.white;
                var sz = eventPill.CalcSize(new GUIContent(e.title));
                var pr = new Rect(c.x - sz.x / 2f - 8f, c.y - d / 2f - 26f, sz.x + 16f, 20f);
                UITheme.Box(pr, new Color(0.05f, 0.08f, 0.14f, 0.85f), 10f);
                GUI.Label(new Rect(pr.x + 8f, pr.y, sz.x + 4f, 20f), e.title, eventPill);
            }

            // Sağ üstte etkinlik rozeti (alanı olmayan etkinlikler de görünsün)
            string txt = $"{e.title} · {e.TimeText}";
            eventPill.normal.textColor = UITheme.Text;
            var s2 = eventPill.CalcSize(new GUIContent(txt));
            var br = new Rect(sw - s2.x - 60f, TopBar + 10f, s2.x + 44f, 26f);
            UITheme.Shadowed(br, new Color(0.07f, 0.09f, 0.13f, 0.92f), 13f, 3f);
            Icons.Draw(new Rect(br.x + 10f, br.y + 5f, 16f, 16f), Icons.Kind.Calendar, col);
            GUI.Label(new Rect(br.x + 32f, br.y, s2.x + 6f, 26f), txt, eventPill);
        }
    }
}
