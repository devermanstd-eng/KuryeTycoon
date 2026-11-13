using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Kurulum kredisinin bir taksiti.</summary>
    [System.Serializable]
    public class Installment
    {
        public int month;      // 1–12
        public int day;        // ayın son günü
        public long amount;    // ödenmesi gereken (kaçırılan taksitler eklenir)
        public long paid;
        public bool closed;    // ödendi ya da kaçırıldı (kalan bir sonrakine aktarıldı)

        public long Remaining => System.Math.Max(0L, amount - paid);
        public int DueDay => GameClock.AbsoluteDay(1, month, day);
        public string DueText => $"{day} {GameClock.MonthNameOf(month)}";
    }

    /// <summary>
    /// Hikaye: Yıllarca Turbo Paket'te kuryelik yaptın. Platformun her ay biraz daha düşen paket ücretinden bıkıp
    /// kendi kurye şirketini kurmaya karar verdin ve Duna Bank'tan 1 000 000 Ft girişimci kredisi aldın.
    /// Kredi faiziyle birlikte Mart–Temmuz arası 5 taksitle geri ödenir (süre baskısı).
    /// Taksit kaçırırsan kredi notun düşer; not biterse banka krediyi geri çağırır ve şirket kapanır.
    /// Erken veya kısmi ödeme yapılabilir. Kredi ödemesi gider değildir, vergiden düşülmez.
    /// (Sınıfın adı eski kayıtlar ve diğer scriptler bozulmasın diye FamilyLoan olarak kaldı.)
    /// </summary>
    public class FamilyLoan : MonoBehaviour
    {
        public static FamilyLoan I { get; private set; }

        public const string Banka = "Duna Bank";
        public const string Temsilci = "Kovács Réka";
        public const string EskiSirket = "Turbo Paket";
        public const string EskiArkadas = "Bence";

        [Header("Kredi")]
        public long principal = 1000000;          // çekilen kredi (başlangıç kasası)
        [Tooltip("Kredinin toplam faizi (5 aylık vade için)")]
        public float faizOrani = 0.08f;
        // Mart–Temmuz sonu (5 taksit).
        public int[] dueMonths = { 3, 4, 5, 6, 7 };
        public int maxTrust = 3;

        public readonly List<Installment> Schedule = new List<Installment>();
        public int Trust { get; private set; }
        public long TotalPaid { get; private set; }
        public bool Finished { get; private set; }
        public bool Failed { get; private set; }

        /// <summary>Faiz dahil geri ödenecek toplam (eski kayıtlarda faizsiz: anapara).</summary>
        public long Toplam { get; private set; }
        public long Faiz => System.Math.Max(0L, Toplam - principal);

        int deliveriesMilestone;
        bool ready;

        static string Ft(long v) => GameData.Ft(v);

        public long Remaining => Finished ? 0 : System.Math.Max(0L, Toplam - TotalPaid);

        void Awake() { I = this; }

        // ---------- Kayıt ----------

        public LoanSave CaptureSave()
        {
            var s = new LoanSave
            {
                principal = principal, toplam = Toplam, totalPaid = TotalPaid, trust = Trust, deliveriesMilestone = deliveriesMilestone,
                finished = Finished, failed = Failed
            };
            s.schedule.AddRange(Schedule);
            return s;
        }

        /// <summary>Kayıttan yüklerken Init yerine çağrılır: taksitleri geri koyar, başlangıç hikayesini tekrar göstermez.</summary>
        public void InitRestored(LoanSave s)
        {
            if (s == null || s.schedule == null || s.schedule.Count == 0) { Init(); return; }
            Schedule.Clear();
            Schedule.AddRange(s.schedule);
            Trust = s.trust;
            TotalPaid = s.totalPaid;
            Finished = s.finished;
            Failed = s.failed;
            deliveriesMilestone = s.deliveriesMilestone;
            Toplam = s.toplam > 0 ? s.toplam : principal;   // eski kayıt (aile borcu): faiz yok
            GameClock.I.OnNewDay += OnNewDay;
            ready = true;
        }

        public void Init()
        {
            Trust = maxTrust;
            Toplam = (long)Mathf.Round(principal * (1f + faizOrani) / 1000f) * 1000;
            long each = (Toplam / dueMonths.Length) / 1000 * 1000;
            for (int i = 0; i < dueMonths.Length; i++)
            {
                int m = dueMonths[i];
                long amount = i == dueMonths.Length - 1 ? Toplam - each * (dueMonths.Length - 1) : each;
                Schedule.Add(new Installment { month = m, day = GameClock.MonthLength(m), amount = amount });
            }
            GameClock.I.OnNewDay += OnNewDay;
            ready = true;

            var first = Schedule[0];
            EventManager.I.Push(new GameEvent("Bir hayalin başlangıcı",
                $"Üç yıl boyunca {TurkceEk.In(EskiSirket)} mor çantasını taşıdın. Yağmurda, karda, gece yarısı...\n" +
                "Her ay paket başı ücret biraz daha düştü, uygulama her ay biraz daha çok şey istedi. " +
                $"Bir gece vardiya arkadaşın {EskiArkadas} durakta söylendi: \"Bu işi bizden iyi bilen yok, ama parayı hep başkası kazanıyor.\"\n\n" +
                $"Ertesi sabah {TurkceEk.In(Banka)} şubesindeydin. Temsilci {Temsilci} iş planını okudu, gözlüğünü çıkardı:\n" +
                $"\"{Ft(principal)} girişimci kredisi verebiliriz. Faiziyle {Ft(Toplam)}, Temmuz sonuna kadar beş taksit. " +
                "Taksiti aksatırsanız notunuz düşer; üçüncüsünde krediyi geri çağırırız.\"\n\n" +
                $"Taksitler: her ayın sonunda {Ft(each)} ({first.DueText}, ...). Erken ödeyebilirsin. " +
                $"{maxTrust} taksit kaçırırsan banka krediyi geri çağırır ve şirket kapanır.\n\n" +
                $"Eski patronun {EskiSirket} artık rakibin. Şehirde onlardan başka platformlar da var.")
                .Add("İmzala. Bu sefer kendi şirketim için çalışacağım.", null));
        }

        public Installment NextDue
        {
            get
            {
                foreach (var t in Schedule) if (!t.closed && t.Remaining > 0) return t;
                return null;
            }
        }

        public int DaysUntil(Installment t) => t.DueDay - GameClock.I.Day;

        void OnNewDay()
        {
            if (!ready || Finished || Failed) return;
            var clock = GameClock.I;

            // Vadesi geçen taksit
            var due = NextDue;
            if (due != null && clock.Day > due.DueDay)
            {
                MissInstallment(due);
                return;
            }

            // Hatırlatmalar
            if (due != null)
            {
                int left = DaysUntil(due);
                if (left == 7)
                    EventManager.I.Notify($"{Banka} SMS: \"Kredi taksitinizin ({Ft(due.Remaining)}) vadesine 7 gün kaldı: {due.DueText}.\"");
                else if (left == 1)
                    EventManager.I.Push(new GameEvent("Yarın taksit günü",
                        $"{Temsilci} ({Banka}) e-posta attı: \"Yarın {due.DueText}, {Ft(due.Remaining)} tutarındaki taksitinizin son günü. " +
                        "Hesabınızda bakiye bulundurmanızı rica ederiz.\"\n\n" +
                        $"Kasanda {Ft(EconomyManager.I.money)} var.")
                        .Add($"Şimdi öde ({Ft(due.Remaining)})", () => Pay(due.Remaining))
                        .Add("Yarına kadar biraz daha kazanırım", null));
            }

            StoryBeats();
        }

        void StoryBeats()
        {
            int total = 0;
            foreach (var c in CourierManager.I.Couriers) total += c.deliveries;
            int[] marks = { 100, 500, 1000, 2500 };
            foreach (int m in marks)
            {
                if (deliveriesMilestone < m && total >= m)
                {
                    deliveriesMilestone = m;
                    string msg = m == 100 ? $"{EskiArkadas} yazdı: \"Senin kuryeleri gördüm abi! 100 teslimat olmuş, {EskiSirket}'te herkes seni konuşuyor.\""
                               : m == 500 ? $"{EskiArkadas}: \"500 teslimat ha? {EskiSirket}'te bölge müdürü adını sordu, tedirgin olmuşlar.\""
                               : m == 1000 ? $"{EskiArkadas}: \"Bin teslimat! Bizim duraktaki yarı kuryeler sana geçmek istiyor.\""
                               : $"{Temsilci} ({Banka}): \"Şubede sizin şirketi örnek gösteriyoruz. Büyüme kredisi düşünürseniz kapımız açık.\"";
                    EventManager.I.Notify(msg);
                }
            }
        }

        /// <summary>Krediye ödeme (erken, kısmi veya tamamı). Önce en eski taksit kapanır.</summary>
        public bool Pay(long amount)
        {
            if (Finished || Failed || amount <= 0) return false;
            amount = System.Math.Min(amount, Remaining);
            if (EconomyManager.I.money < amount)
            {
                EventManager.I.Notify($"Kasada yeterli para yok ({Ft(amount)} gerekiyor).");
                return false;
            }

            EconomyManager.I.PayLoan(amount, $"{Banka} kredi taksiti");
            TotalPaid += amount;

            long left = amount;
            foreach (var t in Schedule)
            {
                if (t.closed || left <= 0) continue;
                long take = System.Math.Min(left, t.Remaining);
                t.paid += take;
                left -= take;
                if (t.Remaining == 0)
                {
                    t.closed = true;
                    EventManager.I.Notify($"{t.DueText} kredi taksiti ödendi. {Banka}: \"Ödemeniz için teşekkür ederiz.\"");
                }
            }
            // Erken ödenen fazlalık sonraki taksitlerden düşülmüş olur
            if (Remaining <= 0) Finish();
            return true;
        }

        void MissInstallment(Installment t)
        {
            long unpaid = t.Remaining;
            t.closed = true;
            Trust--;
            CompanyProfile.AddReputation(-0.1f);

            Installment next = null;
            foreach (var s in Schedule) if (!s.closed) { next = s; break; }

            if (Trust <= 0)
            {
                Fail($"{t.DueText} taksitini de ödeyemedin. {Banka} krediyi geri çağırdı.");
                return;
            }

            if (next == null)
            {
                // Son taksit kaçtı: banka 30 gün daha süre verir
                int gm = Mathf.Min(12, t.month + 1);
                next = new Installment { month = gm, day = GameClock.MonthLength(gm), amount = 0 };
                Schedule.Add(next);
            }
            next.amount += unpaid;

            string mood = Trust == 2
                ? $"{Temsilci} aradı, sesi nazik ama kesindi:\n\"Bu sefer gecikme notu düştük. Bir sonraki taksitte sorun çıkmasın lütfen.\""
                : $"{Banka}'tan imzalı bir mektup geldi:\n\"Son uyarıdır. Bir taksit daha ödenmezse kredi sözleşmesi feshedilir ve şirket varlıklarına haciz başlatılır.\"";

            EventManager.I.Push(new GameEvent("Taksit kaçtı!",
                $"{t.DueText} taksitinden {Ft(unpaid)} ödenemedi.\n\n{mood}\n\n" +
                $"Ödenmeyen tutar {next.DueText} taksitine eklendi (yeni taksit {Ft(next.Remaining)}). " +
                $"Kredi notu: {Trust}/{maxTrust}")
                .Add("Telafi edeceğim.", null));
        }

        void Finish()
        {
            Finished = true;
            foreach (var t in Schedule) t.closed = true;
            CompanyProfile.AddReputation(0.3f);
            foreach (var c in CourierManager.I.Couriers) c.morale = Mathf.Min(1f, c.morale + 0.1f);

            string ending = Trust == maxTrust
                ? "Tek bir taksit bile kaçırmadın."
                : "Kolay olmadı, birkaç kez sendeledin ama sonunda başardın.";
            EventManager.I.Push(new GameEvent("Kredi bitti!",
                $"Son taksiti yatırdığın akşam {EskiArkadas} seni eski durağa çağırdı, kuryeler sana kahve ısmarladı. {ending}\n\n" +
                $"\"Bizden biri patron oldu ve {EskiSirket}'e kafa tutuyor,\" dedi {EskiArkadas}. \"Unutma nereden geldiğini.\"\n\n" +
                $"{Banka} kredisi kapandı. Bundan sonrası tamamen senin: şirketini istediğin kadar büyüt.")
                .Add("Devam et (serbest mod)", null));
        }

        void Fail(string reason)
        {
            Failed = true;
            EventManager.I.Push(new GameEvent("Son: Şirket kapandı",
                reason + "\n\nKuryelere veda ettin, dükkanlarla sözleşmeleri bitirdin. Belki bir gün yeniden denersin... " +
                $"{EskiSirket} kapıyı açık bıraktı: \"İstersen yine kuryelik yapabilirsin.\"\n\n" +
                $"Ödenen: {Ft(TotalPaid)} / {Ft(Toplam)}")
                .Add("Yeniden başla", () => GameBootstrap.Restart())
                .Add("Devam et (sadece test için)", null));
        }

        public string TopBarText
        {
            get
            {
                if (!ready) return "";
                if (Finished) return "Kredi: ödendi";
                if (Failed) return "Kredi: ödenemedi";
                var n = NextDue;
                if (n == null) return $"Kredi: {Ft(Remaining)}";
                return $"Kredi: {Ft(Remaining)} · Taksit {Ft(n.Remaining)} → {n.DueText} ({DaysUntil(n)} gün) · Not {Trust}/{maxTrust}";
            }
        }
    }
}
