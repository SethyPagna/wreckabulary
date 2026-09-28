using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wreckabulary
{
    public enum Phase { Lobby, Countdown, Playing, RoundOver, MatchOver }

    /// <summary>
    /// Dibs! match flow. Lobby (join and mess around) → countdown → play until one roommate is left
    /// standing → next round. First to 3 wins the match, then everyone heads back to the house.
    /// Coming from the house with 2+ roommates skips the lobby.
    /// </summary>
    public class RoundManager : MonoBehaviour
    {
        public static RoundManager Instance { get; private set; }

        [SerializeField] PlayerJoinManager joins;
        [SerializeField] RoomBuilder room;
        [SerializeField] DeliverySpawner deliveries;
        [SerializeField] GameHud hud;

        [SerializeField] string mapName = "Living Room";
        [Tooltip("Use the rounds and starting letters chosen in Creative.")]
        [SerializeField] bool useCustomRules;
        [SerializeField] int roundsToWin = 3;
        [SerializeField] float collapseAfter = 90f;
        [SerializeField] float countdownTime = 3f;
        [SerializeField] float roundOverTime = 3f;
        [SerializeField] float matchOverTime = 6f;

        readonly Dictionary<PlayerController, int> wins = new();
        float phaseStarted, roundStarted;
        PlayerController lastWinner;
        int lastCount;

        public Phase Phase { get; private set; } = Phase.Lobby;
        public int Round { get; private set; }
        public float CountdownTime { get => countdownTime; set => countdownTime = value; }
        public int WinsOf(PlayerController p) => p && wins.TryGetValue(p, out int w) ? w : 0;

        public event Action<PlayerController> RoundWon;
        public event Action<PlayerController> MatchWon;
        public event Action CollapseStarted;

        void Awake() => Instance = this;

        void Start()
        {
            if (useCustomRules)
            {
                roundsToWin = Mathf.Max(1, Session.CustomRounds);
                joins.StarterLetters = Session.CustomStarterLetters;
            }
            Music.Play(Track.Brawl);
            joins.Joined += OnJoined;
            foreach (var p in Players) OnJoined(p);
            EnterLobby();
            if (joins.RestoredFromSession && Players.Count >= 2) StartMatch();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        IReadOnlyList<PlayerController> Players => joins.Players;
        float PhaseTime => Time.unscaledTime - phaseStarted;

        void SetPhase(Phase p)
        {
            Phase = p;
            phaseStarted = Time.unscaledTime;
            joins.RespawnKnockedOut = p == Phase.Lobby;
        }

        void Update()
        {
            switch (Phase)
            {
                case Phase.Lobby:
                    if (Players.Count < 2)
                        hud.SetTitle("DIBS!", "Press SPACE (keyboard), . (second keyboard player) or A (gamepad) to join");
                    else
                        hud.SetTitle("DIBS!", $"{Players.Count} roommates in.  Press ENTER or START to begin");
                    hud.SetTimer("");
                    if (Players.Count >= 2 && joins.AnyStartPressed()) StartMatch();
                    break;

                case Phase.Countdown:
                    int left = Mathf.CeilToInt(countdownTime - PhaseTime);
                    if (left != lastCount && left > 0) Sfx.Play(Sound.Countdown);
                    lastCount = left;
                    hud.SetTitle(left > 0 ? left.ToString() : "DIBS!", $"Round {Round}  •  {mapName}");
                    if (PhaseTime >= countdownTime) BeginPlay();
                    break;

                case Phase.Playing:
                    float t = Time.time - roundStarted;
                    if (PhaseTime > 0.8f) hud.SetTitle("", "");
                    if (!deliveries.Collapsing)
                    {
                        hud.SetTimer(Mathf.CeilToInt(Mathf.Max(0f, collapseAfter - t)).ToString());
                        if (t >= collapseAfter) StartCollapse();
                    }
                    break;

                case Phase.RoundOver:
                    if (PhaseTime >= roundOverTime)
                    {
                        if (WinsOf(lastWinner) >= roundsToWin) EnterMatchOver();
                        else StartRound();
                    }
                    break;

                case Phase.MatchOver:
                    if (PhaseTime >= matchOverTime && !Session.GoHome()) EnterLobby();
                    break;
            }
            hud.SetScoreboard(Players, WinsOf, roundsToWin, Phase != Phase.Lobby);
        }

        void OnJoined(PlayerController p)
        {
            wins[p] = 0;
            p.Health.KnockedOut += OnKnockedOut;
        }

        void EnterLobby()
        {
            SetPhase(Phase.Lobby);
            Time.timeScale = 1f;
            joins.AllowJoining = true;
            deliveries.Running = true;
            deliveries.ResetDrops();
            foreach (var p in Players) p.Frozen = false;
        }

        public void StartMatch()
        {
            foreach (var p in Players) wins[p] = 0;
            Round = 0;
            joins.AllowJoining = false;
            StartRound();
        }

        void StartRound()
        {
            Round++;
            Time.timeScale = 1f;
            room.ResetRoom();
            deliveries.Running = false;
            deliveries.ResetDrops();
            foreach (var p in Players)
            {
                joins.Place(p);
                p.Frozen = true;
            }
            SetPhase(Phase.Countdown);
        }

        void BeginPlay()
        {
            Sfx.Play(Sound.Go);
            SetPhase(Phase.Playing);
            roundStarted = Time.time;
            hud.SetTitle("DIBS!", "");
            foreach (var p in Players) p.Frozen = false;
            deliveries.Running = true;
            deliveries.ResetDrops();
        }

        void StartCollapse()
        {
            deliveries.StartCollapse();
            hud.SetTimer("<color=#FF6A4D>COLLAPSE!</color>");
            CameraRig.Shake(0.4f);
            Sfx.Play(Sound.Collapse);
            CollapseStarted?.Invoke();
        }

        void OnKnockedOut(PlayerHealth victim)
        {
            if (Phase != Phase.Playing) return;

            var alive = Players.Where(p => !p.IsKnockedOut).ToList();
            if (alive.Count > 1) return;

            lastWinner = alive.FirstOrDefault();
            SetPhase(Phase.RoundOver);
            deliveries.Running = false;
            if (!lastWinner)
            {
                hud.SetTitle("DRAW!", "Nobody gets dibs");
                return;
            }
            wins[lastWinner] = WinsOf(lastWinner) + 1;
            hud.SetTitle($"{lastWinner.Name} WINS THE ROUND", $"{WinsOf(lastWinner)}/{roundsToWin}");
            StartCoroutine(SlowMo());
            Sfx.Play(Sound.RoundWin);
            RoundWon?.Invoke(lastWinner);
        }

        void EnterMatchOver()
        {
            SetPhase(Phase.MatchOver);
            hud.SetTitle($"{lastWinner.Name} CALLS DIBS!", "Winner of the match.  Heading home...");
            MatchWon?.Invoke(lastWinner);
        }

        IEnumerator SlowMo()
        {
            Time.timeScale = 0.35f;
            yield return new WaitForSecondsRealtime(0.9f);
            Time.timeScale = 1f;
        }
    }
}
