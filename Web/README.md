# Wreckabulary browser edition

Production hosting is a Cloudflare Worker at <https://wreckabulary.pagna.workers.dev>.
The deployment's `/BUILD-INFO.json` identifies its exact GitHub source commit.
The Worker uses static assets from `dist` and the James account pinned in
`wrangler.toml`. Build from repository `main`, directory `Web`, with
`npm ci --ignore-scripts && npm test && npm run build`, then deploy with
`npx wrangler deploy` using credentials for the James account. Pin Node 24.15.0.

A standalone Three.js game for one human with AI housemates. It uses the Unity project's JSON rules, item catalogue, two maps and cosmetic wardrobe, and genuine supplied models exported to GLB. This is a separately implemented HTML game; it is not a Unity WebGL build.

Requires Node 22.12+ or 24. The HTML models and images are ordinary Git files;
the native Unity source art separately requires Git LFS hydration. From this directory:

```sh
npm ci
npm test
npm run build
npm run preview -- --port 4173
```

Open `http://localhost:4173`. `dist/` can be hosted by any static HTTP server, including under a subdirectory. Serve over HTTP; browsers do not load model/data assets reliably from `file://`. No CDN, account or API key is needed during play. For development use `npm run dev` (port 4173).

The build synchronizes canonical data from `Assets/_Project/Data/Config/`; edit those source files rather than the public copies. GLB export and licensing provenance are documented in `Tools/AssetPipeline/README.md` and `docs/art/provenance.md`. The generated action art is a raster button atlas; game objects are actual meshes.

For desktop: WASD/arrows move, mouse aims, left click/J attacks, right click/K blocks with PLATE, E picks up or holds a revive, Q/C opens spelling, 1/2 selects a hand, holding Tab shows the bag, hands, outfit and full house map, F places/uses, R drops, G throws, Space jumps, Shift dodges and Esc pauses. Tap a bag letter to toss it. The on-screen action buttons appear only on touch screens; there, the Bag link opens the same panel. Touch uses a movement joystick, autoaim and illustrated action buttons. All outfits and Classic/Candy/Arcade gear styles are cosmetic.

Dibs is first to three rounds; Duos adds an AI buddy and revives. Moving Day delivers recipe parcels, checks the shared room checklist and replenishes shortages. Moving Out requires physically carrying marked original keepsakes, dropping them at the van, and gathering all surviving teammates there alive. Play & Learn has a training dummy and no time limit. Browser play is solo; online multiplayer and a second local human are not implemented in this edition.

Creative Workshop is a separate activity with unlimited decor. Type supplied
object words, choose a room, arrange a preview on the half-metre grid and apply a
finish. Select existing props to move, rotate or remove them; undo and redo keep
editing recoverable. Save each house locally, export/import a portable layout,
and choose Tour to walk through the design peacefully. Doors, spawns, room edges
and overlapping footprints are reserved by the shared layout rules. Other game
modes retain their authored maps and crafting limits.

Run functional browser checks against a running dev or preview server:

```sh
npm run test:browser
# Optional overrides:
CHROMIUM_PATH=/path/to/chromium WRECKABULARY_URL=http://localhost:4173 npm run test:browser
```

The test uses actual Chromium keyboard, pointer and touchscreen input, and saves screenshots/report to ignored `playwright-results/`. Mechanics tests cover letter conservation, duplicate letters, channel cancellation, health/shields, hazards, item ownership, physical objectives, replenishment and cooperative AI completion. Test results do not establish real device frame rates or verify the Unity build.

After `npm run build`, package the complete static edition with:

```sh
python3 ../Tools/Delivery/package-html.py --output /path/to/a-new-delivery.zip
```

The packager checks required build files and the ZIP CRC and writes a SHA256
sidecar. It preserves existing delivery files.
