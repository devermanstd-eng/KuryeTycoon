using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Haritanın üstüne çizilen 2D efektlerin kaydı: hava (WeatherFx), gece/gündüz (DayNight) ve harita katmanı (HeatMap).
    /// Bunlar eskiden OnGUI'de GUI.DrawTexture ile çiziliyordu. IMGUI her zaman UI Toolkit'in üstüne çizildiği için güneş
    /// parlaması, bulutlar ve gece tonu kurye telefonunun, bina kartının ve durum kartlarının üstüne biniyordu (16.11).
    /// Şimdi aynı çağrılar (Renk, Doku, Dondur) dörtgen olarak kaydedilir ve UI Toolkit'te haritanın hemen üstündeki, bütün
    /// arayüzün altındaki katmanda çizilir (Arayuz/UiHaritaKatmani.cs). OnGUI yedeği 25.11'de kaldırıldı (UI Toolkit kurulamazsa
    /// arayüz zaten yok). Kayıt bir karede bir kez yapılır (LateUpdate); çizim aynı kayıttan.
    /// </summary>
    public sealed class Ciz2D
    {
        /// <summary>Hava ve gece/gündüz: ekran pikseli (sol üst köşe 0,0).</summary>
        public static readonly Ciz2D Hava = new Ciz2D();
        /// <summary>Harita katmanı (sipariş yoğunluğu, teslim süresi…): GUI birimi (ekran / arayüz ölçeği).</summary>
        public static readonly Ciz2D Isi = new Ciz2D();

        /// <summary>UI Toolkit katmanı çiziyor mu? (Değilse kayıt yapılır ama çizilmez.)</summary>
        public static bool UitkCiziyor;

        struct Dortgen
        {
            public Rect r;
            public Color32 renk;
            public Texture doku;      // null: düz renk
            public bool donuk;
            public float aci, cos, sin;
            public Vector2 pivot;
        }

        readonly List<Dortgen> liste = new List<Dortgen>(1024);
        Rect alan;
        bool donuk;
        float aci, cos = 1f, sin;
        Vector2 pivot;

        /// <summary>Sıradaki çizimlerin rengi (GUI.color gibi; doku rengiyle çarpılır).</summary>
        public Color Renk = Color.white;

        /// <summary>Kaydın çizileceği alan (kayıt biriminde). UI Toolkit'te bu alana kırpılır.</summary>
        public Rect Alan => alan;

        public int Sayi => liste.Count;

        public void Basla(Rect cizimAlani)
        {
            liste.Clear();
            alan = cizimAlani;
            Renk = Color.white;
            donuk = false;
        }

        /// <summary>GUIUtility.RotateAroundPivot gibi: sonraki dörtgenler pivotun çevresinde döner (derece, ekranda saat yönü).</summary>
        public void Dondur(float derece, Vector2 p)
        {
            aci = derece;
            float r = derece * Mathf.Deg2Rad;
            cos = Mathf.Cos(r);
            sin = Mathf.Sin(r);
            pivot = p;
            donuk = true;
        }

        /// <summary>Dönmeyi bırakır (GUI.matrix'i eski haline döndürmek gibi).</summary>
        public void Duz() => donuk = false;

        /// <summary>GUI.DrawTexture gibi: dokuyu dikdörtgene, Renk ile boyayarak çizer. Texture2D.whiteTexture düz renktir.</summary>
        public void Doku(Rect r, Texture doku)
        {
            if (Renk.a <= 0.002f || r.width <= 0f || r.height <= 0f) return;
            liste.Add(new Dortgen
            {
                r = r,
                renk = Renk,
                doku = doku == Texture2D.whiteTexture ? null : doku,
                donuk = donuk,
                aci = aci,
                cos = cos,
                sin = sin,
                pivot = pivot,
            });
        }

        /// <summary>Yarı saydam düz renk (ton).</summary>
        public void Doldur(Rect r, Color c, float a)
        {
            if (a <= 0.002f) return;
            Renk = new Color(c.r, c.g, c.b, a);
            Doku(r, null);
        }

        static Vector2 Kose(in Dortgen q, float x, float y)
        {
            if (!q.donuk) return new Vector2(x, y);
            float dx = x - q.pivot.x, dy = y - q.pivot.y;
            return new Vector2(q.pivot.x + dx * q.cos - dy * q.sin, q.pivot.y + dx * q.sin + dy * q.cos);
        }

        /// <summary>
        /// UI Toolkit'te çizer: koordinat = (kayıt − alanın sol üstü) × ölçek. Kayıt sırası korunur (ton, bulut, parlama, yağmur
        /// üst üste doğru biner); aynı dokulu ardışık dörtgenler tek ağda.
        /// </summary>
        public void Ciz(MeshGenerationContext mgc, float olcek)
        {
            int n = liste.Count;
            if (n == 0 || olcek <= 0f) return;
            Vector2 o = alan.position;
            int i = 0;
            while (i < n)
            {
                var doku = liste[i].doku;
                int j = i;
                while (j < n && liste[j].doku == doku && j - i < 8000) j++;   // bir ağda en fazla 32 000 köşe
                int adet = j - i;
                var md = mgc.Allocate(adet * 4, adet * 6, doku);
                var uv = new Rect(0f, 0f, 1f, 1f);   // Unity 6: doku koordinatlarını atlasa çizici kendisi çevirir (uvRegion eskidi)
                for (int k = 0; k < adet; k++)
                {
                    var q = liste[i + k];
                    var a = (Kose(q, q.r.xMin, q.r.yMin) - o) * olcek;
                    var b = (Kose(q, q.r.xMax, q.r.yMin) - o) * olcek;
                    var c = (Kose(q, q.r.xMax, q.r.yMax) - o) * olcek;
                    var d = (Kose(q, q.r.xMin, q.r.yMax) - o) * olcek;
                    // GUI.DrawTexture gibi: dokunun üst satırı dikdörtgenin üstünde
                    md.SetNextVertex(new Vertex { position = new Vector3(a.x, a.y, Vertex.nearZ), tint = q.renk, uv = new Vector2(uv.xMin, uv.yMax) });
                    md.SetNextVertex(new Vertex { position = new Vector3(b.x, b.y, Vertex.nearZ), tint = q.renk, uv = new Vector2(uv.xMax, uv.yMax) });
                    md.SetNextVertex(new Vertex { position = new Vector3(c.x, c.y, Vertex.nearZ), tint = q.renk, uv = new Vector2(uv.xMax, uv.yMin) });
                    md.SetNextVertex(new Vertex { position = new Vector3(d.x, d.y, Vertex.nearZ), tint = q.renk, uv = new Vector2(uv.xMin, uv.yMin) });
                    ushort v = (ushort)(k * 4);
                    md.SetNextIndex(v); md.SetNextIndex((ushort)(v + 1)); md.SetNextIndex((ushort)(v + 2));
                    md.SetNextIndex(v); md.SetNextIndex((ushort)(v + 2)); md.SetNextIndex((ushort)(v + 3));
                }
                i = j;
            }
        }

    }
}
