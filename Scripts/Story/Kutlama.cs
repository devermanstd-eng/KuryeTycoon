using System;
using UnityEngine;

namespace KuryeTycoon
{
    [Serializable]
    public class KutlamaSave
    {
        public int seri, enIyiSeri, rekorSayisi, sonKartGunu = -999, sonSayilanGun = -1;
        public long rekorNet, rekorTeslimat;
        public float ortKar = -1f;
    }

    /// <summary>
    /// Görsel tatmin ("sayının büyüdüğünü görmek"):
    ///  • Seri: art arda zamanında teslimat sayısı (bütün şirket). Geç teslimat ya da iptal seriyi bozar. 10, 25, 50, 100, 250, 500 ve
    ///    1000'de haritada altın "Seri ×50!" yazısı; 100 ve üstünde bildirim. Üst çubukta 10'dan büyükse görünür. Sadece görsel, ekonomiye etkisi yok.
    ///  • Büyük kâr: ortalamanın 3 katından kârlı bir teslimatın "+Ft" yazısı altın renkte uçar.
    ///  • Rekor gün: gün sonunda net kâr şirketin rekorunu kırarsa (3. günden sonra) bildirim; rekoru en az %15 aşarsa ve son kutlama
    ///    kartından 3 gün geçtiyse büyük sayılı "Rekor gün!" kartı. Teslimat rekoru da bildirilir.
    /// Kayda girer (seri, rekorlar).
    /// </summary>
    public class Kutlama : MonoBehaviour
    {
        public static Kutlama I { get; private set; }

        public int seri, enIyiSeri, rekorSayisi;
        public long rekorNet, rekorTeslimat;
        int sonKartGunu = -999, sonSayilanGun = -1;
        float ortKar = -1f;   // teslimat başına ortalama kâr (yavaş hareketli)

        static readonly int[] SeriEsikleri = { 10, 25, 50, 100, 250, 500, 1000 };

        void Awake() { I = this; }

        public void Init()
        {
            seri = 0; enIyiSeri = 0; rekorSayisi = 0; rekorNet = 0; rekorTeslimat = 0;
            sonKartGunu = -999; sonSayilanGun = -1; ortKar = -1f;
        }

        // ------------------------------------------------------------------ teslimat

        /// <summary>MapFx.Delivered çağırır. Dönüş: uçan yazının rengi altın mı (büyük kâr).</summary>
        public static bool Teslim(Order o, float late)
        {
            var k = I;
            if (k == null || o == null) return false;
            bool buyuk = false;
            float p = o.Profit;
            if (k.ortKar < 0f) k.ortKar = Mathf.Max(1f, p);
            else
            {
                buyuk = p > 0f && p >= 3f * Mathf.Max(100f, k.ortKar);
                k.ortKar = Mathf.Lerp(k.ortKar, Mathf.Max(0f, p), 0.02f);
            }

            if (late > 0f) { k.Boz(o.customerTile); return buyuk; }
            k.seri++;
            if (k.seri > k.enIyiSeri) k.enIyiSeri = k.seri;
            foreach (int e in SeriEsikleri)
            {
                if (k.seri != e) continue;
                MapFx.Ozel(o.customerTile, $"Seri ×{e}!", MapFx.Altin);
                GameSounds.Play(Sfx.Iyi);
                if (e >= 100 && EventManager.I != null)
                    EventManager.I.Notify($"Seri: art arda {e} zamanında teslimat! (Şirket rekoru {k.enIyiSeri}.)");
            }
            return buyuk;
        }

        /// <summary>İptal (MapFx.Cancelled).</summary>
        public static void Iptal(Order o)
        {
            if (I != null && o != null) I.Boz(o.customerTile);
        }

        void Boz(Vector2Int tile)
        {
            if (seri >= 25) MapFx.Ozel(tile, $"Seri bitti ({seri})", MapFx.Bad);
            seri = 0;
        }

        // ------------------------------------------------------------------ rekor gün

        void Update()
        {
            if (GameBootstrap.I == null || !GameBootstrap.I.Started || StatsHistory.I == null) return;
            var h = StatsHistory.I.Days;
            if (h == null || h.Count == 0) return;
            var g = h[h.Count - 1];
            if (g.companyDay == sonSayilanGun) return;
            sonSayilanGun = g.companyDay;
            GunBitti(g);
        }

        void GunBitti(DayRecord g)
        {
            var ev = EventManager.I;
            if (ev == null) return;
            bool netRekor = g.net > rekorNet && g.net > 0;
            bool teslimatRekor = g.deliveries > rekorTeslimat && g.deliveries > 0;
            long eskiNet = rekorNet, eskiTeslimat = rekorTeslimat;
            if (netRekor) rekorNet = g.net;
            if (teslimatRekor) rekorTeslimat = g.deliveries;
            if (g.companyDay < 3) return;   // ilk günler her gün rekor: sessiz say

            if (netRekor)
            {
                rekorSayisi++;
                float artis = eskiNet > 0 ? (g.net - eskiNet) / (float)eskiNet : 1f;
                if (artis >= 0.15f && g.companyDay - sonKartGunu >= 3)
                {
                    sonKartGunu = g.companyDay;
                    var kart = new GameEvent("Rekor gün!",
                        $"Şirketin {g.companyDay}. gününde bugüne kadarki en yüksek net kârı yaptı. " +
                        $"{g.deliveries} teslimat" + (teslimatRekor ? " (bu da rekor)" : "") +
                        $", en uzun zamanında teslimat serisi {enIyiSeri}. Bu, şirketin {rekorSayisi}. rekor günü.");
                    kart.Ozet("Bugünkü net kâr", GameData.Ft(g.net), 1);
                    kart.Ozet("Önceki rekor", eskiNet > 0 ? GameData.Ft(eskiNet) : "—");
                    kart.Ozet("Artış", eskiNet > 0 ? $"+%{artis * 100f:0}" : "ilk", 1);
                    kart.Add("Devam", null);
                    ev.Push(kart);
                    GameSounds.Play(Sfx.Buyuk);
                }
                else ev.Notify($"Rekor gün: net kâr {GameData.Ft(g.net)} (önceki rekor {GameData.Ft(eskiNet)}).");
            }
            else if (teslimatRekor && eskiTeslimat > 0)
                ev.NotifyQuiet($"Teslimat rekoru: bir günde {g.deliveries} teslimat (önceki {eskiTeslimat}).");
        }

        // ------------------------------------------------------------------ kayıt

        public KutlamaSave Kaydet() => new KutlamaSave
        {
            seri = seri, enIyiSeri = enIyiSeri, rekorSayisi = rekorSayisi, sonKartGunu = sonKartGunu, sonSayilanGun = sonSayilanGun,
            rekorNet = rekorNet, rekorTeslimat = rekorTeslimat, ortKar = ortKar
        };

        public void Yukle(KutlamaSave s)
        {
            Init();
            if (s == null)
            {
                // Eski kayıt: rekorları geçmişten çıkar (yüklemede kutlama çıkmasın)
                if (StatsHistory.I != null)
                    foreach (var d in StatsHistory.I.Days)
                    {
                        rekorNet = Math.Max(rekorNet, d.net);
                        rekorTeslimat = Math.Max(rekorTeslimat, d.deliveries);
                        sonSayilanGun = d.companyDay;
                    }
                return;
            }
            seri = s.seri; enIyiSeri = s.enIyiSeri; rekorSayisi = s.rekorSayisi; sonKartGunu = s.sonKartGunu; sonSayilanGun = s.sonSayilanGun;
            rekorNet = s.rekorNet; rekorTeslimat = s.rekorTeslimat; ortKar = s.ortKar;
        }
    }
}
