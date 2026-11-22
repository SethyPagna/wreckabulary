using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class LobbyPartyStrip : MonoBehaviour
    {
        public const string CopyCode = "Copy code", Copied = "Copied ✓", LeaveWord = "Leave", RetryWord = "Retry";
        public const string FindWord = "Find match", CancelWord = "Cancel", QuietEdge = "Quiet edge";
        const int QuietRadius = 8, QuietWidth = 2;
        public static int Showing { get; private set; }

        GameObject solo, group, code;
        TMP_Text heading, members, copyLabel, leaveLabel, footerHeading, footerDetail, goLabel;
        Button copy, leave, go, custom, firstSolo;
        LobbyPartyChips plates;
        LobbyMatchBand band;
        Image goFace, goEdge;
        string summary, detail, serviceLine, partyLine;
        bool available, ready, wasParty, counted;
        int shownSize = 1;
        Action resized;

        public void WatchPartySize(int shown, Action rebuild)
        {
            shownSize = shown;
            resized = rebuild;
        }

        public void BindStrip(GameObject soloRow, GameObject partyRow, Button invite, TMP_Text partyHeading, TMP_Text partyMembers,
            LobbyPartyChips partyPlates, Button copyButton, Button leaveButton)
        {
            solo = soloRow; group = partyRow; firstSolo = invite;
            heading = partyHeading; members = partyMembers; plates = partyPlates;
            copy = copyButton; leave = leaveButton;
            copyLabel = copyButton.GetComponentInChildren<TMP_Text>(true);
            leaveLabel = leaveButton.GetComponentInChildren<TMP_Text>(true);
        }

        public void BindParty(GameObject codeBox, LobbyMatchBand searchBand, string serviceStatus, string shortLine)
        {
            code = codeBox; band = searchBand;
            serviceLine = serviceStatus; partyLine = shortLine;
        }

        public void BindFooter(TMP_Text footerTitle, TMP_Text footerLine, Button goButton, Button customButton,
            string playlistSummary, string onlineDetail, bool online)
        {
            footerHeading = footerTitle; footerDetail = footerLine; go = goButton; custom = customButton;
            goLabel = goButton.GetComponentInChildren<TMP_Text>(true);
            goFace = goButton.FaceOf();
            goEdge = LobbyKit.Frame(goButton.Body(), LobbyKit.Line, QuietRadius, QuietWidth, QuietEdge);
            goEdge.enabled = false;
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
            int size = inParty ? Mathf.Max(1, party.Members.Count) : 1;
            if (resized != null && size != shownSize)
            {
                var rebuild = resized;
                resized = null;
                rebuild();
                return;
            }
            if (solo.activeSelf == inParty) solo.SetActive(!inParty);
            if (group.activeSelf != inParty) group.SetActive(inParty);
            if (code && code.activeSelf == inParty) code.SetActive(!inParty);
            if (inParty != wasParty) { wasParty = inParty; KeepSelection(inParty); }
            bool searching = inParty && party.Searching;
            if (inParty) ShowParty(party);
            if (band) band.Show(searching ? party : null);
            Set(footerHeading, searching ? party.SearchTitle : summary);
            string problem = inParty ? party.Problem : null;
            string idle = inParty && serviceLine != null && party.Status == serviceLine ? partyLine : inParty ? party.Status : detail;
            Set(footerDetail, problem ?? (searching ? party.Heading : idle));
            footerDetail.color = problem != null || !available ? LobbyKit.Hot : LobbyKit.Muted;
            Set(goLabel, LobbyKit.Upper(searching ? CancelWord : FindWord));
            Quiet(searching);
            go.interactable = available && (!inParty || (searching ? party.CanCancel : party.CanFind));
            custom.interactable = available && !searching;
        }

        void ShowParty(IWordboundLobbyParty party)
        {
            Set(heading, party.Heading);
            bool listed = party.Members.Count > 0;
            if (plates.gameObject.activeSelf != listed) plates.gameObject.SetActive(listed);
            if (members.gameObject.activeSelf == listed) members.gameObject.SetActive(!listed);
            if (listed) plates.Show(party.Members);
            else Set(members, party.Status);
            copy.interactable = party.CanCopy;
            Set(copyLabel, LobbyKit.Upper(party.Copied ? Copied : CopyCode));
            bool retry = party.CanRetry;
            Set(leaveLabel, LobbyKit.Upper(retry ? RetryWord : LeaveWord));
            leave.interactable = retry || party.CanLeave;
        }

        void Quiet(bool quiet)
        {
            if (!goFace || goEdge.enabled == quiet) return;
            goEdge.enabled = quiet;
            goFace.color = quiet ? LobbyKit.Card : LobbyKit.PrimaryFill;
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

    public sealed class LobbyPartyChips : MonoBehaviour
    {
        public const string PlatePrefix = "Party plate ", ChipName = "Plate chip", NameName = "Plate name", TagName = "Plate tag";
        public const float Height = 44f, NameText = 20f, NameMinimum = 15f, TagText = 15f, Chip = 30f, ChipText = 16f;
        const float Pad = 8f, Gap = 8f, Spacing = 10f, Slack = 4f, NameWidest = 200f, NameLeast = 48f;
        const int PlateRadius = 12, ChipRadius = 8, FrameWidth = 3, ChipEdge = 2;
        string signature;

        public IReadOnlyList<PartyPlate> Plates { get; private set; } = Array.Empty<PartyPlate>();

        public static LobbyPartyChips Create(Transform parent, string name, bool centred)
        {
            var row = LobbyKit.Row(parent, name, Spacing);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childAlignment = centred ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            return row.gameObject.AddComponent<LobbyPartyChips>();
        }

        public void Show(IReadOnlyList<string> lines)
        {
            var next = PlayPageText.OnlineParty(lines);
            string key = PartyPlates.Signature(next);
            if (key == signature) return;
            signature = key;
            Plates = next;
            LobbyKit.Clear(transform);
            foreach (var plate in next) Build(plate);
        }

        void Build(PartyPlate plate)
        {
            Color seat = SeatColour(plate.Place), paper = LobbyKit.Panel;
            var rect = LobbyKit.Rect(transform, PlatePrefix + (plate.Place + 1));
            LobbyKit.Face(rect, paper, PlateRadius, seat, FrameWidth, 0);
            var chip = LobbyKit.Rect(rect, ChipName).Pin(new Vector2(0, .5f), new Vector2(Pad, 0f), new Vector2(Chip, Chip));
            LobbyKit.Face(chip, seat, ChipRadius, LobbyKit.Navy, ChipEdge);
            var initial = LobbyKit.Display(chip, plate.Initial, ChipText, LobbyKit.ArtInk);
            initial.enableAutoSizing = true; initial.fontSizeMin = NameMinimum; initial.fontSizeMax = ChipText;
            initial.rectTransform.Fill();
            var ink = PartyPlates.Ink(new Srgb(seat.r, seat.g, seat.b), new Srgb(paper.r, paper.g, paper.b));
            var name = LobbyKit.Display(rect, plate.Name, NameText, new Color(ink.R, ink.G, ink.B), TextAlignmentOptions.MidlineLeft);
            name.name = NameName;
            float nameWidth = Mathf.Clamp(Mathf.Ceil(name.GetPreferredValues(plate.Name).x) + Slack, NameLeast, NameWidest);
            name.enableAutoSizing = true; name.fontSizeMin = NameMinimum; name.fontSizeMax = NameText;
            float left = Pad + Chip + Gap;
            name.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(left, 0f), new Vector2(left + nameWidth, 0f));
            float width = left + nameWidth + Pad;
            if (plate.Tag.Length > 0)
            {
                var tag = LobbyKit.Caps(rect, plate.Tag, TagText, TextAlignmentOptions.MidlineLeft);
                tag.name = TagName;
                float tagWidth = Mathf.Ceil(tag.GetPreferredValues(tag.text).x) + Slack;
                tag.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(width - Pad + Gap, 0f), new Vector2(width - Pad + Gap + tagWidth, 0f));
                width += Gap + tagWidth;
            }
            rect.Size(width, Height);
        }

        static Color SeatColour(int place) => GameAssets.I ? GameAssets.I.PlayerColor(place) : LobbyKit.Lime;
    }
}
