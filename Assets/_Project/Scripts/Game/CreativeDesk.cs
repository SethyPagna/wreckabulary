using System.Text;
using TMPro;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// The desk in Creative. Walk up and press grab: play Dibs! in this room with your own rules,
    /// save or load the room (three slots), or clear it. Up/down to choose, left/right to change,
    /// grab or attack to pick, spell to get up.
    /// </summary>
    [DefaultExecutionOrder(-40)] // after input is read, before combat sees the grab press
    public class CreativeDesk : MonoBehaviour
    {
        public enum Item { Play, Rounds, Letters, Save, Load, Clear }

        static readonly string[] Labels = { "PLAY DIBS! HERE", "ROUNDS TO WIN", "STARTING LETTERS", "SAVE ROOM", "LOAD ROOM", "CLEAR ROOM" };
        static readonly int[] RoundChoices = { 1, 2, 3, 5 };
        const int Slots = 3;

        [SerializeField] PlayerJoinManager joins;
        [SerializeField] TextMeshPro menuText;
        [SerializeField] float useRange = 2.2f;

        readonly StringBuilder sb = new();
        string message;
        float messageUntil;

        public PlayerController User { get; private set; }
        public Item Selected { get; private set; }
        public int Slot { get; private set; } = 1;

        void Update()
        {
            if (User)
            {
                if (User.IsKnockedOut || User.IsHeld) { Close(); return; }
                var c = User.Commands;
                if (c.up) Step(-1);
                if (c.down) Step(1);
                if (c.left) Adjust(-1);
                if (c.right) Adjust(1);
                if (c.grab || c.attack) Activate(Selected);
                else if (c.spellDown) Close();
                return;
            }
            foreach (var p in World.Players)
            {
                if (!p.CanAct || p.Combat.IsHolding || !InRange(p) || !p.Commands.grab) continue;
                Open(p);
                break;
            }
        }

        void LateUpdate()
        {
            if (!menuText) return;
            Popup.Billboard(menuText.transform);
            menuText.text = MenuLines();
        }

        public bool InRange(PlayerController p) => World.Flat(p.transform.position - transform.position).magnitude <= useRange;

        public void Open(PlayerController p)
        {
            Close();
            User = p;
            p.Frozen = true;
            p.FaceTowards(transform.position - p.transform.position);
            Sfx.Play(Sound.SpellOpen, transform.position);
        }

        public void Close()
        {
            if (User) User.Frozen = false;
            User = null;
        }

        void Step(int d)
        {
            int n = Labels.Length;
            Selected = (Item)(((int)Selected + d + n) % n);
            Sfx.Play(Sound.SpellAdd, transform.position);
        }

        public void Select(Item item) => Selected = item;

        /// <summary>Left/right changes the highlighted setting: rounds, starting letters, or save slot.</summary>
        public void Adjust(int d)
        {
            switch (Selected)
            {
                case Item.Rounds:
                    int i = System.Array.IndexOf(RoundChoices, Session.CustomRounds);
                    Session.CustomRounds = RoundChoices[((i < 0 ? 2 : i) + d + RoundChoices.Length) % RoundChoices.Length];
                    break;
                case Item.Letters:
                    Session.CustomStarterLetters = (Session.CustomStarterLetters + d + 7) % 7;
                    break;
                case Item.Save:
                case Item.Load:
                    Slot = (Slot - 1 + d + Slots) % Slots + 1;
                    break;
                default:
                    return;
            }
            Sfx.Play(Sound.SpellAdd, transform.position, 1f, 1.2f);
        }

        /// <summary>Does what the item says. Returns true if it worked (for Play: the arena is loading).</summary>
        public bool Activate(Item item)
        {
            Selected = item;
            switch (item)
            {
                case Item.Play:
                    int players = joins ? joins.Players.Count : World.Players.Count;
                    if (players < 2) return Say("NEEDS 2 ROOMMATES", false);
                    Session.CustomRoom = RoomLayout.Capture();
                    Session.ReturnScene = Session.CreativeScene;
                    Close();
                    Sfx.Play(Sound.Cast, transform.position);
                    Session.Load(Session.CustomArenaScene);
                    return true;

                case Item.Rounds:
                case Item.Letters:
                    Adjust(1);
                    return true;

                case Item.Save:
                    var layout = RoomLayout.Capture();
                    layout.Save(Slot);
                    return Say($"SAVED {layout.Count} THINGS TO SLOT {Slot}", true);

                case Item.Load:
                    var loaded = RoomLayout.Load(Slot);
                    if (loaded == null) return Say($"SLOT {Slot} IS EMPTY", false);
                    RoomLayout.ClearScene();
                    loaded.Build(World.Transient);
                    Session.CustomRoom = loaded;
                    return Say($"LOADED SLOT {Slot}", true);

                case Item.Clear:
                    RoomLayout.ClearScene();
                    Session.CustomRoom = null;
                    return Say("ROOM CLEARED", true);
            }
            return false;
        }

        bool Say(string text, bool good)
        {
            message = text;
            messageUntil = Time.time + 2f;
            Sfx.Play(good ? Sound.Placed : Sound.Fizzle, transform.position, 0.7f);
            return good;
        }

        string Value(Item item) => item switch
        {
            Item.Rounds => Session.CustomRounds.ToString(),
            Item.Letters => Session.CustomStarterLetters.ToString(),
            Item.Save or Item.Load => $"slot {Slot}" + (RoomLayout.Exists(Slot) ? "" : " (empty)"),
            _ => "",
        };

        string MenuLines()
        {
            sb.Clear();
            bool someoneNear = false;
            foreach (var p in World.Players) someoneNear |= InRange(p);
            if (!User)
                return someoneNear ? "<color=#FFD24A>Press grab for the room menu</color>" : "<color=#FFF4E0AA>ROOM MENU</color>";

            for (int i = 0; i < Labels.Length; i++)
            {
                var item = (Item)i;
                string value = Value(item);
                if (item == Selected)
                {
                    sb.Append("<size=120%><color=#FFD24A>> ").Append(Labels[i]);
                    if (value.Length > 0) sb.Append("  <color=#8FD6FF>< ").Append(value).Append(" ></color>");
                    sb.Append(" <</color></size>\n");
                }
                else
                {
                    sb.Append("<color=#FFF4E0>").Append(Labels[i]);
                    if (value.Length > 0) sb.Append("  ").Append(value);
                    sb.Append("</color>\n");
                }
            }
            if (Time.time < messageUntil) sb.Append("<color=#FF8A6A>").Append(message).Append("</color>");
            return sb.ToString();
        }
    }
}
