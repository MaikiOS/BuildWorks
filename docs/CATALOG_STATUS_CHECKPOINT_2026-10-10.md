# .139 — catalogue availability checks

Code a4e66c95ef294a7f1c120eacd7712a79b4b3ed33. Installed only in TerrainRamp-1.0-Test;
both SHA verified, Default clean,137 existing config/import/save files retain
bytes/size/exact UTC ticks. No Valheim launch. See QA_0.19.139_RU.md.

EditorCatalogStatus called the Session prefab resolver for each part before
Distinct. Two invocation-local ordinal HashSets now track checked names and
warnings per blueprint card. No cross-card or persistent cache; subsequent
passes resolve afresh. Absolute-scale rows still warn, including mixed rows.
Construction, object state, costs and permissions are unchanged.

Actual Controller regression:50 000 rows, three relative names including a
case-distinct spelling, repeated/mixed absolute rows and an absolute-only name.
Baseline30 000 callbacks; final3. Sorted warnings and a fresh availability pass
passed. Single fake-callback samples4.326/2.999ms are not real Valheim timing.

Opt-in StoreTests --catalog-phases uses temporary files and the actual CatalogAll
snapshot/worker/publication, unit-scale/no-material callbacks, no Unity/UI/images.
364 docs/610822parts/454names: read1819.411/1761.220ms; independent Distinct probe
27.180/23.740ms; synchronous scheduling28.517/23.611ms; remaining worker wait
913.328/904.363ms; publication0.409/0.113ms. Probe is not additive; OS/JIT caches
were not flushed. Fake callback total≈0.02ms is not host resolver cost.
Assertions pin callback thread/count, worker-result publication, source order,
part counts, cached result and unchanged input files. An initial wrong raw-ID
assertion was corrected: host preparation intentionally derives IDs from basis.

Normal/QA0/0, full Store, Host25185596, localization948, isolation/preflight,
Unity492/controller/exactAsset/nativeInteraction and both reviews PASSED/Ship.
gameHostTested=false; verify absent; dedicated Geometry tests not rerun because
math is unchanged. Sandbox Unity -536870911/Store access denial logs retained;
identical runs outside sandbox reproduced the regression then passed.

SocratiCode Default collection codebase_fbb51d0bb614 green26437/watcher active;
empty symbol/impact results were supplemented by actual rg callers, not reindexing.
MemPalace's historical Store-v9 note was not used as current native22 evidence.
Artifacts: catalog-phases-0.19.138 and checkpoint-0.19.139. Full import latency,
native construction/reload/network and physical cursor backend remain unproven.
