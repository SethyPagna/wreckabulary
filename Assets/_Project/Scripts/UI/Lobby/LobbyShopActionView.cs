using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbyShopActionView : MonoBehaviour
    {
        public const string PriceCoin = "Coin", PriceFace = "Price face";
        const float PillWidth = 128f, PillHeight = 38f, CoinSize = 24f, PoorInk = .5f, PoorHover = 1f, RichHover = 1.05f;
        const int PillRadius = 19;
        public Button Button;
        public TextMeshProUGUI Label;
        public Image Icon;
        public Image Underline;
        public LobbyPress Press;
        public Color IdleInk = LobbyKit.Navy;
        public Color ActiveInk = LobbyKit.Cyan;
        UnityAction listener;
        bool selected, priced, poor;
        RectTransform pill, coin;

        public bool Priced => priced;
        public bool Poor => priced && poor;

        public void Validate()
        {
            LobbyShopLayout.Require(this, Button, nameof(Button));
            LobbyShopLayout.Require(this, Label, nameof(Label));
            LobbyShopLayout.Require(this, Icon, nameof(Icon));
            LobbyShopLayout.Require(this, Underline, nameof(Underline));
            LobbyShopLayout.Require(this, Press, nameof(Press));
            LobbyShopLayout.Require(this, Press.Body, "motion container");
            LobbyShopLayout.Require(this, Press.Ring, "focus ring");
            if (Button.gameObject != gameObject || Press.gameObject != gameObject || Button.onClick.GetPersistentEventCount() != 0)
                throw new InvalidOperationException("Shop actions use their own button and runtime controller callbacks only.");
        }

        public void Bind(string objectName, string text, Action action, bool isSelected, Sprite icon = null)
        {
            Validate();
            Unbind();
            name = objectName;
            Label.text = text;
            selected = isSelected;
            priced = poor = false;
            ShowPrice(false);
            Icon.sprite = icon;
            Icon.gameObject.SetActive(icon);
            Button.interactable = action != null;
            if (action != null) { listener = () => action(); Button.onClick.AddListener(listener); }
            Press.Hot += SetHot;
            SetHot(false);
        }

        public void BindPrice(string objectName, int price, bool cannotAfford, Action action)
        {
            Bind(objectName, price.ToString("N0", CultureInfo.InvariantCulture), action, false);
            priced = true;
            poor = cannotAfford;
            ShowPrice(true);
            SetHot(false);
        }

        public void SetIcon(Sprite icon, float size)
        {
            Icon.sprite = icon;
            Icon.gameObject.SetActive(icon);
            if (icon) Icon.Size(size, size);
        }

        void ShowPrice(bool shown)
        {
            if (shown && !pill)
            {
                pill = LobbyKit.Rect(Press.Body, PriceFace).Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(PillWidth, PillHeight));
                pill.SetAsFirstSibling();
                LobbyKit.Face(pill, LobbyKit.Panel, PillRadius, LobbyKit.Line, 1);
                coin = LobbyKit.Coin(Label.transform.parent, CoinSize);
                coin.name = PriceCoin;
                coin.SetSiblingIndex(Label.transform.GetSiblingIndex());
            }
            if (pill) pill.gameObject.SetActive(shown);
            if (coin) coin.gameObject.SetActive(shown);
            Press.HoverScale = shown ? poor ? PoorHover : RichHover : 1f;
        }

        void SetHot(bool hot)
        {
            bool active = selected || hot;
            Underline.enabled = active && !priced;
            var ink = active && !Poor ? ActiveInk : IdleInk;
            if (Poor) ink.a *= PoorInk;
            Label.color = ink;
            Icon.color = ink;
        }

        void Unbind()
        {
            if (Button && listener != null) Button.onClick.RemoveListener(listener);
            listener = null;
            if (Press) Press.Hot -= SetHot;
        }

        void OnDestroy() => Unbind();
    }
}
