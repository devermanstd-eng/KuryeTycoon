using UnityEngine;

namespace KuryeTycoon
{
    [System.Serializable]
    public class AbonelikSave
    {
        public bool kuruldu, acik;
        public int aylikUcret = 1490;
        public float abone;
        public long toplamGelir;
        public int toplamSiparis;
    }

    /// <summary>
    /// Abonelik (İşletme > Kampanya): Wolt+ benzeri aylık üyelik. Şirketin adıyla "&lt;Şirket&gt;+".
    ///  • Kurulum bir kez 1 000 € (uygulamaya üyelik sistemi). Sonra istediğin zaman açıp kapatırsın; kapatınca üyeler gider.
    ///  • Üyeler teslimat ücreti ödemez (o ücreti şirket karşılar) ve daha sık sipariş verir: abone oranı × %35 daha çok sipariş.
    ///    Teslimat ücreti pahalı diye vazgeçmezler.
    ///  • Üyelik ücreti her gün (aylık ücret / 30) × abone sayısı olarak gelir yazılır.
    ///  • Abone hedefi = son 7 günün günlük teslimatı × 6 × fiyat cazibesi × itibar etkisi; abone sayısı her gün hedefe
    ///    yaklaşır (artarken yavaş, düşerken daha hızlı). Ücret düşükse çok üye olur ama teslimat ücretinden daha çok kaybedersin.
    /// </summary>
    public class Abonelik : MonoBehaviour
    {
        public static Abonelik I { get; private set; }

        public const long KurulumBedeli = 400000;      // 1 000 €
        public const int EnAzUcret = 400, EnFazlaUcret = 2500;

        public bool kuruldu;      // kurulum bedeli ödendi
        public bool acik;
        public int aylikUcret = 1490;
        public float abone;
        public long toplamGelir, dunGelir;
        public int bugunSiparis, dunSiparis, toplamSiparis;
        public long bugunKarsilanan, dunKarsilanan;   // üyelerin ödemediği teslimat ücretleri (şirket karşıladı)

        void Awake() { I = this; }

        public void Init()
        {
            kuruldu = acik = false;
            aylikUcret = 1490;
            abone = 0f;
            toplamGelir = dunGelir = 0;
            bugunSiparis = dunSiparis = toplamSiparis = 0;
            bugunKarsilanan = dunKarsilanan = 0;
            GameClock.I.OnNewDay += YeniGun;
        }

        public string Ad => CompanyProfile.Name + "+";
        public int Abone => Mathf.FloorToInt(abone);

        static float OrtTeslimat
        {
            get
            {
                var h = StatsHistory.I != null ? StatsHistory.I.Days : null;
                if (h == null || h.Count == 0) return 0f;
                float t = 0f; int n = 0;
                for (int i = Mathf.Max(0, h.Count - 7); i < h.Count; i++) { t += h[i].deliveries; n++; }
                return t / n;
            }
        }

        /// <summary>Fiyat cazibesi: 400 Ft'ta 1, 1 490 Ft'ta ~%50, 2 500 Ft'ta ~%5.</summary>
        public float Cazibe => Mathf.Clamp((2600f - aylikUcret) / 2200f, 0.05f, 1f);

        static float ItibarEtkisi
        {
            get
            {
                float r = Mathf.Clamp(CompanyProfile.Reputation / 4.5f, 0.5f, 1.1f);
                return r * r;
            }
        }

        public float Hedef => acik ? OrtTeslimat * 6f * Cazibe * ItibarEtkisi : 0f;

        /// <summary>Siparişlerin ne kadarı üyelerden (en fazla %60).</summary>
        public float AboneOrani
        {
            get
            {
                float ort = OrtTeslimat;
                return acik && ort > 1f ? Mathf.Clamp(abone / (ort * 10f), 0f, 0.6f) : 0f;
            }
        }

        public float SiparisCarpani => 1f + 0.35f * AboneOrani;

        public bool AboneSiparisiMi() => acik && Random.value < AboneOrani;

        public long GunlukGelirTahmini => (long)(abone * aylikUcret / 30f);

        /// <summary>Üye siparişi: teslimat ücreti müşteriden alınmaz.</summary>
        public void UyeSiparisi(Order o)
        {
            o.abone = true;
            o.aboneUcreti = o.deliveryFee;
            o.deliveryFee = 0;
            bugunSiparis++;
            toplamSiparis++;
            bugunKarsilanan += o.aboneUcreti;
        }

        public bool Kur()
        {
            if (kuruldu) return true;
            if (!EconomyManager.I.TrySpend(KurulumBedeli, $"{Ad} üyelik sistemi kurulumu", ExpenseCategory.Gelistirme)) return false;
            kuruldu = true;
            acik = true;
            EventManager.I.Notify($"{Ad} üyeliği başladı: aylık {GameData.Ft(aylikUcret)}. Üyeler teslimat ücreti ödemez ve daha sık sipariş verir.");
            return true;
        }

        public void Ac(bool ac)
        {
            if (!kuruldu) return;
            if (acik == ac) return;
            acik = ac;
            if (!ac)
            {
                EventManager.I.Notify($"{Ad} kapatıldı: {Abone} üye üyeliğini kaybetti.");
                abone = 0f;
            }
            else EventManager.I.Notify($"{Ad} yeniden açıldı.");
        }

        void YeniGun()
        {
            dunSiparis = bugunSiparis; bugunSiparis = 0;
            dunKarsilanan = bugunKarsilanan; bugunKarsilanan = 0;
            dunGelir = 0;
            if (!acik) return;
            float hedef = Hedef;
            abone += (hedef - abone) * (hedef > abone ? 0.06f : 0.12f);
            if (abone < 0f) abone = 0f;
            long gelir = GunlukGelirTahmini;
            if (gelir > 0)
            {
                EconomyManager.I.AddIncome(gelir, $"{Ad} üyelikleri ({Abone} üye)");
                dunGelir = gelir;
                toplamGelir += gelir;
            }
            Telemetry.Add($"Abonelik: üye {(Abone / 100) * 100}+");
        }

        public AbonelikSave Kaydet() => new AbonelikSave
        {
            kuruldu = kuruldu, acik = acik, aylikUcret = aylikUcret, abone = abone, toplamGelir = toplamGelir, toplamSiparis = toplamSiparis
        };

        public void Yukle(AbonelikSave s)
        {
            if (s == null) return;
            acik = s.acik;
            aylikUcret = Mathf.Clamp(s.aylikUcret, EnAzUcret, EnFazlaUcret);
            abone = Mathf.Max(0f, s.abone);
            toplamGelir = s.toplamGelir;
            toplamSiparis = s.toplamSiparis;
            kuruldu = s.kuruldu || s.acik || s.abone > 0f || s.toplamGelir > 0;
        }
    }
}
