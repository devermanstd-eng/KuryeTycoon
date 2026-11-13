using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace KuryeTycoon
{
    [System.Serializable]
    public class TelemetrySave
    {
        public List<string> keys = new List<string>();
        public List<int> values = new List<int>();
        public List<string> log = new List<string>();
    }

    /// <summary>
    /// Hata ayıklama sayaçları: oyunda neler olduğunu (neden iptal, neden kaçan sipariş, olaylar, kararlar) sayar.
    /// Kayda yazılır, böylece uzun oynanmış bir oyunun raporunda "neden" soruları cevaplanabilir.
    /// Ayrıca bütün bildirimlerin son 400 satırı (oyun içi saatiyle) tutulur.
    /// </summary>
    public static class Telemetry
    {
        public static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
        public static readonly List<string> Log = new List<string>();
        const int MaxLog = 400;

        public static void Add(string key, int n = 1)
        {
            if (string.IsNullOrEmpty(key)) return;
            Counts.TryGetValue(key, out int v);
            Counts[key] = v + n;
        }

        public static void Line(string msg)
        {
            var c = GameClock.I;
            Log.Add(c != null ? $"{c.Day}. gün {c.Hour:00}:{c.Minute:00} · {msg}" : msg);
            if (Log.Count > MaxLog) Log.RemoveRange(0, Log.Count - MaxLog);
        }

        // Günlük teslimat kalitesi (gün sonunda StatsHistory'ye yazılır, sonra sıfırlanır)
        public static int DayLate, DayLate15, DayCold, DayDelivered;
        public static float DayMinutes, DayAssignWait, DayPickupWait;

        public static void ResetDay()
        {
            DayLate = DayLate15 = DayCold = DayDelivered = 0;
            DayMinutes = DayAssignWait = DayPickupWait = 0f;
        }

        public static void Reset() { Counts.Clear(); Log.Clear(); ResetDay(); }

        public static TelemetrySave Capture()
        {
            var s = new TelemetrySave();
            foreach (var kv in Counts) { s.keys.Add(kv.Key); s.values.Add(kv.Value); }
            s.log.AddRange(Log);
            return s;
        }

        public static void Restore(TelemetrySave s)
        {
            Reset();
            if (s == null) return;
            for (int i = 0; i < s.keys.Count && i < s.values.Count; i++) Counts[s.keys[i]] = s.values[i];
            if (s.log != null) Log.AddRange(s.log);
        }
    }

    /// <summary>
    /// Hata ayıklama raporu (F9 ya da Ayarlar > Gelişmiş > "Hata ayıklama raporu oluştur"):
    /// oyunun o anki bütün durumunu, otomatik kontrolleri (şüpheli değerler, denge sorunları) ve kaydın tamamını
    /// tek bir metin dosyasına yazar. Dosya kayıt klasöründeki "hata_ayiklama" klasörüne gider.
    /// Bu dosya geliştiriciye gönderilir: oyunun eksikleri ve sorunları ondan bulunur.
    /// </summary>
    public class DebugReport : MonoBehaviour
    {
        public static DebugReport I { get; private set; }
        /// <summary>Raporda hangi kodla oynandığını görmek için; her önemli güncellemede değişir.</summary>
        public const string CodeVersion = "2026-11-13 a (otomatik test modu)";
        public static string Folder => Path.Combine(Application.persistentDataPath, "hata_ayiklama");
        public string lastPath = "";

        void Awake()
        {
            I = this;
            EventManager.Notified += OnNotified;
        }

        void OnDestroy() { EventManager.Notified -= OnNotified; }
        static void OnNotified(string m) => Telemetry.Line(m);

        // F9: IMGUI klavye olayı (proje yeni Input System kullanıyor; eski UnityEngine.Input hata verir)
        void OnGUI()
        {
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown || e.keyCode != KeyCode.F9) return;
            if (GameBootstrap.I == null || !GameBootstrap.I.Started) return;
            e.Use();
            Write(true);
        }

        // ------------------------------------------------------------------ rapor

        public string Write(bool notify)
        {
            var sb = new StringBuilder();
            try
            {
                Header(sb);
                Checks(sb);
                Economy(sb);
                History(sb);
                Couriers(sb);
                Shops(sb);
                Orders(sb);
                Systems(sb);
                TelemetrySection(sb);
                FeedSection(sb);
                RawSave(sb);
            }
            catch (System.Exception ex)
            {
                sb.AppendLine();
                sb.AppendLine("!!! RAPOR YAZILIRKEN HATA: " + ex);
            }

            try
            {
                Directory.CreateDirectory(Folder);
                string name = $"rapor_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
                lastPath = Path.Combine(Folder, name);
                File.WriteAllText(lastPath, sb.ToString(), new UTF8Encoding(false));
                if (notify) EventManager.I.Notify($"Hata ayıklama raporu yazıldı: {lastPath}");
                Debug.Log($"[KuryeTycoon] Hata ayıklama raporu: {lastPath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Rapor yazılamadı: " + ex.Message);
                if (notify) EventManager.I.Notify("Hata ayıklama raporu yazılamadı: " + ex.Message);
            }
            return lastPath;
        }

        public static void OpenFolder()
        {
            Directory.CreateDirectory(Folder);
            Application.OpenURL(new System.Uri(Folder).AbsoluteUri);
        }

        static string Ft(long v) => GameData.Ft(v);

        static void H(StringBuilder sb, string title)
        {
            sb.AppendLine();
            sb.AppendLine("==================================================================");
            sb.AppendLine("  " + title);
            sb.AppendLine("==================================================================");
        }

        void Header(StringBuilder sb)
        {
            var clock = GameClock.I;
            var eco = EconomyManager.I;
            sb.AppendLine("KURYE TYCOON — HATA AYIKLAMA RAPORU");
            sb.AppendLine($"Kod sürümü: {CodeVersion}");
            sb.AppendLine($"Rapor zamanı (gerçek): {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Unity {Application.unityVersion} · {SystemInfo.operatingSystem} · {Screen.width}x{Screen.height}");
            sb.AppendLine($"Kayıt yuvası: {SaveSystem.Slot + 1} · kayıt dosyası: {SaveSystem.SavePath}");
            H(sb, "ÖZET");
            sb.AppendLine($"Şirket: {CompanyProfile.Name} · zorluk: {Difficulty.Level}");
            sb.AppendLine($"Tarih: {clock.DateOf(clock.Day)} {clock.Hour:00}:{clock.Minute:00} · şirketin {clock.CompanyDay}. günü · mevsim {clock.Season}");
            sb.AppendLine($"Hız: {clock.SpeedMultiplier}× (etkin {clock.EffectiveSpeed}×) · duraklatıldı: {clock.Paused}");
            sb.AppendLine($"Kasa: {Ft(eco.money)} · serbest kasa: {Ft(eco.FreeCash)} · vergi borcu: {Ft(eco.taxDebt)}");
            sb.AppendLine($"İtibar: {CompanyProfile.Reputation:0.00} · açık bölge seviyesi: {CityMap.I.UnlockLevel} ({CityMap.DistrictNames[CityMap.I.UnlockLevel]})");
            sb.AppendLine($"Hava: {(WeatherSystem.I != null ? WeatherSystem.I.Current.ToString() : "-")}");
            int online = 0;
            foreach (var c in CourierManager.I.Couriers) if (c.IsOnline) online++;
            int contracted = 0, own = 0;
            foreach (var s in CityMap.I.Shops) { if (s.contracted) contracted++; if (s.own) own++; }
            sb.AppendLine($"Kurye: {CourierManager.I.Couriers.Count} (çevrimiçi {online}, sınır {UpgradeManager.I.CourierCap}) · sözleşmeli dükkan: {contracted}/{CityMap.I.Shops.Count}" +
                          (own > 0 ? " · kendi marketin açık" : ""));
        }

        // ------------------------------------------------------------------ otomatik kontroller

        void Checks(StringBuilder sb)
        {
            H(sb, "OTOMATİK KONTROLLER (şüpheli durumlar)");
            var warn = new List<string>();
            var eco = EconomyManager.I;
            var om = OrderManager.I;
            var hist = StatsHistory.I != null ? StatsHistory.I.Days : new List<DayRecord>();

            if (eco.money < 0) warn.Add($"Kasa eksi: {Ft(eco.money)}");
            if (eco.FreeCash < 0) warn.Add($"Serbest kasa eksi: {Ft(eco.FreeCash)} (kurye hakedişleri kasadan fazla)");
            if (eco.taxDebt > 0) warn.Add($"Vergi borcu var: {Ft(eco.taxDebt)}");

            // Son 7 gün
            int n = Mathf.Min(7, hist.Count);
            if (n > 0)
            {
                long net = 0; int del = 0, can = 0, lost = 0;
                for (int i = hist.Count - n; i < hist.Count; i++) { net += hist[i].net; del += hist[i].deliveries; can += hist[i].cancelled; lost += hist[i].lost; }
                float cancelRate = del + can > 0 ? can / (float)(del + can) : 0f;
                sb.AppendLine($"Son {n} gün: {del} teslimat, {can} iptal (%{cancelRate * 100f:0.0}), {lost} kaçan, net {Ft(net)} (günlük ort. {Ft(net / n)})");
                if (cancelRate > 0.05f) warn.Add($"İptal oranı yüksek: son {n} günde %{cancelRate * 100f:0.0} (gerçekte %1,5–3)");
                if (net < 0) warn.Add($"Son {n} gün toplam zarar: {Ft(net)}");
                if (lost > del * 0.3f) warn.Add($"Kaçan sipariş çok: {lost} (teslimatın %{lost * 100f / Mathf.Max(1, del):0}'i) — ücret pahalı ya da stok yok");
                // Gelir/gider sıçraması
                for (int i = Mathf.Max(1, hist.Count - 14); i < hist.Count; i++)
                    if (hist[i - 1].deliveries > 10 && hist[i].deliveries < hist[i - 1].deliveries * 0.4f)
                        warn.Add($"Teslimat ani düştü: {hist[i - 1].date} {hist[i - 1].deliveries} → {hist[i].date} {hist[i].deliveries}");
            }
            else warn.Add("Gün geçmişi boş (ilk gün sonu henüz olmadı ya da kayıt eski)");

            long fixedDaily = UpgradeManager.I.TotalDailyCost + (DarkStore.I != null && DarkStore.I.IsOpen ? DarkStore.I.DailyCost : 0);
            sb.AppendLine($"Sabit günlük gider: geliştirmeler {Ft(UpgradeManager.I.TotalDailyCost)}" +
                          (DarkStore.I != null && DarkStore.I.IsOpen ? $" + kendi marketin {Ft(DarkStore.I.DailyCost)}" : "") + $" = {Ft(fixedDaily)}");
            if (n > 0)
            {
                long avgIncome = 0;
                for (int i = hist.Count - n; i < hist.Count; i++) avgIncome += hist[i].income;
                avgIncome /= n;
                if (fixedDaily > avgIncome * 0.5f) warn.Add($"Sabit günlük gider ({Ft(fixedDaily)}) günlük gelirin yarısından fazla ({Ft(avgIncome)})");
            }

            // Kuryeler
            foreach (var c in CourierManager.I.Couriers)
            {
                if (c.morale < 0.3f) warn.Add($"Kurye morali düşük: {c.courierName} ({c.morale:0.00})");
                if (c.fatigue > 0.85f) warn.Add($"Kurye çok yorgun: {c.courierName} ({c.fatigue:0.00})");
                if (c.hygiene < Courier.HygieneLimit) warn.Add($"Kurye çantası kirli: {c.courierName} ({c.hygiene:0.00})");
                if (c.IsOnline && c.State == CourierState.Bosta && c.OrderCount == 0 && om.Pending.Count > 3)
                    warn.Add($"Bekleyen sipariş varken boşta kurye: {c.courierName} ({c.V.name}) — atama / kâr eşiği?");
            }
            if (CourierManager.I.Couriers.Count == 0) warn.Add("Hiç kurye yok");

            // Dükkanlar
            foreach (var s in CityMap.I.Shops)
            {
                if (!s.contracted || s.own) continue;
                if (s.satisfaction < 35f) warn.Add($"Dükkan memnuniyeti düşük: {s.Name} ({s.satisfaction:0})");
                if (!CityMap.I.IsUnlocked(s.tile)) warn.Add($"Sözleşmeli dükkan kilitli bölgede: {s.Name}");
            }

            // Siparişler
            float now = GameClock.I.TotalMinutes;
            foreach (var o in om.Pending)
                if (now - o.createdAt > om.pendingTimeout * 0.8f) warn.Add($"Bekleyen sipariş iptale yakın: #{o.id} {o.shop.Name} ({now - o.createdAt:0} dk)");
            foreach (var o in om.Active)
                if (now - o.createdAt > 150f) warn.Add($"Sipariş çok uzun süredir yolda: #{o.id} {o.shop.Name} ({now - o.createdAt:0} dk, durum {o.status})");

            if (warn.Count == 0) sb.AppendLine("Şüpheli bir durum bulunmadı.");
            foreach (var w in warn) sb.AppendLine(" ! " + w);
        }

        // ------------------------------------------------------------------ ekonomi

        void Economy(StringBuilder sb)
        {
            H(sb, "EKONOMİ");
            var eco = EconomyManager.I;
            Period(sb, "Bu yıl", eco.Year);
            Period(sb, "Bugün", eco.Today);
            if (eco.Yesterday != null) Period(sb, "Dün", eco.Yesterday);
            sb.AppendLine();
            sb.AppendLine($"Kurye bakiyeleri toplamı: {Ft(eco.TotalCourierBalances)} · bekleyen hakediş: {Ft(eco.TotalPendingPayouts)} · kuryelerdeki nakit: {Ft(eco.TotalCashHeld)}");
            sb.AppendLine("Son hesap hareketleri:");
            for (int i = 0; i < Mathf.Min(40, eco.Ledger.Count); i++) sb.AppendLine("   " + eco.Ledger[i]);
        }

        static void Period(StringBuilder sb, string name, PeriodStats p)
        {
            if (p == null) return;
            sb.AppendLine($"[{name}] teslimat {p.deliveries} · gelir {Ft(p.Income)} (komisyon {Ft(p.commission)}, ücret {Ft(p.fees)}, diğer {Ft(p.otherIncome)})");
            sb.AppendLine($"   kurye ödemesi {Ft(p.courierPay)} · boost {Ft(p.boosts)} · ödüller {Ft(p.rewards)} · cezalar {Ft(p.penalties)} · nakit sipariş {p.cashOrders}");
            var parts = new List<string>();
            for (int i = 0; i < p.byCategory.Length && i < PeriodStats.CategoryNames.Length; i++)
                if (p.byCategory[i] != 0) parts.Add($"{PeriodStats.CategoryNames[i]} {Ft(p.byCategory[i])}");
            if (parts.Count > 0) sb.AppendLine("   giderler: " + string.Join(" · ", parts));
        }

        // ------------------------------------------------------------------ geçmiş

        void History(StringBuilder sb)
        {
            H(sb, "GÜNLÜK GEÇMİŞ (her gün sonu)");
            var hist = StatsHistory.I != null ? StatsHistory.I.Days : null;
            if (hist == null || hist.Count == 0) { sb.AppendLine("(boş)"); return; }
            sb.AppendLine(Fields(hist[0], true));
            foreach (var d in hist) sb.AppendLine(Fields(d, false));
        }

        // ------------------------------------------------------------------ kuryeler

        void Couriers(StringBuilder sb)
        {
            H(sb, "KURYELER");
            foreach (var c in CourierManager.I.Couriers)
            {
                sb.AppendLine($"- {c.courierName} · {c.V.name} · durum {c.State} · elinde {c.OrderCount} sipariş · çevrimiçi {c.IsOnline}");
                sb.AppendLine("  " + Fields(c, false, true));
            }
            sb.AppendLine($"Başvurular: {CourierManager.I.Applications.Count}");
        }

        // ------------------------------------------------------------------ dükkanlar

        void Shops(StringBuilder sb)
        {
            H(sb, "DÜKKANLAR");
            foreach (var s in CityMap.I.Shops)
            {
                sb.AppendLine($"- {s.Name} · bölge {CityMap.RegionOf(s.tile)} {(CityMap.I.IsUnlocked(s.tile) ? "açık" : "KİLİTLİ")} · " +
                              $"{(s.own ? "SENİN" : s.exclusive ? "münhasır" : s.contracted ? "sözleşmeli" : "sözleşmesiz")} · " +
                              $"{ShopHours.StatusText(s.def)} ({ShopHours.HoursText(s.def)}) · ağırlık {s.def.weight:0.00} · komisyon %{s.Commission * 100f:0.#}");
                sb.AppendLine("  " + Fields(s, false, true));
            }
        }

        // ------------------------------------------------------------------ siparişler

        void Orders(StringBuilder sb)
        {
            H(sb, "SİPARİŞLER (şu an)");
            var om = OrderManager.I;
            float now = GameClock.I.TotalMinutes;
            sb.AppendLine($"Bekleyen {om.Pending.Count} · yolda {om.Active.Count} · bugün iptal {om.cancelledToday} (gecikme {om.cancelledDelayToday}), dükkan iptali {om.cancelledShopToday}, müşteri vazgeçti {om.cancelledCustomerToday}, kaçan {om.lostToday} · otomatik atama {om.autoAssign}");
            var hours = new List<string>();
            for (int h = 0; h < 24; h++) if (om.cancelledByHour[h] > 0) hours.Add($"{h:00}:00={om.cancelledByHour[h]}");
            if (hours.Count > 0) sb.AppendLine("Bugünkü iptaller saat saat: " + string.Join(", ", hours));
            foreach (var o in om.Pending)
                sb.AppendLine($"  bekliyor #{o.id} {o.shop.Name} · {now - o.createdAt:0} dk · {GameData.KmText(o.distance)} · gelir {Ft(o.CompanyIncome)} · red {o.priceDeclines}/{o.rejectedBy.Count}");
            foreach (var o in om.Active)
                sb.AppendLine($"  yolda #{o.id} {o.shop.Name} · {o.status} · {now - o.createdAt:0} dk · kurye {(o.courier != null ? o.courier.courierName : "-")}");
        }

        // ------------------------------------------------------------------ diğer sistemler

        void Systems(StringBuilder sb)
        {
            H(sb, "GELİŞTİRMELER VE DİĞER SİSTEMLER");
            var um = UpgradeManager.I;
            foreach (UpgradeKind t in System.Enum.GetValues(typeof(UpgradeKind)))
                sb.AppendLine($"  {t}: seviye {um.Level(t)}");
            sb.AppendLine($"  Toplam günlük geliştirme gideri: {Ft(um.TotalDailyCost)}");

            if (GoalManager.I != null)
            {
                sb.AppendLine($"Haftalık hedefler (tamamlanan toplam {GoalManager.I.completedTotal}):");
                foreach (var g in GoalManager.I.Goals) sb.AppendLine($"  {(g.done ? "[x]" : "[ ]")} {g.Title} · {g.ProgressText} · ödül {Ft(g.reward)}");
            }
            if (DarkStore.I != null && DarkStore.I.IsOpen)
            {
                var ds = DarkStore.I;
                sb.AppendLine($"Kendi marketin: boyut {ds.size} · stok {Ft(ds.TotalStock)}/{Ft(ds.Capacity)} · günlük gider {Ft(ds.DailyCost)} · " +
                              $"toplam {ds.ordersTotal} sipariş, satış {Ft(ds.salesTotal)}, brüt kâr {Ft(ds.profitTotal)}, bozulan {Ft(ds.spoiledTotal)}, kaçan {ds.lostTotal}");
            }
            if (CityEvents.I != null)
            {
                sb.AppendLine("Şehir etkinlikleri:");
                foreach (var e in CityEvents.I.Events) sb.AppendLine($"  {e.day}. gün {e.TimeText} {e.title} (talep ×{e.demand:0.00}, yemek ×{e.food:0.00}, market ×{e.market:0.00})");
            }
            if (RivalCompany.I != null) sb.AppendLine("Rakip: " + Fields(RivalCompany.I, false, true));
            if (RivalCompany.I != null)
            {
                var rv = RivalCompany.I;
                sb.AppendLine("Rakip özeti: " + rv.ReportLine);
                sb.AppendLine($"  saldırganlık {rv.aggression:0.00} · kalite {rv.quality:0.00} · yatırım turu {rv.fundingRounds} · kasa {GameData.Ft(rv.money)} · " +
                              $"fiyat savaşı {rv.priceWarDays} gün (bölge {rv.warDistrict}, ×{rv.warFactor:0.00}) · reklam {rv.marketingDays} gün");
                sb.AppendLine($"  hizmet: sen {rv.myAvgMinutes:0.0} dk / %{rv.myLateRate * 100f:0} geç → ×{rv.MyService:0.00} · rakip {rv.rivalAvgMinutes:0.0} dk / " +
                              $"%{rv.rivalLateRate * 100f:0} geç → ×{rv.RivalService:0.00} · talep ×{rv.ServiceDemandFactor:0.00}");
                if (rv.shareHistory.Count > 0)
                {
                    var hs = new System.Text.StringBuilder("  haftalık pazar payın:");
                    foreach (var v in rv.shareHistory) hs.Append($" %{v * 100f:0}");
                    sb.AppendLine(hs.ToString());
                }
            }
            if (CrisisManager.I != null) sb.AppendLine("Krizler:\n  " + CrisisManager.I.StatusText.Replace("\n", "\n  "));
            sb.AppendLine($"Yıl sonu vergisi (tahmini, kademeli): {GameData.Ft(EconomyManager.ProgressiveTax(System.Math.Max(0L, EconomyManager.I.Year.Profit)))}");
            if (SupportManager.I != null) sb.AppendLine("Destek: " + Fields(SupportManager.I, false, true));
            if (CampaignManager.I != null) sb.AppendLine("Kampanya: " + Fields(CampaignManager.I, false, true));
        }

        void TelemetrySection(StringBuilder sb)
        {
            H(sb, "SAYAÇLAR (bu güncellemeden sonra oynanan süre için)");
            if (Telemetry.Counts.Count == 0) { sb.AppendLine("(henüz sayaç yok)"); return; }
            var keys = new List<string>(Telemetry.Counts.Keys);
            keys.Sort(System.StringComparer.Ordinal);
            foreach (var k in keys) sb.AppendLine($"  {k}: {Telemetry.Counts[k]}");
        }

        void FeedSection(StringBuilder sb)
        {
            H(sb, "BİLDİRİMLER (en eski üstte)");
            if (Telemetry.Log.Count > 0) foreach (var l in Telemetry.Log) sb.AppendLine("  " + l);
            else
            {
                var feed = EventManager.I.Feed;
                for (int i = feed.Count - 1; i >= 0; i--) sb.AppendLine("  " + feed[i]);
            }
        }

        void RawSave(StringBuilder sb)
        {
            H(sb, "KAYDIN TAMAMI (JSON)");
            var d = SaveSystem.I != null ? SaveSystem.I.CaptureForDebug() : null;
            sb.AppendLine(d != null ? JsonUtility.ToJson(d, true) : "(kayıt alınamadı)");
        }

        // ------------------------------------------------------------------ yansıma ile alan dökümü

        /// <summary>Nesnenin basit (sayı, metin, bool, enum) public alanlarını "ad=değer" olarak yazar.</summary>
        static string Fields(object o, bool header, bool named = false)
        {
            if (o == null) return "";
            var parts = new List<string>();
            foreach (var f in o.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var t = f.FieldType;
                if (!(t.IsPrimitive || t.IsEnum || t == typeof(string))) continue;
                if (header) { parts.Add(f.Name); continue; }
                object v;
                try { v = f.GetValue(o); } catch { continue; }
                string s = v is float fl ? fl.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : v?.ToString() ?? "";
                parts.Add(named ? $"{f.Name}={s}" : s);
            }
            return string.Join(named ? " · " : ";", parts);
        }
    }
}
