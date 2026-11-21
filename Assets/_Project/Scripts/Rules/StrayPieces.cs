using System;

namespace Wreckabulary.Rules
{
    public enum StrayPlace { InPlace, Outside, Below, High, Lost }

    [Flags]
    public enum StrayHold { None = 0, Broken = 1, Carried = 2, Thrown = 4, Goal = 8, Frozen = 16 }

    public struct StrayWatch
    {
        public float OutSeconds, RestSeconds;
    }

    public static class StrayPieces
    {
        public const float CheckSeconds = .5f;
        public const float ReturnAfterSeconds = 4f;
        public const float RestSeconds = 1f;
        public const float RestSpeed = .5f;
        public const float HomeRadius = .5f;
        public const float WallSlack = .1f;
        public const float FloorSlack = .3f;
        public const float StairSlack = .5f;
        public const float ReachHeight = 1.2f;
        public const float HomeRiseSlack = .3f;
        public const float LostDepth = 4f;
        public const float DropHeight = .15f;
        public const float MinSpotStep = .6f;
        public const float SpotGap = .1f;
        public const float SpotFit = .9f;
        public const float FloorClearance = .08f;
        public const float MinProbeHeight = .1f;
        public const int SpotRings = 2;
        public const int SpotsPerRing = 8;
        public const int SpotCount = 1 + SpotRings * SpotsPerRing;

        public static bool AtHome(float dx, float dy, float dz) =>
            dx * dx + dz * dz < HomeRadius * HomeRadius && Math.Abs(dy) < HomeRadius;

        public static RoomBox FloorUnder(HouseLayout layout, float x, float y, float z)
        {
            if (layout == null) return null;
            RoomBox under = null;
            bool inside = false;
            foreach (var room in layout.Rooms)
            {
                if (!room.Touches(x, z, WallSlack) || room.FloorY > y + FloorSlack) continue;
                bool within = room.Contains(x, z);
                bool higher = under != null && room.FloorY > under.FloorY + RoomBox.StoreyTolerance;
                bool level = under != null && Math.Abs(room.FloorY - under.FloorY) <= RoomBox.StoreyTolerance;
                if (under != null && !higher && !(level && within && !inside)) continue;
                under = room;
                inside = within;
            }
            return under;
        }

        public static float Rise(HouseLayout layout, float x, float y, float z, float bottom)
        {
            var under = FloorUnder(layout, x, y, z);
            return under == null ? 0f : Math.Max(0f, bottom - under.FloorY);
        }

        public static bool OnStairs(HouseLayout layout, float x, float y, float z)
        {
            if (layout == null) return false;
            foreach (var s in layout.Stairs)
            {
                RoomBox lower = layout.Room(s.Lower), upper = layout.Room(s.Upper);
                if (lower == null || upper == null || !s.Covers(x, z)) continue;
                if (y > lower.FloorY - StairSlack && y < upper.FloorY + StairSlack) return true;
            }
            return false;
        }

        public static StrayPlace Locate(HouseLayout layout, float homeRise, float x, float y, float z, float bottom)
        {
            if (layout == null || layout.Rooms.Count == 0) return StrayPlace.InPlace;
            float lowest = float.PositiveInfinity;
            bool overAFloor = false;
            foreach (var room in layout.Rooms)
            {
                if (room.FloorY < lowest) lowest = room.FloorY;
                if (room.Touches(x, z, WallSlack)) overAFloor = true;
            }
            if (y < lowest - LostDepth) return StrayPlace.Lost;
            var under = FloorUnder(layout, x, y, z);
            if (under == null) return overAFloor ? StrayPlace.Below : StrayPlace.Outside;
            if (OnStairs(layout, x, y, z)) return StrayPlace.InPlace;
            float limit = Math.Max(ReachHeight, homeRise + HomeRiseSlack);
            return bottom - under.FloorY > limit ? StrayPlace.High : StrayPlace.InPlace;
        }

        public static bool Returns(StrayPlace place, StrayHold hold, float outSeconds, float restSeconds, float returnAfter = ReturnAfterSeconds)
        {
            if (hold != StrayHold.None || place == StrayPlace.InPlace) return false;
            if (place == StrayPlace.Lost) return true;
            return outSeconds >= returnAfter && restSeconds >= Math.Min(RestSeconds, returnAfter);
        }

        public static bool Tick(ref StrayWatch watch, StrayPlace place, StrayHold hold, float speed, float seconds, float returnAfter = ReturnAfterSeconds)
        {
            if (hold != StrayHold.None || place == StrayPlace.InPlace)
            {
                watch = default;
                return false;
            }
            float step = Math.Max(0f, seconds);
            watch.OutSeconds += step;
            watch.RestSeconds = speed < RestSpeed ? watch.RestSeconds + step : 0f;
            return Returns(place, hold, watch.OutSeconds, watch.RestSeconds, returnAfter);
        }

        public static float SpotStep(float radius) => Math.Max(MinSpotStep, Math.Max(0f, radius) * 2f + SpotGap);

        public static bool TrySpot(RoomBox room, float homeX, float homeZ, float radius, int index, out float x, out float z)
        {
            x = homeX;
            z = homeZ;
            if (index == 0) return true;
            if (room == null || index < 0 || index >= SpotCount) return false;
            int ring = (index - 1) / SpotsPerRing + 1, turn = (index - 1) % SpotsPerRing;
            double angle = turn * 2.0 * Math.PI / SpotsPerRing;
            float reach = SpotStep(radius) * ring;
            x = homeX + (float)Math.Sin(angle) * reach;
            z = homeZ + (float)Math.Cos(angle) * reach;
            float inset = Math.Max(0f, radius) + WallSlack;
            return x >= room.MinX + inset && x <= room.MaxX - inset && z >= room.MinZ + inset && z <= room.MaxZ - inset;
        }
    }
}
