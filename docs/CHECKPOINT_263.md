# Checkpoint263 — shared cancellation scan and background measurement limits

2026-10-20. Source and installed QA **0.19.250**. Existing fog/shader, payment rules and single-set executors are unchanged. Full paid board cancellation remains open.

The existing reader can now verify construction operations and payout IDs in one complete mapped-chunk scan of one saved-world generation. Immutable sets preserve the old constructor/50000-part bound and allow65536 payout IDs; distinct keys, duplicate/empty rejection and copied inputs are checked. Composite legacy getters refuse rather than expose only one namespace. The host overload retains worker/generation/custody/thread fences. No journal/plan format or terminal/UI transition was added.

Normal/QA builds0warnings/0errors; Geometry passes597 saved-world assertions; ContentReservations passes195 generation/worker/custody/file assertions with host substitutes. Host contract and localization1122/1008/built resources pass. Independent review passes; `verify` skill unavailable. Guarded TerrainRamp-1.0-Test installation and DLL-pair/version readback verified with game closed; new .250 has not had a native game run.

QA DLL SHA256 `A5608BDC767CB49F538276AC9499FA1F4A4E5F4F6D0894AE63D3EFC12DB6113E`; Geometry `0C27A7FBA028F97C065602B632A05BFD7B84AC9065F675600140CB8DE8CB8916`.

Native run on unchanged **.249**: `ValheimQA/artifacts/20261020-195725/`,12setup+5close, two routes2/3 through an owned unpaid28-part Forest Hut.52.046/52.073s,1875/1876 consecutive host frames,36.006/36.007FPS,26windows36..36.5,max40.83/35.13ms; routes complete,28 recovery fields zero, inventory unchanged.

**Simulation/host cadence only:** independent evidence shows RenderFrames=0 throughout. The background host does not draw the house automatically without Camera.Render. These numbers do not establish rendered cadence, visual smoothness, GPU execution time or v11 appearance. The [262](CHECKPOINT_262.md)343/520 recording remains FAILED.

All87previous project files unchanged; only the owned new unpaid plan cancelled/revision2/checksum checked.4141ordinary-save files retain membership/size/mtime, not content-hash proof. Pins restored, normal logout/quit and bridge3restored. No grants/native construction/terrain/reset/other profile/network.

Next: bounded render-only use of the existing QA observer/Read(...,pixels:null), verify actual rendered-frame count and avoid PNG/readback workload; matched visual acceptance remains open. In parallel8.7→8.8: wire the composite proof into a durable terminal and validate rollback/replay before public board cancellation.14sections/115IDs/16scoped checks preserved.

Local evidence: `artifacts/checkpoint-263/` native-verification, build/test logs, deployment-verification and reviews. This source-only checkpoint publishes no binaries, game assets or user saves.
