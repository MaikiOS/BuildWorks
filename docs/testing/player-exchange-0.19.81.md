<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](../CURRENT_STATUS.md).

# Prepared player checks — 0.19.81

[Detailed Russian steps](player-exchange-0.19.81_RU.md) are the owner-facing test instructions.
This is an intermediate build, not complete foreign blueprint support.

Installed test files: TerrainRamp-1.0-Test/BepInEx/config/BuildWorks/test-fixtures.
In-game navigation: Hammer → Blueprints → Import file → BuildWorks folder → test-fixtures.
No resource gathering, recipe progression, placed fixtures or world modifications are needed.
Recipe availability is still checked. If a recipe is unavailable, copy the diagnostic report
and skip that example; do not unlock it just for this check. A universal test character is not prepared.

1. Open that folder. The long path must remain single-line; Home/End reveal either end.
   Close is at the upper-right. Repeat in the editor's import dialog.
2. Import 01-walls.blueprint, confirm, close the result, find TEST 01 Walls and Edit.
   Expect two walls. Do not place them.
3. Export TEST 01 as QA81-walls-json in BuildWorks JSON, then import that file from
   BuildWorks folder → exports and Edit. Expect two unchanged walls. Repeat as QA81-walls-zip
   using ZIP. Reusing the same output name must refuse overwrite.
4. Import 02-sign.blueprint and Edit TEST 02 Sign. Expect the exact text
   “BuildWorks: Привет, Ostrix!”. Export/reimport and inspect the same text.
5. Import 03-opaque.blueprint and 04-snap.blueprint separately. Both should Edit as two walls.
   Check blueprint should report Editor: OK but a blocked World reason. World execution is
   deliberately not enabled by this editor-opening fix.
6. Import 05-missing.blueprint. Expect rejection naming buildworks_test_missing_prefab;
   no partially imported single wall. This file is intentionally invalid for the host.
7. Import 06-lamp.blueprint and Check blueprint → Copy report. Send the report and,
   if Edit is available, a wall/lamp screenshot. Also report the original problematic blueprint.
   Do not unlock a recipe just for this check. Lamp root cause is not yet established.
8. For any unavailable blueprint, use Check blueprint → Copy report. Above512 remains
   blocked pending profiling/implementation; a thumbnail is not a placement readiness guarantee.

Not ready: dedicated survival test world/character and prepared physical contents fixtures,
placement-time storage/mount choices, foreign-state execution, heavy interactive scenes,
durable world sockets, armour/free items and editable external thumbnails.
Do not ask the owner to grind or manually build that test setup. Prepare separate preview
and survival-cost test modes; preserve production costs/access/ownership.

Current automation is isolated Unity plus code/format checks. The exposed UI-control tool
does not control the native Valheim window; no autonomous in-game validation was performed.
