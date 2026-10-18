# .216 — hardware-filtered sparse v11 publication

2026-10-18 · [Русский](CHECKPOINT_216_RU.md) · [Complete open scope](LIVE_CHECKLIST_RU.md)

QA **0.19.216.0** installed only in TerrainRamp-1.0-Test; SHA `CCDF3CA513C8D2A39E47564E698D300C16B790124B264C108F878EF4442CE45C`. All143 profile data files unchanged during installation. The working embedded bundle is unchanged; the strict backend is **not activated by Building**. No public binary release.

## Implementation and evidence

One bounded GPU page texture replaces per-owner dense dynamic copies. Each page includes one-cell neighbor halos and uses native hardware filtering.8192 blocks allocate126×126×114 texels, **14478912 bytes**, independent of owner count. The tested static+dynamic render resources total **22601184 bytes**, previously29208736. These are texture accounting figures, not total process memory or FPS.

Published map-bank address and metadata advance only after successful packing; rejection retains the previous address and all32 metadata words. Identity/generation/bank/buffer limits and physical page capacity are checked, including rounded unused texture cells. GPU drain still owns texture lifetime. Dense reference exists only as an independent oracle.

Manual eight-tap interpolation was rejected after two unchanged local-error failures0.0611572/0.0612793 against0.03. Logs remain; no threshold relaxation. Hardware pages passed continuous56 field/ledger and18 image comparisons, runtime30 captures/14 comparisons/7 mapping negatives; runtime mean8.00248e-8/local0.000610352, sensitive cold control0.114075. Page tests cover15 invalid addresses/buffers/owners/generations plus empty-neighbor and edge sampling.

Normal/QA builds0warnings/0errors; host/control/isolation/localization1104, Geometry131, corrupted-evidence1664/metadata32 passed. Physics240commits/2rejects/664fields/498ledgers/28atlas/242metadata; Recovery90/42frames; Frames220real frames/54confirmations. Frames exposed a private test-observer race: a new runtime tick could be compared before its independent oracle callback. The fixture now waits for both exact callback counts; the first failure remains recorded. Reservation fixtures include the actual extra4bytes per owner; memory protection remains intact. Independent read-only reviews passed; `verify` is unavailable.

Candidate5assets/28kernels SHA `A24A7CED68795563AAE47A40113BB55C6944E4DA2D0FDC7BF32A1B023F5CF99E`, not the working embedded bundle. Local proof folders: `artifacts/checkpoint-216`, `fog-runtime-assets-candidate`, `fog-published-render`, `fog-arena-physics`, `fog-arena-frames`, `fog-runtime-recovery`, `fog-packed-publication`.

## Actual background ValheimQA

The standing18October workflow ran GABS/ValAgentBridge1.8.0 without desktop input. Verified profile/background/batchMode/qaSaveRoot, cloud=false. Connection13.141s, existing-world entry17.781s, measured for this session. **7/7 steps**,0optional failures, clean repeat: real candidate loading, GPU allocation14478912bytes/reserved fields58779988, published address86400, rejection retaining address/all32metadata words, and texture destruction after Dispose. Complete bridge-visible inventory/backpacks matched. This proves native host resources/metadata, **not PublishFields/house rendering/building costs**.

The first packet stopped on incorrect escaped-string expectations even though the probe returned success. Its script/report remain; exact equality of the complete returned JSON string replaced faulty contains assertions. Initial optional Backpack API warnings are separated from the clean repeat. No grants/construction/terrain edits. Normal logout/main_menu/Application.Quit, owned PID exit, three hash-restored bridge DLLs. Only config version header changed .215→.216;4137 ordinary save files retained size/mtime, not full content hashes. QA saves changed normally; no token/receipt/catalog resets. Private evidence: `Default/ValheimQA/artifacts/20261018-102426`, excluded from GitHub.

## Remaining boundary

Dense dynamic publication no longer imposes the former house-size allocation. **28 velocity states still need137625600bytes against98304000 field cap**; whole-house support remains false. Next candidate is exact cold velocity RAM storage with bounded hot GPU slots; implementation, queue/lifetime/fault handling, promotion before contact and timing remain unverified. No history reset or approximate replay.

Next: full-house visual v11 and frame timings, then durable project/stake/board/materials/acknowledged cancellation-refunds/reload/server. No new owner fog test is ready. All14 live goal blocks remain; accepted cases need no unrelated repetition.
