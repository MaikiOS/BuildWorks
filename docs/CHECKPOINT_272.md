# Checkpoint272 — real GPU timing for fog rendering

2026-10-21. Isolated Unity6000.0.61f1/D3D11,RTX4090;no Valheim,DLL or save changes. GPU-enabled CustomSampler works in batchmode/PlayMode:36submissions,30positive samples. Capability alone is not fog timing.

The fog marker surrounds only `atlas.Render`:28procedural objects/32resident,960×540,frozen after8ticks. Unlit/lit/lit/unlit arms each execute26real render callbacks;6warmup frames leave20positive GPU samples with blocks1 per arm. Unique markers isolate arms; history/confirmed state stays frozen. Lighting1.6 and24lighting steps are preserved.

| Arm | GPU mean ms | GPU median ms | CPU atlas submit mean ms | CPU readback wait mean ms |
| --- | ---: | ---: | ---: | ---: |
| Unlit A |30.73|30.85|0.22|32.24|
| Lit B |89.53|87.86|0.21|92.23|
| Lit B |90.53|87.90|0.22|92.14|
| Unlit A |30.30|30.44|0.21|31.64|

This stand is dominated by GPU rendering,especially lighting. CPU readback waiting mostly waits for GPU; receipt grouping269 does not remove that cost. GPU data is delayed relative to CPU; frozen-arm aggregates are meaningful,per-frame subtraction is not.

Evidence:`artifacts/checkpoint-272/capability-20261021-093800-fddecc69/`,`fog-timing-20261021-094252-7c791dab/`,`timing-summary.json`. First fog attempt retained CS0103 from a missing using in the test;production untouched. Corrected run exit0,settings restored to priorSHA,csc.rsp removed,Editor exited. Independent correctness/complexity review passed. Bundle `0BF387946A0215DA98087D796E3B34C02DE52A82AA9602D5CAE2E0CBE42A5A66` remains local.

Reproduce: `scripts/Build-FogGhostAssets.ps1 -CandidateOnly`,then `scripts/Measure-FogGpu.ps1` for capability or `-Fog` for fog. Refuses existing game/Editor;never launches Valheim. Checks manifest/bundle/runtime mirrors,positive GPU data and cleanup. Canonical script separately executed;artifact path in `artifacts/checkpoint-272/canonical-run.log`. Gameplay/source deployment250 unchanged.

Next small DXBC-based candidate: exact brick coordinates using two integer divisions instead of three;compiler/negative/parity first,then timing. **Not implemented yet.** Do not reduce quality/lighting steps.

Solver GPU,total frame,asynchronous service headroom,native house and game FPS remain unmeasured. Board/completion/full paid cancellation and networking remain open. [Checklist](LIVE_CHECKLIST_RU.md),[271](CHECKPOINT_271.md).
