using System.Text;
using TMPro;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public class PlayerHud : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] TextMeshPro lettersText;
        [SerializeField] TextMeshPro wheelText;
        [SerializeField] float height = 1.95f;

        const float RingRadius = .6f, RingWidth = .07f, RingLift = .05f;

        readonly StringBuilder sb = new();
        string lastLetters, lastWheel, nameMarkup;
        WorldSpaceBillboard billboard;
        LineRenderer ring;

        public LineRenderer SeatRing => ring;

        void Awake()
        {
            billboard = GetComponent<WorldSpaceBillboard>();
            if (!billboard) billboard = gameObject.AddComponent<WorldSpaceBillboard>();
        }

        void Start()
        {
            if (!player) return;
            nameMarkup = "<color=#" + ColorUtility.ToHtmlStringRGB(player.Color) + ">" + player.Name + "</color>";
            if (CameraRig.Instance) billboard.SetCamera(CameraRig.Instance.ViewCamera);
        }

        void OnDisable()
        {
            if (ring) ring.enabled = false;
        }

        void OnDestroy()
        {
            if (ring) Destroy(ring.gameObject);
        }

        static bool IsSeat(PlayerController p) => p && p.Binding != null && p.Binding is not BotBinding;

        void LateUpdate()
        {
            if (!player) return;
            transform.position = player.transform.position + Vector3.up * height;
            bool human = IsSeat(player);
            int humans = 0;
            foreach (var seat in World.Players) if (IsSeat(seat)) humans++;
            var rig = CameraRig.Instance;
            bool shoulder = rig && rig.isActiveAndEnabled && rig.IsThirdPerson && rig.IsFollowing(player);
            bool followed = (GameHud.Active && GameHud.Active.LocalPlayer == player) || (rig && rig.IsFollowing(player)) || (human && humans == 1);
            SetIfChanged(lettersText, IdentityLine(human, humans, shoulder), ref lastLetters);
            bool remoteCraft = !followed && player.Summoner && player.Summoner.IsSpelling;
            SetIfChanged(wheelText, remoteCraft ? WheelLines() : ContextLine(), ref lastWheel);
            ShowRing(SeatTags.ShowsRing(human, player.IsEliminated));
        }

        void ShowRing(bool on)
        {
            if (!on && !ring) return;
            if (!ring)
            {
                ring = SummonEffects.Ring(player.transform, "Seat ring", player.Color, RingRadius, Quaternion.Euler(90f, 0f, 0f));
                ring.transform.localPosition = Vector3.up * RingLift;
                ring.startWidth = ring.endWidth = RingWidth;
            }
            if (ring.enabled != on) ring.enabled = on;
        }

        string IdentityLine(bool human, int humans, bool shoulder)
        {
            if (player.IsEliminated) return "";
            if (player.IsDowned)
                return $"<color=#FF967D>REVIVE · {Mathf.CeilToInt(player.Health.BleedOutLeft)}s</color>";
            bool tagged = player.Binding == null ? humans != 1 : SeatTags.ShowsTag(human, humans, shoulder);
            return tagged ? nameMarkup ?? "" : "";
        }

        string ContextLine()
        {
            var combat = player.Combat;
            if (!combat) return "";
            if (combat.IsReviving) return "<color=#98F3CA>REVIVING</color>";
            if (player.CanAct && !(combat.IsHolding && !combat.Weapon) && combat.DownedTeammateNearby())
                return "<color=#F6D98B>Hold grab to revive</color>";
            return "";
        }

        static void SetIfChanged(TextMeshPro text, string value, ref string last)
        {
            if (!text || value == last) return;
            last = value;
            text.text = value;
        }

        string WheelLines()
        {
            var summon = player.Summoner;
            sb.Clear();
            if (summon.Ready.Count == 0) return "";
            int selected = Mathf.Clamp(summon.Selected, 0, summon.Ready.Count - 1);
            return sb.Append("<color=#F6D98B>").Append(summon.Ready[selected].word).Append("</color>").ToString();
        }
    }
}
