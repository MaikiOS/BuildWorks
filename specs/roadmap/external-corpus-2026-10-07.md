# Additional external corpus and format boundaries — 2026-10-07

**English** | [Русский](external-corpus-2026-10-07_RU.md)

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
