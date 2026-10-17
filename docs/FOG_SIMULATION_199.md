<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# Runtime v11 simulation (.199)

2026-10-16. `BlueprintFogSimulation` ports all seven v11 compute stages into an owned, disposable runtime instance with bounded local coordinates/fixed-step catch-up. Borrowed shape/pigment/noise remain caller-owned. No gameplay/startup/payment/save/network callers exist yet.

Unity6000.0.61f1/DX11:270 ticks,18 byte-exact complete 3D Smoke/Flow/Surface comparisons against frozen v11 (namespace-only change; normalized SHA7576245A…D87150). Procedural test source, no game assets. Fractional intervals/eight-step catch-up, standing body/return, emit=false, invalid input and disposal passed. Damage1→0; emitted smoke is nonzero. Origin is not directly compared. Normal/QA0/0, Host/control/isolation/localization1104 passed. Verify helper unavailable; direct checks and independent reviews are separate evidence.

Evidence: local `artifacts/fog-runtime-simulation/{verification.json,samples.txt,sources.json,unity.log}`. Early first-layer-only readback failures are retained and not counted; final checks read every layer and assert the exact byte count. Own-process wait now has a three-minute limit. Initial CIM/junction sandbox refusals and stale-LASTEXITCODE wrapper error were corrected; standalone required checks passed.

Independent correctness PASSED/Ponytail Ship; reviewers did not rerun GPU. Guarded QA0.19.199.0 installed, SHA `A18E73EBF96B4BAB9A87A2189BF3C9548ABA62CA379DE7E3714738167370B891`;143 prior QA files retained SHA/UTCmtime. Valheim closed. Geometry/Store/full UI were not repeated because their code did not change.

Ten fields require49,152,000 bytes, not a measured house VRAM figure. One-wall YZ ownership remains an explicit shortcut: do not allocate this per piece or infer correct mixed ownership merely from XYZ bins. Next: native meshes/textures, local frame, real body passage/render comparison, then rotated/scaled parts and a persistent project/board lifecycle. New simulation has not run in Valheim; visual approval/performance/network/board remain open. Full goal ACTIVE.
