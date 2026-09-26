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
    /// standing → next round. First to 3 wins the match, then it's back to the lobby.
    /// </summary>
    public class RoundManager : MonoBehaviour
    {
        public static RoundManager Instance { get; private set; }

        [SerializeField] PlayerJoinManager joins;
        [SerializeField] RoomBuilder room;
        [SerializeField] DeliverySpawner deliveries;
        [SerializeField] GameHud hud;

        [SerializeField] int roundsToWin = 3;
        [SerializeField] float collapseAfter = 90f;
        [SerializeField] float countdownTime = 3f;
        [SerializeField] float roundOverTime = 3f;
        [SerializeField] float matchOverTime = 6f;
        [Tooltip("Everyone starts a round holding this many letters, so the first hit isn't a knockout.")]
        [SerializeField] int starterLetters = 3;

        readonly Dictionary<PlayerController, int> wins = new();
        float phaseStarted, roundStarted;
        PlayerController lastWinner;

        public Phase Phase { get; private set; } = Phase.Lobby;
        public int Round { get; private set; }
        public float CountdownTime { get => countdownTime; set => countdownTime = value; }
        public int WinsOf(PlayerController p) => wins.TryGetValue(p, out int w) ? w : 0;

        public event Action<PlayerController> RoundWon;
        public event Action<PlayerController> MatchWon;
        public event Action CollapseStarted;

        void Awake() => Instance = this;

        void Start()
        {
            if (joins) joins.Joined += OnJoined;
            EnterLobby();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        IReadOnlyList<PlayerController> Players => joins ? joins.Players : Array.Empty<PlayerController>();
        float PhaseTime => Time.unscaledTime - phaseStarted;

        void SetPhase(Phase p)
        {
            Phase = p;
            phaseStarted = Time.unscaledTime;
        }

        void Update()
        {
            switch (Phase)
            {
                case Phase.Lobby:
                    if (Players.Count < 2)
                        hud?.SetTitle("DIBS!", "Press SPACE (keyboard), . (second keyboard player) or A (gamepad) to join");
                    else
                        hud?.SetTitle("DIBS!", $"{Players.Count} roommates in.  Press ENTER or START to begin");
                    hud?.SetTimer("");
                    if (Players.Count >= 2 && joins.AnyStartPressed()) StartMatch();
                    break;

                case Phase.Countdown:
                    int left = Mathf.CeilToInt(countdownTime - PhaseTime);
                    hud?.SetTitle(left > 0 ? left.ToString() : "DIBS!", $"Round {Round}");
                    if (PhaseTime >= countdownTime) BeginPlay();
                    break;

                case Phase.Playing:
                    float t = Time.time - roundStarted;
                    if (PhaseTime > 0.8f) hud?.SetTitle("", "");
                    if (!deliveries || !deliveries.Collapsing)
                    {
                        hud?.SetTimer(Mathf.CeilToInt(Mathf.Max(0f, collapseAfter - t)).ToString());
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
                    if (PhaseTime >= matchOverTime) EnterLobby();
                    break;
            }
            hud?.SetScoreboard(Players, WinsOf, roundsToWin, Phase != Phase.Lobby);
        }

        void OnJoined(PlayerController p)
        {
            wins[p] = 0;
            p.Inventory.Set(StarterLetters());
            p.Health.KnockedOut += OnKnockedOut;
        }

        void EnterLobby()
        {
            SetPhase(Phase.Lobby);
            Time.timeScale = 1f;
            if (joins) joins.AllowJoining = true;
            if (deliveries) { deliveries.Running = true; deliveries.ResetDrops(); }
            foreach (var p in Players) p.Frozen = false;
        }

        public void StartMatch()
        {
            foreach (var p in Players) wins[p] = 0;
            Round = 0;
            if (joins) joins.AllowJoining = false;
            StartRound();
        }

        void StartRound()
        {
            Round++;
            Time.timeScale = 1f;
            if (room) room.ResetRoom();
            if (deliveries) { deliveries.Running = false; deliveries.ResetDrops(); }
            for (int i = 0; i < Players.Count; i++) ResetPlayer(Players[i], i);
            foreach (var p in Players) p.Frozen = true;
            SetPhase(Phase.Countdown);
        }

        void ResetPlayer(PlayerController p, int i)
        {
            p.Combat.ResetForRound();
            p.Summoner.Close();
            p.Health.ResetForRound();
            p.Respawn(joins.SpawnPoint(i));
            p.Inventory.Set(StarterLetters());
        }

        void BeginPlay()
        {
            SetPhase(Phase.Playing);
            roundStarted = Time.time;
            hud?.SetTitle("DIBS!", "");
            foreach (var p in Players) p.Frozen = false;
            if (deliveries) { deliveries.Running = true; deliveries.ResetDrops(); }
        }

        void StartCollapse()
        {
            deliveries?.StartCollapse();
            hud?.SetTimer("<color=#FF6A4D>COLLAPSE!</color>");
            CameraRig.Shake(0.4f);
            CollapseStarted?.Invoke();
        }

        void OnKnockedOut(PlayerHealth victim)
        {
            if (Phase == Phase.Lobby)
            {
                StartCoroutine(LobbyRespawn(victim.GetComponent<PlayerController>()));
                return;
            }
            if (Phase != Phase.Playing) return;

            var alive = Players.Where(p => !p.IsKnockedOut).ToList();
            if (alive.Count > 1) return;

            lastWinner = alive.FirstOrDefault();
            SetPhase(Phase.RoundOver);
            if (deliveries) deliveries.Running = false;
            if (!lastWinner)
            {
                hud?.SetTitle("DRAW!", "Nobody gets dibs");
                return;
            }
            wins[lastWinner] = WinsOf(lastWinner) + 1;
            hud?.SetTitle($"{lastWinner.Name} WINS THE ROUND", $"{WinsOf(lastWinner)}/{roundsToWin}");
            StartCoroutine(SlowMo());
            RoundWon?.Invoke(lastWinner);
        }

        void EnterMatchOver()
        {
            SetPhase(Phase.MatchOver);
            hud?.SetTitle($"{lastWinner.Name} CALLS DIBS!", "Winner of the match");
            MatchWon?.Invoke(lastWinner);
        }

        IEnumerator LobbyRespawn(PlayerController p)
        {
            yield return new WaitForSeconds(2f);
            if (!p || Phase != Phase.Lobby) yield break;
            p.Combat.ResetForRound();
            p.Health.ResetForRound();
            p.Respawn(joins.SpawnPoint(p.Index));
            p.Inventory.Set(StarterLetters());
        }

        IEnumerator SlowMo()
        {
            Time.timeScale = 0.35f;
            yield return new WaitForSecondsRealtime(0.9f);
            Time.timeScale = 1f;
        }

        /// <summary>One vowel, then common consonants.</summary>
        string StarterLetters()
        {
            const string vowels = "AAEEIOU";
            const string common = "RSTLNDBMPW";
            var s = new char[starterLetters];
            for (int i = 0; i < s.Length; i++)
                s[i] = i == 0 ? vowels[UnityEngine.Random.Range(0, vowels.Length)] : common[UnityEngine.Random.Range(0, common.Length)];
            return new string(s);
        }
    }
}
