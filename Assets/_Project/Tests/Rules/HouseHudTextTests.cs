using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class HouseHudTextTests
    {
        static SeatResult Seat(string name, int wins, bool won = false, bool you = false, bool human = false, bool wrecked = false, int health = 100, bool downed = false) =>
            new SeatResult(name, wins, health, won, wrecked, downed, you, human);

        static RoundSummary Round(int round, bool final, bool timeUp, params SeatResult[] seats) => new RoundSummary(round, 3, final, timeUp, seats);

        [TestCase("Dibs", "Knock out everyone else to win the round")]
        [TestCase("Duos", "Revive your partner · last team standing")]
        [TestCase("MovingOut", "Bring every keepsake to the van")]
        [TestCase("MovingDay", "Set each checklist piece in its room")]
        [TestCase("Escape", "Open the doors, find the keys, get out")]
        [TestCase("Tutorial", "No pressure. Just play.")]
        [TestCase("Hub", "")]
        public void EveryHouseModeStatesItsGoalInOneLine(string mode, string line) => Assert.AreEqual(line, HouseGoals.Line(mode));

        [Test]
        public void RoundModesNameTheRoundTheTargetAndTheBanner()
        {
            Assert.IsTrue(HouseGoals.Rounds("Dibs"));
            Assert.IsTrue(HouseGoals.Rounds("Duos"));
            Assert.IsFalse(HouseGoals.Rounds("Escape"));
            Assert.AreEqual("ROUND 2 · FIRST TO 3", HouseGoals.RoundTag(2, 3));
            Assert.AreEqual("Round 1 · Last One Standing", HouseGoals.BannerTitle(1, HouseModes.Name(HouseModes.Dibs)));
            Assert.AreEqual("Knock out everyone else to win the round · First to 3", HouseGoals.BannerLine("Dibs", 3));
            StringAssert.Contains("First to three", HouseGoals.Blurb("Dibs", 3));
            StringAssert.Contains("First to three", HouseGoals.Blurb("Duos", 3));
            StringAssert.Contains("keepsake", HouseGoals.Blurb("MovingOut", 3));
        }

        [Test]
        public void ASoloWinIsYoursAndCountsWhatIsLeft()
        {
            var s = Round(1, false, false, Seat("P1", 1, won: true, you: true, human: true, health: 64), Seat("AI 2", 0, wrecked: true), Seat("AI 3", 0, wrecked: true));
            Assert.AreEqual("ROUND 1 · YOU WIN", HouseResultText.Eyebrow(s));
            Assert.AreEqual("You are the last one standing!", HouseResultText.Heading(s));
            Assert.AreEqual("2 more round wins to take the house.", HouseResultText.Flavour(s));
            Assert.AreEqual("You", HouseResultText.Winner(s));
            CollectionAssert.AreEqual(new[] { "<b>You · 1/3 wins · 64 HP</b>", "AI 2 · 0/3 wins · wrecked", "AI 3 · 0/3 wins · wrecked" }, HouseResultText.Lines(s));
        }

        [Test]
        public void ABotWinIsNamedAsTheLastOneStanding()
        {
            var s = Round(2, false, false, Seat("P1", 1, you: true, human: true, wrecked: true), Seat("AI 3", 1, won: true, health: 20));
            Assert.AreEqual("ROUND 2 · AI 3 WINS", HouseResultText.Eyebrow(s));
            Assert.AreEqual("AI 3 is the last one standing!", HouseResultText.Heading(s));
            Assert.AreEqual("2 more round wins to take the house.", HouseResultText.Flavour(s));
        }

        [Test]
        public void OnACouchEverySeatIsNamedNeverYou()
        {
            var s = Round(3, false, false, Seat("P1", 2, you: true, human: true, wrecked: true), Seat("P2", 1, won: true, human: true, health: 41), Seat("AI 3", 0, downed: true));
            Assert.AreEqual("ROUND 3 · P2 WINS", HouseResultText.Eyebrow(s));
            Assert.AreEqual("P2 is the last one standing!", HouseResultText.Heading(s));
            Assert.AreEqual("First to 3 takes the house.", HouseResultText.Flavour(s));
            CollectionAssert.AreEqual(new[] { "P1 · 2/3 wins · wrecked", "<b>P2 · 1/3 wins · 41 HP</b>", "AI 3 · 0/3 wins · down" }, HouseResultText.Lines(s));
        }

        [Test]
        public void TeamsWinTogether()
        {
            var mine = Round(1, false, false, Seat("P1", 1, won: true, you: true, human: true), Seat("AI 2", 0), Seat("AI 3", 1, won: true), Seat("AI 4", 0));
            Assert.AreEqual("ROUND 1 · YOUR TEAM WINS", HouseResultText.Eyebrow(mine));
            Assert.AreEqual("Your team wins!", HouseResultText.Heading(mine));
            var couch = Round(1, false, false, Seat("P1", 1, won: true, you: true, human: true), Seat("P2", 0, human: true), Seat("AI 3", 1, won: true), Seat("AI 4", 0));
            Assert.AreEqual("ROUND 1 · P1 + AI 3 WIN", HouseResultText.Eyebrow(couch));
            Assert.AreEqual("P1 + AI 3 win!", HouseResultText.Heading(couch));
            var final = Round(5, true, false, Seat("P1", 3, won: true, you: true, human: true), Seat("P2", 1, human: true), Seat("AI 3", 3, won: true), Seat("AI 4", 1));
            Assert.AreEqual("MATCH OVER · P1 + AI 3 WIN", HouseResultText.Eyebrow(final));
            Assert.AreEqual("P1 + AI 3 take the house.", HouseResultText.Flavour(final));
        }

        [Test]
        public void TheLastRoundHandsOverTheHouse()
        {
            var yours = Round(4, true, false, Seat("P1", 3, won: true, you: true, human: true), Seat("AI 2", 1, wrecked: true));
            Assert.AreEqual("MATCH OVER · YOU WIN", HouseResultText.Eyebrow(yours));
            Assert.AreEqual("The house is yours.", HouseResultText.Flavour(yours));
            var theirs = Round(4, true, false, Seat("P1", 1, you: true, human: true, wrecked: true), Seat("AI 2", 3, won: true));
            Assert.AreEqual("MATCH OVER · AI 2 WINS", HouseResultText.Eyebrow(theirs));
            Assert.AreEqual("AI 2 takes the house.", HouseResultText.Flavour(theirs));
        }

        [Test]
        public void DrawsSayTheyDontCount()
        {
            var timeUp = Round(2, false, true, Seat("P1", 0, you: true, human: true), Seat("AI 2", 0));
            Assert.AreEqual("ROUND 2 · DRAW", HouseResultText.Eyebrow(timeUp));
            Assert.AreEqual("Time’s up. A perfectly messy draw.", HouseResultText.Heading(timeUp));
            Assert.AreEqual("Draws don’t count. First to 3 takes the house.", HouseResultText.Flavour(timeUp));
            Assert.AreEqual("", HouseResultText.Winner(timeUp));
            var wiped = Round(2, false, false, Seat("P1", 0, you: true, human: true, wrecked: true), Seat("AI 2", 0, wrecked: true));
            Assert.AreEqual("Nobody left standing. A draw.", HouseResultText.Heading(wiped));
        }

        [Test]
        public void NoResultHeadingUsesAnOldModeName()
        {
            var rounds = new[]
            {
                Round(1, false, false, Seat("P1", 1, won: true, you: true, human: true), Seat("AI 2", 0, wrecked: true)),
                Round(1, false, false, Seat("P1", 0, you: true, human: true, wrecked: true), Seat("AI 2", 1, won: true)),
                Round(3, true, false, Seat("P1", 3, won: true, human: true), Seat("P2", 1, human: true, wrecked: true)),
                Round(2, false, true, Seat("P1", 0, you: true, human: true), Seat("AI 2", 0)),
            };
            foreach (var round in rounds)
                foreach (string text in new[] { HouseResultText.Eyebrow(round), HouseResultText.Heading(round), HouseResultText.Flavour(round) })
                    StringAssert.DoesNotContain("dibs", text.ToLowerInvariant());
        }

        [Test]
        public void TheCountdownNamesTheModeByItsPlayerNameAndTheHouse()
        {
            Assert.AreEqual("FURNISH THE HOUSE · CITY FLAT", HouseCountdown.Line(HouseModes.MovingDay, "City Flat"));
            Assert.AreEqual("ESCAPE THE HOUSE · PINWHEEL HOUSE", HouseCountdown.Line(HouseModes.Escape, "Pinwheel House"));
            Assert.AreEqual("PACK THE VAN · TERRACE HOUSE", HouseCountdown.Line(HouseModes.MovingOut, "Terrace House"));
            Assert.AreEqual("FURNISH THE HOUSE", HouseCountdown.Line(HouseModes.MovingDay, ""));
            Assert.AreEqual("FURNISH THE HOUSE", HouseCountdown.Line(HouseModes.MovingDay, null));
            foreach (var mode in HouseModes.All) StringAssert.DoesNotContain("LEVEL", HouseCountdown.Line(mode.Id, "City Flat"));
        }

        [Test]
        public void TheFurnishChecklistNamesTheHouseWithoutALevelNumber()
        {
            Assert.AreEqual("<b>CHECKLIST</b>\n<size=75%>City Flat</size>\n", FurnishText.ChecklistHeading("City Flat"));
            Assert.AreEqual("<b>CHECKLIST</b>\n", FurnishText.ChecklistHeading(""));
            StringAssert.DoesNotContain("Level", FurnishText.ChecklistHeading("City Flat"));
        }

        [Test]
        public void TheEscapeGuideCountsKeysAndNeverNamesTheVan()
        {
            Assert.AreEqual("KEYS 0/2 · the next one is in Kitchen", EscapeText.Guide(false, 0, 2, null, "Kitchen"));
            Assert.AreEqual("KEYS 1/2 · spell L A M P, then on to Study", EscapeText.Guide(false, 1, 2, "L A M P", "Study"));
            Assert.AreEqual("KEY · it is hidden in Kitchen", EscapeText.Guide(false, 0, 1, "", "Kitchen"));
            Assert.AreEqual("KEY · spell B A T, then on to Hall", EscapeText.Guide(false, 0, 1, "B A T", "Hall"));
            Assert.AreEqual("EXIT · get everybody to the exit", EscapeText.Guide(false, 2, 2, null, "the exit"));
            Assert.AreEqual("EXIT · spell B E D to get through, then on to Hall", EscapeText.Guide(false, 2, 2, "B E D", "Hall"));
            Assert.AreEqual("EXIT OPEN · everybody out!", EscapeText.Guide(true, 2, 2, "B E D", "Hall"));
            Assert.AreEqual("KEY", EscapeText.KeyLead(0, 1));
            Assert.AreEqual("KEYS 1/3", EscapeText.KeyLead(1, 3));
            for (int needed = 1; needed <= 3; needed++)
                for (int found = 0; found <= needed; found++)
                    foreach (string door in new[] { null, "B A T" })
                        foreach (bool open in new[] { false, true })
                            StringAssert.DoesNotContain("VAN", EscapeText.Guide(open, found, needed, door, "Kitchen"));
        }

        [Test]
        public void EscapeResultsSayWhatHappenedWithoutBlamingTheMovers()
        {
            Assert.AreEqual("Everybody out with 42s to spare!", EscapeText.Result(true, false, 42));
            Assert.AreEqual("Everybody out with 0s to spare!", EscapeText.Result(true, false, -3));
            Assert.AreEqual("The crew went down inside.", EscapeText.Result(false, true, 120));
            Assert.AreEqual("Time ran out before everybody got out.", EscapeText.Result(false, false, 0));
            foreach (bool won in new[] { false, true })
                foreach (bool wiped in new[] { false, true })
                    StringAssert.DoesNotContain("mover", EscapeText.Result(won, wiped, 10).ToLowerInvariant());
        }

        [Test]
        public void ShakeLevelsScaleAndStepWithinOffToFull()
        {
            Assert.AreEqual(0f, ShakeLevels.Scale(ShakeLevel.Off));
            Assert.AreEqual(.5f, ShakeLevels.Scale(ShakeLevel.Low));
            Assert.AreEqual(1f, ShakeLevels.Scale(ShakeLevel.Full));
            Assert.AreEqual("LOW", ShakeLevels.Label(ShakeLevel.Low));
            Assert.AreEqual(ShakeLevel.Off, ShakeLevels.Step(ShakeLevel.Low, -1));
            Assert.AreEqual(ShakeLevel.Off, ShakeLevels.Step(ShakeLevel.Off, -1));
            Assert.AreEqual(ShakeLevel.Full, ShakeLevels.Step(ShakeLevel.Full, 1));
            Assert.AreEqual(ShakeLevel.Full, ShakeLevels.Parse(7), "A broken saved value falls back to full shake.");
        }

        [Test]
        public void StarsRiseWithTheTier()
        {
            Assert.AreEqual(0, RecipeStars.Count(RecipeTier.Common));
            Assert.AreEqual(1, RecipeStars.Count(RecipeTier.Uncommon));
            Assert.AreEqual(2, RecipeStars.Count(RecipeTier.Rare));
            Assert.AreEqual(3, RecipeStars.Count(RecipeTier.Epic));
        }

        [Test]
        public void HumansWearATagOnlyWhenThereIsMoreThanOne()
        {
            Assert.IsFalse(SeatTags.ShowsTag(true, 1, false), "Solo: ring only.");
            Assert.IsTrue(SeatTags.ShowsTag(true, 2, false), "Couch: every human, the followed one too.");
            Assert.IsFalse(SeatTags.ShowsTag(true, 2, true), "The shoulder camera's player keeps the view clear.");
            Assert.IsTrue(SeatTags.ShowsTag(false, 1, false), "Bots keep their AI tag.");
            Assert.IsTrue(SeatTags.ShowsRing(false), "Every seat wears a ring, bots included.");
            Assert.IsFalse(SeatTags.ShowsRing(true), "A wrecked seat's ring goes.");
            Assert.IsTrue(SeatTags.MarksYou(true, false), "You get the white outer ring.");
            Assert.IsFalse(SeatTags.MarksYou(true, true));
            Assert.IsFalse(SeatTags.MarksYou(false, false));
            Assert.AreEqual("You", SeatTags.Name("P1", true, 1));
            Assert.AreEqual("P1", SeatTags.Name("P1", true, 2));
            Assert.AreEqual("AI 3", SeatTags.Name("AI 3", false, 1));
        }

        static BattleSeat Fighter(string name, int knockouts, int team, bool won = false, bool you = false, bool human = false) =>
            new BattleSeat(name, knockouts, team, won, you, human);

        [Test]
        public void NewModesStateTheirGoalAndBannerFromTheirRules()
        {
            Assert.AreEqual("Most knockouts wins", HouseGoals.Line("Deathmatch"));
            Assert.AreEqual("Most knockouts wins · First to 10", HouseGoals.Banner("Deathmatch", new GameRules { KnockoutsToWin = 10 }));
            Assert.AreEqual("Knock out the other team’s lives · 12 lives each", HouseGoals.Banner("War", new GameRules { TeamLives = 12 }));
            Assert.AreEqual("Knock out everyone else to win the round · First to 3", HouseGoals.Banner("Dibs", new GameRules { RoundsToWin = 3 }));
            Assert.AreEqual("Bring every keepsake to the van", HouseGoals.Banner("MovingOut", new GameRules()));
            var riddles = new GameRules();
            riddles.SetSetting(HouseGoals.PointsToWin, 5);
            Assert.AreEqual("Solve the riddle, spell the answer · First to 5", HouseGoals.Banner("RiddleRush", riddles));
            Assert.AreEqual("Spell everything on your list first · First to 3", HouseGoals.Banner("ShoppingList", new GameRules { RoundsToWin = 3 }));
            StringAssert.Contains("First to three", HouseGoals.Blurb("Dibs", new GameRules { RoundsToWin = 3 }));
            StringAssert.Contains("First to 10 knockouts", HouseGoals.Blurb("Deathmatch", new GameRules { KnockoutsToWin = 10, RespawnSeconds = 3f }));
        }

        [Test]
        public void FreeForAllResultsNameTheWinnerAndListKnockouts()
        {
            var s = new BattleSummary(new[] { Fighter("P1", 4, 0, human: true), Fighter("AI 2", 10, 1, won: true), Fighter("AI 3", 4, 2) }, 10, false, false);
            Assert.AreEqual("AI 2", BattleResultText.Winner(s));
            Assert.AreEqual("MATCH OVER · AI 2 WINS", BattleResultText.Eyebrow(s));
            Assert.AreEqual("AI 2 wins with 10 knockouts!", BattleResultText.Heading(s));
            Assert.AreEqual("First to 10 knockouts.", BattleResultText.Flavour(s));
            Assert.AreEqual(new[] { "<b>AI 2 · 10 knockouts</b>", "P1 · 4 knockouts", "AI 3 · 4 knockouts" }, BattleResultText.Lines(s));
        }

        [Test]
        public void SoloFreeForAllSaysYouAndExplainsTheClock()
        {
            var you = new BattleSummary(new[] { Fighter("P1", 1, 0, won: true, you: true, human: true), Fighter("AI 2", 0, 1) }, 10, true, true);
            Assert.AreEqual("You", BattleResultText.Winner(you));
            Assert.AreEqual("MATCH OVER · YOU WIN", BattleResultText.Eyebrow(you));
            Assert.AreEqual("You win in sudden death!", BattleResultText.Heading(you));
            Assert.AreEqual("Tied when time ran out. The next knockout decided it.", BattleResultText.Flavour(you));
            Assert.AreEqual("<b>You · 1 knockout</b>", BattleResultText.Lines(you)[0]);
            var bell = new BattleSummary(new[] { Fighter("P1", 3, 0, human: true), Fighter("P2", 5, 1, won: true, human: true) }, 10, true, false);
            Assert.AreEqual("Most knockouts when time ran out.", BattleResultText.Flavour(bell));
            Assert.AreEqual("P2 wins with 5 knockouts!", BattleResultText.Heading(bell));
        }

        [Test]
        public void WarResultsNameTheTeamItsLivesAndEveryKnockout()
        {
            var seats = new[]
            {
                Fighter("P1", 3, 0, won: true, you: true, human: true), Fighter("AI 3", 1, 0, won: true),
                Fighter("AI 2", 2, 1), Fighter("AI 4", 0, 1),
            };
            var s = new BattleSummary(seats, 0, false, false, new[] { 4, 0 }, 0, 1);
            Assert.AreEqual("Your team", BattleResultText.Winner(s));
            Assert.AreEqual("MATCH OVER · YOUR TEAM WINS", BattleResultText.Eyebrow(s));
            Assert.AreEqual("Your team wins!", BattleResultText.Heading(s));
            Assert.AreEqual("Team 2 ran out of lives.", BattleResultText.Flavour(s));
            Assert.AreEqual(new[] { "<b>Your team · 4 lives left</b>", "You 3 KO · AI 3 1 KO", "Team 2 · 0 lives left", "AI 2 2 KO · AI 4 0 KO" },
                BattleResultText.Lines(s));
            var couch = new BattleSummary(seats.Select(x => new BattleSeat(x.Name, x.Knockouts, x.Team, x.Won, false, true)).ToArray(), 0, true, false, new[] { 1, 1 }, 0, 1);
            Assert.AreEqual("Team 1 wins!", BattleResultText.Heading(couch));
            Assert.AreEqual("More lives left when time ran out.", BattleResultText.Flavour(couch));
            Assert.AreEqual("1 life left", BattleResultText.Lives(1));
        }

        [Test]
        public void ABattleWithNoWinnerIsADraw()
        {
            var war = new BattleSummary(new[] { Fighter("P1", 0, 0, human: true), Fighter("AI 2", 0, 1) }, 0, false, false, new[] { 0, 0 });
            Assert.IsTrue(war.Draw);
            Assert.AreEqual("MATCH OVER · DRAW", BattleResultText.Eyebrow(war));
            Assert.AreEqual("Both teams fell together. A draw.", BattleResultText.Heading(war));
            Assert.AreEqual("", BattleResultText.Winner(war));
        }
    }
}
