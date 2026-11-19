using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class WordSolverTests
    {
        const string Csv = "word,category,hidden,notes\n" +
                           "AXE,Weapon,false,\n" +
                           "BLADE,Weapon,false,\n" +
                           "SWORD,Weapon,false,\n" +
                           "UMBRELLA,Defence,true,\n" +
                           "ZAP,Chaos,true,\n" +
                           "BEES,Chaos,false,swarm\n";

        static WordDatabase Db() => WordDatabase.FromCsv(Csv);

        [Test]
        public void ParsesCsvRows()
        {
            var db = Db();
            Assert.AreEqual(6, db.Words.Count);
            Assert.AreEqual(WordCategory.Chaos, db.Find("bees").category);
            Assert.IsTrue(db.Find("ZAP").hidden);
            Assert.AreEqual("swarm", db.Find("BEES").notes);
        }

        [Test]
        public void CanSpellNeedsEveryLetterIncludingRepeats()
        {
            Assert.IsTrue(WordSolver.CanSpell(WordSolver.Count("SEEB"), "BEES"));
            Assert.IsFalse(WordSolver.CanSpell(WordSolver.Count("SEB"), "BEES"));
        }

        [Test]
        public void MissingListsWhatIsLeft()
        {
            Assert.AreEqual("O", WordSolver.Missing(WordSolver.Count("SWRD"), "SWORD"));
        }

        [Test]
        public void SpellableOffersAnEmptyHandAWeaponFirst()
        {
            var words = WordSolver.Spellable(Db(), "ABLDEXZP");
            CollectionAssert.AreEqual(new[] { "AXE", "BLADE", "ZAP" }, words.Select(w => w.word).ToArray());
            var armed = WordSolver.Spellable(Db().Words, "ABLDEXZP", new SpellNeeds(true, false, false), null);
            CollectionAssert.AreEqual(new[] { "ZAP", "AXE", "BLADE" }, armed.Select(w => w.word).ToArray(), "with a weapon in hand the harder word leads");
        }

        [Test]
        public void DeployablesAreNeverTheQuickSpellWhileSomethingElseIsReady()
        {
            var words = GameConfig.Current.Items.Enabled.Select(i => new WordEntry { word = i.Id }).ToList();
            Assert.AreEqual("PAN", WordSolver.Spellable(words, "PLANT".ToCharArray().Concat("PAN")).First().word, "a newcomer gets a PAN, not a potted PLANT");
            Assert.AreEqual("FAN", WordSolver.Spellable(words, "FAN".ToCharArray()).First().word, "a deployable is offered when it is the only word ready");
            Assert.AreEqual(WordCategory.Deployable, UnityEngine.Resources.Load<GameAssets>("GameAssets").words.Find("SPEAKER").category);
            var checklist = new[] { "BED" };
            Assert.AreEqual("BED", WordSolver.Spellable(words, "BEDBAT".ToCharArray(), default, checklist).First().word, "a checklist word comes first");
        }

        [Test]
        public void HintsSkipHiddenAndTooLongWords()
        {
            var hints = WordSolver.Hints(Db().Words, "SWRDZA", capacity: 6);
            var words = hints.Select(h => h.entry.word).ToArray();
            CollectionAssert.Contains(words, "SWORD");
            CollectionAssert.DoesNotContain(words, "ZAP");
            Assert.AreEqual("O", hints.First(h => h.entry.word == "SWORD").missing);

            var umbrella = WordSolver.Hints(Db().Words, "UMBRELL", capacity: 6);
            CollectionAssert.IsEmpty(umbrella.Where(h => h.entry.word == "UMBRELLA"));
        }

        [Test]
        public void ShippedWordListParses()
        {
            var db = UnityEngine.Resources.Load<GameAssets>("GameAssets").words;
            Assert.AreEqual(GameConfig.Current.Items.Enabled.Count(), db.Words.Count);
            Assert.IsNotNull(db.Find("BLADE"));
        }
    }
}
