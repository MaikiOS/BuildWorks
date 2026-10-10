# .130 — replacement at the missing record pose

Source 0.19.130, checkpoint d28efc3; installed QA remains 0.19.117 in TerrainRamp-1.0-Test.
No deployment, Valheim launch, save mutation or network acceptance.

Selected replacement parts rigid-align their first document record origin/full
rotation with the target. The confirmation names that anchor. Relative poses and
own scales/native prefab basis are retained; unknown model surfaces are not inferred.
With only a missing record selected, Open catalogue → part/complete blueprint
opens an aligned confirmation preview directly. Blueprint primary-or-first is
the anchor. Insertion plus replacement is one atomic edit and one Undo/Redo.

Cancel/Escape restores original visuals without document mutation. Confirmation
is on the right, with a transparent modal input shield so the result remains
visible. Hidden source parts stay hidden. Incomplete replacement sources are
refused explicitly. Stale document/selection/prefab availability is checked again.
Ordinary catalogue placement is retained. Existing independent supports/custom
points remain fixed; inserted blueprint supports/points follow its rigid transform.
Source evidence is retained, existing primary/pivot links are repaired, inserted
records get new IDs. No resources, gameplay costs or server rights are changed.

Evidence: old non-alignment reproduced; same-count stale part-index failure found
by actual catalogue 1→1 UI acceptance and fixed with a dedicated geometry test.
Normal/QA 0 warnings/errors; Geometry includes arbitrary rotation, 1→N, one Undo,
sources/groups/native basis, 50000 boundary and atomic overflow refusal. Store
209 inputs:202 parsed,7 explicit existing refusals. Host Steam25185596,
localization933, compiled isolation and launch preflight pass. Unity6000.0.61f1:
492 screenshots/cases, completion2026-10-10T11:32:44.6029846Z; native UI buttons,
preview/cancel/stale selection/prefabs, single/whole blueprint, hidden/incomplete
sources, ordinary placement recovery and save/reopen. Correctness review PASSED;
Ponytail review is recorded separately. Evidence: artifacts/checkpoint-0.19.130.

Ponytail verdict: Ship; no blocking finding. Final Unity repeat passed.

The [Russian protocol](REPLACEMENT_POSE_CHECKPOINT_2026-10-10_RU.md) gives exact
fixture paths, initial state, Russian buttons, expected results and failure signs.
Do not run it on .117; fresh guarded QA deployment is a prerequisite. Existing
accepted file16 construction does not need repeating. Game construction/reload
and multiplayer remain unverified.
