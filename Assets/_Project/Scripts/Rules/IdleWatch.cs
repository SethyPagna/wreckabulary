using System.Collections.Generic;
using System.Text;

namespace Wreckabulary.Rules
{
    public sealed class IdleWatch
    {
        public const double DefaultSeconds = 30.0;
        public const float Stride = .5f;

        readonly struct Anchor
        {
            public readonly float X, Y, Z;
            public readonly double Since;
            public readonly bool Connected;

            public Anchor(float x, float y, float z, double since, bool connected)
            {
                X = x; Y = y; Z = z; Since = since; Connected = connected;
            }
        }

        readonly Dictionary<int, Anchor> anchors = new Dictionary<int, Anchor>();
        readonly double seconds;

        public IdleWatch(double seconds = DefaultSeconds) => this.seconds = seconds;

        public void Clear() => anchors.Clear();

        public void Observe(int player, float x, float y, float z, bool acting, bool connected, double now)
        {
            if (anchors.TryGetValue(player, out var last) && !acting && last.Connected == connected && Near(last, x, y, z)) return;
            anchors[player] = new Anchor(x, y, z, now, connected);
        }

        public bool IsIdle(int player, double now) =>
            anchors.TryGetValue(player, out var last) && (!last.Connected || now - last.Since >= seconds);

        static bool Near(Anchor a, float x, float y, float z)
        {
            float dx = x - a.X, dy = y - a.Y, dz = z - a.Z;
            return dx * dx + dy * dy + dz * dz < Stride * Stride;
        }

        public static string Notice(int idleMask)
        {
            if (idleMask == 0) return "";
            var names = new List<string>();
            for (int seat = 0; seat < 31; seat++)
                if ((idleMask & (1 << seat)) != 0) names.Add("P" + (seat + 1));
            var text = new StringBuilder();
            for (int i = 0; i < names.Count; i++)
                text.Append(i == 0 ? "" : i == names.Count - 1 ? " and " : ", ").Append(names[i]);
            return text.Append(names.Count == 1 ? " is idle" : " are idle").Append(" · the crew can leave without them").ToString();
        }
    }
}
