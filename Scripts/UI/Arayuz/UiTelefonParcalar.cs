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
        /// Telefondaki harita (kurye uygulamasının koyu haritası gibi):
        ///  • Yollar kesintisiz şerit (caddeler geniş ve ortası kesik çizgili, ara sokaklar ince), köprüler açık renk.
        ///  • Binalar yan yana birleşen bloklar (komşusu bina olan kenarda boşluk yok), parklar yeşil, nehir mavi, kilitli semtler karanlık.
        ///  • Cadde / sokak adları yolun üstünde, semt ve park adları soluk; sözleşmeli dükkanlar renkli nokta ve adla.
        ///  • Rota koyu kenarlı beyaz çizgi; dükkana giderken dükkandan müşteriye giden ikinci ayak kesik çizgi.
        ///  • Dükkan ve müşteri iğne (pin) şeklinde; kuryenin etrafında nabız gibi atan halka ve gidiş yönünü gösteren ışık konisi.
        ///  • Kamera yumuşak kayar ve yakınlaşır (kurye ile hedef her zaman ekranda); hedefe yaklaştıkça harita yakınlaşır.
        /// Konum ve ölçek her karede (Guncelle); zemin, yollar ve binalar tek ağ olarak çizilir.
        /// </summary>
        class TelHarita : VisualElement
        {
            public Courier c;
            Courier sonKurye;
            Vector2 merkez, hedefMerkez;
            float olcek, hedefOlcek;
            Vector2 sonKonum, yon = Vector2.up;
            float nabiz, durgun = 99f;
            readonly VisualElement balon, balonNokta, kurye;
            readonly Label balonYazi, kuryeHarf;
            readonly List<Label> poi = new List<Label>();
            readonly List<Label> sokak = new List<Label>();
            readonly List<Label> semt = new List<Label>();

            // ağ tamponları (her çizimde yeniden kullanılır)
            static Vertex[] kose = new Vertex[4096];
            static ushort[] dizin = new ushort[6144];
            static int kv, kd;

            static readonly Color CZemin = new Color(0.135f, 0.125f, 0.115f);
            static readonly Color CAvlu = new Color(0.165f, 0.155f, 0.145f);
            static readonly Color CBina = new Color(0.250f, 0.236f, 0.220f);
            static readonly Color CBinaKenar = new Color(0.205f, 0.192f, 0.178f);
            static readonly Color CPark = new Color(0.145f, 0.215f, 0.150f);
            static readonly Color CMeydan = new Color(0.205f, 0.195f, 0.180f);
            static readonly Color CSu = new Color(0.095f, 0.190f, 0.275f);
            static readonly Color CKir = new Color(0.120f, 0.150f, 0.115f);
            static readonly Color CCadde = new Color(0.355f, 0.335f, 0.315f);
            static readonly Color CSokak = new Color(0.285f, 0.270f, 0.255f);
            static readonly Color CKopru = new Color(0.430f, 0.405f, 0.380f);
            static readonly Color CSerit = new Color(0.52f, 0.49f, 0.45f, 0.55f);

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

            Label Havuz(List<Label> liste, int i, string sinif)
            {
                if (i < liste.Count) return liste[i];
                var l = U.Kalin(null, "", sinif);
                l.pickingMode = PickingMode.Ignore;
                Insert(0, l);   // balon ve kurye işaretinin altında kalsın
                liste.Add(l);
                return l;
            }

            /// <summary>Her karede: merkez, ölçek (yumuşak), etiketler, balon ve kurye işareti; sonra yeniden çizim.</summary>
            public void Guncelle()
            {
                var map = CityMap.I;
                float w = contentRect.width, h = contentRect.height;
                if (c == null || map == null || !(w > 2f) || !(h > 2f)) return;
                Vector2 me = c.Position;
                var cur = c.CurrentOrder;
                bool dukkana = cur != null && Dukkana(c);
                Vector2 hedef = cur != null ? (dukkana ? (Vector2)cur.shop.tile : (Vector2)cur.customerTile) : me;

                // Hedef kamera: kurye ile hedef ekrana sığsın; yakınken daha yakın
                hedefMerkez = cur != null ? (me + hedef) / 2f : me;
                float yariX = Mathf.Abs(hedef.x - me.x) / 2f + 1.6f, yariY = Mathf.Abs(hedef.y - me.y) / 2f + 1.8f;
                float gorunurH = Mathf.Max(60f, h - 70f);   // üstteki düğmeler ve alttaki panelin payı
                hedefOlcek = Mathf.Clamp(Mathf.Min(w / (yariX * 2f), gorunurH / (yariY * 2f)), 10f, cur != null ? 40f : 30f);

                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                bool ani = sonKurye != c || olcek <= 0f;
                if (ani)
                {
                    sonKurye = c;
                    merkez = hedefMerkez;
                    olcek = hedefOlcek;
                    sonKonum = me;
                }
                else
                {
                    float k = 1f - Mathf.Exp(-dt * 5f);
                    merkez = Vector2.Lerp(merkez, hedefMerkez, k);
                    olcek = Mathf.Lerp(olcek, hedefOlcek, k);
                }
                // Gidiş yönü (ışık konisi): kuryenin hareketinden, yumuşatılmış
                Vector2 d = me - sonKonum;
                if (d.sqrMagnitude > 1e-6f) { yon = Vector2.Lerp(yon, d.normalized, 1f - Mathf.Exp(-dt * 10f)).normalized; durgun = 0f; }
                else durgun += dt;
                sonKonum = me;
                nabiz = (nabiz + dt * 0.7f) % 1f;

                // Sözleşmeli dükkanların adı
                int n = 0;
                foreach (var shop in map.Shops)
                {
                    if (!shop.contracted) continue;
                    var p = P(shop.tile, w, h);
                    if (p.x < -20f || p.y < -20f || p.x > w + 20f || p.y > h + 20f) continue;
                    var l = Havuz(poi, n++, "tel-poi");
                    U.Goster(l, true);
                    U.Metin(l, shop.Name);
                    l.style.left = p.x + 8f;
                    l.style.top = p.y - 7f;
                }
                for (int i = n; i < poi.Count; i++) U.Goster(poi[i], false);

                // Semt, park ve nehir adları (soluk)
                n = 0;
                foreach (var lm in CityMap.Landmarks)
                {
                    var p = P(lm.pos, w, h);
                    if (p.x < 10f || p.y < 30f || p.x > w - 10f || p.y > h - 20f) continue;
                    var l = Havuz(semt, n++, "tel-semt");
                    U.Goster(l, true);
                    U.Metin(l, lm.name);
                    l.style.left = p.x;
                    l.style.top = p.y;
                }
                for (int i = n; i < semt.Count; i++) U.Goster(semt[i], false);

                SokakAdlari(w, h);

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
                    var tp = P(hedef, w, h);
                    float bw = balon.resolvedStyle.width;
                    if (!(bw > 1f)) bw = 140f;
                    balon.style.left = Mathf.Clamp(tp.x - bw / 2f, 6f, Mathf.Max(6f, w - bw - 6f));
                    balon.style.top = Mathf.Clamp(tp.y - 66f, 48f, Mathf.Max(48f, h - 110f));   // iğnenin üstünde
                }

                var mp = P(me, w, h);
                kurye.style.left = mp.x - 12f;
                kurye.style.top = mp.y - 12f;
                U.Metin(kuryeHarf, c.V.letter);
                MarkDirtyRepaint();
            }

            /// <summary>Görünen caddelerin (dikey) ve sokakların (yatay) adı, yolun ortasında, yol yönünde.</summary>
            void SokakAdlari(float w, float h)
            {
                int n = 0;
                if (olcek >= 15f)
                {
                    float s = olcek;
                    int x0 = Mathf.Max(0, Mathf.FloorToInt(merkez.x - w / 2f / s)), x1 = Mathf.Min(CityMap.Width - 1, Mathf.CeilToInt(merkez.x + w / 2f / s));
                    int y0 = Mathf.Max(0, Mathf.FloorToInt(merkez.y - h / 2f / s)), y1 = Mathf.Min(CityMap.Height - 1, Mathf.CeilToInt(merkez.y + h / 2f / s));
                    // Caddeler: x = 4k sütunları; etiket blok ortasında (y ≡ 2), ekranın üst yarısında
                    for (int x = (x0 + 3) / 4 * 4; x <= x1 && n < 3; x += 4)
                    {
                        int yy = -1;
                        for (int y = y1; y >= y0; y--)
                            if (y % CityMap.Block == 2 && CityMap.RoadAt(x, y) && !CityMap.BridgeAt(x, y))
                            {
                                var p = P(new Vector2(x, y), w, h);
                                if (p.y > 60f && p.y < h * 0.55f) { yy = y; break; }
                            }
                        if (yy < 0) continue;
                        var pp = P(new Vector2(x, yy), w, h);
                        if (pp.x < 14f || pp.x > w - 14f) continue;
                        Etiket(n++, Addresses.StreetOf(new Vector2Int(x, yy)), pp, true);
                    }
                    // Sokaklar: y = 4k satırları; etiket blok ortasında (x ≡ 2)
                    for (int y = (y0 + 3) / 4 * 4; y <= y1 && n < 6; y += 4)
                    {
                        int xx = -1;
                        for (int x = x0; x <= x1; x++)
                            if (x % CityMap.Block == 2 && CityMap.RoadAt(x, y))
                            {
                                var p = P(new Vector2(x, y), w, h);
                                if (p.x > 50f && p.x < w - 50f) { xx = x; break; }
                            }
                        if (xx < 0) continue;
                        var pp = P(new Vector2(xx, y), w, h);
                        if (pp.y < 60f || pp.y > h - 30f) continue;
                        Etiket(n++, Addresses.StreetOf(new Vector2Int(xx, y)), pp, false);
                    }
                }
                for (int i = n; i < sokak.Count; i++) U.Goster(sokak[i], false);
            }

            void Etiket(int i, string ad, Vector2 p, bool dikey)
            {
                // kurye işaretinin ve hedef iğnesinin üstüne binmesin: yol boyunca kaydır
                float w = contentRect.width, h = contentRect.height;
                var mp = P(c.Position, w, h);
                var cur = c.CurrentOrder;
                Vector2 hp = cur != null ? P(Dukkana(c) ? (Vector2)cur.shop.tile : (Vector2)cur.customerTile, w, h) : mp;
                for (int k = 0; k < 4; k++)
                {
                    bool carpar = (p - mp).sqrMagnitude < 40f * 40f || (p - hp - new Vector2(0f, -14f)).sqrMagnitude < 36f * 36f;
                    if (!carpar) break;
                    p += dikey ? new Vector2(0f, olcek * 2f) : new Vector2(olcek * 2f, 0f);
                }
                var l = Havuz(sokak, i, "tel-sokak");
                U.Goster(l, true);
                U.Metin(l, ad);
                l.style.left = p.x;
                l.style.top = p.y;
                l.style.translate = new StyleTranslate(new Translate(Length.Percent(-50f), Length.Percent(-50f)));
                l.style.rotate = new StyleRotate(new Rotate(new Angle(dikey ? -90f : 0f)));
            }

            // ---------------------------------------------------------------- çizim

            static void Dortgen(float ax, float ay, float bx, float by, Color col)
            {
                if (bx <= ax || by <= ay) return;
                if (kv + 4 > kose.Length) System.Array.Resize(ref kose, kose.Length * 2);
                if (kd + 6 > dizin.Length) System.Array.Resize(ref dizin, dizin.Length * 2);
                kose[kv] = new Vertex { position = new Vector3(ax, ay, Vertex.nearZ), tint = col };
                kose[kv + 1] = new Vertex { position = new Vector3(bx, ay, Vertex.nearZ), tint = col };
                kose[kv + 2] = new Vertex { position = new Vector3(bx, by, Vertex.nearZ), tint = col };
                kose[kv + 3] = new Vertex { position = new Vector3(ax, by, Vertex.nearZ), tint = col };
                dizin[kd] = (ushort)kv; dizin[kd + 1] = (ushort)(kv + 1); dizin[kd + 2] = (ushort)(kv + 2);
                dizin[kd + 3] = (ushort)kv; dizin[kd + 4] = (ushort)(kv + 2); dizin[kd + 5] = (ushort)(kv + 3);
                kv += 4; kd += 6;
            }

            static bool Bina(int x, int y)
            {
                if (x < 0 || y < 0 || x >= CityMap.Width || y >= CityMap.Height) return false;
                if (CityMap.RoadAt(x, y) || CityMap.WaterAt(x, y) || CityMap.RiverBankAt(x, y)) return false;
                var z = CityMap.ZoneOf(x, y);
                switch (z)
                {
                    case Zone.Park: case Zone.Plaza: case Zone.Country: case Zone.River: return false;
                    case Zone.Warehouse: case Zone.Mall: return true;
                }
                // cepheler: yola komşu kareler bina, içerisi avlu / bahçe
                return CityMap.RoadAt(x - 1, y) || CityMap.RoadAt(x + 1, y) || CityMap.RoadAt(x, y - 1) || CityMap.RoadAt(x, y + 1)
                       || z == Zone.HighRise || z == Zone.OldTown;
            }

            static Color Kilit(Color col, int x, int y, CityMap cm) => cm.IsUnlocked(x, y) ? col : Color.Lerp(col, Color.black, 0.55f);

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

                kv = 0; kd = 0;
                Dortgen(0f, 0f, w, h, CKir);   // şehrin dışı: kır
                float yarim = s / 2f, b = Mathf.Max(1f, s * 0.10f), ic = Mathf.Max(0.8f, s * 0.05f);

                // 1) Zemin: avlu, park, meydan, su
                for (int x = x0; x <= x1; x++)
                    for (int y = y0; y <= y1; y++)
                    {
                        var p = P(new Vector2(x, y), w, h);
                        float ax = p.x - yarim, ay = p.y - yarim, bx = p.x + yarim + 0.6f, by = p.y + yarim + 0.6f;
                        var z = CityMap.ZoneOf(x, y);
                        Color col;
                        if (CityMap.WaterAt(x, y)) col = CSu;
                        else if (CityMap.RiverBankAt(x, y)) col = CPark;
                        else if (z == Zone.Country) col = CKir;
                        else if (z == Zone.Park) col = CPark;
                        else if (z == Zone.Plaza) col = CMeydan;
                        else if (z == Zone.Houses || z == Zone.Campus) col = Color.Lerp(CAvlu, CPark, 0.45f);   // bahçeler
                        else col = CAvlu;
                        Dortgen(ax, ay, bx, by, Kilit(col, x, y, cm));
                    }

                // 2) Binalar: komşu binaya doğru boşluk bırakmadan birleşir
                for (int x = x0; x <= x1; x++)
                    for (int y = y0; y <= y1; y++)
                    {
                        if (!Bina(x, y)) continue;
                        var p = P(new Vector2(x, y), w, h);
                        bool sol = Bina(x - 1, y), sag = Bina(x + 1, y), ust = Bina(x, y + 1), alt = Bina(x, y - 1);
                        float ax = p.x - yarim + (sol ? 0f : b), bx = p.x + yarim + (sag ? 0.6f : -b);
                        float ay = p.y - yarim + (ust ? 0f : b), by = p.y + yarim + (alt ? 0.6f : -b);
                        Color ton = Color.Lerp(CBina, cm.MiniColor(x, y), 0.10f);
                        Dortgen(ax, ay, bx, by, Kilit(CBinaKenar, x, y, cm));
                        Dortgen(ax + (sol ? 0f : ic), ay + (ust ? 0f : ic), bx - (sag ? 0f : ic), by - (alt ? 0f : ic), Kilit(ton, x, y, cm));
                    }

                // 3) Yollar: kesintisiz şeritler (cadde geniş, ara sokak ince), caddelerde orta şerit
                for (int x = x0; x <= x1; x++)
                    for (int y = y0; y <= y1; y++)
                    {
                        if (!CityMap.RoadAt(x, y)) continue;
                        var p = P(new Vector2(x, y), w, h);
                        bool cadde = x % CityMap.Block == 0 || y % CityMap.Block == 0;
                        bool kopru = CityMap.BridgeAt(x, y);
                        float r = s * (cadde ? 0.34f : 0.22f);
                        Color col = Kilit(kopru ? CKopru : cadde ? CCadde : CSokak, x, y, cm);
                        bool sol = CityMap.RoadAt(x - 1, y), sag = CityMap.RoadAt(x + 1, y);
                        bool ust = CityMap.RoadAt(x, y + 1), alt = CityMap.RoadAt(x, y - 1);
                        // merkez kare + bağlı yönlere uzanan kollar
                        Dortgen(p.x - r, p.y - r, p.x + r, p.y + r, col);
                        if (sol || sag) Dortgen(sol ? p.x - yarim - 0.3f : p.x - r, p.y - r, sag ? p.x + yarim + 0.3f : p.x + r, p.y + r, col);
                        if (ust || alt) Dortgen(p.x - r, ust ? p.y - yarim - 0.3f : p.y - r, p.x + r, alt ? p.y + yarim + 0.3f : p.y + r, col);
                        if (cadde && s >= 16f)
                        {
                            bool kavsak = (sol || sag) && (ust || alt);
                            float t = Mathf.Max(0.8f, s * 0.035f), l = s * 0.22f;
                            if (!kavsak && (sol || sag)) Dortgen(p.x - l, p.y - t, p.x + l, p.y + t, CSerit);
                            if (!kavsak && (ust || alt)) Dortgen(p.x - t, p.y - l, p.x + t, p.y + l, CSerit);
                        }
                    }

                if (kv > 60000) return;   // ağ sınırı (olmaz: en küçük ölçekte ~1500 kare)
                var md = mgc.Allocate(kv, kd);
                for (int i = 0; i < kv; i++) md.SetNextVertex(kose[i]);
                for (int i = 0; i < kd; i++) md.SetNextIndex(dizin[i]);

                var pz = mgc.painter2D;
                // Sözleşmeli dükkanlar: koyu halkalı renkli nokta
                foreach (var shop in cm.Shops)
                {
                    if (!shop.contracted) continue;
                    var p = P(shop.tile, w, h);
                    if (p.x < -20f || p.y < -20f || p.x > w + 20f || p.y > h + 20f) continue;
                    Daire(pz, p, 5.5f, new Color(0.08f, 0.07f, 0.06f));
                    Daire(pz, p, 4f, shop.def.color);
                }

                var cur = c.CurrentOrder;
                bool dukkana = cur != null && Dukkana(c);

                // Dükkana giderken ikinci ayak (dükkan → müşteri): soluk kesik çizgi
                if (cur != null && dukkana)
                    KesikL(pz, P(cur.shop.tile, w, h), P(cur.customerTile, w, h), U.A(PGreen, 0.55f));

                // Rota: koyu kenar + beyaz çizgi (ızgarada önce yatay sonra dikey)
                var path = c.PathList;
                if (cur != null && path != null && path.Count > 0 && c.PathIndex < path.Count)
                {
                    for (int kat = 0; kat < 2; kat++)
                    {
                        pz.strokeColor = kat == 0 ? new Color(0.03f, 0.03f, 0.03f, 0.55f) : Color.white;
                        pz.lineWidth = kat == 0 ? 8f : 4.5f;
                        pz.lineCap = LineCap.Round;
                        pz.lineJoin = LineJoin.Round;
                        pz.BeginPath();
                        var once = P(c.Position, w, h);
                        pz.MoveTo(once);
                        for (int i = c.PathIndex; i < path.Count; i++)
                        {
                            var bn = P(path[i], w, h);
                            pz.LineTo(new Vector2(bn.x, once.y));
                            pz.LineTo(bn);
                            once = bn;
                        }
                        pz.Stroke();
                    }
                }

                // Hedef iğneleri: önce uzaktaki (ikinci) hedef, sonra şu anki
                if (cur != null)
                {
                    Vector2 ds = P(cur.shop.tile, w, h), dm = P(cur.customerTile, w, h);
                    if (dukkana) { Igne(pz, dm, PGreen, false); Igne(pz, ds, PAccent, true); }
                    else Igne(pz, dm, PGreen, true);
                }

                // Kurye: nabız halkası ve gidiş yönü konisi (beyaz yuvarlak işaret UI elemanı olarak üstte)
                var mp = P(c.Position, w, h);
                float nr = 13f + nabiz * 16f;
                Daire(pz, mp, nr, U.A(PAccent, 0.28f * (1f - nabiz)));
                Daire(pz, mp, 16f, U.A(PAccent, 0.16f));
                if (durgun < 0.6f && c.State != CourierState.Bekliyor && c.State != CourierState.KataCikiyor)
                {
                    var ekranYon = new Vector2(yon.x, -yon.y);
                    var dik = new Vector2(-ekranYon.y, ekranYon.x);
                    pz.fillColor = U.A(PAccent, 0.55f);
                    pz.BeginPath();
                    pz.MoveTo(mp + ekranYon * 27f);
                    pz.LineTo(mp + ekranYon * 9f + dik * 10f);
                    pz.LineTo(mp + ekranYon * 9f - dik * 10f);
                    pz.ClosePath();
                    pz.Fill();
                }
            }

            /// <summary>Harita iğnesi: gölge, renkli damla, ortasında beyaz nokta; ucu hedef karesinde.</summary>
            static void Igne(Painter2D pz, Vector2 uc, Color renk, bool etkin)
            {
                float r = etkin ? 10f : 7.5f;
                Vector2 m = uc + new Vector2(0f, -r * 1.75f);
                // gölge
                pz.fillColor = new Color(0f, 0f, 0f, 0.35f);
                pz.BeginPath();
                pz.Arc(uc + new Vector2(0f, 1f), r * 0.55f, 0f, 360f);
                pz.ClosePath();
                pz.Fill();
                if (!etkin) renk = Color.Lerp(renk, new Color(0.3f, 0.3f, 0.3f), 0.35f);
                // damla: daire + uca inen üçgen
                pz.fillColor = new Color(0.05f, 0.05f, 0.05f, 0.9f);
                pz.BeginPath();
                pz.Arc(m, r + 1.5f, 0f, 360f);
                pz.ClosePath();
                pz.Fill();
                pz.BeginPath();
                pz.MoveTo(m + new Vector2(-r * 0.80f, r * 0.55f));
                pz.LineTo(uc + new Vector2(0f, 1.5f));
                pz.LineTo(m + new Vector2(r * 0.80f, r * 0.55f));
                pz.ClosePath();
                pz.Fill();
                pz.fillColor = renk;
                pz.BeginPath();
                pz.Arc(m, r, 0f, 360f);
                pz.ClosePath();
                pz.Fill();
                pz.BeginPath();
                pz.MoveTo(m + new Vector2(-r * 0.72f, r * 0.62f));
                pz.LineTo(uc);
                pz.LineTo(m + new Vector2(r * 0.72f, r * 0.62f));
                pz.ClosePath();
                pz.Fill();
                Daire(pz, m, r * 0.42f, Color.white);
            }

            /// <summary>İki nokta arasında (önce yatay, sonra dikey) kesik çizgi.</summary>
            static void KesikL(Painter2D pz, Vector2 a, Vector2 b, Color renk)
            {
                pz.strokeColor = renk;
                pz.lineWidth = 3f;
                pz.lineCap = LineCap.Round;
                Vector2 k = new Vector2(b.x, a.y);
                foreach (var (p, q) in new[] { (a, k), (k, b) })
                {
                    float L = Vector2.Distance(p, q);
                    if (L < 1f) continue;
                    Vector2 d = (q - p) / L;
                    for (float t = 0f; t < L; t += 11f)
                    {
                        pz.BeginPath();
                        pz.MoveTo(p + d * t);
                        pz.LineTo(p + d * Mathf.Min(L, t + 5f));
                        pz.Stroke();
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
                U.Zemin(sol, aktif ? Color.Lerp(CompanyProfile.Primary, Color.white, 0.1f) : new Color(0.24f, 0.22f, 0.20f));
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
                p.strokeColor = new Color(0.26f, 0.24f, 0.22f);
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
                U.Renk(etiket, U.Okunur(c));
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
