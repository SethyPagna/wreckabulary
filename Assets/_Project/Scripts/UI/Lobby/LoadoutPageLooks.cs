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
        public const string LookPrefix = "Look ", SavedPrefix = "Saved look ", DeleteLook = "Delete look", LookPicture = "Look picture";
        public const string LookFallback = "Look fallback", SavedArt = "Saved look art", ProblemMark = "Problem";
        public const string ReplacePrompt = "Pick a look to replace", PortraitFolder = "UI/Shop/Looks/";
        public const string NewerLooks = "These saved looks come from a newer version, so they can't be changed here.";
        public const string LookName = "Look name", LookNameHint = "Type a new name for this look";
        public const string AddLook = "Add look", AddLookArt = "Add look art", ShopLooksTitle = "Shop looks title", ShopLooksName = "Shop looks";
        public const float DeleteWindow = 3f;
        const int NoLook = -1, NameRadius = 8, NameCaret = 2;
        const float SavedBlob = 64f, SavedGlyph = 44f, LookBlob = 110f, DeleteWidth = 190f, StripControl = 46f;
        const float NameText = 20f, NameInset = 14f, NameEdge = 2f, NameSelection = .5f;
        const float AddGlyph = 72f, AddFade = .55f;
        static readonly string[] SavedSwatches = { "Top", "Hair", "Bottoms" };
        static readonly Vector2[] SavedSpots = { new Vector2(-46f, 30f), new Vector2(46f, 30f), new Vector2(0f, -18f) };

        int focusedLook = NoLook, armedDelete = NoLook, naming = NoLook;
        float armedAt;
        Button deleteButton;
        TMP_InputField nameField;

        void LooksGrid(RectTransform view, RectTransform body)
        {
            var mine = TileGrid(view, body);
            for (int i = 0; i < looks.Count; i++) tiles.Add(SavedLookTile(mine, i));
            if (!looks.IsFull) tiles.Add(AddLookTile(mine));
            if (tiles.Count == 0) mine.gameObject.SetActive(false);
            var owned = CosmeticBundles.All.Where(bundle => bundle.OwnedBy(Menu.Career)).ToList();
            var locked = OwnedOnly ? new List<CosmeticBundle>() : CosmeticBundles.All.Where(bundle => !bundle.OwnedBy(Menu.Career)).ToList();
            if (owned.Count + locked.Count > 0)
            {
                LobbyKit.SectionLabel(lockerList, "Shop looks").name = ShopLooksTitle;
                var shop = MoreTiles(ShopLooksName);
                tileBreak = tiles.Count;
                foreach (var bundle in owned) tiles.Add(ShopLookTile(shop, bundle, true));
                foreach (var bundle in locked) tiles.Add(ShopLookTile(shop, bundle, false));
            }
            First = tiles.Count > 0 ? tiles[0] : viewSwitch;
        }

        Button AddLookTile(Transform grid)
        {
            var button = TileButton(grid, AddLook, SaveLook);
            var body = button.Body();
            var art = LobbyKit.Rect(body, AddLookArt).Fill();
            var ink = LobbyKit.Navy;
            LobbyKit.Icon(art, LobbyIcons.Plus, new Color(ink.r, ink.g, ink.b, AddFade)).rectTransform.Pin(new Vector2(.5f, .5f), new Vector2(0f, 18f),
                new Vector2(AddGlyph, AddGlyph));
            TileTag(art, "Save look");
            button.interactable = store.Readable;
            button.gameObject.AddComponent<LobbyHint>().Text = SaveLookHint;
            string line = SaveLookHint + Separator + LooksCount();
            var face = button.FaceOf();
            button.GetComponent<LobbyPress>().Hot = hot =>
            {
                face.color = hot ? LobbyKit.Raised : LobbyKit.Card;
                Hover(AddLook, null, line, hot);
            };
            return button;
        }

        string LooksCount() =>
            looks.Count.ToString(CultureInfo.InvariantCulture) + " of " + SavedLooks.Slots.ToString(CultureInfo.InvariantCulture) + " looks saved";

        Button ShopLookTile(Transform grid, CosmeticBundle bundle, bool owned)
        {
            var button = TileButton(grid, LookPrefix + bundle.Id, () => WearLook(bundle));
            var body = button.Body();
            var portrait = Resources.Load<Texture2D>(PortraitFolder + bundle.Id);
            if (portrait)
            {
                var image = LobbyKit.Rect(body, LookPicture).Place(Vector2.zero, Vector2.one, new Vector2(PictureInset, PictureInset),
                    new Vector2(-PictureInset, -PictureInset)).gameObject.AddComponent<RawImage>();
                image.texture = portrait;
                image.raycastTarget = false;
                var fit = image.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = (float)portrait.width / portrait.height;
            }
            else
            {
                var art = LobbyKit.Rect(body, LookFallback).Fill();
                var colour = Wardrobe.Colour(SoldPalette, bundle.TopColour);
                Blob(art, colour != null ? new Color(colour.R, colour.G, colour.B) : LobbyKit.Raised, LookBlob);
                LobbyKit.Icon(art, LobbyIcons.Looks, LobbyKit.Navy).rectTransform.Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(SavedGlyph, SavedGlyph));
                TileTag(art, bundle.Name);
            }
            var applied = bundle.Apply(Menu.Outfit, Wardrobe);
            bool worn = applied != null && applied.Serialize() == Menu.Outfit.Serialize();
            bool tried = !worn && tryOn != null && bundle.Apply(tryOn, Wardrobe)?.Serialize() == tryOn.Serialize();
            if (worn) WornMark(body);
            else if (tried) TriedMark(body);
            var offer = Dressing.OfferFor(LookKind + ":" + bundle.Id);
            string price = "";
            if (!owned)
            {
                CornerDisc(body, LockMark, LobbyIcons.Lock, LobbyKit.Panel, LobbyKit.Navy, TopRight);
                if (offer != null)
                {
                    int left = Menu.Career.PriceOf(offer);
                    PriceMark(body, left);
                    price = Separator + left.ToString("N0", CultureInfo.InvariantCulture) + " coins";
                }
            }
            button.gameObject.AddComponent<LobbyHint>().Text = bundle.Name + price;
            string line = bundle.Name + Separator + bundle.Collection + price;
            string key = LookKind + ":" + bundle.Id;
            var face = button.FaceOf();
            button.GetComponent<LobbyPress>().Hot = hot =>
            {
                face.color = hot ? LobbyKit.Raised : LobbyKit.Card;
                Hover(key, () => bundle.Apply(Shown, Wardrobe), line, hot);
            };
            return button;
        }

        void WearLook(CosmeticBundle bundle)
        {
            Disarm();
            string name = LookPrefix + bundle.Id;
            var next = bundle.Apply(Menu.Outfit, Wardrobe);
            if (next == null)
            {
                Menu.Post(Clash);
                return;
            }
            if (Menu.Career.Missing(next).Count > 0)
            {
                StartTryOn(bundle.Apply(Shown, Wardrobe) ?? next, name);
                return;
            }
            if (next.Serialize() != Menu.Outfit.Serialize()) Menu.Wear(next);
            tryOn = null;
            Rebuild();
            Reselect(name);
        }

        Button SavedLookTile(Transform grid, int index)
        {
            var look = looks[index];
            var check = CheckLook(look);
            var button = TileButton(grid, SavedPrefix + (index + 1).ToString(CultureInfo.InvariantCulture), () => PickSaved(index));
            var body = button.Body();
            var art = LobbyKit.Rect(body, SavedArt).Fill();
            var outfit = look.ToOutfit();
            for (int i = 0; i < SavedSwatches.Length; i++)
            {
                var colour = Wardrobe.ColourFor(outfit, SavedSwatches[i]);
                var blob = Blob(art, colour != null ? new Color(colour.R, colour.G, colour.B) : LobbyKit.Raised, SavedBlob);
                blob.anchoredPosition = SavedSpots[i];
            }
            LobbyKit.Icon(art, LobbyIcons.Looks, LobbyKit.Navy).rectTransform.Pin(new Vector2(.5f, .5f), new Vector2(0f, 18f), new Vector2(SavedGlyph, SavedGlyph));
            TileTag(art, look.Name);
            bool worn = look.OutfitText == Menu.Outfit.Serialize();
            bool tried = !worn && tryOn != null && look.OutfitText == tryOn.Serialize();
            if (worn) WornMark(body);
            else if (tried) TriedMark(body);
            else if (check.Problems.Count > 0) CornerDisc(body, ProblemMark, LobbyIcons.Close, LobbyKit.Panel, LobbyKit.Hot, TopRight);
            else if (check.Missing.Count > 0) CornerDisc(body, LockMark, LobbyIcons.Lock, LobbyKit.Panel, LobbyKit.Navy, TopRight);
            string reason = ReasonLine(check);
            button.gameObject.AddComponent<LobbyHint>().Text = look.Name + reason;
            string key = SavedKey(index);
            var preview = check.Problems.Count == 0 ? check.Outfit : null;
            var face = button.FaceOf();
            button.GetComponent<LobbyPress>().Hot = hot =>
            {
                face.color = hot ? LobbyKit.Raised : LobbyKit.Card;
                if (hot) FocusLook(index);
                Hover(key, () => preview, replacing ? ReplacePrompt : SavedLine(index, reason), hot);
            };
            return button;
        }

        string SavedLine(int index, string reason) => looks.Has(index) ? looks[index].Name + reason : "";

        static string SavedKey(int index) => SavedPrefix + index.ToString(CultureInfo.InvariantCulture);

        SavedLookCheck CheckLook(SavedLook look) => SavedLooks.Check(look, Wardrobe, Menu.Career, GameConfig.Current.Items);

        static string ReasonLine(SavedLookCheck check) => check.Reason == null ? "" : Separator + check.Reason;

        void FocusLook(int index)
        {
            focusedLook = index;
            if (armedDelete != index) armedDelete = NoLook;
            if (deleteButton) deleteButton.interactable = looks.Has(index) && store.Readable;
            ShowLookName();
        }

        void PickSaved(int index)
        {
            if (!looks.Has(index)) return;
            string name = SavedPrefix + (index + 1).ToString(CultureInfo.InvariantCulture);
            var look = looks[index];
            armedDelete = NoLook;
            focusedLook = index;
            if (replacing)
            {
                replacing = false;
                string error = looks.Replace(index, Menu.Outfit);
                if (error == null && !store.Save(looks)) error = NewerLooks;
                Menu.Post(error ?? look.Name + " now holds what you are wearing.");
                Rebuild();
                Reselect(name);
                return;
            }
            var check = CheckLook(look);
            if (check.Problems.Count > 0)
            {
                Menu.Post(check.Reason);
                return;
            }
            if (check.Missing.Count > 0)
            {
                StartTryOn(check.Outfit, name);
                return;
            }
            if (look.OutfitText != Menu.Outfit.Serialize()) Menu.Wear(check.Outfit);
            tryOn = null;
            Rebuild();
            Reselect(name);
        }

        public void SaveLook()
        {
            Disarm();
            if (!store.Readable)
            {
                Menu.Post(NewerLooks);
                return;
            }
            int known = looks.IndexOf(Menu.Outfit);
            if (known >= 0)
            {
                Menu.Post("Already saved as " + looks[known].Name + ".");
                OpenLooksOn(known);
                return;
            }
            if (looks.IsFull)
            {
                Menu.Post(SavedLooks.Full);
                replacing = true;
                OpenLooksOn(0);
                return;
            }
            string name = looks.SuggestName();
            string error = looks.Add(name, Menu.Outfit);
            if (error == null && !store.Save(looks)) error = NewerLooks;
            Menu.Post(error ?? "Saved as " + name + ".");
            if (error == null) OpenLooksOn(looks.Count - 1);
        }

        void OpenLooksOn(int index)
        {
            ShowingLooks = true;
            ShowingRecipes = false;
            ResetScroll();
            focusedLook = index;
            Rebuild();
            Reselect(SavedPrefix + (index + 1).ToString(CultureInfo.InvariantCulture));
        }

        bool LooksStripShown() => looks != null && looks.Count > 0;

        void LooksStrip(RectTransform strip)
        {
            var row = LobbyKit.Row(strip, "Look actions", 10);
            row.Fill();
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            nameField = LookNameField(row);
            deleteButton = LobbyKit.TextAction(row, DeleteLook, "Delete look", DetailText, DeleteFocused, icon: LobbyIcons.Delete);
            deleteButton.Size(DeleteWidth, StripControl);
            deleteButton.interactable = looks.Has(focusedLook) && store.Readable;
            deleteButton.gameObject.AddComponent<LobbyHint>().Text = "Delete the saved look you chose last";
            RaiseText(deleteButton);
            stripFirst = deleteButton;
            ShowLookName();
        }

        TMP_InputField LookNameField(Transform row)
        {
            var holder = LobbyKit.Rect(row, LookName);
            holder.gameObject.SetActive(false);
            holder.Size(-1, StripControl, 1);
            var face = holder.Paint(LobbyKit.Raised, NameRadius);
            LobbyKit.Frame(holder, LobbyKit.Line, NameRadius, 1);
            var area = LobbyKit.Rect(holder, "Text area").Place(Vector2.zero, Vector2.one, new Vector2(NameInset, NameEdge), new Vector2(-NameInset, -NameEdge));
            area.gameObject.AddComponent<RectMask2D>();
            var text = LobbyKit.Text(area, "", NameText, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            text.overflowMode = TextOverflowModes.Overflow;
            text.rectTransform.Fill();
            var field = holder.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = face;
            field.textViewport = area;
            field.textComponent = text;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = SavedLooks.NameLength;
            field.customCaretColor = true;
            field.caretColor = LobbyKit.Navy;
            field.caretWidth = NameCaret;
            field.selectionColor = new Color(LobbyKit.Selection.r, LobbyKit.Selection.g, LobbyKit.Selection.b, NameSelection);
            field.inputValidator = LatinNameValidator.Create();
            field.shouldActivateOnSelect = false;
            field.onSelect.AddListener(_ => naming = focusedLook);
            field.onEndEdit.AddListener(RenameLook);
            holder.gameObject.AddComponent<LobbyHint>().Text = LookNameHint;
            holder.gameObject.SetActive(true);
            return field;
        }

        void ShowLookName()
        {
            if (!nameField || nameField.isFocused) return;
            bool chosen = looks.Has(focusedLook);
            nameField.SetTextWithoutNotify(chosen ? looks[focusedLook].Name : "");
            nameField.interactable = chosen && store.Readable;
        }

        void RenameLook(string typed)
        {
            int index = naming == NoLook ? focusedLook : naming;
            naming = NoLook;
            string name = SavedLooks.CleanName(typed);
            if (store.Readable && looks.Has(index) && name != null && name != looks[index].Name) Rename(index, name);
            ShowLookName();
        }

        void Rename(int index, string name)
        {
            Disarm();
            string before = looks[index].Name;
            string error = looks.Rename(index, name);
            if (error == null && !store.Save(looks)) error = NewerLooks;
            Menu.Post(error ?? before + " is now " + name + ".");
            string after = looks[index].Name;
            var tile = Root.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == SavedPrefix + (index + 1).ToString(CultureInfo.InvariantCulture));
            if (!tile) return;
            foreach (var tag in tile.GetComponentsInChildren<TMP_Text>())
                if (tag.name == TagLine) tag.text = after;
            if (tile.TryGetComponent(out LobbyHint hint) && hint.Text.StartsWith(before, System.StringComparison.Ordinal))
                hint.Text = after + hint.Text.Substring(before.Length);
            if (hovering == null) SetDetail(RestingDetail());
            else if (hovering == SavedKey(index)) SetDetail(SavedLine(index, ReasonLine(CheckLook(looks[index]))));
        }

        void DeleteFocused()
        {
            replacing = false;
            if (!looks.Has(focusedLook)) return;
            var look = looks[focusedLook];
            if (armedDelete != focusedLook || Time.unscaledTime - armedAt > DeleteWindow)
            {
                armedDelete = focusedLook;
                armedAt = Time.unscaledTime;
                SetDetail("Press again to delete " + look.Name);
                return;
            }
            armedDelete = NoLook;
            string error = looks.Remove(focusedLook);
            if (error == null && !store.Save(looks)) error = NewerLooks;
            Menu.Post(error ?? "Deleted " + look.Name + ".");
            focusedLook = Mathf.Min(focusedLook, looks.Count - 1);
            Rebuild();
            if (looks.Count > 0) Reselect(SavedPrefix + (focusedLook + 1).ToString(CultureInfo.InvariantCulture));
            else Reselect(LooksView);
        }

        string LooksDetail()
        {
            if (tryOn != null) return "Trying on" + Separator + "not saved";
            int worn = looks.IndexOf(Menu.Outfit);
            if (worn >= 0) return "Wearing " + looks[worn].Name;
            return LooksCount();
        }
    }
}
