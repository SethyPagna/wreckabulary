using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    /// <summary>
    /// Colours and builders for the PC lobby, in the web lobby's look (Web/src/style.css) on the CS2
    /// layout: navy glass panels over the live map (no blue backdrop, user, 6 Oct 2026), white and
    /// sun type in Lilita One, sun slabs for what's chosen, wood letter tiles, navy outlines and hard
    /// navy drop shadows. Buttons lift under the pointer and sink when pressed (LobbyPress).
    /// </summary>
    public static class LobbyKit
    {
        /// <summary>A web colour. uGUI takes Graphic.color as sRGB even in this linear project, so no .linear.</summary>
        public static Color Hex(uint rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);

        /// <summary>
        /// The web's see-through white at this alpha, as it looks over navy. This project blends UI in
        /// linear space, where white at the web's alpha comes out far paler than in a browser (rows read
        /// as grey); a periwinkle at the same alpha lands close to the browser's colour.
        /// </summary>
        public static Color Mist(float alpha) => Hex(0x7d80bb, alpha);

        public static readonly Color Navy = Hex(0x0b0e45), Navy2 = Hex(0x151c7a), Cyan = Hex(0x2fe6ff);
        public static readonly Color Sun = Hex(0xffd21f), Sun2 = Hex(0xffad00), SunHi = Hex(0xffe55c), PlayHi = Hex(0xfff06a);
        public static readonly Color Hot = Hex(0xff5a1f), Lime = Hex(0x7be03a);
        public static readonly Color WoodHi = Hex(0xffd998), Wood = Hex(0xf2b25c), WoodLo = Hex(0xe38f34), Cocoa = Hex(0x3b2314), WoodInk = Hex(0x4a2a14);
        public static readonly Color CardSub = Hex(0x4a5290), Owned = Hex(0x2fa84f), Short = Hex(0xe6e9f7), Focus = Hex(0xedab51);
        public static readonly Color CoinRim = Hex(0xf0a400), CoinRimHi = Hex(0xffe066), CoinInk = Hex(0x8a5a10);

        /// <summary>The top of the bar's fade; it clears to nothing at the bottom.</summary>
        public static readonly Color Bar = Hex(0x06072f, .85f);
        /// <summary>Navy glass for panels beside you.</summary>
        public static readonly Color Panel = Hex(0x0a0e52, .92f);
        /// <summary>For pages that cover you (PLAY, Settings, Leaderboard): solid, because UI blends in
        /// linear space and even .975 left a ghost of the avatar through a centred panel.</summary>
        public static readonly Color Page = Hex(0x0b0e45);
        public static readonly Color Card = Mist(.122f);
        public static readonly Color Line = Mist(.188f);
        public static readonly Color TabIdle = Hex(0x0a0e52, .79f), TabHover = Hex(0x151c7a, .9f);
        public static readonly Color ChipFill = Hex(0x0a0e52, .85f), ChipEdge = Mist(.25f);
        public static readonly Color PillFill = Mist(.133f), PillEdge = Mist(.333f);
        public static readonly Color Cream = Color.white;
        /// <summary>Second-line text: the web's mist.</summary>
        public static readonly Color Muted = Hex(0xdfe6ff);
        /// <summary>Locked and disabled text.</summary>
        public static readonly Color Faded = new Color(1f, 1f, 1f, .5f);
        public static readonly Color Honey = Sun, Tomato = Hot;
        public static readonly Color Shade = Hex(0x04052a, .8f);
        public const int TabRadius = 6;

        public enum Ink { Plain, Stroke, Drop }

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

        public static Image Paint(this RectTransform rect, Color colour, bool rounded = false) => rect.Paint(colour, rounded ? 10 : -1);

        /// <summary>Fills a rect, with rounded corners of this radius in pixels; -1 for square.</summary>
        public static Image Paint(this RectTransform rect, Color colour, int radius)
        {
            if (!rect.TryGetComponent(out Image image)) image = rect.gameObject.AddComponent<Image>();
            image.color = colour;
            if (radius >= 0) { image.sprite = LobbyIcons.RoundedSprite(radius); image.type = Image.Type.Sliced; }
            return image;
        }

        /// <summary>Body text in Nunito ExtraBold; Bold asks for Nunito Black, not a faked bold.</summary>
        public static TextMeshProUGUI Text(Transform parent, string text, float size, Color colour,
            TextAlignmentOptions align = TextAlignmentOptions.Left, FontStyles style = FontStyles.Normal)
        {
            var label = Rect(parent, "Text").gameObject.AddComponent<TextMeshProUGUI>();
            var font = (style & FontStyles.Bold) != 0 ? LobbyFonts.Black : LobbyFonts.Body;
            if (font) { label.font = font; style &= ~FontStyles.Bold; }
            else if (GameAssets.I && GameAssets.I.font) label.font = GameAssets.I.font;
            label.text = text; label.fontSize = size; label.color = colour; label.alignment = align;
            label.fontStyle = style; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        /// <summary>Lilita One, the web's display face, optionally with the navy stroke or stroke and drop.</summary>
        public static TextMeshProUGUI Display(Transform parent, string text, float size, Color colour,
            TextAlignmentOptions align = TextAlignmentOptions.Center, Ink ink = Ink.Plain)
        {
            var label = Text(parent, text, size, colour, align);
            var font = LobbyFonts.Display;
            if (!font) { label.fontStyle = FontStyles.Bold; return label; }
            label.font = font;
            var material = ink == Ink.Stroke ? LobbyFonts.Stroke : ink == Ink.Drop ? LobbyFonts.Drop : null;
            if (material) label.fontSharedMaterial = material;
            return label;
        }

        /// <summary>The web's small caps: Nunito Black, spaced, in cyan.</summary>
        public static TextMeshProUGUI Caps(Transform parent, string text, float size = 15, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var label = Text(parent, Upper(text), size, Cyan, align, FontStyles.Bold);
            label.characterSpacing = 6;
            return label;
        }

        public static Image Icon(Transform parent, string icon, Color colour)
        {
            var image = Rect(parent, "Icon " + icon).gameObject.AddComponent<Image>();
            image.sprite = LobbyIcons.Get(icon); image.color = colour; image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        /// <summary>
        /// Paints a rect as a web sticker: a fill (a gradient when <paramref name="to"/> is given, top
        /// to bottom), an optional edge inside it and a hard drop shadow under it. Skew leans it like
        /// a tab. Mesh effects go skew, gradient, shadow, so the shadow copies the final shape.
        /// </summary>
        public static Image Face(RectTransform rect, Color fill, int radius, Color? edge = null, int edgeWidth = 0, float drop = 0,
            Color? to = null, float skew = 0, Color? dropColour = null)
        {
            var image = rect.Paint(to.HasValue ? Color.white : fill, radius);
            image.raycastTarget = false;
            if (skew != 0) rect.gameObject.AddComponent<LobbySkew>().Degrees = skew;
            if (to.HasValue) rect.gameObject.AddComponent<LobbyGradient>().Set(fill, to.Value);
            if (drop > 0) Drop(rect, drop, dropColour ?? Navy);
            if (edge.HasValue && edgeWidth > 0)
            {
                var frame = Frame(rect, edge.Value, radius, edgeWidth);
                if (skew != 0) frame.gameObject.AddComponent<LobbySkew>().Degrees = skew;
            }
            return image;
        }

        /// <summary>A hard shadow straight down, like the web's "0 4px 0 navy".</summary>
        public static Shadow Drop(RectTransform rect, float depth, Color colour)
        {
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = colour;
            shadow.effectDistance = new Vector2(0, -depth);
            // Opaque under see-through fills too: the web's shadow doesn't fade with the card.
            shadow.useGraphicAlpha = false;
            return shadow;
        }

        /// <summary>An edge of this width just inside a rect's rounded outline.</summary>
        public static Image Frame(RectTransform rect, Color colour, int radius, int width, string name = "Edge", float outset = 0)
        {
            var frame = Rect(rect, name).Place(Vector2.zero, Vector2.one, new Vector2(-outset, -outset), new Vector2(outset, outset));
            // Decoration, not content: a row or column it sits in leaves it alone.
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var image = frame.gameObject.AddComponent<Image>();
            image.sprite = LobbyIcons.FrameSprite(Mathf.Max(0, radius + Mathf.RoundToInt(outset)), width);
            image.type = Image.Type.Sliced;
            image.color = colour; image.raycastTarget = false;
            return image;
        }

        /// <summary>The web's "0 0 0 5px sun" ring just outside a chosen card.</summary>
        public static Image Ring(RectTransform rect, Color colour, int radius, int width) => Frame(rect, colour, radius, width, "Ring", width);

        public static LobbyGradient Gradient(Graphic graphic, Color from, Color to, bool horizontal = false)
        {
            graphic.color = Color.white;
            var gradient = graphic.gameObject.AddComponent<LobbyGradient>();
            gradient.Set(from, to, horizontal);
            return gradient;
        }

        /// <summary>A flat button whose body lifts under the pointer and sinks when pressed.</summary>
        public static Button Button(Transform parent, string name, Color colour, Action click, bool rounded = true) =>
            Button(parent, name, colour, click, rounded ? 12 : -1);

        /// <summary>
        /// A button with a sticker face (see <see cref="Face"/>). The root is a still, invisible hit box
        /// for layouts and raycasts; children go on <see cref="Body"/>, which moves.
        /// </summary>
        public static Button Button(Transform parent, string name, Color colour, Action click, int radius,
            Color? edge = null, int edgeWidth = 0, float drop = 0, Color? to = null, float skew = 0)
        {
            var rect = Rect(parent, name);
            var hit = rect.Paint(Color.clear);
            var body = Rect(rect, "Body").Fill();
            Face(body, colour, radius, edge, edgeWidth, drop, to, skew);
            var button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            var press = rect.gameObject.AddComponent<LobbyPress>();
            press.Body = body;
            if (drop > 0) { press.Drop = body.GetComponent<Shadow>(); press.DropRest = drop; }
            // Keyboard and controller focus: the web's 3px orange ring, a little outside the button.
            var ring = Frame(body, Focus, Mathf.Max(radius, 0), 3, "Focus", 7);
            if (skew != 0) ring.gameObject.AddComponent<LobbySkew>().Degrees = skew;
            ring.enabled = false;
            press.Ring = ring;
            button.onClick.AddListener(() => click?.Invoke());
            return button;
        }

        /// <summary>What moves inside a lobby button: put its text and art here.</summary>
        public static RectTransform Body(this Selectable button) =>
            button.TryGetComponent(out LobbyPress press) && press.Body ? press.Body : (RectTransform)button.transform;

        public static Image FaceOf(this Selectable button) => button.Body().GetComponent<Image>();

        /// <summary>A button with one centred label.</summary>
        public static Button LabelButton(Transform parent, string title, Color colour, Color text, float size, Action click)
        {
            var button = Button(parent, title, colour, click);
            Display(button.Body(), title, size, text).rectTransform.Fill();
            return button;
        }

        /// <summary>The web's secondary pill (CHANGE, CANCEL, STAY): see-through white, sun under the pointer.</summary>
        public static Button Pill(Transform parent, string name, string text, float size, Action click)
        {
            var button = Button(parent, name, PillFill, click, 10, PillEdge, 2);
            var body = button.Body();
            var label = Display(body, Upper(text), size, Cream);
            label.characterSpacing = 2;
            label.rectTransform.Fill();
            var face = body.GetComponent<Image>();
            var edge = body.Find("Edge").GetComponent<Image>();
            button.GetComponent<LobbyPress>().Hot = hot =>
            {
                face.color = hot ? Sun : PillFill;
                edge.color = hot ? Navy : PillEdge;
                label.color = hot ? Navy : Cream;
            };
            return button;
        }

        /// <summary>The red button that does something you can't take back (QUIT GAME).</summary>
        public static Button Danger(Transform parent, string title, float size, Action click)
        {
            var button = Button(parent, title, Hot, click, 12, Navy, 3, 4);
            Display(button.Body(), title, size, Cream, TextAlignmentOptions.Center, Ink.Stroke).rectTransform.Fill();
            return button;
        }

        /// <summary>
        /// The web's PLAY button: a sun slab with a navy edge and a deep drop, the word spelled in
        /// wooden letter tiles and an arrow after it. It fades when it can't be pressed.
        /// </summary>
        public static Button Primary(Transform parent, string name, string word, Action click, float tile = 62, float arrow = 38)
        {
            var button = Button(parent, name, PlayHi, click, 16, Navy, 4, 8, Sun2);
            var press = button.GetComponent<LobbyPress>();
            press.Sink = 6f; press.DropPressed = 2f;
            var content = Row(button.Body(), "Content", 7);
            content.Fill();
            var layout = content.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandHeight = false;
            for (int i = 0; i < word.Length; i++)
                LetterTile(content, word[i], tile, (i - (word.Length - 1) / 2f) * 3f);
            Rect(content, "Gap").Size(7, tile);
            Icon(content, LobbyIcons.Arrow, Navy).Size(arrow, arrow);
            return button;
        }

        /// <summary>A honey-wood letter tile with a cocoa edge, as on the art boards.</summary>
        public static RectTransform LetterTile(Transform parent, char letter, float size, float degrees = 0) =>
            LetterTile(parent, letter.ToString(), size, degrees);

        /// <summary>The same tile with a short word or number on it (the career level).</summary>
        public static RectTransform LetterTile(Transform parent, string letter, float size, float degrees = 0)
        {
            var tile = Rect(parent, "Tile " + letter);
            tile.Size(size, size);
            int radius = Mathf.RoundToInt(size * .22f);
            Face(tile, WoodHi, radius, Cocoa, 2, 3, WoodLo, dropColour: Cocoa);
            var shine = Rect(tile, "Shine").Place(new Vector2(0, 1), Vector2.one, new Vector2(size * .2f, -size * .14f), new Vector2(-size * .2f, -size * .1f));
            shine.Paint(new Color(1, 1, 1, .6f), 1).raycastTarget = false;
            Display(tile, letter, size * .7f, WoodInk).rectTransform.Fill();
            tile.localRotation = Quaternion.Euler(0, 0, -degrees);
            return tile;
        }

        /// <summary>The web's coin: a sun disc with an ink rim, a paler middle and a W.</summary>
        public static RectTransform Coin(Transform parent, float size)
        {
            var coin = Rect(parent, "Coin");
            coin.Size(size, size);
            // Diameters from the web's 24-unit coin: r 10.2 with a 1.6 stroke, then r 7.2 with 1.2.
            Disc(coin, "Rim", size * 22f / 24f, CoinInk);
            Disc(coin, "Face", size * 18.8f / 24f, Sun);
            Disc(coin, "Ring", size * 15.6f / 24f, CoinRim);
            Disc(coin, "Middle", size * 13.2f / 24f, CoinRimHi);
            var w = Icon(coin, LobbyIcons.CoinW, CoinInk);
            w.rectTransform.Fill();
            return coin;
        }

        public static Image Disc(Transform parent, string name, float diameter, Color colour)
        {
            var disc = Rect(parent, name).Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(diameter, diameter));
            var image = disc.Paint(colour, Mathf.Max(1, Mathf.RoundToInt(diameter / 2f)));
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A craftable item's picture from Resources/UI/Items, or null when there is none.</summary>
        public static RawImage ItemImage(Transform parent, string id, float size, float degrees = 0)
        {
            var texture = Resources.Load<Texture2D>("UI/Items/" + id);
            if (!texture) return null;
            var rect = Rect(parent, "Art " + id).Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(size, size));
            rect.localRotation = Quaternion.Euler(0, 0, degrees);
            var raw = rect.gameObject.AddComponent<RawImage>();
            raw.texture = texture; raw.raycastTarget = false;
            return raw;
        }

        /// <summary>Each mode's poster colour, as the web lobby gives it.</summary>
        public static Color ModeColour(string mode) => mode switch
        {
            "Dibs" => Hex(0xef5b2b),
            "Duos" => Hex(0x3fa9dd),
            "MovingOut" => Hex(0x6fa957),
            "MovingDay" => Hex(0x9471dc),
            "Tutorial" => Hex(0xf2b230),
            _ => Sun,
        };

        /// <summary>Each mode's poster art: a craftable item, as the web lobby gives it.</summary>
        public static string ModeArt(string mode) => mode switch
        {
            "Dibs" => "BAT",
            "Duos" => "BALL",
            "MovingDay" => "SOFA",
            "Tutorial" => "BOOK",
            "Workshop" => "HAMMER",
            _ => "BOX",
        };

        /// <summary>A leaning web tab with an icon and a label; LobbyTab.On shows it open.</summary>
        public static LobbyTab Tab(Transform parent, string title, string icon, Action click, float width = 200, float height = 54, float size = 27)
        {
            var button = Button(parent, title, TabIdle, click, TabRadius, Line, 2, 0, null, 10);
            var body = button.Body();
            // Only the open tab shows these; LobbyTab turns them on.
            var gradient = body.gameObject.AddComponent<LobbyGradient>();
            gradient.Set(SunHi, Sun2);
            gradient.enabled = false;
            var drop = Drop(body, 4, Navy);
            drop.enabled = false;
            var content = Row(body, "Content", 10);
            content.Fill();
            var layout = content.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandHeight = false;
            Image glyph = null;
            if (icon != null) { glyph = Icon(content, icon, Cream); glyph.Size(26, 26); }
            var label = Display(content, Upper(title), size, Cream);
            label.characterSpacing = 3;
            label.Size(-1, height);
            button.Size(width, height);
            var tab = button.gameObject.AddComponent<LobbyTab>();
            tab.Init(button, body.GetComponent<Image>(), body.Find("Edge").GetComponent<Image>(), gradient, drop, label, glyph);
            return tab;
        }

        /// <summary>A round white button for the top bar with a navy glyph, and a hint underneath it.</summary>
        public static Button IconButton(Transform parent, string icon, string hint, Action click)
        {
            var button = Button(parent, hint, Color.white, click, 26, Navy, 3, 3);
            var body = button.Body();
            var glyph = Icon(body, icon, Navy);
            glyph.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(12, 12), new Vector2(-12, -12));
            var face = body.GetComponent<Image>();
            var press = button.GetComponent<LobbyPress>();
            // The web's rotate(-8deg): CSS turns clockwise for positive angles, uGUI anticlockwise.
            press.Tilt = 8f;
            press.Hot = hot => face.color = hot ? Sun : Color.white;
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

        /// <summary>A section heading: cyan Lilita capitals, as the web's locker labels.</summary>
        public static TextMeshProUGUI Heading(Transform parent, string text)
        {
            var label = Display(parent, Upper(text), 24, Cyan, TextAlignmentOptions.BottomLeft);
            label.characterSpacing = 3;
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

        /// <summary>A choice chip: a sun slab with a navy edge when chosen, see-through glass when not,
        /// and faded type when it's locked.</summary>
        public static Button Chip(Transform parent, string title, bool on, Action click, bool locked = false, float size = 22)
        {
            var button = on
                ? Button(parent, title, SunHi, click, 10, Navy, 3, 4, Sun2)
                : Button(parent, title, Card, click, 10, ChipEdge, 2);
            var label = Display(button.Body(), title, size, on ? Navy : locked ? Faded : Cream);
            label.characterSpacing = 1;
            label.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, 0));
            if (on) button.GetComponent<LobbyPress>().Lift = 0f;
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
