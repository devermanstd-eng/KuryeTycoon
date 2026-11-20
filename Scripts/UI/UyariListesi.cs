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

        /// <summary>Uyarının yerine gider; kuryeyse telefonunu, dükkansa bilgi kartını açar.</summary>
        void UyariyaGit(Uyari u)
        {
            if (MapCamera.I != null) MapCamera.I.FocusOn(u.yer, 0.7f);
            if (u.kurye != null && CourierManager.I.Couriers.Contains(u.kurye))
            {
                TelefonAc(u.kurye);
            }
            else if (u.dukkan != null) DukkanKartiAc(u.dukkan);
        }
    }
}
