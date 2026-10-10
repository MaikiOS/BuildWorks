# .131 — tools popup and compact outliner filters

Source0.19.131, local checkpoint `279d265`. Not deployed; QA0.19.117 remains
installed in TerrainRamp-1.0-Test. No Valheim, save or network execution.

The tools popup is raised above context hints, fitted to the actual viewport,
and recomputed after search or layout changes. Filter choices use28-unit rows,
12-unit text and2-unit spacing. Popup height follows content and available space,
retaining scrolling. Existing filter/search/selection/hierarchy behavior is retained.

The old implementation reproduced `ToolsMenu is clipped by ViewportInput`.
Normal/QA builds:0warnings/0errors. HostContract25185596, localization933,
compiled isolation and launch preflight passed. Unity6000.0.61f1 passed492
screenshots at2026-10-10T11:47:35.7184456Z: four resolutions1280×720—3440×1440
and100/120/140% UI scales, native clicks, post-Tick layer order, viewport bounds,
compact filter rows and existing filter/search/group checks. Extreme screenshots
were inspected. Independent correctness review PASSED; Ponytail Ship.

Receipts: `artifacts/checkpoint-0.19.131/`. Geometry/Store were unchanged and not
rerun for this UI checkpoint; previous results belong to .130. No game proof.

After a fresh verified QA deployment, use the exact fixture and Russian actions
in [the owner scenario](EDITOR_POPUP_FILTER_CHECKPOINT_2026-10-10_RU.md).
Retest only the affected popup placement and compactness; preserve accepted .117 cases.
