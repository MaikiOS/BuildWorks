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
- [x] English-first runtime localization and complete Russian catalog;
- [x] contributor architecture, localization, and verification documentation;
- [x] accept English localization in-game on 0.19.35;
- [x] accept the reported slanted-piece Q/E positioning in-game and keep the active editor snap mode visible;
- [ ] verify Russian localization, 2 m nearby-target preview, unchanged 0.55 m magnetic threshold, then continue the pending roof/furniture, Q/E-below-grid, and fresh-Array scenarios;
- [ ] complete save/exit/reload gate;
- [ ] host + remote-client multiplayer gate;
- [ ] expanded vanilla and modded prefab matrix.

## Required foundation: runtime localization

- [x] replace current BuildWorks-owned display strings with stable language keys;
- [x] make English the fallback and provide a complete Russian catalog;
- [x] follow the current Valheim language automatically;
- [x] keep internal category/material/source identifiers language-neutral;
- [x] localize current hints, errors, tool parameters, catalog, outliner, and HUD;
- [x] check catalog parity, key syntax, formatting arguments, static usage, embedded resources, and source literals;
- [x] verify English in Valheim;
- [ ] verify Russian in Valheim; automated registration already passes.

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
