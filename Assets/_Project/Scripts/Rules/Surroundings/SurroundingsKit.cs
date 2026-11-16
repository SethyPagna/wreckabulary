using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules.Surroundings
{
    public sealed class SurroundingsPiece
    {
        static readonly string[] BuildingFamilies = { "house", "garage", "shed", "landmark", "building" };
        static readonly string[] BuildingPrefixes = { "house_", "garage_", "shed_", "landmark_" };

        public string Id { get; internal set; }
        public string Kit { get; internal set; }
        public string Family { get; internal set; }
        public string Fbx { get; internal set; }
        public string FbxObject { get; internal set; }
        public string Far { get; internal set; }
        public float Width { get; internal set; }
        public float Depth { get; internal set; }
        public float Height { get; internal set; }
        public int Tris { get; internal set; }
        public int Verts { get; internal set; }
        public IReadOnlyList<string> Slots { get; internal set; } = Array.Empty<string>();
        internal Dictionary<string, int> SlotTris { get; set; }

        public bool IsCard => Id.StartsWith("card_", StringComparison.Ordinal) || Family == "card";
        public bool IsLandmark => Id.StartsWith("landmark_", StringComparison.Ordinal) || Family == "landmark";
        public bool IsFarStandIn => Id.EndsWith("_far", StringComparison.Ordinal);
        public bool AllowedFar => IsFarStandIn || IsCard || IsLandmark;
        public bool IsPavement => Id.StartsWith("pavement", StringComparison.Ordinal) || Family == "pavement";
        public bool IsRoad => Id.StartsWith("road", StringComparison.Ordinal) || Family == "road";
        public bool IsFlat => IsPavement || IsRoad || Id == "driveway" || Id == "verge" || Family == "ground";

        public bool IsBuilding
        {
            get
            {
                if (Array.IndexOf(BuildingFamilies, Family) >= 0) return true;
                foreach (string prefix in BuildingPrefixes)
                    if (Id.StartsWith(prefix, StringComparison.Ordinal)) return true;
                return false;
            }
        }

        public int TrisIn(string slot)
        {
            if (SlotTris != null) return SlotTris.TryGetValue(slot, out int t) ? t : 0;
            int at = IndexOfSlot(slot);
            if (at < 0) return 0;
            int share = Tris / Slots.Count;
            return at == 0 ? share + Tris % Slots.Count : share;
        }

        public int VertsIn(string slot)
        {
            int tris = TrisIn(slot);
            if (Verts <= 0 || Tris <= 0) return tris * 3;
            return (int)Math.Ceiling(Verts * (double)tris / Tris);
        }

        int IndexOfSlot(string slot)
        {
            for (int i = 0; i < Slots.Count; i++)
                if (Slots[i] == slot) return i;
            return -1;
        }
    }

    public sealed class SurroundingsKit
    {
        readonly List<SurroundingsPiece> pieces = new List<SurroundingsPiece>();
        readonly Dictionary<string, SurroundingsPiece> byId = new Dictionary<string, SurroundingsPiece>(StringComparer.Ordinal);

        public IReadOnlyList<SurroundingsPiece> Pieces => pieces;

        public static SurroundingsKit FromJson(string manifestJson, string source = "kit.json") => new SurroundingsKit().Add(manifestJson, source);

        public bool Has(string id) => id != null && byId.ContainsKey(id);

        public SurroundingsPiece Piece(string id) => id != null && byId.TryGetValue(id, out var piece) ? piece : null;

        public SurroundingsKit Add(string manifestJson, string source = "kit.json")
        {
            var root = Json.Parse(manifestJson, source);
            string kit = root["kit"].String();
            string fbx = root["fbx"].String(null);
            foreach (var node in root["pieces"].Items)
            {
                var piece = Read(node, kit, fbx);
                if (byId.ContainsKey(piece.Id)) throw node.Error($"piece \"{piece.Id}\" is already in kit \"{byId[piece.Id].Kit}\"");
                byId[piece.Id] = piece;
                pieces.Add(piece);
            }
            return this;
        }

        static SurroundingsPiece Read(JsonNode node, string kit, string fbx)
        {
            string id = node["id"].String();
            var piece = new SurroundingsPiece
            {
                Id = id, Kit = kit, Family = node["family"].String(""), Fbx = node["fbx"].String(fbx),
                FbxObject = node["fbxObject"].String(id), Far = node["far"].String(null),
                Width = Positive(node, "width"), Depth = Positive(node, "depth"), Height = Positive(node, "height"),
                Tris = node["tris"].Int(), Verts = node["verts"].Int(0), Slots = node["slots"].Strings(),
            };
            if (piece.Slots.Count == 0) throw node.Error("slots must name at least one material slot");
            if (node.Has("slotTris"))
            {
                piece.SlotTris = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (string slot in node["slotTris"].Keys) piece.SlotTris[slot] = node["slotTris"][slot].Int();
            }
            return piece;
        }

        static float Positive(JsonNode node, string key)
        {
            float value = node[key].Float();
            if (!(value > 0f)) throw node[key].Error("must be more than 0");
            return value;
        }
    }
}
