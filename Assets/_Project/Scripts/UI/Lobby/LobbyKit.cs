using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    /// <summary>
    /// Colours and builders for the PC lobby: flat dark bars over the live house, cream type,
    /// honey for the selected tab and tomato for GO. No blue backgrounds (user, 6 Oct 2026).
    /// </summary>
    public static class LobbyKit
    {
        public static readonly Color Bar = new Color(.10f, .08f, .07f, .86f);
        public static readonly Color Panel = new Color(.12f, .10f, .09f, .9f);
        /// <summary>For pages that cover you (PLAY, Settings, Leaderboard): solid, because UI blends in
        /// linear space and even .975 left a ghost of the avatar through a centred panel.</summary>
        public static readonly Color Page = new Color(.10f, .08f, .07f, 1f);
        public static readonly Color Card = new Color(1f, .97f, .9f, .07f);
        public static readonly Color CardHover = new Color(1f, .97f, .9f, .14f);
        public static readonly Color Line = new Color(1f, .97f, .9f, .1f);
        public static readonly Color Cream = new Color(.97f, .94f, .86f);
        public static readonly Color Muted = new Color(.97f, .94f, .86f, .58f);
        public static readonly Color Honey = new Color(.96f, .71f, .28f);
        public static readonly Color Tomato = new Color(.89f, .33f, .24f);
        public static readonly Color Sage = new Color(.56f, .72f, .52f);
        public static readonly Color Shade = new Color(.06f, .05f, .04f, .55f);

        public static RectTransform Rect(Transform parent, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Anchors a rect to a fraction of its parent, with pixel insets.</summary>
        public static RectTransform Place(this RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            return rect;
        }

        /// <summary>Pins a fixed-size rect to an anchor point of its parent.</summary>
        public static RectTransform Pin(this RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Fill(this RectTransform rect) => rect.Place(Vector2.zero, Vector2.one);

        public static Image Paint(this RectTransform rect, Color colour, bool rounded = false)
        {
            if (!rect.TryGetComponent(out Image image)) image = rect.gameObject.AddComponent<Image>();
            image.color = colour;
            if (rounded) { image.sprite = LobbyIcons.Rounded; image.type = Image.Type.Sliced; }
            return image;
        }

        public static TextMeshProUGUI Text(Transform parent, string text, float size, Color colour,
            TextAlignmentOptions align = TextAlignmentOptions.Left, FontStyles style = FontStyles.Normal)
        {
            var label = Rect(parent, "Text").gameObject.AddComponent<TextMeshProUGUI>();
            if (GameAssets.I && GameAssets.I.font) label.font = GameAssets.I.font;
            label.text = text; label.fontSize = size; label.color = colour; label.alignment = align;
            label.fontStyle = style; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        public static Image Icon(Transform parent, string icon, Color colour)
        {
            var image = Rect(parent, "Icon " + icon).gameObject.AddComponent<Image>();
            image.sprite = LobbyIcons.Get(icon); image.color = colour; image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        /// <summary>A flat button whose background brightens on hover and dims when pressed.</summary>
        public static Button Button(Transform parent, string name, Color colour, Action click, bool rounded = true)
        {
            var rect = Rect(parent, name);
            var image = rect.Paint(colour, rounded);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colours = button.colors;
            // The multiplier scales every state, Normal too, so it stays 1 and a button rests at its own colour.
            colours.normalColor = Color.white;
            colours.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 2f);
            colours.selectedColor = new Color(1.15f, 1.15f, 1.15f, 1.7f);
            colours.pressedColor = new Color(.8f, .8f, .8f, 1f);
            colours.disabledColor = new Color(.6f, .6f, .6f, .5f);
            colours.colorMultiplier = 1f;
            colours.fadeDuration = .08f;
            button.colors = colours;
            button.onClick.AddListener(() => click?.Invoke());
            return button;
        }

        /// <summary>A button with one centred label.</summary>
        public static Button LabelButton(Transform parent, string title, Color colour, Color text, float size, Action click)
        {
            var button = Button(parent, title, colour, click);
            var label = Text(button.transform, title, size, text, TextAlignmentOptions.Center, FontStyles.Bold);
            label.rectTransform.Fill();
            return button;
        }

        /// <summary>A button with no fill until the pointer or a controller is on it.</summary>
        public static Button Ghost(Transform parent, string name, Action click)
        {
            var button = Button(parent, name, Color.white, click);
            var colours = button.colors;
            colours.normalColor = new Color(1, 1, 1, 0);
            colours.highlightedColor = new Color(1, 1, 1, .13f);
            colours.selectedColor = new Color(1, 1, 1, .1f);
            colours.pressedColor = new Color(1, 1, 1, .06f);
            colours.disabledColor = new Color(1, 1, 1, 0);
            colours.colorMultiplier = 1f;
            button.colors = colours;
            return button;
        }

        /// <summary>A square icon button for the top bar, with a hover hint underneath it.</summary>
        public static Button IconButton(Transform parent, string icon, string hint, Action click)
        {
            var button = Ghost(parent, hint, click);
            var glyph = Icon(button.transform, icon, Cream);
            glyph.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(12, 12), new Vector2(-12, -12));
            button.gameObject.AddComponent<LobbyHint>().Text = hint;
            return button;
        }

        public static string Upper(string text) => (text ?? "").ToUpperInvariant();

        /// <summary>A vertical stack whose children keep their preferred heights and fill the width.</summary>
        public static RectTransform Column(Transform parent, string name, float spacing, int padding = 0)
        {
            var rect = Rect(parent, name);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing; layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            return rect;
        }

        /// <summary>A horizontal row whose children keep their preferred widths and fill the height.</summary>
        public static RectTransform Row(Transform parent, string name, float spacing, int padding = 0)
        {
            var rect = Rect(parent, name);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing; layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;
            return rect;
        }

        public static RectTransform Grid(Transform parent, string name, Vector2 cell, float spacing)
        {
            var rect = Rect(parent, name);
            var grid = rect.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell; grid.spacing = new Vector2(spacing, spacing);
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            return rect;
        }

        /// <summary>Gives a layout child a preferred size; -1 leaves that axis to the layout.
        /// A given size is fixed: a row or column inside would otherwise report itself flexible
        /// (force-expanded children make a group flexible) and swallow its parent's spare room.</summary>
        public static T Size<T>(this T component, float width, float height = -1, float flexibleWidth = -1) where T : Component
        {
            if (!component.TryGetComponent(out LayoutElement element)) element = component.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width; element.preferredHeight = height;
            element.flexibleWidth = flexibleWidth >= 0 ? flexibleWidth : width >= 0 ? 0 : -1;
            element.flexibleHeight = height >= 0 ? 0 : -1;
            if (width >= 0) element.minWidth = width;
            if (height >= 0) element.minHeight = height;
            return component;
        }

        /// <summary>A small caps heading with a faint rule under it.</summary>
        public static TextMeshProUGUI Heading(Transform parent, string text)
        {
            var label = Text(parent, Upper(text), 20, Muted, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            label.characterSpacing = 6;
            label.Size(-1, 34);
            return label;
        }

        /// <summary>A vertical scroll view filling its parent. Returns the column to add rows to.</summary>
        public static RectTransform Scroll(Transform parent, string name, float spacing)
        {
            var view = Rect(parent, name).Fill();
            view.Paint(new Color(0, 0, 0, 0));
            view.gameObject.AddComponent<RectMask2D>();
            var content = Column(view, "Content", spacing);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.viewport = view; scroll.content = content;
            return content;
        }

        /// <summary>A pill that shows whether it's the chosen option.</summary>
        public static Button Chip(Transform parent, string title, bool on, Action click, bool locked = false)
        {
            var button = LabelButton(parent, title, on ? new Color(Honey.r, Honey.g, Honey.b, .9f) : Card,
                on ? new Color(.12f, .09f, .06f) : locked ? Muted : Cream, 20, click);
            button.name = title;
            return button;
        }

        public static void Clear(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }
    }
}
