<!-- docs-role: design record; reviewed 2026-10-17 -->
> Design/specification record: requirements below are not blanket implementation or acceptance claims. [Current state and queue](../../docs/CURRENT_STATUS.md).

# Additional external corpus and format boundaries — 2026-10-07

**English** | [Русский](external-corpus-2026-10-07_RU.md)

## Real-file pipeline and pinned dialect evidence

[Verified segment](../../docs/testing/external-path-2026-10-07.md): BiomeBlueprints1.0.3 CoffeeTable4/
Forest Hut28 pass actual reviewed import, editor/edit/free support, disk reopen, our export/reimport and
renderer-only world planning with installed native prefabs. Construction/world reload/network untested.
Original attribution/hash provenance is local; no redistribution license established.

Pinned [InfinityHammer ce2737f](https://github.com/JereKuusela/valheim-infinity_hammer/blob/ce2737f0156df44aa7d193b5e1aed6167f3d794b/InfinityHammer/Blueprint.cs#L31)
and [EWD b036810](https://github.com/JereKuusela/valheim-expand_world_data/blob/b036810d4708949b9b1886291aa08c61ca269729/ExpandWorldData/data/Blueprints.cs#L205)
disagree: IH empty Center includes origin in X/Z extrema, but uses minimum piece Y; EWD adds0.05Y.
IH retains a nonempty marker row; EWD excludes only the first matching row through Chance0.
Absent versus empty Center differ through configured defaults. IH
[writer](https://github.com/JereKuusela/valheim-infinity_hammer/blob/ce2737f0156df44aa7d193b5e1aed6167f3d794b/InfinityHammer/commands/blueprint/HammerSave.cs#L127)
uses YXZ Euler, while the pinned
[reader](https://github.com/JereKuusela/valheim-infinity_hammer/blob/ce2737f0156df44aa7d193b5e1aed6167f3d794b/InfinityHammer/commands/blueprint/HammerBlueprint.cs#L143)
uses VectorXZY. Nonzero Rotation needs explicit dialect evidence. Coordinates XZY restore original world
location, not ordinary relocation. Syntax alone does not identify consumer; even74 zero-rotation/empty-center
files cannot safely receive a shared whitelist. Explicit dialect/provenance and coordinated source-row binding
are required. No production reader change in this test-only checkpoint.

## Local test data, never installed or redistributed

[DarkTrooper/2v2t5.0.0](https://thunderstore.io/c/valheim/p/DarkTrooper/2v2t/v/5.0.0/):
official download `https://thunderstore.io/package/download/DarkTrooper/2v2t/5.0.0/`.
Saved locally as artifacts/external-blueprints/2v2t-5.0.0/package.zip,12847060 bytes,
SHA256 `4A6E6A53F246B5CF4C7EA9472F726D9F35687BCE978F28AB47A020AEE759DD88`.
ZIP has777 entries:140.blueprint,69.vbuild,138.png and other package/config data.
Only bounded text readers were used; no extraction, mod installation or game execution.
Package manifest dependencies describe the pack, not proven dependencies of each construction.
README credits original authors, including builds collected from servers/Nexus/Thunderstore.
No redistribution licence was found; retain original package/#Creator, do not bundle it in Git/releases.
Source metadata/hashes/counts are retained in provenance.json beside the archive.

The existing inventory script produced inventory-blueprint.json for140 files. Its2–128 gate is
historical and is not current support. The actual current LoadFile parser, reflected from the
StoreTests assembly containing linked production source, returned209 cards:156 parsed,53 refused.
parser-report.json retains exact source entry/count/error. This did NOT resolve host prefabs/scales,
convert all foreign state, render, place, reload or test networking. Never label156 as game-compatible.
The first reflection invocation failed because a PowerShell PSObject was passed as System.String;
the unchanged reader succeeded with an explicit string argument.

[Dvergr Library](https://www.valheimians.com/build/dvergr-library-modded-comp-build/) by Con Seannery
explicitly offers a blueprint and lists required mods: Refined Stone Pieces, Finewood Furnitures,
Clutter, BuildIt, Gizmo and Plant Everything. Local download attempted, but Cloudflare returned
`Sorry, you have been blocked`; no fixture was obtained. Redistribution terms were not established.
[Valheimians terms](https://www.valheimians.com/terms_of_use/) do not grant unrestricted repackaging.

## Format inventory

| Format | Current reader/export | Limits and unimplemented behavior |
|---|---|---|
| Store14 / portable5 `.buildworks.json` | Native structure/groups/supports/snaps/sign/source/payload; export our JSON or ZIP | Strict unknown schema rejection; source archival is distinct from behavior;32MiB portable |
| PlanBuild `.blueprint` | Known geometry/scale/sign/snaps and typed survival conversions | Host resolution required; unknown state/header blocks world;2–50000 parts,±512 local coordinates |
| BuildShare `.vbuild` | Eight core fields, whitespace/semicolon; retain extended data | ZDO scale/scaleScalar ambiguous, remains blocked; no export writer |
| ZIP | Bounded read, no extraction, atomic native exchange/source retention |2000 supported documents;128MiB read budget; not a gameplay capacity proof |
| [KGvalheim Blueprint](https://thunderstore.io/c/valheim/p/KGvalheim/Blueprint/) `.yml`/`.oprint` | Unsupported | Source link unavailable; no verified spec or promised adapter |
| OBJ/FBX/glTF / terrain | Out of scope | No arbitrary model or terrain adapter |

[Buildheim](https://github.com/AugusDogus/Buildheim) is another primary implementation of known
`.blueprint/.vbuild`, not evidence of a new format or permission to copy its code.

## Header research: no blanket whitelist

BiomeBlueprints353-file archive has89 files with #Center/#Coordinates/#Rotation:81 empty Center,
8 nonempty;80 zero Rotation,9 nonzero. Four named markers are absent; one church contains86
matching ashwood_beam_1m rows. Deleting all matching prefab names would destroy real construction.

Primary sources: [InfinityHammer Blueprint.Center](https://github.com/JereKuusela/valheim-infinity_hammer/blob/main/InfinityHammer/Blueprint.cs),
[reader](https://github.com/JereKuusela/valheim-infinity_hammer/blob/main/InfinityHammer/commands/blueprint/HammerBlueprint.cs),
[writer](https://github.com/JereKuusela/valheim-infinity_hammer/blob/main/InfinityHammer/commands/blueprint/HammerSave.cs),
[selection](https://github.com/JereKuusela/valheim-infinity_hammer/blob/main/InfinityHammer/selection/ObjectSelection.cs),
[EWD spec](https://github.com/JereKuusela/valheim-expand_world_data/blob/main/docs/blueprints.md).
Center chooses a matching frame or lower positional bounds centre and rebases pieces/snaps.
Coordinates belongs to original-location restore, not ordinary relocation. Writer uses XZY coordinates
and YXZ Euler rotation; inspected loader versions disagree on Euler order. EWD documents excluding
the marker; inspected InfinityHammer selection instantiates all rows. Cached versions may differ.
Do not infer a dialect merely from these headers or claim original-location restore.

Next adapter must distinguish verified semantics, preserve exact source, use existing placement
support/frame paths and keep malformed/duplicate/unknown headers blocked. Change both the parser
and BlueprintSourceDocument row indexing. Tests: first of repeated marker prefab, absent marker,
nonidentity marker, snaps, BOM/CRLF, inline headers, unknown/malformed sections, native/portable roundtrip.
No header adapter was implemented in .86.
