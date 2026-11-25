using System;
using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Bina Atölyesi'nde kurulan bir binanın (ya da süslenmiş arsanın) tasarım verisi. Oyunda Resources/Atolye/Tasarimlar
    /// altındaki JSON'lardan okunur ve model oyun çalışırken kurulur (AtolyeUretici); atölye penceresi aynı sınıfı düzenler. Alan adları Tools/Blender/pastel_parcalar.py'deki tasarım JSON'uyla aynı
    /// (JsonUtility ile içe / dışa aktarılır). Eski dosyalarda olmayan alanlar varsayılanında kalır.
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
            public int ct;           // çatı tarzı: 0 tarza göre · 1 kırma · 2 beşik · 3 mansart · 4 düz teras · 5 basamaklı alınlık · 6 testere
        }

        /// <summary>Arsaya serbest konmuş dekor / ağaç (parça adı kütüphanedeki "cevre" ya da "agac" grubundan).</summary>
        [Serializable]
        public struct Sus
        {
            public string ad;
            public float x, y;       // metre; arsanın ortası 0, sokak −y
            public float aci;        // derece
            public float olcek;      // 0: 1 sayılır
            public int renk;         // yaprak / vurgu rengi sırası
        }

        public string ad = "yeni_bina";
        /// <summary>City3D'nin bölge havuzu anahtarı (önek "Bolge" olan türlerde): eski, karma, kule, ev, kampus, sanayi, avm.</summary>
        public string bolge = "eski";
        public int tohum = 1;
        /// <summary>Eski dosyalar: tür boşken true ise "dukkan" türü sayılır.</summary>
        public bool dukkan;
        /// <summary>Kullanım türü (kütüphanedeki tür tablosundan: burger, kafe, belediye, depo ...). Boş: konut.</summary>
        public string tur = "";
        /// <summary>Tabela yazısı (boşsa türün varsayılanı). Büyük harf, rakam ve &amp; ' - / . çizilir.</summary>
        public string tabela = "";
        /// <summary>Tente rengi sırası.</summary>
        public int tente;
        public List<Blok> bloklar = new List<Blok>();
        public List<Sus> susler = new List<Sus>();
    }
}
