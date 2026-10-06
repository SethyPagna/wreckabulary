using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    /// <summary>Touch and mouse accessible start/retry actions, above movement and attack controls.</summary>
    public sealed class ModeActions : MonoBehaviour
    {
        GameObject panel;
        Canvas canvas;
        TMP_Text label;
        static readonly System.Collections.Generic.List<ModeActions> all = new();
        static bool hidden;

        /// <summary>The Tab bag is open: the buttons stay where they are but aren't drawn or clickable.</summary>
        public static bool Hidden
        {
            get => hidden;
            set { hidden = value; foreach (var a in all) if (a && a.canvas) a.canvas.enabled = !value; }
        }

        /// <summary>Start, retry or home buttons are on screen, so the pointer must be free to click them.</summary>
        public static bool AnyShown
        {
            get
            {
                foreach (var a in all)
                    if (a && a.panel && a.panel.activeInHierarchy) return true;
                return false;
            }
        }

        void OnEnable() { all.Add(this); if (canvas) canvas.enabled = !hidden; }
        void OnDisable() => all.Remove(this);
        public static ModeActions Create(Transform owner, string caption, Action play)
        {
            var component = new GameObject("Mode actions").AddComponent<ModeActions>();
            component.transform.SetParent(owner, false);
            var canvas = new GameObject("Actions canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(component.transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 30;
            component.canvas = canvas.GetComponent<Canvas>();
            component.canvas.enabled = !hidden;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = .5f;
            component.panel = new GameObject("Buttons", typeof(RectTransform));
            component.panel.transform.SetParent(canvas.transform, false);
            var row = component.panel.GetComponent<RectTransform>();
            row.anchorMin = row.anchorMax = new Vector2(.5f, .82f);
            row.sizeDelta = new Vector2(460f, 72f);
            component.label = component.AddButton("Play", caption, new Vector2(-115f, 0f), () => play());
            component.AddButton("Home", "HOME", new Vector2(115f, 0f), () => Session.GoHome());
            return component;
        }

        TMP_Text AddButton(string name, string caption, Vector2 at, Action action)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(panel.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(212f, 68f);
            rect.anchoredPosition = at;
            go.GetComponent<Image>().color = new Color(.15f, .19f, .22f, .96f);
            go.GetComponent<Button>().onClick.AddListener(() => action());
            var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(go.transform, false);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            var value = text.GetComponent<TextMeshProUGUI>();
            value.font = GameAssets.I.font;
            value.fontSize = 25f;
            value.alignment = TextAlignmentOptions.Center;
            value.text = caption;
            value.raycastTarget = false;
            return value;
        }

        public void Show(bool visible, string caption = null)
        {
            panel.SetActive(visible);
            if (caption != null) label.text = caption;
        }
    }
}
