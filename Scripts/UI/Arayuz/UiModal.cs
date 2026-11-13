using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Ekranın ortasında açılan kutular (UI Toolkit): olay kartı, Esc menüsü ve onay penceresi.
    /// UI Toolkit, IMGUI'nin üstüne çizildiği için bu kutular IMGUI'de kalırsa açık pencerenin altında kalıyordu; artık
    /// en üst katmanda (katUst), ekranı karartan bir perdenin üstünde. Perde altındaki her şeye tıklamayı keser.
    /// Aynı anda tek kutu görünür: onay > Esc menüsü > olay kartı. Klavye (Esc, Enter) IMGUI tarafında kaldı (GameUI.HandleMenuKey, HandleConfirmKey).
    /// </summary>
    public partial class GameUI
    {
        VisualElement modalPerde, olayKart, menuKart, onayKart;

        enum UiModalTur { Yok, Olay, Menu, Onay }

        UiModalTur UiAcikModal =>
            ConfirmOpen ? UiModalTur.Onay :
            pauseMenuOpen ? UiModalTur.Menu :
            EventManager.I != null && EventManager.I.Current != null ? UiModalTur.Olay :
            UiModalTur.Yok;

        void UiModalKur()
        {
            modalPerde = U.El(katUst, "perde");
            // Perdeye tıklamak hiçbir şey yapmaz ama haritaya ve alttaki pencerelere de geçmez
            modalPerde.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            U.Goster(modalPerde, false);

            olayKart = U.El(modalPerde, "modal-kart", "olay-kart");
            menuKart = U.El(modalPerde, "modal-kart", "menu-kart");
            onayKart = U.El(modalPerde, "modal-kart", "onay-kart");
            MenuKartKur();
            OnayKartKur();
        }

        void UiModalYenile(bool agir)
        {
            var tur = UiAcikModal;
            U.Goster(modalPerde, tur != UiModalTur.Yok);
            U.Goster(olayKart, tur == UiModalTur.Olay);
            U.Goster(menuKart, tur == UiModalTur.Menu);
            U.Goster(onayKart, tur == UiModalTur.Onay);
            if (tur != UiModalTur.Menu) menuIlkKez = true;   // menü her açılışta hemen tazelensin
            if (tur == UiModalTur.Yok) { olayKartFor = null; return; }

            float sh = UiEkranYuksekligi;
            float maxH = Mathf.Max(200f, sh - 40f);
            switch (tur)
            {
                case UiModalTur.Olay: OlayKartYenile(maxH); break;
                case UiModalTur.Menu: if (agir || menuIlkKez) MenuKartYenile(); break;
                case UiModalTur.Onay: OnayKartYenile(); break;
            }
        }

        /// <summary>Büyük kurye kartı (IMGUI) şu an çiziliyor mu: olay kartı ve Esc menüsü açıkken çizilmez (GameUI.OnGUI).</summary>
        bool BuyukKartCiziliyor => BigCardOpen && (EventManager.I == null || EventManager.I.Current == null) && !pauseMenuOpen;

        /// <summary>Şirket renginde düğme (ilk seçenek, "Evet"). Satır içi zemin USS'teki :hover'ı ezdiği için üstüne gelme koddan.</summary>
        static void VurguDugme(Button b)
        {
            var acc = UITheme.Accent;
            U.Zemin(b, acc);
            U.Renk(b, UITheme.AccentText);
            b.RegisterCallback<PointerEnterEvent>(_ => U.Zemin(b, Color.Lerp(UITheme.Accent, Color.white, 0.15f)));
            b.RegisterCallback<PointerLeaveEvent>(_ => U.Zemin(b, UITheme.Accent));
        }

        // ------------------------------------------------------------------ olay kartı

        GameEvent olayKartFor;
        bool olayKartAyrinti;
        ScrollView olayKaydir;

        void OlayKartYenile(float maxH)
        {
            var ev = EventManager.I.Current;
            if (olayKart.style.maxHeight != maxH) olayKart.style.maxHeight = maxH;
            if (ev == olayKartFor && olayAyrintiAcik == olayKartAyrinti) return;
            if (olayAyrintiFor != ev) { olayAyrintiFor = ev; olayAyrintiAcik = false; }
            olayKartFor = ev;
            olayKartAyrinti = olayAyrintiAcik;
            OlayKartKur(ev);
        }

        void OlayKartKur(GameEvent ev)
        {
            olayKart.Clear();
            bool ozetli = ev.ozet != null && ev.ozet.Count > 0;
            olayKart.style.width = ozetli ? 640f : 580f;

            var ust = U.Gecirgen(olayKart, "olay-ust");
            U.Zemin(ust, U.A(UITheme.Accent, 0.16f));
            U.Zemin(U.Gecirgen(ust, "olay-serit"), UITheme.Accent);
            U.Kalin(ust, ev.title, "olay-baslik");

            var icerik = U.Gecirgen(olayKart, "olay-icerik");
            if (ozetli)
            {
                var satir = U.Gecirgen(icerik, "ozet-satir");
                for (int i = 0; i < ev.ozet.Count; i++)
                {
                    var o = ev.ozet[i];
                    Color ton = o.ton == 1 ? UITheme.Good : o.ton == 2 ? UITheme.Bad : o.ton == 3 ? UITheme.Warn : UITheme.Text;
                    var kutu = U.Gecirgen(satir, "ozet-kutu");
                    if (i == 0) kutu.AddToClassList("ilk");
                    U.Zemin(kutu, o.ton == 0 ? new Color(1f, 1f, 1f, 0.05f) : U.A(ton, 0.13f));
                    U.Kalin(kutu, (o.baslik ?? "").ToUpperInvariant(), "ozet-baslik");
                    U.Renk(U.Kalin(kutu, o.deger, "ozet-deger"), ton);
                }
            }

            // Rapor kartı: önce ilk paragraf, dökümün geri kalanı "Ayrıntılar" ile açılır (IMGUI'deki DrawEvent ile aynı)
            string metin = ev.body ?? "";
            int kesim = ozetli ? metin.IndexOf("\n\n", System.StringComparison.Ordinal) : -1;
            bool katlanir = kesim > 0 && kesim < metin.Length - 2;
            if (katlanir && !olayAyrintiAcik) metin = metin.Substring(0, kesim);

            olayKaydir = new ScrollView(ScrollViewMode.Vertical);
            olayKaydir.AddToClassList("kaydir");
            olayKaydir.AddToClassList("olay-govde");
            olayKaydir.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            icerik.Add(olayKaydir);
            var govde = U.Yazi(olayKaydir, metin, "olay-metin");
            if (ozetli) { govde.AddToClassList("kucuk"); govde.AddToClassList("soluk"); }
            govde.enableRichText = true;

            if (katlanir)
            {
                var ay = U.Dugme(icerik, olayAyrintiAcik ? "Ayrıntıları gizle ▲" : "Ayrıntılar (gelir, gider, kasa) ▼",
                                 () => olayAyrintiAcik = !olayAyrintiAcik, "kucuk-dg", "olay-ayrinti");
                ay.style.alignSelf = Align.FlexStart;
            }

            var secenekler = U.Gecirgen(icerik, "olay-secenekler");
            for (int i = 0; i < ev.choices.Count; i++)
            {
                int idx = i;
                var b = U.Dugme(secenekler, ev.choices[i].label, () =>
                {
                    // Aynı karede iki kez basılmasın: seçim bir sonraki Update'te (IMGUI'deki gibi)
                    var e = EventManager.I.Current;
                    deferred = () => { if (EventManager.I.Current == e) EventManager.I.Choose(idx); };
                }, "olay-secim");
                if (i == 0) VurguDugme(b);
            }
        }

        // ------------------------------------------------------------------ Esc menüsü

        Label menuKayitYazi, menuKayitDugmeYazi;
        Button menuKaydet, menuKaydetCik;
        VisualElement menuYuvalar, menuAnaDugme, menuAnaOnay, menuKayitBolum;
        U.Onay menuOtomatik;
        bool menuIlkKez = true;
        string menuYuvaImza = "";

        void MenuKartKur()
        {
            U.Kalin(menuKart, "Menü", "modal-baslik");
            U.Yazi(menuKart, "Oyun duraklatıldı. Esc ile kapanır.", "kucuk", "soluk");

            var devam = U.Dugme(menuKart, "Devam et", () => deferred = () => SetPauseMenu(false), "modal-dg");
            devam.style.marginTop = 12;
            VurguDugme(devam);

            menuKayitBolum = U.Gecirgen(menuKart, "kolon");
            menuKaydet = U.Dugme(menuKayitBolum, "", () => { var s = SaveSystem.I; if (s != null) deferred = () => { s.Save(false); menuIlkKez = true; }; }, "modal-dg");
            var yr = U.Gecirgen(menuKayitBolum, "satir");
            yr.style.marginTop = 6;
            U.Yazi(yr, "Başka yuvaya kaydet:", "kucuk", "soluk").style.width = 140;
            menuYuvalar = U.Gecirgen(yr, "satir");
            menuYuvalar.style.flexGrow = 1;
            menuKayitYazi = U.Yazi(menuKayitBolum, "", "kucuk", "soluk");
            menuKayitYazi.style.marginTop = 8;
            menuOtomatik = U.OnayEkle(menuKayitBolum, "Her gece otomatik kaydet",
                () => SaveSystem.I != null && SaveSystem.I.autoSave, v => { if (SaveSystem.I != null) SaveSystem.I.autoSave = v; });
            menuOtomatik.kok.style.marginTop = 8;

            var ayrac = U.Gecirgen(menuKart, "olaylar-ayrac");
            ayrac.style.marginTop = 8;
            ayrac.style.marginBottom = 4;

            menuAnaDugme = U.Dugme(menuKart, "Ana menüye dön", () => deferred = () => { confirmMainMenu = true; menuIlkKez = true; }, "modal-dg");
            menuAnaOnay = U.Gecirgen(menuKart, "kolon");
            menuAnaOnay.style.marginTop = 6;
            U.Yazi(menuAnaOnay, "Son kayıttan sonraki ilerleme kaybolur. Emin misin?", "kucuk", "uyari-renk");
            var ao = U.Gecirgen(menuAnaOnay, "satir");
            ao.style.marginTop = 6;
            var evet = U.Dugme(ao, "Evet, ana menüye dön", () => deferred = () => { SetPauseMenu(false); GameBootstrap.Restart(); }, "genis", "ilk", "tehlike");
            evet.style.height = 32;
            U.Dugme(ao, "Vazgeç", () => deferred = () => { confirmMainMenu = false; menuIlkKez = true; }, "genis").style.height = 32;

            menuKaydetCik = U.Dugme(menuKart, "Kaydet ve çık", () =>
            {
                var s = SaveSystem.I;
                if (s != null) deferred = () => { if (s.Save(false)) QuitGame(); };
            }, "modal-dg");
            U.Dugme(menuKart, "Kaydetmeden çık", () => deferred = QuitGame, "modal-dg");
        }

        void MenuKartYenile()
        {
            menuIlkKez = false;
            var save = SaveSystem.I;
            U.Goster(menuKayitBolum, save != null);
            U.Goster(menuKaydetCik, save != null);
            U.Goster(menuAnaDugme, !confirmMainMenu);
            U.Goster(menuAnaOnay, confirmMainMenu);
            if (save == null) return;

            bool kaydedilir = SaveSystem.CanSaveNow;
            menuKaydet.text = $"Kaydet ({SaveSystem.Slot + 1}. yuva)";
            menuKaydet.SetEnabled(kaydedilir);
            menuKaydetCik.SetEnabled(kaydedilir);
            U.Metin(menuKayitYazi, string.IsNullOrEmpty(save.LastSaveText) ? "Bu oturumda henüz kaydedilmedi." : $"Son kayıt: {save.LastSaveText}");
            menuOtomatik.Yenile();

            // Diğer yuvalar: dolu olanlar "üstüne yaz" der
            var sb = new System.Text.StringBuilder();
            sb.Append(SaveSystem.Slot).Append(kaydedilir);
            for (int i = 0; i < SaveSystem.SlotCount; i++) sb.Append(SaveSystem.SlotHasSave(i) ? '1' : '0');
            string imza = sb.ToString();
            if (imza == menuYuvaImza) return;
            menuYuvaImza = imza;
            menuYuvalar.Clear();
            bool ilk = true;
            for (int i = 0; i < SaveSystem.SlotCount; i++)
            {
                if (i == SaveSystem.Slot) continue;
                int slot = i;
                bool dolu = SaveSystem.SlotHasSave(i);
                var b = U.Dugme(menuYuvalar, $"{i + 1}" + (dolu ? " (üstüne yaz)" : ""), () => deferred = () =>
                {
                    SaveSystem.Slot = slot;
                    save.Save(false);
                    setupSaveChecked = false;
                    menuIlkKez = true;
                }, "genis", "kucuk-dg");
                if (ilk) b.AddToClassList("ilk");
                ilk = false;
                b.SetEnabled(kaydedilir);
            }
        }

        // ------------------------------------------------------------------ onay penceresi

        Label onayBaslik, onayMetin;
        Button onayEvet;
        string onayImza = "";

        void OnayKartKur()
        {
            onayBaslik = U.Kalin(onayKart, "", "onay-baslik");
            onayMetin = U.Yazi(onayKart, "", "onay-metin");
            onayMetin.enableRichText = true;
            var satir = U.Gecirgen(onayKart, "satir");
            satir.style.marginTop = 16;
            onayEvet = U.Dugme(satir, "", AcceptConfirm, "genis", "ilk", "modal-dg");
            VurguDugme(onayEvet);
            U.Dugme(satir, "Vazgeç", () => deferred = CloseConfirm, "genis", "modal-dg");
        }

        void OnayKartYenile()
        {
            string imza = confirmTitle + "\u0001" + confirmText + "\u0001" + confirmYes;
            if (imza == onayImza) return;
            onayImza = imza;
            U.Metin(onayBaslik, confirmTitle);
            U.Metin(onayMetin, confirmText);
            onayEvet.text = confirmYes;
            U.Zemin(onayEvet, UITheme.Accent);
            U.Renk(onayEvet, UITheme.AccentText);
        }
    }
}
