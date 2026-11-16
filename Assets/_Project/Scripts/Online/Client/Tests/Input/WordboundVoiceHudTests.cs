using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Rules.Voice;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Voice;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundVoiceHudTests
    {
        const string Me = "voice-me", Mate = "voice-mate", Rival = "voice-rival", RivalMate = "voice-rival-mate";
        static readonly WordboundSeat[] TeamSeats =
            { new WordboundSeat(Me, 0, 0), new WordboundSeat(Mate, 0, 1), new WordboundSeat(Rival, 1, 0), new WordboundSeat(RivalMate, 1, 1) };
        static ModeDefinition TeamMode => ModeDefinition.For("TeamRace", RaceFormat.Duo);

        static WordboundRecipeGridFixture Hud(bool join = true)
        {
            var f = new WordboundRecipeGridFixture();
            f.Set("initialized", true);
            f.View.SetMenuOpen(false);
            if (join) f.Call("JoinVoice", TeamMode, TeamSeats, Me);
            f.Call("PlaceVoice", 1280f, 1f);
            return f;
        }

        static bool Shown(WordboundRecipeGridFixture f) =>
            (bool)typeof(WordboundMatchHud).GetProperty("VoiceShown", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f.Hud);

        static void Tap(WordboundRecipeGridFixture f, Rect rect, double now = 1, double held = .05)
        {
            Assert.IsTrue((bool)f.Call("PressVoice", rect.center, now), "Press lands on a voice control at " + rect);
            f.Call("LiftVoice", rect.center, now + held);
        }

        static Rect R(WordboundRecipeGridFixture f, string name) => (Rect)f.Get(name);
        static Rect Row(WordboundRecipeGridFixture f, int index) => ((List<Rect>)f.Get("voiceRows"))[index];
        static Rect Choice(WordboundRecipeGridFixture f, int index) => (Rect)f.Call("VoiceChoiceAt", index);

        [Test]
        public void LocalAndSoloCoursesShowNoVoiceControls()
        {
            using var voice = new VoiceTestScope();
            using var f = Hud(false);
            foreach (string mode in new[] { "TimeRun", "Freestyle", "FreeForAll" })
            {
                f.Call("JoinVoice", ModeDefinition.For(mode), new[] { new WordboundSeat(Me, 0, 0) }, Me);
                Assert.IsNull(f.Get("voiceRoom"), mode + ": a one-seat game never joins voice.");
                Assert.IsFalse(Shown(f));
                Assert.AreSame(VoiceRoom.Empty, VoiceChat.Room);
                Assert.AreSame(VoiceRoom.Empty, voice.Fake.Room);
            }
            Assert.IsFalse((bool)f.Call("PressVoice", R(f, "micRect").center, 1.0), "Hidden controls take no input.");
        }

        [Test]
        public void TeamCoursesJoinTheTeamRoomAndLeaveOnlyTheirOwn()
        {
            using var voice = new VoiceTestScope();
            using var f = Hud();
            var room = (VoiceRoom)f.Get("voiceRoom");
            Assert.IsNotNull(room);
            Assert.IsTrue(Shown(f));
            Assert.AreSame(room, VoiceChat.Room);
            Assert.AreSame(room, voice.Fake.Room);
            Assert.AreEqual(Me, voice.Fake.Self);
            CollectionAssert.AreEqual(new[] { Mate }, ((IReadOnlyList<VoiceMember>)f.Get("voiceMembers")).Select(m => m.PlayerId).ToArray(),
                "Only teammates are listed.");
            Assert.AreEqual(1, ((List<Rect>)f.Get("voiceRows")).Count);
            f.View.SetMenuOpen(true);
            Assert.IsFalse(Shown(f), "The menu covers the voice controls.");
            f.View.SetMenuOpen(false);
            f.Call("LeaveVoice");
            Assert.AreSame(VoiceRoom.Empty, VoiceChat.Room, "Leaving the match leaves its voice room.");
            Assert.IsFalse(Shown(f));

            f.Call("JoinVoice", TeamMode, TeamSeats, Me);
            var party = VoiceRoom.Party(new[] { Me, Mate });
            VoiceChat.Join(party, Me);
            f.Call("LeaveVoice");
            Assert.AreSame(party, VoiceChat.Room, "A HUD never leaves a room it did not join.");
        }

        [Test]
        public void MicAndSpeakerSitLeftOfTheEyeAtTheEyeSize()
        {
            using var voice = new VoiceTestScope();
            using var f = Hud();
            f.Call("PlaceEye", 1280f, 1f);
            Rect eye = R(f, "eyeRect"), eyeKey = R(f, "keyCapRect"), mic = R(f, "micRect"), speaker = R(f, "speakerRect"), micKey = R(f, "micKeyRect");
            Assert.AreEqual(eye.size, mic.size);
            Assert.AreEqual(eye.size, speaker.size);
            Assert.AreEqual(eye.y, mic.y); Assert.AreEqual(eye.y, speaker.y);
            Assert.Less(mic.xMax, eye.xMin); Assert.Less(speaker.xMax, mic.xMin);
            Assert.IsFalse(micKey.Overlaps(eyeKey), "The push-to-talk pill and the eye pill never touch.");
            Assert.AreEqual(mic.center.x, micKey.center.x, 1e-3f);
            Assert.Greater(Row(f, 0).yMin, micKey.yMax, "Teammates are listed under the buttons.");
        }

        [Test]
        public void ButtonFacesAndTapsFollowVoiceChatAndReachTheTransport()
        {
            using var voice = new VoiceTestScope();
            using var f = Hud();
            Rect mic = R(f, "micRect"), speaker = R(f, "speakerRect");
            Assert.AreEqual(MicMode.Off, VoiceChat.Mic, "The microphone starts off.");
            Assert.AreEqual(MicState.Off, VoiceChat.MicState);

            Tap(f, mic);
            Assert.IsTrue((bool)f.Get("voiceChoosing"), "With the mic off, a tap opens Off / Push to talk / Open.");
            Tap(f, Choice(f, 1));
            Assert.AreEqual(MicMode.PushToTalk, VoiceChat.Mic);
            Assert.IsFalse((bool)f.Get("voiceChoosing"));
            Assert.AreEqual(MicState.Ready, VoiceChat.MicState);
            Assert.IsFalse(voice.Fake.Sending, "Push to talk sends nothing until the key is held.");

            VoiceChat.SetTalkButton(true);
            Assert.AreEqual(MicState.Live, VoiceChat.MicState);
            Assert.IsTrue(voice.Fake.Sending);
            VoiceChat.SetTalkButton(false);
            Assert.IsFalse(voice.Fake.Sending);

            Tap(f, mic);
            Assert.IsTrue(VoiceChat.SelfMuted, "A tap mutes yourself.");
            Assert.AreEqual(MicState.Muted, VoiceChat.MicState);
            VoiceChat.SetTalkButton(true);
            Assert.IsFalse(voice.Fake.Sending, "Self-muted never sends, even while holding the key.");
            VoiceChat.SetTalkButton(false);
            Tap(f, mic);
            Assert.IsFalse(VoiceChat.SelfMuted);

            Assert.IsTrue((bool)f.Call("PressVoice", mic.center, 10.0));
            f.Call("LiftVoice", mic.center, 10.0 + VoiceButtons.LongPressSeconds + .1);
            Assert.IsTrue((bool)f.Get("voiceChoosing"), "A long press opens the mode choice.");
            Assert.IsFalse(VoiceChat.SelfMuted, "A long press is not a mute tap.");
            Tap(f, Choice(f, 2));
            Assert.AreEqual(MicMode.OpenMic, VoiceChat.Mic);
            Assert.AreEqual(MicState.Live, VoiceChat.MicState);
            Assert.IsTrue(voice.Fake.Sending, "Open mic sends while it is on.");

            Assert.IsTrue(VoiceChat.HearTeammates);
            Tap(f, speaker);
            Assert.IsFalse(VoiceChat.HearTeammates, "The speaker turns teammates off.");
            Assert.IsFalse(voice.Fake.Receiving);
            Tap(f, speaker);
            Assert.IsTrue(VoiceChat.HearTeammates);
            Assert.IsTrue(voice.Fake.Receiving);
            Assert.AreEqual("", f.Get("notice"), "Connected taps show no status line.");
        }

        [Test]
        public void RosterMutesArePerPlayer()
        {
            using var voice = new VoiceTestScope();
            using var f = Hud();
            Tap(f, Row(f, 0));
            Assert.IsTrue(VoiceChat.IsMuted(Mate));
            Assert.IsTrue(voice.Fake.Muted.Contains(Mate));
            Assert.IsFalse(VoiceChat.IsMuted(Rival));
            Assert.IsFalse(VoiceChat.IsMuted(RivalMate));
            Assert.AreEqual(VoiceIcons.SpeakerOff, f.Call("VoiceRowIcon", 0));
            Tap(f, Row(f, 0));
            Assert.IsFalse(VoiceChat.IsMuted(Mate));
            Assert.IsFalse(voice.Fake.Muted.Contains(Mate));
        }

        [Test]
        public void SpeakingMarksAppearAndClearAfterTheHold()
        {
            using var voice = new VoiceTestScope();
            using var f = Hud();
            VoiceChat.Tick(false, 100);
            Assert.AreEqual("", f.Call("VoiceRowIcon", 0));
            voice.Fake.Speak(Mate, true);
            Assert.AreEqual(VoiceIcons.Wave, f.Call("VoiceRowIcon", 0), "A teammate talking shows the sound-wave mark.");
            voice.Fake.Speak(Rival, true);
            Assert.IsFalse(VoiceChat.IsSpeaking(Rival), "The other team is never heard or shown.");
            voice.Fake.Speak(Mate, false);
            VoiceChat.Tick(false, 100 + SpeakingTracker.HoldSeconds * .5);
            Assert.AreEqual(VoiceIcons.Wave, f.Call("VoiceRowIcon", 0), "The mark holds briefly after they stop.");
            VoiceChat.Tick(false, 100 + SpeakingTracker.HoldSeconds + .05);
            Assert.AreEqual("", f.Call("VoiceRowIcon", 0), "Then it clears.");
            voice.Fake.Speak(Mate, true);
            VoiceChat.ToggleHearing();
            Assert.AreEqual("", f.Call("VoiceRowIcon", 0), "With the speaker off nobody shows as talking.");
        }

        [Test]
        public void NotConnectedButtonsStayVisibleDimmedAndExplainThemselves()
        {
            using var voice = new VoiceTestScope(false);
            using var f = Hud();
            Assert.IsFalse(VoiceChat.Available);
            Assert.IsTrue(Shown(f), "The buttons stay visible so the feature is findable.");
            Tap(f, R(f, "micRect"));
            Assert.AreEqual(NullVoiceTransport.NotConnected, f.Get("notice"));
            Assert.IsFalse(VoiceChat.SelfMuted, "Nothing pretends to mute.");
            f.Call("SetNotice", "");
            Tap(f, R(f, "speakerRect"));
            Assert.AreEqual(NullVoiceTransport.NotConnected, f.Get("notice"));
            Assert.IsTrue(VoiceChat.HearTeammates);
            f.Call("SetNotice", "");
            Tap(f, Row(f, 0));
            Assert.AreEqual(NullVoiceTransport.NotConnected, f.Get("notice"));
            Assert.IsFalse(VoiceChat.IsMuted(Mate));
        }

        [Test]
        public void VoiceControlsPaintEveryFaceWithoutErrors()
        {
            using var voice = new VoiceTestScope();
            using var f = Hud();
            PaintWindow window = null;
            try
            {
                window = ScriptableObject.CreateInstance<PaintWindow>();
                window.Fixture = f;
                window.position = new Rect(100, 100, 640, 480); window.ShowUtility();
                foreach (var mode in VoiceButtons.Modes)
                    foreach (bool muted in new[] { false, true })
                    {
                        VoiceChat.Mic = mode; VoiceChat.SelfMuted = muted;
                        f.Set("voiceChoosing", muted);
                        voice.Fake.Speak(Mate, !muted);
                        window.SendEvent(new Event { type = EventType.Layout });
                        window.SendEvent(new Event { type = EventType.Repaint });
                        Assert.IsNull(window.Failure, mode + (muted ? " muted" : ""));
                    }
                Assert.Greater(window.Repaints, 0);
            }
            finally { if (window) window.Close(); }
        }

        sealed class PaintWindow : EditorWindow
        {
            public WordboundRecipeGridFixture Fixture;
            public Exception Failure;
            public int Repaints;
            void OnGUI()
            {
                if (Fixture == null) return;
                try
                {
                    if (Event.current.type == EventType.Repaint) Repaints++;
                    var ui = (WordboundUiStyles)Fixture.Get("ui");
                    using (ui.Apply(Fixture.Theme)) Fixture.Call("DrawVoice", 640f);
                }
                catch (Exception error) { Failure = error; }
            }
        }
    }
}
