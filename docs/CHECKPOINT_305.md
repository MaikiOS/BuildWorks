# 305 — exact refund pickup in either order

22 October2026. Installed **QA0.19.273**, TerrainRamp-1.0-Test only.
BuildWorks.dll SHA256: `2A5F8CBAB7D540592BDB0F8F2712913D6FDFE772B9AAAFEEEF5AFA029155CBD4`.
[Detailed Russian evidence](CHECKPOINT_305_RU.md), [full goal](GOAL_FULL_RU.md).

Collections now uses immutable Items indices with null slots for rows whose credit
has never started. Legacy prefixes read unchanged. The .272 reader rejects sparse
journals; downgrade needs a separate compatibility check. Physical source overlap
is unchanged. Exact hovered source/blob/properties, payer/world/custody/profile,
one-intent and paired save/generation guards remain. Empty slots cannot be credited
or cleaned by confirmation; cleanup/finalization skips them without demanding receipts.

Independent review found a missing early admission check: another unsettled credit
could reach a baseline profile save before BeginCredit refused it. Fixed before any
game launch: the constructor refuses before snapshot/scans; journal rechecks before
writing. Successful new admission clears stale Failure. The QA diagnostic compares
inventory against ProfileCredited rows without pretending index order is chronology.

Old-source RED; payout200/cancellation64/admission7 plus guard-removal negative
control/interaction63/finalizer57/board16/router30 PASS. Normal and QA builds have
zero errors/warnings; HostContract/geometry/localization1127 and independent
correctness/Ponytail reviews pass. verify skill is unavailable.

Native isolated QA project9d2568de03664632ac211e673fa1db2d:
ordinary plan confirmation → E/Continue → two committed walls/4Wood67→63 →
E/two cancel confirmations → saved removal without native drops → separate
E/two issuance confirmations → saved issuance/terminal/no board → row1 E first:
65Wood/seven receipts/cleanup saved/null row0 → normal quit → fresh PID57920:
9/9 and115unchanged project hashes → row0 E last:67Wood/eight receipts/both clean/
zero sources → normal quit → fresh PID50652:9/9, exact27inventory records,
67Wood/eight receipts/zero sources/no board/115unchanged hashes. Hammer equipped
again after final proof; normal quit. These are virtual input/UI checks, not owner
physical mouse/keyboard acceptance. QA aiming adjusts the existing player's view.

11PASS packages/59steps. Independent .fch SHA512/inventory extraction matches exact
AfterBytes for sparse index1 and final index0; custom-data-only production reader
separately matches seven/eight receipts. Final Hammer equip follows those byte proofs.
109old project hashes unchanged; six new files belong only to the new project.
Ordinary4141files retain membership/size/mtime; full hashes were not taken. Effective
configuration unchanged. Three normal exits and three verified three-DLL bridge
restores. No grants/resets/terrain edits/network session. Local evidence lives in
artifacts/checkpoint-305 and ValheimQA runs142218/143123/143544.

Other item properties/types, interruption windows, rollback, secondary collapse,
owner UX and remote authority remain open. No full-goal completion is claimed.
Next8.7–8.8: property-sensitive refunds and targeted recovery. Missing-workbench
pause text remains misleading. Surface303 and EarthWorks boundaries are unchanged.
