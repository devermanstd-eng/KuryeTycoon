using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Başarımlar sekmesi (Hedefler > Başarımlar; UI Toolkit). Üstte kaç başarımın açıldığı ve bitmeye en yakın üç uzun vadeli hedef;
    /// altında kategori kategori bütün başarımlar: açılanlar tarihiyle, açılmayanlar ilerleme çubuğuyla. Başarımlar oyunlar arası
    /// kalıcıdır (Basarimlar.cs). Saniyede bir yenilenir.
    /// </summary>
    public partial class GameUI
    {
        class UiBasarimlar : UiSekme
        {
            Yazici y;
            float sonYenileme = -10f;

            static readonly Color Altin = new Color(0.95f, 0.75f, 0.3f);

            protected override void Kur() { y = new Yazici(ui, kok); }

            public override void Acildi() { sonYenileme = -10f; }

            public override void Yenile()
            {
                if (Time.unscaledTime - sonYenileme < 1f) return;
                sonYenileme = Time.unscaledTime;
                var liste = Basarimlar.Tanimlar;
                y.Basla();
                int acik = Basarimlar.AcikSayisi;
                y.Yazi($"{acik} / {liste.Count} başarım açıldı.");
                y.Yardim("Başarımlar bütün oyunlarında geçerlidir: şirketi satıp yeni şehirde başlasan da kaybolmaz. " +
                         (TestModu.Aktif ? "Otomatik testte açılanlar sadece bu test için sayılır, kaydedilmez." : "Steam sürümünde aynı başarımlar Steam'de de açılır."));
                y.Cubuk("Toplam", liste.Count > 0 ? acik / (float)liste.Count : 0f, Altin, $"%{(liste.Count > 0 ? acik * 100f / liste.Count : 0f):0}");

                // Uzun vadeli hedefler: açılmamışlar içinde bitmeye en yakın üç
                var yakin = new List<(BasarimTanimi t, float v)>();
                foreach (var t in liste)
                {
                    if (Basarimlar.AcikMi(t.id) || t.ilerleme == null) continue;
                    float v = Deger(t);
                    if (v > 0f && v < 1f) yakin.Add((t, v));
                }
                yakin.Sort((a, b) => b.v.CompareTo(a.v));
                if (yakin.Count > 0)
                {
                    y.Bolum("Sıradaki hedefler", "Açılmamış başarımlardan bitmeye en yakın üçü.");
                    for (int i = 0; i < Mathf.Min(3, yakin.Count); i++)
                    {
                        var t = yakin[i].t;
                        y.Cubuk(t.ad, yakin[i].v, UITheme.Accent, Metin(t));
                        y.Kucuk(t.aciklama);
                    }
                }

                string kat = null;
                foreach (var t in liste)
                {
                    if (t.kategori != kat)
                    {
                        kat = t.kategori;
                        int n = 0, a = 0;
                        foreach (var x in liste) if (x.kategori == kat) { n++; if (Basarimlar.AcikMi(x.id)) a++; }
                        y.Bolum($"{kat} · {a}/{n}");
                    }
                    if (Basarimlar.AcikMi(t.id))
                    {
                        string tarih = Basarimlar.Tarih(t.id);
                        y.Cubuk("✓ " + t.ad, 1f, Altin, tarih.Length > 0 ? tarih : "açıldı");
                    }
                    else
                    {
                        float v = t.ilerleme != null ? Deger(t) : 0f;
                        y.Cubuk(t.ad, v, new Color(0.45f, 0.5f, 0.6f), t.ilerleme != null ? Metin(t) : "henüz değil");
                    }
                    y.Kucuk(t.aciklama);
                }
                y.Bitir();
            }

            static float Deger(BasarimTanimi t)
            {
                try { return Mathf.Clamp01(t.ilerleme()); }
                catch { return 0f; }
            }

            static string Metin(BasarimTanimi t)
            {
                try { return t.ilerlemeMetni != null ? t.ilerlemeMetni() : ""; }
                catch { return ""; }
            }
        }
    }
}
