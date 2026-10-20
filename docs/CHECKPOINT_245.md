# .245 — native house backlog without continuous capture

20 October2026 · [Русский](CHECKPOINT_245_RU.md). Installed QA stays **0.19.241**; game code, DLL and bundle are unchanged. Goal API is **active**.

The real28-part Forest Hut prepared7/7 in the existing isolated Valheim world. The route reached `complete`, but the scenario then **FAILED:25/26 in35.4s**, with one part's queue reaching64. Continuous recording was off. One hold GPU readback cost127.1ms plus18.3ms analysis and disturbed this workload.

| Check | Evidence |
| --- | --- |
| Actual contact |9 active parts;67732 nonzero smoke/19494 surface texels;209600 exact published inner comparisons |
| Backlog after route completion |1016 total queued samples; largest source42 →51 →60 →64 over successive4s intervals |
| Failure handling |Accepted history retained;28 visible nonphysical parts and board remain; input unblocked |
| Full post-exit recovery |Not proven in this run: post-recovery metrics were not reached |
| Frame settings |36/0 read back after setting; original−1/0 restored and independently read back |
| This house's frame timeline |Not proven: Unity omitted the first observer's frame array; the evidence gate rejected it |

## Observer repair is separate from house correctness

The existing `ValheimQA/render-probe` library now contains bounded `CadenceProbe`: consecutive real frame/time observations,65s/4096-frame limit, loaded Local QA-session guards and temporary36FPS/vSync0 with restoration on stop/error/unload. It does not control camera, input or simulation. Existing Newtonsoft.Json now writes the frame list that native Unity JsonUtility omitted. No new dependency or second gameplay controller was added.

A separate native run **without a house or movement** passed32/32 in50.7s. Its raw JSON contains1810 consecutive frames spanning50.232s: mean36.013FPS;25 full two-second windows at36–36.5FPS. Maximum frame interval32.50ms,95th percentile29.52ms. This proves the repaired observer in an idle QA scene, not GPU durations, house smoothness or the failed route's retrospective FPS.

QA-library build0 warnings/errors;6 Python tests including14 negative evidence mutations passed. Missing-frame regression also rejects the saved broken native JSON. Current .241 host contract passed. The `verify` skill is absent; these builds, targeted tests, independent reading and native artifacts are the checks actually performed. Ponytail review: one observer reuses the existing library, with no new controller/packages/production API; validation and settings cleanup retained.

## Confirmed behavior and next work

Source and queue growth establish a producer/consumer deficit: each part keeps idle observations, and only a confirmed GPU prefix retires them.28 owners share4 resident velocity slots. Velocity readback/upload is a likely cost, not a measured attribution in this run. Do not enlarge64, discard idle history or reset hidden v11 state.

Next: enable existing `FogPerformanceCounters` before owner creation, separate receipt waiting, paging, CPU-copy/upload and solver work, then fix the measured cause under existing exact-state checks. The same failed scenario was not repeated. Continuous capture and owner visual acceptance remain open.

Parallel read-only cancellation research identified a durable cancel-intent/paid-part manifest as the next safe slice. It is not implemented; paid cancellation/refund/pickup remain closed. EarthWorks boundaries and all14 goal blocks are preserved.

## Cleanup and local evidence

Initial PID44088 never entered a world: Steam was closed, `SteamAPI_Init() failed`, and QA save preparation refused. After normal Quit and verified Steam `-silent` startup, PID10144 entered the existing QA identities. The repaired observer used PID38284. All three quit normally; both loaded sessions logged out through main_menu. All3 bridge DLLs were restored by hash.

Only own unpaid fixture `2bcd10a9adae43c68c02baa0efe1f04a` was cancelled:0 parts, no board, input free. Inventory matched exactly.4137 ordinary save files retained file set, sizes and timestamps; content hashes were not collected. Changes were restricted to QA saves/receipts and the exact own cancelled project/lock. Its checksum, world/ID, revision2 and nonempty cancel operation were checked. No grants, construction, terrain edits or resets.

Local evidence: `Default/ValheimQA/artifacts/20261020-080220/`: scenario/scripts, full reports, hold fields, broken/fixed raw frame JSON, validation, logs, collector, verification, normal-exit and restore manifests. `auditPassed=true` validates the audit; **`gameScenarioPassed=false` remains the failed house result**. Read-only reviews/build logs: `BuildWorks/artifacts/checkpoint-245/`.
