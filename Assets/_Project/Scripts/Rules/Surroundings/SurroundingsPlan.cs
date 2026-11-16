using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Wreckabulary.Rules.Surroundings
{
    public sealed class SurroundingsPlan
    {
        static readonly string[] Sides = { "default", "north", "east", "south", "west" };

        public string MapId { get; private set; }
        public string Name { get; private set; }
        public string HouseFile { get; private set; }
        public string LightId { get; private set; }
        public string AmbienceId { get; private set; }

        internal string Source;
        internal GroundSpec Ground;
        internal readonly Dictionary<string, List<uint>> Palette = new Dictionary<string, List<uint>>(StringComparer.Ordinal);
        internal readonly List<string> StreetFurniture = new List<string>();
        internal readonly Dictionary<string, EdgingSpec> Edging = new Dictionary<string, EdgingSpec>(StringComparer.Ordinal);
        internal readonly List<StreetSpec> Streets = new List<StreetSpec>();
        internal readonly List<PlotSpec> Plots = new List<PlotSpec>();
        internal readonly List<PropSpec> Props = new List<PropSpec>();
        internal readonly List<RowSpec> Rows = new List<RowSpec>();
        internal readonly List<BeltSpec> Belts = new List<BeltSpec>();
        internal readonly List<SightlineSpec> Sightlines = new List<SightlineSpec>();
        internal readonly List<CardRingSpec> Cards = new List<CardRingSpec>();
        internal readonly List<MoverSpec> Movers = new List<MoverSpec>();
        internal readonly List<ZoneSpec> Zones = new List<ZoneSpec>();
        internal PropSpec Landmark;
        internal ScatterSpec Scatter;
        internal LawnSpec Lawns;
        internal SoilSpec Soil;

        public static SurroundingsPlan FromJson(string json, string source = "plan.json")
        {
            var root = Json.Parse(json, source);
            var plan = new SurroundingsPlan
            {
                Source = source, MapId = Id(root["map"]), Name = root["name"].String(), HouseFile = root["house"].String(),
                LightId = root["light"].String(), AmbienceId = root["ambience"].String(),
                Ground = GroundSpec.Read(root["ground"]),
            };
            if (root.Has("palette"))
                foreach (string key in root["palette"].Keys)
                    plan.Palette[key] = root["palette"][key].Items.Select(Tint).ToList();
            plan.StreetFurniture.AddRange(root.Has("streetFurniture") ? root["streetFurniture"].Strings() : new List<string> { "lamp_post", "bench_park" });
            if (root.Has("edging"))
                foreach (string side in root["edging"].Keys)
                {
                    if (Array.IndexOf(Sides, side) < 0) throw root["edging"][side].Error("must be default, north, east, south or west");
                    plan.Edging[side] = EdgingSpec.Read(root["edging"][side]);
                }
            plan.Streets.AddRange(root["streets"].Items.Select(StreetSpec.Read));
            plan.Plots.AddRange(root["plots"].Items.Select(PlotSpec.Read));
            plan.Props.AddRange(root["props"].Items.Select(PropSpec.Read));
            plan.Rows.AddRange(root["rows"].Items.Select(RowSpec.Read));
            plan.Belts.AddRange(root["treeBelts"].Items.Select(BeltSpec.Read));
            plan.Sightlines.AddRange(root["sightlines"].Items.Select(SightlineSpec.Read));
            plan.Cards.AddRange(root["cards"].Items.Select(CardRingSpec.Read));
            plan.Movers.AddRange(root["movers"].Items.Select(MoverSpec.Read));
            plan.Zones.AddRange(root["zones"].Items.Select(ZoneSpec.Read));
            if (root.Has("landmark")) plan.Landmark = PropSpec.Read(root["landmark"]);
            if (root.Has("farScatter")) plan.Scatter = ScatterSpec.Read(root["farScatter"]);
            plan.Lawns = LawnSpec.Read(root["lawns"]);
            plan.Soil = SoilSpec.Read(root["soil"]);
            CheckUnique(plan.Streets.Select(s => (s.Id, s.Path)), "street");
            CheckUnique(plan.Plots.Select(p => (p.Id, p.Path)), "plot");
            return plan;
        }

        internal List<uint> PaletteOf(string name) => Palette.TryGetValue(name, out var list) ? list : new List<uint>();

        static void CheckUnique(IEnumerable<(string id, string path)> ids, string what)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (id, path) in ids)
                if (!seen.Add(id)) throw new FormatException($"{path}: {what} id \"{id}\" is used twice");
        }

        internal static string Id(JsonNode node)
        {
            string id = node.String();
            if (id.Length == 0 || id.Any(c => !(char.IsLetterOrDigit(c) || c == '_' || c == '-'))) throw node.Error("must be a plain id (letters, digits, _ or -)");
            return id;
        }

        internal static uint Tint(JsonNode node)
        {
            string text = node.String();
            if (text.Length == 7 && text[0] == '#' && uint.TryParse(text.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb)) return rgb;
            throw node.Error("must be a colour like \"#A9573F\"");
        }

        internal static uint? OptionalTint(JsonNode node) => node.IsNull ? (uint?)null : Tint(node);

        internal static Flat Point(JsonNode node)
        {
            var p = node.Floats(2);
            return new Flat(p[0], p[1]);
        }

        internal static string Choice(JsonNode node, string fallback, params string[] allowed)
        {
            string value = node.String(fallback);
            if (value != null && Array.IndexOf(allowed, value) < 0) throw node.Error("must be one of " + string.Join(", ", allowed));
            return value;
        }

        internal static Flat Compass(JsonNode node)
        {
            switch (Choice(node, null, "north", "east", "south", "west"))
            {
                case "north": return new Flat(0, 1);
                case "east": return new Flat(1, 0);
                case "south": return new Flat(0, -1);
                default: return new Flat(-1, 0);
            }
        }

        internal static (float min, float max) Range(JsonNode node, float fallback)
        {
            if (node.IsNull) return (fallback, fallback);
            var r = node.Floats(2);
            if (r[1] < r[0]) throw node.Error("must be [low, high]");
            return (r[0], r[1]);
        }
    }

    sealed class GroundSpec
    {
        public float Y = -.35f, PlinthY = -.15f, PlinthReach = 1f, Radius = 600f;
        public string Slot = SurroundingsSlots.Grass;
        public uint Tint = SurroundingsSlots.NoTint;
        public List<(float radius, uint tint)> Fades = new List<(float, uint)>();

        public static GroundSpec Read(JsonNode n)
        {
            if (n.IsNull) return new GroundSpec();
            var g = new GroundSpec
            {
                Y = n["y"].Float(-.35f), PlinthY = n["plinthY"].Float(-.15f), PlinthReach = n["plinthReach"].Float(1f),
                Radius = n["radius"].Float(600f), Slot = n["slot"].String(SurroundingsSlots.Grass),
                Tint = SurroundingsPlan.OptionalTint(n["tint"]) ?? SurroundingsSlots.NoTint,
                Fades = n["fade"].Items.Select(f => (f["radius"].Float(), SurroundingsPlan.Tint(f["tint"]))).ToList(),
            };
            for (int i = 1; i < g.Fades.Count; i++)
                if (!(g.Fades[i].radius < g.Fades[i - 1].radius)) throw n["fade"].Error("radii must shrink, the outer ring first");
            return g;
        }
    }

    sealed class LawnSpec
    {
        public List<uint> Tones;
        public float Stripe, Shift;

        public static LawnSpec Read(JsonNode n) => n.IsNull ? null : new LawnSpec
        {
            Tones = n["tones"].Items.Select(SurroundingsPlan.Tint).ToList(), Stripe = n["stripe"].Float(0f), Shift = n["stripeShift"].Float(.07f),
        };
    }

    sealed class SoilSpec
    {
        public List<string> Under;
        public float Margin;
        public uint Tint;

        public static SoilSpec Read(JsonNode n) => n.IsNull ? null : new SoilSpec
        {
            Under = n["under"].Strings(), Margin = n["margin"].Float(.2f), Tint = SurroundingsPlan.Tint(n["tint"]),
        };
    }

    sealed class EdgingSpec
    {
        public string Path, Piece, End;
        public float Offset;
        public bool None;

        public static EdgingSpec Read(JsonNode n) => new EdgingSpec
        {
            Path = n.Path, Piece = n["piece"].String(null), End = n["end"].String(null), Offset = n["offset"].Float(1f),
            None = !n.Has("piece"),
        };
    }

    sealed class LampSpec
    {
        public string Path, Piece, Side;
        public float Every, Phase, Inset;

        public static LampSpec Read(JsonNode n) => n.IsNull ? null : new LampSpec
        {
            Path = n.Path, Piece = n["piece"].String("lamp_post"), Side = SurroundingsPlan.Choice(n["side"], "left", "left", "right", "both"),
            Every = Math.Max(2f, n["every"].Float(16f)), Phase = n["phase"].Float(0f), Inset = n["inset"].Float(.45f),
        };
    }

    sealed class ParkingSpec
    {
        public string Path, Side;
        public float Every, Phase, Chance, Reach;
        public List<string> Pieces, ClearOf;

        public static ParkingSpec Read(JsonNode n) => n.IsNull ? null : new ParkingSpec
        {
            Path = n.Path, Side = SurroundingsPlan.Choice(n["side"], "left", "left", "right", "both"),
            Every = Math.Max(4.5f, n["every"].Float(10f)), Phase = n["phase"].Float(0f), Chance = n["chance"].Float(.6f),
            Reach = n["reach"].Float(3.5f), Pieces = n["pieces"].Strings(), ClearOf = n.Has("clearOf") ? n["clearOf"].Strings() : new List<string> { "driveway" },
        };
    }

    sealed class StreetSpec
    {
        public string Path, Id, Road, Pavement, PavementSides;
        public Flat From, To;
        public float Width;
        public LampSpec Lamps;
        public ParkingSpec Parking;
        public PropSpec End;

        public static StreetSpec Read(JsonNode n) => new StreetSpec
        {
            Path = n.Path, Id = SurroundingsPlan.Id(n["id"]), From = SurroundingsPlan.Point(n["from"]), To = SurroundingsPlan.Point(n["to"]),
            Width = n["width"].Float(7f), Road = n["road"].String("road_straight"), Pavement = n["pavement"].String("pavement_straight"),
            PavementSides = SurroundingsPlan.Choice(n["pavementSides"], "both", "both", "left", "right", "none"),
            Lamps = LampSpec.Read(n["lamps"]), Parking = ParkingSpec.Read(n["parking"]), End = n.Has("end") ? PropSpec.Read(n["end"]) : null,
        };
    }

    sealed class FenceSpec
    {
        public string Path, Piece, Gate;
        public List<string> Sides;

        public static FenceSpec Read(JsonNode n) => n.IsNull ? null : new FenceSpec
        {
            Path = n.Path, Piece = n["piece"].String(), Gate = n["gate"].String(null), Sides = n["sides"].Strings(),
        };
    }

    sealed class PlotSpec
    {
        public string Path, Id, House, Driveway, Car, Garage;
        public Rect Area;
        public Flat Front;
        public float Setback, Along;
        public uint? Plaster, Roof, Paint;
        public int Trees, Bushes;
        public bool Flowers;
        public List<string> Extras, TreePieces;
        public FenceSpec Fence;

        public static PlotSpec Read(JsonNode n)
        {
            var r = n["rect"].Floats(4);
            var plot = new PlotSpec
            {
                Path = n.Path, Id = SurroundingsPlan.Id(n["id"]), Area = new Rect(r[0], r[1], r[2], r[3]), Front = SurroundingsPlan.Compass(n["front"]),
                House = n["house"].String(null), Setback = n["setback"].Float(6f), Along = n["along"].Float(0f),
                Plaster = SurroundingsPlan.OptionalTint(n["plaster"]), Roof = SurroundingsPlan.OptionalTint(n["roof"]), Paint = SurroundingsPlan.OptionalTint(n["paint"]),
                Driveway = SurroundingsPlan.Choice(n["driveway"], null, "left", "right"), Car = n["car"].String(null), Garage = n["garage"].String(null),
                Trees = n["trees"].Int(1), Bushes = n["bushes"].Int(2), Flowers = n["flowers"].Bool(true),
                Extras = n["extras"].Strings(),
                TreePieces = n.Has("treePieces") ? n["treePieces"].Strings() : new List<string> { "tree_round_a", "tree_round_b", "tree_round_c" },
                Fence = FenceSpec.Read(n["fence"]),
            };
            if (!(r[2] > r[0] && r[3] > r[1])) throw n["rect"].Error("must be [minX, minZ, maxX, maxZ]");
            return plot;
        }
    }

    sealed class PropSpec
    {
        public string Path, Piece;
        public Flat At;
        public float Yaw, Scale;
        public float? Y;
        public uint? Plaster, Roof, Paint;

        public static PropSpec Read(JsonNode n) => new PropSpec
        {
            Path = n.Path, Piece = n["piece"].String(), At = SurroundingsPlan.Point(n["at"]), Yaw = n["yaw"].Float(0f), Scale = n["scale"].Float(1f),
            Y = n.Has("y") ? n["y"].Float() : (float?)null,
            Plaster = SurroundingsPlan.OptionalTint(n["plaster"]), Roof = SurroundingsPlan.OptionalTint(n["roof"]), Paint = SurroundingsPlan.OptionalTint(n["paint"]),
        };
    }

    sealed class RowSpec
    {
        public string Path, Piece;
        public Flat From, To;
        public float Every, Scale;
        public bool Flip;
        public uint? Paint;

        public static RowSpec Read(JsonNode n) => new RowSpec
        {
            Path = n.Path, Piece = n["piece"].String(), From = SurroundingsPlan.Point(n["from"]), To = SurroundingsPlan.Point(n["to"]),
            Every = n["every"].Float(0f), Scale = n["scale"].Float(1f), Flip = n["flip"].Bool(false), Paint = SurroundingsPlan.OptionalTint(n["paint"]),
        };
    }

    sealed class BeltSpec
    {
        public string Path;
        public List<string> Pieces;
        public Flat From, To;
        public float Every, Jitter;
        public (float min, float max) Scale;

        public static BeltSpec Read(JsonNode n) => new BeltSpec
        {
            Path = n.Path, Pieces = n["pieces"].Strings(), From = SurroundingsPlan.Point(n["from"]), To = SurroundingsPlan.Point(n["to"]),
            Every = Math.Max(1f, n["every"].Float(8f)), Jitter = n["jitter"].Float(1f), Scale = SurroundingsPlan.Range(n["scale"], 1f),
        };
    }

    sealed class SightlineSpec
    {
        public string Path;
        public Flat From;
        public Flat? To;
        public float Width, Eye;

        public static SightlineSpec Read(JsonNode n) => new SightlineSpec
        {
            Path = n.Path, From = SurroundingsPlan.Point(n["from"]),
            To = n["to"].IsNull || (n["to"].IsArray == false && n["to"].String() == "landmark") ? (Flat?)null : SurroundingsPlan.Point(n["to"]),
            Width = n["width"].Float(8f), Eye = n["eye"].Float(1.7f),
        };
    }

    sealed class ScatterSpec
    {
        public string Path;
        public float From, To, Cell, Jitter, HouseChance, TreeChance;
        public List<string> Houses, Trees;

        public static ScatterSpec Read(JsonNode n) => new ScatterSpec
        {
            Path = n.Path, From = n["from"].Float(SurroundingsLayout.MidReach), To = n["to"].Float(200f), Cell = Math.Max(8f, n["cell"].Float(26f)),
            Jitter = n["jitter"].Float(6f), HouseChance = n["houseChance"].Float(.5f), TreeChance = n["treeChance"].Float(.3f),
            Houses = n["houses"].Strings(), Trees = n.Has("trees") ? n["trees"].Strings() : new List<string> { "tree_far" },
        };
    }

    sealed class CardRingSpec
    {
        public string Path, Piece;
        public float Radius, Phase, Jitter;
        public int Count;

        public static CardRingSpec Read(JsonNode n) => new CardRingSpec
        {
            Path = n.Path, Piece = n["piece"].String(), Radius = n["radius"].Float(), Count = Math.Max(1, n["count"].Int()),
            Phase = n["phase"].Float(0f), Jitter = n["jitter"].Float(0f),
        };
    }

    sealed class MoverSpec
    {
        public string Path, Piece, Street;
        public List<Flat> Points;
        public float Lane, Speed, Phase;
        public uint? Paint;

        public static MoverSpec Read(JsonNode n) => new MoverSpec
        {
            Path = n.Path, Piece = n["piece"].String(), Street = n["street"].String(null),
            Points = n["path"].Items.Select(SurroundingsPlan.Point).ToList(),
            Lane = n["lane"].Float(1.75f), Speed = n["speed"].Float(4f), Phase = n["phase"].Float(0f), Paint = SurroundingsPlan.OptionalTint(n["paint"]),
        };
    }

    sealed class ZoneSpec
    {
        public string Path, Slot;
        public Rect Area;
        public float Radius;
        public float? Y;
        public uint Tint;
        public bool Disc, Stripes;

        public static ZoneSpec Read(JsonNode n)
        {
            var zone = new ZoneSpec
            {
                Path = n.Path, Slot = n["slot"].String(null), Y = n.Has("y") ? n["y"].Float() : (float?)null,
                Tint = SurroundingsPlan.OptionalTint(n["tint"]) ?? SurroundingsSlots.NoTint, Stripes = n["stripes"].Bool(false), Disc = n.Has("disc"),
            };
            if (zone.Disc)
            {
                var d = n["disc"].Floats(3);
                zone.Radius = d[2];
                zone.Area = new Rect(d[0] - d[2], d[1] - d[2], d[0] + d[2], d[1] + d[2]);
            }
            else
            {
                var r = n["rect"].Floats(4);
                zone.Area = new Rect(r[0], r[1], r[2], r[3]);
            }
            return zone;
        }
    }
}
