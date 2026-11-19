using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class RiddleRushHud : MonoBehaviour
    {
        const int MaxTiles = 10;
        const float TileSize = 34f, TileGap = 6f, Width = 720f, Height = 98f, Top = 12f;
        static readonly Color Backing = new(.07f, .16f, .16f, .86f), Ink = new(1f, .97f, .91f), Muted = new(1f, .96f, .88f, .78f);
        static readonly Color Blank = new(1f, 1f, 1f, .14f), Filled = new(.96f, .86f, .65f), Hinted = new(1f, .83f, .42f), TileInk = new(.09f, .23f, .24f);

        GameHud hud;
        RiddleRushDirector director;
        RectTransform root;
        TextMeshProUGUI clue, line;
        readonly List<Image> faces = new();
        readonly List<TextMeshProUGUI> letters = new();
        Sprite rounded;
        Texture2D texture;
        Riddle shownRiddle;
        RiddleStage shownStage;
        int shownSeconds = int.MinValue, shownSolver = int.MinValue;
        bool shownHint;

        public bool Shown => root && root.gameObject.activeSelf;
        public string ClueText => clue ? clue.text : "";
        public string LineText => line ? line.text : "";
        public string TileText
        {
            get
            {
                var text = new StringBuilder();
                for (int i = 0; i < letters.Count && faces[i].gameObject.activeSelf; i++) text.Append(letters[i].text.Length > 0 ? letters[i].text : "_");
                return text.ToString();
            }
        }

        public void Configure(GameHud display, RiddleRushDirector source)
        {
            hud = display;
            director = source;
            if (!root && hud && hud.UiCanvas) Build();
        }

        void OnDestroy()
        {
            if (root) Destroy(root.gameObject);
            if (rounded) Destroy(rounded);
            if (texture) Destroy(texture);
        }

        void Build()
        {
            var canvas = hud && hud.UiCanvas ? hud.UiCanvas.transform : null;
            if (!canvas) return;
            var safe = canvas.Find("Safe HUD");
            var parent = safe ? safe : canvas;
            rounded = Rounded();
            root = Box("Riddle panel", parent, new Vector2(.5f, 1f), new Vector2(0f, -Top - hud.TopInset), new Vector2(Width, Height), Backing);
            root.pivot = new Vector2(.5f, 1f);
            clue = Label("Clue", root, new Vector2(0f, -8f), new Vector2(Width - 32f, 30f), 24f, Ink);
            clue.enableAutoSizing = true; clue.fontSizeMin = 16f; clue.fontSizeMax = 24f;
            for (int i = 0; i < MaxTiles; i++)
            {
                var face = Box("Tile " + (i + 1), root, new Vector2(.5f, 1f), new Vector2(0f, -42f), new Vector2(TileSize, TileSize), Blank);
                face.pivot = new Vector2(.5f, 1f);
                var letter = Label("Letter", face, Vector2.zero, new Vector2(TileSize, TileSize), 22f, TileInk);
                letter.rectTransform.anchorMin = letter.rectTransform.anchorMax = new Vector2(.5f, .5f);
                letter.rectTransform.pivot = new Vector2(.5f, .5f);
                letter.fontStyle = FontStyles.Bold;
                faces.Add(face.GetComponent<Image>());
                letters.Add(letter);
            }
            line = Label("Line", root, new Vector2(0f, -78f), new Vector2(Width - 32f, 18f), 17f, Muted);
            line.enableAutoSizing = true; line.fontSizeMin = 13f; line.fontSizeMax = 17f;
            root.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (!root && hud && hud.UiCanvas) Build();
            if (!root) return;
            var race = director ? director.Race : null;
            bool show = race != null && race.Current != null && director.Playing && hud
                && !hud.BagOpen && !hud.ResultShown && !hud.BannerShown && !hud.CountdownShown;
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show) return;
            float y = -Top - hud.TopInset;
            if (root.anchoredPosition.y != y) root.anchoredPosition = new Vector2(0f, y);
            Refresh(race);
        }

        void Refresh(RiddleRace race)
        {
            var riddle = race.Current;
            int seconds = race.Stage == RiddleStage.Asking && !race.HintShown ? Mathf.CeilToInt(race.UntilHint) : -1;
            if (riddle == shownRiddle && race.Stage == shownStage && seconds == shownSeconds && race.HintShown == shownHint && race.Solver == shownSolver) return;
            bool fresh = riddle != shownRiddle || race.Stage != shownStage || race.HintShown != shownHint;
            shownRiddle = riddle; shownStage = race.Stage; shownSeconds = seconds; shownHint = race.HintShown; shownSolver = race.Solver;
            if (fresh) Tiles(race);
            clue.text = riddle.Clue;
            line.text = race.Stage != RiddleStage.Asking ? RiddleText.Spelled(director.SeatName(race.Solver), riddle.Answer)
                : race.HintShown ? RiddleText.StartsWith(riddle) : RiddleText.UntilHint(race.UntilHint);
        }

        void Tiles(RiddleRace race)
        {
            int count = Mathf.Min(MaxTiles, race.Current.Answer.Length);
            float start = -(count - 1) * (TileSize + TileGap) * .5f;
            for (int i = 0; i < faces.Count; i++)
            {
                bool used = i < count;
                if (faces[i].gameObject.activeSelf != used) faces[i].gameObject.SetActive(used);
                if (!used) continue;
                char shown = race.Tile(i);
                faces[i].rectTransform.anchoredPosition = new Vector2(start + i * (TileSize + TileGap), -42f);
                faces[i].color = shown == '\0' ? Blank : race.Stage == RiddleStage.Asking ? Hinted : Filled;
                letters[i].text = shown == '\0' ? "" : shown.ToString();
            }
        }

        RectTransform Box(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var rt = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor; rt.anchoredPosition = position; rt.sizeDelta = size;
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = rounded; image.type = Image.Type.Sliced; image.color = color; image.raycastTarget = false;
            return rt;
        }

        static TextMeshProUGUI Label(string label, Transform parent, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var rt = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, 1f);
            rt.anchoredPosition = position; rt.sizeDelta = size;
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = LobbyFonts.Body ? LobbyFonts.Body : GameAssets.I ? GameAssets.I.font : TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize; text.color = color; text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false;
            return text;
        }

        Sprite Rounded()
        {
            const int size = 32;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Riddle panel", filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x - 15.5f) - 8f, 0f), dy = Mathf.Max(Mathf.Abs(y - 15.5f) - 8f, 0f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(.5f - (Mathf.Sqrt(dx * dx + dy * dy) - 7f)));
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(10f, 10f, 10f, 10f));
        }
    }
}
