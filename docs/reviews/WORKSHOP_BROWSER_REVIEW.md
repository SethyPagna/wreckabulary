# Independent Creative Workshop review — completed

**Result: no outstanding defects in the reviewed scope.** W1, W2 and the subsequently identified W3 were corrected and independently retested. The whole-game interaction suite and art comparison belong to the parent agents; this review does not substitute for Unity/device execution or a broad gameplay release claim.

Read-only review of `docs/design/CREATIVE_WORKSHOP.md`, `Web/src/home-design.js`, `Web/src/main.js`, `Web/src/engine.js`, `Web/src/renderer.js`, and relevant tests. Reviewer authored only the independent materials helper, not editor/gameflow or renderer integration. No repository files changed during review.

## Findings and resolution

### W2 — cross-map import undo skipped an existing saved target house — resolved

Original location: cross-map import target-session initialization in `Web/src/main.js` (formerly lines 835–847). The import handler used an empty `createLayout(map)` instead of loading the target map's persisted layout.

Original reproduction: save a courtyard house with TABLE and BALL; reload; open pinwheel only; import another courtyard document; Undo restored `My Cozy House` with zero props rather than the existing saved courtyard. Saving that undo state could then replace the saved document.

Resolution: both opening the Workshop and cross-map import use `loadWorkshopSession` before appending import history. Independent DOM retest confirms Undo restores the complete previously saved target-map name and props. Invalid cross-map imports still leave current map, design, selection, ghost and history unchanged.

### W1 — blank position could fall through to a stale ghost commit — resolved

Original location: `updateDesignGhost` and `applyDesignGhost` in `Web/src/main.js`. A blank/nonfinite coordinate returned an error, but the caller continued using the preceding valid ghost position.

Resolution: invalid input returns false and applying stops. Independent DOM retest confirms no prop is committed from an empty position field. The real renderer also marks unsnapped positions invalid and preserves the design when placement is attempted.

### W3 — name-only changes left stale history buttons and discarded pending previews — resolved

Location: `commitDesign(candidate, message, refresh = true)` in `Web/src/main.js`, the `refresh=false` path used by house-name blur. State/history updated but Undo/Redo disabled attributes stayed stale. The same path also reset a pending new preview before skipping the inspector redraw, leaving visible Place controls with no active ghost.

Baseline reproduction through actual UI: open an empty Workshop; rename to `Sunbeam House`; blur the input. Layout name changes, history index becomes 1 and dirty state is true, but Undo is still disabled. Evidence: `workshop-independent-name-history-baseline.json`; main source SHA before/after matches. Resolution: the `refresh=false` path updates both history-control disabled properties and preserves pending ghost/invalid-input inspector state; the reset belongs to `refresh=true`. Browser integration agent applied the fix and added future actual-renderer assertions. Independent focused verification completed with **14 passing normal-UI checks**, zero page errors, and unchanged before/after main SHA256 `acff1577173618fe0acd7f5d911f47e58172ede0558316d51d2de72bb0dddac2`. Evidence: `workshop-independent-name-history-check.json`; a temporary isolated-browser harness; the permanent future actual-renderer regression is in `Web/test/workshop-browser.mjs`. It verifies rename → blur → Undo/Redo, new-name history branching, normal Save and reload, BOOK preview surviving a name change and actually placing, and blank coordinates remaining invalid/refusing placement after a rename. This uses real editor/engine modules with an external renderer stub; no redundant heavy graphics run was made or claimed.

## Gameflow validation

`workshop-independent-gameflow-check.json`: **22 passing Chromium DOM checks**, no defects or page errors. Actual `main.js`, `home-design.js`, `engine.js`, data and UI controls execute with a renderer stub to isolate state transitions. This isolated check is additional to the subsequent actual WebGL audit, not a rendering claim. Source SHA256 before/after matches; Vite HMR was disabled for the final run.

Verified: all 40 catalogue choices; furnishing from text; exact undo/redo snapshots; redo branch invalidation; duplicate-key/overlap/invalid cross-map import atomicity and visible errors; independent pinwheel/courtyard saves and reloads; cross-map Undo restoring its previous persisted house; storage failure blocking Tour and reporting failure; peaceful Tour with one human, stable health/decor and no economy/hazards; return preserving the exact design.

Independent engine-free checks additionally confirmed each of the 40 models can furnish the courtyard Garden individually, a 64-BALL layout is valid, and 65 props are rejected. At that earlier review snapshot, all **39 home-design tests** pass, covering portable fixtures, whole footprint/rotation/door/spawn geometry, source snapshots, duplicate keys, bounded text/JSON, finite grid coordinates, and deterministic placement. At that snapshot, all **4 Tour tests** pass, including full rotated decor collision footprints and ordinary matches ignoring workshop layout options.

## Actual renderer validation

`workshop-independent-renderer-check.json`: **25 passing real Chromium WebGL checks**, no defects, page errors or console errors. No renderer stub is used. All 40 supplied item GLBs are present in the actual loaded model cache (75 total loaded models including letters, environment and avatar).

Verified with actual UI/geometry: model preview and coordinate placement; saved quarter-turn orientation; raycast resolving the prop ID; real canvas click selecting the same inspector object; invalid placement ghost; rejected placement preserving the saved layout. Editor whole-house bounds and reachable header/camera controls pass at 1280×800, 390×844 and 844×390. Captures: `workshop-independent-desktop.png`, `workshop-independent-portrait.png`, `workshop-independent-landscape.png`.

**Viewport limitation:** portrait/landscape checks resize a desktop Chromium context. They establish CSS/layout/camera fitting and hit-target reachability; they do not establish true touch/coarse-pointer rendering, native mobile execution or real-device speed.

Same-map rebuild checks verify a stable prop ID whose word changes replaces the GLB node; unchanged word/ID edits reuse the node while updating position, rotation and finish; changed saved IDs cannot retain stale selection identity; removed decor leaves both entity and pick registries. A real model ghost and selection outline coexist and are removed when leaving the editor.

### Resource lifetime

Eight cycles each alternate four actual gameplay/map/Tour scenes. Counters stabilize identically every sampled cycle: **165 GPU geometries, 169 GPU textures, 17 programs**, 210 owned palette materials and 114 generated palette textures. Imported source textures and shared palette textures emit **zero** dispose events during world rebuilds and ghost/selection churn.

At final teardown, **all 90 imported source texture objects** and **all 63 generated palette texture objects observed at audit start** emit exactly one dispose event. Calling `view.dispose()` again emits none. Later-created generated maps are owned by the same independently tested palette lifecycle; the observer count is not mislabeled as the full final palette size.

`workshop-independent-shadow-teardown.json` is a separate small probe against the newest renderer source after explicit per-light shadow cleanup was added. It allocates an actual shadow render target and environment map, renders, and disposes twice: the shadow target is disposed exactly once; GPU geometries and textures both return to **0**; source geometry/material/texture sets are empty and `scene.environment` is null. This checks the latest shadow cleanup without repeating the heavy scene audit.

## Scope limits

No native Unity, physical mobile/controller, production archive or entire competitive gameplay verification is inferred from this review. Parent agents own their complete interaction suite, build checks and visual comparison. The rendered captures are inspection evidence, not reference-art or speed claims.
