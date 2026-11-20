using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Art;

namespace Wreckabulary
{
    [DisallowMultipleComponent]
    public sealed class LobbyWreck : MonoBehaviour
    {
        public const float FirstDelay = 2.5f, Interval = 8.5f, TileSize = .2f;
        const float Shake = .32f, Fly = 1.1f, Rest = 1.5f, Gather = .55f, Pop = .35f;
        const float Gravity = 9.8f;

        enum Phase { Waiting, Shaking, Flying, Resting, Gathering, Popping }

        sealed class Piece
        {
            public Transform root;
            public Vector3 velocity, spin, home, landed;
            public Quaternion landedRotation;
            public bool down;
        }

        LobbyRoom room;
        LobbyStage stage;
        readonly List<Piece> pieces = new();
        Phase phase = Phase.Waiting;
        float clock, wait = FirstDelay;
        int turn;
        Transform target;
        Vector3 targetScale;
        Quaternion targetRotation;
        public bool Enabled = true;
        public Transform Target => target;
        public bool Busy => phase != Phase.Waiting;
        public int Tiles => pieces.Count;

        public void Init(LobbyRoom room, LobbyStage stage)
        {
            this.room = room;
            this.stage = stage;
        }

        public bool Trigger()
        {
            if (Busy || !room) return false;
            var pick = Pick();
            if (!pick) return false;
            Begin(pick);
            return true;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            clock += dt;
            switch (phase)
            {
                case Phase.Waiting:
                    if (!Enabled || !stage || !stage.IsPresenting || clock < wait) return;
                    if (!Trigger()) { clock = 0f; wait = 1f; }
                    break;
                case Phase.Shaking:
                    float wobble = Mathf.Sin(clock * 55f) * 7f * (1f - clock / Shake);
                    target.localRotation = targetRotation * Quaternion.Euler(0f, wobble, wobble * .5f);
                    target.localScale = Vector3.Scale(targetScale, new Vector3(1f + clock * .25f, 1f - clock * .3f, 1f + clock * .25f));
                    if (clock >= Shake) Burst();
                    break;
                case Phase.Flying:
                    Fall(dt);
                    if (clock >= Fly) { Next(Phase.Resting); foreach (var p in pieces) { p.landed = p.root.position; p.landedRotation = p.root.rotation; } }
                    break;
                case Phase.Resting:
                    if (clock >= Rest) Next(Phase.Gathering);
                    break;
                case Phase.Gathering:
                    float t = Mathf.Clamp01(clock / Gather), ease = t * t * (3f - 2f * t);
                    foreach (var p in pieces)
                    {
                        var lift = Vector3.up * Mathf.Sin(t * Mathf.PI) * .5f;
                        p.root.position = Vector3.Lerp(p.landed, p.home, ease) + lift;
                        p.root.rotation = Quaternion.Slerp(p.landedRotation, Quaternion.LookRotation(Vector3.up), ease);
                        p.root.localScale = Vector3.one * Mathf.Lerp(1f, .25f, ease * ease);
                    }
                    if (t >= 1f) Rebuild();
                    break;
                case Phase.Popping:
                    float k = Mathf.Clamp01(clock / Pop);
                    float overshoot = 1f + Mathf.Sin(k * Mathf.PI) * .12f * (1f - k * .4f);
                    target.localScale = targetScale * Mathf.Lerp(.55f, 1f, Mathf.Sqrt(k)) * overshoot;
                    if (k >= 1f) { target.localScale = targetScale; target = null; Next(Phase.Waiting); wait = Interval + (turn % 3) * .9f; }
                    break;
            }
        }

        Transform Pick()
        {
            var cam = stage ? stage.Camera : null;
            var choices = room.Wreckable;
            for (int i = 0; i < choices.Count; i++)
            {
                var candidate = choices[(turn + i) % choices.Count];
                if (!candidate || !candidate.gameObject.activeInHierarchy) continue;
                if (cam)
                {
                    var view = cam.WorldToViewportPoint(candidate.position + Vector3.up * .3f);
                    if (view.z <= 0f || view.x < .05f || view.x > stage.VisibleRight - .05f || view.y < .05f || view.y > .85f) continue;
                }
                turn += i + 1;
                return candidate;
            }
            turn++;
            return null;
        }

        void Begin(Transform pick)
        {
            target = pick;
            targetScale = pick.localScale;
            targetRotation = pick.localRotation;
            Next(Phase.Shaking);
        }

        void Burst()
        {
            target.localRotation = targetRotation;
            target.localScale = targetScale;
            var bounds = BoundsOf(target);
            target.gameObject.SetActive(false);
            string word = target.name.ToUpperInvariant();
            var cam = stage ? stage.Camera : null;
            var towardCamera = cam ? Vector3.ProjectOnPlane(cam.transform.position - bounds.center, Vector3.up).normalized : Vector3.back;
            var across = Vector3.Cross(Vector3.up, towardCamera);
            for (int i = 0; i < word.Length; i++)
            {
                var tile = SpawnTile(word[i]);
                if (!tile) continue;
                float spread = word.Length == 1 ? 0f : i / (word.Length - 1f) * 2f - 1f;
                var start = bounds.center + Vector3.up * bounds.extents.y * .3f + across * spread * Mathf.Min(bounds.extents.x, .4f);
                tile.position = start;
                tile.rotation = Random.rotationUniform;
                pieces.Add(new Piece
                {
                    root = tile,
                    home = bounds.center,
                    velocity = across * spread * 1.6f + towardCamera * (1.1f + (i % 2) * .5f) + Vector3.up * (2.9f + (i % 3) * .35f),
                    spin = new Vector3(Random.Range(-540f, 540f), Random.Range(-540f, 540f), Random.Range(-540f, 540f)),
                });
            }
            Puff(bounds.center);
            Next(Phase.Flying);
        }

        Transform SpawnTile(char letter)
        {
            string key = "Letters/Tile_" + letter;
            var library = ModelLibrary.Load();
            if (!library || !library.Find(key)) return null;
            var root = new GameObject("Wreck tile " + letter).transform;
            root.SetParent(transform, false);
            var holder = new GameObject("Tile").transform;
            holder.SetParent(root, false);
            holder.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var model = ModelVisual.Spawn(key, holder);
            if (!model) { Destroy(root.gameObject); return null; }
            var bounds = ModelVisual.BoundsIn(root, model);
            float size = Mathf.Max(.001f, bounds.size.x, bounds.size.y, bounds.size.z);
            holder.localScale = Vector3.one * (TileSize / size);
            holder.localPosition = -bounds.center * (TileSize / size);
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        void Fall(float dt)
        {
            float floor = transform.position.y + TileSize * .5f;
            foreach (var p in pieces)
            {
                if (!p.root) continue;
                p.velocity += Vector3.down * Gravity * dt;
                var position = p.root.position + p.velocity * dt;
                p.root.rotation = Quaternion.Euler(p.spin * dt) * p.root.rotation;
                if (position.y < floor)
                {
                    position.y = floor;
                    if (p.velocity.y < -1.2f) p.velocity = new Vector3(p.velocity.x * .55f, -p.velocity.y * .32f, p.velocity.z * .55f);
                    else { p.velocity = new Vector3(p.velocity.x * .8f, 0f, p.velocity.z * .8f); p.down = true; }
                    p.spin *= .45f;
                }
                if (p.down)
                {
                    p.spin = Vector3.Lerp(p.spin, Vector3.zero, dt * 8f);
                    p.root.rotation = Quaternion.Slerp(p.root.rotation, Upright(p.root.rotation), dt * 10f);
                }
                p.root.position = position;
            }
        }

        static Quaternion Upright(Quaternion rotation)
        {
            var axes = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            Vector3 best = Vector3.up; float most = -2f;
            foreach (var axis in axes)
            {
                float d = Vector3.Dot(rotation * axis, Vector3.up);
                if (d > most) { most = d; best = axis; }
            }
            return Quaternion.FromToRotation(rotation * best, Vector3.up) * rotation;
        }

        void Rebuild()
        {
            foreach (var p in pieces) if (p.root) Destroy(p.root.gameObject);
            pieces.Clear();
            target.gameObject.SetActive(true);
            target.localRotation = targetRotation;
            target.localScale = targetScale * .55f;
            Next(Phase.Popping);
        }

        void Puff(Vector3 at)
        {
            var library = ModelLibrary.Load();
            const string Key = "VFX/Impact_Star";
            if (!library || !library.Find(Key)) return;
            var root = new GameObject("Wreck puff").transform;
            root.SetParent(transform, false);
            root.position = at;
            library.Spawn(Key, root);
            Destroy(root.gameObject, 1.5f);
        }

        static Bounds BoundsOf(Transform prop)
        {
            var renderers = prop.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(prop.position + Vector3.up * .4f, Vector3.one * .8f);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        void Next(Phase next) { phase = next; clock = 0f; }

        public void Settle()
        {
            foreach (var p in pieces) if (p.root) Destroy(p.root.gameObject);
            pieces.Clear();
            if (target)
            {
                target.gameObject.SetActive(true);
                target.localRotation = targetRotation;
                target.localScale = targetScale;
                target = null;
            }
            Next(Phase.Waiting);
            wait = FirstDelay;
        }

        void OnDisable() => Settle();
    }
}
