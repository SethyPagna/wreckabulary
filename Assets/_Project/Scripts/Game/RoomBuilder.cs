using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>Builds the selected shared house layout, including open doorways, spawns and original furniture.</summary>
    [DefaultExecutionOrder(-150)]
    public class RoomBuilder : MonoBehaviour
    {
        [SerializeField] Transform furnitureRoot;
        Transform geometry;
        /// <summary>The scene's wall sign, kept on the two original houses until the walls stand tall.</summary>
        GameObject sign;
        readonly List<Smashable> originals = new();
        public HouseLayout Layout { get; private set; }
        public IReadOnlyList<Smashable> Originals => originals;

        void Awake()
        {
            Layout = GameConfig.Current.HouseFor(Session.MapId);
            var oldRoom = GameObject.Find("Room");
            if (oldRoom) oldRoom.SetActive(false);
            // The scene's wall sign floats over the two original houses; on other maps their walls cut it short.
            var found = GameObject.Find("Sign");
            if (Session.MapId is "pinwheel" or "courtyard") sign = found;
            else if (found) found.SetActive(false);
            if (furnitureRoot) furnitureRoot.gameObject.SetActive(false);
            geometry = CreateGeometry(Layout, Session.MapId, transform);
            // With an upstairs, the floors above you would hide you from the overhead camera.
            if (Layout.StoreyFloors().Count > 1) gameObject.AddComponent<StoreyCutaway>().Configure(Layout, geometry);
            var joins = GetComponent<PlayerJoinManager>();
            if (joins) joins.ConfigureLayout(Layout);
            var camera = FindAnyObjectByType<CameraRig>();
            if (camera) camera.FrameLayout(Layout);
            BuildFurniture();
        }

        public void ResetRoom(bool furnish = true)
        {
            SummonedThing.ClearAll();
            World.ClearTransient();
            if (TilePool.Instance) TilePool.Instance.ReleaseAll();
            if (furnitureRoot) { furnitureRoot.gameObject.SetActive(false); Destroy(furnitureRoot.gameObject); }
            originals.Clear();
            furnitureRoot = new GameObject("Original furniture").transform;
            furnitureRoot.SetParent(transform, false);
            if (furnish) BuildFurnitureIntoRoot();
        }

        void BuildFurniture()
        {
            if (furnitureRoot) Destroy(furnitureRoot.gameObject);
            furnitureRoot = new GameObject("Original furniture").transform;
            furnitureRoot.SetParent(transform, false);
            BuildFurnitureIntoRoot();
        }

        void BuildFurnitureIntoRoot()
        {
            foreach (var f in Layout.Furniture)
            {
                var prop = FurnitureCatalog.Spawn(f.Word, new Vector3(f.X, Layout.Room(f.Room).FloorY + f.Y, f.Z), f.Yaw, furnitureRoot);
                originals.Add(prop);
            }
        }

        /// <summary>
        /// Full-height walls for a third-person camera, low ones for the overhead view. At eye level the
        /// standing sign would block the view, so it goes while the walls are tall (web renderer.js).
        /// </summary>
        public void SetTallWalls(bool tall)
        {
            if (sign) sign.SetActive(!tall);
            SetTallWalls(geometry, tall);
        }

        public static void SetTallWalls(Transform geometry, bool tall)
        {
            if (!geometry) return;
            foreach (var wall in geometry.GetComponentsInChildren<TallWall>(true)) wall.Apply(tall);
        }

        /// <summary>The geometry's group for a storey (0 is the ground floor).</summary>
        public static string StoreyName(int storey) => "Storey " + storey;

        /// <summary>Read-only map geometry shared by matches and the creative preview; adds no furniture or director.</summary>
        public static Transform CreateGeometry(HouseLayout layout, string mapId, Transform parent, bool includeExtras = true)
        {
            var root = new GameObject(layout.Name + " geometry").transform;
            root.SetParent(parent, false);
            new GeometryFactory(layout, mapId, root, includeExtras).Build();
            return root;
        }

        sealed class GeometryFactory
        {
            readonly HouseLayout Layout;
            readonly string mapId;
            readonly Transform geometry;
            readonly bool includeExtras;
            public GeometryFactory(HouseLayout layout, string id, Transform root, bool extras)
            { Layout = layout; mapId = id; geometry = root; includeExtras = extras; }

            public void Build()
            {
                Color[] floors = { new(.76f, .62f, .45f), new(.66f, .73f, .73f), new(.83f, .74f, .59f), new(.64f, .63f, .75f), new(.73f, .55f, .47f) };
                storeyFloors = Layout.StoreyFloors();
                // One group per storey, so a cutaway can hide the floors above you and the lobby can show one.
                storeys = new Transform[storeyFloors.Count];
                for (int s = 0; s < storeys.Length; s++)
                {
                    storeys[s] = new GameObject(StoreyName(s)).transform;
                    storeys[s].SetParent(geometry, false);
                }
                var edges = new Dictionary<string, Edge>();
                for (int i = 0; i < Layout.Rooms.Count; i++)
                {
                    var r = Layout.Rooms[i];
                    int storey = Layout.StoreyOf(r);
                    bool garden = r.Name == "Garden";
                    // A floor is left open over each flight of stairs that comes up into it.
                    var holes = Layout.Stairs.Where(s => s.Upper == r.Name).Select(s => Rect.MinMaxRect(s.MinX, s.MinZ, s.MaxX, s.MaxZ));
                    foreach (var piece in Slab(r, holes))
                        Block(r.Name + " floor", storeys[storey], new Vector3(piece.center.x, r.FloorY - .12f, piece.center.y),
                            new Vector3(piece.width, .24f, piece.height), garden ? new Color(.41f, .56f, .35f) : floors[i % floors.Length]);
                    AddEdge(edges, storey, r.FloorY, true, r.MinX, r.MinZ, r.MaxZ);
                    AddEdge(edges, storey, r.FloorY, true, r.MaxX, r.MinZ, r.MaxZ);
                    AddEdge(edges, storey, r.FloorY, false, r.MinZ, r.MinX, r.MaxX);
                    AddEdge(edges, storey, r.FloorY, false, r.MaxZ, r.MinX, r.MaxX);
                    // A single imported rug per indoor room gives material detail without tiling hundreds of meshes.
                    float cx = (r.MinX + r.MaxX) * .5f, cz = (r.MinZ + r.MaxZ) * .5f;
                    bool stairsOnRug = Layout.Stairs.Any(s => (s.Lower == r.Name || s.Upper == r.Name)
                        && s.MinX < cx + .8f && s.MaxX > cx - .8f && s.MinZ < cz + .8f && s.MaxZ > cz - .8f);
                    if (includeExtras && !garden && !stairsOnRug) Decor("Environment/Arena_Rug", storeys[storey], new Vector3(cx, r.FloorY + .006f, cz), .8f);
                }
                foreach (var edge in edges.Values) BuildEdge(edge);
                foreach (var s in Layout.Stairs) BuildStairs(s);
                // One sun lights every storey; floors above you mustn't black out the rooms below.
                for (int s = 1; s < storeys.Length; s++)
                    foreach (var r in storeys[s].GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
                if (!includeExtras) return;
                // The pinwheel balcony is a reachable elevated route; the courtyard is intentionally open and flat.
                // Other maps take their shape from the data alone.
                var ground = storeys[0];
                if (mapId == "pinwheel")
                {
                    Block("Playroom balcony", ground, new Vector3(3.25f, 1.55f, 2.5f), new Vector3(1.5f, .3f, 3f), new Color(.7f, .52f, .36f));
                    var ramp = Block("Balcony ramp", ground, new Vector3(3.25f, .76f, .02f), new Vector3(1.5f, .15f, 2.7f), new Color(.65f, .47f, .33f));
                    ramp.transform.rotation = Quaternion.Euler(-40f, 0f, 0f);
                    Decor("Environment/Railing_2m", ground, new Vector3(2.5f, 1.7f, 2.5f), 1f, 90f);
                }
                else if (mapId == "courtyard")
                {
                    Block("Courtyard path east-west", ground, new Vector3(0f, .008f, 0f), new Vector3(12f, .018f, 2.5f), new Color(.75f, .72f, .59f), false);
                    Block("Courtyard path north-south", ground, new Vector3(0f, .009f, 0f), new Vector3(2.5f, .018f, 12f), new Color(.75f, .72f, .59f), false);
                    Decor("Items/PLANT", ground, new Vector3(-4.5f, 0f, 4.5f), 1.4f);
                    Decor("Items/PLANT", ground, new Vector3(4.5f, 0f, -4.5f), 1.4f);
                }
            }

            List<float> storeyFloors;
            Transform[] storeys;

            /// <summary>The room's floor as rectangles (x, z), with the stair openings cut out.</summary>
            static List<Rect> Slab(RoomBox r, IEnumerable<Rect> holes)
            {
                var pieces = new List<Rect> { Rect.MinMaxRect(r.MinX, r.MinZ, r.MaxX, r.MaxZ) };
                foreach (var hole in holes)
                {
                    var next = new List<Rect>();
                    foreach (var p in pieces)
                    {
                        if (!p.Overlaps(hole)) { next.Add(p); continue; }
                        // Strips in front of and behind the opening span the piece; the two beside it fill the rest.
                        float z0 = Mathf.Max(p.yMin, hole.yMin), z1 = Mathf.Min(p.yMax, hole.yMax);
                        next.Add(Rect.MinMaxRect(p.xMin, p.yMin, p.xMax, hole.yMin));
                        next.Add(Rect.MinMaxRect(p.xMin, hole.yMax, p.xMax, p.yMax));
                        next.Add(Rect.MinMaxRect(p.xMin, z0, hole.xMin, z1));
                        next.Add(Rect.MinMaxRect(hole.xMax, z0, p.xMax, z1));
                    }
                    pieces = next.Where(p => p.width > .01f && p.height > .01f).ToList();
                }
                return pieces;
            }

            sealed class Edge
            {
                public bool Vertical;
                public float Fixed, FloorY;
                public int Storey;
                public readonly List<Vector2> Intervals = new();
            }

            static void AddEdge(Dictionary<string, Edge> all, int storey, float floorY, bool vertical, float fixedAt, float min, float max)
            {
                string key = storey + (vertical ? "x" : "z") + fixedAt.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!all.TryGetValue(key, out var edge)) all[key] = edge = new Edge { Vertical = vertical, Fixed = fixedAt, Storey = storey, FloorY = floorY };
                edge.Intervals.Add(new Vector2(min, max));
            }

            void BuildEdge(Edge edge)
            {
                var cuts = edge.Intervals.SelectMany(i => new[] { i.x, i.y }).ToList();
                var openings = Layout.Doors.Where(d => Layout.StoreyOf(Layout.Room(d.A)) == edge.Storey && Mathf.Abs((edge.Vertical ? d.X : d.Z) - edge.Fixed) < .01f)
                    .Select(d => new Vector2((edge.Vertical ? d.Z : d.X) - d.Width * .5f, (edge.Vertical ? d.Z : d.X) + d.Width * .5f)).ToArray();
                cuts.AddRange(openings.SelectMany(i => new[] { i.x, i.y }));
                cuts = cuts.Distinct().OrderBy(v => v).ToList();
                // Cutaway rendering keeps players visible; the tall collider enforces the doorway route, stopping
                // under the floor above so it doesn't poke up through it.
                float height = edge.Storey + 1 < storeyFloors.Count ? Mathf.Min(3.3f, storeyFloors[edge.Storey + 1] - .24f - edge.FloorY) : 3.3f;
                for (int i = 0; i < cuts.Count - 1; i++)
                {
                    float mid = (cuts[i] + cuts[i + 1]) * .5f;
                    if (!edge.Intervals.Any(range => mid >= range.x && mid <= range.y) || openings.Any(range => mid > range.x && mid < range.y)) continue;
                    float length = cuts[i + 1] - cuts[i];
                    var wall = Block("Wall", storeys[edge.Storey], edge.Vertical ? new Vector3(edge.Fixed, edge.FloorY + .55f, mid) : new Vector3(mid, edge.FloorY + .55f, edge.Fixed),
                        edge.Vertical ? new Vector3(.2f, 1.1f, length) : new Vector3(length, 1.1f, .2f), new Color(.66f, .7f, .65f));
                    var tall = wall.AddComponent<TallWall>();
                    tall.FloorY = edge.FloorY;
                    tall.Height = height;
                    // Outside on either side, or a garden there: an exterior wall.
                    tall.Outside = edge.Vertical
                        ? Outdoors(edge.Storey, edge.Fixed - .1f, mid) || Outdoors(edge.Storey, edge.Fixed + .1f, mid)
                        : Outdoors(edge.Storey, mid, edge.Fixed - .1f) || Outdoors(edge.Storey, mid, edge.Fixed + .1f);
                    tall.Apply(false);
                }
            }

            bool Outdoors(int storey, float x, float z) => !Layout.Rooms.Any(r => r.Name != "Garden" && Layout.StoreyOf(r) == storey
                && x > r.MinX && x < r.MaxX && z > r.MinZ && z < r.MaxZ);

            /// <summary>
            /// A flight from the lower room to the one above: an unseen solid ramp to walk on, steps to look at,
            /// rails along both sides, and on the upper floor a railing round the opening except at the top.
            /// </summary>
            void BuildStairs(Stairway s)
            {
                RoomBox lower = Layout.Room(s.Lower), upper = Layout.Room(s.Upper);
                Transform below = storeys[Layout.StoreyOf(lower)], above = storeys[Layout.StoreyOf(upper)];
                s.Along(0f, out float fx, out float fz);
                s.Along(s.Run, out float tx, out float tz);
                var foot = new Vector3(fx, lower.FloorY, fz);
                var top = new Vector3(tx, upper.FloorY, tz);
                var slope = (top - foot).normalized;
                var flat = new Vector3(slope.x, 0f, slope.z).normalized;
                var across = Vector3.Cross(Vector3.up, flat);
                var normal = Vector3.Cross(slope, across);
                float length = Vector3.Distance(foot, top), rise = top.y - foot.y;
                var tilt = Quaternion.LookRotation(slope, normal);
                var wood = new Color(.65f, .47f, .33f);

                var ramp = Block("Stairs ramp", below, (foot + top) * .5f - normal * .075f, new Vector3(s.Width, .15f, length), wood);
                ramp.transform.rotation = tilt;
                ramp.GetComponent<Renderer>().enabled = false;

                // Each step's nose touches the ramp, so feet never sink into a tread; the last riser climbs onto
                // the floor above. The steps are solid underneath the ramp, so nobody walks under the flight,
                // their tops kept a little below it so the ramp alone is what you stand on.
                int count = Mathf.Max(1, Mathf.RoundToInt(rise / .2f));
                float riser = rise / count, tread = s.Run / count;
                for (int i = 1; i < count; i++)
                {
                    float h = i * riser;
                    var step = Block("Step", below, foot + flat * ((i + .5f) * tread) + Vector3.up * (h * .5f), new Vector3(s.Width, h, tread),
                        i % 2 == 0 ? wood : new Color(.7f, .52f, .36f));
                    step.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
                    var collider = step.GetComponent<BoxCollider>();
                    collider.size = new Vector3(1f, (h - .03f) / h, 1f);
                    collider.center = new Vector3(0f, -.015f / h, 0f);
                }

                // A rail stands square to the flight, so a full-length one leans out past the foot at head
                // height, where it catches anyone crossing in front. Starting it as far up the flight as the
                // slope's tangent (for a 1 m rail) puts its top end right over the bottom step instead.
                float start = rise / s.Run;
                var railColour = new Color(.45f, .32f, .22f);
                foreach (float side in new[] { -1f, 1f })
                {
                    var rail = Block("Stair rail", below, foot + slope * ((start + length) * .5f) + across * (side * (s.Width * .5f + .05f)) + normal * .5f,
                        new Vector3(.1f, 1f, length - start), railColour);
                    rail.transform.rotation = tilt;
                }

                // Upstairs, rails along both sides of the opening and across its foot end; the top end is the way on.
                float y = upper.FloorY + .5f;
                var middle = (new Vector3(fx, 0f, fz) + new Vector3(tx, 0f, tz)) * .5f;
                foreach (float side in new[] { -1f, 1f })
                {
                    var at = middle + across * (side * (s.Width * .5f + .05f));
                    Block("Stairwell railing", above, new Vector3(at.x, y, at.z), Scaled(flat, s.Run, .1f), railColour);
                }
                var end = new Vector3(fx, 0f, fz) - flat * .05f;
                Block("Stairwell railing", above, new Vector3(end.x, y, end.z), Scaled(across, s.Width + .2f, .1f), railColour);
            }

            /// <summary>A 1 m tall box this long along an axis-aligned direction and this thick across it.</summary>
            static Vector3 Scaled(Vector3 along, float length, float thickness) =>
                Mathf.Abs(along.x) > Mathf.Abs(along.z) ? new Vector3(length, 1f, thickness) : new Vector3(thickness, 1f, length);

            GameObject Block(string name, Transform group, Vector3 at, Vector3 scale, Color colour, bool solid = true)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(group, false);
                go.transform.position = at;
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = GameAssets.I.Tinted(colour);
                if (!solid) Object.Destroy(go.GetComponent<Collider>());
                return go;
            }

            void Decor(string key, Transform group, Vector3 at, float scale, float yaw = 0f)
            {
                var library = ModelLibrary.Load();
                if (!library || !library.Find(key)) return;
                var root = new GameObject(key).transform;
                root.SetParent(group, false);
                root.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
                root.localScale = Vector3.one * scale;
                library.Spawn(key, root);
            }
        }
    }
}
