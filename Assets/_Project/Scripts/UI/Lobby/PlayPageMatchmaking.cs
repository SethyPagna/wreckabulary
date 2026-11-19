using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary
{
    public sealed partial class PlayPage
    {
        public const string PlaylistCard = "Playlist ", FindMatch = "Find match GO", CustomMatch = "Custom match";
        public const string MatchmakingSummary = "Matchmaking summary", KeepOnePlaylist = "Keep at least one playlist ticked.";
        public const string PartyStrip = "Party strip", PartyLabel = "Party label", InviteFriendsButton = "Invite friends";
        public const string PartyCodeField = "Party code field", JoinParty = "Join party";
        public const string PartySolo = "Party solo", PartyGroup = "Party group", PartyHeading = "Party heading", PartyMembers = "Party members";
        public const string CopyPartyCode = "Copy party code", LeaveParty = "Leave party";
        public const string NotAPartyCode = "That isn't a party code. Ask your friend to copy it again.";
        public const string TeamSizeWord = "Team size", TeamSizeSwitch = "Team size", TeamSizeRow = "Team size row";
        public const string TeamSizeTitle = "Team size label", TeamSizeNote = "Team size note";
        public const string PlaylistBand = "Playlist band", PlaylistSection = "Playlists ";
        public bool Online;
        string partyCodeDraft = "";

        void MatchmakingChoices(RectTransform column, RectTransform body)
        {
            var choices = new QuickPlayChoices(PlayerPrefs.GetString(QuickPlayChoices.StorageKey, ""));
            bool available = Session.CanLoad(Session.WordboundScene);
            int partySize = OnlinePartySize();
            string size = choices.SizeFor(partySize);
            TeamSizeRowFor(column, TeamSizeSwitch,
                QuickPlayCards.TeamSizes.Select(each => (TeamSizeSwitch + " " + each, QuickPlayCards.TeamLabel(each))).ToArray(), TeamSizeSwitch + " " + size,
                id => ChooseTeamSize(id.Substring(TeamSizeSwitch.Length + 1), id),
                QuickPlayCards.TeamSizes.Where(each => !QuickPlayCards.CanPlay(each, partySize)).Select(each => TeamSizeSwitch + " " + each).ToArray(),
                QuickPlayCards.CanPlay(QuickPlayCards.Duo, partySize) || partySize > QuickPlaylists.MaximumParty ? null : PartyPlaysSquad(partySize));
            var strip = PartyStripFor(column, available);
            var band = LobbyKit.Rect(column, PlaylistBand);
            band.gameObject.AddComponent<LobbyPlayCardBand>();
            band.Size(-1, LobbyPlayCardGrid.HeightFor(LobbyPlayCardGrid.PreferredWidth) + 34);
            foreach (string group in QuickPlaylists.Groups)
            {
                var section = LobbyKit.Rect(band, PlaylistSection + group);
                LobbyKit.SectionLabel(section, group).name = LobbyPlayCardBand.Caption;
                foreach (var card in QuickPlayCards.All.Where(card => card.Group == group)) PlaylistCardFor(section, choices, card, size);
            }
            var bar = SummaryFrame(body, MatchmakingSummary);
            SummaryLine(bar);
            string summary = choices.Summary(partySize: partySize);
            var heading = LobbyKit.Text(bar, summary, 26, LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
            heading.name = "Matchmaking playlists";
            heading.enableAutoSizing = true; heading.fontSizeMin = 18; heading.fontSizeMax = 26;
            heading.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 44), new Vector2(-560, -8));
            string region = QuickPlayRegions.Name(QuickPlayRegions.Nearest(QuickPlayRegions.LocalOffsetMinutes()));
            string onlineDetail = available ? "Online" + MapCardText.Separator + region : "Online play is unavailable in this build.";
            var detail = LobbyKit.Text(bar, onlineDetail, 18, available ? LobbyKit.Muted : LobbyKit.Hot, TextAlignmentOptions.TopLeft);
            detail.name = "Matchmaking detail";
            detail.enableAutoSizing = true; detail.fontSizeMin = 14; detail.fontSizeMax = 18;
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-560, -48));
            var friends = LobbyKit.Pill(bar, CustomMatch, "Custom match", 18, ShowFriends);
            ((RectTransform)friends.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-544, -22), new Vector2(-332, 22));
            friends.interactable = available;
            var go = LobbyKit.Primary(bar, FindMatch, "Find match", Menu.PlayOnline, 42, 26);
            ((RectTransform)go.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-316, -30), new Vector2(-8, 30));
            go.interactable = available;
            strip.BindFooter(heading, detail, go, friends, summary, onlineDetail, available);
            strip.WatchPartySize(partySize, KeepSelectionAndRefresh);
            if (Aim != null)
            {
                var aimed = Root.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.name == Aim && s.IsInteractable());
                First = aimed ? aimed : go;
                Aim = null;
            }
            else if (available) First = go;
        }

        public static string PartyPlaysSquad(int partySize) =>
            "A party of " + partySize + " plays " + QuickPlayCards.TeamLabel(QuickPlayCards.Squad);

        static int OnlinePartySize()
        {
            var party = WordboundLobbyParty.Current;
            return party != null && party.Active ? Mathf.Max(1, party.Members.Count) : 1;
        }

        void KeepSelectionAndRefresh()
        {
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            string name = selected && selected.transform.IsChildOf(Root) ? selected.name : null;
            Refresh();
            if (name == null) return;
            var target = Root.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.name == name && s.IsInteractable()) ??
                Root.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.name.StartsWith(TeamSizeSwitch + " ") && s.IsInteractable());
            if (target) EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        RectTransform TeamSizeRowFor(RectTransform column, string name, (string id, string label)[] sizes, string picked, Action<string> pick,
            string[] disabled, string note)
        {
            var row = LobbyKit.Row(column, name + " row", 16);
            row.Size(-1, 52);
            var title = LobbyKit.Text(row, TeamSizeWord, 20, LobbyKit.Cyan, TextAlignmentOptions.MidlineLeft);
            title.name = TeamSizeTitle;
            title.Size(130, 48);
            var track = LobbyKit.Segmented(row, name, sizes, picked, pick, 48, 20);
            track.Size(110 * sizes.Length + 8, 48);
            foreach (var button in track.GetComponentsInChildren<Button>().Where(button => disabled.Contains(button.name)))
            {
                button.interactable = false;
                button.gameObject.AddComponent<CanvasGroup>().alpha = .45f;
            }
            if (note == null) return row;
            var line = LobbyKit.Text(row, note, 17, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            line.name = TeamSizeNote;
            line.enableAutoSizing = true; line.fontSizeMin = 13; line.fontSizeMax = 17;
            line.Size(-1, 48, 1);
            return row;
        }

        void ChooseTeamSize(string size, string id)
        {
            var choices = new QuickPlayChoices(PlayerPrefs.GetString(QuickPlayChoices.StorageKey, ""));
            if (!QuickPlayCards.CanPlay(size, OnlinePartySize()) || !choices.ChooseTeamSize(size)) return;
            PlayerPrefs.SetString(QuickPlayChoices.StorageKey, choices.Save());
            PlayerPrefs.Save();
            Refresh();
            Reselect(id);
        }

        LobbyPartyStrip PartyStripFor(RectTransform column, bool available)
        {
            var strip = LobbyKit.Rect(column, PartyStrip);
            strip.gameObject.SetActive(false);
            strip.Size(-1, 52);
            var solo = LobbyKit.Row(strip, PartySolo, 12, 2);
            solo.Fill();
            var label = LobbyKit.Text(solo, "Party" + MapCardText.Separator + "You", 20, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            label.name = PartyLabel;
            label.Size(150, 48);
            var invite = LobbyKit.Pill(solo, InviteFriendsButton, "Invite friends", 18, Menu.InviteFriends);
            invite.Size(220, 48);
            invite.interactable = available;
            TMP_InputField field = null;
            field = CodeInput(solo, PartyCodeField, "Join a party: paste a code", partyCodeDraft,
                value => partyCodeDraft = value, () => JoinPartyWith(field));
            field.interactable = available;
            var join = LobbyKit.Pill(solo, JoinParty, "Join", 18, () => JoinPartyWith(field));
            join.Size(130, 48);
            join.interactable = available;
            var group = LobbyKit.Row(strip, PartyGroup, 12, 2);
            group.Fill();
            var heading = LobbyKit.Text(group, "", 20, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            heading.name = PartyHeading;
            heading.Size(150, 48);
            var members = LobbyKit.Text(group, "", 18, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            members.name = PartyMembers;
            members.enableAutoSizing = true; members.fontSizeMin = 12; members.fontSizeMax = 18;
            members.textWrappingMode = TextWrappingModes.Normal;
            members.Size(-1, 48, 1);
            var copy = LobbyKit.Pill(group, CopyPartyCode, LobbyPartyStrip.CopyCode, 18, LobbyPartyStrip.CopyNow);
            copy.Size(190, 48);
            var leave = LobbyKit.Pill(group, LeaveParty, LobbyPartyStrip.LeaveWord, 18, LobbyPartyStrip.LeaveOrRetry);
            leave.Size(130, 48);
            group.gameObject.SetActive(false);
            var live = strip.gameObject.AddComponent<LobbyPartyStrip>();
            live.BindStrip(solo.gameObject, group.gameObject, invite, heading, members, copy, leave);
            strip.gameObject.SetActive(true);
            return live;
        }

        void JoinPartyWith(TMP_InputField field)
        {
            string code = (field.text ?? "").Trim();
            if (code.Length == 0)
            {
                string copied = GUIUtility.systemCopyBuffer;
                if (FriendRooms.LooksLikePartyInvite(copied) || FriendRooms.LooksLikeInvite(copied))
                {
                    code = copied.Trim();
                    field.text = code;
                }
            }
            if (FriendRooms.LooksLikePartyInvite(code)) Menu.JoinFriendsParty(code);
            else if (FriendRooms.LooksLikeInvite(code)) Menu.JoinFriendsRoom(code);
            else Menu.Post(NotAPartyCode);
        }

        void PlaylistCardFor(Transform section, QuickPlayChoices choices, QuickPlayCard card, string size)
        {
            string name = PlaylistCard + card.Id;
            var button = MapCard(section, name, choices.Cards.IsTicked(card.Id), () => ToggleCard(card.Id, name));
            MapWords(button, CardPicture(button, PlaylistPicture(card.Mode), LobbyIcons.Layers), card.Label, card.Detail(size));
        }

        Sprite PlaylistPicture(string mode) => mode == "FreeForAll"
            ? playLayout ? playLayout.CombatArenaPreview : null
            : LobbyMapSnapshots.For(WordboundSoloEntry.LostAddressMap) ?? CoursePreview(WordboundSoloEntry.LostAddressMap);

        void ToggleCard(string id, string name)
        {
            var choices = new QuickPlayChoices(PlayerPrefs.GetString(QuickPlayChoices.StorageKey, ""));
            if (!choices.Cards.Toggle(id))
            {
                Menu.Post(KeepOnePlaylist);
                return;
            }
            PlayerPrefs.SetString(QuickPlayChoices.StorageKey, choices.Save());
            PlayerPrefs.Save();
            Refresh();
            Reselect(name);
        }
    }
}
