using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class ShoppingListHud : MonoBehaviour
    {
        const float Left = 38f, Width = 280f, Head = 30f, Row = 28f, Tile = 24f, TileStep = 28f, Gap = 8f;
        static readonly Color Backing = Hex(0x10292b, .62f), Band = Hex(0xffd46a, .22f), Gold = Hex(0xffe7b8), OnGame = Hex(0xfff7e8);
        static readonly Color Done = Hex(0xb7dfc8), Faded = Hex(0xfff7e8, .55f), Pending = Hex(0xfff7e8, .82f);
        static readonly Color TileGot = Hex(0xe8c48b), TileGotInk = Hex(0x3b2614), TileMissingInk = Hex(0xffffff, .65f), TileRim = Hex(0xfff4e2, .55f);

        sealed class Line
        {
            public RectTransform root;
            public Image band, mark;
            public TextMeshProUGUI word;
        }

        GameHud hud;
        IShoppingListSource source;
        RectTransform root, tray;
        TextMeshProUGUI heading, count, prompt;
        readonly List<Line> lines = new();
        readonly List<(Image face, Image rim, TextMeshProUGUI letter)> tiles = new();
        readonly List<char> spare = new();
        readonly List<Object> owned = new();
        Sprite panel, ring, dot;
        long shownKey = long.MinValue;
        int builtFor = -1;

        public RectTransform Root => root;
        public string HeadingText => heading ? heading.text : "";
        public string PromptText => prompt ? prompt.text : "";
        public IReadOnlyList<string> LineTexts
        {
            get
            {
                var all = new List<string>();
                foreach (var line in lines) if (line.root.gameObject.activeSelf) all.Add(line.word.text);
                return all;
            }
        }

        public static ShoppingListHud Attach(GameHud hud, IShoppingListSource source)
        {
            if (!hud.TryGetComponent(out ShoppingListHud view)) view = hud.gameObject.AddComponent<ShoppingListHud>();
            view.hud = hud;
            view.source = source;
            return view;
        }

        void OnDestroy()
        {
            if (root) Destroy(root.gameObject);
            foreach (var o in owned) if (o) Destroy(o);
            owned.Clear();
        }

        bool Build()
        {
            if (root) return true;
            if (!hud.UiCanvas) return false;
            var canvas = hud.UiCanvas.transform;
            var parent = canvas.Find("Safe HUD") ?? canvas;
            panel = Shape(0f);
            ring = Shape(4.4f);
            dot = Shape(0f, true);
            root = Rect("Shopping list", parent, new Vector2(0f, 1f), new Vector2(Left, -hud.LeftColumnStart), new Vector2(Width, 200f));
            root.pivot = new Vector2(0f, 1f);
            var back = root.gameObject.AddComponent<Image>();
            back.sprite = panel; back.type = Image.Type.Sliced; back.color = Backing; back.raycastTarget = false;
            heading = Text("Heading", root, new Vector2(14f, -6f), new Vector2(180f, 22f), 16f, Gold, TextAlignmentOptions.Left);
            heading.characterSpacing = 1f;
            heading.text = "SHOPPING LIST";
            count = Text("Count", root, new Vector2(Width - 194f, -6f), new Vector2(180f, 22f), 18f, OnGame, TextAlignmentOptions.Right);
            tray = Rect("Letters", root, new Vector2(0f, 1f), Vector2.zero, new Vector2(Width, Tile + 8f));
            tray.pivot = new Vector2(0f, 1f);
            prompt = Text("Prompt", root, Vector2.zero, new Vector2(Width - 28f, 22f), 17f, Gold, TextAlignmentOptions.Left);
            root.gameObject.SetActive(false);
            return true;
        }

        void LateUpdate()
        {
            if (!hud || !Build()) return;
            var you = hud.LocalPlayer;
            var race = source?.Race;
            bool shown = you && race != null && race.Items.Count > 0 && source.ShowsList && !hud.ResultShown;
            if (root.gameObject.activeSelf != shown) root.gameObject.SetActive(shown);
            if (!shown) return;
            var spot = new Vector2(Left, -hud.LeftColumnStart);
            if (root.anchoredPosition != spot) root.anchoredPosition = spot;
            long key = Key(race, you);
            if (key == shownKey) return;
            shownKey = key;
            Refresh(race, you);
        }

        long Key(ShoppingRace race, PlayerController you)
        {
            unchecked
            {
                long key = race.GetHashCode() * 31L + you.Index;
                key = key * 31 + race.Done(you.Index) * 2 + (race.CurrentSpelled(you.Index) ? 1 : 0);
                var letters = you.Inventory.Letters;
                for (int i = 0; i < letters.Count; i++) key = key * 31 + letters[i];
                return key * 31 + source.Players.Count;
            }
        }

        void Refresh(ShoppingRace race, PlayerController you)
        {
            int seat = you.Index, done = race.Done(seat), total = race.Items.Count;
            if (builtFor != total) BuildLines(total);
            count.text = ShoppingList.Progress(done, total);
            for (int i = 0; i < total; i++)
            {
                var line = lines[i];
                var item = race.Items[i];
                bool ticked = i < done, current = i == done;
                line.band.enabled = current;
                line.mark.sprite = ticked ? dot : ring;
                line.mark.color = ticked ? Done : current ? Gold : Faded;
                line.word.text = ticked ? $"<s>{item.Word}</s>" : item.Word;
                line.word.color = ticked ? Faded : current ? Gold : Pending;
            }
            float y = -Head - total * Row - Gap * .5f;
            var wanted = race.Current(seat);
            tray.gameObject.SetActive(wanted != null);
            if (wanted != null)
            {
                tray.anchoredPosition = new Vector2(0f, y);
                ShowTiles(wanted.Value.Word, you.Inventory.Letters);
                y -= Tile + 8f + 4f;
            }
            prompt.rectTransform.anchoredPosition = new Vector2(14f, y);
            prompt.text = wanted != null ? ShoppingList.Prompt(wanted.Value, race.CurrentSpelled(seat)) : "List done!";
            y -= 24f;
            root.sizeDelta = new Vector2(Width, -y + Gap);
        }

        void BuildLines(int total)
        {
            builtFor = total;
            for (int i = lines.Count; i < total; i++)
            {
                var rt = Rect($"Item {i + 1}", root, new Vector2(0f, 1f), new Vector2(6f, -Head - i * Row), new Vector2(Width - 12f, Row - 2f));
                rt.pivot = new Vector2(0f, 1f);
                var band = rt.gameObject.AddComponent<Image>();
                band.sprite = panel; band.type = Image.Type.Sliced; band.color = Band; band.raycastTarget = false;
                var mark = NewImage("Mark", rt, new Vector2(10f, -(Row - 2f) * .5f + 7f), new Vector2(14f, 14f));
                var word = Text("Word", rt, new Vector2(32f, -2f), new Vector2(Width - 60f, Row - 4f), 19f, Pending, TextAlignmentOptions.Left);
                lines.Add(new Line { root = rt, band = band, mark = mark, word = word });
            }
            for (int i = 0; i < lines.Count; i++) lines[i].root.gameObject.SetActive(i < total);
        }

        void ShowTiles(string word, IReadOnlyList<char> letters)
        {
            while (tiles.Count < word.Length)
            {
                var face = NewImage($"Tile {tiles.Count + 1}", tray, Vector2.zero, new Vector2(Tile, Tile + 6f));
                face.sprite = panel; face.type = Image.Type.Sliced; face.pixelsPerUnitMultiplier = 11f / 3.75f;
                var rim = NewImage("Rim", face.rectTransform, Vector2.zero, Vector2.zero);
                rim.rectTransform.anchorMin = Vector2.zero; rim.rectTransform.anchorMax = Vector2.one;
                rim.rectTransform.offsetMin = rim.rectTransform.offsetMax = Vector2.zero;
                rim.sprite = ring; rim.type = Image.Type.Sliced; rim.pixelsPerUnitMultiplier = face.pixelsPerUnitMultiplier; rim.color = TileRim;
                var letter = Text("Letter", face.rectTransform, Vector2.zero, new Vector2(Tile, Tile + 6f), 20f, TileMissingInk, TextAlignmentOptions.Center);
                tiles.Add((face, rim, letter));
            }
            spare.Clear();
            for (int i = 0; i < letters.Count; i++) spare.Add(char.ToUpperInvariant(letters[i]));
            for (int i = 0; i < tiles.Count; i++)
            {
                var (face, rim, letter) = tiles[i];
                bool used = i < word.Length;
                face.gameObject.SetActive(used);
                if (!used) continue;
                face.rectTransform.anchoredPosition = new Vector2(14f + i * TileStep, -4f);
                int at = spare.IndexOf(word[i]);
                bool got = at >= 0;
                if (got) spare.RemoveAt(at);
                face.color = got ? TileGot : Color.clear;
                rim.enabled = !got;
                letter.text = word[i].ToString();
                letter.color = got ? TileGotInk : TileMissingInk;
            }
        }

        static Color Hex(int rgb, float a = 1f) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        static RectTransform Rect(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rt = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        static Image NewImage(string label, Transform parent, Vector2 position, Vector2 size)
        {
            var rt = Rect(label, parent, new Vector2(0f, 1f), position, size);
            rt.pivot = new Vector2(0f, 1f);
            var image = rt.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        static TextMeshProUGUI Text(string label, Transform parent, Vector2 position, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var rt = Rect(label, parent, new Vector2(0f, 1f), position, size);
            rt.pivot = new Vector2(0f, 1f);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = LobbyFonts.Body ? LobbyFonts.Body : GameAssets.I ? GameAssets.I.font : TMP_Settings.defaultFontAsset;
            if (font) text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.outlineWidth = .14f;
            text.outlineColor = new Color32(0x0b, 0x1f, 0x1f, 0xb0);
            return text;
        }

        Sprite Shape(float rim, bool circle = false)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Shopping list shape", filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - 31.5f), dy = Mathf.Abs(y - 31.5f);
                    float distance = circle ? Mathf.Sqrt(dx * dx + dy * dy) - 30f : new Vector2(Mathf.Max(dx - 20f, 0f), Mathf.Max(dy - 20f, 0f)).magnitude - 11f;
                    float alpha = Mathf.Clamp01(.5f - distance);
                    if (rim > 0f) alpha *= Mathf.Clamp01(.5f + distance + rim);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect,
                circle ? Vector4.zero : new Vector4(14f, 14f, 14f, 14f));
            owned.Add(sprite);
            owned.Add(texture);
            return sprite;
        }
    }

    public interface IShoppingListSource
    {
        ShoppingRace Race { get; }
        IReadOnlyList<PlayerController> Players { get; }
        bool ShowsList { get; }
    }
}
