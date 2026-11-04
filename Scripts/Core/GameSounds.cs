using UnityEngine;

namespace KuryeTycoon
{
    public enum Sfx { Teslimat, Iyi, Kotu, Buyuk }

    /// <summary>
    /// Basit oyun sesleri. Ses dosyası gerekmez: kısa tonlar oyun açılırken koddan üretilir.
    ///  • Teslimat: yumuşak "ding" (çok hızlı oyunda ve art arda gelince susar)
    ///  • İyi / kötü bildirim: yükselen / alçalan iki nota
    ///  • Büyük an (kilometre taşı): kısa arpej
    /// Ayarlar > Arayüz'den açılıp kapatılır ve ses seviyesi seçilir (bu bilgisayarda kalıcı).
    /// Unity'de ses kapalıysa (Project Settings > Audio > Disable Unity Audio) hiçbir şey çalmaz, hata da vermez.
    /// </summary>
    public class GameSounds : MonoBehaviour
    {
        public static GameSounds I { get; private set; }

        const int Rate = 44100;
        AudioSource source;
        AudioClip[] clips;
        readonly float[] lastPlayed = { -9f, -9f, -9f, -9f };
        static readonly float[] MinGap = { 0.35f, 0.5f, 0.5f, 0.8f };   // aynı ses art arda en az bu kadar arayla
        static readonly float[] BaseVolume = { 0.22f, 0.35f, 0.35f, 0.45f };

        // ---------------------------------------------------------------- ayarlar
        const string OnKey = "KuryeTycoon.Sesler";
        const string VolumeKey = "KuryeTycoon.SesSeviyesi";
        static int onCache = -1;
        static float volumeCache = -1f;

        public static bool Enabled
        {
            get
            {
                if (onCache < 0) onCache = PlayerPrefs.GetInt(OnKey, 1);
                return onCache == 1;
            }
            set
            {
                onCache = value ? 1 : 0;
                PlayerPrefs.SetInt(OnKey, onCache);
                PlayerPrefs.Save();
            }
        }

        /// <summary>0–1</summary>
        public static float Volume
        {
            get
            {
                if (volumeCache < 0f) volumeCache = PlayerPrefs.GetFloat(VolumeKey, 0.6f);
                return volumeCache;
            }
            set
            {
                volumeCache = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, volumeCache);   // kaydırırken her kare diske yazmasın: Unity çıkışta kaydeder
            }
        }

        void Awake()
        {
            I = this;
            // Sesin duyulması için sahnede bir dinleyici olmalı (kamera kodla kurulduysa yoktur)
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;   // 2D ses: kameranın yerinden bağımsız

            clips = new[]
            {
                Make("Teslimat", new[] { 1318.5f }, 0.28f, 0f, 9f),
                Make("Iyi", new[] { 1046.5f, 1568f }, 0.13f, 0.11f, 7f),
                Make("Kotu", new[] { 440f, 329.6f }, 0.16f, 0.14f, 6f),
                Make("Buyuk", new[] { 523.3f, 659.3f, 784f, 1046.5f }, 0.2f, 0.09f, 5f),
            };
        }

        /// <summary>Sesi çalar (ayar kapalıysa ya da aynı ses az önce çaldıysa çalmaz).</summary>
        public static void Play(Sfx s)
        {
            if (I == null || !Enabled || I.source == null) return;
            // Çok hızlı oyunda teslimat sesi sürekli çalmasın
            if (s == Sfx.Teslimat && GameClock.I != null && GameClock.I.SpeedMultiplier > 16f) return;
            int i = (int)s;
            float now = Time.unscaledTime;
            if (now - I.lastPlayed[i] < MinGap[i]) return;
            I.lastPlayed[i] = now;
            I.source.PlayOneShot(I.clips[i], BaseVolume[i] * Volume);
        }

        /// <summary>
        /// Notaları sırayla çalan kısa bir klip üretir. Her nota zil gibi: temel ton + biraz üst ton, üstel sönüm.
        /// </summary>
        static AudioClip Make(string name, float[] notes, float noteLength, float step, float decay)
        {
            float total = step * (notes.Length - 1) + noteLength;
            int n = Mathf.CeilToInt(total * Rate);
            var data = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = Mathf.RoundToInt(k * step * Rate);
                int len = Mathf.Min(n - start, Mathf.CeilToInt(noteLength * Rate));
                float f = notes[k];
                for (int j = 0; j < len; j++)
                {
                    float t = j / (float)Rate;
                    float env = Mathf.Exp(-t * decay / noteLength * 0.25f) * Mathf.Clamp01(t / 0.004f); // yumuşak başlangıç, sönüm
                    env *= Mathf.Clamp01((len - j) / (0.01f * Rate));                                  // sonda tık olmasın
                    float w = Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * f * t) + 0.12f * Mathf.Sin(6f * Mathf.PI * f * t);
                    data[start + j] += w * env * 0.5f;
                }
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
