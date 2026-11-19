using System;

namespace Wreckabulary.Rules.Voice
{
    public sealed class VoiceLoop
    {
        readonly float[] pending;
        int pendingCount;
        ushort sequence;
        uint stamp;

        public VoiceLoop(int sampleRate, VoiceNetworkProfile network = null, VoiceJitterOptions jitter = null, IVoiceFec fec = null)
        {
            SampleRate = VoiceFormat.ValidRate(sampleRate);
            FrameSamples = VoiceFormat.FrameSamples(SampleRate);
            pending = new float[FrameSamples];
            Chain = new VoiceMicChain(SampleRate);
            Network = new VoiceNetworkSimulator(FrameSamples, network, 256, SampleRate);
            Receiver = new VoiceReceiver(SampleRate, jitter, fec);
        }

        public int SampleRate { get; }
        public int FrameSamples { get; }
        public VoiceMicChain Chain { get; }
        public VoiceNetworkSimulator Network { get; }
        public VoiceReceiver Receiver { get; }
        public MicMode Mode { get; set; } = MicMode.OpenMic;
        public MicState State { get; set; } = MicState.Live;
        public long Captured { get; private set; }
        public long Sent { get; private set; }

        public VoiceNetworkProfile Profile
        {
            get => Network.Profile;
            set => Network.Profile = value;
        }

        public void Reset()
        {
            pendingCount = 0;
            sequence = 0;
            stamp = 0;
            Captured = Sent = 0;
            Chain.Reset();
            Network.Clear();
            Receiver.Reset();
        }

        public void Capture(float[] samples, int offset, int count, double now)
        {
            if (samples == null) return;
            count = Math.Min(count, samples.Length - offset);
            for (int i = 0; i < count; i++)
            {
                pending[pendingCount++] = samples[offset + i];
                if (pendingCount < FrameSamples) continue;
                pendingCount = 0;
                Send(now - (count - 1 - i) / (double)SampleRate);
            }
        }

        public void Pump(double now) => Network.Deliver(now, Receiver);

        public int Read(float[] output, int offset, int count, double now) => Receiver.Read(output, offset, count, now);

        void Send(double at)
        {
            Captured++;
            int n = Chain.Process(pending, 0, Mode, State);
            for (int i = 0; i < n; i++)
            {
                uint at0 = stamp - (uint)(Chain.Ago(i) * FrameSamples);
                Network.Send(sequence, at0, Chain.Outgoing(i), 0, Chain.OutgoingFlags(i), at);
                sequence = VoiceFormat.Next(sequence);
                Sent++;
            }
            stamp += (uint)FrameSamples;
        }
    }
}
