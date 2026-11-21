using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class ModeGuidesTests
    {
        static readonly string[] Courses = { "TimeRun", "Freestyle", "TeamRace", "BrawlRace", "FreeForAll" };
        static readonly string[] Landmarks = { "VAN" };
        static readonly string[] ExistingCards =
        {
            "Tutorial-1", "Tutorial-2", "Tutorial-3", "Tutorial-4", "Dibs-1", "Dibs-2", "Dibs-3", "Dibs-4", "Duos-1", "Duos-2", "Duos-3",
            "Deathmatch-1", "Deathmatch-2", "Deathmatch-3", "Deathmatch-4", "War-1", "War-2", "War-3", "War-4",
            "MovingOut-1", "MovingOut-2", "MovingOut-3", "MovingDay-1", "MovingDay-2", "MovingDay-3", "MovingDay-4",
            "Escape-1", "Escape-2", "Escape-3", "ShoppingList-1", "ShoppingList-2", "ShoppingList-3", "ShoppingList-4",
            "RiddleRush-1", "RiddleRush-2", "RiddleRush-3", "RiddleRush-4", "TimeRun-1", "TimeRun-2", "TimeRun-3",
            "Freestyle-1", "Freestyle-2", "Freestyle-3",
        };
        static readonly string[] Banned = { "craft", "summon", "build", "make", "wordbound", "flagship" };

        static string[] Expected => HouseModes.All.Select(m => m.Id).Concat(Courses).ToArray();

        static string DesktopKey(PlayerControl control) => control switch
        {
            PlayerControl.Attack => "LMB",
            PlayerControl.Spell => "Q",
            PlayerControl.Grab => "E",
            _ => control.ToString().ToUpperInvariant(),
        };

        static string Filled(string line, ControlDevice device) => ModeGuideBook.Fill(line, device,
            control => device == ControlDevice.MouseKeyboard ? DesktopKey(control) : ControlWords.Key(device, control));

        static ModeGuideStep[] Steps => ModeGuideBook.All.SelectMany(g => g.Steps).ToArray();

        static ItemCatalogue HouseItems()
        {
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json"));
            return items;
        }

        static bool CanSpell(ItemCatalogue house, string mode, string word) => HouseModes.Has(mode)
            ? house.TryGet(word, out var item) && item.Enabled
            : ModeDefinition.For(mode).Recipes.Contains(word);

        static string Text(ModeGuide guide) => string.Join(" ", guide.Steps.Select(s => s.Title + " " + s.Line));

        [Test]
        public void EveryRegistryModeAndEveryCourseModeHasAGuideAndNothingElseDoes()
        {
            foreach (string id in Expected) Assert.IsTrue(ModeGuideBook.Has(id), id);
            CollectionAssert.AreEquivalent(Expected, ModeGuideBook.All.Select(g => g.Mode).ToArray());
            CollectionAssert.AllItemsAreUnique(ModeGuideBook.All.Select(g => g.Mode).ToArray());
            CollectionAssert.AreEquivalent(Courses, WorkshopModes.All.ToArray(), "every course mode is covered");
        }

        [Test]
        public void TheCardsThatAlreadyHavePicturesKeepTheirIds()
        {
            CollectionAssert.AreEqual(ExistingCards, ModeGuideBook.Images.Take(ExistingCards.Length).ToArray(), "new guides are added after the pictured ones");
            Assert.AreEqual("TimeRun", ModeGuideBook.TimeTrial);
            Assert.AreEqual("Freestyle", ModeGuideBook.Freeplay);
        }

        [Test]
        public void EveryGuideHasThreeOrFourSteps()
        {
            foreach (var guide in ModeGuideBook.All) Assert.That(guide.Steps.Count, Is.InRange(3, 4), guide.Mode);
        }

        [Test]
        public void EveryStepHasAShortTitleOnePlainLineAndAPicture()
        {
            foreach (var guide in ModeGuideBook.All)
                for (int i = 0; i < guide.Steps.Count; i++)
                {
                    var step = guide.Steps[i];
                    string where = guide.Mode + " step " + (i + 1);
                    Assert.That(step.Title.Split(' ').Length, Is.InRange(2, 4), where);
                    Assert.AreEqual(step.Title.Trim(), step.Title, where);
                    Assert.IsNotEmpty(step.Line, where);
                    Assert.That(step.Line.Length, Is.LessThanOrEqualTo(72), where);
                    Assert.IsTrue(".!".Contains(step.Line[^1]), where + " ends a sentence");
                    Assert.IsFalse(Regex.IsMatch(step.Line, @"[.!?;]\s"), where + " is one sentence");
                    Assert.IsFalse(step.Line.Contains('\n'), where);
                    Assert.AreEqual(guide.Mode + "-" + (i + 1), step.Image, where);
                    Assert.IsNotEmpty(step.Art, where);
                }
            CollectionAssert.AllItemsAreUnique(Steps.Select(s => s.Image).ToArray());
            CollectionAssert.AreEqual(Steps.Select(s => s.Image).ToArray(), ModeGuideBook.Images.ToArray());
        }

        [Test]
        public void TheFallbackArtIsAnItemThatModeCanSpell()
        {
            var house = HouseItems();
            foreach (var guide in ModeGuideBook.All)
                foreach (var step in guide.Steps)
                    Assert.IsTrue(CanSpell(house, guide.Mode, step.Art), step.Image + " art " + step.Art + " cannot be spelled in " + guide.Mode);
        }

        [Test]
        public void CaptionsOnlyNameItemsThatModeCanSpell()
        {
            var house = HouseItems();
            var word = new Regex(@"\b[A-Z]{3,}\b");
            foreach (var guide in ModeGuideBook.All)
                foreach (var step in guide.Steps)
                    foreach (System.Text.RegularExpressions.Match named in word.Matches(step.Title + " " + step.Line))
                        Assert.IsTrue(Landmarks.Contains(named.Value) || CanSpell(house, guide.Mode, named.Value), step.Image + " names " + named.Value);
        }

        [Test]
        public void TheRaceGuidesTeachLettersCheckpointsAndTheKeepsake()
        {
            foreach (string mode in new[] { ModeGuideBook.TeamRace, ModeGuideBook.BrawlRace })
            {
                var definition = ModeDefinition.For(mode);
                string text = Text(ModeGuideBook.For(mode));
                Assert.IsTrue(definition.IsRace, mode);
                Assert.AreEqual(2, definition.TeamCount, mode);
                StringAssert.Contains("Two teams", text, mode);
                StringAssert.Contains("checkpoint", text, mode);
                StringAssert.Contains("keepsake", text, mode);
                StringAssert.Contains("lap", text, mode);
            }
            string race = Text(ModeGuideBook.For(ModeGuideBook.TeamRace)), brawl = Text(ModeGuideBook.For(ModeGuideBook.BrawlRace));
            StringAssert.Contains("{Grab}", race, "courses collect letters by hand");
            Assert.IsFalse(ModeDefinition.For(ModeGuideBook.TeamRace).EnemyDamage);
            Assert.IsFalse(Regex.IsMatch(race, @"\b(hit|knock|brawl)", RegexOptions.IgnoreCase), "Team Race has no fighting");
            Assert.IsTrue(ModeDefinition.For(ModeGuideBook.BrawlRace).EnemyDamage);
            StringAssert.Contains("hit your rivals", brawl);
            StringAssert.Contains("Knock out", brawl);
        }

        [Test]
        public void TheArenaFreeForAllGuideQuotesTheOnlineRules()
        {
            var definition = ModeDefinition.For(ModeGuideBook.ArenaFreeForAll);
            string text = Text(ModeGuideBook.For(ModeGuideBook.ArenaFreeForAll));
            Assert.AreEqual(WordboundObjective.Knockouts, definition.Objective);
            StringAssert.Contains("Up to " + definition.MaximumPlayers + " players", text);
            StringAssert.Contains("First to " + definition.KnockoutTarget + " knockouts", text);
            StringAssert.Contains("arena", text);
            StringAssert.Contains("{Grab}", text, "courses collect letters by hand");
            Assert.AreNotEqual(Text(ModeGuideBook.For(HouseModes.FreeForAll)), text);
            Assert.AreNotEqual(ModeGuideBook.For(HouseModes.FreeForAll).Steps[0].Title, ModeGuideBook.For(ModeGuideBook.ArenaFreeForAll).Steps[0].Title);
        }

        [Test]
        public void FreeRoamOnlyPromisesTheItemsACourseAllows()
        {
            string text = Text(ModeGuideBook.For(ModeGuideBook.Freeplay));
            Assert.IsFalse(Regex.IsMatch(text, @"\b(anything|any item)\b", RegexOptions.IgnoreCase), text);
            StringAssert.Contains("the items this course allows", text);
            StringAssert.Contains("practice station", text);
            Assert.AreEqual(0, ModeDefinition.For(ModeGuideBook.Freeplay).TimeLimitSeconds, "the guide says there is no clock");
            Assert.Less(ModeDefinition.For(ModeGuideBook.Freeplay).Recipes.Count, HouseItems().Enabled.Count(), "courses allow fewer items than houses");
        }

        [Test]
        public void EveryGuideIsHeadedByThePlayerNameAndTheTwoFreeForAllsAreToldApart()
        {
            Assert.AreEqual("Last One Standing · How to play", ModeGuideBook.Eyebrow(HouseModes.Dibs));
            Assert.AreEqual("Time Trial · How to play", ModeGuideBook.Eyebrow(ModeGuideBook.TimeTrial));
            Assert.AreEqual("Free Roam · How to play", ModeGuideBook.Eyebrow(ModeGuideBook.Freeplay));
            Assert.AreEqual("Team Race · How to play", ModeGuideBook.Eyebrow(ModeGuideBook.TeamRace));
            Assert.AreEqual("Brawl Race · How to play", ModeGuideBook.Eyebrow(ModeGuideBook.BrawlRace));
            Assert.AreEqual("Free-for-All · House · How to play", ModeGuideBook.Eyebrow(HouseModes.FreeForAll));
            Assert.AreEqual("Free-for-All · Arena · How to play", ModeGuideBook.Eyebrow(ModeGuideBook.ArenaFreeForAll));
            Assert.AreEqual(ModeGuideBook.Name(HouseModes.FreeForAll), ModeGuideBook.Name(ModeGuideBook.ArenaFreeForAll), "one player name for both");
            Assert.AreEqual("Riddles · How to play", ModeGuideBook.Eyebrow(HouseModes.RiddleRush, "Riddles"));
            CollectionAssert.AllItemsAreUnique(ModeGuideBook.All.Select(g => ModeGuideBook.Eyebrow(g.Mode)).ToArray());
            foreach (var guide in ModeGuideBook.All)
            {
                string name = ModeGuideBook.Name(guide.Mode);
                Assert.IsNotEmpty(name, guide.Mode);
                StringAssert.StartsWith(name, ModeGuideBook.Eyebrow(guide.Mode), guide.Mode);
                bool shared = ModeGuideBook.All.Count(other => ModeGuideBook.Name(other.Mode) == name) > 1;
                Assert.AreEqual(shared, ModeGuideBook.Place(guide.Mode).Length > 0, guide.Mode + " names its place only when the name is shared");
            }
        }

        [Test]
        public void GuidesNeverUseAnOlderModeName()
        {
            var old = new Regex(@"\b(dibs|deathmatch|freeplay|freestyle|relay|moving day|moving out|shopping list|riddle rush|time run)\b", RegexOptions.IgnoreCase);
            foreach (var guide in ModeGuideBook.All)
            {
                Assert.IsFalse(old.IsMatch(ModeGuideBook.Eyebrow(guide.Mode)), guide.Mode);
                foreach (var step in guide.Steps) Assert.IsFalse(old.IsMatch(step.Title) || old.IsMatch(step.Line), step.Image);
            }
        }

        [Test]
        public void EveryLineNamesOneKeyForEachDevice()
        {
            foreach (var step in Steps)
            {
                foreach (string token in ModeGuideBook.Tokens(step.Line)) Assert.IsTrue(ModeGuideBook.IsToken(token), step.Image + " {" + token + "}");
                foreach (ControlDevice device in Enum.GetValues(typeof(ControlDevice)))
                {
                    string line = Filled(step.Line, device);
                    string where = step.Image + " on " + device + ": " + line;
                    Assert.IsFalse(line.Contains('{') || line.Contains('}'), where);
                    Assert.IsFalse(line.Contains("  ") || line.StartsWith(" ", StringComparison.Ordinal), where + " has an empty key");
                }
            }
        }

        [Test]
        public void KeysAndVerbsFollowTheDevice()
        {
            Assert.AreEqual("Tap SMASH to break furniture into letters.", Filled("{Press} {Attack} to break furniture into letters.", ControlDevice.Touch));
            Assert.AreEqual("Press X to break furniture into letters.", Filled("{Press} {Attack} to break furniture into letters.", ControlDevice.Gamepad));
            Assert.AreEqual("Use K to spell.", Filled("Use {Spell} to spell.", ControlDevice.KeysLeft));
            Assert.AreEqual("Use Q to spell.", Filled("Use {Spell} to spell.", ControlDevice.MouseKeyboard));
            Assert.AreEqual("Keep {Nope}.", ModeGuideBook.Fill("Keep {Nope}.", ControlDevice.Gamepad, _ => "X"));
        }

        [Test]
        public void GuidesUseThePlayerWords()
        {
            foreach (var step in Steps)
                foreach (string word in Banned)
                {
                    var pattern = new Regex(@"\b" + word, RegexOptions.IgnoreCase);
                    Assert.IsFalse(pattern.IsMatch(step.Title) || pattern.IsMatch(step.Line), step.Image + " says " + word);
                }
        }

        [Test]
        public void UnknownModesHaveNoGuide()
        {
            Assert.IsFalse(ModeGuideBook.Has(null));
            Assert.IsFalse(ModeGuideBook.Has("Workshop"));
            Assert.IsFalse(ModeGuideBook.Has("freeforall"), "mode ids are exact");
            Assert.IsNull(ModeGuideBook.For("Hub"));
            Assert.AreEqual("Dibs-2", ModeGuideBook.ImageId("Dibs", 2));
        }
    }
}
