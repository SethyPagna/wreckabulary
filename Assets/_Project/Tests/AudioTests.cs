using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    /// <summary>The synthesised sounds and music are usable, and gameplay actually plays them.</summary>
    public class AudioTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            Sfx.History.Clear();
        }

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.Reset();

        static (float peak, float rms) Measure(AudioClip clip)
        {
            var data = new float[clip.samples];
            clip.GetData(data, 0);
            float peak = 0f, sum = 0f;
            foreach (var v in data)
            {
                Assert.IsFalse(float.IsNaN(v), $"{clip.name} has NaN samples");
                peak = Mathf.Max(peak, Mathf.Abs(v));
                sum += v * v;
            }
            return (peak, Mathf.Sqrt(sum / data.Length));
        }

        [Test]
        public void EverySoundIsAudibleAndDoesntClip()
        {
            foreach (Sound s in Enum.GetValues(typeof(Sound)))
            {
                var clip = Sfx.ClipFor(s);
                Assert.IsNotNull(clip, s.ToString());
                Assert.That(clip.length, Is.InRange(0.02f, 1.5f), $"{s} is a short effect");
                var (peak, rms) = Measure(clip);
                Assert.Greater(peak, 0.1f, $"{s} is audible");
                Assert.LessOrEqual(peak, 0.91f, $"{s} doesn't clip");
                Assert.Greater(rms, 0.01f, $"{s} isn't just a click");
            }
        }

        [Test]
        public void EveryMusicTrackLoopsCleanly()
        {
            foreach (Track t in Enum.GetValues(typeof(Track)))
            {
                var clip = Music.ClipFor(t);
                Assert.Greater(clip.length, 8f, $"{t} is a proper loop");
                var (peak, rms) = Measure(clip);
                Assert.LessOrEqual(peak, 0.81f, $"{t} leaves headroom for effects");
                Assert.Greater(rms, 0.03f, $"{t} isn't silent");

                // The loop point shouldn't pop: the last and first samples are close.
                var data = new float[clip.samples];
                clip.GetData(data, 0);
                Assert.Less(Mathf.Abs(data[^1] - data[0]), 0.25f, $"{t} loops without a click");
            }
        }

        [UnityTest]
        public IEnumerator ActionsMakeTheirSounds()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            TilePool.Ensure();
            var p = UnityEngine.Object.Instantiate(GameAssets.I.playerPrefab);
            p.Setup(0, new ScriptedBinding());
            p.Respawn(Vector3.zero);
            yield return null;

            p.Inventory.Set("BATX");
            p.Summoner.Open();
            p.Summoner.Add();
            p.Summoner.Add();
            p.Summoner.Add();
            p.Summoner.Cast();
            CollectionAssert.IsSubsetOf(new[] { Sound.SpellOpen, Sound.SpellAdd, Sound.Cast }, Sfx.History);

            p.Summoner.Open();
            p.Summoner.Add(); // X
            p.Summoner.Cast();
            CollectionAssert.Contains(Sfx.History, Sound.Fizzle);

            var box = DeliverySpawner.CreateBox("BOX", Vector3.forward * 3f);
            box.Break();
            CollectionAssert.Contains(Sfx.History, Sound.Smash);

            p.Inventory.Set("A");
            p.Health.TakeHit(Vector3.forward);
            CollectionAssert.Contains(Sfx.History, Sound.Hit);
            yield return new WaitForSeconds(0.7f);
            p.Inventory.Set(""); // it may have picked up letters from the smashed box meanwhile
            p.Health.TakeHit(Vector3.forward);
            CollectionAssert.Contains(Sfx.History, Sound.Knockout);
        }

        [UnityTest]
        public IEnumerator EachSceneStartsItsMusic()
        {
            yield return TestScenes.Load(Session.HubScene);
            Assert.AreEqual(Track.Cozy, Music.Playing);
            yield return TestScenes.Load(Session.DibsScene);
            Assert.AreEqual(Track.Brawl, Music.Playing);
            yield return TestScenes.Load(Session.MovingDayScene);
            Assert.AreEqual(Track.Bouncy, Music.Playing);
        }
    }
}
