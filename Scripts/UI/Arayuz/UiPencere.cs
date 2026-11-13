using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Sol menü ve pencere çerçevesi (UI Toolkit). Menü grupları ve sekmeler MenuGroups.cs'teki tanımlardan gelir;
    /// açık sekme, rozetler ve "yeni" noktaları GameUI'nin durumundan okunur (panelOpen, tab, newTabs).
    /// Pencerenin gövdesi: sekme UI Toolkit'e taşındıysa kaydırılabilir alanda çizilir; taşınmadıysa gövde saydam kalır
    /// ve içeriği IMGUI (DrawPanel) aynı yere çizer.
    /// </summary>
    public partial class GameUI
    {
        VisualElement dockKok;

        class DockOge
        {
            public int g;
            public VisualElement kok, simge, yeni;
            public Label yazi, rozet;
        }

        readonly List<DockOge> dockOgeleri = new List<DockOge>();

        void UiDockKur()
        {
            dockKok = U.El(katHud, "dock");
            for (int g = 0; g < MenuGroups.Length; g++)
            {
                if (IsSettingsGroup(g)) U.Gecirgen(dockKok, "bosluk");   // Ayarlar en altta
                int gg = g;
                var o = new DockOge { g = g };
                o.kok = U.Tikla(U.El(dockKok, "dock-oge"), () => OpenGroup(gg));
                o.simge = U.Simge(o.kok, MenuGroups[g].icon, U.Soluk, 0f, "dock-simge");
                o.yazi = U.Yazi(o.kok, MenuGroups[g].label, "dock-yazi");
                o.rozet = U.Kalin(o.kok, "", "rozet");
                o.yeni = U.Gecirgen(o.kok, "yeni-nokta");
                dockOgeleri.Add(o);
            }
        }

        void UiDockYenile(bool agir)
        {
            float sh = UiEkranYuksekligi;
            float step = GroupDockStep(sh);
            float boslukSol = MapLeftGui;
            if (boslukSol > DockW + 0.5f)
            {
                U.Goster(uiHaritaBosluk, true);
                uiHaritaBosluk.style.width = boslukSol - DockW;
            }
            else U.Goster(uiHaritaBosluk, false);

            foreach (var o in dockOgeleri)
            {
                bool gorunur = GroupVisible(o.g);
                U.Goster(o.kok, gorunur);
                if (!gorunur) continue;
                bool ayar = IsSettingsGroup(o.g);
                float h = ayar ? 62f : step - 6f;
                if (o.kok.style.height != h) o.kok.style.height = h;
                bool aktif = panelOpen && GroupIndexOf(tab) == o.g;
                if (aktif) U.Zemin(o.kok, UITheme.Accent); else ZeminSil(o.kok);
                float isz = Mathf.Clamp(h - 30f, 18f, 30f);
                if (o.simge.style.width != isz) { o.simge.style.width = isz; o.simge.style.height = isz; }
                U.SimgeAyarla(o.simge, MenuGroups[o.g].icon, aktif ? UITheme.AccentText : UITheme.TextDim);
                if (aktif) U.Renk(o.yazi, UITheme.AccentText); else RenkSil(o.yazi);
                if (!agir) continue;
                int rozet = GroupBadge(o.g);
                U.Goster(o.rozet, rozet > 0);
                if (rozet > 0) U.Metin(o.rozet, rozet > 99 ? "99+" : rozet.ToString());
                U.Goster(o.yeni, rozet == 0 && GroupHasNew(o.g));
            }
        }

        // ------------------------------------------------------------------ pencere çerçevesi

        VisualElement pencereKok, pencereSimgeKutu, pencereSimge, sekmeSerit, pencereGovde;
        Label pencereBaslik;
        ScrollView pencereKaydir;
        int sekmeSeritGrubu = -1;
        Tab? gosterilenSekme;

        class SekmeOge
        {
            public Tab t;
            public string ad;
            public Label l;
            public VisualElement yeni;
        }

        readonly List<SekmeOge> sekmeOgeleri = new List<SekmeOge>();

        void UiPencereKur()
        {
            pencereKok = U.Gecirgen(katPencere, "pencere");
            var ust = U.El(pencereKok, "pencere-ust");
            var bs = U.El(ust, "pencere-baslik-satir");
            pencereSimgeKutu = U.El(bs, "pencere-simge-kutu");
            pencereSimge = U.Simge(pencereSimgeKutu, Icons.Kind.Orders, Color.white, 18f);
            pencereBaslik = U.Kalin(bs, "", "pencere-baslik");
            var kapat = U.Tikla(U.El(bs, "simge-dugme"), () => panelOpen = false);
            U.Simge(kapat, Icons.Kind.Close, U.Soluk, 12f);
            Ipucu(kapat, "Kapat");
            sekmeSerit = U.El(ust, "sekmeler");
            U.El(ust, "pencere-cizgi");
            pencereGovde = U.El(pencereKok, "pencere-govde");
            pencereKaydir = new ScrollView(ScrollViewMode.Vertical);
            pencereKaydir.AddToClassList("kaydir");
            pencereKaydir.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            pencereGovde.Add(pencereKaydir);

            // UI Toolkit'e taşınan sekmeler (taşınmayanlar IMGUI ile çizilmeye devam eder)
            UiSekmeEkle(Tab.Siparisler, new UiSiparisler());
            UiSekmeEkle(Tab.Kuryeler, new UiKuryeler());
            UiSekmeEkle(Tab.Gelistirmeler, new UiGelisim());
            UiSekmeEkle(Tab.Hedefler, new UiHedefler());
        }

        void SekmeSeridiKur(int g)
        {
            sekmeSeritGrubu = g;
            sekmeSerit.Clear();
            sekmeOgeleri.Clear();
            var def = MenuGroups[g];
            for (int i = 0; i < def.tabs.Length; i++)
            {
                var so = new SekmeOge { t = def.tabs[i], ad = def.tabLabels[i] };
                var t = so.t;
                so.l = U.Kalin(sekmeSerit, so.ad, "sekme");
                U.Tikla(so.l, () => SelectSubTab(t));
                so.yeni = U.Gecirgen(so.l, "yeni-nokta");
                sekmeOgeleri.Add(so);
            }
        }

        void UiPencereYenile(bool agir)
        {
            bool acik = panelOpen;
            U.Goster(pencereKok, acik);
            if (!acik) { gosterilenSekme = null; return; }
            if (pencereKok.style.width != panelWidth) pencereKok.style.width = panelWidth;

            int g = GroupIndexOf(tab);
            var def = MenuGroups[g];
            U.Zemin(pencereSimgeKutu, UITheme.Accent);
            U.SimgeAyarla(pencereSimge, def.icon, UITheme.AccentText);
            U.Metin(pencereBaslik, def.title);

            if (g != sekmeSeritGrubu) SekmeSeridiKur(g);
            int acikSekme = 0;
            foreach (var so in sekmeOgeleri)
            {
                bool gor = TabOpen(so.t);
                U.Goster(so.l, gor);
                if (!gor) continue;
                acikSekme++;
                bool sec = so.t == tab;
                int rozet = BadgeOf(so.t);
                U.Metin(so.l, so.ad + (rozet > 0 ? $" ({rozet})" : ""));
                if (sec) { U.Zemin(so.l, UITheme.Accent); U.Renk(so.l, UITheme.AccentText); }
                else { ZeminSil(so.l); RenkSil(so.l); }
                U.Goster(so.yeni, !sec && newTabs.Contains(so.t));
            }
            U.Goster(sekmeSerit, acikSekme > 1);
            // Rehber (Tutorial.cs) sekmeyi vurgulamak için IMGUI'deki sekme dikdörtgenlerini kullanır
            subTabRects.Clear();
            if (acikSekme > 1)
                foreach (var so in sekmeOgeleri)
                    if (U.Gorunur(so.l) && so.l.worldBound.width > 1f) subTabRects[so.t] = so.l.worldBound;

            bool uitk = uiSekmeler.TryGetValue(tab, out var s);
            U.Sinif(pencereGovde, "imgui", !uitk);
            pencereGovde.pickingMode = uitk ? PickingMode.Position : PickingMode.Ignore;
            U.Goster(pencereKaydir, uitk);
            if (!uitk) { gosterilenSekme = null; return; }
            if (gosterilenSekme != tab)
            {
                pencereKaydir.Clear();
                pencereKaydir.Add(s.kok);
                pencereKaydir.scrollOffset = Vector2.zero;
                gosterilenSekme = tab;
                s.Acildi();
                s.Yenile();
            }
            else if (agir) s.Yenile();
        }

        /// <summary>Test modu: UI Toolkit penceresini aşağı kaydırır. Dönüş: kaydırılabildi mi.</summary>
        bool UiPencereKaydir(float dy)
        {
            if (!UiAktif || pencereKaydir == null || !UiSekmeVar(tab)) return false;
            var once = pencereKaydir.scrollOffset;
            pencereKaydir.scrollOffset = once + new Vector2(0f, dy);
            return pencereKaydir.scrollOffset.y > once.y + 0.5f;
        }
    }
}
