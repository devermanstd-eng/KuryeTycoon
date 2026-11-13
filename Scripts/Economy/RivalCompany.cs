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
        public int priceWarDays;            // fiyat savaşının kalan günü (warDistrict bölgesinde)

        [Header("Aktif rakip (yapay zekâ)")]
        public float aggression;            // 0–1: pazar payın büyüdükçe rakip saldırganlaşır
        public float quality;               // 0–1: yatırımlarla artan hizmet kalitesi (hedef itibarı yükseltir)
        public int warDistrict;             // fiyat savaşı bölgesi (1–5, 0 = yok)
        public float warFactor = 0.75f;     // savaşta o bölgedeki talep çarpanın
        public int marketingDays;           // reklam atağı: rakibin itibarı geçici olarak yüksek görünür
        public int fundingCooldownUntil;
        public int fundingRounds;
        public int lastCourierRaidDay = -999;   // kurye ayartma en fazla 6 haftada bir

        // Hizmet kalitesi (her gece güncellenir; kayda yazılmaz, yüklemede yeniden hesaplanır)
        [System.NonSerialized] public float myAvgMinutes = 40f, myLateRate = 0.25f;
        [System.NonSerialized] public float rivalAvgMinutes = 38f, rivalLateRate = 0.2f;
        [System.NonSerialized] bool serviceReady;
        [System.NonSerialized] float rivalLoad = 1f;
        public readonly List<float> shareHistory = new List<float>();   // haftalık pazar payın (son 12 hafta)

        /// <summary>Bugün dükkan başına senin teslimatların (rakibin teslimatını tahmin etmek için).</summary>
        readonly Dictionary<Shop, int> myToday = new Dictionary<Shop, int>();

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

        // ================================================================ kayıt

        public RivalSave CaptureSave() => new RivalSave
        {
            reputation = reputation, couriers = couriers, money = money, deliveriesLastWeek = deliveriesLastWeek,
            priceWarDays = priceWarDays, deliveriesThisWeek = deliveriesThisWeek,
            myDeliveriesThisWeek = myDeliveriesThisWeek, myDeliveriesLastWeek = myDeliveriesLastWeek,
            aggression = aggression, quality = quality, warDistrict = warDistrict, warFactor = warFactor,
            marketingDays = marketingDays, fundingCooldownUntil = fundingCooldownUntil, fundingRounds = fundingRounds,
            lastCourierRaidDay = lastCourierRaidDay,
            shareHistory = new List<float>(shareHistory), activeVersion = 1
        };

        /// <summary>Init'ten sonra çağrılır (dükkanların sözleşme durumu SaveSystem'de ayrıca geri konur).</summary>
        public void RestoreSave(RivalSave s)
        {
            reputation = s.reputation; couriers = s.couriers; money = s.money; deliveriesLastWeek = s.deliveriesLastWeek;
            priceWarDays = s.priceWarDays; deliveriesThisWeek = s.deliveriesThisWeek;
            myDeliveriesThisWeek = s.myDeliveriesThisWeek; myDeliveriesLastWeek = s.myDeliveriesLastWeek;
            shareHistory.Clear();
            if (s.activeVersion >= 1)
            {
                aggression = s.aggression; quality = s.quality; warDistrict = s.warDistrict; warFactor = s.warFactor > 0f ? s.warFactor : 0.75f;
                marketingDays = s.marketingDays; fundingCooldownUntil = s.fundingCooldownUntil; fundingRounds = s.fundingRounds;
                lastCourierRaidDay = s.lastCourierRaidDay;
                if (s.shareHistory != null) shareHistory.AddRange(s.shareHistory);
            }
            else
            {
                // Eski kayıt: rakip artık aktif. Haftalık tahmin yeni hesapla yeniden kurulsun, fiyat savaşı bir bölgeye bağlansın.
                warDistrict = priceWarDays > 0 ? 1 : 0;
                fundingCooldownUntil = Day + 14;
            }
        }

        // ================================================================ sipariş paylaşımı

        /// <summary>Müşterinin gözündeki çekicilik: itibarın karesi × filonun yeterliliği (40 kuryede doyar).</summary>
        static float Score(float rep, int courierCount) =>
            rep * rep * (0.4f + 0.6f * Mathf.Min(1f, courierCount / 40f) + 0.15f * Mathf.Clamp01((courierCount - 40) / 120f));

        /// <summary>
        /// Müşterinin gözünde teslimat hızı ve dakikliği: 40 dk ortalama = 1. Her dakika hızlı +%2, yavaş −%2;
        /// geç kalan siparişler (oranın yarısı kadar) düşürür. Hızlı ve dakik olan rakipten müşteri kapar.
        /// </summary>
        public static float ServiceFactor(float avgMinutes, float lateRate) =>
            Mathf.Clamp(1f + (40f - avgMinutes) * 0.02f, 0.7f, 1.35f) * (1f - 0.5f * Mathf.Clamp01(lateRate));

        public float MyService { get { EnsureService(); return ServiceFactor(myAvgMinutes, myLateRate); } }
        public float RivalService { get { EnsureService(); return ServiceFactor(rivalAvgMinutes, rivalLateRate); } }

        /// <summary>Son 7 günün ortalama teslimat süresi ve geç kalma oranı; rakibin süresi iş yüküne göre tahmin.</summary>
        void UpdateService()
        {
            serviceReady = true;
            var hist = StatsHistory.I != null ? StatsHistory.I.Days : null;
            if (hist != null && hist.Count > 0)
            {
                int n = Mathf.Min(7, hist.Count), del = 0, late = 0; float mins = 0f;
                for (int i = hist.Count - n; i < hist.Count; i++)
                {
                    var r = hist[i];
                    if (r.deliveries <= 0) continue;
                    del += r.deliveries; late += r.late; mins += r.avgMinutes * r.deliveries;
                }
                if (del > 0) { myAvgMinutes = mins / del; myLateRate = late / (float)del; }
            }
            // Rakip: kurye başına iş yükü arttıkça yavaşlar, kalite yatırımı hızlandırır
            rivalAvgMinutes = 37f + Mathf.Max(0f, rivalLoad - 0.8f) * 25f - quality * 4f;
            rivalLateRate = Mathf.Clamp(0.15f + Mathf.Max(0f, rivalLoad - 0.8f) * 0.3f - quality * 0.05f, 0.08f, 0.6f);
        }

        void EnsureService() { if (!serviceReady) UpdateService(); }

        /// <summary>Hızlı teslimat pazarı da büyütür: insanlar hızlı gelen yemeği daha sık sipariş eder (−%10 … +%15).</summary>
        public float ServiceDemandFactor
        {
            get { EnsureService(); return Mathf.Clamp(1f + (40f - myAvgMinutes) * 0.01f - (myLateRate - 0.25f) * 0.2f, 0.9f, 1.15f); }
        }

        /// <summary>Reklam atağı sırasında rakip müşteriye daha iyi görünür.</summary>
        public float EffectiveReputation => Mathf.Min(5f, reputation + (marketingDays > 0 ? 0.4f : 0f));

        /// <summary>Paylaşılan dükkanda müşterilerin senin uygulamanı seçme oranı (0,25–0,85).</summary>
        public float MyFraction(Shop s)
        {
            if (!s.rivalContracted || s.exclusive) return 1f;
            float mine = Score(CompanyProfile.Reputation, CourierManager.I.Couriers.Count) * MyService;
            float theirs = Score(EffectiveReputation, couriers) * RivalService;
            return Mathf.Clamp(mine / Mathf.Max(0.01f, mine + theirs), 0.2f, 0.9f);
        }

        /// <summary>Fiyat savaşı bölgesindeyse senin talep çarpanın.</summary>
        public float WarFactor(Shop s) =>
            priceWarDays > 0 && warDistrict > 0 && CityMap.RegionOf(s.tile) == warDistrict ? warFactor : 1f;

        /// <summary>Senin bu dükkandaki sipariş payın (talep çarpanı).</summary>
        public float ShareFor(Shop s)
        {
            if (s.exclusive) return 1.15f * WarFactor(s);   // dükkan sadece seni tanıtıyor
            if (!s.rivalContracted) return WarFactor(s);
            return Mathf.Min(1.1f, MyFraction(s) * 1.25f) * WarFactor(s);   // iki uygulamada da görünen dükkanın toplam müşterisi biraz daha fazla
        }

        public float PriceSensitivity => priceWarDays > 0 ? 0.95f : 1f;

        public int RivalShopCount
        {
            get { int n = 0; foreach (var s in CityMap.I.Shops) if (s.rivalContracted) n++; return n; }
        }

        /// <summary>Geçen haftanın teslimatlarına göre pazar payın.</summary>
        public float MarketShare => myDeliveriesLastWeek + deliveriesLastWeek == 0 ? 0.5f
            : myDeliveriesLastWeek / (float)(myDeliveriesLastWeek + deliveriesLastWeek);

        // ================================================================ memnuniyet

        /// <summary>Bir günde teslimat sorunlarından kaybedilebilecek en fazla memnuniyet.</summary>
        public const float MaxDailyLoss = 12f;

        public static void AddSatisfaction(Shop s, float delta)
        {
            if (s == null) return;
            s.satisfaction = Mathf.Clamp(s.satisfaction + delta, 0f, 100f);
        }

        /// <summary>
        /// Dükkanın hizmetten beklediği memnuniyet: son günlerin karnesine göre. Az sipariş varken nötr (65).
        /// Gecikme, soğuk yemek, iptal ve şikayet oranı arttıkça hedef düşer; her şey yolundaysa 90'lara çıkar.
        /// Memnuniyet her gün bu hedefe doğru yavaşça yaklaşır: tek bir kötü sipariş ya da kötü bir saat dükkanı küstürmez.
        /// </summary>
        public static float TargetSatisfaction(Shop s)
        {
            float total = s.perfOk + s.perfLate + s.perfCancel;
            if (total < 8f) return 65f;
            float bad = (s.perfLate + 0.7f * s.perfCold + 2f * s.perfCancel + s.perfIncident) / total;
            return Mathf.Clamp(92f - bad * 110f, 10f, 95f);
        }

        public static string PerformanceText(Shop s)
        {
            float total = s.perfOk + s.perfLate + s.perfCancel;
            if (total < 8f) return "Henüz yeterli sipariş yok: dükkan seni tanıyor (hedef memnuniyet 65).";
            return $"Son günler: zamanında %{s.perfOk / total * 100f:0}, gecikmeli %{s.perfLate / total * 100f:0}, iptal %{s.perfCancel / total * 100f:0}" +
                   (s.perfCold > 0.5f ? $", soğuk {s.perfCold:0}" : "") + $" → memnuniyet {TargetSatisfaction(s):0}'a doğru gidiyor.";
        }

        /// <summary>Teslimat sonrası: zamanında artar, gecikme ve soğuk yemek düşürür.</summary>
        public void OnDelivered(Order o, float lateMinutes)
        {
            var s = o.shop;
            s.monthOrders++;
            myDeliveriesThisWeek++;
            myToday.TryGetValue(s, out int n);
            myToday[s] = n + 1;
            // Karneye yazılır: birkaç dakikalık gecikmeyi dükkan dert etmez, çok geç kalan tam gecikme sayılır
            if (lateMinutes <= 5f) s.perfOk += 1f;
            else if (lateMinutes <= 15f) { s.perfOk += 0.5f; s.perfLate += 0.5f; }
            else s.perfLate += 1f;
            if (o.deliveredCold) s.perfCold += 1f;
        }

        public void OnCancelled(Order o) => o.shop.perfCancel += 1f;
        public void OnIncident(Order o) => o.shop.perfIncident += 1f;

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

            // Rakibin teslimatları (tahmini): paylaşılan dükkanda senin teslimatından müşteri payına göre,
            // sadece rakiple çalışan dükkanda senin dükkan başı ortalamandan
            int myTotal = 0; float myWeight = 0f;
            foreach (var kv in myToday) { myTotal += kv.Value; myWeight += kv.Key.def.weight; }
            float perWeight = myWeight > 0f ? myTotal / myWeight : 15f;
            float est = 0f;
            foreach (var s in shops)
            {
                if (!s.rivalContracted || s.own) continue;
                if (s.contracted)
                {
                    myToday.TryGetValue(s, out int mine);
                    float f = MyFraction(s);
                    est += mine * (1f - f) / Mathf.Max(0.15f, f);
                }
                else est += perWeight * s.def.weight * 0.9f;
            }
            int d = Mathf.Min(Mathf.RoundToInt(est * Random.Range(0.9f, 1.1f)), couriers * 22);
            myToday.Clear();
            deliveriesThisWeek += d;
            money += d * 280L - couriers * 1500L - 4000;
            float load = d / Mathf.Max(1f, couriers * 15f);           // kurye başına iş yükü
            rivalLoad = load;
            UpdateService();
            float target = Mathf.Clamp(3.6f + 0.9f * quality - Mathf.Max(0f, load - 1f) * 0.8f, 2.4f, 4.8f);
            reputation = Mathf.Clamp(reputation + (target - reputation) * 0.05f + Random.Range(-0.04f, 0.04f), 1.5f, 5f);
            if (priceWarDays > 0 && --priceWarDays == 0)
            {
                EventManager.I.Notify($"{companyName}'ın {CityMap.DistrictNames[Mathf.Clamp(warDistrict, 0, 5)]} fiyat savaşı bitti.");
                warDistrict = 0;
            }
            if (marketingDays > 0) marketingDays--;

            foreach (var s in shops)
            {
                if (!s.contracted || s.own) continue;
                // Memnuniyet son günlerin karnesine doğru yavaşça gider (günde aradaki farkın %12'si); karne her gün %20 unutulur
                s.satisfaction = Mathf.Clamp(s.satisfaction + (TargetSatisfaction(s) - s.satisfaction) * 0.12f, 0f, 100f);
                s.perfOk *= 0.8f; s.perfLate *= 0.8f; s.perfCold *= 0.8f; s.perfCancel *= 0.8f; s.perfIncident *= 0.8f;
                s.todayLoss = 0f;

                if (s.satisfaction < 35f && !s.lowSatisfactionWarned)
                {
                    s.lowSatisfactionWarned = true;
                    // Bilgi: oyunu durduran kart değil, bildirim kartı
                    EventManager.I.Notify($"{s.Name} hizmetinden şikayetçi (memnuniyet {s.satisfaction:0}/100): gecikme, soğuk yemek ve iptaller. " +
                                          "15'in altına düşerse sözleşmeyi bozabilir.");
                }
                else if (s.satisfaction >= 45f) s.lowSatisfactionWarned = false;

                if (s.satisfaction < 15f && Random.value < 0.2f) { LoseShop(s, "memnuniyetsizlik yüzünden sözleşmeyi bozdu"); continue; }
                if (Day >= s.contractEndDay) Renewal(s);
            }

            if (GameClock.I.DayOfWeek == 0) Weekly();
        }

        /// <summary>Haftayı en son hangi gün kapattı (haftalık rapor, sırası ne olursa olsun güncel satırı yazsın diye).</summary>
        public int HaftaKapanisGunu { get; private set; } = -1;

        void Weekly()
        {
            HaftaKapanisGunu = Day;
            deliveriesLastWeek = deliveriesThisWeek;
            deliveriesThisWeek = 0;
            myDeliveriesLastWeek = myDeliveriesThisWeek;
            myDeliveriesThisWeek = 0;
            shareHistory.Add(MarketShare);
            while (shareHistory.Count > 12) shareHistory.RemoveAt(0);
            if (EconomyManager.I != null) EconomyManager.I.RakipSatiriniYaz(ReportLine);   // haftalık rapor bu sayılarla

            // Saldırganlık: pazar payın %45'in üstüne çıktıkça rakip daha çok hamle yapar, yatırım arar
            float targetAgg = Mathf.Clamp01((MarketShare - 0.45f) * 2.5f);
            aggression = Mathf.Lerp(aggression, targetAgg, 0.5f);

            // Yatırım turu: rakip büyük pay kaybettiyse yatırımcı bulur
            if (aggression > 0.4f && Day >= fundingCooldownUntil && GameClock.I.CompanyDay >= 90 && myDeliveriesLastWeek >= 1500 &&
                Random.value < 0.5f) FundingRound();

            // Büyüme: para varsa kurye alır (saldırganken daha hızlı), hizmet kalitesine yatırım yapar
            int maxCouriers = 40 + 30 * fundingRounds;
            if (money > 1000000 && couriers < Mathf.Min(160, maxCouriers))
            {
                int add = Mathf.Min((int)((money - 1000000) / 400000), aggression > 0.5f ? 8 : 3);
                add = Mathf.Max(1, Mathf.Min(add, Mathf.Min(160, maxCouriers) - couriers));
                couriers += add; money -= add * 400000L;
            }
            else if (money < 0 && couriers > 4) couriers -= Mathf.Min(couriers - 4, 2);
            if (money > 5000000 && quality < 1f) { money -= 3000000; quality = Mathf.Min(1f, quality + 0.05f); }

            // Yeni dükkan anlaşması (senin sözleşmeli dükkanların da olabilir, münhasırlar hariç)
            if (Random.value < 0.35f)
            {
                var cands = new List<Shop>();
                foreach (var s in CityMap.I.Shops) if (!s.rivalContracted && !s.exclusive && !s.own) cands.Add(s);
                if (cands.Count > 0)
                {
                    var s = cands[Random.Range(0, cands.Count)];
                    s.rivalContracted = true;
                    if (s.contracted)
                        EventManager.I.Push(new GameEvent($"{companyName} {s.Name} ile anlaştı",
                            $"Rakip platform {companyName}, {s.Name} ile de çalışmaya başladı. Bu dükkanın siparişleri artık ikiniz arasında paylaşılacak " +
                            $"(şu an payın ~%{MyFraction(s) * 100f:0}). Dükkanlar sekmesinden münhasırlık teklif ederek rakibi çıkarabilirsin."));
                    else
                        EventManager.I.Notify($"{companyName}, {s.Name} ile anlaştı.");
                }
            }

            // Rakibin haftalık hamlesi
            float moveChance = (0.2f + 0.55f * aggression) * (GameClock.I.CompanyDay < 60 ? 0.4f : 1f);   // ilk aylarda daha nadir
            if (GameClock.I.CompanyDay >= 21 && Random.value < moveChance) RivalMove();

            // Moralsiz kuryeyi ayartma (haftada en fazla bir; çalışan statüsündeki kuryeler gitmez)
            if (!CrisisManager.Employees)
                foreach (var c in CourierManager.I.Couriers.ToArray())
                {
                    if (c.morale >= 0.35f || c.OrderCount > 0) continue;
                    if (UpgradeManager.I != null && UpgradeManager.I.LoyaltyProgram && c.level >= 3) continue;   // sadakat programı: kıdemli kurye kalır
                    if (Random.value < 0.2f) { CourierManager.I.PoachToRival(c, companyName); break; }
                }
        }

        // ================================================================ rakibin hamleleri

        static long Round1000(float v) => (long)Mathf.Round(v / 1000f) * 1000;

        void FundingRound()
        {
            long amount = Round1000(Random.Range(30000000f, 60000000f) * (1f + 0.3f * fundingRounds));
            money += amount;
            fundingRounds++;
            quality = Mathf.Min(1f, quality + 0.2f);
            fundingCooldownUntil = Day + 60;
            Telemetry.Add("Rakip: yatırım turu");
            EventManager.I.Push(new GameEvent($"{companyName} yatırım aldı",
                $"Rakip {companyName}, pazar payını geri almak için yatırımcılardan {GameData.Ft(amount)} topladı. " +
                "Önümüzdeki haftalarda daha çok kurye alacak, hizmet kalitesini yükseltecek ve dükkanlarına, kuryelerine teklif yapacak.\n\n" +
                $"Pazar payın: %{MarketShare * 100f:0}. İtibarını yüksek tut, iyi dükkanlarla münhasırlık yap, kuryelerinin moralini koru."));
        }

        void RivalMove()
        {
            float r = Random.value;
            if (r < 0.4f && ShopRaid()) return;
            if (r < 0.65f && CourierRaid()) return;
            if (r < 0.85f && priceWarDays == 0 && PriceWar()) return;
            if (marketingDays == 0) MarketingBlitz();
        }

        /// <summary>En çok sipariş veren dükkanlarından birine sözleşme bitmeden daha düşük komisyon teklifi.</summary>
        bool ShopRaid()
        {
            var cands = new List<Shop>();
            foreach (var s in CityMap.I.Shops)
                if (s.contracted && !s.own && !s.exclusive && CityMap.I.IsUnlocked(s.tile)) cands.Add(s);
            if (cands.Count == 0) return false;
            cands.Sort((a, b) => (b.lastMonthOrders + b.monthOrders).CompareTo(a.lastMonthOrders + a.monthOrders));
            var shop = cands[Random.Range(0, Mathf.Min(3, cands.Count))];
            float offer = Mathf.Max(0.05f, shop.Commission - 0.04f);
            float keep = Mathf.Clamp(0.15f + shop.satisfaction / 100f * 0.8f, 0.15f, 0.95f);
            Telemetry.Add("Rakip: dükkan teklifi");
            var ev = new GameEvent($"{companyName}, {shop.Name} dükkanına göz dikti",
                $"{companyName} sözleşmen bitmeden {shop.Name} dükkanına {Pct(offer)} komisyonla teklif verdi " +
                $"(senin oranın {Pct(shop.Commission)}, memnuniyet {shop.satisfaction:0}/100, geçen ay {shop.lastMonthOrders} sipariş).");
            ev.Add($"Karşı teklif ver (→ {Pct(shop.Commission - 0.03f)}, sözleşme 6 ay uzar, memnuniyet +6)", () =>
            {
                shop.commissionBonus -= 0.03f; Renew(shop); AddSatisfaction(shop, 6f);
            });
            if (shop.satisfaction >= 50f)
            {
                float chance = Mathf.Clamp(ExclusiveChance(shop) + 0.15f, 0f, 0.95f);
                ev.Add($"Münhasırlık öner (→ {Pct(shop.Commission - 0.04f)}, rakip çıkar · kabul ihtimali %{chance * 100f:0})", () =>
                {
                    if (Random.value < chance)
                    {
                        shop.commissionBonus -= 0.04f; shop.exclusive = true; shop.rivalContracted = false; Renew(shop); AddSatisfaction(shop, 8f);
                        EventManager.I.Notify($"{shop.Name} münhasırlığı kabul etti: artık sadece seninle çalışıyor.");
                    }
                    else if (Random.value < keep) { AddSatisfaction(shop, -5f); EventManager.I.Notify($"{shop.Name} münhasırlığı reddetti ama seninle devam ediyor."); }
                    else LoseShop(shop, $"münhasırlığı reddetti ve {companyName}'ın teklifini kabul etti");
                });
            }
            ev.Add($"Bir şey yapma (kalma ihtimali %{keep * 100f:0})", () =>
            {
                if (Random.value < keep) EventManager.I.Notify($"{shop.Name} {companyName}'ın teklifini reddetti, seninle devam ediyor.");
                else LoseShop(shop, $"{companyName}'ın teklifini kabul etti");
            });
            EventManager.I.Push(ev);
            return true;
        }

        /// <summary>En deneyimli kuryelerine daha yüksek ücret teklifi.</summary>
        bool CourierRaid()
        {
            var list = new List<Courier>(CourierManager.I.Couriers);
            if (list.Count < 8) return false;
            // Karşı zam sürerken ya da son 6 hafta içinde denediyse tekrar denemez (başka hamle seçer)
            if (CrisisManager.I.HasEffect("rakip_zam") || Day - lastCourierRaidDay < 42) return false;
            lastCourierRaidDay = Day;
            if (CrisisManager.Employees)
            {
                EventManager.I.Notify($"{companyName} kuryelerine daha yüksek ücret teklif etti ama çalışan statüsündeki kuryelerin hiçbiri gitmedi.");
                return true;
            }
            list.Sort((a, b) => b.level != a.level ? b.level.CompareTo(a.level) : b.deliveries.CompareTo(a.deliveries));
            int n = Mathf.Clamp(list.Count / 12, 3, 6);
            var targets = list.GetRange(0, n);
            var names = new List<string>();
            foreach (var c in targets) names.Add($"{c.courierName} (seviye {c.level})");
            long bonus = Round1000(n * 30000f);
            Telemetry.Add("Rakip: kurye teklifi");
            EventManager.I.Push(new GameEvent($"{companyName} kuryelerini ayartıyor",
                    $"{companyName}, en deneyimli {n} kuryene %20 daha yüksek ücret teklif etti: {string.Join(", ", names)}.")
                .Add("Tüm kurye ücretlerini 4 hafta +%10 artır (hepsi kalır, moral +8)", () =>
                {
                    CrisisManager.I.AddEffect(new CrisisEffect { id = "rakip_zam", title = $"{companyName}'a karşı zam: ücretler +%10", pay = 1.1f }, 28);
                    foreach (var c in CourierManager.I.Couriers) c.morale = Mathf.Clamp01(c.morale + 0.08f);
                })
                .Add($"Sadece bu {n} kuryeye prim ver (−{GameData.Ft(bonus)}, her biri %85 kalır)", () =>
                {
                    EconomyManager.I.Spend(bonus, "Rakibe karşı kurye primi", ExpenseCategory.Diger);
                    RaidOutcome(targets, 0.85f, 0.15f);
                })
                .Add("Bırak (her biri %55 ihtimalle gider)", () => RaidOutcome(targets, 0.45f, -0.05f)));
            return true;
        }

        void RaidOutcome(List<Courier> targets, float stay, float moraleDelta)
        {
            var gone = new List<string>();
            foreach (var c in targets)
            {
                if (c == null || !CourierManager.I.Couriers.Contains(c)) continue;
                bool loyal = UpgradeManager.I != null && UpgradeManager.I.LoyaltyProgram && c.level >= 3;
                if (c.OrderCount > 0 || Random.value < (loyal ? Mathf.Max(stay, 0.75f) : stay))
                    c.morale = Mathf.Clamp01(c.morale + moraleDelta);
                else { gone.Add(c.courierName); CourierManager.I.PoachToRival(c, companyName, false); }
            }
            couriers += gone.Count;
            EventManager.I.Notify(gone.Count == 0 ? "Teklif alan kuryelerin hepsi seninle kaldı."
                : $"{gone.Count} kurye {companyName}'a geçti: {string.Join(", ", gone)}.");
        }

        /// <summary>Bir bölgede 7 gün ücret kırar: o bölgedeki siparişlerin düşer.</summary>
        bool PriceWar()
        {
            int maxD = Mathf.Clamp(CityMap.I.UnlockLevel, 1, 5);
            warDistrict = Random.Range(1, maxD + 1);
            priceWarDays = 7;
            warFactor = 0.75f;
            string dn = CityMap.DistrictNames[warDistrict];
            long cost = Round1000(Mathf.Max(400000f, CrisisManager.AvgDailyIncome() * 0.5f));
            Telemetry.Add("Rakip: fiyat savaşı");
            EventManager.I.Push(new GameEvent($"Fiyat savaşı: {dn}",
                    $"{companyName}, {dn} bölgesinde 7 gün boyunca teslimat ücretini sıfırladı. Bu bölgedeki siparişlerin %25 düşecek.")
                .Add($"Karşı kampanya yap (−{GameData.Ft(cost)}, kayıp %10'a iner)", () =>
                {
                    EconomyManager.I.Spend(cost, $"Fiyat savaşı karşı kampanyası ({dn})", ExpenseCategory.Reklam);
                    warFactor = 0.9f;
                })
                .Add("Görmezden gel (bölgede siparişler −%25)", null));
            return true;
        }

        /// <summary>İki hafta yoğun reklam: müşteriler rakibi daha iyi görür.</summary>
        void MarketingBlitz()
        {
            marketingDays = 14;
            long cost = Round1000(Mathf.Max(300000f, CrisisManager.AvgDailyIncome() * 0.4f));
            Telemetry.Add("Rakip: reklam atağı");
            EventManager.I.Push(new GameEvent($"{companyName} reklam atağında",
                    $"{companyName} şehrin her yerine ilan verdi ve 2 hafta boyunca müşterilere daha iyi görünecek " +
                    "(paylaşılan dükkanlarda siparişlerin bir kısmı rakibe kayar).")
                .Add($"Sen de reklam ver (−{GameData.Ft(cost)}, etkisi 1 haftaya iner)", () =>
                {
                    EconomyManager.I.Spend(cost, "Rakibe karşı reklam", ExpenseCategory.Reklam);
                    marketingDays = 7;
                })
                .Add("Görmezden gel", null));
        }

        void OnNewMonth()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            foreach (var s in CityMap.I.Shops)
            {
                s.lastMonthOrders = s.monthOrders;
                s.monthOrders = 0;
                if (!s.contracted || s.own || s.lastMonthOrders < volumeTier || s.commissionBonus <= -0.04f || Random.value < 0.5f) continue;

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
            $"geçen hafta ~{deliveriesLastWeek} teslimat · pazar payın ~%{MarketShare * 100f:0} · tavrı: {MoodText}";

        public string MoodText => aggression > 0.66f ? "çok saldırgan" : aggression > 0.33f ? "saldırgan" : aggression > 0.1f ? "temkinli" : "sakin";

        /// <summary>Geçen haftaya göre pazar payı değişimi (puan).</summary>
        public float ShareTrend => shareHistory.Count < 2 ? 0f : shareHistory[shareHistory.Count - 1] - shareHistory[shareHistory.Count - 2];
    }
}
