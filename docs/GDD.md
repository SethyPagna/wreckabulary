# Wreckabulary: Game Design Document

## Pitch

A 2–4 player couch brawler set in a cozy family home. Players smash furniture into letter tiles and spell new objects to fight with. Everything is made of letters, including what you summon.

![Gameplay mockup](art/gameplay_mockup.jpg)

## Pillars

1. **Everything is letters.** Furniture, weapons and health are all letter tiles.
2. **Cozy room, total chaos.** Warm homely spaces turned into floppy ragdoll battlefields.
3. **Readable at a glance.** Chunky shapes, bold letters, one colour per player.

## Core loop

Smash → Scavenge → Spell → Summon. Summoned objects can themselves be smashed, scattering their letters for anyone to steal.

## Rules

- Carry at most **6 letters**.
- A hit knocks **2 letters** loose. A hit with **0 letters** is a knockout.
- Last player standing wins the round. First to **3 rounds** wins.
- After **90 seconds** the room starts collapsing to force fights.

## Words

About 40 curated words in four categories: Weapon, Defence, Movement, Chaos. Longer words and rare letters (Q, Z, X, J) are stronger. Hidden words are not shown in the word wheel until discovered. Full list: `Assets/_Project/Data/word_list.csv`.

## Characters

Cartoonish round-headed people in knitted sweaters with a big initial letter, driven by ragdoll physics. Stable core, floppy limbs. All four play the same; differences are cosmetic.

## Maps

1. **Living room** (main, fully polished): sofa, coffee table, lamp, mug, plate, vase.
2. **Kitchen** (stretch goal)
3. **Bedroom** (stretch goal)

A mischievous letter **cat** wanders the room, steals loose letters and runs off with them.

## Art direction: Toybox Workshop

Warm wooden letter tiles, cream and sage walls, terracotta accents. Objects are built from their own letters. Fonts: Lilita One (display), Nunito (UI), Gochi Hand (tagline).

## Technical highlights (for the Technicality grade)

- Active ragdoll characters with balance forces and grab joints
- Destruction that bursts objects into their exact letters
- Real-time word solver over held letters
- Letter-built objects assembled from letter meshes
- Object pooling for dozens of physics tiles
