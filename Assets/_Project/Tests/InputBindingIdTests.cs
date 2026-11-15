using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Wreckabulary.Tests
{
    public sealed class InputBindingIdTests
    {
        Gamepad pad;

        [TearDown]
        public void TearDown()
        {
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            pad = null;
        }

        [Test]
        public void KeyboardIdsKeepTheirNamesAndAreBuiltOnce()
        {
            var left = new KeyboardBinding(KeyboardBinding.Side.Left);
            Assert.AreEqual("keyboard-Left", left.Id);
            Assert.AreEqual("keyboard-Right", new KeyboardBinding(KeyboardBinding.Side.Right).Id);
            Assert.AreSame(left.Id, left.Id);
            Assert.That(() => { _ = left.Id; }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void GamepadIdFollowsTheDeviceAndIsBuiltOnce()
        {
            pad = InputSystem.AddDevice<Gamepad>();
            var binding = new GamepadBinding(pad);
            Assert.AreEqual("gamepad-" + pad.deviceId, binding.Id);
            Assert.AreSame(binding.Id, binding.Id);
            Assert.That(() => { _ = binding.Id; }, Is.Not.AllocatingGCMemory());
            Assert.DoesNotThrow(() => new GamepadBinding(null), "A binding without a pad still constructs.");
        }
    }
}
