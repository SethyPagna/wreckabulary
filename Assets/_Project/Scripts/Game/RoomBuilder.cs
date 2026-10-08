using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>Builds the selected shared house layout, including open doorways, spawns and original furniture.</summary>
    [DefaultExecutionOrder(-150)]
    public class RoomBuilder : MonoBehaviour
    {
        [SerializeField] Transform furnitureRoot;
        [SerializeField] AuthoredHouse authoredHouse;
        Transform geometry;
        readonly List<Smashable> originals = new();
        public HouseLayout Layout { get; private set; }
        public IReadOnlyList<Smashable> Originals => originals;
        public AuthoredHouse AuthoredWorld => authoredHouse;

        void Awake()
        {
            Layout = GameConfig.Current.HouseFor(Session.MapId);
            var previousFurniture = furnitureRoot;
            if (FindAuthoredWorld(Session.MapId))
            {
                if (previousFurniture && previousFurniture != authoredHouse.FurnitureRoot) previousFurniture.gameObject.SetActive(false);
                authoredHouse.gameObject.SetActive(true);
                authoredHouse.PrepareForPlay();
                geometry = authoredHouse.GeometryRoot;
                furnitureRoot = authoredHouse.FurnitureRoot;
                CollectOriginals();
            }
            else
            {
                if (furnitureRoot) furnitureRoot.gameObject.SetActive(false);
                geometry = new GameObject(Layout.Name).transform;
                geometry.SetParent(transform, false);
                BuildGeometry(Session.MapId);
                HousePresentation.Apply(geometry.gameObject);
                BuildFurniture();
            }
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if (root.name == "Room" && (!authoredHouse || !root.GetComponentInChildren<AuthoredHouse>(true))) root.SetActive(false);
            var joins = GetComponent<PlayerJoinManager>();
            if (joins) joins.ConfigureLayout(Layout);
            var camera = FindAnyObjectByType<CameraRig>();
            if (camera) camera.FrameLayout(Layout);
        }

        bool FindAuthoredWorld(string mapId)
        {
            if (authoredHouse && !authoredHouse.Matches(mapId)) authoredHouse.gameObject.SetActive(false);
            AuthoredHouse selected = authoredHouse && authoredHouse.Matches(mapId) ? authoredHouse : null;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<AuthoredHouse>(true))
                {
                    if (!selected && candidate.Matches(mapId)) selected = candidate;
                    if (candidate != selected) candidate.gameObject.SetActive(false);
                }
            if (!selected)
            {
                string path = AuthoredHouse.ResourcePath(mapId);
                var prefab = string.IsNullOrEmpty(path) ? null : Resources.Load<GameObject>(path);
                if (prefab) selected = Instantiate(prefab, transform).GetComponent<AuthoredHouse>();
            }
            authoredHouse = selected;
            return authoredHouse;
        }

        public void SetAuthoredWorld(AuthoredHouse world)
        {
            authoredHouse = world;
            if (!world) return;
            geometry = world.GeometryRoot;
            furnitureRoot = world.FurnitureRoot;
        }

        void CollectOriginals()
        {
            originals.Clear();
            if (furnitureRoot) originals.AddRange(furnitureRoot.GetComponentsInChildren<Smashable>());
        }

        /// <summary>Used only by the one-time editor bake; saved worlds never regenerate their authored content.</summary>
        public AuthoredHouse BuildForAuthoring(HouseLayout layout, string mapId)
        {
            Layout = layout;
            geometry = new GameObject("Geometry").transform;
            geometry.SetParent(transform, false);
            BuildGeometry(mapId);
            HousePresentation.Apply(geometry.gameObject);
            BuildFurniture();
            var world = gameObject.AddComponent<AuthoredHouse>();
            world.Configure(mapId, geometry, furnitureRoot);
            return world;
        }

        public void ResetRoom(bool furnish = true)
        {
            SummonedThing.ClearAll();
            World.ClearTransient();
            if (TilePool.Instance) TilePool.Instance.ReleaseAll();
            if (authoredHouse)
            {
                furnitureRoot = authoredHouse.ResetFurniture(furnish);
                CollectOriginals();
                return;
            }
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

        void BuildGeometry(string mapId)
        {
            var edges = new Dictionary<string, Edge>();
            for (int i = 0; i < Layout.Rooms.Count; i++)
            {
                var r = Layout.Rooms[i];
                bool garden = r.Name == "Garden";
                Block(r.Name + " floor", new Vector3((r.MinX + r.MaxX) * .5f, r.FloorY - .12f, (r.MinZ + r.MaxZ) * .5f),
                    new Vector3(r.MaxX - r.MinX, .24f, r.MaxZ - r.MinZ), garden ? new Color(.42f, .63f, .36f) : HousePresentation.FloorColor(i));
                AddEdge(edges, true, r.MinX, r.MinZ, r.MaxZ);
                AddEdge(edges, true, r.MaxX, r.MinZ, r.MaxZ);
                AddEdge(edges, false, r.MinZ, r.MinX, r.MaxX);
                AddEdge(edges, false, r.MaxZ, r.MinX, r.MaxX);
                // A single imported rug per indoor room gives material detail without tiling hundreds of meshes.
                if (!garden) Decor("Environment/Arena_Rug", new Vector3((r.MinX + r.MaxX) * .5f, r.FloorY + .006f, (r.MinZ + r.MaxZ) * .5f), .8f);
            }
            foreach (var edge in edges.Values) BuildEdge(edge);
            // The pinwheel balcony is a reachable elevated route; the courtyard is intentionally open and flat.
            if (mapId == "pinwheel")
            {
                Block("Playroom balcony", new Vector3(3.25f, 1.55f, 2.5f), new Vector3(1.5f, .3f, 3f), new Color(.7f, .52f, .36f));
                var ramp = Block("Balcony ramp", new Vector3(3.25f, .76f, .02f), new Vector3(1.5f, .15f, 2.7f), new Color(.65f, .47f, .33f));
                ramp.transform.rotation = Quaternion.Euler(-40f, 0f, 0f);
                Decor("Environment/Railing_2m", new Vector3(2.5f, 1.7f, 2.5f), 1f, 90f);
            }
            else
            {
                Block("Courtyard path east-west", new Vector3(0f, .008f, 0f), new Vector3(12f, .018f, 2.5f), new Color(.75f, .72f, .59f), false);
                Block("Courtyard path north-south", new Vector3(0f, .009f, 0f), new Vector3(2.5f, .018f, 12f), new Color(.75f, .72f, .59f), false);
                Decor("Items/PLANT", new Vector3(-4.5f, 0f, 4.5f), 1.4f);
                Decor("Items/PLANT", new Vector3(4.5f, 0f, -4.5f), 1.4f);
            }
        }

        sealed class Edge
        {
            public bool Vertical;
            public float Fixed;
            public readonly List<Vector2> Intervals = new();
        }

        static void AddEdge(Dictionary<string, Edge> all, bool vertical, float fixedAt, float min, float max)
        {
            string key = (vertical ? "x" : "z") + fixedAt.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!all.TryGetValue(key, out var edge)) all[key] = edge = new Edge { Vertical = vertical, Fixed = fixedAt };
            edge.Intervals.Add(new Vector2(min, max));
        }

        void BuildEdge(Edge edge)
        {
            var cuts = edge.Intervals.SelectMany(i => new[] { i.x, i.y }).ToList();
            var openings = Layout.Doors.Where(d => Mathf.Abs((edge.Vertical ? d.X : d.Z) - edge.Fixed) < .01f)
                .Select(d => new Vector2((edge.Vertical ? d.Z : d.X) - d.Width * .5f, (edge.Vertical ? d.Z : d.X) + d.Width * .5f)).ToArray();
            cuts.AddRange(openings.SelectMany(i => new[] { i.x, i.y }));
            cuts = cuts.Distinct().OrderBy(v => v).ToList();
            for (int i = 0; i < cuts.Count - 1; i++)
            {
                float mid = (cuts[i] + cuts[i + 1]) * .5f;
                if (!edge.Intervals.Any(range => mid >= range.x && mid <= range.y) || openings.Any(range => mid > range.x && mid < range.y)) continue;
                float length = cuts[i + 1] - cuts[i];
                var wall = Block("Wall", edge.Vertical ? new Vector3(edge.Fixed, .55f, mid) : new Vector3(mid, .55f, edge.Fixed),
                    edge.Vertical ? new Vector3(.2f, 1.1f, length) : new Vector3(length, 1.1f, .2f), new Color(.66f, .7f, .65f));
                // Cutaway rendering keeps players visible; the full-height collider enforces the doorway route.
                var collider = wall.GetComponent<BoxCollider>();
                collider.size = new Vector3(1f, 3f, 1f);
                collider.center = new Vector3(0f, 1f, 0f);
                var capSize = edge.Vertical ? new Vector3(.24f, .055f, length) : new Vector3(length, .055f, .24f);
                Block("Cream wall cap", wall.transform.position + Vector3.up * .565f, capSize, new Color(.96f, .84f, .63f), false);
                var trimSize = edge.Vertical ? new Vector3(.215f, .105f, length) : new Vector3(length, .105f, .215f);
                Block("Terracotta skirting", wall.transform.position + Vector3.down * .49f, trimSize, new Color(.65f, .35f, .23f), false);
            }
        }

        GameObject Block(string name, Vector3 at, Vector3 scale, Color colour, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(geometry, false);
            go.transform.position = at;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = GameAssets.I.Tinted(colour);
            if (!solid)
            {
                var collider = go.GetComponent<Collider>();
                collider.enabled = false;
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }
            return go;
        }

        void Decor(string key, Vector3 at, float scale, float yaw = 0f)
        {
            var library = ModelLibrary.Load();
            if (!library || !library.Find(key)) return;
            var root = new GameObject(key).transform;
            root.SetParent(geometry, false);
            root.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            root.localScale = Vector3.one * scale;
            library.Spawn(key, root);
        }
    }
}
