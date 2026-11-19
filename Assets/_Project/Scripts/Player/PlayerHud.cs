using System.Collections.Generic;
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

        public const float RingRadius = .78f, RingWidth = .13f, RingLift = .05f;
        public const float YouRadius = .9f, YouWidth = .04f, YouLift = .055f;
        public const float TagSize = 4.2f, WheelSize = 2.6f, TagOutline = .3f;
        static readonly Color TagInk = new(.04f, .1f, .1f, 1f);
        const string SurfacePath = "Wordbound/Render/Surface";

        static readonly Dictionary<Color, Material> marks = new();
        static Material surface, tagMaterial;
        static Material tagSource;

        readonly StringBuilder sb = new();
        string lastLetters, lastWheel, nameMarkup;
        WorldSpaceBillboard billboard;
        LineRenderer ring, youRing;

        public LineRenderer SeatRing => ring;
        public LineRenderer YouRing => youRing;

        void Awake()
        {
            billboard = GetComponent<WorldSpaceBillboard>();
            if (!billboard) billboard = gameObject.AddComponent<WorldSpaceBillboard>();
            StyleTag(lettersText, TagSize);
            StyleTag(wheelText, WheelSize);
        }

        void Start()
        {
            if (!player) return;
            nameMarkup = "<color=#" + ColorUtility.ToHtmlStringRGB(Vivid(player.Color)) + ">" + player.Name + "</color>";
            if (CameraRig.Instance) billboard.SetCamera(CameraRig.Instance.ViewCamera);
        }

        void OnDisable()
        {
            if (ring) ring.enabled = false;
            if (youRing) youRing.enabled = false;
        }

        void OnDestroy()
        {
            if (ring) Destroy(ring.gameObject);
            if (youRing) Destroy(youRing.gameObject);
        }

        static bool IsSeat(PlayerController p) => p && p.Binding != null && p.Binding is not BotBinding;

        public static Color Vivid(Color colour)
        {
            var (r, g, b) = PlayerBar.Vivid(colour.r, colour.g, colour.b);
            return new Color(r, g, b, 1f);
        }

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
            bool wrecked = player.IsEliminated;
            ShowRing(SeatTags.ShowsRing(wrecked));
            ShowYouRing(SeatTags.MarksYou(IsYou(human, humans), wrecked));
        }

        bool IsYou(bool human, int humans)
        {
            var hud = GameHud.Active;
            if (hud && hud.LocalPlayer) return hud.LocalPlayer == player;
            return human && humans == 1;
        }

        void ShowRing(bool on)
        {
            if (!on && !ring) return;
            if (!ring) ring = MakeRing("Seat ring", Vivid(player.Color), RingRadius, RingWidth, RingLift);
            if (ring.enabled != on) ring.enabled = on;
        }

        void ShowYouRing(bool on)
        {
            if (!on && !youRing) return;
            if (!youRing) youRing = MakeRing("You ring", Color.white, YouRadius, YouWidth, YouLift);
            if (youRing.enabled != on) youRing.enabled = on;
        }

        LineRenderer MakeRing(string label, Color colour, float radius, float width, float lift)
        {
            var line = SummonEffects.Ring(player.transform, label, colour, radius, Quaternion.Euler(90f, 0f, 0f));
            line.transform.localPosition = Vector3.up * lift;
            line.startWidth = line.endWidth = width;
            line.sharedMaterial = Mark(colour);
            return line;
        }

        public static Material Mark(Color colour)
        {
            colour.a = 1f;
            if (marks.TryGetValue(colour, out var cached) && cached) return cached;
            if (!surface) surface = Resources.Load<Material>(SurfacePath);
            Material made;
            if (surface && surface.shader)
            {
                made = new Material(surface) { name = "Seat mark" };
                if (made.HasProperty("_BaseColor")) made.SetColor("_BaseColor", colour);
                if (made.HasProperty("_Color")) made.SetColor("_Color", colour);
            }
            else
            {
                made = new Material(GameAssets.I.Tinted(colour)) { name = "Seat mark" };
                made.EnableKeyword("_EMISSION");
                if (made.HasProperty("_EmissionColor")) made.SetColor("_EmissionColor", colour);
                made.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            marks[colour] = made;
            return made;
        }

        static void StyleTag(TextMeshPro text, float size)
        {
            if (!text) return;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            var material = TagMaterial(text.font ? text.font.material : text.fontSharedMaterial);
            if (material) text.fontSharedMaterial = material;
        }

        static Material TagMaterial(Material source)
        {
            if (!source) return null;
            if (tagMaterial && tagSource == source) return tagMaterial;
            tagSource = source;
            tagMaterial = new Material(source) { name = "Seat tag" };
            tagMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
            tagMaterial.SetColor("_OutlineColor", TagInk);
            tagMaterial.SetFloat("_OutlineWidth", TagOutline);
            tagMaterial.SetFloat("_FaceDilate", .12f);
            ShaderUtilities.UpdateShaderRatios(tagMaterial);
            return tagMaterial;
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
