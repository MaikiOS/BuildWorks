# Checkpoint286 — native renderer and first candidate game window

2026-10-21. Independent Astra review used `gpt-6-astra`, effort `ultra`. Candidate285 was temporarily attached to the preserved real Valheim house. **Full comparison FAILED; neither fix nor visuals are accepted.** Installed QA .264 DLL/hash remain unchanged. [Русский](CHECKPOINT_286_RU.md).

| Completed window | Mean frames/s | Median / p95 frame | Production renders |
| --- | --- | --- | --- |
| Candidate, approximately4s rest |138.18 |6.03 /13.37ms |555 |
| Candidate, approximately15.3s route |44.27 |10.96 /79.29ms |681 |
| Previous shader, approximately4s rest |23.89 |36.40 /61.18ms |98 |
| Previous shader, next contact |Did not finish |No valid result |— |
| Surface-only |Not reached |— |— |

These are actual non-batch Camera.main renders on Direct3D11 at3440×1440, target=0, without additional `Camera.Render()`. The native camera controller was temporarily paused for one fixed diagnostic view; pose/FOV/aspect/enabled state are owned/restored by the probe. Each completed interval validates matching mode/shader/camera/fog, active state, dimensions and advancing production/camera counters. Candidate route ended with28visible parts and null Failure/PassFailure. Its2s hold is at the house centre, not within wall material. Mean44fps with p95≈79ms does not prove smoothness. No exact contact speedup ratio, GPU timing, displayed-frame latency or owner visual acceptance is claimed. New candidate game images were not recorded; editor movie285 is separate evidence.

Two bounded batch probes—screen camera and automatic RT—produced zero camera/production renders. Their11/11 and12/12RPC passes are not performance proof. State restored in both; no further batch rendering attempts. Astra recommended the normal windowed GABS launch without desktop input.

The first windowed load overflowed the old fog input queue before candidate attachment; generic ready/wholly-unpaid refusal also covers FogError, not evidence of payment changes. One normal logout/reentry then applied candidate immediately during load without resetting the world.31steps completed; step32in the subsequent old-shader contact stopped answering. Cleanup and a single normal logout also returned `The game's main thread did not answer in time (loading screen or paused?). The action may still happen when the game catches up.` No further game commands/retries. There is no new overflow/exception/DXGI/TDR after this route starts; deadlock or its exact cause are unproven. Baseline rendering back-pressure is an architectural hypothesis.

**Cleanup:** owner separately approved emergency termination of own QA PID67688; exit confirmed,3bridge DLLs restored and SHA-verified, no game/editor processes.97project hashes,4141ordinary save membership/size/time records and8cfg values unchanged; ordinary save contents were not hashed. Final normal save, post-route inventory and temporary shader/camera cleanup before emergency exit are unconfirmed. First two processes exited normally and restored their no-render probe state. Installed .264 unchanged. Initial post-kill guard reported `QA process remains live`; restoration was deferred until the next check confirmed exit.

Linear2-shader bundle reload/names/support/SHA pass: `864E7AC014E82B28F8E0D36A9A051B169D826C753F6AAE8B26C6C560AE3E399F`. External QA helper build0warnings/0errors, Python7/7, completed-window analyzer with6negative controls PASS; correctness/Ponytail review addressed4blocking findings before affected runs. Expiry/session/peers/teleport/respawn guards, fixed-camera ownership, saved-object cleanup and mutual exclusion of manual captures are explicit. Watchdog cleanup cannot run while Update is blocked. `verify` skill unavailable; no full production build/regression repeated for external diagnostic changes.

Evidence under `artifacts/checkpoint-286`: original scenarios/reports, three completed cadence files, `native-screen-summary.json` (`analysisPassed=true`, `comparisonPassed=false`), failed cleanup report, logs and bundle verification. Transport FileExistsError, positional-wildcard rg error123 and non-repository ValheimQA git lookup were preserved/reported; no evidence was silently overwritten.

Process/bridge cleanup is complete. Next: candidate versus surface-only plus a visual contact recording without repeating the hazardous baseline contact. Investigate long frames and preserve v11 appearance before a new owner build.9.2–9.7, full paid build/cancellation and network remain open.
