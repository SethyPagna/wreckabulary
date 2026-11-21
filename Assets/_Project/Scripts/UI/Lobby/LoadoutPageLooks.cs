using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        public const float DeleteWindow = 3f;
        const int NoLook = -1;
        const float SavedBlob = 64f, SavedGlyph = 44f, LookBlob = 110f, DeleteWidth = 190f, StripControl = 46f;
        static readonly string[] SavedSwatches = { "Top", "Hair", "Bottoms" };
        static readonly Vector2[] SavedSpots = { new Vector2(-46f, 30f), new Vector2(46f, 30f), new Vector2(0f, -18f) };

        int focusedLook = NoLook, armedDelete = NoLook;
        float armedAt;
        Button deleteButton;

        void LooksGrid(RectTransform view, RectTransform body)
        {
            var grid = TileGrid(view, body);
            var owned = CosmeticBundles.All.Where(bundle => bundle.OwnedBy(Menu.Career)).ToList();
            var locked = OwnedOnly ? new List<CosmeticBundle>() : CosmeticBundles.All.Where(bundle => !bundle.OwnedBy(Menu.Career)).ToList();
            foreach (var bundle in owned) tiles.Add(ShopLookTile(grid, bundle, true));
            foreach (var bundle in locked) tiles.Add(ShopLookTile(grid, bundle, false));
            for (int i = 0; i < looks.Count; i++) tiles.Add(SavedLookTile(grid, i));
            First = tiles.Count > 0 ? tiles[0] : viewSwitch;
        }

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
            var check = SavedLooks.Check(look, Wardrobe, Menu.Career, GameConfig.Current.Items);
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
            string reason = check.Reason == null ? "" : Separator + check.Reason;
            button.gameObject.AddComponent<LobbyHint>().Text = look.Name + reason;
            string line = look.Name + reason;
            string key = SavedPrefix + index.ToString(CultureInfo.InvariantCulture);
            var preview = check.Problems.Count == 0 ? check.Outfit : null;
            var face = button.FaceOf();
            button.GetComponent<LobbyPress>().Hot = hot =>
            {
                face.color = hot ? LobbyKit.Raised : LobbyKit.Card;
                if (hot) FocusLook(index);
                Hover(key, () => preview, replacing ? ReplacePrompt : line, hot);
            };
            return button;
        }

        void FocusLook(int index)
        {
            focusedLook = index;
            if (armedDelete != index) armedDelete = NoLook;
            if (deleteButton) deleteButton.interactable = looks.Has(index) && store.Readable;
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
            var check = SavedLooks.Check(look, Wardrobe, Menu.Career, GameConfig.Current.Items);
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
            deleteButton = LobbyKit.TextAction(row, DeleteLook, "Delete look", DetailText, DeleteFocused, icon: LobbyIcons.Delete);
            deleteButton.Size(DeleteWidth, StripControl);
            deleteButton.interactable = looks.Has(focusedLook) && store.Readable;
            deleteButton.gameObject.AddComponent<LobbyHint>().Text = "Delete the saved look you chose last";
            RaiseText(deleteButton);
            stripFirst = deleteButton;
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
            return looks.Count.ToString(CultureInfo.InvariantCulture) + " of " + SavedLooks.Slots.ToString(CultureInfo.InvariantCulture) + " looks saved";
        }
    }
}
