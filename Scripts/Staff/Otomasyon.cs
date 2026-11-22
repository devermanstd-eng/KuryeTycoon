using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Operasyon otomasyonu (Gelişim > Operasyon otomasyonu). Incremental oyunlarda olduğu gibi: önce elle yaptığın işi sonra
    /// bir sisteme devredersin, sen daha büyük kararlara geçersin. Her sabah 07:00'de dünün verisine bakar:
    ///  1. Vardiya asistanı: akşam / öğle yoğun saat primini iptal oranına göre açar-kapatır, yoğunluktan sipariş kaçıyorsa planlı teslimatı açar.
    ///  2. Kayıt asistanı: kurye yetmiyorsa bütün kayıtları açar, kuryeler çok boştaysa bisiklet kaydını kapatır.
    ///  3. Operasyon müdürü: dükkanda bekleme uzunsa en yoğun, yavaş mutfakla öncelikli hazırlık anlaşması yapar.
    /// Oyun kapalıyken kazanç (SenYokken) de otomasyon seviyesine bağlıdır.
    /// </summary>
    public class Otomasyon : MonoBehaviour
    {
        public static Otomasyon I { get; private set; }

        static readonly float[] VerimTablo = { 0.15f, 0.25f, 0.35f, 0.5f };
        static readonly float[] SaatTablo = { 4f, 8f, 12f, 16f };

        /// <summary>Oyun kapalıyken kazancın verimi: gerçek saat başına ortalama günlük net kârın bu kadarı.</summary>
        public static float Verim(int lv) => VerimTablo[Mathf.Clamp(lv, 0, 3)];
        public static float SaatSiniri(int lv) => SaatTablo[Mathf.Clamp(lv, 0, 3)];

        public static int Seviye => UpgradeManager.I != null ? UpgradeManager.I.Level(UpgradeKind.Otomasyon) : 0;

        int sonGun = -1;
        public int bugunIslem;
        public long toplamIslem;

        void Awake() { I = this; }

        public void Init() { sonGun = -1; bugunIslem = 0; toplamIslem = 0; }

        void Update()
        {
            var clk = GameClock.I;
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || clk == null || clk.Paused) return;
            if (clk.Hour < 7 || clk.Day == sonGun) return;
            sonGun = clk.Day;
            bugunIslem = 0;
            int lv = Seviye;
            if (lv <= 0) return;
            var son = SonGun();
            if (son == null) return;
            var yapilan = new System.Collections.Generic.List<string>();
            VardiyaAsistani(son, yapilan);
            if (lv >= 2) KayitAsistani(son, yapilan);
            if (lv >= 3) OperasyonMuduru(son, yapilan);
            bugunIslem = yapilan.Count;
            toplamIslem += yapilan.Count;
            if (yapilan.Count > 0)
                EventManager.I.Notify("Otomasyon: " + string.Join(" · ", yapilan) + ".");
        }

        static DayRecord SonGun()
        {
            var h = StatsHistory.I != null ? StatsHistory.I.Days : null;
            return h != null && h.Count > 0 ? h[h.Count - 1] : null;
        }

        static float OrtMoral
        {
            get
            {
                var cm = CourierManager.I;
                if (cm == null || cm.Couriers.Count == 0) return 1f;
                float t = 0f;
                foreach (var c in cm.Couriers) t += c.morale;
                return t / cm.Couriers.Count;
            }
        }

        static float IptalOrani(DayRecord d) =>
            d.deliveries + d.cancelled > 0 ? d.cancelled / (float)(d.deliveries + d.cancelled) : 0f;

        void VardiyaAsistani(DayRecord son, System.Collections.Generic.List<string> yapilan)
        {
            var camp = CampaignManager.I;
            float iptal = IptalOrani(son);
            if (camp != null)
            {
                if (!camp.aksamPrimi && iptal > 0.04f)
                {
                    camp.aksamPrimi = true;
                    camp.aksamPrimiYuzde = 0.2f;
                    yapilan.Add($"akşam primi açıldı (dün iptal %{iptal * 100f:0})");
                }
                else if (camp.aksamPrimi && iptal < 0.015f && OrtMoral > 0.6f)
                {
                    camp.aksamPrimi = false;
                    yapilan.Add("akşam primi kapatıldı (iptal yok, moral iyi)");
                }
                if (!camp.oglePrimi && iptal > 0.07f)
                {
                    camp.oglePrimi = true;
                    camp.oglePrimiYuzde = 0.15f;
                    yapilan.Add($"öğle primi açıldı (dün iptal %{iptal * 100f:0})");
                }
                else if (camp.oglePrimi && iptal < 0.02f && OrtMoral > 0.6f)
                {
                    camp.oglePrimi = false;
                    yapilan.Add("öğle primi kapatıldı");
                }
            }
            var hz = HizmetSeviyeleri.I;
            if (hz != null && !hz.planliAcik && son.deliveries > 0 && son.yogunKacan > son.deliveries * 0.02f)
            {
                hz.planliAcik = true;
                yapilan.Add($"planlı teslimat açıldı (dün {son.yogunKacan} sipariş yoğunluktan kaçtı)");
            }
        }

        void KayitAsistani(DayRecord son, System.Collections.Generic.List<string> yapilan)
        {
            var ks = KayitSistemi.I;
            if (ks == null) return;
            float iptal = IptalOrani(son);
            float kuryeBasi = son.deliveries / (float)Mathf.Max(1, son.couriers);
            if (iptal > 0.03f || son.avgAssignWait > 5f)
            {
                int acilan = 0;
                for (int v = 0; v < ks.acik.Length; v++) if (!ks.acik[v]) { ks.acik[v] = true; acilan++; }
                if (acilan > 0) yapilan.Add($"kurye kayıtları açıldı (iptal %{iptal * 100f:0}, atama bekleme {son.avgAssignWait:0.0} dk)");
            }
            else if (kuryeBasi > 0f && kuryeBasi < 5f && iptal < 0.02f && ks.acik[(int)VehicleKind.Bisiklet])
            {
                ks.acik[(int)VehicleKind.Bisiklet] = false;
                yapilan.Add($"bisiklet kaydı kapatıldı (kurye başı dün {kuryeBasi:0.#} teslimat)");
            }
        }

        void OperasyonMuduru(DayRecord son, System.Collections.Generic.List<string> yapilan)
        {
            if (son.avgPickupWait <= 14f || CityMap.I == null) return;
            Shop en = null;
            foreach (var s in CityMap.I.Shops)
            {
                if (!s.contracted || s.own || s.oncelikli || s.def.isMarket || s.def.prepMinutes < 8f || !Mutfak.DegistirilebilirMi(s)) continue;
                if (en == null || s.monthOrders + s.lastMonthOrders > en.monthOrders + en.lastMonthOrders) en = s;
            }
            if (en == null || en.monthOrders + en.lastMonthOrders < 40) return;
            Mutfak.Degistir(en, true);
            yapilan.Add($"{en.Name} ile öncelikli hazırlık anlaşması (dükkanda bekleme {son.avgPickupWait:0} dk)");
        }

        // ------------------------------------------------------------------ oyun kapalıyken

        /// <summary>
        /// Kayıt yüklenince çağrılır: kayıttan bu yana geçen gerçek süreye göre kazanç. Son 7 günün ortalama net kârı × saat × verim
        /// (otomasyon seviyesi), en fazla SaatSiniri saat. Kâr yoksa ya da 15 dakikadan kısa süre geçtiyse bir şey olmaz.
        /// </summary>
        public static void SenYokken(long kayitUtcTicks)
        {
            if (kayitUtcTicks <= 0 || TestModu.Aktif || EconomyManager.I == null) return;
            double saat = (System.DateTime.UtcNow.Ticks - kayitUtcTicks) / (double)System.TimeSpan.TicksPerHour;
            if (saat < 0.25) return;
            int lv = Seviye;
            float sinir = SaatSiniri(lv);
            float sayilan = (float)System.Math.Min(saat, sinir);
            long ort = Buyume.OrtalamaNet(7);
            if (ort <= 0)
            {
                EventManager.I.Notify($"Sen yokken ({saat:0.#} saat) şirket kâr etmedi: son 7 günün ortalaması eksi. Önce şirketi kâra geçir.");
                return;
            }
            long kazanc = (long)(ort * sayilan * Verim(lv));
            kazanc = (kazanc / 100) * 100;
            if (kazanc <= 0) return;
            EconomyManager.I.AddIncome(kazanc, $"Sen yokken ({sayilan:0.#} saat)");
            string fazla = saat > sinir ? $"\n\n{saat:0.#} saat yoktun ama otomasyonun en fazla {sinir:0} saati sayıyor." : "";
            string ipucu = lv < 3 ? $"\nOperasyon otomasyonunu yükseltirsen daha uzun süre ve daha verimli kazanırsın (sonraki seviye: %{Verim(lv + 1) * 100f:0}, {SaatSiniri(lv + 1):0} saat)." : "";
            EventManager.I.Push(new GameEvent("Sen yokken",
                $"Sen yokken ekibin çalışmaya devam etti: {Ft(kazanc)} kazanç kasana eklendi.\n" +
                $"Hesap: son 7 günün ortalama kârı {Ft(ort)}/gün × {sayilan:0.#} saat × %{Verim(lv) * 100f:0} verim." + fazla + ipucu)
                .Ozet("Kazanç", Ft(kazanc), 1)
                .Ozet("Süre", $"{sayilan:0.#} saat"));
        }

        static string Ft(long v) => GameData.Ft(v);
    }
}
