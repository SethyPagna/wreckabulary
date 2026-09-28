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
        [SerializeField] TextMeshProUGUI checklist;
        [SerializeField] GameObject checklistPanel;

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

        /// <summary>The panel on the left used by Moving Day.</summary>
        public void SetChecklist(string text)
        {
            if (checklist) checklist.text = text;
            if (checklistPanel) checklistPanel.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>Moves the checklist to the top centre, over the back wall (for modes that use every corner).</summary>
        public void MoveChecklistToTop()
        {
            foreach (var rt in new[] { checklist ? checklist.rectTransform : null, checklistPanel ? (RectTransform)checklistPanel.transform : null })
            {
                if (!rt) continue;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.985f);
                rt.pivot = new Vector2(0.5f, 1f);
            }
            if (checklistPanel) ((RectTransform)checklistPanel.transform).sizeDelta = new Vector2(640f, 240f);
            if (checklist)
            {
                checklist.rectTransform.anchoredPosition = new Vector2(0f, -12f);
                checklist.alignment = TMPro.TextAlignmentOptions.Top;
            }
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
