<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .107 — actual construction payment and cancellation

QA .107 was installed by the guarded script; hashes match,48 QA save/receipt files and the library are unchanged. The game was not launched.

The existing sequential placement now records actual item-stack debits per created
part instead of refunding the recipe through `Piece.DropResources`. Item properties
are retained. Native mounted contents remain a separate removal path.

The UpdatePlacement finalizer records partial spending even after an exception.
Last-part completion waits until this boundary. Direct blueprint TryPlacePiece
calls without the tracked UpdatePlacement transaction are refused.

Refund requires successful native resource-drop suppression, an owned enqueue of
the original ZDOID, and removal of that exact ID in the same live session. The
native deletion batch is flushed before teardown; deferred Unity GameObject
destruction does not postpone settlement. Ownership is never claimed for rollback.
Pieces without ZNetView are rejected before world placement, preserving editor records.

Uncertain deletion, flush or native item-drop results block automatic refund retries.
This avoids duplicate issuance but is not crash recovery, remote-server acknowledgement
or disk durability. Persistent stake jobs and their server journal remain unfinished.

Normal/QA builds passed with zero warnings/errors; production-ledger tests with
inert host doubles cover actual stacks/properties, free placement, repeat calls,
unconfirmed/ordinary/uncertain destruction and before/after-spawn drop failures.
Host contracts, 854-key localization, QA separation, whitespace and two independent
read-only source/native-IL reviews including ponytail-review passed.

Valheim cancellation, exact real debits, logout/reload and remote-client acceptance
remain open. See [the full requirements/status ledger](WORK_STATUS_RU.md).
