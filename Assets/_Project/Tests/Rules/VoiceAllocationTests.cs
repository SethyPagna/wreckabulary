using System;
using NUnit.Framework;
using Wreckabulary.Rules.Voice;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class VoiceAllocationTests
    {
        const int Rate = 48000;

        [Test]
        public void TheWholeVoicePathAllocatesNothingPerFrame()
        {
            var speech = VoiceSignals.Speech(Rate, 20, 41, .05f, true);
            var input = VoiceSignals.Mix(speech.Samples, VoiceSignals.Fan(Rate, 20, .004f, 2));
            var loop = new VoiceLoop(Rate, VoiceNetworkProfile.Bad);
            int f = loop.FrameSamples, frames = input.Length / f;
            var output = new float[1024];
            int read = 0;
            void Run(int from, int to)
            {
                for (int j = from; j < to; j++)
                {
                    double now = (j + 1) * f / (double)Rate;
                    loop.Capture(input, j * f, f, now);
                    loop.Pump(now);
                    while (read < (j + 1) * f)
                    {
                        loop.Read(output, 0, output.Length, read / (double)Rate);
                        read += output.Length;
                    }
                    var stats = loop.Receiver.Stats;
                    if (stats.Received < 0) Assert.Fail();
                }
            }
            Run(0, 100);
            long before = Allocated();
            Run(100, frames);
            long after = Allocated();
            if (before < 0 || after < 0) Assert.Ignore("This runtime does not count allocations.");
            Assert.Greater(loop.Receiver.Stats.Played, 300);
            Assert.AreEqual(0L, after - before, "mic chain, network simulator and receiver reuse their buffers");
        }

        [Test]
        public void TheReceiverAloneAllocatesNothingUnderLossAndJitter()
        {
            var speech = VoiceNetworkTests.Source;
            int f = speech.FrameSamples;
            var receiver = new VoiceReceiver(Rate);
            var network = new VoiceNetworkSimulator(f, new VoiceNetworkProfile { LossPercent = 10, BurstLossPercent = 10, JitterMs = 60, ReorderPercent = 5, DuplicatePercent = 3 }, 256, Rate);
            var output = new float[f];
            void Run(int from, int to)
            {
                for (int j = from; j < to; j++)
                {
                    double now = j * .02;
                    network.Send((ushort)j, (uint)(j * f), speech.Samples, (j % speech.Frames) * f, VoiceFrameFlags.None, now);
                    network.Deliver(now, receiver);
                    receiver.Read(output, 0, f, now);
                }
            }
            Run(0, 100);
            long before = Allocated();
            Run(100, 2000);
            long after = Allocated();
            if (before < 0 || after < 0) Assert.Ignore("This runtime does not count allocations.");
            Assert.AreEqual(0L, after - before);
        }

        static long Allocated()
        {
            try { return GC.GetAllocatedBytesForCurrentThread(); }
            catch (NotSupportedException) { return -1; }
        }
    }
}
