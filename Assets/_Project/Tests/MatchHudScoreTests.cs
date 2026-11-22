using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class MatchHudScoreTests
    {
        const float Slow = 10f, Slack = .5f, SmallestText = 15f;

        readonly List<Object> made = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            MatchStats.Begin();
            LetterInventory.FreeSpelling = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var thing in made) if (thing) Object.Destroy(thing);
            made.Clear();
            Time.timeScale = 1f;
            LetterInventory.FreeSpelling = false;
            MatchStats.Begin();
            TouchBinding.Shared.ReleaseAll();
            TouchBinding.Shared.Enabled = TouchBinding.Shared.OverlayDesktop = false;
            TouchBinding.Shared.OverlayBindingId = null;
            SummonedThing.ClearAll();
            yield return TestScenes.Reset();
        }

        static PlayerJoinManager Joins => Object.FindAnyObjectByType<PlayerJoinManager>();
        static GameHud SceneHud => Object.FindAnyObjectByType<GameHud>();

        void Ground()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = Vector3.down * .5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            made.Add(ground);
            TilePool.Ensure();
        }

        PlayerController Spawn(int seat, int team, InputBinding binding = null)
        {
            var at = new Vector3(seat * 3f - 6f, 0f, 0f);
            var player = Object.Instantiate(GameAssets.I.playerPrefab, at, Quaternion.identity);
            player.Setup(seat, binding ?? new ScriptedBinding());
            player.Respawn(at);
            player.Inventory.Collects = false;
            player.Team = team;
            made.Add(player.gameObject);
            return player;
        }

        PlayerController[] Seats(params int[] teams)
        {
            Ground();
            return teams.Select((team, seat) => Spawn(seat, team)).ToArray();
        }

        IEnumerator Hud(PlayerController you, System.Action<GameHud> ready)
        {
            var hud = new GameObject("Score HUD", typeof(Canvas)).AddComponent<GameHud>();
            made.Add(hud.gameObject);
            yield return null;
            hud.ShowTouchControls(false);
            hud.Follow(you);
            yield return TestScenes.WaitUntil(() => hud.AcceptsGameplayInput, Slow, "the gameplay HUD");
            ready(hud);
        }

        static RectTransform Bar(GameHud hud) => (RectTransform)hud.transform.Find("Safe HUD/Player bar");
        static RectTransform Part(GameHud hud, string path) => (RectTransform)Bar(hud).Find(path);
        static float X(GameHud hud, int card) => Part(hud, $"Card {card}").anchoredPosition.x;
        static string Name(GameHud hud, int card) => Part(hud, $"Card {card}/Name").GetComponent<TMP_Text>().text;
        static string CardScore(GameHud hud, int card) => Part(hud, $"Card {card}/Score").GetComponent<TMP_Text>().text;
        static bool Leads(GameHud hud, int card) => Part(hud, $"Card {card}/Lead").GetComponent<Image>().enabled;
        static bool Marked(GameHud hud, string mark) => Part(hud, "Centre/" + mark).GetComponent<Image>().enabled;

        static void AssertSameColour(Color expected, Color actual, string message)
        {
            Assert.AreEqual(expected.r, actual.r, .01f, message);
            Assert.AreEqual(expected.g, actual.g, .01f, message);
            Assert.AreEqual(expected.b, actual.b, .01f, message);
        }

        static void AssertCentreBetween(GameHud hud, int leftCard, int rightCard, float cardWidth)
        {
            var centre = Part(hud, "Centre");
            Assert.IsTrue(centre.gameObject.activeSelf, "The centre block shows.");
            Assert.AreEqual(0f, centre.anchoredPosition.x, Slack, "The centre block sits in the middle of the bar.");
            Assert.AreEqual(ScoreLine.CentreWidth, centre.rect.width, Slack);
            float half = ScoreLine.CentreWidth * .5f;
            Assert.LessOrEqual(X(hud, leftCard) + cardWidth * .5f, -half + Slack, "The left cards stop before the centre.");
            Assert.GreaterOrEqual(X(hud, rightCard) - cardWidth * .5f, half - Slack, "The right cards start after it.");
            var plate = Part(hud, "Backing");
            Assert.AreEqual(PlayerBar.PlateWidth(Bar(hud).rect.width), plate.rect.width, Slack, "One plate spans both sides and the centre.");
            var caption = Part(hud, "Centre/Caption");
            Assert.LessOrEqual(caption.rect.height - caption.anchoredPosition.y, Bar(hud).rect.height + Slack, "The centre fits the bar's height.");
            Assert.AreEqual(Bar(hud).rect.height, centre.rect.height, Slack);
        }

        [UnityTest]
        public IEnumerator TwoSidesShowTheirScoresEitherSideOfTheClock()
        {
            var seats = Seats(0, 1, 0, 1);
            GameHud hud = null;
            yield return Hud(seats[0], h => hud = h);
            hud.SetScoreboard(seats, p => p.Team == 0 ? 2 : 1, 3, true);
            hud.SetTimer("1:23");
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.Score && hud.CentreScore == "2 : 1", Slow, "the score line");
            Assert.AreEqual("1:23", hud.CentreClock, "The match clock sits between the scores.");
            Assert.AreEqual("", hud.CentreCaption);
            Assert.AreEqual(ScoreLine.Separator, Part(hud, "Centre/Colon").GetComponent<TMP_Text>().text);
            CollectionAssert.AreEqual(new[] { "P1", "P3", "P2", "P4" }, Enumerable.Range(1, 4).Select(i => Name(hud, i)), "Each side's cards are grouped on its own side.");
            for (int card = 2; card <= 4; card++) Assert.Greater(X(hud, card), X(hud, card - 1), "left to right");
            AssertCentreBetween(hud, 2, 3, PlayerBar.RoomyCard);
            Assert.AreEqual(PlayerBar.CardHeight(4), Bar(hud).rect.height, Slack, "The bar is no taller than before.");
            AssertSameColour(PlayerHud.Vivid(seats[0].Color), Part(hud, "Centre/Left score").GetComponent<TMP_Text>().color, "The left score wears the left side's colour.");
            AssertSameColour(PlayerHud.Vivid(seats[1].Color), Part(hud, "Centre/Right score").GetComponent<TMP_Text>().color, "The right score wears the right side's colour.");
            Assert.AreEqual("2/3", CardScore(hud, 1), "Cards keep their own scores.");
            Assert.IsFalse(Part(hud, "Centre/Target").GetComponent<RawImage>().enabled, "No target picture in a plain score line.");
            Assert.IsFalse(Marked(hud, "Left lead") || Marked(hud, "Right lead"), "The colours and numbers carry a plain score.");
            foreach (var text in Part(hud, "Centre").GetComponentsInChildren<TMP_Text>(true))
                Assert.GreaterOrEqual(text.enableAutoSizing ? text.fontSizeMin : text.fontSize, SmallestText, text.name);
        }

        [UnityTest]
        public IEnumerator ADuelIsTwoSidesAndThreeOrMoreAreNot()
        {
            var seats = Seats(0, 1, 2);
            GameHud hud = null;
            yield return Hud(seats[0], h => hud = h);
            hud.SetScoreboard(seats, p => p.Index, 3, true);
            yield return TestScenes.WaitUntil(() => Bar(hud).gameObject.activeSelf && CardScore(hud, 3) == "2/3", Slow, "three cards");
            Assert.AreEqual(ScoreCentre.None, hud.Centre, "Everyone for themselves has no centre score.");
            Assert.IsFalse(Part(hud, "Centre").gameObject.activeSelf);
            Assert.AreEqual("", hud.CentreScore);
            Assert.AreEqual(PlayerBar.RoomyCard + PlayerBar.SideGap, X(hud, 2) - X(hud, 1), Slack, "Each seat is its own side: only two outlines apart.");
            hud.SetScoreboard(new[] { seats[0], seats[2] }, p => p.Index + 1, 0, true);
            yield return TestScenes.WaitUntil(() => hud.CentreScore == "1 : 3", Slow, "a duel's score line");
            AssertCentreBetween(hud, 1, 2, PlayerBar.RoomyCard);
            hud.SetScoreboard(new[] { seats[0], seats[2] }, _ => 0, 0, false);
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.None, Slow, "no score line without scores");
            hud.SetScoreboard(new[] { seats[0] }, _ => 0, 3, true);
            yield return TestScenes.WaitUntil(() => !Bar(hud).gameObject.activeSelf, Slow, "no bar alone");
            Assert.AreEqual(ScoreCentre.None, hud.Centre);
        }

        [UnityTest]
        public IEnumerator EveryoneForThemselvesKeepsTheClockInTheMiddleOfTheBar()
        {
            var seats = Seats(0, 1, 2, 3);
            GameHud hud = null;
            yield return Hud(seats[0], h => hud = h);
            hud.SetScoreboard(seats, p => p.Index, 3, true);
            hud.SetTimer("3:30");
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.Clock && hud.CentreClock == "3:30", Slow, "the clock between the cards");
            Assert.AreEqual("", hud.CentreScore, "Four sides have no score line.");
            Assert.AreEqual("", hud.CentreCaption);
            Assert.IsFalse(hud.transform.Find("Safe HUD/Side column/Timer").gameObject.activeSelf, "One clock only, never under the minimap.");
            Assert.IsFalse(hud.transform.Find("Safe HUD/Side column/Alive").gameObject.activeSelf, "The portraits already show who is up.");
            var centre = Part(hud, "Centre");
            Assert.IsTrue(centre.gameObject.activeSelf);
            Assert.AreEqual(0f, centre.anchoredPosition.x, Slack);
            Assert.AreEqual(ScoreLine.ClockWidth, centre.rect.width, Slack, "A clock alone takes less room than a score line.");
            float half = ScoreLine.ClockWidth * .5f;
            Assert.LessOrEqual(X(hud, 2) + PlayerBar.RoomyCard * .5f, -half + Slack, "Two cards sit left of the clock...");
            Assert.GreaterOrEqual(X(hud, 3) - PlayerBar.RoomyCard * .5f, half - Slack, "...and two right of it.");
            Assert.AreEqual(-half - PlayerBar.Outline, X(hud, 2) + PlayerBar.RoomyCard * .5f, Slack, "The seat outline meets the clock's outline, no gap.");
            Assert.IsTrue(Part(hud, "Centre/Outline").GetComponent<Image>().enabled, "The clock block has its own outline.");
            var clock = Part(hud, "Centre/Clock").GetComponent<TMP_Text>();
            Assert.GreaterOrEqual(clock.fontSizeMin, SmallestText);
            Assert.Greater(clock.fontSizeMax, SmallestText * 2f, "The lone clock reads at score size.");
            Assert.IsFalse(Part(hud, "Centre/Left score").GetComponent<TMP_Text>().enabled);
            Assert.IsFalse(Part(hud, "Centre/Right score").GetComponent<TMP_Text>().enabled);
            Assert.IsFalse(Part(hud, "Centre/Colon").GetComponent<TMP_Text>().enabled);
            Assert.AreEqual("3/3", CardScore(hud, 4), "Cards keep their own scores.");
            hud.SetTimer("");
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.None, Slow, "no clock, no centre");
            Assert.IsFalse(Part(hud, "Centre").gameObject.activeSelf);
            Assert.AreEqual(PlayerBar.RoomyCard + PlayerBar.SideGap, X(hud, 2) - X(hud, 1), Slack, "The cards close up again.");
        }

        [UnityTest]
        public IEnumerator AloneTheClockSitsOnItsOwnPlateAtTheTop()
        {
            var seats = Seats(0);
            GameHud hud = null;
            yield return Hud(seats[0], h => hud = h);
            hud.SetScoreboard(seats, _ => 0, 0, false);
            hud.SetTimer("4:59");
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.Clock && hud.CentreClock == "4:59", Slow, "a clock plate");
            Assert.IsTrue(Bar(hud).gameObject.activeSelf);
            Assert.IsFalse(hud.BarSeated, "A lone player gets no card.");
            for (int card = 1; card <= PlayerBar.MostSeats; card++) Assert.IsFalse(Part(hud, $"Card {card}").gameObject.activeSelf, $"card {card}");
            Assert.IsFalse(Part(hud, "Scores").gameObject.activeSelf, "Nothing to rank alone.");
            Assert.IsFalse(hud.ScoreboardReady);
            Assert.AreEqual(ScoreLine.ClockWidth, Bar(hud).rect.width, Slack);
            Assert.AreEqual(ScoreLine.ClockRow, Bar(hud).rect.height, Slack);
            Assert.AreEqual(PlayerBar.PlateWidth(ScoreLine.ClockWidth), Part(hud, "Backing").rect.width, Slack, "The plate pads the clock like the cards.");
            Assert.Greater(hud.TopInset, 0f, "Notices still clear the plate...");
            Assert.Less(hud.TopInset, PlayerBar.Shift, "...but a short plate moves them less than the bar.");
            Assert.IsFalse(hud.transform.Find("Safe HUD/Side column/Timer").gameObject.activeSelf);
            hud.SetTimer("");
            yield return TestScenes.WaitUntil(() => !Bar(hud).gameObject.activeSelf, Slow, "no plate without a clock");
            Assert.AreEqual(0f, hud.TopInset);
        }

        [UnityTest]
        public IEnumerator TeamMatesShareTheirSideColourOnTheBarAndOnTheFloor()
        {
            var seats = Seats(0, 0, 1, 1);
            GameHud hud = null;
            yield return Hud(seats[0], h => hud = h);
            hud.SetScoreboard(seats, _ => 0, 3, true);
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.Score && Name(hud, 4).Length > 0, Slow, "a duos bar");
            Color red = PlayerHud.Vivid(seats[0].Color), green = PlayerHud.Vivid(seats[2].Color);
            Color Frame(int card) => Part(hud, $"Card {card}/Frame").GetComponent<Image>().color;
            Color Tag(int card) => Part(hud, $"Card {card}/Name").GetComponent<TMP_Text>().color;
            AssertSameColour(red, Frame(1), "P1 is framed in the side colour.");
            AssertSameColour(red, Frame(2), "P2 is framed in P1's colour, as on the line-up.");
            AssertSameColour(green, Frame(3), "The other side takes its first seat's colour.");
            AssertSameColour(green, Frame(4), "Both of them.");
            AssertSameColour(PlayerHud.Vivid(seats[1].Color), Tag(2), "P2's own colour stays on the name.");
            AssertSameColour(PlayerHud.Vivid(seats[3].Color), Tag(4), "AI 4 keeps its own colour on the name.");
            AssertSameColour(red, Part(hud, "Centre/Left score").GetComponent<TMP_Text>().color, "The score matches the frames.");
            AssertSameColour(green, Part(hud, "Centre/Right score").GetComponent<TMP_Text>().color, "Both sides.");
            Color Ring(PlayerController p)
            {
                var material = p.GetComponentInChildren<PlayerHud>(true).SeatRing.sharedMaterial;
                return material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
            }
            yield return TestScenes.WaitUntil(() => seats.All(p => p.GetComponentInChildren<PlayerHud>(true).SeatRing), Slow, "a ring under every seat");
            AssertSameColour(red, Ring(seats[1]), "P2's floor ring is the side colour too.");
            AssertSameColour(green, Ring(seats[3]), "The other side's rings too.");

            foreach (var p in seats) p.Team = p.Index;
            yield return TestScenes.WaitUntil(() => Frame(2) != red, Slow, "free-for-all colours");
            AssertSameColour(PlayerHud.Vivid(seats[1].Color), Frame(2), "Everyone for themselves wears their own colour.");
            AssertSameColour(new Color(1f, .941f, .851f), Tag(2), "Names go back to cream.");
            yield return TestScenes.WaitUntil(() => Ring(seats[1]) != red, Slow, "the ring to follow");
            AssertSameColour(PlayerHud.Vivid(seats[1].Color), Ring(seats[1]), "And so does the ring.");
        }

        static Rect Box(GameHud hud, string path)
        {
            var corners = new Vector3[4];
            Part(hud, path).GetWorldCorners(corners);
            var bar = Bar(hud);
            Vector3 low = bar.InverseTransformPoint(corners[0]), high = bar.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(low.x, low.y, high.x, high.y);
        }

        static bool Outlined(GameHud hud, int side) => Part(hud, $"Side {side}").GetComponent<Image>().enabled;

        static void AssertTouching(Rect left, Rect right, string message) => Assert.AreEqual(left.xMax, right.xMin, Slack, message);

        static void AssertInside(RectTransform outer, RectTransform inner, string message)
        {
            var corners = new Vector3[4];
            outer.GetWorldCorners(corners);
            var o = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            inner.GetWorldCorners(corners);
            var i = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            Assert.IsTrue(i.xMin >= o.xMin - Slack && i.xMax <= o.xMax + Slack && i.yMin >= o.yMin - Slack && i.yMax <= o.yMax + Slack, $"{message}: {i} inside {o}");
        }

        static void AssertSquare(GameHud hud, int cards)
        {
            var line = Part(hud, "Card 1/Frame").GetComponent<Image>().sprite;
            Assert.IsNotNull(line, "Frames are drawn from a line box...");
            Assert.AreEqual(4, line.texture.width, "...a square one, not a rounded sprite.");
            Assert.AreEqual(FilterMode.Point, line.texture.filterMode);
            Assert.IsNull(Part(hud, "Backing").GetComponent<Image>().sprite, "The plate is square.");
            Assert.AreSame(line, Part(hud, "Centre/Outline").GetComponent<Image>().sprite, "So is the centre block's outline.");
            for (int side = 1; side <= PlayerBar.MostSeats; side++)
                Assert.AreSame(line, Part(hud, $"Side {side}").GetComponent<Image>().sprite, $"Side {side} outline is square");
            for (int card = 1; card <= cards; card++)
            {
                Assert.AreSame(line, Part(hud, $"Card {card}/Frame").GetComponent<Image>().sprite, $"card {card} frame");
                Assert.AreSame(line, Part(hud, $"Card {card}/You").GetComponent<Image>().sprite, $"card {card} rim");
                foreach (var path in new[] { "Backing", "Shade", "Health track", "Health track/Health", "Stamina track", "Stamina track/Stamina" })
                    Assert.IsNull(Part(hud, $"Card {card}/{path}").GetComponent<Image>().sprite, $"card {card} {path} is square");
            }
        }

        static void AssertNamesShow(GameHud hud, int cards)
        {
            for (int card = 1; card <= cards; card++)
                foreach (var line in new[] { "Name", "Score" })
                {
                    var text = Part(hud, $"Card {card}/{line}").GetComponent<TMP_Text>();
                    text.ForceMeshUpdate();
                    Assert.Greater(text.text.Length, 0, $"card {card} {line}");
                    Assert.IsFalse(text.isTextTruncated, $"card {card} {line} shows whole: {text.text}");
                    Assert.GreaterOrEqual(text.fontSize, SmallestText, $"card {card} {line}");
                }
        }

        [UnityTest]
        public IEnumerator TeamMatesTouchAndEachSideHasOneOutlineAgainstTheScore()
        {
            var seats = Seats(0, 1, 0, 1);
            GameHud hud = null;
            yield return Hud(seats[0], h => hud = h);
            hud.SetScoreboard(seats, p => p.Team == 0 ? 2 : 1, 3, true);
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.Score && hud.CentreScore == "2 : 1", Slow, "a two-side bar");
            yield return null;
            Canvas.ForceUpdateCanvases();
            Rect Card(int card) => Box(hud, $"Card {card}");
            Rect Health(int card) => Box(hud, $"Card {card}/Health track");
            Rect Stamina(int card) => Box(hud, $"Card {card}/Stamina track");
            Assert.AreEqual(PlayerBar.Portrait(4), Card(1).width, Slack, "A card is as wide as its portrait.");
            AssertTouching(Card(1), Card(2), "Team-mates' cards touch.");
            AssertTouching(Card(3), Card(4), "On both sides.");
            for (int card = 1; card <= 4; card++)
            {
                Assert.AreEqual(Box(hud, $"Card {card}/Frame").yMin, Health(card).yMax, Slack, $"card {card}: the health bar hangs right under the portrait");
                Assert.AreEqual(Card(card).center.x, Health(card).center.x, Slack, $"card {card}: health is centred");
                Assert.AreEqual(Card(card).center.x, Stamina(card).center.x, Slack, $"card {card}: stamina is centred");
            }
            foreach (var (a, b) in new[] { (1, 2), (3, 4) })
            {
                float join = Health(b).xMin - Health(a).xMax;
                Assert.GreaterOrEqual(join, -Slack, $"cards {a} and {b}: health bars never overlap");
                Assert.LessOrEqual(join, PlayerBar.Hairline + Slack, $"cards {a} and {b}: health bars run on with only a hairline between");
                Assert.GreaterOrEqual(Stamina(b).xMin - Stamina(a).xMax, 10f, $"cards {a} and {b}: the shorter stamina bars leave a gap that marks each player");
                Assert.Less(Stamina(a).width, Health(a).width);
                Assert.GreaterOrEqual(Stamina(a).height, PlayerBar.StaminaHeight - Slack, "Stamina is thick enough to read.");
            }
            Assert.IsTrue(Outlined(hud, 1) && Outlined(hud, 2), "One outline per side...");
            Assert.IsFalse(Outlined(hud, 3), "...and no more.");
            Rect left = Box(hud, "Side 1"), right = Box(hud, "Side 2"), middle = Box(hud, "Centre/Outline");
            Assert.AreEqual(Card(1).xMin - PlayerBar.Outline, left.xMin, Slack, "The left outline wraps both team-mates...");
            Assert.AreEqual(Card(2).xMax + PlayerBar.Outline, left.xMax, Slack);
            Assert.AreEqual(Card(3).xMin - PlayerBar.Outline, right.xMin, Slack, "...and so does the right.");
            Assert.AreEqual(Card(4).xMax + PlayerBar.Outline, right.xMax, Slack);
            Assert.AreEqual(Card(1).yMax + PlayerBar.Outline, left.yMax, Slack, "The outline clears the cards above...");
            Assert.AreEqual(Card(1).yMin - PlayerBar.Outline, left.yMin, Slack, "...and below.");
            Assert.IsTrue(Part(hud, "Centre/Outline").GetComponent<Image>().enabled, "The score block has its own outline.");
            AssertTouching(left, middle, "The left side meets the score block with no gap and no overlap...");
            AssertTouching(middle, right, "...and so does the right.");
            Assert.AreEqual(left.yMax, middle.yMax, Slack, "All three boxes line up.");
            Assert.AreEqual(left.yMin, middle.yMin, Slack);
            var plate = Box(hud, "Backing");
            foreach (var box in new[] { left, middle, right })
                Assert.IsTrue(plate.xMin <= box.xMin + Slack && plate.xMax >= box.xMax - Slack && plate.yMin <= box.yMin + Slack && plate.yMax >= box.yMax - Slack, $"The plate holds {box}");
            AssertSameColour(PlayerHud.Vivid(seats[0].Color), Part(hud, "Side 1").GetComponent<Image>().color, "The left outline wears its side's colour.");
            AssertSameColour(PlayerHud.Vivid(seats[1].Color), Part(hud, "Side 2").GetComponent<Image>().color, "The right one too.");
            AssertSquare(hud, 4);
            AssertNamesShow(hud, 4);
        }

        [UnityTest]
        public IEnumerator EveryoneForThemselvesOutlinesEachSeatInItsOwnColour()
        {
            var seats = Seats(0, 1, 2, 3);
            GameHud hud = null;
            yield return Hud(seats[0], h => hud = h);
            hud.SetScoreboard(seats, p => p.Index, 3, true);
            hud.SetTimer("3:30");
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.Clock && CardScore(hud, 4) == "3/3", Slow, "four seats and a clock");
            yield return null;
            Canvas.ForceUpdateCanvases();
            for (int side = 1; side <= 4; side++)
            {
                Assert.IsTrue(Outlined(hud, side), $"Seat {side} is its own side.");
                Assert.AreEqual(Box(hud, $"Card {side}").width + 2f * PlayerBar.Outline, Box(hud, $"Side {side}").width, Slack, $"Side {side} wraps one card");
                AssertSameColour(PlayerHud.Vivid(seats[side - 1].Color), Part(hud, $"Side {side}").GetComponent<Image>().color, $"Side {side} wears its seat's colour");
            }
            Assert.IsFalse(Outlined(hud, 5));
            var middle = Box(hud, "Centre/Outline");
            AssertTouching(Box(hud, "Side 1"), Box(hud, "Side 2"), "Neighbouring outlines touch...");
            AssertTouching(Box(hud, "Side 2"), middle, "...the clock block...");
            AssertTouching(middle, Box(hud, "Side 3"), "...on both sides...");
            AssertTouching(Box(hud, "Side 3"), Box(hud, "Side 4"), "...with no space between.");
            AssertSquare(hud, 4);
            AssertNamesShow(hud, 4);
        }

        [UnityTest]
        public IEnumerator ACrewIsOneOutlinedGroupWithTheClockTouchingOnItsRight()
        {
            var seats = Seats(0, 0);
            GameHud hud = null;
            yield return Hud(seats[0], h => hud = h);
            hud.SetScoreboard(seats, _ => 0, 3, true);
            hud.SetTimer("5:29");
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.Clock && Name(hud, 2).Length > 0, Slow, "a crew and its clock");
            yield return null;
            Canvas.ForceUpdateCanvases();
            Rect Card(int card) => Box(hud, $"Card {card}");
            CollectionAssert.AreEqual(new[] { "P1", "P2" }, new[] { Name(hud, 1), Name(hud, 2) });
            AssertTouching(Card(1), Card(2), "The crew's cards touch, not split around the clock.");
            Assert.IsTrue(Outlined(hud, 1), "One outline wraps the crew...");
            Assert.IsFalse(Outlined(hud, 2), "...only one.");
            Rect crew = Box(hud, "Side 1"), clock = Box(hud, "Centre/Outline");
            Assert.AreEqual(Card(1).xMin - PlayerBar.Outline, crew.xMin, Slack);
            Assert.AreEqual(Card(2).xMax + PlayerBar.Outline, crew.xMax, Slack);
            Assert.IsTrue(Part(hud, "Centre/Outline").GetComponent<Image>().enabled, "The clock keeps its own outline.");
            Assert.AreEqual(ScoreLine.ClockWidth, clock.width, Slack);
            AssertTouching(crew, clock, "The clock sits right of the crew, touching, no overlap.");
            Assert.AreEqual(-crew.xMin, clock.xMax, Slack, "The crew and the clock are centred together.");
            Assert.AreEqual(Bar(hud).rect.width * .5f, clock.xMax, Slack, "Nothing else on the bar.");
            Assert.AreEqual(crew.yMax, clock.yMax, Slack, "Both boxes line up.");
            Assert.AreEqual(crew.yMin, clock.yMin, Slack);
            var plate = Box(hud, "Backing");
            Assert.AreEqual(-plate.xMin, plate.xMax, Slack, "The plate is centred on screen.");
            AssertSquare(hud, 2);
            AssertNamesShow(hud, 2);

            seats[1].Team = 1;
            yield return TestScenes.WaitUntil(() => Outlined(hud, 2), Slow, "two sides again");
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(0f, Part(hud, "Centre").anchoredPosition.x, Slack, "Versus keeps the clock in the middle, between the sides.");
            AssertTouching(Box(hud, "Side 1"), Box(hud, "Centre/Outline"), "Each side meets the clock...");
            AssertTouching(Box(hud, "Centre/Outline"), Box(hud, "Side 2"), "...from its own side.");
        }

        [UnityTest]
        public IEnumerator EightSeatsInTwoTeamsTouchAndFitPhoneWindows()
        {
            var seats = Seats(0, 1, 0, 1, 0, 1, 0, 1);
            GameHud hud = null;
            yield return Hud(seats[0], h => hud = h);
            hud.SetScoreboard(seats, p => p.Team == 0 ? 1 : 0, 3, true);
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.Score && Name(hud, 8).Length > 0, Slow, "an eight-seat bar");
            Assert.Less(Bar(hud).rect.width, 892f, "Eight seats take less room than the old spaced cards.");
            var safe = (RectTransform)hud.transform.Find("Safe HUD");
            foreach (var units in new[] { new Vector2(1920f, 1080f), new Vector2(2119.6f, 978.3f) })
            {
                safe.anchorMin = safe.anchorMax = safe.pivot = Vector2.zero;
                safe.anchoredPosition = Vector2.zero;
                safe.sizeDelta = units;
                yield return new WaitForSecondsRealtime(.2f);
                Canvas.ForceUpdateCanvases();
                for (int card = 2; card <= 8; card++)
                    if (card != 5) AssertTouching(Box(hud, $"Card {card - 1}"), Box(hud, $"Card {card}"), $"{units}: cards {card - 1} and {card} touch");
                AssertTouching(Box(hud, "Side 1"), Box(hud, "Centre/Outline"), $"{units}: the left team meets the score");
                AssertTouching(Box(hud, "Centre/Outline"), Box(hud, "Side 2"), $"{units}: so does the right");
                Assert.IsFalse(Outlined(hud, 3), $"{units}: two teams, two outlines");
                AssertInside(safe, Part(hud, "Backing"), $"{units}: the plate fits the window");
                AssertSquare(hud, 8);
                AssertNamesShow(hud, 8);
            }
        }

        [UnityTest]
        public IEnumerator ADirectorCanNameTheSideScore()
        {
            var seats = Seats(0, 1, 0, 1);
            GameHud hud = null;
            yield return Hud(seats[0], h => hud = h);
            hud.SetScoreboard(seats, p => p.Index, 0, true, FreeForAllDirector.Unit);
            yield return TestScenes.WaitUntil(() => hud.CentreScore == "2 : 3", Slow, "the best member's score by default");
            hud.SetSideScores(team => team == 0 ? 12 : 9, WarDirector.LivesCaption);
            yield return TestScenes.WaitUntil(() => hud.CentreScore == "12 : 9", Slow, "the named side score");
            Assert.AreEqual(WarDirector.LivesCaption, hud.CentreCaption);
            Assert.AreEqual("2 KO", CardScore(hud, 2), "Seat scores still come from SetScoreboard.");
            hud.SetSideScores(null);
            yield return TestScenes.WaitUntil(() => hud.CentreScore == "2 : 3" && hud.CentreCaption == "", Slow, "the default again");
        }

        [UnityTest]
        public IEnumerator ATargetSitsInTheCentreWithProgressAndALeader()
        {
            var seats = Seats(0, 1, 2, 3);
            GameHud hud = null;
            yield return Hud(seats[0], h => hud = h);
            var done = new[] { 3, 4, 1, 0 };
            hud.SetScoreboard(seats, p => done[p.Index], 18, true, ShoppingListDirector.Unit);
            hud.SetTarget("BAT");
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.Target && hud.LeadingSeat == seats[1], Slow, "the target and its leader");
            Assert.AreEqual("BAT", hud.TargetWord);
            Assert.AreEqual("BAT", hud.CentreCaption);
            Assert.AreEqual("", hud.CentreScore, "Four sides keep their progress on their cards.");
            Assert.AreEqual("3/18 items", CardScore(hud, 1));
            AssertCentreBetween(hud, 2, 3, PlayerBar.RoomyCard);
            var picture = Part(hud, "Centre/Target").GetComponent<RawImage>();
            Assert.AreEqual(Wreckabulary.UI.ItemArt.TryGet("BAT", out _, out _), picture.enabled, "The item's own picture is the target.");
            if (picture.enabled) Assert.IsNotNull(picture.texture);
            CollectionAssert.AreEqual(new[] { false, true, false, false }, Enumerable.Range(1, 4).Select(i => Leads(hud, i)), "Only the leader's card is marked.");
            done[0] = 4;
            yield return TestScenes.WaitUntil(() => !hud.LeadingSeat, Slow, "a tie has no leader");
            Assert.IsFalse(Enumerable.Range(1, 4).Any(i => Leads(hud, i)));

            hud.SetScoreboard(new[] { seats[0], seats[1] }, p => done[p.Index], 18, true, ShoppingListDirector.Unit);
            done[0] = 3;
            yield return TestScenes.WaitUntil(() => hud.CentreScore == "3/18 : 4/18", Slow, "progress per side");
            yield return TestScenes.WaitUntil(() => hud.LeadingSide == ScoreLine.Right, Slow, "the right side leads");
            Assert.IsTrue(Marked(hud, "Right lead"));
            Assert.IsFalse(Marked(hud, "Left lead"));
            Assert.AreSame(seats[1], hud.LeadingSeat);
            Assert.IsFalse(Leads(hud, 1) || Leads(hud, 2), "With two sides the centre carries the mark.");
            done[0] = 5;
            yield return TestScenes.WaitUntil(() => hud.LeadingSide == ScoreLine.Left && hud.CentreScore == "5/18 : 4/18", Slow, "the lead changes hands");
            Assert.IsTrue(Marked(hud, "Left lead"));
            Assert.IsFalse(Marked(hud, "Right lead"));

            hud.SetTarget(null);
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.Score && hud.CentreScore == "5 : 4", Slow, "a plain score line again");
            Assert.AreEqual("", hud.TargetWord);
            Assert.IsFalse(Part(hud, "Centre/Target").GetComponent<RawImage>().enabled);
            Assert.IsFalse(Marked(hud, "Left lead") || Marked(hud, "Right lead"));
        }

        [UnityTest]
        public IEnumerator TheScoreboardShowsABlockPerSideAndZerosBeforeAnythingIsRecorded()
        {
            var seats = Seats(0, 1, 0, 1);
            GameHud hud = null;
            yield return Hud(seats[0], h => hud = h);
            hud.SetScoreboard(seats, p => p.Team == 0 ? 2 : 1, 3, true);
            yield return TestScenes.WaitUntil(() => hud.ScoreboardReady && hud.CentreScore == "2 : 1", Slow, "a bar to score");
            var board = (RectTransform)hud.transform.Find("Safe HUD/Scoreboard");
            Assert.IsFalse(hud.ScoreboardShown);
            Assert.IsFalse(board.gameObject.activeSelf);
            hud.ToggleScoreboard();
            Assert.IsTrue(hud.ScoreboardShown, "A tap shows the scoreboard.");
            Assert.AreEqual(2, hud.ScoreboardBlocks);
            CollectionAssert.AreEqual(new[] { "P1 0 0 0 0 2", "P3 0 0 0 0 2", "P2 0 0 0 0 1", "P4 0 0 0 0 1" }, hud.ScoreboardLines, "Nothing recorded reads as zeros.");
            Assert.AreEqual("2", board.Find("Block 1/Total").GetComponent<TMP_Text>().text);
            Assert.AreEqual("1", board.Find("Block 2/Total").GetComponent<TMP_Text>().text);
            Assert.AreEqual("TEAM 1", board.Find("Block 1/Side").GetComponent<TMP_Text>().text);
            AssertSameColour(PlayerHud.Vivid(seats[0].Color), board.Find("Block 1/Total").GetComponent<TMP_Text>().color, "The total wears the side's colour.");
            AssertSameColour(PlayerHud.Vivid(seats[1].Color), board.Find("Block 2/Total").GetComponent<TMP_Text>().color, "The total wears the side's colour.");
            CollectionAssert.AreEqual(new[] { true, true, false, false },
                Enumerable.Range(1, 4).Select(i => board.Find($"Row {i}/Band/Face").GetComponent<Image>().enabled), "Your own side is highlighted.");
            CollectionAssert.AreEqual(ScoreLine.Columns, ScoreLine.Columns.Select(c => board.Find("Column " + c).GetComponent<TMP_Text>().text));
            foreach (var text in board.GetComponentsInChildren<TMP_Text>(true))
                Assert.GreaterOrEqual(text.enableAutoSizing ? text.fontSizeMin : text.fontSize, SmallestText, text.name);
            var area = new Vector3[4];
            var panel = new Vector3[4];
            ((RectTransform)hud.transform.Find("Safe HUD")).GetWorldCorners(area);
            board.GetWorldCorners(panel);
            Assert.GreaterOrEqual(panel[0].x, area[0].x - Slack, "The scoreboard fits the screen.");
            Assert.LessOrEqual(panel[2].x, area[2].x + Slack);
            Assert.GreaterOrEqual(panel[0].y, area[0].y - Slack);
            Assert.LessOrEqual(panel[2].y, area[2].y + Slack);

            var stats = MatchStats.Begin();
            foreach (var p in seats) stats.Enter(p.Index, p.Name, p.Team, false);
            stats.Knockout(1, 2);
            stats.Craft(0);
            stats.Break(3);
            yield return TestScenes.WaitUntil(() => hud.ScoreboardLines.Count == 4 && hud.ScoreboardLines[0] == "P3 1 0 0 0 2", Slow, "recorded numbers");
            CollectionAssert.AreEqual(new[] { "P3 1 0 0 0 2", "P1 0 0 1 0 2", "P4 0 0 0 1 1", "P2 0 1 0 0 1" }, hud.ScoreboardLines);

            hud.ToggleScoreboard();
            Assert.IsFalse(hud.ScoreboardShown, "A second tap hides it.");
            var button = Part(hud, "Scores").GetComponent<Button>();
            button.onClick.Invoke();
            Assert.IsTrue(hud.ScoreboardShown, "The small button by the bar toggles it.");
            hud.ShowResult(new HudResult { Round = 1, Heading = "Round over" }, () => { });
            yield return TestScenes.WaitUntil(() => !hud.ScoreboardShown, Slow, "the result card to take over");
            hud.HideResult();
            yield return null; yield return null;
            Assert.IsFalse(hud.ScoreboardShown, "It does not come back by itself.");
        }

        [UnityTest]
        public IEnumerator ManySidesShareOneRankedList()
        {
            var seats = Seats(0, 1, 2);
            GameHud hud = null;
            yield return Hud(seats[1], h => hud = h);
            hud.SetScoreboard(seats, p => p.Index * 2, 10, true, FreeForAllDirector.Unit);
            yield return TestScenes.WaitUntil(() => hud.ScoreboardReady && CardScore(hud, 3) == "4/10 KO", Slow, "a bar to score");
            hud.ToggleScoreboard();
            Assert.IsTrue(hud.ScoreboardShown);
            Assert.AreEqual(1, hud.ScoreboardBlocks);
            CollectionAssert.AreEqual(new[] { "P3 0 0 0 0 4", "P2 0 0 0 0 2", "P1 0 0 0 0 0" }, hud.ScoreboardLines, "Ranked by score.");
            var board = hud.transform.Find("Safe HUD/Scoreboard");
            Assert.IsFalse(board.Find("Block 2").gameObject.activeSelf);
            Assert.IsFalse(board.Find("Block 1/Total").GetComponent<TMP_Text>().enabled, "No team total for everyone-for-themselves.");
            CollectionAssert.AreEqual(new[] { "1", "2", "3" }, Enumerable.Range(1, 3).Select(i => board.Find($"Row {i}/Rank").GetComponent<TMP_Text>().text));
            CollectionAssert.AreEqual(new[] { false, true, false },
                Enumerable.Range(1, 3).Select(i => board.Find($"Row {i}/Band/Face").GetComponent<Image>().enabled), "Only your own row is highlighted.");
            Assert.IsFalse(board.Find("Row 4").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator HoldingTheKeyOrPadSelectShowsTheScoreboardAndStartStillPauses()
        {
            var route = InputSystem.settings.editorInputBehaviorInPlayMode;
            var focus = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keys = InputSystem.AddDevice<Keyboard>();
            var pad = InputSystem.AddDevice<Gamepad>();
            try
            {
                Ground();
                var you = Spawn(0, 0, new GamepadBinding(pad));
                var foe = Spawn(1, 1);
                GameHud hud = null;
                yield return Hud(you, h => hud = h);
                made.Add(new GameObject("Back to hub").AddComponent<BackToHub>().gameObject);
                hud.StateController.FadeDuration = 0f;
                hud.SetScoreboard(new[] { you, foe }, _ => 0, 3, true);
                yield return TestScenes.WaitUntil(() => hud.ScoreboardReady, Slow, "a bar to score");
                Assert.AreEqual(GameHud.PadScoreboardKey, GameHud.ScoreboardKeyLabel(you.Binding));
                Assert.AreEqual(GameHud.ScoreboardKey, GameHud.ScoreboardKeyLabel(foe.Binding));
                yield return TestScenes.WaitUntil(() => Part(hud, "Scores/Key cap/Key").GetComponent<TMP_Text>().text == GameHud.PadScoreboardKey, Slow, "the button's key cap");

                InputSystem.QueueStateEvent(keys, new KeyboardState(Key.Backquote));
                yield return TestScenes.WaitUntil(() => hud.ScoreboardShown, Slow, "the key to show the scoreboard");
                InputSystem.QueueStateEvent(keys, new KeyboardState());
                yield return TestScenes.WaitUntil(() => !hud.ScoreboardShown, Slow, "letting go to hide it");

                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Select));
                yield return TestScenes.WaitUntil(() => hud.ScoreboardShown, Slow, "Select to show the scoreboard");
                yield return null; yield return null;
                Assert.IsFalse(hud.Paused, "Select shows the scores in a match; it does not pause.");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return TestScenes.WaitUntil(() => !hud.ScoreboardShown, Slow, "letting go to hide it");
                Assert.IsFalse(hud.Paused);

                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Start));
                yield return TestScenes.WaitUntil(() => hud.Paused, Slow, "Start to pause");
                Assert.IsFalse(hud.ScoreboardShown, "The pause menu never sits under a scoreboard.");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null; yield return null;
            }
            finally
            {
                Time.timeScale = 1f;
                if (keys.added) InputSystem.RemoveDevice(keys);
                if (pad.added) InputSystem.RemoveDevice(pad);
                InputSystem.settings.editorInputBehaviorInPlayMode = route;
                InputSystem.settings.backgroundBehavior = focus;
            }
        }

        [UnityTest]
        public IEnumerator WarmUpSaysSpellingIsFree()
        {
            Ground();
            var you = Spawn(0, 0);
            GameHud hud = null;
            yield return Hud(you, h => hud = h);
            you.Inventory.Set("");
            var tray = hud.transform.Find("Safe HUD/Letter bag");
            var hint = tray.Find("Spell hint").GetComponent<TMP_Text>();
            LetterInventory.FreeSpelling = true;
            string free = GameHud.FreeSpellHint(GameHud.SpellKeyLabel(you.Binding));
            StringAssert.StartsWith(GameHud.FreeSpelling, free);
            yield return TestScenes.WaitUntil(() => hint.gameObject.activeInHierarchy && hint.text == free, Slow, "the free spelling hint");
            hud.ToggleBag();
            yield return TestScenes.WaitUntil(() => hud.BagOpen, Slow, "the bag");
            var book = hud.transform.Find("Safe HUD/Bag panel/Recipe book");
            var line = book.Find("Book line").GetComponent<TMP_Text>();
            yield return TestScenes.WaitUntil(() => line.text == GameHud.FreeBookLine, Slow, "the recipe book to say spelling is free");
            Assert.IsTrue(book.Find("Legend").gameObject.activeSelf, "The star key keeps its own line under the free line.");
            var tile = book.Find("View/Cards/BAT/Tile 1").GetComponent<Image>();
            yield return TestScenes.WaitUntil(() => tile.color.a > 0f, Slow, "every recipe tile to light up");
            Assert.IsFalse(book.Find("View/Cards/BAT/Tile 1/Rim").GetComponent<Image>().enabled);

            LetterInventory.FreeSpelling = false;
            yield return TestScenes.WaitUntil(() => line.text == "" && tile.color.a == 0f, Slow, "the real bag again");
            Assert.IsTrue(book.Find("Legend").gameObject.activeSelf);
            hud.ToggleBag();
            yield return TestScenes.WaitUntil(() => !hud.BagOpen, Slow, "the bag to close");
            yield return TestScenes.WaitUntil(() => !hint.gameObject.activeInHierarchy || hint.text != free, Slow, "the real hint again");
        }

        static IEnumerator Load(string mode, string map)
        {
            Session.SelectMap(map);
            Match.ModeOverride = mode;
            Session.Remember(new ScriptedBinding());
            yield return TestScenes.Load(Session.DibsScene);
            yield return TestScenes.ExploreHouse();
            SceneHud.Follow(Joins.Players[0]);
        }

        static IEnumerator Play(HouseRoundDirector director)
        {
            director.CountdownSeconds = .05f;
            director.StartMatch();
            yield return TestScenes.WaitUntil(() => director.Current == HouseRoundDirector.Stage.Playing, Slow, "the match to start");
            foreach (var p in Joins.Players)
            {
                if (p.TryGetComponent<BotController>(out var bot)) bot.enabled = false;
                if (p.Binding is BotBinding binding) binding.Commands = default;
                var rules = Match.Rules.Clone(); rules.SpawnProtectionSeconds = 0f;
                p.Health.UseRules(rules);
            }
        }

        [UnityTest]
        public IEnumerator TeamBattleShowsLivesBetweenTheTeamsAndTheClockMovesToTheCentre()
        {
            yield return Load(HouseModes.War, "pinwheel");
            var director = Object.FindAnyObjectByType<WarDirector>();
            Assert.IsNotNull(director);
            yield return Play(director);
            var hud = SceneHud;
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.Score && hud.CentreScore == ScoreLine.Text(director.StartingLives, director.StartingLives), Slow, "lives either side of the clock");
            Assert.AreEqual(WarDirector.LivesCaption, hud.CentreCaption);
            AssertCentreBetween(hud, 4, 5, PlayerBar.SnugCard);
            Assert.LessOrEqual(Bar(hud).rect.width, 900f, "Eight snug cards and the centre stay within about 900 px.");
            var sidePill = hud.transform.Find("Safe HUD/Side column/Timer");
            var sideClock = sidePill.Find("Timer").GetComponent<TMP_Text>();
            yield return TestScenes.WaitUntil(() => hud.CentreClock.Length > 0 && hud.CentreClock == sideClock.text, Slow, "the clock in the centre");
            Assert.IsFalse(sidePill.gameObject.activeSelf, "One clock only: the side pill steps aside.");
            var attacker = Joins.Players.First(p => p.Team == 0);
            var victim = Joins.Players.First(p => p.Team == 1);
            victim.Health.ApplyDamage(Hits.Of(attacker, Vector3.forward, HitSource.Melee, 1000f, 1f));
            yield return TestScenes.WaitUntil(() => director.Score.Lives(1) == director.StartingLives - 1, Slow, "a knockout to cost a life");
            yield return TestScenes.WaitUntil(() => hud.CentreScore == ScoreLine.Text(director.StartingLives, director.StartingLives - 1), Slow, "the score line to follow");
            Assert.IsTrue(hud.HealthOnBar, "In a house match your health is on your seat card...");
            Assert.IsFalse(hud.transform.Find("Safe HUD/Vitals").gameObject.activeSelf, "...so the bottom-left block steps aside.");
            RectTransform CardOf(PlayerController p) => Enumerable.Range(1, PlayerBar.MostSeats).Select(i => Part(hud, $"Card {i}"))
                .First(card => card.Find("Name").GetComponent<TMP_Text>().text == SeatTags.Name(p.Name, p == hud.LocalPlayer, 1));
            var victimCard = CardOf(victim);
            var attackerCard = CardOf(attacker);
            yield return TestScenes.WaitUntil(() => victimCard.Find("Health track/Number").GetComponent<TMP_Text>().text == "", Slow, "the knocked-out card to drop its number");
            Assert.IsFalse(victimCard.Find("Stamina track").gameObject.activeSelf, "A knocked-out seat shows no stamina.");
            Assert.AreEqual(Mathf.CeilToInt(attacker.Health.Current).ToString(), attackerCard.Find("Health track/Number").GetComponent<TMP_Text>().text, "A standing seat shows its health in its bar.");
            Assert.AreEqual(PlayerBar.HealthWidth(PlayerBar.MostSeats), ((RectTransform)attackerCard.Find("Health track")).rect.width, Slack, "Eight seats get snug bars.");
            Assert.AreEqual(PlayerBar.SnugCard, attackerCard.rect.width, Slack, "A card is its portrait.");
            Assert.IsTrue(Part(hud, "Side 1").GetComponent<Image>().enabled && Part(hud, "Side 2").GetComponent<Image>().enabled, "Each team has one outline...");
            Assert.IsFalse(Part(hud, "Side 3").GetComponent<Image>().enabled, "...and only one.");
            Assert.IsTrue(Part(hud, "Centre/Outline").GetComponent<Image>().enabled, "The lives and clock block has its own outline.");
            hud.ToggleScoreboard();
            Assert.IsTrue(hud.ScoreboardShown);
            Assert.AreEqual(2, hud.ScoreboardBlocks);
            Assert.AreEqual(8, hud.ScoreboardLines.Count);
            var board = hud.transform.Find("Safe HUD/Scoreboard");
            Assert.AreEqual(director.StartingLives.ToString(), board.Find("Block 1/Total").GetComponent<TMP_Text>().text);
            Assert.AreEqual((director.StartingLives - 1).ToString(), board.Find("Block 2/Total").GetComponent<TMP_Text>().text);
            Assert.AreEqual(GameHud.YourSide, board.Find("Block 1/Side").GetComponent<TMP_Text>().text, "One human: their side reads as theirs.");
            hud.ToggleScoreboard();
        }

        [UnityTest]
        public IEnumerator SpellTheListPutsTheTargetPictureInTheCentre()
        {
            yield return Load(HouseModes.ShoppingList, "flat");
            var director = ShoppingListDirector.Active;
            Assert.IsNotNull(director);
            director.Seed = 2024;
            yield return Play(director);
            var hud = SceneHud;
            var race = director.Race;
            var you = Joins.Players[0];
            var rival = Joins.Players[1];
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.Target && hud.TargetWord == race.Items[0].Word && hud.CentreCaption == race.Items[0].Word, Slow, "your first item as the target");
            Assert.AreEqual(race.Items[0].Word, hud.CentreCaption);
            Assert.AreEqual("", hud.CentreScore, "Four seats keep progress on their cards.");
            Assert.IsNull(hud.LeadingSeat, "Nobody leads before the first item.");
            AssertCentreBetween(hud, 2, 3, PlayerBar.RoomyCard);
            var first = race.Items[0];
            Assert.IsTrue(race.Spelled(rival.Index, first.Word));
            Assert.IsTrue(race.Count(rival.Index, first.Word, first.Act, 1.0));
            yield return TestScenes.WaitUntil(() => hud.LeadingSeat == rival, Slow, "the rival to take the lead");
            Assert.AreEqual(race.Items[0].Word, hud.TargetWord, "Your target is still your own next item.");
            int rivalCard = Enumerable.Range(1, 4).First(i => Name(hud, i) == rival.Name);
            Assert.IsTrue(Leads(hud, rivalCard), "The leader's card is marked.");
            Assert.AreEqual(1, Enumerable.Range(1, 4).Count(i => Leads(hud, i)));
            Assert.IsTrue(race.Spelled(you.Index, first.Word));
            Assert.IsTrue(race.Count(you.Index, first.Word, first.Act, 2.0));
            yield return TestScenes.WaitUntil(() => hud.TargetWord == race.Items[1].Word, Slow, "your next item as the target");
            yield return TestScenes.WaitUntil(() => !hud.LeadingSeat, Slow, "a tie has no leader");
        }
    }
}
