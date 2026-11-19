using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundPartyHostTests
    {
        const string Player = "10000000-0000-4000-8000-000000000001", Friend = "10000000-0000-4000-8000-000000000002";
        const string PartyId = "20000000-0000-4000-8000-000000000001";
        const string Seed = "0123456789abcdef", Code = "1234567890abcdef";
        readonly List<OnlineRequest> requests = new List<OnlineRequest>();
        readonly List<GameObject> stands = new List<GameObject>();
        Func<IWordboundLobbyParty> previousFactory;
        WordboundPartyCoordinator coordinator;
        WordboundPublicCourseCatalog catalogue;
        RSA rsa;
        OnlineSettings settings;
        Dictionary<string, object> assignment;
        string items, rules, leader, channel, failPath, guestFailure;
        bool ready, hub;
        int revision, humans, entered;
        float now;

        [SetUp]
        public void SetUp()
        {
            leader = Player; channel = null; failPath = null; guestFailure = null; settings = null; assignment = null;
            ready = false; hub = true; revision = 0; humans = 1; entered = 0; now = 100f;
            requests.Clear();
            items = File.ReadAllText("Assets/_Project/Data/Config/items.json");
            rules = File.ReadAllText("Assets/_Project/Data/Config/rules.json");
            catalogue = WordboundPublicCourseCatalog.FromTestedContent("host-tests",
                new[] { new WordboundClientContent(items, rules, "Freestyle", "Solo", Seed) });
            rsa = new RSACryptoServiceProvider(2048) { PersistKeyInCsp = false };
            var key = rsa.ExportParameters(false);
            var verifier = new OnlineAdmissionVerifier("wreckabulary-wordbound", OnlineJson.Parse(OnlineJson.Encode(new Dictionary<string, object> {
                ["keys"] = new[] { new Dictionary<string, object> { ["kty"] = "RSA", ["kid"] = "host-test", ["use"] = "sig",
                    ["alg"] = "RS256", ["n"] = OnlineJson.Base64Url(key.Modulus), ["e"] = OnlineJson.Base64Url(key.Exponent) } }
            })));
            coordinator = new WordboundPartyCoordinator(new OnlineCoordinatorClient(new FakeHttp { Owner = this }), verifier, items, rules,
                "host-tests", new WordboundPublicCourseRegistry(catalogue.Json), () => 1000000);
            previousFactory = WordboundLobbyParty.Register(MakeHost);
            WordboundQuickPlayEntry.CancelReturn(); WordboundRoomEntry.CancelReturn();
            WordboundLobbyParty.TakeMessage();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                foreach (var left in Object.FindObjectsByType<WordboundPartyHost>(FindObjectsSortMode.None)) Object.DestroyImmediate(left.gameObject);
                foreach (var stand in stands) if (stand) Object.DestroyImmediate(stand);
                stands.Clear();
            }
            finally
            {
                WordboundLobbyParty.Register(previousFactory);
                WordboundLobbyParty.TakeMessage();
                coordinator?.Dispose(); rsa?.Dispose();
                WordboundQuickPlayEntry.CancelReturn(); WordboundRoomEntry.CancelReturn();
            }
        }

        [Test]
        public void InviteFromTheLobbyMakesAPartyAndStaysInTheLobby()
        {
            var party = WordboundLobbyParty.Open();
            Assert.IsInstanceOf<WordboundPartyHost>(party);
            Assert.AreSame(party, WordboundLobbyParty.Open(), "One lobby party at a time.");
            party.Invite();
            Assert.IsTrue(party.Active); Assert.IsTrue(party.Leader); Assert.IsTrue(ready);
            CollectionAssert.AreEqual(new[] { "/v1/guests", "/v1/parties" }, requests.Take(2).Select(request => request.Path));
            Assert.IsTrue(requests.Last().Path.EndsWith("/ready"));
            Assert.AreEqual("Party · 1 of 4", party.Heading);
            CollectionAssert.AreEqual(new[] { "You · leader · ready" }, party.Members);
            Assert.AreEqual("Searches Free Roam · 8", party.Status);
            Assert.IsNull(party.Problem);
            Assert.IsTrue(party.CanCopy); Assert.IsTrue(party.CanLeave); Assert.IsTrue(party.CanFind);
            Assert.IsFalse(party.CanCancel); Assert.IsFalse(party.Searching); Assert.IsFalse(party.Found);
            Tick();
            Assert.AreEqual(0, entered, "Nothing loads a scene until a match is found.");
            Assert.AreSame(party, WordboundLobbyParty.Current);
            Assert.IsFalse(WordboundPartyHost.Current.Menu.Visible, "The online menu stays hidden in the lobby.");
            Assert.AreEqual(1, Object.FindObjectsByType<WordboundPartyMenu>(FindObjectsSortMode.None).Length);
            string before = GUIUtility.systemCopyBuffer;
            try
            {
                party.Copy();
                Assert.IsTrue(party.Copied);
                Assert.IsTrue(FriendRooms.LooksLikePartyInvite(GUIUtility.systemCopyBuffer), GUIUtility.systemCopyBuffer);
            }
            finally { GUIUtility.systemCopyBuffer = before; }
        }

        [Test]
        public void TheLeadersSearchCountsDownInTheLobbyThenOpensTheMatchSceneOnce()
        {
            var party = WordboundLobbyParty.Open();
            party.Invite();
            party.FindMatch();
            Assert.IsTrue(party.Searching); Assert.IsFalse(party.Found); Assert.AreEqual("QuickPlay", channel);
            Assert.IsFalse(party.CanFind); Assert.IsTrue(party.CanCancel);
            now += 42.4f;
            Assert.AreEqual("Searching 0:42", party.SearchTitle);
            assignment = new Dictionary<string, object> { ["state"] = "ADMITTING" };
            coordinator.Poll().GetAwaiter().GetResult();
            Assert.IsTrue(party.Found);
            Assert.AreEqual("Match found · starting in 5", party.SearchTitle);
            Tick();
            Assert.AreEqual(0, entered, "The countdown runs in the lobby first.");
            now += 2.2f;
            Assert.AreEqual("Match found · starting in 3", party.SearchTitle);
            hub = false; now += 3f; Tick();
            Assert.AreEqual(0, entered, "Only the lobby opens the match scene.");
            hub = true; Tick(); Tick();
            Assert.AreEqual(1, entered, "The match scene opens once, after the countdown.");
            var host = WordboundPartyHost.Current;
            Assert.IsTrue(host.TakeEntering()); Assert.IsFalse(host.TakeEntering());
            Assert.AreSame(party, WordboundLobbyParty.Current);
        }

        [Test]
        public void CancelStopsTheSearchAndKeepsThePartyInTheLobby()
        {
            var party = WordboundLobbyParty.Open();
            party.Invite();
            party.FindMatch();
            party.Cancel();
            Assert.IsTrue(requests.Last().Path.EndsWith("/cancel"));
            Assert.IsFalse(party.Searching); Assert.IsTrue(party.Active); Assert.IsTrue(party.CanFind);
            Tick();
            Assert.AreSame(party, WordboundLobbyParty.Current);
            Assert.IsNull(WordboundLobbyParty.TakeMessage());
        }

        [Test]
        public void AMemberIsReadiedAndSeesTheLeadersSearchWithoutControllingIt()
        {
            leader = Friend; humans = 2;
            settings = coordinator.Settings("Freestyle", "Solo", true, "asia");
            var party = WordboundLobbyParty.Open();
            party.Join(WordboundPartyInvite.Encode(PartyId, Code, Seed));
            var menu = WordboundPartyHost.Current.Menu;
            Assert.IsTrue(party.Active); Assert.IsFalse(party.Leader);
            Assert.AreEqual("/v1/party-joins", requests.Last().Path);
            Invoke(menu, "TickFriendsParty");
            Assert.IsTrue(ready, "A member is ready as soon as they join.");
            Assert.AreEqual("Party · 2 of 4", party.Heading);
            CollectionAssert.AreEqual(new[] { "You · ready", "Player 2 · leader · ready" }, party.Members);
            Assert.AreEqual(WordboundPartyMenu.WaitingForLeader, party.Status);
            Assert.IsFalse(party.CanFind); Assert.IsFalse(party.CanCancel); Assert.IsTrue(party.CanLeave);
            channel = "QuickPlay";
            coordinator.Poll().GetAwaiter().GetResult();
            Invoke(menu, "TickFriendsParty");
            Assert.IsTrue(party.Searching);
            Assert.IsFalse(party.CanCancel, "Only the leader cancels the search.");
            now += 7f;
            Assert.AreEqual("Searching 0:07", party.SearchTitle);
            int count = requests.Count;
            party.FindMatch(); party.Cancel();
            Assert.AreEqual(count, requests.Count, "A member never queues or cancels.");
        }

        [Test]
        public void LeavingFromTheLobbyLeavesThePartyAndClosesItsMenuAndCoordinator()
        {
            var party = WordboundLobbyParty.Open();
            party.Invite();
            var host = WordboundPartyHost.Current;
            party.Leave();
            Assert.IsTrue(requests.Last().Path.EndsWith("/leave"));
            Assert.IsNull(coordinator.Party);
            Assert.IsNull(WordboundLobbyParty.Current); Assert.IsFalse(party.Active);
            Assert.IsFalse(host, "The scene-surviving owner is gone.");
            Assert.AreEqual(0, Object.FindObjectsByType<WordboundPartyMenu>(FindObjectsSortMode.None).Length);
            Assert.IsNull(WordboundLobbyParty.TakeMessage(), "Leaving on purpose posts nothing.");
            Assert.IsFalse(WordboundQuickPlayEntry.TakeReturn(), "The lobby party never marks a scene return.");
        }

        [Test]
        public void AFailedConnectClosesThePartyAndTellsTheLobbyWhy()
        {
            guestFailure = "GUEST_LIMIT";
            var party = WordboundLobbyParty.Open();
            party.Invite();
            Assert.IsFalse(coordinator.RetryRequired);
            string problem = party.Problem;
            Assert.IsNotNull(problem);
            Tick();
            Assert.IsNull(WordboundLobbyParty.Current);
            Assert.AreEqual(problem, WordboundLobbyParty.TakeMessage());
        }

        [Test]
        public void ALostResponseKeepsThePartyOpenWithRetry()
        {
            failPath = "/v1/parties";
            var party = WordboundLobbyParty.Open();
            party.Invite();
            Assert.IsTrue(coordinator.RetryRequired);
            Tick();
            Assert.AreSame(party, WordboundLobbyParty.Current, "A lost response is retried, not dropped.");
            Assert.IsTrue(party.CanRetry); Assert.IsNotNull(party.Problem);
            party.Retry();
            Assert.IsFalse(coordinator.RetryRequired); Assert.IsNotNull(coordinator.Party);
            Invoke(WordboundPartyHost.Current.Menu, "TickFriendsParty");
            Assert.IsTrue(ready, "The leader is readied after the retry.");
            Assert.AreEqual("Party · 1 of 4", party.Heading);
        }

        [Test]
        public void PlayingSomethingElseLeavesThePartyAndSaysSo()
        {
            var party = WordboundLobbyParty.Open();
            party.Invite();
            WordboundPartyHost.Current.LeaveAndClose(WordboundPartyHost.LeftForOtherPlay);
            Assert.IsTrue(requests.Last().Path.EndsWith("/leave"));
            Assert.IsNull(WordboundLobbyParty.Current);
            Assert.AreEqual(WordboundPartyHost.LeftForOtherPlay, WordboundLobbyParty.TakeMessage());
        }

        [Test]
        public void TheLauncherAdoptsOnlyAPartyThatOpenedTheMatchScene()
        {
            var party = WordboundLobbyParty.Open();
            party.Invite();
            var host = WordboundPartyHost.Current;
            var pick = typeof(WordboundLauncher).GetMethod("LobbyPartyFor", BindingFlags.Static | BindingFlags.NonPublic);
            Set(host, "entering", true);
            Assert.IsNull(pick.Invoke(null, new object[] { "Freestyle", false, false, null }), "A solo course leaves the party.");
            Assert.IsNull(WordboundLobbyParty.Current);
            Assert.AreEqual(WordboundPartyHost.LeftForOtherPlay, WordboundLobbyParty.TakeMessage());
            party = WordboundLobbyParty.Open();
            party.Invite();
            host = WordboundPartyHost.Current;
            Set(host, "entering", true);
            Assert.AreSame(host, pick.Invoke(null, new object[] { null, false, false, null }));
            Assert.IsTrue(party.Active, "Adoption keeps the party.");
            Assert.IsNull(pick.Invoke(null, new object[] { null, false, false, null }), "Entering is taken once; another load leaves the party.");
            Assert.IsNull(WordboundLobbyParty.Current);
        }

        [Test]
        public void AdoptionShowsTheMenuInTheMatchSceneAndReleaseHandsItBack()
        {
            var party = WordboundLobbyParty.Open();
            party.Invite();
            var host = WordboundPartyHost.Current;
            var launcher = Stand();
            var menu = host.Adopt(launcher);
            Assert.AreSame(host.Menu, menu); Assert.IsTrue(menu.Visible); Assert.IsTrue(host.Adopted);
            Assert.IsFalse(party.CanFind, "While a scene holds the party, the lobby cannot search.");
            Assert.IsTrue(host.ReadyForLobby, "An idle party goes back to the lobby.");
            Assert.Throws<InvalidOperationException>(() => host.Adopt(Stand()));
            menu.BeginMatchRequested = (_, _) => Task.CompletedTask;
            host.Release(Stand());
            Assert.IsTrue(host.Adopted, "Only the adopting scene releases the party.");
            host.Release(launcher);
            Assert.IsFalse(menu.Visible); Assert.IsFalse(host.Adopted); Assert.IsNull(menu.BeginMatchRequested);
            Assert.AreSame(party, WordboundLobbyParty.Current); Assert.IsTrue(party.CanFind);
        }

        [Test]
        public void AFoundMatchThatFallsThroughSendsTheMatchSceneBackToTheLobby()
        {
            var party = WordboundLobbyParty.Open();
            party.Invite();
            assignment = new Dictionary<string, object> { ["state"] = "ADMITTING" };
            party.FindMatch();
            var host = WordboundPartyHost.Current;
            host.Adopt(Stand());
            Assert.IsTrue(party.Found);
            Assert.IsFalse(host.ReadyForLobby, "A found match keeps the match scene open.");
            assignment = null;
            coordinator.Poll().GetAwaiter().GetResult();
            Assert.IsFalse(party.Found);
            Assert.IsTrue(host.ReadyForLobby);
            Assert.IsTrue(party.Searching, "The search carries on from the lobby.");
        }

        [Test]
        public void WithoutTheOnlineFactoryTheLobbyKeepsTheSceneRoute()
        {
            WordboundLobbyParty.Register(null);
            Assert.IsFalse(WordboundLobbyParty.Available);
            Assert.IsNull(WordboundLobbyParty.Open());
            Assert.IsFalse(WordboundLobbyParty.InParty);
        }

        IWordboundLobbyParty MakeHost()
        {
            var host = WordboundPartyHost.Create(menu =>
            {
                Set(menu, "coordinator", coordinator); Set(menu, "publicCourses", catalogue);
                Set(menu, "itemSource", items); Set(menu, "ruleSource", rules);
                Set(menu, "loadQuickChoices", (Func<string>)(() => "v2;lists=freeplay"));
                Set(menu, "saveQuickChoices", (Action<string>)(_ => { }));
                Set(menu, "utcOffsetMinutes", (Func<int>)(() => 420));
                Set(menu, "quickPlayClock", (Func<long>)(() => 0L));
                Set(menu, "searchClock", (Func<float>)(() => now));
            });
            Set(host, "inHub", (Func<bool>)(() => hub));
            Set(host, "enterMatch", (Action)(() => entered++));
            Set(host, "clock", (Func<float>)(() => now));
            return host;
        }

        WordboundLauncher Stand()
        {
            var stand = new GameObject("Launcher stand-in");
            stands.Add(stand);
            return stand.AddComponent<WordboundLauncher>();
        }

        static void Tick() => Invoke(WordboundPartyHost.Current, "Tick");

        OnlineResponse Respond(OnlineRequest request)
        {
            requests.Add(request);
            if (failPath != null && request.Path == failPath) { failPath = null; throw new IOException("Synthetic lost response"); }
            if (request.Path == "/v1/guests")
            {
                if (guestFailure != null)
                    return new OnlineResponse(403, OnlineJson.Encode(new Dictionary<string, object> {
                        ["error"] = new Dictionary<string, object> { ["code"] = guestFailure } }));
                return Json(new Dictionary<string, object> {
                    ["playerId"] = Player, ["accessToken"] = "test.token", ["refreshToken"] = Player + "." + OnlineJson.Base64Url(new byte[32]), ["expiresIn"] = 900
                });
            }
            var body = string.IsNullOrEmpty(request.JsonBody) ? null : OnlineJson.Parse(request.JsonBody);
            if (request.Path == "/v1/parties" || request.Method == "PATCH") settings = OnlineSettings.Read(body["settings"]);
            if (request.Path.EndsWith("/ready")) ready = body["ready"].Bool();
            if (request.Path.EndsWith("/queue")) channel = body["channel"].String();
            if (request.Path.EndsWith("/cancel")) channel = null;
            if (request.Path.EndsWith("/leave")) return Json(new Dictionary<string, object>());
            var response = new Dictionary<string, object> {
                ["id"] = PartyId, ["revision"] = (++revision).ToString(), ["leaderId"] = leader, ["invite"] = Code,
                ["members"] = Enumerable.Range(0, humans).Select(index => new Dictionary<string, object> {
                    ["playerId"] = index == 0 ? Player : "10000000-0000-4000-8000-" + (index + 1).ToString("D12"),
                    ["ready"] = ready }).ToArray(),
                ["settings"] = settings.ToValue(), ["state"] = channel == null ? "OPEN" : "QUEUED"
            };
            if (channel != null) response["activeAttempt"] = new Dictionary<string, object> { ["channel"] = channel };
            if (channel != null && assignment != null) response["assignment"] = assignment;
            return Json(response);
        }

        static OnlineResponse Json(object value) => new OnlineResponse(200, OnlineJson.Encode(value));

        sealed class FakeHttp : IOnlineHttpTransport
        {
            public WordboundPartyHostTests Owner;
            public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken cancellation)
            {
                try { return Task.FromResult(Owner.Respond(request)); }
                catch (Exception error) { return Task.FromException<OnlineResponse>(error); }
            }
        }

        static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        static void Invoke(object target, string name, params object[] args)
        {
            try { target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
    }
}
