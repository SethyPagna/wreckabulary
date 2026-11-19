using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Surroundings
{
    public enum SurroundingsRing { Near = 0, Mid = 1, Far = 2 }

    public enum SurroundingsGroundShape { Disc, Rect }

    public static class SurroundingsSlots
    {
        public const string Plaster = "bk_plaster", Roof = "bk_roof", Paint = "bk_paint", Card = "bk_card", Grass = "bk_grass";
        public const uint NoTint = 0xFFFFFF;

        public static uint Pick(string slot, uint plaster, uint roof, uint paint) =>
            slot == Plaster ? plaster : slot == Roof ? roof : slot == Paint ? paint : NoTint;
    }

    public readonly struct SurroundingsPoint
    {
        public readonly float X, Y, Z;

        public SurroundingsPoint(float x, float y, float z)
        {
            X = x; Y = y; Z = z;
        }
    }

    public readonly struct SurroundingsBox
    {
        public readonly float MinX, MinY, MinZ, MaxX, MaxY, MaxZ;

        public SurroundingsBox(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        {
            MinX = minX; MinY = minY; MinZ = minZ;
            MaxX = maxX; MaxY = maxY; MaxZ = maxZ;
        }
    }

    public sealed class SurroundingsPlacement
    {
        public string Piece { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Z { get; internal set; }
        public float Yaw { get; internal set; }
        public float Scale { get; internal set; } = 1f;
        public SurroundingsRing Ring { get; internal set; }
        public bool Card { get; internal set; }
        public uint PlasterTint { get; internal set; } = SurroundingsSlots.NoTint;
        public uint RoofTint { get; internal set; } = SurroundingsSlots.NoTint;
        public uint PaintTint { get; internal set; } = SurroundingsSlots.NoTint;
        public string Source { get; internal set; }

        public bool InLow => Ring == SurroundingsRing.Near || Card;

        public uint TintFor(string slot) => SurroundingsSlots.Pick(slot, PlasterTint, RoofTint, PaintTint);
    }

    public sealed class SurroundingsMover
    {
        public string Piece { get; internal set; }
        public IReadOnlyList<SurroundingsPoint> Path { get; internal set; }
        public float Speed { get; internal set; }
        public bool Loop { get; internal set; } = true;
        public float Phase { get; internal set; }
        public uint PaintTint { get; internal set; } = SurroundingsSlots.NoTint;
        public string Source { get; internal set; }

        public uint TintFor(string slot) => SurroundingsSlots.Pick(slot, SurroundingsSlots.NoTint, SurroundingsSlots.NoTint, PaintTint);
    }

    public sealed class SurroundingsGround
    {
        public SurroundingsGroundShape Shape { get; internal set; }
        public string Slot { get; internal set; }
        public float Y { get; internal set; }
        public float CentreX { get; internal set; }
        public float CentreZ { get; internal set; }
        public float Radius { get; internal set; }
        public float MinX { get; internal set; }
        public float MinZ { get; internal set; }
        public float MaxX { get; internal set; }
        public float MaxZ { get; internal set; }
        public uint Tint { get; internal set; } = SurroundingsSlots.NoTint;
        public string Source { get; internal set; }
    }

    public sealed class SurroundingsGroup
    {
        public SurroundingsRing Ring { get; internal set; }
        public string Slot { get; internal set; }
        public int Pieces { get; internal set; }
        public int Triangles { get; internal set; }
        public int Vertices { get; internal set; }
        public int Renderers => Math.Max(1, (Vertices + SurroundingsLayout.MergeVertexLimit - 1) / SurroundingsLayout.MergeVertexLimit);
    }

    public sealed class SurroundingsTier
    {
        public IReadOnlyList<SurroundingsGroup> Groups { get; internal set; } = Array.Empty<SurroundingsGroup>();
        public int GroundRenderers { get; internal set; }
        public int Movers { get; internal set; }
        public int MoverTriangles { get; internal set; }

        public int Renderers => Groups.Sum(g => g.Renderers) + GroundRenderers + Movers;
        public int Triangles => Groups.Sum(g => g.Triangles) + MoverTriangles;

        public int RenderersIn(SurroundingsRing ring) => Groups.Where(g => g.Ring == ring).Sum(g => g.Renderers);
        public int TrianglesIn(SurroundingsRing ring) => Groups.Where(g => g.Ring == ring).Sum(g => g.Triangles);
    }

    public sealed class SurroundingsBudget
    {
        public const int HighRenderers = 40, HighTriangles = 150000, LowRenderers = 12, LowTriangles = 40000, MaxMovers = 12;

        public SurroundingsTier High { get; internal set; } = new SurroundingsTier();
        public SurroundingsTier Low { get; internal set; } = new SurroundingsTier();
    }

    public sealed class SurroundingsResult
    {
        public string MapId { get; internal set; }
        public string Name { get; internal set; }
        public string LightId { get; internal set; }
        public string AmbienceId { get; internal set; }
        public IReadOnlyList<SurroundingsPlacement> Placements { get; internal set; }
        public IReadOnlyList<SurroundingsMover> Movers { get; internal set; }
        public IReadOnlyList<SurroundingsGround> Ground { get; internal set; }
        public SurroundingsBox PlayVolume { get; internal set; }
        public SurroundingsBudget Budget { get; internal set; }
        public IReadOnlyList<string> Problems { get; internal set; }
        public IReadOnlyList<string> Dropped { get; internal set; }
        public string Canonical { get; internal set; }
        public string Hash { get; internal set; }

        public bool Valid => Problems.Count == 0;
    }

    public sealed class SurroundingsException : Exception
    {
        public IReadOnlyList<string> Problems { get; }

        public SurroundingsException(string map, IReadOnlyList<string> problems)
            : base($"{map}: {problems.Count} surroundings problem(s):\n  " + string.Join("\n  ", problems))
        {
            Problems = problems;
        }
    }
}
