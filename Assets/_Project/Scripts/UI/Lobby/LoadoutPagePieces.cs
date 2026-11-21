using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed partial class LoadoutPage
    {
        public const string NoPrefix = "No ", ColourPrefix = "Colour ", ScrollName = "Scroll", TilesName = "Tiles";
        public const string PiecePicture = "Piece picture", PieceFallback = "Piece fallback", NoPieceArt = "No piece";
        public const string CheckMark = "Check", LockMark = "Locked", NewMark = "New", BringsMark = "Brings", PricePill = "Price";
        public const string TriedRing = "Tried on", SoldPalette = "Top", NothingName = "None";
        public const float Tile = 196f, TileSpacing = 12f, SwatchSize = 46f, SwatchSpacing = 7f;
        const float ScrollGutter = 18f, PictureInset = 6f, MarkSize = 34f, MarkInset = 8f, MarkIcon = 18f;
        const float FallbackIcon = 96f, FallbackBlob = 124f, NoneIcon = 96f, NoneCross = 52f, NoneFade = .4f;
        const float PillWidth = 86f, PillHeight = 30f, BringsWidth = 58f, TagHeight = 44f, TagText = 16f;
        const int GridSide = 3, GridEdge = 6, SwatchRadius = 23;
        static readonly Vector4 MaskSpill = new Vector4(-6f, 0f, -6f, -4f);
        static readonly Vector2 TopRight = Vector2.one, TopLeft = new Vector2(0, 1), BottomLeft = Vector2.zero, BottomRight = new Vector2(1, 0);

        public static string TileName(string slot, string choice) => choice ?? NoPrefix + slot;

        RectTransform TileGrid(RectTransform view, RectTransform body)
        {
            lockerList = LobbyKit.Scroll(view, ScrollName, 0);
            lockerList.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(GridSide, GridSide, GridEdge, GridEdge);
            var scroll = lockerList.GetComponentInParent<ScrollRect>();
            AddScrollbar(view, scroll);
            if (scroll && scroll.TryGetComponent(out RectMask2D mask)) mask.padding = MaskSpill;
            float room = body.rect.width > 0 ? body.rect.width - ScrollGutter - GridSide * 2 : Tile * Columns + TileSpacing * (Columns - 1);
            float cell = Mathf.Min(Tile, Mathf.Floor((room - TileSpacing * (Columns - 1)) / Columns));
            var grid = LobbyKit.Grid(lockerList, TilesName, new Vector2(cell, cell), TileSpacing);
            var cells = grid.GetComponent<GridLayoutGroup>();
            cells.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            cells.constraintCount = Columns;
            lockerList.anchoredPosition = new Vector2(0, scrolled);
            return grid;
        }

        static void AddScrollbar(RectTransform host, ScrollRect scroll)
        {
            if (!scroll) return;
            ((RectTransform)scroll.transform).offsetMax = new Vector2(-ScrollGutter, 0);
            var track = LobbyKit.Rect(host, "Scroll position").Place(new Vector2(1, 0), Vector2.one, new Vector2(-8, 0), Vector2.zero);
            track.Paint(LobbyKit.Line, 4).raycastTarget = true;
            var handle = LobbyKit.Rect(track, "Handle").Fill();
            var ink = handle.Paint(LobbyKit.Cyan, 4);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle; bar.targetGraphic = ink;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        void PieceGrid(RectTransform view, RectTransform body)
        {
            var grid = TileGrid(view, body);
            string worn = Dressing.Worn(Wardrobe, Menu.Outfit, Category);
            string shown = Dressing.Worn(Wardrobe, Shown, Category);
            Selectable wornTile = null;
            foreach (string choice in Dressing.Choices(Wardrobe, Category))
            {
                bool owned = choice == null || Menu.Career.Owns(PieceKind, choice);
                if (OwnedOnly && !owned && choice != shown) continue;
                var tile = PieceTile(grid, Category, choice, choice == worn, choice == shown && choice != worn, owned);
                tiles.Add(tile);
                if (choice == shown) First = tile;
                if (choice == worn) wornTile = tile;
            }
            if (!First) First = wornTile ? wornTile : tiles.FirstOrDefault();
        }

        Button TileButton(Transform grid, string name, Action click)
        {
            var button = LobbyKit.Button(grid, name, LobbyKit.Card, click, TileRadius, LobbyKit.Line, 2);
            var press = button.GetComponent<LobbyPress>();
            press.Lift = 3f; press.Tilt = 0f;
            return button;
        }

        Button PieceTile(Transform grid, string slot, string choice, bool worn, bool tried, bool owned)
        {
            var button = TileButton(grid, TileName(slot, choice), () => Choose(slot, choice));
            var body = button.Body();
            var change = Dressing.Wear(Wardrobe, Shown, slot, choice);
            var sprite = choice == null ? null : Wreckabulary.UI.WardrobeArt.For(slot, choice);
            if (sprite) Picture(body, PiecePicture, sprite);
            else if (choice == null) NoneArt(body, slot);
            else FallbackArt(body, slot, choice, change.Outfit ?? Shown);
            if (worn) WornMark(body);
            else if (tried) TriedMark(body);
            if (!owned)
            {
                CornerDisc(body, LockMark, LobbyIcons.Lock, LobbyKit.Panel, LobbyKit.Navy, TopRight);
                var offer = Dressing.OfferFor(PieceKind + ":" + choice);
                if (offer != null) PriceMark(body, Menu.Career.PriceOf(offer));
            }
            if (choice != null && IsNew(PieceKind, choice)) CornerDisc(body, NewMark, LobbyIcons.New, LobbyKit.PrimaryFill, LobbyKit.PrimaryInk, TopLeft);
            if (change.PutOn.Count > 0 && Wardrobe.Piece(change.PutOn[0]) is WardrobePiece brought) BringsMarkFor(body, brought.Slot);
            string note = Note(choice, owned, change);
            string name = choice == null ? NothingName : Wardrobe.NameOf(choice);
            button.gameObject.AddComponent<LobbyHint>().Text = note == null ? name : name + Separator + note;
            string line = PieceLine(slot, choice, change.Outfit ?? Shown, note);
            string key = PieceKind + ":" + TileName(slot, choice);
            var face = button.FaceOf();
            button.GetComponent<LobbyPress>().Hot = hot =>
            {
                face.color = hot ? LobbyKit.Raised : LobbyKit.Card;
                Hover(key, () => change.Outfit, line, hot);
            };
            return button;
        }

        static Image Picture(RectTransform body, string name, Sprite sprite)
        {
            var image = LobbyKit.Rect(body, name).Place(Vector2.zero, Vector2.one, new Vector2(PictureInset, PictureInset),
                new Vector2(-PictureInset, -PictureInset)).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        static void NoneArt(RectTransform body, string slot)
        {
            var art = LobbyKit.Rect(body, NoPieceArt).Fill();
            var ink = LobbyKit.Navy;
            var faded = LobbyKit.Icon(art, LobbyIcons.ForSlot(slot), new Color(ink.r, ink.g, ink.b, NoneFade));
            faded.rectTransform.Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(NoneIcon, NoneIcon));
            LobbyKit.Icon(art, LobbyIcons.Close, ink).rectTransform.Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(NoneCross, NoneCross));
        }

        void FallbackArt(RectTransform body, string slot, string choice, Outfit wearing)
        {
            var art = LobbyKit.Rect(body, PieceFallback).Fill();
            var piece = Wardrobe.Piece(choice);
            var colour = piece != null && piece.TintMaterial != null ? Wardrobe.ColourFor(wearing, slot) : null;
            Blob(art, colour != null ? new Color(colour.R, colour.G, colour.B) : LobbyKit.Raised, FallbackBlob);
            LobbyKit.Icon(art, LobbyIcons.ForSlot(slot), LobbyKit.Navy).rectTransform.Pin(new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(FallbackIcon, FallbackIcon));
            TileTag(art, Wardrobe.NameOf(choice));
        }

        static RectTransform TileTag(RectTransform art, string line)
        {
            var tag = LobbyKit.Rect(art, Tag).Place(Vector2.zero, new Vector2(1, 0), new Vector2(6, 6), new Vector2(-6, 6 + TagHeight));
            var fill = LobbyKit.Panel; fill.a = .88f;
            tag.Paint(fill, 9).raycastTarget = false;
            var text = LobbyKit.Text(tag, line, TagText, LobbyKit.Cream, TextAlignmentOptions.Center);
            text.name = TagLine;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true; text.fontSizeMin = MinText; text.fontSizeMax = TagText;
            text.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 2), new Vector2(-8, -2));
            return tag;
        }

        static void WornMark(RectTransform body)
        {
            LobbyKit.Ring(body, LobbyKit.Selection, TileRadius, 3);
            CornerDisc(body, CheckMark, LobbyIcons.Check, LobbyKit.Selection, LobbyKit.OnSelection, TopRight);
        }

        static void TriedMark(RectTransform body) => LobbyKit.Ring(body, LobbyKit.Focus, TileRadius, 3).name = TriedRing;

        static RectTransform CornerDisc(RectTransform body, string name, string icon, Color fill, Color ink, Vector2 corner)
        {
            var offset = new Vector2(corner.x > .5f ? -MarkInset : MarkInset, corner.y > .5f ? -MarkInset : MarkInset);
            var disc = LobbyKit.Rect(body, name).Pin(corner, offset, new Vector2(MarkSize, MarkSize));
            LobbyKit.Face(disc, fill, Mathf.RoundToInt(MarkSize / 2f), LobbyKit.Line, 1);
            LobbyKit.Icon(disc, icon, ink).rectTransform.Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(MarkIcon, MarkIcon));
            return disc;
        }

        static void PriceMark(RectTransform body, int price)
        {
            var pill = LobbyKit.Row(body, PricePill, 4);
            pill.Pin(BottomRight, new Vector2(-MarkInset, MarkInset), new Vector2(PillWidth, PillHeight));
            LobbyKit.Face(pill, LobbyKit.Panel, Mathf.RoundToInt(PillHeight / 2f), LobbyKit.Line, 1);
            var row = pill.GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childForceExpandHeight = false;
            row.padding = new RectOffset(8, 10, 0, 0);
            LobbyKit.Coin(pill, 20);
            var text = LobbyKit.Display(pill, price.ToString("N0", CultureInfo.InvariantCulture), DetailText, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            text.Size(-1, 24);
        }

        static void BringsMarkFor(RectTransform body, string slot)
        {
            var mark = LobbyKit.Row(body, BringsMark, 2);
            mark.Pin(BottomLeft, new Vector2(MarkInset, MarkInset), new Vector2(BringsWidth, PillHeight));
            LobbyKit.Face(mark, LobbyKit.Panel, Mathf.RoundToInt(PillHeight / 2f), LobbyKit.Line, 1);
            var row = mark.GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childForceExpandHeight = false;
            LobbyKit.Icon(mark, LobbyIcons.Plus, LobbyKit.Navy).Size(14, 14);
            LobbyKit.Icon(mark, LobbyIcons.ForSlot(slot), LobbyKit.Navy).Size(22, 22);
        }

        string Note(string choice, bool owned, DressChange change)
        {
            if (!owned)
                return CosmeticBundles.GrantedBy(choice) is CosmeticBundle look ? "comes with " + look.Name : "in the shop";
            if (change.PutOn.Count > 0) return "puts on " + Names(change.PutOn);
            return change.TakenOff.Count > 0 ? "takes off " + Names(change.TakenOff) : null;
        }

        static string Names(IEnumerable<string> pieces) => string.Join(" and ", pieces.Select(Wardrobe.NameOf));

        string PieceLine(string slot, string choice, Outfit wearing, string note = null)
        {
            var parts = new List<string> { choice == null ? NothingName : Wardrobe.NameOf(choice) };
            var piece = Wardrobe.Piece(choice ?? "");
            if (piece != null && piece.TintMaterial != null)
            {
                string from = piece.ColourFrom ?? slot;
                var colour = Wardrobe.Colour(from, ColourIn(wearing, from) ?? "");
                if (colour != null) parts.Add(colour.Name);
                if (piece.ColourFrom != null) parts.Add("matches your " + piece.ColourFrom.ToLowerInvariant());
            }
            if (note != null) parts.Add(note);
            else if (tryOn != null && choice != null && !Menu.Career.Owns(PieceKind, choice)) parts.Add("trying on");
            return string.Join(Separator, parts);
        }

        static string ColourIn(Outfit outfit, string paletteSlot) => outfit.ColourOf(paletteSlot) ?? Wardrobe.Default.ColourOf(paletteSlot);

        void Choose(string slot, string choice)
        {
            Disarm();
            string name = TileName(slot, choice);
            if (choice != null && !Menu.Career.Owns(PieceKind, choice))
            {
                var tried = Dressing.Wear(Wardrobe, Shown, slot, choice);
                if (!tried.Fits) { Menu.Post(Clash); return; }
                StartTryOn(tried.Outfit, name);
                return;
            }
            bool same = Dressing.Worn(Wardrobe, Menu.Outfit, slot) == choice;
            if (same && tryOn == null) return;
            if (!same && !Commit(slot, choice)) return;
            if (tryOn != null && Dressing.Wear(Wardrobe, tryOn, slot, choice) is { Fits: true } also) tryOn = also.Outfit;
            Rebuild();
            Reselect(name);
        }

        bool Commit(string slot, string choice)
        {
            var change = Dressing.Wear(Wardrobe, Menu.Outfit, slot, choice);
            if (!change.Fits)
            {
                Menu.Post(Clash);
                return false;
            }
            if (Menu.Career.Missing(change.Outfit).Any(missing => missing.StartsWith(PieceKind + ":", StringComparison.Ordinal)))
            {
                Menu.Post(NotYours);
                return false;
            }
            Menu.Wear(change.Outfit);
            foreach (string put in change.PutOn)
                Menu.Post(Wardrobe.NameOf(put) + " put on to go with the " + Wardrobe.NameOf(choice) + ".");
            foreach (string off in change.TakenOff) Menu.Post(Wardrobe.NameOf(off) + " taken off.");
            return true;
        }

        public void PutOn(string slot, string choice, string reselect)
        {
            Disarm();
            if (!Commit(slot, choice == NoPrefix + slot ? null : choice)) return;
            Rebuild();
            Reselect(reselect);
        }

        bool LetterShown() => Wardrobe.Piece(Shown.PieceIn(Category) ?? "")?.Letter == true;

        bool PieceStripShown() => LetterShown() || Dressing.Tintable(Wardrobe, Shown, Category, out _);

        void PieceStrip(RectTransform strip)
        {
            if (LetterShown()) LetterStepper(strip);
            else if (Dressing.Tintable(Wardrobe, Shown, Category, out string paletteSlot)) ColourStrip(strip, Category, paletteSlot);
        }

        void ColourStrip(RectTransform strip, string slot, string paletteSlot)
        {
            var row = LobbyKit.Row(strip, "Colours", SwatchSpacing);
            row.Fill();
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            string worn = ColourIn(Menu.Outfit, paletteSlot), shown = ColourIn(Shown, paletteSlot);
            foreach (var colour in Wardrobe.Palettes[paletteSlot])
            {
                bool owned = paletteSlot != SoldPalette || Menu.Career.Owns(ColourKind, colour.Id);
                if (OwnedOnly && !owned && colour.Id != shown) continue;
                var swatch = Swatch(row, slot, paletteSlot, colour, colour.Id == shown, colour.Id == worn, owned);
                if (!stripFirst) stripFirst = swatch;
            }
        }

        Button Swatch(Transform row, string slot, string paletteSlot, Colourway colour, bool shown, bool worn, bool owned)
        {
            var button = LobbyKit.Button(row, ColourPrefix + colour.Id, LobbyKit.Navy, () => Paint(slot, paletteSlot, colour), SwatchRadius, null, 0);
            button.Size(SwatchSize, SwatchSize);
            var body = button.Body();
            SwatchFill(body, colour, SwatchRadius - 3);
            var press = button.GetComponent<LobbyPress>();
            press.HoverScale = 1.06f; press.Tilt = 0f; press.Lift = 0f;
            if (shown)
            {
                press.Scale = 1.06f;
                LobbyKit.Ring(body, worn ? LobbyKit.Selection : LobbyKit.Focus, SwatchRadius, 2);
            }
            var offer = owned ? null : Dressing.OfferFor(ColourKind + ":" + colour.Id);
            if (!owned) LobbyKit.Coin(body, 18).Pin(BottomRight, new Vector2(4, -4), new Vector2(18, 18));
            if (paletteSlot == SoldPalette && IsNew(ColourKind, colour.Id))
                CornerDisc(body, NewMark, LobbyIcons.New, LobbyKit.PrimaryFill, LobbyKit.PrimaryInk, TopLeft).localScale = Vector3.one * .6f;
            string price = offer != null ? Separator + Menu.Career.PriceOf(offer).ToString("N0", CultureInfo.InvariantCulture) + " coins" : "";
            button.gameObject.AddComponent<LobbyHint>().Text = colour.Name + price;
            string line = ColourLine(slot, paletteSlot, colour) + price;
            string key = ColourKind + ":" + paletteSlot + ":" + colour.Id;
            press.Hot = hot => Hover(key, () => Painted(Shown, slot, paletteSlot, colour.Id), line, hot);
            return button;
        }

        string ColourLine(string slot, string paletteSlot, Colourway colour)
        {
            string piece = Dressing.Worn(Wardrobe, Shown, slot);
            var parts = new List<string> { piece == null ? SlotTitle(slot) : Wardrobe.NameOf(piece), colour.Name };
            if (paletteSlot != slot) parts.Add("matches your " + paletteSlot.ToLowerInvariant());
            return string.Join(Separator, parts);
        }

        static void SwatchFill(RectTransform body, Colourway colour, int radius)
        {
            var fill = LobbyKit.Rect(body, "Fill").Place(Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3));
            fill.Paint(new Color(colour.R, colour.G, colour.B), radius).raycastTarget = false;
            var shine = LobbyKit.Rect(fill, "Shine").Pin(new Vector2(.32f, .72f), Vector2.zero, new Vector2(radius * .55f, radius * .42f));
            shine.Paint(new Color(1f, 1f, 1f, .67f), Mathf.Max(1, Mathf.RoundToInt(radius * .25f))).raycastTarget = false;
        }

        static Outfit Painted(Outfit outfit, string slot, string paletteSlot, string colourId)
        {
            if (Dressing.Tintable(Wardrobe, outfit, slot, out string own) && own == paletteSlot) return Dressing.Paint(Wardrobe, outfit, slot, colourId);
            if (Wardrobe.Colour(paletteSlot, colourId) == null) return null;
            var next = outfit.Clone();
            next.Colours[paletteSlot] = colourId;
            return Wardrobe.Fits(next) ? next : null;
        }

        void Paint(string slot, string paletteSlot, Colourway colour)
        {
            Disarm();
            string name = ColourPrefix + colour.Id;
            if (paletteSlot == SoldPalette && !Menu.Career.Owns(ColourKind, colour.Id))
            {
                var tried = Painted(Shown, slot, paletteSlot, colour.Id);
                if (tried != null) StartTryOn(tried, name);
                return;
            }
            bool same = ColourIn(Menu.Outfit, paletteSlot) == colour.Id;
            if (same && tryOn == null) return;
            if (!same)
            {
                var next = Painted(Menu.Outfit, slot, paletteSlot, colour.Id);
                if (next == null) return;
                Menu.Wear(next);
            }
            if (tryOn != null && Painted(tryOn, slot, paletteSlot, colour.Id) is Outfit also) tryOn = also;
            Rebuild();
            Reselect(name);
        }

        void LetterStepper(RectTransform strip)
        {
            var row = LobbyKit.Row(strip, "Letter", 10);
            row.Fill();
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            LobbyKit.SectionLabel(row, "Letter").Size(86, 28);
            var previous = LobbyKit.TextAction(row, "Letter previous", "‹", 30, () => StepLetter(-1));
            previous.Size(52, 48);
            LobbyKit.LetterTile(row, LetterBadge.For(Shown, 0), 44).name = "Letter tile";
            LobbyKit.TextAction(row, "Letter next", "›", 30, () => StepLetter(1)).Size(52, 48);
            var seat = LobbyKit.TextAction(row, "Letter seat", "Seat letter", DetailText, () => WearLetter(null, "Letter seat"), Shown.Letter == null);
            seat.Size(130, 44);
            RaiseText(seat);
            LobbyKit.Rect(row, "Gap").Size(0, 10, 1);
            stripFirst = previous;
        }

        void StepLetter(int step)
        {
            var letters = LetterBadge.Letters.ToList();
            int index = Mathf.Max(0, letters.IndexOf(LetterBadge.For(Shown, 0)));
            WearLetter(letters[(index + step + letters.Count) % letters.Count], step < 0 ? "Letter previous" : "Letter next");
        }

        void WearLetter(string letter, string reselect)
        {
            Disarm();
            var next = LetterBadge.With(Menu.Outfit, letter);
            if (next == null) return;
            if (next.Serialize() != Menu.Outfit.Serialize()) Menu.Wear(next);
            if (tryOn != null) tryOn = LetterBadge.With(tryOn, letter) ?? tryOn;
            Rebuild();
            Reselect(reselect);
        }
    }
}
