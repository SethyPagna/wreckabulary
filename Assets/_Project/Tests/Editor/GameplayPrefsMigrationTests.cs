using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Wreckabulary.EditorTests
{
    public class GameplayPrefsMigrationTests
    {
        static readonly string[] Touched = GameplayPrefs.Keys.Concat(new[] { GameplayPrefs.OldDamageKey, GameplayPrefs.OldTipsKey }).ToArray();
        readonly Dictionary<string, int?> saved = new();

        [SetUp]
        public void Save()
        {
            foreach (string key in Touched) saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : null;
            foreach (string key in Touched) PlayerPrefs.DeleteKey(key);
        }

        [TearDown]
        public void Restore()
        {
            foreach (var pair in saved)
                if (pair.Value.HasValue) PlayerPrefs.SetInt(pair.Key, pair.Value.Value); else PlayerPrefs.DeleteKey(pair.Key);
            PlayerPrefs.Save();
        }

        [Test]
        public void SwitchedOffDamageNumbersAndTipsStayOffAfterTheKeysMove()
        {
            PlayerPrefs.SetInt(GameplayPrefs.OldDamageKey, 0);
            PlayerPrefs.SetInt(GameplayPrefs.OldTipsKey, 0);
            Assert.IsFalse(GameplayPrefs.DamageNumbers);
            Assert.IsFalse(ControlTips.Enabled);
            Assert.AreEqual(0, PlayerPrefs.GetInt(GameplayPrefs.DamageKey, 1));
            Assert.AreEqual(0, PlayerPrefs.GetInt(GameplayPrefs.TipsKey, 1));
            Assert.IsFalse(PlayerPrefs.HasKey(GameplayPrefs.OldDamageKey), "The old key is copied once, then dropped.");
            Assert.IsFalse(PlayerPrefs.HasKey(GameplayPrefs.OldTipsKey));
            Assert.IsTrue(GameplayPrefs.EffectWords, "Effect words start on.");
            Assert.IsFalse(GameplayPrefs.FurnitureNames, "Furniture names start off.");
        }

        [Test]
        public void FurnitureNamesStartOffButAnExplicitChoiceIsKept()
        {
            Assert.IsFalse(GameplayPrefs.FurnitureNames, "A player who never chose gets names off.");
            Assert.IsTrue(GameplayPrefs.DamageNumbers && GameplayPrefs.EffectWords && GameplayPrefs.ControlTips, "Only the names default changed.");
            Assert.IsFalse(GameplayPrefs.Default(GameplayPrefs.NamesKey));
            Assert.IsTrue(new[] { GameplayPrefs.DamageKey, GameplayPrefs.EffectsKey, GameplayPrefs.TipsKey }.All(GameplayPrefs.Default));
            PlayerPrefs.SetInt(GameplayPrefs.NamesKey, 1);
            GameplayPrefs.Migrate();
            Assert.IsTrue(GameplayPrefs.FurnitureNames, "A player who turned names on keeps them on.");
            Assert.AreEqual(1, PlayerPrefs.GetInt(GameplayPrefs.NamesKey, 0));
            PlayerPrefs.SetInt(GameplayPrefs.NamesKey, 0);
            Assert.IsFalse(GameplayPrefs.FurnitureNames, "A player who turned names off keeps them off.");
            GameplayPrefs.FurnitureNames = true;
            Assert.AreEqual(1, PlayerPrefs.GetInt(GameplayPrefs.NamesKey, 0));
            GameplayPrefs.ResetToDefaults(GameplayPrefs.NamesKey);
            Assert.IsFalse(PlayerPrefs.HasKey(GameplayPrefs.NamesKey));
            Assert.IsFalse(GameplayPrefs.FurnitureNames, "Reset returns to the new default.");
        }

        [Test]
        public void StartUpMigrationCopiesBothKeys()
        {
            PlayerPrefs.SetInt(GameplayPrefs.OldDamageKey, 0);
            PlayerPrefs.SetInt(GameplayPrefs.OldTipsKey, 1);
            GameplayPrefs.Migrate();
            Assert.AreEqual(0, PlayerPrefs.GetInt(GameplayPrefs.DamageKey, 1));
            Assert.AreEqual(1, PlayerPrefs.GetInt(GameplayPrefs.TipsKey, 0));
            Assert.IsFalse(PlayerPrefs.HasKey(GameplayPrefs.OldDamageKey) || PlayerPrefs.HasKey(GameplayPrefs.OldTipsKey));
        }

        [Test]
        public void ANewKeyWinsOverAnOldOne()
        {
            PlayerPrefs.SetInt(GameplayPrefs.DamageKey, 1);
            PlayerPrefs.SetInt(GameplayPrefs.OldDamageKey, 0);
            Assert.IsTrue(GameplayPrefs.DamageNumbers);
            Assert.IsFalse(PlayerPrefs.HasKey(GameplayPrefs.OldDamageKey));
        }

        [Test]
        public void ResetAndSetNeverBringAnOldValueBack()
        {
            PlayerPrefs.SetInt(GameplayPrefs.OldDamageKey, 0);
            GameplayPrefs.ResetToDefaults(GameplayPrefs.DamageKey);
            Assert.IsTrue(GameplayPrefs.DamageNumbers, "Reset means the default, not the old value.");
            PlayerPrefs.SetInt(GameplayPrefs.OldDamageKey, 0);
            GameplayPrefs.DamageNumbers = true;
            Assert.IsTrue(GameplayPrefs.DamageNumbers);
            Assert.IsFalse(PlayerPrefs.HasKey(GameplayPrefs.OldDamageKey));
        }
    }
}
