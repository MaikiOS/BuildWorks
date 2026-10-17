# Contact fields and blueprint adapter .208

[Русский](FOG_CONTACT_208_RU.md) · 2026-10-17 · [Current evidence](CURRENT_STATUS.md)

.208 keeps the whole building's shape, materials, and textures. Simulation fields activate only near possible body contact with final filtered R8 source density, excluding empty source bounds. Swept motion includes emission and interpolation margins. Dormant pieces borrow an immutable zero texture while retaining volumetric v11 and `_Accumulated=1`. Shader/compute and field resolution are unchanged.

Activation starts at the previous absolute clock, preserving phase and bounded catch-up. Possible contact prevents release; departure requires GPU-confirmed recovery. Stale empty readbacks cannot remove new matter. The contact signal guards readback revisions without disabling body wind on existing smoke. Budget or duplicate-owner rejection preserves neighboring fields.

`BlueprintFogBuilding` assembles an existing document using part positions, rotations, intrinsic prefab scale, and component scale. Payments and persistent state stay external. Isolated QA prepares `StartFogHouse(existingBlueprintId)` for an existing personal entry, loaded authorized session, and idle placement mode. It creates temporary visuals, not world pieces or resources.

## Verified

- Unity:28 synthetic dormant pieces allocate no simulation fields; atlas32330592 bytes. Mixed dormant/active rendering and neighbor removal retain shared resources.
- First contact after10 seconds idle and re-entry:5 frames against an eager owner. Idle delta0; maximum mean image delta0.000137874, foreground0.00169127, within unchanged0.005/0.03 thresholds. Images inspected. Dynamics are not claimed byte-identical: initial flow velocity differs.
- Test contact9830400 field bytes against98304000 global budget; a separate tight-pool negative control preserves neighboring matter.
- Source void, swept crossing, diagonal miss, invalid initial clocks, stale GPU response, recovery with native emission continuously enabled, duplicate owner, shared-empty cleanup.
- Actual Blueprint document type with28 synthetic records: root TRS, contact, visible render versus blank-frame negative control, and cleanup. This is not28 actual game prefabs.
- Frozen v11:270 ticks/18 exact field comparisons; Owners420 ticks/3 frames; previous Atlas4 frames/3 sources with border/depth/order/translation/allocation-failure checks. Normal/QA0 errors/0 warnings; host/control/isolation and localization1104 passed. Independent correctness PASSED and Ponytail Ship.

QA0.19.208.0 installed only through guarded Deploy-TestBuild into TerrainRamp-1.0-Test.143 files retain exact hashes/times; catalog, grants, and saves were not reset. SHA `A7BAC4CCAABB4748DB648BAA2B3144350924B16D28EC7433D0A84A1B750E5822`. No new public binary release.

## Still unproven

Valheim .208 has not run. Actual Forest Hut prefabs, simultaneous active and recovering owners, memory/FPS, and visual acceptance remain open. The adapter currently supports1..32 available MeshRenderer pieces, not arbitrary heavy buildings; missing/unsupported models are not guessed. Sources, textures, and atlas still consume memory while simulations sleep. Conservative swept Y bounds may over-admit channels; a real budget failure requires local contact regions rather than silently raising the cap.

Persistent projects, stake/board, resume/save, and network integration remain unconnected. Last native visual remains .203/two walls. Local-only receipts/images: `artifacts/fog-contact-208`.
