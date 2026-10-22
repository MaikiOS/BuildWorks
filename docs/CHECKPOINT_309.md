# Checkpoint309 — refund full-inventory feedback

22 October2026. Candidate **0.19.275 is not installed**. Verified installed QA remains **0.19.273**, SHA `2A5F8CBAB7D540592BDB0F8F2712913D6FDFE772B9AAAFEEEF5AFA029155CBD4`.

Sealed refund collection runs asynchronously after Interact. A full inventory previously caused a safe refusal with only a logged reason. The unchanged refusal branch now sends a localized Center message asking for one free slot and retains the same failure text. RU/EN agree. Payment, receipts, item properties and row-major free-cell selection are unchanged; no stack merge is introduced.

The gate remains after the existing baseline profile save and before `BeginCredit`/`AddCell`. No-credit-intent/insertion is claimed for this branch; absence of all profile writes is not claimed. The sealed source remains available, but native full-inventory retry has not been tested.

Identical test with only its source path redirected to clean39bf582 reproduces missing feedback/exit1; log and old-source SHA receipt retained. Fixed actual block7PASS covers full/zero-size inventories, exactly one Center message, matching retained reason and unchanged free-cell selection. Host boundaries are substituted. Normal/QA builds have0errors/0warnings; HostContract and localization1130 embedded keys PASS. Initial sandbox NuGet access was denied; unsandboxed build passed. Independent correctness/Ponytail PASS, reviewer reran7checks. `verify` absent.

Evidence: `artifacts/checkpoint-309/` before/after test logs/receipts, normal/QA build logs, host/localization and candidate hash report. Candidate .275 also includes306 station/DLC pause reasons and supersedes local .274. Owner PID57236 remains live outside sandbox; no Deploy/Prepare, launch, game/save/profile intervention. Native messages, free-slot retry, prepared306 controlled saved-credit interruption/fresh recovery/cleanup/replay remain next. No new grants/reset; full goal and network/other-item acceptance stay open.
