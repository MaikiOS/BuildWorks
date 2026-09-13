# BuildWorks 0.19.33 alpha

**English** | [Русский](ALPHA_0.19.33_RU.md)

Build and automated verification date: September 11, 2026.

## Main change

The editor now places an individual piece against the selected surface using its visible mesh geometry. This removes the previous behavior where a slope, roof, or furniture piece could be positioned halfway below the grid.

- the editor ray returns the exact normal of the selected triangle;
- Q/E aligns the selected source point of the piece with the cursor;
- the Q/E hint displays the point number and direction;
- furniture and decorations without vanilla snap points receive seven editor-only points;
- a new Array always starts in `PACK / NO GAP`, 2×1;
- `FIT LENGTH` remains a separate explicit mode;
- Apply Array remains a single Undo operation.

## Evidence

| Check | Result |
| --- | --- |
| Release build | PASS, 0 warnings / 0 errors |
| Geometry | PASS, 104 |
| Store | PASS |
| EditorBridge | PASS, 3 |
| WorldLayout | PASS, 10 |
| HostContract | PASS for Valheim Steam build 25185596 |
| Unity Workbench | PASS, 81/81 |
| Actual prefab gate | PASS: `woodwall`, `wood_beam_26`, `wood_roof`, `piece_chair` |

## Not yet accepted

The manual 0.19.33 smoke test in Valheim is not complete. Real placement of slopes, roofs, and furniture, snapping below the grid, and the fresh Array workflow cannot be considered accepted until that test is finished.

Multiplayer and the complete save/exit/reload matrix also remain open.

## Localization status

The 0.19.33 runtime UI is Russian-only. Valheim localization is currently used only for native game-piece names; BuildWorks-owned UI strings are still embedded in the runtime. English-first runtime localization and automated locale coverage are planned work, not a feature of this alpha.
