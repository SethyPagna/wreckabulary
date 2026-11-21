using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed partial class LoadoutPage
    {
        public const string RailName = "Category rail", CategoryPrefix = "Category ";
        public const int RailRows = 5, RailColumns = 2, Columns = 3;
        public const float RailButton = 72f, RailGap = 8f, RailPad = 10f, RailTop = 158f, RailTuck = 6f, RailFade = .6f;
        public const float RailWidth = RailPad * 2f + RailButton * RailColumns + RailGap * (RailColumns - 1);
        public const float RailHeight = RailPad * 2f + RailButton * RailRows + RailGap * (RailRows - 1);
        const float RailIcon = 44f;
        const int RailRadius = 20;

        public static readonly string[] RailOrder =
            { "Hair", "Eyes", "Headwear", "Face", "Badge", "Top", "Bottoms", "Gloves", "Footwear", "Back" };

        public static string SlotTitle(string slot) => slot switch
        {
            "Headwear" => "Hats & ears",
            "Face" => "Glasses",
            "Badge" => "Badge and letter",
            "Footwear" => "Shoes",
            "Back" => "Bag",
            _ => slot,
        };

        void BuildRail()
        {
            var rail = LobbyKit.Rect(Root, RailName).Pin(Vector2.one, new Vector2(-(PanelSide - RailTuck), -RailTop), new Vector2(RailWidth, RailHeight));
            rail.SetAsFirstSibling();
            LobbyKit.PanelFace(rail, RailRadius);
            if (ShowingRecipes) rail.gameObject.AddComponent<CanvasGroup>().alpha = RailFade;
            for (int i = 0; i < RailOrder.Length; i++)
            {
                string slot = RailOrder[i];
                bool on = slot == Category;
                var button = IconTile(rail, CategoryPrefix + slot, LobbyIcons.ForSlot(slot), SlotTitle(slot), on, () => ShowCategory(slot), RailIcon);
                float step = RailButton + RailGap;
                ((RectTransform)button.transform).Pin(new Vector2(0, 1), new Vector2(RailPad + i / RailRows * step, -(RailPad + i % RailRows * step)),
                    new Vector2(RailButton, RailButton));
                railButtons.Add(button);
                if (on) selectedRail = button;
            }
        }

        public void ShowCategory(string slot)
        {
            Enter(slot);
            Reselect(CategoryPrefix + slot);
        }

        void Enter(string slot)
        {
            if (Array.IndexOf(RailOrder, slot) < 0) throw new ArgumentException("No wardrobe category " + slot, nameof(slot));
            Category = slot;
            ShowingLooks = ShowingRecipes = false;
            ResetScroll();
            Disarm();
            focusedLook = NoLook;
            Rebuild();
        }

        public void StepCategory(int step)
        {
            int at = Mathf.Max(0, Array.IndexOf(RailOrder, Category));
            string next = RailOrder[((at + step) % RailOrder.Length + RailOrder.Length) % RailOrder.Length];
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            bool onRail = selected && selected.name.StartsWith(CategoryPrefix, StringComparison.Ordinal);
            Enter(next);
            if (onRail) Reselect(CategoryPrefix + next);
            else Select(First);
        }

        void ListenForKeys()
        {
            if (!Root.TryGetComponent(out LobbyLoadoutKeys keys)) keys = Root.gameObject.AddComponent<LobbyLoadoutKeys>();
            keys.Step = StepCategory;
        }

        void WireNavigation()
        {
            for (int i = 0; i < railButtons.Count; i++)
            {
                int column = i / RailRows, row = i % RailRows;
                Wire(railButtons[i],
                    row > 0 ? railButtons[i - 1] : null,
                    row < RailRows - 1 && i + 1 < railButtons.Count ? railButtons[i + 1] : null,
                    column > 0 ? railButtons[i - RailRows] : null,
                    column < RailColumns - 1 && i + RailRows < railButtons.Count ? railButtons[i + RailRows] : First);
            }
            Selectable below = stripFirst ? stripFirst : saveLook;
            int lastRow = tiles.Count == 0 ? 0 : (tiles.Count - 1) / Columns;
            for (int i = 0; i < tiles.Count; i++)
            {
                int column = i % Columns;
                Selectable down = i + Columns < tiles.Count ? tiles[i + Columns] : i / Columns == lastRow ? below : tiles[tiles.Count - 1];
                Wire(tiles[i],
                    i >= Columns ? tiles[i - Columns] : viewSwitch,
                    down,
                    column > 0 ? tiles[i - 1] : selectedRail,
                    column < Columns - 1 && i + 1 < tiles.Count ? tiles[i + 1] : null);
            }
        }

        static void Wire(Selectable from, Selectable up, Selectable down, Selectable left, Selectable right)
        {
            if (!from) return;
            from.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down, selectOnLeft = left, selectOnRight = right,
            };
        }
    }
}
