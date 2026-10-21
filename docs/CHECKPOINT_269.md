# Checkpoint269 — GPU receipts checked, meaningful acceleration unconfirmed

2026-10-21. **Local candidate, neither deployed nor published as a working fog fix.** Installed QA .250 unchanged. Isolated Unity checks ran after Ostrix reported leaving the game; no Valheim launch.

One `PumpSources` collects immutable owner/slot/token entries after successful `FinishStep` submission and requests one shared `_Receipts` read. Each owner keeps16words and complete identity checks. `PollSnapshots` remains the sole bank/tick/history commit boundary. Direct single submissions remain immediate. A partial failure flushes the submitted prefix in `finally`; request/read failure preserves unknown outcomes and history. Velocity/recovery/final fences, shader, quality and queue limits are unchanged by this step.

Separate physical request/byte counters preserve the distinction from logical receipts. Normal/QA builds have0warnings/0errors;geometry,host/localization and CPU receipt guards passed.18additional CPU type checks reject string/double/bool/null counters. The `verify` skill is unavailable; direct checks and independent correctness/Ponytail review were used.

Actual GPU guards passed33/33:28owners/slots4–31,empty/mixed,direct/immediate,short/error,request-creation/partial failure,duplicate/stale/ABA,pending Dispose and combined receipt/velocity retirement. First launch failed CS1654 in the test NativeArray; try/finally disposal fixed the test,not production. Both records retained. Successful repeat made one physical request for28owners. Affected parity passed664fields/498ledger/252paging/7logical-source and14frozen RGB delta0.

ABBA with identical captured history:4warmup+48measured renders/arm,28procedural objects/32resident. A5532.40855ms versus B5519.3223ms:0.236538%less,within paired-arm spread.784field/ledger+560scalar comparisons passed;1344→48physical reads per48renders. **Meaningful acceleration unconfirmed.** CPU+GPU+readback-drain elapsed; imposed maxQueue1 does not prove native headroom/FPS/house appearance. Independent evidence review passed. Next:[271](CHECKPOINT_271.md);no unchanged full52s house without measured headroom.

Evidence:`artifacts/checkpoint-269/receipt-20261021-085951-11b97919/`,`receipt-measurement-20261021-091033-9a6be22d/`,`timing-summary.json`,`verified-before-271/` frozen source/reports. Prior generic proof folders backed up before overwrite. No deployment/new owner test assignment. [Checklist](LIVE_CHECKLIST_RU.md),[parallel270](CHECKPOINT_270.md).
