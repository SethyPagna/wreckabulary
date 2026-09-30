using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>
    /// What each word does when summoned. Words not listed here fall back to a default for their category,
    /// so new rows in word_list.csv work straight away.
    /// </summary>
    public static class SummonEffects
    {
        static readonly Color Wood = new(0.80f, 0.55f, 0.33f);
        static readonly Color Steel = new(0.72f, 0.76f, 0.80f);
        static readonly Color Sky = new(0.55f, 0.78f, 0.95f);

        public static void Apply(PlayerController p, WordEntry e)
        {
            Popup.Show(e.word + "!", p.OverheadPosition + Vector3.up * 0.8f, p.Color, 5f);
            CameraRig.Shake(0.06f);

            if (e.category == WordCategory.Furniture)
            {
                FurnitureCatalog.Summon(p, e.word);
                return;
            }

            switch (e.word)
            {
                // Weapons
                case "AXE": Melee(p, e.word, Steel, uses: 6, cooldown: 0.25f, reach: 1.0f, knockback: 7f, damage: 18f); break;
                case "BAT": Melee(p, e.word, Wood, uses: 5, cooldown: 0.4f, reach: 1.1f, knockback: 15f, damage: 15f); break;
                case "BLADE": Melee(p, e.word, Steel, uses: 5, cooldown: 0.35f, reach: 1.2f, knockback: 9f, damage: 25f); break;
                case "SWORD": Melee(p, e.word, Steel, uses: 6, cooldown: 0.35f, reach: 1.4f, knockback: 9f, damage: 25f); break;
                case "SPEAR": Melee(p, e.word, Wood, uses: 5, cooldown: 0.45f, reach: 2.0f, knockback: 10f, damage: 20f); break;
                case "BOW": Ranged(p, e.word, Wood, uses: 4, speed: 20f, knockback: 6f, damage: 15f, blast: 0f); break;
                case "CANNON": Ranged(p, e.word, new Color(0.3f, 0.3f, 0.34f), uses: 2, speed: 14f, knockback: 14f, damage: 50f, blast: 1.8f); break;

                // Defence
                case "WALL": Wall(p); break;
                case "SHIELD": Shield(p, e.word, 7f); break;
                case "UMBRELLA": Shield(p, e.word, 12f); break;
                case "PLATE": Plate(p, e.word); break;
                case "ARMOR": Armor(p, e.word); break;

                // Movement
                case "WINGS": Wings(p); break;
                case "SPRING": Spring(p); break;
                case "SKATES": Skates(p); break;
                case "ROPE": Rope(p); break;

                // Chaos
                case "BEES": BeeSwarm.Spawn(p); break;
                case "FLOOD": Flood(p); break;
                case "MAGNET": Magnet(p); break;
                case "DUCK": DuckWalker.Spawn(p); break;
                case "QUAKE": Quake(p); break;
                case "ZAP": Zap(p); break;

                default: Fallback(p, e); break;
            }
        }

        static void Fallback(PlayerController p, WordEntry e)
        {
            switch (e.category)
            {
                case WordCategory.Weapon: Melee(p, e.word, Wood, 5, 0.35f, 1.2f, 9f, 20f); break;
                case WordCategory.Defence: Armor(p, e.word); break;
                case WordCategory.Movement: Skates(p, e.word); break;
                default: Shockwave(p, e.word, 4f, 8f); break;
            }
        }

        // ---- Weapons ----

        static HeldWeapon MakeWeapon(PlayerController p, string word, Color color, int perRow, float block)
        {
            var built = LetterBuilt.Spawn(word, Vector3.one * block, perRow, color, World.Transient);
            var rb = built.gameObject.AddComponent<Rigidbody>();
            rb.mass = 1f;
            built.gameObject.AddComponent<Smashable>().Init(word, 15f);
            var w = built.gameObject.AddComponent<HeldWeapon>();
            w.word = word;
            p.Combat.Equip(w);
            return w;
        }

        /// <summary>A held weapon. BAT and BLADE swing with their items.json numbers; the rest convert these.</summary>
        static void Melee(PlayerController p, string word, Color color, int uses, float cooldown, float reach,
                          float knockback, float damage)
        {
            var w = MakeWeapon(p, word, color, 1, 0.17f);
            w.uses = uses;
            w.cooldown = cooldown;
            w.reach = reach;
            w.radius = 0.7f + reach * 0.15f;
            w.knockback = knockback;
            w.damage = damage;
        }

        static void Ranged(PlayerController p, string word, Color color, int uses, float speed, float knockback,
                           float damage, float blast)
        {
            var w = MakeWeapon(p, word, color, word.Length > 3 ? 3 : 0, 0.2f);
            w.ranged = true;
            w.uses = uses;
            w.cooldown = blast > 0f ? 0.9f : 0.45f;
            w.projectileSpeed = speed;
            w.knockback = knockback;
            w.damage = damage;
            w.blastRadius = blast;
        }

        // ---- Defence ----

        static void Wall(PlayerController p)
        {
            var built = LetterBuilt.Spawn("WALL", new Vector3(0.75f, 1.4f, 0.35f), 0, new Color(0.86f, 0.62f, 0.45f), World.Transient);
            built.transform.SetPositionAndRotation(p.transform.position + p.Facing * 1.6f, Quaternion.LookRotation(p.Facing));
            var rb = built.gameObject.AddComponent<Rigidbody>();
            rb.mass = 30f;
            built.gameObject.AddComponent<Smashable>().Init("WALL", 45f);
            SummonedThing.Attach(built.gameObject, "WALL", null, 20f);
        }

        /// <summary>A letter-built decoration that rides along on the player.</summary>
        static SummonedThing Wearable(PlayerController p, string word, Vector3 localPos, Vector3 block, int perRow,
                                      Color color, float duration, bool onVisual = true)
        {
            var built = LetterBuilt.Spawn(word, block, perRow, color, onVisual ? p.visual : p.transform, colliders: false);
            built.transform.localPosition = localPos;
            built.transform.localRotation = Quaternion.identity;
            return SummonedThing.Attach(built.gameObject, word, p, duration);
        }

        /// <summary>Blocks hits from the front for a while.</summary>
        static void Shield(PlayerController p, string word, float seconds)
        {
            p.Health.FrontBlockUntil = Time.time + seconds;
            var thing = Wearable(p, word, new Vector3(0f, 0.3f, 0.6f), new Vector3(0.24f, 0.24f, 0.08f), 3, Steel, seconds);
            thing.Ended = () => p.Health.FrontBlockUntil = 0f;
        }

        /// <summary>
        /// A PLATE in the hand. Hold block to raise it: it stops hits from the front (items.json's arc)
        /// and slows you while up, and blocked damage wears it down until it cracks apart.
        /// </summary>
        static void Plate(PlayerController p, string word)
        {
            var w = MakeWeapon(p, word, Steel, 3, 0.2f);
            w.uses = int.MaxValue; // attacking with it in hand is a punch, so it only wears by blocking
        }

        /// <summary>A bubble that soaks the next 35 damage, like FOAM. It lasts 30 s or until used up.</summary>
        static void Armor(PlayerController p, string word)
        {
            const float seconds = 30f;
            p.Health.GiveBubble(35f, seconds);
            var thing = Wearable(p, word, new Vector3(0f, 1.45f, 0f), Vector3.one * 0.16f, 0, Steel, seconds);
            thing.KeepAlive = () => p.Health.Bubble > 0f;
            var t = thing.transform;
            thing.Tick = () => t.localRotation = Quaternion.Euler(0f, Time.time * 180f, 0f);
        }

        // ---- Movement ----

        static void Wings(PlayerController p)
        {
            p.Launch(p.Facing * 8f + Vector3.up * 8f);
            p.MakeFloaty(1.4f);
            var thing = Wearable(p, "WINGS", new Vector3(0f, 0.8f, -0.5f), new Vector3(0.22f, 0.3f, 0.08f), 0, Sky, 3f);
            float start = Time.time;
            thing.KeepAlive = () => Time.time - start < 0.4f || !p.Grounded;
            var t = thing.transform;
            thing.Tick = () => t.localScale = new Vector3(1f + Mathf.Sin(Time.time * 25f) * 0.25f, 1f, 1f);
        }

        static void Spring(PlayerController p)
        {
            p.Launch(World.Flat(p.Body.linearVelocity) + Vector3.up * 13f);
            var thing = Wearable(p, "SPRING", new Vector3(0f, -0.2f, 0f), Vector3.one * 0.18f, 1, new Color(0.7f, 0.85f, 0.5f), 4f, onVisual: false);
            float start = Time.time;
            thing.KeepAlive = () => Time.time - start < 0.4f || !p.Grounded;
            // Landing slams everyone nearby.
            thing.Ended = () => { if (!p.IsKnockedOut) HitAround(p, 3f, 9f, 10f); };
        }

        static void Skates(PlayerController p) => Skates(p, "SKATES");

        static void Skates(PlayerController p, string word)
        {
            p.Boost(1.7f, 8f);
            Wearable(p, word, new Vector3(0f, 0.05f, 0f), Vector3.one * 0.14f, 0, new Color(0.95f, 0.45f, 0.55f), 8f);
        }

        static void Rope(PlayerController p)
        {
            var target = World.NearestOpponent(p, p.transform.position, 10f);
            if (target)
            {
                var pull = World.Flat(p.transform.position - target.transform.position);
                target.Knock(pull.normalized * Mathf.Min(16f, pull.magnitude * 2.2f) + Vector3.up * 3f, 0.35f);
                Popup.Show("YOINK", target.OverheadPosition, Color.white, 3f);
            }
            else
            {
                p.Knock(p.Facing * 14f, 0.2f);
            }
            var mid = target ? (p.transform.position + target.transform.position) * 0.5f : p.transform.position + p.Facing * 2f;
            TilePool.Instance?.Burst("ROPE", mid + Vector3.up, 3f);
        }

        // ---- Chaos ----

        static void Flood(PlayerController p)
        {
            foreach (var other in World.Players)
                if (other != p) other.MakeSlippery(6f);

            var go = new GameObject("FLOOD");
            go.transform.SetParent(World.Transient, false);
            go.transform.position = new Vector3(0f, 0.02f, 0f);
            var water = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(water.GetComponent<Collider>());
            water.transform.SetParent(go.transform, false);
            water.transform.localScale = new Vector3(15f, 0.02f, 10f);
            water.GetComponent<Renderer>().sharedMaterial = GameAssets.I.Tinted(new Color(0.45f, 0.7f, 0.95f));
            SummonedThing.Attach(go, "FLOOD", null, 6f);
        }

        static void Magnet(PlayerController p)
        {
            var thing = Wearable(p, "MAGNET", new Vector3(0f, 1.5f, 0f), Vector3.one * 0.16f, 3, new Color(0.9f, 0.3f, 0.3f), 6f);
            thing.Tick = () =>
            {
                var pool = TilePool.Instance;
                if (!pool) return;
                var centre = p.transform.position + Vector3.up * 0.5f;
                foreach (var tile in pool.Active)
                {
                    var d = centre - tile.transform.position;
                    if (d.sqrMagnitude < 81f) tile.Body.AddForce(d.normalized * 25f, ForceMode.Acceleration);
                }
            };
        }

        static void Quake(PlayerController p)
        {
            CameraRig.Shake(0.6f);
            foreach (var other in World.Players.ToArray())
                if (other != p && other.Grounded)
                    other.Health.ApplyDamage(Hits.Of(p, other.transform.position - p.transform.position, HitSource.Explosion, 10f, 4f, 0.3f));
            foreach (var s in Object.FindObjectsByType<Smashable>()) s.TakeHit(Smashable.HealthPerBreakPower);
            TilePool.Instance?.Burst("QUAKE", p.OverheadPosition, 4f);
        }

        static void Zap(PlayerController p)
        {
            foreach (var other in World.Players.ToArray())
            {
                if (other == p || Vector3.Distance(other.transform.position, p.transform.position) > 5.5f) continue;
                // The stun follows the hit-stun rules, so ZAP can't lock anyone down.
                if (other.Health.ApplyDamage(Hits.Of(p, other.transform.position - p.transform.position, HitSource.Explosion, 8f, 2f, 0.35f)))
                    Popup.Show("ZAP", other.OverheadPosition, new Color(1f, 0.95f, 0.4f), 3f);
            }
            CameraRig.Shake(0.2f);
            TilePool.Instance?.Burst("ZAP", p.OverheadPosition, 4f);
        }

        static void Shockwave(PlayerController p, string word, float radius, float knockback)
        {
            HitAround(p, radius, knockback, 8f);
            TilePool.Instance?.Burst(word, p.OverheadPosition, 4f);
        }

        /// <param name="knockback">Push in m/s, like the other summon numbers.</param>
        static void HitAround(PlayerController p, float radius, float knockback, float damage)
        {
            CameraRig.Shake(0.25f);
            foreach (var other in World.Players.ToArray())
                if (other != p && Vector3.Distance(other.transform.position, p.transform.position) < radius)
                    other.Health.ApplyDamage(Hits.Of(p, other.transform.position - p.transform.position, HitSource.Explosion,
                                                      damage, knockback / Hits.KnockbackSpeed, 0.3f));
        }
    }
}
