using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public enum TutorialStep
    {
        Walk, JumpDodge, Smash, Collect, Book, Spell, Hit, Throw, Place, Heal, Effect, Hands, KnockedOut, Block, Finish,
    }

    public enum KnockoutBeat { Standing, Down, Back }

    public static class TutorialSteps
    {
        public const string FirstWord = "BAT", FirstFurniture = "TABLE", EffectWord = "LAMP", ThrowWord = "CHAIR";
        public const string PlaceWord = "TABLE", HealWord = "APPLE", ShieldWord = "PLATE";
        public const string TouchPlace = "PLACE", TouchUse = "USE";
        public const int MaxWords = 12;
        public const float GiveUpSeconds = 60f;

        static readonly TutorialStep[] order =
        {
            TutorialStep.Walk, TutorialStep.JumpDodge, TutorialStep.Smash, TutorialStep.Collect, TutorialStep.Book,
            TutorialStep.Spell, TutorialStep.Hit, TutorialStep.Throw, TutorialStep.Place, TutorialStep.Heal,
            TutorialStep.Effect, TutorialStep.Hands, TutorialStep.KnockedOut, TutorialStep.Block, TutorialStep.Finish,
        };

        public static IReadOnlyList<TutorialStep> Order => order;

        public static bool GivesUp(TutorialStep step) =>
            step is TutorialStep.Place or TutorialStep.Heal or TutorialStep.KnockedOut or TutorialStep.Block;

        public static string Text(TutorialStep step) => step switch
        {
            TutorialStep.Walk => "Walk around",
            TutorialStep.JumpDodge => "Jump, then dodge",
            TutorialStep.Smash => $"Smash the {FirstFurniture}",
            TutorialStep.Collect => "Pick up the letters",
            TutorialStep.Book => "Open the recipe book",
            TutorialStep.Spell => $"Spell {FirstWord}",
            TutorialStep.Hit => "Whack the dummy",
            TutorialStep.Throw => $"Throw the {ThrowWord}",
            TutorialStep.Place => $"Spell {PlaceWord}, then put it down",
            TutorialStep.Heal => $"Take a punch, then spell {HealWord} and eat it",
            TutorialStep.Effect => $"Smash the {EffectWord}, then spell {EffectWord}",
            TutorialStep.Hands => "Switch hands",
            TutorialStep.KnockedOut => "Let the dummy knock you out",
            TutorialStep.Block => $"Spell {ShieldWord}, then block the dummy's punch",
            TutorialStep.Finish => "Knock out the dummy",
            _ => "",
        };

        public static int Words(string sentence) =>
            string.IsNullOrWhiteSpace(sentence) ? 0 : sentence.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;

        static string HeldKey(ControlDevice device, string attack, string touch) => device == ControlDevice.Touch ? touch : attack;

        public static string PlaceHow(ControlDevice device, string attack, string word) =>
            $"Smash the {word} again for its letters. {ControlWords.Verb(device)} {HeldKey(device, attack, TouchPlace)} to place it as cover.";

        public static string HealHow(ControlDevice device, string attack, string word, int amount, bool hurt) => hurt
            ? $"Smash the {word} for its letters. {ControlWords.Verb(device)} {HeldKey(device, attack, TouchUse)} to eat it and heal {amount}."
            : "Walk up to the dummy and let it punch you first.";

        public static string BlockHow(ControlDevice device, string block, string word) =>
            $"Smash the {word} for its letters. Hold {block} to stop hits from the front.";

        public static string KnockedOutHow(ControlDevice device, string grab, KnockoutBeat beat, int seconds) => beat switch
        {
            KnockoutBeat.Down => $"You are back in {seconds} seconds. Some modes keep you out until the next round.",
            KnockoutBeat.Back => $"You are back at full health with empty hands. {ControlWords.Verb(device)} {grab} to pick your items up.",
            _ => "Stand next to the dummy. A knockout drops your letters and both items.",
        };

        public static string HandsFullHow(ControlDevice device, string drop) => device == ControlDevice.Touch
            ? $"Your hands are full. Use {drop} to drop an item first."
            : $"Your hands are full. Hold {drop} to drop an item first.";
    }
}
