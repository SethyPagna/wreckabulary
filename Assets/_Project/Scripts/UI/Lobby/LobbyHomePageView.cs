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
        public const float PlayHeight = 88f, PlayText = 56f;
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

        public Selectable Bind(bool showTurnHint, bool starting, Action play)
        {
            Validate();
            TurnHint.name = "Turn hint"; TurnHint.gameObject.SetActive(showTurnHint);
            Dock.name = "Next match";
            Start.Bind(PlayButton, PlayLabel, starting ? null : play, true);
            var size = Start.GetComponent<LayoutElement>();
            if (size) size.preferredHeight = size.minHeight = Mathf.Max(size.preferredHeight, PlayHeight);
            Start.Label.fontSizeMax = Mathf.Max(Start.Label.fontSizeMax, PlayText);
            Start.Label.fontSize = Start.Label.fontSizeMax;
            Start.Label.Size(-1, Start.Label.fontSizeMax + 10).GetComponent<LayoutElement>().minWidth = 0;
            foreach (var other in new[] { Mode, House, Wordbound })
            {
                other.Bind(other.name, other.Label.text, null, false);
                other.gameObject.SetActive(false);
            }
            WordboundNote.gameObject.SetActive(false);
            Dock.gameObject.SetActive(!starting);
            return starting ? null : Start.Button;
        }
    }
}
