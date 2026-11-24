using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Sade menü: sol çubukta 6 ana menü, her birinin içinde sekmeler (Two Point / Game Dev Tycoon tarzı).
    ///   Siparişler · Mesajlar · Hedefler · Ekip (Kuryeler, Kayıtlar, Ekipman, Destek)
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
            new MenuGroupDef { label = "Hedefler", title = "Hedefler", icon = Icons.Kind.Flag, tabs = new[] { Tab.Hedefler, Tab.Hikaye, Tab.Basarimlar }, tabLabels = new[] { "Haftalık", "Hikâye", "Başarımlar" } },
            new MenuGroupDef
            {
                label = "Ekip", title = "Ekip", icon = Icons.Kind.Couriers,
                tabs = new[] { Tab.Kuryeler, Tab.IseAlim, Tab.Ekipman, Tab.Destek, Tab.Efsaneler },
                tabLabels = new[] { "Kuryeler", "Kayıtlar", "Ekipman", "Destek", "Efsaneler" }
            },
            new MenuGroupDef
            {
                label = "İşletme", title = "İşletme", icon = Icons.Kind.Shops,
                tabs = new[] { Tab.Dukkanlar, Tab.Bolgeler, Tab.Market, Tab.Mutfak, Tab.Kampanyalar },
                tabLabels = new[] { "Dükkanlar", "Bölgeler", "Market", "Mutfak", "Kampanya" }
            },
            new MenuGroupDef
            {
                label = "Şirket", title = "Şirket", icon = Icons.Kind.Finance,
                tabs = new[] { Tab.Finans, Tab.Gelistirmeler, Tab.ArGe, Tab.Holding, Tab.Istatistik, Tab.Analitik },
                tabLabels = new[] { "Finans", "Gelişim", "Ar-Ge", "Holding", "Grafik", "Analitik" }
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

        // ---------------------------------------------------------------- pencere başlığı ve sekmeler

        readonly Dictionary<Tab, Rect> subTabRects = new Dictionary<Tab, Rect>();

        /// <summary>Pencere başlığının yüksekliği: tek sekmeli menüde 50, sekmeliyse sekme şeridi eklenir.</summary>
        float WindowHeaderHeight()
        {
            var def = MenuGroups[GroupIndexOf(tab)];
            int n = 0;
            foreach (var t in def.tabs) if (TabOpen(t)) n++;
            return n > 1 ? 92f : 50f;
        }
    }
}
