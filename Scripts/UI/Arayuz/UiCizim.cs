using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// UI Toolkit'te çizilen küçük grafikler (Painter2D ile, generateVisualContent):
    ///  • GrafikKutu: başlık + "son: değer" + çubuk ya da çizgi grafik + ilk/son dönem etiketi; üstüne gelince o dönemin değeri.
    ///  • SaatCizelgesi: 24 saatlik kapsama şeridi (Destek), şu anki saat altı çizili.
    /// Yazıcı ile: var g = y.Ozel("net", () => new GrafikKutu()); g.Ayarla(...).
    /// </summary>
    public partial class GameUI
    {
        /// <summary>Çubuk ya da çizgi grafik (yalnızca çizim; başlık ve etiketler GrafikKutu'da).</summary>
        class GrafikEl : VisualElement
        {
            public readonly List<float> degerler = new List<float>();
            public bool cubuk = true;
            public Color renk = Color.white, negRenk = Color.red;
            public float min, max = -1f;   // max < min: ölçek veriden
            public int hover = -1;
            public Action<int> hoverDegisti;

            public GrafikEl()
            {
                AddToClassList("grafik");
                pickingMode = PickingMode.Position;
                generateVisualContent += Ciz;
                RegisterCallback<PointerMoveEvent>(e => HoverAyarla(Indeks(e.localPosition.x)));
                RegisterCallback<PointerLeaveEvent>(_ => HoverAyarla(-1));
            }

            int Indeks(float x)
            {
                if (degerler.Count == 0 || contentRect.width <= 0f) return -1;
                return Mathf.Clamp((int)(x / (contentRect.width / degerler.Count)), 0, degerler.Count - 1);
            }

            void HoverAyarla(int i)
            {
                if (i == hover) return;
                hover = i;
                MarkDirtyRepaint();
                hoverDegisti?.Invoke(i);
            }

            void Ciz(MeshGenerationContext mgc)
            {
                var r = contentRect;
                int n = degerler.Count;
                if (n == 0 || r.width <= 2f || r.height <= 2f) return;
                var p = mgc.painter2D;
                float bw = r.width / n;
                if (cubuk)
                {
                    float maxAbs = 0f; bool neg = false;
                    foreach (var v in degerler) { maxAbs = Mathf.Max(maxAbs, Mathf.Abs(v)); if (v < 0f) neg = true; }
                    if (maxAbs <= 0f) return;
                    float sifir = neg ? r.height / 2f : r.height - 2f;
                    float olcek = (neg ? r.height / 2f - 3f : r.height - 5f) / maxAbs;
                    for (int i = 0; i < n; i++)
                    {
                        float v = degerler[i];
                        float h = Mathf.Max(1f, Mathf.Abs(v) * olcek);
                        float x0 = i * bw + 1f, x1 = x0 + Mathf.Max(1f, bw - 2f);
                        float y0 = v >= 0f ? sifir - h : sifir, y1 = v >= 0f ? sifir : sifir + h;
                        var c = v >= 0f ? renk : negRenk;
                        if (hover >= 0 && hover != i) c.a *= 0.55f;
                        Kutu(p, x0, y0, x1, y1, c);
                    }
                    if (neg) Kutu(p, 0f, sifir - 0.5f, r.width, sifir + 0.5f, new Color(1f, 1f, 1f, 0.35f));
                    return;
                }

                float lo = min, hi = max;
                if (hi < lo)
                {
                    lo = float.MaxValue; hi = float.MinValue;
                    foreach (var v in degerler) { lo = Mathf.Min(lo, v); hi = Mathf.Max(hi, v); }
                    float pay = Mathf.Max(1f, (hi - lo) * 0.15f);
                    lo -= pay; hi += pay;
                }
                if (hi <= lo) return;
                Vector2 Nokta(int i) => new Vector2(i * bw + bw / 2f, r.height - 4f - Mathf.InverseLerp(lo, hi, degerler[i]) * (r.height - 8f));
                p.strokeColor = renk;
                p.lineWidth = 2f;
                p.lineJoin = LineJoin.Round;
                p.BeginPath();
                p.MoveTo(Nokta(0));
                for (int i = 1; i < n; i++) p.LineTo(Nokta(i));
                p.Stroke();
                for (int i = 0; i < n; i++)
                {
                    var q = Nokta(i);
                    float s = i == hover ? 3.5f : 2f;
                    Kutu(p, q.x - s, q.y - s, q.x + s, q.y + s, renk);
                }
            }

            static void Kutu(Painter2D p, float x0, float y0, float x1, float y1, Color c)
            {
                p.fillColor = c;
                p.BeginPath();
                p.MoveTo(new Vector2(x0, y0));
                p.LineTo(new Vector2(x1, y0));
                p.LineTo(new Vector2(x1, y1));
                p.LineTo(new Vector2(x0, y1));
                p.ClosePath();
                p.Fill();
            }
        }

        /// <summary>Başlıklı grafik: "Net kâr · son: 120 €", grafik, altında ilk ve son dönemin adı. Üstüne gelince değer başlıkta yazar.</summary>
        class GrafikKutu : VisualElement
        {
            readonly Label baslik, deger, ilk, son;
            readonly GrafikEl grafik;
            string ad = "";
            List<string> etiketler = new List<string>();
            Func<float, string> bicim = v => v.ToString("0");

            public GrafikKutu()
            {
                AddToClassList("grafik-kutu");
                pickingMode = PickingMode.Ignore;
                var ust = U.Gecirgen(this, "satir");
                baslik = U.Yazi(ust, "", "kucuk", "soluk", "esnek", "tek-satir");
                deger = U.Kalin(ust, "", "kucuk");
                grafik = new GrafikEl();
                Add(grafik);
                var alt = U.Gecirgen(this, "satir");
                ilk = U.Yazi(alt, "", "minik", "soluk", "esnek");
                son = U.Yazi(alt, "", "minik", "soluk");
                grafik.hoverDegisti = _ => Yaz();
            }

            public void Ayarla(string ad, IList<float> degerler, List<string> etiketler, bool cubuk, Color renk, Color negRenk,
                               Func<float, string> bicim, float min = 0f, float max = -1f)
            {
                this.ad = ad;
                this.etiketler = etiketler;
                this.bicim = bicim;
                grafik.cubuk = cubuk;
                grafik.renk = renk;
                grafik.negRenk = negRenk;
                grafik.min = min;
                grafik.max = max;
                grafik.degerler.Clear();
                grafik.degerler.AddRange(degerler);
                if (grafik.hover >= degerler.Count) grafik.hover = -1;
                U.Sinif(grafik, "cizgi", !cubuk);
                U.Metin(ilk, etiketler.Count > 0 ? etiketler[0] : "");
                U.Metin(son, etiketler.Count > 1 ? etiketler[etiketler.Count - 1] : "");
                grafik.MarkDirtyRepaint();
                Yaz();
            }

            void Yaz()
            {
                var d = grafik.degerler;
                int i = grafik.hover;
                if (i >= 0 && i < d.Count)
                {
                    U.Metin(baslik, $"{ad} · {(i < etiketler.Count ? etiketler[i] : "")}");
                    U.Metin(deger, bicim(d[i]));
                }
                else
                {
                    U.Metin(baslik, ad);
                    U.Metin(deger, d.Count > 0 ? "son: " + bicim(d[d.Count - 1]) : "");
                }
            }
        }

        /// <summary>24 saatlik şerit: kapsanan saatler yeşil, şu anki saatin altı beyaz çizgi; altında 00 · 06 · 12 · 18 · 24.</summary>
        class SaatCizelgesi : VisualElement
        {
            readonly bool[] dolu = new bool[24];
            int saat = -1;
            readonly VisualElement serit;

            public SaatCizelgesi()
            {
                AddToClassList("saat-cizelgesi");
                pickingMode = PickingMode.Ignore;
                serit = new VisualElement { pickingMode = PickingMode.Ignore };
                serit.AddToClassList("saat-serit");
                serit.generateVisualContent += Ciz;
                Add(serit);
                var etiket = U.Gecirgen(this, "saat-etiketler");
                for (int h = 0; h <= 24; h += 6) U.Yazi(etiket, h.ToString("00"), "minik", "soluk");
            }

            public void Ayarla(Func<int, bool> kapsar, int simdi)
            {
                bool degisti = simdi != saat;
                for (int h = 0; h < 24; h++)
                {
                    bool v = kapsar(h);
                    if (v != dolu[h]) { dolu[h] = v; degisti = true; }
                }
                saat = simdi;
                if (degisti) serit.MarkDirtyRepaint();
            }

            void Ciz(MeshGenerationContext mgc)
            {
                var r = serit.contentRect;
                if (r.width <= 2f) return;
                var p = mgc.painter2D;
                float cw = r.width / 24f;
                for (int h = 0; h < 24; h++)
                {
                    float x0 = h * cw + 1f, x1 = (h + 1) * cw - 1f;
                    Doldur(p, x0, 0f, x1, 16f, dolu[h] ? new Color(0.3f, 0.8f, 0.45f) : new Color(1f, 1f, 1f, 0.12f));
                    if (h == saat) Doldur(p, x0, 18f, x1, 21f, Color.white);
                }
                for (int h = 0; h <= 24; h += 6)
                {
                    float x = Mathf.Clamp(h * cw, 0.5f, r.width - 0.5f);
                    Doldur(p, x - 0.5f, -2f, x + 0.5f, 18f, new Color(1f, 1f, 1f, 0.35f));
                }
            }

            static void Doldur(Painter2D p, float x0, float y0, float x1, float y1, Color c)
            {
                p.fillColor = c;
                p.BeginPath();
                p.MoveTo(new Vector2(x0, y0));
                p.LineTo(new Vector2(x1, y0));
                p.LineTo(new Vector2(x1, y1));
                p.LineTo(new Vector2(x0, y1));
                p.ClosePath();
                p.Fill();
            }
        }
    }
}
