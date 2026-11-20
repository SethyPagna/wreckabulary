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
        public const string RoomModeCard = "Room mode ", TeamSizeChoice = "Room team size", LayoutChoice = "Room layout";
        public const string RoomCodeField = "Room code field", JoinRoom = "Join room", CreateRoom = "Create room GO";
        public const string BackToPlaylists = "Back to playlists", CustomMap = "Host a custom map", FriendsSummary = "Friends summary";
        public const string NotARoomCode = "That isn't a room code. Copy the whole code from your friend.";
        public const string CustomMatchNote = "Custom match note", AmongYourselves = "Play among yourselves. Only people with the code can join.";
        public bool Friends;
        string roomCodeDraft = "";

        void ShowFriends()
        {
            Friends = true;
            Refresh();
            Reselect(CreateRoom);
        }

        void ShowPlaylists()
        {
            Friends = false;
            Refresh();
            Reselect(CustomMatch);
        }

        void FriendsChoices(RectTransform column, RectTransform body)
        {
            var choice = new FriendRoomChoice(PlayerPrefs.GetString(FriendRooms.StorageKey, ""));
            bool available = Session.CanLoad(Session.WordboundScene);
            var selected = choice.Selected;
            var sizes = FriendRooms.TeamSizes.Select(size => (TeamSizeChoice + " " + size, FriendRooms.TeamLabel(size))).ToArray();
            TeamSizeRowFor(column, TeamSizeChoice, sizes, TeamSizeChoice + " " + choice.TeamSize,
                id => SaveRoomChoice(next => next.ChooseTeamSize(id.Substring(TeamSizeChoice.Length + 1)), id),
                selected.Teams ? new string[0] : sizes.Select(size => size.Item1).ToArray(), selected.Teams ? null : NoTeams(selected));
            var top = LobbyKit.Row(column, "Host a room row", 16);
            top.Size(-1, 28);
            LobbyKit.SectionLabel(top, "Host a room");
            var note = LobbyKit.Text(top, AmongYourselves, 17, LobbyKit.Muted, TextAlignmentOptions.BottomRight);
            note.name = CustomMatchNote;
            note.Size(-1, 28, 1);
            var cards = MapCards(column, "Room modes");
            var grid = cards.GetComponent<LobbyPlayCardGrid>();
            grid.MaximumColumns = Mathf.Max(grid.MaximumColumns, FriendRooms.All.Count);
            foreach (var room in FriendRooms.All) RoomModeCardFor(cards, choice, room);
            LobbyKit.SectionLabel(column, "Map");
            LobbyKit.Segmented(column, LayoutChoice,
                Enumerable.Range(0, FriendRooms.Layouts).Select(index => (LayoutChoice + " " + (index + 1), OnlineMaps.For(choice.Selected.Mode, index))).ToArray(),
                LayoutChoice + " " + (choice.Layout + 1),
                id => SaveRoomChoice(next => next.ChooseLayout(int.Parse(id.Substring(LayoutChoice.Length + 1)) - 1), id), 48, 18).Size(-1, 48);
            LobbyKit.SectionLabel(column, "Join a room");
            var join = LobbyKit.Row(column, "Join row", 12);
            join.Size(-1, 52);
            TMP_InputField field = null;
            field = CodeInput(join, RoomCodeField, "Paste a room code", roomCodeDraft, value => roomCodeDraft = value, () => JoinWith(field));
            field.interactable = available;
            var joinButton = LobbyKit.Pill(join, JoinRoom, "Join", 20, () => JoinWith(field));
            joinButton.Size(180, 48);
            joinButton.interactable = available;
            var custom = LobbyKit.TextAction(column, CustomMap, "Host a custom map from the Map editor", 17, Menu.HostCustomMap,
                icon: LobbyIcons.Layers, ink: LobbyKit.Muted);
            custom.Size(-1, 40);
            custom.interactable = available;

            var bar = SummaryFrame(body, FriendsSummary);
            SummaryLine(bar);
            var heading = LobbyKit.Text(bar, choice.Summary(), 26, LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
            heading.name = "Friends room";
            heading.enableAutoSizing = true; heading.fontSizeMin = 18; heading.fontSizeMax = 26;
            heading.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 44), new Vector2(-560, -8));
            string region = QuickPlayRegions.Name(QuickPlayRegions.Nearest(QuickPlayRegions.LocalOffsetMinutes()));
            var detail = LobbyKit.Text(bar, available
                    ? FriendRooms.Players(selected.Mode, choice.Format) + MapCardText.Separator + "Online" + MapCardText.Separator + region
                    : "Online play is unavailable in this build.",
                18, available ? LobbyKit.Muted : LobbyKit.Hot, TextAlignmentOptions.TopLeft);
            detail.name = "Friends detail";
            detail.enableAutoSizing = true; detail.fontSizeMin = 14; detail.fontSizeMax = 18;
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-560, -48));
            var back = LobbyKit.Pill(bar, BackToPlaylists, "Playlists", 18, ShowPlaylists);
            ((RectTransform)back.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-544, -22), new Vector2(-332, 22));
            var go = LobbyKit.Primary(bar, CreateRoom, "Create room",
                () => Menu.HostFriendsRoom(selected.Mode, choice.Format, choice.Layout), 36, 26);
            ((RectTransform)go.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-316, -30), new Vector2(-8, 30));
            go.interactable = available;
            if (Aim != null)
            {
                var aimed = Root.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.name == Aim && s.IsInteractable());
                First = aimed ? aimed : go;
                Aim = null;
            }
            else First = available ? go : back;
        }

        public static string NoTeams(FriendRoomMode room) => room.Label + " has no teams";

        void RoomModeCardFor(Transform cards, FriendRoomChoice choice, FriendRoomMode room)
        {
            string name = RoomModeCard + room.Id;
            var card = MapCard(cards, name, choice.Mode == room.Id, () => SaveRoomChoice(next => next.Choose(room.Id), name));
            var picture = room.Mode == "FreeForAll"
                ? playLayout ? playLayout.CombatArenaPreview : null
                : LobbyMapSnapshots.For(WordboundSoloEntry.LostAddressMap) ?? CoursePreview(WordboundSoloEntry.LostAddressMap);
            MapWords(card, CardPicture(card, picture, LobbyIcons.Layers), room.Label, room.Detail);
        }

        void SaveRoomChoice(System.Func<FriendRoomChoice, bool> change, string aim)
        {
            var choice = new FriendRoomChoice(PlayerPrefs.GetString(FriendRooms.StorageKey, ""));
            if (!change(choice)) return;
            PlayerPrefs.SetString(FriendRooms.StorageKey, choice.Save());
            PlayerPrefs.Save();
            Refresh();
            Reselect(aim);
        }

        TMP_InputField CodeInput(RectTransform row, string name, string placeholder, string draft,
            System.Action<string> keep, System.Action submit)
        {
            var holder = LobbyKit.Rect(row, name);
            holder.gameObject.SetActive(false);
            holder.Size(-1, 48, 1);
            holder.Paint(LobbyKit.Raised, 6);
            LobbyKit.Frame(holder, LobbyKit.Line, 6, 1);
            var area = LobbyKit.Rect(holder, "Text area").Place(Vector2.zero, Vector2.one, new Vector2(14, 2), new Vector2(-14, -2));
            area.gameObject.AddComponent<RectMask2D>();
            var hint = LobbyKit.Text(area, placeholder, 20, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            hint.rectTransform.Fill();
            var text = LobbyKit.Text(area, "", 20, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            text.overflowMode = TextOverflowModes.Overflow;
            text.rectTransform.Fill();
            var field = holder.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = FriendRooms.InviteLength;
            field.customCaretColor = true;
            field.caretColor = LobbyKit.Navy;
            field.caretWidth = 2;
            field.selectionColor = new Color(LobbyKit.Selection.r, LobbyKit.Selection.g, LobbyKit.Selection.b, .5f);
            field.text = draft;
            field.onValueChanged.AddListener(value => keep(value));
            field.onSubmit.AddListener(_ => submit());
            holder.gameObject.SetActive(true);
            return field;
        }

        void JoinWith(TMP_InputField field)
        {
            string code = (field.text ?? "").Trim();
            if (code.Length == 0)
            {
                string copied = GUIUtility.systemCopyBuffer;
                if (FriendRooms.LooksLikeInvite(copied))
                {
                    code = copied.Trim();
                    field.text = code;
                }
            }
            if (!FriendRooms.LooksLikeInvite(code))
            {
                Menu.Post(NotARoomCode);
                return;
            }
            Menu.JoinFriendsRoom(code);
        }
    }
}
