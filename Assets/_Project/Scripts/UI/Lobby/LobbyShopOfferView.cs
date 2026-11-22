using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbyShopOfferView : MonoBehaviour
    {
        public const string MarkLayer = "Marks", FreeLabel = "FREE", EquipLabel = "EQUIP";
        public const string WearingPrefix = "Wearing ", WearPrefix = "Wear ", BuyPrefix = "Buy ";
        const float MarkSize = 34f, MarkInset = 8f, MarkIcon = 18f;
        public Button Preview;
        public RectTransform Face;
        public RectTransform Art;
        public TextMeshProUGUI Title;
        public Image Outline;
        public LobbyHint Hint;
        public LobbyShopActionView Action;
        public Sprite Coin;
        public Sprite Check;
        public Color PreviewColour = LobbyKit.Sun;
        public Color EquippedColour = LobbyKit.Cyan;
        UnityAction previewListener;
        bool worn;
        RectTransform marks, tick, locked;

        public void Validate()
        {
            LobbyShopLayout.ValidateTree(this);
            LobbyShopLayout.Require(this, Preview, nameof(Preview));
            LobbyShopLayout.Require(this, Face, nameof(Face));
            LobbyShopLayout.Require(this, Art, nameof(Art));
            LobbyShopLayout.Require(this, Title, nameof(Title));
            LobbyShopLayout.Require(this, Outline, nameof(Outline));
            LobbyShopLayout.Require(this, Hint, nameof(Hint));
            LobbyShopLayout.Require(this, Action, nameof(Action));
            Action.Validate();
            if (!Coin || !Check || Preview == Action.Button || Preview.onClick.GetPersistentEventCount() != 0)
                throw new InvalidOperationException("Shop cards need separate preview/action buttons, saved icons and runtime callbacks only.");
        }

        public void Bind(string id, string title, string description, bool owned, bool equipped, int price,
            bool spotlight, Action preview, Action action, bool affordable = true)
        {
            Validate();
            if (previewListener != null) Preview.onClick.RemoveListener(previewListener);
            previewListener = () => preview();
            Preview.onClick.AddListener(previewListener);
            name = "Offer " + id;
            Title.text = title;
            Hint.Text = (description ?? "").Replace("\n", " · ");
            worn = equipped;
            if (worn) Action.Bind(WearingPrefix + id, "", null, true);
            else if (owned) Action.Bind(WearPrefix + id, EquipLabel, action, false);
            else if (price > 0) Action.BindPrice(BuyPrefix + id, price, !affordable, action);
            else Action.Bind(BuyPrefix + id, FreeLabel, action, false);
            Action.gameObject.SetActive(!worn);
            ShowMarks(owned);
            SetPreview(spotlight);
        }

        public void SetPreview(bool selected)
        {
            Outline.enabled = worn || selected;
            Outline.color = worn ? EquippedColour : PreviewColour;
        }

        void ShowMarks(bool owned)
        {
            if (!marks)
            {
                marks = Layer(Face, Art);
                tick = Tick(marks);
                locked = Lock(marks);
            }
            tick.gameObject.SetActive(worn);
            locked.gameObject.SetActive(!owned && !worn);
            marks.SetAsLastSibling();
        }

        public static RectTransform Layer(RectTransform face, RectTransform art)
        {
            var layer = LobbyKit.Rect(face, MarkLayer);
            layer.anchorMin = art.anchorMin; layer.anchorMax = art.anchorMax; layer.pivot = art.pivot;
            layer.offsetMin = art.offsetMin; layer.offsetMax = art.offsetMax;
            layer.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return layer;
        }

        public static RectTransform Tick(RectTransform host) =>
            Corner(host, LoadoutPage.CheckMark, LobbyIcons.Check, LobbyKit.Selection, LobbyKit.OnSelection);

        public static RectTransform Lock(RectTransform host) =>
            Corner(host, LoadoutPage.LockMark, LobbyIcons.Lock, LobbyKit.Panel, LobbyKit.Navy);

        static RectTransform Corner(RectTransform host, string name, string icon, Color fill, Color ink)
        {
            var disc = LobbyKit.Rect(host, name).Pin(Vector2.one, new Vector2(-MarkInset, -MarkInset), new Vector2(MarkSize, MarkSize));
            LobbyKit.Face(disc, fill, Mathf.RoundToInt(MarkSize / 2f), LobbyKit.Line, 1);
            LobbyKit.Icon(disc, icon, ink).rectTransform.Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(MarkIcon, MarkIcon));
            return disc;
        }

        void OnDestroy()
        {
            if (Preview && previewListener != null) Preview.onClick.RemoveListener(previewListener);
        }
    }
}
