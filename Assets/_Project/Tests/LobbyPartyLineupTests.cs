using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public sealed class LobbyPartyLineupTests
    {
        const float Ready = 3f, Pixel = .5f, Shade = .01f, SmallestText = 15f;
        const string PlayerName = "Robin";
        const int MatchesPlayed = 12;
        static readonly string[] Keys =
            { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey, "wv.outfit.1", "wv.outfit.2", "wv.outfit.3" };
        string[] saved;
        Func<IWordboundLobbyParty> previousFactory;
        StubParty stub;
        bool registered;

        sealed class StubParty : IWordboundLobbyParty
        {
            public bool Active { get; set; }
            public bool Leader { get; set; }
            public bool Searching { get; set; }
            public bool Found { get; set; }
            public string Heading { get; set; } = "";
            public string Status { get; set; } = "";
            public string Problem { get; set; }
            public string SearchTitle { get; set; } = "";
            public IReadOnlyList<string> Members { get; set; } = Array.Empty<string>();
            public bool Copied { get; set; }
            public bool CanCopy { get; set; }
            public bool CanLeave { get; set; }
            public bool CanFind { get; set; }
            public bool CanCancel { get; set; }
            public bool CanRetry { get; set; }
            public void Invite() { }
            public void Join(string code) { }
            public void Copy() { }
            public void Leave() { }
            public void FindMatch() { }
            public void Cancel() { }
            public void Retry() { }
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            yield return TestScenes.Reset();
            foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetString(MatchTally.CareerKey, new Career { Name = PlayerName, Matches = MatchesPlayed, Wins = 5 }.Serialize());
            PlayerPrefs.SetString(LobbyMenu.TurnHintKey, "seen");
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning,
                Ready, "lobby ready");
            yield return Settle();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (stub != null) WordboundLobbyParty.Closed(stub);
            if (registered) WordboundLobbyParty.Register(previousFactory);
            WordboundLobbyParty.TakeMessage();
            stub = null; registered = false;
            yield return TestScenes.Reset();
            for (int i = 0; i < Keys.Length; i++)
                if (saved[i] == null) PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetString(Keys[i], saved[i]);
            PlayerPrefs.Save();
        }

        static LobbyMenu Menu => LobbyMenu.Instance;

        static LobbyPartyLineup Lineup => Menu ? Menu.GetComponentInChildren<LobbyPartyLineup>(true) : null;

        static bool Shown(int place)
        {
            var lineup = Lineup;
            var plate = lineup && lineup.Showing ? lineup.PlateAt(place) : null;
            return plate && plate.gameObject.activeInHierarchy;
        }

        static IEnumerator Settle()
        {
            yield return TestScenes.WaitUntil(() => Shown(0), Ready, "your plate on the stage");
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
        }

        static Rect OnScreen(Component part)
        {
            var corners = new Vector3[4];
            ((RectTransform)part.transform).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        static RectTransform Dock => Menu.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Next match");

        static TMP_Text Label(RectTransform plate, string name) =>
            plate.GetComponentsInChildren<TMP_Text>().SingleOrDefault(text => text.name == name);

        static Color Seat(int place) => GameAssets.I.PlayerColor(place);

        static void AssertColour(Color expected, Color actual, string what)
        {
            Assert.AreEqual(expected.r, actual.r, Shade, what);
            Assert.AreEqual(expected.g, actual.g, Shade, what);
            Assert.AreEqual(expected.b, actual.b, Shade, what);
        }

        static void AssertSeatLook(RectTransform plate, int place)
        {
            var seat = Seat(place);
            AssertColour(seat, plate.Find(LobbyPartyLineup.FrameName).GetComponent<Image>().color, "the frame wears the seat colour");
            AssertColour(seat, plate.Find(LobbyPartyLineup.ChipName).GetComponent<Image>().color, "the chip wears the seat colour");
            var paper = LobbyKit.Panel;
            var ink = PartyPlates.Ink(new Srgb(seat.r, seat.g, seat.b), new Srgb(paper.r, paper.g, paper.b));
            AssertColour(new Color(ink.R, ink.G, ink.B), Label(plate, LobbyPartyLineup.NameName).color, "the name is in the seat colour, made readable");
        }

        static void AssertTextFits(LobbyPartyLineup lineup)
        {
            foreach (var text in lineup.Layer.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextTruncated, $"\"{text.text}\" fits without an ellipsis");
                Assert.GreaterOrEqual(text.fontSize, SmallestText - Shade, $"\"{text.text}\" stays readable");
            }
        }

        static List<RectTransform> AssertTidy(LobbyPartyLineup lineup, int standing)
        {
            var screen = Rect.MinMaxRect(0, 0, Screen.width, Screen.height);
            var dock = OnScreen(Dock);
            var plates = new List<RectTransform>();
            for (int place = 0; place < standing; place++)
            {
                Assert.IsTrue(Shown(place), "player " + (place + 1) + " has a plate");
                plates.Add(lineup.PlateAt(place));
            }
            Assert.IsNull(lineup.PlateAt(standing), "nobody else has one");
            var shown = new List<RectTransform>(plates);
            if (lineup.Open && lineup.Open.gameObject.activeInHierarchy) shown.Add((RectTransform)lineup.Open.transform);
            for (int i = 0; i < shown.Count; i++)
            {
                var box = OnScreen(shown[i]);
                Assert.IsFalse(box.Overlaps(dock), shown[i].name + " stays off the PLAY card");
                Assert.GreaterOrEqual(box.xMin, screen.xMin - Pixel, shown[i].name + " is on screen");
                Assert.LessOrEqual(box.xMax, screen.xMax + Pixel, shown[i].name + " is on screen");
                Assert.GreaterOrEqual(box.yMin, screen.yMin - Pixel, shown[i].name + " is on screen");
                for (int j = i + 1; j < shown.Count; j++)
                    Assert.IsFalse(box.Overlaps(OnScreen(shown[j])), shown[i].name + " and " + shown[j].name + " stay apart");
            }
            return plates;
        }

        [UnityTest]
        public IEnumerator ASoloLobbyShowsYourPlateUnderYouAndOneAddPlayerPlate()
        {
            var menu = Menu; var lineup = Lineup;
            Assert.AreEqual(LobbyMenu.Home, menu.Current);
            Assert.AreEqual(LobbyPartyLineup.RootName, lineup.name);
            Assert.AreEqual("Safe area", lineup.transform.parent.name, "the line-up sits beside the pages, not inside the Home page");
            Assert.IsFalse(lineup.transform.IsChildOf(menu.Page<HomePage>().Root));
            CollectionAssert.AreEqual(new[] { PartyPlateKind.You, PartyPlateKind.Open }, lineup.Plates.Select(plate => plate.Kind).ToArray());
            var plates = AssertTidy(lineup, 1);
            var mine = plates[0];
            Assert.AreEqual(PlayerName, Label(mine, LobbyPartyLineup.NameName).text);
            Assert.IsNull(Label(mine, LobbyPartyLineup.TagName), "a couch seat has no leader or ready tag");
            AssertSeatLook(mine, 0);
            var foot = menu.Stage.Camera.WorldToScreenPoint(menu.Stage.Avatar.position);
            var box = OnScreen(mine);
            Assert.AreEqual(foot.x, box.center.x, 1f, "the plate is centred under your character");
            Assert.Less(box.yMax, foot.y, "and hangs below their feet");
            Assert.Greater(box.yMax, foot.y - Screen.height * .1f, "close to them");
            Assert.IsTrue(lineup.Open && lineup.Open.gameObject.activeInHierarchy, "one soft plate invites another player");
            Assert.AreEqual(LobbyPartyLineup.OpenName, lineup.Open.name);
            Assert.AreEqual(PartyPlates.OpenLabel, lineup.Open.GetComponentInChildren<TMP_Text>().text);
            Assert.Less(OnScreen(lineup.Open).center.x, box.xMin, "it waits on the side away from PLAY");
            AssertTextFits(lineup);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlatesNeverBlockTurningAndTheAddPlayerPlateOpensThePartyPanel()
        {
            var menu = Menu; var lineup = Lineup;
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = OnScreen(lineup.PlateAt(0)).center }, hits);
            Assert.IsNotEmpty(hits);
            Assert.AreEqual("Turn area", hits[0].gameObject.name, "dragging on a name plate still turns your character");
            hits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = OnScreen(lineup.Open).center }, hits);
            Assert.IsNotEmpty(hits);
            Assert.IsTrue(hits[0].gameObject.transform.IsChildOf(lineup.Open.transform), "the soft plate takes the pointer");
            var panel = menu.transform.Find(LobbyPartyLineup.PartyPanel);
            Assert.IsNotNull(panel);
            Assert.IsFalse(panel.gameObject.activeSelf);
            EventSystem.current.SetSelectedGameObject(lineup.Open.gameObject);
            ExecuteEvents.Execute(lineup.Open.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            yield return null;
            Assert.IsTrue(panel.gameObject.activeSelf, "the plate opens the Party panel");
            Assert.AreEqual(LobbyMenu.Home, menu.Current);
            lineup.Open.onClick.Invoke();
            yield return null;
            Assert.IsTrue(panel.gameObject.activeSelf, "pressing it again keeps the panel open");
            menu.ToggleParty();
            Assert.IsFalse(panel.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator CouchGuestsGetSeatColouredPlatesAndAFullPartyHasNoAddPlayerPlate()
        {
            var menu = Menu; var lineup = Lineup;
            var guests = new[] { new ScriptedBinding(), new ScriptedBinding(), new ScriptedBinding() };
            for (int count = 2; count <= LobbyMenu.PartyMax; count++)
            {
                menu.Join(guests[count - 2]);
                yield return Settle();
                Assert.AreEqual(count, menu.Stage.PresentedPartySize);
                Assert.AreEqual(count, PartyPlates.Standing(lineup.Plates));
                var plates = AssertTidy(lineup, count);
                for (int place = 0; place < count; place++)
                {
                    AssertSeatLook(plates[place], place);
                    Assert.AreEqual(place == 0 ? PlayerName : "Player " + (place + 1), Label(plates[place], LobbyPartyLineup.NameName).text);
                    Assert.IsTrue(menu.Stage.PartyFoot(place, out var foot), "player " + (place + 1) + " stands on the stage");
                    Assert.Less(OnScreen(plates[place]).yMax, menu.Stage.Camera.WorldToScreenPoint(foot).y, "each plate hangs below its character");
                }
                var order = Enumerable.Range(0, count).OrderBy(place => PartyPlates.Offset(place, count)).ToArray();
                for (int i = 1; i < count; i++)
                    Assert.Less(OnScreen(plates[order[i - 1]]).center.x, OnScreen(plates[order[i]]).center.x, "plates keep the order the characters stand in");
                AssertTextFits(lineup);
            }
            Assert.IsFalse(lineup.Plates.Any(plate => plate.Kind == PartyPlateKind.Open), "four players fill the stage");
            Assert.IsFalse(lineup.Open && lineup.Open.gameObject.activeInHierarchy, "so nothing offers a fifth place");
            Assert.AreEqual(LobbyHomePageView.PlayButton, EventSystem.current.currentSelectedGameObject.name, "PLAY keeps the controller");
            menu.Leave(guests[2]);
            yield return Settle();
            Assert.AreEqual(3, menu.Stage.PresentedPartySize);
            AssertTidy(lineup, 3);
            Assert.IsTrue(lineup.Plates.Any(plate => plate.Kind == PartyPlateKind.Open), "a free place is offered again");
        }

        [UnityTest]
        public IEnumerator PlatesHideWithThePartyOnOtherPagesAndWhenCharactersHide()
        {
            var menu = Menu; var lineup = Lineup;
            menu.Join(new ScriptedBinding());
            yield return Settle();
            Assert.IsTrue(Shown(1));
            foreach (string page in new[] { LobbyMenu.CareerPage, LobbyMenu.Play, LobbyMenu.Shop })
            {
                menu.Open(page);
                yield return TestScenes.WaitUntil(() => !lineup.Showing, Ready, "plates hidden on " + page);
                Assert.IsFalse(Shown(0)); Assert.IsFalse(Shown(1));
                Assert.AreEqual(1, menu.Stage.PresentedPartySize);
            }
            menu.Open(LobbyMenu.Home);
            yield return Settle();
            Assert.IsTrue(Shown(1), "the party and its plates return on Home");
            menu.Stage.SetPartyVisible(false);
            yield return TestScenes.WaitUntil(() => !lineup.Showing, Ready, "plates hidden with the party");
            menu.Stage.SetPartyVisible(true);
            yield return Settle();
            menu.Stage.SetCharactersVisible(false);
            yield return TestScenes.WaitUntil(() => !lineup.Showing, Ready, "plates hidden with the characters");
            menu.Stage.SetCharactersVisible(true);
            yield return Settle();
            Assert.AreEqual(2, menu.Stage.PresentedPartySize);
            AssertTidy(lineup, 2);
            Assert.AreEqual(1, menu.GetComponentsInChildren<LobbyPartyLineup>(true).Length, "refreshing Home keeps one line-up");
        }

        [UnityTest]
        public IEnumerator AnOnlinePartyStandsFriendsBesideYouWithLeaderAndReadyTags()
        {
            var menu = Menu; var lineup = Lineup;
            stub = new StubParty { Active = true, Members = new[] { "You · leader · ready", "Player 2 · not ready", "Player 3 · ready" } };
            previousFactory = WordboundLobbyParty.Register(() => stub);
            registered = true;
            Assert.AreSame(stub, WordboundLobbyParty.Open());
            yield return TestScenes.WaitUntil(() => PartyPlates.Friends(lineup.Plates) == 2 && Shown(2), Ready, "friends on the stage");
            yield return Settle();
            Assert.AreEqual(1, menu.PartySize, "the couch party is unchanged");
            Assert.AreEqual(3, menu.Stage.PresentedPartySize);
            Assert.AreEqual(2, menu.Stage.PartyFriends);
            Assert.IsTrue(menu.Stage.transform.Find("Lobby guest 2").gameObject.activeSelf);
            Assert.IsTrue(menu.Stage.transform.Find("Lobby guest 3").gameObject.activeSelf);
            var plates = AssertTidy(lineup, 3);
            Assert.AreEqual(PlayerName, Label(plates[0], LobbyPartyLineup.NameName).text);
            Assert.AreEqual("LEADER · READY", Label(plates[0], LobbyPartyLineup.TagName).text);
            Assert.AreEqual("Player 2", Label(plates[1], LobbyPartyLineup.NameName).text);
            Assert.IsNull(Label(plates[1], LobbyPartyLineup.TagName), "not ready shows no tag");
            Assert.AreEqual("Player 3", Label(plates[2], LobbyPartyLineup.NameName).text);
            Assert.AreEqual(PartyPlates.ReadyTag, Label(plates[2], LobbyPartyLineup.TagName).text);
            for (int place = 0; place < 3; place++) AssertSeatLook(plates[place], place);
            AssertTextFits(lineup);
            stub.Members = new[] { "You · leader · ready", "Player 2 · ready" };
            yield return TestScenes.WaitUntil(() => PartyPlates.Friends(lineup.Plates) == 1 && menu.Stage.PresentedPartySize == 2, Ready, "a friend leaving");
            yield return Settle();
            Assert.AreEqual(PartyPlates.ReadyTag, Label(AssertTidy(lineup, 2)[1], LobbyPartyLineup.TagName).text);
            stub.Active = false;
            yield return TestScenes.WaitUntil(() => PartyPlates.Friends(lineup.Plates) == 0 && menu.Stage.PresentedPartySize == 1, Ready, "the party ending");
            yield return Settle();
            Assert.IsNull(Label(AssertTidy(lineup, 1)[0], LobbyPartyLineup.TagName), "alone again, no tag");
            Assert.IsFalse(menu.Stage.transform.Find("Lobby guest 2").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator HomeShowsMatchesPlayedUnderPlayAndNothingForANewCareer()
        {
            var menu = Menu;
            var dock = Dock;
            var line = dock.GetComponentsInChildren<TMP_Text>().Single(text => text.name == LobbyHomePageView.CareerLine);
            Assert.AreEqual(PartyPlates.CareerLine(MatchesPlayed, PartyPlates.NotRecorded), line.text);
            Assert.AreEqual("12 matches played", line.text);
            line.ForceMeshUpdate();
            Assert.IsFalse(line.isTextTruncated);
            Assert.GreaterOrEqual(line.fontSize, SmallestText - Shade);
            var play = dock.GetComponentsInChildren<Button>().Single(button => button.name == LobbyHomePageView.PlayButton);
            Assert.LessOrEqual(OnScreen(line).yMax, OnScreen(play).yMin + Pixel, "the line sits under PLAY");
            Assert.IsTrue(dock.GetComponentsInChildren<Button>().All(button =>
                button.name == LobbyHomePageView.PlayButton || button.name == LobbyHomePageView.ChangeButton), "the line adds no control");
            Assert.AreEqual(300f, dock.rect.width, 1f, "the dock keeps its width");
            menu.Career.Matches = 0;
            menu.Open(LobbyMenu.Home);
            yield return Settle();
            Assert.IsFalse(Dock.GetComponentsInChildren<TMP_Text>().Any(text => text.name == LobbyHomePageView.CareerLine),
                "a career with no matches shows no line");
        }
    }
}
