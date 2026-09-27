using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>
    /// Says which JSON files in <c>Data/Config</c> the game reads. It sits in Resources so builds
    /// can find it. Designers edit the JSON, not this asset; <see cref="GameConfig.Current"/>
    /// parses and checks the files the first time something asks for them.
    /// </summary>
    [CreateAssetMenu(menuName = "Wreckabulary/Game Data")]
    public sealed class GameData : ScriptableObject
    {
        public const string ResourcePath = "GameData";

        [Tooltip("rules.json: limits and timings, with overrides per mode.")]
        public TextAsset rules;
        [Tooltip("items.json: the recipe catalogue.")]
        public TextAsset items;
        [Tooltip("house_*.json: the map's rooms, doors, spawns, furniture and clear-out orders.")]
        public TextAsset house;
        [Tooltip("wardrobe.json: the avatar's pieces and colourways.")]
        public TextAsset wardrobe;

        public GameConfig Parse()
        {
            var missing = new List<string>();
            if (!rules) missing.Add(nameof(rules));
            if (!items) missing.Add(nameof(items));
            if (!house) missing.Add(nameof(house));
            if (!wardrobe) missing.Add(nameof(wardrobe));
            if (missing.Count > 0)
                throw new InvalidOperationException($"{name} has no {string.Join(", ", missing)} file. Run Wreckabulary > Data > Set Up Game Data.");
            return GameConfig.FromJson(rules.text, items.text, house.text, wardrobe.text, rules.name, items.name, house.name, wardrobe.name);
        }
    }

    /// <summary>
    /// The game's data, parsed through the Rules layer and checked as a whole: rules for every
    /// mode, the item catalogue, the house and the wardrobe. Loading fails with every problem
    /// listed, so a bad edit shows up at start-up instead of mid-match.
    /// </summary>
    public sealed class GameConfig
    {
        public RuleBook Rules { get; }
        public ItemCatalogue Items { get; }
        public HouseLayout House { get; }
        public WardrobeCatalogue Wardrobe { get; }

        static GameConfig current;

        /// <summary>The data from Resources/GameData, loaded on first use.</summary>
        public static GameConfig Current => current ??= Load();

        /// <summary>Replaces the current data, for tests and tools. Null means load it again next time.</summary>
        public static void Use(GameConfig config) => current = config;

        public GameConfig(RuleBook rules, ItemCatalogue items, HouseLayout house, WardrobeCatalogue wardrobe)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Items = items ?? throw new ArgumentNullException(nameof(items));
            House = house ?? throw new ArgumentNullException(nameof(house));
            Wardrobe = wardrobe ?? throw new ArgumentNullException(nameof(wardrobe));
        }

        public static GameConfig Load()
        {
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            if (!data) throw new InvalidOperationException($"Resources/{GameData.ResourcePath} is missing. Run Wreckabulary > Data > Set Up Game Data.");
            return data.Parse();
        }

        /// <summary>Parses and checks the four files. Throws if any of them has a problem.</summary>
        public static GameConfig FromJson(string rules, string items, string house, string wardrobe,
            string rulesName = "rules.json", string itemsName = "items.json", string houseName = "house.json", string wardrobeName = "wardrobe.json")
        {
            var config = new GameConfig(
                RuleBook.FromJson(rules, rulesName),
                ItemCatalogue.FromJson(items, itemsName),
                HouseLayout.FromJson(house, houseName),
                WardrobeCatalogue.FromJson(wardrobe, wardrobeName));
            var problems = config.Validate();
            if (problems.Count > 0)
                throw new InvalidOperationException($"The game data has {problems.Count} problem(s):\n" + string.Join("\n", problems));
            return config;
        }

        /// <summary>Rules for a mode, or the defaults for a mode rules.json doesn't list.</summary>
        public GameRules RulesFor(string mode) => Rules.Modes.Contains(mode) ? Rules.For(mode) : Rules.Defaults;

        /// <summary>Every problem in the data, each file's own checks plus the ones that span files.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            problems.AddRange(Rules.Validate().Select(p => "rules: " + p));
            foreach (var rules in AllRules())
                foreach (string p in Items.Validate(rules.MaxLetters))
                    if (!problems.Contains("items: " + p)) problems.Add("items: " + p);
            problems.AddRange(House.Validate(Items).Select(p => "house: " + p));
            problems.AddRange(Wardrobe.Validate().Select(p => "wardrobe: " + p));

            foreach (var rules in AllRules())
            {
                string mode = rules.Mode;
                if (rules.ClearOutEnabled && !House.ClearOutOrders.ContainsKey(mode))
                    problems.Add($"rules: {mode} turns the clear-out on, but {House.Name} has no clear-out order for it");
                if (rules.StarterLetters.Length > rules.MaxLetters)
                    problems.Add($"rules: {mode} starts players with {rules.StarterLetters.Length} letters but a bag holds {rules.MaxLetters}");
                if (rules.StarterLetters.Length > 0 && !LetterBag.IsWord(rules.StarterLetters))
                    problems.Add($"rules: {mode} starter letters must be A-Z");
            }
            foreach (string mode in House.ClearOutOrders.Keys)
                if (!Rules.Modes.Contains(mode))
                    problems.Add($"house: clear-out order '{mode}' is for a mode rules.json doesn't have");
            foreach (var f in House.Furniture)
                if (Items.TryGet(f.Word, out var item) && string.IsNullOrEmpty(item.Model))
                    problems.Add($"house: {f.Word} stands in {f.Room}, but its item has no model");
            return problems;
        }

        IEnumerable<GameRules> AllRules() => new[] { Rules.Defaults }.Concat(Rules.Modes.Select(Rules.For));
    }
}
