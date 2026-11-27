using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KuryeTycoon
{
    /// <summary>
    /// Bina Atölyesi parçalarının çok sayıda kopyasını (sokak donatıları, kıyı sazlıkları) malzeme başına tek mesh'te
    /// birleştirir. Mesh.CombineMeshes yerine kullanılır, çünkü o yalnız konum ve normali taşır:
    ///  • kalem çizgisi şeritlerinin öbür ucu (TEXCOORD1) taşınmıyor, birleşik donatılarda çizgiler yamuluyordu;
    ///  • çevre animasyonunun sabitleri (TEXCOORD0.zw: taban yüksekliği, bayrak çapası, dönme ekseni) taşınmalı.
    /// Birleşik mesh "metre, Y yukarı" uzayında kurulur (atölye model uzayı; CevreAnimasyon.hlsl bu uzayı bekler); onu
    /// taşıyan nesne dünyaya −90° X dönüşü ve ölçekle oturur (dünyada yukarı −Z, kare = 10 m).
    /// </summary>
    public static class AtolyeBirlestirici
    {
        /// <summary>Bir kopya: mesh'in bir alt-mesh'i, üst nesnenin uzayındaki dönüşümüyle. faz: kopyaya özgü rüzgâr fazı (radyan).</summary>
        public struct Ornek
        {
            public Mesh mesh;
            public int sub;
            public Matrix4x4 m;
            public float faz;
        }

        class Kaynak
        {
            public Vector3[] v, n;
            public Color32[] c;
            public List<Vector4> uv0 = new List<Vector4>();
            public List<Vector3> uv1 = new List<Vector3>();
            public readonly Dictionary<int, int[]> uc = new Dictionary<int, int[]>();
            public int[] esle;       // kaynak köşe → birleşik köşe (her kopyada sıfırlanır)
            public int[] damga;
        }

        static readonly Dictionary<Mesh, Kaynak> kaynaklar = new Dictionary<Mesh, Kaynak>();
        static int damgaNo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Sifirla() { kaynaklar.Clear(); damgaNo = 0; }

        /// <summary>Birleştirme bitince kaynak dizilerinin kopyalarını bırakır (bellek).</summary>
        public static void Bosalt() { kaynaklar.Clear(); }

        static Kaynak Al(Mesh mesh)
        {
            if (kaynaklar.TryGetValue(mesh, out var k)) return k;
            k = new Kaynak { v = mesh.vertices, n = mesh.normals, c = mesh.colors32 };
            mesh.GetUVs(0, k.uv0);
            mesh.GetUVs(1, k.uv1);
            k.esle = new int[k.v.Length];
            k.damga = new int[k.v.Length];
            kaynaklar[mesh] = k;
            return k;
        }

        /// <summary>
        /// Kopyaları birleştirip <paramref name="ust"/> altında nesne(ler) kurar. Kopya dönüşümleri üst nesnenin uzayında
        /// verilir (dünyada yukarı −Z); olcek: metre → dünya birimi. Çok büyük listeler birkaç mesh'e bölünür.
        /// </summary>
        public static List<GameObject> Kur(Transform ust, string ad, Material malzeme, List<Ornek> liste, float olcek, bool golgeDusur)
        {
            var sonuc = new List<GameObject>();
            if (liste == null || liste.Count == 0 || malzeme == null) return sonuc;
            bool cizgi = malzeme.name.StartsWith("Pastel_Cizgi");
            // taşıyıcının yerel dönüşümü ve tersi: kopyalar taşıyıcının (metre, Y yukarı) uzayına çevrilir
            var tasiyici = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(-90f, 0f, 0f), Vector3.one * olcek);
            var ters = tasiyici.inverse;

            var V = new List<Vector3>();
            var N = new List<Vector3>();
            var C = new List<Color32>();
            var U0 = new List<Vector4>();
            var U1 = new List<Vector3>();
            var T = new List<int>();
            var beyaz = new Color32(255, 255, 255, 255);
            const int Sinir = 300000;

            void Bitir()
            {
                if (T.Count == 0) return;
                var mesh = new Mesh { name = "Atolye_" + ad, indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(V);
                mesh.SetNormals(N);
                mesh.SetColors(C);
                mesh.SetUVs(0, U0);
                mesh.SetUVs(1, U1);
                mesh.SetTriangles(T, 0);
                mesh.RecalculateBounds();
                var go = new GameObject(ad);
                go.transform.SetParent(ust, false);
                go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                go.transform.localScale = Vector3.one * olcek;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = malzeme;
                mr.shadowCastingMode = golgeDusur && !cizgi ? ShadowCastingMode.On : ShadowCastingMode.Off;
                mr.receiveShadows = true;
                sonuc.Add(go);
                V.Clear(); N.Clear(); C.Clear(); U0.Clear(); U1.Clear(); T.Clear();
            }

            foreach (var o in liste)
            {
                if (o.mesh == null || !o.mesh.isReadable || o.sub >= o.mesh.subMeshCount) continue;
                var k = Al(o.mesh);
                if (!k.uc.TryGetValue(o.sub, out var uc)) k.uc[o.sub] = uc = o.mesh.GetTriangles(o.sub);
                if (uc.Length == 0) continue;
                if (V.Count + uc.Length > Sinir) Bitir();
                var M = ters * o.m;
                int damga = ++damgaNo;
                bool nVar = k.n != null && k.n.Length == k.v.Length;
                bool cVar = k.c != null && k.c.Length == k.v.Length;
                bool u0Var = k.uv0.Count == k.v.Length;
                bool u1Var = k.uv1.Count == k.v.Length;
                for (int i = 0; i < uc.Length; i++)
                {
                    int s = uc[i];
                    if (k.damga[s] != damga)
                    {
                        k.damga[s] = damga;
                        k.esle[s] = V.Count;
                        Vector3 p = M.MultiplyPoint3x4(k.v[s]);
                        V.Add(p);
                        N.Add(nVar ? M.MultiplyVector(k.n[s]).normalized : Vector3.up);
                        Color32 c = cVar ? k.c[s] : beyaz;
                        C.Add(c);
                        Vector4 u = u0Var ? k.uv0[s] : Vector4.zero;
                        // animasyon sabitleri kopyanın yerine taşınır (AtolyeKurucu.Topla ile aynı anlam)
                        switch (AtolyeKurucu.AlfadanMod(c.a))
                        {
                            case AtolyeKurucu.AnimSalin: case AtolyeKurucu.AnimHisir: case AtolyeKurucu.AnimSaz:
                                u.z = M.MultiplyPoint3x4(new Vector3(0f, u.z, 0f)).y; u.w += o.faz; break;
                            case AtolyeKurucu.AnimSark:
                                u.z = M.MultiplyPoint3x4(new Vector3(0f, u.z, 0f)).y; break;
                            case AtolyeKurucu.AnimTitrek:
                                u.w += o.faz; break;
                            case AtolyeKurucu.AnimDalga: case AtolyeKurucu.AnimDoner:
                            {
                                Vector3 q = M.MultiplyPoint3x4(new Vector3(u.z, 0f, u.w));
                                u.z = q.x; u.w = q.z; break;
                            }
                            case AtolyeKurucu.AnimYok:
                                u.z = 0f; u.w = 0f; break;
                        }
                        U0.Add(u);
                        // kalem çizgisi: şeridin öbür ucu da taşınır; yüzeyde uv1 = konum (AtolyeKurucu.MeshYap)
                        U1.Add(cizgi && u1Var ? M.MultiplyPoint3x4(k.uv1[s]) : p);
                    }
                    T.Add(k.esle[s]);
                }
            }
            Bitir();
            return sonuc;
        }
    }
}
