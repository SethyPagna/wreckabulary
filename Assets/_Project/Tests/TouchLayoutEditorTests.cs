using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class TouchLayoutEditorTests
    {
        string folder;
        GameHud hud;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            folder = Path.Combine(Application.temporaryCachePath, "touch-layout-editor-" + System.Guid.NewGuid().ToString("N"));
            TouchLayouts.Folder = folder;
            TouchLayouts.Reload();
            TouchBinding.Shared.ReleaseAll();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (TouchLayoutEditor.Current) Object.Destroy(TouchLayoutEditor.Current.gameObject);
            if (hud) Object.Destroy(hud.gameObject);
            TouchBinding.Shared.ReleaseAll();
            TouchBinding.Shared.Enabled = TouchBinding.Shared.OverlayDesktop = false;
            TouchBinding.Shared.OverlayBindingId = null;
            yield return null;
            TouchLayouts.Folder = null;
            TouchLayouts.Reload();
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            yield return TestScenes.Reset();
        }

        IEnumerator ShowHud()
        {
            hud = new GameObject("Touch layout HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.ShowTouchControls(true);
            yield return null;
        }

        RectTransform HudPart(TouchPart part) => (RectTransform)hud.transform.Find("Safe HUD/Touch controls/" + TouchLayoutView.HudName(part));
        RectTransform HudRoot => (RectTransform)hud.transform.Find("Safe HUD/Touch controls");

        static void Click(TouchLayoutEditor editor, string name) =>
            editor.GetComponentsInChildren<Button>().Single(button => button.name == name).onClick.Invoke();

        static RectTransform HandleOf(TouchLayoutEditor editor, TouchPart part) =>
            (RectTransform)editor.transform.Find("Safe area/Handles/Handle " + TouchLayout.Label(part));

        static void Drag(RectTransform handle, Vector2 by)
        {
            var from = RectTransformUtility.WorldToScreenPoint(null, handle.position);
            var data = new PointerEventData(EventSystem.current) { pointerId = 7, position = from };
            ExecuteEvents.Execute(handle.gameObject, data, ExecuteEvents.pointerDownHandler);
            data.position = from + by * .5f;
            ExecuteEvents.Execute(handle.gameObject, data, ExecuteEvents.dragHandler);
            data.position = from + by;
            ExecuteEvents.Execute(handle.gameObject, data, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(handle.gameObject, data, ExecuteEvents.pointerUpHandler);
        }

        static float OffGrid(float value, float step) => Mathf.Abs(value / step - Mathf.Round(value / step));

        [UnityTest]
        public IEnumerator DraggingAButtonSnapsItInsideTheSafeAreaAndSavingMovesTheHud()
        {
            yield return ShowHud();
            var spell = HudPart(TouchPart.Spell);
            float classicX = spell.anchorMin.x;
            var editor = TouchLayoutEditor.Show();
            yield return null;
            Assert.AreEqual(TouchTemplate.Classic, editor.Target);
            var before = editor.Draft[TouchPart.Spell];
            Drag(HandleOf(editor, TouchPart.Spell), new Vector2(-Screen.width * .2f, Screen.height * .25f));
            Assert.AreEqual(TouchPart.Spell, editor.Selected, "Touching a button selects it.");
            var after = editor.Draft[TouchPart.Spell];
            Assert.Less(after.X, before.X - .1f);
            Assert.Greater(after.Y, before.Y + .1f);
            Assert.Less(OffGrid(after.X, TouchLayout.GridX), 1e-3f, "It snaps to the grid.");
            Assert.Less(OffGrid(after.Y, TouchLayout.GridY), 1e-3f);
            Assert.IsTrue(editor.Dirty);
            Assert.AreEqual(TouchTemplate.Custom1, editor.Target, "Classic is read-only, so the copy goes to Custom 1.");
            Assert.AreEqual(1f, editor.Card.GetComponent<CanvasGroup>().alpha, 1e-5f, "The tools come back after the drag.");

            Drag(HandleOf(editor, TouchPart.Spell), new Vector2(-Screen.width * 3f, -Screen.height * 3f));
            var cornered = editor.Draft[TouchPart.Spell];
            var corner = TouchLayout.Clamp(new TouchSpot(0f, 0f, cornered.Size), editor.SafeSize.x / editor.SafeSize.y);
            Assert.AreEqual(corner.X, cornered.X, 1e-4f, "A button can't leave the safe area.");
            Assert.AreEqual(corner.Y, cornered.Y, 1e-4f);
            Assert.Greater(cornered.X, 0f);
            yield return null;
            Assert.AreEqual(classicX, spell.anchorMin.x, 1e-5f, "Nothing changes in play before Save.");

            Click(editor, "Touch save");
            yield return null;
            Assert.IsFalse(TouchLayoutEditor.Current);
            Assert.AreEqual(TouchTemplate.Custom1, TouchLayouts.Book.Active);
            Assert.AreEqual(cornered.X, spell.anchorMin.x, 1e-4f, "Saving moves the HUD button.");
            Assert.AreEqual(cornered.Y, spell.anchorMin.y, 1e-4f);
            var saved = TouchLayoutBook.Load(folder, out string problem);
            Assert.IsNull(problem);
            Assert.AreEqual(TouchTemplate.Custom1, saved.Active);
            Assert.AreEqual(cornered.X, saved.Stored(TouchTemplate.Custom1)[TouchPart.Spell].X, 1e-4f);
        }

        [UnityTest]
        public IEnumerator MicAndSpeakerArePlaceableInTheEditorAndNeverBuiltInHousePlay()
        {
            yield return ShowHud();
            var editor = TouchLayoutEditor.Show();
            yield return null;
            foreach (var part in new[] { TouchPart.Mic, TouchPart.Speaker })
            {
                Assert.IsNotNull(HandleOf(editor, part), part + " can be placed in the editor.");
                Assert.IsNull(HudPart(part), part + " is never built in House play.");
            }
            var before = editor.Draft[TouchPart.Mic];
            Drag(HandleOf(editor, TouchPart.Mic), new Vector2(-Screen.width * .2f, -Screen.height * .3f));
            Assert.AreEqual(TouchPart.Mic, editor.Selected);
            var after = editor.Draft[TouchPart.Mic];
            Assert.Less(after.X, before.X - .1f);
            Assert.Less(after.Y, before.Y - .1f);
            Click(editor, "Touch save");
            yield return null;
            var saved = TouchLayoutBook.Load(folder, out string problem);
            Assert.IsNull(problem);
            Assert.AreEqual(after.X, saved.Stored(TouchTemplate.Custom1)[TouchPart.Mic].X, 1e-4f);
            Assert.AreEqual(after.Y, saved.Stored(TouchTemplate.Custom1)[TouchPart.Mic].Y, 1e-4f);
            Assert.IsNull(HudPart(TouchPart.Mic), "Saving a layout still adds no voice button to House play.");
            Assert.IsNotNull(HudPart(TouchPart.Spell));
        }

        [UnityTest]
        public IEnumerator ChoosingATemplateAppliesItInPlayAtOnce()
        {
            yield return ShowHud();
            var editor = TouchLayoutEditor.Show();
            yield return null;
            Click(editor, "Touch template Claw");
            yield return null;
            Assert.AreEqual(TouchTemplate.Claw, TouchLayouts.Book.Active);
            var size = HudRoot.rect.size;
            var claw = TouchLayout.Claw(size.x, size.y);
            foreach (var part in new[] { TouchPart.Spell, TouchPart.Attack, TouchPart.Move, TouchPart.Eye })
            {
                Assert.AreEqual(claw[part].X, HudPart(part).anchorMin.x, 1e-4f, part.ToString());
                Assert.AreEqual(claw[part].Y, HudPart(part).anchorMin.y, 1e-4f, part.ToString());
            }
            Assert.IsFalse(editor.Dirty);
            Assert.AreEqual(claw[TouchPart.Spell].X, editor.Draft[TouchPart.Spell].X, 1e-3f, "The editor shows the chosen template.");
            Click(editor, "Touch cancel");
            yield return null;
            Assert.IsFalse(TouchLayoutEditor.Current);
            Assert.AreEqual(TouchTemplate.Claw, TouchLayouts.Book.Active, "Cancel keeps the template you chose.");
            Assert.AreEqual(TouchTemplate.Claw, TouchLayoutBook.Load(folder, out _).Active);
        }

        [UnityTest]
        public IEnumerator CancelDropsEditsAndBadCodesExplainThemselves()
        {
            var editor = TouchLayoutEditor.Show();
            yield return null;
            editor.Move(TouchPart.Jump, new Vector2(.5f, .5f));
            Assert.IsTrue(editor.Dirty);
            Assert.IsFalse(editor.Paste("nonsense"));
            Assert.AreEqual(TouchLayout.NotACode, editor.Status);
            Assert.IsFalse(editor.Paste(null));
            Assert.AreEqual(TouchLayout.NoCode, editor.Status);
            Assert.IsFalse(editor.Paste("wb-touch:9:abc:0000"));
            Assert.AreEqual(TouchLayout.Newer, editor.Status);
            string code = TouchLayout.Classic(1920f, 1080f).Encode();
            Assert.IsFalse(editor.Paste(code.Substring(0, 20) + (code[20] == 'A' ? 'B' : 'A') + code.Substring(21)));
            Assert.AreEqual(TouchLayout.Damaged, editor.Status);
            Click(editor, "Touch copy code");
            Assert.AreEqual("Code copied.", editor.Status);
            Assert.IsTrue(TouchLayout.TryDecode(editor.Draft.Encode(), out _, out _));
            var claw = TouchLayout.Claw(editor.SafeSize.x, editor.SafeSize.y);
            Assert.IsTrue(editor.Paste(claw.Encode()));
            Assert.AreEqual("Layout pasted. Save to keep it.", editor.Status);
            Assert.AreEqual(claw[TouchPart.Spell].X, editor.Draft[TouchPart.Spell].X, 1e-3f);
            Click(editor, "Touch cancel");
            yield return null;
            Assert.AreEqual(TouchTemplate.Classic, TouchLayouts.Book.Active);
            Assert.IsFalse(File.Exists(Path.Combine(folder, TouchLayoutBook.FileName)), "Cancel saves nothing.");
        }

        [UnityTest]
        public IEnumerator SlidersResetAndOverlapsWorkOnTheSelectedButton()
        {
            var editor = TouchLayoutEditor.Show();
            yield return null;
            var classic = editor.Draft;
            var sliders = editor.GetComponentsInChildren<Slider>();
            var size = sliders.Single(s => s.name == "Touch size");
            Assert.IsFalse(size.interactable, "Nothing is selected yet.");
            editor.Select(TouchPart.Spell);
            Assert.IsTrue(size.interactable);
            size.value = .25f;
            Assert.AreEqual(.25f, editor.Draft[TouchPart.Spell].Size, 1e-5f);
            sliders.Single(s => s.name == "Touch opacity").value = .5f;
            Assert.AreEqual(.5f, editor.Draft[TouchPart.Spell].Opacity, 1e-5f);
            sliders.Single(s => s.name == "Touch layout opacity").value = .6f;
            Assert.AreEqual(.6f, editor.Draft.Opacity, 1e-5f);
            Click(editor, "Touch reset control");
            Assert.AreEqual(classic[TouchPart.Spell], editor.Draft[TouchPart.Spell]);
            Assert.AreEqual(.6f, editor.Draft.Opacity, 1e-5f, "Reset control leaves the rest alone.");

            var jump = editor.Draft[TouchPart.Jump];
            editor.Move(TouchPart.Spell, new Vector2(jump.X, jump.Y));
            StringAssert.Contains("overlaps", editor.Status, "Overlaps are a warning.");
            Click(editor, "Touch reset layout");
            Assert.IsTrue(editor.Draft.Matches(classic));
            editor.Move(TouchPart.Spell, new Vector2(jump.X, jump.Y));
            Click(editor, "Touch save");
            yield return null;
            Assert.IsFalse(TouchLayoutEditor.Current, "Saving works with an overlap.");
            Assert.AreEqual(TouchTemplate.Custom1, TouchLayouts.Book.Active);
            Assert.IsNotEmpty(TouchLayouts.Book.Stored(TouchTemplate.Custom1).Overlaps(editor.SafeSize.x / editor.SafeSize.y));
        }

        [UnityTest]
        public IEnumerator TheCardFitsPhoneShapesAndMatchesTheHudScale()
        {
            var editor = TouchLayoutEditor.Show();
            yield return null;
            foreach (var (width, height) in new[] { (2340, 1080), (1920, 1080), (2048, 1536), (1080, 2340) })
            {
                var space = TouchLayoutEditor.SafeSizeFor(new Rect(0, 0, width, height), width, height);
                Assert.AreEqual(Mathf.Sqrt(1920f * 1080f), Mathf.Sqrt(space.x * space.y), 1f, "The safe area keeps the HUD's canvas units.");
                editor.FitCard(space);
                var card = editor.Card.sizeDelta;
                Assert.LessOrEqual(card.x, space.x - 2f * TouchLayoutEditor.Gutter + .01f, $"{width}x{height}");
                Assert.LessOrEqual(card.y, space.y - 2f * TouchLayoutEditor.Gutter + .01f, $"{width}x{height}");
                if (width > height) Assert.AreEqual(TouchLayoutEditor.CardWidth, card.x, .01f, $"Landscape phones get the full card at {width}x{height}.");
            }
            var notch = TouchLayoutEditor.SafeSizeFor(new Rect(100, 0, 2140, 1080), 2340, 1080);
            Assert.Less(notch.x, TouchLayoutEditor.SafeSizeFor(new Rect(0, 0, 2340, 1080), 2340, 1080).x);
            Click(editor, "Touch hide tools");
            Assert.IsFalse(editor.Card.gameObject.activeSelf);
            Click(editor, "Touch show tools");
            Assert.IsTrue(editor.Card.gameObject.activeSelf);
            Assert.AreSame(editor, TouchLayoutEditor.Show(), "Opening twice keeps one editor.");
        }
    }
}
