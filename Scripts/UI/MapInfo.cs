using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Haritada binaya veya dükkana tıklayınca sağda açılan bilgi kartı.
    ///  • Dükkan / market: tür, adres, sözleşme durumu, şu anki siparişleri, memnuniyet ve sözleşme işlemleri.
    ///  • Müşteri binası: adres, bina türü, kat / asansör / blok, bu binadaki aktif siparişler ve son teslimatlar.
    /// 3D'de yüksek binaların çatısına tıklamak da çalışır (binanın ekrandaki sütununa göre seçilir).
    /// </summary>
    public partial class GameUI
    {
        Shop infoShop;
        Vector2Int infoTile;
        bool infoBuilding;
        Vector2 infoScroll;
        GUIStyle infoTitle;

        bool showMenu;

        static string CategoryName(char c, bool market)
        {
            if (market) return c == 'L' ? "Sıvı ürünler" : c == 'F' ? "Kırılgan / taze" : "Ürünler";
            switch (c)
            {
                case 'M': return "Menüler";
                case 'A': return "Ana yemekler";
                case 'Y': return "Yan ürünler / fırın";
                case 'I': return "İçecekler";
                case 'K': return "Kahveler";
                case 'C': return "Çorbalar";
                case 'T': return "Tatlılar";
                default: return "Ekstralar / soslar";
            }
        }

        bool InfoOpen => infoShop != null || infoBuilding;

        void CloseInfo() { infoShop = null; infoBuilding = false; }

        Rect InfoRect(float sw, float sh)
        {
            float w = 370f;
            float h = Mathf.Min(600f, sh - TopBar - 150f);
            return new Rect(sw - w - 18f, TopBar + 14f, w, h);
        }

        /// <summary>Fare altındaki dükkan ya da müşteri binası. Önde (kameraya yakın) olan seçilir.</summary>
        bool PickBuilding(Vector2 m, out Shop shop, out Vector2Int tile)
        {
            shop = null;
            tile = default;
            var cam = Camera.main;
            var map = CityMap.I;
            if (cam == null || map == null || MapCamera.I == null) return false;
            if (!MapCamera.I.GroundPoint(cam, GuiToScreenPx(m), out var g)) return false;

            var shopAt = new Dictionary<Vector2Int, Shop>();
            foreach (var s in map.Shops) shopAt[s.tile] = s;

            var center = Vector2Int.RoundToInt(g);
            float bestY = float.MinValue;
            bool found = false;
            // Yüksek binaların çatısı zemindeki noktanın "arkasında" görünür: imlecin çevresindeki karelere bak
            for (int dx = -4; dx <= 4; dx++)
                for (int dy = -7; dy <= 3; dy++)
                {
                    var t = new Vector2Int(center.x + dx, center.y + dy);
                    if (!map.InBounds(t)) continue;
                    bool isShop = shopAt.TryGetValue(t, out var sh);
                    if (!isShop && !map.IsCustomerBuilding(t)) continue;

                    float h = Mathf.Max(0.05f, BuildingTop(t));
                    var w0 = CityMap.ToWorld(t);
                    Vector2 a = ToGui(cam, w0), b = ToGui(cam, w0 + new Vector3(0f, 0f, -h));
                    float half = Mathf.Max(8f, Vector2.Distance(a, ToGui(cam, w0 + new Vector3(0.42f, 0f, 0f))));
                    // İmlecin binanın ekrandaki sütununa uzaklığı
                    var ab = b - a;
                    float k = ab.sqrMagnitude < 0.01f ? 0f : Mathf.Clamp01(Vector2.Dot(m - a, ab) / ab.sqrMagnitude);
                    float d = Vector2.Distance(m, a + ab * k);
                    if (d > half) continue;
                    // Önde olan (ekranda tabanı daha aşağıda) kazanır
                    if (a.y > bestY)
                    {
                        bestY = a.y;
                        found = true;
                        tile = t;
                        shop = isShop ? sh : null;
                    }
                }
            return found;
        }

        /// <summary>Haritada sol tık: kurye yoksa bina / dükkan kartını açar, boş yere tıklayınca kapatır.</summary>
        bool HandleInfoClick(Vector2 m)
        {
            if (PickBuilding(m, out var shop, out var tile))
            {
                infoShop = shop;
                infoTile = tile;
                infoBuilding = shop == null;
                infoScroll = Vector2.zero;
                popupCourier = null;
                return true;
            }
            CloseInfo();
            return false;
        }

        static string StatusText(Order o)
        {
            switch (o.status)
            {
                case OrderStatus.Bekliyor: return "Kurye bekleniyor";
                case OrderStatus.Atandi: return o.courier != null ? $"{o.courier.ShortName} dükkana gidiyor" : "Kurye atandı";
                case OrderStatus.Alindi: return o.courier != null ? $"{o.courier.ShortName} yolda" : "Yolda";
                case OrderStatus.Teslim: return "Teslim edildi";
                default: return "İptal";
            }
        }

        void InfoHeader(Rect r, Color accent, Icons.Kind icon, string title, string subtitle)
        {
            UITheme.Box(new Rect(r.x + 14f, r.y + 14f, 40f, 40f), accent, 12f);
            Icons.Draw(new Rect(r.x + 24f, r.y + 24f, 20f, 20f), icon, accent.grayscale > 0.7f ? new Color(0.1f, 0.1f, 0.12f) : Color.white);
            if (infoTitle == null) infoTitle = new GUIStyle(header) { fontSize = 16, wordWrap = false, clipping = TextClipping.Clip };
            infoTitle.normal.textColor = UITheme.Yazi(UITheme.Text);
            GUI.Label(new Rect(r.x + 64f, r.y + 12f, r.width - 110f, 24f), title, infoTitle);
            small.normal.textColor = UITheme.Yazi(UITheme.TextDim);
            GUI.Label(new Rect(r.x + 64f, r.y + 34f, r.width - 110f, 20f), subtitle, small);
        }

        void DrawInfoPopup(float sw, float sh)
        {
            if (!InfoOpen || CityMap.I == null) return;
            var map = CityMap.I;
            var r = InfoRect(sw, sh);
            UITheme.Shadowed(r, UITheme.Bg, 14f, 8f);
            UITheme.Outline(r, UITheme.Line, 1f, 14f);

            // Kapat
            var close = new Rect(r.xMax - 40f, r.y + 12f, 26f, 26f);
            bool ch = close.Contains(Event.current.mousePosition);
            if (ch) UITheme.Box(close, UITheme.CardHi, 8f);
            Icons.Draw(new Rect(close.x + 7f, close.y + 7f, 12f, 12f), Icons.Kind.Close, ch ? UITheme.Text : UITheme.TextDim);
            if (GUI.Button(close, GUIContent.none, GUIStyle.none)) { deferred = CloseInfo; return; }

            var om = OrderManager.I;
            float now = GameClock.I.TotalMinutes;

            if (infoShop != null)
            {
                var s = infoShop;
                string kind = s.def.isMarket ? "Market" : s.def.hotFood ? "Restoran" : "Kafe";
                string status = !map.IsUnlocked(s.tile) ? "Kilitli bölge" : s.exclusive ? "Münhasır" : s.contracted ? "Sözleşmeli" : "Sözleşmesiz";
                InfoHeader(r, s.def.color, s.def.isMarket ? Icons.Kind.Orders : Icons.Kind.Shops, s.Name,
                           $"{kind} · {status} · {(ShopHours.IsOpen(s.def) ? "Açık" : "Kapalı")}");
            }
            else
            {
                var z = CityMap.ZoneOf(infoTile);
                InfoHeader(r, CityMap.RoofColor(z, infoTile.x, infoTile.y), Icons.Kind.Shops,
                           AddressOf(infoTile), $"{CityMap.ZoneName(z)} · {CityMap.DistrictOf(infoTile)}");
            }
            Fill(new Rect(r.x + 12f, r.y + 66f, r.width - 24f, 1f), UITheme.Line);

            GUILayout.BeginArea(new Rect(r.x + 12f, r.y + 74f, r.width - 24f, r.height - 84f));
            infoScroll = GUILayout.BeginScrollView(infoScroll);

            if (infoShop != null)
            {
                var s = infoShop;
                GUILayout.Label($"Adres: {AddressOf(s.tile)} · {CityMap.DistrictOf(s.tile)}", small);
                int waiting = 0, onWay = 0;
                foreach (var o in om.Pending) if (o.shop == s) waiting++;
                foreach (var o in om.Active) if (o.shop == s) onWay++;
                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                MiniStat("Bekleyen", waiting.ToString());
                MiniStat("Yolda", onWay.ToString());
                MiniStat("Bu ay", s.monthOrders.ToString());
                MiniStat("Toplam", s.ordersTotal.ToString());
                GUILayout.EndHorizontal();
                GUILayout.Space(6);
                DrawShopCard(s, RivalCompany.I, false);

                // Menü / ürünler
                var menu = Menus.Of(s.Name);
                if (menu.Length > 0)
                {
                    GUILayout.Space(4);
                    if (GUILayout.Button(showMenu ? $"Menüyü gizle ({menu.Length} ürün)" : $"Menüyü göster ({menu.Length} ürün)", smallButton))
                        showMenu = !showMenu;
                    if (showMenu)
                    {
                        GUILayout.BeginVertical(box);
                        char last = ' ';
                        foreach (var it in menu)
                        {
                            if (!s.def.isMarket && it.cat != last)
                            {
                                last = it.cat;
                                GUILayout.Label(CategoryName(it.cat, s.def.isMarket), header);
                            }
                            GUILayout.BeginHorizontal();
                            GUILayout.Label(it.name, small);
                            GUILayout.FlexibleSpace();
                            GUILayout.Label(Ft(it.price), small);
                            GUILayout.EndHorizontal();
                        }
                        GUILayout.EndVertical();
                    }
                }

                // Bu dükkanın şu anki siparişleri
                var list = new List<Order>();
                foreach (var o in om.Pending) if (o.shop == s) list.Add(o);
                foreach (var o in om.Active) if (o.shop == s) list.Add(o);
                if (list.Count > 0)
                {
                    GUILayout.Space(6);
                    GUILayout.Label("Şu anki siparişler", header);
                    foreach (var o in list) OrderLine(o, now, true);
                }
            }
            else
            {
                var t = infoTile;
                int floors = Addresses.Floors(t);
                string lift = Addresses.HasElevator(t) ? "asansörlü" : "asansörsüz (merdiven)";
                string blocks = Addresses.Blocks(t) > 1 ? $" · {Addresses.Blocks(t)} blok (A, B)" : "";
                GUILayout.Label($"{floors} katlı bina · {lift}{blocks} · katta {Addresses.FlatsPerFloor(t)} daire", small);
                if (!map.IsUnlocked(t)) GUILayout.Label("Bu bina kilitli bölgede: bölge açılınca buradan sipariş gelir.", warn);
                if (floors >= 5 && !Addresses.HasElevator(t)) GUILayout.Label("Merdivenle çıkılır: teslimat uzun sürer.", warn);

                var active = new List<Order>();
                foreach (var o in om.Pending) if (o.customerTile == t) active.Add(o);
                foreach (var o in om.Active) if (o.customerTile == t) active.Add(o);
                GUILayout.Space(6);
                GUILayout.Label(active.Count > 0 ? $"Aktif siparişler ({active.Count})" : "Aktif sipariş yok", header);
                foreach (var o in active) OrderLine(o, now, false);

                var past = new List<Order>();
                foreach (var o in om.History) if (o.customerTile == t && past.Count < 6) past.Add(o);
                if (past.Count > 0)
                {
                    GUILayout.Space(6);
                    GUILayout.Label("Son siparişler", header);
                    foreach (var o in past)
                    {
                        GUILayout.BeginVertical(box);
                        string res = o.status == OrderStatus.Teslim
                            ? $"Teslim · {o.rating:0.0}★" + (o.tip > 0 ? $" · bahşiş {Ft(o.tip)}" : "") + (o.deliveredCold ? " · soğuk" : "")
                            : "İptal";
                        GUILayout.Label($"#{o.id} {o.shop.Name} · {o.customerName}", label);
                        GUILayout.Label($"{o.ItemsText}", small);
                        GUILayout.Label(res, o.status == OrderStatus.Teslim ? small : warn);
                        GUILayout.EndVertical();
                    }
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void MiniStat(string caption, string value)
        {
            GUILayout.BeginVertical(box, GUILayout.MinWidth(70));
            small.normal.textColor = UITheme.Yazi(UITheme.TextDim);
            GUILayout.Label(caption, small);
            GUILayout.Label(value, header);
            GUILayout.EndVertical();
        }

        void OrderLine(Order o, float now, bool showCustomer)
        {
            GUILayout.BeginVertical(box);
            string who = showCustomer ? $"{o.customerName} · {o.AddressText}" : $"{o.shop.Name} · {o.customerName}";
            GUILayout.Label($"#{o.id} {who}", label);
            GUILayout.Label($"{o.ItemsText} · {GameData.KgText(o.weightKg)} · {o.DistanceKm:0.0} km" + (o.cashPay ? " · nakit" : ""), small);
            if (!showCustomer) GUILayout.Label($"{o.BuildingText}", small);
            float left = o.deadlineAt - now;
            string time = left >= 0 ? $"{left:0} dk kaldı" : $"{-left:0} dk gecikti";
            GUILayout.Label($"{StatusText(o)} · {time}", left < 5f ? warn : small);
            GUILayout.EndVertical();
        }

        static string AddressOf(Vector2Int t)
        {
            var map = CityMap.I;
            var door = map.GetDoor(t);
            return $"{Addresses.StreetOf(door)} No: {Addresses.Number(t, door)}";
        }

        /// <summary>Seçili binanın üstünde küçük işaret.</summary>
        void DrawInfoMarker()
        {
            if (!InfoOpen) return;
            var cam = Camera.main;
            if (cam == null) return;
            var t = infoShop != null ? infoShop.tile : infoTile;
            var top = CityMap.ToWorld(t) + MapCamera.LabelOffset(0.6f, BuildingTop(t) + 0.15f);
            var p = ToGui(cam, top);
            var r = new Rect(p.x - 9f, p.y - 22f, 18f, 18f);
            UITheme.Box(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), UITheme.WithAlpha(Color.black, 0.45f), 12f);
            UITheme.Box(r, UITheme.Accent, 9f);
        }
    }
}
