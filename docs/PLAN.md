# Wreckabulary production plan (merged, 5 October 2026)

This is the one current plan. It merges three sources, in this order of priority:

1. **The creator's master plan and chat history** (5 Oct). Identity, quality rules, evidence rules and milestone gates.
2. **PC Game Plan Revision 2** (1 Oct). Tool set, crafting timings, destinations, camera, depth systems and production stages.
3. **The earlier production plan** (D1–D23, 27 Sep to 1 Oct). 100 HP, modes, Unity code structure and tests.

Where they disagree, the creator's later decisions win. They are listed in section 2.
Older documents (the brief, GDD, ROADMAP and earlier PLAN text) are history. Use them only where this plan points to them.

## 1. The game in one paragraph

Wreckabulary is a warm, tactile third-person house game for 1–4 players.
- Plush roommates smash real household furniture.
- The furniture breaks into its exact wooden letters. Players deliberately pick those letters up and type a word to build a useful object. A BAT swings, a PLATE blocks and a BED springs.
- They use these to fight (Brawl), to work together (Co-op Jobs), or to decorate and experiment at home (Home Studio).
- Success comes from timing, letter choices, movement and teamwork, not from memorising a dictionary.
- A small set of polished, physically readable objects beats a large set of shallow ones.

## 2. Decisions in force

| # | Decision | Source |
| --- | --- | --- |
| C1 | **The next slice is the house game.** Brawl and Co-op Jobs come first. The Photograph Room is kept as a later Home Studio experiment; it is neither cancelled nor merged into the house game. | Creator, 5 Oct |
| C2 | **The bag holds 10 loose letters**, down from 18. Each player also has 2 carried items and 2 deployed items, and starts with 100 HP. Matches start with an empty bag. | Creator, 5 Oct (10); D-plan (the rest) |
| C3 | **One unified handling map**, set out in section 6. E is the contextual hand. Hold right mouse to aim, then left mouse to throw. Q opens the typed composer. | Master plan section 6; creator, 5 Oct |
| C4 | **The world stays live while typing.** Typing takes the movement keys, but physics, momentum and knockback continue and the camera can still look around. After you confirm, the build channel runs at 40% movement. Nothing freezes. | Master plan; Revision 2 |
| C5 | **Health, not letters, is the life bar.** Health is 100 HP, with downed, revive and bleed-out in team modes. | Earlier decision |
| C6 | **Unity 6 is the main native edition.** It lives in the `SethyPagna/wreckabulary` fork and is PC first, Android later. The browser edition (Three.js, `Web/`) shares the JSON data and stays playable, but has its own tests. | Earlier decisions |
| C7 | **Assets: reuse the supplied art first.** That is 98 models: the 22-bone avatar with 17 clips, 40 items, 26 letters, 21 environment pieces and 10 VFX. New assets may be generated with Higgsfield where something real is missing. Each generated asset needs editable source, scale, pivot, collision, rig and a provenance record. | Creator, 5 Oct |
| C8 | **Commits are made in batches**, as SethyPagna only, with no co-author lines. Push only when the creator says so. | Creator |
| C9 | **Evidence stays with its edition.** A browser pass does not certify Unity, Windows does not certify mobile, and emulated touch does not certify a phone. "Built" is never claimed without a run of that exact build. | Master plan section 8 |

**Disclosed defaults.** The creator can overturn any of these.
- The local couch game keeps the shared couch camera.
- The 2c-1b gamepad layout remains, re-aligned to C3: A jump, X primary, B dodge, RT hand/revive, LT aim/guard, Y compose.
- Typing a word uses the keyboard. Gamepads choose the word from a known-word list.
- VACUUM needs an authored source of the letters C, U, U and V in the house; see section 4.

## 3. Shared rules (both editions)

**Materials**
- Recipes count letters exactly: BALL needs two L tiles and VACUUM needs two U tiles.
- Each letter tile and item has exactly one identity, owner and state. Picking up, dropping, throwing, building, breaking, being knocked out and resetting all move that state; none of them copies it.
- Shards and sparkles are visual only. They are never resources.
- The bag limit (10) also counts letters reserved for a build in progress and letters stored in a VACUUM hopper. Letters already inside a built object belong to that object.

**Building an object**
- The preview shows the cost, the missing letters and where the object will appear. It reserves nothing.
- Confirming reserves the letters. The channel takes **0.6 s + 0.12 s per letter**, at 40% movement.
- When the channel finishes, the letters are spent once.

**Refunds and spills**
- Cancelling, being interrupted or an invalid placement returns the reservation once.
- When a player is knocked out, the letters they hold spill once.
- Breaking a reusable object returns its current recipe letters once.
- Consumables spend their letters for good. An expired effect or an empty casing refunds nothing.

**Slots and cosmetics**
- A full slot never silently destroys or replaces an item. Destination, capacity and owner are checked both when the build is requested and when it completes.
- Cosmetics never change damage, collision, reach, timing, capacity or movement.

**Rewriting** (Home Studio first)
- "New" builds from the bag. "Rewrite" locks one existing object and reserves only the letters it is missing.
- When it completes, the surplus letters are released visibly.
- Damage and cooldowns carry over. A rewrite never heals an object or resets a cooldown.
- The channel uses the longer of the two words.

## 4. Catalogue

**The polished eight.** These are the launch focus from Revision 2. Each must have a distinct physical decision.

| Tool | What it does | Notes |
| --- | --- | --- |
| BAT | Charged impulse swing with knockback | Clear grip, arc and recovery. BAT material pass first (no credits). |
| BLADE | Cutting melee with little shove. A thrown blade lodges in place. | Will later cut ROPE. |
| BALL | Ricochet throw that can be caught and passed | Throw, release, catch and impact all share one state. |
| PLATE | Brittle front shield, already in Unity (2c-1b). A thrown plate shatters in a 2 m ring and leaves a shard patch for 3 s. | The pale-contrast art issue needs fixing. |
| BED | Spring pad, or a wall when stood up | Launch strength is limited. It has a cooldown. |
| SOAP | 8 s slippery patch, or a coating | It is consumed; no repeat refunds. |
| FOAM | 35 points of padding for 10 s | Damping, not launching. |
| VACUUM | Hold to inhale letters and small objects into a 5-cell hopper, release to spray them | The hopper counts toward the bag. A melee hit spills it. **Needs a model (Higgsfield) and a C/U/V source in the house**, for example an appliance cupboard that breaks into those letters. |

**Kept, but not polished yet**
- **TABLE** stays as cover and furniture for Moving Day, the Workshop and the Photograph Room.
- **BOMB, LAMP, MAT and SOFA** stay enabled in data until the eight pass their gate, then are disabled until each is reworked.
- The first new recipe candidate is **ROPE**.

**Ideas bank** (not scheduled)
- BELL; CUP as a team container; RUG as concealment; FAN as a fixture; TAPE as repair only; POT, folded into the Mixer Box; MOP; and the meeting-note items.
- FORK hidden-inventory theft and CLOCK resource rewind are excluded.

## 5. Destinations, modes and maps

**Destinations**
- **Brawl**
  - Dibs: free-for-all, last one standing, first to 3 rounds, with the Movers closing rooms.
  - Duos: 2v2 with downed and hold-to-revive.
- **Co-op Jobs**
  - Moving Day: four furnishing objectives per map.
  - Moving Out: get the keepsakes to the van, with everyone left alive at extraction.
- **Home Studio**
  - Creative Workshop: already built in the browser edition.
  - Photograph Room experiment: later. TABLE, BED, BAT, BALL and BELL, an 11-letter kit and the camera incident from the master plan, section 3.
- **Support**
  - Practice (Tutorial).
  - Wardrobe: customisable avatar and cosmetic skins only.
  - Progress: later.

**Maps**
- **Pinwheel House**: the Laundry Lift comes later.
- **Garden Courtyard**: the Turning Conservatory comes later.
- **Skyline Balance Deck**: later.
- Each map gets one distinctive physical feature. The rest of the map bank is in master plan section 5.

**Later progression** (after the slice is accepted)
- Focus / Letter Echo, Stitches (cosmetic currency) and mastery.
- No paid shop and no stat rewards.

## 6. Controls and camera

**PC.** The same action means the same thing everywhere. A new key needs a new mechanic.

| Action | Key |
| --- | --- |
| Move / look | WASD / mouse |
| Primary (use the item; smash furniture bare-handed) | Left mouse; hold it to charge |
| Aim or guard | Hold right mouse. With a throwable item: left press, hold and release throws; release right mouse first, or press Esc, to cancel. With a PLATE: guard. |
| Hand (contextual) | E: tap to store the letter you're looking at, grip, pick up or set down; hold to revive. Hold E to place, turn the wheel to rotate, left mouse to confirm, right mouse or Esc to cancel. |
| Compose | Q opens the typed composer. Enter confirms. Tab switches New/Rewrite (Home Studio). |
| Carried slots | 1 / 2, or the mouse wheel outside placement |
| Bag peek | Tab, outside the composer |
| Jump / dodge | Space / Left Shift |
| Drop | Hold R for 0.25 s. R no longer rotates; the wheel does. |
| Pause | Esc. It cancels the current mode first, then pauses. It no longer quits to the Hub. |
| Later | V tune, B Letter Echo, M route peek, middle mouse ping, C wardrobe |

**Input rules**
- Typing owns the keyboard.
- A modal opening or closing must never turn a release into an attack.
- Losing focus or resetting needs a fresh press.

**Gamepad and couch halves.** These keep the 2c-1b layouts, moved onto the same actions.

**Mobile (later)**
- Movement thumb, look region, Action, Aim, Hand and Craft.
- Chips for the letters you hold and a list of known words.
- 48 dp targets.
- Tested on physical phones.

**Camera**
- Solo and online use a close shoulder camera, 2.4 m behind and 1.4 m up, tested against walls, doors, clutter and held items.
- The local multi-seat couch game keeps a shared camera.
- The browser keeps its follow camera until the shoulder camera passes there too.

## 7. Art, animation and physics standard

**Look.** The look is a warm Toybox Workshop:
- plush bodies and large tactile wooden letters;
- readable walnut, ceramic, cloth and rubber.

**Motion**
- Both editions should use **all 17 clips**: Idle, Walk, Run, Jump, Pickup, Hold, Carry, Place, Throw, Swing, Thrust, Block, Drink, Hit, Celebrate, Inspect and Present.
- Clips crossfade into each other.
- Items attach to the hand bones (`Grip_R` / `Grip_L`).
- Posture is layered on top:
  - leaning into speed and turns;
  - look and aim on the upper body;
  - recoil when hit;
  - foot planting on stops;
  - a downed pose that reads clearly.

**Physics.** Walking, stopping, turning, slopes, doors, landings and spawning come before any fictional power. Collision should match the visible shapes.

**Acceptance.** To pass, an asset or motion needs:
- front, back, side and in-game captures;
- a continuous moving capture, not a single attractive frame;
- feet on the ground and the hand on the item;
- residual defects listed openly.

## 8. Milestones

Each milestone closes only for a named edition and commit, with recorded evidence. Unity milestones 1, 2a, 2b, 2c-1a and 2c-1b were done earlier (see the progress log).

| Stage | Contents | Gate (observable) |
| --- | --- | --- |
| **P0 Baseline and plan** | This plan. Capacity 18 → 10 in data, code, UI and tests in both editions. The Unity HUD tray follows the data limit. | Rules harness, Unity EditMode and browser mechanics tests pass with 10. |
| **P1 Motion and presentation** | Browser: all 17 clips, crossfades, hand-bone attachment, posture layer, downed fix, VFX loaded. Unity: crossfades, the clips it doesn't use yet, the posture layer, and a closer perspective camera for solo play. Unity grips keep their authored socket orientation, which the BAT attachment tests require, and an over-the-shoulder view needs camera-relative controls, so it moves to P2. | Captures from four angles plus in-game. No sliding feet. The item stays in the hand. |
| **P2 Unified controls** | The section 6 map in both editions: typed Q composer in Unity, E store/grip/place, RMB aim with LMB throw, 1/2, Tab peek, wheel rotate, Esc pause menu; an optional over-the-shoulder solo camera with camera-relative movement. | Unity input tests and browser flows. No stray attack after a modal closes. |
| **P3 Material journey** (was 2c-2) | Smash, exact tile release, deliberate E pickup, reserve and channel, cancel, refund and spill; capacity 10, 2 carried, 2 deployed; the interaction room (Revision 2 stage 1). | Normal, interrupted and failed paths. Each tile's identity balances. |
| **P4 The polished eight** (was 2c-3) | BAT, BLADE, BALL, PLATE, BED, SOAP, FOAM and VACUUM to the section 4 contracts, with their models. VACUUM is modelled with Higgsfield. | Per-tool tests and a capture of each tool in use. |
| **P5 Playable slice** | Dibs end to end (start, craft, fight, result, retry), then Duos, Moving Day and Moving Out. | An uninterrupted match plus a failed and recovered one, in a named edition. |
| **P6 Platform** | Windows package smoke (menu, play, retry), gamepads, local seats, a 60 fps target measured on the RX 6700S laptop. | Platform receipts with build hash, hardware and settings. |
| **P7 Depth** | Tunings (BALL +L, PLATE +P, BED +E, VACUUM +U), Mixer Box, Suds, map features, Letter Echo, Stitches, Home Studio in Unity, Photograph Room experiment. | Each feature gets its own gate. |
| **P8 Reach** | Android, then online play (listen server, LAN/IP). Real clients and real devices only. | Device and client receipts. |

## 9. Acceptance checklist (carried forward)

| Area | Required observable result |
| --- | --- |
| Crafting | Exact repeated letters spent; cancelling conserves letters; never over 10; no disabled recipe |
| Combat | One hit per target; friendly-fire rules; cooldown and revive reset correctly |
| Gear | Switch, use, drop, throw and deploy both slots; correct lifespan; refunds happen once |
| Modes | Solo can start; the objective progresses; win, loss and draw all end the match; retry resets |
| Maps | Doors traversable; spawns clear; danger telegraphed; camera readable |
| Art | Supplied meshes in play; all clips used where relevant; hand contact; wardrobe modules hidden correctly |
| Controls | Section 6 map; keyboard and mouse, gamepad, couch halves; touch later |
| Browser | Bundled dependencies; same JSON; browser flows with no console errors |
| Unity | Editor 6000.6.3f1; tests and builds recorded separately from source inspection |
| Authorship | SethyPagna only, no co-author lines; batched commits |

`docs/progress/WRECKABULARY.md` (in the private workspace) holds the evidence and the exact next step.
