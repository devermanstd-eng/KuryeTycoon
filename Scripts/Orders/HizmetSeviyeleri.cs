using UnityEngine;

namespace KuryeTycoon
{
    [System.Serializable]
    public class HizmetSave
    {
        public bool ekspresAcik, planliAcik;
        public int ekspresUcret = 600, planliIndirim = 200;
        public int ekspresToplam, ekspresGecToplam, planliToplam, planliKurtarilanToplam;
        public long ekspresGelirToplam, ekspresIadeToplam;
    }

    /// <summary>
    /// Hizmet seviyeleri (Ayarlar > Ekonomi): standart teslimatın yanında müşteriye iki seçenek daha sunulabilir.
    ///
    /// Ekspres (30 dk garanti): müşteri ek ücret öder, sipariş 30 dakikada teslim sözüyle gelir ve atamada öne geçer.
    ///   Geç kalırsa ekspres ücreti müşteriye iade edilir ve puan daha çok düşer; zamanında gelirse müşteri çok memnun kalır.
    ///   Sadece 30 dakikada yetişebilecek siparişlerde sunulur (hazırlık + yol kısa). Aceleci müşteriler daha çok seçer,
    ///   ücret arttıkça seçen azalır.
    /// Planlı teslimat: müşteri siparişini 1–2 saat sonraki bir saat aralığına verir, küçük bir indirim alır. Yoğun saatte
    ///   "kurye yok" görüp vazgeçecek müşterilerin bir kısmı planlı teslimata döner (sipariş kaçmaz), yoğun saatte de bazı müşteriler
    ///   kendiliğinden sonraki saati seçer. Planlı sipariş, saati gelene kadar kuryeye atanmaz: talep yoğun saatten sakin saate kayar.
    /// </summary>
    public class HizmetSeviyeleri : MonoBehaviour
    {
        public static HizmetSeviyeleri I { get; private set; }

        public const float EkspresSoz = 30f;          // dk
        public const float PlanliPencere = 30f;       // planlı teslimatın saat aralığı (dk)

        [Header("Ekspres")]
        public bool ekspresAcik;
        public int ekspresUcret = 600;                // 1,50 €
        [Header("Planlı teslimat")]
        public bool planliAcik;
        public int planliIndirim = 200;               // 0,50 €

        // Bugünkü ve dünkü sayılar (arayüz) + toplamlar (kayıt)
        public int ekspresBugun, ekspresGecBugun, planliBugun, planliKurtarilanBugun;
        public long ekspresGelirBugun, ekspresIadeBugun;
        public int ekspresDun, ekspresGecDun, planliDun, planliKurtarilanDun;
        public long ekspresGelirDun, ekspresIadeDun;
        public int ekspresToplam, ekspresGecToplam, planliToplam, planliKurtarilanToplam;
        public long ekspresGelirToplam, ekspresIadeToplam;

        void Awake() { I = this; }

        public void Init()
        {
            GameClock.I.OnNewDay += YeniGun;
        }

        void YeniGun()
        {
            ekspresDun = ekspresBugun; ekspresGecDun = ekspresGecBugun; planliDun = planliBugun; planliKurtarilanDun = planliKurtarilanBugun;
            ekspresGelirDun = ekspresGelirBugun; ekspresIadeDun = ekspresIadeBugun;
            ekspresBugun = ekspresGecBugun = planliBugun = planliKurtarilanBugun = 0;
            ekspresGelirBugun = ekspresIadeBugun = 0;
        }

        // ------------------------------------------------------------------ ekspres

        /// <summary>Bu sipariş 30 dakikada yetişebilir mi (müşteriye ekspres seçeneği gösterilir mi)?</summary>
        public static bool EkspresYetisir(float hazirDk, int mesafeKare) => hazirDk + mesafeKare / 1.1f + 8f <= EkspresSoz - 3f;

        /// <summary>Müşteri ekspres seçer mi? Aceleci müşteri daha çok, ücret arttıkça daha az.</summary>
        public bool EkspresSecer(Order o, float hazirDk)
        {
            if (!ekspresAcik || o.eczane || o.catering || o.kargo || !EkspresYetisir(hazirDk, o.distance)) return false;
            float p = 0.1f;
            if (o.customerType == CustomerType.Aceleci) p += 0.25f;
            else if (o.customerType == CustomerType.Comert) p += 0.06f;
            int h = GameClock.I.Hour;
            if ((h >= 11 && h < 14) || (h >= 18 && h < 21)) p += 0.04f;   // yoğun saatte acele eden çok
            p *= Mathf.Clamp(1.7f - ekspresUcret / 700f, 0.12f, 1.3f);
            return Random.value < p;
        }

        public void EkspresUygula(Order o)
        {
            int u = Mathf.Max(0, ekspresUcret);
            o.ekspres = true;
            o.ekspresUcret = u;
            o.deliveryFee += u;
            o.feeExtra += u;
            o.deadlineAt = o.createdAt + EkspresSoz;
            ekspresBugun++; ekspresToplam++;
            ekspresGelirBugun += u; ekspresGelirToplam += u;
            Telemetry.Add("Sipariş: ekspres");
        }

        // ------------------------------------------------------------------ planlı

        static bool YogunSaat(int h, bool hafsonu) =>
            hafsonu ? (h >= 12 && h < 15) || (h >= 18 && h < 22) : (h >= 11 && h < 14) || (h >= 18 && h < 21);

        /// <summary>Yoğunlukta "kurye yok" görüp vazgeçecek müşteri planlı teslimata döner mi?</summary>
        public bool YogunluktaPlanliyaDoner() => planliAcik && Random.value < 0.6f;

        /// <summary>Yoğun saatte müşteri kendiliğinden sonraki bir saati seçer mi?</summary>
        public bool PlanliSecer(Order o)
        {
            if (!planliAcik || o.eczane || o.catering || o.kargo || o.ekspres) return false;
            var clk = GameClock.I;
            if (!YogunSaat(clk.Hour, clk.IsWeekend)) return false;
            if (!ShopHours.IsOpenAt(o.shop.def, clk.TotalMinutes + 120f)) return false;   // dükkan o saatte kapalı olacak
            return Random.value < 0.07f + Mathf.Clamp(planliIndirim / 4000f, 0f, 0.1f);
        }

        /// <summary>
        /// Siparişi planlı yap: müşteri 1–2 saat sonrasını seçer (dükkan açıksa). Kurye saati gelince atanır,
        /// mutfak da yemeği o saate hazırlar.
        /// </summary>
        public void PlanliUygula(Order o, bool kurtarilan)
        {
            var clk = GameClock.I;
            float now = clk.TotalMinutes;
            float bas = Mathf.Ceil((now + Random.Range(60f, 120f)) / 15f) * 15f;
            // Dükkan kapanacaksa daha erken bir saate
            while (bas > now + 45f && !ShopHours.IsOpenAt(o.shop.def, bas)) bas -= 15f;
            float yolDk = o.distance / 0.9f + 4f;
            float hazir = o.readyAt - now;
            o.planli = true;
            o.readyAt = bas - yolDk;                                 // yemek kurye geldiğinde hazır
            o.holdUntil = Mathf.Max(now + 5f, o.readyAt - Mathf.Max(8f, hazir * 0.5f) - 6f);
            o.deadlineAt = bas + PlanliPencere;
            int indirim = Mathf.Min(planliIndirim, Mathf.Max(0, o.deliveryFee - 100));
            o.deliveryFee -= indirim;
            o.feeExtra -= indirim;
            int bs = Mathf.RoundToInt(bas) % (24 * 60);
            int bt = Mathf.RoundToInt(bas + PlanliPencere) % (24 * 60);
            string aralik = $"{bs / 60:00}:{bs % 60:00}–{bt / 60:00}:{bt % 60:00}";
            o.customerNote = $"Planlı teslimat: {aralik}. " + o.customerNote;
            if (!string.IsNullOrEmpty(o.noteTranslation)) o.noteTranslation = $"Planlı teslimat: {aralik}. " + o.noteTranslation;
            o.cancelPlan = 0;   // saatini kendisi seçti: hemen vazgeçmez
            planliBugun++; planliToplam++;
            if (kurtarilan) { planliKurtarilanBugun++; planliKurtarilanToplam++; Telemetry.Add("Sipariş: yoğunlukta planlı teslimata döndü"); }
            else Telemetry.Add("Sipariş: planlı teslimat");
        }

        // ------------------------------------------------------------------ teslimat

        /// <summary>Teslimattan sonra: geç kalan ekspresin ücreti iade edilir. Puan etkisi (dönen değer) OrderManager'da eklenir.</summary>
        public float Teslim(Order o, float gec)
        {
            if (!o.ekspres) return 0f;
            if (gec <= 0f) return 0.3f;   // söz tutuldu: müşteri çok memnun
            ekspresGecBugun++; ekspresGecToplam++;
            ekspresIadeBugun += o.ekspresUcret; ekspresIadeToplam += o.ekspresUcret;
            if (o.ekspresUcret > 0) EconomyManager.I.Spend(o.ekspresUcret, $"#{o.id} ekspres ücreti iadesi (geç kaldı)", ExpenseCategory.Iade);
            Telemetry.Add("Teslim: ekspres geç (ücret iade)");
            return -0.5f;
        }

        // ------------------------------------------------------------------ kayıt

        public HizmetSave Kaydet() => new HizmetSave
        {
            ekspresAcik = ekspresAcik, planliAcik = planliAcik, ekspresUcret = ekspresUcret, planliIndirim = planliIndirim,
            ekspresToplam = ekspresToplam, ekspresGecToplam = ekspresGecToplam, planliToplam = planliToplam,
            planliKurtarilanToplam = planliKurtarilanToplam, ekspresGelirToplam = ekspresGelirToplam, ekspresIadeToplam = ekspresIadeToplam
        };

        public void Yukle(HizmetSave s)
        {
            if (s == null) return;
            ekspresAcik = s.ekspresAcik; planliAcik = s.planliAcik;
            ekspresUcret = s.ekspresUcret > 0 ? s.ekspresUcret : 600;
            planliIndirim = Mathf.Max(0, s.planliIndirim);
            ekspresToplam = s.ekspresToplam; ekspresGecToplam = s.ekspresGecToplam; planliToplam = s.planliToplam;
            planliKurtarilanToplam = s.planliKurtarilanToplam; ekspresGelirToplam = s.ekspresGelirToplam; ekspresIadeToplam = s.ekspresIadeToplam;
        }
    }
}
