# Wreckabulary

**Wreck the room, build the word!**

Wreckabulary is a 2–4 player couch party game for COMP4122 where everything is made of letters. Smash furniture into letter tiles, grab them, and spell them into new things. Everything you summon can be smashed and re-spelled too.

| | |
|---|---|
| Genre | Party, brawler, word game |
| Theme | Moving day in a cozy world where everything is made of words |
| Art style | Toybox Workshop: warm wooden letter tiles, blobby ragdoll characters, objects built from their own letters |

## Core loop

**Smash → Scavenge → Spell → Summon**

1. **Smash** furniture and it bursts into the letters that spell it.
2. **Scavenge** letters. You carry up to 6, and getting hit knocks some loose.
3. **Spell** the word yourself, letter by letter, from the letters you carry.
4. **Summon** the object instantly, like a BLADE, WINGS or BEES.

## Modes

Everything starts in the **hub**, your own customisable house. Friends join by picking up a controller and walking in the door, and you choose a mode at the typewriter.

| Mode | Players | Summary |
|---|---|---|
| Tutorial | 1–4 | Learn to smash, grab and spell. |
| **Versus: "Dibs!"** | 2–4 | Fight your roommates for the best stuff. Letters are your health. Last roommate standing wins the round, first to 3 wins. |
| **Co-op: "Moving Day"** | 1–4 | Furnish the house together: unpack boxes, spell the checklist items and place them in the right rooms against the clock. 1–3 stars per level. |
| **Creative: "Home Sweet Home"** | 1–4 | The typewriter prints endless letters to build and decorate. Hit Play to turn your room into a Dibs! arena with custom rules. Save and load rooms. |

Maps: living room, bedroom, kitchen, garden, and more.

## Tech

| | |
|---|---|
| Engine | Unity 6 (6000.6.3f1) |
| Render pipeline | URP |
| Input | Unity Input System: up to 4 gamepads, or 2 players sharing a keyboard |
| Physics | Rigidbody roommates with a wobbly visual rig, pooled rigidbody letter tiles |
| Platform | Windows PC, local multiplayer |

## Getting started

1. Install **Git LFS** once: `git lfs install`
2. Clone the repo: `git clone <repo-url>`
3. Open the folder with **Unity Hub → Add project from disk**, using the Unity version above.
4. Open `Assets/_Project/Scenes/Hub.unity` and press Play.
5. Press a button to walk in through the front door (Space, `.`, or A on a gamepad). Walk up to the typewriter, press grab, and pick a mode.

You can also open `LivingRoom.unity` or `Tutorial.unity` directly. Players join there by pressing a button.

## What's playable

**The house (hub):** roommates join by walking in through the front door. Pick a mode at the typewriter: Tutorial, Dibs!, Furnish First, Moving Day or Home Sweet Home. Whoever is in the house comes along into the mode, and everyone heads home after a match or on Esc / Select.

**Tutorial:** 7 steps covering walk, smash the BAT box, pick up the letters, spell BAT, whack the dummy, throw the chair, and knock the dummy out. Any roommate can complete a step.

**Moving Day** (co-op, 1–4 players) in a house split into a living room and a bedroom. Boxes labelled with the checklist arrive at the front door. Smash them for the letters, spell each item (only checklist words work here) to build it, and carry it into the right room. Placed furniture locks in with a tick. Stars (1–3) depend on time left. Run out of time and the level restarts. There are 3 levels: *Moving In*, *Housewarming* (with slippery spills) and *Cozy Corner* (SHELF, TEDDY, MIRROR, PIANO, STOOL, FAN). Lost letters are resent in a new box.

**Furnish First** (versus, 2–4 players): everyone gets a colour-coded corner and the same checklist of 3 objects. Boxes of those words drop in the middle, so you all fight over the same letters. Spell the items and get them into *your* corner, at rest. The first to have the whole list wins the round, and first to 2 rounds wins. Anything in your corner counts, so stealing, smashing and shoving are all fair. Knocked-out roommates get back up in their corner.

**Home Sweet Home** (Creative, 1–4 players): build your own room. Press spell and the typewriter gives you endless letters. Pick any letter from A to Z (repeats allowed) and any of the 43 objects is built for free. Grab to arrange things, punch to remove them (the letters tidy themselves away), and nobody gets knocked out. At the room menu desk you can:
- **Play Dibs! here:** a match in your room with your rules (rounds to win: 1, 2, 3 or 5, and starting letters: 0–6). Afterwards you're back in your room, still built.
- **Save or load the room** in one of three slots. Rooms are saved as JSON in Unity's `persistentDataPath/rooms`.
- **Clear the room.**

**Dibs!** in four arenas, chosen at the typewriter with left/right: **Living Room**, **Bedroom**, **Kitchen** and **Garden** (outdoors, with hedges and a garden path). First to 3 rounds:

- Smash furniture (SOFA, TABLE, LAMP, CHAIR…) with punches or by throwing things. It bursts into its own letters.
- Walk over letters to pick them up. You carry up to 6, and you start each round with 3.
- Press spell and your letters appear over your head. Move the highlight left/right, add letters one at a time (undo if you slip), and press spell again to cast. Hints show which words you could still finish. Non-words fizzle and you keep your letters. You stand still while spelling.
- Objects are built Word World-style from 3D copies of their own letters: the SOFA has S and A armrests, an O backrest and an F cushion, and the BED is a B headboard, a flat E mattress and a D footboard. Loose letters are 3D letters too.
- Grab anything. Light things are carried in front with both hands, and heavy things (and roommates) are lifted overhead. Heavy loads slow you down. Grab again to put it down neatly in front of you (on the floor, or on top of whatever is there), or punch to throw it.
- Hands full of letters? While spelling, drop the highlighted letter to make room for a better one.
- Action words do something: weapons (AXE, BAT, BLADE, SWORD, SPEAR, BOW, CANNON), WALL, SHIELD, ARMOR, WINGS, SPRING, SKATES, ROPE, BEES, FLOOD, MAGNET, DUCK, QUAKE, ZAP.
- 43 object words build the object in front of you, letter-shaped, and every one has its own Word World design:
  - Living room: BED, SOFA, TABLE, DESK, LAMP, CHAIR, RUG, TV, PLANT, CLOCK, VASE, MUG, BOOKS, PLATE, PILLOW, RADIO, BOX, PIANO, STOOL, FAN.
  - Kitchen: STOVE, FRIDGE, SINK, PAN, POT, KETTLE, CUP, BOWL.
  - Bedroom: SHELF, MIRROR, TEDDY, CLOSET.
  - Garden: TREE, BUSH, FLOWER, ROSE, BENCH, FENCE, SWING, GNOME, ROCK, HOSE, POND.
  Smash a TABLE and you can spell it right back, or build a SOFA to hide behind (or throw). The weapons have shapes too: the SWORD has an S pommel and a W crossguard, and the AXE an X head.
- Summons fall apart back into their letters when they're used up, so they can be grabbed and re-spelled.
- A hit knocks 2 letters loose. A hit with no letters is a knockout.
- Delivery boxes drop in when the room runs low on letters. After 90 seconds the room collapses and boxes rain down.

**Audio:** every sound and the music are synthesised in code at startup (`Scripts/Audio`), so there are no audio files or licences to track. You get wooden clacks, smashes, punches, typewriter keys while spelling, cast chimes, fizzles, bees, booms, quacks, countdowns and fanfares. Each mode has its own looping track (cozy for the house and tutorial, upbeat for Dibs!, bouncy for Moving Day) that crossfades between scenes. Sounds pan left and right by where they happen. **M** mutes. To use a recorded sound instead, return its clip from `Sfx.ClipFor`.

Not in yet: final art and fonts, character customisation, and the letter cat.

## Controls

| Action | Gamepad | Keyboard (left) | Keyboard (right) |
|---|---|---|---|
| Join | A / X / Start | Space or J | `.` or `/` |
| Move | Left stick / d-pad | WASD | Arrow keys |
| Grab / put down | A or RT | Space | `.` or Numpad 1 |
| Punch, use weapon, throw what you hold | X | J | `/` or Numpad 2 |
| Start spelling / cast | Y | K | Right Shift or Numpad 3 |
| Choose a letter (while spelling) | Stick or d-pad left/right | A / D | Left / Right |
| Jump 5 letters (Creative spelling) | Stick or d-pad up/down | W / S | Up / Down |
| Add the letter (while spelling) | A | Space | `.` |
| Undo a letter, or stop spelling if empty | B | J or Backspace | `/` |
| Drop the highlighted letter (while spelling) | Stick or d-pad down | S | Down |
| Use typewriter | A / RT (grab) | Space | `.` |
| Pick the Dibs! arena (at the typewriter) | Left / right | A / D | Left / Right |
| Start match (Dibs! lobby) | Start | Enter | Numpad Enter |
| Back to the house | Select / View | Esc | Esc |
| Mute sound | | M | M |

## Project structure

```
Assets/_Project/
  Scripts/
    Core/      GameAssets (shared art/data), World, Popup, Session
    Input/     InputBinding: keyboard halves, gamepads, scripted input for tests
    Letters/   LetterTile, TilePool, LetterBuilt + LetterShapes (Word World recipes), FurnitureCatalog,
               Furniture, LetterBlocks (labelled boxes), Smashable, LetterScores
    Words/     WordDatabase (reads word_list.csv), WordSolver
    Player/    PlayerController, PlayerHealth, LetterInventory, PlayerCombat, Summoner, PlayerHud
    Summons/   SummonEffects (what each word does), HeldWeapon, Projectile, BeeSwarm, DuckWalker, ...
    Game/      RoundManager (Dibs!), HubDirector + Typewriter (house), TutorialDirector, MovingDayDirector, FurnishFirstDirector,
               CreativeDirector + CreativeDesk + RoomLayout (Home Sweet Home),
               PlayerJoinManager, RoomBuilder, DeliverySpawner, GameHud, CameraRig, BackToHub
  Editor/      PrototypeBuilder: generates the scenes, prefabs and materials
               LetterMeshBuilder: generates 3D letters A–Z from the font's distance field
  Tests/       Play mode tests for the core loop
  Data/        word_list.csv
  Prefabs/  Scenes/  Resources/  Materials/  Art/  Audio/
docs/          design doc, roadmap, workflow, asset log, contribution table
```

**Adding a Moving Day item or level:** levels and checklists live on the MovingDayDirector (set in `PrototypeBuilder.BuildMovingDay`). Any word works as a checklist item. To give it a custom look, add it to `FurnitureCatalog.cs`.

**Shaping an object:** add a recipe to `LetterShapes.cs`. Each letter gets a centre, a size, whether it stands up or lies flat, and optionally its own colour (a brown trunk under green leaves). Give it a weight and colour in `FurnitureCatalog.cs`, and add the word to `word_list.csv` as `Furniture`. Tests check that every object word has a design that builds at a sensible size and stays standing.

**Adding a Dibs! arena:** add a `Build...` method next to `BuildKitchen` in `PrototypeBuilder.cs` with a room style, a furniture list and themed delivery words. Then add the scene to the build list and to the DIBS! entry's `maps` in `Typewriter.cs`.

**Adding a word:** add a row to `word_list.csv`. It works straight away with a default effect for its category. For a custom effect, add a `case` in `SummonEffects.cs`.

**Regenerating the prototype:** **Wreckabulary → Rebuild Prototype** recreates the materials, prefabs and the `Hub`, `LivingRoom`, `Tutorial` and `MovingDay` scenes from `PrototypeBuilder.cs`. It overwrites those scenes, so once someone starts editing a scene by hand, change the builder or stop using it.

**Adding a mode:** add an entry to the typewriter's mode list (`Typewriter.cs`, or in the Inspector on the Typewriter in `Hub.unity`) with its scene name, and add the scene to the build settings.

## Tests

**Window → General → Test Runner → PlayMode → Run All.** Or, from the command line:

```
Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults results.xml
```

`CaptureTests` is explicit, so it only runs when selected. It saves screenshots of the living room to `Temp/Captures`.

## Documents

- [Game design document](docs/GDD.md)
- [Roadmap](docs/ROADMAP.md)
- [Team workflow](docs/CONTRIBUTING.md)
- [Third-party assets](docs/ASSETS.md) (required in the final report)
- [Contribution table](docs/CONTRIBUTIONS.md) (required in the final report)

## Team

| Name | Student ID | Role |
|---|---|---|
| | | |
