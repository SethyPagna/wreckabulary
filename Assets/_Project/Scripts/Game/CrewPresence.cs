using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public static class CrewPresence
    {
        public static int IdleMask(IdleWatch watch, IReadOnlyList<PlayerController> players)
        {
            int mask = 0;
            double now = Time.time;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (!p) continue;
                var at = p.transform.position;
                watch.Observe(p.Index, at.x, at.y, at.z, Acting(p.Commands), Connected(p), now);
                if (!p.IsEliminated && watch.IsIdle(p.Index, now)) mask |= 1 << p.Index;
            }
            return players.Count > 1 ? mask : 0;
        }

        public static bool Waits(int idleMask, PlayerController p) => (idleMask & (1 << p.Index)) == 0;

        static bool Acting(in PlayerCommands c) =>
            c.move.sqrMagnitude > .04f || c.look.sqrMagnitude > .04f || c.lookDelta.sqrMagnitude > 1e-6f
            || c.grab || c.grabHeld || c.attack || c.attackHeld || c.aimHeld || c.jump || c.dodge || c.blockHeld || c.drop
            || c.swap || c.slot != 0 || c.spellHeld || c.spellDown || c.spellUp || c.up || c.down || c.start;

        static bool Connected(PlayerController p) => p.Binding is not GamepadBinding pad || (pad.Pad != null && pad.Pad.added);
    }
}
