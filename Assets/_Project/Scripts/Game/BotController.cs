using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>A match-only seat. The brain uses the same commands and crafting entry point as a human.</summary>
    public sealed class BotBinding : InputBinding
    {
        static int nextId;
        readonly string id = $"bot-{nextId++}";
        public PlayerCommands Commands;
        public override string Id => id;
        public override void Read(ref PlayerCommands commands) => commands = Commands;
        public override bool JoinPressed() => false;
        public override bool StartPressed() => false;
    }

    [DefaultExecutionOrder(-100)]
    public sealed class BotController : MonoBehaviour
    {
        PlayerController player;
        BotBinding binding;
        Transform target;
        float nextThink, nextAttack, nextCraft, nextEscape;
        Vector3 lastPosition;
        float stuckTime;
        ClearOutController clearOut;
        HouseLayout layout;
        RoomGraph graph;

        public void Configure(PlayerController owner, BotBinding input)
        {
            player = owner;
            binding = input;
            lastPosition = transform.position;
            clearOut = FindAnyObjectByType<ClearOutController>();
            layout = clearOut ? clearOut.Layout : GameConfig.Current.HouseFor(Session.MapId);
            graph = layout.Graph();
        }

        void Update()
        {
            if (binding == null || !player) return;
            var c = new PlayerCommands();
            if (!player.CanAct) { binding.Commands = c; return; }

            var at = player.transform.position;
            string here = layout.RoomAt(at.x, at.y, at.z);
            Vector3 destination = at;

            var downed = World.Players.Where(p => p && p.IsDowned && Teams.AreTeammates(player.Team, p.Team) && WithinReach(layout, p.transform.position))
                .OrderBy(p => (p.transform.position - at).sqrMagnitude).FirstOrDefault();
            bool evacuating = clearOut && clearOut.Running && here != null &&
                clearOut.Schedule.PhaseOf(here, clearOut.Elapsed) >= RoomPhase.Warning;

            if (evacuating)
            {
                var safe = layout.Rooms.Where(r => clearOut.Schedule.PhaseOf(r.Name, clearOut.Elapsed) == RoomPhase.Safe)
                    .OrderBy(r => (Centre(r) - at).sqrMagnitude).FirstOrDefault();
                if (safe != null) destination = Centre(safe);
            }
            else if (downed)
            {
                destination = downed.transform.position;
                if (World.Flat(destination - at).magnitude <= player.Health.Rules.ReviveRange * .9f && SameLevel(destination, at) && Reachable(layout, at, destination))
                {
                    c.grabHeld = true;
                    c.drop = player.Combat.IsHolding;
                    c.grab = !player.Combat.IsReviving && !player.Combat.IsHolding;
                    destination = at;
                }
            }
            else
            {
                if (Time.time >= nextThink)
                {
                    nextThink = Time.time + .3f;
                    var opponent = World.NearestOpponent(player, at);
                    // Gather letters and build equipment before closing in; never conjure free gear.
                    var tile = TilePool.Instance && player.Inventory.Count < player.Inventory.Capacity
                        ? TilePool.Instance.Active.Where(t => t).OrderBy(t => Cost(t.transform.position, at)).FirstOrDefault() : null;
                    if (opponent && (player.Combat.Weapon || player.Inventory.TotalCount >= 8 || Vector3.Distance(opponent.transform.position, at) < 3f)) target = opponent.transform;
                    else if (tile && !player.Combat.Weapon) target = tile.transform;
                    else target = FindObjectsByType<Smashable>().Where(s => s && !s.IsBroken && !s.Invulnerable && WithinReach(layout, s.transform.position))
                        .OrderBy(s => Cost(s.transform.position, at)).FirstOrDefault()?.transform ?? opponent?.transform;
                }
                if (target)
                {
                    destination = target.position;
                    float distance = World.Flat(destination - at).magnitude;
                    if (distance < 1.35f && SameLevel(destination, at) && Reachable(layout, at, destination) && !target.GetComponent<LetterTile>())
                    {
                        if (Time.time >= nextAttack) { c.attack = true; nextAttack = Time.time + .6f; }
                        destination = at;
                    }
                }
                if (!player.Combat.Weapon && Time.time >= nextCraft)
                {
                    nextCraft = Time.time + 1f;
                    var ready = WordSolver.Spellable(GameAssets.I.words.Words, player.Inventory.Letters)
                        .FirstOrDefault(w => w.category == WordCategory.Weapon);
                    if (ready != null) player.Summoner.BeginCraft(ready);
                }
            }

            var waypoint = Waypoint(layout, graph, here, at, destination, clearOut);
            var delta = World.Flat(waypoint - at);
            // Stop a little short of where you're going, but walk right up to a point on the way there: stopping short
            // of a doorway's mouth left a bot standing just off to one side of the door for good.
            bool final = World.Flat(waypoint - destination).sqrMagnitude < .0001f && SameLevel(waypoint, destination);
            var direction = delta.sqrMagnitude > (final ? .16f : .01f) ? delta.normalized : Vector3.zero;
            if (target && !evacuating && !downed) c.look = new Vector2(target.position.x - at.x, target.position.z - at.z);
            else c.look = new Vector2(direction.x, direction.z);
            if (direction.sqrMagnitude > 0f)
            {
                // Room graph handles the walls; local steering skirts furniture between doorways. A slope you can
                // walk up (stairs, a ramp) isn't in the way, and nor is the thing you're after (only it: the whole
                // house hangs off one root).
                if (Physics.SphereCast(at + Vector3.up * .4f, .25f, direction, out var hit, .85f, World.GroundMask,
                    QueryTriggerInteraction.Ignore) && hit.normal.y < .6f && hit.rigidbody != player.Body && (!target || !hit.transform.IsChildOf(target)))
                {
                    // Turn aside, but not back onto a flight of stairs you've just stepped off (a door beside the
                    // top, or a rail's end beside the foot, otherwise sends you up and down it).
                    float turn = 55f + player.Index * 12f;
                    var usual = Quaternion.Euler(0f, turn, 0f) * direction;
                    var other = Quaternion.Euler(0f, -turn, 0f) * direction;
                    direction = !NearStairs(layout, waypoint, .8f) && NearStairs(layout, at + usual * .85f, .4f)
                        && !NearStairs(layout, at + other * .85f, .4f) ? other : usual;
                }
                c.move = new Vector2(direction.x, direction.z);
            }
            stuckTime = c.move.sqrMagnitude > .2f && World.Flat(at - lastPosition).sqrMagnitude < .0004f
                ? stuckTime + Time.deltaTime : 0f;
            if (stuckTime > .7f && Time.time >= nextEscape)
            {
                c.jump = true;
                c.dodge = player.Health.CanDodge;
                nextEscape = Time.time + 1.5f;
                stuckTime = 0f;
            }
            lastPosition = at;
            binding.Commands = c;
        }

        static Vector3 Centre(RoomBox room) => new((room.MinX + room.MaxX) * .5f, room.FloorY, (room.MinZ + room.MaxZ) * .5f);

        /// <summary>Close enough in height to hit or revive: not a floor apart.</summary>
        static bool SameLevel(Vector3 a, Vector3 b) => Mathf.Abs(a.y - b.y) < 1.2f;

        /// <summary>
        /// On a floor or a flight, or low enough to hit from one. Up on a ledge (the playroom balcony) there's no
        /// way up a bot can find, so it would only stand underneath.
        /// </summary>
        static bool WithinReach(HouseLayout layout, Vector3 p)
        {
            if (FlightUnder(layout, p) != null) return true;
            string room = layout.RoomAt(p.x, p.y, p.z);
            return room == null || p.y - layout.Room(room).FloorY < 1.2f;
        }

        /// <summary>How far a thing feels: a storey up or down costs about six metres of walking to the stairs.</summary>
        static float Cost(Vector3 thing, Vector3 at) => World.Flat(thing - at).magnitude + Mathf.Abs(thing.y - at.y) * 2f;

        static Vector3 Waypoint(HouseLayout layout, RoomGraph graph, string start, Vector3 at, Vector3 goal, ClearOutController hazard)
        {
            // Already there (standing to hit or revive): stay put, on a flight too.
            if (World.Flat(goal - at).sqrMagnitude < .01f && SameLevel(goal, at)) return goal;
            var goalFlight = FlightUnder(layout, goal);
            // On a flight, finish it (the rails would stop you cutting across) towards the goal's floor, unless the goal is on it.
            foreach (var s in layout.Stairs)
                if (OnFlight(layout, s, at))
                    return s == goalFlight ? goal : StairsWaypoint(layout, s, at, goal.y > (layout.Room(s.Lower).FloorY + layout.Room(s.Upper).FloorY) * .5f);
            string end = layout.RoomAt(goal.x, goal.y, goal.z);
            if (start == null || end == null) return goal;
            if (start == end)
                // Something on a flight in this room is reached along the flight, not across its rail.
                return goalFlight != null && (goalFlight.Lower == start || goalFlight.Upper == start)
                    ? StairsWaypoint(layout, goalFlight, at, goalFlight.Lower == start)
                    : Around(layout, start, at, goal);
            var queue = new Queue<string>();
            var from = new Dictionary<string, string> { [start] = null };
            queue.Enqueue(start);
            while (queue.Count > 0 && !from.ContainsKey(end))
            {
                string room = queue.Dequeue();
                foreach (var next in graph.Neighbours(room))
                {
                    if (from.ContainsKey(next) || (hazard && hazard.Running && hazard.Schedule.PhaseOf(next, hazard.Elapsed) == RoomPhase.Closed)) continue;
                    from[next] = room;
                    queue.Enqueue(next);
                }
            }
            if (!from.ContainsKey(end))
            {
                // No open way there: wait in the middle of this room.
                var middle = Centre(layout.Room(start));
                return World.Flat(middle - at).sqrMagnitude < .16f ? at : Around(layout, start, at, middle);
            }
            string step = end;
            while (from[step] != start) step = from[step];
            var door = layout.Doors.FirstOrDefault(d => (d.A == start && d.B == step) || (d.B == start && d.A == step));
            if (door == null)
            {
                var stairs = layout.Stairs.First(s => (s.Lower == start && s.Upper == step) || (s.Upper == start && s.Lower == step));
                return StairsWaypoint(layout, stairs, at, stairs.Lower == start);
            }
            // Line up in front of the doorway, then aim just beyond the threshold so RoomAt switches rooms instead
            // of stopping on the wall. Cutting the corner from off to one side catches the jamb.
            var here = layout.Room(start);
            var doorAt = new Vector3(door.X, here.FloorY, door.Z);
            bool inSideWall = (Mathf.Abs(door.X - here.MinX) < .01f || Mathf.Abs(door.X - here.MaxX) < .01f)
                && door.Z > here.MinZ + .01f && door.Z < here.MaxZ - .01f;
            var normal = inSideWall ? new Vector3(Mathf.Sign(door.X - (here.MinX + here.MaxX) * .5f), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(door.Z - (here.MinZ + here.MaxZ) * .5f));
            var offset = at - doorAt;
            float before = -Vector3.Dot(offset, normal), aside = Mathf.Abs(inSideWall ? offset.z : offset.x);
            bool lined = before < .25f || aside < Mathf.Max(.15f, door.Width * .5f - .45f);
            var through = new Vector3(door.X, layout.Room(step).FloorY, door.Z) + normal * .7f;
            return Around(layout, start, at, lined ? through : doorAt - normal * .8f);
        }

        /// <summary>
        /// Where to head to take a flight of stairs: line up in front of the end you're on (walking round the
        /// side if you're beside the flight), then go straight along it and off the far end, where RoomAt
        /// switches rooms.
        /// </summary>
        static Vector3 StairsWaypoint(HouseLayout layout, Stairway s, Vector3 at, bool up)
        {
            RoomBox lower = layout.Room(s.Lower), upper = layout.Room(s.Upper), here = up ? lower : upper;
            s.Along(0f, out float fx, out float fz);
            s.Along(s.Run, out float tx, out float tz);
            var foot = new Vector3(fx, lower.FloorY, fz);
            var top = new Vector3(tx, upper.FloorY, tz);
            var forward = World.Flat(top - foot).normalized;
            var side = Vector3.Cross(Vector3.up, forward);
            var offset = World.Flat(at - foot);
            float along = Vector3.Dot(offset, forward), across = Vector3.Dot(offset, side);
            bool inLine = Mathf.Abs(across) < s.Width * .5f;
            // Where you step on, and where you step off at the far end.
            Vector3 onto = up ? foot - forward * Stairway.StepOff : top + forward * Stairway.StepOff;
            Vector3 off = up ? top + forward * Stairway.StepOff : foot - forward * Stairway.StepOff;
            if (up)
            {
                // In front of the foot or on the flight (not underneath it, where the steps are solid).
                float ramp = lower.FloorY + Mathf.Max(0f, along) * (upper.FloorY - lower.FloorY) / s.Run;
                if (inLine && along > -1.2f && along < s.Run + .5f && at.y > ramp - .6f) return off;
                if (along > -.3f) return Around(layout, here.Name, at, Beside(here, onto, side * Mathf.Sign(across) * (s.Width * .5f + .7f)));
                return Around(layout, here.Name, at, onto);
            }
            // Coming down, the only way into the opening is at the top; the railing closes off the rest. From beside
            // the lane, a line straight to the foot clips the railing's end, so head into the middle of the top first.
            if (inLine && along < s.Run + 1.2f && (along > s.Run - 1f || OnFlight(layout, s, at)))
                return OnFlight(layout, s, at) ? off : top - forward * .5f;
            if (along < s.Run + .3f) return Around(layout, here.Name, at, Beside(here, onto, side * Mathf.Sign(across) * (s.Width * .5f + .7f)));
            return Around(layout, here.Name, at, onto);
        }

        /// <summary>The flight something is on, if any.</summary>
        static Stairway FlightUnder(HouseLayout layout, Vector3 p)
        {
            foreach (var s in layout.Stairs)
                if (OnFlight(layout, s, p)) return s;
            return null;
        }

        /// <summary>
        /// Near enough to hit or revive isn't enough with a flight between you: whoever is on a flight can only be
        /// reached from it, or from in line with it beyond an end; the rails close off its sides.
        /// </summary>
        static bool Reachable(HouseLayout layout, Vector3 a, Vector3 b)
        {
            var onA = FlightUnder(layout, a);
            var onB = FlightUnder(layout, b);
            if (onA == onB) return true;
            return (onA == null || InLine(onA, b)) && (onB == null || InLine(onB, a));
        }

        /// <summary>On the line of a flight, from a little before its foot to a little past its top.</summary>
        static bool InLine(Stairway s, Vector3 p)
        {
            s.Along(0f, out float fx, out float fz);
            s.Along(s.Run, out float tx, out float tz);
            var forward = new Vector2(tx - fx, tz - fz) / s.Run;
            var offset = new Vector2(p.x - fx, p.z - fz);
            float along = Vector2.Dot(offset, forward), across = forward.x * offset.y - forward.y * offset.x;
            return Mathf.Abs(across) < s.Width * .5f && along > -1.5f && along < s.Run + 1.5f;
        }

        /// <summary>How far round a flight to keep (its solid side downstairs, its railed opening upstairs), and where to turn.</summary>
        const float Skirt = .4f, Rounding = .75f;

        /// <summary>
        /// The next point on the shortest way to a target in the same room that doesn't cut across a flight of stairs:
        /// straight there, or by the corners just outside the flights in this room (a handful of points).
        /// </summary>
        static Vector3 Around(HouseLayout layout, string roomName, Vector3 at, Vector3 target)
        {
            var room = layout.Room(roomName);
            var a = new Vector2(at.x, at.z);
            var b = new Vector2(target.x, target.z);
            // Each flight here with a skirt round it, or just the flight itself for one you (or the target) are already
            // inside the skirt of, as just after stepping off the end: you still go round it rather than back onto it.
            var skirts = new List<Rect>();
            var blocks = new List<Rect>();
            foreach (var s in layout.Stairs)
            {
                if (s.Lower != roomName && s.Upper != roomName) continue;
                var bare = Rect.MinMaxRect(s.MinX, s.MinZ, s.MaxX, s.MaxZ);
                if (bare.Contains(a) || bare.Contains(b)) continue;
                var skirt = Rect.MinMaxRect(s.MinX - Skirt, s.MinZ - Skirt, s.MaxX + Skirt, s.MaxZ + Skirt);
                skirts.Add(skirt);
                blocks.Add(skirt.Contains(a) || skirt.Contains(b) ? bare : skirt);
            }
            bool Clear(Vector2 p, Vector2 q)
            {
                foreach (var r in blocks) if (Crosses(p, q, r)) return false;
                return true;
            }
            if (blocks.Count == 0 || Clear(a, b)) return target;
            var nodes = new List<Vector2> { a, b };
            foreach (var r in skirts)
                foreach (var corner in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax) })
                {
                    var c = corner + new Vector2(corner.x < r.center.x ? -1f : 1f, corner.y < r.center.y ? -1f : 1f) * (Rounding - Skirt);
                    if (c.x > room.MinX + .35f && c.x < room.MaxX - .35f && c.y > room.MinZ + .35f && c.y < room.MaxZ - .35f && !blocks.Any(o => o.Contains(c)))
                        nodes.Add(c);
                }
            // Shortest way from a (node 0) to b (node 1) through corners that can see each other.
            int n = nodes.Count;
            var cost = Enumerable.Repeat(float.PositiveInfinity, n).ToArray();
            var via = Enumerable.Repeat(-1, n).ToArray();
            var done = new bool[n];
            cost[0] = 0f;
            for (int k = 0; k < n; k++)
            {
                int u = -1;
                for (int i = 0; i < n; i++) if (!done[i] && (u < 0 || cost[i] < cost[u])) u = i;
                if (u < 0 || float.IsPositiveInfinity(cost[u])) break;
                done[u] = true;
                for (int v = 0; v < n; v++)
                {
                    if (done[v] || !Clear(nodes[u], nodes[v])) continue;
                    float c = cost[u] + Vector2.Distance(nodes[u], nodes[v]);
                    if (c < cost[v]) { cost[v] = c; via[v] = u; }
                }
            }
            if (via[1] < 0) return target;
            // Head for the first corner on the way, or the one after it once you're there.
            var path = new List<int>();
            for (int i = 1; i > 0; i = via[i]) path.Add(i);
            path.Reverse();
            int next = path[0];
            if (path.Count > 1 && Vector2.Distance(a, nodes[next]) < .6f) next = path[1];
            return next == 1 ? target : new Vector3(nodes[next].x, room.FloorY, nodes[next].y);
        }

        /// <summary>True if the segment from p to q passes through the inside of the rectangle.</summary>
        static bool Crosses(Vector2 p, Vector2 q, Rect r)
        {
            float t0 = 0f, t1 = 1f;
            var d = q - p;
            bool Clip(float den, float num)
            {
                if (Mathf.Abs(den) < 1e-6f) return num > 0f;
                float t = num / den;
                if (den < 0f) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
                return true;
            }
            return Clip(-d.x, p.x - r.xMin) && Clip(d.x, r.xMax - p.x) && Clip(-d.y, p.y - r.yMin) && Clip(d.y, r.yMax - p.y) && t1 - t0 > 1e-4f;
        }

        /// <summary>
        /// Anywhere on a flight, from its foot to the lip at the top. Near the top RoomAt already says upstairs,
        /// but heading for a doorway from there runs into the rail; stepping off the end first doesn't.
        /// </summary>
        static bool OnFlight(HouseLayout layout, Stairway s, Vector3 at) =>
            s.Covers(at.x, at.z) && at.y > layout.Room(s.Lower).FloorY - .5f && at.y < layout.Room(s.Upper).FloorY + .5f;

        /// <summary>Over a flight of stairs, or within this margin of one, between its two floors (a flight a storey away is just floor here).</summary>
        static bool NearStairs(HouseLayout layout, Vector3 p, float margin)
        {
            foreach (var s in layout.Stairs)
                if (p.y > layout.Room(s.Lower).FloorY - .5f && p.y < layout.Room(s.Upper).FloorY + .5f
                    && p.x > s.MinX - margin && p.x < s.MaxX + margin && p.z > s.MinZ - margin && p.z < s.MaxZ + margin) return true;
            return false;
        }

        /// <summary>A point beside where you step on, kept inside the room.</summary>
        static Vector3 Beside(RoomBox room, Vector3 onto, Vector3 sideways)
        {
            var p = onto + sideways;
            return new Vector3(Mathf.Clamp(p.x, room.MinX + .5f, room.MaxX - .5f), room.FloorY, Mathf.Clamp(p.z, room.MinZ + .5f, room.MaxZ - .5f));
        }
    }
}
