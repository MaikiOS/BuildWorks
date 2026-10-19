# Diagnostics242–243: no whole-house speedup

2026-10-19 · [Русский](CHECKPOINT_243_RU.md)

Game version remains0.19.241. Neither experiment fixed whole-house fog; no game DLL was deployed merely to change its version. Valheim did not run in this checkpoint; saves/resources were untouched. All14 goal blocks remain in the live checklist; Goal API active.

| Experiment | Correctness | Load result | Disposition |
| --- | --- | --- | --- |
|242: two confirmed batches before velocity eviction |9 cases/210 exact fields, existing252 paging comparisons/38 lifecycle checks; reproduced and fixed cold-owner deadlock |FAILED queue64 at21.283s;16466 confirmed ticks |Archived; removed from working runtime |
|243: alternate three matter fields between ticks |6 cases/84 exact fields,32 copies versus104 for the same history; velocity predictor, canonical bindings and memory bounds retained |FAILED queue64 at22.776s/35.871hostFPS |Archived; removed from working code/fixtures |
|243: fresh old-copyback control |Same28 procedural walls,32slots/32768blocks/Spatial3D/4resident; report control=true |FAILED queue64 at38.697s/35.920hostFPS |No speedup established; repetitions stopped |

Final traces contain17606 candidate/31047 control confirmed ticks; submitted totals are larger and must not replace confirmed work. These individual run durations are not a statistical benchmark or game FPS. Average receipt request→callback latency was35.716ms versus33.783ms; this is **not GPU execution time**.

## GPU timing attempt

A senior architect verified the actual host DLL APIs and primary sources. An Editor-only `CustomSampler.Create(name,true)` brackets BeginStep through FinishStep, before readback requests. Raw rows retain hostFrame, documented gpuFrame=hostFrame−3, GPU blocks/nanoseconds and actual submitted batches/ticks. Attribution requires matching batch counts; CPU Stopwatch is never substituted.

Both20s probes failed closed with `GPU batch timing unavailable or frame association unconfirmed; raw frames retained.` Recorder-only had compiled/support/sampler/recorder validity=true but all GPU counters zero. The separate GPU-area control really enabled that area and still returned only zeros. It restored the previous setting (`gpuAreaRestored=true`); global Profiler was not enabled.

**No GPU duration was measured.** API availability/support does not establish timestamps in this batchmode. The backend reason remains unknown; zeros were not reported as cheap GPU work. Repetitions stopped. Sources: [CustomSampler](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Profiling.CustomSampler.Create.html), [GPU timestamps](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Profiling.Recorder-gpuElapsedNanoseconds.html).

## Isolation and evidence

Failed prototypes and the timestamp probe are archived locally. Editor-only isolation was verified first, then all experimental runtime/Workbench/fixture edits were restored to verified710bcb6. Complete source snapshots remain available; no failed experimental code remains in working sources. Normal/QA retain their original AdvanceTo/receipt path. Shader bundle unchanged: `E734631FB7DE49C00237C1F0F4DBB28DFA491DF6DF177C7D79353BE354C4AA78`.

Normal/QA builds0warnings/0errors; repeated exact GPU6/84 plus existing252 paging/38 lifecycle PASS before archiving. CPU1775 negative cases, receipt gate8 modes/96 negatives and positive control/timing fixtures PASS. Host/localization1122/compiled QA save isolation and IL8 methods/handlers plus solver AdvanceTo PASS; experiments absent from normal/QA. These checks do not turn failed48s/timestamp probes into PASS. An attempted `scripts/Test-TestStandSeparation.ps1` does not exist; the existing `Test-QASaveIsolation.ps1` was used. Its first temporary-junction attempt returned Access denied; an authorized test-directory access rerun passed.

- `artifacts/checkpoint-242/`: source snapshot, exact correctness, deadlock reproduction and failed48s.
- `artifacts/checkpoint-243/`: paired/control results, two timestamp failures, builds, validators, IL and final verification.json.
- Raw load runs: `artifacts/fog-production-frames/AsyncCaptureCandidate-20261019-200425-317d3464/`, `AsyncCaptureCandidate-20261019-201037-228f7a01/`.
- Raw timestamp probes: `artifacts/fog-production-frames/TelemetryMeasurement-20261019-201539-dfbf7899/`, `TelemetryMeasurement-20261019-201838-53e62116/`.

Independent read-only correctness/complexity review found no blocking probe defect. That is not proof of valid GPU measurements or improved performance. `verify` is unavailable; actual checks are recorded in verification.json.

Next separate actual game behavior from video capture cost: native ValheimQA house state/recovery with sparse control images, without continuous recording. This does **not** remove visual/smoothness acceptance or relax the48s gate. Capture/low-cadence throughput remains unresolved. Stable house precedes paid cancellation/physical refund; terrain integration remains deferred.
