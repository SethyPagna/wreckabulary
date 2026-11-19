using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class GameplayPrefsTests
    {
        sealed class LocalInput : InputBinding
        {
            public override string Id => "gameplay-prefs-local";
            public override bool CanLook => true;
            public override void Read(ref PlayerCommands commands) { }
            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }

        static readonly string[] Switches = { "Damage numbers", "Effect words", "Furniture names", "Control tips" };
        static readonly string[] SavedKeys = GameplayPrefs.Keys.Concat(new[] { CombatFeedbackOptions.PointsKey, ControlTips.LearnedKey, ControlTips.SeenKey }).ToArray();
        readonly Dictionary<string, int?> saved = new();
        bool? focus;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (string key in SavedKeys) saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : null;
            focus = GameHud.PauseOnFocusLossOverride; GameHud.PauseOnFocusLossOverride = false;
            GameplayPrefs.ResetToDefaults();
            CombatFeedbackOptions.ShowPoints = false;
            yield return TestScenes.Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            foreach (var pair in saved)
                if (pair.Value.HasValue) PlayerPrefs.SetInt(pair.Key, pair.Value.Value); else PlayerPrefs.DeleteKey(pair.Key);
            PlayerPrefs.Save();
            GameHud.PauseOnFocusLossOverride = focus;
        }

        static string[] Popups() => Object.FindObjectsByType<Popup>(FindObjectsSortMode.None).Select(p => p.GetComponent<TMP_Text>().text).ToArray();
        static void ClearPopups() { foreach (var popup in Object.FindObjectsByType<Popup>(FindObjectsSortMode.None)) Object.DestroyImmediate(popup.gameObject); }

        [Test]
        public void TheFourSwitchesKeepTheirDefaultsAndAreSavedAndAnnounced()
        {
            CollectionAssert.AreEqual(new[] { "wv.pref.damage", "wv.pref.effects", "wv.pref.names", "wv.pref.tips" }, GameplayPrefs.Keys);
            Assert.IsTrue(GameplayPrefs.DamageNumbers && GameplayPrefs.EffectWords && GameplayPrefs.ControlTips);
            Assert.IsFalse(GameplayPrefs.FurnitureNames, "Furniture names start off in matches.");
            var heard = new List<string>();
            void Hear(string key) => heard.Add(key);
            GameplayPrefs.Changed += Hear;
            try
            {
                GameplayPrefs.EffectWords = false;
                Assert.AreEqual(0, PlayerPrefs.GetInt(GameplayPrefs.EffectsKey, 1));
                Assert.IsFalse(GameplayPrefs.EffectWords);
                Assert.IsTrue(GameplayPrefs.DamageNumbers && GameplayPrefs.ControlTips && !GameplayPrefs.FurnitureNames, "Each switch stands alone.");
                GameplayPrefs.DamageNumbers = false;
                Assert.IsFalse(CombatFeedbackOptions.ShowDamage, "The older damage switch reads the same setting.");
                ControlTips.Enabled = false;
                Assert.IsFalse(GameplayPrefs.ControlTips, "The older tips switch writes the same setting.");
                GameplayPrefs.FurnitureNames = false;
                CombatFeedbackOptions.ResetToDefaults();
                Assert.IsTrue(GameplayPrefs.DamageNumbers && GameplayPrefs.EffectWords, "Reset feedback restores damage numbers and effect words.");
                Assert.IsFalse(GameplayPrefs.FurnitureNames);
                GameplayPrefs.ResetToDefaults();
                Assert.IsFalse(GameplayPrefs.Keys.Any(PlayerPrefs.HasKey));
                CollectionAssert.AreEqual(new[]
                {
                    GameplayPrefs.EffectsKey, GameplayPrefs.DamageKey, GameplayPrefs.TipsKey, GameplayPrefs.NamesKey,
                    GameplayPrefs.DamageKey, GameplayPrefs.EffectsKey,
                    GameplayPrefs.DamageKey, GameplayPrefs.EffectsKey, GameplayPrefs.NamesKey, GameplayPrefs.TipsKey,
                }, heard);
            }
            finally { GameplayPrefs.Changed -= Hear; }
        }

        [Test]
        public void TheControlTipsSwitchSilencesTipsAndTurningItOnReplaysThem()
        {
            ControlTips.Learn(ControlTips.Tip.Move);
            Assert.AreEqual(ControlTips.Tip.Look, ControlTips.Next(true));
            GameplayPrefs.ControlTips = false;
            Assert.IsNull(ControlTips.Next(true), "No tips with the switch off.");
            GameplayPrefs.ControlTips = true;
            Assert.AreEqual(ControlTips.Tip.Move, ControlTips.Next(true), "Turning tips on from any menu replays them.");
        }

        [UnityTest]
        public IEnumerator AHitShowsJustTheNumberAndEachSwitchHidesOnlyItsOwnPopups()
        {
            yield return TestScenes.Load(Session.DibsScene);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var player = joins.Join(new LocalInput());
            var victim = joins.Join(new ScriptedBinding());
            var rules = Match.Rules.Clone(); rules.SpawnProtectionSeconds = 0;
            victim.Health.UseRules(rules);
            player.Frozen = victim.Frozen = true;
            yield return null;
            var hit = HitInfo.From(player.Index, player.Team, rules.Unarmed, null, 0, 1);

            ClearPopups();
            Assert.IsTrue(victim.Health.ApplyDamage(hit));
            CollectionAssert.AreEqual(new[] { HitText.Damage(rules.Unarmed.Damage) }, Popups(), "A hit shows just the number, no DMG.");
            var prop = new GameObject("Feedback prop", typeof(Rigidbody)).AddComponent<Smashable>(); prop.Init("BOX", 100);
            ClearPopups(); prop.TakeHit(12);
            CollectionAssert.AreEqual(new[] { "12" }, Popups());

            GameplayPrefs.DamageNumbers = false;
            ClearPopups(); victim.Health.ApplyDamage(hit); prop.TakeHit(12);
            Assert.IsEmpty(Popups(), "Damage numbers off hides every hit number.");

            victim.Health.RaisedShield = new ShieldStats { FrontArcDegrees = 360f, DamageReduction = 1f, MoveSpeedMultiplier = 1f };
            ClearPopups(); victim.Health.ApplyDamage(hit);
            CollectionAssert.AreEqual(new[] { "BLOCKED" }, Popups(), "Effect words keep their own switch.");
            GameplayPrefs.EffectWords = false;
            ClearPopups(); victim.Health.ApplyDamage(hit);
            Assert.IsEmpty(Popups(), "Effect words off hides the word.");

            GameplayPrefs.DamageNumbers = true;
            victim.Health.RaisedShield = null;
            ClearPopups(); victim.Health.ApplyDamage(hit);
            CollectionAssert.AreEqual(new[] { HitText.Damage(rules.Unarmed.Damage) }, Popups(), "Numbers come back on their own.");
        }

        [UnityTest]
        public IEnumerator PauseGameplayListsTheSwitchesUnderScreenShakeAndThePadWalksThem()
        {
            yield return TestScenes.Load(Session.DibsScene);
            var hud = Object.FindAnyObjectByType<GameHud>();
            hud.StateController.FadeDuration = 0;
            Button Find(string name) => hud.GetComponentsInChildren<Button>().Single(b => b.name == name);
            hud.TogglePause(); Find("Pause settings").onClick.Invoke(); Find("Pause tab gameplay").onClick.Invoke();

            var shake = Find("Screen shake less");
            CollectionAssert.AreEqual(new[] { "Screen shake" }.Concat(Switches).Append("Points per hit"),
                shake.transform.parent.parent.Cast<Transform>().Select(row => row.name).Take(6));
            Assert.AreSame(shake.gameObject, EventSystem.current.currentSelectedGameObject, "The pad starts on the first row.");
            Assert.AreSame(Find("Pause tab gameplay"), shake.FindSelectableOnUp());
            Assert.AreSame(Find("Screen shake more"), shake.FindSelectableOnRight());
            Selectable at = shake;
            foreach (string name in Switches)
            {
                var below = at.FindSelectableOnDown();
                Assert.AreEqual(name + " toggle", below ? below.name : null, "Down from " + at.name);
                Assert.AreSame(at, below.FindSelectableOnUp(), "Up from " + below.name);
                at = below;
            }
            Assert.AreEqual("Points per hit toggle", at.FindSelectableOnDown().name);
            yield return null;

            static string Start(string name) => name == "Furniture names" ? "OFF" : "ON";
            static string Flip(string state) => state == "ON" ? "OFF" : "ON";
            foreach (string name in Switches)
            {
                var label = Find(name + " toggle").GetComponentInChildren<TMP_Text>();
                Assert.AreEqual(Start(name), label.text, name);
                Find(name + " toggle").onClick.Invoke();
                Assert.AreEqual(Flip(Start(name)), label.text, name);
            }
            Assert.IsFalse(GameplayPrefs.DamageNumbers || GameplayPrefs.EffectWords || GameplayPrefs.ControlTips);
            Assert.IsTrue(GameplayPrefs.FurnitureNames, "Furniture names turn on from the pause menu.");
            Assert.IsTrue(GameplayPrefs.Keys.All(key => PlayerPrefs.GetInt(key, -1) == (key == GameplayPrefs.NamesKey ? 1 : 0)), "Each switch is saved.");
            Find("Settings back").onClick.Invoke(); Find("Pause settings").onClick.Invoke(); Find("Pause tab gameplay").onClick.Invoke();
            foreach (string name in Switches) Assert.AreEqual(Flip(Start(name)), Find(name + " toggle").GetComponentInChildren<TMP_Text>().text, name + " reopens as set");
            Assert.IsTrue(hud.Paused, "Changing settings never resumes the match.");
        }
    }
}
