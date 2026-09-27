using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>Screen-space text: big callouts, the round timer and the scoreboard.</summary>
    public class GameHud : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI title;
        [SerializeField] TextMeshProUGUI subtitle;
        [SerializeField] TextMeshProUGUI timer;
        [SerializeField] TextMeshProUGUI scoreboard;
        [SerializeField] TextMeshProUGUI instruction;

        readonly StringBuilder sb = new();

        public void SetTitle(string text, string sub = "")
        {
            if (title) title.text = text;
            if (subtitle) subtitle.text = sub;
        }

        /// <summary>The line near the top of the screen used by the house and the tutorial.</summary>
        public void SetInstruction(string main, string hint = "")
        {
            if (instruction)
                instruction.text = string.IsNullOrEmpty(hint) ? main : $"{main}\n<size=65%><color=#FFF4E0CC>{hint}</color></size>";
        }

        public void SetTimer(string text)
        {
            if (timer) timer.text = text;
        }

        public void SetScoreboard(IReadOnlyList<PlayerController> players, Func<PlayerController, int> wins, int roundsToWin, bool showWins)
        {
            if (!scoreboard) return;
            sb.Clear();
            foreach (var p in players)
            {
                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(p.Color)).Append("><b>")
                  .Append(p.Initial).Append("</b> ").Append(p.Name).Append("</color>");
                if (showWins) sb.Append("  ").Append(wins(p)).Append('/').Append(roundsToWin);
                sb.Append("  <size=80%>").Append(p.IsKnockedOut ? "KO" : $"{p.Inventory.Count} letters").Append("</size>");
                sb.Append("        ");
            }
            scoreboard.text = sb.ToString();
        }
    }
}
