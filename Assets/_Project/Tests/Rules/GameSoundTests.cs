using System;
using System.Collections.Generic;
using NUnit.Framework;
using Wreckabulary.Rules.Sound;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class ToyCueSynthTests
    {
        static readonly Dictionary<string, (int length, double checksum)> Mirror = new Dictionary<string, (int, double)>
        {
            ["Pickup"] = (6962, -0.04126027551170386), ["Break"] = (9601, 4.702148583114586), ["Craft"] = (18241, -0.02319313441888582),
            ["Hit"] = (5761, -0.5908219581494141), ["Block"] = (4801, 2.1714424496106313), ["Jump"] = (6722, -0.23844425405891467),
            ["Dodge"] = (6722, -1.1933532641018125), ["Swing"] = (6241, -0.6222348017692296), ["Out"] = (17761, 0.031888355589209105),
            ["Protect"] = (12001, 0.010118218918482524), ["Boost"] = (10082, -0.1247214153138918), ["Blast"] = (17281, -7.978452273549878),
            ["Heal"] = (15361, 0.023822948101209107), ["Tick"] = (1682, 1.728549258338625),
        };

        static double Peak(float[] x)
        {
            double p = 0;
            foreach (float v in x) p = Math.Max(p, Math.Abs(v));
            return p;
        }

        static double Energy(float[] x, params Biquad[] chain)
        {
            double e = 0;
            foreach (float v in x)
            {
                double y = v;
                for (int i = 0; i < chain.Length; i++) y = chain[i].Process(y);
                e += y * y;
            }
            return e;
        }

        static Biquad Pass(double hz, int rate, bool high)
        {
            double w = 2 * Math.PI * hz / rate, c = Math.Cos(w), alpha = Math.Sin(w) / (2 * .7071067811865476), a0 = 1 + alpha;
            return high
                ? new Biquad((1 + c) / 2 / a0, -(1 + c) / a0, (1 + c) / 2 / a0, -2 * c / a0, (1 - alpha) / a0)
                : new Biquad((1 - c) / 2 / a0, (1 - c) / a0, (1 - c) / 2 / a0, -2 * c / a0, (1 - alpha) / a0);
        }

        [Test]
        public void TheMirrorInToolsRendersTheSameSamples()
        {
            foreach (string cue in ToyCueSynth.Cues)
            {
                var samples = ToyCueSynth.Render(cue);
                Assert.AreEqual(Mirror[cue].length, samples.Length, cue);
                Assert.AreEqual(Mirror[cue].checksum, ToyCueSynth.Checksum(samples), 1e-6, cue);
            }
            Assert.AreEqual(-0.03430696359127561, ToyCueSynth.Checksum(ToyCueSynth.Render("Pickup", 48000, 1.03)), 1e-6);
            Assert.AreEqual(1.5009415819413405, ToyCueSynth.Checksum(ToyCueSynth.Render("Tick", 44100)), 1e-6);
        }

        [Test]
        public void EveryCueIsShortQuietAndStartsAndEndsOnSilence()
        {
            foreach (string cue in ToyCueSynth.Cues)
            {
                var x = ToyCueSynth.Render(cue);
                Assert.LessOrEqual(x.Length, (int)(.4 * ToyCueSynth.Rate), cue);
                Assert.AreEqual(0f, x[0], cue);
                Assert.AreEqual(0f, x[x.Length - 1], cue);
                Assert.LessOrEqual(Peak(x), Math.Pow(10, -3.0 / 20), cue + " keeps 3 dB of headroom");
                double sum = 0;
                foreach (float v in x) { Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), cue); sum += v; }
                Assert.Less(Math.Abs(sum / x.Length), 1e-5, cue + " has no DC");
                double loudness = Loudness.MomentaryMax(x, ToyCueSynth.Rate);
                Assert.That(loudness, Is.InRange(-27.5, -13.0), cue + " momentary loudness");
            }
        }

        [Test]
        public void CuesAreBandLimitedAndNotHarsh()
        {
            int rate = ToyCueSynth.Rate;
            foreach (string cue in ToyCueSynth.Cues)
            {
                var x = ToyCueSynth.Render(cue);
                double total = Energy(x);
                double harsh = Energy(x, Pass(2000, rate, true), Pass(2000, rate, true), Pass(6000, rate, false), Pass(6000, rate, false));
                double air = Energy(x, Pass(8000, rate, true), Pass(8000, rate, true), Pass(8000, rate, true));
                Assert.Less(harsh / total, .03, cue + " 2-6 kHz share");
                Assert.Less(10 * Math.Log10(air / total + 1e-30), -40, cue + " energy above 8 kHz");
                Assert.Less(ToyCueSynth.HighestPartial(cue), ToyCueSynth.BandLimit * rate, cue);
                Assert.Less(ToyCueSynth.HighestPartial(cue, 16000), ToyCueSynth.BandLimit * 16000, cue + " even at a low rate");
            }
        }

        [Test]
        public void PitchVariantsStayWithinThreePercentAndKeepTheirLevel()
        {
            foreach (string cue in new[] { "Pickup", "Hit", "Dodge", "Tick" })
            {
                double basePeak = Peak(ToyCueSynth.Render(cue));
                foreach (double pitch in SoundJitter.Pitches)
                {
                    Assert.That(pitch, Is.InRange(1 - SoundJitter.PitchSpread, 1 + SoundJitter.PitchSpread));
                    Assert.AreEqual(basePeak, Peak(ToyCueSynth.Render(cue, ToyCueSynth.Rate, pitch)), 1e-6, cue);
                    Assert.AreEqual(ToyCueSynth.HighestPartial(cue) * pitch, ToyCueSynth.HighestPartial(cue, ToyCueSynth.Rate, pitch), 1e-6, cue);
                }
            }
        }

        [Test]
        public void RenderingIsDeterministicAndRejectsUnknownCues()
        {
            CollectionAssert.AreEqual(ToyCueSynth.Render("Break"), ToyCueSynth.Render("Break"));
            Assert.Throws<ArgumentException>(() => ToyCueSynth.Render("Kazoo"));
            var low = ToyCueSynth.Render("Craft", 44100);
            Assert.AreEqual(0f, low[0]);
            Assert.AreEqual(0f, low[low.Length - 1]);
        }

        [Test]
        public void ImpactsSitAboveFeedbackCuesAndWhooshesSitLowest()
        {
            double Effective(string cue)
            {
                double clip = Loudness.MomentaryMax(ToyCueSynth.Render(cue), ToyCueSynth.Rate);
                return clip + HouseCueMix.LevelDb(cue, clip);
            }
            double loudestFeedback = double.MinValue, quietestImpact = double.MaxValue;
            foreach (string cue in ToyCueSynth.Cues)
            {
                if (cue == "Tick") continue;
                double effective = Effective(cue);
                Assert.LessOrEqual(effective, HouseCueMix.TargetLufs(cue) + 1e-9, cue + " never exceeds its role level");
                Assert.LessOrEqual(HouseCueMix.LevelDb(cue, Loudness.MomentaryMax(ToyCueSynth.Render(cue), ToyCueSynth.Rate)), 0, cue);
                if (HouseCueMix.Role(cue) == CueRole.Feedback) loudestFeedback = Math.Max(loudestFeedback, effective);
                if (HouseCueMix.Role(cue) == CueRole.Impact) quietestImpact = Math.Min(quietestImpact, effective);
            }
            Assert.Less(loudestFeedback, quietestImpact, "UI cues stay below impacts");
            Assert.Less(Effective("Swing"), loudestFeedback, "the constant swing whoosh sits under the feedback cues");
            Assert.AreEqual(HouseCueMix.ImpactLufs, Effective("Hit"), .01);
            Assert.AreEqual(0, HouseCueMix.LevelDb("Pickup", double.NaN));
            Assert.AreEqual(1f, HouseCueMix.Scale("Hit", -40));
        }

        [Test]
        public void SpellSuccessIsARisingMajorArpeggio()
        {
            var parts = ToyCueSynth.Parts("Craft");
            Assert.Less(parts[0].From, parts[1].From);
            Assert.Less(parts[1].From, parts[2].From);
            Assert.AreEqual(5.0 / 4, parts[1].From / parts[0].From, .01);
            Assert.AreEqual(3.0 / 2, parts[2].From / parts[0].From, .01);
            var knockedOut = ToyCueSynth.Parts("Out");
            Assert.Greater(knockedOut[0].From, knockedOut[1].To, "knocked out falls");
        }
    }

    [TestFixture]
    public class LoudnessTests
    {
        static float[] Sine(double hz, double dbfs, double seconds, int rate, int channels = 1)
        {
            int frames = (int)(seconds * rate);
            double amp = Math.Pow(10, dbfs / 20);
            var x = new float[frames * channels];
            for (int i = 0; i < frames; i++)
                for (int c = 0; c < channels; c++) x[i * channels + c] = (float)(amp * Math.Sin(2 * Math.PI * hz * i / rate));
            return x;
        }

        [Test]
        public void KWeightingMatchesTheBs1770CoefficientsAt48k()
        {
            Loudness.KWeighting(48000, out var shelf, out var high);
            Assert.AreEqual(1.53512485958697, shelf.B0, 1e-9);
            Assert.AreEqual(-2.69169618940638, shelf.B1, 1e-9);
            Assert.AreEqual(1.19839281085285, shelf.B2, 1e-9);
            Assert.AreEqual(-1.69065929318241, shelf.A1, 1e-9);
            Assert.AreEqual(0.73248077421585, shelf.A2, 1e-9);
            Assert.AreEqual(-1.99004745483398, high.A1, 1e-9);
            Assert.AreEqual(0.99007225036621, high.A2, 1e-9);
        }

        [Test]
        public void A997HzToneAtMinus20DbfsReadsMinus23Lufs()
        {
            var mono = Loudness.Measure(Sine(997, -20, 4, 48000), 1, 48000);
            Assert.AreEqual(-23.0, mono.IntegratedLufs, .15);
            Assert.AreEqual(-23.0, mono.MomentaryMaxLufs, .15);
            Assert.AreEqual(-20.0, mono.PeakDbfs, .01);
            Assert.AreEqual(0, mono.ClippedSamples);
            Assert.Less(mono.LoudnessRangeLu, .5);
            var stereo = Loudness.Measure(Sine(997, -20, 4, 48000, 2), 2, 48000);
            Assert.AreEqual(mono.IntegratedLufs + 3.01, stereo.IntegratedLufs, .05, "two channels add their power");
        }

        [Test]
        public void AStepInLevelShowsAsLoudnessRange()
        {
            var quiet = Sine(997, -30, 10, 48000);
            var loud = Sine(997, -20, 10, 48000);
            var both = new float[quiet.Length + loud.Length];
            Array.Copy(quiet, both, quiet.Length);
            Array.Copy(loud, 0, both, quiet.Length, loud.Length);
            var report = Loudness.Measure(both, 1, 48000);
            Assert.That(report.LoudnessRangeLu, Is.InRange(8.0, 10.5));
            Assert.AreEqual(-23.0, report.ShortTermMaxLufs, .2);
            Assert.AreEqual(-33.0, report.ShortTermMinLufs, .2);
        }

        [Test]
        public void ClippedSamplesAreCounted()
        {
            var x = Sine(100, 0, .1, 48000);
            Assert.Greater(Loudness.Measure(x, 1, 48000).ClippedSamples, 0);
        }
    }

    [TestFixture]
    public class MasterDynamicsTests
    {
        static float[] Tone(double amp, int frames, int channels = 2, double hz = 440)
        {
            var x = new float[frames * channels];
            for (int i = 0; i < frames; i++)
                for (int c = 0; c < channels; c++) x[i * channels + c] = (float)(amp * Math.Sin(2 * Math.PI * hz * i / 48000));
            return x;
        }

        [Test]
        public void QuietSignalPassesUntouched()
        {
            var bus = new MasterDynamics(48000);
            var input = Tone(Math.Pow(10, -30 / 20.0), 4800);
            var output = (float[])input.Clone();
            bus.Process(output, 2);
            for (int i = 0; i < input.Length; i++) Assert.AreEqual(input[i], output[i], 1e-6);
            Assert.AreEqual(0, bus.LimitedFrames);
        }

        [Test]
        public void TheCompressorIsGentle()
        {
            Assert.AreEqual(0, MasterDynamics.GainComputer(-30), 1e-9);
            Assert.AreEqual(0, MasterDynamics.GainComputer(-16), 1e-9, "a busy mix near -23 LUFS passes untouched below -15 dBFS");
            Assert.AreEqual(-5, MasterDynamics.GainComputer(-2), 1e-9, "10 dB over at 2:1 loses 5 dB");
            Assert.That(MasterDynamics.GainComputer(-12), Is.InRange(-1.0, 0.0), "soft knee");
        }

        [Test]
        public void LoudPileUpsNeverPassMinusOneDbfs()
        {
            var bus = new MasterDynamics(48000);
            var random = new Random(7);
            var block = new float[1024 * 2];
            double ceiling = Math.Pow(10, MasterDynamics.CeilingDb / 20);
            for (int n = 0; n < 400; n++)
            {
                for (int i = 0; i < block.Length; i++) block[i] = (float)((random.NextDouble() * 2 - 1) * (n % 7) * .9);
                bus.Process(block, 2);
                foreach (float v in block)
                {
                    Assert.IsFalse(float.IsNaN(v));
                    Assert.LessOrEqual(Math.Abs(v), ceiling);
                }
            }
            Assert.LessOrEqual(bus.PeakOut, ceiling);
            Assert.Less(bus.DeepestReductionDb, -3);
        }

        [Test]
        public void NaNIsSilencedAndTheBusRecovers()
        {
            var bus = new MasterDynamics(48000);
            var block = Tone(.1, 512);
            block[10] = float.NaN;
            block[11] = float.PositiveInfinity;
            bus.Process(block, 2);
            foreach (float v in block) Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v));
            var next = Tone(.1, 512);
            bus.Process(next, 2);
            Assert.Greater(Math.Abs(next[201]), 0);
        }

        [Test]
        public void DuckingGlidesWithoutSteps()
        {
            var bus = new MasterDynamics(48000);
            var block = new float[48000 * 2];
            for (int i = 0; i < block.Length; i++) block[i] = .1f;
            bus.Duck = .5f;
            bus.Process(block, 2);
            Assert.AreEqual(.5, bus.CurrentDuck, .01);
            double worst = 0;
            for (int i = 2; i < block.Length; i += 2) worst = Math.Max(worst, Math.Abs(block[i] - block[i - 2]));
            Assert.Less(worst, .001, "no audible step while ducking");
            Assert.AreEqual(.05, block[block.Length - 1], .001);
            bus.Duck = 2f;
            Assert.AreEqual(1f, bus.Duck);
            bus.Duck = float.NaN;
            Assert.AreEqual(1f, bus.Duck);
        }

        [Test]
        public void ProcessingAllocatesNothing()
        {
            var bus = new MasterDynamics(48000);
            var block = Tone(.9, 1024);
            for (int i = 0; i < 64; i++) bus.Process(block, 2);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 512; i++) bus.Process(block, 2);
            Assert.Less(GC.GetAllocatedBytesForCurrentThread() - before, 64, "no allocation per audio block");
        }
    }

    [TestFixture]
    public class CueGateTests
    {
        [Test]
        public void TheSameSoundCoolsDownBriefly()
        {
            var gate = new CueGate(4, 6, 3, .06);
            Assert.IsTrue(gate.Admit(0, CuePriority.Normal, 0, .3));
            Assert.IsFalse(gate.Admit(0, CuePriority.Normal, .01, .3), "same frame");
            Assert.IsTrue(gate.Admit(1, CuePriority.Normal, .01, .3), "a different sound may start");
            Assert.IsTrue(gate.Admit(0, CuePriority.Normal, .07, .3));
            Assert.IsTrue(gate.Admit(0, CuePriority.Normal, .14, .3));
            Assert.IsFalse(gate.Admit(0, CuePriority.Normal, .21, .3), "three copies at once is the limit");
            Assert.IsTrue(gate.Admit(0, CuePriority.Normal, .6, .3), "old copies expire");
        }

        [Test]
        public void ImportantSoundsWinWhenTheMixIsFull()
        {
            var gate = new CueGate(16, 6, 3, .06, 1.5);
            for (int i = 0; i < 4; i++) Assert.IsTrue(gate.Admit(i, CuePriority.Low, 0, 1));
            Assert.IsFalse(gate.Admit(4, CuePriority.Low, 0, 1), "low sounds stop at 4");
            Assert.IsTrue(gate.Admit(5, CuePriority.Normal, 0, 1));
            Assert.IsFalse(gate.Admit(6, CuePriority.Normal, 0, 1), "normal sounds stop at 5");
            Assert.IsTrue(gate.Admit(7, CuePriority.High, 0, 1));
            Assert.IsFalse(gate.Admit(8, CuePriority.High, 0, 1), "nothing passes 6");
            Assert.AreEqual(6, gate.Active(.5));
            Assert.AreEqual(0, gate.Active(2));
            Assert.AreEqual(4, CueGate.CapFor(6, CuePriority.Low));
            Assert.AreEqual(6, CueGate.CapFor(6, CuePriority.High));
        }

        [Test]
        public void BusyTimeIsCappedSoLongTailsDoNotBlockTheMix()
        {
            var gate = new CueGate(2, 2, 2, 0, .25);
            Assert.IsTrue(gate.Admit(0, CuePriority.High, 0, 3));
            Assert.AreEqual(0, gate.ActiveFor(0, .26));
            Assert.IsFalse(gate.Admit(-1, CuePriority.High, 0, 1));
            Assert.IsFalse(gate.Admit(5, CuePriority.High, 0, 1));
        }

        [Test]
        public void JitterStaysWithinThreePercentAndOneAndAHalfDecibels()
        {
            var jitter = new SoundJitter(42);
            int previous = -1;
            double low = 9, high = 0;
            for (int i = 0; i < 2000; i++)
            {
                double gain = jitter.Gain();
                low = Math.Min(low, gain); high = Math.Max(high, gain);
                int v = jitter.Variant(SoundJitter.Pitches.Length, previous);
                Assert.AreNotEqual(previous, v);
                previous = v;
                double pitch = SoundJitter.PitchFor((uint)(i * 2654435761u));
                Assert.That(pitch, Is.InRange(.97, 1.03));
            }
            Assert.That(20 * Math.Log10(low), Is.InRange(-1.5, -1.3));
            Assert.That(20 * Math.Log10(high), Is.InRange(1.3, 1.5));
            var a = new SoundJitter(9);
            var b = new SoundJitter(9);
            Assert.AreEqual(a.Next01(), b.Next01());
        }

        [Test]
        public void CouchDistanceIsMeasuredFromWhereTheCameraLooks()
        {
            double d = CouchAttenuation.FocusDistance(0, 13.5, -12.5, 0, -.73, .68, 0, 0, 0);
            Assert.Less(d, .6, "the screen centre is near");
            double side = CouchAttenuation.FocusDistance(0, 13.5, -12.5, 0, -.73, .68, 10, 0, 0);
            Assert.AreEqual(10, side, .6);
            Assert.AreEqual(1, CouchAttenuation.Falloff(0));
            Assert.AreEqual(1, CouchAttenuation.Falloff(CouchAttenuation.NearMeters));
            Assert.AreEqual(CouchAttenuation.FarGain, CouchAttenuation.Falloff(100));
            double previous = 1;
            for (double m = 0; m < 30; m += .5)
            {
                double g = CouchAttenuation.Falloff(m);
                Assert.LessOrEqual(g, previous + 1e-12);
                Assert.GreaterOrEqual(g, CouchAttenuation.FarGain, "nothing on screen goes silent");
                previous = g;
            }
        }

        [Test]
        public void OverlapCountsAndShares()
        {
            var spans = new List<(double, double)> { (0, 1), (.5, 1.5), (.5, 1), (2, 3) };
            Assert.AreEqual(3, Overlap.Max(spans));
            Assert.AreEqual(.5 / 3, Overlap.ShareAbove(spans, 2, 0, 3), 1e-9);
            Assert.AreEqual(0, Overlap.ShareAbove(spans, 3, 0, 3));
            Assert.AreEqual(1, Overlap.Max(new List<(double, double)> { (0, 1), (1, 2) }), "touching spans do not overlap");
            var log = new MixLog(16);
            log.Add(0, 1, "a", true, 1);
            log.Add(.5, 1, "b", false, 1);
            Assert.AreEqual(1, log.Spans(true).Count);
            Assert.AreEqual(2, log.Spans(false).Count);
        }

        [Test]
        public void TheRecorderKeepsWhatFitsAndOneLayout()
        {
            var recorder = new MixRecorder(1024);
            recorder.Write(new float[600], 2);
            recorder.Write(new float[600], 2);
            Assert.AreEqual(1024, recorder.Written);
            Assert.IsTrue(recorder.Full);
            var other = new MixRecorder(2048);
            other.Write(new float[10], 2);
            other.Write(new float[10], 1);
            Assert.AreEqual(10, other.Written);
            Assert.AreEqual(2, other.Channels);
        }
    }
}
