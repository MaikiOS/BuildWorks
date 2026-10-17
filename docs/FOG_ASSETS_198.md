<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# First v11 asset port, QA .198

2026-10-16. Added an explicit owned bundle loader, reproducible Unity6000.0.61f1/DX11 build and QA-only compatibility probe. Four source files match the approved v11 snapshot. Bundle49591B contains exactly two original shaders and one compute, no game models/textures or external dependencies. Bundle SHA FA9133955263DAE16A8BB51B3D0CE10C8FBB70F35ECF43A0A49AFBCAD2D3484F.

Normal/QA builds0/0, Host/control/isolation/localization1104, Unity asset/load/7 kernel-layout/ClearLedger GPU readback/reload/empty rejection and independent PASSED/Ship passed. Verify helper absent, direct checks used. Guarded QA deployment preserved143 existing files with SHA/UTCmtime. Installed0.19.198.0, DLL SHA EA2BECDDF9F8863FCC67E4619BDA59D4E7C39E8F8D2D1783681D12E43DD21D50.

Actual Valheim Unity6000.0.75f1, launched through Thunderstore TerrainRamp-1.0-Test Modded and existing QA identity: embedded assets supported; all7 kernel layouts verified; **only ClearLedger** dispatched/read back, followed by unload/reload. Exact QA return assertion passed without probe warnings/errors. Visible14-item inventory/Wood1 and legacy WAL retained. No objects/cards/grants/spending; full binary inventory not compared. Protections restored, game closed, own helper stopped, endpoint absent.

The loader is not connected to startup/current ghosts/construction. This does not prove rendered v11, smoke evolution, actors, house memory/performance, persistence or multiplayer. Next actual meshes/textures/one player contact, then bounded shared/local fields and correct per-part ownership, persistent project/board and safe continuation/cancellation. Whole goal ACTIVE/incomplete. [Full evidence, early diagnostic failures and limits](FOG_ASSETS_198_RU.md). Local receipts: artifacts/fog-runtime-assets and artifacts/fog-runtime-198. Unchanged Geometry/Store/full UI regression not repeated.
