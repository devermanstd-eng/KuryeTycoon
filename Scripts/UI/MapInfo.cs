using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Haritada binaya veya dükkana tıklayınca sağda açılan bilgi kartının seçim mantığı ve haritadaki işareti.
    /// Kartın kendisi UI Toolkit'te (Arayuz/UiBinaKarti.cs).
    /// 3D'de yüksek binaların çatısına tıklamak da çalışır (binanın ekrandaki sütununa göre seçilir).
    /// </summary>
    public partial class GameUI
    {
        Shop infoShop;
        Vector2Int infoTile;
        bool infoBuilding;

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
                binaKaydirSifirla = true;
                binaKirli = true;
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
