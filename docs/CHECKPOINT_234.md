# Diagnostic checkpoint234: v11 queue

2026-10-19 · [Русский](CHECKPOINT_234_RU.md)

**Game version remains0.19.233.** Failure reproduced in Unity Editor;
no runtime fix, deployment or binary release.

| Condition | Observed result |
| --- | --- |
| Four contact/capture combinations, uncapped | 4PASS,126–157FPS, queue≤8;784field/560scalar |
| Contact+capture, cap36,22s | PASS196/140;31.76FPS, queue57,2.27s drain; not sustained stability |
| Contact+capture, cap36, up to48s | FAILED22.13s/31.67FPS, queue64; no exact comparisons |
| Contact without capture, cap36,48s | PASS196/140;35.98FPS, queue15 |
| Capture without contact, cap36, up to48s | FAILED26.08s/31.70FPS, queue64; no exact comparisons |

Production32slots/32768blocks/Spatial3D/4resident,28 procedural parts.
Every owner advances, but recording grows backlog. Contact is not required;
starvation is unproven. Capture960×540 includes Render/readback/PNG/disk,
not pure GPU time. These are not native house assets.

A **test-only**, not GPU-tested `PollingCandidate` explicitly updates pending
readback requests before existing Pumps. No waits, lost history, changed quality,
limits or batches. Production/payment/persistence code remains unchanged.

`scripts/Test-FogProductionFrames.ps1` validates runID/freshness/scope/clocks/FPS,
140scalar/196field comparisons, scenario pairs, capture/polling and source/bundle hashes.
Exit0 with a failed report is rejected. No force-kill; settings restore only after exit.
Unique directories retain failed evidence. CPU suite passed4 modes/1manifest/41negative
cases using synthetic validator inputs. Full Editor assembly compiled offline with
Unity Roslyn, exit0, without Unity/Valheim. Independent reviews passed for diagnostics.

Local proof: `artifacts/checkpoint-234/{production-frames,native-cadence-22s,
native-cadence,native-controls,offline-compile}` and CPU receipt
`artifacts/fog-evidence-fixtures/production-ab53c19e27464a3fa158ceced2a99666`.
Initial failed exit0 remains recorded; final controls PID51540 exited1.

Ostrix uses his game profile: no new GPU/game runs while in use. Next is one changed
capped candidate, then a proven minimal runtime change with late-reply/retirement
checks and native Forest Hut verification. Whole-house fog, paid cancellation,
special objects and networking remain open. All14 goal blocks remain; goal API active.
