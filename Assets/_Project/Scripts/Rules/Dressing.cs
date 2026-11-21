using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public sealed class DressChange
    {
        static readonly IReadOnlyList<string> None = Array.Empty<string>();

        public Outfit Outfit { get; }
        public IReadOnlyList<string> PutOn { get; }
        public IReadOnlyList<string> TakenOff { get; }
        public bool Fits => Outfit != null;

        internal DressChange(Outfit outfit, IReadOnlyList<string> putOn, IReadOnlyList<string> takenOff)
        {
            Outfit = outfit;
            PutOn = putOn ?? None;
            TakenOff = takenOff ?? None;
        }
    }

    public static class Dressing
    {
        const string PieceKind = "piece", ColourKind = "colour", SkinKind = "skin", LookKind = "look";
        const char KindBreak = ':';

        public static string Worn(WardrobeCatalogue wardrobe, Outfit outfit, string slot)
        {
            if (wardrobe == null) throw new ArgumentNullException(nameof(wardrobe));
            if (outfit == null || slot == null) return null;
            return outfit.PieceIn(slot) ?? (wardrobe.FallbackSlots.Contains(slot) ? wardrobe.Default.PieceIn(slot) : null);
        }

        public static IReadOnlyList<string> Choices(WardrobeCatalogue wardrobe, string slot)
        {
            if (wardrobe == null) throw new ArgumentNullException(nameof(wardrobe));
            var choices = new List<string>();
            if (slot == null || !wardrobe.Slots.Contains(slot)) return choices;
            if (!wardrobe.RequiredSlots.Contains(slot)) choices.Add(null);
            foreach (var piece in wardrobe.PiecesFor(slot)) choices.Add(piece.Id);
            return choices;
        }

        public static DressChange Wear(WardrobeCatalogue wardrobe, Outfit outfit, string slot, string pieceOrNull)
        {
            if (wardrobe == null) throw new ArgumentNullException(nameof(wardrobe));
            if (outfit == null || slot == null || !wardrobe.Slots.Contains(slot)) return new DressChange(null, null, null);
            var piece = pieceOrNull == null ? null : wardrobe.Piece(pieceOrNull);
            if (pieceOrNull != null && (piece == null || piece.Slot != slot)) return new DressChange(null, null, null);
            var next = outfit;
            foreach (string need in piece?.Requires ?? new List<string>())
            {
                var needed = wardrobe.Piece(need);
                if (needed == null || next.PieceIn(needed.Slot) == need) continue;
                next = wardrobe.Wear(next, needed.Slot, need) ?? next;
            }
            next = wardrobe.Wear(next, slot, pieceOrNull);
            if (next == null) return new DressChange(null, null, null);
            var putOn = new List<string>();
            var takenOff = new List<string>();
            foreach (string other in wardrobe.Slots)
            {
                if (other == slot) continue;
                string before = outfit.PieceIn(other), after = next.PieceIn(other);
                if (before == after) continue;
                if (after != null) putOn.Add(after);
                else takenOff.Add(before);
            }
            return new DressChange(next, putOn, takenOff);
        }

        public static bool Tintable(WardrobeCatalogue wardrobe, Outfit outfit, string slot, out string paletteSlot)
        {
            paletteSlot = null;
            var piece = wardrobe.Piece(Worn(wardrobe, outfit, slot) ?? "");
            if (piece == null || piece.TintMaterial == null) return false;
            string from = piece.ColourFrom ?? slot;
            if (!wardrobe.Palettes.ContainsKey(from)) return false;
            paletteSlot = from;
            return true;
        }

        public static Outfit Paint(WardrobeCatalogue wardrobe, Outfit outfit, string slot, string colourId)
        {
            if (!Tintable(wardrobe, outfit, slot, out string paletteSlot)) return null;
            if (colourId == null || wardrobe.Colour(paletteSlot, colourId) == null) return null;
            var next = outfit.Clone();
            next.Colours[paletteSlot] = colourId;
            if (paletteSlot == slot && next.PieceIn(slot) == null && Worn(wardrobe, outfit, slot) is string piece)
                next.Pieces[slot] = piece;
            return wardrobe.Fits(next) ? next : null;
        }

        public static ShopOffer OfferFor(string missingId)
        {
            if (string.IsNullOrEmpty(missingId)) return null;
            int split = missingId.IndexOf(KindBreak);
            if (split <= 0) return null;
            string kind = missingId.Substring(0, split), value = missingId.Substring(split + 1);
            if (kind == PieceKind)
                return CosmeticBundles.GrantedBy(value) is CosmeticBundle look ? Offer(LookKind + KindBreak + look.Id) : null;
            return kind == ColourKind || kind == SkinKind || kind == LookKind ? Offer(missingId) : null;
        }

        public static IReadOnlyList<ShopOffer> Needed(Career career, Outfit outfit)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            var offers = new List<ShopOffer>();
            var covered = new HashSet<string>(StringComparer.Ordinal);
            foreach (string missing in career.Missing(outfit))
            {
                var offer = OfferFor(missing);
                if (offer == null || covered.Contains(offer.Id)) continue;
                offers.Add(offer);
                covered.Add(offer.Id);
                if (offer.Kind != LookKind || !(CosmeticBundles.Find(offer.Value) is CosmeticBundle look)) continue;
                foreach (string unlock in look.Unlocks) covered.Add(unlock);
            }
            return offers;
        }

        static ShopOffer Offer(string id) => Career.Shop.FirstOrDefault(offer => offer.Id == id);
    }
}
