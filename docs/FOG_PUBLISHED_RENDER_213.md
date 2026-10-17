# v11: published state in mesh and volume shaders

2026-10-17 · [Русский](FOG_PUBLISHED_RENDER_213_RU.md) · [Current status](CURRENT_STATUS.md)

**Verified private Unity consumer route, not the game house.** `BUILDWORKS_FOG_PUBLISHED` reads one64-byte GPU record for clock/body/actor/active; mesh vertex/fragment sample published surface atlas. Static shape and dynamic fields use separate sizes/offsets. Exact64-bit owner/generation checks do not depend on CPU sequence confirmation. Actual buffer dimensions/stride, region and slot are checked before reads. Invalid bindings retain the owner's static source and exclude foreign fields.

| Check | Actual result |
| --- | --- |
| Unity6000.0.61f1 / D3D11 / Linear | Candidate bundle3 assets/8 kernels,88727B;0 shader errors |
| Mesh-only / volume-only / combined |87 captures,62 comparisons,2 full96×80×80 owners |
| Dense oracle versus publication | Maximum mean RGB9.8727858e-8; maximum local channel0.00048828125 |
| Stale CPU clock/body/actor/active | Delta0; CPU confirmation held until after camera.Render |
| Metadata-only / packing rejection | Capsule/fractional clock changes without a fixed step; rejection retains the old frame |
| Owner/generation low/high, slot; region/short buffers |26 negative cases match static fallback; neighbour retained |
| Disabled keyword / invalid generation | Delta0.0034708318 /0.0021120982; controls are sensitive |
| Installed bundle versus candidate, keyword off |16 captures:mesh/single/pair/atlas, idle/contact; exact pixel delta0 |
| CPU |Normal/QA/Editor C#0 warnings/0 errors;129 geometry groups;363 corrupt receipts/manifests rejected |
| Independent reading |Correctness PASSED, Ponytail Ship; reviewer did not rerun GPU |

Frozen camera:perspective49°,384×216,position(4,2.3,-5),target(0,1,1.1),roll0,Deferred/HDR/depth;background(.15,.2,.3),no extra tone mapping and beam disabled. Two owned procedural(.2,2,2) walls use origins(0,0,0)/(0,0,2.5),colour(.64,.31,.12). Raw captures, `comparison-sheet.png`, `verification.json` and `sources.json` remain local under `artifacts/fog-published-render/`; camera/fields were not adjusted to pass. This is numerical/render equivalence, not owner acceptance of the house appearance.

Expanded checks exposed surface-analysis `Unexpected identifier StructuredBuffer` and legacy delta0.018276. Unity's builder had exited successfully despite shader errors. Fixed by hiding StructuredBuffer only from the surface-analysis parser; real GPU passes retain it. ShaderUtil messages and logs now reject shader errors; a7-source manifest ties shaders/compute to the bundle. Corrupt source/bundle hashes/manifests are rejected before GPU execution. The failed intermediate bundle was never installed.

Commands:`scripts/Build-FogGhostAssets.ps1 -CandidateOnly`, then `scripts/Test-FogSimulation.ps1 -PublishedOnly`. Candidate is not copied into the working resource. Candidate SHA `A9CD597CACB133047E9C6C7EE29B55FA9308E3D6677F7DAB753AB4F0514848CC`. QA .213 and its bundle remain unchanged; Valheim/worlds/inventory were untouched.

Next complete step:runtime arena and split atlas in existing Channels/Simulation/Scratch/Atlas, then actual-geometry admission and spatial ledger replacing YZ-only ownership. Full domain, retained-state budget and safe rejection are mandatory. Production scheduler/render lifecycle,28-part house/FPS, multiple players and durable board remain unproven. The new consumer covers current accumulated-v11 with one actor/trail[0]; legacy variants remain available. This D3D11 run does not establish other graphics APIs.

Independent review also caught missing input/state copies in the asset builder. Both are now staged and hash-checked; a CPU test executes the actual copy loop into an empty directory. No reliance on leftover RuntimeSources files.

### Runtime: explicit borrowed fields

`BlueprintFogSimulation.StagedState` replaces reflection-based field/ledger substitution in the fixed-pool probe. Dimensions/format/UAV/Trilinear/noMip/ClampU/V/W and aliases are validated before GPU writes. Borrowed fields are neither reallocated nor released by the solver; staged recovery cannot authorize release from the common ledger. Committed velocity/ledger remain owned by the pool. The legacy constructor/Dispose route is retained. `BlueprintFogScratch(includeReadFields:true)` owns all9 workspace fields/44236800B; existing callers retain6 fields. Probe no longer releases shared reads separately.

Verified:11 invalid resource sets reject without changing sentinels; valid Dispose retains borrowed resources. GPU physics664/498/28,239commits/2rejections/242metadata;Frame236frames54/232/174/20/54;render87/62/26+legacy16/delta0; legacy Scratch360/36 and Recovery24 passed with the same runtime SHA. Normal/QA/Editor0/0,129groups,371negative CPU cases, independent PASSED/Ship. Local receipts/fingerprints:`artifacts/fog-borrow-213/`.

This is the runtime solver API exercised by the check, **not a complete game allocator/scheduler**. The pool remains an Editor fixture. House memory/source admission/spatial ledger/FPS remain open; installed .213/bundle unchanged.

### Runtime GPU core

`BlueprintFogArena` now owns maps/free-list/payload/control/receipts/metadata, the shared9-field workspace and two dynamic atlases. Bindings/dispatch and metadata publication were extracted from Probe without changing kernels. Probe calls runtime code and still owns committed velocity/ledger, queues, callbacks and dense oracle. Normal builds have no UnityEditor dependency for this core. The caller must drain readbacks before arena disposal; the existing Probe follows that order.

The CPU reservation now includes published metadata64B/owner:8owners/24859blocks=98303696B fits;8/24860 exceeds98304000B; rounded24576=98157668B. The verified2owner/8192block reservation58779980B includes separately owned committed fields/ledger; it is not whole-house memory measurement. Dynamic atlases have a separate128MiB/device3D-size guard; static source and driver memory are excluded. Invalid sizing/budget rejects before allocation; metadata slot rejects before dispatch.

Final GPU:physics239commits/2rejections/664field/498ledger/28atlas/242metadata;4 sizing/budget and2 slot cases plus11 borrowed-resource rejections passed. Frame227/54commits/232/174/20/54;render87/62/26+legacy16delta0;Scratch360/36,Recovery24. Normal/QA/Editor0/0,129groups/374CPU rejections, independent PASSED/Ship. All5 modes match current Arena/Scratch/Simulation SHA; local archive:`artifacts/fog-arena-runtime-213/`.

The first final-series launch never executed: automatic approval review exceeded its deadline. One retry, guarded against starting after17:56UTC, completed. This was not a physics failure. Installed .213/bundle and worlds remained unchanged.

**Not connected at the GPU-core checkpoint:** construction from `BlueprintFogChannels`, channel lifetime/callback scheduler, packaging/loading the two additional compute programs in the production bundle. At that checkpoint Editor supplied their prototypes; the following section records bundle preparation. Split static source layout, real28-part admission, spatial ledger, house/FPS and board remain open. Runtime GPU core does not mean complete in-game construction.

### Runtime compute assets: preparation without GPU

Two owned compute programs moved from Editor to `src/BuildWorks/Assets/FogGhostRuntime/FogArena.compute` and `FogSnapshotMetadata.compute`; compute source remains byte-exact and meta GUIDs are retained; one meta uses LF for the Git whitespace check. `BlueprintFogAssetLayout` defines the exact3 legacy/5 current asset sets and9 source files. Loader accepts complete sets only and checks8+17+2 kernel names/thread layouts, retaining old3-asset loading. Arena shares its17-kernel list. QA reports actual loaded asset/kernel counts.

Builder prepares5assets/27kernels in a separate candidate output with metadata publication/rejection/reload checks. Physics/frame/render checks take both programs from that bundle; the synthetic8³ gate uses canonical source. Clean staging and fingerprints cover the new files; an old3-asset candidate cannot prove this stage. Working bundle was not replaced.

Fixed a real initialization gap: one64-thread dispatch left metadata of owners5–8 uninitialized. Runtime now covers all16 words per owner. CPU regression compiles the actual C# dispatch expression and verifies coverage against the actual HLSL kernel width for1–32 owners. The earlier2-owner GPU gate did not cover this fault.

Verified130CPU groups,377 corrupt receipt/manifest fixtures and32 coverage cases; clean staging checked in an empty folder. Normal/QA/offline Editor C#0/0; all19compute kernels compiled with fxc without GPU. Local evidence:`artifacts/fog-assets-runtime-213/`. **New bundle GPU build/load/reload, new physics/frame/render and Valheim were not run:** the authorized one-hour window ended. This prepares the production loader; it is not a complete game backend. Next:new candidate GPU gate, Channels/callback/split source atlas; real28 admission/spatial ledger/house/FPS and board remain open. Installed .213 and embedded3-asset bundle unchanged.

### Runtime Channels: input, receipts and retirement

Separate Linear candidate5assets/27kernels,93496B, SHA `D14FE97D44AA6DB7296A52C4FC2F2C362719CB090D76632A8A45B821CC62FB13` passed D3D11 load/publication/rejection/reload. These are owned shader assets; the working3-asset bundle was not replaced.

`BlueprintFogChannels.CreateArena` is an explicit strict route alongside the retained legacy API. Channels owns full-field snapshots, committed velocity/ledger, staged solver, queue, generation/bank and current native readback. Callbacks copy receipts; Poll confirms bank/queue without waits or expired native data. Rejected speculative CPU history cannot retry; unknown results and allocator flag4 stop the pool until teardown. Duplicate factories never take ownership of another solver; actual borrowed references are checked.

Retirement has its own copied GPU Control confirmation: slot/resources remain until it arrives; repeated free/reuse is blocked. A later neighbour packing failure cannot replace that result. Dispose closes input, drains only live requests and a separate GPU fence, then frees owners/core. Failed drain retains resources and permits another Dispose attempt. Normal polling does not block; teardown/private comparisons may wait.

`FogArenaPhysicsProbe` no longer has duplicate candidate/bank/generation lifecycle. Frames/render capture through runtime CaptureSnapshot; metadata observation and dense oracle remain checks.38 GPU cases cover nonzero initial clock, duplicate/wrong factory/reentry, observer-independent metadata-only, pending retirement/reuse/stale callback, corrupt ID/map/status/null, allocator4, pending Dispose, initialization/release failure and failed-drain/retry. Earlier mandatory comparisons were retained.

Final:physics664field/498ledger/28atlas,240commits/2rejections/242metadata;239 real Unity frames,54commits/232field/174ledger/20atlas/54metadata;render87captures/62comparisons/26negative cases,legacy16delta0,mean9.8727858e-8/maxlocal0.00048828125,CPU-poison0. The extra commit versus239 is the zero-step metadata update now using the same confirmed queue instead of a private receipt-free bypass. Legacy Channels6comparisons, Scratch360/36 and Recovery24 passed with the same5runtime SHA. Normal/QA/offline Editor0/0;130CPU groups/391corrupt receipt/manifest fixtures and32coverage cases. Local proofs/fingerprints:`artifacts/fog-channels-213/`; bundle validation before extraction:`artifacts/fog-bundled-213/`.

**Boundary:** runtime Channels is verified through Unity fixtures. Game Visual/Atlas still use the old route. Committed-ledger recovery, split static source/dynamic atlas, actual28-part admission, spatial ledger, house/FPS and permanent board remain open. Private comparison barriers do not measure game scheduling throughput. Valheim was not launched; installed .213/embedded bundle unchanged.

### Committed recovery: confirmed owner return

`TryGetRecoveredSnapshot` reads only the owner's committed `Ledger[2]`, separately from transaction and retirement receipts. A copied result is valid only for all five fields of `LastSubmission` and an empty, ready, non-faulted input queue. Pending/new input, newer sequence, retirement or whole-pool uncertainty cannot authorize recovery. A failed/invalid readback returns false and retains the owner; a newer confirmed sequence permits another observation. This query never retires or frees the owner itself.

Retirement keeps the ledger while its recovery readback is native-pending. Dispose drains that request before releasing resources. Normal queries do not wait or inspect expired native handles. The approved solver/shader formulas and thresholds are unchanged.

Actual D3D11 gates: Recovery48 real frames covers delayed copied results after native expiry, workspace isolation, capture/submit during recovery, stale sequence callbacks, slot generation reuse, pending retirement/disposal, smoke-only=2/surface-only=1 and a failed readback. A separate two-wall physical route first observes active state, then both committed ledgers become zero at480 ticks (16seconds simulation); fields, ledgers and published atlases still match independent dense solvers exactly. No synthetic initial-empty success can finish that route. The original physics counts remain240commits/2safe rejections,664field/498ledger/28atlas/242metadata comparisons; the return route is additional.

Frames and published rendering passed again;87 captures/62 comparisons/26 negative controls, legacy16 pixel-exact, mean9.8727858e-8/maxlocal0.00048828125. Normal/QA/offline Editor builds0warnings/0errors,130CPU groups,449 corrupt-report fixtures and32metadata-coverage cases passed. Independent correctness/Ponytail: PASSED/Ship. Local raw evidence and matching runtime fingerprints: `artifacts/fog-snapshot-recovery-213/`.

**Limit:** runtime Channels are invoked by isolated Unity checks. Game Visual/Atlas still use the old path; whole-house spatial ownership, source layout/admission, gameplay FPS and the persistent project/board are unproved. Installed .213, embedded working bundle and all game saves are unchanged. No Valheim was launched.

### Runtime Visual/Atlas: separated source and fields

`BlueprintFogAtlas(assets, snapshots)` owns only an immutable RHalf source atlas and bindings. The sparse pool retains the two dynamic atlases and published metadata; no per-frame field copies or additional dynamic textures are allocated. Static packing uses one new owned `PackStaticSource` kernel, leaving all approved solver formulas and frozen references unchanged. Candidate5 assets/28 kernels,93713bytes, SHA `9E9A864C2D32F0E70088FA756DA9A27266FBE6ED99614D63113D658F91D993C8`, passed load/publication/rejection/reload.

`PreparePublished` must run before camera geometry; Render rejects an unprepared/changed mapping. Exact pool handles become slot/64-bit identity/generation bindings. Duplicate, foreign, stale and count-invalid handles are rejected. Removed visuals and Dispose restore their ordinary material route before releasing bindings. The atlas borrows pool textures and never frees them. RHalf layout uses the remaining128MiB render allowance after dynamic atlas reservation; oversized static layout rejects before allocation, retaining the old atlas.

The actual runtime Visual/Atlas path, not a duplicate fixture shader setup, passed30 captures/14 comparisons/7negative cases against independent dense Visual/Atlas. The owners have different actor paths and capsules. Dormant/initial/active, geometry before CPU confirmation, metadata-only, packing rejection and reorder pass; deliberately swapped handles visibly differ by0.00239947. Correct maximum mean7.03359e-8/local0.00048828125. Static source packs only twice across updates; two-owner static textures8122272B plus borrowed dynamic21086464B total29208736B. This excludes source/pigment, solver fields, driver and oracle allocations, and is not house memory/FPS proof.

Original87/62/26 checks and16 pixel-exact legacy captures remain. Physics240/2/664/498/28/242, real frames, Recovery48 (physical recovery480ticks), and the prior Atlas suite pass with matching runtime sources. Normal/QA/offline Editor0/0;130CPU groups/501 corrupt-report fixtures/32coverage. Independent review initially rejected a symmetric mapping fixture; it now uses asymmetric inputs and an explicit sensitive wrong-mapping control, PASSED/Ship. Raw evidence: `artifacts/fog-render-wiring-213/`.

**Next integration boundary:** retain the accepted logical-part input queue while a physical slot waits/retires, and replay it on a new generation. Continuous input cannot invalidate every recovery observation forever; reentry cannot undo retirement or discard accepted movement. That scheduler and Building activation are not implemented by this renderer change. Real28 admission/spatial ledger/house/FPS and persistent project/board remain open. Installed .213/working bundle and saves unchanged; no Valheim launch.

### Logical source: part queue and generations

`SnapshotSource` keeps one detachable `BlueprintFogInputQueue` independently of its physical `SnapshotChannel`. Registration is dormant; accepted contact input waits for a free slot. Factory initialization starts at the committed queue baseline, never allocation-time actor/clock. Queue attachment happens only after successful GPU initialization. The physical State remains immutable even when that queue later attaches to another generation; old callbacks/handles cannot confirm new input.

Capture continues during recovery and retirement. Pump first confirms all physical releases, then grants slots starting after the last successful acquisition, so a retiring source cannot immediately seize its old slot ahead of a waiter. Recovery temporarily freezes submissions while preserving capture. A queued contact cancels the barrier; with an empty tail the last committed contact holds the owner. A recovered generation may retire with a noncontact accepted tail; that tail survives release and is replayed by the next generation. New generations submit retained input before querying recovery, avoiding an initial-empty retirement loop. Overflow/unknown remain sticky and retain accepted history.

Source release callbacks run only after confirmed physical release or whole-pool drain. Review found an orphan path: failed GPU initialization left Source.Snapshot null while its physical owner remained retained. RemoveSource now defers its callback whenever pool teardown is required. The injected fault test proves removal does not close the source early and repeated Dispose closes it exactly once.

Actual real-frame gate: 105total/57source frames; two sources share one slot, reentry during recovery and retirement, fairness, baseline/path replay, contact hold, idle-tail replay, pending removal, stale closed source and failed acquisition. An independent physical-wall gate queues8 observations behind an occupied slot, starts at the original0 clock, replays them unchanged and exactly matches4 fields/3 ledgers of a dense solver; the state must be nonzero. Existing physics240/2/664/498/28/242 plus complete return480ticks, frames and render30/14/7+87/62/26/legacy16delta0 remain. Normal/QA/offline Editor0/0,131CPU groups,533 corrupt-report fixtures/32coverage; independent correctness/Ponytail PASSED/Ship. Evidence: `artifacts/fog-logical-sources-213/`; seven runtime/input fingerprints agree across four GPU modes.

**Limit:** an accepted non-emitting tail is replayed rather than discarded and can require a fresh slot; cold-state fast-forward and throughput are unproved. Logical Source is not yet connected to Visual/Building. Next: borrowed source-texture lifetime, geometry binding before draw, continuous dormant source clock, full-domain real28 admission/spatial ledger and house look/FPS, then durable board/material/reload/server. No game launch, install, working bundle replacement or save changes.

### Logical Visual and the continuous-idle blocker

Visual detects the strict pool, registers one logical source and captures immutable samples without directly advancing the shared solver. The factory retains source textures, bounds and noise frame independently of Visual lifetime. Removing Visual hides geometry but defers source texture/mesh disposal until confirmed release or pool drain. Both visual-first and pool-first disposal are exercised with pending work. Strict visuals reject compact physical domains and legacy volume/atlas paths; published mapping must belong to the same visual/pool. Recovery-retired sources render as intentional dormant sources while physical release finishes.

An exact unassigned binding tuple permits only this source's CPU clock/body while no GPU slot exists. Invalid owner/generation/region/stride/short buffers remain inert and cannot select that fallback. Assigned sources continue to read committed GPU metadata, even with newer CPU input pending. This fixes the visible noise-clock reset to zero after unassignment. The dormant check exercises both accumulated geometry and GhostClear.

Actual Unity GPU evidence:8 mesh/volume comparisons, maximum mean1.2938868361736031e-8 and local0.0001220703125; delayed receipt/new CPU body, deferred source destruction, both disposal orders, invalid mapping/compact/legacy rejection. Existing30/14/7 plus87/62/26 and16 legacy delta0 remain. Candidate5assets/28kernels,100866B, SHA AE54E1539481B664AAB3710A634CB5E5B60A69A352F84E0DF5DC7D16FC89C8F5. Normal/QA/Editor0warnings/0errors;131CPU groups,574 negative report fixtures/32coverage. Correctness PASSED/Ponytail Ship applies only to this source checkpoint. Protocols and source hashes: `artifacts/fog-logical-visual-213/`.

**Required fidelity gate still fails:** after3.2s dormant, the first contact at3.4s differs from continuously running v11 by0.11407470703125 in a local RGB channel. AdvanceVelocity transports/damps old velocity and adds curl/buoyancy even with Emit=false. TrackDormant retains the latest observation, not that velocity history; a fresh generation starts with zero velocity. Retirement/reentry has the same risk. The passing integration oracle explicitly starts at the same3.2s baseline; it does not replace the failing continuous control. Report fields keep continuousV11Parity=false and continuousIdleControlSensitive=true; tolerances were not widened. This path is not activated in Building or installed.

Next: preserve bounded idle/reentry dynamics without dropping history, then prove full-domain real28 admission/spatial source ownership and house look/FPS. Increasing the per-part dense count cannot silently bypass the field cap:28 velocity fields alone cost137625600B before workspace. A shared global idle field is only a proposal until different local boundaries/lattice phases are proven equivalent. The durable project/board/material/reload/server work follows the accepted visual path. No Valheim launch or save changes.
