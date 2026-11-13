using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Aile borcunun bir taksiti.</summary>
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
    /// Hikaye: Şirketi annenle babanın birikimi olan 1 000 000 Ft ile kurdun.
    /// Borcu 4 aylık taksitle ödemen gerekiyor (Taksimetre'deki gibi süre baskısı).
    /// Taksit kaçırırsan ailenin güveni azalır; güven biterse şirket kapanır.
    /// Erken veya kısmi ödeme yapılabilir. Borç anapara ödemesidir: gider değildir, vergiden düşülmez.
    /// </summary>
    public class FamilyLoan : MonoBehaviour
    {
        public static FamilyLoan I { get; private set; }

        [Header("Borç")]
        public long principal = 1000000;
        // Mart–Temmuz sonu (5 taksit). Eskiden ilk taksit Nisan sonundaydı ve borç oyunun başında hiç baskı yapmıyordu.
        public int[] dueMonths = { 3, 4, 5, 6, 7 };
        public int maxTrust = 3;

        public readonly List<Installment> Schedule = new List<Installment>();
        public int Trust { get; private set; }
        public long TotalPaid { get; private set; }
        public bool Finished { get; private set; }
        public bool Failed { get; private set; }

        int deliveriesMilestone;
        bool ready;

        static string Ft(long v) => GameData.Ft(v);

        public long Remaining => Finished ? 0 : principal - TotalPaid;

        void Awake() { I = this; }

        // ---------- Kayıt ----------

        public LoanSave CaptureSave()
        {
            var s = new LoanSave
            {
                principal = principal, totalPaid = TotalPaid, trust = Trust, deliveriesMilestone = deliveriesMilestone,
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
            GameClock.I.OnNewDay += OnNewDay;
            ready = true;
        }

        public void Init()
        {
            Trust = maxTrust;
            long each = principal / dueMonths.Length;
            for (int i = 0; i < dueMonths.Length; i++)
            {
                int m = dueMonths[i];
                long amount = i == dueMonths.Length - 1 ? principal - each * (dueMonths.Length - 1) : each;
                Schedule.Add(new Installment { month = m, day = GameClock.MonthLength(m), amount = amount });
            }
            GameClock.I.OnNewDay += OnNewDay;
            ready = true;

            var first = Schedule[0];
            EventManager.I.Push(new GameEvent("Bir hayalin başlangıcı",
                "Yıllarca başkalarının siparişini taşıdın. Yağmurda, karda, gece yarısı...\n" +
                "Bir akşam yemekte annenle babana kendi kurye şirketini kurmak istediğini söyledin.\n\n" +
                "Baban uzun süre sustu. Sonra annene baktı ve çekmeceden banka kartını çıkardı:\n" +
                $"\"Bu bizim emeklilik birikimimiz, {Ft(principal)}. Sana güveniyoruz evladım. " +
                "Ama bu para bize lazım, Temmuz sonuna kadar geri ödemen gerekiyor.\"\n\n" +
                $"Taksitler: her ayın sonunda {Ft(each)} ({first.DueText}, ...). " +
                $"Erken ödeyebilirsin. Taksit kaçırırsan ailenin güveni sarsılır; {maxTrust} kez kaçırırsan şirketi kapatırsın.")
                .Add("Sizi utandırmayacağım.", null));
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
                    EventManager.I.Notify($"Annen aradı: \"Taksite 1 hafta kaldı, {Ft(due.Remaining)}. Kolay gelsin evladım.\"");
                else if (left == 1)
                    EventManager.I.Push(new GameEvent("Yarın taksit günü",
                        $"Baban mesaj attı: \"Yarın {due.DueText}. {Ft(due.Remaining)} bekliyoruz.\"\n\n" +
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
                    string msg = m == 100 ? "Annen: \"Komşulara anlattım, senin kuryeleri gördüler! 100 teslimat olmuş, maşallah.\""
                               : m == 500 ? "Baban: \"500 teslimat ha? Ben ilk dükkanımı açtığımda bu kadar müşterim yoktu.\""
                               : m == 1000 ? "Annen: \"Bin teslimat! Babana söyledim, gözleri doldu.\""
                               : "Baban: \"Artık mahallede herkes senin şirketini konuşuyor.\"";
                    EventManager.I.Notify(msg);
                }
            }
        }

        /// <summary>Borca ödeme (erken, kısmi veya tamamı). Önce en eski taksit kapanır.</summary>
        public bool Pay(long amount)
        {
            if (Finished || Failed || amount <= 0) return false;
            amount = System.Math.Min(amount, Remaining);
            if (EconomyManager.I.money < amount)
            {
                EventManager.I.Notify($"Kasada yeterli para yok ({Ft(amount)} gerekiyor).");
                return false;
            }

            EconomyManager.I.PayLoan(amount, "Aile borcu ödemesi");
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
                    EventManager.I.Notify($"{t.DueText} taksiti ödendi. Baban: \"Aferin evladım.\"");
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
                Fail($"{t.DueText} taksitini de ödeyemedin. Baban artık sabredemiyor.");
                return;
            }

            if (next == null)
            {
                // Son taksit kaçtı: aile 30 gün daha süre verir
                int gm = Mathf.Min(12, t.month + 1);
                next = new Installment { month = gm, day = GameClock.MonthLength(gm), amount = 0 };
                Schedule.Add(next);
            }
            next.amount += unpaid;

            string mood = Trust == 2
                ? "Annen telefonda sesini yükseltmedi ama hayal kırıklığını hissettin:\n\"Oğlum, babanın ilaç parası bile o hesaptaydı...\""
                : "Baban telefonu açar açmaz konuştu:\n\"Son şansın. Bir daha olursa şirketi kapatıp bize parayı geri vereceksin.\"";

            EventManager.I.Push(new GameEvent("Taksit kaçtı!",
                $"{t.DueText} taksitinden {Ft(unpaid)} ödenemedi.\n\n{mood}\n\n" +
                $"Ödenmeyen tutar {next.DueText} taksitine eklendi (yeni taksit {Ft(next.Remaining)}). " +
                $"Aile güveni: {Trust}/{maxTrust}")
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
            EventManager.I.Push(new GameEvent("Borç bitti!",
                $"Son ödemeyi yaptığın akşam annenle baban seni yemeğe çağırdı. {ending}\n\n" +
                "Baban kadehini kaldırdı: \"Evladımın şirketine! Artık bizim değil, senin paran çalışıyor.\"\n\n" +
                "Aile borcu kapandı. Bundan sonrası tamamen senin: şirketini istediğin kadar büyüt.")
                .Add("Devam et (serbest mod)", null));
        }

        void Fail(string reason)
        {
            Failed = true;
            EventManager.I.Push(new GameEvent("Son: Şirket kapandı",
                reason + "\n\nAilenle oturup konuştunuz. Kuryelere veda ettin, dükkanlarla sözleşmeleri bitirdin. " +
                "Belki bir gün yeniden denersin...\n\n" +
                $"Ödenen: {Ft(TotalPaid)} / {Ft(principal)}")
                .Add("Yeniden başla", () => GameBootstrap.Restart())
                .Add("Devam et (sadece test için)", null));
        }

        public string TopBarText
        {
            get
            {
                if (!ready) return "";
                if (Finished) return "Aile borcu: ödendi";
                if (Failed) return "Aile borcu: ödenemedi";
                var n = NextDue;
                if (n == null) return $"Aile borcu: {Ft(Remaining)}";
                return $"Aile borcu: {Ft(Remaining)} · Taksit {Ft(n.Remaining)} → {n.DueText} ({DaysUntil(n)} gün) · Güven {Trust}/{maxTrust}";
            }
        }
    }
}
