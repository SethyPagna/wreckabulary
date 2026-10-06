using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>
    /// Moving day: when the room runs low on letters, labelled boxes drop in. Smash one and it
    /// bursts into the letters on its label. During the collapse, boxes rain down and hurt.
    /// </summary>
    public class DeliverySpawner : MonoBehaviour
    {
        [SerializeField] string[] words =
        {
            "BOX", "WAX", "PIZZA", "QUILT", "SOCKS", "SPOONS", "KETTLE", "TOWELS", "CANDLE", "DISHES",
            "BRUSH", "GAMES", "SHOES", "JUICE", "JAM", "FORKS", "COMICS", "ZIPPER", "GLOVES", "WIGS",
        };
        [SerializeField] Vector2 areaX = new(-6.5f, 6.5f);
        [SerializeField] Vector2 areaZ = new(-4.2f, 3.2f);
        [SerializeField] float dropHeight = 6f;
        /// <summary>How far a box lands from a flight of stairs: half a turned long box (about .73 m) and the rail beside it.</summary>
        const float StairClearance = .85f;
        [Tooltip("Boxes only arrive while fewer letters than this are in play.")]
        [SerializeField] int minLettersInPlay = 26;
        [SerializeField] float interval = 4f;
        [SerializeField] float collapseInterval = 1.1f;

        public bool Running { get; set; } = true;
        public bool Collapsing { get; private set; }

        float next;

        public void ResetDrops()
        {
            Collapsing = false;
            next = Time.time + interval;
        }

        public void StartCollapse() => Collapsing = true;

        void Update()
        {
            if (!Running || Time.time < next) return;
            next = Time.time + (Collapsing ? collapseInterval : interval);
            if (!Collapsing && LettersInPlay() >= minLettersInPlay) return;
            Drop(Collapsing);
        }

        /// <summary>The house being played. With an upstairs, boxes drop into its rooms instead of all landing on the top floor.</summary>
        public HouseLayout Layout { get; set; }
        /// <summary>Rooms boxes may drop into (in a house with an upstairs); null means any.</summary>
        public System.Func<string, bool> RoomOpen { get; set; }

        public Smashable Drop(bool hazard)
        {
            var word = words[Random.Range(0, words.Length)];
            var box = CreateBox(word, DropPoint());
            if (hazard) ThrowTracker.Attach(box.gameObject, null, 4f, 3.5f);
            return box;
        }

        Vector3 DropPoint()
        {
            var floors = Layout?.StoreyFloors();
            if (floors == null || floors.Count < 2)
                return new Vector3(Random.Range(areaX.x, areaX.y), dropHeight, Random.Range(areaZ.x, areaZ.y));
            // A big room is likelier than a small one; a metre in from the walls and clear of the stairs, under the floor above.
            var rooms = Layout.Rooms.Where(r => r.MaxX - r.MinX > 2.5f && r.MaxZ - r.MinZ > 2.5f && (RoomOpen == null || RoomOpen(r.Name))).ToList();
            if (rooms.Count == 0) rooms = Layout.Rooms.Where(r => r.MaxX - r.MinX > 2.5f && r.MaxZ - r.MinZ > 2.5f).ToList();
            if (rooms.Count == 0) rooms = Layout.Rooms.ToList();
            float total = rooms.Sum(r => (r.MaxX - r.MinX) * (r.MaxZ - r.MinZ));
            RoomBox room = rooms[0];
            float x = 0f, z = 0f;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float pick = Random.Range(0f, total);
                room = rooms.FirstOrDefault(r => (pick -= (r.MaxX - r.MinX) * (r.MaxZ - r.MinZ)) <= 0f) ?? rooms[rooms.Count - 1];
                float inset = Mathf.Min(1f, (room.MaxX - room.MinX) * .4f), insetZ = Mathf.Min(1f, (room.MaxZ - room.MinZ) * .4f);
                x = Random.Range(room.MinX + inset, room.MaxX - inset);
                z = Random.Range(room.MinZ + insetZ, room.MaxZ - insetZ);
                if (!Layout.Stairs.Any(s => x > s.MinX - StairClearance && x < s.MaxX + StairClearance && z > s.MinZ - StairClearance && z < s.MaxZ + StairClearance)) break;
                x = (room.MinX + room.MaxX) * .5f; z = (room.MinZ + room.MaxZ) * .5f;
            }
            int storey = Layout.StoreyOf(room);
            float ceiling = storey + 1 < floors.Count ? floors[storey + 1] - .24f : float.PositiveInfinity;
            return new Vector3(x, Mathf.Min(room.FloorY + dropHeight, ceiling - 1f), z);
        }

        public static Smashable CreateBox(string word, Vector3 position)
        {
            var go = new GameObject($"Box ({word})");
            go.transform.SetParent(World.Transient, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f));

            var size = new Vector3(0.95f, 0.7f, 0.95f) * (word.Length > 5 ? 1.15f : 1f);
            var block = LetterBlocks.Create(word, size, GameAssets.I.cardboard, go.transform, true);
            block.transform.localPosition = Vector3.up * size.y * 0.5f;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var smash = go.AddComponent<Smashable>();
            smash.Init(word, 8f);
            return smash;
        }

        static int LettersInPlay()
        {
            int n = TilePool.Instance ? TilePool.Instance.Active.Count : 0;
            foreach (var s in FindObjectsByType<Smashable>()) n += s.Word.Length;
            foreach (var p in World.Players) n += p.Inventory.Count;
            return n;
        }
    }
}
