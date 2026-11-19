using System;

namespace Wreckabulary.Rules.Voice
{
    public sealed class VoiceSendGate
    {
        public const double PushToTalkTailSeconds = .15, FadeInSeconds = .005;
        public const int PreRollFrames = 2;

        readonly int frameSamples, tailSamples, fadeIn;
        int tailLeft;

        public VoiceSendGate(int sampleRate)
        {
            int rate = VoiceFormat.ValidRate(sampleRate);
            frameSamples = VoiceFormat.FrameSamples(rate);
            tailSamples = (int)Math.Round(PushToTalkTailSeconds * rate);
            fadeIn = Math.Max(1, (int)(FadeInSeconds * rate));
        }

        public bool Open { get; private set; }
        public int TailSamples => tailSamples;
        public bool InTail => Open && tailLeft > 0 && tailLeft < tailSamples;

        public void Reset()
        {
            Open = false;
            tailLeft = 0;
        }

        public VoiceGateStep Step(MicMode mode, MicState state, bool voice)
        {
            bool live = state == MicState.Live;
            if (state == MicState.Off || state == MicState.Muted || mode == MicMode.Off)
            {
                bool was = Open;
                Reset();
                return was ? VoiceGateStep.Cut : VoiceGateStep.Closed;
            }
            if (mode == MicMode.PushToTalk)
            {
                if (live)
                {
                    tailLeft = tailSamples;
                    return Begin(false);
                }
                if (Open && tailLeft > 0)
                {
                    int left = tailLeft;
                    tailLeft = Math.Max(0, tailLeft - frameSamples);
                    if (left > frameSamples) return VoiceGateStep.Send;
                    Open = false;
                    tailLeft = 0;
                    FadeLength = left;
                    return VoiceGateStep.Last;
                }
                bool wasOpen = Open;
                Open = false;
                FadeLength = 0;
                return wasOpen ? VoiceGateStep.Last : VoiceGateStep.Closed;
            }
            if (live && voice) return Begin(true);
            if (!Open) return VoiceGateStep.Closed;
            Open = false;
            FadeLength = frameSamples;
            return VoiceGateStep.Last;
        }

        public int FadeLength { get; private set; }

        VoiceGateStep Begin(bool preRoll)
        {
            if (Open) return VoiceGateStep.Send;
            Open = true;
            return preRoll ? VoiceGateStep.StartWithPreRoll : VoiceGateStep.Start;
        }

        public void FadeInto(float[] frame, int offset)
        {
            for (int i = 0; i < fadeIn && i < frameSamples; i++) frame[offset + i] *= i / (float)fadeIn;
        }

        public void FadeOut(float[] frame, int offset, int length)
        {
            length = Math.Max(0, Math.Min(frameSamples, length));
            for (int i = 0; i < frameSamples; i++)
                frame[offset + i] *= i >= length ? 0f : 1f - (i + 1) / (float)Math.Max(1, length);
        }
    }

    public enum VoiceGateStep { Closed, Start, StartWithPreRoll, Send, Last, Cut }

    public sealed class VoiceMicChain
    {
        readonly float[][] ring;
        readonly VoiceFrameFlags[] outFlags;
        readonly int[] outSlots = new int[VoiceSendGate.PreRollFrames + 1];
        int head, filledRing, outCount;

        public VoiceMicChain(int sampleRate)
        {
            SampleRate = VoiceFormat.ValidRate(sampleRate);
            FrameSamples = VoiceFormat.FrameSamples(SampleRate);
            HighPass = new VoiceHighPass(SampleRate);
            Noise = new VoiceNoiseSuppressor(SampleRate);
            Activity = new VoiceActivity(SampleRate);
            AutoGain = new VoiceAutoGain(SampleRate);
            Limiter = new VoiceLimiter(SampleRate);
            Gate = new VoiceSendGate(SampleRate);
            ring = new float[VoiceSendGate.PreRollFrames + 1][];
            for (int i = 0; i < ring.Length; i++) ring[i] = new float[FrameSamples];
            outFlags = new VoiceFrameFlags[ring.Length];
        }

        public int SampleRate { get; }
        public int FrameSamples { get; }
        public VoiceHighPass HighPass { get; }
        public VoiceNoiseSuppressor Noise { get; }
        public VoiceActivity Activity { get; }
        public VoiceAutoGain AutoGain { get; }
        public VoiceLimiter Limiter { get; }
        public VoiceSendGate Gate { get; }
        public float Level { get; private set; }
        public bool Voice => Activity.Active;
        public int Count => outCount;

        public float[] Outgoing(int index) => ring[outSlots[index]];

        public VoiceFrameFlags OutgoingFlags(int index) => outFlags[index];

        public int Ago(int index) => outCount - 1 - index;

        public void Reset()
        {
            HighPass.Reset();
            Noise.Reset();
            Activity.Reset();
            AutoGain.Reset();
            Limiter.Reset();
            Gate.Reset();
            head = filledRing = outCount = 0;
            Level = 0f;
        }

        public void Clean(float[] frame, int offset)
        {
            HighPass.Process(frame, offset, FrameSamples);
            Noise.Process(frame, offset, FrameSamples);
            bool voice = Activity.Update(frame, offset, FrameSamples, Noise.TakeBandSnrDb());
            AutoGain.Process(frame, offset, FrameSamples, voice && Activity.Likely);
            Limiter.Process(frame, offset, FrameSamples);
            Level = VoiceFormat.Rms(frame, offset, FrameSamples);
        }

        public int Process(float[] frame, int offset, MicMode mode, MicState state)
        {
            Clean(frame, offset);
            int slot = head;
            Array.Copy(frame, offset, ring[slot], 0, FrameSamples);
            head = (head + 1) % ring.Length;
            if (filledRing < ring.Length) filledRing++;
            outCount = 0;
            var step = Gate.Step(mode, state, Activity.Active);
            switch (step)
            {
                case VoiceGateStep.Start:
                    Gate.FadeInto(ring[slot], 0);
                    Add(slot, VoiceFrameFlags.Start);
                    break;
                case VoiceGateStep.StartWithPreRoll:
                    int back = Math.Min(VoiceSendGate.PreRollFrames, filledRing - 1);
                    for (int i = back; i >= 1; i--)
                    {
                        int older = (slot - i + ring.Length) % ring.Length;
                        Add(older, i == back ? VoiceFrameFlags.Start : VoiceFrameFlags.None);
                    }
                    Add(slot, back == 0 ? VoiceFrameFlags.Start : VoiceFrameFlags.None);
                    Gate.FadeInto(ring[outSlots[0]], 0);
                    break;
                case VoiceGateStep.Send:
                    Add(slot, VoiceFrameFlags.None);
                    break;
                case VoiceGateStep.Last:
                    Gate.FadeOut(ring[slot], 0, Gate.FadeLength);
                    Add(slot, VoiceFrameFlags.End);
                    break;
            }
            return outCount;
        }

        void Add(int slot, VoiceFrameFlags flags)
        {
            outSlots[outCount] = slot;
            outFlags[outCount] = flags;
            outCount++;
        }
    }
}
