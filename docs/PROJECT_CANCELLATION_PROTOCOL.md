# Paid cancellation: boundaries and implementation plan

Confirmed behavior: actual paid items with their own attributes drop near the board. Unpaid plans simply disappear. No restored historical attributes, duplicate refund or terrain rollback. Future shared board/storage belongs to the separate [EarthWorks boundary](EARTHWORKS_BOUNDARY.md).

This is a technical draft, not an implemented feature or newly approved UI. First scope: private Local host, same paying actor and verified vanilla structural pieces. Remote authority, contents and arbitrary IRemoved effects require separate proof.

1. Freeze continuation, reconcile pending execution; persist cancel ID/project revision/part receipt/world/payer/exact PaidRows. Unknown state blocks cancellation.
2. Validate each uniquely owned live part; persist deletion intent, suppress normal native drops, delete, save world and scan the complete mapped set for target absence. .237 implements only the final read-only scan primitive.
3. Persist IssueIntent for each row. Spawn an ItemDrop near the board, sealed against collection and tagged in ZDO with cancel/row/receipt identity in the early spawn path, including throw-after-Instantiate. There must be no untracked spawn/tag window. Verify exact native item bytes and a unique saved drop before Issued. Issued is not Collected.
4. After every row is durably issued, complete cancellation and remove board/fog, retaining the delivery ledger. Guarded pickup persists CollectIntent and exact inventory-before, credits the supplied item into a verified empty cell without native merge, verifies saved profile, then removes the drop and verifies saved-world absence before Collected.

Crash policy: a part already absent may have produced normal drops. Unknown deletion, missing IssueIntent drop, duplicate/merged drop, partial collection or Issued drop without collection evidence must freeze the affected entitlement, never automatically regrant it. Absence alone never proves no prior delivery. Resume only an unambiguous recorded before/after branch.

Native FindFreeStackItem does not compare all customData/variant; item customData alone is not durable refund identity. ZDO tagging plus guarded pickup/auto-stack/despawn/cleanup and disabled-mod behavior remain to be proven. Early tagging and exact ItemDrop.SaveToZDO readback are blockers before enabling delivery.

Next slices: durable cancellation journal/fault transitions; controlled deletion/drop suppression/save evidence; sealed tagged issuance and collection; native crash/reload checks, then server. Paid cancellation remains blocked until the complete protocol is proven. File reads, RAM flags and absence evidence are insufficient alone.

## Next durable completion slice — 2026-10-20 draft, not implemented

Player cancellation must finish after all refund drops are durably issued, before collection. Building/fog/board disappear; collection and old-source cleanup continue independently. Settling every return is a later audit and must not delay that visible cancellation milestone.

Architect proposal: preserve rev1/rev2 bytes, add rev3 paid-cancelled/returns-pending, then rev4 paid-cancelled/returns-settled. These are proposed format/API revisions only. Rev3 binds immutable removal/issuance data and fresh construction absence; its issuance fingerprint excludes mutable Collections. Receipts, credit and payout-drop absence are not rev3 prerequisites. Rev4 adds every ProfileCredited/CleanupSaved, permanent receipts and the .250 two-set composite absence scan.

Rev3 does not recreate the board but must permit collection/cleanup without OpenBoard; rev4 permits read-only reconciliation. Unknown outcomes, rollback, reappearing sources or missing receipts freeze without reissue/rebuild/delete. Metadata transitions themselves have no native world/inventory effects. Coordinated rollback of every file still needs an external monotonic anchor and is outside the claim.

The initial single-rev3 proposal requiring all drops already collected was rejected because it violated player-visible ordering. Prove legacy compatibility, both atomic transitions/replay, board-independent collection and startup/fault/rollback with host substitutes before native verification. The two already-collected Wood debts are only a scoped QA case.
