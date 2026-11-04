using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Harita kamerası. İki mod:
    ///  • Açılı görünüm (varsayılan): harita her zaman yukarıdan eğik görülür (kuzey yukarıda, sadece eğik),
    ///    binalar hep ayakta; yakınlaştıkça eğim biraz artar, kuryeler 3D karakter olur.
    ///  • Harita modu (M tuşu): eski düz, kuş bakışı 2D harita; yakınlaştıkça eğilir ve binalar yükselir.
    /// Fare tekerleği = yakınlaştır (imlecin olduğu yere), sağ/orta tuş veya sol tuşla sürükle = kaydır,
    /// ok tuşları / WASD = kaydır, + / − = yakınlaştır, 0 veya Home = tüm harita.
    /// </summary>
    public class MapCamera : MonoBehaviour
    {
        public static MapCamera I { get; private set; }

        [Header("Yakınlaştırma")]
        public float minOrthoSize = 3.2f;       // en yakın
        public float maxTilt = 50f;             // en yakında kameranın eğimi (derece)
        public float baseTilt = 38f;            // açılı görünümde en uzaktaki eğim (derece)
        public float tiltStart = 0.45f;         // bu yakınlıktan sonra eğilmeye başlar
        public float tiltFull = 0.8f;           // bu yakınlıkta tam eğik
        public float smooth = 10f;

        [Header("Döndürme")]
        float yaw, targetYaw;                   // kullanıcının seçtiği açı (derece); yakınlaşıp uzaklaşırken sabit kalır, kaydedilir
        public float Yaw => yaw;

        /// <summary>0 = tüm harita, 1 = en yakın.</summary>
        public float Zoom { get; private set; }
        float targetZoom;
        Vector2 focus;          // kameranın baktığı yer (harita karesi koordinatı)
        Vector2 targetFocus;
        bool initialized;

        // ---------- Harita modu (düz 2D) ----------
        const string FlatKey = "KuryeTycoon.HaritaModu";
        bool flatMode;
        float angled = 1f;      // 1 = açılı görünüm, 0 = düz harita modu (geçiş yumuşak)

        /// <summary>Düz 2D harita modu açık mı (M tuşu)?</summary>
        public bool FlatMode => flatMode;

        public void ToggleFlatMode()
        {
            flatMode = !flatMode;
            PlayerPrefs.SetInt(FlatKey, flatMode ? 1 : 0);
            PlayerPrefs.Save();
        }

        float FlatTilt01(float zoom) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(tiltStart, tiltFull, zoom));

        /// <summary>0 = düz kuş bakışı, 1 = binalar tam ayakta. Açılı görünümde hep 1.</summary>
        public float Tilt01 => Mathf.Lerp(FlatTilt01(Zoom), 1f, angled);

        float TiltDegrees(float zoom)
        {
            float t01 = FlatTilt01(zoom);
            float flat = maxTilt * t01;
            float ang = Mathf.Lerp(baseTilt, maxTilt, t01);
            return Mathf.Lerp(flat, ang, angled);
        }

        /// <summary>3D karakterler gösterilsin mi? Uzakta daireler daha okunaklı, yakınlaşınca karakter.</summary>
        public bool ShowModels => angled > 0.5f ? Zoom > tiltStart * 0.85f : FlatTilt01(Zoom) > 0.25f;

        Rect viewportRect = new Rect(0, 0, 1, 1);

        void Awake()
        {
            I = this;
            flatMode = PlayerPrefs.GetInt(FlatKey, 0) == 1;
            angled = flatMode ? 0f : 1f;
        }

        static Vector2 MapCenter => new Vector2((CityMap.Width - 1) / 2f, (CityMap.Height - 1) / 2f);

        float FitSize(Camera cam)
        {
            // En uzakta harita kuzey yukarıda durduğu için döndürmesiz sığdırılır.
            // Eğik bakınca harita ekranda dikeyde kısalır (cos eğim); binaların boyu için biraz pay bırakılır.
            float tilt = TiltDegrees(0f) * Mathf.Deg2Rad;
            float w = CityMap.Width + 1f, h = (CityMap.Height + 1f) * Mathf.Cos(tilt) + 2.2f * Mathf.Sin(tilt);
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
                yaw = targetYaw = 0f; // oyun hep düz (kuzey yukarı) başlar
                initialized = true;
            }

            cam.rect = viewportRect;
            float k = 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime);
            Zoom = Mathf.Lerp(Zoom, targetZoom, k);
            if (Mathf.Abs(Zoom - targetZoom) < 0.0005f) Zoom = targetZoom;
            targetFocus = ClampFocus(cam, targetFocus, targetZoom);
            focus = Vector2.Lerp(focus, targetFocus, k);
            yaw = Mathf.LerpAngle(yaw, targetYaw, k);
            angled = Mathf.MoveTowards(angled, flatMode ? 0f : 1f, Time.unscaledDeltaTime * 2.5f);
            Apply(cam, Zoom, focus);
        }

        void Apply(Camera cam, float zoom, Vector2 f)
        {
            cam.orthographic = true;
            cam.orthographicSize = SizeFor(cam, zoom);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;
            float tilt = TiltDegrees(zoom);
            // Kamera +Z'ye bakar (harita XY düzleminde, binalar −Z yönünde yükselir).
            // Önce dikey eksen (Z) etrafında döner, sonra eğilir. Kuzey yukarıda: sadece eğik.
            var rot = Quaternion.Euler(0f, 0f, -EffectiveYaw(zoom)) * Quaternion.Euler(-tilt, 0f, 0f);
            cam.transform.rotation = rot;
            var ground = new Vector3(f.x, f.y, 0f);
            cam.transform.position = ground - rot * Vector3.forward * 60f;
        }

        /// <summary>
        /// Uygulanan açı: 3D görünümde (eğik) seçilen açı tam uygulanır, yakınlaşıp uzaklaşırken dönmez.
        /// Binalar yere indikten sonra (düz 2D bölümde) açı yumuşakça sıfırlanır; en uzakta harita kuzey yukarıda.
        /// Seçilen açı unutulmaz: tekrar yakınlaşınca aynı yumuşaklıkla geri gelir.
        /// </summary>
        float EffectiveYaw(float zoom) => yaw;

        Vector2 ClampFocus(Camera cam, Vector2 f, float zoom)
        {
            if (zoom < 0.02f) return MapCenter;
            if (angled > 0.01f || zoom > tiltStart || Mathf.Abs(Mathf.DeltaAngle(EffectiveYaw(zoom), 0f)) > 1f)
            {
                // Dönük/eğik görünümde harita sınırları içinde serbest
                return new Vector2(Mathf.Clamp(f.x, 0f, CityMap.Width - 1), Mathf.Clamp(f.y, 0f, CityMap.Height - 1));
            }
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
            // Yakınlaşırken açı hiç değişmez (sadece Q/E ve sağ tuşla döner).
            // Uzaklaşırken binalar yere inip harita düz 2D'ye geçince yumuşakça kuzey yukarı haline döner.
            if (newZoom < targetZoom && newZoom < tiltStart) Straighten();
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
            // Ekrana göre: "yukarı" kameranın baktığı yön (döndürülmüşse ona göre)
            float a = -EffectiveYaw(Zoom) * Mathf.Deg2Rad;
            var d = new Vector2(dir.x * Mathf.Cos(a) - dir.y * Mathf.Sin(a), dir.x * Mathf.Sin(a) + dir.y * Mathf.Cos(a));
            targetFocus += d * size * 0.25f;
        }

        /// <summary>Kamerayı dikey eksen etrafında döndür (derece). Sadece yakın/eğik görünümde etkili.</summary>
        public void Rotate(float degrees)
        {
            targetYaw += degrees;
            SaveYaw();
        }

        /// <summary>Kuzeyi yukarı getir (döndürmeyi sıfırla).</summary>
        public void ResetRotation()
        {
            targetYaw = Mathf.Round(targetYaw / 360f) * 360f;
            SaveYaw();
        }

        void SaveYaw() { }

        /// <summary>Açıyı en yakın "kuzey yukarı" haline getirir (yumuşak, en kısa yönden döner).</summary>
        void Straighten()
        {
            targetYaw = Mathf.Round(targetYaw / 360f) * 360f;
        }

        /// <summary>Sağ tuşla sürükleme: yatay hareket döndürür.</summary>
        public void RotateDrag(float pixels)
        {
            targetYaw += pixels * 0.3f;
            yaw = targetYaw; // sürüklerken gecikmesiz
            SaveYaw();
        }

        public void ResetView()
        {
            targetZoom = 0f;
            targetFocus = MapCenter;
            Straighten();
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
            // Düz görünümde "yukarı" ekranın yukarısı: kamera döndürülmüşse ona göre
            var up = Quaternion.Euler(0f, 0f, I != null ? -I.EffectiveYaw(I.Zoom) : 0f) * new Vector3(0f, upTopDown, 0f);
            return Vector3.Lerp(up, new Vector3(0f, 0f, -height), t);
        }
    }
}
