# Wreckabulary

**Wreck the room, build the word!**

![Key art](docs/art/key_art.jpg)

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
3. **Spell** with the word wheel, which shows what your letters can make.
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

**The house (hub):** roommates join by walking in through the front door. Pick a mode at the typewriter: Tutorial or Dibs!. Moving Day and Home Sweet Home are marked "coming soon". Whoever is in the house comes along into the mode, and everyone heads home after a match or on Esc / Select.

**Tutorial:** 7 steps covering walk, smash the BAT box, pick up the letters, spell BAT, whack the dummy, throw the chair, and knock the dummy out. Any roommate can complete a step.

**Dibs!** in the living room, first to 3 rounds:

- Smash furniture (SOFA, TABLE, LAMP, CHAIR…) with punches or by throwing things. It bursts into its own letters.
- Walk over letters to pick them up. You carry up to 6, and you start each round with 3.
- Hold spell to open the word wheel. It shows the words you can make, plus near misses in grey. Release to summon.
- All 20 words in `word_list.csv` do something: weapons, WALL, SHIELD, ARMOR, WINGS, SPRING, SKATES, ROPE, BEES, FLOOD, MAGNET, DUCK, QUAKE, ZAP.
- Summons fall apart back into their letters when they're used up, so they can be grabbed and re-spelled.
- A hit knocks 2 letters loose. A hit with no letters is a knockout.
- Delivery boxes drop in when the room runs low on letters. After 90 seconds the room collapses and boxes rain down.

Everything is placeholder art built from blocks. Not in yet: audio, character customisation, Co-op (Moving Day) and Creative (Home Sweet Home).

## Controls

| Action | Gamepad | Keyboard (left) | Keyboard (right) |
|---|---|---|---|
| Join | A / X / Start | Space or J | `.` or `/` |
| Move | Left stick / d-pad | WASD | Arrow keys |
| Grab, throw | A or RT | Space | `.` or Numpad 1 |
| Attack, use weapon | X | J | `/` or Numpad 2 |
| Spell (hold, release to summon) | Y | K | Right Shift or Numpad 3 |
| Choose word (while spelling) | Stick or d-pad up/down | W / S | Up / Down |
| Cancel spelling | A | Space | `.` |
| Use typewriter | A / RT (grab) | Space | `.` |
| Start match (Dibs! lobby) | Start | Enter | Numpad Enter |
| Back to the house | Select / View | Esc | Esc |

## Project structure

```
Assets/_Project/
  Scripts/
    Core/      GameAssets (shared art/data), World, Popup, Session
    Input/     InputBinding: keyboard halves, gamepads, scripted input for tests
    Letters/   LetterTile, TilePool, LetterBlocks, LetterBuilt, Smashable, LetterScores
    Words/     WordDatabase (reads word_list.csv), WordSolver
    Player/    PlayerController, PlayerHealth, LetterInventory, PlayerCombat, Summoner, PlayerHud
    Summons/   SummonEffects (what each word does), HeldWeapon, Projectile, BeeSwarm, DuckWalker, ...
    Game/      RoundManager (Dibs!), HubDirector + Typewriter (house), TutorialDirector,
               PlayerJoinManager, RoomBuilder, DeliverySpawner, GameHud, CameraRig, BackToHub
  Editor/      PrototypeBuilder: generates the scene, prefabs and materials
  Tests/       Play mode tests for the core loop
  Data/        word_list.csv
  Prefabs/  Scenes/  Resources/  Materials/  Art/  Audio/
docs/          design doc, roadmap, workflow, asset log, contribution table
```

**Adding a word:** add a row to `word_list.csv`. It works straight away with a default effect for its category. For a custom effect, add a `case` in `SummonEffects.cs`.

**Regenerating the prototype:** **Wreckabulary → Rebuild Prototype** recreates the materials, prefabs and the `Hub`, `LivingRoom` and `Tutorial` scenes from `PrototypeBuilder.cs`. It overwrites those scenes, so once someone starts editing a scene by hand, change the builder or stop using it.

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
