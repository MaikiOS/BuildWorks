# Anchor projection and visibility work — source 0.19.121

Installed QA remains 0.19.117. No deployment, Valheim launch, saves or multiplayer
test belongs to this checkpoint. All12 parser refusals, including4 large custom
snap-set files, remain open. Storage/ordinary-anchor/support/per-world-piece
codec limits and native21/portable10 formats are unchanged.

## Verified change

`TransformGizmoView` caches each source index's center/edge screen projection by
camera identity, exact view/projection matrices, pixel rectangle and exact world
coordinates. In-place mutations and cloned/rebuilt arrays are detected by values;
there is no array-reference freshness assumption. Edge changes also catch model
size and point-type/size changes. Camera state is rechecked before public input
hit tests, including calls between `Show`s. The projection counter measures only
anchor center/edge native projection calls, not every gizmo/UI projection.

Occlusion results are reusable only inside one `PrepareAnchorHandles` call and
reset for every `Show`. Editor/screen-space mode allows occluded selectable
points; only the displayed <=512 slots need fresh occlusion feedback. World mode
tests visibility for full-set hit candidates that could replace the current
winner, then fills sorted slots with visible candidates. Fully occluded cases can
still require every candidate. No stale cross-frame collider cache was added.

The exact legacy radius/coincident/screen-distance/depth comparator is shared by
preselection and public input. The latter iterates displayed slots in descending
source-index order, rather than scanning all source points. Full-set winner
promotion, selected/pinned priority, small-set slot identity, source coordinates
and source order are preserved. Source scan/candidate sort remain per `Show`.

## Evidence

Local folder: `artifacts/checkpoint-0.19.121/`.

- `before-unity.log`: actual old runtime source queried17125 editor visibility
  callbacks for17125 points despite its512 renderer pool. Baseline source is in
  `baseline/TransformGizmoView.cs`.
- Test setup initially had `CS0103: The name 'camera' does not exist in the current
  context`; `RuntimeEditorAcceptance` was corrected to use `template`.
  `test-setup-compile.log` records that failure; it is not the before repro.
- Normal/QA builds:0 warnings/0 errors. Geometry, Store, HostContract25185596,
  localization930 and compiled QA isolation: PASS.
- Final Unity6000.0.61f1:492 existing screenshot cases PASS, completion
  `2026-10-09T19:09:37.5733719Z`; new assertions have their separate receipt
  `gizmo-query-check.txt`. Runtime source copy hashes match canonical source.
- `GizmoQueryAcceptance`:17125 editor points, exact value/camera/projection/
  pixelRect/model-size invalidation, cloned array and in-place changes, cursor
  movement, empty cleanup. Random/dense128/513/1200 point sets compare exact
  legacy renderer indices and public hit results, both world/editor modes,
  geometry radii, near-depth ties, show-all/radius filtering, selected/pinned,
  fresh visibility and input between `Show`s.
- World17125 coincident points:512 visibility calls when clear;17125 calls once
  per source when all occluded; obstacle removal and changed points/camera before
  `Show` do not leave stale hits. Existing `LargeGizmoAcceptance` still covers
  native-only/plane/behind/hidden, coincident geometry priority and pooled cleanup.
  These visibility transitions use controlled callbacks; they do not constitute
  real Valheim collider/physics proof. The compiled default visibility path still
  calls native `Physics.RaycastNonAlloc` through the shared checked adapter.
- `large-gizmo-pool.png` inspected: source glyphs and selected/pinned highlights
  remain in the isolated capture; no visual redesign was introduced.
- Independent correctness and Ponytail reviews found no blocker. The `verify`
  helper remains unavailable; direct regression/build/static checks were used.

Measured work counts in the isolated assertions:

| Case | Anchor center/edge projection calls | Visibility callbacks |
| --- | ---: | ---: |
| Old editor17125, cold | not instrumented | 17125 |
| New editor17125, repeated identical points/camera | 0 | 512 fresh |
| Same values in a cloned array | 0 | 512 fresh |
| One in-place point change | 2 | at most512 |
| Only model size changes,17125 points | 17125 edge calls | at most512 |
| World17125 coincident, all clear | 17125 cold centers | 512 |
| World17125 coincident, all occluded | reused if camera/points unchanged | 17125 once each |

These are callback/projection counts, not total CPU time, GPU cost or Valheim FPS.
Changing camera or all point values still rebuilds all required projections.
Cheap source validation/scan, radius math and candidate sort remain unbounded by
the512 renderer cap; exact adverse world visibility retains its linear worst case.
No fixed frame-time ceiling or whole-chain optimization is claimed.

## Next dependencies before large-import enablement

Read-only audit confirmed two additional consumers:

1. `PrecisionPlacementSession.TrySaveWorldSelection` merges captured custom
   points, uses an all-prior-points `.Any` duplicate search and **refuses** the
   entire capture beyond512 total points. It does not silently truncate. The
   blueprint-level bound and duplicate lookup must be coordinated with storage,
   retaining the per-piece512 codec guard.
2. `HammerBlueprintPieceRegistry.CreatePrefab` eagerly creates one
   tagged child `snappoint` for every custom point. Its native snap consumers and
   lifecycle need measured, complete representation before exposing large sets.

Separate bounded custom-point guards are then needed at `.blueprint` parsing,
Store validation, Geometry creation/insertion and portable field validation;
ordinary anchors512, supports64, per-piece codec512, coordinates±512 and byte
limits must remain. Four-source native/portable/save/copy/roundtrip acceptance is
still required. Existing corpus parse counts and nearest-index tests do not prove
those roundtrips. Owner .117 instructions remain valid; no new manual .121 test.
Full goal remains active.
