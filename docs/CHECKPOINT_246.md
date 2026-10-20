# .246 — native house velocity-transfer measurements

20 October 2026 · [Русский](CHECKPOINT_246_RU.md). Installed QA remains **0.19.241**; game DLL, shader, queue and budgets are unchanged. Goal active; diagnostic evidence, not a fix.

Existing QA library `ProjectPerformanceProbe` atomically creates the real project and enables existing `FogPerformanceCounters` before deferred owner preparation. Its guard requires zero snapshot owners. No new gameplay controller or production API.

Only TerrainRamp-1.0-Test, existing isolated identities, cloud=false. Stationary player outside the28-part Forest Hut; no video, movement, construction, grants or terrain edits. Preparation **7/7**; stationary scenario **27/27 in18.5s**; published fields and close **5/5**. These reports have no warnings/errors.

| Delta over18.37255s | Measurement |
| --- | --- |
| Largest source queue |5 →15 →23 →35 |
| Work submitted |1839 batches;14508 observations;14648 fixed ticks |
| Velocity CPU copies |9043968000bytes in1840 callbacks |
| CPU CallbackCopy / Upload |3689.71 /1366.10ms |
| CPU RestoreCreate / PageRelease |216.82 /91.54ms |
| CPU Submit |414.92ms, including SolverSubmission223.44ms |
| CopyEnqueue |6.01ms CPU; GPU execution time unknown |
| Counter validity |All4 valid; zero invalid/stale/unobserved/threadMismatch, unreadable/rejected/submissionFailure |

Nested CPU stages and concurrent request latency must not be added as exclusive wall-time. The57.4s request→callback sums cover concurrent requests, not one stall or GPU duration. One callback belongs to a request predating the baseline; full snapshots retain pending/accepted accounting.

**Finding:** copying velocity to CPU and uploading it back has a measured cost worth targeting. It is not yet proof that removing the copy fixes throughput, nor that the GPU solver is fast. The queue grows while visible matter is zero.

Raw cadence:637 consecutive frames/18.320s, mean34.716FPS at target36; maximum frame interval69.51ms. Original FPS/vSync restored and checked. The unchanged48–65s gate correctly rejects this short window; no48s stability or smoothness claim.

Afterward all28 smoke/surface fields have zero flags/pages/nonzero/max. There are no active inner texels (`comparedTexels=0`), so this is neither an exact active-contact comparison nor recovery after a walk-through. No queue64 occurred in this process log.

Existing `QaFailNextBoard` first exercised own post-commit cleanup. The wrapper refused absent channels and cancelled its newly assigned unpaid record only. The original negative tool report remains **FAILED5/6**: test/run treats any success=false as a tool failure even when refusal is expected. Exact refusal and separate state checks **3/3** verify no fixture/board/input block, unchanged inventory and consumed one-shot flag. Unknown persistence before ID assignment is retained, never blindly cancelled. The close-file preparation initially hit a shell quoting SyntaxError and a no-test-file refusal; no game action ran. A saved file script corrected this; close then passed5/5. The no-test-file refusal is in mcp.jsonl; SyntaxError is in this session's exec_command output, with no separate shell log saved.

The solver already runs on GPU. CPU pages exist because our software field cap is98,304,000bytes, not because the device can hold only4 owners. For32 owners/32768blocks/Spatial3D,4-resident reservation is97,423,684bytes;5 requires102,338,884. All32 resident without upload need230,134,084bytes, a132,710,400 increase. With measured render bytes this is341,264,404bytes/325.5MiB of accounted buffers, excluding source textures/models/driver/rest of game. The existing fully resident path has no VelocityUpload and issues no velocity readbacks. First compare bounded GPU residency without disabling safety guards; a4-owner control fits existing limits but does not establish28-part house throughput. A house-budget change needs its own guarded candidate; normal/installed .241 unchanged.

Paging fallback candidate: bounded persistent native readback buffers, removing the intermediate managed copy while retaining old pages until exact owner/generation/sequence confirmation, upload parity and pending-callback cleanup. Earlier managed reuse240 retained that copy. Reflection confirmed installed Unity overloads for `AsyncGPUReadback.RequestIntoNativeArray(ref NativeArray, Texture, mip, callback)` and `Texture3D.SetPixelData(NativeArray, mip, start)`. API availability is not a compiled/executed candidate or speedup. Exact layout/upload and lifetime tests precede integration. A parallel zero-matter shortcut review remains research only: `MightContact` false-negative safety needs targeted proof first.

QA-library build0 warnings/errors;6 Python tests; independent read-only ownership/deferred-arming review passed. `verify` skill absent. Ponytail review retains the existing fixture/counters/library without duplicating the solver/controller.

Both own unpaid records cancelled; exact inventory unchanged;4137 ordinary save files retain set/size/time (no content hashes). Only QA saves/receipts and the exact two cancelled project/lock records changed; checksum/ID/world/revision2/cancel operation checked. Normal logout/main_menu/Quit, PID43984 gone,3/3 bridge DLLs restored.

**House remains unfixed.**48s/contact/animation with a candidate, paid cancellation/refund and network remain open; all14 goal blocks preserved. Local evidence: `Default/ValheimQA/artifacts/20261020-084356/` full reports, four counter snapshots, raw short cadence, idle fields, logs, collector/audit/exit/restore. Reviews/builds: `BuildWorks/artifacts/checkpoint-246/`.
