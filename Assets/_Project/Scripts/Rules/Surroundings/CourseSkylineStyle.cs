using System;
using System.Collections.Generic;
using System.Linq;
using static Wreckabulary.Rules.Surroundings.CourseSurroundingsStyle;

namespace Wreckabulary.Rules.Surroundings
{
    sealed class CourseSkylineSpec
    {
        public const float MinStorey = 2.5f, MinHeight = 8f, MaxHeight = 200f;

        public float Cell, Jitter, Gap, Plaza, CardGap, Inset;
        public (float min, float max) Storey, Plinth;
        public CourseDowntownSpec Downtown;
        public CourseSkylineBand Ring;
        public readonly List<CourseFacadeSpec> Facades = new List<CourseFacadeSpec>();
        public List<uint> Trim;
        public uint Roof, Pavement, Tank;
        public List<string> ThinCards;
        public float KeepDowntown, KeepRing;
        public int StoreysPerBand;

        public float LotLimit => Cell - Gap - 2 * Plaza;

        public static CourseSkylineSpec Read(JsonNode n)
        {
            var low = At(n, "low");
            var spec = new CourseSkylineSpec
            {
                Cell = AtLeast(At(n, "cell"), 36f, 12f), Jitter = AtLeast(At(n, "jitter"), 4f, 0f), Gap = AtLeast(At(n, "gap"), 8f, 2f),
                Plaza = AtLeast(At(n, "plaza"), 2.5f, 0f), CardGap = AtLeast(At(n, "cardGap"), 4f, 0f), Inset = AtLeast(At(n, "inset"), .3f, .05f),
                Storey = Span(At(n, "storey"), 3.5f, MinStorey), Plinth = Span(At(n, "plinth"), 4f, MinStorey),
                Downtown = CourseDowntownSpec.Read(At(n, "downtown")),
                Ring = CourseSkylineBand.Read(At(n, "ring"), 92f, 170f, .3f, (20f, 38f), (16f, 24f)),
                Trim = At(n, "trim").Items.Select(SurroundingsPlan.Tint).ToList(),
                Roof = SurroundingsPlan.OptionalTint(At(n, "roof")) ?? SurroundingsSlots.NoTint,
                Pavement = SurroundingsPlan.OptionalTint(At(n, "pavement")) ?? SurroundingsSlots.NoTint,
                Tank = SurroundingsPlan.OptionalTint(At(n, "tank")) ?? SurroundingsSlots.NoTint,
                ThinCards = Names(At(n, "thinCards"), false),
                KeepDowntown = Fraction(At(low, "keepDowntown"), .75f), KeepRing = Fraction(At(low, "keepRing"), .4f),
                StoreysPerBand = Math.Max(1, At(low, "storeysPerBand").Int(2)),
            };
            if (spec.Trim.Count == 0) spec.Trim.Add(SurroundingsSlots.NoTint);
            var facades = At(n, "facades");
            if (facades.IsNull) throw n.Error("needs facades");
            foreach (string key in facades.Keys.OrderBy(k => k, StringComparer.Ordinal))
                spec.Facades.Add(CourseFacadeSpec.Read(key, facades[key]));
            if (spec.Facades.Count == 0) throw facades.Error("needs at least one facade");
            float widest = Math.Max(spec.Downtown.Band.Width.max, spec.Ring.Width.max);
            if (widest > spec.LotLimit) throw At(n, "cell").Error($"leaves {spec.LotLimit:0.##} m for a building, less than the widest footprint {widest} m");
            return spec;
        }
    }

    sealed class CourseSkylineBand
    {
        public float From, To, Chance, TallChance;
        public (float min, float max) Height, Width, Tall;

        public static CourseSkylineBand Read(JsonNode n, float from, float to, float chance, (float, float) height, (float, float) width)
        {
            var band = new CourseSkylineBand
            {
                From = AtLeast(At(n, "from"), from, 1f), To = At(n, "to").Float(to), Chance = Fraction(At(n, "chance"), chance),
                Height = Has(n, "height") ? Span(At(n, "height"), 0f, CourseSkylineSpec.MinHeight) : height,
                Width = Has(n, "width") ? Span(At(n, "width"), 0f, 6f) : width,
                TallChance = Fraction(At(n, "tallChance"), 0f),
            };
            band.Tall = Has(n, "tall") ? Span(At(n, "tall"), 0f, CourseSkylineSpec.MinHeight) : band.Height;
            if (band.To < band.From) throw At(n, "to").Error("must be at least from");
            if (Math.Max(band.Height.max, band.Tall.max) > CourseSkylineSpec.MaxHeight) throw At(n, "height").Error($"must stay at or below {CourseSkylineSpec.MaxHeight} m");
            return band;
        }
    }

    sealed class CourseDowntownSpec
    {
        public CourseSkylineBand Band;
        public (int min, int max) Clusters;
        public float Spread, Second, Core;

        public static CourseDowntownSpec Read(JsonNode n)
        {
            var clusters = Span(At(n, "clusters"), 1f, 1f);
            var spec = new CourseDowntownSpec
            {
                Band = CourseSkylineBand.Read(n, 110f, 235f, .9f, (48f, 90f), (14f, 22f)),
                Clusters = ((int)clusters.min, (int)clusters.max),
                Spread = AtLeast(At(n, "spread"), 30f, 5f), Second = AtLeast(At(n, "second"), 120f, 30f),
                Core = Fraction(At(n, "core"), .35f),
            };
            if (spec.Clusters.max > 2) throw At(n, "clusters").Error("allows at most two downtown clusters");
            if (spec.Second > 180f) throw At(n, "second").Error("must be at most 180 degrees");
            return spec;
        }
    }

    sealed class CourseFacadeSpec
    {
        const float MinFinSpacing = 1.5f;

        public string Id;
        public (float min, float max) Glass;
        public (float low, float tall) Weight;
        public List<uint> Walls, Panes;
        public bool Piers;
        public float Fins;

        public static CourseFacadeSpec Read(string id, JsonNode n)
        {
            var spec = new CourseFacadeSpec
            {
                Id = id, Glass = Span(At(n, "glass"), .5f, .1f), Weight = Pair(At(n, "weight"), 1f),
                Walls = At(n, "walls").Items.Select(SurroundingsPlan.Tint).ToList(),
                Panes = At(n, "panes").Items.Select(SurroundingsPlan.Tint).ToList(),
                Piers = At(n, "piers").Bool(false), Fins = At(n, "fins").Float(0f),
            };
            if (spec.Fins != 0 && spec.Fins < MinFinSpacing) throw At(n, "fins").Error($"must be 0 or a spacing of at least {MinFinSpacing} m");
            if (spec.Glass.max > .9f) throw At(n, "glass").Error("must stay at or below 0.9 so wall bands show");
            if (spec.Walls.Count == 0) throw At(n, "walls").Error("must list at least one colour");
            if (spec.Panes.Count == 0) spec.Panes.Add(SurroundingsSlots.NoTint);
            return spec;
        }

        public float WeightAt(float t) => Weight.low + (Weight.tall - Weight.low) * t;

        static (float, float) Pair(JsonNode n, float fallback)
        {
            if (n.IsNull) return (fallback, fallback);
            var pair = n.Floats(2);
            if (!(pair[0] >= 0f && pair[1] >= 0f)) throw n.Error("must be two weights at or above 0, for the lowest and the tallest building");
            return (pair[0], pair[1]);
        }
    }
}
