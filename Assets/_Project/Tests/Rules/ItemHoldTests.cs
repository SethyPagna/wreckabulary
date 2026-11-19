using System;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class ItemHoldTests
    {
        static ItemCatalogue Live()
        {
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json"));
            return items;
        }

        static (float x, float y, float z) InBody(string id, float x, float y, float z)
        {
            var item = Live().Get(id);
            return (ItemHolds.Socket(item) * ItemHolds.InHand(item)).Rotate(x, y, z);
        }

        [Test]
        public void HoldsNameLiveItemsThatNoUseOrGuardPoseOverrides() => CollectionAssert.IsEmpty(ItemHolds.Validate(Live()));

        [Test]
        public void TheHandSocketsTipAnUnturnedItemBackTowardTheCamera()
        {
            var held = ItemHolds.OneHandSocket.Rotate(0f, 1f, 0f);
            Assert.Less(held.z, -.8f);
            Assert.Greater(held.y, .4f);
            var carried = ItemHolds.TwoHandSocket.Rotate(0f, 1f, 0f);
            Assert.Less(carried.z, -.95f, "a carried item lies on its back with its front to the sky");
        }

        [TestCase("PILLOW", false), TestCase("BEAR", false), TestCase("ARROW", false), TestCase("PAN", false)]
        [TestCase("BENCH", true), TestCase("CABINET", true), TestCase("SPEAKER", true)]
        [TestCase("BARREL", true), TestCase("BED", true), TestCase("CHAIR", true), TestCase("CRATE", true)]
        [TestCase("DESK", true), TestCase("SHELF", true), TestCase("SOFA", true), TestCase("STOOL", true), TestCase("TABLE", true)]
        public void EachHoldUsesTheSocketOfItsHandling(string id, bool twoHanded)
        {
            var item = Live().Get(id);
            Assert.AreEqual(twoHanded, item.IsTwoHanded);
            Assert.AreEqual(twoHanded ? ItemHolds.TwoHandSocket : ItemHolds.OneHandSocket, ItemHolds.Socket(item));
        }

        [TestCase("BAT"), TestCase("HAMMER"), TestCase("BROOM"), TestCase("SHIELD"), TestCase("WATER"), TestCase("LAMP"), TestCase("MAT")]
        public void UnlistedItemsKeepTheHandSocketAndTheirCatalogueGrip(string id)
        {
            var item = Live().Get(id);
            var turn = ItemHolds.InHand(item);
            Assert.AreEqual((0f, 0f, 0f, 1f), (turn.X, turn.Y, turn.Z, turn.W));
            Assert.AreSame(item.Grip, ItemHolds.Grip(item));
            Assert.IsNull(ItemHolds.For(id));
        }

        [TestCase("BENCH"), TestCase("CABINET"), TestCase("SPEAKER"), TestCase("BARREL"), TestCase("BED"), TestCase("CHAIR")]
        [TestCase("CRATE"), TestCase("DESK"), TestCase("SHELF"), TestCase("SOFA"), TestCase("STOOL"), TestCase("TABLE")]
        public void CarriedFurnitureStandsUprightInTheTwoHandedCarry(string id)
        {
            Assert.Greater(InBody(id, 0f, 1f, 0f).y, .95f, id + " stands on its feet in the carry");
        }

        [TestCase("SOFA"), TestCase("DESK"), TestCase("SHELF"), TestCase("CHAIR")]
        public void SeatsDrawersAndShelvesFaceTheCarrier(string id)
        {
            Assert.Less(InBody(id, 0f, 0f, 1f).z, -.95f, id + " shows its +Z front to the camera behind the carrier");
        }

        [TestCase("TABLE", 1f, 0f), TestCase("DESK", 1f, 0f), TestCase("SOFA", 1f, 0f), TestCase("SHELF", 1f, 0f), TestCase("BED", 0f, 1f)]
        public void LongFurnitureRunsAcrossTheBody(string id, float x, float z)
        {
            Assert.Greater(Math.Abs(InBody(id, x, 0f, z).x), .95f, id + " carries its long side across the chest");
        }

        [Test]
        public void BedShowsItsSideProfileToTheCamera() => Assert.Greater(Math.Abs(InBody("BED", 1f, 0f, 0f).z), .95f);

        [TestCase("BARREL"), TestCase("BED"), TestCase("CHAIR"), TestCase("CRATE"), TestCase("DESK")]
        [TestCase("PAN"), TestCase("SHELF"), TestCase("SOFA"), TestCase("STOOL"), TestCase("TABLE")]
        public void CarryTurnsLeaveTheGripAndThePlacedModelAsAuthored(string id)
        {
            var hold = ItemHolds.For(id);
            Assert.IsNull(hold.Grip, id + " keeps its catalogue grip");
            Assert.AreEqual(0f, hold.ModelYaw, id + " is placed facing the way it was put down");
            Assert.IsNull(hold.ModelShift, id);
        }

        [Test]
        public void PanRisesFromTheFistWithItsFaceTowardTheCamera()
        {
            var length = InBody("PAN", 1f, 0f, 0f);
            Assert.Greater(length.y, .9f, "the pan head rises out of the fist like a raised BAT");
            Assert.Less(length.z, 0f, "and leans back over the hand");
            Assert.Less(InBody("PAN", 0f, 1f, 0f).z, -.9f, "its cooking face, not its rim, is toward the camera behind");
        }

        [Test]
        public void BenchRunsAcrossTheBodyAndReachesPastTheRightHand()
        {
            Assert.Greater(InBody("BENCH", 1f, 0f, 0f).x, .95f);
            Assert.Less(ItemHolds.Grip(Live().Get("BENCH"))[0], 0f, "more of the bench reaches out past the right hand where the camera sees it");
        }

        [TestCase("CABINET"), TestCase("SPEAKER")]
        public void CabinetDoorsAndSpeakerConesFaceTheCarrier(string id)
        {
            var front = HoldTurn.Euler(0f, ItemHolds.For(id).ModelYaw, 0f).Rotate(0f, 0f, 1f);
            Assert.Less(InBody(id, front.x, front.y, front.z).z, -.95f, id + " shows its front to the third-person camera");
        }

        [Test]
        public void PlacedCabinetShowsItsDoorsToThePlacerAndFillsItsCoverFootprint()
        {
            var item = Live().Get("CABINET");
            var hold = ItemHolds.For("CABINET");
            var turn = HoldTurn.Euler(0f, hold.ModelYaw, 0f);
            Assert.Less(turn.Rotate(0f, 0f, 1f).z, -.99f, "the doors face back toward the player who set it down");
            Assert.AreEqual(0f, turn.Rotate(0f, 0f, item.Grip[2]).z + hold.ModelShift[2], 1e-4f, "the deeper door side no longer pokes out of the box collider");
            Assert.AreEqual(0f, ItemHolds.Grip(item)[2], "the hand holds the centred cabinet at its middle");
        }

        [Test]
        public void ArrowTipLeadsForwardAndUpReadyToThrow()
        {
            var tip = InBody("ARROW", 0f, 1f, 0f);
            Assert.Greater(tip.z, .5f, "the tip points where it will fly");
            Assert.Greater(tip.y, .5f, "raised so the camera behind sees its length");
        }

        [Test]
        public void PillowRisesFromTheFistWithItsFaceTowardTheCamera()
        {
            var length = InBody("PILLOW", 1f, 0f, 0f);
            Assert.Greater(length.y, .5f, "held by one end, the pillow rises out of the fist");
            Assert.Greater(length.x, .5f, "and leans away from the head");
            Assert.Greater(Math.Abs(InBody("PILLOW", 0f, 0f, 1f).z), .75f, "its broad face, not its edge, is toward the camera behind");
        }

        [Test]
        public void TeddyBearSitsUpAndLooksBackAtItsThrower()
        {
            Assert.Greater(InBody("BEAR", 0f, 1f, 0f).y, .99f);
            Assert.Less(InBody("BEAR", 0f, 0f, 1f).z, -.95f, "the teddy faces the prefab's +Z");
        }
    }
}
