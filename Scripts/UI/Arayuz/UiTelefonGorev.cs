using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Kurye telefonunda aktif görev ekranı (UI Toolkit; eski IMGUI PhoneTask.cs yerine), gerçek kurye uygulaması gibi:
    ///  • Teslim alma: "Teslim alınacak yer", dükkan adı ve adresi; "Siparişi al" kartları (#numara, müşterinin kısa adı,
    ///    sipariş içeriği). Kartın okuna basınca "Teslimatta sorun mu var?": Gecikme bildir · Destek.
    ///    Altta "Bu teslimatı iptal et" (onaylı) ve "Destekle iletişime geç".
    ///  • Teslimat: "Teslim edilecek kişi", müşteri ve adres; Bilgi sekmesi (kapı kodu, zil/interkom, kat, daire, notlar ve
    ///    Çevir, nakit, temassız teslimat uyarısı), Ürünler sekmesi (ürünler, teslimat zaman çizelgesi, çevrimdışı saatini
    ///    ayarla, önceki görev).
    ///  • Altta sabit durum hapı (Dükkana gidiyor / Siparişleri al / Sipariş teslim ediliyor): UiTelefon.cs çizer, yazısı burada.
    /// Düğmeler gerçekten çalışır: gecikme bildirmek söz verilen süreyi uzatır, destek dükkanı hızlandırır ya da adres
    /// sorununu hemen çözmeye çalışır, iptal siparişi başka kuryeye verir, çevrimdışı saati kuryeyi o saatte eve gönderir.
    /// </summary>
    public partial class GameUI
    {
        int phoneTaskTab;                                   // 0 Bilgi, 1 Ürünler
        readonly HashSet<int> phoneExpanded = new HashSet<int>();
        readonly HashSet<int> phoneTranslated = new HashSet<int>();
        bool phoneOfflineEdit;
        int phoneConfirmCancel = -1;

        static readonly Color PWarnBg = new Color(0.22f, 0.10f, 0.03f);
        static readonly Color PWarnText = new Color(1f, 0.65f, 0.35f);
        static readonly Color PCancelBg = new Color(0.32f, 0.04f, 0.04f);
        static readonly Color PLate = new Color(1f, 0.74f, 0.48f);
        static readonly Color PLateText = new Color(0.08f, 0.06f, 0.04f);
        static readonly string[] GorevSekmeleri = { "Bilgi", "Ürünler" };

        static string Hm(float totalMinutes)
        {
            int m = Mathf.Max(0, Mathf.RoundToInt(totalMinutes));
            return $"{(m / 60) % 24:00}:{m % 60:00}";
        }

        static string ShopAddress(Shop s) => $"{Addresses.StreetOf(s.door)} {Addresses.Number(s.tile, s.door)}, 4400 Nyíregyháza";
        static string CustomerAddress(Order o) => $"{o.street} {o.number}" + (o.blocks > 1 ? $" {o.block} Blok" : "") + ", 4400 Nyíregyháza";

        static bool InPickupPhase(Courier c) =>
            c.State == CourierState.DukkanaGidiyor || c.State == CourierState.Bekliyor;

        // ====================================================================== görev ekranı

        void HomeTaskApp(Yazici y, Courier c)
        {
            var cur = c.CurrentOrder;
            if (cur == null)
            {
                // Sipariş arası (bir sonrakine geçiyor)
                y.Yazi("Sıradaki görev hazırlanıyor", "tel-baslik");
                float now = GameClock.I.TotalMinutes;
                foreach (var o in c.CarryingList) OrderCard(y, o, c, true, now);
                foreach (var o in c.ToPickList) OrderCard(y, o, c, false, now);
                return;
            }
            if (InPickupPhase(c)) PickupView(y, c, cur);
            else DropoffView(y, c, cur);
        }

        // ---------------------------------------------------------------------- teslim alma

        void PickupView(Yazici y, Courier c, Order cur)
        {
            y.Yazi("Teslim alınacak yer", "tel-kalin14");
            y.Yazi(cur.shop.Name, "tel-buyuk", PAccent);
            y.Yazi(ShopAddress(cur.shop));
            TelCizgi(y);

            y.Yazi("Siparişi al", "tel-baslik");
            y.Bosluk(4);
            // Bu dükkandan alınacak bütün siparişler (toplu siparişte birden fazla)
            foreach (var o in c.ToPickList)
                if (o.shop == cur.shop) CollectCard(y, c, o);

            y.Bosluk(6);
            TelCizgi(y);
            y.Yazi("Teslimatta sorun mu var?", "tel-baslik");
            y.Bosluk(6);
            var siparis = cur;
            if (phoneConfirmCancel == cur.id)
            {
                y.Yazi($"#{cur.id} iptal edilsin mi? Sipariş başka kuryeye verilir; müşteri biraz daha bekler.", "tel-soluk");
                y.SatirBasla();
                TelDugme(y, "Evet, iptal et", () => { c.CancelTask(siparis); phoneConfirmCancel = -1; }, PCancelBg, PRed, true, "genis");
                TelDugme(y, "Vazgeç", () => phoneConfirmCancel = -1, PCard, Color.white, true, "genis");
                y.SatirBitir();
            }
            else TelDugme(y, "Bu teslimatı iptal et", () => phoneConfirmCancel = siparis.id, PCancelBg, PRed, true, "tel-dg-buyuk");
            y.Bosluk(4);
            TelBaglanti(y, "Destekle iletişime geç", () => SupportForPickup(c, siparis));
        }

        void CollectCard(Yazici y, Courier c, Order o)
        {
            y.KartBasla("tel-kart");
            y.Yazi($"#{o.id}", "tel-dev24");
            y.Yazi(o.ShortCustomerName);
            TelCizgi(y);
            y.Yazi("SİPARİŞ DETAYI", "tel-kucuk");
            foreach (var it in o.items) y.Yazi($"{it.qty} x {it.name}", "tel-soluk");
            if (o.markedLate) y.Yazi("Gecikme bildirildi: müşteriye haber verildi (+10 dk).", "tel-soluk", PLate);
            if (o.shopQueue && c.State == CourierState.Bekliyor && c.CurrentOrder == o)
                y.Yazi($"Dükkanda sıra var · hazır olmasına ~{Mathf.Max(1f, o.readyAt - GameClock.I.TotalMinutes):0} dk", "tel-soluk", PLate);

            bool open = phoneExpanded.Contains(o.id);
            var ord = o;
            if (open)
            {
                TelCizgi(y);
                y.Yazi("Teslimatta sorun mu var?", "tel-kalin14");
                y.Bosluk(4);
                y.SatirBasla();
                TelDugme(y, o.markedLate ? "Bildirildi" : "Gecikme bildir", () => MarkLate(c, ord), PLate, PLateText, !o.markedLate, "genis tel-dg-orta");
                TelBaglanti(y, "Destek", () => SupportForPickup(c, ord), "tel-baglanti-satir");
                y.SatirBitir();
            }
            TelCizgi(y);
            string status = c.CurrentOrder == o && c.State == CourierState.Bekliyor ? "Hazırlanıyor" : "Teslim alınacak";
            var alt = y.Ozel("topla-alt", () => new TelToplaAlt());
            int id = o.id;
            alt.tik = () => { if (!phoneExpanded.Remove(id)) phoneExpanded.Add(id); telKirli = true; };
            alt.Ayarla(status, open);
            y.KartBitir();
        }

        // ---------------------------------------------------------------------- teslimat

        void DropoffView(Yazici y, Courier c, Order cur)
        {
            y.Yazi("Teslim edilecek kişi", "tel-kalin14");
            y.Yazi(cur.ShortCustomerName, "tel-buyuk", PGreen);
            y.Yazi(CustomerAddress(cur));
            y.Bosluk(8);

            // Bilgi | Ürünler
            var seg = y.Ozel("segment", () => new TelSegment());
            seg.sec = i => { phoneTaskTab = i; telKirli = true; };
            seg.Ayarla(GorevSekmeleri, phoneTaskTab);
            y.Bosluk(8);

            if (phoneTaskTab == 0) DropoffInfo(y, c, cur);
            else DropoffItems(y, c, cur);
        }

        void DropoffInfo(Yazici y, Courier c, Order o)
        {
            // Adres sorunu: hemen destek
            if (c.State == CourierState.AdresSorunu)
            {
                WarnBox(y, o.addressIssue == AddressIssue.YanlisAdres ? "ADRES YANLIŞ İŞARETLENMİŞ" : "MÜŞTERİ KAPIYI AÇMIYOR",
                        o.addressIssue == AddressIssue.YanlisAdres ? "Kurye binayı bulamıyor. Destek müşteriyi arayıp doğru adresi öğrenebilir."
                                                                   : "Kurye kapıda bekliyor. Destek müşteriyi arayabilir.");
                var ord = o;
                TelDugme(y, "Desteği hemen ara", () =>
                {
                    if (c.ContactSupportNow()) EventManager.I.Notify($"{c.ShortName} #{ord.id} için desteği aradı.");
                }, PLate, PLateText, true, "tel-dg-orta");
                y.Bosluk(6);
            }

            y.Yazi("Adres detayları", "tel-baslik");
            y.Bosluk(2);
            if (o.doorCode != null)
                DetailRow(y, "Bina kapı kodu", o.codeMissing ? "Müşteri yazmamış · kapıda arayacak (+1,5 dk)" : o.doorCode, o.codeMissing ? PLate : (Color?)null);
            else DetailRow(y, "Bina kapı kodu", "Yok (müstakil / küçük bina)");
            DetailRow(y, "Zil / İnterkom", o.intercom);
            y.KutularBasla();
            y.Kutu("Kat", Addresses.FloorText(o.floor) + (o.blocks > 1 ? $" · {o.block} Blok" : ""));
            y.Kutu("Daire", o.flat > 0 ? o.flat.ToString() : "—");
            y.KutularBitir();
            TelCizgi(y);
            y.Yazi(o.BuildingText, "tel-soluk", o.UsesStairs && o.floor >= 4 ? PLate : (Color?)null);

            if (!string.IsNullOrEmpty(o.customerNote))
            {
                TelCizgi(y);
                y.Yazi("Notlar", "tel-baslik");
                bool tr = phoneTranslated.Contains(o.id);
                y.Yazi(tr && o.noteTranslation != "" ? o.noteTranslation : o.customerNote);
                if (o.noteTranslation != "")
                {
                    int id = o.id;
                    TelBaglanti(y, tr ? "Orijinali göster" : "Çevir", () => { if (!phoneTranslated.Remove(id)) phoneTranslated.Add(id); }, "tel-baglanti-sol");
                }
            }

            if (o.cashPay)
            {
                TelCizgi(y);
                y.Yazi("Ödeme", "tel-baslik");
                DetailRow(y, "Nakit tahsil edilecek", Ft(o.CashAmount), PLate);
                DetailRow(y, "Müşteri verecek / para üstü", $"{Ft(o.cashGiven)} / {Ft(o.CashChange)}");
            }

            y.Bosluk(6);
            y.Yazi("Sipariş detayı", "tel-baslik");
            if (o.noContact) WarnBox(y, "TEMASSIZ TESLİMAT", "Lütfen siparişi müşterinin kapısına bırak. Müşteri evde olmasa da teslim edilir.");
            float left = o.deadlineAt - GameClock.I.TotalMinutes;
            DetailRow(y, "Söz verilen süre", left >= 0 ? $"{left:0} dk kaldı · {Hm(o.deadlineAt)}" : $"{-left:0} dk gecikmede", left >= 0 ? (Color?)null : PRed);
            DetailRow(y, "Kazanç", Ft(o.courierPay + o.boostPay));
            if (!o.markedLate && left < 8f)
            {
                var ord = o;
                TelDugme(y, "Gecikme bildir (+10 dk)", () => MarkLate(c, ord), PLate, PLateText, true, "tel-dg-orta");
            }

            // Çantadaki diğer siparişler
            int others = 0;
            foreach (var x in c.CarryingList) if (x != o) others++;
            others += c.ToPickList.Count;
            if (others > 0)
            {
                TelCizgi(y);
                y.Yazi("Sıradaki siparişler", "tel-baslik");
                float now = GameClock.I.TotalMinutes;
                foreach (var x in c.CarryingList) if (x != o) OrderCard(y, x, c, true, now);
                foreach (var x in c.ToPickList) OrderCard(y, x, c, false, now);
            }
        }

        void DropoffItems(Yazici y, Courier c, Order o)
        {
            y.Yazi("Ürünler", "tel-baslik");
            y.Bosluk(2);
            foreach (var it in o.items) y.Yazi($"{it.qty} ×  {it.name}", "tel-kalin14");
            y.Yazi($"{o.ItemCount} ürün · {GameData.KgText(o.weightKg)} · sepet {Ft(o.basket)}", "tel-soluk");
            TelCizgi(y);

            // Zaman çizelgesi
            y.Yazi("Teslimat zaman çizelgesi", "tel-baslik");
            y.Bosluk(4);
            float now = GameClock.I.TotalMinutes;
            bool picked = o.pickedAt > 0f;
            TimelineRow(y, picked ? Hm(o.pickedAt) : Hm(Mathf.Max(now, o.readyAt)), picked, PAccent, $"{o.shop.Name}'dan teslim alma");
            float eta = c.CurrentOrder == o ? now + EtaMinutes(c) + (c.State == CourierState.KataCikiyor ? c.DoorMinutesLeft : o.DoorMinutes(c.stamina, true)) : o.deadlineAt;
            TimelineRow(y, Hm(eta), false, PGreen, $"{o.ShortCustomerName}'ya teslimat");
            y.Bosluk(8);

            // Çevrimdışı saati
            if (c.plannedOfflineAt > 0f)
                y.Yazi($"Planlanan çevrimdışı saati: {Hm(c.plannedOfflineAt)}", "tel-soluk", PLate);
            if (!phoneOfflineEdit)
                TelDugme(y, "Çevrimdışı saatini ayarla", () => phoneOfflineEdit = true, PCancelBg, PRed, true, "tel-dg-buyuk");
            else
            {
                y.Yazi("Ne zaman çevrimdışı olsun? Elindeki siparişleri her zaman bitirir.", "tel-soluk");
                y.SatirBasla();
                TelDugme(y, "Bu görevden sonra", () =>
                {
                    c.plannedOfflineAt = -1f; c.EndShiftEarly("Uygulamadan: bu görevden sonra çevrimdışı"); phoneOfflineEdit = false;
                    EventManager.I.Notify($"{c.ShortName} bu görevden sonra çevrimdışı olacak.");
                }, PCard, Color.white, true, "genis tel-dg-kucuk");
                TelDugme(y, "1 saat sonra", () =>
                {
                    c.plannedOfflineAt = GameClock.I.TotalMinutes + 60f; phoneOfflineEdit = false;
                    EventManager.I.Notify($"{c.ShortName} {Hm(c.plannedOfflineAt)}'da çevrimdışı olacak.");
                }, PCard, Color.white, true, "genis tel-dg-kucuk");
                y.SatirBitir();
                y.SatirBasla();
                TelDugme(y, "2 saat sonra", () =>
                {
                    c.plannedOfflineAt = GameClock.I.TotalMinutes + 120f; phoneOfflineEdit = false;
                    EventManager.I.Notify($"{c.ShortName} {Hm(c.plannedOfflineAt)}'da çevrimdışı olacak.");
                }, PCard, Color.white, true, "genis tel-dg-kucuk");
                TelDugme(y, c.plannedOfflineAt > 0f ? "Planı kaldır" : "Vazgeç", () => { c.plannedOfflineAt = -1f; phoneOfflineEdit = false; },
                         PCard, PDim, true, "genis tel-dg-kucuk");
                y.SatirBitir();
            }
            TelCizgi(y);

            y.Yazi("Hızlı bağlantılar", "tel-baslik");
            var r = y.Ozel("menu-satir", () => new TelMenuSatir());
            r.tik = () => TelefonSayfa(PhoneScreen.PrevTask);
            r.Ayarla(null, "Önceki görev", null, Icons.Kind.Clock);
        }

        static void TimelineRow(Yazici y, string time, bool done, Color c, string text)
        {
            y.Ozel("zaman", () => new TelZaman()).Ayarla(time, done, c, text);
        }

        static void DetailRow(Yazici y, string k, string v, Color? vc = null, bool line = true)
        {
            y.Yazi(k, "tel-soluk");
            y.Yazi(v, "tel-deger", vc);
            if (line) TelCizgi(y);
        }

        static void WarnBox(Yazici y, string head, string text)
        {
            y.Ozel("uyari", () => new TelUyari()).Ayarla(head, text);
        }

        /// <summary>Alt paneldeki sabit durum hapının yazısı.</summary>
        static string TaskStatusText(Courier c)
        {
            var cur = c.CurrentOrder;
            if (cur == null) return "Sıradaki görev";
            if (c.State == CourierState.DukkanaGidiyor) return $"Dükkana gidiyor · {EtaMinutes(c):0} dk";
            if (c.State == CourierState.Bekliyor) return "Siparişleri al · hazırlanıyor";
            if (c.State == CourierState.KataCikiyor) return $"Sipariş teslim ediliyor · {c.DoorMinutesLeft:0} dk";
            if (c.State == CourierState.AdresSorunu) return "Adreste sorun var";
            return $"Teslimata {EtaMinutes(c):0} dk";
        }

        // ====================================================================== işlemler

        void MarkLate(Courier c, Order o)
        {
            if (o == null || o.markedLate) return;
            o.markedLate = true;
            o.deadlineAt += 10f;
            EventManager.I.Notify($"{c.ShortName} #{o.id} için gecikme bildirdi: müşteriye haber verildi, söz verilen süre +10 dk.");
        }

        /// <summary>Teslim almada destek: dükkanda bekliyorsa destek dükkanı arar (hazırlık hızlanır).</summary>
        void SupportForPickup(Courier c, Order o)
        {
            if (o == null) return;
            float now = GameClock.I.TotalMinutes;
            float wait = o.readyAt - now;
            if (wait <= 0.5f)
            {
                EventManager.I.Notify($"Destek: #{o.id} dükkanda hazır görünüyor, kurye vardığında teslim alabilir.");
                return;
            }
            if (o.shopPinged)
            {
                EventManager.I.Notify($"Destek #{o.id} için dükkanı zaten aradı. Hazır olmasına ~{wait:0} dk.");
                return;
            }
            o.shopPinged = true;
            var agent = SupportManager.I != null ? SupportManager.I.OnDutyAgent() : null;
            if (agent != null)
            {
                o.readyAt = now + wait * 0.5f;
                EventManager.I.Notify($"Destek ({agent.name.Split(' ')[0]}) {o.shop.Name}'ı aradı: #{o.id} öne alındı, ~{wait * 0.5f:0} dk sonra hazır.");
            }
            else if (Random.value < 0.35f)
            {
                o.readyAt = now + wait * 0.75f;
                EventManager.I.Notify($"Vardiyada destek yok; {c.ShortName} dükkanla kendisi konuştu, #{o.id} biraz hızlandı (~{wait * 0.75f:0} dk).");
            }
            else EventManager.I.Notify($"Vardiyada destek personeli yok. {c.ShortName} beklemeye devam ediyor (~{wait:0} dk). Destek'ten personel alabilirsin.");
        }

        // ====================================================================== önceki görev

        void PagePrevTask(Yazici y, Courier c)
        {
            var o = c.lastDelivered;
            if (o == null)
            {
                y.Yazi("Bugün henüz tamamlanan görev yok.", "tel-soluk");
                return;
            }
            y.Yazi($"#{o.id} · {o.shop.Name}", "tel-baslik");
            y.Yazi($"{o.ShortCustomerName} · {CustomerAddress(o)}", "tel-soluk");
            TelCizgi(y);
            foreach (var it in o.items) y.Yazi($"{it.qty} × {it.name}");
            TelCizgi(y);
            TimelineRow(y, o.pickedAt > 0f ? Hm(o.pickedAt) : "—", true, PAccent, $"{o.shop.Name}'dan teslim alındı");
            TimelineRow(y, Hm(o.deliveredAt), true, PGreen, o.keptByCourier ? "Müşteriye ulaşılamadı" : $"{o.ShortCustomerName}'ya teslim edildi");
            TelCizgi(y);
            PRow(y, "Kazanç", Ft(o.courierPay + o.boostPay));
            if (o.tip > 0) PRow(y, "Bahşiş", Ft(o.tip), PGreen);
            PRow(y, "Mesafe", GameData.KmText(o.distance));
            PRow(y, "Müşteri puanı", o.rating > 0f ? $"{o.rating:0.0} ★" : "—");
            if (o.deliveredCold) y.Yazi("Yemek soğuk teslim edildi.", "tel-soluk", PRed);
            if (o.markedLate) y.Yazi("Gecikme bildirilmişti.", "tel-soluk", PLate);
        }
    }
}
