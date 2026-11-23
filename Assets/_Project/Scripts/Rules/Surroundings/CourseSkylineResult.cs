using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Surroundings
{
    public sealed class SkylineBox
    {
        public string Slot { get; internal set; }
        public string Part { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Z { get; internal set; }
        public float Width { get; internal set; }
        public float Depth { get; internal set; }
        public float Height { get; internal set; }
        public float Yaw { get; internal set; }
        public uint Tint { get; internal set; } = SurroundingsSlots.NoTint;
        public bool Top { get; internal set; }

        public float TopY => Y + Height;
        public int Faces => Top ? 5 : 4;
        public int Triangles => Faces * 2;
        public int Vertices => Faces * 4;

        public (double x, double z)[] Corners()
        {
            double yaw = Yaw * Math.PI / 180, hw = Width * .5, hd = Depth * .5;
            double ax = Math.Sin(yaw), az = Math.Cos(yaw), rx = az, rz = -ax;
            return new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }
                .Select(c => (X + rx * hw * c.Item1 + ax * hd * c.Item2, Z + rz * hw * c.Item1 + az * hd * c.Item2)).ToArray();
        }
    }

    public sealed class CourseTower
    {
        public int Ordinal { get; internal set; }
        public float X { get; internal set; }
        public float Z { get; internal set; }
        public float Yaw { get; internal set; }
        public float Width { get; internal set; }
        public float Depth { get; internal set; }
        public float LotWidth { get; internal set; }
        public float LotDepth { get; internal set; }
        public float Ground { get; internal set; }
        public float Height { get; internal set; }
        public float Distance { get; internal set; }
        public float Heading { get; internal set; }
        public float Weight { get; internal set; }
        public bool Downtown { get; internal set; }
        public string Facade { get; internal set; }
        public uint Wall { get; internal set; }
        public uint Pane { get; internal set; }
        public string Source { get; internal set; }
        public IReadOnlyList<SkylineBox> Boxes { get; internal set; } = Array.Empty<SkylineBox>();

        public float Top => Boxes.Count == 0 ? Ground + Height : Boxes.Max(b => b.TopY);
        public int Triangles => Boxes.Sum(b => b.Triangles);
    }

    public sealed class SkylineGroup
    {
        public string Slot { get; internal set; }
        public int Boxes { get; internal set; }
        public int Triangles { get; internal set; }
        public int Vertices { get; internal set; }
        public int Renderers { get; internal set; }
    }

    public sealed class SkylineMeshPart
    {
        public string Slot { get; internal set; }
        public int Index { get; internal set; }
        public float[] Positions { get; internal set; }
        public float[] Normals { get; internal set; }
        public uint[] Colours { get; internal set; }
        public int[] Indices { get; internal set; }

        public int Vertices => Positions.Length / 3;
        public int Triangles => Indices.Length / 3;
        public string Label => Slot + (Index == 0 ? "" : "_" + (Index + 1));
    }

    public sealed class CourseSkylineResult
    {
        public CourseSurroundingsTier Tier { get; internal set; }
        public IReadOnlyList<float> Headings { get; internal set; } = Array.Empty<float>();
        public IReadOnlyList<CourseTower> Towers { get; internal set; } = Array.Empty<CourseTower>();
        public IReadOnlyList<SkylineGroup> Groups { get; internal set; } = Array.Empty<SkylineGroup>();
        public IReadOnlyList<string> ThinnedCards { get; internal set; } = Array.Empty<string>();
        public int Trimmed { get; internal set; }
        public int RendererLimit { get; internal set; }
        public int TriangleLimit { get; internal set; }
        public string Canonical { get; internal set; } = "";
        public string Hash { get; internal set; } = "";

        public IEnumerable<SkylineBox> Boxes => Towers.SelectMany(t => t.Boxes);
        public int Renderers => Groups.Sum(g => g.Renderers);
        public int Triangles => Groups.Sum(g => g.Triangles);
        public int Vertices => Groups.Sum(g => g.Vertices);
        public bool Empty => Towers.Count == 0;

        public static CourseSkylineResult None(CourseSurroundingsTier tier) => new CourseSkylineResult
        {
            Tier = tier,
            RendererLimit = tier == CourseSurroundingsTier.High ? CourseSkyline.HighRenderers : CourseSkyline.LowRenderers,
            TriangleLimit = tier == CourseSurroundingsTier.High ? CourseSkyline.HighTriangles : CourseSkyline.LowTriangles,
        };
    }
}
