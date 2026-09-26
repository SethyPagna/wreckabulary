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

        readonly StringBuilder sb = new();

        public void SetTitle(string text, string sub = "")
        {
            if (title) title.text = text;
            if (subtitle) subtitle.text = sub;
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
                  .Append(GameAssets.I.PlayerInitial(p.Index)).Append("</b> ").Append(p.Name).Append("</color>");
                if (showWins) sb.Append("  ").Append(wins(p)).Append('/').Append(roundsToWin);
                sb.Append("  <size=80%>").Append(p.IsKnockedOut ? "KO" : $"{p.Inventory.Count} letters").Append("</size>");
                sb.Append("        ");
            }
            scoreboard.text = sb.ToString();
        }
    }
}
