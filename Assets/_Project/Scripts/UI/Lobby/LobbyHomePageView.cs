using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbyHomePageView : MonoBehaviour
    {
        public const string PlayButton = "Home PLAY", PlayLabel = "PLAY";
        public const string LastLine = "Last match", ChangeButton = "Home CHANGE", ChangeLabel = "Change";
        public const string CareerLine = "Play time";
        public const float PlayHeight = 88f, PlayText = 56f;
        const float LastHeight = 24f, LastText = 17f, LastTextMinimum = 12f, ChangeHeight = 32f, ChangeText = 18f;
        const float CareerHeight = 22f, CareerText = 16f, CareerTextMinimum = 15f;
        public RectTransform TurnHint;
        public RectTransform Dock;
        public LobbyShopActionView Start;
        public LobbyShopActionView Mode;
        public LobbyShopActionView House;
        public LobbyShopActionView Wordbound;
        public TextMeshProUGUI WordboundNote;

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

        public Selectable Bind(bool showTurnHint, bool starting, Action play, string last, Action change)
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
            ShowLast(starting ? null : last, change);
            Dock.gameObject.SetActive(!starting);
            return starting ? null : Start.Button;
        }

        void ShowLast(string last, Action change)
        {
            Remove(LastLine); Remove(ChangeButton);
            if (string.IsNullOrEmpty(last)) return;
            var slot = Start.transform;
            while (slot.parent != Dock) slot = slot.parent;
            var line = LobbyKit.Text(Dock, last, LastText, LobbyKit.Muted, TextAlignmentOptions.Center);
            line.name = LastLine;
            line.enableAutoSizing = true; line.fontSizeMin = LastTextMinimum; line.fontSizeMax = LastText;
            line.Size(-1, LastHeight);
            line.transform.SetSiblingIndex(slot.GetSiblingIndex());
            var button = LobbyKit.TextAction(Dock, ChangeButton, ChangeLabel, ChangeText, change);
            button.Size(-1, ChangeHeight);
            button.transform.SetSiblingIndex(slot.GetSiblingIndex() + 1);
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
