using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Haritanın üstündeki UI Toolkit parçaları:
    ///  • Alt sol araç çubuğu: yakınlaş/uzaklaş, döndür, tüm harita, lejant, 3D/düz, harita katmanı menüsü ve açıklaması.
    ///  • Sağ alt "Olaylar" kartı: üstte tıklanabilir Dikkat listesi (UyariListesi.cs), altında son olaylar (eski akış). Tek yerde.
    ///  • Sol üst haftalık hedef kutusu (pencere kapalıyken).
    /// </summary>
    public partial class GameUI
    {
        // ------------------------------------------------------------------ harita araç çubuğu

        VisualElement aracKok, katmanMenu, katmanDugme;
        Label katmanDugmeYazi, lejantYazi, gorunumYazi, katmanNot;
        readonly List<(VisualElement kok, Label ad, HeatMode m)> katmanOgeleri = new List<(VisualElement, Label, HeatMode)>();

        static readonly string[] KatmanAciklamalari =
        {
            "Sadece şehir", "Son 7 gün: nereden sipariş geliyor", "İptal, pahalı bulan, yoğunlukta kaçan",
            "Hangi mahalleye geç gidiliyor", "Şu an müsait kuryeye uzaklık", "Dükkan başına günlük kâr",
        };

        VisualElement AracEkle(VisualElement grup, string yazi, System.Action tik, string ipucu, Icons.Kind? simge = null)
        {
            var a = U.Tikla(U.El(grup, "arac"), tik);
            if (simge.HasValue) U.Simge(a, simge.Value, Color.white, 0f, "arac-simge");
            if (!string.IsNullOrEmpty(yazi))
            {
                var l = U.Kalin(a, yazi, "arac-yazi");
                if (simge.HasValue) l.AddToClassList("simgeli");
            }
            if (!string.IsNullOrEmpty(ipucu)) Ipucu(a, ipucu);
            return a;
        }

        void UiHaritaKur()
        {
            aracKok = U.Gecirgen(katHud, "harita-arac");
            aracKok.style.left = DockW + 10f;

            var g1 = U.El(aracKok, "arac-grup");
            AracEkle(g1, "+", () => MapCamera.I?.ZoomStep(1.5f), "Yakınlaş (tekerlek)");
            AracEkle(g1, "−", () => MapCamera.I?.ZoomStep(-1.5f), "Uzaklaş (tekerlek)");
            AracEkle(g1, "↺", () => MapCamera.I?.Rotate(-45f), "Sola döndür (Q)");
            AracEkle(g1, "↻", () => MapCamera.I?.Rotate(45f), "Sağa döndür (E)");
            AracEkle(g1, "Tümü", () => MapCamera.I?.ResetView(), "Bütün haritayı göster (0)");

            var g2 = U.El(aracKok, "arac-grup");
            var lej = AracEkle(g2, "Lejant", () => legendOpen = !legendOpen, "Haritadaki renk ve simgelerin anlamı");
            lejantYazi = lej.Q<Label>();
            var gor = AracEkle(g2, "Düz harita", () => MapCamera.I?.ToggleFlatMode(), "Düz harita / 3D görünüm (M)");
            gorunumYazi = gor.Q<Label>();

            var g3 = U.El(aracKok, "arac-grup");
            katmanDugme = AracEkle(g3, "Harita katmanı ▲", () => heatMenuOpen = !heatMenuOpen,
                "Harita katmanları: sipariş yoğunluğu, kaçan siparişler, teslimat süresi, kurye kapsaması, dükkan kârlılığı (H: sırayla)");
            katmanDugmeYazi = katmanDugme.Q<Label>();
            katmanMenu = U.El(g3, "katman-menu");
            katmanMenu.style.left = 0;
            for (int i = 0; i < HeatModeCount; i++)
            {
                var m = (HeatMode)i;
                var oge = U.Tikla(U.El(katmanMenu, "katman-oge"), () => SetHeat(m));
                var ad = U.Kalin(oge, HeatName(m), "katman-ad");
                U.Yazi(oge, KatmanAciklamalari[i], "katman-aciklama");
                katmanOgeleri.Add((oge, ad, m));
            }
            U.Goster(katmanMenu, false);

            var yardim = U.El(aracKok, "arac-grup");
            var soru = AracEkle(yardim, "?", null, null);
            Ipucu(soru, "Harita kısayolları\nTekerlek: yakınlaş · sol sürükle: kaydır · sağ sürükle ya da Q/E: döndür\n" +
                        "N: kuzey · M: düz harita · 0: tüm harita · H: harita katmanları\n" +
                        "Boşluk: duraklat · 1–8: hız · Esc: menü · F10: fotoğraf modu · F8: eski arayüz");

            katmanNot = U.Yazi(aracKok, "", "katman-not");
            U.Goster(katmanNot, false);
        }

        void UiHaritaYenile(bool agir)
        {
            var mc = MapCamera.I;
            U.Goster(katmanMenu, heatMenuOpen);
            if (heatMenuOpen)
                foreach (var (kok, ad, m) in katmanOgeleri)
                {
                    bool sec = m == heatMode;
                    if (sec) U.Zemin(kok, U.A(UITheme.Accent, 0.3f)); else ZeminSil(kok);
                }
            U.Metin(katmanDugmeYazi, HeatButtonText);
            if (heatMode != HeatMode.Kapali) U.Zemin(katmanDugme, U.A(UITheme.Accent, 0.35f)); else ZeminSil(katmanDugme);
            U.Metin(lejantYazi, legendOpen ? "Lejant ✓" : "Lejant");
            if (mc != null) U.Metin(gorunumYazi, mc.FlatMode ? "3D görünüm" : "Düz harita");
            bool not = heatMode != HeatMode.Kapali;
            U.Goster(katmanNot, not);
            if (not && agir) U.Metin(katmanNot, HeatCaption);
        }

        /// <summary>
        /// Haritanın üstündeki UI Toolkit kutularının dikdörtgenleri (GUI koordinatı). IMGUI harita etiketleri ve uçan yazılar
        /// bunların altına girmesin diye dolu sayılır.
        /// </summary>
        readonly List<Rect> uiHaritaDolu = new List<Rect>();

        void UiHaritaUstuDolu(List<Rect> hedef)
        {
            uiHaritaDolu.Clear();
            void Ekle(VisualElement e)
            {
                if (e == null || !U.Gorunur(e)) return;
                var r = e.worldBound;
                if (r.width > 1f && r.height > 1f && !float.IsNaN(r.x)) uiHaritaDolu.Add(new Rect(r.x - 4f, r.y - 4f, r.width + 8f, r.height + 8f));
            }
            Ekle(hedefKok);
            Ekle(olayKok);
            Ekle(aracKok);
            if (heatMenuOpen) Ekle(katmanMenu);
            hedef?.AddRange(uiHaritaDolu);
        }

        bool UiHaritaUstuDoluMu(Rect r)
        {
            foreach (var o in uiHaritaDolu) if (o.Overlaps(r)) return true;
            return false;
        }

        // ------------------------------------------------------------------ olaylar kartı (Dikkat + son olaylar)

        VisualElement olayKok, olayDikkat, olayAyrac, olayAkis, olayBaslikSimge;
        Label olayBaslik, olayOk;
        readonly List<(VisualElement kok, VisualElement nokta, Label yazi)> dikkatSatirlari = new List<(VisualElement, VisualElement, Label)>();
        readonly List<(VisualElement kok, VisualElement nokta, Label yazi)> akisSatirlari = new List<(VisualElement, VisualElement, Label)>();
        float uyariToplamaAni;
        const int OlayDikkatMax = 4, OlayAkisMax = 4;
        const string OlaylarKucukKey = "KuryeTycoon.OlaylarKucuk";

        static bool OlaylarKucuk
        {
            get { return PlayerPrefs.GetInt(OlaylarKucukKey, 0) == 1; }
            set { PlayerPrefs.SetInt(OlaylarKucukKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        void UiOlaylarKur()
        {
            olayKok = U.El(katHud, "olaylar");
            var bas = U.Tikla(U.El(olayKok, "olaylar-baslik"), () => OlaylarKucuk = !OlaylarKucuk);
            olayBaslikSimge = U.Simge(bas, Icons.Kind.Warning, UITheme.Warn, 14f);
            olayBaslik = U.Kalin(bas, "", "kucuk");
            olayBaslik.style.marginLeft = 8;
            U.Gecirgen(bas, "bosluk");
            olayOk = U.Yazi(bas, "▼", "kucuk", "soluk");
            Ipucu(bas, "Tıkla: küçült / aç");

            olayDikkat = U.Gecirgen(olayKok, "kolon");
            for (int i = 0; i < OlayDikkatMax; i++)
            {
                int k = i;
                var kok = U.Tikla(U.El(olayDikkat, "olay-oge", "tiklanir"), () => { if (k < uyarilar.Count) UyariyaGit(uyarilar[k]); });
                var nokta = U.Gecirgen(kok, "olay-nokta");
                var yazi = U.Yazi(kok, "", "olay-yazi", "tek-satir");
                U.Simge(kok, Icons.Kind.Flag, Color.white, 0f, "olay-git");
                dikkatSatirlari.Add((kok, nokta, yazi));
            }
            olayAyrac = U.Gecirgen(olayKok, "olaylar-ayrac");
            olayAkis = U.Gecirgen(olayKok, "kolon");
            for (int i = 0; i < OlayAkisMax; i++)
            {
                int k = i;
                var kok = U.El(olayAkis, "olay-oge");
                var nokta = U.Gecirgen(kok, "olay-nokta");
                var yazi = U.Yazi(kok, "", "olay-yazi", "tek-satir");
                Ipucu(kok, () => EventManager.I != null && k < EventManager.I.Feed.Count ? EventManager.I.Feed[k] : "");
                akisSatirlari.Add((kok, nokta, yazi));
            }
        }

        void UiOlaylarYenile(bool agir)
        {
            bool gor = popupCourier == null && !InfoOpen && EventManager.I != null;
            U.Goster(olayKok, gor);
            if (!gor || !agir) return;

            if (Time.unscaledTime >= uyariToplamaAni)
            {
                uyariToplamaAni = Time.unscaledTime + 1f;
                UyarilariTopla();
            }
            var feed = EventManager.I.Feed;
            bool kucuk = OlaylarKucuk;
            int nUyari = uyarilar.Count;
            U.Metin(olayOk, kucuk ? "▲" : "▼");
            U.SimgeAyarla(olayBaslikSimge, nUyari > 0 ? Icons.Kind.Warning : Icons.Kind.Clock, nUyari > 0 ? UITheme.Warn : UITheme.TextDim);
            U.Metin(olayBaslik, nUyari > 0 ? $"Dikkat ({nUyari})" : "Son olaylar");

            U.Goster(olayDikkat, !kucuk && nUyari > 0);
            U.Goster(olayAkis, !kucuk && feed.Count > 0);
            U.Goster(olayAyrac, !kucuk && nUyari > 0 && feed.Count > 0);
            if (kucuk) return;

            for (int i = 0; i < dikkatSatirlari.Count; i++)
            {
                var (kok, nokta, yazi) = dikkatSatirlari[i];
                bool vr = i < nUyari;
                U.Goster(kok, vr);
                if (!vr) continue;
                var u = uyarilar[i];
                U.Zemin(nokta, u.renk);
                U.Metin(yazi, i == OlayDikkatMax - 1 && nUyari > OlayDikkatMax ? $"{u.metin} (+{nUyari - OlayDikkatMax} daha)" : u.metin);
            }
            for (int i = 0; i < akisSatirlari.Count; i++)
            {
                var (kok, nokta, yazi) = akisSatirlari[i];
                bool vr = i < feed.Count;
                U.Goster(kok, vr);
                if (!vr) continue;
                U.Zemin(nokta, i == 0 ? UITheme.Accent : U.A(UITheme.TextDim, 0.5f));
                U.Renk(yazi, i == 0 ? UITheme.Text : UITheme.TextDim);
                U.Metin(yazi, feed[i]);
            }
        }

        // ------------------------------------------------------------------ haftalık hedef kutusu

        VisualElement hedefKok;
        Label hedefKalan, hedefEk, hedefSenaryo;
        readonly List<(VisualElement kok, VisualElement daire, Label ad, Label sayi, U.Cubuk cubuk)> hedefSatirlari =
            new List<(VisualElement, VisualElement, Label, Label, U.Cubuk)>();

        void UiHedefKur()
        {
            hedefKok = U.Tikla(U.El(katHud, "hedef-kutu"), () => OpenTab(Tab.Hedefler));
            hedefKok.style.left = DockW + 10f;
            hedefSenaryo = U.Kalin(hedefKok, "", "kucuk");   // senaryo ilerlemesi (Senaryo.cs)
            hedefSenaryo.style.marginBottom = 6;
            hedefSenaryo.style.whiteSpace = WhiteSpace.Normal;
            var bas = U.Gecirgen(hedefKok, "satir");
            U.Simge(bas, Icons.Kind.Flag, UITheme.Accent, 14f);
            var t = U.Kalin(bas, "Haftalık hedefler", "kucuk");
            t.style.marginLeft = 8;
            U.Gecirgen(bas, "bosluk");
            hedefKalan = U.Yazi(bas, "", "kucuk", "soluk");
            for (int i = 0; i < TrackerMax; i++)
            {
                var kok = U.Gecirgen(hedefKok, "kolon");
                kok.style.marginTop = 6;
                var sat = U.Gecirgen(kok, "satir");
                var daire = U.Gecirgen(sat, "hedef-daire");
                var ad = U.Yazi(sat, "", "hedef-ad", "tek-satir");
                var sayi = U.Yazi(sat, "", "hedef-sayi");
                var cb = U.CubukEkle(kok, 0f, UITheme.Accent);
                cb.kok.style.height = 4;
                cb.kok.style.marginLeft = 22;
                cb.kok.style.marginTop = 4;
                hedefSatirlari.Add((kok, daire, ad, sayi, cb));
            }
            hedefEk = U.Yazi(hedefKok, "", "minik", "soluk");
            hedefEk.style.marginTop = 6;
            hedefEk.style.marginLeft = 22;
        }

        void UiHedefYenile(bool agir)
        {
            bool gor = TrackerVisible;
            U.Goster(hedefKok, gor);
            if (!gor || !agir) return;
            var gm = GoalManager.I;
            bool senaryo = Senaryo.HedefVar;
            U.Goster(hedefSenaryo, senaryo);
            if (senaryo)
            {
                U.Metin(hedefSenaryo, Senaryo.IlerlemeYazisi);
                U.Renk(hedefSenaryo, Senaryo.Durum == 1 ? UITheme.Good : Senaryo.Durum == 2 ? UITheme.Bad : ItibarRengi);
            }
            U.Metin(hedefKalan, $"{gm.DaysLeft} gün kaldı");
            var hedefler = TrackerGoals();
            for (int i = 0; i < hedefSatirlari.Count; i++)
            {
                var (kok, daire, ad, sayi, cb) = hedefSatirlari[i];
                bool vr = i < hedefler.Count;
                U.Goster(kok, vr);
                if (!vr) continue;
                var g = hedefler[i];
                if (g.done) U.Zemin(daire, UITheme.Good); else ZeminSil(daire);
                var kenar = g.done ? UITheme.Good : U.A(Color.white, 0.45f);
                daire.style.borderTopColor = daire.style.borderBottomColor = daire.style.borderLeftColor = daire.style.borderRightColor = kenar;
                U.Metin(ad, g.Title);
                U.Renk(ad, g.done ? UITheme.TextDim : UITheme.Text);
                U.Metin(sayi, g.done ? "✓" : g.ProgressText);
                U.Renk(sayi, g.done ? UITheme.Good : UITheme.TextDim);
                cb.Ayarla(g.Progress01, g.done ? UITheme.Good : UITheme.Accent);
            }
            int ek = TrackerExtra;
            U.Goster(hedefEk, ek > 0);
            if (ek > 0)
            {
                int done = 0;
                foreach (var g in gm.Goals) if (g.done) done++;
                U.Metin(hedefEk, $"+{ek} hedef daha · {done}/{gm.Goals.Count} bitti · tıkla");
            }
        }
    }
}
