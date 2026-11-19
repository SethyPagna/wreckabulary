using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class WordboundPlayerView : MonoBehaviour
    {
        public WordboundCameraProfile Profile;
        readonly RaycastHit[] obstructions = new RaycastHit[64];
        readonly CourseLook look = new CourseLook();
        WordboundGameplayController controller;
        WordboundNetworkBody player;
        InputBinding binding, effectiveBinding;
        IDisposable ownedBinding;
        Camera view;
        GameObject ownedCamera;
        CursorLockMode previousLock;
        bool previousCursor, initialized, capturedCursor, jumpPending, placed, orbitPlaced;
        Vector2 movement;
        Vector3 steadyPosition;
        Quaternion steadyRotation = Quaternion.identity;
        float yaw, pitch = ShoulderView.DefaultPitch, distance = ShoulderView.Distance, orbitDistance = ShoulderView.Distance;
        float followDistance = ShoulderView.Distance, minimumDistance = ShoulderView.MinDistance;
        float pivotHeight = ShoulderView.PivotHeight, lift = ShoulderView.Lift;
        float probeRadius = ShoulderView.ProbeRadius, skin = ShoulderView.Skin, outEase = ShoulderView.OutEase;
        float minimumPitch = ShoulderView.MinPitch, maximumPitch = ShoulderView.MaxPitch;
        float sensitivity = ShoulderView.MouseSensitivity, stickYaw = 2.6f, stickPitch = 1.8f;
        WordboundCameraProfile activeProfile;
        string profileError;
        int bindingRevision;
        public bool MenuOpen { get; private set; } = true;
        public Camera Camera => view;
        public WordboundNetworkBody Player => player;
        public bool UsesGamepad => effectiveBinding is WordboundInputBinding automatic &&
            automatic.ActiveGamepad != null && automatic.ActiveGamepad.added && automatic.ActiveGamepad.enabled;
        public string ControlHint(WordboundControl control) => WordboundInputBinding.ControlHint(effectiveBinding, control);
        public string LookAroundHint => effectiveBinding is WordboundInputBinding automatic ? automatic.LookAroundHint : "";
        public bool Looking => look.Looking;
        public bool Dragging => look.Dragging;
        public float OrbitYaw => look.Yaw;
        public float OrbitPitch => look.Pitch;
        public void BeginLook() => look.BeginLook();
        public void EndLook() => look.EndLook();
        public void Orbit(float yawDegrees, float pitchDegrees) =>
            look.Orbit(yawDegrees, pitchDegrees, pitch * Mathf.Rad2Deg, minimumPitch * Mathf.Rad2Deg, maximumPitch * Mathf.Rad2Deg);
        public void OrbitDrag(Vector2 screenPixels) => Orbit(FreeLook.DragDegrees(screenPixels.x, Screen.height),
            FreeLook.DragDegrees(KeyBindings.InvertY ? screenPixels.y : -screenPixels.y, Screen.height));
        public Vector3 SteadyViewportPoint(Vector3 world) => view == null ? Vector3.zero :
            look.Orbiting && placed ? ViewportPoint(view, steadyPosition, steadyRotation, world) : view.WorldToViewportPoint(world);
        public event Action<PlayerCommands> CommandsRead;
        public event Action<WordboundMenuCommands> MenuCommandsRead;
        public event Action MenuRequested;

        public void Initialize(WordboundGameplayController gameplay, Camera camera = null, InputBinding controls = null,
            WordboundCameraProfile cameraProfile = null)
        {
            if (initialized) throw new InvalidOperationException("Player view is already initialized.");
            controller = gameplay != null ? gameplay : throw new ArgumentNullException(nameof(gameplay));
            if (controls == null)
            {
                var automatic = new WordboundInputBinding();
                controls = automatic;
                ownedBinding = automatic;
            }
            binding = controls;
            effectiveBinding = controls;
            if (controls is DesktopBinding desktop) effectiveBinding = new WordboundInputBinding(desktop);
            else if (controls is GamepadBinding gamepad) effectiveBinding = new WordboundInputBinding(gamepad);
            if (!ReferenceEquals(effectiveBinding, controls)) ownedBinding = (IDisposable)effectiveBinding;
            view = camera;
            if (view == null)
            {
                ownedCamera = new GameObject("Wordbound player camera");
                view = ownedCamera.AddComponent<Camera>();
                view.clearFlags = CameraClearFlags.Skybox;
                GraphicsOptions.ApplyTo(view);
            }
            if (cameraProfile) Profile = cameraProfile;
            Profile?.Validate();
            ApplyCameraProfile();
            previousLock = Cursor.lockState;
            previousCursor = Cursor.visible;
            capturedCursor = true;
            initialized = true;
        }

        void ApplyCameraProfile()
        {
            try { if (Profile) Profile.Validate(); }
            catch (ArgumentException error)
            {
                if (profileError != error.Message) Debug.LogWarning("Wordbound camera profile: " + error.Message, this);
                profileError = error.Message;
                return;
            }
            profileError = null;
            view.fieldOfView = Profile ? Profile.FieldOfView : ShoulderView.FieldOfView;
            view.nearClipPlane = Profile ? Profile.NearClip : ShoulderView.NearClip;
            view.farClipPlane = Profile ? Profile.FarClip : 1800f;
            followDistance = Profile ? Profile.Distance : ShoulderView.Distance;
            minimumDistance = Profile ? Profile.MinDistance : ShoulderView.MinDistance;
            pivotHeight = Profile ? Profile.PivotHeight : ShoulderView.PivotHeight;
            lift = Profile ? Profile.Lift : ShoulderView.Lift;
            probeRadius = Profile ? Profile.ProbeRadius : ShoulderView.ProbeRadius;
            skin = Profile ? Profile.Skin : ShoulderView.Skin;
            outEase = Profile ? Profile.OutEase : ShoulderView.OutEase;
            minimumPitch = Profile ? Profile.MinPitch : ShoulderView.MinPitch;
            maximumPitch = Profile ? Profile.MaxPitch : ShoulderView.MaxPitch;
            sensitivity = Profile ? Profile.MouseSensitivity : ShoulderView.MouseSensitivity;
            stickYaw = Profile ? Profile.StickYawSpeed : 2.6f;
            stickPitch = Profile ? Profile.StickPitchSpeed : 1.8f;
            if (activeProfile != Profile)
            {
                pitch = Profile ? Profile.DefaultPitch : ShoulderView.DefaultPitch;
                activeProfile = Profile;
                placed = false;
            }
            pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);
        }

        public void LocalPlayerChanged(WordboundNetworkBody body)
        {
            player = body;
            placed = false;
            orbitPlaced = false;
            look.Recenter();
            movement = Vector2.zero;
            jumpPending = false;
            if (body != null) yaw = body.transform.eulerAngles.y * Mathf.Deg2Rad;
        }

        public void SetMenuOpen(bool open)
        {
            MenuOpen = open;
            movement = Vector2.zero;
            jumpPending = false;
            look.Hold(false);
            (effectiveBinding as WordboundInputBinding)?.SuspendGameplay();
            if (open) CommandsRead?.Invoke(default);
            ApplyCursor();
        }

        void ApplyCursor()
        {
            if (!initialized) return;
            bool lockMouse = effectiveBinding.ReadsMouse && !MenuOpen && Application.isFocused && player != null;
            Cursor.lockState = lockMouse ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockMouse;
        }

        void Update()
        {
            if (!initialized) return;
            ApplyCameraProfile();
            ProcessInput(Application.isFocused);
        }

        void ProcessInput(bool focused)
        {
            look.Hold(false);
            if (effectiveBinding is WordboundInputBinding automatic)
            {
                bool lost = automatic.RefreshDevice(focused);
                if (bindingRevision != automatic.Revision)
                {
                    bindingRevision = automatic.Revision;
                    movement = Vector2.zero;
                    jumpPending = false;
                    CommandsRead?.Invoke(default);
                    if (MenuOpen && !lost) MenuRequested?.Invoke();
                    ApplyCursor();
                }
                if (lost) { SetMenuOpen(true); MenuRequested?.Invoke(); }
            }
            if (!focused)
            {
                movement = Vector2.zero;
                jumpPending = false;
                (effectiveBinding as WordboundInputBinding)?.SuspendGameplay();
                return;
            }
            bool pause = effectiveBinding.StartPressed();
            if (pause)
            {
                SetMenuOpen(!MenuOpen);
                MenuRequested?.Invoke();
                return;
            }
            if (MenuOpen)
            {
                movement = Vector2.zero;
                jumpPending = false;
                if (effectiveBinding is WordboundInputBinding menuBinding)
                    MenuCommandsRead?.Invoke(menuBinding.ReadMenu(Time.unscaledTime));
                return;
            }
            if (player == null || controller.IsSimulationPaused)
            {
                movement = Vector2.zero;
                jumpPending = false;
                (effectiveBinding as WordboundInputBinding)?.SuspendGameplay();
                return;
            }
            ApplyCursor();
            var commands = default(PlayerCommands);
            effectiveBinding.Read(ref commands);
            Steer(commands, Time.unscaledDeltaTime);
        }

        void Steer(PlayerCommands commands, float seconds)
        {
            look.Hold(commands.lookAround);
            if (commands.lookAround) Orbit(commands.orbit.x * Mathf.Rad2Deg, commands.orbit.y * Mathf.Rad2Deg);
            else if (effectiveBinding.ReadsMouse)
            {
                float scale = sensitivity / ShoulderView.MouseSensitivity;
                yaw += commands.lookDelta.x * scale;
                pitch += commands.lookDelta.y * scale;
            }
            else
            {
                yaw += commands.look.x * stickYaw * seconds;
                pitch -= commands.look.y * stickPitch * seconds;
            }
            pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);
            yaw = Mathf.Repeat(yaw + Mathf.PI, 2 * Mathf.PI) - Mathf.PI;
            movement = ShoulderView.CameraRelative(Vector2.ClampMagnitude(commands.move, 1), yaw);
            jumpPending |= commands.jump;
            CommandsRead?.Invoke(commands);
        }

        public WordboundMovementInput ReadMovement()
        {
            if (!initialized || MenuOpen || !Application.isFocused || player == null || controller.IsSimulationPaused ||
                DesktopBinding.Typing || KeyBindings.GameplayBlocked)
            {
                jumpPending = false;
                return default;
            }
            return Pending();
        }

        WordboundMovementInput Pending()
        {
            var input = new WordboundMovementInput {
                MoveX = movement.x, MoveZ = movement.y, Jump = jumpPending,
                HasFacing = true, FacingYaw = Mathf.Repeat(yaw * Mathf.Rad2Deg + 180f, 360f) - 180f
            };
            jumpPending = false;
            return input;
        }

        void LateUpdate()
        {
            if (!initialized) return;
            look.Update(Time.unscaledDeltaTime, pitch * Mathf.Rad2Deg, minimumPitch * Mathf.Rad2Deg, maximumPitch * Mathf.Rad2Deg);
            if (view == null || player == null || player.Object == null || !player.Object.IsValid) return;
            var visual = player.GetComponent<WordboundGameplayBody>()?.VisualRoot;
            Vector3 feet = visual != null ? visual.position : player.transform.position;
            Vector3 pivot = feet + Vector3.up * (pivotHeight + lift);
            Vector3 forward = ShoulderView.Forward(yaw, pitch);
            var scene = player.gameObject.scene.GetPhysicsScene();
            if (!scene.IsValid() || scene == Physics.defaultPhysicsScene) return;
            float allowed = Clearance(scene, pivot, forward);
            distance = !placed || allowed < distance ? allowed : Ease(distance, allowed);
            steadyPosition = pivot - forward * distance;
            steadyRotation = Quaternion.LookRotation(forward, Vector3.up);
            placed = true;
            if (!look.Orbiting)
            {
                orbitPlaced = false;
                view.transform.SetPositionAndRotation(steadyPosition, steadyRotation);
                return;
            }
            Vector3 turned = ShoulderView.Forward(yaw + look.Yaw * Mathf.Deg2Rad, pitch + look.Pitch * Mathf.Deg2Rad);
            float room = Clearance(scene, pivot, turned);
            if (!orbitPlaced) orbitDistance = distance;
            orbitDistance = room < orbitDistance ? room : Ease(orbitDistance, room);
            orbitPlaced = true;
            view.transform.SetPositionAndRotation(pivot - turned * orbitDistance, Quaternion.LookRotation(turned, Vector3.up));
        }

        float Clearance(PhysicsScene scene, Vector3 pivot, Vector3 forward)
        {
            float allowed = followDistance;
            int count = scene.SphereCast(pivot, probeRadius, -forward, obstructions,
                followDistance, ~0, QueryTriggerInteraction.Ignore);
            if (count == obstructions.Length) allowed = minimumDistance;
            else
                for (int i = 0; i < count; i++)
                {
                    var collider = obstructions[i].collider;
                    if (collider == null || collider.attachedRigidbody == player.Rigidbody) continue;
                    allowed = Mathf.Min(allowed, obstructions[i].distance - skin);
                }
            return Mathf.Clamp(allowed, minimumDistance, followDistance);
        }

        float Ease(float from, float to) => Mathf.Lerp(from, to, 1 - Mathf.Exp(-outEase * Time.unscaledDeltaTime));

        static Vector3 ViewportPoint(Camera camera, Vector3 position, Quaternion rotation, Vector3 world)
        {
            Vector3 local = Quaternion.Inverse(rotation) * (world - position);
            Vector4 clip = camera.projectionMatrix * new Vector4(local.x, local.y, -local.z, 1f);
            if (Mathf.Approximately(clip.w, 0f)) return new Vector3(.5f, .5f, local.z);
            return new Vector3(clip.x / clip.w * .5f + .5f, clip.y / clip.w * .5f + .5f, local.z);
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && initialized) { SetMenuOpen(true); MenuRequested?.Invoke(); }
        }

        void OnDisable()
        {
            movement = Vector2.zero;
            jumpPending = false;
            look.Clear();
            orbitPlaced = false;
            (effectiveBinding as WordboundInputBinding)?.SuspendGameplay();
            CommandsRead?.Invoke(default);
            if (capturedCursor)
            {
                Cursor.lockState = previousLock;
                Cursor.visible = previousCursor;
            }
        }

        void OnDestroy()
        {
            ownedBinding?.Dispose();
            ownedBinding = null;
            if (ownedCamera != null) Destroy(ownedCamera);
        }
    }
}
