using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Voice;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class VoiceSettingsTests
    {
        static Dictionary<string, string> Saved(VoiceSettings settings) => settings.Write().ToDictionary(pair => pair.Key, pair => pair.Value);

        static VoiceSettings ReadFrom(Dictionary<string, string> saved) =>
            VoiceSettings.Read(key => saved.TryGetValue(key, out string value) ? value : null);

        static void AssertDefaults(VoiceSettings settings, string why)
        {
            Assert.AreEqual(MicMode.Off, settings.Mic, why + ": the microphone starts off");
            Assert.IsFalse(settings.SelfMuted, why);
            Assert.IsTrue(settings.HearTeammates, why + ": you hear your team by default");
            Assert.AreEqual(VoiceSettings.DefaultVolume, settings.Volume, why);
            Assert.AreEqual("", settings.Device, why + ": the system default device");
            Assert.IsEmpty(settings.Muted, why);
        }

        [Test]
        public void FreshSettingsKeepTheMicrophoneOffAndVoiceAtEightyPercent()
        {
            AssertDefaults(new VoiceSettings(), "new");
            AssertDefaults(VoiceSettings.Read(_ => null), "nothing saved");
            AssertDefaults(VoiceSettings.Read(null), "no store");
            Assert.AreEqual(80, VoiceSettings.DefaultVolume);
        }

        [Test]
        public void EverySettingSurvivesASaveAndLoad()
        {
            var settings = new VoiceSettings { Mic = MicMode.PushToTalk, SelfMuted = true, HearTeammates = false, Volume = 40, Device = "USB Headset" };
            Assert.IsTrue(settings.SetMuted("player-b", true));
            Assert.IsTrue(settings.SetMuted("0f8c2a64-0000-4000-8000-000000000001", true));
            var saved = Saved(settings);
            CollectionAssert.AreEquivalent(VoiceSettings.Keys, saved.Keys);
            Assert.IsTrue(saved.Keys.All(key => key.StartsWith("wv.voice.")));
            Assert.AreEqual("1", saved[VoiceSettings.VersionKey]);
            Assert.AreEqual("push", saved[VoiceSettings.MicKey]);
            var loaded = ReadFrom(saved);
            Assert.AreEqual(MicMode.PushToTalk, loaded.Mic);
            Assert.IsTrue(loaded.SelfMuted);
            Assert.IsFalse(loaded.HearTeammates);
            Assert.AreEqual(40, loaded.Volume);
            Assert.AreEqual("USB Headset", loaded.Device);
            CollectionAssert.AreEquivalent(settings.Muted, loaded.Muted);
            foreach (var mode in new[] { MicMode.Off, MicMode.PushToTalk, MicMode.OpenMic })
                Assert.AreEqual(mode, ReadFrom(Saved(new VoiceSettings { Mic = mode })).Mic);
        }

        [Test]
        public void UnknownOrMissingVersionsFallBackToSafeDefaults()
        {
            var saved = Saved(new VoiceSettings { Mic = MicMode.OpenMic, Volume = 30, HearTeammates = false });
            foreach (string version in new[] { null, "", "0", "2", "one", "1.0", "99999999999" })
            {
                var copy = new Dictionary<string, string>(saved);
                if (version == null) copy.Remove(VoiceSettings.VersionKey); else copy[VoiceSettings.VersionKey] = version;
                AssertDefaults(ReadFrom(copy), "version " + (version ?? "missing"));
            }
        }

        [Test]
        public void CorruptFieldsFallBackOneByOneWithTheMicrophoneOff()
        {
            var saved = new Dictionary<string, string>
            {
                [VoiceSettings.VersionKey] = " 1 ", [VoiceSettings.MicKey] = "LOUD", [VoiceSettings.SelfMuteKey] = "yes",
                [VoiceSettings.HearKey] = "maybe", [VoiceSettings.VolumeKey] = "eighty", [VoiceSettings.DeviceKey] = "Mic\u0007",
                [VoiceSettings.MutedKey] = "good-id,bad id,,good-id, spaced ,x",
            };
            var loaded = ReadFrom(saved);
            Assert.AreEqual(MicMode.Off, loaded.Mic);
            Assert.IsFalse(loaded.SelfMuted);
            Assert.IsTrue(loaded.HearTeammates);
            Assert.AreEqual(80, loaded.Volume);
            Assert.AreEqual("", loaded.Device);
            CollectionAssert.AreEquivalent(new[] { "good-id", "spaced", "x" }, loaded.Muted);
            foreach (var (text, volume) in new[] { ("150", 100), ("-4", 0), ("0", 0), ("55", 55) })
            {
                saved[VoiceSettings.VolumeKey] = text;
                Assert.AreEqual(volume, ReadFrom(saved).Volume, text);
            }
            saved[VoiceSettings.DeviceKey] = new string('m', VoiceSettings.MaxDevice + 1);
            Assert.AreEqual("", ReadFrom(saved).Device, "an overlong device name is dropped");
            saved[VoiceSettings.MicKey] = null;
            Assert.AreEqual(MicMode.Off, ReadFrom(saved).Mic);
        }

        [Test]
        public void TheMuteSetIsKeyedByStablePlayerIdsAndBounded()
        {
            var settings = new VoiceSettings();
            Assert.IsFalse(settings.SetMuted(null, true));
            Assert.IsFalse(settings.SetMuted("", true));
            Assert.IsFalse(settings.SetMuted("two words", true));
            Assert.IsFalse(settings.SetMuted("a,b", true), "commas would break the saved list");
            Assert.IsTrue(settings.SetMuted("p1", true));
            Assert.IsFalse(settings.SetMuted("p1", true), "already muted");
            Assert.IsTrue(settings.IsMuted("p1"));
            Assert.IsFalse(settings.IsMuted("P1"), "ids are exact");
            Assert.IsTrue(settings.SetMuted("p1", false));
            Assert.IsFalse(settings.IsMuted("p1"));
            for (int i = 0; i < VoiceSettings.MaxMuted; i++) Assert.IsTrue(settings.SetMuted("p" + i, true));
            Assert.IsFalse(settings.SetMuted("one-more", true));
            Assert.AreEqual(VoiceSettings.MaxMuted, settings.Muted.Count);
            var copy = settings.Clone();
            copy.SetMuted("p0", false);
            Assert.IsTrue(settings.IsMuted("p0"), "a clone is independent");
        }

        [Test]
        public void VolumeAndDeviceStayInRange()
        {
            var settings = new VoiceSettings { Volume = 120 };
            Assert.AreEqual(100, settings.Volume);
            settings.Volume = -1;
            Assert.AreEqual(0, settings.Volume);
            settings.Device = null;
            Assert.AreEqual("", settings.Device);
            settings.Device = "   ";
            Assert.AreEqual("", settings.Device);
        }

        [Test]
        public void DeviceNamesShortenForTheSettingsRow()
        {
            Assert.AreEqual("Default", VoiceSettings.ShortDevice(""));
            Assert.AreEqual("Default", VoiceSettings.ShortDevice(null));
            Assert.AreEqual("USB Mic", VoiceSettings.ShortDevice("USB Mic"));
            Assert.AreEqual("Yeti", VoiceSettings.ShortDevice("Microphone (Yeti)"));
            string shortened = VoiceSettings.ShortDevice("Microphone (Realtek(R) Audio)");
            Assert.LessOrEqual(shortened.Length, 12);
            StringAssert.StartsWith("Realtek", shortened);
            StringAssert.EndsWith("…", shortened);
        }
    }
}
