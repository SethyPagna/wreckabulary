using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules.Voice;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Voice;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundMatchHud
    {
        const float VoiceGap = 14f, VoiceRowHeight = 26f, VoiceRowGap = 4f, VoiceMarkSize = 18f, VoiceChoiceWidth = 150f,
            VoiceChoiceHeight = 32f, VoiceDim = .45f, VoiceFont = 14f;
        const int VoiceNone = -1, VoiceMic = -2, VoiceSpeaker = -3, VoiceChoice = -100;
        const float LinkBarWidth = 3f, LinkBarGap = 2f, LinkBarLow = 6f, LinkBarHigh = 14f, LinkTipWidth = 150f;
        static readonly Color VoiceAlarm = new Color32(0xf0, 0x5a, 0x5a, 255), VoiceLive = new Color32(0x8e, 0xe6, 0xb4, 255),
            VoicePick = new Color32(0x2f, 0x6f, 0x63, 230), LinkFair = new Color32(0xf2, 0xc2, 0x4b, 255), LinkOff = new Color(1f, 1f, 1f, .25f);
        readonly VoicePress micPress = new VoicePress();
        readonly GUIContent voiceText = new GUIContent();
        readonly List<Rect> voiceRows = new List<Rect>();
        IReadOnlyList<VoiceMember> voiceMembers = Array.Empty<VoiceMember>();
        VoiceRoom voiceRoom;
        GUIStyle voiceLabel, voiceKey;
        int voiceControl, voicePressed = VoiceNone, voiceStyleGeneration = -1;
        float voiceStyleUnit;
        bool voiceChoosing;
        Rect micRect, speakerRect, micKeyRect, voiceChoiceRect;
        float voiceUnit;

        public string VoiceRegion;
        string voiceMatchId;

        bool VoiceShown => voiceRoom != null && initialized && view && !view.MenuOpen;

        void JoinVoice(ModeDefinition definition, IEnumerable<WordboundSeat> seats, string self)
        {
            var room = VoiceRoom.Match(definition, seats).On(new VoiceChannel("match:" + voiceMatchId, VoiceRegion));
            if (!VoiceButtons.Shows(room, self)) return;
            voiceRoom = room;
            voiceMembers = VoiceButtons.Rows(room, self);
            VoiceChat.Join(room, self);
        }

        void LeaveVoice()
        {
            ReleaseVoice();
            if (voiceRoom != null && VoiceChat.Room == voiceRoom) VoiceChat.Leave();
            voiceRoom = null;
            voiceMembers = Array.Empty<VoiceMember>();
        }

        void DrawVoice(float width)
        {
            if (!VoiceShown) { ReleaseVoice(); return; }
            float scale = Screen.width / Mathf.Max(1f, width);
            PlaceVoice(width, HouseScale(Screen.width, Screen.height) / Mathf.Max(.01f, scale));
            double now = Time.unscaledTimeAsDouble;
            if (micPress.Long(now)) voiceChoosing = true;
            voiceControl = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;
            switch (e.GetTypeForControl(voiceControl))
            {
                case EventType.MouseDown:
                    if (e.button != 0 || !PressVoice(e.mousePosition, now)) break;
                    GUIUtility.hotControl = voiceControl;
                    e.Use();
                    break;
                case EventType.MouseUp:
                    if (voicePressed == VoiceNone || GUIUtility.hotControl != voiceControl) break;
                    LiftVoice(e.mousePosition, now);
                    e.Use();
                    break;
                case EventType.Repaint:
                    PaintVoice();
                    break;
            }
        }

        void PlaceVoice(float width, float unit)
        {
            voiceUnit = unit;
            float size = EyeSize * unit, gap = VoiceGap * unit;
            float right = LookEyeShown ? width - EyeMargin - size - gap : width - EyeMargin;
            micRect = new Rect(right - size, EyeMargin, size, size);
            speakerRect = new Rect(micRect.x - gap - size, EyeMargin, size, size);
            micKeyRect = new Rect(micRect.center.x - KeyCapWidth * unit * .5f, micRect.yMax + KeyCapGap * unit,
                KeyCapWidth * unit, KeyCapHeight * unit);
            float top = micKeyRect.yMax + gap, rowWidth = micRect.xMax - speakerRect.x, rowHeight = VoiceRowHeight * unit;
            voiceRows.Clear();
            for (int i = 0; i < voiceMembers.Count; i++)
                voiceRows.Add(new Rect(speakerRect.x, top + i * (rowHeight + VoiceRowGap * unit), rowWidth, rowHeight));
            float choiceWidth = VoiceChoiceWidth * unit, inset = 4f * unit;
            voiceChoiceRect = new Rect(micRect.xMax - choiceWidth, micKeyRect.yMax + gap, choiceWidth,
                VoiceChoiceHeight * unit * VoiceButtons.Modes.Count + inset * 2f);
        }

        Rect VoiceChoiceAt(int index)
        {
            float inset = 4f * voiceUnit, high = VoiceChoiceHeight * voiceUnit;
            return new Rect(voiceChoiceRect.x + inset, voiceChoiceRect.y + inset + index * high, voiceChoiceRect.width - inset * 2f, high);
        }

        int VoiceTargetAt(Vector2 at)
        {
            if (voiceChoosing)
                for (int i = 0; i < VoiceButtons.Modes.Count; i++)
                    if (VoiceChoiceAt(i).Contains(at)) return VoiceChoice - i;
            if (micRect.Contains(at)) return VoiceMic;
            if (speakerRect.Contains(at)) return VoiceSpeaker;
            for (int i = 0; i < voiceRows.Count; i++) if (voiceRows[i].Contains(at)) return i;
            return VoiceNone;
        }

        bool PressVoice(Vector2 at, double now)
        {
            if (voicePressed != VoiceNone || !VoiceShown) return false;
            int target = VoiceTargetAt(at);
            if (voiceChoosing && target > VoiceChoice) { voiceChoosing = false; return false; }
            if (target == VoiceNone) return false;
            voicePressed = target;
            if (target == VoiceMic) micPress.Begin(now);
            return true;
        }

        void LiftVoice(Vector2 at, double now)
        {
            int pressed = voicePressed, over = VoiceTargetAt(at);
            voicePressed = VoiceNone;
            if (GUIUtility.hotControl == voiceControl) GUIUtility.hotControl = 0;
            if (pressed == VoiceMic)
            {
                var end = micPress.End(now);
                if (end == VoicePressEnd.Long) voiceChoosing = true;
                else if (end == VoicePressEnd.Tap && over == VoiceMic) TapMic();
                return;
            }
            if (pressed != over) return;
            if (pressed == VoiceSpeaker) TapSpeaker();
            else if (pressed <= VoiceChoice) ChooseMic(VoiceChoice - pressed);
            else if (pressed >= 0) TapVoiceRow(pressed);
        }

        void TapMic()
        {
            switch (VoiceButtons.MicTap(VoiceChat.Available, VoiceChat.Mic))
            {
                case VoiceTap.Status: SetNotice(VoiceChat.Status); break;
                case VoiceTap.Choose: voiceChoosing = true; break;
                default: VoiceChat.ToggleSelfMute(); break;
            }
        }

        void TapSpeaker()
        {
            if (VoiceButtons.SpeakerTap(VoiceChat.Available) == VoiceTap.Status) SetNotice(VoiceChat.Status);
            else VoiceChat.ToggleHearing();
        }

        void TapVoiceRow(int row)
        {
            if (row < 0 || row >= voiceMembers.Count) return;
            if (!VoiceChat.Available) SetNotice(VoiceChat.Status);
            else VoiceChat.ToggleMuted(voiceMembers[row].PlayerId);
        }

        void ChooseMic(int index)
        {
            voiceChoosing = false;
            if (index >= 0 && index < VoiceButtons.Modes.Count) VoiceChat.Mic = VoiceButtons.Modes[index];
        }

        string VoiceRowIcon(int row)
        {
            if (row < 0 || row >= voiceMembers.Count) return "";
            string id = voiceMembers[row].PlayerId;
            return VoiceChat.IsMuted(id) ? VoiceIcons.SpeakerOff : VoiceChat.IsSpeaking(id) ? VoiceIcons.Wave : "";
        }

        string VoiceKeyHint() => view && view.UsesGamepad ? ControlHints.PadPushToTalk : ControlHints.PushToTalkKey;

        void PaintVoice()
        {
            var color = GUI.color;
            try
            {
                float dim = VoiceChat.Available ? 1f : VoiceDim, unit = voiceUnit;
                var state = VoiceChat.MicState;
                var micInk = VoiceButtons.Alarm(state) ? VoiceAlarm : VoiceButtons.Lit(state) ? VoiceLive : EyeInk;
                PaintVoiceDisc(micRect, VoiceIcons.MicIcon(VoiceButtons.Crossed(state)), micInk,
                    voicePressed == VoiceMic || VoiceButtons.Lit(state), dim);
                if (VoiceButtons.ShowsKey(VoiceChat.Mic))
                {
                    voiceText.text = VoiceKeyHint();
                    if (voiceText.text.Length != 0)
                    {
                        GUI.color = Faded(VoiceButtons.Lit(state) ? VoicePick : KeyCapFill, dim);
                        GUI.DrawTexture(micKeyRect, KeyCap());
                        GUI.color = Faded(Color.white, dim);
                        GUI.Label(micKeyRect, voiceText, VoiceStyle(unit, true));
                    }
                }
                bool hearing = VoiceChat.HearTeammates;
                PaintVoiceDisc(speakerRect, VoiceIcons.SpeakerIcon(!hearing), EyeInk, voicePressed == VoiceSpeaker, dim);
                for (int i = 0; i < voiceRows.Count; i++) PaintVoiceRow(i, dim);
                if (voiceChoosing) PaintVoiceChoice(dim);
            }
            finally { GUI.color = color; }
        }

        void PaintVoiceDisc(Rect rect, string icon, Color ink, bool held, float dim)
        {
            GUI.color = Faded(held ? EyeGlassHeld : EyeGlass, dim);
            GUI.DrawTexture(rect, EyeDisc());
            float side = EyeIcon * voiceUnit;
            var at = new Rect(rect.center.x - side * .5f, rect.center.y - side * .5f, side, side);
            var texture = VoiceIcons.Get(icon);
            GUI.color = Faded(EyeShade, dim);
            GUI.DrawTexture(new Rect(at.x, at.y + EyeShadow * voiceUnit, side, side), texture);
            GUI.color = Faded(ink, dim);
            GUI.DrawTexture(at, texture);
        }

        void PaintVoiceRow(int row, float dim)
        {
            var rect = voiceRows[row];
            string id = voiceMembers[row].PlayerId, icon = VoiceRowIcon(row);
            bool muted = VoiceChat.IsMuted(id);
            GUI.color = Faded(voicePressed == row ? EyeGlassHeld : EyeGlass, dim);
            GUI.DrawTexture(rect, KeyCap());
            float side = VoiceMarkSize * voiceUnit, pad = 6f * voiceUnit;
            var mark = new Rect(rect.x + pad, rect.center.y - side * .5f, side, side);
            if (icon.Length != 0)
            {
                GUI.color = Faded(muted ? VoiceAlarm : VoiceLive, dim);
                GUI.DrawTexture(mark, VoiceIcons.Get(icon));
            }
            var bars = PaintLinkBars(new Vector2(mark.xMax + pad, rect.center.y), VoiceChat.Link(id), dim);
            voiceText.text = VoiceButtons.Label(voiceRoom, id);
            GUI.color = Faded(muted ? new Color(1f, 1f, 1f, .6f) : Color.white, dim);
            GUI.Label(new Rect(bars.xMax + pad, rect.y, rect.xMax - bars.xMax - pad * 2f, rect.height), voiceText, VoiceStyle(voiceUnit, false));
            if (VoiceChat.WeakTip(id)) PaintLinkTip(rect, dim);
        }

        Rect LinkBarsRect(Vector2 leftCentre)
        {
            float width = (LinkBarWidth * 3f + LinkBarGap * 2f) * voiceUnit, high = LinkBarHigh * voiceUnit;
            return new Rect(leftCentre.x, leftCentre.y - high * .5f, width, high);
        }

        Rect PaintLinkBars(Vector2 leftCentre, VoiceLinkQuality quality, float dim)
        {
            var area = LinkBarsRect(leftCentre);
            int lit = VoiceLinkGrades.Bars(quality);
            var ink = quality == VoiceLinkQuality.Poor ? VoiceAlarm : quality == VoiceLinkQuality.Fair ? LinkFair : VoiceLive;
            float width = LinkBarWidth * voiceUnit, gap = LinkBarGap * voiceUnit;
            for (int i = 0; i < 3; i++)
            {
                float high = Mathf.Lerp(LinkBarLow, LinkBarHigh, i / 2f) * voiceUnit;
                var bar = new Rect(area.x + i * (width + gap), area.yMax - high, width, high);
                GUI.color = Faded(i < lit ? ink : LinkOff, dim);
                GUI.DrawTexture(bar, Texture2D.whiteTexture);
            }
            return area;
        }

        void PaintLinkTip(Rect row, float dim)
        {
            float width = LinkTipWidth * voiceUnit, gap = 6f * voiceUnit;
            var tip = new Rect(row.x - gap - width, row.y, width, row.height);
            GUI.color = Faded(KeyCapFill, Mathf.Max(dim, .9f));
            GUI.DrawTexture(tip, KeyCap());
            voiceText.text = VoiceLinkGrades.Weak;
            GUI.color = Faded(Color.white, Mathf.Max(dim, .9f));
            GUI.Label(tip, voiceText, VoiceStyle(voiceUnit, true));
        }

        void PaintVoiceChoice(float dim)
        {
            GUI.color = Faded(KeyCapFill, 1f);
            GUI.DrawTexture(voiceChoiceRect, KeyCap());
            for (int i = 0; i < VoiceButtons.Modes.Count; i++)
            {
                var rect = VoiceChoiceAt(i);
                var mode = VoiceButtons.Modes[i];
                if (mode == VoiceChat.Mic || voicePressed == VoiceChoice - i)
                {
                    GUI.color = mode == VoiceChat.Mic ? VoicePick : EyeGlassHeld;
                    GUI.DrawTexture(rect, KeyCap());
                }
                voiceText.text = VoiceButtons.ModeName(mode);
                GUI.color = Faded(Color.white, Mathf.Max(dim, .8f));
                GUI.Label(rect, voiceText, VoiceStyle(voiceUnit, true));
            }
        }

        static Color Faded(Color color, float dim) { color.a *= dim; return color; }

        GUIStyle VoiceStyle(float unit, bool centred)
        {
            if (voiceLabel == null || voiceStyleGeneration != ui.Generation || voiceStyleUnit != unit)
            {
                int size = Mathf.Max(1, Mathf.RoundToInt(VoiceFont * unit));
                voiceLabel = new GUIStyle(ui.SingleLine) { alignment = TextAnchor.MiddleLeft, wordWrap = false, fontSize = size,
                    clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
                voiceLabel.normal.textColor = EyeInk;
                voiceKey = new GUIStyle(voiceLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
                voiceStyleGeneration = ui.Generation;
                voiceStyleUnit = unit;
            }
            return centred ? voiceKey : voiceLabel;
        }

        void ReleaseVoice()
        {
            micPress.Cancel();
            voiceChoosing = false;
            if (voicePressed == VoiceNone) return;
            voicePressed = VoiceNone;
            if (voiceControl != 0 && GUIUtility.hotControl == voiceControl) GUIUtility.hotControl = 0;
        }
    }
}
