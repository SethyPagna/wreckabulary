using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundRecipeGridGuiTests
    {
        [TestCase(1280, 720, 14)]
        [TestCase(1920, 1080, 14)]
        [TestCase(854, 480, 22)]
        public void ActualMenuShowsAPictureGridOverAPinnedDetailLineAndDefersSelection(int width, int height, int font)
        {
            using var f = new WordboundRecipeGridFixture(font);
            var previous = Gamepad.current; Gamepad pad = null; GridWindow window = null;
            try
            {
                pad = InputSystem.AddDevice<Gamepad>();
                var binding = (WordboundInputBinding)typeof(WordboundPlayerView).GetField("binding", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f.View);
                typeof(WordboundInputBinding).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(binding, pad);
                typeof(WordboundInputBinding).GetField("gamepad", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(binding, new GamepadBinding(pad));
                f.Set("persistentNotice", "Reconnecting…");
                ((WordboundMenuFocus)f.Get("menuFocus")).Reset("recipe:STOOL");
                f.Set("revealMenuSelection", true);
                window = ScriptableObject.CreateInstance<GridWindow>(); window.Fixture = f; window.Size = new Vector2(width, height);
                window.position = new Rect(80, 80, width, height); window.ShowUtility();
                window.ConfirmTile = true;
                window.SendEvent(new Event { type = EventType.Layout }); window.SendEvent(new Event { type = EventType.Repaint });
                Assert.IsNull(window.Failure); Assert.Greater(window.Repaints, 0); Assert.Greater(window.Layouts, 0);
                Assert.IsFalse(window.SelectionChangedDuringGui); Assert.AreNotEqual("STOOL", f.Get("selectedRecipe"));
                float scrolled = ((Vector2)f.Get("recipeScroll")).y;
                if (height < 720) Assert.Greater(scrolled, 0,
                    "Earlier chosen BAT must not consume the controller's request to reveal the lower STOOL card.");
                else Assert.AreEqual(0, scrolled, .5f, "When every recipe fits, the grid does not scroll.");
                f.Call("ExecuteMenuAction", true); Assert.AreEqual("STOOL", f.Get("selectedRecipe"));
                window.SendEvent(new Event { type = EventType.Layout }); window.SendEvent(new Event { type = EventType.Repaint });
                Assert.IsNull(window.Failure); Assert.IsTrue(f.View.MenuOpen);
                Assert.AreEqual("Reconnecting…", f.Get("persistentNotice"));
                Assert.AreEqual("Reconnecting…", f.Get("menuConnectionStatus"));
            }
            finally
            {
                if (window) window.Close();
                if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
                if (previous != null && previous.added) previous.MakeCurrent();
            }
        }

        sealed class GridWindow : EditorWindow
        {
            internal WordboundRecipeGridFixture Fixture;
            internal Vector2 Size;
            internal bool ConfirmTile, SelectionChangedDuringGui;
            internal int Layouts, Repaints;
            internal Exception Failure;
            void OnGUI()
            {
                if (Fixture == null || !Fixture.Hud) return;
                try
                {
                    string before = (string)Fixture.Get("selectedRecipe");
                    if (Event.current.type == EventType.Layout) Layouts++;
                    if (Event.current.type == EventType.Repaint && ConfirmTile)
                    { ConfirmTile = false; ((WordboundMenuFocus)Fixture.Get("menuFocus")).Confirm(); }
                    Fixture.Call("Menu", Size.x, Size.y, true);
                    SelectionChangedDuringGui |= before != (string)Fixture.Get("selectedRecipe");
                    if (Event.current.type != EventType.Repaint) return;
                    Repaints++;
                    var viewport = (Rect)Fixture.Get("recipeViewportRect");
                    var detail = (Rect)Fixture.Get("recipeDetailRect");
                    var back = (Rect)Fixture.Get("recipeReturnRect");
                    var craft = (Rect)Fixture.Get("recipeCraftRect");
                    var styles = (WordboundUiStyles)Fixture.Get("ui");
                    Rect area = styles.MenuRect(Size.x, Size.y);
                    Assert.GreaterOrEqual(viewport.height, 30, "At least one scrollable area remains at maximum text size.");
                    Assert.LessOrEqual(viewport.yMax + styles.Gap, detail.yMin + .5f, "The grid sits above the pinned detail line.");
                    Assert.AreEqual(viewport.xMin, detail.xMin, .5f);
                    Assert.AreEqual(viewport.xMax, detail.xMax, .5f);
                    Assert.GreaterOrEqual(viewport.width, 400, "The grid uses the whole page width.");
                    Assert.GreaterOrEqual(craft.height, styles.RowHeight);
                    Assert.GreaterOrEqual(craft.xMin, detail.xMin);
                    Assert.LessOrEqual(craft.xMax, detail.xMax + .5f);
                    Assert.LessOrEqual(craft.yMax, detail.yMax + .5f);
                    Assert.GreaterOrEqual(craft.yMin, detail.yMin - .5f);
                    Assert.LessOrEqual(detail.yMax, back.yMin + 1);
                    Assert.LessOrEqual(back.yMax, area.height - styles.Padding + 1);
                    Assert.LessOrEqual(detail.xMax, area.width - styles.Padding + 1);
                    Assert.GreaterOrEqual(viewport.xMin, styles.Padding);
                    Assert.AreEqual(5, (int)Fixture.Get("recipeColumns"), "Five pictures across, as in the house recipe book.");
                    if (Size.y >= 720) Assert.LessOrEqual((float)Fixture.Get("recipeContentBottom"), viewport.height + .5f,
                        "All thirteen course recipes fit without scrolling.");
                    var tiles = (GUIStyle)Fixture.Get("recipeTileStyle");
                    Assert.GreaterOrEqual(tiles.fontSize, 10, "Letter tiles stay readable.");
                    var line = (GUIStyle)Fixture.Get("recipeLineStyle");
                    Assert.AreEqual(styles.Wrapped.normal.textColor, line.normal.textColor);
                    var iconArea = new Rect(0, 0, 120, 60);
                    var fit = (Rect)typeof(WordboundMatchHud).GetMethod("FitRecipeIcon", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { iconArea, 192f, 192f });
                    Assert.AreEqual(1, fit.width / fit.height, .0001f);
                    Assert.IsTrue(iconArea.Contains(fit.min)); Assert.LessOrEqual(fit.xMax, iconArea.xMax); Assert.LessOrEqual(fit.yMax, iconArea.yMax);
                }
                catch (Exception error) { Failure = error; }
            }
        }
    }
}
