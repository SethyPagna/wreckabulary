using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Online;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundLetterRestTests
    {
        GameObject root;
        readonly RaycastHit[] hits = new RaycastHit[16];

        [SetUp]
        public void SetUp() => root = new GameObject("Letter rest test");

        [TearDown]
        public void TearDown() { if (root) Object.DestroyImmediate(root); }

        GameObject Box(Vector3 centre, Vector3 size, bool body)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.SetParent(root.transform, false);
            box.transform.position = centre;
            box.transform.localScale = size;
            if (body) box.AddComponent<Rigidbody>().isKinematic = true;
            return box;
        }

        float Rest(Vector3 body, HashSet<Rigidbody> ignored, bool expected = true)
        {
            Physics.SyncTransforms();
            Assert.AreEqual(expected, WordboundLetterRest.TryGround(Physics.defaultPhysicsScene, body, ignored, hits, out float ground));
            return ground;
        }

        [Test]
        public void StackedOrAirborneLettersRestOnTheFloorBeneathOtherLetters()
        {
            Box(new Vector3(0, -.5f, 0), new Vector3(10, 1, 10), false);
            var lower = Box(new Vector3(0, .06f, 0), new Vector3(.3f, .12f, .3f), true);
            var upper = Box(new Vector3(0, .18f, 0), new Vector3(.3f, .12f, .3f), true);
            var letters = new HashSet<Rigidbody> { lower.GetComponent<Rigidbody>(), upper.GetComponent<Rigidbody>() };
            Assert.AreEqual(0f, Rest(new Vector3(0, .12f, 0), letters), .001f, "the upper letter shows on the floor, not on the lower one");
            Assert.AreEqual(0f, Rest(new Vector3(0, 2f, 0), letters), .001f, "a letter still flying shows where it will land");
            Assert.AreEqual(0f, Rest(new Vector3(.5f, -.004f, 0), letters), .001f, "a letter sunk a few millimetres still finds its floor");
        }

        [Test]
        public void LettersStillRestOnFurnitureThatIsNotALetter()
        {
            Box(new Vector3(0, -.5f, 0), new Vector3(10, 1, 10), false);
            Box(new Vector3(1, .25f, 0), new Vector3(.6f, .5f, .6f), true);
            Assert.AreEqual(.5f, Rest(new Vector3(1, .5f, 0), new HashSet<Rigidbody>()), .001f);
        }

        [Test]
        public void WithoutASurfaceBelowTheBodyHeightIsKept()
        {
            Box(new Vector3(0, -10f, 0), new Vector3(10, 1, 10), false);
            Assert.AreEqual(1.25f, Rest(new Vector3(0, 1.25f, 0), new HashSet<Rigidbody>(), false), .0001f);
        }

        [TestCase(70f, 40f, 20f, 40f), TestCase(180f, 30f, 0f, 210f), TestCase(90f, 0f, 0f, 0f), TestCase(0f, -65f, 0f, -65f)]
        public void LettersLieFlatAndKeepOnlyTheirBodyYaw(float x, float y, float z, float yaw)
        {
            var flat = WordboundLetterRest.Flat(Quaternion.Euler(x, y, z));
            Assert.That(Quaternion.Angle(Quaternion.Euler(0, yaw, 0), flat), Is.LessThan(.05f));
            Assert.That((flat * Vector3.up).y, Is.GreaterThan(.9999f), "the letter face shows upward even when its body landed face down");
        }
    }
}
