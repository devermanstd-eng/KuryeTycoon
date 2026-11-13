using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Teslimat robotları (geç oyun geliştirmesi "Teslimat robotları", Starship benzeri kaldırım robotları).
    /// Merkez ofisten çıkar; yakın dükkanlardan kısa mesafeli (≤ 2 km), kartla ödenen, hafif siparişleri yavaşça götürür.
    /// Kurye ücreti ve bahşiş yoktur. Fırtınada ve karda yeni sipariş almaz (yoldaki işini bitirir).
    /// Robot ancak söz verilen saate yetişecekse siparişi alır. Aldığı sipariş Bekleyenler listesinden çıkar.
    /// </summary>
    public class RobotFilo : MonoBehaviour
    {
        public static RobotFilo I { get; private set; }

        /// <summary>Robot hızı: saatte ~5 km (0,2 km'lik karede 2,4 dk).</summary>
        public const float KareDakika = 2.4f;
        /// <summary>Dükkan → müşteri en fazla bu kadar kare (2 km).</summary>
        public const int MaxMesafe = 10;
        /// <summary>Robot boştayken en fazla bu kadar uzaktaki dükkana gider.</summary>
        public const int MaxDukkanUzakligi = 12;
        /// <summary>Müşteri aşağı inip robotun kapağını açar.</summary>
        public const float KapidaDakika = 2f;

        public static readonly Color RobotRengi = new Color(0.92f, 0.94f, 0.96f);
        public static readonly Color RobotHalka = new Color(0.25f, 0.85f, 0.75f);

        public enum Durum { Bosta, Donuyor, Dukkana, Dukkanda, Musteriye, Kapida }

        public class Robot
        {
            public int no;
            public Vector2 pos;
            public Durum durum;
            public Order order;
            public List<Vector2Int> path;
            public int pathIndex;
            public float waitUntil;
            public Transform tr;
            public SpriteRenderer govde, halka;
            public string Ad => $"Robot {no}";
        }

        public readonly List<Robot> Robotlar = new List<Robot>();
        public int BugunTeslim { get; private set; }
        public int ToplamTeslim { get; private set; }
        public long BugunTasarruf { get; private set; }   // kuryeye ödenmeyen ücret
        float atamaSayaci;
        Transform kok;

        void Awake() { I = this; }

        public void Init()
        {
            foreach (var r in Robotlar) if (r.tr != null) Destroy(r.tr.gameObject);
            Robotlar.Clear();
            BugunTeslim = 0;
            ToplamTeslim = 0;
            BugunTasarruf = 0;
            if (kok == null)
            {
                kok = new GameObject("Robotlar").transform;
                kok.SetParent(transform, false);
            }
            GameClock.I.OnNewDay += () => { BugunTeslim = 0; BugunTasarruf = 0; };
        }

        /// <summary>Hava robota uygun mu? (Fırtına ve kar: kaldırım robotu çalışmaz.)</summary>
        public static bool HavaUygun =>
            WeatherSystem.I == null || (WeatherSystem.I.Current != WeatherKind.Firtina && WeatherSystem.I.Current != WeatherKind.Karli);

        public int Yolda
        {
            get { int n = 0; foreach (var r in Robotlar) if (r.order != null) n++; return n; }
        }

        void Update()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || CityMap.I == null) return;
            int hedef = UpgradeManager.I != null ? UpgradeManager.I.RobotSayisi : 0;
            while (Robotlar.Count < hedef) Ekle();

            var clock = GameClock.I;
            float dm = clock.DeltaMinutes;
            if (dm > 0f)
            {
                float now = clock.TotalMinutes;
                foreach (var r in Robotlar) Ilerle(r, dm, now);

                atamaSayaci += dm;
                if (atamaSayaci >= 0.5f)
                {
                    atamaSayaci = 0f;
                    if (HavaUygun && OrderManager.I != null)
                        foreach (var r in Robotlar)
                            if (r.order == null) IsBul(r, now);
                }
            }

            foreach (var r in Robotlar)
            {
                if (r.tr == null) continue;
                r.tr.position = CityMap.ToWorld(r.pos);
                bool iceride = r.durum == Durum.Bosta;   // merkezde şarjda
                r.govde.enabled = !iceride;
                r.halka.enabled = !iceride;
                r.halka.color = r.order != null ? RobotHalka : new Color(RobotHalka.r, RobotHalka.g, RobotHalka.b, 0.45f);
            }
        }

        void Ekle()
        {
            var r = new Robot { no = Robotlar.Count + 1, pos = CityMap.I.HQDoor, durum = Durum.Bosta };
            var go = new GameObject(r.Ad);
            go.transform.SetParent(kok, false);
            r.tr = go.transform;
            r.halka = SpriteFactory.Create("Halka", r.tr, true, RobotHalka, new Vector3(0f, 0f, -0.04f), 0.5f, 9);
            r.govde = SpriteFactory.Create("Gövde", r.tr, false, RobotRengi, new Vector3(0f, 0f, -0.05f), 0.3f, 10);
            Robotlar.Add(r);
        }

        void Git(Robot r, Vector2Int hedef)
        {
            r.path = CityMap.I.FindPath(Vector2Int.RoundToInt(r.pos), hedef);
            r.pathIndex = 0;
        }

        bool Yuru(Robot r, float dm)
        {
            if (r.path == null || r.path.Count == 0) return true;
            float kalan = dm / KareDakika;
            while (kalan > 0f && r.pathIndex < r.path.Count)
            {
                Vector2 t = r.path[r.pathIndex];
                Vector2 d = t - r.pos;
                float m = d.magnitude;
                if (m <= kalan) { r.pos = t; kalan -= m; r.pathIndex++; }
                else { r.pos += d / m * kalan; kalan = 0f; }
            }
            return r.pathIndex >= r.path.Count;
        }

        void Ilerle(Robot r, float dm, float now)
        {
            var o = r.order;
            switch (r.durum)
            {
                case Durum.Dukkana:
                    if (Yuru(r, dm)) { r.durum = Durum.Dukkanda; r.waitUntil = Mathf.Max(now + 0.5f, o.readyAt); }
                    break;
                case Durum.Dukkanda:
                    if (now >= r.waitUntil)
                    {
                        o.status = OrderStatus.Alindi;
                        o.pickedAt = now;
                        if (o.marker != null) o.marker.color = RobotHalka;
                        Git(r, o.customerDoor);
                        r.durum = Durum.Musteriye;
                    }
                    break;
                case Durum.Musteriye:
                    if (Yuru(r, dm)) { r.durum = Durum.Kapida; r.waitUntil = now + KapidaDakika; }
                    break;
                case Durum.Kapida:
                    if (now >= r.waitUntil)
                    {
                        OrderManager.I.CompleteByRobot(o);
                        r.order = null;
                        BugunTeslim++;
                        ToplamTeslim++;
                        // Hava uygunsa bir sonraki işi buradan alabilir; yoksa merkeze döner
                        Git(r, CityMap.I.HQDoor);
                        r.durum = Durum.Donuyor;
                    }
                    break;
                case Durum.Donuyor:
                    if (Yuru(r, dm)) r.durum = Durum.Bosta;
                    break;
            }
        }

        /// <summary>Robotun yetişebileceği, ona uygun bekleyen siparişi alır (en yakın dükkandaki).</summary>
        void IsBul(Robot r, float now)
        {
            var bekleyen = OrderManager.I.Pending;
            Order sec = null;
            int secUzaklik = int.MaxValue;
            var konum = Vector2Int.RoundToInt(r.pos);
            for (int i = 0; i < bekleyen.Count; i++)
            {
                var o = bekleyen[i];
                if (!Uygun(o)) continue;
                int u = CityMap.Manhattan(konum, o.shop.door);
                if (u > MaxDukkanUzakligi || u >= secUzaklik) continue;
                // Söz verilen saate yetişecek mi? (Yol, kuş uçuşundan ~%15 uzun.)
                float alis = Mathf.Max(now + u * 1.15f * KareDakika, o.readyAt);
                float varis = alis + o.distance * KareDakika + KapidaDakika;
                if (varis > o.deadlineAt - 1f) continue;
                sec = o;
                secUzaklik = u;
            }
            if (sec == null) return;

            bekleyen.Remove(sec);
            // Bu siparişi götürecek kuryeye ödenecek olan (robot yaptığı için cepte kalır)
            if (EconomyManager.I != null) BugunTasarruf += EconomyManager.I.ExpectedCourierPay(sec.distance, sec.weightKg);
            sec.robot = true;
            sec.status = OrderStatus.Atandi;
            sec.assignedAt = now;
            if (sec.marker != null) sec.marker.color = Color.Lerp(RobotHalka, Color.white, 0.4f);
            Telemetry.Add("Robot siparişi aldı");
            r.order = sec;
            Git(r, sec.shop.door);
            r.durum = Durum.Dukkana;
        }

        /// <summary>Robota uygun sipariş: kartla ödenmiş, hafif, kısa mesafe, sorunsuz adres, iptal planı yok.</summary>
        public static bool Uygun(Order o)
        {
            if (o == null || o.status != OrderStatus.Bekliyor || o.courier != null || o.robot) return false;
            if (o.cashPay || o.IsHeavy || o.distance > MaxMesafe) return false;
            if (o.addressIssue != AddressIssue.Yok || o.cancelPlan > 0) return false;
            return true;
        }

        /// <summary>Siparişler panelinde gösterilen kısa durum.</summary>
        public string DurumMetni()
        {
            if (Robotlar.Count == 0) return "";
            string hava = HavaUygun ? "" : " · hava yüzünden yeni iş almıyor";
            return $"Robotlar: {Yolda}/{Robotlar.Count} yolda · bugün {BugunTeslim} teslimat, {GameData.Ft(BugunTasarruf)} kurye ücreti cepte{hava}";
        }
    }
}
