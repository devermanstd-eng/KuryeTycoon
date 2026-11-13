using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Oyuncunun şirketi: adı, renk paleti ve itibarı.</summary>
    public static class CompanyProfile
    {
        public static string Name = "Hızlı Kurye";
        public static Color Primary = new Color(0.0f, 0.62f, 0.87f);
        public static Color Secondary = Color.white;
        /// <summary>1–5 arası müşteri puanı ortalaması. Sipariş sayısını etkiler.</summary>
        public static float Reputation = 3.5f;

        public static void ResetDefaults()
        {
            Name = "Hızlı Kurye";
            Primary = new Color(0.0f, 0.62f, 0.87f);
            Secondary = Color.white;
            Reputation = 3.5f;
            Itibar.Sifirla();
        }

        /// <summary>Olaylardan gelen itibar etkisi (viral övgü, ödül, ceza...): iki haftada yavaşça söner (bkz. Itibar).</summary>
        public static void AddReputation(float delta)
        {
            Itibar.Olay(delta);
        }

        public static string Initial =>
            string.IsNullOrEmpty(Name) ? "K" : Name.Trim().Substring(0, 1).ToUpper();
    }

    /// <summary>
    /// İtibar = son 14 günün müşteri puanlarının ortalaması (iptal edilen sipariş 1 yıldız sayılır) + olay etkisi.
    /// Eskiden her teslimatta puana doğru %4 kayıyordu: bir günde ±1 yıldız oynuyordu, %24 geç teslimatla bile 4,9'da kalıyordu.
    /// Şimdi bir günün etkisi en fazla iki haftanın bir günü kadar; olaylar (viral övgü, ceza) iki haftada söner.
    /// Veri azken (oyunun başı) 3,5 yıldızlık bir başlangıç ağırlığı ortalamayı tutar.
    /// </summary>
    public static class Itibar
    {
        public const int GunSayisi = 14;
        const float OnDeger = 3.5f, OnAgirlik = 60f;   // başlangıçta 60 puanlık 3,5 varsayılır
        const float OlaySiniri = 0.8f;

        static readonly float[] toplam = new float[GunSayisi];
        static readonly int[] adet = new int[GunSayisi];
        static int gun;
        public static float OlayEtkisi;

        public static void Sifirla()
        {
            System.Array.Clear(toplam, 0, GunSayisi);
            System.Array.Clear(adet, 0, GunSayisi);
            gun = 0;
            OlayEtkisi = 0f;
            Hesapla();
        }

        /// <summary>Teslim edilen siparişin puanı (1–5).</summary>
        public static void Puan(float yildiz)
        {
            toplam[gun] += Mathf.Clamp(yildiz, 1f, 5f);
            adet[gun]++;
            Hesapla();
        }

        /// <summary>Şirket yüzünden iptal: müşteri 1 yıldız vermiş sayılır (ağırlık 0–1 arası: dükkan iptalinde az).</summary>
        public static void Iptal(float agirlik = 1f)
        {
            if (agirlik <= 0f) return;
            // Ağırlıklı 1 yıldız: toplam ve adet kesirli tutulamadığı için ağırlık kadar olasılıkla sayılır
            if (agirlik < 1f && Random.value > agirlik) return;
            toplam[gun] += 1f;
            adet[gun]++;
            Hesapla();
        }

        public static void Olay(float delta)
        {
            OlayEtkisi = Mathf.Clamp(OlayEtkisi + delta, -OlaySiniri, OlaySiniri);
            Hesapla();
        }

        /// <summary>Gece yarısı: en eski gün silinir, olay etkisi biraz söner.</summary>
        public static void YeniGun()
        {
            gun = (gun + 1) % GunSayisi;
            toplam[gun] = 0f;
            adet[gun] = 0;
            OlayEtkisi *= 1f - 1f / GunSayisi * 1.5f;   // ~2 haftada büyük kısmı söner
            if (Mathf.Abs(OlayEtkisi) < 0.005f) OlayEtkisi = 0f;
            Hesapla();
        }

        public static float PuanOrtalamasi
        {
            get
            {
                double t = OnDeger * OnAgirlik, n = OnAgirlik;
                for (int i = 0; i < GunSayisi; i++) { t += toplam[i]; n += adet[i]; }
                return (float)(t / n);
            }
        }

        public static int PuanSayisi
        {
            get { int n = 0; for (int i = 0; i < GunSayisi; i++) n += adet[i]; return n; }
        }

        static void Hesapla()
        {
            CompanyProfile.Reputation = Mathf.Clamp(PuanOrtalamasi + OlayEtkisi, 1f, 5f);
        }

        // ---------- kayıt ----------
        public static float[] KayitToplam() => (float[])toplam.Clone();
        public static int[] KayitAdet() => (int[])adet.Clone();
        public static int KayitGun => gun;

        /// <summary>Kayıttan yükle. Eski kayıtta (dizi yok) kayıttaki itibar olay etkisi olarak başlar ve zamanla puanlara bırakır.</summary>
        public static void Yukle(float[] t, int[] a, int g, float olay, float eskiItibar)
        {
            Sifirla();
            if (t != null && a != null && t.Length == GunSayisi && a.Length == GunSayisi)
            {
                System.Array.Copy(t, toplam, GunSayisi);
                System.Array.Copy(a, adet, GunSayisi);
                gun = Mathf.Clamp(g, 0, GunSayisi - 1);
                OlayEtkisi = olay;
            }
            else OlayEtkisi = Mathf.Clamp(eskiItibar - OnDeger, -OlaySiniri, OlaySiniri);
            Hesapla();
        }
    }
}
