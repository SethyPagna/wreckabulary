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
        public const int Width = 1280, Height = 720;
        const int Supersample = 2, MaximumShadowDistance = 400;
        const float Pitch = 50f, FieldOfView = 30f, WidthFill = .85f, DepthSpill = 1.08f, ShownHeight = 2.2f, ShadowStrength = .8f, LevelStep = .5f;
        static readonly float[] Yaws = { 20f, 110f };
        const string LightingProfile = "Environment/SunlitHouse";
        const string OnlineEditor = ", Wreckabulary.Online.Authoring.Editor";
        const string StageType = "Wreckabulary.Online.Editor.WordboundSoloPreviewStage" + OnlineEditor;
        const string SettingsType = "Wreckabulary.Online.Editor.WordboundSoloPreviewSettings" + OnlineEditor;
        static readonly Color Sky = new Color(.93f, .87f, .76f);
        static readonly string[] Courses = { WordboundSoloEntry.ParcelServiceMap, WordboundSoloEntry.LostAddressMap, WordboundSoloEntry.ParcelHallMap };

        sealed class Shot { public string id, path, source, note; public int bytes; }

        [MenuItem("Wreckabulary/Art/Capture map snapshots")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Capture map snapshots in Edit Mode.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Map snapshots need a graphics device; do not run with -nographics.");
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset pipeline) throw new InvalidOperationException("Map snapshots expect the game's URP pipeline asset to be active.");
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            if (!data) throw new InvalidOperationException("Resources/GameData is missing; run Wreckabulary > Data > Set Up Game Data.");
            var houses = data.Parse().Houses;
            var lighting = Resources.Load<EnvironmentLightingProfile>(LightingProfile);
            if (!lighting) Debug.LogWarning("[Wreckabulary] Map snapshots: Resources/" + LightingProfile + " is missing; using the default daylight values.");
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
                    Attempt(house.Key, () => House(house.Key, house.Value, pipeline, lighting), shots, skipped);
                foreach (string course in Courses) Attempt(course, () => Course(course, pipeline, lighting), shots, skipped);
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
            bool scenesKept = SceneState() == scenes, globalsKept = Global() == global, tactileKept = TactileMaterials.RegisteredScopeCount == tactile;
            foreach (var shot in shots) Log("wrote " + shot.path + " (" + shot.bytes + " bytes, " + shot.source + (shot.note == null ? "" : ", " + shot.note) + ")");
            foreach (string reason in skipped) Debug.LogWarning("[Wreckabulary] Map snapshot skipped " + reason);
            string summary = "[Wreckabulary] Map snapshots " + (failure == null && errors.Count == 0 && scenesKept && globalsKept && tactileKept ? "DONE" : "FAIL") +
                ": written=" + shots.Count + " skipped=" + skipped.Count + " scenesPreserved=" + scenesKept + " globalsPreserved=" + globalsKept +
                " tactileReleased=" + tactileKept + " folder=" + Folder;
            if (failure != null) throw new InvalidOperationException(summary, failure);
            if (errors.Count > 0) throw new AggregateException(summary, errors);
            if (!scenesKept || !globalsKept || !tactileKept) throw new InvalidOperationException(summary + "\nscenes before: " + scenes + "\nscenes after: " + SceneState() + "\nglobals before: " + global + "\nglobals after: " + Global());
            if (skipped.Count > 0) Debug.LogWarning(summary); else Debug.Log(summary);
        }

        static void Attempt(string id, Func<(byte[] png, string source, string note)> render, List<Shot> shots, List<string> skipped)
        {
            try
            {
                var (png, source, note) = render();
                string path = Folder + "/" + id + ".png";
                File.WriteAllBytes(Full(path), png);
                shots.Add(new Shot { id = id, path = path, source = source, note = note, bytes = png.Length });
            }
            catch (Exception error) when (error is not (IOException or UnauthorizedAccessException))
            {
                var cause = error is TargetInvocationException { InnerException: { } inner } ? inner : error;
                skipped.Add(id + ": " + cause.GetType().Name + ": " + cause.Message);
            }
        }

        static (byte[] png, string source, string note) House(string id, HouseLayout layout, UniversalRenderPipelineAsset pipeline, EnvironmentLightingProfile lighting)
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
                CutAway(anchor.transform, geometry, floors, focus);
                var (png, framing) = Shoot(preview, Footprint(layout, floors, focus), pipeline, lighting);
                string storey = floors.Count > 1 ? layout.StoreyLabel(focus).ToLowerInvariant() + " shown, upper floors cut away" : "single floor";
                return (png, source, storey + ", " + framing);
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

        static void CutAway(Transform root, Transform geometry, List<float> floors, int focus)
        {
            var handled = new HashSet<Renderer>();
            foreach (var surface in root.GetComponentsInChildren<CutawaySurface>(true))
                foreach (var renderer in surface.Renderers)
                {
                    if (!renderer || !handled.Add(renderer) || surface.OwnerStorey < focus) continue;
                    if (surface.OwnerStorey > focus || renderer.shadowCastingMode == ShadowCastingMode.Off) renderer.forceRenderingOff = true;
                    else renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }
            var fixedParts = new HashSet<Renderer>();
            for (int storey = 0; geometry && storey < floors.Count; storey++)
            {
                var group = geometry.Find(RoomBuilder.StoreyName(storey));
                if (!group) continue;
                foreach (var renderer in group.GetComponentsInChildren<Renderer>(true))
                {
                    fixedParts.Add(renderer);
                    if (storey > focus && handled.Add(renderer)) renderer.forceRenderingOff = true;
                }
            }
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                if (!fixedParts.Contains(renderer) && !handled.Contains(renderer) && StoreyAt(floors, renderer.bounds.min.y) > focus)
                    renderer.forceRenderingOff = true;
        }

        static int StoreyAt(List<float> floors, float y)
        {
            int storey = 0;
            for (int i = 1; i < floors.Count; i++) if (floors[i] <= y + HouseLayout.StandingSlack) storey = i;
            return storey;
        }

        static Bounds Footprint(HouseLayout layout, List<float> floors, int focus)
        {
            var rooms = layout.Rooms.Where(room => layout.StoreyOf(room) <= focus).ToList();
            if (rooms.Count == 0) rooms = layout.Rooms.ToList();
            if (rooms.Count == 0) throw new InvalidOperationException("The house has no rooms to frame.");
            var box = new Bounds();
            box.SetMinMax(new Vector3(rooms.Min(r => r.MinX), floors.Count > 0 ? floors[0] : 0f, rooms.Min(r => r.MinZ)),
                new Vector3(rooms.Max(r => r.MaxX), (floors.Count > focus ? floors[focus] : 0f) + ShownHeight, rooms.Max(r => r.MaxZ)));
            return box;
        }

        static (byte[] png, string framing) Shoot(PreviewRenderUtility preview, Bounds box, UniversalRenderPipelineAsset pipeline, EnvironmentLightingProfile lighting)
        {
            float yaw = Yaws.OrderByDescending(candidate => Spread(box, Quaternion.Euler(Pitch, candidate, 0f))).First();
            var rotation = Quaternion.Euler(Pitch, yaw, 0f);
            var (position, distance, nearest) = Fit(box, rotation, FieldOfView, Width / (float)Height);
            pipeline.shadowDistance = Mathf.Min(MaximumShadowDistance, Mathf.Max(pipeline.shadowDistance, distance + box.extents.magnitude * 1.5f));
            var png = Render(preview, camera =>
            {
                camera.orthographic = false; camera.fieldOfView = FieldOfView; camera.aspect = Width / (float)Height;
                camera.nearClipPlane = Mathf.Max(.3f, nearest - 4f); camera.farClipPlane = distance + 600f; camera.cullingMask = ~0;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Sky; camera.allowHDR = false; camera.allowMSAA = true;
                camera.transform.SetPositionAndRotation(position, rotation);
                Illuminate(preview, lighting);
            }, lighting);
            return (png, "yaw " + yaw.ToString("F0") + ", camera " + distance.ToString("F1") + " m");
        }

        static float Spread(Bounds box, Quaternion rotation)
        {
            var right = rotation * Vector3.right; var up = rotation * Vector3.up;
            var corners = Corners(box).ToArray();
            float wide = corners.Max(c => Vector3.Dot(c, right)) - corners.Min(c => Vector3.Dot(c, right));
            float tall = corners.Max(c => Vector3.Dot(c, up)) - corners.Min(c => Vector3.Dot(c, up));
            return wide / Mathf.Max(.01f, tall);
        }

        static (Vector3 position, float distance, float nearest) Fit(Bounds box, Quaternion rotation, float fieldOfView, float aspect)
        {
            var right = rotation * Vector3.right; var up = rotation * Vector3.up; var forward = rotation * Vector3.forward;
            float tan = Mathf.Tan(fieldOfView * .5f * Mathf.Deg2Rad), tanV = tan * DepthSpill, tanH = tan * aspect * WidthFill;
            var corners = Corners(box).ToArray();
            var target = box.center; float distance = 0f;
            for (int pass = 0; pass < 8; pass++)
            {
                distance = Required(corners, target, right, up, forward, tanH, tanV);
                var eye = target - forward * distance;
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                foreach (var corner in corners)
                {
                    var v = corner - eye; float z = Mathf.Max(.01f, Vector3.Dot(v, forward));
                    float x = Vector3.Dot(v, right) / (z * tanH), y = Vector3.Dot(v, up) / (z * tanV);
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
                target += right * ((minX + maxX) * .5f * distance * tanH) + up * ((minY + maxY) * .5f * distance * tanV);
            }
            distance = Required(corners, target, right, up, forward, tanH, tanV);
            var position = target - forward * distance;
            float nearest = corners.Min(corner => Vector3.Dot(corner - position, forward));
            if (!float.IsFinite(distance) || distance <= 0f) throw new InvalidOperationException("The camera fit is degenerate: " + box);
            return (position, distance, nearest);
        }

        static float Required(Vector3[] corners, Vector3 target, Vector3 right, Vector3 up, Vector3 forward, float tanH, float tanV)
        {
            float distance = .5f;
            foreach (var corner in corners)
            {
                var v = corner - target; float z = Vector3.Dot(v, forward);
                distance = Mathf.Max(distance, Mathf.Abs(Vector3.Dot(v, right)) / tanH - z, Mathf.Abs(Vector3.Dot(v, up)) / tanV - z);
            }
            return distance;
        }

        static void Illuminate(PreviewRenderUtility preview, EnvironmentLightingProfile profile)
        {
            var lights = preview.lights;
            var sun = lights[0];
            sun.type = LightType.Directional; sun.cullingMask = ~0;
            sun.color = profile ? profile.SunColor : new Color(1f, .91f, .76f);
            sun.intensity = profile ? profile.SunIntensity : 1.1f;
            sun.transform.rotation = Quaternion.Euler(profile ? profile.SunEuler : new Vector3(48f, -32f, 0f));
            sun.shadows = LightShadows.Soft; sun.shadowStrength = ShadowStrength;
            for (int i = 1; i < lights.Length; i++) { lights[i].intensity = 0f; lights[i].shadows = LightShadows.None; }
            if (lights.Length > 1)
            {
                var fill = lights[1];
                fill.type = LightType.Directional; fill.cullingMask = ~0;
                fill.color = profile ? profile.FillColor : new Color(.7f, .83f, 1f);
                fill.intensity = profile ? profile.FillIntensity : .22f;
                fill.transform.rotation = Quaternion.Euler(profile ? profile.FillEuler : new Vector3(35f, 145f, 0f));
            }
            preview.ambientColor = profile ? Color.Lerp(profile.AmbientSky, profile.AmbientEquator, .4f) : new Color(.61f, .67f, .74f);
        }

        static (byte[] png, string source, string note) Course(string id, UniversalRenderPipelineAsset pipeline, EnvironmentLightingProfile lighting)
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
                var (level, box) = Overview(world);
                CutAbove(world.transform.root, level + ShownHeight);
                var (png, framing) = Shoot(preview, box, pipeline, lighting);
                return (png, "installed Solo content, overview", "level at " + level.ToString("F1") + " m shown, higher levels cut away" + anchor + ", " + framing);
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

        static (float level, Bounds box) Overview(WordboundCourseWorld world)
        {
            var nodes = world.GetComponentsInChildren<WordboundCourseNode>(true);
            var floors = nodes.Where(node => node.Kind == WordboundCourseNodeKind.Floor)
                .Select(node => node.GetComponent<Renderer>()).Where(renderer => renderer).Select(renderer => renderer.bounds).ToList();
            if (floors.Count == 0) throw new InvalidOperationException("The course has no floors to frame.");
            var levels = floors.GroupBy(bounds => Mathf.RoundToInt(bounds.max.y / LevelStep)).ToList();
            var spawn = nodes.FirstOrDefault(node => node.Kind == WordboundCourseNodeKind.Spawn);
            var chosen = spawn ? levels.Where(level => level.Key * LevelStep <= spawn.transform.position.y + LevelStep).OrderByDescending(level => level.Key).FirstOrDefault() : null;
            chosen ??= levels.OrderByDescending(level => level.Sum(bounds => bounds.size.x * bounds.size.z)).ThenBy(level => level.Key).First();
            float top = chosen.Max(bounds => bounds.max.y);
            var box = new Bounds();
            box.SetMinMax(new Vector3(chosen.Min(bounds => bounds.min.x), chosen.Min(bounds => bounds.min.y), chosen.Min(bounds => bounds.min.z)),
                new Vector3(chosen.Max(bounds => bounds.max.x), top + ShownHeight, chosen.Max(bounds => bounds.max.z)));
            return (top, box);
        }

        static void CutAbove(Transform root, float height)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer.bounds.min.y > height) renderer.forceRenderingOff = true;
        }

        static byte[] Render(PreviewRenderUtility preview, Action<Camera> pose, EnvironmentLightingProfile grade)
        {
            Texture2D image = null; bool begun = false; var active = RenderTexture.active;
            try
            {
                preview.BeginStaticPreview(new Rect(0, 0, Width * Supersample, Height * Supersample)); begun = true;
                pose(preview.camera);
                SetSsao(false); preview.Render(true, false); preview.Render(true, false);
                image = preview.EndStaticPreview(); begun = false;
                if (!image || image.width < Width || image.height < Height || Mathf.Abs(image.width / (float)image.height - Width / (float)Height) > .02f)
                    throw new InvalidOperationException("The native capture has an unexpected size: " + (image ? image.width + "x" + image.height : "none"));
                var pixels = Downsample(image.GetPixels32(), image.width, image.height);
                if (grade) Grade(pixels, grade);
                RequireVaried(pixels);
                return Encode(pixels);
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

        static void Grade(Color32[] pixels, EnvironmentLightingProfile profile)
        {
            float exposure = Mathf.Pow(2f, profile.Exposure), contrast = 1f + profile.Contrast / 100f, saturation = 1f + profile.Saturation / 100f;
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = (Color)pixels[i];
                var v = new Vector3(c.r, c.g, c.b) * exposure;
                float luma = v.x * .2126f + v.y * .7152f + v.z * .0722f;
                v = Vector3.LerpUnclamped(new Vector3(luma, luma, luma), v, saturation);
                v = (v - Vector3.one * .5f) * contrast + Vector3.one * .5f;
                pixels[i] = new Color(Mathf.Clamp01(v.x), Mathf.Clamp01(v.y), Mathf.Clamp01(v.z), 1f);
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
