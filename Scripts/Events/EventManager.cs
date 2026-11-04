using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    public class EventChoice
    {
        public string label;
        public System.Action action;
    }

    /// <summary>Oyuncuya karar sunan olay kartı.</summary>
    public class GameEvent
    {
        public string title;
        public string body;
        public readonly List<EventChoice> choices = new List<EventChoice>();

        public GameEvent(string title, string body)
        {
            this.title = title;
            this.body = body;
        }

        public GameEvent Add(string label, System.Action action)
        {
            choices.Add(new EventChoice { label = label, action = action });
            return this;
        }
    }

    /// <summary>Olay kartları kuyruğu, haber akışı ve rastgele olaylar.</summary>
    public class EventManager : MonoBehaviour
    {
        public static EventManager I { get; private set; }

        public GameEvent Current { get; private set; }
        public readonly List<string> Feed = new List<string>();
        readonly Queue<GameEvent> queue = new Queue<GameEvent>();

        static string Ft(long v) => GameData.Ft(v);

        void Awake() { I = this; }

        void Start() { GameClock.I.OnNewDay += OnNewDay; }

        void Update()
        {
            if (Current == null && queue.Count > 0) Current = queue.Dequeue();
            if (GameClock.I != null) GameClock.I.EventPause = Current != null;
        }

        /// <summary>Açık ya da sırada bekleyen olay kartı yok (kayıt sistemi bekleyen kararları kaybetmemek için bakar).</summary>
        public bool Idle => Current == null && queue.Count == 0;

        public void Push(GameEvent e)
        {
            if (e.choices.Count == 0) e.Add("Tamam", null);
            queue.Enqueue(e);
        }

        /// <summary>Her bildirimde çağrılır (arayüzdeki bildirim kartları için).</summary>
        public static System.Action<string> Notified;

        public void Notify(string msg)
        {
            Notified?.Invoke(msg);
            var c = GameClock.I;
            Feed.Insert(0, c != null ? $"[{c.Hour:00}:{c.Minute:00}] {msg}" : msg);
            if (Feed.Count > 40) Feed.RemoveAt(Feed.Count - 1);
        }

        public void Choose(int index)
        {
            if (Current == null || index < 0 || index >= Current.choices.Count) return;
            var choice = Current.choices[index];
            Current = null;
            choice.action?.Invoke();
        }

        // ---------- Teslimat sonrası olaylar ----------

        /// <summary>
        /// Teslimattan sonra küçük bir ihtimalle sorun çıkar (gerçek hayattaki gibi nadir):
        ///  • Dükkanda eksik ürün (en sık): ürün dükkanda yokmuş / paketlenmemiş. Dükkan müşteriye bildirir ve
        ///    ücretini kendisi iade eder: şirkete ve kuryeye maliyeti yok, sadece bilgi. Markette (çok ürün) daha sık.
        ///  • Kurye kaynaklı sorun: içecek döküldü, paket ezildi / ıslandı ya da kurye bir poşeti dükkanda unuttu.
        ///    Dikkatsiz kuryede daha sık. Müşteri isterse (yaklaşık %60) desteğe ulaşır; iade tutarı kuryenin
        ///    hesabından düşülür (Wolt kuralı). Desteğe ulaşmayan müşteri sadece düşük puan verir.
        /// </summary>
        public void RollDeliveryIncident(Order o, Courier c)
        {
            // Dükkan kaynaklı eksik ürün: market ~%1,5, restoran/kafe ~%0,7
            float shopMissing = (o.shop.def.isMarket ? 0.015f : 0.007f) * UpgradeManager.I.ShopMissingMultiplier;
            // Kurye kaynaklı: dikkatli kuryede ~%0,5, dikkatsizde ~%1,5
            float careRisk = 1f - c.care;
            float courierFault = 0.003f + careRisk * 0.025f;
            // Yağmurluğu olmayan açık araçlı kuryenin paketi ıslanır; termal çanta korur
            if (WeatherSystem.I.IsRainy && c.V.exposed && !c.Has(EquipmentKind.Yagmurluk)) courierFault *= 1.6f;
            if (c.Has(EquipmentKind.TermalCanta)) courierFault *= 0.6f;

            float r = Random.value;
            if (r < shopMissing) ShopMissingItem(o);
            else if (r < shopMissing + courierFault) CourierFault(o, c);
        }

        /// <summary>Dükkanda eksik ürün: dükkan müşteriye bildirir ve ürünün ücretini iade eder. Kimse bir şey ödemez.</summary>
        void ShopMissingItem(Order o)
        {
            string item = o.items != null && o.items.Count > 0 ? o.items[Random.Range(0, o.items.Count)].name : "bir ürün";
            int value = o.items != null && o.items.Count > 0 ? GameData.Round10(o.items.Find(i => i.name == item).unitPrice) : GameData.Round10(o.basket * 0.2f);
            Notify($"#{o.id}: {o.shop.Name}'da \"{item}\" kalmamış. {o.shop.Name} müşteriye bildirdi ve ücretini ({Ft(value)}) kendisi iade etti; " +
                   "sana ve kuryeye maliyeti yok.");
        }

        /// <summary>Kurye kaynaklı sorun: müşteri desteğe ulaşırsa tutar kuryenin hesabından düşülür.</summary>
        void CourierFault(Order o, Courier c)
        {
            bool forgot = Random.value < 0.3f;
            bool wet = WeatherSystem.I.IsRainy && c.V.exposed && !c.Has(EquipmentKind.Yagmurluk);
            bool spill = !forgot && !wet && Random.value < 0.55f;
            string kind = forgot ? "forgot" : spill ? "spill" : "crush";
            // İade sadece etkilenen ürünün fiyatı (siparişin tamamı değil)
            int value = GameData.Round10(Mathf.Max(100, Menus.DamagedValue(o.items, kind, out string item)));
            string what = forgot ? $"\"{item}\" poşetini dükkanda unuttu"
                        : spill ? $"\"{item}\" döküldü"
                        : wet ? $"\"{item}\" yağmurda ıslandı"
                        : (o.shop.def.isMarket ? $"poşet yırtıldı, \"{item}\" ezildi / kırıldı" : $"\"{item}\" ezildi");

            // Müşterilerin çoğu desteğe yazmaz, sadece düşük puan verir
            if (Random.value > 0.6f)
            {
                o.rating = Mathf.Max(1f, o.rating - 1f);
                CompanyProfile.AddReputation(-0.01f);
                Notify($"#{o.id}: {c.ShortName} teslimatında {what}. Müşteri şikayet etmedi ama düşük puan verdi ({o.rating:0.0}★).");
                return;
            }

            if (RivalCompany.I != null) RivalCompany.I.OnIncident(o);

            // Destek ekibi varsa kuralı uygular: iade, kuryenin hesabından düşülür
            var agent = SupportManager.I.OnDutyAgent();
            if (agent != null)
            {
                agent.handledTotal++;
                SupportManager.I.handledThisMonth++;
                long taken = ChargeCourier(o, c, value);
                Notify($"#{o.id}: {c.ShortName} teslimatında {what}. Müşteri desteğe yazdı; destek ürünün ücretini ({Ft(value)}) iade etti, " +
                       (taken >= value ? $"tutar {c.ShortName}'ın hesabından düşüldü." : $"{Ft(taken)}'ı {c.ShortName}'ın hesabından düşüldü, kalanı şirket ödedi."));
                return;
            }

            Push(new GameEvent(forgot ? "Eksik teslimat (kurye)" : "Hasarlı sipariş",
                    $"#{o.id} · {o.shop.Name}: kurye {c.courierName} teslimatında {what}. Müşteri desteğe yazdı ve bu ürünün ücretini ({Ft(value)}) istiyor.\n" +
                    "Kural: kurye kaynaklı sorunlarda iade kuryenin hesabından düşülür. İstersen şirket olarak üstlenebilirsin.")
                .Add($"İade et, kuryeden düş (kural, moral biraz düşer)", () => ChargeCourier(o, c, value))
                .Add($"İade et, şirket karşılasın (−{Ft(value)}, kuryenin morali artar)", () =>
                {
                    EconomyManager.I.Spend(value, $"İade #{o.id} (şirket üstlendi)", ExpenseCategory.Iade);
                    c.morale = Mathf.Min(1f, c.morale + 0.05f);
                })
                .Add($"%50 kupon ver, kimseden düşme (−{Ft(value / 2)}, itibar −0.03)", () =>
                {
                    EconomyManager.I.Spend(value / 2, $"Kupon #{o.id}", ExpenseCategory.Iade);
                    CompanyProfile.AddReputation(-0.03f);
                })
                .Add("Reddet (itibar −0.15)", () => CompanyProfile.AddReputation(-0.15f)));
        }

        /// <summary>Müşteriye iade yapılır, tutar kuryenin hesabındaki kazançtan düşülür (yetmezse kalanı şirket öder).</summary>
        long ChargeCourier(Order o, Courier c, int value)
        {
            EconomyManager.I.Spend(value, $"İade #{o.id}", ExpenseCategory.Iade);
            long taken = System.Math.Min(c.balance, (long)value);
            c.balance -= taken;   // şirketin kuryeye borcu azalır: iade fiilen kuryeden çıkar
            c.morale = Mathf.Max(0f, c.morale - 0.05f);
            c.AddInbox($"#{o.id} iadesi: {Ft(taken)} hesabından düşüldü ({o.shop.Name} siparişi).");
            return taken;
        }

        /// <summary>
        /// Kirli çanta (hijyen %45'in altı): yemek kokusu fark edilir. Müşteri çoğunlukla sadece düşük puan verir;
        /// nadiren şikayet eder (çok kirli çantada teslimat başına en fazla ~%4, paketli market ürününde yarısı).
        /// Şikayet platform kuralıyla kendiliğinden işlenir: kurye uyarılır ve ilk boşlukta çantasını temizler.
        /// Aynı kurye bir haftada 3. şikayeti alırsa karar şirkete gelir.
        /// </summary>
        public void RollHygieneComplaint(Order o, Courier c)
        {
            if (c.hygiene >= Courier.HygieneLimit) return;
            float chance = (Courier.HygieneLimit - c.hygiene) * 0.1f;
            if (o.shop.def.isMarket || !o.shop.def.hotFood) chance *= 0.5f;
            if (Random.value >= chance) return;

            c.hygieneComplaintsWeek++;
            o.rating = Mathf.Max(1f, o.rating - 0.8f);
            CompanyProfile.AddReputation(-0.01f);
            CourierManager.I.RequestCleaning(c, false);

            if (c.hygieneComplaintsWeek < 3)
            {
                Notify($"#{o.id}: müşteri {c.ShortName}'ın çantasının koktuğunu bildirdi (hijyen %{c.hygiene * 100f:0}). " +
                       $"{c.ShortName} uyarıldı, paketi bitince çantasını temizleyecek.");
                return;
            }

            c.hygieneComplaintsWeek = 0;
            var e = GameData.Equip(EquipmentKind.TermalCanta);
            string worn = c.BagWorn ? " Çantası yıpranmış: temizlense de tam temiz olmuyor." : "";
            var ev = new GameEvent("Tekrarlayan hijyen şikayeti",
                    $"{c.courierName} bu hafta 3. kez hijyen şikayeti aldı (çanta hijyeni %{c.hygiene * 100f:0}, titizlik {c.tidiness:0.00}).{worn}\n" +
                    "Çanta temizliği kuryenin sorumluluğu; şirket olarak nasıl davranacağına sen karar ver.")
                .Add("Ciddi uyarı yap (moral biraz düşer)", () => { c.morale = Mathf.Max(0f, c.morale - 0.05f); c.AddInbox("Son uyarı: çanta temizliği şikayetleri tekrarlarsa hesabın dondurulabilir."); });
            if (c.BagWorn)
                ev.Add($"Yeni termal çanta ver (−{Ft(e.price)})", () => CourierManager.I.ReplaceBag(c));
            ev.Add("Hesabını 1 gün dondur (bugün çalışamaz, moral düşer)", () => CourierManager.I.SuspendForDay(c))
              .Add("Görmezden gel (itibar −0.05)", () => CompanyProfile.AddReputation(-0.05f));
            Push(ev);
        }

        // ---------- Günlük rastgele olaylar ----------

        void OnNewDay()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;

            var map = CityMap.I;
            foreach (var s in map.Shops) s.prepMultiplier = 1f;

            var contracted = new List<Shop>();
            foreach (var s in map.Shops)
                if (s.contracted && map.IsUnlocked(s.tile)) contracted.Add(s);

            if (contracted.Count == 0 || Random.value > 0.45f) return;
            var shop = contracted[Random.Range(0, contracted.Count)];

            switch (Random.Range(0, 3))
            {
                case 0:
                    shop.prepMultiplier = 2f;
                    Push(new GameEvent("Dükkan yavaş",
                        $"{shop.Name} bugün personel eksikliği yaşıyor. Siparişler 2 kat geç hazırlanacak; kuryeler dükkanda bekleyebilir."));
                    break;

                case 1:
                    if (CompanyProfile.Reputation >= 3.8f)
                    {
                        CompanyProfile.AddReputation(0.2f);
                        Push(new GameEvent("Viral övgü!",
                            $"Bir müşteri {CompanyProfile.Name} kuryesinin hızını sosyal medyada övdü. İtibar +0.2"));
                    }
                    else if (CompanyProfile.Reputation < 2.8f)
                    {
                        CompanyProfile.AddReputation(-0.15f);
                        Push(new GameEvent("Kötü yorum viral oldu",
                            "Geç gelen bir sipariş hakkındaki yorum çok paylaşıldı. İtibar −0.15. Daha çok kurye almayı düşün."));
                    }
                    break;

                case 2:
                    const int adCost = 30000;
                    Push(new GameEvent("Komisyon teklifi",
                            $"{shop.Name}, ortak reklam kampanyası karşılığında komisyonu %2 artırmayı teklif ediyor.")
                        .Add($"Kabul et (−{Ft(adCost)}, komisyon +%2)", () =>
                        {
                            if (EconomyManager.I.TrySpend(adCost, $"Reklam: {shop.Name}", ExpenseCategory.Reklam))
                                shop.commissionBonus += 0.02f;
                        })
                        .Add("Reddet", null));
                    break;
            }
        }
    }
}
