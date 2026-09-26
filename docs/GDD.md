# Wreckabulary: Game Design Document

## Pitch

**Wreck the room, build the word!**

A 2–4 player couch party game where everything is made of letters. Smash furniture into letter tiles, grab them, and spell them into new things. Everything you summon can be smashed and re-spelled too.

- **Genre:** Party, brawler, word game
- **Theme:** Moving day in a cozy world where everything is made of words
- **Art style:** Toybox Workshop

![Gameplay mockup](art/gameplay_mockup.jpg)

## Pillars

1. **Everything is letters.** Furniture, weapons and health are all letter tiles.
2. **Cozy room, total chaos.** Warm homely spaces turned into floppy ragdoll battlefields.
3. **Readable at a glance.** Chunky shapes, bold letters, one colour per player.

## Core loop

Smash → Scavenge → Spell → Summon. Summoned objects can themselves be smashed, scattering their letters for anyone to steal.

## Premise (trailer)

Roommates carry boxes through the door → one box turns out to be made of B-O-X → it pops open and tiles spill out → two roommates grab for the same letters → the fight begins.

## Hub

Your own customisable house, where you can also customise your character. Friends join by picking up a controller and walking in the door. Choose a mode at the typewriter.

## Modes

### Tutorial

Teaches smashing, grabbing, spelling and summoning.

### Versus: "Dibs!"

Fight your roommates for the best stuff. Letters are your health.

- Everyone starts a round with **3 letters**, so the first hit isn't a knockout.
- Carry at most **6 letters**.
- A hit knocks **2 letters** loose. A hit with **0 letters** is a knockout.
- Last player standing wins the round. First to **3 rounds** wins.
- After **90 seconds** the room starts collapsing to force fights.

- When the room runs low on letters, labelled **delivery boxes** drop in (PIZZA, QUILT, SOCKS…). Smash one to get the letters on its label.
- Summons **fall apart into their letters** when used up (a weapon out of swings, SKATES wearing off), so spent letters come back into play.

Later variations: **Furnish First** (race to furnish the room), and more.

### Co-op: "Moving Day"

Furnish the house together, 1–4 players. Unpack boxes, spell the checklist items and place them in the right rooms, with obstacles and a time limit. Each level awards 1–3 stars.

### Creative: "Home Sweet Home"

The typewriter prints endless letters to build and decorate your house. Hit **Play** to turn it into a Dibs! arena with custom rules. Rooms can be saved and loaded.

## Words

About 40 curated words in four categories: Weapon, Defence, Movement, Chaos. Longer words and rare letters (Q, Z, X, J) are stronger. Hidden words are not shown in the word wheel until discovered. Full list: `Assets/_Project/Data/word_list.csv`. What each word does is in `SummonEffects.cs`.

Note: UMBRELLA has 8 letters but players carry at most 6, so it can never be spelled. Either raise the carry limit or swap it for a shorter word.

## Characters

Cartoonish, blobby ragdoll characters (in the spirit of Fall Guys and PEAK) in knitted sweaters with a big initial letter, customisable in the hub. In the prototype the four sweater initials spell W, O, R, D. Stable core, floppy limbs. All four play the same; differences are cosmetic.

## Maps

1. **Living room** (main, fully polished): sofa, coffee table, lamp, mug, plate, vase.
2. **Bedroom** (should have)
3. **Kitchen** (stretch goal)
4. **Garden** (stretch goal)
5. More to come

Stretch goal: a mischievous letter **cat** wanders the room, steals loose letters and runs off with them.

## Art direction: Toybox Workshop

Warm wooden letter tiles, cartoonish blobby ragdoll characters, cream and sage walls, terracotta accents. Objects are built from their own letters. Fonts: Lilita One (display), Nunito (UI), Gochi Hand (tagline).

## Technical highlights (for the Technicality grade)

- Active ragdoll characters with balance forces and grab joints
- Destruction that bursts objects into their exact letters
- Real-time word solver over held letters
- Letter-built objects assembled from letter meshes
- Object pooling for dozens of physics tiles
