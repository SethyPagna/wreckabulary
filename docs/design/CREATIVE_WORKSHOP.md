# Creative Workshop — continuation contract

Goal: furnish a cozy house from words and the supplied 40 models, save it, and
walk through it. This is a separate creative activity with unlimited decor;
competitive and objective maps keep their authored furniture and letter economy.
The editable house shells use flat floors, authored rooms, walls and open doors.
Raised balcony/stair extras and unsaved indoor appliances are omitted in this
activity so portable decor has the same floor placement in both editions. Normal
Pinwheel gameplay retains its balcony/stairs. Outside planting is cosmetic.

## Portable layout

Schema 1: `{ "schema": 1, "map": "pinwheel", "name": "My Cozy House",
"props": [{ "id": "p1", "word": "SOFA", "x": -7, "z": -6,
"yaw": 90, "skin": "Classic" }] }`.

- Map is `pinwheel` or `courtyard`, using canonical unscaled metre coordinates.
- Name is trimmed, 1–48 characters. Maximum 64 props.
- Unique prop IDs match `[A-Za-z0-9_-]{1,48}`; word is a supplied catalogue ID.
- Coordinates are finite and snapped to a 0.5 m grid; yaw is 0/90/180/270 degrees.
- Skin is Classic, Candy or Arcade. All existing catalogue objects are decor here;
  disabled crafting recipes remain disabled in matches.
- Footprints come from catalogue size X/Z, each at least 0.4 m. Quarter rotation
  swaps axes. Keep the complete footprint 0.15 m inside one authored room.
- Keep rectangular footprints separated by 0.1 m. Reserve spawn circles of 0.75 m
  and door circles of `width / 2 + 0.35 m`: rectangle/circle overlap is rejected.
- Reject malformed, unsupported, overlapping or blocked imported layouts atomically.
  Imported JSON never changes the current design on failure.
- Layout validation and deterministic first-fit text furnishing share this contract
  in engine-free C# and JavaScript, with representative parity fixtures.

## User flow

A visible Workshop entry opens a full-house design view. Type words such as
`SOFA TABLE PLANT`, choose a room, preview/place a supplied model, select existing
props, quarter-turn rotate, delete, undo/redo, and apply item finishes. Show useful
validation messages rather than silently changing an invalid position. Save local
layouts independently per map and support JSON export/import. Browsers use local
storage; native uses a local application save. Failed storage/import is reported.

The tour starts a peaceful walkable preview using exactly the saved layout. Native
source has keyboard/controller/touch navigation; HTML supports keyboard and touch.
Competitive mode objectives, AI and conservation must retain their prior behavior.
Return to the workshop preserves the design. Reload roundtrips name, map, props,
position, rotation and finish; transient selection/history need not persist.

## Evidence

Meaningful checks: placement rejection at doors/spawns/walls/other props, bounded
text batches, invalid-import atomicity, undo/redo branching, local persistence,
actual browser word entry and object selection/rotation/removal, roundtrip reload,
peaceful tour and return, portrait/landscape fit, existing game regressions. Record
native source compilation separately from unrun Unity/device execution. Review
same-camera surface changes without claiming real-device speed.
