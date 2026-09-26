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
            sb.Clear();
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(player.Color)).Append('>')
              .Append(player.Name).Append("</color> ");
            if (player.IsKnockedOut) return sb.Append("<color=#FFFFFF>KO</color>").ToString();

            for (int i = 0; i < inv.Capacity; i++)
            {
                if (i < inv.Count)
                {
                    char c = inv.Letters[i];
                    string hex = LetterScores.RarityOf(c) switch
                    {
                        LetterRarity.Legendary => "FFD24A",
                        LetterRarity.Rare => "7FD6CB",
                        _ => "FFF4E0"
                    };
                    sb.Append("<color=#").Append(hex).Append('>').Append(c).Append("</color>");
                }
                else sb.Append("<color=#FFFFFF55>·</color>");
                if (i < inv.Capacity - 1) sb.Append(' ');
            }
            return sb.ToString();
        }

        string WheelLines()
        {
            var s = player.Summoner;
            sb.Clear();
            if (s.Ready.Count == 0) sb.Append("<color=#FFFFFFAA>no words yet</color>\n");
            for (int i = 0; i < s.Ready.Count; i++)
            {
                var w = s.Ready[i];
                if (i == s.Selected)
                    sb.Append("<size=130%><color=#FFD24A>> ").Append(w.word).Append(" <</color></size>\n");
                else
                    sb.Append("<color=#FFF4E0>").Append(w.word).Append("</color>\n");
            }
            foreach (var (entry, missing) in s.Hints)
                sb.Append("<color=#FFFFFF66>").Append(entry.word).Append("  +").Append(missing).Append("</color>\n");
            return sb.ToString();
        }
    }
}
