using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;

namespace Wreckabulary.Tests
{
    public sealed class WordboundRoomArchitectureTests
    {
        GameObject parent;
        WordboundRoomStyleProfile profile;

        [SetUp]
        public void SetUp()
        {
            parent = new GameObject("Architecture contract");
            parent.transform.SetPositionAndRotation(new Vector3(11, 7, -19), Quaternion.Euler(0, 37, 0));
            profile = ScriptableObject.CreateInstance<WordboundRoomStyleProfile>();
        }
        [TearDown]
        public void TearDown()
        {
            if (parent) UnityEngine.Object.DestroyImmediate(parent);
            if (profile) UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void ExistingSavedProfileLoadsAddedControlsWithoutRewritingItsBytes()
        {
            var saved = Resources.Load<WordboundRoomStyleProfile>(WordboundRoomStyleProfile.ResourcePath);
            Assert.IsNotNull(saved);
            string path = UnityEditor.AssetDatabase.GetAssetPath(saved);
            var before = File.ReadAllBytes(path);
            saved.Validate();
            if (!File.ReadAllText(path).Contains("FrameRouteOpenings:"))
            {
                Assert.IsTrue(saved.FrameRouteOpenings);
                Assert.AreEqual(.24f, saved.DoorwayFrameWidth);
                Assert.AreEqual(512, saved.MaximumOpeningBands);
            }
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            Assert.IsFalse(UnityEditor.EditorUtility.IsDirty(saved));
        }

        [Test]
        public void FlagshipDecorationPreservesEveryColliderManifestAndRouteOpening()
        {
            profile.MaximumInsetsPerWallPiece = 2;
            profile.DoorwayFrameWidth = .4f;
            var content = Content(); var course = Resolve(content);
            var legacy = WordboundCourseWorld.Build(course, content.Geometry, parent.transform,
                new WordboundCourseWorldOptions { EnableRoomArchitecture = false });
            int callbacks = 0;
            var styled = WordboundCourseWorld.Build(course, content.Geometry, parent.transform,
                new WordboundCourseWorldOptions { RoomStyle = profile, ConfigureCollision = (_, __) => callbacks++ });
            Assert.AreEqual(legacy.ManifestHash, styled.ManifestHash);
            Assert.AreEqual(legacy.Surfaces.Count, styled.Surfaces.Count);
            Assert.AreEqual(styled.Surfaces.Count, callbacks);
            Assert.AreEqual(styled.Surfaces.Count, styled.GetComponentsInChildren<Collider>().Length);
            Assert.IsEmpty(styled.GetComponentsInChildren<Rigidbody>());
            for (int i = 0; i < legacy.Surfaces.Count; i++)
            {
                var a = (BoxCollider)legacy.Surfaces[i]; var b = (BoxCollider)styled.Surfaces[i];
                Assert.AreEqual(a.center, b.center); Assert.AreEqual(a.size, b.size);
                Assert.AreEqual(a.transform.position, b.transform.position);
                Assert.AreEqual(a.transform.rotation, b.transform.rotation);
                Assert.AreEqual(a.transform.lossyScale, b.transform.lossyScale);
                Assert.AreEqual(a.GetComponent<WordboundCourseNode>().Kind, b.GetComponent<WordboundCourseNode>().Kind);
            }
            var architecture = styled.GetComponent<WordboundRoomArchitecture>();
            Assert.Greater(architecture.InsetCount, 0); Assert.That(architecture.RenderBatchCount, Is.InRange(1, 20));
            Assert.Greater(architecture.OpeningBandCount, 0);
            var walls = styled.Surfaces.Where(c => c.GetComponent<WordboundCourseNode>().Kind == WordboundCourseNodeKind.Wall)
                .Cast<BoxCollider>().ToArray();
            foreach (var filter in styled.GetComponentsInChildren<MeshFilter>().Where(f => f.name.StartsWith("Room architecture ", StringComparison.Ordinal)))
            {
                var vertices = filter.sharedMesh.vertices; var normals = filter.sharedMesh.normals;
                var tangents = filter.sharedMesh.tangents;
                Assert.AreEqual(vertices.Length, tangents.Length);
                Assert.IsTrue(tangents.All(t => Finite(t.x) && Finite(t.y) && Finite(t.z) && Finite(t.w)));
                foreach (var vertex in vertices)
                {
                    Vector3 point = filter.transform.TransformPoint(vertex);
                    Assert.IsTrue(walls.Any(wall => Contains(wall, point)), "Decorative geometry must stay inside an existing solid wall.");
                }
                var triangles = filter.sharedMesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    float facing = Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), normals[a]);
                    Assert.That(facing, Is.GreaterThan(0), "Visible faces must have consistent winding.");
                }
            }
        }

        [Test]
        public void RouteWoodworkRetainsOriginalConnectorTextureAndCanonicalUvs()
        {
            var content = Content(); var course = Resolve(content);
            var baseline = WordboundCourseWorld.Build(course, content.Geometry, parent.transform,
                new WordboundCourseWorldOptions { EnableRoomArchitecture = false });
            var styled = WordboundCourseWorld.Build(course, content.Geometry, parent.transform,
                new WordboundCourseWorldOptions { RoomStyle = profile });
            int connectors = 0;
            for (int i = 0; i < styled.Surfaces.Count; i++)
            {
                if (styled.Surfaces[i].GetComponent<WordboundCourseNode>().Kind != WordboundCourseNodeKind.Connection) continue;
                connectors++;
                var original = baseline.Surfaces[i].GetComponent<Renderer>().sharedMaterial;
                var actual = styled.Surfaces[i].GetComponent<Renderer>().sharedMaterial;
                Assert.AreSame(original.mainTexture, actual.mainTexture);
                Assert.AreSame(original.GetTexture("_BumpMap"), actual.GetTexture("_BumpMap"));
                Assert.AreEqual(original.GetFloat("_BumpScale"), actual.GetFloat("_BumpScale"));
                Assert.That(Vector4.Distance(profile.Woodwork, actual.color), Is.LessThan(0.00001f),
                    "The shader must retain the authored wood tint within float storage precision.");
                CollectionAssert.AreEqual(baseline.Surfaces[i].GetComponent<MeshFilter>().sharedMesh.uv,
                    styled.Surfaces[i].GetComponent<MeshFilter>().sharedMesh.uv);
            }
            Assert.Greater(connectors, 0);
        }

        [Test]
        public void DoorwayBandsAreOptionalBoundedAndUseBorrowedMaterialUnchanged()
        {
            var content = Content(); var course = Resolve(content);
            profile.MaximumOpeningBands = 2;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.magenta };
            profile.WoodworkMaterial = material;
            try
            {
                var styled = WordboundCourseWorld.Build(course, content.Geometry, parent.transform,
                    new WordboundCourseWorldOptions { RoomStyle = profile });
                Assert.AreEqual(2, styled.GetComponent<WordboundRoomArchitecture>().OpeningBandCount);
                var connectors = styled.Surfaces.Where(c => c.GetComponent<WordboundCourseNode>().Kind == WordboundCourseNodeKind.Connection).ToArray();
                Assert.IsNotEmpty(connectors);
                Assert.IsTrue(connectors.All(c => c.GetComponent<Renderer>().sharedMaterial == material));
                Assert.AreEqual(Color.magenta, material.color);
                styled.Dispose(); Assert.IsTrue(material);
                profile.FrameRouteOpenings = false;
                var disabled = WordboundCourseWorld.Build(course, content.Geometry, parent.transform,
                    new WordboundCourseWorldOptions { RoomStyle = profile });
                Assert.AreEqual(0, disabled.GetComponent<WordboundRoomArchitecture>().OpeningBandCount);
                Assert.AreEqual(course.Hash, disabled.ManifestHash);
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }

        [Test]
        public void DefaultRoomsRetainPlankTexturesWithoutWindowPanels()
        {
            var content = Content(); var course = Resolve(content);
            var baseline = WordboundCourseWorld.Build(course, content.Geometry, parent.transform,
                new WordboundCourseWorldOptions { EnableRoomArchitecture = false });
            var styled = WordboundCourseWorld.Build(course, content.Geometry, parent.transform,
                new WordboundCourseWorldOptions { RoomStyle = profile });
            Assert.AreEqual(0, styled.GetComponent<WordboundRoomArchitecture>().InsetCount);
            for (int i = 0; i < styled.Surfaces.Count; i++)
            {
                if (styled.Surfaces[i].GetComponent<WordboundCourseNode>().Kind != WordboundCourseNodeKind.Floor) continue;
                var original = baseline.Surfaces[i].GetComponent<Renderer>().sharedMaterial;
                var actual = styled.Surfaces[i].GetComponent<Renderer>().sharedMaterial;
                Assert.AreSame(original.mainTexture, actual.mainTexture);
                Assert.AreSame(original.GetTexture("_BumpMap"), actual.GetTexture("_BumpMap"));
                Assert.AreEqual(original.GetFloat("_BumpScale"), actual.GetFloat("_BumpScale"));
                CollectionAssert.AreEqual(baseline.Surfaces[i].GetComponent<MeshFilter>().sharedMesh.uv,
                    styled.Surfaces[i].GetComponent<MeshFilter>().sharedMesh.uv);
            }
        }

        [Test]
        public void AssignedMaterialsAndCanonicalSurfaceUvsRemainIntact()
        {
            var content = Content(); var course = Resolve(content);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                var world = WordboundCourseWorld.Build(course, content.Geometry, parent.transform,
                    new WordboundCourseWorldOptions { RoomStyle = profile, FloorMaterial = material,
                        WallMaterial = material, ConnectorMaterial = material });
                Assert.AreEqual(0, world.GetComponent<WordboundRoomArchitecture>().RenderBatchCount);
                Assert.IsTrue(world.Surfaces.All(c => c.GetComponent<Renderer>().enabled && c.GetComponent<Renderer>().sharedMaterial == material));
                world.Dispose(); Assert.IsTrue(material); Assert.IsTrue(profile);
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
            var baseline = WordboundCourseWorld.Build(course, content.Geometry, parent.transform,
                new WordboundCourseWorldOptions { EnableRoomArchitecture = false });
            var styled = WordboundCourseWorld.Build(course, content.Geometry, parent.transform,
                new WordboundCourseWorldOptions { RoomStyle = profile });
            for (int i = 0; i < baseline.Surfaces.Count; i++)
            {
                var a = baseline.Surfaces[i].GetComponent<MeshFilter>().sharedMesh;
                var b = styled.Surfaces[i].GetComponent<MeshFilter>().sharedMesh;
                CollectionAssert.AreEqual(a.vertices, b.vertices); CollectionAssert.AreEqual(a.uv, b.uv);
            }
            var owned = styled.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh)
                .Where(m => m.name.StartsWith("Room ", StringComparison.Ordinal)).Distinct().ToArray();
            var ownedMaterials = styled.GetComponentsInChildren<Renderer>().SelectMany(renderer => renderer.sharedMaterials)
                .Where(m => m.name.StartsWith("Room ", StringComparison.Ordinal)).Distinct().ToArray();
            Assert.IsNotEmpty(owned); Assert.IsNotEmpty(ownedMaterials);
            styled.Dispose(); Assert.IsTrue(owned.All(m => !m)); Assert.IsTrue(ownedMaterials.All(m => !m));
        }

        [Test]
        public void DestroyingTheParentReleasesArchitectureMeshesAndMaterials()
        {
            var content = Content();
            var styled = WordboundCourseWorld.Build(Resolve(content), content.Geometry, parent.transform,
                new WordboundCourseWorldOptions { RoomStyle = profile });
            var meshes = styled.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh)
                .Where(m => m.name.StartsWith("Room ", StringComparison.Ordinal)).Distinct().ToArray();
            var materials = styled.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials)
                .Where(m => m.name.StartsWith("Room ", StringComparison.Ordinal)).Distinct().ToArray();
            Assert.IsNotEmpty(meshes); Assert.IsNotEmpty(materials);
            UnityEngine.Object.DestroyImmediate(parent);
            Assert.IsTrue(meshes.All(m => !m)); Assert.IsTrue(materials.All(m => !m));
            Assert.IsTrue(profile);
        }

        [Test]
        public void InspectorBoundsRejectInvalidDataAndKeepPaletteDefaultsIndependent()
        {
            JsonUtility.FromJsonOverwrite("{\"MaximumInsetsPerWallPiece\":0,\"InsetWidth\":4.6}", profile);
            Assert.IsTrue(profile.FrameRouteOpenings);
            Assert.AreEqual(.24f, profile.DoorwayFrameWidth);
            Assert.AreEqual(512, profile.MaximumOpeningBands);
            profile.DoorwayFrameWidth = float.NaN; Assert.Throws<ArgumentException>(() => profile.Validate());
            profile.DoorwayFrameWidth = .24f;
            profile.MaximumOpeningBands = 1025; Assert.Throws<ArgumentException>(() => profile.Validate());
            profile.MaximumOpeningBands = 512;
            var woodwork = profile.Woodwork; profile.Woodwork = new Color(1, 1, 1, .5f);
            Assert.Throws<ArgumentException>(() => profile.Validate()); profile.Woodwork = woodwork;
            profile.Validate(); profile.InsetWidth = float.NaN;
            Assert.Throws<ArgumentException>(() => profile.Validate()); profile.InsetWidth = 4.6f;
            profile.Palettes.Add(profile.Palettes[0]); Assert.Throws<ArgumentException>(() => profile.Validate());
            profile.Palettes = WordboundRoomStyleProfile.Defaults(); profile.Palettes[0].Wall = Color.magenta;
            Assert.AreNotEqual(Color.magenta, WordboundRoomStyleProfile.Defaults()[0].Wall);
        }

        [Test]
        public void RoomFinishesForAbsentRoomsLeaveACourseExactlyAsBefore()
        {
            var content = Content(); var course = Resolve(content);
            var baseline = WordboundCourseWorld.Build(course, content.Geometry, parent.transform,
                new WordboundCourseWorldOptions { RoomStyle = profile });
            var other = ScriptableObject.CreateInstance<WordboundRoomStyleProfile>();
            try
            {
                other.Rooms.Add(new WordboundRoomFinish { ModuleId = "AbsentYard", Ground = Art.Surfaces.Kind.Tile, GroundTile = 3, Outdoor = true });
                other.Validate();
                var styled = WordboundCourseWorld.Build(course, content.Geometry, parent.transform,
                    new WordboundCourseWorldOptions { RoomStyle = other });
                var a = baseline.GetComponent<WordboundRoomArchitecture>(); var b = styled.GetComponent<WordboundRoomArchitecture>();
                Assert.AreEqual(a.RenderBatchCount, b.RenderBatchCount);
                Assert.AreEqual(a.OpeningBandCount, b.OpeningBandCount);
                Assert.AreEqual(a.InsetCount, b.InsetCount);
                var before = ArchitectureMeshes(baseline); var after = ArchitectureMeshes(styled);
                Assert.AreEqual(before.Length, after.Length);
                for (int i = 0; i < before.Length; i++)
                {
                    CollectionAssert.AreEqual(before[i].vertices, after[i].vertices);
                    CollectionAssert.AreEqual(before[i].uv, after[i].uv);
                }
                for (int i = 0; i < baseline.Surfaces.Count; i++)
                {
                    CollectionAssert.AreEqual(baseline.Surfaces[i].GetComponent<MeshFilter>().sharedMesh.uv,
                        styled.Surfaces[i].GetComponent<MeshFilter>().sharedMesh.uv);
                    CollectionAssert.AreEqual(baseline.Surfaces[i].GetComponent<Renderer>().sharedMaterials.Select(m => m.color),
                        styled.Surfaces[i].GetComponent<Renderer>().sharedMaterials.Select(m => m.color));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(other); }
        }

        [Test]
        public void DepotRoomFinishesKeepCollisionAndOpenTheYardInsideItsWalls()
        {
            var yard = new WordboundRoomFinish { ModuleId = "Yard", Ground = Art.Surfaces.Kind.Tile, GroundTile = 3.2f, Outdoor = true,
                Palette = new WordboundRoomPalette { Floor = new Color(.74f, .72f, .67f), Wall = new Color(.6f, .57f, .52f) } };
            var dock = new WordboundRoomFinish { ModuleId = "Dock", Ground = Art.Surfaces.Kind.Tile, GroundTile = 9.6f,
                Palette = new WordboundRoomPalette { Wall = new Color(.25f, .42f, .44f) } };
            var hall = new WordboundRoomFinish { ModuleId = "Hall", Palette = new WordboundRoomPalette { Wall = new Color(.19f, .27f, .36f) } };
            profile.Rooms.AddRange(new[] { yard, dock, hall });
            profile.Validate();
            var content = WordboundParcelServiceContent.Create(ItemCatalogue.FromJson(File.ReadAllText("Assets/_Project/Data/Config/items.json")));
            var resolution = content.Resolve("0123456789abcdef", MatchStateValidation.Requirements(ModeDefinition.For("TimeRun", RaceFormat.Solo)));
            Assert.IsTrue(resolution.Success, string.Join("; ", resolution.Issues.Select(i => i.Message)));
            var course = resolution.Manifest;
            var plain = WordboundCourseWorld.Build(course, content.Geometry, parent.transform,
                new WordboundCourseWorldOptions { EnableRoomArchitecture = false });
            var styled = WordboundCourseWorld.Build(course, content.Geometry, parent.transform,
                new WordboundCourseWorldOptions { RoomStyle = profile });
            Assert.AreEqual(plain.ManifestHash, styled.ManifestHash);
            Assert.AreEqual(plain.Surfaces.Count, styled.Surfaces.Count);
            for (int i = 0; i < plain.Surfaces.Count; i++)
            {
                var a = (BoxCollider)plain.Surfaces[i]; var b = (BoxCollider)styled.Surfaces[i];
                Assert.AreEqual(a.transform.position, b.transform.position); Assert.AreEqual(a.transform.lossyScale, b.transform.lossyScale);
                Assert.AreEqual(a.center, b.center); Assert.AreEqual(a.size, b.size);
            }
            var walls = styled.Surfaces.Where(c => c.GetComponent<WordboundCourseNode>().Kind == WordboundCourseNodeKind.Wall).Cast<BoxCollider>().ToArray();
            foreach (var filter in styled.GetComponentsInChildren<MeshFilter>().Where(f => f.name.StartsWith("Room architecture ", StringComparison.Ordinal)))
            {
                var vertices = filter.sharedMesh.vertices; var normals = filter.sharedMesh.normals; var triangles = filter.sharedMesh.triangles;
                foreach (var vertex in vertices)
                    Assert.IsTrue(walls.Any(wall => Contains(wall, filter.transform.TransformPoint(vertex))), "Room finishes must stay inside existing walls.");
                for (int i = 0; i < triangles.Length; i += 3)
                    Assert.That(Vector3.Dot(Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]],
                        vertices[triangles[i + 2]] - vertices[triangles[i]]), normals[triangles[i]]), Is.GreaterThan(0));
            }
            var used = styled.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.StartsWith("Room architecture ", StringComparison.Ordinal))
                .Select(r => r.sharedMaterial).ToArray();
            Assert.IsTrue(used.Any(m => m.name == "Room Railing"), "The open yard needs a railing on its free edges.");
            Assert.IsTrue(used.Any(m => Vector4.Distance(m.color, dock.Palette.Wall) < .00001f));
            Assert.IsTrue(used.Any(m => Vector4.Distance(m.color, hall.Palette.Wall) < .00001f));
            var mezzanine = profile.ForLayer("Mezzanine");
            foreach (var floor in styled.Surfaces.Where(c => c.GetComponent<WordboundCourseNode>().Kind == WordboundCourseNodeKind.Floor))
            {
                var node = floor.GetComponent<WordboundCourseNode>();
                var material = floor.GetComponent<Renderer>().sharedMaterials[0];
                if (node.ModuleId == "Mezzanine") Assert.That(Vector4.Distance(mezzanine.Floor, material.color), Is.LessThan(.00001f));
                if (node.ModuleId != "Yard") continue;
                Assert.That(Vector4.Distance(yard.Palette.Floor, material.color), Is.LessThan(.00001f));
                var mesh = floor.GetComponent<MeshFilter>().sharedMesh; var uv = mesh.uv; var points = mesh.vertices; var up = mesh.normals;
                for (int i = 0; i < points.Length; i++)
                {
                    if (up[i].y < .9f) continue;
                    Vector3 local = styled.ToCourse(floor.transform.TransformPoint(points[i]));
                    Assert.That(Vector2.Distance(uv[i] * 3.2f, new Vector2(local.x, local.z)), Is.LessThan(.002f));
                }
            }
        }

        [Test]
        public void RoomFinishBoundsRejectInvalidEntries()
        {
            profile.Rooms.Add(new WordboundRoomFinish { ModuleId = "Yard", Outdoor = true });
            profile.Validate();
            profile.Rooms.Add(new WordboundRoomFinish { ModuleId = "Yard" });
            Assert.Throws<ArgumentException>(() => profile.Validate());
            profile.Rooms[1].ModuleId = "";
            Assert.Throws<ArgumentException>(() => profile.Validate());
            profile.Rooms[1].ModuleId = "Dock"; profile.Validate();
            profile.Rooms[1].Ground = Art.Surfaces.Kind.Rug; Assert.Throws<ArgumentException>(() => profile.Validate());
            profile.Rooms[1].Ground = Art.Surfaces.Kind.Tile; profile.Rooms[1].GroundTile = .2f;
            Assert.Throws<ArgumentException>(() => profile.Validate());
            profile.Rooms[1].GroundTile = 0; profile.Rooms[1].Railing = new Color(float.NaN, 0, 0, 1);
            Assert.Throws<ArgumentException>(() => profile.Validate());
            profile.Rooms[1].Railing = Color.black; profile.Rooms[1].BoundaryHeight = 3;
            Assert.Throws<ArgumentException>(() => profile.Validate());
            profile.Rooms[1].BoundaryHeight = .6f; profile.Rooms[1].Palette = null;
            Assert.Throws<ArgumentException>(() => profile.Validate());
            profile.Rooms.RemoveAt(1); profile.Validate();
            Assert.AreSame(profile.Rooms[0], profile.ForModule("Yard")); Assert.IsNull(profile.ForModule("Dock"));
        }

        static Mesh[] ArchitectureMeshes(WordboundCourseWorld world) => world.GetComponentsInChildren<MeshFilter>()
            .Where(f => f.name.StartsWith("Room architecture ", StringComparison.Ordinal)).Select(f => f.sharedMesh).ToArray();

        static bool Contains(BoxCollider box, Vector3 worldPoint)
        {
            Vector3 p = box.transform.InverseTransformPoint(worldPoint) - box.center, h = box.size * .5f;
            return Mathf.Abs(p.x) <= h.x + .001f && Mathf.Abs(p.y) <= h.y + .001f && Mathf.Abs(p.z) <= h.z + .001f;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static WordboundFlagshipContent Content() => WordboundFlagshipContent.Create(
            ItemCatalogue.FromJson(File.ReadAllText("Assets/_Project/Data/Config/items.json")));
        static ResolvedCourse Resolve(WordboundFlagshipContent content)
        {
            var result = content.Resolve("0123456789abcdef", MatchStateValidation.Requirements(ModeDefinition.For("TimeRun")));
            Assert.IsTrue(result.Success, string.Join("; ", result.Issues.Select(i => i.Message)));
            return result.Manifest;
        }
    }
}
