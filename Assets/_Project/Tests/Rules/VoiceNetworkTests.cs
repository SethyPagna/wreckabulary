using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using Wreckabulary.Rules.Voice;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class VoiceNetworkTests
    {
        const int Rate = 48000;
        const double Seconds = 60, MaxDelayMs = 200;

        public sealed class Row
        {
            public string Name;
            public double UnderrunsPerMinute, MeanDelayMs, PeakDelayMs, RealPercent, ConcealedPercent, LatePercent, LossPercent, JitterMs;
            public int Jumps;
            public long Duplicates, Dropped, Inserted, Recovered;
            public VoiceLinkQuality Grade;
            public double OutputDb, SourceDb;

            public override string ToString() => string.Format(CultureInfo.InvariantCulture,
                "| {0,-22} | {1,5:F2} | {2,6:F1} | {3,6:F1} | {4,4} | {5,6:F1} | {6,5:F1} | {7,5:F1} | {8,5:F1} | {9,5:F1} | {10,-4} |",
                Name, UnderrunsPerMinute, MeanDelayMs, PeakDelayMs, Jumps, RealPercent, ConcealedPercent, LossPercent, LatePercent, JitterMs, Grade);
        }

        public const string Header =
            "| network                | und/m | mean ms | peak ms | jumps | real % | conc % | loss % | late % | jit ms | link |";

        static readonly Dictionary<string, Row> rows = new Dictionary<string, Row>();

        public static VoiceSpeech Source => VoiceSignals.Speech(Rate, Seconds, 11, .1f, false);

        public static double Threshold => 1.5 * VoiceSignals.MaxStep(Source.Samples);

        public static Row Measure(string name, VoiceNetworkProfile profile, IVoiceFec fec = null, Func<ushort, byte[], int> fecOf = null)
        {
            lock (rows)
                if (rows.TryGetValue(name, out var hit)) return hit;
            var speech = Source;
            int f = speech.FrameSamples, frames = speech.Frames;
            var receiver = new VoiceReceiver(Rate, fec: fec);
            var network = new VoiceNetworkSimulator(f, profile, 256, Rate);
            var output = new float[frames * f];
            var fecBytes = new byte[16];
            double frameSeconds = f / (double)Rate, sendStep = frameSeconds / (1 + profile.DriftPercent / 100);
            var sendTimes = new double[65536];
            ushort first = 65000;
            int sent = 0, delays = 0;
            double delaySum = 0, peak = 0;
            for (int j = 0; j < frames; j++)
            {
                double now = j * frameSeconds + .0005;
                while (sent * sendStep <= now)
                {
                    ushort sequence = (ushort)(first + sent);
                    sendTimes[sequence] = sent * sendStep;
                    int length = fecOf == null ? 0 : fecOf((ushort)(sequence - 1), fecBytes);
                    network.Send(sequence, (uint)(sent * f), speech.Samples, (sent % frames) * f, VoiceFrameFlags.None, sent * sendStep, fecBytes, length);
                    sent++;
                }
                network.Deliver(now, receiver);
                receiver.Read(output, j * f, f, now);
                if (receiver.LastKind != VoiceFrameKind.Data) continue;
                double delay = now - sendTimes[receiver.LastSequence] - profile.BaseLatencyMs / 1000;
                delaySum += delay;
                peak = Math.Max(peak, delay);
                delays++;
            }
            var stats = receiver.Stats;
            double minutes = frames * frameSeconds / 60;
            var row = new Row
            {
                Name = name,
                UnderrunsPerMinute = stats.Underruns / minutes,
                MeanDelayMs = 1000 * delaySum / Math.Max(1, delays),
                PeakDelayMs = 1000 * peak,
                Jumps = VoiceSignals.Jumps(output, Threshold),
                RealPercent = 100.0 * (stats.Played + stats.Recovered) / Math.Max(1, sent),
                ConcealedPercent = 100.0 * (stats.Concealed + stats.Comfort) / frames,
                LossPercent = 100.0 * Math.Max(0, stats.Missing - stats.Late) / Math.Max(1, stats.Expected),
                LatePercent = 100.0 * stats.Late / Math.Max(1, stats.Expected),
                JitterMs = stats.JitterMs,
                Duplicates = stats.Duplicates,
                Dropped = stats.Dropped,
                Inserted = stats.Inserted,
                Recovered = stats.Recovered,
                Grade = VoiceLinkGrades.Grade(stats),
                OutputDb = VoiceSignals.Db(VoiceSignals.Energy(output, Rate * 2) / (output.Length - Rate * 2)),
                SourceDb = VoiceSignals.Db(VoiceSignals.Energy(speech.Samples, Rate * 2, output.Length) / (output.Length - Rate * 2)),
            };
            lock (rows) rows[name] = row;
            return row;
        }

        public static IEnumerable<(string name, VoiceNetworkProfile profile)> Levels()
        {
            yield return ("clean", new VoiceNetworkProfile { Seed = 2 });
            yield return ("5% loss", new VoiceNetworkProfile { LossPercent = 5, Seed = 3 });
            yield return ("15% loss", new VoiceNetworkProfile { LossPercent = 15, Seed = 4 });
            yield return ("30% burst loss", new VoiceNetworkProfile { BurstLossPercent = 30, MeanBurstFrames = 4, Seed = 5 });
            yield return ("80 ms jitter", new VoiceNetworkProfile { JitterMs = 80, Seed = 6 });
            yield return ("5% loss + 80 ms jitter", new VoiceNetworkProfile { LossPercent = 5, JitterMs = 80, Seed = 7 });
            yield return ("5% reorder", new VoiceNetworkProfile { ReorderPercent = 5, Seed = 8 });
            yield return ("5% duplicates", new VoiceNetworkProfile { DuplicatePercent = 5, Seed = 9 });
            yield return ("+0.3% clock drift", new VoiceNetworkProfile { DriftPercent = .3, Seed = 10 });
            yield return ("-0.3% clock drift", new VoiceNetworkProfile { DriftPercent = -.3, Seed = 10 });
            yield return ("QA bad preset", VoiceNetworkProfile.Bad);
        }

        [Test]
        public void EveryNetworkLevelPlaysWithoutClicksInsideTheDelayCap()
        {
            TestContext.WriteLine("Speech-like source: harmonics 100-200 Hz with syllable envelopes, 60 s at 48 kHz; click threshold " +
                Threshold.ToString("F3", CultureInfo.InvariantCulture) + " (1.5x the source's largest step).");
            TestContext.WriteLine(Header);
            foreach (var (name, profile) in Levels())
            {
                var row = Measure(name, profile);
                TestContext.WriteLine(row.ToString());
                Assert.AreEqual(0, row.Jumps, name + ": no hard discontinuities");
                Assert.LessOrEqual(row.PeakDelayMs, MaxDelayMs, name + ": playout delay stays under the cap");
                Assert.Greater(row.RealPercent, 55, name + ": most of the talker's audio is heard");
            }
        }

        [Test]
        public void FivePercentLossWithEightyMillisecondJitterUnderrunsAtMostTwicePerMinute()
        {
            var row = Measure("5% loss + 80 ms jitter", new VoiceNetworkProfile { LossPercent = 5, JitterMs = 80, Seed = 7 });
            Assert.LessOrEqual(row.UnderrunsPerMinute, 2, row.ToString());
            Assert.Less(row.LatePercent, 1, "the buffer grew enough that frames are rarely late");
            Assert.GreaterOrEqual(row.MeanDelayMs, 80, "the playout delay grew to cover the jitter");
        }

        [Test]
        public void ACleanNetworkKeepsTheDelayLowAndTheLevelUnchanged()
        {
            var row = Measure("clean", new VoiceNetworkProfile { Seed = 2 });
            Assert.AreEqual(0, row.UnderrunsPerMinute);
            Assert.LessOrEqual(row.MeanDelayMs, 45, "the buffer shrinks to its 40 ms floor");
            Assert.AreEqual(row.SourceDb, row.OutputDb, .2, "the voice level is unchanged");
            Assert.AreEqual(VoiceLinkQuality.Good, row.Grade);
        }

        [Test]
        public void LossIsConcealedAndTheLinkGradeFollowsIt()
        {
            Assert.AreEqual(VoiceLinkQuality.Fair, Measure("5% loss", new VoiceNetworkProfile { LossPercent = 5, Seed = 3 }).Grade);
            var heavy = Measure("15% loss", new VoiceNetworkProfile { LossPercent = 15, Seed = 4 });
            Assert.AreEqual(VoiceLinkQuality.Poor, heavy.Grade);
            Assert.AreEqual(15, heavy.LossPercent, 2.5);
            Assert.AreEqual(VoiceLinkQuality.Poor, Measure("30% burst loss", new VoiceNetworkProfile { BurstLossPercent = 30, MeanBurstFrames = 4, Seed = 5 }).Grade);
            Assert.AreEqual(VoiceLinkQuality.Fair, Measure("80 ms jitter", new VoiceNetworkProfile { JitterMs = 80, Seed = 6 }).Grade);
        }

        [Test]
        public void ClockDriftIsCorrectedInQuietFrames()
        {
            var fast = Measure("+0.3% clock drift", new VoiceNetworkProfile { DriftPercent = .3, Seed = 10 });
            var slow = Measure("-0.3% clock drift", new VoiceNetworkProfile { DriftPercent = -.3, Seed = 10 });
            Assert.Greater(fast.Dropped, 3, "a fast sender's extra frames are dropped in silence");
            Assert.Greater(slow.Inserted, 3, "a slow sender's missing time is filled in silence");
            Assert.LessOrEqual(fast.PeakDelayMs, 80);
            Assert.AreEqual(0, fast.UnderrunsPerMinute + slow.UnderrunsPerMinute);
        }

        [Test]
        public void ReorderedAndDuplicatedFramesPlayOnceInOrder()
        {
            var reorder = Measure("5% reorder", new VoiceNetworkProfile { ReorderPercent = 5, Seed = 8 });
            Assert.AreEqual(0, reorder.LossPercent, .01);
            Assert.AreEqual(0, reorder.ConcealedPercent, .01);
            var duplicates = Measure("5% duplicates", new VoiceNetworkProfile { DuplicatePercent = 5, Seed = 9 });
            Assert.Greater(duplicates.Duplicates, 100);
            Assert.AreEqual(0, duplicates.ConcealedPercent, .01);
            Assert.AreEqual(duplicates.SourceDb, duplicates.OutputDb, .2);
        }

        [Test]
        public void TheFecHookRecoversLostFramesFromTheNextPacket()
        {
            var speech = Source;
            var decoder = new SourceFec(speech);
            var plain = Measure("15% loss", new VoiceNetworkProfile { LossPercent = 15, Seed = 4 });
            var fec = Measure("15% loss + FEC", new VoiceNetworkProfile { LossPercent = 15, Seed = 4 }, decoder, SourceFec.Encode);
            TestContext.WriteLine(Header);
            TestContext.WriteLine(plain.ToString());
            TestContext.WriteLine(fec.ToString());
            Assert.Greater(fec.Recovered, 200, "single losses come back from the next frame's redundancy");
            Assert.Greater(fec.RealPercent, plain.RealPercent + 8);
            Assert.AreEqual(0, fec.Jumps);
            Assert.Greater(decoder.Calls, 0);
        }

        sealed class SourceFec : IVoiceFec
        {
            readonly VoiceSpeech speech;
            public int Calls;

            public SourceFec(VoiceSpeech speech) => this.speech = speech;

            public static int Encode(ushort previous, byte[] into)
            {
                into[0] = (byte)(previous & 255);
                into[1] = (byte)(previous >> 8);
                return 2;
            }

            public bool Recover(ushort missing, byte[] next, int nextLength, float[] output, int offset, int count)
            {
                Calls++;
                if (nextLength < 2 || (ushort)(next[0] | next[1] << 8) != missing) return false;
                int index = ((ushort)(missing - 65000) % speech.Frames) * speech.FrameSamples;
                Array.Copy(speech.Samples, index, output, offset, count);
                return true;
            }
        }
    }
}
