using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class TouchLayoutTests
    {
        static readonly (float width, float height)[] Phones =
        {
            ((float)Math.Sqrt(1920 * 1080 * 19.5 / 9), (float)Math.Sqrt(1920 * 1080 * 9 / 19.5)),
            (1920f, 1080f),
            ((float)Math.Sqrt(1920 * 1080 * 4.0 / 3), (float)Math.Sqrt(1920 * 1080 * 3.0 / 4)),
        };
        static readonly (TouchPart part, float ax, float x, float y, float d)[] TodaysHud =
        {
            (TouchPart.Move, 0f, 135f, 128f, 190f), (TouchPart.Aim, 1f, -128f, 142f, 152f), (TouchPart.Attack, 1f, -280f, 167f, 112f),
            (TouchPart.Dodge, 1f, -250f, 297f, 89f), (TouchPart.Jump, 1f, -123f, 294f, 89f), (TouchPart.Block, 1f, -384f, 270f, 89f),
            (TouchPart.Grab, 1f, -384f, 161f, 89f), (TouchPart.Spell, 1f, -503f, 270f, 89f), (TouchPart.Drop, 1f, -384f, 51f, 76f),
        };
        string folder;

        [SetUp]
        public void SetUp() => folder = Path.Combine(Path.GetTempPath(), "wb-touch-tests-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        static IEnumerable<(string name, Func<float, float, TouchLayout> make)> Presets()
        {
            yield return ("Classic", TouchLayout.Classic);
            yield return ("Claw", TouchLayout.Claw);
        }

        [Test]
        public void ClassicIsTodaysHudOnEveryPhoneShape()
        {
            foreach (var (width, height) in Phones)
            {
                var classic = TouchLayout.Classic(width, height);
                foreach (var (part, ax, x, y, d) in TodaysHud)
                {
                    var spot = classic[part];
                    Assert.AreEqual((ax * width + x) / width, spot.X, 1e-5f, $"{part} across at {width}x{height}");
                    Assert.AreEqual(y / height, spot.Y, 1e-5f, $"{part} up at {width}x{height}");
                    Assert.AreEqual(d / height, spot.Size, 1e-5f, $"{part} size at {width}x{height}");
                    Assert.AreEqual(1f, spot.Opacity);
                }
                Assert.AreEqual(1f, classic.Opacity);
            }
        }

        [Test]
        public void PresetsFitInsideTheSafeAreaWithoutOverlapsOnPhones()
        {
            foreach (var (name, make) in Presets())
                foreach (var (width, height) in Phones)
                {
                    var layout = make(width, height);
                    float aspect = width / height;
                    Assert.IsTrue(layout.Matches(layout.Clamped(aspect)), $"{name} stays inside at {width}x{height}");
                    CollectionAssert.IsEmpty(layout.Overlaps(aspect), $"{name} at {width}x{height}: {TouchLayout.OverlapWarning(layout.Overlaps(aspect))}");
                }
        }

        [Test]
        public void PresetsStayInsideInPortraitToo()
        {
            foreach (var (name, make) in Presets())
            {
                var layout = make(978f, 2120f);
                foreach (var part in TouchLayout.Parts)
                {
                    var spot = layout[part];
                    float rx = spot.Size / 2f, ry = spot.Size * 978f / 2120f / 2f;
                    Assert.GreaterOrEqual(spot.X - rx, -1e-5f, $"{name} {part}");
                    Assert.LessOrEqual(spot.X + rx, 1f + 1e-5f, $"{name} {part}");
                    Assert.GreaterOrEqual(spot.Y - ry, -1e-5f, $"{name} {part}");
                    Assert.LessOrEqual(spot.Y + ry, 1f + 1e-5f, $"{name} {part}");
                }
            }
        }

        [Test]
        public void ClampKeepsTheWholeButtonInsideAndValuesInRange()
        {
            var corner = TouchLayout.Clamp(new TouchSpot(0f, 1f, .3f), 2f);
            Assert.AreEqual(.075f, corner.X, 1e-6f);
            Assert.AreEqual(.85f, corner.Y, 1e-6f);
            var huge = new TouchSpot(.5f, .5f, 5f, 3f);
            Assert.AreEqual(TouchLayout.MaxSize, huge.Size);
            Assert.AreEqual(1f, huge.Opacity);
            var tiny = new TouchSpot(-4f, 9f, 0f, 0f);
            Assert.AreEqual((0f, 1f, TouchLayout.MinSize, TouchLayout.MinOpacity), (tiny.X, tiny.Y, tiny.Size, tiny.Opacity));
            var broken = new TouchSpot(float.NaN, float.PositiveInfinity, float.NaN, float.NaN);
            Assert.AreEqual((.5f, .5f, TouchLayout.MinSize, 1f), (broken.X, broken.Y, broken.Size, broken.Opacity));
            Assert.AreEqual(.5f, TouchLayout.Clamp(new TouchSpot(.5f, .5f, .3f), float.NaN).Y, 1e-6f, "A bad aspect falls back to 16:9.");
            var classic = TouchLayout.Classic(1920f, 1080f);
            Assert.AreEqual(TouchLayout.MinOpacity, new TouchLayout(TouchLayout.Parts.Select(part => classic[part]).ToArray(), 0f).Opacity);
            Assert.Throws<ArgumentException>(() => new TouchLayout(new TouchSpot[3]));
        }

        [Test]
        public void SnapLandsOnTheGridAndStaysInside()
        {
            var snapped = TouchLayout.Snap(new TouchSpot(.503f, .51f, .1f), 16f / 9f);
            Assert.AreEqual(0f, Remainder(snapped.X, TouchLayout.GridX), 1e-4f);
            Assert.AreEqual(0f, Remainder(snapped.Y, TouchLayout.GridY), 1e-4f);
            var edge = TouchLayout.Snap(new TouchSpot(.001f, .999f, .2f), 16f / 9f);
            Assert.AreEqual(.2f / 2f / (16f / 9f), edge.X, 1e-5f, "The edge wins over the grid.");
            Assert.AreEqual(.9f, edge.Y, 1e-5f);
        }

        static float Remainder(float value, float step)
        {
            float r = value / step - (float)Math.Round(value / step);
            return Math.Abs(r);
        }

        [Test]
        public void OverlapsNameBothButtonsAndJustTouchingIsFine()
        {
            var classic = TouchLayout.Classic(1920f, 1080f);
            var piled = classic.With(TouchPart.Spell, classic[TouchPart.Jump]);
            var overlaps = piled.Overlaps(16f / 9f);
            CollectionAssert.Contains(overlaps, (TouchPart.Jump, TouchPart.Spell));
            Assert.AreEqual("JUMP overlaps SPELL.", TouchLayout.OverlapWarning(new[] { (TouchPart.Jump, TouchPart.Spell) }));
            Assert.AreEqual("JUMP overlaps SPELL (+2 more).", TouchLayout.OverlapWarning(new[] { (TouchPart.Jump, TouchPart.Spell), (TouchPart.Move, TouchPart.Aim), (TouchPart.Book, TouchPart.Eye) }));
            Assert.AreEqual("", TouchLayout.OverlapWarning(Array.Empty<(TouchPart, TouchPart)>()));
            var a = new TouchSpot(.4f, .5f, .1f);
            var touching = new TouchSpot(.4f + .1f / (16f / 9f), .5f, .1f);
            Assert.IsFalse(TouchLayout.Overlap(a, touching, 16f / 9f));
            Assert.IsTrue(TouchLayout.Overlap(a, new TouchSpot(.45f, .5f, .1f), 16f / 9f));
        }

        [Test]
        public void PinchScalesWithTheFingersAndStaysInRange()
        {
            Assert.AreEqual(.2f, TouchLayout.Pinch(.1f, 100f, 200f), 1e-6f);
            Assert.AreEqual(.1f, TouchLayout.Pinch(.1f, 200f, 200f), 1e-6f);
            Assert.AreEqual(TouchLayout.MaxSize, TouchLayout.Pinch(.2f, 10f, 400f));
            Assert.AreEqual(TouchLayout.MinSize, TouchLayout.Pinch(.2f, 400f, 10f));
            Assert.AreEqual(.2f, TouchLayout.Pinch(.2f, 0f, 100f), "No start distance means no change.");
            Assert.AreEqual(.2f, TouchLayout.Pinch(.2f, 100f, float.NaN));
        }

        [Test]
        public void CodesRoundTripWithinOneStep()
        {
            var random = new Random(7);
            for (int round = 0; round < 200; round++)
            {
                var spots = TouchLayout.Parts.Select(_ => new TouchSpot((float)random.NextDouble(), (float)random.NextDouble(),
                    (float)random.NextDouble() * .3f, (float)random.NextDouble())).ToArray();
                var layout = new TouchLayout(spots, (float)random.NextDouble());
                string code = layout.Encode();
                StringAssert.StartsWith("wb-touch:" + TouchLayout.Version + ":", code);
                Assert.Less(code.Length, 160, "A code stays short enough to paste in a chat.");
                Assert.IsTrue(TouchLayout.TryDecode(code, out var back, out string message), message);
                Assert.IsNull(message);
                Assert.AreEqual(layout.Opacity, back.Opacity, .5f / 255f * .8f + 1e-6f);
                foreach (var part in TouchLayout.Parts)
                {
                    Assert.AreEqual(layout[part].X, back[part].X, 1f / 65535f, part.ToString());
                    Assert.AreEqual(layout[part].Y, back[part].Y, 1f / 65535f, part.ToString());
                    Assert.AreEqual(layout[part].Size, back[part].Size, .24f / 255f, part.ToString());
                    Assert.AreEqual(layout[part].Opacity, back[part].Opacity, .8f / 255f, part.ToString());
                }
                Assert.AreEqual(code, back.Encode(), "Decoding then encoding is stable.");
            }
        }

        [Test]
        public void PastedCodesSurviveSpacesLineBreaksAndCapitals()
        {
            string code = TouchLayout.Claw(1920f, 1080f).Encode();
            string wrapped = "  " + code.Substring(0, 40) + "\n " + code.Substring(40) + "\t ";
            Assert.IsTrue(TouchLayout.TryDecode(wrapped, out var layout, out _));
            Assert.AreEqual(code, layout.Encode());
            Assert.IsTrue(TouchLayout.TryDecode("WB-TOUCH" + code.Substring(8), out _, out _));
        }

        [Test]
        public void BadCodesExplainThemselves()
        {
            string code = TouchLayout.Classic(1920f, 1080f).Encode();
            var parts = code.Split(':');
            Expect(null, TouchLayout.NoCode);
            Expect("   \n", TouchLayout.NoCode);
            Expect("hello", TouchLayout.NotACode);
            Expect("wb-room:abc:def:ghi", TouchLayout.NotACode);
            Expect("wb-touch:x:" + parts[2] + ":" + parts[3], TouchLayout.NotACode);
            Expect("wb-touch:-1:" + parts[2] + ":" + parts[3], TouchLayout.NotACode);
            Expect("wb-touch:99999999999999:" + parts[2] + ":" + parts[3], TouchLayout.NotACode);
            Expect(new string('A', 600), TouchLayout.NotACode);
            Expect(Code(TouchLayout.Version + 1, Payload(TouchLayout.Count)), TouchLayout.Newer);
            Expect("wb-touch:" + (TouchLayout.Version + 1) + ":anything:0000", TouchLayout.Newer);
            Expect(Code(0, Payload(TouchLayout.Count)), TouchLayout.Older);
            char swapped = parts[2][5] == 'A' ? 'B' : 'A';
            Expect(parts[0] + ":" + parts[1] + ":" + parts[2].Substring(0, 5) + swapped + parts[2].Substring(6) + ":" + parts[3], TouchLayout.Damaged);
            Expect(parts[0] + ":" + parts[1] + ":" + parts[2] + ":" + (parts[3] == "0000" ? "0001" : "0000"), TouchLayout.Damaged);
            Expect(code.Substring(0, code.Length - 5), TouchLayout.NotACode);
            int version = TouchLayout.Version;
            Expect(TextCode(version, parts[2].Substring(0, parts[2].Length - 8)), TouchLayout.Damaged);
            Expect(Code(version, Payload(TouchLayout.Count - 1)), TouchLayout.Damaged);
            Expect(Code(version, Payload(TouchLayout.Count).Concat(new byte[] { 0 }).ToArray()), TouchLayout.Damaged);
            Expect(Code(version, Payload(TouchLayout.LegacyCount)), TouchLayout.Damaged);
            Expect(Code(TouchLayout.LegacyVersion, Payload(TouchLayout.Count)), TouchLayout.Damaged);
            Expect(Code(version, Encoding.ASCII.GetBytes("not a layout")), TouchLayout.Damaged);
            Expect(TextCode(version, "!!!!"), TouchLayout.Damaged);
            Expect(TextCode(version, "A"), TouchLayout.Damaged);
            Assert.IsTrue(TouchLayout.TryDecode(Code(version, Payload(TouchLayout.Count)), out var built, out _), "The test's own encoder matches the game's.");
            Assert.AreEqual(TouchLayout.MinOpacity, built.Opacity);
        }

        [Test]
        public void OlderCodesWithoutMicAndSpeakerStillLoad()
        {
            var classic = TouchLayout.Classic(1920f, 1080f);
            var bytes = Payload(TouchLayout.LegacyCount);
            bytes[1] = 255;
            for (int i = 0; i < TouchLayout.LegacyCount; i++)
            {
                var spot = TouchLayout.Claw(1920f, 1080f)[(TouchPart)i];
                int at = 2 + i * 6, x = (int)Math.Round(spot.X * ushort.MaxValue), y = (int)Math.Round(spot.Y * ushort.MaxValue);
                bytes[at] = (byte)(x >> 8); bytes[at + 1] = (byte)x; bytes[at + 2] = (byte)(y >> 8); bytes[at + 3] = (byte)y;
                bytes[at + 4] = (byte)Math.Round((spot.Size - TouchLayout.MinSize) / (TouchLayout.MaxSize - TouchLayout.MinSize) * 255f);
                bytes[at + 5] = 255;
            }
            string old = Code(TouchLayout.LegacyVersion, bytes);
            Assert.IsTrue(TouchLayout.TryDecode(old, out var layout, out string message), message);
            Assert.AreEqual(TouchLayout.Claw(1920f, 1080f)[TouchPart.Eye].X, layout[TouchPart.Eye].X, 1f / 65535f);
            foreach (var part in new[] { TouchPart.Mic, TouchPart.Speaker })
                Assert.AreEqual(classic[part], layout[part], part + " takes its Classic place.");
            StringAssert.StartsWith("wb-touch:" + TouchLayout.Version + ":", layout.Encode(), "Saving writes the current version.");
            var book = TouchLayoutBook.Parse(TouchLayoutBook.Header + "\nactive Custom1\nslot 1 " + old + " Mine");
            Assert.AreEqual(TouchTemplate.Custom1, book.Active, "A saved book from before Mic and Speaker keeps its layout.");
            Assert.AreEqual(classic[TouchPart.Mic], book.Stored(TouchTemplate.Custom1)[TouchPart.Mic]);
        }

        [Test]
        public void MicAndSpeakerSitBesidePauseInClassicAndTopLeftInClaw()
        {
            foreach (var (width, height) in Phones)
            {
                var classic = TouchLayout.Classic(width, height);
                Assert.AreEqual(classic[TouchPart.Pause].Y, classic[TouchPart.Mic].Y, 1e-5f);
                Assert.AreEqual(classic[TouchPart.Pause].Y, classic[TouchPart.Speaker].Y, 1e-5f);
                Assert.Greater(classic[TouchPart.Mic].X, classic[TouchPart.Pause].X);
                Assert.Less(classic[TouchPart.Speaker].X, classic[TouchPart.Pause].X);
                var claw = TouchLayout.Claw(width, height);
                Assert.Less(claw[TouchPart.Speaker].X, claw[TouchPart.Mic].X);
                Assert.Less(claw[TouchPart.Mic].X, .25f);
                Assert.Greater(claw[TouchPart.Mic].Y, .85f);
            }
            Assert.AreEqual("MIC", TouchLayout.Label(TouchPart.Mic));
            Assert.AreEqual("SPEAKER", TouchLayout.Label(TouchPart.Speaker));
        }

        static void Expect(string text, string message)
        {
            Assert.IsFalse(TouchLayout.TryDecode(text, out var layout, out string said), text);
            Assert.IsNull(layout);
            Assert.AreEqual(message, said, text);
        }

        [Test]
        public void FuzzedTextNeverThrows()
        {
            var random = new Random(11);
            string code = TouchLayout.Claw(2120f, 978f).Encode();
            const string alphabet = "wb-touch:0123456789ABCDEFabcdef_-+/= \né";
            for (int i = 0; i < 2000; i++)
            {
                string text;
                if (i % 2 == 0)
                {
                    var chars = new char[random.Next(0, 160)];
                    for (int c = 0; c < chars.Length; c++) chars[c] = alphabet[random.Next(alphabet.Length)];
                    text = new string(chars);
                }
                else
                {
                    var builder = new StringBuilder(code);
                    int at = random.Next(builder.Length);
                    switch (random.Next(3))
                    {
                        case 0: builder[at] = alphabet[random.Next(alphabet.Length)]; break;
                        case 1: builder.Remove(at, 1); break;
                        default: builder.Insert(at, alphabet[random.Next(alphabet.Length)]); break;
                    }
                    text = builder.ToString();
                }
                TouchLayout layout = null;
                string message = null;
                Assert.DoesNotThrow(() => TouchLayout.TryDecode(text, out layout, out message), text);
                Assert.IsTrue(layout != null ^ message != null, text);
            }
        }

        static byte[] Payload(int count)
        {
            var bytes = new byte[2 + count * 6];
            bytes[0] = (byte)count;
            return bytes;
        }

        static string Code(int version, byte[] bytes) =>
            TextCode(version, Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'));

        static string TextCode(int version, string payload)
        {
            string body = "wb-touch:" + version + ":" + payload;
            int crc = 0xFFFF;
            foreach (char c in body)
            {
                crc ^= c << 8;
                for (int bit = 0; bit < 8; bit++) crc = ((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1) & 0xFFFF;
            }
            return body + ":" + crc.ToString("x4");
        }

        [Test]
        public void LabelsAreTheTouchWordsFromTheControlHints()
        {
            var words = new Dictionary<TouchPart, PlayerControl>
            {
                [TouchPart.Move] = PlayerControl.Move, [TouchPart.Aim] = PlayerControl.Look, [TouchPart.Attack] = PlayerControl.Attack,
                [TouchPart.Dodge] = PlayerControl.Dodge, [TouchPart.Jump] = PlayerControl.Jump, [TouchPart.Block] = PlayerControl.Block,
                [TouchPart.Grab] = PlayerControl.Grab, [TouchPart.Spell] = PlayerControl.Spell, [TouchPart.Drop] = PlayerControl.Drop,
                [TouchPart.Book] = PlayerControl.Book,
            };
            foreach (var pair in words) Assert.AreEqual(ControlWords.Key(ControlDevice.Touch, pair.Value), TouchLayout.Label(pair.Key), pair.Key.ToString());
            foreach (var hand in new[] { TouchPart.Hand1, TouchPart.Hand2 })
                StringAssert.StartsWith(ControlWords.Key(ControlDevice.Touch, PlayerControl.Hands), TouchLayout.Label(hand));
            Assert.AreEqual(TouchLayout.Count, TouchLayout.Parts.Select(TouchLayout.Label).Distinct().Count(label => label.Length > 0));
        }

        [Test]
        public void TheBookSavesAtomicallyAndComesBack()
        {
            var book = new TouchLayoutBook();
            var claw = TouchLayout.Claw(1920f, 1080f).WithOpacity(.6f);
            book.Store(TouchTemplate.Custom2, claw, "  My   claw\n");
            book.Active = TouchTemplate.Custom2;
            Assert.IsTrue(book.TrySave(folder, out string error), error);
            var back = TouchLayoutBook.Load(folder, out string problem);
            Assert.IsNull(problem);
            Assert.AreEqual(TouchTemplate.Custom2, back.Active);
            Assert.AreEqual("My claw", back.Name(TouchTemplate.Custom2));
            Assert.AreEqual(claw.Encode(), back.Stored(TouchTemplate.Custom2).Encode());
            Assert.IsNull(back.Stored(TouchTemplate.Custom1));
            Assert.AreEqual("Custom 1", back.Name(TouchTemplate.Custom1));
            CollectionAssert.IsEmpty(Directory.GetFiles(folder, "*.tmp"));
            Assert.IsFalse(File.Exists(Path.Combine(folder, TouchLayoutBook.FileName + TouchLayoutBook.Backup)), "The first save has nothing to back up.");
            back.Rename(TouchTemplate.Custom2, "Second");
            Assert.IsTrue(back.TrySave(folder, out error), error);
            Assert.IsTrue(File.Exists(Path.Combine(folder, TouchLayoutBook.FileName + TouchLayoutBook.Backup)));
            CollectionAssert.IsEmpty(Directory.GetFiles(folder, "*.tmp"));
            Assert.AreEqual("Second", TouchLayoutBook.Load(folder, out _).Name(TouchTemplate.Custom2));
        }

        [Test]
        public void ABrokenFileFallsBackToTheBackupThenToClassic()
        {
            var book = new TouchLayoutBook();
            book.Store(TouchTemplate.Custom1, TouchLayout.Classic(1920f, 1080f).WithOpacity(.5f));
            book.Active = TouchTemplate.Custom1;
            Assert.IsTrue(book.TrySave(folder, out _));
            Assert.IsTrue(book.TrySave(folder, out _));
            string path = Path.Combine(folder, TouchLayoutBook.FileName);
            File.WriteAllText(path, "half a fi");
            var restored = TouchLayoutBook.Load(folder, out string problem);
            Assert.AreEqual(TouchLayoutBook.Restored, problem);
            Assert.AreEqual(TouchTemplate.Custom1, restored.Active);
            Assert.IsTrue(book.TrySave(folder, out _), "Saving over a broken file works.");
            Assert.AreEqual(TouchTemplate.Custom1, TouchLayoutBook.Load(folder, out problem).Active);
            Assert.IsNull(problem);
            File.WriteAllText(path, "junk");
            File.WriteAllText(path + TouchLayoutBook.Backup, "junk");
            var fresh = TouchLayoutBook.Load(folder, out problem);
            Assert.AreEqual(TouchTemplate.Classic, fresh.Active);
            Assert.IsNotNull(problem);
            File.WriteAllText(path, TouchLayoutBook.Header + "\n" + new string('x', TouchLayoutBook.MaximumBytes));
            TouchLayoutBook.Load(folder, out problem);
            StringAssert.Contains("too large", problem);
            var none = TouchLayoutBook.Load(Path.Combine(folder, "missing"), out problem);
            Assert.IsNull(problem, "No file yet is not a problem.");
            Assert.AreEqual(TouchTemplate.Classic, none.Active);
        }

        [Test]
        public void TheBookIgnoresBadLinesAndEmptySlots()
        {
            string good = TouchLayout.Claw(1920f, 1080f).Encode();
            var book = TouchLayoutBook.Parse(string.Join("\r\n", TouchLayoutBook.Header, "active Custom3", "slot 3 nonsense Named",
                "slot 9 " + good, "slot 1 " + good + " First one", "weird line", "slot"));
            Assert.IsNotNull(book);
            Assert.AreEqual(TouchTemplate.Classic, book.Active, "Custom 3 had no readable layout.");
            Assert.IsTrue(book.Has(TouchTemplate.Custom1));
            Assert.AreEqual("First one", book.Name(TouchTemplate.Custom1));
            Assert.IsFalse(book.Has(TouchTemplate.Custom3));
            Assert.IsNull(TouchLayoutBook.Parse("wb-touch-book 2\nactive Claw"));
            Assert.AreEqual(TouchTemplate.Claw, TouchLayoutBook.Parse(TouchLayoutBook.Header + "\nactive Claw").Active);
            Assert.AreEqual(TouchTemplate.Classic, TouchLayoutBook.Parse(TouchLayoutBook.Header + "\nactive 7").Active);
        }

        [Test]
        public void PresetsAreReadOnlyAndEditsGoToTheFirstFreeSlot()
        {
            var book = new TouchLayoutBook();
            var layout = TouchLayout.Classic(1920f, 1080f);
            Assert.Throws<ArgumentException>(() => book.Store(TouchTemplate.Claw, layout));
            Assert.Throws<ArgumentException>(() => book.Rename(TouchTemplate.Classic, "Mine"));
            Assert.AreEqual(TouchTemplate.Custom1, book.SaveTarget(TouchTemplate.Classic));
            book.Store(TouchTemplate.Custom1, layout);
            Assert.AreEqual(TouchTemplate.Custom2, book.SaveTarget(TouchTemplate.Claw));
            Assert.AreEqual(TouchTemplate.Custom1, book.SaveTarget(TouchTemplate.Custom1));
            book.Store(TouchTemplate.Custom2, layout);
            book.Store(TouchTemplate.Custom3, layout);
            Assert.AreEqual(TouchTemplate.Custom1, book.SaveTarget(TouchTemplate.Classic), "With every slot full, Custom 1 is replaced.");
            book.Active = TouchTemplate.Claw;
            var copy = book.Clone();
            copy.Active = TouchTemplate.Custom3;
            Assert.AreEqual(TouchTemplate.Claw, book.Active, "A clone is separate.");
            Assert.AreEqual(TouchLayout.Claw(2000f, 1000f).Encode(), book.Current(2000f, 1000f).Encode(), "Presets follow the screen shape.");
            Assert.AreEqual(TouchLayout.Classic(1000f, 0f).Encode(), new TouchLayoutBook().Current(1000f, 0f).Encode());
        }

        [Test]
        public void NamesAreShortSingleLineAndDefaultWhenBlank()
        {
            Assert.AreEqual("", TouchLayoutBook.CleanName(null));
            Assert.AreEqual("My claw", TouchLayoutBook.CleanName("  My \t claw \n"));
            Assert.AreEqual(TouchLayoutBook.NameLength, TouchLayoutBook.CleanName(new string('a', 40)).Length);
            Assert.AreEqual("aaaaaaaaaaaaaaa", TouchLayoutBook.CleanName(new string('a', 15) + " b"), "A word that does not fit is dropped, not glued on.");
            Assert.AreEqual("ab", TouchLayoutBook.CleanName("a\u0007b"));
            var book = new TouchLayoutBook();
            book.Rename(TouchTemplate.Custom1, "   ");
            Assert.AreEqual("Custom 1", book.Name(TouchTemplate.Custom1));
        }
    }
}
