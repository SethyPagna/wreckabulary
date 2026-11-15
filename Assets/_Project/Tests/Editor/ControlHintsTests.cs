using NUnit.Framework;
using Wreckabulary.Rules;

namespace Wreckabulary.EditorTests
{
    public class ControlHintsTests
    {
        static readonly InputBinding Left = new KeyboardBinding(KeyboardBinding.Side.Left), Right = new KeyboardBinding(KeyboardBinding.Side.Right);

        [Test]
        public void EachSeatReadsItsOwnDevice()
        {
            Assert.AreEqual(ControlDevice.MouseKeyboard, ControlHints.Device(DesktopBinding.Shared));
            Assert.AreEqual(ControlDevice.KeysLeft, ControlHints.Device(Left));
            Assert.AreEqual(ControlDevice.KeysRight, ControlHints.Device(Right));
            Assert.AreEqual(ControlDevice.Touch, ControlHints.Device(TouchBinding.Shared));
            Assert.AreEqual(ControlDevice.MouseKeyboard, ControlHints.Device(new ScriptedBinding()), "scripted seats read as mouse and keyboard");
            Assert.AreEqual(ControlDevice.MouseKeyboard, ControlHints.Device(null));
        }

        [Test]
        public void EachSeatGetsOneKeyForEachControl()
        {
            Assert.AreEqual("K", ControlHints.For(Left, PlayerControl.Spell));
            Assert.AreEqual("R-SHIFT", ControlHints.For(Right, PlayerControl.Spell));
            Assert.AreEqual("SPELL", ControlHints.For(TouchBinding.Shared, PlayerControl.Spell));
            Assert.AreEqual("Y", ControlHints.For(ControlDevice.Gamepad, PlayerControl.Spell));
            Assert.AreEqual(ControlHints.PadBag, ControlHints.For(ControlDevice.Gamepad, PlayerControl.Book));
            Assert.AreEqual(ControlHints.PadHands, ControlHints.For(ControlDevice.Gamepad, PlayerControl.Hands));
            Assert.AreEqual("START", ControlHints.For(ControlDevice.Gamepad, PlayerControl.Pause), "Start pauses a House party on a pad");
            foreach (PlayerControl control in System.Enum.GetValues(typeof(PlayerControl)))
            {
                string key = ControlHints.For(DesktopBinding.Shared, control);
                StringAssert.DoesNotContain(",", key, control + " names one key");
                StringAssert.DoesNotContain(" or ", key, control + " names one key");
            }
            Assert.IsNotEmpty(ControlHints.For(DesktopBinding.Shared, PlayerControl.Spell));
            StringAssert.Contains(" / ", ControlHints.For(DesktopBinding.Shared, PlayerControl.Hands), "Hand 1 and Hand 2 keys");
        }

        [Test]
        public void OnlyKeyboardHalvesHaveNoBook()
        {
            Assert.IsTrue(ControlHints.HasBook(DesktopBinding.Shared));
            Assert.IsTrue(ControlHints.HasBook(TouchBinding.Shared));
            Assert.IsFalse(ControlHints.HasBook(Left));
            Assert.IsFalse(ControlHints.HasBook(Right));
            Assert.IsFalse(ControlHints.HasBook(null), "no seat, no book step");
        }

        [Test]
        public void JoinLinesNameOneKeyPerDevice()
        {
            string jump = ControlHints.For(DesktopBinding.Shared, PlayerControl.Jump);
            Assert.AreEqual($"Press {jump} to join, or Start on a controller", ControlHints.Join("join"), "the mouse seat joins with its jump key");
            Assert.AreEqual(ControlWords.JoinMore, ControlHints.Players);
            Assert.AreEqual(ControlWords.JoinMore, ControlHints.JoinMore);
        }
    }
}
