using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Ar-Ge sekmesi (Şirket > Ar-Ge; UI Toolkit). Üstte araştırılan proje ve ilerlemesi, sıradakiler; altında bütün projeler:
    /// tamamlananlar, başlatılabilenler (bedel, süre, etki) ve kilitliler (hangi projeyi istediği). Veri ArGe'den. Saniyede bir yenilenir.
    /// </summary>
    public partial class GameUI
    {
        class UiArGe : UiSekme
        {
            Yazici y;
            float son = -10f;

            protected override void Kur() { y = new Yazici(ui, kok); }

            public override void Acildi() { son = -10f; }

            public override void Yenile()
            {
                var a = ArGe.I;
                if (a == null || GameClock.I == null) return;
                if (Time.unscaledTime - son < 0.5f) return;
                son = Time.unscaledTime;
                y.Basla();
                y.Yazi("Araştırma projeleri: bedeli peşin, oyun günleriyle ilerler, bitince kalıcı etki. Oyun kapalıyken de ilerler.");
                y.Yardim($"Aynı anda bir proje araştırılır; {ArGe.SiraSiniri} proje sıraya konabilir (bedeli sırası gelince ödenir, para yoksa bekler). " +
                         $"Oyun kapalıyken gerçek her saat {ArGe.CevrimdisiGunSaatBasi:0.#} oyun günü sayılır. Bazı projeler öncekileri ister.");

                var ak = a.Aktif;
                y.KutularBasla();
                y.Kutu("Araştırılan", ak != null ? ak.ad : "yok", ak != null ? $"%{a.Oran * 100f:0} · {a.KalanGun:0.#} gün kaldı" : "bir proje başlat", ak != null ? UITheme.Accent : (Color?)null);
                y.Kutu("Tamamlanan", $"{a.TamamSayisi} / {ArGe.Projeler.Count}", $"harcanan {Ft(a.harcanan)}");
                y.Kutu("Sırada", a.Sira.Count.ToString(), a.Sira.Count > 0 ? ArGe.Bul(a.Sira[0])?.ad : "boş");
                y.KutularBitir();
                if (ak != null) y.Cubuk(ak.ad, a.Oran, UITheme.Accent, $"{a.KalanGun:0.#} gün");
                foreach (var id in a.Sira)
                {
                    var p = ArGe.Bul(id);
                    if (p == null) continue;
                    string sid = id;
                    y.SatirBasla();
                    y.Kucuk($"Sırada: {p.ad} ({Ft(p.bedel)}, {p.gun:0} gün)");
                    y.Dugme("Sıradan çıkar", () => a.SiradanCikar(sid), true, "kucuk-dg");
                    y.SatirBitir();
                }

                y.Bolum("Projeler");
                foreach (var p in ArGe.Projeler)
                {
                    string engel = a.Engel(p);
                    y.KartBasla();
                    bool tamam = a.Tamam(p.id);
                    y.Baslik((tamam ? "✓ " : "") + p.ad);
                    y.Kucuk(p.aciklama);
                    y.KV("Etki", p.etki, tamam ? U.Iyi : (Color?)null);
                    if (!tamam)
                    {
                        y.KV("Bedel ve süre", $"{Ft(p.bedel)} · {p.gun:0} oyun günü");
                        if (p.onKosul.Length > 0)
                        {
                            var adlar = new System.Collections.Generic.List<string>();
                            foreach (var k in p.onKosul) adlar.Add((a.Tamam(k) ? "✓ " : "") + ArGe.Bul(k).ad);
                            y.KV("Önce", string.Join(", ", adlar));
                        }
                        var pp = p;
                        bool siraya = !string.IsNullOrEmpty(a.aktif);
                        if (engel.Length == 0)
                            y.Dugme(siraya ? $"Sıraya ekle ({Ft(p.bedel)} sırası gelince)" : $"Araştırmayı başlat ({Ft(p.bedel)})", () => a.Baslat(pp),
                                    siraya || EconomyManager.I.FreeCash >= p.bedel, "kucuk-dg");
                        else y.Kucuk(engel == "araştırılıyor" ? $"Araştırılıyor: %{a.Oran * 100f:0}" : char.ToUpper(engel[0]) + engel.Substring(1) + ".");
                    }
                    y.KartBitir();
                }
                y.Bitir();
            }
        }
    }
}
