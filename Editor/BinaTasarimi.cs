using System;
using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// Bina Atölyesi'nde kurulan bir binanın tasarım verisi (blok listesi): prefab kaydedilirken yanına yazılır, sonradan
    /// açılıp düzenlenebilir. Alan adları Tools/Blender/pastel_parcalar.py'deki örnek tasarım JSON'uyla aynı
    /// (JsonUtility ile içe / dışa aktarılır).
    /// </summary>
    public class BinaTasarimi : ScriptableObject
    {
        [Serializable]
        public struct Blok
        {
            public int x, y, k;      // hücre (0…4; y = 0 sokak tarafı) ve kat
            public int tarz;         // 0 pastel apartman · 1 yarı ahşap ev · 2 taş kule · 3 taş kaide · 4 taş merdiven
            public int renk;         // duvar rengi sırası
            public int cati;         // çatı rengi sırası
        }

        public string ad = "yeni_bina";
        /// <summary>City3D'nin bölge havuzu anahtarı: eski, karma, kule, ev, kampus, sanayi, avm.</summary>
        public string bolge = "eski";
        public int tohum = 1;
        /// <summary>Zemin katın sokağa bakan yüzü dükkân (vitrin, tenteli) olsun.</summary>
        public bool dukkan;
        public List<Blok> bloklar = new List<Blok>();
    }
}
