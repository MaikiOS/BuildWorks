# Approved v11 integration

[Русский](FOG_V11_INTEGRATION_RU.md) · [Current evidence](CURRENT_STATUS.md) · [Next milestones](ROADMAP.md)

Source/QA .213 embeds an owned Linear shader/compute bundle with3 assets/8 kernels. The approved v11 source/archive is frozen locally; extracted vanilla assets are never redistributed. Native prefabs/textures are resolved from the host.

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

[.213 shared noise frame](FOG_NOISE_FRAME_213.md) is checked and installed. The native .212 technical route passed, but Ostrix rejected dissolution/recovery and reported stutter. This is not v11 acceptance; native .213 remains untested.

### Why the smaller field did not retain the effect

The Unity control uses identical source density, noise, seed, body, path, time and physical voxel spacing. A private compute copy changes only the absorption-edge expression. Its full-domain control matches all four runtime fields plus away/peak/state at12 capture times. Runtime shaders and installed .213 are unchanged.

Volume-weighted smoke alpha at tick165:

| Storage domain | Absorption boundary | Value |
| --- | --- | --- |
| Full | Full | 1.025640 |
| Cropped | Cropped | 0.577369 |
| Full | Cropped | 0.577870 |
| Cropped | Full | 0.989743 |

Inference for this route: premature absorption dominates the early difference; physical outflow alone does not explain it. Decoupling absorption still leaves differences: tick240 residue is0.068365 versus0.045331, with changed surface fields and return accounting. This alpha sum is a diagnostic, not proof of physical mass conservation.

One bounded halo sweep retained voxel spacing and grid phase:

| Grid | Smoke tick165 | Maximum surface-field difference tick165 | Fields for8 identical active sources + one scratch, bytes |
| --- | --- | --- | --- |
| 48×56×72 | 0.989743 | 0.164001 | 58834944 |
| 64×72×80 | 1.024990 | 0.149597 | 112066560 |
| 80×80×80 | 1.025927 | 0.053894 | 155648000 |
| 96×80×80 | 1.025640 | 0 | 186777600 |

A close total hides local differences. Only the full control retained exact state; this is an engineering check, not an owner-approved visual tolerance. The98304000-byte field limit is unchanged. These hypothetical identical-grid estimates exclude atlas/source costs; they are **not** an actual28-part layout check or proof that every other padding arrangement is impossible.

### Next candidate: independent state, shared workspace

Astra's review confirmed that merging independent pieces into one simulation is unsafe here. `history.xyz / mass` stores a mean source position; owner fractions cannot be reconstructed afterward using nearest-piece or a3D lookup.

This candidate is **not implemented**:

- Each owner retains the full logical v11 domain, clock, identity and dense velocity: curl exists even in empty cells.
- Colour/origin/surface use lossless sparse blocks. Only a block whose raw half bits are all0 may be absent; negative zero must survive. Decode to one shared dense workspace before each step, initially retaining the physics kernels and hardware filtering.
- A static RHalf atlas holds all source shapes; separate RGBAHalf smoke/surface atlases hold active and unrecovered owners. Pack and bind **before geometry rendering**; `OnRenderImage` reads the published generation only. Stable slots/revisions, gutters and slot reuse need their own checks.

CPU executed the current integer-only `Layout` without Unity/GPU. The saved28-block fixture reproduced126955800 bytes exactly. Its bounds conservatively allocate14106200 bytes for static RHalf shape; eight hypothetical full dynamic fields including gutters need84345856. The98452056-byte sum fits128MiB. The former common18-byte layout for28 full fields needs at least332111808 and was rejected. This covers **the saved fixture and hypothetical8 fields**, not admission of the current house.

The proposed9-texture workspace takes44236800 bytes and eight dense velocities39321600, leaving14745600 under the current field cap for sparse blocks, maps, ledger and simultaneously live old/new generations. This remaining capacity is **not verified** on real state. Count staging/allocation peaks; no added epsilon or eviction. Neither32 dense velocities nor a permanent eight-owner maximum may be assumed.

First GPU proof after separate authorization: the same original full wall, pack→clear dense reads→unpack→existing kernels on every step; compare all4 fields and ledger bitwise through360 ticks, recontact and block boundaries. Measure occupancy, actual allocations and GPU time. CPU readback/allocation every tick is acceptable only as a measurement prototype. Then verify sample/gutters/slot reuse and camera/geometry order, rotated pieces, owner removal and the house.

Source/anchor/admission must separately inspect actual triangles and pivots: do not replace the wall source with compact bake or apply global scaling. The per-piece YZ ledger still needs a separate spatial fix preserving the wall oracle and checking return normalization. Sparse storage does not eliminate the expensive owner-loop raymarch or prove FPS. Roof, native/owner acceptance and durable board remain open.

Local CPU proof: `artifacts/fog-full-domain-plan-213/Check-Layout.ps1` and `layout.json`. While Ostrix plays another profile, continue source/calculation work without game/GPU execution. Runtime .213 is unchanged.

Local receipts: `artifacts/fog-boundary-213/sweep.json`, `sources.json`, `guard-verification.json`; nine corrupt receipts were rejected. Independent reading PASSED, separate Ponytail Ship; redundant GPU reads removed. No new game test, resource/save mutation or public release.
