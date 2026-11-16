using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules.Voice;
using Wreckabulary.Voice;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class VoiceLoopbackTests
    {
        sealed class ToneMicrophone : IMicrophoneSource
        {
            public const int Rate = 16000;
            public AudioClip Clip;
            public int Ends;
            float started;

            public IReadOnlyList<string> Devices => new[] { "Tone mic" };
            public bool Allowed => true;
            public void Ask() { }

            public AudioClip Start(string device, int seconds)
            {
                Clip = AudioClip.Create("Tone mic", Rate * seconds, 1, Rate, false);
                Clip.SetData(Enumerable.Range(0, Clip.samples).Select(i => .2f * Mathf.Sin(2 * Mathf.PI * 160 * i / Rate)).ToArray(), 0);
                started = Time.unscaledTime;
                return Clip;
            }

            public int Position(string device) => Clip ? (int)((Time.unscaledTime - started) * Rate) % Clip.samples : 0;
            public void End(string device) => Ends++;
        }

        readonly List<Object> made = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var thing in made) if (thing) Object.Destroy(thing);
            made.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator HearYourselfRunsThroughTheVoicePipelineAndCanSimulateABadConnection()
        {
            var host = new GameObject("Voice loopback host");
            made.Add(host);
            var test = host.AddComponent<LocalVoiceTest>();
            var microphone = new ToneMicrophone();
            test.Source = microphone;
            test.HearYourself = true;
            Assert.IsTrue(test.Begin());
            yield return new WaitForSecondsRealtime(.4f);
            Assert.IsTrue(test.Looping, "hear yourself plays the pipeline's output");
            Assert.IsNotNull(test.Loop);
            Assert.AreEqual(ToneMicrophone.Rate, test.Loop.SampleRate);
            Assert.Greater(test.Loop.Captured, 5, "the microphone is framed and cleaned");
            Assert.AreEqual(0, test.Loop.Profile.LossPercent, "a clean connection by default");
            Assert.IsTrue(VoicePlayback.IsSeparate(test.Playback));
            Assert.AreSame(test.Playback.clip, test.Playback.GetComponent<AudioSource>().clip);

            test.SimulateBadConnection = true;
            Assert.Greater(test.Loop.Profile.LossPercent, 0);
            Assert.Greater(test.Loop.Profile.JitterMs, 0);
            long sentBefore = test.Loop.Network.Sent;
            yield return new WaitForSecondsRealtime(.4f);
            Assert.Greater(test.Loop.Network.Sent, sentBefore, "frames keep flowing through the simulated network");
            Assert.IsTrue(test.Looping);

            test.HearYourself = false;
            Assert.IsNull(test.Loop);
            Assert.IsFalse(test.Looping);
            test.Stop();
            Assert.AreEqual(1, microphone.Ends);
        }
    }
}
