using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Gelişim ağacı ekranı (UI Toolkit, tam ekran, pencere katmanının üstünde kendi katmanı). Gelişim sekmesi = bu ekran; üstte aynı
    /// menünün sekmeleri (Finans, Grafik), solda şirket değeri ve satış. Ortadan dışa yayılan ağaç (GelisimAgaci): sürükle = kaydır, tekerlek = yakınlaştır, üstüne gel = bilgi
    /// kutusu, tıkla = yükselt. Efektler:
    ///  • alınabilir düğüm yavaşça nabız gibi atar · en üst seviye yeşil hale ile parlar
    ///  • satın alınca düğüm büyüyüp söner, halka yayılır, parçacıklar saçılır, merkezden düğüme ışık akar, ses çalar
    ///  • "?" düğüm açılınca soru işareti dönerek kaybolur, ikon büyüyerek belirir
    ///  • para yetmezse ya da kilitliyse düğüm sallanır, fiyat çubuğu kırmızı yanıp söner
    ///  • bilgi kutusu yumuşakça açılır
    /// Çizgiler Painter2D ile tek bir elemanda çizilir.
    /// </summary>
    public partial class GameUI
    {
        bool agacAcik;
        VisualElement agacPerde, agacTuval, agacCizgi, agacEfekt, agacBilgi, agacBar, agacBarDolgu;
        Label agacDeger, agacEngel;
        Button agacSat;
        Label agacKasa, agacKar, agacCarpan, agacMarka, agacBilgiAd, agacBilgiDeger, agacBilgiAcik, agacBilgiSeviye, agacBarYazi, agacBilgiAlt;
        float agacZoom = 0.85f;
        Vector2 agacKay;
        bool agacKayHazir, agacBasildi, agacSurukluyor, agacIlkYenileme = true;
        Vector2 agacBasPos, agacBasKay;
        float agacAgirZaman, agacBilgiAcilis, agacBarFlas = -10f;
        AgacGorsel agacSecili, agacUstunde;
        readonly List<AgacGorsel> agacGorseller = new List<AgacGorsel>();
        readonly Dictionary<string, AgacGorsel> agacSozluk = new Dictionary<string, AgacGorsel>();
        readonly List<AgacParcacik> agacParcaciklar = new List<AgacParcacik>();
        readonly List<Vector2> agacAkisYolu = new List<Vector2>();
        float agacAkisBas = -10f;

        const float AgacOrtaX = 720f, AgacOrtaY = 470f, AgacTuvalW = 1440f, AgacTuvalH = 940f;
        static readonly Color AgYesil = AgRenk(155, 227, 107, 255);
        static readonly Color AgBeyaz = AgRenk(242, 245, 247, 255);
        static readonly Color AgKirmizi = AgRenk(239, 90, 95, 255);
        static readonly Color AgGri = AgRenk(61, 69, 80, 255);
        static readonly Color AgZemin = AgRenk(50, 57, 68, 255);
        static readonly Color AgGizliZemin = AgRenk(42, 47, 55, 255);
        static readonly Color AgSolukCizgi = AgRenk(75, 102, 112, 255);
        static readonly Color AgPerde = AgRenk(24, 28, 35, 255);

        /// <summary>Ağaç ekranda (OnGUI haritayı ve IMGUI çizimlerini atlar).</summary>
        bool AgacGorunur => agacAcik && agacPerde != null && U.Gorunur(agacPerde);

        enum AgDurum { Merkez, Gizli, Max, Kilit, Alinir, Pahali }

        class AgacGorsel
        {
            public AgacDugum d;
            public VisualElement halo, kok, ikon;
            public Label soru, seviye;
            public AgDurum durum;
            public bool goruldu;            // açık hali en az bir kez gösterildi (açılma efekti için)
            public float acilma = -10f, patlama = -10f, sallanma = -10f;
            public float boy;
        }

        class AgacParcacik
        {
            public VisualElement e;
            public Vector2 p, v;
            public float bas, omur;
            public bool halka;
        }

        /// <summary>Color32 → Color (stil alanları Color32 kabul etmiyor).</summary>
        static Color AgRenk(byte r, byte g, byte b, byte a) => new Color32(r, g, b, a);

        static Vector2 AgacYer(Vector2 konum) => new Vector2(konum.x + AgacOrtaX, konum.y + AgacOrtaY);

        static void Cerceve(VisualElement e, float w, Color c, float r)
        {
            e.style.borderTopWidth = w; e.style.borderBottomWidth = w; e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
            e.style.borderTopColor = c; e.style.borderBottomColor = c; e.style.borderLeftColor = c; e.style.borderRightColor = c;
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r; e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

        static void CerceveRengi(VisualElement e, Color c)
        {
            if (e.style.borderTopColor == c) return;
            e.style.borderTopColor = c; e.style.borderBottomColor = c; e.style.borderLeftColor = c; e.style.borderRightColor = c;
        }

        static void Mutlak(VisualElement e, float x, float y, float w, float h)
        {
            e.style.position = Position.Absolute;
            e.style.left = x; e.style.top = y;
            if (w > 0) e.style.width = w;
            if (h > 0) e.style.height = h;
        }

        // ------------------------------------------------------------------ kurulum

        void UiAgacKur()
        {
            // Kendi katmanı: pencere katmanının hemen üstü, üst katmanın (olay kartı, onay) altı. Pencere katmanı onay açıkken
            // soluklaşıyor (SetEnabled); ağaç ondan etkilenmesin, arkası hiç görünmesin.
            agacPerde = new VisualElement();
            uiKok.Insert(uiKok.IndexOf(katPencere) + 1, agacPerde);
            agacPerde.name = "gelisim-agaci";
            Mutlak(agacPerde, 0, 0, -1, -1);
            agacPerde.style.right = 0; agacPerde.style.bottom = 0;
            agacPerde.style.backgroundColor = AgPerde;
            agacPerde.style.overflow = Overflow.Hidden;
            agacPerde.pickingMode = PickingMode.Position;

            // Yıldızlar (süs)
            var rnd = new System.Random(7);
            for (int i = 0; i < 90; i++)
            {
                var y = U.Gecirgen(agacPerde);
                float s = rnd.Next(2, 5);
                y.style.position = Position.Absolute;
                y.style.left = Length.Percent((float)rnd.NextDouble() * 100f);
                y.style.top = Length.Percent((float)rnd.NextDouble() * 100f);
                y.style.width = s; y.style.height = s;
                Cerceve(y, 0, Color.clear, s);
                y.style.backgroundColor = new Color(0.55f, 0.78f, 0.74f, 0.15f + (float)rnd.NextDouble() * 0.3f);
            }

            agacTuval = U.Gecirgen(agacPerde);
            Mutlak(agacTuval, 0, 0, AgacTuvalW, AgacTuvalH);
            agacTuval.style.transformOrigin = new TransformOrigin(0, 0);

            agacCizgi = U.Gecirgen(agacTuval);
            Mutlak(agacCizgi, 0, 0, AgacTuvalW, AgacTuvalH);
            agacCizgi.generateVisualContent += AgacCizgileriCiz;

            foreach (var d in GelisimAgaci.Dugumler) AgacDugumKur(d);

            agacEfekt = U.Gecirgen(agacTuval);
            Mutlak(agacEfekt, 0, 0, AgacTuvalW, AgacTuvalH);

            // Sol üst: kaynaklar
            var kay = U.Gecirgen(agacPerde);
            Mutlak(kay, 28, 24, -1, -1);
            kay.style.flexDirection = FlexDirection.Column;
            agacKasa = AgacKaynak(kay, Icons.Kind.Wallet);
            agacKar = AgacKaynak(kay, Icons.Kind.Chart);
            agacCarpan = AgacKaynak(kay, Icons.Kind.Bolt);
            agacMarka = AgacKaynak(kay, Icons.Kind.Star);

            // Şirket değeri ve satış (prestij)
            var deg = U.El(kay);
            deg.style.marginTop = 8;
            deg.style.width = 300;
            deg.style.paddingLeft = 14; deg.style.paddingRight = 14; deg.style.paddingTop = 10; deg.style.paddingBottom = 12;
            deg.style.backgroundColor = AgRenk(34, 27, 43, 235);
            Cerceve(deg, 1, AgRenk(74, 58, 92, 255), 12);
            agacDeger = U.Yazi(deg, "", "kucuk");
            agacDeger.style.color = AgBeyaz; agacDeger.style.whiteSpace = WhiteSpace.Normal;
            agacEngel = U.Yazi(deg, "", "minik");
            agacEngel.style.color = U.Uyari; agacEngel.style.whiteSpace = WhiteSpace.Normal; agacEngel.style.marginTop = 4;
            agacSat = U.Dugme(deg, "", AgacSatisSor, "ilk");
            agacSat.style.marginTop = 8;

            // Sağ üst: başlık, liste, kapat
            var ust = U.Gecirgen(agacPerde);
            ust.style.position = Position.Absolute;
            ust.style.right = 28; ust.style.top = 24;
            ust.style.flexDirection = FlexDirection.Row;
            ust.style.alignItems = Align.Center;
            var baslik = U.Kalin(ust, "Gelişim");
            baslik.style.fontSize = 28;
            baslik.style.color = AgRenk(28, 32, 40, 255);
            baslik.style.backgroundColor = AgRenk(233, 237, 240, 255);
            Cerceve(baslik, 0, Color.clear, 28);
            baslik.style.paddingLeft = 28; baslik.style.paddingRight = 28; baslik.style.paddingTop = 8; baslik.style.paddingBottom = 8;
            baslik.style.marginRight = 12;
            // Aynı menünün diğer sekmeleri (Finans, Grafik): ağaç tam ekran olduğu için sekmeler burada
            var grup = MenuGroups[GroupIndexOf(Tab.Gelistirmeler)];
            for (int i = 0; i < grup.tabs.Length; i++)
            {
                var t = grup.tabs[i];
                if (t == Tab.Gelistirmeler) continue;
                var sd = U.Dugme(ust, grup.tabLabels[i], () => { agacAcik = false; SelectSubTab(t); }, "kucuk-dg");
                sd.style.marginRight = 8;
            }
            var kapat = U.Dugme(ust, "Kapat", () => { agacAcik = false; panelOpen = false; }, "kucuk-dg");
            Ipucu(kapat, "Gelişimi kapat");

            // Sol alt: gösterim
            var gos = U.Gecirgen(agacPerde);
            gos.style.position = Position.Absolute;
            gos.style.left = 28; gos.style.bottom = 24;
            AgacGosterim(gos, AgYesil, "En üst seviye");
            AgacGosterim(gos, AgBeyaz, "Alınabilir");
            AgacGosterim(gos, AgKirmizi, "Para yetmiyor");
            AgacGosterim(gos, AgGri, "Kilitli · \"?\" komşusunu alınca açılır");
            var nasil = U.Yazi(gos, "Sürükle: kaydır · Tekerlek: yakınlaştır · Tıkla: yükselt", "minik");
            nasil.style.color = AgRenk(169, 179, 188, 255);
            nasil.style.marginTop = 6;

            // Sağ alt: bilgi kutusu
            agacBilgi = U.Gecirgen(agacPerde);
            agacBilgi.style.position = Position.Absolute;
            agacBilgi.style.right = 28; agacBilgi.style.bottom = 24;
            agacBilgi.style.width = 320;
            agacBilgi.style.backgroundColor = AgRenk(22, 26, 33, 245);
            Cerceve(agacBilgi, 1, AgRenk(47, 55, 66, 255), 16);
            agacBilgi.style.paddingLeft = 20; agacBilgi.style.paddingRight = 20; agacBilgi.style.paddingTop = 16; agacBilgi.style.paddingBottom = 16;
            agacBilgi.style.alignItems = Align.Center;
            agacBilgiAd = U.Kalin(agacBilgi, "");
            agacBilgiAd.style.fontSize = 16; agacBilgiAd.style.color = AgBeyaz;
            agacBilgiAd.style.unityTextAlign = TextAnchor.MiddleCenter; agacBilgiAd.style.whiteSpace = WhiteSpace.Normal;
            agacBilgiDeger = U.Kalin(agacBilgi, "");
            agacBilgiDeger.style.fontSize = 15; agacBilgiDeger.style.marginTop = 6;
            agacBilgiDeger.style.unityTextAlign = TextAnchor.MiddleCenter; agacBilgiDeger.style.whiteSpace = WhiteSpace.Normal;
            AgacAyrac(agacBilgi);
            agacBilgiAcik = U.Yazi(agacBilgi, "", "kucuk");
            agacBilgiAcik.style.color = AgRenk(213, 219, 224, 255);
            agacBilgiAcik.style.unityTextAlign = TextAnchor.MiddleCenter; agacBilgiAcik.style.whiteSpace = WhiteSpace.Normal;
            AgacAyrac(agacBilgi);
            agacBilgiSeviye = U.Kalin(agacBilgi, "");
            agacBilgiSeviye.style.fontSize = 14; agacBilgiSeviye.style.color = AgBeyaz;
            agacBar = U.Gecirgen(agacBilgi);
            agacBar.style.marginTop = 10;
            agacBar.style.width = Length.Percent(100); agacBar.style.height = 36;
            agacBar.style.backgroundColor = AgRenk(40, 46, 56, 255);
            Cerceve(agacBar, 0, Color.clear, 8);
            agacBar.style.overflow = Overflow.Hidden;
            agacBarDolgu = U.Gecirgen(agacBar);
            Mutlak(agacBarDolgu, 0, 0, -1, -1);
            agacBarDolgu.style.bottom = 0;
            agacBarYazi = U.Kalin(agacBar, "");
            Mutlak(agacBarYazi, 0, 0, -1, -1);
            agacBarYazi.style.right = 0; agacBarYazi.style.bottom = 0;
            agacBarYazi.style.unityTextAlign = TextAnchor.MiddleCenter;
            agacBarYazi.style.color = AgBeyaz; agacBarYazi.style.fontSize = 15;
            agacBilgiAlt = U.Yazi(agacBilgi, "", "minik");
            agacBilgiAlt.style.color = AgRenk(169, 179, 188, 255);
            agacBilgiAlt.style.marginTop = 6;
            agacBilgiAlt.style.unityTextAlign = TextAnchor.MiddleCenter; agacBilgiAlt.style.whiteSpace = WhiteSpace.Normal;

            // Kaydırma ve yakınlaştırma
            agacPerde.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0 && e.button != 1 && e.button != 2) return;
                agacBasildi = true; agacSurukluyor = false;
                agacBasPos = e.localPosition; agacBasKay = agacKay;
            }, TrickleDown.TrickleDown);
            agacPerde.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!agacBasildi) return;
                Vector2 fark = (Vector2)e.localPosition - agacBasPos;
                if (!agacSurukluyor && fark.magnitude > 6f) { agacSurukluyor = true; agacPerde.CapturePointer(e.pointerId); }
                if (agacSurukluyor) agacKay = agacBasKay + fark;
            });
            agacPerde.RegisterCallback<PointerUpEvent>(e =>
            {
                agacBasildi = false;
                if (agacPerde.HasPointerCapture(e.pointerId)) agacPerde.ReleasePointer(e.pointerId);
            });
            agacPerde.RegisterCallback<WheelEvent>(e =>
            {
                float yeni = Mathf.Clamp(agacZoom * (e.delta.y > 0 ? 0.9f : 1.1f), 0.45f, 1.6f);
                Vector2 m = e.localMousePosition;
                Vector2 q = (m - agacKay) / agacZoom;
                agacZoom = yeni;
                agacKay = m - q * agacZoom;
                e.StopPropagation();
            });
        }

        void AgacSatisSor()
        {
            long deger = Marka.SirketDegeri;
            int p = Marka.PuanFor(deger);
            string sonraki = Marka.Sehirler[Mathf.Min(Marka.Veri.satilan + 1, Marka.Sehirler.Length - 1)];
            AskConfirm("Şirketi sat",
                $"{CompanyProfile.Name} {Ft(deger)} değerle satılacak. Karşılığında {p} marka puanı kazanırsın. " +
                $"Bu oyun biter ve kaydı silinir; {sonraki} şehrinde yeni bir şirketle baştan başlarsın. Marka puanlarını kurulum ekranında " +
                "kalıcı geliştirmelere harcarsın. Ayrıca her satılan şirket komisyon gelirini kalıcı olarak %5 artırır.",
                "Evet, sat ve yeni şehre geç", () =>
                {
                    int alinan = Marka.Sat();
                    if (alinan > 0) deferred = () => GameBootstrap.Restart();
                });
        }

        Label AgacKaynak(VisualElement ust, Icons.Kind k)
        {
            var s = U.Gecirgen(ust);
            s.style.flexDirection = FlexDirection.Row;
            s.style.alignItems = Align.Center;
            s.style.marginBottom = 8;
            U.Simge(s, k, Color.white, 26f).style.marginRight = 10;
            var l = U.Kalin(s, "");
            l.style.fontSize = 22; l.style.color = AgBeyaz;
            return l;
        }

        void AgacGosterim(VisualElement ust, Color c, string metin)
        {
            var s = U.Gecirgen(ust);
            s.style.flexDirection = FlexDirection.Row;
            s.style.alignItems = Align.Center;
            s.style.marginTop = 4;
            var k = U.Gecirgen(s);
            k.style.width = 16; k.style.height = 16; k.style.marginRight = 10;
            Cerceve(k, 2, c, 5);
            var l = U.Yazi(s, metin, "kucuk");
            l.style.color = AgRenk(201, 209, 216, 255);
        }

        static void AgacAyrac(VisualElement ust)
        {
            var a = U.Gecirgen(ust);
            a.style.width = Length.Percent(100); a.style.height = 1;
            a.style.backgroundColor = AgRenk(47, 55, 66, 255);
            a.style.marginTop = 10; a.style.marginBottom = 10;
        }

        void AgacDugumKur(AgacDugum d)
        {
            var g = new AgacGorsel { d = d, boy = d.id == GelisimAgaci.Merkez ? 72f : (d.buyuk ? 56f : 46f) };
            Vector2 p = AgacYer(d.konum);
            g.halo = U.Gecirgen(agacTuval);
            float hb = g.boy + 22f;
            Mutlak(g.halo, p.x - hb / 2f, p.y - hb / 2f, hb, hb);
            Cerceve(g.halo, 0, Color.clear, d.buyuk ? 22f : 30f);
            g.halo.style.backgroundColor = U.A(AgYesil, 0.35f);

            g.kok = U.El(agacTuval);
            Mutlak(g.kok, p.x - g.boy / 2f, p.y - g.boy / 2f, g.boy, g.boy);
            g.kok.style.alignItems = Align.Center;
            g.kok.style.justifyContent = Justify.Center;
            g.kok.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            Cerceve(g.kok, 3, AgGri, d.buyuk ? 14f : 23f);
            g.kok.style.backgroundColor = AgZemin;
            g.kok.pickingMode = PickingMode.Position;
            g.ikon = U.Simge(g.kok, d.simge, Color.white, g.boy * 0.55f);
            g.ikon.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            g.soru = U.Kalin(g.kok, "?");
            Mutlak(g.soru, 0, 0, -1, -1);
            g.soru.style.right = 0; g.soru.style.bottom = 0;
            g.soru.style.unityTextAlign = TextAnchor.MiddleCenter;
            g.soru.style.fontSize = 26; g.soru.style.color = AgRenk(108, 118, 130, 255);
            g.soru.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));

            g.seviye = U.Kalin(agacTuval, "");
            Mutlak(g.seviye, p.x - 40f, p.y + g.boy / 2f + 4f, 80f, 16f);
            g.seviye.style.unityTextAlign = TextAnchor.MiddleCenter;
            g.seviye.style.fontSize = 11; g.seviye.style.color = AgRenk(201, 209, 216, 255);

            g.kok.RegisterCallback<PointerEnterEvent>(_ => { agacUstunde = g; AgacSec(g); });
            g.kok.RegisterCallback<PointerLeaveEvent>(_ => { if (agacUstunde == g) agacUstunde = null; });
            g.kok.RegisterCallback<ClickEvent>(e =>
            {
                if (e.button != 0 || agacSurukluyor) return;
                AgacTikla(g);
                e.StopPropagation();
            });
            agacGorseller.Add(g);
            agacSozluk[d.id] = g;
        }

        void AgacSec(AgacGorsel g)
        {
            if (agacSecili == g) return;
            agacSecili = g;
            agacBilgiAcilis = Time.unscaledTime;
            AgacBilgiYaz();
        }

        // ------------------------------------------------------------------ durum

        AgDurum AgacDurumu(AgacGorsel g)
        {
            var d = g.d;
            var up = UpgradeManager.I;
            var e = EconomyManager.I;
            if (!GelisimAgaci.Acik(d)) return AgDurum.Gizli;
            if (d.buyuk)
            {
                int lv = up.Level(d.kind), mx = UpgradeManager.Def(d.kind).MaxLevel;
                if (lv >= mx) return d.id == GelisimAgaci.Merkez ? AgDurum.Merkez : AgDurum.Max;
                if (up.IsLocked(d.kind)) return d.id == GelisimAgaci.Merkez ? AgDurum.Merkez : AgDurum.Kilit;
                if (d.id == GelisimAgaci.Merkez) return AgDurum.Merkez;
                return e.FreeCash >= up.NextCost(d.kind) ? AgDurum.Alinir : AgDurum.Pahali;
            }
            if (GelisimAgaci.Sahip(d.id)) return AgDurum.Max;
            return e.FreeCash >= d.bedel ? AgDurum.Alinir : AgDurum.Pahali;
        }

        int AgacSeviye(AgacDugum d, out int mx)
        {
            if (d.buyuk) { mx = UpgradeManager.Def(d.kind).MaxLevel; return UpgradeManager.I.Level(d.kind); }
            mx = 1;
            return GelisimAgaci.Sahip(d.id) ? 1 : 0;
        }

        long AgacBedel(AgacDugum d) => d.buyuk ? UpgradeManager.I.NextCost(d.kind) : (GelisimAgaci.Sahip(d.id) ? -1 : d.bedel);

        void AgacDurumYenile()
        {
            float t = Time.unscaledTime;
            foreach (var g in agacGorseller)
            {
                var yeni = AgacDurumu(g);
                bool acikti = g.goruldu;
                g.durum = yeni;
                bool gizli = yeni == AgDurum.Gizli;
                if (!gizli && !acikti)
                {
                    g.goruldu = true;
                    if (!agacIlkYenileme) g.acilma = t;   // ilk açılışta efekt yok, sonradan açılanda var
                }
                Color c = yeni == AgDurum.Max ? AgYesil : yeni == AgDurum.Alinir ? AgBeyaz : yeni == AgDurum.Pahali ? AgKirmizi
                        : yeni == AgDurum.Merkez ? UITheme.Accent : AgGri;
                CerceveRengi(g.kok, c);
                U.Zemin(g.kok, gizli ? AgGizliZemin : AgZemin);
                int lv = AgacSeviye(g.d, out int mx);
                string sv = gizli ? "" : g.d.buyuk && lv > 0 ? $"{lv}/{mx}" : (!g.d.buyuk && lv > 0 ? "alındı" : "");
                U.Metin(g.seviye, sv);
            }
            agacIlkYenileme = false;
            agacCizgi.MarkDirtyRepaint();
        }

        // ------------------------------------------------------------------ bilgi kutusu

        void AgacBilgiYaz()
        {
            var g = agacSecili;
            if (g == null)
            {
                U.Metin(agacBilgiAd, "GELİŞİM AĞACI");
                U.Metin(agacBilgiDeger, "Bir düğümün üstüne gel");
                U.Renk(agacBilgiDeger, GelisimAgaci.DalRenkleri[0]);
                U.Metin(agacBilgiAcik, "Merkezden dışa doğru büyü. Her düğüm yanındakileri açar; küçük düğümler ucuz, kalıcı bonuslar.");
                U.Metin(agacBilgiSeviye, "");
                U.Goster(agacBar, false);
                U.Metin(agacBilgiAlt, "");
                return;
            }
            var d = g.d;
            var up = UpgradeManager.I;
            Color dalRengi = d.dal >= 0 ? GelisimAgaci.DalRenkleri[d.dal] : UITheme.Accent;
            U.Renk(agacBilgiDeger, dalRengi);
            if (g.durum == AgDurum.Gizli)
            {
                U.Metin(agacBilgiAd, "?");
                U.Metin(agacBilgiDeger, "Bilinmiyor");
                U.Metin(agacBilgiAcik, $"Önce \"{GelisimAgaci.Bul(d.ust).Ad}\" al; sonra ne olduğu görünür.");
                U.Metin(agacBilgiSeviye, d.dal >= 0 ? GelisimAgaci.DalAdlari[d.dal].ToUpper() : "");
                U.Goster(agacBar, false);
                U.Metin(agacBilgiAlt, "");
                return;
            }
            U.Goster(agacBar, true);
            int lv = AgacSeviye(d, out int mx);
            if (d.buyuk)
            {
                var def = UpgradeManager.Def(d.kind);
                U.Metin(agacBilgiAd, def.name.ToUpper());
                U.Metin(agacBilgiDeger, lv < mx ? $"{def.levelNames[lv]}  ›  {def.levelNames[lv + 1]}" : $"{def.levelNames[lv]} (en üst)");
                U.Metin(agacBilgiAcik, lv < mx ? $"Sonraki: {def.effect(lv + 1)}" + (lv > 0 ? $"\nŞimdi: {def.effect(lv)}" : "") : $"Şimdi: {def.effect(lv)}");
                U.Metin(agacBilgiSeviye, $"SEVİYE: {lv} / {mx}");
                int gNow = up.DailyCost(d.kind), gNext = lv < mx ? def.DailyAt(lv + 1) : gNow;
                string kilit = lv < mx ? up.LockReason(d.kind) : "";
                U.Metin(agacBilgiAlt, kilit.Length > 0 ? $"Kilitli: {kilit}"
                                     : lv < mx ? $"Günlük gider {Ft(gNow)} → {Ft(gNext)} · tıkla: yükselt" : "Bu dal tamam");
                U.Renk(agacBilgiAlt, kilit.Length > 0 ? U.Uyari : AgRenk(169, 179, 188, 255));
            }
            else
            {
                U.Metin(agacBilgiAd, d.ad.ToUpper());
                U.Metin(agacBilgiDeger, AgacBonusMetni(d));
                U.Metin(agacBilgiAcik, $"{GelisimAgaci.DalAdlari[d.dal]} dalında küçük, kalıcı bonus. Günlük gideri yok.");
                U.Metin(agacBilgiSeviye, lv > 0 ? "ALINDI" : "TEK SEFERLİK");
                U.Metin(agacBilgiAlt, lv > 0 ? "Bu düğüm tamam" : "Tıkla: al");
                U.Renk(agacBilgiAlt, AgRenk(169, 179, 188, 255));
            }
            AgacBarYaz();
        }

        static string AgacBonusMetni(AgacDugum d)
        {
            float simdi = GelisimAgaci.Bonus(d.tip);
            bool alindi = GelisimAgaci.Sahip(d.id);
            float once = alindi ? simdi - d.miktar : simdi, sonra = alindi ? simdi : simdi + d.miktar;
            if (d.tip == BonusTip.SenYokken) return $"Sen yokken +{once:0} saat  ›  +{sonra:0} saat";
            if (d.tip == BonusTip.Moral) return $"Moral +{once * 100f:0}  ›  +{sonra * 100f:0} puan";
            string ad = d.tip == BonusTip.Komisyon ? "Komisyon" : d.tip == BonusTip.Siparis ? "Sipariş" : d.tip == BonusTip.Hiz ? "Kurye hızı" : "Başvuru";
            return $"{ad} +%{once * 100f:0}  ›  +%{sonra * 100f:0}";
        }

        void AgacBarYaz()
        {
            var g = agacSecili;
            if (g == null || g.durum == AgDurum.Gizli) return;
            long bedel = AgacBedel(g.d);
            long serbest = EconomyManager.I.FreeCash;
            float dol; string yazi; Color renk;
            if (bedel < 0) { dol = 1f; yazi = "En üst seviye"; renk = AgRenk(47, 90, 51, 255); }
            else if (g.durum == AgDurum.Kilit) { dol = 0f; yazi = $"Kilitli · {Ft(bedel)}"; renk = AgGri; }
            else
            {
                dol = Mathf.Clamp01(bedel > 0 ? serbest / (float)bedel : 1f);
                yazi = $"{Ft(System.Math.Max(0, serbest))} / {Ft(bedel)}";
                renk = serbest >= bedel ? AgRenk(47, 90, 51, 255) : AgRenk(110, 40, 44, 255);
            }
            float flas = Time.unscaledTime - agacBarFlas;
            if (flas < 0.6f) renk = Color.Lerp(AgKirmizi, renk, flas / 0.6f);
            agacBarDolgu.style.width = Length.Percent(dol * 100f);
            U.Zemin(agacBarDolgu, renk);
            U.Metin(agacBarYazi, yazi);
        }

        // ------------------------------------------------------------------ tıklama

        void AgacTikla(AgacGorsel g)
        {
            AgacSec(g);
            AgacDurumYenile();
            AgacBilgiYaz();
            var d = g.d;
            float t = Time.unscaledTime;
            switch (g.durum)
            {
                case AgDurum.Gizli:
                case AgDurum.Kilit:
                    g.sallanma = t;
                    return;
                case AgDurum.Pahali:
                    g.sallanma = t;
                    agacBarFlas = t;
                    GameSounds.Play(Sfx.Kotu);
                    return;
                case AgDurum.Max:
                    return;
            }
            if (d.buyuk)
            {
                var up = UpgradeManager.I;
                int lv = up.Level(d.kind);
                int cost = up.NextCost(d.kind);
                if (cost < 0 || up.IsLocked(d.kind)) { g.sallanma = t; return; }
                var def = UpgradeManager.Def(d.kind);
                ConfirmIfExpensive(cost, "Geliştirme",
                    $"{def.name}: {def.LevelText(lv + 1)}. Kurulum {Ft(cost)}, sonra günlük {Ft(def.DailyAt(lv + 1))} gider. " +
                    $"Serbest kasan {Ft(EconomyManager.I.FreeCash)}. Kurulum bedeli geri gelmez.",
                    () => { if (up.Buy(d.kind)) AgacSatinAlindi(g); else { g.sallanma = Time.unscaledTime; agacBarFlas = Time.unscaledTime; } });
            }
            else if (GelisimAgaci.KucukAl(d)) AgacSatinAlindi(g);
            else { g.sallanma = t; agacBarFlas = t; GameSounds.Play(Sfx.Kotu); }
        }

        void AgacSatinAlindi(AgacGorsel g)
        {
            float t = Time.unscaledTime;
            g.patlama = t;
            int lv = AgacSeviye(g.d, out int mx);
            GameSounds.Play(lv >= mx ? Sfx.Buyuk : Sfx.Iyi);
            Vector2 p = AgacYer(g.d.konum);
            Color c = g.d.dal >= 0 ? GelisimAgaci.DalRenkleri[g.d.dal] : UITheme.Accent;
            // Halka
            var halka = U.Gecirgen(agacEfekt);
            Cerceve(halka, 3, AgBeyaz, 60f);
            agacParcaciklar.Add(new AgacParcacik { e = halka, p = p, bas = t, omur = 0.55f, halka = true });
            // Parçacıklar
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                float hiz = Random.Range(90f, 170f);
                var e = U.Gecirgen(agacEfekt);
                float s = Random.Range(4f, 8f);
                e.style.position = Position.Absolute;
                e.style.width = s; e.style.height = s;
                Cerceve(e, 0, Color.clear, s);
                e.style.backgroundColor = i % 3 == 0 ? AgBeyaz : c;
                agacParcaciklar.Add(new AgacParcacik { e = e, p = p, v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * hiz, bas = t, omur = Random.Range(0.5f, 0.8f) });
            }
            // Merkezden düğüme ışık akışı
            agacAkisYolu.Clear();
            foreach (var k in GelisimAgaci.Yol(g.d)) agacAkisYolu.Add(AgacYer(k));
            agacAkisBas = t;
            AgacDurumYenile();
            AgacBilgiYaz();
        }

        // ------------------------------------------------------------------ çizgiler (Painter2D)

        void AgacCizgileriCiz(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            p.lineCap = LineCap.Round;
            // Önce sönük, sonra parlak (alınmış) bağlar
            for (int gecis = 0; gecis < 2; gecis++)
                foreach (var d in GelisimAgaci.Dugumler)
                {
                    if (d.id == GelisimAgaci.Merkez) continue;
                    var ust = d.ust != null ? GelisimAgaci.Bul(d.ust) : GelisimAgaci.Bul(GelisimAgaci.Merkez);
                    if (ust == null) continue;
                    bool parlak = GelisimAgaci.Sahip(d.id) && GelisimAgaci.Sahip(ust.id);
                    if ((gecis == 1) != parlak) continue;
                    Vector2 a = AgacYer(ust.konum), b = AgacYer(d.konum);
                    if (parlak)
                    {
                        p.strokeColor = new Color(1f, 1f, 1f, 0.18f);
                        p.lineWidth = 9f;
                        p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke();
                        p.strokeColor = AgBeyaz;
                        p.lineWidth = 3f;
                    }
                    else
                    {
                        p.strokeColor = GelisimAgaci.Acik(d) ? AgSolukCizgi : U.A(AgSolukCizgi, 0.45f);
                        p.lineWidth = 2f;
                    }
                    p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke();
                }

            // Satın alma ışığı: yol boyunca akan parlak nokta ve iz
            float k = (Time.unscaledTime - agacAkisBas) / 0.7f;
            if (k >= 0f && k < 1f && agacAkisYolu.Count >= 2)
            {
                float toplam = 0f;
                for (int i = 1; i < agacAkisYolu.Count; i++) toplam += Vector2.Distance(agacAkisYolu[i - 1], agacAkisYolu[i]);
                Vector2 bas = YolNoktasi(Mathf.Max(0f, k - 0.25f) * toplam), son = YolNoktasi(k * toplam);
                p.strokeColor = new Color(1f, 1f, 1f, 0.55f);
                p.lineWidth = 7f;
                p.BeginPath(); p.MoveTo(bas); p.LineTo(son); p.Stroke();
                p.fillColor = Color.white;
                p.BeginPath(); p.Arc(son, 7f, 0f, 360f); p.Fill();
            }
        }

        Vector2 YolNoktasi(float uzaklik)
        {
            for (int i = 1; i < agacAkisYolu.Count; i++)
            {
                float l = Vector2.Distance(agacAkisYolu[i - 1], agacAkisYolu[i]);
                if (uzaklik <= l || i == agacAkisYolu.Count - 1)
                    return Vector2.Lerp(agacAkisYolu[i - 1], agacAkisYolu[i], l > 0f ? Mathf.Clamp01(uzaklik / l) : 1f);
                uzaklik -= l;
            }
            return agacAkisYolu[agacAkisYolu.Count - 1];
        }

        // ------------------------------------------------------------------ her kare

        void UiAgacYenile()
        {
            bool goster = agacAcik && panelOpen && tab == Tab.Gelistirmeler && GameBootstrap.I != null && GameBootstrap.I.Started;
            if (!goster)
            {
                if (agacPerde != null) U.Goster(agacPerde, false);
                if (!(panelOpen && tab == Tab.Gelistirmeler)) agacAcik = false;
                return;
            }
            if (agacPerde == null) UiAgacKur();
            U.Goster(agacPerde, true);
            // Ortada bir kutu (onay, olay kartı) açıkken ağaç tıklanmaz ama soluklaşmaz
            agacPerde.pickingMode = UiSerbest ? PickingMode.Position : PickingMode.Ignore;
            float t = Time.unscaledTime;

            // İlk açılışta ağacı ortala
            float pw = agacPerde.resolvedStyle.width, ph = agacPerde.resolvedStyle.height;
            if (!agacKayHazir && pw > 10f && ph > 10f)
            {
                agacKayHazir = true;
                agacZoom = Mathf.Clamp(Mathf.Min(pw / 1300f, ph / 900f), 0.5f, 1.2f);
                agacKay = new Vector2(pw / 2f - AgacOrtaX * agacZoom, ph / 2f - AgacOrtaY * agacZoom);
            }
            agacTuval.style.left = agacKay.x;
            agacTuval.style.top = agacKay.y;
            agacTuval.style.scale = new Scale(new Vector3(agacZoom, agacZoom, 1f));

            if (t >= agacAgirZaman)
            {
                agacAgirZaman = t + 0.125f;
                AgacDurumYenile();
                AgacBilgiYaz();
                var e = EconomyManager.I;
                U.Metin(agacKasa, Ft(e.FreeCash));
                long ort = Buyume.OrtalamaNet(7);
                U.Metin(agacKar, (ort >= 0 ? "+" : "−") + Ft(System.Math.Abs(ort)) + "/gün");
                U.Metin(agacCarpan, $"×{Buyume.KomisyonCarpani:0.00}");
                U.Metin(agacMarka, $"{Marka.Veri.puan} marka");
                long deger = Marka.SirketDegeri;
                int puan = Marka.PuanFor(deger);
                U.Metin(agacDeger, $"Şirketin değeri {Ft(deger)} · satarsan {puan} marka puanı (sonraki puan {Ft(Marka.SonrakiPuanDegeri(puan))} değerde)");
                string engel = Marka.SatisEngeli;
                U.Goster(agacEngel, engel.Length > 0);
                if (engel.Length > 0) U.Metin(agacEngel, $"Satış için: {engel}.");
                U.Goster(agacSat, engel.Length == 0);
                if (engel.Length == 0) U.Metin(agacSat, $"Şirketi sat ve yeni şehre geç — {puan} puan");
            }
            else AgacBarYaz();

            // Düğüm efektleri
            foreach (var g in agacGorseller)
            {
                float s = 1f, dx = 0f;
                // Alınabilir: nabız
                if (g.durum == AgDurum.Alinir) s *= 1f + 0.05f * (0.5f + 0.5f * Mathf.Sin(t * 4f + g.d.konum.x * 0.01f));
                // Satın alma: büyüyüp sönme
                float pk = (t - g.patlama) / 0.4f;
                if (pk >= 0f && pk < 1f) s *= 1f + 0.35f * (1f - pk) * (1f - pk);
                // Sallanma
                float sk = (t - g.sallanma) / 0.35f;
                if (sk >= 0f && sk < 1f) dx = Mathf.Sin(sk * 30f) * 6f * (1f - sk);
                g.kok.style.scale = new Scale(new Vector3(s, s, 1f));
                g.kok.style.translate = new Translate(dx, 0f);
                // Hale: en üst seviye yeşil, merkez şirket renginde
                bool halo = g.durum == AgDurum.Max || g.durum == AgDurum.Merkez;
                U.Goster(g.halo, halo);
                if (halo)
                {
                    g.halo.style.opacity = 0.35f + 0.25f * Mathf.Sin(t * 2f + g.d.konum.y * 0.01f);
                    U.Zemin(g.halo, U.A(g.durum == AgDurum.Merkez ? (Color)UITheme.Accent : AgYesil, 0.45f));
                }
                // "?" açılması: soru dönerek kaybolur, ikon büyüyerek gelir
                bool gizli = g.durum == AgDurum.Gizli;
                float ak = (t - g.acilma) / 0.5f;
                if (gizli)
                {
                    U.Goster(g.soru, true); g.soru.style.opacity = 1f; g.soru.style.rotate = new Rotate(new Angle(0f, AngleUnit.Degree));
                    U.Goster(g.ikon, false);
                }
                else if (ak >= 0f && ak < 1f)
                {
                    U.Goster(g.soru, true);
                    g.soru.style.opacity = 1f - ak;
                    g.soru.style.rotate = new Rotate(new Angle(ak * 540f, AngleUnit.Degree));
                    U.Goster(g.ikon, true);
                    g.ikon.style.opacity = ak;
                    float isc = 0.4f + 0.6f * ak;
                    g.ikon.style.scale = new Scale(new Vector3(isc, isc, 1f));
                }
                else
                {
                    U.Goster(g.soru, false);
                    U.Goster(g.ikon, true);
                    g.ikon.style.opacity = g.durum == AgDurum.Kilit ? 0.4f : 1f;
                    g.ikon.style.scale = new Scale(Vector3.one);
                }
            }

            // Parçacıklar ve halka
            for (int i = agacParcaciklar.Count - 1; i >= 0; i--)
            {
                var pc = agacParcaciklar[i];
                float k = (t - pc.bas) / pc.omur;
                if (k >= 1f) { pc.e.RemoveFromHierarchy(); agacParcaciklar.RemoveAt(i); continue; }
                if (pc.halka)
                {
                    float r = Mathf.Lerp(30f, 90f, k);
                    Mutlak(pc.e, pc.p.x - r, pc.p.y - r, r * 2f, r * 2f);
                    Cerceve(pc.e, 3f * (1f - k) + 1f, AgBeyaz, r);   // daire kalsın
                    pc.e.style.opacity = 1f - k;
                }
                else
                {
                    Vector2 q = pc.p + pc.v * (t - pc.bas) * (1f - 0.5f * k);
                    pc.e.style.left = q.x; pc.e.style.top = q.y;
                    pc.e.style.opacity = 1f - k;
                }
            }

            // Bilgi kutusu yumuşak açılış
            float bk = Mathf.Clamp01((t - agacBilgiAcilis) / 0.18f);
            agacBilgi.style.opacity = 0.35f + 0.65f * bk;
            agacBilgi.style.translate = new Translate(0f, (1f - bk) * 10f);

            if (t - agacAkisBas < 0.75f) agacCizgi.MarkDirtyRepaint();
        }
    }
}
