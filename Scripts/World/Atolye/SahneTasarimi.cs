using System;
using System.Collections.Generic;

namespace KuryeTycoon
{
    /// <summary>
    /// Bina Atölyesi sahnesi (Sahne Atölyesi penceresi): n × n hücrelik zemin (çim, yol, kaldırım, meydan, patika, su, tarh, çit;
    /// hücre başına 0–2 yükselti kademesi), tasarım adıyla yerleştirilmiş binalar ve serbest süsler. JSON olarak saklanır
    /// (Resources/Atolye/Sahneler/sahne_&lt;ad&gt;.json); alan adları Tools/Blender/pastel_parcalar.py'deki sahne sözlüğüyle aynı.
    /// Model dosyası yoktur: AtolyeKurucu.ZeminKur ve AtolyeUretici.SahneKur çalışırken kurar.
    /// </summary>
    [Serializable]
    public class SahneTasarimi
    {
        [Serializable]
        public struct Zemin
        {
            public int x, y;         // hücre (0…n−1; y = 0 ön)
            public string t;         // cim · yol · kaldirim · meydan · patika · su · tarh · cit
            public int k;            // yükselti kademesi (0, 1, 2)
        }

        [Serializable]
        public struct Bina
        {
            public string tasarim;   // tasarım adı (Resources/Atolye/Tasarimlar/atolye_<ad>.json)
            public int x, y;         // binanın 5 × 5 arsasının orta hücresi
            public int don;          // 90°'lik dönüş sayısı (0: ön −y'ye bakar; saat yönünün tersine)
            public bool kapali;      // önizlemede kepenk inik (yalnız görsel)
        }

        public string ad = "yeni_sahne";
        public int n = 16;
        public int tohum = 1;
        public List<Zemin> zemin = new List<Zemin>();
        public List<Bina> binalar = new List<Bina>();
        public List<BinaTasarimi.Sus> susler = new List<BinaTasarimi.Sus>();

        public int ZeminBul(int x, int y)
        {
            for (int i = 0; i < zemin.Count; i++) if (zemin[i].x == x && zemin[i].y == y) return i;
            return -1;
        }

        public int Kademe(int x, int y)
        {
            int i = ZeminBul(x, y);
            return i < 0 ? 0 : Math.Max(0, Math.Min(2, zemin[i].k));
        }
    }
}
