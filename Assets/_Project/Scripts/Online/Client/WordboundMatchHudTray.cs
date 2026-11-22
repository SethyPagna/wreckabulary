using System;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.UI;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundMatchHud
    {
        const string SpellWord = "Spell", MenuWord = "Menu", HandGlyph = "hand";
        const float PlateSize = 40f, PlateGap = 12f, PlateTilt = 5f, PlateMaxWidth = 420f, PlateShare = .42f, PlateMinRoom = 160f,
            PlateTagHeight = 18f, PlateTitleHeight = 30f, PlateRowGap = 2f, PlateLift = 1f;
        const float VitalsCross = 22f, VitalsCrossBar = 7f, VitalsBarX = 30f, VitalsBarWidth = 160f, VitalsBarHeight = 24f,
            VitalsNumberPad = 5f, HealthFloor = 1f, KeepsakeGap = 6f;
        const float TrayTileWidth = 36f, TrayTileHeight = 39f, TrayTileStepX = 41f, TrayTileStepY = 46f, TrayTileEdge = 2f,
            TrayHandGap = 16f, TrayKeyGap = 6f, TrayKeySpacing = 18f, HandWidth = 117f, HandHeight = 55f, HandGapY = 8f,
            HandPicture = 34f, HandPictureX = 22f, HandWordX = 60f, HandKeyInset = 7f, HandKeyHeight = 18f, HandCapInset = 5f,
            HandCapPad = 4f, HandGlyphSize = 24f,
            HandGlyphShift = 5f, ActionGap = 8f, ActionShare = .6f, ActionMaxWidth = 760f;
        const int PlateTitleFont = 24, PlateTitleMinFont = 18, PlateTagFont = 13, PlateDetailFont = 15, PlateBrandFont = 26,
            VitalsNumberFont = 20, TrayTileFont = 22, HandWordFont = 13, HandWordMinFont = 10, HandKeyFont = 12,
            TrayCells = 10, TrayColumns = 5, HandSlots = 2;
        static readonly Color HudPlateShadow = KitHex(0x0b1f1f, .35f);
        static readonly string[] HandSlotNumbers = { "1", "2" };
        readonly GUIContent vitalsContent = new GUIContent(), keepsakeContent = new GUIContent("Carrying the keepsake"),
            brandContent = new GUIContent("W<size=14><color=#B86647>!</color></size>");
        readonly GUIContent[] handCaps = { new GUIContent(), new GUIContent() }, handWordContents = { new GUIContent(), new GUIContent() };
        readonly string[] handWords = new string[HandSlots], handWordFitted = new string[HandSlots];
        readonly int[] handWordFonts = new int[HandSlots];
        readonly KeyPrompt actionPrompt = new KeyPrompt(), spellKeys = new KeyPrompt(), menuKeys = new KeyPrompt();
        GUIStyle kitHandKey;
        MatchSnapshot handFrame, vitalsFrame;
        MatchActorState vitalsActor;
        HealthBand vitalsBand;
        PlayerInventory handInventory;
        (bool Pad, int Version) handCapKey = (false, -1);
        LetterBag reservedBag;
        string reservedText = "", plateTitleText;
        int statusTagRow = -1, vitalsReading = int.MinValue, plateTitleFont = PlateTitleFont, plateTitleGeneration = -1;
        float healthPeak, healthMax, vitalsShare, plateTitleRoom = -1f;
        bool trayKeepsake;
        Rect plateRect, vitalsRect, healthBarRect, trayRect, actionRect;

        void PrepareTray(TeamProgress progress)
        {
            if (inventory == null) { tileText = ""; tileBag = null; reservedText = ""; reservedBag = null; }
            else
            {
                if (tileBag == null || !inventory.Letters.Equals(tileBag)) { tileBag = new LetterBag(inventory.Letters); tileText = tileBag.ToString(); }
                if (reservedBag == null || !inventory.Reserved.Equals(reservedBag)) { reservedBag = new LetterBag(inventory.Reserved); reservedText = reservedBag.ToString(); }
            }
            RefreshHands();
            trayKeepsake = progress != null && !string.IsNullOrEmpty(actorId) && progress.KeepsakeCarrier == actorId;
            PrepareVitals();
            var key = (view && view.UsesGamepad, KeyBindings.Version);
            if (key.Equals(handCapKey)) return;
            handCapKey = key;
            for (int slot = 0; slot < HandSlots; slot++) handCaps[slot].text = HandCap(slot);
        }

        void PrepareVitals()
        {
            if (actor == null || ReferenceEquals(vitalsFrame, frame) && ReferenceEquals(vitalsActor, actor)) return;
            vitalsFrame = frame;
            vitalsActor = actor;
            healthPeak = Mathf.Max(healthPeak, actor.Health, HealthFloor);
            var own = ProgressOf(actorId);
            bool knocked = own != null && own.Eliminated, alive = actor.Health > 0f && !knocked, waiting = knocked && own.RespawnAt >= 0;
            float respawn = waiting ? Mathf.Max(0f, (own.RespawnAt - frame.Progress.Tick) / (float)ModeDefinition.TickRate) : 0f;
            vitalsShare = alive ? SeatVitals.Share(actor.Health, healthMax > 0f ? healthMax : healthPeak) : 0f;
            vitalsBand = SeatVitals.Band(vitalsShare);
            int reading = SeatVitals.Reading(actor.Health, alive, waiting, respawn, true);
            if (reading == vitalsReading) return;
            vitalsReading = reading;
            vitalsContent.text = SeatVitals.Label(reading);
        }

        void RefreshHands()
        {
            if (ReferenceEquals(handFrame, frame) && ReferenceEquals(handInventory, inventory)) return;
            handFrame = frame;
            handInventory = inventory;
            for (int slot = 0; slot < HandSlots; slot++) handWords[slot] = null;
            if (frame == null || inventory == null || inventory.Slots == null) return;
            var held = frame.Economy.Items;
            for (int slot = 0; slot < HandSlots && slot < inventory.Slots.Length; slot++)
                for (int i = 0; i < held.Count; i++)
                {
                    var item = held[i];
                    if (item.Id != inventory.Slots[slot] || item.State != ItemState.Held || item.HolderId != seat) continue;
                    handWords[slot] = item.Word;
                    break;
                }
        }

        string HandCap(int slot)
        {
            string key = HandKey(slot);
            if (key.Length == 0) return slot < HandSlotNumbers.Length ? HandSlotNumbers[slot] : "";
            return view.UsesGamepad ? ControlHints.PadHand(slot) : key;
        }

        Rect DrawTitlePlate(float width)
        {
            int rows = statusPanel.Count;
            if (rows == 0 || kitTitle == null) return default;
            float left = EyeMargin, top = EyeMargin, textX = left + PlateSize + PlateGap;
            float room = Mathf.Max(PlateMinRoom, Mathf.Min(PlateMaxWidth, (width - EyeMargin * 2f) * PlateShare) - PlateSize - PlateGap);
            PaintBrand(new Rect(left, top, PlateSize, PlateSize));
            float y = top - PlateLift, used = 0f;
            bool tagged = statusTagRow > 0 && statusTagRow < rows;
            if (tagged)
            {
                var tag = statusPanel.Row(statusTagRow);
                KitInk(new Rect(textX, y, room, PlateTagHeight), tag, kitTag, HudGold);
                used = Mathf.Min(room, kitTag.CalcSize(tag).x);
                y += PlateTagHeight;
            }
            var title = statusPanel.Row(0);
            FitTitle(title, room);
            KitInk(new Rect(textX, y, room, PlateTitleHeight), title, kitTitle, HudOnGame);
            used = Mathf.Max(used, Mathf.Min(room, kitTitle.CalcSize(title).x));
            y += PlateTitleHeight;
            for (int i = 1; i < rows; i++)
            {
                if (tagged && i == statusTagRow) continue;
                var row = statusPanel.Row(i);
                float high = kitDetail.CalcHeight(row, room);
                KitInk(new Rect(textX, y, room, high), row, kitDetail, HudOnGame);
                used = Mathf.Max(used, Mathf.Min(room, kitDetail.CalcSize(row).x));
                y += high + PlateRowGap;
            }
            var area = new Rect(left, top, textX - left + used, Mathf.Max(PlateSize, y - top));
            if (Painting) plateRect = area;
            return area;
        }

        void FitTitle(GUIContent title, float room)
        {
            if (plateTitleGeneration == kitGeneration && string.Equals(title.text, plateTitleText, StringComparison.Ordinal) &&
                Mathf.Approximately(room, plateTitleRoom)) { kitTitle.fontSize = plateTitleFont; return; }
            plateTitleGeneration = kitGeneration; plateTitleText = title.text; plateTitleRoom = room;
            int size = PlateTitleFont;
            kitTitle.fontSize = size;
            while (size > PlateTitleMinFont && kitTitle.CalcSize(title).x > room) kitTitle.fontSize = --size;
            plateTitleFont = size;
        }

        void PaintBrand(Rect plate)
        {
            if (!Painting) return;
            var matrix = GUI.matrix;
            try
            {
                GUIUtility.RotateAroundPivot(PlateTilt, plate.center);
                KitFill(new Rect(plate.x, plate.y + KitDrop, plate.width, plate.height), kitLargeShape, HudPlateShadow);
                KitFill(plate, kitLargeShape, HudBrand);
                KitPlain(new Rect(plate.x - KitOutline, plate.y, plate.width, plate.height), brandContent, kitBrand, HudInk);
            }
            finally { GUI.matrix = matrix; }
        }

        Rect DrawVitals(float height)
        {
            if (actor == null || kitNumber == null) return default;
            float left = EyeMargin, bottom = height - EyeMargin;
            var bar = SeatStripShown ? default : new Rect(left + VitalsBarX, bottom - VitalsBarHeight, VitalsBarWidth, VitalsBarHeight);
            var area = new Rect(left, bottom, 0f, 0f);
            if (bar.width > 0f)
            {
                PaintCross(new Vector2(left + VitalsCross * .5f, bar.center.y));
                PaintVitalsBar(bar, vitalsContent, vitalsShare, vitalsBand, kitNumber, VitalsNumberPad);
                area = Rect.MinMaxRect(left, bar.yMin, bar.xMax, bottom);
            }
            if (trayKeepsake)
            {
                var chip = new Rect(left, area.yMin - KeepsakeGap - PlateTagHeight, Mathf.Ceil(kitTag.CalcSize(keepsakeContent).x) + KitOutline * 2f, PlateTagHeight);
                KitInk(chip, keepsakeContent, kitTag, HudGold);
                area = Rect.MinMaxRect(left, chip.yMin, Mathf.Max(area.xMax, chip.xMax), bottom);
            }
            if (Painting) { vitalsRect = area; healthBarRect = bar; }
            return area;
        }

        static void PaintVitalsBar(Rect bar, GUIContent text, float share, HealthBand band, GUIStyle style, float pad)
        {
            KitFlat(bar, HudBarTrack);
            var number = new Rect(bar.x + pad, bar.y, bar.width - pad * 2f, bar.height);
            KitPlain(number, text, style, HudOnGame);
            var fill = new Rect(bar.x, bar.y, Mathf.Round(bar.width * share), bar.height);
            KitFlat(fill, BandColour(band));
            if (!Painting || fill.width <= 0f || string.IsNullOrEmpty(text.text)) return;
            GUI.BeginGroup(fill);
            try { KitPlain(new Rect(number.x - fill.x, number.y - fill.y, number.width, number.height), text, style, HudInk); }
            finally { GUI.EndGroup(); }
        }

        static Color BandColour(HealthBand band) => band switch
        {
            HealthBand.Critical => HudCritical, HealthBand.Hurt => HudAmber, _ => HudOnGame
        };

        static void PaintCross(Vector2 middle)
        {
            var across = new Rect(middle.x - VitalsCross * .5f, middle.y - VitalsCrossBar * .5f, VitalsCross, VitalsCrossBar);
            var down = new Rect(middle.x - VitalsCrossBar * .5f, middle.y - VitalsCross * .5f, VitalsCrossBar, VitalsCross);
            KitFlat(new Rect(across.x, across.y + KitDrop, across.width, across.height), HudShade);
            KitFlat(new Rect(down.x, down.y + KitDrop, down.width, down.height), HudShade);
            KitFlat(across, HudOnGame);
            KitFlat(down, HudOnGame);
        }

        static void KitFlat(Rect area, Color colour)
        {
            if (!Painting || area.width <= 0f || area.height <= 0f) return;
            Color tint = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = tint;
        }

        static float TilesWidth => TrayColumns * TrayTileStepX - (TrayTileStepX - TrayTileWidth);

        Rect DrawTray(float width, float height)
        {
            if (actor == null || inventory == null || inventory.Slots == null || kitTile == null) return default;
            float trayWidth = TilesWidth + TrayHandGap + HandWidth;
            float left = Mathf.Round((width - trayWidth) * .5f), bottom = height - EyeMargin;
            float handsTop = bottom - HandHeight * 2f - HandGapY;
            float tilesTop = bottom - CapHeight - TrayKeyGap - TrayTileEdge - TrayTileHeight - TrayTileStepY;
            PaintTiles(left, tilesTop);
            float handsLeft = left + TilesWidth + TrayHandGap;
            for (int slot = 0; slot < HandSlots && slot < inventory.Slots.Length; slot++)
                PaintHand(slot, new Rect(handsLeft, handsTop + slot * (HandHeight + HandGapY), HandWidth, HandHeight));
            float keys = bottom - CapHeight * .5f, spell = PaintInline(spellKeys, left, keys);
            PaintInline(menuKeys, left + (spell > 0f ? spell + TrayKeySpacing : 0f), keys);
            float top = Mathf.Min(tilesTop, handsTop);
            var area = new Rect(left, top, trayWidth, bottom - top);
            if (Painting) trayRect = area;
            return area;
        }

        void PaintTiles(float left, float top)
        {
            int held = tileText.Length, reserved = reservedText.Length;
            var (shown, extra) = TileSplit(held + reserved, TrayCells);
            if (extra != extraShown) { extraShown = extra; extraContent.text = "+" + extra; }
            for (int i = 0; i < TrayCells; i++)
            {
                var cell = new Rect(left + i % TrayColumns * TrayTileStepX, top + i / TrayColumns * TrayTileStepY, TrayTileWidth, TrayTileHeight);
                if (extra > 0 && i == TrayCells - 1) PaintTile(cell, extraContent, HudTile, HudInk);
                else if (i < held && i < shown) PaintTile(cell, LetterContent(tileText[i]), HudTile, HudInk);
                else if (i < shown) PaintTile(cell, LetterContent(reservedText[i - held]), HudReserved, HudReservedInk);
                else { KitFill(cell, kitSmallShape, HudEmptyCell); KitFill(cell, kitSmallRim, HudCellRim); }
            }
        }

        void PaintTile(Rect cell, GUIContent letter, Color face, Color ink)
        {
            KitFill(new Rect(cell.x, cell.y + TrayTileEdge, cell.width, cell.height), kitSmallShape, HudTileEdge);
            KitFill(cell, kitSmallShape, face);
            KitPlain(cell, letter, kitTile, ink);
        }

        void PaintHand(int slot, Rect area)
        {
            bool active = actor.EquippedSlot == slot;
            KitFill(area, kitLargeShape, active ? HudGlassActive : HudGlass);
            KitFill(area, kitLargeRim, active ? HudGlassEdge : HudGlassIdle);
            var cap = handCaps[slot];
            if (cap.text.Length != 0)
            {
                var key = new Rect(area.x + HandCapInset, area.y + HandCapInset, Mathf.Max(HandKeyHeight, Mathf.Ceil(kitHandKey.CalcSize(cap).x) + HandCapPad * 2f), HandKeyHeight);
                KitFill(key, kitSmallShape, HudKeyPill);
                KitPlain(key, cap, kitHandKey, HudOnGame);
            }
            string word = handWords[slot];
            if (word == null)
            {
                var glyph = new Rect(area.center.x + HandGlyphShift - HandGlyphSize * .5f, area.center.y - HandGlyphSize * .5f, HandGlyphSize, HandGlyphSize);
                KitImage(glyph, KitGlyph(HandGlyph), HudEmptyHand);
                return;
            }
            var picture = OriginalItemArt.Find(word);
            if (picture) KitImage(new Rect(area.x + HandPictureX, area.center.y - HandPicture * .5f, HandPicture, HandPicture), picture, Color.white);
            var label = new Rect(area.x + HandWordX, area.y, area.width - HandWordX - HandKeyInset, area.height);
            FitHandWord(slot, word, label.width);
            KitInk(label, handWordContents[slot], kitWord, HudOnGame);
        }

        void FitHandWord(int slot, string word, float room)
        {
            var content = handWordContents[slot];
            if (!string.Equals(handWordFitted[slot], word, StringComparison.Ordinal))
            {
                handWordFitted[slot] = word;
                content.text = word;
                int size = HandWordFont;
                kitWord.fontSize = size;
                while (size > HandWordMinFont && kitWord.CalcSize(content).x > room) kitWord.fontSize = --size;
                handWordFonts[slot] = size;
            }
            kitWord.fontSize = handWordFonts[slot];
        }

        Rect DrawActionChip(float width, float height, Rect tray)
        {
            if (actionPrompt.Count == 0) return default;
            var size = MeasurePrompt(actionPrompt, Mathf.Min(ActionMaxWidth, width * ActionShare));
            float bottom = tray.height > 0f ? tray.yMin - ActionGap : height - EyeMargin;
            var area = new Rect(Mathf.Round((width - size.x) * .5f), bottom - size.y, size.x, size.y);
            PaintPromptAt(actionPrompt, area);
            if (Painting) actionRect = area;
            return area;
        }
    }
}
