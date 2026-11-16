using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Büyük kurye kartı (UI Toolkit; Mert'in IMGUI CourierBigCard.cs'i yerine): Kuryeler penceresinde "Büyük kart" ile ekranın
    /// ortasında açılır. Üst katmanda (katUst), kendi perdesiyle; olay kartı, Esc menüsü ve onay kutusu onun da üstünde açılır
    /// (o sırada büyük kart gizlenir, kapanınca geri gelir).
    ///  • Üstte kimlik: büyük portre (tecrübe rozetiyle), isim ve araç, meslek, çalışma saatleri, durum çipleri (durum, yorgun,
    ///    sigortasız), moral ve yorgunluk çubukları.
    ///  • Altında 2×2 kutu: Beceriler (yıldızlar, aynı anda, seçicilik) · Para · Performans (haftalık hedef çubuğu) · Ekipman.
    ///    İçerik sığmazsa kartın içinde kayar; düğmeler hep altta: Telefonunu aç · Ekipman · Kapat.
    /// Arkadaki arayüz kilitli; Esc, "Kapat" ya da kartın dışına tıklamak kapatır. Oyun durmaz.
    /// </summary>
    public partial class GameUI
    {
        Courier bigCardCourier;

        bool BigCardOpen => bigCardCourier != null;

        void OpenBigCard(Courier c) { bigCardCourier = c; bkKirli = true; bkKaydirSifirla = true; }

        void CloseBigCard() => bigCardCourier = null;

        /// <summary>Esc kapatır (OnGUI'den; olay ve onay kutusu yokken).</summary>
        void HandleBigCardKey()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                e.Use();
                deferred = CloseBigCard;
            }
        }

        VisualElement bkPerde, bkKart, bkPortre, bkAracSimge, bkCipler;
        Label bkSeviye, bkAd, bkArac, bkMeslek, bkSaatler, bkMoralDeger, bkYorgDeger;
        U.Cubuk bkMoral, bkYorgunluk;
        ScrollView bkKaydir;
        Yazici bkBeceri, bkPara, bkPerformans, bkEkipman;
        bool bkKirli = true, bkKaydirSifirla;
        int bkPortreRuh = -1;
        Courier bkPortreFor;

        void UiBuyukKartKur()
        {
            bkPerde = U.El(katUst, "perde", "bk-perde");
            // Kartın dışına tıklamak kapatır
            bkPerde.RegisterCallback<PointerDownEvent>(e => { if (e.target == bkPerde) CloseBigCard(); });
            bkKart = U.El(bkPerde, "modal-kart", "bk-kart");
            bkKart.RegisterCallback<PointerUpEvent>(_ => bkKirli = true, TrickleDown.TrickleDown);

            bkKaydir = new ScrollView(ScrollViewMode.Vertical);
            bkKaydir.AddToClassList("kaydir");
            bkKaydir.AddToClassList("bk-govde");
            bkKaydir.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            bkKart.Add(bkKaydir);
            var ic = bkKaydir.contentContainer;

            // Kimlik
            var bas = U.Gecirgen(ic, "satir", "bk-bas");
            bkPortre = U.Gecirgen(bas, "portre", "bk-portre");
            bkSeviye = U.Kalin(bkPortre, "", "yildiz", "bk-seviye");
            Ipucu(bkSeviye, () => bigCardCourier != null ? $"Tecrübe {bigCardCourier.level}: teslimat yaptıkça artar; her seviye biraz daha hızlı." : "");
            var sag = U.Gecirgen(bas, "esnek", "bk-kimlik");
            var adSatir = U.Gecirgen(sag, "satir");
            bkAd = U.Kalin(adSatir, "", "bk-ad", "tek-satir");
            bkAd.style.flexShrink = 1;
            U.Gecirgen(adSatir, "bosluk");
            var arac = U.Gecirgen(adSatir, "bk-arac");
            bkAracSimge = U.Simge(arac, Icons.Kind.Bike, UITheme.TextDim, 14f);
            bkArac = U.Yazi(arac, "", "kucuk");
            bkArac.style.marginLeft = 5f;
            bkMeslek = U.Yazi(sag, "", "kucuk", "soluk");
            bkSaatler = U.Yazi(sag, "", "kucuk", "soluk");
            bkSaatler.style.marginTop = 2f;
            bkCipler = U.Gecirgen(sag, "cipler");
            var m = U.Gecirgen(sag, "olcu");
            U.Yazi(m, "Moral", "olcu-ad");
            bkMoral = U.CubukEkle(m, 0f, UITheme.Good);
            bkMoralDeger = U.Yazi(m, "", "olcu-deger");
            var yo = U.Gecirgen(sag, "olcu");
            U.Yazi(yo, "Yorgunluk", "olcu-ad");
            bkYorgunluk = U.CubukEkle(yo, 0f, FatigueColor);
            bkYorgDeger = U.Yazi(yo, "", "olcu-deger");

            // 2×2 kutu
            var s1 = U.Gecirgen(ic, "bk-satir");
            bkBeceri = new Yazici(this, U.El(s1, "kart", "bk-kutu"));
            bkPara = new Yazici(this, U.El(s1, "kart", "bk-kutu"));
            var s2 = U.Gecirgen(ic, "bk-satir");
            bkPerformans = new Yazici(this, U.El(s2, "kart", "bk-kutu"));
            bkEkipman = new Yazici(this, U.El(s2, "kart", "bk-kutu"));

            // Düğmeler: kaymaz, hep altta
            var dg = U.El(bkKart, "satir", "bk-dugmeler");
            U.Dugme(dg, "Telefonunu aç", () =>
            {
                var c = bigCardCourier;
                CloseBigCard();
                if (c != null) TelefonAc(c);
            }, "ilk");
            U.Dugme(dg, "Ekipman", () =>
            {
                var c = bigCardCourier;
                CloseBigCard();
                selectedCourier = c;
                tab = Tab.Ekipman;
                panelOpen = true;
                scroll = Vector2.zero;
            });
            U.Gecirgen(dg, "bosluk");
            U.Dugme(dg, "Kapat", CloseBigCard).style.minWidth = 110f;
            U.Goster(bkPerde, false);
        }

        void UiBuyukKartYenile(bool agir)
        {
            var c = bigCardCourier;
            if (c != null && (CourierManager.I == null || !CourierManager.I.Couriers.Contains(c))) { bigCardCourier = null; c = null; }
            // Olay kartı, Esc menüsü ya da onay kutusu açıkken gizli (onlar perdeleriyle üstte; kapanınca kart geri gelir)
            bool gor = c != null && UiAcikModal == UiModalTur.Yok && GameClock.I != null && EconomyManager.I != null;
            U.Goster(bkPerde, gor);
            if (!gor) { bkKirli = true; return; }
            float sw = UiEkranGenisligi, sh = UiEkranYuksekligi;
            bkKart.style.width = Mathf.Min(760f, sw - 40f);
            bkKart.style.maxHeight = Mathf.Min(680f, sh - 40f);
            if (!agir && !bkKirli) return;
            bkKirli = false;
            if (bkKaydirSifirla)
            {
                bkKaydirSifirla = false;
                bkKaydir.scrollOffset = Vector2.zero;
            }

            // Kimlik
            int ruh = Portraits.MoodOf(c.morale);
            if (bkPortreFor != c || bkPortreRuh != ruh)
            {
                var tex = Portraits.Doku(c.courierName, ruh, CompanyProfile.Primary);
                if (tex != null) bkPortre.style.backgroundImage = new StyleBackground(tex);
                bkPortreFor = c;
                bkPortreRuh = ruh;
            }
            U.Goster(bkSeviye, c.level > 0);
            U.Metin(bkSeviye, "★" + c.level);
            U.Metin(bkAd, c.courierName);
            U.SimgeAyarla(bkAracSimge, Icons.Of(c.vehicle), UITheme.TextDim);
            U.Metin(bkArac, c.V.name);
            U.Metin(bkMeslek, c.OccupationName);
            U.Metin(bkSaatler, "Çalışma saatleri: " + string.Join("  ·  ", WorkInfoFor(c.occupation, c.scheduleSeed, GameClock.I).lines));
            CiplerAyarla(c);
            bkMoral.Ayarla(c.morale, Icons.MoodColor(c.morale));
            U.Metin(bkMoralDeger, $"%{c.morale * 100f:0}");
            bkYorgunluk.Ayarla(c.fatigue, FatigueColor);
            U.Metin(bkYorgDeger, $"%{c.fatigue * 100f:0}");

            BkBeceriler(bkBeceri, c);
            BkPara(bkPara, c);
            BkPerformans(bkPerformans, c);
            BkEkipman(bkEkipman, c);
        }

        readonly List<string> bkCipAd = new List<string>();
        readonly List<Color> bkCipRenk = new List<Color>();

        void CiplerAyarla(Courier c)
        {
            bkCipAd.Clear(); bkCipRenk.Clear();
            bkCipAd.Add(c.StateName); bkCipRenk.Add(CourierStateColor(c));
            if (c.fatigue >= 0.6f) { bkCipAd.Add("Yorgun"); bkCipRenk.Add(UITheme.Warn); }
            if (!c.insured) { bkCipAd.Add("Sigortasız"); bkCipRenk.Add(NeutralTone); }
            while (bkCipler.childCount < bkCipAd.Count) U.Cip(bkCipler, "", Color.white);
            while (bkCipler.childCount > bkCipAd.Count) bkCipler.RemoveAt(bkCipler.childCount - 1);
            for (int i = 0; i < bkCipAd.Count; i++)
            {
                var l = (Label)bkCipler[i];
                U.Metin(l, bkCipAd[i]);
                U.CipRengi(l, bkCipRenk[i]);
            }
        }

        void BkBeceriler(Yazici y, Courier c)
        {
            y.Basla();
            y.Yazi("BECERİLER", "bk-baslik");
            BkYildiz(y, "Hız", SpeedStars(c.speedSkill), "teslimatları daha çabuk bitirir.");
            BkYildiz(y, "Dikkat", CareStars(c.care), "siparişte eksik ve hasar az olur, kaskını daha çok takar.");
            BkYildiz(y, "Nezaket", CareStars(c.politeness), "müşteriler daha yüksek puan verir.");
            BkYildiz(y, "Dayanıklılık", StaminaStars(c.stamina), "geç yorulur, kapıya ve kata daha çabuk çıkar.");
            y.Bosluk(4);
            y.KV("Aynı anda", $"{c.EffectiveCapacity} paket");
            y.Kucuk($"Seçicilik: {c.PickinessText}");
            y.Bitir();
        }

        /// <summary>Beceri adı solda, beş yıldız sağda; üstüne gelince ne işe yaradığı.</summary>
        void BkYildiz(Yazici y, string ad, int yildiz, string aciklama)
        {
            var e = y.Ozel("yildiz-satir", () =>
            {
                var s = new VisualElement();
                s.AddToClassList("bk-beceri");
                s.pickingMode = PickingMode.Position;
                U.Yazi(s, "", "beceri-ad");
                U.Gecirgen(s, "bosluk");
                var k = U.Gecirgen(s, "satir");
                for (int i = 0; i < 5; i++) U.Simge(k, Icons.Kind.Star, StarOff, 13f).style.marginLeft = 2f;
                Ipucu(s, () => s.userData as string);
                return s;
            });
            U.Metin((Label)e[0], ad);
            var yildizlar = e[2];
            for (int i = 0; i < yildizlar.childCount; i++) U.SimgeAyarla(yildizlar[i], Icons.Kind.Star, i < yildiz ? StarOn : StarOff);
            e.userData = $"<b>{ad}</b> {yildiz}/5: {aciklama}";
        }

        void BkPara(Yazici y, Courier c)
        {
            y.Basla();
            y.Yazi("PARA", "bk-baslik");
            y.KV("Hesabı", Ft(c.balance));
            y.KV("Kesim", EconomyManager.I.NextCutText);
            if (c.pendingPayout > 0) y.KV("Yatacak", Ft(c.pendingPayout));
            y.KV("Bugüne kadar yatan", Ft(c.receivedTotal));
            y.KV("Bahşiş (toplam)", Ft(c.tipsTotal));
            y.KV("Paket başı", Ft(c.V.basePay));
            y.KV("Km başı", $"{Ft(c.V.payPerKm)} + bonus");
            y.Bitir();
        }

        void BkPerformans(Yazici y, Courier c)
        {
            y.Basla();
            y.Yazi("PERFORMANS", "bk-baslik");
            y.KV("Bugün", $"{c.deliveriesToday} teslimat · {Ft(c.earnedToday)}");
            y.KV("Bu hafta", $"{c.weekDeliveries} teslimat · {Ft(c.earnedWeek)}");
            y.KV("Toplam", $"{c.deliveries} teslimat");
            y.KV("Km", $"bugün {c.kmToday:0} · hafta {c.kmWeek:0}");
            var camp = CampaignManager.I;
            if (camp != null && camp.weeklyChallenge && !c.weeklyRewardClaimed)
                y.Cubuk("Haftalık", c.weekDeliveries / (float)Mathf.Max(1, camp.weeklyTarget), new Color(0.4f, 0.7f, 1f),
                        $"{c.weekDeliveries}/{camp.weeklyTarget}");
            y.Bitir();
        }

        void BkEkipman(Yazici y, Courier c)
        {
            y.Basla();
            y.Yazi("EKİPMAN", "bk-baslik");
            y.Kucuk(EquipmentText(c));
            y.Bosluk(4);
            y.KV("Çanta kullanımı", $"{c.bagUses}/{Courier.BagWornUses}", c.BagWorn ? UITheme.Warn : (Color?)null);
            if (c.BagWorn) y.Uyari("Çanta yıprandı, yenisi gerekiyor.");
            y.KV("Sigorta", c.insured ? $"var · haftalık {Ft(c.CurrentPremium)}" : "yok");
            y.KV("Araçları", c.VehiclesText);
            y.Bitir();
        }
    }
}
