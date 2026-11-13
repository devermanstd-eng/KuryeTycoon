using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Sağ üstte "Dikkat" listesi: o an ilgilenilmesi gereken şeyler. Saniyede bir yenilenir, en önemlisi üstte, en fazla 5 satır.
    ///  • kurye bulunamayan siparişler • geç kalan siparişler • memnuniyeti düşen dükkanlar
    ///  • bırakmayı düşünen ya da çok yorgun kuryeler • (nakit borcu açıksa) üzerinde çok nakit olan kuryeler
    /// Satıra tıklayınca harita oraya gider ve ilgili kart (dükkan bilgisi ya da kuryenin telefonu) açılır.
    /// Başlığa tıklayınca küçülür (tercih saklanır). Sağda bir kart açıkken gizlenir.
    /// </summary>
    public partial class GameUI
    {
        struct Uyari
        {
            public string metin;
            public Color renk;
            public Vector2 yer;
            public Courier kurye;
            public Shop dukkan;
            public float oncelik;   // büyük olan üstte
        }

        readonly List<Uyari> uyarilar = new List<Uyari>();
        float uyariYenileAni;
        const int UyariMax = 5;
        const float UyariW = 310f, UyariSatir = 24f;
        const string UyariKey = "KuryeTycoon.DikkatListesiKucuk";

        static bool UyariKucuk
        {
            get { return PlayerPrefs.GetInt(UyariKey, 0) == 1; }
            set { PlayerPrefs.SetInt(UyariKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        bool UyariGorunur => uyarilar.Count > 0 && !InfoOpen && popupCourier == null;

        Rect UyariRect(float sw)
        {
            int n = UyariKucuk ? 0 : Mathf.Min(UyariMax, uyarilar.Count);
            return new Rect(sw - UyariW - 14f, TopBar + 10f, UyariW, 30f + n * UyariSatir + (n > 0 ? 6f : 0f));
        }

        void UyarilariTopla()
        {
            uyarilar.Clear();
            var om = OrderManager.I;
            var cm = CourierManager.I;
            if (om == null || cm == null || GameClock.I == null || CityMap.I == null) return;
            float now = GameClock.I.TotalMinutes;

            // 1) Kurye bulunamayan siparişler (8 dk'dan uzun bekleyen)
            Order enEski = null;
            int bekleyen = 0;
            foreach (var o in om.Pending)
            {
                if (o.courier != null || now - o.createdAt < 8f) continue;
                bekleyen++;
                if (enEski == null || o.createdAt < enEski.createdAt) enEski = o;
            }
            if (enEski != null)
            {
                float w = now - enEski.createdAt;
                uyarilar.Add(new Uyari
                {
                    metin = bekleyen == 1 ? $"#{enEski.id} {enEski.shop.Name}: {w:0} dk'dır kurye bekliyor"
                                          : $"{bekleyen} sipariş kurye bekliyor (en eskisi {w:0} dk)",
                    renk = UITheme.Warn, yer = enEski.shop.tile, dukkan = enEski.shop,
                    oncelik = 3f + Mathf.Min(1f, w / 30f),
                });
            }

            // 2) Söz verilen saati geçen siparişler
            Order enGec = null;
            int gec = 0;
            foreach (var o in om.Active)
            {
                if (now <= o.deadlineAt) continue;
                gec++;
                if (enGec == null || o.deadlineAt < enGec.deadlineAt) enGec = o;
            }
            if (enGec != null)
            {
                float fark = now - enGec.deadlineAt;
                string kim = enGec.courier != null ? " · " + enGec.courier.ShortName : "";
                uyarilar.Add(new Uyari
                {
                    metin = gec == 1 ? $"#{enGec.id} geç kalıyor (+{fark:0} dk){kim}" : $"{gec} sipariş geç kalıyor (en çok +{fark:0} dk)",
                    renk = UITheme.Bad, yer = enGec.customerTile, kurye = enGec.courier,
                    oncelik = 3.5f + Mathf.Min(1f, fark / 20f),
                });
            }

            // 3) Memnuniyeti düşen dükkanlar (%35'in altında sözleşmeyi bozabilir)
            foreach (var s in CityMap.I.Shops)
            {
                if (!s.contracted || s.own || s.satisfaction >= 40f) continue;
                uyarilar.Add(new Uyari
                {
                    metin = $"{s.Name}: memnuniyet %{s.satisfaction:0}" + (s.satisfaction < 25f ? ", ayrılabilir" : ""),
                    renk = s.satisfaction < 25f ? UITheme.Bad : UITheme.Warn, yer = s.tile, dukkan = s,
                    oncelik = 2f + (40f - s.satisfaction) / 40f,
                });
            }

            // 4) Kuryeler: bırakmak üzere, çok yorgun, (nakit borcu açıksa) çok nakit taşıyan
            foreach (var c in cm.Couriers)
            {
                if (c.morale < Denge.BirakmaMoralSiniri)
                    uyarilar.Add(new Uyari
                    {
                        metin = $"{c.ShortName} bırakmayı düşünüyor (moral %{c.morale * 100f:0})",
                        renk = UITheme.Bad, yer = c.Position, kurye = c, oncelik = 2.5f,
                    });
                else if (c.IsOnline && (c.burnout >= 0.6f || c.fatigue >= 0.85f))
                    uyarilar.Add(new Uyari
                    {
                        metin = $"{c.ShortName} çok yorgun: dinlendir ya da izin ver",
                        renk = UITheme.Warn, yer = c.Position, kurye = c, oncelik = 1.5f + c.burnout,
                    });
                if (Sade.NakitBorcu && c.cashHeld >= Courier.CashLimit * 0.7f)
                    uyarilar.Add(new Uyari
                    {
                        metin = $"{c.ShortName}: üzerinde {GameData.Ft(c.cashHeld)} nakit",
                        renk = UITheme.Warn, yer = c.Position, kurye = c, oncelik = 1.2f,
                    });
            }

            uyarilar.Sort((a, b) => b.oncelik.CompareTo(a.oncelik));
        }

        void DrawUyarilar(float sw)
        {
            if (Event.current.type == EventType.Layout && Time.unscaledTime >= uyariYenileAni)
            {
                uyariYenileAni = Time.unscaledTime + 1f;
                UyarilariTopla();
            }
            if (!UyariGorunur) return;

            var r = UyariRect(sw);
            UITheme.Shadowed(r, new Color(0.07f, 0.075f, 0.09f, 0.88f), 12f, 4f);

            // Başlık: tıklayınca küçülür / açılır
            var hr = new Rect(r.x, r.y, r.width, 30f);
            Icons.Draw(new Rect(r.x + 10f, r.y + 8f, 14f, 14f), Icons.Kind.Warning, UITheme.Warn);
            small.normal.textColor = UITheme.Yazi(new Color(0.93f, 0.94f, 0.96f));
            GUI.Label(new Rect(r.x + 30f, r.y + 5f, r.width - 60f, 20f), $"Dikkat ({uyarilar.Count})", small);
            GUI.Label(new Rect(r.xMax - 24f, r.y + 5f, 20f, 20f), UyariKucuk ? "▼" : "▲", small);
            if (GUI.Button(hr, GUIContent.none, GUIStyle.none)) { bool k = !UyariKucuk; deferred += () => UyariKucuk = k; }

            if (!UyariKucuk)
            {
                int n = Mathf.Min(UyariMax, uyarilar.Count);
                for (int i = 0; i < n; i++)
                {
                    var u = uyarilar[i];
                    var row = new Rect(r.x + 6f, r.y + 30f + i * UyariSatir, r.width - 12f, UyariSatir - 2f);
                    if (row.Contains(Event.current.mousePosition)) UITheme.Box(row, new Color(1f, 1f, 1f, 0.07f), 6f);
                    UITheme.Box(new Rect(row.x + 6f, row.center.y - 3f, 6f, 6f), u.renk, 3f);
                    small.normal.textColor = UITheme.Yazi(new Color(0.88f, 0.9f, 0.93f));
                    GUI.Label(new Rect(row.x + 18f, row.y + 2f, row.width - 22f, 20f), u.metin, small);
                    if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                    {
                        var sec = u;
                        deferred += () => UyariyaGit(sec);
                    }
                }
                if (uyarilar.Count > n)
                {
                    small.normal.textColor = UITheme.Yazi(UITheme.TextDim);
                    GUI.Label(new Rect(r.xMax - 70f, r.y + 5f, 44f, 20f), $"+{uyarilar.Count - n}", small);
                }
            }
            small.normal.textColor = UITheme.Yazi(UITheme.TextDim);
        }

        /// <summary>Uyarının yerine gider; kuryeyse telefonunu, dükkansa bilgi kartını açar.</summary>
        void UyariyaGit(Uyari u)
        {
            if (MapCamera.I != null) MapCamera.I.FocusOn(u.yer, 0.7f);
            if (u.kurye != null && CourierManager.I.Couriers.Contains(u.kurye))
            {
                CloseInfo();
                popupCourier = u.kurye;
                popupScroll = Vector2.zero;
            }
            else if (u.dukkan != null)
            {
                popupCourier = null;
                infoShop = u.dukkan;
                infoTile = u.dukkan.tile;
                infoBuilding = false;
                infoScroll = Vector2.zero;
            }
        }
    }
}
