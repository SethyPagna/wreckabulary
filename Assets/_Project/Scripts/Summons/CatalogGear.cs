using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>One physical representation for an enabled catalogue recipe; data selects its behavior.</summary>
    public static class CatalogGear
    {
        public static HeldWeapon Create(ItemDefinition item)
        {
            var root = new GameObject(item.Id);
            root.transform.SetParent(World.Transient, false);
            if (!ModelVisual.Spawn(item.Model, root.transform))
            {
                var visual = LetterBuilt.Spawn(item.Id, Vector3.one * 0.2f, 3, new Color(0.8f, 0.6f, 0.4f), root.transform, false);
                visual.name = "Recipe visual";
            }
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(Mathf.Max(0.1f, item.Size[0]), Mathf.Max(0.1f, item.Size[1]), Mathf.Max(0.1f, item.Size[2]));
            box.center = Vector3.up * box.size.y * 0.5f;
            var body = root.AddComponent<Rigidbody>();
            body.mass = item.IsTwoHanded ? 4f : 1f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var gear = root.AddComponent<HeldWeapon>();
            gear.Configure(item);
            var materials = MaterialLibrary.Load();
            if (materials) materials.ApplySkin(root, Skin.Standard);
            return gear;
        }

        public static void ApplyUse(PlayerController owner, ItemDefinition item)
        {
            var use = item.Use;
            if (use == null) return;
            if (use.Effect == UseEffect.Heal) { owner.Health.Heal(use.Amount); return; }
            if (use.Effect == UseEffect.Speed) { owner.Boost(use.Amount, use.Seconds); return; }
            if (use.Effect != UseEffect.Bubble) return;
            int token = owner.Health.GiveOwnedBubble(use.Amount, use.Seconds);
            var visual = new GameObject(item.Id + " protection");
            visual.transform.SetParent(owner.visual ? owner.visual : owner.transform, false);
            visual.transform.localPosition = Vector3.up * 0.8f;
            // Three open rings remain readable from a behind-the-player camera without concealing the avatar.
            var color = GameFeedback.SkillColor(item.Id);
            var ringColor = Color.Lerp(color, Color.white, .45f);
            var front = SummonEffects.Ring(visual.transform, "Foam front", ringColor, .72f, Quaternion.identity);
            var cross = SummonEffects.Ring(visual.transform, "Foam cross", ringColor, .72f, Quaternion.Euler(0f, 90f, 0f));
            var equator = SummonEffects.Ring(visual.transform, "Foam equator", ringColor, .72f, Quaternion.Euler(90f, 0f, 0f));
            front.startWidth = front.endWidth = cross.startWidth = cross.endWidth = equator.startWidth = equator.endWidth = .018f;
            var camera = Camera.main;
            var bubbles = new Transform[7];
            for (int i = 0; i < bubbles.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / bubbles.Length;
                var bubble = SummonEffects.Ring(visual.transform, "Foam bubble", Color.Lerp(color, Color.white, .55f), .075f + .02f * (i % 3), Quaternion.identity);
                bubble.transform.localPosition = new Vector3(Mathf.Cos(angle) * .64f, Mathf.Sin(angle * 2f) * .42f, Mathf.Sin(angle) * .64f);
                bubble.startWidth = bubble.endWidth = .016f;
                bubbles[i] = bubble.transform;
            }
            GameFeedback.Play(GameCue.Protect);
            GameFeedback.Burst("Foam_Cloud", owner.transform.position + Vector3.up * .9f, .85f, color, .55f);
            var life = SummonedThing.Attach(visual, item.Id, owner, use.Seconds);
            life.ReturnsLetters = false;
            life.Tick = () =>
            {
                if (!camera) return;
                foreach (var bubble in bubbles) if (bubble) bubble.rotation = camera.transform.rotation;
            };
            life.KeepAlive = () => owner && owner.Health.OwnsBubble(token) && owner.Health.Bubble > 0f;
            life.Ended = () => { if (owner) owner.Health.ClearOwnedBubble(token); };
        }
    }
}
