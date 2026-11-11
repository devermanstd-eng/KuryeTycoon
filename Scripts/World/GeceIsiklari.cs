using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Gece ışıkları: Blender modellerinin kendi parçaları geceleri yanar (ekrana çizilen noktalar değil).
    ///  • Her ışık veren malzemenin bir "gece hâli" vardır (ışık saçan kopyası). Gece olunca o parça gece hâline geçer;
    ///    malzeme ayarına (prefab aracı) bağlı değildir, her bilgisayarda aynı çalışır.
    ///  • Pencereler: her binada camlar üç grupta (Cam_, Cam2_, Cam3_). Her grup kendi saatinde yanar ve söner:
    ///    akşam erken yananlar, gece yarısına kadar, bütün gece; sabaha karşı erkenciler. Arada biri kısa süre söndürüp yakar,
    ///    bazı pencerelerde titreyen mavi televizyon ışığı. Şehir yaşıyor gibi.
    ///  • Vitrinler (Vitrin_) dükkân kapanana kadar ışıklı; tabela yazıları (Yazi_) parlar.
    ///  • Ofis camları mesai bitene kadar yanar, birkaçında temizlikçiler gece de çalışır.
    ///  • Sokak ve park lambaları hava karardıkça sırayla yanar; kameranın baktığı yerdeki lambalara gerçek ışık kaynağı
    ///    konur (zemini, kaldırımı ve cepheleri aydınlatır). Birkaç arızalı lamba titrer.
    /// Sadece 3D ışık (Gorsel3D) açıkken çalışır.
    /// </summary>
    public class GeceIsiklari : MonoBehaviour
    {
        public static GeceIsiklari I { get; private set; }

        /// <summary>Gece ışıkları modellerin üstünde çiziliyor mu (DayNight ekran lambalarını çizmesin).</summary>
        public static bool Showing => I != null && I.active && I.entries.Count > 0;

        enum Kind { Window, Shop, Sign, Office, Lamp, Pool }

        class Entry
        {
            public Renderer r;
            public int index;          // malzeme sırası (alt mesh)
            public Kind kind;
            public Material day;       // gündüz malzemesi
            public uint seed;
            public bool locked;
            public int state = -1;     // son uygulanan: -1 bilinmiyor, 0 gündüz, 1+ gece varyantı
            public float onAt, offAt, morningOn, morningOff, threshold;
            public int variant;        // gece hâli: sıcak, sıcak2, TV (3 faz), ofis, vitrin, tabela, lamba, arızalı lamba
        }

        readonly List<Entry> entries = new List<Entry>();
        readonly List<Vector3> lampPoints = new List<Vector3>();
        readonly List<float> lampThresholds = new List<float>();
        readonly Dictionary<(Material, int), Material> nightMats = new Dictionary<(Material, int), Material>();
        readonly List<(Material m, Color c, int phase)> animated = new List<(Material, Color, int)>();
        readonly List<Light> lights = new List<Light>();
        readonly List<int> nearest = new List<int>();
        MaterialPropertyBlock mpb;
        bool active;
        int tintVersion = -1;
        float timer, lightTimer, collectTimer;
        Transform collectedRoot;

        const int MaxLights = 72;            // aynı anda gerçek ışık alan lamba sayısı (kameranın baktığı yer)

        static readonly Color Warm = new Color(1.00f, 0.72f, 0.34f);
        static readonly Color Warm2 = new Color(1.00f, 0.82f, 0.50f);
        static readonly Color Tv = new Color(0.45f, 0.62f, 1.00f);
        static readonly Color OfficeC = new Color(0.86f, 0.93f, 1.00f);
        static readonly Color ShopC = new Color(1.00f, 0.92f, 0.75f);
        static readonly Color LampC = new Color(1.00f, 0.68f, 0.36f);   // turuncu sodyum lamba (referanstaki gibi)

        const int VWarm = 1, VWarm2 = 2, VTv0 = 3, VTv1 = 4, VTv2 = 5, VOffice = 6, VShop = 7, VSign = 8, VLamp = 9, VLampBroken = 10;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("Gece ışıkları");
            DontDestroyOnLoad(go);
            go.AddComponent<GeceIsiklari>();
        }

        void Awake()
        {
            I = this;
            mpb = new MaterialPropertyBlock();
            City3D.Built += Collect;
            for (int i = 0; i < MaxLights; i++)
            {
                var go = new GameObject("Lamba ışığı");
                go.transform.SetParent(transform, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = LampC;
                l.range = 0.75f;           // ≈ 7,5 m: lambanın altında küçük sıcak havuz (çimleri boyamasın)
                l.intensity = 0f;
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
                go.SetActive(false);
                lights.Add(l);
            }
        }

        void OnDestroy()
        {
            City3D.Built -= Collect;
            if (I == this) I = null;
            foreach (var m in nightMats.Values) if (m != null) Destroy(m);
        }

        // ---------------------------------------------------------------- kayıt

        static bool IsWindow(string n) => n.StartsWith("Cam_") || n.StartsWith("Cam2_") || n.StartsWith("Cam3_");
        static bool IsOffice(string n) => n.StartsWith("CamKoyu") || n.StartsWith("OfisCam");
        static bool IsShop(string n) => n.StartsWith("Vitrin_");
        static bool IsSign(string n) => n.StartsWith("Yazi_");
        static bool IsStreetLamp(string n) => n.StartsWith("YIsik") || n.StartsWith("PIsik");
        static bool IsLamp(string n) => !n.StartsWith("IsikHavuz") && (IsStreetLamp(n) || n.StartsWith("Isik_") || n.StartsWith("TSari_isik"));

        static uint Hash(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return h;
            }
        }

        static float R01(uint h, int k) => (Hash((int)h, k, 977) & 0xFFFF) / 65535f;

        void Collect()
        {
            // Önceki şehrin parçalarını gündüz hâline döndür (yeniden kurulumda eski malzemeler takılı kalmasın)
            entries.Clear();
            lampPoints.Clear();
            lampThresholds.Clear();
            var city = City3D.I;
            if (city == null || city.Root == null) return;
            collectedRoot = city.Root;
            var map = CityMap.I;
            var seen = new HashSet<Vector2Int>();
            foreach (var r in city.Root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                var p = r.transform.position;
                int px = Mathf.RoundToInt(p.x * 4f), py = Mathf.RoundToInt(p.y * 4f);
                var tile = new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
                bool locked = map != null && map.InBounds(tile) && !map.IsUnlocked(tile);
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    string n = m.name;
                    Kind kind;
                    int group = 0;
                    if (IsWindow(n)) { kind = Kind.Window; group = n.StartsWith("Cam2_") ? 1 : n.StartsWith("Cam3_") ? 2 : 0; }
                    else if (IsShop(n)) kind = Kind.Shop;
                    else if (IsSign(n)) kind = Kind.Sign;
                    else if (IsOffice(n)) kind = Kind.Office;
                    else if (n.StartsWith("IsikHavuz")) kind = Kind.Pool;
                    else if (IsLamp(n)) kind = Kind.Lamp;
                    else continue;
                    var e = new Entry { r = r, index = i, kind = kind, locked = locked, day = m, seed = Hash(px, py, group * 7 + (int)kind) };
                    Plan(e);
                    entries.Add(e);
                    // Sokak ve park lambalarının başları: gerçek ışık kaynağının yerleri
                    if (kind == Kind.Lamp && IsStreetLamp(n)) CollectLampPoints(r, i, seen, e.threshold);
                }
            }
            Debug.Log($"Kurye Tycoon · gece ışıkları: {entries.Count} pencere/vitrin/lamba parçası, {lampPoints.Count} sokak lambası");
        }

        /// <summary>Lamba başı malzemesinin üçgenlerini kümeleyip her lambanın dünyadaki yerini bulur.</summary>
        void CollectLampPoints(Renderer r, int sub, HashSet<Vector2Int> seen, float threshold)
        {
            var mf = r.GetComponent<MeshFilter>();
            var mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null || !mesh.isReadable || sub >= mesh.subMeshCount) return;
            var verts = mesh.vertices;
            var tris = mesh.GetTriangles(sub);
            var clusters = new Dictionary<Vector2Int, (Vector3 sum, int n)>();
            var l2w = r.transform.localToWorldMatrix;
            foreach (int ti in tris)
            {
                var w = l2w.MultiplyPoint3x4(verts[ti]);
                var key = new Vector2Int(Mathf.RoundToInt(w.x * 6f), Mathf.RoundToInt(w.y * 6f));   // ≈ 1,7 m'lik kümeler
                clusters.TryGetValue(key, out var c);
                clusters[key] = (c.sum + w, c.n + 1);
            }
            foreach (var kv in clusters)
            {
                var pos = kv.Value.sum / kv.Value.n;
                var k = new Vector2Int(Mathf.RoundToInt(pos.x * 4f), Mathf.RoundToInt(pos.y * 4f));   // yakın/uzak modelin aynı lambası bir kez
                if (!seen.Add(k)) continue;
                lampPoints.Add(pos + new Vector3(0f, 0f, 0.04f));   // başın hemen altı (dünyada aşağı +Z)
                lampThresholds.Add(threshold + R01((uint)(k.x * 31 + k.y), 3) * 0.08f);
            }
        }

        /// <summary>Her parçanın kendi saatleri (tohumlu: her oyunda aynı bina aynı alışkanlıkta).</summary>
        static void Plan(Entry e)
        {
            uint s = e.seed;
            switch (e.kind)
            {
                case Kind.Window:
                    // Akşam şehir pırıl pırıl: pencerelerin çoğu gün batımı civarında yanar, gece ilerledikçe seyrelir
                    e.onAt = -0.8f + R01(s, 1) * 1.6f;                 // gün batımına göre (saat)
                    float type = R01(s, 2);
                    if (type < 0.20f) e.offAt = 99f;                   // bütün gece (gece kuşu, nöbetçi)
                    else if (type < 0.32f) e.offAt = 21.5f + R01(s, 3) * 1.5f;   // erken yatan
                    else e.offAt = 23.0f + R01(s, 4) * 3.0f;           // 23:00–02:00
                    e.morningOn = R01(s, 5) < 0.35f ? -1.6f + R01(s, 6) * 1.0f : 99f;   // gün doğumuna göre: erkenciler
                    e.morningOff = 0.2f + R01(s, 7) * 0.8f;
                    float v = R01(s, 8);
                    e.variant = v < 0.06f ? VTv0 + (int)(s % 3u) : v < 0.6f ? VWarm : VWarm2;
                    break;
                case Kind.Office:
                    e.onAt = -1.0f + R01(s, 1) * 0.8f;
                    e.offAt = R01(s, 2) < 0.2f ? 99f : 19.0f + R01(s, 3) * 3f;
                    e.morningOn = -0.8f; e.morningOff = 1.0f;
                    e.variant = VOffice;
                    break;
                case Kind.Shop:
                    e.onAt = -0.8f;
                    e.offAt = R01(s, 2) < 0.25f ? 99f : 21.0f + R01(s, 3) * 2.5f;     // bazıları gece de açık (non-stop)
                    e.morningOn = -1.5f; e.morningOff = 0.8f;
                    e.variant = VShop;
                    break;
                case Kind.Sign:
                    e.onAt = -0.5f; e.offAt = 99f;
                    e.morningOn = 99f; e.morningOff = 0f;
                    e.variant = VSign;
                    break;
                default:
                    // Lambalar: hava karardıkça sırayla (eşik 0,08–0,35 gece oranı)
                    e.threshold = 0.08f + R01(s, 1) * 0.27f;
                    e.variant = R01(s, 2) < 0.03f ? VLampBroken : VLamp;
                    break;
            }
        }

        // ---------------------------------------------------------------- gece hâli malzemeleri

        Material Night(Material day, int variant, bool locked)
        {
            int key = variant + (locked ? 100 : 0);
            if (nightMats.TryGetValue((day, key), out var m) && m != null) return m;
            m = new Material(day) { name = day.name + " (gece)" };
            Color c; float k;
            switch (variant)
            {
                case VWarm: c = Warm; k = 2.2f; break;
                case VWarm2: c = Warm2; k = 1.9f; break;
                case VTv0: case VTv1: case VTv2: c = Tv; k = 1.0f; break;
                case VOffice: c = OfficeC; k = 1.2f; break;
                case VShop: c = ShopC; k = 2.0f; break;
                case VSign:
                {
                    var baseC = day.HasProperty("_BaseColor") ? day.GetColor("_BaseColor") : day.color;
                    c = Color.Lerp(baseC, Color.white, 0.2f); k = 1.8f; break;
                }
                default: c = new Color(1f, 0.85f, 0.6f); k = 3.0f; break;
            }
            if (locked) k *= 0.4f;
            if (variant != VSign && m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", Color.Lerp(m.GetColor("_BaseColor"), c, 0.85f));
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", c * k);
            nightMats[(day, key)] = m;
            if (variant >= VTv0 && variant <= VTv2) animated.Add((m, c * k, variant - VTv0));
            if (variant == VLampBroken) animated.Add((m, c * k, 10));
            return m;
        }

        // ---------------------------------------------------------------- her kare

        void LateUpdate()
        {
            collectTimer -= Time.unscaledDeltaTime;
            if (collectTimer <= 0f)
            {
                collectTimer = 1.5f;
                var city = City3D.I;
                if (city != null && city.Root != null && city.Root != collectedRoot && city.Root.childCount > 0) Collect();
            }

            bool on = Gorsel3D.Active && DayNight.Enabled && GameClock.I != null && MapCamera.I != null &&
                      MapCamera.I.ShowModels && entries.Count > 0;
            if (!on)
            {
                if (active) { active = false; ApplyAll(true); HideLights(); }
                return;
            }
            active = true;
            // Şehir yeniden boyandıysa (bölge açıldı, sözleşme değişti) camların rengi sıfırlandı: hepsini yeniden uygula
            if (tintVersion != City3D.TintVersion) { tintVersion = City3D.TintVersion; foreach (var e in entries) e.state = -1; timer = 0f; }

            // Televizyonlar ve arızalı lambalar titrer (ortak malzemeler: tek tek boyamaya gerek yok)
            float t = Time.unscaledTime;
            foreach (var a in animated)
            {
                if (a.m == null) continue;
                float f = a.phase == 10
                    ? (Mathf.PerlinNoise(t * 6f, 0.7f) < 0.35f ? 0.05f : 1f)
                    : 0.55f + 0.45f * Mathf.PerlinNoise(t * 6f + a.phase * 13.1f, 0.3f);
                a.m.SetColor("_EmissionColor", a.c * f);
            }

            timer -= Time.unscaledDeltaTime;
            if (timer <= 0f) { timer = 0.25f; ApplyAll(false); }

            lightTimer -= Time.unscaledDeltaTime;
            if (lightTimer <= 0f) { lightTimer = 0.4f; UpdateLights(); }
        }

        void ApplyAll(bool allOff)
        {
            float hour = DayNight.HourF;
            float night = DayNight.Night01;
            float rise = DayNight.Sunrise, set = DayNight.Sunset;
            float minutes = GameClock.I != null ? (float)GameClock.I.TotalMinutes : 0f;
            foreach (var e in entries)
            {
                if (e.r == null) continue;
                if (e.kind == Kind.Pool)
                {
                    // Eski sert ışık diskleri (önceki modellerde): her zaman görünmez
                    if (e.state != 0) { e.state = 0; HidePool(e); }
                    continue;
                }
                bool lit = !allOff && IsLit(e, hour, night, rise, set, minutes);
                int st = lit ? e.variant : 0;
                if (st == e.state) continue;
                e.state = st;
                var mats = e.r.sharedMaterials;
                if (e.index >= mats.Length) continue;
                var nm = lit ? Night(e.day, e.variant, e.locked) : e.day;
                mats[e.index] = nm;
                e.r.sharedMaterials = mats;
                // Bina renk çeşitliliği (City3D) camın rengini property block ile sabitliyor: yanınca sıcak renge geçir
                if (e.kind != Kind.Lamp && e.kind != Kind.Sign && !e.locked)
                {
                    e.r.GetPropertyBlock(mpb, e.index);
                    var bc = nm.HasProperty("_BaseColor") ? nm.GetColor("_BaseColor") : nm.color;
                    mpb.SetColor("_BaseColor", bc);
                    mpb.SetColor("_Color", bc);
                    e.r.SetPropertyBlock(mpb, e.index);
                }
            }
        }

        void HidePool(Entry e)
        {
            e.r.GetPropertyBlock(mpb, e.index);
            mpb.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));
            mpb.SetColor("_Color", new Color(0f, 0f, 0f, 0f));
            e.r.SetPropertyBlock(mpb, e.index);
        }

        static bool IsLit(Entry e, float hour, float night, float rise, float set, float minutes)
        {
            if (e.kind == Kind.Lamp) return night > e.threshold;
            if (night < 0.05f) return false;
            // Saatleri gün batımı / doğumuna göre karşılaştır (gece yarısını geçen saatler için 24 ekle)
            float h = hour < 12f ? hour + 24f : hour;
            float evening = set + e.onAt;
            float off = e.offAt < 12f ? e.offAt + 24f : e.offAt;
            bool lit = h >= evening && h < off;
            if (hour < 12f && hour >= rise + e.morningOn && hour < rise + e.morningOff) lit = true;
            if (e.kind == Kind.Window)
            {
                if (e.locked && (e.seed & 1) == 0) lit = false;          // kilitli bölge daha sönük
                // Arada biri ışığı söndürüp yakar (mutfağa gitti, yattı, kalktı): her pencerenin kendi aralığı
                float period = 25f + (e.seed % 40u);
                uint slot = (uint)Mathf.FloorToInt(minutes / period);
                if (R01(e.seed ^ slot, 9) < 0.08f) lit = !lit;
            }
            return lit;
        }

        // ---------------------------------------------------------------- gerçek lamba ışıkları

        void UpdateLights()
        {
            var cam = Camera.main;
            float night = DayNight.Night01;
            if (cam == null || lampPoints.Count == 0 || night < 0.08f) { HideLights(); return; }
            var focus = new Vector2(CityMap.Width / 2f, CityMap.Height / 2f);
            if (MapCamera.I.GroundPoint(cam, new Vector2(Screen.width * 0.5f, Screen.height * 0.45f), out var f)) focus = f;
            // Kameranın baktığı yere en yakın, yanmış lambalar
            nearest.Clear();
            for (int i = 0; i < lampPoints.Count; i++)
                if (night > lampThresholds[i]) nearest.Add(i);
            nearest.Sort((a, b) =>
                ((Vector2)lampPoints[a] - focus).sqrMagnitude.CompareTo(((Vector2)lampPoints[b] - focus).sqrMagnitude));
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.6f, night));
            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i];
                bool use = i < nearest.Count;
                if (l.gameObject.activeSelf != use) l.gameObject.SetActive(use);
                if (!use) continue;
                l.transform.position = lampPoints[nearest[i]];
                l.intensity = 0.32f * k;   // lamba başı yerden ≈0,42 birim yukarıda: altı sıcak bir ışık havuzu
            }
        }

        void HideLights()
        {
            foreach (var l in lights) if (l.gameObject.activeSelf) l.gameObject.SetActive(false);
        }
    }
}
