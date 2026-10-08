using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>Cosmetic imported avatar, wardrobe and canonical animation playback.</summary>
    [RequireComponent(typeof(PlayerController))]
    [DefaultExecutionOrder(90)]
    public sealed class PlayerAppearance : MonoBehaviour
    {
        const string AvatarKey = "Avatar/Avatar";
        [Tooltip("An authored avatar instance beneath Visual. Runtime initialization preserves this hierarchy and its transforms.")]
        [SerializeField] GameObject authoredAvatar;
        readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        readonly List<Renderer> oldRenderers = new List<Renderer>();
        PlayerController controller;
        GameObject model;
        SkinnedMeshRenderer[] meshes;
        Transform gripL, gripR;
        Quaternion gripCorrectionL, gripCorrectionR;
        PlayableGraph graph;
        AnimationLayerMixerPlayable layers;
        MotionTrack locomotion, upperBody;
        AvatarMask upperBodyMask;
        SkinnedMeshRenderer face;
        int blinkShape = -1, browShape = -1;
        float poseWeight, expression, blinkAt;
        string actionClip;
        bool fullBodyAction;
        float actionUntil;
        bool wasHolding;
        bool initialized;
        Outfit outfit;

        public Outfit CurrentOutfit => outfit?.Clone();
        public GameObject AuthoredAvatar => authoredAvatar;
        public GameObject AvatarModel => model;
        public Transform RightGrip => gripR;
        public string LocomotionClip => locomotion?.Current;
        public string UpperBodyClip => upperBody?.Current;
        public float UpperBodyWeight => poseWeight;

        /// <summary>The unsaved presentation look; shared rules defaults and saved player choices remain independent.</summary>
        public static Outfit DefaultPresentationOutfit()
        {
            var catalogue = GameConfig.Current.Wardrobe;
            var look = catalogue.Default.Clone();
            look.Pieces["Top"] = "Hoodie";
            look.Pieces["Headwear"] = "Hood";
            look.Pieces["Back"] = "Satchel";
            look.Colours["Gloves"] = "charcoal";
            look.Colours["Bottoms"] = "charcoal";
            return catalogue.Sanitize(look);
        }

        /// <summary>Called by PlayerController.Setup, including existing serialized prefabs.</summary>
        public bool Initialize(PlayerController player)
        {
            controller = player;
            if (initialized) return true;
            var library = ModelLibrary.Load();
            if (!controller || !controller.visual || !library || !library.Find(AvatarKey)) return false;
            model = authoredAvatar;
            if (!model)
            {
                foreach (var renderer in controller.visual.GetComponentsInChildren<Renderer>(true))
                    oldRenderers.Add(renderer);
                var root = new GameObject("ImportedAvatar");
                root.transform.SetParent(controller.visual, false);
                model = ModelVisual.Spawn(AvatarKey, root.transform);
                if (!model) { Destroy(root); return false; }
            }
            meshes = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            gripL = ModelVisual.FindNamed(model, "grip_L");
            gripR = ModelVisual.FindNamed(model, "grip_R");
            if (gripL && controller.handL) gripCorrectionL = Quaternion.Inverse(gripL.rotation) * controller.handL.rotation;
            if (gripR && controller.handR) gripCorrectionR = Quaternion.Inverse(gripR.rotation) * controller.handR.rotation;
            foreach (var mesh in meshes)
            {
                if (!mesh.sharedMesh || mesh.name != "SK_Head") continue;
                face = mesh;
                for (int i = 0; i < mesh.sharedMesh.blendShapeCount; i++)
                {
                    string shape = mesh.sharedMesh.GetBlendShapeName(i);
                    if (shape.EndsWith("Blink", System.StringComparison.OrdinalIgnoreCase)) blinkShape = i;
                    if (shape.EndsWith("BrowRelax", System.StringComparison.OrdinalIgnoreCase)) browShape = i;
                }
            }
            blinkAt = Time.time + 2.2f + controller.Index * 0.31f;
            foreach (var renderer in oldRenderers) if (renderer) renderer.enabled = false;

            foreach (string name in new[] { "Idle", "Walk_InPlace", "Run_InPlace", "Jump_Preview",
                "Hold_OneHand", "Carry_TwoHand", "Block_Plate", "Swing_OneHand", "Thrust_OneHand",
                "Throw_OneHand", "Hit_Reaction", "Celebrate", "Drink_Consumable", "Pickup", "Place" })
            {
                var clip = library.FindClip(AvatarKey, name);
                if (clip) clips[name] = clip;
            }
            var animator = model.GetComponentInChildren<Animator>(true);
            if (animator && clips.Count > 0)
            {
                animator.applyRootMotion = false;
                // Held gear uses animated sockets even when the avatar's previous skinned bounds are off screen.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create("Wreckabulary Avatar " + controller.Index);
                graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                locomotion = new MotionTrack(graph, clips);
                upperBody = new MotionTrack(graph, clips);
                layers = AnimationLayerMixerPlayable.Create(graph, 2);
                graph.Connect(locomotion.Mixer, 0, layers, 0);
                graph.Connect(upperBody.Mixer, 0, layers, 1);
                layers.SetInputWeight(0, 1f);
                layers.SetInputWeight(1, 0f);
                upperBodyMask = CreateUpperBodyMask(animator.transform);
                layers.SetLayerMaskFromAvatarMask(1, upperBodyMask);
                var output = AnimationPlayableOutput.Create(graph, "Avatar", animator);
                output.SetSourcePlayable(layers);
                graph.Play();
            }
            initialized = true;
            var catalogue = GameConfig.Current.Wardrobe;
            string saved = PlayerPrefs.GetString("wv.outfit." + controller.Index, "");
            var initial = string.IsNullOrEmpty(saved) ? DefaultPresentationOutfit() : Outfit.Deserialize(saved);
            if (string.IsNullOrEmpty(saved) && catalogue.Palettes.TryGetValue("Top", out var palette))
            {
                float nearest = float.PositiveInfinity;
                foreach (var colour in palette)
                {
                    var rgb = new Vector3(colour.R, colour.G, colour.B);
                    float distance = (rgb - new Vector3(controller.Color.r, controller.Color.g, controller.Color.b)).sqrMagnitude;
                    if (distance >= nearest) continue;
                    nearest = distance;
                    initial.Colours["Top"] = colour.Id;
                }
            }
            ApplyOutfit(catalogue.Sanitize(initial), false);
            controller.Jumped += OnJumped;
            controller.Dodged += OnDodged;
            if (controller.Health) controller.Health.Damaged += OnDamaged;
            locomotion?.Set("Idle", true);
            return true;
        }

        public void ApplyOutfit(Outfit next) => ApplyOutfit(next, true);

        void ApplyOutfit(Outfit next, bool save)
        {
            var catalogue = GameConfig.Current.Wardrobe;
            outfit = catalogue.Sanitize(next).Clone();
            if (meshes != null)
                foreach (var renderer in meshes)
                {
                    WardrobePiece piece = null;
                    foreach (var candidate in catalogue.Pieces)
                        if (candidate.Mesh == renderer.name) { piece = candidate; break; }
                    bool visible = piece == null || outfit.PieceIn(piece.Slot) == piece.Id;
                    renderer.enabled = visible;
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var block = new MaterialPropertyBlock();
                        if (visible && piece != null)
                        {
                            var colour = catalogue.ColourFor(outfit, piece.Slot);
                            string materialName = materials[i] ? materials[i].name : "";
                            bool rib = MatchesTintMaterial(materialName, "fabric_rib") && piece.TintMaterial == "fabric_main";
                            if (colour != null && (MatchesTintMaterial(materialName, piece.TintMaterial) || rib))
                            {
                                var tint = new Color(colour.R, colour.G, colour.B, 1f);
                                if (rib) tint *= .75f;
                                tint.a = 1f;
                                block.SetColor("_BaseColor", tint);
                                block.SetColor("_Color", tint);
                            }
                        }
                        renderer.SetPropertyBlock(block, i);
                    }
                }
            if (save && controller)
            {
                PlayerPrefs.SetString("wv.outfit." + controller.Index, outfit.Serialize());
                PlayerPrefs.Save();
            }
        }

        public string SkinFor(string word) => outfit?.SkinFor(word) ?? Skin.Standard;

        static bool MatchesTintMaterial(string materialName, string canonicalName) =>
            !string.IsNullOrEmpty(canonicalName) && (materialName == canonicalName ||
                materialName.StartsWith(canonicalName + "__Default_", System.StringComparison.Ordinal));

        public void SetItemSkin(string word, string skin)
        {
            if (outfit == null || !GameConfig.Current.Items.TryGet(word, out var definition)) return;
            outfit.ItemSkins[word] = Appearance.Pick(definition, skin);
            ApplyOutfit(outfit);
        }

        /// <summary>Action visuals never change damage windows, movement or item rules.</summary>
        public void Play(string clipName, float duration = .5f)
        {
            if (!clips.ContainsKey(clipName)) return;
            actionClip = clipName;
            fullBodyAction = clipName is "Jump_Preview" or "Run_InPlace" or "Hit_Reaction" or "Celebrate";
            actionUntil = Time.time + Mathf.Max(.05f, duration);
            (fullBodyAction ? locomotion : upperBody)?.Set(clipName, true);
        }

        void OnJumped(PlayerController _) => Play("Jump_Preview", .45f);
        void OnDodged(PlayerController _) => Play("Run_InPlace", .2f);

        void OnDamaged(PlayerHealth health, HitInfo hit, HitResult result)
        {
            if (result.Damage > 0f && !result.Blocked) Play("Hit_Reaction", Mathf.Clamp(result.HitStun, .15f, .4f));
        }

        void Update()
        {
            if (!initialized || !controller) return;
            bool holding = controller.Combat && controller.Combat.IsHolding;
            bool blocking = controller.Combat && controller.Combat.IsBlocking;
            if (!controller.IsKnockedOut && holding && !wasHolding) Play("Pickup", .28f);
            if (!controller.IsKnockedOut && !holding && wasHolding) Play("Throw_OneHand", .35f);
            wasHolding = holding;
            var velocity = controller.Body ? controller.Body.linearVelocity : Vector3.zero;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            bool acting = Time.time < actionUntil;
            string movement = controller.IsKnockedOut ? "Hit_Reaction"
                : acting && fullBodyAction ? actionClip
                : !controller.Grounded ? "Jump_Preview"
                : speed > 2.5f ? "Run_InPlace" : speed > .2f ? "Walk_InPlace" : "Idle";
            locomotion?.Set(movement);
            if (locomotion != null)
                locomotion.SetSpeed(movement == "Walk_InPlace" || movement == "Run_InPlace"
                    ? Mathf.Clamp(speed / (movement == "Run_InPlace" ? 5f : 2f), .4f, 1.6f) : 1f);

            string pose = controller.IsKnockedOut ? null
                : acting && !fullBodyAction ? actionClip
                : acting && fullBodyAction ? null
                : blocking ? "Block_Plate"
                : holding ? (controller.Combat.Weapon && controller.Combat.Weapon.Definition?.IsTwoHanded != true
                    ? "Hold_OneHand" : "Carry_TwoHand") : null;
            if (pose != null) upperBody?.Set(pose);
            float dt = Time.deltaTime;
            poseWeight = Mathf.MoveTowards(poseWeight, pose == null ? 0f : 1f, dt * 10f);
            if (layers.IsValid()) layers.SetInputWeight(1, poseWeight);
            locomotion?.Tick(dt);
            upperBody?.Tick(dt);
        }

        void LateUpdate()
        {
            if (!initialized || !controller) return;
            // Animated mitten transforms retain the existing combat sockets and miniature item grip offsets.
            if (gripL && controller.handL) controller.handL.SetPositionAndRotation(gripL.position, gripL.rotation * gripCorrectionL);
            if (gripR && controller.handR) controller.handR.SetPositionAndRotation(gripR.position, gripR.rotation * gripCorrectionR);
            if (!face) return;
            float now = Time.time;
            if (now > blinkAt + .18f) blinkAt = now + 2.8f + controller.Index * .23f;
            float blink = now < blinkAt ? 0f : Mathf.Sin(Mathf.Clamp01((now - blinkAt) / .18f) * Mathf.PI) * 100f;
            if (blinkShape >= 0) face.SetBlendShapeWeight(blinkShape, controller.IsKnockedOut ? 78f : blink);
            bool exerting = controller.IsDodging || controller.IsStaggered || (controller.Combat && controller.Combat.IsChanneling);
            expression = Mathf.MoveTowards(expression, exerting ? 0f : 55f, Time.deltaTime * 180f);
            if (browShape >= 0) face.SetBlendShapeWeight(browShape, expression);
        }

        static AvatarMask CreateUpperBodyMask(Transform animatorRoot)
        {
            var bones = animatorRoot.GetComponentsInChildren<Transform>(true);
            var mask = new AvatarMask { name = "Wreckabulary upper body", transformCount = bones.Length };
            for (int i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];
                string path = "";
                bool upper = false;
                for (var at = bone; at && at != animatorRoot; at = at.parent)
                {
                    path = path.Length == 0 ? at.name : at.name + "/" + path;
                    if (at.name == "spine") upper = true;
                }
                mask.SetTransformPath(i, path);
                mask.SetTransformActive(i, upper);
            }
            return mask;
        }

        /// <summary>Two live clips crossfade; transitions allocate only when the actual animation state changes.</summary>
        sealed class MotionTrack
        {
            readonly PlayableGraph graph;
            readonly Dictionary<string, AnimationClip> clips;
            readonly AnimationClipPlayable[] playing = new AnimationClipPlayable[2];
            int active;
            float blend = 1f;
            public AnimationMixerPlayable Mixer { get; }
            public string Current { get; private set; }

            public MotionTrack(PlayableGraph graph, Dictionary<string, AnimationClip> clips)
            {
                this.graph = graph;
                this.clips = clips;
                Mixer = AnimationMixerPlayable.Create(graph, 2);
            }

            public void Set(string name, bool restart = false)
            {
                if ((!restart && Current == name) || !clips.TryGetValue(name, out var clip)) return;
                int next = 1 - active;
                if (playing[next].IsValid())
                {
                    Mixer.DisconnectInput(next);
                    graph.DestroyPlayable(playing[next]);
                }
                playing[next] = AnimationClipPlayable.Create(graph, clip);
                playing[next].SetApplyFootIK(false);
                playing[next].SetTime(0);
                graph.Connect(playing[next], 0, Mixer, next);
                blend = playing[active].IsValid() ? 0f : 1f;
                Mixer.SetInputWeight(active, 1f - blend);
                Mixer.SetInputWeight(next, blend);
                active = next;
                Current = name;
            }

            public void SetSpeed(float speed)
            {
                if (playing[active].IsValid()) playing[active].SetSpeed(speed);
            }

            public void Tick(float dt)
            {
                blend = Mathf.MoveTowards(blend, 1f, dt / .14f);
                Mixer.SetInputWeight(active, blend);
                Mixer.SetInputWeight(1 - active, 1f - blend);
            }
        }

        void OnDestroy()
        {
            if (controller)
            {
                controller.Jumped -= OnJumped;
                controller.Dodged -= OnDodged;
                if (controller.Health) controller.Health.Damaged -= OnDamaged;
            }
            if (graph.IsValid()) graph.Destroy();
            if (upperBodyMask) Destroy(upperBodyMask);
        }
    }
}
