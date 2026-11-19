using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules.Voice
{
    public readonly struct VoiceSendQuality : IEquatable<VoiceSendQuality>
    {
        public VoiceSendQuality(int step, int bitrate, int packetMs, bool fec)
        {
            Step = step;
            Bitrate = bitrate;
            PacketMs = packetMs;
            Fec = fec;
        }

        public int Step { get; }
        public int Bitrate { get; }
        public int PacketMs { get; }
        public bool Fec { get; }

        public bool Equals(VoiceSendQuality other) => Step == other.Step && Bitrate == other.Bitrate && PacketMs == other.PacketMs && Fec == other.Fec;
        public override bool Equals(object obj) => obj is VoiceSendQuality other && Equals(other);
        public override int GetHashCode() => Step * 397 ^ Bitrate ^ PacketMs * 31 ^ (Fec ? 1 : 0);
        public override string ToString() => Bitrate / 1000 + " kbps, " + PacketMs + " ms" + (Fec ? ", FEC" : "");
    }

    public sealed class VoiceQualityPolicy
    {
        public const double DownImpairment = 5, UpImpairment = 1, DownAfterSeconds = 2, UpAfterSeconds = 8;

        public static readonly IReadOnlyList<VoiceSendQuality> Steps = new[]
        {
            new VoiceSendQuality(0, 32000, 20, false),
            new VoiceSendQuality(1, 24000, 20, true),
            new VoiceSendQuality(2, 16000, 40, true),
            new VoiceSendQuality(3, 12000, 60, true),
        };

        int step;
        double badSince = double.NaN, cleanSince = double.NaN;

        public VoiceSendQuality Current => Steps[step];
        public int Step => step;

        public void Reset()
        {
            step = 0;
            badSince = cleanSince = double.NaN;
        }

        public bool Update(VoiceLinkStats report, double now)
        {
            if (!report.Known || double.IsNaN(now)) return false;
            double impairment = Math.Max(0, report.LossPercent) + Math.Max(0, report.LatePercent);
            if (impairment >= DownImpairment)
            {
                cleanSince = double.NaN;
                if (double.IsNaN(badSince)) badSince = now;
                if (now - badSince < DownAfterSeconds || step >= Steps.Count - 1) return false;
                step++;
                badSince = now;
                return true;
            }
            badSince = double.NaN;
            if (impairment >= UpImpairment)
            {
                cleanSince = double.NaN;
                return false;
            }
            if (double.IsNaN(cleanSince)) cleanSince = now;
            if (now - cleanSince < UpAfterSeconds || step == 0) return false;
            step--;
            cleanSince = now;
            return true;
        }
    }
}
