using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Wreckabulary.Art;

namespace Wreckabulary
{
    /// <summary>
    /// The lobby's own room: a half-packed moving-day living room built from the game's furniture and
    /// surfaces. The avatar stands at the local origin and the camera looks down +Z. Themes re-dress it
    /// (walls, window, light and a few props) instead of swapping in a painted backdrop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyRoom : MonoBehaviour
    {
        public const string RoomName = "Moving day";
        public const float BackWall = 3.1f, SideWall = 5.4f, Height = 3.4f;

        public readonly struct Look
        {
            public readonly Color Wall, Panel, Trim, Floor, Sky, Light, Rug;
            public Look(uint wall, uint panel, uint trim, uint floor, uint sky, uint light, uint rug)
            {
                Wall = Surfaces.Hex(wall); Panel = Surfaces.Hex(panel); Trim = Surfaces.Hex(trim); Floor = Surfaces.Hex(floor);
                Sky = Surfaces.Hex(sky); Light = Surfaces.Hex(light); Rug = Surfaces.Hex(rug);
            }
        }

        public static Look LookOf(string theme) => theme switch
        {
            "candy" => new Look(0xF7D3DC, 0x9ED8C6, 0xFFF6EE, 0xE2B48F, 0xBFE6F7, 0xFFE9F0, 0xF3A9BE),
            "lantern" => new Look(0x2E5B5A, 0xB5553B, 0xE9B867, 0x9C6B48, 0x24324F, 0xFFC98A, 0xD98B4A),
            "winter" => new Look(0xE4EBF1, 0xB8434B, 0xF7F4EE, 0xC69E7C, 0xDCE9F2, 0xE9F1FF, 0xC9D7E3),
            _ => new Look(0xF1DFBF, 0x8DB79E, 0xFFF8EA, 0xCBA37B, 0x9FD3EE, 0xFFF0D2, 0xE4C695),
        };

        static readonly string[] Themes = { "sunroom", "candy", "lantern", "winter" };

        readonly List<Renderer> walls = new(), panels = new(), trims = new(), floors = new(), skies = new();
        readonly Dictionary<string, Transform> dressings = new();
        readonly List<Light> lamps = new();
        readonly List<Transform> wreckable = new();
        Renderer rug;
        Transform sideBox;
        public IReadOnlyList<Transform> Wreckable => wreckable;
        public string Dressed { get; private set; }

        public static LobbyRoom Build(Transform parent)
        {
            var root = new GameObject("Room").transform;
            root.SetParent(parent, false);
            var room = root.gameObject.AddComponent<LobbyRoom>();
            room.Construct();
            return room;
        }

        void Construct()
        {
            var shell = Group("Shell");
            floors.Add(Slab("Floor", shell, new Vector3(0f, -.12f, -1f), new Vector3(SideWall * 2f + .4f, .24f, 12f), Surfaces.Kind.Planks));
            walls.Add(Slab("Back wall", shell, new Vector3(0f, Height * .5f, BackWall + .1f), new Vector3(SideWall * 2f + .4f, Height, .2f), Surfaces.Kind.Plaster));
            foreach (float side in new[] { -1f, 1f })
                walls.Add(Slab(side < 0 ? "Left wall" : "Right wall", shell, new Vector3(side * (SideWall + .1f), Height * .5f, -1f),
                    new Vector3(.2f, Height, 8.2f), Surfaces.Kind.Plaster));
            panels.Add(Slab("Wainscot", shell, new Vector3(0f, .44f, BackWall - .02f), new Vector3(SideWall * 2f, .88f, .04f), Surfaces.Kind.Wood));
            foreach (float side in new[] { -1f, 1f })
                panels.Add(Slab("Wainscot", shell, new Vector3(side * (SideWall - .02f), .44f, -1f), new Vector3(.04f, .88f, 8.2f), Surfaces.Kind.Wood));
            trims.Add(Slab("Dado rail", shell, new Vector3(0f, .9f, BackWall - .05f), new Vector3(SideWall * 2f, .06f, .08f), Surfaces.Kind.Wood));
            trims.Add(Slab("Skirting", shell, new Vector3(0f, .05f, BackWall - .05f), new Vector3(SideWall * 2f, .1f, .08f), Surfaces.Kind.Wood));
            trims.Add(Slab("Cornice", shell, new Vector3(0f, Height - .06f, BackWall - .05f), new Vector3(SideWall * 2f, .12f, .1f), Surfaces.Kind.Wood));
            Window(shell, new Vector3(-2.35f, 1.62f, BackWall), new Vector2(1.7f, 1.3f));
            Window(shell, new Vector3(3.55f, 1.62f, BackWall), new Vector2(1.1f, 1.3f));

            var rugRoot = Prop("Environment/Round_Rug", shell, new Vector3(0f, .006f, .1f), 0f, .8f);
            if (rugRoot) rug = rugRoot.GetComponentInChildren<Renderer>();

            var furniture = Group("Furniture");
            Prop("Items/SOFA", furniture, new Vector3(-3.55f, 0f, 1.45f), 155f);
            Prop("Items/PLANT", furniture, new Vector3(-4.65f, 0f, 2.55f), 20f, 1.15f);
            Wreck(Prop("Items/LAMP", furniture, new Vector3(-1.25f, 0f, 2.55f), 0f));
            Prop("Environment/Radiator", furniture, new Vector3(-2.35f, 0f, BackWall - .12f), 180f);
            var table = Wreck(Prop("Items/TABLE", furniture, new Vector3(2.35f, 0f, 1.05f), 8f));
            Wreck(Prop("Items/CHAIR", furniture, new Vector3(3.45f, 0f, .75f), -70f));
            Prop("Items/SHELF", furniture, new Vector3(4.55f, 0f, 2.6f), 180f);
            Wreck(Prop("Items/PLANT", furniture, new Vector3(1.05f, 0f, 2.7f), 140f, .9f));
            Wreck(Prop("Items/STOOL", furniture, new Vector3(-2.15f, 0f, -.35f), 25f));
            if (table) Prop("Items/MUG", table, new Vector3(-.2f, Top(table), .05f), 30f, .32f);

            var boxes = Group("Moving boxes");
            var bottom = Prop("Items/BOX", boxes, new Vector3(-.15f, 0f, 2.75f), 6f, 1.15f);
            if (bottom)
            {
                Label(bottom, "BOOKS", Surfaces.Hex(0x3B2A1C));
                Wreck(Prop("Items/BOX", bottom, new Vector3(.03f, Top(bottom) / 1.15f, .02f), -14f, .85f));
            }
            Wreck(Prop("Items/CRATE", boxes, new Vector3(-3.05f, 0f, 2.6f), -8f));
            sideBox = Prop("Items/BOX", boxes, new Vector3(1.75f, 0f, 2.75f), 8f, .9f);
            if (sideBox) Label(sideBox, "KITCHEN", Surfaces.Hex(0x3B2A1C));
            var front = Wreck(Prop("Items/BOX", boxes, new Vector3(-1.3f, 0f, -.95f), -12f, .75f));
            if (front) Label(front, "FRAGILE", Surfaces.Hex(0xC4513A));

            foreach (string id in Themes) dressings[id] = Group("Dressing · " + id);
            DressSunroom(dressings["sunroom"]);
            DressCandy(dressings["candy"]);
            DressLantern(dressings["lantern"]);
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        public void Dress(LobbyTheme theme)
        {
            string id = theme != null ? theme.Id : "sunroom";
            var look = LookOf(id);
            Paint(walls, Surfaces.Get(Surfaces.Kind.Plaster, look.Wall));
            Paint(panels, Surfaces.Get(Surfaces.Kind.Wood, look.Panel));
            Paint(trims, Surfaces.Get(Surfaces.Kind.Wood, look.Trim));
            Paint(floors, Surfaces.Get(Surfaces.Kind.Planks, look.Floor));
            Paint(skies, GameAssets.I.Tinted(look.Sky));
            if (rug) rug.sharedMaterial = Surfaces.Get(Surfaces.Kind.Rug, look.Rug);
            foreach (var kv in dressings) kv.Value.gameObject.SetActive(kv.Key == id);
            foreach (var lamp in lamps) if (lamp) lamp.color = look.Light;
            Dressed = id;
        }

        static void Paint(List<Renderer> renderers, Material material)
        {
            foreach (var r in renderers) if (r) r.sharedMaterial = material;
        }

        void DressSunroom(Transform group)
        {
            Prop("Items/PLANT", group, new Vector3(-1.95f, 0f, BackWall - .35f), 75f, .7f);
            Prop("Items/PLANT", group, new Vector3(3.1f, 0f, 2.75f), 200f, .8f);
            Prop("Items/BOOK", group, new Vector3(-2.9f, 1.0f, BackWall - .16f), 90f, .4f);
        }

        void DressCandy(Transform group)
        {
            var colours = new[] { 0xF27C9B, 0x7BCDB8, 0xF6C35B, 0x8FB4F0 };
            Bunting(group, -SideWall + .3f, -.9f, 1.52f, colours);
            Bunting(group, .9f, SideWall - .3f, 1.52f, colours);
            if (sideBox) Prop("Items/CAKE", group, sideBox.position + Vector3.up * Top(sideBox), 0f, .5f, true);
            var table = wreckable.Find(t => t.name == "TABLE");
            if (table) Prop("Items/SODA", group, table.position + Vector3.up * Top(table) - table.right * .35f, 20f, .4f, true);
        }

        void DressLantern(Transform group)
        {
            var colours = new[] { 0xE8693F, 0xF2B547, 0xD9483B };
            const float Line = 1.62f, Depth = BackWall - .45f;
            Piece("Lantern string", group, new Vector3(0f, Line, Depth), new Vector3(SideWall * 2f - .6f, .012f, .012f), Surfaces.Hex(0x3B2A1C));
            var xs = new[] { -4.3f, -3.4f, -2.5f, -1.6f, 1.6f, 2.5f, 3.4f, 4.3f };
            for (int i = 0; i < xs.Length; i++)
            {
                float drop = .1f + (i % 2) * .08f;
                Piece("Lantern cord", group, new Vector3(xs[i], Line - drop * .5f, Depth), new Vector3(.012f, drop, .012f), Surfaces.Hex(0x3B2A1C));
                var lantern = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lantern.name = "Paper lantern";
                Destroy(lantern.GetComponent<Collider>());
                lantern.transform.SetParent(group, false);
                lantern.transform.localPosition = new Vector3(xs[i], Line - drop - .13f, Depth);
                lantern.transform.localScale = new Vector3(.24f, .28f, .24f);
                lantern.GetComponent<Renderer>().sharedMaterial = Glow(Surfaces.Hex((uint)colours[i % colours.Length]));
                foreach (float cap in new[] { .135f, -.135f })
                    Piece("Lantern cap", group, lantern.transform.localPosition + Vector3.up * cap, new Vector3(.13f, .03f, .13f), Surfaces.Hex(0x3B2A1C));
            }
            foreach (float x in new[] { -2.6f, 2.6f })
            {
                var light = new GameObject("Lantern light").AddComponent<Light>();
                light.transform.SetParent(group, false);
                light.transform.localPosition = new Vector3(x, 1.4f, BackWall - .9f);
                light.type = LightType.Point;
                light.range = 4.5f;
                light.intensity = 2.4f;
                light.shadows = LightShadows.None;
                lamps.Add(light);
            }
        }

        static readonly Dictionary<Color, Material> glows = new();

        static Material Glow(Color colour)
        {
            if (glows.TryGetValue(colour, out var cached) && cached) return cached;
            var material = glows[colour] = new Material(GameAssets.I.Tinted(colour)) { name = "Lantern glow" };
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", colour * .45f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return material;
        }

        static Mesh pennant;

        /// <summary>A double-sided downward triangle, 1 unit wide and tall, hanging from its top edge.</summary>
        static Mesh Pennant()
        {
            if (pennant) return pennant;
            pennant = new Mesh { name = "Bunting pennant" };
            pennant.vertices = new[] { new Vector3(-.5f, 0f, 0f), new Vector3(.5f, 0f, 0f), new Vector3(0f, -1f, 0f) };
            pennant.triangles = new[] { 0, 1, 2, 0, 2, 1 };
            pennant.normals = new[] { Vector3.back, Vector3.back, Vector3.back };
            pennant.RecalculateBounds();
            return pennant;
        }

        static void Bunting(Transform group, float from, float to, float y, int[] colours)
        {
            int count = Mathf.Max(2, Mathf.RoundToInt((to - from) / .3f));
            Piece("Bunting string", group, new Vector3((from + to) * .5f, y, BackWall - .12f), new Vector3(to - from, .012f, .012f), Surfaces.Hex(0xFFF6EE));
            for (int i = 0; i < count; i++)
            {
                float x = from + (i + .5f) * (to - from) / count;
                var flag = new GameObject("Bunting flag");
                flag.transform.SetParent(group, false);
                flag.transform.localPosition = new Vector3(x, y, BackWall - .13f);
                flag.transform.localScale = new Vector3(.22f, .24f, 1f);
                flag.AddComponent<MeshFilter>().sharedMesh = Pennant();
                flag.AddComponent<MeshRenderer>().sharedMaterial = GameAssets.I.Tinted(Surfaces.Hex((uint)colours[i % colours.Length]));
            }
        }

        void Window(Transform group, Vector3 at, Vector2 size)
        {
            const float Frame = .09f;
            skies.Add(Slab("Window glass", group, at + new Vector3(0f, 0f, -.03f), new Vector3(size.x, size.y, .02f), Surfaces.Kind.Plaster));
            trims.Add(Slab("Window frame", group, at + new Vector3(0f, size.y * .5f, -.06f), new Vector3(size.x + Frame * 2f, Frame, .08f), Surfaces.Kind.Wood));
            trims.Add(Slab("Window frame", group, at + new Vector3(0f, -size.y * .5f, -.06f), new Vector3(size.x + Frame * 2f, Frame, .08f), Surfaces.Kind.Wood));
            trims.Add(Slab("Window sill", group, at + new Vector3(0f, -size.y * .5f - .05f, -.12f), new Vector3(size.x + Frame * 3f, .05f, .2f), Surfaces.Kind.Wood));
            foreach (float side in new[] { -1f, 1f })
                trims.Add(Slab("Window frame", group, at + new Vector3(side * size.x * .5f, 0f, -.06f), new Vector3(Frame, size.y, .08f), Surfaces.Kind.Wood));
            trims.Add(Slab("Window mullion", group, at + new Vector3(0f, 0f, -.05f), new Vector3(.045f, size.y, .05f), Surfaces.Kind.Wood));
            trims.Add(Slab("Window transom", group, at + new Vector3(0f, size.y * .12f, -.05f), new Vector3(size.x, .045f, .05f), Surfaces.Kind.Wood));
        }

        Transform Wreck(Transform prop)
        {
            if (prop) wreckable.Add(prop);
            return prop;
        }

        /// <summary>Writes a moving-box label in marker on the box side that faces the camera (-Z).</summary>
        static void Label(Transform box, string text, Color ink)
        {
            var renderers = box.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var local = new Bounds(box.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
            foreach (var r in renderers)
            {
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                    local.Encapsulate(box.InverseTransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1) * 2f - Vector3.one)));
            }
            var toward = box.InverseTransformDirection(Vector3.back);
            Vector3 normal = Mathf.Abs(toward.x) > Mathf.Abs(toward.z) ? new Vector3(Mathf.Sign(toward.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(toward.z));
            float half = Vector3.Dot(local.extents, new Vector3(Mathf.Abs(normal.x), 0f, Mathf.Abs(normal.z)));
            float width = Vector3.Dot(local.size, new Vector3(Mathf.Abs(normal.z), 0f, Mathf.Abs(normal.x)));
            var label = new GameObject("Label " + text).AddComponent<TMPro.TextMeshPro>();
            label.transform.SetParent(box, false);
            label.transform.localPosition = local.center + normal * (half + .004f) + Vector3.up * local.extents.y * .12f;
            label.transform.localRotation = Quaternion.LookRotation(-normal, Vector3.up) * Quaternion.Euler(0f, 0f, -4f);
            if (LobbyFonts.Display) label.font = LobbyFonts.Display;
            label.text = text;
            label.color = ink;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = .1f; label.fontSizeMax = 3f;
            label.rectTransform.sizeDelta = new Vector2(width * .62f, local.size.y * .24f);
            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        }

        public static float Top(Transform prop)
        {
            var renderers = prop.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 0f;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds.max.y - prop.position.y;
        }

        Transform Group(string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(transform, false);
            return group;
        }

        Transform Prop(string key, Transform parent, Vector3 at, float yaw, float scale = 1f, bool world = false)
        {
            var library = ModelLibrary.Load();
            if (!library || !library.Find(key)) return null;
            var root = new GameObject(key.Substring(key.IndexOf('/') + 1)).transform;
            root.SetParent(parent, false);
            if (world) root.position = at; else root.localPosition = at;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            root.localScale = Vector3.one * scale;
            var model = ModelVisual.Spawn(key, root);
            if (!model) { Destroy(root.gameObject); return null; }
            var bounds = ModelVisual.BoundsIn(root, model);
            model.transform.localPosition += Vector3.up * -bounds.min.y;
            return root;
        }

        Renderer Slab(string name, Transform group, Vector3 at, Vector3 size, Surfaces.Kind kind)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(group, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            go.GetComponent<MeshFilter>().sharedMesh = Surfaces.Box(size, Surfaces.Tile(kind), null, kind == Surfaces.Kind.Wood);
            return go.GetComponent<Renderer>();
        }

        static GameObject Piece(string name, Transform group, Vector3 at, Vector3 size, Color colour)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(group, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = GameAssets.I.Tinted(colour);
            return go;
        }

        /// <summary>A flat UI picture of a theme's room for shop cards: wall, window, wainscot and floor.</summary>
        public static RectTransform Swatch(Transform parent, string theme)
        {
            var look = LookOf(theme);
            var root = LobbyKit.Rect(parent, "Room swatch");
            void Band(string name, Vector2 min, Vector2 max, Color colour)
            {
                var band = LobbyKit.Rect(root, name).Place(min, max);
                var image = band.gameObject.AddComponent<Image>();
                image.color = colour; image.raycastTarget = false;
            }
            Band("Wall", Vector2.zero, Vector2.one, look.Wall);
            Band("Window frame", new Vector2(.14f, .42f), new Vector2(.5f, .9f), look.Trim);
            Band("Window", new Vector2(.17f, .46f), new Vector2(.47f, .86f), look.Sky);
            Band("Mullion", new Vector2(.315f, .46f), new Vector2(.325f, .86f), look.Trim);
            Band("Wainscot", new Vector2(0f, .18f), new Vector2(1f, .36f), look.Panel);
            Band("Rail", new Vector2(0f, .355f), new Vector2(1f, .38f), look.Trim);
            Band("Floor", Vector2.zero, new Vector2(1f, .18f), look.Floor);
            Band("Box", new Vector2(.62f, .12f), new Vector2(.84f, .42f), Surfaces.Hex(0xC99A62));
            Band("Box tape", new Vector2(.715f, .12f), new Vector2(.745f, .42f), Surfaces.Hex(0xEAD6A8));
            return root;
        }
    }
}
