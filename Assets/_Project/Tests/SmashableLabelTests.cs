using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    public class SmashableLabelTests
    {
        sealed class Roommate : InputBinding
        {
            public override string Id => "furniture-label-roommate";
            public override void Read(ref PlayerCommands commands) { }
            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }

        int? savedNames;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            savedNames = PlayerPrefs.HasKey(GameplayPrefs.NamesKey) ? PlayerPrefs.GetInt(GameplayPrefs.NamesKey) : null;
            GameplayPrefs.FurnitureNames = true;
            return TestScenes.Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Match.ModeOverride = null;
            if (savedNames.HasValue) PlayerPrefs.SetInt(GameplayPrefs.NamesKey, savedNames.Value); else PlayerPrefs.DeleteKey(GameplayPrefs.NamesKey);
            PlayerPrefs.Save();
            return TestScenes.Reset();
        }

        [Test]
        public void LabelsFadeInNearARoommateAndReachFurtherFromAbove()
        {
            Assert.AreEqual(1f, SmashableLabel.Visibility(false, 2f, 0f));
            Assert.AreEqual(1f, SmashableLabel.Visibility(false, 3.5f, 0f));
            Assert.AreEqual(.5f, SmashableLabel.Visibility(false, SmashableLabel.NearDistance, 0f), .001f);
            Assert.AreEqual(0f, SmashableLabel.Visibility(false, 4.5f, 0f));
            Assert.AreEqual(0f, SmashableLabel.Visibility(false, float.PositiveInfinity, 0f));
            Assert.AreEqual(1f, SmashableLabel.Visibility(true, 5f, .5f), "The overhead camera names furniture around each player on their storey.");
            Assert.AreEqual(.5f, SmashableLabel.Visibility(true, SmashableLabel.OverheadDistance, .5f), .001f);
            Assert.AreEqual(0f, SmashableLabel.Visibility(true, 30f, .5f), "Far furniture stays unlabelled so names never crowd the overhead view.");
            Assert.AreEqual(0f, SmashableLabel.Visibility(true, 2f, 3f), "Furniture on a hidden upper storey stays unlabelled.");
        }

        [Test]
        public void NamesThatWouldOverlapOnScreenCrowdEachOther()
        {
            const float line = 20f;
            var first = SmashableLabel.ScreenBox(new Vector2(400f, 300f), line, 5);
            var taken = new[] { first };
            Assert.IsTrue(SmashableLabel.Crowds(SmashableLabel.ScreenBox(new Vector2(440f, 300f), line, 4), taken), "SOFA beside TABLE overlaps it");
            Assert.IsTrue(SmashableLabel.Crowds(SmashableLabel.ScreenBox(new Vector2(400f, 320f), line, 4), taken), "one line above still overlaps");
            Assert.IsFalse(SmashableLabel.Crowds(SmashableLabel.ScreenBox(new Vector2(400f, 330f), line, 4), taken), "a clear line apart reads");
            Assert.IsFalse(SmashableLabel.Crowds(SmashableLabel.ScreenBox(new Vector2(520f, 300f), line, 4), taken), "a word's width apart reads");
        }

        [UnityTest]
        public IEnumerator OnlyTheNearerOfTwoTouchingNamesShowsFromAbove()
        {
            var camera = new GameObject("Overhead camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 18f, -13f);
            camera.transform.LookAt(Vector3.zero);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = Vector3.down * .5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            var near = FurnitureCatalog.Spawn("TABLE", new Vector3(1.5f, .05f, 0f), 0f, World.Transient);
            var far = FurnitureCatalog.Spawn("CHAIR", new Vector3(2.3f, .05f, 0f), 0f, World.Transient);
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, new Roommate());
            player.Respawn(Vector3.zero);
            player.Inventory.Collects = false;
            yield return new WaitForSeconds(.8f);
            var shown = near.GetComponent<SmashableLabel>();
            var hidden = far.GetComponent<SmashableLabel>();
            Assert.Greater(shown.Alpha, .9f, "the nearer name reads");
            Assert.IsTrue(hidden.Crowded, "the touching name yields");
            Assert.AreEqual(0f, hidden.Alpha);
            near.Break();
            yield return new WaitForSeconds(.5f);
            Assert.IsFalse(hidden.Crowded, "once the nearer piece is gone its neighbour is named");
            Assert.Greater(hidden.Alpha, .5f);
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator FurnitureNamesItselfUntilItBreaks()
        {
            var camera = new GameObject("Overhead camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 18f, -13f);
            camera.transform.LookAt(Vector3.zero);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = Vector3.down * .5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            var table = FurnitureCatalog.Spawn("TABLE", new Vector3(2f, .05f, 0f), 0f, World.Transient);
            yield return new WaitForSeconds(.3f);
            var label = table.GetComponent<SmashableLabel>();
            Assert.IsNotNull(label, "Smashable adds its own label at runtime.");
            Assert.IsTrue(!label.Text || !label.Text.gameObject.activeSelf, "No roommate, no label.");

            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, new Roommate());
            player.Respawn(Vector3.zero);
            player.Inventory.Collects = false;
            yield return new WaitForSeconds(.6f);
            Assert.IsNotNull(label.Text);
            Assert.IsTrue(label.Text.gameObject.activeSelf);
            Assert.AreEqual("TABLE", label.Text.text);
            Assert.Greater(label.Alpha, .9f);
            Assert.IsFalse(label.Text.transform.IsChildOf(table.transform), "The label lives outside the furniture's renderers.");
            float top = float.NegativeInfinity;
            foreach (var part in table.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, part.bounds.max.y);
            Assert.Greater(label.Text.transform.position.y, top, "The word floats above the furniture.");
            Assert.Less(Quaternion.Angle(label.Text.transform.rotation, camera.transform.rotation), 1f);

            var text = label.Text.gameObject;
            table.Break();
            yield return null;
            yield return null;
            Assert.IsTrue(!text, "Breaking the furniture removes its word.");
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator APulsedNameShowsInGoldOverItsNeighbourEvenWithNamesOff()
        {
            int? saved = PlayerPrefs.HasKey(GameplayPrefs.NamesKey) ? PlayerPrefs.GetInt(GameplayPrefs.NamesKey) : null;
            try
            {
                GameplayPrefs.FurnitureNames = true;
                var camera = new GameObject("Overhead camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0f, 18f, -13f);
                camera.transform.LookAt(Vector3.zero);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = Vector3.down * .5f;
                ground.transform.localScale = new Vector3(40f, 1f, 40f);
                var near = FurnitureCatalog.Spawn("TABLE", new Vector3(1.5f, .05f, 0f), 0f, World.Transient);
                var far = FurnitureCatalog.Spawn("CHAIR", new Vector3(2.3f, .05f, 0f), 0f, World.Transient);
                var distant = FurnitureCatalog.Spawn("SOFA", new Vector3(-9f, .05f, 0f), 0f, World.Transient);
                var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
                player.Setup(0, new Roommate());
                player.Respawn(Vector3.zero);
                player.Inventory.Collects = false;
                yield return new WaitForSeconds(.8f);
                var nearLabel = near.GetComponent<SmashableLabel>();
                var farLabel = far.GetComponent<SmashableLabel>();
                var distantLabel = distant.GetComponent<SmashableLabel>();
                Assert.IsTrue(farLabel.Crowded);
                SmashableLabel.Pulse = s => s == far || s == distant;
                yield return new WaitForSecondsRealtime(.6f);
                Assert.IsTrue(farLabel.Pulsing);
                Assert.IsFalse(farLabel.Crowded, "A pulsed name never yields");
                Assert.Greater(farLabel.Alpha, .9f);
                Assert.Greater(distantLabel.Alpha, .9f, "A pulsed name shows even far from every player");
                Assert.IsTrue(nearLabel.Crowded, "its neighbour yields to it instead");
                Assert.Less(farLabel.Text.color.b, .5f, "Pulsed names glow gold.");
                GameplayPrefs.FurnitureNames = false;
                yield return new WaitForSecondsRealtime(.4f);
                Assert.Greater(farLabel.Alpha, .9f, "The pulse is a hint, so it shows with names off.");
                Assert.AreEqual(0f, nearLabel.Alpha);
                SmashableLabel.Pulse = null;
                yield return new WaitForSecondsRealtime(.4f);
                Assert.IsFalse(farLabel.Pulsing);
                Assert.AreEqual(0f, farLabel.Alpha);
                Object.Destroy(player.gameObject);
            }
            finally
            {
                SmashableLabel.Pulse = null;
                if (saved.HasValue) PlayerPrefs.SetInt(GameplayPrefs.NamesKey, saved.Value); else PlayerPrefs.DeleteKey(GameplayPrefs.NamesKey);
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator NamesStayOffInMatchesButTheTutorialShowsThem()
        {
            PlayerPrefs.DeleteKey(GameplayPrefs.NamesKey);
            Assert.IsFalse(GameplayPrefs.FurnitureNames, "Names start off for a player who never chose.");
            var camera = new GameObject("Overhead camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 18f, -13f);
            camera.transform.LookAt(Vector3.zero);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = Vector3.down * .5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            var table = FurnitureCatalog.Spawn("TABLE", new Vector3(2.5f, .05f, 0f), 0f, World.Transient);
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, new Roommate());
            player.Respawn(Vector3.zero);
            player.Inventory.Collects = false;
            yield return null;
            var label = table.GetComponent<SmashableLabel>();
            Assert.IsNotNull(label, "Smashable adds its own label on its first frame.");
            foreach (string mode in new[] { Wreckabulary.Rules.HouseModes.Dibs, Wreckabulary.Rules.HouseModes.ShoppingList, Wreckabulary.Rules.HouseModes.MovingOut })
            {
                Match.ModeOverride = mode;
                yield return new WaitForSecondsRealtime(.4f);
                Assert.IsFalse(SmashableLabel.Named, mode);
                Assert.AreEqual(0f, label.Alpha, mode + " hides furniture names by default");
            }
            Match.ModeOverride = Wreckabulary.Rules.HouseModes.Tutorial;
            yield return new WaitForSecondsRealtime(.6f);
            Assert.IsTrue(SmashableLabel.Named);
            Assert.Greater(label.Alpha, .9f, "The Tutorial always names furniture: it teaches the rule.");
            Match.ModeOverride = Wreckabulary.Rules.HouseModes.Dibs;
            yield return new WaitForSecondsRealtime(.4f);
            Assert.AreEqual(0f, label.Alpha);
            GameplayPrefs.FurnitureNames = true;
            yield return new WaitForSecondsRealtime(.6f);
            Assert.Greater(label.Alpha, .9f, "The Settings switch still turns them on in a match.");
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator TheFurnitureNamesSwitchHidesEveryLabel()
        {
            int? saved = PlayerPrefs.HasKey(GameplayPrefs.NamesKey) ? PlayerPrefs.GetInt(GameplayPrefs.NamesKey) : null;
            try
            {
                GameplayPrefs.FurnitureNames = true;
                var camera = new GameObject("Overhead camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0f, 18f, -13f);
                camera.transform.LookAt(Vector3.zero);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = Vector3.down * .5f;
                ground.transform.localScale = new Vector3(40f, 1f, 40f);
                var table = FurnitureCatalog.Spawn("TABLE", new Vector3(2.5f, .05f, 0f), 0f, World.Transient);
                var chair = FurnitureCatalog.Spawn("CHAIR", new Vector3(-2.5f, .05f, 0f), 0f, World.Transient);
                var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
                player.Setup(0, new Roommate());
                player.Respawn(Vector3.zero);
                player.Inventory.Collects = false;
                yield return new WaitForSeconds(.6f);
                var labels = new[] { table.GetComponent<SmashableLabel>(), chair.GetComponent<SmashableLabel>() };
                foreach (var label in labels) Assert.Greater(label.Alpha, .9f, label.name + " reads with names on");
                GameplayPrefs.FurnitureNames = false;
                yield return new WaitForSecondsRealtime(.4f);
                foreach (var label in labels)
                {
                    Assert.AreEqual(0f, label.Alpha, label.name + " hides with names off");
                    Assert.IsTrue(!label.Text || !label.Text.gameObject.activeSelf);
                }
                GameplayPrefs.FurnitureNames = true;
                yield return new WaitForSecondsRealtime(.6f);
                foreach (var label in labels) Assert.Greater(label.Alpha, .9f, label.name + " comes back");
                Object.Destroy(player.gameObject);
            }
            finally
            {
                if (saved.HasValue) PlayerPrefs.SetInt(GameplayPrefs.NamesKey, saved.Value); else PlayerPrefs.DeleteKey(GameplayPrefs.NamesKey);
                PlayerPrefs.Save();
            }
        }
    }
}
