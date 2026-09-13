# BuildWorks 0.19.33 alpha

**English** | [Русский](https://github.com/MaikiOS/BuildWorks/blob/main/release-notes-0.19.33_RU.md)

Pre-release test build for Valheim 1.0.

## Changes

- precise visible-mesh contact for individual slopes, roofs, and furniture in the editor;
- Q/E aligns the selected source snap point with the cursor and displays its direction;
- seven editor-only snap points for furniture and decorations without vanilla snap points;
- a new Array starts in `PACK / NO GAP`, 2×1;
- fixed Array profile persistence on Apply with a single atomic Undo step.

## Verification

Release 0/0, Geometry 104, Store, EditorBridge 3, WorldLayout 10, HostContract, and Unity Workbench 81/81 — PASS. Actual prefab gate: `woodwall`, `wood_beam_26`, `wood_roof`, `piece_chair`.

## Known limitations

- the manual in-game smoke test for 0.19.33 is not complete;
- the BuildWorks UI is currently Russian-only; a real English-first localization layer is planned and is not claimed by this release;
- this is a pre-release alpha, not a stable release;
- multiplayer, complete save/reload validation, import/export, and procedural curves have not passed their final gates.

Installation and complete documentation are available in the repository.
