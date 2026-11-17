using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Photon.Realtime;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules.Voice;

namespace Wreckabulary.Online.Tests
{
    [TestFixture]
    public class PhotonVoiceTransportTests
    {
        const string FakeAppId = "3f2504e0-4f89-11d3-9a0c-0305e82c3301", Host = "Wreckabulary voice";
        readonly List<PhotonVoiceTransport> made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var transport in made) transport.Dispose();
            made.Clear();
        }

        PhotonVoiceTransport Make()
        {
            var transport = new PhotonVoiceTransport(new PhotonVoiceOptions { AppId = FakeAppId });
            made.Add(transport);
            return transport;
        }

        static VoiceRoom Pair(bool channel = true)
        {
            var room = VoiceRoom.Party(new[] { "me", "pal" });
            return channel ? room.On(new VoiceChannel("party:p1", "asia")) : room;
        }

        static bool HostExists => GameObject.Find(Host) != null;

        [Test]
        public void OnlyRealGuidsCountAsAppIds()
        {
            Assert.IsTrue(PhotonVoiceSetup.IsAppId(FakeAppId));
            Assert.IsTrue(PhotonVoiceSetup.IsAppId("  " + FakeAppId + " "));
            foreach (string bad in new[] { null, "", " ", "not-an-id", Guid.Empty.ToString() })
                Assert.IsFalse(PhotonVoiceSetup.IsAppId(bad), bad ?? "null");
            Assert.Throws<ArgumentNullException>(() => new PhotonVoiceTransport(null));
            Assert.Throws<ArgumentException>(() => new PhotonVoiceTransport(new PhotonVoiceOptions { AppId = "" }));
        }

        [Test]
        public void NothingGoesOnlineUntilThereIsSomeoneToTalkTo()
        {
            var transport = Make();
            Assert.AreEqual(PhotonVoiceTransport.Idle, transport.Status);
            transport.SetRoom(VoiceRoom.Party(new[] { "me" }).On(new VoiceChannel("party:p1", "asia")), "me");
            Assert.AreEqual(PhotonVoiceTransport.Idle, transport.Status, "alone in a party");
            transport.SetRoom(Pair(false), "me");
            Assert.AreEqual(PhotonVoiceTransport.NoChannel, transport.Status, "a room without a channel stays local");
            transport.SetRoom(VoiceRoom.Empty, "");
            Assert.AreEqual(PhotonVoiceTransport.Idle, transport.Status);
            Assert.IsFalse(HostExists, "no voice client is created");
            Assert.IsFalse(transport.Available);
            Assert.IsFalse(transport.Sending);
            Assert.IsNull(transport.RoomName);
            Assert.AreEqual(0f, transport.InputLevel);
            Assert.AreEqual(0, transport.FramesSent + transport.FramesReceived + transport.FramesLost + transport.FramesRecovered);
            Assert.AreEqual(-1, transport.LagMs("pal"));
        }

        [Test]
        public void SettingsAreClampedAndForgiving()
        {
            var transport = Make();
            transport.PlaybackVolume = float.NaN;
            Assert.AreEqual(0f, transport.PlaybackVolume);
            transport.PlaybackVolume = 2f;
            Assert.AreEqual(1f, transport.PlaybackVolume);
            transport.PlaybackVolume = -1f;
            Assert.AreEqual(0f, transport.PlaybackVolume);
            transport.PlaybackVolume = .4f;
            Assert.AreEqual(.4f, transport.PlaybackVolume, 1e-6f);
            Assert.DoesNotThrow(() => transport.SetMuted(null, true));
            transport.Device = null;
            Assert.AreEqual("", transport.Device);
            transport.SetReceiving(false);
            transport.SetMic(MicMode.PushToTalk, MicState.Ready);
            Assert.IsFalse(transport.Audible("pal"));
            Assert.IsNotNull(transport.Devices);
        }

        [Test]
        public void AWrongAppTypeSaysSoAndOtherRoomErrorsRetry()
        {
            var transport = Make();
            int changes = 0;
            transport.Changed += () => changes++;
            IMatchmakingCallbacks callbacks = transport;
            callbacks.OnJoinRoomFailed(ErrorCode.GameFull, "full");
            Assert.AreEqual(PhotonVoiceTransport.Retrying, transport.Status);
            callbacks.OnCreateRoomFailed(ErrorCode.PluginReportedError, "Unsupported Plugin: ");
            Assert.AreEqual(PhotonVoiceTransport.WrongApp, transport.Status);
            callbacks.OnJoinRoomFailed(ErrorCode.PluginMismatch, "");
            Assert.AreEqual(PhotonVoiceTransport.WrongApp, transport.Status);
            Assert.IsFalse(transport.Available);
            Assert.AreEqual(2, changes, "listeners hear each real change once");
        }

        [UnityTest]
        public IEnumerator AChannelRoomBuildsOneTunedVoiceClientAndDisposeRemovesIt()
        {
            var transport = Make();
            transport.SetMic(MicMode.OpenMic, MicState.Live);
            transport.SetRoom(Pair(), "me");
            var host = GameObject.Find(Host);
            Assert.IsNotNull(host);
            Assert.AreEqual(PhotonVoiceTransport.Connecting, transport.Status);
            var recorder = host.GetComponent<Recorder>();
            Assert.IsNotNull(host.GetComponent<WordboundVoiceConnection>());
            Assert.AreEqual(PhotonVoiceTransport.FecInterval, host.GetComponent<WordboundVoiceTuning>().Fec);
            Assert.AreEqual("me", recorder.UserData);
            Assert.AreEqual(Photon.Voice.OpusCodec.FrameDuration.Frame20ms, recorder.FrameDuration, "short frames keep delay low");
            Assert.AreEqual(POpusCodec.Enums.SamplingRate.Sampling48000, recorder.SamplingRate, "no resampling of a 48 kHz mic");
            Assert.IsTrue(recorder.VoiceDetection, "an open mic waits for speech");
            Assert.IsFalse(recorder.TransmitEnabled, "nothing is sent before the room is joined");
            Assert.IsFalse(recorder.RecordingEnabled, "the microphone stays closed before the room is joined");
            transport.SetMic(MicMode.PushToTalk, MicState.Ready);
            Assert.IsFalse(recorder.VoiceDetection, "push to talk sends everything while held");
            transport.SetRoom(Pair(), "me");
            Assert.AreEqual(1, Count(Host), "joining again reuses the client");
            transport.Dispose();
            transport.Dispose();
            yield return null;
            Assert.IsFalse(HostExists);
            transport.SetRoom(Pair(), "me");
            Assert.IsFalse(HostExists, "a disposed transport stays down");
        }

        static int Count(string name)
        {
            int count = 0;
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.name == name) count++;
            return count;
        }
    }
}
