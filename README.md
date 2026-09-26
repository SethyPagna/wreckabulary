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
| Engine | Unity 6 LTS (6000.0.x) |
| Render pipeline | URP |
| Input | Unity Input System, 4 controllers |
| Physics | Active ragdoll characters, pooled rigidbody letter tiles |
| Platform | Windows PC, local multiplayer |

## Getting started

1. Install **Git LFS** once: `git lfs install`
2. Clone the repo: `git clone <repo-url>`
3. Open the folder with **Unity Hub → Add project from disk**, using the Unity version above.
4. Open `Assets/_Project/Scenes/LivingRoom.unity` and press Play.

First-time setup (one person only): create a new URP project in Unity Hub, copy this repository's files into it, commit `Assets/`, `Packages/` and `ProjectSettings/`, then push. Everyone else just clones.

## Controls (planned)

| Action | Controller | Keyboard |
|---|---|---|
| Move | Left stick | WASD |
| Grab / pick up | RT | Space |
| Attack / use | X | J |
| Spell (open word wheel) | Y (hold) | K (hold) |
| Choose word | Right stick | Arrow keys |

## Project structure

```
Assets/_Project/
  Scripts/
    Letters/   LetterTile, TilePool, Smashable, LetterScores
    Words/     WordDatabase, WordSolver
    Player/    LetterInventory, Summoner, PlayerHealth
    Game/      RoundManager
  Data/        word_list.csv
  Prefabs/  Scenes/  Art/  Audio/  Materials/
docs/          design doc, roadmap, workflow, asset log, contribution table
```

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
