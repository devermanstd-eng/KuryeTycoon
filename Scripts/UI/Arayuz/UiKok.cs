using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// UI Toolkit kökü. Oyun içindeki bütün ekranlar UI Toolkit'te; IMGUI (OnGUI) yalnızca haritanın üstüne çizilenler
    /// (etiketler, katman, uçan yazılar, seçili bina işareti), kurulum ekranı ve eski arayüz (F8) için kaldı.
    ///  • UIDocument ve PanelSettings çalışma anında kurulur (sahneye bir şey eklemek gerekmez).
    ///  • Ölçek IMGUI ile aynı (GameUI.uiScale): iki sistemin koordinatları birebir aynı, dikdörtgenler karşılaştırılabilir.
    ///  • Katmanlar: hud (üst çubuk, sol menü, harita araçları, olaylar, durum kartları) · pencere (pencere, kurye telefonu,
    ///    bina kartı) · üst (bildirimler, rehber, büyük kurye kartı, en üstte olay kartı / Esc menüsü / onay) · ipucu.
    ///  • Harita farenin UI Toolkit elemanının üstünde olup olmadığını <see cref="UiUzerinde"/> ile sorar.
    ///  • Değerler 8 Hz'de yenilenir (hız düğmeleri her karede); ağaç bir kez kurulur.
    /// Kural: yeni arayüz UI Toolkit ile yazılır; OnGUI'ye yeni çizim eklenmez (bkz. KURULUM.md).
    /// </summary>
    public partial class GameUI
    {
        UIDocument uiDoc;
        PanelSettings uiPanel;
        VisualElement uiKok, katHud, katPencere, katUst, katIpucu, uiHaritaBosluk;
        Label uiIpucu;
        bool uiHazir;
        float uiTikZamani;

        /// <summary>UI Toolkit'e taşınan pencere sekmeleri. Burada olmayan sekmenin içi hâlâ IMGUI ile çizilir.</summary>
        readonly Dictionary<Tab, UiSekme> uiSekmeler = new Dictionary<Tab, UiSekme>();

        /// <summary>Bir pencere sekmesinin UI Toolkit hali: ağacı bir kez kurar, açıkken yenilenir.</summary>
        abstract class UiSekme
        {
            protected GameUI ui;
            public VisualElement kok;
            public void Baslat(GameUI g)
            {
                ui = g;
                kok = new VisualElement();
                kok.AddToClassList("sekme-icerik");
                Kur();
            }
            protected abstract void Kur();
            /// <summary>Sekmenin kaymayan üst kısmı (filtreler, sıralama): pencerede kaydırılan alanın üstünde durur. Yoksa null.</summary>
            public VisualElement ust;
            protected VisualElement Ust()
            {
                if (ust == null) { ust = new VisualElement(); ust.AddToClassList("sekme-ust"); }
                return ust;
            }
            /// <summary>Sekme açıkken 8 Hz'de çağrılır.</summary>
            public abstract void Yenile();
            /// <summary>Sekme her açıldığında.</summary>
            public virtual void Acildi() { }
        }

        /// <summary>Arayüz ölçeği: ekran yüksekliği 900'e göre × oyuncunun seçtiği boyut (OnGUI ile ortak).</summary>
        float UiOlcek() => Mathf.Clamp(Mathf.Clamp(Screen.height / 900f, 0.8f, 2f) * UiSizeFactor, 0.7f, 2.4f);

        const string YeniArayuzKey = "KuryeTycoon.YeniArayuz";
        static int yeniArayuzCache = -1;

        /// <summary>
        /// Yeni arayüz (UI Toolkit) açık mı? Geçiş döneminde kaçış yolu: F8 eski IMGUI arayüzüne döner (bu bilgisayarda kalıcı).
        /// </summary>
        static bool YeniArayuz
        {
            get
            {
                if (yeniArayuzCache < 0) yeniArayuzCache = PlayerPrefs.GetInt(YeniArayuzKey, 1);
                return yeniArayuzCache == 1;
            }
            set
            {
                yeniArayuzCache = value ? 1 : 0;
                PlayerPrefs.SetInt(YeniArayuzKey, yeniArayuzCache);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Yeni arayüz kuruldu ve açık: IMGUI'deki eski çubuklar ve pencere başlığı çizilmez.</summary>
        bool UiAktif => uiHazir && YeniArayuz;

        bool UiSerbest =>
            (EventManager.I == null || EventManager.I.Current == null) && !pauseMenuOpen && !ConfirmOpen && !BigCardOpen;

        /// <summary>Update'ten: kurulum, ölçek, görünürlük ve yenileme.</summary>
        void UiGuncelle()
        {
            bool basladi = GameBootstrap.I != null && GameBootstrap.I.Started;
            if (!uiHazir)
            {
                if (!basladi || uiKurulamadi) return;
                try { UiKur(); }
                catch (Exception ex)
                {
                    // Kurulamadıysa eski (IMGUI) arayüzle devam edilir: oyun oynanabilir kalsın
                    uiKurulamadi = true;
                    Debug.LogException(ex);
                    if (uiDoc != null) uiDoc.gameObject.SetActive(false);
                    return;
                }
                if (!uiHazir) return;
            }

            float s = UiOlcek();
            uiScale = s;
            if (Mathf.Abs(uiPanel.scale - s) > 0.0005f) uiPanel.scale = s;

            bool goster = basladi && !fotoModu && YeniArayuz;
            U.Goster(uiKok, goster);
            if (!goster) { U.Goster(uiIpucu, false); return; }
            // Ortadaki kutu açıkken alttaki çubuklar ve pencere tıklanmaz; üst katman (kutular, bildirimler, rehber) açık kalır
            bool serbest = UiSerbest;
            if (katHud.enabledSelf != serbest) { katHud.SetEnabled(serbest); katPencere.SetEnabled(serbest); }
            if (!serbest && !(BigCardOpen && UiAcikModal == UiModalTur.Yok)) U.Goster(uiIpucu, false);

            float t = Time.unscaledTime;
            bool agir = t >= uiTikZamani;
            if (agir) uiTikZamani = t + 0.125f;
            try
            {
                UiUstYenile(agir);
                UiDockYenile(agir);
                UiPencereYenile(agir);
                UiHaritaYenile(agir);
                UiOlaylarYenile(agir);
                UiHedefYenile(agir);
                UiBildirimYenile();
                UiRehberYenile();
            }
            catch (Exception ex) { UiHata("çubuklar ve pencere", ex); }
            // Haritaya bağlı kartlar ayrı ayrı: birinde hata olursa o kart kapanır, olay kartı ve Esc menüsü çalışmaya devam eder
            try { UiTelefonYenile(agir); }   // kayma ve harita her karede
            catch (Exception ex) { UiHata("kurye telefonu", ex); popupCourier = null; if (telKok != null) U.Goster(telKok, false); }
            try { UiBinaKartiYenile(agir); }
            catch (Exception ex) { UiHata("bina kartı", ex); CloseInfo(); if (binaKok != null) U.Goster(binaKok, false); }
            try { UiBuyukKartYenile(agir); }
            catch (Exception ex) { UiHata("büyük kurye kartı", ex); CloseBigCard(); if (bkPerde != null) U.Goster(bkPerde, false); }
            try { UiModalYenile(agir); }
            catch (Exception ex) { UiHata("olay kartı / menü / onay", ex); }
        }

        bool uiKurulamadi;
        readonly HashSet<string> uiHatalari = new HashSet<string>();

        /// <summary>Arayüzde bir hata oyunu durdurmasın: her yer için bir kez, yeriyle birlikte yazılır (test raporuna düşer).</summary>
        void UiHata(string yer, Exception ex)
        {
            if (!uiHatalari.Add(yer)) return;
            Debug.LogError($"Arayüz hatası ({yer}): {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }

        void UiKur()
        {
            if (uiDoc == null)
            {
                uiPanel = ScriptableObject.CreateInstance<PanelSettings>();
                uiPanel.name = "Kurye Arayüzü";
                uiPanel.scaleMode = PanelScaleMode.ConstantPixelSize;
                uiPanel.scale = UiOlcek();
                uiPanel.sortingOrder = 100;
                uiPanel.clearColor = false;
                var tema = Resources.Load<ThemeStyleSheet>("UI/Arayuz/Tema");
                if (tema != null) uiPanel.themeStyleSheet = tema;
                else Debug.LogWarning("Arayüz teması bulunamadı: Resources/UI/Arayuz/Tema.tss");

                var go = new GameObject("Arayüz (UI Toolkit)");
                go.SetActive(false);
                go.transform.SetParent(transform, false);
                uiDoc = go.AddComponent<UIDocument>();
                uiDoc.panelSettings = uiPanel;
                go.SetActive(true);
            }
            var root = uiDoc.rootVisualElement;
            if (root == null) return;   // panel bir sonraki karede hazır olur

            var stil = Resources.Load<StyleSheet>("UI/Arayuz/Kurye");
            if (stil != null) root.styleSheets.Add(stil);
            else Debug.LogError("Arayüz stili bulunamadı: Resources/UI/Arayuz/Kurye.uss");
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = 0; root.style.top = 0; root.style.right = 0; root.style.bottom = 0;
            var font = UITheme.UIFont;
            if (font != null) root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(font));

            uiKok = U.Gecirgen(root, "kok");
            katHud = U.Gecirgen(uiKok, "katman");
            katPencere = U.Gecirgen(uiKok, "katman");
            katUst = U.Gecirgen(uiKok, "katman");
            katIpucu = U.Gecirgen(uiKok, "katman");
            uiIpucu = U.Yazi(katIpucu, "", "ipucu");
            U.Goster(uiIpucu, false);
            // Herhangi bir tıklama ipucunu kapatır
            uiKok.RegisterCallback<PointerDownEvent>(_ => U.Goster(uiIpucu, false), TrickleDown.TrickleDown);

            // Pencere haritayı sağa ittiğinde sol menü ile harita arasındaki şerit (pencerenin arkası)
            uiHaritaBosluk = U.Gecirgen(katHud);
            uiHaritaBosluk.style.position = Position.Absolute;
            uiHaritaBosluk.style.left = DockW;
            uiHaritaBosluk.style.top = TopBar;
            uiHaritaBosluk.style.bottom = 0;
            uiHaritaBosluk.style.backgroundColor = new Color(0.06f, 0.066f, 0.08f, 1f);
            uiHaritaBosluk.style.borderRightWidth = 1;
            uiHaritaBosluk.style.borderRightColor = new Color(1f, 1f, 1f, 0.07f);

            UiUstKur();
            UiDockKur();
            UiHaritaKur();
            UiOlaylarKur();
            UiHedefKur();
            UiPencereKur();
            UiTelefonKur();      // pencere katmanında, pencerenin üstünde
            UiBinaKartiKur();
            UiBildirimKur();
            UiRehberKur();
            UiBuyukKartKur();    // üst katmanda, bildirim ve rehberin üstünde
            UiModalKur();   // en son: perde her şeyin üstünde
            uiHazir = true;
        }

        // ------------------------------------------------------------------ ipucu

        /// <summary>Elemanın üstüne gelince küçük açıklama kutusu (metin her seferinde yeniden sorulur).</summary>
        void Ipucu(VisualElement e, Func<string> metin)
        {
            if (e.pickingMode == PickingMode.Ignore) e.pickingMode = PickingMode.Position;
            e.RegisterCallback<PointerEnterEvent>(ev => IpucuGoster(metin(), ev.position));
            e.RegisterCallback<PointerMoveEvent>(ev => { if (U.Gorunur(uiIpucu)) IpucuKonum(ev.position); });
            e.RegisterCallback<PointerLeaveEvent>(_ => U.Goster(uiIpucu, false));
        }

        void Ipucu(VisualElement e, string metin) => Ipucu(e, () => metin);

        void IpucuGoster(string m, Vector2 p)
        {
            // Ortada bir kutu açıkken arkadakilerin ipucu çıkmaz; büyük kurye kartının kendi ipuçları (yıldızlar, rozet) çıkar
            bool serbest = UiSerbest || (BigCardOpen && UiAcikModal == UiModalTur.Yok);
            if (string.IsNullOrEmpty(m) || !serbest) { U.Goster(uiIpucu, false); return; }
            U.Metin(uiIpucu, m);
            U.Goster(uiIpucu, true);
            IpucuKonum(p);
        }

        void IpucuKonum(Vector2 p)
        {
            float w = uiKok.layout.width, h = uiKok.layout.height;
            float x = p.x + 16f, y = p.y + 18f;
            if (x + 330f > w) x = Mathf.Max(4f, p.x - 334f);
            if (y + 90f > h) y = Mathf.Max(4f, p.y - 80f);
            uiIpucu.style.left = x;
            uiIpucu.style.top = y;
        }

        // ------------------------------------------------------------------ IMGUI ile birlikte çalışma

        /// <summary>Fare (GUI koordinatı) bir UI Toolkit elemanının üstünde mi? Haritayı kaydırma/yakınlaştırma buna bakar.</summary>
        bool UiUzerinde(Vector2 guiPos)
        {
            if (!UiAktif || uiKok == null || uiKok.panel == null || !U.Gorunur(uiKok)) return false;
            var e = uiKok.panel.Pick(guiPos);
            return e != null && e != uiKok && uiKok.Contains(e);
        }

        /// <summary>Bir yazı kutusu odakta mı? (Klavye kısayolları o sırada çalışmaz.)</summary>
        bool UiYaziOdakta
        {
            get
            {
                if (!uiHazir || uiKok == null || uiKok.panel == null) return false;
                var fe = uiKok.panel.focusController?.focusedElement as VisualElement;
                return fe != null && (fe is TextField || fe.GetFirstAncestorOfType<TextField>() != null);
            }
        }

        /// <summary>Sekme UI Toolkit'e taşındı mı (o zaman IMGUI içeriği çizilmez).</summary>
        bool UiSekmeVar(Tab t) => UiAktif && uiSekmeler.ContainsKey(t);

        void UiSekmeEkle(Tab t, UiSekme s)
        {
            s.Baslat(this);
            uiSekmeler[t] = s;
        }

        /// <summary>Elemanın satır içi arka plan rengini kaldırır (USS'teki :hover yeniden çalışsın).</summary>
        static void ZeminSil(VisualElement e)
        {
            if (e.style.backgroundColor.keyword != StyleKeyword.Null) e.style.backgroundColor = StyleKeyword.Null;
        }

        static void RenkSil(VisualElement e)
        {
            if (e.style.color.keyword != StyleKeyword.Null) e.style.color = StyleKeyword.Null;
        }

        /// <summary>
        /// İçerik kadar uzayan kartlar (bina kartı, büyük kurye kartı): kaydırma alanına açık yükseklik verir, en fazla enFazla.
        /// Yükseklik verilmeyip kart en fazla yükseklikle sınırlanınca UI Toolkit içeriği sıkıştırıyor, satırlar üst üste biniyordu
        /// (16.11 testi, bina kartı). İçeriğin boyu son çocuğun alt kenarından okunur (içerik kabı görünür alan kadar uzayabilir).
        /// </summary>
        static void KaydirmaYuksekligi(ScrollView k, float dolgu, float enFazla)
        {
            var cc = k.contentContainer;
            if (cc.childCount == 0) return;
            var son = cc[cc.childCount - 1];
            float icerik = son.layout.yMax + son.resolvedStyle.marginBottom;
            if (!(icerik > 1f) || !(enFazla > 1f)) return;   // ilk karede düzen henüz yok (NaN)
            float h = Mathf.Clamp(icerik + dolgu, 40f, Mathf.Max(40f, enFazla));
            if (Mathf.Abs(k.resolvedStyle.height - h) > 0.5f) k.style.height = h;
        }

        float UiEkranGenisligi => uiKok != null && uiKok.layout.width > 1f ? uiKok.layout.width : Screen.width / Mathf.Max(0.01f, uiScale);

        float UiEkranYuksekligi => uiKok != null && uiKok.layout.height > 1f ? uiKok.layout.height : Screen.height / Mathf.Max(0.01f, uiScale);
    }
}
