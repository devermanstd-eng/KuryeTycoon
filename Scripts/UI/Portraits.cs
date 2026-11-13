using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Kurye ve başvuru kartlarındaki küçük profil resmi (kimlik kartı gibi). Dosya gerekmez:
    /// isimden (aynı isim = hep aynı yüz) ten rengi, saç modeli ve rengi, gözlük, bıyık ve arka plan seçilir,
    /// Icons gibi mesafe fonksiyonlarıyla yumuşak kenarlı bir doku çizilir ve önbelleğe alınır.
    ///  • İşe alınan kuryenin tişörtü şirket renginde; başvuranlar kendi kıyafetiyle.
    ///  • Yüz ifadesi moralini gösterir: gülümsüyor / düz / üzgün.
    /// Uzun listede takılma olmasın diye bir karede en fazla birkaç yeni resim çizilir; sırası gelmeyen yerine
    /// bir karelik düz renk kutu görünür.
    /// </summary>
    public static class Portraits
    {
        const int Size = 112;
        const int BuildPerFrame = 2;

        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static int builtFrame = -1, builtThisFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
            builtFrame = -1;
            builtThisFrame = 0;
        }

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        static readonly Color[] Skins =
        {
            C(0.99f, 0.87f, 0.76f), C(0.95f, 0.78f, 0.63f), C(0.87f, 0.67f, 0.5f),
            C(0.74f, 0.53f, 0.37f), C(0.58f, 0.4f, 0.27f), C(0.42f, 0.29f, 0.2f)
        };

        static readonly Color[] Hairs =
        {
            C(0.11f, 0.09f, 0.09f), C(0.24f, 0.16f, 0.11f), C(0.42f, 0.27f, 0.16f),
            C(0.84f, 0.68f, 0.4f), C(0.6f, 0.27f, 0.14f), C(0.68f, 0.68f, 0.7f)
        };

        static readonly Color[] Backs =
        {
            C(0.29f, 0.41f, 0.6f), C(0.33f, 0.52f, 0.46f), C(0.6f, 0.41f, 0.36f), C(0.47f, 0.39f, 0.6f),
            C(0.56f, 0.5f, 0.33f), C(0.32f, 0.48f, 0.57f), C(0.54f, 0.35f, 0.47f), C(0.38f, 0.42f, 0.48f)
        };

        static readonly Color[] Shirts =
        {
            C(0.86f, 0.86f, 0.89f), C(0.22f, 0.24f, 0.29f), C(0.74f, 0.29f, 0.29f), C(0.29f, 0.48f, 0.74f),
            C(0.33f, 0.58f, 0.39f), C(0.9f, 0.68f, 0.28f), C(0.52f, 0.36f, 0.66f)
        };

        static readonly Color Ink = C(0.1f, 0.09f, 0.1f);
        static readonly Color Lips = C(0.5f, 0.2f, 0.2f);

        /// <summary>Moral (0–1) → ifade: 0 üzgün, 1 düz, 2 gülümsüyor.</summary>
        public static int MoodOf(float morale) => morale < 0.35f ? 0 : morale < 0.7f ? 1 : 2;

        /// <summary>Profil resmini çizer. shirt null ise kişinin kendi kıyafet rengi.</summary>
        public static void Draw(Rect r, string seed, int mood, Color? shirt = null)
        {
            if (Event.current.type != EventType.Repaint) return;
            var tex = Get(seed ?? "", mood, shirt);
            if (tex == null)
            {
                UITheme.Box(r, Backs[Hash(seed ?? "") % (uint)Backs.Length], r.width * 0.16f);
                return;
            }
            var old = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }

        /// <summary>Profil resmi dokusu (UI Toolkit için). Bu karede üretim sınırı dolduysa null: bir sonraki yenilemede gelir.</summary>
        public static Texture2D Doku(string seed, int mood, Color? shirt = null) => Get(seed ?? "", mood, shirt);

        static Texture2D Get(string seed, int mood, Color? shirt)
        {
            uint h = Hash(seed);
            Color sc = shirt ?? Shirts[(h >> 7) % (uint)Shirts.Length];
            string key = seed + "|" + mood + "|" + ColorUtility.ToHtmlStringRGB(sc);
            if (cache.TryGetValue(key, out var t) && t != null) return t;

            if (builtFrame != Time.frameCount) { builtFrame = Time.frameCount; builtThisFrame = 0; }
            if (builtThisFrame >= BuildPerFrame) return null;   // sonraki karede
            builtThisFrame++;

            if (cache.Count > 240)
            {
                foreach (var old in cache.Values) if (old != null) Object.Destroy(old);
                cache.Clear();
            }
            t = Build(h, Mathf.Clamp(mood, 0, 2), sc);
            cache[key] = t;
            return t;
        }

        /// <summary>FNV-1a: isimden kalıcı sayı (oyun her açıldığında aynı yüz).</summary>
        static uint Hash(string s)
        {
            uint h = 2166136261u;
            foreach (char ch in s) { h ^= ch; h *= 16777619u; }
            return h == 0 ? 1u : h;
        }

        // ---------------------------------------------------------------- çizim

        struct Rng
        {
            uint s;
            public Rng(uint seed) { s = seed == 0 ? 1u : seed; }
            public int Pick(int n)
            {
                s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                return (int)(s % (uint)n);
            }
        }

        static Texture2D Build(uint h, int mood, Color shirt)
        {
            var rng = new Rng(h);
            Color bg = Backs[rng.Pick(Backs.Length)];
            Color skin = Skins[rng.Pick(Skins.Length)];
            Color hair = Hairs[rng.Pick(Hairs.Length)];
            int style = rng.Pick(6);            // 0 kısa, 1 çok kısa, 2 uzun, 3 topuz, 4 kıvırcık, 5 şapka
            bool glasses = rng.Pick(100) < 18;
            bool mustache = style != 2 && style != 3 && rng.Pick(100) < 16;
            float hw = 0.35f + rng.Pick(5) * 0.012f;   // yüz genişliği
            Color capCol = Shirts[rng.Pick(Shirts.Length)];
            if (style == 5 && Mathf.Abs(capCol.grayscale - shirt.grayscale) < 0.05f) capCol = Color.Lerp(capCol, Color.black, 0.35f);

            Color skinShade = skin * 0.86f;
            Color shirtShade = shirt * 0.8f;
            Color brow = Color.Lerp(hair, Ink, 0.35f);
            var head = new Vector2(0f, 0.08f);
            const float hh = 0.43f;

            var px = new Color32[Size * Size];
            const float zoom = 0.84f;            // yüz kareyi doldursun, omuzların altı kesilsin
            float pixel = 2f / Size * zoom;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    var raw = new Vector2((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f);
                    var p = raw * zoom + new Vector2(0f, 0.03f);

                    // Arka plan: üstü biraz açık
                    Color col = Color.Lerp(bg * 0.82f, bg * 1.12f, (p.y + 1f) * 0.5f);

                    // Saçın arkası (uzun saç)
                    if (style == 2) Paint(ref col, Box(p, new Vector2(0f, -0.06f), new Vector2(hw + 0.09f, 0.44f), 0.22f), hair, pixel);

                    // Omuzlar, yaka, boyun
                    Paint(ref col, Box(p, new Vector2(0f, -1.0f), new Vector2(0.68f, 0.44f), 0.38f), shirt, pixel);
                    Paint(ref col, Box(p, new Vector2(0f, -0.44f), new Vector2(0.135f, 0.17f), 0.05f), skinShade, pixel);
                    Paint(ref col, Tri(p, new Vector2(-0.2f, -0.56f), new Vector2(0.2f, -0.56f), new Vector2(0f, -0.76f)), skinShade, pixel);
                    Paint(ref col, Mathf.Abs(Tri(p, new Vector2(-0.2f, -0.56f), new Vector2(0.2f, -0.56f), new Vector2(0f, -0.76f))) - 0.018f, shirtShade, pixel);

                    // Kulaklar ve yüz
                    Paint(ref col, Mathf.Min(Circle(p, new Vector2(-hw, 0.04f), 0.085f), Circle(p, new Vector2(hw, 0.04f), 0.085f)), skinShade, pixel);
                    Paint(ref col, Ellipse(p, head, hw, hh), skin, pixel);

                    // Saç / şapka
                    Paint(ref col, HairFront(p, style, hw, head), style == 5 ? capCol : hair, pixel);
                    if (style == 5) Paint(ref col, Box(p, new Vector2(0.2f, 0.27f), new Vector2(0.32f, 0.045f), 0.04f), capCol * 0.8f, pixel);

                    // Kaşlar (üzgünken içleri yukarı kalkar)
                    float lift = mood == 0 ? 0.04f : 0f;
                    Paint(ref col, Mathf.Min(Seg(p, new Vector2(-0.22f, 0.17f), new Vector2(-0.08f, 0.17f + lift), 0.022f),
                                             Seg(p, new Vector2(0.08f, 0.17f + lift), new Vector2(0.22f, 0.17f), 0.022f)), brow, pixel);

                    // Gözler
                    Paint(ref col, Mathf.Min(Circle(p, new Vector2(-0.145f, 0.07f), 0.043f), Circle(p, new Vector2(0.145f, 0.07f), 0.043f)), Ink, pixel);

                    // Bıyık, ağız
                    if (mustache) Paint(ref col, Box(p, new Vector2(0f, -0.1f), new Vector2(0.12f, 0.032f), 0.03f), hair, pixel);
                    float mouth = mood == 2 ? Mathf.Max(Mathf.Abs(Circle(p, new Vector2(0f, -0.02f), 0.17f)) - 0.026f, p.y + 0.1f)
                                : mood == 1 ? Seg(p, new Vector2(-0.095f, -0.17f), new Vector2(0.095f, -0.17f), 0.026f)
                                : Mathf.Max(Mathf.Abs(Circle(p, new Vector2(0f, -0.34f), 0.16f)) - 0.026f, -(p.y + 0.22f));
                    Paint(ref col, mouth, Lips, pixel);

                    // Gözlük
                    if (glasses)
                    {
                        float g = Mathf.Min(Mathf.Abs(Circle(p, new Vector2(-0.145f, 0.07f), 0.088f)) - 0.017f,
                                            Mathf.Abs(Circle(p, new Vector2(0.145f, 0.07f), 0.088f)) - 0.017f);
                        g = Mathf.Min(g, Seg(p, new Vector2(-0.058f, 0.08f), new Vector2(0.058f, 0.08f), 0.016f));
                        Paint(ref col, g, Ink, pixel);
                    }

                    // Kart köşeleri yuvarlak
                    float frame = Box(raw, Vector2.zero, new Vector2(1f, 1f), 0.3f);
                    float a = Cov(frame, 2f / Size);
                    px[y * Size + x] = new Color(col.r, col.g, col.b, a);
                }

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        static float HairFront(Vector2 p, int style, float hw, Vector2 head)
        {
            // Saç çizgisi hafif eğik (yüz simetrik görünmesin)
            float cap = Mathf.Max(Circle(p, head + new Vector2(0f, 0.04f), hw + 0.05f), -(p.y - (0.2f + 0.1f * p.x)));
            switch (style)
            {
                case 1: // çok kısa
                    return Mathf.Max(Circle(p, head + new Vector2(0f, 0.02f), hw + 0.018f), -(p.y - 0.3f));
                case 2: // uzun: yanlarda perçemler
                    {
                        float sides = Mathf.Min(Box(p, new Vector2(-hw + 0.01f, -0.02f), new Vector2(0.075f, 0.3f), 0.07f),
                                                Box(p, new Vector2(hw - 0.01f, -0.02f), new Vector2(0.075f, 0.3f), 0.07f));
                        return Mathf.Min(cap, sides);
                    }
                case 3: // topuz
                    return Mathf.Min(cap, Circle(p, new Vector2(0f, 0.64f), 0.17f));
                case 4: // kıvırcık
                    {
                        float d = Mathf.Max(Circle(p, head + new Vector2(0f, 0.05f), hw + 0.04f), -(p.y - 0.24f));
                        for (int i = 0; i < 7; i++)
                        {
                            float ang = Mathf.Lerp(0.15f, Mathf.PI - 0.15f, i / 6f);
                            var c = head + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (hw + 0.04f);
                            d = Mathf.Min(d, Circle(p, c, 0.12f));
                        }
                        return Mathf.Max(d, -(p.y - 0.05f));
                    }
                case 5: // şapka (tepe)
                    return Mathf.Max(Circle(p, head + new Vector2(0f, 0.05f), hw + 0.06f), -(p.y - 0.26f));
                default: // kısa
                    return cap;
            }
        }

        static float Cov(float d, float pixel) => Mathf.Clamp01(0.5f - d / pixel);

        static void Paint(ref Color c, float d, Color layer, float pixel)
        {
            float a = Cov(d, pixel);
            if (a > 0f) c = Color.Lerp(c, layer, a);
        }

        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        static float Ellipse(Vector2 p, Vector2 c, float rx, float ry)
        {
            var q = new Vector2((p.x - c.x) / rx, (p.y - c.y) / ry);
            return (q.magnitude - 1f) * Mathf.Min(rx, ry);
        }

        static float Box(Vector2 p, Vector2 c, Vector2 h, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - (h - new Vector2(r, r));
            var mq = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            return mq.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        static float Seg(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            var pa = p - a; var ba = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * t).magnitude - r;
        }

        static float Tri(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            Vector2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
            Vector2 v0 = p - p0, v1 = p - p1, v2 = p - p2;
            Vector2 q0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 q1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 q2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            float dx = Mathf.Min(Mathf.Min(q0.sqrMagnitude, q1.sqrMagnitude), q2.sqrMagnitude);
            float dy = Mathf.Min(Mathf.Min(s * (v0.x * e0.y - v0.y * e0.x), s * (v1.x * e1.y - v1.y * e1.x)), s * (v2.x * e2.y - v2.y * e2.x));
            return -Mathf.Sqrt(dx) * Mathf.Sign(dy);
        }
    }
}
