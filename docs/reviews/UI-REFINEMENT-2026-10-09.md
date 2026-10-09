# Unity interface and lobby refinement

STATUS: IN PROGRESS

Baseline: `9d054bd` on `main`. Requested scope: transparent tile-only letter/hand tray; readable typography and recipes; working pause controls, persisted rebinding and reset; detailed minimap with unboxed labels; brighter lobby with icon/text navigation; selectable themes, wardrobe/accessories and per-recipe styles.

## Ownership

- Root: integration, lobby chrome, generated-art provenance, Unity screenshots/tests, Git checkpoints.
- UI: HUD, pause/settings integration, input lifecycle and responsive HUD tests.
- Controls/shop: stable bindings and shared controls panel; shop/ownership/wardrobe integration.
- Visuals: cached detailed minimap; themed stage and import settings.

Unity must run only after all Assets writers freeze. Preserve existing authored maps, local multiplayer, saves and free cosmetics. Theme illustrations are GPT-generated backgrounds; avatars and playable props remain native 3D content. No online service or time-limited event is claimed.

## Confirmed findings

- Previous pause Controls button opened help instead of editable controls.
- Binding IDs recreated on each launch prevented reliable saved remaps. Stable IDs and migration are implemented; native validation pending.
- HUD tray heading/background and small recipe cells obscured the requested transparent layout.
- Old lobby explicitly used dusk lighting, dark gradients and filled navigation buttons. Current scope supersedes that design.
- Three bright theme illustrations generated with the built-in GPT image tool: Sunroom Social, Candy Carnival, Lantern Festival. Runtime copies and import settings are being integrated.

## Verification

- Native EditMode: 315 passed, 0 failed (`Logs/ui-editmode.xml`, 2026-10-08 23:18 UTC).
- RulesHarness: 234 passed, 0 failed.
- Initial broad PlayMode run ended without a results XML; outcome is unverified.
- Follow-up source fixes address special font glyph initialization, dark-on-dark remnants, MovingDay furniture registration on the map, and settings blocking direct lobby device polling.
- Focused native PlayMode v2: 122/124 passed. All visual/layout, minimap, theme, shop ownership, mixed-finish and font cases passed. Controls follow-up v3 passed 13/14; the remaining simulated keyboard fixture created its keyboard before configuring background input, leaving it disabled in batch mode. Its creation order is corrected for the final rerun.
- First native UI journey passed and captured 18 actual frames at 1600×900, 2100×900 and 1280×720. Independent pixel review accepted the bright home themes and clear tray/map. It caught a distorted reused timer font and a shop miniature partly obscured by the panel; both were repaired and recaptured. Initial frames are preserved under `Logs/checkpoints/ui-first-capture-20261009`.
- Final full PlayMode regression: **319 passed, 0 failed, 15 skipped**, 411.504 seconds (`Logs/ui-full-playmode.xml`). This includes the repaired input fixture, timer mesh/atlas regression, native preview projection, mixed recipe finishes, pause/rebinding lifecycle and the existing gameplay suites. The 15 skipped cases are explicit capture/animation probes; the UI capture is executed separately.
- Final native capture passed: **18 frames**, 22.412 seconds. Reviewed frame06 shows the entire miniature clear of the shop panel; frame08 shows a readable timer. All three home themes, the recipe layouts and pause/settings screens passed pixel review.
- Windows build and packaged startup remain pending. Machine-readable evidence is saved with the final frames in `evidence/unity-ui-2026-10-09/verification.json`.

## Recoverable next action

Commit and push the native-verified feature checkpoint as SethyPagna without coauthor trailers. Build Windows, smoke-test packaged startup, preserve and restore known generated build churn, then complete this UI report and begin Winter House Party under the ongoing asset-expansion goal.
