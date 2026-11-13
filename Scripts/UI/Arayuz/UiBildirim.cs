using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Haritanın üstünde beliren kartlar (UI Toolkit):
    ///  • Bildirimler (Toasts.cs'teki liste): haritanın görünen kısmının üst ortasında, birkaç saniye; tıklayınca kapanır.
    ///  • İlk gün rehberi (Tutorial.cs'teki adımlar): alt ortada kart; hedef menü sol çubukta (ya da penceredeki sekme) yanıp söner.
    /// IMGUI'deki çizimleri (DrawToasts, DrawGuideOverlays) yeni arayüz açıkken çizilmez.
    /// </summary>
    public partial class GameUI
    {
        // ------------------------------------------------------------------ bildirimler

        VisualElement bildirimKok;
        U.Liste<Toast, Toast, BildirimKart> bildirimler;

        class BildirimKart
        {
            public VisualElement kok, serit, simgeKutu, simge;
            public Label bas, metin;
            public ToastType tur = (ToastType)(-1);
        }

        void UiBildirimKur()
        {
            bildirimKok = U.Gecirgen(katUst, "bildirimler");
            bildirimler = new U.Liste<Toast, Toast, BildirimKart>(bildirimKok, t => t, BildirimKartKur, k => k.kok, BildirimKartYenile);
        }

        BildirimKart BildirimKartKur(Toast t)
        {
            var k = new BildirimKart();
            k.kok = U.El(null, "bildirim");
            var tost = t;
            U.Tikla(k.kok, () =>
            {
                bool mesaj = tost.type == ToastType.Message;
                toasts.Remove(tost);
                if (mesaj && !(panelOpen && tab == Tab.Mesajlar)) OpenTab(Tab.Mesajlar);
            });
            k.serit = U.Gecirgen(k.kok, "bildirim-serit");
            k.simgeKutu = U.Gecirgen(k.kok, "bildirim-simge-kutu");
            k.simge = U.Simge(k.simgeKutu, Icons.Kind.Star, Color.white, 16f);
            var yazi = U.Gecirgen(k.kok, "bildirim-yazi");
            k.bas = U.Kalin(yazi, "", "minik");
            k.metin = U.Yazi(yazi, "", "bildirim-metin");
            return k;
        }

        static void ToastGorunumu(Toast t, out Color renk, out Icons.Kind simge, out string bas)
        {
            switch (t.type)
            {
                case ToastType.Good: renk = UITheme.Good; simge = Icons.Kind.Star; bas = "Harika"; break;
                case ToastType.Boost: renk = new Color(1f, 0.62f, 0.20f); simge = Icons.Kind.Bolt; bas = "Boost"; break;
                case ToastType.Info:
                    renk = new Color(0.40f, 0.70f, 1f);
                    bool sehir = t.text.StartsWith("Şehir");
                    simge = sehir ? Icons.Kind.Calendar : Icons.Kind.Clock;
                    bas = sehir ? "Şehir" : "İpucu";
                    break;
                case ToastType.Message: renk = new Color(0.55f, 0.85f, 0.65f); simge = Icons.Kind.Phone; bas = "Kurye mesajı · tıkla"; break;
                default: renk = UITheme.Bad; simge = Icons.Kind.Warning; bas = "Dikkat"; break;
            }
        }

        void BildirimKartYenile(BildirimKart k, Toast t)
        {
            if (k.tur != t.type)
            {
                k.tur = t.type;
                ToastGorunumu(t, out var renk, out var simge, out var bas);
                U.Zemin(k.serit, renk);
                U.Zemin(k.simgeKutu, U.A(renk, 0.2f));
                U.SimgeAyarla(k.simge, simge, renk);
                U.Metin(k.bas, bas);
                U.Renk(k.bas, renk);
            }
            U.Metin(k.metin, t.text + (t.count > 1 ? $"  (+{t.count - 1})" : ""));
            // Belirme ve kaybolma: 0,25 sn'de gelir, son yarım saniyede söner
            float yas = Time.unscaledTime - t.born;
            float a = Mathf.Min(Mathf.Clamp01(yas / 0.25f), Mathf.Clamp01((t.Life - yas) / 0.5f));
            float kay = (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(yas / 0.25f))) * -16f;
            k.kok.style.opacity = a;
            k.kok.style.translate = new Translate(0, kay);
        }

        void UiBildirimYenile()
        {
            float now = Time.unscaledTime;
            toasts.RemoveAll(t => now - t.born > t.Life);
            U.Goster(bildirimKok, toasts.Count > 0);
            if (toasts.Count == 0) { bildirimler.Esitle(toasts); return; }

            // Haritanın görünen kısmının ortası (açık pencere solda, kurye telefonu / bina kartı sağda kalır)
            float sw = UiEkranGenisligi;
            float sol = MapLeftGui;
            float sag = popupCourier != null || InfoOpen ? sw - 376f : sw;
            float w = Mathf.Min(460f, Mathf.Max(260f, sag - sol - 24f));
            float x = Mathf.Max(sol + 12f, (sol + sag) / 2f - w / 2f);
            bildirimKok.style.left = x;
            bildirimKok.style.width = w;
            bildirimler.Esitle(toasts);
        }

        // ------------------------------------------------------------------ ilk gün rehberi

        VisualElement rehberKart, rehberVurgu, rehberTikla;
        Label rehberBaslik, rehberAdim, rehberMetin;
        Button rehberIleri, rehberKapat;
        int rehberGosterilen = -1;

        void UiRehberKur()
        {
            rehberVurgu = U.Gecirgen(katUst, "rehber-vurgu");
            rehberTikla = U.Gecirgen(katUst, "rehber-tikla");
            U.Kalin(rehberTikla, "← Tıkla", "rehber-tikla-yazi");

            rehberKart = U.El(katUst, "rehber-kart");
            var ust = U.Gecirgen(rehberKart, "satir");
            ust.style.alignItems = Align.FlexStart;
            rehberBaslik = U.Kalin(ust, "", "rehber-baslik");
            rehberAdim = U.Yazi(ust, "", "minik", "soluk");
            rehberAdim.style.marginLeft = 8;
            rehberAdim.style.marginTop = 4;
            rehberMetin = U.Yazi(rehberKart, "", "rehber-metin");
            var alt = U.Gecirgen(rehberKart, "satir");
            alt.style.marginTop = 12;
            rehberIleri = U.Dugme(alt, "", () => deferred = () => Guide.I?.NextStep(TutorialSteps.Length), "ilk");
            rehberIleri.style.minWidth = 110;
            U.Gecirgen(alt, "bosluk");
            rehberKapat = U.Dugme(alt, "Rehberi kapat", () => deferred = () => Guide.I?.FinishTutorial(), "kucuk-dg");
            U.Goster(rehberVurgu, false);
            U.Goster(rehberTikla, false);
            U.Goster(rehberKart, false);
        }

        void UiRehberYenile()
        {
            bool gor = TutorialVisible && !BuyukKartCiziliyor;
            U.Goster(rehberKart, gor);
            if (!gor)
            {
                U.Goster(rehberVurgu, false);
                U.Goster(rehberTikla, false);
                rehberGosterilen = -1;
                return;
            }

            int index = Mathf.Clamp(Guide.I.TutorialStep, 0, TutorialSteps.Length - 1);
            var st = TutorialSteps[index];
            if (index != rehberGosterilen)
            {
                rehberGosterilen = index;
                bool son = index == TutorialSteps.Length - 1;
                bool bekler = st.target != null || st.done != null;   // kendiliğinden geçen adım
                U.Metin(rehberBaslik, st.title);
                U.Metin(rehberAdim, $"Rehber {index + 1}/{TutorialSteps.Length}");
                U.Metin(rehberMetin, st.text);
                rehberIleri.text = son ? "Bitir" : (bekler ? "Atla" : "Devam");
                U.Sinif(rehberIleri, "kucuk-dg", bekler);
                if (bekler) { ZeminSil(rehberIleri); RenkSil(rehberIleri); }
                else { U.Zemin(rehberIleri, UITheme.Accent); U.Renk(rehberIleri, UITheme.AccentText); }
                U.Goster(rehberKapat, !son);
            }
            RehberKartKonumu();

            // Hedef yanıp söner: doğru menü açıksa penceredeki sekme, değilse sol çubuktaki menü
            VisualElement hedef = null;
            bool dock = false;
            if (st.target != null && !(panelOpen && tab == st.target.Value))
            {
                var t = st.target.Value;
                int g = GroupIndexOf(t);
                if (panelOpen && GroupIndexOf(tab) == g)
                    foreach (var so in sekmeOgeleri)
                        if (so.t == t && U.Gorunur(so.l)) { hedef = so.l; break; }
                if (hedef == null)
                    foreach (var o in dockOgeleri)
                        if (o.g == g && U.Gorunur(o.kok)) { hedef = o.kok; dock = true; break; }
            }
            bool vr = hedef != null && hedef.worldBound.width > 1f;
            U.Goster(rehberVurgu, vr);
            U.Goster(rehberTikla, vr && dock && !panelOpen);
            if (!vr) return;

            var r = hedef.worldBound;
            float nabiz = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f);
            var renk = U.A(TutorialColor, nabiz);
            rehberVurgu.style.left = r.x - 3f;
            rehberVurgu.style.top = r.y - 3f;
            rehberVurgu.style.width = r.width + 6f;
            rehberVurgu.style.height = r.height + 6f;
            rehberVurgu.style.borderTopColor = rehberVurgu.style.borderBottomColor =
                rehberVurgu.style.borderLeftColor = rehberVurgu.style.borderRightColor = renk;
            if (U.Gorunur(rehberTikla))
            {
                rehberTikla.style.left = r.xMax + 10f;
                rehberTikla.style.top = r.center.y - 13f;
                U.Zemin(rehberTikla, U.A(TutorialColor, 0.85f + 0.15f * nabiz));
            }
        }

        /// <summary>Haritanın alt ortası (pencere ve lejantın sağı, olaylar kartının solu); sığmazsa olaylar kartının üstü.</summary>
        void RehberKartKonumu()
        {
            float sw = UiEkranGenisligi, sh = UiEkranYuksekligi;
            float sol = Mathf.Max(MapLeftGui, panelOpen ? WindowRect(sh).xMax : DockW) + 12f;
            if (legendOpen) sol = Mathf.Max(sol, LegendRect(sh).xMax + 12f);
            bool olay = olayKok != null && U.Gorunur(olayKok) && olayKok.layout.width > 1f;
            float sag = olay ? olayKok.worldBound.x - 12f : sw - 12f;
            float x, alt;
            if (sag - sol >= TutorialW) { x = sol + (sag - sol - TutorialW) / 2f; alt = 54f; }
            else { x = sw - TutorialW - 12f; alt = olay ? sh - olayKok.worldBound.y + 12f : 54f; }
            rehberKart.style.left = x;
            rehberKart.style.bottom = alt;
        }
    }
}
