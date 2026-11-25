using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Haritada tıklanan dükkânın, binanın ya da kuryenin altında nabız gibi hafifçe genişleyen seçim halkası
    /// (Resources/Gorsel/SecimHalkasi.shader). Eskiden binanın üstüne IMGUI ile küçük bir nokta çiziliyordu (seçili bina işareti).
    ///  • GameUI her karede seçili olanı bildirir (Goster) ya da gizler; kurye yürüdükçe halka onunla gider.
    ///  • Renkler DioramaAyari'nda (halkaRengi, halkaMurekkep): arayüzün sıcak çizim temasıyla aynı dil.
    /// </summary>
    public static class SecimHalkasi
    {
        static GameObject go;
        static MeshRenderer mr;
        static Material mat;
        static bool arandi;
        static readonly int RenkId = Shader.PropertyToID("_Renk");
        static readonly int MurekkepId = Shader.PropertyToID("_Murekkep");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Sifirla()
        {
            go = null; mr = null; mat = null; arandi = false;
        }

        /// <summary>Halkayı dünyadaki bu noktanın (x, y harita karesi) altında, bu yarıçapla (dünya birimi) gösterir.</summary>
        public static void Goster(Vector2 merkez, float yaricap)
        {
            if (!Hazirla()) return;
            float z = CityMap.Organik ? Arazi.Z(merkez.x, merkez.y) : 0f;
            go.transform.position = new Vector3(merkez.x, merkez.y, z - 0.006f);   // yukarı −Z
            go.transform.localScale = new Vector3(yaricap * 2f, yaricap * 2f, 1f);
            var a = DioramaAyari.Gecerli;
            mat.SetColor(RenkId, a.halkaRengi);
            mat.SetColor(MurekkepId, a.halkaMurekkep);
            if (!mr.enabled) mr.enabled = true;
        }

        public static void Gizle()
        {
            if (mr != null && mr.enabled) mr.enabled = false;
        }

        static bool Hazirla()
        {
            if (go != null) return true;
            if (arandi) return false;
            arandi = true;
            var sh = Shader.Find("KuryeTycoon/SecimHalkasi");
            if (sh == null)
            {
                Debug.LogWarning("Kurye Tycoon: SecimHalkasi gölgelendiricisi bulunamadı (Resources/Gorsel/SecimHalkasi.shader).");
                return false;
            }
            mat = new Material(sh) { name = "Seçim halkası" };
            var mesh = new Mesh { name = "Seçim halkası" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.RecalculateBounds();
            go = new GameObject("Seçim halkası");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.enabled = false;
            return true;
        }
    }
}
