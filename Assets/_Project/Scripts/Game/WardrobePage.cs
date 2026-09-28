using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    /// <summary>
    /// The dress-up page: a live, turning preview of the roommate on the left and the outfit options on the right.
    /// Shown by the <see cref="Wardrobe"/> while someone is using it.
    /// </summary>
    public class WardrobePage : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] RawImage preview;
        [SerializeField] TextMeshProUGUI title;
        [SerializeField] TextMeshProUGUI rows;
        [SerializeField] TextMeshProUGUI hint;

        readonly StringBuilder sb = new();

        public bool Visible => root && root.activeSelf;
        public Texture PreviewTexture => preview ? preview.texture : null;

        public void Show(Texture previewTexture)
        {
            if (preview) preview.texture = previewTexture;
            if (root) root.SetActive(true);
        }

        public void Hide()
        {
            if (root) root.SetActive(false);
        }

        public void Refresh(PlayerController p, PlayerLook look, Wardrobe.Row selected)
        {
            if (!Visible) return;
            if (title) title.text = $"<color=#{ColorUtility.ToHtmlStringRGB(p.Color)}>{p.Name}</color>  DRESS UP";

            sb.Clear();
            Line(Wardrobe.Row.Colour, "Colour", $"<color=#{ColorUtility.ToHtmlStringRGB(p.Color)}>{Looks.ColourName(look)}</color>", selected);
            Line(Wardrobe.Row.Letter, "Sweater", look.initial.ToString(), selected);
            Line(Wardrobe.Row.Hat, "Hat", Looks.HatLabel(look), selected);
            Line(Wardrobe.Row.Extra, "Extra", Looks.ExtraLabel(look), selected);
            Line(Wardrobe.Row.Shuffle, "Shuffle", "", selected);
            Line(Wardrobe.Row.Done, "Done", "", selected);
            if (rows) rows.text = sb.ToString();

            if (hint)
                hint.text = selected switch
                {
                    Wardrobe.Row.Shuffle => "Press grab for a surprise look",
                    Wardrobe.Row.Done => "Press grab to put it on",
                    _ => "Left/right to change  •  up/down to move  •  spell to finish",
                };
        }

        void Line(Wardrobe.Row row, string label, string value, Wardrobe.Row selected)
        {
            bool on = row == selected;
            if (on) sb.Append("<color=#FFD24A>> ");
            else sb.Append("<color=#FFF4E0CC>  ");
            sb.Append(label);
            if (value.Length > 0)
            {
                sb.Append("    ");
                sb.Append(on ? "<  " : "").Append(value).Append(on ? "  >" : "");
            }
            sb.Append("</color>\n");
        }
    }
}
