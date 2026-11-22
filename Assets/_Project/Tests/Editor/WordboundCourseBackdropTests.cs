using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules.Surroundings;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundCourseBackdropTests
    {
        const string DataFolder = "Assets/_Project/Data/Surroundings";
        const string StyleFile = DataFolder + "/Courses/course_city.json";
        const float Slack = .02f, RealMeshSlack = .25f;

        readonly List<Object> created = new();
        readonly List<Mesh> meshes = new();
        GameObject parent;
        Material material;
        WordboundCourseWorld world;

        [SetUp]
        public void SetUp()
        {
            parent = new GameObject("BackdropFixture");
            parent.transform.SetPositionAndRotation(new Vector3(12f, 0f, -5f), Quaternion.Euler(0f, 90f, 0f));
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var catalogue = CourseTemplates.ProofCatalogue();
            var resolved = CourseResolver.Resolve(CourseTemplates.RaceCircuit(), catalogue,
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(ModeDefinition.For("TimeRun"))));
            Assert.IsTrue(resolved.Success, string.Join("; ", resolved.Issues.Select(i => i.Code + ": " + i.Message)));
            world = WordboundCourseWorld.Build(resolved.Manifest, catalogue, parent.transform,
                new WordboundCourseWorldOptions { FloorMaterial = material, WallMaterial = material, ConnectorMaterial = material });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var mesh in meshes) if (mesh) Object.DestroyImmediate(mesh);
            meshes.Clear();
            foreach (var item in created) if (item) Object.DestroyImmediate(item);
            created.Clear();
            if (parent) Object.DestroyImmediate(parent);
            if (material) Object.DestroyImmediate(material);
        }

        static string Project(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));

        TextAsset Text(string path)
        {
            var asset = new TextAsset(File.ReadAllText(Project(path))) { name = Path.GetFileNameWithoutExtension(path) };
            created.Add(asset);
            return asset;
        }

        CourseBackdropKit BoxKit()
        {
            var manifests = Directory.GetFiles(Project(DataFolder), "kit_*.json").OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => Text(DataFolder + "/" + Path.GetFileName(f))).ToList();
            var kit = ScriptableObject.CreateInstance<CourseBackdropKit>();
            created.Add(kit);
            var shader = Shader.Find(BackdropShader.ShaderName) ?? Shader.Find("Universal Render Pipeline/Lit");
            var slotMaterials = new Dictionary<string, Material>(StringComparer.Ordinal);
            var pieces = new List<CourseBackdropKit.Piece>();
            kit.Configure(manifests, Text(StyleFile), pieces, Array.Empty<CourseBackdropKit.Slot>());
            foreach (var piece in kit.Kit().Pieces)
            {
                foreach (string slot in piece.Slots)
                    if (!slotMaterials.ContainsKey(slot))
                    {
                        var slotMaterial = new Material(shader) { name = slot };
                        created.Add(slotMaterial);
                        slotMaterials.Add(slot, slotMaterial);
                    }
                pieces.Add(new CourseBackdropKit.Piece { id = piece.Id, mesh = Box(piece), slots = piece.Slots.ToArray() });
            }
            kit.Configure(manifests, Text(StyleFile), pieces, slotMaterials.Select(p => new CourseBackdropKit.Slot { id = p.Key, material = p.Value }));
            return kit;
        }

        Mesh Box(SurroundingsPiece piece)
        {
            float x = piece.Width * .5f, z = piece.Depth * .5f, y = piece.Height;
            var mesh = new Mesh { name = piece.Id };
            mesh.SetVertices(new List<Vector3>
            {
                new(-x, 0, -z), new(x, 0, -z), new(x, 0, z), new(-x, 0, z),
                new(-x, y, -z), new(x, y, -z), new(x, y, z), new(-x, y, z),
            });
            mesh.SetNormals(Enumerable.Repeat(Vector3.up, 8).ToList());
            mesh.SetColors(Enumerable.Repeat(new Color32(255, 255, 255, 0), 8).ToList());
            int[] faces = { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7 };
            mesh.subMeshCount = piece.Slots.Count;
            for (int s = 0; s < piece.Slots.Count; s++) mesh.SetTriangles(faces, s);
            created.Add(mesh);
            return mesh;
        }

        GameObject Build(CourseBackdropKit kit, bool low, out CourseSurroundingsResult layout)
        {
            var root = WordboundCourseBackdrop.Build(world, kit, low, meshes, out layout, readable: true);
            created.Add(root);
            return root;
        }

        void AssertOutsideCourse(GameObject root, CourseSurroundingsResult layout, float slack)
        {
            var footprint = CourseFootprint.Of(world.Course.Geometry.Select(m => m.Bounds));
            float reach = layout.Clear - slack, below = footprint.Floor - CourseSurroundingsStyle.MinGroundDrop + slack;
            int checkedVertices = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var local = world.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    bool outside = local.x < footprint.MinX - reach || local.x > footprint.MaxX + reach || local.z < footprint.MinZ - reach || local.z > footprint.MaxZ + reach;
                    Assert.IsTrue(outside || local.y <= below, $"{filter.name} has a vertex at {local} inside the course bounds plus {layout.Clear} m");
                    checkedVertices++;
                }
            Assert.Greater(checkedVertices, 0);
        }

        void AssertNotSimulated(GameObject root, int surfaces)
        {
            Assert.IsEmpty(root.GetComponentsInChildren<Collider>(true), "the backdrop has no colliders");
            Assert.IsEmpty(root.GetComponentsInChildren<Rigidbody>(true), "the backdrop has no bodies");
            Assert.IsEmpty(root.GetComponentsInChildren<WordboundCourseNode>(true), "the backdrop carries no course nodes");
            Assert.IsFalse(root.transform.IsChildOf(world.transform), "the backdrop stays out of the course world the host simulates");
            Assert.IsFalse(world.transform.IsChildOf(root.transform));
            Assert.IsNull(root.transform.parent);
            Assert.AreEqual(surfaces, world.Surfaces.Count);
            var renderers = new HashSet<GameObject>(root.GetComponentsInChildren<Renderer>(true).Select(r => r.gameObject));
            Assert.IsFalse(world.Surfaces.Any(c => renderers.Contains(c.gameObject)));
            Assert.AreEqual(world.gameObject.scene, root.scene);
        }

        [Test]
        public void TheHighBackdropHasNoCollidersAndStaysOutsideTheCourse()
        {
            int surfaces = world.Surfaces.Count;
            var root = Build(BoxKit(), false, out var layout);
            Assert.AreEqual(CourseSurroundingsTier.High, layout.Tier);
            AssertNotSimulated(root, surfaces);
            AssertOutsideCourse(root, layout, Slack);
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            Assert.AreEqual(layout.Groups.Count, renderers.Length, "one merged renderer per band and slot");
            Assert.LessOrEqual(renderers.Length, CourseSurroundings.HighRenderers);
            Assert.IsTrue(renderers.Any(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On));
            Assert.IsTrue(renderers.Where(r => r.transform.parent.name == nameof(SurroundingsRing.Far)).All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off));
            Assert.That(Vector3.Distance(root.transform.position, world.transform.position), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(root.transform.rotation, world.transform.rotation), Is.LessThan(.01f));
        }

        [Test]
        public void TheLowBackdropIsLighterAndCastsNoShadows()
        {
            int surfaces = world.Surfaces.Count;
            var kit = BoxKit();
            Build(kit, false, out var high);
            var root = Build(kit, true, out var low);
            AssertNotSimulated(root, surfaces);
            AssertOutsideCourse(root, low, Slack);
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            Assert.LessOrEqual(renderers.Length, CourseSurroundings.LowRenderers);
            Assert.Less(low.Placements.Count, high.Placements.Count);
            Assert.IsTrue(renderers.All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off));
        }

        [Test]
        public void TheWatcherBuildsOnceAndCleansUpWithTheCourse()
        {
            if (!WordboundCourseBackdrop.Supported) Assert.Ignore("Backdrops are off here (no graphics device or WRECKABULARY_NO_BACKDROP=1).");
            var host = new GameObject("BackdropHost");
            created.Add(host);
            var kit = BoxKit();
            var watcher = WordboundCourseBackdrop.Watch(host, () => world ? world : null, kit);
            Assert.AreSame(watcher, WordboundCourseBackdrop.Watch(host, () => world ? world : null, kit), "one watcher per host");
            Assert.IsNull(watcher.Root, "nothing is built before the first refresh");
            watcher.Refresh();
            var first = watcher.Root;
            Assert.IsNotNull(first, "the watcher builds a backdrop for a live course world");
            Assert.IsNotNull(watcher.Layout);
            watcher.Refresh();
            Assert.AreSame(first, watcher.Root, "the backdrop is built once per course world");
            Object.DestroyImmediate(world.gameObject);
            watcher.Refresh();
            Assert.IsTrue(!watcher.Root, "the backdrop goes when the course world goes");
            Assert.IsTrue(!first);
            Assert.IsNull(watcher.Layout);
        }

        [Test]
        public void TheInstalledKitCoversTheStyleAndBuildsOutsideTheCourse()
        {
            var kit = CourseBackdropKit.Load();
            Assert.IsNotNull(kit, $"No kit at Resources/{CourseBackdropKit.ResourcePath}: exec Wreckabulary.EditorTools.CourseBackdropKitBake.Run.");
            CollectionAssert.IsEmpty(kit.Gaps());
            int surfaces = world.Surfaces.Count;
            foreach (bool low in new[] { false, true })
            {
                var root = Build(kit, low, out var layout);
                AssertNotSimulated(root, surfaces);
                AssertOutsideCourse(root, layout, RealMeshSlack);
                Assert.LessOrEqual(root.GetComponentsInChildren<MeshRenderer>(true).Length, low ? CourseSurroundings.LowRenderers : CourseSurroundings.HighRenderers);
            }
        }
    }
}
