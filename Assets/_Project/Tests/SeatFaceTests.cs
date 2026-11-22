using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public sealed class SeatFaceTests
    {
        const int Seats = 4, Humans = 2, SavedSeats = 8;
        const float Spacing = 2f, FramesToSettle = .5f, TuckDrop = .01f, HatSlack = .005f, PixelChange = .08f;
        const float WaitForPortrait = 5f, SunIntensity = 1.1f, LampIntensity = 4f, LampRange = 4f, FacingApart = 3f;
        const int FacePixels = 150, SameLightPixels = 300;
        const string HairSlot = "Hair", EyesSlot = "Eyes", HeadwearSlot = "Headwear", TopSlot = "Top";
        const string OldSave = "Back=Satchel:olive;Badge=TBadge;Bottoms=Joggers:olive;Footwear=Boots:forest;Gloves=Mittens:charcoal;Top=Hoodie:mint|";
        static readonly string[] Keys = Enumerable.Range(0, SavedSeats).Select(Key).ToArray();
        string[] saved;

        static string Key(int seat) => "wv.outfit." + seat;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            yield return TestScenes.Reset();
            foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = Vector3.down * .5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            for (int i = 0; i < Keys.Length; i++)
                if (saved[i] == null) PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetString(Keys[i], saved[i]);
            PlayerPrefs.Save();
        }

        static PlayerController Spawn(int seat, InputBinding binding) => Spawn(seat, binding, Vector3.right * seat * Spacing);

        static PlayerController Spawn(int seat, InputBinding binding, Vector3 at)
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, at, Quaternion.identity);
            player.Setup(seat, binding);
            player.Respawn(at);
            return player;
        }

        static CuteFace FaceOf(PlayerController player)
        {
            var model = player.GetComponent<PlayerAppearance>().AvatarModel;
            Assert.IsNotNull(model, player.Name + " has an avatar");
            var face = model.GetComponentInChildren<CuteFace>(true);
            Assert.IsNotNull(face, player.Name + " wears the redesigned face");
            return face;
        }

        static string HairStyle(WardrobeCatalogue wardrobe, string piece) => wardrobe.Piece(piece).Rigid.Substring(CuteWear.HairPrefix.Length);
        static string EyeStyle(WardrobeCatalogue wardrobe, string piece) => wardrobe.Piece(piece).Rigid.Substring(CuteWear.EyesPrefix.Length);

        static void AssertNewFace(PlayerController player, string hair, string eyes, string when)
        {
            string who = player.Name + " " + when;
            var model = player.GetComponent<PlayerAppearance>().AvatarModel;
            var face = FaceOf(player);
            Assert.IsTrue(face.Shown, who + ": the face is drawn");
            Assert.IsTrue(face.HidesOldFace, who + ": the old eyes and hair are not drawn under it");
            Assert.IsNotNull(face.ShownHair, who + ": has hair");
            Assert.IsTrue(face.ShownHair.gameObject.activeInHierarchy, who + ": the hair is shown");
            Assert.AreEqual(hair, face.ShownHair.Style, who + ": hair style");
            Assert.AreEqual(eyes, face.EyeStyle, who + ": eye style");
            Assert.AreEqual(1, model.GetComponentsInChildren<CuteHair>(false).Length, who + ": one head of hair, never two stacked");
            var built = model.GetComponentsInChildren<MeshRenderer>(true).Where(part => CuteFace.IsCodeBuilt(part.transform)).ToList();
            Assert.IsNotEmpty(built, who);
            foreach (var part in built)
                Assert.IsTrue(CuteShapes.OnRegularPath(part), who + ": " + part.name + " must be seen by a camera that moves it to another layer in the same frame");
        }

        [UnityTest]
        public IEnumerator EverySeatAndBotWearsTheNewFaceWithItsOwnHairAndEyes()
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            var players = new List<PlayerController>();
            for (int seat = 0; seat < Seats; seat++)
                players.Add(Spawn(seat, seat < Humans ? new ScriptedBinding() : new BotBinding()));
            var looks = players.Select(player => player.GetComponent<PlayerAppearance>().CurrentOutfit).ToList();
            foreach (var look in looks)
            {
                Assert.IsNotNull(look.PieceIn(HairSlot), "an unsaved seat is given hair");
                Assert.IsNotNull(look.PieceIn(EyesSlot), "an unsaved seat is given eyes");
                Assert.IsTrue(Career.FreePieces.Contains(look.PieceIn(HairSlot)) && Career.FreePieces.Contains(look.PieceIn(EyesSlot)), "only free pieces");
            }
            Assert.AreEqual(Seats, looks.Select(look => look.PieceIn(HairSlot)).Distinct().Count(), "every seat has its own hair");
            Assert.AreEqual(Seats, looks.Select(look => look.PieceIn(EyesSlot)).Distinct().Count(), "every seat has its own eyes");
            var tops = Enumerable.Range(0, Seats)
                .Select(seat => PlayerAppearance.SeatDefault(wardrobe, seat, GameAssets.I.PlayerColor(seat)).ColourOf(TopSlot)).ToList();
            CollectionAssert.AreEqual(tops, looks.Select(look => look.ColourOf(TopSlot)), "each seat keeps its own top colour");
            Assert.AreEqual(Seats, tops.Distinct().Count());
            for (int seat = 0; seat < Seats; seat++)
                AssertNewFace(players[seat], HairStyle(wardrobe, looks[seat].PieceIn(HairSlot)), EyeStyle(wardrobe, looks[seat].PieceIn(EyesSlot)), "in the frame it joins");
            yield return new WaitForSecondsRealtime(FramesToSettle);
            for (int seat = 0; seat < Seats; seat++)
                AssertNewFace(players[seat], HairStyle(wardrobe, looks[seat].PieceIn(HairSlot)), EyeStyle(wardrobe, looks[seat].PieceIn(EyesSlot)), "a few frames later");
            foreach (string key in Keys) Assert.IsFalse(PlayerPrefs.HasKey(key), key + ": a default look is not written as a saved one");
        }

        [UnityTest]
        public IEnumerator ASeatWithASavedLookKeepsItAndStillGetsTheNewFace()
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            var chosen = wardrobe.Wear(wardrobe.Wear(PlayerAppearance.DefaultPresentationOutfit(), HairSlot, "HairBuns"), EyesSlot, "EyesJelly");
            Assert.IsNotNull(chosen);
            PlayerPrefs.SetString(Key(1), OldSave);
            PlayerPrefs.SetString(Key(2), chosen.Serialize());
            var first = Spawn(0, new ScriptedBinding());
            var old = Spawn(1, new ScriptedBinding());
            var picked = Spawn(2, new BotBinding());
            var last = Spawn(3, new BotBinding());
            Assert.AreEqual(OldSave, old.GetComponent<PlayerAppearance>().CurrentOutfit.Serialize(), "a look saved before hair existed loads unchanged");
            Assert.AreEqual(chosen.Serialize(), picked.GetComponent<PlayerAppearance>().CurrentOutfit.Serialize());
            AssertNewFace(old, CuteFace.DefaultHair, CuteFace.DefaultEyes, "with an old save");
            AssertNewFace(picked, "Buns", "Jelly", "with a chosen look");
            var faces = new[] { first, old, picked, last }.Select(FaceOf).ToList();
            Assert.AreEqual(Seats, faces.Select(face => face.ShownHair.Style).Distinct().Count(), "seats without a look take hair nobody saved");
            Assert.AreEqual(Seats, faces.Select(face => face.EyeStyle).Distinct().Count(), "and eyes nobody saved");
            yield return new WaitForSecondsRealtime(FramesToSettle);
            AssertNewFace(old, CuteFace.DefaultHair, CuteFace.DefaultEyes, "with an old save, later");
            AssertNewFace(picked, "Buns", "Jelly", "with a chosen look, later");
            Assert.AreEqual(OldSave, PlayerPrefs.GetString(Key(1)), "the saved bytes are not rewritten");
            Assert.AreEqual(chosen.Serialize(), PlayerPrefs.GetString(Key(2)));
            Assert.IsFalse(PlayerPrefs.HasKey(Key(0)) || PlayerPrefs.HasKey(Key(3)), "a default look is never written as a saved one");
        }

        static float ShellTop(CuteHair hair) => hair.GetComponentInChildren<MeshFilter>(true).sharedMesh.bounds.max.y;

        [UnityTest]
        public IEnumerator HeadwearThatCoversTheHeadTucksTheHairUnderItInTheSameFrame()
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            var player = Spawn(0, new ScriptedBinding());
            yield return new WaitForSecondsRealtime(FramesToSettle);
            var appearance = player.GetComponent<PlayerAppearance>();
            var meshes = appearance.AvatarModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var face = FaceOf(player);
            var bare = wardrobe.Wear(appearance.CurrentOutfit, HeadwearSlot, null);
            Assert.IsNotNull(bare);
            int covering = 0, decorated = 0;
            foreach (var hair in wardrobe.PiecesFor(HairSlot))
            {
                var styled = wardrobe.Wear(bare, HairSlot, hair.Id);
                Assert.IsNotNull(styled, hair.Id);
                PlayerAppearance.Dress(meshes, styled, wardrobe, player.Index);
                Assert.AreEqual(HairStyle(wardrobe, hair.Id), face.ShownHair.Style);
                Assert.IsFalse(face.ShownHair.Tucked, hair.Id + " is full with nothing on the head");
                float fullTop = ShellTop(face.ShownHair);
                bool buns = face.ShownHair.BunsShown;
                foreach (var hat in wardrobe.PiecesFor(HeadwearSlot))
                {
                    var worn = wardrobe.Wear(styled, HeadwearSlot, hat.Id);
                    Assert.IsNotNull(worn, hat.Id + " fits the default clothes");
                    PlayerAppearance.Dress(meshes, worn, wardrobe, player.Index);
                    string what = hair.Id + " under " + hat.Id;
                    bool decor = CuteWear.IsHeadDecor(hat.Rigid);
                    Assert.AreEqual(!decor, face.ShownHair.Tucked, what + ": everything that is not an ear, bow, clip or sprout covers the head");
                    Assert.IsFalse(face.ShownHair.BunsShown, what + ": buns never poke through what is on the head");
                    if (decor) { decorated++; continue; }
                    covering++;
                    Assert.Less(ShellTop(face.ShownHair), fullTop - TuckDrop, what + ": the hair lies flat on top");
                    var skinned = meshes.FirstOrDefault(mesh => mesh.name == hat.Mesh);
                    if (!skinned || !string.IsNullOrEmpty(hat.Rigid)) continue;
                    Assert.IsTrue(skinned.enabled, what);
                    float hairTop = face.ShownHair.GetComponentInChildren<MeshRenderer>().bounds.max.y;
                    Assert.GreaterOrEqual(skinned.bounds.max.y, hairTop - HatSlack, what + ": the hat's top is over the hair's top");
                }
                PlayerAppearance.Dress(meshes, styled, wardrobe, player.Index);
                Assert.IsFalse(face.ShownHair.Tucked, hair.Id + " springs back when the hat comes off");
                Assert.AreEqual(buns, face.ShownHair.BunsShown, hair.Id);
            }
            Assert.Greater(covering, 0, "the wardrobe has hats that cover the head");
            Assert.Greater(decorated, 0, "and decorations that do not");
            foreach (string cover in CuteWear.HairCovers)
                Assert.IsTrue(wardrobe.Pieces.Any(piece => piece.Slot == HeadwearSlot && (piece.Mesh == cover || piece.Rigid == cover)), cover + " is still a hat in the wardrobe");
            PlayerAppearance.Dress(meshes, appearance.CurrentOutfit, wardrobe, player.Index);
        }

        static Color32[] Pixels(Texture texture)
        {
            var target = texture as RenderTexture;
            Assert.IsNotNull(target, "the portrait was drawn");
            var active = RenderTexture.active;
            var copy = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                copy.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                copy.Apply();
                return copy.GetPixels32();
            }
            finally
            {
                RenderTexture.active = active;
                Object.Destroy(copy);
            }
        }

        static int Changed(Color32[] a, Color32[] b)
        {
            Assert.AreEqual(a.Length, b.Length);
            int limit = Mathf.RoundToInt(PixelChange * 255f), count = 0;
            for (int i = 0; i < a.Length; i++)
                if (Mathf.Abs(a[i].r - b[i].r) > limit || Mathf.Abs(a[i].g - b[i].g) > limit || Mathf.Abs(a[i].b - b[i].b) > limit) count++;
            return count;
        }

        [UnityTest]
        public IEnumerator APortraitDrawnFramesAfterASeatJoinsStillShowsTheNewFace()
        {
            if (!SeatPortrait.CanRender) Assert.Ignore("Portraits need a native graphics device.");
            var player = Spawn(1, new BotBinding());
            var appearance = player.GetComponent<PlayerAppearance>();
            var portrait = SeatPortrait.For(player);
            yield return new WaitForSecondsRealtime(FramesToSettle);
            yield return TestScenes.WaitUntil(() => portrait.Wants, 5f, "the seat to be ready for its portrait");
            var with = Pixels(portrait.Refresh(true));
            var parts = appearance.AvatarModel.GetComponentsInChildren<MeshRenderer>(false).Where(part => CuteFace.IsCodeBuilt(part.transform)).ToList();
            Assert.IsNotEmpty(parts);
            Color32[] without;
            try
            {
                foreach (var part in parts) part.forceRenderingOff = true;
                appearance.ApplyOutfit(appearance.CurrentOutfit);
                yield return TestScenes.WaitUntil(() => portrait.Wants, WaitForPortrait, "a new outfit to ask for a new portrait");
                without = Pixels(portrait.Refresh(true));
            }
            finally
            {
                foreach (var part in parts) if (part) part.forceRenderingOff = false;
            }
            Assert.Greater(Changed(with, without), FacePixels,
                "the late portrait must contain the code-built eyes, blush and hair: hiding them has to change the picture");
        }

        [UnityTest]
        public IEnumerator ASeatNoCameraSeesIsDrawnInItsLivePose()
        {
            if (!SeatPortrait.CanRender) Assert.Ignore("Portraits need a native graphics device.");
            var player = Spawn(1, new BotBinding());
            var appearance = player.GetComponent<PlayerAppearance>();
            var portrait = SeatPortrait.For(player);
            var skins = appearance.AvatarModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.IsNotEmpty(skins);
            Assert.IsFalse(portrait.Wants, "the first portrait waits a frame so the skin, hood and clothes catch up with the bones");
            foreach (var skin in skins)
                Assert.IsTrue(skin.updateWhenOffscreen, skin.name + " keeps its pose while no camera sees it, like a bot in another room");
            yield return TestScenes.WaitUntil(() => portrait.Wants, WaitForPortrait, "the seat to be ready for its portrait");
            Assert.IsNotNull(portrait.Refresh(true), "the portrait is drawn");
            Assert.IsTrue(FaceOf(player).ShownHair.Tucked, "the hair is tucked under the hood when the picture is taken");
            int renders = portrait.Renders;
            appearance.ApplyOutfit(appearance.CurrentOutfit);
            Assert.IsFalse(portrait.Wants, "a new outfit also waits a frame before it is drawn");
            yield return TestScenes.WaitUntil(() => portrait.Wants, WaitForPortrait, "a new outfit to ask for a new portrait");
            portrait.Refresh(true);
            Assert.AreEqual(renders + 1, portrait.Renders, "and is then drawn once");
        }

        [UnityTest]
        public IEnumerator EverySeatIsLitTheSameWayWhicheverWayItFaces()
        {
            if (!SeatPortrait.CanRender) Assert.Ignore("Portraits need a native graphics device.");
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(Color.grey);
            RenderSettings.ambientProbe = ambient;
            var sun = new GameObject("Test sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = SunIntensity;
            sun.transform.rotation = Quaternion.LookRotation(Vector3.left + Vector3.down);
            RenderSettings.sun = sun;
            var lamp = new GameObject("Test lamp").AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.intensity = LampIntensity;
            lamp.range = LampRange;
            lamp.transform.position = Vector3.left * FacingApart + Vector3.up;
            var sunward = Spawn(1, new ScriptedBinding(), Vector3.left * FacingApart);
            var shaded = Spawn(1, new ScriptedBinding(), Vector3.right * FacingApart);
            Assert.Greater(Vector3.Dot(sunward.Facing, -sun.transform.forward), 0f, "one seat faces the sun and stands by the lamp");
            Assert.Less(Vector3.Dot(shaded.Facing, -sun.transform.forward), 0f, "the other has both behind it");
            var portraits = new[] { SeatPortrait.For(sunward), SeatPortrait.For(shaded) };
            yield return new WaitForSecondsRealtime(FramesToSettle);
            yield return TestScenes.WaitUntil(() => portraits.All(portrait => portrait.Wants), WaitForPortrait, "both seats to be ready for their portraits");
            var lit = Pixels(portraits[0].Refresh(true));
            var dark = Pixels(portraits[1].Refresh(true));
            Assert.Less(Changed(lit, dark), SameLightPixels, "a portrait has its own light, so the seat with the sun behind it looks the same");
            Assert.IsTrue(sun.enabled && lamp.enabled, "the house lights come back on after the portrait");
            Assert.AreSame(sun, RenderSettings.sun);
            Assert.IsFalse(SeatPortrait.KeyLight && SeatPortrait.KeyLight.enabled, "the portrait light only shines while a portrait is drawn");
        }
    }
}
