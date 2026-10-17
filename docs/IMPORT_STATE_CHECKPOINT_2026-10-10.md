<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .138 — scoped foreign-state conversion work

Code b89cac712e15e2853fb13111d5502b42c1c8f848. Ordinary ZDO field-type map is
lazy and document-local in PrepareForHost and ReviewImportFile. No global cache;
source identity, row, type, unknown-field and atomic conversion checks remain.
Exact0/1/-1 numeric strings bypass float.Parse only after legacy blank/comma
handling; every other spelling retains invariant parser and finite validation.
Baseline regression rebuilt64 maps; final preparation/review build1, salted hash
callbacks stay independent, unknown fields/source evidence remain intact. Float-bit
parity covers signed zero, whitespace, exponents, legacy and invalid/overflow cases.

Full Store, normal/QA0/0, Host25185596, localization948, isolation/preflight and both
reviews passed. verify absent. Geometry/UI/input/render unchanged; no dedicated
Geometry/Unity rerun. Previous .136/.137 evidence is not native .138 runtime proof.
Installed only TerrainRamp-1.0-Test; hashes match, Default clean,137 existing
config/import/save files unchanged. No game/world/network execution.

File-only tests use temporary copies of364docs/610822parts/359sources, unit-scale
resolver, no Unity prefabs/UI/thumbnails; OS/JIT caches not flushed. CoffeeTable4
samples/version, including reversed order: worker2275.002→2097.956ms, later
catalog880.639→805.916ms; initial catalog2973.122→2779.769ms; maxpoll26.893→25.500ms.
4096parts2samples/version: worker2484.710→2472.413ms, later catalog978.307→924.658ms,
initial3015.433→2857.410ms, maxpoll26.411→27.019ms. Heavy worker improvement is NOT
established. Independent preparation hash calls1403214→196784 do not prove speed;
map-only timing showed no material improvement. Independent probes are not additive.
Build-overlap trials retained/excluded; final trials ran after builds. Initial
sandbox Store/NuGet access failures passed on the same elevated checks. See
artifacts/checkpoint-0.19.138/{verification,timing-summary,timing-provenance}.json
and Russian checkpoint for full limitations. Existing QA.137 cases remain pending
on .138; native construction/reload/network/cursor and overall import latency open.
