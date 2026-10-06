using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Rules;
using System.Linq;

namespace Wreckabulary
{
    /// <summary>
    /// The match camera. One person playing with a mouse or a controller gets the browser edition's centred
    /// third-person view (<see cref="ShoulderView"/>) in every scene. Couch players share a view that frames
    /// them all; a lone touch or half-keyboard player keeps a closer overhead view; a scene without a house
    /// layout keeps its own pose and drifts a little towards the action. Big hits shake it.
    /// </summary>
    [DefaultExecutionOrder(-60)] // turns before the players read their look, places after they move
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }

        [SerializeField] float follow = 0.12f;
        [SerializeField] float soloDistance = .5f;
        [SerializeField] float soloFieldOfView = 40f;

        /// <summary>The third-person view shakes less: the camera is close, so the same hit reads bigger.</summary>
        const float ThirdPersonShake = .3f;
        /// <summary>How long furniture stays faded after it stops hiding the player.</summary>
        const float FadeHold = .15f;

        Vector3 basePosition, smooth;
        Quaternion baseRotation;
        bool baseOrthographic;
        float baseFieldOfView, baseNearClip;
        float shake;
        bool framesLayout;
        Vector3 layoutCentre;
        float wholeHouseSize;
        float layoutWidth, layoutDepth;
        Camera lens;

        readonly ShoulderView view = new();
        PlayerController target, followed;
        Vector3 lastFeet;
        readonly RaycastHit[] blockers = new RaycastHit[16];
        /// <summary>Furniture faded out of the way of the view: its own shadow mode, and when it may come back.</summary>
        readonly Dictionary<Renderer, (ShadowCastingMode mode, float until)> faded = new();
        readonly List<Renderer> unfade = new();

        /// <summary>The player the third-person view follows; null in the shared and overhead views.</summary>
        public PlayerController Target => target;
        public bool IsThirdPerson => target;
        /// <summary>How far behind the pivot the third-person camera sits now, after any pull-in.</summary>
        public float ViewDistance => view.CurrentDistance;
        /// <summary>The furniture renderers faded right now because they hid the player.</summary>
        public IReadOnlyCollection<Renderer> Faded => faded.Keys;

        /// <summary>A person playing here: not a bot, and not a script (tests; the tutorial dummy has no binding).</summary>
        public static bool IsHuman(PlayerController p) =>
            p && p.Binding != null && p.Binding is not BotBinding && p.Binding is not ScriptedBinding;

        /// <summary>Follows this player in third person whoever plays it (tests); null goes back to choosing.</summary>
        public void Follow(PlayerController player) => followed = player;

        public void FrameLayout(HouseLayout layout)
        {
            var camera = lens = GetComponent<Camera>();
            float minX = layout.Rooms.Min(r => r.MinX), maxX = layout.Rooms.Max(r => r.MaxX);
            float minZ = layout.Rooms.Min(r => r.MinZ), maxZ = layout.Rooms.Max(r => r.MaxZ);
            var centre = new Vector3((minX + maxX) * .5f, 0f, (minZ + maxZ) * .5f);
            layoutWidth = maxX - minX;
            layoutDepth = maxZ - minZ;
            camera.orthographic = true;
            float aspect = Mathf.Max(.5f, camera.aspect);
            camera.orthographicSize = HouseSize(aspect);
            layoutCentre = centre;
            wholeHouseSize = camera.orthographicSize;
            basePosition = smooth = centre + new Vector3(0f, 30f, -22f);
            if (!target)
            {
                transform.position = basePosition;
                transform.LookAt(centre);
            }
            baseRotation = Quaternion.LookRotation(centre - basePosition);
            baseOrthographic = true;
            framesLayout = true;
        }

        void Awake()
        {
            Instance = this;
            basePosition = smooth = transform.position;
            baseRotation = transform.rotation;
            lens = GetComponent<Camera>();
            if (lens)
            {
                baseOrthographic = lens.orthographic;
                baseFieldOfView = lens.fieldOfView;
                baseNearClip = lens.nearClipPlane;
                // The browser edition's deep teal surrounds the house, so the edges blend into the HUD instead of a void.
                lens.clearFlags = CameraClearFlags.SolidColor;
                lens.backgroundColor = new Color32(0x17, 0x3a, 0x3d, 0xff);
            }
            GraphicsOptions.ApplyTo(lens);
        }

        void OnDisable()
        {
            // The lobby stage and the workshop take the camera: the player walks the old way until it's back.
            if (target) target.ShooterView = false;
            target = null;
            RestoreFaded();
            CursorPolicy.Apply(false);
        }

        /// <summary>Orthographic size that shows the whole house with a slim margin.</summary>
        float HouseSize(float aspect) => Mathf.Max(layoutWidth / aspect, layoutDepth * .85f) * .5f + .8f;

        public static void Shake(float amount)
        {
            if (Instance) Instance.shake = Mathf.Max(Instance.shake, amount);
        }

        /// <summary>The one human player who can turn the view, or the player a test asked for. Couch play has none.</summary>
        PlayerController ChooseTarget()
        {
            if (followed && followed.isActiveAndEnabled) return followed;
            PlayerController only = null;
            foreach (var p in World.Players)
            {
                if (!IsHuman(p)) continue;
                if (only) return null;
                only = p;
            }
            return only && only.Binding.CanLook ? only : null;
        }

        void SetTarget(PlayerController next)
        {
            bool wasThirdPerson = target;
            if (target) target.ShooterView = false;
            target = next;
            if (target)
            {
                target.ShooterView = true;
                target.ResetLook();
                lastFeet = target.transform.position;
                view.Snap();
            }
            else
            {
                RestoreFaded();
                CursorPolicy.Apply(false);
                if (wasThirdPerson && lens)
                {
                    // Back to the scene's own view, gliding out from where the camera was.
                    smooth = transform.position;
                    transform.rotation = baseRotation;
                    lens.orthographic = baseOrthographic;
                    lens.fieldOfView = baseFieldOfView;
                    lens.nearClipPlane = baseNearClip;
                }
            }
            // Walls stand full height around a third-person camera and stay low under the overhead one.
            var rooms = FindAnyObjectByType<RoomBuilder>();
            if (rooms) rooms.SetTallWalls(target);
            if (StoreyCutaway.Instance) StoreyCutaway.Instance.Refresh();
        }

        void LateUpdate()
        {
            var next = ChooseTarget();
            if (next != target) SetTarget(next);
            if (target) FollowTarget();
            else Overview();
        }

        void FollowTarget()
        {
            // A knocked-out body tumbles away; the view stays where they fell.
            if (!target.IsEliminated) lastFeet = target.transform.position;
            float dt = Time.unscaledDeltaTime;
            if (lens) view.Place(lens, lastFeet, target.LookYaw, target.LookPitch, dt);
            if (shake > 0f)
            {
                var t = transform;
                t.position += (t.right * (Random.value - .5f) + t.up * (Random.value - .5f)) * (shake * ThirdPersonShake);
            }
            shake = Mathf.MoveTowards(shake, 0f, dt * 1.2f);
            FadeBlockers();
            var hud = GameHud.Active;
            bool mouse = target.Binding != null && target.Binding.ReadsMouse;
            CursorPolicy.Apply(CursorPolicy.WantsLock(mouse, hud && hud.NeedsPointer, Application.isFocused));
        }

        /// <summary>
        /// Furniture between the camera and the player fades to its shadow and comes back once it's out of the
        /// way. Walls never fade: the camera pulls in in front of them instead.
        /// </summary>
        void FadeBlockers()
        {
            float now = Time.unscaledTime;
            var from = transform.position;
            var to = lastFeet + Vector3.up * .9f - from;
            float length = to.magnitude;
            if (length > .01f)
            {
                int n = Physics.RaycastNonAlloc(from, to / length, blockers, length, World.GroundMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var body = blockers[i].rigidbody;
                    if (!body || body.GetComponentInParent<PlayerController>()) continue;
                    foreach (var r in body.GetComponentsInChildren<Renderer>())
                    {
                        if (faded.TryGetValue(r, out var was)) faded[r] = (was.mode, now + FadeHold);
                        else
                        {
                            faded[r] = (r.shadowCastingMode, now + FadeHold);
                            r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                        }
                    }
                }
            }
            unfade.Clear();
            foreach (var pair in faded)
                if (pair.Value.until < now) unfade.Add(pair.Key);
            foreach (var r in unfade)
            {
                if (r) r.shadowCastingMode = faded[r].mode;
                faded.Remove(r);
            }
        }

        void RestoreFaded()
        {
            foreach (var pair in faded)
                if (pair.Key) pair.Key.shadowCastingMode = pair.Value.mode;
            faded.Clear();
        }

        void Overview()
        {
            CursorPolicy.Apply(false);
            var centre = Vector3.zero;
            int n = 0;
            int localCount = 0;
            PlayerController localPlayer = null;
            Vector3 low = Vector3.positiveInfinity, high = Vector3.negativeInfinity;
            foreach (var p in World.Players)
            {
                if (!p || p.IsEliminated) continue;
                centre += p.transform.position;
                n++;
                // The tutorial's dummy has no binding: it isn't someone at the couch.
                if (p.Binding != null && p.Binding is not BotBinding)
                {
                    localCount++; localPlayer = p;
                    low = Vector3.Min(low, p.transform.position); high = Vector3.Max(high, p.transform.position);
                }
            }
            var goal = basePosition;
            if (framesLayout)
            {
                // A lone player who can't turn the view (touch, half a keyboard) gets a closer perspective
                // view. Couch players share a view that frames them all, close when they're together and
                // never wider than the whole house.
                bool followLocal = localCount == 1;
                float aspect = lens ? Mathf.Max(.5f, lens.aspect) : 16f / 9f;
                wholeHouseSize = HouseSize(aspect);
                var focus = followLocal ? localPlayer.transform.position : layoutCentre;
                float size = wholeHouseSize;
                if (localCount > 1)
                {
                    var spread = high - low;
                    // Room for the HUD columns and a step of space around everyone.
                    size = Mathf.Clamp(Mathf.Max((spread.x + 9f) / aspect, (spread.z + 7f) * .85f) * .5f + 1f, 7.5f, wholeHouseSize);
                    var middle = (low + high) * .5f;
                    // Near the whole-house size, settle on the house centre so the edges don't wobble.
                    focus = Vector3.Lerp(middle, layoutCentre, Mathf.InverseLerp(wholeHouseSize * .7f, wholeHouseSize, size));
                }
                // Upstairs, look at the floor you're on rather than the ground.
                var cutaway = StoreyCutaway.Instance;
                focus.y = cutaway ? cutaway.FocusY : 0f;
                // The solo overhead view looks along the same direction, so world-aligned controls and
                // pointer aim read the same as the couch view.
                goal = focus + new Vector3(0f, 30f, -22f) * (followLocal ? soloDistance : 1f);
                if (lens)
                {
                    lens.orthographic = !followLocal;
                    if (followLocal) lens.fieldOfView = soloFieldOfView;
                    lens.orthographicSize = Mathf.Lerp(lens.orthographicSize, size,
                        1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
                }
            }
            else if (n > 0)
            {
                centre /= n;
                goal += new Vector3(centre.x * follow, 0f, centre.z * follow * 0.6f);
            }
            smooth = Vector3.Lerp(smooth, goal, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
            transform.position = smooth + Random.insideUnitSphere * shake;
            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 1.5f);
        }
    }
}
