# Creative Workshop acceptance record

The continuation adds furnishing from text with the actual 40 supplied models,
per-map saves, editing/history, portable validated JSON and peaceful tours in the
native source and standalone HTML game. Both editions share the schema and fixed
flat house shells in [the contract](../design/CREATIVE_WORKSHOP.md).

## Behavior and evidence

- C# rules: 192 passing checks; 21 shared portable-import fixtures, five exact
  batch outputs and two Unicode creation fixtures agree with JavaScript.
- Browser domain: 88 passing checks (40 mechanics, 43 home layout, five tour).
- Actual native HomeStorage/test source: 11 passing cases in an injected temporary
  filesystem .NET host. Application.persistentDataPath throws in that host;
  application initialization and Unity/platform persistence are not exercised.
- Six actual C# assembly boundaries compile with zero errors; runtime source has
  34 existing serialization/unused-field warnings. Official editor/template DLLs
  are used; template package versions can differ from project pins.
- Combined actual-renderer Chromium run: **25 scenarios passed, zero errors**.
  The subsequent rename history/preview correction passed 14 independent normal-UI DOM assertions and
  also passed the actual final extracted-bundle check; engine/renderer/style
  do not change in that correction.
- [Independent browser review](WORKSHOP_BROWSER_REVIEW.md): three findings fixed,
  22 renderer-isolated DOM checks and 25 actual WebGL checks on its recorded
  snapshot, plus the newer shadow teardown probe. These precede the final flat
  Workshop-shell change and are additional evidence, not final-suite counts.
- [Independent native source review](WORKSHOP_NATIVE_SOURCE_REVIEW.md): five
  findings corrected, exact source hashes recorded. The reviewer authored the
  material helper, but not the reviewed Workshop/storage/UI/tour paths.

The two browser findings were invalid-coordinate placement using a stale ghost
and cross-map Undo skipping a previously saved house. Native source review closed
phone target sizing, JSON overlap, pending Hub knockout recovery cancellation,
scaled title/status rows and stale labels after changing orientation. The owner
also corrected gamepad-only tour binding and jump submitting a selected Back button.

The final extracted HTML ZIP passed **five actual-renderer scenarios** from an
HTTP subdirectory: local asset loading, rename Undo/Redo, preview preservation
through rename and Place, furnishing/save/tour/return, and no page/resource errors
or external runtime requests. Its CRC and final bundle SHA256 comparisons pass.

## Native release gate

Unity exits198 before import without Personal activation. The current native
lifecycle and tour-input tests are authored and compiled, but not executed.
The storage fixture executes in the separate filesystem host described above.
Native rendering, physics, controller/touch interaction, File.Replace on target
platforms and builds remain unrun. Setup generates persistent tactile shader seed
assets from actual imported materials; their rendered/player behavior needs the
licensed editor gate. Verified Windows module installation is not a Windows build.

Browser interaction fixtures provision bounded scenarios and use real pointer,
keyboard and touch-emulated controls. Their success does not certify physical
phones, controller hardware, online multiplayer or a finished all-platform release.

The original source art and imported maps are preserved. Tactile procedural maps
add surface detail only to eligible materials, with explicit resource ownership.
No new 3D mesh generation or photorealistic-object claim is made for this slice.
See [the supplied-art audit](../art/SUPPLIED_ASSET_AUDIT.md) for the critical rating.
