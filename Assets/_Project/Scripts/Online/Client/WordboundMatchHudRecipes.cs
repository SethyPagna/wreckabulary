using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.UI;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundMatchHud
    {
        const float RecipeCardGap = 8f, RecipeCardMin = 96f, RecipeCardPad = 6f, RecipeCardInner = 6f, RecipePictureShare = .5f,
            RecipePictureMin = 36f, RecipePictureMax = 64f, RecipeTileMax = 22f, RecipeTileGap = 2f, RecipeTileAspect = 1.2f,
            RecipeTileFontShare = .62f, RecipeScrollRoom = 20f, RecipeMark = 16f, RecipeLetterTile = 22f, RecipeLetterHeight = 26f,
            RecipeCraftMin = 150f, RecipeCraftShare = .4f, RecipeCraftPad = 48f;
        const int RecipeColumnsMax = 5, RecipeTileMinFont = 10, RecipeTileMaxFont = 16, RecipeLetterFont = 15;
        static readonly Color RecipeTileMissing = KitHex(0x3b2614, .10f), RecipeTileMissingRim = KitHex(0x3b2614, .35f),
            RecipeTileMissingInk = KitHex(0x3b2614, .55f);
        string selectedRecipe, recipeContext, menuLetterText = "", recipeLineRecipe;
        int recipeColumns = 1, recipeStyleGeneration = -1, recipeKitGeneration = -1, recipeLineGeneration = -1;
        bool recipeScrollActive, recipeLineCrafting;
        float recipeContentBottom, recipeLineWidth = -1f;
        readonly int[] recipeUsed = new int[LetterBag.Alphabet];
        readonly GUIContent recipeLineContent = new GUIContent(), recipeLettersLabel = new GUIContent("Letters"),
            recipeNoLetters = new GUIContent("No letters yet"), recipeCraftLabel = new GUIContent("› Spell");
        GUIStyle recipeTileStyle, recipeLetterStyle, recipeLineStyle;
        LetterBag menuLetterBag;
        List<RecipeCard> recipeLineCards;
        Texture2D recipeCheck;
        Rect recipeViewportRect, recipeDetailRect, recipeReturnRect, recipeCraftRect, recipeFocusRect;

        bool RecipeVisible(RecipeCard card) => card.Item != null &&
            card.Item.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        RecipeCard SelectedRecipe()
        {
            foreach (var card in recipeCards)
                if (card.Item.Id == selectedRecipe && RecipeVisible(card)) return card;
            return default;
        }

        void NormalizeRecipeSelection()
        {
            if (SelectedRecipe().Item != null) return;
            selectedRecipe = null;
            foreach (var card in recipeCards)
                if (RecipeVisible(card)) { selectedRecipe = card.Item.Id; break; }
        }

        void SelectRecipe(string id)
        {
            foreach (var card in recipeCards)
                if (card.Item.Id == id && RecipeVisible(card)) { selectedRecipe = id; return; }
        }

        void PrepareRecipeMenu()
        {
            NormalizeRecipeSelection();
            if (search.Length != 0 || inventory == null || inventory.Craft != null) return;
            var suggestion = RecipeSuggestion();
            if (suggestion.Recipe == null || suggestion.Context == recipeContext) return;
            if (!recipeCards.Any(card => card.Item.Id == suggestion.Recipe && RecipeVisible(card))) return;
            SelectRecipe(suggestion.Recipe);
            recipeContext = suggestion.Context;
            revealMenuSelection = true;
        }

        (string Context, string Recipe) RecipeSuggestion()
        {
            if (course == null || frame == null || mode == null || course.Play.ModeId != mode.Id ||
                frame.Binding.ModeHash != mode.Hash || frame.Binding.ManifestHash != course.Hash ||
                frame.Binding.CourseHash != course.SourceHash || frame.Progress.Migrating || frame.Progress.Result != null)
                return default;
            var player = frame.Progress.Players.SingleOrDefault(p => p.Id == actorId);
            if (player == null || !player.Connected || player.Returned || player.Eliminated ||
                !frame.Binding.Seats.Any(s => s.PlayerId == actorId && s.Team == player.Team)) return default;
            ResolvedCoursePhysicalSite site = null;
            if (mode.Id == "Freestyle")
            {
                site = WordboundPracticePolicy.Selected(course, frame.Practice);
                if (site == null || WordboundPracticeGuidePlan.Completed(frame, site))
                    site = WordboundPracticeGuidePlan.Nearest(course, frame, actorId, 20);
                if (site == null || WordboundPracticeGuidePlan.Completed(frame, site)) return default;
            }
            else if (mode.IsRace)
            {
                var progress = frame.Progress.Teams.SingleOrDefault(t => t.Team == player.Team);
                if (progress == null || progress.Finished || progress.Lap < 0 || progress.Lap >= course.Laps.Count ||
                    progress.NextCheckpoint < 0 || progress.NextCheckpoint > course.Laps[progress.Lap].Checkpoints.Count)
                    return default;
                var lap = course.Laps[progress.Lap];
                foreach (var checkpoint in lap.Checkpoints.Skip(progress.NextCheckpoint))
                {
                    foreach (string id in checkpoint.PuzzleIds.Where(id => !progress.CompletedPuzzles.Contains(id)))
                    {
                        site = course.PhysicalSites.FirstOrDefault(s => s.Lap == lap.Number && s.PuzzleId == id && s.CheckpointId == checkpoint.Id);
                        if (site != null) break;
                    }
                    if (site != null) break;
                }
            }
            if (site == null) return default;
            var puzzle = course.Puzzles.SingleOrDefault(p => p.Id == site.PuzzleId);
            if (puzzle == null || !mode.Recipes.Contains(puzzle.RecipeId)) return default;
            string recipe = puzzle.RecipeId;
            if (puzzle.PresetId == "CollectLetters" && mode.Recipes.Contains(site.ToolRecipeId) &&
                !frame.Economy.Items.Any(held => held.Word == site.ToolRecipeId && held.State == ItemState.Held &&
                    held.HolderId == seat && held.Durability > 0 && !held.Spent))
            {
                var sources = course.Placements.Where(p => p.ZoneId == site.PropSourceZoneId &&
                    (p.TeamIndex < 0 || p.TeamIndex == player.Team)).Select(p => p.Id).ToArray();
                if (frame.Bodies.Any(b => b.Kind == WorldBodyKind.Item && b.Active && sources.Contains(b.SourceId)))
                    recipe = site.ToolRecipeId;
            }
            return (course.Hash + ":" + player.Team + ":" + site.Hash + ":" + recipe, recipe);
        }

        bool CanCraftRecipe(RecipeCard card) => card.Item != null && RecipeVisible(card) &&
            controller && controller.Fault == "" && controller.IsPresentationReady && !controller.IsSimulationPaused &&
            frame != null && frame.Progress.Result == null && actor != null && actor.Health > 0 &&
            inventory != null && inventory.CanAct && inventory.Craft == null && card.Craftable &&
            inventory.Letters.Contains(card.Item.Letters);

        void CraftSelectedRecipe()
        {
            var card = SelectedRecipe();
            if (CanCraftRecipe(card) && Send(WordboundAction.BeginCraft, value: card.Item.Id)) Resume();
        }

        string RecipeDetail(RecipeCard card)
        {
            if (card.Item == null) return "Choose a recipe";
            if (!card.Craftable) return card.Item.Id + " · Need " + MissingInWordOrder(card.Item.Id, card.Missing);
            if (inventory?.Craft != null) return card.Item.Id + " · Spelling…";
            return card.Item.Id;
        }

        void RecipeWorkspace(Rect body)
        {
            PrepareKit();
            PrepareRecipeStyles();
            float rowHeight = ui.RowHeight + 4;
            var detail = new Rect(body.x, body.yMax - rowHeight, body.width, rowHeight);
            float craftWide = Mathf.Min(body.width * RecipeCraftShare, Mathf.Max(RecipeCraftMin, ui.SingleLine.CalcSize(recipeCraftLabel).x + RecipeCraftPad));
            var craft = new Rect(detail.xMax - craftWide, detail.y, craftWide, detail.height);
            var line = new Rect(detail.x, detail.y, Mathf.Max(1f, craft.x - detail.x - ui.Gap * 2f), detail.height);
            var list = new Rect(body.x, body.y, body.width, Mathf.Max(1f, detail.y - body.y - ui.Gap));
            if (Event.current.type == EventType.Repaint) { recipeContentBottom = 0f; recipeFocusRect = default; }

            GUILayout.BeginArea(list);
            recipeScroll = GUILayout.BeginScrollView(recipeScroll);
            recipeScrollActive = true;
            EquipmentMenu();
            GUI.enabled = true;
            GUILayout.Space(ui.Gap);
            RecipeLetters(Mathf.Max(1f, list.width - RecipeScrollRoom));
            GUILayout.Space(ui.Gap);
            RecipeGrid(list.width);
            recipeScrollActive = false;
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            GUILayout.BeginArea(line);
            RecipeLine(new Rect(0f, 0f, line.width, line.height));
            GUILayout.EndArea();
            GUILayout.BeginArea(craft);
            RecipeSelectionDetail();
            GUILayout.EndArea();
            if (Event.current.type == EventType.Repaint)
            {
                recipeViewportRect = list;
                recipeViewportHeight = Mathf.Max(30, list.height);
                recipeDetailRect = detail;
                recipeCraftRect = craft;
            }
        }

        void PrepareRecipeStyles()
        {
            if (recipeStyleGeneration == ui.Generation && recipeKitGeneration == kitGeneration && recipeTileStyle != null) return;
            recipeStyleGeneration = ui.Generation;
            recipeKitGeneration = kitGeneration;
            recipeTileStyle = KitText(KitBlack, RecipeTileMaxFont, TextAnchor.MiddleCenter);
            recipeLetterStyle = KitText(KitBlack, RecipeLetterFont, TextAnchor.MiddleCenter);
            recipeLineStyle = new GUIStyle(ui.Wrapped) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
            recipeLineGeneration = -1;
        }

        string MenuLetters()
        {
            if (ReferenceEquals(menuLetterBag, recipeLetters)) return menuLetterText;
            menuLetterBag = recipeLetters;
            menuLetterText = recipeLetters == null ? "" : recipeLetters.ToString();
            return menuLetterText;
        }

        void RecipeLetters(float width)
        {
            var row = GUILayoutUtility.GetRect(width, RecipeLetterHeight, GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint) return;
            var label = ui.SingleLine;
            float labelWide = Mathf.Ceil(label.CalcSize(recipeLettersLabel).x), x = row.x + labelWide + RecipeCardGap;
            GUI.Label(new Rect(row.x, row.y, labelWide, row.height), recipeLettersLabel, label);
            string letters = MenuLetters();
            if (letters.Length == 0)
            {
                GUI.Label(new Rect(x, row.y, Mathf.Max(1f, row.xMax - x), row.height), recipeNoLetters, label);
                return;
            }
            for (int i = 0; i < letters.Length && x + RecipeLetterTile <= row.xMax; i++, x += RecipeLetterTile + RecipeTileGap * 2f)
            {
                var cell = new Rect(x, row.y, RecipeLetterTile, RecipeLetterHeight);
                KitFill(cell, kitSmallShape, HudTileGot);
                KitPlain(cell, LetterContent(letters[i]), recipeLetterStyle, HudTileGotInk);
            }
        }

        void RecipeGrid(float width)
        {
            float grid = Mathf.Max(1f, width - RecipeScrollRoom);
            int columns = Mathf.Clamp(Mathf.FloorToInt((grid + RecipeCardGap) / (RecipeCardMin + RecipeCardGap)), 1, RecipeColumnsMax);
            recipeColumns = columns;
            float cardWidth = Mathf.Floor((grid - RecipeCardGap * (columns - 1)) / columns);
            float picture = Mathf.Clamp(Mathf.Floor(cardWidth * RecipePictureShare), RecipePictureMin, RecipePictureMax);
            int longest = 1;
            foreach (var card in recipeCards) if (card.Item != null) longest = Mathf.Max(longest, card.Item.Id.Length);
            float tileStep = Mathf.Min(RecipeTileMax, Mathf.Floor((cardWidth - RecipeCardPad * 2f + RecipeTileGap) / longest));
            float tileHeight = Mathf.Round((tileStep - RecipeTileGap) * RecipeTileAspect);
            recipeTileStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(tileHeight * RecipeTileFontShare), RecipeTileMinFont, RecipeTileMaxFont);
            float cardHeight = RecipeCardPad * 2f + picture + RecipeCardInner + tileHeight;
            int column = 0, visible = 0;
            foreach (var card in recipeCards)
            {
                if (!RecipeVisible(card)) continue;
                if (column == 0) GUILayout.BeginHorizontal();
                else GUILayout.Space(RecipeCardGap);
                RecipeTile(card, cardWidth, cardHeight, picture, tileStep, tileHeight);
                visible++;
                if (++column < columns) continue;
                GUILayout.EndHorizontal();
                GUILayout.Space(RecipeCardGap);
                column = 0;
            }
            if (column != 0) { GUILayout.FlexibleSpace(); GUILayout.EndHorizontal(); }
            if (visible == 0) GUILayout.Label("No matching recipes.", ui.Wrapped);
        }

        void RecipeTile(RecipeCard card, float width, float height, float picture, float tileStep, float tileHeight)
        {
            string id = card.Item.Id;
            if (MenuButton("recipe:" + id, "", GUILayout.Width(width), GUILayout.Height(height)))
                QueueMenuAction(() => SelectRecipe(id));
            if (Event.current.type != EventType.Repaint) return;
            Rect tile = GUILayoutUtility.GetLastRect();
            bool focused = menuButtonSelected;
            recipeContentBottom = Mathf.Max(recipeContentBottom, tile.yMax);
            if (focused) recipeFocusRect = tile;
            bool hover = tile.Contains(Event.current.mousePosition);
            (focused ? ui.Selected : GUI.skin.button).Draw(tile, GUIContent.none, hover, false, id == selectedRecipe && !focused, false);
            var art = OriginalItemArt.Find(id);
            var pictureArea = new Rect(tile.x + RecipeCardPad, tile.y + RecipeCardPad, tile.width - RecipeCardPad * 2f, picture);
            if (art) GUI.DrawTexture(FitRecipeIcon(pictureArea, art.width, art.height), art, ScaleMode.StretchToFill, true);
            if (card.Craftable)
            {
                if (!recipeCheck) recipeCheck = LobbyIcons.Get(LobbyIcons.Check).texture;
                KitImage(new Rect(tile.xMax - RecipeCardPad - RecipeMark, tile.y + RecipeCardPad, RecipeMark, RecipeMark), recipeCheck, HudHealth);
            }
            PaintRecipeTiles(card, new Rect(tile.x, pictureArea.yMax + RecipeCardInner, tile.width, tileHeight), tileStep);
        }

        void PaintRecipeTiles(RecipeCard card, Rect row, float tileStep)
        {
            string word = card.Item.Id;
            Array.Clear(recipeUsed, 0, recipeUsed.Length);
            float tileWidth = tileStep - RecipeTileGap, x = row.center.x - (word.Length * tileStep - RecipeTileGap) * .5f;
            for (int i = 0; i < word.Length; i++, x += tileStep)
            {
                char letter = word[i];
                if (!LetterBag.IsLetter(letter)) continue;
                bool held = ++recipeUsed[letter - 'A'] <= card.Have(letter);
                var cell = new Rect(x, row.y, tileWidth, row.height);
                if (held) KitFill(cell, kitSmallShape, HudTileGot);
                else { KitFill(cell, kitSmallShape, RecipeTileMissing); KitFill(cell, kitSmallRim, RecipeTileMissingRim); }
                KitPlain(cell, LetterContent(letter), recipeTileStyle, held ? HudTileGotInk : RecipeTileMissingInk);
            }
        }

        void RecipeLine(Rect area)
        {
            var card = SelectedRecipe();
            bool crafting = inventory?.Craft != null;
            string id = card.Item?.Id;
            if (!ReferenceEquals(recipeLineCards, recipeCards) || !string.Equals(recipeLineRecipe, id, StringComparison.Ordinal) ||
                recipeLineCrafting != crafting || !Mathf.Approximately(recipeLineWidth, area.width) || recipeLineGeneration != ui.Generation)
            {
                recipeLineCards = recipeCards; recipeLineRecipe = id; recipeLineCrafting = crafting;
                recipeLineWidth = area.width; recipeLineGeneration = ui.Generation;
                recipeLineContent.text = RecipeLineText(card, area.width);
            }
            GUI.Label(area, recipeLineContent, recipeLineStyle);
        }

        string RecipeLineText(RecipeCard card, float width)
        {
            if (card.Item == null) return RecipeDetail(card);
            string status = inventory?.Craft != null ? "Spelling…" : !card.Craftable
                ? "Need " + string.Join(" ", MissingInWordOrder(card.Item.Id, card.Missing).ToCharArray()) : "Letters ready";
            string blurb = LoadoutPage.Blurb(card.Item);
            string brief = card.Item.Id + " · " + status;
            if (string.IsNullOrEmpty(blurb)) return brief;
            recipeLineContent.text = card.Item.Id + " · " + blurb + " · " + status;
            return recipeLineStyle.CalcSize(recipeLineContent).x <= width ? recipeLineContent.text : brief;
        }

        static string MissingInWordOrder(string word, LetterBag missing)
        {
            var owed = new LetterBag(missing);
            var ordered = new System.Text.StringBuilder(owed.Count);
            foreach (char letter in word)
                if (LetterBag.IsLetter(letter) && owed.TryRemove(letter)) ordered.Append(letter);
            foreach (char letter in owed.Letters()) ordered.Append(letter);
            return ordered.ToString();
        }

        static Rect FitRecipeIcon(Rect area, float width, float height)
        {
            float scale = Mathf.Min(area.width / Mathf.Max(1, width), area.height / Mathf.Max(1, height));
            var size = new Vector2(width * scale, height * scale);
            return new Rect(area.center - size * .5f, size);
        }

        void RecipeSelectionDetail()
        {
            var card = SelectedRecipe();
            bool before = GUI.enabled;
            GUI.enabled = before && CanCraftRecipe(card);
            if (MenuButton("craft-selected", SpellWord, GUILayout.MinHeight(ui.RowHeight), GUILayout.ExpandWidth(true)))
                QueueMenuAction(CraftSelectedRecipe);
            GUI.enabled = before;
        }
    }
}
