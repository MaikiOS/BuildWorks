# 0.19.122 — exact spatial capture deduplication

Source checkpoint only. Installed QA remains0.19.117 in TerrainRamp-1.0-Test.
All12 external parser refusals, including the4 large snap sets, remain open.

## Change and boundary

`PrecisionPlacementSession.TrySaveWorldSelection` previously scanned every retained
custom point for each incoming point. It now uses the bounded Geometry
`BlueprintSnapPointSet`. The caller retains the same512 aggregate capture budget,
failure message and no-save-on-overflow behavior. The per-world-piece512 codec,
native21/portable10 formats, permissions, inventory and world objects are unchanged.

The collector uses neighboring0.0002 cells, exact float subtraction/multiply/add
and the original strict squared-distance threshold `<0.00000001f`. Only already
retained representatives deduplicate later points; proximity is not transitive.
The first point's exact float coordinates and input order survive. Duplicates
remain accepted at capacity; new points and non-finite coordinates fail without
changing the set. Output is read-only. Extreme coordinates retain the old scan.

`AnchorAdjustment.ExternalCompositeSnapPoints` cannot substitute for this path:
it compares double distances with an inclusive tolerance and excludes shared
cross-piece joins. Those semantics would change user custom points.

## Evidence

Local folder: `artifacts/checkpoint-0.19.122/`.

- Geometry regression compares every acceptance decision and exact retained
  coordinates/order with the old float full scan: dense inputs, negative/cell
  boundaries, neighboring float values around the threshold, non-transitive
  chains, extreme ranges, invalid coordinates, capacity1/512 and17125 retained
  points with repeated entries. `geometry.log`: PASS.
- One standalone .NET sample:19572 inputs/17125 retained; reference full scan
  250.1125ms, spatial collector12.9544ms. This compares the reference math and
  collector, not the complete native capture method, Unity timing or game FPS.
  It is a single observation, not a benchmark distribution or memory peak.
- Unity6000.0.61f1:17125 actual rotated/transformed/XYZ-scaled points match the
  original `Vector3.sqrMagnitude` scan exactly. Strict threshold/negative/extreme
  representatives, repeated entries and unchanged512 overflow behavior pass.
  This is an isolated transform/collector test, not Session capture, world save,
  reload, native construction or network proof.
- Final Unity receipt:492 existing screenshot cases plus new assertions PASS,
  completion `2026-10-09T19:29:12.8751317Z`. Initial pass was repeated after adding
  threshold edge cases. No visual design changed.
- Normal and QA builds0 warnings/0 errors; Store, HostContract25185596,
  localization930 and compiled QA isolation PASS. HostContract checks compiled
  capture calls to the collector plus the unchanged codec-budget constructor.
- Initial build failure `CS0246` for the unqualified collector name was fixed by
  qualifying `Geometry`; `setup-build-error.log` preserves that failure.
- `verify` helper is absent; direct checks were used. Independent correctness
  and Ponytail reviews found no blocker; outcomes are in `verification.json`.

## Next dependency: native hammer selector

Current host IL was read into `native-snap-contract.log`, with independent research.
`Piece.GetSnapPoints(List<Transform>)` enumerates tagged direct children in their
order. `Player.FindClosestSnapPoints` reads the entire source list and nearby world
targets, uses `m_manualSnapPoint` as an exact source index, and automatically scans
source×targets. `FindClosestSnappoint` evaluates `Vector3.Distance`, retains the
first equal target; the outer scan retains the first equal source.

Filtering or capping the returned list would alter manual selection and tie
behavior. Hammer still creates a child transform for every point. Next: measure
this allocation/query path and accelerate the BuildWorks-only automatic selector
while preserving full order/manual indexing and exact native distance semantics.
Then coordinate a separate blueprint-wide capacity through parser/Store/Geometry/
portable/capture and prove all4 files' complete roundtrips. No blanket cap increase
or dropping of points has been implemented here.

No installation, Valheim launch, world/save changes or multiplayer test occurred.
The owner's .117 QA scenario is still the only prepared installed acceptance.
