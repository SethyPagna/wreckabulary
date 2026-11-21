using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Wreckabulary.WordboundRuntime;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class MapSnapshotCapture
    {
        public const string Folder = "Assets/_Project/Resources/MapSnapshots";
        public const string SheetPath = "BuildNotes/w20d/maps-sheet.png";
        public const int Width = 1280, Height = 720;
        const int Supersample = 2, MaximumShadowDistance = 400;
        const float Pitch = 34f, Tilt = 28f, FieldOfView = 28f, FillAcross = .93f, FillAbove = .93f, FillBelow = .84f;
        const float MinimumNear = .3f, NearSlack = 4f, FarSlack = 50f, ShadowReach = 10f;
        const float SunPitch = 50f, SunTurn = 18f, SunIntensity = .55f, FillPitch = 30f, FillTurn = 70f, FillIntensity = .16f, ShadowStrength = .55f;
        const float WallProbe = .45f, WallReach = .45f, WallSlack = .2f, LowSlack = .05f, YardMargin = 1.5f, Oversize = 8f, TallestPart = 9f;
        const float ShownHeight = 2.2f, LevelStep = .5f, CourseMargin = 1f;
        const float MinCover = .004f, SolidCover = .95f, WhitePoint = .98f, TargetWhite = .92f, MinGain = .85f, MaxGain = 1.5f, Contrast = 1.05f, Saturation = 1.08f;
        const float PaperShadow = .7f, PaperShadowDepth = .5f, EmptyShare = .02f, FullShare = .995f;
        const int ShadowBlur = 22, ShadowPasses = 2, ShadowDrop = 12;
        const int SheetColumns = 4, CellWidth = 480, CellHeight = 270, SheetGap = 8, SmallColumns = 8, SmallWidth = 236, SmallHeight = 133, LabelScale = 2, LabelRoom = 16;
        const string OnlineEditor = ", Wreckabulary.Online.Authoring.Editor";
        const string StageType = "Wreckabulary.Online.Editor.WordboundSoloPreviewStage" + OnlineEditor;
        const string SettingsType = "Wreckabulary.Online.Editor.WordboundSoloPreviewSettings" + OnlineEditor;
        static readonly Color SunColour = LobbyKit.Hex(0xfff3dc), FillColour = LobbyKit.Hex(0xfff3e6), Ambient = new Color(.42f, .4f, .37f);
        static readonly Color PaperCentre = LobbyKit.Hex(0xf8eccf), PaperEdge = Color.Lerp(LobbyStage.SkyTop, LobbyParcel.Kraft, .18f);
        static readonly Color PaperShade = Color.Lerp(Color.white, LobbyParcel.Ink, PaperShadowDepth);
        static readonly string[] Courses = { WordboundSoloEntry.ParcelServiceMap, WordboundSoloEntry.LostAddressMap, WordboundSoloEntry.ParcelHallMap };

        sealed class Shot { public string id, path, source, note; public int bytes; public Color32[] pixels; public byte[] previous; }

        sealed class Partition { public Bounds Box; public float Top; public int Storey; public bool AlongX, Lowered; }

        readonly struct View
        {
            public readonly float Yaw, SunYaw, FillYaw;
            public View(float facing, int side) { Yaw = facing + side * Tilt; SunYaw = facing + side * SunTurn; FillYaw = facing + side * FillTurn; }
            public Vector3 Forward => Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward;
            public Quaternion Rotation => Quaternion.Euler(Pitch, Yaw, 0f);
        }

        [MenuItem("Wreckabulary/Art/Capture map snapshots")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Capture map snapshots in Edit Mode.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Map snapshots need a graphics device; do not run with -nographics.");
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset pipeline) throw new InvalidOperationException("Map snapshots expect the game's URP pipeline asset to be active.");
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            if (!data) throw new InvalidOperationException("Resources/GameData is missing; run Wreckabulary > Data > Set Up Game Data.");
            var houses = data.Parse().Houses;
            Log("start: " + houses.Count + " houses, " + Courses.Length + " courses -> " + Folder + " (" + Width + "x" + Height + ")");

            string scenes = SceneState(), global = Global();
            var selection = Selection.objects; var selected = Selection.activeObject;
            var target = RenderTexture.active; bool srgb = GL.sRGBWrite, async = EditorSettings.asyncShaderCompilation, ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
            float shadowDistance = pipeline.shadowDistance;
            int tactile = TactileMaterials.RegisteredScopeCount;
            var shots = new List<Shot>(); var skipped = new List<string>(); var errors = new List<Exception>();
            Exception failure = null;
            try
            {
                EditorSettings.asyncShaderCompilation = false;
                EnsureFolder();
                foreach (var house in houses.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    Attempt(house.Key, () => House(house.Key, house.Value, pipeline), shots, skipped);
                foreach (string course in Courses) Attempt(course, () => Course(course, pipeline), shots, skipped);
            }
            catch (Exception error) { failure = error; }
            finally
            {
                Clean(() => pipeline.shadowDistance = shadowDistance, errors);
                Clean(() => EditorSettings.asyncShaderCompilation = async, errors);
                Clean(() => RenderTexture.active = target, errors); Clean(() => GL.sRGBWrite = srgb, errors); Clean(() => SetSsao(ssao), errors);
                Clean(() => { Selection.objects = selection; Selection.activeObject = selected; }, errors);
            }
            if (failure == null) Clean(() => Import(shots), errors);
            string sheet = WriteSheet(shots);
            bool scenesKept = SceneState() == scenes, globalsKept = Global() == global, tactileKept = TactileMaterials.RegisteredScopeCount == tactile;
            foreach (var shot in shots) Log("wrote " + shot.path + " (" + shot.bytes + " bytes, " + shot.source + (shot.note == null ? "" : ", " + shot.note) + ")");
            foreach (string reason in skipped) Debug.LogWarning("[Wreckabulary] Map snapshot skipped " + reason);
            string summary = "[Wreckabulary] Map snapshots " + (failure == null && errors.Count == 0 && scenesKept && globalsKept && tactileKept ? "DONE" : "FAIL") +
                ": written=" + shots.Count + " skipped=" + skipped.Count + " scenesPreserved=" + scenesKept + " globalsPreserved=" + globalsKept +
                " tactileReleased=" + tactileKept + " folder=" + Folder + " sheet=" + sheet;
            if (failure != null) throw new InvalidOperationException(summary, failure);
            if (errors.Count > 0) throw new AggregateException(summary, errors);
            if (!scenesKept || !globalsKept || !tactileKept) throw new InvalidOperationException(summary + "\nscenes before: " + scenes + "\nscenes after: " + SceneState() + "\nglobals before: " + global + "\nglobals after: " + Global());
            if (skipped.Count > 0) Debug.LogWarning(summary); else Debug.Log(summary);
        }

        static void Attempt(string id, Func<(Color32[] pixels, string source, string note)> render, List<Shot> shots, List<string> skipped)
        {
            try
            {
                var (pixels, source, note) = render();
                byte[] png = Encode(pixels);
                string path = Folder + "/" + id + ".png", file = Full(path);
                byte[] previous = File.Exists(file) ? File.ReadAllBytes(file) : null;
                File.WriteAllBytes(file, png);
                shots.Add(new Shot { id = id, path = path, source = source, note = note, bytes = png.Length, pixels = pixels, previous = previous });
            }
            catch (Exception error) when (error is not (IOException or UnauthorizedAccessException))
            {
                var cause = error is TargetInvocationException { InnerException: { } inner } ? inner : error;
                skipped.Add(id + ": " + cause.GetType().Name + ": " + cause.Message);
            }
        }

        static (Color32[] pixels, string source, string note) House(string id, HouseLayout layout, UniversalRenderPipelineAsset pipeline)
        {
            PreviewRenderUtility preview = null; GameObject anchor = null; Scene scene = default;
            try
            {
                preview = new PreviewRenderUtility(); scene = preview.camera.gameObject.scene;
                if (!EditorSceneManager.IsPreviewScene(scene)) throw new InvalidOperationException("Expected an owned preview scene.");
                anchor = EditorUtility.CreateGameObjectWithHideFlags("Map snapshot " + id, HideFlags.HideAndDontSave);
                SceneManager.MoveGameObjectToScene(anchor, scene);
                var (geometry, source) = Build(id, layout, anchor.transform);
                if (anchor.scene != scene || anchor.GetComponentsInChildren<Transform>(true).Any(part => part.gameObject.scene != scene))
                    throw new InvalidOperationException("The house did not stay inside the owned preview scene.");
                foreach (var part in anchor.GetComponentsInChildren<Transform>(true)) part.gameObject.hideFlags = HideFlags.HideAndDontSave;
                var floors = layout.StoreyFloors();
                int focus = FocusStorey(layout);
                var shown = layout.Rooms.Where(room => layout.StoreyOf(room) <= focus).ToList();
                if (shown.Count == 0) shown = layout.Rooms.ToList();
                if (shown.Count == 0) throw new InvalidOperationException("The house has no rooms to frame.");
                var footprint = Footprint(shown);
                var view = Choose(footprint, forward => shown.Sum(room => (room.Outdoor ? -1f : 1f) * room.Area * Vector3.Dot(Centre(room) - footprint.center, forward)));
                var walls = SetWalls(geometry, layout, floors, focus, view.Forward);
                var yard = Footprint(layout.Rooms);
                yard.Expand(new Vector3(YardMargin, 0f, YardMargin) * 2f);
                CutAway(anchor.transform, geometry, floors, focus, walls, yard);
                var points = Silhouette(anchor.transform, null);
                foreach (var room in shown) AddRectangle(points, room.MinX, room.MaxX, room.MinZ, room.MaxZ, room.FloorY);
                foreach (var wall in walls) AddRectangle(points, wall.Box.min.x, wall.Box.max.x, wall.Box.min.z, wall.Box.max.z, wall.Top);
                var (pixels, framing) = Shoot(preview, points, view, pipeline);
                string storey = floors.Count > 1 ? layout.StoreyLabel(focus).ToLowerInvariant() + " shown, upper floors cut away" : "single floor";
                return (pixels, source, storey + ", " + walls.Count(wall => wall.Lowered) + " of " + walls.Count + " walls low, " + framing);
            }
            finally
            {
                var errors = new List<Exception>();
                Clean(() => { if (anchor) Object.DestroyImmediate(anchor); }, errors);
                Clean(() => preview?.Cleanup(), errors);
                Clean(() => { if (scene.IsValid() && scene.isLoaded && EditorSceneManager.IsPreviewScene(scene)) EditorSceneManager.ClosePreviewScene(scene); }, errors);
                if (errors.Count > 0) throw new AggregateException("Map snapshot cleanup failed: " + id, errors);
            }
        }

        static (Transform geometry, string source) Build(string id, HouseLayout layout, Transform anchor)
        {
            string path = AuthoredHouse.ResourcePath(id);
            var prefab = string.IsNullOrEmpty(path) ? null : Resources.Load<GameObject>(path);
            var authored = prefab ? prefab.GetComponent<AuthoredHouse>() : null;
            if (authored && authored.IsCurrent && authored.Matches(id))
            {
                var copy = Object.Instantiate(prefab, anchor, false).GetComponent<AuthoredHouse>();
                return (copy.GeometryRoot, "furnished prefab Resources/" + path);
            }
            var geometry = RoomBuilder.CreateGeometry(layout, id, anchor);
            RoomBuilder.SetTallWalls(geometry, true);
            var furniture = EditorUtility.CreateGameObjectWithHideFlags("Original furniture", HideFlags.HideAndDontSave).transform;
            furniture.SetParent(anchor, false);
            foreach (var piece in layout.Furniture)
            {
                var room = layout.Room(piece.Room);
                FurnitureCatalog.Spawn(piece.Word, new Vector3(piece.X, (room?.FloorY ?? 0f) + piece.Y, piece.Z), piece.Yaw, furniture);
            }
            return (geometry, "built from house_" + id + ".json (no current authored prefab)");
        }

        static int FocusStorey(HouseLayout layout) => layout.Spawns.Select(spawn => layout.Room(spawn.Room)).Where(room => room != null)
            .GroupBy(layout.StoreyOf).OrderByDescending(group => group.Count()).ThenBy(group => group.Key).Select(group => group.Key).DefaultIfEmpty(0).First();

        static Bounds Footprint(IReadOnlyList<RoomBox> rooms)
        {
            var box = new Bounds();
            box.SetMinMax(new Vector3(rooms.Min(r => r.MinX), rooms.Min(r => r.FloorY), rooms.Min(r => r.MinZ)),
                new Vector3(rooms.Max(r => r.MaxX), rooms.Max(r => r.FloorY), rooms.Max(r => r.MaxZ)));
            return box;
        }

        static Vector3 Centre(RoomBox room) => new Vector3((room.MinX + room.MaxX) * .5f, room.FloorY, (room.MinZ + room.MaxZ) * .5f);

        static View Choose(Bounds footprint, Func<Vector3, float> score)
        {
            float facing = footprint.size.x >= footprint.size.z ? 0f : 90f;
            var candidates = new[] { new View(facing, 1), new View(facing, -1), new View(facing + 180f, 1), new View(facing + 180f, -1) };
            return candidates.OrderByDescending(view => score(view.Forward)).First();
        }

        static List<Partition> SetWalls(Transform geometry, HouseLayout layout, List<float> floors, int focus, Vector3 forward)
        {
            var walls = new List<Partition>();
            if (!geometry) return walls;
            foreach (var wall in geometry.GetComponentsInChildren<TallWall>(true))
            {
                int storey = StoreyAt(floors, wall.FloorY);
                if (storey > focus || !wall.TryGetComponent<Renderer>(out var renderer) || !wall.TryGetComponent<BoxCollider>(out _)) continue;
                var box = renderer.bounds;
                bool alongX = box.size.x >= box.size.z;
                var nearSide = NearSide(box, alongX, forward);
                bool tall = layout.Rooms.Any(room => !room.Outdoor && layout.StoreyOf(room) == storey && room.Contains(nearSide.x, nearSide.z));
                wall.Apply(tall);
                float rise = wall.VisualHeight(tall);
                walls.Add(new Partition { Box = box, Top = wall.FloorY + rise, Storey = storey, AlongX = alongX, Lowered = rise <= TallWall.Low });
            }
            return walls;
        }

        static Vector3 NearSide(Bounds box, bool alongX, Vector3 forward)
        {
            var away = alongX ? Vector3.forward : Vector3.right;
            if (Vector3.Dot(away, forward) < 0f) away = -away;
            return box.center - away * ((alongX ? box.extents.z : box.extents.x) + WallProbe);
        }

        static void CutAway(Transform root, Transform geometry, List<float> floors, int focus, List<Partition> walls, Bounds yard)
        {
            var settled = new HashSet<Renderer>();
            foreach (var surface in root.GetComponentsInChildren<CutawaySurface>(true))
                foreach (var renderer in surface.Renderers)
                {
                    if (!renderer || !settled.Add(renderer)) continue;
                    bool onWall = surface.Kind == CutawayKind.UpperWall;
                    bool open = surface.OwnerStorey > focus || !onWall && surface.OwnerStorey == focus;
                    if (open || onWall && AboveWall(renderer.bounds, surface.OwnerStorey, walls, floors)) renderer.forceRenderingOff = true;
                }
            var fixedParts = new HashSet<Renderer>();
            for (int storey = 0; geometry && storey < floors.Count; storey++)
            {
                var group = geometry.Find(RoomBuilder.StoreyName(storey));
                if (!group) continue;
                foreach (var renderer in group.GetComponentsInChildren<Renderer>(true))
                {
                    fixedParts.Add(renderer);
                    if (storey > focus && settled.Add(renderer)) renderer.forceRenderingOff = true;
                }
            }
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var box = renderer.bounds;
                bool above = !fixedParts.Contains(renderer) && !settled.Contains(renderer) && StoreyAt(floors, box.min.y) > focus;
                if (above || Outlying(box, yard)) renderer.forceRenderingOff = true;
            }
        }

        static bool AboveWall(Bounds detail, int storey, List<Partition> walls, List<float> floors)
        {
            float top = float.MaxValue; bool lowered = true;
            foreach (var wall in walls)
            {
                if (wall.Storey != storey) continue;
                float from = wall.AlongX ? wall.Box.min.x : wall.Box.min.z, to = wall.AlongX ? wall.Box.max.x : wall.Box.max.z;
                float low = wall.AlongX ? detail.min.x : detail.min.z, high = wall.AlongX ? detail.max.x : detail.max.z;
                float gap = wall.AlongX ? Mathf.Abs(detail.center.z - wall.Box.center.z) : Mathf.Abs(detail.center.x - wall.Box.center.x);
                float reach = (wall.AlongX ? wall.Box.extents.z : wall.Box.extents.x) + WallReach;
                if (low > to || high < from || gap > reach || wall.Top >= top) continue;
                top = wall.Top; lowered = wall.Lowered;
            }
            if (top == float.MaxValue) top = (storey >= 0 && storey < floors.Count ? floors[storey] : 0f) + TallWall.Low;
            return detail.max.y > top + (lowered ? LowSlack : WallSlack);
        }

        static bool Outlying(Bounds box, Bounds yard) =>
            box.size.x > yard.size.x + Oversize || box.size.z > yard.size.z + Oversize ||
            box.center.x < yard.min.x || box.center.x > yard.max.x || box.center.z < yard.min.z || box.center.z > yard.max.z;

        static int StoreyAt(List<float> floors, float y)
        {
            int storey = 0;
            for (int i = 1; i < floors.Count; i++) if (floors[i] <= y + HouseLayout.StandingSlack) storey = i;
            return storey;
        }

        static List<Vector3> Silhouette(Transform root, Bounds? region)
        {
            var points = new List<Vector3>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(false))
            {
                if (renderer is not (MeshRenderer or SkinnedMeshRenderer) || !renderer.enabled || renderer.forceRenderingOff
                    || renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                var box = renderer.bounds;
                if (box.size.y > TallestPart) continue;
                if (region is { } area && (box.max.x < area.min.x || box.min.x > area.max.x || box.max.z < area.min.z || box.min.z > area.max.z
                    || box.max.y < area.min.y - LevelStep || box.size.x > area.size.x + Oversize || box.size.z > area.size.z + Oversize)) continue;
                points.AddRange(Corners(box));
            }
            return points;
        }

        static void AddRectangle(List<Vector3> points, float minX, float maxX, float minZ, float maxZ, float y)
        {
            points.Add(new Vector3(minX, y, minZ)); points.Add(new Vector3(maxX, y, minZ));
            points.Add(new Vector3(minX, y, maxZ)); points.Add(new Vector3(maxX, y, maxZ));
        }

        static (Color32[] pixels, string framing) Shoot(PreviewRenderUtility preview, List<Vector3> points, View view, UniversalRenderPipelineAsset pipeline)
        {
            if (points.Count == 0) throw new InvalidOperationException("Nothing visible to frame.");
            var rotation = view.Rotation;
            var (position, nearest, farthest) = Fit(points, rotation, FieldOfView, Width / (float)Height);
            pipeline.shadowDistance = Mathf.Min(MaximumShadowDistance, Mathf.Max(pipeline.shadowDistance, farthest + ShadowReach));
            var camera = preview.camera;
            camera.orthographic = false; camera.fieldOfView = FieldOfView; camera.aspect = Width / (float)Height;
            camera.nearClipPlane = Mathf.Max(MinimumNear, nearest - NearSlack); camera.farClipPlane = farthest + FarSlack; camera.cullingMask = ~0;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.allowHDR = false; camera.allowMSAA = true;
            camera.transform.SetPositionAndRotation(position, rotation);
            Illuminate(preview, view);
            bool outlined = Compose(Render(preview, Color.white), Render(preview, Color.black), out var pixels, out float cover, out float gain);
            if (!outlined) pixels = Render(preview, PaperCentre);
            RequireVaried(pixels);
            return (pixels, "yaw " + view.Yaw.ToString("F0") + ", camera " + nearest.ToString("F1") + " to " + farthest.ToString("F1") + " m, "
                + (outlined ? "fills " + Mathf.RoundToInt(cover * 100f) + "% of the frame, gain " + gain.ToString("F2")
                    : "NO OUTLINE (" + Mathf.RoundToInt(cover * 100f) + "% covered), plain paper background without shadow or grade"));
        }

        static (Vector3 position, float nearest, float farthest) Fit(List<Vector3> points, Quaternion rotation, float fieldOfView, float aspect)
        {
            var right = rotation * Vector3.right; var up = rotation * Vector3.up; var forward = rotation * Vector3.forward;
            float tan = Mathf.Tan(fieldOfView * .5f * Mathf.Deg2Rad), across = tan * aspect * FillAcross, above = tan * FillAbove, below = tan * FillBelow;
            float leftLimit = float.MaxValue, rightLimit = float.MinValue, lowLimit = float.MaxValue, highLimit = float.MinValue;
            float front = float.MaxValue, back = float.MinValue;
            foreach (var point in points)
            {
                float x = Vector3.Dot(point, right), y = Vector3.Dot(point, up), z = Vector3.Dot(point, forward);
                leftLimit = Mathf.Min(leftLimit, x + across * z); rightLimit = Mathf.Max(rightLimit, x - across * z);
                lowLimit = Mathf.Min(lowLimit, y + below * z); highLimit = Mathf.Max(highLimit, y - above * z);
                front = Mathf.Min(front, z); back = Mathf.Max(back, z);
            }
            float depth = Mathf.Min((leftLimit - rightLimit) / (2f * across), (lowLimit - highLimit) / (above + below));
            float side = (leftLimit + rightLimit) * .5f, height = (highLimit + above * depth + lowLimit - below * depth) * .5f;
            var position = right * side + up * height + forward * depth;
            float nearest = front - depth, farthest = back - depth;
            if (!float.IsFinite(depth) || !float.IsFinite(side) || !float.IsFinite(height) || farthest <= 0f)
                throw new InvalidOperationException("The camera fit is degenerate for " + points.Count + " points.");
            return (position, nearest, farthest);
        }

        static void Illuminate(PreviewRenderUtility preview, View view)
        {
            var lights = preview.lights;
            var sun = lights[0];
            sun.type = LightType.Directional; sun.cullingMask = ~0;
            sun.color = SunColour; sun.intensity = SunIntensity;
            sun.transform.rotation = Quaternion.Euler(SunPitch, view.SunYaw, 0f);
            sun.shadows = LightShadows.Soft; sun.shadowStrength = ShadowStrength;
            for (int i = 1; i < lights.Length; i++) { lights[i].intensity = 0f; lights[i].shadows = LightShadows.None; }
            if (lights.Length > 1)
            {
                var fill = lights[1];
                fill.type = LightType.Directional; fill.cullingMask = ~0;
                fill.color = FillColour; fill.intensity = FillIntensity;
                fill.transform.rotation = Quaternion.Euler(FillPitch, view.FillYaw, 0f);
            }
            preview.ambientColor = Ambient;
        }

        static (Color32[] pixels, string source, string note) Course(string id, UniversalRenderPipelineAsset pipeline)
        {
            const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var stageType = Type.GetType(StageType); var settingsType = Type.GetType(SettingsType);
            if (stageType == null || settingsType == null) throw new InvalidOperationException("The Solo preview stage is not compiled in this editor; the card keeps its play-layout preview.");
            string path = settingsType.GetField("Path", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
            var saved = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath(path, settingsType);
            if (!saved) throw new InvalidOperationException("No saved Solo preview settings at " + (path ?? "(unknown path)") + ".");
            var create = stageType.GetConstructor(Members, null, new[] { settingsType, typeof(string) }, null);
            var previewField = stageType.GetField("Preview", Members);
            var worldField = stageType.GetField("World", Members);
            if (create == null || previewField == null || worldField == null) throw new MissingMemberException("WordboundSoloPreviewStage changed; review MapSnapshotCapture.Course.");
            Object settings = null; IDisposable stage = null;
            try
            {
                settings = Object.Instantiate(saved);
                settings.hideFlags = HideFlags.HideAndDontSave;
                string anchor = Anchor(settingsType, settings, id);
                stage = (IDisposable)create.Invoke(new object[] { settings, id });
                var preview = previewField.GetValue(stage) as PreviewRenderUtility ?? throw new InvalidOperationException("The Solo preview stage has no preview renderer.");
                var world = worldField.GetValue(stage) as WordboundCourseWorld ?? throw new InvalidOperationException("The Solo preview stage has no course world.");
                var (level, floors, spawn) = Overview(world);
                CutAbove(world.transform.root, level + ShownHeight);
                var footprint = floors[0];
                foreach (var floor in floors) footprint.Encapsulate(floor);
                var view = Choose(footprint, forward => spawn.HasValue ? -Vector3.Dot(spawn.Value - footprint.center, forward) : 0f);
                int hidden = HideNearWalls(world, floors, view.Forward);
                var region = footprint;
                region.Expand(new Vector3(CourseMargin, 0f, CourseMargin) * 2f);
                var (pixels, framing) = Shoot(preview, Silhouette(world.transform.root, region), view, pipeline);
                return (pixels, "installed Solo content, overview", "level at " + level.ToString("F1") + " m shown, higher levels cut away, "
                    + hidden + " near walls hidden" + anchor + ", " + framing);
            }
            finally
            {
                stage?.Dispose();
                if (settings) Object.DestroyImmediate(settings);
            }
        }

        static string Anchor(Type settingsType, Object settings, string id)
        {
            if (id != WordboundSoloEntry.ParcelServiceMap) return "";
            var depot = settingsType.GetMethod("DepotPose", BindingFlags.Public | BindingFlags.Static);
            var field = settingsType.GetField("ParcelService", BindingFlags.Public | BindingFlags.Instance);
            if (depot == null || field == null) throw new MissingMemberException("WordboundSoloPreviewSettings changed; review MapSnapshotCapture.Anchor.");
            field.SetValue(settings, depot.Invoke(null, null));
            return ", anchored on the default Depot module";
        }

        static (float level, List<Bounds> floors, Vector3? spawn) Overview(WordboundCourseWorld world)
        {
            var nodes = world.GetComponentsInChildren<WordboundCourseNode>(true);
            var floors = nodes.Where(node => node.Kind == WordboundCourseNodeKind.Floor)
                .Select(node => node.GetComponent<Renderer>()).Where(renderer => renderer).Select(renderer => renderer.bounds).ToList();
            if (floors.Count == 0) throw new InvalidOperationException("The course has no floors to frame.");
            var levels = floors.GroupBy(bounds => Mathf.RoundToInt(bounds.max.y / LevelStep)).ToList();
            var spawn = nodes.FirstOrDefault(node => node.Kind == WordboundCourseNodeKind.Spawn);
            var chosen = spawn ? levels.Where(level => level.Key * LevelStep <= spawn.transform.position.y + LevelStep).OrderByDescending(level => level.Key).FirstOrDefault() : null;
            chosen ??= levels.OrderByDescending(level => level.Sum(bounds => bounds.size.x * bounds.size.z)).ThenBy(level => level.Key).First();
            return (chosen.Max(bounds => bounds.max.y), chosen.ToList(), spawn ? spawn.transform.position : (Vector3?)null);
        }

        static void CutAbove(Transform root, float height)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer.bounds.min.y > height) renderer.forceRenderingOff = true;
        }

        static int HideNearWalls(WordboundCourseWorld world, List<Bounds> floors, Vector3 forward)
        {
            int hidden = 0;
            foreach (var node in world.GetComponentsInChildren<WordboundCourseNode>(true))
            {
                if (node.Kind != WordboundCourseNodeKind.Wall || !node.TryGetComponent<Renderer>(out var renderer) || renderer.forceRenderingOff) continue;
                var box = renderer.bounds;
                var nearSide = NearSide(box, box.size.x >= box.size.z, forward);
                if (floors.Any(floor => nearSide.x >= floor.min.x && nearSide.x <= floor.max.x && nearSide.z >= floor.min.z && nearSide.z <= floor.max.z)) continue;
                renderer.forceRenderingOff = true;
                hidden++;
            }
            return hidden;
        }

        static Color32[] Render(PreviewRenderUtility preview, Color clear)
        {
            Texture2D image = null; bool begun = false; var active = RenderTexture.active;
            try
            {
                preview.camera.backgroundColor = clear;
                preview.BeginStaticPreview(new Rect(0, 0, Width * Supersample, Height * Supersample)); begun = true;
                SetSsao(false); preview.Render(true, false); preview.Render(true, false);
                image = preview.EndStaticPreview(); begun = false;
                if (!image || image.width < Width || image.height < Height || Mathf.Abs(image.width / (float)image.height - Width / (float)Height) > .02f)
                    throw new InvalidOperationException("The native capture has an unexpected size: " + (image ? image.width + "x" + image.height : "none"));
                return Downsample(image.GetPixels32(), image.width, image.height);
            }
            finally
            {
                if (begun) { var pending = preview.EndStaticPreview(); if (pending) Object.DestroyImmediate(pending); }
                if (image) Object.DestroyImmediate(image);
                RenderTexture.active = active;
            }
        }

        static Color32[] Downsample(Color32[] source, int width, int height)
        {
            var result = new Color32[Width * Height];
            float stepX = width / (float)Width, stepY = height / (float)Height;
            for (int y = 0; y < Height; y++)
            {
                int y0 = Mathf.FloorToInt(y * stepY), y1 = Mathf.Max(y0 + 1, Mathf.Min(height, Mathf.FloorToInt((y + 1) * stepY)));
                for (int x = 0; x < Width; x++)
                {
                    int x0 = Mathf.FloorToInt(x * stepX), x1 = Mathf.Max(x0 + 1, Mathf.Min(width, Mathf.FloorToInt((x + 1) * stepX)));
                    int r = 0, g = 0, b = 0, count = 0;
                    for (int sy = y0; sy < y1; sy++)
                        for (int sx = x0; sx < x1; sx++)
                        {
                            var c = source[sy * width + sx];
                            r += c.r; g += c.g; b += c.b; count++;
                        }
                    result[y * Width + x] = new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), 255);
                }
            }
            return result;
        }

        static bool Compose(Color32[] onWhite, Color32[] onBlack, out Color32[] result, out float share, out float gain)
        {
            int count = Width * Height, solid = 0;
            result = null; gain = 1f;
            var cover = new float[count]; var colour = new Vector3[count]; var histogram = new int[256];
            for (int i = 0; i < count; i++)
            {
                var white = onWhite[i]; var black = onBlack[i];
                float covered = 1f - Mathf.Clamp01((white.r - black.r + white.g - black.g + white.b - black.b) / (3f * 255f));
                cover[i] = covered;
                if (covered <= MinCover) continue;
                var seen = Vector3.Min(new Vector3(black.r, black.g, black.b) / (255f * covered), Vector3.one);
                colour[i] = seen;
                if (covered < SolidCover) continue;
                histogram[Mathf.Clamp(Mathf.RoundToInt(Luma(seen) * 255f), 0, 255)]++;
                solid++;
            }
            share = solid / (float)count;
            if (share < EmptyShare || share > FullShare) return false;
            gain = Gain(histogram, solid);
            var shade = Soften(cover);
            result = new Color32[count];
            var tint = new Vector3(PaperShade.r, PaperShade.g, PaperShade.b);
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    var paper = Paper(x, y);
                    float shadow = Mathf.Clamp01(shade[Mathf.Min(Height - 1, y + ShadowDrop) * Width + x] * PaperShadow);
                    var ground = Vector3.Lerp(paper, Vector3.Scale(paper, tint), shadow);
                    var mixed = Vector3.Lerp(ground, Grade(colour[i] * gain), cover[i]);
                    result[i] = new Color(Mathf.Clamp01(mixed.x), Mathf.Clamp01(mixed.y), Mathf.Clamp01(mixed.z), 1f);
                }
            return true;
        }

        static float Luma(Vector3 v) => v.x * .2126f + v.y * .7152f + v.z * .0722f;

        static float Gain(int[] histogram, int total)
        {
            int wanted = Mathf.CeilToInt(total * WhitePoint), seen = 0, level = histogram.Length - 1;
            for (int i = 0; i < histogram.Length; i++)
            {
                seen += histogram[i];
                if (seen < wanted) continue;
                level = i;
                break;
            }
            return Mathf.Clamp(TargetWhite * (histogram.Length - 1) / Mathf.Max(1, level), MinGain, MaxGain);
        }

        static Vector3 Grade(Vector3 v)
        {
            float luma = Luma(v);
            v = Vector3.LerpUnclamped(new Vector3(luma, luma, luma), v, Saturation);
            v = (v - Vector3.one * .5f) * Contrast + Vector3.one * .5f;
            return new Vector3(Mathf.Clamp01(v.x), Mathf.Clamp01(v.y), Mathf.Clamp01(v.z));
        }

        static Vector3 Paper(int x, int y)
        {
            float u = (x + .5f) / Width * 2f - 1f, v = (y + .5f) / Height * 2f - 1f;
            var c = Color.Lerp(PaperCentre, PaperEdge, Mathf.SmoothStep(0f, 1f, Mathf.Sqrt((u * u + v * v) * .5f)));
            return new Vector3(c.r, c.g, c.b);
        }

        static float[] Soften(float[] source)
        {
            var a = (float[])source.Clone(); var b = new float[a.Length];
            for (int pass = 0; pass < ShadowPasses; pass++)
            {
                Smear(a, b, Width, Height, 1, Width);
                Smear(b, a, Height, Width, Width, 1);
            }
            return a;
        }

        static void Smear(float[] from, float[] to, int length, int lines, int step, int stride)
        {
            float window = 2 * ShadowBlur + 1;
            for (int line = 0; line < lines; line++)
            {
                int start = line * stride; float sum = 0f;
                for (int i = 0; i <= ShadowBlur && i < length; i++) sum += from[start + i * step];
                for (int i = 0; i < length; i++)
                {
                    to[start + i * step] = sum / window;
                    int enter = i + ShadowBlur + 1, leave = i - ShadowBlur;
                    if (enter < length) sum += from[start + enter * step];
                    if (leave >= 0) sum -= from[start + leave * step];
                }
            }
        }

        static void RequireVaried(Color32[] pixels)
        {
            var distinct = new HashSet<int>(); int lit = 0;
            foreach (var c in pixels)
            {
                if (c.r + c.g + c.b > 24) lit++;
                if (distinct.Count < 4096) distinct.Add(c.r << 16 | c.g << 8 | c.b);
            }
            if (lit < pixels.Length / 4 || distinct.Count < 256) throw new InvalidOperationException("The render is blank or nearly uniform (" + distinct.Count + " colours).");
        }

        static byte[] Encode(Color32[] pixels)
        {
            Texture2D texture = null;
            try
            {
                texture = new Texture2D(Width, Height, TextureFormat.RGB24, false, false) { hideFlags = HideFlags.HideAndDontSave };
                texture.SetPixels32(pixels); texture.Apply(false);
                return texture.EncodeToPNG();
            }
            finally { if (texture) Object.DestroyImmediate(texture); }
        }

        static string WriteSheet(List<Shot> shots)
        {
            if (shots.Count == 0) return "none";
            try
            {
                int rows = (shots.Count + SheetColumns - 1) / SheetColumns, smallRows = (shots.Count + SmallColumns - 1) / SmallColumns;
                int width = SheetColumns * CellWidth + (SheetColumns + 1) * SheetGap;
                int height = SheetGap + rows * (LabelRoom + CellHeight + SheetGap) + 2 * smallRows * (LabelRoom + SmallHeight + SheetGap);
                var sheet = new CaptureSheet(width, height, PaperCentre);
                Color32 ink = LobbyParcel.Ink;
                int top = SheetGap;
                for (int i = 0; i < shots.Count; i++)
                {
                    int left = SheetGap + i % SheetColumns * (CellWidth + SheetGap), at = top + i / SheetColumns * (LabelRoom + CellHeight + SheetGap);
                    sheet.Label(shots[i].id, left, at + 2, LabelScale, ink);
                    sheet.Paste(CaptureSheet.Resize(shots[i].pixels, Width, Height, CellWidth, CellHeight), CellWidth, CellHeight, left, at + LabelRoom);
                }
                top += rows * (LabelRoom + CellHeight + SheetGap);
                Strip(sheet, shots, top, "NEW AT CARD SIZE", ink, shot => shot.pixels == null ? null : CaptureSheet.Resize(shot.pixels, Width, Height, SmallWidth, SmallHeight));
                top += smallRows * (LabelRoom + SmallHeight + SheetGap);
                Strip(sheet, shots, top, "BEFORE", ink, Before);
                sheet.Save(Full(SheetPath));
                return SheetPath;
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Wreckabulary] Map snapshots: the contact sheet was not written. " + error);
                return "not written";
            }
        }

        static void Strip(CaptureSheet sheet, List<Shot> shots, int top, string caption, Color32 ink, Func<Shot, Color32[]> picture)
        {
            for (int i = 0; i < shots.Count; i++)
            {
                int left = SheetGap + i % SmallColumns * (SmallWidth + SheetGap), at = top + i / SmallColumns * (LabelRoom + SmallHeight + SheetGap);
                if (i % SmallColumns == 0) sheet.Label(caption, left, at + 2, LabelScale, ink);
                var small = picture(shots[i]);
                if (small != null) sheet.Paste(small, SmallWidth, SmallHeight, left, at + LabelRoom);
            }
        }

        static Color32[] Before(Shot shot)
        {
            if (shot.previous == null) return null;
            var old = CaptureSheet.Decode(shot.previous, out int width, out int height);
            return CaptureSheet.Resize(old, width, height, SmallWidth, SmallHeight);
        }

        static void Import(List<Shot> shots)
        {
            foreach (var shot in shots)
            {
                AssetDatabase.ImportAsset(shot.path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(shot.path) as TextureImporter ?? throw new InvalidOperationException("No texture importer for " + shot.path);
                if (MapSnapshotImporter.Configure(importer)) importer.SaveAndReimport();
                if (!AssetDatabase.LoadAssetAtPath<Sprite>(shot.path)) throw new InvalidOperationException("The snapshot did not import as a Sprite: " + shot.path);
            }
        }

        static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder)) return;
            int split = Folder.LastIndexOf('/');
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(Folder.Substring(0, split), Folder.Substring(split + 1))))
                throw new IOException("Could not create " + Folder);
        }

        static IEnumerable<Vector3> Corners(Bounds bounds)
        { for (int i = 0; i < 8; i++) yield return bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)); }
        static string Global() => string.Join("|", new object[] {
            RenderSettings.ambientMode, RenderSettings.ambientLight, RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor,
            RenderSettings.ambientIntensity, RenderSettings.reflectionIntensity, RenderSettings.fog, RenderSettings.fogColor, RenderSettings.fogDensity, RenderSettings.fogMode,
            RenderSettings.skybox ? RenderSettings.skybox.GetEntityId().ToString() : "", RenderSettings.sun ? RenderSettings.sun.GetEntityId().ToString() : "",
            AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline), GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp ? urp.shadowDistance : -1f,
            RenderTexture.active ? RenderTexture.active.GetEntityId().ToString() : "", GL.sRGBWrite, EditorSettings.asyncShaderCompilation,
            Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION"), QualitySettings.GetQualityLevel(), QualitySettings.activeColorSpace,
            string.Join(",", Selection.objects.Select(o => o ? o.GetEntityId().ToString() : "")) });
        static string SceneState() => string.Join("|", Enumerable.Range(0, SceneManager.sceneCount).Select(i => {
            var s = SceneManager.GetSceneAt(i); return s.handle.GetRawData() + ":" + s.path + ":" + s.isLoaded + ":" + s.isDirty + ":" + (s == SceneManager.GetActiveScene()) + ":" +
                (s.isLoaded ? string.Join(",", s.GetRootGameObjects().Select(o => o.GetEntityId().ToString()).OrderBy(id => id, StringComparer.Ordinal)) : "unloaded"); }));
        static void SetSsao(bool enabled) { if (enabled) Shader.EnableKeyword("_SCREEN_SPACE_OCCLUSION"); else Shader.DisableKeyword("_SCREEN_SPACE_OCCLUSION"); }
        static void Clean(Action action, List<Exception> errors) { try { action(); } catch (Exception error) { errors.Add(error); } }
        static void Log(string line) => Debug.Log("[Wreckabulary] Map snapshots " + line);
        static string Full(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
    }

    public sealed class MapSnapshotImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(MapSnapshotCapture.Folder + "/", StringComparison.Ordinal)) Configure((TextureImporter)assetImporter);
        }

        public static bool Configure(TextureImporter importer)
        {
            string before = Describe(importer);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            return before != Describe(importer);
        }

        static string Describe(TextureImporter importer)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            return string.Join("|", new object[] { importer.textureType, importer.spriteImportMode, settings.spriteMeshType, settings.spriteGenerateFallbackPhysicsShape,
                importer.sRGBTexture, importer.alphaSource, importer.alphaIsTransparency, importer.mipmapEnabled, importer.isReadable, importer.wrapMode,
                importer.filterMode, importer.maxTextureSize, importer.textureCompression });
        }
    }
}
