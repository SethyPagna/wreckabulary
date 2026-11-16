using System;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class SessionTests
    {
        [Test]
        public void AReconnectingPlayerGetsTheirSeatBack()
        {
            var roster = new PlayerRoster(4, 60);
            Assert.AreEqual(JoinResult.Joined, roster.Join("key-a", 0, 1, "Ana", out var ana));
            Assert.AreEqual(JoinResult.Joined, roster.Join("key-b", 0, 2, "Bo", out var bo));
            roster.JoiningOpen = false;

            var dropped = roster.Disconnect(1, 100);
            Assert.AreEqual(1, dropped.Count);
            Assert.AreEqual(SeatState.Disconnected, ana.State);

            Assert.AreEqual(JoinResult.Rejoined, roster.Join("key-a", 0, 7, "Ana", out var back));
            Assert.AreSame(ana, back);
            Assert.AreEqual(ana.PlayerId, back.PlayerId, "same player id, so the same letters and items");
            Assert.AreEqual(7UL, back.ClientId);
            Assert.AreEqual(SeatState.Connected, back.State);
            Assert.AreEqual(JoinResult.Closed, roster.Join("key-c", 0, 9, "Cy", out _), "no new players mid-round");
        }

        [Test]
        public void TheSeatIsKeptOnlyForTheGracePeriod()
        {
            var roster = new PlayerRoster(4, 60);
            roster.Join("key-a", 0, 1, "Ana", out var ana);
            roster.Disconnect(1, 100);
            Assert.AreEqual(0, roster.Expire(159.9).Count);
            var gone = roster.Expire(160);
            Assert.AreEqual(ana.PlayerId, gone.Single().PlayerId);
            Assert.AreEqual(SeatState.Left, ana.State);
            Assert.AreEqual(JoinResult.Joined, roster.Join("key-a", 0, 3, "Ana", out var fresh));
            Assert.AreNotEqual(ana.PlayerId, fresh.PlayerId, "player ids are never reused");
        }

        [Test]
        public void CouchPlayersShareAConnection()
        {
            var roster = new PlayerRoster(4, 60);
            roster.Join("pc-1", 0, 5, "P1", out var p1);
            roster.Join("pc-1", 1, 5, "P2", out var p2);
            Assert.AreNotEqual(p1.PlayerId, p2.PlayerId);
            Assert.AreEqual(JoinResult.AlreadyConnected, roster.Join("pc-1", 1, 5, "P2", out _));
            Assert.AreEqual(2, roster.Disconnect(5, 10).Count, "one dropped connection drops both seats");
        }

        [Test]
        public void TheRoomIsFullAtFour()
        {
            var roster = new PlayerRoster(4, 60);
            for (int i = 0; i < 4; i++) Assert.AreEqual(JoinResult.Joined, roster.Join("k" + i, 0, (ulong)i, null, out _));
            Assert.AreEqual(JoinResult.Full, roster.Join("k9", 0, 9, null, out _));
            roster.Leave(0);
            Assert.AreEqual(JoinResult.Joined, roster.Join("k9", 0, 9, null, out _));
            Assert.Throws<ArgumentException>(() => roster.Join("", 0, 1, null, out _));
        }
    }
}
