using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Rakip kurye platformu (tamamen yapay zekâ) ve dükkanlarla sözleşme ilişkileri.
    ///
    /// Rakip:
    ///  • Kendi itibarı, kurye sayısı, parası ve sözleşmeli dükkanları var; her hafta büyümeye çalışır.
    ///  • Seninle aynı dükkanla çalışıyorsa o dükkanın müşterileri ikiniz arasında paylaşılır
    ///    (itibar ve kurye sayısına göre). Münhasır dükkanlarda rakip yoktur.
    ///  • Sözleşme yenilemelerinde dükkanlarına teklif verir, ara sıra fiyat kırar, moralsiz kuryeleri ayartır.
    ///
    /// Dükkanlar:
    ///  • Memnuniyet (0–100): zamanında teslimat artırır; gecikme, soğuk yemek, iptal ve şikayetler düşürür.
    ///  • Sözleşme 6 ayda bir yenilenir. Memnuniyet düşükse rakip araya girer.
    ///  • Yeniden pazarlık (ayda bir): komisyonu +2 puan artırmayı dene; başarısızsa dükkan küser.
    ///  • Hacim: ayda 400+ sipariş veren dükkan bazen komisyon indirimi ister.
    ///  • Münhasırlık: rakip çıkar, siparişler artar (+%15), komisyon −3 puan.
    /// </summary>
    public class RivalCompany : MonoBehaviour
    {
        public static RivalCompany I { get; private set; }

        [Header("Rakip")]
        public string companyName = "Turbo Paket";
        public float reputation = 3.3f;
        public int couriers = 6;
        public long money = 1500000;
        public int deliveriesLastWeek;
        public int priceWarDays;

        [Header("Sözleşmeler")]
        public int contractDays = 182;          // 6 ay
        public int volumeTier = 400;            // aylık bu kadar siparişin üstünde dükkan indirim isteyebilir
        public float maxNegotiatedBonus = 0.06f;

        int deliveriesThisWeek;
        int myDeliveriesThisWeek;
        public int myDeliveriesLastWeek;

        void Awake() { I = this; }

        static int Day => GameClock.I.Day;
        static string Pct(float v) => $"%{v * 100f:0.#}";

        public void Init()
        {
            GameClock.I.OnNewDay += OnNewDay;
            GameClock.I.OnNewMonth += OnNewMonth;

            var shops = CityMap.I.Shops;
            foreach (var s in shops)
            {
                if (!s.contracted) continue;
                s.contractStartDay = Day;
                s.contractEndDay = Day + contractDays;
            }
            // Rakip başlangıçta senin sözleşmesiz olduğun birkaç dükkanla çalışır
            int n = 0;
            foreach (var s in shops)
                if (!s.contracted && n < 3 && Random.value < 0.6f) { s.rivalContracted = true; n++; }
        }

        // ================================================================ sipariş paylaşımı

        static float Score(float rep, int courierCount) =>
            rep * rep * (0.6f + 0.4f * Mathf.Min(1f, courierCount / 10f));

        /// <summary>Senin bu dükkandaki sipariş payın (talep çarpanı).</summary>
        public float ShareFor(Shop s)
        {
            if (s.exclusive) return 1.15f;              // dükkan sadece seni tanıtıyor
            if (!s.rivalContracted) return 1f;
            float mine = Score(CompanyProfile.Reputation, CourierManager.I.Couriers.Count);
            float theirs = Score(reputation, couriers);
            float share = Mathf.Clamp(mine / Mathf.Max(0.01f, mine + theirs), 0.3f, 0.8f);
            return Mathf.Min(1f, share * 1.25f);        // iki uygulamada da görünen dükkanın toplam müşterisi biraz daha fazla
        }

        public float PriceSensitivity => priceWarDays > 0 ? 0.9f : 1f;

        public int RivalShopCount
        {
            get { int n = 0; foreach (var s in CityMap.I.Shops) if (s.rivalContracted) n++; return n; }
        }

        /// <summary>Geçen haftanın teslimatlarına göre pazar payın.</summary>
        public float MarketShare => myDeliveriesLastWeek + deliveriesLastWeek == 0 ? 0.5f
            : myDeliveriesLastWeek / (float)(myDeliveriesLastWeek + deliveriesLastWeek);

        // ================================================================ memnuniyet

        public static void AddSatisfaction(Shop s, float delta)
        {
            if (s == null) return;
            s.satisfaction = Mathf.Clamp(s.satisfaction + delta, 0f, 100f);
        }

        /// <summary>Teslimat sonrası: zamanında artar, gecikme ve soğuk yemek düşürür.</summary>
        public void OnDelivered(Order o, float lateMinutes)
        {
            var s = o.shop;
            s.monthOrders++;
            myDeliveriesThisWeek++;
            if (lateMinutes > 0f) AddSatisfaction(s, -Mathf.Min(4f, 0.5f + lateMinutes / 5f));
            else AddSatisfaction(s, 0.4f);
            if (o.deliveredCold) AddSatisfaction(s, -2f);
        }

        public void OnCancelled(Order o) => AddSatisfaction(o.shop, -2.5f);
        public void OnIncident(Order o) => AddSatisfaction(o.shop, -3f);

        // ================================================================ oyuncunun hamleleri

        public float NegotiateChance(Shop s)
        {
            float rep = CompanyProfile.Reputation;
            return Mathf.Clamp(0.1f + (s.satisfaction - 50f) / 100f + (rep - 3f) * 0.1f + Mathf.Min(0.2f, s.lastMonthOrders / 500f), 0.05f, 0.85f);
        }

        public bool CanNegotiate(Shop s) => s.contracted && Day >= s.negotiateCooldownUntil && s.commissionBonus < maxNegotiatedBonus - 0.001f;

        /// <summary>Komisyonu 2 puan artırmayı dene. Başarısızsa dükkan küser; ayda bir denenebilir.</summary>
        public void Negotiate(Shop s)
        {
            if (!CanNegotiate(s)) return;
            s.negotiateCooldownUntil = Day + 30;
            if (Random.value < NegotiateChance(s))
            {
                s.commissionBonus += 0.02f;
                AddSatisfaction(s, -5f);
                EventManager.I.Push(new GameEvent("Pazarlık başarılı",
                    $"{s.Name} komisyonu 2 puan artırmayı kabul etti. Yeni oran: {Pct(s.Commission)}."));
            }
            else
            {
                AddSatisfaction(s, -12f);
                EventManager.I.Push(new GameEvent("Pazarlık başarısız",
                    $"{s.Name} teklifini reddetti ve biraz küstü (memnuniyet {s.satisfaction:0}). " +
                    "Bir ay sonra yeniden deneyebilirsin. Zamanında teslimat memnuniyeti artırır."));
            }
        }

        public float ExclusiveChance(Shop s) => Mathf.Clamp((s.satisfaction - 40f) / 50f, 0f, 0.9f);

        public bool CanOfferExclusive(Shop s) => s.contracted && !s.exclusive && Day >= s.negotiateCooldownUntil;

        /// <summary>Münhasırlık: rakip çıkar, siparişler artar, komisyon 3 puan düşer, sözleşme yeniden 6 ay.</summary>
        public void OfferExclusive(Shop s)
        {
            if (!CanOfferExclusive(s)) return;
            s.negotiateCooldownUntil = Day + 30;
            if (Random.value < ExclusiveChance(s))
            {
                bool hadRival = s.rivalContracted;
                s.exclusive = true;
                s.rivalContracted = false;
                s.commissionBonus -= 0.03f;
                s.contractStartDay = Day;
                s.contractEndDay = Day + contractDays;
                AddSatisfaction(s, 8f);
                EventManager.I.Push(new GameEvent("Münhasır sözleşme!",
                    $"{s.Name} artık sadece seninle çalışacak" + (hadRival ? $" ({companyName} ile anlaşmasını bitirdi)" : "") +
                    $". Siparişler +%15, komisyon {Pct(s.Commission)}. Sözleşme {GameClock.I.DateOf(s.contractEndDay)} tarihine kadar."));
            }
            else
            {
                AddSatisfaction(s, -3f);
                EventManager.I.Notify($"{s.Name} münhasırlık teklifini şimdilik kabul etmedi (memnuniyet {s.satisfaction:0}).");
            }
        }

        // ================================================================ günlük / haftalık / aylık

        void OnNewDay()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            var shops = CityMap.I.Shops;

            // Rakibin teslimatları (tahmini) ve itibarı
            int d = 0;
            foreach (var s in shops)
            {
                if (!s.rivalContracted) continue;
                float theirShare = s.contracted ? 1f - Mathf.Min(1f, ShareFor(s) / 1.25f) : 1f;
                d += Mathf.RoundToInt(18f * s.def.weight * theirShare * Random.Range(0.8f, 1.2f));
            }
            d = Mathf.Min(d, couriers * 22);
            deliveriesThisWeek += d;
            money += d * 280L - couriers * 1500L - 4000;
            float load = d / Mathf.Max(1f, couriers * 15f);           // kurye başına iş yükü
            float target = Mathf.Clamp(3.8f - Mathf.Max(0f, load - 1f) * 0.8f, 2.4f, 4.4f);
            reputation = Mathf.Clamp(reputation + (target - reputation) * 0.05f + Random.Range(-0.04f, 0.04f), 1.5f, 5f);
            if (priceWarDays > 0) priceWarDays--;

            foreach (var s in shops)
            {
                if (!s.contracted) continue;
                s.satisfaction += (60f - s.satisfaction) * 0.01f; // zamanla ortalamaya döner

                if (s.satisfaction < 35f && !s.lowSatisfactionWarned)
                {
                    s.lowSatisfactionWarned = true;
                    EventManager.I.Push(new GameEvent("Dükkan memnun değil",
                        $"{s.Name} hizmetinden şikayetçi (memnuniyet {s.satisfaction:0}/100). Gecikmeler, soğuk yemek ve iptaller yüzünden. " +
                        "20'nin altına düşerse sözleşmeyi bozup rakibe geçebilir."));
                }
                else if (s.satisfaction >= 45f) s.lowSatisfactionWarned = false;

                if (s.satisfaction < 20f && Random.value < 0.25f) { LoseShop(s, "memnuniyetsizlik yüzünden sözleşmeyi bozdu"); continue; }
                if (Day >= s.contractEndDay) Renewal(s);
            }

            if (GameClock.I.DayOfWeek == 0) Weekly();
        }

        void Weekly()
        {
            deliveriesLastWeek = deliveriesThisWeek;
            deliveriesThisWeek = 0;
            myDeliveriesLastWeek = myDeliveriesThisWeek;
            myDeliveriesThisWeek = 0;

            // Büyüme: para varsa kurye alır
            if (money > 300000 && couriers < 40) { int add = Random.Range(1, 3); couriers += add; money -= add * 20000L; }
            else if (money < 0 && couriers > 4) couriers--;

            // Yeni dükkan anlaşması (senin sözleşmeli dükkanların da olabilir, münhasırlar hariç)
            if (Random.value < 0.35f)
            {
                var cands = new List<Shop>();
                foreach (var s in CityMap.I.Shops) if (!s.rivalContracted && !s.exclusive) cands.Add(s);
                if (cands.Count > 0)
                {
                    var s = cands[Random.Range(0, cands.Count)];
                    s.rivalContracted = true;
                    if (s.contracted)
                        EventManager.I.Push(new GameEvent($"{companyName} {s.Name} ile anlaştı",
                            $"Rakip platform {companyName}, {s.Name} ile de çalışmaya başladı. Bu dükkanın siparişleri artık ikiniz arasında paylaşılacak " +
                            $"(şu an payın ~%{ShareFor(s) / 1.25f * 100f:0}). Dükkanlar sekmesinden münhasırlık teklif ederek rakibi çıkarabilirsin."));
                    else
                        EventManager.I.Notify($"{companyName}, {s.Name} ile anlaştı.");
                }
            }

            // Fiyat savaşı
            if (priceWarDays == 0 && Random.value < 0.12f)
            {
                priceWarDays = 5;
                EventManager.I.Notify($"{companyName} 5 gün boyunca teslimat ücretlerini düşürdü: müşteriler fiyata daha duyarlı.");
            }

            // Moralsiz kuryeyi ayartma (haftada en fazla bir)
            foreach (var c in CourierManager.I.Couriers.ToArray())
            {
                if (c.morale >= 0.35f || c.OrderCount > 0) continue;
                if (Random.value < 0.2f) { CourierManager.I.PoachToRival(c, companyName); break; }
            }
        }

        void OnNewMonth()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            foreach (var s in CityMap.I.Shops)
            {
                s.lastMonthOrders = s.monthOrders;
                s.monthOrders = 0;
                if (!s.contracted || s.lastMonthOrders < volumeTier || s.commissionBonus <= -0.04f || Random.value < 0.5f) continue;

                var shop = s;
                EventManager.I.Push(new GameEvent($"{s.Name} indirim istiyor",
                        $"{s.Name} geçen ay {s.lastMonthOrders} sipariş verdi ({volumeTier}+). Bu hacim için komisyonun 2 puan düşmesini istiyor " +
                        $"(şu an {Pct(s.Commission)}).")
                    .Add($"Kabul et (→ {Pct(s.Commission - 0.02f)}, memnuniyet +15)", () => { shop.commissionBonus -= 0.02f; AddSatisfaction(shop, 15f); })
                    .Add("Reddet (memnuniyet −15)", () => AddSatisfaction(shop, -15f)));
            }
        }

        /// <summary>6 aylık sözleşme bitti: memnuniyet düşükse rakip araya girer.</summary>
        void Renewal(Shop s)
        {
            bool rivalOffer = (s.satisfaction < 70f && Random.value < 0.6f) || Random.value < 0.2f;
            if (!rivalOffer)
            {
                Renew(s);
                EventManager.I.Notify($"{s.Name} sözleşmesini 6 ay uzattı ({GameClock.I.DateOf(s.contractEndDay)} tarihine kadar).");
                return;
            }

            var shop = s;
            float offer = s.Commission - 0.02f;
            var ev = new GameEvent("Sözleşme yenileme: rakip teklifi",
                $"{s.Name} ile sözleşmen bitti. {companyName} dükkana {Pct(offer)} komisyonla teklif verdi " +
                $"(senin oranın {Pct(s.Commission)}, memnuniyet {s.satisfaction:0}/100).");
            ev.Add($"Teklife eşitle (→ {Pct(offer)}), 6 ay uzat", () => { shop.commissionBonus -= 0.02f; Renew(shop); AddSatisfaction(shop, 5f); });
            if (s.satisfaction >= 50f)
                ev.Add($"Münhasırlık öner (→ {Pct(s.Commission - 0.03f)}, rakip çıkar, siparişler +%15)", () =>
                {
                    shop.commissionBonus -= 0.03f; shop.exclusive = true; shop.rivalContracted = false; Renew(shop); AddSatisfaction(shop, 8f);
                });
            ev.Add($"Mevcut şartlarda ısrar et (kalma ihtimali %{s.satisfaction:0})", () =>
            {
                if (Random.value < shop.satisfaction / 100f) { Renew(shop); AddSatisfaction(shop, -5f); EventManager.I.Notify($"{shop.Name} mevcut şartlarla devam etmeyi kabul etti."); }
                else LoseShop(shop, $"{companyName}'ın teklifini kabul etti");
            });
            EventManager.I.Push(ev);
            s.contractEndDay = Day + 3; // karar verilmezse birkaç gün sonra yine sorulur
        }

        void Renew(Shop s)
        {
            s.contractStartDay = Day;
            s.contractEndDay = Day + contractDays;
        }

        void LoseShop(Shop s, string reason)
        {
            s.contracted = false;
            s.exclusive = false;
            s.rivalContracted = true;
            s.resignBlockedUntil = Day + 30;
            s.satisfaction = 40f;
            CityMap.I.RefreshColors();
            EventManager.I.Push(new GameEvent($"{s.Name} gitti",
                $"{s.Name} {reason}. Artık sadece {companyName} ile çalışıyor. " +
                $"30 gün sonra yeniden sözleşme teklif edebilirsin ({GameClock.I.DateOf(s.resignBlockedUntil)})."));
        }

        /// <summary>Haftalık rapor satırı.</summary>
        public string ReportLine =>
            $"Rakip {companyName}: itibar {reputation:0.0}/5 · {couriers} kurye · {RivalShopCount} dükkan · " +
            $"geçen hafta ~{deliveriesLastWeek} teslimat · pazar payın ~%{MarketShare * 100f:0}";
    }
}
