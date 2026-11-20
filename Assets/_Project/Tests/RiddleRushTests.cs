using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class RiddleRushTests
    {
        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RiddleRushDirector.SeedOverride = null;
            yield return TestScenes.Reset();
        }

        static IEnumerator Load(string map, ulong seed)
        {
            Session.SelectMap(map);
            Match.ModeOverride = HouseModes.RiddleRush;
            RiddleRushDirector.SeedOverride = seed;
            Session.Remember(new ScriptedBinding());
            yield return TestScenes.Load(Session.DibsScene);
            yield return TestScenes.WaitUntil(() => RiddleRushDirector.Active, 5f, "the Riddle Rush director");
        }

        static IEnumerator Play(RiddleRushDirector director, int target, float hint, float beat, float knowsMin, float knowsMax)
        {
            director.Target = target;
            director.HintSeconds = hint;
            director.BeatSeconds = beat;
            director.KnowsMin = knowsMin;
            director.KnowsMax = knowsMax;
            director.CountdownSeconds = .05f;
            director.StartMatch();
            yield return TestScenes.WaitUntil(() => director.Playing && director.Race?.Current != null, 5f, "the first riddle");
        }

        static void StopBots(PlayerJoinManager joins)
        {
            foreach (var p in joins.Players)
            {
                if (p.TryGetComponent<BotController>(out var bot)) bot.enabled = false;
                if (p.Binding is BotBinding binding) binding.Commands = default;
            }
        }

        static string Standing(RiddleRushDirector director, PlayerJoinManager joins)
        {
            var race = director.Race;
            var furniture = Object.FindObjectsByType<Smashable>(FindObjectsSortMode.None).Where(s => s && !s.IsBroken).Select(s => s.Word).OrderBy(w => w);
            var loose = TilePool.Instance ? new string(TilePool.Instance.Active.Where(t => t).Select(t => t.Letter).OrderBy(c => c).ToArray()) : "";
            var seats = joins.Players.Select(p =>
                $"seat {p.Index} {race.Points(p.Index)} pt, bag '{new string(p.Inventory.Letters.ToArray())}', wants '{p.Summoner.WantedWord}', holds '{(p.Combat.Weapon ? p.Combat.Weapon.word : "")}', at {p.transform.position:0.0}, out {p.IsKnockedOut}");
            return $"Riddle {race.Asked} '{race.Current?.Answer}' {race.Stage} for {race.Elapsed:0} s, time left {director.TimeLeft:0} s. {string.Join("; ", seats)}. Loose letters '{loose}'. Furniture: {string.Join(" ", furniture)}";
        }

        static bool Holds(PlayerController p, string word) =>
            (p.Combat.Weapon && p.Combat.Weapon.word == word) || (p.Combat.StoredGear && p.Combat.StoredGear.word == word);

        [UnityTest]
        public IEnumerator TheFirstToSpellTheRiddleScoresAndTheHintPointsAtFurniture()
        {
            yield return Load("pinwheel", 11);
            var director = RiddleRushDirector.Active;
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.Greater(director.HouseRiddles.Count, 30, "Pinwheel supplies most riddles");
            yield return Play(director, 5, 4f, .5f, .2f, .4f);
            StopBots(joins);
            var race = director.Race;
            var human = joins.Players.First(p => p.Binding is ScriptedBinding);
            var bot = joins.Players.First(p => p.Binding is BotBinding);
            Assert.AreEqual(4, joins.Players.Count);
            yield return TestScenes.WaitUntil(() => director.Panel.Shown, 4f, "the riddle panel once the banner has gone");
            Assert.AreEqual(1, race.Asked);
            Assert.AreEqual(race.Current.Clue, director.Panel.ClueText);
            Assert.AreEqual(new string('_', race.Current.Answer.Length), director.Panel.TileText);
            StringAssert.StartsWith("Hint in 0:0", director.Panel.LineText);

            string answer = race.Current.Answer;
            Assert.IsNull(director.WantedWord(human), "a human is never handed the answer");
            human.Inventory.Set(answer);
            Assert.IsTrue(human.Summoner.Summon(answer));
            Assert.AreEqual(1, race.Points(human.Index));
            Assert.AreEqual(RiddleStage.Solved, race.Stage);
            Assert.IsTrue(Holds(human, answer), "the answer lands in a hand as usual");
            yield return null;
            Assert.AreEqual(answer, director.Panel.TileText);
            StringAssert.EndsWith($"spelled {answer}!", director.Panel.LineText);

            yield return TestScenes.WaitUntil(() => race.Asked == 2 && race.Stage == RiddleStage.Asking, 3f, "the next riddle after the beat");
            string next = race.Current.Answer;
            Assert.AreNotEqual(answer, next);
            Assert.IsNull(director.WantedWord(bot), "a bot doesn't know the answer straight away");
            yield return TestScenes.WaitUntil(() => race.Elapsed > .45f, 3f, "the bots' thinking time");
            Assert.AreEqual(next, director.WantedWord(bot));
            Assert.AreEqual(next, HouseModeDirectors.WantedWord(bot));
            Assert.IsNull(director.WantedWord(human));

            yield return TestScenes.WaitUntil(() => race.HintShown, 6f, "the hint");
            yield return null;
            Assert.AreEqual(next[0], director.Panel.TileText[0]);
            Assert.AreEqual(new string('_', next.Length - 1), director.Panel.TileText.Substring(1));
            StringAssert.StartsWith($"Starts with {next[0]}", director.Panel.LineText);
            Assert.IsNotEmpty(director.Pulsing, "the hint points at furniture");
            Assert.IsNotNull(SmashableLabel.Pulse);
            Assert.IsTrue(director.Pulsing.All(s => SmashableLabel.Pulse(s)), "their name labels pulse");
            var held = new LetterBag();
            foreach (var s in director.Pulsing) held.Add(LetterBag.FromWord(s.Word));
            Assert.IsTrue(director.Pulsing.All(s => s.Word == next) || held.Contains(LetterBag.FromWord(next)));
            Assert.AreEqual(1, race.Points(human.Index));
        }

        [UnityTest]
        public IEnumerator ARiddleNobodySolvesPassesAndTheNextIsAsked()
        {
            yield return Load("pinwheel", 11);
            var director = RiddleRushDirector.Active;
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            director.PassSeconds = 1.5f;
            yield return Play(director, 5, .5f, .5f, 50f, 60f);
            StopBots(joins);
            var race = director.Race;
            string first = race.Current.Answer;
            yield return TestScenes.WaitUntil(() => race.Passes == 1, 15f, "the unsolved riddle to pass");
            Assert.AreEqual(first, race.LastPassed);
            Assert.AreEqual(2, race.Asked);
            Assert.AreEqual(RiddleStage.Asking, race.Stage);
            Assert.AreNotEqual(first, race.Current.Answer);
            Assert.AreEqual(0, race.Solved, "a pass scores nothing");
            Assert.IsTrue(director.Playing, "the match carries on");
            Assert.IsEmpty(director.Pulsing, "the passed riddle's furniture stops pulsing");
            yield return TestScenes.WaitUntil(() => director.Panel.ClueText == race.Current.Clue, 3f, "the next riddle on the panel");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator BotsPlayRiddleRushToAWinnerOnPinwheel()
        {
            yield return Load("pinwheel", 34);
            var director = RiddleRushDirector.Active;
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var hud = Object.FindAnyObjectByType<GameHud>();
            yield return Play(director, 2, 20f, .5f, .5f, 1.5f);
            float until = Time.realtimeSinceStartup + 540f;
            while (director.Current != HouseRoundDirector.Stage.Over)
            {
                if (Time.realtimeSinceStartup > until) Assert.Fail("Timed out waiting for: a bot to win Riddle Rush. " + Standing(director, joins));
                yield return null;
            }
            var race = director.Race;
            Assert.AreEqual(RiddleStage.Won, race.Stage);
            Assert.IsNull(SmashableLabel.Pulse, "the match end stops the label pulse");
            var winner = joins.Players.First(p => p.Index == race.Winner);
            Assert.IsInstanceOf<BotBinding>(winner.Binding);
            Assert.AreEqual(2, race.Points(winner.Index));
            Assert.AreEqual(Phase.MatchOver, RoundManager.Instance.Phase);
            Assert.AreEqual(winner, RoundManager.Instance.LastWinner);
            yield return TestScenes.WaitUntil(() => hud.ResultShown, 5f, "the result card");
            var card = hud.transform.Find("Safe HUD/Result/Result card");
            Assert.AreEqual($"{winner.Name} wins {Wreckabulary.Rules.HouseModes.Name(Wreckabulary.Rules.HouseModes.RiddleRush)}!", card.Find("Heading").GetComponent<TMPro.TMP_Text>().text);
            var lines = card.Find("Players").GetComponent<TMPro.TMP_Text>().text.Split('\n');
            Assert.AreEqual(joins.Players.Count, lines.Length, "one line per player");
            Assert.AreEqual($"<b>{winner.Name} · 2 points</b>", lines[0], "the winner leads the list");
            Assert.IsTrue(lines.Skip(1).All(l => !l.StartsWith("<b>")));
            Assert.Greater(race.Asked, 1);
        }
    }
}
