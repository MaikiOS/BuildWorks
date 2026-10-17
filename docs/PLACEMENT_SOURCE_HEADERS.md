<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# Original placement headers — .116

The .blueprint adapter recognizes a single empty `#Center:` and finite three-number
`#Coordinates:` / `#Rotation:` headers before the first `#Pieces`.
Their exact original text, BOM, line endings and part-row references remain in
sourceDocuments through native19/portable9 save/reopen/export. No schema change.

Coordinates/Rotation describe the old world placement, not an extra transform of
local piece rows. BuildWorks uses its own selected placement anchor; it does not
restore the old world location or interpret the header Euler convention. Local
position/quaternion/XYZ scale remain unchanged. This is understood provenance,
not gameplay state restoration. These headers alone no longer block construction.

A named Center still requires a verified placement-anchor adapter. Named/duplicate/
malformed/late headers, nonfinite values, vbuild use and unknown sections remain
opaque and continue blocking world placement. No unknown object state is dropped.

## Evidence

Store regression reproduced the old false blocker before the fix. Tests cover
nonzero original pose, local rotated/nonuniform-scale wall, native copy/reopen,
portable roundtrip, exact source and row binding, unsupported cases, and the ready
15-source-position.blueprint fixture.

Local BiomeBlueprints1.0.3 corpus:89 documents have these headers;81 have empty
Center, of which78 already parse. Six parsed documents previously blocked only
by the headers now have no unknown state. This proves neither installed prefab
availability nor in-game build/reload. RandMatSeed and other state blockers remain.
No external author blueprint/archive or game-decompiled source is redistributed.

## Primary format evidence

Infinity Hammer revision ce2737f0156df44aa7d193b5e1aed6167f3d794b:
- [Writer, GetPlanBuildFile / BuildBluePrint](https://github.com/JereKuusela/valheim-infinity_hammer/blob/ce2737f0156df44aa7d193b5e1aed6167f3d794b/InfinityHammer/commands/blueprint/HammerSave.cs).
- [Reader, GetPlanBuild / hammer_restore](https://github.com/JereKuusela/valheim-infinity_hammer/blob/ce2737f0156df44aa7d193b5e1aed6167f3d794b/InfinityHammer/commands/blueprint/HammerBlueprint.cs).
- [Local-coordinate centering](https://github.com/JereKuusela/valheim-infinity_hammer/blob/ce2737f0156df44aa7d193b5e1aed6167f3d794b/InfinityHammer/Blueprint.cs).
