<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# XYZ scale — verified source checkpoint, not deployed

The transform inspector now has X/Y/Z percent fields and a visible keep-proportions
toggle, enabled by default. Linked editing scales every axis proportionally;
independent editing changes only the chosen components. A single part uses its own
axes; multiple parts use the selected Local/World frame. The tooltip explains this.
Toggling alone does not edit the document.

Existing Scale/BaseScale storage is reused without a format change. Untouched
components retain document precision. Relative scale remains 1–400%; pending
absolute source scale retains its existing positive range. Numeric dragging still
uses 1–400%, as before. Invalid fields are marked red.

Group transforms stage every output before commitment. Axis permutations and
rotations in equal-scale planes are accepted. Shear, invalid scale and overflow
reject the complete operation without changing data/history; the shear message is
localized. Unity float error near 180 degrees is tolerated while actual 45-degree
shear remains rejected. Free supports are not scaled. Transient attachment points
use the same transform as the parts. Existing Array spacing semantics are retained.

## Evidence

- Normal/QA builds: zero warnings/errors; Geometry, Store and EditorBridge passed.
- Unity Workbench: 372 screenshots, UTC `2026-10-09T14:01:10.2858444Z`, four
  resolutions at 100/120/140% UI scale.
- Actual TMP fields/toggle clicks: independent Y, proportional Z, multi-selection
  X, invalid values, single history entry, Undo/Redo and save/reopen passed.
- Geometry checks compare transformed corners, rotated frames/equal-factor planes,
  native float180, atomic45 refusal, seed/sign/base preservation and pinned-base
  overflow before any part mutation.
- Localization885, HostContract Steam25185596, QA compiled/profile guards and
  git diff checking passed. Format exit0 retains the existing workspace warning.
- Independent correctness/Ponytail review found no blocker. The verify helper is
  unavailable; direct diff review and the checks above were used.

Preparation exposed and fixed lower controls clipped at 1280x720 and long Russian
camera hints. Inspector minimum height is472; hint groups use font size10–12.
Early runs also caught compilation and initial selection/frame-wait mistakes in
the new test; these were corrected before the successful final run. Check results
and hashes: `artifacts/checkpoint-0.19.117/xyz-verification.json`.

## Still open

Installed QA `.116` remains unchanged, SHA
`867E5E474A3C1B335500E47F1B2DC3A6250AC1FAA36E6D92E766E904F33DCF3A`.
No deployment/game/save/network execution. Native construction, snap/collisions,
stability and world reload/network are not established by this checkpoint. Keep
the user's accepted cost/refund/reload case; the reported strange world snap stays
open. Next: automatic supported QA fixture, delete/hide, visible missing-part
replacement, tree filters and interior camera. Fogv11 and the full goal remain
incomplete. Request new game acceptance only after a verified deployment/fixture.
