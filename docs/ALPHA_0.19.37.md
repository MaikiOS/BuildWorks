# BuildWorks 0.19.37 alpha source status

**English** | [Русский](ALPHA_0.19.37_RU.md)

The reported slanted-piece placement is correct after cycling Q/E. This alpha closes the remaining feedback gap in the Blueprint Editor.

## Change

- the bottom hint always shows the current source snap mode during part placement;
- Q/E refreshes that hint immediately;
- target points on editor pieces become visible within 2 m of the active source point;
- the existing 0.55 m magnetic snap threshold is unchanged;
- existing candidate rendering and the placement scan are reused, with no separate scanner or snap algorithm.

## Automated evidence

- Release build: 0 warnings, 0 errors;
- localization catalogs and runtime tokens: 596/596, no collisions;
- English/Russian runtime registration: PASS;
- Geometry 104, Store, EditorBridge 3, WorldLayout 10, HostContract: PASS;
- Unity Workbench: 81/81 PASS;
- installed DLL hashes match the verified artifacts in `TerrainRamp-1.0-Test`.

In-game acceptance of the target preview and Russian localization remains pending. The verified binary pair is packaged as `BuildWorks-0.19.37-alpha.zip` for the GitHub prerelease.
