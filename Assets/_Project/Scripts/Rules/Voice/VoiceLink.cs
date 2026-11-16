using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules.Voice
{
    public enum VoiceLinkQuality { Unknown, Poor, Fair, Good }

    public readonly struct VoiceLinkStats
    {
        public VoiceLinkStats(float lossPercent, float latePercent, float jitterMs, float delayMs, float targetMs, long received,
            long missing, long late, long duplicates, long recovered, long concealed, long comfort, long underruns, long dry,
            long dropped, long inserted, long expected, long played, bool active)
        {
            LossPercent = lossPercent;
            LatePercent = latePercent;
            JitterMs = jitterMs;
            DelayMs = delayMs;
            TargetMs = targetMs;
            Received = received;
            Missing = missing;
            Late = late;
            Duplicates = duplicates;
            Recovered = recovered;
            Concealed = concealed;
            Comfort = comfort;
            Underruns = underruns;
            Dry = dry;
            Dropped = dropped;
            Inserted = inserted;
            Expected = expected;
            Played = played;
            Active = active;
        }

        public float LossPercent { get; }
        public float LatePercent { get; }
        public float JitterMs { get; }
        public float DelayMs { get; }
        public float TargetMs { get; }
        public long Received { get; }
        public long Missing { get; }
        public long Late { get; }
        public long Duplicates { get; }
        public long Recovered { get; }
        public long Concealed { get; }
        public long Comfort { get; }
        public long Underruns { get; }
        public long Dry { get; }
        public long Dropped { get; }
        public long Inserted { get; }
        public long Expected { get; }
        public long Played { get; }
        public bool Active { get; }
        public bool Known => Received > 0;

        public static VoiceLinkStats Report(float lossPercent, float latePercent, float jitterMs, float delayMs) =>
            new VoiceLinkStats(lossPercent, latePercent, jitterMs, delayMs, delayMs, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, true);
    }

    public static class VoiceLinkGrades
    {
        public const float FairImpairment = 3f, PoorImpairment = 10f, FairJitterMs = 20f, PoorJitterMs = 60f;
        public const string Weak = "Weak connection";

        public static VoiceLinkQuality Grade(VoiceLinkStats stats)
        {
            if (!stats.Known) return VoiceLinkQuality.Unknown;
            float impairment = Clean(stats.LossPercent) + Clean(stats.LatePercent), jitter = Clean(stats.JitterMs);
            if (impairment >= PoorImpairment || jitter >= PoorJitterMs) return VoiceLinkQuality.Poor;
            if (impairment >= FairImpairment || jitter >= FairJitterMs) return VoiceLinkQuality.Fair;
            return VoiceLinkQuality.Good;
        }

        public static int Bars(VoiceLinkQuality quality) => quality switch
        {
            VoiceLinkQuality.Good => 3,
            VoiceLinkQuality.Fair => 2,
            VoiceLinkQuality.Poor => 1,
            _ => 0,
        };

        public static string Tip(VoiceLinkQuality quality) => quality == VoiceLinkQuality.Poor ? Weak : "";

        static float Clean(float value) => float.IsNaN(value) || value < 0f ? 0f : value;
    }

    public sealed class VoiceLinkMeter
    {
        public const double WorseAfterSeconds = 1, BetterAfterSeconds = 3, TipSeconds = 4;

        VoiceLinkQuality candidate;
        double candidateSince, poorSince = double.NegativeInfinity;

        public VoiceLinkQuality Quality { get; private set; }

        public bool Update(VoiceLinkStats stats, double now)
        {
            var grade = VoiceLinkGrades.Grade(stats);
            if (grade == VoiceLinkQuality.Unknown) return false;
            if (Quality == VoiceLinkQuality.Unknown) return Set(grade, now);
            if (grade == Quality)
            {
                candidate = grade;
                candidateSince = now;
                return false;
            }
            if (grade != candidate)
            {
                candidate = grade;
                candidateSince = now;
            }
            double wait = grade < Quality ? WorseAfterSeconds : BetterAfterSeconds;
            return now - candidateSince >= wait && Set(grade, now);
        }

        public bool TipShown(double now) => Quality == VoiceLinkQuality.Poor && now - poorSince < TipSeconds;

        public void Clear()
        {
            Quality = candidate = VoiceLinkQuality.Unknown;
            poorSince = double.NegativeInfinity;
        }

        bool Set(VoiceLinkQuality grade, double now)
        {
            if (grade == VoiceLinkQuality.Poor && Quality != VoiceLinkQuality.Poor) poorSince = now;
            Quality = candidate = grade;
            candidateSince = now;
            return true;
        }
    }

    public sealed class VoiceLinkBoard
    {
        readonly Dictionary<string, VoiceLinkMeter> meters = new(StringComparer.Ordinal);
        readonly List<string> gone = new();

        public bool Update(string playerId, VoiceLinkStats stats, double now)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            if (!meters.TryGetValue(playerId, out var meter)) meters[playerId] = meter = new VoiceLinkMeter();
            return meter.Update(stats, now);
        }

        public VoiceLinkQuality Quality(string playerId) =>
            playerId != null && meters.TryGetValue(playerId, out var meter) ? meter.Quality : VoiceLinkQuality.Unknown;

        public bool TipShown(string playerId, double now) =>
            playerId != null && meters.TryGetValue(playerId, out var meter) && meter.TipShown(now);

        public bool Keep(Func<string, bool> keep)
        {
            gone.Clear();
            foreach (var entry in meters)
                if (keep == null || !keep(entry.Key)) gone.Add(entry.Key);
            foreach (string id in gone) meters.Remove(id);
            return gone.Count > 0;
        }

        public void Clear() => meters.Clear();

        public int Count => meters.Count;
    }
}
