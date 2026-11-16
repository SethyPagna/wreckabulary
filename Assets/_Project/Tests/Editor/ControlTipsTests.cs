using System;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary.EditorTests
{
    public class ControlTipsTests
    {
        int? savedEnabled, savedLearned, savedSeen;

        static int? Read(string key) => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : null;

        [SetUp]
        public void Save()
        {
            savedEnabled = Read(ControlTips.EnabledKey);
            savedLearned = Read(ControlTips.LearnedKey);
            savedSeen = Read(ControlTips.SeenKey);
            ControlTips.Reset();
        }

        [TearDown]
        public void Restore()
        {
            ControlTips.Reset();
            foreach (ControlTips.Tip tip in Enum.GetValues(typeof(ControlTips.Tip)))
                if (savedLearned.HasValue && (savedLearned.Value & (1 << (int)tip)) != 0) ControlTips.Learn(tip);
            if (savedEnabled.HasValue) PlayerPrefs.SetInt(ControlTips.EnabledKey, savedEnabled.Value);
            if (savedSeen.HasValue) PlayerPrefs.SetInt(ControlTips.SeenKey, savedSeen.Value);
            PlayerPrefs.Save();
        }

        [Test]
        public void TipsComeOneAtATimeUntilEachControlIsUsed()
        {
            Assert.AreEqual(ControlTips.Tip.Move, ControlTips.Next(true));
            ControlTips.Learn(ControlTips.Tip.Move);
            Assert.AreEqual(ControlTips.Tip.Look, ControlTips.Next(true));
            Assert.AreEqual(ControlTips.Tip.Smash, ControlTips.Next(false), "Looking and aiming only apply over the shoulder.");
            foreach (ControlTips.Tip tip in Enum.GetValues(typeof(ControlTips.Tip))) ControlTips.Learn(tip);
            Assert.IsNull(ControlTips.Next(true), "Nothing left to teach: the view stays clear.");
        }

        [Test]
        public void SettingsTurnTipsOffAndReplayThem()
        {
            ControlTips.Learn(ControlTips.Tip.Move);
            ControlTips.Showing(ControlTips.Tip.Smash, 1f);
            Assert.AreNotEqual(0, PlayerPrefs.GetInt(ControlTips.SeenKey, 0));
            ControlTips.Enabled = false;
            Assert.IsNull(ControlTips.Next(true));
            ControlTips.Enabled = true;
            Assert.AreEqual(ControlTips.Tip.Move, ControlTips.Next(true), "Turning tips back on replays them from the start.");
            Assert.IsFalse(PlayerPrefs.HasKey(ControlTips.SeenKey), "and forgets how often they showed");
        }

        [Test]
        public void EveryTipNamesItsKeyAndTheLastPointsToSettings()
        {
            foreach (ControlTips.Tip tip in Enum.GetValues(typeof(ControlTips.Tip)))
                StringAssert.Contains("</color></b>", ControlTips.Text(tip), tip + " shows a key");
            StringAssert.Contains("Settings › Controls", ControlTips.Text(ControlTips.Tip.Menu), "The full list lives in Settings.");
        }

        [Test]
        public void PadsGetTheirOwnButtonsAndSkipTipsWithoutOne()
        {
            StringAssert.StartsWith("<b><color=#FFE7B8>Y</color></b>", ControlTips.Text(ControlTips.Tip.Spell, ControlDevice.Gamepad));
            StringAssert.Contains("hold to spell", ControlTips.Text(ControlTips.Tip.Spell, ControlDevice.Gamepad));
            StringAssert.Contains(">RB<", ControlTips.Text(ControlTips.Tip.Bag, ControlDevice.Gamepad));
            StringAssert.Contains(">START<", ControlTips.Text(ControlTips.Tip.Menu, ControlDevice.Gamepad));
            StringAssert.Contains("LEFT STICK", ControlTips.Text(ControlTips.Tip.Move, ControlDevice.Gamepad));
            foreach (ControlTips.Tip tip in Enum.GetValues(typeof(ControlTips.Tip)))
                StringAssert.DoesNotContain(",", Between(ControlTips.Text(tip, ControlDevice.Gamepad)), tip + " shows one button");

            ControlTips.Learn(ControlTips.Tip.Move);
            ControlTips.Learn(ControlTips.Tip.Look);
            ControlTips.Learn(ControlTips.Tip.Smash);
            Assert.AreEqual(ControlTips.Tip.Spell, ControlTips.Next(true, ControlDevice.Gamepad), "Pads have no aim button, so that tip is skipped.");
            Assert.AreEqual(ControlTips.Tip.Aim, ControlTips.Next(true, ControlDevice.MouseKeyboard));
            ControlTips.Learn(ControlTips.Tip.Spell);
            ControlTips.Learn(ControlTips.Tip.Interact);
            Assert.AreEqual(ControlTips.Tip.Menu, ControlTips.Next(false, ControlDevice.KeysLeft), "Keyboard halves have no bag key.");
            Assert.AreEqual(ControlTips.Tip.Bag, ControlTips.Next(false, ControlDevice.Gamepad));
        }

        [Test]
        public void KeyboardHalvesReadTheirOwnKeys()
        {
            StringAssert.StartsWith("<b><color=#FFE7B8>K</color></b>", ControlTips.Text(ControlTips.Tip.Spell, new KeyboardBinding(KeyboardBinding.Side.Left)));
            StringAssert.StartsWith("<b><color=#FFE7B8>R-SHIFT</color></b>", ControlTips.Text(ControlTips.Tip.Spell, new KeyboardBinding(KeyboardBinding.Side.Right)));
            StringAssert.StartsWith("<b><color=#FFE7B8>J</color></b>", ControlTips.Text(ControlTips.Tip.Smash, new KeyboardBinding(KeyboardBinding.Side.Left)));
        }

        [Test]
        public void UsingAControlOnAnyDeviceRetiresItsTip()
        {
            var commands = new PlayerCommands { move = Vector2.right, spellDown = true, grab = true };
            ControlTips.LearnFrom(commands, false, false);
            Assert.IsTrue(ControlTips.Learned(ControlTips.Tip.Move));
            Assert.IsTrue(ControlTips.Learned(ControlTips.Tip.Spell));
            Assert.IsTrue(ControlTips.Learned(ControlTips.Tip.Interact));
            Assert.IsFalse(ControlTips.Learned(ControlTips.Tip.Smash));
            ControlTips.LearnFrom(new PlayerCommands { look = Vector2.up }, false, true);
            Assert.IsFalse(ControlTips.Learned(ControlTips.Tip.Look), "looking only counts over the shoulder");
            Assert.IsTrue(ControlTips.Learned(ControlTips.Tip.Bag));
            ControlTips.LearnFrom(new PlayerCommands { look = Vector2.up }, true, false);
            Assert.IsTrue(ControlTips.Learned(ControlTips.Tip.Look));
        }

        [Test]
        public void ATipNobodyUsesStepsAsideAfterTwentySeconds()
        {
            for (int i = 0; i < 19; i++) ControlTips.Showing(ControlTips.Tip.Move, 1f);
            Assert.AreEqual(ControlTips.Tip.Move, ControlTips.Next(false));
            ControlTips.Showing(ControlTips.Tip.Move, 1f);
            Assert.IsTrue(ControlTips.Learned(ControlTips.Tip.Move));
            Assert.AreEqual(ControlTips.Tip.Smash, ControlTips.Next(false), "the next tip gets its turn");
        }

        [Test]
        public void ATipStepsAsideAfterThreeMatches()
        {
            for (int match = 0; match < TipRetirement.Matches; match++)
            {
                ControlTips.Showing(ControlTips.Tip.Move, 1f);
                Assert.AreEqual(ControlTips.Tip.Move, ControlTips.Next(false));
                ControlTips.BeginMatch();
            }
            Assert.AreEqual(ControlTips.Tip.Smash, ControlTips.Next(false), "the fourth match skips it");
            Assert.IsTrue(ControlTips.Learned(ControlTips.Tip.Move));
        }

        static string Between(string line)
        {
            int start = line.IndexOf("#FFE7B8>", StringComparison.Ordinal) + 8;
            int end = line.IndexOf("</color>", StringComparison.Ordinal);
            return line.Substring(start, end - start);
        }
    }
}
