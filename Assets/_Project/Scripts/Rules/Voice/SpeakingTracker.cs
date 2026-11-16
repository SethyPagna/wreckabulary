using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Voice
{
    public sealed class SpeakingTracker
    {
        public const double HoldSeconds = .4;

        readonly double hold;
        readonly Dictionary<string, double> until = new(StringComparer.Ordinal);
        readonly HashSet<string> talking = new(StringComparer.Ordinal);
        readonly List<string> ended = new();

        public SpeakingTracker(double holdSeconds = HoldSeconds) => hold = Math.Max(0, holdSeconds);

        public bool Report(string playerId, bool speaking, double now)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            bool before = IsSpeaking(playerId, now);
            if (speaking)
            {
                talking.Add(playerId);
                until.Remove(playerId);
            }
            else if (talking.Remove(playerId)) until[playerId] = now + hold;
            bool expired = Tick(now);
            return expired || before != IsSpeaking(playerId, now);
        }

        public bool IsSpeaking(string playerId, double now) =>
            playerId != null && (talking.Contains(playerId) || (until.TryGetValue(playerId, out double end) && now < end));

        public IReadOnlyList<string> Speaking(double now) =>
            talking.Concat(until.Where(entry => now < entry.Value).Select(entry => entry.Key))
                .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();

        public bool Tick(double now)
        {
            if (until.Count == 0) return false;
            ended.Clear();
            foreach (var entry in until)
                if (now >= entry.Value) ended.Add(entry.Key);
            foreach (string id in ended) until.Remove(id);
            return ended.Count > 0;
        }

        public void Forget(string playerId)
        {
            if (playerId == null) return;
            talking.Remove(playerId);
            until.Remove(playerId);
        }

        public void Clear()
        {
            talking.Clear();
            until.Clear();
        }
    }
}
