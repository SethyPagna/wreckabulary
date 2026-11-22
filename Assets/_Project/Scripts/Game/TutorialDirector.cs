using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public class TutorialDirector : MonoBehaviour
    {
        public const string DoneKey = "wv.tutorial.done";
        public const string FirstWord = TutorialSteps.FirstWord, FirstFurniture = TutorialSteps.FirstFurniture, EffectWord = TutorialSteps.EffectWord, PlayNextMode = "Dibs";
        public const string PlaceWord = TutorialSteps.PlaceWord, HealWord = TutorialSteps.HealWord, ShieldWord = TutorialSteps.ShieldWord;
        public const float DummyHealth = 60f, DummyJab = 10f, ReturnLinger = 1.5f;
        const float PunchEvery = 1.4f, PunchWindup = 0.3f, PunchReach = 1.5f, PunchArc = 120f, PunchRange = 1.7f;
        const float JabPush = 1f, KnockoutPush = 3f, KnockoutPunch = 2f, HurtBy = 0.5f;
        const int FallbackRespawnSeconds = 2;
        static readonly Vector3 LampOffset = Vector3.forward * 2f, AppleOffset = Vector3.right * 1.8f, PlateOffset = Vector3.right * 1.8f + Vector3.back * 1.6f;
        public static bool Done => PlayerPrefs.GetInt(DoneKey, 0) == 1;

        public static void MarkDone()
        {
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
        }

        [SerializeField] PlayerJoinManager joins;
        [SerializeField] GameHud hud;
        [SerializeField] Transform boxSpot;
        [SerializeField] Transform chairSpot;
        [SerializeField] Transform dummySpot;
        [SerializeField] float endCardDelay = 1.5f;

        enum Sparring { Off, UntilHurt, AtShields, Knockout }

        class Step
        {
            public TutorialStep kind;
            public string text;
            public Func<ControlDevice, string> hint;
            public Func<string> needs;
            public Func<bool> done, skip;
            public Func<int> beat;
            public Action enter, keep;
            public Sparring sparring;
        }

        List<Step> steps;
        readonly HashSet<PlayerController> watched = new();
        readonly HashSet<string> spelled = new();
        readonly Dictionary<PlayerController, int> slotsAtEnter = new();
        readonly Dictionary<PlayerController, int> placedAtEnter = new();
        readonly HashSet<PlayerController> fallen = new();
        readonly List<(int seat, ControlDevice device)> seats = new();
        Vector3[] lastPositions = new Vector3[8];
        float[] lastHealth = new float[8];
        float walked, finishedAt, nextResupply, nextTether, hintFresh, stepStartedAt, nextPunch, backAt = -1f;
        bool thrown, jumped, dodged, endCardShown, healed, blocked;
        int dummyHits, hintSignature = -1;
        string hintText = "";
        Smashable table, lamp, chair, apple, plate;

        public PlayerController Dummy { get; private set; }
        public int StepIndex { get; private set; }
        public int StepCount => steps.Count;
        public bool Finished => StepIndex >= steps.Count;
        public bool EndCardShown => endCardShown;
        public TutorialStep Current => steps[Mathf.Min(StepIndex, steps.Count - 1)].kind;
        public float GiveUpSeconds { get; set; } = TutorialSteps.GiveUpSeconds;

        void Awake()
        {
            for (int i = 0; i < lastHealth.Length; i++) lastHealth[i] = -1f;
            var built = new Dictionary<TutorialStep, Step>
            {
                [TutorialStep.Walk] = new() { hint = d => ControlWords.MoveHow(d, Key(d, PlayerControl.Move)), done = () => walked > 4f },
                [TutorialStep.JumpDodge] = new() { hint = d => ControlWords.JumpDodgeHow(d, Key(d, PlayerControl.Jump), Key(d, PlayerControl.Dodge)), done = () => jumped && dodged },
                [TutorialStep.Smash] = new() { hint = d => ControlWords.SmashHow(d, Key(d, PlayerControl.Attack)), needs = NeedsFirstWord, enter = EnsureTable, done = () => !table },
                [TutorialStep.Collect] = new() { hint = _ => "Walk over T, A, B, L and E. They go in your bag.", needs = NeedsFirstWord, done = HasFirstWord },
                [TutorialStep.Book] = new()
                {
                    hint = d => ControlWords.BookHow(d, Key(d, PlayerControl.Book)), needs = NeedsFirstWord,
                    skip = () => !ControlHints.HasBook(hud.LocalPlayer ? hud.LocalPlayer.Binding : null), done = () => hud.BagOpen
                },
                [TutorialStep.Spell] = new() { hint = d => ControlWords.SpellHow(d, Key(d, PlayerControl.Spell), FirstWord), needs = NeedsFirstWord, done = () => Spelled(FirstWord) },
                [TutorialStep.Hit] = new() { hint = d => ControlWords.SwingHow(d, Key(d, PlayerControl.Attack), FirstWord), needs = Rearm, enter = EnsureDummy, done = () => dummyHits > 0 },
                [TutorialStep.Throw] = new() { hint = d => ControlWords.ThrowHow(d, Key(d, PlayerControl.Grab), Key(d, PlayerControl.Attack)), needs = Rearm, enter = EnsureChair, keep = EnsureChair, done = () => thrown },
                [TutorialStep.Place] = new()
                {
                    hint = d => TutorialSteps.PlaceHow(d, Key(d, PlayerControl.Attack), PlaceWord),
                    needs = () => Holding(PlaceWord) ? null : PlaceWord, enter = BeginPlace, done = Placed
                },
                [TutorialStep.Heal] = new()
                {
                    hint = d => TutorialSteps.HealHow(d, Key(d, PlayerControl.Attack), HealWord, HealAmount(), AnyHurt()),
                    needs = () => Holding(HealWord) ? null : HealWord, beat = () => AnyHurt() ? 1 : 0,
                    enter = BeginHeal, done = () => healed, sparring = Sparring.UntilHurt
                },
                [TutorialStep.Effect] = new()
                {
                    hint = _ => "Harder words carry effects: LAMP hits are Heavy and push people further.",
                    needs = () => Spelled(EffectWord) ? null : EffectWord, enter = EnsureLamp, done = () => Spelled(EffectWord)
                },
                [TutorialStep.Hands] = new() { hint = d => ControlWords.HandsHow(d, Key(d, PlayerControl.Hands)), needs = Rearm, enter = RememberSlots, done = SlotChanged },
                [TutorialStep.KnockedOut] = new()
                {
                    hint = d => TutorialSteps.KnockedOutHow(d, Key(d, PlayerControl.Grab), Beat(), RespawnSeconds()),
                    beat = () => (int)Beat(), enter = BeginKnockout, keep = NoteReturn,
                    done = () => backAt >= 0f && Time.time - backAt >= ReturnLinger, sparring = Sparring.Knockout
                },
                [TutorialStep.Block] = new()
                {
                    hint = d => TutorialSteps.BlockHow(d, Key(d, PlayerControl.Block), ShieldWord),
                    needs = () => Shielded() ? null : ShieldWord, enter = BeginBlock, done = () => blocked, sparring = Sparring.AtShields
                },
                [TutorialStep.Finish] = new()
                {
                    hint = _ => "Keep hitting it. In a match, the last one standing wins the round.",
                    needs = Rearm, enter = ResetDummy, done = () => Dummy && Dummy.IsKnockedOut
                },
            };
            steps = new List<Step>();
            foreach (var kind in TutorialSteps.Order)
            {
                var step = built[kind];
                step.kind = kind;
                step.text = TutorialSteps.Text(kind);
                steps.Add(step);
            }
        }

        static string Key(ControlDevice device, PlayerControl control) => ControlHints.For(device, control);

        void Start()
        {
            joins.RespawnKnockedOut = true;
            SpawnDummy();
            Enter();
        }

        void Update()
        {
            Watch();
            if (Finished) { Finish(); return; }

            var step = steps[StepIndex];
            if (joins.Players.Count == 0) hud.SetInstruction(ControlHints.Join("join"), "");
            else hud.SetInstruction($"<color=#FFD24A>{StepIndex + 1}/{steps.Count}</color>  {step.text}", Hint(step));

            Resupply(step);
            step.keep?.Invoke();
            Spar(step);
            if (Time.time > nextTether && Dummy && !Dummy.IsKnockedOut && !Dummy.IsStaggered && !Dummy.IsHeld)
            {
                nextTether = Time.time + 2f;
                EnsureDummy();
            }
            if (joins.Players.Count > 0 && (step.skip?.Invoke() == true || GaveUp(step))) Advance(false);
            else if (step.done()) Advance(true);
        }

        void Advance(bool cheer)
        {
            if (cheer) Popup.Show("NICE!", joins.Players.Count > 0 ? joins.Players[0].OverheadPosition + Vector3.up : Vector3.up * 2f, Color.white, 5f);
            StepIndex++;
            if (Finished) { finishedAt = Time.time; MarkDone(); return; }
            Enter();
        }

        void Enter()
        {
            stepStartedAt = Time.time;
            steps[StepIndex].enter?.Invoke();
        }

        bool At(TutorialStep kind) => !Finished && steps[StepIndex].kind == kind;

        bool GaveUp(Step step) => TutorialSteps.GivesUp(step.kind) && Time.time - stepStartedAt > GiveUpSeconds;

        public bool GiveUp()
        {
            if (Finished || !TutorialSteps.GivesUp(steps[StepIndex].kind)) return false;
            Advance(false);
            return true;
        }

        string Hint(Step step)
        {
            seats.Clear();
            int signature = StepIndex * 31 + (step.beat?.Invoke() ?? 0);
            for (int i = 0; i < joins.Players.Count; i++)
            {
                var p = joins.Players[i];
                if (p.Binding is BotBinding) continue;
                var device = ControlHints.Device(p.Binding);
                seats.Add((p.Index + 1, device));
                signature = signature * 31 + p.Index * 8 + (int)device;
            }
            if (signature == hintSignature && Time.unscaledTime < hintFresh) return hintText;
            hintSignature = signature;
            hintFresh = Time.unscaledTime + 1f;
            Func<ControlDevice, string> line = step.hint;
            if (HandsFull(step)) line = FullHands;
            return hintText = ControlWords.PerDevice(seats, line);
        }

        static string FullHands(ControlDevice device) => TutorialSteps.HandsFullHow(device, Key(device, PlayerControl.Drop));

        bool HandsFull(Step step)
        {
            if (step.needs?.Invoke() == null) return false;
            bool anyone = false;
            foreach (var p in joins.Players)
            {
                if (p.Binding is BotBinding) continue;
                if (p.Combat.HasFreeGearSlot || p.Summoner.IsCrafting) return false;
                anyone = true;
            }
            return anyone;
        }

        void Finish()
        {
            if (endCardShown)
            {
                if (hud.ResultShown && Time.time - finishedAt > endCardDelay + 1.6f && joins.AnyStartPressed()) hud.ConfirmResult();
                return;
            }
            hud.SetInstruction("You're ready!", "");
            if (Time.time - finishedAt < endCardDelay) return;
            endCardShown = true;
            hud.SetInstruction("", "");
            hud.ShowResult(EndCard(), PlayNext, () => Session.GoHome());
        }

        public static HudResult EndCard() => new HudResult
        {
            Round = 1, Final = true, Won = true, Stats = false,
            Eyebrow = "TUTORIAL COMPLETE",
            Heading = "You're ready!",
            Flavour = $"Now try a real match of {HouseModes.Name(PlayNextMode)} with bots.",
            NextLabel = "PLAY A MATCH NOW →",
            HomeLabel = "Back to the lobby",
        };

        static void PlayNext() => Session.LoadMode(PlayNextMode);

        void Watch()
        {
            for (int i = 0; i < joins.Players.Count; i++)
            {
                var p = joins.Players[i];
                if (watched.Add(p))
                {
                    p.Summoner.Summoned += w => spelled.Add(w);
                    p.Combat.Thrown += _ => thrown = true;
                    p.Jumped += _ => jumped = true;
                    p.Dodged += _ => dodged = true;
                    p.Health.Damaged += (_, _, result) => { if (result.Blocked) blocked = true; };
                    p.Health.KnockedOut += _ => { if (At(TutorialStep.KnockedOut) && p.Binding is not BotBinding) fallen.Add(p); };
                    lastPositions[i] = p.transform.position;
                }
                var pos = p.transform.position;
                float d = World.Flat(pos - lastPositions[i]).magnitude;
                if (d < 1f) walked += d;
                lastPositions[i] = pos;

                float health = p.Health.IsAlive ? p.Health.Current : -1f;
                if (lastHealth[i] >= 0f && health > lastHealth[i] + HurtBy) healed = true;
                lastHealth[i] = health;
            }
        }

        IEnumerable<PlayerController> Humans => joins.Players.Where(p => p.Binding is not BotBinding);

        bool Spelled(string word) => spelled.Contains(word);

        bool HasFirstWord() => Spelled(FirstWord) || joins.Players.Any(p => Has(p, FirstWord));

        string NeedsFirstWord() => Spelled(FirstWord) ? null : FirstWord;

        string Rearm() => joins.Players.Any(Armed) ? null : FirstWord;

        static bool Armed(PlayerController p) => p.Binding is not BotBinding && (Fights(p.Combat.Weapon) || Fights(p.Combat.StoredGear));

        static bool Fights(HeldWeapon gear) => gear && (gear.Definition == null || gear.Definition.Melee != null);

        static bool IsShield(HeldWeapon gear) => gear && gear.Shield != null;

        static bool Holds(HeldWeapon gear, string word) => gear && gear.word == word;

        bool Holding(string word) => Humans.Any(p => Holds(p.Combat.Weapon, word) || Holds(p.Combat.StoredGear, word));

        bool Shielded() => Humans.Any(p => IsShield(p.Combat.Weapon) || IsShield(p.Combat.StoredGear));

        bool AnyHurt()
        {
            foreach (var p in joins.Players)
                if (p.Binding is not BotBinding && p.Health.IsAlive && p.Health.Current < p.Health.Max - HurtBy) return true;
            return false;
        }

        static int HealAmount() =>
            GameConfig.Current.Items.TryGet(HealWord, out var item) && item.Use != null ? Mathf.RoundToInt(item.Use.Amount) : 0;

        void BeginPlace()
        {
            placedAtEnter.Clear();
            foreach (var p in Humans) placedAtEnter[p] = DeployedGear.CountFor(p);
            EnsureTable();
        }

        bool Placed()
        {
            foreach (var p in joins.Players)
            {
                if (p.Binding is BotBinding) continue;
                int now = DeployedGear.CountFor(p);
                if (!placedAtEnter.TryGetValue(p, out int before)) before = 0;
                if (now > before) return true;
                placedAtEnter[p] = now;
            }
            return false;
        }

        void BeginHeal()
        {
            healed = false;
            EnsureApple();
        }

        void BeginKnockout()
        {
            fallen.Clear();
            backAt = -1f;
        }

        KnockoutBeat Beat() => fallen.Count == 0 ? KnockoutBeat.Standing : backAt >= 0f ? KnockoutBeat.Back : KnockoutBeat.Down;

        void NoteReturn()
        {
            if (backAt >= 0f) return;
            foreach (var p in fallen)
                if (p && !p.IsKnockedOut) { backAt = Time.time; return; }
        }

        static int RespawnSeconds()
        {
            float seconds = Match.Rules.RespawnSeconds;
            return seconds > 0f ? Mathf.CeilToInt(seconds) : FallbackRespawnSeconds;
        }

        void BeginBlock()
        {
            blocked = false;
            EnsurePlate();
        }

        void Spar(Step step)
        {
            if (step.sparring == Sparring.Off || !Dummy || !Dummy.CanAct || Time.time < nextPunch) return;
            if (step.sparring == Sparring.UntilHurt && AnyHurt()) return;
            if (step.sparring == Sparring.Knockout && fallen.Count > 0) return;
            bool knockout = step.sparring == Sparring.Knockout;
            var target = PunchTarget(step.sparring == Sparring.AtShields, knockout);
            if (!target) return;
            nextPunch = Time.time + PunchEvery;
            var fist = Dummy.Health.Rules.Unarmed;
            fist.Damage = knockout ? target.Health.Max * KnockoutPunch : DummyJab;
            fist.Knockback = knockout ? KnockoutPush : JabPush;
            Dummy.FaceTowards(target.transform.position - Dummy.transform.position);
            Dummy.Combat.Attack();
        }

        PlayerController PunchTarget(bool shieldsOnly, bool knockout)
        {
            PlayerController best = null;
            float bestSq = PunchRange * PunchRange;
            foreach (var p in joins.Players)
            {
                if (p.Binding is BotBinding || p.IsKnockedOut || p.IsHeld || p.Health.IsInvulnerable) continue;
                if (shieldsOnly && !IsShield(p.Combat.Weapon)) continue;
                if (!knockout && p.Health.Current <= DummyJab) continue;
                float distanceSq = World.Flat(p.transform.position - Dummy.transform.position).sqrMagnitude;
                if (distanceSq > bestSq) continue;
                bestSq = distanceSq;
                best = p;
            }
            return best;
        }

        void Resupply(Step step)
        {
            if (Time.time < nextResupply) return;
            nextResupply = Time.time + 1f;
            string word = step.needs?.Invoke();
            if (word == null || SourceOf(word) || joins.Players.Any(p => p.Summoner.IsCrafting && p.Summoner.CraftWord == word)) return;
            var letters = new List<char>();
            if (TilePool.Instance) letters.AddRange(TilePool.Instance.Active.Select(t => t.Letter));
            foreach (var p in joins.Players) letters.AddRange(p.Inventory.Letters);
            if (WordSolver.CanSpell(WordSolver.Count(letters), word)) return;
            Supply(word);
        }

        Smashable SourceOf(string word) => word == EffectWord ? lamp : word == HealWord ? apple : word == ShieldWord ? plate : table;

        void Supply(string word)
        {
            if (word == EffectWord) EnsureLamp();
            else if (word == HealWord) EnsureApple();
            else if (word == ShieldWord) EnsurePlate();
            else EnsureTable();
        }

        static bool Has(PlayerController p, string word) => WordSolver.CanSpell(WordSolver.Count(p.Inventory.Letters), word);

        void EnsureTable()
        {
            if (!table) table = Place(FirstFurniture, boxSpot.position);
        }

        void EnsureLamp()
        {
            if (!lamp) lamp = Place(EffectWord, boxSpot.position + LampOffset);
        }

        void EnsureApple()
        {
            if (!apple) apple = Place(HealWord, boxSpot.position + AppleOffset);
        }

        void EnsurePlate()
        {
            if (!plate) plate = Place(ShieldWord, boxSpot.position + PlateOffset);
        }

        Smashable Place(string word, Vector3 at) => FurnitureCatalog.Spawn(word, at + Vector3.up * 0.5f, 0f, transform);

        void EnsureChair()
        {
            if (chair) return;
            chair = Furniture.Create(transform, "CHAIR", chairSpot.position + Vector3.up * 0.5f, 20f, Vector3.one * 0.4f, 3,
                                     new Color(0.71f, 0.51f, 0.35f), 4f, 40f);
        }

        void RememberSlots()
        {
            slotsAtEnter.Clear();
            foreach (var p in joins.Players)
                if (p.Binding is not BotBinding) slotsAtEnter[p] = p.Combat.ActiveSlot;
        }

        bool SlotChanged() => joins.Players.Any(p => slotsAtEnter.TryGetValue(p, out int slot) && p.Combat.ActiveSlot != slot);

        void SpawnDummy()
        {
            var prefab = GameAssets.I.playerPrefab;
            Dummy = Instantiate(prefab, dummySpot.position, Quaternion.identity, transform);
            Dummy.Setup(9, null, new Color(0.72f, 0.70f, 0.66f), '?', "DUMMY");
            Dummy.Health.UseRules(DummyRules());
            Dummy.Respawn(dummySpot.position);
            Dummy.FaceTowards(Vector3.back);
            Dummy.Inventory.Set("DUMMY");
            Dummy.Inventory.Collects = false;
            Dummy.Health.Hit += (_, attacker) => { if (attacker) dummyHits++; };
            Dummy.Health.KnockedOut += _ => { if (!Finished) Invoke(nameof(EnsureDummy), 1.5f); };
        }

        static GameRules DummyRules()
        {
            var rules = Match.Rules.Clone();
            rules.SpawnProtectionSeconds = 0f;
            rules.DownedEnabled = false;
            rules.MaxHealth = DummyHealth;
            rules.ReviveHealth = Mathf.Min(rules.ReviveHealth, DummyHealth);
            rules.Unarmed.Damage = DummyJab;
            rules.Unarmed.Windup = PunchWindup;
            rules.Unarmed.Reach = PunchReach;
            rules.Unarmed.ArcDegrees = PunchArc;
            rules.Unarmed.Knockback = JabPush;
            rules.Unarmed.BreakPower = 0f;
            return rules;
        }

        void EnsureDummy()
        {
            if (!Dummy || Finished) return;
            bool wandered = World.Flat(Dummy.transform.position - dummySpot.position).magnitude > 2.5f;
            if (!Dummy.IsKnockedOut && !wandered) return;
            ResetDummy();
        }

        void ResetDummy()
        {
            if (!Dummy || Finished) return;
            Dummy.Combat.ResetForRound();
            Dummy.Health.ResetForRound();
            Dummy.Respawn(dummySpot.position);
            Dummy.FaceTowards(Vector3.back);
            Dummy.Inventory.Set("DUMMY");
        }
    }
}
