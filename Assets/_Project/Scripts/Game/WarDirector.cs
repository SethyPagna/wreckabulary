using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class WarDirector : HouseRoundDirector
    {
        public const int SideCount = 2;
        public const string LivesCaption = "LIVES";
        readonly KnockoutWatch watch = new();
        readonly List<Spot> spawns = new();
        readonly List<Spot> foes = new();
        readonly HashSet<PlayerController> returning = new();
        readonly Dictionary<int, int> knockouts = new();
        readonly int[] up = new int[SideCount];
        readonly List<int> sidesInRoom = new();
        readonly List<PlayerController> roster = new();
        int[] ends = System.Array.Empty<int>();
        System.Func<PlayerController, int> score;
        System.Func<int, int> lives;
        int panelKey = int.MinValue;

        public static WarDirector Active { get; private set; }
        public WarScore Score { get; private set; }
        public RoomHold Hold { get; private set; }
        public string HoldRoom { get; private set; }
        public int StartingLives { get; set; }
        public float RespawnSeconds { get; set; }
        public float HoldSeconds { get; set; }
        public IReadOnlyList<int> Standing => up;

        protected override void Configured()
        {
            StartingLives = Context.Rules.TeamLives > 0 ? Context.Rules.TeamLives : 12;
            RespawnSeconds = Context.Rules.RespawnSeconds > 0f ? Context.Rules.RespawnSeconds : 3f;
            HoldSeconds = Context.Rules.HoldBonusSeconds;
            score = p => p && knockouts.TryGetValue(p.Index, out int n) ? n : 0;
            lives = side => Score != null ? Score.Lives(side) : 0;
            watch.KnockedOut += KnockedOut;
            Active = this;
        }

        void OnDestroy()
        {
            watch.Clear();
            if (Active == this) Active = null;
        }

        public int SideOfSpawn(int spawn) => spawn >= 0 && spawn < ends.Length ? ends[spawn] : 0;

        protected override void OnMatchStart()
        {
            StopAllCoroutines();
            Score = new WarScore(SideCount, StartingLives, TimeLimit);
            Hold = new RoomHold(HoldSeconds);
            spawns.Clear();
            for (int i = 0; i < Joins.SpawnCount; i++) spawns.Add(Joins.SpawnSpot(i));
            ends = HouseEnds.Split(spawns);
            HoldRoom = HoldSeconds > 0f ? HouseEnds.MiddleRoom(Room.Layout, spawns, ends) : null;
            knockouts.Clear();
            returning.Clear();
            watch.Watch(Players);
            roster.Clear();
            roster.AddRange(Players.Where(p => p).OrderBy(p => p.Team).ThenBy(p => p.Index));
            panelKey = int.MinValue;
        }

        protected override void PlaceAtStart(PlayerController p)
        {
            var mine = HouseEnds.Of(ends, p.Team);
            Joins.Place(p, mine[(Joins.SpawnSeat(p) / SideCount) % mine.Count]);
        }

        protected override void OnPlay() => Hud.Toast($"Two teams, {StartingLives} lives each. Every knockout costs one.");

        protected override string TimerText => Score != null && Score.SuddenDeath ? "SUDDEN DEATH" : base.TimerText;

        protected override void Tick()
        {
            Count();
            if (HoldRoom != null)
            {
                sidesInRoom.Clear();
                foreach (var p in Players)
                    if (p && !p.IsKnockedOut && Room.Layout.RoomAt(p.transform.position.x, p.transform.position.y, p.transform.position.z) == HoldRoom)
                        sidesInRoom.Add(p.Team);
                int earned = Hold.Tick(RoomHold.HolderOf(sidesInRoom), Time.deltaTime);
                if (earned != Teams.NoTeam)
                {
                    Score.Grant(earned);
                    Hud.Toast($"{SideName(earned)} held the {GameHud.Spaced(HoldRoom)}: +1 life");
                }
            }
            Score.Evaluate(up, Elapsed);
            Panel();
            if (Score.Over) Finish(Result());
        }

        void Count()
        {
            for (int s = 0; s < SideCount; s++) up[s] = 0;
            foreach (var p in Players)
                if (p && p.Team >= 0 && p.Team < SideCount && (!p.IsEliminated || returning.Contains(p))) up[p.Team]++;
        }

        protected override void Scoreboard()
        {
            Hud.SetScoreboard(roster.Count > 0 ? roster : Players, score, 0, Score != null, FreeForAllDirector.Unit);
            Hud.SetSideScores(lives, LivesCaption);
        }

        void KnockedOut(PlayerController victim, PlayerController attacker)
        {
            if (Current != Stage.Playing || Score == null) return;
            if (attacker) knockouts[attacker.Index] = score(attacker) + 1;
            bool back = Score.Knockout(victim.Team, attacker ? attacker.Team : Teams.NoTeam);
            if (back)
            {
                returning.Add(victim);
                StartCoroutine(Return(victim));
                if (Score.Lives(victim.Team) == 0) Hud.Toast($"{SideName(victim.Team)} is out of lives!");
            }
            else Hud.Toast($"{victim.Name} is out for good.");
        }

        IEnumerator Return(PlayerController p)
        {
            yield return new WaitForSeconds(RespawnSeconds);
            returning.Remove(p);
            if (!p || !p.IsEliminated || Current != Stage.Playing) yield break;
            watch.Forget(p);
            Joins.Place(p, HomeSpawn(p));
        }

        public int HomeSpawn(PlayerController p)
        {
            foes.Clear();
            foreach (var other in Players)
                if (other && other != p && !other.IsKnockedOut && Teams.AreHostile(p.Team, other.Team))
                    foes.Add(new Spot(other.transform.position.x, other.transform.position.y, other.transform.position.z));
            var mine = HouseEnds.Of(ends, p.Team);
            return SpawnPick.Farthest(spawns, mine, foes, mine[0]);
        }

        string SideName(int side)
        {
            var you = Hud.LocalPlayer;
            return Joins.HumanCount == 1 && you && you.Team == side ? "Your team" : Teams.SideName(side);
        }

        void Panel()
        {
            int held = Hold != null && Hold.Holder != Teams.NoTeam ? Mathf.FloorToInt(Hold.Progress) : -1;
            int key = Score.Lives(0) * 7919 + Score.Lives(1) * 104729 + up[0] * 31 + up[1] * 17 + (Hold?.Holder ?? -1) * 3 + held * 1299709;
            if (key == panelKey) return;
            panelKey = key;
            var lines = new List<string> { "<b>" + HouseModes.Name(HouseModes.War).ToUpperInvariant() + "</b>" };
            for (int s = 0; s < SideCount; s++)
                lines.Add(Score.Lives(s) > 0 ? $"{SideName(s)} · {BattleResultText.LifeCount(Score.Lives(s))}"
                    : $"<color=#FF6B5E>{SideName(s)} · no lives · {up[s]} standing</color>");
            if (HoldRoom != null)
                lines.Add(Hold.Holder != Teams.NoTeam && held >= 0
                    ? $"{SideName(Hold.Holder)} holds the {GameHud.Spaced(HoldRoom)} · {Mathf.Max(1, Mathf.CeilToInt(HoldSeconds - Hold.Progress))} s"
                    : $"Hold the {GameHud.Spaced(HoldRoom)} alone: +1 life every {Mathf.RoundToInt(HoldSeconds)} s");
            Hud.SetChecklist(string.Join("\n", lines));
        }

        protected override void OnFinish() => Hud.SetChecklist("");

        HouseRoundResult Result()
        {
            var you = Hud.LocalPlayer;
            var seats = roster.Where(p => p).Select(p => new BattleSeat(p.Name, score(p), p.Team, p.Team == Score.Winner, p == you,
                p.Binding != null && p.Binding is not BotBinding)).ToArray();
            var summary = new BattleSummary(seats, 0, Score.TimeUp, Score.WonInSuddenDeath,
                Enumerable.Range(0, SideCount).Select(Score.Lives).ToArray(), Score.Winner, Score.Loser);
            return new HouseRoundResult
            {
                WinningTeam = Score.Winner,
                Winner = roster.FirstOrDefault(p => p && p.Team == Score.Winner),
                WinnerName = BattleResultText.Winner(summary),
                Heading = BattleResultText.Heading(summary),
                Eyebrow = BattleResultText.Eyebrow(summary),
                Flavour = BattleResultText.Flavour(summary),
                Lines = BattleResultText.Lines(summary),
                Draw = summary.Draw,
            };
        }
    }
}
