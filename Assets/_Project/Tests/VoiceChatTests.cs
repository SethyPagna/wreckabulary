using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules.Voice;
using Wreckabulary.Voice;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class VoiceChatTests
    {
        sealed class FakeTransport : IVoiceTransport
        {
            public bool Available => true;
            public string Status => "Connected";
            public bool Sending { get; private set; }
            public void SetSending(bool on) => Sending = on;
            public void SetReceiving(bool on) { }
            public float PlaybackVolume { get; set; }
            public void SetMuted(string playerId, bool muted) { }
            public void SetRoom(VoiceRoom room, string self) { }
            public float InputLevel => 0f;
            public IReadOnlyList<string> Devices => Array.Empty<string>();
            public string Device { get; set; }
            public event Action<string, bool> SpeakingChanged { add { } remove { } }
            public event Action Changed { add { } remove { } }
        }

        sealed class FakeMicrophone : IMicrophoneSource
        {
            public const int Rate = 16000;
            public string[] List = { "Test mic" };
            public bool Allow = true, Asked;
            public int Starts, Ends;
            public AudioClip Clip;
            float started;

            public IReadOnlyList<string> Devices => List;
            public bool Allowed => Allow;
            public void Ask() => Asked = true;

            public AudioClip Start(string device, int seconds)
            {
                Starts++;
                Clip = AudioClip.Create("Fake mic", Rate * seconds, 1, Rate, false);
                Clip.SetData(Enumerable.Range(0, Clip.samples).Select(i => .5f * Mathf.Sin(i * .05f)).ToArray(), 0);
                started = Time.unscaledTime;
                return Clip;
            }

            public int Position(string device) => Clip ? (int)((Time.unscaledTime - started) * Rate) % Clip.samples : 0;
            public void End(string device) => Ends++;
        }

        string[] saved;
        bool[] had;
        bool hadOverrides;
        string overrides;
        Keyboard keyboard;
        Gamepad pad;
        InputSettings.EditorInputBehaviorInPlayMode route;
        InputSettings.BackgroundBehavior focus;
        float listener;
        readonly List<Object> made = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            had = VoiceSettings.Keys.Select(PlayerPrefs.HasKey).ToArray();
            saved = VoiceSettings.Keys.Select(key => PlayerPrefs.GetString(key, "")).ToArray();
            hadOverrides = PlayerPrefs.HasKey(KeyBindings.OverridesKey);
            overrides = PlayerPrefs.GetString(KeyBindings.OverridesKey, "");
            listener = AudioListener.volume;
            KeyBindings.Stop();
            DesktopBinding.Shared.Map.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(KeyBindings.OverridesKey);
            VoiceChat.Use(null);
            VoiceChat.Leave();
            VoiceChat.ResetToDefaults();
            DesktopBinding.Typing = false;
            route = InputSystem.settings.editorInputBehaviorInPlayMode;
            focus = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            pad = InputSystem.AddDevice<Gamepad>();
            _ = DesktopBinding.Shared;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var thing in made) if (thing) Object.Destroy(thing);
            made.Clear();
            KeyBindings.Stop();
            yield return TestScenes.Reset();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            InputSystem.settings.editorInputBehaviorInPlayMode = route;
            InputSystem.settings.backgroundBehavior = focus;
            DesktopBinding.Typing = false;
            AudioListener.volume = listener;
            VoiceChat.SetTalkButton(false);
            VoiceChat.Use(null);
            VoiceChat.Leave();
            for (int i = 0; i < VoiceSettings.Keys.Length; i++)
                if (had[i]) PlayerPrefs.SetString(VoiceSettings.Keys[i], saved[i]); else PlayerPrefs.DeleteKey(VoiceSettings.Keys[i]);
            if (hadOverrides) PlayerPrefs.SetString(KeyBindings.OverridesKey, overrides); else PlayerPrefs.DeleteKey(KeyBindings.OverridesKey);
            PlayerPrefs.Save();
            KeyBindings.Load(DesktopBinding.Shared.Map);
            VoiceChat.Reload();
        }

        IEnumerator Keys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return null;
            yield return null;
        }

        IEnumerator Pad(GamepadState state)
        {
            InputSystem.QueueStateEvent(pad, state);
            yield return null;
            yield return null;
        }

        LocalVoiceTest NewTest(FakeMicrophone microphone)
        {
            var host = new GameObject("Voice test host");
            made.Add(host);
            var test = host.AddComponent<LocalVoiceTest>();
            test.Source = microphone;
            return test;
        }

        [UnityTest]
        public IEnumerator PushToTalkSendsOnlyWhileTheKeyOrPadButtonIsHeld()
        {
            var fake = new FakeTransport();
            VoiceChat.Use(fake);
            VoiceChat.Mic = MicMode.PushToTalk;
            yield return Keys();
            Assert.IsFalse(fake.Sending);
            yield return Keys(Key.V);
            Assert.IsTrue(VoiceChat.TalkHeld, "V is the push-to-talk key");
            Assert.IsTrue(fake.Sending);
            yield return Keys();
            Assert.IsFalse(fake.Sending, "releasing V stops sending");
            yield return Pad(new GamepadState().WithButton(GamepadButton.DpadUp));
            Assert.IsTrue(fake.Sending, "D-pad up talks on a controller");
            yield return Pad(new GamepadState());
            Assert.IsFalse(fake.Sending);

            VoiceChat.SelfMuted = true;
            yield return Keys(Key.V);
            Assert.IsFalse(fake.Sending, "self-mute wins over the key");
            yield return Keys();
            VoiceChat.SelfMuted = false;

            DesktopBinding.Typing = true;
            yield return Keys(Key.V);
            Assert.IsFalse(fake.Sending, "typing a name never opens the mic");
            yield return Keys();
            DesktopBinding.Typing = false;

            VoiceChat.Mic = MicMode.Off;
            yield return Keys(Key.V);
            Assert.IsFalse(fake.Sending, "Off never sends");
            yield return Pad(new GamepadState().WithButton(GamepadButton.DpadUp));
            Assert.IsFalse(fake.Sending);
            yield return Pad(new GamepadState());
            yield return Keys();
        }

        [UnityTest]
        public IEnumerator PushToTalkIsARebindableRowAfterDrop()
        {
            var keys = DesktopBinding.Shared;
            var rows = KeyBindings.Rows(keys).ToList();
            int drop = rows.FindIndex(row => row.action.name == "Drop");
            Assert.AreEqual("Push to talk (hold)", rows[drop + 1].label);
            Assert.AreSame(keys.PushToTalk, rows[drop + 1].action);
            Assert.AreEqual("<Keyboard>/v", keys.PushToTalk.bindings[0].effectivePath);
            Assert.AreEqual("V", ControlHints.PushToTalkKey);
            Assert.AreEqual("V", VoiceChat.PushToTalkKey);
            Assert.AreEqual(ControlHints.PadPushToTalk, ControlHints.PushToTalk(new GamepadBinding(pad)));
            Assert.AreEqual("", ControlHints.PushToTalk(new KeyboardBinding(KeyboardBinding.Side.Left)), "couch halves have no voice key");
            KeyBindings.Assign(keys.PushToTalk, 0, "<Keyboard>/t");
            using (var fresh = new DesktopBinding())
            {
                Assert.AreEqual("<Keyboard>/t", fresh.PushToTalk.bindings[0].effectivePath);
                Assert.AreEqual("<Keyboard>/r", fresh.Drop.bindings[0].effectivePath);
            }
            Assert.AreEqual("T", ControlHints.PushToTalkKey);
            var fake = new FakeTransport();
            VoiceChat.Use(fake);
            VoiceChat.Mic = MicMode.PushToTalk;
            yield return Keys(Key.T);
            Assert.IsTrue(fake.Sending, "the rebound key talks");
            yield return Keys();
            Assert.IsFalse(keys.Map.bindings.Any(item => (item.effectivePath ?? "").StartsWith("<Gamepad>", StringComparison.Ordinal)),
                "the pad button stays out of the keyboard map");
        }

        [UnityTest]
        public IEnumerator TheLocalTestMetersAndPlaysBackThroughAVoiceOnlySource()
        {
            var microphone = new FakeMicrophone();
            var test = NewTest(microphone);
            test.HearYourself = true;
            Assert.IsTrue(test.Begin());
            Assert.IsTrue(test.Running);
            Assert.AreEqual(LocalVoiceTest.Listening, test.Status);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.Greater(test.Level, 0f, "the meter moves with the voice");
            Assert.IsTrue(test.Looping, "hear yourself plays the microphone back");
            Assert.IsTrue(VoicePlayback.IsSeparate(test.Playback), "the loopback ignores game volume and pause");
            Assert.AreEqual(VoiceRules.Gain(VoiceChat.Volume), test.Playback.volume, 1e-4f);
            AudioListener.volume = .1f;
            VoiceChat.Volume = 40;
            yield return null;
            Assert.AreEqual(.4f, test.Playback.volume, 1e-4f, "voice volume, not game volume");
            test.HearYourself = false;
            Assert.IsFalse(test.Looping);
            test.SendMessage("OnApplicationFocus", false);
            Assert.IsFalse(test.Running, "losing focus stops the test");
            Assert.AreEqual(1, microphone.Ends);
            Assert.AreEqual(0f, test.Level);
        }

        [UnityTest]
        public IEnumerator TheLocalTestStopsWhenItsPageCloses()
        {
            var microphone = new FakeMicrophone();
            var test = NewTest(microphone);
            var row = new GameObject("Voice test row");
            made.Add(row);
            VoiceTestView.Bind(row, test, null, null, null);
            Assert.IsTrue(test.Begin());
            yield return null;
            Assert.IsTrue(test.Running, "the row is open");
            row.SetActive(false);
            yield return null;
            Assert.IsFalse(test.Running, "closing the row stops the microphone");
            Assert.AreEqual(1, microphone.Ends);
            row.SetActive(true);
            Assert.IsTrue(test.Begin());
            test.gameObject.SetActive(false);
            Assert.IsFalse(test.Running, "closing the page stops the microphone");
            Assert.AreEqual(2, microphone.Ends);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NoMicrophoneOrNoPermissionIsSaidPlainly()
        {
            var none = new FakeMicrophone { List = Array.Empty<string>() };
            var test = NewTest(none);
            Assert.IsFalse(test.Begin());
            Assert.IsFalse(test.Running);
            Assert.AreEqual(LocalVoiceTest.NoDevice, test.Status);
            Assert.AreEqual(0, none.Starts);
            var denied = new FakeMicrophone { Allow = false };
            test.Source = denied;
            Assert.IsFalse(test.Begin());
            Assert.IsTrue(denied.Asked, "the player is asked first");
            Assert.IsTrue(test.Waiting);
            Assert.AreEqual(LocalVoiceTest.Asking, test.Status);
            yield return null;
            Assert.AreEqual(0, denied.Starts, "nothing opens without permission");
            denied.Allow = true;
            yield return null;
            Assert.IsTrue(test.Running, "it starts once allowed");
            test.Stop();
            Assert.IsFalse(test.Running);
        }

        [UnityTest]
        public IEnumerator TheRealMicrophoneWithoutDevicesSaysSo()
        {
            Assume.That(UnityMicrophone.Shared.Devices.Count, Is.EqualTo(0), "This machine has a microphone.");
            var host = new GameObject("Voice test host");
            made.Add(host);
            var test = host.AddComponent<LocalVoiceTest>();
            Assert.IsFalse(test.Begin());
            Assert.AreEqual(LocalVoiceTest.NoDevice, test.Status);
            CollectionAssert.IsEmpty(VoiceChat.Devices);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SettingsAudioHasTheVoiceChatGroup()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning,
                2f, "settings ready");
            var menu = LobbyMenu.Instance;
            menu.Open(LobbyMenu.Settings);
            var page = menu.Page<SettingsPage>();
            page.Root.anchorMin = page.Root.anchorMax = new Vector2(.5f, .5f);
            page.Root.sizeDelta = new Vector2(1600, 720);
            page.ShowTab("audio");
            yield return null;
            Canvas.ForceUpdateCanvases();
            Button Button(string name) => page.Root.GetComponentsInChildren<Button>().Single(button => button.name == name);
            TMP_Text Text(string name) => page.Root.GetComponentsInChildren<TMP_Text>().Single(text => text.name == name);
            var list = page.Root.GetComponentsInChildren<ScrollRect>().Single().content;
            var names = list.Cast<Transform>().Select(row => row.name).ToArray();
            CollectionAssert.AreEqual(SettingsPage.VoiceRows, names.Skip(names.Length - SettingsPage.VoiceRows.Length));
            Assert.AreEqual(NullVoiceTransport.NotConnected, Text("Voice status").text, "the honest status line");
            Assert.AreEqual(ControlHints.PushToTalkKey, Text("Push-to-talk key name").text);
            Assert.IsTrue(page.Root.GetComponentsInChildren<TMP_Text>().Any(text => text.text == VoiceChat.SpeakersNote + "."));
            foreach (string control in new[] { "Microphone off", "Microphone push", "Microphone open", "Change push-to-talk key", "Mic device less",
                "Mic device more", "Test microphone", "Hear yourself on", "Hear yourself off", "Voice volume less", "Voice volume more",
                "Hear teammates on", "Hear teammates off" })
                Assert.IsNotNull(Button(control), control);
            foreach (var text in list.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextTruncated, text.name + ": " + text.text);
            }

            Button("Microphone push").onClick.Invoke();
            yield return null;
            Assert.AreEqual(MicMode.PushToTalk, VoiceChat.Mic);
            Assert.AreEqual("push", PlayerPrefs.GetString(VoiceSettings.MicKey));
            Button("Voice volume less").onClick.Invoke();
            Assert.AreEqual(70, VoiceChat.Volume);
            Button("Hear teammates off").onClick.Invoke();
            Assert.IsFalse(VoiceChat.HearTeammates);

            var test = page.Root.GetComponent<LocalVoiceTest>();
            Assert.IsNotNull(test);
            var microphone = new FakeMicrophone();
            test.Source = microphone;
            Button("Test microphone").onClick.Invoke();
            yield return null;
            Assert.IsTrue(test.Running);
            Assert.AreEqual(LocalVoiceTest.Listening, Text("Mic test status").text);
            Assert.AreEqual(VoiceTestView.StopLabel, Button("Test microphone").GetComponentInChildren<TMP_Text>().text);
            Button("Hear yourself on").onClick.Invoke();
            yield return null;
            Assert.IsTrue(test.Running, "changing a voice setting keeps the test running");
            Assert.IsTrue(test.HearYourself);
            page.ShowTab("graphics");
            yield return null;
            yield return null;
            Assert.IsFalse(test.Running, "leaving the Audio tab stops the test");
            page.ShowTab("audio");
            yield return null;
            Button("Test microphone").onClick.Invoke();
            yield return null;
            Assert.IsTrue(test.Running);
            menu.Open(LobbyMenu.Home);
            yield return null;
            Assert.IsFalse(test.Running, "closing Settings stops the test");
            Assert.AreEqual(2, microphone.Ends);

            menu.Open(LobbyMenu.Settings);
            page.ShowTab("audio");
            yield return null;
            Button("Change push-to-talk key").onClick.Invoke();
            yield return null;
            Assert.IsTrue(page.Root.GetComponentsInChildren<Button>().Any(button => button.name == "Rebind Push to talk (hold)"),
                "Change opens the Controls tab with the push-to-talk row");
        }
    }
}
