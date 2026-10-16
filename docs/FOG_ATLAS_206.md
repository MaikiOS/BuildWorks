# Common multi-source v11 rendering — .206

2026-10-16. A bounded renderer checkpoint, not a completed construction project.

`BlueprintFogVolume.RenderMany` retains the existing one/two-owner paths. For more owners, `BlueprintFogAtlas` packs independent Shape/Smoke/Surface into three derived GPU textures. One raymarch sums extinction and scattering. Each source retains its origin, clock, capsule, pigment and recovery state. The atlas does not own simulations or debit resources.

## Evidence

- Unity6000.0.61f1/DX11/Linear/HDR/Deferred: own wall/floor/rotated nonuniform beam, four frames0/45/90/420. Contact90 and recovery420 images inspected.
- ABC/CBA maximum mean RGB delta9.094e-8; forced two-owner atlas versus existing pair7.617e-7. Removing the third owner restores the exact pair output and releases derived textures.
- Packed Smoke/Surface byte exact; Shape within RHalf precision. Duplicated borders and the actual shared UV helper: nine distinct-owner GPU edge probes, delta0. Missing-third negative control0.01694 rejected.
- Opaque depth delta0; translation(2500,45,-1900) delta0.00006064<existing0.005. Invalid/duplicate/oversized requests retain simulation fields. Cleanup passed.
- Layout32 blocks rolls into X/Y under synthetic axis64, without overlap; axis/memory overflow rejected before allocation. This proves layout, not32-owner rendering.
- Frozen simulation270ticks/18 full-field byte comparisons passed after PackVolume was added. The final redundant shader early-return removal did not alter compute/simulation. Pair4, Gamma visual6+2 and source Shape/Pigment/Noise parity passed. Color: Linear0, wrong Gamma0.0487058 rejected. Existing thresholds unchanged.
- Production Linear bundle59,409bytes/3 own assets/8kernels, SHA `EF3D5AF894DDE8937183D616CAD3352D29EC04D545020973493EEDB3FB8A8A7B`. Gamma59,543 only for regression. Approved v11 archive unchanged; ProjectSettings restored exactly.
- Normal/QA0.19.206.0 builds0errors/0warnings; Host Steam25909357, control/isolation/localization1104 passed. No verify helper: direct checks and independent review. Local receipts `artifacts/fog-atlas-206`; no game assets committed.

## Deployment

Correctness PASSED and Ponytail Ship for the bounded checkpoint. Guarded `Deploy-TestBuild.ps1` installed QA0.19.206.0 only into TerrainRamp-1.0-Test. DLL SHA `B1691BB1DAE7668AEBC924A185329F4AE552492B4A57457D6A798A0DF68042FC`; all143 prior files retain SHA/UTCmtime, token unchanged. Game not launched. Mistyped Test-QAControl.ps1 corrected to the existing Test-QAControlContract.ps1; required checks completed.

## Limits and next result

Measured atlas13,914,432bytes/13.27MiB, in addition to simulation/source/mesh memory.128MiB caps the current atlas only; Resize can temporarily hold old and new sets. Every axis is checked against `SystemInfo.maxTexture3DSize`.32owners/span64m/steps×owners32768 are guardrails, not measured FPS; beam attenuation adds sampling work.

Native .206 and whole-house rendering/performance, large parts/seams/multiple players remain open. Last native visual .203/two walls. Next existing28-part Forest Hut, then durable project/stake/board/material continuation/confirmed cancel-refund/reload/server. House/board not connected; no new manual unfinished-house test ready. Full goal ACTIVE.
