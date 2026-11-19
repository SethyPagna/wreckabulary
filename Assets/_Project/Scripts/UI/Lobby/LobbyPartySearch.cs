using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbyPartySearch : MonoBehaviour
    {
        public const string HostName = "Party search host", CardName = "Party search", TitleName = "Party search title";
        public const string DetailName = "Party search detail", CancelName = "Cancel party search";
        LobbyMenu menu;
        RectTransform card;
        TextMeshProUGUI title, detail;
        Button cancel;

        public static RectTransform Build(RectTransform parent, LobbyMenu menu, float top)
        {
            var root = LobbyKit.Rect(parent, HostName).Pin(new Vector2(.5f, 1), new Vector2(0, top), new Vector2(560, 88));
            var search = root.gameObject.AddComponent<LobbyPartySearch>();
            search.menu = menu;
            search.card = LobbyKit.Rect(root, CardName).Fill();
            LobbyKit.PanelFace(search.card, 18, 4, 6).raycastTarget = true;
            LobbyKit.Rect(search.card, "Accent").Place(Vector2.zero, new Vector2(0, 1), new Vector2(14, 14), new Vector2(24, -14))
                .Paint(LobbyKit.Cyan, 5).raycastTarget = false;
            search.title = LobbyKit.Display(search.card, "", 28, LobbyKit.Sun, TextAlignmentOptions.MidlineLeft, LobbyKit.Ink.Drop);
            search.title.name = TitleName;
            search.title.enableAutoSizing = true; search.title.fontSizeMin = 18; search.title.fontSizeMax = 28;
            search.title.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(40, 42), new Vector2(-150, -8));
            search.detail = LobbyKit.Text(search.card, "", 17, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            search.detail.name = DetailName;
            search.detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(40, 10), new Vector2(-150, -48));
            search.cancel = LobbyKit.Pill(search.card, CancelName, LobbyPartyStrip.CancelWord, 20, CancelSearch);
            ((RectTransform)search.cancel.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-130, -24), new Vector2(-16, 24));
            search.card.gameObject.SetActive(false);
            return root;
        }

        static void CancelSearch() => WordboundLobbyParty.Current?.Cancel();

        void Update()
        {
            string message = WordboundLobbyParty.TakeMessage();
            if (message != null && menu) menu.Post(message);
            var party = WordboundLobbyParty.Current;
            bool show = party != null && party.Active && party.Searching && LobbyPartyStrip.Showing == 0 && !(menu && menu.Starting);
            if (card.gameObject.activeSelf != show) card.gameObject.SetActive(show);
            if (!show) return;
            Set(title, LobbyKit.Upper(party.SearchTitle));
            Set(detail, party.Problem ?? party.Heading);
            if (cancel.gameObject.activeSelf != party.Leader) cancel.gameObject.SetActive(party.Leader);
            cancel.interactable = party.CanCancel;
        }

        static void Set(TMP_Text text, string value)
        {
            if (text.text != value) text.text = value;
        }
    }
}
