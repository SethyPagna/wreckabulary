using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public partial class GameHud
    {
        public const string ScoreboardKey = "~", PadScoreboardKey = "VIEW", YourSide = "YOUR TEAM";
        const int BoardLines = PlayerBar.MostSeats, BoardBlocks = 2, BoardColumns = 5;
        const float BoardWidth = 1000f, BoardPad = 22f, BoardHead = 40f, BoardRow = 46f, BoardGap = 12f, BoardInset = 6f, BoardMargin = 16f;
        const float BoardTotal = 132f, BoardRank = 48f, BoardFace = 36f, BoardFaceGap = 10f, BoardColumn = 100f, BoardSideLabel = 22f;
        const float BoardTitleSize = 26f, BoardLabelSize = 15f, BoardNameSize = 24f, BoardNumberSize = 24f, BoardTotalSize = 60f, BoardMinText = 15f;
        const float BoardOutAlpha = .5f;
        const int BoardRadius = 22, BlockRadius = 14, BandRadius = 10;

        sealed class BoardLine
        {
            public RectTransform root, band;
            public Image bandFace, disc;
            public RawImage face;
            public TextMeshProUGUI rank, letter, name;
            public readonly TextMeshProUGUI[] numbers = new TextMeshProUGUI[BoardColumns];
            public readonly int[] shown = new int[BoardColumns];
            public int shownRank = int.MinValue;
            public float lead = -1f;
            public PlayerController seat;
        }

        sealed class BoardBlock
        {
            public RectTransform root;
            public TextMeshProUGUI total, side;
            public int shownTotal = int.MinValue, shownTeam = int.MinValue;
            public bool shownYours;
        }

        readonly BoardLine[] boardLines = new BoardLine[BoardLines];
        readonly BoardBlock[] boardBlocks = new BoardBlock[BoardBlocks];
        readonly List<ScoreSeat> boardSeats = new();
        readonly List<string> boardTexts = new();
        GameObject boardRoot;
        RectTransform boardPanel;
        TextMeshProUGUI boardTitle;
        bool boardPinned;

        public bool ScoreboardShown => boardRoot && boardRoot.activeSelf;
        public bool ScoreboardReady => boardRoot && AcceptsGameplayInput && BarSeated && !hubScene && !ResultShown && !LeaveAsked;
        public int ScoreboardBlocks { get; private set; }

        public IReadOnlyList<string> ScoreboardLines
        {
            get
            {
                boardTexts.Clear();
                if (!ScoreboardShown) return boardTexts;
                foreach (var line in boardLines)
                {
                    if (!line.root.gameObject.activeSelf) continue;
                    sb.Clear().Append(line.name.text);
                    foreach (var number in line.numbers) sb.Append(' ').Append(number.text);
                    boardTexts.Add(sb.ToString());
                }
                return boardTexts;
            }
        }

        public static string ScoreboardKeyLabel(InputBinding binding) => binding is GamepadBinding ? PadScoreboardKey : ScoreboardKey;

        public void ToggleScoreboard()
        {
            boardPinned = ScoreboardReady && !ScoreboardShown;
            UpdateScoreboard();
        }

        void UpdateScoreboard()
        {
            bool ready = ScoreboardReady && !BagOpen;
            if (!ready) boardPinned = false;
            ShowScoreboard(ready && (boardPinned || ScoreboardHeld()));
        }

        static bool ScoreboardHeld()
        {
            if (KeyBindings.Busy) return false;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.backquoteKey.isPressed) return true;
            foreach (var pad in Gamepad.all) if (pad.added && pad.selectButton.isPressed) return true;
            return false;
        }

        bool PadSelectShowsScoreboard()
        {
            if (!ScoreboardReady) return false;
            bool select = false;
            foreach (var pad in Gamepad.all)
            {
                if (pad.startButton.wasPressedThisFrame) return false;
                select |= pad.selectButton.wasPressedThisFrame;
            }
            return select;
        }

        void ShowScoreboard(bool on)
        {
            if (!boardRoot || boardRoot.activeSelf == on) return;
            boardRoot.SetActive(on);
            if (!on) return;
            boardTitle.text = MatchHeading(Match.Mode).name.ToUpperInvariant();
            RefreshScoreboard();
        }

        void BuildScoreboard()
        {
            boardPanel = Rect("Scoreboard", safe, new Vector2(.5f, .5f), Vector2.zero, new Vector2(BoardWidth, 2f * BoardPad + BoardHead));
            boardRoot = boardPanel.gameObject;
            var group = boardRoot.AddComponent<CanvasGroup>();
            group.blocksRaycasts = group.interactable = false;
            BoardFill("Face", boardPanel, Dark, BoardRadius);
            BoardFill("Edge", boardPanel, BagCardEdge, BoardRadius).sprite = LobbyIcons.FrameSprite(BoardRadius, 1);
            float inner = BoardWidth - 2f * BoardPad, columns = BoardColumns * BoardColumn;
            boardTitle = BoardText("Title", boardPanel, BoardTitleSize, BagInk, TextAlignmentOptions.MidlineLeft, LobbyFonts.Black);
            Box(boardTitle.rectTransform, BoardPad + BoardInset, BoardPad, inner - columns - BoardInset, BoardHead);
            for (int column = 0; column < BoardColumns; column++)
            {
                var label = BoardText("Column " + ScoreLine.Columns[column], boardPanel, BoardLabelSize, column == BoardColumns - 1 ? Gold : BagInk, TextAlignmentOptions.Center, LobbyFonts.Black);
                label.characterSpacing = 1f;
                label.text = ScoreLine.Columns[column];
                Box(label.rectTransform, BoardPad + inner - columns + column * BoardColumn, BoardPad, BoardColumn, BoardHead);
            }
            for (int block = 0; block < BoardBlocks; block++) boardBlocks[block] = MakeBoardBlock(block);
            for (int line = 0; line < BoardLines; line++) boardLines[line] = MakeBoardLine(line, inner);
            boardRoot.SetActive(false);
        }

        BoardBlock MakeBoardBlock(int index)
        {
            var root = Rect($"Block {index + 1}", boardPanel, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            root.pivot = new Vector2(0f, 1f);
            BoardFill("Face", root, BagCard, BlockRadius);
            BoardFill("Edge", root, BagCardEdge, BlockRadius).sprite = LobbyIcons.FrameSprite(BlockRadius, 1);
            var block = new BoardBlock { root = root };
            block.total = BoardText("Total", root, BoardTotalSize, BagInk, TextAlignmentOptions.Center, LobbyFonts.Display);
            block.side = BoardText("Side", root, BoardLabelSize, BagInk, TextAlignmentOptions.Center, LobbyFonts.Black);
            block.side.characterSpacing = 1f;
            return block;
        }

        BoardLine MakeBoardLine(int index, float width)
        {
            var root = Rect($"Row {index + 1}", boardPanel, new Vector2(0f, 1f), Vector2.zero, new Vector2(width, BoardRow));
            root.pivot = new Vector2(0f, 1f);
            var line = new BoardLine { root = root };
            line.band = Rect("Band", root, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            line.band.pivot = new Vector2(0f, 1f);
            line.bandFace = BoardFill("Face", line.band, BagCardActive, BandRadius);
            line.rank = BoardText("Rank", root, BoardNumberSize, BagInk, TextAlignmentOptions.Center, LobbyFonts.Display);
            line.disc = CreateImage("Disc", root, new Vector2(0f, 1f), Vector2.zero, new Vector2(BoardFace, BoardFace), Color.white);
            line.disc.rectTransform.pivot = new Vector2(0f, 1f);
            Square(line.disc);
            line.face = Rect("Portrait", line.disc.transform, Vector2.zero, Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
            Stretch(line.face.rectTransform);
            line.face.raycastTarget = false;
            line.letter = Text("Letter", line.disc.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(BoardFace, BoardFace), BoardNumberSize, Ink);
            line.letter.fontStyle = FontStyles.Bold;
            line.name = BoardText("Name", root, BoardNameSize, BagInk, TextAlignmentOptions.MidlineLeft, null);
            line.name.fontStyle = FontStyles.Bold;
            for (int column = 0; column < BoardColumns; column++)
            {
                var number = line.numbers[column] = BoardText("Number " + ScoreLine.Columns[column], root, BoardNumberSize, column == BoardColumns - 1 ? Gold : BagInk, TextAlignmentOptions.Center, null);
                Box(number.rectTransform, width - (BoardColumns - column) * BoardColumn, 0f, BoardColumn, BoardRow);
                line.shown[column] = int.MinValue;
            }
            root.gameObject.SetActive(false);
            return line;
        }

        Image BoardFill(string label, Transform parent, Color colour, int radius)
        {
            var image = CreateImage(label, parent, Vector2.zero, Vector2.zero, Vector2.zero, colour);
            Stretch(image.rectTransform);
            image.sprite = LobbyIcons.RoundedSprite(radius);
            image.type = Image.Type.Sliced;
            return image;
        }

        TextMeshProUGUI BoardText(string label, Transform parent, float size, Color colour, TextAlignmentOptions alignment, TMP_FontAsset font)
        {
            var text = Text(label, parent, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, size, colour, alignment);
            text.rectTransform.pivot = new Vector2(0f, 1f);
            if (font) text.font = font;
            text.enableAutoSizing = true; text.fontSizeMin = BoardMinText; text.fontSizeMax = size;
            return text;
        }

        static void Box(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
        }

        void RefreshScoreboard()
        {
            var you = LocalPlayer;
            int humans = 0;
            for (int i = 0; i < barSeats.Count; i++) if (SeatHuman(barSeats[i])) humans++;
            bool scored = Scored && barScores.Count == barSeats.Count;
            boardSeats.Clear();
            for (int i = 0; i < barSeats.Count; i++)
            {
                var p = barSeats[i];
                bool mine = you && (p == you || Teams.AreTeammates(p.Team, you.Team));
                boardSeats.Add(new ScoreSeat(p.Index, SeatTags.Name(p.Name, p == you, humans), p.Team, scored ? barScores[i] : 0, scored ? SideScore(i) : 0, scored, mine));
            }
            var board = ScoreLine.Board(boardSeats, MatchStats.Current);
            ScoreboardBlocks = board.Count;
            float inner = BoardWidth - 2f * BoardPad, y = BoardPad + BoardHead;
            int used = 0;
            for (int b = 0; b < BoardBlocks; b++)
            {
                var block = boardBlocks[b];
                bool shown = b < board.Count;
                if (block.root.gameObject.activeSelf != shown) block.root.gameObject.SetActive(shown);
                if (!shown) continue;
                var data = board[b];
                float height = 2f * BoardInset + data.Rows.Count * BoardRow;
                Box(block.root, BoardPad, y, inner, height);
                ShowBoardTotal(block, data, height, humans == 1);
                float lead = data.Ranked ? BoardRank : BoardTotal;
                for (int r = 0; r < data.Rows.Count && used < BoardLines; r++)
                    ShowBoardLine(boardLines[used++], data.Rows[r], data.Ranked ? r + 1 : 0, lead, inner, y + BoardInset + r * BoardRow);
                y += height + BoardGap;
            }
            for (int i = used; i < BoardLines; i++)
                if (boardLines[i].root.gameObject.activeSelf) boardLines[i].root.gameObject.SetActive(false);
            float tall = y - BoardGap + BoardPad;
            boardPanel.sizeDelta = new Vector2(BoardWidth, tall);
            var room = safe.rect;
            float fit = Mathf.Min(1f, (room.width - 2f * BoardMargin) / BoardWidth, (room.height - 2f * BoardMargin) / tall);
            boardPanel.localScale = Vector3.one * Mathf.Max(.1f, fit);
        }

        void ShowBoardTotal(BoardBlock block, ScoreBlock data, float height, bool solo)
        {
            bool sided = !data.Ranked;
            if (block.total.enabled != sided) block.total.enabled = sided;
            bool named = sided && data.Rows.Count > 1 && data.Team >= 0;
            if (block.side.enabled != named) block.side.enabled = named;
            if (!sided) return;
            float label = named ? BoardSideLabel : 0f;
            Box(block.total.rectTransform, 0f, BoardInset, BoardTotal, height - 2f * BoardInset - label);
            Box(block.side.rectTransform, 0f, height - BoardInset - BoardSideLabel, BoardTotal, BoardSideLabel);
            if (data.Total != block.shownTotal) { block.shownTotal = data.Total; block.total.text = data.Total.ToString(); }
            var captain = SideCaptain(data.Rows[0].Seat);
            block.total.color = captain ? SeatColour(captain) : BagInk;
            bool yours = solo && data.Mine;
            if (!named || (data.Team == block.shownTeam && yours == block.shownYours)) return;
            block.shownTeam = data.Team; block.shownYours = yours;
            block.side.text = yours ? YourSide : Teams.SideName(data.Team).ToUpperInvariant();
        }

        PlayerController SideCaptain(int seat)
        {
            int member = -1;
            for (int i = 0; i < barSeats.Count && member < 0; i++) if (barSeats[i].Index == seat) member = i;
            if (member < 0) return null;
            for (int i = 0; i < barSeats.Count; i++) if (ScoreLine.SameSide(barTeams, i, member)) return barSeats[i];
            return null;
        }

        PlayerController SeatOf(int seat)
        {
            for (int i = 0; i < barSeats.Count; i++) if (barSeats[i] && barSeats[i].Index == seat) return barSeats[i];
            return null;
        }

        void ShowBoardLine(BoardLine line, ScoreRow row, int rank, float lead, float width, float y)
        {
            if (!line.root.gameObject.activeSelf) line.root.gameObject.SetActive(true);
            Box(line.root, BoardPad, y, width, BoardRow);
            if (line.lead != lead) LayBoardLine(line, lead, width);
            if (line.bandFace.enabled != row.Mine) line.bandFace.enabled = row.Mine;
            if (line.rank.enabled != rank > 0) line.rank.enabled = rank > 0;
            if (rank > 0 && rank != line.shownRank) { line.shownRank = rank; line.rank.text = rank.ToString(); }
            line.name.text = row.Name;
            for (int column = 0; column < BoardColumns; column++)
            {
                int value = row.Column(column);
                if (value == line.shown[column]) continue;
                line.shown[column] = value;
                line.numbers[column].text = value.ToString();
            }
            var p = SeatOf(row.Seat);
            if (p != line.seat)
            {
                line.seat = p;
                if (p) line.letter.text = PlayerBar.Letter(p.Initial);
            }
            var colour = p ? PlayerHud.Vivid(p.Color) : BagInk;
            bool isOut = p && p.IsEliminated;
            var portrait = p ? SeatPortrait.For(p) : null;
            var picture = portrait ? portrait.Texture : null;
            line.face.texture = picture;
            line.face.enabled = picture;
            line.letter.enabled = !picture;
            line.disc.color = picture ? CardBacking : colour;
            line.face.color = isOut ? WreckedTint : Color.white;
            if (isOut) colour.a = BoardOutAlpha;
            line.name.color = colour;
        }

        static void LayBoardLine(BoardLine line, float lead, float width)
        {
            line.lead = lead;
            float face = lead + BoardInset, name = face + BoardFace + BoardFaceGap, columns = BoardColumns * BoardColumn;
            Box(line.band, lead, 2f, width - lead - BoardInset, BoardRow - 4f);
            Box(line.rank.rectTransform, 0f, 0f, lead, BoardRow);
            Box(line.disc.rectTransform, face, (BoardRow - BoardFace) * .5f, BoardFace, BoardFace);
            Box(line.name.rectTransform, name, 0f, width - columns - name, BoardRow);
        }
    }
}
