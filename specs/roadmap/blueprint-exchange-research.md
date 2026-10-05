# External blueprint libraries — verified research, not implemented import

2026-10-05. Ostrix asked us to find blueprint-capable mods and their ready-building libraries.
No other gameplay mod was installed; no game/world/store was modified.

## Selected library and local evidence

[BiomeBlueprints 1.0.3](https://thunderstore.io/c/valheim/p/OverDrive/BiomeBlueprints/)
is a PlanBuild catalogue. Downloaded package only to
`artifacts/external-blueprints/BiomeBlueprints-1.0.3/package.zip`.
SHA256: `36C5A04DF80294C6B49B21C916D808FCE60698B17479BFAB7C95F850BE12C707`.

`scripts/Inspect-BlueprintLibrary.ps1` reads ZIP entries without extraction or execution.
Its self-test passes; `inventory.json` records names, creators, categories, piece
counts, prefab identifiers, terrain marker counts and extended-row counts.
This is inventory, not a transform parser, dependency validator or importer.

- 353 `.blueprint` files; 100 meet the current 2–128-part size gate only.
- All 100 small files contain terrain markers; 27 also contain extended piece fields.
- Actual maximum is 39,487 parts, not the conflicting 16,568 maximum in one paragraph
  of the package description. The BuildWorks limit remains 128.
- Small examples: CoffeeTable (Madzobuild, 4 parts), Couch1 (Madzobuild, 10),
  Plains Hut (Biome Blueprints, 28). These are candidates, not proven-compatible imports.
- Package DLL, thumbnails and foreign code/assets were not installed, executed,
  copied into BuildWorks, or included in the source repository.

Credits are in the source files/package page. No licence granting redistribution
of this collection was identified there. Keep the external archive local; do not
bundle it into BuildWorks without resolving author permissions.

## Mechanics and documented formats

[PlanBuild](https://github.com/sirskunkalot/PlanBuild) uses `.blueprint` and accepts
BuildShare `.vbuild`. [Infinity Hammer](https://github.com/JereKuusela/valheim-infinity_hammer/blob/main/blueprints.md)
also supports prefab-based blueprints. The primary
[Expand World Data format documentation](https://github.com/JereKuusela/valheim-expand_world_data/blob/main/docs/blueprints.md)
describes names, positions, quaternion rotations, scale and optional object data.
[Valheimians builds](https://www.valheimians.com/builds/) is a second source,
but dependencies and permissions must be checked per author/file.

These are constructions made of game prefab references, not FBX/OBJ/glTF meshes.
Installed mod pieces can be resolved by exact prefab ID without copying their assets.
Do not guess mod ownership from a name or automatically install dependencies.

## Next bounded stage — pending scope decision

First support original versioned `.buildworks.json` exchange and read-only
preflight for PlanBuild `.blueprint`; `.vbuild` can follow. Put the same Import/Export
actions in blueprint catalog and Hammer library, not two independent implementations.
Parse into a staging document; validate byte/count limits, finite transforms, scale
range, hierarchy/IDs and exact prefab resolution before an atomic library write.
Preserve creator/source URL and original file. Missing prefabs must be visible and
must block placement, not disappear or silently be substituted.

Owner question is outstanding: first import parts only, never applying terrain and
explicitly rejecting unsupported object-data files, versus full terrain/object-data
support. Terrain is a material expansion of world-edit authority, not a parser detail.
The current Store v9 library file is internal persistence, not a public import format.
No import/export button or library ingestion is claimed as implemented in 0.19.55.
