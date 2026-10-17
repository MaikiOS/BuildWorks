<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# QA .144 — independent contents fixtures

Installed only into TerrainRamp-1.0-Test; source9b40045. Evidence: `artifacts/checkpoint-0.19.144/{verification,installation}.json`. Both DLLs and new fixtures24/25 match SHA;137 existing config/import/save files unchanged, Default clean. Valheim was not launched and saves were not written.

Two authored portable fixtures contain floor/wall/wood chest/itemstand, empty versus three archived Wood, and archived SwordIron quality3/durability125.05. v109 packages and independent contents policies are tested; native layout/support is not. Separate QA provisioning preflights actual prefab/cost/empty-container/mount eligibility, grants materials for two structures plus two ordinary quality1 swords, and uses `qa-contents-supplies.txt`. Original seven and supported-snap cohorts/receipts remain unchanged; capacity/weight and interrupted-grant safeguards remain. Native save precedes completion. Provisioning types are absent from normal DLL.

Passed: normal/QA0/0, Store import/packages/four policies/immutable originals/portable reopen, localization972, Host Steam25185596, isolation8/25/20/13/15/10/11, replica deployment16 plus forbidden profiles4, EditorBridge, native/compiled input contract and protocol28. Read-only correctness PASSED, UX no gaps, ponytail Ship. Previous Unity492 evidence belongs to .143; unchanged UI/runtime copies were not retested.

Initial fixture validation caught missing mandatory `chance`; the authored files were corrected without relaxing the parser. Portable export tests use the source portable identity rather than the catalog's transient external ID. Localization excludes the new receipt filename through the existing literal allowlist. Affected checks passed again.

Still unproven: live input channel, native grants/capacity, fixture geometry/support, in-game review, supplied-item attributes, resource accounting/cancel/reload/network. Populated-container restoration, ArmorStand and administrator restoration remain open. I05/full goal are not complete. [Exact Russian test steps](QA_0.19.144_RU.md).
