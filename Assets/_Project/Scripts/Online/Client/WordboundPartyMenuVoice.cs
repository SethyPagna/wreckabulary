using System.Linq;
using UnityEngine;
using Wreckabulary.Rules.Voice;
using Wreckabulary.Voice;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundPartyMenu
    {
        const float VoiceMarkSize = 22f, LinkBarWidth = 4f, LinkBarGap = 2f, LinkBarLow = 7f, LinkBarHigh = 17f;
        static readonly Color VoiceSpeakingInk = new Color32(0x2f, 0x9e, 0x74, 255), VoiceMutedInk = new Color32(0xd9, 0x4a, 0x4a, 255),
            LinkFairInk = new Color32(0xd9, 0x9a, 0x1e, 255), LinkOffInk = new Color(.5f, .5f, .5f, .35f);
        VoiceRoom partyVoice;
        string partyVoiceKey;

        bool PartyVoiceShown => partyVoice != null && VoiceChat.Room == partyVoice;

        void SyncPartyVoice()
        {
            var party = coordinator?.Party;
            if (coordinator == null || coordinator.MatchActive || party == null) { LeavePartyVoice(); return; }
            string self = coordinator.PlayerId;
            string key = party.Id + "|" + self + "|" + string.Join(",", party.Members.Select(m => m.PlayerId));
            if (key == partyVoiceKey && (partyVoice == null || VoiceChat.Room == partyVoice)) return;
            LeavePartyVoice();
            partyVoiceKey = key;
            var room = VoiceRoom.Party(party.Members.Select(m => m.PlayerId));
            if (!VoiceButtons.Shows(room, self)) return;
            partyVoice = room;
            VoiceChat.Join(room, self);
        }

        void LeavePartyVoice()
        {
            if (partyVoice != null && VoiceChat.Room == partyVoice) VoiceChat.Leave();
            partyVoice = null;
            partyVoiceKey = null;
        }

        string MemberLine(WordboundPartyView party, WordboundPartyMember member) =>
            (member.PlayerId == coordinator.PlayerId ? "You" : "Player " + member.PlayerId.Substring(0, 6)) +
            (member.PlayerId == party.LeaderId ? " · leader" : "") + (member.Ready ? " · ready" : " · not ready");

        string PartyVoiceKeyHint() => partyUsesPad ? ControlHints.PadPushToTalk : ControlHints.PushToTalkKey;

        string SelfVoiceLine() => VoiceButtons.MicLine(VoiceChat.MicState, PartyVoiceKeyHint());

        void DrawMembers(WordboundPartyView party)
        {
            bool voice = PartyVoiceShown;
            if (voice && !VoiceChat.Available) GUILayout.Label(VoiceChat.Status, hint);
            foreach (var member in party.Members)
            {
                if (!voice) { GUILayout.Label(MemberLine(party, member)); continue; }
                bool self = member.PlayerId == coordinator.PlayerId;
                GUILayout.BeginHorizontal();
                DrawVoiceMark(self ? "" : MemberVoiceIcon(member.PlayerId));
                if (!self) DrawLinkBars(VoiceChat.Link(member.PlayerId));
                GUILayout.Label(MemberLine(party, member) + (self ? " · " + SelfVoiceLine() : LinkSuffix(member.PlayerId)), GUILayout.ExpandWidth(true),
                    GUILayout.MinHeight(ui.RowHeight));
                if (!self)
                {
                    string id = member.PlayerId;
                    PartyControl("voice-mute:" + id, VoiceButtons.MuteLabel(VoiceChat.IsMuted(id)), () => VoiceChat.ToggleMuted(id));
                }
                GUILayout.EndHorizontal();
            }
        }

        static string LinkSuffix(string playerId) =>
            VoiceChat.Link(playerId) == VoiceLinkQuality.Poor ? " · " + VoiceLinkGrades.Weak : "";

        void DrawLinkBars(VoiceLinkQuality quality)
        {
            float width = LinkBarWidth * 3f + LinkBarGap * 2f;
            var rect = GUILayoutUtility.GetRect(width, LinkBarHigh, GUILayout.Width(width), GUILayout.MinHeight(ui.RowHeight));
            if (Event.current.type != EventType.Repaint) return;
            int lit = VoiceLinkGrades.Bars(quality);
            var ink = quality == VoiceLinkQuality.Poor ? VoiceMutedInk : quality == VoiceLinkQuality.Fair ? LinkFairInk : VoiceSpeakingInk;
            var color = GUI.color;
            try
            {
                float bottom = rect.center.y + LinkBarHigh * .5f;
                for (int i = 0; i < 3; i++)
                {
                    float high = Mathf.Lerp(LinkBarLow, LinkBarHigh, i / 2f);
                    GUI.color = i < lit ? ink : LinkOffInk;
                    GUI.DrawTexture(new Rect(rect.x + i * (LinkBarWidth + LinkBarGap), bottom - high, LinkBarWidth, high), Texture2D.whiteTexture);
                }
            }
            finally { GUI.color = color; }
        }

        string MemberVoiceIcon(string playerId) =>
            VoiceChat.IsMuted(playerId) ? VoiceIcons.SpeakerOff : VoiceChat.IsSpeaking(playerId) ? VoiceIcons.Wave : "";

        void DrawVoiceMark(string icon)
        {
            var rect = GUILayoutUtility.GetRect(VoiceMarkSize, VoiceMarkSize, GUILayout.Width(VoiceMarkSize), GUILayout.MinHeight(ui.RowHeight));
            if (Event.current.type != EventType.Repaint || icon.Length == 0) return;
            var color = GUI.color;
            try
            {
                GUI.color = icon == VoiceIcons.SpeakerOff ? VoiceMutedInk : VoiceSpeakingInk;
                GUI.DrawTexture(new Rect(rect.x, rect.center.y - VoiceMarkSize * .5f, VoiceMarkSize, VoiceMarkSize), VoiceIcons.Get(icon));
            }
            finally { GUI.color = color; }
        }
    }
}
