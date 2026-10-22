# 299 — service receipts before capture; QA268 installed

22 October2026. A reproduced queue-boundary failure is fixed. The native scenario
passes, but candidate297 provides **no rendering speed improvement** and is rejected
for ordinary delivery. Goal9.2–9.7 and the full blueprint module remain open.

`BlueprintConstructionProjectFog.Update` moves its existing `channels.PumpSources()`
before the project loop. Delivered receipts release accepted history before another
capture. Post-capture `Building.Pump()` remains, so newly accepted observations still
submit in the same frame; total calls are unchanged. No solver/clock/capacity 64,
payment, ownership or network changes. This fixes the delivered-receipt boundary;
it does not guarantee a finite queue survives sustained consumer overload.

GPU guards pass40/40. Two new metadata-only receipt cases distinguish old capture-first
(reject,64history entries retained) from service-first (16confirmed, new sample
accepted,49retained). This is not a physics/appearance acceptance. Compiled-order
checks reject preserved267 and pass normal/QA268; host-contract invokes this guard.
Normal/QA builds0warnings/0errors, geometry, host and localization pass. Initial
NuGet access-denied build failed; the permitted-cache retry passed. `verify` skill
is absent; direct checks and independent Astra correctness/Ponytail review passed.

QA 0.19.268 was installed through the guarded script only in TerrainRamp-1.0-Test.
Ordinary shader/bundle are unchanged. The actual3440×1351 window was verified before
world entry; existing QA identities, UID 1137745799, cloud=false, nonbatch/background DX11.

Native85/85 in106.938s,24negative window controls, four shader/camera closures,
three exact23entry/67Wood inventories. Four windows use the **same route**, not four
house directions. No overflow/errors, extra Camera.Render/PNG, construction or grants.

| Window | Temporary candidate | Active FPS | p95 interval ms |
| --- | --- | ---: | ---: |
| A1 | explicit297 | 13.23 | 114.61 |
| B1 | half289 | 13.40 | 114.41 |
| B2 | half289 | 13.33 | 114.83 |
| A2 | explicit297 | 13.38 | 113.47 |

297 has no meaningful gain. These are actual host/render intervals, not direct GPU
timestamps or a broad building-corpus result. Different aspect prevents direct
comparison with earlier3440×1440 FPS. v11 appearance, contact smoothness and owner
acceptance are not established by state assertions.

Normal logout/main_menu/Quit completed; PID49620 exited, bridge3 restored.
97project hashes/4141 ordinary-save metadata unchanged. Config changed only its
version comment; active settings unchanged. Expected QA-save/control files saved
normally;299 includes no reload verification.

Local proof: `artifacts/checkpoint-299/verification.json`, native report/validation,
four cadence JSON files, QA/config audits, receipt GPU/order/build/host/localization/
geometry/deployment logs. Host/MCP: `../ValheimQA/artifacts/20261022-102017/`.
Installed QA SHA: `D81532AF9E453487F4A672E27B27C610B97984771C80C26380BFAC805CBFDF4D`.

Next: measure the actual expensive native render branch before another depth-formula
change. No owner acceptance task is ready. Paid completion/cancellation, physical E
and remote play remain separate open work.
