# Portable Workshop fixtures

`fixtures.json` is consumed by both `Tools/RulesHarness/HomeDesignCases.cs` and
`Web/test/home-design.test.mjs`. The tests load the same canonical item and map
files under `Assets/_Project/Data/Config`; positions remain unscaled metres.
Import examples exercise strict schema, supplied-model permission, complete
footprints, doorway and spawn reservations, and atomic refusal. Fixed batch
outputs pin placement order, quarter-turn trials, allocated IDs and rejected words.

For example, the Pinwheel SOFA's 0.813 m depth cannot fit at the first grid row
z = -9.5 with a 0.15 m wall margin. Its first safe centre is (-9, -9).
The following TABLE scans the earlier row again and avoids both that footprint
and the LivingRoom spawn, reaching (-6, -9.5). PLANT is allowed as decor even
though its match crafting recipe is disabled; AXE has no supplied model and is
rejected. The Courtyard fixtures independently exercise the larger map.

First-fit order is z ascending, then x ascending, then yaw 0/90/180/270.
Grid centres are 0.5 m apart; rectangle clearance uses the Euclidean distance
between their closest points. Rectangle/circle reservations permit tangency.
A 1e-6 m geometric tolerance reconciles the native catalogue's float storage
with JavaScript numbers; half-metre coordinates themselves must be exact.
IDs use the smallest unoccupied `pN`. Batches split on ECMAScript whitespace,
commas or semicolons and uppercase ASCII a–z. The full batch is refused before
placement beyond 2048 UTF-16 characters or 64 tokens. Individual unknown,
unmodelled, full-capacity or unplaceable words are reported in input order.

Run from the repository root:

```sh
dotnet run --project Tools/RulesHarness -- HomeDesign
node --test Web/test/home-design.test.mjs
```

These engine-free tests do not establish Unity execution, native persistence,
browser UI interactions, hardware performance, or platform builds.
