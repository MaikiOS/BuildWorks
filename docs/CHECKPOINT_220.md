# .220 — four recorded passes through one long wall

2026-10-18 · [Русский](CHECKPOINT_220_RU.md) · [Full checklist](LIVE_CHECKLIST_RU.md)

QA **0.19.220.0**, installed only in TerrainRamp-1.0-Test; SHA `86B01BFCD04CF369AA019B9D89E1327396E6116DED752202B091C50BA893C1C0`. No cost/permission/durable-construction changes. Public package and embedded bundle unchanged.

One transient8m wall comprises four adjacent native woodwall sections. The real player crossed front/back and along from both ends, with2s holds and8s recovery observations. Fixed Camera.main recordings restore pose/target/FOV/active texture per frame; no desktop input. Preparation18/18 and traversal39/39, optional failures0,52.1s, no scenario errors. Four clips contain82/75/94/81 frames over13.09/11.28/14.29/12.75s, each with enter/hold/exit/recovery. `recordingValidationPassed=false` remains explicit: reverse routes start at their entry point and omit a separate approach frame. This is not comprehensive phase coverage or visual acceptance.

The old8192 shared reserve failed twice along the wall. Retained GPU receipt proves flags2: free2800/wanted2834, slot0/sequence2551/tick2701. First rejected/unknown receipt, CPU token and pre-Complete queue context survive cleanup, including synchronous submission exceptions. Native log/inspect confirmed retention after disposal. A nested DTO was omitted by native HouseStatus; final status uses `firstFaultJson`, currently native-tested only with an empty fault on the successful series.

The four-wall fixture now reserves4×8192=32768 blocks with the same4resident owners: field81,769,060/98,304,000B and combined render74,580,096/134,217,728B in post-traversal states (58,335,552B before first render). House28 retains8192. This is a bounded fixture correction, not universal house-capacity proof. Domain/solver/precision/safety unchanged.

Normal/QA0warnings/0errors; Geometry131, current host/localization1104/991/isolation/QaControl43/45 passed. Unity Recovery90frames adds6 retention cases: real GPU injection8, synthetic cached1/2/4/wrong identity, real dispatch exception; context/deep copy/accepted history/double disposal verified. Independent correctness/Ponytail review found no blockers; no installed `verify` skill.

Bridge numeric JSON vectors no longer round-trip through locale text; IEnumerator inspection no longer executes a route clone.58 compiled tests cover en-US/ru-RU/de-DE and iterator safety; transport4/4. QA-only SetControls postfix retains scripted movement only for its active isolated owner. Bounded capture600frames/256MiB/65s stops on faults; not a render benchmark. Earlier locale/observer/stale-failure/collector errors remain documented in private artifacts; no unknown mutation was blindly retried.

Full inventory/backpacks unchanged, closed counters0, normal logout/menu/Quit, PID57864 exited,3bridge DLLs hash-restored.134/143 protected paths retained hashes; remaining9 are ordinary QA save/control changes/rotations.4137 regular-save files unchanged by size/mtime, not content hashes. No grants/resets/native construction/terrain/hostile removal. Native recordings/reports/audit/logs are private under `Default/ValheimQA/artifacts/20261018-135752`; exact failed receipt under134800, earlier failed runs under130446/131716; source/installation/Unity under `BuildWorks/artifacts/checkpoint-220`. No game pixels/assets shipped to GitHub.

Still open: numeric published surface/smoke recovery, continuous approved-v11 parity, renderFPS/GPU timings, house reserve, owner acceptance, durable project/board/reload/network. Capture is about6fps, peak captureCPU123.56ms; not game FPS. Next compare the long wall against v11, then house; durable stake/board follows. All14 goal blocks retained and goal active.
