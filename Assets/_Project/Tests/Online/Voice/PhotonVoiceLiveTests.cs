using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Photon.Voice;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules.Voice;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Tests
{
    [TestFixture, Category("LivePhoton")]
    public class PhotonVoiceLiveTests
    {
        const string Region = "asia";
        const int HeardFrames = 25, SteadySeconds = 4;
        const float MinDelivered = .98f;
        readonly List<PhotonVoiceTransport> transports = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var transport in transports) transport.Dispose();
            transports.Clear();
        }

        PhotonVoiceTransport Join(PhotonVoiceOptions saved, VoiceRoom room, string self, double tone)
        {
            var options = new PhotonVoiceOptions { AppId = saved.AppId, CleanMicrophone = false };
            if (tone > 0) options.Input = () => new AudioUtil.ToneAudioReader<float>(null, tone, 48000, 1);
            var transport = new PhotonVoiceTransport(options);
            transports.Add(transport);
            transport.SetReceiving(true);
            transport.SetRoom(room, self);
            transport.SetSending(tone > 0);
            return transport;
        }

        static IEnumerator Until(Func<bool> done, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what + ".");
                yield return null;
            }
        }

        [UnityTest, Explicit("Needs the live Photon Voice service and a saved Voice App ID."), Timeout(240000)]
        public IEnumerator TeammatesHearEachOtherAndTheOtherTeamStaysPrivate()
        {
            var saved = PhotonVoiceSetup.Options();
            Assert.IsNotNull(saved, "No Photon Voice App ID is saved.");
            string key = "match:voice-proof-" + Guid.NewGuid().ToString("N");
            var seats = new[] { new WordboundSeat("annVoice", 0, 0), new WordboundSeat("beaVoice", 0, 1),
                new WordboundSeat("calVoice", 1, 0), new WordboundSeat("deeVoice", 1, 1) };
            var room = VoiceRoom.Match(ModeDefinition.For("TeamRace", RaceFormat.Duo), seats).On(new VoiceChannel(key, Region));
            float started = Time.realtimeSinceStartup;
            var ann = Join(saved, room, "annVoice", 440);
            var bea = Join(saved, room, "beaVoice", 0);
            var cal = Join(saved, room, "calVoice", 660);
            ann.SetMic(MicMode.PushToTalk, MicState.Live);
            cal.SetMic(MicMode.OpenMic, MicState.Live);
            var dee = Join(saved, room, "deeVoice", 0);
            var heardSpeaking = new HashSet<string>();
            bea.SpeakingChanged += (id, on) => { if (on) heardSpeaking.Add(id); };

            yield return Until(() => transports.All(t => t.Available), 60f, "all four players to join voice");
            float joined = Time.realtimeSinceStartup - started;
            Assert.AreEqual(ann.RoomName, bea.RoomName, "teammates share a voice room");
            Assert.AreEqual(cal.RoomName, dee.RoomName);
            Assert.AreNotEqual(ann.RoomName, cal.RoomName, "each team has its own voice room");
            Assert.AreEqual(Region, ann.Region);

            yield return Until(() => bea.FramesFrom("annVoice") >= HeardFrames && dee.FramesFrom("calVoice") >= HeardFrames, 30f,
                "each team to hear its speaker");
            float heard = Time.realtimeSinceStartup - started;
            yield return Until(() => heardSpeaking.Contains("annVoice"), 5f, "the speaking mark for a teammate");
            Assert.IsTrue(bea.Audible("annVoice"), "a teammate's voice plays");
            Assert.AreEqual(0, bea.FramesFrom("calVoice") + bea.FramesFrom("deeVoice"), "the other team never reaches us");
            Assert.AreEqual(0, ann.FramesFrom("calVoice") + dee.FramesFrom("annVoice") + cal.FramesFrom("annVoice"));
            Assert.IsTrue(ann.Sending && cal.Sending && !bea.Sending && !dee.Sending);

            bea.SetMuted("annVoice", true);
            Assert.IsFalse(bea.Audible("annVoice"), "muting a teammate silences them for us only");
            bea.SetMuted("annVoice", false);
            Assert.IsTrue(bea.Audible("annVoice"));
            bea.SetReceiving(false);
            Assert.IsFalse(bea.Audible("annVoice"), "turning the speaker off silences everyone");
            bea.SetReceiving(true);

            int sentMark = ann.FramesSent, gotMark = bea.FramesFrom("annVoice"), lostMark = bea.FramesLost, recoveredMark = bea.FramesRecovered, triedMark = bea.FecAttempts;
            int lag = -1;
            float steadyEnd = Time.realtimeSinceStartup + SteadySeconds;
            while (Time.realtimeSinceStartup < steadyEnd)
            {
                lag = Math.Max(lag, bea.LagMs("annVoice"));
                yield return null;
            }
            int sent = ann.FramesSent - sentMark;
            ann.SetMic(MicMode.PushToTalk, MicState.Ready);
            ann.SetSending(false);
            yield return new WaitForSecondsRealtime(1.5f);
            int got = bea.FramesFrom("annVoice") - gotMark, lost = bea.FramesLost - lostMark, recovered = bea.FramesRecovered - recoveredMark, tried = bea.FecAttempts - triedMark;
            yield return new WaitForSecondsRealtime(1.5f);
            float delivered = sent > 0 ? (float)got / sent : 0f;
            Write(new Dictionary<string, string>
            {
                ["utc"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                ["region"] = Region,
                ["players"] = "4",
                ["teams"] = "2",
                ["secondsToJoin"] = joined.ToString("0.0", CultureInfo.InvariantCulture),
                ["secondsToHear"] = heard.ToString("0.0", CultureInfo.InvariantCulture),
                ["framesSent"] = sent.ToString(CultureInfo.InvariantCulture),
                ["framesDelivered"] = got.ToString(CultureInfo.InvariantCulture),
                ["deliveredPercent"] = (100f * delivered).ToString("0.0", CultureInfo.InvariantCulture),
                ["framesLostByPhoton"] = lost.ToString(CultureInfo.InvariantCulture),
                ["framesRecoveredByFec"] = recovered.ToString(CultureInfo.InvariantCulture),
                ["fecRecoveryAttempts"] = tried.ToString(CultureInfo.InvariantCulture),
                ["fecIntervalApplied"] = ann.AppliedFec.ToString(CultureInfo.InvariantCulture),
                ["maxPlaybackDelayMs"] = lag >= 0 ? lag.ToString(CultureInfo.InvariantCulture) : "not recorded",
                ["otherTeamFrames"] = (bea.FramesFrom("calVoice") + bea.FramesFrom("deeVoice") + ann.FramesFrom("calVoice") + dee.FramesFrom("annVoice") + cal.FramesFrom("annVoice")).ToString(CultureInfo.InvariantCulture),
                ["result"] = "measured"
            });
            Assert.LessOrEqual(bea.FramesFrom("annVoice") - gotMark - got, 2, "releasing push to talk stops sending");
            Assert.IsFalse(ann.Sending);
            Assert.AreEqual(PhotonVoiceTransport.FecInterval, ann.AppliedFec, "error correction is on for the outgoing voice");
            Assert.GreaterOrEqual(sent, SteadySeconds * 1000 / PhotonVoiceTransport.FrameMs * 8 / 10, "a steady 20 ms stream was sent");
            Assert.GreaterOrEqual(delivered, MinDelivered, "every frame of the stream arrives");
            Assert.LessOrEqual(lost - recovered, Math.Max(1, sent / 50), "Photon reports no unrecovered loss");
            if (lag >= 0) Assert.LessOrEqual(lag, PhotonVoiceTransport.PlayDelayMaxMs, "playback delay stays under the cap");

        }

        static void Write(Dictionary<string, string> fields)
        {
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs", "wordbound");
            Directory.CreateDirectory(dir);
            string body = "{\n" + string.Join(",\n", fields.Select(f => "  \"" + f.Key + "\": \"" + f.Value + "\"")) + "\n}\n";
            File.WriteAllText(Path.Combine(dir, "photon-voice-proof.json"), body);
        }
    }
}
