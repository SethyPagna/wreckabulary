using System;
using NUnit.Framework;
using Wreckabulary.Rules.Voice;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class VoiceJitterBufferTests
    {
        const int Rate = 16000;
        static readonly int F = VoiceFormat.FrameSamples(Rate);
        const double Frame = .02;

        static float[] Tone(int frames, float amplitude = .3f, double hz = 220)
        {
            var x = new float[frames * F];
            for (int i = 0; i < x.Length; i++) x[i] = (float)(amplitude * Math.Sin(2 * Math.PI * hz * i / Rate));
            return x;
        }

        sealed class Talk
        {
            public readonly VoiceReceiver Receiver;
            public readonly float[] Source, Output;
            public double Now;
            int read;

            public Talk(int frames, VoiceJitterOptions options = null)
            {
                Receiver = new VoiceReceiver(Rate, options);
                Source = Tone(frames);
                Output = new float[frames * 3 * F];
            }

            public void Send(int frame, ushort sequence, double arrival, VoiceFrameFlags flags = VoiceFrameFlags.None, uint? stamp = null) =>
                Assert.IsTrue(Receiver.Push(sequence, stamp ?? (uint)(frame * F), Source, frame * F, flags, arrival));

            public VoiceFrameKind Pull()
            {
                Receiver.Read(Output, read, F, Now);
                read += F;
                Now += Frame;
                return Receiver.LastKind;
            }
        }

        [Test]
        public void PlayoutWaitsForTheStartDelayThenPlaysInOrder()
        {
            var talk = new Talk(20);
            for (int i = 0; i < 10; i++) talk.Send(i, (ushort)i, i * Frame);
            Assert.AreEqual(VoiceFrameKind.Silence, talk.Pull(), "0 ms");
            Assert.AreEqual(VoiceFrameKind.Silence, talk.Pull(), "20 ms");
            Assert.AreEqual(VoiceFrameKind.Silence, talk.Pull(), "40 ms");
            Assert.AreEqual(VoiceFrameKind.Data, talk.Pull(), "60 ms: the start delay");
            Assert.AreEqual(0, talk.Receiver.LastSequence);
            Assert.That(talk.Receiver.Stats.TargetMs, Is.InRange(55f, 60f), "the target starts at 60 ms and shrinks slowly");
            for (int i = 1; i < 10; i++)
            {
                Assert.AreEqual(VoiceFrameKind.Data, talk.Pull());
                Assert.AreEqual(i, talk.Receiver.LastSequence);
            }
        }

        [Test]
        public void OutOfOrderFramesAreReorderedAndDuplicatesDropped()
        {
            var talk = new Talk(12);
            int[] order = { 0, 2, 1, 3, 3, 5, 4, 1, 6, 7, 9, 8 };
            double arrival = 0;
            foreach (int i in order)
            {
                arrival = Math.Max(arrival, i * Frame) + .0001;
                talk.Send(i, (ushort)i, arrival);
            }
            talk.Now = .15;
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(VoiceFrameKind.Data, talk.Pull(), "frame " + i);
                Assert.AreEqual(i, talk.Receiver.LastSequence);
            }
            Assert.AreEqual(2, talk.Receiver.Stats.Duplicates);
            Assert.AreEqual(0, talk.Receiver.Stats.Missing);
        }

        [Test]
        public void AFrameThatArrivesAfterItsTurnIsDroppedAsLateAndTheDelayGrows()
        {
            var talk = new Talk(20);
            for (int i = 0; i < 6; i++) if (i != 3) talk.Send(i, (ushort)i, i * Frame);
            talk.Now = .06;
            for (int i = 0; i < 3; i++) talk.Pull();
            double before = talk.Receiver.Buffer.TargetSeconds;
            Assert.AreEqual(VoiceFrameKind.Concealed, talk.Pull(), "frame 3 is missing at its turn");
            talk.Send(3, 3, talk.Now + .15);
            talk.Pull();
            Assert.AreEqual(1, talk.Receiver.Stats.Late);
            Assert.Greater(talk.Receiver.Buffer.TargetSeconds, before + .1, "a late frame raises the target at once");
            Assert.AreEqual(4, talk.Receiver.LastSequence, "the late frame is never played out of order");
        }

        [Test]
        public void SequenceNumbersWrapWithoutAGlitch()
        {
            var talk = new Talk(12);
            for (int i = 0; i < 12; i++) talk.Send(i, (ushort)(65530 + i), i * Frame);
            talk.Now = .06;
            ushort expected = 65530;
            for (int i = 0; i < 12; i++)
            {
                Assert.AreEqual(VoiceFrameKind.Data, talk.Pull());
                Assert.AreEqual(expected, talk.Receiver.LastSequence);
                expected++;
            }
            Assert.AreEqual(0, talk.Receiver.Stats.Missing + talk.Receiver.Stats.Late + talk.Receiver.Stats.Duplicates);
        }

        [Test]
        public void ShortGapsRepeatThePitchAndLongGapsFadeToComfortNoiseWithoutClicks()
        {
            var talk = new Talk(40);
            for (int i = 0; i < 40; i++) if (i < 10 || i > 13) talk.Send(i, (ushort)i, i * Frame);
            talk.Now = .06;
            var kinds = new VoiceFrameKind[40];
            for (int i = 0; i < 40; i++) kinds[i] = talk.Pull();
            int at = Array.IndexOf(kinds, VoiceFrameKind.Concealed);
            Assert.Greater(at, 0);
            Assert.AreEqual(VoiceFrameKind.Concealed, kinds[at + 1], "the first 40 ms repeat the voice");
            Assert.AreEqual(VoiceFrameKind.Comfort, kinds[at + 3], "after 60 ms only quiet comfort noise is left");
            Assert.AreEqual(VoiceFrameKind.Data, kinds[at + 4]);
            int frameStart = at * F;
            float early = VoiceFormat.Rms(talk.Output, frameStart, F), late = VoiceFormat.Rms(talk.Output, frameStart + 3 * F, F);
            Assert.Greater(early, .15f, "a short gap keeps the voice going");
            Assert.Less(late, .003f, "a long gap is quiet");
            Assert.Greater(late, 0f, "but never dead silent");
            double limit = 1.5 * VoiceSignals.MaxStep(talk.Source);
            Assert.AreEqual(0, VoiceSignals.Jumps(talk.Output, limit), "no click at either edge of the gap");
        }

        [Test]
        public void TalkSpurtsEndWithoutUnderrunsAndRestartOnTheirOwnDelay()
        {
            var talk = new Talk(60);
            for (int i = 0; i < 10; i++)
                talk.Send(i, (ushort)i, i * Frame, i == 0 ? VoiceFrameFlags.Start : i == 9 ? VoiceFrameFlags.End : VoiceFrameFlags.None);
            for (int i = 0; i < 10; i++)
                talk.Send(30 + i, (ushort)(10 + i), (30 + i) * Frame + .03, i == 0 ? VoiceFrameFlags.Start : i == 9 ? VoiceFrameFlags.End : VoiceFrameFlags.None);
            int silent = 0, data = 0;
            for (int i = 0; i < 60; i++)
            {
                var kind = talk.Pull();
                if (kind == VoiceFrameKind.Data) data++;
                if (kind == VoiceFrameKind.Silence) silent++;
            }
            var stats = talk.Receiver.Stats;
            Assert.AreEqual(20, data);
            Assert.AreEqual(0, stats.Underruns, "the pause after an End frame is not an underrun");
            Assert.AreEqual(0, stats.Concealed + stats.Comfort, "nothing is concealed between talk spurts");
            Assert.AreEqual(VoiceFrameKind.Silence, talk.Receiver.LastKind);
            double limit = 1.5 * VoiceSignals.MaxStep(talk.Source);
            Assert.AreEqual(0, VoiceSignals.Jumps(talk.Output, limit), "spurt edges fade in and out");
        }

        [Test]
        public void ABufferThatRunsDryCountsOneUnderrunAndAddsDelay()
        {
            var talk = new Talk(30);
            for (int i = 0; i < 8; i++) talk.Send(i, (ushort)i, i * Frame);
            talk.Now = .06;
            for (int i = 0; i < 8; i++) talk.Pull();
            Assert.AreEqual(VoiceFrameKind.Concealed, talk.Pull());
            Assert.AreEqual(VoiceFrameKind.Concealed, talk.Pull());
            for (int i = 8; i < 16; i++) talk.Send(i, (ushort)i, talk.Now);
            Assert.AreEqual(VoiceFrameKind.Data, talk.Pull());
            Assert.AreEqual(8, talk.Receiver.LastSequence, "nothing was lost, so playback resumes where it stopped");
            var stats = talk.Receiver.Stats;
            Assert.AreEqual(1, stats.Underruns);
            Assert.AreEqual(1, stats.Dry);
            Assert.AreEqual(0, stats.Missing);
        }

        [Test]
        public void ADryBufferCausedByLossIsNotAnUnderrun()
        {
            var talk = new Talk(30);
            for (int i = 0; i < 8; i++) talk.Send(i, (ushort)i, i * Frame);
            talk.Now = .06;
            for (int i = 0; i < 8; i++) talk.Pull();
            talk.Pull();
            talk.Pull();
            for (int i = 12; i < 20; i++) talk.Send(i, (ushort)i, talk.Now);
            for (int i = 0; i < 3; i++) talk.Pull();
            Assert.AreEqual(12, talk.Receiver.LastSequence);
            var stats = talk.Receiver.Stats;
            Assert.AreEqual(0, stats.Underruns, "frames 8-11 were lost; the stall covered two of them");
            Assert.AreEqual(4, stats.Missing);
        }

        [Test]
        public void TheDelayIsCappedAtTwoHundredMilliseconds()
        {
            var talk = new Talk(60);
            for (int i = 0; i < 60; i++) talk.Send(i, (ushort)i, i < 30 ? i * Frame : .9);
            talk.Now = .06;
            double peak = 0;
            for (int i = 0; i < 70; i++)
            {
                talk.Pull();
                if (talk.Receiver.LastKind == VoiceFrameKind.Data) peak = Math.Max(peak, talk.Receiver.LastDelaySeconds);
            }
            Assert.LessOrEqual(peak, .2 + 1e-9);
            Assert.Greater(talk.Receiver.Stats.Dropped, 0, "frames are dropped to get back under the cap");
            Assert.LessOrEqual(talk.Receiver.Buffer.TargetSeconds, .18 + 1e-9);
        }

        [Test]
        public void ReadAcceptsAnyBlockSize()
        {
            var a = new Talk(30);
            var b = new Talk(30);
            for (int i = 0; i < 30; i++)
            {
                a.Send(i, (ushort)i, i * Frame);
                b.Send(i, (ushort)i, i * Frame);
            }
            var whole = new float[25 * F];
            var pieces = new float[25 * F];
            for (int i = 0; i < 25; i++) a.Receiver.Read(whole, i * F, F, .06 + i * Frame);
            int done = 0, size = 37;
            while (done < pieces.Length)
            {
                int n = Math.Min(size, pieces.Length - done);
                b.Receiver.Read(pieces, done, n, .06 + done / (double)Rate);
                done += n;
                size = size == 37 ? 512 : 37;
            }
            CollectionAssert.AreEqual(whole, pieces);
        }

        [Test]
        public void StatsReportLossLateJitterAndDelay()
        {
            var row = VoiceNetworkTests.Measure("5% loss + 80 ms jitter", new VoiceNetworkProfile { LossPercent = 5, JitterMs = 80, Seed = 7 });
            Assert.AreEqual(5, row.LossPercent, 1.5);
            Assert.AreEqual(80 / 3.0, row.JitterMs, 8, "RFC 3550 jitter of uniform 0-80 ms delay");
            Assert.Greater(row.MeanDelayMs, 80);
        }
    }
}
