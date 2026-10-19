# Diagnostic238 — whole-house fog throughput

2026-10-19 · [Русский](CHECKPOINT_238_RU.md)

Game version remains0.19.237. This completed diagnostic has a negative result; no production fix, promotion or deployment.

Explicit polling before the original pumps failed queue64 at34.15s/30.72FPS with140captures. Separate PostCaptureCandidate, adding polling plus exactly one PumpSources after every completed capture, also failed queue64 at35.05s/29.24FPS:135captures/135extra pumps. Exact dense comparisons were not reached (0scalar/0field), never passed. CPU receipt gate5modes/1manifest/48negative and independent correctness/ponytail review passed only for the diagnostic.

Accepted observations, solver, shaders/bundle,32slots/32768blocks/Spatial3D/4resident and140scalar/196field/48s/drain5s gates are unchanged. All28 owners keep progressing (final ticks971–979,4resident), indicating aggregate throughput deficit rather than a stopped owner. A later failure is not a proven runtime speedup. Repeats stopped after two equivalent failures; native house was not repeated. Unity PID53564 exited1 for the failed scenario, runner restored settings after exit. Game saves/catalog/resources/payment and installed DLLs unchanged. All14 goal blocks retained; goal active.

Next measure actual batches/samples/ticks/admission refusals; receipt and velocity submitted→callback→accepted latencies/bytes/copy time; eviction wait; texture Create/Release/upload CPU time; Capture Render/ReadPixels/PNG/disk separately. Update1833+1833 counts calls, not completed transfers. Dispatch Stopwatch measures CPU submission, not GPU execution. Keep bounded aggregate counters, not per-dispatch log spam.

Source identifies a hypothesis, not a measured cause: velocity RenderTextures are destroyed at page-out and recreated on restore,4915200bytes per payload. Reusing four textures requires exact fields, unique leases after confirmed page-out, late-reply/retirement/fault/dispose tests, and accounting idle textures within the same total four-texture budget. Do not change ownership before measurement.

Post-capture service would still differ from an OnRenderImage hook: synchronous ReadPixels/PNG/disk occur later. Never add capture to production as a workaround. Production service must cover all snapshotSlots including retirement, not visible parts only.

Local evidence: `artifacts/checkpoint-238/`, full raw reports/logs/frames under `artifacts/fog-production-frames/PollingCandidate-20261019-172119-b06faa2a/` and `PostCaptureCandidate-20261019-173856-b5c809e5/`. Runtime fog source and embedded bundle remain unchanged from .237. Procedural geometry is not native-house appearance/FPS/network acceptance.
