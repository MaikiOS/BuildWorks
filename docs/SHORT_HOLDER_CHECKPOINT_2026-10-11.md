<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

**Current2026-10-11:** QA .157 installed after normal game exit; both DLLs/155 existing config/import/save files verified, Default clean. No agent game launch or new native acceptance. [Deployment and next steps](QA_DEPLOY_2026-10-11.md). Earlier source/native observations below remain historical.

# .157 — short PlanBuild holder recipes

Source `3287e01`,2026-10-11. **Source/CPU verified; not installed.** Installed QA
remains .146 by SHA, Valheim process27108 is still open. No game commands were sent
after the .146 reply failure. The .156 Respond fix is included, with native
acceptance pending. [Failure and safe continuation](QA_RESPOND_CHECKPOINT_2026-10-11.md).

Only `.blueprint` itemstand/itemstandh Info without ZDO/chance is extended:
name:variant → name:variant:1:0; name:variant:quality → name:variant:quality:0.
These are the pinned PlanBuild reader defaults. The existing strict four-field
validator still checks every result. Unknown tails, invalid/overflow/negative
integers, quality0 and other dialects remain inert. Info+ZDO, chance, ArmorStand,
vbuild and the .155 source-bound four-field numeric-item adapter are unchanged.

This is an item recipe, not a full durability/custom-data snapshot. Exact source
documents survive disk/portable copies and clearing effective contents. Native22,
portable11, costs, server authority and placement behavior are unchanged.

Full Store red/green/final cases cover both holders, quoted/plain2/3/4 fields,
source/disk/portable/clear-policy and invalid guards. Biome353/353 parses;609 short
Info records in16 documents (106 two-field,503 three-field) now become recognized
recipes. Retained foreign parts27905→27296; documents with Unknown234→232.
The other14 affected documents still have Unknown barriers. This census uses unit
prefab scales and no host material support, not native prefab/placement proof.
The second corpus remains202/209 with7 explicit coordinate refusals.

Normal/QA build0 warnings/errors, Host Steam25185596, localization989 and compiled
QA isolation passed. Plugin formatting passed; changed test lines have0 diagnostics.
141 whitespace diagnostics remain on unchanged baseline Program.cs lines; whole-file
format is not claimed clean. Independent correctness and Ponytail reviews PASSED/Ship.
Geometry/EditorBridge/QA input protocol were not rerun: these paths are unchanged.
Installed146, both source archives and clean Default were rechecked unchanged.

Pinned source: artifacts/planbuild-reader-0.19.140/PlacementComponent.cs304–322,
commit e12ed86ff2498bbd4a9c0f5d81cb388e3aaeb549,
SHA2562FF4FB7F122EFA9F0F05ADBE419AAD3E0BDB8E69246D74BE969CAF0215863ED7.
Receipts: artifacts/checkpoint-0.19.157/verification.json, short-info-census.json,
format-baseline-comparison.json; conversion-remaining-0.19.157/remaining-state.json.

Initial errors are retained: sandbox temp access denied before new tests;
second archive passed to the Biome-only mode (Downloaded library coverage), then
corrected --large-snap-corpus passed; changed-method whitespace fixed while baseline
format retained. An accidentally created PluginVersion.cs was verified/deleted;
the real version is the existing BuildWorksPlugin.cs constant.

Next: normal game exit, guarded QA≥.157 deploy, TerrainRamp-1.0-Test Modded, same
closed QA world, matching state/folder/close terminal receipts, then existing24/25
native series. No .157 native construction/resources/reload/network acceptance yet.
Legacy Inventory106 and other unknown formats remain unsupported; full goal active.
