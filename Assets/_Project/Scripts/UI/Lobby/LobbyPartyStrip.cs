using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbyPartyStrip : MonoBehaviour
    {
        public const string CopyCode = "Copy code", Copied = "Copied ✓", LeaveWord = "Leave", RetryWord = "Retry";
        public const string FindWord = "Find match", CancelWord = "Cancel";
        public static int Showing { get; private set; }

        GameObject solo, group;
        TMP_Text heading, members, copyLabel, leaveLabel, footerHeading, footerDetail, goLabel;
        Button copy, leave, go, custom, firstSolo;
        string summary, detail;
        bool available, ready, wasParty, counted;

        public void BindStrip(GameObject soloRow, GameObject partyRow, Button invite, TMP_Text partyHeading, TMP_Text partyMembers,
            Button copyButton, Button leaveButton)
        {
            solo = soloRow; group = partyRow; firstSolo = invite;
            heading = partyHeading; members = partyMembers;
            copy = copyButton; leave = leaveButton;
            copyLabel = copyButton.GetComponentInChildren<TMP_Text>(true);
            leaveLabel = leaveButton.GetComponentInChildren<TMP_Text>(true);
        }

        public void BindFooter(TMP_Text footerTitle, TMP_Text footerLine, Button goButton, Button customButton,
            string playlistSummary, string onlineDetail, bool online)
        {
            footerHeading = footerTitle; footerDetail = footerLine; go = goButton; custom = customButton;
            goLabel = goButton.GetComponentInChildren<TMP_Text>(true);
            summary = playlistSummary; detail = onlineDetail; available = online;
            ready = true;
            wasParty = WordboundLobbyParty.InParty;
            Refresh();
        }

        public static void CopyNow() => WordboundLobbyParty.Current?.Copy();

        public static void LeaveOrRetry()
        {
            var party = WordboundLobbyParty.Current;
            if (party == null) return;
            if (party.CanRetry) party.Retry();
            else party.Leave();
        }

        void OnEnable()
        {
            if (!counted) { Showing++; counted = true; }
        }

        void OnDisable()
        {
            if (counted) { Showing--; counted = false; }
        }

        void Update() => Refresh();

        public void Refresh()
        {
            if (!ready) return;
            var party = WordboundLobbyParty.Current;
            bool inParty = party != null && party.Active;
            if (solo.activeSelf == inParty) solo.SetActive(!inParty);
            if (group.activeSelf != inParty) group.SetActive(inParty);
            if (inParty != wasParty) { wasParty = inParty; KeepSelection(inParty); }
            bool searching = inParty && party.Searching;
            if (inParty)
            {
                Set(heading, party.Heading);
                Set(members, party.Members.Count == 0 ? party.Status : string.Join(",  ", party.Members));
                copy.interactable = party.CanCopy;
                Set(copyLabel, LobbyKit.Upper(party.Copied ? Copied : CopyCode));
                bool retry = party.CanRetry;
                Set(leaveLabel, LobbyKit.Upper(retry ? RetryWord : LeaveWord));
                leave.interactable = retry || party.CanLeave;
            }
            Set(footerHeading, searching ? party.SearchTitle : summary);
            string problem = inParty ? party.Problem : null;
            Set(footerDetail, problem ?? (!inParty ? detail : searching ? party.Heading : party.Status));
            footerDetail.color = problem != null || !available ? LobbyKit.Hot : LobbyKit.Muted;
            Set(goLabel, LobbyKit.Upper(searching ? CancelWord : FindWord));
            go.interactable = available && (!inParty || (searching ? party.CanCancel : party.CanFind));
            custom.interactable = available && !searching;
        }

        void KeepSelection(bool inParty)
        {
            var events = EventSystem.current;
            if (!events) return;
            var selected = events.currentSelectedGameObject;
            if (!selected || selected.activeInHierarchy) return;
            var target = inParty ? copy.interactable ? copy : leave : firstSolo;
            if (target && target.gameObject.activeInHierarchy) events.SetSelectedGameObject(target.gameObject);
        }

        static void Set(TMP_Text text, string value)
        {
            if (text && text.text != value) text.text = value;
        }
    }
}
