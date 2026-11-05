using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Sade menü: sol çubukta 6 ana menü, her birinin içinde sekmeler (Two Point / Game Dev Tycoon tarzı).
    ///   Siparişler · Mesajlar · Hedefler · Ekip (Kuryeler, İşe Alım, Ekipman, Destek)
    ///   İşletme (Dükkanlar, Market, Kampanya) · Şirket (Finans, Gelişim, Grafik) · en altta Ayarlar
    /// Henüz açılmamış sekme görünmez ("Kilitli" yazısı yok); açılınca sekmede ve menüde "yeni" noktası çıkar.
    /// Pencerelerin içi (DrawOrders, DrawCouriers...) değişmedi; sadece nasıl açıldıkları değişti.
    /// </summary>
    public partial class GameUI
    {
        class MenuGroupDef
        {
            public string label;      // sol çubukta
            public string title;      // pencere başlığında
            public Icons.Kind icon;
            public Tab[] tabs;
            public string[] tabLabels;
        }

        static readonly MenuGroupDef[] MenuGroups =
        {
            new MenuGroupDef { label = "Siparişler", title = "Siparişler", icon = Icons.Kind.Orders, tabs = new[] { Tab.Siparisler }, tabLabels = new[] { "Siparişler" } },
            new MenuGroupDef { label = "Hedefler", title = "Haftalık Hedefler", icon = Icons.Kind.Flag, tabs = new[] { Tab.Hedefler }, tabLabels = new[] { "Hedefler" } },
            new MenuGroupDef
            {
                label = "Ekip", title = "Ekip", icon = Icons.Kind.Couriers,
                tabs = new[] { Tab.Kuryeler, Tab.IseAlim, Tab.Ekipman, Tab.Destek },
                tabLabels = new[] { "Kuryeler", "İşe Alım", "Ekipman", "Destek" }
            },
            new MenuGroupDef
            {
                label = "İşletme", title = "İşletme", icon = Icons.Kind.Shops,
                tabs = new[] { Tab.Dukkanlar, Tab.Market, Tab.Kampanyalar },
                tabLabels = new[] { "Dükkanlar", "Market", "Kampanya" }
            },
            new MenuGroupDef
            {
                label = "Şirket", title = "Şirket", icon = Icons.Kind.Finance,
                tabs = new[] { Tab.Finans, Tab.Gelistirmeler, Tab.Istatistik },
                tabLabels = new[] { "Finans", "Gelişim", "Grafik" }
            },
            new MenuGroupDef { label = "Ayarlar", title = "Ayarlar", icon = Icons.Kind.Settings, tabs = new[] { Tab.Ayarlar }, tabLabels = new[] { "Ayarlar" } },
        };

        static int GroupIndexOf(Tab t)
        {
            for (int g = 0; g < MenuGroups.Length; g++)
                foreach (var gt in MenuGroups[g].tabs)
                    if (gt == t) return g;
            return 0;
        }

        static bool IsSettingsGroup(int g) => MenuGroups[g].tabs[0] == Tab.Ayarlar;

        /// <summary>Menüde en az bir sekme açıksa menü görünür (Mesajlar ilk mesaja kadar gizli).</summary>
        static bool GroupVisible(int g)
        {
            foreach (var t in MenuGroups[g].tabs) if (TabOpen(t)) return true;
            return false;
        }

        readonly Dictionary<int, Tab> lastTabOfGroup = new Dictionary<int, Tab>();

        /// <summary>Menü simgesine tıklayınca: aynı menü açıksa kapanır, değilse son bakılan (açık) sekmesi açılır.</summary>
        void OpenGroup(int g)
        {
            if (panelOpen && GroupIndexOf(tab) == g) { panelOpen = false; return; }
            Tab target = MenuGroups[g].tabs[0];
            bool found = false;
            if (lastTabOfGroup.TryGetValue(g, out var last) && TabOpen(last)) { target = last; found = true; }
            if (!found)
                foreach (var t in MenuGroups[g].tabs)
                    if (TabOpen(t)) { target = t; break; }
            tab = target;
            panelOpen = true;
            scroll = Vector2.zero;
        }

        /// <summary>Pencere içindeki sekme seçimi (aynı menüde).</summary>
        void SelectSubTab(Tab t)
        {
            if (tab == t) return;
            tab = t;
            scroll = Vector2.zero;
        }

        int GroupBadge(int g)
        {
            int n = 0;
            foreach (var t in MenuGroups[g].tabs) if (TabOpen(t)) n += BadgeOf(t);
            return n;
        }

        // ---------------------------------------------------------------- "yeni" işareti

        readonly HashSet<Tab> newTabs = new HashSet<Tab>();
        readonly HashSet<Tab> seenOpen = new HashSet<Tab>();
        bool newTabsReady;

        /// <summary>LateUpdate'te: yeni açılan sekmeyi "yeni" diye işaretler; bakılınca işaret kalkar.</summary>
        void UpdateMenuGroups()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            foreach (Tab t in System.Enum.GetValues(typeof(Tab)))
            {
                if (!TabOpen(t)) continue;
                if (seenOpen.Add(t) && newTabsReady) newTabs.Add(t);
            }
            newTabsReady = true;   // ilk turda açık olanlar "yeni" sayılmaz (yeni oyun / kayıt yükleme)
            if (panelOpen)
            {
                newTabs.Remove(tab);
                lastTabOfGroup[GroupIndexOf(tab)] = tab;
            }
        }

        bool GroupHasNew(int g)
        {
            foreach (var t in MenuGroups[g].tabs) if (newTabs.Contains(t)) return true;
            return false;
        }

        // ---------------------------------------------------------------- sol çubuk

        int VisibleDockGroups()
        {
            int n = 0;
            for (int g = 0; g < MenuGroups.Length; g++) if (!IsSettingsGroup(g) && GroupVisible(g)) n++;
            return n;
        }

        float GroupDockStep(float sh)
        {
            float avail = sh - TopBar - 10f - 84f;
            return Mathf.Clamp(avail / Mathf.Max(1, VisibleDockGroups()), 44f, 70f);
        }

        /// <summary>Menünün sol çubuktaki kutusu (çizimle aynı diziliş).</summary>
        Rect GroupDockRect(int g, float sh)
        {
            if (IsSettingsGroup(g)) return new Rect(7f, sh - 74f, DockW - 14f, 62f);
            float y = TopBar + 10f, step = GroupDockStep(sh);
            for (int i = 0; i < MenuGroups.Length; i++)
            {
                if (IsSettingsGroup(i) || !GroupVisible(i)) continue;
                if (i == g) return new Rect(7f, y, DockW - 14f, step - 6f);
                y += step;
            }
            return Rect.zero;
        }

        void DrawGroupDock(float sh)
        {
            var mouse = Event.current.mousePosition;
            for (int g = 0; g < MenuGroups.Length; g++)
            {
                if (!GroupVisible(g)) continue;
                var def = MenuGroups[g];
                var r = GroupDockRect(g, sh);
                bool active = panelOpen && GroupIndexOf(tab) == g;
                bool hover = r.Contains(mouse) && modalFree;

                if (active) UITheme.Box(r, UITheme.Accent, 14f);
                else if (hover) UITheme.Box(r, UITheme.CardHi, 14f);

                var iconCol = active ? UITheme.AccentText : (hover ? UITheme.Text : UITheme.TextDim);
                float isz = Mathf.Clamp(r.height - 30f, 18f, 30f);
                Icons.Draw(new Rect(r.center.x - isz / 2f, r.y + (r.height - isz - 18f) / 2f, isz, isz), def.icon, iconCol);
                dockLabel.normal.textColor = iconCol;
                GUI.Label(new Rect(r.x - 4f, r.yMax - 19f, r.width + 8f, 16f), def.label, dockLabel);

                int badge = GroupBadge(g);
                if (badge > 0)
                {
                    var br = new Rect(r.xMax - 22f, r.y + 3f, 20f, 18f);
                    UITheme.Box(br, UITheme.Bad, 9f);
                    dockLabel.normal.textColor = Color.white;
                    GUI.Label(new Rect(br.x, br.y + 1f, br.width, br.height), badge > 99 ? "99+" : badge.ToString(), dockLabel);
                }
                else if (GroupHasNew(g))
                    UITheme.Box(new Rect(r.xMax - 14f, r.y + 6f, 9f, 9f), new Color(1f, 0.82f, 0.25f), 4.5f);   // yeni sekme var

                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { int gg = g; deferred = () => OpenGroup(gg); }
            }
        }

        // ---------------------------------------------------------------- pencere başlığı ve sekmeler

        readonly Dictionary<Tab, Rect> subTabRects = new Dictionary<Tab, Rect>();
        GUIStyle subTabStyle;

        /// <summary>Pencere başlığının yüksekliği: tek sekmeli menüde 50, sekmeliyse sekme şeridi eklenir.</summary>
        float WindowHeaderHeight()
        {
            var def = MenuGroups[GroupIndexOf(tab)];
            int n = 0;
            foreach (var t in def.tabs) if (TabOpen(t)) n++;
            return n > 1 ? 92f : 50f;
        }

        /// <summary>Başlık (menü adı) ve altında sekme şeridi. Sekmeler aynı genişlikte; seçili olan şirket renginde.</summary>
        void DrawWindowHeader(Rect w)
        {
            var mouse = Event.current.mousePosition;
            int g = GroupIndexOf(tab);
            var def = MenuGroups[g];

            UITheme.Box(new Rect(w.x + 14f, w.y + 11f, 28f, 28f), UITheme.Accent, 8f);
            Icons.Draw(new Rect(w.x + 19f, w.y + 16f, 18f, 18f), def.icon, UITheme.AccentText);
            windowTitle.normal.textColor = UITheme.Text;
            GUI.Label(new Rect(w.x + 52f, w.y, w.width - 110f, 50f), def.title, windowTitle);

            var close = new Rect(w.xMax - 42f, w.y + 11f, 28f, 28f);
            bool closeHover = close.Contains(mouse);
            if (closeHover) UITheme.Box(close, UITheme.CardHi, 8f);
            Icons.Draw(new Rect(close.x + 8f, close.y + 8f, 12f, 12f), Icons.Kind.Close, closeHover ? UITheme.Text : UITheme.TextDim);
            if (GUI.Button(close, GUIContent.none, GUIStyle.none)) deferred = () => panelOpen = false;

            subTabRects.Clear();
            var open = new List<int>();
            for (int i = 0; i < def.tabs.Length; i++) if (TabOpen(def.tabs[i])) open.Add(i);
            float hh = WindowHeaderHeight();
            if (open.Count > 1)
            {
                if (subTabStyle == null)
                    subTabStyle = new GUIStyle(label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
                // Sekme şeridi: koyu zemin üstünde eşit parçalar
                var strip = new Rect(w.x + 12f, w.y + 50f, w.width - 24f, 34f);
                UITheme.Box(strip, new Color(0f, 0f, 0f, 0.28f), 10f);
                float pw = (strip.width - 6f) / open.Count;
                for (int k = 0; k < open.Count; k++)
                {
                    var t = def.tabs[open[k]];
                    var pr = new Rect(strip.x + 3f + k * pw, strip.y + 3f, pw - 2f, strip.height - 6f);
                    subTabRects[t] = pr;
                    bool sel = t == tab;
                    bool hov = pr.Contains(mouse) && modalFree;
                    if (sel) UITheme.Box(pr, UITheme.Accent, 8f);
                    else if (hov) UITheme.Box(pr, UITheme.WithAlpha(Color.white, 0.07f), 8f);
                    int badge = BadgeOf(t);
                    string txt = def.tabLabels[open[k]] + (badge > 0 ? $" ({badge})" : "");
                    subTabStyle.normal.textColor = sel ? UITheme.AccentText : (hov ? UITheme.Text : UITheme.TextDim);
                    GUI.Label(pr, txt, subTabStyle);
                    if (newTabs.Contains(t) && !sel) UITheme.Box(new Rect(pr.xMax - 12f, pr.y + 5f, 7f, 7f), new Color(1f, 0.82f, 0.25f), 3.5f);
                    if (GUI.Button(pr, GUIContent.none, GUIStyle.none)) { var tt = t; deferred = () => SelectSubTab(tt); }
                }
            }
            Fill(new Rect(w.x + 12f, w.y + hh, w.width - 24f, 1f), UITheme.Line);
        }
    }
}
