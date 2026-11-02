using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Hiç görsel dosyası gerekmeden kare ve daire sprite'ları üretir.</summary>
    public static class SpriteFactory
    {
        static Sprite square;
        static Sprite circle;

        public static Sprite Square
        {
            get
            {
                if (square == null) square = Make(false);
                return square;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle == null) circle = Make(true);
                return circle;
            }
        }

        static Sprite Make(bool round)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color32[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte a = 255;
                    if (round)
                    {
                        float dx = x + 0.5f - r;
                        float dy = y + 0.5f - r;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        a = (byte)(Mathf.Clamp01(r - d) * 255f);
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        public static SpriteRenderer Create(string name, Transform parent, bool round, Color color,
                                            Vector3 localPosition, float scale, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = round ? Circle : Square;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }
    }
}
