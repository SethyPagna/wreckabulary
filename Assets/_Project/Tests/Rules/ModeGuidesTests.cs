using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public sealed class ModeGuidesTests
    {
        static readonly string[] Courses = { "TimeRun", "Freestyle" };
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

        [Test]
        public void EveryRegistryModeAndBothCoursesHaveAGuideAndNothingElseDoes()
        {
            foreach (string id in Expected) Assert.IsTrue(ModeGuideBook.Has(id), id);
            CollectionAssert.AreEquivalent(Expected, ModeGuideBook.All.Select(g => g.Mode).ToArray());
            CollectionAssert.AllItemsAreUnique(ModeGuideBook.All.Select(g => g.Mode).ToArray());
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
        public void TheFallbackArtIsARealItem()
        {
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json"));
            foreach (var step in Steps) Assert.IsTrue(items.TryGet(step.Art, out _), step.Image + " art " + step.Art);
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
            Assert.IsFalse(ModeGuideBook.Has("FreeForAll"));
            Assert.IsNull(ModeGuideBook.For("Hub"));
            Assert.AreEqual("Dibs-2", ModeGuideBook.ImageId("Dibs", 2));
        }
    }
}
