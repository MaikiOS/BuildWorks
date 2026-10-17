# .211: removing redundant fog volume work

[Русский](FOG_RENDER_CACHE_211_RU.md) · [Current evidence](CURRENT_STATUS.md)

## Change

The .210 native Forest Hut passed its field budget and complete route but sampled as low as8 FPS during contact. Disabling the volume path restored speed; simulation alone did not explain the slowdown. .211 removes two identified sources of unnecessary work:

- Unchanged atlas blocks are no longer packed every frame. Keys bind the actual owner, Smoke/Surface texture references and simulation tick; layout invalidation commits only after successful allocation. Clock, actor and shader coordinates update independently of packing.
- Dormant owners bind known-zero shared fields. The shader skips reading them and selects dormant candidates using the rest-shape region with a conservative nearly-parallel-ray margin. Active owners retain their transported-matter region. The original sampling branch remains available as a comparison keyword.

Geometry, density, integration steps, v11 formulas/compute and memory limits are unchanged. The approved source stays frozen. This optimizes temporary visuals; payment, items, durable projects and server permissions are unaffected.

## Checks

Before the fix, the targeted check reproduced `InvalidOperationException: Unchanged owners were packed again`. GPU checks now cover repeated frames, one-owner updates, owner reorder/replacement, committed/failed layouts and the existing atlas order/depth/translation/filtering/cleanup gates.

Lazy:28 **synthetic** parts, five existing comparison frames, activation/shared-zero recovery/re-entry. Ten same-camera/state RGB-float comparisons cover idle, first tick,60-tick contact with confirmed surface damage>0.5 and smoke>0, recovery and reacquisition; each with light strengths0 and2. New/cached rendering equals the original sampling branch and forced packing with zero difference. A comparison sheet/manifest is retained locally. Existing eager-v11 maximum mean image difference remains0.000137874, foreground0.00169127.

Atlas4 frames/3 owners and six-frame approved-v11 parity passed; Shape/Pigment/Noise match. Normal/QA builds0 errors/0 warnings; host/control/isolation/localization1104 passed. Isolation initially encountered sandbox `New-Item ... Access denied` creating a temporary test junction; the elevated rerun passed. Independent correctness PASSED and Ponytail Ship. The `verify` skill is unavailable; direct review and targeted checks were used.

## Timing and deployment

Isolated Unity6000.0.61f1/D3D11,384×216,28 dormant synthetic parts; one warm-up and median of five three-frame batches including GPU-readback wait. These are complete test-frame durations, not pure GPU timing or Valheim FPS.

| Mode | Latest ms/frame |
| --- | --- |
| No volume |0.9144 |
| New shader/cache |3.8302 |
| New shader/forced packing |2.9722 |
| Original sampling/cache |4.6900 |
| Original sampling/forced packing |5.2330 |

Timing is noisy: the preceding run measured1.4043 ms for the new path and3.3918 ms for the previous path. Fast-mode ordering is unstable; no stable speedup factor is established. Reduced dispatches and equal output are proven separately from speed. The actual house needs3440×1440 and a fixed camera.

QA **0.19.211.0** was guarded-deployed, SHA `F748D15F60768E0B6C6A4BB24FF878F66613036432FA2CBD2A7093EEEA2278C2`; all143 protected files retained hashes/mtimes. **.211 is partially native-tested and exposed a new blocker; the full script FAILED.** At3440×1440, one position/camera and frozen simulation:new shader49.15 FPS, original24.80, new again48.26 (frameCount/realtime differences over≈3 seconds). Packing counter523 stayed unchanged. This compares shaders, not full simulation speed. An idle sample was144 FPS; reverse contact with simulation running sampled11 FPS. First exit recovered to0 owners/0 bytes; peak87408640. At step59/62 the QA visual was cleared and stopRevision changed1→3; final reverse recovery remains unproved. Automated cleanup/inventory0→0 passed.

After control handoff, the house was recreated with both SkipSimulation/SkipVolume=false. Disappearance recurred. The log records three instances of `BuildWorks fog QA stopped: Fog recovery readback failed; retain its owner.` The last attempt detached only the renderer from focus cleanup and scheduled its Cleanup after300 gameplay seconds; it disappeared during passage before that timer. Focus does not explain every disappearance. Owner traversal is not accepted; no repeat on .211 is requested.

Next: reproduce AsyncGPUReadback lifetime across actual frames and fix recovery while preserving freshness and field ownership. [Unity documents a single-frame result lifetime](https://docs.unity.cn/6000.0/Documentation/ScriptReference/Rendering.AsyncGPUReadbackRequest.html); current polling runs only outside contact. This is a source-backed explanation requiring reproduction, not an implemented fix. Then repeat full-cycle/performance checks and leave a stable manual preview. Full roof appearance and house performance remain unaccepted; the durable project/board is not connected. Next: native route/memory/recovery/FPS and same-camera shader comparison, then durable unfinished construction with continuation/cancel/confirmed refund/reload/server authority.

Local evidence: `artifacts/fog-pack-cache-211`. No game assets, catalog, private QA identity or saves are published.
