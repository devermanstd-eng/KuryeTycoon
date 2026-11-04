using System;
using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Arayüz simgeleri: dosya gerekmez, çalışma anında mesafe fonksiyonlarıyla (SDF) kenarları yumuşatılmış
    /// beyaz simgeler çizilir. GUI.color ile istenen renge boyanır.
    /// Koordinatlar −1…1, y yukarı.
    /// </summary>
    public static class Icons
    {
        public enum Kind { Orders, Couriers, Hiring, Support, Upgrades, Shops, Equipment, Campaigns, Finance, Settings, Close }

        const int Size = 96;
        static readonly Dictionary<Kind, Texture2D> cache = new Dictionary<Kind, Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cache.Clear();

        public static Texture2D Get(Kind k)
        {
            if (cache.TryGetValue(k, out var t) && t != null) return t;
            t = Rasterize(Shape(k));
            cache[k] = t;
            return t;
        }

        /// <summary>Simgeyi verilen renkte çizer.</summary>
        public static void Draw(Rect r, Kind k, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Get(k), ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }

        static Texture2D Rasterize(Func<Vector2, float> sdf)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var px = new Color32[Size * Size];
            float pixel = 2f / Size;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f);
                    float d = sdf(p * 1.08f);   // biraz kenar boşluğu
                    float a = Mathf.Clamp01(0.5f - d / pixel);
                    px[y * Size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        // ---------- Mesafe fonksiyonları ----------

        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
        static float Ring(Vector2 p, Vector2 c, float r, float th) => Mathf.Abs(Circle(p, c, r)) - th;

        static float Box(Vector2 p, Vector2 c, Vector2 h, float r = 0f)
        {
            var q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - (h - new Vector2(r, r));
            var mq = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            return mq.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            var pa = p - a; var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static float Triangle(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            Vector2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
            Vector2 v0 = p - p0, v1 = p - p1, v2 = p - p2;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float s = Mathf.Sign(Cross(e0, e2));
            float dx = Mathf.Min(Mathf.Min(pq0.sqrMagnitude, pq1.sqrMagnitude), pq2.sqrMagnitude);
            float dy = Mathf.Min(Mathf.Min(s * Cross(v0, e0), s * Cross(v1, e1)), s * Cross(v2, e2));
            return -Mathf.Sqrt(dx) * Mathf.Sign(dy);
        }

        static float U(float a, float b) => Mathf.Min(a, b);          // birleşim
        static float Sub(float a, float b) => Mathf.Max(a, -b);       // a eksi b

        static Vector2 V(float x, float y) => new Vector2(x, y);

        // ---------- Simgeler ----------

        static Func<Vector2, float> Shape(Kind k)
        {
            switch (k)
            {
                case Kind.Orders: // alışveriş çantası
                    return p =>
                    {
                        float body = Box(p, V(0f, -0.25f), V(0.66f, 0.58f), 0.12f);
                        float handle = Mathf.Max(Ring(p, V(0f, 0.33f), 0.3f, 0.08f), -(p.y - 0.33f));
                        return U(body, handle);
                    };
                case Kind.Couriers: // kişi
                    return p =>
                    {
                        float head = Circle(p, V(0f, 0.42f), 0.32f);
                        float body = Mathf.Max(Box(p, V(0f, -0.62f), V(0.7f, 0.45f), 0.4f), -(p.y + 0.86f));
                        return U(head, body);
                    };
                case Kind.Hiring: // kişi + artı
                    return p =>
                    {
                        float head = Circle(p, V(-0.22f, 0.36f), 0.28f);
                        float body = Mathf.Max(Box(p, V(-0.22f, -0.6f), V(0.58f, 0.4f), 0.34f), -(p.y + 0.86f));
                        float plus = U(Box(p, V(0.6f, 0.42f), V(0.32f, 0.09f), 0.04f), Box(p, V(0.6f, 0.42f), V(0.09f, 0.32f), 0.04f));
                        return U(U(head, body), plus);
                    };
                case Kind.Support: // konuşma balonu
                    return p =>
                    {
                        float bubble = U(Box(p, V(0f, 0.12f), V(0.86f, 0.62f), 0.32f), Triangle(p, V(-0.5f, -0.35f), V(-0.05f, -0.35f), V(-0.6f, -0.88f)));
                        float dots = U(U(Circle(p, V(-0.38f, 0.12f), 0.12f), Circle(p, V(0f, 0.12f), 0.12f)), Circle(p, V(0.38f, 0.12f), 0.12f));
                        return Sub(bubble, dots);
                    };
                case Kind.Upgrades: // yukarı ok
                    return p =>
                    {
                        float head = Triangle(p, V(-0.72f, 0.02f), V(0.72f, 0.02f), V(0f, 0.86f));
                        float shaft = Box(p, V(0f, -0.42f), V(0.25f, 0.46f), 0.06f);
                        return U(head, shaft);
                    };
                case Kind.Shops: // dükkan cephesi + tente
                    return p =>
                    {
                        float body = Sub(Box(p, V(0f, -0.4f), V(0.72f, 0.48f), 0.06f), Box(p, V(0f, -0.58f), V(0.2f, 0.32f), 0.04f));
                        float awning = Box(p, V(0f, 0.42f), V(0.86f, 0.2f), 0.06f);
                        float scallops = 1f;
                        for (int i = 0; i < 4; i++) scallops = U(scallops, Circle(p, V(-0.645f + i * 0.43f, 0.22f), 0.215f));
                        float top = Box(p, V(0f, 0.72f), V(0.7f, 0.1f), 0.05f);
                        return U(U(Sub(body, Box(p, V(0f, 0.12f), V(0.9f, 0.08f))), U(awning, scallops)), top);
                    };
                case Kind.Equipment: // sırt çantası
                    return p =>
                    {
                        float body = Box(p, V(0f, -0.15f), V(0.62f, 0.72f), 0.26f);
                        float handle = Mathf.Max(Ring(p, V(0f, 0.6f), 0.22f, 0.075f), -(p.y - 0.6f));
                        float pocket = Mathf.Abs(Box(p, V(0f, -0.42f), V(0.36f, 0.24f), 0.1f)) - 0.055f;
                        float flap = Box(p, V(0f, 0.18f), V(0.62f, 0.035f));
                        return Sub(Sub(U(body, handle), pocket), flap);
                    };
                case Kind.Campaigns: // yüzde işareti
                    return p =>
                    {
                        float a = Ring(p, V(-0.42f, 0.46f), 0.2f, 0.085f);
                        float b = Ring(p, V(0.42f, -0.46f), 0.2f, 0.085f);
                        float slash = Segment(p, V(0.55f, 0.72f), V(-0.55f, -0.72f), 0.095f);
                        return U(U(a, b), slash);
                    };
                case Kind.Finance: // çubuk grafik
                    return p =>
                    {
                        float b1 = Box(p, V(-0.55f, -0.5f), V(0.17f, 0.28f), 0.05f);
                        float b2 = Box(p, V(0f, -0.3f), V(0.17f, 0.48f), 0.05f);
                        float b3 = Box(p, V(0.55f, -0.08f), V(0.17f, 0.7f), 0.05f);
                        float baseLine = Box(p, V(0f, -0.88f), V(0.86f, 0.055f), 0.03f);
                        return U(U(U(b1, b2), b3), baseLine);
                    };
                case Kind.Settings: // çark
                    return p =>
                    {
                        float ang = Mathf.Atan2(p.y, p.x);
                        float teeth = Mathf.Clamp(Mathf.Cos(ang * 8f) * 3f, -1f, 1f);
                        float outer = p.magnitude - (0.66f + 0.13f * teeth);
                        return Sub(outer, Circle(p, Vector2.zero, 0.27f));
                    };
                default: // kapat (çarpı)
                    return p => U(Segment(p, V(-0.6f, -0.6f), V(0.6f, 0.6f), 0.11f), Segment(p, V(-0.6f, 0.6f), V(0.6f, -0.6f), 0.11f));
            }
        }
    }
}
