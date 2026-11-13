using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Bir krizin ya da kararın haftalarca süren etkisi.</summary>
    [System.Serializable]
    public class CrisisEffect
    {
        public string id;
        public string title;
        public int endDay;                 // bu günün başında biter
        public float pay = 1f;             // tüm kurye ücretleri
        public float payMotor = 1f;        // sadece motor ve araba kuryeleri
        public float demand = 1f;          // sipariş talebi
        public float fatigue = 1f;         // yorulma hızı
        public float online = 1f;          // çevrimiçi olma isteği
        public float moraleDaily;          // her gün moral değişimi
        public bool motorOnly;             // online / moral sadece motor ve araba kuryelerine
        public string offlineReason = "";
    }

    [System.Serializable]
    public class CrisisSave
    {
        public List<CrisisEffect> effects = new List<CrisisEffect>();
        public int nextCrisisDay;
        public int lastStrikeDay = -999, lastOutageDay = -999, lastFuelDay = -999, lastHeatDay = -999;
        public float outageUntil;
        public bool serverUpgraded;
        public bool employees;             // kuryeler çalışan statüsünde
        public int lawStage;               // 0 henüz yok · 1 ertelendi · 2 karar verildi
        public int lawDecisionDay;
        public long lawMonthlyFee;         // serbest çalışan modeli için aylık hukuki uyum gideri
    }

    /// <summary>
    /// Krizler ve kalıcı etkili kararlar: kurye grevi, uygulama çökmesi, akaryakıt zammı, sıcak dalgası
    /// ve platform çalışanları yasası. Her krizde bir seçim yapılır; sonucu günlerce/haftalarca sürer
    /// (ücret, talep, yorgunluk, kuryelerin çevrimiçi olma isteği, moral).
    /// Etkiler başka sistemlerden statik özelliklerle okunur (PayMultiplier, DemandFactor, FatigueFactor, OnlineFactor).
    /// </summary>
    public class CrisisManager : MonoBehaviour
    {
        public static CrisisManager I { get; private set; }

        public readonly List<CrisisEffect> Effects = new List<CrisisEffect>();
        int nextCrisisDay;
        int lastStrikeDay = -999, lastOutageDay = -999, lastFuelDay = -999, lastHeatDay = -999;
        float outageUntil;
        public bool serverUpgraded;
        public bool employees;
        int lawStage, lawDecisionDay;
        public long lawMonthlyFee;

        /// <summary>
        /// Çalışan statüsünün işveren maliyeti: kuryelere ödenen hakedişin %28'i (Macaristan'da işveren sosyal katkısı %13
        /// + ücretli izin, hastalık ve sigorta) + kurye başına günde 800 Ft (~2 €; bordro, iş güvenliği eğitimi, ekipman).
        /// Az çalışan kurye az maliyet çıkarır; boşta bekleyen çok kurye yine de pahalıdır.
        /// </summary>
        public const float EmployeeCostShare = 0.28f;
        public const long EmployeeDailyFixed = 800;

        /// <summary>Son günün kurye ödemesine göre çalışan statüsünün günlük maliyeti.</summary>
        public static long EmployeeDailyCost(int courierCount)
        {
            var hist = StatsHistory.I != null ? StatsHistory.I.Days : null;
            long pay = hist != null && hist.Count > 0 ? hist[hist.Count - 1].courierPay : 0;
            return (long)Mathf.Round(pay * EmployeeCostShare) + EmployeeDailyFixed * courierCount;
        }

        static int Day => GameClock.I.Day;
        static string Ft(long v) => GameData.Ft(v);

        void Awake() { I = this; }

        public void Init()
        {
            Effects.Clear();
            nextCrisisDay = Day + Random.Range(12, 20);
            lastStrikeDay = lastOutageDay = lastFuelDay = lastHeatDay = -999;
            outageUntil = 0f;
            serverUpgraded = false;
            employees = false;
            lawStage = 0; lawDecisionDay = 0; lawMonthlyFee = 0;
            GameClock.I.OnNewDay -= OnNewDay;
            GameClock.I.OnNewMonth -= OnNewMonth;
            GameClock.I.OnNewDay += OnNewDay;
            GameClock.I.OnNewMonth += OnNewMonth;
        }

        // ================================================================ kayıt

        public CrisisSave CaptureSave() => new CrisisSave
        {
            effects = new List<CrisisEffect>(Effects), nextCrisisDay = nextCrisisDay,
            lastStrikeDay = lastStrikeDay, lastOutageDay = lastOutageDay, lastFuelDay = lastFuelDay, lastHeatDay = lastHeatDay,
            outageUntil = outageUntil, serverUpgraded = serverUpgraded, employees = employees,
            lawStage = lawStage, lawDecisionDay = lawDecisionDay, lawMonthlyFee = lawMonthlyFee
        };

        public void RestoreSave(CrisisSave s)
        {
            if (s == null) return;   // eski kayıt: Init'teki varsayılanlar kalır
            Effects.Clear();
            if (s.effects != null) Effects.AddRange(s.effects);
            nextCrisisDay = s.nextCrisisDay > 0 ? s.nextCrisisDay : Day + 10;
            lastStrikeDay = s.lastStrikeDay; lastOutageDay = s.lastOutageDay; lastFuelDay = s.lastFuelDay; lastHeatDay = s.lastHeatDay;
            outageUntil = s.outageUntil; serverUpgraded = s.serverUpgraded; employees = s.employees;
            lawStage = s.lawStage; lawDecisionDay = s.lawDecisionDay; lawMonthlyFee = s.lawMonthlyFee;
        }

        // ================================================================ diğer sistemlerin okuduğu etkiler

        static bool Motorized(VehicleKind k) => k == VehicleKind.Motosiklet || k == VehicleKind.Araba;

        /// <summary>Kurye ücret çarpanı (grev zammı, yakıt desteği, rakibe karşı zam...).</summary>
        public static float PayMultiplier(VehicleDef v)
        {
            if (I == null) return 1f;
            float m = 1f;
            foreach (var e in I.Effects)
            {
                m *= e.pay;
                if (v != null && Motorized(v.kind)) m *= e.payMotor;
            }
            return m;
        }

        public static float DemandFactor
        {
            get { if (I == null) return 1f; float m = 1f; foreach (var e in I.Effects) m *= e.demand; return m; }
        }

        public static float FatigueFactor
        {
            get { if (I == null) return 1f; float m = 1f; foreach (var e in I.Effects) m *= e.fatigue; return m; }
        }

        public static float OnlineFactor(Courier c)
        {
            if (I == null || c == null) return 1f;
            float m = 1f;
            foreach (var e in I.Effects)
                if (!e.motorOnly || Motorized(c.vehicle)) m *= e.online;
            return m;
        }

        public static string OfflineReason(Courier c)
        {
            if (I == null || c == null) return "";
            foreach (var e in I.Effects)
                if (e.online < 1f && (!e.motorOnly || Motorized(c.vehicle)) && !string.IsNullOrEmpty(e.offlineReason)) return e.offlineReason;
            return "";
        }

        /// <summary>Uygulama çöktü: yeni sipariş gelmiyor.</summary>
        public static bool OrdersBlocked => I != null && GameClock.I != null && GameClock.I.TotalMinutes < I.outageUntil;

        public static bool Employees => I != null && I.employees;

        public static float OutageMinutesLeft => I == null ? 0f : Mathf.Max(0f, I.outageUntil - GameClock.I.TotalMinutes);

        /// <summary>Aynı türden etki varsa üst üste binmez: yenisi eskisinin yerine geçer.</summary>
        public void AddEffect(CrisisEffect e, int days)
        {
            e.endDay = Day + Mathf.Max(1, days);
            Effects.RemoveAll(x => x.id == e.id);
            Effects.Add(e);
        }

        public bool HasEffect(string id) => Effects.Exists(x => x.id == id);

        public int DaysLeft(CrisisEffect e) => Mathf.Max(0, e.endDay - Day);

        /// <summary>Üst çubuk ve pencereler için kısa açıklama.</summary>
        public static string EffectText(CrisisEffect e)
        {
            var parts = new List<string>();
            if (!Mathf.Approximately(e.pay, 1f)) parts.Add($"kurye ücretleri {Pct(e.pay)}");
            if (!Mathf.Approximately(e.payMotor, 1f)) parts.Add($"motor/araba ücretleri {Pct(e.payMotor)}");
            if (!Mathf.Approximately(e.demand, 1f)) parts.Add($"talep {Pct(e.demand)}");
            if (!Mathf.Approximately(e.fatigue, 1f)) parts.Add($"yorulma {Pct(e.fatigue)}");
            if (!Mathf.Approximately(e.online, 1f)) parts.Add($"{(e.motorOnly ? "motor/araba kuryelerinin " : "")}çalışma isteği {Pct(e.online)}");
            if (e.moraleDaily < 0f) parts.Add("moral her gün düşüyor");
            else if (e.moraleDaily > 0f) parts.Add("moral her gün artıyor");
            return string.Join(", ", parts);
        }

        static string Pct(float m) => m >= 1f ? $"+%{(m - 1f) * 100f:0}" : $"−%{(1f - m) * 100f:0}";

        // ================================================================ yardımcılar

        /// <summary>Son 7 günün ortalama günlük geliri (kriz maliyetleri şirketin büyüklüğüne göre ölçeklenir).</summary>
        public static long AvgDailyIncome()
        {
            var hist = StatsHistory.I != null ? StatsHistory.I.Days : null;
            if (hist == null || hist.Count == 0) return 400000;
            int n = Mathf.Min(7, hist.Count);
            long sum = 0;
            for (int i = hist.Count - n; i < hist.Count; i++) sum += hist[i].income;
            return System.Math.Max(200000L, sum / n);
        }

        static long Round1000(float v) => (long)Mathf.Round(v / 1000f) * 1000;

        static List<Courier> Couriers => CourierManager.I.Couriers;

        int MotorizedCount()
        {
            int n = 0;
            foreach (var c in Couriers) if (Motorized(c.vehicle)) n++;
            return n;
        }

        static void AddMorale(float delta, bool motorOnly = false)
        {
            foreach (var c in Couriers)
                if (!motorOnly || Motorized(c.vehicle)) c.morale = Mathf.Clamp01(c.morale + delta);
        }

        public float AvgMorale()
        {
            if (Couriers.Count == 0) return 0.7f;
            float s = 0f;
            foreach (var c in Couriers) s += c.morale;
            return s / Couriers.Count;
        }

        // ================================================================ gün / ay

        void OnNewDay()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;

            // Süresi dolan etkiler
            for (int i = Effects.Count - 1; i >= 0; i--)
            {
                if (Day < Effects[i].endDay) continue;
                EventManager.I.Notify($"Sona erdi: {Effects[i].title}.");
                Effects.RemoveAt(i);
            }
            // Günlük moral etkileri
            foreach (var e in Effects)
                if (e.moraleDaily != 0f) AddMorale(e.moraleDaily, e.motorOnly);

            // Çalışan statüsü: işveren maliyeti ve biraz daha yüksek bağlılık
            if (employees && Couriers.Count > 0)
            {
                EconomyManager.I.Spend(EmployeeDailyCost(Couriers.Count), $"Kurye çalışan maliyeti ({Couriers.Count} kişi: hakedişin %28'i + kişi başı {Ft(EmployeeDailyFixed)})", ExpenseCategory.Maas);
                foreach (var c in Couriers) if (c.morale < 0.75f) c.morale = Mathf.Min(0.75f, c.morale + 0.01f);
            }

            // Yasa: ertelendiyse vakti gelince yeniden sorulur, hiç gelmediyse bir gün gelir
            if (lawStage == 1 && Day >= lawDecisionDay) PlatformLaw();
            else if (lawStage == 0 && GameClock.I.CompanyDay >= 120 && Couriers.Count >= 20 && Random.value < 0.02f) PlatformLaw();

            if (Day >= nextCrisisDay && GameClock.I.CompanyDay >= 25 && !Anlatici.Mola) RollCrisis();
        }

        void OnNewMonth()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            if (lawStage == 2 && !employees && lawMonthlyFee > 0)
            {
                EconomyManager.I.Spend(lawMonthlyFee, "Platform yasası: hukuki uyum ve sözleşme denetimi", ExpenseCategory.Vergi);
                if (Random.value < 0.08f)
                {
                    long fine = Round1000(Mathf.Max(2000000f, AvgDailyIncome() * 1.5f));
                    EconomyManager.I.Spend(fine, "Çalışma denetimi cezası (kurye statüsü)", ExpenseCategory.Vergi);
                    EventManager.I.Push(new GameEvent("Çalışma denetimi: ceza",
                        $"Müfettişler bazı kuryelerin fiilen tam zamanlı çalıştığını tespit etti. Ceza: {Ft(fine)}. " +
                        "Kuryeleri çalışan statüsüne alırsan bu risk kalkar."));
                }
            }
        }

        void RollCrisis()
        {
            nextCrisisDay = Day + Anlatici.KrizAraligi();   // olay anlatıcısına göre (Anlatici.cs)
            Anlatici.BuyukOlay();
            int month = GameClock.I.Month;
            var pool = new List<(System.Action run, float w)>();
            // Grev: en az 90 günde bir ve önceki grev anlaşmasının zammı sürerken değil
            // Grev ancak kuryeler gerçekten mutsuzken (ortalama moral %50'nin altı)
            if (Couriers.Count >= 12 && AvgMorale() < Denge.GrevMoralSiniri && Day - lastStrikeDay >= 90 && !HasEffect("grev_zam") && !HasEffect("grev_uzlasma") && !HasEffect("paket_ucret"))
                pool.Add((new System.Action(Strike), (AvgMorale() < 0.6f ? 2.5f : 1f) * (employees ? 0.3f : 1f)
                                                  * (UpgradeManager.I != null && UpgradeManager.I.InsuranceProgram ? 0.5f : 1f)));
            if (Day - lastOutageDay >= 40)
                pool.Add((new System.Action(Outage), serverUpgraded ? 0.3f : 1f));
            if (Day - lastFuelDay >= 60 && MotorizedCount() >= 3)
                pool.Add((new System.Action(FuelHike), 1f));
            if (month >= 6 && month <= 8 && Day - lastHeatDay >= 20)
                pool.Add((new System.Action(Heatwave), 2f));
            if (pool.Count == 0) return;

            float total = 0f;
            foreach (var p in pool) total += p.w;
            float r = Random.value * total;
            foreach (var p in pool)
            {
                r -= p.w;
                if (r <= 0f) { p.run(); return; }
            }
            pool[pool.Count - 1].run();
        }

        // ================================================================ krizler

        void Strike()
        {
            lastStrikeDay = Day;
            Telemetry.Add("Kriz: kurye grevi");
            EventManager.I.Push(new GameEvent("Kuryeler grev hazırlığında",
                    $"Kurye temsilcileri ücretlere %15 zam istiyor (ortalama moral %{AvgMorale() * 100f:0}). " +
                    "Kabul edilmezse bugünden itibaren 2 gün grev yapacaklar: kuryelerin çoğu çevrimdışı kalır, siparişler kaçar.")
                .Add("Kabul et: 6 hafta boyunca ücretler +%15 (moral +12)", () =>
                {
                    AddEffect(new CrisisEffect { id = "grev_zam", title = "Grev anlaşması: ücretler +%15", pay = 1.15f }, 42);
                    AddMorale(0.12f);
                })
                .Add("Uzlaş: 6 hafta +%7 (bir kısmı yine de 1 gün iş bırakabilir)", () =>
                {
                    AddEffect(new CrisisEffect { id = "grev_uzlasma", title = "Grev uzlaşması: ücretler +%7", pay = 1.07f }, 42);
                    if (Random.value < 0.4f)
                    {
                        AddEffect(new CrisisEffect { id = "grev_kismi", title = "Kısmi iş bırakma", online = 0.6f, offlineReason = "İş bırakma eyleminde" }, 1);
                        EventManager.I.Notify("Kuryelerin bir kısmı uzlaşmayı az bulup bugün iş bıraktı.");
                    }
                    else AddMorale(0.05f);
                })
                .Add("Reddet: 2 gün grev (moral −12, itibar düşer)", () =>
                {
                    AddEffect(new CrisisEffect { id = "grev", title = "Kurye grevi", online = 0.3f, offlineReason = "Grevde" }, 2);
                    AddMorale(-0.12f);
                    CompanyProfile.AddReputation(-0.05f);
                }));
        }

        void Outage()
        {
            lastOutageDay = Day;
            outageUntil = GameClock.I.TotalMinutes + 120f;
            Telemetry.Add("Kriz: uygulama çöktü");
            long coupon = Round1000(AvgDailyIncome() * 0.08f);
            long server = Round1000(Mathf.Max(4000000f, AvgDailyIncome() * 2f));
            var ev = new GameEvent("Uygulama çöktü!",
                "Sunucu arızası: 2 saat boyunca yeni sipariş alınamayacak. Yoldaki siparişler teslim edilmeye devam ediyor. " +
                "Müşteriler sosyal medyada şikayet ediyor.");
            ev.Add($"Müşterilere özür kuponu gönder (−{Ft(coupon)}, itibar korunur)", () =>
                EconomyManager.I.Spend(coupon, "Uygulama çökmesi: özür kuponları", ExpenseCategory.Reklam));
            ev.Add("Sadece duyuru yap (itibar −0,08)", () => CompanyProfile.AddReputation(-0.08f));
            if (!serverUpgraded)
                ev.Add($"Yedekli sunucuya geç (−{Ft(server)}, ileride çökme çok seyrek)", () =>
                {
                    EconomyManager.I.Spend(server, "Yedekli sunucu altyapısı", ExpenseCategory.Gelistirme);
                    serverUpgraded = true;
                    CompanyProfile.AddReputation(-0.04f);
                });
            EventManager.I.Push(ev);
        }

        void FuelHike()
        {
            lastFuelDay = Day;
            Telemetry.Add("Kriz: akaryakıt zammı");
            int n = MotorizedCount();
            long card = Round1000(n * 40000f);
            EventManager.I.Push(new GameEvent("Akaryakıt zammı",
                    $"Benzinin litresi bir gecede ~%18 arttı. {n} motor ve araba kuryenin masrafı yükseldi; destek alamazlarsa daha az çalışacaklar. " +
                    "Bisiklet ve scooter kuryeleri etkilenmiyor.")
                .Add("Yakıt desteği: motor/araba kurye ücretleri 3 hafta +%12", () =>
                    AddEffect(new CrisisEffect { id = "yakit_destek", title = "Yakıt desteği: motor/araba ücretleri +%12", payMotor = 1.12f }, 21))
                .Add($"Tek seferlik yakıt kartı (−{Ft(card)}, etkisi yarıya iner)", () =>
                {
                    EconomyManager.I.Spend(card, "Kuryelere yakıt kartı", ExpenseCategory.Diger);
                    AddMorale(0.05f, true);
                    AddEffect(new CrisisEffect { id = "yakit", title = "Akaryakıt zammı (yakıt kartı verildi)", motorOnly = true,
                                                 online = 0.92f, moraleDaily = -0.01f, offlineReason = "Benzin pahalı, bugün az çalışıyor" }, 21);
                })
                .Add("Bir şey yapma (3 hafta: daha az çalışırlar, moral düşer)", () =>
                    AddEffect(new CrisisEffect { id = "yakit", title = "Akaryakıt zammı", motorOnly = true,
                                                 online = 0.8f, moraleDaily = -0.03f, offlineReason = "Benzin pahalı, bugün az çalışıyor" }, 21)));
        }

        void Heatwave()
        {
            lastHeatDay = Day;
            Telemetry.Add("Kriz: sıcak dalgası");
            long kits = Round1000(Couriers.Count * 6000f);
            EventManager.I.Push(new GameEvent("Sıcak dalgası",
                    "5 gün boyunca 35°C üstü bekleniyor. İçecek ve dondurma siparişleri artacak (+%15), " +
                    "açıkta çalışan kuryeler çok daha çabuk yorulacak.")
                .Add($"Su ve soğutma paketi dağıt (−{Ft(kits)}, yorulma +%15)", () =>
                {
                    EconomyManager.I.Spend(kits, "Sıcak dalgası: su ve soğutma paketleri", ExpenseCategory.Ekipman);
                    AddEffect(new CrisisEffect { id = "sicak", title = "Sıcak dalgası (soğutma paketi var)", demand = 1.15f, fatigue = 1.15f }, 5);
                })
                .Add("Sıcak primi: 5 gün ücretler +%8 (daha çok kurye çıkar)", () =>
                    AddEffect(new CrisisEffect { id = "sicak", title = "Sıcak dalgası (sıcak primi)", demand = 1.15f, fatigue = 1.3f, online = 1.1f, pay = 1.08f }, 5))
                .Add("Bir şey yapma (yorulma +%50, moral düşer)", () =>
                    AddEffect(new CrisisEffect { id = "sicak", title = "Sıcak dalgası", demand = 1.15f, fatigue = 1.5f, moraleDaily = -0.02f }, 5)));
        }

        void PlatformLaw()
        {
            Telemetry.Add("Kriz: platform yasası");
            int n = Couriers.Count;
            long daily = EmployeeDailyCost(n);
            long monthly = Round1000(Mathf.Max(2000000f, n * 30000f));
            long lawyer = Round1000(Mathf.Max(1500000f, AvgDailyIncome() * 0.5f));
            var ev = new GameEvent("Platform Çalışanları Yasası",
                "AB'nin platform çalışması direktifi Macaristan'da yürürlüğe giriyor: uygulama ve algoritmayla yönetilen kuryeler " +
                "artık çalışan sayılabilir. Şirketin kurye modelini seçmesi gerekiyor. Bu karar kalıcıdır.\n\n" +
                $"• Çalışan statüsü: kurye hakedişlerinin %28'i kadar işveren maliyeti + kurye başına günde {Ft(EmployeeDailyFixed)} " +
                $"(şu an {n} kurye → günde ~{Ft(daily)}). " +
                "Moral artar, rakip kuryelerini ayartamaz, grev ihtimali düşer.\n" +
                $"• Serbest çalışan kalsın: her ay {Ft(monthly)} hukuki uyum gideri ve arada denetim cezası riski.");
            ev.Add("Kuryeleri çalışan statüsüne al", () =>
            {
                lawStage = 2; employees = true;
                AddMorale(0.15f);
                EventManager.I.Notify("Kuryelerin artık çalışan statüsünde. Günlük işveren maliyeti giderlerde 'Personel maaşları' altında.");
            });
            ev.Add($"Serbest çalışan kalsın (her ay −{Ft(monthly)})", () =>
            {
                lawStage = 2; employees = false; lawMonthlyFee = monthly;
                AddMorale(-0.04f);
            });
            if (lawStage == 0)
                ev.Add($"Avukat tut, kararı 90 gün ertele (−{Ft(lawyer)})", () =>
                {
                    EconomyManager.I.Spend(lawyer, "Platform yasası: hukuk danışmanlığı", ExpenseCategory.Diger);
                    lawStage = 1; lawDecisionDay = Day + 90;
                });
            EventManager.I.Push(ev);
        }

        /// <summary>Hata ayıklama raporu ve Rakip penceresi için.</summary>
        public string StatusText
        {
            get
            {
                var parts = new List<string>();
                foreach (var e in Effects) parts.Add($"{e.title} ({DaysLeft(e)} gün): {EffectText(e)}");
                if (OrdersBlocked) parts.Add($"Uygulama çökmüş: {OutageMinutesLeft:0} dk daha sipariş yok");
                parts.Add(employees ? "Kurye modeli: çalışan statüsü" :
                          lawStage == 2 ? $"Kurye modeli: serbest çalışan (aylık uyum {Ft(lawMonthlyFee)})" :
                          lawStage == 1 ? $"Platform yasası ertelendi ({GameClock.I.DateOf(lawDecisionDay)})" : "Kurye modeli: serbest çalışan");
                parts.Add(serverUpgraded ? "Sunucu: yedekli" : "Sunucu: tek");
                parts.Add($"Sonraki kriz en erken: {GameClock.I.DateOf(nextCrisisDay)}");
                return string.Join("\n", parts);
            }
        }
    }
}
