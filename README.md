# Wreckabulary

**Wreck the room, build the word!**

![Key art](docs/art/key_art.jpg)

Wreckabulary is a 2–4 player couch brawler for COMP4122. Smash furniture into letter tiles, grab the letters, and spell them into weapons and gadgets to knock your friends out. Everything in the room is made of letters, including what you summon, so every object can be smashed and re-spelled.

## Core loop

**Smash → Scavenge → Spell → Summon**

1. **Smash** furniture and it bursts into the letters that spell it.
2. **Scavenge** letters. You carry up to 6, and getting hit knocks some loose.
3. **Spell** with the word wheel, which shows what your letters can make.
4. **Summon** the object instantly, like a BLADE, WINGS or BEES.

## Win condition: Last Word Standing

Your letters are both your ammo and your health. A hit knocks letters off you, and a hit while holding no letters is a knockout. Last player standing wins the round, and the first to 3 rounds wins the match.

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
