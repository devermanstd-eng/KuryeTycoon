using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Küçük ağaç düğümlerinin etkisi (sıra kayıtla ilgisiz: kayıtta düğüm kimliği tutulur).</summary>
    public enum BonusTip { Komisyon, Siparis, Hiz, Basvuru, Moral, SenYokken }

    /// <summary>Gelişim ağacının bir düğümü: büyük düğüm bir geliştirme (UpgradeKind), küçük düğüm tek seferlik bir bonus.</summary>
    public class AgacDugum
    {
        public string id;
        public bool buyuk;
        public UpgradeKind kind;        // büyük düğüm
        public string ad;               // küçük düğüm: "+%2 komisyon"
        public BonusTip tip;            // küçük düğüm
        public float miktar;            // küçük düğüm (oran; SenYokken'de saat)
        public long bedel;              // küçük düğüm (Ft, tek seferlik)
        public string ust;              // üst düğüm (null: merkeze bağlı, baştan açık)
        public Vector2 konum;           // ağaçtaki yeri (merkez 0,0; +y aşağı)
        public int dal;                 // 0 Operasyon · 1 Kurye · 2 Müşteri ve dükkan · 3 Yeni iş kolları · -1 merkez
        public Icons.Kind simge;

        public string Ad => buyuk ? UpgradeManager.Def(kind).name : ad;
    }

    /// <summary>
    /// Gelişim ağacı (incremental oyunlardaki gibi ortadan dışa yayılan ağaç). Merkezde merkez ofis; dört dal: Operasyon,
    /// Kurye, Müşteri ve dükkan, Yeni iş kolları. Büyük düğümler mevcut geliştirmeler (seviyeli), aralarındaki küçük düğümler
    /// ucuz, tek seferlik bonuslar. Bir düğüm ancak üst düğümü alınınca görünür ("?" açılır) ve alınabilir; büyük düğümün
    /// eski kilitleri (bölge, ofis) de geçerli (UpgradeManager.LockReason ağaç kilidini de sorar).
    /// Küçük bonusların toplamı sınırlı tutuldu: komisyon +%8, sipariş +%9, kurye hızı +%4, başvuru +%13, moral +3 puan.
    /// </summary>
    public static class GelisimAgaci
    {
        public const string Merkez = "merkez";
        public static readonly string[] DalAdlari = { "Operasyon", "Kurye", "Müşteri ve dükkan", "Yeni iş kolları" };
        public static readonly Color[] DalRenkleri =
        {
            new Color32(76, 195, 255, 255), new Color32(126, 217, 87, 255), new Color32(255, 180, 84, 255), new Color32(232, 138, 210, 255)
        };

        public static readonly List<AgacDugum> Dugumler = new List<AgacDugum>();
        static readonly Dictionary<string, AgacDugum> sozluk = new Dictionary<string, AgacDugum>();
        static readonly HashSet<string> alinan = new HashSet<string>();

        static AgacDugum B(string id, UpgradeKind k, string ust, float x, float y, int dal, Icons.Kind s) =>
            Ekle(new AgacDugum { id = id, buyuk = true, kind = k, ust = ust, konum = new Vector2(x, y), dal = dal, simge = s });

        static AgacDugum K(string id, string ad, BonusTip t, float m, long bedel, string ust, float x, float y, int dal, Icons.Kind s) =>
            Ekle(new AgacDugum { id = id, ad = ad, tip = t, miktar = m, bedel = bedel, ust = ust, konum = new Vector2(x, y), dal = dal, simge = s });

        static AgacDugum Ekle(AgacDugum d) { Dugumler.Add(d); sozluk[d.id] = d; return d; }

        static GelisimAgaci()
        {
            Ekle(new AgacDugum { id = Merkez, buyuk = true, kind = UpgradeKind.Ofis, konum = Vector2.zero, dal = -1, simge = Icons.Kind.Upgrades });
            // Operasyon (yukarı)
            B("dispatcher", UpgradeKind.Dispatcher, null, 0, -140, 0, Icons.Kind.Chart);
            B("otomasyon", UpgradeKind.Otomasyon, "dispatcher", -130, -240, 0, Icons.Kind.Settings);
            K("k_senyokken", "+2 saat sen yokken", BonusTip.SenYokken, 2f, 100000, "otomasyon", -250, -320, 0, Icons.Kind.Clock);
            K("k_siparis1", "+%3 sipariş", BonusTip.Siparis, 0.03f, 60000, "dispatcher", 0, -280, 0, Icons.Kind.Orders);
            B("hub", UpgradeKind.Hub, "dispatcher", 130, -240, 0, Icons.Kind.Shops);
            B("robot", UpgradeKind.Robot, "hub", 220, -360, 0, Icons.Kind.Equipment);
            K("k_komisyon1", "+%2 komisyon", BonusTip.Komisyon, 0.02f, 400000, "hub", 80, -380, 0, Icons.Kind.Coins);
            B("platform", UpgradeKind.Platform, null, 200, -110, 0, Icons.Kind.Couriers);
            K("k_basvuru1", "+%5 kurye başvurusu", BonusTip.Basvuru, 0.05f, 60000, "platform", 330, -180, 0, Icons.Kind.Hiring);
            // Kurye (sola)
            B("dinlenme", UpgradeKind.Dinlenme, null, -150, 0, 1, Icons.Kind.Heart);
            B("uygulama", UpgradeKind.Uygulama, "dinlenme", -280, -80, 1, Icons.Kind.Phone);
            K("k_hiz1", "+%2 kurye hızı", BonusTip.Hiz, 0.02f, 80000, "uygulama", -400, -160, 1, Icons.Kind.Bolt);
            B("efilo", UpgradeKind.EFilo, "uygulama", -420, -40, 1, Icons.Kind.Bike);
            B("kargobisiklet", UpgradeKind.KargoBisikleti, "efilo", -560, -90, 1, Icons.Kind.Scooter);
            K("k_hiz2", "+%2 kurye hızı", BonusTip.Hiz, 0.02f, 500000, "efilo", -550, 40, 1, Icons.Kind.Bolt);
            B("akademi", UpgradeKind.Akademi, "dinlenme", -280, 90, 1, Icons.Kind.Star);
            K("k_moral", "+3 puan moral", BonusTip.Moral, 0.03f, 50000, "akademi", -410, 120, 1, Icons.Kind.Smile);
            K("k_basvuru2", "+%8 kurye başvurusu", BonusTip.Basvuru, 0.08f, 150000, "akademi", -350, 210, 1, Icons.Kind.Hiring);
            B("bakim", UpgradeKind.Bakim, "dinlenme", -160, 170, 1, Icons.Kind.Equipment);
            if (Sade.Hijyen) B("hijyen", UpgradeKind.Hijyen, "bakim", -230, 290, 1, Icons.Kind.Basket);
            // Müşteri ve dükkan (sağa)
            B("restoran", UpgradeKind.Restoran, null, 150, 0, 2, Icons.Kind.Shops);
            K("k_komisyon2", "+%2 komisyon", BonusTip.Komisyon, 0.02f, 100000, "restoran", 270, -60, 2, Icons.Kind.Coins);
            B("adres", UpgradeKind.Adres, "restoran", 300, 50, 2, Icons.Kind.Flag);
            B("dolap", UpgradeKind.PaketDolabi, "adres", 450, 20, 2, Icons.Kind.Basket);
            K("k_siparis2", "+%3 sipariş", BonusTip.Siparis, 0.03f, 600000, "dolap", 580, -40, 2, Icons.Kind.Orders);
            K("k_komisyon3", "+%2 komisyon", BonusTip.Komisyon, 0.02f, 200000, "adres", 420, 150, 2, Icons.Kind.Coins);
            B("reklam", UpgradeKind.Reklam, null, 150, 150, 2, Icons.Kind.Campaigns);
            K("k_siparis3", "+%3 sipariş", BonusTip.Siparis, 0.03f, 120000, "reklam", 250, 260, 2, Icons.Kind.Orders);
            // Yeni iş kolları (aşağı)
            B("kargo", UpgradeKind.Kargo, null, 0, 160, 3, Icons.Kind.Car);
            B("isletme", UpgradeKind.Isletme, null, 130, 310, 3, Icons.Kind.Basket);
            K("k_komisyon4", "+%2 komisyon", BonusTip.Komisyon, 0.02f, 300000, "kargo", 0, 290, 3, Icons.Kind.Coins);
        }

        public static AgacDugum Bul(string id) => id != null && sozluk.TryGetValue(id, out var d) ? d : null;

        /// <summary>Büyük düğümün ağaçtaki karşılığı (yoksa null).</summary>
        public static AgacDugum Bul(UpgradeKind k)
        {
            foreach (var d in Dugumler) if (d.buyuk && d.kind == k) return d;
            return null;
        }

        /// <summary>Düğüm alınmış mı (büyük: en az 1. seviye; merkez her zaman).</summary>
        public static bool Sahip(string id)
        {
            var d = Bul(id);
            if (d == null) return false;
            if (d.id == Merkez) return true;
            if (d.buyuk) return UpgradeManager.I != null && UpgradeManager.I.Level(d.kind) > 0;
            return alinan.Contains(id);
        }

        /// <summary>Düğüm görünüyor mu (üst düğümü alınmış ya da merkeze bağlı).</summary>
        public static bool Acik(AgacDugum d) => d.ust == null || Sahip(d.ust);

        /// <summary>UpgradeManager.LockReason için: ilk seviye ancak üst düğüm alınınca.</summary>
        public static string UstKilidi(UpgradeKind k, int hedefSeviye)
        {
            if (hedefSeviye != 1) return "";
            var d = Bul(k);
            if (d == null || d.ust == null || Sahip(d.ust)) return "";
            return $"önce ağaçta \"{Bul(d.ust).Ad}\" al";
        }

        // ------------------------------------------------------------------ küçük düğümler

        public static float Bonus(BonusTip t)
        {
            float s = 0f;
            foreach (var id in alinan)
            {
                var d = Bul(id);
                if (d != null && !d.buyuk && d.tip == t) s += d.miktar;
            }
            return s + ArGe.Bonus(t);   // tamamlanan Ar-Ge projeleri de aynı bonuslara eklenir
        }

        /// <summary>Sadece ağacın küçük düğümlerinden gelen bonus (Ar-Ge hariç).</summary>
        public static float AgacBonusu(BonusTip t) => Bonus(t) - ArGe.Bonus(t);

        /// <summary>Küçük düğüm neden alınamıyor (boşsa alınabilir; para hariç).</summary>
        public static string KucukEngeli(AgacDugum d)
        {
            if (d == null || d.buyuk) return "";
            if (alinan.Contains(d.id)) return "alındı";
            if (!Acik(d)) return $"önce \"{Bul(d.ust).Ad}\" al";
            return "";
        }

        public static bool KucukAl(AgacDugum d)
        {
            if (KucukEngeli(d).Length > 0) return false;
            if (!EconomyManager.I.TrySpendYatirim(d.bedel, $"Gelişim ağacı: {d.ad}", ExpenseCategory.Gelistirme)) return false;
            alinan.Add(d.id);
            EventManager.I.Notify($"Gelişim ağacı: {d.ad} alındı (kalıcı).");
            return true;
        }

        public static int AlinanSayisi => alinan.Count;

        public static void Sifirla() => alinan.Clear();

        public static List<string> Kaydet() => new List<string>(alinan);

        public static void Yukle(List<string> ids)
        {
            alinan.Clear();
            if (ids == null) return;
            foreach (var id in ids) { var d = Bul(id); if (d != null && !d.buyuk) alinan.Add(id); }
        }

        /// <summary>Merkezden düğüme giden yol (efekt: satın alınca ışık bu yoldan akar).</summary>
        public static List<Vector2> Yol(AgacDugum d)
        {
            var yol = new List<Vector2>();
            var x = d;
            int guvenlik = 0;
            while (x != null && guvenlik++ < 20)
            {
                yol.Insert(0, x.konum);
                if (x.id == Merkez) break;
                x = x.ust != null ? Bul(x.ust) : Bul(Merkez);
            }
            return yol;
        }
    }
}
