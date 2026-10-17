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
