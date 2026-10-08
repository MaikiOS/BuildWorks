# QA save isolation: checkpoints 0.19.90–0.19.93

**English** | [Русский](qa-save-isolation-0.19.90_RU.md)

Verified source preparation, **not a ready survival stand or game acceptance**. Installed .82; current .93 is not
deployed. No Valheim launch or game-save mutation. With explicit owner permission only TerrainRamp-1.0-Test's
TerrainRamp bootstrap `[Bootstrap] CreateMissingSaves=true` changed to false; all other text/BOM/newlines preserved.
Local backup/hash proof: `artifacts/checkpoint-0.19.90/bootstrap-config-change.json` and `bootstrap-config-before.cfg`.

## Physical stations and guarded package: .93

[Current source contract/manual cases](qa-stand-0.19.93.md): scaled combined bounds, loaded flat sites,
terrain-component refusal, one-shot native station identity/knowledge and committed world-save counter.
Explicit QaStandToken deploy stages QA pair and local pinned files, marker last. QA64BCL/deployment12replica/
forbidden4 PASS; native game/reload unexecuted. Older sections below describe their individual checkpoints.

## One-shot supplies: .92

QA-only BuildWorksTestSupplies runs in the actual coroutine after loaded gate. Pins owned01/02/03/04/06 and
external CoffeeTable/ForestHut hashes, uses actual reviewed importer. Sources unchanged; external files local
only, [provenance/terms](external-path-2026-10-07.md), only names/hashes enter Git. External QA config staging
is absent: missing files refuse before grants. Actual native Piece resources/ResourceCosts plan one copy of
each fixture. Missing/free-key/invalid requirements refuse; total cost<=10000 before existing accumulator.
Canonical ObjectDB IDs, Hammer1 before materials/AddKnownItem for recipe updates. Station knowledge/physical
stations remain absent. Conservative empty slots/weight must fit, otherwise refuse pending an unimplemented chest.
Different prefab IDs sharing a name refuse; batches<=maxStack and actual prefab/count delta must match.
False/partial never retries. Loaded gate before each batch. IDs/source hashes/actual plan bind receipt;
CreateNew+Flush(true) precedes grants, profile.SavePlayerData+Save(bool result) precedes complete.
Complete never reissues after consumption; interrupted/foreign/changed receipts require inspection, no auto retry.
25 identity+11 one-shot+13 stack BCL cases execute; native call/order/coroutine wiring is compiled proof only.
Native grants/knowledge/save unexecuted; **not a ready stand**. Proof: artifacts/checkpoint-0.19.92/verification.json.
Older UI/corpus tests not rerun.

Next: physical stations without terraform and native world save. Player.PlacePiece neither validates nor consumes
cost: QA fixture preparation only, never ordinary construction proof. Verify loaded terrain/footprint/obstacles/
absence of terrain components, then fresh owner ZDO/creator/range after Start. WorldSaveFinished/SaveDoneTime
are not success acknowledgements. After ZNet.Save(false,false,false), wait for !IsSaving and committed
GetSaveNumber()==before+1, recheck identity/root. EndSave(writeOK:true) commits the number; failure does not.
Reload remains a separate test.

## Loaded-session gate: .91

After save preparation the coroutine waits for a local player and teleport completion, then calls
RequireLoadedSession. This is a one-time first-entry check, not continuous connection blocking or a capability
to grant later. Every future mutating operation must call the guard again immediately before proceeding.
Refusal only logs an error; no test items, stations or materials are created yet.

The guard rechecks root/cloud/owner, initialized storage, current Local world/profile, existing save paths inside
the root, and exact world name/profile filename and signed64 world UID/profile ID/local player ID against the
original complete receipt. Player.GetPlayerID reads the live ZDO ID; uninitialized zero is rejected.
Receipt validation is shared with reopening previously prepared saves.

Requires living player, inventory, ZoneSystem, !shutdown, server, !dedicated, !open server and zero GetPeers.
Native GetPeers includes accepted connections before handshake; counting only ready peers is weaker.
Debug, NoCostCheat, nonzero world level and global NoBuildCost/NoCraftCost refuse. These do not prove the
survival settings of future individual pieces: their plan must also check Piece.FreeBuildKey().

Test-QASaveIsolation executes **25** BCL cases: valid large signed64 IDs, each corrupt field, partial/extended/null
receipt, zero/overflowing IDs, foreign/malformed token, every loaded name/ID mismatch and player ID=0.
Native guard/coroutine checks prove compiled wiring only. Native methods, save creation, loaded lifecycle and
network transitions have not executed. Proof: `artifacts/checkpoint-0.19.91/verification.json`.
Older Unity/corpus checks were not rerun.

## Implementation boundary

Normal builds contain no BuildWorksTestIsolation type. Explicit BuildWorksTestStand=true produces a separate
artifacts/test-stand-bin QA binary; ordinary Deploy-TestBuild does not install it. No new mod/dependency.
Activation needs an explicitly prepared GUID token in the mod's config; token provisioning/deployment are absent.

The fixed root is `TerrainRamp-1.0-Test/BepInEx/config/BuildWorks/qa-saves/<GUID>`. Before writing, activation
requires cold native save initialization/collections/world caches, no saving/active world/player/Game or populated
menu selection. Late activation refuses without cache clearing. Reparse paths and foreign/nonempty unowned roots
are rejected. Native DLL fingerprints require renewed analysis after a host update.

Process-only Harmony prefixes force both cloud capabilities false, guard native storage startup and pin
Utils.SetSaveDataPath/ResetSaveDataPath until process exit. Prefix registration is verified; the isolation patch owner
survives normal BuildWorks UnpatchSelf. Permanent Steam cloud preferences remain untouched.

After successful placement integration a coroutine waits up to60 seconds for native storage. New Local world/character
use `BW_QA_<8 token characters>` and fresh native IDs. A started receipt precedes the first save; incomplete attempts
never retry automatically. Sources/paths are checked, completion appended after success. Restart does not recreate
files. A completed receipt does not prove disk IDs after outside modification: loaded identity must be verified before
grants. World DB requires real world startup. Materials/items/recipes/stations/fixtures are not prepared; no grants
or survival bypass exists.

## Mandatory launch preflight

Installed TerrainRamp.TestBootstrap0.3.0 independently creates saves after1 second when CreateMissingSaves=true.
Activate refusal disables only BuildWorks, not the process or that coroutine. Test-QASaveIsolation.ps1
-CheckLaunchProfileOnly verifies the known installed DLL hash and explicit config false. Unknown/duplicate DLL or
missing config refuses launch. Initial live preflight refused; owner-approved config change made it PASS. This checks
the known writer, not all indirect behavior of all plugins. Repeat before any future launch.

## Reproduction without game

From BuildWorks in PowerShell7:

```powershell
$qaProfile = (Resolve-Path '../../TerrainRamp-1.0-Test').Path
dotnet build ./src/BuildWorks/BuildWorks.csproj -c Release -p:BuildWorksTestStand=true "-p:ProfileRoot=$qaProfile"
dotnet build ./src/BuildWorks/BuildWorks.csproj -c Release -p:BuildWorksTestStand=false "-p:ProfileRoot=$qaProfile"
./scripts/Test-QASaveIsolation.ps1
./scripts/Test-QASaveIsolation.ps1 -CheckLaunchProfileOnly
./scripts/Test-HostContract.ps1
```

QA checks execute BCL path/ownership helpers on artifacts only: token/profile restrictions, reopen, foreign owner,
nonempty unknown root, sibling prefix, traversal and junction. Negative cases require InvalidDataException, never
arbitrary reflection/sandbox failure. Compiled checks pin normal/QA separation, wiring and host fingerprints.
Harmony activation, native root setter/cold-storage state/save creation/callbacks are unexecuted. Junction creation
required sandbox escalation; both junction and target stay under artifacts.

## Native evidence and next step

Installed IL: Local+cloud-disabled CheckMove IL158–163→233 returns false without moving. Character PreSave skips
world migration; quota returns false. First .fch.new/.fch/.old writes stay in the new folder. ConsiderBackup chooses
first autobackup for a new name; absent old backup skips Delete, Local→Local Copy targets the same folder with
<name>_backup_auto-<timestamp>.fch. This covers fresh first writes, not arbitrary existing saves.

Next: recheck loaded Local world UID/player ID and closed single-player server immediately before preparing resources/supplied items/native
knowledge/physical stations with one-shot receipt and real count checks. Then provide a ready import/editor/build/
cancel/reload/network test series. Do not ask the owner to gather materials or treat this source stage as a ready stand.
