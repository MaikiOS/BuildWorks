# QA save isolation: checkpoint 0.19.90

**English** | [Русский](qa-save-isolation-0.19.90_RU.md)

Verified source preparation, **not a ready survival stand or game acceptance**. Installed .82; .90 is not
deployed. No Valheim launch or game-save mutation. With explicit owner permission only TerrainRamp-1.0-Test's
TerrainRamp bootstrap `[Bootstrap] CreateMissingSaves=true` changed to false; all other text/BOM/newlines preserved.
Local backup/hash proof: `artifacts/checkpoint-0.19.90/bootstrap-config-change.json` and `bootstrap-config-before.cfg`.

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

Next: verify loaded Local world UID/player ID and closed single-player server; prepare resources/supplied items/native
knowledge/physical stations with one-shot receipt and real count checks. Then provide a ready import/editor/build/
cancel/reload/network test series. Do not ask the owner to gather materials or treat this source stage as a ready stand.
