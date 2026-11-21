using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class ShoppingListDirector : HouseRoundDirector, IHouseWordGoal, IShoppingListSource
    {
        public const string Unit = " items";

        readonly Dictionary<PlayerController, (Action<PlayerHealth, HitInfo, HitResult> hit, Action<string> spelled)> hooks = new();
        readonly Dictionary<PlayerController, HeldWeapon> watched = new();
        readonly Dictionary<int, int> wins = new();
        Func<PlayerController, int> score;
        ShoppingListHud view;
        bool between;
        float betweenAt;
        int matchSeed;

        public static ShoppingListDirector Active { get; private set; }
        public ShoppingRace Race { get; private set; }
        public int RoundNumber => Round;
        public int ItemCount { get; set; }
        public int Copies { get; set; }
        public int RoundsToWin { get; set; }
        public int? Seed { get; set; }
        public HouseRoundResult LastResult { get; private set; }
        public ShoppingListHud View => view;
        public bool BetweenRounds => between;
        public bool ShowsList => Race != null && !between && (Current == Stage.Countdown || Current == Stage.Playing);
        IReadOnlyList<PlayerController> IShoppingListSource.Players => Players;
        bool Live => Race != null && !between && Current == Stage.Playing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Enlist() => HouseModeDirectors.Register(HouseModes.ShoppingList, c => c.Host.AddComponent<ShoppingListDirector>().Configure(c));

        public int WinsOf(PlayerController p) => p && wins.TryGetValue(p.Index, out int n) ? n : 0;

        public string WantedWord(PlayerController player) => player && Live ? Race.Wanted(player.Index) : null;

        protected override void Configured()
        {
            Active = this;
            ItemCount = Mathf.Max(1, Context.Rules.Setting("items", ShoppingList.Length));
            Copies = Mathf.Max(1, Context.Rules.Setting("copies", ShoppingList.Copies));
            RoundsToWin = Mathf.Max(1, Context.Rules.RoundsToWin);
            score = p => Race != null && p ? Race.Done(p.Index) : 0;
            Context.WordGoal = this;
            view = ShoppingListHud.Attach(Hud, this);
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
            foreach (var entry in hooks)
            {
                if (!entry.Key) continue;
                if (entry.Key.Health) entry.Key.Health.Damaged -= entry.Value.hit;
                if (entry.Key.Summoner) entry.Key.Summoner.Summoned -= entry.Value.spelled;
            }
            hooks.Clear();
            if (Context != null && Context.Rounds && ReferenceEquals(Context.WordGoal, this)) Context.WordGoal = null;
            if (Context != null && Context.Hud) Context.Hud.SetTarget(null);
            if (view) Destroy(view);
        }

        protected override void OnMatchStart()
        {
            between = false;
            if (Round == 1)
            {
                wins.Clear();
                matchSeed = Seed ?? UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            }
            var list = ShoppingList.Draw(GameConfig.Current.Items.Enabled, ShoppingList.Supply(Room.Layout), Context.Rules.MaxLetters,
                ShoppingList.RoundSeed(matchSeed, RoundNumber), ItemCount, Copies);
            Race = new ShoppingRace(list);
            watched.Clear();
            foreach (var p in Players)
            {
                if (!p) continue;
                Race.Join(p.Index);
                Hook(p);
            }
        }

        protected override void PlaceAtStart(PlayerController p)
        {
            p.Inventory.Set("");
            base.PlaceAtStart(p);
        }

        protected override void OnPlay()
        {
            Joins.RespawnKnockedOut = true;
            if (RoundNumber == 1) Hud.Toast("Spell each item on your list, then use it to tick it off.");
        }

        protected override string TimerText => between ? "" : base.TimerText;

        protected override void Scoreboard()
        {
            Hud.SetScoreboard(Players, score, Race?.Items.Count ?? 0, Race != null, Unit);
            Hud.SetTarget(ShowsList ? TargetWord(Hud.LocalPlayer) : null);
        }

        public string TargetWord(PlayerController viewer)
        {
            if (Race == null) return null;
            string wanted = viewer ? Race.Wanted(viewer.Index) : null;
            for (int i = 0; wanted == null && i < Race.Seats.Count; i++) wanted = Race.Wanted(Race.Seats[i]);
            return wanted;
        }

        protected override void Tick()
        {
            if (between)
            {
                if (Hud.ResultShown && Time.time - betweenAt > 1.6f && TakesStart && Joins.AnyStartPressed()) Hud.ConfirmResult();
                return;
            }
            foreach (var p in Players) if (p) Watch(p);
            if (Race.Winner != null) EndRound(Race.Winner, true);
            else if (TimeLimit > 0f && TimeLeft <= 0f) EndRound(Race.Leader(), false);
        }

        bool TakesStart => !Hud.StateController ||
            (Hud.StateController.CurrentState == UIState.GameplayHUD && !Hud.StateController.IsTransitioning);

        void Hook(PlayerController p)
        {
            if (hooks.ContainsKey(p)) return;
            var who = p;
            Action<PlayerHealth, HitInfo, HitResult> hit = Landed;
            Action<string> spelled = word => Spelled(who, word);
            p.Health.Damaged += hit;
            p.Summoner.Summoned += spelled;
            hooks[p] = (hit, spelled);
        }

        void Landed(PlayerHealth victim, HitInfo info, HitResult result)
        {
            if (!Live || !result.Landed || string.IsNullOrEmpty(info.ItemId)) return;
            var attacker = Hits.Attacker(info);
            if (attacker && attacker.Health != victim) Tally(attacker, info.ItemId, ShoppingAct.Hit);
        }

        void Spelled(PlayerController p, string word)
        {
            if (!Live || !p || !Race.Spelled(p.Index, word) || Race.Wanted(p.Index) != word) return;
            var gear = Holding(p, word);
            if (gear) watched[p] = gear;
        }

        void Watch(PlayerController p)
        {
            var item = Race.Current(p.Index);
            if (item == null || item.Value.Kind == ShoppingKind.Weapon || !Race.CurrentSpelled(p.Index)) { watched.Remove(p); return; }
            string word = item.Value.Word;
            if (watched.TryGetValue(p, out var gear) && !ReferenceEquals(gear, null))
            {
                if (gear.IsSpent) { watched.Remove(p); Tally(p, word, item.Value.Act); return; }
                if (gear && gear.TryGetComponent(out DeployedGear placed) && placed.Owner == p) { watched.Remove(p); Tally(p, word, ShoppingAct.Place); return; }
                if (gear && gear.IsHeld) return;
                watched.Remove(p);
            }
            var held = Holding(p, word);
            if (held) watched[p] = held;
        }

        static HeldWeapon Holding(PlayerController p, string word)
        {
            for (int slot = 0; slot < 2; slot++)
            {
                var gear = p.Combat.GearIn(slot);
                if (gear && gear.word == word) return gear;
            }
            return null;
        }

        void Tally(PlayerController p, string word, ShoppingAct act)
        {
            if (!Race.Count(p.Index, word, act, Elapsed)) return;
            int done = Race.Done(p.Index), total = Race.Items.Count;
            Popup.Show($"{word} DONE · {ShoppingList.Progress(done, total)}", p.OverheadPosition + Vector3.up * .35f, p.Color, 3.5f);
            if (done == total - 1) Hud.Toast(p == Hud.LocalPlayer && Joins.HumanCount == 1 ? "One item left!" : $"{p.Name} has one item left!");
        }

        void EndRound(int? seat, bool listDone)
        {
            var winner = seat != null ? World.PlayerById(seat.Value) : null;
            if (winner) wins[winner.Index] = WinsOf(winner) + 1;
            bool final = winner && WinsOf(winner) >= RoundsToWin;
            LastResult = Result(winner, listDone, final);
            Joins.RespawnKnockedOut = false;
            watched.Clear();
            if (final)
            {
                Finish(LastResult);
                return;
            }
            between = true;
            betweenAt = Time.time;
            Context.Phase = Phase.RoundOver;
            Deliveries.Running = false;
            foreach (var p in Players) if (p) p.Frozen = true;
            World.FreezeTransient();
            if (winner) winner.GetComponent<PlayerAppearance>()?.Play("Celebrate", 1.5f);
            var you = Hud.LocalPlayer;
            var card = HudResult.Of(RoundNumber, false, winner && you && winner == you, LastResult.Heading, null);
            card.Eyebrow = LastResult.Eyebrow;
            card.Flavour = LastResult.Flavour;
            card.Lines = LastResult.Lines;
            card.Winner = LastResult.WinnerName;
            card.Draw = LastResult.Draw;
            Hud.ShowResultSoon(card, NextRound);
        }

        void NextRound()
        {
            between = false;
            StartRound();
            Hud.ShowBanner(HouseGoals.BannerTitle(RoundNumber, HouseModes.Name(HouseModes.ShoppingList)),
                HouseGoals.BannerLine(HouseModes.ShoppingList, RoundsToWin));
        }

        HouseRoundResult Result(PlayerController winner, bool listDone, bool final)
        {
            var you = Hud.LocalPlayer;
            int humans = Joins.HumanCount, total = Race.Items.Count;
            string Tag(PlayerController p) => SeatTags.Name(p.Name, p == you, humans);
            string name = winner ? Tag(winner) : "";
            var lines = Race.Standings().Select(World.PlayerById).Where(p => p)
                .Select(p => ShoppingList.ResultLine(Tag(p), Race.Done(p.Index), total, WinsOf(p), RoundsToWin, p == winner)).ToArray();
            return new HouseRoundResult
            {
                Winner = winner,
                WinnerName = name,
                Draw = !winner,
                Heading = ShoppingList.Heading(name, listDone),
                Eyebrow = ShoppingList.Eyebrow(RoundNumber, final, name),
                Flavour = ShoppingList.Flavour(name, final, RoundsToWin),
                Lines = lines,
            };
        }
    }
}
