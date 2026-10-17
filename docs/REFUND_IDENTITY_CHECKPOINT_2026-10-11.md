<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .159 — count each source stack once

Source `edf602f`; QA **0.19.159.0** deployed only to `TerrainRamp-1.0-Test`
through `Deploy-TestBuild.ps1`, after dry-run and two independent reviews.
The game is closed and was not launched by the agent. The full goal remains active.

## Reproduced defect and change

A damaged or modded inventory can list the same `ItemData` reference twice.
The previous construction ledger captured both entries. One actual debit from
10 to 6 became two debits of 4, allowing a refund of 8. This was reproduced with
the production-linked ledger; no ordinary in-game occurrence was established.

`BlueprintConstructionRefund.Complete` now rejects repeated source references
inside its existing error handler, before `Recorded=true`. Refund and automatic
rollback removal remain blocked; intermediate `Paid` values cannot be issued.
Retrying does not clear the error. Distinct stacks with identical properties
remain independent. Native costs, payment, privileges and item transfer are unchanged.

On this error an already placed part may remain in the world. That is the existing
error path, not a successful cancellation. Durable recovery remains unfinished.

## Evidence

- Geometry red/green/final: duplicate-reference debit4 cannot refund; retries
  issue nothing; distinct equal-property stacks refund4+2 exactly once. Existing
  free-build, provided-property and uncertain-drop regressions pass.
- Normal/QA builds: zero warnings/errors. HostContract Steam25185596 verifies
  native `ItemData` reference equality; localization989 and compiled QA isolation pass.
- Scoped whitespace passes with a Debug project-metadata warning; `git diff --check`
  and five canonical/mirror pairs pass. `verify` is absent in both skill roots.
- Independent correctness PASSED and Ponytail Ship. Logs are under
  `artifacts/checkpoint-0.19.159`. No game or native inventory execution occurred.

Build and formatter CPU checks were retried after sandbox access errors. The first
protected-file comparison falsely flagged a JSON timestamp automatically parsed as
`DateTime`; the corrected string-date, size and SHA comparison passes for all155 files.
The file set is unchanged. Default plugins are clean.

Installed `BuildWorks.dll` SHA-256:
`5DC5BDF3A8DB3F14793850202F4269F35057713853DDD2A269E23241AD6989DD`.
Geometry SHA-256:
`EC2315A128CA4AF1B19DD5BE69A93B69F18BB3F558F018C6C36A17D0E22C0261`.
Both match QA artifacts;155 config/import/save files retain bytes, size and timestamp.

## Open work

Populated-container transfer remains disabled. Next dependencies are shared N-item
reservations plus next-piece payment, postpayment transfer, observed item custody,
durable recovery and server acknowledgement; see [contract](../specs/roadmap/storage-placement.md).
Native channel/cancel/drop/reload/network behavior remains unverified.
Next authorized QA entry: fresh state/capture and folder/close smoke, then authored
files24/25 and29. Existing .158 instructions remain valid on .159. Do not damage
a game inventory to repeat the new automated regression.
