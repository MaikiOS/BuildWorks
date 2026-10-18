# .218 — strict v11 connected to the real-house QA renderer

2026-10-18 · [Русский](CHECKPOINT_218_RU.md) · [Full checklist](LIVE_CHECKLIST_RU.md)

QA **0.19.218.0** is installed only in TerrainRamp-1.0-Test; SHA `6522F72375A7567AD3F2D10378E5CA08FFF347A0E41729BCCA42F423B9F79A6A`. Public binary and embedded bundle are unchanged. The house QA controller alone loads the candidate shaders/paged pool; construction state, costs and permissions are unchanged.

## Change and verification

Building now supports the strict .217 pool/full domains and published atlas. Bindings precede geometry (initial preparation plus OnPreCull), body observations follow changed30Hz ticks while scheduling/readbacks run each Update. Geometry-centred domains account for native prefab scale/TRS independently of pivot, retaining a common noise frame. Legacy remains. Render bytes include static atlas and dynamic pages. Normal construction projects do not yet use this backend.

Normal/QA0warnings/0errors; Geometry131, host contract, compiled QA isolation and localization1104/991 passed. Receipt checks1735negative/32metadata, parser0. `verify` skill is absent; independent correctness/Ponytail review PASSED/Ship. Unity adds **4 image comparisons** against two independent dense-v11 solvers: rest/contact/hold/initial exit, distinct rotations/scales/exactTRS, no colliders,24ticks with queues0/confirmed tick/flags0. Previous56field-ledger/18image,14runtime/15negative and legacy checks still pass with unchanged tolerances. This fixture is not whole-house visual acceptance.

Native ValheimQA used the existing **QA194 Forest Hut (28)** without new cards. Background/batch/root=true, cloud=false; connection6.282s, entry17.187s. All28 logical owners/seven resident; fields94478116/98304000, render128190720bytes. Five actual Camera.Render PNGs; background host frame count is not FPS. Last active observation clock364.873s/minimum confirmed tick10937/flags0/no Failure; queued68 are in flight, not proof of complete drain. The real character approached/held in the wall plane/passed inside/exited. Wall8 confirmed emission revisions687→1947→1957/generation1; advancing history and images do not measure GPU-density recovery. Ghost remained present. CloseFogVisual yielded inactive/zero part/field/render/RAM counters; inventory/backpacks unchanged. Zero counters do not independently inventory driver allocations.

## Failures and boundaries

Creation exceeded the bridge's internal15s main-thread timeout. It was not repeated: live PID/connection and subsequent actual state proved eventual completion. Source preparation is synchronous and too slow: **integrationStatePassed=true, scenarioFullyPassed=false/startResponsivenessPassed=false**. Two separate tool mistakes (instance method addressed as a type; wrong render-probe DLL name) were corrected after inspecting source/file names and retained in JSON.

No fixed-camera continuous animation/reference comparison, numeric native surface/smoke recovery, actual render FPS/GPU timing or full GPU memory measurement. Five PNGs cannot establish smoothness/fidelity. Mono's process-memory property returned0/unavailable; external Get-Process sampled5.375GB private/4.069GB working before first render and4.618/3.759GB after visual closure/during logout. Different phases without baseline do not measure incremental effect cost. Owner previously rejected .212; this does not replace acceptance. Durable project/board/reload/server remain open.

Normal logout→main_menu→Application.Quit, own PID65176 exited, three bridge DLLs hash-restored. No grants, resets, construction, terrain or hostile removal. Library/provisioning/token preserved; normal QA save/session changes expected. Config changed only version header217→218;4137 regular-save files unchanged by size/mtime, not content hashes.

Private evidence: `Default/ValheimQA/artifacts/20261018-114638` (scenario/native-receipt/logs/five PNGs/restore/hashes), not published. Unity: `artifacts/fog-published-render`; source manifest: `artifacts/checkpoint-218`. No extracted game models/approved lab archive in public source.

Next: remove synchronous setup stall; record the complete house continuously from one camera with numeric contact/hold/recovery probes, measure actual rendering/memory and compare approved v11. Then durable project/stake-board/resources/refunds/reload/server. All14 checklist blocks remain; no new owner manual fog series yet.
