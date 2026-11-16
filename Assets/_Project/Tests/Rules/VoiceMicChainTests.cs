using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using Wreckabulary.Rules.Voice;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class VoiceMicChainTests
    {
        const int Rate = 48000;
        const double Seconds = 30, Settle = 3;
        static readonly int F = VoiceFormat.FrameSamples(Rate);

        static VoiceSpeech Talker => VoiceSignals.Speech(Rate, Seconds, 21, .1f, true);

        public sealed class Suppression
        {
            public string Name;
            public double SnrIn, SnrOut, SpeechBandDb, NoiseDb, NoiseInPausesDb;
            public double Improvement => SnrOut - SnrIn;

            public override string ToString() => string.Format(CultureInfo.InvariantCulture,
                "| {0,-22} | {1,6:F1} | {2,6:F1} | {3,6:F1} | {4,6:F2} | {5,6:F1} | {6,6:F1} |",
                Name, SnrIn, SnrOut, Improvement, SpeechBandDb, NoiseDb, NoiseInPausesDb);
        }

        public const string SuppressionHeader =
            "| speech + noise         | SNR in | SNR out | gain dB | speech band dB | noise dB | pauses dB |";

        static readonly Dictionary<string, Suppression> suppressions = new Dictionary<string, Suppression>();

        public static IEnumerable<(string name, float[] noise, bool stationary)> Noises()
        {
            yield return ("fan (pink, 14 dB SNR)", VoiceSignals.Fan(Rate, Seconds, .0178f, 3), true);
            yield return ("fan (pink, 4 dB SNR)", VoiceSignals.Fan(Rate, Seconds, .056f, 3), true);
            yield return ("mains hum 60 Hz", VoiceSignals.Hum(Rate, Seconds, .0178f), true);
            yield return ("low rumble", VoiceSignals.Rumble(Rate, Seconds, .0178f, 3), true);
            yield return ("keyboard clicks", VoiceSignals.Keyboard(Rate, Seconds, .3f, 5), false);
            yield return ("breathing", VoiceSignals.Breath(Rate, Seconds, .02f, 6), false);
        }

        public static Suppression Suppress(string name, float[] noise)
        {
            lock (suppressions)
                if (suppressions.TryGetValue(name, out var hit)) return hit;
            var speech = Talker;
            var s = speech.Samples;
            var plus = VoiceSignals.Mix(s, noise);
            var minus = VoiceSignals.Mix(s, VoiceSignals.Gain(noise, -1f));
            Clean(plus);
            Clean(minus);
            var speechOut = new float[s.Length];
            var noiseOut = new float[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                speechOut[i] = (plus[i] + minus[i]) * .5f;
                noiseOut[i] = (plus[i] - minus[i]) * .5f;
            }
            int lag = new VoiceNoiseSuppressor(Rate).Latency, from = (int)(Settle * Rate), end = s.Length - lag;
            double pauseIn = 0, pauseOut = 0;
            for (int k = from / F; k < end / F; k++)
            {
                if (speech.Active[k]) continue;
                pauseIn += VoiceSignals.Energy(noise, k * F, k * F + F);
                pauseOut += VoiceSignals.Energy(noiseOut, k * F + lag, k * F + lag + F);
            }
            var result = new Suppression
            {
                Name = name,
                SnrIn = VoiceSignals.Db(VoiceSignals.Energy(s, from, end) / VoiceSignals.Energy(noise, from, end)),
                SnrOut = VoiceSignals.Db(VoiceSignals.Energy(speechOut, from + lag) / VoiceSignals.Energy(noiseOut, from + lag)),
                SpeechBandDb = VoiceSignals.Db(Band(speechOut, from + lag, s.Length) / Band(s, from, end)),
                NoiseDb = VoiceSignals.Db(VoiceSignals.Energy(noiseOut, from + lag) / VoiceSignals.Energy(noise, from, end)),
                NoiseInPausesDb = VoiceSignals.Db(pauseOut / pauseIn),
            };
            lock (suppressions) suppressions[name] = result;
            return result;
        }

        static void Clean(float[] x)
        {
            var highPass = new VoiceHighPass(Rate);
            var noise = new VoiceNoiseSuppressor(Rate);
            for (int i = 0; i + F <= x.Length; i += F)
            {
                highPass.Process(x, i, F);
                noise.Process(x, i, F);
                noise.TakeBandSnrDb();
            }
        }

        static double Band(float[] x, int from, int to, double low = 300, double high = 3400)
        {
            const int n = 1024;
            var fft = new VoiceFft(n);
            var re = new double[n];
            var im = new double[n];
            int a = (int)(low * n / Rate), b = (int)(high * n / Rate);
            double sum = 0;
            for (int i = from; i + n <= to; i += n)
            {
                for (int k = 0; k < n; k++)
                {
                    re[k] = x[i + k] * Math.Sin(Math.PI * k / n);
                    im[k] = 0;
                }
                fft.Forward(re, im);
                for (int k = a; k <= b; k++) sum += re[k] * re[k] + im[k] * im[k];
            }
            return sum;
        }

        [Test]
        public void NoiseSuppressionGainsTenDecibelsOnStationaryNoiseAndKeepsTheVoice()
        {
            TestContext.WriteLine("Hagerman phase-inversion method: process speech+noise and speech-noise, then halve the sum and difference.");
            TestContext.WriteLine(SuppressionHeader);
            foreach (var (name, noise, stationary) in Noises())
            {
                var row = Suppress(name, noise);
                TestContext.WriteLine(row.ToString());
                Assert.AreEqual(0, row.SpeechBandDb, 3, name + ": speech-band energy stays within 3 dB");
                Assert.Greater(row.Improvement, 0, name + ": never makes the SNR worse");
                if (name.StartsWith("fan", StringComparison.Ordinal) || name.StartsWith("mains", StringComparison.Ordinal))
                    Assert.GreaterOrEqual(row.Improvement, 10, name + ": at least 10 dB on stationary noise");
                if (stationary) Assert.LessOrEqual(row.NoiseInPausesDb, -18, name + ": pauses fall to the floor");
            }
        }

        [Test]
        public void TheHighPassRemovesHumAndKeepsTheVoiceBand()
        {
            double Response(double hz)
            {
                var highPass = new VoiceHighPass(Rate);
                var x = new float[Rate];
                for (int i = 0; i < x.Length; i++) x[i] = (float)Math.Sin(2 * Math.PI * hz * i / Rate);
                highPass.Process(x, 0, x.Length);
                return VoiceSignals.Db(VoiceSignals.Energy(x, Rate / 2) / (Rate / 4.0));
            }
            Assert.Less(Response(50), -18);
            Assert.Less(Response(60), -12);
            Assert.AreEqual(-3, Response(90), .5, "the corner is 90 Hz");
            Assert.AreEqual(0, Response(300), .1);
            Assert.AreEqual(0, Response(3000), .05);
        }

        [Test]
        public void AutomaticGainBringsQuietAndLoudTalkersToTheTarget()
        {
            foreach (float level in new[] { .01f, .03f, .1f, .4f })
            {
                var talker = VoiceSignals.Speech(Rate, 15, 22, level, true);
                var x = VoiceSignals.Mix(talker.Samples, VoiceSignals.Fan(Rate, 15, level * .03f, 9));
                var chain = new VoiceMicChain(Rate);
                int lag = chain.Noise.Latency;
                double energy = 0;
                long count = 0;
                for (int k = 0; k + 1 < talker.Frames; k++)
                {
                    chain.Process(x, k * F, MicMode.OpenMic, MicState.Live);
                    int source = (k * F + F / 2 - lag) / F;
                    if (k * F < 5 * Rate || source < 0 || !talker.Active[source]) continue;
                    energy += VoiceSignals.Energy(x, k * F, k * F + F);
                    count += F;
                }
                double output = VoiceSignals.Db(energy / count);
                TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture, "AGC: talker at {0,6:F1} dBFS -> {1,6:F1} dBFS (target {2} dBFS)",
                    20 * Math.Log10(level), output, VoiceAutoGain.TargetDb));
                Assert.AreEqual(VoiceAutoGain.TargetDb, output, 3, "talker at " + level);
            }
        }

        [Test]
        public void TheGainHoldsWhileNobodyTalksSoNoiseIsNotPumpedUp()
        {
            var chain = new VoiceMicChain(Rate);
            var fan = VoiceSignals.Fan(Rate, 10, .001f, 4);
            for (int k = 0; k + 1 < fan.Length / F; k++) chain.Process(fan, k * F, MicMode.OpenMic, MicState.Live);
            Assert.AreEqual(0, chain.AutoGain.GainDb, 1e-9);
        }

        [Test]
        public void TheSoftLimiterNeverClipsAndLeavesNormalSpeechAlone()
        {
            var limiter = new VoiceLimiter(Rate);
            var loud = new float[Rate];
            for (int i = 0; i < loud.Length; i++) loud[i] = (float)(2.5 * Math.Sin(2 * Math.PI * 200 * i / Rate));
            limiter.Process(loud, 0, loud.Length);
            float peak = 0;
            foreach (float x in loud) peak = Math.Max(peak, Math.Abs(x));
            Assert.LessOrEqual(peak, VoiceLimiter.DefaultCeiling);
            Assert.Greater(peak, .8f);
            Assert.Less(VoiceSignals.MaxStep(loud), 2.5 * 2 * Math.PI * 200 / Rate, "the gain moves smoothly");

            float speechPeak = 0;
            foreach (float x in Talker.Samples) speechPeak = Math.Max(speechPeak, Math.Abs(x));
            var normal = VoiceSignals.Gain(Talker.Samples, VoiceLimiter.DefaultThreshold * .95f / speechPeak);
            var copy = (float[])normal.Clone();
            new VoiceLimiter(Rate).Process(copy, 0, copy.Length);
            CollectionAssert.AreEqual(normal, copy, "speech under the threshold passes untouched");
        }

        public sealed class Activity
        {
            public string Name;
            public double Detected, FalseTriggers, PauseSent;
            public int SpeechFrames, IdleFrames;
        }

        public static Activity Detect(string name, float[] x, VoiceSpeech truth)
        {
            var chain = new VoiceMicChain(Rate);
            int frames = x.Length / F - 1, lag = chain.Noise.Latency;
            var sent = new bool[frames];
            for (int k = 0; k < frames; k++)
            {
                int n = chain.Process(x, k * F, MicMode.OpenMic, MicState.Live);
                for (int i = 0; i < n; i++)
                {
                    int at = k - chain.Ago(i);
                    if (at >= 0) sent[at] = true;
                }
            }
            int hit = 0, speech = 0, idle = 0, falseHits = 0;
            for (int k = 50; k < frames; k++)
            {
                int source = (k * F + F / 2 - lag) / F;
                bool on = truth != null && source >= 0 && source < truth.Active.Length && truth.Active[source];
                if (on) { speech++; if (sent[k]) hit++; }
                else { idle++; if (sent[k]) falseHits++; }
            }
            return new Activity
            {
                Name = name,
                Detected = speech == 0 ? 0 : 100.0 * hit / speech,
                FalseTriggers = truth == null && idle > 0 ? 100.0 * falseHits / idle : 0,
                PauseSent = truth != null && idle > 0 ? 100.0 * falseHits / idle : 0,
                SpeechFrames = speech,
                IdleFrames = idle,
            };
        }

        [Test]
        public void VoiceActivitySendsSpeechAndIgnoresNoiseOnItsOwn()
        {
            const double length = 20;
            var speech = VoiceSignals.Speech(Rate, length, 21, .1f, true);
            var fan = VoiceSignals.Fan(Rate, length, .0178f, 3);
            var keys = VoiceSignals.Keyboard(Rate, length, .3f, 5);
            var hum = VoiceSignals.Hum(Rate, length, .0178f);
            var breath = VoiceSignals.Breath(Rate, length, .02f, 6);
            var quiet = VoiceSignals.Speech(Rate, length, 22, .01f, true);
            var cases = new[]
            {
                Detect("speech + fan", VoiceSignals.Mix(speech.Samples, fan), speech),
                Detect("speech + keyboard", VoiceSignals.Mix(speech.Samples, keys), speech),
                Detect("quiet talker + fan", VoiceSignals.Mix(quiet.Samples, VoiceSignals.Gain(fan, .1f)), quiet),
            };
            foreach (var row in cases)
            {
                TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "VAD {0,-20} detected {1,5:F1}% of {2} speech frames; pause frames sent {3,5:F1}% (300 ms hangover)",
                    row.Name, row.Detected, row.SpeechFrames, row.PauseSent));
                Assert.GreaterOrEqual(row.Detected, 95, row.Name);
            }
            var noiseOnly = new[]
            {
                Detect("fan", (float[])fan.Clone(), null),
                Detect("mains hum", (float[])hum.Clone(), null),
                Detect("keyboard clicks", (float[])keys.Clone(), null),
                Detect("breathing", (float[])breath.Clone(), null),
                Detect("all four", VoiceSignals.Mix(fan, hum, keys, breath), null),
            };
            foreach (var row in noiseOnly)
            {
                TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture, "VAD noise only: {0,-16} false triggers {1,5:F2}% of {2} frames",
                    row.Name, row.FalseTriggers, row.IdleFrames));
                Assert.LessOrEqual(row.FalseTriggers, 1, row.Name);
            }
        }

        [Test]
        public void PushToTalkKeepsSendingForAboutOneHundredFiftyMillisecondsAfterRelease()
        {
            var gate = new VoiceSendGate(Rate);
            Assert.AreEqual(VoiceGateStep.Closed, gate.Step(MicMode.PushToTalk, MicState.Ready, true));
            Assert.AreEqual(VoiceGateStep.Start, gate.Step(MicMode.PushToTalk, MicState.Live, false), "push to talk ignores voice activity");
            Assert.AreEqual(VoiceGateStep.Send, gate.Step(MicMode.PushToTalk, MicState.Live, false));
            int frames = 0;
            VoiceGateStep step;
            while ((step = gate.Step(MicMode.PushToTalk, MicState.Ready, false)) == VoiceGateStep.Send) frames++;
            Assert.AreEqual(VoiceGateStep.Last, step);
            double tail = (frames * F + gate.FadeLength) / (double)Rate;
            Assert.AreEqual(VoiceSendGate.PushToTalkTailSeconds, tail, 1e-9);
            Assert.AreEqual(VoiceGateStep.Closed, gate.Step(MicMode.PushToTalk, MicState.Ready, false));

            gate.Step(MicMode.PushToTalk, MicState.Live, false);
            Assert.AreEqual(VoiceGateStep.Cut, gate.Step(MicMode.PushToTalk, MicState.Muted, false), "muting cuts at once, with no tail");
            Assert.AreEqual(VoiceGateStep.Closed, gate.Step(MicMode.PushToTalk, MicState.Ready, false));
        }

        [Test]
        public void TheChainMarksTalkSpurtsAndFadesTheirEdges()
        {
            var chain = new VoiceMicChain(Rate);
            var speech = Talker;
            var x = VoiceSignals.Mix(speech.Samples, VoiceSignals.Fan(Rate, Seconds, .002f, 3));
            int starts = 0, ends = 0, preRolls = 0;
            bool open = false;
            for (int k = 0; k + 1 < speech.Frames; k++)
            {
                int n = chain.Process(x, k * F, MicMode.OpenMic, MicState.Live);
                if (n > 1) preRolls++;
                for (int i = 0; i < n; i++)
                {
                    var flags = chain.OutgoingFlags(i);
                    var frame = chain.Outgoing(i);
                    if ((flags & VoiceFrameFlags.Start) != 0)
                    {
                        Assert.IsFalse(open);
                        open = true;
                        starts++;
                        Assert.AreEqual(0f, frame[0], "a spurt fades in");
                    }
                    if ((flags & VoiceFrameFlags.End) != 0)
                    {
                        Assert.IsTrue(open);
                        open = false;
                        ends++;
                        Assert.AreEqual(0f, frame[F - 1], 1e-6f, "a spurt fades out");
                    }
                }
            }
            Assert.Greater(starts, 5);
            Assert.That(ends, Is.InRange(starts - 1, starts));
            Assert.AreEqual(starts, preRolls, "voice activity sends the 40 ms before it opened");
            Assert.AreEqual(0, chain.Process(new float[F], 0, MicMode.Off, MicState.Off), "Off sends nothing");
        }

        [Test]
        public void TheWholeLoopSurvivesABadNetworkWithoutClicks()
        {
            var speech = VoiceSignals.Speech(Rate, 20, 31, .05f, false);
            var input = VoiceSignals.Mix(speech.Samples, VoiceSignals.Fan(Rate, 20, .005f, 4));
            var loop = new VoiceLoop(Rate, VoiceNetworkProfile.Bad);
            int frames = input.Length / F;
            var output = new float[frames * F];
            for (int j = 0; j < frames; j++)
            {
                double now = (j + 1) * F / (double)Rate;
                loop.Capture(input, j * F, F, now);
                loop.Pump(now);
                loop.Read(output, j * F, F, now);
            }
            var stats = loop.Receiver.Stats;
            double limit = 1.5 * VoiceSignals.MaxStep(VoiceSignals.Gain(speech.Samples, 2.5f));
            Assert.AreEqual(0, VoiceSignals.Jumps(output, limit));
            Assert.Greater(stats.Played, frames / 2);
            Assert.Greater(stats.Concealed + stats.Comfort, 0);
            Assert.LessOrEqual(VoiceSignals.MaxStep(output), 1.0);
            Assert.AreEqual(VoiceLinkQuality.Poor, VoiceLinkGrades.Grade(stats), "the QA preset shows as a weak link");
        }
    }
}
