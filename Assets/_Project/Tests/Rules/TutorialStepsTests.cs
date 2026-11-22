using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class TutorialStepsTests
    {
        static readonly TutorialStep[] Added = { TutorialStep.Place, TutorialStep.Heal, TutorialStep.KnockedOut, TutorialStep.Block };
        static readonly ControlDevice[] FixedKeys = { ControlDevice.KeysLeft, ControlDevice.KeysRight, ControlDevice.Gamepad, ControlDevice.Touch };
        static ControlDevice[] Devices => (ControlDevice[])Enum.GetValues(typeof(ControlDevice));
        static KnockoutBeat[] Beats => (KnockoutBeat[])Enum.GetValues(typeof(KnockoutBeat));

        static ItemCatalogue HouseItems()
        {
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json"));
            return items;
        }

        static string KeyOr(ControlDevice device, PlayerControl control, string desktop) =>
            device == ControlDevice.MouseKeyboard ? desktop : ControlWords.Key(device, control);

        static IEnumerable<string> Hints(ControlDevice d)
        {
            yield return TutorialSteps.PlaceHow(d, KeyOr(d, PlayerControl.Attack, "LMB"), TutorialSteps.PlaceWord);
            yield return TutorialSteps.HealHow(d, KeyOr(d, PlayerControl.Attack, "LMB"), TutorialSteps.HealWord, 30, false);
            yield return TutorialSteps.HealHow(d, KeyOr(d, PlayerControl.Attack, "LMB"), TutorialSteps.HealWord, 30, true);
            yield return TutorialSteps.BlockHow(d, KeyOr(d, PlayerControl.Block, "LMB"), TutorialSteps.ShieldWord);
            foreach (var beat in Beats) yield return TutorialSteps.KnockedOutHow(d, KeyOr(d, PlayerControl.Grab, "E"), beat, 2);
            yield return TutorialSteps.HandsFullHow(d, KeyOr(d, PlayerControl.Drop, "R"));
        }

        [Test]
        public void EveryStepIsTaughtOnceAndTheFirstEightKeepTheirPlaces()
        {
            var all = (TutorialStep[])Enum.GetValues(typeof(TutorialStep));
            CollectionAssert.AreEquivalent(all, TutorialSteps.Order);
            Assert.AreEqual(15, TutorialSteps.Order.Count);
            CollectionAssert.AreEqual(new[]
            {
                TutorialStep.Walk, TutorialStep.JumpDodge, TutorialStep.Smash, TutorialStep.Collect,
                TutorialStep.Book, TutorialStep.Spell, TutorialStep.Hit, TutorialStep.Throw,
            }, TutorialSteps.Order.Take(8), "the guide cards and the QA walk rely on the first steps");
            Assert.AreEqual(TutorialStep.Finish, TutorialSteps.Order[TutorialSteps.Order.Count - 1], "the tutorial still ends by knocking out the dummy");
        }

        [Test]
        public void TheOldStepsKeepTheirOrderAroundTheNewOnes()
        {
            CollectionAssert.AreEqual(new[]
            {
                TutorialStep.Walk, TutorialStep.JumpDodge, TutorialStep.Smash, TutorialStep.Collect, TutorialStep.Book, TutorialStep.Spell,
                TutorialStep.Hit, TutorialStep.Throw, TutorialStep.Effect, TutorialStep.Hands, TutorialStep.Finish,
            }, TutorialSteps.Order.Where(s => !Added.Contains(s)));
            CollectionAssert.AreEqual(new[] { TutorialStep.Place, TutorialStep.Heal, TutorialStep.KnockedOut, TutorialStep.Block },
                TutorialSteps.Order.Where(Added.Contains));
        }

        [Test]
        public void HandsAreNeverFullWhenAStepAsksForASpell()
        {
            var order = TutorialSteps.Order.ToList();
            int place = order.IndexOf(TutorialStep.Place), heal = order.IndexOf(TutorialStep.Heal), effect = order.IndexOf(TutorialStep.Effect);
            int knockedOut = order.IndexOf(TutorialStep.KnockedOut), block = order.IndexOf(TutorialStep.Block);
            Assert.Less(order.IndexOf(TutorialStep.Throw), place, "the throw leaves one hand free");
            Assert.Less(place, effect, "a placed TABLE and an eaten APPLE leave the hand free again, so they come before the second weapon");
            Assert.Less(heal, effect);
            Assert.Less(effect, knockedOut, "BAT and LAMP fill both hands; the knockout empties them");
            Assert.Less(knockedOut, block, "so the shield is spelled after the knockout");
            Assert.AreEqual(2, TestData.HouseRulesFor(HouseModes.Tutorial).MaxCarried);
        }

        [Test]
        public void EveryStepIsOneShortPlainSentence()
        {
            foreach (var step in TutorialSteps.Order)
            {
                string text = TutorialSteps.Text(step);
                Assert.IsNotEmpty(text, step.ToString());
                Assert.LessOrEqual(TutorialSteps.Words(text), TutorialSteps.MaxWords, text);
                StringAssert.DoesNotContain(".", text, "one sentence, shown as a heading");
                StringAssert.DoesNotContain("\n", text);
                Assert.IsTrue(char.IsUpper(text[0]), text);
            }
            Assert.AreEqual(TutorialSteps.Order.Count, TutorialSteps.Order.Select(TutorialSteps.Text).Distinct().Count());
            Assert.AreEqual("Spell TABLE, then put it down", TutorialSteps.Text(TutorialStep.Place));
            Assert.AreEqual("Take a punch, then spell APPLE and eat it", TutorialSteps.Text(TutorialStep.Heal));
            Assert.AreEqual("Let the dummy knock you out", TutorialSteps.Text(TutorialStep.KnockedOut));
            Assert.AreEqual("Spell PLATE, then block the dummy's punch", TutorialSteps.Text(TutorialStep.Block));
        }

        [Test]
        public void WordsCountsWhatAPlayerReads()
        {
            Assert.AreEqual(0, TutorialSteps.Words(null));
            Assert.AreEqual(0, TutorialSteps.Words("  "));
            Assert.AreEqual(2, TutorialSteps.Words("Walk around"));
            Assert.AreEqual(9, TutorialSteps.Words("Take a punch, then spell APPLE and eat it"));
        }

        [Test]
        public void OnlyTheNewStepsGiveUp()
        {
            CollectionAssert.AreEquivalent(Added, TutorialSteps.Order.Where(TutorialSteps.GivesUp));
            Assert.Greater(TutorialSteps.GiveUpSeconds, 30f, "long enough to try twice");
        }

        [Test]
        public void EveryHintNamesTheControlOnEveryDevice()
        {
            foreach (var d in FixedKeys)
            {
                bool touch = d == ControlDevice.Touch;
                string attack = ControlWords.Key(d, PlayerControl.Attack);
                StringAssert.Contains($" {(touch ? TutorialSteps.TouchPlace : attack)} to place", TutorialSteps.PlaceHow(d, attack, "TABLE"));
                StringAssert.Contains($" {(touch ? TutorialSteps.TouchUse : attack)} to eat", TutorialSteps.HealHow(d, attack, "APPLE", 30, true));
                StringAssert.Contains(ControlWords.Key(d, PlayerControl.Block), TutorialSteps.BlockHow(d, ControlWords.Key(d, PlayerControl.Block), "PLATE"));
                StringAssert.Contains(ControlWords.Key(d, PlayerControl.Grab), TutorialSteps.KnockedOutHow(d, ControlWords.Key(d, PlayerControl.Grab), KnockoutBeat.Back, 2));
                StringAssert.Contains(ControlWords.Key(d, PlayerControl.Drop), TutorialSteps.HandsFullHow(d, ControlWords.Key(d, PlayerControl.Drop)));
                foreach (var control in new[] { PlayerControl.Attack, PlayerControl.Block, PlayerControl.Grab, PlayerControl.Drop })
                    Assert.IsNotEmpty(ControlWords.Key(d, control), $"{d} {control}");
            }
            StringAssert.Contains("Hold LMB", TutorialSteps.BlockHow(ControlDevice.MouseKeyboard, "LMB", "PLATE"));
        }

        [Test]
        public void HintsAreShortFinishedSentencesWithNoGaps()
        {
            foreach (var d in Devices)
                foreach (string hint in Hints(d))
                {
                    Assert.IsNotEmpty(hint);
                    Assert.LessOrEqual(hint.Length, 90, hint);
                    StringAssert.EndsWith(".", hint);
                    StringAssert.DoesNotContain("  ", hint);
                    StringAssert.DoesNotContain("{", hint);
                    string words = hint.Replace(" . ", " KEY ");
                    StringAssert.DoesNotContain(" .", words);
                    Assert.LessOrEqual(words.Count(c => c == '.'), 2, hint);
                }
        }

        [Test]
        public void TouchTapsAndEveryoneElsePresses()
        {
            StringAssert.Contains("Tap PLACE to place it", TutorialSteps.PlaceHow(ControlDevice.Touch, "SMASH", "TABLE"));
            StringAssert.DoesNotContain("SMASH", TutorialSteps.PlaceHow(ControlDevice.Touch, "SMASH", "TABLE"), "the touch button reads PLACE while a TABLE is held");
            StringAssert.Contains("Press X to place it", TutorialSteps.PlaceHow(ControlDevice.Gamepad, "X", "TABLE"));
            StringAssert.Contains("Tap USE to eat it and heal 30", TutorialSteps.HealHow(ControlDevice.Touch, "SMASH", "APPLE", 30, true));
            StringAssert.Contains("Press J to eat it and heal 30", TutorialSteps.HealHow(ControlDevice.KeysLeft, "J", "APPLE", 30, true));
            StringAssert.Contains("Use HOLD DROP to drop", TutorialSteps.HandsFullHow(ControlDevice.Touch, "HOLD DROP"));
            StringAssert.Contains("Hold LB to drop", TutorialSteps.HandsFullHow(ControlDevice.Gamepad, "LB"));
            StringAssert.Contains("Tap INTERACT to pick", TutorialSteps.KnockedOutHow(ControlDevice.Touch, "INTERACT", KnockoutBeat.Back, 2));
        }

        [Test]
        public void TheKnockoutHintFollowsTheFall()
        {
            var lines = Beats.Select(b => TutorialSteps.KnockedOutHow(ControlDevice.Gamepad, "RT", b, 2)).ToArray();
            Assert.AreEqual(3, lines.Distinct().Count());
            StringAssert.Contains("drops your letters and both items", lines[(int)KnockoutBeat.Standing]);
            StringAssert.Contains("back in 2 seconds", lines[(int)KnockoutBeat.Down]);
            StringAssert.Contains("full health with empty hands", lines[(int)KnockoutBeat.Back]);
        }

        [Test]
        public void TheHealHintSendsAnUnhurtPlayerToTheDummyFirst()
        {
            string unhurt = TutorialSteps.HealHow(ControlDevice.Gamepad, "X", "APPLE", 30, false);
            StringAssert.Contains("punch you first", unhurt);
            StringAssert.DoesNotContain("APPLE", unhurt);
        }

        [Test]
        public void TheWordsTheStepsUseDoWhatTheStepsSay()
        {
            var items = HouseItems();
            var shield = items.Get(TutorialSteps.ShieldWord);
            Assert.IsTrue(shield.Enabled);
            Assert.AreEqual(1, shield.Hands);
            Assert.IsNotNull(shield.Shield, "PLATE is a shield");
            Assert.AreEqual(1f, shield.Shield.DamageReduction, "a blocked punch does no damage");
            Assert.Less(shield.Shield.FrontArcDegrees, 360f, "the hint says it stops hits from the front");
            Assert.GreaterOrEqual(shield.Shield.FrontArcDegrees, 90f);

            var placed = items.Get(TutorialSteps.PlaceWord);
            Assert.IsTrue(placed.Enabled);
            Assert.IsNotNull(placed.Deploy, "TABLE is put down, not swung");
            Assert.AreEqual(DeployEffect.Cover, placed.Deploy.Effect, "the hint calls it cover");
            Assert.IsNull(placed.Use);
            Assert.IsNull(placed.Thrown);

            var heal = items.Get(TutorialSteps.HealWord);
            Assert.IsTrue(heal.Enabled);
            Assert.AreEqual(1, heal.Hands);
            Assert.IsNotNull(heal.Use);
            Assert.AreEqual(UseEffect.Heal, heal.Use.Effect);
            Assert.AreEqual(30f, heal.Use.Amount, "the hint tests quote 30");
            Assert.IsTrue(heal.Consumable, "an eaten APPLE frees the hand again");
        }

        [Test]
        public void TheNewWordsFitTheBagAndComeFromFurnitureTheTutorialSpawns()
        {
            var rules = TestData.HouseRulesFor(HouseModes.Tutorial);
            foreach (string word in new[] { TutorialSteps.PlaceWord, TutorialSteps.HealWord, TutorialSteps.ShieldWord })
            {
                Assert.LessOrEqual(word.Length + 2, rules.MaxLetters, word + " fits beside the two letters left over from BAT");
                Assert.IsTrue(LetterBag.IsWord(word));
            }
            Assert.AreEqual(TutorialSteps.FirstFurniture, TutorialSteps.PlaceWord, "the TABLE the tutorial already spawns spells the placed item");
            Assert.GreaterOrEqual(rules.MaxDeployed, 1);
        }

        [Test]
        public void AKnockoutInTheTutorialComesBackAsTheHintSays()
        {
            var rules = TestData.HouseRulesFor(HouseModes.Tutorial);
            Assert.AreEqual(2f, rules.RespawnSeconds, "the tutorial brings a knockout back");
            Assert.IsFalse(rules.DownedEnabled, "a tutorial knockout is a full knockout, not downed");
            Assert.AreEqual(100f, rules.MaxHealth);
            Assert.AreEqual("", rules.StarterLetters, "back with an empty bag");
            Assert.Less(TestData.HouseRulesFor(HouseModes.Dibs).RespawnSeconds, 0f, "Last One Standing keeps a knockout out until the next round");
        }
    }
}
