using System.Linq;
using TMPro;
using UnityEngine;
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
        public const string NotAPartyCode = "That isn't a party code. Ask your friend to copy it again.";
        public bool Online;
        string partyCodeDraft = "";

        void MatchmakingChoices(RectTransform column, RectTransform body)
        {
            var choices = new QuickPlayChoices(PlayerPrefs.GetString(QuickPlayChoices.StorageKey, ""));
            PartyStripFor(column, Session.CanLoad(Session.WordboundScene));
            foreach (string group in QuickPlaylists.Groups)
            {
                LobbyKit.SectionLabel(column, group);
                var cards = MapCards(column, "Playlists " + group);
                foreach (var list in QuickPlaylists.All.Where(list => list.Group == group)) PlaylistCardFor(cards, choices, list);
            }
            bool available = Session.CanLoad(Session.WordboundScene);
            var bar = SummaryFrame(body, MatchmakingSummary);
            SummaryLine(bar);
            var heading = LobbyKit.Text(bar, choices.Summary(), 26, LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
            heading.name = "Matchmaking playlists";
            heading.enableAutoSizing = true; heading.fontSizeMin = 18; heading.fontSizeMax = 26;
            heading.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 44), new Vector2(-560, -8));
            string region = QuickPlayRegions.Name(QuickPlayRegions.Nearest(QuickPlayRegions.LocalOffsetMinutes()));
            var detail = LobbyKit.Text(bar, available ? "Online" + MapCardText.Separator + region : "Online play is unavailable in this build.",
                18, available ? LobbyKit.Muted : LobbyKit.Hot, TextAlignmentOptions.TopLeft);
            detail.name = "Matchmaking detail";
            detail.enableAutoSizing = true; detail.fontSizeMin = 14; detail.fontSizeMax = 18;
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-560, -48));
            var friends = LobbyKit.Pill(bar, CustomMatch, "Custom match", 18, ShowFriends);
            ((RectTransform)friends.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-544, -22), new Vector2(-332, 22));
            friends.interactable = available;
            var go = LobbyKit.Primary(bar, FindMatch, "Find match", Menu.PlayOnline, 42, 26);
            ((RectTransform)go.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-316, -30), new Vector2(-8, 30));
            go.interactable = available;
            if (Aim != null)
            {
                var aimed = Root.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.name == Aim && s.IsInteractable());
                First = aimed ? aimed : go;
                Aim = null;
            }
            else if (available) First = go;
        }

        void PartyStripFor(RectTransform column, bool available)
        {
            var strip = LobbyKit.Row(column, PartyStrip, 12, 2);
            strip.Size(-1, 52);
            var label = LobbyKit.Text(strip, "Party" + MapCardText.Separator + "You", 20, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            label.name = PartyLabel;
            label.Size(150, 48);
            var invite = LobbyKit.Pill(strip, InviteFriendsButton, "Invite friends", 18, Menu.InviteFriends);
            invite.Size(220, 48);
            invite.interactable = available;
            TMP_InputField field = null;
            field = CodeInput(strip, PartyCodeField, "Join a party: paste a code", partyCodeDraft,
                value => partyCodeDraft = value, () => JoinPartyWith(field));
            field.interactable = available;
            var join = LobbyKit.Pill(strip, JoinParty, "Join", 18, () => JoinPartyWith(field));
            join.Size(130, 48);
            join.interactable = available;
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

        void PlaylistCardFor(Transform cards, QuickPlayChoices choices, QuickPlaylist list)
        {
            string name = PlaylistCard + list.Id;
            var card = MapCard(cards, name, choices.Playlists.IsTicked(list.Id), () => TogglePlaylist(list.Id, name));
            MapWords(card, CardPicture(card, PlaylistPicture(list), LobbyIcons.Layers), list.Label, list.Detail);
        }

        Sprite PlaylistPicture(QuickPlaylist list) => list.Mode == "FreeForAll"
            ? playLayout ? playLayout.CombatArenaPreview : null
            : LobbyMapSnapshots.For(WordboundSoloEntry.LostAddressMap) ?? CoursePreview(WordboundSoloEntry.LostAddressMap);

        void TogglePlaylist(string id, string name)
        {
            var choices = new QuickPlayChoices(PlayerPrefs.GetString(QuickPlayChoices.StorageKey, ""));
            if (!choices.Playlists.Toggle(id))
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
