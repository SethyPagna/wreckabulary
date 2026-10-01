# Game readiness — current verified milestone

The standalone HTML edition is playable. The Creative Workshop continuation adds
40 real furniture models, text furnishing, selection/movement/rotation/finishes,
undo/redo, per-map saves, strict portable JSON and peaceful tours. Existing game
modes keep their authored maps, AI, objectives, gear skills and letter economy.

| Deliverable | Actual state |
| --- | --- |
| Standalone HTML ZIP | Built, CRC checked, final bundle hashes match; five extracted-archive real Chromium scenarios pass without errors or external requests |
| Browser mechanics/UI | 88 domain checks, 25 combined actual-renderer scenarios and 14 focused DOM assertions pass; touch is Chromium emulation |
| Engine-free native rules | 192 checks pass, including shared home-layout fixtures |
| Native home files/history | Actual source passes 11 injected temporary-filesystem cases |
| Native C# assemblies | Six compile, zero errors; 34 existing runtime warnings |
| Native Unity import/tests/player builds | Not run: supported Unity Personal activation is missing on the cloud editor machine |
| Windows verification helper | Parser and 33 synthetic process/XML cases pass under PowerShell 7.4.19; actual Windows PowerShell 5.1/Unity unrun |
| Windows support | Exact official Mono module installed and integrity checked; no player build inferred |
| Android/iOS and physical device performance | Not run; Android modules absent, iOS needs Mac |

The HTML package is `Wreckabulary-HTML-Workshop-2026-10-01.zip` (18,385,248 bytes),
SHA256 `3f7d931501e93ef6049b8944f16b657c959aa29e16ec1112c0fa62c5745e078e`.
Extract and serve with the included PLAY-README instructions. Code and bundled
runtime art need no provider keys or runtime CDN.

[Workshop acceptance](CREATIVE_WORKSHOP_REVIEW.md) records scoped independent
reviews, fixed defects and source snapshots. [Visual review](../art/WORKSHOP_VISUAL_REVIEW.md)
shows actual supplied models, matching cameras and the saved ten-prop bedroom.
Its original art ratings remain critical; this is a stylized game, not a proven
photorealistic or completed all-platform release.

## Finish the native gate

On the editor machine, activate Unity Personal using supported Unity Hub login,
open this project with exact Unity 6000.6.3f1, then run the documented platform verification
workflow (`.\Tools\CloudSetup\verify-unity.ps1 -Build` on Windows). Setup refreshes actual imported clip/material references and tactile
shader seeds, without rebuilding authored scenes. Require successful real
EditMode/PlayMode XML with nonzero cases, then real player builds. Finally play
actual native portrait/landscape/controller input, all maps/modes and Workshop
save/tour/return; profile real target hardware.

No credentials or license contents belong in chat or the repository. See
[cloud/platform verification instructions](../../Tools/CloudSetup/README.md) and
[Resume here](../progress/WRECKABULARY.md) for the exact commands and current gate.
The user handled the target branch overwrite; it is not part of this readiness work.
