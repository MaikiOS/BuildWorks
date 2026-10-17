<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .148 — source evidence temporary memory

Source `3a23969`; normal/QA0.19.148 built, installed QA remains0.19.146.
No Valheim, Unity or input while Ostrix works. Previous .147 checkpoint retained.

BlueprintSourceDocument encoded text strictly for validation, then concatenated
and encoded the full identity again for SHA. It now hashes the prefix plus the
existing validated bytes using two TransformBlock calls and an empty final block.
No dependency/cache/retained buffer/schema migration. IDs remain byte-identical;
strict text UTF8/budgets/source row binding/cache checks unchanged. Prefix encoding
retains previous name fallback semantics. Gameplay/resources/authority unchanged.

Red regression reproduced excess allocations. .NET10 constructor allocations for
7340045 UTF8 bytes:29361576→7341440, about75% lower. This proves that constructor's
temporary allocation improvement, not process/Valheim peak memory. Tests compare
with the independent former concat+SHA algorithm, empty/Unicode/BOM/CRLF input,
malformed name fallback, invalid Unicode text and byte cap. Full Geometry/Store353(351/2),
normalQA0/0, Host25185596/localization973/isolation/EditorBridge/scoped format passed.
Independent correctness/Ponytail: Ship. Global15 pre-existing WHITESPACE from .147
remain; unrelated files unchanged. verify helper absent; direct checks used.

Installed Mono TransformBlock IL skips BlockCopy for null outputBuffer. IL and
mscorlib SHA retained; static API evidence does not prove new DLL gameplay.

.NETFramework4.7.2 probes used identical temporary copies of12 files/31564103bytes:
364 documents/610822 parts/359 sources, unit-scale resolver, no native prefab/UI/
thumbnails. Independent probes are not additive. Four baseline catalogue reads
(including reverse order) averaged2231.8ms, two new1897.8ms; cloned evidence320.8→
163.2ms; PrepareForHost912.5→770.7ms. Synthetic4096-part import (two per version):
initial catalogue2904.4→2884.9ms; remaining worker wait2611.5→2478.9ms. Variation,
few samples and unflushed OS/JIT caches mean no broad end-to-end speed claim.
Current fixture07 actually has1025 parts; initial probes retained separately;
4096 authored file generated under artifacts. Imports verify original/archive bytes.

Evidence/SHA/baseline binaries/timing summary: `artifacts/checkpoint-0.19.148`.
Future deployment must use Deploy-TestBuild and both DLLs in TerrainRamp-1.0-Test.
Game memory/speed, construction/resources/reload/network remain open. Unity layout/
Workbench deferred; no UI/geometry change. No new hash-specific manual series:
after deployment, use existing deferred fixture26 and main QA queue without
repeating accepted cases. See [Russian protocol](SOURCE_MEMORY_CHECKPOINT_2026-10-11_RU.md).
