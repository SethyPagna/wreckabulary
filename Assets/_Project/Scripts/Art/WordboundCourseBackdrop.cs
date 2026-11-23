using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules.Surroundings;
using Wreckabulary.WordboundRuntime;

namespace Wreckabulary.Art
{
    [DisallowMultipleComponent]
    public sealed class WordboundCourseBackdrop : MonoBehaviour
    {
        public const string RootName = "Course backdrop";

        readonly List<Mesh> meshes = new();
        Func<WordboundCourseWorld> source;
        CourseBackdropKit kit;
        WordboundCourseWorld world;
        GameObject root;
        bool builtLow, failed;

        public GameObject Root => root;
        public WordboundCourseWorld World => world;
        public CourseSurroundingsResult Layout { get; private set; }

        public static bool Supported => !MapSurroundings.Disabled && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        public static WordboundCourseBackdrop Watch(GameObject host, Func<WordboundCourseWorld> courseWorld, CourseBackdropKit kitOverride = null)
        {
            if (!host) throw new ArgumentNullException(nameof(host));
            if (!host.TryGetComponent<WordboundCourseBackdrop>(out var backdrop)) backdrop = host.AddComponent<WordboundCourseBackdrop>();
            backdrop.source = courseWorld;
            backdrop.kit = kitOverride;
            return backdrop;
        }

        void LateUpdate() => Refresh();

        public void Refresh()
        {
            var current = source?.Invoke();
            bool low = MapSurroundings.UseLowTier;
            bool moved = !ReferenceEquals(current, world);
            if (moved || !current || (root && builtLow != low) || (!root && meshes.Count > 0))
            {
                if (moved) failed = false;
                Release();
                world = current;
            }
            if (!world || root || failed || !Supported || !world.isActiveAndEnabled || !world.gameObject.scene.isLoaded) return;
            try
            {
                root = Build(world, kit ? kit : CourseBackdropKit.Load(), low, meshes, out var layout);
                Layout = layout;
                builtLow = low;
            }
            catch (Exception error)
            {
                failed = true;
                Release();
                Debug.LogWarning($"[Wreckabulary] Course backdrop skipped: {error.Message}");
            }
        }

        public static GameObject Build(WordboundCourseWorld world, CourseBackdropKit kit, bool low, List<Mesh> owned, out CourseSurroundingsResult layout, bool readable = false)
        {
            if (!world) throw new ArgumentNullException(nameof(world));
            if (!kit) throw new InvalidOperationException($"No course backdrop kit at Resources/{CourseBackdropKit.ResourcePath}.");
            var course = world.Course;
            var footprint = CourseFootprint.Of(course.Geometry.Select(m => m.Bounds));
            string id = course.CopyDocument().Id;
            var tier = low ? CourseSurroundingsTier.Low : CourseSurroundingsTier.High;
            layout = CourseSurroundings.Draft(kit.Style(), kit.Kit(), id, footprint, tier);
            if (!layout.Valid && tier == CourseSurroundingsTier.High)
                layout = CourseSurroundings.Draft(kit.Style(), kit.Kit(), id, footprint, CourseSurroundingsTier.Low);
            if (!layout.Valid) throw new InvalidOperationException(string.Join("; ", layout.Problems));
            var built = CourseBackdropMesh.Build(layout, kit, RootName, readable, owned);
            var scene = world.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded && built.scene != scene) SceneManager.MoveGameObjectToScene(built, scene);
            built.transform.SetPositionAndRotation(world.transform.position, world.transform.rotation);
            built.AddComponent<BackdropLife>();
            var skyline = layout.Skyline;
            Debug.Log($"[Wreckabulary] Course backdrop {id} {layout.Tier}: {layout.Renderers} renderers, {layout.Triangles} triangles, {layout.Placements.Count} pieces, {layout.Trimmed} trimmed; " +
                $"skyline {skyline.Towers.Count} towers, {skyline.Renderers} renderers, {skyline.Triangles} triangles, {skyline.Trimmed} trimmed.");
            return built;
        }

        void Release()
        {
            if (root) Discard(root);
            root = null;
            foreach (var mesh in meshes)
                if (mesh) Discard(mesh);
            meshes.Clear();
            Layout = null;
        }

        static void Discard(UnityEngine.Object target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        void OnDestroy() => Release();
    }
}
