using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    /// <summary>
    /// A web lobby tab: a leaning near-opaque navy slab with white type, royal under the pointer, darker
    /// while pressed, and a sun slab with navy type, a navy edge and a hard drop when it is the open page.
    /// LobbyKit.Tab builds it. A Cell (LobbyKit.IconTab) is the same idea for an icon in a segmented pill:
    /// clear until the pointer arrives, and the sun slab while its page is open.
    /// </summary>
    public sealed class LobbyTab : MonoBehaviour
    {
        public enum Look { Slab, Cell }

        public Look Style;
        public int Radius = LobbyKit.TabRadius;
        public Color HoverColour = LobbyKit.TabHover;
        public Button Button { get; private set; }
        Image face, edge, glyph;
        LobbyGradient gradient;
        Shadow drop;
        TextMeshProUGUI label;
        LobbyPress press;
        bool on, hot, down;

        public bool On
        {
            get => on;
            set { on = value; Paint(); }
        }

        public void Init(Button button, Image face, Image edge, LobbyGradient gradient, Shadow drop, TextMeshProUGUI label, Image glyph)
        {
            Button = button; this.face = face; this.edge = edge; this.gradient = gradient; this.drop = drop;
            this.label = label; this.glyph = glyph;
            press = button.GetComponent<LobbyPress>();
            press.Hot = h => { hot = h; Paint(); };
            press.Pressed = d => { down = d; Paint(); };
            Paint();
        }

        void Paint()
        {
            if (!face) return;
            bool cell = Style == Look.Cell;
            face.color = on ? Color.white : down ? LobbyKit.TabPress : hot ? HoverColour : cell ? Color.clear : LobbyKit.TabIdle;
            gradient.enabled = on;
            drop.enabled = on;
            // A cell has no edge of its own until it opens: the pill round it is the edge.
            edge.enabled = on || !cell;
            edge.sprite = LobbyIcons.FrameSprite(Radius, on ? (cell ? 2 : 4) : 2);
            edge.color = on ? LobbyKit.Navy : LobbyKit.Line;
            var ink = on ? LobbyKit.Navy : LobbyKit.Cream;
            if (label) label.color = ink;
            if (glyph) glyph.color = ink;
            // The open tab sits a little bigger and doesn't lift; the others lift under the pointer.
            press.Scale = on && !cell ? 1.06f : 1f;
            press.Lift = on ? 0f : 2f;
        }
    }
}
