using System;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class LastPlayChoiceTests
    {
        static readonly string[] Modes = { "Tutorial", "Dibs", "Duos", "MovingOut" };
        static readonly string[] Houses = { "pinwheel", "flat", "loft", "manor" };
        static readonly string[] Courses = { "TimeRun", "Freestyle" };
        static readonly string[] CourseMaps = { "ParcelService", "LostAddress", "ParcelHall" };
        const string Saved = "v1;tab=practice;mode=Duos;maps=flat+loft;course=TimeRun;courseMap=ParcelHall;shown=house;chosen=1";

        static LastPlayChoice Load(string saved) => LastPlayChoice.Load(saved, Modes, Houses, Courses, CourseMaps);

        [Test]
        public void TheKeyAndTheTextFormatArePinned()
        {
            Assert.AreEqual("wv.lobby.last", LastPlayChoice.StorageKey);
            var choice = new LastPlayChoice(LastPlayChoice.Practice, "Duos", new[] { "flat", "loft" }, "TimeRun", "ParcelHall", false, true);
            Assert.AreEqual(Saved, choice.Save());
            Assert.AreEqual("v1;tab=matchmaking;mode=Dibs;maps=;course=Freestyle;courseMap=LostAddress;shown=course;chosen=0",
                new LastPlayChoice(LastPlayChoice.Matchmaking, "Dibs", null, "Freestyle", "LostAddress", true, false).Save());
        }

        [Test]
        public void ASavedChoiceComesBackWithEveryPart()
        {
            var choice = Load(Saved);
            Assert.IsNotNull(choice);
            Assert.AreEqual(LastPlayChoice.Practice, choice.Tab);
            Assert.AreEqual("Duos", choice.Mode);
            Assert.AreEqual(new[] { "flat", "loft" }, choice.Houses);
            Assert.AreEqual("flat+loft", choice.Maps);
            Assert.AreEqual("TimeRun", choice.Course);
            Assert.AreEqual("ParcelHall", choice.CourseMap);
            Assert.IsFalse(choice.OnCourse);
            Assert.IsTrue(choice.Chosen);
            Assert.AreEqual(Saved, choice.Save());
            foreach (string tab in new[] { LastPlayChoice.Practice, LastPlayChoice.Matchmaking, LastPlayChoice.Workshop })
            {
                var other = new LastPlayChoice(tab, "MovingOut", new[] { "manor" }, "Freestyle", "LostAddress", true, false);
                var loaded = Load(other.Save());
                Assert.AreEqual(tab, loaded.Tab);
                Assert.AreEqual("MovingOut", loaded.Mode);
                Assert.AreEqual(new[] { "manor" }, loaded.Houses);
                Assert.AreEqual("Freestyle", loaded.Course);
                Assert.AreEqual("LostAddress", loaded.CourseMap);
                Assert.IsTrue(loaded.OnCourse);
                Assert.IsFalse(loaded.Chosen);
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("nonsense")]
        [TestCase("v0;tab=practice;mode=Duos;maps=flat;course=TimeRun;courseMap=ParcelHall;shown=house;chosen=1")]
        [TestCase("v2;tab=practice;mode=Duos;maps=flat;course=TimeRun;courseMap=ParcelHall;shown=house;chosen=1")]
        [TestCase("v1")]
        [TestCase("v1;tab=practice;mode=Duos;maps=flat;course=TimeRun;courseMap=ParcelHall;chosen=1")]
        [TestCase("v1;tab=practice;mode=Duos;maps=flat;course=TimeRun;courseMap=ParcelHall;shown=house")]
        [TestCase("v1;tab=practice;mode=Duos;maps=flat;course=TimeRun;courseMap=ParcelHall;shown=house;chosen=yes")]
        [TestCase("v1;tab=practice;mode=Duos;course=TimeRun;courseMap=ParcelHall;shown=house;chosen=1")]
        [TestCase("v1;tab=practice;tab=workshop;mode=Duos;maps=flat;course=TimeRun;courseMap=ParcelHall;shown=house;chosen=1")]
        [TestCase("v1;tab=online;mode=Duos;maps=flat;course=TimeRun;courseMap=ParcelHall;shown=house;chosen=1")]
        [TestCase("v1;tab=practice;mode=Workshop;maps=flat;course=TimeRun;courseMap=ParcelHall;shown=house;chosen=1")]
        [TestCase("v1;tab=practice;mode=;maps=flat;course=TimeRun;courseMap=ParcelHall;shown=house;chosen=1")]
        [TestCase("v1;tab=practice;mode=Duos;maps=flat;course=TeamRace;courseMap=ParcelHall;shown=house;chosen=1")]
        [TestCase("v1;tab=practice;mode=Duos;maps=flat;course=TimeRun;courseMap=flat;shown=house;chosen=1")]
        [TestCase("v1;tab=practice;mode=Duos;maps=flat;course=TimeRun;courseMap=ParcelHall;shown=both;chosen=1")]
        [TestCase("v1;tab=practice;mode=Duos;maps=flat++loft;course=TimeRun;courseMap=ParcelHall;shown=house;chosen=1")]
        [TestCase("v1;tab=practice;mode=Duos;maps=flat+flat;course=TimeRun;courseMap=ParcelHall;shown=house;chosen=1")]
        [TestCase("v1;tab=practice;mode=Duos;maps=+;course=TimeRun;courseMap=ParcelHall;shown=house;chosen=1")]
        [TestCase("v1;tab=practice;mode=Duos;maps=fl at;course=TimeRun;courseMap=ParcelHall;shown=house;chosen=1")]
        [TestCase("v1;broken;tab=practice;mode=Duos;maps=flat;course=TimeRun;courseMap=ParcelHall;shown=house;chosen=1")]
        [TestCase("v1;=x;tab=practice;mode=Duos;maps=flat;course=TimeRun;courseMap=ParcelHall;shown=house;chosen=1")]
        public void AnOldOrMalformedValueRemembersNothing(string saved) => Assert.IsNull(Load(saved));

        [Test]
        public void HousesFollowTheInstalledListAndUnknownOnesAreDropped()
        {
            Assert.AreEqual(new[] { "flat", "manor" }, Load(Saved.Replace("maps=flat+loft", "maps=manor+castle+flat")).Houses,
                "Ticked houses keep the installed order and lose a house that is gone.");
            Assert.IsEmpty(Load(Saved.Replace("maps=flat+loft", "maps=")).Houses, "No list means every house.");
            Assert.IsEmpty(Load(Saved.Replace("maps=flat+loft", "maps=castle+moat")).Houses, "A list with nothing installed means every house.");
            Assert.IsEmpty(Load(Saved.Replace("maps=flat+loft", "maps=manor+loft+flat+pinwheel")).Houses,
                "Every house ticked is saved as no list, so a new house joins the pool.");
            Assert.AreEqual("", Load(Saved.Replace("maps=flat+loft", "maps=")).Maps);
        }

        [Test]
        public void AnExtraKeyIsIgnoredSoALaterAdditionStillLoads()
        {
            var choice = Load(Saved + ";later=1");
            Assert.IsNotNull(choice);
            Assert.AreEqual(Saved, choice.Save());
        }

        [Test]
        public void TextThatWouldBreakTheFormatIsRejectedBeforeItIsSaved()
        {
            Assert.Throws<ArgumentException>(() => new LastPlayChoice("online", "Duos", null, "TimeRun", "ParcelHall", false, true));
            Assert.Throws<ArgumentException>(() => new LastPlayChoice(null, "Duos", null, "TimeRun", "ParcelHall", false, true));
            foreach (string bad in new[] { null, "", "a;b", "a=b", "a+b", "a b" })
            {
                Assert.Throws<ArgumentException>(() => new LastPlayChoice(LastPlayChoice.Practice, bad, null, "TimeRun", "ParcelHall", false, true), "mode " + bad);
                Assert.Throws<ArgumentException>(() => new LastPlayChoice(LastPlayChoice.Practice, "Duos", null, bad, "ParcelHall", false, true), "course " + bad);
                Assert.Throws<ArgumentException>(() => new LastPlayChoice(LastPlayChoice.Practice, "Duos", null, "TimeRun", bad, false, true), "course map " + bad);
                Assert.Throws<ArgumentException>(() => new LastPlayChoice(LastPlayChoice.Practice, "Duos", new[] { bad }, "TimeRun", "ParcelHall", false, true), "house " + bad);
            }
            Assert.Throws<ArgumentException>(() => new LastPlayChoice(LastPlayChoice.Practice, "Duos", new[] { "flat", "flat" }, "TimeRun", "ParcelHall", false, true));
        }

        [Test]
        public void MissingCataloguesRememberNothing()
        {
            Assert.IsNull(LastPlayChoice.Load(Saved, null, Houses, Courses, CourseMaps));
            Assert.IsNull(LastPlayChoice.Load(Saved, Modes, Houses, null, CourseMaps));
            Assert.IsNull(LastPlayChoice.Load(Saved, Modes, Houses, Courses, null));
            Assert.IsEmpty(LastPlayChoice.Load(Saved, Modes, null, Courses, CourseMaps).Houses, "Without an installed list no house can be singled out.");
        }
    }
}
