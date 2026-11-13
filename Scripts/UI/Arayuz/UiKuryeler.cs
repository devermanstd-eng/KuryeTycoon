using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Kuryeler penceresi (UI Toolkit). Üstte ekibin özeti, araç filtresi ve sıralama; her kurye sade bir kart:
    /// profil resmi (tecrübe rozeti) · isim ve araç · tek durum etiketi · moral ve yorgunluk çubukları · bugünkü iş.
    /// Sorunlar (yorgunluk, nakit, çanta, eksikler, bırakma riski) tek bir uyarı simgesinde toplanır; üstüne gelince listesi.
    /// Karta tıklayınca ayrıntı açılır: beceriler, meslek, hesap, sigorta, hedefler ve işlemler (büyüt, telefon, ekipman, çanta, işten çıkar).
    /// </summary>
    public partial class GameUI
    {
        class UiKuryeler : UiSekme
        {
            VisualElement ozet, kartKap;
            Label bosNot;
            U.Secenekler filtre, siralama;
            U.Liste<Courier, Courier, Kart> kartlar;
            readonly HashSet<Courier> acik = new HashSet<Courier>();
            int arac = -1;           // -1 hepsi, yoksa (int)VehicleKind
            int sira;                // 0 durum, 1 moral, 2 yorgunluk, 3 bugün, 4 ad
            string ozetImza = "";

            static readonly string[] SiraAdlari = { "Durum", "Moral", "Yorgunluk", "Bugün", "Ad" };

            class Kart
            {
                public Courier c;
                public VisualElement kok, ust, portre, sorun, aracSimge, ayrinti, yildizlar;
                public Label ad, durum, yildiz, bugun, moralDeger, yorgDeger, aracAd;
                public U.Cubuk moral, yorgunluk;
                public Label meslek, kapasite, kazanc, hesap, sigorta, hedef, ekipman, canta, eksikler;
                public Button cantaDugme, cikarDugme;
                public int portreRuh = -1;
                public VisualElement ozellikSatir;
                public readonly List<(Button b, Label durum, KuryeEgitim.Tur t)> egitimler = new List<(Button, Label, KuryeEgitim.Tur)>();
            }

            protected override void Kur()
            {
                var ust = Ust();   // özet, filtre ve sıralama kaymaz
                ozet = U.El(ust, "cipler");
                ui.Ipucu(ozet, "Kuryeler serbest çalışır, istedikleri saatte çevrimiçi olurlar. Saatte iyi kazanınca mutlu olurlar; uzun günler yorgunluk biriktirir. " +
                               "Asıl mesleklerine, yorgunluklarına ve günlük hayatlarına göre çevrimiçi/çevrimdışı olurlar; onları zorlayamazsın. " +
                               "Kendi araçlarını ve sigortalarını kendi kazançlarından öderler.");

                var filtreler = U.El(ust, "satir");
                filtreler.style.marginTop = 6;
                filtreler.style.marginBottom = 6;
                filtre = U.SecenekEkle(filtreler, new[] { "Hepsi", "Bisiklet", "Scooter", "Motor", "Araba" }, () => arac + 1, v => arac = v - 1);
                filtre.kok.style.flexGrow = 1;
                var sr = U.El(ust, "satir");
                sr.style.marginBottom = 8;
                U.Yazi(sr, "Sırala:", "kucuk", "soluk").style.marginRight = 8;
                siralama = U.SecenekEkle(sr, SiraAdlari, () => sira, v => sira = v);
                siralama.kok.style.flexGrow = 1;

                bosNot = U.Yazi(kok, "", "kucuk", "soluk");
                kartKap = U.El(kok, "kolon");
                kartlar = new U.Liste<Courier, Courier, Kart>(kartKap, c => c, KartKur, k => k.kok, KartYenile);
            }

            Kart KartKur(Courier c)
            {
                var k = new Kart { c = c };
                k.kok = U.El(null, "kart");
                k.ust = U.Tikla(U.El(k.kok, "satir"), () => { if (!acik.Remove(k.c)) acik.Add(k.c); KartYenile(k, k.c); });
                k.ust.style.alignItems = Align.FlexStart;

                k.portre = U.Gecirgen(k.ust, "portre");
                k.yildiz = U.Kalin(k.portre, "", "yildiz");
                ui.Ipucu(k.yildiz, () => $"Tecrübe {k.c.level}: teslimat yaptıkça artar; her seviye biraz daha hızlı.");

                var ana = U.Gecirgen(k.ust, "kurye-ana");
                var ilk = U.Gecirgen(ana, "satir");
                k.ad = U.Kalin(ilk, "", "buyuk", "tek-satir");
                k.ad.style.flexShrink = 1;
                k.sorun = U.Simge(ilk, Icons.Kind.Warning, UITheme.Warn, 15f);
                k.sorun.style.marginLeft = 6;
                ui.Ipucu(k.sorun, () => string.Join("\n", Sorunlar(k.c)));
                U.Gecirgen(ilk, "bosluk");
                k.aracSimge = U.Simge(ilk, Icons.Kind.Bike, UITheme.TextDim, 14f);
                k.aracAd = U.Yazi(ilk, "", "minik", "soluk");
                k.aracAd.style.marginLeft = 4;

                var ikinci = U.Gecirgen(ana, "satir");
                ikinci.style.marginTop = 3;
                k.durum = U.Kalin(ikinci, "", "cip");
                k.bugun = U.Yazi(ikinci, "", "minik", "soluk", "tek-satir");
                k.bugun.style.marginLeft = 4;

                // Kalıcı özellikler (gece kuşu, güler yüzlü...): üstüne gelince ne yaptığı
                k.ozellikSatir = U.Gecirgen(ana, "cipler");
                foreach (int t in c.ozellikler)
                {
                    int tt = t;
                    var cip = U.Cip(k.ozellikSatir, KuryeOzellik.Ad(t), KuryeOzellik.OlumluMu(t) ? UITheme.Good : UITheme.Warn);
                    ui.Ipucu(cip, () => $"{KuryeOzellik.Ad(tt)}: {KuryeOzellik.Aciklama(tt)}");
                }

                var m = U.Gecirgen(ana, "olcu");
                U.Yazi(m, "Moral", "olcu-ad");
                k.moral = U.CubukEkle(m, 0f, UITheme.Good);
                k.moralDeger = U.Yazi(m, "", "olcu-deger");
                var y = U.Gecirgen(ana, "olcu");
                U.Yazi(y, "Yorgunluk", "olcu-ad");
                k.yorgunluk = U.CubukEkle(y, 0f, FatigueColor);
                k.yorgDeger = U.Yazi(y, "", "olcu-deger");

                // Ayrıntı
                k.ayrinti = U.El(k.kok, "kolon");
                k.ayrinti.style.marginTop = 8;
                k.eksikler = U.Yazi(k.ayrinti, "", "kucuk", "uyari-renk");
                k.yildizlar = U.El(k.ayrinti, "kolon");
                BeceriSatiri(k.yildizlar, k, "Hız", c2 => SpeedStars(c2.speedSkill), "teslimatları daha çabuk bitirir.");
                BeceriSatiri(k.yildizlar, k, "Dikkat", c2 => CareStars(c2.care), "siparişte eksik ve hasar az olur.");
                BeceriSatiri(k.yildizlar, k, "Nezaket", c2 => CareStars(c2.politeness), "müşteriler daha yüksek puan verir.");
                BeceriSatiri(k.yildizlar, k, "Dayanıklılık", c2 => StaminaStars(c2.stamina), "geç yorulur, kapıya ve kata daha çabuk çıkar.");
                k.meslek = U.Yazi(k.ayrinti, "", "kucuk");
                k.kapasite = U.Yazi(k.ayrinti, "", "kucuk", "soluk");
                k.kazanc = U.Yazi(k.ayrinti, "", "kucuk", "soluk");
                k.hesap = U.Yazi(k.ayrinti, "", "kucuk", "soluk");
                k.sigorta = U.Yazi(k.ayrinti, "", "kucuk", "soluk");
                k.hedef = U.Yazi(k.ayrinti, "", "kucuk", "soluk");
                k.ekipman = U.Yazi(k.ayrinti, "", "kucuk", "soluk");
                k.canta = U.Yazi(k.ayrinti, "", "kucuk", "soluk");
                foreach (var l in new[] { k.meslek, k.kapasite, k.kazanc, k.hesap, k.sigorta, k.hedef, k.ekipman, k.canta }) l.style.marginTop = 3;

                // Eğitimler: şirket öder, beceri artar ya da yeni sipariş türü açılır
                U.Bolum(k.ayrinti, "EĞİTİM");
                foreach (KuryeEgitim.Tur t in System.Enum.GetValues(typeof(KuryeEgitim.Tur)))
                {
                    var tt = t;
                    var sat = U.El(k.ayrinti, "satir");
                    sat.style.marginBottom = 4;
                    var ad = U.Yazi(sat, KuryeEgitim.Adlar[(int)t], "kucuk", "esnek", "tek-satir");
                    ui.Ipucu(ad, () => KuryeEgitim.Aciklamalar[(int)tt]);
                    var durum = U.Yazi(sat, "Alındı ✓", "minik", "iyi");
                    var b = U.Dugme(sat, "", () => KuryeEgitim.Ver(k.c, tt), "kucuk-dg");
                    k.egitimler.Add((b, durum, t));
                }

                var dugmeler = U.El(k.ayrinti, "satir");
                dugmeler.style.marginTop = 8;
                dugmeler.style.flexWrap = Wrap.Wrap;
                U.Dugme(dugmeler, "Büyük kart", () => ui.OpenBigCard(k.c), "kucuk-dg", "ilk");
                U.Dugme(dugmeler, "Telefon", () => { ui.popupCourier = k.c; ui.popupScroll = Vector2.zero; ui.CloseInfo(); }, "kucuk-dg");
                U.Dugme(dugmeler, "Ekipman", () => { ui.selectedCourier = k.c; ui.tab = Tab.Ekipman; ui.panelOpen = true; ui.scroll = Vector2.zero; }, "kucuk-dg");
                k.cantaDugme = U.Dugme(dugmeler, "Yeni çanta", () => CourierManager.I.ReplaceBag(k.c), "kucuk-dg");
                U.Gecirgen(dugmeler, "bosluk");
                k.cikarDugme = U.Dugme(dugmeler, "İşten çıkar", () =>
                {
                    var cr = k.c;
                    ui.AskConfirm("Kuryeyle yolları ayır",
                        $"{cr.courierName} ({cr.V.name}, {cr.deliveries} teslimat) işten çıkarılsın mı? " +
                        $"Hesabında bekleyen {Ft(cr.balance)} ve varsa depozitosu ödenir. Bu geri alınamaz.",
                        "Evet, işten çıkar", () => CourierManager.I.Fire(cr));
                }, "kucuk-dg", "tehlike");
                return k;
            }

            void BeceriSatiri(VisualElement ust, Kart k, string ad, System.Func<Courier, int> yildiz, string aciklama)
            {
                var s = U.Gecirgen(ust, "satir");
                s.style.marginTop = 2;
                var l = U.Yazi(s, ad, "kucuk", "soluk");
                l.style.width = 96;
                var y = U.Gecirgen(s, "satir");
                for (int i = 0; i < 5; i++)
                {
                    var st = U.Simge(y, Icons.Kind.Star, StarOff, 13f);
                    st.style.marginRight = 2;
                }
                s.userData = (System.Func<Courier, int>)yildiz;
                ui.Ipucu(s, () => $"{ad} {yildiz(k.c)}/5: {aciklama}");
            }

            static List<string> Sorunlar(Courier c)
            {
                var l = new List<string>();
                if (c.cashBlocked) l.Add("Hesabı kilitli: nakit borcunu kapatması gerekiyor");
                if (c.morale < Denge.BirakmaMoralSiniri) l.Add("Bırakmayı düşünüyor (moral düşük)");
                if (c.burnout >= 0.5f) l.Add(c.burnout >= Denge.ZorunluIzinSiniri ? "Tükenmiş: izin günü yapacak" : "Yorgunluk birikti");
                else if (c.fatigue >= 0.6f) l.Add("Yorgun");
                if (c.cashHeld >= Courier.CashLimit * 0.7f && !c.cashBlocked && Sade.NakitBorcu) l.Add("Nakit borcu yüksek");
                if (Sade.Hijyen && c.hygiene < Courier.HygieneLimit) l.Add("Çanta kirli");
                if (c.BagWorn) l.Add("Çanta yıpranmış: yenisi gerekiyor");
                foreach (var p in c.ActivePenalties()) l.Add(p);
                if (c.RejectChance > 0f) l.Add($"Teklif reddedebilir (%{c.RejectChance * 100f:0})");
                return l;
            }

            void KartYenile(Kart k, Courier c)
            {
                k.c = c;
                var tex = Portraits.Doku(c.courierName, Portraits.MoodOf(c.morale), CompanyProfile.Primary);
                int ruh = Portraits.MoodOf(c.morale);
                if (tex != null && (k.portreRuh != ruh || k.portre.style.backgroundImage.value.texture != tex))
                {
                    k.portre.style.backgroundImage = new StyleBackground(tex);
                    k.portreRuh = ruh;
                }
                U.Metin(k.yildiz, "★" + c.level);
                U.Metin(k.ad, c.courierName);
                var sorunlar = Sorunlar(c);
                U.Goster(k.sorun, sorunlar.Count > 0);
                U.SimgeAyarla(k.sorun, Icons.Kind.Warning, c.cashBlocked || c.morale < Denge.BirakmaMoralSiniri ? UITheme.Bad : UITheme.Warn);
                U.SimgeAyarla(k.aracSimge, Icons.Of(c.vehicle), UITheme.TextDim);
                U.Metin(k.aracAd, c.V.name);
                U.Metin(k.durum, ShortState(c));
                U.CipRengi(k.durum, CourierStateColor(c));
                U.Metin(k.bugun, $"Bugün {c.deliveriesToday} teslimat · {Ft(c.earnedToday)}");
                k.moral.Ayarla(c.morale, Icons.MoodColor(c.morale));
                U.Metin(k.moralDeger, $"%{c.morale * 100f:0}");
                k.yorgunluk.Ayarla(c.fatigue, FatigueColor);
                U.Metin(k.yorgDeger, $"%{c.fatigue * 100f:0}");

                bool ac = acik.Contains(c);
                U.Goster(k.ayrinti, ac);
                if (!ac) return;
                var pens = c.ActivePenalties();
                U.Goster(k.eksikler, pens.Count > 0);
                if (pens.Count > 0) U.Metin(k.eksikler, string.Join("\n", pens));
                foreach (var satir in k.yildizlar.Children())
                {
                    if (!(satir.userData is System.Func<Courier, int> f)) continue;
                    int n = f(c);
                    var yildizKap = satir[1];
                    for (int i = 0; i < yildizKap.childCount; i++) U.SimgeAyarla(yildizKap[i], Icons.Kind.Star, i < n ? StarOn : StarOff);
                }
                U.Metin(k.meslek, $"Meslek: {c.OccupationName} · Bugün: {c.OccupationStatus}" + (c.dayOff ? " · izin günü" : "") +
                                  (c.occupation == Occupation.Ogrenci ? $"\nDers programı: {Courier.ScheduleText(c.scheduleSeed, GameClock.I)}" : ""));
                string capNote = "";
                if (c.morale >= 0.8f) capNote += ", moral yüksek +1";
                if (c.morale < 0.35f) capNote += ", moral düşük −1";
                if (c.fatigue >= 0.6f) capNote += ", yorgun −1";
                if (UpgradeManager.I.KargoBisikletiVar(c)) capNote += ", kargo bisikleti +2";
                U.Metin(k.kapasite, $"Aynı anda {c.EffectiveCapacity} paket (araç {c.V.capacity}{capNote}) · taşır: {GameData.CarryText(c.V)}");
                U.Metin(k.kazanc, $"Kazanç: {Ft(c.V.basePay)}/paket + {Ft(c.V.payPerKm)}/km + bonus · araçları: {c.VehiclesText}" +
                                  (c.tipsTotal > 0 ? $" · bahşiş toplam {Ft(c.tipsTotal)}" : ""));
                U.Metin(k.hesap, $"Hesabı: {Ft(c.balance)} (kesim {EconomyManager.I.NextCutText})" +
                                 (c.pendingPayout > 0 ? $" · yatacak {Ft(c.pendingPayout)}" : "") + $" · bugüne kadar yatan {Ft(c.receivedTotal)}");
                U.Metin(k.sigorta, c.insured ? $"Sigorta: var ({(c.insuredViaCompany ? EconomyManager.I.Plan.name : "dışarıdan")}, haftalık {Ft(c.CurrentPremium)})"
                                             : "Sigorta: yok (kazada masrafları kendi öder, daha uzun süre çalışamaz)");
                var camp = CampaignManager.I;
                var hedefler = new List<string>();
                if (camp.weeklyChallenge) hedefler.Add(c.weeklyRewardClaimed ? "haftalık hedef tamam" : $"haftalık {c.weekDeliveries}/{camp.weeklyTarget}");
                if (camp.monthlyChallenge) hedefler.Add(c.monthlyRewardClaimed ? "aylık hedef tamam" : $"aylık {c.monthDeliveries}/{camp.monthlyTarget}");
                U.Goster(k.hedef, hedefler.Count > 0);
                U.Metin(k.hedef, "Hedefler: " + string.Join(" · ", hedefler));
                U.Metin(k.ekipman, $"Ekipman: {EquipmentText(c)}");
                U.Metin(k.canta, $"Çanta kullanımı: {c.bagUses}/{Courier.BagWornUses}" + (c.BagWorn ? " · yıprandı" : "") +
                                 $" · teklif seçiciliği: {c.PickinessText}");
                k.cantaDugme.SetEnabled(c.BagWorn);
                U.Metin(k.cantaDugme, $"Yeni çanta (−{Ft(GameData.Equip(EquipmentKind.TermalCanta).price)})");
                long serbest = EconomyManager.I.FreeCash;
                foreach (var (b, durum, t) in k.egitimler)
                {
                    bool alindi = KuryeEgitim.Var(c, t);
                    U.Goster(durum, alindi);
                    U.Goster(b, !alindi);
                    if (alindi) continue;
                    int bedel = KuryeEgitim.Bedel((int)t);
                    bool aracUygun = t != KuryeEgitim.Tur.Catering || c.vehicle == VehicleKind.Motosiklet || c.vehicle == VehicleKind.Araba;
                    U.Metin(b, aracUygun ? $"Ver — {Ft(bedel)}" : "Motor/araba gerekir");
                    b.SetEnabled(aracUygun && serbest >= bedel);
                }
                k.cikarDugme.SetEnabled(c.OrderCount == 0);
                U.Metin(k.cikarDugme, c.OrderCount == 0 ? "İşten çıkar" : "İşten çıkar (paketi var)");
            }

            static int Kiyasla(Courier a, Courier b, int sira)
            {
                int r;
                switch (sira)
                {
                    case 1: r = a.morale.CompareTo(b.morale); break;
                    case 2: r = b.fatigue.CompareTo(a.fatigue); break;
                    case 3: r = b.deliveriesToday.CompareTo(a.deliveriesToday); break;
                    case 4: r = 0; break;
                    default: r = StateRank(a).CompareTo(StateRank(b)); break;
                }
                if (r == 0) r = string.CompareOrdinal(a.courierName, b.courierName);
                if (r == 0) r = a.courierId.CompareTo(b.courierId);
                return r;
            }

            public override void Yenile()
            {
                var cm = CourierManager.I;
                if (cm == null) return;
                var up = UpgradeManager.I;

                // Ekibin özeti (değişince yeniden kurulur)
                float om = 0f; int yorgun = 0, birakacak = 0;
                foreach (var c in cm.Couriers)
                {
                    om += c.morale;
                    if (c.burnout >= 0.5f) yorgun++;
                    if (c.morale < Denge.BirakmaMoralSiniri) birakacak++;
                }
                if (cm.Couriers.Count > 0) om /= cm.Couriers.Count;
                string imza = $"{cm.Couriers.Count}/{up.CourierCap}|{cm.OnlineCount}|{om:0.00}|{yorgun}|{birakacak}|{up.CourierCapFull}";
                if (imza != ozetImza)
                {
                    ozetImza = imza;
                    ozet.Clear();
                    U.Cip(ozet, $"{cm.Couriers.Count}/{up.CourierCap} hesap", up.CourierCapFull ? UITheme.Warn : U.Notr);
                    U.Cip(ozet, $"{cm.OnlineCount} çevrimiçi", cm.OnlineCount > 0 ? UITheme.Good : U.Notr);
                    if (cm.Couriers.Count > 0)
                        U.Cip(ozet, $"ort. moral %{om * 100f:0}", om >= 0.6f ? UITheme.Good : om >= 0.4f ? UITheme.Warn : UITheme.Bad);
                    if (yorgun > 0) U.Cip(ozet, $"{yorgun} kurye yorgun", UITheme.Warn);
                    if (birakacak > 0) U.Cip(ozet, $"{birakacak} kurye bırakabilir", UITheme.Bad);
                }

                // Filtre adlarında sayılar
                string[] adlar = { "Hepsi", "Bisiklet", "Scooter", "Motor", "Araba" };
                for (int i = 0; i < filtre.ogeler.Count; i++)
                {
                    int n = i == 0 ? cm.Couriers.Count : ui.CourierCountByVehicle((VehicleKind)(i - 1));
                    U.Metin(filtre.ogeler[i], $"{adlar[i]} {n}");
                }
                filtre.Yenile();
                siralama.Yenile();

                var liste = new List<Courier>();
                foreach (var c in cm.Couriers) if (PassesFilter(arac, c.vehicle)) liste.Add(c);
                int s = sira;
                liste.Sort((a, b) => Kiyasla(a, b, s));
                U.Goster(bosNot, liste.Count == 0);
                if (liste.Count == 0)
                    U.Metin(bosNot, cm.Couriers.Count == 0 ? "Henüz kurye yok: İşe Alım sekmesinden başvurulara bak."
                                                           : "Bu araçla çalışan kurye yok.");
                kartlar.Esitle(liste);
                acik.RemoveWhere(c => !cm.Couriers.Contains(c));
            }
        }
    }
}
