using System;

namespace Wreckabulary.Rules
{
    public static class FreeLook
    {
        public const float MinElevation = 30f, MaxElevation = 85f;
        public const float ReturnRate = 8f, Settled = .2f;
        public const float DegreesPerScreen = 360f;
        const double Degree = Math.PI / 180.0;

        public static readonly float BaseElevation = (float)(Math.Atan2(PartyCamera.Rise, PartyCamera.Back) / Degree);

        public static float Wrap(float degrees)
        {
            float wrapped = degrees % 360f;
            if (wrapped > 180f) wrapped -= 360f;
            else if (wrapped <= -180f) wrapped += 360f;
            return wrapped;
        }

        public static float ClampPitch(float offset, float baseAngle, float min, float max) =>
            Clamp(offset, min - baseAngle, max - baseAngle);

        public static float OverheadPitch(float offset) => ClampPitch(offset, BaseElevation, MinElevation, MaxElevation);

        public static float Settle(float degrees, float dt)
        {
            float eased = degrees * (float)Math.Exp(-ReturnRate * Math.Max(0f, dt));
            return Math.Abs(eased) < Settled ? 0f : eased;
        }

        public static float DragDegrees(float pixels, float screenHeight) => pixels * DegreesPerScreen / Math.Max(1f, screenHeight);

        public static float Elevation(float x, float y, float z) => (float)(Math.Atan2(y, Math.Sqrt(x * x + z * z)) / Degree);

        public static (float x, float y, float z) Orbit(float x, float y, float z, float yaw, float pitch)
        {
            double across = Math.Sqrt(x * x + z * z), length = Math.Sqrt(across * across + y * y);
            if (length < 1e-6) return (x, y, z);
            double from = Math.Atan2(y, across) / Degree;
            double elevation = Clamp(from + pitch, Math.Min(MinElevation, from), Math.Max(MaxElevation, from)) * Degree;
            double azimuth = Math.Atan2(x, z) + yaw * Degree;
            double flat = Math.Cos(elevation) * length;
            return ((float)(Math.Sin(azimuth) * flat), (float)(Math.Sin(elevation) * length), (float)(Math.Cos(azimuth) * flat));
        }

        public static float WidenedSize(float size, float aspect, float yaw)
        {
            if (yaw == 0f) return size;
            aspect = Math.Max(PartyCamera.MinAspect, aspect);
            double turn = yaw * Degree;
            float c = (float)Math.Abs(Math.Cos(turn)), s = (float)Math.Abs(Math.Sin(turn));
            float across = size * aspect, along = size / PartyCamera.DepthScale;
            float wide = c * across + s * along, deep = s * across + c * along;
            return Math.Max(size, Math.Max(wide / aspect, deep * PartyCamera.DepthScale));
        }

        static float Clamp(float value, float low, float high) => value < low ? low : value > high ? high : value;
        static double Clamp(double value, double low, double high) => value < low ? low : value > high ? high : value;
    }
}
