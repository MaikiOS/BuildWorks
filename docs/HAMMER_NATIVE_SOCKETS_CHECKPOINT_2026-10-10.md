<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .128 — native Hammer blueprint sockets

Ostrix accepted .117 Save As, fixture16 stability/ghost agreement, new shape
reload, no repeat supplies, catalogue management, partial thumbnails, filters
and interior camera.17/18 snapping remains rejected; scaling/replacement UX
and popup/filter layout require work. See `feedback-2026-10-10_RU.md`.

Hammer previously added axis-aligned envelope corners even when real sockets
were available. Rotated parts do not pass through those artificial corners;
native placement can translate the ghost using the wrong source. The actual
source/target pair from the user's screenshot was not logged.

The native17/18 Workbench regression failed with the old condition:
`Hammer adds false envelope corners despite native sockets: TEST 17 Snap A`.
Bounds are now fallback only when native and custom sockets are both absent.
The existing native-socket selection/order/transforms/512 cap moved unchanged
to `BlueprintPlacementFrame`; the production registry and acceptance use it.

Blueprint passive joining keeps the selected Hammer frame orientation instead
of multiplying it by an individual target part's rotation. Ordinary pieces
retain `target * ghost`; saved support inversion remains in `ToRootPose`.
Editor alignment was not changed; no unified editor/world semantics claim.

Normal/QA0/0, Store202/209, Host25185596, localization931, compiled isolation,
launch preflight, Unity492 completed2026-10-10T10:20:48.5643416Z. Native socket
coordinates, explicit frame, custom-only, socket-free fallback, XYZ rebase,
ordinary rotation, rotated support and native translation expression pass.
Independent correctness review PASSED; final Ponytail result is recorded in
`artifacts/checkpoint-0.19.128/review.txt` before commit. `verify` is absent.

Initial sandbox Unity crash−536870911/License IPC and Store access denial are
recorded; unsandboxed isolated reruns reproduced the regression then passed.
No Valheim launch, save changes or deployment. Installed QA remains .117 with
both hashes verified; Default has no BuildWorks DLLs. Piece is a host stub in
Workbench: no native Player/Session placement/reload/network proof for .128.

Next: axis scaling, replacement pose alignment, popup layering, compact filters,
import timings; then fresh isolated QA and targeted game checks.17/18 authored
floor rotations are already30°/120°: do not add another90° with Hammer.
