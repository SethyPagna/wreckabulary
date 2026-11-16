using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class ShoppingListTests
    {
        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();
        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        static PlayerJoinManager Joins => Object.FindAnyObjectByType<PlayerJoinManager>();
        static GameHud Hud => Object.FindAnyObjectByType<GameHud>();

        static IEnumerator Load(string map)
        {
            Session.SelectMap(map);
            Match.ModeOverride = HouseModes.ShoppingList;
            Session.Remember(new ScriptedBinding());
            yield return TestScenes.Load(Session.DibsScene);
            Hud.Follow(Joins.Players[0]);
        }

        static IEnumerator Play(ShoppingListDirector director)
        {
            director.CountdownSeconds = .05f;
            director.Seed = 2024;
            director.StartMatch();
            yield return TestScenes.WaitUntil(() => director.Current == HouseRoundDirector.Stage.Playing, 3f, "the race to start");
            foreach (var p in Joins.Players)
            {
                var rules = Match.Rules.Clone(); rules.SpawnProtectionSeconds = 0f;
                p.Health.UseRules(rules);
            }
        }

        static string Card(string path) => Hud.UiCanvas.transform.Find("Safe HUD/Result/Result card/" + path).GetComponent<TMP_Text>().text;

        static string Seat(int seat) => Hud.UiCanvas.transform.Find($"Safe HUD/Roommate {seat}/Name").GetComponent<TMP_Text>().text;

        [UnityTest]
        public IEnumerator EveryoneGetsTheSameListAndQOffersYourCurrentItem()
        {
            yield return Load("flat");
            var director = ShoppingListDirector.Active;
            Assert.IsNotNull(director, "Shopping List attaches its own director.");
            Assert.AreEqual(4, Joins.Players.Count, "bots fill the empty seats");
            Assert.AreEqual(3, director.RoundsToWin);
            Assert.AreEqual(180f, director.TimeLimit);
            yield return Play(director);
            var race = director.Race;
            Assert.AreEqual(4, race.Items.Count);
            Assert.AreEqual(1, race.Items.Count(i => i.Kind != ShoppingKind.Weapon), string.Join(",", race.Items));
            foreach (var p in Joins.Players)
            {
                Assert.AreEqual(race.Items[0].Word, director.WantedWord(p));
                Assert.AreEqual(race.Items[0].Word, p.Summoner.WantedWord, "bots and Q read the director's word goal");
            }
            var human = Joins.Players[0];
            string wanted = race.Items[0].Word, filler = wanted == "BAT" ? "PAN" : "BAT";
            human.Inventory.Set(wanted + filler);
            Assert.AreEqual(wanted, human.Summoner.Options()[0].word, "the quick spell picks the list item first");
            human.Inventory.Set("");
            yield return TestScenes.WaitUntil(() => director.View && director.View.Root && director.View.Root.gameObject.activeInHierarchy, 2f, "the list panel");
            CollectionAssert.AreEqual(race.Items.Select(i => i.Word), director.View.LineTexts);
            Assert.AreEqual("Spell it", director.View.PromptText);
            yield return TestScenes.WaitUntil(() => Seat(1).Contains("0/4 items"), 1f, "progress on the roster cards");
        }

        [UnityTest]
        public IEnumerator SpellingThenUsingEachItemTicksItOffAndTheFirstToFinishWinsTheRound()
        {
            yield return Load("pinwheel");
            var director = ShoppingListDirector.Active;
            yield return Play(director);
            foreach (var p in Joins.Players)
            {
                if (p.TryGetComponent<BotController>(out var bot)) bot.enabled = false;
                if (p.Binding is BotBinding binding) binding.Commands = default;
            }
            var race = director.Race;
            var you = Joins.Players[0];
            var target = Joins.Players[1];
            foreach (var item in race.Items)
            {
                Assert.AreEqual(item.Word, race.Wanted(you.Index));
                you.Combat.ResetForRound();
                you.Inventory.Set(item.Word);
                Assert.IsTrue(you.Summoner.Summon(item.Word), item.Word);
                Assert.IsTrue(race.CurrentSpelled(you.Index), item.Word);
                if (item.Kind == ShoppingKind.Weapon)
                    target.Health.ApplyDamage(Hits.Of(you, Vector3.forward, HitSource.Melee, 1f, 0f, 0f, 1f, item.Word));
                else
                {
                    you.Combat.Attack();
                    yield return TestScenes.WaitUntil(() => race.Wanted(you.Index) != item.Word, 6f, $"{item.Word} to count");
                }
                yield return null;
                Assert.AreNotEqual(item.Word, race.Wanted(you.Index), $"{item.Word} ticks off");
                target.Health.ResetForRound();
            }
            Assert.AreEqual(you.Index, race.Winner);
            yield return TestScenes.WaitUntil(() => director.LastResult != null, 2f, "the round to end");
            Assert.AreEqual(1, director.WinsOf(you));
            Assert.IsTrue(director.BetweenRounds, "first to 3 rounds: the match goes on");
            yield return TestScenes.WaitUntil(() => Hud.ResultShown, 4f, "the round's result card");
            Assert.AreEqual("You got everything on the list!", Card("Heading"));
            Assert.AreEqual("ROUND 1 · YOU WIN", Card("Eyebrow"));
            StringAssert.StartsWith("<b>You · 4 of 4 items · 1/3 wins</b>", Card("Players"));
            Hud.ConfirmResult();
            yield return TestScenes.WaitUntil(() => director.Current == HouseRoundDirector.Stage.Playing && !director.BetweenRounds, 3f, "round 2");
            Assert.AreEqual(2, director.RoundNumber);
            Assert.AreEqual(0, director.Race.Done(you.Index), "a new round brings a new list");
        }

        [UnityTest]
        public IEnumerator BotsPlayARoundOfShoppingListToAWinner()
        {
            yield return Load("flat");
            var director = ShoppingListDirector.Active;
            yield return Play(director);
            Time.timeScale = 2f;
            yield return TestScenes.WaitUntil(() => director.LastResult != null, director.TimeLimit / 2f + 60f, "a bot to win the round");
            Time.timeScale = 1f;
            var result = director.LastResult;
            Assert.IsFalse(result.Draw, string.Join("\n", result.Lines));
            Assert.IsTrue(result.Winner && result.Winner.Binding is BotBinding, "a bot wins: the human seat stands still");
            Assert.GreaterOrEqual(director.Race.Done(result.Winner.Index), 1, "the winner ticked off at least one item");
            Assert.AreEqual(Joins.Players.Count, result.Lines.Length, "the card lists every player's progress");
            yield return TestScenes.WaitUntil(() => Hud.ResultShown, 4f, "the result card");
            StringAssert.Contains(result.Winner.Name, Card("Heading"), "the card names the winner");
            foreach (var p in Joins.Players.Where(p => p.Binding is BotBinding)) StringAssert.Contains(p.Name, Card("Players"));
        }
    }
}
