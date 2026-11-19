using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    public sealed class TilePoolTests
    {
        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();
        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        static Transform Art(LetterTile tile) => tile.transform.Find("ImportedTile");

        [UnityTest]
        public IEnumerator ReleasedLettersKeepTheirArtForTheSameLetter()
        {
            var pool = TilePool.Ensure();
            var a = pool.Get('A');
            var b = pool.Get('B');
            var artA = Art(a);
            var artB = Art(b);
            Assert.IsNotNull(artA, "The generated imported art library must be available.");
            pool.Release(a);
            pool.Release(b);
            Assert.AreSame(a, pool.Get('a'), "A free tile already showing the letter is reused, even under a newer one.");
            Assert.AreSame(artA, Art(a), "Its art is kept, not rebuilt.");
            var c = pool.Get('C');
            Assert.AreNotSame(b, c, "A new letter takes a blank tile before rebuilding another letter's art.");
            Assert.AreSame(b, pool.Get('B'));
            Assert.AreSame(artB, Art(b));
            CollectionAssert.AreEqual(new[] { 'A', 'C', 'B' }, pool.Active.Select(t => t.Letter).ToArray(), "Active tiles keep the order they were taken in.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AReusedTileLooksLikeAFreshTileOfTheSameLetter()
        {
            char rare = Enumerable.Range('A', 26).Select(i => (char)i).First(c => LetterScores.RarityOf(c) == LetterRarity.Rare);
            var pool = TilePool.Ensure();
            var reused = pool.Get(rare);
            pool.Release(reused);
            pool.Get('E');
            Assert.AreSame(reused, pool.Get(rare));
            var fresh = pool.Get(rare);
            Assert.AreNotSame(reused, fresh);
            var collider = reused.GetComponent<BoxCollider>();
            var freshCollider = fresh.GetComponent<BoxCollider>();
            Assert.AreEqual(freshCollider.size, collider.size);
            Assert.AreEqual(freshCollider.center, collider.center);
            Assert.AreEqual(Art(fresh).localPosition, Art(reused).localPosition);
            Assert.AreEqual(Art(fresh).localRotation, Art(reused).localRotation);
            Assert.AreEqual(Art(fresh).localScale, Art(reused).localScale);
            Assert.AreEqual(Tints(fresh), Tints(reused), "Rare letters keep their tint.");
            Assert.IsNotEmpty(Tints(fresh));
            Assert.AreEqual(Labels(fresh), Labels(reused));
            yield return null;
        }

        static string[] Tints(LetterTile tile)
        {
            var block = new MaterialPropertyBlock();
            return Art(tile).GetComponentsInChildren<Renderer>().SelectMany(renderer =>
                Enumerable.Range(0, renderer.sharedMaterials.Length).Select(i =>
                {
                    renderer.GetPropertyBlock(block, i);
                    return block.isEmpty ? "" : block.GetColor("_BaseColor").ToString();
                })).Where(tint => tint.Length > 0).ToArray();
        }

        static string[] Labels(LetterTile tile) => tile.GetComponentsInChildren<TextMeshPro>(true)
            .Where(label => label.gameObject.activeSelf).Select(label => label.name + ":" + label.text).ToArray();
    }
}
