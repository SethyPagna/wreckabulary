using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Rules.Voice;
using Wreckabulary.Voice;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTests
{
    public class VoiceChatEditorTests
    {
        sealed class FakeTransport : IVoiceTransport
        {
            public readonly List<bool> SendCalls = new();
            public readonly HashSet<string> Muted = new();
            public bool Receiving;
            public VoiceRoom Room = VoiceRoom.Empty;
            public string RoomSelf;
            public bool Available => true;
            public string Status => "Connected";
            public bool Sending { get; private set; }
            public void SetSending(bool on) { Sending = on; SendCalls.Add(on); }
            public void SetReceiving(bool on) => Receiving = on;
            public float PlaybackVolume { get; set; }
            public void SetMuted(string playerId, bool muted) { if (muted) Muted.Add(playerId); else Muted.Remove(playerId); }
            public void SetRoom(VoiceRoom room, string self) { Room = room; RoomSelf = self; }
            public float InputLevel => .5f;
            public IReadOnlyList<string> Devices => new[] { "Desk mic" };
            public string Device { get; set; }
            public event Action<string, bool> SpeakingChanged;
            public event Action Changed;
            public void Speak(string playerId, bool on) => SpeakingChanged?.Invoke(playerId, on);
            public void Poke() => Changed?.Invoke();
        }

        string[] saved;
        bool[] had;

        [SetUp]
        public void SetUp()
        {
            had = VoiceSettings.Keys.Select(PlayerPrefs.HasKey).ToArray();
            saved = VoiceSettings.Keys.Select(key => PlayerPrefs.GetString(key, "")).ToArray();
            VoiceChat.Use(null);
            VoiceChat.Leave();
            VoiceChat.ResetToDefaults();
            VoiceChat.Tick(false, 0);
        }

        [TearDown]
        public void TearDown()
        {
            VoiceChat.SetTalkButton(false);
            VoiceChat.Use(null);
            VoiceChat.Leave();
            for (int i = 0; i < VoiceSettings.Keys.Length; i++)
                if (had[i]) PlayerPrefs.SetString(VoiceSettings.Keys[i], saved[i]); else PlayerPrefs.DeleteKey(VoiceSettings.Keys[i]);
            PlayerPrefs.Save();
            VoiceChat.Reload();
        }

        [Test]
        public void TheDefaultTransportSaysPlainlyThatVoiceIsNotConnected()
        {
            Assert.IsInstanceOf<NullVoiceTransport>(VoiceChat.Transport);
            Assert.IsFalse(VoiceChat.Available);
            Assert.AreEqual("Voice chat isn't connected in this build.", VoiceChat.Status);
            Assert.AreEqual(NullVoiceTransport.NotConnected, VoiceChat.Status);
            Assert.AreEqual(MicMode.Off, VoiceChat.Mic, "the microphone starts off");
            Assert.AreEqual(MicState.Off, VoiceChat.MicState);
            Assert.IsTrue(VoiceChat.HearTeammates);
            Assert.AreEqual(80, VoiceChat.Volume);
            VoiceChat.Mic = MicMode.OpenMic;
            Assert.IsTrue(VoiceChat.WantsToSend);
            Assert.IsFalse(VoiceChat.Sending, "nothing is ever sent without a voice service");
            Assert.AreEqual(0f, VoiceChat.InputLevel);
            var none = (NullVoiceTransport)VoiceChat.Transport;
            VoiceChat.Volume = 50;
            Assert.AreEqual(.5f, none.PlaybackVolume, 1e-5f);
            VoiceChat.HearTeammates = false;
            Assert.IsFalse(none.Receiving);
            VoiceChat.SetMuted("pal", true);
            Assert.IsTrue(none.IsMuted("pal"));
            VoiceChat.Join(VoiceRoom.Party(new[] { "me", "pal" }), "me");
            Assert.AreSame(VoiceChat.Room, none.Room);
            Assert.IsNotNull(VoiceChat.Devices);
        }

        [Test]
        public void PushToTalkSendsOnlyWhileHeldAndNeverWhenMutedOrOff()
        {
            var fake = new FakeTransport();
            VoiceChat.Use(fake);
            VoiceChat.Mic = MicMode.PushToTalk;
            VoiceChat.Tick(false, 1);
            Assert.IsFalse(fake.Sending);
            Assert.AreEqual(MicState.Ready, VoiceChat.MicState);
            VoiceChat.Tick(true, 1.1);
            Assert.IsTrue(fake.Sending, "holding the key sends");
            Assert.IsTrue(VoiceChat.TalkHeld);
            Assert.AreEqual(MicState.Live, VoiceChat.MicState);
            VoiceChat.Tick(true, 1.2);
            VoiceChat.Tick(false, 1.3);
            Assert.IsFalse(fake.Sending, "letting go stops at once");
            CollectionAssert.AreEqual(new[] { false, true, false }, fake.SendCalls, "one call per change");

            VoiceChat.SelfMuted = true;
            VoiceChat.Tick(true, 2);
            Assert.IsFalse(fake.Sending, "self-mute wins over the key");
            Assert.AreEqual(MicState.Muted, VoiceChat.MicState);
            VoiceChat.SetTalkButton(true);
            Assert.IsFalse(fake.Sending, "and over the touch button");
            VoiceChat.SelfMuted = false;
            Assert.IsTrue(fake.Sending, "unmuting while the key is held starts sending");
            VoiceChat.ToggleSelfMute();
            Assert.IsFalse(fake.Sending);
            VoiceChat.ToggleSelfMute();
            VoiceChat.SetTalkButton(false);
            VoiceChat.Tick(false, 2.5);
            Assert.IsFalse(fake.Sending);

            VoiceChat.Mic = MicMode.Off;
            VoiceChat.Tick(true, 3);
            VoiceChat.SetTalkButton(true);
            Assert.IsFalse(fake.Sending, "Off never sends");
            Assert.IsFalse(VoiceChat.TalkHeld);
            Assert.AreEqual(MicState.Off, VoiceChat.MicState);

            VoiceChat.Mic = MicMode.OpenMic;
            VoiceChat.Tick(false, 4);
            Assert.IsTrue(fake.Sending, "an open mic needs no key");
            VoiceChat.Use(null);
            Assert.IsFalse(fake.Sending, "switching transports never leaves a mic live");
        }

        [Test]
        public void SettingsSaveUnderVoiceKeysAndCorruptSavesComeBackOff()
        {
            VoiceChat.Mic = MicMode.OpenMic;
            VoiceChat.SelfMuted = true;
            VoiceChat.HearTeammates = false;
            VoiceChat.StepVolume(-3);
            VoiceChat.Device = "Desk mic";
            VoiceChat.SetMuted("pal-1", true);
            Assert.AreEqual("1", PlayerPrefs.GetString(VoiceSettings.VersionKey));
            Assert.AreEqual("open", PlayerPrefs.GetString(VoiceSettings.MicKey));
            Assert.AreEqual("50", PlayerPrefs.GetString(VoiceSettings.VolumeKey));
            Assert.AreEqual("pal-1", PlayerPrefs.GetString(VoiceSettings.MutedKey));
            VoiceChat.Reload();
            Assert.AreEqual(MicMode.OpenMic, VoiceChat.Mic);
            Assert.IsTrue(VoiceChat.SelfMuted);
            Assert.IsFalse(VoiceChat.HearTeammates);
            Assert.AreEqual(50, VoiceChat.Volume);
            Assert.AreEqual("Desk mic", VoiceChat.Device);
            Assert.IsTrue(VoiceChat.IsMuted("pal-1"));

            PlayerPrefs.SetString(VoiceSettings.VersionKey, "7");
            VoiceChat.Reload();
            Assert.AreEqual(MicMode.Off, VoiceChat.Mic, "an unknown save turns the microphone off");
            Assert.IsTrue(VoiceChat.HearTeammates);
            Assert.AreEqual(80, VoiceChat.Volume);
            PlayerPrefs.SetString(VoiceSettings.VersionKey, "1");
            PlayerPrefs.SetInt(VoiceSettings.MicKey, 2);
            VoiceChat.Reload();
            Assert.AreEqual(MicMode.Off, VoiceChat.Mic, "a wrongly typed value reads as Off");
            VoiceChat.ResetToDefaults();
            Assert.IsFalse(VoiceSettings.Keys.Any(PlayerPrefs.HasKey));
            Assert.AreEqual(MicMode.Off, VoiceChat.Mic);
        }

        [Test]
        public void RoutingMutesAndTheSpeakerReachTheTransport()
        {
            var fake = new FakeTransport();
            VoiceChat.Use(fake);
            Assert.IsTrue(fake.Receiving);
            Assert.AreEqual(.8f, fake.PlaybackVolume, 1e-5f);
            var room = VoiceRoom.Party(new[] { "me", "pal", "friend" });
            VoiceChat.Join(room, "me");
            Assert.AreSame(room, fake.Room);
            Assert.AreEqual("me", fake.RoomSelf);
            Assert.IsTrue(VoiceChat.Hears("pal"));
            Assert.IsFalse(VoiceChat.Hears("me"));
            VoiceChat.ToggleMuted("pal");
            Assert.IsTrue(fake.Muted.Contains("pal"));
            Assert.IsFalse(VoiceChat.Hears("pal"));
            VoiceChat.ToggleMuted("pal");
            Assert.IsFalse(fake.Muted.Contains("pal"));
            VoiceChat.ToggleHearing();
            Assert.IsFalse(fake.Receiving, "the speaker switch stops playback");
            Assert.IsFalse(VoiceChat.Hears("friend"));
            VoiceChat.ToggleHearing();
            VoiceChat.Volume = 30;
            Assert.AreEqual(.3f, fake.PlaybackVolume, 1e-5f);
            VoiceChat.SetMuted("friend", true);
            var second = new FakeTransport();
            VoiceChat.Use(second);
            Assert.IsTrue(second.Muted.Contains("friend"), "a new transport gets the saved mutes");
            Assert.AreSame(room, second.Room);
            VoiceChat.Leave();
            Assert.AreEqual(VoiceScope.Nobody, second.Room.Scope);
            Assert.IsFalse(VoiceChat.Hears("friend"));
        }

        [Test]
        public void SpeakingIndicatorsHoldBrieflyAndHideMutedPlayers()
        {
            var fake = new FakeTransport();
            VoiceChat.Use(fake);
            VoiceChat.Join(VoiceRoom.Party(new[] { "me", "pal", "friend" }), "me");
            int changes = 0;
            Action count = () => changes++;
            VoiceChat.Changed += count;
            try
            {
                VoiceChat.Tick(false, 10);
                fake.Speak("pal", true);
                Assert.IsTrue(VoiceChat.IsSpeaking("pal"));
                CollectionAssert.AreEqual(new[] { "pal" }, VoiceChat.Speaking);
                Assert.Greater(changes, 0);
                fake.Speak("pal", false);
                VoiceChat.Tick(false, 10.2);
                Assert.IsTrue(VoiceChat.IsSpeaking("pal"), "held briefly so it doesn't flicker");
                VoiceChat.Tick(false, 10 + SpeakingTracker.HoldSeconds + .01);
                Assert.IsFalse(VoiceChat.IsSpeaking("pal"));
                fake.Speak("friend", true);
                VoiceChat.SetMuted("friend", true);
                Assert.IsFalse(VoiceChat.IsSpeaking("friend"), "a muted player shows no speaking mark");
                CollectionAssert.IsEmpty(VoiceChat.Speaking);
                fake.Speak("stranger", true);
                Assert.IsFalse(VoiceChat.IsSpeaking("stranger"));
                int before = VoiceChat.Version;
                fake.Poke();
                Assert.Greater(VoiceChat.Version, before, "transport changes reach the UI");
            }
            finally { VoiceChat.Changed -= count; }
        }

        [Test]
        public void VoicePlaybackIgnoresGameVolumeAndPause()
        {
            float volume = AudioListener.volume;
            var source = VoicePlayback.Create(null, "Voice test source");
            try
            {
                Assert.IsTrue(source.ignoreListenerVolume, "game volume never changes voice");
                Assert.IsTrue(source.ignoreListenerPause, "pausing game sound never pauses voice");
                Assert.AreEqual(0f, source.spatialBlend);
                Assert.IsNull(source.outputAudioMixerGroup);
                Assert.IsFalse(source.playOnAwake);
                Assert.IsTrue(VoicePlayback.IsSeparate(source));
                Assert.AreEqual(VoiceRules.Gain(VoiceChat.Volume), source.volume, 1e-5f);
                AudioListener.volume = .1f;
                VoicePlayback.Configure(source);
                Assert.AreEqual(VoiceRules.Gain(VoiceChat.Volume), source.volume, 1e-5f, "voice has its own volume");
                source.ignoreListenerPause = false;
                Assert.IsFalse(VoicePlayback.IsSeparate(source));
                Assert.IsFalse(VoicePlayback.IsSeparate(null));
            }
            finally
            {
                AudioListener.volume = volume;
                Object.DestroyImmediate(source.gameObject);
            }
        }
    }
}
