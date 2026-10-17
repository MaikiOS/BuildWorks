# Approved v11 integration

[Русский](FOG_V11_INTEGRATION_RU.md) · [Current evidence](CURRENT_STATUS.md) · [Next milestones](ROADMAP.md)

Source/QA .210 embeds an owned Linear shader/compute bundle with3 assets/8 kernels. The approved v11 source/archive is frozen locally; extracted vanilla assets are never redistributed. Native prefabs/textures are resolved from the host.

Implemented:source baking, capsule-driven deformation, independent owners, compact fields and shared GPU atlas. .210 adds exact-grid shared scratch:the actual28-part house passed both-way traversal/standing/exit, recovery0, peak87408640. Volume speed (down to8 FPS)/full roof appearance, multiple players and durable construction remain open. [Exact .210 evidence](FOG_SCRATCH_210.md).

Connection points:

- `BlueprintFogVisual/Source/Simulation/Channels/Volume/Atlas` own temporary visual/compute data. Atlas data is derived; it does not own material balances or durable state.
- `PrecisionPlacementSession` owns current native placement and transient pause; `BlueprintBatchedPreview` owns batched ordinary previews. A queue index/hidden preview is not a durable per-part project state.
- `BlueprintConstructionRefund` verifies actual payment and removal before return; `BlueprintStoragePersistence/TransferJournal` handles separate inventory evidence. Existing storage WAL is not an unfinished-project journal.
- A persistent project needs immutable blueprint/revision/placement binding, stable part IDs and completion/created-object records, server-authorized continuation/cancel, board interaction and crash-safe exact refund. The fog observes that state and must survive reload/reconnect through reconstruction.

Current work: existing Forest Hut (28) parts with bounded contact fields, recognizable materials, body passage/standing/return, no interactions/colliders and measured memory/FPS. Retain each unrecovered owner's matter. Then connect the durable board/project. Success on a wall or a shader load does not complete either milestone.

Approved behavior: shape/textures/transmission remain recognizable; body lightly distorts and opens bright transparent swirling fog; opening holds while inside; matter returns smoothly after exit. No timer-only disappearance, wood chips, dense brown replacement smoke or repeating sprite substitute. Preserve the visual reference while optimizing ownership.

The local original handoff is `artifacts/fog-ghost-study/wall-11/HANDOFF_BUILDWORKS_RU.md`, not a public download. [Initial integration analysis](FOG_V11_INTEGRATION_HISTORY_206.md) is historical, including its former compatibility and memory assumptions.

## Historical stages before .210

[.207 packing and allocation-failure checks](FOG_PACKING_207.md) admit the saved house block layout; actual contact fields and native house remain open.

[Contact fields .208](FOG_CONTACT_208.md): dormant pieces retain their full visual; actual house/contact set/FPS still require Valheim.

[Native .208 house failure and .209 alpha fix](FOG_CUTOUT_209.md): .209 installed, not game-tested; budget unchanged.

## Current result

[.210 shared scratch and native traversal](FOG_SCRATCH_210.md). Memory passes the tested route; performance and the durable board remain unfinished.
