<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

**Current2026-10-11:** QA .157 installed after normal game exit; both DLLs/155 existing config/import/save files verified, Default clean. No agent game launch or new native acceptance. [Deployment and next steps](QA_DEPLOY_2026-10-11.md). Earlier source/native observations below remain historical.

# .156 — QA replies after the selected UI object is destroyed

Code **19b520b**, normal/QA built, **not installed**. The running game remains .146.
Goal active; no deployment, main-save changes or push.

Ostrix launched the isolated BW_QA_43d3719a world and authorized testing. Fresh
state, a short look/step, released movement, inventory, Hammer catalogue,
blueprint tab and import dialogue were observed. The test-fixtures folder click
(request1791740604598) timed out: Respond accessed the destroyed selected
GameObject through `?.name`. The error handler repeated the same access.
stop.flag was sent and consumed; no terminal receipt could be published.
Readonly state1791740676209 failed identically. No input was replayed and no
construction/resource debit was attempted. The game was left open.

Evidence: `artifacts/qa-native-2026-10-11-146`, including original responses,
screenshots and game log. The unacknowledged screenshot is not completion proof.

Respond now snapshots EventSystem, selection, module and ZNet, using Unity alive
checks before access. Missing/destroyed observation fields become null without
changing request identity, status, safety, permission, resource or input rules.
The stale cancellation contract now follows the existing .150 helper chain;
an in-memory disconnected-call negative control proves it rejects a broken path.
World-selection production code did not change.

Actual extracted Reply+Respond fails on the old code and passes11 CPU-shim cases
on .156. Normal/QA0 warnings/errors; protocol28/publisher, client100 replacements,
Host25185596, compiled control/native input/cancellation, isolation, localization989
and scoped format passed. Correctness PASSED; Ponytail Ship. verify unavailable.
Logs/hashes: `artifacts/checkpoint-0.19.156/verification.json`.

No Unity/Workbench execution or native acceptance of .156. Geometry/Store and
EditorBridge were unchanged and not rerun; .155 results remain separate. Formats
native22/portable11 and converters are unchanged; short Info/legacy inventories
remain research, not new adapters in this release.

Next: close the old game normally, guarded deployment of verified QA≥.156,
Thunderstore TerrainRamp-1.0-Test → Modded, same closed QA world. Recheck fresh
state, Hammer → blueprints → import → test-fixtures folder, parent/folder roundtrip
and closing, with matching terminal responses and no NullReference. Only then
continue the [prepared storage/mount series](QA_0.19.144_RU.md). Exact inventory
baseline, resource/item behaviour, drag/physical stop/focus, reload and network
remain unverified. Do not repeat unrelated accepted user cases.
