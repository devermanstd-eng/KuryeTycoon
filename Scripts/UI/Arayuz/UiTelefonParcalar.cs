using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Kurye telefonunun UI Toolkit parçaları. Çoğu Yazıcı'nın Ozel'iyle kullanılır: bir kez kurulur, her yenilemede Ayarla ile
    /// güncellenir; tıklananların tik alanı her yenilemede yeniden verilir (eleman yeniden kurulmaz).
    ///  • TelSimge: menü, zil, kapat, geri ve ok çizimleri (Painter2D).
    ///  • TelHarita: uygulamadaki koyu harita (kareler tek ağda), dükkanlar, rota, hedef halkaları, balon ve kurye.
    ///  • TelHap (büyük yuvarlak durum düğmesi), TelBoost, TelBoostBuyuk, TelSegment, TelYogunluk, TelGosterge, TelZaman,
    ///    TelUyari, TelBaslikEtiket, TelIkili, TelMenuSatir, TelProfil, TelKonu, TelHedef, TelBuyukSayi, TelSigortaResim, TelToplaAlt.
    /// Renkler telefonun kendi koyu teması (P… renkleri, UiTelefon.cs); boyutlar ve yazılar Kurye.uss'te "tel-" sınıflarında.
    /// </summary>
    public partial class GameUI
    {
        enum TelSimgeTur { Menu, Zil, Kapat, Geri, Sag, Asagi, Yukari }

        /// <summary>Çizgi simge (24 birimlik tuvalde, ortalı).</summary>
        class TelSimge : VisualElement
        {
            TelSimgeTur tur;
            Color renk = Color.white;

            public TelSimge(TelSimgeTur t, float boy)
            {
                tur = t;
                pickingMode = PickingMode.Ignore;
                style.width = boy;
                style.height = boy;
                style.flexShrink = 0;
                generateVisualContent += Ciz;
            }

            public void Ayarla(TelSimgeTur t, Color c)
            {
                if (t == tur && c == renk) return;
                tur = t;
                renk = c;
                MarkDirtyRepaint();
            }

            void Ciz(MeshGenerationContext mgc)
            {
                var r = contentRect;
                if (!(r.width > 2f) || !(r.height > 2f)) return;
                var p = mgc.painter2D;
                var m = r.center;
                float s = Mathf.Min(r.width, r.height) / 24f;
                Vector2 P(float x, float y) => new Vector2(m.x + x * s, m.y + y * s);
                p.strokeColor = renk;
                p.fillColor = renk;
                p.lineWidth = 2f * s;
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;
                switch (tur)
                {
                    case TelSimgeTur.Menu:
                        for (int i = -1; i <= 1; i++) Cizgi(p, P(-8f, i * 5.5f), P(8f, i * 5.5f));
                        break;
                    case TelSimgeTur.Kapat:
                        Cizgi(p, P(-6f, -6f), P(6f, 6f));
                        Cizgi(p, P(-6f, 6f), P(6f, -6f));
                        break;
                    case TelSimgeTur.Geri:
                        Cizgi(p, P(-7f, 0f), P(8f, 0f));
                        Kirik(p, P(-1f, -6f), P(-7f, 0f), P(-1f, 6f));
                        break;
                    case TelSimgeTur.Sag:
                        Kirik(p, P(-3f, -7f), P(4f, 0f), P(-3f, 7f));
                        break;
                    case TelSimgeTur.Asagi:
                        Kirik(p, P(-6f, -3f), P(0f, 3f), P(6f, -3f));
                        break;
                    case TelSimgeTur.Yukari:
                        Kirik(p, P(-6f, 3f), P(0f, -3f), P(6f, 3f));
                        break;
                    case TelSimgeTur.Zil:
                        p.BeginPath();
                        p.MoveTo(P(-6f, -1f));
                        p.Arc(P(0f, -1f), 6f * s, 180f, 360f);
                        p.LineTo(P(6f, 4f));
                        p.LineTo(P(8f, 6f));
                        p.LineTo(P(-8f, 6f));
                        p.LineTo(P(-6f, 4f));
                        p.ClosePath();
                        p.Fill();
                        p.BeginPath();
                        p.Arc(P(0f, 8.5f), 2f * s, 0f, 360f);
                        p.Fill();
                        break;
                }
            }

            static void Cizgi(Painter2D p, Vector2 a, Vector2 b)
            {
                p.BeginPath();
                p.MoveTo(a);
                p.LineTo(b);
                p.Stroke();
            }

            static void Kirik(Painter2D p, Vector2 a, Vector2 b, Vector2 c)
            {
                p.BeginPath();
                p.MoveTo(a);
                p.LineTo(b);
                p.LineTo(c);
                p.Stroke();
            }
        }

        static void Daire(Painter2D p, Vector2 m, float r, Color c)
        {
            p.fillColor = c;
            p.BeginPath();
            p.Arc(m, r, 0f, 360f);
            p.ClosePath();
            p.Fill();
        }

        // ------------------------------------------------------------------ harita

        /// <summary>
        /// Telefondaki harita: kurye ortada (görevdeyse kurye ile hedefin ortası), şehir kareleri uygulamadaki koyu temada,
        /// sözleşmeli dükkanlar sarı adla, rota beyaz çizgiyle, dükkan ve müşteri halkayla, hedefin üstünde "Teslimat X dk" balonu.
        /// Konum ve ölçek her karede (Guncelle), kareler tek ağ olarak çizilir.
        /// </summary>
        class TelHarita : VisualElement
        {
            public Courier c;
            Vector2 merkez;
            float olcek;
            readonly VisualElement balon, balonNokta, kurye;
            readonly Label balonYazi, kuryeHarf;
            readonly List<Label> poi = new List<Label>();

            public TelHarita()
            {
                AddToClassList("tel-harita");
                generateVisualContent += Ciz;
                balon = U.Gecirgen(this, "tel-balon");
                balonNokta = U.Gecirgen(balon, "tel-balon-nokta");
                balonYazi = U.Kalin(balon, "", "tel-balon-yazi");
                kurye = U.Gecirgen(this, "tel-kurye");
                kuryeHarf = U.Kalin(kurye, "", "tel-kurye-harf");
            }

            Vector2 P(Vector2 t, float w, float h) => new Vector2((t.x - merkez.x) * olcek + w / 2f, (merkez.y - t.y) * olcek + h / 2f - 10f);

            static bool Dukkana(Courier c) => c.State == CourierState.DukkanaGidiyor || c.State == CourierState.Bekliyor;

            /// <summary>Her karede: merkez, ölçek, dükkan adları, balon ve kurye işareti; sonra yeniden çizim.</summary>
            public void Guncelle()
            {
                var map = CityMap.I;
                float w = contentRect.width, h = contentRect.height;
                if (c == null || map == null || !(w > 2f) || !(h > 2f)) return;
                Vector2 me = c.Position;
                var cur = c.CurrentOrder;
                bool dukkana = Dukkana(c);
                Vector2 hedef = cur != null ? (dukkana ? (Vector2)cur.shop.tile : (Vector2)cur.customerTile) : me;
                merkez = cur != null ? (me + hedef) / 2f : me;
                float span = cur != null ? Mathf.Max(Mathf.Abs(hedef.x - me.x), Mathf.Abs(hedef.y - me.y)) + 3f : 6f;
                olcek = Mathf.Clamp(Mathf.Min(w, h - 40f) / (span * 2f), 9f, 24f);

                // Yakındaki sözleşmeli dükkanların adı
                int n = 0;
                foreach (var shop in map.Shops)
                {
                    if (!shop.contracted) continue;
                    var p = P(shop.tile, w, h);
                    if (p.x < -20f || p.y < -20f || p.x > w + 20f || p.y > h + 20f) continue;
                    if (n >= poi.Count)
                    {
                        var yeni = U.Kalin(null, "", "tel-poi");
                        Insert(0, yeni);   // balon ve kurye işaretinin altında kalsın
                        poi.Add(yeni);
                    }
                    var l = poi[n++];
                    U.Goster(l, true);
                    U.Metin(l, shop.Name);
                    l.style.left = p.x + 7f;
                    l.style.top = p.y - 7f;
                }
                for (int i = n; i < poi.Count; i++) U.Goster(poi[i], false);

                // Balon: "Teslim alma X dk" / "Teslimat X dk"
                U.Goster(balon, cur != null);
                if (cur != null)
                {
                    float eta = EtaMinutes(c);
                    string yazi = c.State == CourierState.KataCikiyor ? $"Binada {c.DoorMinutesLeft:0} dk"
                                : c.State == CourierState.Bekliyor ? "Dükkanda bekliyor"
                                : dukkana ? $"Teslim alma {eta:0} dk" : $"Teslimat {eta:0} dk";
                    U.Metin(balonYazi, yazi);
                    U.Zemin(balonNokta, dukkana ? PAccent : PGreen);
                    var tp = P(dukkana ? (Vector2)cur.shop.tile : (Vector2)cur.customerTile, w, h);
                    float bw = balon.resolvedStyle.width;
                    if (!(bw > 1f)) bw = 140f;
                    balon.style.left = Mathf.Clamp(tp.x - bw / 2f, 6f, Mathf.Max(6f, w - bw - 6f));
                    balon.style.top = Mathf.Max(48f, tp.y - 48f);
                }

                var mp = P(me, w, h);
                kurye.style.left = mp.x - 12f;
                kurye.style.top = mp.y - 12f;
                U.Metin(kuryeHarf, c.V.letter);
                MarkDirtyRepaint();
            }

            void Ciz(MeshGenerationContext mgc)
            {
                var cm = CityMap.I;
                float w = contentRect.width, h = contentRect.height;
                if (c == null || cm == null || olcek <= 0f || !(w > 2f) || !(h > 2f)) return;
                float s = olcek;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(merkez.x - w / 2f / s) - 1);
                int x1 = Mathf.Min(CityMap.Width - 1, Mathf.CeilToInt(merkez.x + w / 2f / s) + 1);
                int y0 = Mathf.Max(0, Mathf.FloorToInt(merkez.y - h / 2f / s) - 2);
                int y1 = Mathf.Min(CityMap.Height - 1, Mathf.CeilToInt(merkez.y + h / 2f / s) + 2);
                int kare = Mathf.Max(0, x1 - x0 + 1) * Mathf.Max(0, y1 - y0 + 1);
                int dortgen = 1 + kare * 2;
                if (dortgen * 4 > 60000) return;   // olmaz (en küçük ölçekte ~1800 kare), yine de ağ sınırı aşılmasın

                var md = mgc.Allocate(dortgen * 4, dortgen * 6);
                int v = 0;
                void Dortgen(float ax, float ay, float bx, float by, Color col)
                {
                    md.SetNextVertex(new Vertex { position = new Vector3(ax, ay, Vertex.nearZ), tint = col });
                    md.SetNextVertex(new Vertex { position = new Vector3(bx, ay, Vertex.nearZ), tint = col });
                    md.SetNextVertex(new Vertex { position = new Vector3(bx, by, Vertex.nearZ), tint = col });
                    md.SetNextVertex(new Vertex { position = new Vector3(ax, by, Vertex.nearZ), tint = col });
                    md.SetNextIndex((ushort)v); md.SetNextIndex((ushort)(v + 1)); md.SetNextIndex((ushort)(v + 2));
                    md.SetNextIndex((ushort)v); md.SetNextIndex((ushort)(v + 2)); md.SetNextIndex((ushort)(v + 3));
                    v += 4;
                }

                Dortgen(0f, 0f, w, h, new Color(0.13f, 0.15f, 0.20f));
                for (int x = x0; x <= x1; x++)
                    for (int y = y0; y <= y1; y++)
                    {
                        var p = P(new Vector2(x, y), w, h);
                        float ax = p.x - s / 2f, ay = p.y - s / 2f, bx = ax + s + 0.5f, by = ay + s + 0.5f;
                        bool yol = cm.IsRoad(x, y), su = CityMap.WaterAt(x, y);
                        Color col;
                        if (yol) col = CityMap.BridgeAt(x, y) ? new Color(0.36f, 0.36f, 0.42f) : new Color(0.27f, 0.29f, 0.38f);
                        else if (su) col = new Color(0.12f, 0.28f, 0.45f);
                        else col = Color.Lerp(cm.MiniColor(x, y), new Color(0.10f, 0.12f, 0.17f), 0.62f);   // uygulamadaki koyu harita teması
                        if (!cm.IsUnlocked(x, y)) col = Color.Lerp(col, Color.black, 0.5f);
                        Dortgen(ax, ay, bx, by, col);
                        // Binalar biraz açık: kareler ayrı görünür (yol ve suda aynı renk)
                        Dortgen(ax + 1.5f, ay + 1.5f, bx - 1.5f, by - 1.5f, yol || su ? col : Color.Lerp(col, Color.white, 0.03f));
                    }

                var pz = mgc.painter2D;
                // Dükkanlar
                foreach (var shop in cm.Shops)
                {
                    if (!shop.contracted) continue;
                    var p = P(shop.tile, w, h);
                    if (p.x < -20f || p.y < -20f || p.x > w + 20f || p.y > h + 20f) continue;
                    Daire(pz, p, 5f, shop.def.color);
                }

                // Rota: ızgarada yollar düz, önce yatay sonra dikey parça
                var cur = c.CurrentOrder;
                var path = c.PathList;
                if (cur != null && path != null && path.Count > 0)
                {
                    pz.strokeColor = Color.white;
                    pz.lineWidth = 4.5f;
                    pz.lineCap = LineCap.Round;
                    pz.lineJoin = LineJoin.Round;
                    pz.BeginPath();
                    var once = P(c.Position, w, h);
                    pz.MoveTo(once);
                    for (int i = c.PathIndex; i < path.Count; i++)
                    {
                        var b = P(path[i], w, h);
                        pz.LineTo(new Vector2(b.x, once.y));
                        pz.LineTo(b);
                        once = b;
                    }
                    pz.Stroke();
                }

                // Hedefler: dükkan (şirket rengi) ve müşteri (yeşil) halkası
                if (cur != null)
                {
                    foreach (var (t, renk) in new[] { ((Vector2)cur.shop.tile, PAccent), ((Vector2)cur.customerTile, PGreen) })
                    {
                        var p = P(t, w, h);
                        Daire(pz, p, 12f, renk);
                        Daire(pz, p, 9f, new Color(0.06f, 0.07f, 0.10f));
                    }
                }
            }
        }

        // ------------------------------------------------------------------ düğme benzeri parçalar

        /// <summary>Büyük yuvarlak hap: solda koyu daire içinde ok (isteğe bağlı), ortada kalın yazı. Bilgi amaçlı (tıklanmaz).</summary>
        class TelHap : VisualElement
        {
            readonly VisualElement daire;
            readonly TelSimge ok;
            readonly Label yazi;

            public TelHap()
            {
                AddToClassList("tel-hap");
                pickingMode = PickingMode.Ignore;
                daire = U.Gecirgen(this, "tel-hap-daire");
                ok = new TelSimge(TelSimgeTur.Sag, 22f);
                daire.Add(ok);
                yazi = U.Kalin(this, "", "tel-hap-yazi");
            }

            public void Ayarla(string metin, Color zemin, bool okVar)
            {
                U.Zemin(this, zemin);
                U.Goster(daire, okVar);
                if (okVar) ok.Ayarla(TelSimgeTur.Sag, zemin);
                yazi.style.paddingLeft = okVar ? 44f : 0f;
                U.Metin(yazi, metin);
            }
        }

        /// <summary>Boost kartı (ana ekran): solda şirket renginde "%", yanında şu anki ya da sıradaki boost. Tıklayınca Ekstra kazan.</summary>
        class TelBoost : VisualElement
        {
            public Action tik;
            readonly VisualElement sol;
            readonly Label yazi;

            public TelBoost()
            {
                AddToClassList("tel-boost");
                sol = U.Gecirgen(this, "tel-boost-sol");
                U.Kalin(sol, "%", "tel-boost-yuzde");
                yazi = U.Kalin(this, "", "tel-boost-yazi");
                Add(new TelSimge(TelSimgeTur.Sag, 14f));
                U.Tikla(this, () => tik?.Invoke());
            }

            public void Ayarla(string metin)
            {
                U.Zemin(sol, Color.Lerp(CompanyProfile.Primary, Color.white, 0.1f));
                U.Metin(yazi, metin);
            }
        }

        /// <summary>Ekstra kazan sayfasındaki büyük boost kartı ("Şimdi · Boost", "+%30", saat aralığı).</summary>
        class TelBoostBuyuk : VisualElement
        {
            readonly VisualElement sol, ic;
            readonly Label ust, orta, alt, bos;

            public TelBoostBuyuk()
            {
                AddToClassList("tel-boost-buyuk");
                sol = U.Gecirgen(this, "tel-boost-buyuk-sol");
                U.Kalin(sol, "%", "tel-boost-buyuk-yuzde");
                ic = U.Gecirgen(this, "tel-boost-buyuk-ic");
                ust = U.Yazi(ic, "", "tel-soluk-yazi");
                orta = U.Kalin(ic, "", "tel-boost-buyuk-deger");
                alt = U.Yazi(ic, "", "tel-soluk-yazi");
                bos = U.Yazi(this, "Şu an aktif boost yok", "tel-govde-yazi");
                bos.style.alignSelf = Align.Center;
                bos.style.marginLeft = 14f;
            }

            public void Ayarla(bool aktif, string a, string b, string c)
            {
                U.Zemin(this, aktif ? new Color(0.07f, 0.26f, 0.33f) : PCard);
                U.Zemin(sol, aktif ? Color.Lerp(CompanyProfile.Primary, Color.white, 0.1f) : new Color(0.2f, 0.21f, 0.27f));
                U.Goster(ic, aktif);
                U.Goster(bos, !aktif);
                if (!aktif) return;
                U.Metin(ust, a);
                U.Metin(orta, b);
                U.Metin(alt, c);
            }
        }

        /// <summary>Sekme şeridi (İstatistikler: Bugün/Hafta/Ay; görev: Bilgi/Ürünler).</summary>
        class TelSegment : VisualElement
        {
            public Action<int> sec;
            readonly List<Label> ogeler = new List<Label>();

            public TelSegment() { AddToClassList("tel-segment"); }

            public void Ayarla(IList<string> adlar, int secili)
            {
                while (ogeler.Count < adlar.Count)
                {
                    int k = ogeler.Count;
                    var l = U.Kalin(this, "", "tel-segment-oge");
                    U.Tikla(l, () => sec?.Invoke(k));
                    ogeler.Add(l);
                }
                for (int i = 0; i < ogeler.Count; i++)
                {
                    U.Goster(ogeler[i], i < adlar.Count);
                    if (i >= adlar.Count) continue;
                    U.Metin(ogeler[i], adlar[i]);
                    U.Sinif(ogeler[i], "secili", i == secili);
                }
            }
        }

        /// <summary>"Sipariş yoğunluğu ▂▄▆ Normal" satırı.</summary>
        class TelYogunluk : VisualElement
        {
            readonly VisualElement[] cubuk = new VisualElement[3];
            readonly Label deger;

            public TelYogunluk()
            {
                AddToClassList("tel-yogunluk");
                U.Yazi(this, "Sipariş yoğunluğu", "tel-soluk-yazi");
                var k = U.Gecirgen(this, "tel-yogunluk-cubuklar");
                for (int i = 0; i < 3; i++)
                {
                    cubuk[i] = U.Gecirgen(k, "tel-yogunluk-cubuk");
                    cubuk[i].style.height = 5f + i * 3f;
                }
                deger = U.Yazi(this, "", "tel-soluk-yazi");
            }

            public void Ayarla(int seviye, string ad)
            {
                Color on = seviye == 0 ? PRed : seviye == 1 ? new Color(1f, 0.75f, 0.3f) : PGreen;
                for (int i = 0; i < 3; i++) U.Zemin(cubuk[i], i <= seviye ? on : U.A(PDim, 0.35f));
                U.Metin(deger, ad);
            }
        }

        /// <summary>Halka gösterge: ortada yüzde, halka yüzde kadar dolu, altında açıklama.</summary>
        class TelGosterge : VisualElement
        {
            float deger;
            Color renk = Color.white;
            readonly VisualElement halka;
            readonly Label yuzde, alt;

            public TelGosterge()
            {
                AddToClassList("tel-gosterge");
                pickingMode = PickingMode.Ignore;
                halka = U.Gecirgen(this, "tel-gosterge-halka");
                halka.generateVisualContent += Ciz;
                yuzde = U.Kalin(halka, "", "tel-gosterge-yuzde");
                alt = U.Yazi(this, "", "tel-soluk-yazi", "tel-orta");
            }

            public void Ayarla(float v, string altYazi, Color c)
            {
                v = Mathf.Clamp01(v);
                if (Mathf.Abs(v - deger) > 0.001f || c != renk) { deger = v; renk = c; halka.MarkDirtyRepaint(); }
                U.Metin(yuzde, $"%{v * 100f:0}");
                U.Metin(alt, altYazi);
            }

            void Ciz(MeshGenerationContext mgc)
            {
                var r = halka.contentRect;
                if (!(r.width > 4f)) return;
                var p = mgc.painter2D;
                var m = r.center;
                float rad = Mathf.Min(r.width, r.height) / 2f - 4f;
                p.lineWidth = 6f;
                p.strokeColor = new Color(0.22f, 0.24f, 0.32f);
                p.BeginPath();
                p.Arc(m, rad, 0f, 360f);
                p.Stroke();
                if (deger <= 0.001f) return;
                p.strokeColor = renk;
                p.lineWidth = 7f;
                p.lineCap = LineCap.Round;
                p.BeginPath();
                p.Arc(m, rad, -90f, -90f + 360f * Mathf.Min(deger, 0.999f));
                p.Stroke();
            }
        }

        /// <summary>Teslimat zaman çizelgesi satırı: saat · nokta (bittiyse dolu ve ✓) · yazı.</summary>
        class TelZaman : VisualElement
        {
            readonly Label saat, isaret, yazi;
            readonly VisualElement nokta;

            public TelZaman()
            {
                AddToClassList("tel-zaman");
                saat = U.Yazi(this, "", "tel-zaman-saat");
                nokta = U.Gecirgen(this, "tel-zaman-nokta");
                isaret = U.Kalin(nokta, "✓", "tel-zaman-isaret");
                yazi = U.Yazi(this, "", "tel-zaman-yazi");
                yazi.enableRichText = true;
            }

            public void Ayarla(string zaman, bool bitti, Color c, string metin)
            {
                U.Metin(saat, zaman);
                U.Zemin(nokta, bitti ? c : new Color(0, 0, 0, 0));
                nokta.style.borderTopColor = c; nokta.style.borderBottomColor = c;
                nokta.style.borderLeftColor = c; nokta.style.borderRightColor = c;
                U.Goster(isaret, bitti);
                U.Metin(yazi, bitti ? metin : $"<b>{metin}</b>");
            }
        }

        /// <summary>Turuncu uyarı kutusu (adres sorunu, temassız teslimat).</summary>
        class TelUyari : VisualElement
        {
            readonly Label bas, metin;

            public TelUyari()
            {
                AddToClassList("tel-uyari");
                pickingMode = PickingMode.Ignore;
                U.Simge(this, Icons.Kind.Warning, PWarnText, 18f, "tel-uyari-simge");
                var ic = U.Gecirgen(this, "esnek");
                bas = U.Kalin(ic, "", "tel-uyari-bas");
                metin = U.Yazi(ic, "", "tel-uyari-metin");
            }

            public void Ayarla(string b, string m) { U.Metin(bas, b); U.Metin(metin, m); }
        }

        /// <summary>Sipariş kartının başı: solda kalın "#12 Dükkan", sağda renkli küçük etiket (ÇANTADA, ALINACAK…).</summary>
        class TelBaslikEtiket : VisualElement
        {
            readonly Label ad, etiket;

            public TelBaslikEtiket()
            {
                AddToClassList("tel-iki");
                pickingMode = PickingMode.Ignore;
                ad = U.Kalin(this, "", "tel-govde-yazi", "esnek");
                etiket = U.Kalin(this, "", "tel-etiket");
            }

            public void Ayarla(string a, string e, Color c)
            {
                U.Metin(ad, a);
                U.Metin(etiket, e);
                U.Zemin(etiket, U.A(c, 0.2f));
                U.Renk(etiket, c);
            }
        }

        /// <summary>Solda yazı, sağda yazı (sipariş kartının altı: kalan süre · kazanç).</summary>
        class TelIkili : VisualElement
        {
            readonly Label sol, sag;

            public TelIkili()
            {
                AddToClassList("tel-iki");
                pickingMode = PickingMode.Ignore;
                sol = U.Yazi(this, "", "tel-govde-yazi", "esnek");
                sag = U.Kalin(this, "", "tel-govde-yazi");
            }

            public void Ayarla(string a, Color? aRenk, string b)
            {
                U.Metin(sol, a);
                if (aRenk.HasValue) U.Renk(sol, aRenk.Value);
                else RenkSil(sol);
                U.Metin(sag, b);
            }
        }

        /// <summary>Menü satırı: solda çerçeveli harf (ya da simge), başlık ve alt yazı, altında ince çizgi.</summary>
        class TelMenuSatir : VisualElement
        {
            public Action tik;
            readonly Label harf, baslik, alt;
            readonly VisualElement simge;

            public TelMenuSatir()
            {
                AddToClassList("tel-menu-satir");
                var kutu = U.Gecirgen(this, "tel-menu-harf-kutu");
                harf = U.Kalin(kutu, "", "tel-menu-harf");
                simge = U.Simge(kutu, Icons.Kind.Clock, Color.white, 18f);
                var ic = U.Gecirgen(this, "esnek");
                baslik = U.Yazi(ic, "", "tel-satir-yazi", "tek-satir");
                alt = U.Yazi(ic, "", "tel-soluk-yazi", "tek-satir");
                Add(new TelSimge(TelSimgeTur.Sag, 14f));
                U.Tikla(this, () => tik?.Invoke());
            }

            public void Ayarla(string h, string b, string a, Icons.Kind? ikon = null)
            {
                U.Goster(harf, ikon == null);
                U.Goster(simge, ikon != null);
                U.Sinif(this, "simgeli", ikon != null);
                if (ikon != null) U.SimgeAyarla(simge, ikon.Value, Color.white);
                else U.Metin(harf, h);
                U.Metin(baslik, b);
                U.Goster(alt, !string.IsNullOrEmpty(a));
                U.Metin(alt, a ?? "");
            }
        }

        /// <summary>Menünün başındaki profil satırı: baş harfli yuvarlak, isim ve çevrimiçi durumu.</summary>
        class TelProfil : VisualElement
        {
            public Action tik;
            readonly Label avatar, ad, durum;

            public TelProfil()
            {
                AddToClassList("tel-profil");
                avatar = U.Kalin(this, "", "tel-avatar");
                var ic = U.Gecirgen(this, "esnek");
                ad = U.Yazi(ic, "", "tel-satir-yazi", "tek-satir");
                durum = U.Yazi(ic, "", "tel-soluk-yazi");
                U.Tikla(this, () => tik?.Invoke());
            }

            public void Ayarla(string isim, bool cevrimici)
            {
                U.Zemin(avatar, CompanyProfile.Secondary);
                U.Renk(avatar, CompanyProfile.Primary);
                U.Metin(avatar, string.IsNullOrEmpty(isim) ? "?" : isim.Substring(0, 1));
                U.Metin(ad, isim);
                U.Metin(durum, cevrimici ? "Çevrimiçi" : "Çevrimdışı");
                U.Renk(durum, cevrimici ? PGreen : PDim);
            }
        }

        /// <summary>Destek konusu: başlık ve açılır ok (gövdesi Yazıcı ile altına yazılır).</summary>
        class TelKonu : VisualElement
        {
            public Action tik;
            readonly Label baslik;
            readonly TelSimge ok;

            public TelKonu()
            {
                AddToClassList("tel-konu");
                baslik = U.Yazi(this, "", "tel-satir-yazi", "esnek");
                ok = new TelSimge(TelSimgeTur.Sag, 14f);
                Add(ok);
                U.Tikla(this, () => tik?.Invoke());
            }

            public void Ayarla(string b, bool acik)
            {
                U.Metin(baslik, b);
                ok.Ayarla(acik ? TelSimgeTur.Asagi : TelSimgeTur.Sag, PDim);
            }
        }

        /// <summary>Hedef çubuğu: ad, sağda "12/40" (ya da Tamamlandı), altında ilerleme.</summary>
        class TelHedef : VisualElement
        {
            readonly Label ad, deger;
            readonly VisualElement dolu;

            public TelHedef()
            {
                AddToClassList("tel-hedef");
                pickingMode = PickingMode.Ignore;
                var ust = U.Gecirgen(this, "tel-iki");
                ad = U.Yazi(ust, "", "tel-govde-yazi", "esnek");
                deger = U.Yazi(ust, "", "tel-soluk-yazi");
                var iz = U.Gecirgen(this, "tel-hedef-iz");
                dolu = U.Gecirgen(iz, "tel-hedef-dolu");
            }

            public void Ayarla(string isim, int olan, int hedef, bool bitti)
            {
                U.Metin(ad, isim);
                U.Metin(deger, bitti ? "Tamamlandı" : $"{olan}/{hedef}");
                U.Renk(deger, bitti ? PGreen : PDim);
                float v = bitti ? 1f : Mathf.Clamp01(olan / (float)Mathf.Max(1, hedef));
                U.Goster(dolu, v > 0f);
                dolu.style.width = Length.Percent(v * 100f);
                U.Zemin(dolu, bitti ? PGreen : PAccent);
            }
        }

        /// <summary>Analizler: şirket renginde büyük kutu, haftanın teslimat sayısı ve süs olarak çanta.</summary>
        class TelBuyukSayi : VisualElement
        {
            readonly Label sayi;

            public TelBuyukSayi()
            {
                AddToClassList("tel-buyuk-sayi");
                pickingMode = PickingMode.Ignore;
                var ic = U.Gecirgen(this, "tel-buyuk-sayi-ic");
                sayi = U.Kalin(ic, "", "tel-buyuk-sayi-deger");
                U.Kalin(ic, "Tamamlanan teslimat", "tel-buyuk-sayi-yazi");
                var canta = U.Gecirgen(this, "tel-canta");
                U.Gecirgen(canta, "tel-canta-sap");
            }

            public void Ayarla(int n)
            {
                U.Zemin(this, PAccent);
                U.Metin(sayi, n.ToString());
            }
        }

        /// <summary>Sigorta sayfasının resmi: pano ve kalkan (sigortalıysa "OK").</summary>
        class TelSigortaResim : VisualElement
        {
            readonly Label isaret;

            public TelSigortaResim()
            {
                AddToClassList("tel-sigorta");
                pickingMode = PickingMode.Ignore;
                var pano = U.Gecirgen(this, "tel-pano");
                var kagit = U.Gecirgen(pano, "tel-pano-kagit");
                U.Gecirgen(kagit, "tel-pano-yuvarlak");
                for (int i = 0; i < 4; i++) U.Gecirgen(kagit, "tel-pano-satir");
                var kalkan = U.Gecirgen(this, "tel-kalkan");
                var daire = U.Gecirgen(kalkan, "tel-kalkan-daire");
                isaret = U.Kalin(daire, "", "tel-kalkan-isaret");
            }

            public void Ayarla(bool sigortali) => U.Metin(isaret, sigortali ? "OK" : "–");
        }

        /// <summary>Teslim alma kartının altı: şirket renginde durum hapı (tıklanmaz) ve açılır ok düğmesi.</summary>
        class TelToplaAlt : VisualElement
        {
            public Action tik;
            readonly Label hap;
            readonly TelSimge ok;

            public TelToplaAlt()
            {
                AddToClassList("tel-iki");
                AddToClassList("tel-topla-alt");
                pickingMode = PickingMode.Ignore;
                hap = U.Kalin(this, "", "tel-topla-hap", "esnek");
                var d = U.Tikla(U.El(this, "tel-topla-ok"), () => tik?.Invoke());
                ok = new TelSimge(TelSimgeTur.Asagi, 18f);
                d.Add(ok);
            }

            public void Ayarla(string metin, bool acik)
            {
                U.Metin(hap, metin);
                U.Zemin(hap, PAccent);
                ok.Ayarla(acik ? TelSimgeTur.Yukari : TelSimgeTur.Asagi, PAccent);
            }
        }
    }
}
