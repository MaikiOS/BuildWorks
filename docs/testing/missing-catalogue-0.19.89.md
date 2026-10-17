<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](../CURRENT_STATUS.md).

# Incomplete catalogue insertion: checkpoint 0.19.89

**English** | [Русский](missing-catalogue-0.19.89_RU.md)

Source .89, installed .82. Evidence: `artifacts/checkpoint-0.19.89/verification.json`.
This checkpoint does not establish native construction, world reload or multiplayer.

## Change and evidence

.88 retained imported missing records and allowed direct editor opening. Catalogue selection still treated
missing models as a blocking error, repeated by Controller insertion. Cards now show `[!]` and unknown prefab
identifiers while allowing selection/insertion of every record. File errors and interactive limits remain blocks.
Editor normalization works on a copy with known intrinsic scales; source and unknown bases remain intact.
World resolution stays strict.

Incomplete costs/stations are hidden with an explicit unknown-cost message. Complete costs reuse the existing
aggregator over fully resolved Pieces, independently of Hammer registration. An empty world registry does not
mean free construction. Prefab identifiers are not verified missing-mod names; source/type/local bounds and
one-to-many replacement remain open.

Actual Unity acceptance runs reviewed import, production catalogue diagnosis, native GraphicRaycaster/click,
Controller.Update/LateUpdate: two-record insertion → Undo → Redo → stop repeated insertion → native library
save → fresh disk library. It checks missing absolute scale `(.001,.002,.003)`, both records, unchanged source
and no errors. Fixture `missing.blueprint` is generated under the current Workbench output `c89-*` directory;
it is an inert fixture, not a game stand. Repeat with `scripts/Capture-UiWorkbench.ps1` using Unity6000.0.61f1.

The card is assembled from production diagnosis; Workbench does not execute native Session catalogue production.
Compiled HostContract pins that wiring and resolved-cost call. Adaptive panel text is inspected including its
inactive subtree; visibility/readability at all resolutions is not established. The252 screenshots belong to the
general UI series; missing-catalogue acceptance is a separate interaction check.

Release 0 warnings/errors, HostContract, Localization853/LocalizationTests, Store, EditorBridge, WorldLayout15,
deployment guards, format and Unity252 PASS; source hashes match. Geometry and external corpus353 are unchanged;
.88 evidence is reused, not reported as freshly run. Both independent read-only reviews PASS; verify is absent.
No deployment, Valheim launch, save mutation, reload, multiplayer or push occurred.

## Next dependency: prepared safe survival stand

Read-only native analysis establishes an early boundary: configured BepInEx GameObject..cctor entrypoint inserts
Chainloader before its first instruction; plugin Awake precedes PlatformInitializer.InitializePlatform. A prefix
can guard InitializeSaveDataStorage before native save enumeration. Both FileHelpers.CloudStorageSupported and
CloudStorageSupportedAndEnabled getters need process-only interception; Enabled alone leaves FileExistsCloud,
MountDepot and BeginFileWriteBatch paths. SteamCloud.InitializeAsync only invokes its callback; permanent Steam
cloud preferences need not change.

This does not prove isolation from other plugins: an earlier plugin Awake may access saves. Launch/plugin order
and refusal after initialization or populated caches must be proven; never clear caches or redirect a late process.
No preparation code is implemented.

OstrixMemory note:06d63fa7c627289c records historical TerrainRamp_Lab/SurvivalQA and Test. Live config/source
TerrainRamp.TestBootstrap confirms Local World/Profile creation APIs, but current exact world UID is unproven:
only backup FWL files for these worlds were found locally. They are not verified resource-grant targets. Existing
worlds/characters remain untouched. Full import/editor/build/reload remains the first priority.
