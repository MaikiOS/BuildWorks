# .240 — bounded RAM reuse did not resolve the whole-house failure

2026-10-19 · [Русский](CHECKPOINT_240_RU.md)

Both48s attempts failed at queue64; repetitions stopped. Synchronous capture:25.676s/31.391hostFPS/91captures. Async capture:34.374s/35.929hostFPS/220completed of221requests. The140scalar/196field oracle was not reached in either failed attempt. No dropped accepted history, enlarged queue, explicit simulation polling or reduced GPU limits.

The experimental page pool passed15 grouped actual-source CPU checks and252 exact GPU paging comparisons, including receipt/velocity order, rejection, metadata and retirement. At most32 pages were actually allocated (157286400B);2489/3429 reuses. Hard limit capacity+resident=36, free<=4. Baselines clear, stale leases cannot write a reused array, old committed pages survive until matching receipt+velocity. Faults retain custody until GPU drain.

**Not promoted:** integration is guarded by `BUILDWORKS_FOG_PAGE_REUSE_CANDIDATE || UNITY_EDITOR`. Normal and installed default QA .240 exclude it (0lease fields/0Rent calls); explicit candidate has4/2 and a pre-copy custody guard. Normal8 methods/handlers match the baseline after generated closure-name normalization only. No native .240 Valheim run or house visual acceptance.

Async recording is Workbench-only, one960x540 in-flight target, full backpressure fails, cleanup drains its request. First-frame raw bytes matched synchronous RGBA32; legacy RGB24 equivalence and per-PNG submission metadata remain unproved. It records more frames under a different workload, so elapsed times are not direct speed comparisons or GPU timings. The final pending221st request is not claimed as a saved frame. ValheimQA recorder unchanged.

Geometry, normal/QA/explicit candidate builds0warnings/errors, current host, localization1122, QA separation, candidate IL contract, offline final Editor compilation and independent correctness/Ponytail review passed. Receipt gate8positive modes/1manifest/81negative fixtures is CPU-only. Default QA .240 deployed with hash `19D8521FECBF78A79F3B305F59933132D15311D33EE41FDD052731A5F65B354D`, normal `F4B40C75B518349092261F9D74F7054DE94EFB06FFC404FE7D60B642838DD82F`, Geometry `4166DB0659711459BFEE0CFD17635D4287EEB84283DC9CD945774FB7EAF6D2E4`. No game/world/inventory/resource changes; both own Unity PIDs exited1 and runner settings were restored. Initial offline path/output-name command errors were corrected, not treated as game defects.

Raw evidence: `artifacts/checkpoint-240/`, `artifacts/fog-production-frames/ReuseCandidate-20261019-182439-52fc5a8d/`, `artifacts/fog-production-frames/AsyncCaptureCandidate-20261019-183008-9c6d0c2a/`. The successful paging receipt is copied into checkpoint240; it is separate from the failed frame tests.

Next: analyze the saved paging/receipt throughput traces before another GPU attempt. Allocation reuse and removing most synchronous ReadPixels cost did not eliminate backlog at35.93hostFPS. Keep queue/history/budgets intact. Native house, durable paid cancellation/refund, special contents, multiplayer and all14goal requirements remain open. Goal active.
