<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# Exact nearest-part assignment — source 0.19.120

Installed QA remains 0.19.117. No deployment, Valheim launch, world/save change or
multiplayer proof belongs to this checkpoint. Four large snap-set imports and all
12 remaining parser refusals are still open; storage limits are unchanged.

## Change

`BlueprintPlacementFrame.SnapLocalSets` previously compared every custom point
against every part origin. It now builds a bounded, balanced spatial index once
per call. The shared Geometry helper copies the origins, uses the same float
squared-distance arithmetic and returns the earliest original part index on ties.
The opposite tree branch remains eligible at equal distance. Coordinates, point
order, inverse rotation, prefab/relative scale and per-piece codec validation are
unchanged. The index accepts 1–50000 finite float origins; query coordinates must
also be finite. Empty snap lists do not build an index. Tree depth stays bounded
by balanced median ranges; no new dependency or persistent cache was introduced.

## Evidence

Local receipts: `artifacts/checkpoint-0.19.120/`.

- `geometry.log`: random 3D, coplanar/line/grid sets, reversed/equal-distance
  ties, coincident origins, underflow/overflow/rounded float ties, immutable
  source, malformed/count guards and 50000 origins pass against the old full scan.
- `nearest-benchmark.json`: all source assignment IDs match, every point in each
  of four real archive entries, repeated five times. Archive hash before/after:
  `4A6E6A53F246B5CF4C7EA9472F726D9F35687BCE978F28AB47A020AEE759DD88`.
- `build-normal.log`, `build-qa.log`: 0 warnings / 0 errors.
- `store.log`, `host.log`, `localization.log`, `qa-isolation.log`: PASS. Host
  contract checks the compiled shared-index call and retained rotation, scale
  and codec calls; this is not gameplay proof. Localization: 930 keys.
- `verification-status.json`: Unity 6000.0.61f1 PASS, 492 existing screenshot
  cases; completion UTC `2026-10-09T18:48:39.5151915Z`.
  `WorldSnapPointAcceptance.TestNearestPartAssignments` additionally compares
  actual Unity old full-scan local coordinates and point order against the new
  adapter for 257 parts / 1200 points, two native prefab scales, rotations and
  nonuniform relative scales. It runs before the fresh world-snap PASS receipt
  is written. Existing native snap byte reconstruction, ownership and invalid
  prefab-scale checks also pass. Generated runtime frame source hash matches
  canonical source (`source-sha256.txt`).
- Independent correctness and Ponytail read-only reviews found no blocker.
  `verify` helper is absent from both skill roots; direct checks and reviews
  above were used instead.

Read-only benchmark medians (ms, five repetitions, same process):

| Source | Parts / points | Index build | All queries | Old full scan | Maximum points / part |
| --- | ---: | ---: | ---: | ---: | ---: |
| Arani_Forte_Negro | 5062 / 6596 | 8.613 | 5.036 | 82.939 | 46 |
| Big_Bridge_1 | 770 / 2012 | 0.197 | 0.346 | 4.045 | 14 |
| Decay | 4070 / 11790 | 1.575 | 3.219 | 123.603 | 24 |
| Flabellum | 6265 / 17125 | 2.598 | 4.655 | 273.509 | 17 |

These are standalone .NET raw-XYZ assignment timings. The table does not measure
Unity/Valheim FPS, whole-import latency, prefab resolution or network packets.
The last-pass managed index-build allocations were 45072–362368 bytes; this is
not process peak memory. Coincident/degenerate queries can still visit many
origins to preserve exact ties; no worst-case constant-time claim is made.

Reproduce from BuildWorks:

```powershell
dotnet run --project tests/BuildWorks.GeometryTests -c Release --no-restore
dotnet run --project tests/BuildWorks.GeometryTests -c Release --no-build --no-restore -- --snap-benchmark artifacts/external-blueprints/2v2t-5.0.0/package.zip artifacts/checkpoint-0.19.120/nearest-benchmark.json
```

## Still required

Bound full gizmo projection/visibility/candidate work, coordinate large snap
storage across parser/Store/Geometry/portable, then test full roundtrips and world
placement/reload/network. The .119 renderer pool is only one resolved cost.
Do not raise all 512-point guards or discard saved source points to mask this.
Owner .117 instructions remain unchanged; there is no new manual test for .120
before a coordinated next installation. The full goal remains active.
