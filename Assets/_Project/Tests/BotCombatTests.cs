using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class BotCombatTests
    {
        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();
        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        PlayerController bot, rival;

        IEnumerator BotAgainstOneRival(Vector3 botAt, Vector3 rivalAt)
        {
            Session.SelectMap("pinwheel");
            Match.ModeOverride = "Dibs";
            Session.Remember(new ScriptedBinding());
            yield return TestScenes.Load(Session.DibsScene);
            RoundManager.Instance.CountdownTime = .05f;
            yield return TestScenes.WaitUntil(() => RoundManager.Instance.Phase == Phase.Playing, 2f, "battle start");
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            foreach (var p in joins.Players)
            {
                if (p.TryGetComponent<BotController>(out var controller)) controller.enabled = false;
                if (p.Binding is BotBinding binding) binding.Commands = default;
                var rules = Match.Rules.Clone(); rules.SpawnProtectionSeconds = 0f;
                p.Health.UseRules(rules);
            }
            var bots = joins.Players.Where(p => p.Binding is BotBinding).ToArray();
            bot = bots[0];
            rival = bots[1];
            var away = new[] { new Vector3(10f, 0f, 10f), new Vector3(-10f, 0f, -11f), new Vector3(10f, 0f, -10f) };
            int far = 0;
            foreach (var p in joins.Players.Where(p => p != bot && p != rival)) { p.Respawn(away[far++ % away.Length]); p.Frozen = true; }
            rival.Respawn(rivalAt);
            rival.Frozen = true;
            bot.Respawn(botAt);
            yield return new WaitForSeconds(.2f);
        }

        static float Apart(PlayerController a, PlayerController b) => World.Flat(a.transform.position - b.transform.position).magnitude;

        [UnityTest]
        public IEnumerator ABotWithASpearStrikesFromTheSpearsReach()
        {
            yield return BotAgainstOneRival(new Vector3(-2f, 0f, 0f), new Vector3(.7f, 0f, 0f));
            bot.Inventory.Set("SPEAR");
            Assert.IsTrue(bot.Summoner.Summon("SPEAR"));
            float closest = Apart(bot, rival);
            Assert.Less(closest, BotTactics.StrikeRange(bot.Combat.Weapon.Definition), "the rival starts inside the spear's reach");
            bot.GetComponent<BotController>().enabled = true;
            float until = Time.time + 4f;
            while (rival.Health.Current >= rival.Health.Max && Time.time < until)
            {
                closest = Mathf.Min(closest, Apart(bot, rival));
                yield return null;
            }
            Assert.Less(rival.Health.Current, rival.Health.Max, "the spear landed");
            Assert.Greater(closest, BotTactics.FistRange + .25f, "the bot fought from the spear's reach, not toe to toe");
        }

        [UnityTest]
        public IEnumerator ABotWithABagFullOfJunkDropsALetter()
        {
            yield return BotAgainstOneRival(new Vector3(-2f, 0f, 0f), new Vector3(11f, 0f, -11f));
            bot.Inventory.Set("QQZZXXJJVV");
            Assert.IsTrue(bot.Inventory.IsFull);
            Assert.IsEmpty(WordSolver.Spellable(bot.Summoner.Words, bot.Inventory.Letters), "nothing to spell from these letters");
            bot.GetComponent<BotController>().enabled = true;
            yield return TestScenes.WaitUntil(() => bot.Inventory.Count < bot.Inventory.Capacity, 3f, "the bot drops a junk letter");
            Assert.IsTrue(TilePool.Instance.Active.Any(t => t && "QZXJV".IndexOf(t.Letter) >= 0), "the letter is back in play");
        }

        [UnityTest]
        public IEnumerator ABotRaisesTheShieldInItsOtherHandWhenHit()
        {
            yield return BotAgainstOneRival(new Vector3(-1f, 0f, 0f), new Vector3(.5f, 0f, 0f));
            bot.Inventory.Set("SHIELD");
            Assert.IsTrue(bot.Summoner.Summon("SHIELD"));
            bot.Inventory.Set("SPEAR");
            Assert.IsTrue(bot.Summoner.Summon("SPEAR"));
            Assert.AreEqual("SPEAR", bot.Combat.Weapon.word);
            Assert.AreEqual("SHIELD", bot.Combat.StoredGear.word);
            bot.GetComponent<BotController>().enabled = true;
            yield return null;
            bot.Health.ApplyDamage(Hits.Of(rival, bot.transform.position - rival.transform.position, HitSource.Melee, 5f, 0f, hitStun: 0f));
            yield return TestScenes.WaitUntil(() => bot.Combat.IsBlocking, 1.5f, "the bot raises its shield after the hit");
            Assert.AreEqual("SHIELD", bot.Combat.Weapon.word);
            yield return TestScenes.WaitUntil(() => bot.Combat.Weapon && bot.Combat.Weapon.word == "SPEAR", 3f, "the bot goes back to its spear");
        }
    }
}
