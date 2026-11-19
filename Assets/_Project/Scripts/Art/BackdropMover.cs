using UnityEngine;

namespace Wreckabulary.Art
{
    public sealed class BackdropMover : MonoBehaviour
    {
        public Vector3[] Path;
        public float Speed = 4f;
        public bool Loop = true;
        public float Phase;

        float[] marks;
        float length, travelled;

        internal float Cycle => Loop ? length : length * 2f;

        internal bool Prepare()
        {
            length = 0f;
            if (Path == null || Path.Length < 2) { marks = null; return false; }
            if (marks == null || marks.Length != Path.Length) marks = new float[Path.Length];
            marks[0] = 0f;
            for (int i = 1; i < Path.Length; i++) marks[i] = length += Vector3.Distance(Path[i - 1], Path[i]);
            if (length <= 0f) return false;
            travelled = Mathf.Repeat(Phase, 1f) * Cycle;
            Place();
            return true;
        }

        internal void Advance(float deltaTime)
        {
            if (marks == null || marks.Length != (Path?.Length ?? 0)) { if (!Prepare()) return; }
            if (length <= 0f) return;
            travelled = Mathf.Repeat(travelled + Mathf.Max(0f, Speed) * deltaTime, Cycle);
            Place();
        }

        void Place()
        {
            bool back = !Loop && travelled > length;
            float along = back ? Cycle - travelled : travelled;
            int segment = 1;
            while (segment < marks.Length - 1 && marks[segment] < along) segment++;
            float span = marks[segment] - marks[segment - 1];
            float t = span > 0f ? (along - marks[segment - 1]) / span : 0f;
            Vector3 from = Path[segment - 1], to = Path[segment];
            Vector3 heading = back ? from - to : to - from;
            heading.y = 0f;
            var rotation = heading.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(heading) : transform.localRotation;
            transform.SetLocalPositionAndRotation(Vector3.LerpUnclamped(from, to, t), rotation);
        }
    }
}
