using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Analitik ekranının verisi: son 7 günde saat × bölge bazında sipariş, kaçan sipariş ve teslimat süresi; saat bazında
    /// kurye açığı (10 oyun dakikasında bir örnek: kurye bekleyen sipariş, boşta kurye, çevrimiçi kurye).
    /// OrderHeat'in kayıtlarıyla aynı anda yazılır (harita katmanı yer, bu tablo zaman gösterir). Kayda girmez: yüklenen oyunda
    /// yeni baştan toplanır (harita katmanları gibi).
    /// </summary>
    public static class SaatAnalizi
    {
        public const int Gun = 7, Bolge = 6, Saat = 24;
        const float OrnekAraligi = 10f;

        static readonly int[,,] siparis = new int[Gun, Bolge, Saat];
        static readonly int[,,] kacan = new int[Gun, Bolge, Saat];
        static readonly int[,,] teslim = new int[Gun, Bolge, Saat];
        static readonly float[,,] dakika = new float[Gun, Bolge, Saat];
        static readonly float[,] bekleyen = new float[Gun, Saat];
        static readonly float[,] bosta = new float[Gun, Saat];
        static readonly float[,] cevrimici = new float[Gun, Saat];
        static readonly int[,] ornek = new int[Gun, Saat];
        static readonly int[] slotGun = new int[Gun];
        static float sonOrnek = -1f;

        public static void Reset()
        {
            System.Array.Clear(siparis, 0, siparis.Length);
            System.Array.Clear(kacan, 0, kacan.Length);
            System.Array.Clear(teslim, 0, teslim.Length);
            System.Array.Clear(dakika, 0, dakika.Length);
            System.Array.Clear(bekleyen, 0, bekleyen.Length);
            System.Array.Clear(bosta, 0, bosta.Length);
            System.Array.Clear(cevrimici, 0, cevrimici.Length);
            System.Array.Clear(ornek, 0, ornek.Length);
            for (int i = 0; i < Gun; i++) slotGun[i] = -1;
            sonOrnek = -1f;
        }

        static int Bugun => GameClock.I != null ? GameClock.I.Day : 0;
        static int SimdikiSaat => GameClock.I != null ? Mathf.Clamp(GameClock.I.Hour, 0, Saat - 1) : 0;

        static int Slot()
        {
            int g = Bugun;
            int s = ((g % Gun) + Gun) % Gun;
            if (slotGun[s] != g)
            {
                for (int b = 0; b < Bolge; b++)
                    for (int h = 0; h < Saat; h++) { siparis[s, b, h] = 0; kacan[s, b, h] = 0; teslim[s, b, h] = 0; dakika[s, b, h] = 0f; }
                for (int h = 0; h < Saat; h++) { bekleyen[s, h] = 0f; bosta[s, h] = 0f; cevrimici[s, h] = 0f; ornek[s, h] = 0; }
                slotGun[s] = g;
            }
            return s;
        }

        static bool Gecerli(int s) => slotGun[s] >= 0 && slotGun[s] <= Bugun && slotGun[s] > Bugun - Gun;
        static int BolgeOf(Vector2Int t) => Mathf.Clamp(CityMap.RegionOf(t), 0, Bolge - 1);

        /// <summary>OrderHeat.Record ile: sipariş geldi (kayip=false) ya da kaçtı.</summary>
        public static void Kaydet(Vector2Int tile, bool kayip)
        {
            int s = Slot(), b = BolgeOf(tile), h = SimdikiSaat;
            if (kayip) kacan[s, b, h]++;
            else siparis[s, b, h]++;
        }

        /// <summary>OrderHeat.RecordDelivery ile: teslim edildi (sipariş verildiğinden teslime dk).</summary>
        public static void Teslim(Vector2Int tile, float dk)
        {
            int s = Slot(), b = BolgeOf(tile), h = SimdikiSaat;
            teslim[s, b, h]++;
            dakika[s, b, h] += dk;
        }

        /// <summary>OrderManager.Update'ten: 10 oyun dakikasında bir kurye açığı örneği.</summary>
        public static void Ornekle()
        {
            if (GameClock.I == null || OrderManager.I == null || CourierManager.I == null) return;
            float now = GameClock.I.TotalMinutes;
            if (sonOrnek >= 0f && now - sonOrnek < OrnekAraligi && now >= sonOrnek) return;
            sonOrnek = now;
            int s = Slot(), h = SimdikiSaat;
            int online = 0;
            foreach (var c in CourierManager.I.Couriers) if (c.IsOnline) online++;
            bekleyen[s, h] += OrderManager.I.AktifBekleyen;
            bosta[s, h] += OrderManager.I.IdleCourierCount();
            cevrimici[s, h] += online;
            ornek[s, h]++;
        }

        // ------------------------------------------------------------------ sorgular (son 7 gün)

        public class Ozet
        {
            public readonly int[] siparisSaat = new int[Saat], kacanSaat = new int[Saat], teslimSaat = new int[Saat];
            public readonly float[] dakikaSaat = new float[Saat];
            public readonly int[,] siparisBS = new int[Bolge, Saat], kacanBS = new int[Bolge, Saat], teslimBS = new int[Bolge, Saat];
            public readonly float[,] dakikaBS = new float[Bolge, Saat];
            public readonly float[] ortBekleyen = new float[Saat], ortBosta = new float[Saat], ortCevrimici = new float[Saat];
            public int gunSayisi, toplamSiparis, toplamKacan;

            /// <summary>Kaçan oranı (kaçan / (gelen + kaçan)).</summary>
            public float KacanOrani(int b, int h0, int h1)
            {
                int g = 0, k = 0;
                for (int h = h0; h < h1; h++) { int hh = h % Saat; g += siparisBS[b, hh]; k += kacanBS[b, hh]; }
                return g + k > 0 ? k / (float)(g + k) : 0f;
            }

            public int Hacim(int b, int h0, int h1)
            {
                int n = 0;
                for (int h = h0; h < h1; h++) { int hh = h % Saat; n += siparisBS[b, hh] + kacanBS[b, hh]; }
                return n;
            }

            public float OrtDakika(int b, int h0, int h1)
            {
                int n = 0; float d = 0f;
                for (int h = h0; h < h1; h++) { int hh = h % Saat; n += teslimBS[b, hh]; d += dakikaBS[b, hh]; }
                return n > 0 ? d / n : 0f;
            }
        }

        public static Ozet Topla()
        {
            var o = new Ozet();
            var ornekToplam = new int[Saat];
            for (int s = 0; s < Gun; s++)
            {
                if (!Gecerli(s)) continue;
                o.gunSayisi++;
                for (int h = 0; h < Saat; h++)
                {
                    for (int b = 0; b < Bolge; b++)
                    {
                        o.siparisBS[b, h] += siparis[s, b, h];
                        o.kacanBS[b, h] += kacan[s, b, h];
                        o.teslimBS[b, h] += teslim[s, b, h];
                        o.dakikaBS[b, h] += dakika[s, b, h];
                        o.siparisSaat[h] += siparis[s, b, h];
                        o.kacanSaat[h] += kacan[s, b, h];
                        o.teslimSaat[h] += teslim[s, b, h];
                        o.dakikaSaat[h] += dakika[s, b, h];
                    }
                    o.ortBekleyen[h] += bekleyen[s, h];
                    o.ortBosta[h] += bosta[s, h];
                    o.ortCevrimici[h] += cevrimici[s, h];
                    ornekToplam[h] += ornek[s, h];
                }
            }
            for (int h = 0; h < Saat; h++)
            {
                if (ornekToplam[h] > 0)
                {
                    o.ortBekleyen[h] /= ornekToplam[h];
                    o.ortBosta[h] /= ornekToplam[h];
                    o.ortCevrimici[h] /= ornekToplam[h];
                }
                o.toplamSiparis += o.siparisSaat[h];
                o.toplamKacan += o.kacanSaat[h];
            }
            return o;
        }
    }
}
