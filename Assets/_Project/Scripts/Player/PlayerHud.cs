using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>Letters carried and the word wheel, floating above the player's head.</summary>
    public class PlayerHud : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] TextMeshPro lettersText;
        [SerializeField] TextMeshPro wheelText;
        [SerializeField] float height = 1.75f;

        readonly StringBuilder sb = new();
        string lastLetters, lastWheel;

        void LateUpdate()
        {
            if (!player) return;
            transform.position = player.Body.position + Vector3.up * height;
            Popup.Billboard(transform);

            SetIfChanged(lettersText, LettersLine(), ref lastLetters);
            SetIfChanged(wheelText, player.Summoner.IsSpelling ? WheelLines() : "", ref lastWheel);
        }

        static void SetIfChanged(TextMeshPro t, string value, ref string last)
        {
            if (value == last) return;
            last = value;
            t.text = value;
        }

        string LettersLine()
        {
            var inv = player.Inventory;
            var spell = player.Summoner;
            sb.Clear();
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(player.Color)).Append('>')
              .Append(player.Name).Append("</color> ");
            if (player.IsKnockedOut) return sb.Append("<color=#FFFFFF>KO</color>").ToString();

            for (int i = 0; i < inv.Capacity; i++)
            {
                if (i < inv.Count)
                {
                    char c = inv.Letters[i];
                    bool used = spell.IsSpelling && spell.Picked.Contains(i);
                    bool highlighted = spell.IsSpelling && spell.Cursor == i;
                    string hex = used ? "FFFFFF44" : LetterScores.RarityOf(c) switch
                    {
                        LetterRarity.Legendary => "FFD24A",
                        LetterRarity.Rare => "7FD6CB",
                        _ => "FFF4E0"
                    };
                    if (highlighted) sb.Append("<size=135%><color=#FFD24A>[").Append(c).Append("]</color></size>");
                    else sb.Append("<color=#").Append(hex).Append('>').Append(c).Append("</color>");
                }
                else sb.Append("<color=#FFFFFF55>·</color>");
                if (i < inv.Capacity - 1) sb.Append(' ');
            }
            return sb.ToString();
        }

        /// <summary>Above the letters while spelling: key help, words you could finish, and the word so far.</summary>
        string WheelLines()
        {
            var s = player.Summoner;
            sb.Clear();
            if (player.Binding != null && player.Binding.SpellHelp.Length > 0)
                sb.Append("<size=55%><color=#FFFFFFAA>").Append(player.Binding.SpellHelp).Append("</color></size>\n");
            if (s.Hints.Count > 0)
            {
                sb.Append("<size=75%><color=#FFFFFF99>");
                for (int i = 0; i < s.Hints.Count; i++) sb.Append(i > 0 ? "   " : "").Append(s.Hints[i].word);
                sb.Append("</color></size>\n");
            }
            else if (s.Match == null)
            {
                sb.Append("<size=70%><color=#FF9A7A>")
                  .Append(s.Spelled.Length == 0 ? "no words from these letters yet" : "no word starts like that")
                  .Append("</color></size>\n");
            }

            string spelled = s.Spelled;
            sb.Append("<size=150%>");
            if (spelled.Length == 0) sb.Append("<color=#FFFFFF88>spell!</color>");
            else if (s.Match != null) sb.Append("<color=#8FE08A>").Append(spelled).Append("!</color>");
            else sb.Append("<color=#FFD24A>").Append(spelled).Append("_</color>");
            sb.Append("</size>");
            return sb.ToString();
        }
    }
}
