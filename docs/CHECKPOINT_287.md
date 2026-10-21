# Check287 — remaining local-fog delay isolated

21 October2026. Real non-batch Valheim, Camera.main3440×1440/target0, no additional Camera.Render. Candidate285 and surface-only retain the same surfaces, fields, simulation and publication. **Renderer control passed; v11 appearance and performance fix remain unaccepted.** Installed QA remains .264. [Русский](CHECKPOINT_287_RU.md).

| Mode | Rest | Whole route | Active segment2–9s |
| --- | --- | --- | --- |
| Wake-only candidate |148.25FPS |45.87FPS |16.73FPS; all116intervals exceed33.3ms |
| Surface-only |149.02FPS |150.07FPS |148.96FPS;1042intervals |

The authoritative atomic scenario passed55/55: four windows, advancing production/camera counters,28visible parts, null Failure/PassFailure,15.39/15.07s routes. Whole-route averages hide contact stalls. This control directs the next change at wake raymarch, not a solver rewrite. GPU timings and displayed-frame age were not measured; the camera is a fixed diagnostic view.

Rejected evidence is preserved: Windowed was actually3440×1351 and failed the size guard; FullScreenWindow was temporarily used then restored. A stale PassPhase=complete produced an invalid short second route despite35/35 RPC success. The corrected scenario waits for PassRunning=false; the analyzer requires10s minimum routes, a closed observer and no pending measurement. Short-window/missing-Stop/still-measuring negative controls and independent correctness/Ponytail review pass.

Three pilotPNG decode3440×1440 but were taken after the180s observer expiry and show the ordinary scene, not the candidate. Expired/not-ready attempts remain failures. The final atomic scenario requested three candidate screenshots with relative paths: API success did not create files, and Unity logged Failed to store screen shot. **Candidate capture failed; no visual acceptance.** Next capture must use absolute paths and validate actual files, dimensions, decoding and active-observer receipts.

Normal logout/Quit, verified shader/camera restoration, restore3 and zero remaining game/editor processes.97project SHA hashes,4141ordinary-save membership/size/time entries and8cfg values unchanged; ordinary-save contents were not hashed. The final full inventory is unverified because the report truncated its array; command intent is not item-state proof. No project/cost/permission/network changes.

Evidence: `artifacts/checkpoint-287/native-candidate-surface-report.json`, `native-screen-summary.json`, `frame-segments.json`, `route-guard-regression.json`, `audit.json`, both logs and rejected reports; shared run `../ValheimQA/artifacts/20261021-191100`. No production code/shader/bundle/DLL changed for this checkpoint. No full rebuild for documentation; no installed verify skill.

Next Astra-selected correction: half-resolution wake volume only, full-resolution original surfaces/source/composition, depth-aware reconstruction and full-resolution fallback at incompatible depth. No solver rewrite, sparse DDA or temporal history. Compare contact/appearance against fullres wake, then one native control; do not repeat the hung old-baseline contact.9.2–9.7, paid completion/cancellation and networking remain open.
