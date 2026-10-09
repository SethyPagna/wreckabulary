# Interaction, feedback and stamina

STATUS: TESTS PASSED; WINDOWS BUILD PENDING

Scope: shared crosshair/pointer aim for E pickup and hand-origin throws, physical reach and obstruction, persistent damage/points switches, real dodge stamina beside health. Starting revision f2f60d7. Preserve preexisting TMP fallback and Lunar source changes.

Confirmed: old pickup selected the nearest body in a forward overlap sphere; old throws used horizontal facing regardless of camera pitch. Player health had no stamina resource. Damage text had no preference; career damage points accumulated without per-hit display.

## Changes

- E uses the reticle in third person and the pointer ray in cursor view. A 1.7 m chest-to-surface reach limit, first solid hit and chest obstruction check prevent remote or through-wall pickups. Full hands do not advertise unusable pickups. The prompt follows the rebound interaction key.
- Throws leave the current animated hand/carry pose, with gravity compensation toward the same ray hit. Free aim uses a point 12 m ahead. Targets beyond ballistic range receive aim direction, not guaranteed impact. Shared-view directional controls retain their prior behavior.
- Carry retracts before obstacles, including the initial-overlap case. Drops restore original collider/interpolation settings and separate full-size recipe furniture from floors/walls. Carrier collision suppression ends after physical separation.
- Separate saved damage and points switches live in lobby Settings and Pause > Gameplay, with reset. Damage reflects the amount actually applied. Points reflect existing career damage awards. Life-state callouts remain visible.
- Stamina defaults to 100; dodge costs 30. Recovery starts after 1 second at 20/second, retaining the existing 1.5-second dodge cooldown. Exhaustion blocks dodging. Pause/death do not bank recovery; respawn restores stamina. GameRules and optional balance JSON fields expose tuning.
- Mint STA text and a fill sit beside HP. Numeric popups reserve separate horizontal positions to avoid overlap and ceiling occlusion.

## Verification

21/21 health/stamina EditMode tests passed. Native runs passed 56/56, 39/39, 40/40 and 36/36. These overlapping runs cover 61 unique native tests, including existing controls, HUD, catalogue items and wall interactions. Raw evidence: Logs/interaction-*.xml and .log.

Nine actual Unity ScreenCapture frames cover reachable E targeting, held ball, hand release, simultaneous DMG/PTS with 70 STA, feedback ON/OFF, ultrawide and 720p. Visual inspection caught overlapping labels and vertical stacking through the ceiling; both were corrected and recaptured. This is self-review.

Fixtures verify off-facing reticle selection, pointer parity, reach/obstruction, elevated ballistic convergence, hand origin, original physics settings, carrier collision release, close-wall carry and expanded furniture drops. This change exercises the existing local/couch runtime; remote transport is outside its verification scope.

Unity documents the initial-overlap limitation addressed by the centre-ray fallback: [Physics.SphereCast](https://docs.unity3d.com/ja/current/ScriptReference/Physics.SphereCast.html).

## Preservation

The preexisting TMP fallback still matches its start-of-task backup byte-for-byte. Lunar source/scripts remain untouched. The optional toolkit install preview proposed 282 unrelated changes and was not applied. Tests use the project's established native editor workflow, with one editor at a time; no package, hook or global setup was installed.

Stamina checkpoint 8c47686 is pushed as SethyPagna without a coauthor. Windows packaging/startup results will follow.
