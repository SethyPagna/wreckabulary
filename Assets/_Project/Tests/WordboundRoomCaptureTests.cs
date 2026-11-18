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
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    [Explicit, Category("WordboundRoomCapture")]
    public sealed class WordboundRoomCaptureTests
    {
        const string Player = "10000000-0000-4000-8000-000000000001";
        const string Second = "10000000-0000-4000-8000-000000000002", Third = "10000000-0000-4000-8000-000000000003";
        const string PartyId = "20000000-0000-4000-8000-000000000001", MatchId = "30000000-0000-4000-8000-000000000001";
        const string Seed = "0123456789abcdef", Code = "1234567890abcdef", Pool = "room-capture";
        const string ThemePath = "Assets/_Project/Data/Wordbound/Authoring/Interface.asset";
        readonly List<string> captured = new();
        readonly List<string> requests = new();
        UIRefinementCaptureTests.GameViewScope view;
        GameObject owner;
        WordboundPartyMenu menu;
        WordboundOnlineShellCapturePresenter presenter;
        WordboundPartyCoordinator coordinator;
        RSA rsa;
        OnlineSettings settings;
        Dictionary<string, object> assignment;
        string channel, directory, leader = Player;
        string[] members = { Player };
        bool ready;
        int revision;
        float now = 1000f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.GetFullPath("BuildNotes/wordbound-rooms/capture/run-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ"));
            Directory.CreateDirectory(directory);
            view = new UIRefinementCaptureTests.GameViewScope(); view.Select(1280, 720);
            yield return TestScenes.Reset();
            string items = File.ReadAllText("Assets/_Project/Data/Config/items.json");
            string rules = File.ReadAllText("Assets/_Project/Data/Config/rules.json");
            var catalogue = WordboundPublicCourseCatalog.FromTestedContent(Pool, new[] {
                new WordboundClientContent(items, rules, "Freestyle", "Solo", Seed),
                new WordboundClientContent(items, rules, "TeamRace", "Duo", Seed) });
            rsa = new RSACryptoServiceProvider(2048) { PersistKeyInCsp = false };
            var key = rsa.ExportParameters(false);
            var verifier = new OnlineAdmissionVerifier("wreckabulary-wordbound", OnlineJson.Parse(OnlineJson.Encode(
                new Dictionary<string, object> { ["keys"] = new[] { new Dictionary<string, object> {
                    ["kty"] = "RSA", ["kid"] = "room-fixture", ["use"] = "sig", ["alg"] = "RS256",
                    ["n"] = OnlineJson.Base64Url(key.Modulus), ["e"] = OnlineJson.Base64Url(key.Exponent) } } })));
            coordinator = new WordboundPartyCoordinator(new OnlineCoordinatorClient(new MemoryHttp { Owner = this }), verifier, items, rules,
                Pool, new WordboundPublicCourseRegistry(catalogue.Json), () => 1000000);
            owner = new GameObject("Room capture fixture - no network");
            var camera = owner.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.07f, .11f, .12f); camera.cullingMask = 0;
            menu = owner.AddComponent<WordboundPartyMenu>(); menu.enabled = false;
            menu.Theme = AssetDatabase.LoadAssetAtPath<WordboundUiTheme>(ThemePath);
            Assert.IsNotNull(menu.Theme);
            Set("coordinator", coordinator); Set("publicCourses", catalogue);
            Set("itemSource", items); Set("ruleSource", rules);
            Set("loadQuickChoices", (Func<string>)(() => "v2;lists=freeplay;name=Ana"));
            Set("saveQuickChoices", (Action<string>)(_ => { }));
            Set("utcOffsetMinutes", (Func<int>)(() => 420));
            Set("quickPlayClock", (Func<long>)(() => 0L));
            Set("searchClock", (Func<float>)(() => now));
            menu.BeginMatchRequested = (_, _) => throw new InvalidOperationException("The capture fixture cannot launch gameplay.");
            presenter = owner.AddComponent<WordboundOnlineShellCapturePresenter>(); presenter.Menu = menu;
        }

        [UnityTest]
        public IEnumerator CaptureSearchCountdownAndRoomViews()
        {
            menu.StartMatchmaking();
            Assert.IsTrue(menu.Searching);
            now += 42f;
            Assert.AreEqual("Searching 0:42", Call("LobbySearchTitle"));
            yield return Capture("search-timer", "lobby-cancel");
            yield return Complete((Task)Call("LeaveLobbySearch"));

            assignment = new Dictionary<string, object> { ["matchId"] = MatchId, ["state"] = "ADMITTING" };
            menu.StartMatchmaking();
            Assert.AreEqual("Match found · starting in 5", Call("LobbySearchTitle"));
            yield return Capture("match-found-5", "lobby-cancel");
            now += 3f;
            Assert.AreEqual("Match found · starting in 2", Call("LobbySearchTitle"));
            yield return Capture("match-found-2", "lobby-cancel");
            yield return Complete((Task)Call("LeaveLobbySearch"));

            assignment = Room("FORMING", (Player, 0), (Second, 0), (Third, 1));
            menu.HostRoom("TeamRace", "Duo", 0);
            Assert.IsTrue(menu.InRoom);
            Assert.AreEqual("3 / 4 players · needs 4 to start", Call("RoomStatus", Code));
            yield return Capture("room-host-team-race", "room-copy", "room-leave");
            yield return Complete((Task)Call("LeaveRoom"));

            assignment = Room("FORMING", (Second, 0), (Player, 1));
            menu.JoinFriendRoom(WordboundRoomInvite.Encode(Code, coordinator.Settings("TeamRace", "Duo", true, "eu"), Seed));
            Assert.IsTrue(menu.InRoom);
            yield return Capture("room-guest-team-race", "room-copy", "room-leave");
            yield return Complete((Task)Call("LeaveRoom"));

            assignment = Room("ADMITTING", (Player, 0), (Second, 0), (Third, 0));
            menu.HostRoom("Freestyle", "Solo", 0);
            Assert.AreEqual("Everyone's in · starting in 5", Call("RoomStatus", Code));
            yield return Capture("room-starting", "room-leave");
            yield return Complete((Task)Call("LeaveRoom"));

            assignment = null;
            menu.InviteFriends();
            Assert.IsTrue(menu.InFriendsParty);
            Assert.AreEqual("Searches Free Roam · 8", Call("FriendsPartyStatus"));
            yield return Capture("party-leader", "party-copy", "party-leave", "party-find");
            yield return Complete((Task)Call("LeaveFriendsParty"));

            leader = Second; members = new[] { Second, Player, Third }; ready = true;
            settings = coordinator.Settings("Freestyle", "Solo", true, "asia");
            menu.JoinFriendsParty(WordboundPartyInvite.Encode(PartyId, Code, Seed));
            Assert.IsTrue(menu.InFriendsParty);
            Assert.AreEqual(WordboundPartyMenu.WaitingForLeader, Call("FriendsPartyStatus"));
            yield return Capture("party-member", "party-copy", "party-leave");
            yield return Complete((Task)Call("LeaveFriendsParty"));
            Assert.AreEqual(8, captured.Count);
            File.WriteAllText(Path.Combine(directory, "receipt.json"), JsonUtility.ToJson(new Receipt {
                status = "PASS", engine = Application.unityVersion, utc = DateTime.UtcNow.ToString("O"),
                scope = "Actual menu rendering with in-memory coordinator responses. No network, no physical input, no match launch.",
                files = captured.ToArray(), requests = requests.ToArray() }, true));
        }

        [Serializable] sealed class Receipt
        {
            public string status, engine, utc, scope;
            public bool synthetic = true;
            public string[] files, requests;
        }

        IEnumerator Capture(string state, params string[] required)
        {
            int before = menu.QaCompletedRepaintCount;
            for (int frame = 0; frame < 120 && (Screen.width != 1280 || Screen.height != 720 ||
                menu.QaCompletedRepaintCount < before + 3); frame++) yield return null;
            Assert.AreEqual(1280, Screen.width); Assert.AreEqual(720, Screen.height);
            Assert.GreaterOrEqual(menu.QaCompletedRepaintCount, before + 3); Assert.IsNull(presenter.Failure);
            yield return new WaitForEndOfFrame();
            var controls = (List<string>)typeof(WordboundMenuFocus).GetField("controls", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(Get<WordboundMenuFocus>("partyFocus"));
            foreach (string id in required) Assert.Contains(id, controls, state + " must register " + id);
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                string file = captured.Count.ToString("00") + "-" + state + "-1280x720.png";
                byte[] png = texture.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(directory, file), png);
                using var sha = SHA256.Create();
                captured.Add(file + " " + BitConverter.ToString(sha.ComputeHash(png)).Replace("-", "").ToLowerInvariant());
            }
            finally { Object.Destroy(texture); }
        }

        static IEnumerator Complete(Task task)
        {
            for (int frame = 0; frame < 240 && !task.IsCompleted; frame++) yield return null;
            Assert.IsTrue(task.IsCompleted); Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
        }

        static Dictionary<string, object> Room(string state, params (string player, int team)[] seats) => new Dictionary<string, object> {
            ["matchId"] = MatchId, ["state"] = state, ["privateCode"] = Code,
            ["roster"] = seats.Select((seat, index) => new Dictionary<string, object> {
                ["playerId"] = seat.player, ["teamId"] = seat.team, ["seat"] = index }).ToArray() };

        OnlineResponse Respond(OnlineRequest request)
        {
            requests.Add(request.Method + " " + request.Path);
            if (request.Path == "/v1/guests") return Json(new Dictionary<string, object> {
                ["playerId"] = Player, ["accessToken"] = "synthetic.room.token",
                ["refreshToken"] = Player + "." + OnlineJson.Base64Url(new byte[32]), ["expiresIn"] = 900 });
            var body = string.IsNullOrEmpty(request.JsonBody) ? null : OnlineJson.Parse(request.JsonBody);
            if (request.Path == "/v1/parties") { settings = OnlineSettings.Read(body["settings"]); channel = null; ready = false; }
            else if (request.Path.EndsWith("/ready")) ready = body["ready"].Bool();
            else if (request.Path.EndsWith("/queue")) channel = body["channel"].String();
            else if (request.Path.EndsWith("/cancel")) channel = null;
            else if (request.Path.EndsWith("/leave")) return Json(new Dictionary<string, object>());
            else if (request.Path != "/v1/party-joins") throw new InvalidOperationException("Unexpected capture fixture request: " + request.Path);
            var result = new Dictionary<string, object> {
                ["id"] = PartyId, ["revision"] = (++revision).ToString(), ["leaderId"] = leader, ["invite"] = Code,
                ["members"] = members.Select(member => new Dictionary<string, object> { ["playerId"] = member, ["ready"] = ready }).ToArray(),
                ["settings"] = settings.ToValue(), ["state"] = channel == null ? "OPEN" : "QUEUED" };
            if (channel != null) result["activeAttempt"] = new Dictionary<string, object> { ["channel"] = channel };
            if (channel != null && assignment != null) result["assignment"] = assignment;
            return Json(result);
        }

        static OnlineResponse Json(object value) => new OnlineResponse(200, OnlineJson.Encode(value));

        sealed class MemoryHttp : IOnlineHttpTransport
        {
            public WordboundRoomCaptureTests Owner;
            public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken cancellation)
            {
                cancellation.ThrowIfCancellationRequested();
                return Task.FromResult(Owner.Respond(request));
            }
        }

        object Call(string name, params object[] args)
        {
            try { return typeof(WordboundPartyMenu).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        void Set(string name, object value) => typeof(WordboundPartyMenu).GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(menu, value);

        T Get<T>(string name) => (T)typeof(WordboundPartyMenu).GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (owner) Object.DestroyImmediate(owner);
            coordinator?.Dispose(); rsa?.Dispose(); view?.Dispose();
            WordboundQuickPlayEntry.CancelRequest(); WordboundQuickPlayEntry.CancelReturn();
            WordboundRoomEntry.CancelRequest(); WordboundRoomEntry.CancelReturn();
            yield return TestScenes.Reset();
        }
    }
}
