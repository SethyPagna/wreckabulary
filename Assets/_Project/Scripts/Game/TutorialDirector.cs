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
        public const string FirstWord = "BAT", FirstFurniture = "TABLE", EffectWord = "LAMP", PlayNextMode = "Dibs";
        public const float DummyHealth = 60f;
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

        class Step
        {
            public string text;
            public Func<ControlDevice, string> hint;
            public Func<string> needs;
            public Func<bool> done, skip;
            public Action enter, keep;
        }

        List<Step> steps;
        readonly HashSet<PlayerController> watched = new();
        readonly HashSet<string> spelled = new();
        readonly Dictionary<PlayerController, int> slotsAtEnter = new();
        readonly List<(int seat, ControlDevice device)> seats = new();
        Vector3[] lastPositions = new Vector3[8];
        float walked, finishedAt, nextResupply, nextTether, hintFresh;
        bool thrown, jumped, dodged, endCardShown;
        int dummyHits, hintSignature = -1;
        string hintText = "";
        Smashable table, lamp, chair;

        public PlayerController Dummy { get; private set; }
        public int StepIndex { get; private set; }
        public int StepCount => steps.Count;
        public bool Finished => StepIndex >= steps.Count;
        public bool EndCardShown => endCardShown;

        void Awake()
        {
            steps = new List<Step>
            {
                new() { text = "Walk around", hint = d => ControlWords.MoveHow(d, Key(d, PlayerControl.Move)), done = () => walked > 4f },
                new() { text = "Jump, then dodge", hint = d => ControlWords.JumpDodgeHow(d, Key(d, PlayerControl.Jump), Key(d, PlayerControl.Dodge)), done = () => jumped && dodged },
                new() { text = "Smash the TABLE", hint = d => ControlWords.SmashHow(d, Key(d, PlayerControl.Attack)), needs = NeedsFirstWord, enter = EnsureTable, done = () => !table },
                new() { text = "Pick up the letters", hint = _ => "Walk over T, A, B, L and E. They go in your bag.", needs = NeedsFirstWord, done = HasFirstWord },
                new()
                {
                    text = "Open the recipe book", hint = d => ControlWords.BookHow(d, Key(d, PlayerControl.Book)), needs = NeedsFirstWord,
                    skip = () => !ControlHints.HasBook(hud.LocalPlayer ? hud.LocalPlayer.Binding : null), done = () => hud.BagOpen
                },
                new() { text = "Spell BAT", hint = d => ControlWords.SpellHow(d, Key(d, PlayerControl.Spell), FirstWord), needs = NeedsFirstWord, done = () => Spelled(FirstWord) },
                new() { text = "Whack the dummy", hint = d => ControlWords.SwingHow(d, Key(d, PlayerControl.Attack), FirstWord), needs = Rearm, enter = EnsureDummy, done = () => dummyHits > 0 },
                new() { text = "Throw the CHAIR", hint = d => ControlWords.ThrowHow(d, Key(d, PlayerControl.Grab), Key(d, PlayerControl.Attack)), needs = Rearm, enter = EnsureChair, keep = EnsureChair, done = () => thrown },
                new()
                {
                    text = "Smash the LAMP, then spell LAMP", hint = _ => "Harder words carry effects: LAMP hits are Heavy and push people further.",
                    needs = () => Spelled(EffectWord) ? null : EffectWord, enter = EnsureLamp, done = () => Spelled(EffectWord)
                },
                new() { text = "Switch hands", hint = d => ControlWords.HandsHow(d, Key(d, PlayerControl.Hands)), needs = Rearm, enter = RememberSlots, done = SlotChanged },
                new()
                {
                    text = "Knock out the dummy", hint = _ => "Keep hitting it. In a match, the last one standing wins the round.",
                    needs = Rearm, enter = ResetDummy, done = () => Dummy && Dummy.IsKnockedOut
                },
            };
        }

        static string Key(ControlDevice device, PlayerControl control) => ControlHints.For(device, control);

        void Start()
        {
            joins.RespawnKnockedOut = true;
            SpawnDummy();
            steps[0].enter?.Invoke();
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
            if (Time.time > nextTether && Dummy && !Dummy.IsKnockedOut && !Dummy.IsStaggered && !Dummy.IsHeld)
            {
                nextTether = Time.time + 2f;
                EnsureDummy();
            }
            if (joins.Players.Count > 0 && step.skip?.Invoke() == true) Advance(false);
            else if (step.done()) Advance(true);
        }

        void Advance(bool cheer)
        {
            if (cheer) Popup.Show("NICE!", joins.Players.Count > 0 ? joins.Players[0].OverheadPosition + Vector3.up : Vector3.up * 2f, Color.white, 5f);
            StepIndex++;
            if (Finished) { finishedAt = Time.time; MarkDone(); return; }
            steps[StepIndex].enter?.Invoke();
        }

        string Hint(Step step)
        {
            seats.Clear();
            int signature = StepIndex;
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
            return hintText = ControlWords.PerDevice(seats, step.hint);
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
            Flavour = "Now try a real match of Dibs! with bots.",
            NextLabel = "PLAY DIBS! NOW →",
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
                    lastPositions[i] = p.transform.position;
                }
                var pos = p.transform.position;
                float d = World.Flat(pos - lastPositions[i]).magnitude;
                if (d < 1f) walked += d;
                lastPositions[i] = pos;
            }
        }

        bool Spelled(string word) => spelled.Contains(word);

        bool HasFirstWord() => Spelled(FirstWord) || joins.Players.Any(p => Has(p, FirstWord));

        string NeedsFirstWord() => Spelled(FirstWord) ? null : FirstWord;

        string Rearm() => joins.Players.Any(Armed) ? null : FirstWord;

        static bool Armed(PlayerController p) => p.Binding is not BotBinding && (p.Combat.Weapon || p.Combat.StoredGear);

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
            if (word == EffectWord) EnsureLamp();
            else EnsureTable();
        }

        Smashable SourceOf(string word) => word == EffectWord ? lamp : table;

        static bool Has(PlayerController p, string word) => WordSolver.CanSpell(WordSolver.Count(p.Inventory.Letters), word);

        void EnsureTable()
        {
            if (!table) table = Place(FirstFurniture, boxSpot.position);
        }

        void EnsureLamp()
        {
            if (!lamp) lamp = Place(EffectWord, boxSpot.position + Vector3.forward * 2f);
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
