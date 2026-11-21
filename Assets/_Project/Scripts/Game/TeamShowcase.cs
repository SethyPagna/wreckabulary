using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public partial class GameHud
    {
        public const string ShowcaseRootName = "Team showcase";
        public const float ShowcaseMinFont = 15f;
        const float ShowcaseSeatMax = 170f, ShowcaseSeatMin = 84f, ShowcaseSeatGap = 18f, ShowcaseSideGap = 96f, ShowcaseMargin = 48f;
        const float ShowcaseNameHeight = 38f, ShowcaseTagHeight = 30f, ShowcaseRim = 6f, ShowcaseLetterShare = .5f;
        const float ShowcaseModeY = 290f, ShowcaseTitleY = 236f, ShowcaseLineY = 62f, ShowcaseLeadY = -110f, ShowcaseNumberY = -196f, ShowcaseSkipY = -292f;
        const float ShowcaseModeHeight = 36f, ShowcaseTitleHeight = 34f, ShowcaseLeadHeight = 40f, ShowcaseNumberHeight = 130f, ShowcaseSkipHeight = 34f;
        const float ShowcaseModeFont = 26f, ShowcaseTitleFont = 24f, ShowcaseVersusFont = 30f, ShowcaseNameFont = 24f, ShowcaseTagFont = 18f;
        const float ShowcaseLeadFont = 34f, ShowcaseNumberFont = 124f, ShowcaseSkipFont = 22f;
        const float ShowcasePulseSeconds = .25f, ShowcasePulseScale = 1.18f, ShowcaseTextWidth = 1200f;
        const int ShowcaseFrameRadius = 20, ShowcaseInnerRadius = 15;

        sealed class ShowcaseSeat
        {
            public PlayerController player;
            public RectTransform root, frame;
            public TextMeshProUGUI name, tag, letter;
            public RawImage portrait;
            public SeatPortrait face;
            public Color colour;
            public int row;
        }

        RectTransform showcaseRoot, showcaseLine;
        TextMeshProUGUI showcaseMode, showcaseLead, showcaseNumber, showcaseSkip;
        readonly List<ShowcaseSeat> showcaseSeats = new();
        readonly List<TextMeshProUGUI> showcaseTitles = new(), showcaseVersus = new();
        int showcaseRows, showcaseCount = -1, showcaseReady = -1, showcaseVoters = -1;
        string showcaseKey;
        bool showcaseSkipSet;
        float showcaseWidth = -1f, showcasePulseAt = -10f;

        public bool ShowcaseShown => showcaseRoot && showcaseRoot.gameObject.activeSelf;
        public RectTransform ShowcaseRoot => showcaseRoot;
        public string ShowcaseCountText => ShowcaseShown ? showcaseLead.text + " " + showcaseNumber.text : "";
        public string ShowcaseSkipText => ShowcaseShown ? showcaseSkip.text : "";
        public string ShowcaseModeText => ShowcaseShown ? showcaseMode.text : "";
        public int ShowcaseSeatCount => ShowcaseShown ? showcaseSeats.Count : 0;
        public int ShowcaseRowCount => ShowcaseShown ? showcaseRows : 0;
        public PlayerController ShowcaseSeatPlayer(int seat) => ShowcaseShown && seat >= 0 && seat < showcaseSeats.Count ? showcaseSeats[seat].player : null;
        public string ShowcaseSeatName(int seat) => ShowcaseSeatPlayer(seat) ? showcaseSeats[seat].name.text : "";
        public string ShowcaseSeatTag(int seat) => ShowcaseSeatPlayer(seat) ? showcaseSeats[seat].tag.text : "";
        public Color ShowcaseSeatColour(int seat) => ShowcaseSeatPlayer(seat) ? showcaseSeats[seat].colour : Color.clear;
        public int ShowcaseSeatRow(int seat) => ShowcaseSeatPlayer(seat) ? showcaseSeats[seat].row : -1;
        public RectTransform ShowcaseSeatRect(int seat) => ShowcaseSeatPlayer(seat) ? showcaseSeats[seat].root : null;
        public string ShowcaseRowTitle(int row) => ShowcaseShown && row >= 0 && row < showcaseTitles.Count ? showcaseTitles[row].text : "";

        public static Color ShowcaseSideColour(PlayerController player, IReadOnlyList<PlayerController> everyone, bool sided)
        {
            if (!player) return Color.white;
            var lead = player;
            if (sided && everyone != null)
                foreach (var other in everyone)
                    if (other && other.Team == player.Team && other.Index < lead.Index) lead = other;
            return PlayerHud.Vivid(lead.Color);
        }

        public void ShowShowcase(IReadOnlyList<PlayerController> everyone, string modeId)
        {
            if (!safe || everyone == null) return;
            if (!showcaseRoot) BuildShowcase();
            CloseComposer();
            if (bagPinned) SetBagPinned(false);
            ClearShowcaseSeats();
            var seated = new List<PlayerController>();
            foreach (var p in everyone) if (p) seated.Add(p);
            var teams = new int[seated.Count];
            PlayerController you = null;
            int humans = 0;
            for (int i = 0; i < seated.Count; i++)
            {
                teams[i] = seated[i].Team;
                if (!SeatHuman(seated[i])) continue;
                humans++;
                you = seated[i];
            }
            if (humans != 1) you = null;
            bool sided = MatchShowcase.Sided(teams);
            var rows = MatchShowcase.Rows(teams);
            showcaseRows = rows.Count;
            for (int row = 0; row < rows.Count; row++)
            {
                Color side = rows[row].Count > 0 ? ShowcaseSideColour(seated[rows[row][0]], seated, sided) : Cream;
                var title = ShowcaseText(showcaseLine, "Side " + (row + 1), LobbyFonts.Black, ShowcaseTitleFont, side, 4f);
                title.text = MatchShowcase.RowTitle(teams, rows[row], you ? you.Team : Teams.NoTeam);
                showcaseTitles.Add(title);
                if (row > 0)
                {
                    var versus = ShowcaseText(showcaseLine, "Versus " + row, DisplayFont, ShowcaseVersusFont, Cream);
                    versus.text = MatchShowcase.Versus;
                    showcaseVersus.Add(versus);
                }
                foreach (int index in rows[row])
                    showcaseSeats.Add(MakeShowcaseSeat(seated[index], row, ShowcaseSideColour(seated[index], seated, sided), seated[index] == you, humans));
            }
            showcaseMode.text = MatchEnd.ModeLine(modeId, Layout?.Name);
            showcaseLead.text = MatchShowcase.Lead;
            showcaseCount = showcaseReady = showcaseVoters = -1;
            showcaseSkipSet = false;
            showcaseNumber.text = "";
            showcaseSkip.text = "";
            showcaseRoot.gameObject.SetActive(true);
            showcaseRoot.SetAsLastSibling();
            LayShowcase();
        }

        public void TickShowcase(Warmup votes, string startKey)
        {
            if (!ShowcaseShown || votes == null) return;
            int count = MatchShowcase.Count(votes.TimeLeft);
            if (count != showcaseCount)
            {
                showcaseCount = count;
                showcaseNumber.text = count.ToString(CultureInfo.InvariantCulture);
                showcasePulseAt = Time.unscaledTime;
            }
            if (!showcaseSkipSet || votes.ReadyCount != showcaseReady || votes.Voters != showcaseVoters || startKey != showcaseKey)
            {
                showcaseSkipSet = true; showcaseReady = votes.ReadyCount; showcaseVoters = votes.Voters; showcaseKey = startKey;
                showcaseSkip.text = MatchShowcase.SkipLine(votes, startKey);
            }
            float pulse = Mathf.Clamp01((Time.unscaledTime - showcasePulseAt) / ShowcasePulseSeconds);
            showcaseNumber.rectTransform.localScale = Vector3.one * Mathf.Lerp(ShowcasePulseScale, 1f, pulse);
            DrawShowcaseFaces();
            if (!Mathf.Approximately(safe.rect.width, showcaseWidth)) LayShowcase();
        }

        public void HideShowcase()
        {
            if (!showcaseRoot) return;
            showcaseRoot.gameObject.SetActive(false);
            ClearShowcaseSeats();
        }

        void BuildShowcase()
        {
            showcaseRoot = Rect(ShowcaseRootName, safe, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(showcaseRoot);
            var shade = showcaseRoot.gameObject.AddComponent<Image>();
            shade.color = ToastFill; shade.raycastTarget = false;
            var quiet = showcaseRoot.gameObject.AddComponent<CanvasGroup>();
            quiet.blocksRaycasts = quiet.interactable = false;
            showcaseMode = ShowcaseText(showcaseRoot, "Mode", LobbyFonts.Black, ShowcaseModeFont, Cream, 6f);
            Put(showcaseMode.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, ShowcaseModeY), new Vector2(ShowcaseTextWidth, ShowcaseModeHeight));
            showcaseLine = Rect("Line-up", showcaseRoot, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            showcaseLead = ShowcaseText(showcaseRoot, "Lead", LobbyFonts.Black, ShowcaseLeadFont, Cream, 8f);
            Put(showcaseLead.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, ShowcaseLeadY), new Vector2(ShowcaseTextWidth, ShowcaseLeadHeight));
            showcaseNumber = ShowcaseText(showcaseRoot, "Count", DisplayFont, ShowcaseNumberFont, Gold);
            Put(showcaseNumber.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, ShowcaseNumberY), new Vector2(ShowcaseTextWidth, ShowcaseNumberHeight));
            showcaseSkip = ShowcaseText(showcaseRoot, "Skip", LobbyFonts.Body, ShowcaseSkipFont, ToastInk);
            Put(showcaseSkip.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, ShowcaseSkipY), new Vector2(ShowcaseTextWidth, ShowcaseSkipHeight));
            showcaseRoot.gameObject.SetActive(false);
        }

        TextMeshProUGUI ShowcaseText(Transform parent, string label, TMP_FontAsset font, float size, Color colour, float spacing = 0f)
        {
            var text = Copy(parent, label, "", font, size, colour, spacing);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true; text.fontSizeMin = Mathf.Min(ShowcaseMinFont, size); text.fontSizeMax = size;
            return text;
        }

        ShowcaseSeat MakeShowcaseSeat(PlayerController player, int row, Color colour, bool you, int humans)
        {
            var seat = new ShowcaseSeat { player = player, row = row, colour = colour, face = SeatPortrait.For(player) };
            seat.root = Rect("Seat " + (player.Index + 1), showcaseLine, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            seat.name = ShowcaseText(seat.root, "Name", LobbyFonts.Black, ShowcaseNameFont, colour);
            seat.name.text = SeatTags.Name(player.Name, you, humans);
            seat.frame = Rect("Frame", seat.root, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            Paint(seat.frame, LobbyIcons.RoundedSprite(ShowcaseFrameRadius), colour);
            Paint(Cover("Backing", seat.frame, -ShowcaseRim), LobbyIcons.RoundedSprite(ShowcaseInnerRadius), Primary);
            seat.portrait = Cover("Portrait", seat.frame, -ShowcaseRim).gameObject.AddComponent<RawImage>();
            seat.portrait.raycastTarget = false;
            seat.portrait.enabled = false;
            seat.letter = ShowcaseText(seat.frame, "Letter", DisplayFont, ShowcaseSeatMax * ShowcaseLetterShare, colour);
            Stretch(seat.letter.rectTransform);
            seat.letter.text = PlayerBar.Letter(player.Initial);
            seat.tag = ShowcaseText(seat.root, "Tag", LobbyFonts.Body, ShowcaseTagFont, ToastInk);
            seat.tag.text = SeatHuman(player) ? "" : MatchShowcase.BotTag;
            return seat;
        }

        void ClearShowcaseSeats()
        {
            foreach (var seat in showcaseSeats) if (seat.root) Destroy(seat.root.gameObject);
            foreach (var title in showcaseTitles) if (title) Destroy(title.gameObject);
            foreach (var versus in showcaseVersus) if (versus) Destroy(versus.gameObject);
            showcaseSeats.Clear(); showcaseTitles.Clear(); showcaseVersus.Clear();
            showcaseRows = 0;
        }

        void DrawShowcaseFaces()
        {
            bool drew = false;
            foreach (var seat in showcaseSeats)
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

        void LayShowcase()
        {
            showcaseWidth = safe.rect.width;
            int seats = showcaseSeats.Count, sides = Mathf.Max(1, showcaseRows);
            if (seats == 0) return;
            float gaps = (seats - sides) * ShowcaseSeatGap + (sides - 1) * ShowcaseSideGap;
            float size = Mathf.Clamp((showcaseWidth - 2f * ShowcaseMargin - gaps) / seats, ShowcaseSeatMin, ShowcaseSeatMax);
            float height = ShowcaseNameHeight + size + ShowcaseTagHeight;
            float x = -(seats * size + gaps) * .5f;
            showcaseLine.anchoredPosition = new Vector2(0f, ShowcaseLineY);
            int row = 0, versus = 0;
            float rowStart = x;
            for (int i = 0; i < seats; i++)
            {
                var seat = showcaseSeats[i];
                if (seat.row != row)
                {
                    PlaceShowcaseTitle(row, rowStart, x - ShowcaseSeatGap);
                    if (versus < showcaseVersus.Count)
                        Put(showcaseVersus[versus++].rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(x - ShowcaseSeatGap + ShowcaseSideGap * .5f, (ShowcaseTagHeight - ShowcaseNameHeight) * .5f), new Vector2(ShowcaseSideGap, ShowcaseTitleHeight));
                    x += ShowcaseSideGap - ShowcaseSeatGap;
                    row = seat.row;
                    rowStart = x;
                }
                Put(seat.root, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x + size * .5f, 0f), new Vector2(size, height));
                Put(seat.name.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f), Vector2.zero, new Vector2(size + ShowcaseSeatGap - 4f, ShowcaseNameHeight));
                Put(seat.frame, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -ShowcaseNameHeight), new Vector2(size, size));
                Put(seat.tag.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f), Vector2.zero, new Vector2(size + ShowcaseSeatGap - 4f, ShowcaseTagHeight));
                seat.letter.fontSizeMax = Mathf.Max(ShowcaseMinFont, size * ShowcaseLetterShare);
                x += size + ShowcaseSeatGap;
            }
            PlaceShowcaseTitle(row, rowStart, x - ShowcaseSeatGap);
        }

        void PlaceShowcaseTitle(int row, float from, float to)
        {
            if (row < 0 || row >= showcaseTitles.Count) return;
            Put(showcaseTitles[row].rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2((from + to) * .5f, ShowcaseTitleY - ShowcaseLineY), new Vector2(Mathf.Max(to - from, ShowcaseSeatMax), ShowcaseTitleHeight));
        }
    }
}
