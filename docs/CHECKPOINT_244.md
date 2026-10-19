# .244 — native house contact/recovery with sparse images

2026-10-19 · [Русский](CHECKPOINT_244_RU.md). Installed QA remains0.19.241; no code/DLL deployment.

Existing isolated Valheim world, actual unpaid28-part Forest Hut project and board, existing ValheimQA/GABS route. No resource grants, construction or terrain changes.

| Check | Result |
| --- | --- |
| Preparation |6/6,29.3s;28 visible nonphysical parts, board, unblocked input |
| Unrecorded route |14/14,19.1s; approach/enter/hold/exit/recovery/complete |
| Contact snapshot and two separate images |24/24,15.3s; no continuous capture |
| Hold state |12 active parts;127241 nonzero smoke/19087 surface texels;354624 exact published inner-texel comparisons |
| Recovery |All28 parts: zero ledger flags and empty smoke/surface pages/nonzero/max |
| Cleanup |Only own CancelUnpaid project; zero parts, no board, input unblocked |
| Data protection |Inventory identical; same4137 regular save files/sizes/timestamps, not content hashes |
| Exit |main_menu → Application.Quit; PID40120 exited; bridge3/3 restored |

Initial Create refused before writing a project because the previous route left the player away from a suitable flat/loaded/free site. Historical receipts identified the original yard start. Only the player was teleported; the same strict site checks then passed. Terrain and old buildings remained untouched.

Both images were inspected: recognizable house remains visible, light contact fog and restored form afterward; cameraRestored=true. They do **not** establish full animation or owner visual acceptance. Captures cost130.9ms/103.0ms; hold readback118.6ms plus24.3ms analysis. These are not GPU execution durations.

Only QA saves and own cancelled project707092bbb7034c9fb7be7313f671af34/lock in QA-world1137745799 storage changed. Project checksum/identity/world/revision2/nonempty cancel operation verified; the cancellation record was retained. Both route reports have no warnings/errors. Early optional Backpack API warnings remain in the initial site-refusal report.

Still open:48s low-cadence/continuous capture, full animation and owner acceptance; prior243 queue64 failures remain valid. Paid cancellation/physical refunds, crash recovery, special objects/contents, permissions and networking remain open. Next separate native cadence from capture cost without raising queue limits or discarding history. All14 goal blocks remain.

Evidence: `../ValheimQA/artifacts/20261019-202907/`: scenario, initial refusal,6/14/24 full reports,3 metrics,2 PNG/frame manifests, logs, verification, process exit and bridge restoration. Report parsing was corrected for .NET fractional timestamps under Python3.10 without rerunning gameplay. New-file auditing allows only the exact own cancelled project, not arbitrary server storage.

Goal API was active at turn start but the final read returned **paused**. No `update_goal` call was made. The available API has no resume operation; resume through the UI. Editing documents neither updates nor activates the goal.
