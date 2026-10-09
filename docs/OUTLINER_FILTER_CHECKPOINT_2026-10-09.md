# E08 — hierarchy-preserving outliner filters

2026-10-09. Source and automated evidence only; not deployed. QA0.19.116
remains installed in TerrainRamp-1.0-Test, SHA256
`867E5E474A3C1B335500E47F1B2DC3A6250AC1FAA36E6D92E766E904F33DCF3A`.
No game, world or save mutation occurred.

The explicit FILTER menu replaces the five-state cycle: all, selected,
hidden, locked, missing, unknown source/type, nonempty sign and nonunit
relative scale. Search can target name, prefab/record ID, recorded source
and version, or recorded type. Unknown metadata and unresolved absolute
scale are not inferred. Matching records retain their ancestor path;
filtered branches expand temporarily without changing saved collapse state.
Free supports participate in name/ID searches. No document, selection,
hierarchy or history mutation is introduced. Group selection and Ctrl+A
still include records outside the filter; menu help states this explicitly.
No-match reset restores the previous collapse state; an unfiltered empty
document does not display a misleading search reset. The shared menu stays
inside the safe area, scrolls and returns focus to the search input.

Normal/QA: zero warnings/errors; localization925, Store and HostContract
Valheim Steam25185596 passed. Unity6000.0.61f1 completed480 screenshots at
`2026-10-09T16:52:28.5095963Z`, four resolutions and100/120/140%.
Actual menu/row clicks, scoped queries, metadata uncertainty, unchanged
selection/history/hierarchy, empty/reset and collapsed ancestors passed.
A50000-part/64-group document remains row-virtualized and finds its last
missing record; list binding measured69ms on this Workbench, not game FPS.
The parent inspected the1280×720/140% Russian filter screen. Independent
review results are recorded in `artifacts/checkpoint-0.19.117/outliner-verification.json`.
The unavailable verify helper is replaced by direct checks and read-only reviews.

Fixture `tests/fixtures/metadata/20-tree-filters.buildworks.json`, card
**QA E08 Tree Filters**, passes import and portable roundtrip with fresh IDs
and preserved nested relationships. It contains two native walls and a
synthetic missing record; its recorded envelope is not a real missing model.
The artifact directory .117 does not identify an installed .117 release.

Owner acceptance remains pending deployment. The exact Russian procedure,
initial conditions, buttons and failure signs are in
[the Russian checkpoint](OUTLINER_FILTER_CHECKPOINT_2026-10-09_RU.md).
It tests retained missing records, nested filter paths, collapse/reset,
name/ID/type search, uncertainty and no unintended edits. No resources or
world construction are needed. Game input/readability remain unverified;
world building, reload and networking are separate acceptance gates.
