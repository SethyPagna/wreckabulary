using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class FreeForAllDirector : HouseRoundDirector
    {
        public const string Unit = " KO";
        readonly KnockoutWatch watch = new();
        readonly List<Spot> spawns = new();
        readonly List<Spot> foes = new();
        System.Func<PlayerController, int> score;
        string shownLine;

        public KnockoutRace Race { get; private set; }
        public int Target { get; set; }
        public float RespawnSeconds { get; set; }

        protected override void Configured()
        {
            Target = Mathf.Max(1, Context.Rules.KnockoutsToWin > 0 ? Context.Rules.KnockoutsToWin : 10);
            RespawnSeconds = Context.Rules.RespawnSeconds > 0f ? Context.Rules.RespawnSeconds : 3f;
            score = p => Race != null && p ? Race.Of(p.Index) : 0;
            watch.KnockedOut += KnockedOut;
        }

        void OnDestroy() => watch.Clear();

        protected override void OnMatchStart()
        {
            StopAllCoroutines();
            Race = new KnockoutRace(Target, TimeLimit);
            foreach (var p in Players) Race.Enter(p.Index);
            watch.Watch(Players);
            spawns.Clear();
            for (int i = 0; i < Joins.SpawnCount; i++) spawns.Add(Joins.SpawnSpot(i));
            shownLine = null;
        }

        protected override void OnPlay() => Hud.Toast($"Everyone for themselves. First to {Target} knockouts.");

        protected override string TimerText => Race != null && Race.SuddenDeath ? "SUDDEN DEATH" : base.TimerText;

        protected override void Tick()
        {
            Race.Tick(Elapsed);
            string line = Race.SuddenDeath ? "Sudden death: the next knockout for a leader wins" : "";
            if (!ReferenceEquals(line, shownLine)) { shownLine = line; Hud.SetInstruction(line, ""); }
            if (Race.Over) Finish(Result());
        }

        protected override void Scoreboard() => Hud.SetScoreboard(Players, score, Target, Race != null, Unit);

        void KnockedOut(PlayerController victim, PlayerController attacker)
        {
            if (Current != Stage.Playing || Race == null) return;
            if (Race.Knockout(victim.Index, attacker ? attacker.Index : -1) && !Race.Over && Race.Of(attacker.Index) == Target - 1)
                Hud.Toast($"{attacker.Name} needs one more knockout!");
            if (!Race.Over) StartCoroutine(Return(victim));
        }

        IEnumerator Return(PlayerController p)
        {
            yield return new WaitForSeconds(RespawnSeconds);
            if (!p || !p.IsEliminated || Current != Stage.Playing) yield break;
            watch.Forget(p);
            Joins.Place(p, FarthestSpawn(p));
        }

        public int FarthestSpawn(PlayerController p)
        {
            foes.Clear();
            foreach (var other in Players)
                if (other && other != p && !other.IsKnockedOut && Teams.AreHostile(p.Team, other.Team))
                    foes.Add(new Spot(other.transform.position.x, other.transform.position.y, other.transform.position.z));
            return SpawnPick.Farthest(spawns, Enumerable.Range(0, spawns.Count), foes, p.Index);
        }

        HouseRoundResult Result()
        {
            var you = Hud.LocalPlayer;
            var seats = Players.Where(p => p).Select(p => new BattleSeat(p.Name, Race.Of(p.Index), p.Team, p.Index == Race.Winner, p == you,
                p.Binding != null && p.Binding is not BotBinding)).ToArray();
            var summary = new BattleSummary(seats, Target, Race.TimeUp, Race.WonInSuddenDeath);
            return new HouseRoundResult
            {
                Winner = World.PlayerById(Race.Winner),
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
