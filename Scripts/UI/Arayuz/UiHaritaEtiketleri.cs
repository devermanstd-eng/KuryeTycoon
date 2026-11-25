using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Haritanın üstündeki dünyaya bağlı yazılar UI Toolkit'te (25.11'e kadar OnGUI'deydi):
    ///  • Etiketler: kuryeler (araç simgesiyle), merkez ofis, dükkanlar, rakip kuryeler, robotlar, yer adları. Koyu mürekkep
    ///    rozetler; çakışmasınlar diye önem sırasıyla yerleşir (çakışan biraz kayar, yer yoksa gizlenir), önemli olan üstte.
    ///  • Uçan yazılar: teslimatta "+350 Ft", iptalde "İptal" (MapFx). Küçük rozet olarak belirir, yukarı süzülüp kaybolur.
    ///  • Şehir etkinliği işareti: etkinlik alanında nabız gibi atan halka, takvim noktası ve başlık.
    ///  • Lejant kutusu (harita araç çubuğundaki "Lejant") ve fotoğraf modu ipucu (F10).
    /// Katman: hud katmanının en altında ve harita alanına kırpılmış. Üst çubuk, menü, pencere, kurye telefonu, bina kartı,
    /// durum kartları ve bildirimler hep üstte kalır: IMGUI'de bu yazılar UI Toolkit'in üstüne çiziliyordu, altına girenler
    /// tek tek gizleniyordu. Seçili binanın işareti artık 3D seçim halkası (SecimHalkasi).
    /// Konumlar her karede, harita kamerası yerini aldıktan sonra yazılır (HaritaUstuSurucu: geç çalışan küçük bileşen).
    /// </summary>
    public partial class GameUI
    {
        VisualElement katEtiket, katEtkinlik, katEtiketYazi, katUcan, lejantKok, lejantMerkezRenk;
        Label fotoIpucu, etkinlikYazi;
        VisualElement etkinlikHalka, etkinlikNokta;
        Rect etiketAlani;

        class Etiket
        {
            public VisualElement kok, simge;
            public Label yazi;
            public string anahtar;          // genişlik önbelleği için (yer adı / simgeli / metin)
            public Vector2 konum = new Vector2(float.NaN, float.NaN);
        }

        readonly List<Etiket> etiketler = new List<Etiket>();
        readonly List<Etiket> ucanlar = new List<Etiket>();
        /// <summary>Ekranda ölçülen genişlikler (bir kare sonra düzenden okunur). İlk karede tahmin kullanılır.</summary>
        readonly Dictionary<string, float> etiketGenislik = new Dictionary<string, float>();

        // ------------------------------------------------------------------ kurulum

        /// <summary>UiKur'dan (kök ve katmanlar hazırken).</summary>
        void UiHaritaEtiketKur(VisualElement root)
        {
            katEtiket = U.Gecirgen(null, "harita-etiketler");
            katHud.Insert(0, katEtiket);                 // hud'ın en altında: bütün kutular üstünde kalır
            katEtkinlik = U.Gecirgen(katEtiket, "katman");
            katEtiketYazi = U.Gecirgen(katEtiket, "katman");
            katUcan = U.Gecirgen(katEtiket, "katman");   // uçan yazılar etiketlerin üstünde

            etkinlikHalka = U.Gecirgen(katEtkinlik, "etkinlik-halka");
            etkinlikNokta = U.Gecirgen(katEtkinlik, "etkinlik-nokta");
            U.Simge(etkinlikNokta, Icons.Kind.Calendar, Color.white, 13f, "etkinlik-simge");
            etkinlikYazi = U.Kalin(katEtkinlik, "", "etkinlik-yazi");
            U.Goster(katEtkinlik, false);

            LejantKur();

            // Fotoğraf modunda arayüz (uiKok) gizli: ipucu kökte, her şeyin üstünde
            fotoIpucu = U.Yazi(root, "Fotoğraf modu · F10 ya da Esc: arayüzü geri getir", "foto-ipucu");
            U.Goster(fotoIpucu, false);

            var surucu = gameObject.GetComponent<HaritaUstuSurucu>();
            if (surucu == null) surucu = gameObject.AddComponent<HaritaUstuSurucu>();
            surucu.ui = this;
        }

        void LejantKur()
        {
            lejantKok = U.El(katHud, "lejant");
            lejantKok.style.bottom = 52f;            // araç çubuğunun üstünde (LegendRect ile aynı yer)
            U.Kalin(lejantKok, "LEJANT", "lejant-baslik");
            var ogeler = new List<(Color c, string t)>
            {
                (CityMap.RoadColor, "Yol"),
                (CityMap.BridgeColor, "Köprü (nehri sadece buradan geç)"),
                (CityMap.WaterColor, "Nehir"),
                (CityMap.GrassColor, "Park"),
                (CityMap.PlazaColor, "Meydan"),
                (CityMap.LegendColor(Zone.OldTown), "Eski şehir (3–5 kat)"),
                (CityMap.LegendColor(Zone.HighRise), "Kule bloklar (7–12 kat, asansör)"),
                (CityMap.LegendColor(Zone.Houses), "Bahçeli evler (1–2 kat)"),
                (CityMap.LegendColor(Zone.Mixed), "Çarşı (3–6 kat)"),
                (CityMap.MallColor, "AVM"),
                (CityMap.LegendColor(Zone.Campus), "Kampüs (2–4 kat)"),
                (CityMap.LegendColor(Zone.Warehouse), "Sanayi (1–2 kat)"),
                (CityMap.RailBedColor, "Tren yolu"),
                (CompanyProfile.Primary, "Merkez ofis"),
            };
            foreach (var (c, t) in ogeler)
            {
                var satir = U.El(lejantKok, "lejant-satir");
                var kare = U.El(satir, "lejant-renk");
                U.Zemin(kare, c);
                if (t == "Merkez ofis") lejantMerkezRenk = kare;
                U.Yazi(satir, t, "lejant-yazi");
            }
            U.Goster(lejantKok, false);
        }

        // ------------------------------------------------------------------ her kare

        /// <summary>HaritaUstuSurucu'dan, harita kamerası yerini aldıktan sonra.</summary>
        internal void HaritaUstuKareSonu()
        {
            SecimHalkasiGuncelle();
            if (!uiHazir || katEtiket == null) return;
            bool basladi = GameBootstrap.I != null && GameBootstrap.I.Started;

            U.Goster(fotoIpucu, basladi && fotoModu && Time.unscaledTime < fotoIpucuBitis);

            bool lejant = basladi && legendOpen && !AgacGorunur && !IceriBakis.Acik;
            U.Goster(lejantKok, lejant);
            if (lejant)
            {
                var r = LegendRect(UiEkranYuksekligi);
                if (Mathf.Abs(lejantKok.resolvedStyle.left - r.x) > 0.5f) lejantKok.style.left = r.x;
                if (lejantMerkezRenk != null) U.Zemin(lejantMerkezRenk, CompanyProfile.Primary);
            }

            // Gelişim ağacı tam ekran açıkken harita yazıları çizilmez (arkada kalırdı)
            bool goster = basladi && !AgacGorunur && !IceriBakis.Acik && Camera.main != null && CityMap.I != null && CourierManager.I != null;
            U.Goster(katEtiket, goster);
            if (!goster) return;

            // Harita alanı: açık pencerenin sağı, üst çubuğun altı (yazılar pencerenin ve çubuğun altına taşmaz)
            float sw = UiEkranGenisligi, sh = UiEkranYuksekligi;
            float sol = Mathf.Max(MapLeftGui, panelOpen ? WindowRect(sh).xMax : 0f);
            var alan = new Rect(sol, TopBar, Mathf.Max(0f, sw - sol), Mathf.Max(0f, sh - TopBar));
            if (alan != etiketAlani)
            {
                etiketAlani = alan;
                katEtiket.style.left = alan.x;
                katEtiket.style.top = alan.y;
                katEtiket.style.width = alan.width;
                katEtiket.style.height = alan.height;
            }

            EtiketleriYerlestir();
            UcanYazilar();
            EtkinlikIsareti();
        }

        // ------------------------------------------------------------------ etiketler

        struct LabelReq { public Vector2 p; public string text; public Color color; public bool landmark; public Rect r; public bool skip; public bool hasIcon; public Icons.Kind icon; public bool onemli; }
        readonly List<LabelReq> labelReqs = new List<LabelReq>();
        readonly List<Rect> labelPlaced = new List<Rect>();

        /// <summary>Etiketi sıraya koyar; icon verilirse yazının soluna simge çizilir (ör. kuryenin aracı).</summary>
        void QueueLabel(Camera cam, Vector3 world, string text, Color color, bool landmark = false, Icons.Kind? icon = null, bool onemli = false)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) return;
            labelReqs.Add(new LabelReq
            {
                p = new Vector2(sp.x / uiScale, (Screen.height - sp.y) / uiScale), text = text, color = color, landmark = landmark,
                hasIcon = icon.HasValue, icon = icon ?? Icons.Kind.Close, onemli = onemli
            });
        }

        /// <summary>
        /// Etiketler önem sırasına göre yerleştirilir: kuryeler, merkez ofis, dükkanlar, rakipler, yer adları. Çakışan etiket biraz
        /// yukarı/aşağı kaydırılır; yer yoksa gösterilmez (önemliler hep gösterilir). Önemli olan en üstte çizilir.
        /// </summary>
        void EtiketleriYerlestir()
        {
            var cam = Camera.main;
            labelReqs.Clear();
            float zoom = MapCamera.I != null ? MapCamera.I.Zoom : 1f;

            // 1) Kuryeler (aynı karedekiler üst üste dizilir). Kalabalık ekipte bütün şehre bakarken isim yazılmaz
            //    (sadece fare üstündeki / seçili kurye): etiketler birbirine girmesin
            var stack = new Dictionary<Vector2Int, int>();
            bool kalabalik = CourierManager.I.Couriers.Count > 8 && zoom < 0.18f;
            foreach (var c in CourierManager.I.Couriers)
            {
                var key = Vector2Int.RoundToInt(c.Position);
                stack.TryGetValue(key, out int n);
                stack[key] = n + 1;
                bool hi = c == popupCourier || c == hoverCourier;
                if (kalabalik && !hi) continue;
                QueueLabel(cam, CityMap.ToWorld(c.Position) + CourierLabelOffset(n),
                    c.ShortName, hi ? new Color(1f, 0.85f, 0.2f) : Color.white, false, Icons.Of(c.vehicle), hi);
            }

            // 2) Merkez ofis
            float hqTop = BuildingTop(CityMap.I.HQTile);
            QueueLabel(cam, CityMap.ToWorld(CityMap.I.HQTile) + MapCamera.LabelOffset(0f, hqTop + 0.05f), CompanyProfile.Initial, CompanyProfile.Secondary, false, null, true);
            QueueLabel(cam, CityMap.ToWorld(CityMap.I.HQTile) + MapCamera.LabelOffset(-0.7f, hqTop + 0.35f), "Merkez", Color.white, false, null, true);

            // 3) Dükkanlar (uzaktayken sözleşmesizler gizlenir: harita kalabalık olmasın)
            foreach (var s in CityMap.I.Shops)
            {
                if (!CityMap.I.IsUnlocked(s.tile)) continue;
                if (!s.contracted && zoom < 0.3f && infoShop != s) continue;
                if (heatMode == HeatMode.DukkanKari && !s.contracted) continue;   // kârlılık katmanında yalnızca sözleşmeli dükkanlar
                if (ShopProfitLabel(s, out string karYazi, out Color karRenk))
                {
                    QueueLabel(cam, CityMap.ToWorld(s.tile) + MapCamera.LabelOffset(0.7f, BuildingTop(s.tile) + 0.2f), karYazi, karRenk);
                    continue;
                }
                bool shopOpen = ShopHours.IsOpen(s.def);
                QueueLabel(cam, CityMap.ToWorld(s.tile) + MapCamera.LabelOffset(0.7f, BuildingTop(s.tile) + 0.2f),
                    (s.contracted ? s.Name : s.Name + " (sözleşmesiz)") + (shopOpen ? "" : " · Kapalı"),
                    !shopOpen ? new Color(0.55f, 0.55f, 0.6f) : s.contracted ? Color.white : new Color(0.7f, 0.7f, 0.7f));
            }

            // 4) Rakip kuryeler: sadece yakındayken
            if (RivalFleet.I != null && MapCamera.I != null && MapCamera.I.ShowModels)
                foreach (var a in RivalFleet.I.Agents)
                    if (a != null && a.Visible)
                        QueueLabel(cam, CityMap.ToWorld(a.Position) + MapCamera.LabelOffset(0.5f, 0.55f), a.label,
                                   Color.Lerp(a.renk, Color.white, 0.4f));

            // 4b) Teslimat robotları: sadece yakındayken
            if (RobotFilo.I != null && MapCamera.I != null && MapCamera.I.ShowModels)
                foreach (var rb in RobotFilo.I.Robotlar)
                    if (rb.durum != RobotFilo.Durum.Bosta)
                        QueueLabel(cam, CityMap.ToWorld(rb.pos) + MapCamera.LabelOffset(0.5f, 0.35f),
                                   rb.order != null ? $"{rb.Ad} · #{rb.order.id}" : rb.Ad, RobotFilo.RobotHalka);

            // 5) Yer adları: kilitli bölgedekiler soluk
            if (ShowLandmarks)
                foreach (var lm in CityMap.Landmarks)
                {
                    bool open = CityMap.I.IsUnlocked(Vector2Int.RoundToInt(lm.pos));
                    var c = open ? lm.color : UITheme.WithAlpha(lm.color, 0.45f);
                    QueueLabel(cam, CityMap.ToWorld(lm.pos) + MapCamera.LabelOffset(0f, 0.05f), lm.name, c, true);
                }

            // Yerleştir. Haritanın üstündeki kutular (olaylar, hedefler, araç çubuğu, telefon, bina kartı...) dolu sayılır:
            // etiket önce onların yanına kaymayı dener (kutunun altında kalan etiket zaten görünmez).
            labelPlaced.Clear();
            UiHaritaUstuDolu(labelPlaced);
            float[] shifts = { 0f, -19f, 19f, -38f, 38f };
            for (int i = 0; i < labelReqs.Count; i++)
            {
                var q = labelReqs[i];
                float tw = Genislik(Anahtar(q), q);
                var baseR = new Rect(q.p.x - tw / 2f, q.p.y - 9f, tw, 18f);
                q.r = baseR;
                q.skip = false;
                bool ok = false;
                int deneme = q.landmark ? 1 : shifts.Length;
                for (int k = 0; k < deneme; k++)
                {
                    var r = new Rect(baseR.x, baseR.y + shifts[k], baseR.width, baseR.height);
                    bool hit = false;
                    foreach (var o in labelPlaced) if (o.Overlaps(r)) { hit = true; break; }
                    if (!hit) { q.r = r; ok = true; break; }
                }
                if (!ok && !q.onemli) q.skip = true;   // yer yoksa çizilmez (üst üste binen etiket okunmuyor)
                if (!q.skip) labelPlaced.Add(q.r);
                labelReqs[i] = q;
            }

            // Önemsizden önemliye: sonraki eleman üstte çizilir, kuryeler en üstte kalsın
            int kullanilan = 0;
            for (int i = labelReqs.Count - 1; i >= 0; i--)
            {
                var q = labelReqs[i];
                if (q.skip) continue;
                var e = kullanilan < etiketler.Count ? etiketler[kullanilan] : YeniEtiket(katEtiketYazi, etiketler);
                kullanilan++;
                EtiketYaz(e, q);
            }
            for (int i = kullanilan; i < etiketler.Count; i++) U.Goster(etiketler[i].kok, false);
        }

        static string Anahtar(in LabelReq q) => (q.landmark ? "Y|" : q.hasIcon ? "S|" : "E|") + q.text;

        /// <summary>Etiketin genişliği: daha önce ekranda ölçüldüyse o, yoksa harf sayısından tahmin.</summary>
        float Genislik(string anahtar, in LabelReq q)
        {
            if (etiketGenislik.TryGetValue(anahtar, out float w)) return w;
            return q.landmark ? q.text.Length * 7.4f + 6f : q.text.Length * 6.6f + 14f + (q.hasIcon ? 18f : 0f);
        }

        static Etiket YeniEtiket(VisualElement ust, List<Etiket> liste)
        {
            var e = new Etiket();
            e.kok = U.Gecirgen(ust, "harita-etiket");
            e.simge = U.Simge(e.kok, Icons.Kind.Close, Color.white, 13f, "harita-etiket-simge");
            e.yazi = U.Kalin(e.kok, "", "harita-etiket-yazi");
            liste.Add(e);
            return e;
        }

        void EtiketYaz(Etiket e, in LabelReq q)
        {
            // Önceki yazının genişliğini sakla (düzen bir önceki karede hesaplandı)
            OlcuSakla(e);
            string anahtar = Anahtar(q);
            if (e.anahtar != anahtar)
            {
                e.anahtar = anahtar;
                U.Metin(e.yazi, q.text);
                U.Sinif(e.kok, "yer-adi", q.landmark);
                U.Goster(e.simge, q.hasIcon);
            }
            if (q.hasIcon) U.SimgeAyarla(e.simge, q.icon, q.color);
            U.Renk(e.yazi, q.landmark ? q.color : U.Okunur(q.color));
            U.Sinif(e.kok, "onemli", q.onemli && !q.landmark);
            Konum(e, new Vector2(q.r.x - etiketAlani.x, q.r.y - etiketAlani.y + (q.landmark ? -2f : 0f)));
            U.Goster(e.kok, true);
        }

        void OlcuSakla(Etiket e)
        {
            if (string.IsNullOrEmpty(e.anahtar) || !U.Gorunur(e.kok)) return;
            float w = e.kok.resolvedStyle.width;
            if (w > 1f && !float.IsNaN(w)) etiketGenislik[e.anahtar] = w;
            if (etiketGenislik.Count > 4000) etiketGenislik.Clear();   // uzun oyunda sınırsız büyümesin (dükkan kârı yazıları değişir)
        }

        static void Konum(Etiket e, Vector2 p)
        {
            if (Mathf.Abs(p.x - e.konum.x) < 0.25f && Mathf.Abs(p.y - e.konum.y) < 0.25f) return;
            e.konum = p;
            e.kok.style.translate = new Translate(p.x, p.y);
        }

        // ------------------------------------------------------------------ uçan yazılar

        /// <summary>Uçan yazılar: küçük rozet olarak belirir, yukarı süzülür ve kaybolur (gerçek zaman, oyun hızından bağımsız).</summary>
        void UcanYazilar()
        {
            var pops = MapFx.Pops;
            float now = Time.unscaledTime;
            pops.RemoveAll(q => now - q.born > MapFx.Life);
            int kullanilan = 0;
            var cam = Camera.main;
            if (ShowPopsSetting && cam != null)
                foreach (var p in pops)
                {
                    float age = now - p.born;
                    var world = CityMap.ToWorld(p.tile) + MapCamera.LabelOffset(0.4f, BuildingTop(p.tile) + 0.3f);
                    Vector3 sp = cam.WorldToScreenPoint(world);
                    if (sp.z < 0f) continue;
                    float x = sp.x / uiScale - etiketAlani.x;
                    float y = (Screen.height - sp.y) / uiScale - age * 26f - etiketAlani.y;
                    float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(MapFx.Life * 0.45f, MapFx.Life, age));
                    var e = kullanilan < ucanlar.Count ? ucanlar[kullanilan] : YeniUcan();
                    kullanilan++;
                    U.Metin(e.yazi, p.text);
                    var renk = p.color;
                    renk.a = 1f;
                    U.Renk(e.yazi, renk);
                    float w = e.kok.resolvedStyle.width, h = e.kok.resolvedStyle.height;
                    if (!(w > 1f)) w = p.text.Length * 7.6f + 16f;
                    if (!(h > 1f)) h = 22f;
                    Konum(e, new Vector2(x - w / 2f, y - h / 2f));
                    float s = Mathf.Lerp(0.82f, 1f, Mathf.SmoothStep(0f, 1f, age / 0.16f));   // belirirken hafifçe büyür
                    e.kok.style.scale = new Scale(new Vector3(s, s, 1f));
                    e.kok.style.opacity = a;
                    U.Goster(e.kok, true);
                }
            for (int i = kullanilan; i < ucanlar.Count; i++) U.Goster(ucanlar[i].kok, false);
        }

        Etiket YeniUcan()
        {
            var e = new Etiket { anahtar = "U" };
            e.kok = U.Gecirgen(katUcan, "ucan-yazi");
            e.yazi = U.Kalin(e.kok, "", "ucan-yazi-metin");
            ucanlar.Add(e);
            return e;
        }

        // ------------------------------------------------------------------ şehir etkinliği

        void EtkinlikIsareti()
        {
            var ce = CityEvents.I;
            var e = ce != null ? ce.ActiveNow : null;
            var cam = Camera.main;
            bool gorunur = e != null && e.HasSpot && cam != null && !IceriBakis.Acik && CityMap.I.IsUnlocked(e.Spot);
            U.Goster(katEtkinlik, gorunur);
            if (!gorunur) return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            var col = new Color(0.40f, 0.70f, 1f);
            var c = ToGui(cam, CityMap.ToWorld(e.Spot)) - etiketAlani.position;
            var kenar = ToGui(cam, CityMap.ToWorld(e.Spot) + new Vector3(e.spotRadius, 0f, 0f)) - etiketAlani.position;
            float rad = Mathf.Max(18f, Vector2.Distance(c, kenar));

            etkinlikHalka.style.width = rad * 2f;
            etkinlikHalka.style.height = rad * 2f;
            etkinlikHalka.style.borderTopLeftRadius = etkinlikHalka.style.borderTopRightRadius =
                etkinlikHalka.style.borderBottomLeftRadius = etkinlikHalka.style.borderBottomRightRadius = rad;
            var halkaRenk = U.A(col, 0.35f + pulse * 0.3f);
            etkinlikHalka.style.borderTopColor = etkinlikHalka.style.borderBottomColor =
                etkinlikHalka.style.borderLeftColor = etkinlikHalka.style.borderRightColor = halkaRenk;
            etkinlikHalka.style.translate = new Translate(c.x - rad, c.y - rad);

            float d = 16f + pulse * 4f;
            etkinlikNokta.style.width = d;
            etkinlikNokta.style.height = d;
            etkinlikNokta.style.borderTopLeftRadius = etkinlikNokta.style.borderTopRightRadius =
                etkinlikNokta.style.borderBottomLeftRadius = etkinlikNokta.style.borderBottomRightRadius = d / 2f;
            etkinlikNokta.style.translate = new Translate(c.x - d / 2f, c.y - d / 2f);

            U.Metin(etkinlikYazi, e.title);
            float w = etkinlikYazi.resolvedStyle.width;
            if (!(w > 1f)) w = e.title.Length * 6.6f + 18f;
            etkinlikYazi.style.translate = new Translate(c.x - w / 2f, c.y - d / 2f - 26f);
        }

        // ------------------------------------------------------------------ seçim halkası

        /// <summary>Haritada tıklanan kurye, dükkan ya da binanın altındaki 3D halka (eski IMGUI işaretinin yerine).</summary>
        void SecimHalkasiGuncelle()
        {
            bool basladi = GameBootstrap.I != null && GameBootstrap.I.Started;
            if (!basladi || fotoModu || AgacGorunur || IceriBakis.Acik || CityMap.I == null) { SecimHalkasi.Gizle(); return; }
            var mc = MapCamera.I;
            bool yakin = mc != null && mc.ShowModels;
            if (popupCourier != null)
            {
                SecimHalkasi.Goster(popupCourier.Position, yakin ? 0.17f : 0.34f);
                return;
            }
            if (InfoOpen)
            {
                var t = infoShop != null ? infoShop.tile : infoTile;
                SecimHalkasi.Goster(new Vector2(t.x, t.y), 0.62f);
                return;
            }
            SecimHalkasi.Gizle();
        }
    }

    /// <summary>
    /// Harita üstü yazıların konumu kamera hareket ettikten sonra yazılsın diye geç çalışan küçük bileşen (MapCamera da
    /// LateUpdate'te; ikisinin sırası belirsizdi, yazılar kaydırırken bir kare geriden geliyordu).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class HaritaUstuSurucu : MonoBehaviour
    {
        public GameUI ui;

        void LateUpdate()
        {
            if (ui == null) return;
            try { ui.HaritaUstuKareSonu(); }
            catch (System.Exception ex) { Debug.LogException(ex); }
        }
    }
}
