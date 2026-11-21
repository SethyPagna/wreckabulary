using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundLobbyPartyTests
    {
        const string Player = "10000000-0000-4000-8000-000000000001", PartyId = "20000000-0000-4000-8000-000000000001";
        const string Seed = "0123456789abcdef", Code = "1234567890abcdef", Pool = "lobby-party";
        const string ThemePath = "Assets/_Project/Data/Wordbound/Authoring/Interface.asset";
        static readonly string[] Keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey, QuickPlayChoices.StorageKey, FriendRooms.StorageKey, Wreckabulary.Rules.LastPlayChoice.StorageKey };
        readonly List<string> requests = new List<string>();
        string[] saved;
        bool hadTutorial;
        int tutorial;
        Func<IWordboundLobbyParty> previousFactory;
        WordboundPartyCoordinator coordinator;
        WordboundPublicCourseCatalog catalogue;
        RSA rsa;
        OnlineSettings settings;
        Dictionary<string, object> assignment;
        string items, rules, channel;
        bool ready;
        int revision;
        float now = 500f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            hadTutorial = PlayerPrefs.HasKey(TutorialDirector.DoneKey); tutorial = PlayerPrefs.GetInt(TutorialDirector.DoneKey, 0);
            yield return TestScenes.Reset();
            foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetInt(TutorialDirector.DoneKey, 1);
            items = File.ReadAllText("Assets/_Project/Data/Config/items.json");
            rules = File.ReadAllText("Assets/_Project/Data/Config/rules.json");
            catalogue = WordboundPublicCourseCatalog.FromTestedContent(Pool, new[] { new WordboundClientContent(items, rules, "Freestyle", "Solo", Seed) });
            rsa = new RSACryptoServiceProvider(2048) { PersistKeyInCsp = false };
            var publicKey = rsa.ExportParameters(false);
            var verifier = new OnlineAdmissionVerifier("wreckabulary-wordbound", OnlineJson.Parse(OnlineJson.Encode(
                new Dictionary<string, object> { ["keys"] = new[] { new Dictionary<string, object> {
                    ["kty"] = "RSA", ["kid"] = "lobby-party", ["use"] = "sig", ["alg"] = "RS256",
                    ["n"] = OnlineJson.Base64Url(publicKey.Modulus), ["e"] = OnlineJson.Base64Url(publicKey.Exponent) } } })));
            coordinator = new WordboundPartyCoordinator(new OnlineCoordinatorClient(new MemoryHttp { Owner = this }), verifier, items, rules,
                Pool, new WordboundPublicCourseRegistry(catalogue.Json), () => 1000000);
            previousFactory = WordboundLobbyParty.Register(MakeHost);
            WordboundLobbyParty.TakeMessage();
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning,
                2f, "lobby ready");
            LobbyMenu.Instance.Open(LobbyMenu.Play);
            yield return null;
            Submit("Queue " + LobbyMenu.Matchmaking);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var current = WordboundLobbyParty.Current;
            if (current != null && current is not WordboundPartyHost) WordboundLobbyParty.Closed(current);
            foreach (var host in Object.FindObjectsByType<WordboundPartyHost>(FindObjectsSortMode.None)) Object.DestroyImmediate(host.gameObject);
            WordboundLobbyParty.Register(previousFactory);
            WordboundLobbyParty.TakeMessage();
            WordboundQuickPlayEntry.CancelReturn(); WordboundQuickPlayEntry.CancelRequest();
            yield return TestScenes.Reset();
            coordinator?.Dispose(); rsa?.Dispose();
            for (int i = 0; i < Keys.Length; i++)
                if (saved[i] == null) PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetString(Keys[i], saved[i]);
            if (hadTutorial) PlayerPrefs.SetInt(TutorialDirector.DoneKey, tutorial); else PlayerPrefs.DeleteKey(TutorialDirector.DoneKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator ThePartyStaysInTheLobbyThroughTheMatchSceneWithOneCoordinator()
        {
            Assert.AreEqual("Party · You", Text(PlayPage.PartyLabel));
            Submit(PlayPage.InviteFriendsButton);
            yield return TestScenes.WaitUntil(() => coordinator.Party != null && ready, 5f, "the party in the lobby");
            yield return null;
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name, "Invite friends stays in the lobby.");
            var host = WordboundPartyHost.Current;
            Assert.IsTrue(host && host.Active && !host.Adopted);
            Assert.AreEqual("Party · 1 of 4", Text(PlayPage.PartyHeading));
            CollectionAssert.AreEqual(new[] { "You" }, PlateNames(StripPlates), "the party shows as people");
            CollectionAssert.AreEqual(new[] { LobbyKit.Upper(Wreckabulary.Rules.PartyPlates.Tag(true, true)) }, PlateTags(StripPlates));
            Assert.IsTrue(Find(PlayPage.CopyPartyCode).interactable); Assert.IsTrue(Find(PlayPage.LeaveParty).interactable);
            Assert.AreEqual(1, Menus(), "One coordinator, owned by the lobby party.");

            assignment = new Dictionary<string, object> { ["state"] = "ADMITTING" };
            Submit(PlayPage.FindMatch);
            yield return null;
            Assert.AreEqual("QuickPlay", channel);
            Assert.AreEqual("Match found · starting in 5", Text("Matchmaking playlists"));
            Assert.AreEqual(LobbyKit.Upper(LobbyPartyStrip.CancelWord), Find(PlayPage.FindMatch).GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name, "The countdown runs in the lobby.");
            now += WordboundPartyMenu.StartCountdownSeconds;
            yield return TestScenes.WaitForActive(Session.WordboundScene, 10f);
            WordboundLauncher launcher = null;
            yield return TestScenes.WaitUntil(() => (launcher = Object.FindAnyObjectByType<WordboundLauncher>()) && launcher.HoldsLobbyParty,
                5f, "the match scene adopting the lobby party");
            Assert.AreSame(host, WordboundPartyHost.Current); Assert.IsTrue(host.Adopted);
            Assert.AreEqual(1, Menus(), "The match scene adopts the lobby's coordinator instead of making its own.");
            Assert.IsTrue(host.Menu.Visible);

            assignment = null;
            yield return TestScenes.WaitForActive(Session.HubScene, 10f);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning,
                3f, "the lobby again");
            yield return null;
            Assert.AreSame(host, WordboundPartyHost.Current, "The party is kept on the way back.");
            Assert.IsTrue(host.Active); Assert.IsFalse(host.Adopted); Assert.IsFalse(host.Menu.Visible);
            Assert.IsNotNull(coordinator.Party);
            Assert.AreEqual(1, Menus());
            Assert.AreEqual("Party · 1 of 4", Text(PlayPage.PartyHeading), "The lobby reopens on the Matchmaking tab with the party.");

            Submit(PlayPage.LeaveParty);
            yield return TestScenes.WaitUntil(() => WordboundLobbyParty.Current == null, 5f, "the party closing");
            yield return null; yield return null;
            Assert.IsTrue(requests.Last().EndsWith("/leave"), string.Join(", ", requests));
            Assert.AreEqual(0, Menus(), "Leaving disposes the lobby party's menu and coordinator.");
            Assert.AreEqual("Party · You", Text(PlayPage.PartyLabel));
            Assert.IsFalse(WordboundQuickPlayEntry.TakeReturn());
        }

        [UnityTest]
        public IEnumerator TheStripFooterAndSearchCardFollowTheLobbyParty()
        {
            var stub = new StubParty { Active = true, Leader = true, Heading = "Party · 3 of 4",
                Members = new[] { "You · leader · ready", "Player 2 · ready", "Player 3 · not ready" },
                Status = new QuickPlayChoices().PartyLine(3), CanCopy = true, CanLeave = true, CanFind = true };
            Assert.IsTrue(Find(PlayPage.TeamSizeSwitch + " Duo").interactable, "one player picks either size");
            WordboundLobbyParty.Register(() => stub);
            Assert.AreSame(stub, WordboundLobbyParty.Open());
            yield return null;
            Assert.AreEqual("Party · 3 of 4", Text(PlayPage.PartyHeading));
            CollectionAssert.AreEqual(new[] { "You", "Player 2", "Player 3" }, PlateNames(StripPlates), "one plate per person");
            CollectionAssert.AreEqual(new[] { LobbyKit.Upper(Wreckabulary.Rules.PartyPlates.Tag(true, true)), LobbyKit.Upper(Wreckabulary.Rules.PartyPlates.ReadyTag), "" },
                PlateTags(StripPlates), "leader and ready tags like the Home plates");
            AssertPlates(StripPlates, 3);
            Assert.IsFalse(Page.GetComponentsInChildren<TMP_Text>().Any(text => text.name == PlayPage.PartyMembers), "no grey sentence of names");
            string region = QuickPlayRegions.Name(QuickPlayRegions.Nearest(QuickPlayRegions.LocalOffsetMinutes()));
            Assert.AreEqual(Wreckabulary.Rules.PlayPageText.PartyLine(3, region), Text("Matchmaking detail"), "the footer never repeats a mode list that contradicts the cards");
            StringAssert.StartsWith("4v4 for a party of 3 · ", stub.Status);
            Assert.IsFalse(Page.GetComponentsInChildren<TMP_InputField>().Any(), "the code box steps aside while in a party");
            Assert.IsFalse(Find(PlayPage.TeamSizeSwitch + " Duo").interactable, "a party of 3 cannot play 2v2");
            Assert.IsTrue(Find(PlayPage.TeamSizeSwitch + " Squad").interactable);
            Assert.IsTrue(Find(PlayPage.TeamSizeSwitch + " Squad").GetComponentsInChildren<Image>().Single(image => image.name == "Underline").enabled, "the switch shows 4v4");
            Assert.AreEqual(PlayPage.PartyPlaysSquad(3), Text(PlayPage.TeamSizeNote));
            Assert.AreEqual("3 modes · 4v4", Text("Matchmaking playlists"), "the summary counts only what the party can play");
            Assert.AreEqual("Teams of 4 race to the delivery", Detail("relay"));
            Assert.AreEqual(Wreckabulary.Rules.PlayPageText.SkipLine(Wreckabulary.Rules.PlayPageText.SoloOnly), Detail("ffa"), "a card the party cannot play says why");
            Assert.AreEqual(LobbyKit.Hot, Find(PlayPage.PlaylistCard + "ffa").GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Map detail").color);
            Assert.Less(Find(PlayPage.PlaylistCard + "ffa").GetComponent<CanvasGroup>().alpha, 1f, "and is dimmed");
            Assert.IsFalse(Find(PlayPage.PlaylistCard + "relay").TryGetComponent(out CanvasGroup _), "a card the party can play stays lit");
            Assert.AreEqual(LobbyPlayModeLabels.Name("FreeForAll"), Find(PlayPage.PlaylistCard + "ffa").GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Map title").text);
            Assert.AreEqual("8", Find(PlayPage.PlaylistCard + "relay").GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.PlayersCount).text);
            StringAssert.Contains("size=Duo", PlayerPrefs.GetString(QuickPlayChoices.StorageKey, "size=Duo"), "the saved 2v2 is kept for later");
            Assert.IsEmpty(Page.GetComponentsInChildren<Button>().Where(button => button.name == PlayPage.InviteFriendsButton), "Invite steps aside for the party.");
            Submit(PlayPage.CopyPartyCode);
            Submit(PlayPage.FindMatch);
            Assert.AreEqual(1, stub.Copies); Assert.AreEqual(1, stub.Finds);
            stub.Searching = true; stub.CanCancel = true; stub.SearchTitle = "Searching 0:42";
            yield return null;
            Assert.AreEqual("Searching 0:42", Text("Matchmaking playlists"));
            Assert.AreEqual("Party · 3 of 4", Text("Matchmaking detail"));
            Assert.AreEqual(LobbyKit.Upper(LobbyPartyStrip.CancelWord), Find(PlayPage.FindMatch).GetComponentInChildren<TMP_Text>().text);
            Assert.IsFalse(Find(PlayPage.CustomMatch).interactable, "No custom match while the party searches.");
            Assert.IsFalse(Card.gameObject.activeInHierarchy, "The Matchmaking footer shows the search, so the header card stays hidden.");
            var band = Band;
            Assert.IsTrue(band.gameObject.activeInHierarchy, "the search covers the playlist cards");
            Assert.AreEqual("SEARCHING 0:42", band.GetComponentsInChildren<TMP_Text>().Single(text => text.name == LobbyMatchBand.TitleName).text);
            Assert.IsFalse(band.Found);
            CollectionAssert.AreEqual(new[] { "You", "Player 2", "Player 3" }, PlateNames(band.Plates), "the search shows who is coming");
            Assert.IsFalse(Find(PlayPage.PlaylistCard + "relay").IsInteractable(), "the covered cards cannot be ticked mid-search");
            Assert.IsTrue(Find(PlayPage.FindMatch).GetComponentsInChildren<Image>(true).Single(image => image.name == LobbyPartyStrip.QuietEdge).enabled, "Cancel is quiet");
            int cues = LobbyMatchMoment.Cues;
            stub.Found = true; stub.SearchTitle = "Match found · starting in 4";
            yield return TestScenes.WaitUntil(() => band.Found, 2f, "the match found stamp");
            Assert.IsTrue(band.transform.Find(LobbyMatchBand.StampName).gameObject.activeSelf, "the MATCH FOUND stamp shows");
            Assert.AreEqual(LobbyKit.Upper(LobbyMatchBand.StampWord), band.GetComponentsInChildren<TMP_Text>().Single(text => text.name == LobbyMatchBand.StampWordName).text);
            Assert.IsFalse(band.transform.Find(LobbyMatchBand.TitleName).gameObject.activeSelf, "the stamp replaces the searching line");
            Assert.AreEqual("4", Count(band));
            yield return TestScenes.WaitUntil(() => band.Landed, 2f, "the stamp landing");
            Assert.AreEqual(cues + 1, LobbyMatchMoment.Cues, "the stamp lands with one thump");
            Assert.AreEqual(GameCue.Craft, LobbyMatchMoment.Sound);
            stub.SearchTitle = "Match found · starting in 3";
            yield return TestScenes.WaitUntil(() => Count(band) == "3", 2f, "the count going down");
            Assert.AreEqual(cues + 1, LobbyMatchMoment.Cues, "one thump per match found");
            Assert.AreEqual("Match found · starting in 3", Text("Matchmaking playlists"));
            stub.Found = false; stub.SearchTitle = "Searching 0:42";
            yield return TestScenes.WaitUntil(() => !band.Found, 2f, "the search line again");
            Submit(PlayPage.FindMatch);
            Assert.AreEqual(1, stub.Cancels);
            LobbyMenu.Instance.Open(LobbyMenu.Home);
            yield return null; yield return null;
            Assert.IsTrue(Card.gameObject.activeInHierarchy, "Other pages show the search in the header.");
            Assert.AreEqual("SEARCHING 0:42", Card.GetComponentsInChildren<TMP_Text>().Single(text => text.name == LobbyPartySearch.TitleName).text);
            var cancel = Card.GetComponentsInChildren<Button>().Single(button => button.name == LobbyPartySearch.CancelName);
            Assert.IsTrue(cancel.interactable);
            cancel.onClick.Invoke();
            Assert.AreEqual(2, stub.Cancels);
            stub.Leader = false; stub.CanCancel = false;
            yield return null;
            Assert.IsEmpty(Card.GetComponentsInChildren<Button>().Where(button => button.name == LobbyPartySearch.CancelName), "Members cannot cancel.");
            WordboundLobbyParty.Closed(stub, "Your party has ended.");
            yield return null;
            Assert.IsFalse(Card.gameObject.activeInHierarchy);
            CollectionAssert.Contains(LobbyMenu.Instance.Messages, "Your party has ended.");
            LobbyMenu.Instance.Open(LobbyMenu.Play);
            yield return null; yield return null;
            Assert.AreEqual("Party · You", Text(PlayPage.PartyLabel));
            Assert.AreEqual(LobbyKit.Upper(LobbyPartyStrip.FindWord), Find(PlayPage.FindMatch).GetComponentInChildren<TMP_Text>().text);
        }

        static RectTransform Page => LobbyMenu.Instance.Page<PlayPage>().Root;

        static RectTransform Card => (RectTransform)Object.FindAnyObjectByType<LobbyPartySearch>().transform.Find(LobbyPartySearch.CardName);

        static LobbyMatchBand Band => Page.GetComponentsInChildren<LobbyMatchBand>(true).Single();

        static LobbyPartyChips StripPlates => Page.GetComponentsInChildren<LobbyPartyChips>().Single(row => row.name == PlayPage.PartyPlatesRow);

        static string Detail(string card) => Find(PlayPage.PlaylistCard + card).GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Map detail").text;

        static string Count(LobbyMatchBand band) => band.transform.Find(LobbyMatchBand.CountName).GetComponentInChildren<TMP_Text>().text;

        static RectTransform[] Plates(LobbyPartyChips row) => row.transform.Cast<Transform>().Select(child => (RectTransform)child)
            .Where(child => child.name.StartsWith(LobbyPartyChips.PlatePrefix)).ToArray();

        static string[] PlateNames(LobbyPartyChips row) => Plates(row)
            .Select(plate => plate.GetComponentsInChildren<TMP_Text>().Single(text => text.name == LobbyPartyChips.NameName).text).ToArray();

        static string[] PlateTags(LobbyPartyChips row) => Plates(row)
            .Select(plate => plate.GetComponentsInChildren<TMP_Text>().SingleOrDefault(text => text.name == LobbyPartyChips.TagName)?.text ?? "").ToArray();

        static void AssertPlates(LobbyPartyChips row, int count)
        {
            Canvas.ForceUpdateCanvases();
            var plates = Plates(row);
            Assert.AreEqual(count, plates.Length);
            CollectionAssert.AreEqual(Enumerable.Range(1, count).Select(place => LobbyPartyChips.PlatePrefix + place).ToArray(), plates.Select(plate => plate.name).ToArray());
            float right = float.NegativeInfinity;
            foreach (var plate in plates)
            {
                Assert.GreaterOrEqual(plate.rect.height, LobbyPartyChips.Height - .01f, plate.name);
                var corners = new Vector3[4];
                plate.GetWorldCorners(corners);
                Assert.GreaterOrEqual(corners[0].x, right - .5f, plate.name + " sits right of the plate before it");
                right = corners[2].x;
                foreach (var text in plate.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, plate.name + ": " + text.text);
                    Assert.GreaterOrEqual(text.fontSize, 15f - .01f, plate.name + ": " + text.text);
                }
            }
        }

        static Button Find(string name) => Page.GetComponentsInChildren<Button>().Single(button => button.name == name);

        static string Text(string name) => Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == name).text;

        static int Menus() => Object.FindObjectsByType<WordboundPartyMenu>(FindObjectsSortMode.None).Length;

        static void Submit(string name)
        {
            var button = Find(name);
            Assert.IsTrue(button.IsInteractable(), name);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }

        IWordboundLobbyParty MakeHost() => WordboundPartyHost.Create(menu =>
        {
            menu.Theme = AssetDatabase.LoadAssetAtPath<WordboundUiTheme>(ThemePath);
            Set(menu, "coordinator", coordinator); Set(menu, "publicCourses", catalogue);
            Set(menu, "itemSource", items); Set(menu, "ruleSource", rules);
            Set(menu, "loadQuickChoices", (Func<string>)(() => "v2;lists=freeplay;name=Ana"));
            Set(menu, "saveQuickChoices", (Action<string>)(_ => { }));
            Set(menu, "utcOffsetMinutes", (Func<int>)(() => 420));
            Set(menu, "quickPlayClock", (Func<long>)(() => 0L));
            Set(menu, "searchClock", (Func<float>)(() => now));
        });

        static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        OnlineResponse Respond(OnlineRequest request)
        {
            requests.Add(request.Method + " " + request.Path);
            if (request.Path == "/v1/guests") return Json(new Dictionary<string, object> {
                ["playerId"] = Player, ["accessToken"] = "synthetic.lobby.token",
                ["refreshToken"] = Player + "." + OnlineJson.Base64Url(new byte[32]), ["expiresIn"] = 900 });
            var body = string.IsNullOrEmpty(request.JsonBody) ? null : OnlineJson.Parse(request.JsonBody);
            if (request.Path == "/v1/parties" || request.Method == "PATCH") settings = OnlineSettings.Read(body["settings"]);
            if (request.Path.EndsWith("/ready")) ready = body["ready"].Bool();
            if (request.Path.EndsWith("/queue")) channel = body["channel"].String();
            if (request.Path.EndsWith("/cancel")) channel = null;
            if (request.Path.EndsWith("/leave")) { channel = null; return Json(new Dictionary<string, object>()); }
            var result = new Dictionary<string, object> {
                ["id"] = PartyId, ["revision"] = (++revision).ToString(), ["leaderId"] = Player, ["invite"] = Code,
                ["members"] = new[] { new Dictionary<string, object> { ["playerId"] = Player, ["ready"] = ready } },
                ["settings"] = settings.ToValue(), ["state"] = channel == null ? "OPEN" : "QUEUED" };
            if (channel != null) result["activeAttempt"] = new Dictionary<string, object> { ["channel"] = channel };
            if (channel != null && assignment != null) result["assignment"] = assignment;
            return Json(result);
        }

        static OnlineResponse Json(object value) => new OnlineResponse(200, OnlineJson.Encode(value));

        sealed class MemoryHttp : IOnlineHttpTransport
        {
            public WordboundLobbyPartyTests Owner;
            public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken cancellation)
            {
                cancellation.ThrowIfCancellationRequested();
                return Task.FromResult(Owner.Respond(request));
            }
        }

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
            public int Copies, Finds, Cancels, Leaves;
            public void Invite() { }
            public void Join(string code) { }
            public void Copy() => Copies++;
            public void Leave() => Leaves++;
            public void FindMatch() => Finds++;
            public void Cancel() => Cancels++;
            public void Retry() { }
        }
    }
}
