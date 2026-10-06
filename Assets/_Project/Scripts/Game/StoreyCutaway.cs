using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>
    /// Keeps players in view in a house with an upstairs, under a camera that looks down from above. Storeys
    /// above the highest local player are hidden. In couch play, the upstairs rooms that would cover a local
    /// player on a lower storey are lifted off as well, so nobody is hidden (when one stands right over
    /// another, the upper one's room comes off too, floor and all). Once every local player is out, it keeps
    /// whoever is still fighting in view instead. Only drawing changes: colliders, physics and bots carry
    /// on as before, and a player being kept in view is never hidden.
    /// </summary>
    public sealed class StoreyCutaway : MonoBehaviour
    {
        public static StoreyCutaway Instance { get; private set; }

        const float Interval = .2f;
        /// <summary>Going down a flight, the view drops a storey only once you're this far below the top.</summary>
        const float Hysteresis = .3f;
        /// <summary>Clear space kept round a player under a lifted room, and their height.</summary>
        const float Margin = .6f, BodyHeight = 1.8f;
        /// <summary>A piece of a flight rising less than this above your feet can't hide you.</summary>
        const float WallHeight = 1.1f;
        /// <summary>The match camera looks down along (0, -30, 22): each metre up shifts the view 22/30 m south.</summary>
        const float SouthPerMetre = 22f / 30f;

        HouseLayout layout;
        List<float> floors;
        List<(RoomBox room, int storey)> rooms;
        readonly List<(Renderer renderer, int storey, Rect area)> fixedParts = new();
        readonly HashSet<Renderer> fixedSet = new();
        /// <summary>Steps and rails, which stand taller than a wall on their own storey.</summary>
        readonly Dictionary<Renderer, Bounds> flightParts = new();
        readonly HashSet<Renderer> hidden = new();
        readonly Dictionary<PlayerController, int> storeyOf = new();
        readonly HashSet<string> lifted = new();
        readonly List<(int storey, Rect area)> liftedAreas = new();
        float next;

        /// <summary>The highest storey drawn.</summary>
        public int TopStorey { get; private set; }
        /// <summary>The height the camera should centre on: the floor you're on (between the lowest and highest in couch play).</summary>
        public float FocusY { get; private set; }
        /// <summary>Upstairs rooms lifted off to show someone below them.</summary>
        public IReadOnlyCollection<string> LiftedRooms => lifted;

        public void Configure(HouseLayout house, Transform geometry)
        {
            layout = house;
            floors = house.StoreyFloors();
            rooms = house.Rooms.Select(r => (r, house.StoreyOf(r))).ToList();
            Instance = this;
            for (int s = 0; s < floors.Count; s++)
            {
                var group = geometry.Find(RoomBuilder.StoreyName(s));
                if (!group) continue;
                foreach (var r in group.GetComponentsInChildren<Renderer>(true))
                {
                    var b = r.bounds;
                    fixedParts.Add((r, s, Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z)));
                    fixedSet.Add(r);
                    if (r.name == "Step" || r.name == "Stair rail") flightParts[r] = b;
                }
            }
            Refresh();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var r in hidden) if (r) r.forceRenderingOff = false;
        }

        void LateUpdate()
        {
            if (layout == null || Time.unscaledTime < next) return;
            next = Time.unscaledTime + Interval;
            Refresh();
        }

        /// <summary>Works out what to draw now (it also runs about five times a second on its own).</summary>
        public void Refresh()
        {
            var locals = World.Players.Where(p => p && !p.IsEliminated && p.Binding is not BotBinding).ToList();
            // Spectating: watch whoever is still fighting, so the floors above don't hide the rest of the round.
            if (locals.Count == 0) locals = World.Players.Where(p => p && !p.IsEliminated).ToList();
            foreach (var gone in storeyOf.Keys.Where(p => !p || !locals.Contains(p)).ToList()) storeyOf.Remove(gone);
            int top = 0, low = int.MaxValue;
            foreach (var p in locals) { int s = StoreyOf(p); top = Mathf.Max(top, s); low = Mathf.Min(low, s); }
            // Nobody left at all: just the ground floor.
            if (locals.Count == 0) low = 0;
            TopStorey = top;
            FocusY = (floors[low] + floors[top]) * .5f;

            lifted.Clear();
            liftedAreas.Clear();
            // Behind the player in third person, nothing upstairs lies between the camera and anyone; the
            // storeys above stay off, an open top as in the browser edition.
            bool overhead = !CameraRig.Instance || !CameraRig.Instance.isActiveAndEnabled || !CameraRig.Instance.IsThirdPerson;
            if (overhead) foreach (var p in locals)
            {
                var at = p.transform.position;
                for (int above = storeyOf[p] + 1; above <= top; above++)
                {
                    // Where the camera's view of this player's body crosses the floor above.
                    float rise = floors[above] - at.y;
                    var view = Rect.MinMaxRect(at.x - Margin, at.z - rise * SouthPerMetre - Margin,
                        at.x + Margin, at.z - Mathf.Max(0f, rise - BodyHeight) * SouthPerMetre + Margin);
                    foreach (var (room, storey) in rooms)
                    {
                        if (storey != above) continue;
                        var area = Rect.MinMaxRect(room.MinX + .05f, room.MinZ + .05f, room.MaxX - .05f, room.MaxZ - .05f);
                        if (!area.Overlaps(view) || !lifted.Add(room.Name)) continue;
                        liftedAreas.Add((above, area));
                    }
                }
            }

            // The tall end of a flight hides whoever stands just past it on the same floor, so it comes off for them.
            bool InTheWay(Renderer r, int storey)
            {
                if (!overhead || !flightParts.TryGetValue(r, out var b)) return false;
                var piece = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
                foreach (var p in locals)
                {
                    var at = p.transform.position;
                    float over = b.max.y - at.y;
                    if (storeyOf[p] != storey || over < WallHeight) continue;
                    if (piece.Overlaps(Rect.MinMaxRect(at.x - Margin, at.z - over * SouthPerMetre - Margin, at.x + Margin, at.z + Margin))) return true;
                }
                return false;
            }
            foreach (var (renderer, storey, area) in fixedParts)
                if (renderer) Show(renderer, storey <= top && !Lifted(storey, area) && !InTheWay(renderer, storey));

            // Everything else (furniture, letters, boxes, bots, effects) by where it is now.
            var mine = new HashSet<Transform>(locals.Select(p => p.transform));
            foreach (var r in FindObjectsByType<Renderer>())
            {
                if (fixedSet.Contains(r)) continue;
                var b = r.bounds;
                int storey = StoreyAt(b.min.y);
                bool draw = storey <= top && !Lifted(storey, Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z));
                if (!draw && Belongs(r.transform, mine)) draw = true;
                Show(r, draw);
            }
            hidden.RemoveWhere(r => !r);
        }

        /// <summary>The storey a player counts as on: the one the view is holding for them on a flight, else by height.</summary>
        public int StoreyOfPlayer(PlayerController p) =>
            p && storeyOf.TryGetValue(p, out int s) ? s : p ? StoreyAt(p.transform.position.y) : 0;

        int StoreyOf(PlayerController p)
        {
            float y = p.transform.position.y;
            int now = StoreyAt(y);
            if (storeyOf.TryGetValue(p, out int was) && now < was && StoreyAt(y + Hysteresis) >= was) now = was;
            storeyOf[p] = now;
            return now;
        }

        /// <summary><see cref="HouseLayout.StoreyAt"/> without rebuilding the storey list for every object.</summary>
        int StoreyAt(float y)
        {
            int storey = 0;
            for (int i = 1; i < floors.Count; i++)
                if (floors[i] <= y + HouseLayout.StandingSlack) storey = i;
            return storey;
        }

        bool Lifted(int storey, Rect area)
        {
            foreach (var (s, a) in liftedAreas)
                if (s == storey && a.Overlaps(area)) return true;
            return false;
        }

        static bool Belongs(Transform t, HashSet<Transform> players)
        {
            for (; t; t = t.parent)
                if (players.Contains(t)) return true;
            return false;
        }

        void Show(Renderer r, bool draw)
        {
            if (draw)
            {
                if (hidden.Remove(r)) r.forceRenderingOff = false;
            }
            else if (hidden.Add(r)) r.forceRenderingOff = true;
        }

        /// <summary>True if the renderer is drawn by the cutaway (it may still be off for its own reasons).</summary>
        public bool Draws(Renderer r) => !hidden.Contains(r);
    }
}
