using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Gece far huzmeleri: hava kararınca yoldaki kuryelerin (bisiklet lambası, scooter, motosiklet, araba) ve trafikteki araba ile
    /// otobüslerin önündeki zemine yumuşak, sıcak bir ışık düşer (Resources/Gorsel/YerIsigi.shader).
    ///  • Gerçek ışık kaynağı değil: zemine yatırılmış toplamalı dörtgen. Yüzlerce araç için ucuz, Forward'da ışık sınırına takılmaz.
    ///  • Yalnız 3D görünümde ve yakından (karakterler görünürken), yalnız yolda giden kuryeler (dükkanda / müşteride duran söner).
    ///  • Arabalı kuryenin aracında zaten gerçek far ışığı var (AracAnimasyon): onun huzmesi yarı güçte, iki ışık üst üste parlamasın.
    ///  • Güç ve renk DioramaAyari'nda (farGucu, farRengi).
    /// </summary>
    public class FarHuzmeleri : MonoBehaviour
    {
        public static FarHuzmeleri I { get; private set; }

        class Iz
        {
            public Vector2 son;
            public Vector2 yon = Vector2.right;
            public int arac = -1;          // AracAnimasyon var mı (−1 bakılmadı, 0 yok, 1 var)
        }

        readonly List<MeshRenderer> havuz = new List<MeshRenderer>();
        readonly Dictionary<Courier, Iz> izler = new Dictionary<Courier, Iz>();
        readonly List<Courier> silinecek = new List<Courier>();
        Mesh dortgen;
        Material tam, yarim;
        int kullanilan;
        static readonly int GucId = Shader.PropertyToID("_Guc");
        static readonly int RenkId = Shader.PropertyToID("_Renk");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("Far huzmeleri");
            DontDestroyOnLoad(go);
            go.AddComponent<FarHuzmeleri>();
        }

        void Awake()
        {
            I = this;
            var sh = Shader.Find("KuryeTycoon/YerIsigi");
            if (sh == null)
            {
                Debug.LogWarning("Kurye Tycoon: YerIsigi gölgelendiricisi bulunamadı (Resources/Gorsel/YerIsigi.shader); far huzmeleri kapalı.");
                enabled = false;
                return;
            }
            tam = new Material(sh) { name = "Far huzmesi", enableInstancing = true };
            yarim = new Material(sh) { name = "Far huzmesi (yarım)", enableInstancing = true };
            // Birim dörtgen: x boyuna 0..1 (far → uç), y enine −0,5..0,5; uv.x boyuna, uv.y enine
            dortgen = new Mesh { name = "Far huzmesi" };
            dortgen.SetVertices(new List<Vector3> { new Vector3(0f, -0.5f, 0f), new Vector3(1f, -0.5f, 0f), new Vector3(1f, 0.5f, 0f), new Vector3(0f, 0.5f, 0f) });
            dortgen.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
            dortgen.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            dortgen.SetNormals(new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            dortgen.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            if (tam != null) Destroy(tam);
            if (yarim != null) Destroy(yarim);
            if (dortgen != null) Destroy(dortgen);
        }

        void LateUpdate()
        {
            kullanilan = 0;
            var a = DioramaAyari.Gecerli;
            float gece = DayNight.Enabled && GameClock.I != null ? DayNight.Night01 : 0f;
            float guc = Mathf.InverseLerp(0.2f, 0.7f, gece) * a.farGucu;
            var mc = MapCamera.I;
            bool goster = guc > 0.01f && Gorsel3D.Active && mc != null && mc.ShowModels && !mc.FlatMode &&
                          GameBootstrap.I != null && GameBootstrap.I.Started;
            if (goster)
            {
                var renk = a.farRengi;
                tam.SetColor(RenkId, renk);
                yarim.SetColor(RenkId, renk);
                tam.SetFloat(GucId, guc);
                yarim.SetFloat(GucId, guc * 0.5f);
                Kuryeler();
                Trafik();
            }
            for (int i = kullanilan; i < havuz.Count; i++)
                if (havuz[i].enabled) havuz[i].enabled = false;
        }

        void Kuryeler()
        {
            var cm = CourierManager.I;
            if (cm == null) return;
            foreach (var c in cm.Couriers)
            {
                if (c == null) continue;
                if (!izler.TryGetValue(c, out var iz)) { iz = new Iz { son = c.Position }; izler[c] = iz; }
                var d = c.Position - iz.son;
                iz.son = c.Position;
                if (d.sqrMagnitude > 1e-8f) iz.yon = d.normalized;
                var model = c.Model;
                bool yolda = c.State == CourierState.DukkanaGidiyor || c.State == CourierState.MusteriyeGidiyor;
                if (model == null || !model.activeInHierarchy || !yolda) continue;
                if (iz.arac < 0) iz.arac = model.GetComponentInChildren<AracAnimasyon>() != null ? 1 : 0;
                Olcu(c.vehicle, out float boy, out float en, out float on);
                Ciz(CityMap.ToWorld(c.Position), iz.yon, boy, en, on, iz.arac == 1);
            }
            // Ayrılan kuryelerin izi silinir
            if (izler.Count > cm.Couriers.Count + 8)
            {
                silinecek.Clear();
                foreach (var k in izler.Keys) if (k == null || !cm.Couriers.Contains(k)) silinecek.Add(k);
                foreach (var k in silinecek) izler.Remove(k);
            }
        }

        static void Olcu(VehicleKind v, out float boy, out float en, out float on)
        {
            switch (v)
            {
                case VehicleKind.Araba: boy = 0.62f; en = 0.30f; on = 0.16f; break;
                case VehicleKind.Motosiklet: boy = 0.44f; en = 0.17f; on = 0.08f; break;
                case VehicleKind.EScooter: boy = 0.28f; en = 0.10f; on = 0.05f; break;
                default: boy = 0.30f; en = 0.11f; on = 0.06f; break;   // bisiklet lambası
            }
        }

        void Trafik()
        {
            var tr = Traffic.I;
            if (tr == null || !Traffic.Enabled || tr.Kok == null) return;
            foreach (Transform t in tr.Kok)
            {
                if (!t.gameObject.activeInHierarchy) continue;
                bool otobus = t.name == "Otobüs";
                if (!otobus && t.name != "Araba") continue;
                var ileri = t.TransformDirection(Vector3.right);
                var yon = new Vector2(ileri.x, ileri.y);
                if (yon.sqrMagnitude < 1e-6f) continue;
                Ciz(t.position, yon.normalized, otobus ? 0.8f : 0.62f, otobus ? 0.36f : 0.30f, otobus ? 0.32f : 0.18f, false);
            }
        }

        /// <summary>Aracın önüne (on: aracın merkezinden farlara uzaklık) huzmeyi yerleştirir.</summary>
        void Ciz(Vector3 merkez, Vector2 yon, float boy, float en, float on, bool yarimGuc)
        {
            var r = Al();
            float z = CityMap.Organik ? Arazi.Z(merkez.x, merkez.y) : 0f;
            var t = r.transform;
            t.position = new Vector3(merkez.x + yon.x * on, merkez.y + yon.y * on, z - 0.004f);   // yukarı −Z
            t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(yon.y, yon.x) * Mathf.Rad2Deg);
            t.localScale = new Vector3(boy, en, 1f);
            var m = yarimGuc ? yarim : tam;
            if (r.sharedMaterial != m) r.sharedMaterial = m;
            if (!r.enabled) r.enabled = true;
        }

        MeshRenderer Al()
        {
            if (kullanilan < havuz.Count) return havuz[kullanilan++];
            var go = new GameObject("Huzme");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = dortgen;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = tam;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            havuz.Add(r);
            kullanilan++;
            return r;
        }
    }
}
