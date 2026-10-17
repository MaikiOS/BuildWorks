# Approved v11 integration

[Русский](FOG_V11_INTEGRATION_RU.md) · [Current evidence](CURRENT_STATUS.md) · [Next milestones](ROADMAP.md)

**Logical-part input is separated from GPU generations, verified in Unity:** Channels retains accepted movement while waiting for a slot, recovery and retirement; new generations receive the original baseline and tail. The105-frame run (57 source frames) covers two sources/one slot, both reentry windows, contact hold, waiting-source fairness, removal and failed-acquire callback after drain. A real wall replayed after waiting matches7 field/ledger comparisons with nonzero effect. C#0/0,131CPU groups/533rejections/32coverage; physics/frames/render and independent PASSED/Ship. [Details](FOG_PUBLISHED_RENDER_213.md#logical-source-part-queue-and-generations). **Building is not switched yet:** Visual must own a logical source safely, source textures must outlive GPU use and dormant rendering must retain continuous clocks; then real28 admission/spatial ledger/house/FPS and durable board. Accepted idle tails can still require another slot; throughput remains open. Installed .213/working bundle unchanged.

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

Production integration **not implemented yet**:

- Each owner retains the full logical v11 domain, clock, identity and dense velocity: curl exists even in empty cells.
- Colour/origin/surface use lossless sparse blocks. Only a block whose raw half bits are all0 may be absent; negative zero must survive. Decode to one shared dense workspace before each step, initially retaining the physics kernels and hardware filtering.
- A static RHalf atlas holds all source shapes; separate RGBAHalf smoke/surface atlases hold active and unrecovered owners. Pack and bind **before geometry rendering**; `OnRenderImage` reads the published generation only. Stable slots/revisions, gutters and slot reuse need their own checks.

CPU executed the current integer-only `Layout` without Unity/GPU. The saved28-block fixture reproduced126955800 bytes exactly. Its bounds conservatively allocate14106200 bytes for static RHalf shape; eight hypothetical full dynamic fields including gutters need84345856. The98452056-byte sum fits128MiB. The former common18-byte layout for28 full fields needs at least332111808 and was rejected. This covers **the saved fixture and hypothetical8 fields**, not admission of the current house.

The proposed9-texture workspace takes44236800 bytes and eight dense velocities39321600, leaving14745600 under the current field cap for sparse blocks, maps, ledger and simultaneously live old/new generations. One eight-state GPU route is verified below; remaining capacity for the actual house is **unverified**. Count staging/allocation peaks; no added epsilon or eviction. Neither32 dense velocities nor a permanent eight-owner maximum may be assumed.

Measurement code in `Assets/Editor/FogSparseStorageProbe.*` / `FogRuntimeSparseCheck.cs` / `FogSparseSharedProbe.cs` passed onGPU. It omits only blocks with zero half bits; physics kernels and dense velocity are unchanged. Fixtures cover63488 finite half patterns, negative zero/subnormals, empty blocks and four rejection cases. The360-tick route schedules1444 field comparisons,1083 ledger comparisons and16 additional CPU raw comparisons; old state survives until new state is restored and compared. Occupancy, overlapping generations and a hypothetical eight-identical-owner projection are reported separately. This is neither house admission nor the production shared-workspace implementation.

GPU storage/shared-workspace experiment **passed**: one full wall1444 field/1083 ledger/16 CPU comparisons; eight independent states in alternating order11584/8688/16, including final restoration after another owner used the workspace. Fixtures cover63488 finite half patterns, negative zero/subnormals and four rejection cases. Single-wall encoded peak970240 bytes; counted eight-candidate field peak92268592 of98304000, including maps, ledger and overlapping generations. This private reflection experiment with synchronous reads is **not the game allocator/renderer or house/FPS proof**; reference simulations, source/atlas and driver staging are excluded from that field counter. Next: production storage lifetime, split atlases published before geometry, spatial return and the house. Runtime .213/bundle unchanged; Valheim was not launched.

Local receipts: `artifacts/fog-sparse-213/shared/verification.json`, `sources.json`, `unity.log`; the earlier single-owner run is retained in `roundtrip`. `scripts/Test-FogSimulation.ps1 -SparseOnly` requires both results. The validator replay accepted the actual GPU receipt and rejected57 corrupt shared variants; these are validator checks, not57 GPU runs.

CPU readback/allocation every tick is measurement-only; its elapsed wall time is not a GPU timing. After storage, verify the shared workspace, sample/gutters/slot reuse, camera/geometry order, rotated pieces, owner removal and the house; separately measure all allocations/staging and GPU time.

### CPU confirmation for the next step

CPU confirmation control is implemented in `BlueprintFogSnapshotState`: owner/generation/sequence/tick/revision, no resubmit/recovery before confirmation, retirement/late replies and uncertain readback. All128 CPU test groups, normal/QA builds0/0 and independent PASSED/Ship passed. The controller is now exercised with real v11 in the private GPU fixture; production lifetime, input queue and renderer remain open. Installed .213 is unchanged.

Astra selected a fixed shared4³ GPU block pool, two maps per owner, staging ledger and asynchronous receipts. Reserve the whole request before modifying the free stack; a rejected step must retain encoded state/velocity/ledger/clock/render slot. A readback error after a possible commit does not prove rollback: hold the owner until safe whole-pool teardown. Draining the GPU queue alone cannot authorize freeing blocks with unknown ownership.

The CPU implementation accounts for declared logical resources:8 owners/24860 blocks98303700 bytes; one extra block exceeds98304000. Rounded24576 blocks98157156, reserve146844 bytes; driver/readback staging, source and atlas are excluded. This is sizing, **not actual GPU allocations**. A test-only CPU oracle covers a one-block shortage, successful commit and packing rollback, neighbor preservation and1000 leak/duplicate-free cycles. A small GPU reservation/commit candidate is verified below; this code does not replace the current solver yet. Local receipt: `artifacts/fog-sparse-213/snapshot-verification.json`.

### Fixed-pool GPU candidate

Small fixed-pool GPU candidate passed `-ArenaOnly`:8 synthetic8×8×8 owners,47 commits/3 expected rejections,15 full checks —480 exact field and360 ledger comparisons, including atlas gutters and unique block ownership.17 kernels compiled onCPU, C#0/0,71 corrupt receipts rejected; independent PASSED/Ship. This is **not full v11, the production allocator/callback cadence, or house/FPS proof**. The full-physics fixture now passes; next is the production scheduler and shader consumers. Installed .213/bundle unchanged; Valheim was not launched.

`FogSparseArenaProbe.compute` / `FogRuntimeArenaCheck.cs` allocate buffers once. GPU counts all three fields, validates the complete request and reserves new blocks before old blocks are freed. After packing validation, immutable flags allow velocity, staging ledger and smoke/surface atlas writes; then old blocks are returned and a64-byte receipt is written. Rejection returns only the reservation and retains old fields/velocity/ledger/atlas. CPU confirmation matches owner/generation/sequence/tick/revision and switches maps only after the reply. The same owner cannot resubmit before confirmation; other owners can reuse staging.

Four alternating batches after initialization exercise8 owners without per-owner quotas. A one-block shortage, post-pack failure andInf retain old state; pending removal and a stale reply after slot reuse preserve neighbors. An empty snapshot returns old blocks and another owner can grow into shared free space. Every map/free ID and both atlases including gutters are checked. Fixtures deliberately upload and block on verification readbacks; this is neither game-loop speed nor a staging-ledger-through-physics check.

Local receipts: `artifacts/fog-arena-213/verification.json`, `sources.json`, `unity.log`, `guard-verification.json`; hashes match the executed source. Prior Sparse/Noise/Domain receipts passed their existing validator branches;71 negative checks test the CPU report validator, not71 GPU runs. Ostrix later authorized the full-physics GPU gate recorded below; no Valheim run followed.

Source/anchor/admission must separately inspect actual triangles and pivots: do not replace the wall source with compact bake or apply global scaling. The per-piece YZ ledger still needs a separate spatial fix preserving the wall oracle and checking return normalization. Sparse storage does not eliminate the expensive owner-loop raymarch or prove FPS. Roof, native/owner acceptance and durable board remain open.

Local CPU proof: `artifacts/fog-full-domain-plan-213/Check-Layout.ps1` and `layout.json`. A later free GPU window was explicitly authorized; the full-physics result is recorded below. Runtime .213 is unchanged.

Local receipts: `artifacts/fog-boundary-213/sweep.json`, `sources.json`, `guard-verification.json`; nine corrupt receipts were rejected. Independent reading PASSED, separate Ponytail Ship; redundant GPU reads removed. No new game test, resource/save mutation or public release.

### Full physics and metadata publication: GPU gate

**Full physics GPU gate passed:** actual v11, two96×80×80 owners, batches1–8 and fractional time;664 exact field/498 ledger/28 atlas comparisons,239 commits/2 safe rejections,242 checks of64-byte metadata before CPU confirmation. Zero-step metadata-only and pending-owner rejection checked. C#0/0,2 HLSL kernels0/0,169 negative CPU fixtures and independent PASSED/Ship. **This private fixture waits for readbacks: shader consumers, real callback frames, production memory, house and FPS remain unproven.** Runtime/QA .213 unchanged; Valheim was not launched.

`FogArenaPhysicsProbe.cs` uses the existing solver without changing runtime. Reflection borrows three read fields and staging ledgers; original owner velocity/ledgers remain committed until GPU publication. Prediction holds read velocity only until `AdvanceVelocity`, is then reused normally, and receives final velocity after all reads. Full maps require450 groups and ledger16; the synthetic fixture did not prove ledger carry-through.

Main route:360 ticks/two owners,80 batches1–8,161 expected commits and one injected packing rejection; a separate one-block pool must exhaust safely. The oracle advances only after a confirmed commit. Rejected speculative input history cannot be retried. Checks cover four fields, three ledgers, clock, unique map/free IDs; six publication checks include all gutters. Actual GPU total664/498/28 is confirmed by a fresh receipt;239 commits/2 rejections.

This fixture waits for readbacks, uses reflection and additional reference/source/atlas/temporary construction allocations; it is not production memory admission or live callback/render proof. Dynamic64-byte metadata is now conditionally published after fields and before the receipt; immutable domain/bounds stay bound to slot/generation. At that physics stage shaders were not connected; the later private consumer result is recorded below. The Editor-reference compile output must not be installed in Valheim.

`FogSnapshotMetadataProbe.compute` adds two separate kernels; the previous17 arena kernels are unchanged. The64B record stores actor/radius; halfHeight/fractionalClock/trailWeight/flags;64-bit owner/generation;64-bit sequence;32-bit tick/revision. Rejection retains every old word. The private zero-step call updates both solvers' input history without changing fixed tick/fields; pending updates are rejected. That physics gate alone is not rendering proof; the later private consumer gate is recorded below.

Eight slots add512B to prior CPU sizing:24860 blocks98304212 exceed98304000;24859 give98303696, rounded24576 give98157668. This is sizing, not a measured allocation peak; existing `EstimateFieldBytes` still describes the previous layout without metadata and cannot admit the entire new path. The two-owner/8192-block/metadata fixture declares58779980 logical bytes, excluding reference/source/atlas/construction/driver staging.

Local: `artifacts/fog-arena-physics/verification.json`, `sources.json`, `unity.log`, `cpu-verification.json`, `fingerprint-verification.json`. Executed source hashes match; owned bundle and QA DLL unchanged. Unity logged LicensingClient/access-token warnings, then resolved Personal entitlement and exited0; no compile/shader/test errors. FXC first rejected Unity `#pragma kernel` (X3568); stripping only those lines in a temporary copy passed /WX.

### Input queue and real frames: verified private route

**Input queue and real frames verified:**64 observations, up to16 samples/8 ticks per batch; exact order/fractional and equal time/body/emit/possible/nearby. Confirmation removes only its prefix; overflow remains faulted after success; recovery waits for an empty queue.129 CPU groups, normal/QA/Editor C#0/0,261 negative CPU fixtures, independent PASSED/Ship. GPU:243 real Unity player frames,54 commits,232 field/174 ledger/20 atlas/54 metadata comparisons; capture while pending, receipt-first, expired handle, pending retirement/new generation/stale receipt passed. Full physics regression664/498/28 and239/2/242 passed again. **Queue integration is private-fixture only; production scheduler/render integration, house/FPS remain open.** Installed .213/bundle unchanged; Valheim was not launched.

`FogArenaFramesCheck.cs` reuses the full Probe with an owned procedural wall, not a game-world source.24 batches1–8 alternate owners. Six frames without polling after completion must expire a native handle while copied replies survive. A further pending batch retires owner0; reuse is blocked until callback, then maps/atlas are cleared and new solver/ledger/velocity/generation created. A stale receipt is rejected during the new pending batch; the neighbor retains its clock.

The join no longer assumes callback order. The fixture reverses request registration and requires an observed receipt-first case. `Poll` reads cached CPU phases without `WaitForCompletion`, `WaitAllRequests` or `GetData`; separate exact oracle comparisons and teardown may block. This does not prove production input queue, render cadence or FPS. Real Play Mode requires the runner to omit `-quit`; its own timeout remains.

With queued inputs and metadata-only pending, actual results are232/174/20/54,54 commits and243 frames. Full664/498/28 and239/2/242 regression reran on current sources. Historical receipts retained; installed DLL/bundle unchanged.

`BlueprintFogInputQueue` is a fixed64-entry CPU ring; each batch admits up to16 observations and8 fixed ticks. Captured actor values are canonical floats and tick arithmetic matches the solver. Each sample preserves inputTime/position/body/Emit/EmissionPossible/Nearby; `MightContact` must be captured against the preceding accepted observation. Commit removes only the oldest immutable prefix while capture may append a tail. Rejected/Unknown retain accepted history and stop retry. Overflow explicitly rejects the incoming sample; successful pending completion never clears its sticky fault. `CanRecover` needs an empty queue and confirmed revision.

`SnapshotState.Submit(..., metadataOnly:true)` permits only the same fixed tick/revision, with a new sequence and normal pending/retirement rules. The private zero-step GPU path retains maps/velocity/ledger/bank while replay updates both solvers' input history. Two metadata-only commits extend prior Frame52 to actual54 commits,232/174/20/54 and243 frames. Only submission/Poll avoid GPU waits; exact verification barriers/teardown may block. Production scheduler throughput is not measured.

Fresh archives: `artifacts/fog-input-213/physics/` and `frames/`; CPU `geometry-tests.log`, `cpu-verification.json`, `fingerprint-verification.json`. The command wrapper initially checked an unset `$LASTEXITCODE` after the successful PowerShell physics runner and emitted `Full physics regression failed`; physics was not rerun, and the Frame runner then separately exited0. Same Unity licensing warnings; no shader/compile/test errors.

### Published state: mesh and volume

**Mesh/Atlas consumer verified in Unity:**87 captures/62 comparisons/26 negative cases;16 legacy captures are pixel-exact, CPU poison0. Separate candidate; normal/QA/Editor0/0,129 groups/363 CPU rejections, independent PASSED/Ship. [Evidence and limits](FOG_PUBLISHED_RENDER_213.md). Runtime backend, house/FPS and board remain open; installed .213 unchanged.

### Runtime: explicit borrowed fields

`BlueprintFogSimulation.StagedState` replaces reflection-based field/ledger substitution in the fixed-pool probe. Dimensions/format/UAV/Trilinear/noMip/ClampU/V/W and aliases are validated before GPU writes. Borrowed fields are neither reallocated nor released by the solver; staged recovery cannot authorize release from the common ledger. Committed velocity/ledger remain owned by the pool. The legacy constructor/Dispose route is retained. `BlueprintFogScratch(includeReadFields:true)` owns all9 workspace fields/44236800B; existing callers retain6 fields. Probe no longer releases shared reads separately.

Verified:11 invalid resource sets reject without changing sentinels; valid Dispose retains borrowed resources. GPU physics664/498/28,239commits/2rejections/242metadata;Frame236frames54/232/174/20/54;render87/62/26+legacy16/delta0; legacy Scratch360/36 and Recovery24 passed with the same runtime SHA. Normal/QA/Editor0/0,129groups,371negative CPU cases, independent PASSED/Ship. Local receipts/fingerprints:`artifacts/fog-borrow-213/`.

This is the runtime solver API exercised by the check, **not a complete game allocator/scheduler**. The pool remains an Editor fixture. House memory/source admission/spatial ledger/FPS remain open; installed .213/bundle unchanged.

### Runtime GPU core

`BlueprintFogArena` now owns maps/free-list/payload/control/receipts/metadata, the shared9-field workspace and two dynamic atlases. Bindings/dispatch and metadata publication were extracted from Probe without changing kernels. Probe calls runtime code and still owns committed velocity/ledger, queues, callbacks and dense oracle. Normal builds have no UnityEditor dependency for this core. The caller must drain readbacks before arena disposal; the existing Probe follows that order.

The CPU reservation now includes published metadata64B/owner:8owners/24859blocks=98303696B fits;8/24860 exceeds98304000B; rounded24576=98157668B. The verified2owner/8192block reservation58779980B includes separately owned committed fields/ledger; it is not whole-house memory measurement. Dynamic atlases have a separate128MiB/device3D-size guard; static source and driver memory are excluded. Invalid sizing/budget rejects before allocation; metadata slot rejects before dispatch.

Final GPU:physics239commits/2rejections/664field/498ledger/28atlas/242metadata;4 sizing/budget and2 slot cases plus11 borrowed-resource rejections passed. Frame227/54commits/232/174/20/54;render87/62/26+legacy16delta0;Scratch360/36,Recovery24. Normal/QA/Editor0/0,129groups/374CPU rejections, independent PASSED/Ship. All5 modes match current Arena/Scratch/Simulation SHA; local archive:`artifacts/fog-arena-runtime-213/`.

The first final-series launch never executed: automatic approval review exceeded its deadline. One retry, guarded against starting after17:56UTC, completed. This was not a physics failure. Installed .213/bundle and worlds remained unchanged.

**Not connected yet:** construction from `BlueprintFogChannels`, channel lifetime/callback scheduler, packaging/loading the two additional compute programs in the production bundle. Editor still supplies their prototypes. Split static source layout, real28-part admission, spatial ledger, house/FPS and board remain open. Runtime GPU core does not mean complete in-game construction.
