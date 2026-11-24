using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Türkçe ek yardımcıları (rakip adlarında ünlü uyumu: TurboDrop'un, Flash Eats'in, Panda Courier'a...).</summary>
    public static class TurkceEk
    {
        static char SonUnlu(string s)
        {
            for (int i = s.Length - 1; i >= 0; i--)
            {
                char c = char.ToLowerInvariant(s[i]);
                switch (c)
                {
                    case 'a': case 'á': case 'ı': return 'a';
                    case 'e': case 'é': case 'i': case 'í': return 'e';
                    case 'o': case 'ó': case 'u': case 'ú': return 'o';
                    case 'ö': case 'ő': case 'ü': case 'ű': return 'ö';
                }
            }
            return 'e';
        }

        static bool UnluyleBiter(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            return "aáeéıiíoóöőuúüű".IndexOf(char.ToLowerInvariant(s[s.Length - 1])) >= 0;
        }

        /// <summary>Tamlayan eki: "TurboDrop'un", "Flash Eats'in".</summary>
        public static string In(string ad)
        {
            char u = SonUnlu(ad);
            string e = u == 'a' ? "ın" : u == 'e' ? "in" : u == 'o' ? "un" : "ün";
            return ad + "'" + (UnluyleBiter(ad) ? "n" : "") + e;
        }

        /// <summary>Yönelme eki: "TurboDrop'a", "Flash Eats'e".</summary>
        public static string A(string ad)
        {
            char u = SonUnlu(ad);
            string e = u == 'a' || u == 'o' ? "a" : "e";
            return ad + "'" + (UnluyleBiter(ad) ? "y" : "") + e;
        }
    }

    /// <summary>Bir rakip kurye platformu (yapay zekâ). Kişiliği büyüme hızını, fiyat savaşına ve kaliteye eğilimini belirler.</summary>
    [System.Serializable]
    public class Rakip
    {
        public string ad = "TurboDrop";
        public string kisa = "Turbo";
        public Color renk = new Color(0.62f, 0.32f, 0.85f);

        // Kişilik (sabit)
        public float buyume = 1f;           // kurye alma ve dükkan anlaşması hızı
        public float fiyatEgilimi = 0.5f;   // fiyat savaşı ve ucuz teklif eğilimi
        public float kaliteEgilimi = 0.5f;  // hizmet kalitesine yatırım eğilimi
        public float saldirganlik = 1f;     // pazar payın büyüdükçe ne kadar sert tepki verdiği

        // Durum
        public float reputation = 3.3f;
        public int couriers = 6;
        public long money = 1500000;
        public int deliveriesLastWeek, deliveriesThisWeek;
        public float aggression, quality;
        public int marketingDays, fundingCooldownUntil, fundingRounds;
        public int lastCourierRaidDay = -999;
        public int yatirimGunu = -999;      // son yatırım turunun günü (sağ üstte "yatırım aldı" kartı)
        public bool satinAlindi;            // oyuncu bu platformu satın aldı: faaliyeti yok, dükkanları devralındı

        // Hizmet (her gece güncellenir; kayda yazılmaz)
        [System.NonSerialized] public float load = 1f, avgMinutes = 38f, lateRate = 0.2f;

        public float EffectiveReputation => Mathf.Min(5f, reputation + (marketingDays > 0 ? 0.4f : 0f));
        public float Service => RivalCompany.ServiceFactor(avgMinutes, lateRate);
        public string MoodText => aggression > 0.66f ? "çok saldırgan" : aggression > 0.33f ? "saldırgan" : aggression > 0.1f ? "temkinli" : "sakin";
        public string Tanim => fiyatEgilimi >= 0.8f ? "ucuz ve hızlı büyüyen" : kaliteEgilimi >= 0.8f ? "kaliteli ve seçici" : "şehrin en eski platformu";
    }

    /// <summary>
    /// Rakip kurye platformları (tamamen yapay zekâ) ve dükkanlarla sözleşme ilişkileri.
    ///
    /// Rakipler (3 platform):
    ///  • TurboDrop: şehrin en eski ve en büyük platformu (oyuncunun eski işvereni). Dengeli.
    ///  • Panda Courier: az ama iyi kuryeli, yüksek itibarlı; kaliteye yatırım yapar, iyi dükkanlara göz diker.
    ///  • Flash Eats: ucuz ve hızlı büyüyen yeni girişim; sık fiyat savaşı açar, itibarı düşük.
    ///  • Her birinin kendi itibarı, kurye sayısı, parası ve sözleşmeli dükkanları var; her hafta büyümeye çalışırlar.
    ///  • Bir dükkanla birden çok platform çalışabilir: müşteriler itibar, kurye sayısı ve hıza göre paylaşılır.
    ///    Münhasır dükkanlarda rakip yoktur.
    ///  • Sözleşme yenilemelerinde dükkanlarına teklif verirler, ara sıra fiyat kırarlar, moralsiz kuryeleri ayartırlar.
    ///
    /// Dükkanlar:
    ///  • Memnuniyet (0–100): zamanında teslimat artırır; gecikme, soğuk yemek, iptal ve şikayetler düşürür.
    ///  • Sözleşme 6 ayda bir yenilenir. Memnuniyet düşükse bir rakip araya girer.
    ///  • Yeniden pazarlık (ayda bir): komisyonu +2 puan artırmayı dene; başarısızsa dükkan küser.
    ///  • Hacim: ayda 400+ sipariş veren dükkan bazen komisyon indirimi ister.
    ///  • Münhasırlık: dükkan sadece seninle çalışır: bütün rakipler çıkar, siparişler artar (+%15), komisyon −3 puan.
    /// </summary>
    public partial class RivalCompany : MonoBehaviour
    {
        public static RivalCompany I { get; private set; }

        /// <summary>Şehirdeki rakip platformlar (sıra sabit: dükkanların rakip maskesindeki bit numarası).</summary>
        public readonly List<Rakip> Rakipler = new List<Rakip>();

        static List<Rakip> Varsayilanlar() => new List<Rakip>
        {
            new Rakip { ad = "TurboDrop", kisa = "Turbo", renk = new Color(0.62f, 0.32f, 0.85f),
                        buyume = 1f, fiyatEgilimi = 0.5f, kaliteEgilimi = 0.5f, saldirganlik = 1f,
                        reputation = 3.4f, couriers = 7, money = 1500000 },
            new Rakip { ad = "Panda Courier", kisa = "Panda", renk = new Color(0.93f, 0.27f, 0.55f),
                        buyume = 0.7f, fiyatEgilimi = 0.2f, kaliteEgilimi = 0.9f, saldirganlik = 0.75f,
                        reputation = 3.8f, couriers = 4, money = 1200000, quality = 0.15f },
            new Rakip { ad = "Flash Eats", kisa = "Flash", renk = new Color(0.96f, 0.76f, 0.12f),
                        buyume = 1.35f, fiyatEgilimi = 1f, kaliteEgilimi = 0.2f, saldirganlik = 1.25f,
                        reputation = 3.0f, couriers = 3, money = 900000 },
        };
        static readonly int[] BaslangicDukkan = { 3, 2, 1 };

        [Header("Fiyat savaşı (aynı anda bir tane)")]
        public int priceWarDays;            // fiyat savaşının kalan günü (warDistrict bölgesinde)
        public int warDistrict;             // fiyat savaşı bölgesi (1–5, 0 = yok)
        public float warFactor = 0.75f;     // savaşta o bölgedeki talep çarpanın
        public int warOwner;                // savaşı açan rakip

        // Hizmet kalitesi (her gece güncellenir; kayda yazılmaz, yüklemede yeniden hesaplanır)
        [System.NonSerialized] public float myAvgMinutes = 40f, myLateRate = 0.25f;
        [System.NonSerialized] bool serviceReady;
        public readonly List<float> shareHistory = new List<float>();   // haftalık pazar payın (son 12 hafta)

        /// <summary>Bugün dükkan başına senin teslimatların (rakiplerin teslimatını tahmin etmek için).</summary>
        readonly Dictionary<Shop, int> myToday = new Dictionary<Shop, int>();

        [Header("Sözleşmeler")]
        public int contractDays = 182;          // 6 ay
        public int volumeTier = 400;            // aylık bu kadar siparişin üstünde dükkan indirim isteyebilir
        public float maxNegotiatedBonus = 0.06f;

        int myDeliveriesThisWeek;
        public int myDeliveriesLastWeek;

        void Awake() { I = this; }

        static int Day => GameClock.I.Day;
        static string Pct(float v) => $"%{v * 100f:0.#}";

        // ================================================================ toplu bilgiler (eski tek rakip arayüzü)

        /// <summary>En çok kuryesi olan rakip.</summary>
        public Rakip Lider
        {
            get
            {
                Rakip best = null;
                foreach (var r in Rakipler) if (!r.satinAlindi && (best == null || r.couriers > best.couriers)) best = r;
                return best ?? new Rakip();
            }
        }

        public string companyName => Lider.ad;
        public int couriers { get { int n = 0; foreach (var r in Rakipler) n += r.couriers; return n; } }
        public int deliveriesLastWeek { get { int n = 0; foreach (var r in Rakipler) n += r.deliveriesLastWeek; return n; } }
        public float aggression { get { float a = 0f; foreach (var r in Rakipler) a = Mathf.Max(a, r.aggression); return a; } }
        public string MoodText => Lider.MoodText;

        public Rakip RakipAt(int i) => i >= 0 && i < Rakipler.Count ? Rakipler[i] : null;
        public static bool Var(Shop s, int i) => (s.rivalMask & (1 << i)) != 0;
        public static void Ekle(Shop s, int i) => s.rivalMask |= 1 << i;

        /// <summary>Dükkanla çalışan rakiplerin adları ("TurboDrop, Flash Eats").</summary>
        public string DukkanRakipleri(Shop s)
        {
            var l = new List<string>();
            for (int i = 0; i < Rakipler.Count; i++) if (Var(s, i)) l.Add(Rakipler[i].ad);
            return string.Join(", ", l);
        }

        public int DukkanRakipSayisi(Shop s)
        {
            int n = 0;
            for (int i = 0; i < Rakipler.Count; i++) if (Var(s, i)) n++;
            return n;
        }

        public int ShopCountOf(int i)
        {
            int n = 0;
            foreach (var s in CityMap.I.Shops) if (Var(s, i)) n++;
            return n;
        }

        public int RivalShopCount
        {
            get { int n = 0; foreach (var s in CityMap.I.Shops) if (s.rivalContracted) n++; return n; }
        }

        // ================================================================ kurulum

        public void Init()
        {
            GameClock.I.OnNewDay += OnNewDay;
            GameClock.I.OnNewMonth += OnNewMonth;

            Rakipler.Clear();
            Rakipler.AddRange(Varsayilanlar());
            Tehditler.Clear();
            sonTehditSeviyesi = 0;
            for (int b = 0; b < bolgeSadakat.Length; b++) bolgeSadakat[b] = 1f;
            savasGormezden = false;
            sonGeriKazanmaGunu = -999;

            var shops = CityMap.I.Shops;
            foreach (var s in shops)
            {
                s.rivalMask = 0;
                if (!s.contracted) continue;
                s.contractStartDay = Day;
                s.contractEndDay = Day + contractDays;
            }
            BaslangicDukkanlari(0);
        }

        /// <summary>Rakipler (from numarasından itibaren) senin sözleşmesiz olduğun birkaç dükkanla başlar.</summary>
        void BaslangicDukkanlari(int from)
        {
            var bos = new List<Shop>();
            foreach (var s in CityMap.I.Shops) if (!s.contracted && !s.own && !s.exclusive) bos.Add(s);
            for (int i = from; i < Rakipler.Count; i++)
            {
                int want = i < BaslangicDukkan.Length ? BaslangicDukkan[i] : 1, n = 0;
                // Önce kimsenin olmayan dükkanlar, sonra başka rakiple paylaşılanlar
                for (int pass = 0; pass < 2 && n < want; pass++)
                    for (int k = 0; k < 30 && n < want && bos.Count > 0; k++)
                    {
                        var s = bos[Random.Range(0, bos.Count)];
                        if (Var(s, i) || (pass == 0 && s.rivalMask != 0)) continue;
                        if (Random.value < 0.7f) { Ekle(s, i); n++; }
                    }
            }
        }

        // ================================================================ kayıt

        public RivalSave CaptureSave()
        {
            var s = new RivalSave
            {
                priceWarDays = priceWarDays, warDistrict = warDistrict, warFactor = warFactor, warOwner = warOwner,
                myDeliveriesThisWeek = myDeliveriesThisWeek, myDeliveriesLastWeek = myDeliveriesLastWeek,
                shareHistory = new List<float>(shareHistory), activeVersion = 2, sonSatisTeklifiGunu = sonSatisTeklifiGunu,
                tehditSeviyesi = sonTehditSeviyesi
            };
            foreach (var t in Tehditler) s.tehditler.Add(JsonUtility.FromJson<RakipTehdit>(JsonUtility.ToJson(t)));
            foreach (var r in Rakipler) s.rakipler.Add(JsonUtility.FromJson<Rakip>(JsonUtility.ToJson(r)));
            SadakatKaydet(s);
            // Eski sürümlerin okuyabilmesi için lider rakip eski alanlara da yazılır
            var l = Lider;
            s.reputation = l.reputation; s.couriers = l.couriers; s.money = l.money; s.deliveriesLastWeek = l.deliveriesLastWeek;
            s.deliveriesThisWeek = l.deliveriesThisWeek; s.aggression = l.aggression; s.quality = l.quality;
            s.marketingDays = l.marketingDays; s.fundingCooldownUntil = l.fundingCooldownUntil; s.fundingRounds = l.fundingRounds;
            s.lastCourierRaidDay = l.lastCourierRaidDay;
            return s;
        }

        /// <summary>Init'ten sonra, dükkanların sözleşme durumu (rakip maskesi dahil) geri konduktan sonra çağrılır.</summary>
        public void RestoreSave(RivalSave s)
        {
            priceWarDays = s.priceWarDays;
            myDeliveriesThisWeek = s.myDeliveriesThisWeek; myDeliveriesLastWeek = s.myDeliveriesLastWeek;
            shareHistory.Clear();
            if (s.shareHistory != null) shareHistory.AddRange(s.shareHistory);
            warDistrict = s.warDistrict; warFactor = s.warFactor > 0f ? s.warFactor : 0.75f; warOwner = s.warOwner;
            sonSatisTeklifiGunu = s.sonSatisTeklifiGunu;
            sonTehditSeviyesi = s.tehditSeviyesi;
            Tehditler.Clear();
            if (s.tehditler != null) Tehditler.AddRange(s.tehditler);
            SadakatYukle(s);

            Rakipler.Clear();
            var defs = Varsayilanlar();
            if (s.activeVersion >= 2 && s.rakipler != null && s.rakipler.Count > 0)
            {
                Rakipler.AddRange(s.rakipler);
                // Kayıtta olmayan yeni rakipler (ileride eklenirse) varsayılanla girer
                for (int i = Rakipler.Count; i < defs.Count; i++) Rakipler.Add(defs[i]);
                if (Rakipler.Count > s.rakipler.Count) BaslangicDukkanlari(s.rakipler.Count);
            }
            else
            {
                // Eski kayıt (tek rakip): onun bilgileri TurboDrop'a, diğer iki platform şehre yeni giriyor
                Rakipler.AddRange(defs);
                var t = Rakipler[0];
                t.reputation = s.reputation; t.couriers = s.couriers; t.money = s.money;
                t.deliveriesLastWeek = s.deliveriesLastWeek; t.deliveriesThisWeek = s.deliveriesThisWeek;
                if (s.activeVersion >= 1)
                {
                    t.aggression = s.aggression; t.quality = s.quality; t.marketingDays = s.marketingDays;
                    t.fundingCooldownUntil = s.fundingCooldownUntil; t.fundingRounds = s.fundingRounds; t.lastCourierRaidDay = s.lastCourierRaidDay;
                }
                else
                {
                    warDistrict = priceWarDays > 0 ? 1 : 0;
                    t.fundingCooldownUntil = Day + 14;
                }
                warOwner = 0;
                BaslangicDukkanlari(1);
            }
            if (warOwner < 0 || warOwner >= Rakipler.Count) warOwner = 0;
        }

        // ================================================================ sipariş paylaşımı

        /// <summary>Müşterinin gözündeki çekicilik: itibarın karesi × filonun yeterliliği (40 kuryede doyar).</summary>
        static float Score(float rep, int courierCount) =>
            rep * rep * (0.4f + 0.6f * Mathf.Min(1f, courierCount / 40f) + 0.15f * Mathf.Clamp01((courierCount - 40) / 120f));

        float RakipSkoru(Rakip r) => Score(r.EffectiveReputation, r.couriers) * r.Service;

        /// <summary>
        /// Müşterinin gözünde teslimat hızı ve dakikliği: 40 dk ortalama = 1. Her dakika hızlı +%2, yavaş −%2;
        /// geç kalan siparişler (oranın yarısı kadar) düşürür. Hızlı ve dakik olan rakipten müşteri kapar.
        /// </summary>
        public static float ServiceFactor(float avgMinutes, float lateRate) =>
            Mathf.Clamp(1f + (40f - avgMinutes) * 0.02f, 0.7f, 1.35f) * (1f - 0.5f * Mathf.Clamp01(lateRate));

        public float MyService { get { EnsureService(); return ServiceFactor(myAvgMinutes, myLateRate); } }

        /// <summary>Son 7 günün ortalama teslimat süresi ve geç kalma oranı; rakiplerin süresi iş yüküne göre tahmin.</summary>
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
            // Rakipler: kurye başına iş yükü arttıkça yavaşlar, kalite yatırımı hızlandırır
            foreach (var r in Rakipler)
            {
                r.avgMinutes = 37f + Mathf.Max(0f, r.load - 0.8f) * 25f - r.quality * 4f;
                r.lateRate = Mathf.Clamp(0.15f + Mathf.Max(0f, r.load - 0.8f) * 0.3f - r.quality * 0.05f, 0.08f, 0.6f);
            }
        }

        void EnsureService() { if (!serviceReady) UpdateService(); }

        /// <summary>Hızlı teslimat pazarı da büyütür: insanlar hızlı gelen yemeği daha sık sipariş eder (−%10 … +%15).</summary>
        public float ServiceDemandFactor
        {
            get { EnsureService(); return Mathf.Clamp(1f + (40f - myAvgMinutes) * 0.01f - (myLateRate - 0.25f) * 0.2f, 0.9f, 1.15f); }
        }

        /// <summary>Paylaşılan dükkanda müşterilerin senin uygulamanı seçme oranı (rakip sayısına göre 0,12–0,9).</summary>
        public float MyFraction(Shop s)
        {
            if (!s.rivalContracted || s.exclusive) return 1f;
            EnsureService();
            float mine = Score(CompanyProfile.Reputation, CourierManager.I.Couriers.Count) * MyService;
            float theirs = 0f;
            for (int i = 0; i < Rakipler.Count; i++) if (Var(s, i)) theirs += RakipSkoru(Rakipler[i]);
            return Mathf.Clamp(mine / Mathf.Max(0.01f, mine + theirs), 0.12f, 0.9f);
        }

        /// <summary>Fiyat savaşı bölgesindeyse senin talep çarpanın.</summary>
        public float WarFactor(Shop s)
        {
            int bolge = CityMap.RegionOf(s.tile);
            float savas = priceWarDays > 0 && warDistrict > 0 && bolge == warDistrict ? warFactor : 1f;
            return savas * BolgeSadakati(bolge);   // görmezden gelinen eski savaşların kalıcı izi
        }

        /// <summary>Senin bu dükkandaki sipariş payın (talep çarpanı).</summary>
        public float ShareFor(Shop s)
        {
            if (s.exclusive) return 1.15f * WarFactor(s);   // dükkan sadece seni tanıtıyor
            if (!s.rivalContracted) return WarFactor(s);
            // Birden çok uygulamada görünen dükkanın toplam müşterisi biraz daha fazla
            float bonus = 1.25f + 0.1f * (DukkanRakipSayisi(s) - 1);
            return Mathf.Min(1.1f, MyFraction(s) * bonus) * WarFactor(s);
        }

        public float PriceSensitivity => priceWarDays > 0 ? 0.95f : 1f;

        int ToplamRakipTeslimati { get { int n = 0; foreach (var r in Rakipler) n += r.deliveriesLastWeek; return n; } }

        /// <summary>Geçen haftanın teslimatlarına göre pazar payın.</summary>
        public float MarketShare => myDeliveriesLastWeek + ToplamRakipTeslimati == 0 ? 1f / (1 + Mathf.Max(1, Rakipler.Count))
            : myDeliveriesLastWeek / (float)(myDeliveriesLastWeek + ToplamRakipTeslimati);

        /// <summary>Geçen hafta bir rakibin pazar payı.</summary>
        public float PayiOf(Rakip r)
        {
            int total = myDeliveriesLastWeek + ToplamRakipTeslimati;
            return total == 0 ? 0f : r.deliveriesLastWeek / (float)total;
        }

        /// <summary>Geçen hafta her rakipten daha çok teslimat yaptın mı.</summary>
        public bool Lideriz
        {
            get
            {
                if (myDeliveriesLastWeek <= 0) return false;
                foreach (var r in Rakipler) if (r.deliveriesLastWeek >= myDeliveriesLastWeek) return false;
                return true;
            }
        }

        /// <summary>Pazar payın bu eşiğin üstüne çıktıkça rakipler saldırganlaşır (3 rakipte %35).</summary>
        float SaldirganlikEsigi => Mathf.Clamp(0.5f - 0.05f * Rakipler.Count, 0.3f, 0.45f);

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
            if (o.kargo) return;   // kargo paketi yemek/market pazar payına sayılmaz
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
                EventManager.I.Notify($"Pazarlık başarılı: {s.Name} komisyonu 2 puan artırmayı kabul etti. Yeni oran: {Pct(s.Commission)}.");
            }
            else
            {
                AddSatisfaction(s, -12f);
                EventManager.I.Notify($"Pazarlık başarısız: {s.Name} teklifini reddetti ve biraz küstü (memnuniyet {s.satisfaction:0}). " +
                                      "Bir ay sonra yeniden deneyebilirsin; zamanında teslimat memnuniyeti artırır.");
            }
        }

        /// <summary>Münhasırlık kabul ihtimali: memnuniyete bağlı; dükkanla çalışan her rakip biraz zorlaştırır.</summary>
        public float ExclusiveChance(Shop s) =>
            Mathf.Clamp((s.satisfaction - 40f) / 50f - 0.05f * Mathf.Max(0, DukkanRakipSayisi(s) - 1) + DukkanSadakati.MunhasirlikEki(s), 0f, 0.9f);

        /// <summary>Primli münhasırlık: tek seferlik prim kabul ihtimalini 30 puan artırır (en fazla %95).</summary>
        public float ExclusiveChance(Shop s, bool primli) =>
            primli ? Mathf.Min(0.95f, ExclusiveChance(s) + DukkanSadakati.PrimEki) : ExclusiveChance(s);

        /// <summary>Primli teklif yapılabilir mi (serbest kasa primi karşılıyor mu).</summary>
        public bool CanOfferExclusivePrim(Shop s) => CanOfferExclusive(s) && EconomyManager.I.FreeCash >= DukkanSadakati.Prim(s);

        public bool CanOfferExclusive(Shop s) => s.contracted && !s.exclusive && Day >= s.negotiateCooldownUntil;

        /// <summary>Münhasırlık: bütün rakipler çıkar, siparişler artar, komisyon 3 puan düşer, sözleşme yeniden 6 ay.</summary>
        public void OfferExclusive(Shop s, bool primli = false)
        {
            if (!CanOfferExclusive(s)) return;
            if (primli && !CanOfferExclusivePrim(s)) primli = false;
            long prim = primli ? DukkanSadakati.Prim(s) : 0;
            s.negotiateCooldownUntil = Day + 30;
            if (Random.value < ExclusiveChance(s, primli))
            {
                if (primli && !EconomyManager.I.TrySpendYatirim(prim, $"Münhasırlık primi: {s.Name}", ExpenseCategory.Sozlesme)) primli = false;
                if (primli) Telemetry.Add("Münhasırlık: primli kabul");
                string eski = DukkanRakipleri(s);
                MakeExclusive(s);
                s.commissionBonus -= 0.03f;
                AddSatisfaction(s, 8f);
                EventManager.I.Push(new GameEvent("Münhasır sözleşme!",
                    $"{s.Name} artık sadece seninle çalışacak" + (eski.Length > 0 ? $" ({eski} ile anlaşmasını bitirdi)" : "") +
                    $". Siparişler +%15, komisyon {Pct(s.Commission)}. Sözleşme {GameClock.I.DateOf(s.contractEndDay)} tarihine kadar." +
                    (primli ? $" Münhasırlık primi: {GameData.Ft(prim)}." : "")));
            }
            else
            {
                AddSatisfaction(s, -3f);
                EventManager.I.Notify($"{s.Name} münhasırlık teklifini şimdilik kabul etmedi (memnuniyet {s.satisfaction:0})" +
                                      (primli ? "; prim ödenmedi." : "."));
            }
        }

        void MakeExclusive(Shop s)
        {
            s.exclusive = true;
            s.rivalMask = 0;
            Renew(s);
        }

        // ================================================================ günlük / haftalık / aylık

        void OnNewDay()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            var shops = CityMap.I.Shops;
            EnsureService();

            // Rakiplerin teslimatları (tahmini): paylaşılan dükkanda senin teslimatından müşteri payına göre,
            // sadece rakiplerle çalışan dükkanda senin dükkan başı ortalamandan. Dükkandaki rakipler kendi skorlarına göre bölüşür.
            int myTotal = 0; float myWeight = 0f;
            foreach (var kv in myToday) { myTotal += kv.Value; myWeight += kv.Key.def.weight; }
            float perWeight = myWeight > 0f ? myTotal / myWeight : 15f;
            var est = new float[Rakipler.Count];
            foreach (var s in shops)
            {
                if (!s.rivalContracted || s.own) continue;
                float toplam;
                if (s.contracted)
                {
                    myToday.TryGetValue(s, out int mine);
                    float f = MyFraction(s);
                    toplam = mine * (1f - f) / Mathf.Max(0.12f, f);
                }
                else toplam = perWeight * s.def.weight * 0.9f;
                float skor = 0f;
                for (int i = 0; i < Rakipler.Count; i++) if (Var(s, i)) skor += RakipSkoru(Rakipler[i]);
                for (int i = 0; i < Rakipler.Count; i++)
                    if (Var(s, i)) est[i] += toplam * RakipSkoru(Rakipler[i]) / Mathf.Max(0.01f, skor);
            }
            myToday.Clear();

            for (int i = 0; i < Rakipler.Count; i++)
            {
                var r = Rakipler[i];
                if (r.satinAlindi) continue;   // satın alınan platform: faaliyeti yok
                int d = Mathf.Min(Mathf.RoundToInt(est[i] * Random.Range(0.9f, 1.1f)), r.couriers * 22);
                r.deliveriesThisWeek += d;
                // Ucuz platform paket başına daha az kazanır
                long perDelivery = (long)Mathf.Lerp(320f, 230f, r.fiyatEgilimi);
                r.money += d * perDelivery - r.couriers * 1500L - 4000;
                r.load = d / Mathf.Max(1f, r.couriers * 15f);           // kurye başına iş yükü
                float target = Mathf.Clamp(3.5f + 0.9f * r.quality + 0.3f * (r.kaliteEgilimi - 0.5f) - Mathf.Max(0f, r.load - 1f) * 0.8f, 2.3f, 4.9f);
                r.reputation = Mathf.Clamp(r.reputation + (target - r.reputation) * 0.05f + Random.Range(-0.04f, 0.04f), 1.5f, 5f);
                if (r.marketingDays > 0) r.marketingDays--;
            }
            UpdateService();

            if (priceWarDays > 0 && --priceWarDays == 0)
            {
                var o = RakipAt(warOwner);
                EventManager.I.Notify($"{(o != null ? TurkceEk.In(o.ad) : "Rakibin")} {CityMap.DistrictNames[Mathf.Clamp(warDistrict, 0, 5)]} fiyat savaşı bitti.");
                SavasBittiSadakat(warDistrict, TehditSeviyesi >= 2);
                warDistrict = 0;
            }
            SadakatGunluk();

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

                if (s.satisfaction < 15f && Random.value < 0.2f) { LoseShop(s, "memnuniyetsizlik yüzünden sözleşmeyi bozdu", TeklifVeren(s)); continue; }
                if (Day >= s.contractEndDay) Renewal(s);
            }

            TehditleriIsle();   // önceden haber verilen rakip hamleleri (RakipTehditleri.cs)
            if (GameClock.I.DayOfWeek == 0) Weekly();
        }

        /// <summary>Haftayı en son hangi gün kapattı (haftalık rapor, sırası ne olursa olsun güncel satırı yazsın diye).</summary>
        public int HaftaKapanisGunu { get; private set; } = -1;

        /// <summary>Ağırlıklı rastgele rakip seçimi (ağırlık: kurye sayısı ve saldırganlık; isteğe bağlı kişilik çarpanı).</summary>
        int Sec(System.Func<Rakip, float> carpan = null)
        {
            float total = 0f;
            var w = new float[Rakipler.Count];
            for (int i = 0; i < Rakipler.Count; i++)
            {
                var r = Rakipler[i];
                w[i] = r.satinAlindi ? 0f : (4f + r.couriers) * (0.5f + r.aggression) * (carpan != null ? carpan(r) : 1f);
                total += w[i];
            }
            float x = Random.value * total;
            for (int i = 0; i < w.Length; i++) { if (x < w[i]) return i; x -= w[i]; }
            return Rakipler.Count - 1;
        }

        /// <summary>Bir dükkana teklif verecek rakip: dükkanla zaten çalışanlardan biri, yoksa herhangi biri.</summary>
        int TeklifVeren(Shop s)
        {
            var l = new List<int>();
            for (int i = 0; i < Rakipler.Count; i++) if (Var(s, i)) l.Add(i);
            if (l.Count > 0 && Random.value < 0.7f) return l[Random.Range(0, l.Count)];
            return Sec();
        }

        void Weekly()
        {
            HaftaKapanisGunu = Day;
            foreach (var r in Rakipler) { r.deliveriesLastWeek = r.deliveriesThisWeek; r.deliveriesThisWeek = 0; }
            myDeliveriesLastWeek = myDeliveriesThisWeek;
            myDeliveriesThisWeek = 0;
            shareHistory.Add(MarketShare);
            while (shareHistory.Count > 12) shareHistory.RemoveAt(0);
            if (EconomyManager.I != null) EconomyManager.I.RakipSatiriniYaz(ReportLine);   // haftalık rapor bu sayılarla

            float esik = SaldirganlikEsigi;
            int seviye = TehditSeviyesi;
            TehditSeviyesiniDuyur(seviye);
            SatisTeklifi();   // zayıflayan bir platform satılık olabilir
            for (int i = 0; i < Rakipler.Count; i++)
            {
                var r = Rakipler[i];
                if (r.satinAlindi) continue;
                // Saldırganlık: pazar payın eşiğin üstüne çıktıkça rakip daha çok hamle yapar, yatırım arar
                // Takvime değil pazar payına bağlı: %35'ten (3 rakipte) sonra tetikte, %45'ten sonra saldırıda (taban saldırganlık)
                float targetAgg = Mathf.Clamp01((MarketShare - esik) * 2.5f * r.saldirganlik);
                float taban = seviye >= 2 ? 0.7f : seviye == 1 ? 0.35f : 0f;
                targetAgg = Mathf.Max(targetAgg, taban * r.saldirganlik);
                r.aggression = Mathf.Lerp(r.aggression, targetAgg, 0.5f);

                // Yatırım turu: pazar payın eşiği geçince rakip yatırımcı arar (saldırıdayken daha sık ve daha erken)
                if (seviye >= 1 && r.aggression > 0.3f && Day >= r.fundingCooldownUntil && myDeliveriesLastWeek >= 600 &&
                    Random.value < (seviye >= 2 ? 0.5f : 0.2f)) FundingRound(r);

                // Büyüme: para varsa kurye alır (saldırganken ve hızlı büyüyen platformda daha hızlı), kaliteye yatırım yapar
                int maxCouriers = Mathf.Min(160, Mathf.RoundToInt((30 + 30 * r.fundingRounds) * (0.7f + 0.4f * r.buyume)));
                if (r.money > 1000000 && r.couriers < maxCouriers)
                {
                    int add = Mathf.Min((int)((r.money - 1000000) / 400000), Mathf.RoundToInt((r.aggression > 0.5f ? 8 : 3) * r.buyume));
                    add = Mathf.Max(1, Mathf.Min(add, maxCouriers - r.couriers));
                    r.couriers += add; r.money -= add * 400000L;
                }
                else if (r.money < 0 && r.couriers > 3) r.couriers -= Mathf.Min(r.couriers - 3, 2);
                long kaliteEsigi = (long)Mathf.Lerp(8000000f, 3000000f, r.kaliteEgilimi);
                if (r.money > kaliteEsigi && r.quality < 1f) { r.money -= 3000000; r.quality = Mathf.Min(1f, r.quality + 0.05f); }

                // Yeni dükkan anlaşması: pazar payın büyüdükçe sıklaşır (sakin %25, tetikte %45, saldırıda %70 × büyüme hızı;
                // yatırım almış rakip saldırıdayken haftada iki). Tetikteyken ve saldırıdayken çoğunlukla senin en çok sipariş
                // aldığın (münhasır olmayan) dükkanlarına girer: payını ancak senin dükkanlarında sipariş paylaşarak alabilir.
                // 14.11 (e1) testinde rakipler 60 günde sadece 4 dükkanla anlaştı, pay %44'ten %65'e çıktı.
                float anlasmaSansi = (seviye >= 2 ? 0.7f : seviye == 1 ? 0.45f : 0.25f) * r.buyume;
                int anlasma = Random.value < anlasmaSansi ? 1 : 0;
                if (seviye >= 2 && r.fundingRounds > 0 && Random.value < 0.5f) anlasma++;
                for (int a = 0; a < anlasma; a++) YeniDukkanAnlasmasi(i, seviye);
            }

            // Rakiplerin haftalık hamlesi: rakip sayısı arttıkça biraz daha sık, hamleyi büyük ve saldırgan olan yapar
            float maxAgg = aggression;
            // Hamle sıklığı pazar payına bağlı (sakin ×0,5, tetikte ×1, saldırıda ×1,4 ve ikinci bir hamle şansı).
            // İlk iki hafta hamle yok: pay o zaman birkaç dükkanın siparişinden hesaplanıyor, çok oynak.
            float moveChance = (0.2f + 0.55f * maxAgg) * (1f + 0.2f * (Rakipler.Count - 1)) * (seviye >= 2 ? 1.4f : seviye == 1 ? 1f : 0.5f);
            if (AktifRakip == 0) return;   // bütün rakipler satın alındı: hamle ve kurye ayartma yok
            if (GameClock.I.CompanyDay >= 14 && Random.value < Mathf.Min(0.95f, moveChance))
            {
                int ilk = Sec();
                RivalMove(ilk);
                if (seviye >= 2 && AktifRakip > 1 && Random.value < 0.5f)
                {
                    int ikinci = Sec();
                    if (ikinci != ilk) RivalMove(ikinci);
                }
            }

            // Moralsiz kuryeyi ayartma: rakipler tetikteyken ve saldırıdayken moral eşiği yükselir, saldırıda haftada iki kurye.
            // Çalışan statüsündeki kuryeler gitmez; sadakat programında kıdemli kurye kalır; akademi ayartmayı zorlaştırır.
            if (!CrisisManager.Employees)
            {
                float esikMoral = seviye >= 2 ? 0.48f : seviye == 1 ? 0.42f : 0.35f;
                int enFazla = seviye >= 2 ? 2 : 1, giden = 0;
                float akademi = UpgradeManager.I != null ? UpgradeManager.I.AkademiAyartmaCarpani : 1f;
                foreach (var c in CourierManager.I.Couriers.ToArray())
                {
                    if (giden >= enFazla) break;
                    if (c.morale >= esikMoral || c.OrderCount > 0) continue;
                    if (CrisisManager.CalisanMi(c)) continue;   // karma model: çalışan statüsündeki kurye gitmez
                    if (UpgradeManager.I != null && UpgradeManager.I.LoyaltyProgram && c.level >= 3) continue;   // sadakat programı: kıdemli kurye kalır
                    float sans = (0.2f + 0.05f * (Rakipler.Count - 1) + (esikMoral - c.morale) * 0.8f) * akademi;
                    if (Random.value < sans)
                    {
                        var r = Rakipler[Sec()];
                        r.couriers++;
                        CourierManager.I.PoachToRival(c, r.ad);
                        giden++;
                    }
                }
            }
        }

        /// <summary>Rakip yeni bir dükkanla anlaşır (münhasır dükkanlar hariç). Senin dükkanınsa siparişler paylaşılmaya başlar.</summary>
        void YeniDukkanAnlasmasi(int i, int seviye)
        {
            var r = Rakipler[i];
            var cands = new List<Shop>();
            Shop s = null;
            if (seviye >= 1 && Random.value < 0.6f)
            {
                foreach (var d in CityMap.I.Shops) if (d.contracted && !d.own && !d.exclusive && !Var(d, i) && Random.value >= DukkanSadakati.RakipRedOrani(d)) cands.Add(d);
                cands.Sort((a, b) => (b.lastMonthOrders + b.monthOrders).CompareTo(a.lastMonthOrders + a.monthOrders));
                if (cands.Count > 0) s = cands[Random.Range(0, Mathf.Min(3, cands.Count))];
            }
            if (s == null)
            {
                cands.Clear();
                foreach (var d in CityMap.I.Shops) if (!Var(d, i) && !d.exclusive && !d.own && Random.value >= DukkanSadakati.RakipRedOrani(d)) cands.Add(d);
                if (cands.Count == 0) return;
                s = cands[Random.Range(0, cands.Count)];
            }
            Ekle(s, i);
            if (s.contracted)
            {
                Telemetry.Add("Rakip: senin dükkanınla anlaştı");
                // Sık olabildiği için oyunu durduran kart değil bildirim; Dükkanlar'da da yazar
                EventManager.I.Notify($"{r.ad}, {s.Name} ile de anlaştı: bu dükkanın siparişleri artık " +
                                      $"{(DukkanRakipSayisi(s) > 1 ? "rakiplerle" : "ikiniz arasında")} paylaşılıyor (payın ~%{MyFraction(s) * 100f:0}). " +
                                      "Münhasırlık önerirsen rakipler çıkar.");
            }
            else EventManager.I.Notify($"{r.ad}, {s.Name} ile anlaştı.");
        }

        // ================================================================ rakiplerin hamleleri

        static long Round1000(float v) => (long)Mathf.Round(v / 1000f) * 1000;

        void FundingRound(Rakip r)
        {
            long amount = Round1000(Random.Range(25000000f, 50000000f) * (1f + 0.3f * r.fundingRounds) * (0.8f + 0.4f * r.buyume));
            r.money += amount;
            r.fundingRounds++;
            r.quality = Mathf.Min(1f, r.quality + 0.2f);
            r.fundingCooldownUntil = Day + (TehditSeviyesi >= 2 ? 45 : 60);
            r.yatirimGunu = Day;
            Telemetry.Add("Rakip: yatırım turu");
            EventManager.I.Push(new GameEvent($"{r.ad} yatırım aldı",
                $"Rakip {r.ad}, pazar payını geri almak için yatırımcılardan {GameData.Ft(amount)} topladı. " +
                "Önümüzdeki haftalarda daha çok kurye alacak, hizmet kalitesini yükseltecek ve dükkanlarına, kuryelerine teklif yapacak.\n\n" +
                $"Pazar payın: %{MarketShare * 100f:0}. İtibarını yüksek tut, iyi dükkanlarla münhasırlık yap, kuryelerinin moralini koru."));
        }

        void RivalMove(int i)
        {
            var r = Rakipler[i];
            // Kişiliğe göre hamle: ucuz platform fiyat savaşı, kaliteli platform dükkan avı sever
            float wShop = 0.4f + 0.3f * r.kaliteEgilimi, wCourier = 0.25f, wWar = 0.15f + 0.45f * r.fiyatEgilimi, wAd = 0.2f;
            float x = Random.value * (wShop + wCourier + wWar + wAd);
            // Dükkan, kurye ve fiyat savaşı hamleleri önce duyulur (sağ üstte durum kartı), birkaç gün sonra gerçekleşir
            if (x < wShop && TehditHazirla(i, TehditTur.Dukkan)) return;
            x -= wShop;
            if (x < wCourier && TehditHazirla(i, TehditTur.Kurye)) return;
            x -= wCourier;
            if (x < wWar && priceWarDays == 0 && TehditHazirla(i, TehditTur.FiyatSavasi)) return;
            if (r.marketingDays == 0) MarketingBlitz(r);
        }

        /// <summary>
        /// Teklif yapılacak dükkan: en çok sipariş verenlerden biri. Saldırıdayken (pay %50+) münhasır dükkanlar da hedef;
        /// başka bir tehdidin hedefi olan dükkan seçilmez.
        /// </summary>
        Shop DukkanHedefi()
        {
            bool saldiri = TehditSeviyesi >= 2;
            var cands = new List<Shop>();
            foreach (var s in CityMap.I.Shops)
                if (s.contracted && !s.own && (!s.exclusive || saldiri) && CityMap.I.IsUnlocked(s.tile) && !HedefteMi(s)) cands.Add(s);
            if (cands.Count == 0) return null;
            cands.Sort((a, b) => (b.lastMonthOrders + b.monthOrders).CompareTo(a.lastMonthOrders + a.monthOrders));
            return cands[Random.Range(0, Mathf.Min(3, cands.Count))];
        }

        /// <summary>Sözleşme bitmeden daha düşük komisyon teklifi (önceden duyurulmuş dükkan tehdidi vadesinde).</summary>
        void ShopRaidOn(int ri, Shop shop)
        {
            var r = Rakipler[ri];
            if (!shop.contracted)
            {
                EventManager.I.Notify($"{r.ad}, {shop.Name} için teklifini geri çekti: dükkan zaten seninle çalışmıyor.");
                return;
            }
            bool saldiri = TehditSeviyesi >= 2;
            if (shop.exclusive && !saldiri)
            {
                EventManager.I.Notify($"{r.ad}, {shop.Name} münhasır olduğu için teklifini geri çekti.");
                return;
            }
            if (shop.exclusive) { MunhasirDukkanTeklifi(ri, shop); return; }
            float indirim = Mathf.Lerp(0.03f, 0.05f, r.fiyatEgilimi) + (saldiri ? 0.01f : 0f);
            float offer = Mathf.Max(0.05f, shop.Commission - indirim);
            // Saldırıdayken rakip daha cömert davranır; öncelikli hazırlık anlaşması olan dükkan daha sadık
            float keep = Mathf.Clamp(0.15f + shop.satisfaction / 100f * 0.8f - (saldiri ? 0.1f : 0f) + SadakatEki(shop), 0.15f, 0.95f);
            Telemetry.Add("Rakip: dükkan teklifi");
            var ev = new GameEvent($"{r.ad}, {shop.Name} dükkanına göz dikti",
                $"{r.ad} ({r.Tanim}) sözleşmen bitmeden {shop.Name} dükkanına {Pct(offer)} komisyonla teklif verdi " +
                $"(senin oranın {Pct(shop.Commission)}, memnuniyet {shop.satisfaction:0}/100, geçen ay {shop.lastMonthOrders} sipariş).");
            ev.Add($"Karşı teklif ver (→ {Pct(shop.Commission - 0.03f)}, sözleşme 6 ay uzar, memnuniyet +6)", () =>
            {
                shop.commissionBonus -= 0.03f; Renew(shop); AddSatisfaction(shop, 6f);
            });
            if (shop.satisfaction >= 50f)
            {
                float chance = Mathf.Clamp(ExclusiveChance(shop) + 0.15f, 0f, 0.95f);
                ev.Add($"Münhasırlık öner (→ {Pct(shop.Commission - 0.04f)}, bütün rakipler çıkar · kabul ihtimali %{chance * 100f:0})", () =>
                {
                    if (Random.value < chance)
                    {
                        shop.commissionBonus -= 0.04f; MakeExclusive(shop); AddSatisfaction(shop, 8f);
                        EventManager.I.Notify($"{shop.Name} münhasırlığı kabul etti: artık sadece seninle çalışıyor.");
                    }
                    else if (Random.value < keep) { AddSatisfaction(shop, -5f); EventManager.I.Notify($"{shop.Name} münhasırlığı reddetti ama seninle devam ediyor."); }
                    else LoseShop(shop, $"münhasırlığı reddetti ve {TurkceEk.In(r.ad)} teklifini kabul etti", ri);
                });
            }
            ev.Add($"Bir şey yapma (kalma ihtimali %{keep * 100f:0})", () =>
            {
                if (Random.value < keep) EventManager.I.Notify($"{shop.Name} {TurkceEk.In(r.ad)} teklifini reddetti, seninle devam ediyor.");
                else LoseShop(shop, $"{TurkceEk.In(r.ad)} teklifini kabul etti", ri);
            });
            EventManager.I.Push(ev);
        }

        /// <summary>
        /// Teklif yapılacak kuryeler: eğitimliler (sertifika), sonra seviyesi ve teslimatı yüksek olanlar. Karşı zam sürerken ya da
        /// bu rakip yakın zamanda denediyse (sakinken 6, tetikteyken 4, saldırıdayken 3 hafta) null.
        /// </summary>
        List<Courier> KuryeHedefleri(Rakip r)
        {
            var list = new List<Courier>(CourierManager.I.Couriers);
            if (list.Count < 8) return null;
            list.RemoveAll(HedefteMi);   // başka rakibin hedefindekiler
            list.RemoveAll(CrisisManager.CalisanMi);   // çalışan statüsündekiler (karma model) teklife açık değil
            if (list.Count < 3) return null;
            int seviye = TehditSeviyesi;
            int bekleme = seviye >= 2 ? 21 : seviye == 1 ? 28 : 42;
            if (CrisisManager.I.HasEffect("rakip_zam") || Day - r.lastCourierRaidDay < bekleme) return null;
            list.Sort((a, b) =>
            {
                int ea = a.egitimler != null ? a.egitimler.Count : 0, eb = b.egitimler != null ? b.egitimler.Count : 0;
                if (ea != eb) return eb.CompareTo(ea);
                return b.level != a.level ? b.level.CompareTo(a.level) : b.deliveries.CompareTo(a.deliveries);
            });
            int n = Mathf.Clamp(list.Count / (seviye >= 2 ? 9 : 12), 3, seviye >= 2 ? 7 : 6);
            return list.GetRange(0, n);
        }

        /// <summary>Kuryelere daha yüksek ücret teklifi (önceden duyurulmuş kurye tehdidi vadesinde).</summary>
        void CourierRaidOn(int ri, List<Courier> targets)
        {
            var r = Rakipler[ri];
            if (targets.Count == 0) { EventManager.I.Notify($"{r.ad} kurye teklifinden vazgeçti: hedeflediği kuryeler artık sende değil."); return; }
            if (CrisisManager.Employees)
            {
                EventManager.I.Notify($"{r.ad} kuryelerine daha yüksek ücret teklif etti ama çalışan statüsündeki kuryelerin hiçbiri gitmedi.");
                return;
            }
            int n = targets.Count;
            int zamYuzde = TehditSeviyesi >= 2 ? 30 : 20;
            var names = new List<string>();
            foreach (var c in targets)
                names.Add($"{c.courierName} (seviye {c.level}{(c.egitimler != null && c.egitimler.Count > 0 ? $", {c.egitimler.Count} sertifika" : "")}, moral %{c.morale * 100f:0})");
            long bonus = Round1000(n * 30000f);
            Telemetry.Add("Rakip: kurye teklifi");
            EventManager.I.Push(new GameEvent($"{r.ad} kuryelerini ayartıyor",
                    $"{r.ad}, en değerli {n} kuryene %{zamYuzde} daha yüksek ücret teklif etti: {string.Join(", ", names)}.\n\n" +
                    "Morali yüksek kurye daha çok kalır (moral %90 olan, %30 olandan belirgin şekilde daha sadık).")
                .Add("Tüm kurye ücretlerini 4 hafta +%10 artır (hepsi kalır, moral +8)", () =>
                {
                    CrisisManager.I.AddEffect(new CrisisEffect { id = "rakip_zam", title = $"{r.ad} karşısında zam: ücretler +%10", pay = 1.1f }, 28);
                    foreach (var c in CourierManager.I.Couriers) c.morale = Mathf.Clamp01(c.morale + 0.08f);
                })
                .Add($"Sadece bu {n} kuryeye prim ver (−{GameData.Ft(bonus)}, her biri %85 kalır)", () =>
                {
                    EconomyManager.I.Spend(bonus, "Rakibe karşı kurye primi", ExpenseCategory.Diger);
                    RaidOutcome(r, targets, 0.85f, 0.15f);
                })
                .Add("Bırak (her biri ortalama %55 ihtimalle gider; morali yüksek olan daha az)", () => RaidOutcome(r, targets, 0.45f, -0.05f)));
        }

        void RaidOutcome(Rakip r, List<Courier> targets, float stay, float moraleDelta)
        {
            var gone = new List<string>();
            foreach (var c in targets)
            {
                if (c == null || !CourierManager.I.Couriers.Contains(c)) continue;
                bool loyal = UpgradeManager.I != null && UpgradeManager.I.LoyaltyProgram && c.level >= 3;
                // Morali yüksek olan daha çok kalır: moral %90 → +%15, %30 → −%15
                float kal = Mathf.Clamp(stay + (c.morale - 0.6f) * 0.5f, 0.1f, 0.97f);
                if (c.OrderCount > 0 || Random.value < (loyal ? Mathf.Max(kal, 0.75f) : kal))
                    c.morale = Mathf.Clamp01(c.morale + moraleDelta);
                else { gone.Add(c.courierName); CourierManager.I.PoachToRival(c, r.ad, false); }
            }
            r.couriers += gone.Count;
            EventManager.I.Notify(gone.Count == 0 ? "Teklif alan kuryelerin hepsi seninle kaldı."
                : $"{gone.Count} kurye {TurkceEk.A(r.ad)} geçti: {string.Join(", ", gone)}.");
        }

        /// <summary>
        /// Fiyat savaşının bölgesi: sakin/tetikteyken açık bölgelerden rastgele, saldırıdayken en çok sipariş aldığın dükkanın bölgesi.
        /// </summary>
        int SavasBolgesi()
        {
            int maxD = Mathf.Clamp(CityMap.I.UnlockLevel, 1, 5);
            if (TehditSeviyesi >= 2)
            {
                Shop en = null;
                foreach (var s in CityMap.I.Shops)
                    if (s.contracted && !s.own && (en == null || s.monthOrders + s.lastMonthOrders > en.monthOrders + en.lastMonthOrders)) en = s;
                if (en != null) return Mathf.Clamp(CityMap.RegionOf(en.tile), 1, maxD);
            }
            return Random.Range(1, maxD + 1);
        }

        /// <summary>Bir bölgede 7 gün (saldırıdayken 10) ücret kırar: o bölgedeki siparişlerin düşer.</summary>
        void PriceWarOn(int ri, int bolge)
        {
            var r = Rakipler[ri];
            bool saldiri = TehditSeviyesi >= 2;
            warDistrict = Mathf.Clamp(bolge, 1, 5);
            warOwner = ri;
            priceWarDays = saldiri ? 10 : 7;
            warFactor = Mathf.Lerp(0.8f, 0.7f, r.fiyatEgilimi) - (saldiri ? 0.05f : 0f);
            string dn = CityMap.DistrictNames[warDistrict];
            long cost = Round1000(Mathf.Max(400000f, CrisisManager.AvgDailyIncome() * 0.5f));
            Telemetry.Add("Rakip: fiyat savaşı");
            EventManager.I.Push(new GameEvent($"Fiyat savaşı: {dn}",
                    $"{r.ad}, {dn} bölgesinde {priceWarDays} gün boyunca teslimat ücretini sıfırladı. Bu bölgedeki siparişlerin %{(1f - warFactor) * 100f:0} düşecek.\n\n" +
                    "Savaş sürerken büyüme giderleri (İşletme giderleri) hafifler: şirket tek seferde batmasın.")
                .Add($"Karşı kampanya yap (−{GameData.Ft(cost)}, kayıp %10'a iner, müşteriler sende kalır)", () =>
                {
                    EconomyManager.I.Spend(cost, $"Fiyat savaşı karşı kampanyası ({dn})", ExpenseCategory.Reklam);
                    warFactor = 0.9f;
                    savasGormezden = false;
                })
                .Add($"Görmezden gel (bölgede siparişler −%{(1f - warFactor) * 100f:0}; savaş bitince müşterilerin bir kısmı rakipte kalır: kalıcı −%{(saldiri ? 16 : 12)})",
                    () => { savasGormezden = true; }));
        }

        /// <summary>İki hafta yoğun reklam: müşteriler rakibi daha iyi görür.</summary>
        void MarketingBlitz(Rakip r)
        {
            r.marketingDays = 14;
            long cost = Round1000(Mathf.Max(300000f, CrisisManager.AvgDailyIncome() * 0.4f));
            Telemetry.Add("Rakip: reklam atağı");
            EventManager.I.Push(new GameEvent($"{r.ad} reklam atağında",
                    $"{r.ad} şehrin her yerine ilan verdi ve 2 hafta boyunca müşterilere daha iyi görünecek " +
                    "(paylaşılan dükkanlarda siparişlerin bir kısmı rakibe kayar).")
                .Add($"Sen de reklam ver (−{GameData.Ft(cost)}, etkisi 1 haftaya iner)", () =>
                {
                    EconomyManager.I.Spend(cost, "Rakibe karşı reklam", ExpenseCategory.Reklam);
                    r.marketingDays = 7;
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

        /// <summary>6 aylık sözleşme bitti: memnuniyet düşükse bir rakip araya girer.</summary>
        void Renewal(Shop s)
        {
            bool rivalOffer = (s.satisfaction < 70f && Random.value < 0.6f) || Random.value < 0.2f + 0.05f * (Rakipler.Count - 1);
            if (!rivalOffer || AktifRakip == 0)
            {
                Renew(s);
                EventManager.I.Notify($"{s.Name} sözleşmesini 6 ay uzattı ({GameClock.I.DateOf(s.contractEndDay)} tarihine kadar).");
                return;
            }

            var shop = s;
            int ri = TeklifVeren(s);
            var r = Rakipler[ri];
            float offer = s.Commission - Mathf.Lerp(0.015f, 0.03f, r.fiyatEgilimi);
            // Bölgenin müdürü varsa kart açılmaz: müdür ya mevcut şartlarla tutar ya da teklife eşitler
            if (BolgeMudurleri.I != null && BolgeMudurleri.I.YenilemeyiUstlen(s, r.ad, out bool eslestir))
            {
                if (eslestir) s.commissionBonus -= s.Commission - offer;
                Renew(s);
                AddSatisfaction(s, eslestir ? 5f : 2f);
                return;
            }
            var ev = new GameEvent("Sözleşme yenileme: rakip teklifi",
                $"{s.Name} ile sözleşmen bitti. {r.ad} dükkana {Pct(offer)} komisyonla teklif verdi " +
                $"(senin oranın {Pct(s.Commission)}, memnuniyet {s.satisfaction:0}/100).");
            ev.Add($"Teklife eşitle (→ {Pct(offer)}), 6 ay uzat", () => { shop.commissionBonus -= shop.Commission - offer; Renew(shop); AddSatisfaction(shop, 5f); });
            if (s.satisfaction >= 50f)
                ev.Add($"Münhasırlık öner (→ {Pct(s.Commission - 0.03f)}, bütün rakipler çıkar, siparişler +%15)", () =>
                {
                    shop.commissionBonus -= 0.03f; MakeExclusive(shop); AddSatisfaction(shop, 8f);
                });
            ev.Add($"Mevcut şartlarda ısrar et (kalma ihtimali %{s.satisfaction:0})", () =>
            {
                if (Random.value < shop.satisfaction / 100f) { Renew(shop); AddSatisfaction(shop, -5f); EventManager.I.Notify($"{shop.Name} mevcut şartlarla devam etmeyi kabul etti."); }
                else LoseShop(shop, $"{TurkceEk.In(r.ad)} teklifini kabul etti", ri);
            });
            EventManager.I.Push(ev);
            s.contractEndDay = Day + 3; // karar verilmezse birkaç gün sonra yine sorulur
        }

        void Renew(Shop s)
        {
            s.contractStartDay = Day;
            s.contractEndDay = Day + contractDays;
        }

        void LoseShop(Shop s, string reason, int ri)
        {
            var r = RakipAt(ri) ?? Lider;
            s.contracted = false;
            s.exclusive = false;
            s.oncelikli = false;
            s.sadakat *= 0.3f;   // rakibe giden dükkanın sadakati büyük ölçüde kaybolur
            if (ri >= 0 && ri < Rakipler.Count) Ekle(s, ri);
            s.resignBlockedUntil = Day + 30;
            s.satisfaction = 40f;
            CityMap.I.RefreshColors();
            EventManager.I.Push(new GameEvent($"{s.Name} gitti",
                $"{s.Name} {reason}. Artık {DukkanRakipleri(s)} ile çalışıyor. " +
                $"30 gün sonra yeniden sözleşme teklif edebilirsin ({GameClock.I.DateOf(s.resignBlockedUntil)})."));
        }

        /// <summary>Haftalık rapor satırı.</summary>
        public string ReportLine
        {
            get
            {
                var sb = new System.Text.StringBuilder($"Pazar payın ~%{MarketShare * 100f:0}. Rakipler:");
                foreach (var r in Rakipler)
                {
                    if (r.satinAlindi) { sb.Append($"\n• {r.ad}: senin (satın alındı)"); continue; }
                    sb.Append($"\n• {r.ad}: pay ~%{PayiOf(r) * 100f:0} · itibar {r.reputation:0.0}/5 · {r.couriers} kurye · " +
                              $"{ShopCountOf(Rakipler.IndexOf(r))} dükkan · ~{r.deliveriesLastWeek} teslimat · tavrı: {r.MoodText}");
                }
                return sb.ToString();
            }
        }

        // ================================================================ satın alma

        int sonSatisTeklifiGunu = -999;

        /// <summary>Faaliyette olan (satın alınmamış) rakip sayısı.</summary>
        public int AktifRakip { get { int n = 0; foreach (var r in Rakipler) if (!r.satinAlindi) n++; return n; } }

        /// <summary>Bir platformu satın alma bedeli: kuryeleri, dükkanları ve kasası.</summary>
        public long SatinAlmaBedeli(int i)
        {
            var r = RakipAt(i);
            if (r == null) return 0;
            return Round1000(r.couriers * 450000f + ShopCountOf(i) * 220000f + Mathf.Max(0f, r.money) * 0.5f + 3000000f);
        }

        /// <summary>
        /// Haftalık: zayıflayan bir platformun (payı %12'nin altına indi ya da kasası eksiye düştü) yatırımcıları onu sana satmak ister.
        /// 60. günden sonra, en fazla 4 haftada bir (Mad Games Tycoon'daki şirket satın alma gibi). En zayıf olan sorulur.
        /// </summary>
        void SatisTeklifi()
        {
            if (GameClock.I.CompanyDay < 60 || Day - sonSatisTeklifiGunu < 28 || myDeliveriesLastWeek <= 0) return;
            int sec = -1; float enDusuk = float.MaxValue;
            for (int k = 0; k < Rakipler.Count; k++)
            {
                var x = Rakipler[k];
                if (x.satinAlindi) continue;
                float pay = PayiOf(x);
                if (!(pay < 0.12f || x.money < 0)) continue;
                if (pay < enDusuk) { enDusuk = pay; sec = k; }
            }
            if (sec < 0) return;
            sonSatisTeklifiGunu = Day;
            int i = sec;
            var r = Rakipler[i];
            long bedel = SatinAlmaBedeli(i);
            long serbest = EconomyManager.I.FreeCash;
            var ev = new GameEvent($"{r.ad} satılık",
                    $"{r.ad} zor durumda: geçen hafta pazar payı ~%{PayiOf(r) * 100f:0}, kasası {(r.money < 0 ? "eksi" : "eriyor")}. " +
                    "Yatırımcıları şirketi sana satmak istiyor.\n\n" +
                    $"Satın alırsan bu platform şehirden çekilir: çalıştığı {ShopCountOf(i)} dükkanda müşterileri sana kalır, " +
                    "seninle sözleşmesi olmayanlar sözleşmeleriyle birlikte sana geçer. Diğer rakipler yerinde kalır.")
                .Ozet("Bedel", GameData.Ft(bedel), serbest >= bedel ? 0 : 2)
                .Ozet("Kuryesi", r.couriers.ToString())
                .Ozet("Dükkanı", ShopCountOf(i).ToString())
                .Ozet("Pazar payın", $"%{MarketShare * 100f:0}", 1);
            if (serbest >= bedel) ev.Add($"Satın al ({GameData.Ft(bedel)})", () => SatinAl(i));
            else ev.Add($"Param yetmiyor (harcanabilir {GameData.Ft(serbest)})", null);
            ev.Add("Şimdilik hayır", null);
            EventManager.I.Push(ev);
        }

        /// <summary>Bir platformu satın alır: dükkanlardan çekilir, sözleşmesiz dükkanları sana geçer, faaliyeti durur.</summary>
        public void SatinAl(int i)
        {
            var r = RakipAt(i);
            if (r == null || r.satinAlindi) return;
            long bedel = SatinAlmaBedeli(i);
            if (!EconomyManager.I.TrySpend(bedel, $"{r.ad} satın alındı", ExpenseCategory.Diger)) return;
            r.satinAlindi = true;
            int devralinan = 0;
            foreach (var s in CityMap.I.Shops)
            {
                if (!Var(s, i)) continue;
                s.rivalMask &= ~(1 << i);
                if (!s.contracted && !s.own && CityMap.I.IsUnlocked(s.tile))
                {
                    s.contracted = true;
                    s.contractStartDay = Day;
                    s.contractEndDay = Day + contractDays;
                    s.satisfaction = 60f;
                    devralinan++;
                }
            }
            r.couriers = 0;
            r.marketingDays = 0;
            r.deliveriesThisWeek = 0;
            r.aggression = 0f;
            if (warOwner == i) { priceWarDays = 0; warDistrict = 0; }
            Itibar.Olay(0.1f);
            if (CityMap.I != null) CityMap.I.RefreshColors();
            EventManager.I.Push(new GameEvent($"{r.ad} artık senin",
                $"Anlaşma imzalandı: {r.ad} şehirden çekiliyor. " +
                (devralinan > 0 ? $"{devralinan} dükkan sözleşmesiyle birlikte sana geçti (Dükkanlar). " : "") +
                (AktifRakip > 0 ? $"Şehirde {AktifRakip} rakibin kaldı." : "Şehirde rakibin kalmadı: bütün müşteriler senin uygulamanda.")));
        }

        /// <summary>Geçen haftaya göre pazar payı değişimi (puan).</summary>
        public float ShareTrend => shareHistory.Count < 2 ? 0f : shareHistory[shareHistory.Count - 1] - shareHistory[shareHistory.Count - 2];
    }
}
