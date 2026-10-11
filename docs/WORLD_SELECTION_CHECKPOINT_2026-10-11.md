# .149 — avoid repeated screen selection projections, offline

Source .149; installed QA remains .146. No Valheim, Unity, input or deployment.
The full goal remains active and incomplete.

`SelectBlueprintArea` now stores squared screen distance during candidate filtering,
instead of rescanning renderer bounds/projecting inside every sort comparison.
No persistent cache/dependency. Recipe/visibility/raycast/rectangle/radius128m,
nearest-first order, remove semantics, first-refusal stop and QA guards are unchanged.
Equal-distance tie order remains unspecified.

Actual method source runs with host substitutes in
`tests/BuildWorks.WorldSelectionChecks/Run.ps1`: filters, reversed rectangle,
occlusion/projection/boundary, ordering, removal, budget refusal and empty repeat.
50000 candidates: old4922980 projection calls, new50000. This is a call count,
not native host speed/FPS. Normal/QA builds0/0, Host25185596/localization973,
EditorBridge and compiled isolation passed. Both DLLs also assert no projection
inside the sort comparer. Reviews/commit/logs: artifacts/checkpoint-0.19.149.

Format first failed at a sandbox MSBuild pipe; permitted retry found3 pre-existing
WHITESPACE diagnostics outside this method. Entire unchanged source suffix matches
0404abb; evidence retained. EditorBridge temporary JSON write needed a permitted
retry. No user blueprints/saves changed. Installed .146 DLL hashes remain unchanged.
No verify helper; direct checks/reviews used. Store/Geometry full reruns skipped:
unchanged here, last full results .148. No game/UI/capture/reload/network proof.
List IndexOf/Contains and other heavy selection work remain open.

P03 stale capture512 claim corrected: since .126 QA selection and capture use50000,
normal512 with loaded-session gates; compiled checks confirm this, not runtime scale.
C01–C04 volume selection is still pending. Read-only integration audit proposes
an additional area-XZ then bottom/top bounds mode, retained manual include/exclude,
object-position membership, loaded-zone warning and no forced loading. Existing
screen rectangle stays available. New visual design is not approved or implemented;
trees need the separate administrative capture/placement contract.

Deferred: one verified deployment when Ostrix is free, then existing QA queue.
No new manual volume test until implementation and a prepared stand exist.
