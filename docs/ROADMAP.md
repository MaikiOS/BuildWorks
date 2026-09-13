# BuildWorks roadmap

**English** | [Русский](ROADMAP_RU.md)

This is a public direction summary. A feature status describes availability and verification level, not a promised delivery date.

## Current: stabilize alpha 0.19.x

- [x] precise placement of one native piece;
- [x] XYZ gizmo, three-axis rotation, and numeric input;
- [x] uniform scaling from 1–400%;
- [x] indexed hammer with search, categories, materials, recents, and favorites;
- [x] composite blueprint library;
- [x] dedicated Blueprint Editor;
- [x] object tree with nested groups;
- [x] local group anchors and a separate world blueprint anchor;
- [x] editor/world Array and Contour;
- [x] Undo/Redo for primary operations;
- [x] automated Unity Workbench and host-contract checks;
- [ ] manual 0.19.33 smoke: slope/roof/furniture surface contact, Q/E below the grid, fresh Array;
- [ ] complete save/exit/reload gate;
- [ ] host + remote-client multiplayer gate;
- [ ] expanded vanilla and modded prefab matrix.

## Required foundation: runtime localization

- [ ] replace BuildWorks-owned display strings with stable language keys;
- [ ] make English the default runtime language and provide a complete Russian catalog;
- [ ] follow the current Valheim language automatically, with an explicit BepInEx override if needed;
- [ ] keep internal category/material identifiers language-neutral;
- [ ] localize contextual hints, errors, tool parameters, catalog, outliner, HUD, and adaptive-piece metadata;
- [ ] add missing-key, fallback, formatting-argument, and English/Russian coverage checks;
- [ ] verify both locales in the Unity Workbench and then in Valheim.

## Next product stage: persistent operations

- [ ] Store v10 with optional recipes;
- [ ] non-destructive group operation stack;
- [ ] persistent Array with stable copy IDs;
- [ ] Line guide;
- [ ] move the current Contour onto the shared evaluator;
- [ ] bake to regular pieces as one Undo operation;
- [ ] Align/Target after the base model is proven.

## Curves and guides

- [ ] Polyline guide;
- [ ] Arc guide;
- [ ] Bezier guide with stable tangent/normal frames;
- [ ] repeat rigid pieces without deformation;
- [ ] separate original adaptive pieces with shared preview/commit meshes;
- [ ] validate UVs, colliders, snap points, and multiplayer reconstruction.

## Exchange and compatibility

- [ ] safe blueprint import/export;
- [ ] prefab/material/source dependency manifest;
- [ ] lossless preservation of unknown data;
- [ ] understandable recovery when a required mod is missing;
- [ ] explicit EarthWorks/TerrainRamp compatibility contract without hidden terrain changes.

## Later

- [ ] compact redesign of the world F9 HUD;
- [ ] additional MoGraph-like operations after the Array/Guide foundation;
- [ ] original materials and a production-ready adaptive building set;
- [ ] mod-manager packaging.

Not in the near-term scope: node graph, expression language, arbitrary external references, deformation of third-party prefabs, full Fields/dynamics, or automatic installation of third-party mods.
