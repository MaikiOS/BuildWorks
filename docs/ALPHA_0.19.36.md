# BuildWorks 0.19.36 alpha source status

**English** | [Русский](ALPHA_0.19.36_RU.md)

English localization from 0.19.35 is accepted in-game. A separate slope/railing check showed that the Blueprint Editor reset every new part to AUTO instead of carrying the active vanilla Q/E snap-point index.

## Fix

- single-part editor placement inherits `Player.m_manualSnapPoint` through the existing host adapter;
- E continues to the next source snap point without rotating the part;
- composite-blueprint placement remains in AUTO because a single-piece index does not identify a composite anchor;
- the collider approximation explored during diagnosis was removed because exact prefab tests showed no useful placement change.

## Automated evidence

- Release build: 0 warnings, 0 errors;
- localization catalogs and runtime tokens: 596/596, no collisions;
- English/Russian runtime registration: PASS;
- Geometry 104, Store, EditorBridge 3, WorldLayout 10, HostContract: PASS;
- Unity Workbench: 81/81 PASS;
- installed DLL hashes match the verified artifacts in `TerrainRamp-1.0-Test`.

In-game slope/railing parity and Russian localization remain manual gates. No commit, push, pull request, binary package, or public release was created.
