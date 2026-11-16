using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Kuryeler penceresi (UI Toolkit). Üstte ekibin özeti, araç filtresi ve sıralama; her kurye sade bir kart:
    /// profil resmi (tecrübe rozeti) · isim ve araç · tek durum etiketi · moral ve yorgunluk çubukları · bugünkü iş.
    /// Sorunlar (yorgunluk, nakit, çanta, eksikler, bırakma riski) tek bir uyarı simgesinde toplanır; üstüne gelince listesi.
    /// Karta tıklayınca ayrıntı açılır (sade): dört sayı kutusu (puan, hesap, kapasite, toplam), 2×2 beceri yıldızları,
    /// üç kısa bilgi satırı (meslek, sigorta, ekipman), eğitim çipleri ve işlemler (büyüt, telefon, ekipman, çanta, askı, kapat).
    /// Ayrıntılar (kazanç formülü, ders programı, seçicilik…) kutuların ipucunda.
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
                public Label eksikler;
                public Button cantaDugme, cikarDugme, askiDugme;
                public Label puan, puanAlt, hesap, hesapAlt, kapasite, kapasiteAlt, toplam, toplamAlt;
                public string puanIpucu = "", hesapIpucu = "", kapasiteIpucu = "", toplamIpucu = "", meslekIpucu = "";
                public VisualElement hedefSatir;
                public Label meslek, sigorta, ekipman, hedef;
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

                // Ayrıntı (sade): sayı kutuları, beceriler, kısa bilgi, eğitim çipleri, işlemler
                k.ayrinti = U.El(k.kok, "kolon");
                k.ayrinti.style.marginTop = 8;
                k.eksikler = U.Yazi(k.ayrinti, "", "kucuk", "uyari-renk");
                k.eksikler.style.marginBottom = 4;
                var kutular = U.Gecirgen(k.ayrinti, "yz-kutular");
                (k.puan, k.puanAlt) = MetrikKutu(kutular, "PUAN", () => k.puanIpucu);
                (k.hesap, k.hesapAlt) = MetrikKutu(kutular, "HESABI", () => k.hesapIpucu);
                (k.kapasite, k.kapasiteAlt) = MetrikKutu(kutular, "KAPASİTE", () => k.kapasiteIpucu);
                (k.toplam, k.toplamAlt) = MetrikKutu(kutular, "TOPLAM", () => k.toplamIpucu);

                k.yildizlar = U.Gecirgen(k.ayrinti, "beceri-izgara");
                BeceriSatiri(k.yildizlar, k, "Hız", c2 => SpeedStars(c2.speedSkill), "teslimatları daha çabuk bitirir.");
                BeceriSatiri(k.yildizlar, k, "Dikkat", c2 => CareStars(c2.care), "siparişte eksik ve hasar az olur.");
                BeceriSatiri(k.yildizlar, k, "Nezaket", c2 => CareStars(c2.politeness), "müşteriler daha yüksek puan verir.");
                BeceriSatiri(k.yildizlar, k, "Dayanıklılık", c2 => StaminaStars(c2.stamina), "geç yorulur, kapıya ve kata daha çabuk çıkar.");

                k.meslek = BilgiSatiri(k.ayrinti, "Meslek", out var meslekSatir);
                ui.Ipucu(meslekSatir, () => k.meslekIpucu);
                k.sigorta = BilgiSatiri(k.ayrinti, "Sigorta", out _);
                k.ekipman = BilgiSatiri(k.ayrinti, "Ekipman", out _);
                k.hedef = BilgiSatiri(k.ayrinti, "Hedefler", out k.hedefSatir);

                // Eğitimler: şirket öder, beceri artar ya da yeni sipariş türü açılır. Alınanlar yeşil çip, alınmayanlar düğme.
                var egitim = U.El(k.ayrinti, "satir");
                egitim.style.flexWrap = Wrap.Wrap;
                egitim.style.marginTop = 6;
                U.Yazi(egitim, "Eğitim", "kucuk", "soluk").style.width = 72;
                foreach (KuryeEgitim.Tur t in System.Enum.GetValues(typeof(KuryeEgitim.Tur)))
                {
                    var tt = t;
                    var durum = U.Cip(egitim, "✓ " + KuryeEgitim.Adlar[(int)t], UITheme.Good);
                    ui.Ipucu(durum, () => KuryeEgitim.Aciklamalar[(int)tt]);
                    var b = U.Dugme(egitim, "", () => KuryeEgitim.Ver(k.c, tt), "kucuk-dg", "ilk");
                    b.style.marginRight = 4;
                    b.style.marginBottom = 3;
                    ui.Ipucu(b, () => KuryeEgitim.Aciklamalar[(int)tt]);
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
                k.askiDugme = U.Dugme(dugmeler, "Askıya al", () =>
                {
                    var cr = k.c;
                    var ks = KayitSistemi.I;
                    if (ks == null) return;
                    ui.AskConfirm("Hesabı askıya al",
                        $"{cr.courierName} hesabı {KayitSistemi.AskiGun} gün askıya alınsın mı? Bu sürede sipariş alamaz, morali düşer. " +
                        "60 gün içinde 3. askıda hesap kendiliğinden kapanır.",
                        "Evet, askıya al", () => ks.AskiyaAl(cr, "operasyon kararı", true));
                }, "kucuk-dg");
                k.cikarDugme = U.Dugme(dugmeler, "Hesabı kapat", () =>
                {
                    var cr = k.c;
                    ui.AskConfirm("Kurye hesabını kapat",
                        $"{cr.courierName} ({cr.V.name}, {cr.deliveries} teslimat) kurye hesabı kapatılsın mı? " +
                        $"Hesabında bekleyen {Ft(cr.balance)} ve varsa depozitosu ödenir. Bu geri alınamaz.",
                        "Evet, hesabı kapat", () => CourierManager.I.Fire(cr));
                }, "kucuk-dg", "tehlike");
                return k;
            }

            (Label deger, Label alt) MetrikKutu(VisualElement ust, string baslik, System.Func<string> ipucu)
            {
                var e = U.Gecirgen(ust, "yz-kutu");
                U.Yazi(e, baslik, "yz-kutu-baslik");
                var d = U.Kalin(e, "", "yz-kutu-deger");
                var a = U.Yazi(e, "", "yz-kutu-alt");
                ui.Ipucu(e, ipucu);
                return (d, a);
            }

            static Label BilgiSatiri(VisualElement ust, string ad, out VisualElement satir)
            {
                satir = U.Gecirgen(ust, "kurye-bilgi");
                U.Yazi(satir, ad, "kurye-bilgi-ad");
                return U.Yazi(satir, "", "kurye-bilgi-deger");
            }

            void BeceriSatiri(VisualElement ust, Kart k, string ad, System.Func<Courier, int> yildiz, string aciklama)
            {
                var s = U.Gecirgen(ust, "beceri");
                var l = U.Yazi(s, ad, "beceri-ad");
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
                // Sayı kutuları
                var ks = KayitSistemi.I;
                bool askida = ks != null && ks.Askida(c);
                U.Metin(k.puan, c.puanAdet >= 5 ? $"{c.puanOrt:0.0}★" : "—");
                U.Renk(k.puan, c.puanAdet < 5 ? UITheme.Text : c.puanOrt >= 4.5f ? UITheme.Good : c.puanOrt >= 3.8f ? UITheme.Text : UITheme.Warn);
                string puanAlt = askida ? $"askıda · {c.askiBitis - GameClock.I.Day} gün"
                               : c.aktifGun > 0 && !c.ilkHaftaTamam ? "ilk haftası"
                               : c.puanAdet >= 5 ? "son ~30 teslimat" : "az teslimat";
                U.Metin(k.puanAlt, puanAlt);
                U.Renk(k.puanAlt, askida ? UITheme.Bad : U.Soluk);
                k.puanIpucu = "Müşterilerin bu kuryeye verdiği puanların ortalaması (son ~30 teslimat)." +
                              (c.askiSayisi > 0 && GameClock.I.Day - c.sonAskiGun <= 60 ? $" Son 60 günde {c.askiSayisi} kez askıya alındı." : "");

                U.Metin(k.hesap, Ft(c.balance));
                U.Metin(k.hesapAlt, c.pendingPayout > 0 ? $"yatacak {Ft(c.pendingPayout)}" : $"kesim {EconomyManager.I.NextCutText}");
                k.hesapIpucu = $"Hesabında biriken kazanç; kesim {EconomyManager.I.NextCutText}. Bugüne kadar yatan {Ft(c.receivedTotal)}." +
                               $"\nKazanç: {Ft(c.V.basePay)}/paket + {Ft(c.V.payPerKm)}/km + bonus" +
                               (c.tipsTotal > 0 ? $" · bahşiş toplamı {Ft(c.tipsTotal)}" : "");

                string capNote = "";
                if (c.morale >= 0.8f) capNote += ", moral yüksek +1";
                if (c.morale < 0.35f) capNote += ", moral düşük −1";
                if (c.fatigue >= 0.6f) capNote += ", yorgun −1";
                if (UpgradeManager.I.KargoBisikletiVar(c)) capNote += ", kargo bisikleti +2";
                int fark = c.EffectiveCapacity - c.V.capacity;
                U.Metin(k.kapasite, $"{c.EffectiveCapacity} paket");
                U.Metin(k.kapasiteAlt, fark == 0 ? $"araç {c.V.capacity}" : $"araç {c.V.capacity} ({(fark > 0 ? "+" : "−")}{Mathf.Abs(fark)})");
                U.Renk(k.kapasiteAlt, fark > 0 ? UITheme.Good : fark < 0 ? UITheme.Warn : U.Soluk);
                k.kapasiteIpucu = $"Aynı anda {c.EffectiveCapacity} paket (araç {c.V.capacity}{capNote}). Taşır: {GameData.CarryText(c.V)}." +
                                  $"\nTeklif seçiciliği: {c.PickinessText}";

                U.Metin(k.toplam, $"{c.deliveries}");
                U.Metin(k.toplamAlt, "teslimat");
                k.toplamIpucu = $"Bugüne kadar {c.deliveries} teslimat. Araçları: {c.VehiclesText}.";

                // Kısa bilgi
                U.Metin(k.meslek, $"{c.OccupationName} · bugün {c.OccupationStatus}" + (c.dayOff ? " · izin günü" : ""));
                k.meslekIpucu = c.occupation == Occupation.Ogrenci ? $"Ders programı: {Courier.ScheduleText(c.scheduleSeed, GameClock.I)}" : null;
                U.Metin(k.sigorta, c.insured ? $"var · {(c.insuredViaCompany ? EconomyManager.I.Plan.name : "dışarıdan")} · {Ft(c.CurrentPremium)}/hafta"
                                             : "yok (kazada masrafını kendi öder, daha uzun süre çalışamaz)");
                U.Renk(k.sigorta, c.insured ? UITheme.Text : UITheme.Warn);
                U.Metin(k.ekipman, $"{EquipmentText(c)} · çanta {c.bagUses}/{Courier.BagWornUses}" + (c.BagWorn ? " (yıprandı)" : ""));
                var camp = CampaignManager.I;
                var hedefler = new List<string>();
                if (camp.weeklyChallenge) hedefler.Add(c.weeklyRewardClaimed ? "haftalık tamam" : $"haftalık {c.weekDeliveries}/{camp.weeklyTarget}");
                if (camp.monthlyChallenge) hedefler.Add(c.monthlyRewardClaimed ? "aylık tamam" : $"aylık {c.monthDeliveries}/{camp.monthlyTarget}");
                U.Goster(k.hedefSatir, hedefler.Count > 0);
                U.Metin(k.hedef, string.Join(" · ", hedefler));
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
                    string ad = KuryeEgitim.Adlar[(int)t];
                    U.Metin(b, aracUygun ? $"{ad} · {Ft(bedel)}" : $"{ad} · motor/araba gerekir");
                    b.SetEnabled(aracUygun && serbest >= bedel);
                }
                k.cikarDugme.SetEnabled(c.OrderCount == 0);
                U.Metin(k.cikarDugme, c.OrderCount == 0 ? "Hesabı kapat" : "Hesabı kapat (paketi var)");
                k.askiDugme.SetEnabled(ks != null && c.OrderCount == 0 && !askida);
                U.Metin(k.askiDugme, askida ? "Askıda" : $"Askıya al ({KayitSistemi.AskiGun} gün)");
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
                    U.Metin(bosNot, cm.Couriers.Count == 0 ? "Henüz kurye yok: Kayıtlar sekmesinden kayıt sürecine bak."
                                                           : "Bu araçla çalışan kurye yok.");
                kartlar.Esitle(liste);
                acik.RemoveWhere(c => !cm.Couriers.Contains(c));
            }
        }
    }
}
