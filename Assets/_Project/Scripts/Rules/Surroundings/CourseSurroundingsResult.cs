using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Surroundings
{
    public enum CourseSurroundingsTier { High, Low }

    public sealed class CourseFootprint
    {
        public const float MaxSpan = 2000f;
        const float MillimetresPerMetre = 1000f;

        public float MinX { get; }
        public float MinZ { get; }
        public float MaxX { get; }
        public float MaxZ { get; }
        public float Floor { get; }
        public float Top { get; }

        public CourseFootprint(float minX, float minZ, float maxX, float maxZ, float floor, float top)
        {
            bool finite = new[] { minX, minZ, maxX, maxZ, floor, top }.All(v => !float.IsNaN(v) && !float.IsInfinity(v));
            if (!finite || !(maxX > minX) || !(maxZ > minZ) || !(top >= floor) || maxX - minX > MaxSpan || maxZ - minZ > MaxSpan)
                throw new ArgumentException($"A course footprint needs a finite area up to {MaxSpan} m a side and a top at or above its floor.");
            MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ; Floor = floor; Top = top;
        }

        public static CourseFootprint Of(IEnumerable<CourseBox> modules)
        {
            var boxes = (modules ?? throw new ArgumentNullException(nameof(modules))).ToList();
            if (boxes.Count == 0) throw new ArgumentException("A course with no modules has no footprint.", nameof(modules));
            return new CourseFootprint(
                boxes.Min(b => b.Min.X) / MillimetresPerMetre, boxes.Min(b => b.Min.Z) / MillimetresPerMetre,
                boxes.Max(b => b.Max.X) / MillimetresPerMetre, boxes.Max(b => b.Max.Z) / MillimetresPerMetre,
                boxes.Min(b => b.Min.Y) / MillimetresPerMetre, boxes.Max(b => b.Max.Y) / MillimetresPerMetre);
        }

        public float Width => MaxX - MinX;
        public float Depth => MaxZ - MinZ;
        public float CentreX => (MinX + MaxX) * .5f;
        public float CentreZ => (MinZ + MaxZ) * .5f;
        public float HalfDiagonal => (float)Math.Sqrt(Width * (double)Width + Depth * (double)Depth) * .5f;

        public string Key => string.Join(",", new[] { MinX, MinZ, MaxX, MaxZ, Floor, Top }.Select(v => v.ToString("0.###", CultureInfo.InvariantCulture)));

        internal Rect Rect => new Rect(MinX, MinZ, MaxX, MaxZ);
    }

    public sealed class CourseSurroundingsResult
    {
        public string CourseId { get; internal set; }
        public string StyleId { get; internal set; }
        public string Name { get; internal set; }
        public CourseSurroundingsTier Tier { get; internal set; }
        public CourseFootprint Footprint { get; internal set; }
        public float Clear { get; internal set; }
        public float GroundY { get; internal set; }
        public IReadOnlyList<SurroundingsPlacement> Placements { get; internal set; } = Array.Empty<SurroundingsPlacement>();
        public IReadOnlyList<float> Distances { get; internal set; } = Array.Empty<float>();
        public IReadOnlyList<SurroundingsGround> Ground { get; internal set; } = Array.Empty<SurroundingsGround>();
        public IReadOnlyList<SurroundingsGroup> Groups { get; internal set; } = Array.Empty<SurroundingsGroup>();
        public IReadOnlyList<string> SkippedSlots { get; internal set; } = Array.Empty<string>();
        public IReadOnlyList<string> Problems { get; internal set; } = Array.Empty<string>();
        public int Trimmed { get; internal set; }
        public int RendererLimit { get; internal set; }
        public int TriangleLimit { get; internal set; }
        public string Canonical { get; internal set; }
        public string Hash { get; internal set; }

        public bool Valid => Problems.Count == 0;
        public int Renderers => Groups.Sum(g => g.Renderers);
        public int Triangles => Groups.Sum(g => g.Triangles);
        public int Vertices => Groups.Sum(g => g.Vertices);

        public SurroundingsRing BandOf(SurroundingsPlacement placement) =>
            Tier == CourseSurroundingsTier.High && placement.Ring == SurroundingsRing.Near ? SurroundingsRing.Near : SurroundingsRing.Far;

        public bool CastsShadows(SurroundingsRing band) => Tier == CourseSurroundingsTier.High && band == SurroundingsRing.Near;

        public bool Skips(string slot) => SkippedSlots.Contains(slot);

        public int Count(string piece) => Placements.Count(p => p.Piece == piece);
    }
}
