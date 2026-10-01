# Independent native workshop source review

Delivery follow-up: the actual HomeStorage/test source subsequently passed all
11 cases in the separate injected filesystem host. Unity execution remains
unrun; fixture references below refer to that native execution gate.

Review scope: home persistence/history, imported decor placement, workshop UI,
peaceful tour input and restoration of the original Hub. The reviewer authored
the tactile material helper but did not author or edit the reviewed home runtime,
storage, UI or pure layout domain. Material quality/lifecycle is therefore a
separate implementation handoff, not an independent assessment here.

Status: **five reviewer findings fixed and rechecked in source**, plus two
controller-tour corrections from the owner. No remaining concrete source issue
was identified in the reviewed paths.
Unity import, UI interaction,
physics, rendering and player builds remain **UNRUN** because editor activation
has not changed. Source closure is not native runtime acceptance.

## Findings and source closure

| Finding | Original trigger/evidence | Current correction |
| --- | --- | --- |
| Portrait toolbar targets too small | Entire 1024-unit header row was scaled to phone width. At a 391×844 viewport, Save/Tour/Back height was `46×391/1024 = 17.6` viewport pixels. | `CreativeWorkshop.LayoutUi` stacks portrait title, name and action rows. The action target accounts for Canvas scale and reported density. Landscape height also compensates for fitting. Rechecked source layout, not a native screenshot. |
| Portrait JSON buttons overlapped | With the same CanvasScaler, logical width was approximately 653.4 and JSON panel width 614.2. Apply ended at x482 while the right-anchored Close began at x446.2: 35.8 units of overlap. | JSON actions now divide available panel width into equal thirds with explicit margins/gaps, and move the status line above their target height. Rechecked anchor/size arithmetic. |
| Returning from workshop could strand a knocked-out Hub player | Deactivating the Game root stopped `PlayerJoinManager.RespawnLater`; merely reactivating it did not restart the pending coroutine. | Capture keeps the join-manager service root active and disables its behaviours, while hiding renderers/colliders/Canvases and freezing retained rigidbodies. LateUpdate reapplies hiding/freezing after a retained coroutine may reset a player. BodyPause preserves a completed life transition's spawn and clears old knockout momentum. A real open-during-KO reproduction remains required. |
| Final portrait title slot does not follow scaled text | At 320×568 with reported dpi160, the Workshop CanvasScaler gives scale≈.444, unit≈2.25. Portrait title font was `26×unit = 58.5`, but its rectangle remained height44 and the name row started at54. The tour title had the same fixed slot. | Title rectangles now use `44×unit` and subsequent title/name/action offsets scale together. JSON title/status heights scale too; its input reserves the top title and bottom status/action regions. Rechecked source equations. No native clipping screenshot is claimed. |
| Cross-orientation finish/sound labels could show stale state (P3) | Change Item finish or Sound in one FrontDoor sheet, then rotate the viewport. Their callbacks refreshed only that sheet's local button; map/look alone were synchronized across both sheets. | Both callback copies now call RefreshLabels, which updates both finish labels from skinIndex and both sound labels from GameFeedback.Muted. Rechecked source. Native observation unrun. |

The owner also added explicit `TactileMaterials.Release` before abandoned inactive
stages and previews are destroyed. This avoids relying on a never-enabled material
marker's OnDestroy. The material handoff and its three native lifecycle tests are
separate evidence; those tests have not executed.

## Reviewed contracts

### Isolated scene and restoration

`Session.OpenWorkshop` creates a disposable overlay in the existing Hub without
changing Session.MapId, Bindings, match mode or stars. `CreativeWorkshop.Open`
rejects another active workshop and rejects entry from a non-Hub scene. Failure
calls the same restoration/close path as normal exit.

Capture records each original root's activeSelf, each suspended component's enabled
state, retained renderer/collider/Canvas enabled flags, rigidbody kinematic,
gravity and collision flags plus linear/angular velocities, main-camera position/rotation,
rect/projection/size/FOV, timeScale, selected UI object, and touch enabled/overlay
settings. Restore uses those snapshots rather than enabling everything. Original
player bindings are retained; no Session.Remember/Match reset is performed by the
workshop. Tour is deactivated before original roots resume, removing its disposable
player from World.Players before those originals re-register.

BodyPause freezes retained bodies with isKinematic=true and detectCollisions=false.
It restores saved flags/velocities when no health-life transition occurred. For a
scheduled knockout recovery it keeps the new position/life and restores a dynamic
body only when the player is no longer held, with zero old knockout momentum.
LateUpdate skips after restoration, so it cannot re-hide restored originals.
The current serialized Hub was inspected directly: camera and directional light
have separate roots; players, Game, HUD, Room and furniture also have separate
roots. Retained service roots now hide renderer/collider/Canvas descendants, so the
fallback grouped-root case also avoids visible/collidable old scene geometry.
The native event system and input module remain available for workshop UI while
original gameplay scripts pause.

### Responsive controls and controller reachability

FrontDoor has a separate portrait ScrollRect, rather than scaling the full
landscape postcard. Its vertical layout scales row heights and text together;
all mode/map/wardrobe/workshop/sound/explore actions retain their callbacks. A
safe-area change switches sheets and selects an active control. Controller
selection changes move the scroll content enough to reveal the chosen row and
clamp its offset to the content bounds.

Workshop reflows the entire tool dock: row groups retain horizontal alignment,
control heights have a 48-unit minimum before density/Canvas scaling, and content
height, text and dropdown option slots scale together. Its controller selection
uses the same bounds-to-viewport scroll correction. Header/tour action buttons
and title rows have responsive heights/offsets that scale with their text.
JSON actions use equal thirds of the current sheet with margins/gaps. Source
arithmetic and call ordering were checked; actual navigation, soft-keyboard,
safe-area and device usability are still native acceptance tasks.

The owner additionally corrected tour controller entry and submit behavior:
with no joined/persisted binding, an available current gamepad supplies the tour
binding; tour clears EventSystem selection so gamepad A can jump without also
submitting Back to workshop. START/B returns to design and restores an
interactable design control. Source paths were rechecked; controller execution
is unrun.

### Validation, import and history

Clipboard text goes through `HomeDesigner.Import` before any visible/document
replacement. This requires exact schema-1 keys, the two known map IDs, valid
catalogue/model words and skins, finite half-metre coordinates and quarter-turn
yaw, unique IDs, at most 64 props, valid room footprints and reserved door/spawn
space. Malformed import returns errors without assigning a partial layout.

Imported rendering is preflighted before committing a target map's history.
Snapshots are detached clones. Rejected edits do not consume history; successful
new edits clear the redo branch, unchanged edits retain it, and history is bounded
to the current snapshot plus the latest 100 edits. A failed staged render keeps
the prior displayed decor; failed history navigation rolls its cursor back.

Pure domain behavior and C#/JavaScript parity are owned by root's rule validation;
this review checks native call ordering and data ownership rather than claiming
their tests ran here.

### Files and failure behavior

HomeStorage validates before touching disk. It writes a unique temporary file in
the destination directory, then uses File.Replace for an existing save or
File.Move for a new one. Exceptions return an error and clean up the temporary
path. Filenames depend only on the allowlisted map ID, so the home name cannot
change the directory or path. Export files are separate from active per-map saves.
Load bounds file size, parses/validates the complete document and confirms its map
matches the requested save. It never returns a partial layout on rejection.

Current native storage/history fixtures contain 10 test methods / 11 authored
cases, covering per-map round trips, invalid-save
preservation, replacement without changing another map, malformed/mismatched files,
path rejection, snapshot detachment and undo/redo behavior. These fixtures are
**UNRUN** until licensed Unity tests execute. File.Replace and persistence behavior
on Unity Web/mobile targets still require their actual runtime gate.

### Geometry and peaceful tour

Decor spawns through ModelVisual with the catalogue model key and selected skin.
The authored import rotation and full scale are preserved. The measured model
footprint is centered and its lowest point grounded at the room floor. The static
box proxy uses the same minimum .4 m X/Z footprint used by the pure validator,
and parent yaw rotates it into the corresponding quarter-turn footprint.
Decor receives no Smashable, HeldWeapon, delivery, summon or damage-trigger behavior.
RoomBuilder's geometry-only path does not populate match furniture or extras.

Tour saves and reads back a validated home before showing it. It instantiates a
disposable roommate, disables combat, summoner, loose inventory, health updates
and its player HUD. The tour binding has a distinct `home-tour:` ID; source touch
overlay input is merged before movement/aim/jump filtering, preventing the normal
PlayerController overlay merge from reintroducing attacks/crafting/grabbing.
Only movement and jump touch controls are presented. Ending tour releases touch
state and restores design controls; exiting the overlay restores the Hub snapshot.

The new HomeTourInputTests source contains three focused cases: unfiltered combat
commands cannot reach a peaceful player, a desktop touch overlay merges before
filtering and cannot merge again using the distinct tour ID, and a direct touch
source can move/jump while craft/dodge remain absent. These tests are authored
evidence and remain **UNRUN** in Unity.

## Native acceptance still required

- Open/close with previously inactive roots, disabled behaviours and non-default
  camera/touch state; assert those exact settings and original bindings return.
- Open during a scheduled Hub knockout respawn, then return after its deadline.
- Import malformed/unsafe JSON and a valid different-map home; verify documents,
  history and visible geometry change only after successful validation/staging.
- Exercise undo/redo, per-map save/load, clipboard copy/paste and export-file errors.
- Place rotated narrow/large props at door/spawn boundaries and walk every route.
- Test desktop/controller and simultaneous touch movement/jump; hold combat/craft
  inputs during tour and verify no action, letter/HP or match-state mutation.
- Capture actual portrait/landscape layouts with safe areas and soft keyboards;
  verify usable targets, no overlap/clipping and reachable scrolling controls.
- Run current EditMode/PlayMode suites and produced native/Web players after setup;
  verify platform file persistence and tactile shader variants in rendered output.

The last licensing inspection and exact supported commands are in
[cloud setup](../../Tools/CloudSetup/README.md). No editor retry, activation
workaround, network request, Git mutation or heavy build was performed in this
review. Root owns final integrated compilation and native acceptance.
