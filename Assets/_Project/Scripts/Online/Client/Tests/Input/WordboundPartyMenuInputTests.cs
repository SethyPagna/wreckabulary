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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Wreckabulary.Rules.Voice;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Voice;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundPartyMenuInputTests
    {
        const string Player = "10000000-0000-4000-8000-000000000001", PartyId = "20000000-0000-4000-8000-000000000001";
        const string Seed = "0123456789abcdef", Code = "1234567890abcdef";
        const string Host = "10000000-0000-4000-8000-000000000009";
        const string FreeplayOnly = "v2;lists=freeplay";
        GameObject root;
        WordboundPartyMenu menu;
        WordboundPartyCoordinator coordinator;
        MenuWindow window;
        FakeHttp http;
        RSA rsa;
        Gamepad pad, previousPad;
        bool previousTyping;
        WordboundNativeInputScope input;
        OnlineSettings settings;
        bool ready;
        string channel, savedChoices, leader;
        Dictionary<string, object> assignment;
        int revision, closed, humans;

        [SetUp]
        public void SetUp()
        {
            previousPad = Gamepad.current; previousTyping = DesktopBinding.Typing;
            try
            {
                settings = null; ready = false; channel = null; assignment = null; savedChoices = FreeplayOnly; revision = closed = 0; humans = 1;
                leader = Player;
                input = new WordboundNativeInputScope();
                DesktopBinding.Typing = false;
                pad = InputSystem.AddDevice<Gamepad>();
                string items = File.ReadAllText("Assets/_Project/Data/Config/items.json");
                string rules = File.ReadAllText("Assets/_Project/Data/Config/rules.json");
                var content = new WordboundClientContent(items, rules, "Freestyle", "Solo", Seed);
                var catalogue = WordboundPublicCourseCatalog.FromTestedContent("pad-tests", new[] { content });
                rsa = new RSACryptoServiceProvider(2048) { PersistKeyInCsp = false };
                var key = rsa.ExportParameters(false);
                var verifier = new OnlineAdmissionVerifier("wreckabulary-wordbound", OnlineJson.Parse(OnlineJson.Encode(new Dictionary<string, object> {
                    ["keys"] = new[] { new Dictionary<string, object> { ["kty"] = "RSA", ["kid"] = "pad-test", ["use"] = "sig",
                        ["alg"] = "RS256", ["n"] = OnlineJson.Base64Url(key.Modulus), ["e"] = OnlineJson.Base64Url(key.Exponent) } }
                })));
                http = new FakeHttp { Handler = Respond };
                coordinator = new WordboundPartyCoordinator(new OnlineCoordinatorClient(http), verifier, items, rules,
                    "pad-tests", new WordboundPublicCourseRegistry(catalogue.Json), () => 1000000);
                root = new GameObject("Party input native test"); menu = root.AddComponent<WordboundPartyMenu>(); menu.enabled = false;
                Set(menu, "coordinator", coordinator); Set(menu, "publicCourses", catalogue);
                Set(menu, "itemSource", items); Set(menu, "ruleSource", rules);
                Set(menu, "loadQuickChoices", (Func<string>)(() => savedChoices));
                Set(menu, "saveQuickChoices", (Action<string>)(value => savedChoices = value));
                Set(menu, "utcOffsetMinutes", (Func<int>)(() => 420));
                Set(menu, "quickPlayClock", (Func<long>)(() => 0L));
                menu.CloseRequested += () => closed++;
                window = ScriptableObject.CreateInstance<MenuWindow>(); window.Menu = menu;
                window.position = new Rect(100, 100, 700, 900); window.ShowUtility();
                Sample(new GamepadState { leftStick = new Vector2(.8f, 0) });
                Sample(default); Sample(default);
            }
            catch (Exception setupError)
            {
                try { TearDown(); }
                catch (Exception cleanupError) { throw new AggregateException(setupError, cleanupError); }
                throw;
            }
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (window) window.Close();
                if (menu) Get<WordboundUiStyles>(menu, "ui").Dispose();
                if (root) Object.DestroyImmediate(root);
                coordinator?.Dispose(); rsa?.Dispose();
                if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
                WordboundQuickPlayEntry.CancelRequest(); WordboundQuickPlayEntry.CancelReturn();
                WordboundRoomEntry.CancelRequest(); WordboundRoomEntry.CancelReturn();
                if (previousPad != null && previousPad.added) previousPad.MakeCurrent();
            }
            finally
            {
                try { input?.Dispose(); }
                finally { input = null; DesktopBinding.Typing = previousTyping; }
            }
        }

        [UnityTest]
        public IEnumerator PublicCoopDefaultsFindHumansWhilePrivateAndFfaChoicesRemainExplicit()
        {
            yield return Classic();
            yield return Activate("mode:4");
            Assert.IsTrue(Get<bool>(menu, "fill"));
            Assert.IsFalse(coordinator.Authenticated); Assert.IsEmpty(http.Requests);
            Draw(EventType.Layout);
            var controls = Get<List<string>>(Get<WordboundMenuFocus>(menu, "partyFocus"), "controls");
            Assert.Contains("fill", controls); Assert.IsFalse(controls.Contains("public-map:0"));
            yield return Activate("mode:3"); Draw(EventType.Layout);
            Assert.IsFalse(Get<bool>(menu, "fill")); Assert.IsFalse(controls.Contains("fill"));
            yield return Activate("mode:4"); Assert.IsTrue(Get<bool>(menu, "fill"));
            yield return Activate("create-settings");
            Draw(EventType.Layout);
            Assert.Contains("public-map:0", controls);
            yield return Activate("map-source:1"); Assert.IsFalse(Get<bool>(menu, "fill"));
            Assert.IsEmpty(http.Requests, "Browsing modes and maps cannot create a party or enqueue a match.");
        }

        [UnityTest]
        public IEnumerator PublicCoopPreflightBlocksOneHumanWithoutFillAndReenablesForFillOrTwoHumans()
        {
            yield return Classic();
            yield return Activate("connect"); yield return Activate("mode:4");
            yield return Activate("fill"); yield return Activate("create-party");
            Assert.AreEqual(1, coordinator.Party.Members.Count); Assert.IsFalse(coordinator.Party.Settings.Fill);
            Draw(EventType.Layout); Draw(EventType.Repaint);
            var controls = Get<List<string>>(Get<WordboundMenuFocus>(menu, "partyFocus"), "controls");
            Assert.IsFalse(controls.Contains("quick-play"), "The real Find match control must be disabled before a blocked request.");
            Assert.Contains("private-actions", controls, "Solo private play remains reachable.");
            int requests = http.Requests.Count;
            Assert.Throws<InvalidOperationException>(() => coordinator.Queue("QuickPlay"));
            Assert.AreEqual(requests, http.Requests.Count, "The coordinator's independent human guard remains intact.");

            yield return Activate("party-settings"); yield return Activate("fill"); yield return Activate("apply-settings");
            yield return Activate("party-settings"); Draw(EventType.Layout);
            Assert.IsTrue(coordinator.Party.Settings.Fill); Assert.Contains("quick-play", controls);

            yield return Activate("party-settings"); yield return Activate("fill"); yield return Activate("apply-settings");
            yield return Activate("party-settings"); Draw(EventType.Layout);
            Assert.IsFalse(coordinator.Party.Settings.Fill); Assert.IsFalse(controls.Contains("quick-play"));
            humans = 2; coordinator.Poll().GetAwaiter().GetResult();
            Draw(EventType.Layout); Draw(EventType.Repaint);
            Assert.AreEqual(2, coordinator.Party.Members.Count); Assert.IsFalse(coordinator.Party.Settings.Fill);
            Assert.Contains("quick-play", controls, "Exactly two human players must be admitted without changing their Fill setting.");
            yield return Activate("ready"); yield return Activate("quick-play");
            Assert.AreEqual("QuickPlay", coordinator.Party.Channel); Assert.IsFalse(coordinator.Party.Settings.Fill);
        }

        [UnityTest]
        public IEnumerator PartyMembersGetPerPlayerMutesSpeakingMarksAndYourRowShowsYourMic()
        {
            yield return Classic();
            using var voice = new VoiceTestScope();
            const string other = "10000000-0000-4000-8000-000000000002";
            humans = 2;
            yield return Activate("connect"); yield return Activate("mode:4"); yield return Activate("create-party");
            Assert.AreEqual(2, coordinator.Party.Members.Count);
            Invoke(menu, "SyncPartyVoice");
            Assert.AreEqual(2, VoiceChat.Room.Members.Count, "The party lobby is one voice room.");
            Assert.AreEqual(Player, VoiceChat.Self);
            Assert.AreSame(VoiceChat.Room, voice.Fake.Room);
            Assert.AreEqual("party:" + coordinator.Party.Id, VoiceChat.Room.Channel.Key, "Party voice meets on the party's own channel.");
            Assert.AreEqual(coordinator.Party.Settings.Region, VoiceChat.Room.Channel.Region, "Voice uses the party's region.");
            Assert.IsNotNull(VoiceChannel.RoomName(VoiceChat.Room, Player));
            Draw(EventType.Layout); Draw(EventType.Repaint);
            var controls = Get<List<string>>(Get<WordboundMenuFocus>(menu, "partyFocus"), "controls");
            Assert.Contains("voice-mute:" + other, controls);
            Assert.IsFalse(controls.Contains("voice-mute:" + Player), "Your own row has no mute button.");

            yield return Activate("voice-mute:" + other);
            Assert.IsTrue(VoiceChat.IsMuted(other));
            Assert.IsTrue(voice.Fake.Muted.Contains(other), "The mute reaches the transport.");
            Assert.AreEqual(VoiceIcons.SpeakerOff, Call(menu, "MemberVoiceIcon", other));
            yield return Activate("voice-mute:" + other);
            Assert.IsFalse(VoiceChat.IsMuted(other));

            VoiceChat.Tick(false, 50);
            Assert.AreEqual("", Call(menu, "MemberVoiceIcon", other));
            voice.Fake.Speak(other, true);
            Assert.AreEqual(VoiceIcons.Wave, Call(menu, "MemberVoiceIcon", other), "A talking member shows the sound-wave mark.");
            Draw(EventType.Layout); Draw(EventType.Repaint);

            Assert.AreEqual("Mic off", Call(menu, "SelfVoiceLine"));
            VoiceChat.Mic = MicMode.PushToTalk;
            string key = Get<bool>(menu, "partyUsesPad") ? ControlHints.PadPushToTalk : ControlHints.PushToTalkKey;
            Assert.AreEqual("Hold " + key + " to talk", Call(menu, "SelfVoiceLine"));
            VoiceChat.SelfMuted = true;
            Assert.AreEqual("Mic muted", Call(menu, "SelfVoiceLine"));

            Invoke(menu, "SyncPartyVoice");
            Assert.AreEqual(2, VoiceChat.Room.Members.Count, "An unchanged party keeps its room.");
            yield return Activate("leave-party");
            Assert.IsNull(coordinator.Party);
            Invoke(menu, "SyncPartyVoice");
            Assert.AreSame(VoiceRoom.Empty, VoiceChat.Room, "Leaving the party leaves its voice room.");
        }

        [UnityTest]
        public IEnumerator ASoloPartyHasNoVoiceRows()
        {
            yield return Classic();
            using var voice = new VoiceTestScope();
            yield return Activate("connect"); yield return Activate("mode:4"); yield return Activate("create-party");
            Invoke(menu, "SyncPartyVoice");
            Assert.AreSame(VoiceRoom.Empty, VoiceChat.Room, "Nobody to hear, so no voice room.");
            Draw(EventType.Layout); Draw(EventType.Repaint);
            var controls = Get<List<string>>(Get<WordboundMenuFocus>(menu, "partyFocus"), "controls");
            Assert.IsFalse(controls.Any(id => id.StartsWith("voice-", StringComparison.Ordinal)));
        }

        [UnityTest]
        public IEnumerator FindMatchFromTheLobbyShowsOnlyTheCountAndCancel()
        {
            menu.StartMatchmaking();
            Assert.IsTrue(coordinator.Authenticated); Assert.IsTrue(menu.Searching);
            var chosen = coordinator.Party.Settings;
            Assert.AreEqual("Freestyle", chosen.Mode, "The only ticked playlist with a public map.");
            Assert.AreEqual("asia", chosen.Region, "The region comes from the UTC+7 clock.");
            Assert.IsTrue(chosen.Fill); Assert.AreEqual(Seed, coordinator.Seed);
            Assert.AreEqual(4, http.Requests.Count, string.Join(", ", http.Requests.Select(r => r.Path)));
            Assert.AreEqual("/v1/guests", http.Requests[0].Path); Assert.AreEqual("/v1/parties", http.Requests[1].Path);
            Assert.IsTrue(http.Requests[2].Path.EndsWith("/ready")); Assert.IsTrue(http.Requests[3].Path.EndsWith("/queue"));
            StringAssert.StartsWith("v2;lists=freeplay;", savedChoices, "Find match remembers the playlists it used.");
            Draw(EventType.Layout); Draw(EventType.Repaint);
            CollectionAssert.AreEqual(new[] { "lobby-cancel" }, Controls(), "Only Cancel shows while searching.");
            StringAssert.StartsWith("Searching 0:", (string)Call(menu, "LobbySearchTitle"));
            Assert.IsNull(Call(menu, "LobbySearchProblem"));
            yield return Activate("lobby-cancel");
            Assert.IsTrue(http.Requests.Last().Path.EndsWith("/leave"), "Cancel cancels the search, then leaves the solo party.");
            Assert.IsNull(coordinator.Party); Assert.AreEqual(1, closed, "Cancel returns to the lobby.");
            Assert.IsTrue(WordboundQuickPlayEntry.TakeReturn(), "The lobby reopens on the Matchmaking tab.");
        }

        [UnityTest]
        public IEnumerator BackDuringTheSearchIsTheSameAsCancel()
        {
            menu.StartMatchmaking();
            Assert.IsTrue(menu.Searching);
            Invoke(menu, "PartyBack");
            yield return null;
            Assert.IsNull(coordinator.Party); Assert.AreEqual(1, closed);
        }

        [Test]
        public void TheRememberedNameReachesTheServiceAndTheRegionIsAlwaysAutomatic()
        {
            savedChoices = "v2;lists=freeplay;region=us;team=Duo;name=Ana";
            Set(menu, "quickChoices", null);
            menu.StartMatchmaking();
            Assert.AreEqual("asia", coordinator.Party.Settings.Region, "No region choice: the UTC+7 clock picks Asia.");
            Assert.AreEqual("Ana", OnlineJson.Parse(http.Requests[0].JsonBody)["displayName"].String());
            StringAssert.DoesNotContain("region", savedChoices);
        }

        [Test]
        public void AnOldServiceMessageDoesNotStopANewSearch()
        {
            typeof(WordboundPartyCoordinator).GetProperty("Error").SetValue(coordinator, "The party changed. Refresh and try again.");
            menu.StartMatchmaking();
            Assert.IsTrue(menu.Searching);
            Assert.IsNull(coordinator.Error);
        }

        [UnityTest]
        public IEnumerator AFailedStepShowsWhyAndBackReturnsToTheLobby()
        {
            http.FailNext = true;
            menu.StartMatchmaking();
            Assert.IsFalse(coordinator.Authenticated); Assert.IsFalse(Get<bool>(menu, "quickPlayArmed"));
            Assert.IsFalse(string.IsNullOrEmpty(coordinator.Error));
            Assert.AreEqual(1, http.Requests.Count, "Nothing more is sent after the failed step.");
            Draw(EventType.Layout); Draw(EventType.Repaint);
            CollectionAssert.AreEqual(new[] { "lobby-cancel" }, Controls());
            Assert.AreEqual("Matchmaking stopped", Call(menu, "LobbySearchTitle"));
            Assert.AreEqual(coordinator.Error, Call(menu, "LobbySearchProblem"));
            yield return Activate("lobby-cancel");
            Assert.AreEqual(1, http.Requests.Count, "With no party there is nothing to cancel or leave.");
            Assert.AreEqual(1, closed); Assert.IsTrue(WordboundQuickPlayEntry.TakeReturn());
        }

        [UnityTest]
        public IEnumerator APlaylistWithNoPublicMapExplainsWhyWithoutTalkingToTheService()
        {
            savedChoices = "v2;lists=relay-2v2";
            Set(menu, "quickChoices", null);
            menu.StartMatchmaking();
            Assert.IsEmpty(http.Requests);
            StringAssert.Contains("No public map is open", (string)Call(menu, "LobbySearchProblem"));
            yield return Activate("lobby-cancel");
            Assert.AreEqual(1, closed); Assert.IsEmpty(http.Requests);
        }

        [UnityTest]
        public IEnumerator TheOnlinePageIsThePrivateRoomPageAndBackReturnsToTheLobby()
        {
            Draw(EventType.Layout);
            var controls = Controls();
            Assert.Contains("connect", controls); Assert.Contains("back", controls);
            Assert.IsFalse(controls.Any(id => id == "play" || id == "play-options" || id == "more-options" || id == "lobby-cancel" ||
                id.StartsWith("qp-", StringComparison.Ordinal)), "The old Find match panel is gone.");
            yield return Activate("mode:4"); yield return Activate("create-settings");
            Draw(EventType.Layout);
            Assert.Contains("public-map:0", controls); Assert.IsFalse(controls.Contains("random-map"), "Shuffle stays gone.");
            Invoke(menu, "PartyBack"); Invoke(menu, "PartyBack");
            Assert.AreEqual(1, closed, "Back on the private room page returns to the lobby.");
            Assert.IsTrue(WordboundQuickPlayEntry.TakeReturn(), "The lobby reopens on the Matchmaking tab.");
            Assert.IsEmpty(http.Requests);
        }

        [Test]
        public void TheHomePlayRequestIsTakenOnceAndOtherEntriesCancelIt()
        {
            try
            {
                WordboundQuickPlayEntry.Request();
                Assert.IsTrue(WordboundQuickPlayEntry.TakeRequest()); Assert.IsFalse(WordboundQuickPlayEntry.TakeRequest());
                WordboundQuickPlayEntry.Request(); WordboundWorkshopEntry.Request();
                Assert.IsFalse(WordboundQuickPlayEntry.TakeRequest(), "Opening the Map editor cancels a pending search.");
                WordboundQuickPlayEntry.Request();
                Assert.IsFalse(WordboundWorkshopEntry.TakeRequest(), "Play cancels a pending Map editor entry.");
                WordboundQuickPlayEntry.Request(); WordboundSoloEntry.Request(WordboundSoloEntry.SelectedMode);
                Assert.IsFalse(WordboundQuickPlayEntry.TakeRequest(), "A solo course cancels a pending search.");
                WordboundQuickPlayEntry.MarkReturned();
                Assert.IsTrue(WordboundQuickPlayEntry.TakeReturn()); Assert.IsFalse(WordboundQuickPlayEntry.TakeReturn());
                WordboundQuickPlayEntry.MarkReturned(); WordboundQuickPlayEntry.CancelReturn();
                Assert.IsFalse(WordboundQuickPlayEntry.TakeReturn());
            }
            finally
            {
                WordboundQuickPlayEntry.CancelRequest(); WordboundQuickPlayEntry.CancelReturn();
                WordboundWorkshopEntry.CancelRequest(); WordboundSoloEntry.CancelRequest();
            }
        }

        [UnityTest]
        public IEnumerator HostingARoomMakesAPrivateRoomAndShowsTheCodeMembersAndStart()
        {
            menu.HostRoom("Freestyle", "Solo", 0);
            Assert.IsTrue(menu.InRoom); Assert.IsTrue(coordinator.Authenticated);
            Assert.AreEqual(4, http.Requests.Count, string.Join(", ", http.Requests.Select(r => r.Path)));
            Assert.AreEqual("/v1/guests", http.Requests[0].Path); Assert.AreEqual("/v1/parties", http.Requests[1].Path);
            Assert.IsTrue(http.Requests[2].Path.EndsWith("/ready"));
            var queued = OnlineJson.Parse(http.Requests[3].JsonBody);
            Assert.AreEqual("PrivateCreate", queued["channel"].String()); Assert.IsFalse(queued.Has("code"));
            var chosen = coordinator.Party.Settings;
            Assert.AreEqual("Freestyle", chosen.Mode); Assert.AreEqual("asia", chosen.Region, "The region comes from the UTC+7 clock.");
            Assert.IsFalse(chosen.Fill); Assert.AreEqual(Seed, coordinator.Seed);
            Draw(EventType.Layout); Draw(EventType.Repaint);
            CollectionAssert.AreEqual(new[] { "room-leave" }, Controls(), "Start waits for the room code.");
            Assert.AreEqual("Making your room…", Call(menu, "RoomStatus", new object[] { null }));
            Assert.IsNull(Call(menu, "RoomProblem"));
            assignment = Room(Player);
            coordinator.Poll().GetAwaiter().GetResult();
            Draw(EventType.Layout); Draw(EventType.Repaint);
            CollectionAssert.AreEqual(new[] { "room-copy", "room-leave", "room-start" }, Controls());
            Assert.AreEqual("room-copy", Get<WordboundMenuFocus>(menu, "partyFocus").Selected, "The pad lands on Copy code, never on Leave.");
            Assert.AreEqual("Free Roam · The Lost Address", Call(menu, "RoomTitle"), "The room names its course, not a layout number.");
            Assert.AreEqual("1 / 8 players · ready to start", Call(menu, "RoomStatus", Code));
            CollectionAssert.AreEqual(new[] { "You" }, (IEnumerable)Call(menu, "RoomMembers"));
            Assert.IsTrue((bool)Call(menu, "CanStartRoom", Code));
            string before = GUIUtility.systemCopyBuffer;
            try
            {
                yield return Activate("room-copy");
                string copied = GUIUtility.systemCopyBuffer;
                Assert.IsTrue(FriendRooms.LooksLikeInvite(copied), copied);
                var invite = new WordboundRoomInvite(copied);
                Assert.AreEqual(Code, invite.Code); Assert.AreEqual("Freestyle", invite.Mode); Assert.AreEqual(Seed, invite.Seed);
            }
            finally { GUIUtility.systemCopyBuffer = before; }
            yield return Activate("room-leave");
            Assert.IsTrue(http.Requests[http.Requests.Count - 2].Path.EndsWith("/cancel"));
            Assert.IsTrue(http.Requests.Last().Path.EndsWith("/leave"), "Leave gives up the room, then the solo party.");
            Assert.IsNull(coordinator.Party); Assert.IsFalse(menu.InRoom); Assert.AreEqual(1, closed);
            Assert.IsTrue(WordboundRoomEntry.TakeReturn(), "The lobby reopens on Custom match.");
            Assert.IsFalse(WordboundQuickPlayEntry.TakeReturn());
        }

        [UnityTest]
        public IEnumerator JoiningWithARoomCodeTakesTheRoomSettingsAndWaitsForTheHost()
        {
            string invite = WordboundRoomInvite.Encode(Code, coordinator.Settings("Freestyle", "Solo", false, "us"), Seed);
            menu.JoinFriendRoom(invite);
            Assert.IsTrue(menu.InRoom);
            var queued = OnlineJson.Parse(http.Requests.Last().JsonBody);
            Assert.AreEqual("PrivateJoin", queued["channel"].String()); Assert.AreEqual(Code, queued["code"].String());
            Assert.AreEqual("us", coordinator.Party.Settings.Region, "A guest uses the host's region, not their own clock.");
            Assert.IsFalse(coordinator.Party.Settings.Fill); Assert.AreEqual(Seed, coordinator.Seed);
            assignment = Room(Host, Player);
            coordinator.Poll().GetAwaiter().GetResult();
            Draw(EventType.Layout); Draw(EventType.Repaint);
            CollectionAssert.AreEqual(new[] { "room-copy", "room-leave" }, Controls(), "Guests never see Start.");
            CollectionAssert.AreEqual(new[] { "Host, You" }, (IEnumerable)Call(menu, "RoomMembers"));
            StringAssert.EndsWith("you're ready, the host starts", (string)Call(menu, "RoomStatus", Code));
            Assert.IsFalse((bool)Call(menu, "CanStartRoom", Code));
            Invoke(menu, "PartyBack");
            yield return null;
            Assert.IsNull(coordinator.Party); Assert.AreEqual(1, closed, "Back leaves the room like Leave.");
            Assert.IsTrue(WordboundRoomEntry.TakeReturn());
        }

        [UnityTest]
        public IEnumerator ABrokenRoomCodeExplainsItselfWithoutTalkingToTheService()
        {
            menu.JoinFriendRoom("wb-room:" + Code + ":NoSuchMode:Solo:asia:p1:" + Seed + ":pad-tests");
            Assert.IsTrue(menu.InRoom); Assert.IsEmpty(http.Requests);
            Draw(EventType.Layout); Draw(EventType.Repaint);
            CollectionAssert.AreEqual(new[] { "room-leave", "room-paste" }, Controls());
            Assert.AreEqual(WordboundPartyMenu.BadRoomCode, Call(menu, "RoomProblem"));
            yield return Activate("room-leave");
            Assert.IsEmpty(http.Requests, "With no party there is nothing to cancel or leave.");
            Assert.AreEqual(1, closed); Assert.IsTrue(WordboundRoomEntry.TakeReturn()); Assert.IsFalse(WordboundQuickPlayEntry.TakeReturn());
        }

        [UnityTest]
        public IEnumerator ABrokenRoomCodeOffersPastingTheCodeAgainAndJoinsWithTheNewCode()
        {
            string before = GUIUtility.systemCopyBuffer;
            try
            {
                menu.JoinFriendRoom("wb-room:" + Code + ":NoSuchMode:Solo:asia:p1:" + Seed + ":pad-tests");
                Draw(EventType.Layout); Draw(EventType.Repaint);
                Assert.AreEqual("room-paste", Get<WordboundMenuFocus>(menu, "partyFocus").Selected, "The pad lands on Paste the code again.");
                GUIUtility.systemCopyBuffer = WordboundRoomInvite.Encode(Code, coordinator.Settings("Freestyle", "Solo", false, "us"), Seed);
                yield return Activate("room-paste");
                Assert.IsTrue(menu.InRoom); Assert.AreEqual(0, closed);
                Assert.IsNotEmpty(http.Requests, "The pasted code joins like a fresh room code.");
                var queued = OnlineJson.Parse(http.Requests.Last().JsonBody);
                Assert.AreEqual("PrivateJoin", queued["channel"].String()); Assert.AreEqual(Code, queued["code"].String());
                Assert.AreNotEqual(WordboundPartyMenu.BadRoomCode, Call(menu, "RoomProblem"));
            }
            finally { GUIUtility.systemCopyBuffer = before; }
        }

        [UnityTest]
        public IEnumerator InviteFriendsMakesAPartyWithACodeAndOnlyTheLeaderFindsAMatch()
        {
            menu.InviteFriends();
            Assert.IsTrue(menu.InFriendsParty); Assert.IsFalse(menu.InRoom); Assert.IsTrue(coordinator.Authenticated);
            Assert.AreEqual(3, http.Requests.Count, string.Join(", ", http.Requests.Select(r => r.Path)));
            Assert.AreEqual("/v1/guests", http.Requests[0].Path); Assert.AreEqual("/v1/parties", http.Requests[1].Path);
            Assert.IsTrue(http.Requests[2].Path.EndsWith("/ready"));
            Assert.IsTrue(ready); Assert.IsNull(channel, "Nothing is queued until the leader presses Find match.");
            var chosen = coordinator.Party.Settings;
            Assert.AreEqual("Freestyle", chosen.Mode, "Free Roam has room for four friends."); Assert.AreEqual("Solo", chosen.Format);
            Assert.AreEqual("asia", chosen.Region); Assert.AreEqual(Seed, coordinator.Seed);
            Draw(EventType.Layout); Draw(EventType.Repaint);
            CollectionAssert.AreEqual(new[] { "party-copy", "party-leave", "party-find" }, Controls());
            Assert.AreEqual("party-copy", Get<WordboundMenuFocus>(menu, "partyFocus").Selected, "The pad lands on Copy code.");
            Assert.AreEqual("Searches Free Roam · 8", Call(menu, "FriendsPartyStatus"), "The party says what Find match will search.");
            Assert.IsNull(Call(menu, "FriendsPartyProblem"));
            string before = GUIUtility.systemCopyBuffer;
            try
            {
                yield return Activate("party-copy");
                string copied = GUIUtility.systemCopyBuffer;
                Assert.IsTrue(FriendRooms.LooksLikePartyInvite(copied), copied);
                Assert.IsFalse(FriendRooms.LooksLikeInvite(copied), "A party code is not a room code.");
                var invite = new WordboundPartyInvite(copied);
                Assert.AreEqual(PartyId, invite.PartyId); Assert.AreEqual(Code, invite.Invite); Assert.AreEqual(Seed, invite.Seed);
            }
            finally { GUIUtility.systemCopyBuffer = before; }
            yield return Activate("party-find");
            Assert.IsTrue(menu.Searching); Assert.AreEqual("QuickPlay", channel);
            Assert.IsTrue(http.Requests.Last().Path.EndsWith("/queue"));
            Draw(EventType.Layout); Draw(EventType.Repaint);
            CollectionAssert.AreEqual(new[] { "lobby-cancel" }, Controls(), "The leader's search uses the normal search bar.");
            yield return Activate("lobby-cancel");
            Assert.IsTrue(http.Requests.Last().Path.EndsWith("/cancel"), "Cancel stops the search and keeps the party.");
            Assert.IsNotNull(coordinator.Party); Assert.IsTrue(menu.InFriendsParty); Assert.IsFalse(menu.Searching);
            Assert.AreEqual(0, closed, "Cancel goes back to the party, not the lobby.");
            Draw(EventType.Layout); Draw(EventType.Repaint);
            CollectionAssert.AreEqual(new[] { "party-copy", "party-leave", "party-find" }, Controls());
            Assert.AreEqual("party-find", Get<WordboundMenuFocus>(menu, "partyFocus").Selected);
            yield return Activate("party-leave");
            Assert.IsTrue(http.Requests.Last().Path.EndsWith("/leave"));
            Assert.IsNull(coordinator.Party); Assert.IsFalse(menu.InFriendsParty); Assert.AreEqual(1, closed);
            Assert.IsTrue(WordboundQuickPlayEntry.TakeReturn(), "Leave reopens the lobby on the Matchmaking tab.");
            Assert.IsFalse(WordboundRoomEntry.TakeReturn());
        }

        [UnityTest]
        public IEnumerator JoiningAPartyReadiesYouAndWaitsForTheLeader()
        {
            leader = "10000000-0000-4000-8000-000000000002"; humans = 2;
            settings = coordinator.Settings("Freestyle", "Solo", true, "asia");
            menu.JoinFriendsParty("  " + WordboundPartyInvite.Encode(PartyId, Code, Seed) + " ");
            Assert.IsTrue(menu.InFriendsParty);
            Assert.AreEqual("/v1/party-joins", http.Requests.Last().Path);
            Assert.IsFalse(coordinator.IsLeader); Assert.IsNotNull(coordinator.Content); Assert.IsFalse(ready);
            Invoke(menu, "TickFriendsParty");
            Assert.IsTrue(ready, "A member is ready as soon as they join.");
            Assert.IsTrue(http.Requests.Last().Path.EndsWith("/ready"));
            int count = http.Requests.Count;
            Invoke(menu, "TickFriendsParty");
            Assert.AreEqual(count, http.Requests.Count, "Ready is sent once, not every frame.");
            Draw(EventType.Layout); Draw(EventType.Repaint);
            CollectionAssert.AreEqual(new[] { "party-copy", "party-leave" }, Controls(), "Only the leader sees Find match.");
            Assert.AreEqual(WordboundPartyMenu.WaitingForLeader, Call(menu, "FriendsPartyStatus"));
            Invoke(menu, "PartyBack");
            yield return null;
            Assert.IsNull(coordinator.Party); Assert.AreEqual(1, closed);
            Assert.IsTrue(WordboundQuickPlayEntry.TakeReturn()); Assert.IsFalse(WordboundRoomEntry.TakeReturn());
        }

        [Test]
        public void PartyMembersAreNumberedInJoinOrder()
        {
            leader = "10000000-0000-4000-8000-000000000002"; humans = 3;
            settings = coordinator.Settings("Freestyle", "Solo", true, "asia");
            menu.JoinFriendsParty(WordboundPartyInvite.Encode(PartyId, Code, Seed));
            var party = coordinator.Party;
            CollectionAssert.AreEqual(new[] { "You · not ready", "Player 2 · leader · not ready", "Player 3 · not ready" },
                party.Members.Select(member => (string)Call(menu, "MemberLine", party, member)).ToArray());
        }

        [UnityTest]
        public IEnumerator ABrokenPartyCodeExplainsItselfWithoutTalkingToTheService()
        {
            menu.JoinFriendsParty("wb-party:" + PartyId + ":short:" + Seed);
            Assert.IsTrue(menu.InFriendsParty); Assert.IsEmpty(http.Requests);
            Assert.AreEqual(WordboundPartyMenu.BadPartyCode, Call(menu, "FriendsPartyProblem"));
            Draw(EventType.Layout); Draw(EventType.Repaint);
            CollectionAssert.AreEqual(new[] { "party-leave" }, Controls());
            yield return Activate("party-leave");
            Assert.IsEmpty(http.Requests, "With no party there is nothing to leave.");
            Assert.AreEqual(1, closed); Assert.IsTrue(WordboundQuickPlayEntry.TakeReturn()); Assert.IsFalse(WordboundRoomEntry.TakeReturn());
        }

        [Test]
        public void APartyCountsDownFiveSecondsWhenTheLeadersSearchFindsAMatch()
        {
            float now = 30f;
            Set(menu, "searchClock", (Func<float>)(() => now));
            menu.InviteFriends();
            assignment = new Dictionary<string, object> { ["matchId"] = "30000000-0000-4000-8000-000000000002", ["state"] = "ADMITTING" };
            menu.StartMatchmaking();
            Assert.AreEqual("Match found · starting in 5", Call(menu, "LobbySearchTitle"));
            Assert.IsTrue((bool)Call(menu, "LaunchHeld"));
            now += 5f;
            Assert.IsFalse((bool)Call(menu, "LaunchHeld"));
        }

        [Test]
        public void PartyRequestsNeitherHostNorJoinARoom()
        {
            var start = WordboundRoomRequest.StartParty();
            Assert.IsTrue(start.InviteFriends); Assert.IsTrue(start.Party); Assert.IsFalse(start.Hosting); Assert.IsNull(start.Invite);
            var join = WordboundRoomRequest.JoinParty("  wb-party:x  ");
            Assert.AreEqual("wb-party:x", join.PartyInvite); Assert.IsTrue(join.Party); Assert.IsFalse(join.Hosting); Assert.IsNull(join.Invite);
            Assert.IsFalse(WordboundRoomRequest.Join("wb-room:x").Party);
        }

        [Test]
        public void AModeWithNoOpenMapSaysSoBeforeConnecting()
        {
            menu.HostRoom("TeamRace", "Duo", 2);
            Assert.IsEmpty(http.Requests);
            StringAssert.StartsWith("No map is open", (string)Call(menu, "RoomProblem"));
        }

        [Test]
        public void TheRoomRequestIsTakenOnceAndOtherEntriesCancelIt()
        {
            try
            {
                WordboundRoomEntry.Request(WordboundRoomRequest.Host("TeamRace", "Duo", 1));
                var taken = WordboundRoomEntry.TakeRequest();
                Assert.IsTrue(taken.Hosting); Assert.AreEqual("TeamRace", taken.Mode); Assert.AreEqual("Duo", taken.Format); Assert.AreEqual(1, taken.Layout);
                Assert.IsNull(WordboundRoomEntry.TakeRequest());
                WordboundRoomEntry.Request(WordboundRoomRequest.Join("  wb-room:x  "));
                Assert.AreEqual("wb-room:x", WordboundRoomEntry.TakeRequest().Invite);
                WordboundRoomEntry.Request(WordboundRoomRequest.HostCustomMap()); WordboundQuickPlayEntry.Request();
                Assert.IsNull(WordboundRoomEntry.TakeRequest(), "Find match cancels a pending room.");
                WordboundRoomEntry.Request(WordboundRoomRequest.HostCustomMap());
                Assert.IsFalse(WordboundQuickPlayEntry.TakeRequest(), "A room cancels a pending search.");
                Assert.IsTrue(WordboundRoomEntry.TakeRequest().CustomMap);
                WordboundRoomEntry.Request(WordboundRoomRequest.Host("Freestyle", "Solo", 0)); WordboundWorkshopEntry.Request();
                Assert.IsNull(WordboundRoomEntry.TakeRequest(), "The Map editor cancels a pending room.");
                WordboundRoomEntry.Request(WordboundRoomRequest.Host("Freestyle", "Solo", 0)); WordboundSoloEntry.Request(WordboundSoloEntry.SelectedMode);
                Assert.IsNull(WordboundRoomEntry.TakeRequest(), "A solo course cancels a pending room.");
                WordboundRoomEntry.MarkReturned();
                Assert.IsTrue(WordboundRoomEntry.TakeReturn()); Assert.IsFalse(WordboundRoomEntry.TakeReturn());
            }
            finally
            {
                WordboundRoomEntry.CancelRequest(); WordboundRoomEntry.CancelReturn();
                WordboundQuickPlayEntry.CancelRequest(); WordboundWorkshopEntry.CancelRequest(); WordboundSoloEntry.CancelRequest();
            }
        }

        [Test]
        public void TheSearchTimerCountsFromFindMatch()
        {
            float now = 100f;
            Set(menu, "searchClock", (Func<float>)(() => now));
            menu.StartMatchmaking();
            Assert.IsTrue(menu.Searching);
            now += 42.4f;
            Assert.AreEqual("Searching 0:42", Call(menu, "LobbySearchTitle"));
            Assert.IsTrue((bool)Call(menu, "LaunchHeld"), "Nothing launches before a match is found.");
        }

        [Test]
        public void AFoundMatchCountsDownFiveSecondsBeforeItStarts()
        {
            float now = 100f;
            Set(menu, "searchClock", (Func<float>)(() => now));
            assignment = new Dictionary<string, object> { ["matchId"] = "30000000-0000-4000-8000-000000000002", ["state"] = "ADMITTING" };
            menu.StartMatchmaking();
            Assert.IsTrue(menu.Searching);
            Assert.AreEqual(5, WordboundPartyMenu.StartCountdownSeconds);
            Assert.AreEqual("Match found · starting in 5", Call(menu, "LobbySearchTitle"));
            Assert.IsTrue((bool)Call(menu, "LaunchHeld"), "The launch waits for the countdown.");
            now += 2.2f;
            Assert.AreEqual("Match found · starting in 3", Call(menu, "LobbySearchTitle"));
            Assert.IsTrue((bool)Call(menu, "LaunchHeld"));
            now += 2.9f;
            Assert.IsFalse((bool)Call(menu, "LaunchHeld"));
            Assert.AreEqual("Joining match…", Call(menu, "LobbySearchTitle"));
            Draw(EventType.Layout); Draw(EventType.Repaint);
            CollectionAssert.AreEqual(new[] { "lobby-cancel" }, Controls(), "Cancel stays until the match has started.");
        }

        [Test]
        public void ARoomCountsDownAfterTheHostStarts()
        {
            float now = 50f;
            Set(menu, "searchClock", (Func<float>)(() => now));
            var room = Room(Player, Host);
            room["state"] = "ADMITTING";
            assignment = room;
            menu.HostRoom("Freestyle", "Solo", 0);
            Assert.AreEqual("Everyone's in · starting in 5", Call(menu, "RoomStatus", Code));
            Assert.IsFalse((bool)Call(menu, "CanStartRoom", Code), "Start is pressed once.");
            now += 5f;
            Assert.AreEqual("Joining match…", Call(menu, "RoomStatus", Code));
            Assert.IsFalse((bool)Call(menu, "LaunchHeld"));
        }

        [Test]
        public void ANewRoomCountsDownFromFiveAfterAnEarlierFoundMatch()
        {
            float now = 80f;
            Set(menu, "searchClock", (Func<float>)(() => now));
            Set(menu, "matchFoundAt", 10f);
            var room = Room(Player, Host);
            room["state"] = "ADMITTING";
            assignment = room;
            menu.HostRoom("Freestyle", "Solo", 0);
            Assert.AreEqual("Everyone's in · starting in 5", Call(menu, "RoomStatus", Code));
        }

        [Test]
        public void ANewSearchCountsDownFromFiveAfterAnEarlierFoundMatch()
        {
            float now = 80f;
            Set(menu, "searchClock", (Func<float>)(() => now));
            Set(menu, "matchFoundAt", 10f);
            assignment = new Dictionary<string, object> { ["matchId"] = "30000000-0000-4000-8000-000000000002", ["state"] = "ADMITTING" };
            menu.StartMatchmaking();
            Assert.AreEqual("Match found · starting in 5", Call(menu, "LobbySearchTitle"));
        }

        static Dictionary<string, object> Room(params string[] players) => new Dictionary<string, object> {
            ["matchId"] = "30000000-0000-4000-8000-000000000001", ["state"] = "FORMING", ["privateCode"] = Code,
            ["roster"] = players.Select((player, seat) => new Dictionary<string, object> {
                ["playerId"] = player, ["teamId"] = 0, ["seat"] = seat }).ToArray() };

        List<string> Controls() => Get<List<string>>(Get<WordboundMenuFocus>(menu, "partyFocus"), "controls");

        IEnumerator Classic()
        {
            Draw(EventType.Layout);
            yield break;
        }

        static object Call(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        [UnityTest]
        public IEnumerator RealPadButtonsConnectSelectCreateReadyAndQuickPlayOnlyAfterRepaint()
        {
            yield return Classic();
            yield return Activate("connect"); Assert.IsTrue(coordinator.Authenticated);
            yield return Activate("mode:4"); Assert.AreEqual(4, Get<int>(menu, "mode"));
            yield return Activate("create-settings");
            yield return Activate("region:1"); Assert.AreEqual(1, Get<int>(menu, "region"));
            Assert.IsTrue(Get<bool>(menu, "fill"), "A new public co-op choice finds human teammates by default.");
            yield return Activate("create-party"); Assert.IsNotNull(coordinator.Party);
            Assert.AreEqual("Freestyle", coordinator.Party.Settings.Mode); Assert.AreEqual("eu", coordinator.Party.Settings.Region);
            yield return Activate("ready"); Assert.IsTrue(coordinator.Party.Members.Single().Ready);
            yield return Activate("quick-play"); Assert.AreEqual("QuickPlay", coordinator.Party.Channel);
            Assert.IsTrue(http.Requests.Any(r => r.Path == "/v1/guests"));
            Assert.IsTrue(http.Requests.Any(r => r.Path.EndsWith("/queue") && OnlineJson.Parse(r.JsonBody)["channel"].String() == "QuickPlay"));
            Assert.Greater(window.Layouts, 0); Assert.Greater(window.Repaints, 0); Assert.IsNull(window.Failure);
        }

        [UnityTest]
        public IEnumerator RetryReusesPendingCreateAndPrivateQueueThenBackLeavesParty()
        {
            yield return Classic();
            yield return Activate("connect"); yield return Activate("mode:4");
            http.FailNext = true; yield return Activate("create-party");
            Assert.IsTrue(coordinator.RetryRequired);
            var first = http.Requests.Last();
            yield return Activate("retry");
            Assert.IsFalse(coordinator.RetryRequired); Assert.IsNotNull(coordinator.Party);
            Assert.AreEqual(first.IdempotencyKey, http.Requests.Last().IdempotencyKey);
            Assert.AreEqual(first.JsonBody, http.Requests.Last().JsonBody);
            yield return Activate("ready"); yield return Activate("private-actions"); yield return Activate("create-private");
            Assert.AreEqual("PrivateCreate", coordinator.Party.Channel);
            Sample(default); Sample(default); Sample(new GamepadState().WithButton(GamepadButton.East));
            Assert.IsNull(coordinator.Party); Assert.IsTrue(http.Requests.Last().Path.EndsWith("/leave"));
            Sample(default); Sample(new GamepadState().WithButton(GamepadButton.East));
            Assert.AreEqual(1, closed, "With no party, Back returns to the lobby.");
            Assert.IsTrue(WordboundQuickPlayEntry.TakeReturn());
        }

        [UnityTest]
        public IEnumerator EntryTabsRegisterOnlyVisibleActionsAndRoomSettingsReturnToCreate()
        {
            yield return Classic();
            yield return Activate("connect"); yield return Activate("mode:4");
            Draw(EventType.Layout);
            var controls = Get<List<string>>(Get<WordboundMenuFocus>(menu, "partyFocus"), "controls");
            Assert.Contains("create-party", controls); Assert.IsFalse(controls.Contains("join-party"));
            yield return Activate("entry:1"); Draw(EventType.Layout);
            Assert.Contains("join-party", controls); Assert.Contains("use-room-settings", controls);
            Assert.IsFalse(controls.Contains("create-party")); Assert.IsFalse(controls.Contains("mode:4"));
            Set(menu, "scroll", new Vector2(0, 900)); Set(menu, "revealPartySelection", false);
            Invoke(menu, "PartyBack");
            Assert.AreEqual(0, Get<int>(menu, "entryPage")); Assert.AreEqual(Vector2.zero, Get<Vector2>(menu, "scroll"));
            Assert.IsTrue(Get<bool>(menu, "revealPartySelection"));
            Assert.AreEqual("entry:0", Get<WordboundMenuFocus>(menu, "partyFocus").Selected);
            yield return Activate("entry:1");
            var requested = coordinator.Settings("Freestyle", "Solo", false, "eu");
            Set(menu, "privateCode", WordboundRoomInvite.Encode(Code, requested, Seed));
            int count = http.Requests.Count;
            yield return Activate("use-room-settings");
            Assert.AreEqual(count, http.Requests.Count, "Using invite settings must not create or queue a party.");
            Assert.AreEqual(0, Get<int>(menu, "entryPage"));
            Assert.AreEqual(4, Get<int>(menu, "mode")); Assert.AreEqual(1, Get<int>(menu, "region"));
            Assert.IsTrue(Get<bool>(menu, "customSeedMode")); Assert.AreEqual(Seed, Get<string>(menu, "seed"));
            Draw(EventType.Layout); Assert.Contains("create-party", controls);
            Assert.IsFalse(controls.Contains("join-party"));
        }

        [UnityTest]
        public IEnumerator CompactCreateResolvesPublicDefaultsAndPreservesFoldedPrivateSettings()
        {
            yield return Classic();
            yield return Activate("connect"); Draw(EventType.Layout);
            var controls = Get<List<string>>(Get<WordboundMenuFocus>(menu, "partyFocus"), "controls");
            Assert.Contains("create-settings", controls); Assert.IsFalse(controls.Contains("create-party"));
            yield return Activate("mode:4"); Set(menu, "seed", "ffffffffffffffff");
            int requests = http.Requests.Count;
            Draw(EventType.Layout); Draw(EventType.Repaint);
            Assert.AreEqual(Seed, Get<string>(menu, "seed"), "The closed form must resolve the same approved default map.");
            Assert.AreEqual(requests, http.Requests.Count); Assert.Contains("create-party", controls);
            Assert.Contains("mode:4", controls); Assert.IsFalse(controls.Contains("region:1"));
            Assert.Contains("fill", controls); Assert.IsFalse(controls.Contains("public-map:0"));
            yield return Activate("create-settings"); yield return Activate("region:1");
            yield return Activate("map-source:1"); yield return Activate("fill");
            const string privateSeed = "fedcba9876543210";
            Set(menu, "privateSeed", privateSeed); Draw(EventType.Layout); Draw(EventType.Repaint);
            Set(menu, "scroll", new Vector2(0, 900)); Set(menu, "revealPartySelection", false);
            Invoke(menu, "PartyBack");
            Assert.IsFalse(Get<bool>(menu, "showCreateSettings")); Assert.AreEqual(Vector2.zero, Get<Vector2>(menu, "scroll"));
            Assert.IsTrue(Get<bool>(menu, "revealPartySelection"));
            Assert.AreEqual("create-settings", Get<WordboundMenuFocus>(menu, "partyFocus").Selected);
            Draw(EventType.Layout); Assert.IsFalse(controls.Contains("region:1")); Assert.IsFalse(controls.Contains("new-seed"));
            Assert.AreEqual(privateSeed, Get<string>(menu, "seed")); Assert.AreEqual(1, Get<int>(menu, "region"));
            Assert.IsTrue(Get<bool>(menu, "fill"));
            yield return Activate("create-party");
            Assert.AreEqual(privateSeed, coordinator.Seed); Assert.AreEqual("eu", coordinator.Party.Settings.Region);
            Assert.IsTrue(coordinator.Party.Settings.Fill); Assert.AreEqual("Freestyle", coordinator.Party.Settings.Mode);
        }

        [UnityTest]
        public IEnumerator HiddenCreateSettingCannotDispatchAfterItsSectionCloses()
        {
            yield return Classic();
            yield return Activate("connect"); yield return Activate("mode:4"); yield return Activate("create-settings");
            int requests = http.Requests.Count;
            yield return QueueControl("region:1");
            Assert.AreEqual(0, Get<int>(menu, "region"));
            Set(menu, "showCreateSettings", false); Sample(default); Draw(EventType.Layout); Draw(EventType.Repaint);
            Assert.AreEqual(0, Get<int>(menu, "region")); Assert.AreEqual(requests, http.Requests.Count);
            Assert.IsNull(Get<Action>(menu, "pendingPartyAction"));
            var controls = Get<List<string>>(Get<WordboundMenuFocus>(menu, "partyFocus"), "controls");
            Assert.IsFalse(controls.Contains("region:1")); Assert.Contains("create-settings", controls);
        }

        [UnityTest]
        public IEnumerator HiddenPrivateActionCannotDispatchAfterItsPanelContextChanges()
        {
            yield return Classic();
            yield return Activate("connect"); yield return Activate("mode:4");
            yield return Activate("create-party"); yield return Activate("ready");
            Draw(EventType.Layout);
            var controls = Get<List<string>>(Get<WordboundMenuFocus>(menu, "partyFocus"), "controls");
            Assert.Contains("ready", controls); Assert.Contains("quick-play", controls);
            Assert.IsFalse(controls.Contains("create-private")); Assert.IsFalse(controls.Contains("mode:4"));
            yield return Activate("private-actions");
            int count = http.Requests.Count;
            yield return QueueControl("create-private");
            Assert.AreEqual(count, http.Requests.Count);
            Set(menu, "showPrivateActions", false);
            Sample(default); Draw(EventType.Layout); Draw(EventType.Repaint);
            Assert.AreEqual(count, http.Requests.Count);
            Assert.IsFalse(coordinator.Party.HasAttempt);
            Assert.IsNull(Get<Action>(menu, "pendingPartyAction"));
            Assert.IsFalse(controls.Contains("create-private"));
        }

        [UnityTest]
        public IEnumerator BackClosesOptionalPanelsBeforeLeavingIdlePartyAndCancelRemainsVisible()
        {
            yield return Classic();
            yield return Activate("connect"); yield return Activate("mode:4");
            yield return Activate("create-settings"); Assert.IsTrue(Get<bool>(menu, "fill"));
            yield return Activate("create-party");
            string party = coordinator.Party.Id;
            yield return Activate("party-settings");
            Set(menu, "scroll", new Vector2(0, 900)); Set(menu, "revealPartySelection", false);
            Invoke(menu, "PartyBack");
            Assert.IsTrue(Get<bool>(menu, "revealPartySelection"));
            Assert.AreEqual("party-settings", Get<WordboundMenuFocus>(menu, "partyFocus").Selected);
            Assert.IsFalse(Get<bool>(menu, "showPartySettings")); Assert.AreEqual(party, coordinator.Party.Id);
            yield return Activate("private-actions");
            Set(menu, "scroll", new Vector2(0, 900)); Set(menu, "revealPartySelection", false);
            Invoke(menu, "PartyBack");
            Assert.IsTrue(Get<bool>(menu, "revealPartySelection"));
            Assert.AreEqual("private-actions", Get<WordboundMenuFocus>(menu, "partyFocus").Selected);
            Assert.IsFalse(Get<bool>(menu, "showPrivateActions")); Assert.AreEqual(party, coordinator.Party.Id);
            yield return Activate("ready"); yield return Activate("quick-play");
            Draw(EventType.Layout);
            var controls = Get<List<string>>(Get<WordboundMenuFocus>(menu, "partyFocus"), "controls");
            Assert.Contains("cancel-queue", controls); Assert.IsFalse(controls.Contains("party-settings"));
            Assert.IsFalse(controls.Contains("private-actions"));
            yield return Activate("cancel-queue");
            Assert.IsFalse(coordinator.Party.HasAttempt);
            Assert.IsTrue(http.Requests.Last().Path.EndsWith("/cancel"));
            yield return Activate("leave-party");
            Assert.IsNull(coordinator.Party); Assert.IsTrue(http.Requests.Last().Path.EndsWith("/leave"));
        }

        [UnityTest]
        public IEnumerator FailedQueueKeepsRetryVisibleWithoutReopeningPrivateForms()
        {
            yield return Classic();
            yield return Activate("connect"); yield return Activate("mode:4");
            yield return Activate("create-settings"); Assert.IsTrue(Get<bool>(menu, "fill"));
            yield return Activate("create-party"); yield return Activate("ready");
            http.FailNext = true; yield return Activate("quick-play");
            Assert.IsTrue(coordinator.RetryRequired);
            var failed = http.Requests.Last();
            Draw(EventType.Layout); Draw(EventType.Repaint);
            var controls = Get<List<string>>(Get<WordboundMenuFocus>(menu, "partyFocus"), "controls");
            Assert.Contains("retry", controls); Assert.IsFalse(controls.Contains("create-private"));
            yield return Activate("retry");
            Assert.AreEqual(failed.IdempotencyKey, http.Requests.Last().IdempotencyKey);
            Assert.AreEqual(failed.JsonBody, http.Requests.Last().JsonBody);
            Assert.AreEqual("QuickPlay", coordinator.Party.Channel);
            Assert.IsFalse(coordinator.RetryRequired);
        }

        [Test]
        public void EditableBackdropIsBorrowedAndDoesNotMutateItsThemeOrGuiState()
        {
            var theme = ScriptableObject.CreateInstance<WordboundUiTheme>();
            var backdrop = new Texture2D(2, 2);
            try
            {
                theme.OnlineUseLobbyBackground = false; theme.OnlineBackground = backdrop;
                theme.OnlineBackgroundShade = .32f; menu.Theme = theme;
                string before = JsonUtility.ToJson(theme);
                Color colour = new Color(.2f, .6f, .8f, .9f);
                window.BeforeRepaint = () => GUI.color = colour;
                bool checkedState = false;
                window.AfterRepaint = () => { Assert.AreEqual(colour, GUI.color); checkedState = true; window.AfterRepaint = null; };
                Draw(EventType.Layout); Draw(EventType.Repaint);
                Assert.IsTrue(checkedState); Assert.AreEqual(before, JsonUtility.ToJson(theme));
                Assert.IsTrue(window.SkinRestored); Assert.IsEmpty(http.Requests);
                window.Menu = null; Invoke(menu, "OnDestroy");
                Assert.IsTrue(backdrop); Assert.IsTrue(theme);
            }
            finally { Object.DestroyImmediate(backdrop); Object.DestroyImmediate(theme); }
        }

        [Test]
        public void HumanCompositionCopyAgreesWithActualWordboundModeCapacities()
        {
            var describe = typeof(WordboundPartyMenu).GetMethod("Composition", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (string id in GetStatic<string[]>(typeof(WordboundPartyMenu), "Modes"))
                foreach (var format in id == "TeamRace" || id == "BrawlRace" ?
                    new[] { RaceFormat.Solo, RaceFormat.Duo, RaceFormat.Squad } : new[] { RaceFormat.Solo })
                {
                    var mode = ModeDefinition.For(id, format);
                    string copy = (string)describe.Invoke(null, new object[] { id, format.ToString() });
                    StringAssert.DoesNotContain("bot", copy.ToLowerInvariant());
                    if (mode.HasRaceFormat)
                    {
                        StringAssert.Contains(mode.TeamCount + " teams", copy);
                        StringAssert.Contains(mode.TeamCapacity + " per team", copy);
                    }
                    else StringAssert.Contains(mode.MaximumPlayers + " humans", copy);
                }
        }

        static T GetStatic<T>(Type type, string name) => (T)type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-.01f)]
        [TestCase(1.01f)]
        public void InvalidBackdropShadeIsRejected(float opacity)
        {
            var theme = ScriptableObject.CreateInstance<WordboundUiTheme>();
            try { theme.OnlineBackgroundShade = opacity; Assert.Throws<ArgumentException>(theme.Validate); }
            finally { Object.DestroyImmediate(theme); }
        }

        [Test]
        public void ActualPartyDrawUsesAssignedThemeAndRestoresTheBorrowedSkin()
        {
            var theme = ScriptableObject.CreateInstance<WordboundUiTheme>();
            try
            {
                theme.BodySize = 17; theme.RowHeight = 40; theme.ModuleHeight = 64;
                menu.Theme = theme;
                Draw(EventType.Layout); Draw(EventType.Repaint);
                var styles = Get<WordboundUiStyles>(menu, "ui");
                var owned = Get<GUISkin>(styles, "skin");
                var textures = new List<Texture2D>(Get<List<Texture2D>>(styles, "textures"));
                Assert.AreEqual(17, owned.label.fontSize);
                Assert.GreaterOrEqual(styles.RowHeight, 40);
                Assert.IsTrue(window.SkinRestored);
                Assert.IsEmpty(http.Requests, "Styling must not dispatch a party action.");
                window.Menu = null;
                Invoke(menu, "OnDestroy");
                Assert.IsFalse(owned);
                foreach (var texture in textures) Assert.IsFalse(texture);
                Assert.IsTrue(theme, "The designer's assigned asset is borrowed.");
            }
            finally { Object.DestroyImmediate(theme); }
        }

        [UnityTest]
        public IEnumerator FocusLossDiscardsActualQueuedConnectAndHeldConfirmNeedsNeutral()
        {
            yield return Classic();
            yield return QueueControl("connect");
            Invoke(menu, "ProcessPartyInput", false);
            Sample(new GamepadState().WithButton(GamepadButton.South));
            Draw(EventType.Layout); Draw(EventType.Repaint);
            Assert.IsEmpty(http.Requests); Assert.IsFalse(coordinator.Authenticated);
            Sample(default); Sample(default);
            yield return Activate("connect"); Assert.IsTrue(coordinator.Authenticated);
        }

        [TestCase(700)]
        [TestCase(420)]
        public void DisconnectedPartyCardFitsActualMeasuredContentWithoutChangingItDuringRepaint(int width)
        {
            window.position = new Rect(100, 100, width, 800);
            Rect before = StablePartyCard(width);
            float measured = Get<float>(menu, "guestHeight");
            Assert.AreEqual(width, window.position.width, 1, "The native window must use the requested test width.");
            var styles = Get<WordboundUiStyles>(menu, "ui");
            Assert.AreEqual(styles.MenuRect(window.position.width, window.position.height).width, before.width, 1,
                "The actual drawn card must use this window's viewport, not a wider shared Screen size.");
            Assert.Greater(measured, 0);
            Assert.IsTrue(Get<bool>(menu, "compactPartyLayout"));
            Assert.GreaterOrEqual(before.height, measured, "The actual drawn content must fit without hiding controls.");
            Assert.Less(before.height, window.position.height - 48, "The disconnected card should release its unused vertical space.");
            Rect repaintStart = default;
            int repaintSerial = -1;
            bool checkedRepaint = false;
            window.BeforeRepaint = () => {
                repaintStart = Get<Rect>(menu, "partyRect");
                repaintSerial = window.Repaints;
                Set(menu, "guestHeight", repaintStart.height + 20);
            };
            window.AfterRepaint = () => {
                Assert.AreEqual(repaintSerial, window.Repaints, "Compare within the same actual Repaint event.");
                Assert.AreEqual(repaintStart, Get<Rect>(menu, "partyRect"), "Measured height is applied only during Layout.");
                checkedRepaint = true;
                window.AfterRepaint = null;
            };
            Draw(EventType.Repaint);
            Assert.IsTrue(checkedRepaint, "The same-event guard must actually execute.");
            Assert.IsEmpty(http.Requests);
        }

        [Test]
        public void DisconnectedCardReflowsAfterResizeAndThemedErrorScrollThenShrinksWithoutClippingActions()
        {
            var theme = ScriptableObject.CreateInstance<WordboundUiTheme>();
            try
            {
                window.position = new Rect(100, 100, 420, 600);
                Rect narrow = StablePartyCard(420);
                window.position = new Rect(100, 100, 700, 600);
                Rect wide = StablePartyCard(700);
                Assert.LessOrEqual(wide.height, narrow.height);
                theme.BodySize = 18; theme.Padding = 20; theme.RowHeight = 42;
                menu.Theme = theme;
                window.position = new Rect(100, 100, 420, 600);
                Set(menu, "localError", string.Join(" ", Enumerable.Repeat("Connection could not finish. Please try again.", 30)));
                Rect overflow = StablePartyCard(420, true);
                window.SendEvent(new Event { type = EventType.ScrollWheel, mousePosition = overflow.center,
                    delta = new Vector2(0, 100) });
                Draw(EventType.Layout); Draw(EventType.Repaint);
                Assert.Greater(Get<Vector2>(menu, "scroll").y, 0, "Overflow must remain scrollable through the actual GUI event.");
                Set(menu, "localError", null);
                Rect smaller = StablePartyCard(420);
                Assert.Less(smaller.height, overflow.height, "Clearing content must release height rather than preserve a maximum.");
                Assert.AreEqual(Vector2.zero, Get<Vector2>(menu, "scroll"));
                Assert.IsEmpty(http.Requests);
                Assert.IsTrue(theme, "The assigned theme remains borrowed.");
            }
            finally { menu.Theme = null; Object.DestroyImmediate(theme); }
        }

        Rect StablePartyCard(int width, bool overflow = false)
        {
            var observations = new List<(int Layout, Vector4 Metrics)>();
            window.AfterRepaint = () => {
                Rect drawn = Get<Rect>(menu, "partyRect");
                if (observations.Count > 0 && observations[observations.Count - 1].Layout == window.Layouts)
                    observations.RemoveAt(observations.Count - 1);
                if (observations.Count == 16) observations.RemoveAt(0);
                observations.Add((window.Layouts, new Vector4(drawn.width, drawn.height, Get<float>(menu, "guestHeight"),
                    Get<float>(menu, "partyViewportHeight"))));
            };
            try { for (int i = 0; i < 12; i++) { Draw(EventType.Layout); Draw(EventType.Repaint); } }
            finally { window.AfterRepaint = null; }
            string trace = string.Join(" | ", observations.Select(value =>
                $"layout={value.Layout},width={value.Metrics.x},card={value.Metrics.y},measured={value.Metrics.z},scroll={value.Metrics.w}"));
            TestContext.WriteLine("WORDBOUND_PARTY_CARD_LAYOUT: requestedWidth=" + width + "; overflow=" + overflow + "; " + trace);
            Assert.GreaterOrEqual(observations.Count, 3, "Observe actual rendered layout convergence: " + trace);
            var styles = Get<WordboundUiStyles>(menu, "ui");
            Rect available = styles.MenuRect(window.position.width, window.position.height);
            Assert.AreEqual(width, window.position.width, 1);
            var settled = observations.Skip(observations.Count - 3).Select(value => value.Metrics).ToArray();
            foreach (var value in settled)
            {
                Assert.AreEqual(available.width, value.x, 1, "Draw the actual window viewport: " + trace);
                Assert.AreEqual(settled[0].y, value.y, .5f, "The card must stop changing height: " + trace);
                if (overflow)
                {
                    Assert.AreEqual(available.height, value.y, .5f, "Overflow stays inside the available viewport: " + trace);
                    Assert.Greater(value.z, value.y, "The control must exercise actual overflow: " + trace);
                }
                else Assert.AreEqual(value.z, value.y, .5f, "The settled card must fit its current measured content: " + trace);
            }
            Rect card = Get<Rect>(menu, "partyRect");
            if (!overflow)
            {
                foreach (string field in new[] { "guestFirstContentRect", "guestLastContentRect" })
                {
                    Rect child = Get<Rect>(menu, field);
                    Assert.Greater(child.height, 0, field);
                    Assert.GreaterOrEqual(child.xMin, styles.Panel.padding.left - .5f, field + " left edge");
                    Assert.GreaterOrEqual(child.yMin, styles.Panel.padding.top - .5f, field + " top edge");
                    Assert.LessOrEqual(child.xMax, card.width - styles.Panel.padding.right + .5f, field + " right edge");
                    Assert.LessOrEqual(child.yMax, card.height - styles.Panel.padding.bottom + .5f, field + " bottom edge");
                }
            }
            return card;
        }

        [Test]
        public void ExplicitMouseActionSurvivesChangingFromPadToDesktop()
        {
            Mouse previousMouse = Mouse.current, mouse = null;
            try
            {
                mouse = InputSystem.AddDevice<Mouse>();
                int actions = 0;
                window.BeforeRepaint = () => Invoke(menu, "QueuePartyAction", (Action)(() => actions++), false);
                Draw(EventType.Layout); Draw(EventType.Repaint);
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 }); WordboundNativeInputScope.Pump();
                Assert.IsTrue(mouse.leftButton.isPressed, "The pointer state must reach the input system before device handoff.");
                Invoke(menu, "ProcessPartyInput", true);
                Assert.AreEqual(1, actions);
                Assert.IsFalse(Get<bool>(menu, "partyUsesPad"));
                Invoke(menu, "ProcessPartyInput", true);
                Assert.AreEqual(1, actions);
            }
            finally
            {
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            }
        }

        [UnityTest]
        public IEnumerator NewInputBlockerDiscardsActualQueuedPadConnectWithTyping() => BlockQueuedConnect(false);

        [UnityTest]
        public IEnumerator NewInputBlockerDiscardsActualQueuedPadConnectWithGameplayLease() => BlockQueuedConnect(true);

        IEnumerator BlockQueuedConnect(bool gameplayLease)
        {
            IDisposable lease = null;
            try
            {
                yield return Classic();
                yield return QueueControl("connect");
                if (gameplayLease) lease = KeyBindings.BlockGameplay();
                else DesktopBinding.Typing = true;
                Invoke(menu, "ProcessPartyInput", true);
                Assert.IsNull(Get<Action>(menu, "pendingPartyAction"));
                Assert.IsEmpty(http.Requests);
                Assert.IsFalse(coordinator.Authenticated);
            }
            finally
            {
                lease?.Dispose();
                DesktopBinding.Typing = false;
            }
            if (gameplayLease) Assert.IsTrue(KeyBindings.GameplayBlocked, "Lease release intentionally suppresses input through the next engine frame.");
            Sample(new GamepadState().WithButton(GamepadButton.South));
            Draw(EventType.Layout); Draw(EventType.Repaint);
            Assert.IsEmpty(http.Requests);
            Sample(default); Sample(default);
            yield return Activate("connect"); Assert.IsTrue(coordinator.Authenticated);
        }

        IEnumerator Activate(string id)
        {
            int count = http.Requests.Count;
            yield return QueueControl(id);
            Assert.AreEqual(count, http.Requests.Count, "The real button handler must not execute during Repaint.");
            Sample(default);
            Assert.IsNull(window.Failure);
        }

        IEnumerator QueueControl(string id)
        {
            double deadline = EditorApplication.timeSinceStartup + 5;
            do
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
                Assert.Less(EditorApplication.timeSinceStartup, deadline, "Input capture did not release: " + InputState());
            } while (KeyBindings.GameplayBlocked);
            if (Get<WordboundInputBinding>(menu, "partyInput")?.ActiveGamepad != pad)
                Sample(new GamepadState { leftStick = new Vector2(.8f, 0) });
            Sample(default); Sample(default);
            Get<WordboundMenuFocus>(menu, "partyFocus").Reset(id);
            Draw(EventType.Layout);
            Assert.AreEqual(id, Get<WordboundMenuFocus>(menu, "partyFocus").Selected, "The intended enabled control must exist.");
            Draw(EventType.Repaint);
            Assert.AreEqual("party:" + id, window.FocusedControl, "The selected real control must receive rendered focus before confirmation: " + InputState());
            Assert.IsFalse(Get<bool>(menu, "partyTyping"), "A rendered button must not remain in text-entry mode.");
            Sample(default); Sample(default);
            Sample(new GamepadState().WithButton(GamepadButton.South));
            Draw(EventType.Repaint);
            Assert.IsNotNull(Get<Action>(menu, "pendingPartyAction"), "Confirmation must use the real drawn control: " + InputState());
        }

        string InputState() => "frame=" + Time.frameCount + ", quietUntil=" +
            typeof(KeyBindings).GetField("quietUntil", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) +
            ", blocked=" + KeyBindings.GameplayBlocked +
            ", typing=" + DesktopBinding.Typing + ", menuTyping=" + Get<bool>(menu, "partyTyping") +
            ", pad=" + Get<bool>(menu, "partyUsesPad") + ", selected=" + Get<WordboundMenuFocus>(menu, "partyFocus").Selected +
            ", focused=" + window.FocusedControl + ", layouts=" + window.Layouts + ", repaints=" + window.Repaints;

        void Sample(GamepadState state)
        {
            WordboundNativeInputScope.Sample(pad, state);
            Invoke(menu, "ProcessPartyInput", true);
        }
        void Draw(EventType type)
        {
            int before = type == EventType.Layout ? window.Layouts : window.Repaints;
            window.SendEvent(new Event { type = type });
            Assert.IsNull(window.Failure);
            Assert.Greater(type == EventType.Layout ? window.Layouts : window.Repaints, before,
                "EditorWindow did not process the requested " + type + ": " + InputState());
        }
        OnlineResponse Respond(OnlineRequest request)
        {
            if (request.Path == "/v1/guests") return Json(new Dictionary<string, object> {
                ["playerId"] = Player, ["accessToken"] = "test.token", ["refreshToken"] = Player + "." + OnlineJson.Base64Url(new byte[32]), ["expiresIn"] = 900
            });
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
            public readonly List<OnlineRequest> Requests = new List<OnlineRequest>();
            public Func<OnlineRequest, OnlineResponse> Handler;
            public bool FailNext;
            public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken cancellation)
            {
                Requests.Add(request);
                if (FailNext) { FailNext = false; return Task.FromException<OnlineResponse>(new IOException("Synthetic lost response")); }
                return Task.FromResult(Handler(request));
            }
        }
        static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static void Invoke(object target, string name, params object[] args)
        {
            try { target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        sealed class MenuWindow : EditorWindow
        {
            public WordboundPartyMenu Menu;
            public Exception Failure;
            public Action BeforeRepaint, AfterRepaint;
            public int Layouts, Repaints;
            public string FocusedControl;
            public bool SkinRestored = true;
            void OnGUI()
            {
                if (!Menu) return;
                bool enabled = GUI.enabled;
                var borrowedSkin = GUI.skin;
                var borrowedColor = GUI.color;
                try
                {
                    if (Event.current.type == EventType.Layout) Layouts++;
                    if (Event.current.type == EventType.Repaint) Repaints++;
                    Invoke(Menu, "BeginPartyGui", true);
                    if (Event.current.type == EventType.Repaint && BeforeRepaint != null)
                    { var action = BeforeRepaint; BeforeRepaint = null; action(); }
                    try { Invoke(Menu, "DrawPartyGui", position.size); }
                    finally { Invoke(Menu, "EndPartyGui"); FocusedControl = GUI.GetNameOfFocusedControl(); }
                    if (Event.current.type == EventType.Repaint) AfterRepaint?.Invoke();
                }
                catch (Exception error) { Failure = error; }
                finally { SkinRestored &= ReferenceEquals(borrowedSkin, GUI.skin); GUI.enabled = enabled; GUI.color = borrowedColor; }
            }
        }
    }
}
