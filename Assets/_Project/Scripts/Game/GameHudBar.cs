using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public partial class GameHud
    {
        const int MaxCards = PlayerBar.MostSeats;
        const float BrandX = 38f, LabelX = 112f, LabelY = 28f, LabelWidth = 460f, LabelHeight = 100f;
        const float ChipY = -25f, BannerY = -100f, StatusY = -140f, PortraitStatusY = -360f, PlayY = -185f, ToastY = -238f, ModeActionsY = -300f;
        const float LeftColumnTop = 150f;
        static readonly Color CardBacking = Hex(0x16302f), WreckedFrame = Hex(0x8b9592), WreckedTint = Hex(0x6d7472);
        static readonly Color WreckedShade = Hex(0x0b1515, .55f), DownShade = Hex(0x0b1515, .28f), PulseFrame = Hex(0xff8a6b);
        static readonly Color HealthTrack = Hex(0x0b1f1f, .78f), ScoreInk = Hex(0xffe7b8), YouEdge = Color.white, Plate = Hex(0x121c24, .72f);

        sealed class PlayerCard
        {
            public RectTransform root, track;
            public Image you, backing, shade, frame, fill;
            public RawImage portrait;
            public TextMeshProUGUI letter, status, name, score;
            public PlayerController seat;
            public SeatPortrait face;
            public string shownName, shownUnit;
            public int shownWins = int.MinValue, shownTarget = int.MinValue, shownStatus = int.MinValue, shownLook = -1, shownSeats = -1;
            public bool shownYou, pulsing;
            public Texture shownPicture;
            public Color vivid;
            public float shownX = float.NaN;
        }

        readonly List<PlayerCard> cards = new();
        readonly List<PlayerController> barSeats = new();
        readonly List<int> barTeams = new();
        readonly List<BarBlock> barBlocks = new();
        readonly int[] barOrder = new int[MaxCards];
        readonly float[] barCentres = new float[MaxCards];
        readonly Vector3[] barCorners = new Vector3[4];
        RectTransform bar, plate, brandRect, labelRect, minimapRect, eyeRect;
        float barWidth = -1f, barScale = 1f, barTop = PlayerBar.Top, topInset, labelWidth = LabelWidth;
        int barSize = -1;

        public float TopInset => topInset;
        public bool BarShown => bar && bar.gameObject.activeSelf;
        public float BarBottom => BarShown ? barTop + (PlayerBar.CardHeight(barSize) + PlayerBar.PlatePad) * barScale : 0f;
        public float LeftColumnStart => Mathf.Max(LeftColumnTop, BarShown && OverlapsLeftColumn() ? BarBottom + PlayerBar.Clearance : 0f);

        void BuildBar()
        {
            bar = Rect("Player bar", safe, new Vector2(.5f, 1f), new Vector2(0f, -PlayerBar.Top), new Vector2(0f, PlayerBar.Height));
            bar.pivot = new Vector2(.5f, 1f);
            var backing = CardImage("Backing", bar, Plate);
            backing.sprite = LobbyIcons.RoundedSprite(Mathf.RoundToInt(PlayerBar.PlateRadius));
            plate = backing.rectTransform;
            for (int i = 0; i < MaxCards; i++) cards.Add(MakeSeatCard(i));
            bar.gameObject.SetActive(false);
        }

        PlayerCard MakeSeatCard(int index)
        {
            var root = Rect($"Card {index + 1}", bar, new Vector2(.5f, 1f), Vector2.zero, new Vector2(PlayerBar.RoomyCard, PlayerBar.Height));
            root.pivot = new Vector2(.5f, 1f);
            var card = new PlayerCard { root = root };
            card.you = CardImage("You", root, YouEdge);
            card.backing = CardImage("Backing", root, CardBacking);
            card.portrait = Rect("Portrait", root, new Vector2(.5f, 1f), Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
            card.portrait.rectTransform.pivot = new Vector2(.5f, 1f);
            card.portrait.raycastTarget = false;
            card.letter = CardText("Letter", root, 38f, Ink);
            card.letter.fontStyle = FontStyles.Bold;
            card.letter.alignment = TextAlignmentOptions.Center;
            card.shade = CardImage("Shade", root, WreckedShade);
            card.shade.sprite = null;
            card.frame = CardImage("Frame", root, Mint);
            card.status = CardText("Status", root, PlayerBar.StatusSize, LowHp);
            card.status.fontStyle = FontStyles.Bold;
            card.status.enableAutoSizing = true; card.status.fontSizeMin = 9f; card.status.fontSizeMax = PlayerBar.StatusSize;
            OnGameText(card.status);
            card.track = Panel("Health track", root, new Vector2(.5f, 1f), Vector2.zero, Vector2.zero, new Vector2(.5f, 1f), HealthTrack);
            card.fill = Fill("Health", card.track, Health);
            card.name = CardText("Name", root, PlayerBar.NameSize, Cream);
            card.name.fontStyle = FontStyles.Bold;
            card.name.overflowMode = TextOverflowModes.Ellipsis;
            OnGameText(card.name); AddHudShadow(card.name);
            card.score = CardText("Score", root, PlayerBar.ScoreSize, ScoreInk);
            card.score.overflowMode = TextOverflowModes.Ellipsis;
            OnGameText(card.score); AddHudShadow(card.score);
            card.you.enabled = card.shade.enabled = card.letter.enabled = card.portrait.enabled = false;
            SizeSeatCard(card, PlayerBar.RoomySeats);
            root.gameObject.SetActive(false);
            return card;
        }

        Image CardImage(string label, RectTransform parent, Color colour)
        {
            var image = CreateImage(label, parent, new Vector2(.5f, 1f), Vector2.zero, Vector2.zero, colour);
            image.rectTransform.pivot = new Vector2(.5f, 1f);
            image.type = Image.Type.Sliced;
            return image;
        }

        TextMeshProUGUI CardText(string label, RectTransform parent, float size, Color colour)
        {
            var text = Text(label, parent, new Vector2(.5f, 1f), Vector2.zero, Vector2.zero, size, colour);
            text.rectTransform.pivot = new Vector2(.5f, 1f);
            return text;
        }

        static void Place(RectTransform rt, float y, float width, float height)
        {
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(width, height);
        }

        static void SizeSeatCard(PlayerCard card, int seats)
        {
            if (card.shownSeats == seats) return;
            card.shownSeats = seats;
            float width = PlayerBar.CardWidth(seats), side = PlayerBar.Portrait(seats), rim = PlayerBar.Frame;
            int radius = Mathf.RoundToInt(side * .2f);
            card.root.sizeDelta = new Vector2(width, PlayerBar.CardHeight(seats));
            Place(card.you.rectTransform, 3f, side + 6f, side + 6f);
            card.you.sprite = LobbyIcons.RoundedSprite(radius + 3);
            Place(card.backing.rectTransform, 0f, side, side);
            card.backing.sprite = LobbyIcons.RoundedSprite(radius);
            Place(card.portrait.rectTransform, -rim, side - 2f * rim, side - 2f * rim);
            Place(card.letter.rectTransform, 0f, side, side);
            card.letter.fontSize = side * .56f;
            Place(card.shade.rectTransform, -rim, side - 2f * rim, side - 2f * rim);
            Place(card.frame.rectTransform, 0f, side, side);
            card.frame.sprite = LobbyIcons.FrameSprite(radius, Mathf.RoundToInt(rim));
            Place(card.status.rectTransform, -(side * .5f - 10f), width - 4f, 20f);
            Place(card.track, -PlayerBar.HealthY(seats), side, PlayerBar.HealthHeight);
            Place(card.name.rectTransform, -PlayerBar.NameY(seats), width, PlayerBar.NameHeight);
            Place(card.score.rectTransform, -PlayerBar.ScoreY(seats), width, PlayerBar.ScoreHeight);
        }

        void RefreshCards()
        {
            var current = Roster;
            barSeats.Clear(); barTeams.Clear();
            int seats = 0, humans = 0;
            if (current != null)
                for (int i = 0; i < current.Count; i++)
                {
                    var p = current[i];
                    if (!p) continue;
                    seats++;
                    if (SeatHuman(p)) humans++;
                    if (barSeats.Count < MaxCards) { barSeats.Add(p); barTeams.Add(p.Team); }
                }
            bool shown = PlayerBar.Visible(seats);
            if (bar.gameObject.activeSelf != shown) bar.gameObject.SetActive(shown);
            if (shown) LayOutBar();
            SetTopInset(shown ? PlayerBar.ShiftFor(barTop, barScale) : 0f);
            if (!shown) return;
            bool drew = false;
            for (int slot = 0; slot < cards.Count; slot++)
            {
                var card = cards[slot];
                bool used = slot < barSeats.Count;
                if (card.root.gameObject.activeSelf != used) card.root.gameObject.SetActive(used);
                if (used) ShowSeat(card, barSeats[barOrder[slot]], humans, ref drew);
            }
        }

        void LayOutBar()
        {
            int count = barSeats.Count;
            float width = PlayerBar.Arrange(barTeams, barOrder, barCentres);
            for (int slot = 0; slot < count; slot++)
            {
                var card = cards[slot];
                SizeSeatCard(card, count);
                if (card.shownX == barCentres[slot]) continue;
                card.shownX = barCentres[slot];
                card.root.anchoredPosition = new Vector2(card.shownX, 0f);
            }
            float scale = PlayerBar.Scale(width, safe.rect.width);
            float height = PlayerBar.CardHeight(count);
            CollectBlocks();
            float top = PlayerBar.ClearTop(width * scale * .5f, height * scale, barBlocks, PlayerBar.PlatePad * scale);
            if (width == barWidth && scale == barScale && top == barTop && count == barSize) return;
            barWidth = width; barScale = scale; barTop = top; barSize = count;
            bar.sizeDelta = new Vector2(width, height);
            plate.anchoredPosition = new Vector2(0f, PlayerBar.PlatePad);
            plate.sizeDelta = new Vector2(PlayerBar.PlateWidth(width), PlayerBar.PlateHeight(count));
            bar.localScale = new Vector3(scale, scale, 1f);
            bar.anchoredPosition = new Vector2(0f, -top);
        }

        void ShowSeat(PlayerCard card, PlayerController p, int humans, ref bool drew)
        {
            if (card.seat != p)
            {
                card.seat = p;
                card.face = SeatPortrait.For(p);
                card.letter.text = PlayerBar.Letter(p.Initial);
                card.shownName = null; card.shownWins = card.shownStatus = card.shownLook = int.MinValue;
                card.shownPicture = null;
            }
            bool you = p == LocalPlayer;
            string name = SeatTags.Name(p.Name, you, humans);
            if (name != card.shownName) { card.shownName = name; card.name.text = name; }
            int won = showWins && wins != null ? wins(p) : -1;
            if (won != card.shownWins || roundsToWin != card.shownTarget || !ReferenceEquals(scoreUnit, card.shownUnit))
            {
                card.shownWins = won; card.shownTarget = roundsToWin; card.shownUnit = scoreUnit;
                bool scored = won >= 0;
                card.score.text = scored ? SeatScore(won, roundsToWin, scoreUnit) : "";
                if (card.score.gameObject.activeSelf != scored) card.score.gameObject.SetActive(scored);
            }
            float health = p.Health ? p.Health.Fraction : 0f;
            card.fill.fillAmount = health;
            card.fill.color = PlayerBar.Healthy(health) ? Health : Low;
            int status = p.IsEliminated ? -1 : p.IsDowned ? Mathf.CeilToInt(p.Health.BleedOutLeft) : -2;
            if (status != card.shownStatus)
            {
                card.shownStatus = status;
                card.status.text = PlayerBar.Status(status == -1, status >= 0, status);
            }
            Texture picture = null;
            if (card.face)
            {
                bool render = !drew && card.face.Wants;
                picture = card.face.Refresh(render);
                drew |= render;
            }
            int look = status == -1 ? 2 : status >= 0 ? 1 : 0;
            var vivid = PlayerHud.Vivid(p.Color);
            if (look == card.shownLook && picture == card.shownPicture && you == card.shownYou && vivid == card.vivid) return;
            card.shownLook = look; card.shownPicture = picture; card.shownYou = you; card.vivid = vivid;
            bool drawn = picture;
            card.portrait.texture = picture;
            card.portrait.enabled = drawn;
            card.portrait.color = look == 2 ? WreckedTint : Color.white;
            card.letter.enabled = !drawn;
            card.letter.color = look == 2 ? Hex(0x2a3333) : Ink;
            card.backing.color = drawn ? CardBacking : look == 2 ? WreckedFrame : vivid;
            card.shade.enabled = look != 0;
            card.shade.color = look == 2 ? WreckedShade : DownShade;
            card.frame.color = look == 2 ? WreckedFrame : vivid;
            card.pulsing = look == 1;
            card.you.enabled = you;
        }

        void PulseCards()
        {
            if (!BarShown) return;
            float wave = .5f + .5f * Mathf.Sin(Time.unscaledTime * 7f);
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card.pulsing && card.root.gameObject.activeSelf) card.frame.color = Color.Lerp(card.vivid, PulseFrame, wave);
            }
        }

        void MeasureLabel()
        {
            float widest = 0f;
            foreach (var text in new[] { matchTag, matchTitle, matchDetail })
                if (text && text.text.Length > 0) widest = Mathf.Max(widest, text.GetPreferredValues(text.text).x);
            labelWidth = Mathf.Min(LabelWidth, widest);
        }

        void CollectBlocks()
        {
            barBlocks.Clear();
            Block(brandRect);
            if (labelRect && labelRect.gameObject.activeInHierarchy && labelWidth > 0f)
            {
                Block(labelRect);
                var label = barBlocks[barBlocks.Count - 1];
                barBlocks[barBlocks.Count - 1] = new BarBlock(label.Left, label.Left + labelWidth, label.Top, label.Bottom);
            }
            Block(minimapRect);
            Block(eyeRect);
            if (!TouchControlsShown || !touchRoot.TryGetComponent(out TouchLayoutView touch)) return;
            for (var part = TouchPart.Move; part <= TouchPart.Eye; part++)
            {
                var rt = touch.Part(part);
                if (rt) Block(rt);
            }
        }

        void Block(RectTransform rt)
        {
            if (!rt || !rt.gameObject.activeInHierarchy) return;
            rt.GetWorldCorners(barCorners);
            var area = safe.rect;
            float left = float.MaxValue, right = float.MinValue, low = float.MaxValue, high = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var local = safe.InverseTransformPoint(barCorners[i]);
                left = Mathf.Min(left, local.x); right = Mathf.Max(right, local.x);
                low = Mathf.Min(low, local.y); high = Mathf.Max(high, local.y);
            }
            float middle = area.center.x;
            barBlocks.Add(new BarBlock(left - middle, right - middle, area.yMax - high, area.yMax - low));
        }

        bool OverlapsLeftColumn()
        {
            float half = (barWidth * .5f + PlayerBar.PlatePad) * barScale, columnRight = BrandX + 280f - safe.rect.width * .5f;
            return -half < columnRight;
        }

        void SetTopInset(float inset)
        {
            if (Mathf.Approximately(inset, topInset)) return;
            topInset = inset;
            PlaceTop();
        }

        void PlaceTop()
        {
            bool portrait = lastWidth < lastHeight;
            if (chip) chip.anchoredPosition = new Vector2(0f, ChipY - topInset);
            if (banner) banner.anchoredPosition = new Vector2(0f, BannerY - topInset);
            if (status) status.anchoredPosition = new Vector2(0f, (portrait ? PortraitStatusY : StatusY) - topInset);
            if (hint) hint.anchoredPosition = new Vector2(0f, (portrait ? PortraitStatusY : StatusY) - topInset);
            if (playRect) playRect.anchoredPosition = portrait ? new Vector2(30f, 340f) : new Vector2(0f, PlayY - topInset);
            if (modeActions) ((RectTransform)modeActions.transform).anchoredPosition = new Vector2(0f, ModeActionsY - topInset);
        }
    }
}
