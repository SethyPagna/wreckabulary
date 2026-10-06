using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    /// <summary>
    /// A web lobby tab: a leaning navy-glass slab with white type, navy under the pointer, and a sun
    /// slab with navy type, a navy edge and a hard drop when it is the open page. LobbyKit.Tab builds it.
    /// </summary>
    public sealed class LobbyTab : MonoBehaviour
    {
        public Button Button { get; private set; }
        Image face, edge, glyph;
        LobbyGradient gradient;
        Shadow drop;
        TextMeshProUGUI label;
        LobbyPress press;
        bool on, hot;

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
            Paint();
        }

        void Paint()
        {
            if (!face) return;
            face.color = on ? Color.white : hot ? LobbyKit.TabHover : LobbyKit.TabIdle;
            gradient.enabled = on;
            drop.enabled = on;
            edge.sprite = LobbyIcons.FrameSprite(LobbyKit.TabRadius, on ? 3 : 2);
            edge.color = on ? LobbyKit.Navy : LobbyKit.Line;
            var ink = on ? LobbyKit.Navy : LobbyKit.Cream;
            if (label) label.color = ink;
            if (glyph) glyph.color = ink;
            // The open tab sits a little bigger and doesn't lift; the others lift under the pointer.
            press.Scale = on ? 1.06f : 1f;
            press.Lift = on ? 0f : 2f;
        }
    }
}
