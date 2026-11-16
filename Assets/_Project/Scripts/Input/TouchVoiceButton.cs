using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Voice;
using Wreckabulary.Voice;

namespace Wreckabulary
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class TouchVoiceButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public const float Diameter = 80f;
        static readonly Color Ink = LobbyKit.Hex(0x173b3c), Cream = LobbyKit.Hex(0xfff0d9), Mint = LobbyKit.Hex(0xb7dfc8),
            Face = LobbyKit.Hex(0xfff0dc, .95f), Glow = LobbyKit.Hex(0x8ee6b4), Alarm = LobbyKit.Hex(0xf05a5a);
        RawImage glyph;
        Image ring;
        CanvasGroup group;
        Color resting;
        int pointer = int.MinValue;
        float scale = 1f, opacity = 1f;
        string shown;

        public TouchPart Part { get; private set; }
        public bool Held => pointer != int.MinValue;
        public bool Shown => group && group.blocksRaycasts;

        public float Scale
        {
            get => scale;
            set { scale = value; Rescale(); }
        }

        public float Opacity
        {
            get => opacity;
            set { opacity = Mathf.Clamp01(value); Refresh(); }
        }

        public static TouchVoiceButton Create(RectTransform parent, TouchPart part)
        {
            if (part != TouchPart.Mic && part != TouchPart.Speaker) throw new ArgumentException("Only MIC and SPEAKER are voice buttons.", nameof(part));
            var rt = LobbyKit.Rect(parent, TouchLayout.Label(part)).Pin(new Vector2(.5f, .5f), Vector2.zero, Vector2.one * Diameter);
            var body = rt.Paint(part == TouchPart.Mic ? Mint : Cream, Mathf.RoundToInt(Diameter / 2f));
            body.raycastTarget = true;
            var face = LobbyKit.Rect(rt, "Face").Pin(new Vector2(.5f, .5f), Vector2.zero, Vector2.one * (Diameter - 7f));
            face.Paint(Face, Mathf.RoundToInt((Diameter - 7f) / 2f)).raycastTarget = false;
            var icon = LobbyKit.Rect(face, "Icon").Pin(new Vector2(.5f, .5f), Vector2.zero, Vector2.one * Diameter * .5f).gameObject.AddComponent<RawImage>();
            icon.raycastTarget = false;
            var label = LobbyKit.Text(rt, TouchLayout.Label(part), 14f, Cream, TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, 0f);
            label.rectTransform.pivot = new Vector2(.5f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -4f);
            label.rectTransform.sizeDelta = new Vector2(Diameter + 34f, 26f);
            label.outlineWidth = .2f; label.outlineColor = Ink;
            var button = rt.gameObject.AddComponent<TouchVoiceButton>();
            button.Part = part; button.glyph = icon; button.ring = body; button.resting = body.color;
            button.group = rt.GetComponent<CanvasGroup>();
            button.Refresh();
            return button;
        }

        public static bool Applies => VoiceButtons.Shows(VoiceChat.Room, VoiceChat.Self);

        public void OnPointerDown(PointerEventData e)
        {
            if (Held || !Applies) return;
            pointer = e.pointerId;
            if (Part == TouchPart.Mic) VoiceChat.SetTalkButton(true);
            else if (VoiceChat.Available) VoiceChat.ToggleHearing();
            Rescale();
            Refresh();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == pointer) Release();
        }

        void LateUpdate() => Refresh();
        void OnDisable() => Release();
        void OnApplicationFocus(bool focused) { if (!focused) Release(); }

        public void Refresh()
        {
            if (!group) group = GetComponent<CanvasGroup>();
            bool on = Applies;
            if (!on && Held) Release();
            group.alpha = on ? opacity * (VoiceChat.Available ? 1f : TouchActionButton.UnavailableAlpha) : 0f;
            group.blocksRaycasts = group.interactable = on;
            var state = VoiceChat.MicState;
            string icon = Part == TouchPart.Mic ? VoiceIcons.MicIcon(VoiceButtons.Crossed(state)) : VoiceIcons.SpeakerIcon(!VoiceChat.HearTeammates);
            if (glyph && icon != shown) { shown = icon; glyph.texture = VoiceIcons.Get(icon); }
            if (glyph) glyph.color = Part == TouchPart.Mic && VoiceButtons.Alarm(state) ? Alarm : Ink;
            if (ring) ring.color = Part == TouchPart.Mic && VoiceButtons.Lit(state) ? Glow : resting;
        }

        void Release()
        {
            if (!Held) return;
            pointer = int.MinValue;
            if (Part == TouchPart.Mic) VoiceChat.SetTalkButton(false);
            Rescale();
        }

        void Rescale() => transform.localScale = Vector3.one * (scale * (Held ? TouchActionButton.PressedScale : 1f));
    }
}
