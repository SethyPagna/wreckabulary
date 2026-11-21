using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class ShoppingListHud : MonoBehaviour
    {
        public const string RootName = "Shopping list";
        const float Pad = 10f, Chip = 60f, ChipGap = 8f, Picture = 46f, Tick = 24f, Section = 18f, Tile = 28f, TileStep = 32f, TileHeight = 34f;
        const float CountWidth = 56f, PromptWidth = 190f, Height = Chip + 2f * Pad, TopGap = 8f, MinFont = 15f, CountFont = 22f, PromptFont = 20f, LetterFont = 20f;
        const float DoneAlpha = .5f;
        const int PanelRadius = 16, ChipRadius = 12, TileRadius = 6, RingWidth = 3;
        static readonly Color Backing = Hex(0x10292b, .62f), Gold = Hex(0xffe7b8), OnGame = Hex(0xfff7e8), ChipFill = Hex(0xffffff, .09f);
        static readonly Color Done = Hex(0xb7dfc8), Ring = Hex(0xffd46a);
        static readonly Color TileGot = Hex(0xe8c48b), TileGotInk = Hex(0x3b2614), TileMissingInk = Hex(0xffffff, .65f), TileRim = Hex(0xfff4e2, .55f);

        sealed class Item
        {
            public RectTransform root;
            public RawImage picture;
            public Image ring, tick;
            public CanvasGroup fade;
            public string word = "", text = "";
        }

        GameHud hud;
        IShoppingListSource source;
        RectTransform root, tray;
        TextMeshProUGUI heading, count, prompt;
        readonly List<Item> items = new();
        readonly List<(Image face, Image rim, TextMeshProUGUI letter)> tiles = new();
        readonly List<char> spare = new();
        long shownKey = long.MinValue;
        int builtFor = -1, shownTiles;

        public RectTransform Root => root;
        public string HeadingText => heading ? heading.text : "";
        public string PromptText => prompt ? prompt.text : "";
        public IReadOnlyList<string> LineTexts
        {
            get
            {
                var all = new List<string>();
                foreach (var item in items) if (item.root.gameObject.activeSelf) all.Add(item.text);
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
        }

        bool Build()
        {
            if (root) return true;
            if (!hud.UiCanvas) return false;
            var canvas = hud.UiCanvas.transform;
            var parent = canvas.Find("Safe HUD") ?? canvas;
            root = Rect(RootName, parent, new Vector2(.5f, 1f), new Vector2(.5f, 1f), Vector2.zero, new Vector2(400f, Height));
            Paint(root.gameObject, LobbyIcons.RoundedSprite(PanelRadius), Backing);
            heading = Text("Heading", root, LobbyFonts.Black, CountFont, Gold, TextAlignmentOptions.Center);
            heading.text = "YOUR LIST";
            heading.gameObject.SetActive(false);
            count = Text("Count", root, LobbyFonts.Black, CountFont, Gold, TextAlignmentOptions.Center);
            Place(count.rectTransform, Pad, new Vector2(CountWidth, Chip));
            tray = Rect("Letters", root, new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, new Vector2(0f, TileHeight));
            prompt = Text("Prompt", root, LobbyFonts.Body, PromptFont, Gold, TextAlignmentOptions.MidlineLeft);
            root.gameObject.SetActive(false);
            return true;
        }

        void LateUpdate()
        {
            if (!hud || !Build()) return;
            var you = hud.LocalPlayer;
            var race = source?.Race;
            bool shown = you && race != null && race.Items.Count > 0 && source.ShowsList && !hud.ResultShown && !hud.CountdownShown && !hud.BagOpen;
            if (root.gameObject.activeSelf != shown) root.gameObject.SetActive(shown);
            if (!shown) return;
            var spot = new Vector2(0f, -(hud.BarShown ? hud.BarBottom + PlayerBar.Clearance : hud.TopInset + TopGap));
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
            if (builtFor != total) BuildItems(total);
            count.text = ShoppingList.Progress(done, total);
            float x = Pad + CountWidth + ChipGap;
            for (int i = 0; i < total; i++)
            {
                var item = items[i];
                var wanted = race.Items[i];
                bool ticked = i < done, current = i == done;
                if (item.word != wanted.Word) { item.word = wanted.Word; Wreckabulary.UI.ItemArt.Apply(item.picture, wanted.Word); }
                item.text = ticked ? $"<s>{wanted.Word}</s>" : wanted.Word;
                item.tick.enabled = ticked;
                item.ring.enabled = current;
                item.fade.alpha = ticked ? DoneAlpha : 1f;
                item.root.anchoredPosition = new Vector2(x, 0f);
                x += Chip + ChipGap;
            }
            x += Section - ChipGap;
            var next = race.Current(seat);
            tray.gameObject.SetActive(next != null);
            if (next != null)
            {
                tray.anchoredPosition = new Vector2(x, 0f);
                ShowTiles(next.Value.Word, you.Inventory.Letters);
                x += shownTiles * TileStep + ChipGap;
            }
            prompt.text = next != null ? ShoppingList.Prompt(next.Value, race.CurrentSpelled(seat)) : "List done!";
            float wide = Mathf.Min(PromptWidth, prompt.GetPreferredValues(prompt.text).x + 4f);
            Place(prompt.rectTransform, x, new Vector2(wide, Chip));
            root.sizeDelta = new Vector2(x + wide + Pad, Height);
        }

        void BuildItems(int total)
        {
            builtFor = total;
            for (int i = items.Count; i < total; i++)
            {
                var item = new Item { root = Rect($"Item {i + 1}", root, new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, Vector2.one * Chip) };
                Paint(item.root.gameObject, LobbyIcons.RoundedSprite(ChipRadius), ChipFill);
                item.fade = item.root.gameObject.AddComponent<CanvasGroup>();
                item.fade.blocksRaycasts = item.fade.interactable = false;
                var art = Rect("Picture", item.root, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Vector2.one * Picture);
                item.picture = art.gameObject.AddComponent<RawImage>();
                item.picture.raycastTarget = false;
                var ring = Rect("Ring", item.root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                ring.anchorMax = Vector2.one; ring.offsetMin = ring.offsetMax = Vector2.zero;
                item.ring = Paint(ring.gameObject, LobbyIcons.FrameSprite(ChipRadius, RingWidth), Ring);
                var tick = Rect("Tick", item.root, Vector2.one, Vector2.one, new Vector2(4f, 4f), Vector2.one * Tick);
                item.tick = Paint(tick.gameObject, LobbyIcons.Get(LobbyIcons.Check), Done);
                item.tick.type = Image.Type.Simple; item.tick.preserveAspect = true;
                items.Add(item);
            }
            for (int i = 0; i < items.Count; i++) items[i].root.gameObject.SetActive(i < total);
        }

        void ShowTiles(string word, IReadOnlyList<char> letters)
        {
            while (tiles.Count < word.Length)
            {
                var face = Rect($"Tile {tiles.Count + 1}", tray, new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, new Vector2(Tile, TileHeight));
                var faceImage = Paint(face.gameObject, LobbyIcons.RoundedSprite(TileRadius), TileGot);
                var rim = Rect("Rim", face, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                rim.anchorMax = Vector2.one; rim.offsetMin = rim.offsetMax = Vector2.zero;
                var rimImage = Paint(rim.gameObject, LobbyIcons.FrameSprite(TileRadius, 1), TileRim);
                var letter = Text("Letter", face, LobbyFonts.Black, LetterFont, TileMissingInk, TextAlignmentOptions.Center);
                letter.rectTransform.anchorMin = Vector2.zero; letter.rectTransform.anchorMax = Vector2.one;
                letter.rectTransform.offsetMin = letter.rectTransform.offsetMax = Vector2.zero;
                tiles.Add((faceImage, rimImage, letter));
            }
            spare.Clear();
            for (int i = 0; i < letters.Count; i++) spare.Add(char.ToUpperInvariant(letters[i]));
            shownTiles = word.Length;
            for (int i = 0; i < tiles.Count; i++)
            {
                var (face, rim, letter) = tiles[i];
                bool used = i < word.Length;
                face.gameObject.SetActive(used);
                if (!used) continue;
                face.rectTransform.anchoredPosition = new Vector2(i * TileStep, 0f);
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

        static RectTransform Rect(string label, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rt = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        static void Place(RectTransform rt, float left, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, .5f);
            rt.anchoredPosition = new Vector2(left, 0f);
            rt.sizeDelta = size;
        }

        static Image Paint(GameObject host, Sprite sprite, Color colour)
        {
            var image = host.AddComponent<Image>();
            image.sprite = sprite; image.type = Image.Type.Sliced; image.color = colour; image.raycastTarget = false;
            return image;
        }

        static TextMeshProUGUI Text(string label, Transform parent, TMP_FontAsset font, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var rt = Rect(label, parent, new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, Vector2.zero);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var chosen = font ? font : GameAssets.I ? GameAssets.I.font : TMP_Settings.defaultFontAsset;
            if (chosen) text.font = chosen;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true; text.fontSizeMin = Mathf.Min(MinFont, fontSize); text.fontSizeMax = fontSize;
            text.raycastTarget = false;
            return text;
        }
    }

    public interface IShoppingListSource
    {
        ShoppingRace Race { get; }
        IReadOnlyList<PlayerController> Players { get; }
        bool ShowsList { get; }
    }
}
