using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Kurye özellikleri (Two Point Hospital'daki personel özellikleri gibi): her kuryenin 1–2 kalıcı özelliği vardır,
    /// kimi işe yarar kimi zorlar. İşe alırken başvuru kartında görünür. Etkileri küçük ama hissedilir:
    /// hız (saate ve havaya göre), müşteri puanı, yorulma, teklif seçiciliği, moral ve bırakma ihtimali.
    /// Ayrıca şirketin parasıyla verilen eğitimler (KuryeEgitim): beceri artırır ya da yeni sipariş türlerini açar
    /// (ilaç sertifikası → eczane siparişleri, kurumsal teslimat → catering siparişleri).
    /// </summary>
    public static class KuryeOzellik
    {
        public enum Tur { GeceKusu, Sabahci, GulerYuzlu, Kestirmeci, Dayanikli, CabukYorulur, Sadik, Paragoz, HavayaAldirmaz, YagmurSevmez }

        public static readonly string[] Adlar =
        {
            "Gece kuşu", "Sabahçı", "Güler yüzlü", "Kestirmeci", "Dayanıklı", "Çabuk yorulur", "Sadık", "Paragöz", "Havaya aldırmaz", "Yağmuru sevmez",
        };

        public static readonly string[] Aciklamalar =
        {
            "Akşam 20'den sonra %10 hızlı, sabah 7–11 arası %10 yavaş.",
            "Sabah 6–12 arası %10 hızlı, akşam 21'den sonra %10 yavaş.",
            "Müşteriler daha yüksek puan verir (+0,25 yıldız).",
            "%8 hızlı ama aceleci: müşteri puanı −0,1.",
            "%25 daha geç yorulur.",
            "%25 daha çabuk yorulur.",
            "Kolay kolay bırakmaz (bırakma ihtimali yarıya) ve biraz daha mutlu.",
            "Teklif seçer (%15 daha seçici); kazancı düşük kalırsa morali daha çok bozulur.",
            "Yağmurda, karda ve soğukta %8 hızlı (havanın yavaşlatmasını kısmen telafi eder).",
            "Yağmurda %10 yavaş ve morali biraz düşük.",
        };

        /// <summary>İyi (true) mi, zorlayıcı mı (arayüzde renk).</summary>
        public static readonly bool[] Olumlu = { true, true, true, true, true, false, true, false, true, false };

        static bool Var(Courier c, Tur t) => c != null && c.ozellikler != null && c.ozellikler.Contains((int)t);

        public static string Ad(int t) => t >= 0 && t < Adlar.Length ? Adlar[t] : "?";
        public static string Aciklama(int t) => t >= 0 && t < Aciklamalar.Length ? Aciklamalar[t] : "";
        public static bool OlumluMu(int t) => t >= 0 && t < Olumlu.Length && Olumlu[t];

        /// <summary>Yeni kurye / başvuru için 1–2 özellik (çelişenler birlikte gelmez).</summary>
        public static List<int> Rastgele()
        {
            var l = new List<int>();
            int n = Random.value < 0.65f ? 2 : 1;
            int deneme = 0;
            while (l.Count < n && deneme++ < 20)
            {
                int t = Random.Range(0, Adlar.Length);
                if (l.Contains(t) || Celisir(l, t)) continue;
                l.Add(t);
            }
            return l;
        }

        static bool Celisir(List<int> l, int t)
        {
            int Es(int a) =>
                a == (int)Tur.GeceKusu ? (int)Tur.Sabahci : a == (int)Tur.Sabahci ? (int)Tur.GeceKusu :
                a == (int)Tur.Dayanikli ? (int)Tur.CabukYorulur : a == (int)Tur.CabukYorulur ? (int)Tur.Dayanikli :
                a == (int)Tur.HavayaAldirmaz ? (int)Tur.YagmurSevmez : a == (int)Tur.YagmurSevmez ? (int)Tur.HavayaAldirmaz :
                a == (int)Tur.Sadik ? (int)Tur.Paragoz : a == (int)Tur.Paragoz ? (int)Tur.Sadik : -1;
            return l.Contains(Es(t));
        }

        // ------------------------------------------------------------------ etkiler

        public static float HizCarpani(Courier c)
        {
            if (c == null || c.ozellikler == null || c.ozellikler.Count == 0) return 1f;
            float m = 1f;
            int h = GameClock.I != null ? GameClock.I.Hour : 12;
            if (Var(c, Tur.GeceKusu)) m *= h >= 20 || h < 4 ? 1.10f : h >= 7 && h < 11 ? 0.90f : 1f;
            if (Var(c, Tur.Sabahci)) m *= h >= 6 && h < 12 ? 1.10f : h >= 21 || h < 4 ? 0.90f : 1f;
            if (Var(c, Tur.Kestirmeci)) m *= 1.08f;
            var w = WeatherSystem.I;
            if (w != null)
            {
                if (Var(c, Tur.HavayaAldirmaz) && (w.IsRainy || w.IsCold)) m *= 1.08f;
                if (Var(c, Tur.YagmurSevmez) && w.IsRainy) m *= 0.90f;
            }
            return m;
        }

        public static float PuanEki(Courier c)
        {
            float p = 0f;
            if (Var(c, Tur.GulerYuzlu)) p += 0.25f;
            if (Var(c, Tur.Kestirmeci)) p -= 0.1f;
            return p;
        }

        public static float YorgunlukCarpani(Courier c) =>
            Var(c, Tur.Dayanikli) ? 0.75f : Var(c, Tur.CabukYorulur) ? 1.25f : 1f;

        public static float TeklifCarpani(Courier c) => Var(c, Tur.Paragoz) ? 1.15f : 1f;

        /// <summary>Moral hedefine eklenen (gece hesabında).</summary>
        public static float MoralEki(Courier c, float kazancOrani)
        {
            float m = 0f;
            if (Var(c, Tur.Sadik)) m += 0.03f;
            if (Var(c, Tur.Paragoz) && kazancOrani < 0.9f) m -= 0.05f;
            if (Var(c, Tur.YagmurSevmez) && WeatherSystem.I != null && WeatherSystem.I.IsRainy) m -= 0.05f;
            return m;
        }

        public static float BirakmaCarpani(Courier c) => Var(c, Tur.Sadik) ? 0.5f : 1f;
    }

    /// <summary>
    /// Kurye eğitimleri: şirket öder, hemen verilir (kurye o akşam kursa gider), kurye bunu sever (moral +0,05).
    /// Her eğitim bir kuryeye bir kez verilir. Bedel şirket büyüdükçe artar (Denge.Olcekle).
    /// </summary>
    public static class KuryeEgitim
    {
        public enum Tur { Iletisim, Rota, GuvenliSurus, Ilac, Catering }

        public static readonly string[] Adlar =
        {
            "Müşteri iletişimi", "Şehir ve rota bilgisi", "Güvenli sürüş ve ilk yardım", "İlaç teslimatı sertifikası", "Kurumsal ve catering teslimatı",
        };

        public static readonly string[] KisaAdlar = { "İletişim", "Rota", "Güvenli sürüş", "İlaç sertifikası", "Catering" };

        public static readonly string[] Aciklamalar =
        {
            "Nezaket +0,1: müşteriler daha yüksek puan verir.",
            "Hız becerisi +0,05: şehri daha iyi tanır, kestirmeleri bilir.",
            "Dikkat +0,1: daha az hasar ve eksik, kaskını daha çok takar.",
            "Eczane siparişlerini götürebilir (kimlik/PIN kontrolü, soğuk zincir). Eczanelerle anlaşınca gerekir.",
            "Kurumsal (catering) siparişleri götürebilir: büyük, zamanı önceden belli ofis siparişleri. Motor ya da araba gerekir.",
        };

        static readonly int[] TabanBedel = { 24000, 32000, 28000, 40000, 36000 };

        public static int Bedel(int t) => (int)Denge.Olcekle(TabanBedel[Mathf.Clamp(t, 0, TabanBedel.Length - 1)]);

        public static bool Var(Courier c, Tur t) => c != null && c.egitimler != null && c.egitimler.Contains((int)t);

        /// <summary>Eğitimi verir: parayı öder, beceriyi artırır, morali yükseltir. Para yetmezse false.</summary>
        public static bool Ver(Courier c, Tur t)
        {
            if (c == null || Var(c, t)) return false;
            int bedel = Bedel((int)t);
            if (!EconomyManager.I.TrySpend(bedel, $"Eğitim: {Adlar[(int)t]} ({c.courierName})", ExpenseCategory.Diger)) return false;
            if (c.egitimler == null) c.egitimler = new List<int>();
            c.egitimler.Add((int)t);
            switch (t)
            {
                case Tur.Iletisim: c.politeness = Mathf.Min(1f, c.politeness + 0.1f); break;
                case Tur.Rota: c.speedSkill = Mathf.Min(1.35f, c.speedSkill + 0.05f); break;
                case Tur.GuvenliSurus: c.care = Mathf.Min(1f, c.care + 0.1f); break;
            }
            c.morale = Mathf.Clamp01(c.morale + 0.05f);
            EventManager.I.Notify($"{c.courierName} \"{Adlar[(int)t]}\" eğitimini aldı ({GameData.Ft(bedel)}).");
            return true;
        }

        /// <summary>Ekipte bu eğitimi almış kurye var mı (isteğe bağlı: çevrimiçi olanlar).</summary>
        public static int Sayi(Tur t, bool sadeceCevrimici = false)
        {
            int n = 0;
            if (CourierManager.I == null) return 0;
            foreach (var c in CourierManager.I.Couriers)
                if (Var(c, t) && (!sadeceCevrimici || c.IsOnline)) n++;
            return n;
        }
    }
}
