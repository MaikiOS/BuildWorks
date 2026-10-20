# Checkpoint264 — real rendering exposes the unfinished-house failure

2026-10-20. Installed/native QA **0.19.250** unchanged; no gameplay DLL/shader edit. Only the local external ValheimQA helper changed. This source-only publication contains documentation/evidence descriptions, not its DLL.

## Native result: FAILED

`ValheimQA/artifacts/20261020-201834/`:12setup passes on .250; early rendered-observer Stop7/7 with restored camera/RT/cap/vSync and3camera-ownership refusals. Main side2:52.072s,854host frames/854Camera.Render calls. Complete2s windows before failure show11..14FPS. Production fog then fails `Fog input queue is full; observation rejected, accepted history retained.` Only after failure do windows return to36FPS. Maximum host gap289.32ms. Production fog counter493at cleanup includes7early frames; it is not854successful fog frames.

First route fails; no second route or final recovery-field proof. The independent failure audit passes, the scenario does not. No v11 appearance/smoothness acceptance, GPU execution timings or monitor-presentation measurement. [263](CHECKPOINT_263.md)36FPS remains simulation-only/RenderFrames0; [262](CHECKPOINT_262.md)343/520 recording remains FAILED.

## Helper and isolation

Opt-in CadenceProbe.StartRendered uses one960×540RT and existing Read(camera,target,null) once per host frame, without ReadPixels/PNG/pixel transfer. Old Start remains simulation-only. Camera ownership, restoration and release are checked;65s/4096frames and48..65s/2s-window30..40criteria unchanged. Build0/0,Python7/7, native earlyStop/ownership/cleanup and independent reviews pass. `verify` skill unavailable.

89old project files unchanged; only owned unpaid plan2256a9f3074447808b643258c85672d3 cancelled/revision2/checksum checked. Inventory unchanged at cleanup.4141ordinary-save files retain membership/size/mtime, not content-hash proof. Only config difference is BepInEx version comment249→250, byte-verified. Pins restored; normal logout/quit; bridge3restored. No grants/native construction/terrain/reset/other profile/network.

QA DLL remains SHA256 A5608BDC767CB49F538276AC9499FA1F4A4E5F4F6D0894AE63D3EFC12DB6113E; no redeployment needed.

## Next and parallel work

Read-only diagnosis found repeated sparse-map lookups for identical block coordinates in FogPublished.cginc. Candidate: visit only unique neighboring block coordinates while retaining first z/y/x order, empty/invalid behavior, selected page/UV and native filtering. Not implemented or benchmarked. Require CPU selection/bounds/invalid cases and GPU baseline/candidate parity before native retry; do not reduce quality, enlarge queues or drop history.

Architect review corrected cancellation ordering: remove building/fog/board after durable refund issuance, before pickup; collection/cleanup continue without the board. Proposed rev3 returns-pending/rev4 settled are Draft, not implementation. The all-collected-gate proposal was rejected. [Protocol](PROJECT_CANCELLATION_PROTOCOL.md). Existing .250 composite scan is not the terminal transition.

Local evidence: artifacts/checkpoint-264/native-failure.json, audit/build/test logs and exact helper source snapshot; full reports in the QA run.14sections/115IDs/16scoped checks retained; no new completion claims.
