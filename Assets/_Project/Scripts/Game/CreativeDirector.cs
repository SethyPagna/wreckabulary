using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// Creative, "Home Sweet Home": spell any object from endless letters (A–Z, nothing spent) and arrange
    /// your room. Nobody gets knocked out, and letters from smashed objects tidy themselves away.
    /// The room is remembered for the session, so it's still there after playing Dibs! in it.
    /// </summary>
    public class CreativeDirector : MonoBehaviour
    {
        [SerializeField] PlayerJoinManager joins;
        [SerializeField] GameHud hud;
        [SerializeField] CreativeDesk desk;
        [Tooltip("Loose letters vanish after this many seconds.")]
        [SerializeField] float tidyAfter = 3f;

        List<WordEntry> objectWords;
        float nextRemember;

        public IReadOnlyList<WordEntry> ObjectWords =>
            objectWords ??= GameAssets.I.words.Words.Where(w => w.category == WordCategory.Furniture).ToList();

        void Start()
        {
            Music.Play(Track.Cozy);
            joins.RespawnKnockedOut = true;
            joins.StarterLetters = 0;
            joins.Joined += Setup;
            foreach (var p in joins.Players) Setup(p);
            Session.CustomRoom?.Build(World.Transient);
        }

        /// <summary>Builder settings: A–Z spelling of objects only, no letters to carry, and no knockouts.</summary>
        public void Setup(PlayerController p)
        {
            p.Summoner.EndlessLetters = true;
            p.Summoner.WordsOverride = ObjectWords;
            p.Health.Harmless = true;
            p.Inventory.Collects = false;
            p.Inventory.Set("");
        }

        void Update()
        {
            TidyLetters();
            if (Time.time >= nextRemember)
            {
                nextRemember = Time.time + 1f;
                Session.CustomRoom = RoomLayout.Capture();
            }

            if (joins.Players.Count == 0)
                hud.SetInstruction("Press SPACE, . or A to join", "Build your own room, then play Dibs! in it");
            else if (desk && desk.User)
                hud.SetInstruction("Room menu", "Up/down to choose  •  left/right to change  •  grab to pick  •  spell to get up");
            else
                hud.SetInstruction("Press spell and build anything from A to Z",
                                   "Grab to move things  •  punch to remove them  •  the desk: play, save and load");
            hud.SetScoreboard(joins.Players, _ => 0, 0, false);
        }

        void TidyLetters()
        {
            var pool = TilePool.Instance;
            if (!pool) return;
            for (int i = pool.Active.Count - 1; i >= 0; i--)
                if (Time.time - pool.Active[i].LaunchedAt > tidyAfter) pool.Release(pool.Active[i]);
        }
    }
}
