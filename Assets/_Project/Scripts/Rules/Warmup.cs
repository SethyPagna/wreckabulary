using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public sealed class Warmup
    {
        public const float DefaultSeconds = 30f, ReadyHoldSeconds = 1f, RespawnSeconds = 1f, CardSeconds = 5f;
        public const string Title = "WARM-UP", StartWord = "Start", ReadyWord = "READY";
        public const string Tip = "Spell anything, no letters needed. Nothing counts yet.";
        public const string TypeAnything = "Warm-up: type any recipe, no letters needed. Tab or right-click closes.";

        static readonly string[] ModeIds =
        {
            HouseModes.Dibs, HouseModes.Duos, HouseModes.FreeForAll, HouseModes.War, HouseModes.ShoppingList, HouseModes.RiddleRush,
        };

        sealed class Seat
        {
            public bool Votes, Ready;
            public float Held, Down;
        }

        readonly Dictionary<int, Seat> seats = new Dictionary<int, Seat>();
        float length, elapsed;

        public Warmup(float seconds = DefaultSeconds) => Length = seconds;

        public static IReadOnlyList<string> Modes => ModeIds;

        public static bool Applies(string modeId) => modeId != null && Array.IndexOf(ModeIds, modeId) >= 0;

        public float Length { get => length; set => length = Math.Max(0f, value); }
        public float Elapsed => elapsed;
        public float TimeLeft => Math.Max(0f, length - elapsed);
        public int Voters { get; private set; }
        public int ReadyCount { get; private set; }
        public bool AllReady => Voters > 0 && ReadyCount == Voters;
        public bool Ends => TimeLeft <= 0f || AllReady;
        public string Clock => FormatClock(TimeLeft);

        public void Join(int seat, bool votes)
        {
            Leave(seat);
            seats[seat] = new Seat { Votes = votes };
            if (votes) Voters++;
        }

        public void Leave(int seat)
        {
            if (!seats.TryGetValue(seat, out var left)) return;
            seats.Remove(seat);
            if (left.Votes) Voters--;
            if (left.Ready) ReadyCount--;
        }

        public bool IsReady(int seat) => seats.TryGetValue(seat, out var found) && found.Ready;

        public float HoldProgress(int seat) =>
            !seats.TryGetValue(seat, out var found) || !found.Votes ? 0f : found.Ready ? 1f : Math.Min(1f, found.Held / ReadyHoldSeconds);

        public void Tick(float seconds) => elapsed += Math.Max(0f, seconds);

        public bool Hold(int seat, bool held, float seconds)
        {
            if (!seats.TryGetValue(seat, out var found) || !found.Votes) return false;
            if (found.Ready) return true;
            if (!held) { found.Held = 0f; return false; }
            found.Held += Math.Max(0f, seconds);
            if (found.Held < ReadyHoldSeconds) return false;
            found.Ready = true;
            ReadyCount++;
            return true;
        }

        public bool Down(int seat, bool down, float seconds)
        {
            if (!seats.TryGetValue(seat, out var found)) return false;
            if (!down) { found.Down = 0f; return false; }
            found.Down += Math.Max(0f, seconds);
            if (found.Down < RespawnSeconds) return false;
            found.Down = 0f;
            return true;
        }

        public int Card(int count) => count <= 0 ? 0 : (int)(elapsed / CardSeconds) % count;

        public string ReadyLine(string startKey = null) =>
            Voters == 0 ? "" : ReadyCount == 0 ? ReadyPrompt(startKey) : $"{ReadyCount} of {Voters} ready";

        public static string ReadyPrompt(string startKey = null) =>
            $"Hold {(string.IsNullOrEmpty(startKey) ? StartWord : startKey)} when ready";

        public static string FormatClock(float seconds)
        {
            int whole = Math.Max(0, (int)Math.Ceiling(seconds));
            return $"{whole / 60}:{whole % 60:00}";
        }
    }
}
