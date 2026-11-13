using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Yazıcı: IMGUI'deki GUILayout gibi her yenilemede baştan sona "yazılır" (Yazi, KV, Dugme, KartBasla…), ama elemanlar
    /// silinip kurulmaz: aynı sırada aynı tür gelirse var olan eleman güncellenir. Böylece üstüne gelme, tıklama ve ipucu bozulmaz,
    /// metin ağırlıklı pencereler (Finans, Ekipman, Dükkanlar…) IMGUI kodundan neredeyse satır satır taşınır.
    /// Kullanım: Kur'da y = new Yazici(ui, kok); Yenile'de y.Basla(); … y.Bitir();
    /// Elle tasarlanmış sekmeler (Siparişler, Kuryeler) bunu kullanmaz; ikisi birlikte de kullanılabilir.
    /// </summary>
    public partial class GameUI
    {
        /// <summary>Türkçe büyük harf (i → İ, ı → I). CultureInfo("tr-TR") her platformda olmayabilir.</summary>
        static string BuyukHarf(string s) => (s ?? "").Replace('i', 'İ').Replace('ı', 'I').ToUpperInvariant();

        class Yazici
        {
            class Oge
            {
                public string tur, sinif, ipucu;
                public bool ipucuKayitli;
                public VisualElement e, rozet;
                public Label a, b, yardim;
                public Button d;
                public Action tik;
                public Kap ic;
                public U.Cubuk cubuk;
                public readonly List<Label> secenekler = new List<Label>();
                public Action<int> secim;
                public string secImza;
            }

            class Kap
            {
                public VisualElement e;
                public readonly List<Oge> ogeler = new List<Oge>();
                public int i;
            }

            readonly GameUI ui;
            readonly Kap kok;
            readonly List<Kap> yigin = new List<Kap>();
            Oge son;

            public Yazici(GameUI ui, VisualElement ust)
            {
                this.ui = ui;
                kok = new Kap { e = U.Gecirgen(ust, "kolon", "yz") };
            }

            Kap K => yigin.Count > 0 ? yigin[yigin.Count - 1] : kok;

            public void Basla()
            {
                yigin.Clear();
                kok.i = 0;
                son = null;
            }

            public void Bitir()
            {
                while (yigin.Count > 0) KapKapat();
                Kirp(kok);
            }

            static void Kirp(Kap k)
            {
                for (int j = k.ogeler.Count - 1; j >= k.i; j--)
                {
                    k.ogeler[j].e.RemoveFromHierarchy();
                    k.ogeler.RemoveAt(j);
                }
            }

            /// <summary>Sıradaki eleman: aynı türse var olanı verir, değilse yenisini o sıraya koyar.</summary>
            Oge Al(string tur, Func<Oge> kur)
            {
                var k = K;
                Oge o = k.i < k.ogeler.Count ? k.ogeler[k.i] : null;
                if (o == null || o.tur != tur)
                {
                    var yeni = kur();
                    yeni.tur = tur;
                    if (o != null)
                    {
                        o.e.RemoveFromHierarchy();
                        k.ogeler[k.i] = yeni;
                    }
                    else k.ogeler.Add(yeni);
                    k.e.Insert(Mathf.Min(k.i, k.e.childCount), yeni.e);
                    o = yeni;
                }
                k.i++;
                son = o;
                return o;
            }

            static void SinifAyarla(VisualElement e, Oge o, string sinif)
            {
                if (o.sinif == sinif) return;
                if (!string.IsNullOrEmpty(o.sinif)) foreach (var s in o.sinif.Split(' ')) e.RemoveFromClassList(s);
                if (!string.IsNullOrEmpty(sinif)) foreach (var s in sinif.Split(' ')) e.AddToClassList(s);
                o.sinif = sinif;
            }

            static void RenkAyarla(VisualElement e, Color? renk)
            {
                if (renk.HasValue) U.Renk(e, renk.Value);
                else if (e.style.color.keyword != StyleKeyword.Null) e.style.color = StyleKeyword.Null;
            }

            // ------------------------------------------------------------------ yazılar

            /// <summary>Bölüm başlığı (büyük harf, ince çizgi). Yardım verilirse sağda "?" ve ipucu.</summary>
            public void Bolum(string baslik, string yardim = null)
            {
                var o = Al("bolum", () =>
                {
                    var x = new Oge { e = U.El(null, "bolum") };
                    x.a = U.Kalin(x.e, "", "bolum-yazi");
                    U.Gecirgen(x.e, "bolum-cizgi");
                    return x;
                });
                U.Metin(o.a, BuyukHarf(baslik));
                YardimRozeti(o, yardim);
            }

            /// <summary>Metin satırı. sinif: null (normal), "kucuk soluk", "kucuk uyari-renk", "kucuk not-renk", "kalin" … </summary>
            public void Yazi(string metin, string sinif = null, Color? renk = null)
            {
                var o = Al("yazi", () =>
                {
                    var x = new Oge { e = U.Gecirgen(null, "yz-satir") };
                    x.a = U.Yazi(x.e, "", "yz-metin");
                    x.a.enableRichText = true;
                    return x;
                });
                SinifAyarla(o.a, o, sinif);
                RenkAyarla(o.a, renk);
                U.Metin(o.a, metin);
                YardimRozeti(o, null);
            }

            public void Kucuk(string metin) => Yazi(metin, "kucuk soluk");
            public void Uyari(string metin) => Yazi(metin, "kucuk uyari-renk");
            public void Not(string metin) => Yazi(metin, "kucuk not-renk");
            public void Baslik(string metin) => Yazi(metin, "yz-baslik");

            /// <summary>Solda renkli nokta, yanında metin (rakip listesi gibi).</summary>
            public void Noktali(string metin, Color nokta, string sinif = null)
            {
                var o = Al("noktali", () =>
                {
                    var x = new Oge { e = U.Gecirgen(null, "yz-satir") };
                    x.rozet = U.Gecirgen(x.e, "yz-nokta");
                    x.a = U.Yazi(x.e, "", "yz-metin");
                    return x;
                });
                U.Zemin(o.rozet, nokta);
                SinifAyarla(o.a, o, sinif);
                U.Metin(o.a, metin);
            }

            /// <summary>Anahtar · değer satırı (değer sağda, kalın).</summary>
            public void KV(string anahtar, string deger, Color? renk = null)
            {
                var o = Al("kv", () =>
                {
                    var x = new Oge { e = U.Gecirgen(null, "yz-kv") };
                    x.a = U.Yazi(x.e, "", "yz-kv-ad");
                    x.b = U.Kalin(x.e, "", "yz-kv-deger");
                    return x;
                });
                U.Metin(o.a, anahtar);
                U.Metin(o.b, deger);
                RenkAyarla(o.b, renk);
                YardimRozeti(o, null);
            }

            /// <summary>Son yazılan satıra "?" ve ipucu ekler (IMGUI'deki HelpOnLast).</summary>
            public void Yardim(string metin)
            {
                if (son == null) return;
                if (son.tur == "yazi" || son.tur == "kv" || son.tur == "bolum" || son.tur == "baslikli") YardimRozeti(son, metin);
                else IpucuVer(son, son.e, metin);
            }

            void YardimRozeti(Oge o, string metin)
            {
                bool vr = !string.IsNullOrEmpty(metin);
                if (o.yardim == null)
                {
                    if (!vr) return;
                    o.yardim = U.Kalin(o.e, "?", "yardim-rozet");
                }
                U.Goster(o.yardim, vr);
                if (vr) IpucuVer(o, o.yardim, metin);
            }

            void IpucuVer(Oge o, VisualElement e, string metin)
            {
                o.ipucu = metin;
                if (o.ipucuKayitli) return;
                o.ipucuKayitli = true;
                ui.Ipucu(e, () => o.ipucu);
            }

            // ------------------------------------------------------------------ düğmeler

            /// <summary>Düğme. Satır içindeyse yan yana dizilir, değilse tam genişlik. sinif: "kucuk-dg", "vurgu", "tehlike".</summary>
            public void Dugme(string metin, Action tik, bool etkin = true, string sinif = null)
            {
                var o = Al("dugme", () =>
                {
                    var x = new Oge();
                    x.d = U.Dugme(null, "", null, "yz-dugme");
                    x.d.clicked += () => x.tik?.Invoke();
                    x.e = x.d;
                    return x;
                });
                o.tik = tik;
                if (o.d.text != metin) o.d.text = metin;
                if (o.d.enabledSelf != etkin) o.d.SetEnabled(etkin);
                if (o.sinif != sinif)
                {
                    bool vurgu = sinif != null && sinif.Contains("vurgu");
                    SinifAyarla(o.d, o, sinif);
                    if (vurgu) { U.Zemin(o.d, UITheme.Accent); U.Renk(o.d, UITheme.AccentText); }
                    else { ZeminSil(o.d); RenkSil(o.d); }
                }
            }

            /// <summary>Seçenek şeridi (sayfa sekmeleri, plan seçimi). Çoksa alt satıra kayar.</summary>
            public void Secim(IList<string> adlar, int secili, Action<int> yaz, bool sarar = false)
            {
                var o = Al(sarar ? "secim-sarar" : "secim", () =>
                {
                    var x = new Oge { e = U.El(null, sarar ? "yz-cipler" : "secenekler") };
                    x.e.style.marginTop = 4;
                    x.e.style.marginBottom = 6;
                    return x;
                });
                o.secim = yaz;
                string imza = string.Join("\u0001", adlar);
                if (imza != o.secImza)
                {
                    o.secImza = imza;
                    o.e.Clear();
                    o.secenekler.Clear();
                    for (int i = 0; i < adlar.Count; i++)
                    {
                        int k = i;
                        var l = U.Kalin(o.e, adlar[i], sarar ? "yz-cip" : "secenek");
                        U.Tikla(l, () => o.secim?.Invoke(k));
                        o.secenekler.Add(l);
                    }
                }
                for (int i = 0; i < o.secenekler.Count; i++)
                {
                    bool sec = i == secili;
                    var l = o.secenekler[i];
                    if (sec) { U.Zemin(l, UITheme.Accent); U.Renk(l, UITheme.AccentText); }
                    else if (sarar) { U.Zemin(l, new Color(1f, 1f, 1f, 0.08f)); U.Renk(l, UITheme.Text); }
                    else { U.Zemin(l, new Color(0, 0, 0, 0)); U.Renk(l, U.Soluk); }
                }
            }

            // ------------------------------------------------------------------ çubuk, boşluk

            public void Cubuk(string ad, float v, Color renk, string deger)
            {
                var o = Al("cubuk", () =>
                {
                    var x = new Oge { e = U.Gecirgen(null, "olcu") };
                    x.a = U.Yazi(x.e, "", "olcu-ad");
                    x.cubuk = U.CubukEkle(x.e, v, renk);
                    x.b = U.Yazi(x.e, "", "olcu-deger");
                    return x;
                });
                U.Metin(o.a, ad);
                U.Goster(o.a, !string.IsNullOrEmpty(ad));
                o.cubuk.Ayarla(v, renk);
                U.Metin(o.b, deger);
            }

            public void Bosluk(float px)
            {
                var o = Al("bosluk", () => new Oge { e = U.Gecirgen(null) });
                if (o.e.style.height != px) o.e.style.height = px;
            }

            // ------------------------------------------------------------------ kapsayıcılar

            /// <summary>Kart (kutu). İçindekiler KartBitir'e kadar karta yazılır.</summary>
            public void KartBasla(string sinif = null) => KapAc("kart", "kart yz-kart", sinif);
            public void KartBitir() => KapKapat();

            /// <summary>Yatay satır (düğmeler yan yana, sığmazsa alt satıra).</summary>
            public void SatirBasla() => KapAc("satir", "yz-dugmeler", null);
            public void SatirBitir() => KapKapat();

            /// <summary>Tıklanınca açılıp kapanan kart başlığı: büyük yazı, sağda kısa durum ve ok.</summary>
            public void AcilirBaslik(string baslik, string sag, Color? sagRenk, bool acik, Action tik)
            {
                var o = Al("baslikli", () =>
                {
                    var x = new Oge { e = U.El(null, "yz-acilir") };
                    x.a = U.Kalin(x.e, "", "yz-acilir-ad");
                    x.b = U.Kalin(x.e, "", "yz-acilir-sag");
                    x.rozet = U.Yazi(x.e, "", "kucuk", "soluk");
                    x.rozet.style.marginLeft = 8;
                    U.Tikla(x.e, () => x.tik?.Invoke());
                    return x;
                });
                o.tik = tik;
                U.Metin(o.a, baslik);
                U.Metin(o.b, sag ?? "");
                RenkAyarla(o.b, sagRenk);
                U.Metin((Label)o.rozet, acik ? "▲" : "▼");
            }

            void KapAc(string tur, string siniflar, string ekSinif)
            {
                var o = Al(tur, () =>
                {
                    var x = new Oge { e = U.Gecirgen(null, siniflar.Split(' ')) };
                    x.ic = new Kap { e = x.e };
                    return x;
                });
                SinifAyarla(o.e, o, ekSinif);
                o.ic.i = 0;
                yigin.Add(o.ic);
            }

            void KapKapat()
            {
                if (yigin.Count == 0) return;
                var k = yigin[yigin.Count - 1];
                Kirp(k);
                yigin.RemoveAt(yigin.Count - 1);
                // "son" kapsayıcının kendisi olsun: Yardim() karta ipucu verir
                var ust = K;
                son = ust.i > 0 ? ust.ogeler[ust.i - 1] : null;
            }
        }
    }
}
