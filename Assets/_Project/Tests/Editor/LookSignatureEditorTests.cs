using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary.EditorTests
{
    [TestFixture]
    public class LookSignatureEditorTests
    {
        readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void CleanUp()
        {
            foreach (var root in spawned) if (root) Object.DestroyImmediate(root);
            spawned.Clear();
        }

        GameObject Avatar(out SkinnedMeshRenderer[] skins)
        {
            var root = new GameObject("Look signature test");
            spawned.Add(root);
            var model = ModelLibrary.Load().Spawn("Avatar/Avatar", root.transform);
            Assert.IsNotNull(model);
            skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            return model;
        }

        static Transform Under(Transform node)
        {
            for (var at = node.parent; at; at = at.parent)
                if (at.name == "head" || at.name == "chest") return at;
            return null;
        }

        [Test]
        public void EveryLookBelongsToARealLobbyTheme()
        {
            var themes = LobbyThemes.All.Select(theme => theme.Name).ToList();
            foreach (var look in CosmeticBundles.All) CollectionAssert.Contains(themes, look.Collection, look.Id);
            Assert.AreEqual(4, LobbyThemes.All.Count, "no theme was added for a look");
        }

        [Test]
        public void EveryLookHasItsOwnReadableEmblemPicture()
        {
            var seen = new HashSet<string>();
            foreach (var look in CosmeticBundles.All)
            {
                var emblem = Resources.Load<Texture2D>(look.Emblem);
                Assert.IsNotNull(emblem, look.Emblem);
                Assert.GreaterOrEqual(emblem.width, 64, look.Id);
                Assert.IsTrue(seen.Add(UnityEditor.AssetDatabase.GetAssetPath(emblem)), look.Id);
            }
        }

        [Test]
        public void DefaultSeatsReadApartAgainstTheRealPlayerColours()
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            var tops = Enumerable.Range(0, 4).Select(seat => PlayerAppearance.SeatDefault(wardrobe, seat, GameAssets.I.PlayerColor(seat)).ColourOf("Top")).ToList();
            Assert.AreEqual(4, tops.Distinct().Count(), string.Join(",", tops));
            for (int a = 0; a < 4; a++)
            {
                Assert.IsTrue(Career.FreeColours.Contains(tops[a]), tops[a]);
                var colour = Srgb.Of(wardrobe.Colour("Top", tops[a]));
                for (int b = 0; b < 4; b++)
                {
                    if (a == b) continue;
                    var seat = GameAssets.I.PlayerColor(b);
                    Assert.GreaterOrEqual(SeatLooks.DeltaE76(colour, Srgb.Of(wardrobe.Colour("Top", tops[b]))), SeatLooks.SeatGap, $"P{a + 1} vs P{b + 1}");
                    Assert.GreaterOrEqual(SeatLooks.DeltaE76(colour, new Srgb(seat.r, seat.g, seat.b)), SeatLooks.OtherSeatGap, $"P{a + 1} top vs P{b + 1} colour");
                }
            }
        }

        [Test]
        public void SignaturePiecesHangFromTheAvatarsBonesWhereTheyBelong()
        {
            Assert.IsNotNull(WardrobeRig.Art, "Resources/" + WardrobeRig.ArtPath);
            var wardrobe = GameConfig.Current.Wardrobe;
            foreach (var look in CosmeticBundles.All)
            {
                var model = Avatar(out var skins);
                var head = skins.First(s => s.name == "SK_Head").bounds;
                var tee = skins.First(s => s.name == "SK_Badge_T").bounds;
                PlayerAppearance.Dress(skins, look.Apply(wardrobe.Default, wardrobe));
                var rig = model.GetComponentInChildren<WardrobeRig>();
                Assert.IsNotNull(rig, look.Id);
                Assert.IsFalse(skins.First(s => s.name == "SK_Badge_T").enabled, look.Id + ": the emblem replaces the T");
                var badge = rig.NodeFor(wardrobe.Piece(look.Signature).Rigid);
                Assert.IsTrue(badge && badge.gameObject.activeInHierarchy, look.Id);
                Assert.AreEqual("chest", Under(badge)?.name, look.Id);
                var badgeBounds = BoundsOf(badge);
                Assert.Less(Vector3.Distance(badgeBounds.center, tee.center), .05f, look.Id + ": the emblem sits where the T was");
                foreach (string piece in look.Exclusive.Skip(1))
                {
                    var node = rig.NodeFor(wardrobe.Piece(piece).Rigid);
                    Assert.IsTrue(node && node.gameObject.activeInHierarchy, piece);
                    Assert.AreEqual(wardrobe.Piece(piece).Bone, Under(node)?.name, piece);
                    if (wardrobe.Piece(piece).Bone == "head")
                    {
                        var hat = BoundsOf(node);
                        Assert.Less(Vector3.Distance(new Vector3(hat.center.x, 0, hat.center.z), new Vector3(head.center.x, 0, head.center.z)), .08f, piece);
                        Assert.Greater(hat.max.y, head.max.y - .01f, piece + " sits on top of the head");
                    }
                }
                int active = rig.Attached.Count(id => !CuteWear.Handles(id) && rig.NodeFor(id).gameObject.activeInHierarchy);
                Assert.AreEqual(look.Exclusive.Count, active, look.Id + ": only this look's pieces show");
            }
        }

        [Test]
        public void TheLetterBadgeShowsTheSeatInitialOfflineAndOnlineKeepsTheT()
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            string[] initials = { "W", "O", "R", "D" };
            for (int seat = 0; seat < 4; seat++)
            {
                var model = Avatar(out var skins);
                PlayerAppearance.Dress(skins, PlayerAppearance.SeatDefault(wardrobe, seat, GameAssets.I.PlayerColor(seat)));
                var rig = model.GetComponentInChildren<WardrobeRig>();
                Assert.IsNotNull(rig);
                var badge = rig.NodeFor(wardrobe.Piece("TBadge").Rigid);
                Assert.IsTrue(badge.gameObject.activeInHierarchy);
                var glyphs = badge.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(WardrobeRig.GlyphPrefix)).ToList();
                Assert.AreEqual(26, glyphs.Count);
                CollectionAssert.AreEqual(new[] { WardrobeRig.GlyphPrefix + initials[seat] },
                    glyphs.Where(g => g.gameObject.activeInHierarchy).Select(g => g.name), "P" + (seat + 1));
                Assert.IsFalse(skins.First(s => s.name == "SK_Badge_T").enabled);
            }
            var picked = Avatar(out var pickedSkins);
            PlayerAppearance.Dress(pickedSkins, LetterBadge.With(wardrobe.Default, "Q"));
            var letter = picked.GetComponentInChildren<WardrobeRig>().NodeFor(wardrobe.Piece("TBadge").Rigid);
            Assert.IsTrue(ModelVisual.FindNamed(letter.gameObject, WardrobeRig.GlyphPrefix + "Q").gameObject.activeInHierarchy);
            Assert.IsFalse(ModelVisual.FindNamed(letter.gameObject, WardrobeRig.GlyphPrefix + "W").gameObject.activeInHierarchy);

            var online = Avatar(out var onlineSkins);
            var outfit = PlayerAppearance.DefaultPresentationOutfit(wardrobe);
            Assert.AreEqual(outfit.Serialize(), PlayerAppearance.Dress(onlineSkins, outfit, wardrobe).Serialize());
            Assert.IsTrue(onlineSkins.First(s => s.name == "SK_Badge_T").enabled, "online keeps the skinned T");
            Assert.IsNull(online.GetComponentInChildren<WardrobeRig>(), "online never loads the signature art");
        }

        [Test]
        public void TakingASignaturePieceOffHidesItAndTheMessengerStrapLiftsTheBadge()
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            var model = Avatar(out var skins);
            var courier = CosmeticBundles.Find("courier").Apply(wardrobe.Default, wardrobe);
            PlayerAppearance.Dress(skins, courier);
            var rig = model.GetComponentInChildren<WardrobeRig>();
            var stamp = rig.NodeFor("Sig_StampBadge");
            var lifted = stamp.localPosition;
            var satchel = wardrobe.Wear(courier, "Back", "Satchel");
            PlayerAppearance.Dress(skins, satchel);
            Assert.IsFalse(rig.NodeFor("Sig_MessengerBag").gameObject.activeSelf);
            Assert.IsTrue(skins.First(s => s.name == "SK_Satchel").enabled);
            Assert.AreEqual(.012f * 1f, Vector3.Distance(lifted, stamp.localPosition) * stamp.parent.lossyScale.x, .004f, "the badge rides 12 mm out on the strap");
            PlayerAppearance.Dress(skins, wardrobe.Wear(satchel, "Badge", null));
            Assert.AreEqual(0, rig.Attached.Count(id => !CuteWear.Handles(id) && rig.NodeFor(id).gameObject.activeSelf));
            Assert.IsFalse(skins.First(s => s.name == "SK_Badge_T").enabled);
        }

        static Bounds BoundsOf(Transform node)
        {
            var renderers = node.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
