using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class LobbyHomePageView : MonoBehaviour
    {
        public const string PlayButton = "Home PLAY", PlayLabel = "PLAY";
        public const string LastLine = "Last match", ChangeButton = "Home CHANGE", ChangeLabel = "Change";
        public const string LastPlate = "Last match plate", ModePicture = "Mode picture", ChangeRow = "Change row";
        public const string CareerLine = "Play time";
        public const float PlayHeight = 92f, PlayText = 56f;
        public const float LastText = 22f, LastTextMinimum = 18f, ChangeHeight = 44f, ChangeText = 18f;
        public const int HouseScale = 85;
        const float LastHeight = 60f, LastGap = 10f, PictureSize = 48f, PictureArt = 40f, PictureTilt = 6f;
        const float ChangeWidth = 132f, ChangePad = 18f;
        const int PictureRadius = 12, PictureEdge = 2, ChangeRadius = 12, ChangeEdge = 1, LastPad = 2;
        const float PictureLight = .25f, PictureDark = .2f;
        const float CareerHeight = 22f, CareerText = 16f, CareerTextMinimum = 15f;
        static readonly MatchLines Lines = new MatchLines();
        public RectTransform TurnHint;
        public RectTransform Dock;
        public LobbyShopActionView Start;
        public LobbyShopActionView Mode;
        public LobbyShopActionView House;
        public LobbyShopActionView Wordbound;
        public TextMeshProUGUI WordboundNote;

        sealed class MatchLines : ITextPreprocessor
        {
            public string PreprocessText(string text)
            {
                int split = string.IsNullOrEmpty(text) ? -1 : text.IndexOf(MapCardText.Separator, StringComparison.Ordinal);
                if (split < 0) return text;
                string house = text.Substring(split + MapCardText.Separator.Length);
                return text.Substring(0, split) + "\n<size=" + HouseScale + "%><color=#" + ColorUtility.ToHtmlStringRGB(LobbyKit.Muted) + ">" +
                    house + "</color></size>";
            }
        }

        public void Validate()
        {
            LobbyShopLayout.ValidateTree(this);
            foreach (var reference in new Component[] { TurnHint, Dock, Start, Mode, House, Wordbound, WordboundNote })
                LobbyShopLayout.Require(this, reference, "Home binding");
            var actions = new[] { Start, Mode, House, Wordbound };
            if (TurnHint.IsChildOf(Dock) || Dock.IsChildOf(TurnHint) || !WordboundNote.transform.IsChildOf(Dock) ||
                actions.Distinct().Count() != actions.Length)
                throw new InvalidOperationException("Home needs a separate turn hint and dock with four distinct actions.");
            foreach (var action in actions)
            {
                action.Validate();
                if (!action.transform.IsChildOf(Dock) || WordboundNote.transform.IsChildOf(action.transform) ||
                    action.transform.IsChildOf(WordboundNote.transform) ||
                    actions.Any(other => other != action && other.transform.IsChildOf(action.transform)))
                    throw new InvalidOperationException("Home actions and the Wordbound note must have independent visibility inside the dock.");
            }
        }

        public Selectable Bind(bool showTurnHint, bool starting, Action play) => Bind(showTurnHint, starting, play, null, null);

        public Selectable Bind(bool showTurnHint, bool starting, Action play, string last, Action change) =>
            Bind(showTurnHint, starting, play, last, change, null);

        public Selectable Bind(bool showTurnHint, bool starting, Action play, string last, Action change, string mode)
        {
            Validate();
            TurnHint.name = "Turn hint"; TurnHint.gameObject.SetActive(showTurnHint);
            Dock.name = "Next match";
            Start.IdleInk = Start.ActiveInk = LobbyKit.PrimaryInk;
            Start.Underline.color = Color.clear;
            if (!Start.Press.Body.GetComponent<Image>())
                LobbyKit.Face(Start.Press.Body, LobbyKit.PrimaryFill, 16, drop: 5, dropColour: LobbyKit.Hex(0xe8a93a));
            Start.Bind(PlayButton, PlayLabel, starting ? null : play, true);
            var size = Start.GetComponent<LayoutElement>();
            if (size) size.preferredHeight = size.minHeight = Mathf.Max(size.preferredHeight, PlayHeight);
            Start.Label.fontSizeMax = Mathf.Max(Start.Label.fontSizeMax, PlayText);
            Start.Label.fontSize = Start.Label.fontSizeMax;
            Start.Label.Size(-1, Start.Label.fontSizeMax + 10).GetComponent<LayoutElement>().minWidth = 0;
            LobbyParcel.ShippingLabel(Start.Press.Body);
            foreach (var other in new[] { Mode, House, Wordbound })
            {
                other.Bind(other.name, other.Label.text, null, false);
                other.gameObject.SetActive(false);
            }
            WordboundNote.gameObject.SetActive(false);
            ShowLast(starting ? null : last, mode, change);
            Dock.gameObject.SetActive(!starting);
            return starting ? null : Start.Button;
        }

        void ShowLast(string last, string mode, Action change)
        {
            Remove(LastPlate); Remove(ChangeRow); Remove(LastLine); Remove(ChangeButton);
            if (string.IsNullOrEmpty(last)) return;
            var slot = Start.transform;
            while (slot.parent != Dock) slot = slot.parent;
            var plate = Plate(last, mode);
            plate.SetSiblingIndex(slot.GetSiblingIndex());
            var row = LobbyKit.Row(Dock, ChangeRow, 0);
            row.Size(-1, ChangeHeight);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandHeight = false;
            Chip(row, change);
            row.SetSiblingIndex(slot.GetSiblingIndex() + 1);
        }

        RectTransform Plate(string last, string mode)
        {
            var plate = LobbyKit.Row(Dock, LastPlate, LastGap);
            plate.Size(-1, LastHeight);
            var layout = plate.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(LastPad, LastPad, 0, 0);
            if (!string.IsNullOrEmpty(mode)) Picture(plate, mode);
            var line = LobbyKit.Display(plate, last, LastText, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            line.name = LastLine;
            line.textPreprocessor = Lines;
            line.textWrappingMode = TextWrappingModes.Normal;
            line.enableAutoSizing = true; line.fontSizeMin = LastTextMinimum; line.fontSizeMax = LastText;
            line.Size(-1, LastHeight, 1);
            line.havePropertiesChanged = true;
            return plate;
        }

        static void Picture(Transform parent, string mode)
        {
            var colour = LobbyKit.ModeColour(mode);
            var badge = LobbyKit.Rect(parent, ModePicture);
            badge.Size(PictureSize, PictureSize);
            LobbyKit.Face(badge, Color.Lerp(colour, Color.white, PictureLight), PictureRadius, LobbyKit.Navy, PictureEdge, 0,
                Color.Lerp(colour, Color.black, PictureDark));
            LobbyKit.ItemImage(badge, LobbyPlayModeLabels.Emblem(mode), PictureArt, PictureTilt);
        }

        static void Chip(Transform parent, Action change)
        {
            var button = LobbyKit.Button(parent, ChangeButton, LobbyKit.Raised, change, ChangeRadius, LobbyKit.Line, ChangeEdge);
            var label = LobbyKit.Display(button.Body(), ChangeLabel, ChangeText, LobbyKit.Navy);
            label.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(ChangePad, 0), new Vector2(-ChangePad, 0));
            float width = Mathf.Ceil(label.GetPreferredValues(ChangeLabel).x) + 2f * ChangePad;
            button.Size(Mathf.Max(ChangeWidth, width), ChangeHeight);
            button.GetComponent<LobbyPress>().Hot = hot => label.color = hot ? LobbyKit.Cyan : LobbyKit.Navy;
        }

        public void ShowCareer(string career)
        {
            Remove(CareerLine);
            if (string.IsNullOrEmpty(career)) return;
            var line = LobbyKit.Text(Dock, career, CareerText, LobbyKit.Muted, TextAlignmentOptions.Center);
            line.name = CareerLine;
            line.enableAutoSizing = true; line.fontSizeMin = CareerTextMinimum; line.fontSizeMax = CareerText;
            line.Size(-1, CareerHeight);
            line.transform.SetAsLastSibling();
        }

        void Remove(string child)
        {
            var old = Dock.Find(child);
            if (!old) return;
            old.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
        }
    }
}
