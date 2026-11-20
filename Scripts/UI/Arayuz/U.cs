using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// UI Toolkit için küçük yapı taşları: eleman, yazı, düğme, simge, çip, çubuk, seviye, onay kutusu, seçenek şeridi.
    /// Görünüm Resources/UI/Arayuz/Kurye.uss'te; burada sadece ağaç kurulur ve değerler güncellenir.
    /// Değer güncelleyen yardımcılar (Metin, Goster, Zemin...) değişmeyen değeri yeniden yazmaz: düzen boşuna yenilenmesin.
    /// </summary>
    public static class U
    {
        // ------------------------------------------------------------------ kurulum

        public static VisualElement El(VisualElement ust, params string[] siniflar)
        {
            var e = new VisualElement();
            foreach (var s in siniflar) if (!string.IsNullOrEmpty(s)) e.AddToClassList(s);
            ust?.Add(e);
            return e;
        }

        /// <summary>Tıklamayı geçiren kapsayıcı (haritaya giden fare olaylarını tutmaz).</summary>
        public static VisualElement Gecirgen(VisualElement ust, params string[] siniflar)
        {
            var e = El(ust, siniflar);
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        public static Label Yazi(VisualElement ust, string metin, params string[] siniflar)
        {
            var l = new Label(metin ?? "");
            l.AddToClassList("yazi");
            foreach (var s in siniflar) if (!string.IsNullOrEmpty(s)) l.AddToClassList(s);
            l.pickingMode = PickingMode.Ignore;
            ust?.Add(l);
            return l;
        }

        public static Label Kalin(VisualElement ust, string metin, params string[] siniflar)
        {
            var l = Yazi(ust, metin, siniflar);
            Kalinlastir(l);
            return l;
        }

        /// <summary>Inter SemiBold (yoksa Unity'nin kalın yazısı).</summary>
        public static void Kalinlastir(TextElement t)
        {
            var f = UITheme.UIFontBold;
            if (f != null) t.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(f));
            else t.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        public static Button Dugme(VisualElement ust, string metin, Action tik, params string[] siniflar)
        {
            var b = new Button(tik) { text = metin ?? "" };
            b.AddToClassList("dg");
            // Odak almasın: odaktaki düğme Boşluk/Enter ile yeniden basılır (Boşluk = duraklat; ör. "Yükselt" iki kez alınmasın)
            b.focusable = false;
            // Düğme bir satırın / kartın içindeyse tıklama satıra geçmesin (satır açılıp kapanmasın)
            b.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            foreach (var s in siniflar) if (!string.IsNullOrEmpty(s)) b.AddToClassList(s);
            Kalinlastir(b);
            ust?.Add(b);
            return b;
        }

        /// <summary>Tıklanınca çalışan herhangi bir eleman (kart, satır, simge).</summary>
        public static T Tikla<T>(T e, Action tik) where T : VisualElement
        {
            e.pickingMode = PickingMode.Position;
            e.RegisterCallback<ClickEvent>(ev =>
            {
                if (ev.button != 0) return;
                tik?.Invoke();
                ev.StopPropagation();
            });
            return e;
        }

        public static VisualElement Simge(VisualElement ust, Icons.Kind k, Color renk, float boy, params string[] siniflar)
        {
            var e = El(ust, siniflar);
            e.pickingMode = PickingMode.Ignore;
            if (boy > 0f) { e.style.width = boy; e.style.height = boy; e.style.flexShrink = 0; }
            SimgeAyarla(e, k, renk);
            return e;
        }

        /// <summary>
        /// Simgeler elle çizilmiş renkli PNG'lerdir (Resources/UI/Ikonlar/&lt;Ad&gt;.png, Icons.ElCizimi): kendi
        /// renkleriyle çizilir, verilen rengin yalnızca saydamlığı kullanılır. PNG'si olmayan simge ve Kapat (×) eskisi gibi tek
        /// renk (soluk) çizilir: küçük kapat düğmesinde renkli daire okunmuyor.
        /// </summary>
        public static bool ElCizimiSimge = true;

        public static void SimgeAyarla(VisualElement e, Icons.Kind k, Color renk)
        {
            Texture2D tex = ElCizimiSimge && k != Icons.Kind.Close ? Icons.ElCizimi(k) : null;
            Color ton = tex != null ? new Color(1f, 1f, 1f, renk.a) : renk;
            if (tex == null) tex = Icons.Get(k);
            if (e.userData is Icons.Kind eski && eski == k && e.style.unityBackgroundImageTintColor.value == ton) return;
            e.userData = k;
            e.style.backgroundImage = new StyleBackground(tex);
            e.style.unityBackgroundImageTintColor = ton;
#if UNITY_2022_2_OR_NEWER
            e.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
#endif
        }

        public static Label Cip(VisualElement ust, string metin, Color renk)
        {
            var l = Yazi(ust, metin, "cip");
            Kalinlastir(l);
            CipRengi(l, renk);
            return l;
        }

        /// <summary>Çip: rengin saydam tonu zemin, rengin açığı yazı (koyu panel üstünde okunur).</summary>
        public static void CipRengi(VisualElement l, Color renk)
        {
            Zemin(l, A(renk, 0.18f));
            Renk(l, Okunur(Color.Lerp(renk, Color.white, 0.35f)));
        }

        public static Label Bolum(VisualElement ust, string metin)
        {
            var s = El(ust, "bolum");
            var l = Kalin(s, metin, "bolum-yazi");
            El(s, "bolum-cizgi");
            return l;
        }

        public class Cubuk
        {
            public VisualElement kok, dolu;
            public void Ayarla(float v, Color c)
            {
                v = Mathf.Clamp01(v);
                var w = new Length(v * 100f, LengthUnit.Percent);
                if (dolu.style.width != w) dolu.style.width = w;
                Zemin(dolu, c);
            }
        }

        public static Cubuk CubukEkle(VisualElement ust, float v, Color c)
        {
            var k = El(ust, "cubuk");
            var d = El(k, "cubuk-dolu");
            var cb = new Cubuk { kok = k, dolu = d };
            cb.Ayarla(v, c);
            return cb;
        }

        /// <summary>Seviye noktaları: dolu olanlar renkli.</summary>
        public static VisualElement Seviyeler(VisualElement ust, int dolu, int toplam, Color renk)
        {
            var k = El(ust, "seviyeler");
            SeviyeAyarla(k, dolu, toplam, renk);
            return k;
        }

        public static void SeviyeAyarla(VisualElement k, int dolu, int toplam, Color renk)
        {
            while (k.childCount < toplam) El(k, "seviye");
            while (k.childCount > toplam) k.RemoveAt(k.childCount - 1);
            for (int i = 0; i < toplam; i++)
                Zemin(k[i], i < dolu ? renk : Ton(0.14f));
        }

        /// <summary>Kendi çizdiğimiz onay kutusu (varsayılan Toggle tema ile uyumsuz).</summary>
        public class Onay
        {
            public VisualElement kok, kutu, isaret;
            public Label yazi;
            public Func<bool> oku;
            public Action<bool> yaz;
            public void Yenile()
            {
                bool v = oku();
                Goster(isaret, v);
                Zemin(kutu, v ? UITheme.Accent : new Color(0, 0, 0, 0));
                kutu.style.borderTopColor = kutu.style.borderBottomColor = kutu.style.borderLeftColor = kutu.style.borderRightColor =
                    v ? UITheme.Accent : Ton(0.4f);
            }
        }

        public static Onay OnayEkle(VisualElement ust, string metin, Func<bool> oku, Action<bool> yaz)
        {
            var o = new Onay { oku = oku, yaz = yaz };
            o.kok = El(ust, "onay");
            o.kutu = El(o.kok, "onay-kutu");
            o.isaret = Simge(o.kutu, Icons.Kind.Close, Color.white, 0f, "onay-isaret");
            OnayIsareti(o.isaret);
            o.yazi = Yazi(o.kok, metin, "onay-yazi");
            Tikla(o.kok, () => { o.yaz(!o.oku()); o.Yenile(); });
            o.Yenile();
            return o;
        }

        static Texture2D onayTex;

        /// <summary>Tik işareti (çalışma anında çizilir).</summary>
        static void OnayIsareti(VisualElement e)
        {
            if (onayTex == null)
            {
                const int n = 48;
                onayTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                Vector2 a = new Vector2(0.18f, 0.52f), b = new Vector2(0.42f, 0.28f), c = new Vector2(0.84f, 0.74f);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        var p = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
                        float d = Mathf.Min(SegMesafe(p, a, b), SegMesafe(p, b, c));
                        float al = Mathf.Clamp01((0.085f - d) * n * 0.6f);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(al * 255f));
                    }
                onayTex.SetPixels32(px);
                onayTex.Apply();
            }
            e.userData = null;
            e.style.backgroundImage = new StyleBackground(onayTex);
            e.style.unityBackgroundImageTintColor = Color.white;
        }

        static float SegMesafe(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Seçenek şeridi (radyo düğmeleri).</summary>
        public class Secenekler
        {
            public VisualElement kok;
            public readonly List<Label> ogeler = new List<Label>();
            public Func<int> oku;
            public void Yenile()
            {
                int s = oku();
                for (int i = 0; i < ogeler.Count; i++)
                {
                    bool sec = i == s;
                    ogeler[i].EnableInClassList("secili", sec);
                    Zemin(ogeler[i], sec ? UITheme.Accent : new Color(0, 0, 0, 0));
                    Renk(ogeler[i], sec ? UITheme.AccentText : Soluk);
                }
            }
        }

        public static Secenekler SecenekEkle(VisualElement ust, string[] adlar, Func<int> oku, Action<int> yaz)
        {
            var s = new Secenekler { oku = oku };
            s.kok = El(ust, "secenekler");
            for (int i = 0; i < adlar.Length; i++)
            {
                int k = i;
                var l = Kalin(s.kok, adlar[i], "secenek");
                l.pickingMode = PickingMode.Position;
                Tikla(l, () => { yaz(k); s.Yenile(); });
                s.ogeler.Add(l);
            }
            s.Yenile();
            return s;
        }

        // ------------------------------------------------------------------ güncelleme

        public static void Metin(TextElement l, string m)
        {
            m = m ?? "";
            if (l.text != m) l.text = m;
        }

        public static void Goster(VisualElement e, bool goster)
        {
            var d = goster ? DisplayStyle.Flex : DisplayStyle.None;
            if (e.style.display != d) e.style.display = d;
        }

        public static bool Gorunur(VisualElement e) => e.style.display != DisplayStyle.None;

        public static void Zemin(VisualElement e, Color c)
        {
            if (e.style.backgroundColor != c) e.style.backgroundColor = c;
        }

        public static void Renk(VisualElement e, Color c)
        {
            if (e.style.color != c) e.style.color = c;
        }

        public static void Sinif(VisualElement e, string s, bool var) => e.EnableInClassList(s, var);

        public static Color A(Color c, float a) { c.a = a; return c; }

        // ------------------------------------------------------------------ koyu mürekkep teması (16.11 akşam)
        // Kurye.uss'in :root değişkenleriyle aynı renkler. Koddan satır içi verilen renkler buradan gelir: koyu kömür panel
        // üstünde okunur açık tonlar. UITheme.Text/Good/Bad/Warn IMGUI için (harita etiketleri) kaldı;
        // UI Toolkit kodu onların yerine bunları kullanır. (Önce krem kâğıt denendi; açık haritada eridiği için koyu seçildi.)

        /// <summary>Panel zemini (pencere, yüzen kutular): kömür.</summary>
        public static readonly Color PanelRengi = new Color32(30, 27, 25, 255);
        /// <summary>Şerit (sol menü, pencerenin arkası): en koyu.</summary>
        public static readonly Color SeritRengi = new Color32(20, 18, 16, 255);
        /// <summary>Yazı: açık krem.</summary>
        public static readonly Color YaziRengi = new Color32(245, 238, 226, 255);
        /// <summary>Çerçeve: siyah mürekkep.</summary>
        public static readonly Color Cerceve = new Color32(8, 7, 6, 255);
        /// <summary>Soluk yazı (sıcak gri).</summary>
        public static readonly Color Soluk = new Color32(178, 166, 152, 255);
        public static readonly Color Notr = Soluk;
        /// <summary>Adaçayı yeşili: kâr, artış, tamam.</summary>
        public static readonly Color Iyi = new Color32(132, 204, 118, 255);
        /// <summary>Kiremit kırmızısı: zarar, hata.</summary>
        public static readonly Color Kotu = new Color32(238, 110, 88, 255);
        /// <summary>Terracotta turuncu: uyarı, fiyat savaşı.</summary>
        public static readonly Color Uyari = new Color32(245, 165, 80, 255);
        /// <summary>Hardal sarısı.</summary>
        public static readonly Color Hardal = new Color32(242, 196, 84, 255);

        /// <summary>Açık katman: koyu panel üstünde üstüne gelme, boş seviye noktası, ince zemin ve çizgi.</summary>
        public static Color Ton(float a) => new Color(1f, 0.94f, 0.87f, a);

        static float Parlaklik(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>
        /// Koyu panel üstünde okunur yazı rengi: açık renkler aynen kalır; koyu renkler tonu korunarak açılır, koyu gri/siyah
        /// yazı rengine ya da soluğa döner. Oyun verisinden gelen (dükkan, olay, etkinlik) koyu renkler için.
        /// Renkli zemin üstündeki yazıya (şirket rengi düğme, açık hap) uygulanmaz.
        /// </summary>
        public static Color Okunur(Color c)
        {
            float l = Parlaklik(c);
            if (l >= 0.42f) return c;
            float doy = Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            Color r;
            if (doy < 0.15f) r = l < 0.2f ? YaziRengi : Soluk;
            else r = Color.Lerp(c, Color.white, (0.5f - l) / (1f - l));
            r.a = c.a;
            return r;
        }

        public static string Ft(long v) => GameData.Ft(v);

        /// <summary>
        /// Anahtarlı liste eşitleyici: listedeki her öğe için bir satır tutar; yeni öğeye satır açar, gideni siler,
        /// sırayı listeye uydurur ve her satırı günceller. Satırlar yeniden kurulmadığı için üstüne gelme ve tıklama bozulmaz.
        /// </summary>
        public class Liste<TAnahtar, TOge, TSatir> where TSatir : class
        {
            readonly VisualElement kap;
            readonly Func<TOge, TAnahtar> anahtar;
            readonly Func<TOge, TSatir> kur;
            readonly Func<TSatir, VisualElement> eleman;
            readonly Action<TSatir, TOge> guncelle;
            readonly Dictionary<TAnahtar, TSatir> satirlar = new Dictionary<TAnahtar, TSatir>();
            readonly HashSet<TAnahtar> gorulen = new HashSet<TAnahtar>();
            readonly List<TAnahtar> silinecek = new List<TAnahtar>();

            public Liste(VisualElement kap, Func<TOge, TAnahtar> anahtar, Func<TOge, TSatir> kur,
                         Func<TSatir, VisualElement> eleman, Action<TSatir, TOge> guncelle)
            {
                this.kap = kap; this.anahtar = anahtar; this.kur = kur; this.eleman = eleman; this.guncelle = guncelle;
            }

            public int Sayi => satirlar.Count;

            public void Esitle(IEnumerable<TOge> ogeler)
            {
                gorulen.Clear();
                int i = 0;
                foreach (var o in ogeler)
                {
                    var k = anahtar(o);
                    if (!gorulen.Add(k)) continue;
                    if (!satirlar.TryGetValue(k, out var s))
                    {
                        s = kur(o);
                        satirlar[k] = s;
                    }
                    var e = eleman(s);
                    if (e.parent != kap || kap.IndexOf(e) != i)
                    {
                        if (e.parent != null) e.RemoveFromHierarchy();
                        kap.Insert(Mathf.Min(i, kap.childCount), e);
                    }
                    guncelle(s, o);
                    i++;
                }
                silinecek.Clear();
                foreach (var kv in satirlar) if (!gorulen.Contains(kv.Key)) silinecek.Add(kv.Key);
                foreach (var k in silinecek)
                {
                    eleman(satirlar[k]).RemoveFromHierarchy();
                    satirlar.Remove(k);
                }
            }

            public void Temizle()
            {
                foreach (var s in satirlar.Values) eleman(s).RemoveFromHierarchy();
                satirlar.Clear();
            }
        }
    }
}
