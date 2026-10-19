# .239 — fog throughput measurements

2026-10-19 · [Русский](CHECKPOINT_239_RU.md)

Separate TelemetryMeasurement observes the unchanged algorithm for20s at target36FPS, with no explicit polling/post-capture pump. Existing48s stress gates/failures remain. Actual20.024s/32.16hostFPS,59captures,maxQueue45,drain1.77s; exact140scalar/196field comparisons passed. Growing queue is not whole-house stability.

Window totals:1964RestoreCreate252.47ms/PageRelease113.96ms/Upload1441.12ms/CopyTexture enqueue6.51ms;1972CallbackCopy2197.34ms, Solver submission255.12ms, inclusive Submit454.73ms. Capture Render63.60ms/ReadPixels2802.19ms/PNG390.53ms/disk41.56ms. CPU wall intervals are not GPU execution time; ReadPixels can include GPU waiting, Submit includes solver, overlapping owner waits cannot be summed into CPU percentages.

1972successful velocity callbacks allocated/copied1972 arrays of4915200bytes:9.6927744GB of payload allocations, not overall memory or measured PCIe transfer.1972batches/14020samples/15562ticks,1964cached pages. Average request→callback-end33.5ms/callback-end→accept4.6ms, including delivery/copy/owner-thread handling. Invalid/stale/thread mismatch/unobserved0. GC.CollectionCount deltas1007each must not be added or called GC pause time. Managed bytes85.45→166.14MB are snapshots, not a peak.

Observer is bounded32slots×2request kinds, keyed owner/generation/sequence; received differs from accepted, cached aliases are excluded. Bad observations invalidate measurement without throwing into mechanics. Snapshots detach data. Normal8methods and handlers including callbacks match .237 instructions after only compiler closure-number normalization,0observer/clock calls. QA observer defaults off and enables before owner creation.

Geometry with15observer CPU checks and574saved-world assertions, receipt gate6modes/1manifest/65negative, normal/QA0warnings/errors, host1.0.20/Steam25909357, localization1122, QA separation and independent correctness/ponytail review passed. GPU20s and exact comparisons passed, not fault/retirement/reuse/network. Shaders/bundle/payment/history/32/32768/Spatial3D/4resident and overflow guards unchanged.

Next candidate: bounded managed-payload reuse rather than GPU texture-pool redesign (Create+Release only0.37s/20s). At32/4, cap capacity committed plus resident staging pages to36×4915200=176947200bytes, including every unique pending/free/committed array. Existing RAM counter can double-count cached aliases and is not a hard cap. Retain the old committed page/stamp until matching receipt and velocity acceptance, then swap; guard stale/late callbacks, cached alias, faults and teardown custody. CPU ordering/alias/bounds/fault checks precede exact GPU reuse/48s stress and native house. This is a plan, not an implemented fix.

Evidence: `artifacts/checkpoint-239/`, `artifacts/fog-production-frames/TelemetryMeasurement-20261019-180336-89a3c1f8/`. Unity PID54820 exited0, runner restored settings. Deployment/native .239 proof tracked separately. No game saves/resources used. All14goal blocks retained; goal active.

**.239 deployment:** guarded deploy/hashes PASS; QA DLL `EBB944D46849727A2EC7F6A4A60C2F4D4AFDB343B4EA2D3AA113A310CF0925BD`. Normal/QA/source builds, host/localization1122/separation passed. Valheim has not run .239:20s measurement used Unity workbench, not a native house. This step did not modify game saves/resources.
