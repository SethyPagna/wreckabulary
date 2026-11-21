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
        const float ShowcaseSeatMax = 270f, ShowcaseSeatMin = 84f, ShowcaseSeatGap = 18f, ShowcaseSideGap = 96f, ShowcaseMargin = 48f, ShowcaseEdge = 24f;
        const float ShowcaseNameHeight = 38f, ShowcaseTagHeight = 30f, ShowcaseRim = 6f, ShowcaseLetterShare = .5f;
        const float ShowcaseModeHeight = 36f, ShowcaseTitleHeight = 34f, ShowcaseLeadHeight = 40f, ShowcaseNumberHeight = 130f, ShowcaseSkipHeight = 34f;
        const float ShowcaseModeGap = 18f, ShowcaseTitleGap = 14f, ShowcaseLeadGap = 26f, ShowcaseNumberGap = 1f, ShowcaseSkipGap = 14f;
        const float ShowcaseModeFont = 26f, ShowcaseTitleFont = 24f, ShowcaseVersusFont = 30f, ShowcaseNameFont = 26f, ShowcaseTagFont = 18f;
        const float ShowcaseLeadFont = 34f, ShowcaseNumberFont = 124f, ShowcaseSkipFont = 22f;
        const float ShowcasePulseSeconds = .25f, ShowcasePulseScale = 1.18f, ShowcaseTickScale = 1.4f, ShowcaseTextWidth = 1200f, ShowcaseCrewTitleWidth = 320f;
        const float ShowcaseRise = 120f;
        const int ShowcaseFrameRadius = 24, ShowcaseInnerRadius = 18;

        sealed class ShowcaseSeat
        {
            public PlayerController player;
            public RectTransform root, body, frame;
            public TextMeshProUGUI name, tag, letter;
            public RawImage portrait;
            public SeatPortrait face;
            public CanvasGroup fade;
            public Color colour;
            public int row, side;
            public float delay;
        }

        RectTransform showcaseRoot, showcaseLine;
        TextMeshProUGUI showcaseMode, showcaseLead, showcaseNumber, showcaseSkip;
        readonly List<ShowcaseSeat> showcaseSeats = new();
        readonly List<TextMeshProUGUI> showcaseTitles = new(), showcaseVersus = new();
        int showcaseRows, showcaseCount = -1, showcaseReady = -1, showcaseVoters = -1;
        string showcaseKey;
        bool showcaseSkipSet, showcaseCrew;
        float showcaseWidth = -1f, showcaseHeight = -1f, showcasePulseAt = -10f, showcasePulse = 1f, showcaseTitleOffset, showcaseSize, showcaseSlid = -1f;

        public bool ShowcaseShown => showcaseRoot && showcaseRoot.gameObject.activeSelf;
        public RectTransform ShowcaseRoot => showcaseRoot;
        public string ShowcaseCountText => ShowcaseShown ? showcaseLead.text + " " + showcaseNumber.text : "";
        public string ShowcaseSkipText => ShowcaseShown ? showcaseSkip.text : "";
        public string ShowcaseModeText => ShowcaseShown ? showcaseMode.text : "";
        public int ShowcaseSeatCount => ShowcaseShown ? showcaseSeats.Count : 0;
        public int ShowcaseRowCount => ShowcaseShown ? showcaseRows : 0;
        public float ShowcaseSeatSize => ShowcaseShown ? showcaseSize : 0f;
        public PlayerController ShowcaseSeatPlayer(int seat) => ShowcaseShown && seat >= 0 && seat < showcaseSeats.Count ? showcaseSeats[seat].player : null;
        public string ShowcaseSeatName(int seat) => ShowcaseSeatPlayer(seat) ? showcaseSeats[seat].name.text : "";
        public string ShowcaseSeatTag(int seat) => ShowcaseSeatPlayer(seat) ? showcaseSeats[seat].tag.text : "";
        public Color ShowcaseSeatColour(int seat) => ShowcaseSeatPlayer(seat) ? showcaseSeats[seat].colour : Color.clear;
        public int ShowcaseSeatRow(int seat) => ShowcaseSeatPlayer(seat) ? showcaseSeats[seat].row : -1;
        public RectTransform ShowcaseSeatRect(int seat) => ShowcaseSeatPlayer(seat) ? showcaseSeats[seat].root : null;
        public bool ShowcaseSeatLanded(int seat) => ShowcaseSeatPlayer(seat) && showcaseSeats[seat].body.anchoredPosition == Vector2.zero;
        public string ShowcaseRowTitle(int row) => ShowcaseShown && row >= 0 && row < showcaseTitles.Count ? showcaseTitles[row].text : "";

        public static Color ShowcaseSideColour(PlayerController player, IReadOnlyList<PlayerController> everyone, bool sided) =>
            sided ? SeatColour(player, everyone) : player ? PlayerHud.Vivid(player.Color) : Color.white;

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
            bool crew = showcaseCrew = Warmup.Crew(modeId);
            bool sided = !crew && MatchShowcase.Sided(teams);
            bool accented = sided && SeatAccented(seated);
            var rows = MatchShowcase.Rows(teams, modeId);
            showcaseRows = rows.Count;
            for (int row = 0; row < rows.Count; row++)
            {
                Color side = !crew && rows[row].Count > 0 ? ShowcaseSideColour(seated[rows[row][0]], seated, sided) : Cream;
                var title = ShowcaseText(showcaseLine, "Side " + (row + 1), LobbyFonts.Black, ShowcaseTitleFont, side, 4f);
                title.text = MatchShowcase.RowTitle(teams, rows[row], you ? you.Team : Teams.NoTeam, modeId);
                showcaseTitles.Add(title);
                if (row > 0)
                {
                    var versus = ShowcaseText(showcaseLine, "Versus " + row, DisplayFont, ShowcaseVersusFont, Cream);
                    versus.text = MatchShowcase.Versus;
                    showcaseVersus.Add(versus);
                }
                foreach (int index in rows[row])
                    showcaseSeats.Add(MakeShowcaseSeat(seated[index], row, ShowcaseSideColour(seated[index], seated, sided), seated[index] == you, accented));
            }
            for (int i = 0; i < showcaseSeats.Count; i++)
            {
                var seat = showcaseSeats[i];
                seat.side = MatchShowcase.SlideSide(i, showcaseSeats.Count, seat.row, showcaseRows);
                seat.delay = MatchShowcase.SlideDelay(i, showcaseSeats.Count, seat.row, showcaseRows);
            }
            showcaseMode.text = MatchEnd.ModeLine(modeId, Layout?.Name);
            showcaseLead.text = MatchShowcase.Lead;
            showcaseCount = showcaseReady = showcaseVoters = -1;
            showcaseSkipSet = false;
            showcaseSlid = -1f;
            showcaseNumber.text = "";
            showcaseSkip.text = "";
            showcaseRoot.gameObject.SetActive(true);
            showcaseRoot.SetAsLastSibling();
            LayShowcase();
            SlideShowcase(0f);
        }

        public void TickShowcase(Warmup votes, string startKey)
        {
            if (!ShowcaseShown || votes == null) return;
            int count = MatchShowcase.Count(votes.TimeLeft);
            if (count != showcaseCount)
            {
                bool first = showcaseCount < 0;
                showcaseCount = count;
                showcaseNumber.text = count.ToString(CultureInfo.InvariantCulture);
                showcasePulseAt = Time.unscaledTime;
                bool ticks = !first && MatchShowcase.Ticks(count);
                showcasePulse = ticks ? ShowcaseTickScale : ShowcasePulseScale;
                if (ticks) GameSoundBank.Tick();
            }
            if (!showcaseSkipSet || votes.ReadyCount != showcaseReady || votes.Voters != showcaseVoters || startKey != showcaseKey)
            {
                showcaseSkipSet = true; showcaseReady = votes.ReadyCount; showcaseVoters = votes.Voters; showcaseKey = startKey;
                showcaseSkip.text = MatchShowcase.SkipLine(votes, startKey);
            }
            float pulse = Mathf.Clamp01((Time.unscaledTime - showcasePulseAt) / ShowcasePulseSeconds);
            showcaseNumber.rectTransform.localScale = Vector3.one * Mathf.Lerp(showcasePulse, 1f, pulse);
            DrawShowcaseFaces();
            if (!Mathf.Approximately(safe.rect.width, showcaseWidth) || !Mathf.Approximately(safe.rect.height, showcaseHeight)) LayShowcase();
            SlideShowcase(votes.Elapsed);
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
            showcaseLine = Rect("Line-up", showcaseRoot, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            showcaseLead = ShowcaseText(showcaseRoot, "Lead", LobbyFonts.Black, ShowcaseLeadFont, Cream, 8f);
            showcaseNumber = ShowcaseText(showcaseRoot, "Count", DisplayFont, ShowcaseNumberFont, Gold);
            showcaseSkip = ShowcaseText(showcaseRoot, "Skip", LobbyFonts.Body, ShowcaseSkipFont, ToastInk);
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

        ShowcaseSeat MakeShowcaseSeat(PlayerController player, int row, Color colour, bool you, bool accented)
        {
            var seat = new ShowcaseSeat { player = player, row = row, colour = colour, face = SeatPortrait.For(player) };
            seat.root = Rect("Seat " + (player.Index + 1), showcaseLine, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            seat.body = Rect("Body", seat.root, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            Stretch(seat.body);
            seat.fade = seat.body.gameObject.AddComponent<CanvasGroup>();
            seat.fade.blocksRaycasts = seat.fade.interactable = false;
            seat.name = ShowcaseText(seat.body, "Name", LobbyFonts.Black, ShowcaseNameFont, accented ? PlayerHud.Vivid(player.Color) : colour);
            seat.name.text = player.Name;
            seat.frame = Rect("Frame", seat.body, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            Paint(seat.frame, LobbyIcons.RoundedSprite(ShowcaseFrameRadius), colour);
            Paint(Cover("Backing", seat.frame, -ShowcaseRim), LobbyIcons.RoundedSprite(ShowcaseInnerRadius), Primary);
            seat.portrait = Cover("Portrait", seat.frame, -ShowcaseRim).gameObject.AddComponent<RawImage>();
            seat.portrait.raycastTarget = false;
            seat.portrait.enabled = false;
            seat.letter = ShowcaseText(seat.frame, "Letter", DisplayFont, ShowcaseSeatMax * ShowcaseLetterShare, colour);
            Stretch(seat.letter.rectTransform);
            seat.letter.text = PlayerBar.Letter(player.Initial);
            seat.tag = ShowcaseText(seat.body, "Tag", LobbyFonts.Black, ShowcaseTagFont, you ? Gold : ToastInk, 2f);
            seat.tag.text = SeatTags.Tag(you, !SeatHuman(player));
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

        static float ShowcaseStack(float size) =>
            ShowcaseModeHeight + ShowcaseModeGap + ShowcaseTitleHeight + ShowcaseTitleGap + ShowcaseNameHeight + size + ShowcaseTagHeight
            + ShowcaseLeadGap + ShowcaseLeadHeight + ShowcaseNumberGap + ShowcaseNumberHeight + ShowcaseSkipGap + ShowcaseSkipHeight;

        void LayShowcase()
        {
            showcaseWidth = safe.rect.width;
            showcaseHeight = safe.rect.height;
            int seats = showcaseSeats.Count, sides = Mathf.Max(1, showcaseRows);
            if (seats == 0) return;
            float gaps = (seats - sides) * ShowcaseSeatGap + (sides - 1) * ShowcaseSideGap;
            float byWidth = (showcaseWidth - 2f * ShowcaseMargin - gaps) / seats;
            float byHeight = showcaseHeight - 2f * ShowcaseEdge - ShowcaseStack(0f);
            float size = showcaseSize = Mathf.Clamp(Mathf.Min(byWidth, byHeight), ShowcaseSeatMin, ShowcaseSeatMax);
            float block = ShowcaseNameHeight + size + ShowcaseTagHeight;
            float top = ShowcaseStack(size) * .5f;
            PlaceShowcaseText(showcaseMode, top - ShowcaseModeHeight * .5f, ShowcaseModeHeight);
            float blockTop = top - ShowcaseModeHeight - ShowcaseModeGap - ShowcaseTitleHeight - ShowcaseTitleGap;
            float lineY = blockTop - block * .5f;
            showcaseTitleOffset = block * .5f + ShowcaseTitleGap + ShowcaseTitleHeight * .5f;
            float leadTop = blockTop - block - ShowcaseLeadGap;
            PlaceShowcaseText(showcaseLead, leadTop - ShowcaseLeadHeight * .5f, ShowcaseLeadHeight);
            float numberTop = leadTop - ShowcaseLeadHeight - ShowcaseNumberGap;
            PlaceShowcaseText(showcaseNumber, numberTop - ShowcaseNumberHeight * .5f, ShowcaseNumberHeight);
            float skipTop = numberTop - ShowcaseNumberHeight - ShowcaseSkipGap;
            PlaceShowcaseText(showcaseSkip, skipTop - ShowcaseSkipHeight * .5f, ShowcaseSkipHeight);
            float x = -(seats * size + gaps) * .5f;
            showcaseLine.anchoredPosition = new Vector2(0f, lineY);
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
                Put(seat.root, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x + size * .5f, 0f), new Vector2(size, block));
                Put(seat.name.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f), Vector2.zero, new Vector2(size + ShowcaseSeatGap - 4f, ShowcaseNameHeight));
                Put(seat.frame, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -ShowcaseNameHeight), new Vector2(size, size));
                Put(seat.tag.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f), Vector2.zero, new Vector2(size + ShowcaseSeatGap - 4f, ShowcaseTagHeight));
                seat.letter.fontSizeMax = Mathf.Max(ShowcaseMinFont, size * ShowcaseLetterShare);
                x += size + ShowcaseSeatGap;
            }
            PlaceShowcaseTitle(row, rowStart, x - ShowcaseSeatGap);
        }

        static void PlaceShowcaseText(TextMeshProUGUI text, float y, float height) =>
            Put(text.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, y), new Vector2(ShowcaseTextWidth, height));

        void PlaceShowcaseTitle(int row, float from, float to)
        {
            if (row < 0 || row >= showcaseTitles.Count) return;
            Put(showcaseTitles[row].rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2((from + to) * .5f, showcaseTitleOffset),
                new Vector2(Mathf.Max(to - from, showcaseCrew ? ShowcaseCrewTitleWidth : ShowcaseSeatMin), ShowcaseTitleHeight));
        }

        void SlideShowcase(float elapsed)
        {
            if (Mathf.Approximately(elapsed, showcaseSlid)) return;
            showcaseSlid = elapsed;
            float travel = showcaseWidth * .5f + showcaseSize;
            foreach (var seat in showcaseSeats)
            {
                float landed = MatchShowcase.Slide(elapsed, seat.delay);
                float away = 1f - landed;
                seat.body.anchoredPosition = seat.side != 0 ? new Vector2(seat.side * away * travel, 0f) : new Vector2(0f, -away * ShowcaseRise);
                seat.fade.alpha = landed;
            }
        }
    }
}
