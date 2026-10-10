# .132 — reuse unchanged foreign parsing within import validation

Code checkpoint `4e9b34e`; diagnostic checkpoint `b9e6312`. QA 0.19.132 is installed
only in `TerrainRamp-1.0-Test` through the guarded deployment script.
[Russian evidence and exact timings](IMPORT_VALIDATION_CHECKPOINT_2026-10-10_RU.md).
[Targeted manual acceptance](QA_0.19.132_RU.md).

Two fresh-store runs per case on a copy of the actual 364-document, 12-file,
31,564,103-byte QA library: CoffeeTable worker remaining wait fell from 4.10–4.29s
to 2.09–2.33s (mean −47.3%); 4096-part fixture from 4.37–4.43s to 2.52–2.65s
(−41.3%). Repeat ran without concurrent builds/Unity. OS/JIT caches were not flushed.
Unit-scale file fixture only, no native prefab metadata, UI or thumbnails.
Initial catalogue preparation still takes ~2.9–3.0s; post-import ~0.8–1.0s.

The cache lives inside one import worker's two validation reads. Every file/member
is reread, byte-budget checked and strict-decoded before exact text comparison.
Native JSON bypasses caching; each read owns its PNG envelope. Shared parsed lists
remain read-only during validation. Future ordering, staged validation, limits and
atomic move remain intact. Do not reuse this cache for mutable editor objects.

Full Store and focused parity/change/duplicate/PNG/budget/invalid UTF-8 regressions
pass. Normal/QA 0 warnings/errors, Host25185596, localization933, isolation/preflight,
Unity492 (`2026-10-10T12:19:54.4761975Z`), correctness PASSED, Ponytail Ship.
Geometry unchanged/not rerun; .130 evidence retained separately. `verify` unavailable.

Both installed DLL hashes match QA artifacts; Default is clean. All 118 existing
protected config/import/save files retain hashes and metadata, including 60 QA-save
files. No Valheim launch, world construction/reload or network acceptance performed.
