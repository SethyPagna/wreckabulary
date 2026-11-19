using System;
using System.Threading;

namespace Wreckabulary.Rules.Voice
{
    public sealed class VoiceReceiver
    {
        enum Phase { Idle, Prefill, Playing }

        public const int CooldownFrames = 2;
        const double StatsFrames = 150;

        readonly VoiceInbox inbox;
        readonly VoiceJitterBuffer buffer;
        readonly VoiceConcealer concealer;
        readonly float[] frame, recovered;
        readonly VoiceLinkStats[] published = new VoiceLinkStats[2];
        readonly double frameSeconds, idleAfter, dropMargin, insertMargin;
        readonly float quiet;
        Phase phase;
        int used, debt, stallRun, cooldown, publishIndex;
        bool continuous;
        float lastLevel;
        double missingRate, lateRate, delay;
        long expected, missing, recoveredFrames, concealed, comfort, underruns, dry, dropped, inserted, played;

        public VoiceReceiver(int sampleRate = VoiceFormat.DefaultRate, VoiceJitterOptions options = null, IVoiceFec fec = null)
        {
            options ??= new VoiceJitterOptions();
            SampleRate = VoiceFormat.ValidRate(sampleRate);
            FrameSamples = VoiceFormat.FrameSamples(SampleRate);
            frameSeconds = FrameSamples / (double)SampleRate;
            buffer = new VoiceJitterBuffer(SampleRate, options);
            inbox = new VoiceInbox(FrameSamples, Math.Max(16, options.Capacity));
            concealer = new VoiceConcealer(SampleRate);
            frame = new float[FrameSamples];
            recovered = new float[FrameSamples];
            idleAfter = Math.Max(frameSeconds, options.IdleAfterMs / 1000.0);
            dropMargin = frameSeconds * .75;
            insertMargin = frameSeconds * .75;
            quiet = Math.Max(0f, options.QuietRms);
            Fec = fec;
            used = FrameSamples;
            Publish();
        }

        public int SampleRate { get; }
        public int FrameSamples { get; }
        public IVoiceFec Fec { get; set; }
        public VoiceJitterBuffer Buffer => buffer;
        public VoiceFrameKind LastKind { get; private set; }
        public ushort LastSequence { get; private set; }
        public double LastDelaySeconds { get; private set; }
        public bool Playing => phase == Phase.Playing;
        public int InboxOverflow => inbox.Overflow;

        public VoiceLinkStats Stats => published[Volatile.Read(ref publishIndex)];

        public bool Push(ushort sequence, uint timestamp, float[] samples, int offset, VoiceFrameFlags flags, double arrival,
            byte[] fec = null, int fecLength = 0)
        {
            if (samples == null || offset < 0 || offset + FrameSamples > samples.Length) return false;
            return inbox.Write(sequence, timestamp, samples, offset, flags, arrival, fec, fecLength);
        }

        public void Reset()
        {
            inbox.Clear();
            buffer.Reset();
            concealer.Reset();
            phase = Phase.Idle;
            used = FrameSamples;
            debt = stallRun = cooldown = 0;
            continuous = false;
            lastLevel = 0f;
            missingRate = lateRate = delay = 0;
            expected = missing = recoveredFrames = concealed = comfort = underruns = dry = dropped = inserted = played = 0;
            LastKind = VoiceFrameKind.Silence;
            Publish();
        }

        public int Read(float[] output, int offset, int count, double now)
        {
            if (output == null || count <= 0) return 0;
            count = Math.Min(count, output.Length - offset);
            buffer.ChunkSeconds += (count / (double)SampleRate - buffer.ChunkSeconds) * .1;
            Drain();
            int done = 0;
            while (done < count)
            {
                if (used >= FrameSamples)
                {
                    Produce(now + done / (double)SampleRate);
                    used = 0;
                }
                int n = Math.Min(count - done, FrameSamples - used);
                Array.Copy(frame, used, output, offset + done, n);
                used += n;
                done += n;
            }
            return count;
        }

        void Drain()
        {
            VoicePacket packet;
            while ((packet = inbox.Peek()) != null)
            {
                var result = buffer.Insert(packet);
                inbox.Pop();
                if (result == VoiceInsert.Late)
                {
                    lateRate += (1 - lateRate) / StatsFrames;
                }
                else if (result == VoiceInsert.Resync)
                {
                    phase = Phase.Prefill;
                    buffer.Rewinding = true;
                    debt = stallRun = 0;
                }
            }
        }

        void Produce(double now)
        {
            if (cooldown > 0) cooldown--;
            for (int guard = 0; guard < 4; guard++)
            {
                if (phase == Phase.Idle)
                {
                    if (buffer.Count == 0) { Quiet(); Publish(); return; }
                    buffer.SeekNearest();
                    buffer.Rewinding = true;
                    phase = Phase.Prefill;
                }
                if (phase == Phase.Prefill)
                {
                    if (buffer.Count == 0) { Quiet(); Publish(); return; }
                    if (!buffer.Has(buffer.Next)) buffer.SeekNearest();
                    if (buffer.DelayAt(now) + 1e-6 < buffer.TargetSeconds) { Quiet(); Publish(); return; }
                    phase = Phase.Playing;
                    buffer.Rewinding = false;
                }
                if (phase == Phase.Playing)
                {
                    Step(now);
                    Publish();
                    return;
                }
            }
            Quiet();
            Publish();
        }

        void Step(double now)
        {
            ushort next = buffer.Next;
            if (buffer.Has(next))
            {
                Resume(buffer.FlagsOf(next));
                AdjustAndPlay(now);
                return;
            }
            if (TryRecover()) return;
            int gap = buffer.Distance();
            if (gap > 0)
            {
                if (debt > 0)
                {
                    int skip = Math.Min(gap, debt);
                    for (int i = 0; i < skip; i++) Advance(true);
                    debt -= skip;
                    if (buffer.Has(buffer.Next))
                    {
                        if (debt > 0) underruns++;
                        debt = stallRun = 0;
                        AdjustAndPlay(now);
                        return;
                    }
                    debt = stallRun = 0;
                    if (TryRecover()) return;
                }
                Advance(true);
                Conceal();
                return;
            }
            if (stallRun == 0) dry++;
            stallRun++;
            debt++;
            if (debt * frameSeconds > idleAfter)
            {
                debt = stallRun = 0;
                phase = Phase.Idle;
                Quiet();
                return;
            }
            Conceal();
        }

        bool TryRecover()
        {
            if (Fec == null || !buffer.Recover(Fec, recovered)) return false;
            Resume(VoiceFrameFlags.None);
            LastSequence = buffer.Next;
            buffer.Skip();
            expected++;
            missingRate += (0 - missingRate) / StatsFrames;
            lateRate += (0 - lateRate) / StatsFrames;
            recoveredFrames++;
            Array.Copy(recovered, 0, frame, 0, FrameSamples);
            if (!continuous || concealer.Active) concealer.BlendInto(frame, 0, FrameSamples);
            float level = VoiceFormat.Rms(frame, 0, FrameSamples);
            concealer.Floor(level);
            Emit(VoiceFrameKind.Recovered, VoiceFrameFlags.None, level);
            return true;
        }

        void Resume(VoiceFrameFlags flags)
        {
            if (debt > 0 && (flags & VoiceFrameFlags.Start) == 0) underruns++;
            debt = stallRun = 0;
        }

        void AdjustAndPlay(double now)
        {
            double at = buffer.DelayAt(now), target = buffer.TargetSeconds;
            if (at > buffer.MaxDelaySeconds)
            {
                while (at > buffer.MaxDelaySeconds && buffer.Has(VoiceFormat.Next(buffer.Next)))
                {
                    Advance(false);
                    dropped++;
                    continuous = false;
                    at = buffer.DelayAt(now);
                }
                cooldown = CooldownFrames;
            }
            else if (cooldown == 0)
            {
                bool calm = lastLevel < quiet && buffer.LevelOf(buffer.Next) < quiet;
                if (calm && at > target + dropMargin && buffer.Has(VoiceFormat.Next(buffer.Next)))
                {
                    Advance(false);
                    dropped++;
                    continuous = false;
                    cooldown = CooldownFrames;
                    at = buffer.DelayAt(now);
                }
                else if (calm && at < target - insertMargin && (buffer.FlagsOf(buffer.Next) & VoiceFrameFlags.Start) == 0)
                {
                    inserted++;
                    cooldown = CooldownFrames;
                    Conceal();
                    return;
                }
            }
            Play(at);
        }

        void Play(double at)
        {
            ushort sequence = buffer.Next;
            buffer.Take(frame, out var flags, out float level);
            expected++;
            played++;
            missingRate += (0 - missingRate) / StatsFrames;
            lateRate += (0 - lateRate) / StatsFrames;
            if (!continuous || concealer.Active) concealer.BlendInto(frame, 0, FrameSamples);
            concealer.Floor(level);
            LastSequence = sequence;
            LastDelaySeconds = at;
            delay = at;
            Emit(VoiceFrameKind.Data, flags, level);
            if ((flags & VoiceFrameFlags.End) != 0) phase = Phase.Idle;
        }

        void Emit(VoiceFrameKind kind, VoiceFrameFlags flags, float level)
        {
            concealer.Remember(frame, 0, FrameSamples);
            continuous = kind == VoiceFrameKind.Data || kind == VoiceFrameKind.Recovered;
            lastLevel = level;
            LastKind = kind;
        }

        void Advance(bool lost)
        {
            buffer.Skip();
            expected++;
            if (lost)
            {
                missing++;
                missingRate += (1 - missingRate) / StatsFrames;
            }
            else missingRate += (0 - missingRate) / StatsFrames;
            lateRate += (0 - lateRate) / StatsFrames;
        }

        void Conceal()
        {
            concealer.Generate(frame, 0, FrameSamples);
            bool comfortNow = concealer.Comfort;
            if (comfortNow) comfort++; else concealed++;
            Emit(comfortNow ? VoiceFrameKind.Comfort : VoiceFrameKind.Concealed, VoiceFrameFlags.None, VoiceFormat.Rms(frame, 0, FrameSamples));
        }

        void Quiet()
        {
            if (continuous || concealer.Active)
            {
                concealer.FadeOut();
                concealer.Generate(frame, 0, FrameSamples);
                if (concealer.Silent) concealer.Stop();
            }
            else Array.Clear(frame, 0, FrameSamples);
            concealer.Remember(frame, 0, FrameSamples);
            continuous = false;
            lastLevel = 0f;
            LastKind = VoiceFrameKind.Silence;
        }

        void Publish()
        {
            int index = 1 - publishIndex;
            published[index] = new VoiceLinkStats(
                (float)(Math.Max(0, missingRate - lateRate) * 100), (float)(lateRate * 100), (float)(buffer.JitterSeconds * 1000),
                (float)(delay * 1000), (float)(buffer.TargetSeconds * 1000), buffer.Received, missing, buffer.Late, buffer.Duplicates,
                recoveredFrames, concealed, comfort, underruns, dry, dropped, inserted, expected, played, phase == Phase.Playing);
            Volatile.Write(ref publishIndex, index);
        }
    }
}
