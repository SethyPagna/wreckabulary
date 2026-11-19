using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;

namespace Wreckabulary.Online
{
    [DisallowMultipleComponent]
    public sealed partial class WordboundMatchHud : MonoBehaviour
    {
        WordboundGameplayController controller;
        WordboundPlayerView view;
        WordboundStoryView story;
        ItemCatalogue items;
        ModeDefinition mode;
        ResolvedCourse course;
        string actorId, search = "", notice = "", persistentNotice = "";
        double noticeUntil;
        bool showQuickTips = true;
        string returnLabel = "Return to party";
        int seat, team;
        bool initialized, crafting, journalOpen, helpOpen, defenseHeld;
        int defenseItem = -1, preparedFrame = -1;
        double nextDefenseInput;
        Vector2 recipeScroll;
        long receiptSequence, receiptEpoch = -1;
        MatchSnapshot frame;
        PlayerInventory inventory;
        MatchActorState actor;
        readonly RaycastHit[] sightHits = new RaycastHit[64];
        readonly Dictionary<string, WorldBodyState> canonicalBodies = new Dictionary<string, WorldBodyState>(StringComparer.Ordinal);
        readonly HashSet<Collider> staticSurfaces = new HashSet<Collider>();
        WordboundCourseWorld sightWorld;
        readonly HudPanel statusPanel = new HudPanel(), actionPanel = new HudPanel(),
            inventoryPanel = new HudPanel(), menuHintPanel = new HudPanel();
        bool panelsReady;
        public event Action ReturnRequested;
        public event Action PlayAgainRequested;
        public WordboundUiTheme Theme;
        public WordboundStoryProfile StoryProfile;
        readonly WordboundUiStyles ui = new WordboundUiStyles();

        public void Initialize(WordboundGameplayController gameplay, WordboundPlayerView playerView,
            ItemCatalogue catalogue, ModeDefinition definition, ResolvedCourse resolved, string localActor,
            string returnButtonLabel = "Return to party")
        {
            if (initialized) throw new InvalidOperationException("Match HUD is already initialized.");
            controller = gameplay != null ? gameplay : throw new ArgumentNullException(nameof(gameplay));
            view = playerView != null ? playerView : throw new ArgumentNullException(nameof(playerView));
            items = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
            mode = definition ?? throw new ArgumentNullException(nameof(definition));
            course = resolved ?? throw new ArgumentNullException(nameof(resolved));
            actorId = localActor;
            if (string.IsNullOrWhiteSpace(returnButtonLabel)) throw new ArgumentException("A return label is required.");
            returnLabel = returnButtonLabel;
            var binding = controller.Authority.Match.Binding;
            seat = Enumerable.Range(0, binding.Seats.Count).Single(i => binding.Seats[i].PlayerId == actorId);
            team = binding.Seats[seat].Team;
            voiceMatchId = binding.MatchId;
            JoinVoice(mode, binding.Seats, actorId);
            story = gameObject.AddComponent<WordboundStoryView>();
            story.Bind(controller, course, items, actorId, view.Camera, StoryProfile);
            controller.FrameChanged += ApplyFrame;
            controller.Faulted += ConnectionFailed;
            view.CommandsRead += ReadCommands;
            view.MenuCommandsRead += ReadMenuCommands;
            view.MenuRequested += MenuRequested;
            initialized = true;
            PrepareIntro();
            ApplyFrame(controller.Frame);
        }

        void ApplyFrame(MatchSnapshot next)
        {
            var before = frame;
            frame = next;
            canonicalBodies.Clear();
            if (next == null) { inventory = null; actor = null; return; }
            ObserveProgress(before, next);
            foreach (var body in next.Bodies) canonicalBodies.Add(body.Id, body);
            inventory = next.Economy.Players.Single(p => p.PlayerId == seat);
            RefreshRecipes();
            actor = next.Actors.Single(p => p.PlayerId == actorId);
            if (receiptEpoch != next.Epoch) { receiptEpoch = next.Epoch; receiptSequence = 0; }
            foreach (var receipt in next.Commands.Receipts.Where(r => r.ActorId == actorId && r.Sequence > receiptSequence)
                .OrderBy(r => r.Sequence))
            {
                receiptSequence = receipt.Sequence;
                if (receipt.Effect.Code != "Ok") SetNotice(RefusalText(receipt.Effect.Code));
            }
        }

        void ReadCommands(PlayerCommands input)
        {
            NoteIntroInput(input);
            if (input.spellDown)
            {
                ReleaseDefense(); crafting = true; journalOpen = false; helpOpen = false; resultsOpen = false; leaveConfirm = false;
                PrepareRecipeMenu();
                menuFocus.Reset(selectedRecipe == null ? FirstCraftableRecipe() : "recipe:" + selectedRecipe);
                revealMenuSelection = true; view.SetMenuOpen(true); return;
            }
            if (input.slot > 0) { Send(WordboundAction.Equip, slot: input.slot - 1); return; }
            if (input.swap)
            {
                if (inventory != null && actor != null && frame != null)
                {
                    int next = -1;
                    for (int slot = actor.EquippedSlot + 1; slot < inventory.Slots.Length; slot++)
                    {
                        var item = HeldItem(slot);
                        if (item == null) continue;
                        next = slot; break;
                    }
                    Send(WordboundAction.Equip, slot: next);
                }
                return;
            }
            bool shieldHeld = SelectedShield() != null && (input.blockHeld || input.attackHeld) &&
                !input.aimHeld && !input.grab && !input.drop;
            ReadDefense(shieldHeld);
            if (input.grab) Interact();
            if (input.drop) Send(WordboundAction.Drop, slot: inventory?.CarriedFurniture >= 0 ? -1 : actor?.EquippedSlot ?? -1);
            if (input.attack && !shieldHeld)
            {
                if (input.aimHeld && actor != null && actor.EquippedSlot >= 0)
                    Send(WordboundAction.Throw, slot: actor.EquippedSlot);
                else Send(WordboundAction.Attack);
            }
        }

        ItemInstance SelectedShield()
        {
            if (actor == null || inventory == null || frame == null || actor.EquippedSlot < 0 ||
                actor.EquippedSlot >= inventory.Slots.Length || inventory.Craft != null ||
                inventory.CarriedFurniture >= 0 || !inventory.CanAct || actor.Health <= 0) return null;
            int id = inventory.Slots[actor.EquippedSlot];
            var item = frame.Economy.Items.FirstOrDefault(i => i.Id == id && i.State == ItemState.Held && i.HolderId == seat);
            return item != null && items.Get(item.Word).Shield != null ? item : null;
        }

        void ReadDefense(bool held)
        {
            var item = held ? SelectedShield() : null;
            if (item == null) { ReleaseDefense(); return; }
            if (defenseHeld && defenseItem != item.Id) ReleaseDefense();
            if (Time.unscaledTimeAsDouble < nextDefenseInput) return;
            nextDefenseInput = Time.unscaledTimeAsDouble + WordboundDefensePolicy.RefreshTicks / (double)ModeDefinition.TickRate;
            if (Send(WordboundAction.Block, value: "Hold")) { defenseHeld = true; defenseItem = item.Id; }
        }

        void ReleaseDefense()
        {
            if (!defenseHeld) return;
            defenseHeld = false;
            defenseItem = -1;
            nextDefenseInput = 0;
            if (controller && controller.Frame != null) Send(WordboundAction.Block, value: "Release");
        }

        void Update()
        {
            if (!initialized) return;
            ExecuteMenuAction(Application.isFocused);
            ShowResultsOnce();
            if (eyeHeld && (!view || view.MenuOpen || !Application.isFocused)) ReleaseEye();
            if (voicePressed != VoiceNone && (!view || view.MenuOpen || !Application.isFocused)) ReleaseVoice();
            if (!defenseHeld) return;
            var shield = SelectedShield();
            if (!view || view.MenuOpen || !Application.isFocused || controller.IsSimulationPaused ||
                shield == null || defenseItem != shield.Id) ReleaseDefense();
        }

        void MenuRequested()
        {
            ReleaseDefense(); crafting = false; helpOpen = false; leaveConfirm = false; ResetMenuInput();
            if (!double.IsNaN(introShownAt)) CloseIntro();
            if (view.MenuOpen) PrepareRecipeMenu();
        }
        void ConnectionFailed(string reason)
            => ShowFailure("The session stopped. Choose " + returnLabel + " to continue.");
        public void ShowFailure(string message)
        {
            ReleaseDefense();
            persistentNotice = message ?? "";
            SetNotice("");
            crafting = false;
            view.SetMenuOpen(true);
        }
        bool Send(WordboundAction action, string target = "", int revision = 0, int slot = -1, string value = "")
        {
            if (action != WordboundAction.Block) ReleaseDefense();
            try
            {
                controller.Submit(action, target, revision, slot, value);
                if (action != WordboundAction.Block) SetNotice("");
                if (action == WordboundAction.Attack) showQuickTips = false;
                return true;
            }
            catch (InvalidOperationException) { SetNotice("Waiting for the connection. Try again shortly."); return false; }
        }

        WordboundNetworkBody Target()
        {
            if (view.Player == null || view.Player.Object == null || !view.Player.Object.IsValid ||
                view.Player.Rigidbody == null || frame == null ||
                !canonicalBodies.TryGetValue(view.Player.BodyId.ToString(), out var playerState) ||
                playerState.Kind != WorldBodyKind.Player || playerState.SourceId != actorId || !playerState.Active)
                return null;
            WordboundNetworkBody best = null;
            float score = float.PositiveInfinity;
            Vector3 origin = view.Player.Rigidbody.position;
            foreach (var body in controller.PresentedBodies)
            {
                if (!body || body.Object == null || !body.Object.IsValid || !body.BodyActive || body.Rigidbody == null ||
                    body.Runner != view.Player.Runner || !Interactable(body.BodyKind)) continue;
                float squared = (body.Rigidbody.position - origin).sqrMagnitude;
                if (squared > 9f || !canonicalBodies.TryGetValue(body.BodyId.ToString(), out var state) ||
                    !state.Active || body.BodyKind != (int)state.Kind || !mode.CanInteract(team, state.OwnerTeam, state.Protected)) continue;
                var screen = view.SteadyViewportPoint(body.Rigidbody.position + Vector3.up * .35f);
                if (screen.z <= 0 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) continue;
                float candidate = squared + Mathf.Abs(screen.x - .5f) * 3;
                if (candidate >= score || !VisibleFromPlayer(playerState, body)) continue;
                best = body; score = candidate;
            }
            return best;
        }

        static bool Interactable(int kind) => kind == (int)WorldBodyKind.Item || kind == (int)WorldBodyKind.Tile ||
            kind == (int)WorldBodyKind.Keepsake;

        ItemInstance ItemFor(string bodyId)
        {
            if (frame == null || bodyId == null || !bodyId.StartsWith("item:", StringComparison.Ordinal) ||
                !int.TryParse(bodyId.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out int id)) return null;
            foreach (var item in frame.Economy.Items)
                if (item.Id == id && bodyId == "item:" + item.Id) return item;
            return null;
        }

        bool VisibleFromPlayer(WorldBodyState playerState, WordboundNetworkBody target)
        {
            Vector3 from = view.Player.Rigidbody.position, to = target.Rigidbody.position;
            if (controller.PhysicsWorld != null)
                return controller.PhysicsWorld.HasLineOfSight(actorId,
                    new WorldVector(from.x, from.y, from.z), new WorldVector(to.x, to.y, to.z));
            var world = controller.CourseWorld;
            if (!world) return false;
            var scene = world.gameObject.scene.GetPhysicsScene();
            if (!scene.IsValid() || scene == Physics.defaultPhysicsScene ||
                view.Player.gameObject.scene.GetPhysicsScene() != scene || target.gameObject.scene.GetPhysicsScene() != scene)
                return false;
            if (sightWorld != world)
            {
                staticSurfaces.Clear();
                foreach (var surface in world.Surfaces) staticSurfaces.Add(surface);
                sightWorld = world;
            }
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < .000001f) return true;
            int count = scene.Raycast(from + Vector3.up * .75f, delta.normalized, sightHits,
                delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count >= sightHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var collider = sightHits[i].collider;
                if (!collider) continue;
                if (staticSurfaces.Contains(collider)) return false;
                var other = collider.attachedRigidbody ? collider.attachedRigidbody.GetComponent<WordboundNetworkBody>() : null;
                if (!other || other.Object == null || !other.Object.IsValid || other.Runner != view.Player.Runner ||
                    !canonicalBodies.TryGetValue(other.BodyId.ToString(), out var state) ||
                    other.BodyKind != (int)state.Kind || !WordboundPhysicsPolicy.ShouldCollide(mode, playerState, state)) continue;
                if (other == view.Player || (other.Rigidbody.position - to).sqrMagnitude < .0001f) continue;
                return false;
            }
            return true;
        }

        void Interact()
        {
            var target = Target();
            if (target == null)
            {
                var nearby = WordboundPracticeGuidePlan.Nearest(course, frame, actorId);
                if (nearby != null && frame.Practice.SiteHash != nearby.Hash && !WordboundPracticeGuidePlan.Completed(frame, nearby))
                    Send(WordboundAction.PracticePuzzle, nearby.Hash);
                else SetNotice(nearby != null ? WordboundPracticeGuidePlan.Status(frame, nearby) : "Move closer to a letter, object or keepsake.");
                return;
            }
            string id = target.BodyId.ToString();
            switch ((WorldBodyKind)target.BodyKind)
            {
                case WorldBodyKind.Tile: Send(WordboundAction.CollectLetter, id); break;
                case WorldBodyKind.Keepsake: Send(WordboundAction.TakeKeepsake); break;
                case WorldBodyKind.Item:
                    var item = ItemFor(id);
                    if (item == null) return;
                    var puzzle = InteractionPuzzle(item);
                    if (puzzle != null) Send(WordboundAction.UsePuzzle, puzzle.Id, value: id);
                    else Send(WordboundAction.PickUp, id, item.Revision);
                    break;
            }
        }

        void OnGUI()
        {
            if (!initialized) return;
            Matrix4x4 saved = GUI.matrix;
            bool enabled = GUI.enabled;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), .6f, 1.8f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float width = Screen.width / scale, height = Screen.height / scale;
            try
            {
                if (!panelsReady || preparedFrame != Time.frameCount) PreparePanels();
                if (view.MenuOpen) Menu(width, height, Application.isFocused);
                else
                {
                    DrawOverlay(width, height);
                }
            }
            finally { GUI.matrix = saved; GUI.enabled = enabled; }
        }

        string Hint(WordboundControl control, string fallback)
        {
            string hint = view.ControlHint(control);
            return string.IsNullOrEmpty(hint) ? fallback : "[" + hint + "] " + fallback;
        }

        sealed class HudPanel
        {
            readonly List<GUIContent> rows = new List<GUIContent>();
            readonly List<Rect> rects = new List<Rect>();
            int count;
            float measuredWidth = -1, measuredHeight;
            bool dirty = true;
            GUIStyle measuredLabel, measuredBox;
            public void Begin() { count = 0; }
            public void Add(string text)
            {
                if (count == rows.Count) { rows.Add(new GUIContent()); rects.Add(default); dirty = true; }
                if (rows[count].text != text) { rows[count].text = text; dirty = true; }
                count++;
            }
            public Rect DrawCompact(float anchor, float y, float maximumWidth, GUIStyle label, GUIStyle box, bool bottom, float alignment)
            {
                if (count == 0) return new Rect(anchor, y, 0, 0);
                float width = 0;
                for (int i = 0; i < count; i++) width = Mathf.Max(width, label.CalcSize(rows[i]).x);
                width = Mathf.Min(maximumWidth, Mathf.Ceil(width + box.padding.horizontal + 4));
                return Draw(anchor - width * alignment, y, width, label, box, bottom);
            }
            int measuredCount = -1;
            public Rect Draw(float x, float y, float width, GUIStyle label, GUIStyle box, bool bottom = false)
            {
                if (dirty || measuredCount != count || measuredWidth != width || measuredLabel != label || measuredBox != box)
                {
                    float offset = box.padding.top;
                    float inner = Mathf.Max(1, width - box.padding.horizontal);
                    for (int i = 0; i < count; i++)
                    {
                        float rowHeight = label.CalcHeight(rows[i], inner);
                        rects[i] = new Rect(box.padding.left, offset, inner, rowHeight);
                        offset += rowHeight + (i + 1 < count ? 2 : 0);
                    }
                    measuredHeight = offset + box.padding.bottom;
                    measuredWidth = width; measuredCount = count; measuredLabel = label; measuredBox = box; dirty = false;
                }
                var area = new Rect(x, bottom ? y - measuredHeight : y, width, measuredHeight);
                GUI.Box(area, GUIContent.none, box);
                for (int i = 0; i < count; i++)
                {
                    var row = rects[i]; row.position += area.position;
                    GUI.Label(row, rows[i], label);
                }
                return area;
            }
        }

        string ObjectiveText(TeamProgress progress) => CourseGuidance.Race(course, progress, actorId, frame).Text;

        string ResultText(MatchResult result) => !result.Confirmed || frame == null ? "Confirming result…" :
            CourseGuidance.Summary(mode, course, frame, actorId, result.Outcome, result.WinningTeams).Title;

        static string RefusalText(string code) => code switch
        {
            "OutOfReach" => "Move closer and keep the object in sight.",
            "MissingLetters" => "Collect the missing letters first.",
            "HandsFull" => "Free a slot or put down the furniture.",
            "Capacity" => "Object limit reached. Try again after unused objects clear.",
            "BlockedPlacement" => "That spot is blocked. Move to a clear space.",
            "BlockedRelease" => "Make room in front of you before putting the item down.",
            "EmptySlot" => "That hand is empty.",
            "NotAlive" => "Wait until you recover.",
            "StaleRevision" => "The object changed. Try again.",
            "TooEarly" => "Wait a moment before trying again.",
            "OutOfOrder" => "Complete the current puzzle first.",
            "Unavailable" => "That action is unavailable here.",
            "AlreadySolved" => "Completed this session. Start a fresh session to repeat it.",
            "NoShield" => "Select a shield, finish your current action, and try again.",
            "Blocking" => "Lower your shield before attacking.",
            _ => "That action could not be completed."
        };

        void OnDisable() { ReleaseDefense(); ResetMenuInput(); ReleaseEye(); ReleaseVoice(); }

        void OnDestroy()
        {
            ui.Dispose();
            DisposeGuidance();
            DisposeLookEye();
            LeaveVoice();
            if (controller) { controller.FrameChanged -= ApplyFrame; controller.Faulted -= ConnectionFailed; }
            if (view)
            {
                view.CommandsRead -= ReadCommands; view.MenuRequested -= MenuRequested;
                view.MenuCommandsRead -= ReadMenuCommands;
            }
        }
    }
}
