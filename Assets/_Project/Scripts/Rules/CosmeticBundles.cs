using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    internal sealed class LookParts
    {
        public string Top, Headwear, Back = "Satchel";
        public bool Glasses;
        public string Bottoms, Gloves, Boots, HeadwearColour, FaceColour, BagColour;
        public string Gear, Skin;
    }

    public sealed class CosmeticBundle
    {
        public const string EmblemFolder = "UI/Shop/Looks/Emblems/";
        public string Id { get; }
        public string Name { get; }
        public string Collection { get; }
        public string Tagline { get; }
        public string Description { get; }
        public int Price { get; }
        public IReadOnlyList<string> Unlocks { get; }
        public string TopColour { get; }
        public string Emblem => EmblemFolder + Id;
        public string Signature => Exclusive[0];
        public IReadOnlyList<string> Exclusive { get; }
        readonly LookParts parts;

        internal CosmeticBundle(string id, string name, string collection, string tagline, string description, int price,
            string[] unlocks, string topColour, string[] exclusive, LookParts parts)
        {
            if (exclusive == null || exclusive.Length == 0) throw new ArgumentException("A look needs a signature piece.", nameof(exclusive));
            Id = id; Name = name; Collection = collection; Tagline = tagline; Description = description; Price = price;
            Unlocks = Array.AsReadOnly(unlocks); TopColour = topColour;
            Exclusive = Array.AsReadOnly(exclusive);
            this.parts = parts;
        }

        public bool OwnedBy(Career career) => career != null && Unlocks.All(id => career.Owns(IdKind(id), IdValue(id)));

        public int RemainingPrice(Career career) => Unlocks.Where(id => !career.Owns(IdKind(id), IdValue(id)))
            .Sum(id => Career.Shop.First(offer => offer.Id == id).Price);

        internal static string IdKind(string id) => id.Substring(0, id.IndexOf(':'));
        internal static string IdValue(string id) => id.Substring(id.IndexOf(':') + 1);

        public Outfit Apply(Outfit current, WardrobeCatalogue wardrobe)
        {
            var next = (current ?? wardrobe.Default).Clone();
            next.Pieces["Top"] = parts.Top; next.Pieces["Bottoms"] = "Joggers";
            next.Pieces["Gloves"] = "Mittens"; next.Pieces["Footwear"] = "Boots";
            next.Pieces["Headwear"] = parts.Headwear; next.Pieces["Back"] = parts.Back; next.Pieces["Badge"] = Signature;
            if (parts.Glasses) next.Pieces["Face"] = "Glasses"; else next.Pieces.Remove("Face");
            next.Colours["Top"] = TopColour; next.Colours["Bottoms"] = parts.Bottoms; next.Colours["Gloves"] = parts.Gloves;
            next.Colours["Footwear"] = parts.Boots; next.Colours["Headwear"] = parts.HeadwearColour;
            next.Colours["Face"] = parts.FaceColour; next.Colours["Back"] = parts.BagColour;
            if (parts.Gear != null) next.ItemSkins[parts.Gear] = parts.Skin;
            return wardrobe.Fits(next) ? next : null;
        }
    }

    public static class CosmeticBundles
    {
        public static IReadOnlyList<CosmeticBundle> All { get; } = Array.AsReadOnly(new[]
        {
            new CosmeticBundle("sunroom", "Sunny-side Club", "Sunroom Social", "Sun badge, cap and shades for a bright day",
                "Cap, glasses, a sunflower crewneck and the Sun badge", 0, Array.Empty<string>(), "sunflower", new[] { "SunBadge" },
                new LookParts { Top = "Crewneck", Headwear = "Cap", Glasses = true, Bottoms = "cream", Gloves = "putty", Boots = "mustard",
                    HeadwearColour = "sunflower", FaceColour = "gold", BagColour = "mustard" }),
            new CosmeticBundle("courier", "Mint Messenger", "Sunroom Social", "Stamp badge and a messenger bag for deliveries",
                "Mint hoodie, messenger bag and the Stamp badge", 0, Array.Empty<string>(), "mint", new[] { "StampBadge", "MessengerBag" },
                new LookParts { Top = "Hoodie", Headwear = "Hood", Back = "MessengerBag", Bottoms = "olive", Gloves = "charcoal", Boots = "classic",
                    HeadwearColour = "mint", FaceColour = "classic", BagColour = "olive" }),
            new CosmeticBundle("candy", "Sugar Rush", "Candy Carnival", "Sweet badge and a Candy BAT",
                "Pink hoodie, cherry glasses, Candy BAT and the Sweet badge", 400, new[] { "colour:bubblegum", "skin:Candy" }, "bubblegum", new[] { "SweetBadge" },
                new LookParts { Top = "Hoodie", Headwear = "Cap", Glasses = true, Bottoms = "plum", Gloves = "putty", Boots = "cherry",
                    HeadwearColour = "oat", FaceColour = "cherry", BagColour = "canvas", Gear = "BAT", Skin = "Candy" }),
            new CosmeticBundle("arcade", "Midnight Arcade", "Candy Carnival", "Pixel heart badge, headphones and an Arcade SHIELD",
                "Grape crewneck, headphones, shades, Arcade SHIELD and the Pixel heart badge", 550, new[] { "colour:grape", "skin:Arcade" }, "grape",
                new[] { "PixelBadge", "Headphones" },
                new LookParts { Top = "Crewneck", Headwear = "Headphones", Glasses = true, Bottoms = "charcoal", Gloves = "ink", Boots = "navy",
                    HeadwearColour = "mint", FaceColour = "sky", BagColour = "navy", Gear = "SHIELD", Skin = "Arcade" }),
            new CosmeticBundle("lantern", "Lantern Walker", "Lantern Festival", "Lantern badge and gold glasses for the festival",
                "Warm hoodie, gold glasses, satchel and the Lantern badge", 0, Array.Empty<string>(), "tangerine", new[] { "LanternBadge" },
                new LookParts { Top = "Hoodie", Headwear = "Hood", Glasses = true, Bottoms = "cocoa", Gloves = "sunflower", Boots = "tan",
                    HeadwearColour = "tomato", FaceColour = "gold", BagColour = "brick" }),
            new CosmeticBundle("winter", "Holly Housemate", "Winter House Party", "Holly badge and a bobble beanie for snow days",
                "Red hoodie, bobble beanie, forest boots and the Holly badge", 0, Array.Empty<string>(), "tomato", new[] { "HollyBadge", "Beanie" },
                new LookParts { Top = "Hoodie", Headwear = "Beanie", Bottoms = "forest", Gloves = "putty", Boots = "forest",
                    HeadwearColour = "oat", FaceColour = "gold", BagColour = "saddle" })
        });

        public static CosmeticBundle Find(string id) => All.FirstOrDefault(bundle => bundle.Id == id);

        public static CosmeticBundle GrantedBy(string pieceId) => All.FirstOrDefault(bundle => bundle.Exclusive.Contains(pieceId));

        public static Outfit WithItemSkin(Outfit current, string recipe, string skin, Career career, ItemCatalogue items)
        {
            if (current == null || career == null || !career.Owns("skin", skin) ||
                !items.TryGet(recipe, out var item) || !item.Enabled || !item.HasSkin(skin)) return null;
            var next = current.Clone();
            next.ItemSkins[item.Id] = skin;
            return next;
        }
    }
}
