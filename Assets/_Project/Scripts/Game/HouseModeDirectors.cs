using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public interface IHouseWordGoal
    {
        string WantedWord(PlayerController player);
    }

    public sealed class HouseRoundResult
    {
        public PlayerController Winner;
        public int WinningTeam = Teams.NoTeam;
        public bool Draw;
        public string WinnerName = "", Heading = "", Eyebrow = "", Flavour = "";
        public string[] Lines = Array.Empty<string>();
        public bool Stats = true;
    }

    public sealed class HouseModeContext
    {
        public PlayerJoinManager Joins { get; }
        public RoomBuilder Room { get; }
        public DeliverySpawner Deliveries { get; }
        public GameHud Hud { get; }
        public RoundManager Rounds { get; }
        public GameObject Host => Rounds.gameObject;
        public string ModeId => Match.Mode;
        public HouseMode Mode => HouseModes.Find(Match.Mode);
        public GameRules Rules => Match.Rules;
        public HouseLayout Layout => Room ? Room.Layout : null;
        public Action Restart { get; set; }
        public int Round { get; set; } = 1;
        public IHouseWordGoal WordGoal { get => Rounds.WordGoal; set => Rounds.WordGoal = value; }
        public Phase Phase { get => Rounds.Phase; set => Rounds.EnterDirectedPhase(value); }
        public bool Finished => Rounds.Phase == Phase.MatchOver;

        public HouseModeContext(RoundManager rounds, PlayerJoinManager joins, RoomBuilder room, DeliverySpawner deliveries, GameHud hud)
        {
            Rounds = rounds; Joins = joins; Room = room; Deliveries = deliveries; Hud = hud;
        }

        public void Finish(HouseRoundResult result, Action again) => Rounds.FinishDirected(result, again);

        public void FinishRound(HouseRoundResult result, Action next) => Rounds.FinishDirected(result, next, final: false);
    }

    public static class HouseModeDirectors
    {
        static readonly Dictionary<string, Action<HouseModeContext>> Attachers = new(StringComparer.Ordinal);

        static HouseModeDirectors()
        {
            Register(HouseModes.Escape, c =>
            {
                c.Host.AddComponent<EscapeDirector>().Configure(c.Joins, c.Room, c.Deliveries, c.Hud);
                c.Rounds.enabled = false;
            });
            Register(HouseModes.MovingOut, c =>
            {
                c.Host.AddComponent<MovingOutDirector>().Configure(c.Joins, c.Room, c.Deliveries, c.Hud);
                c.Rounds.enabled = false;
            });
            Register(HouseModes.FreeForAll, c => c.Host.AddComponent<FreeForAllDirector>().Configure(c));
            Register(HouseModes.War, c => c.Host.AddComponent<WarDirector>().Configure(c));
        }

        public static void Register(string modeId, Action<HouseModeContext> attach)
        {
            if (string.IsNullOrEmpty(modeId)) throw new ArgumentException("A House mode needs an id.", nameof(modeId));
            Attachers[modeId] = attach ?? throw new ArgumentNullException(nameof(attach));
        }

        public static bool Has(string modeId) => modeId != null && Attachers.ContainsKey(modeId);

        public static IEnumerable<string> Registered => Attachers.Keys;

        public static bool TryAttach(HouseModeContext context)
        {
            if (context == null || !Attachers.TryGetValue(context.ModeId ?? "", out var attach)) return false;
            attach(context);
            return true;
        }

        public static string WantedWord(PlayerController player)
        {
            var rounds = RoundManager.Instance;
            return rounds && player && rounds.WordGoal != null ? rounds.WordGoal.WantedWord(player) : null;
        }
    }

    public sealed class KnockoutWatch
    {
        readonly Dictionary<PlayerController, (Action<PlayerHealth, HitInfo, HitResult> hit, Action<PlayerHealth> wrecked)> watched = new();
        readonly Dictionary<int, int> lastHitBy = new();

        public event Action<PlayerController, PlayerController> KnockedOut;

        public void Watch(IEnumerable<PlayerController> players)
        {
            foreach (var p in players)
            {
                if (!p || watched.ContainsKey(p)) continue;
                var victim = p;
                Action<PlayerHealth, HitInfo, HitResult> hit = (_, info, _) => Hit(victim, info);
                Action<PlayerHealth> wrecked = _ => Wrecked(victim);
                victim.Health.Damaged += hit;
                victim.Health.Eliminated += wrecked;
                watched[victim] = (hit, wrecked);
            }
        }

        public void Forget(PlayerController p) { if (p) lastHitBy.Remove(p.Index); }

        public void Clear()
        {
            foreach (var entry in watched)
            {
                if (!entry.Key || !entry.Key.Health) continue;
                entry.Key.Health.Damaged -= entry.Value.hit;
                entry.Key.Health.Eliminated -= entry.Value.wrecked;
            }
            watched.Clear();
            lastHitBy.Clear();
        }

        void Hit(PlayerController victim, HitInfo info)
        {
            var attacker = Hits.Attacker(info);
            if (attacker && attacker != victim && Teams.AreHostile(attacker.Team, victim.Team)) lastHitBy[victim.Index] = attacker.Index;
        }

        void Wrecked(PlayerController victim)
        {
            var attacker = lastHitBy.TryGetValue(victim.Index, out int id) ? World.PlayerById(id) : null;
            lastHitBy.Remove(victim.Index);
            KnockedOut?.Invoke(victim, attacker);
        }
    }

    public abstract class HouseRoundDirector : MonoBehaviour
    {
        public enum Stage { Lobby, Countdown, Playing, Over }

        string joinLine;
        int joinVersion = -1, clockSeconds = -1;
        string clockText = "";
        float stageAt;

        protected HouseModeContext Context { get; private set; }
        protected PlayerJoinManager Joins => Context.Joins;
        protected GameHud Hud => Context.Hud;
        protected RoomBuilder Room => Context.Room;
        protected DeliverySpawner Deliveries => Context.Deliveries;
        protected IReadOnlyList<PlayerController> Players => Context.Joins.Players;
        public Stage Current { get; private set; } = Stage.Lobby;
        public float CountdownSeconds { get; set; } = 3f;
        public float TimeLimit { get; set; }
        public float Elapsed => Current == Stage.Playing ? Time.time - stageAt : 0f;
        public float TimeLeft => TimeLimit > 0f ? Mathf.Max(0f, TimeLimit - Elapsed) : 0f;

        public void Configure(HouseModeContext context)
        {
            Context = context;
            context.Restart = StartMatch;
            TimeLimit = context.Rules.RoundTimeLimitSeconds;
            Configured();
        }

        protected virtual void Start()
        {
            Deliveries.Layout = Room.Layout;
            Deliveries.RoomOpen = null;
            EnterLobby();
            if (Joins.RestoredFromSession && Joins.HumanCount > 0) StartMatch();
        }

        void EnterLobby()
        {
            Current = Stage.Lobby;
            Context.Phase = Phase.Lobby;
            Joins.AllowJoining = true;
            Deliveries.Running = true;
            Deliveries.ResetDrops();
            foreach (var p in Players) p.Frozen = false;
        }

        public int Round { get; private set; }

        public void StartMatch()
        {
            if (Time.timeScale <= 0f || Joins.HumanCount == 0) return;
            Round = 0;
            MatchTally.BeginMatch();
            StartRound();
        }

        protected void StartRound()
        {
            if (Time.timeScale <= 0f || Joins.HumanCount == 0) return;
            StopAllCoroutines();
            Round++;
            Joins.EnsureOpponents();
            Joins.AllowJoining = false;
            Hud.ShowModeActions(false);
            Hud.HideResult();
            MatchTally.BeginRound();
            Room.ResetRoom();
            Deliveries.Running = false;
            Deliveries.ResetDrops();
            Joins.RespawnKnockedOut = false;
            OnMatchStart();
            foreach (var p in Players) PlaceAtStart(p);
            foreach (var p in Players) p.Frozen = true;
            Current = Stage.Countdown;
            Context.Round = Round;
            Context.Phase = Phase.Countdown;
            stageAt = Time.time;
            clockSeconds = -1;
            Hud.SetInstruction("", "");
            Hud.ShowBanner(HouseGoals.BannerTitle(Round, HouseModes.Name(Context.ModeId)), HouseGoals.Banner(Context.ModeId, Context.Rules));
        }

        void BeginPlay()
        {
            Current = Stage.Playing;
            Context.Phase = Phase.Playing;
            Joins.RespawnKnockedOut = false;
            stageAt = Time.time;
            foreach (var p in Players) p.Frozen = false;
            Deliveries.Running = true;
            Deliveries.ResetDrops();
            Hud.ShowGo();
            OnPlay();
        }

        protected virtual void Update()
        {
            if (Time.timeScale <= 0f || Context == null) return;
            switch (Current)
            {
                case Stage.Lobby:
                    Hud.SetInstruction(Joins.HumanCount == 0 ? JoinLine() : "Press START — solo seats get AI opponents", "");
                    Hud.SetTimer("");
                    Hud.ShowModeActions(Joins.HumanCount > 0, "START WITH AI →", StartMatch);
                    if (AcceptsStart && Joins.HumanCount > 0 && Joins.AnyStartPressed()) StartMatch();
                    break;
                case Stage.Countdown:
                    float since = Time.time - stageAt;
                    Hud.ShowCountdown(Mathf.Max(1, Mathf.CeilToInt(CountdownSeconds - since)), $"{HouseModes.Name(Context.ModeId).ToUpperInvariant()} · {Room.Layout.Name.ToUpperInvariant()}");
                    if (since >= CountdownSeconds) BeginPlay();
                    break;
                case Stage.Playing:
                    Hud.SetTimer(TimerText);
                    Tick();
                    break;
                case Stage.Over:
                    if (AcceptsStart && Hud.ResultShown && Time.time - stageAt > 1.6f && Joins.AnyStartPressed()) Hud.ConfirmResult();
                    break;
            }
            Scoreboard();
        }

        bool AcceptsStart => !Hud.StateController ||
            (Hud.StateController.CurrentState == UIState.GameplayHUD && !Hud.StateController.IsTransitioning);

        protected void Finish(HouseRoundResult result) => End(result, true);

        protected void FinishRound(HouseRoundResult result) => End(result, false);

        void End(HouseRoundResult result, bool final)
        {
            if (Current == Stage.Over) return;
            Current = Stage.Over;
            stageAt = Time.time;
            Hud.SetTimer("");
            Hud.SetInstruction("", "");
            OnFinish();
            if (final) Context.Finish(result, StartMatch);
            else Context.FinishRound(result, StartRound);
        }

        string JoinLine()
        {
            if (joinLine == null || joinVersion != KeyBindings.Version) { joinLine = ControlHints.Join("join"); joinVersion = KeyBindings.Version; }
            return joinLine;
        }

        protected string Clock(float seconds)
        {
            int shown = Mathf.Max(0, Mathf.CeilToInt(seconds));
            if (shown != clockSeconds) { clockSeconds = shown; clockText = RoundManager.FormatTime(shown); }
            return clockText;
        }

        protected virtual string TimerText => TimeLimit > 0f ? Clock(TimeLeft) : "";
        protected virtual void Configured() { }
        protected virtual void PlaceAtStart(PlayerController p) => Joins.Place(p);
        protected abstract void OnMatchStart();
        protected virtual void OnPlay() { }
        protected abstract void Tick();
        protected virtual void OnFinish() { }
        protected virtual void Scoreboard() => Hud.SetScoreboard(Players, _ => 0, 0, false);
    }
}
