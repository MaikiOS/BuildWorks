# External blueprint libraries — verified test build 0.19.56

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

## Approved construction transfer: source candidate 0.19.56

Ostrix approved game-piece constructions, editable in our editor and placeable in
the world. Terrain is excluded. One shared adapter reads `.blueprint`, both `.vbuild`
dialects and ZIP without extraction from `BepInEx/config/BuildWorks/imports` at the
first library browse in a session. Later additions require a session restart;
recursive directory discovery is not included.

External source cards are separated by the External blueprints source filter.
One editor-card click starts insertion of the whole construction. Edit opens a
detached draft; save confirms name/category and creates a native copy with new IDs.
Hammer's Place copy saves a native copy first, then uses existing per-piece resource
and station checks. Originals remain untouched. Thumbnails use resolved game pieces,
one per frame, never package images.

Bounds: 2–128 pieces, finite positions within ±512, scale 0.01–4, exact prefab IDs,
no substitutions. Nonempty info/ZDO, probabilistic pieces, unknown sections or invalid
transforms block the entire source with a reason. Sign text and object contents are
not supported. Creator/description remain in the original file, not the native v9 copy.
Native limits remain 32 blueprints and 12 categories; browsing external files uses no slots.

Release, Store, EditorBridge, WorldLayout, DeploymentChecks, HostContract and localization
pass; independent source correctness review PASS. Actual archive: 353 cards, 62
format-compatible, 291 blocked. CoffeeTable accepts; WoodCouch1 blocks on sign data.
Host prefab availability is a separate game check. After Hub/licence activation,
UI acceptance passes 144 states including open/edit/save/insert and thumbnail updates
without resetting the picker. New fixture local-name collisions CS0128/CS0841 fixed.
Log: `artifacts/unity-workbench-capture.log`; status:
`artifacts/ui-runtime/verification-status.json` = passed_runtime_workbench,
gameHostTested=false. .56 and inert source ZIP deployed only to TerrainRamp-1.0-Test;
both DLL/archive SHA256 verified. Default, worlds and saves remain unchanged.

Fresh diagnosis of all 353 sources: displayed reason is piece limit for 104,
#SnapPoints for 124, object data for 63; 62 accepted. Reasons overlap: 253 exceed
128 pieces, maximum 39,487. Of 100 within size bounds, 28 block on #SnapPoints and
10 on object data. Terrain alone does not block. The 128 limit exists in reader,
Store and BlueprintEditorDocument; raising it needs coordinated changes and large
construction tests, not removing a single guard. SnapPoints/object-data support
and larger blueprints are not implemented yet.

Public `.buildworks.json`, lossless interchange, export and missing-piece substitution
remain separate BW-04/BW-05 work. Store v9 is not a public interchange format.
No foreign DLL, model or texture extraction; no automatic dependency installation.
