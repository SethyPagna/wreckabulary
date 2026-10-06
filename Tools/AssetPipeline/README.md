# Asset pipeline (Blender → Unity)

Turns the Wreckabulary source packs (the `.glb` deliveries in `Wreckabulary.zip`) into
Unity-ready FBX files plus a material library. Unity never reads the packs directly.

| Step | Script | What it does |
|---|---|---|
| 1 | `build_assets.py` | Imports each chosen GLB, strips cameras, lights and object clips, restores the authored rest transform, puts item models on the floor, and writes FBX. Merges the avatar and every wardrobe module onto one 22-bone rig with all 17 clips, then runs `refine_avatar.py` and `author_clips.py` when `selection.json` asks for them. Writes `materials.json` (authored glTF material values) and the textures (max 512 px). |
| 2 | `verify_assets.py` | Re-imports every FBX and checks triangle count, bounds (±1 mm), authored item size (±2 cm), `Grip_R` on items, no camera or light, every material in the library, and all avatar bones and clips. |

`selection.json` lists what gets converted (40 items, 26 letter tiles, 21 house modules,
10 VFX meshes, the avatar and 6 wardrobe modules).

## Run it

Run one step at a time, and not alongside a Unity build or test run.

```bash
BLENDER="C:/Program Files/Blender Foundation/Blender 5.2/blender.exe"
"$BLENDER" -b --factory-startup --python-exit-code 1 --python Tools/AssetPipeline/build_assets.py -- --packs <folder with vault-v2/ and quality-pass-01/> --repo .
"$BLENDER" -b --factory-startup --python-exit-code 1 --python Tools/AssetPipeline/verify_assets.py -- --repo .
```

Tested with Blender 5.2.2 LTS. Add `--only items,letters` to rebuild part of the set.
Blender 5 keeps an action's F-curves in layered channel bags (`action.fcurves` is
gone), so scripts that read curves go through a small `action_curves` helper.

### Avatar refine and authored clips

Two optional avatar steps run inside `build_assets.py`, switched on in `selection.json`:

- `"refine": true` runs `refine_avatar.py`. It reworks the merged avatar toward
  art-direction board 1 on the same 22 bones and the same wardrobe meshes: hood
  without the crown lobes, hair (its own `hair` material), drawstrings, mitten
  cuffs, baggier trousers, wider satchel straps and a buckle on the front of the
  right strap. The report lists what changed under `refinements`.
- `"author_clips": true` runs `author_clips.py`. The pack's walk and run were a
  shuffle (the feet slid and the legs never passed), so it authors new Idle,
  Walk_InPlace, Run_InPlace, Jump_Preview, Swing/Thrust/Throw_OneHand,
  Hit_Reaction and Celebrate under the same names and lengths, with planted-foot
  IK. Pickup and Place stay the pack's clips. The report records
  `authored_clips`, `ik_overreach_m` and `locomotion`: the metres one walk or run
  cycle covers. `export_web.py` copies `locomotion` into the web manifest. The web
  animator and Unity's `PlayerAppearance.WalkStride`/`RunStride` set the cadence
  from it; the EditMode test `WalkAndRunCadenceFollowsTheAuthoredStrides` fails
  if Unity's constants drift from the report.

After an avatar rebuild, run Unity's *Wreckabulary > Art > Set Up Imported Art*
(or `-executeMethod Wreckabulary.EditorTools.ArtSetup.Run` in batch) so new
materials such as `hair` reach the library, then the EditMode and PlayMode tests.

Avatar conversion also requires Node.js on `PATH`. After FBX export,
`build_assets.py` runs `correct_pickup_contact.mjs`. This offline authoring pass
changes only Pickup's six leg X-rotation tracks. It preserves the authored pelvis
crouch, upper-body motion, all other clips, and the controller's action timing.
It solves the two leg links against their rest ankle positions and preserves the
sole orientation; it adds no runtime IK or actor-height offset.

The same pass can be reproduced on an existing exported FBX. Omit `--output` to
audit the proposed correction without writing the asset:

```sh
node Tools/AssetPipeline/correct_pickup_contact.mjs --input Assets/_Project/Art/Imported/Avatar/Avatar.fbx --report pickup-contact-audit.json
node Tools/AssetPipeline/correct_pickup_contact.mjs --input Assets/_Project/Art/Imported/Avatar/Avatar.fbx --output Assets/_Project/Art/Imported/Avatar/Avatar.fbx --report pickup-contact-correction.json
```

The pass checks both boots across the full authored clip, including between
keys, and rejects unsupported rig or curve assumptions. Its CPU checks do not
certify Unity's imported interpolation, compression, floor contact, or rendered
appearance. Run the full-clip Pickup PlayMode contact test and the normal
Pickup-to-Hold graphics transition before accepting a rebuilt avatar.

## Outputs

- `Assets/_Project/Art/Imported/{Items,Letters,Environment,VFX,Avatar}/*.fbx`
- `Assets/_Project/Art/Imported/Textures/*.png`
- `Assets/_Project/Data/Generated/materials.json`: one entry per material name. Item
  materials end in `_Classic`, `_Candy` or `_Arcade`; `family` + `skin` give the held-skin swap.
- `Assets/_Project/Data/Generated/build_report.json` and `verify_report.json`.

Generated files: don't edit them by hand. Change `selection.json` or the scripts and rerun.

## Notes

- FBX settings: metres, `-Z` forward, `Y` up, no leaf bones, no embedded textures. In Unity,
  the importer (`ImportedArtPostprocessor`) turns on *Bake Axis Conversion* and binds
  materials by name to the library materials.
- Facing: Unity puts Blender (x, y, z) at (x, z, y). This was measured on every model. The
  packs face glTF +Z, which is Blender -Y, so the build turns each scene a half turn about Z
  before export. Models then face Unity's +Z, and the report's bounds are in Unity axes.
- Single-mesh models keep Blender's axis conversion (and the half turn) as a rotation on their root.
  In Unity, put a model under its own parent and move or rotate the parent, never the model
  root, or the model ends up lying down.
- Held items are not separate files. At runtime a held item is the world model scaled by
  the recipe's `held_scale`, moved so `Grip_R` sits in the hand, with the holder's skin.
- The pack's house floors (7,344 triangles per 2 m tile), ceiling lamp and door panels are
  left out on purpose. See `selection.json`.

## Browser derivatives and actual-model previews

The cloud audit uses Blender 4.3.2 to read the existing hydrated FBXs. It does not
rebuild or overwrite the original Unity delivery. `export_web.py` writes 98 GLBs
to `Web/public/art/` and records mesh facts, source hashes, bounds, item grips and
canonical animations in `audit.json` / `manifest.json`. `gltf_materials.py` restores
authoritative PBR factors only after proving the embedded textures are unchanged;
this corrects a Blender interchange bug that dropped tint through Multiply nodes.
`render_supplied.py` and `render_icons.py` render the actual supplied models.

`export_mobile_avatar.py` creates a separate clothing LOD, retaining the head and
both facial expressions. `gltf_avatar_animation.py` preserves exact original
animation and inverse-bind bytes after checking the rest skeleton is equivalent.
`verify_web.py` independently checks 99 exports, including sparse morph accessors,
weights, joint indices, geometry, PBR, hashes, default outfit counts and animation
byte preservation. `promote_mobile_avatar.py` adds `avatar.mobilePath` only after
the verifier passes and the comparison render references current GLB hashes.

```sh
blender -b --factory-startup --python-exit-code 1 --python Tools/AssetPipeline/export_web.py -- --repo .
blender -b --factory-startup --python-exit-code 1 --python Tools/AssetPipeline/render_supplied.py
blender -b --factory-startup --python-exit-code 1 --python Tools/AssetPipeline/render_icons.py
blender -b --factory-startup --python-exit-code 1 --python Tools/AssetPipeline/export_mobile_avatar.py -- --repo .
python3 Tools/AssetPipeline/verify_web.py --repo .
blender -b --factory-startup --python-exit-code 1 --python Tools/AssetPipeline/render_avatar_lod.py
python3 Tools/AssetPipeline/promote_mobile_avatar.py --repo .
```

Run these sequentially. Re-exporting the full avatar invalidates the mobile source
hash and comparison; regenerate and review the derivative before promoting it.
`export_web.py --only avatar` still writes the current wardrobe config into the
manifest, because the verifier counts the default outfit from it.
The mobile default outfit has 29,136 triangles versus 52,488 full detail, with the
same eight renderers and 17 material slots. Neither file verification nor the
comparison render proves phone frame time or Unity import/build acceptance. See
`docs/art/SUPPLIED_ASSET_AUDIT.md` for the critical visual assessment and limitations.
