<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# Whole-corpus file pipeline checks

2026-10-12: tests/docs only; runtime code and installed QA .163 unchanged.
[Detailed Russian evidence](CORPUS_ROUNDTRIP_2026-10-12_RU.md).

| Corpus | Files | Parsed | Unit-base converted | Native reopen / portable / reimport |
| --- | ---: | ---: | ---: | ---: |
| BiomeBlueprints1.0.3 | 353 | 353 | 353 | 353 |
| 2v2t5.0.0 | 209 | 202 | 202 | 202 |

StoreTests `--roundtrip-corpus <archive>` pins SHA/counts, bounds uncompressed
reads and uses actual Parse/PrepareForHost/TryGetOrCreatePlacementCopy. All
serialized part fields survive copy creation (only new stableIds normalized,
then restored in finally). Source text/counts survive; portable bytes match
native reopen and Decode→Encode. Portable reimport and final archive SHA checked.
Unique temporary libraries are cleaned up; archives/third-party blueprints are
not distributed. No unknown state is executed by this check.

Seven current 2v2t refusals remain at exact source rows: Dragonsreach_vanilla
and _2 line8616 stonewall_hardrock_2x1 Y2514.233; PortalPlatform316 guard_stone
Y2500.8; TheDecay DoorCapper2/Full lines8/6 LocationProxy Y-4936.202/-4998.202;
Tyr Dragonsreach_v3/Tyr Lawgivr_Dragonsreach_v3 line9434 MarketPlaceNPC Y557.3793.
These exceed the existing +/-512 position bound. No authoritative origin rule
was found; no arbitrary offset, deletion or safety relaxation performed.
This is an importer limit, not proof of damaged files or absent mods.

Artifacts: artifacts/corpus-roundtrip-2026-10-12/{build-final.log,biome-final.log,
2v2t-final.log,refusals.json,verification.json}. Build0/0 and both final passes,
independent correctness PASSED/Ponytail Ship. Initial sandbox temp-write access
denial remains in biome.log; successful outside runs are recorded separately.
`verify` unavailable. Runtime builds not repeated for test/docs-only changes.

Inert unit-prefab evidence only: no actual prefab/base-scale availability,
editor/ghost/cost/placement/world reload/network acceptance. Retained unknown
data is not understood/restored behavior. Next authorized native series remains
.163 TEST24 persistence/snap then supported external buildings; filled storage,
authority, persistent projects/v11 and network remain open.
