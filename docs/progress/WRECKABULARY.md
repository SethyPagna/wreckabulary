# Wreckabulary progress

## Resume here

**3 October 2026 laptop delivery:** the latest Workshop continuation has been
reconciled with published main history and the later sealed Pickup native fixes.
Browser mechanics pass 88/88 and actual Chrome desktop/mobile flows pass 25/25,
with zero captured page/resource errors. Hosting is being moved to Cloudflare
Pages, using the James account only. GitHub branches retain their history.

The native corrections are 22 selectively transferred files whose source and
destination each passed two SHA-256 reads against the sealed native source
manifest. They repair animation binding, Pickup contact, BAT attachment and
squash stability. An independent source review found no integration defect.
The combined Workshop/native source has **not yet passed fresh Unity engine
tests**. The separately retained 2 October Windows player compiled successfully;
its full gameplay acceptance remains unverified. Preserve its source and evidence.
The original generated solution-file edit remains unstaged.

Next: finish Pages deployment and hosted checks, update the portfolio through its
active owner, then remove only verified redundant laptop copies. The cloud-machine
license notes below describe the earlier environment; the laptop has the pinned
editor and an existing Personal license, but its serial verification is queued.

Work on `feature/wreckabulary-production`, based on the latest laptop commit
`6030d6b` from `codex/latest-wreckabulary-2026-10-01`. Read [the production plan](../PLAN.md)
and [the current README](../../README.md). The laptop progress file was absent from
that commit; this record contains observed cloud work and checks.

The immediate gate is **Unity Personal activation through Unity Hub on the editor
machine**. The exact editor is installed, but it exits with code 198 before import
because no valid UnityEditor license/entitlements are available. After activation:

1. Open the project with Unity **6000.6.3f1**, revision `45d8eee7de74`.
2. Run **Wreckabulary → Set Up Art and Data** to populate imported animation clip
   references and refresh the generated libraries. Preserve existing scene edits.
3. On Windows, run `.\Tools\CloudSetup\verify-unity.ps1 -Build` from the repo.
   On Linux, run `bash Tools/CloudSetup/verify-unity.sh --build`. Inspect the fresh
   EditMode/PlayMode XML and build logs; require nonzero real tests. The Windows
   helper builds Windows PC and Unity Web; the Linux helper builds Linux PC and Unity Web.
4. Inspect actual native gameplay, wardrobe/animation, wall/door collisions,
   touch/controller input and complete mode loops on both maps.
5. Build/test Windows with the verified matching Windows Mono module installed
   here. Install the matching Android modules and profile actual target hardware.
   Linux and Unity Web modules are installed; iOS needs a supported Mac workflow.

Do not mark the complete Unity/platform release finished until those gates pass.
No account credentials or license contents belong in chat or this repository.

## Delivered implementation

- Shared contract: 100 HP, 18 loose letters including craft reservations, two
  carried gear slots and two deployed items. Exact-word crafting, interruption,
  one-time reusable refunds and spent consumables use the enabled catalogue.
- All twelve recipes: BALL, BAT, BED, BLADE, BOMB, FOAM, LAMP, MAT, PLATE, SOAP,
  SOFA and TABLE. Authored attack/use/place timings, durability, shield, bomb,
  recoverable throw, soap zone, jump pad, directional speed strip and cover.
- Connected Pinwheel House (20 × 20 m) and Garden Courtyard (32 × 32 m), with
  distinct room dressing, per-map objectives, keepsakes, spawns and extraction.
- Dibs, Duos, Moving Day and Moving Out, plus tutorial, solo AI, round/results,
  retry/home, clear-out warnings and physical rescue/placement objectives.
- Imported supplied furniture, tiles and modular avatar; outfit colours and
  accessories; Classic/Candy/Arcade item finishes. Native animation needs the
  licensed editor's library refresh above.
- Native health/bag/gear/craft HUD, independent move/aim joysticks, illustrated
  actions, contextual availability, accessible typewriter selection and HOME.
  Single-local-player camera follows the action; couch players share the map.
- Standalone HTML/Three.js edition in `Web`, with real supplied GLBs, shared JSON,
  typed recipes, pointer/keyboard/touch controls, AI, wardrobe, minimap, pause,
  cues, objectives, results and replay. It is separate from a Unity Web build.
- Creative Workshop: all 40 supplied catalogue models as decor, text furnishing,
  three idea cards, placement preview, selection, movement, rotation, finishes,
  removal, undo/redo, independent per-map saves and portable JSON import/export.
  Saved peaceful tours preserve the design. The two authored editable shells use
  flat floors; unsaved indoor appliances and balcony/stairs are omitted there.
- Tactile wood/ceramic/cloth material detail, grounded floor slabs and woven rugs.
  Native shader seed generation is wired into setup and awaits licensed rendering.
- Pinned toolchain helpers, repeatable source checks, Unity test/build entry
  points, asset audit/export scripts and portable workflow-harness pointer.

## Verification, 2026-10-01 UTC

| Check | Observed outcome |
| --- | --- |
| Engine-free C# rules | **192 passed, 0 failed**, including shared layout/batch parity |
| Separate C# assemblies | **6 compiled, 0 errors**; 34 serialized/unused-field warnings in runtime source |
| Browser domain checks | **88 passed, 0 failed**: 40 mechanics, 43 home-design and 5 peaceful-tour checks |
| Actual home storage source | **11 passed, 0 failed**, injected temporary-filesystem .NET host; not Unity execution |
| Final static HTML delivery | **5 focused real Chromium scenarios passed**, nested-path extracted ZIP, final rename/history/preview corrections, text furnishing/save/tour/return; no errors/external requests, CRC and exact bundle hashes pass |
| Real Chromium interactions | **25 scenarios passed, 0 page/resource errors**; real WebGL, match/Workshop input, touch emulation and portrait/landscape |
| Independent browser review | Two gameflow findings fixed; 22 isolated DOM and 25 actual WebGL checks passed on the reviewed pre-final-geometry snapshot, plus latest shadow-teardown probe |
| Interchange asset verifier | **99 checked, 0 failed**, including mobile derivative, normals/weights, material factors, texture hashes and exact animation streams |
| Unity metadata | **502 GUIDs checked across Assets**, no missing asset `.meta` files or duplicate GUIDs; 41 UI textures have explicit transparent 2D importers |
| Independent review | Scoped findings corrected; separate Chromium pause and BALL/cover reproductions pass |
| Windows validation helper | **33 synthetic orchestration cases passed** under PowerShell 7.4.19; actual Windows PowerShell 5.1 and Unity execution remain unrun |
| Unity editor setup | **Blocked: exit 198**, no Personal license; no project import completed |
| Real Unity EditMode/PlayMode/captures/builds | **NOT RUN** |
| Android/iOS/controller hardware and device performance | **NOT RUN** |
| Workflow harness | **74 passed, 0 failed, 3 intentional opt-in skips**; these are workflow checks, not game checks |

The source check compiles actual assembly boundaries against installed Unity
engine and editor-template package DLLs. Those template DLL versions may differ
from the project's pinned packages. This is useful C# evidence, not a successful
Unity import or native execution.

The filesystem host executes actual HomeStorage and its test source with a
throwing persistentDataPath shim; it verifies file/history behavior using an
injected directory. Unity application initialization and platform persistence
remain unrun. The final combined browser run precedes a focused rename-only UI correction;
renderer, engine and stylesheet stay unchanged. The corrected final bundle gets
a separate extracted-archive browser check.
The independent browser review scopes and source snapshots are
recorded in the Workshop review; its earlier counters are not final-suite counts.

The browser interaction fixtures position/provision bounded scenarios; actual
keyboard, pointer, joystick and action inputs exercise them. They are not an
exhaustive manual playthrough or physical phone certification.

## Art assessment and optimization

Read the [independent source review](../reviews/PRODUCTION_SOURCE_REVIEW.md),
[Chromium evidence](../reviews/evidence/browser-report.json),
[Workshop close-camera visual review](../art/WORKSHOP_VISUAL_REVIEW.md),
and the [critical supplied-asset audit](../art/SUPPLIED_ASSET_AUDIT.md) and actual
[40-item contact sheet](../art/SUPPLIED_ITEMS_CONTACT_SHEET.png). Cohesion is **7/10**;
object recognition **8/10**; surface realism **5/10**. The meshes have recognizable
rounded shapes, while finish variation and complete native lighting still need work.

The accepted browser mobile avatar has **17,660 default-outfit triangles** versus
31,696 original (**44.3% fewer**), preserving the head, 22 bones and exact 17-clip
animation data. A matching four-avatar renderer comparison reports 287,722 triangles
versus 343,866 (**16.3% fewer**). Material-slot count is unchanged; these are geometry
measurements, not phone FPS. Original FBX/texture assets are preserved.

## Environment and continuation

.NET SDK 9.0.318 is installed rootlessly under `/workspace/.cloud-setup/dotnet-root`.
Unity, WebGL and verified Windows Mono support are under
`/workspace/.cloud-setup/Unity6000.6.3f1`. The Windows archive has 403 extracted
files; official integrity, Win64 PE headers and repeat installation passed.
No Windows player build was run. Archive
sizes/integrity and Microsoft package SHA256 checks passed. System Chromium and
locked browser dependencies are available. [Cloud setup](../../Tools/CloudSetup/README.md)
contains the reproducible checks.

Reusable `install_script` and `start_skill` have been saved in the environment
configuration draft. Unity login/package redirect destinations were added there;
a draft save does not activate a license, change current networking, publish a
snapshot or prove a fresh machine works.

The workflow harness stays outside the repository, with private state in its own
folder. The production run tracks reviewed Workshop delivery and native readiness workflow. The user handled the
`cchayadap/wreckabulary` `james-v1` overwrite themselves; do not repeat it. The native license/device gate
remains external to these code and HTML deliveries. Do not copy private writer identities or task history
into the game. Resume from this portable record when working on another machine.

Keep Git author and committer **SethyPagna**, using the latest laptop commit's user
email. Add no coauthor trailers. Online matchmaking, arbitrary floor-plan construction, additional recipe
families and iOS delivery remain follow-on features. Workshop supports furnishing
the two authored house shells, not constructing arbitrary building geometry.

The LFS upload endpoint rejected the cloud authentication, although the normal Git
push route passed its check. Reviewed new GLBs/UI images/review renders (each below
8 MB) are therefore committed as regular Git blobs. Their bytes/hashes are unchanged;
the original supplied asset pack retains its upstream LFS objects. The commit adds
no new LFS object requirement.

## Delivery and current goal

The reviewed new game continuation is on `feature/wreckabulary-production` for
SethyPagna's fork. The user stated they already handled the target overwrite and
asked to focus on making the game ready. Do not repeat that overwrite or reconcile
older target features. PR #12 is attached as user-provided context; its observed
head `e79ed4c` had the previous production tree. Preserve later user repository
changes when delivering this new continuation.

The standalone HTML package is playable and passed actual browser interactions.
Native release requires supported Personal activation followed by real editor
import, tests, captures and builds. A compiled native project is not a verified
native executable. Use the current progress/build instructions to finish that gate.

## HTML Workshop package

The final rebuilt HTML ZIP is `Wreckabulary-HTML-Workshop-2026-10-01.zip`,
18,385,248 bytes, SHA256
`3f7d931501e93ef6049b8944f16b657c959aa29e16ec1112c0fa62c5745e078e`.
CRC and extracted-bundle hashes match the frozen final build. Serve the extracted
folder over HTTP using PLAY-README.txt. The final focused real-browser extracted
package check passed all five scenarios with no page/resource errors or external
runtime requests; see [delivery evidence](../reviews/evidence/workshop-delivery-browser.json).
