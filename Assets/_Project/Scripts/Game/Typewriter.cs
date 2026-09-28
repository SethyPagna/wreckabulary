using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// The mode select in the house. Walk up and press grab to sit at it, step through the modes
    /// with up/down, press grab or attack to pick one, or spell to get up again.
    /// </summary>
    [DefaultExecutionOrder(-40)] // after input is read, before combat sees the grab press
    public class Typewriter : MonoBehaviour
    {
        [Serializable]
        public class Mode
        {
            public string label;
            public string blurb;
            public string scene;
            public int minPlayers = 1;
            public bool comingSoon;
            [Tooltip("Optional choice of arenas (scene names), picked with left/right. Empty uses Scene.")]
            public string[] maps = { };
            public string[] mapNames = { };
            public string SceneFor(int map) => maps.Length > 0 ? maps[Mathf.Clamp(map, 0, maps.Length - 1)] : scene;
        }

        [SerializeField] Mode[] modes =
        {
            new() { label = "TUTORIAL", blurb = "Learn to smash, spell and summon", scene = Session.TutorialScene },
            new()
            {
                label = "DIBS!", blurb = "Versus: last roommate standing", scene = Session.DibsScene, minPlayers = 2,
                maps = new[] { Session.DibsScene, Session.BedroomScene, Session.KitchenScene, Session.GardenScene },
                mapNames = new[] { "Living Room", "Bedroom", "Kitchen", "Garden" },
            },
            new() { label = "MOVING DAY", blurb = "Co-op: furnish the house together", scene = Session.MovingDayScene },
            new() { label = "HOME SWEET HOME", blurb = "Creative: build your own room", comingSoon = true },
        };
        [SerializeField] PlayerJoinManager joins;
        [SerializeField] TextMeshPro menuText;
        [SerializeField] TextMeshPro paperText;
        [SerializeField] float useRange = 2.2f;

        readonly StringBuilder sb = new();
        string message;
        float messageUntil;

        public IReadOnlyList<Mode> Modes => modes;
        public PlayerController User { get; private set; }
        public int Selected { get; private set; } = 1;
        /// <summary>The arena picked for modes that have a choice of maps.</summary>
        public int Map { get; private set; }

        void Update()
        {
            if (User)
            {
                if (User.IsKnockedOut || User.IsHeld) { Close(); return; }
                var c = User.Commands;
                if (c.up) Step(-1);
                if (c.down) Step(1);
                if (c.left) StepMap(-1);
                if (c.right) StepMap(1);
                if (c.grab || c.attack) Confirm();
                else if (c.spellDown) Close();
            }
            else
            {
                foreach (var p in World.Players)
                {
                    if (!p.CanAct || p.Combat.IsHolding || !InRange(p) || !p.Commands.grab) continue;
                    Open(p);
                    break;
                }
            }
        }

        void LateUpdate()
        {
            if (menuText)
            {
                Popup.Billboard(menuText.transform);
                menuText.text = MenuLines();
            }
            if (paperText) paperText.text = modes[Selected].label + MapLabel(modes[Selected], "\n");
        }

        public bool InRange(PlayerController p) =>
            World.Flat(p.transform.position - transform.position).magnitude <= useRange;

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

        void Step(int delta)
        {
            Selected = (Selected + delta + modes.Length) % modes.Length;
            Sfx.Play(Sound.SpellAdd, transform.position);
        }

        public void StepMap(int delta)
        {
            var maps = modes[Selected].maps;
            if (maps.Length < 2) return;
            Map = (Map + delta + maps.Length) % maps.Length;
            Sfx.Play(Sound.SpellAdd, transform.position, 1f, 1.2f);
        }

        /// <summary>Picks a mode by index. Returns true if its scene is loading.</summary>
        public bool Choose(int index)
        {
            Selected = Mathf.Clamp(index, 0, modes.Length - 1);
            return Confirm();
        }

        bool Confirm()
        {
            var m = modes[Selected];
            int players = joins ? joins.Players.Count : World.Players.Count;
            string scene = m.SceneFor(Map);
            if (m.comingSoon || !Session.CanLoad(scene)) return Say("COMING SOON");
            if (players < m.minPlayers) return Say($"NEEDS {m.minPlayers} ROOMMATES");

            Close();
            Sfx.Play(Sound.Cast, transform.position);
            Session.Load(scene);
            return true;
        }

        bool Say(string text)
        {
            message = text;
            messageUntil = Time.time + 1.5f;
            Sfx.Play(Sound.Fizzle, transform.position, 0.6f);
            return false;
        }

        string MapLabel(Mode m, string before, string open = "", string close = "")
        {
            if (m.mapNames.Length == 0) return "";
            return before + open + m.mapNames[Mathf.Clamp(Map, 0, m.mapNames.Length - 1)] + close;
        }

        static string StarsFor(Mode m)
        {
            if (m.scene != Session.MovingDayScene || Session.MovingDayStars.Count == 0) return "";
            int total = 0;
            foreach (var s in Session.MovingDayStars.Values) total += s;
            return $"  ({total} stars)";
        }

        string MenuLines()
        {
            sb.Clear();
            bool someoneNear = false;
            foreach (var p in World.Players) someoneNear |= InRange(p);

            if (!User)
            {
                sb.Append(someoneNear ? "<color=#FFD24A>Press grab to type</color>" : "<color=#FFF4E0AA>TYPEWRITER</color>");
                return sb.ToString();
            }

            for (int i = 0; i < modes.Length; i++)
            {
                var m = modes[i];
                string colour = m.comingSoon ? "#FFFFFF66" : "#FFF4E0";
                if (i == Selected)
                    sb.Append("<size=125%><color=#FFD24A>> ").Append(m.label).Append(" <</color></size>\n")
                      .Append(MapLabel(m, "", "<size=85%><color=#8FD6FF>< ", " ></color></size>\n"))
                      .Append("<size=70%><color=#FFF4E0CC>").Append(m.comingSoon ? "coming soon" : m.blurb).Append(StarsFor(m)).Append("</color></size>\n");
                else
                    sb.Append("<color=").Append(colour).Append('>').Append(m.label).Append("</color>\n");
            }
            if (Time.time < messageUntil) sb.Append("<color=#FF8A6A>").Append(message).Append("</color>");
            return sb.ToString();
        }
    }
}
