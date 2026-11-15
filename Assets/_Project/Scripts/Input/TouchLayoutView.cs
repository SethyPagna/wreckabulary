using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class TouchLayoutView : MonoBehaviour
    {
        const float AddedDiameter = 80f;
        static readonly string[] HudNames =
            { "Move stick", "Aim stick", "SMASH", "DODGE", "JUMP", "BLOCK", "INTERACT", "SPELL", "HOLD DROP", "BOOK", "HAND 1", "HAND 2", "PAUSE", "EYE" };
        static readonly Color Ink = LobbyKit.Hex(0x173b3c), Cream = LobbyKit.Hex(0xfff0d9), Mint = LobbyKit.Hex(0xb7dfc8), FaceColour = LobbyKit.Hex(0xfff0dc, .95f);

        readonly RectTransform[] parts = new RectTransform[TouchLayout.Count];
        readonly float[] design = new float[TouchLayout.Count];
        readonly CanvasGroup[] groups = new CanvasGroup[TouchLayout.Count];
        readonly TouchActionButton[] buttons = new TouchActionButton[TouchLayout.Count];
        readonly TouchEyeButton[] eyes = new TouchEyeButton[TouchLayout.Count];
        RectTransform root;
        GameHud hud;
        int version = -1;
        Vector2 size;

        public TouchLayout Applied { get; private set; }
        public static string HudName(TouchPart part) => HudNames[(int)part];
        public RectTransform Part(TouchPart part) => parts[(int)part];

        public static TouchLayoutView Attach(RectTransform root, GameHud hud)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            if (!root.TryGetComponent(out TouchLayoutView view)) view = root.gameObject.AddComponent<TouchLayoutView>();
            view.Bind(root, hud);
            return view;
        }

        void Bind(RectTransform target, GameHud owner)
        {
            root = target; hud = owner;
            Add(TouchPart.Book, Mint, face => Glyph(face, "book"), () => { if (hud) hud.ToggleBag(); });
            Add(TouchPart.Hand1, Cream, face => Number(face, "1"), () => TouchBinding.Shared.PulseHand(1));
            Add(TouchPart.Hand2, Cream, face => Number(face, "2"), () => TouchBinding.Shared.PulseHand(2));
            Add(TouchPart.Pause, Cream, PauseBars, () => { if (hud) hud.TogglePause(); });
            Add(TouchPart.Eye, Mint, face => LobbyKit.Icon(face, LobbyIcons.Eye, Ink).rectTransform.Pin(new Vector2(.5f, .5f), Vector2.zero, Vector2.one * AddedDiameter * .5f), null);
            for (int i = 0; i < TouchLayout.Count; i++)
            {
                parts[i] = root.Find(HudNames[i]) as RectTransform;
                if (!parts[i]) continue;
                design[i] = Mathf.Max(1f, parts[i].sizeDelta.x);
                buttons[i] = parts[i].GetComponent<TouchActionButton>();
                eyes[i] = parts[i].GetComponent<TouchEyeButton>();
                if (!parts[i].TryGetComponent(out groups[i])) groups[i] = parts[i].gameObject.AddComponent<CanvasGroup>();
            }
            version = -1;
            Apply();
        }

        void Add(TouchPart part, Color accent, Action<RectTransform> art, Action press)
        {
            if (root.Find(HudName(part))) return;
            var rt = LobbyKit.Rect(root, HudName(part)).Pin(new Vector2(.5f, .5f), Vector2.zero, Vector2.one * AddedDiameter);
            var ring = rt.Paint(accent, Mathf.RoundToInt(AddedDiameter / 2f));
            ring.raycastTarget = true;
            var face = LobbyKit.Rect(rt, "Face").Pin(new Vector2(.5f, .5f), Vector2.zero, Vector2.one * (AddedDiameter - 7f));
            face.Paint(FaceColour, Mathf.RoundToInt((AddedDiameter - 7f) / 2f)).raycastTarget = false;
            art(face);
            var label = LobbyKit.Text(rt, TouchLayout.Label(part), 14f, Cream, TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, 0f);
            label.rectTransform.pivot = new Vector2(.5f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -4f);
            label.rectTransform.sizeDelta = new Vector2(AddedDiameter + 34f, 26f);
            label.outlineWidth = .2f; label.outlineColor = Ink;
            if (part == TouchPart.Eye) rt.gameObject.AddComponent<TouchEyeButton>();
            else
            {
                var button = rt.gameObject.AddComponent<TouchActionButton>();
                button.sendsInput = false;
                button.pressed = press;
            }
        }

        static void Glyph(RectTransform face, string name)
        {
            var image = LobbyKit.Rect(face, "Icon").Pin(new Vector2(.5f, .5f), Vector2.zero, Vector2.one * AddedDiameter * .5f).gameObject.AddComponent<RawImage>();
            image.texture = Resources.Load<Texture2D>("UI/Glyphs/" + name);
            image.color = Ink; image.raycastTarget = false;
            if (!image.texture) image.enabled = false;
        }

        static void Number(RectTransform face, string digit)
        {
            var text = LobbyKit.Text(face, digit, 34f, Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            text.rectTransform.Fill();
        }

        static void PauseBars(RectTransform face)
        {
            foreach (float x in new[] { -8f, 8f })
                LobbyKit.Rect(face, "Bar").Pin(new Vector2(.5f, .5f), new Vector2(x, 0f), new Vector2(9f, 30f)).Paint(Ink, 3).raycastTarget = false;
        }

        void OnEnable() => version = -1;

        void LateUpdate()
        {
            if (root && (version != TouchLayouts.Version || size != root.rect.size)) Apply();
        }

        public void Apply()
        {
            if (!root) return;
            version = TouchLayouts.Version;
            size = root.rect.size;
            if (size.x < 1f || size.y < 1f) return;
            Applied = TouchLayouts.Current(size);
            float side = Mathf.Min(size.x, size.y);
            for (int i = 0; i < TouchLayout.Count; i++)
            {
                var rect = parts[i];
                if (!rect) continue;
                var spot = Applied[(TouchPart)i];
                rect.anchorMin = rect.anchorMax = new Vector2(spot.X, spot.Y);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                float scale = spot.Size * side / design[i], alpha = spot.Opacity * Applied.Opacity;
                if (buttons[i]) { buttons[i].Scale = scale; buttons[i].Opacity = alpha; continue; }
                if (eyes[i]) eyes[i].Scale = scale;
                else rect.localScale = Vector3.one * scale;
                groups[i].alpha = alpha;
            }
        }
    }
}
