using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    public enum PhoneKind { Bilgi, Karar, Yorum }

    /// <summary>Telefona düşen mesaj: kuryeden bilgi, karar isteyen mesaj ya da müşteri yorumu.</summary>
    [System.Serializable]
    public class PhoneMessage
    {
        public PhoneKind kind;
        public string from;
        public string text;
        public string timeText;
        public float time;            // GameClock.TotalMinutes
        public int stars;             // yorumlarda 1–5
        public bool viral;
        public bool read;
        public bool answered;
        public string answer = "";
        public string shopName = "";

        // Karar mesajı (kayda yazılmaz: kayıttan sonra cevaplanmış sayılır)
        [System.NonSerialized] public List<string> optionTexts;
        [System.NonSerialized] public List<System.Action> optionActions;
        [System.NonSerialized] public int defaultOption;
        [System.NonSerialized] public float expiresAt;

        public bool Pending => kind == PhoneKind.Karar && !answered && optionActions != null;
    }

    [System.Serializable]
    public class MessagesSave
    {
        public List<PhoneMessage> messages = new List<PhoneMessage>();
        public float viralUntil;
        public float viralFactor = 1f;
        public int reviewsTotal;
        public float starsTotal;
    }

    /// <summary>
    /// Kurye mesajları ve müşteri yorumları (telefondaki Mesajlar penceresi):
    ///  • Kuryeler çalışırken mesaj atar: trafik gecikmesi, müşteri telefonu açmıyor, dükkanda sıra, bahşiş sevinci.
    ///  • Bazı mesajlar karar ister: mola isteği, yağmurda eve gitmek, telefon şarjı bitiyor, lastik patladı. 30 dakika (oyun)
    ///    cevap vermezsen kurye kendi karar verir.
    ///  • Teslimatların ~%15'inde müşteri kısa bir yorum ve yıldız bırakır. Kötü yorum (%3) viral olabilir: itibar düşer ve
    ///    2 gün siparişler azalır. Çok iyi yorum (%2) da viral olabilir: itibar ve siparişler artar.
    /// </summary>
    public class CourierMessages : MonoBehaviour
    {
        public static CourierMessages I { get; private set; }

        public readonly List<PhoneMessage> Messages = new List<PhoneMessage>();
        public const int MaxMessages = 80;
        float nextCheck;
        float viralUntil;
        float viralFactor = 1f;
        public int reviewsTotal;
        public float starsTotal;

        void Awake() { I = this; }

        public void Init()
        {
            nextCheck = GameClock.I.TotalMinutes + 20f;
        }

        public int Unread { get { int n = 0; foreach (var m in Messages) if (!m.read) n++; return n; } }
        public int PendingCount { get { int n = 0; foreach (var m in Messages) if (m.Pending) n++; return n; } }
        public float AverageStars => reviewsTotal > 0 ? starsTotal / reviewsTotal : 0f;

        /// <summary>Viral yorumun siparişlere etkisi (2 gün).</summary>
        public static float ReviewDemandFactor
        {
            get
            {
                if (I == null || GameClock.I == null) return 1f;
                return GameClock.I.TotalMinutes < I.viralUntil ? I.viralFactor : 1f;
            }
        }

        public bool ViralActive => GameClock.I != null && GameClock.I.TotalMinutes < viralUntil;
        public float ViralFactor => viralFactor;

        static string Now => GameClock.I == null ? "" : $"{GameClock.I.Hour:00}:{GameClock.I.Minute:00}";

        PhoneMessage Add(PhoneKind kind, string from, string text)
        {
            var m = new PhoneMessage { kind = kind, from = from, text = text, time = GameClock.I.TotalMinutes, timeText = Now };
            Messages.Insert(0, m);
            while (Messages.Count > MaxMessages)
            {
                // Cevap bekleyen mesajı silme
                int idx = Messages.FindLastIndex(x => !x.Pending);
                if (idx < 0) break;
                Messages.RemoveAt(idx);
            }
            return m;
        }

        void Info(Courier c, string text, bool toast = false)
        {
            Add(PhoneKind.Bilgi, c.courierName, text);
            if (toast) EventManager.I.Notify($"Mesaj — {c.ShortName}: {text}");
        }

        void Ask(Courier c, string text, int defaultOption, params (string, System.Action)[] options)
        {
            var m = Add(PhoneKind.Karar, c.courierName, text);
            m.optionTexts = new List<string>();
            m.optionActions = new List<System.Action>();
            foreach (var o in options) { m.optionTexts.Add(o.Item1); m.optionActions.Add(o.Item2); }
            m.defaultOption = Mathf.Clamp(defaultOption, 0, options.Length - 1);
            m.expiresAt = GameClock.I.TotalMinutes + 30f;
            EventManager.I.Notify($"Mesaj — {c.ShortName}: {text} (cevap bekliyor)");
        }

        public void Answer(PhoneMessage m, int option, bool auto = false)
        {
            if (!m.Pending || option < 0 || option >= m.optionActions.Count) return;
            m.answered = true;
            m.read = true;
            m.answer = (auto ? "Cevap gelmedi, kurye kendi karar verdi: " : "Cevabın: ") + m.optionTexts[option];
            var act = m.optionActions[option];
            m.optionActions = null;
            act?.Invoke();
        }

        public void MarkAllRead() { foreach (var m in Messages) m.read = true; }

        // ---------------------------------------------------------------- kurye mesajları

        void Update()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || GameClock.I == null) return;
            float now = GameClock.I.TotalMinutes;

            // Süresi dolan kararlar: kurye kendi karar verir
            foreach (var m in Messages.ToArray())
                if (m.Pending && now >= m.expiresAt) Answer(m, m.defaultOption, true);

            if (now < nextCheck) return;
            nextCheck = now + Random.Range(25f, 45f);
            var cm = CourierManager.I;
            if (cm == null) return;
            var online = new List<Courier>();
            foreach (var c in cm.Couriers) if (c.IsOnline) online.Add(c);
            if (online.Count == 0) return;
            // Kurye sayısı arttıkça biraz daha sık, ama ekran mesaja boğulmasın
            if (Random.value > Mathf.Min(0.6f, 0.22f + online.Count * 0.03f)) return;
            var who = online[Random.Range(0, online.Count)];
            Generate(who);
        }

        void Generate(Courier c)
        {
            var w = WeatherSystem.I;
            bool rainy = w != null && (w.IsRainy || w.Current == WeatherKind.Karli);
            bool pendingAlready = PendingCount > 0;
            var st = c.State;

            // Karar isteyen mesajlar (aynı anda en fazla bir tane)
            if (!pendingAlready && c.OrderCount == 0 && st == CourierState.Bosta)
            {
                if (c.fatigue > 0.55f && Random.value < 0.6f)
                {
                    Ask(c, "Çok yoruldum, 20 dakika mola verebilir miyim? Bir şeyler atıştırıp dinleneceğim.", 0,
                        ("Tabii, dinlen", () => { c.TakeBreak(20f, "Mola: dinleniyor"); c.morale = Mathf.Clamp01(c.morale + 0.05f); }),
                        ("Şimdi olmaz, yoğunluk var", () => { c.morale = Mathf.Clamp01(c.morale - 0.07f); c.fatigue = Mathf.Min(1f, c.fatigue + 0.05f); }));
                    return;
                }
                if (rainy && c.V.exposed && !c.Has(EquipmentKind.Yagmurluk) && Random.value < 0.6f)
                {
                    Ask(c, "Sırılsıklam oldum, yağmurluğum yok. Bugünlük eve gidebilir miyim?", 0,
                        ("Git, geçmiş olsun", () => { c.EndShiftEarly("Yağmurda ıslandı, eve gitti"); c.morale = Mathf.Clamp01(c.morale + 0.03f); }),
                        ("Yağmurluk gönder (depozitolu)", () => { CourierManager.I.IssueGear(c, EquipmentKind.Yagmurluk, true); c.morale = Mathf.Clamp01(c.morale + 0.02f); }),
                        ("Biraz daha dayan", () => { c.morale = Mathf.Clamp01(c.morale - 0.08f); }));
                    return;
                }
                if (!c.Has(EquipmentKind.Powerbank) && Random.value < 0.25f)
                {
                    Ask(c, "Telefonumun şarjı %9 kaldı, birazdan kapanacak.", 1,
                        ("Powerbank gönder (depozitolu)", () => CourierManager.I.IssueGear(c, EquipmentKind.Powerbank, true)),
                        ("Molada şarj et (30 dk)", () => c.TakeBreak(30f, "Telefonunu şarj ediyor")));
                    return;
                }
                if ((c.vehicle == VehicleKind.Bisiklet || c.vehicle == VehicleKind.EScooter || c.vehicle == VehicleKind.Motosiklet) && Random.value < 0.12f)
                {
                    string part = c.vehicle == VehicleKind.EScooter ? "Scooterın lastiği patladı" : c.vehicle == VehicleKind.Motosiklet ? "Motorun lastiği patladı" : "Bisikletin lastiği patladı";
                    Ask(c, $"{part}! Yakında bir tamirci var, 35–40 dakika sürer.", 0,
                        ("Tamir ettir, bekleriz", () => c.TakeBreak(38f, "Lastik tamiri")),
                        ("Bugünlük eve git", () => { c.EndShiftEarly("Lastiği patladı"); c.morale = Mathf.Clamp01(c.morale + 0.01f); }));
                    return;
                }
            }

            // Bilgi mesajları
            switch (st)
            {
                case CourierState.MusteriyeGidiyor:
                    if (rainy) Info(c, Pick("Yağmurdan yollar kaygan, yavaş gidiyorum. Birkaç dakika gecikebilirim.",
                                            "Yağmur çok şiddetli, paketi korumaya çalışıyorum."));
                    else Info(c, Pick("Trafik çok sıkışık, siparişe ~5 dakika gecikeceğim.",
                                      "Yol çalışması var, başka yoldan dolaşıyorum.",
                                      "Siparişi aldım, müşteriye gidiyorum. 10 dakikaya oradayım.",
                                      "Köprüde kaza var, trafik durdu. Biraz gecikeceğim."));
                    break;
                case CourierState.AdresSorunu:
                    Info(c, Pick("Müşteri telefonu açmıyor, kapıda bekliyorum.",
                                 "Adres yanlış işaretlenmiş galiba, binayı bulamıyorum. Destek müşteriyi arayabilir mi?",
                                 "Zil çalışmıyor, müşteriye mesaj attım, bekliyorum."), true);
                    break;
                case CourierState.Bekliyor:
                case CourierState.DukkanaGidiyor:
                    Info(c, Pick("Dükkanda sipariş hâlâ hazır değil, 10 dakikadır bekliyorum.",
                                 "Dükkanda çok kalabalık var, sıradayım.",
                                 "Dükkan siparişi yanlış hazırlamış, düzeltmelerini bekliyorum."));
                    break;
                case CourierState.KataCikiyor:
                    Info(c, Pick("Asansör bozuk, 6. kata merdivenle çıkıyorum", "Bina girişi kilitli, müşteri aşağı iniyor."));
                    break;
                case CourierState.Bosta:
                    if (c.morale > 0.8f) Info(c, Pick("Bugün işler iyi gidiyor, teşekkürler!", "Boştayım, yeni sipariş bekliyorum."));
                    else if (c.morale < 0.35f) Info(c, Pick("Bugün hiç sipariş düşmüyor, sıkıldım.", "Kazanç çok az bu ara... Biraz daha iş gelse iyi olur."), true);
                    else Info(c, "Boştayım, yeni sipariş bekliyorum.");
                    break;
                case CourierState.Mola:
                    Info(c, Pick("Mola veriyorum, birazdan devam edeceğim.", "Bir şeyler atıştırıyorum, 10 dakikaya buradayım."));
                    break;
            }
        }

        static string Pick(params string[] s) => s[Random.Range(0, s.Length)];

        // ---------------------------------------------------------------- teslimat sonrası: bahşiş mesajı ve müşteri yorumu

        public void OnDelivered(Order o, Courier c, float late)
        {
            if (o == null || c == null) return;
            if (o.tip >= 1000 && Random.value < 0.35f)
                Info(c, $"Müşteri {GameData.Ft(o.tip)} bahşiş verdi, çok mutlu oldum!");

            int stars = Mathf.Clamp(Mathf.RoundToInt(o.rating), 1, 5);
            float chance = stars <= 2 ? 0.3f : stars >= 5 ? 0.18f : 0.1f;
            if (Random.value > chance) return;
            Review(o, c, stars, late);
        }

        void Review(Order o, Courier c, int stars, float late)
        {
            string text = ReviewText(o, c, stars, late);
            var m = Add(PhoneKind.Yorum, o.customerName, text);
            m.stars = stars;
            m.shopName = o.shop != null ? o.shop.Name : "";
            reviewsTotal++;
            starsTotal += stars;

            // Viral olma ihtimali
            if (stars <= 2 && Random.value < 0.03f)
            {
                m.viral = true;
                CompanyProfile.AddReputation(-0.1f);
                viralFactor = 0.9f;
                viralUntil = GameClock.I.TotalMinutes + 2 * 1440f;
                EventManager.I.Notify($"Kötü yorum viral oldu! {o.customerName}: \"{text}\" — sosyal medyada yayıldı. İtibar −0,1, 2 gün siparişler ~%10 az.");
            }
            else if (stars >= 5 && Random.value < 0.02f)
            {
                m.viral = true;
                CompanyProfile.AddReputation(0.05f);
                viralFactor = 1.1f;
                viralUntil = GameClock.I.TotalMinutes + 2 * 1440f;
                EventManager.I.Notify($"Müşteri övgüsü viral oldu! {o.customerName}: \"{text}\" — itibar +0,05, 2 gün siparişler ~%10 fazla.");
            }
        }

        static string ReviewText(Order o, Courier c, int stars, float late)
        {
            string k = c.ShortName;
            var parts = new List<string>();
            if (stars >= 5)
            {
                parts.Add(Pick($"{k} çok hızlı ve kibar, teşekkürler!", "Süper hızlı teslimat", "Her şey mükemmeldi.",
                               $"Kurye {k} güler yüzlüydü, tavsiye ederim.", "Söylenen saatten önce geldi!"));
                if (o.shop != null && o.shop.def.hotFood && !o.deliveredCold) parts.Add(Pick("Yemek sıcacıktı.", "Yemek hâlâ dumanı tüterken geldi."));
                if (WeatherSystem.I != null && WeatherSystem.I.IsRainy) parts.Add("Bu yağmurda geldiği için ayrıca teşekkürler.");
            }
            else if (stars == 4)
            {
                parts.Add(Pick("Gayet iyiydi.", "Teslimat iyiydi, biraz daha hızlı olabilirdi.", "Kurye kibardı, sorun yok.", "Güzel, yine sipariş veririm."));
            }
            else if (stars == 3)
            {
                parts.Add(Pick("Fena değil ama biraz geç geldi.", "Ortalama bir teslimat.", "İdare eder."));
                if (o.deliveredCold) parts.Add("Yemek ılık gelmişti.");
            }
            else
            {
                if (late > 10f) parts.Add(Pick($"{Mathf.RoundToInt(late)} dakika geç geldi!", "Çok geç geldi, açlıktan öldük.", "Söylenen süreden çok sonra geldi."));
                if (o.deliveredCold) parts.Add(Pick("Yemek buz gibiydi.", "Pizza soğuk geldi, rezalet."));
                if (c.hygiene < Courier.HygieneLimit) parts.Add("Çanta çok kirliydi.");
                if (o.customerType == CustomerType.Zor) parts.Add(Pick("Kurye kapıda beni beklettirdi.", "Hiç memnun kalmadım."));
                if (parts.Count == 0) parts.Add(Pick("Hiç memnun kalmadım.", "Bir daha sipariş vermem.", "Kötü bir deneyimdi."));
            }
            return string.Join(" ", parts);
        }

        // ---------------------------------------------------------------- kayıt

        public MessagesSave CaptureSave()
        {
            var s = new MessagesSave { viralUntil = viralUntil, viralFactor = viralFactor, reviewsTotal = reviewsTotal, starsTotal = starsTotal };
            for (int i = 0; i < Messages.Count && i < 40; i++) s.messages.Add(Messages[i]);
            return s;
        }

        public void RestoreSave(MessagesSave s)
        {
            if (s == null) return;
            Messages.Clear();
            if (s.messages != null)
                foreach (var m in s.messages)
                {
                    if (m.kind == PhoneKind.Karar && !m.answered) { m.answered = true; m.answer = "Oyun kaydedildi, cevap verilmedi."; }
                    Messages.Add(m);
                }
            viralUntil = s.viralUntil;
            viralFactor = s.viralFactor <= 0f ? 1f : s.viralFactor;
            reviewsTotal = s.reviewsTotal;
            starsTotal = s.starsTotal;
        }
    }
}
