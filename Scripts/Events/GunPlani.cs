using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    [System.Serializable]
    public class GunPlaniSave
    {
        public int gripBitis;       // grip dalgasının bittiği gün (0 = yok)
        public int sonGripGunu = -999;
    }

    /// <summary>
    /// Planı bozan olaylar ve önceden haber:
    ///  • Her akşam 20:00'de "Yarının planı": hava tahmini (saat saat), şehir etkinlikleri (maç, konser, festival),
    ///    izinli/hasta kuryeler ve grip dalgası. Kötü hava ya da akşam etkinliği varsa yoğun saat primi önerilir.
    ///  • Hastalık: her kurye her gün küçük bir ihtimalle hastalanır (kışın daha sık). Sonbahar ve kışta ara sıra grip dalgası
    ///    gelir: 4–6 gün boyunca hastalanma ihtimali 4 katı. Dalga bir gün önceden duyurulur.
    /// </summary>
    public class GunPlani : MonoBehaviour
    {
        public static GunPlani I { get; private set; }

        public int gripBitis;
        int sonGripGunu = -999;
        int sonDuyuruGunu = -1;
        int gripBaslangic;          // dalganın başlayacağı gün (duyurulduktan sonra)

        void Awake() { I = this; }

        public void Init()
        {
            GameClock.I.OnNewDay += YeniGun;
        }

        /// <summary>Grip dalgası sürüyor mu (bugün)?</summary>
        public static bool GripVar => I != null && GameClock.I != null && I.gripBitis > 0 && GameClock.I.Day >= I.gripBaslangic && GameClock.I.Day <= I.gripBitis;

        /// <summary>Hastalanma ihtimalinin çarpanı (grip dalgasında 4).</summary>
        public static float HastalikCarpani => GripVar ? 4f : 1f;

        /// <summary>Bir kuryenin bugün (genel nedenlerle) hastalanma ihtimali.</summary>
        public static float GenelHastalik(Season s) =>
            (s == Season.Kis ? 0.010f : s == Season.Sonbahar ? 0.007f : 0.004f) * HastalikCarpani;

        void YeniGun()
        {
            var clk = GameClock.I;
            if (gripBitis > 0 && clk.Day > gripBitis)
            {
                gripBitis = 0;
                EventManager.I.Notify("Grip dalgası geçti: kuryelerin hastalanma ihtimali normale döndü.");
            }
            // Sonbahar ve kışta ara sıra grip dalgası (en az 20 gün arayla); bir gün önceden duyurulur
            var s = clk.Season;
            if (gripBitis == 0 && (s == Season.Kis || s == Season.Sonbahar) && clk.CompanyDay >= 7 &&
                clk.Day - sonGripGunu >= 20 && Random.value < 0.045f)
            {
                gripBaslangic = clk.Day + 1;
                gripBitis = gripBaslangic + Random.Range(3, 6);
                sonGripGunu = clk.Day;
                EventManager.I.Push(new GameEvent("Şehirde grip dalgası",
                    $"Doktorlar grip vakalarının arttığını söylüyor. Yarından itibaren {gripBitis - gripBaslangic + 1} gün boyunca kuryelerin hastalanma ihtimali " +
                    "4 katına çıkıyor: her gün birkaç kurye çalışamayabilir.\n\nYoğun saatler için kurye sayını gözden geçir; yoğun saat primi " +
                    "çevrimdışı kuryeleri çevrimiçi çeker.").Add("Tamam", null));
                Telemetry.Add("Olay: grip dalgası");
            }
        }

        void Update()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || GameClock.I == null) return;
            var clk = GameClock.I;
            if (clk.Hour != 20 || sonDuyuruGunu == clk.Day) return;
            sonDuyuruGunu = clk.Day;
            string m = YarininPlani(out bool onemli);
            if (string.IsNullOrEmpty(m)) return;
            if (onemli) EventManager.I.Notify(m);
            else EventManager.I.NotifyQuiet(m);
        }

        /// <summary>Yarının planı metni. onemli: kötü hava, etkinlik ya da grip varsa (bildirim sessiz değil).</summary>
        public string YarininPlani(out bool onemli)
        {
            onemli = false;
            var clk = GameClock.I;
            int yarin = clk.Day + 1;
            var parcalar = new List<string>();
            bool aksamYogun = false;

            var wx = WeatherSystem.I;
            if (wx != null)
            {
                string t = wx.Tahmin(yarin, out var enKotu, out float enKotuBas);
                if (WeatherSystem.Kotu(enKotu))
                {
                    onemli = true;
                    parcalar.Add($"hava {t} (siparişler +%{(WeatherSystem.TalepCarpani(enKotu) - 1f) * 100f:0}, açık araçlı kuryeler isteksiz)");
                    float saat = (enKotuBas % 1440f) / 60f;
                    if (saat <= 21f) aksamYogun = true;
                }
                else parcalar.Add($"hava {t}");
            }

            if (CityEvents.I != null)
                foreach (var e in CityEvents.I.Events)
                {
                    if (e.day != yarin) continue;
                    onemli = true;
                    parcalar.Add($"{e.title} {e.TimeText} ({CityEvents.ImpactText(e)})");
                    if (e.toHour > 17f) aksamYogun = true;
                }

            int izinli = 0;
            if (CourierManager.I != null)
                foreach (var c in CourierManager.I.Couriers) if (c.unavailableDays > 1) izinli++;
            if (izinli > 0) parcalar.Add($"{izinli} kurye izinde/hasta");
            if (gripBitis > 0 && yarin >= gripBaslangic && yarin <= gripBitis) { onemli = true; parcalar.Add("grip dalgası sürüyor"); }

            string metin = $"Yarının planı ({clk.DateOf(yarin)}): " + string.Join(" · ", parcalar) + ".";
            var camp = CampaignManager.I;
            if (aksamYogun && camp != null && !camp.aksamPrimi)
                metin += " Öneri: akşam yoğun geçecek, Kampanyalar'dan akşam primini açabilirsin.";
            return metin;
        }

        public GunPlaniSave Kaydet() => new GunPlaniSave { gripBitis = gripBitis, sonGripGunu = sonGripGunu };

        public void Yukle(GunPlaniSave s)
        {
            if (s == null) return;
            gripBitis = s.gripBitis;
            gripBaslangic = 0;
            sonGripGunu = s.sonGripGunu;
        }
    }
}
