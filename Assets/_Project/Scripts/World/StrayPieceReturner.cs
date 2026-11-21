using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    [DisallowMultipleComponent]
    public sealed class StrayPieceReturner : MonoBehaviour
    {
        sealed class Piece
        {
            public Smashable Smash;
            public Rigidbody Body;
            public Transform Parent;
            public Collider[] Shapes;
            public Vector3 Home;
            public Quaternion Facing;
            public Bounds Shape;
            public RoomBox Room;
            public float Rise;
            public StrayWatch Watch;
        }

        const int OverlapBudget = 32;
        const float PuffSize = .5f, RingSize = .6f, RingLift = .05f, FallbackSize = .4f;
        const string Puff = "Foam_Cloud", Ring = "Craft_Ring";
        static readonly Collider[] overlaps = new Collider[OverlapBudget];

        readonly List<Piece> pieces = new();
        RoomBuilder room;
        Smashable firstKnown;
        int knownCount = -1;
        float nextCheck, lastCheck = -1f;

        public float ReturnAfter { get; set; } = StrayPieces.ReturnAfterSeconds;
        public int Tracked => pieces.Count;
        public int Returned { get; private set; }
        public event Action<Smashable> PieceReturned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void WatchScenes()
        {
            SceneManager.sceneLoaded -= Attach;
            SceneManager.sceneLoaded += Attach;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToOpenScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++) Attach(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
        }

        static void Attach(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isPlaying || !scene.IsValid() || !scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var builder in root.GetComponentsInChildren<RoomBuilder>(true))
                    if (!builder.TryGetComponent(out StrayPieceReturner _)) builder.gameObject.AddComponent<StrayPieceReturner>();
        }

        void Awake() => room = GetComponent<RoomBuilder>();

        void Update()
        {
            if (!room || Time.timeScale <= 0f) return;
            if (FurnitureChanged()) Register();
            if (Time.time < nextCheck) return;
            float elapsed = lastCheck < 0f ? 0f : Time.time - lastCheck;
            lastCheck = Time.time;
            nextCheck = Time.time + StrayPieces.CheckSeconds;
            for (int i = pieces.Count - 1; i >= 0; i--)
                if (!Check(pieces[i], elapsed)) pieces.RemoveAt(i);
        }

        bool FurnitureChanged()
        {
            var originals = room.Originals;
            var first = originals.Count > 0 ? originals[0] : null;
            return originals.Count != knownCount || !ReferenceEquals(first, firstKnown);
        }

        void Register()
        {
            var originals = room.Originals;
            knownCount = originals.Count;
            firstKnown = knownCount > 0 ? originals[0] : null;
            pieces.Clear();
            if (knownCount == 0) return;
            Physics.SyncTransforms();
            var layout = room.Layout;
            foreach (var smash in originals)
            {
                if (!smash || smash.IsBroken || !smash.TryGetComponent(out Rigidbody body)) continue;
                var piece = new Piece
                {
                    Smash = smash,
                    Body = body,
                    Parent = smash.transform.parent,
                    Shapes = smash.GetComponentsInChildren<Collider>(true),
                    Home = smash.transform.position,
                    Facing = smash.transform.rotation,
                };
                piece.Shape = Outline(piece);
                float middle = piece.Shape.center.y;
                piece.Room = StrayPieces.FloorUnder(layout, piece.Home.x, middle, piece.Home.z);
                piece.Rise = StrayPieces.Rise(layout, piece.Home.x, middle, piece.Home.z, piece.Shape.min.y);
                pieces.Add(piece);
            }
        }

        static bool Solid(Collider shape) => shape && shape.enabled && !shape.isTrigger && shape.gameObject.activeInHierarchy;

        static Bounds Outline(Piece piece)
        {
            bool any = false;
            var outline = new Bounds(piece.Home, Vector3.one * FallbackSize);
            foreach (var shape in piece.Shapes)
            {
                if (!Solid(shape)) continue;
                if (any) outline.Encapsulate(shape.bounds);
                else outline = shape.bounds;
                any = true;
            }
            return outline;
        }

        static float Bottom(Piece piece, float fallback)
        {
            bool any = false;
            float bottom = fallback;
            foreach (var shape in piece.Shapes)
            {
                if (!Solid(shape)) continue;
                float low = shape.bounds.min.y;
                if (!any || low < bottom) bottom = low;
                any = true;
            }
            return bottom;
        }

        bool Check(Piece piece, float elapsed)
        {
            var smash = piece.Smash;
            if (!smash || smash.IsBroken || !piece.Body) return false;
            if (!smash.isActiveAndEnabled)
            {
                piece.Watch = default;
                return true;
            }
            var offset = smash.transform.position - piece.Home;
            if (StrayPieces.AtHome(offset.x, offset.y, offset.z))
            {
                piece.Watch = default;
                return true;
            }
            var hold = HoldOf(piece);
            var place = hold != StrayHold.None ? StrayPlace.InPlace : Locate(piece);
            if (StrayPieces.Tick(ref piece.Watch, place, hold, piece.Body.linearVelocity.magnitude, elapsed, ReturnAfter)) Return(piece);
            return true;
        }

        StrayPlace Locate(Piece piece)
        {
            var layout = room.Layout;
            var at = piece.Body.worldCenterOfMass;
            var place = StrayPieces.Locate(layout, piece.Rise, at.x, at.y, at.z, at.y);
            return place == StrayPlace.High ? StrayPieces.Locate(layout, piece.Rise, at.x, at.y, at.z, Bottom(piece, at.y)) : place;
        }

        static StrayHold HoldOf(Piece piece)
        {
            var hold = StrayHold.None;
            var smash = piece.Smash;
            if (piece.Body.isKinematic) hold |= StrayHold.Frozen;
            if (smash.Invulnerable || smash.TryGetComponent(out Keepsake _)) hold |= StrayHold.Goal;
            if (smash.TryGetComponent(out ThrowTracker _)) hold |= StrayHold.Thrown;
            if (smash.transform.parent != piece.Parent) hold |= StrayHold.Carried;
            foreach (var player in World.Players)
                if (player && player.Combat && player.Combat.Held == piece.Body) hold |= StrayHold.Carried;
            return hold;
        }

        void Return(Piece piece)
        {
            if (!TryFreeSpot(piece, out var spot)) return;
            var body = piece.Body;
            var target = spot + Vector3.up * StrayPieces.DropHeight;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            piece.Smash.transform.SetPositionAndRotation(target, piece.Facing);
            body.position = target;
            body.rotation = piece.Facing;
            piece.Watch = default;
            Returned++;
            var moved = spot - piece.Home;
            var middle = piece.Shape.center + moved;
            GameFeedback.Burst(Puff, middle, PuffSize);
            GameFeedback.Burst(Ring, new Vector3(middle.x, piece.Shape.min.y + moved.y + RingLift, middle.z), RingSize);
            PieceReturned?.Invoke(piece.Smash);
        }

        bool TryFreeSpot(Piece piece, out Vector3 spot)
        {
            float radius = Mathf.Max(piece.Shape.extents.x, piece.Shape.extents.z);
            for (int i = 0; i < StrayPieces.SpotCount; i++)
            {
                if (!StrayPieces.TrySpot(piece.Room, piece.Home.x, piece.Home.z, radius, i, out float x, out float z)) continue;
                spot = new Vector3(x, piece.Home.y, z);
                if (Free(piece, spot, i == 0)) return true;
            }
            spot = default;
            return false;
        }

        static bool Free(Piece piece, Vector3 spot, bool home)
        {
            var shape = piece.Shape;
            var moved = spot - piece.Home;
            float bottom = shape.min.y + moved.y + StrayPieces.FloorClearance;
            float top = Mathf.Max(shape.max.y + moved.y, bottom + StrayPieces.MinProbeHeight);
            var centre = new Vector3(shape.center.x + moved.x, (bottom + top) * .5f, shape.center.z + moved.z);
            var half = new Vector3(shape.extents.x * StrayPieces.SpotFit, (top - bottom) * .5f, shape.extents.z * StrayPieces.SpotFit);
            int mask = World.TileLayer >= 0 ? ~(1 << World.TileLayer) : ~0;
            int count = Physics.OverlapBoxNonAlloc(centre, half, overlaps, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
            var found = overlaps;
            if (count == found.Length)
            {
                found = Physics.OverlapBox(centre, half, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
                count = found.Length;
            }
            for (int i = 0; i < count; i++)
            {
                var owner = found[i] ? found[i].attachedRigidbody : null;
                if (owner == piece.Body) continue;
                if (home && !owner) continue;
                return false;
            }
            return true;
        }
    }
}
