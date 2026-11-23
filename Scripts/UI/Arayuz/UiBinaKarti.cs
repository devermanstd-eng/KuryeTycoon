using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Bina kartı (UI Toolkit; eski IMGUI DrawInfoPopup yerine): haritada binaya veya dükkana tıklayınca sağda açılır,
    /// pencere katmanında (katPencere), kurye telefonuyla aynı yerde (ikisi birlikte açılmaz).
    ///  • Üstte renkli simge, ad ve alt yazı (dükkan: tür · sözleşme · açık/kapalı; bina: bölge türü · semt), sağda kapat.
    ///  • Dükkan / market: adres, dört sayı kutusu (bekleyen, yolda, bu ay, toplam), dükkanın ayrıntısı ve sözleşme işlemleri
    ///    (Dükkanlar penceresiyle ortak: UiDukkanAyrinti), menü / ürünler (aç-kapa), şu anki siparişler.
    ///    Kendi marketin: kısa özet ve "Market penceresini aç".
    ///  • Müşteri binası: kat / asansör / blok / daire, kilitli bölge ve merdiven uyarısı, aktif siparişler, son 6 sipariş.
    /// Seçim mantığı (PickBuilding, HandleInfoClick, haritadaki işaret) MapInfo.cs'te.
    /// </summary>
    public partial class GameUI
    {
        VisualElement binaKok, binaUst, binaRenk, binaSimge;
        Label binaAd, binaAlt;
        ScrollView binaKaydir;
        Yazici binaY;
        bool binaKirli = true, binaKaydirSifirla;
        readonly List<Order> binaListe = new List<Order>();

        /// <summary>Dükkanın kartını açar (uyarılardan; haritada tıklamak HandleInfoClick ile).</summary>
        void DukkanKartiAc(Shop s)
        {
            popupCourier = null;
            infoShop = s;
            infoTile = s.tile;
            infoBuilding = false;
            binaKaydirSifirla = true;
            binaKirli = true;
        }

        /// <summary>Müşteri binasının kartını açar (test modu).</summary>
        void BinaKartiAc(Vector2Int t)
        {
            popupCourier = null;
            infoShop = null;
            infoTile = t;
            infoBuilding = true;
            binaKaydirSifirla = true;
            binaKirli = true;
        }

        void UiBinaKartiKur()
        {
            binaKok = U.El(katPencere, "bina-kart");
            binaKok.style.width = BinaGenislik;
            binaKok.style.right = SagKartKenar;
            binaKok.style.top = TopBar + 14f;
            binaKok.RegisterCallback<PointerUpEvent>(_ => binaKirli = true, TrickleDown.TrickleDown);

            var ust = binaUst = U.Gecirgen(binaKok, "bina-ust");
            binaRenk = U.Gecirgen(ust, "bina-simge-kutu");
            binaSimge = U.Simge(binaRenk, Icons.Kind.Shops, Color.white, 20f);
            var yazi = U.Gecirgen(ust, "esnek");
            yazi.style.marginLeft = 10f;
            binaAd = U.Kalin(yazi, "", "bina-ad", "tek-satir");
            binaAlt = U.Yazi(yazi, "", "kucuk", "soluk", "tek-satir");
            var kapat = U.Tikla(U.El(ust, "simge-dugme"), CloseInfo);
            U.Simge(kapat, Icons.Kind.Close, U.Soluk, 12f);
            Ipucu(kapat, "Kapat (Esc)");
            U.Gecirgen(binaKok, "pencere-cizgi");

            binaKaydir = new ScrollView(ScrollViewMode.Vertical);
            binaKaydir.AddToClassList("kaydir");
            binaKaydir.AddToClassList("bina-govde");
            binaKaydir.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            binaKok.Add(binaKaydir);
            binaY = new Yazici(this, binaKaydir.contentContainer);
            binaY.KutuSutun = 2;   // dar kart: kutular 2'şerli satırlar (sarılan satır üst üste biniyordu)
            U.Goster(binaKok, false);
        }

        void UiBinaKartiYenile(bool agir)
        {
            bool gor = InfoOpen && CityMap.I != null && OrderManager.I != null && GameClock.I != null;
            U.Goster(binaKok, gor);
            if (!gor) { binaKirli = true; return; }
            // Kart içeriği kadar uzar, en fazla 600 (ekran küçükse daha az); fazlası kayar
            float maxH = Mathf.Max(240f, Mathf.Min(600f, UiEkranYuksekligi - TopBar - 150f));
            KaydirmaYuksekligi(binaKaydir, 18f, maxH - binaUst.layout.height - 1f);
            if (!agir && !binaKirli) return;
            binaKirli = false;
            if (binaKaydirSifirla)
            {
                binaKaydirSifirla = false;
                binaKaydir.scrollOffset = Vector2.zero;
            }

            var map = CityMap.I;
            var om = OrderManager.I;
            float now = GameClock.I.TotalMinutes;

            // Başlık
            Color renk;
            if (infoShop != null)
            {
                var s = infoShop;
                string kind = s.def.isMarket ? "Market" : s.def.hotFood ? "Restoran" : "Kafe";
                string status = !map.IsUnlocked(s.tile) ? "Kilitli bölge" : s.exclusive ? "Münhasır" : s.contracted ? "Sözleşmeli" : "Sözleşmesiz";
                renk = s.def.color;
                U.SimgeAyarla(binaSimge, s.def.isMarket ? Icons.Kind.Orders : Icons.Kind.Shops, renk.grayscale > 0.7f ? new Color(0.1f, 0.1f, 0.12f) : Color.white);
                U.Metin(binaAd, s.isletme ? s.Name : s.kargo ? KargoAnlasmasi.DepoAdi : s.own && DarkStore.I != null ? DarkStore.StoreName : s.Name);
                U.Metin(binaAlt, s.isletme ? "İş ortağın · işletme teslimatı (komisyonsuz, paket başı ücret)" :
                                 s.kargo ? "Senin kargo deponun · paket dağıtımı" : s.own ? "Senin marketin · komisyonsuz" : $"{kind} · {status} · {(ShopHours.IsOpen(s.def) ? "Açık" : "Kapalı")}");
            }
            else
            {
                var z = CityMap.ZoneOf(infoTile);
                renk = CityMap.RoofColor(z, infoTile.x, infoTile.y);
                U.SimgeAyarla(binaSimge, Icons.Kind.Shops, renk.grayscale > 0.7f ? new Color(0.1f, 0.1f, 0.12f) : Color.white);
                U.Metin(binaAd, AddressOf(infoTile));
                U.Metin(binaAlt, $"{CityMap.ZoneName(z)} · {CityMap.DistrictOf(infoTile)}");
            }
            U.Zemin(binaRenk, renk);

            var y = binaY;
            y.Basla();
            if (infoShop != null) BinaDukkan(y, infoShop, om, now);
            else BinaMusteri(y, infoTile, map, om, now);
            y.Bitir();
        }

        void BinaIsletme(Yazici y, Shop s)
        {
            var it = IsletmeTeslimati.I;
            var o = it != null ? it.OrtakBul(s) : null;
            if (o == null) return;
            y.Kucuk($"{o.Ad} müşterilerine gidecek paketleri senin kuryelerinle gönderiyor: 09:00–20:00 arası, en çok 10–11 ve 14–17 saatlerinde. " +
                    $"Paket başı {GameData.Ft(IsletmeTeslimati.Ucret(15))} (3 km), 90 dk içinde teslim edilmeli.");
            Color mc = Color.Lerp(new Color(0.9f, 0.3f, 0.3f), new Color(0.3f, 0.85f, 0.4f), o.memnuniyet / 100f);
            y.Cubuk("Memnuniyet", o.memnuniyet / 100f, mc, $"{o.memnuniyet:0}/100");
            if (o.Kusgun) y.Uyari($"Küs: {GameClock.I.DateOf(o.kusBitis)} tarihine kadar paket göndermiyor.");
            y.KutularBasla();
            y.Kutu("Bugün", $"{o.bugunTeslim} teslim", o.bugunGec > 0 ? $"{o.bugunGec} geç" : null);
            y.Kutu("Teslim edilemeyen", o.bugunKacan.ToString(), "bugün", o.bugunKacan > 0 ? U.Uyari : (Color?)null);
            y.Kutu("Bütün ortaklar · dün", $"{it.dunTeslim} paket", GameData.Ft(it.dunGelir));
            y.Kutu("Toplam", $"{it.toplamTeslim} paket", GameData.Ft(it.toplamGelir));
            y.KutularBitir();
        }

        void BinaKargo(Yazici y)
        {
            var k = KargoAnlasmasi.I;
            if (k == null) return;
            y.Kucuk($"İş günlerinde 09:00–16:00 arası paketler dağıtıma çıkar, 19:00'a kadar teslim edilmeli (cumartesi yarısı, pazar yok). " +
                    $"Paket başı {GameData.Ft(KargoAnlasmasi.Ucret(15))} (3 km); dağıtılamayan paket için {GameData.Ft(KargoAnlasmasi.Ceza)} ceza.");
            y.KutularBasla();
            y.Kutu("Bugün", $"{k.bugunTeslim}/{k.BugunPlan}", $"çıkan {k.BugunCikan}");
            y.Kutu("Dün", $"{k.dunTeslim} teslim", k.dunKacan > 0 ? $"{k.dunKacan} kaçtı" : "kaçan yok", null, null, k.dunKacan > 0 ? U.Uyari : (Color?)null);
            y.Kutu("Dünkü gelir", GameData.Ft(k.dunGelir), k.dunCeza > 0 ? $"ceza −{GameData.Ft(k.dunCeza)}" : null);
            y.Kutu("Toplam", $"{k.toplamTeslim} paket", GameData.Ft(k.toplamGelir));
            y.KutularBitir();
        }

        void BinaDukkan(Yazici y, Shop s, OrderManager om, float now)
        {
            y.Kucuk($"Adres: {AddressOf(s.tile)} · {CityMap.DistrictOf(s.tile)}");
            int waiting = 0, onWay = 0;
            foreach (var o in om.Pending) if (o.shop == s) waiting++;
            foreach (var o in om.Active) if (o.shop == s) onWay++;
            y.KutularBasla();
            y.Kutu("Bekleyen", waiting.ToString(), null, waiting > 0 ? U.Uyari : (Color?)null);
            y.Kutu("Yolda", onWay.ToString());
            y.Kutu("Bu ay", s.monthOrders.ToString());
            y.Kutu("Toplam", s.ordersTotal.ToString());
            y.KutularBitir();

            if (s.isletme) BinaIsletme(y, s);
            else if (s.kargo) BinaKargo(y);
            else if (s.own) BinaKendiMarket(y);
            else UiDukkanAyrinti(y, s, RivalCompany.I);

            // Menü / ürünler
            var menu = Menus.Of(s.Name);
            if (menu.Length > 0)
            {
                y.Bosluk(4);
                y.Dugme(showMenu ? $"Menüyü gizle ({menu.Length} ürün)" : $"Menüyü göster ({menu.Length} ürün)", () => showMenu = !showMenu, true, "kucuk-dg");
                if (showMenu)
                {
                    y.KartBasla();
                    char last = ' ';
                    foreach (var it in menu)
                    {
                        if (!s.def.isMarket && it.cat != last)
                        {
                            last = it.cat;
                            y.Yazi(CategoryName(it.cat, s.def.isMarket), "yz-baslik");
                        }
                        y.Tablo(new[] { it.name, Ft(it.price) });
                    }
                    y.KartBitir();
                }
            }

            // Bu dükkanın şu anki siparişleri
            binaListe.Clear();
            foreach (var o in om.Pending) if (o.shop == s) binaListe.Add(o);
            foreach (var o in om.Active) if (o.shop == s) binaListe.Add(o);
            if (binaListe.Count > 0)
            {
                y.Bolum($"Şu anki siparişler ({binaListe.Count})");
                foreach (var o in binaListe) BinaSiparis(y, o, now, true);
            }
        }

        /// <summary>Kendi marketine tıklayınca: kısa özet ve Market penceresi.</summary>
        void BinaKendiMarket(Yazici y)
        {
            var ds = DarkStore.I;
            if (ds == null || !ds.IsOpen) return;
            y.Kucuk($"Kendi deponun marketi · {DarkStore.SizeNames[ds.size]} · komisyon yok, brüt marj ~%{DarkStore.Margin * 100f:0}");
            y.Kucuk($"{ShopHours.StatusText(ds.shop.def)} · {ShopHours.HoursText(ds.shop.def)}");
            y.Kucuk($"Stok {Ft(ds.TotalStock)} / {Ft(ds.Capacity)} · bugün {ds.ordersToday} sipariş, brüt kâr {Ft(ds.profitToday)}");
            y.Dugme("Market penceresini aç", () => { CloseInfo(); OpenTab(Tab.Market); }, true, "vurgu");
        }

        void BinaMusteri(Yazici y, Vector2Int t, CityMap map, OrderManager om, float now)
        {
            int floors = Addresses.Floors(t);
            string lift = Addresses.HasElevator(t) ? "asansörlü" : "asansörsüz (merdiven)";
            string blocks = Addresses.Blocks(t) > 1 ? $" · {Addresses.Blocks(t)} blok (A, B)" : "";
            y.Kucuk($"{floors} katlı bina · {lift}{blocks} · katta {Addresses.FlatsPerFloor(t)} daire");
            if (!map.IsUnlocked(t)) y.Uyari("Bu bina kilitli bölgede: bölge açılınca buradan sipariş gelir.");
            if (floors >= 5 && !Addresses.HasElevator(t)) y.Uyari("Merdivenle çıkılır: teslimat uzun sürer.");

            binaListe.Clear();
            foreach (var o in om.Pending) if (o.customerTile == t) binaListe.Add(o);
            foreach (var o in om.Active) if (o.customerTile == t) binaListe.Add(o);
            y.Bolum(binaListe.Count > 0 ? $"Aktif siparişler ({binaListe.Count})" : "Aktif sipariş yok");
            foreach (var o in binaListe) BinaSiparis(y, o, now, false);

            int n = 0;
            foreach (var o in om.History)
            {
                if (o.customerTile != t) continue;
                if (n++ == 0) y.Bolum("Son siparişler");
                y.KartBasla();
                y.Yazi($"<b>#{o.id} {o.shop.Name}</b> · {o.customerName}");
                y.Kucuk(o.ItemsText);
                if (o.status == OrderStatus.Teslim)
                    y.Kucuk($"Teslim · {o.rating:0.0}★" + (o.tip > 0 ? $" · bahşiş {Ft(o.tip)}" : "") + (o.deliveredCold ? " · soğuk" : ""));
                else y.Uyari("İptal");
                y.KartBitir();
                if (n >= 6) break;
            }
        }

        /// <summary>Sipariş satırı: numara ve kim, ürünler · ağırlık · mesafe, durum ve kalan süre (5 dk altı turuncu).</summary>
        void BinaSiparis(Yazici y, Order o, float now, bool showCustomer)
        {
            y.KartBasla();
            string who = showCustomer ? $"{o.customerName} · {o.AddressText}" : $"{o.shop.Name} · {o.customerName}";
            y.Yazi($"<b>#{o.id}</b> {who}");
            y.Kucuk($"{o.ItemsText} · {GameData.KgText(o.weightKg)} · {o.DistanceKm:0.0} km" + (o.cashPay ? " · nakit" : ""));
            if (!showCustomer) y.Kucuk(o.BuildingText);
            float left = o.deadlineAt - now;
            string time = left >= 0 ? $"{left:0} dk kaldı" : $"{-left:0} dk gecikti";
            if (left < 5f) y.Uyari($"{StatusText(o)} · {time}");
            else y.Kucuk($"{StatusText(o)} · {time}");
            y.KartBitir();
        }
    }
}
