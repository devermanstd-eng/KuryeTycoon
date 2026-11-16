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
    /// Sayı ağırlıklı parçalar: Kutu (başlık, büyük değer, küçük fark), Tablo (sağa yaslı sütunlar), Kaydirici, Onay, CokluSecim;
    /// grafik ve özel çizimler için Ozel (eleman bir kez kurulur, çağıran günceller).
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
                public Label c;
                public Slider kay;
                public Action<float> kayYaz;
                public U.Onay onay;
                public bool deger;
                public Action<bool> onayYaz;
                public readonly List<Label> hucreler = new List<Label>();
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
            int kutuSayac = -1;   // açık Kutular grubundaki kutu sayısı (-1: grup açık değil)

            /// <summary>
            /// Bir satırdaki en fazla kutu (0 = sınırsız). Dar kartlarda (bina kartı) kutular alt satıra flex-wrap ile değil, ayrı
            /// satır kaplarıyla geçer: UI Toolkit sarılan satırın yüksekliğini yanlış hesaplıyor, ikinci satır alttaki içeriğin
            /// üstüne biniyordu (16.11).
            /// </summary>
            public int KutuSutun;

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
                kutuSayac = -1;
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
                if (renk.HasValue) U.Renk(e, U.Okunur(renk.Value));   // kâğıt üstünde açık renkler koyulaşır
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
                if (son.tur == "yazi" || son.tur == "kv" || son.tur == "bolum" || son.tur == "baslikli" || son.tur == "onay" || son.tur == "kaydir")
                    YardimRozeti(son, metin);
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

            /// <summary>Renkli düğme (kurye telefonu): zemin ve yazı rengi satır içi verilir. sinif "vurgu" içermemeli.</summary>
            public void Dugme(string metin, Action tik, bool etkin, string sinif, Color zemin, Color yazi)
            {
                Dugme(metin, tik, etkin, sinif);
                var b = son.d;
                if (b.style.backgroundColor.keyword != StyleKeyword.Undefined || b.style.backgroundColor.value != zemin) U.Zemin(b, zemin);
                if (b.style.color.keyword != StyleKeyword.Undefined || b.style.color.value != yazi) U.Renk(b, yazi);
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
                    else if (sarar) { U.Zemin(l, U.Ton(0.08f)); U.Renk(l, U.Murekkep); }
                    else { U.Zemin(l, new Color(0, 0, 0, 0)); U.Renk(l, U.Soluk); }
                }
            }

            /// <summary>Birden çok seçilebilen çipler (ör. operasyon sorumlusunun teklif edeceği ekipmanlar). Tıklayınca seçim tersine döner.</summary>
            public void CokluSecim(IList<string> adlar, Func<int, bool> secili, Action<int> tik)
            {
                var o = Al("coklu", () =>
                {
                    var x = new Oge { e = U.El(null, "yz-cipler") };
                    x.e.style.marginTop = 2;
                    x.e.style.marginBottom = 4;
                    return x;
                });
                o.secim = tik;
                string imza = string.Join("\u0001", adlar);
                if (imza != o.secImza)
                {
                    o.secImza = imza;
                    o.e.Clear();
                    o.secenekler.Clear();
                    for (int i = 0; i < adlar.Count; i++)
                    {
                        int k = i;
                        var l = U.Kalin(o.e, adlar[i], "yz-cip");
                        U.Tikla(l, () => o.secim?.Invoke(k));
                        o.secenekler.Add(l);
                    }
                }
                for (int i = 0; i < o.secenekler.Count; i++)
                {
                    var l = o.secenekler[i];
                    bool sec = secili(i);
                    U.Metin(l, (sec ? "✓ " : "") + adlar[i]);
                    if (sec) { U.Zemin(l, U.A(UITheme.Accent, 0.85f)); U.Renk(l, UITheme.AccentText); }
                    else { U.Zemin(l, U.Ton(0.08f)); U.Renk(l, U.Soluk); }
                }
            }

            /// <summary>Renkli durum çipleri (tıklanmaz): kayıt aşaması, tür, meslek… Sığmazsa alt satıra geçer.</summary>
            public void Cipler(IList<string> adlar, IList<Color> renkler)
            {
                var o = Al("cipler", () =>
                {
                    var x = new Oge { e = U.Gecirgen(null, "cipler") };
                    x.e.style.marginTop = 2;
                    x.e.style.marginBottom = 2;
                    return x;
                });
                while (o.secenekler.Count < adlar.Count) o.secenekler.Add(U.Cip(o.e, "", Color.white));
                while (o.secenekler.Count > adlar.Count)
                {
                    o.secenekler[o.secenekler.Count - 1].RemoveFromHierarchy();
                    o.secenekler.RemoveAt(o.secenekler.Count - 1);
                }
                for (int i = 0; i < adlar.Count; i++)
                {
                    U.Metin(o.secenekler[i], adlar[i]);
                    U.CipRengi(o.secenekler[i], i < renkler.Count ? renkler[i] : U.Soluk);
                }
            }

            // ------------------------------------------------------------------ sayılar: kutu, tablo

            /// <summary>Yan yana sayı kutuları: KutularBasla(); Kutu(...); Kutu(...); KutularBitir(). Sığmazsa alt satıra geçer.</summary>
            public void KutularBasla()
            {
                KapAc("kutular", "yz-kutular", null);
                kutuSayac = 0;
            }

            public void KutularBitir()
            {
                KapKapat();
                kutuSayac = -1;
            }

            /// <summary>Sayı kutusu: küçük başlık, büyük değer, altında kısa fark/açıklama. İpucu verilirse üstüne gelince açılır.</summary>
            public void Kutu(string baslik, string deger, string alt = null, Color? renk = null, string ipucu = null, Color? altRenk = null)
            {
                if (kutuSayac >= 0)
                {
                    if (KutuSutun > 0 && kutuSayac >= KutuSutun)
                    {
                        // Satır doldu: yeni satır kabı (aynı grubun devamı; KutularBitir bunu kapatır)
                        KapKapat();
                        KapAc("kutular", "yz-kutular", null);
                        kutuSayac = 0;
                    }
                    kutuSayac++;
                }
                var o = Al("kutu", () =>
                {
                    var x = new Oge { e = U.Gecirgen(null, "yz-kutu") };
                    x.a = U.Yazi(x.e, "", "yz-kutu-baslik");
                    x.b = U.Kalin(x.e, "", "yz-kutu-deger");
                    x.c = U.Yazi(x.e, "", "yz-kutu-alt");
                    return x;
                });
                U.Metin(o.a, BuyukHarf(baslik));
                U.Metin(o.b, deger);
                RenkAyarla(o.b, renk);
                bool altVar = !string.IsNullOrEmpty(alt);
                U.Goster(o.c, altVar);
                if (altVar) { U.Metin(o.c, alt); RenkAyarla(o.c, altRenk); }
                if (!string.IsNullOrEmpty(ipucu) || o.ipucuKayitli) IpucuVer(o, o.e, ipucu);
            }

            /// <summary>
            /// Tablo satırı: ilk hücre sola yaslı ve esnek, diğerleri sağa yaslı sabit genişlikte (sütunlar alt alta hizalanır).
            /// baslik: soluk küçük başlık satırı. nokta: ilk hücrenin solunda renkli nokta. renkler: hücre renkleri (null = normal).
            /// Hücreler zengin metin kabul eder (yıldızlar gibi).
            /// </summary>
            public void Tablo(IList<string> hucreler, bool baslik = false, Color? nokta = null, IList<Color?> renkler = null, string ipucu = null)
            {
                var o = Al(baslik ? "tablo-baslik" : "tablo", () =>
                {
                    var x = new Oge { e = U.Gecirgen(null, "yz-tablo-satir") };
                    if (baslik) x.e.AddToClassList("baslik");
                    x.rozet = U.Gecirgen(x.e, "yz-nokta");
                    return x;
                });
                U.Goster(o.rozet, nokta.HasValue);
                if (nokta.HasValue) U.Zemin(o.rozet, nokta.Value);
                while (o.hucreler.Count < hucreler.Count)
                {
                    var l = U.Yazi(o.e, "", o.hucreler.Count == 0 ? "yz-tablo-ilk" : "yz-tablo-hucre");
                    l.enableRichText = true;
                    o.hucreler.Add(l);
                }
                while (o.hucreler.Count > hucreler.Count)
                {
                    o.hucreler[o.hucreler.Count - 1].RemoveFromHierarchy();
                    o.hucreler.RemoveAt(o.hucreler.Count - 1);
                }
                for (int i = 0; i < hucreler.Count; i++)
                {
                    U.Metin(o.hucreler[i], hucreler[i]);
                    RenkAyarla(o.hucreler[i], renkler != null && i < renkler.Count ? renkler[i] : null);
                }
                if (!string.IsNullOrEmpty(ipucu) || o.ipucuKayitli) IpucuVer(o, o.e, ipucu);
            }

            // ------------------------------------------------------------------ ayar kontrolleri

            /// <summary>Onay kutusu (açık/kapalı ayar). Tıklayınca hemen görünür, değer yaz ile modele yazılır.</summary>
            public void Onay(string metin, bool deger, Action<bool> yaz)
            {
                var o = Al("onay", () =>
                {
                    var x = new Oge();
                    x.onay = U.OnayEkle(null, "", () => x.deger, v => { x.deger = v; x.onayYaz?.Invoke(v); });
                    x.e = x.onay.kok;
                    return x;
                });
                o.deger = deger;
                o.onayYaz = yaz;
                U.Metin(o.onay.yazi, metin);
                o.onay.Yenile();
                YardimRozeti(o, null);
            }

            /// <summary>
            /// Etiket · kaydırıcı · değer. Kaydırılan değer yaz ile modele yazılır (adımlama orada yapılır); ekrandaki konum her
            /// yenilemede modeldeki değere döner. Odak almaz: ok tuşları ve boşluk oyunun kısayolları olarak kalır.
            /// </summary>
            public void Kaydirici(string ad, float deger, float min, float max, string degerYazi, Action<float> yaz)
            {
                var o = Al("kaydir", () =>
                {
                    var x = new Oge { e = U.El(null, "yz-kaydir") };
                    x.a = U.Yazi(x.e, "", "yz-kaydir-ad");
                    x.kay = new Slider(0f, 1f);
                    x.kay.AddToClassList("yz-slider");
                    x.kay.focusable = false;
                    x.kay.RegisterValueChangedCallback(ev => x.kayYaz?.Invoke(ev.newValue));
                    x.e.Add(x.kay);
                    x.b = U.Kalin(x.e, "", "yz-kaydir-deger");
                    return x;
                });
                o.kayYaz = yaz;
                U.Metin(o.a, ad);
                if (o.kay.lowValue != min || o.kay.highValue != max)
                {
                    // Sıra önemli: alt sınır üst sınırı geçmesin
                    if (min >= o.kay.highValue) { o.kay.highValue = max; o.kay.lowValue = min; }
                    else { o.kay.lowValue = min; o.kay.highValue = max; }
                }
                float v = Mathf.Clamp(deger, Mathf.Min(min, max), Mathf.Max(min, max));
                if (Mathf.Abs(o.kay.value - v) > 1e-4f) o.kay.SetValueWithoutNotify(v);
                U.Metin(o.b, degerYazi);
                YardimRozeti(o, null);
            }

            /// <summary>
            /// Özel eleman (grafik, saat çizelgesi…): aynı anahtarla ilk çağrıda kur ile kurulur, sonra hep aynısı döner;
            /// çağıran değerlerini kendisi günceller.
            /// </summary>
            public T Ozel<T>(string anahtar, Func<T> kur) where T : VisualElement
            {
                var o = Al("ozel:" + anahtar, () => new Oge { e = kur() });
                return (T)o.e;
            }

            // ------------------------------------------------------------------ çubuk, boşluk

            /// <summary>Ad · çubuk · değer. adGenislik verilirse adın sütunu o kadar (uzun adlar alt satıra kaymasın).</summary>
            public void Cubuk(string ad, float v, Color renk, string deger, float adGenislik = 0f)
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
                if (adGenislik > 0f) { if (o.a.style.width != adGenislik) o.a.style.width = adGenislik; }
                else if (o.a.style.width.keyword != StyleKeyword.Null) o.a.style.width = StyleKeyword.Null;
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
