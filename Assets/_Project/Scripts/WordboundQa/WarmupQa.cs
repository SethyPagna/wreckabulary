#if UNITY_EDITOR || GSK_QA
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameStudioKit.Capture;
using GameStudioKit.Qa;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Qa
{
    public sealed class WarmupQa : IQaScenario
    {
        public const string Scenario = "warmup";
        const string Map = "pinwheel", PreferredWord = "BAT", SecondSuffix = "_free_for_all", VanSuffix = "_pack_the_van", EscapeSuffix = "_escape";
        const float OpenSeconds = 60f, ModeSeconds = 240f, ReturnSeconds = 60f;
        const float LoadSeconds = 40f, EnterSeconds = 30f, StripSeconds = 15f, SettleSeconds = 2f, ActSeconds = 10f, CraftSeconds = 15f;
        const float ProtectionSeconds = 6f, ComeBackSeconds = 8f, ReadySeconds = 15f, ReadyLineSeconds = 5f, MinReadyLeft = 12f;
        const float AfterSeconds = 3f, RoundOneSeconds = 60f, HubSeconds = 30f;
        const float WheelSeconds = .4f, ShortSeconds = .3f, RoundSeconds = .6f, KnockDamage = 1000f, Slack = .01f, BoxSlack = 1f;
        const int BlankColours = 24, BlankStride = 97, CaptureFrames = 4;

        LocalSeat[] seats = new LocalSeat[0];
        PlayerController human, partner;
        PlayerJoinManager joins;
        RoundManager rounds;
        RoomBuilder room;
        GameHud hud;
        Warmup state;
        string word, career, broke, originalMap;
        int loads, pieces;
        bool opened, launched, savedSwitch, crew;
        InputBinding[] originalBindings;
        Saved[] preferences;
        readonly List<string> errors = new();

        public string Name => Scenario;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register() => QaScenarios.Register(Scenario, () => new WarmupQa());

        public IEnumerable<QaStep> BuildSteps(QaContext context)
        {
            yield return QaStep.Named("warmup_open").Timeout(OpenSeconds).When(_ => Object.FindAnyObjectByType<LobbyMenu>() != null).Do(Open);
            yield return QaStep.Named("warmup_duos").Timeout(ModeSeconds).Do(c => Play(c, HouseModes.Duos, 2, ""));
            yield return QaStep.Named("warmup_free_for_all").Timeout(ModeSeconds).Do(c => Play(c, HouseModes.FreeForAll, 1, SecondSuffix));
            yield return QaStep.Named("warmup_pack_the_van").Timeout(ModeSeconds).Do(c => PlayCrew(c, HouseModes.MovingOut, 2, VanSuffix));
            yield return QaStep.Named("warmup_escape").Timeout(ModeSeconds).Do(c => PlayCrew(c, HouseModes.Escape, 1, EscapeSuffix));
            yield return QaStep.Named("warmup_return").Timeout(ReturnSeconds).Do(Return);
        }

        void Log(string condition, string stack, LogType type)
        {
            if (type is LogType.Exception or LogType.Error or LogType.Assert) errors.Add(condition);
        }

        void Loaded(Scene scene, LoadSceneMode mode) => loads++;

        IEnumerator Open(QaContext context)
        {
            if (!Session.CanLoad(Session.DibsScene)) throw new QaFailException(Session.DibsScene + " is not in the build.");
            preferences = Saved.Capture();
            originalBindings = Session.Bindings.ToArray();
            originalMap = Session.MapId;
            savedSwitch = LobbyMenu.WarmupBeforeMatches;
            career = PlayerPrefs.GetString(MatchTally.CareerKey, "");
            Application.logMessageReceived += Log;
            SceneManager.sceneLoaded += Loaded;
            LobbyMenu.WarmupBeforeMatches = true;
            opened = true;
            context.Note("warmup_switch", new Dictionary<string, object> { ["lobby_switch_at_launch"] = savedSwitch, ["set_for_this_run"] = true });
            context.Note("warmup_screen", new Dictionary<string, object> { ["width"] = Screen.width, ["height"] = Screen.height });
            context.Note("warmup_scope", "Offline Windows house matches on Pinwheel through Session.LoadMode with scripted local seats, " +
                "started with the request the lobby sets (LobbyMenu.WarmupBeforeMatches and Warmup.Applies) because the switch is off under -qa-scenario. " +
                "Duos runs with two seats through RoundManager; Free-for-All runs with one seat through its director. The game's own AI seats are parked " +
                "so the run stays deterministic. The knockout is a direct hit, one piece of furniture is broken directly, and the warm-up clock is only " +
                "lengthened if fewer than " + MinReadyLeft + " s are left before the ready hold. Whatever follows the warm-up (a showcase or the countdown) " +
                "is captured as found and not asserted. No network or human-feel claim.");
            yield return null;
        }

        IEnumerator Play(QaContext context, string mode, int seatCount, string suffix)
        {
            if (!opened) throw new QaFailException("The warm-up scenario did not open, so " + HouseModes.Name(mode) + " was not run.");
            yield return Launch(context, mode, seatCount);
            yield return Banner(context, mode, suffix);
            yield return FreeSpell(context, suffix);
            yield return Knockout(context);
            Mess(context);
            yield return ReadyUp(context, suffix);
            yield return RoundOne(context, mode, suffix);
        }

        IEnumerator PlayCrew(QaContext context, string mode, int seatCount, string suffix)
        {
            if (!opened) throw new QaFailException("The warm-up scenario did not open, so " + HouseModes.Name(mode) + " was not run.");
            crew = true;
            try
            {
                yield return Launch(context, mode, seatCount);
                yield return Banner(context, mode, suffix);
                yield return FreeSpell(context, suffix);
                yield return ReadyUp(context, suffix);
                yield return CrewRun(context, mode, suffix);
            }
            finally { crew = false; }
        }

        bool CrewPlaying(string mode) => mode switch
        {
            HouseModes.MovingOut => Object.FindAnyObjectByType<MovingOutDirector>() is { } van && van.Current == MovingOutDirector.State.Playing,
            HouseModes.Escape => Object.FindAnyObjectByType<EscapeDirector>() is { } escape && escape.Current == EscapeDirector.State.Playing,
            _ => false,
        };

        IEnumerator CrewRun(QaContext context, string mode, string suffix)
        {
            yield return Until(() => CrewPlaying(mode), RoundOneSeconds, HouseModes.Name(mode) + " to start its run after the warm-up and line-up");
            bool spelling = LetterInventory.FreeSpelling || Humans().Any(p => p.Inventory.SpellsFree);
            var armed = Humans().Where(p => Held(p) != null || Stored(p) != null).Select(p => p.Name).ToArray();
            bool careerKept = PlayerPrefs.GetString(MatchTally.CareerKey, "") == career;
            context.Note("warmup_crew_run" + suffix, new Dictionary<string, object>
            {
                ["mode"] = Match.Mode, ["map"] = Session.MapId, ["round_manager_phase"] = rounds ? rounds.Phase.ToString() : "none",
                ["free_spelling"] = spelling, ["holding_gear"] = armed, ["career_unchanged"] = careerKept,
            });
            yield return Realtime(RoundSeconds);
            yield return Capture(context, "warmup_round_one" + suffix);
            Check(context, !spelling, "Free spelling is still on when the " + HouseModes.Name(mode) + " run starts.");
            Check(context, armed.Length == 0, "Warm-up gear was carried into the run by " + string.Join(", ", armed) + ".");
            Check(context, careerKept, "The career save changed during the warm-up.");
        }

        IEnumerator Launch(QaContext context, string mode, int seatCount)
        {
            string name = HouseModes.Name(mode), scene = Session.SceneForMode(mode);
            if (!Session.CanLoad(scene)) throw new QaFailException(scene + " is not in the build.");
            Time.timeScale = 1f;
            foreach (var seat in seats) seat.Next = default;
            rounds = null; joins = null; room = null; hud = null; human = partner = null; state = null; word = broke = null;
            seats = Enumerable.Range(1, seatCount).Select(i => new LocalSeat("qa-warmup-" + i)).ToArray();
            Session.Bindings.Clear();
            foreach (var seat in seats) Session.Remember(seat);
            UnityEngine.Random.InitState(context.Seed);
            int before = loads;
            RoundManager.WarmupRequested = LobbyMenu.WarmupBeforeMatches && Warmup.Applies(mode);
            if (!RoundManager.WarmupRequested)
                throw new QaFailException($"{name} did not ask for a warm-up (lobby switch {LobbyMenu.WarmupBeforeMatches}, applies {Warmup.Applies(mode)}).");
            launched = true;
            Session.LoadMode(mode, Map);
            yield return Until(() => loads > before && SceneManager.GetActiveScene().name == scene && Found(), LoadSeconds, "the " + name + " house to load");
            yield return Until(() => Entered(name), EnterSeconds, name + " to enter the warm-up");
            state = rounds.Warmup.State;
            human = Seated(0);
            partner = seatCount > 1 ? Seated(1) : null;
            if (!human || (seatCount > 1 && !partner)) throw new QaFailException("Not every local seat joined " + name + ". " + Dump());
            if (Match.Mode != mode || (!crew && Session.MapId != Map)) throw new QaFailException($"Expected {mode} on {Map}, got {Match.Mode} on {Session.MapId}.");
            bool free = Humans().All(p => !p.Frozen);
            var parked = ParkBots();
            hud.Follow(human);
            foreach (var p in Humans())
            {
                p.Inventory.Collects = false;
                p.Inventory.Set("");
            }
            context.Note("warmup_mode", Match.Mode);
            context.Note("warmup_map", Session.MapId);
            context.Note("warmup_flow", rounds.Directed ? "mode director" : "RoundManager");
            context.Note("warmup_phase", rounds.Phase.ToString());
            context.Note("warmup_round", rounds.Round);
            context.Note("warmup_length_seconds", state.Length);
            context.Note("warmup_voters", state.Voters);
            context.Note("warmup_players", joins.Players.Where(p => p).Select(p => p.Name + " " + Kind(p.Binding) + " team " + p.Team).ToArray());
            context.Note("warmup_bots_parked", parked);
            Check(context, !RoundManager.WarmupRequested, "The house scene did not use up the warm-up request.");
            Check(context, rounds.Round == 0, "Round " + rounds.Round + " had begun when the warm-up started.");
            Check(context, LetterInventory.FreeSpelling, "Free spelling is off in the warm-up.");
            Check(context, state.Voters == seatCount, $"{state.Voters} seats vote in the warm-up; {seatCount} local seats joined.");
            Check(context, crew || joins.Players.Count == HouseModes.BotSeats(mode), $"{joins.Players.Count} players are in the warm-up; {name} seats {HouseModes.BotSeats(mode)}.");
            Check(context, free, "A local seat was frozen when the warm-up started.");
            Check(context, !joins.AllowJoining, "Joining is still open in the warm-up.");
            NothingScored(context, "at the start of the warm-up");
        }

        bool Found()
        {
            rounds = RoundManager.Instance;
            joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            room = Object.FindAnyObjectByType<RoomBuilder>();
            hud = Object.FindAnyObjectByType<GameHud>();
            return rounds && joins && room && hud;
        }

        bool Entered(string name)
        {
            if (!rounds) return false;
            if (rounds.Phase == Phase.Warmup) return rounds.Warmup != null;
            if (rounds.Phase != Phase.Lobby) throw new QaFailException(name + " went to " + rounds.Phase + " without a warm-up. " + Dump());
            return false;
        }

        PlayerController Seated(int seat) => joins.Players.FirstOrDefault(p => p && p.Binding == seats[seat]);

        IEnumerable<PlayerController> Humans() => new[] { human, partner }.Where(p => p);

        string[] ParkBots()
        {
            var parked = new List<string>();
            foreach (var p in joins.Players)
            {
                if (!p || p.Binding is not BotBinding bot) continue;
                bot.Commands = default;
                p.Frozen = true;
                if (p.TryGetComponent<BotController>(out var brain)) brain.enabled = false;
                parked.Add(p.Name);
            }
            return parked.ToArray();
        }

        IEnumerator Banner(QaContext context, string mode, string suffix)
        {
            WarmupGuideStrip strip = null;
            yield return Until(() =>
            {
                strip = rounds && rounds.Warmup != null ? rounds.Warmup.Strip : null;
                return strip && strip.Built && strip.Shown;
            }, StripSeconds, "the warm-up banner and card strip to show");
            yield return Realtime(SettleSeconds);
            if (!strip || !strip.Shown) throw new QaFailException("The warm-up strip went away before it could be captured. " + Dump());
            Canvas.ForceUpdateCanvases();
            var guide = ModeGuideBook.For(mode);
            var parent = strip.Root.parent as RectTransform;
            var box = Box(strip.Root);
            var texts = strip.Root.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t => t.text.Length > 0).ToArray();
            var small = texts.Where(t => t.fontSize < WarmupGuideStrip.MinFont - Slack).Select(t => $"{t.name} at {t.fontSize:0.#} pt").ToArray();
            var cut = texts.Where(t => t.isTextTruncated).Select(t => $"{t.name} '{t.text}'").ToArray();
            string title = strip.TitleText, clock = strip.ClockText, ready = strip.ReadyText, cardTitle = strip.CardTitleText, cardLine = strip.CardLineText;
            context.Note("warmup_strip", new Dictionary<string, object>
            {
                ["title"] = title, ["clock"] = clock, ["ready_line"] = ready, ["tip"] = strip.TipText,
                ["card"] = strip.Card, ["cards"] = strip.CardCount, ["card_title"] = cardTitle, ["card_line"] = cardLine,
                ["picture_shown"] = strip.PictureShown, ["beside_the_letter_bag"] = strip.InCorner,
                ["box"] = new[] { box.xMin, box.yMin, box.xMax, box.yMax },
                ["smallest_font"] = texts.Length > 0 ? texts.Min(t => t.fontSize) : 0f, ["texts_under_minimum"] = small, ["texts_cut_off"] = cut,
            });
            yield return Capture(context, "warmup_banner" + suffix);
            Check(context, rounds.Phase == Phase.Warmup, "The warm-up ended before the banner was captured.");
            Check(context, strip.Mode == mode, $"The strip shows the cards of {strip.Mode}, not {mode}.");
            Check(context, title == Warmup.Title, $"The banner reads '{title}', not '{Warmup.Title}'.");
            Check(context, IsClock(clock), $"The banner clock reads '{clock}'.");
            Check(context, ready.Length > 0, "The banner has no ready line although a seat can press Start.");
            Check(context, guide != null && strip.CardCount == guide.Steps.Count && strip.CardCount > 0,
                $"The strip has {strip.CardCount} cards; the guide has {(guide == null ? 0 : guide.Steps.Count)}.");
            Check(context, cardTitle.Length > 0 && cardLine.Length > 0, "The card on the strip has no title or no line.");
            Check(context, small.Length == 0, "Strip text is under " + WarmupGuideStrip.MinFont + " pt: " + string.Join(", ", small) + ".");
            Check(context, cut.Length == 0, "Strip text is cut off: " + string.Join(", ", cut) + ".");
            if (parent)
            {
                var area = Box(parent);
                Check(context, box.width > 0f && box.height > 0f && box.xMin >= area.xMin - BoxSlack && box.xMax <= area.xMax + BoxSlack
                    && box.yMin >= area.yMin - BoxSlack && box.yMax <= area.yMax + BoxSlack,
                    $"The strip ({box.xMin:0},{box.yMin:0})-({box.xMax:0},{box.yMax:0}) leaves the HUD area ({area.xMin:0},{area.yMin:0})-({area.xMax:0},{area.yMax:0}).");
            }
            Check(context, hud.AcceptsGameplayInput && Time.timeScale > 0f && !human.Frozen, "Play is held up while the strip shows.");
        }

        IEnumerator FreeSpell(QaContext context, string suffix)
        {
            if (rounds.Phase != Phase.Warmup) throw new QaFailException("The warm-up ended before the free spell. " + Dump());
            var seat = seats[0];
            var summoner = human.Summoner;
            var inventory = human.Inventory;
            inventory.Set("");
            word = ChooseWord(summoner) ?? (crew ? summoner.Words.Select(w => w.word).OrderBy(w => w.Length).ThenBy(w => w, System.StringComparer.Ordinal).FirstOrDefault() : null);
            if (word == null) throw new QaFailException("The recipe list has no item to spell.");
            yield return Until(() => human.CanAct && !human.IsDodging && !summoner.IsCrafting, ActSeconds, "P1 to be free to spell");
            bool empty = inventory.Count == 0, free = inventory.SpellsFree;
            seat.Next.spellHeld = true;
            seat.Next.spellDown = true;
            yield return Until(() => summoner.IsSpelling, ActSeconds, "the spell wheel to open");
            int offered = summoner.Ready.Count, recipes = summoner.Words.Count;
            int index = summoner.Ready.FindIndex(w => w.word == word);
            if (index < 0)
            {
                seat.Next = default;
                throw new QaFailException($"{word} is not on the spell wheel with an empty bag ({offered} of {recipes} recipes offered). " + Dump());
            }
            summoner.Select(index);
            yield return Realtime(WheelSeconds);
            yield return Capture(context, "warmup_spell_wheel" + suffix);
            seat.Next.spellHeld = false;
            seat.Next.spellUp = true;
            yield return Until(() => summoner.IsCrafting || Held(human) == word || Stored(human) == word, ActSeconds, word + " to start crafting with an empty bag");
            int reserved = inventory.ReservedCount;
            yield return Until(() => Held(human) == word || Stored(human) == word, CraftSeconds, word + " to appear");
            if (Held(human) != word)
            {
                seat.Next.swap = true;
                yield return Until(() => Held(human) == word, ActSeconds, word + " to be in hand");
            }
            yield return Realtime(ShortSeconds);
            context.Note("warmup_free_spell", new Dictionary<string, object>
            {
                ["word"] = word, ["bag_before"] = empty ? "" : "not empty", ["bag_after"] = Bag(human), ["letters_reserved_while_crafting"] = reserved,
                ["recipes_on_wheel"] = offered, ["recipes_in_mode"] = recipes, ["held"] = Held(human) ?? "nothing",
            });
            yield return Capture(context, "warmup_free_spell" + suffix);
            Check(context, empty && free, "The bag was not empty, or the seat did not spell for free, before the spell.");
            Check(context, offered == recipes, $"The free spell wheel offers {offered} of the mode's {recipes} recipes.");
            Check(context, reserved == 0, reserved + " letters were reserved by a free craft.");
            Check(context, inventory.Count == 0, "A free spell left letters in the bag: '" + Bag(human) + "'.");
            Check(context, rounds.Phase == Phase.Warmup, "The warm-up ended during the free spell.");
            NothingScored(context, "after the free spell");
        }

        static string ChooseWord(Summoner summoner)
        {
            if (summoner.Recipe(PreferredWord) != null) return PreferredWord;
            var known = new HashSet<string>(summoner.Words.Select(w => w.word));
            return GameConfig.Current.Items.Enabled
                .Where(i => i.Melee != null && (i.Family is HandlingFamily.MeleeSwing or HandlingFamily.MeleeThrust) && known.Contains(i.Id))
                .OrderBy(i => i.Id.Length).ThenBy(i => i.Id, System.StringComparer.Ordinal)
                .Select(i => i.Id).FirstOrDefault();
        }

        IEnumerator Knockout(QaContext context)
        {
            if (rounds.Phase != Phase.Warmup) throw new QaFailException("The warm-up ended before the knockout check. " + Dump());
            var foe = joins.Players.FirstOrDefault(p => p && p.Binding is BotBinding && Teams.AreHostile(p.Team, human.Team));
            float protectedUntil = Time.realtimeSinceStartup + ProtectionSeconds;
            while (human.Health.IsInvulnerable && Time.realtimeSinceStartup < protectedUntil) yield return null;
            string how = "a hit from " + (foe ? foe.Name : "nobody");
            if (!foe || !human.Health.ApplyDamage(Hits.Of(foe, Vector3.forward, HitSource.Melee, KnockDamage, 0f)) || !human.IsKnockedOut)
            {
                how = "knocked out directly";
                human.Health.Eliminate();
            }
            if (!human.IsKnockedOut) throw new QaFailException("P1 could not be knocked out in the warm-up. " + Dump());
            string fell = human.Health.State.ToString();
            float downAt = Time.realtimeSinceStartup;
            yield return Until(() => human && !human.IsKnockedOut, ComeBackSeconds, "the knocked-out P1 to come back during the warm-up");
            float took = Time.realtimeSinceStartup - downAt;
            context.Note("warmup_knockout", new Dictionary<string, object>
            {
                ["how"] = how, ["state"] = fell, ["seconds_to_come_back"] = took, ["rule_seconds"] = Warmup.RespawnSeconds,
                ["health_after"] = human.Health.Current, ["health_max"] = human.Health.Max,
            });
            Check(context, rounds.Phase == Phase.Warmup, "The knockout ended the warm-up.");
            Check(context, Full(human), $"P1 came back with {human.Health.Current:0.#} of {human.Health.Max:0.#} health.");
            NothingScored(context, "after the knockout");
        }

        void Mess(QaContext context)
        {
            pieces = room.Originals.Count;
            var piece = room.Originals.FirstOrDefault(s => s && s.isActiveAndEnabled && !s.IsBroken && !s.Invulnerable);
            if (piece)
            {
                broke = piece.Word;
                piece.Break();
            }
            context.Note("warmup_furniture_broken", broke ?? "none");
            context.Note("warmup_furniture_pieces", pieces);
        }

        IEnumerator ReadyUp(QaContext context, string suffix)
        {
            if (rounds.Phase != Phase.Warmup || rounds.Warmup == null)
                throw new QaFailException($"The warm-up ended before the ready hold ({state.TimeLeft:0.0} s were left on its clock). " + Dump());
            bool lengthened = state.TimeLeft < MinReadyLeft;
            if (lengthened) state.Length = state.Elapsed + MinReadyLeft;
            var strip = rounds.Warmup.Strip;
            if (partner)
            {
                yield return Hold(seats[0], () => state.IsReady(human.Index), "P1 to be marked ready after holding Start");
                bool waiting = rounds.Phase == Phase.Warmup;
                int readyCount = state.ReadyCount;
                bool partnerReady = state.IsReady(partner.Index);
                string expected = state.ReadyLine();
                float lineBy = Time.realtimeSinceStartup + ReadyLineSeconds;
                while (waiting && strip && strip.ReadyText != expected && Time.realtimeSinceStartup < lineBy) yield return null;
                yield return Realtime(ShortSeconds);
                string shown = strip ? strip.ReadyText : "";
                context.Note("warmup_one_ready_line", shown);
                yield return Capture(context, "warmup_one_ready" + suffix);
                Check(context, waiting, "The warm-up ended with only one of two players ready.");
                Check(context, readyCount == 1 && !partnerReady, $"{readyCount} seats were ready after only P1 held Start.");
                Check(context, shown == expected, $"The banner reads '{shown}', not '{expected}', with one player ready.");
                if (rounds.Phase != Phase.Warmup) throw new QaFailException("The warm-up ended before P2 held Start. " + Dump());
            }
            yield return Hold(seats[seats.Length - 1], () => rounds.Phase != Phase.Warmup, "the warm-up to end once every player has held Start");
            float endedAt = Time.realtimeSinceStartup;
            string next = rounds.Phase.ToString();
            bool byVotes = state.AllReady && state.TimeLeft > 0f, closed = rounds.Warmup == null, spelling = LetterInventory.FreeSpelling, paused = hud.Paused;
            yield return null;
            context.Note("warmup_end", new Dictionary<string, object>
            {
                ["ended_by_ready_votes"] = byVotes, ["ready"] = state.ReadyCount, ["voters"] = state.Voters, ["seconds_left"] = state.TimeLeft,
                ["clock_lengthened"] = lengthened, ["phase_after"] = next, ["round_after"] = rounds.Round,
            });
            Check(context, byVotes, $"The warm-up did not end on the ready votes ({state.ReadyCount} of {state.Voters} ready, {state.TimeLeft:0.0} s left).");
            Check(context, closed, "The warm-up runner was still open after the warm-up ended.");
            Check(context, !spelling, "Free spelling stayed on after the warm-up ended.");
            Check(context, !paused, "Holding Start opened the pause menu.");
            Check(context, !strip, "The warm-up strip was still there after the warm-up ended.");
            while (Time.realtimeSinceStartup < endedAt + AfterSeconds) yield return null;
            context.Note("warmup_after_phase", rounds ? rounds.Phase.ToString() : "no RoundManager");
            yield return Capture(context, "warmup_after" + suffix);
        }

        IEnumerator Hold(LocalSeat seat, System.Func<bool> done, string what)
        {
            float end = Time.realtimeSinceStartup + ReadySeconds;
            while (!done())
            {
                if (rounds.Phase != Phase.Warmup) throw new QaFailException("The warm-up ended while waiting for " + what + ". " + Dump());
                if (Time.realtimeSinceStartup > end) throw new QaFailException($"Timed out after {ReadySeconds:0} s waiting for {what}. " + Dump());
                seat.Next.start = true;
                yield return null;
            }
        }

        IEnumerator RoundOne(QaContext context, string mode, string suffix)
        {
            yield return Until(() => rounds && rounds.Phase == Phase.Playing, RoundOneSeconds, "round 1 to start playing after the warm-up");
            string starter = Match.Rules.StarterLetters ?? "";
            var players = joins.Players.Where(p => p).ToArray();
            var bags = Humans().Select(p => p.Name + " '" + Bag(p) + "'").ToArray();
            bool bagsFresh = Humans().All(p => Bag(p) == starter && p.Inventory.ReservedCount == 0);
            var hurt = players.Where(p => !Full(p)).Select(p => $"{p.Name} {p.Health.Current:0.#}/{p.Health.Max:0.#}").ToArray();
            bool spelling = LetterInventory.FreeSpelling || Humans().Any(p => p.Inventory.SpellsFree);
            var armed = Humans().Where(p => Held(p) != null || Stored(p) != null).Select(p => p.Name).ToArray();
            var winners = players.Where(p => rounds.WinsOf(p) != 0).Select(p => p.Name).ToArray();
            var race = Object.FindAnyObjectByType<FreeForAllDirector>();
            var scorers = race && race.Race != null ? players.Where(p => race.Race.Of(p.Index) != 0).Select(p => p.Name).ToArray() : new string[0];
            int broken = MatchTally.RoundBroken, crafted = MatchTally.RoundCrafted, damage = MatchTally.RoundDamage;
            int round = rounds.Round, standing = room.Originals.Count(s => s && !s.IsBroken), count = room.Originals.Count;
            bool spends = starter.Length == 0 && word != null && !human.Summoner.Summon(word);
            bool careerKept = PlayerPrefs.GetString(MatchTally.CareerKey, "") == career;
            context.Note("warmup_round_one", new Dictionary<string, object>
            {
                ["phase"] = rounds.Phase.ToString(), ["round"] = round, ["bags"] = bags, ["starter_letters"] = starter, ["hurt"] = hurt,
                ["free_spelling"] = spelling, ["holding_gear"] = armed, ["round_wins"] = winners, ["knockout_scores"] = scorers,
                ["tally_broken"] = broken, ["tally_crafted"] = crafted, ["tally_damage"] = damage,
                ["furniture_standing"] = standing, ["furniture_pieces"] = count, ["career_unchanged"] = careerKept,
            });
            yield return Realtime(RoundSeconds);
            yield return Capture(context, "warmup_round_one" + suffix);
            Check(context, round == 1, "Round " + round + " is playing after the warm-up; round 1 was expected.");
            Check(context, bagsFresh, $"Round 1 bags are not the starter letters '{starter}': " + string.Join(", ", bags) + ".");
            Check(context, starter.Length == 0, $"The rules give starter letters '{starter}', so the bag is not empty in round 1.");
            Check(context, hurt.Length == 0, "Round 1 started without full health: " + string.Join(", ", hurt) + ".");
            Check(context, !spelling, "Free spelling is still on in round 1.");
            Check(context, spends || starter.Length > 0, word + " could be spelled with an empty bag in round 1.");
            Check(context, armed.Length == 0, "Warm-up gear was carried into round 1 by " + string.Join(", ", armed) + ".");
            Check(context, winners.Length == 0, "Round wins were carried into round 1 by " + string.Join(", ", winners) + ".");
            Check(context, scorers.Length == 0, "Knockouts were carried into round 1 by " + string.Join(", ", scorers) + ".");
            Check(context, mode != HouseModes.FreeForAll || (race && race.Race != null), "Free-for-All has no knockout race in round 1.");
            Check(context, broken == 0 && crafted == 0 && damage == 0, $"The round tally starts at {broken} broken, {crafted} crafted, {damage} damage.");
            Check(context, standing == count && count == pieces, $"{standing} of {count} furniture pieces stand in round 1; the warm-up started with {pieces} and broke {broke ?? "none"}.");
            Check(context, careerKept, "The career save changed during the warm-up.");
        }

        void NothingScored(QaContext context, string when)
        {
            var race = Object.FindAnyObjectByType<FreeForAllDirector>();
            Check(context, rounds.Round == 0, "Round " + rounds.Round + " had begun " + when + ".");
            Check(context, joins.Players.All(p => !p || rounds.WinsOf(p) == 0), "A player holds a round win " + when + ".");
            Check(context, !hud.ResultShown && !hud.ResultPending, "A result card is up or on its way " + when + ".");
            Check(context, !race || race.Race == null, "A knockout race is running " + when + ".");
            Check(context, PlayerPrefs.GetString(MatchTally.CareerKey, "") == career, "The career save changed " + when + ".");
        }

        IEnumerator Return(QaContext context)
        {
            Time.timeScale = 1f;
            foreach (var seat in seats) seat.Next = default;
            RoundManager.WarmupRequested = false;
            if (opened) LobbyMenu.WarmupBeforeMatches = savedSwitch;
            if (originalBindings != null)
            {
                Session.Bindings.Clear();
                Session.Bindings.AddRange(originalBindings);
            }
            if (launched)
            {
                Session.GoHome();
                yield return Until(() => SceneManager.GetActiveScene().name == Session.HubScene && Object.FindAnyObjectByType<LobbyMenu>() != null, HubSeconds, "the Hub");
            }
            if (originalMap != null && Session.MapId != originalMap) Session.SelectMap(originalMap);
            var changed = preferences == null ? new Saved[0] : preferences.Where(p => !p.Matches()).ToArray();
            foreach (var preference in changed) preference.Restore();
            if (changed.Length != 0) PlayerPrefs.Save();
            Application.logMessageReceived -= Log;
            SceneManager.sceneLoaded -= Loaded;
            context.Note("warmup_switch_restored", LobbyMenu.WarmupBeforeMatches == savedSwitch);
            context.Note("warmup_free_spelling_left_on", LetterInventory.FreeSpelling);
            context.Note("warmup_prefs_restored", changed.Select(p => p.Key).ToArray());
            context.Note("warmup_errors", errors.ToArray());
            context.Note("warmup_seconds", context.ElapsedSeconds);
            if (LetterInventory.FreeSpelling) throw new QaFailException("Free spelling was still on back in the Hub.");
            if (errors.Count > 0) throw new QaFailException($"The run logged {errors.Count} error(s); first: {errors[0]}");
        }

        void Check(QaContext context, bool ok, string message)
        {
            if (!ok) context.Fail(message + " " + Dump());
        }

        string Dump()
        {
            var parts = new List<string> { "State: scene " + SceneManager.GetActiveScene().name, "mode " + Match.Mode, "time scale " + Time.timeScale.ToString("0.##") };
            if (!rounds) parts.Add("no RoundManager");
            else
            {
                parts.Add($"phase {rounds.Phase}, round {rounds.Round}, {(rounds.Directed ? "mode director" : "RoundManager")} flow");
                var open = rounds.Warmup;
                parts.Add(open == null ? "warm-up closed" : $"warm-up open with {open.State.TimeLeft:0.0} s left, {open.State.ReadyCount} of {open.State.Voters} ready, " +
                    (open.Strip ? open.Strip.Shown ? "strip shown" : "strip hidden" : "no strip"));
            }
            parts.Add("free spelling " + LetterInventory.FreeSpelling);
            if (hud) parts.Add($"HUD takes input {hud.AcceptsGameplayInput}, paused {hud.Paused}, result shown {hud.ResultShown}, bag book open {hud.BagOpen}, touch controls {hud.TouchControlsShown}");
            if (joins)
                foreach (var p in joins.Players)
                    if (p)
                        parts.Add($"{p.Name} {Kind(p.Binding)} team {p.Team} {p.Health.State} {p.Health.Current:0.#}/{p.Health.Max:0.#}" +
                            $"{(p.Frozen ? " frozen" : "")} bag '{Bag(p)}' holds {Held(p) ?? "nothing"}");
            return string.Join("; ", parts) + ".";
        }

        static bool Full(PlayerController player) => player.Health.IsAlive && player.Health.Current >= player.Health.Max - Slack;

        static string Bag(PlayerController player) => new string(player.Inventory.Letters.ToArray());

        static string Held(PlayerController player)
        {
            var weapon = player.Combat.Weapon;
            return weapon ? weapon.word : null;
        }

        static string Stored(PlayerController player)
        {
            var gear = player.Combat.StoredGear;
            return gear ? gear.word : null;
        }

        static string Kind(InputBinding binding) => binding switch
        {
            LocalSeat => "seat",
            BotBinding => "bot",
            null => "none",
            _ => binding.GetType().Name,
        };

        static bool IsClock(string text)
        {
            int colon = text.IndexOf(':');
            return colon >= 1 && text.Length == colon + 3 && text.Where((_, i) => i != colon).All(char.IsDigit);
        }

        static Rect Box(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        static IEnumerator Realtime(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        IEnumerator Until(System.Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) throw new QaFailException($"Timed out after {seconds:0} s waiting for {what}. " + Dump());
                yield return null;
            }
        }

        static IEnumerator Capture(QaContext context, string name)
        {
            string path = context.PathInRun(name + ".png"), error = null;
            if (File.Exists(path)) throw new QaFailException("Capture never overwrites existing evidence: " + name);
            if (PerfProbe.Instance) PerfProbe.Instance.MarkFirstVisit(CaptureFrames);
            yield return StoryCamera.CaptureScreen(path, result => error = result);
            if (error != null) throw new QaFailException("Screen capture failed: " + error);
            var image = new Texture2D(2, 2);
            try
            {
                if (!image.LoadImage(File.ReadAllBytes(path))) throw new QaFailException("Capture is not a readable PNG: " + name);
                var pixels = image.GetPixels32();
                int distinct = pixels.Where((_, i) => i % BlankStride == 0).Select(p => (p.r >> 3, p.g >> 3, p.b >> 3)).Distinct().Count();
                if (distinct < BlankColours) throw new QaFailException("Capture looks blank: " + name);
            }
            finally { Object.Destroy(image); }
            context.AddCapture(name, path, "png", "screen");
        }

        sealed class LocalSeat : InputBinding
        {
            readonly string id;
            public PlayerCommands Next;

            public LocalSeat(string id) => this.id = id;

            public override string Id => id;

            public override void Read(ref PlayerCommands c)
            {
                c = Next;
                Next.grab = Next.attack = Next.jump = Next.dodge = Next.drop = Next.swap = false;
                Next.slot = 0;
                Next.spellDown = Next.spellUp = Next.up = Next.down = Next.start = false;
                Next.lookDelta = Vector2.zero;
            }

            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }

        sealed class Saved
        {
            static readonly string[] Strings = { MatchTally.CareerKey, MatchTally.CorruptKey, "wv.outfit.0", "wv.outfit.1", "wv.outfit.2", "wv.outfit.3" };
            static readonly string[] Integers = GameplayPrefs.Keys.Concat(new[] { ControlTips.LearnedKey, ControlTips.SeenKey, CombatFeedbackOptions.PointsKey, "wv.muted" }).ToArray();

            public string Key { get; private set; }
            bool integer, existed;
            string text;
            int number;

            static Saved Read(string key, bool integer) => new Saved
            {
                Key = key, integer = integer, existed = PlayerPrefs.HasKey(key),
                text = integer ? null : PlayerPrefs.GetString(key), number = integer ? PlayerPrefs.GetInt(key) : 0,
            };

            public static Saved[] Capture() => Strings.Select(k => Read(k, false)).Concat(Integers.Select(k => Read(k, true))).ToArray();

            public bool Matches() => existed == PlayerPrefs.HasKey(Key) &&
                (!existed || (integer ? number == PlayerPrefs.GetInt(Key) : text == PlayerPrefs.GetString(Key)));

            public void Restore()
            {
                if (!existed) PlayerPrefs.DeleteKey(Key);
                else if (integer) PlayerPrefs.SetInt(Key, number);
                else PlayerPrefs.SetString(Key, text);
            }
        }
    }
}
#endif
