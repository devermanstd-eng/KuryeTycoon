using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>Haritada dolaşan tek bir rakip kurye (görsel).</summary>
    public class RivalCourierAgent : MonoBehaviour
    {
        enum S { ToShop, AtShop, ToCustomer, AtDoor }

        public Vector2 Position { get; private set; }
        public bool carrying;
        public bool leaving;
        public string label = "Turbo";

        S state;
        List<Vector2Int> path;
        int pathIndex;
        float waitUntil;
        float speed;
        SpriteRenderer ring, body;
        GameObject model;
        float facing = 180f;
        Vector2 lastPos;

        static readonly string[] Names = { "Ádám", "Gergő", "Nóra", "Kata", "Levente", "Zsófi", "Barış", "Deniz", "Mert", "Ece", "Péter", "Dóra" };

        public void Init(Vector2Int start, int index)
        {
            Position = start;
            lastPos = start;
            label = "Turbo · " + Names[Random.Range(0, Names.Length)];
            var v = GameData.Vehicles[Random.Range(0, 3)]; // çoğu bisiklet/scooter/motor
            speed = v.tilesPerMinute * Random.Range(0.85f, 1.1f);
            transform.position = CityMap.ToWorld(Position);
            ring = SpriteFactory.Create("Halka", transform, true, RivalFleet.RivalColor, new Vector3(0f, 0f, -0.04f), 0.6f, 9);
            body = SpriteFactory.Create("Gövde", transform, true, Color.Lerp(RivalFleet.RivalColor, Color.white, 0.35f), new Vector3(0f, 0f, -0.05f), 0.4f, 10);
            model = CharacterVisuals.Spawn(transform, label + index);
            NextShop();
        }

        void GoTo(Vector2Int target)
        {
            path = CityMap.I.FindPath(Vector2Int.RoundToInt(Position), target);
            pathIndex = 0;
        }

        void NextShop()
        {
            if (leaving) { Destroy(gameObject); return; }
            var options = new List<Shop>();
            foreach (var s in CityMap.I.Shops)
                if (s.rivalContracted && CityMap.I.IsUnlocked(s.tile)) options.Add(s);
            carrying = false;
            if (options.Count == 0)
            {
                // Açık bölgede rakibin dükkanı yok: şehirde dolaşır
                GoTo(CityMap.I.GetDoor(CityMap.I.RandomCustomerTile(new Vector2Int(-10, -10))));
                state = S.ToCustomer;
                return;
            }
            GoTo(options[Random.Range(0, options.Count)].door);
            state = S.ToShop;
        }

        bool Move(float dm)
        {
            if (path == null || path.Count == 0) return true;
            float remaining = speed * dm;
            while (remaining > 0f && pathIndex < path.Count)
            {
                Vector2 target = path[pathIndex];
                Vector2 delta = target - Position;
                float d = delta.magnitude;
                if (d <= remaining) { Position = target; remaining -= d; pathIndex++; }
                else { Position += delta / d * remaining; remaining = 0f; }
            }
            return pathIndex >= path.Count;
        }

        void Update()
        {
            var clock = GameClock.I;
            float dm = clock != null ? clock.DeltaMinutes : 0f;
            if (dm > 0f)
            {
                float now = clock.TotalMinutes;
                switch (state)
                {
                    case S.ToShop:
                        if (Move(dm)) { state = S.AtShop; waitUntil = now + Random.Range(1f, 6f); }
                        break;
                    case S.AtShop:
                        if (now >= waitUntil)
                        {
                            carrying = true;
                            GoTo(CityMap.I.GetDoor(CityMap.I.RandomCustomerTile(new Vector2Int(-10, -10))));
                            state = S.ToCustomer;
                        }
                        break;
                    case S.ToCustomer:
                        if (Move(dm)) { state = S.AtDoor; waitUntil = now + (carrying ? Random.Range(1f, 5f) : 0.5f); }
                        break;
                    case S.AtDoor:
                        if (now >= waitUntil) NextShop();
                        break;
                }
            }

            transform.position = CityMap.ToWorld(Position);
            bool walking = dm > 0f && (state == S.ToShop || state == S.ToCustomer);
            bool inside = state == S.AtDoor && carrying;           // binada teslim ediyor
            if (model != null)
            {
                bool show = MapCamera.I != null && MapCamera.I.ShowModels && !inside;
                CharacterVisuals.UpdateModel(model, Position - lastPos, walking, show, ref facing);
                body.enabled = !show && !inside;
                ring.transform.localScale = Vector3.one * (show ? 0.3f : 0.6f);
            }
            else body.enabled = !inside;
            ring.enabled = !inside;
            lastPos = Position;
        }

        /// <summary>Etiket görünsün mü (binanın içindeyken gizli).</summary>
        public bool Visible => !(state == S.AtDoor && carrying);
    }
}
