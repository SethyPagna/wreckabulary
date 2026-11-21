using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public partial class GameHud
    {
        public const string MatchEndHeadName = "Match end", MatchEndCardsName = "Match cards";
        public const float MatchEndMinFont = 15f;
        const float ResultCardWidth = 575f, ResultCardPad = 44f, ResultHeadingFont = 40f, MatchEndHeadingFont = 26f;
        const float MatchEndHeadHeight = 104f, MatchEndModeHeight = 24f, MatchEndBannerHeight = 72f, MatchEndModeFont = 16f, MatchEndBannerFont = 60f, MatchEndBannerMinFont = 28f;
        const float MatchEndCellMin = 248f, MatchEndCellMax = 270f, MatchEndCellHeight = 156f, MatchEndCellGap = 12f, MatchEndRowGap = 10f, MatchEndTopGap = 14f;
        const float MatchEndPad = 12f, MatchEndPortrait = 64f, MatchEndRim = 4f, MatchEndTextLeft = 88f;
        const float MatchEndNameTop = 10f, MatchEndNameHeight = 26f, MatchEndTitleHeight = 22f, MatchEndTagHeight = 20f;
        const float MatchEndNumbersTop = 86f, MatchEndNumbersHeight = 60f, MatchEndValueHeight = 36f;
        const float MatchEndNameFont = 20f, MatchEndTitleFont = 16f, MatchEndTagFont = 15f, MatchEndValueFont = 26f, MatchEndLabelFont = 15f, MatchEndLetterFont = 34f;
        const int MatchEndRoomySeats = 4, MatchEndCellRadius = 16, MatchEndPortraitRadius = 12, MatchEndInnerRadius = 9;

        sealed class MatchEndSeat
        {
            public MatchEndCard card;
            public RectTransform root;
            public TextMeshProUGUI name, title, tag, letter;
            public RawImage portrait;
            public SeatPortrait face;
            public readonly List<(TextMeshProUGUI value, TextMeshProUGUI label)> numbers = new();
        }

        RectTransform resultCard, matchEndHead, matchEndCards;
        GameObject resultSparkGap;
        LayoutElement matchEndCardsSize;
        TextMeshProUGUI matchEndMode, matchEndBanner;
        readonly List<MatchEndSeat> matchEndSeats = new();
        MatchEndBoard matchEndBoard;
        float matchEndWidth = -1f;

        public bool MatchEndShown => ResultShown && matchEndBoard != null && matchEndHead && matchEndHead.gameObject.activeSelf;
        public MatchEndBoard ShownMatchEnd => MatchEndShown ? matchEndBoard : null;
        public string MatchEndBanner => MatchEndShown ? matchEndBanner.text : "";
        public string MatchEndModeLine => MatchEndShown ? matchEndMode.text : "";
        public int MatchEndCardCount => MatchEndShown ? matchEndSeats.Count : 0;
        public RectTransform MatchEndCardRect(int index) => MatchEndShown && index >= 0 && index < matchEndSeats.Count ? matchEndSeats[index].root : null;
        public string MatchEndCardName(int index) => MatchEndCardRect(index) ? matchEndSeats[index].name.text : "";
        public string MatchEndCardTitle(int index) => MatchEndCardRect(index) ? matchEndSeats[index].title.text : "";
        public string MatchEndCardTag(int index) => MatchEndCardRect(index) ? matchEndSeats[index].tag.text : "";

        public string MatchEndCardNumbers(int index)
        {
            if (!MatchEndCardRect(index)) return "";
            var parts = new List<string>();
            foreach (var (value, label) in matchEndSeats[index].numbers) parts.Add(label.text + " " + value.text);
            return string.Join(MatchEnd.Dot, parts);
        }

        internal int MatchScoreOf(PlayerController player) => showWins && wins != null && player ? wins(player) : 0;

        void ShowMatchEnd(MatchEndBoard board)
        {
            if (board != null && board.Cards.Count == 0) board = null;
            if (board == null && matchEndBoard == null && !matchEndHead) return;
            matchEndBoard = board;
            bool on = board != null;
            if (on && !matchEndHead) BuildMatchEnd();
            if (matchEndHead) matchEndHead.gameObject.SetActive(on);
            if (matchEndCards) matchEndCards.gameObject.SetActive(on);
            resultSpark.gameObject.SetActive(!on);
            if (resultSparkGap) resultSparkGap.SetActive(!on);
            resultHeading.fontSize = on ? MatchEndHeadingFont : ResultHeadingFont;
            ClearMatchEndSeats();
            if (!on)
            {
                SetResultCardWidth(ResultCardWidth);
                return;
            }
            resultPlayers.gameObject.SetActive(false); resultPlayersGap.SetActive(false);
            resultStatsRow.SetActive(false); resultStatsGap.SetActive(false);
            matchEndMode.text = board.ModeLine;
            matchEndBanner.text = board.Banner;
            matchEndBanner.color = board.Won ? ReadyInk : board.Lost ? Bad : Ink;
            var teams = new int[board.Cards.Count];
            for (int i = 0; i < teams.Length; i++) teams[i] = board.Cards[i].Team;
            bool sided = MatchShowcase.Sided(teams);
            for (int i = 0; i < board.Cards.Count; i++) matchEndSeats.Add(MakeMatchEndSeat(board.Cards[i], i, sided));
            int across = MatchEndAcross(board.Cards.Count);
            SetResultCardWidth(Mathf.Max(ResultCardWidth, 2f * ResultCardPad + across * MatchEndCellMax + (across - 1) * MatchEndCellGap));
            matchEndWidth = -1f;
            FitMatchEnd();
            DrawMatchEndFaces();
        }

        static int MatchEndAcross(int seats) => seats <= MatchEndRoomySeats ? Mathf.Max(1, seats) : (seats + 1) / 2;

        void SetResultCardWidth(float width)
        {
            if (!resultCard) return;
            for (int i = 0; i < cardWidths.Count; i++)
                if (cardWidths[i].card == resultCard) cardWidths[i] = (resultCard, width);
            float room = safe ? safe.rect.width - 32f : width;
            resultCard.sizeDelta = new Vector2(room > 0f ? Mathf.Min(width, room) : width, resultCard.sizeDelta.y);
        }

        void BuildMatchEnd()
        {
            matchEndHead = Rect(MatchEndHeadName, resultCard, Vector2.zero, Vector2.zero, Vector2.zero);
            var headSize = matchEndHead.gameObject.AddComponent<LayoutElement>();
            headSize.preferredHeight = headSize.minHeight = MatchEndHeadHeight;
            matchEndHead.SetSiblingIndex(resultHeading.transform.GetSiblingIndex());
            matchEndMode = MatchEndText(matchEndHead, "Mode", LobbyFonts.Black, MatchEndModeFont, MatchEndMinFont, Eyebrow, 4f);
            MatchEndRow(matchEndMode.rectTransform, 0f, 0f, 0f, MatchEndModeHeight);
            matchEndBanner = MatchEndText(matchEndHead, "Banner", DisplayFont, MatchEndBannerFont, MatchEndBannerMinFont, Ink);
            MatchEndRow(matchEndBanner.rectTransform, 0f, 0f, MatchEndModeHeight, MatchEndBannerHeight);
            matchEndCards = Rect(MatchEndCardsName, resultCard, Vector2.zero, Vector2.zero, Vector2.zero);
            matchEndCardsSize = matchEndCards.gameObject.AddComponent<LayoutElement>();
            matchEndCardsSize.preferredHeight = matchEndCardsSize.minHeight = MatchEndCellHeight + MatchEndTopGap;
            matchEndCards.SetSiblingIndex(resultPlayersGap.transform.GetSiblingIndex());
        }

        TextMeshProUGUI MatchEndText(Transform parent, string label, TMP_FontAsset font, float size, float smallest, Color colour, float spacing = 0f)
        {
            var text = Copy(parent, label, "", font, size, colour, spacing);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true; text.fontSizeMin = Mathf.Min(smallest, size); text.fontSizeMax = size;
            return text;
        }

        static void MatchEndRow(RectTransform rt, float left, float right, float top, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = Vector2.one; rt.pivot = new Vector2(.5f, 1f);
            rt.offsetMin = new Vector2(left, -top - height); rt.offsetMax = new Vector2(-right, -top);
        }

        MatchEndSeat MakeMatchEndSeat(MatchEndCard card, int index, bool sided)
        {
            var player = World.PlayerById(card.Seat);
            Color colour = player ? ShowcaseSideColour(player, World.Players, sided) : Cream;
            var seat = new MatchEndSeat { card = card, face = player ? SeatPortrait.For(player) : null };
            seat.root = Rect("Card " + (index + 1), matchEndCards, new Vector2(.5f, 1f), Vector2.zero, Vector2.zero);
            Paint(seat.root, LobbyIcons.RoundedSprite(MatchEndCellRadius), Primary);
            Paint(Cover("Edge", seat.root), LobbyIcons.FrameSprite(MatchEndCellRadius, card.Winner ? 3 : 1), card.Winner ? CoinGold : ToastEdge);
            var frame = Rect("Portrait", seat.root, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            Put(frame, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(MatchEndPad, -MatchEndPad), new Vector2(MatchEndPortrait, MatchEndPortrait));
            Paint(frame, LobbyIcons.RoundedSprite(MatchEndPortraitRadius), colour);
            Paint(Cover("Backing", frame, -MatchEndRim), LobbyIcons.RoundedSprite(MatchEndInnerRadius), PrimaryLip);
            seat.portrait = Cover("Picture", frame, -MatchEndRim).gameObject.AddComponent<RawImage>();
            seat.portrait.raycastTarget = false;
            seat.portrait.enabled = false;
            seat.letter = MatchEndText(frame, "Letter", DisplayFont, MatchEndLetterFont, MatchEndMinFont, colour);
            Stretch(seat.letter.rectTransform);
            seat.letter.text = player ? PlayerBar.Letter(player.Initial) : card.Name.Length > 0 ? PlayerBar.Letter(card.Name[0]) : "";
            seat.name = MatchEndText(seat.root, "Name", LobbyFonts.Black, MatchEndNameFont, MatchEndMinFont, Cream);
            seat.name.alignment = TextAlignmentOptions.MidlineLeft;
            MatchEndRow(seat.name.rectTransform, MatchEndTextLeft, MatchEndPad, MatchEndNameTop, MatchEndNameHeight);
            seat.name.text = card.Name;
            seat.title = MatchEndText(seat.root, "Title", LobbyFonts.Black, MatchEndTitleFont, MatchEndMinFont, Gold);
            seat.title.alignment = TextAlignmentOptions.MidlineLeft;
            MatchEndRow(seat.title.rectTransform, MatchEndTextLeft, MatchEndPad, MatchEndNameTop + MatchEndNameHeight, MatchEndTitleHeight);
            seat.title.text = card.Title;
            seat.tag = MatchEndText(seat.root, "Tag", LobbyFonts.Body, MatchEndTagFont, MatchEndMinFont, ToastInk);
            seat.tag.alignment = TextAlignmentOptions.MidlineLeft;
            MatchEndRow(seat.tag.rectTransform, MatchEndTextLeft, MatchEndPad, MatchEndNameTop + MatchEndNameHeight + MatchEndTitleHeight, MatchEndTagHeight);
            seat.tag.text = MatchEndTag(card);
            var numbers = Rect("Numbers", seat.root, Vector2.zero, Vector2.zero, Vector2.zero);
            MatchEndRow(numbers, MatchEndPad, MatchEndPad, MatchEndNumbersTop, MatchEndNumbersHeight);
            int shown = card.Numbers.Count;
            for (int i = 0; i < shown; i++)
            {
                var cell = Rect(card.Numbers[i].Label, numbers, Vector2.zero, Vector2.zero, Vector2.zero);
                cell.anchorMin = new Vector2(i / (float)shown, 0f); cell.anchorMax = new Vector2((i + 1) / (float)shown, 1f);
                cell.offsetMin = cell.offsetMax = Vector2.zero;
                var value = MatchEndText(cell, "Number", DisplayFont, MatchEndValueFont, MatchEndMinFont, Cream);
                MatchEndRow(value.rectTransform, 0f, 0f, 0f, MatchEndValueHeight);
                value.text = card.Numbers[i].Text;
                var label = MatchEndText(cell, "Label", LobbyFonts.Body, MatchEndLabelFont, MatchEndMinFont, ToastInk);
                MatchEndRow(label.rectTransform, 0f, 0f, MatchEndValueHeight, MatchEndNumbersHeight - MatchEndValueHeight);
                label.text = card.Numbers[i].Label;
                seat.numbers.Add((value, label));
            }
            return seat;
        }

        static string MatchEndTag(MatchEndCard card) =>
            card.Winner ? card.Bot ? MatchEnd.WinnerTag + MatchEnd.Dot + MatchEnd.BotTag : MatchEnd.WinnerTag : card.Bot ? MatchEnd.BotTag : "";

        void ClearMatchEndSeats()
        {
            foreach (var seat in matchEndSeats) if (seat.root) Destroy(seat.root.gameObject);
            matchEndSeats.Clear();
        }

        void FitMatchEnd()
        {
            if (matchEndBoard == null || !matchEndCards || !resultCard || matchEndSeats.Count == 0) return;
            float inner = resultCard.sizeDelta.x - 2f * ResultCardPad;
            if (inner <= 0f || Mathf.Approximately(inner, matchEndWidth)) return;
            matchEndWidth = inner;
            int seats = matchEndSeats.Count;
            int fits = Mathf.Max(1, Mathf.FloorToInt((inner + MatchEndCellGap) / (MatchEndCellMin + MatchEndCellGap)));
            int rows = Mathf.CeilToInt(seats / (float)Mathf.Min(MatchEndAcross(seats), fits));
            int across = Mathf.CeilToInt(seats / (float)rows);
            float cell = Mathf.Min(MatchEndCellMax, (inner - (across - 1) * MatchEndCellGap) / across);
            for (int i = 0; i < seats; i++)
            {
                int row = i / across, column = i % across;
                int inRow = Mathf.Min(across, seats - row * across);
                float wide = inRow * cell + (inRow - 1) * MatchEndCellGap;
                float x = -wide * .5f + column * (cell + MatchEndCellGap) + cell * .5f;
                Put(matchEndSeats[i].root, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                    new Vector2(x, -MatchEndTopGap - row * (MatchEndCellHeight + MatchEndRowGap)), new Vector2(cell, MatchEndCellHeight));
            }
            matchEndCardsSize.preferredHeight = matchEndCardsSize.minHeight = MatchEndTopGap + rows * MatchEndCellHeight + (rows - 1) * MatchEndRowGap;
        }

        void DrawMatchEndFaces()
        {
            if (matchEndBoard == null || matchEndSeats.Count == 0 || !ResultShown) return;
            bool drew = false;
            foreach (var seat in matchEndSeats)
            {
                if (!seat.face || !seat.portrait) continue;
                bool render = !drew && seat.face.Wants;
                var picture = seat.face.Refresh(render);
                drew |= render;
                if (picture == seat.portrait.texture) continue;
                seat.portrait.texture = picture;
                seat.portrait.enabled = picture;
                seat.letter.enabled = !picture;
            }
        }
    }
}
