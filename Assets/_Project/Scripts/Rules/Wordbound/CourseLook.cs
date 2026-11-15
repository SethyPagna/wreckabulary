using System;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class CourseLook
    {
        int leases;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public bool Held { get; private set; }
        public bool Dragging => leases > 0;
        public bool Looking => Held || leases > 0;
        public bool Orbiting => Yaw != 0f || Pitch != 0f;

        public void BeginLook() => leases++;

        public void EndLook() => leases = Math.Max(0, leases - 1);

        public void Hold(bool held) => Held = held;

        public void Orbit(float yawDegrees, float pitchDegrees, float steadyPitch, float minimumPitch, float maximumPitch)
        {
            if (!float.IsFinite(yawDegrees) || !float.IsFinite(pitchDegrees)) return;
            Yaw = FreeLook.Wrap(Yaw + yawDegrees);
            Pitch = FreeLook.ClampPitch(Pitch + pitchDegrees, steadyPitch, minimumPitch, maximumPitch);
        }

        public void Update(float seconds, float steadyPitch, float minimumPitch, float maximumPitch)
        {
            if (!Looking)
            {
                Yaw = FreeLook.Settle(Yaw, seconds);
                Pitch = FreeLook.Settle(Pitch, seconds);
            }
            Pitch = FreeLook.ClampPitch(Pitch, steadyPitch, minimumPitch, maximumPitch);
        }

        public void Recenter()
        {
            Yaw = Pitch = 0f;
            Held = false;
        }

        public void Clear()
        {
            Recenter();
            leases = 0;
        }
    }
}
