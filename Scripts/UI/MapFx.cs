using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Haritada kısa süreli uçan yazılar: teslimatta "+350 Ft" (siparişten kalan kâr), iptalde kırmızı "İptal".
    /// Oyun kodu buraya haber verir (OrderManager), GameUI çizer. Ayarlar > Arayüz'den kapatılabilir.
    /// </summary>
    public static class MapFx
    {
        public struct Pop
        {
            public Vector2Int tile;
            public string text;
            public Color color;
            public float born;   // gerçek zaman (oyun hızından bağımsız)
        }

        public const float Life = 1.6f;
        const int MaxPops = 40;
        public static readonly List<Pop> Pops = new List<Pop>();

        public static readonly Color Good = new Color(0.45f, 0.95f, 0.55f);
        public static readonly Color Bad = new Color(1f, 0.42f, 0.38f);

        public static void Delivered(Order o)
        {
            if (o == null) return;
            int p = o.Profit;
            Add(o.customerTile, p >= 0 ? "+" + GameData.Ft(p) : GameData.Ft(p), p >= 0 ? Good : Bad);
            GameSounds.Play(Sfx.Teslimat);
        }

        public static void Cancelled(Order o)
        {
            if (o == null) return;
            Add(o.customerTile, "İptal", Bad);
        }

        static void Add(Vector2Int tile, string text, Color color)
        {
            if (Pops.Count >= MaxPops) Pops.RemoveAt(0);
            Pops.Add(new Pop { tile = tile, text = text, color = color, born = Time.unscaledTime });
        }
    }

    public partial class GameUI
    {
        const string PopsKey = "KuryeTycoon.UcanYazilar";
        static int popsCache = -1;

        static bool ShowPopsSetting
        {
            get
            {
                if (popsCache < 0) popsCache = PlayerPrefs.GetInt(PopsKey, 1);
                return popsCache == 1;
            }
            set
            {
                popsCache = value ? 1 : 0;
                PlayerPrefs.SetInt(PopsKey, popsCache);
                PlayerPrefs.Save();
            }
        }

        GUIStyle popStyle;

        /// <summary>Uçan yazılar: yukarı süzülüp kaybolur. Dünya etiketlerinden sonra çizilir.</summary>
        void DrawMapPops()
        {
            var pops = MapFx.Pops;
            if (pops.Count == 0) return;
            float now = Time.unscaledTime;
            if (Event.current.type == EventType.Repaint)
                pops.RemoveAll(q => now - q.born > MapFx.Life);
            if (!ShowPopsSetting || Event.current.type != EventType.Repaint) return;
            var cam = Camera.main;
            if (cam == null) return;
            if (popStyle == null)
                popStyle = UITheme.Semi(new GUIStyle(label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false });

            float left = MapLeftGui;
            foreach (var p in pops)
            {
                float age = now - p.born;
                var world = CityMap.ToWorld(p.tile) + MapCamera.LabelOffset(0.4f, BuildingTop(p.tile) + 0.3f);
                Vector3 sp = cam.WorldToScreenPoint(world);
                if (sp.z < 0f) continue;
                float x = sp.x / uiScale;
                float y = (Screen.height - sp.y) / uiScale - age * 26f;
                if (x < left || y < TopBar) continue;
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(MapFx.Life * 0.45f, MapFx.Life, age));
                var r = new Rect(x - 80f, y - 11f, 160f, 22f);
                popStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f * a);
                GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), p.text, popStyle);
                popStyle.normal.textColor = UITheme.WithAlpha(p.color, a);
                GUI.Label(r, p.text, popStyle);
            }
        }

        /// <summary>Ayarlar > Arayüz: sesler ve uçan yazılar.</summary>
        void DrawFeedbackSettings()
        {
            bool pops = GUILayout.Toggle(ShowPopsSetting, " Haritada teslimat kârını göster (uçan \"+350 Ft\" yazıları)", toggle);
            if (pops != ShowPopsSetting) ShowPopsSetting = pops;
            bool snd = GUILayout.Toggle(GameSounds.Enabled, " Sesler (teslimat, bildirim, kilometre taşı)", toggle);
            if (snd != GameSounds.Enabled) GameSounds.Enabled = snd;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Ses seviyesi: %{GameSounds.Volume * 100f:0}", small, GUILayout.Width(130));
            float v = GUILayout.HorizontalSlider(GameSounds.Volume, 0f, 1f, GUILayout.Width(180));
            if (Mathf.Abs(v - GameSounds.Volume) > 0.001f) GameSounds.Volume = v;
            if (GUILayout.Button("Dene", smallButton, GUILayout.Width(60))) GameSounds.Play(Sfx.Iyi);
            GUILayout.EndHorizontal();
        }
    }
}
