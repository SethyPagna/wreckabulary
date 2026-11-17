using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Voice;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class VoiceChannelTests
    {
        static readonly WordboundSeat[] Seats =
        {
            new("ann", 0, 0), new("bea", 0, 1), new("cal", 1, 0), new("dee", 1, 1)
        };

        static VoiceRoom Relay(string key = "match:m1", string region = "asia") =>
            VoiceRoom.Match(ModeDefinition.For("TeamRace", RaceFormat.Duo), Seats).On(new VoiceChannel(key, region));

        [Test]
        public void TeammatesShareARoomAndTheOtherTeamGetsItsOwn()
        {
            var room = Relay();
            string ann = VoiceChannel.RoomName(room, "ann"), bea = VoiceChannel.RoomName(room, "bea");
            string cal = VoiceChannel.RoomName(room, "cal"), dee = VoiceChannel.RoomName(room, "dee");
            Assert.AreEqual(ann, bea);
            Assert.AreEqual(cal, dee);
            Assert.AreNotEqual(ann, cal);
            Assert.AreEqual(2, VoiceChannel.Capacity(room, "ann"));
            StringAssert.StartsWith(VoiceChannel.RoomPrefix, ann);
            Assert.AreEqual(VoiceChannel.RoomPrefix.Length + VoiceChannel.RoomHashLength, ann.Length);
            Assert.IsTrue(ann.Substring(VoiceChannel.RoomPrefix.Length).All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')));
        }

        [Test]
        public void EveryoneRoomsPutTheWholePartyTogether()
        {
            var party = VoiceRoom.Party(new[] { "ann", "bea", "cal" }).On(new VoiceChannel("party:p1", "eu"));
            var names = new[] { "ann", "bea", "cal" }.Select(id => VoiceChannel.RoomName(party, id)).Distinct().ToArray();
            Assert.AreEqual(1, names.Length);
            Assert.AreEqual(3, VoiceChannel.Capacity(party, "bea"));
            Assert.AreEqual("eu", party.Channel.Region);
        }

        [Test]
        public void TheNameDependsOnTheChannelAndRosterButNotTheirOrder()
        {
            Assert.AreNotEqual(VoiceChannel.RoomName(Relay("match:m1"), "ann"), VoiceChannel.RoomName(Relay("match:m2"), "ann"));
            var a = VoiceRoom.Party(new[] { "ann", "bea" }).On(new VoiceChannel("party:p1", "us"));
            var b = VoiceRoom.Party(new[] { "bea", "ann" }).On(new VoiceChannel("party:p1", "us"));
            var c = VoiceRoom.Party(new[] { "ann", "bea", "cal" }).On(new VoiceChannel("party:p1", "us"));
            Assert.AreEqual(VoiceChannel.RoomName(a, "ann"), VoiceChannel.RoomName(b, "ann"));
            Assert.AreNotEqual(VoiceChannel.RoomName(a, "ann"), VoiceChannel.RoomName(c, "ann"));
        }

        [Test]
        public void NoChannelNoListenerOrABadRegionMeansNoRoom()
        {
            Assert.IsNull(VoiceChannel.RoomName(VoiceRoom.Match(ModeDefinition.For("TeamRace", RaceFormat.Duo), Seats), "ann"));
            Assert.IsNull(VoiceChannel.RoomName(VoiceRoom.Party(new[] { "ann" }).On(new VoiceChannel("party:p1", "asia")), "ann"));
            Assert.IsNull(VoiceChannel.RoomName(Relay(), "zed"));
            Assert.IsNull(VoiceChannel.RoomName(VoiceRoom.Empty, "ann"));
            foreach (string region in new[] { null, "", "Asia", "a", "toolongregion", "as-1" })
                Assert.IsFalse(new VoiceChannel("party:p1", region).IsSet, region ?? "null");
            Assert.IsFalse(new VoiceChannel(" ", "asia").IsSet);
            Assert.IsNull(VoiceChannel.RoomName(Relay(region: "Asia"), "ann"));
        }

        [Test]
        public void OnlyTeammatesInTheRosterAreAccepted()
        {
            var room = Relay();
            Assert.IsTrue(VoiceChannel.Accepts(room, "ann", "bea"));
            Assert.IsFalse(VoiceChannel.Accepts(room, "ann", "cal"));
            Assert.IsFalse(VoiceChannel.Accepts(room, "ann", "ann"));
            Assert.IsFalse(VoiceChannel.Accepts(room, "ann", "stranger"));
            Assert.IsFalse(VoiceChannel.Accepts(room, "ann", null));
            Assert.IsFalse(VoiceChannel.Accepts(null, "ann", "bea"));
        }

        [Test]
        public void ChannelsCarryOverWithoutChangingTheRoster()
        {
            var plain = VoiceRoom.Match(ModeDefinition.For("TeamRace", RaceFormat.Duo), Seats);
            var on = plain.On(new VoiceChannel("match:m1", "asia"));
            Assert.AreSame(VoiceChannel.None, plain.Channel);
            Assert.AreEqual(plain.Scope, on.Scope);
            CollectionAssert.AreEqual(plain.Members.Select(m => m.PlayerId), on.Members.Select(m => m.PlayerId));
            Assert.AreSame(VoiceChannel.None, plain.On(null).Channel);
            Assert.AreSame(VoiceChannel.None, VoiceRoom.Empty.Channel);
        }
    }
}
