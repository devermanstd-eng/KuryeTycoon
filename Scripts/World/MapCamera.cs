using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Stratejik yakınlaştırma (Taksimetre / Crusader Kings tarzı):
    ///  • Uzakta: düz, kuş bakışı 2D harita; kuryeler daire.
    ///  • Yakınlaştıkça kamera eğilir, binalar yerden yükselir, kuryeler 3D karakter olur.
    /// Fare tekerleği = yakınlaştır (imlecin olduğu yere), sağ/orta tuş veya sol tuşla sürükle = kaydır,
    /// ok tuşları / WASD = kaydır, + / − = yakınlaştır, 0 veya Home = tüm harita.
    /// </summary>
    public class MapCamera : MonoBehaviour
    {
        public static MapCamera I { get; private set; }

        [Header("Yakınlaştırma")]
        public float minOrthoSize = 3.2f;       // en yakın
        public float maxTilt = 50f;             // en yakında kameranın eğimi (derece)
        public float tiltStart = 0.45f;         // bu yakınlıktan sonra eğilmeye başlar
        public float tiltFull = 0.8f;           // bu yakınlıkta tam eğik
        public float smooth = 10f;

        /// <summary>0 = tüm harita, 1 = en yakın.</summary>
        public float Zoom { get; private set; }
        float targetZoom;
        Vector2 focus;          // kameranın baktığı yer (harita karesi koordinatı)
        Vector2 targetFocus;
        bool initialized;

        /// <summary>0 = düz kuş bakışı, 1 = tam eğik 3D görünüm. Binalar ve karakterler buna göre çıkar.</summary>
        public float Tilt01 => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(tiltStart, tiltFull, Zoom));

        /// <summary>3D karakterler gösterilsin mi (yakındayken)?</summary>
        public bool ShowModels => Tilt01 > 0.25f;

        Rect viewportRect = new Rect(0, 0, 1, 1);

        void Awake() { I = this; }

        static Vector2 MapCenter => new Vector2((CityMap.Width - 1) / 2f, (CityMap.Height - 1) / 2f);

        float FitSize(Camera cam)
        {
            float w = CityMap.Width + 1f, h = CityMap.Height + 1f;
            return Mathf.Max(h / 2f, w / 2f / Mathf.Max(0.1f, cam.aspect));
        }

        float SizeFor(Camera cam, float zoom)
        {
            float fit = FitSize(cam);
            return fit * Mathf.Pow(minOrthoSize / fit, zoom);
        }

        /// <summary>Arayüz kameranın haritaya düşen alanını söyler (sol panel ve üst bar hariç).</summary>
        public void SetViewport(Rect r) { viewportRect = r; }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || GameBootstrap.I == null) return;
            if (!GameBootstrap.I.Started)
            {
                cam.rect = new Rect(0, 0, 1, 1);
                return;
            }
            if (!initialized)
            {
                focus = targetFocus = MapCenter;
                initialized = true;
            }

            cam.rect = viewportRect;
            float k = 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime);
            Zoom = Mathf.Lerp(Zoom, targetZoom, k);
            if (Mathf.Abs(Zoom - targetZoom) < 0.0005f) Zoom = targetZoom;
            targetFocus = ClampFocus(cam, targetFocus, targetZoom);
            focus = Vector2.Lerp(focus, targetFocus, k);
            Apply(cam, Zoom, focus);
        }

        void Apply(Camera cam, float zoom, Vector2 f)
        {
            cam.orthographic = true;
            cam.orthographicSize = SizeFor(cam, zoom);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;
            float tilt = maxTilt * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(tiltStart, tiltFull, zoom));
            // Kamera +Z'ye bakar (harita XY düzleminde, binalar −Z yönünde yükselir); güneyden bakacak şekilde eğilir
            var rot = Quaternion.Euler(-tilt, 0f, 0f);
            cam.transform.rotation = rot;
            var ground = new Vector3(f.x, f.y, 0f);
            cam.transform.position = ground - rot * Vector3.forward * 60f;
        }

        Vector2 ClampFocus(Camera cam, Vector2 f, float zoom)
        {
            float size = SizeFor(cam, zoom);
            float halfH = size, halfW = size * cam.aspect;
            float mapHalfW = (CityMap.Width + 1f) / 2f, mapHalfH = (CityMap.Height + 1f) / 2f;
            var c = MapCenter;
            float maxDx = Mathf.Max(0f, mapHalfW - halfW + 1f);
            float maxDy = Mathf.Max(0f, mapHalfH - halfH + 1f);
            return new Vector2(Mathf.Clamp(f.x, c.x - maxDx, c.x + maxDx), Mathf.Clamp(f.y, c.y - maxDy, c.y + maxDy));
        }

        /// <summary>Ekran noktasının (piksel) zemindeki (z = 0) karşılığı.</summary>
        public bool GroundPoint(Camera cam, Vector2 screenPx, out Vector2 world)
        {
            world = Vector2.zero;
            var ray = cam.ScreenPointToRay(new Vector3(screenPx.x, screenPx.y, 0f));
            if (Mathf.Abs(ray.direction.z) < 1e-5f) return false;
            float t = -ray.origin.z / ray.direction.z;
            var p = ray.origin + ray.direction * t;
            world = new Vector2(p.x, p.y);
            return true;
        }

        /// <summary>Fare tekerleği: imlecin altındaki nokta yerinde kalacak şekilde yakınlaştırır.</summary>
        public void ZoomAt(float steps, Vector2 screenPx)
        {
            var cam = Camera.main;
            if (cam == null) return;
            float newZoom = Mathf.Clamp01(targetZoom + steps * 0.08f);
            if (Mathf.Approximately(newZoom, targetZoom)) return;
            if (GroundPoint(cam, screenPx, out var before))
            {
                // Hedef durumda aynı ekran noktasının nereye düştüğünü hesapla ve farkı kaydır
                Apply(cam, newZoom, targetFocus);
                if (GroundPoint(cam, screenPx, out var after)) targetFocus += before - after;
                Apply(cam, Zoom, focus);
            }
            targetZoom = newZoom;
            if (targetZoom <= 0.001f) targetFocus = MapCenter;
        }

        public void ZoomStep(float steps)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var r = cam.pixelRect;
            ZoomAt(steps, r.center);
        }

        /// <summary>Ekranda sürükleme (piksel): harita parmakla tutulmuş gibi kayar.</summary>
        public void DragPixels(Vector2 fromPx, Vector2 toPx)
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (GroundPoint(cam, fromPx, out var a) && GroundPoint(cam, toPx, out var b))
            {
                targetFocus += a - b;
                focus += a - b; // sürüklerken gecikmesiz
            }
        }

        /// <summary>Klavyeyle kaydırma (ekranın yüzdesi kadar).</summary>
        public void Pan(Vector2 dir)
        {
            var cam = Camera.main;
            if (cam == null) return;
            float size = SizeFor(cam, targetZoom);
            targetFocus += dir * size * 0.25f;
        }

        public void ResetView()
        {
            targetZoom = 0f;
            targetFocus = MapCenter;
        }

        /// <summary>Bir noktaya yakınlaştır (ör. kurye seçilince).</summary>
        public void FocusOn(Vector2 tile, float zoom)
        {
            targetFocus = tile;
            targetZoom = Mathf.Max(targetZoom, zoom);
        }

        /// <summary>
        /// Etiketin dünyadaki yeri: düz görünümde karenin biraz üstünde (+Y), eğik görünümde
        /// nesnenin tepesinde (−Z yönünde, yükseklik kadar).
        /// </summary>
        public static Vector3 LabelOffset(float upTopDown, float height)
        {
            float t = I != null ? I.Tilt01 : 0f;
            return Vector3.Lerp(new Vector3(0f, upTopDown, 0f), new Vector3(0f, 0f, -height), t);
        }
    }
}
