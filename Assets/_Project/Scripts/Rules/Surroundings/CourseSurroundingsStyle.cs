using System;
using System.Collections.Generic;
using System.Linq;
using static Wreckabulary.Rules.Surroundings.CourseSurroundingsStyle;

namespace Wreckabulary.Rules.Surroundings
{
    public sealed class CourseSurroundingsStyle
    {
        public const float MinGroundDrop = .3f, MinClear = 1f;

        public string Id { get; private set; }
        public string Name { get; private set; }
        public float Clear { get; private set; }
        public float GroundDrop { get; private set; }
        public float NearReach { get; private set; }
        public float MidReach { get; private set; }

        internal CourseGroundSpec Ground;
        internal readonly Dictionary<string, List<uint>> Palette = new Dictionary<string, List<uint>>(StringComparer.Ordinal);
        internal CourseStreetSpec Streets;
        internal CourseHouseSpec Houses;
        internal CourseScatterSpec Scatter;
        internal CourseLandmarkSpec Landmark;
        internal readonly List<CourseCardSpec> Cards = new List<CourseCardSpec>();
        internal CourseLowSpec Low;
        internal CourseSkylineSpec Skyline;

        public IReadOnlyList<string> SkippedLowSlots => Low.SkipSlots;
        public bool HasSkyline => Skyline != null;

        public IEnumerable<string> Pieces
        {
            get
            {
                var ids = new List<string> { Streets.Road, Streets.Pavement };
                if (Streets.Lamp != null) ids.Add(Streets.Lamp);
                ids.AddRange(Streets.Cars);
                ids.AddRange(Houses.Pieces);
                ids.AddRange(Houses.Trees);
                ids.AddRange(Scatter.Trees);
                if (Landmark != null) ids.Add(Landmark.Piece);
                ids.AddRange(Cards.Select(c => c.Piece));
                return ids.Distinct(StringComparer.Ordinal).ToList();
            }
        }

        public static CourseSurroundingsStyle FromJson(string json, string source = "course_city.json")
        {
            var root = Json.Parse(json, source);
            var style = new CourseSurroundingsStyle
            {
                Id = SurroundingsPlan.Id(root["style"]), Name = root["name"].String(),
                Clear = AtLeast(root["clear"], 4f, MinClear), GroundDrop = AtLeast(root["groundDrop"], .35f, MinGroundDrop),
                NearReach = AtLeast(root["nearReach"], 30f, 1f), MidReach = AtLeast(root["midReach"], 70f, 1f),
                Ground = CourseGroundSpec.Read(root["ground"]),
                Streets = CourseStreetSpec.Read(root["streets"]),
                Houses = CourseHouseSpec.Read(root["houses"]),
                Scatter = CourseScatterSpec.Read(root["farScatter"]),
                Landmark = root.Has("landmark") ? CourseLandmarkSpec.Read(root["landmark"]) : null,
                Low = CourseLowSpec.Read(root["low"]),
                Skyline = root.Has("skyline") ? CourseSkylineSpec.Read(root["skyline"]) : null,
            };
            if (style.MidReach < style.NearReach) throw root["midReach"].Error("must be at least nearReach");
            if (root.Has("palette"))
                foreach (string key in root["palette"].Keys)
                    style.Palette[key] = root["palette"][key].Items.Select(SurroundingsPlan.Tint).ToList();
            style.Cards.AddRange(root["cards"].Items.Select(CourseCardSpec.Read));
            return style;
        }

        internal List<uint> PaletteOf(string name) => Palette.TryGetValue(name, out var list) ? list : new List<uint>();

        internal static JsonNode At(JsonNode node, string key) => node.IsNull ? node : node[key];

        internal static bool Has(JsonNode node, string key) => !node.IsNull && node.Has(key);

        internal static float AtLeast(JsonNode node, float fallback, float least)
        {
            float value = node.Float(fallback);
            if (!(value >= least)) throw node.Error($"must be at least {least}");
            return value;
        }

        internal static (float min, float max) Span(JsonNode node, float fallback, float least)
        {
            var range = SurroundingsPlan.Range(node, fallback);
            if (!(range.min >= least)) throw node.Error($"must not go below {least}");
            return range;
        }

        internal static float Fraction(JsonNode node, float fallback)
        {
            float value = node.Float(fallback);
            if (!(value >= 0f && value <= 1f)) throw node.Error("must be between 0 and 1");
            return value;
        }

        internal static List<string> Names(JsonNode node, bool required)
        {
            var names = node.IsNull ? new List<string>() : node.Strings();
            if (required && names.Count == 0) throw node.Error("must name at least one piece");
            return names;
        }
    }

    sealed class CourseGroundSpec
    {
        public string Slot, ApronSlot;
        public uint Tint, ApronTint;
        public float Reach;
        public List<(float reach, uint tint)> Fades = new List<(float, uint)>();

        public static CourseGroundSpec Read(JsonNode n)
        {
            var apron = At(n, "apron");
            var fade = At(n, "fade");
            var g = new CourseGroundSpec
            {
                Slot = At(n, "slot").String(SurroundingsSlots.Grass), Tint = SurroundingsPlan.OptionalTint(At(n, "tint")) ?? SurroundingsSlots.NoTint,
                Reach = AtLeast(At(n, "reach"), 560f, 10f),
                Fades = fade.Items.Select(f => (AtLeast(At(f, "reach"), 0f, 1f), SurroundingsPlan.Tint(At(f, "tint")))).ToList(),
                ApronSlot = At(apron, "slot").String("bk_stone"), ApronTint = SurroundingsPlan.OptionalTint(At(apron, "tint")) ?? SurroundingsSlots.NoTint,
            };
            for (int i = 0; i < g.Fades.Count; i++)
                if (!(g.Fades[i].reach < (i == 0 ? g.Reach : g.Fades[i - 1].reach))) throw fade.Error("reaches must shrink, the outer ring first, inside the ground reach");
            return g;
        }
    }

    sealed class CourseStreetSpec
    {
        public string Road, Pavement, Lamp;
        public float Reach, LampEvery, LampInset, ParkEvery, ParkChance, Kerb;
        public List<string> Cars;

        public static CourseStreetSpec Read(JsonNode n)
        {
            var lamps = At(n, "lamps");
            var parking = At(n, "parking");
            return new CourseStreetSpec
            {
                Road = At(n, "road").String("road_straight"), Pavement = At(n, "pavement").String("pavement_straight"),
                Reach = AtLeast(At(n, "reach"), 64f, 0f),
                Lamp = lamps.IsNull ? null : At(lamps, "piece").String("lamp_post"),
                LampEvery = AtLeast(At(lamps, "every"), 18f, 4f), LampInset = At(lamps, "inset").Float(.45f),
                Cars = Names(At(parking, "pieces"), false), ParkEvery = AtLeast(At(parking, "every"), 8f, 4.5f),
                ParkChance = Fraction(At(parking, "chance"), .45f), Kerb = At(parking, "kerb").Float(.25f),
            };
        }
    }

    sealed class CourseHouseSpec
    {
        public List<string> Pieces, Trees;
        public (float min, float max) Setback, Gap, BackScale, BackGap;
        public float TreeChance, Garden;

        public static CourseHouseSpec Read(JsonNode n)
        {
            var back = At(n, "backRow");
            return new CourseHouseSpec
            {
                Pieces = Names(At(n, "pieces"), true), Trees = Names(At(n, "trees"), false),
                Setback = Span(At(n, "setback"), 2.5f, 0f), Gap = Span(At(n, "gap"), 2f, .5f),
                TreeChance = Fraction(At(n, "treeChance"), .6f), Garden = AtLeast(At(back, "garden"), 7f, 0f),
                BackScale = Span(At(back, "scale"), 1.3f, .5f), BackGap = Span(At(back, "gap"), 2f, .5f),
            };
        }
    }

    sealed class CourseScatterSpec
    {
        public float From, To, Cell, Jitter, HouseChance, TreeChance;
        public (float min, float max) Scale;
        public List<string> Trees;

        public static CourseScatterSpec Read(JsonNode n)
        {
            var s = new CourseScatterSpec
            {
                From = AtLeast(At(n, "from"), 66f, 1f), To = At(n, "to").Float(150f),
                Cell = AtLeast(At(n, "cell"), 22f, 8f), Jitter = At(n, "jitter").Float(7f),
                HouseChance = Fraction(At(n, "houseChance"), .6f), TreeChance = Fraction(At(n, "treeChance"), .25f),
                Scale = Span(At(n, "scale"), 1.4f, .5f),
                Trees = Has(n, "trees") ? At(n, "trees").Strings() : new List<string> { "tree_far" },
            };
            if (s.To < s.From) throw At(n, "to").Error("must be at least from");
            if (s.HouseChance + s.TreeChance > 1f) throw At(n, "treeChance").Error("houseChance and treeChance must add up to at most 1");
            return s;
        }
    }

    sealed class CourseLandmarkSpec
    {
        public string Piece;
        public float Reach, Yaw;

        public static CourseLandmarkSpec Read(JsonNode n) => new CourseLandmarkSpec
        {
            Piece = At(n, "piece").String(), Reach = AtLeast(At(n, "reach"), 105f, 1f), Yaw = At(n, "yaw").Float(0f),
        };
    }

    sealed class CourseCardSpec
    {
        public string Piece;
        public float Reach, Spacing, Jitter;
        public (float min, float max) Scale;

        public static CourseCardSpec Read(JsonNode n) => new CourseCardSpec
        {
            Piece = At(n, "piece").String(), Reach = AtLeast(At(n, "reach"), 150f, 1f),
            Spacing = AtLeast(At(n, "spacing"), 46f, 4f), Jitter = At(n, "jitter").Float(0f), Scale = Span(At(n, "scale"), 1f, .25f),
        };
    }

    sealed class CourseLowSpec
    {
        public List<string> SkipSlots;
        public float KeepTrees, KeepBackRow, KeepScatter;

        public static CourseLowSpec Read(JsonNode n) => new CourseLowSpec
        {
            SkipSlots = Names(At(n, "skipSlots"), false),
            KeepTrees = Fraction(At(n, "keepTrees"), .5f), KeepBackRow = Fraction(At(n, "keepBackRow"), .5f),
            KeepScatter = Fraction(At(n, "keepScatter"), .5f),
        };
    }
}
