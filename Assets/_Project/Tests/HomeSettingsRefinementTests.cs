using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Wreckabulary.Tests
{
    public sealed class HomeSettingsRefinementTests
    {
        const float Ready = 3f, Pixel = .5f, Shade = .01f, SmallestButton = 44f;
        static readonly string[] Keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey, LobbyThemes.PreferenceKey };
        static readonly string[] Tabs = { "profile", "audio", "gameplay", "graphics", "controls", "help" };
        string[] saved;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            yield return TestScenes.Reset();
            PlayerPrefs.SetString(LobbyMenu.TurnHintKey, "seen");
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning,
                Ready, "lobby ready");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            KeyBindings.Stop();
            yield return TestScenes.Reset();
            for (int i = 0; i < Keys.Length; i++)
                if (saved[i] == null) PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetString(Keys[i], saved[i]);
            PlayerPrefs.Save();
            LobbyThemes.Restore();
        }

        static LobbyMenu Menu => LobbyMenu.Instance;

        static Button Named(Component root, string name) => root.GetComponentsInChildren<Button>().Single(button => button.name == name);

        static Rect OnScreen(Component part)
        {
            var corners = new Vector3[4];
            ((RectTransform)part.transform).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTest]
        public IEnumerator HomeNamesTheMatchWithItsPictureOnTwoLinesAndAChangeChip()
        {
            var menu = Menu;
            menu.Choose(LobbyMenu.Practice, "Dibs"); menu.Open(LobbyMenu.Play);
            yield return null;
            Named(menu.Page<PlayPage>().Root, PlayPage.ModeButton("Duos")).onClick.Invoke();
            yield return null;
            menu.Open(LobbyMenu.Home);
            yield return Frames(2);
            var dock = menu.Page<HomePage>().Root.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Next match");
            var plate = dock.Find(LobbyHomePageView.LastPlate);
            Assert.IsNotNull(plate, "the plate names the match PLAY starts");
            var picture = plate.Find(LobbyHomePageView.ModePicture);
            Assert.IsNotNull(picture, "with the mode's picture");
            Assert.IsNotNull(picture.GetComponentInChildren<RawImage>(), "the picture shows the mode's item");
            var line = plate.GetComponentsInChildren<TMP_Text>().Single(text => text.name == LobbyHomePageView.LastLine);
            Assert.AreEqual(menu.LastMatch, line.text);
            line.ForceMeshUpdate();
            Assert.IsFalse(line.isTextTruncated || line.isTextOverflowing, line.text);
            Assert.GreaterOrEqual(line.fontSize, LobbyHomePageView.LastTextMinimum - Shade, "the mode name is at least 18 pt");
            Assert.AreEqual(2, line.textInfo.lineCount, "the mode on one line, the house under it");
            var first = line.textInfo.lineInfo[0];
            string mode = new string(line.textInfo.characterInfo.Skip(first.firstCharacterIndex)
                .Take(first.lastCharacterIndex - first.firstCharacterIndex + 1).Select(info => info.character).ToArray()).Trim();
            Assert.AreEqual(LobbyMenu.ModeName("Duos"), mode);
            Assert.Less(OnScreen(picture).xMax, OnScreen(line).xMin + Pixel, "the picture leads the name");

            var play = Named(dock, LobbyHomePageView.PlayButton);
            var change = Named(dock, LobbyHomePageView.ChangeButton);
            var chip = ((RectTransform)change.transform).rect;
            Assert.GreaterOrEqual(chip.height, SmallestButton - Pixel, "Change is a full-size chip");
            Assert.Less(chip.height, ((RectTransform)play.transform).rect.height * .5f, "and still the small button");
            Assert.Less(chip.width, dock.rect.width * .7f, "a chip, not a bar");
            Assert.AreEqual(LobbyKit.Raised, change.FaceOf().color, "with a shape to click");
            Assert.IsNotNull(change.Body().Find("Edge"), "and an edge");
            var label = change.GetComponentInChildren<TMP_Text>();
            Assert.AreEqual(LobbyHomePageView.ChangeLabel, label.text);
            Assert.GreaterOrEqual(label.fontSize, 15f - Shade);
            Assert.AreEqual(OnScreen(play).center.x, OnScreen(change).center.x, 1f, "centred under PLAY");
            Assert.AreEqual(LobbyHomePageView.PlayLabel, play.GetComponentInChildren<TMP_Text>().text);
        }

        [UnityTest]
        public IEnumerator TheCoinBalanceIsADarkNumberAtTheNavigationSize()
        {
            yield return Frames(1);
            var balance = Named(Menu, "Shop").GetComponentInChildren<TMP_Text>();
            var nav = Named(Menu, "PLAY").GetComponentInChildren<TMP_Text>();
            Assert.AreEqual(Menu.Career.Coins.ToString("N0", System.Globalization.CultureInfo.InvariantCulture), balance.text);
            Assert.AreEqual(LobbyKit.Navy, balance.color, "dark ink, not a second coin");
            Assert.GreaterOrEqual(balance.fontSize, nav.fontSize - Shade, "as large as the navigation words");
            balance.ForceMeshUpdate();
            Assert.IsFalse(balance.isTextTruncated);
        }

        [UnityTest]
        public IEnumerator SettingsKeepsOneHeightOnEveryTabUnderAFullScreenDim()
        {
            var menu = Menu;
            menu.Open(LobbyMenu.Settings);
            yield return new WaitForSecondsRealtime(LobbyPop.Seconds + Ready * .1f);
            var page = menu.Page<SettingsPage>();
            float height = -1f;
            foreach (string tab in Tabs)
            {
                page.ShowTab(tab);
                yield return Frames(2);
                var frame = page.Root.GetComponentInChildren<LobbySettingsPageView>();
                float now = frame.Panel.rect.height;
                if (height < 0f) height = now;
                Assert.AreEqual(height, now, Pixel, tab + " keeps the panel height");
                var shade = page.Root.GetComponentsInChildren<LobbyShade>().Single();
                var dim = OnScreen(shade);
                Assert.LessOrEqual(dim.xMin, Pixel, tab + ": the dim reaches the left edge");
                Assert.LessOrEqual(dim.yMin, Pixel, tab + ": the dim reaches the bottom edge");
                Assert.GreaterOrEqual(dim.xMax, Screen.width - Pixel, tab + ": the dim reaches the right edge");
                Assert.GreaterOrEqual(dim.yMax, Screen.height - Pixel, tab + ": the dim reaches the top edge");
            }
            Assert.Greater(height, 0f);
        }

        [UnityTest]
        public IEnumerator HelpEndsInTheStandardPlayButtonAndChoicesHaveAShape()
        {
            var menu = Menu;
            menu.Open(LobbyMenu.Settings);
            var page = menu.Page<SettingsPage>();
            page.ShowTab("help");
            yield return Frames(2);
            var go = Named(page.Root, SettingsPage.HelpPlay);
            var size = ((RectTransform)go.transform).rect;
            Assert.AreEqual(SettingsPage.PlayWidth, size.width, Pixel, "the Play page's button size");
            Assert.AreEqual(SettingsPage.PlayHeight, size.height, Pixel);
            Assert.AreEqual(LobbyKit.PrimaryFill, go.FaceOf().color);
            Assert.AreEqual("PLAY", go.GetComponentInChildren<TMP_Text>().text);
            Assert.IsTrue(go.Body().GetComponentsInChildren<Image>().Any(image => image.sprite && image.sprite.name.Contains(LobbyIcons.Arrow)),
                "with the arrow");
            go.onClick.Invoke();
            yield return null;
            Assert.AreEqual(LobbyMenu.Play, menu.Current, "it opens the Play section");

            menu.Open(LobbyMenu.Settings);
            page.ShowTab("audio");
            yield return Frames(2);
            foreach (string name in new[] { "Sound on", "Sound off" })
            {
                var choice = Named(page.Root, name);
                Assert.GreaterOrEqual(((RectTransform)choice.transform).rect.height, SmallestButton - Pixel, name + " is a full-size control");
                Assert.AreEqual(LobbyKit.Raised, choice.FaceOf().color, name + " has a shape to click");
                var edge = choice.Body().Find(LobbyChoiceShape.EdgeName).GetComponent<Image>();
                Assert.AreEqual(name == (GameFeedback.Muted ? "Sound off" : "Sound on") ? LobbyKit.Selection : LobbyKit.Line, edge.color,
                    name + " shows whether it is picked");
            }
            var reset = Named(page.Root, "Reset audio");
            Assert.IsNotNull(reset.Body().Find(LobbyChoiceShape.EdgeName), "row actions have a shape too");
        }
    }
}
