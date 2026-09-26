using TMPro;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// Physics-driven movement for a blobby roommate. The body stays upright; a separate visual
    /// leans, squashes and bobs so it feels floppy (the roadmap's fallback for full active ragdoll).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(-50)] // read input before combat and spelling run
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] float moveSpeed = 6.5f;
        [SerializeField] float acceleration = 50f;
        [SerializeField] float turnSpeed = 900f;
        [SerializeField] float extraGravity = 14f;

        [Header("Rig")]
        public Transform visual;
        public Transform handL, handR, holdPoint, overheadPoint;
        public Renderer[] bodyRenderers;
        public Renderer[] sweaterRenderers;
        public TextMeshPro initialLabel;

        public int Index { get; private set; }
        public Color Color { get; private set; } = Color.white;
        public string Name => $"P{Index + 1}";
        public InputBinding Binding { get; set; }
        public PlayerCommands Commands;

        public Rigidbody Body { get; private set; }
        public PlayerHealth Health { get; private set; }
        public LetterInventory Inventory { get; private set; }
        public PlayerCombat Combat { get; private set; }
        public Summoner Summoner { get; private set; }

        public Vector3 Facing { get; private set; } = Vector3.forward;
        public bool IsKnockedOut { get; private set; }
        public bool IsHeld { get; private set; }
        public bool Frozen { get; set; }
        public bool Grounded { get; private set; }
        public float MoveScale { get; set; } = 1f;
        public bool IsStaggered => Time.time < staggerUntil;
        public bool CanAct => !Frozen && !IsKnockedOut && !IsHeld && !IsStaggered;
        public Vector3 OverheadPosition => transform.position + Vector3.up * 2.1f;

        float staggerUntil, boostUntil, boost = 1f, slipperyUntil, floatyUntil;
        Collider[] colliders;
        Transform homeParent;

        // Visual wobble state
        Vector3 lean, leanVel;
        float squash, squashVel, walkCycle, punchT;
        bool punchRight;
        Vector3 handLRest, handRRest;
        bool wasGrounded;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Health = GetComponent<PlayerHealth>();
            Inventory = GetComponent<LetterInventory>();
            Combat = GetComponent<PlayerCombat>();
            Summoner = GetComponent<Summoner>();
            colliders = GetComponents<Collider>();
            if (handL) handLRest = handL.localPosition;
            if (handR) handRRest = handR.localPosition;
        }

        void OnEnable() => World.Players.Add(this);
        void OnDisable() => World.Players.Remove(this);

        public void Setup(int index, InputBinding binding)
        {
            Index = index;
            Binding = binding;
            name = $"Player {index + 1}";
            Color = GameAssets.I.PlayerColor(index);
            var sweater = Color.Lerp(Color, new Color(0.2f, 0.12f, 0.1f), 0.35f);
            foreach (var r in bodyRenderers) r.material.color = Color;
            foreach (var r in sweaterRenderers) r.material.color = sweater;
            if (initialLabel)
            {
                initialLabel.text = GameAssets.I.PlayerInitial(index).ToString();
                initialLabel.color = new Color(0.97f, 0.92f, 0.82f);
            }
        }

        void Update()
        {
            if (Binding != null)
            {
                Commands = default;
                Binding.Read(ref Commands);
            }
            AnimateRig();
        }

        void FixedUpdate()
        {
            Grounded = Physics.CheckSphere(transform.position + Vector3.up * 0.3f, 0.36f, World.GroundMask, QueryTriggerInteraction.Ignore);
            if (IsKnockedOut || IsHeld) return;

            if (!Grounded)
            {
                // Wings give a floaty glide, otherwise fall a bit faster than default gravity for snappy hops.
                float g = Time.time < floatyUntil ? -6f : extraGravity;
                Body.AddForce(Vector3.down * g, ForceMode.Acceleration);
            }

            var input = Frozen ? Vector3.zero : new Vector3(Commands.move.x, 0f, Commands.move.y);
            float speed = moveSpeed * MoveScale * (Time.time < boostUntil ? boost : 1f);
            float accel = acceleration
                        * (IsStaggered ? 0.1f : 1f)
                        * (Time.time < slipperyUntil ? 0.1f : 1f)
                        * (Grounded ? 1f : 0.35f);

            var v = Body.linearVelocity;
            var h = Vector3.MoveTowards(new Vector3(v.x, 0f, v.z), input * speed, accel * Time.fixedDeltaTime);
            Body.linearVelocity = new Vector3(h.x, v.y, h.z);

            if (input.sqrMagnitude > 0.01f && !IsStaggered)
                Facing = Vector3.RotateTowards(Facing, input.normalized, turnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
        }

        // ---- Things other systems do to the player ----

        /// <summary>Adds a velocity kick and briefly weakens control.</summary>
        public void Knock(Vector3 velocityChange, float stagger)
        {
            Body.linearVelocity += velocityChange;
            staggerUntil = Mathf.Max(staggerUntil, Time.time + stagger);
            squashVel = -7f;
        }

        public void Stun(float seconds) => staggerUntil = Mathf.Max(staggerUntil, Time.time + seconds);

        public void Launch(Vector3 velocity)
        {
            Body.linearVelocity = velocity;
            Grounded = false;
            squashVel = 6f;
        }

        public void Boost(float multiplier, float seconds) { boost = multiplier; boostUntil = Time.time + seconds; }
        public void MakeSlippery(float seconds) => slipperyUntil = Time.time + seconds;
        public void MakeFloaty(float seconds) => floatyUntil = Time.time + seconds;

        public void FaceTowards(Vector3 dir)
        {
            dir = World.Flat(dir);
            if (dir.sqrMagnitude > 0.001f) Facing = dir.normalized;
        }

        public void SetKnockedOut(bool knockedOut, Vector3 push = default)
        {
            IsKnockedOut = knockedOut;
            if (knockedOut)
            {
                Body.constraints = RigidbodyConstraints.None;
                var axis = Vector3.Cross(Vector3.up, push.sqrMagnitude > 0.01f ? push.normalized : Random.onUnitSphere);
                Body.AddTorque(axis * 10f + Random.insideUnitSphere * 3f, ForceMode.VelocityChange);
                Body.linearVelocity += push * 0.5f + Vector3.up * 3f;
            }
            else
            {
                Body.constraints = RigidbodyConstraints.FreezeRotation;
                Body.angularVelocity = Vector3.zero;
                transform.rotation = Quaternion.identity;
                Body.rotation = Quaternion.identity;
            }
        }

        /// <summary>Picked up (or put down) by another player.</summary>
        public void SetHeld(bool held, Transform at = null)
        {
            if (held == IsHeld) return;
            IsHeld = held;
            Body.isKinematic = held;
            foreach (var c in colliders) c.enabled = !held;
            if (held)
            {
                homeParent = transform.parent;
                transform.SetParent(at, false);
                transform.localPosition = Vector3.down * 0.3f;
                transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            else
            {
                transform.SetParent(homeParent, true);
                if (!IsKnockedOut)
                {
                    transform.rotation = Quaternion.identity;
                    Body.rotation = Quaternion.identity;
                }
            }
        }

        /// <summary>Puts the player back on their feet for a new round or after a lobby knockout.</summary>
        public void Respawn(Vector3 position)
        {
            SetHeld(false);
            SetKnockedOut(false);
            transform.position = position;
            Body.position = position;
            Body.linearVelocity = Vector3.zero;
            staggerUntil = boostUntil = slipperyUntil = floatyUntil = 0f;
            MoveScale = 1f;
            FaceTowards(-position);
        }

        public void PlayPunch()
        {
            punchT = 1f;
            punchRight = !punchRight;
        }

        // ---- Visual wobble ----

        void AnimateRig()
        {
            if (!visual) return;
            float dt = Time.deltaTime;
            var v = Body.linearVelocity;
            var hv = new Vector3(v.x, 0f, v.z);

            if (Grounded && !wasGrounded) squashVel = -4f;
            wasGrounded = Grounded;

            if (!IsKnockedOut && !IsHeld)
            {
                var leanTarget = Vector3.ClampMagnitude(hv * 2.4f, 16f);
                lean = Vector3.SmoothDamp(lean, leanTarget, ref leanVel, 0.12f);
                var tilt = lean.sqrMagnitude > 0.0001f
                    ? Quaternion.AngleAxis(lean.magnitude, Vector3.Cross(Vector3.up, lean.normalized))
                    : Quaternion.identity;
                visual.rotation = tilt * Quaternion.LookRotation(Facing);
            }

            squashVel += (-squash * 180f - squashVel * 10f) * dt;
            squash += squashVel * dt;
            visual.localScale = new Vector3(1f - squash * 0.5f, 1f + squash, 1f - squash * 0.5f);

            float speed01 = Mathf.Clamp01(hv.magnitude / moveSpeed);
            walkCycle += hv.magnitude * dt * 2.2f;
            visual.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(walkCycle)) * 0.07f * speed01);

            if (!handL || !handR) return;
            punchT = Mathf.MoveTowards(punchT, 0f, dt * 5f);
            float jab = Mathf.Sin(punchT * Mathf.PI) * 0.55f;
            bool holding = Combat && Combat.IsHolding;
            var restL = holding ? new Vector3(-0.28f, 0.95f, 0.45f) : handLRest;
            var restR = holding ? new Vector3(0.28f, 0.95f, 0.45f) : handRRest;
            float swing = Mathf.Sin(walkCycle) * 0.14f * speed01;
            handL.localPosition = restL + Vector3.forward * (swing + (!punchRight ? jab : 0f)) + (!punchRight ? Vector3.up * jab * 0.4f : Vector3.zero);
            handR.localPosition = restR + Vector3.forward * (-swing + (punchRight ? jab : 0f)) + (punchRight ? Vector3.up * jab * 0.4f : Vector3.zero);
        }
    }
}
