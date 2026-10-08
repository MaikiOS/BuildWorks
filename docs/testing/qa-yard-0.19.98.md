# QA yard 0.19.98

## QA .101 — native spawn teleport cooldown

.100 selected(-256,43.4301529,-768), then nativeTeleportTo refused beforebackup/terrain.
.101 waits2.1s and rechecks the original player/session before teleporting.
Bothbuilds0/0, QA82, cooldowncompiledguard, host, localization853 andformat passed.
Native .101 yard/construction/reload acceptance remainsopen.


## QA .100 — automated checks passed; native acceptance remains open

Native .99 refused before plan/terrain: bounded4096 search found minimum dry span18.5402069m.
.100 changes both native terrain bounds only for explicitly marked ZDO terrain in the
original isolated QA world: +/-16m during preparation and reload. Original token,
world name/UID, Local closed server and no peers are required. Normal build omits the
patch; unmarked QA terrain retains +/-8m. The requested192x192m footprint is unchanged.
Both builds0/0, QA82, host and localization853 passed; old99 fails the new regression guard.
This QA terrain requires the QA binary after reload; it is not a product terrain feature.
Native backup precedes terrain changes; supplies and previous receipts are never reset.
CoffeeTable construction and world-reload acceptance remain open.

## QA .99 search follow-up

Native .98 enabled noon but found no qualifying192m yard within +/-1024m; it refused
before writing a yard plan/receipt or changing terrain. .99 expands only the bounded
search/plan extent to +/-4096m (4225 candidate centres); size, dry Meadows and native
height limits remain. Both builds0/0, QA80, host, localization853 and affected format
passed. Native .99 and imported construction/reload remain open. Earlier statuses are history.

Source checks passed; .98 installed with matching hashes and all32 existing QA files unchanged. Native yard creation is pending. This is a
test-only extension of the existing isolated stand, not a terrain feature for players.

The owner requested a distant large construction area and permanent day. In the same
closed Local BW_QA_43d3719a world, the QA build accepts only qa-yard.request containing
the current isolation token. It selects a dry Meadows square, 192x192m, with its centre
at least 512m from origin. A bounded scan can refuse unsuitable terrain. The whole
footprint must be loaded, unprotected and unmodified; existing pieces/storage or foreign
behaviours refuse preparation. Native leveling stays within the eight-metre limit.

Before terrain/object changes, native world saving must finish and primary files are
copied to the isolated root's yard-before directory. A started receipt precedes changes;
incomplete attempts refuse automatic replay. Native TerrainComp owner operations save
terrain to ZDO data; the final world save must acknowledge completion. Vegetation is
removed without drops only inside this QA area. Stations have a separate one-shot receipt;
the original supplies receipt is retained. Midday is reapplied after world entry and
previous lighting restored at logout. No debugmode/free-cost/global keys are enabled.

Root: TerrainRamp-1.0-Test/BepInEx/config/BuildWorks/qa-saves/43d3719a7ee649d184e26ef1daa441a3.
Plan: qa-yard-plan.txt (X/Y/Z); receipts: qa-yard.txt and qa-yard-stations.txt.
Do not delete/reset these files to retry an incomplete attempt. Inspect the log and backup.

Native acceptance, after hash-checked installation and manager Modded launch:

1. Select the existing BW_QA_43d3719a character and world, closed Local. Preparation must
   place the character on the distant area, report 192x192m coordinates, save terrain,
   and complete both yard receipts. Error or started-only receipt means failure.
2. Inspect the terrain, stations and noon. These must be real world objects/terrain;
   a receipt or debug log alone does not prove visible success or reload.
3. Hammer -> ЧЕРТЕЖИ -> QA97 CoffeeTable. This saved four-piece copy was already imported,
   edited and reopened in the real editor on .97; do not repeat that series without cause.
   With a workbench in range, place using F9 -> УСТАНОВИТЬ. Expect the same four-part
   scale/transform and native cost 6 ashwood; a refusal, partial build or wrong cost fails.
4. Exit normally, launch through TerrainRamp-1.0-Test -> Modded again and enter the same
   saves. Compare yard terrain, stations, noon and the placed table. Check supply/station
   receipts were not reset and resources were not reissued. Reload remains unverified
   until this is observed. Network/admin/item/container/heavy-blueprint tests are separate.

Automation: normal/QA Release builds 0 warnings/errors, QA80 BCL cases plus compiled
wiring, HostContract, Localization853 and both formatting passes. verify is unavailable.
The old .97 pair fails the new compiled yard-presence guard. None of these tests executes
native terrain saving or proves blueprint construction. Independent correctness and Ponytail reviews passed after the post-wait protection fixes.

Native .97 evidence: stations prepared; supplies not reissued; CoffeeTable imported,
edited, saved as QA97 CoffeeTable and reopened with X=-0.728 on the edited third part.
Cancellation removed zero pieces. Placement inside spawn protection refused; after moving
out, the workbench was out of range. Construction/reload remain open. Local screenshots:
artifacts/checkpoint-0.19.97/native-qa (private; not included in source Git).
