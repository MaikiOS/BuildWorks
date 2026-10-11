# .150 — world area and vertical selection bounds, offline

Source .150 only; installed QA remains .146. No Valheim, Unity, input, deployment
or save changes while Ostrix works. Full goal and runtime acceptance remain open.

Existing world capture now has an XZ-area mode with bottom/top offsets relative
to the entry ghost/player Y plane. Height edits preview the wire box; explicit
refresh applies the selection. Dirty or disappearing selected objects block save
until refreshed. The old screen rectangle remains available as a separate mode.

Membership uses inclusive Piece world origins, not geometry intersection. Manual
include/exclude survives area/height changes; exclusion wins. Six paged rows,
visible-piece clicks and Ctrl exclusion reuse the existing HUD/input flow. Only
loaded buildable Piece objects are considered. No zone loading; unloaded-zone
count is shown without claiming completeness. Queries are bounded by4096 zones,
coordinate checks and existing normal512/isolatedQA50000 limits. Budget/session
refusal preserves the previous selection. Trees/admin world objects remain open.

The proposed thin translucent cyan wire box has no colliders, shadows or fill.
It has not been visually approved or rendered in the game.

Evidence: full Geometry suite; actual production-source rectangle/commit/input
harnesses; normal/QA builds0/0; Host25185596; localization988; compiled isolation;
EditorBridge; scoped new-file format. Independent review found an early plane-ray
return blocking high-wall clicks; regression reproduces it, fixed handler passes
click and invalid start/end area drags. Correctness and Ponytail outcomes/commits
are in `artifacts/checkpoint-0.19.150/verification.json`. `verify` is unavailable.
Three pre-existing Session whitespace errors remain documented. Future Workbench
HUD tests were added but not run or compiled by Unity. Installed .146 hashes match.

Ponytail also found repeated full row localization/allocation during height drag.
Fixed by reusing the row snapshot until membership changes. Actual height/HUD
methods with50000 rows and64 edits reuse the same array with no new localization;
membership refresh rebuilds it. The previous call fails the regression. This
measures work/reference identity with substitutes, not Valheim latency.

No Store/corpus rerun: converter/storage unchanged; prior .148 evidence retained.
Native picking/zones/UI/performance, capture/build/resources/reload/network remain
unverified. Height-field cancel can still require refresh after restoring its old
number. See the Russian checkpoint and deferred `QA_0.19.150_RU.md` for exact steps.
