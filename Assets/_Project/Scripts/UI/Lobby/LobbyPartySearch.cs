using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class LobbyPartySearch : MonoBehaviour
    {
        public const string HostName = "Party search host", CardName = "Party search", TitleName = "Party search title";
        public const string DetailName = "Party search detail", CancelName = "Cancel party search";
        const float PopScale = 1.08f, PopSeconds = .2f;
        LobbyMenu menu;
        RectTransform card;
        TextMeshProUGUI title, detail;
        Button cancel;
        float poppedAt = -1f;

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
            bool found = party != null && party.Active && party.Found;
            LobbyMatchMoment.Track(found);
            bool show = party != null && party.Active && party.Searching && LobbyPartyStrip.Showing == 0 && !(menu && menu.Starting);
            if (card.gameObject.activeSelf != show) card.gameObject.SetActive(show);
            if (!show) return;
            Set(title, LobbyKit.Upper(party.SearchTitle));
            Set(detail, party.Problem ?? party.Heading);
            if (cancel.gameObject.activeSelf != party.Leader) cancel.gameObject.SetActive(party.Leader);
            cancel.interactable = party.CanCancel;
            if (found && LobbyMatchMoment.Cue()) poppedAt = Time.unscaledTime;
            Pop();
        }

        void Pop()
        {
            if (poppedAt < 0f) return;
            float done = Mathf.Clamp01((Time.unscaledTime - poppedAt) / PopSeconds);
            card.localScale = Vector3.one * Mathf.Lerp(PopScale, 1f, done);
            if (done >= 1f) poppedAt = -1f;
        }

        static void Set(TMP_Text text, string value)
        {
            if (text.text != value) text.text = value;
        }
    }

    public static class LobbyMatchMoment
    {
        public const GameCue Sound = GameCue.Craft;
        static bool cued;

        public static int Cues { get; private set; }

        public static void Track(bool found)
        {
            if (!found) cued = false;
        }

        public static bool Cue()
        {
            if (cued) return false;
            cued = true;
            Cues++;
            GameFeedback.Play(Sound);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Forget()
        {
            cued = false;
            Cues = 0;
        }
    }

    public sealed class LobbyMatchBand : MonoBehaviour
    {
        public const string BandName = "Match band", TitleName = "Match band title", StampName = "Match found stamp", StampWordName = "Match found word";
        public const string CountName = "Match count", CaptionName = "Match count caption", PlatesName = "Match party";
        public const string StampWord = "Match found", CountWord = "Starting in";
        public const float LandSeconds = .22f, StartScale = 2.2f, Tilt = -6f, SettleSeconds = .18f, SettleDip = .06f, PopScale = 1.25f, PopSeconds = .2f;
        const float TitleSize = 48f, TitleLeast = 24f, StampSize = 64f, StampLeast = 32f, CountSize = 110f, CountLeast = 24f, CaptionSize = 20f;
        const float StampWidth = 620f, StampHeight = 130f, StampInset = 18f, TitleHeight = 76f, CountWidth = 600f, CountHeight = 130f, CaptionHeight = 30f;
        const float TitleAt = .76f, CaptionAt = .58f, CountAt = .42f, PlatesAt = .16f, PlatesInset = 24f, Dash = 4f;
        const int FaceRadius = 14, StampRadius = 10;
        static readonly Color StampPaper = LobbyKit.Hex(0xFFF6E8);

        readonly List<CanvasGroup> covers = new List<CanvasGroup>();
        RectTransform stamp, countRect;
        CanvasGroup stampInk;
        TextMeshProUGUI title, count, caption;
        LobbyPartyChips plates;
        float foundAt = -1f, poppedAt = -1f;
        bool landed;
        int shownCount = -1;

        public bool Found => foundAt >= 0f;
        public bool Landed => landed;
        public LobbyPartyChips Plates => plates;

        public static LobbyMatchBand Create(RectTransform band, IEnumerable<RectTransform> covered)
        {
            var rect = LobbyKit.Rect(band, BandName).Fill();
            LobbyKit.Face(rect, LobbyKit.Panel, FaceRadius, LobbyKit.Line, 1, 0).raycastTarget = true;
            var view = rect.gameObject.AddComponent<LobbyMatchBand>();
            view.Build(rect, covered);
            rect.gameObject.SetActive(false);
            return view;
        }

        void Build(RectTransform rect, IEnumerable<RectTransform> covered)
        {
            foreach (var part in covered)
            {
                if (!part) continue;
                if (!part.TryGetComponent(out CanvasGroup group)) group = part.gameObject.AddComponent<CanvasGroup>();
                covers.Add(group);
            }
            title = LobbyKit.Display(rect, "", TitleSize, LobbyKit.Navy);
            title.name = TitleName;
            title.enableAutoSizing = true; title.fontSizeMin = TitleLeast; title.fontSizeMax = TitleSize;
            title.rectTransform.Pin(new Vector2(.5f, TitleAt), Vector2.zero, new Vector2(StampWidth, TitleHeight));
            stamp = LobbyKit.Rect(rect, StampName).Pin(new Vector2(.5f, TitleAt), Vector2.zero, new Vector2(StampWidth, StampHeight));
            stamp.Paint(StampPaper, StampRadius).raycastTarget = false;
            LobbyParcel.Dashes(stamp, LobbyParcel.Stamp, Dash);
            stampInk = stamp.gameObject.AddComponent<CanvasGroup>();
            var word = LobbyKit.Display(stamp, LobbyKit.Upper(StampWord), StampSize, LobbyParcel.Stamp);
            word.name = StampWordName;
            word.enableAutoSizing = true; word.fontSizeMin = StampLeast; word.fontSizeMax = StampSize;
            word.rectTransform.Place(Vector2.zero, Vector2.one, Vector2.one * StampInset, -Vector2.one * StampInset);
            stamp.localRotation = Quaternion.Euler(0f, 0f, Tilt);
            caption = LobbyKit.Text(rect, CountWord, CaptionSize, LobbyKit.Muted, TextAlignmentOptions.Center);
            caption.name = CaptionName;
            caption.rectTransform.Pin(new Vector2(.5f, CaptionAt), Vector2.zero, new Vector2(CountWidth, CaptionHeight));
            countRect = LobbyKit.Rect(rect, CountName).Pin(new Vector2(.5f, CountAt), Vector2.zero, new Vector2(CountWidth, CountHeight));
            count = LobbyKit.Display(countRect, "", CountSize, LobbyKit.Navy);
            count.enableAutoSizing = true; count.fontSizeMin = CountLeast; count.fontSizeMax = CountSize;
            count.rectTransform.Fill();
            plates = LobbyPartyChips.Create(rect, PlatesName, true);
            ((RectTransform)plates.transform).Place(new Vector2(0, PlatesAt), new Vector2(1, PlatesAt),
                new Vector2(PlatesInset, -LobbyPartyChips.Height * .5f), new Vector2(-PlatesInset, LobbyPartyChips.Height * .5f));
        }

        public void Show(IWordboundLobbyParty party)
        {
            bool show = party != null;
            LobbyMatchMoment.Track(show && party.Found);
            if (gameObject.activeSelf != show)
            {
                gameObject.SetActive(show);
                Cover(show);
                if (!show) { foundAt = poppedAt = -1f; landed = false; shownCount = -1; }
            }
            if (!show) return;
            plates.Show(party.Members);
            bool found = party.Found;
            if (found && foundAt < 0f) { foundAt = Time.unscaledTime; landed = false; Land(0f); }
            if (!found) { foundAt = -1f; landed = false; }
            int left = found ? PlayPageText.StartCount(party.SearchTitle) : -1;
            if (title.gameObject.activeSelf == found) title.gameObject.SetActive(!found);
            if (stamp.gameObject.activeSelf != found) stamp.gameObject.SetActive(found);
            if (countRect.gameObject.activeSelf != found) countRect.gameObject.SetActive(found);
            if (caption.gameObject.activeSelf != left > 0) caption.gameObject.SetActive(left > 0);
            if (!found) Set(title, LobbyKit.Upper(party.SearchTitle));
            else Set(count, left > 0 ? left.ToString(CultureInfo.InvariantCulture) : party.SearchTitle);
            if (found && left != shownCount) { shownCount = left; poppedAt = Time.unscaledTime; }
        }

        void Cover(bool covered)
        {
            foreach (var group in covers)
            {
                if (!group) continue;
                group.interactable = !covered;
                group.blocksRaycasts = !covered;
            }
        }

        void OnDisable() => Cover(false);

        void Update()
        {
            float now = Time.unscaledTime;
            if (foundAt >= 0f) Land(Mathf.Clamp01((now - foundAt) / LandSeconds));
            if (poppedAt < 0f) return;
            float done = Mathf.Clamp01((now - poppedAt) / PopSeconds);
            countRect.localScale = Vector3.one * Mathf.Lerp(PopScale, 1f, done);
            if (done >= 1f) poppedAt = -1f;
        }

        void Land(float progress)
        {
            float scale = Mathf.Lerp(StartScale, 1f, progress * progress);
            if (progress >= 1f)
            {
                if (!landed) { landed = true; LobbyMatchMoment.Cue(); }
                float settle = Mathf.Clamp01((Time.unscaledTime - foundAt - LandSeconds) / SettleSeconds);
                scale = 1f - SettleDip * Mathf.Sin(settle * Mathf.PI);
            }
            stamp.localScale = Vector3.one * scale;
            stampInk.alpha = Mathf.Clamp01(progress * 2f);
        }

        static void Set(TMP_Text text, string value)
        {
            if (text.text != value) text.text = value;
        }
    }
}
