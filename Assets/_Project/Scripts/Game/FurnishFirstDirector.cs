using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// Versus variation "Furnish First": everyone races to get the same checklist of objects into their own
    /// corner. Boxes of those words drop in the middle, so everyone fights over the same letters. Anything in
    /// your corner counts, so stealing other people's furniture is fair game. First to win 2 rounds takes it.
    /// </summary>
    public class FurnishFirstDirector : MonoBehaviour
    {
        [Serializable]
        public class Zone
        {
            public Vector2 xRange, zRange;
            public bool Contains(Vector3 p) => p.x >= xRange.x && p.x <= xRange.y && p.z >= zRange.x && p.z <= zRange.y;
            public Vector3 Centre => new((xRange.x + xRange.y) * 0.5f, 0f, (zRange.x + zRange.y) * 0.5f);
        }

        public enum State { Waiting, Countdown, Playing, RoundOver, MatchOver }

        [SerializeField] PlayerJoinManager joins;
        [SerializeField] GameHud hud;
        [Tooltip("One corner per player slot, in player order.")]
        [SerializeField] Zone[] zones;
        [Tooltip("Boxes drop in this area, in the middle of the room.")]
        [SerializeField] Vector2 dropX = new(-1.8f, 1.8f), dropZ = new(-4.5f, 4.5f);
        [SerializeField] string[] pool = { "LAMP", "CHAIR", "TV", "RUG", "PLANT", "STOOL", "CLOCK", "VASE", "BOOKS", "TEDDY", "FAN", "MUG", "CUP", "BOWL", "PILLOW" };
        [SerializeField] int itemsPerRound = 3;
        [SerializeField] int roundsToWin = 2;
        [SerializeField] float countdownTime = 3f;
        [SerializeField] float roundOverTime = 4f;
        [SerializeField] float matchOverTime = 6f;
        [SerializeField] float dropEvery = 2.5f;

        readonly Dictionary<PlayerController, int> wins = new();
        readonly List<WordEntry> checklistWords = new();
        readonly StringBuilder sb = new();
        float stateStarted, nextDrop, nextCheck;
        int lastCount;

        public State Current { get; private set; }
        public int Round { get; private set; }
        public List<string> Checklist { get; } = new();
        public PlayerController LastWinner { get; private set; }
        public IReadOnlyList<Zone> Zones => zones;
        public float CountdownTime { get => countdownTime; set => countdownTime = value; }
        public int WinsOf(PlayerController p) => p && wins.TryGetValue(p, out int w) ? w : 0;

        /// <summary>Called by the scene builder.</summary>
        public void Configure(Zone[] newZones) => zones = newZones;

        void Start()
        {
            Music.Play(Track.Brawl);
            hud.MoveChecklistToTop(); // every corner is somebody's, so keep the list over the back wall
            joins.RespawnKnockedOut = true;
            joins.StarterLetters = 0;
            joins.Joined += OnJoined;
            foreach (var p in joins.Players) OnJoined(p);
            SetState(State.Waiting);
            if (joins.RestoredFromSession && joins.Players.Count >= 2) StartMatch();
        }

        void OnJoined(PlayerController p)
        {
            wins[p] = 0;
            // Only this round's checklist can be spelled; the list is refilled each round.
            p.Summoner.WordsOverride = checklistWords;
        }

        void SetState(State s)
        {
            Current = s;
            stateStarted = Time.time;
        }

        public void StartMatch()
        {
            foreach (var p in joins.Players) wins[p] = 0;
            Round = 0;
            joins.AllowJoining = false;
            StartRound();
        }

        void StartRound()
        {
            Round++;
            SummonedThing.ClearAll();
            World.ClearTransient();
            if (TilePool.Instance) TilePool.Instance.ReleaseAll();

            Checklist.Clear();
            Checklist.AddRange(pool.OrderBy(_ => UnityEngine.Random.value).Take(itemsPerRound));
            checklistWords.Clear();
            checklistWords.AddRange(Checklist.Select(w => new WordEntry { word = w, category = WordCategory.Furniture }));

            foreach (var p in joins.Players)
            {
                joins.Place(p);
                p.Frozen = true;
            }
            nextDrop = 0f;
            SetState(State.Countdown);
        }

        void Update()
        {
            float t = Time.time - stateStarted;
            switch (Current)
            {
                case State.Waiting:
                    hud.SetTitle("FURNISH FIRST", joins.Players.Count < 2
                        ? "Press SPACE, . or A to join (2+ roommates)"
                        : $"{joins.Players.Count} roommates in.  Press ENTER or START to begin");
                    if (joins.Players.Count >= 2 && joins.AnyStartPressed()) StartMatch();
                    break;

                case State.Countdown:
                    int left = Mathf.CeilToInt(countdownTime - t);
                    if (left != lastCount && left > 0) Sfx.Play(Sound.Countdown);
                    lastCount = left;
                    hud.SetTitle(left > 0 ? left.ToString() : "GO!", $"Round {Round}: furnish your corner!");
                    if (t >= countdownTime) BeginPlay();
                    break;

                case State.Playing:
                    if (t > 0.8f) hud.SetTitle("", "");
                    DropBoxes();
                    CheckCorners();
                    break;

                case State.RoundOver:
                    if (t >= roundOverTime)
                    {
                        if (WinsOf(LastWinner) >= roundsToWin) EnterMatchOver();
                        else StartRound();
                    }
                    break;

                case State.MatchOver:
                    if (t >= matchOverTime && !Session.GoHome()) SetState(State.Waiting);
                    break;
            }
            hud.SetChecklist(Current == State.Waiting ? "" : ChecklistText());
            hud.SetInstruction(Current == State.Waiting ? "" : "Spell the checklist and get it all into YOUR corner",
                               Current == State.Waiting ? "" : "Anything in your corner counts: steal, smash, shove!");
            hud.SetScoreboard(joins.Players, WinsOf, roundsToWin, Current != State.Waiting);
        }

        void BeginPlay()
        {
            Sfx.Play(Sound.Go);
            SetState(State.Playing);
            foreach (var p in joins.Players) p.Frozen = false;
        }

        /// <summary>Keeps enough checklist boxes coming that everyone has something to fight over.</summary>
        void DropBoxes()
        {
            if (Time.time < nextDrop) return;
            nextDrop = Time.time + dropEvery;
            int boxes = AllSmashables().Count(s => s && !s.GetComponent<LetterBuilt>());
            if (boxes >= Mathf.Max(2, joins.Players.Count * 2)) return;
            var word = Checklist[UnityEngine.Random.Range(0, Checklist.Count)];
            var at = new Vector3(UnityEngine.Random.Range(dropX.x, dropX.y), 3f, UnityEngine.Random.Range(dropZ.x, dropZ.y));
            DeliverySpawner.CreateBox(word, at);
        }

        void CheckCorners()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.25f;
            for (int i = 0; i < joins.Players.Count && i < zones.Length; i++)
                if (Progress(i) == Checklist.Count)
                {
                    RoundWon(joins.Players[i]);
                    return;
                }
        }

        /// <summary>How many checklist items are standing in a player's corner right now.</summary>
        public int Progress(int zone) => Checklist.Count(w => Has(zone, w));

        Smashable[] cache;
        int cacheFrame = -1;

        /// <summary>Everything smashable in the scene, looked up once per frame.</summary>
        Smashable[] AllSmashables()
        {
            if (cacheFrame != Time.frameCount || cache == null)
            {
                cache = FindObjectsByType<Smashable>();
                cacheFrame = Time.frameCount;
            }
            return cache;
        }

        public bool Has(int zone, string word)
        {
            foreach (var s in AllSmashables())
            {
                if (!s || s.Word != word || !s.GetComponent<LetterBuilt>()) continue;
                var rb = s.GetComponent<Rigidbody>();
                if (!rb || rb.isKinematic || rb.linearVelocity.sqrMagnitude > 0.25f) continue;
                if (zones[zone].Contains(rb.worldCenterOfMass)) return true;
            }
            return false;
        }

        void RoundWon(PlayerController winner)
        {
            LastWinner = winner;
            wins[winner] = WinsOf(winner) + 1;
            SetState(State.RoundOver);
            foreach (var p in joins.Players) p.Frozen = true;
            hud.SetTitle($"{winner.Name} FURNISHED FIRST!", $"{WinsOf(winner)}/{roundsToWin}");
            Sfx.Play(Sound.RoundWin);
            CameraRig.Shake(0.2f);
        }

        void EnterMatchOver()
        {
            SetState(State.MatchOver);
            hud.SetTitle($"{LastWinner.Name} WINS!", "Best-furnished roommate.  Heading home...");
            Sfx.Play(Sound.Stars);
        }

        string ChecklistText()
        {
            sb.Clear();
            sb.Append("<b>FURNISH FIRST</b>\n<size=75%>Round ").Append(Round).Append(": get these into your corner</size>\n");
            foreach (var word in Checklist)
            {
                sb.Append(word).Append("  ");
                for (int i = 0; i < joins.Players.Count && i < zones.Length; i++)
                {
                    var p = joins.Players[i];
                    bool has = Current == State.Playing || Current == State.RoundOver ? Has(i, word) : false;
                    sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(p.Color)).Append(has ? "FF" : "44").Append('>')
                      .Append(p.Initial).Append("</color> ");
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
