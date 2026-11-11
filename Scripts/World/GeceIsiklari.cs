using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Gece ışıkları (Blender modellerinin kendi pencere ve lambaları; ekrana çizilen noktalar değil):
    ///  • Pencereler: her binada camlar üç gruba ayrılmış (Cam_, Cam2_, Cam3_). Her grup kendi saatinde yanar ve söner:
    ///    biri akşam erken yanar, biri gece yarısına kadar, biri bütün gece; sabaha karşı erkenciler yakar.
    ///    Arada biri kısa süre kapanıp açılır, bazı pencerelerde mavi televizyon ışığı titrer: şehir yaşıyor gibi.
    ///  • Ofis camları akşam mesai bitene kadar yanar, birkaçında temizlikçiler gece de çalışır.
    ///  • Sokak ve park lambaları hava karardıkça sırayla yanar (hepsi bir anda değil), altlarında ışık halkası parlar;
    ///    birkaç arızalı lamba titrer.
    /// Malzemeler prefab aracında ışık verebilir hâle getirilir (_EMISSION). Sadece 3D ışık (Gorsel3D) açıkken çalışır.
    /// </summary>
    public class GeceIsiklari : MonoBehaviour
    {
        public static GeceIsiklari I { get; private set; }

        /// <summary>Gece ışıkları modellerin üstünde çiziliyor mu (DayNight ekran lambalarını çizmesin).</summary>
        public static bool Showing => I != null && I.active && I.entries.Count > 0;

        enum Kind { Window, Office, Lamp, PoolInner, PoolOuter }

        class Entry
        {
            public Renderer r;
            public int index;          // malzeme sırası (alt mesh)
            public Kind kind;
            public Color baseColor;
            public uint seed;
            public bool locked;
            public int state = -1;     // son uygulanan durum (değişmediyse tekrar yazılmaz)
            public float onAt, offAt, morningOn, morningOff, threshold;
            public bool tv, flicker;
        }

        readonly List<Entry> entries = new List<Entry>();
        MaterialPropertyBlock mpb;   // Awake'te oluşturulur (alan başlatıcısında Unity izin vermiyor)
        bool active;
        int tintVersion = -1;
        float timer;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        static readonly Color Warm = new Color(1.00f, 0.78f, 0.42f);
        static readonly Color Warm2 = new Color(1.00f, 0.86f, 0.60f);
        static readonly Color Tv = new Color(0.55f, 0.70f, 1.00f);
        static readonly Color Office = new Color(0.90f, 0.95f, 1.00f);
        static readonly Color Lamp = new Color(1.00f, 0.82f, 0.48f);

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
        }

        void OnDestroy()
        {
            City3D.Built -= Collect;
            if (I == this) I = null;
        }

        // ---------------------------------------------------------------- kayıt

        static bool IsWindow(string n) => n.StartsWith("Cam_") || n.StartsWith("Cam2_") || n.StartsWith("Cam3_");
        static bool IsOffice(string n) => n.StartsWith("CamKoyu") || n.StartsWith("OfisCam");
        static bool IsPoolInner(string n) => n.StartsWith("IsikHavuzIc");
        static bool IsPoolOuter(string n) => n.StartsWith("IsikHavuzDis");
        static bool IsLamp(string n) => !n.StartsWith("IsikHavuz") && (n.StartsWith("YIsik") || n.StartsWith("PIsik") || n.StartsWith("Isik_") || n.StartsWith("TSari_isik"));

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
            entries.Clear();
            var city = City3D.I;
            if (city == null || city.Root == null) return;
            collectedRoot = city.Root;
            var map = CityMap.I;
            foreach (var r in city.Root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                // Konum: aynı binanın yakın ve uzak (LOD) modelleri aynı saatte yansın diye tohum konumdan
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
                    else if (IsOffice(n)) kind = Kind.Office;
                    else if (IsPoolInner(n)) kind = Kind.PoolInner;
                    else if (IsPoolOuter(n)) kind = Kind.PoolOuter;
                    else if (IsLamp(n)) kind = Kind.Lamp;
                    else continue;
                    var e = new Entry
                    {
                        r = r, index = i, kind = kind, locked = locked,
                        baseColor = m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : m.color,
                        seed = Hash(px, py, group * 7 + (int)kind)
                    };
                    Plan(e);
                    entries.Add(e);
                }
            }
            tintVersion = -1;
            Debug.Log($"Kurye Tycoon · gece ışıkları: {entries.Count} pencere/lamba parçası bulundu");
        }

        /// <summary>Her pencere grubunun ve lambanın kendi saatleri (tohumlu: her oyunda aynı bina aynı alışkanlıkta).</summary>
        static void Plan(Entry e)
        {
            uint s = e.seed;
            switch (e.kind)
            {
                case Kind.Window:
                    // Akşam: gün batımından biraz önce ile 2,5 saat sonrası arasında yanar
                    e.onAt = -0.4f + R01(s, 1) * 2.9f;                 // gün batımına göre (saat)
                    float type = R01(s, 2);
                    if (type < 0.12f) e.offAt = 99f;                   // bütün gece (gece kuşu, nöbetçi)
                    else if (type < 0.30f) e.offAt = 20.5f + R01(s, 3) * 1.5f;   // erken yatan
                    else e.offAt = 22f + R01(s, 4) * 3.5f;             // 22:00–01:30
                    e.morningOn = R01(s, 5) < 0.3f ? -1.6f + R01(s, 6) * 1.0f : 99f;   // gün doğumuna göre: erkenciler
                    e.morningOff = 0.2f + R01(s, 7) * 0.8f;
                    e.tv = R01(s, 8) < 0.1f;
                    break;
                case Kind.Office:
                    e.onAt = -1.0f + R01(s, 1) * 0.8f;
                    e.offAt = R01(s, 2) < 0.15f ? 99f : 18.5f + R01(s, 3) * 3f;
                    e.morningOn = -0.8f;
                    e.morningOff = 1.0f;
                    break;
                default:
                    // Lambalar: hava karardıkça sırayla (eşik 0,10–0,40 gece oranı)
                    e.threshold = 0.10f + R01(s, 1) * 0.30f;
                    e.flicker = R01(s, 2) < 0.03f;
                    break;
            }
        }

        // ---------------------------------------------------------------- her kare

        Transform collectedRoot;
        float collectTimer;

        void LateUpdate()
        {
            // Şehir bu sistemden önce kurulmuş olabilir (oyun açılışı): kök değişince ya da ilk kez görünce pencere ve lambaları topla
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
                // Kapalıyken de bir kez "söndür" uygulanır: ışık halkaları gündüz (ve kilitli bölgede) görünmesin
                if (active || tintVersion != City3D.TintVersion)
                {
                    active = false;
                    tintVersion = City3D.TintVersion;
                    foreach (var e in entries) e.state = -1;
                    ApplyAll(true);
                }
                return;
            }
            bool forced = !active || tintVersion != City3D.TintVersion;
            active = true;
            if (forced) { tintVersion = City3D.TintVersion; foreach (var e in entries) e.state = -1; }

            timer -= Time.unscaledDeltaTime;
            if (timer > 0f && !forced)
            {
                // Arada sadece titreyenleri (televizyon, arızalı lamba) güncelle
                foreach (var e in entries) if ((e.tv || e.flicker) && e.state > 0) Apply(e, true);
                return;
            }
            timer = 0.2f;
            ApplyAll(false);
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
                bool lit = !allOff && IsLit(e, hour, night, rise, set, minutes);
                int st = lit ? 1 : 0;
                if (st == e.state && !(lit && (e.tv || e.flicker))) continue;
                e.state = st;
                Apply(e, lit);
            }
        }

        static bool IsLit(Entry e, float hour, float night, float rise, float set, float minutes)
        {
            switch (e.kind)
            {
                case Kind.Lamp:
                case Kind.PoolInner:
                case Kind.PoolOuter:
                    return night > e.threshold;
                case Kind.Office:
                case Kind.Window:
                {
                    if (night < 0.05f) return false;
                    // Saatleri gün batımı / doğumuna göre karşılaştır (gece yarısını geçen saatler için 24 ekle)
                    float h = hour < 12f ? hour + 24f : hour;
                    float evening = set + e.onAt;
                    float off = e.offAt < 12f ? e.offAt + 24f : e.offAt;
                    bool lit = h >= evening && h < off;
                    if (hour < 12f && hour >= rise + e.morningOn && hour < rise + e.morningOff) lit = true;
                    if (e.locked && (e.seed & 1) == 0) lit = false;         // kilitli bölge daha sönük
                    if (e.kind == Kind.Window)
                    {
                        // Arada biri ışığı söndürüp yakar (mutfağa gitti, yattı, kalktı): her pencerenin kendi aralığı
                        float period = 25f + (e.seed % 40u);
                        uint slot = (uint)Mathf.FloorToInt(minutes / period);
                        if (R01(e.seed ^ slot, 9) < 0.10f) lit = !lit;
                    }
                    return lit;
                }
            }
            return false;
        }

        void Apply(Entry e, bool lit)
        {
            e.r.GetPropertyBlock(mpb, e.index);
            switch (e.kind)
            {
                case Kind.Window:
                case Kind.Office:
                {
                    if (!lit)
                    {
                        mpb.SetColor(BaseColorId, e.baseColor);
                        mpb.SetColor(ColorId, e.baseColor);
                        mpb.SetColor(EmissionId, Color.black);
                        break;
                    }
                    Color c;
                    float k;
                    if (e.kind == Kind.Office) { c = Office; k = 1.1f; }
                    else if (e.tv)
                    {
                        float t = Time.unscaledTime * 7f + e.seed % 100u;
                        c = Tv; k = 0.7f + 0.35f * Mathf.PerlinNoise(t, 0.3f);
                    }
                    else { c = (e.seed & 2) == 0 ? Warm : Warm2; k = 1.25f + R01(e.seed, 11) * 0.5f; }
                    if (e.locked) k *= 0.45f;
                    mpb.SetColor(BaseColorId, Color.Lerp(e.baseColor, c, 0.8f));
                    mpb.SetColor(ColorId, c);
                    mpb.SetColor(EmissionId, c * k);
                    break;
                }
                case Kind.Lamp:
                {
                    float k = lit ? 2.6f : 0f;
                    if (lit && e.flicker)
                    {
                        float n = Mathf.PerlinNoise(Time.unscaledTime * 6f, e.seed % 97u);
                        k *= n < 0.35f ? 0.1f : 1f;
                    }
                    if (e.locked) k *= 0.5f;
                    mpb.SetColor(BaseColorId, lit ? Lamp : e.baseColor);
                    mpb.SetColor(EmissionId, Lamp * k);
                    break;
                }
                default:
                {
                    // Işık halkası: gündüz tamamen saydam, gece yumuşak sarı
                    float a = lit ? (e.kind == Kind.PoolInner ? 0.42f : 0.22f) : 0f;
                    if (lit && e.flicker && Mathf.PerlinNoise(Time.unscaledTime * 6f, e.seed % 97u) < 0.35f) a *= 0.15f;
                    if (e.locked) a *= 0.5f;
                    var c = Lamp;
                    c.a = a;
                    mpb.SetColor(BaseColorId, c);
                    mpb.SetColor(ColorId, c);
                    break;
                }
            }
            e.r.SetPropertyBlock(mpb, e.index);
        }
    }
}
