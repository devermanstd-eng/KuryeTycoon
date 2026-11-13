using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Zor kararlar: iki seçeneği de olumsuz olan olay kartları (kuryeler zam istiyor, vergi denetimi, viral video...).
    /// Oyuncu "hangisi daha az kötü" diye seçer. Nadiren gelir: ilk günlerde gelmez, iki zor karar arasında en az 5 gün olur.
    /// EventManager her gece yarısı TryRoll'u çağırır. Denemek için: Play sırasında Hierarchy'de EventManager'ı seç,
    /// Inspector'da bileşenin ⋮ menüsünden "Zor karar olayı dene".
    /// Yeni olay eklemek için: aşağıya bir metot yaz (şartı tutmazsa null döndürsün) ve Builders listesine ekle.
    /// </summary>
    public static class DilemmaEvents
    {
        const int FirstDay = 4;          // ilk günlerde gelmez (oyuncu oyunu öğreniyor)
        const int MinGapDays = 5;        // iki zor karar arasında en az kaç gün
        const float DailyChance = 0.12f; // şartlar uygunsa her gece çıkma ihtimali (ortalama ~12 günde bir)
        const int RecentMemory = 5;      // son çıkan kaç olay tekrar gelmesin

        static int lastDay = -999;
        static readonly List<int> recent = new List<int>();

        static string Ft(long v) => GameData.Ft(v);

        delegate GameEvent Builder();

        static readonly Builder[] Builders =
        {
            ZamTalebi, VergiDenetimi, DukkanKomisyonTalebi, ParkCezasi, DepodaHirsizlik, KayipPahaliSiparis,
        };

        /// <summary>Gece yarısı çağrılır: şartlar uygunsa bir zor karar kartı çıkarır.</summary>
        public static void TryRoll()
        {
            var clock = GameClock.I;
            if (clock == null || clock.CompanyDay < FirstDay) return;
            if (clock.Day - lastDay < MinGapDays) return;
            if (Random.value > DailyChance) return;
            RollNow();
        }

        /// <summary>Hemen bir zor karar kartı çıkarır (test için de kullanılır). Uygun olay yoksa false.</summary>
        public static bool RollNow()
        {
            var order = new List<int>();
            for (int i = 0; i < Builders.Length; i++)
                if (!recent.Contains(i)) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int t = order[i]; order[i] = order[j]; order[j] = t;
            }

            foreach (int i in order)
            {
                var e = Builders[i]();
                if (e == null) continue;   // şartı tutmadı (ör. yeterli kurye ya da anlaşmalı dükkan yok)
                EventManager.I.Push(e);
                if (GameClock.I != null) lastDay = GameClock.I.Day;
                recent.Add(i);
                if (recent.Count > RecentMemory) recent.RemoveAt(0);
                return true;
            }
            return false;
        }

        // ================================================================== yardımcılar

        static List<Courier> Fleet => CourierManager.I != null ? CourierManager.I.Couriers : new List<Courier>();

        static List<Courier> FreeCouriers()
        {
            var list = new List<Courier>();
            foreach (var c in Fleet)
                if (c.OrderCount == 0 && c.State != CourierState.Izinli) list.Add(c);
            return list;
        }

        static Courier Pick(List<Courier> list) => list.Count > 0 ? list[Random.Range(0, list.Count)] : null;

        static List<Courier> LowestMorale(List<Courier> from, int n)
        {
            var list = new List<Courier>(from);
            list.Sort((a, b) => a.morale.CompareTo(b.morale));
            if (list.Count > n) list.RemoveRange(n, list.Count - n);
            return list;
        }

        static List<Courier> MostDeliveries(List<Courier> from, int n)
        {
            var list = new List<Courier>(from);
            list.Sort((a, b) => b.deliveries.CompareTo(a.deliveries));
            if (list.Count > n) list.RemoveRange(n, list.Count - n);
            return list;
        }

        static string Names(List<Courier> list) => string.Join(", ", list.ConvertAll(c => c.ShortName));

        static void MoraleAll(float delta)
        {
            foreach (var c in Fleet) c.morale = Mathf.Clamp01(c.morale + delta);
        }

        static Shop RandomContractedShop(System.Predicate<Shop> extra = null)
        {
            var map = CityMap.I;
            if (map == null) return null;
            var list = new List<Shop>();
            foreach (var s in map.Shops)
                if (s.contracted && !s.own && map.IsUnlocked(s.tile) && (extra == null || extra(s))) list.Add(s);
            return list.Count > 0 ? list[Random.Range(0, list.Count)] : null;
        }

        static void Spend(long amount, string label) => EconomyManager.I.Spend(amount, label, ExpenseCategory.Diger);

        // ================================================================== olaylar

        /// <summary>
        /// Kuryeler paket başı aldıkları paranın artmasını istiyor. Kabul edilirse 6 hafta boyunca bütün teslimat
        /// ücretleri +%10 (CrisisManager'ın ücret etkisi olarak; üst çubukta görünür ve kayda yazılır).
        /// Reddedilirse tüm kuryelerin morali düşer.
        /// </summary>
        static GameEvent ZamTalebi()
        {
            var fleet = Fleet;
            var crisis = CrisisManager.I;
            if (fleet.Count < 3 || crisis == null) return null;
            // Grev anlaşmasıyla ya da önceki artışla üst üste binmesin
            if (crisis.HasEffect("grev_zam") || crisis.HasEffect("grev_uzlasma") || crisis.HasEffect("paket_ucret")) return null;
            if (crisis.AvgMorale() >= Denge.ZamMoralSiniri) return null;   // memnun kuryeler zam istemez
            const float raise = 0.10f;
            const int weeks = 6;
            return new GameEvent("Kuryeler paket başı ücret artışı istiyor",
                    "Kuryeler WhatsApp grubunda birleşti: paket başına aldıkları paranın artan fiyatlara yetişmediğini söylüyorlar. " +
                    "Paket başı ücret artmazsa kuryelerin hepsinin morali düşecek; mutsuz kuryeler uygulamayı daha az açar.")
                .Add($"Kabul et: {weeks} hafta boyunca paket başı ücretler +%{raise * 100f:0} (her teslimat daha pahalı)", () =>
                {
                    crisis.AddEffect(new CrisisEffect
                    {
                        id = "paket_ucret",
                        title = $"Paket başı ücret artışı: +%{raise * 100f:0}",
                        pay = 1f + raise,
                    }, weeks * 7);
                    MoraleAll(0.05f);
                    EventManager.I.Notify($"Paket başı ücretler {weeks} hafta boyunca %{raise * 100f:0} arttı.");
                })
                .Add("Reddet: tüm kuryelerin morali −12", () =>
                {
                    MoraleAll(-0.12f);
                    EventManager.I.Notify("Paket başı ücret artışı reddedildi; kuryelerin morali düştü.");
                });
        }

        /// <summary>Vergi denetimi: uzlaşıp cezayı ödersin ya da avukatla itiraz edip kumar oynarsın.</summary>
        static GameEvent VergiDenetimi()
        {
            var eco = EconomyManager.I;
            if (eco.money < 100000) return null;
            long penalty = GameData.Round10(Mathf.Max(25000f, eco.money * 0.03f));
            const int lawyer = 15000;
            return new GameEvent("Vergi müfettişi kapıda",
                    "Vergi müfettişi geçen ayın kurye hakedişlerinde eksik fatura buldu ve ceza kesmek istiyor. " +
                    "Uzlaşırsan iş hemen kapanır. İtiraz edersen avukat tutman gerekir: kazanabilirsin de, cezayı büyütebilirsin de.")
                .Add($"Uzlaş: cezayı hemen öde (−{Ft(penalty)})", () => eco.SpendPenalty(penalty, "Vergi uzlaşma cezası"))
                .Add($"İtiraz et: avukat −{Ft(lawyer)}; %50 ihtimalle kaybedersin, o zaman ceza −{Ft(penalty * 3 / 2)} ve itibar düşer", () =>
                {
                    Spend(lawyer, "Avukat (vergi itirazı)");
                    if (Random.value < 0.5f)
                        EventManager.I.Notify("Vergi itirazını kazandın! Sadece avukat ücreti ödendi.");
                    else
                    {
                        eco.SpendPenalty(penalty * 3 / 2, "Vergi cezası (itiraz kaybedildi)");
                        CompanyProfile.AddReputation(-0.05f);
                        EventManager.I.Notify($"Vergi itirazını kaybettin: {Ft(penalty * 3 / 2)} ceza ödendi.");
                    }
                });
        }

        /// <summary>Büyük dükkan komisyon indirimi istiyor, yoksa ilişki bozulur.</summary>
        static GameEvent DukkanKomisyonTalebi()
        {
            var shop = RandomContractedShop(s => s.Commission >= 0.1f);
            if (shop == null) return null;
            return new GameEvent("Dükkan komisyon indirimi istiyor",
                    $"{shop.Name}'ın sahibi aradı: rakip platform ona daha düşük komisyon teklif etmiş. " +
                    $"Senin komisyonun (şu an %{shop.Commission * 100f:0}) inmezse rakiple çalışmayı düşündüğünü söylüyor.")
                .Add("Kabul et: bu dükkanın komisyonu kalıcı olarak −%3 (her siparişte daha az kazanırsın)", () =>
                {
                    shop.commissionBonus -= 0.03f;
                    shop.satisfaction = Mathf.Min(100f, shop.satisfaction + 5f);
                })
                .Add("Reddet: dükkanın memnuniyeti −30 (sözleşmeyi bitirebilir)", () =>
                {
                    shop.satisfaction = Mathf.Max(0f, shop.satisfaction - 30f);
                    EventManager.I.Notify($"{shop.Name} indirimi reddetmene kızdı. Memnuniyet %{shop.satisfaction:0}.");
                });
        }

        /// <summary>Belediye kaldırıma park eden kuryeler için toplu ceza kesti.</summary>
        static GameEvent ParkCezasi()
        {
            var fleet = Fleet;
            if (fleet.Count < 2) return null;
            const int perCourier = 1500;
            long fine = System.Math.Max(5000L, (long)fleet.Count * perCourier);
            return new GameEvent("Belediyeden toplu park cezası",
                    "Belediye, teslimat sırasında kaldırıma ve bisiklet yoluna park eden kuryeler yüzünden platformuna toplu ceza kesti. " +
                    "Kuryeler serbest çalışıyor ama ceza senin adına geldi.\n" +
                    "Cezayı kim ödeyecek?")
                .Add($"Şirket ödesin (−{Ft(fine)})", () => Spend(fine, "Belediye park cezası"))
                .Add($"Kuryelerin hakedişinden kes (kişi başı {Ft(perCourier)}): kuryelerin morali düşer", () =>
                {
                    Spend(fine, "Belediye park cezası (kuryelerden kesildi)");
                    foreach (var c in Fleet)
                    {
                        long taken = System.Math.Min(c.balance, (long)perCourier);
                        c.balance -= taken;   // şirketin kuryeye borcu azalır: ceza fiilen kuryeden çıkar
                        c.morale = Mathf.Max(0f, c.morale - 0.08f);
                        if (taken > 0) c.AddInbox($"Belediye park cezası: {Ft(taken)} hesabından düşüldü.");
                    }
                });
        }



        /// <summary>Ekipman deposundan hırsızlık.</summary>
        static GameEvent DepodaHirsizlik()
        {
            if (Fleet.Count < 3) return null;
            const int cam = 25000, lost = 10000;
            return new GameEvent("Ekipman noktasında hırsızlık",
                    $"Ekipman teslim noktasından termal çantalar ve montlar kayboldu (yaklaşık {Ft(lost)}). Kapıda zorlama izi yok; " +
                    "ekipman almaya gelen kuryelerden biri olabilir.")
                .Add($"Kamera ve kilit sistemi kur (−{Ft(cam)})", () => Spend(cam, "Depo güvenlik sistemi"))
                .Add("Ekipman alan her kuryeye kimlik ve çanta kontrolü yap: kuryelerin morali düşer", () => MoraleAll(-0.1f));
        }

        /// <summary>Pahalı bir sipariş yanlış adrese teslim edildi.</summary>
        static GameEvent KayipPahaliSiparis()
        {
            var c0 = Pick(FreeCouriers());
            if (c0 == null) return null;
            const int value = 24000;
            return new GameEvent("Pahalı sipariş kayboldu",
                    $"Kurye {c0.courierName} büyük bir market siparişini yanlış apartmana bırakmış; kimse geri vermiyor. " +
                    $"Müşteri {Ft(value)} iade istiyor. Kural olarak kurye kaynaklı sorunda iade kuryenin hakedişinden düşülür.")
                .Add($"İadeyi şirket ödesin (−{Ft(value)})", () => EconomyManager.I.Spend(value, "Kayıp sipariş iadesi", ExpenseCategory.Iade))
                .Add($"Yarı yarıya: {Ft(value / 2)} kuryenin hakedişinden düşülsün, kuryenin morali düşer", () =>
                {
                    EconomyManager.I.Spend(value, "Kayıp sipariş iadesi (yarısı kuryeden)", ExpenseCategory.Iade);
                    long taken = System.Math.Min(c0.balance, (long)(value / 2));
                    c0.balance -= taken;
                    c0.morale = Mathf.Max(0f, c0.morale - 0.08f);
                    c0.AddInbox($"Kayıp sipariş: {Ft(taken)} hesabından düşüldü.");
                });
        }

    }
}
