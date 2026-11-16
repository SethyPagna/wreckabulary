using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [DefaultExecutionOrder(-200)]
    public sealed class TouchLayoutEditor : MonoBehaviour
    {
        public const int SortOrder = 900;
        public const float CardWidth = 760f, CardHeight = 560f, Gutter = 16f, MessageSeconds = 4f;
        public const string Hint = "Drag a button to move it. Pinch or use Size to resize it.";
        const int HandleRadius = 64;
        static readonly Color Scrim = new Color(.03f, .08f, .09f, .55f), Outside = new Color(0f, 0f, 0f, .45f), GridLine = new Color(1f, .97f, .9f, .08f),
            GridMiddle = new Color(1f, .97f, .9f, .22f), HandleFill = LobbyKit.Hex(0xfff0d9), HandleInk = LobbyKit.Hex(0x173b3c);

        public static TouchLayoutEditor Current { get; private set; }

        readonly Handle[] handles = new Handle[TouchLayout.Count];
        readonly RectTransform[] outside = new RectTransform[4];
        RectTransform safe, handleLayer, card, chipRow, showTools;
        CanvasGroup cardGroup;
        TouchLayoutBook book;
        TouchLayout draft;
        TouchTemplate target, basis;
        TouchPart? selected;
        bool dirty, pinching, closing;
        float pinchFrom, pinchSize, messageUntil;
        string message = "";
        Action closed;
        IDisposable lease;
        GameObject previousSelection;
        TextMeshProUGUI targetLine, partLine, status;
        Slider sizeSlider, opacitySlider, layoutSlider;
        Button resetPart;
        TMP_InputField nameField;
        Vector2 safeSize = new Vector2(1920f, 1080f);
        Rect lastSafe;
        int lastWidth, lastHeight;

        public TouchLayout Draft => draft;
        public TouchTemplate Target => target;
        public TouchPart? Selected => selected;
        public bool Dirty => dirty;
        public string Status => status ? status.text : "";
        public Vector2 SafeSize => safeSize;
        public RectTransform Card => card;
        float Aspect => safeSize.x / Mathf.Max(1f, safeSize.y);
        float Side => Mathf.Min(safeSize.x, safeSize.y);

        public static void Open() => Show();

        public static TouchLayoutEditor Show(Action closed = null)
        {
            if (Current) { Current.closed += closed; return Current; }
            var host = new GameObject("Touch layout editor", typeof(RectTransform));
            var canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortOrder;
            var scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            host.AddComponent<GraphicRaycaster>();
            if (!EventSystem.current)
                new GameObject("UI Events", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var editor = host.AddComponent<TouchLayoutEditor>();
            editor.closed = closed;
            editor.Build();
            return editor;
        }

        public static Vector2 SafeSizeFor(Rect safeArea, int width, int height)
        {
            float scale = Mathf.Sqrt(Mathf.Max(1, width) / 1920f * (Mathf.Max(1, height) / 1080f));
            return safeArea.size / Mathf.Max(.0001f, scale);
        }

        void Build()
        {
            Current = this;
            lease = KeyBindings.BlockGameplay();
            TouchBinding.Shared.ReleaseAll();
            previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            book = TouchLayouts.Book.Clone();
            target = book.Active;
            basis = target == TouchTemplate.Claw ? TouchTemplate.Claw : TouchTemplate.Classic;
            var root = (RectTransform)transform;
            var scrim = LobbyKit.Rect(root, "Scrim").Fill();
            scrim.Paint(Scrim).raycastTarget = true;
            scrim.gameObject.AddComponent<Backdrop>().Editor = this;
            for (int i = 0; i < outside.Length; i++) outside[i] = Tint(root);
            safe = LobbyKit.Rect(root, "Safe area");
            BuildGrid();
            LobbyKit.Frame(safe, LobbyKit.Cyan, 0, 2, "Safe edge");
            handleLayer = LobbyKit.Rect(safe, "Handles").Fill();
            foreach (var part in TouchLayout.Parts) handles[(int)part] = MakeHandle(part);
            BuildCard();
            Fit();
            if (TouchLayouts.LoadProblem != null) Say(TouchLayouts.LoadProblem);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        static RectTransform Tint(RectTransform root)
        {
            var rect = LobbyKit.Rect(root, "Outside safe area");
            rect.Paint(Outside).raycastTarget = false;
            return rect;
        }

        void BuildGrid()
        {
            var grid = LobbyKit.Rect(safe, "Snap grid").Fill();
            int across = Mathf.RoundToInt(1f / TouchLayout.GridX), up = Mathf.RoundToInt(1f / TouchLayout.GridY);
            for (int i = 1; i < across; i++)
                Line(grid, new Vector2(i * TouchLayout.GridX, 0f), new Vector2(i * TouchLayout.GridX, 1f), new Vector2(-1f, 0f), new Vector2(1f, 0f), i * 2 == across);
            for (int i = 1; i < up; i++)
                Line(grid, new Vector2(0f, i * TouchLayout.GridY), new Vector2(1f, i * TouchLayout.GridY), new Vector2(0f, -1f), new Vector2(0f, 1f), i * 2 == up);
        }

        static void Line(RectTransform grid, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax, bool middle) =>
            LobbyKit.Rect(grid, "Line").Place(min, max, offsetMin, offsetMax).Paint(middle ? GridMiddle : GridLine).raycastTarget = false;

        Handle MakeHandle(TouchPart part)
        {
            var rect = LobbyKit.Rect(handleLayer, "Handle " + TouchLayout.Label(part));
            rect.pivot = new Vector2(.5f, .5f);
            var fill = rect.gameObject.AddComponent<Image>();
            fill.sprite = LobbyIcons.RoundedSprite(HandleRadius);
            fill.raycastTarget = true;
            var ring = LobbyKit.Rect(rect, "Selected").Place(Vector2.zero, Vector2.one, new Vector2(-5f, -5f), new Vector2(5f, 5f)).gameObject.AddComponent<Image>();
            ring.sprite = LobbyIcons.FrameSprite(HandleRadius, 5);
            ring.color = LobbyKit.Sun; ring.raycastTarget = false;
            var label = LobbyKit.Text(rect, TouchLayout.Label(part), 18f, HandleInk, TextAlignmentOptions.Center, FontStyles.Bold);
            label.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            label.enableAutoSizing = true; label.fontSizeMin = 9f; label.fontSizeMax = 18f;
            label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Overflow;
            var handle = rect.gameObject.AddComponent<Handle>();
            handle.Bind(this, part, fill, ring);
            return handle;
        }

        void BuildCard()
        {
            card = LobbyKit.Rect(safe, "Touch layout card").Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
            LobbyKit.PanelFace(card, 18);
            cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            var column = LobbyKit.Column(card, "Tools", 8, 20).Fill();

            var header = Row(column, "Header", 36);
            LobbyKit.Caps(header, "Touch layout", 18).Size(-1, 30, 1);
            targetLine = LobbyKit.Text(header, "", 16, LobbyKit.Muted, TextAlignmentOptions.MidlineRight);
            targetLine.Size(300, 30);
            LobbyKit.Pill(header, "Touch hide tools", "Hide", 16, () => ShowTools(false)).Size(90, 36);

            chipRow = Row(column, "Templates", 46);
            chipRow.GetComponent<HorizontalLayoutGroup>().spacing = 6;

            var naming = Row(column, "Name", 40);
            LobbyKit.Text(naming, "Name", 18, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft).Size(170, 40);
            nameField = NameField(naming);

            var picked = Row(column, "Selected", 40);
            partLine = LobbyKit.Text(picked, "", 18, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            partLine.Size(-1, 40, 1);
            resetPart = LobbyKit.Pill(picked, "Touch reset control", "Reset control", 16, ResetPart);
            resetPart.Size(190, 40);

            sizeSlider = SliderRow(column, "Size", "Touch size", TouchLayout.MinSize, TouchLayout.MaxSize, value => { if (selected is TouchPart part) Resize(part, value); });
            opacitySlider = SliderRow(column, "Opacity", "Touch opacity", TouchLayout.MinOpacity, 1f, value => { if (selected is TouchPart part) Fade(part, value); });
            layoutSlider = SliderRow(column, "Layout opacity", "Touch layout opacity", TouchLayout.MinOpacity, 1f, FadeLayout);

            var actions = Row(column, "Code", 46);
            actions.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            LobbyKit.Pill(actions, "Touch copy code", "Copy code", 16, CopyCode).Size(-1, 44, 1);
            LobbyKit.Pill(actions, "Touch paste code", "Paste code", 16, PasteCode).Size(-1, 44, 1);
            LobbyKit.Pill(actions, "Touch reset layout", "Reset layout", 16, ResetLayout).Size(-1, 44, 1);

            status = LobbyKit.Text(column, Hint, 16, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            status.name = "Touch layout status";
            status.Size(-1, 30);

            var bottom = Row(column, "Finish", 52);
            LobbyKit.Pill(bottom, "Touch cancel", "Cancel", 20, Cancel).Size(170, 52);
            LobbyKit.Rect(bottom, "Space").Size(-1, 52, 1);
            var save = LobbyKit.Button(bottom, "Touch save", LobbyKit.PrimaryFill, Save, 6);
            LobbyKit.Text(save.Body(), "Save", 22, LobbyKit.PrimaryInk, TextAlignmentOptions.Center).rectTransform.Fill();
            save.Size(170, 52);

            var show = LobbyKit.Button(safe, "Touch show tools", LobbyKit.PrimaryFill, () => ShowTools(true), 8);
            LobbyKit.Text(show.Body(), "Show tools", 18, LobbyKit.PrimaryInk, TextAlignmentOptions.Center).rectTransform.Fill();
            showTools = ((RectTransform)show.transform).Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(180f, 48f));
            showTools.gameObject.SetActive(false);
            RebuildChips();
        }

        static RectTransform Row(RectTransform column, string name, float height)
        {
            var row = LobbyKit.Row(column, name, 10);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            row.Size(-1, height);
            return row;
        }

        Slider SliderRow(RectTransform column, string label, string name, float min, float max, Action<float> changed)
        {
            var row = Row(column, label, 40);
            LobbyKit.Text(row, label, 18, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft).Size(170, 40);
            var holder = LobbyKit.Rect(row, name);
            holder.Size(-1, 40, 1);
            var track = LobbyKit.Rect(holder, "Track").Place(new Vector2(0f, .5f), new Vector2(1f, .5f), new Vector2(12f, -5f), new Vector2(-12f, 5f));
            track.Paint(LobbyKit.Track, 5).raycastTarget = true;
            var fillArea = LobbyKit.Rect(holder, "Fill area").Place(new Vector2(0f, .5f), new Vector2(1f, .5f), new Vector2(12f, -5f), new Vector2(-12f, 5f));
            var fill = LobbyKit.Rect(fillArea, "Fill");
            fill.Paint(LobbyKit.Cyan, 5).raycastTarget = false;
            fill.sizeDelta = Vector2.zero;
            var area = LobbyKit.Rect(holder, "Handle area").Place(Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));
            var knob = LobbyKit.Rect(area, "Handle");
            knob.sizeDelta = new Vector2(28f, -12f);
            var knobImage = knob.Paint(LobbyKit.Sun, 14);
            var slider = holder.gameObject.AddComponent<Slider>();
            slider.fillRect = fill; slider.handleRect = knob; slider.targetGraphic = knobImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min; slider.maxValue = max;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.SetValueWithoutNotify(max);
            slider.onValueChanged.AddListener(value => changed(value));
            return slider;
        }

        TMP_InputField NameField(RectTransform row)
        {
            var holder = LobbyKit.Rect(row, "Touch layout name");
            holder.gameObject.SetActive(false);
            holder.Size(-1, 40, 1);
            holder.Paint(LobbyKit.Raised, 6);
            LobbyKit.Frame(holder, LobbyKit.Line, 6, 1);
            var area = LobbyKit.Rect(holder, "Text area").Place(Vector2.zero, Vector2.one, new Vector2(12f, 2f), new Vector2(-12f, -2f));
            area.gameObject.AddComponent<RectMask2D>();
            var text = LobbyKit.Text(area, "", 18, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            text.overflowMode = TextOverflowModes.Overflow;
            text.rectTransform.Fill();
            var field = holder.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.characterLimit = TouchLayoutBook.NameLength;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.customCaretColor = true;
            field.caretColor = LobbyKit.Navy;
            field.onEndEdit.AddListener(value => field.SetTextWithoutNotify(TouchLayoutBook.CleanName(value)));
            holder.gameObject.SetActive(true);
            return field;
        }

        void RebuildChips()
        {
            LobbyKit.Clear(chipRow);
            foreach (var template in TouchLayoutBook.Templates)
            {
                var chip = LobbyKit.Chip(chipRow, book.Name(template), template == target, () => Choose(template), !book.Has(template), 17);
                chip.name = "Touch template " + template;
                chip.Size(-1, 44, 1);
            }
            bool custom = TouchLayoutBook.IsCustom(target);
            nameField.SetTextWithoutNotify(book.Name(target));
            nameField.interactable = custom;
            targetLine.text = !custom ? "Built in" : dirty && !book.Has(target) ? "Saves to " + book.Name(target) : dirty ? "Save replaces " + book.Name(target) : "Yours";
        }

        void Update()
        {
            if (closing) return;
            if (Screen.width != lastWidth || Screen.height != lastHeight || Screen.safeArea != lastSafe) Fit();
            if (BackPressed()) { Cancel(); return; }
            Pinch();
            if (messageUntil > 0f && Time.unscaledTime > messageUntil) { messageUntil = 0f; message = ""; ShowStatus(); }
        }

        bool BackPressed()
        {
            if (nameField && nameField.isFocused) return false;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { KeyBindings.QuietInput(keyboard.escapeKey); return true; }
            foreach (var pad in Gamepad.all)
                if (pad.buttonEast.wasPressedThisFrame) { KeyBindings.QuietInput(pad.buttonEast); return true; }
            return false;
        }

        void Pinch()
        {
            var screen = Touchscreen.current;
            if (screen == null || !(selected is TouchPart part)) { pinching = false; return; }
            int found = 0;
            Vector2 first = default, second = default;
            foreach (var touch in screen.touches)
            {
                if (!touch.press.isPressed) continue;
                if (found == 0) first = touch.position.ReadValue();
                else second = touch.position.ReadValue();
                if (++found == 2) break;
            }
            if (found < 2) { pinching = false; return; }
            float distance = Vector2.Distance(first, second);
            if (!pinching) { pinching = true; pinchFrom = distance; pinchSize = draft[part].Size; return; }
            Resize(part, TouchLayout.Pinch(pinchSize, pinchFrom, distance));
        }

        public void Fit()
        {
            lastWidth = Screen.width; lastHeight = Screen.height; lastSafe = Screen.safeArea;
            float width = Mathf.Max(1, lastWidth), height = Mathf.Max(1, lastHeight);
            Vector2 min = new Vector2(lastSafe.xMin / width, lastSafe.yMin / height), max = new Vector2(lastSafe.xMax / width, lastSafe.yMax / height);
            safe.Place(min, max);
            outside[0].Place(Vector2.zero, new Vector2(min.x, 1f));
            outside[1].Place(new Vector2(max.x, 0f), Vector2.one);
            outside[2].Place(new Vector2(min.x, 0f), new Vector2(max.x, min.y));
            outside[3].Place(new Vector2(min.x, max.y), new Vector2(max.x, 1f));
            safeSize = SafeSizeFor(lastSafe, lastWidth, lastHeight);
            FitCard(safeSize);
            draft = dirty && draft != null ? draft.Clamped(Aspect) : book.Resolve(target, safeSize.x, safeSize.y);
            Refresh();
        }

        public void FitCard(Vector2 space) =>
            card.sizeDelta = new Vector2(Mathf.Min(CardWidth, space.x - 2f * Gutter), Mathf.Min(CardHeight, space.y - 2f * Gutter));

        void Refresh()
        {
            if (draft == null) return;
            var overlaps = draft.Overlaps(Aspect);
            foreach (var handle in handles)
            {
                bool clash = false;
                foreach (var (a, b) in overlaps) clash |= a == handle.Part || b == handle.Part;
                handle.Show(draft[handle.Part], draft.Opacity, Side, selected == handle.Part, clash);
            }
            bool any = selected is TouchPart;
            var spot = any ? draft[selected.Value] : default;
            partLine.text = any ? TouchLayout.Label(selected.Value) : "Tap a button to change it.";
            sizeSlider.interactable = opacitySlider.interactable = resetPart.interactable = any;
            if (any)
            {
                sizeSlider.SetValueWithoutNotify(spot.Size);
                opacitySlider.SetValueWithoutNotify(spot.Opacity);
            }
            layoutSlider.SetValueWithoutNotify(draft.Opacity);
            ShowStatus(overlaps);
        }

        void ShowStatus(System.Collections.Generic.IReadOnlyList<(TouchPart a, TouchPart b)> overlaps = null)
        {
            if (!status || draft == null) return;
            string warning = TouchLayout.OverlapWarning(overlaps ?? draft.Overlaps(Aspect));
            status.text = message.Length > 0 ? message : warning.Length > 0 ? warning : Hint;
            status.color = message.Length == 0 && warning.Length > 0 ? LobbyKit.Hot : LobbyKit.Muted;
        }

        void Say(string text)
        {
            message = text ?? "";
            messageUntil = Time.unscaledTime + MessageSeconds;
            ShowStatus();
        }

        void ShowTools(bool on)
        {
            card.gameObject.SetActive(on);
            showTools.gameObject.SetActive(!on);
        }

        void Dragging(bool on)
        {
            cardGroup.alpha = on ? .2f : 1f;
            cardGroup.blocksRaycasts = !on;
        }

        public void Select(TouchPart? part)
        {
            selected = part;
            Refresh();
        }

        public void Choose(TouchTemplate template)
        {
            target = template;
            if (book.Has(template))
            {
                basis = template == TouchTemplate.Claw ? TouchTemplate.Claw : TouchTemplate.Classic;
                draft = book.Resolve(template, safeSize.x, safeSize.y);
                dirty = false;
                book.Active = template;
                Say(TouchLayouts.Select(template, out string error) ? book.Name(template) + " is on." : error);
            }
            else
            {
                dirty = true;
                Say("Save to keep this layout in " + book.Name(template) + ".");
            }
            RebuildChips();
            Refresh();
        }

        void Edit(TouchLayout next)
        {
            draft = next;
            message = "";
            messageUntil = 0f;
            if (!dirty)
            {
                dirty = true;
                if (!TouchLayoutBook.IsCustom(target))
                {
                    target = book.SaveTarget(target);
                    Say((book.Has(target) ? "Editing a copy. Save replaces " : "Editing a copy. Save puts it in ") + book.Name(target) + ".");
                }
                RebuildChips();
            }
            Refresh();
        }

        public void Move(TouchPart part, Vector2 centre) => Edit(draft.With(part, TouchLayout.Snap(draft[part].At(centre.x, centre.y), Aspect)));
        public void Resize(TouchPart part, float size) => Edit(draft.With(part, TouchLayout.Clamp(draft[part].Sized(size), Aspect)));
        public void Fade(TouchPart part, float opacity) => Edit(draft.With(part, draft[part].Faded(opacity)));
        public void FadeLayout(float opacity) => Edit(draft.WithOpacity(opacity));

        TouchLayout Basis => basis == TouchTemplate.Claw ? TouchLayout.Claw(safeSize.x, safeSize.y) : TouchLayout.Classic(safeSize.x, safeSize.y);

        public void ResetPart()
        {
            if (selected is TouchPart part) Edit(draft.With(part, Basis[part]));
        }

        public void ResetLayout()
        {
            if (TouchLayoutBook.IsCustom(target)) { Edit(Basis); return; }
            draft = Basis;
            dirty = false;
            RebuildChips();
            Refresh();
        }

        public void CopyCode()
        {
            GUIUtility.systemCopyBuffer = draft.Encode();
            Say("Code copied.");
        }

        public void PasteCode() => Paste(GUIUtility.systemCopyBuffer);

        public bool Paste(string text)
        {
            if (!TouchLayout.TryDecode(text, out var layout, out string problem)) { Say(problem); return false; }
            Edit(layout.Clamped(Aspect));
            Say("Layout pasted. Save to keep it.");
            return true;
        }

        public void Save()
        {
            if (TouchLayoutBook.IsCustom(target))
            {
                if (dirty || !book.Has(target)) book.Store(target, draft);
                book.Rename(target, nameField.text);
                book.Active = target;
                if (!TouchLayouts.Commit(book, out string error)) { Say(error); return; }
            }
            Close();
        }

        public void Cancel() => Close();

        void Close()
        {
            if (closing) return;
            closing = true;
            lease?.Dispose();
            lease = null;
            if (EventSystem.current && previousSelection) EventSystem.current.SetSelectedGameObject(previousSelection);
            var done = closed;
            closed = null;
            Destroy(gameObject);
            done?.Invoke();
        }

        void OnDestroy()
        {
            lease?.Dispose();
            if (Current == this) Current = null;
        }

        Vector2 Fraction(Vector2 screen) =>
            new Vector2((screen.x - lastSafe.x) / Mathf.Max(1f, lastSafe.width), (screen.y - lastSafe.y) / Mathf.Max(1f, lastSafe.height));

        sealed class Backdrop : MonoBehaviour, IPointerClickHandler
        {
            public TouchLayoutEditor Editor;
            public void OnPointerClick(PointerEventData e) { if (Editor) Editor.Select(null); }
        }

        sealed class Handle : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
        {
            TouchLayoutEditor editor;
            Image fill, ring;
            RectTransform rect;
            Vector2 grab;
            int pointer = int.MinValue;
            public TouchPart Part { get; private set; }

            public void Bind(TouchLayoutEditor owner, TouchPart part, Image body, Image selection)
            {
                editor = owner; Part = part; fill = body; ring = selection; rect = (RectTransform)transform;
            }

            public void Show(TouchSpot spot, float layoutOpacity, float side, bool selected, bool clash)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(spot.X, spot.Y);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.one * (spot.Size * side);
                var colour = clash ? LobbyKit.Hot : HandleFill;
                colour.a = .3f + .7f * spot.Opacity * layoutOpacity;
                fill.color = colour;
                ring.enabled = selected;
                if (selected) rect.SetAsLastSibling();
            }

            public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

            public void OnPointerDown(PointerEventData e)
            {
                if (pointer != int.MinValue) return;
                pointer = e.pointerId;
                var spot = editor.draft[Part];
                grab = new Vector2(spot.X, spot.Y) - editor.Fraction(e.position);
                editor.Select(Part);
                editor.Dragging(true);
            }

            public void OnDrag(PointerEventData e)
            {
                if (e.pointerId != pointer || editor.pinching) return;
                editor.Move(Part, editor.Fraction(e.position) + grab);
            }

            public void OnPointerUp(PointerEventData e)
            {
                if (e.pointerId != pointer) return;
                pointer = int.MinValue;
                editor.Dragging(false);
            }

            void OnDisable()
            {
                if (pointer == int.MinValue || !editor) return;
                pointer = int.MinValue;
                editor.Dragging(false);
            }
        }
    }
}
