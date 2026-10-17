<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .133 — retain existing immutable evidence when cloning sources

Source-only checkpoint `5dfeaa2`; installed QA remains0.19.132.
[Russian evidence](SOURCE_EVIDENCE_COPY_CHECKPOINT_2026-10-10_RU.md).
[Unchanged manual scenario](QA_0.19.132_RU.md).

`CloneSources` copies the mutable envelope and retains only its already populated
immutable evidence cache. `ToGeometry` still checks Id/Format/Name/Text and verifies
content identity on changes. `FromGeometry`, serialization and gameplay are unchanged.
Legacy clone fails the focused identity assertion; final checks cover independent
records, all four tampered fields and valid new-text rebinding.

Two independent file-only probes on364 documents/610822 parts/359 sources:
warm `ImportCopy + Index` falls370.605/379.393ms to45.012/47.110ms (mean−87.7%).
Cold reading/full preparation are not improved; do not add independent probe times.
Final CoffeeTable worker remaining waits2445.640/2282.965ms, post-import catalogue
851.975/811.322ms: no additional end-to-end import speed claim.
An earlier raw-library retention variant slowed worker waits to3375.919/3246.391ms
and was removed. Separate `early-retention` logs preserve that rejected experiment.
GC pressure is an unproven hypothesis; memory was not profiled.

Final full Store, normal/QA0/0, Host25185596, localization933, isolation/preflight,
and repeated correctness/Ponytail reviews PASS/Ship. Geometry/UI unchanged and not
rerun; latest Unity492 evidence belongs to .132. `verify` unavailable.
Installed .132 hashes rechecked, no deployment/Valheim/save/network operation.
