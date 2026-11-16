using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wreckabulary
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class CameraCutaway : MonoBehaviour
    {
        static readonly Dictionary<Camera, CameraCutaway> cameras = new();
        static readonly List<RenderScope> scopes = new();
        static readonly Stack<RenderScope> pool = new();
        static readonly Stack<VisibilityScope> pipelineScopes = new();
        static readonly Dictionary<Renderer, CameraCutawayRenderer> prepared = new();
        static readonly List<CameraCutawayRenderer> releasing = new();
        static readonly ProfilerMarker ApplyMarker = new("Cutaway.Apply");
        static long nextToken;
        static bool subscribed;
        Camera lens;
        UnityEngine.Object owner;
        readonly List<PlayerController> participants = new(4);
        readonly List<PlayerController> current = new(4);
        readonly List<Vector3> plannedFeet = new(4);
        readonly List<Renderer> occluderShadows = new();
        IReadOnlyList<PlayerController> suppliedParticipants;
        IReadOnlyList<Renderer> occluders;
        int plannedFrame = -1, plannedVersion;
        Vector3 plannedAt;
        Quaternion plannedFacing;
        public StoreyCutaway World { get; private set; }
        public PlayerController FollowedPlayer { get; private set; }
        public bool IsOverview => !FollowedPlayer;
        public StoreyCutaway.VisibilityPlan Visibility { get; } = new();
        public static int PreparedRendererCount => prepared.Count;

        void Awake() => lens = GetComponent<Camera>();

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (!lens) lens = GetComponent<Camera>();
            cameras[lens] = this;
            if (subscribed) return;
            RenderPipelineManager.beginCameraRendering += BeginRender;
            RenderPipelineManager.endCameraRendering += EndRender;
            subscribed = true;
        }

        void OnDisable()
        {
            RestoreAll();
            if (lens) cameras.Remove(lens);
            owner = null;
            Forget();
            if (cameras.Count == 0) { Unsubscribe(); ReleaseRenderers(); }
        }

        public void SetView(UnityEngine.Object owner, StoreyCutaway world, PlayerController followed,
            IReadOnlyList<PlayerController> overviewParticipants = null)
        {
            if (World != world) Visibility.Reset();
            plannedFrame = -1;
            this.owner = owner;
            World = world;
            FollowedPlayer = followed;
            suppliedParticipants = overviewParticipants;
        }

        public void ClearView(UnityEngine.Object source)
        {
            if (owner != source) return;
            owner = null;
            occluders = null;
            Forget();
        }

        public void SetOccluders(IReadOnlyList<Renderer> renderers) => occluders = renderers;

        void Forget()
        {
            Visibility.Clear();
            occluderShadows.Clear();
            plannedFrame = -1;
        }

        public void RefreshVisibility()
        {
            Forget();
            if (!owner || !isActiveAndEnabled) return;
            CollectParticipants(participants);
            if (World)
            {
                World.Evaluate(lens, participants, IsOverview, Visibility);
                Stamp();
            }
            else EvaluateStandalone();
            AddOccluders();
            foreach (var renderer in Visibility.Hidden) PrepareRenderer(renderer);
            foreach (var renderer in Visibility.Shadows) PrepareRenderer(renderer);
        }

        void RefreshForRender()
        {
            if (!PlanStillHolds())
            {
                RefreshVisibility();
                return;
            }
            World.RecheckDynamic(participants, Visibility);
            foreach (var renderer in occluderShadows) Visibility.Shadows.Remove(renderer);
            occluderShadows.Clear();
            AddOccluders();
            foreach (var renderer in Visibility.dynamicHidden) PrepareRenderer(renderer);
            foreach (var renderer in occluderShadows) PrepareRenderer(renderer);
        }

        void CollectParticipants(List<PlayerController> destination)
        {
            destination.Clear();
            if (FollowedPlayer) destination.Add(FollowedPlayer);
            else if (suppliedParticipants != null)
            {
                for (int i = 0; i < suppliedParticipants.Count; i++)
                {
                    var player = suppliedParticipants[i];
                    if (player && player.isActiveAndEnabled && !player.IsEliminated) destination.Add(player);
                }
            }
            else if (World) World.CollectParticipants(destination);
            else foreach (var player in Wreckabulary.World.Players)
                if (player && player.isActiveAndEnabled && player.gameObject.scene == gameObject.scene &&
                    !player.IsEliminated && player.Binding is not BotBinding) destination.Add(player);
        }

        void Stamp()
        {
            if (!lens) return;
            plannedFrame = Time.frameCount;
            plannedVersion = World.Version;
            plannedAt = lens.transform.position;
            plannedFacing = lens.transform.rotation;
            plannedFeet.Clear();
            foreach (var player in participants) plannedFeet.Add(player.transform.position);
        }

        bool PlanStillHolds()
        {
            if (plannedFrame != Time.frameCount || !World || !lens || World.Version != plannedVersion) return false;
            var view = lens.transform;
            if (!view.position.Equals(plannedAt) || !view.rotation.Equals(plannedFacing)) return false;
            CollectParticipants(current);
            if (current.Count != participants.Count) return false;
            for (int i = 0; i < current.Count; i++)
                if (!ReferenceEquals(current[i], participants[i]) || !current[i].transform.position.Equals(plannedFeet[i])) return false;
            return true;
        }

        void AddOccluders()
        {
            if (occluders == null) return;
            for (int i = 0; i < occluders.Count; i++)
            {
                var renderer = occluders[i];
                if (renderer && !Visibility.Hidden.Contains(renderer) && Visibility.Shadows.Add(renderer)) occluderShadows.Add(renderer);
            }
        }

        static void PrepareRenderer(Renderer renderer)
        {
            if (!Application.isPlaying || renderer is not MeshRenderer || prepared.ContainsKey(renderer)) return;
            if (renderer.TryGetComponent<CameraCutawayRenderer>(out _)) return;
            var callbacks = renderer.gameObject.AddComponent<CameraCutawayRenderer>();
            callbacks.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
            prepared.Add(renderer, callbacks);
        }

        static void ReleaseRenderers()
        {
            releasing.Clear();
            releasing.AddRange(prepared.Values);
            prepared.Clear();
            foreach (var component in releasing)
                if (component)
                {
                    if (Application.isPlaying) Destroy(component);
                    else DestroyImmediate(component);
                }
            releasing.Clear();
        }

        internal static void UnregisterRenderer(Renderer renderer, CameraCutawayRenderer component)
        {
            if (!ReferenceEquals(renderer, null) && prepared.TryGetValue(renderer, out var current) && ReferenceEquals(current, component))
                prepared.Remove(renderer);
        }

        public bool WouldHide(Renderer renderer) => owner && Visibility.WouldHide(renderer);
        public bool IsVisible(Renderer renderer) => renderer && renderer.enabled && !renderer.forceRenderingOff && renderer.gameObject.activeInHierarchy && !WouldHide(renderer);

        void EvaluateStandalone()
        {
            var active = CutawaySurface.Active;
            for (int s = 0; s < active.Count; s++)
            {
                var surface = active[s];
                if (!surface || surface.gameObject.scene != gameObject.scene) continue;
                var renderers = surface.Renderers;
                for (int i = 0; i < renderers.Count; i++)
                {
                    var renderer = renderers[i];
                    if (renderer && (IsOverview || StoreyCutaway.Obstructs(lens, renderer.bounds, participants)))
                        Visibility.Shadows.Add(renderer);
                }
            }
        }

        public float ProbeCeilings(Vector3 from, Vector3 direction, float reach, float radius)
        {
            if (World) return World.ProbeCeilings(from, direction, reach, radius);
            float available = reach;
            var active = CutawaySurface.Active;
            for (int s = 0; s < active.Count; s++)
            {
                var surface = active[s];
                if (!surface || surface.gameObject.scene != gameObject.scene || surface.Kind == CutawayKind.UpperWall) continue;
                var renderers = surface.Renderers;
                for (int i = 0; i < renderers.Count; i++)
                    available = StoreyCutaway.ProbeCeiling(renderers[i], from, direction, available, radius);
            }
            return available;
        }

        public static VisibilityScope BeginCameraVisibility(Camera camera)
        {
            if (scopes.Count > 0) scopes[^1].Restore();
            var scope = pool.Count > 0 ? pool.Pop() : new RenderScope();
            scope.token = ++nextToken;
            scopes.Add(scope);
            var view = Planned(camera);
            if (view) view.RefreshForRender();
            using (ApplyMarker.Auto())
            {
                if (view)
                {
                    foreach (var renderer in view.Visibility.Hidden) scope.Add(renderer, true);
                    foreach (var renderer in view.Visibility.Shadows)
                        if (!view.Visibility.Hidden.Contains(renderer)) scope.Add(renderer, false);
                }
                scope.Apply();
            }
            return new VisibilityScope(scope.token);
        }

        static CameraCutaway Planned(Camera camera) =>
            camera && camera.cameraType == CameraType.Game && cameras.TryGetValue(camera, out var view) && view && view.owner ? view : null;

        static void BeginRender(ScriptableRenderContext _, Camera camera) => pipelineScopes.Push(BeginCameraVisibility(camera));
        static void EndRender(ScriptableRenderContext _, Camera camera)
        {
            if (pipelineScopes.Count > 0) pipelineScopes.Pop().Dispose();
        }

        static void RestoreAll()
        {
            if (scopes.Count > 0) scopes[^1].Restore();
            foreach (var scope in scopes) { scope.states.Clear(); scope.active = false; }
            scopes.Clear();
            pipelineScopes.Clear();
        }

        public readonly struct VisibilityScope : IDisposable
        {
            readonly long token;
            internal VisibilityScope(long token) => this.token = token;
            public void Dispose()
            {
                for (int i = scopes.Count - 1; i >= 0; i--)
                    if (scopes[i].token == token) { scopes[i].Dispose(); return; }
            }
        }

        static void Unsubscribe()
        {
            if (!subscribed) return;
            RenderPipelineManager.beginCameraRendering -= BeginRender;
            RenderPipelineManager.endCameraRendering -= EndRender;
            subscribed = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            RestoreAll();
            Unsubscribe();
            cameras.Clear();
            pool.Clear();
            ReleaseRenderers();
        }

        readonly struct RendererState
        {
            readonly Renderer renderer;
            readonly bool forceOff, hide;
            readonly ShadowCastingMode shadows;
            public RendererState(Renderer renderer, bool hide)
            {
                this.renderer = renderer;
                this.hide = hide;
                forceOff = renderer.forceRenderingOff;
                shadows = renderer.shadowCastingMode;
            }
            public void Apply()
            {
                if (!renderer) return;
                if (hide || shadows == ShadowCastingMode.Off) renderer.forceRenderingOff = true;
                else renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            public void Restore()
            {
                if (!renderer) return;
                renderer.forceRenderingOff = forceOff;
                renderer.shadowCastingMode = shadows;
            }
        }

        sealed class RenderScope : IDisposable
        {
            public readonly List<RendererState> states = new(128);
            public long token;
            public bool active;
            public void Add(Renderer renderer, bool hide)
            {
                if (renderer) states.Add(new RendererState(renderer, hide));
            }
            public void Apply() { active = true; foreach (var state in states) state.Apply(); }
            public void Restore() { foreach (var state in states) state.Restore(); }
            public void Dispose()
            {
                if (!active) return;
                if (scopes.Count == 0 || scopes[^1] != this) { RestoreAll(); return; }
                Restore();
                scopes.RemoveAt(scopes.Count - 1);
                active = false;
                states.Clear();
                pool.Push(this);
                if (scopes.Count > 0) scopes[^1].Apply();
            }
        }
    }
}
