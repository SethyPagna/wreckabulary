# Wreckabulary roadmap

The goal is a complete local house game: four modes with clear objectives and replay, a useful tutorial, two connected maps, coherent supplied art and responsive desktop/touch controls. The next creative step is a saveable home editor. [The production plan](PLAN.md) defines the scope; [the progress record](progress/WRECKABULARY.md) is the authority for current test results and blockers.

## Current implementation

| Area | Implemented in source |
| --- | --- |
| Shared rules | 100 HP, 18 letters, two gear slots, two deployed items, exact craft reservations and consumable accounting |
| Recipes | Twelve enabled catalogue recipes with authored combat, use and placement data |
| Maps | Pinwheel House and Garden Courtyard, with connected routes and shared objective data |
| Modes | Dibs, Duos, Moving Day, Moving Out, plus tutorial; AI supports solo starts |
| Presentation | Supplied furniture/letters/avatar, modular wardrobe, item skins, HUD, touch actions and sound/effect cues |
| Browser | Standalone bundled Three.js edition using the canonical JSON, mechanics tests and real Chromium checks |
| Unity workflow | Pinned 6000.6.3f1, current scene/prefab workspace, native regression suites and repeatable platform build entry points |

“Implemented in source” is not a passed platform release gate. The browser has one human with AI housemates; Unity has local roommate seats. Online play and a creative save/load editor are not implemented.

## Required release gates

| Priority | Work | Done when |
| --- | --- | --- |
| 1 | Maintain native regressions | Changes pass actual import, EditMode and PlayMode suites with saved results on the activated editor |
| 1 | Validate complete mode loops | Start, objective progress, win/loss/draw, next round, retry and home work on both maps; no crafting, slot or transient-state leak |
| 1 | Validate controls and navigation | Mouse/UI clicks do not also attack; keyboard/controller prompts agree; touch can move, aim, use gear, craft/cancel, select modes and exit every screen |
| 1 | Accept supplied art | All required meshes/materials/rig clips import correctly; exact-letter destruction, authored root rotations and grip contracts survive play |
| 2 | Finish avatar optimization | Use the accepted reduced browser export; verify Unity deformation/outfits and reduce renderer, material and scene cost as profiling requires |
| 2 | Profile representative devices | Record frame time, memory, loads and readability for four avatars and a crowded tile/effect scene on actual target phones and PCs |
| 2 | Produce tested builds | Linux/Windows PC, Unity Web and Android builds succeed with matching modules and run on their intended platforms; iOS requires a supported Mac workflow |
| 2 | Review visuals and balance | Gameplay captures show readable glyphs, targets, danger and doorways; both maps have useful routes and no dominant recipe or unwinnable objective |

The local **8–9 October 2026** presentation checkpoint has licensed Unity import, 187 EditMode passes, 132 latest PlayMode/capture passes, thirteen rendered screenshots and a successful Windows build/startup smoke. See the [native verification record](reviews/UNITY-PRESENTATION-2026-10-08.md) and [current authoring workspace](UNITY-WORKSPACE.md). The earlier cloud exit-198 activation failure is scoped to the 1 October environment. Other platform builds, physical input devices and mobile performance still need their own evidence.

The supplied avatar's reference outfit has 31,696 triangles. The accepted reduced browser export is 17,660 reference-outfit triangles and passes interchange/animation checks; native Unity animation and grip checks now have evidence. Further reductions and complete scene profiling remain separate work. Use the [critical asset audit](art/SUPPLIED_ASSET_AUDIT.md) and actual measurements to choose further reductions; do not convert a triangle count into an FPS claim.

## Creative home editor: next feature slice

This is an in-game feature. The developer-facing Unity scene and prefab authoring workspace already supports editing the current assets.

1. Add an explicit Creative mode with a curated prop palette and a connected house/garden editing space.
2. Provide snap, rotate, place, remove and undo on mouse, controller and touch.
3. Save/load versioned local room data, with clear reset/recovery behavior.
4. Validate routes, doors, spawns, clutter and camera occlusion before offering the edited room as an arena.
5. Add play-from-editor with a deliberate switch to the chosen mode's inventory and combat rules.

Acceptance is a complete create → save → reload → play → return loop that preserves object placement and remains traversable. Existing wardrobe persistence does not count as saved-room support.

## Expansion after those gates

Additional recipes and house themes, cooperative objectives, richer exploration, pets and online services are separate later work. Enable a new recipe only when its model, timings, effects, accounting and both runtime tests are ready. Keep cosmetics cosmetic and keep PC/mobile readability ahead of raw content count.
