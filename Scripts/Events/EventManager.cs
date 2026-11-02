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

        public void Push(GameEvent e)
        {
            if (e.choices.Count == 0) e.Add("Tamam", null);
            queue.Enqueue(e);
        }

        public void Notify(string msg)
        {
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

        public void RollDeliveryIncident(Order o, Courier c)
        {
            float careRisk = 1f - c.care;
            float missing = 0.02f + careRisk * 0.10f;
            float damage = 0.015f + careRisk * 0.08f;
            // Yağmurluğu olmayan açık araçlı kuryenin paketi ıslanır
            if (WeatherSystem.I.IsRainy && c.V.exposed && !c.Has(EquipmentKind.Yagmurluk)) damage *= 2f;
            if (c.Has(EquipmentKind.TermalCanta)) damage *= 0.6f;

            float r = Random.value;
            if (r < missing) MissingItem(o, c);
            else if (r < missing + damage) Damaged(o, c);
        }

        void MissingItem(Order o, Courier c)
        {
            int value = GameData.Round10(o.basket * Random.Range(0.15f, 0.4f));
            if (SupportManager.I.TryHandleIncident(o, c, value, "eksik ürün")) return;
            Push(new GameEvent("Eksik ürün!",
                    $"{o.shop.Name} siparişinde (#{o.id}) müşteri bir ürünün eksik geldiğini söylüyor.\n" +
                    $"Kurye: {c.courierName} · Ürün değeri: {Ft(value)}")
                .Add($"Tam iade yap (−{Ft(value)})", () =>
                    EconomyManager.I.Spend(value, $"İade #{o.id}", ExpenseCategory.Iade))
                .Add($"Kupon ver (−{Ft(value / 2)}, itibar −0.05)", () =>
                {
                    EconomyManager.I.Spend(value / 2, $"Kupon #{o.id}", ExpenseCategory.Iade);
                    CompanyProfile.AddReputation(-0.05f);
                })
                .Add("İade yap, yarısını kuryeden kes (moral −)", () =>
                {
                    EconomyManager.I.Spend(value, $"İade #{o.id}", ExpenseCategory.Iade);
                    EconomyManager.I.AddIncome(value / 2, $"Kurye kesintisi: {c.courierName}");
                    if (c != null) c.morale = Mathf.Max(0f, c.morale - 0.2f);
                })
                .Add("Reddet (itibar −0.2)", () => CompanyProfile.AddReputation(-0.2f)));
        }

        void Damaged(Order o, Courier c)
        {
            int value = GameData.Round10(o.basket * Random.Range(0.5f, 1f));
            if (SupportManager.I.TryHandleIncident(o, c, value, "hasarlı sipariş")) return;
            string reason = WeatherSystem.I.IsRainy ? "Yağmurda çanta ıslanmış, " : "";
            Push(new GameEvent("Hasarlı sipariş!",
                    $"{reason}{o.shop.Name} siparişi (#{o.id}) hasarlı teslim edildi (dökülmüş içecek / ezilmiş paket).\n" +
                    $"Kurye: {c.courierName} · Zarar: {Ft(value)}")
                .Add($"Tazmin et (−{Ft(value)})", () =>
                    EconomyManager.I.Spend(value, $"Tazminat #{o.id}", ExpenseCategory.Iade))
                .Add($"%50 indirim kuponu (−{Ft(value / 2)}, itibar −0.05)", () =>
                {
                    EconomyManager.I.Spend(value / 2, $"Kupon #{o.id}", ExpenseCategory.Iade);
                    CompanyProfile.AddReputation(-0.05f);
                })
                .Add("Tazmin et, yarısını kuryeden kes (moral −)", () =>
                {
                    EconomyManager.I.Spend(value, $"Tazminat #{o.id}", ExpenseCategory.Iade);
                    EconomyManager.I.AddIncome(value / 2, $"Kurye kesintisi: {c.courierName}");
                    if (c != null) c.morale = Mathf.Max(0f, c.morale - 0.2f);
                })
                .Add("Reddet (itibar −0.25)", () => CompanyProfile.AddReputation(-0.25f)));
        }

        // ---------- Günlük rastgele olaylar ----------

        void OnNewDay()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;

            var map = CityMap.I;
            foreach (var s in map.Shops) s.prepMultiplier = 1f;

            var contracted = new List<Shop>();
            foreach (var s in map.Shops)
                if (s.contracted && map.IsUnlocked(s.tile.x)) contracted.Add(s);

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
