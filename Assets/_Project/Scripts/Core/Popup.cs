using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>Floating callout text, like "BLADE!" or "WRECKED!".</summary>
    public class Popup : MonoBehaviour
    {
        const float Life = 1.1f;

        TextMeshPro text;
        Vector3 start;
        float born;
        Color color;
        static Camera cachedCamera;
        static readonly List<Popup> feedback = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCamera() { cachedCamera = null; feedback.Clear(); }

        public static void Damage(float amount, Vector3 at)
        {
            if (amount > 0f && CombatFeedbackOptions.ShowDamage)
                Create($"{Mathf.CeilToInt(amount)} <size=60%>DMG</size>", at, new Color(1f, .48f, .30f), 2.2f).SeparateFeedback();
        }

        public static void Points(int amount, Vector3 at)
        {
            if (amount > 0 && CombatFeedbackOptions.ShowPoints)
                Create($"+{amount} <size=60%>PTS</size>", at + Vector3.up * .45f, new Color(1f, .84f, .36f), 1.8f).SeparateFeedback();
        }

        public static void Show(string message, Vector3 at, Color color, float size = 4f) => Create(message, at, color, size);

        static Popup Create(string message, Vector3 at, Color color, float size)
        {
            var go = new GameObject("Popup");
            go.transform.SetParent(World.Transient, false);
            go.transform.position = at;

            var t = go.AddComponent<TextMeshPro>();
            t.font = GameAssets.I.font;
            t.text = message;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.color = color;
            t.outlineWidth = 0.25f;
            t.outlineColor = GameAssets.I.ink;
            t.rectTransform.sizeDelta = new Vector2(8f, 2f);

            var p = go.AddComponent<Popup>();
            p.text = t;
            p.start = at;
            p.born = Time.time;
            p.color = color;
            go.AddComponent<WorldSpaceBillboard>();
            return p;
        }

        void SeparateFeedback()
        {
            var camera = CameraRig.Instance ? CameraRig.Instance.ViewCamera : cachedCamera;
            if (!camera) camera = cachedCamera = Camera.main;
            if (!camera) return;
            var screen = camera.WorldToScreenPoint(start);
            if (screen.z <= 0f) return;
            float width = camera.pixelHeight * .18f, height = camera.pixelHeight * .06f;
            var original = screen;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                bool occupied = false;
                foreach (var other in feedback)
                {
                    if (!other || Time.time - other.born >= Life) continue;
                    var position = camera.WorldToScreenPoint(other.transform.position);
                    if (position.z > 0f && Mathf.Abs(screen.x - position.x) < width && Mathf.Abs(screen.y - position.y) < height)
                    { occupied = true; break; }
                }
                if (!occupied) break;
                int slot = attempt + 1, column = slot % 5;
                int offset = column % 2 == 1 ? -(column + 1) / 2 : column / 2;
                screen.x = Mathf.Clamp(original.x + offset * width, width * .5f, camera.pixelWidth - width * .5f);
                screen.y = Mathf.Clamp(original.y - slot / 5 * height, height, camera.pixelHeight - height);
            }
            start = transform.position = camera.ScreenToWorldPoint(screen);
            feedback.Add(this);
        }

        void OnDestroy() => feedback.Remove(this);

        void LateUpdate()
        {
            float k = (Time.time - born) / Life;
            if (k >= 1f) { Destroy(gameObject); return; }

            transform.position = start + Vector3.up * (k * 1.2f);
            float pop = k < 0.15f ? Mathf.Lerp(0.4f, 1.15f, k / 0.15f) : Mathf.Lerp(1.15f, 1f, (k - 0.15f) * 4f);
            transform.localScale = Vector3.one * pop;
            text.color = new Color(color.r, color.g, color.b, 1f - k * k);
        }

        /// <summary>Turns a world-space label to face the main camera.</summary>
        public static void Billboard(Transform t)
        {
            var rig = CameraRig.Instance;
            var cam = rig ? rig.ViewCamera : cachedCamera;
            if (!cam) cam = cachedCamera = Camera.main;
            if (cam) t.rotation = cam.transform.rotation;
        }
    }
}
