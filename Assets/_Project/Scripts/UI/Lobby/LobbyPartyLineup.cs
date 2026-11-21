using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [DefaultExecutionOrder(ExecutionOrder)]
    public sealed class LobbyPartyLineup : MonoBehaviour
    {
        public const int ExecutionOrder = 50;
        public const string RootName = "Party line-up", LayerName = "Plates", PlatePrefix = "Party plate ", OpenName = "Add player";
        public const string ChipName = "Plate chip", NameName = "Plate name", TagName = "Plate tag", FrameName = "Edge";
        public const string PartyPanel = "Safe area/Party and friends";
        public const float NameText = 22f, NameTextMinimum = 15f, TagText = 15f, OpenText = 18f, ChipText = 18f, ChipTextMinimum = 15f;
        public const float Margin = 12f;
        const float PlateHeight = 52f, TaggedHeight = 72f, Pad = 10f, Chip = 36f, Gap = 10f;
        const float NameWidthMinimum = 64f, NameWidthMaximum = 210f, TextSlack = 4f;
        const float TagHeight = 22f, TagInset = 8f, FootDrop = 18f, ScreenMargin = 12f, PlateShadow = 3f, GrainStrength = .14f;
        const float OpenHeight = 44f, OpenPad = 14f, OpenIcon = 20f, OpenGap = 8f, OpenAlpha = .88f;
        const float SyncEvery = .25f, Moved = .05f;
        const int PlateRadius = 14, ChipRadius = 10, FrameWidth = 3, ChipEdge = 2, OpenEdge = 2;
        static readonly Vector2 Centre = new Vector2(.5f, .5f), TopCentre = new Vector2(.5f, 1f), LeftMiddle = new Vector2(0f, .5f);

        sealed class View
        {
            public PartyPlate Plate;
            public RectTransform Rect;
            public Vector2 Size;
        }

        readonly List<View> views = new List<View>();
        readonly List<PlateBox> placed = new List<PlateBox>();
        readonly Vector3[] corners = new Vector3[4];
        IReadOnlyList<PartyPlate> lineup = new PartyPlate[0];
        LobbyMenu menu;
        RectTransform layer, dock, openRect;
        Button open;
        Vector2 openSize;
        string signature;
        float nextSync;

        public IReadOnlyList<PartyPlate> Plates => lineup;
        public RectTransform Layer => layer;
        public Button Open => open;
        public bool Showing => layer && layer.gameObject.activeSelf;

        public RectTransform PlateAt(int place)
        {
            foreach (var view in views)
                if (view.Plate.Place == place) return view.Rect;
            return null;
        }

        public static LobbyPartyLineup Show(LobbyMenu menu, RectTransform page, RectTransform dock)
        {
            var pages = page ? page.parent : null;
            var safe = pages ? pages.parent : null;
            if (!menu || !safe) return null;
            var existing = safe.Find(RootName);
            var lineup = existing ? existing.GetComponent<LobbyPartyLineup>() : Create(safe, pages);
            if (!lineup) return null;
            lineup.menu = menu;
            lineup.dock = dock;
            lineup.signature = null;
            lineup.Sync();
            lineup.Arrange();
            return lineup;
        }

        static LobbyPartyLineup Create(Transform safe, Transform pages)
        {
            var host = LobbyKit.Rect(safe, RootName).Fill();
            host.SetSiblingIndex(pages.GetSiblingIndex() + 1);
            var lineup = host.gameObject.AddComponent<LobbyPartyLineup>();
            lineup.layer = LobbyKit.Rect(host, LayerName).Fill();
            return lineup;
        }

        void Update()
        {
            if (menu && menu.Current == LobbyMenu.Home && Time.unscaledTime >= nextSync) Sync();
        }

        void LateUpdate() => Arrange();

        void Sync()
        {
            nextSync = Time.unscaledTime + SyncEvery;
            var party = WordboundLobbyParty.Current;
            var friends = party != null && party.Active ? party.Members : null;
            lineup = PartyPlates.Lineup(menu.Career != null ? menu.Career.Name : null, menu.PartySize - 1, friends);
            var stage = menu.Stage;
            if (stage && stage.Camera) stage.SetPartyFriends(PartyPlates.Friends(lineup));
            string next = PartyPlates.Signature(lineup);
            if (next == signature) return;
            if (!layer.gameObject.activeInHierarchy) { signature = null; return; }
            signature = next;
            Rebuild();
        }

        void Rebuild()
        {
            bool selected = open && Selected(open.gameObject);
            foreach (var view in views) Discard(view.Rect.gameObject);
            views.Clear();
            if (open) Discard(open.gameObject);
            open = null; openRect = null;
            foreach (var plate in lineup)
            {
                if (plate.Stands) views.Add(Build(plate));
                else BuildOpen();
            }
            if (!selected) return;
            if (open && EventSystem.current) EventSystem.current.SetSelectedGameObject(open.gameObject);
            else SelectHome();
        }

        View Build(PartyPlate plate)
        {
            Color seat = SeatColour(plate.Place), paper = LobbyKit.Panel;
            var rect = LobbyKit.Rect(layer, PlatePrefix + (plate.Place + 1));
            rect.anchorMin = rect.anchorMax = Centre; rect.pivot = TopCentre;
            LobbyKit.Face(rect, paper, PlateRadius, seat, FrameWidth, PlateShadow);
            LobbyParcel.Grain(rect, GrainStrength);
            var chip = LobbyKit.Rect(rect, ChipName).Pin(LeftMiddle, new Vector2(Pad, 0f), new Vector2(Chip, Chip));
            LobbyKit.Face(chip, seat, ChipRadius, LobbyKit.Navy, ChipEdge);
            var initial = LobbyKit.Display(chip, plate.Initial, ChipText, LobbyKit.ArtInk);
            initial.enableAutoSizing = true; initial.fontSizeMin = ChipTextMinimum; initial.fontSizeMax = ChipText;
            initial.rectTransform.Fill();
            var ink = PartyPlates.Ink(new Srgb(seat.r, seat.g, seat.b), new Srgb(paper.r, paper.g, paper.b));
            var name = LobbyKit.Display(rect, plate.Name, NameText, new Color(ink.R, ink.G, ink.B), TextAlignmentOptions.MidlineLeft);
            name.name = NameName;
            float width = name.GetPreferredValues(plate.Name).x;
            name.enableAutoSizing = true; name.fontSizeMin = NameTextMinimum; name.fontSizeMax = NameText;
            bool tagged = plate.Tag.Length > 0;
            float left = Pad + Chip + Gap;
            if (tagged)
            {
                var tag = LobbyKit.Caps(rect, plate.Tag, TagText, TextAlignmentOptions.MidlineLeft);
                tag.name = TagName;
                width = Mathf.Max(width, tag.GetPreferredValues(tag.text).x);
                tag.rectTransform.Place(Vector2.zero, Vector2.right, new Vector2(left, TagInset), new Vector2(-Pad, TagInset + TagHeight));
            }
            width = Mathf.Clamp(Mathf.Ceil(width) + TextSlack, NameWidthMinimum, NameWidthMaximum);
            name.rectTransform.Place(Vector2.zero, Vector2.one,
                new Vector2(left, tagged ? TagInset + TagHeight : 0f), new Vector2(-Pad, tagged ? -TagInset : 0f));
            var size = new Vector2(left + width + Pad, tagged ? TaggedHeight : PlateHeight);
            rect.sizeDelta = size;
            return new View { Plate = plate, Rect = rect, Size = size };
        }

        void BuildOpen()
        {
            var fill = LobbyKit.Card;
            fill.a = OpenAlpha;
            open = LobbyKit.Button(layer, OpenName, fill, OpenParty, PlateRadius, LobbyKit.Line, OpenEdge);
            openRect = (RectTransform)open.transform;
            openRect.anchorMin = openRect.anchorMax = Centre; openRect.pivot = TopCentre;
            var body = open.Body();
            var icon = LobbyKit.Icon(body, LobbyIcons.Plus, LobbyKit.Cyan);
            icon.rectTransform.Pin(LeftMiddle, new Vector2(OpenPad, 0f), new Vector2(OpenIcon, OpenIcon));
            var label = LobbyKit.Text(body, PartyPlates.OpenLabel, OpenText, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            float left = OpenPad + OpenIcon + OpenGap;
            float width = Mathf.Ceil(label.GetPreferredValues(PartyPlates.OpenLabel).x) + TextSlack;
            label.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(left, 0f), new Vector2(-OpenPad, 0f));
            open.GetComponent<LobbyPress>().Hot = hot => label.color = hot ? LobbyKit.Cyan : LobbyKit.Muted;
            open.gameObject.AddComponent<LobbyHint>().Text = PartyPlates.OpenHint;
            openSize = new Vector2(left + width + OpenPad, OpenHeight);
            openRect.sizeDelta = openSize;
        }

        void OpenParty()
        {
            if (!menu || menu.Starting) return;
            var panel = menu.transform.Find(PartyPanel);
            if (panel && panel.gameObject.activeSelf) return;
            menu.ToggleParty();
        }

        void Arrange()
        {
            if (!layer) return;
            var stage = menu ? menu.Stage : null;
            bool show = stage && menu.Current == LobbyMenu.Home && stage.PartyShown && stage.Camera;
            if (layer.gameObject.activeSelf != show) layer.gameObject.SetActive(show);
            if (!show) return;
            if (signature == null) Sync();
            var area = layer.rect;
            var bounds = new PlateBox(area.xMin + ScreenMargin, area.yMin + ScreenMargin, area.xMax - ScreenMargin, area.yMax - ScreenMargin);
            PlateBox? blocked = null;
            if (dock && dock.gameObject.activeInHierarchy) blocked = LocalBox(dock);
            placed.Clear();
            foreach (var view in views)
            {
                bool standing = stage.PartyFoot(view.Plate.Place, out var foot);
                standing = Project(stage.Camera, foot, out var under) && standing;
                if (view.Rect.gameObject.activeSelf != standing) view.Rect.gameObject.SetActive(standing);
                if (!standing) continue;
                var box = PartyPlates.Settle(PlateBox.Under(under.x, under.y - FootDrop, view.Size.x, view.Size.y), bounds, blocked, Margin);
                Put(view.Rect, box);
                placed.Add(box);
            }
            ArrangeOpen(stage, bounds, blocked);
        }

        void ArrangeOpen(LobbyStage stage, PlateBox bounds, PlateBox? blocked)
        {
            if (!open) return;
            bool offer = !menu.Starting;
            var spare = stage.PartyPoint(PartyPlates.OpenOffset(stage.PresentedPartySize));
            offer = Project(stage.Camera, spare, out var under) && offer;
            if (offer)
            {
                var box = PartyPlates.Settle(PlateBox.Under(under.x, under.y - FootDrop, openSize.x, openSize.y), bounds, blocked, Margin);
                var reach = box.Grown(Gap);
                foreach (var other in placed)
                    if (reach.Overlaps(other)) offer = false;
                if (offer) Put(openRect, box);
            }
            if (open.gameObject.activeSelf == offer) return;
            bool selected = !offer && Selected(open.gameObject);
            open.gameObject.SetActive(offer);
            if (selected) SelectHome();
        }

        bool Project(Camera camera, Vector3 world, out Vector2 local)
        {
            var screen = camera.WorldToScreenPoint(world);
            local = default;
            return screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out local);
        }

        PlateBox LocalBox(RectTransform other)
        {
            other.GetWorldCorners(corners);
            Vector2 low = layer.InverseTransformPoint(corners[0]), high = layer.InverseTransformPoint(corners[2]);
            return new PlateBox(low.x, low.y, high.x, high.y);
        }

        void Put(RectTransform rect, PlateBox box)
        {
            var middle = layer.rect.center;
            var at = new Vector2(box.CentreX - middle.x, box.Top - middle.y);
            if ((rect.anchoredPosition - at).sqrMagnitude > Moved * Moved) rect.anchoredPosition = at;
        }

        void SelectHome()
        {
            var first = menu ? menu.Page<HomePage>()?.First : null;
            if (first && EventSystem.current) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        static Color SeatColour(int place) => GameAssets.I ? GameAssets.I.PlayerColor(place) : LobbyKit.Lime;

        static bool Selected(GameObject item) => EventSystem.current && EventSystem.current.currentSelectedGameObject == item;

        static void Discard(GameObject item)
        {
            item.SetActive(false);
            Destroy(item);
        }
    }
}
